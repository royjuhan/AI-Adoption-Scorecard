using System.Globalization;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Comparison;

// 스펙 4.5 허용 오차와 칸 판정 표(승인 질문 8·10). 카드 수용 기준 3(허용오차 경계).
// d = |결과 − 기준|(decimal). 절대: d ≤ tolerance. 백분율: d × 100 ≤ |기준| × tolerance_pct(기준 값 기준, 나눗셈 없음).
// 경계 포함. 기준 0이면 같은 값만. 텍스트 필드는 정규화 뒤 서수 비교.
[Trait("Spec", "metrics")]
public class ToleranceTests
{
    private const string BoundaryLinePrefix = "- 경계 행(";

    // 표의 행마다 판정이 같다.
    [Fact]
    public void Judgement_table_rows_match()
    {
        var rows = MetricsSpec.Table(MetricsSpec.ToleranceHeader);
        Assert.NotEmpty(rows);

        var wrong = rows
            .Select(c => (Row: string.Join(" | ", c), Expected: MetricsSpec.Cell(c[3]), Actual: CompareInput.Judge(MetricsSpec.Cell(c[0]), MetricsSpec.Cell(c[1]), MetricsSpec.Cell(c[2]))))
            .Where(r => r.Actual != r.Expected)
            .Select(r => $"{r.Row}: '{r.Actual}'")
            .ToArray();
        Assert.True(wrong.Length == 0, string.Join("\n", wrong));

        var results = rows.Select(c => MetricsSpec.Cell(c[3])).Distinct().Order(StringComparer.Ordinal);
        Assert.Equal(
            new[] { MetricsSpec.Same, CompareEngine.ResultNotNumber, CompareEngine.ResultOutside, CompareEngine.ResultWithin }.Order(StringComparer.Ordinal),
            results);
    }

    // 표의 within 행은 모두 정확히 같은 차이(d = 허용량)이고, 결과를 기준에서 0.000001 더 멀리 두면 outside다.
    // 스펙 4.5 "경계 행(…)" 줄의 쌍이 모두 표의 within 행이다.
    [Fact]
    public void Within_rows_are_exact_boundaries_and_next_value_is_outside()
    {
        var within = MetricsSpec.Table(MetricsSpec.ToleranceHeader)
            .Where(c => MetricsSpec.Cell(c[3]) == CompareEngine.ResultWithin)
            .Select(c => (Field: CompareInput.Field(MetricsSpec.Cell(c[0])), Reference: MetricsSpec.Cell(c[1]), Candidate: MetricsSpec.Cell(c[2])))
            .ToArray();
        Assert.NotEmpty(within);

        foreach (var (field, reference, candidate) in within)
        {
            var r = Number(reference);
            var c = Number(candidate);
            var d = Math.Abs(c - r);
            if (field.Tolerance is { } t)
            {
                Assert.True(d == t, $"{reference}→{candidate}: 차이 {d} ≠ tolerance {t}");
            }
            else
            {
                Assert.True(d * 100 == Math.Abs(r) * field.TolerancePct!.Value, $"{reference}→{candidate}: 차이 {d} × 100 ≠ |기준| × {field.TolerancePct}");
            }

            var next = c + (c > r ? 0.000001m : -0.000001m);
            Assert.Equal(CompareEngine.ResultOutside, CompareInput.Judge(field, reference, next.ToString(CultureInfo.InvariantCulture)));
        }

        // 줄의 코드 조각 중 숫자만(끝의 `outside`는 뺀다). 앞에서부터 둘씩 기준·결과다.
        var spans = SpecDocument.CodeSpans(SpecDocument.Line(MetricsSpec.Document, BoundaryLinePrefix))
            .Where(s => decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _))
            .ToArray();
        Assert.True(spans.Length >= 10 && spans.Length % 2 == 0, "경계 행 줄의 `기준`→`결과` 쌍을 읽을 수 없다.");
        var pairs = spans.Chunk(2).Select(p => (p[0], p[1])).ToArray();
        Assert.All(pairs, p => Assert.Contains(within, w => (w.Reference, w.Candidate) == p));
    }

    // 넘침 6종(4.5 넘침 규칙). 예외가 새지 않는다.
    // d가 넘치면 outside. d × 100만 넘치면 outside. |기준| × tolerance_pct만 넘치면 within. 둘 다 넘치면 d ≤ |기준| × (tolerance_pct / 100).
    [Theory]
    [InlineData("1", null, "79228162514264337593543950335", "-79228162514264337593543950335", "outside")]
    [InlineData(null, "1.0", "79228162514264337593543950335", "-79228162514264337593543950335", "outside")]
    [InlineData(null, "79228162514264337593543950335", "1e10", "0", "within")]
    [InlineData(null, "1.0", "1", "1e27", "outside")]
    [InlineData(null, "1000000000000000000000000000", "1e27", "-1e27", "within")]
    [InlineData(null, "1.0", "79228162514264337593543950335", "79228162514264337593543950335", MetricsSpec.Same)]
    public void Overflow_never_throws(string? tolerance, string? tolerancePct, string reference, string candidate, string expected)
    {
        var field = new CompareField("v", Parse(tolerance), Parse(tolerancePct));

        Assert.Equal(expected, CompareInput.Judge(field, reference, candidate));
    }

    // 결과 칸의 앞뒤 공백은 뗀다(숫자 필드·텍스트 필드 모두).
    [Theory]
    [InlineData("tolerance: 0", "5", " 5 ")]
    [InlineData("tolerance_pct: 1.0", "30.77", "\t30.77\u00A0")]
    [InlineData("없음", "item-a", " item-a ")]
    public void Candidate_spaces_are_trimmed(string tolerance, string reference, string candidate)
    {
        Assert.Equal(MetricsSpec.Same, CompareInput.Judge(tolerance, reference, candidate));
    }

    // 백분율 허용 오차는 기준 값 기준이다. 결과 값 기준이면 99 → 100도 within이 된다.
    [Fact]
    public void Percent_tolerance_uses_reference_value()
    {
        Assert.Equal(CompareEngine.ResultWithin, CompareInput.Judge("tolerance_pct: 1.0", "100", "99"));
        Assert.Equal(CompareEngine.ResultOutside, CompareInput.Judge("tolerance_pct: 1.0", "99", "100"));
    }

    private static decimal Number(string text) =>
        decimal.Parse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture);

    private static decimal? Parse(string? text) => text is null ? null : decimal.Parse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
}
