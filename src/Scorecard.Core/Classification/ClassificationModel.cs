namespace Scorecard.Core.Classification;

// 분류 결과. docs/spec/classification.md 3.1·4.11.
// 사용자가 준 단계마다 StepClassification 하나. 그 안에 행이 1~3개 있다.
// 행 순서: 단계(step) → 예외 건 사람 검토(exception_review) → 사람 게이트(human_gate)(4.3). SIGNOFF 단계는 step 행 하나다.
public sealed record ClassificationResult(string Case, IReadOnlyList<StepClassification> Steps);

// Number는 case.yaml 단계 번호(1부터)다. Name은 자유 서술 그대로다.
// Defaults: 답 10개 중 IsDefault인 키(case-schema 4.4 표 순서).
// Inferred: 답 10개 중 IsInferred인 키(같은 순서, 4.12). classification.json에는 쓰지 않는다(3.2).
// Checks: 확인 항목 키(4.7 표 순서).
// AiOveruseCandidate: current_actor가 ai_each_time이고 step 행이 ②(4.8).
public sealed record StepClassification(
    int Number,
    string Name,
    IReadOnlyList<string> Defaults,
    IReadOnlyList<string> Inferred,
    IReadOnlyList<string> Checks,
    bool AiOveruseCandidate,
    IReadOnlyList<ClassificationRow> Rows);

// Kind: step, exception_review, human_gate. Assignee: ①~④. Rule: 근거 키(4.4).
// Used: 이 행을 정할 때 트리가 읽은 답의 키(4.6 표, case-schema 4.4 표 순서). JSON에는 쓰지 않는다.
// Reason: 근거 문장(4.5). 기본값 표시(4.6)와 추정 표시(4.12)를 포함한다.
public sealed record ClassificationRow(
    string Kind,
    string Assignee,
    string Rule,
    IReadOnlyList<string> Used,
    string Reason);
