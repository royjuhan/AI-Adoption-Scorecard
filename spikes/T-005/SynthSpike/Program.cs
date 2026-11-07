using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Core.Synthetic;

// T-005 스파이크 도구(머지 대상 아님). 절대 경로를 찍지 않는다.
// gen <spec.md> <예제> <케이스폴더> <출력폴더> : 스펙 표의 계획으로 reference/·candidate/ CSV를 만든다.
// oracle <예제폴더>                          : 키로 맞춰 일치·불일치·누락·추가를 센다(제안 정의, T-006 엔진 아님).
// case-check <예제폴더>                      : case.yaml과 answers.yaml의 단계·답, case.yaml 분류 = expected/classification.json.
// blocks <spec.md> <예제 루트>                : 스펙 7절 블록·SHA-256 = 파일 바이트.
// structure <spec.md> <예제 루트>             : 스펙 4.7 검증 자료 구조 표 = oracle 세기.
// scan <spec.md> <루트>                      : 스펙 패턴 표로 파일을 검사한다.
// probe <spec.md> <예제 루트>                 : 메모리 안 실험.
// refs <dll>                                 : AssemblyRef·TypeRef 네임스페이스.
internal static class Program
{
    private static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        return args switch
        {
            ["gen", var spec, var example, var caseDir, var outDir] => Gen(spec, example, caseDir, outDir),
            ["oracle", var dir] => Oracle.Run(dir),
            ["case-check", var dir] => CaseCheck(dir),
            ["blocks", var spec, var root] => Blocks(spec, root),
            ["structure", var spec, var root] => Structure(spec, root),
            ["scan", var spec, var root] => Scanner.Run(spec, root, quiet: false),
            ["scan", var spec, var root, "--counts"] => Scanner.Run(spec, root, quiet: true),
            ["probe", var spec, var root] => Probes.Run(spec, root),
            ["refs", var dll] => Refs(dll),
            _ => Usage(),
        };
    }

    public static readonly string[] Examples = ["table-compare", "form-fill", "mixed-exceptions"];
    public static readonly string[] DataExamples = ["table-compare", "form-fill"];

    private static int Usage()
    {
        Console.Error.WriteLine("사용: gen|oracle|case-check|blocks|scan|probe|refs");
        return 2;
    }

    public static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static CaseDefinition ParseCase(string dir)
    {
        var parsed = CaseParser.ParseDirectory(dir);
        if (parsed.Case is null)
        {
            throw new InvalidOperationException("case.yaml 오류: " + string.Join(" | ", parsed.Errors));
        }

        return parsed.Case;
    }

    private static int Gen(string spec, string example, string caseDir, string outDir)
    {
        var definition = ParseCase(caseDir);
        var plan = Plans.FromSpec(spec, example);
        var tables = SyntheticGenerator.Generate(definition, plan);
        var refBytes = SyntheticCsv.Render(tables.Header, tables.Reference);
        var candBytes = SyntheticCsv.Render(tables.Header, tables.Candidate);
        Directory.CreateDirectory(Path.Combine(outDir, SyntheticCsv.ReferenceFolder));
        Directory.CreateDirectory(Path.Combine(outDir, SyntheticCsv.CandidateFolder));
        File.WriteAllBytes(Path.Combine(outDir, SyntheticCsv.ReferenceFolder, tables.FileName), refBytes);
        File.WriteAllBytes(Path.Combine(outDir, SyntheticCsv.CandidateFolder, tables.FileName), candBytes);
        Console.WriteLine($"SHA {example} reference/{tables.FileName} {Sha(refBytes)} bytes={refBytes.Length} lines={refBytes.Count(b => b == '\n')}");
        Console.WriteLine($"SHA {example} candidate/{tables.FileName} {Sha(candBytes)} bytes={candBytes.Length} lines={candBytes.Count(b => b == '\n')}");
        Console.WriteLine($"RESULT {example} header=[{string.Join(",", tables.Header)}]");
        foreach (var group in tables.Reference.Where(r => r.Planted != SyntheticGenerator.PlantedNone).GroupBy(r => r.Planted))
        {
            Console.WriteLine($"RESULT {example} reference {group.Key}: {string.Join(" ", group.Select(r => r.Key))}");
        }

        foreach (var group in tables.Candidate.Where(r => r.Planted != SyntheticGenerator.PlantedNone).GroupBy(r => r.Planted).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"RESULT {example} candidate {group.Key}: {string.Join(" ", group.Select(r => r.Key))}");
        }

        if (plan.FocusColumn is not null)
        {
            var f = Array.IndexOf(tables.Header.ToArray(), plan.FocusColumn) - 1;
            var focusKeys = tables.Reference.Where(r => r.Values[f] == plan.FocusValue).Select(r => r.Key).ToArray();
            Console.WriteLine($"RESULT {example} focus {plan.FocusColumn}={plan.FocusValue} rows={focusKeys.Length}: {string.Join(" ", focusKeys)}");
        }

        Console.WriteLine($"RESULT {example} candidate-order: {string.Join(" ", tables.Candidate.Select(r => r.Key))}");
        return 0;
    }

    private static int CaseCheck(string dir)
    {
        var label = Path.GetFileName(Path.TrimEndingDirectorySeparator(dir));
        var caseDef = ParseCase(dir);
        var answers = CaseParser.Parse(File.ReadAllBytes(Path.Combine(dir, "answers.yaml")), "answers.yaml").Case!;
        var sameSteps = caseDef.Steps.Count == answers.Steps.Count && caseDef.Steps.Zip(answers.Steps).All(p => p.First.Number == p.Second.Number && p.First.Name == p.Second.Name && p.First.Answers == p.Second.Answers);
        Console.WriteLine($"RESULT {label} case.yaml errors=0 name-equal={caseDef.Name == answers.Name} task-equal={caseDef.Task == answers.Task} steps-equal={sameSteps} steps={caseDef.Steps.Count}");
        Console.WriteLine($"RESULT {label} inputs={(caseDef.Inputs is null ? "null" : $"{caseDef.Inputs.Reference}|{caseDef.Inputs.Candidate}|{caseDef.Inputs.Key}")} compare=[{string.Join(" ", caseDef.Compare.Select(c => $"{c.Field}:{c.Tolerance?.ToString(CultureInfo.InvariantCulture) ?? "-"}:{c.TolerancePct?.ToString(CultureInfo.InvariantCulture) ?? "-"}"))}] thresholds={caseDef.Thresholds.AccuracyMin}/{caseDef.Thresholds.MissingMax}/{caseDef.Thresholds.TimeMustDrop} before={caseDef.Before ?? "null"} after={caseDef.After ?? "null"} ai_usage={caseDef.AiUsage ?? "null"}");
        var answersText = File.ReadAllText(Path.Combine(dir, "answers.yaml"));
        var body = answersText[answersText.IndexOf("\nname: ", StringComparison.Ordinal)..].TrimStart('\n');
        var caseText = File.ReadAllText(Path.Combine(dir, "case.yaml"));
        Console.WriteLine($"RESULT {label} answers.yaml 'name:'~끝 바이트가 case.yaml 안에 그대로: {caseText.Contains(body, StringComparison.Ordinal)}");
        var fromCase = ClassificationJson.Render(StepClassifier.Classify(caseDef));
        var expected = File.ReadAllBytes(Path.Combine(dir, "expected", ClassificationJson.FileName));
        Console.WriteLine($"RESULT {label} classify(case.yaml) == expected/classification.json: {fromCase.AsSpan().SequenceEqual(expected)}");
        return 0;
    }

    private static int Blocks(string spec, string root)
    {
        var bad = 0;
        foreach (var example in Examples)
        {
            var heading = Spec.HeadingFor(spec, example);
            var blocks = Spec.CodeBlocks(spec, heading);
            var files = Plans.ExampleFiles(spec, example);
            Console.WriteLine($"RESULT blocks {example} heading='{heading}' blocks={blocks.Count} files={files.Length}");
            for (var i = 0; i < files.Length; i++)
            {
                var path = Path.Combine(root, example, files[i]);
                var actual = File.Exists(path) ? File.ReadAllBytes(path) : null;
                var block = i < blocks.Count ? Encoding.UTF8.GetBytes(blocks[i]) : null;
                var same = actual is not null && block is not null && actual.AsSpan().SequenceEqual(block);
                bad += same ? 0 : 1;
                var hashLine = Spec.Lines(spec).Where(l => l.TrimStart().StartsWith($"`examples/{example}/{files[i]}` (SHA-256 `", StringComparison.Ordinal)).ToArray();
                var specSha = hashLine.Length == 1 ? Spec.CodeSpans(hashLine[0])[1] : $"lines={hashLine.Length}";
                var shaSame = actual is not null && specSha == Sha(actual);
                bad += shaSame ? 0 : 1;
                Console.WriteLine($"BLOCK {example} {files[i]} block==file: {same} spec-sha==file: {shaSame}");
            }
        }

        return bad == 0 ? 0 : 1;
    }

    private static int Structure(string spec, string root)
    {
        var bad = 0;
        foreach (var row in Spec.Table(spec, Spec.StructureHeader))
        {
            var example = Spec.Unquote(row[0]);
            var dir = Path.Combine(root, example);
            var definition = ParseCase(dir);
            var (header, refRows) = Oracle.ReadCsvFolder(Path.Combine(dir, SyntheticCsv.ReferenceFolder));
            var (_, candRows) = Oracle.ReadCsvFolder(Path.Combine(dir, SyntheticCsv.CandidateFolder));
            var c = Oracle.Count(definition, header, refRows, candRows, "base");
            string[] actual =
            [
                example, $"{c.Reference}", $"{c.Candidate}", $"{c.Match}", $"{c.Mismatch}", $"{c.Missing}", $"{c.Extra}", $"{c.Within}",
                c.Accuracy.ToString("F1", CultureInfo.InvariantCulture), c.MissingRate.ToString("F1", CultureInfo.InvariantCulture),
            ];
            var same = row.Select(Spec.Unquote).SequenceEqual(actual);
            bad += same ? 0 : 1;
            Console.WriteLine($"RESULT structure {example} table==oracle: {same} [{string.Join(" ", actual)}]");
        }

        return bad == 0 ? 0 : 1;
    }

    private static int Refs(string dll)
    {
        using var stream = File.OpenRead(dll);
        using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
        var md = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
        foreach (var name in md.AssemblyReferences.Select(h => md.GetString(md.GetAssemblyReference(h).Name)).Order(StringComparer.Ordinal))
        {
            Console.WriteLine($"ASMREF {name}");
        }

        foreach (var ns in md.TypeReferences.Select(h => md.GetString(md.GetTypeReference(h).Namespace)).Distinct().Order(StringComparer.Ordinal))
        {
            Console.WriteLine($"TYPEREF-NS {ns}");
        }

        return 0;
    }
}

