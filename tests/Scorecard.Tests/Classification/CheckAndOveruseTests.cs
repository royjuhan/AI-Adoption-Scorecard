namespace Scorecard.Tests.Classification;

// 스펙 4.7 확인 항목(승인 결정 7)과 4.8 AI 과잉 후보(승인 결정 8, 카드 수용 기준 5).
// 4.2 CHECK_EXISTING·OVERUSE 행은 ConditionTableTests가 표에서 읽어 본다. 여기서는 나머지 규칙을 본다.
[Trait("Spec", "classification")]
public class CheckAndOveruseTests
{
    // 4.7 표의 키 두 개, 표 순서(관련 키의 4.4 표 순서).
    [Fact]
    public void Check_table_lists_two_keys_in_order()
    {
        Assert.Equal([ClassificationSpec.MeasureMinutesPerItem, ClassificationSpec.ConfirmExistingFeature], ClassificationSpec.CheckKeys());
    }

    // 4.7: 값만 본다. 직접 쓴 unknown·null도, 기본값도 같다. 순서는 표 순서.
    [Theory]
    [InlineData("1", "no", false, new string[0])]
    [InlineData("null", "no", false, new[] { ClassificationSpec.MeasureMinutesPerItem })]
    [InlineData("null", "no", true, new[] { ClassificationSpec.MeasureMinutesPerItem })]
    [InlineData("1", "unknown", false, new[] { ClassificationSpec.ConfirmExistingFeature })]
    [InlineData("1", "unknown", true, new[] { ClassificationSpec.ConfirmExistingFeature })]
    [InlineData("null", "unknown", false, new[] { ClassificationSpec.MeasureMinutesPerItem, ClassificationSpec.ConfirmExistingFeature })]
    [InlineData("null", "unknown", true, new[] { ClassificationSpec.MeasureMinutesPerItem, ClassificationSpec.ConfirmExistingFeature })]
    [InlineData("0", "yes", false, new string[0])]
    public void Checks_follow_values_not_default_flags(string minutes, string existing, bool isDefault, string[] expected)
    {
        var a = ClassificationInput.With(ClassificationInput.Base, "minutes_per_item", minutes, isDefault);
        a = ClassificationInput.With(a, "existing_feature", existing, isDefault);

        Assert.Equal(expected, ClassificationInput.One(a).Checks);
    }

    // 4.7: existing_feature: unknown은 Q1에 걸리지 않고 아래로 내려가 분류한다(지시문 3.2 "확인 항목으로 남김").
    [Fact]
    public void Unknown_existing_feature_is_classified_below_Q1()
    {
        var step = ClassificationInput.One(ClassificationInput.Answers("existing_feature: unknown"));

        Assert.Equal(["② ELSE"], ClassificationInput.RowLabels(step));
        Assert.Contains(ClassificationSpec.ConfirmExistingFeature, step.Checks);
    }

    // 4.8: current_actor 값이 ai_each_time이고 step 행의 담당이 ②. 부가 행(④)은 보지 않는다.
    // ② + 예외 검토, ② + 게이트도 후보다. Q1~Q4는 후보가 아니다.
    [Theory]
    [InlineData("", true)]
    [InlineData("rule_expressible: partial", true)]
    [InlineData("failure_impact: external", true)]
    [InlineData("rule_expressible: partial, failure_impact: safety_legal", true)]
    [InlineData("frequency: rare, batch_size: 5", true)]
    [InlineData("existing_feature: yes", false)]
    [InlineData("rule_expressible: no", false)]
    [InlineData("input_kind: free_doc", false)]
    [InlineData("frequency: rare, batch_size: 1", false)]
    [InlineData("existing_feature: unknown", true)]
    public void Overuse_candidate_when_ai_each_time_and_step_row_is_rule_software(string changes, bool expected)
    {
        var a = ClassificationInput.With(ClassificationInput.Answers(changes), "current_actor", "ai_each_time");
        var step = ClassificationInput.One(a);

        Assert.Equal(expected, step.AiOveruseCandidate);
        Assert.Equal(expected, step.Rows[0].Assignee == "②");
    }

    // 4.8: ai_each_time이 아니면 ②여도 후보가 아니다.
    [Theory]
    [InlineData("human")]
    [InlineData("program")]
    [InlineData("mixed")]
    public void Other_actors_are_not_overuse_candidates(string actor)
    {
        var a = ClassificationInput.With(ClassificationInput.Base, "current_actor", actor);

        Assert.False(ClassificationInput.One(a).AiOveruseCandidate);
    }

    // 4.8 과잉 후보, 4.7 확인 항목, 4.6 defaults는 단계에 하나씩이다. 결과 단계 수는 입력 단계 수와 같다.
    [Fact]
    public void Step_level_fields_are_one_per_user_step()
    {
        var definition = ClassificationInput.Definition(
            "p",
            ("첫째", ClassificationInput.With(ClassificationInput.Answers("rule_expressible: partial, failure_impact: external"), "current_actor", "ai_each_time")),
            ("둘째", ClassificationInput.Answers("rule_expressible: no")));
        var result = Scorecard.Core.Classification.StepClassifier.Classify(definition);

        Assert.Equal("p", result.Case);
        Assert.Equal([1, 2], result.Steps.Select(s => s.Number));
        Assert.Equal(["첫째", "둘째"], result.Steps.Select(s => s.Name));
        Assert.Equal([true, false], result.Steps.Select(s => s.AiOveruseCandidate));
        Assert.Equal([3, 1], result.Steps.Select(s => s.Rows.Count));
    }
}
