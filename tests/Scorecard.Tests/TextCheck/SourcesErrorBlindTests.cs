namespace Scorecard.Tests.TextCheck;

// 스펙 4.5: 출처 표를 끝까지 읽었으면(source·duplicate-key·pair 오류) 남은 행으로 blind 검사를 계속하고,
// 끝까지 못 읽었으면(empty-key 등) 점수표와 맞춰 보지 않는다. C# `ScoreSheet.Read`의 sourcesComplete.
[Trait("Spec", "text-check")]
public class SourcesErrorBlindTests
{
    private const string Sheet = "scores/sheet.csv";
    private const string Sources = "scores/sources.csv";

    [Theory]
    [InlineData("b02,d07,ai", "b01,d07,ai", "duplicate-key", true)]
    [InlineData("b02,d07,ai", ",d07,ai", "empty-key", false)]
    public void Sources_errors_decide_whether_blind_checks_continue(string oldRow, string newRow, string sourcesCode, bool blindContinues)
    {
        using var folder = TextCheckInput.Folder(TextCheckSpec.Example);
        var sources = TextCheckInput.Read(folder.Root, Sources);
        Assert.Contains(oldRow + "\n", sources, StringComparison.Ordinal);
        TextCheckInput.Write(folder.Root, Sources, sources.Replace(oldRow + "\n", newRow + "\n", StringComparison.Ordinal));

        var run = TextCheckInput.Run(folder.Root);

        Assert.False(run.IsValid);
        Assert.Contains(run.Errors, e => e.Source == Sources && e.Code == sourcesCode);
        var blind = run.Errors.Where(e => e.Source == Sheet && e.Code == "blind" && e.Message == "출처 표에 없는 가림 키다: b02").ToArray();
        Assert.Equal(blindContinues, blind.Length > 0);
        if (!blindContinues)
        {
            Assert.DoesNotContain(run.Errors, e => e.Source == Sheet && e.Code == "blind");
        }
    }
}
