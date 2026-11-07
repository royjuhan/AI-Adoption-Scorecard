using System.Globalization;
using System.Text;
using Scorecard.Core.Comparison;

namespace Scorecard.Tests.Comparison;

// 스펙 4.8 퍼센트(승인 질문 12). 카드 수용 기준 1(90.0%·3.3%).
// 소수 첫째 자리. 0.05는 올린다(0에서 먼 쪽). 정수로 셈한다. 결과는 늘 소수 한 자리다.
[Trait("Spec", "metrics")]
public class PercentTests
{
    // 표의 행마다: 기준 행 whole 중 part 행이 일치하면 정확도가 표의 값이다. 소수 자릿수는 1이다.
    [Fact]
    public void Accuracy_table_rows_match()
    {
        var rows = MetricsSpec.Table(MetricsSpec.PercentHeader);
        Assert.NotEmpty(rows);

        var wrong = new List<string>();
        foreach (var c in rows)
        {
            var (part, whole, expected) = (MetricsSpec.Int(c[0]), MetricsSpec.Int(c[1]), MetricsSpec.Cell(c[2]));
            var metrics = Accuracy(part, whole);
            var actual = metrics.AccuracyPct!.Value;
            if (actual.ToString(CultureInfo.InvariantCulture) != expected || actual.Scale != 1)
            {
                wrong.Add($"{part}/{whole}: {actual.ToString(CultureInfo.InvariantCulture)}(자릿수 {actual.Scale}), 표는 {expected}");
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    // 누락률도 같은 셈이다. 기준 whole 행 중 part 행이 결과에 없다.
    [Fact]
    public void Missing_rate_uses_the_same_rule()
    {
        var wrong = new List<string>();
        foreach (var c in MetricsSpec.Table(MetricsSpec.PercentHeader))
        {
            var (part, whole, expected) = (MetricsSpec.Int(c[0]), MetricsSpec.Int(c[1]), MetricsSpec.Cell(c[2]));
            var metrics = Missing(part, whole);
            var actual = metrics.MissingRatePct!.Value;
            if (actual.ToString(CultureInfo.InvariantCulture) != expected || actual.Scale != 1)
            {
                wrong.Add($"{part}/{whole}: {actual.ToString(CultureInfo.InvariantCulture)}(자릿수 {actual.Scale}), 표는 {expected}");
            }

            Assert.Equal(part, metrics.Counts!.Missing);
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    // 작은 범위 전부(기준 행 1~40): Math.Round(part × 100 / whole, 1, AwayFromZero)와 같고 자릿수는 1이다.
    // 0.05 경계(1/8 = 12.5, 1/16 = 6.25, 1/40 = 2.5 …)에서 ToEven이 아니다.
    [Fact]
    public void Accuracy_rounds_half_away_from_zero_with_one_digit()
    {
        var wrong = new List<string>();
        for (var whole = 1; whole <= 40; whole++)
        {
            for (var part = 0; part <= whole; part++)
            {
                var expected = Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero);
                var actual = Accuracy(part, whole).AccuracyPct!.Value;
                if (actual != expected || actual.Scale != 1)
                {
                    wrong.Add($"{part}/{whole}: {actual.ToString(CultureInfo.InvariantCulture)}, {expected.ToString(CultureInfo.InvariantCulture)}이어야 한다");
                }
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    // 기준 whole 행 중 앞 part 행은 일치, 나머지는 값 불일치(텍스트 필드).
    private static CompareMetrics Accuracy(int part, int whole)
    {
        var reference = new StringBuilder("id,v\n");
        var candidate = new StringBuilder("id,v\n");
        for (var i = 0; i < whole; i++)
        {
            reference.Append(CultureInfo.InvariantCulture, $"k{i},a\n");
            candidate.Append(CultureInfo.InvariantCulture, $"k{i},{(i < part ? "a" : "b")}\n");
        }

        var metrics = CompareInput.Measured(CompareInput.Run(CompareInput.Case(CompareInput.TextCase), reference.ToString(), candidate.ToString()));
        Assert.Equal((whole, part), (metrics.Counts!.Reference, metrics.Counts.Match));
        return metrics;
    }

    // 기준 whole 행 중 앞 part 행은 결과에 없다. 나머지는 일치.
    private static CompareMetrics Missing(int part, int whole)
    {
        var reference = new StringBuilder("id,v\n");
        var candidate = new StringBuilder("id,v\n");
        for (var i = 0; i < whole; i++)
        {
            reference.Append(CultureInfo.InvariantCulture, $"k{i},a\n");
            if (i >= part)
            {
                candidate.Append(CultureInfo.InvariantCulture, $"k{i},a\n");
            }
        }

        return CompareInput.Measured(CompareInput.Run(CompareInput.Case(CompareInput.TextCase), reference.ToString(), candidate.ToString()));
    }
}