// 스펙 문서 읽기. 테스트의 SpecDocument와 같은 규칙(머리 행이 정확히 한 번, '\|'는 칸 구분 아님).
internal static class Spec
{
    public const string PlanHeader = "| 예제 | 시드 | 파일 | 키 접두어 | 키 자릿수 | 기준 건수 | 값 불일치 열 | 누락 | 추가 | 허용오차 안 차이 | 몰림 | 결과 섞기 |";
    public const string ColumnHeader = "| 예제 | 열 | 종류 | 값 |";
    public const string StructureHeader = "| 예제 | 기준 행 | 결과 행 | 일치 | 값 불일치 | 누락 | 추가 | 허용오차 안 차이 | 정확도(%) | 누락률(%) |";
    public const string PatternHeader = "| 패턴 | 무엇 | 정규식 | 잡는 예 | 잡지 않는 예 |";
    public const string FilesHeader = "| 예제 | 파일 |";

    public static string[] Lines(string path) => File.ReadAllLines(path);

    public static IReadOnlyList<string[]> Table(string path, string header)
    {
        var lines = Lines(path);
        var starts = Enumerable.Range(0, lines.Length).Where(i => lines[i].Trim() == header).ToArray();
        if (starts.Length != 1)
        {
            throw new InvalidOperationException($"머리 행 '{header}'이 {starts.Length}번 나온다.");
        }

        var columns = Cells(header).Length;
        var rows = new List<string[]>();
        for (var i = starts[0] + 1; i < lines.Length && lines[i].TrimStart().StartsWith('|'); i++)
        {
            var cells = Cells(lines[i]);
            if (cells.All(c => c.Length > 0 && c.All(ch => ch is '-' or ':')))
            {
                continue;
            }

            if (cells.Length != columns)
            {
                throw new InvalidOperationException($"{i + 1}줄 칸 수 {cells.Length}, {columns}이어야 한다.");
            }

            rows.Add(cells);
        }

        return rows;
    }

