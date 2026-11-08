namespace Scorecard.Core.Cases;

// 단계마다 적는 답 10개(지시문 3.2). 키·선택지·범위·기본값을 이 파일 한 곳에만 둔다.
// 원본은 docs/spec/case-schema.md 4.4 표다. 값을 바꾸려면 스펙 표를 먼저 고친다.
// CaseDefaultsTests가 스펙 표를 읽어 파서 결과와 대조한다(승인 결정 5, 지시문 13절).
internal static class StepQuestions
{
    public static readonly EnumQuestion InputKind =
        new("input_kind", ["table", "fixed_form", "free_doc", "image", "conversation"], "fixed_form");

    public static readonly EnumQuestion RuleExpressible = new("rule_expressible", ["yes", "partial", "no"], "partial");

    public static readonly EnumQuestion Frequency = new("frequency", ["daily", "weekly", "monthly", "rare"], "weekly");

    public static readonly IntegerQuestion BatchSize = new("batch_size", Min: 1, Default: 10);

    public static readonly NumberQuestion ExceptionRate = new("exception_rate", Min: 0m, Max: 100m, Default: 20m);

    public static readonly EnumQuestion CurrentActor =
        new("current_actor", ["human", "ai_each_time", "program", "mixed"], "human");

    // 기본값 null. 측정 권고 표시는 T-004에서 한다.
    public static readonly MeasureQuestion MinutesPerItem = new("minutes_per_item", Min: 0m);

    public static readonly EnumQuestion FailureImpact =
        new("failure_impact", ["internal_rework", "external", "safety_legal"], "internal_rework");

    public static readonly EnumQuestion ExistingFeature = new("existing_feature", ["yes", "no", "unknown"], "unknown");

    // 사람이 최종 확인·서명하는 단계인가. 단계마다 묻지 않고 서명 단계를 한 번 물어 채운다(ADR 0003). 기본값은 no.
    public static readonly EnumQuestion HumanSignoff = new("human_signoff", ["yes", "no"], "no");

    // answers 매핑에 쓸 수 있는 키. 표 순서다. 위 필드보다 뒤에 두어야 초기화 순서가 맞다.
    public static readonly string[] Keys =
    [
        InputKind.Key,
        RuleExpressible.Key,
        Frequency.Key,
        BatchSize.Key,
        ExceptionRate.Key,
        CurrentActor.Key,
        MinutesPerItem.Key,
        FailureImpact.Key,
        ExistingFeature.Key,
        HumanSignoff.Key,
    ];

    // 단계 항목 ai의 담당 선택지(스펙 4.14, ADR 0013). 서수 비교다. ②M은 학습 모델(결정론)이다.
    public static readonly string[] AiAssignees = ["①", "②", "②M", "③", "④"];
}

// 열거 질문. 선택지는 서수(대소문자 구분)로 비교한다.
internal sealed record EnumQuestion(string Key, string[] Choices, string Default);

// 정수 질문. 위 한도는 int 최댓값이다.
internal sealed record IntegerQuestion(string Key, int Min, int Default);

// 숫자 질문. 범위는 Min 이상 Max 이하다.
internal sealed record NumberQuestion(string Key, decimal Min, decimal Max, decimal Default);

// 측정값 질문. 기본값이 null이고 위 한도가 없다.
internal sealed record MeasureQuestion(string Key, decimal Min);
