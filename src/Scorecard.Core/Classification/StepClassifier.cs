using Scorecard.Core.Cases;
using static Scorecard.Core.Classification.ClassificationRules;

namespace Scorecard.Core.Classification;

// 단계 분류. docs/spec/classification.md 4.1~4.8, 지시문 3.3 결정 트리.
// SIGNOFF → Q1 → Q2 → Q3 → Q4 → ELSE 순서로 보고 처음 걸리는 곳에서 멈춘다.
// ELSE이고 rule_expressible이 partial이면 예외 검토 행, failure_impact가 external·safety_legal이면 게이트 행을 붙인다.
// SIGNOFF 단계에는 부가 행을 붙이지 않는다. 사람이 서명하는 단계 뒤에 게이트를 또 두지 않는다(ADR 0003).
// 단계에 ai가 있으면 step 행만 AI 행으로 바꾸고, 트리 step 행은 규칙 의견으로 남긴다(4.13, ADR 0013).
// 부가 행은 트리 것을 그대로 둔다. SIGNOFF 단계라도 AI 담당의 비교 값이 ④가 아니면 게이트를 붙인다.
// 순수 함수다. 파일·난수·시각·환경을 쓰지 않는다. 값·문장은 ClassificationRules에서 읽는다.
public static class StepClassifier
{
    // 4.4 행 종류.
    public const string KindStep = "step";
    public const string KindExceptionReview = "exception_review";
    public const string KindHumanGate = "human_gate";

    // 3.2 source 값. 단계에 ai가 있으면 ai, 없으면 rule.
    public const string SourceAi = "ai";
    public const string SourceRule = "rule";

    // 단계 순서는 case.yaml 순서다. 파싱 오류가 있는 케이스는 부르는 쪽이 걸러 낸다(5절).
    public static ClassificationResult Classify(CaseDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new ClassificationResult(definition.Name, definition.Steps.Select(ClassifyStep).ToArray());
    }

    private static StepClassification ClassifyStep(CaseStep step)
    {
        var a = step.Answers;
        var ai = step.Ai;
        var defaults = DefaultKeys(a);
        var inferred = InferredKeys(a);
        var leaf = Decide(a);

        // 4.13: ai가 있으면 step 행은 AI 행이다. 근거는 그대로 쓰고 표시를 붙이지 않는다(트리가 읽은 답이 아니다).
        // 4.3 행 순서: step → exception_review → human_gate.
        var first = ai is null
            ? Row(KindStep, leaf.Assignee, leaf.Rule, leaf.Used, leaf.Reason, defaults, inferred)
            : new ClassificationRow(KindStep, ai.Assignee, RuleAi, [], ai.Reason);
        var rows = new List<ClassificationRow> { first };

        // PARTIAL은 ELSE일 때만 붙는다. Q3·Q4에서 멈추면 붙지 않는다(4.1).
        if (leaf.Rule == RuleElse && a.RuleExpressible.Value == PartialRuleExpressible)
        {
            rows.Add(Row(KindExceptionReview, AssigneeHuman, RulePartial, [StepQuestions.RuleExpressible.Key], PartialReason, defaults, inferred));
        }

        // GATE는 모든 잎 뒤에 붙는다(지시문 3.3 "항상"). 단 step 행이 사람(④)인 SIGNOFF 단계는 뺀다(ADR 0003).
        // SIGNOFF 단계라도 AI 담당의 비교 값이 ④가 아니면 붙인다(4.13).
        var signoffByHuman = leaf.Rule == RuleSignoff && CompareValue(first.Assignee) == AssigneeHuman;
        if (!signoffByHuman && GateFailureImpacts.Contains(a.FailureImpact.Value, StringComparer.Ordinal))
        {
            rows.Add(Row(KindHumanGate, AssigneeHuman, RuleGate, [StepQuestions.FailureImpact.Key], GateReason(a.FailureImpact.Value), defaults, inferred));
        }

        var differs = ai is not null && CompareValue(ai.Assignee) != leaf.Assignee;
        return new StepClassification(
            step.Number,
            step.Name,
            ai is null ? SourceRule : SourceAi,
            new RuleOpinion(leaf.Assignee, leaf.Rule),
            differs,
            defaults,
            inferred,
            Checks(a),
            IsOveruseCandidate(a, first),
            rows.ToArray(),
            ai?.Detail);
    }

