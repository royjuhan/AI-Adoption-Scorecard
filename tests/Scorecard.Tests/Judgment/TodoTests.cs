using Scorecard.Core.Judgment;

namespace Scorecard.Tests.Judgment;

// 스펙 4.7 다음 할 일(잠정 결정 12). 카드 수용 기준 3(시간 자료가 없을 때의 표시)과 지시문 3.4 "미검증 = 무엇을 재면 되는지 적는다".
// 키마다 나오는 장면과 문구(표의 틀을 4.5·4.7 값으로 채운 것). todo 순서는 표 순서다. 통과·할 일 없음이면 [].
[Trait("Spec", "judgment")]
public class TodoTests
{
    private const string AddData = "add_data";
    private const string AddCompare = "add_compare";
    private const string MeasureTime = "measure_time";
    private const string ReviewMismatches = "review_mismatches";
    private const string AddTypeColumn = "add_type_column";
    private const string ConfirmThresholds = "confirm_thresholds";

    // 4.7 표의 키와 순서.
    [Fact]
    public void Todo_table_has_six_keys_in_order()
    {
        Assert.Equal([AddData, AddCompare, MeasureTime, ReviewMismatches, AddTypeColumn, ConfirmThresholds], JudgmentSpec.TodoOrder());
    }

    // 미검증 no_inputs(문턱 적음, time_must_drop true): add_data → add_compare → measure_time. 문구는 표 그대로다.
    [Fact]
    public void No_inputs_lists_data_compare_and_time()
    {
        var judged = JudgmentInput.JudgeNotVerified(JudgmentInput.NoInputsCase(JudgmentInput.ThresholdsYaml("90", "5", "true")));

        AssertTodo(judged, new Dictionary<string, string>(), AddData, AddCompare, MeasureTime);
    }

    // 4.7 둘째 글머리: no_compare_fields 미검증은 add_data 없이 add_compare부터다(inputs는 있다).
    [Fact]
    public void No_compare_fields_starts_with_add_compare()
    {
        var judged = JudgmentInput.JudgeNotVerified(JudgmentInput.NoCompareCase(JudgmentInput.ThresholdsYaml("90", "5", "true")));

        AssertTodo(judged, new Dictionary<string, string>(), AddCompare, MeasureTime);
    }

    // 4.7 measure_time "미검증이고 time_must_drop이 true": false면 미검증이어도 measure_time이 없다. 기본값(true)이면 있다.
    [Fact]
    public void Not_verified_measure_time_follows_time_must_drop()
    {
        var off = JudgmentInput.JudgeNotVerified(JudgmentInput.NoInputsCase(JudgmentInput.ThresholdsYaml("90", "5", "false")));
        AssertTodo(off, new Dictionary<string, string>(), AddData, AddCompare);

        var byDefault = JudgmentInput.JudgeNotVerified(JudgmentInput.NoCompareCase(JudgmentInput.ThresholdsYaml("90", "5", null)));
        AssertTodo(byDefault, new Dictionary<string, string> { ["defaults"] = "time_must_drop true" }, AddCompare, MeasureTime, ConfirmThresholds);
    }

    // 측정 장면: TIME not_measured → measure_time, UNEXPLAINED not_reviewed → review_mismatches({bad}), FOCUS off → add_type_column.
    // 문턱을 모두 빼면 confirm_thresholds({defaults} = 세 키와 값, 4.2 순서). 네 키가 표 순서로 나온다.
    [Fact]
    public void Measured_todo_lists_time_review_type_column_and_thresholds_in_table_order()
    {
        var scene = new JudgmentScene(Columns: [], Rows: JudgmentInput.Rows(40, mismatch: 2), Extras: [], AccuracyMin: null, MissingMax: null, TimeMustDrop: null);

        var judged = scene.Judge();

        Assert.Equal((JudgmentEngine.VerdictConditional, JudgmentEngine.RuleTime), (judged.Verdict, judged.Rule));
        Assert.Equal(
            [JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.NotReviewed, JudgmentInput.NotMeasured, JudgmentInput.Off],
            JudgmentInput.Results(judged));
        AssertTodo(judged, scene.Slots(), MeasureTime, ReviewMismatches, AddTypeColumn, ConfirmThresholds);
        Assert.Equal("accuracy_min 95, missing_max 2, time_must_drop true", scene.Slots()["defaults"]);
    }