    public static string HeadingFor(string path, string example)
    {
        var matches = Lines(path).Where(l => l.StartsWith("### 7.", StringComparison.Ordinal) && l.EndsWith(" " + example, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException($"7절 제목 {example}: {matches.Length}개");
    }

    public static IReadOnlyList<string> CodeBlocks(string path, string heading)
    {
        var lines = Lines(path);
        var start = Array.FindIndex(lines, l => l.TrimEnd() == heading);
        var blocks = new List<string>();
        List<string>? current = null;
        for (var i = start + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (current is null && line.StartsWith('#'))
            {
                break;
            }

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                if (current is null)
                {
                    current = [];
                }
                else
                {
                    blocks.Add(string.Concat(current.Select(l => l + "\n")));
                    current = null;
                }

                continue;
            }

            current?.Add(line);
        }

        return blocks;
    }

    public static string[] CodeSpans(string text)
    {
        var parts = text.Split('`');
        if (parts.Length % 2 == 0)
        {
            throw new InvalidOperationException("백틱 짝: " + text);
        }

        return parts.Where((_, i) => i % 2 == 1).ToArray();
    }

    public static string Unquote(string cell) => cell.Trim().Trim('`');

    private static string[] Cells(string row)
    {
        var text = row.Trim();
        text = text.Substring(1, text.Length - (text.EndsWith('|') ? 2 : 1));
        var cells = new List<string>();
        var cell = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == '|')
            {
                cell.Append('|');
                i++;
            }
            else if (text[i] == '|')
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
            }
            else
            {
                cell.Append(text[i]);
            }
        }

        cells.Add(cell.ToString().Trim());
        return cells.ToArray();
    }
}

// 스펙 4.3 생성 계획 표 + 4.4 열 표 → SyntheticPlan.
internal static class Plans
{
    public static SyntheticPlan FromSpec(string spec, string example)
    {
        var row = Spec.Table(spec, Spec.PlanHeader).Single(c => Spec.Unquote(c[0]) == example);
        var columns = Spec.Table(spec, Spec.ColumnHeader)
            .Where(c => Spec.Unquote(c[0]) == example)
            .Select(c => Column(Spec.Unquote(c[1]), c[2], c[3]))
            .ToArray();
        var mismatch = row[6] == "없음" ? [] : Spec.CodeSpans(row[6]);
        string? withinColumn = null;
        var within = 0;
        if (row[9] != "없음")
        {
            withinColumn = Spec.CodeSpans(row[9])[0];
            within = int.Parse(row[9].Split(' ').Last(), CultureInfo.InvariantCulture);
        }

        string? focusColumn = null, focusValue = null;
        if (row[10] != "없음")
        {
            var spans = Spec.CodeSpans(row[10]);
            (focusColumn, focusValue) = (spans[0], spans[1]);
        }

        return new SyntheticPlan(
            ulong.Parse(Spec.Unquote(row[1]), CultureInfo.InvariantCulture),
            Spec.Unquote(row[2]),
            Spec.Unquote(row[3]),
            Int(row[4]),
            Int(row[5]),
            columns,
            mismatch,
            Int(row[7]),
            Int(row[8]),
            withinColumn,
            within,
            focusColumn,
            focusValue,
            row[11] switch { "예" => true, "아니오" => false, _ => throw new InvalidOperationException(row[11]) });
    }

