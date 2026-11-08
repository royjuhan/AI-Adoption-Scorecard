using System.Globalization;
using Scorecard.Tests.Guards;

namespace Scorecard.Tests.Cli;

// 결정론과 원본 불변(json-output.md 7절, 지시문 2절 2·3).
[Trait("Spec", "json-output")]
public class CliDeterminismTests
{
    // 같은 입력 → 같은 바이트. 다시 돌림, 다른 경로의 사본, de-DE 문화권. 표준 출력과 출력 폴더 파일 모두.
    [Theory]
    [InlineData("table-compare")]
    [InlineData("reply-drafts")]
    [InlineData("mixed-exceptions")]
    public void Same_input_gives_same_bytes_across_runs_paths_and_cultures(string example)
    {
        var first = RunAll(example, culture: null);
        var again = RunAll(example, culture: null);
        var german = RunAll(example, CultureInfo.GetCultureInfo("de-DE"));

        Assert.Equal(first, again);
        Assert.Equal(first, german);
    }

    // 입력 불변: --out이 케이스 폴더 밖이면 케이스 폴더가 그대로다(경로·크기·SHA-256·수정 시각).
    // 기본 출력 폴더면 바뀌는 것은 새 out/ 폴더뿐이다.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Input_files_are_unchanged(bool explicitOut)
    {
        using var work = CliWorkspace.Create("table-compare");
        var answers = Path.Combine(work.Case, "answers.yaml");
        string[] outArgs = explicitOut ? ["--out", work.PathOf("o")] : [];
        var before = FolderSnapshot.Take(work.Case);

        Assert.Equal(0, CliInput.Run(["start", work.Case, "--answers", answers, .. outArgs, "--json"]).Exit);
        Assert.Equal(0, CliInput.Run(["check", work.Case, .. outArgs, "--json"]).Exit);
        Assert.Equal(0, CliInput.Run(["open", work.Case, .. outArgs, "--json"]).Exit);
        Assert.Equal(0, CliInput.Run("classify", work.Case, "--json").Exit);
        Assert.Equal(0, CliInput.Run("compare", work.Case, "--json").Exit);
        Assert.Equal(0, CliInput.Run("judge", work.Case, "--json").Exit);
        Assert.Equal(2, CliInput.Run("build", work.Case, "--json").Exit);

        Assert.Empty(FolderSnapshot.Compare(before, FolderSnapshot.Take(work.Case, exclude: "out"), compareTime: true));
        var added = FolderSnapshot.Take(work.Case).Select(e => e.Path).Except(before.Select(e => e.Path)).ToArray();
        Assert.Equal(
            explicitOut ? [] : ["out", "out/classification.json", "out/judgment.json", "out/metrics.json", "out/scorecard.html", "out/scorecard.json"],
            added.Order(StringComparer.Ordinal));
    }

    // 사본 하나에서 start·check·내부 명령 셋을 돌려 표준 출력과 출력 파일을 모은다. 사본마다 다른 임시 경로다.
    private static string[] RunAll(string example, CultureInfo? culture)
    {
        var saved = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            if (culture is not null)
            {
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }

            using var work = CliWorkspace.Create(example);
            var output = work.PathOf("o");
            var runs = new[]
            {
                CliInput.Run("start", work.Case, "--answers", Path.Combine(work.Case, "answers.yaml"), "--out", output, "--json"),
                CliInput.Run("check", work.Case, "--out", output, "--json"),
                CliInput.Run("classify", work.Case, "--json"),
                CliInput.Run("compare", work.Case, "--json"),
                CliInput.Run("judge", work.Case, "--json"),
                CliInput.Run("build", work.Case, "--json"),
            };
            var files = Directory.EnumerateFiles(output).Order(StringComparer.Ordinal)
                .Select(f => Path.GetFileName(f) + "\n" + CliInput.Text(File.ReadAllBytes(f)));
            var text = runs.Select(r => r.Exit + "\n" + r.Out + r.Err).Concat(files).ToArray();
            Assert.All(text, t => Assert.DoesNotContain(work.Root, t, StringComparison.Ordinal));
            return text;
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = saved;
        }
    }
}
