using Scorecard.Tests.Classification;

namespace Scorecard.Tests.Judgment;

// 스펙 4.4 확인 다섯, 4.5 숫자(focus_min), 6.1 문턱 경계(잠정 결정 1·5). 카드 수용 기준 2.
// 확인 문장은 표의 틀을 4.5 값으로 채운 것이다. 문턱 비교는 셈(counts)으로 한다. 표시 퍼센트로 비교하지 않는다(4.1 4).
[Trait("Spec", "judgment")]
public class CheckTableTests
{
    // 4.4 표의 (확인, 결과).
    public static TheoryData<string, string> CheckRowKeys =>
        ClassificationInput.TheoryPairs(JudgmentSpec.CheckRows().Select(r => (r.Check, r.Result)));

    // 6.1 표의 행 번호.
    public static TheoryData<int> BoundaryRows => new(Enumerable.Range(0, JudgmentSpec.Table(JudgmentSpec.BoundaryHeader).Count));

    // 4.4 확인 순서: ACCURACY → MISSING → UNEXPLAINED → TIME → FOCUS(표에서 처음 나오는 순서). 판정 결과의 checks 순서와 같다.
    [Fact]
    public void Check_order_is_first_appearance_in_table()
    {
        Assert.Equal(
            [JudgmentSpec.Accuracy, JudgmentSpec.Missing, JudgmentSpec.Unexplained, JudgmentSpec.Time, JudgmentSpec.Focus],
            JudgmentSpec.CheckOrder());

        var judged = JudgmentInput.CheckScene(JudgmentSpec.Focus, JudgmentInput.Found).Judge();
        Assert.Equal(JudgmentSpec.CheckOrder(), judged.Checks.Select(c => c.Rule));
    }

    // 4.4: 행마다 그 결과를 내는 장면에서 그 확인의 결과와 확인 문장이 표와 같다.
    [Theory]
    [MemberData(nameof(CheckRowKeys))]
    public void Each_check_row_gives_its_result_and_sentence(string check, string result)
    {
        var row = JudgmentSpec.FindCheck(check, result);
        var scene = JudgmentInput.CheckScene(check, result);

        var actual = JudgmentInput.CheckOf(scene.Judge(), check);

        Assert.Equal(row.Result, actual.Result);
        Assert.Equal(JudgmentSpec.Fill(row.Template, scene.Slots()), actual.Reason);
    }

    // 장면의 다른 확인 결과도 4.4 표에 있는 결과다(표 밖의 결과 글자를 내지 않는다).
    [Theory]
    [MemberData(nameof(CheckRowKeys))]
    public void Every_check_result_is_a_table_result(string check, string result)
    {
        var judged = JudgmentInput.CheckScene(check, result).Judge();
        var rows = JudgmentSpec.CheckRows();

        Assert.All(judged.Checks, c => Assert.Contains(rows, r => r.Check == c.Rule && r.Result == c.Result));
    }

    // 4.5 숫자 표: focus_min = 2. 불일치 focus_min−1건이 한 값이면 few, focus_min건이면 found.
    [Fact]
    public void Focus_min_is_the_smallest_mismatch_count_for_focus()
    {
        var focusMin = JudgmentSpec.FocusMin();
        Assert.Equal(2, focusMin);

        foreach (var (bad, expected) in new[] { (focusMin - 1, JudgmentInput.Few), (focusMin, JudgmentInput.Found) })
        {
            var scene = new JudgmentScene(
                Columns: [JudgmentInput.Group],
                Rows: JudgmentInput.Rows(10, mismatch: bad, types: i => [i < bad ? "a" : (i % 2 == 0 ? "a" : "b")]),
                Extras: [],
                AccuracyMin: "0",
                MissingMax: "100",
                TimeMustDrop: "false");

            Assert.Equal(expected, JudgmentInput.CheckOf(scene.Judge(), JudgmentSpec.Focus).Result);
        }
    }

