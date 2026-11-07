using Scorecard.Core.Cases;
using static Scorecard.Core.Classification.ClassificationRules;

namespace Scorecard.Core.Classification;

// 단계 분류. docs/spec/classification.md 4.1~4.8, 지시문 3.3 결정 트리.
// Q1 → Q2 → Q3 → Q4 → ELSE 순서로 보고 처음 걸리는 곳에서 멈춘다.
// ELSE이고 rule_expressible이 partial이면 예외 검토 행, failure_impact가 external·safety_legal이면 게이트 행을 붙인다.
// 순수 함수다. 파일·난수·시각·환경을 쓰지 않는다. 값·문장은 ClassificationRules에서 읽는다.
public static class StepClassifier
{
    // 4.4 행 종류.
    public const string KindStep = "step";
    public const string KindExceptionReview = "exception_review";
    public const string KindHumanGate = "human_gate";

    // 단계 순서는 case.yaml 순서다. 파싱 오류가 있는 케이스는 부르는 쪽이 걸러 낸다(5절).
    public static ClassificationResult Classify(CaseDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new ClassificationResult(definition.Name, definition.Steps.Select(ClassifyStep).ToArray());
    }

    private static StepClassification ClassifyStep(CaseStep step)
    {
        var a = step.Answers;
        var defaults = DefaultKeys(a);
        var leaf = Decide(a);

        // 4.3 행 순서: step → exception_review → human_gate.
        var rows = new List<ClassificationRow> { Row(KindStep, leaf.Assignee, leaf.Rule, leaf.Used, leaf.Reason, defaults) };

        // PARTIAL은 ELSE일 때만 붙는다. Q3·Q4에서 멈추면 붙지 않는다(4.1).
        if (leaf.Rule == RuleElse && a.RuleExpressible.Value == PartialRuleExpressible)
        {
            rows.Add(Row(KindExceptionReview, AssigneeHuman, RulePartial, [StepQuestions.RuleExpressible.Key], PartialReason, defaults));
        }

        // GATE는 모든 잎 뒤에 붙는다(지시문 3.3 "항상").
        if (GateFailureImpacts.Contains(a.FailureImpact.Value, StringComparer.Ordinal))
        {
            rows.Add(Row(KindHumanGate, AssigneeHuman, RuleGate, [StepQuestions.FailureImpact.Key], GateReason(a.FailureImpact.Value), defaults));
        }

        return new StepClassification(step.Number, step.Name, defaults, Checks(a), IsOveruseCandidate(a, leaf), rows.ToArray());
    }

    // 결정 트리의 잎 하나. Used는 잎을 정할 때 읽은 답이다(4.6 표).
    // failure_impact는 게이트 여부를 정할 때 늘 읽는다. batch_size는 frequency가 rare일 때만 읽는다(그리고).
    private static Leaf Decide(StepAnswers a)
    {
        var used = new List<string> { StepQuestions.FailureImpact.Key, StepQuestions.ExistingFeature.Key };
        if (a.ExistingFeature.Value == Q1ExistingFeature)
        {
            return new Leaf(RuleQ1, AssigneeExisting, Q1Reason, used);
        }

        used.Add(StepQuestions.RuleExpressible.Key);
        if (a.RuleExpressible.Value == Q2RuleExpressible)
        {
            return new Leaf(RuleQ2, AssigneeRuntimeAi, Q2Reason, used);
        }

        used.Add(StepQuestions.InputKind.Key);
        if (Q3InputKinds.Contains(a.InputKind.Value, StringComparer.Ordinal))
        {
            return new Leaf(RuleQ3, AssigneeRuntimeAi, Q3Reason(a.InputKind.Value), used);
        }

        used.Add(StepQuestions.Frequency.Key);
        if (a.Frequency.Value == Q4Frequency)
        {
            used.Add(StepQuestions.BatchSize.Key);
            if (a.BatchSize.Value < Q4BatchSizeBelow)
            {
                return new Leaf(RuleQ4, AssigneeHuman, Q4Reason(a.BatchSize.Value), used);
            }
        }

        var reason = ElseReason(a.RuleExpressible.Value, a.InputKind.Value, a.Frequency.Value, a.BatchSize.Value);
        return new Leaf(RuleElse, AssigneeRuleSoftware, reason, used);
    }

    // 읽은 답 중 하나라도 기본값이면 문장 끝에 표시를 붙인다(4.6).
    private static ClassificationRow Row(
        string kind, string assignee, string rule, IEnumerable<string> used, string reason, IReadOnlyList<string> defaults)
    {
        var ordered = Ordered(used);
        var marked = ordered.Any(k => defaults.Contains(k, StringComparer.Ordinal));
        return new ClassificationRow(kind, assignee, rule, ordered, marked ? reason + DefaultMark : reason);
    }

    // 4.7 확인 항목. 값만 본다. 직접 쓴 값과 기본값이 같다. 순서는 4.7 표 순서다.
    private static string[] Checks(StepAnswers a)
    {
        var checks = new List<string>();
        if (a.MinutesPerItem.Value is null)
        {
            checks.Add(CheckMeasureMinutesPerItem);
        }

        if (a.ExistingFeature.Value == CheckExistingFeature)
        {
            checks.Add(CheckConfirmExistingFeature);
        }

        return checks.ToArray();
    }

    // 4.8 AI 과잉 후보. step 행만 본다. 부가 행(④)은 보지 않는다.
    private static bool IsOveruseCandidate(StepAnswers a, Leaf leaf) =>
        a.CurrentActor.Value == OveruseCurrentActor && leaf.Assignee == AssigneeRuleSoftware;

    // 답 9개 중 IsDefault인 키.
    private static string[] DefaultKeys(StepAnswers a)
    {
        (string Key, bool IsDefault)[] flags =
        [
            (StepQuestions.InputKind.Key, a.InputKind.IsDefault),
            (StepQuestions.RuleExpressible.Key, a.RuleExpressible.IsDefault),
            (StepQuestions.Frequency.Key, a.Frequency.IsDefault),
            (StepQuestions.BatchSize.Key, a.BatchSize.IsDefault),
            (StepQuestions.ExceptionRate.Key, a.ExceptionRate.IsDefault),
            (StepQuestions.CurrentActor.Key, a.CurrentActor.IsDefault),
            (StepQuestions.MinutesPerItem.Key, a.MinutesPerItem.IsDefault),
            (StepQuestions.FailureImpact.Key, a.FailureImpact.IsDefault),
            (StepQuestions.ExistingFeature.Key, a.ExistingFeature.IsDefault),
        ];
        return Ordered(flags.Where(f => f.IsDefault).Select(f => f.Key));
    }

    // 키를 case-schema 4.4 표 순서(StepQuestions.Keys)로 둔다. 해시 순서를 쓰지 않는다(4.10).
    private static string[] Ordered(IEnumerable<string> keys)
    {
        var set = keys.ToArray();
        return StepQuestions.Keys.Where(k => set.Contains(k, StringComparer.Ordinal)).ToArray();
    }

    private sealed record Leaf(string Rule, string Assignee, string Reason, IReadOnlyList<string> Used);
}
