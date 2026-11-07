namespace Scorecard.Core.Classification;

// T-004 스파이크 후보(머지 대상 아님). 분류 결과 모델.
// 단계 하나(사용자가 준 단계)마다 StepClassification 하나. 그 안에 행이 1~3개 있다.
// 행: 단계 자체(step) + 예외 건 사람 검토(exception_review) + 사람 게이트(human_gate).
public sealed record ClassificationResult(string Case, IReadOnlyList<StepClassification> Steps);

// Number는 case.yaml 단계 번호(1부터). Name은 자유 서술 그대로.
// Defaults: IsDefault인 답의 키(4.4 표 순서). Checks: 확인 항목 키.
public sealed record StepClassification(
    int Number,
    string Name,
    IReadOnlyList<string> Defaults,
    IReadOnlyList<string> Checks,
    bool AiOveruseCandidate,
    IReadOnlyList<ClassificationRow> Rows);

// Kind: step / exception_review / human_gate. Assignee: ① ② ③ ④. Rule: 근거 키.
// Used: 이 행을 정할 때 트리가 읽은 답의 키(4.4 표 순서).
public sealed record ClassificationRow(
    string Kind,
    string Assignee,
    string Rule,
    IReadOnlyList<string> Used,
    string Reason);
