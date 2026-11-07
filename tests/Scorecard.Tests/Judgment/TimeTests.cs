using Scorecard.Core.Judgment;

namespace Scorecard.Tests.Judgment;

// 스펙 4.10 시간과 6.2 시간 표(잠정 결정 4, 안 T1). 카드 수용 기준 2·3.
// 통과 조건 셋째: (처리 + 검토) < 적용 전. 같으면 줄지 않았다. time_must_drop이 true이고 시간이 없으면 조건부 TIME + measure_time.
// time_must_drop이 false면 시간을 받아도 보지 않는다(off).
[Trait("Spec", "judgment")]
public class TimeTests
{
    // 6.2 표의 행 번호.
    public static TheoryData<int> TimeRows => new(Enumerable.Range(0, JudgmentSpec.Table(JudgmentSpec.TimeHeader).Count));

    // 6.2: 행마다 TIME 결과. 불일치 없는 장면(정확도·누락률 met, 몰림 few)이라 TIME이 판정을 정한다.
    // met·not_met이면 확인 문장(4.4 틀), not_met·not_measured이면 판정 문장(4.3 틀)도 본다.
    [Theory]
    [MemberData(nameof(TimeRows))]
    public void Time_table_row_gives_its_result(int index)
    {
        var c = JudgmentSpec.Table(JudgmentSpec.TimeHeader)[index].Select(JudgmentSpec.Cell).ToArray();
        var times = c[1] == "없음" ? null : new JudgmentTimes(JudgmentSpec.Decimal(c[1]), JudgmentSpec.Decimal(c[2]), JudgmentSpec.Decimal(c[3]));
        Assert.True(times is null == (c[2] == "없음" && c[3] == "없음"), "6.2 표의 시간 셋은 모두 있거나 모두 없음이어야 한다.");
        var scene = AllMatch(c[0], times);

        var judged = scene.Judge();
        var time = JudgmentInput.CheckOf(judged, JudgmentSpec.Time);
        var slots = scene.Slots();

        Assert.Equal(c[4], time.Result);
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindCheck(JudgmentSpec.Time, c[4]).Template, slots), time.Reason);
        switch (c[4])
        {
            case JudgmentInput.Met or JudgmentInput.Off:
                Assert.Equal((JudgmentEngine.VerdictPass, JudgmentEngine.RulePass), (judged.Verdict, judged.Rule));
                break;
            default:
                Assert.Equal((JudgmentEngine.VerdictConditional, JudgmentEngine.RuleTime), (judged.Verdict, judged.Rule));
                Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindVerdict(JudgmentSpec.Time, c[4]).Template, slots), judged.Reason);
                break;
        }
    }

    // 6.2 표가 TIME 결과 넷을 모두 쓴다(빈 통과 방지).
    [Fact]
    public void Time_table_covers_all_time_results()
    {
        Assert.Equal(
            JudgmentSpec.CheckRows().Where(r => r.Check == JudgmentSpec.Time).Select(r => r.Result).Order(StringComparer.Ordinal),
            JudgmentSpec.Table(JudgmentSpec.TimeHeader).Select(r => JudgmentSpec.Cell(r[4])).Distinct().Order(StringComparer.Ordinal));
    }

    // 4.5 {total}: process + review의 decimal 덧셈 그대로(자릿수 유지). 1.25 + 1.2 = 2.45, 7.5 + 2.50 = 10.00.
    [Theory]
    [InlineData("10", "1.25", "1.2", "2.45")]
    [InlineData("12", "7.5", "2.50", "10.00")]
    [InlineData("10", "6", "3", "9")]
    public void Total_keeps_decimal_scale(string before, string process, string review, string total)
    {
        var scene = AllMatch("true", new JudgmentTimes(JudgmentSpec.Decimal(before), JudgmentSpec.Decimal(process), JudgmentSpec.Decimal(review)));

        var time = JudgmentInput.CheckOf(scene.Judge(), JudgmentSpec.Time);

        Assert.Equal(JudgmentInput.Met, time.Result);
        Assert.Contains($"= {total}분이", time.Reason, StringComparison.Ordinal);
    }

    // 4.4 셋째 글머리·5절: 시간 합이 decimal 범위를 넘으면 not_met(예외가 새지 않는다). 판정은 조건부 TIME.
    // 그때 문장의 {total} 모양은 스펙이 정하지 않았다(카드 "PR (b)에 넘기는 것" 2). 문장은 보지 않는다.
    [Fact]
    public void Time_sum_overflow_is_not_met()
    {
        var scene = AllMatch("true", new JudgmentTimes(decimal.MaxValue, decimal.MaxValue, 1m));

        var judged = scene.Judge();

        Assert.Equal(JudgmentInput.NotMet, JudgmentInput.CheckOf(judged, JudgmentSpec.Time).Result);
        Assert.Equal((JudgmentEngine.VerdictConditional, JudgmentEngine.RuleTime), (judged.Verdict, judged.Rule));
    }

    // 4.10·4.7: time_must_drop이 true이고 시간이 없으면 다음 할 일 measure_time. 시간이 있으면 없다. false면 없다.
    [Fact]
    public void Measure_time_todo_follows_time_result()
    {
        Assert.Contains("measure_time", JudgmentInput.TodoKeys(AllMatch("true", null).Judge()));
        Assert.DoesNotContain("measure_time", JudgmentInput.TodoKeys(AllMatch("true", JudgmentInput.TimesMet).Judge()));
        Assert.DoesNotContain("measure_time", JudgmentInput.TodoKeys(AllMatch("true", JudgmentInput.TimesNotMet).Judge()));
        Assert.DoesNotContain("measure_time", JudgmentInput.TodoKeys(AllMatch("false", null).Judge()));
    }

    // 4.4 마지막 글머리: time_must_drop이 false면 시간이 줄지 않았어도 보지 않는다(off). 판정에 쓰지 않는다.
    [Fact]
    public void Times_are_ignored_when_time_must_drop_is_false()
    {
        var withTimes = AllMatch("false", new JudgmentTimes(1m, 50m, 50m)).Judge();
        var without = AllMatch("false", null).Judge();

        Assert.Equal(JudgmentInput.Off, JudgmentInput.CheckOf(withTimes, JudgmentSpec.Time).Result);
        Assert.Equal(JudgmentJson.Render(without), JudgmentJson.Render(withTimes));
    }

    // 기준 10행 모두 일치. 정확도·누락률 문턱 0·100(늘 met).
    private static JudgmentScene AllMatch(string timeMustDrop, JudgmentTimes? times) =>
        new(Columns: [], Rows: JudgmentInput.Rows(10), Extras: [], AccuracyMin: "0", MissingMax: "100", TimeMustDrop: timeMustDrop, Times: times);
}
