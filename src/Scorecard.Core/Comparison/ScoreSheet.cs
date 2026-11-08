using System.Globalization;
using Scorecard.Core.Cases;

namespace Scorecard.Core.Comparison;

// 가림 점수표(docs/spec/text-check.md 4.3)와 가림 확인(4.4)과 점수 오류(4.5).
// 점수표(채점자가 채운다)와 출처 표(가림 키 → 건·출처)를 읽어 출처별로 모은다. 엔진은 사람이 매긴 점수를 모으기만 한다.
// 파일은 읽기만 한다. 비교는 서수, 셈은 정수다. 순서는 파일 경로 서수 → 줄이다. Dictionary는 찾기에만 쓴다.
// 오류가 하나라도 있으면 집계를 내지 않는다. 앞 단계가 깨져 뒤 검사가 헛된 오류를 내면 그 검사는 하지 않는다:
// 출처 표를 다 읽지 못하면 점수표의 가림 키·건을 출처 표와 견주지 않고, 두 표 가운데 하나라도 다 읽지 못하면 점수가 없는 키를 보지 않는다.
internal static class ScoreSheet
{
    // 4.5 파일이 정해지기 전 오류의 Path.
    public const string PathSheet = "scores.sheet";
    public const string PathSources = "scores.sources";

    // 4.3 출처 값. 이 순서가 sources[] 순서다.
    public const string SourceAi = "ai";
    public const string SourceHuman = "human";

    // 4.4 가림 상태.
    public const string BlindKept = "kept";
    public const string BlindRevealed = "revealed";

    // 4.4 가림 규칙. 표 순서가 findings 순서의 첫 열쇠다.
    public const string RuleSourceColumn = "source_column";
    public const string RuleSourceValue = "source_value";
    public const string RuleSourceInBlind = "source_in_blind";
    public const string RuleSourcePattern = "source_pattern";

    // 4.3 열 이름(정규화 뒤 서수).
    private const string ColumnRater = "rater";
    private const string ColumnItem = "item";
    private const string ColumnBlind = "blind";
    private const string ColumnScore = "score";
    private const string ColumnSource = "source";

    private static readonly string[] SheetColumns = [ColumnRater, ColumnItem, ColumnBlind, ColumnScore];
    private static readonly string[] SourceColumns = [ColumnBlind, ColumnItem, ColumnSource];
    private static readonly string[] Sources = [SourceAi, SourceHuman];
    private static readonly string[] Rules = [RuleSourceColumn, RuleSourceValue, RuleSourceInBlind, RuleSourcePattern];

    // 오류 묶음(4.5 순서): 출처 표 → 점수표 → 점수가 없는 키.
    private const int GroupSources = 0;
    private const int GroupSheet = 1;
    private const int GroupUnrated = 2;

    // inputFiles·referenceKeys: 대조가 오류 없이 inputs 파일을 읽었을 때(measured)만 넘긴다. 아니면 null이고 그 두 검사를 하지 않는다(4.5).
    public static (ScoreSummary? Summary, IReadOnlyList<CaseError> Errors) Read(
        string caseDirectory, CaseScores scores, IReadOnlySet<string>? inputFiles, IReadOnlySet<string>? referenceKeys)
    {
        ArgumentNullException.ThrowIfNull(scores);
        var errors = new ErrorList();

        // 파일 찾기(metrics.md 4.1). 출처 표 → 점수표 차례로 겹침을 본다. 겹친 둘째 파일은 읽지 않는다.
        var seen = new HashSet<string>(inputFiles ?? (IEnumerable<string>)[], StringComparer.Ordinal);
        var (sourceFiles, sourcesComplete) = Find(caseDirectory, scores.Sources, PathSources, GroupSources, seen, errors);
        var (sheetFiles, sheetComplete) = Find(caseDirectory, scores.Sheet, PathSheet, GroupSheet, seen, errors);

        var table = ReadSources(sourceFiles, referenceKeys, errors);
        sourcesComplete &= table.Complete;
        if (sourcesComplete && table.Entries.Count == 0)
        {
            if (errors.CountOf(GroupSources) == 0)
            {
                errors.Add(GroupSources, new CaseError(CaseParser.FileName, 0, 0, CompareRules.CodeNoRows, PathSources, "출처 표에 데이터 행이 없다"));
            }

            sourcesComplete = false;
        }

        if (sourcesComplete)
        {
            CheckPairs(table, errors);
        }

        var sheet = ReadSheet(sheetFiles, scores, sourcesComplete ? table : null, errors);
        sheetComplete &= sheet.Complete;

        // 4.3 규칙 3: 출처 표의 가림 키마다 점수가 하나 이상(출처 표 순서).
        if (sourcesComplete && sheetComplete)
        {
            foreach (var entry in table.Entries.Where(e => !sheet.Rated.Contains(e.Blind)))
            {
                errors.Add(GroupUnrated, CsvText.Error(entry.File, entry.BlindCell.Line, entry.BlindCell.Column, CompareRules.CodeBlind, ColumnBlind, "점수가 하나도 없다: " + CompareRules.Show(entry.Blind)));
            }
        }

        if (errors.Count > 0)
        {
            return (null, errors.Sorted());
        }

        return (Summarize(scores, sourceFiles, sheetFiles, table, sheet), []);
    }

