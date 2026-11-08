using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Cli;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.Report;

// 스코어카드 테스트 도우미(docs/spec/report.md, 데이터 docs/spec/data/report.md).
// 골든 HTML·JSON은 tests TestData/report/<이름>/에 둔다. examples/*/expected가 아니다(expected migration이 아니다).
internal static class ReportInput
{
    public const string SpecPath = "docs/spec/data/report.md";

    public const string PropertyTableHeader = "| 속성 | 형식 | 뜻 |";
    public const string StepPropertyTableHeader = "| 단계 속성 | 형식 | 뜻 |";
    public const string ZoneTableHeader = "| 구역 | id | 내용 | 결과 객체 |";
    public const string ReviewErrorTableHeader = "| 확인 파일 | 코드 | 줄 | 열 | 경로 | 메시지 |";
    public const string ProposalTableHeader = "| 예제 | 제안 단계 | ② 단계 |";

    // 골든을 다시 쓸 때만 1로 둔다. 쓰면 테스트는 실패한다(쓴 파일을 사람이 diff로 본 뒤 다시 돌린다).
    public const string UpdateVariable = "SCORECARD_UPDATE_GOLDEN";

    // 공개 예제 5종 + AI 판단 표본(확인 파일 포함).
    public const string AiSample = "ai-judgment";

    public static TheoryData<string> GoldenNames => new([.. Classification.ClassificationInput.AllExamples, AiSample]);

    public static string GoldenPath(string name, string file) => RepoRoot.Combine("tests/Scorecard.Tests/TestData/report/" + name + "/" + file);

    public static string AiAnswers => RepoRoot.Combine("tests/Scorecard.Tests/TestData/classification/ai-judgment/answers.yaml");

    public static string AiReview => GoldenPath(AiSample, "review.yaml");

    public static IReadOnlyList<string[]> Table(string header) => SpecDocument.Table(SpecPath, header);

    // 케이스 하나를 준비해 start(→ check) → open --json. open의 표준 출력과 출력 폴더를 돌려준다.
    // 예제는 start·check를 모두 돌린다. AI 표본은 답 파일만 있고 check를 돌리지 않는다(검증 전 모양).
    public static (CliRun Open, string Output) Produce(CliWorkspace work, string name, params string[] openExtra)
    {
        var output = work.PathOf("o");
        var answers = Path.Combine(work.Case, "answers.yaml");
        Assert.Equal(0, CliInput.Run("start", work.Case, "--answers", answers, "--out", output, "--json").Exit);
        if (name != AiSample)
        {
            Assert.Equal(0, CliInput.Run("check", work.Case, "--out", output, "--json").Exit);
        }

        var open = CliInput.Run(["open", work.Case, "--out", output, .. openExtra, "--json"]);
        return (open, output);
    }

    // 예제 사본 또는 AI 표본 케이스 폴더(답 파일 하나).
    public static CliWorkspace Workspace(string name)
    {
        if (name != AiSample)
        {
            return CliWorkspace.Create(name);
        }

        var work = CliWorkspace.Create("mixed-exceptions");
        Directory.Delete(work.Case, recursive: true);
        Directory.CreateDirectory(work.Case);
        File.Copy(AiAnswers, Path.Combine(work.Case, "answers.yaml"));
        return work;
    }

    public static string[] OpenExtra(string name) => name == AiSample ? ["--review", AiReview] : [];
}
