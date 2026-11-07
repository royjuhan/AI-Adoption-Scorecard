using Scorecard.Core.Judgment;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Classification;

namespace Scorecard.Tests.Judgment;

// 스펙 4.2 문턱 기본값과 4.6 기본값 표시(잠정 결정 3). 카드 수용 기준 2.
// thresholds 키가 없으면 4.2 표의 값을 쓰고 defaults에 키를 적는다(4.2 표 순서). 문장 끝에 " (기본값 사용, 확인 필요)"를 한 번 붙인다:
// 확인 문장은 그 확인이 쓰는 문턱이, 판정 문장은 4.6 읽은 문턱 표의 문턱 가운데 하나가 기본값일 때.
[Trait("Spec", "judgment")]
public class ThresholdDefaultTests
{
    // 장면의 문턱은 기본값과 같은 값(95·2·true)으로 적는다. 키를 빼도 판정이 바뀌지 않고 표시만 바뀐다.
    private const string Accuracy95 = "95";
    private const string Missing2 = "2";
    private const string TimeTrue = "true";

    public static TheoryData<string> Keys => new(JudgmentSpec.Defaults().Select(d => d.Key));

    // 4.6 읽은 문턱 표의 근거 키 × 4.2 문턱 키.
    public static TheoryData<string, string> RuleAndKey =>
        ClassificationInput.TheoryPairs(JudgmentSpec.ReadThresholds().Keys.SelectMany(r => JudgmentSpec.Defaults().Select(d => (r, d.Key))));

    public static TheoryData<string> Rules => new(JudgmentSpec.ReadThresholds().Keys);

    // 4.2 표: 키 순서와 기본값. 12.1 값 95·2·true(잠정 결정 3).
    [Fact]
    public void Default_table_has_three_keys_in_order()
    {
        Assert.Equal(
            [(JudgmentSpec.AccuracyMin, "95"), (JudgmentSpec.MissingMax, "2"), (JudgmentSpec.TimeMustDrop, "true")],
            JudgmentSpec.Defaults());
    }

    // thresholds 키가 없으면 세 값 모두 기본값이고 defaults는 4.2 표 순서의 세 키다.
    [Fact]
    public void Missing_thresholds_use_all_defaults()
    {
        var judged = Scene(JudgmentSpec.PassRule, null, null, null).Judge();

        Assert.Equal(new JudgmentThresholds(95m, 2m, true), judged.Thresholds);
        Assert.Equal(JudgmentSpec.Defaults().Select(d => d.Key), judged.Defaults);
    }

    // 5절 "thresholds 일부만 적음": 빈 키만 기본값. 그 키만 defaults에 있다. 적은 키는 적은 값 그대로다.
    [Theory]
    [MemberData(nameof(Keys))]
    public void Only_the_missing_key_uses_its_default(string key)
    {
        string? Given(string k, string text) => k == key ? null : text;
        var scene = Scene(
            JudgmentSpec.PassRule,
            Given(JudgmentSpec.AccuracyMin, "85"),
            Given(JudgmentSpec.MissingMax, "5"),
            Given(JudgmentSpec.TimeMustDrop, "false"));

        var judged = scene.Judge();

        Assert.Equal([key], judged.Defaults);
        Assert.Equal(
            new JudgmentThresholds(
                key == JudgmentSpec.AccuracyMin ? JudgmentSpec.Decimal(JudgmentSpec.DefaultOf(key)) : 85m,
                key == JudgmentSpec.MissingMax ? JudgmentSpec.Decimal(JudgmentSpec.DefaultOf(key)) : 5m,
                key == JudgmentSpec.TimeMustDrop ? bool.Parse(JudgmentSpec.DefaultOf(key)) : false),
            judged.Thresholds);
    }

    // 셋 다 적으면 defaults = [], 표시 없음, confirm_thresholds 없음.
    [Fact]
    public void All_given_thresholds_have_no_defaults()
    {
        var judged = Scene(JudgmentSpec.PassRule, Accuracy95, Missing2, TimeTrue).Judge();

        Assert.Empty(judged.Defaults);
        Assert.DoesNotContain(JudgmentSpec.DefaultMark, judged.Reason, StringComparison.Ordinal);
        Assert.All(judged.Checks, c => Assert.DoesNotContain(JudgmentSpec.DefaultMark, c.Reason, StringComparison.Ordinal));
        Assert.DoesNotContain("confirm_thresholds", JudgmentInput.TodoKeys(judged));
    }

