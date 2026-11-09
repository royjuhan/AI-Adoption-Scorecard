namespace Scorecard.Core.Classification;

// 분류 결과. docs/spec/classification.md 3.1·4.11.
// 사용자가 준 단계마다 StepClassification 하나. 그 안에 행이 1~3개 있다.
// 행 순서: 단계(step) → 예외 건 사람 검토(exception_review) → 사람 게이트(human_gate)(4.3). SIGNOFF 단계는 step 행 하나다.
public sealed record ClassificationResult(string Case, IReadOnlyList<StepClassification> Steps);

// Number는 case.yaml 단계 번호(1부터)다. Name은 자유 서술 그대로다.
// Source: 단계에 ai가 있으면 "ai", 없으면 "rule"(3.2·4.13, ADR 0013).
// RuleOpinion: 트리 step 행의 담당·근거 키. Source와 상관없이 늘 있다.
// Differs: Source가 ai이고 AI 담당의 비교 값(②M은 ②)이 RuleOpinion 담당과 다르면 true.
// Defaults: 답 10개 중 IsDefault인 키(case-schema 4.4 표 순서).
// Inferred: 답 10개 중 IsInferred인 키(같은 순서, 4.12). classification.json에는 쓰지 않는다(3.2).
// Checks: 확인 항목 키(4.7 표 순서).
// AiOveruseCandidate: current_actor가 ai_each_time이고 Rows[0] 담당의 비교 값이 ②(4.8).
// Detail: AI 판단의 선택 항목(동작 목록·네 칸 문장·담당자 확인 메모, T-022). 분류에 쓰지 않고 그대로 옮긴다(3.2, 4.14).
public sealed record StepClassification(
    int Number,
    string Name,
    string Source,
    RuleOpinion RuleOpinion,
    bool Differs,
    IReadOnlyList<string> Defaults,
    IReadOnlyList<string> Inferred,
    IReadOnlyList<string> Checks,
    bool AiOveruseCandidate,
    IReadOnlyList<ClassificationRow> Rows,
    Cases.StepAiDetail? Detail = null);

// 규칙 의견. 트리 step 행의 담당(①~④)과 근거 키(SIGNOFF, Q1~Q4, ELSE).
public sealed record RuleOpinion(string Assignee, string Rule);

// Kind: step, exception_review, human_gate. Assignee: ①~④, AI 행이면 ②M도. Rule: 근거 키(4.4). AI 행은 AI.
// AI 행의 Used는 빈 목록이고 Reason은 ai.reason 그대로다(표시 없음).
// Used: 이 행을 정할 때 트리가 읽은 답의 키(4.6 표, case-schema 4.4 표 순서). JSON에는 쓰지 않는다.
// Reason: 근거 문장(4.5). 기본값 표시(4.6)와 추정 표시(4.12)를 포함한다.
public sealed record ClassificationRow(
    string Kind,
    string Assignee,
    string Rule,
    IReadOnlyList<string> Used,
    string Reason);
