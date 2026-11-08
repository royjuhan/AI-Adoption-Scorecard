namespace Scorecard.Core.Comparison;

// 체크리스트 대조 셈(docs/spec/text-check.md 3.1 compare.checklist). Text는 결과 자료의 문장 열 이름이다.
// Entries는 checklist.items 순서다.
public sealed record ChecklistSummary(string Text, IReadOnlyList<ChecklistEntry> Entries);

// 항목 하나의 셈. Column 항목이면 Phrases는 비어 있고, any 항목이면 Column은 null이다.
// Required = Found + Absent. 기준에 키가 있는 결과 행마다 센다(4.2 규칙 8). 기준 칸이 빈 column 항목은 세지 않는다.
public sealed record ChecklistEntry(string Item, string? Column, IReadOnlyList<string> Phrases, int Required, int Found, int Absent);

// 가림 점수표 집계(text-check.md 3.1 scores, 4.3·4.4). 파일은 케이스 폴더 기준 상대 경로이고 경로 서수 순이다.
// Items: 출처 표의 건 수. Raters: 서수 순. Ratings: 점수 수. Sources: ai → human 순서.
public sealed record ScoreSummary(
    IReadOnlyList<string> SheetFiles,
    IReadOnlyList<string> SourcesFiles,
    int Min,
    int Max,
    int Items,
    IReadOnlyList<string> Raters,
    int Ratings,
    ScoreBlind Blind,
    IReadOnlyList<ScoreSource> Sources,
    ScorePaired Paired,
    IReadOnlyList<RaterSummary> ByRater);

// 가림 확인(4.4). Status: kept(발견 없음) 또는 revealed. Findings는 4.4 순서다.
public sealed record ScoreBlind(string Status, IReadOnlyList<BlindFinding> Findings);

// 출처가 드러난 곳 하나. Rule은 4.4 가림 규칙, Value는 그 칸의 글(열 이름, 칸 글, 가림 키 또는 파일 경로)이다.
public sealed record BlindFinding(string Rule, string File, int Line, string Value);

// 출처 하나의 집계. Mean은 소수 둘째 자리 decimal이다(4.3 규칙 4). Distribution은 min부터 max까지 점수마다 하나.
public sealed record ScoreSource(string Source, int Results, int Ratings, int Sum, decimal Mean, IReadOnlyList<ScoreCount> Distribution);

public sealed record ScoreCount(int Score, int Count);

// 건마다 AI 평균과 사람 평균의 비교(4.3 규칙 5). 나눗셈 없이 합·수 곱으로 견준다.
public sealed record ScorePaired(int Better, int Same, int Worse);

// 채점자 하나의 출처별 점수 수와 평균. 매긴 것이 없으면 평균은 null이다(4.3 규칙 6).
public sealed record RaterSummary(string Rater, int AiRatings, decimal? AiMean, int HumanRatings, decimal? HumanMean);