    // 4.6 첫 글머리: 표시 글자는 classification.md 4.6과 같다. 공백 하나 + `(기본값 사용, 확인 필요)`.
    [Fact]
    public void Default_mark_text_matches_spec()
    {
        var line = SpecDocument.Line(JudgmentSpec.Document, "- 표시 글자는 `classification.md` 4.6과 같다:");

        Assert.Contains("`(기본값 사용, 확인 필요)`", line, StringComparison.Ordinal);
        Assert.Equal(" " + "(기본값 사용, 확인 필요)", JudgmentSpec.DefaultMark);
        Assert.Equal(Scorecard.Tests.Classification.ClassificationSpec.DefaultMark, JudgmentSpec.DefaultMark);
    }

    // 4.6 읽은 문턱 표: 근거 키 일곱이 4.3 표의 근거 키와 같다. NOT_VERIFIED는 문턱을 읽지 않는다.
    [Fact]
    public void Read_threshold_table_covers_every_rule()
    {
        var read = JudgmentSpec.ReadThresholds();

        Assert.Equal(JudgmentSpec.VerdictRows().Select(r => r.Rule).Distinct(), read.Keys);
        Assert.Empty(read[JudgmentSpec.NotVerifiedRule]);
        Assert.All(read.Values.SelectMany(v => v), k => Assert.Contains(k, JudgmentSpec.Defaults().Select(d => d.Key)));
    }

    // 4.6: 근거 키마다 문턱 하나를 기본값으로 바꾸면(값은 같다) 판정은 그대로이고, 판정 문장은 읽은 문턱에 그 키가 있을 때만,
    // 확인 문장은 그 확인이 쓰는 문턱일 때만 표시가 붙는다. defaults는 그 키, 다음 할 일에 confirm_thresholds.
    [Theory]
    [MemberData(nameof(RuleAndKey))]
    public void One_default_threshold_marks_sentences_that_read_it(string rule, string key)
    {
        AssertMarks(rule, [key]);
    }

    // 셋 다 기본값이면 읽은 문턱이 하나라도 있는 판정 문장, 문턱을 쓰는 확인 문장(ACCURACY·MISSING·TIME)에 표시가 붙는다.
    [Theory]
    [MemberData(nameof(Rules))]
    public void All_default_thresholds_mark_sentences_that_read_any(string rule)
    {
        AssertMarks(rule, JudgmentSpec.Defaults().Select(d => d.Key).ToArray());
    }

    // 4.6 마지막 글머리: 미검증 문장은 문턱을 읽지 않는다. 기본값 셋을 써도 판정 문장에 표시가 없고, defaults와 confirm_thresholds에 남는다.
    [Fact]
    public void Not_verified_sentence_has_no_mark()
    {
        var judged = JudgmentInput.JudgeNotVerified(JudgmentInput.NoInputsCase());

        Assert.Equal(JudgmentSpec.FindVerdict(JudgmentSpec.NotVerifiedRule, "no_inputs").Template, judged.Reason);
        Assert.Equal(JudgmentSpec.Defaults().Select(d => d.Key), judged.Defaults);
        Assert.Contains("confirm_thresholds", JudgmentInput.TodoKeys(judged));
    }

