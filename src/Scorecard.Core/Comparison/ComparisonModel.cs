using Scorecard.Core.Cases;

namespace Scorecard.Core.Comparison;

// 대조 입력 파일 하나. docs/spec/metrics.md 2절.
// Path는 케이스 폴더 기준 상대 경로('/' 구분)다. 절대 경로를 넣지 않는다. Content는 읽은 바이트 그대로다(바꾸지 않는다).
public sealed record SourceFile(string Path, byte[] Content);

// 기준 자료(reference)·결과 자료(candidate) 파일 목록. 순서는 상관없다. 대조기가 Path 서수 순으로 쓴다.
public sealed record CompareSources(IReadOnlyList<SourceFile> Reference, IReadOnlyList<SourceFile> Candidate);

// 입력 읽기 결과. 오류가 하나라도 있으면 Sources는 null이다.
public sealed record CompareSourcesResult(CompareSources? Sources, IReadOnlyList<CaseError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

// 대조 결과. 오류가 하나라도 있으면 Metrics는 null이다(CaseParseResult와 같은 모양).
public sealed record CompareResult(CompareMetrics? Metrics, IReadOnlyList<CaseError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

// metrics.json 하나의 내용(스펙 3.2). Status가 not_verified면 Key·Counts·퍼센트는 null, 목록은 비어 있다.
// Items는 모든 항목이다(기준 순서, 그다음 결과 쪽 추가·중복 항목은 결과 순서, 스펙 12.2). JSON에는 차이가 있는 항목만 쓴다.
// Checklist는 case.yaml checklist가 있을 때만, Scores는 scores가 있을 때만 있다(text-check.md 3.1, T-014).
public sealed record CompareMetrics(
    string Case,
    string Status,
    string? Reason,
    string? Key,
    IReadOnlyList<string> ReferenceFiles,
    IReadOnlyList<string> CandidateFiles,
    CompareCounts? Counts,
    decimal? AccuracyPct,
    decimal? MissingRatePct,
    IReadOnlyList<FieldSummary> Fields,
    IReadOnlyList<CompareItem> Items,
    ChecklistSummary? Checklist = null,
    ScoreSummary? Scores = null);

// 항목 셈(스펙 4.4). Reference = Match + Mismatch + Missing, Candidate = Match + Mismatch + Extra + 중복 행(4.3).
// 중복 행 수는 Items의 duplicate 수다. 셈 키를 따로 두지 않는다. 결과 쪽에 두 번 이상 나온 기준 키는 Mismatch다(13.2).
// Within: 일치 항목 중 허용 오차 안의 차이가 하나 이상 있는 항목 수.
public sealed record CompareCounts(int Reference, int Candidate, int Match, int Mismatch, int Missing, int Extra, int Within);

// compare 필드 하나의 셈. Compared = 기준에 키가 있는 결과 행 수(중복 행 포함, 13.2). Outside·Within·NotNumber는 그 행들의 칸 수다.
public sealed record FieldSummary(
    string Field, string Kind, decimal? Tolerance, decimal? TolerancePct,
    int Compared, int Outside, int Within, int NotNumber);

// 항목 하나. Status: match, mismatch, missing, extra, duplicate. 없는 쪽은 null.
// duplicate는 결과 쪽 같은 키의 둘째 행부터다. Reference는 그 키의 기준 행(없으면 null), Differences는 그 행과 기준 행의 차이다(없으면 비어 있다, 13.2).
// Differences: 차이가 있는 compare 필드(compare 순서). 정규화 뒤 같은 텍스트, 숫자로 같은 값은 넣지 않는다.
// MissingItems: 그 결과 행에서 빠진 체크리스트 항목(checklist.items 순서, text-check.md 4.2). checklist가 없으면 null이다.
public sealed record CompareItem(
    string Key, string Status, CsvRow? Reference, CsvRow? Candidate, IReadOnlyList<FieldDifference> Differences,
    IReadOnlyList<string>? MissingItems = null);

// Result: within, outside, not_number. 값은 정규화한 칸 텍스트다.
public sealed record FieldDifference(string Field, string Result, string Reference, string Candidate);

// CSV 데이터 행 하나. Line은 행이 시작한 물리 줄(1부터). Header·Cells는 정규화한 텍스트다(같은 칸 수).
// compare에 없는 열(예: signal_type)도 담는다. 판정 카드가 쓸 수 있다. JSON에는 쓰지 않는다.
public sealed record CsvRow(string File, int Line, IReadOnlyList<string> Header, IReadOnlyList<string> Cells);
