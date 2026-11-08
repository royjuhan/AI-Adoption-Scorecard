using System.Globalization;
using Scorecard.Core.Classification;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Classification;

// 스펙 4.2 조건 표(카드 수용 기준 1, 지시문 13절, 승인 결정 9).
// 분류 숫자·값을 코드에 박지 않는다. 표를 파일에서 읽어 Core 동작과 대조한다.
// 표 행마다 선택지 전부로 걸림·안 걸림을 본다. `<` 행은 값−1과 범위 최솟값이 걸리고, 값은 안 걸린다.
// 앞 분기를 피한 기준 답(6.1)에서 그 키 하나만 바꾼다. 같은 근거 키의 다른 행(그리고)은 걸리는 값으로 둔다.
[Trait("Spec", "classification")]
public class ConditionTableTests
{
    // 표 행마다 (근거 키, 키). 같은 근거 키 안에서 키는 겹치지 않는다.
    public static TheoryData<string, string> ConditionRows =>
        ClassificationInput.TheoryPairs(ClassificationSpec.Conditions().Select(c => (c.Rule, c.Key)));

    // 지시문 3.3·3.4와 스펙 4.7·4.8의 근거 키. 표에 빠지거나 더해지면 이 테스트가 먼저 알린다.
    [Fact]
    public void Condition_table_has_rows_for_every_rule()
    {
        var rules = ClassificationSpec.Conditions().Select(c => c.Rule).Distinct().ToArray();

        // schema 2(ADR 0013): CHECK_EXISTING 행을 지웠다.
        Assert.Equal(["SIGNOFF", "Q1", "Q2", "Q3", "Q4", "PARTIAL", "GATE", ClassificationSpec.OveruseRule], rules);
    }

    // 표의 키·값이 case-schema 4.4 표에 있는 키·선택지다. 비교는 =, in, < 세 가지다.
    [Fact]
    public void Condition_values_are_listed_choices()
    {
        var keys = ClassificationInput.Keys;
        foreach (var c in ClassificationSpec.Conditions())
        {
            Assert.Contains(c.Key, keys);
            Assert.Contains(c.Op, new[] { "=", "in", "<" });
            Assert.NotEmpty(c.Values);
            if (c.Op == "<")
            {
                var row = DefaultRow.ReadAll().Single(r => r.Key == c.Key);
                Assert.True(row.IsInteger, $"{c.Rule} {c.Key}: '<'는 정수 키에만 쓴다.");
                Assert.Single(c.Values);
            }
            else
            {
                Assert.All(c.Values, v => Assert.Contains(v, ClassificationInput.Choices(c.Key)));
                Assert.True(c.Op == "in" || c.Values.Length == 1, $"{c.Rule} {c.Key}: '='는 값이 하나다.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(ConditionRows))]
    public void Condition_row_hits_exactly_the_listed_values(string rule, string key)
    {
        var conditions = ClassificationSpec.Conditions();
        var row = conditions.Single(c => c.Rule == rule && c.Key == key);

        // 같은 근거 키의 다른 행은 걸리는 값으로 둔다.
        var context = ClassificationInput.With(
            ClassificationInput.Base,
            conditions.Where(c => c.Rule == rule && c.Key != key).Select(c => (c.Key, HitValue(c))));

        var probes = Probes(row).ToArray();
        Assert.Contains(probes, p => p.Hit);
        Assert.Contains(probes, p => !p.Hit);
        foreach (var (value, expected) in probes)
        {
            var step = ClassificationInput.One(ClassificationInput.With(context, key, value));
            Assert.True(
                expected == Hits(rule, step),
                $"{rule}: {key} = {value}이면 {(expected ? "걸려야" : "안 걸려야")} 한다. 행: {string.Join(" + ", ClassificationInput.RowLabels(step))}");
        }
    }

    // 스펙 4.2 경계: rare·4건 → Q4, rare·5건 → ELSE, weekly·1건 → ELSE, monthly·4건 → ELSE.
    [Theory]
    [InlineData("rare", 4, "Q4")]
    [InlineData("rare", 5, "ELSE")]
    [InlineData("weekly", 1, "ELSE")]
    [InlineData("monthly", 4, "ELSE")]
    public void Q4_boundary(string frequency, int batchSize, string expectedRule)
    {
        var a = ClassificationInput.Answers($"frequency: {frequency}, batch_size: {batchSize.ToString(CultureInfo.InvariantCulture)}");

        Assert.Equal(expectedRule, ClassificationInput.One(a).Rows[0].Rule);
    }

    // 걸림 판단. OVERUSE는 행을 만들지 않는다(4.2 아래 글머리).
    private static bool Hits(string rule, StepClassification step) => rule switch
    {
        ClassificationSpec.OveruseRule => step.AiOveruseCandidate,
        _ => step.Rows.Any(r => r.Rule == rule),
    };

    // 걸리는 값 하나. =·in은 첫 값, <는 값−1.
    private static string HitValue(Condition c) =>
        c.Op == "<"
            ? (int.Parse(c.Values.Single(), CultureInfo.InvariantCulture) - 1).ToString(CultureInfo.InvariantCulture)
            : c.Values[0];

    // =·in: 4.4 선택지 전부. <: 값−1(걸림), 범위 최솟값(걸림), 값(안 걸림), 값+1(안 걸림).
    private static IEnumerable<(string Value, bool Hit)> Probes(Condition c)
    {
        if (c.Op == "<")
        {
            var limit = int.Parse(c.Values.Single(), CultureInfo.InvariantCulture);
            var min = (int)DefaultRow.ReadAll().Single(r => r.Key == c.Key).Bounds.Min;
            yield return (Invariant(limit - 1), true);
            yield return (Invariant(min), min < limit);
            yield return (Invariant(limit), false);
            yield return (Invariant(limit + 1), false);
            yield break;
        }

        foreach (var choice in ClassificationInput.Choices(c.Key))
        {
            yield return (choice, c.Values.Contains(choice, StringComparer.Ordinal));
        }
    }

    private static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);
}