    private static void AssertMarks(string rule, string[] defaults)
    {
        string? Text(string key, string value) => defaults.Contains(key) ? null : value;
        var accuracy = Text(JudgmentSpec.AccuracyMin, Accuracy95);
        var missing = Text(JudgmentSpec.MissingMax, Missing2);
        var time = Text(JudgmentSpec.TimeMustDrop, TimeTrue);
        var read = JudgmentSpec.ReadThresholds()[rule];
        var verdictMark = read.Any(k => defaults.Contains(k)) ? JudgmentSpec.DefaultMark : string.Empty;

        if (rule == JudgmentSpec.NotVerifiedRule)
        {
            var notVerified = JudgmentInput.JudgeNotVerified(JudgmentInput.NoInputsCase(JudgmentInput.ThresholdsYaml(accuracy, missing, time)));
            Assert.Equal(JudgmentSpec.FindVerdict(rule, "no_inputs").Template + verdictMark, notVerified.Reason);
            Assert.Equal(defaults, notVerified.Defaults);
            Assert.Contains("confirm_thresholds", JudgmentInput.TodoKeys(notVerified));
            return;
        }

        var given = Scene(rule, Accuracy95, Missing2, TimeTrue).Judge();
        var scene = Scene(rule, accuracy, missing, time);
        var judged = scene.Judge();
        var slots = scene.Slots();

        // 값이 같으므로 판정·확인 결과는 그대로다. 장면이 그 근거 키에 걸린다.
        Assert.Equal(rule, given.Rule);
        Assert.Equal((given.Verdict, given.Rule), (judged.Verdict, judged.Rule));
        Assert.Equal(JudgmentInput.Results(given), JudgmentInput.Results(judged));
        Assert.Equal(given.Thresholds, judged.Thresholds);
        Assert.Equal(defaults, judged.Defaults);

        var result = rule == JudgmentSpec.PassRule ? null : JudgmentInput.CheckOf(judged, rule).Result;
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindVerdict(rule, result).Template, slots) + verdictMark, judged.Reason);
        foreach (var check in judged.Checks)
        {
            var mark = JudgmentSpec.CheckThreshold.TryGetValue(check.Rule, out var used) && defaults.Contains(used) ? JudgmentSpec.DefaultMark : string.Empty;
            Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindCheck(check.Rule, check.Result).Template, slots) + mark, check.Reason);
        }

        // 표시는 문장 끝에 한 번뿐이다.
        Assert.All(judged.Checks.Select(c => c.Reason).Append(judged.Reason), r =>
            Assert.True(r.Split(JudgmentSpec.DefaultMark).Length <= 2 && (!r.Contains(JudgmentSpec.DefaultMark, StringComparison.Ordinal) || r.EndsWith(JudgmentSpec.DefaultMark, StringComparison.Ordinal)), r));
        Assert.Contains("confirm_thresholds", JudgmentInput.TodoKeys(judged));
        Assert.DoesNotContain("confirm_thresholds", JudgmentInput.TodoKeys(given));
    }

    // 문턱이 95·2·true일 때 그 근거 키에 걸리는 장면. 문턱 글이 null이면 그 키를 쓰지 않는다(기본값).
    private static JudgmentScene Scene(string rule, string? accuracy, string? missing, string? time) => rule switch
    {
        // 10행 중 값 불일치 1 → 90.0% < 95.
        JudgmentSpec.Accuracy => new(Columns: [], Rows: JudgmentInput.Rows(10, mismatch: 1), Extras: [], accuracy, missing, time),

        // 20행 중 누락 1 → 95.0% ≥ 95, 5.0% > 2.
        JudgmentSpec.Missing => new(Columns: [], Rows: JudgmentInput.Rows(20, missing: 1), Extras: [], accuracy, missing, time),

        // 20행 중 값 불일치 1, 사람이 그 키를 설명 못 함.
        JudgmentSpec.Unexplained => new(Columns: [], Rows: JudgmentInput.Rows(20, mismatch: 1), Extras: [], accuracy, missing, time, Unexplained: [JudgmentInput.Key(0)]),

        // 20행 모두 일치, 시간 없음 → not_measured.
        JudgmentSpec.Time => new(Columns: [], Rows: JudgmentInput.Rows(20), Extras: [], accuracy, missing, time),

        // 40행 중 값 불일치 2(grp = a), 시간 met → 95.0%, 몰림 found.
        JudgmentSpec.Focus => new JudgmentScene(
            Columns: [JudgmentInput.Group],
            Rows: JudgmentInput.Rows(40, mismatch: 2, types: i => [i < 2 ? "a" : "b"]),
            Extras: [],
            accuracy,
            missing,
            time,
            Times: JudgmentInput.TimesMet)
        {
            FocusGroup = JudgmentInput.Group,
            FocusValue = "a",
        },

        // 20행 모두 일치, 시간 met → 통과.
        JudgmentSpec.PassRule => new(Columns: [], Rows: JudgmentInput.Rows(20), Extras: [], accuracy, missing, time, Times: JudgmentInput.TimesMet),
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "장면이 없는 근거 키"),
    };
}