    // 4.3 규칙 4: 평균은 소수 둘째 자리. 셋째 자리가 5 이상이면 올린다. 정수 셈이다. 늘 두 자리다(4.00).
    internal static decimal Mean(int sum, int count)
    {
        var hundredths = ((2L * sum * 100) + count) / (2L * count);
        return new decimal((int)hundredths, 0, 0, false, 2);
    }

    // 4.4 ASCII 소문자: A~Z만 a~z로. 문화권과 상관없다.
    private static string AsciiLower(string text) =>
        string.Create(text.Length, text, static (span, t) =>
        {
            for (var i = 0; i < t.Length; i++)
            {
                span[i] = t[i] is >= 'A' and <= 'Z' ? (char)(t[i] + 32) : t[i];
            }
        });

    private static bool IsSourceWord(string text) => AsciiLower(text) is SourceAi or SourceHuman;

    // 4.4 source_in_blind 조각: 이어진 ASCII 영문자 한 조각, 이어진 ASCII 숫자 한 조각. 그 밖의 글자는 나누기만 한다.
    private static IEnumerable<string> Tokens(string key)
    {
        var i = 0;
        while (i < key.Length)
        {
            var kind = Kind(key[i]);
            if (kind == 0)
            {
                i++;
                continue;
            }

            var start = i;
            while (i < key.Length && Kind(key[i]) == kind)
            {
                i++;
            }

            yield return AsciiLower(key[start..i]);
        }

        static int Kind(char ch) => ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z' ? 1 : ch is >= '0' and <= '9' ? 2 : 0;
    }

    private static (List<SourceFile> Files, bool Complete) Find(
        string caseDirectory, string pattern, string fieldPath, int group, HashSet<string> seen, ErrorList errors)
    {
        var found = new List<CaseError>();
        var files = CompareInputs.ReadSide(caseDirectory, pattern, fieldPath, found);
        foreach (var e in found)
        {
            errors.Add(group, e);
        }

        var kept = new List<SourceFile>();
        foreach (var file in files)
        {
            if (seen.Add(file.Path))
            {
                kept.Add(file);
            }
            else
            {
                errors.Add(group, new CaseError(file.Path, 0, 0, CompareRules.CodeOverlap, string.Empty, "같은 파일이 두 번 나온다(패턴이 겹친다)"));
            }
        }

        return (kept, found.Count == 0 && kept.Count == files.Count);
    }

    // 머리 행에서 열을 찾는다. 하나라도 없으면 null(그 파일의 데이터 행은 보지 않는다).
    private static int[]? Columns(SourceFile file, CsvFile csv, string[] header, string[] names, int group, ErrorList errors)
    {
        var index = names.Select(n => CompareEngine.ColumnIndex(file, csv.Header, header, n, e => errors.Add(group, e))).ToArray();
        return index.Any(i => i < 0) ? null : index;
    }

    // 칸 수가 머리 행과 다르면 field-count(metrics.md 4.7 문구 그대로).
    private static bool FieldCount(SourceFile file, CsvRecord record, int columns, int group, ErrorList errors)
    {
        if (record.Cells.Count == columns)
        {
            return true;
        }

        errors.Add(group, CsvText.Error(file, record.Line, 0, CompareRules.CodeFieldCount, string.Empty, string.Create(CultureInfo.InvariantCulture, $"칸 수가 머리 행과 다르다 (머리 행 {columns}칸, 이 행 {record.Cells.Count}칸)")));
        return false;
    }

