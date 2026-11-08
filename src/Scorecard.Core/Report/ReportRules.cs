namespace Scorecard.Core.Report;

// 스코어카드의 값·문장을 이 파일 한 곳에 둔다(classification.md 4.9와 같은 방식). docs/spec/report.md 4절, 데이터 4절 표.
// 값을 바꾸려면 스펙 표를 먼저 고친다. 테스트가 스펙 표를 읽어 대조한다.
internal static class ReportRules
{
    // 결과 객체 schema_version(ADR 0008 결정 1). 새 문서라 1에서 시작한다.
    public const int SchemaVersion = 1;

    // 검증 항목을 앞에서 몇 개까지 보이나. 나머지는 수만 적는다.
    public const int ItemLimit = 20;

    // 입력 파일 이름(케이스 폴더 바로 아래).
    public const string AnswersFile = "answers.yaml";
    public const string IntakeFile = "intake.md";

    // answers 상태(4.3).
    public const string AnswersUsed = "used";
    public const string AnswersMissing = "missing";
    public const string AnswersInvalid = "invalid";
    public const string AnswersMismatch = "mismatch";

    // 절약 추정 키(4.1). 시간 입력(--times)이 생기면 값이 는다.
    public const string SavingTimeNeeded = "time_needed";

    // 검증 상태(4.5). measured·not_verified는 metrics.json 값 그대로다.
    public const string NotChecked = "not_checked";

    // 확인 상태(5절).
    public const string ReviewPending = "pending";
    public const string ReviewKept = "kept";
    public const string ReviewChanged = "changed";

    // 게이트 종류(4.6).
    public const string GateSignoff = "signoff";

    // 담당 순서(요약). 단계 행 담당의 값이다.
    public static readonly string[] AssigneeOrder = ["①", "②", "②M", "③", "④"];

    // 소프트웨어로 대체할 수 있는 담당(4.1, 4.4).
    public static readonly string[] SoftwareAssignees = ["②", "②M"];

    // 제안 순서(4.4): 지금 하는 쪽. 앞일수록 먼저다. 표에 없으면(모름) 맨 뒤.
    public static readonly (string Actor, string Text)[] ProposalOrder =
    [
        ("ai_each_time", "AI 과잉 후보다. 지금 매번 AI에 맡기지만 규칙으로 된다."),
        ("human", "② 단계 가운데 아직 사람이 하는 첫 단계다."),
        ("mixed", "② 단계 가운데 아직 사람 손이 가는 첫 단계다."),
        ("program", "지금 프로그램이 하는 단계다. 실행 시 AI 없는 규칙 SW로 정리한다."),
    ];

    public const string ProposalUnknown = "② 단계 가운데 첫 단계다.";
    public const string ProposalExceptionTail = " 애매한 건은 사람 검토로 남긴다.";

    // 위험 키와 문장(4.7). {n} 단계 번호, {name} 단계 이름, {a} AI 담당, {r} 규칙 의견 담당, {old}/{new} 확인 전후 담당.
    public const string RiskDiffers = "differs";
    public const string RiskOveruse = "ai_overuse";
    public const string RiskAiAssist = "ai_assist";
    public const string RiskVerdict = "verdict";
    public const string RiskReviewChanged = "review_changed";

    public const string RiskDiffersText = "{n}단계 {name}: AI 판단 {a}와 규칙 의견 {r}이 다르다.";
    public const string RiskOveruseText = "{n}단계 {name}: 지금 매번 AI에 맡기지만 규칙 SW로 된다(AI 과잉 후보).";
    public const string RiskAiAssistText = "{n}단계 {name}: 실행 시 AI 보조다. 자동 승인하지 않고 사람이 확인한다.";
    public const string RiskVerdictText = "검증 판정이 {verdict}다. {reason}";
    public const string RiskReviewChangedText = "{n}단계 {name}: 사람이 {old}를 {new}로 바꿨다. 분류·판정은 다시 계산하지 않았다.";

    // 판정 이름(위험 문장 안). 통과·미검증은 위험에 넣지 않는다.
    public static readonly (string Verdict, string Name)[] RiskVerdicts = [("hold", "보류"), ("conditional", "조건부")];

    // 다음 할 일 키와 문장(4.8). judgment.json todo는 그대로 앞에 둔다.
    public const string TodoRunCheck = "run_check";
    public const string TodoMeasureMinutes = "measure_minutes_per_item";
    public const string TodoConfirmDefaults = "confirm_defaults";
    public const string TodoConfirmInferred = "confirm_inferred";
    public const string TodoReviewSteps = "review_steps";

    public const string TodoRunCheckText = "기준·결과 자료를 넣고 scorecard check를 돌려 검증 결과를 채운다.";
    public const string TodoMeasureMinutesText = "건당 시간(minutes_per_item)을 잰다: {steps}.";
    public const string TodoConfirmDefaultsText = "기본값을 쓴 답을 확인한다: {list}.";
    public const string TodoConfirmInferredText = "AI가 추정한 답을 확인한다: {list}.";
    public const string TodoReviewStepsText = "단계 확인(맞음/바꾸기)이 아직 없다: {steps}. 확인 파일을 --review로 넣는다.";

    // 기본값 확인 목록에서 뺀 키. 건당 시간은 measure 할 일이 따로 있다.
    public const string MinutesPerItem = "minutes_per_item";

    // "1, 2, 3단계"
    public static string StepList(IEnumerable<int> numbers) =>
        string.Join(", ", numbers.Select(n => n.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "단계";
}