    // 스펙 7절 예제 파일 목록 표(블록 순서).
    public static string[] ExampleFiles(string spec, string example) =>
        Spec.Table(spec, Spec.FilesHeader)
            .Where(c => Spec.Unquote(c[0]) == example)
            .Select(c => Spec.Unquote(c[1]))
            .ToArray();

    private static int Int(string cell) => int.Parse(Spec.Unquote(cell), CultureInfo.InvariantCulture);

    private static SyntheticColumn Column(string name, string kind, string values)
    {
        var spans = Spec.CodeSpans(values);
        return kind switch
        {
            "정수" => SyntheticColumn.Integer(name, int.Parse(spans[0], CultureInfo.InvariantCulture), int.Parse(spans[1], CultureInfo.InvariantCulture)),
            "소수" => SyntheticColumn.Decimal(
                name,
                decimal.Parse(spans[0], CultureInfo.InvariantCulture),
                decimal.Parse(spans[1], CultureInfo.InvariantCulture),
                spans[0].Contains('.') ? spans[0].Length - spans[0].IndexOf('.') - 1 : 0),
            "선택" => SyntheticColumn.Choice(name, spans),
            _ => throw new InvalidOperationException("종류: " + kind),
        };
    }
}

// 제안 정의(스펙 4.7)로 센다. T-006 대조 엔진이 아니다. 자료가 12절 숫자를 내도록 심겼는지만 본다.
// variant: base(≤, 기준값 대비 %), strict(<), candbase(결과값 대비 %).
internal static class Oracle
{
    public sealed record Counts(int Reference, int Candidate, int Match, int Mismatch, int Missing, int Extra, int Within, int CellTotal = 0, int CellMatch = 0)
    {
        // 다른 정의: 항목 = 칸(기준 행 × compare 필드). 누락 행의 칸은 모두 불일치로 센다.
        public decimal CellAccuracy => CellTotal == 0 ? 0 : Round(CellMatch * 100m / CellTotal);

        public decimal Accuracy => Round(Match * 100m / Reference);

        public decimal MissingRate => Round(Missing * 100m / Reference);

        // 다른 정의: 일치 / (기준 ∩ 결과).
        public decimal AccuracyOfCompared => Round(Match * 100m / (Match + Mismatch));

        public override string ToString() =>
            $"ref={Reference} cand={Candidate} match={Match} mismatch={Mismatch} missing={Missing} extra={Extra} within={Within} accuracy={Accuracy.ToString("F1", CultureInfo.InvariantCulture)} missing_rate={MissingRate.ToString("F1", CultureInfo.InvariantCulture)} accuracy_of_compared={AccuracyOfCompared.ToString("F1", CultureInfo.InvariantCulture)} cell_accuracy={CellAccuracy.ToString("F1", CultureInfo.InvariantCulture)}";

        private static decimal Round(decimal v) => Math.Round(v, 1, MidpointRounding.AwayFromZero);
    }

    public static int Run(string dir)
    {
        var label = Path.GetFileName(Path.TrimEndingDirectorySeparator(dir));
        var definition = Program.ParseCase(dir);
        var (refHeader, refRows) = ReadCsvFolder(Path.Combine(dir, SyntheticCsv.ReferenceFolder));
        var (candHeader, candRows) = ReadCsvFolder(Path.Combine(dir, SyntheticCsv.CandidateFolder));
        Console.WriteLine($"RESULT oracle {label} same-header={refHeader.SequenceEqual(candHeader)}");
        foreach (var variant in new[] { "base", "strict", "candbase" })
        {
            Console.WriteLine($"RESULT oracle {label} {variant} {Count(definition, refHeader, refRows, candRows, variant)}");
        }

        return 0;
    }

    public static Counts Count(SyntheticTables t, CaseDefinition definition, string variant = "base") =>
        Count(definition, t.Header.ToArray(), t.Reference.Select(r => (string[])[r.Key, .. r.Values]).ToList(), t.Candidate.Select(r => (string[])[r.Key, .. r.Values]).ToList(), variant);