    // 4.3 규칙 1: 출처 표. 가림 키는 모든 출처 표 파일에서 하나씩. 출처는 ai·human.
    // 대조 measured면 건은 기준 자료의 키여야 한다(규칙 2, blind 경로 item).
    private static SourceTable ReadSources(List<SourceFile> files, IReadOnlySet<string>? referenceKeys, ErrorList errors)
    {
        var table = new SourceTable();
        foreach (var file in files)
        {
            var csv = CsvText.Read(file, e => errors.Add(GroupSources, e));
            if (csv is null)
            {
                table.Complete = false;
                continue;
            }

            var header = csv.Header.Cells.Select(c => CompareRules.Normalize(c.Text)).ToArray();
            if (Columns(file, csv, header, SourceColumns, GroupSources, errors) is not [var ib, var ii, var isrc])
            {
                table.Complete = false;
                continue;
            }

            foreach (var record in csv.Rows)
            {
                if (!FieldCount(file, record, header.Length, GroupSources, errors))
                {
                    table.Complete = false;
                    continue;
                }

                var cells = record.Cells.Select(c => CompareRules.Normalize(c.Text)).ToArray();
                var (blind, item, source) = (cells[ib], cells[ii], cells[isrc]);
                var empty = false;
                foreach (var (index, name) in new[] { (ib, ColumnBlind), (ii, ColumnItem) })
                {
                    if (cells[index].Length == 0)
                    {
                        var cell = record.Cells[index];
                        errors.Add(GroupSources, CsvText.Error(file, cell.Line, cell.Column, CompareRules.CodeEmptyKey, name, "키가 비어 있다"));
                        empty = true;
                    }
                }

                if (empty)
                {
                    table.Complete = false;
                    continue;
                }

                var valid = Sources.Contains(source, StringComparer.Ordinal);
                if (!valid)
                {
                    var cell = record.Cells[isrc];
                    errors.Add(GroupSources, CsvText.Error(file, cell.Line, cell.Column, CompareRules.CodeSource, ColumnSource, "출처는 ai 또는 human이다: " + CompareRules.Show(source)));
                    table.Tainted.Add(item);
                }

                if (table.ByBlind.TryGetValue(blind, out var first))
                {
                    var cell = record.Cells[ib];
                    errors.Add(GroupSources, CsvText.Error(file, cell.Line, cell.Column, CompareRules.CodeDuplicateKey, ColumnBlind, $"가림 키가 중복된다: {CompareRules.Show(blind)} (처음: {first.File.Path}:{first.Line.ToString(CultureInfo.InvariantCulture)})"));
                    table.Tainted.Add(item);
                    continue;
                }

                var itemCell = record.Cells[ii];
                if (referenceKeys is not null && !referenceKeys.Contains(item))
                {
                    errors.Add(GroupSources, CsvText.Error(file, itemCell.Line, itemCell.Column, CompareRules.CodeBlind, ColumnItem, "기준 자료에 없는 건이다: " + CompareRules.Show(item)));
                }

                var entry = new SourceEntry(blind, item, valid ? source : null, file, record.Line, record.Cells[ib], itemCell);
                table.ByBlind.Add(blind, entry);
                table.Entries.Add(entry);
            }
        }

        return table;
    }

    // 4.3 규칙 1: 건마다 ai 하나, human 하나. 출처가 틀리거나 가림 키가 겹친 건은 그 오류가 먼저라 보지 않는다.
    private static void CheckPairs(SourceTable table, ErrorList errors)
    {
        foreach (var group in table.Entries.GroupBy(e => e.Item, StringComparer.Ordinal))
        {
            if (table.Tainted.Contains(group.Key))
            {
                continue;
            }

            var ai = group.Count(e => e.Source == SourceAi);
            var human = group.Count(e => e.Source == SourceHuman);
            if (ai != 1 || human != 1)
            {
                var first = group.First();
                errors.Add(GroupSources, CsvText.Error(first.File, first.ItemCell.Line, first.ItemCell.Column, CompareRules.CodePair, ColumnItem, string.Create(CultureInfo.InvariantCulture, $"건마다 ai 하나와 human 하나가 있어야 한다: {CompareRules.Show(group.Key)} (ai {ai}, human {human})")));
            }
        }
    }