    // 6.1: 기준 행·일치·누락(나머지는 값 불일치)과 문턱에서 표시 퍼센트, ACCURACY·MISSING 결과와 문장.
    // 1799/2000은 90.0으로 보이지만 문턱 90에 못 미친다. 누락 1/30은 3.3으로 보이지만 문턱 3.3을 넘는다.
    [Theory]
    [MemberData(nameof(BoundaryRows))]
    public void Threshold_boundary_row_is_compared_by_counts(int index)
    {
        var c = JudgmentSpec.Table(JudgmentSpec.BoundaryHeader)[index].Select(JudgmentSpec.Cell).ToArray();
        var (reference, match, missing) = (JudgmentSpec.Int(c[0]), JudgmentSpec.Int(c[1]), JudgmentSpec.Int(c[2]));
        var scene = new JudgmentScene(
            Columns: [],
            Rows: JudgmentInput.Rows(reference, mismatch: reference - match - missing, missing: missing),
            Extras: [],
            AccuracyMin: c[4],
            MissingMax: c[7],
            TimeMustDrop: "false");

        var metrics = scene.Metrics();
        var judged = scene.Judge();
        var slots = scene.Slots();

        Assert.Equal(c[3], JudgmentInput.Text(metrics.AccuracyPct!.Value));
        Assert.Equal(c[6], JudgmentInput.Text(metrics.MissingRatePct!.Value));
        Assert.Equal(c[5], JudgmentInput.CheckOf(judged, JudgmentSpec.Accuracy).Result);
        Assert.Equal(c[8], JudgmentInput.CheckOf(judged, JudgmentSpec.Missing).Result);
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindCheck(JudgmentSpec.Accuracy, c[5]).Template, slots), JudgmentInput.CheckOf(judged, JudgmentSpec.Accuracy).Reason);
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindCheck(JudgmentSpec.Missing, c[8]).Template, slots), JudgmentInput.CheckOf(judged, JudgmentSpec.Missing).Reason);
    }

    // 4.4 조건은 정확한 셈 비교다(4.1 4 "셈 비교", T-011b 리뷰 N1). decimal 곱이 반올림되어 같아져도 결과가 바뀌면 안 된다.
    // 정확도: 2 × 100 = 200 < 66.666666666666666666666666667 × 3 = 200.000000000000000000000000001 → not_met, 보류.
    //   decimal 곱은 소수 26자리로 반올림되어 200이 된다. 그대로 비교하면 met(통과)이 나온다.
    // 누락률: 1 × 100 = 100 > 33.333333333333333333333333333 × 3 = 99.999999999999999999999999999 → not_met, 보류.
    //   decimal 곱은 100으로 반올림된다.
    // 반대쪽(…666, …334)은 정확한 셈으로도 met이다. 고친 엔진이 너무 엄하게 되지 않는지 본다.
    [Theory]
    [InlineData(3, 2, 0, "66.666666666666666666666666667", "100", "not_met", "met", "hold", "ACCURACY")]
    [InlineData(3, 2, 0, "66.666666666666666666666666666", "100", "met", "met", "pass", "PASS")]
    [InlineData(3, 2, 1, "0", "33.333333333333333333333333333", "met", "not_met", "hold", "MISSING")]
    [InlineData(3, 2, 1, "0", "33.333333333333333333333333334", "met", "met", "pass", "PASS")]
    public void Threshold_comparison_is_exact_beyond_decimal_precision(
        int reference, int match, int missing, string accuracyMin, string missingMax,
        string accuracy, string missingResult, string verdict, string rule)
    {
        var scene = new JudgmentScene(
            Columns: [],
            Rows: JudgmentInput.Rows(reference, mismatch: reference - match - missing, missing: missing),
            Extras: [],
            AccuracyMin: accuracyMin,
            MissingMax: missingMax,
            TimeMustDrop: "false");

        var judged = scene.Judge();
        var slots = scene.Slots();

        // 문턱은 case.yaml 글자 그대로 들어간다(자릿수 유지).
        Assert.Equal(accuracyMin, JudgmentInput.Text(judged.Thresholds.AccuracyMin));
        Assert.Equal(missingMax, JudgmentInput.Text(judged.Thresholds.MissingMax));
        Assert.Equal(accuracy, JudgmentInput.CheckOf(judged, JudgmentSpec.Accuracy).Result);
        Assert.Equal(missingResult, JudgmentInput.CheckOf(judged, JudgmentSpec.Missing).Result);
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindCheck(JudgmentSpec.Accuracy, accuracy).Template, slots), JudgmentInput.CheckOf(judged, JudgmentSpec.Accuracy).Reason);
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindCheck(JudgmentSpec.Missing, missingResult).Template, slots), JudgmentInput.CheckOf(judged, JudgmentSpec.Missing).Reason);
        Assert.Equal((verdict, rule), (judged.Verdict, judged.Rule));
        var verdictRow = rule == JudgmentSpec.PassRule ? JudgmentSpec.FindVerdict(rule, null) : JudgmentSpec.FindVerdict(rule, JudgmentInput.NotMet);
        Assert.Equal(JudgmentSpec.Fill(verdictRow.Template, slots), judged.Reason);
    }

    // 6.1 표가 경계 양쪽을 모두 쓴다: 표시 퍼센트가 문턱과 같아 보이는데 not_met인 행이 ACCURACY·MISSING 모두에 있다.
    [Fact]
    public void Boundary_table_has_display_equal_but_not_met_rows()
    {
        var rows = JudgmentSpec.Table(JudgmentSpec.BoundaryHeader).Select(r => r.Select(JudgmentSpec.Cell).ToArray()).ToArray();

        Assert.Contains(rows, r => JudgmentSpec.Decimal(r[3]) == JudgmentSpec.Decimal(r[4]) && r[5] == JudgmentInput.NotMet);
        Assert.Contains(rows, r => JudgmentSpec.Decimal(r[6]) == JudgmentSpec.Decimal(r[7]) && r[8] == JudgmentInput.NotMet);
        Assert.Contains(rows, r => JudgmentSpec.Decimal(r[3]) == JudgmentSpec.Decimal(r[4]) && r[5] == JudgmentInput.Met);
    }
}
