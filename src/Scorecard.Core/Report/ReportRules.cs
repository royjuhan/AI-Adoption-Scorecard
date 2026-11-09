namespace Scorecard.Core.Report;

// 스코어카드의 값·문장을 이 파일 한 곳에 둔다(classification.md 4.9와 같은 방식). docs/spec/report.md 4절, 데이터 4절 표.
// 값을 바꾸려면 스펙 표를 먼저 고친다. 테스트가 스펙 표를 읽어 대조한다.
internal static class ReportRules
{
    // 결과 객체 schema_version(ADR 0008 결정 1). T-022에서 2: 위험에서 의견 차이·개선 후보를 나누고 할 일에 묶음을 더했다(뜻이 바뀜, ADR 0016).
    public const int SchemaVersion = 2;

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

    // 담당자 확인 화면 문장(4.6, T-022). "사람 게이트"라는 말을 쓰지 않는다(지시문 4.1). 사람 게이트 행은 gate_note가 있으면 그것.
    public static readonly (string Kind, string Text)[] GateTexts =
    [
        ("signoff", "담당자가 최종 확인·서명한다."),
        ("exception_review", "규칙으로 판단이 안 되는 예외 건은 담당자가 검토한다."),
        ("human_gate", "이 단계 결과를 다음으로 넘기기 전에 담당자가 확인한다."),
    ];

    // 담당 순서(요약). 단계 행 담당의 값이다.
    public static readonly string[] AssigneeOrder = ["①", "②", "②M", "③", "④"];

    // 소프트웨어로 대체할 수 있는 담당(4.1, 4.4).
    public static readonly string[] SoftwareAssignees = ["②", "②M"];

    // 제안 순서(4.4): 사용자가 말한 것만으로 정한 단계 먼저, 그다음 지금 하는 쪽. 앞일수록 먼저다. 표에 없으면(모름) 맨 뒤.
    // Text는 의뢰서 번들이 그대로 쓰는 이유 문장이라 T-022에서 바꾸지 않았다. 효과 문장은 ProposalEffect다.
    public static readonly (string Actor, string Text)[] ProposalOrder =
    [
        ("ai_each_time", "AI 과잉 후보다. 지금 매번 AI에 맡기지만 규칙으로 된다."),
        ("human", "② 단계 가운데 아직 사람이 하는 첫 단계다."),
        ("mixed", "② 단계 가운데 아직 사람 손이 가는 첫 단계다."),
        ("program", "지금 프로그램이 하는 단계다. 실행 시 AI 없는 규칙 SW로 정리한다."),
    ];

    public const string ProposalUnknown = "② 단계 가운데 첫 단계다.";
    public const string ProposalExceptionTail = " 애매한 건은 사람 검토로 남긴다.";

    // 효과 문장(지시문 3.5, T-022). 절감은 계산하지 않았으므로 왜 이것을 먼저 보라는지 쓴다.
    public const string ProposalEffect = "절감 효과가 가장 크다고 확인된 건 아니다. 기존 설정으로 해결될 가능성이 있어 비용을 들이기 전에 먼저 확인할 항목으로 골랐다.";
    public const string ProposalEffectOveruse = "지금 매번 AI에 맡기는 일이 규칙으로 되면 실행 때 AI가 필요 없다. 절감 효과는 아직 계산하지 않았다.";
    public const string ProposalTentativeTail = " 이 단계의 판단은 추정·기본값에 기대고 있어 확인하면 바뀔 수 있다.";

    // 상태 네 줄(4.10).
    public const string MaterialUnknown = "unknown";
    public const string ClassificationAiDraft = "ai_draft";
    public const string ClassificationRuleTree = "rule_tree";

    // 단계 표 지금 하는 방식(4.12). AI 선택 항목 now가 없을 때. {actor}는 지금 하는 쪽 이름.
    public static readonly (string Actor, string Name, string Text)[] Actors =
    [
        ("human", "사람", "사람이 손으로 한다."),
        ("ai_each_time", "매번 AI", "매번 AI에 맡긴다."),
        ("program", "프로그램", "프로그램이 한다."),
        ("mixed", "사람과 프로그램", "사람과 프로그램이 나눠 한다."),
    ];

    public const string NowInferredText = "지금 방식은 확인되지 않았다. {actor}가 한다고 가정했다.";
    public const string NowUnknownText = "지금 방식은 말하지 않았다.";

    // 단계 표 바꿔야 할 것(4.12). 담당마다. "같은 결과를 낸다"고 단정하지 않는다(지시문 1.5).
    public static readonly (string Assignee, string Text)[] ChangeTexts =
    [
        ("①", "바꿀 것 없음. 지금 사람 손 없이 도는지만 확인한다."),
        ("②", "규칙으로 검사·처리할 수 있는지 검토한다. 규칙으로 안 되는 부분은 따로 적는다."),
        ("②M", "미리 학습해 고정한 모델로 처리할 수 있는지 검토한다. 실행 때 AI를 부르지 않는다."),
        ("③", "AI 초안을 쓰고 담당자가 확인한다. 자동 승인은 하지 않는다."),
        ("④", "바꾸지 않는다. 사람이 판단한다."),
    ];

