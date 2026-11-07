using System.Globalization;
using Scorecard.Core.Cases;

namespace Scorecard.Core.Classification;

// T-004 스파이크 후보(머지 대상 아님). 지시문 3.3 결정 트리.
// 순서대로 보고 처음 걸리는 곳에서 멈춘다. failure_impact는 항상 본다.
// 숫자·값 집합은 TreeRules 한 곳에만 둔다(스펙 표와 테스트로 대조하는 안).
public static class StepClassifier
{
    public const string KindStep = "step";
    public const string KindExceptionReview = "exception_review";
    public const string KindHumanGate = "human_gate";

    // 4.4 표 순서. Used·Defaults를 이 순서로 정렬한다.
    private static readonly string[] KeyOrder =
    [
        "input_kind", "rule_expressible", "frequency", "batch_size", "exception_rate",
        "current_actor", "minutes_per_item", "failure_impact", "existing_feature",
    ];

    public static ClassificationResult Classify(CaseDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new ClassificationResult(definition.Name, definition.Steps.Select(ClassifyStep).ToArray());
    }

    private static StepClassification ClassifyStep(CaseStep step)
    {
        var a = step.Answers;
        var defaults = DefaultKeys(a);
        var used = new List<string> { "existing_feature" };
        string rule;
        string assignee;
        string reason;
        var addPartial = false;

        if (a.ExistingFeature.Value == TreeRules.Q1ExistingFeature)
        {
            (rule, assignee, reason) = ("Q1", "①", ReasonText.Q1);
        }
        else
        {
            used.Add("rule_expressible");
            if (a.RuleExpressible.Value == TreeRules.Q2RuleExpressible)
            {
                (rule, assignee, reason) = ("Q2", "③", ReasonText.Q2);
            }
            else
            {
                used.Add("input_kind");
                if (TreeRules.Q3InputKinds.Contains(a.InputKind.Value, StringComparer.Ordinal))
                {
                    (rule, assignee, reason) = ("Q3", "③", ReasonText.Q3(a.InputKind.Value));
                }
                else
                {
                    used.Add("frequency");
                    var rare = a.Frequency.Value == TreeRules.Q4Frequency;
                    if (rare)
                    {
                        used.Add("batch_size");
                    }

                    if (rare && a.BatchSize.Value < TreeRules.Q4BatchSizeBelow)
                    {
                        (rule, assignee, reason) = ("Q4", "④", ReasonText.Q4(a.BatchSize.Value));
                    }
                    else
                    {
                        (rule, assignee, reason) = ("ELSE", "②", ReasonText.Else(a.RuleExpressible.Value, a.InputKind.Value, a.Frequency.Value, a.BatchSize.Value));
                        addPartial = a.RuleExpressible.Value == TreeRules.PartialRuleExpressible;
                    }
                }
            }
        }

        // 항상: 사람 게이트 여부를 정할 때 failure_impact를 읽는다.
        used.Add("failure_impact");

        var rows = new List<ClassificationRow> { Row(KindStep, assignee, rule, used, reason, defaults) };
        if (addPartial)
        {
            rows.Add(Row(KindExceptionReview, "④", "PARTIAL", ["rule_expressible"], ReasonText.Partial, defaults));
        }

        if (TreeRules.GateFailureImpacts.Contains(a.FailureImpact.Value, StringComparer.Ordinal))
        {
            rows.Add(Row(KindHumanGate, "④", "GATE", ["failure_impact"], ReasonText.Gate(a.FailureImpact.Value), defaults));
        }

        var checks = new List<string>();
        if (a.MinutesPerItem.Value is null)
        {
            checks.Add("measure_minutes_per_item");
        }

        if (a.ExistingFeature.Value == TreeRules.UnknownExistingFeature)
        {
            checks.Add("confirm_existing_feature");
        }

        var overuse = a.CurrentActor.Value == TreeRules.AiEachTime && assignee == "②";
        return new StepClassification(step.Number, step.Name, defaults, checks, overuse, rows);
    }

    private static ClassificationRow Row(string kind, string assignee, string rule, IEnumerable<string> used, string reason, IReadOnlyList<string> defaults)
    {
        var ordered = Ordered(used);
        var mark = ordered.Any(k => defaults.Contains(k, StringComparer.Ordinal));
        return new ClassificationRow(kind, assignee, rule, ordered, mark ? reason + ReasonText.DefaultMark : reason);
    }

