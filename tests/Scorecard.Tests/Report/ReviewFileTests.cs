using System.Globalization;
using System.Text;
using System.Text.Json;
using Scorecard.Core.Report;
using Scorecard.Tests.Cli;

namespace Scorecard.Tests.Report;

// 확인 파일(--review) 모양과 오류(report.md 5절, 데이터 5절 표). 사유 없는 바꾸기는 확정되지 않고 입력 오류(2)다.
[Trait("Spec", "report")]
public class ReviewFileTests
{
    private static readonly int[] SevenSteps = [1, 2, 3, 4, 5, 6, 7];

    // 데이터 5절 오류 표: 행마다 오류 하나, 코드·줄·열·경로·메시지가 같다. 단계는 1~7.
    [Fact]
    public void Review_error_table_rows_hold()
    {
        var rows = ReportInput.Table(ReportInput.ReviewErrorTableHeader);
        Assert.NotEmpty(rows);
        foreach (var row in rows)
        {
            var content = CliInput.Cell(row[0]);
            var (entries, errors) = ReviewParser.Parse(Encoding.UTF8.GetBytes(content + "\n"), "review.yaml", SevenSteps);

            Assert.Null(entries);
            var e = Assert.Single(errors);
            Assert.Equal(
                (CliInput.Cell(row[1]), int.Parse(CliInput.Cell(row[2]), CultureInfo.InvariantCulture), int.Parse(CliInput.Cell(row[3]), CultureInfo.InvariantCulture), CliInput.Cell(row[4]), CliInput.Cell(row[5])),
                (e.Code, e.Line, e.Column, e.Path, e.Message));
            Assert.Equal("review.yaml", e.Source);
        }
    }

    // 맞음은 사유 없이 확정, 바꾸기는 담당과 사유 한 줄로 확정. 적지 않은 단계는 확인 전. 번호 순서로 돌려준다.
    [Fact]
    public void Keep_and_change_with_reason_are_confirmed()
    {
        const string text = "steps:\n  - {number: 4, answer: change, assignee: ②M, reason: 지난 기록으로 학습한 모델을 고정해 쓴다}\n  - {number: 2, answer: keep}\n";

        var (entries, errors) = ReviewParser.Parse(Encoding.UTF8.GetBytes(text), "review.yaml", SevenSteps);

        Assert.Empty(errors);
        Assert.Equal(
            [new ReviewEntry(2, "keep", null, null), new ReviewEntry(4, "change", "②M", "지난 기록으로 학습한 모델을 고정해 쓴다")],
            entries!);
    }

    // 빈 목록도 받는다(모두 확인 전).
    [Fact]
    public void Empty_step_list_is_valid()
    {
        var (entries, errors) = ReviewParser.Parse(Encoding.UTF8.GetBytes("steps: []\n"), "review.yaml", SevenSteps);

        Assert.Empty(errors);
        Assert.Empty(entries!);
    }

    // 명령: 사유 없는 바꾸기 → 종료 2, 오류 JSON source는 파일 이름. 스코어카드를 쓰지 않는다.
    [Fact]
    public void Open_with_change_without_reason_fails_and_writes_nothing()
    {
        using var work = ReportInput.Workspace("signoff-final");
        var review = work.PathOf("review.yaml");
        File.WriteAllText(review, "steps:\n  - number: 3\n    answer: change\n    assignee: ④\n");
        var output = work.PathOf("o");
        Assert.Equal(0, CliInput.Run("start", work.Case, "--answers", Path.Combine(work.Case, "answers.yaml"), "--out", output).Exit);

        var run = CliInput.Run("open", work.Case, "--out", output, "--review", review, "--json");

        var e = Assert.Single(CliErrorTests.ReadErrorJson(run.Stdout, 2));
        Assert.Equal(("required", "review.yaml", "steps[1].reason"), (e.Code, e.Source, e.Path));
        Assert.False(File.Exists(Path.Combine(output, ScorecardJson.FileName)));
        Assert.False(File.Exists(Path.Combine(output, "scorecard.html")));
    }

    // 확인 파일이 없으면 file 오류(2). source는 파일 이름이다(절대 경로 없음).
    [Fact]
    public void Missing_review_file_is_a_file_error()
    {
        using var work = ReportInput.Workspace("signoff-final");
        var output = work.PathOf("o");
        Assert.Equal(0, CliInput.Run("start", work.Case, "--answers", Path.Combine(work.Case, "answers.yaml"), "--out", output).Exit);

        var run = CliInput.Run("open", work.Case, "--out", output, "--review", work.PathOf("nope.yaml"), "--json");

        var e = Assert.Single(CliErrorTests.ReadErrorJson(run.Stdout, 2));
        Assert.Equal(("file", "nope.yaml", "파일이 없다"), (e.Code, e.Source, e.Message));
    }

    // 성공: 결과 객체에 확인 결과와 확인 파일 SHA-256이 남는다. 분류·판정 값은 그대로다(다시 계산하지 않는다).
    [Fact]
    public void Review_is_recorded_without_rejudging()
    {
        using var work = ReportInput.Workspace("signoff-final");
        var review = work.PathOf("review.yaml");
        File.WriteAllText(review, "steps:\n  - number: 6\n    answer: change\n    assignee: ④\n    reason: 해석 문장은 담당자가 직접 쓴다\n");
        var (plain, _) = ReportInput.Produce(work, "signoff-final");
        var (reviewed, _) = ReportInput.Produce(work, "signoff-final", "--review", review);

        using var a = JsonDocument.Parse(plain.Stdout);
        using var b = JsonDocument.Parse(reviewed.Stdout);
        var step = b.RootElement.GetProperty("steps")[5];
        Assert.Equal("③", step.GetProperty("assignee").GetString());
        Assert.Equal("changed", step.GetProperty("review").GetProperty("status").GetString());
        Assert.Equal("④", step.GetProperty("review").GetProperty("assignee").GetString());
        Assert.Equal("review.yaml", b.RootElement.GetProperty("review").GetProperty("path").GetString());
        Assert.Equal(1, b.RootElement.GetProperty("summary").GetProperty("changed").GetInt32());
        Assert.Equal(a.RootElement.GetProperty("verification").GetRawText(), b.RootElement.GetProperty("verification").GetRawText());
        Assert.Equal(a.RootElement.GetProperty("proposal").GetRawText(), b.RootElement.GetProperty("proposal").GetRawText());
        Assert.Contains(b.RootElement.GetProperty("risks").EnumerateArray(), r => r.GetProperty("key").GetString() == "review_changed");
    }
}