    public static Counts Count(CaseDefinition definition, string[] header, List<string[]> reference, List<string[]> candidate, string variant)
    {
        var key = Array.IndexOf(header, definition.Inputs!.Key);
        var refByKey = reference.ToDictionary(r => r[key], StringComparer.Ordinal);
        var candByKey = candidate.ToDictionary(r => r[key], StringComparer.Ordinal);
        int match = 0, mismatch = 0, missing = 0, within = 0, cellMatch = 0;
        foreach (var r in reference)
        {
            if (!candByKey.TryGetValue(r[key], out var c))
            {
                missing++;
                continue;
            }

            var ok = true;
            var differs = false;
            foreach (var f in definition.Compare)
            {
                var i = Array.IndexOf(header, f.Field);
                differs |= r[i] != c[i];
                var fieldOk = Within(f, r[i], c[i], variant);
                cellMatch += fieldOk ? 1 : 0;
                ok &= fieldOk;
            }

            if (ok)
            {
                match++;
                within += differs ? 1 : 0;
            }
            else
            {
                mismatch++;
            }
        }

        var extra = candidate.Count(c => !refByKey.ContainsKey(c[key]));
        return new Counts(reference.Count, candidate.Count, match, mismatch, missing, extra, within, reference.Count * definition.Compare.Count, cellMatch);
    }

    private static bool Within(CompareField f, string r, string c, string variant)
    {
        if (f.Tolerance is null && f.TolerancePct is null)
        {
            return string.Equals(r, c, StringComparison.Ordinal);
        }

        var rv = decimal.Parse(r, NumberStyles.Number, CultureInfo.InvariantCulture);
        var cv = decimal.Parse(c, NumberStyles.Number, CultureInfo.InvariantCulture);
        var diff = Math.Abs(cv - rv);
        var limit = f.Tolerance ?? (f.TolerancePct!.Value * Math.Abs(variant == "candbase" ? cv : rv) / 100m);
        return variant == "strict" ? diff < limit || (diff == 0 && limit == 0) : diff <= limit;
    }

    public static (string[] Header, List<string[]> Rows) ReadCsvFolder(string folder)
    {
        var files = Directory.EnumerateFiles(folder, "*.csv").Order(StringComparer.Ordinal).ToArray();
        if (files.Length != 1)
        {
            throw new InvalidOperationException($"CSV {files.Length}개");
        }

        var lines = Encoding.UTF8.GetString(File.ReadAllBytes(files[0])).Split('\n');
        if (lines[^1].Length != 0)
        {
            throw new InvalidOperationException("마지막 줄바꿈 없음");
        }

        var header = lines[0].Split(',');
        return (header, lines.Skip(1).Take(lines.Length - 2).Select(l => l.Split(',')).ToList());
    }
}

// 스펙 4.10 패턴 표로 파일을 검사한다. NUL 바이트가 있는 파일은 바이너리로 보고 뺀다(테스트 기존 규칙과 같음).
internal static class Scanner
{
    public sealed record Pattern(string Name, Regex Regex, string[] Hits, string[] Misses);

    public static IReadOnlyList<Pattern> Load(string spec) =>
        Spec.Table(spec, Spec.PatternHeader)
            .Select(c => new Pattern(
                Spec.Unquote(c[0]),
                new Regex(Spec.Unquote(c[2]), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)),
                Spec.CodeSpans(c[3]),
                Spec.CodeSpans(c[4])))
            .ToArray();

    public static IEnumerable<string> Hits(IReadOnlyList<Pattern> patterns, string relative, byte[] bytes)
    {
        foreach (var p in patterns)
        {
            foreach (Match m in p.Regex.Matches(relative))
            {
                yield return $"{p.Name} {relative} (경로): {m.Value}";
            }
        }

        if (bytes.AsSpan().Contains((byte)0))
        {
            yield break;
        }

        var lines = Encoding.UTF8.GetString(bytes).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            foreach (var p in patterns)
            {
                foreach (Match m in p.Regex.Matches(lines[i]))
                {
                    yield return $"{p.Name} {relative}:{i + 1}: {m.Value}";
                }
            }
        }
    }

    // quiet: HIT 줄 대신 (맨 위 폴더, 패턴)별 건수만 낸다. 저장소 전체 검사의 요약용.
    public static int Run(string spec, string root, bool quiet)
    {
        var patterns = Load(spec);
        var skip = new HashSet<string>(StringComparer.Ordinal) { ".git", "bin", "obj", "artifacts", "TestResults" };
        var files = Walk(root, skip).Select(f => (Full: f, Rel: Path.GetRelativePath(root, f).Replace('\\', '/'))).OrderBy(f => f.Rel, StringComparer.Ordinal).ToArray();
        var perTop = new SortedDictionary<string, (int Files, int Hits)>(StringComparer.Ordinal);
        var perPattern = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (full, rel) in files)
        {
            var top = rel.Contains('/') ? rel[..rel.IndexOf('/')] : "(루트)";
            var hits = Hits(patterns, rel, File.ReadAllBytes(full)).ToArray();
            var (f, h) = perTop.GetValueOrDefault(top);
            perTop[top] = (f + 1, h + hits.Length);
            foreach (var hit in hits)
            {
                var k = $"{top} {hit[..hit.IndexOf(' ')]}";
                perPattern[k] = perPattern.GetValueOrDefault(k) + 1;
                if (!quiet)
                {
                    Console.WriteLine("HIT " + hit);
                }
            }
        }

        if (quiet)
        {
            foreach (var (k, n) in perPattern)
            {
                Console.WriteLine($"RESULT scan-pattern {k} hits={n}");
            }
        }

        foreach (var (top, (f, h)) in perTop)
        {
            Console.WriteLine($"RESULT scan {top} files={f} hits={h}");
        }

        Console.WriteLine($"RESULT scan total files={files.Length} hits={perTop.Values.Sum(v => v.Hits)}");
        return perTop.Values.Sum(v => v.Hits) == 0 ? 0 : 1;
    }

    private static IEnumerable<string> Walk(string dir, HashSet<string> skip)
    {
        foreach (var f in Directory.EnumerateFiles(dir))
        {
            yield return f;
        }

        foreach (var d in Directory.EnumerateDirectories(dir))
        {
            if (!skip.Contains(Path.GetFileName(d)))
            {
                foreach (var f in Walk(d, skip))
                {
                    yield return f;
                }
            }
        }
    }
}