    // 4.3 규칙 2: 점수표. 점수는 정수 글이고 min~max 안. 가림 키·건은 출처 표와 같다(sources가 null이면 견주지 않는다).
    // 같은 채점자·같은 가림 키는 한 번. 점수가 틀린 행도 짝은 등록한다(뒤의 같은 짝은 중복이다).
    private static SheetTable ReadSheet(List<SourceFile> files, CaseScores scores, SourceTable? sources, ErrorList errors)
    {
        var sheet = new SheetTable();
        var pairs = new Dictionary<(string Rater, string Blind), (string File, int Line)>();
        foreach (var file in files)
        {
            var csv = CsvText.Read(file, e => errors.Add(GroupSheet, e));
            if (csv is null)
            {
                sheet.Complete = false;
                continue;
            }

            var header = csv.Header.Cells.Select(c => CompareRules.Normalize(c.Text)).ToArray();
            if (Columns(file, csv, header, SheetColumns, GroupSheet, errors) is not [var ir, var ii, var ib, var isc])
            {
                sheet.Complete = false;
                continue;
            }

            sheet.Headers.Add((file.Path, csv.Header.Line, header));
            foreach (var record in csv.Rows)
            {
                if (!FieldCount(file, record, header.Length, GroupSheet, errors))
                {
                    sheet.Complete = false;
                    continue;
                }

                var cells = record.Cells.Select(c => CompareRules.Normalize(c.Text)).ToArray();
                var empty = false;
                foreach (var (index, name) in new[] { (ir, ColumnRater), (ii, ColumnItem), (ib, ColumnBlind) })
                {
                    if (cells[index].Length == 0)
                    {
                        var cell = record.Cells[index];
                        errors.Add(GroupSheet, CsvText.Error(file, cell.Line, cell.Column, CompareRules.CodeEmptyKey, name, "키가 비어 있다"));
                        empty = true;
                    }
                }

                if (empty)
                {
                    sheet.Complete = false;
                    continue;
                }

                var (rater, item, blind) = (cells[ir], cells[ii], cells[ib]);
                sheet.Rated.Add(blind);
                var ok = true;
                if (!TryScore(cells[isc], scores.Min, scores.Max, out var score))
                {
                    var cell = record.Cells[isc];
                    errors.Add(GroupSheet, CsvText.Error(file, cell.Line, cell.Column, CompareRules.CodeScore, ColumnScore, string.Create(CultureInfo.InvariantCulture, $"점수는 {scores.Min}~{scores.Max}의 정수다: {CompareRules.Show(cells[isc])}")));
                    ok = false;
                }

                SourceEntry? entry = null;
                if (sources is not null)
                {
                    if (!sources.ByBlind.TryGetValue(blind, out entry))
                    {
                        var cell = record.Cells[ib];
                        errors.Add(GroupSheet, CsvText.Error(file, cell.Line, cell.Column, CompareRules.CodeBlind, ColumnBlind, "출처 표에 없는 가림 키다: " + CompareRules.Show(blind)));
                        ok = false;
                    }
                    else if (!string.Equals(entry.Item, item, StringComparison.Ordinal))
                    {
                        var cell = record.Cells[ii];
                        errors.Add(GroupSheet, CsvText.Error(file, cell.Line, cell.Column, CompareRules.CodeBlind, ColumnItem, $"출처 표의 건과 다르다: {CompareRules.Show(item)} (출처 표: {CompareRules.Show(entry.Item)})"));
                        ok = false;
                    }
                }

                if (pairs.TryGetValue((rater, blind), out var first))
                {
                    var cell = record.Cells[ib];
                    errors.Add(GroupSheet, CsvText.Error(file, cell.Line, cell.Column, CompareRules.CodeDuplicateKey, ColumnBlind, $"점수가 중복된다: {CompareRules.Show(rater)} {CompareRules.Show(blind)} (처음: {first.File}:{first.Line.ToString(CultureInfo.InvariantCulture)})"));
                    continue;
                }

                pairs.Add((rater, blind), (file.Path, record.Line));
                if (ok && entry?.Source is { } source)
                {
                    sheet.Ratings.Add(new Rating(rater, item, blind, source, score, file.Path, record.Line, header, cells, isc));
                }
            }
        }

        return sheet;
    }

    // 점수 칸: 정수 글 0 또는 [1-9][0-9]*이고 min 이상 max 이하(4.3 규칙 2). max는 100 이하라 네 자리 이상은 범위 밖이다.
    private static bool TryScore(string text, int min, int max, out int score)
    {
        score = 0;
        if (text.Length is 0 or > 3 || text.Any(ch => ch is < '0' or > '9') || (text.Length > 1 && text[0] == '0'))
        {
            return false;
        }

        score = int.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
        return score >= min && score <= max;
    }

