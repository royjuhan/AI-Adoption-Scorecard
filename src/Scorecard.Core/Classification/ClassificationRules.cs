using System.Globalization;

namespace Scorecard.Core.Classification;

// 분류의 값·숫자·문장을 이 파일 한 곳에만 둔다(docs/spec/classification.md 4.9, 승인 결정 9).
// 원본은 스펙 4.2 조건 표, 4.4 값 표, 4.5 문장 틀·문구 표, 4.6 기본값 표시, 4.7 확인 항목 표다.
// 값을 바꾸려면 스펙 표를 먼저 고친다. 테스트가 스펙 표를 읽어 Core 동작과 대조한다.
// 실행 중에 스펙 문서를 읽지 않는다. 비교는 서수, 숫자는 InvariantCulture다.
internal static class ClassificationRules
{
    // 4.4 담당.
    public const string AssigneeExisting = "①";
    public const string AssigneeRuleSoftware = "②";
    public const string AssigneeRuntimeAi = "③";
    public const string AssigneeHuman = "④";

    // 4.4 근거 키.
    public const string RuleQ1 = "Q1";
    public const string RuleQ2 = "Q2";
    public const string RuleQ3 = "Q3";
    public const string RuleQ4 = "Q4";
    public const string RuleElse = "ELSE";
    public const string RulePartial = "PARTIAL";
    public const string RuleGate = "GATE";

    // 4.2 조건 표. 같은 근거 키의 조건은 모두 참이어야 걸린다(그리고).
    public const string Q1ExistingFeature = "yes";
    public const string Q2RuleExpressible = "no";
    public static readonly string[] Q3InputKinds = ["free_doc", "image", "conversation"];
    public const string Q4Frequency = "rare";
    public const int Q4BatchSizeBelow = 5;
    public const string PartialRuleExpressible = "partial";
    public static readonly string[] GateFailureImpacts = ["external", "safety_legal"];
    public const string OveruseCurrentActor = "ai_each_time";
    public const string CheckExistingFeature = "unknown";

    // 4.7 확인 항목 키.
    public const string CheckMeasureMinutesPerItem = "measure_minutes_per_item";
    public const string CheckConfirmExistingFeature = "confirm_existing_feature";

    // 4.6 기본값 표시. 공백 하나 + 지시문 3.3 문구.
    public const string DefaultMark = " (기본값 사용, 확인 필요)";

    // 4.5 문장 틀. 그 잎을 정한 답만 쓴다.
    public const string Q1Reason = "기존 프로그램에 같은 기능이 있으므로(yes) 기존 기능·설정으로 분류.";

    public const string Q2Reason = "판단 기준을 글로 쓸 수 없으므로(no) 실행 시 AI 보조 + 사람 확인으로 분류. 자동 승인은 하지 않는다.";

    public const string PartialReason = "판단 기준을 일부만 글로 쓸 수 있으므로(partial) 예외 건은 사람이 검토한다.";

    public static string Q3Reason(string inputKind) =>
        $"입력이 {InputKindPhrase(inputKind)}({inputKind}) 입력 양식 표준화를 먼저 검토하고, 안 되면 실행 시 AI 보조로 분류. 해석만 AI, 계산·저장은 코드로 한다.";

    public static string Q4Reason(int batchSize) =>
        $"드물게 반복되고 한 번에 {Number(batchSize)}건이므로 그대로 두거나 일회성 AI 보조로 분류. 개발비가 더 크다.";

    public static string ElseReason(string ruleExpressible, string inputKind, string frequency, int batchSize) =>
        $"{RuleExpressiblePhrase(ruleExpressible)}({ruleExpressible}), 입력이 {InputKindPhrase(inputKind)}({inputKind}), {FrequencyPhrase(frequency, batchSize)} 규칙 기반 소프트웨어로 분류.";

    public static string GateReason(string failureImpact) =>
        $"틀리면 {FailureImpactPhrase(failureImpact)}({failureImpact}) 이 단계 뒤에 사람 게이트를 둔다.";

    // 4.5 문구 표. 표에 없는 값은 파서가 막는다(case-schema 4.6). 들어오면 예외를 던진다.
    private static string InputKindPhrase(string value) => value switch
    {
        "table" => "표 형식이며",
        "fixed_form" => "고정 양식이며",
        "free_doc" => "자유 형식 문서이므로",
        "image" => "이미지이므로",
        "conversation" => "대화이므로",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "4.5 문구 표에 없는 input_kind 값"),
    };

    private static string RuleExpressiblePhrase(string value) => value switch
    {
        "yes" => "판단 기준을 글로 쓸 수 있고",
        "partial" => "판단 기준을 일부만 글로 쓸 수 있고",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "4.5 문구 표에 없는 rule_expressible 값"),
    };

    private static string FrequencyPhrase(string value, int batchSize) => value switch
    {
        "daily" => "매일 반복되므로",
        "weekly" => "매주 반복되므로",
        "monthly" => "매월 반복되므로",
        "rare" => $"드물게 반복되지만 한 번에 {Number(batchSize)}건이므로",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "4.5 문구 표에 없는 frequency 값"),
    };

    private static string FailureImpactPhrase(string value) => value switch
    {
        "external" => "외부에 영향이 있으므로",
        "safety_legal" => "안전·법규에 영향이 있으므로",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "4.5 문구 표에 없는 failure_impact 값"),
    };

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