    // 결정 트리의 잎 하나. Used는 잎을 정할 때 읽은 답이다(4.6 표).
    // human_signoff는 늘 맨 먼저 읽는다. SIGNOFF면 게이트를 보지 않으므로 failure_impact를 읽지 않는다.
    // failure_impact는 그 밖의 잎에서 게이트 여부를 정할 때 늘 읽는다. batch_size는 frequency가 rare일 때만 읽는다(그리고).
    private static Leaf Decide(StepAnswers a)
    {
        if (a.HumanSignoff.Value == SignoffHumanSignoff)
        {
            return new Leaf(RuleSignoff, AssigneeHuman, SignoffReason, [StepQuestions.HumanSignoff.Key]);
        }

        var used = new List<string> { StepQuestions.HumanSignoff.Key, StepQuestions.FailureImpact.Key, StepQuestions.ExistingFeature.Key };
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

    // 읽은 답 중 하나라도 기본값이면 문장 끝에 기본값 표시(4.6), 추정이면 그 뒤에 추정 표시(4.12)를 붙인다.
    private static ClassificationRow Row(
        string kind, string assignee, string rule, IEnumerable<string> used, string reason, IReadOnlyList<string> defaults, IReadOnlyList<string> inferred)
    {
        var ordered = Ordered(used);
        var text = reason;
        if (ordered.Any(k => defaults.Contains(k, StringComparer.Ordinal)))
        {
            text += DefaultMark;
        }

        if (ordered.Any(k => inferred.Contains(k, StringComparer.Ordinal)))
        {
            text += InferredMark;
        }

        return new ClassificationRow(kind, assignee, rule, ordered, text);
    }

    // 4.7 확인 항목. 값만 본다. 직접 쓴 값과 기본값이 같다.
    private static string[] Checks(StepAnswers a) =>
        a.MinutesPerItem.Value is null ? [CheckMeasureMinutesPerItem] : [];

    // 4.8 AI 과잉 후보. step 행(Rows[0])만 본다. 부가 행(④)은 보지 않는다. 담당의 비교 값이 ②면 후보다(②M 포함).
    private static bool IsOveruseCandidate(StepAnswers a, ClassificationRow first) =>
        a.CurrentActor.Value == OveruseCurrentActor && CompareValue(first.Assignee) == AssigneeRuleSoftware;

    // 답 10개 중 IsDefault인 키.
    private static string[] DefaultKeys(StepAnswers a) => Ordered(Flags(a).Where(f => f.IsDefault).Select(f => f.Key));

    // 답 10개 중 IsInferred인 키(4.12).
    private static string[] InferredKeys(StepAnswers a) => Ordered(Flags(a).Where(f => f.IsInferred).Select(f => f.Key));

    // 답 10개의 키와 기본값·추정 여부. case-schema 4.4 표 순서다.
    private static (string Key, bool IsDefault, bool IsInferred)[] Flags(StepAnswers a) =>
    [
        (StepQuestions.InputKind.Key, a.InputKind.IsDefault, a.InputKind.IsInferred),
        (StepQuestions.RuleExpressible.Key, a.RuleExpressible.IsDefault, a.RuleExpressible.IsInferred),
        (StepQuestions.Frequency.Key, a.Frequency.IsDefault, a.Frequency.IsInferred),
        (StepQuestions.BatchSize.Key, a.BatchSize.IsDefault, a.BatchSize.IsInferred),
        (StepQuestions.ExceptionRate.Key, a.ExceptionRate.IsDefault, a.ExceptionRate.IsInferred),
        (StepQuestions.CurrentActor.Key, a.CurrentActor.IsDefault, a.CurrentActor.IsInferred),
        (StepQuestions.MinutesPerItem.Key, a.MinutesPerItem.IsDefault, a.MinutesPerItem.IsInferred),
        (StepQuestions.FailureImpact.Key, a.FailureImpact.IsDefault, a.FailureImpact.IsInferred),
        (StepQuestions.ExistingFeature.Key, a.ExistingFeature.IsDefault, a.ExistingFeature.IsInferred),
        (StepQuestions.HumanSignoff.Key, a.HumanSignoff.IsDefault, a.HumanSignoff.IsInferred),
    ];

    // 키를 case-schema 4.4 표 순서(StepQuestions.Keys)로 둔다. 해시 순서를 쓰지 않는다(4.10).
    private static string[] Ordered(IEnumerable<string> keys)
    {
        var set = keys.ToArray();
        return StepQuestions.Keys.Where(k => set.Contains(k, StringComparer.Ordinal)).ToArray();
    }

    private sealed record Leaf(string Rule, string Assignee, string Reason, IReadOnlyList<string> Used);
}
