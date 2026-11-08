using System.Globalization;
using Scorecard.Core.Cases;

namespace Scorecard.Core.Comparison;

// 기준 자료와 결과 자료를 키로 맞춰 일치·값 불일치·누락·추가·중복을 낸다. docs/spec/metrics.md 4.3~4.8, 12.2·12.3(T-012), 13.2(T-013).
// 체크리스트(text-check.md 4.2)는 대조의 한 종류다. 짝지은 결과 행마다 항목 문구가 문장에 있는지 본다. 빠진 항목이 있으면 값 불일치다.
// 가림 점수표(text-check.md 4.3·4.4)는 CompareDirectory가 점수 파일을 읽어 Scores에 붙인다(ScoreSheet).
// Compare는 순수 함수다. 파일·시각·환경 변수·난수를 쓰지 않는다. 비교는 서수, 숫자는 decimal·InvariantCulture.
// 순서는 파일 경로 서수 → 줄 순서다. Dictionary는 찾기에만 쓰고 열거 순서를 출력에 쓰지 않는다.
public static class CompareEngine
{
    public const string StatusMeasured = "measured";
    public const string StatusNotVerified = "not_verified";
    public const string ReasonNoInputs = "no_inputs";
    public const string ReasonNoCompareFields = "no_compare_fields";

    public const string ItemMatch = "match";
    public const string ItemMismatch = "mismatch";
    public const string ItemMissing = "missing";
    public const string ItemExtra = "extra";

    // 결과 쪽 같은 키의 둘째 행부터(스펙 4.3, T-012). 그 행과 기준 행의 차이를 적는다(13.2, T-013).
    public const string ItemDuplicate = "duplicate";

    public const string KindNumber = "number";
    public const string KindText = "text";

    public const string ResultWithin = "within";
    public const string ResultOutside = "outside";
    public const string ResultNotNumber = "not_number";

    // 케이스 폴더의 inputs 파일을 읽어 대조한다. inputs가 없거나 compare·checklist가 모두 없으면 파일을 읽지 않고 미검증이다.
    // scores가 있으면 점수 파일도 읽는다(text-check.md 4.3). 오류 순서는 대조 쪽 → 점수 쪽(4.5)이다.
    public static CompareResult CompareDirectory(string caseDirectory, CaseDefinition definition)
    {
        ArgumentException.ThrowIfNullOrEmpty(caseDirectory);
        ArgumentNullException.ThrowIfNull(definition);

        var compared = CompareInputsOf(caseDirectory, definition);
        if (definition.Scores is not { } scores)
        {
            return compared;
        }

        // 대조가 오류 없이 inputs 파일을 읽었을 때만(measured) 기준 키·inputs 파일과 견준다(4.3 규칙 2, 4.5 overlap).
        var measured = compared.Metrics is { Status: StatusMeasured } m ? m : null;
        var referenceKeys = measured?.Items
            .Where(i => i.Status is ItemMatch or ItemMismatch or ItemMissing)
            .Select(i => i.Key)
            .ToHashSet(StringComparer.Ordinal);
        var inputFiles = measured?.ReferenceFiles.Concat(measured.CandidateFiles).ToHashSet(StringComparer.Ordinal);
        var (summary, scoreErrors) = ScoreSheet.Read(caseDirectory, scores, inputFiles, referenceKeys);
        if (compared.Errors.Count > 0 || scoreErrors.Count > 0)
        {
            return new CompareResult(null, [.. compared.Errors, .. scoreErrors]);
        }

        return new CompareResult(compared.Metrics! with { Scores = summary }, []);
    }

    private static CompareResult CompareInputsOf(string caseDirectory, CaseDefinition definition)
    {
        if (NotVerifiedReason(definition) is { } reason)
        {
            return new CompareResult(NotVerified(definition, reason), []);
        }

        var read = CompareInputs.Read(caseDirectory, definition.Inputs!);
        return read.Sources is null ? new CompareResult(null, read.Errors) : Compare(definition, read.Sources);
    }

    // 바이트로 대조한다. sources는 바꾸지 않는다.
    public static CompareResult Compare(CaseDefinition definition, CompareSources sources)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(sources);