    // 4.3 규칙 4~8. 오류가 없을 때만 부른다(모든 행이 맞다).
    private static ScoreSummary Summarize(CaseScores scores, List<SourceFile> sourceFiles, List<SourceFile> sheetFiles, SourceTable table, SheetTable sheet)
    {
        var ratings = sheet.Ratings;
        var sources = Sources.Select(s =>
        {
            var values = ratings.Where(r => r.Source == s).Select(r => r.Score).ToArray();
            var distribution = Enumerable.Range(scores.Min, scores.Max - scores.Min + 1).Select(v => new ScoreCount(v, values.Count(x => x == v))).ToArray();
            return new ScoreSource(s, table.Entries.Count(e => e.Source == s), values.Length, values.Sum(), values.Length == 0 ? 0.00m : Mean(values.Sum(), values.Length), distribution);
        }).ToArray();

        // 규칙 5: 건마다 AI 점수 합 × 사람 점수 수와 사람 점수 합 × AI 점수 수를 견준다. 나눗셈이 없다.
        int better = 0, same = 0, worse = 0;
        foreach (var item in table.Entries.Select(e => e.Item).Distinct(StringComparer.Ordinal))
        {
            var ai = ratings.Where(r => r.Item == item && r.Source == SourceAi).Select(r => (long)r.Score).ToArray();
            var human = ratings.Where(r => r.Item == item && r.Source == SourceHuman).Select(r => (long)r.Score).ToArray();
            var order = (ai.Sum() * human.Length).CompareTo(human.Sum() * ai.Length);
            if (order > 0)
            {
                better++;
            }
            else if (order == 0)
            {
                same++;
            }
            else
            {
                worse++;
            }
        }

        var raters = ratings.Select(r => r.Rater).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var byRater = raters.Select(rater =>
        {
            var mine = ratings.Where(r => r.Rater == rater).ToArray();
            var ai = mine.Where(r => r.Source == SourceAi).Select(r => r.Score).ToArray();
            var human = mine.Where(r => r.Source == SourceHuman).Select(r => r.Score).ToArray();
            return new RaterSummary(rater, ai.Length, ai.Length == 0 ? null : Mean(ai.Sum(), ai.Length), human.Length, human.Length == 0 ? null : Mean(human.Sum(), human.Length));
        }).ToArray();

        var findings = Findings(table, sheet);
        var blind = new ScoreBlind(findings.Count == 0 ? BlindKept : BlindRevealed, findings);
        var items = table.Entries.Select(e => e.Item).Distinct(StringComparer.Ordinal).Count();
        return new ScoreSummary(
            sheetFiles.Select(f => f.Path).ToArray(), sourceFiles.Select(f => f.Path).ToArray(), scores.Min, scores.Max,
            items, raters, ratings.Count, blind, sources, new ScorePaired(better, same, worse), byRater);
    }

    // 4.4 가림 확인. 순서: 규칙(표 순서) → 파일 경로 서수 → 줄 → 찾은 순서(칸 순서). OrderBy는 안정 정렬이다.
    private static List<BlindFinding> Findings(SourceTable table, SheetTable sheet)
    {
        var found = new List<(int Rule, string File, int Line, string Value)>();
        foreach (var (path, line, header) in sheet.Headers)
        {
            found.AddRange(header.Where(name => AsciiLower(name) == ColumnSource).Select(name => (0, path, line, name)));
        }

        foreach (var rating in sheet.Ratings)
        {
            found.AddRange(rating.Cells.Where((cell, k) => k != rating.ScoreIndex && IsSourceWord(cell)).Select(cell => (1, rating.File, rating.Line, cell)));
        }

        foreach (var entry in table.Entries.Where(e => Tokens(e.Blind).Any(t => t is SourceAi or SourceHuman)))
        {
            found.Add((2, entry.File.Path, entry.Line, entry.Blind));
        }

        found.AddRange(PatternFindings(table, sheet).Select(h => (3, h.File, h.Line, h.Value)));
        return found
            .OrderBy(f => f.Rule).ThenBy(f => f.File, StringComparer.Ordinal).ThenBy(f => f.Line)
            .Select(f => new BlindFinding(Rules[f.Rule], f.File, f.Line, f.Value))
            .ToList();
    }

