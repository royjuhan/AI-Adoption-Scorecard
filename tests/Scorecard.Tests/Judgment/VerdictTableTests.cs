using Scorecard.Core.Judgment;
using Scorecard.Tests.Classification;

namespace Scorecard.Tests.Judgment;

// 스펙 4.1·4.3 판정 표와 6.5 우선순위(잠정 결정 1·2). 카드 수용 기준 2.
// 판정 표를 위에서부터 보고 처음 걸리는 행이 판정이다. 보류 → 조건부 → 통과. 문장은 표의 틀을 4.5 값으로 채운 것이다.
// 장면의 문턱은 모두 적는다(기본값 표시는 ThresholdDefaultTests가 본다).
[Trait("Spec", "judgment")]
public class VerdictTableTests
{
    // 4.3 표의 (근거 키, 걸리는 결과). PASS의 결과 칸은 —다.
    public static TheoryData<string, string> VerdictRowKeys =>
        ClassificationInput.TheoryPairs(JudgmentSpec.VerdictRows().Select(r => (r.Rule, r.Result ?? JudgmentSpec.None)));

    // 6.5 표의 행 번호.
    public static TheoryData<int> PriorityRows => new(Enumerable.Range(0, JudgmentSpec.Table(JudgmentSpec.PriorityHeader).Count));

    // 표 모양: 순서는 1부터 늘어나고, 같은 순서의 행은 근거 키가 같다. 근거 키 일곱이 모두 있고 PASS가 마지막이다.
    [Fact]
    public void Verdict_table_orders_rules_from_hold_to_pass()
    {
        var rows = JudgmentSpec.VerdictRows();

        Assert.Equal(rows.Select(r => r.Order).Order(), rows.Select(r => r.Order));
        Assert.Equal(Enumerable.Range(1, rows.Max(r => r.Order)), rows.Select(r => r.Order).Distinct());
        Assert.All(rows.GroupBy(r => r.Order), g => Assert.Single(g.Select(r => r.Rule).Distinct()));
        Assert.Equal(
            [JudgmentSpec.NotVerifiedRule, JudgmentSpec.Accuracy, JudgmentSpec.Missing, JudgmentSpec.Unexplained, JudgmentSpec.Time, JudgmentSpec.Focus, JudgmentSpec.PassRule],
            rows.Select(r => r.Rule).Distinct());
        Assert.Null(rows[^1].Result);
        Assert.Equal(JudgmentSpec.Pass, rows[^1].Verdict);

        // 보류가 조건부보다 앞이다(4.1 3).
        var firstConditional = rows.First(r => r.Verdict == JudgmentSpec.Conditional).Order;
        Assert.All(rows.Where(r => r.Verdict == JudgmentSpec.Hold), r => Assert.True(r.Order < firstConditional));
    }

    // 4.3: 행마다 그 행에 걸리는 장면의 판정·근거 키·판정 문장. 확인 결과도 그 행의 결과다.
    [Theory]
    [MemberData(nameof(VerdictRowKeys))]
    public void Each_verdict_row_gives_its_verdict_rule_and_sentence(string rule, string result)
    {
        var row = JudgmentSpec.FindVerdict(rule, result == JudgmentSpec.None ? null : result);

        if (rule == JudgmentSpec.NotVerifiedRule)
        {
            var thresholds = JudgmentInput.ThresholdsYaml("90", "5", "false");
            var definition = result == "no_inputs" ? JudgmentInput.NoInputsCase(thresholds) : JudgmentInput.NoCompareCase(thresholds);
            var notVerified = JudgmentInput.JudgeNotVerified(definition);

            Assert.Equal((row.Verdict, row.Rule, row.Template), (notVerified.Verdict, notVerified.Rule, notVerified.Reason));
            Assert.Empty(notVerified.Checks);
            return;
        }

        var scene = SceneFor(rule, result);
        var judged = scene.Judge();

        Assert.Equal((row.Verdict, row.Rule), (judged.Verdict, judged.Rule));
        Assert.Equal(JudgmentSpec.Fill(row.Template, scene.Slots()), judged.Reason);
        if (row.Result is not null)
        {
            Assert.Equal(row.Result, JudgmentInput.CheckOf(judged, rule).Result);
        }
    }

