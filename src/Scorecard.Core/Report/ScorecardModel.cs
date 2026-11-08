using Scorecard.Core.Classification;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;

namespace Scorecard.Core.Report;

// 스코어카드 결과 객체. docs/spec/report.md 3절, 데이터 3절 표.
// HTML과 scorecard.json(open --json)은 이 객체 하나에서 만든다(지시문 2절 7, ADR 0004 결정 6).
// 숫자·판정·문장은 모두 Core가 채운다. HTML은 표시만 한다.
public sealed record ScorecardResult(
    string Case,
    string? Task,
    string? Intake,
    string Answers,
    ScorecardSummary Summary,
    IReadOnlyList<ScorecardStep> Steps,
    ScorecardProposal? Proposal,
    ScorecardVerification Verification,
    IReadOnlyList<ScorecardGate> Gates,
    IReadOnlyList<ScorecardNote> Risks,
    IReadOnlyList<ScorecardNote> Todo,
    IReadOnlyList<FileHash> Inputs,
    IReadOnlyList<FileHash> Results,
    FileHash? Review);

// 요약(1구역). Software는 단계 행 담당이 ② 또는 ②M인 단계 수다. Assignees는 ① ② ②M ③ ④ 다섯 개, 이 순서다.
// Saving은 절약 추정 키다. 시간을 받는 길(--times)이 아직 없어 늘 time_needed다(report.md 4.1).
public sealed record ScorecardSummary(
    int Steps,
    int Software,
    IReadOnlyList<AssigneeCount> Assignees,
    int Differs,
    int AiAssist,
    int AiOveruse,
    int Kept,
    int Changed,
    string Saving);

public sealed record AssigneeCount(string Assignee, int Count);

// 단계 하나(2구역). Assignee·Rule·Reason은 rows[0](단계 행)이다. Extra는 rows[1..](예외 검토·사람 게이트)다.
// CurrentActor·Inferred는 케이스 폴더 answers.yaml에서 읽는다. 쓸 수 없으면 null·빈 목록이다(report.md 4.3).
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
    StepReview Review);

public sealed record ScorecardRow(string Kind, string Assignee, string Rule, string Reason);

// 확인 결과. Status: pending(확인 전), kept(맞음), changed(바꿈). changed면 Assignee·Reason이 있다.
public sealed record StepReview(string Status, string? Assignee, string? Reason);

// 제안 카드(3구역). 먼저 만들 ② 단계 하나(report.md 4.4).
public sealed record ScorecardProposal(int Step, string Name, string Reason);

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
public sealed record ScorecardGate(int Step, string Name, string Kind, string Reason);

// 위험·다음 할 일 한 줄. Step은 단계 번호, 단계와 상관없으면 0이다.
public sealed record ScorecardNote(string Key, int Step, string Text);

// 파일 하나의 SHA-256(소문자 16진). Path는 케이스 폴더 기준 상대 경로('/' 구분), 결과 파일은 출력 폴더 안 이름이다.
public sealed record FileHash(string Path, string Sha256);

// 확인 파일 한 항목(report.md 5절). Answer: keep, change.
public sealed record ReviewEntry(int Number, string Answer, string? Assignee, string? Reason);

// 스코어카드를 만들 바이트. Results는 출력 폴더의 결과 파일(이름), Inputs는 케이스 폴더의 입력 파일(상대 경로)이다.
public sealed record ScorecardSources(IReadOnlyList<SourceFile> Results, IReadOnlyList<SourceFile> Inputs, SourceFile? Review);

public sealed record ScorecardBuildResult(ScorecardResult? Scorecard, IReadOnlyList<Cases.CaseError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