    // 4.4 표시 몰림(source_pattern). 건마다 두 결과의 표시를 견주어 한쪽에만 있는 표시를 센다.
    // 키는 채점자와 상관없이 그 건의 두 키를 견준다. 열·파일은 두 출처를 모두 매긴 채점자의 행만 보고,
    // 그 건의 AI 결과 행 표시 합과 사람 결과 행 표시 합을 견준다(두 결과 모두 그런 행이 있을 때만).
    // 걸린 표시마다 그 표시가 있는 첫 칸 하나. 같은 칸은 한 번. 순서는 파일 경로 서수 → 줄 → 칸 자리(키 0, 파일 -1).
    private static List<(string File, int Line, int Column, string Value)> PatternFindings(SourceTable table, SheetTable sheet)
    {
        var rowOf = sheet.Ratings.ToDictionary(r => (r.Rater, r.Blind));
        var mixed = sheet.Ratings.GroupBy(r => r.Rater, StringComparer.Ordinal)
            .Where(g => Sources.All(s => g.Any(r => r.Source == s)))
            .Select(g => g.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var keyMarks = table.Entries.ToDictionary(e => e.Blind, e => BlindMarks.KeyMarksOf(e.Blind).Select(m => Mark.Key(m.Kind, m.Value)).ToHashSet(), StringComparer.Ordinal);
        var pairs = table.Entries.GroupBy(e => e.Item, StringComparer.Ordinal)
            .Select(g => (Ai: g.Single(e => e.Source == SourceAi).Blind, Human: g.Single(e => e.Source == SourceHuman).Blind))
            .ToArray();
        var lean = new Dictionary<Mark, (int Ai, int Human)>();
        var lower = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (ka, kh) in pairs)
        {
            var aSide = keyMarks[ka].Except(keyMarks[kh]).ToHashSet();
            var hSide = keyMarks[kh].Except(keyMarks[ka]).ToHashSet();
            if (BlindMarks.Lower(ka, kh) is { } low)
            {
                lower.Add(low);
                (low == ka ? aSide : hSide).Add(Mark.Low);
            }

            var ua = RowMarks(mixed, ka, rowOf);
            var uh = RowMarks(mixed, kh, rowOf);
            if (ua.Count > 0 && uh.Count > 0)
            {
                aSide.UnionWith(ua.Except(uh));
                hSide.UnionWith(uh.Except(ua));
            }

            foreach (var mark in aSide.Except(hSide))
            {
                lean[mark] = (lean.GetValueOrDefault(mark).Ai + 1, lean.GetValueOrDefault(mark).Human);
            }

            foreach (var mark in hSide.Except(aSide))
            {
                lean[mark] = (lean.GetValueOrDefault(mark).Ai, lean.GetValueOrDefault(mark).Human + 1);
            }
        }

        var headerLine = sheet.Headers.ToDictionary(h => h.File, h => h.Line, StringComparer.Ordinal);
        var hits = new Dictionary<(string File, int Line, int Column), (string File, int Line, int Column, string Value)>();
        foreach (var (mark, (ai, human)) in lean)
        {
            if (!BlindMarks.Leans(ai, human, pairs.Length))
            {
                continue;
            }

            (string File, int Line, int Column, string Value) hit;
            if (mark.Place == Mark.PlaceKey)
            {
                // 출처 표(경로 서수 → 줄)에서 그 표시가 있는 첫 키 행. 작은 쪽은 건의 작은 키인 첫 행. 값은 키.
                var entry = table.Entries.First(e => mark == Mark.Low ? lower.Contains(e.Blind) : keyMarks[e.Blind].Contains(mark));
                hit = (entry.File.Path, entry.Line, 0, entry.Blind);
            }
            else if (mark.Place == Mark.PlaceFile)
            {
                hit = (mark.Value, headerLine[mark.Value], -1, mark.Value);
            }
            else
            {
                // 점수표(경로 서수 → 줄 → 칸)에서 그 열 칸에 그 표시가 있는 첫 행. 값은 칸 글.
                hit = sheet.Ratings
                    .SelectMany(r => r.Header.Select((name, k) => (r, name, k)))
                    .Where(x => x.name == mark.Name && BlindMarks.MarksOf(x.r.Cells[x.k]).Contains((mark.Kind, mark.Value)))
                    .Select(x => (x.r.File, x.r.Line, x.k, x.r.Cells[x.k]))
                    .First();
            }

            hits.TryAdd((hit.File, hit.Line, hit.Column), hit);
        }

        return hits.Values
            .OrderBy(h => h.File, StringComparer.Ordinal).ThenBy(h => h.Line).ThenBy(h => h.Column)
            .ToList();
    }