internal static class Probes
{
    private static string root = "";
    private static string spec = "";

    private static void P(string group, string text) => Console.WriteLine($"PROBE {group} {text}");

    public static int Run(string specPath, string exampleRoot)
    {
        spec = specPath;
        root = exampleRoot;
        Prng();
        Cultures();
        Repeat();
        Alternatives();
        Seeds();
        Validation();
        Patterns();
        Csv();
        return 0;
    }

    private static CaseDefinition Case(string example) => Program.ParseCase(Path.Combine(root, example));

    private static byte[] Bytes(SyntheticTables t) =>
        [.. SyntheticCsv.Render(t.Header, t.Reference), .. SyntheticCsv.Render(t.Header, t.Candidate)];

    // SplitMix64: 시드 0·101·102의 첫 값 세 개(16진수). run-spike.sh가 Python 독립 구현과 비교한다.
    private static void Prng()
    {
        foreach (var seed in new ulong[] { 0, 101, 102, ulong.MaxValue })
        {
            var rng = new SplitMix64Probe(seed);
            P("prng", $"seed={seed} first3={rng.Next():x16},{rng.Next():x16},{rng.Next():x16}");
        }

        // NextBelow는 Next() × bound의 상위 64비트. bound=1이면 늘 0, bound=2·10의 처음 열 개.
        var r2 = new SplitMix64Probe(0);
        P("prng", $"seed=0 below2x10={string.Join("", Enumerable.Range(0, 10).Select(_ => r2.Below(2)))}");
        var r10 = new SplitMix64Probe(0);
        P("prng", $"seed=0 below10x10={string.Join("", Enumerable.Range(0, 10).Select(_ => r10.Below(10)))}");

        // Core 공개 API로 수열 보기(SplitMix64는 internal): 시드 0, 정수 열 하나(0~999), 3행, 심기·섞기 없음.
        // 기대값은 (첫 세 값 × 1000) >> 64. 테스트가 같은 방법으로 Core 수열을 확인할 수 있다.
        var yaml = "name: p\ntask: p\nsteps:\n  - name: s\ninputs: {reference: r/*.csv, candidate: c/*.csv, key: id}\n"u8.ToArray();
        var small = CaseParser.Parse(yaml, "case.yaml").Case!;
        var smallPlan = new SyntheticPlan(0, "x.csv", "k", 1, 3, [SyntheticColumn.Integer("v", 0, 999)], [], 0, 0, null, 0, null, null, false);
        var values = SyntheticGenerator.Generate(small, smallPlan).Reference.Select(r => r.Values[0]);
        var r1000 = new SplitMix64Probe(0);
        var expected = Enumerable.Range(0, 3).Select(_ => r1000.Below(1000).ToString(CultureInfo.InvariantCulture));
        P("prng", $"core-via-generate seed=0 int0-999 rows={string.Join(",", values)} expected={string.Join(",", expected)}");
    }