    // 단계 표 확인할 것(4.12). 담당마다. AI 선택 항목 check가 없을 때.
    public static readonly (string Assignee, string Text)[] CheckTexts =
    [
        ("①", "정말 사람 손 없이 도는지 확인한다."),
        ("②", "기존 기능·설정으로 되는지 먼저 확인한다. 안 되면 새로 만들지 판단한다."),
        ("②M", "기존 기능·설정으로 되는지 먼저 확인한다. 안 되면 새로 만들지 판단한다."),
        ("③", "AI 초안을 누가 언제 확인하는지 정한다."),
        ("④", "없음."),
    ];

    // 가정한 내용(4.12). 추정 답과 기본값 답을 업무 말로 쓴다. {label} 질문 이름, {value} 값 이름.
    public const string AssumedText = "가정: {label} {value}";
    public const string DefaultedText = "말하지 않아 기본값: {label} {value}";

    // 답 키의 업무 말 이름과 값 이름(case-schema 4.4 순서). 숫자 값은 {n}에 넣는다.
    public static readonly (string Key, string Label, (string Value, string Name)[] Values, string? NumberFormat)[] AnswerWords =
    [
        ("input_kind", "입력 형태", [("table", "표"), ("fixed_form", "정해진 양식"), ("free_doc", "자유 문서"), ("image", "이미지"), ("conversation", "대화")], null),
        ("rule_expressible", "판단 기준을 글로 쓸 수 있나", [("yes", "쓸 수 있음"), ("partial", "일부만"), ("no", "쓸 수 없음")], null),
        ("frequency", "처리 주기", [("daily", "매일"), ("weekly", "주 1회"), ("monthly", "월 1회"), ("rare", "드물게")], null),
        ("batch_size", "한 번에 처리하는 건수", [], "{n}건"),
        ("exception_rate", "예외 비율", [], "{n}%"),
        ("current_actor", "지금 하는 쪽", [("human", "사람"), ("ai_each_time", "매번 AI"), ("program", "프로그램"), ("mixed", "사람과 프로그램")], null),
        ("minutes_per_item", "건당 시간", [], "{n}분"),
        ("failure_impact", "틀렸을 때 영향", [("internal_rework", "내부에서 다시 함"), ("external", "외부에 나감"), ("safety_legal", "안전·법적 문제")], null),
        ("existing_feature", "기존 기능이 있나", [("yes", "있음"), ("no", "없음"), ("unknown", "모름")], null),
        ("human_signoff", "최종 서명 단계인가", [("yes", "서명 단계"), ("no", "서명 단계 아님")], null),
    ];

    // 결론 세 줄(4.11). {steps}는 "1, 2단계".
    public const string KeepHumanText = "{steps}는 사람이 판단하는 일로 지금처럼 둔다.";
    public const string KeepExistingText = "{steps}는 지금 기능 그대로 둔다.";
    public const string KeepGatesText = "담당자 확인 {n}곳은 그대로 둔다.";
    public const string KeepNoneText = "그대로 둘 단계로 정한 것은 없다.";
    public const string FirstCheckText = "{n}단계 {name}: {check}";
    public const string FirstCheckNoneText = "먼저 확인할 것이 없다.";
    public const string UndecidedTentativeText = "추정·기본값에 기댄 판단 {n}단계(확인하면 바뀔 수 있음).";
    public const string UndecidedGatesText = "추정·기본값에 기댄 담당자 확인 {n}곳(확인하면 바뀔 수 있음).";
    public const string UndecidedQuestionsText = "확인 질문 {n}개가 남았다.";
    public const string UndecidedTestText = "실제 자료 시험은 진행하지 않았다.";
    public const string UndecidedSavingText = "절감 효과는 계산하지 않았다.";

    // 의견 차이(4.7). AI 판단과 규칙 의견이 다를 때 추천 결론과 이유를 먼저 쓴다(지시문 3.4). 규칙 의견은 펼쳐 보기.
    public const string OpinionDiffers = "differs";
    public const string OpinionText = "{n}단계 {name}: {a} 권장. 이유: {reason}";

    // 담당의 업무 말 이름(결론·의견 문장 안).
    public static readonly (string Assignee, string Name)[] AssigneeNames =
    [
        ("①", "기존 기능 유지"),
        ("②", "규칙 자동화 검토"),
        ("②M", "고정 학습 모델 검토"),
        ("③", "AI 초안 + 담당자 확인"),
        ("④", "담당자 직접 처리"),
    ];

    // 개선 후보(4.7). AI 과잉 후보. 지금 하는 쪽이 추정이면 추정형으로 쓴다(지시문 2.1).
    public const string CandidateOveruse = "ai_overuse";
    public const string CandidateOveruseText = "{n}단계 {name}: 지금 매번 AI에 맡긴다. 규칙으로 할 수 있는지 검토할 후보다.";
    public const string CandidateOveruseTentativeText = "{n}단계 {name}: 현재 방식은 확인되지 않았다. AI가 하고 있다면 규칙으로 바꿀 후보다.";