    // 한 결과(가림 키)를 두 출처를 매긴 채점자들이 매긴 행의 표시 합: 파일, 고정 열 밖 칸의 표시(열 이름과 짝).
    private static HashSet<Mark> RowMarks(string[] mixed, string blind, Dictionary<(string Rater, string Blind), Rating> rowOf)
    {
        var marks = new HashSet<Mark>();
        foreach (var rater in mixed)
        {
            if (!rowOf.TryGetValue((rater, blind), out var row))
            {
                continue;
            }

            marks.Add(Mark.File(row.File));
            for (var k = 0; k < row.Header.Length; k++)
            {
                if (!SheetColumns.Contains(row.Header[k], StringComparer.Ordinal))
                {
                    marks.UnionWith(BlindMarks.MarksOf(row.Cells[k]).Select(m => Mark.Column(row.Header[k], m.Kind, m.Value)));
                }
            }
        }

        return marks;
    }

    // 표시 하나: 자리(키·파일·열)와 열 이름, 종류, 값의 짝. 값은 서수로 비교한다.
    private readonly record struct Mark(string Place, string Name, string Kind, string Value)
    {
        public const string PlaceKey = "key";
        public const string PlaceFile = "file";
        public const string PlaceColumn = "column";

        public static Mark Low => new(PlaceKey, string.Empty, "low", string.Empty);

        public static Mark Key(string kind, string value) => new(PlaceKey, string.Empty, kind, value);

        public static Mark File(string path) => new(PlaceFile, string.Empty, string.Empty, path);

        public static Mark Column(string name, string kind, string value) => new(PlaceColumn, name, kind, value);
    }

    // 출처 표 한 행. Source는 ai·human이 아니면 null(오류가 이미 있다).
    private sealed record SourceEntry(string Blind, string Item, string? Source, SourceFile File, int Line, CsvCell BlindCell, CsvCell ItemCell);

    private sealed class SourceTable
    {
        public Dictionary<string, SourceEntry> ByBlind { get; } = new(StringComparer.Ordinal);

        public List<SourceEntry> Entries { get; } = [];

        // 출처가 틀리거나 가림 키가 겹친 행의 건. pair를 보지 않는다.
        public HashSet<string> Tainted { get; } = new(StringComparer.Ordinal);

        // 모든 행을 읽었는가(파일·머리 행·행 오류로 건너뛴 것이 없다).
        public bool Complete { get; set; } = true;
    }

    // 점수 하나. 오류가 없을 때 집계와 가림 확인에 쓴다.
    private sealed record Rating(string Rater, string Item, string Blind, string Source, int Score, string File, int Line, string[] Header, string[] Cells, int ScoreIndex);

    private sealed class SheetTable
    {
        public List<(string File, int Line, string[] Header)> Headers { get; } = [];

        public List<Rating> Ratings { get; } = [];

        // 점수가 있는 가림 키(점수 칸이 틀려도 센다).
        public HashSet<string> Rated { get; } = new(StringComparer.Ordinal);

        public bool Complete { get; set; } = true;
    }

    // 오류 순서(4.5): 출처 표 → 점수표 → 점수가 없는 키. 묶음 안에서는 파일이 정해지기 전 오류(case.yaml) → 파일 경로 서수 → (줄, 열) → 찾은 순서.
    private sealed class ErrorList
    {
        private readonly List<(int Group, int Seq, CaseError Error)> errors = [];

        public int Count => errors.Count;

        public int CountOf(int group) => errors.Count(e => e.Group == group);

        public void Add(int group, CaseError error) => errors.Add((group, errors.Count, error));

        public CaseError[] Sorted() => errors
            .OrderBy(e => e.Group)
            .ThenBy(e => e.Error.Source == CaseParser.FileName ? 0 : 1)
            .ThenBy(e => e.Error.Source, StringComparer.Ordinal)
            .ThenBy(e => e.Error.Line).ThenBy(e => e.Error.Column).ThenBy(e => e.Seq)
            .Select(e => e.Error)
            .ToArray();
    }
}