    // 6.5: 확인 다섯의 결과 조합을 내는 장면에서 확인 다섯이 표와 같고, 판정·근거 키가 표와 같다.
    // 판정 문장은 4.3 표에서 그 근거 키·결과의 틀이다.
    [Theory]
    [MemberData(nameof(PriorityRows))]
    public void Priority_table_row_gives_its_verdict(int index)
    {
        var cells = JudgmentSpec.Table(JudgmentSpec.PriorityHeader)[index].Select(JudgmentSpec.Cell).ToArray();
        var scene = JudgmentInput.Checks(cells[0], cells[1], cells[2], cells[3], cells[4]);

        var judged = scene.Judge();

        Assert.Equal(JudgmentSpec.CheckOrder(), judged.Checks.Select(c => c.Rule));
        Assert.Equal(cells[..5], JudgmentInput.Results(judged));
        Assert.Equal((cells[5], cells[6]), (judged.Verdict, judged.Rule));

        var result = cells[6] == JudgmentSpec.PassRule ? null : JudgmentInput.CheckOf(judged, cells[6]).Result;
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindVerdict(cells[6], result).Template, scene.Slots()), judged.Reason);
    }

    // 6.5 표가 판정 넷 가운데 미검증 밖의 셋과 근거 키 여섯(미검증 밖)을 모두 쓴다(빈 통과 방지).
    [Fact]
    public void Priority_table_covers_every_measured_rule()
    {
        var rows = JudgmentSpec.Table(JudgmentSpec.PriorityHeader).Select(c => c.Select(JudgmentSpec.Cell).ToArray()).ToArray();

        Assert.Equal(
            JudgmentSpec.VerdictRows().Where(r => r.Rule != JudgmentSpec.NotVerifiedRule).Select(r => r.Rule).Distinct().Order(StringComparer.Ordinal),
            rows.Select(r => r[6]).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(
            new[] { JudgmentSpec.Pass, JudgmentSpec.Conditional, JudgmentSpec.Hold }.Order(StringComparer.Ordinal),
            rows.Select(r => r[5]).Distinct().Order(StringComparer.Ordinal));
    }

    // 4.1 5(잠정 결정 2): 정확도는 충족하나 누락률이 문턱을 넘으면 보류(MISSING)다. 조건부·통과가 아니다.
    [Fact]
    public void Missing_over_threshold_with_accuracy_met_is_hold()
    {
        var judged = JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.NotMet, JudgmentInput.Explained, JudgmentInput.Met, JudgmentInput.Spread).Judge();

        Assert.Equal((JudgmentEngine.VerdictHold, JudgmentEngine.RuleMissing), (judged.Verdict, judged.Rule));
    }

    // 4.1 2: 확인 다섯을 늘 모두 계산해 남긴다. 다섯이 모두 걸려도 판정은 처음 걸리는 정확도 미달이다.
    [Fact]
    public void Checks_always_keep_all_five_results()
    {
        var judged = JudgmentInput.Checks(JudgmentInput.NotMet, JudgmentInput.NotMet, JudgmentInput.Found, JudgmentInput.NotMet, JudgmentInput.Found).Judge();

        Assert.Equal(JudgmentSpec.CheckOrder(), judged.Checks.Select(c => c.Rule));
        Assert.Equal([JudgmentInput.NotMet, JudgmentInput.NotMet, JudgmentInput.Found, JudgmentInput.NotMet, JudgmentInput.Found], JudgmentInput.Results(judged));
        Assert.Equal((JudgmentEngine.VerdictHold, JudgmentEngine.RuleAccuracy), (judged.Verdict, judged.Rule));
    }

    // 4.3 행(근거 키, 결과)에 걸리는 장면.
    internal static JudgmentScene SceneFor(string rule, string result) => (rule, result) switch
    {
        (JudgmentSpec.Accuracy, JudgmentInput.NotMet) => JudgmentInput.Checks(JudgmentInput.NotMet, JudgmentInput.Met, JudgmentInput.NotReviewed, JudgmentInput.Off, JudgmentInput.Spread),
        (JudgmentSpec.Missing, JudgmentInput.NotMet) => JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.NotMet, JudgmentInput.NotReviewed, JudgmentInput.Off, JudgmentInput.Spread),
        (JudgmentSpec.Unexplained, JudgmentInput.Found) => JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.Found, JudgmentInput.Off, JudgmentInput.Spread),
        (JudgmentSpec.Time, JudgmentInput.NotMeasured) => JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.NoneResult, JudgmentInput.NotMeasured, JudgmentInput.Few),
        (JudgmentSpec.Time, JudgmentInput.NotMet) => JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.NoneResult, JudgmentInput.NotMet, JudgmentInput.Few),
        (JudgmentSpec.Focus, JudgmentInput.Found) => JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.NotReviewed, JudgmentInput.Off, JudgmentInput.Found),
        (JudgmentSpec.PassRule, JudgmentSpec.None) => JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.NoneResult, JudgmentInput.Off, JudgmentInput.Few),
        _ => throw new ArgumentOutOfRangeException(nameof(rule), $"{rule} {result}", "4.3 표에 이 행의 장면이 없다. 표가 바뀌었으면 장면을 더한다."),
    };
}