    // 문화권 7종에서 같은 바이트.
    private static void Cultures()
    {
        string[] cultures = ["", "de-DE", "tr-TR", "ar-SA", "ko-KR", "fr-CH", "th-TH"];
        foreach (var example in Program.DataExamples)
        {
            var definition = Case(example);
            var plan = Plans.FromSpec(spec, example);
            var shas = new List<string>();
            foreach (var name in cultures)
            {
                var before = CultureInfo.CurrentCulture;
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
                    shas.Add(Program.Sha(Bytes(SyntheticGenerator.Generate(definition, plan))));
                }
                finally
                {
                    CultureInfo.CurrentCulture = before;
                    CultureInfo.CurrentUICulture = before;
                }
            }

            P("culture", $"{example} cultures={cultures.Length} distinct-sha={shas.Distinct().Count()} sha={shas[0][..16]} de-DE-decimal-sep={CultureInfo.GetCultureInfo("de-DE").NumberFormat.NumberDecimalSeparator}");
        }
    }

    // 한 프로세스 안 두 번, 계획 객체를 새로 만들어도 같다. 시드를 바꾸면 다르다.
    private static void Repeat()
    {
        foreach (var example in Program.DataExamples)
        {
            var definition = Case(example);
            var a = Bytes(SyntheticGenerator.Generate(definition, Plans.FromSpec(spec, example)));
            var b = Bytes(SyntheticGenerator.Generate(definition, Plans.FromSpec(spec, example)));
            var plan = Plans.FromSpec(spec, example);
            var c = Bytes(SyntheticGenerator.Generate(definition, plan with { Seed = plan.Seed + 1 }));
            P("repeat", $"{example} same-twice={a.AsSpan().SequenceEqual(b)} seed+1-differs={!a.AsSpan().SequenceEqual(c)}");
        }
    }

    // 12절 "불일치 3건·누락 1건"을 심는 방법 비교(table-compare, 같은 시드).
    private static void Alternatives()
    {
        var definition = Case("table-compare");
        var plan = Plans.FromSpec(spec, "table-compare");
        (string Name, string[] Mismatch, int Missing, int Extra)[] alternatives =
        [
            ("P1 불일치3+누락1+추가1", ["start_index", "length", "mean_value"], 1, 1),
            ("P2 불일치2+누락1+추가1(제안)", ["length", "mean_value"], 1, 1),
            ("P3 불일치3+누락1", ["start_index", "length", "mean_value"], 1, 0),
            ("P4 불일치2+누락1", ["length", "mean_value"], 1, 0),
        ];
        foreach (var (name, mismatch, missing, extra) in alternatives)
        {
            var t = SyntheticGenerator.Generate(definition, plan with { MismatchColumns = mismatch, Missing = missing, Extra = extra });
            P("alt", $"{name}: {Oracle.Count(t, definition)}");
        }
    }

    // 시드 0~1999에서 table-compare 계획: 성공 수, 실패 이유, 성공한 것의 세기가 늘 계획과 같은지(세 변형 모두).
    private static void Seeds()
    {
        foreach (var example in Program.DataExamples)
        {
            var definition = Case(example);
            var plan = Plans.FromSpec(spec, example);
            var expected = Oracle.Count(SyntheticGenerator.Generate(definition, plan), definition).ToString();
            int ok = 0, same = 0;
            var failures = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var focusCounts = new List<int>();
            ulong? firstTwelve = null, firstAllChoices = null;
            for (ulong seed = 0; seed < 2000; seed++)
            {
                try
                {
                    var t = SyntheticGenerator.Generate(definition, plan with { Seed = seed });
                    ok++;

                    // 시드 고르기: 선택 열마다 선택지가 기준 자료에 모두 나오는 가장 작은 시드.
                    var allChoices = plan.Columns.Select((c, i) => (c, i))
                        .Where(x => x.c.Kind == SyntheticColumn.KindChoice)
                        .All(x => x.c.Choices.All(v => t.Reference.Any(r => r.Values[x.i] == v)));
                    firstAllChoices ??= allChoices ? seed : null;
                    var counts = new[] { "base", "strict", "candbase" }.Select(v => Oracle.Count(t, definition, v).ToString()).Distinct().ToArray();
                    same += counts.Length == 1 && counts[0] == expected ? 1 : 0;
                    if (plan.FocusColumn is not null)
                    {
                        var f = t.Header.ToList().IndexOf(plan.FocusColumn) - 1;
                        var n = t.Reference.Count(r => r.Values[f] == plan.FocusValue);
                        focusCounts.Add(n);

                        // 시드 고르기: 몰림 행이 12개(30행의 40%, answers.yaml 2단계 exception_rate 40)인 가장 작은 시드.
                        firstTwelve ??= n == 12 ? seed : null;
                    }
                }
                catch (ArgumentException ex)
                {
                    var key = ex.Message.Split(' ')[0];
                    failures[key] = failures.GetValueOrDefault(key) + 1;
                }
            }

            var focus = focusCounts.Count == 0 ? "" : $" focus-rows min={focusCounts.Min()} max={focusCounts.Max()} first-seed-with-12={firstTwelve}";
            focus += $" first-seed-all-choices={firstAllChoices}";
            P("seeds", $"{example} seeds=2000 ok={ok} counts-equal-plan(all-variants)={same} failures=[{string.Join(", ", failures.Select(f => $"{f.Key}×{f.Value}"))}]{focus}");
        }

        // 몰림 행이 모자라면 예외(기준 5건, irregular 4행 이상 요구).
        var d = Case("table-compare");
        var small = Plans.FromSpec(spec, "table-compare") with { Rows = 5, Within = 0, MismatchColumns = ["length", "mean_value", "start_index"], Missing = 1, Extra = 0 };
        var fails = 0;
        string? first = null;
        for (ulong seed = 0; seed < 50; seed++)
        {
            try
            {
                SyntheticGenerator.Generate(d, small with { Seed = seed });
            }
            catch (ArgumentException ex)
            {
                fails++;
                first ??= ex.Message;
            }
        }

        P("seeds", $"small-focus seeds=50 exceptions={fails} first='{first}'");
    }

    // 계획이 case.yaml과 맞지 않으면 예외.
    private static void Validation()
    {
        var d = Case("table-compare");
        var plan = Plans.FromSpec(spec, "table-compare");
        (string Name, Func<SyntheticPlan> Make)[] cases =
        [
            ("compare-field-not-in-columns", () => plan with { Columns = plan.Columns.Where(c => c.Name != "length").ToArray(), MismatchColumns = ["mean_value"] }),
            ("mismatch-on-non-compare", () => plan with { MismatchColumns = ["signal_type"] }),
            ("within-on-integer", () => plan with { WithinColumn = "length" }),
            ("pct-on-integer-column", () => plan with { Columns = plan.Columns.Select(c => c.Name == "mean_value" ? SyntheticColumn.Integer("mean_value", 10, 99) : c).ToArray() }),
            ("key-digits-too-small", () => plan with { KeyDigits = 1 }),
            ("choice-with-comma", () => plan with { Columns = plan.Columns.Select(c => c.Name == "signal_type" ? SyntheticColumn.Choice("signal_type", "a,b", "c") : c).ToArray() }),
            ("non-ascii-choice", () => plan with { Columns = plan.Columns.Select(c => c.Name == "signal_type" ? SyntheticColumn.Choice("signal_type", "정상", "irregular") : c).ToArray() }),
            ("file-name-with-folder", () => plan with { FileName = "../x.csv" }),
        ];
        foreach (var (name, make) in cases)
        {
            try
            {
                SyntheticGenerator.Generate(d, make());
                P("validate", $"{name}: 예외 없음");
            }
            catch (ArgumentException ex)
            {
                P("validate", $"{name}: {ex.GetType().Name} {ex.Message.Split(" (Parameter")[0]}");
            }
        }

        var noInputs = Case("mixed-exceptions");
        try
        {
            SyntheticGenerator.Generate(noInputs, plan);
        }
        catch (ArgumentException ex)
        {
            P("validate", $"case-without-inputs: {ex.GetType().Name} {ex.Message.Split(" (Parameter")[0]}");
        }
    }

    // 패턴 표의 잡는 예·잡지 않는 예. 그리고 생성기가 만들 수 있는 값 모양.
    private static void Patterns()
    {
        var patterns = Scanner.Load(spec);
        foreach (var p in patterns)
        {
            var hits = p.Hits.Count(h => p.Regex.IsMatch(h));
            var misses = p.Misses.Count(m => !p.Regex.IsMatch(m));
            P("pattern", $"{p.Name} hits={hits}/{p.Hits.Length} misses={misses}/{p.Misses.Length}");
        }

        // 키·값 모양이 어떤 패턴에도 걸리지 않는지: 계획의 키 전부(추가 포함 99까지), 선택지, 정수·소수 범위 끝.
        var samples = new List<string>();
        foreach (var example in Program.DataExamples)
        {
            var plan = Plans.FromSpec(spec, example);
            samples.AddRange(Enumerable.Range(1, 99).Select(i => plan.KeyPrefix + i.ToString("D2", CultureInfo.InvariantCulture)));
            foreach (var c in plan.Columns)
            {
                samples.AddRange(c.Kind == SyntheticColumn.KindChoice ? c.Choices : [c.Min.ToString(CultureInfo.InvariantCulture), c.Max.ToString(CultureInfo.InvariantCulture)]);
            }
        }

        var bad = samples.Where(s => patterns.Any(p => p.Regex.IsMatch(s))).ToArray();
        P("pattern", $"generator-shapes samples={samples.Count} hits={bad.Length} [{string.Join(" ", bad.Take(5))}]");

        // .NET 정규식의 \w는 한글을 단어 문자로 본다. 경로 패턴의 앞 조건이 한글 뒤 '/'를 막는지.
        var posix = patterns.Single(p => p.Name == "posix-path").Regex;
        P("pattern", $"posix-path 한글/뒤: '센서/로그/'={posix.IsMatch("센서/로그/")} ' /data/x'={posix.IsMatch(" /data/x")} 'reference/*.csv'={posix.IsMatch("reference/*.csv")}");
    }

    // CSV 규칙: 쉼표·따옴표·줄바꿈·비ASCII 값은 예외. 결과 바이트는 BOM·CR 없음, 끝 LF.
    private static void Csv()
    {
        foreach (var bad in new[] { "a,b", "a\"b", "a\nb", "a\rb", "가", "", "\t" })
        {
            try
            {
                SyntheticCsv.Render(["k", "v"], [new SyntheticRow("k1", [bad], "none")]);
                P("csv", $"'{Escape(bad)}': 예외 없음");
            }
            catch (ArgumentException)
            {
                P("csv", $"'{Escape(bad)}': ArgumentException");
            }
        }

        foreach (var example in Program.DataExamples)
        {
            var t = SyntheticGenerator.Generate(Case(example), Plans.FromSpec(spec, example));
            foreach (var bytes in new[] { SyntheticCsv.Render(t.Header, t.Reference), SyntheticCsv.Render(t.Header, t.Candidate) })
            {
                P("csv", $"{example} bom={bytes is [0xEF, 0xBB, 0xBF, ..]} cr={bytes.Count(b => b == '\r')} last-lf={bytes[^1] == '\n'} non-ascii={bytes.Count(b => b > 0x7E)} quote={bytes.Count(b => b == '"')}");
            }
        }
    }

    private static string Escape(string s) => s.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
}

// Core의 internal SplitMix64와 같은 식(확인용 복사). 같은 수열인지는 생성 결과 바이트가 Python 구현과 같은지로 본다.
internal sealed class SplitMix64Probe(ulong seed)
{
    private ulong state = seed;

    public ulong Next()
    {
        unchecked
        {
            state += 0x9E3779B97F4A7C15UL;
            var z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    public int Below(int bound) => (int)Math.BigMul(Next(), (ulong)bound, out _);
}
