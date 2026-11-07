using System.Globalization;
using Scorecard.Core.Comparison;

namespace Scorecard.Tests.Comparison;

// 스펙 4.6 텍스트 정규화(승인 질문 10·11). 카드 수용 기준 3(텍스트 정규화 단위 테스트).
// 모든 칸에 같은 정규화를 한 번: 칸 안 CRLF·CR → LF, 앞뒤 공백(char.IsWhiteSpace) 떼기. 그 뒤 서수 비교.
// 하지 않는 것: 대소문자, NFC·NFD, 안쪽 공백 합치기, 전각→반각, 숫자 모양 맞추기. 문화권과 상관없다.
[Trait("Spec", "metrics")]
public class TextNormalizationTests
{
    // 앞뒤 글자 표의 행마다: "예"면 그 글자를 칸 앞뒤에 붙여도 같음, "아니오"면 outside(텍스트 필드).
    [Fact]
    public void Trim_table_rows_match()
    {
        var rows = MetricsSpec.Table(MetricsSpec.TrimHeader);
        Assert.NotEmpty(rows);
        var wrong = new List<string>();

        foreach (var c in rows)
        {
            var code = MetricsSpec.Cell(c[0]);
            Assert.StartsWith("U+", code, StringComparison.Ordinal);
            var ch = char.ConvertFromUtf32(int.Parse(code[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
            var expected = MetricsSpec.YesNo(c[2]) ? MetricsSpec.Same : CompareEngine.ResultOutside;

            foreach (var (label, candidate) in new[] { ("앞뒤", ch + "item-a" + ch), ("앞", ch + "item-a"), ("뒤", "item-a" + ch) })
            {
                var actual = CompareInput.Judge(MetricsSpec.NoTolerance, "item-a", candidate);
                if (actual != expected)
                {
                    wrong.Add($"{code} {c[1]} {label}: '{actual}', 표는 '{expected}'");
                }
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    // 표의 "예" 글자는 char.IsWhiteSpace, "아니오" 글자는 아니다(스펙 규칙의 뜻 확인).
    [Fact]
    public void Trim_table_agrees_with_char_IsWhiteSpace()
    {
        foreach (var c in MetricsSpec.Table(MetricsSpec.TrimHeader))
        {
            var ch = (char)int.Parse(MetricsSpec.Cell(c[0])[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            Assert.True(char.IsWhiteSpace(ch) == MetricsSpec.YesNo(c[2]), $"{c[0]} {c[1]}");
        }
    }

    // NFC와 NFD는 다르다(가, é). 대소문자도 다르다.
    [Theory]
    [InlineData("가", "가")]
    [InlineData("é", "é")]
    [InlineData("Normal", "normal")]
    [InlineData("item-a", "Item-A")]
    [InlineData("12", "１２")]
    [InlineData("a b", "a  b")]
    [InlineData("10", "10.0")]
    public void Unnormalized_differences_are_outside(string reference, string candidate)
    {
        Assert.Equal(CompareEngine.ResultOutside, CompareInput.Judge(MetricsSpec.NoTolerance, reference, candidate));
    }

    // tr-TR에서도 I와 ı, i와 İ는 다르다(서수 비교).
    [Fact]
    public void Turkish_culture_does_not_change_comparison()
    {
        var results = CompareInput.WithCulture("tr-TR", () => new[]
        {
            CompareInput.Judge(MetricsSpec.NoTolerance, "I", "ı"),
            CompareInput.Judge(MetricsSpec.NoTolerance, "i", "İ"),
            CompareInput.Judge(MetricsSpec.NoTolerance, "TITLE", "title"),
        });

        Assert.Equal([CompareEngine.ResultOutside, CompareEngine.ResultOutside, CompareEngine.ResultOutside], results);
    }

    // 칸 안의 a\r\nb, a\rb는 a\nb와 같다. 차이 칸에는 LF로 바꾼 텍스트가 나온다.
    [Fact]
    public void Inner_line_breaks_become_lf()
    {
        Assert.Equal(MetricsSpec.Same, CompareInput.Judge(MetricsSpec.NoTolerance, "a\r\nb", "a\nb"));
        Assert.Equal(MetricsSpec.Same, CompareInput.Judge(MetricsSpec.NoTolerance, "a\rb", "a\nb"));

        var metrics = CompareInput.Measured(CompareInput.Run(
            CompareInput.Case(CompareInput.TextCase),
            "id,v\nk,\"a\r\nb\"\n",
            "id,v\nk,\"a\rc\"\n"));
        var difference = Assert.Single(Assert.Single(metrics.Items).Differences);
        Assert.Equal(("a\nb", "a\nc"), (difference.Reference, difference.Candidate));
    }

    // 정규화는 모든 칸에 같다: 머리 행·키·숫자 칸·텍스트 칸. 메모리 결과 CsvRow도 정규화 뒤 텍스트다(3.1).
    [Fact]
    public void Normalization_applies_to_every_cell_in_memory_rows()
    {
        var metrics = CompareInput.Measured(CompareInput.Run(
            CompareInput.Case(CompareInput.NumberAndTextCase),
            " id ,\tn,t , x\nk1 , 1 ,\"a\r\nb\", y \n",
            "id,n,t\nk1,2,c\n"));
        var item = Assert.Single(metrics.Items);

        Assert.Equal(["id", "n", "t", "x"], item.Reference!.Header);
        Assert.Equal(["k1", "1", "a\nb", "y"], item.Reference.Cells);
        Assert.Equal("k1", item.Key);
    }
}
