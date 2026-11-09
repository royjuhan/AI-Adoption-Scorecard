using Scorecard.Core.Classification;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;

namespace Scorecard.Core.Report;

// 스코어카드 결과 객체. docs/spec/report.md 3절, 데이터 3절 표.
// HTML과 scorecard.json(open --json)은 이 객체 하나에서 만든다(지시문 2절 7, ADR 0004 결정 6).
// 숫자·판정·문장은 모두 Core가 채운다. HTML은 표시만 한다.
// schema 2(T-022, ADR 0016): Scenario·Status·Conclusion·TaskSummary, 의견 차이(Opinions)·개선 후보(Candidates)를 위험에서 나눔,
// 할 일 묶음(Todo.Group), 사용자가 준 숫자·확인 질문·자기 검사를 더했다.
public sealed record ScorecardResult(
    string Case,
    string? Task,
    string? Scenario,
    ScorecardStatus Status,
    ScorecardConclusion Conclusion,
    Cases.CaseTaskSummary? TaskSummary,
    string? Intake,
    string Answers,
    ScorecardSummary Summary,
    IReadOnlyList<ScorecardStep> Steps,
    ScorecardProposal? Proposal,
    ScorecardVerification Verification,
    IReadOnlyList<ScorecardGate> Gates,
    IReadOnlyList<ScorecardNote> Risks,
    IReadOnlyList<ScorecardNote> Opinions,
    IReadOnlyList<ScorecardNote> Candidates,
    IReadOnlyList<ScorecardTodo> Todo,
    IReadOnlyList<string> StatedNumbers,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<Cases.SelfCheckItem> SelfCheck,
    IReadOnlyList<FileHash> Inputs,
    IReadOnlyList<FileHash> Results,
    FileHash? Review);

// 상태 네 줄(report.md 4.10). Material: virtual, real, unknown(적지 않음). Classification: ai_draft(AI 판단 단계가 있음), rule_tree.
// Confirmed: 확인된 단계 수(맞음+바꿈), Steps: 전체 단계 수. ConfirmedIn: 확인 파일의 confirmed_in(없으면 null). Test: verification.status.
public sealed record ScorecardStatus(string Material, string Classification, int Confirmed, int Steps, string? ConfirmedIn, string Test);

// 결론 세 줄(report.md 4.11): 유지할 것, 먼저 확인할 것 하나, 아직 판단하지 않은 것. EndState는 답 파일의 end_state 그대로(없으면 null).
public sealed record ScorecardConclusion(string Keep, string FirstCheck, string Undecided, string? EndState);

// 할 일 한 줄. Group: now(지금 할 일), improve(개선을 진행하려면), verify(실제 효과까지 검증하려면), implementer(구현·검증 담당자 안내).
public sealed record ScorecardTodo(string Group, string Key, int Step, string Text);

// 요약(1구역). Software는 단계 행 담당이 ② 또는 ②M인 단계 수다. Assignees는 ① ② ②M ③ ④ 다섯 개, 이 순서다.
// Saving은 절약 추정 키다. 시간을 받는 길(--times)이 아직 없어 늘 time_needed다(report.md 4.1).
// Stated·Tentative(T-022): 같은 셈을 사용자가 말한 것만으로 낸 것과 추정·기본값이 섞여 확인하면 바뀔 수 있는 것으로 나눈다. 둘을 더하면 위 셈이다.
// Minutes: 사용자가 준 건당 시간(기본값이 아닌 minutes_per_item). 절감 계산에는 쓰지 않는다.
public sealed record ScorecardSummary(
    int Steps,
    int Software,
    IReadOnlyList<AssigneeCount> Assignees,
    int Differs,
    int AiAssist,
    int AiOveruse,
    int Kept,
    int Changed,
    string Saving,
    SummaryCounts Stated,
    SummaryCounts Tentative,
    IReadOnlyList<StepMinutes> Minutes);

// 단계 수 셈. Steps는 그 쪽에 든 단계 수다.
public sealed record SummaryCounts(int Steps, int Software, int AiAssist, int AiOveruse);

public sealed record StepMinutes(int Step, decimal Minutes);

public sealed record AssigneeCount(string Assignee, int Count);