    private static string[] Ordered(IEnumerable<string> keys) =>
        keys.Distinct(StringComparer.Ordinal).OrderBy(k => Array.IndexOf(KeyOrder, k)).ToArray();

    private static string[] DefaultKeys(StepAnswers a)
    {
        var flags = new (string Key, bool IsDefault)[]
        {
            ("input_kind", a.InputKind.IsDefault),
            ("rule_expressible", a.RuleExpressible.IsDefault),
            ("frequency", a.Frequency.IsDefault),
            ("batch_size", a.BatchSize.IsDefault),
            ("exception_rate", a.ExceptionRate.IsDefault),
            ("current_actor", a.CurrentActor.IsDefault),
            ("minutes_per_item", a.MinutesPerItem.IsDefault),
            ("failure_impact", a.FailureImpact.IsDefault),
            ("existing_feature", a.ExistingFeature.IsDefault),
        };
        return flags.Where(f => f.IsDefault).Select(f => f.Key).ToArray();
    }
}

// 트리의 값·숫자. 스펙 classification.md 트리 표와 같아야 한다(테스트가 대조하는 안).
internal static class TreeRules
{
    public const string Q1ExistingFeature = "yes";
    public const string Q2RuleExpressible = "no";
    public static readonly string[] Q3InputKinds = ["free_doc", "image", "conversation"];
    public const string Q4Frequency = "rare";
    public const int Q4BatchSizeBelow = 5;
    public const string PartialRuleExpressible = "partial";
    public static readonly string[] GateFailureImpacts = ["external", "safety_legal"];
    public const string AiEachTime = "ai_each_time";
    public const string UnknownExistingFeature = "unknown";
}

// 근거 문장 틀. 스펙 표와 같아야 한다.
internal static class ReasonText
{
    public const string DefaultMark = " (기본값 사용, 확인 필요)";

    public const string Q1 = "기존 프로그램에 같은 기능이 있으므로(yes) 기존 기능·설정으로 분류.";

    public const string Q2 = "판단 기준을 글로 쓸 수 없으므로(no) 실행 시 AI 보조 + 사람 확인으로 분류. 자동 승인은 하지 않는다.";

    public const string Partial = "판단 기준을 일부만 글로 쓸 수 있으므로(partial) 예외 건은 사람이 검토한다.";

    public static string Q3(string inputKind) =>
        $"입력이 {Q3Input(inputKind)}({inputKind}) 입력 양식 표준화를 먼저 검토하고, 안 되면 실행 시 AI 보조로 분류. 해석만 AI, 계산·저장은 코드로 한다.";

    public static string Q4(int batchSize) =>
        $"드물게 반복되고 한 번에 {Number(batchSize)}건이므로 그대로 두거나 일회성 AI 보조로 분류. 개발비가 더 크다.";

    public static string Else(string ruleExpressible, string inputKind, string frequency, int batchSize) =>
        $"{Rule(ruleExpressible)}({ruleExpressible}), {Input(inputKind)}({inputKind}), {Frequency(frequency, batchSize)} 규칙 기반 소프트웨어로 분류.";

    public static string Gate(string failureImpact) =>
        $"틀리면 {Impact(failureImpact)}({failureImpact}) 이 단계 뒤에 사람 게이트를 둔다.";

    private static string Q3Input(string v) => v switch
    {
        "free_doc" => "자유 형식 문서이므로",
        "image" => "이미지이므로",
        "conversation" => "대화이므로",
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null),
    };

    private static string Rule(string v) => v switch
    {
        "yes" => "판단 기준을 글로 쓸 수 있고",
        "partial" => "판단 기준을 일부만 글로 쓸 수 있고",
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null),
    };

    private static string Input(string v) => v switch
    {
        "table" => "입력이 표 형식이며",
        "fixed_form" => "입력이 고정 양식이며",
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null),
    };

    private static string Frequency(string v, int batchSize) => v switch
    {
        "daily" => "매일 반복되므로",
        "weekly" => "매주 반복되므로",
        "monthly" => "매월 반복되므로",
        "rare" => $"드물게 반복되지만 한 번에 {Number(batchSize)}건이므로",
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null),
    };

    private static string Impact(string v) => v switch
    {
        "external" => "외부에 영향이 있으므로",
        "safety_legal" => "안전·법규에 영향이 있으므로",
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, null),
    };

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