        if (NotVerifiedReason(definition) is { } reason)
        {
            return new CompareResult(NotVerified(definition, reason), []);
        }

        var inputs = definition.Inputs!;
        var errors = new ErrorList();
        var referenceFiles = sources.Reference.OrderBy(f => f.Path, StringComparer.Ordinal).ToArray();
        var candidateFiles = sources.Candidate.OrderBy(f => f.Path, StringComparer.Ordinal).ToArray();
        CheckOverlap(referenceFiles, candidateFiles, errors);

        // 한쪽에 파일이 하나도 없으면 file 오류(CompareInputs.Read와 같은 줄: Source = case.yaml, Path = inputs.*).
        foreach (var (side, files, fieldPath) in new[] { (0, referenceFiles, CompareRules.PathReference), (1, candidateFiles, CompareRules.PathCandidate) })
        {
            if (files.Length == 0)
            {
                errors.Add(side, -1, new CaseError(CaseParser.FileName, 0, 0, CompareRules.CodeFile, fieldPath, "일치하는 파일이 없다"));
            }
        }

        // 기준 쪽: 숫자 칸 확인, group 열 확인(12.3), 체크리스트 column 열 확인, 키 중복은 오류. 결과 쪽: 문장 열 확인, 키 중복은 항목 duplicate(12.2).
        var checklist = definition.Checklist;
        var referenceColumns = checklist?.Items.Where(i => i.Column is not null).Select(i => i.Column!).Distinct(StringComparer.Ordinal).ToArray() ?? [];
        string[] candidateColumns = checklist is null ? [] : [checklist.Text];
        var noCheck = definition.Compare.Count == 0 && checklist is not null && checklist.Items.All(i => i.Column is not null);
        var reference = ReadSide(0, referenceFiles, inputs.Key, definition.Compare, inputs.Group, referenceColumns, isReference: true, noCheck, errors);
        var candidate = ReadSide(1, candidateFiles, inputs.Key, definition.Compare, null, candidateColumns, isReference: false, noCheck: false, errors);
        if (errors.Count == 0 && reference.Count == 0)
        {
            errors.Add(0, -1, new CaseError(CaseParser.FileName, 0, 0, CompareRules.CodeNoRows, CompareRules.PathReference, "기준 자료에 데이터 행이 없다"));
        }

        if (errors.Count > 0)
        {
            return new CompareResult(null, errors.Sorted());
        }

