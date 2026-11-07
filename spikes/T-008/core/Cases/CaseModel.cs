namespace Scorecard.Core.Cases;

// case.yaml 하나의 내용. docs/spec/case-schema.md 4.3·4.12.
// 열거 값은 YAML 문자열 그대로 둔다(서수 비교). 숫자는 decimal이다.
// 선택 필드가 없으면 null이다. compare는 빈 목록, thresholds는 세 값 모두 null이다.
public sealed record CaseDefinition(
    string Name,
    string Task,
    string? Before,
    string? After,
    string? AiUsage,
    IReadOnlyList<CaseStep> Steps,
    CaseInputs? Inputs,
    IReadOnlyList<CompareField> Compare,
    CaseThresholds Thresholds);

// 단계 하나. Number는 1부터다. 오류 경로 steps[n]의 n과 같다.
public sealed record CaseStep(int Number, string Name, StepAnswers Answers);

// 답 10개. 순서는 스펙 4.4 표(지시문 3.2)와 같다. HumanSignoff는 T-008(ADR 0003)에서 더했다.
public sealed record StepAnswers(
    Answer<string> InputKind,
    Answer<string> RuleExpressible,
    Answer<string> Frequency,
    Answer<int> BatchSize,
    Answer<decimal> ExceptionRate,
    Answer<string> CurrentActor,
    Answer<decimal?> MinutesPerItem,
    Answer<string> FailureImpact,
    Answer<string> ExistingFeature,
    Answer<string> HumanSignoff);

// 답 하나. IsDefault가 true면 키가 없거나 값이 null이라 기본값을 넣었다.
// "기본값 사용, 확인 필요" 표시의 근거다(스펙 4.4).
// IsInferred가 true면 단계의 inferred 목록에 이 키가 있다(AI 추정, 스펙 4.13). 값이 있어야 하므로 IsDefault와 같이 true가 되지 않는다.
public sealed record Answer<T>(T Value, bool IsDefault, bool IsInferred = false);

// 대조 입력. 경로는 케이스 폴더 기준 상대 경로다. glob 문자는 그대로 둔다(해석은 T-006).
public sealed record CaseInputs(string Reference, string Candidate, string Key);

// 비교 필드 하나. 허용 오차는 하나만 있거나 둘 다 없다.
public sealed record CompareField(string Field, decimal? Tolerance, decimal? TolerancePct);

// 판정 기준. 없으면 null(미입력)이다. 기본값은 판정 카드에서 정한다(스펙 4.5).
public sealed record CaseThresholds(decimal? AccuracyMin, decimal? MissingMax, bool? TimeMustDrop);