    // {defaults}는 기본값을 쓴 키만 4.2 순서로 잇는다. 하나만 빼면 그 키 하나.
    [Theory]
    [InlineData("accuracy_min", "accuracy_min 95")]
    [InlineData("missing_max", "missing_max 2")]
    [InlineData("time_must_drop", "time_must_drop true")]
    public void Confirm_thresholds_names_only_default_keys(string key, string text)
    {
        var scene = new JudgmentScene(
            Columns: [],
            Rows: JudgmentInput.Rows(20),
            Extras: [],
            AccuracyMin: key == JudgmentSpec.AccuracyMin ? null : "95",
            MissingMax: key == JudgmentSpec.MissingMax ? null : "2",
            TimeMustDrop: key == JudgmentSpec.TimeMustDrop ? null : "true",
            Times: JudgmentInput.TimesMet);

        var judged = scene.Judge();

        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.TodoTemplate(ConfirmThresholds), new Dictionary<string, string> { ["defaults"] = text }), Assert.Single(judged.Todo).Text);
        Assert.Equal(ConfirmThresholds, Assert.Single(judged.Todo).Key);
    }

    // 4.7 셋째 글머리: 통과·할 일 없음이면 todo는 []다.
    [Fact]
    public void Pass_without_open_items_has_no_todo()
    {
        var judged = JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.NoneResult, JudgmentInput.Met, JudgmentInput.Few).Judge();

        Assert.Equal(JudgmentEngine.VerdictPass, judged.Verdict);
        Assert.Empty(judged.Todo);
    }

    // 키마다 "언제" 칸의 조건이 아니면 나오지 않는다: 시간 met·off면 measure_time 없음, 사람이 봤으면 review_mismatches 없음,
    // 유형 열이 있으면(found·spread·one_value) add_type_column 없음, 측정했으면 add_data·add_compare 없음.
    [Theory]
    [InlineData(JudgmentInput.Met, JudgmentInput.Explained, JudgmentInput.Spread)]
    [InlineData(JudgmentInput.Off, JudgmentInput.Found, JudgmentInput.Found)]
    [InlineData(JudgmentInput.Met, JudgmentInput.Found, JudgmentInput.OneValue)]
    public void Todo_keys_appear_only_when_their_condition_holds(string time, string unexplained, string focus)
    {
        var judged = JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, unexplained, time, focus).Judge();

        Assert.Empty(judged.Todo);
    }

    // 4.4·4.7: 미검증 밖의 todo는 확인 결과에서 나온다. 모든 확인 결과 장면에서 todo가 표 순서이고 문구가 틀 그대로다.
    [Theory]
    [MemberData(nameof(CheckTableTests.CheckRowKeys), MemberType = typeof(CheckTableTests))]
    public void Todo_follows_table_order_and_templates_in_every_check_scene(string check, string result)
    {
        var scene = JudgmentInput.CheckScene(check, result);
        var judged = scene.Judge();
        var expected = new List<string>();
        var results = judged.Checks.ToDictionary(c => c.Rule, c => c.Result, StringComparer.Ordinal);
        if (results[JudgmentSpec.Time] == JudgmentInput.NotMeasured)
        {
            expected.Add(MeasureTime);
        }

        if (results[JudgmentSpec.Unexplained] == JudgmentInput.NotReviewed)
        {
            expected.Add(ReviewMismatches);
        }

        if (results[JudgmentSpec.Focus] == JudgmentInput.Off)
        {
            expected.Add(AddTypeColumn);
        }

        AssertTodo(judged, scene.Slots(), expected.ToArray());
    }

    // todo 키가 keys(표 순서)와 같고, 문구는 표의 틀을 slots로 채운 것이다.
    private static void AssertTodo(JudgmentResult judged, IReadOnlyDictionary<string, string> slots, params string[] keys)
    {
        var order = JudgmentSpec.TodoOrder();
        Assert.Equal(keys.OrderBy(k => Array.IndexOf(order, k)), keys);
        Assert.Equal(keys, JudgmentInput.TodoKeys(judged));
        Assert.All(judged.Todo, t => Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.TodoTemplate(t.Key), slots), t.Text));
    }
}