        return new CompareResult(Measure(definition, referenceFiles, candidateFiles, reference, candidate), []);
    }

    // checklist가 있으면 compare가 비어도 대조한다(text-check.md 4.2).
    private static string? NotVerifiedReason(CaseDefinition definition) =>
        definition.Inputs is null ? ReasonNoInputs
        : definition.Compare.Count == 0 && definition.Checklist is null ? ReasonNoCompareFields
        : null;

    private static CompareMetrics NotVerified(CaseDefinition definition, string reason) =>
        new(definition.Name, StatusNotVerified, reason, null, [], [], null, null, null, [], []);

    // 같은 파일이 두 번 나오거나 기준·결과 양쪽에 있으면 overlap.
    private static void CheckOverlap(SourceFile[] reference, SourceFile[] candidate, ErrorList errors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (file, side, index) in reference.Select((f, i) => (f, 0, i)).Concat(candidate.Select((f, i) => (f, 1, i))))
        {
            if (!seen.Add(file.Path))
            {
                errors.Add(side, index, new CaseError(file.Path, 0, 0, CompareRules.CodeOverlap, string.Empty, "같은 파일이 두 번 나온다(기준·결과 패턴이 겹친다)"));
            }
        }
    }

    // 한쪽 파일을 모두 읽는다. 키 중복은 같은 쪽 모든 파일에서 본다.
    // 기준 자료(isReference): 숫자 필드가 숫자인지 보고, group 열이 머리 행에 꼭 한 번 있는지 본다(12.3). 키 중복은 duplicate-key 오류다.
    // 결과 자료: 키 중복은 오류가 아니다. 모든 행을 돌려준다(첫 행인지는 Measure가 본다, 12.2).
    // 열을 찾는 순서는 키 열 → compare 필드 → group 열 → 체크리스트 열(extra, text-check.md 4.2 규칙 1)이다(4.7 "같으면 찾은 순서").
    // extra의 열 이름이 이미 찾은 열과 같으면 다시 찾지 않는다(같은 열은 한 번).
    // noCheck: compare 필드가 없고 체크리스트가 column 항목뿐이면, column 칸이 모두 빈 기준 행은 no-check 오류다(4.2 규칙 10).
    private static List<Row> ReadSide(
        int side, SourceFile[] files, string key, IReadOnlyList<CompareField> compare, string? group, IReadOnlyList<string> extra,
        bool isReference, bool noCheck, ErrorList errors)
    {
        var rows = new List<Row>();
        var firstByKey = new Dictionary<string, Row>(StringComparer.Ordinal);
        for (var f = 0; f < files.Length; f++)
        {
            var file = files[f];
            var fileIndex = f;
            var csv = CsvText.Read(file, e => errors.Add(side, fileIndex, e));
            if (csv is null)
            {
                continue;
            }

            var header = csv.Header.Cells.Select(c => CompareRules.Normalize(c.Text)).ToArray();
            void Add(CaseError e) => errors.Add(side, fileIndex, e);
            var found = new Dictionary<string, int>(StringComparer.Ordinal);
            int Find(string name)
            {
                if (!found.TryGetValue(name, out var index))
                {
                    index = ColumnIndex(file, csv.Header, header, name, Add);
                    found.Add(name, index);
                }

                return index;
            }

            var keyIndex = Find(key);
            var fieldIndex = compare.Select(c => Find(c.Field)).ToArray();
            var groupMissing = group is not null && Find(group) < 0;
            var extraIndex = extra.Select(Find).ToArray();
            if (keyIndex < 0 || fieldIndex.Any(i => i < 0) || groupMissing || extraIndex.Any(i => i < 0))
            {
                continue;
            }

            var columns = extra.Select((name, i) => (name, i)).ToDictionary(x => x.name, x => extraIndex[x.i], StringComparer.Ordinal);

            foreach (var record in csv.Rows)
            {
                if (record.Cells.Count != header.Length)
                {
                    errors.Add(side, f, CsvText.Error(file, record.Line, 0, CompareRules.CodeFieldCount, string.Empty, string.Create(CultureInfo.InvariantCulture, $"칸 수가 머리 행과 다르다 (머리 행 {header.Length}칸, 이 행 {record.Cells.Count}칸)")));
                    continue;
                }

                var cells = record.Cells.Select(c => CompareRules.Normalize(c.Text)).ToArray();
                var keyCell = record.Cells[keyIndex];
                if (cells[keyIndex].Length == 0)
                {
                    errors.Add(side, f, CsvText.Error(file, keyCell.Line, keyCell.Column, CompareRules.CodeEmptyKey, key, "키가 비어 있다"));
                    continue;
                }

                if (isReference)
                {
                    for (var c = 0; c < compare.Count; c++)
                    {
                        var cell = record.Cells[fieldIndex[c]];
                        if (IsNumberField(compare[c]) && !CompareRules.TryNumber(cells[fieldIndex[c]], out _))
                        {
                            errors.Add(side, f, CsvText.Error(file, cell.Line, cell.Column, CompareRules.CodeNumber, compare[c].Field, "숫자가 아니다: " + CompareRules.Show(cells[fieldIndex[c]])));
                        }
                    }
                }

                if (noCheck && extraIndex.All(i => cells[i].Length == 0))
                {
                    errors.Add(side, f, CsvText.Error(file, record.Line, 0, CompareRules.CodeNoCheck, string.Empty, $"확인할 항목이 없다: {CompareRules.Show(cells[keyIndex])} (column 항목 칸이 모두 비었다)"));
                }

                var row = new Row(cells[keyIndex], new CsvRow(file.Path, record.Line, header, cells), keyIndex, fieldIndex, columns);
                if (isReference)
                {
                    if (firstByKey.TryGetValue(row.Key, out var first))
                    {
                        errors.Add(side, f, CsvText.Error(file, keyCell.Line, keyCell.Column, CompareRules.CodeDuplicateKey, key, string.Create(CultureInfo.InvariantCulture, $"키가 중복된다: {CompareRules.Show(row.Key)} (처음: {first.Source.File}:{first.Source.Line})")));
                        continue;
                    }

                    firstByKey.Add(row.Key, row);
                }

                rows.Add(row);
            }
        }

        return rows;
    }

    // 머리 행에서 열 이름(정규화 뒤 서수)을 찾는다. 없으면 column, 둘 이상이면 duplicate-column.
    // 점수 파일(ScoreSheet)도 같은 규칙으로 찾는다(text-check.md 4.5: metrics.md 4.7 코드를 같은 뜻으로 쓴다).
    internal static int ColumnIndex(SourceFile file, CsvRecord headerRecord, string[] header, string name, Action<CaseError> add)
    {
        var found = -1;
        for (var i = 0; i < header.Length; i++)
        {
            if (!string.Equals(header[i], name, StringComparison.Ordinal))
            {
                continue;
            }

            if (found >= 0)
            {
                var cell = headerRecord.Cells[i];
                add(CsvText.Error(file, cell.Line, cell.Column, CompareRules.CodeDuplicateColumn, name, string.Create(CultureInfo.InvariantCulture, $"머리 행에 같은 열이 둘 이상 있다 (처음: {found + 1}번째 칸)")));
                return -1;
            }

            found = i;
        }

        if (found < 0)
        {
            add(CsvText.Error(file, headerRecord.Line, 0, CompareRules.CodeColumn, name, "머리 행에 열이 없다"));
        }

        return found;
    }

    private static bool IsNumberField(CompareField field) => field.Tolerance is not null || field.TolerancePct is not null;

    private static CompareMetrics Measure(CaseDefinition definition, SourceFile[] referenceFiles, SourceFile[] candidateFiles, List<Row> reference, List<Row> candidate)
    {
        var compare = definition.Compare;

        // 결과 쪽 같은 키의 첫 행(결과 경로 서수 → 줄)이 대표 행이다. 결과 행은 그 순서로 읽었다.
        // 키마다 결과 행 수를 센다. 두 번 이상이면 그 기준 항목은 값과 상관없이 값 불일치다(13.2, T-013).
        var byKey = new Dictionary<string, Row>(StringComparer.Ordinal);
        var rowsByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var c in candidate)
        {
            byKey.TryAdd(c.Key, c);
            rowsByKey[c.Key] = rowsByKey.GetValueOrDefault(c.Key) + 1;
        }

        var referenceByKey = reference.ToDictionary(r => r.Key, StringComparer.Ordinal);
        var checklist = definition.Checklist is { } cl ? new ChecklistCount(cl) : null;
        var outside = new int[compare.Count];
        var within = new int[compare.Count];
        var notNumber = new int[compare.Count];
        var compared = 0;
        var items = new List<CompareItem>();
        int match = 0, mismatch = 0, missing = 0, withinItems = 0;

        // 기준 행과 결과 행 하나의 차이(4.5 칸 판정). 필드별 셈(fields[])은 기준에 키가 있는 결과 행마다 센다(13.2).
        List<FieldDifference> Differences(Row r, Row c)
        {
            compared++;
            var differences = new List<FieldDifference>();
            for (var i = 0; i < compare.Count; i++)
            {
                var rv = r.Source.Cells[r.FieldIndex[i]];
                var cv = c.Source.Cells[c.FieldIndex[i]];
                var result = Judge(compare[i], rv, cv);
                switch (result)
                {
                    case ResultOutside:
                        outside[i]++;
                        break;
                    case ResultWithin:
                        within[i]++;
                        break;
                    case ResultNotNumber:
                        notNumber[i]++;
                        break;
                }

                if (result is not null)
                {
                    differences.Add(new FieldDifference(compare[i].Field, result, rv, cv));
                }
            }

            return differences;
        }

        // 기준 항목(기준 경로 서수 → 줄). 결과에 키가 있으면 대표 행의 위치와 차이를 적는다.
        foreach (var r in reference)
        {
            if (!byKey.TryGetValue(r.Key, out var c))
            {
                missing++;
                items.Add(new CompareItem(r.Key, ItemMissing, r.Source, null, [], checklist is null ? null : []));
                continue;
            }

            var differences = Differences(r, c);
            var absent = checklist?.Absent(r, c);
            var bad = rowsByKey[r.Key] > 1 || differences.Any(d => d.Result != ResultWithin) || absent is { Count: > 0 };
            if (bad)
            {
                mismatch++;
            }
            else
            {
                match++;
                withinItems += differences.Count > 0 ? 1 : 0;
            }

            items.Add(new CompareItem(r.Key, bad ? ItemMismatch : ItemMatch, r.Source, c.Source, differences, absent));
        }

        // 결과 쪽 항목(결과 경로 서수 → 줄): 기준에 없는 키의 대표 행은 extra, 같은 키의 둘째 행부터는 duplicate.
        // duplicate의 reference는 그 키의 기준 행이고 differences는 그 행과 기준 행의 차이다(13.2). 기준에 없는 키면 null과 []다.
        var extra = 0;
        foreach (var c in candidate)
        {
            referenceByKey.TryGetValue(c.Key, out var r);
            if (!ReferenceEquals(byKey[c.Key], c))
            {
                // 둘째 이후 행의 빠진 항목은 duplicate 항목에 적는다(text-check.md 4.2 규칙 7). 기준에 없는 키면 보지 않는다.
                var differences = r is null ? [] : Differences(r, c);
                var absent = checklist is null ? null : r is null ? [] : checklist.Absent(r, c);
                items.Add(new CompareItem(c.Key, ItemDuplicate, r?.Source, c.Source, differences, absent));
            }
            else if (r is null)
            {
                extra++;
                items.Add(new CompareItem(c.Key, ItemExtra, null, c.Source, [], checklist is null ? null : []));
            }
        }

        var fields = compare.Select((f, i) => new FieldSummary(
            f.Field, IsNumberField(f) ? KindNumber : KindText, f.Tolerance, f.TolerancePct,
            compared, outside[i], within[i], notNumber[i])).ToArray();
        var counts = new CompareCounts(reference.Count, candidate.Count, match, mismatch, missing, extra, withinItems);

        return new CompareMetrics(
            definition.Name, StatusMeasured, null, definition.Inputs!.Key,
            referenceFiles.Select(f => f.Path).ToArray(), candidateFiles.Select(f => f.Path).ToArray(),
            counts, Percent(match, reference.Count), Percent(missing, reference.Count), fields, items, checklist?.Summary());
    }

    // 체크리스트 대조(text-check.md 4.2). 짝지은 결과 행마다 항목을 보고 셈한다(규칙 8: 기준에 키가 있는 결과 행마다).
    // 글은 정규화(4.6) 뒤의 칸을 공백 접기(규칙 2)한 것이다. 있다 = 접은 문구가 접은 문장의 부분 문자열(서수, 규칙 3).
    // column 항목은 그 기준 행의 칸이 문구다. 칸이 비면 그 행에는 요구하지 않는다(규칙 4). any 항목은 문구 하나라도 있으면 있다(규칙 5).
    private sealed class ChecklistCount(CaseChecklist checklist)
    {
        private readonly string[][] phrases = checklist.Items.Select(i => i.Phrases.Select(p => CompareRules.FoldSpaces(CompareRules.Normalize(p))).ToArray()).ToArray();
        private readonly int[] found = new int[checklist.Items.Count];
        private readonly int[] absent = new int[checklist.Items.Count];

        // 그 결과 행에서 빠진 항목(checklist.items 순서).
        public List<string> Absent(Row reference, Row candidate)
        {
            var sentence = CompareRules.FoldSpaces(candidate.Source.Cells[candidate.Columns[checklist.Text]]);
            var missing = new List<string>();
            for (var k = 0; k < checklist.Items.Count; k++)
            {
                var item = checklist.Items[k];
                string[] wanted;
                if (item.Column is { } column)
                {
                    var cell = CompareRules.FoldSpaces(reference.Source.Cells[reference.Columns[column]]);
                    if (cell.Length == 0)
                    {
                        continue;
                    }

                    wanted = [cell];
                }
                else
                {
                    wanted = phrases[k];
                }

                if (wanted.Any(p => sentence.Contains(p, StringComparison.Ordinal)))
                {
                    found[k]++;
                }
                else
                {
                    absent[k]++;
                    missing.Add(item.Item);
                }
            }

            return missing;
        }

        // 항목별 셈. 문구는 case.yaml에 쓴 글 그대로다(text-check.md 3.1).
        public ChecklistSummary Summary() => new(
            checklist.Text,
            checklist.Items.Select((item, k) => new ChecklistEntry(item.Item, item.Column, item.Phrases, found[k] + absent[k], found[k], absent[k])).ToArray());
    }

    // 칸 하나의 비교(스펙 4.5·4.6). 차이가 없으면 null.
    internal static string? Judge(CompareField field, string reference, string candidate)
    {
        if (!IsNumberField(field))
        {
            return string.Equals(reference, candidate, StringComparison.Ordinal) ? null : ResultOutside;
        }

        // 기준 칸은 ReadSide가 숫자임을 확인했다.
        var r = decimal.Parse(reference, CompareRules.NumberStyle, CultureInfo.InvariantCulture);
        if (!CompareRules.TryNumber(candidate, out var c))
        {
            return ResultNotNumber;
        }

        decimal diff;
        try
        {
            diff = Math.Abs(c - r);
        }
        catch (OverflowException)
        {
            return ResultOutside;
        }

        if (diff == 0)
        {
            return null;
        }

        var ok = field.Tolerance is { } t ? diff <= t : WithinPct(diff, r, field.TolerancePct!.Value);
        return ok ? ResultWithin : ResultOutside;
    }

    // |c − r| × 100 ≤ |r| × p. 나눗셈을 쓰지 않는다. 곱이 decimal 범위를 넘으면 그쪽을 무한대로 본다.
    private static bool WithinPct(decimal diff, decimal r, decimal pct)
    {
        var left = Multiply(diff, 100m);
        var right = Multiply(Math.Abs(r), pct);
        return (left, right) switch
        {
            (null, null) => diff <= (Multiply(Math.Abs(r), pct / 100m) ?? decimal.MaxValue),
            (null, _) => false,
            (_, null) => true,
            ({ } a, { } b) => a <= b,
        };
    }

    private static decimal? Multiply(decimal a, decimal b)
    {
        try
        {
            return a * b;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    // 4.8: part / whole × 100을 소수 첫째 자리로. 정수 셈으로 0.05를 올린다(0에서 먼 쪽). 결과의 소수 자릿수는 늘 1이다.
    internal static decimal Percent(int part, int whole)
    {
        var tenths = ((2L * part * 1000) + whole) / (2L * whole);
        return new decimal((int)tenths, 0, 0, false, CompareRules.PercentDigits);
    }

    // Columns: 체크리스트 열 이름 → 칸 자리(기준 쪽 column 항목 열, 결과 쪽 문장 열).
    private sealed record Row(string Key, CsvRow Source, int KeyIndex, int[] FieldIndex, IReadOnlyDictionary<string, int> Columns);

    // 오류 순서: 기준 쪽 → 결과 쪽, 쪽 안에서는 파일이 정해지기 전 오류(-1: pattern, 폴더·파일 없음, no-rows) → 파일 경로 서수 순, 파일 안에서는 (줄, 열), 같으면 찾은 순서.
    private sealed class ErrorList
    {
        private readonly List<(int Side, int File, int Seq, CaseError Error)> errors = [];

        public int Count => errors.Count;

        public void Add(int side, int file, CaseError error) => errors.Add((side, file, errors.Count, error));

        public CaseError[] Sorted() => errors
            .OrderBy(e => e.Side).ThenBy(e => e.File).ThenBy(e => e.Error.Line).ThenBy(e => e.Error.Column).ThenBy(e => e.Seq)
            .Select(e => e.Error)
            .ToArray();
    }
}
