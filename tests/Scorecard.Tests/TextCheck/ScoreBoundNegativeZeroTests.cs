using Scorecard.Core.Cases;

namespace Scorecard.Tests.TextCheck;

// 스펙 4.1·case-schema 4.6 정수 행: `-0`은 범위 밖(range)이다. scores.min·max도 같다.
// 바탕은 7.3 case.yaml 블록. 오류 코드·경로·줄·열·메시지를 본다.
[Trait("Spec", "text-check")]
public class ScoreBoundNegativeZeroTests
{
    [Theory]
    [InlineData("min", "  min: 1\n", "  min: -0\n")]
    [InlineData("max", "  max: 5\n", "  max: -0\n")]
    public void Negative_zero_bound_is_a_range_error(string key, string old, string @new)
    {
        var text = TextCheckInput.Text(TextCheckSpec.FileBlock(CaseParser.FileName));
        var at = text.IndexOf(old, StringComparison.Ordinal);
        Assert.True(at >= 0 && text.IndexOf(old, at + 1, StringComparison.Ordinal) < 0);
        var line = text[..at].Count(c => c == '\n') + 1;
        text = text.Replace(old, @new, StringComparison.Ordinal);

        var parsed = CaseParser.Parse(TextCheckInput.Bytes(text), CaseParser.FileName);

        Assert.Null(parsed.Case);
        var error = Assert.Single(parsed.Errors);
        Assert.Equal("range", error.Code);
        Assert.Equal($"scores.{key}", error.Path);
        Assert.Equal(line, error.Line);
        Assert.Equal(key.Length + 5, error.Column);
        Assert.Equal("범위 밖이다 (0 이상 100 이하): -0", error.Message);
        Assert.Equal(CaseParser.FileName, error.Source);
    }
}
