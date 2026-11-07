using System.Globalization;
using Scorecard.Core.Cases;

namespace Scorecard.Core.Comparison;

// 기준 자료와 결과 자료를 키로 맞춰 일치·값 불일치·누락·추가를 낸다. docs/spec/metrics.md 4.3~4.8.
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

    public const string KindNumber = "number";
    public const string KindText = "text";

    public const string ResultWithin = "within";
    public const string ResultOutside = "outside";
    public const string ResultNotNumber = "not_number";

    // 케이스 폴더의 inputs 파일을 읽어 대조한다. inputs가 없거나 compare가 비면 파일을 읽지 않고 미검증이다.
    public static CompareResult CompareDirectory(string caseDirectory, CaseDefinition definition)
    {
        ArgumentException.ThrowIfNullOrEmpty(caseDirectory);
        ArgumentNullException.ThrowIfNull(definition);

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

        var reference = ReadSide(0, referenceFiles, inputs.Key, definition.Compare, checkNumbers: true, errors);
        var candidate = ReadSide(1, candidateFiles, inputs.Key, definition.Compare, checkNumbers: false, errors);
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

    private static string? NotVerifiedReason(CaseDefinition definition) =>
        definition.Inputs is null ? ReasonNoInputs
        : definition.Compare.Count == 0 ? ReasonNoCompareFields
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

    // 한쪽 파일을 모두 읽는다. 키 중복은 같은 쪽 모든 파일에서 본다. checkNumbers면 숫자 필드가 숫자인지 본다(기준 자료).
    private static List<Row> ReadSide(int side, SourceFile[] files, string key, IReadOnlyList<CompareField> compare, bool checkNumbers, ErrorList errors)
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
            var keyIndex = ColumnIndex(file, csv.Header, header, key, side, f, errors);
            var fieldIndex = compare.Select(c => ColumnIndex(file, csv.Header, header, c.Field, side, f, errors)).ToArray();
            if (keyIndex < 0 || fieldIndex.Any(i => i < 0))
            {
                continue;
            }

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

                if (checkNumbers)
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

                var row = new Row(cells[keyIndex], new CsvRow(file.Path, record.Line, header, cells), keyIndex, fieldIndex);
                if (firstByKey.TryGetValue(row.Key, out var first))
                {
                    errors.Add(side, f, CsvText.Error(file, keyCell.Line, keyCell.Column, CompareRules.CodeDuplicateKey, key, string.Create(CultureInfo.InvariantCulture, $"키가 중복된다: {CompareRules.Show(row.Key)} (처음: {first.Source.File}:{first.Source.Line})")));
                    continue;
                }

                firstByKey.Add(row.Key, row);
                rows.Add(row);
            }
        }

        return rows;
    }

    // 머리 행에서 열 이름(정규화 뒤 서수)을 찾는다. 없으면 column, 둘 이상이면 duplicate-column.
    private static int ColumnIndex(SourceFile file, CsvRecord headerRecord, string[] header, string name, int side, int fileIndex, ErrorList errors)
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
                errors.Add(side, fileIndex, CsvText.Error(file, cell.Line, cell.Column, CompareRules.CodeDuplicateColumn, name, string.Create(CultureInfo.InvariantCulture, $"머리 행에 같은 열이 둘 이상 있다 (처음: {found + 1}번째 칸)")));
                return -1;
            }

            found = i;
        }

        if (found < 0)
        {
            errors.Add(side, fileIndex, CsvText.Error(file, headerRecord.Line, 0, CompareRules.CodeColumn, name, "머리 행에 열이 없다"));
        }

        return found;
    }

    private static bool IsNumberField(CompareField field) => field.Tolerance is not null || field.TolerancePct is not null;

    private static CompareMetrics Measure(CaseDefinition definition, SourceFile[] referenceFiles, SourceFile[] candidateFiles, List<Row> reference, List<Row> candidate)
    {
        var compare = definition.Compare;
        var byKey = candidate.ToDictionary(r => r.Key, StringComparer.Ordinal);
        var referenceKeys = reference.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        var outside = new int[compare.Count];
        var within = new int[compare.Count];
        var notNumber = new int[compare.Count];
        var items = new List<CompareItem>();
        int match = 0, mismatch = 0, missing = 0, withinItems = 0;

        foreach (var r in reference)
        {
            if (!byKey.TryGetValue(r.Key, out var c))
            {
                missing++;
                items.Add(new CompareItem(r.Key, ItemMissing, r.Source, null, []));
                continue;
            }

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

            var bad = differences.Any(d => d.Result != ResultWithin);
            if (bad)
            {
                mismatch++;
            }
            else
            {
                match++;
                withinItems += differences.Count > 0 ? 1 : 0;
            }

            items.Add(new CompareItem(r.Key, bad ? ItemMismatch : ItemMatch, r.Source, c.Source, differences));
        }

        var extra = 0;
        foreach (var c in candidate.Where(c => !referenceKeys.Contains(c.Key)))
        {
            extra++;
            items.Add(new CompareItem(c.Key, ItemExtra, null, c.Source, []));
        }

        var compared = reference.Count - missing;
        var fields = compare.Select((f, i) => new FieldSummary(
            f.Field, IsNumberField(f) ? KindNumber : KindText, f.Tolerance, f.TolerancePct,
            compared, outside[i], within[i], notNumber[i])).ToArray();
        var counts = new CompareCounts(reference.Count, candidate.Count, match, mismatch, missing, extra, withinItems);

        return new CompareMetrics(
            definition.Name, StatusMeasured, null, definition.Inputs!.Key,
            referenceFiles.Select(f => f.Path).ToArray(), candidateFiles.Select(f => f.Path).ToArray(),
            counts, Percent(match, reference.Count), Percent(missing, reference.Count), fields, items);
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

    private sealed record Row(string Key, CsvRow Source, int KeyIndex, int[] FieldIndex);

    // 오류 순서: 기준 쪽 → 결과 쪽, 쪽 안에서는 패턴 오류(-1) → 파일 경로 서수 순, 파일 안에서는 (줄, 열), 같으면 찾은 순서.
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