// 단계 하나(2구역). Assignee·Rule·Reason은 rows[0](단계 행)이다. Extra는 rows[1..](예외 검토·사람 게이트)다.
// CurrentActor·Inferred는 케이스 폴더 answers.yaml에서 읽는다. 쓸 수 없으면 null·빈 목록이다(report.md 4.3).
// T-022: Tentative는 판단이 추정·기본값에 기대는가(4.12). Now·Change·Check는 단계 표 네 칸 중 세 칸(AI 선택 항목 또는 Core 문장).
// Assumptions는 가정한 내용(업무 말). Actions·GateNote는 AI 선택 항목 그대로다.
public sealed record ScorecardStep(
    int Number,
    string Name,
    string Source,
    string Assignee,
    string Rule,
    string Reason,
    RuleOpinion RuleOpinion,
    bool Differs,
    string? CurrentActor,
    IReadOnlyList<string> Defaults,
    IReadOnlyList<string> Inferred,
    IReadOnlyList<string> Checks,
    bool AiOveruseCandidate,
    IReadOnlyList<ScorecardRow> Extra,
    StepReview Review,
    bool Tentative,
    string Now,
    string Change,
    string Check,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<Cases.StepAction> Actions,
    string? GateNote);

public sealed record ScorecardRow(string Kind, string Assignee, string Rule, string Reason);

// 확인 결과. Status: pending(확인 전), kept(맞음), changed(바꿈). changed면 Assignee·Reason이 있다.
public sealed record StepReview(string Status, string? Assignee, string? Reason);

// 제안 카드(3구역). 먼저 확인할 ② 단계 하나(report.md 4.4). Reason은 고른 이유(의뢰서 번들이 그대로 쓴다).
// Check는 먼저 확인할 것(단계의 Check), Effect는 효과 문장, Tentative는 추정이 섞인 단계 가운데서 골랐나(T-022).
public sealed record ScorecardProposal(int Step, string Name, string Reason, string Check, string Effect, bool Tentative);

// 검증(4구역). Status: measured, not_verified, not_checked(metrics.json 없음).
// Counts·퍼센트는 metrics.json, Verdict·Checks·Thresholds·Time은 judgment.json 그대로다. Items는 앞 ItemLimit개, ItemsHidden은 나머지 수다.
public sealed record ScorecardVerification(
    string Status,
    string? Reason,
    CompareCounts? Counts,
    decimal? AccuracyPct,
    decimal? MissingRatePct,
    string? Verdict,
    string? VerdictRule,
    string? VerdictReason,
    JudgmentThresholds? Thresholds,
    JudgmentCheck? Time,
    IReadOnlyList<JudgmentCheck> Checks,
    IReadOnlyList<VerificationItem> Items,
    int ItemsHidden);

public sealed record VerificationItem(string Key, string Status, IReadOnlyList<FieldDifference> Differences);

// 사람 게이트(5구역). Kind: signoff(서명 단계), exception_review, human_gate.
// Text는 화면 문장(T-022, 지시문 4절): 사람 게이트 행은 그 단계 AI 판단의 gate_note, 없으면 종류마다 정한 담당자 확인 문장.
// Reason은 엔진 문장 그대로다(의뢰서 번들이 쓴다). 화면은 Text를 보이고 Reason은 펼쳐 보기에 둔다.
public sealed record ScorecardGate(int Step, string Name, string Kind, string Reason, string Text);

// 위험·다음 할 일 한 줄. Step은 단계 번호, 단계와 상관없으면 0이다.
public sealed record ScorecardNote(string Key, int Step, string Text);

// 파일 하나의 SHA-256(소문자 16진). Path는 케이스 폴더 기준 상대 경로('/' 구분), 결과 파일은 출력 폴더 안 이름이다.
public sealed record FileHash(string Path, string Sha256);

// 확인 파일 한 항목(report.md 5절). Answer: keep, change.
public sealed record ReviewEntry(int Number, string Answer, string? Assignee, string? Reason);

// 확인 파일 하나. ConfirmedIn: conversation(스킬이 대화에서 확인받아 씀) 또는 null.
public sealed record ReviewFile(IReadOnlyList<ReviewEntry> Entries, string? ConfirmedIn);

// 스코어카드를 만들 바이트. Results는 출력 폴더의 결과 파일(이름), Inputs는 케이스 폴더의 입력 파일(상대 경로)이다.
public sealed record ScorecardSources(IReadOnlyList<SourceFile> Results, IReadOnlyList<SourceFile> Inputs, SourceFile? Review);

public sealed record ScorecardBuildResult(ScorecardResult? Scorecard, IReadOnlyList<Cases.CaseError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
