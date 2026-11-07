namespace Scorecard.Core.Judgment;

// T-011 스파이크 후보(머지 대상 아님). docs/spec/judgment.md 3.1·4.11.

// 건당 시간(분). 적용 전(사람이 하던 시간), 처리(투입부터 결과 완성까지), 검토(사람이 결과를 확인·수정한 시간).
// 값은 M2 measure가 만든다. 이 카드에서는 테스트만 넣는다. 없으면 Judge에 null을 준다(시간을 재지 않음).
public sealed record JudgmentTimes(decimal BeforeMinutes, decimal ProcessMinutes, decimal ReviewMinutes);

// 판정에 쓴 문턱. case.yaml thresholds 값, 없으면 기본값(스펙 4.2). 어느 키가 기본값인지는 JudgmentResult.Defaults.
public sealed record JudgmentThresholds(decimal AccuracyMin, decimal MissingMax, bool TimeMustDrop);

// 확인 하나. Rule: ACCURACY, MISSING, UNEXPLAINED, TIME, FOCUS. Result는 스펙 4.4 표의 값. Reason은 확인 문장.
public sealed record JudgmentCheck(string Rule, string Result, string Reason);

// 다음 할 일 하나. Key는 스펙 4.7 표의 키, Text는 문구.
public sealed record JudgmentTodo(string Key, string Text);

// 판정 결과(judgment.json 하나의 내용). Verdict: pass, conditional, hold, not_verified.
// Rule: 판정을 정한 근거 키(스펙 4.3 표의 순서에서 처음 걸린 것). 미검증이면 Checks가 비어 있다.
public sealed record JudgmentResult(
    string Case,
    string Verdict,
    string Rule,
    string Reason,
    JudgmentThresholds Thresholds,
    IReadOnlyList<string> Defaults,
    IReadOnlyList<JudgmentCheck> Checks,
    IReadOnlyList<JudgmentTodo> Todo);
