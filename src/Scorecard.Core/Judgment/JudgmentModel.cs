namespace Scorecard.Core.Judgment;

// 판정이 받는 시간. 건당 분이다(docs/spec/judgment.md 4.10). 적용 전, 처리, 검토 셋. 0 이상이다.
// M2 measure가 만든다. 재지 않았으면 Judge에 null을 넘긴다.
public sealed record JudgmentTimes(decimal BeforeMinutes, decimal ProcessMinutes, decimal ReviewMinutes);

// 판정에 쓴 문턱(4.2). case.yaml 값 그대로이거나 기본값이다. 자릿수를 남긴다.
public sealed record JudgmentThresholds(decimal AccuracyMin, decimal MissingMax, bool TimeMustDrop);

// 확인 하나(4.4). Rule: ACCURACY, MISSING, UNEXPLAINED, TIME, FOCUS. Result는 4.4 표의 결과다. Reason은 확인 문장이다.
public sealed record JudgmentCheck(string Rule, string Result, string Reason);

// 다음 할 일 하나(4.7). Key는 4.7 표의 키, Text는 문구다.
public sealed record JudgmentTodo(string Key, string Text);

// 판정 결과 하나(3.1). judgment.json의 내용과 같다.
// Defaults: 기본값을 쓴 문턱 키(4.2 표 순서). Checks: 확인 다섯(4.4 순서). 미검증이면 비어 있다. Todo: 4.7 표 순서.
public sealed record JudgmentResult(
    string Case,
    string Verdict,
    string Rule,
    string Reason,
    JudgmentThresholds Thresholds,
    IReadOnlyList<string> Defaults,
    IReadOnlyList<JudgmentCheck> Checks,
    IReadOnlyList<JudgmentTodo> Todo);