    // 할 일 묶음(4.8).
    public const string TodoNow = "now";
    public const string TodoImprove = "improve";
    public const string TodoVerify = "verify";
    public const string TodoImplementer = "implementer";

    public const string TodoFirstCheck = "first_check";
    public const string TodoFirstCheckText = "먼저 확인할 것부터 본다. 기존 기능으로 되면 끝, 안 되면 새로 만들지 판단하고, 만들기로 하면 의뢰서를 낸다.";
    public const string TodoDefineErrors = "define_errors";
    public const string TodoDefineErrorsText = "실제 검증 때 무엇을 오류로 셀지 먼저 정한다.";
    public const string TodoCheckCommand = "check_command";
    public const string TodoCheckCommandText = "기준 자료(사람이 만든 결과)는 reference/, 결과 자료(AI 적용 후)는 candidate/에 넣고 case.yaml에 inputs·compare·thresholds를 적은 뒤 scorecard check를 돌린다.";
    public const string TodoReviewFile = "review_file";
    public const string TodoReviewFileText = "단계 확인 결과는 확인 파일(--review)로 open에 넣는다.";
    public const string TodoBuildCommand = "build_command";
    public const string TodoBuildCommandText = "만들기로 정하면 scorecard build {case} --step {n} 명령으로 의뢰서 번들을 쓴다. 내보내기 전에 export-check.md에 사람이 확인을 적는다.";

    // 위험 키와 문장(4.7). {n} 단계 번호, {name} 단계 이름, {a} AI 담당, {r} 규칙 의견 담당, {old}/{new} 확인 전후 담당.
    // T-022: 위험에는 "무엇이 틀릴 수 있고 지금 어떻게 잡는가"만 둔다. 다름(differs)은 의견 차이, AI 과잉 후보는 개선 후보로 옮겼다.
    public const string RiskAiAssist = "ai_assist";
    public const string RiskVerdict = "verdict";
    public const string RiskReviewChanged = "review_changed";

    public const string RiskAiAssistText = "{n}단계 {name}: AI 초안이 틀릴 수 있다. 자동 승인하지 않고 담당자가 확인해 잡는다.";
    public const string RiskVerdictText = "검증 판정이 {verdict}다. {reason}";
    public const string RiskReviewChangedText = "{n}단계 {name}: 확인에서 담당을 바꿨다({old} → {new}). 셈·제안·판정은 다시 계산하지 않아 바뀐 담당이 반영되지 않았다.";

    // 판정 이름(위험 문장 안). 통과·미검증은 위험에 넣지 않는다.
    public static readonly (string Verdict, string Name)[] RiskVerdicts = [("hold", "보류"), ("conditional", "조건부")];

    // 다음 할 일 키와 문장(4.8). judgment.json todo는 그대로 앞에 둔다.
    public const string TodoRunCheck = "run_check";
    public const string TodoMeasureMinutes = "measure_minutes_per_item";
    public const string TodoConfirmDefaults = "confirm_defaults";
    public const string TodoConfirmInferred = "confirm_inferred";
    public const string TodoReviewSteps = "review_steps";

    public const string TodoRunCheckText = "실제 효과까지 보려면 사람이 만든 결과와 AI 적용 결과를 한 벌씩 준비해 비교한다.";
    public const string TodoMeasureMinutesText = "개선 전후를 비교하려면 단계별 건당 시간을 잰다: {steps}.";
    public const string TodoConfirmDefaultsText = "말하지 않아 기본값으로 둔 답을 확인한다: {list}.";
    public const string TodoConfirmInferredText = "AI가 가정한 답을 확인한다: {list}.";
    public const string TodoReviewStepsText = "단계마다 맞는지 확인한다(맞음/바꾸기): {steps}.";

    // 기본값 확인 목록에서 뺀 키. 건당 시간은 measure 할 일이 따로 있다.
    public const string MinutesPerItem = "minutes_per_item";

    // 답 키의 업무 말 이름. 표에 없으면 키 그대로.
    public static string AnswerLabel(string key) =>
        AnswerWords.FirstOrDefault(w => w.Key == key).Label ?? key;

    // 답 값의 업무 말. 숫자 질문은 NumberFormat, 열거는 값 이름. 표에 없으면 값 그대로.
    public static string AnswerValue(string key, string value)
    {
        var words = AnswerWords.FirstOrDefault(w => w.Key == key);
        if (words.NumberFormat is { } format)
        {
            return format.Replace("{n}", value, StringComparison.Ordinal);
        }

        return words.Values?.FirstOrDefault(v => v.Value == value).Name ?? value;
    }

    public static string AssigneeName(string assignee) =>
        AssigneeNames.FirstOrDefault(a => a.Assignee == assignee).Name ?? assignee;

    // "1, 2, 3단계"
    public static string StepList(IEnumerable<int> numbers) =>
        string.Join(", ", numbers.Select(n => n.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "단계";
}
