using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Core.Synthetic;

// T-006 스파이크 도구(머지 대상 아님). 절대 경로를 찍지 않는다.
// run <케이스폴더> <출력파일>          : case.yaml → CompareDirectory → metrics.json(오류면 오류 줄 목록)을 쓴다.
// probe <예제 루트> <실험 폴더>        : 메모리 안 실험(PROBE 줄). 실험 폴더에는 glob 실험용 파일만 만든다.
// spec <spec.md> <출력 루트>           : 스펙 표·블록·SHA-256 줄을 후보 동작·출력과 대조한다.
// guard-samples <case-valid 폴더>      : GuardTargets 고정 계획(T-005 4.11)으로 reference/rows.csv, candidate/rows.csv를 만든다.
// refs <dll>                          : AssemblyRef·TypeRef 네임스페이스.
internal static class Program
{
    public static readonly string[] Examples = ["table-compare", "form-fill", "mixed-exceptions"];

    private static int Main(string[] args)
    {
        // SPIKE_KEEP_CULTURE=1이면 프로세스 문화권(LANG)을 그대로 둔다(S3 de-DE 프로세스 실험).
        if (Environment.GetEnvironmentVariable("SPIKE_KEEP_CULTURE") != "1")
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        return args switch
        {
            ["run", var dir, var output] => Run(dir, output),
            ["probe", var root, var work] => Probes.Run(root, work),
            ["spec", var spec, var root] => SpecCheck.Run(spec, root),
            ["guard-samples", var dir] => GuardSamples(dir),
            ["refs", var dll] => Refs(dll),
            _ => Usage(),
        };
    }

    private static int Usage()
    {
        Console.Error.WriteLine("사용: run|probe|spec|guard-samples|refs");
        return 2;
    }

    public static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static CaseDefinition ParseCase(string dir)
    {
        var parsed = CaseParser.ParseDirectory(dir);
        return parsed.Case ?? throw new InvalidOperationException("case.yaml 오류: " + string.Join(" | ", parsed.Errors));
    }

    // 결과 바이트: 오류가 없으면 metrics.json, 있으면 오류 줄(LF, 끝 LF).
    public static byte[] Output(CompareResult result) =>
        result.Metrics is { } m
            ? MetricsJson.Render(m)
            : Encoding.UTF8.GetBytes(string.Concat(result.Errors.Select(e => e + "\n")));

    private static int Run(string dir, string output)
    {
        var label = Path.GetFileName(Path.TrimEndingDirectorySeparator(dir));
        var definition = ParseCase(dir);
        var result = CompareEngine.CompareDirectory(dir, definition);
        var bytes = Output(result);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllBytes(output, bytes);
        Console.WriteLine($"SHA {label} {Path.GetFileName(output)} {Sha(bytes)} bytes={bytes.Length} cr={bytes.Count(b => b == '\r')} bom={(bytes is [0xEF, 0xBB, 0xBF, ..])} last-lf={bytes[^1] == '\n'} culture={CultureInfo.CurrentCulture.Name}({(1.5m).ToString(CultureInfo.CurrentCulture)})");
        if (result.Metrics is { } m)
        {
            var c = m.Counts;
            Console.WriteLine($"RESULT {label} status={m.Status} reason={m.Reason ?? "null"} key={m.Key ?? "null"} ref-files=[{string.Join(",", m.ReferenceFiles)}] cand-files=[{string.Join(",", m.CandidateFiles)}]");
            Console.WriteLine($"RESULT {label} counts={(c is null ? "null" : $"ref={c.Reference} cand={c.Candidate} match={c.Match} mismatch={c.Mismatch} missing={c.Missing} extra={c.Extra} within={c.Within}")} accuracy={Show(m.AccuracyPct)} missing-rate={Show(m.MissingRatePct)}");
            foreach (var f in m.Fields)
            {
                Console.WriteLine($"RESULT {label} field {f.Field} kind={f.Kind} tol={Show(f.Tolerance)} pct={Show(f.TolerancePct)} compared={f.Compared} outside={f.Outside} within={f.Within} not-number={f.NotNumber}");
            }

            foreach (var item in m.Items.Where(i => i.Status != CompareEngine.ItemMatch || i.Differences.Count > 0))
            {
                Console.WriteLine($"RESULT {label} item {item.Key} {item.Status} ref={Loc(item.Reference)} cand={Loc(item.Candidate)} {string.Join(" ", item.Differences.Select(d => $"{d.Field}:{d.Result}:{d.Reference}->{d.Candidate}"))}");
            }
        }
        else
        {
            foreach (var e in result.Errors)
            {
                Console.WriteLine($"ERR {e}");
            }
        }

        return 0;
    }

    public static string Loc(CsvRow? row) => row is null ? "null" : $"{row.File}:{row.Line}";

    public static string Show(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "null";

    // T-005 GuardTargets.SyntheticGuardPlan과 같은 계획.
    public static readonly SyntheticPlan GuardPlan = new(
        Seed: 7, FileName: "rows.csv", KeyPrefix: "r", KeyDigits: 2, Rows: 12,
        Columns: [SyntheticColumn.Integer("start", 0, 999), SyntheticColumn.Decimal("mean", 10m, 99.99m, 2), SyntheticColumn.Choice("label", "a", "b", "c")],
        MismatchColumns: ["start", "mean", "label"], Missing: 1, Extra: 1, WithinColumn: "mean", Within: 2,
        FocusColumn: null, FocusValue: null, ShuffleCandidate: true);

    private static int GuardSamples(string dir)
    {
        var definition = ParseCase(dir);
        var tables = SyntheticGenerator.Generate(definition, GuardPlan);
        foreach (var (folder, rows) in new[] { (SyntheticCsv.ReferenceFolder, tables.Reference), (SyntheticCsv.CandidateFolder, tables.Candidate) })
        {
            Directory.CreateDirectory(Path.Combine(dir, folder));
            var bytes = SyntheticCsv.Render(tables.Header, rows);
            File.WriteAllBytes(Path.Combine(dir, folder, tables.FileName), bytes);
            Console.WriteLine($"SHA guard {folder}/{tables.FileName} {Sha(bytes)} bytes={bytes.Length}");
        }

        foreach (var group in tables.Reference.Concat(tables.Candidate).Where(r => r.Planted != SyntheticGenerator.PlantedNone).GroupBy(r => r.Planted).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"RESULT guard planted {group.Key}: {string.Join(" ", group.Select(r => r.Key))}");
        }

        return 0;
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

// 메모리 안에서 케이스·CSV를 만들어 공개 API(CompareEngine.Compare)로 대조한다. 테스트가 쓸 수 있는 길과 같다.
internal static class Mem
{
    public static CaseDefinition Case(string compareYaml, string key = "id")
    {
        var yaml = $"name: p\ntask: t\nsteps:\n  - name: s\ninputs: {{reference: reference/*.csv, candidate: candidate/*.csv, key: {key}}}\n{compareYaml}";
        var parsed = CaseParser.Parse(Encoding.UTF8.GetBytes(yaml), "case.yaml");
        return parsed.Case ?? throw new InvalidOperationException(string.Join(" | ", parsed.Errors));
    }

    // compare 한 필드 "v". tolerance 칸 모양: "tolerance: 0", "tolerance_pct: 1.0", "없음".
    public static CaseDefinition OneField(string tolerance) =>
        Case(tolerance == "없음" ? "compare:\n  - {field: v}\n" : $"compare:\n  - {{field: v, {tolerance}}}\n");

    public static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    // RFC 4180 따옴표 칸. 안의 '"'는 '""'.
    public static string Quote(string cell) => "\"" + cell.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    public static CompareResult Run(CaseDefinition definition, string reference, string candidate) =>
        CompareEngine.Compare(definition, new CompareSources([new SourceFile("reference/a.csv", Bytes(reference))], [new SourceFile("candidate/a.csv", Bytes(candidate))]));

    public static CompareResult Run(CaseDefinition definition, IEnumerable<(string Path, byte[] Bytes)> reference, IEnumerable<(string Path, byte[] Bytes)> candidate) =>
        CompareEngine.Compare(definition, new CompareSources(
            reference.Select(f => new SourceFile(f.Path, f.Bytes)).ToArray(),
            candidate.Select(f => new SourceFile(f.Path, f.Bytes)).ToArray()));

    // 칸 하나 비교: 같음 / within / outside / not_number. 공개 API로 한 행짜리 CSV를 대조한다.
    public static string Judge(string tolerance, string reference, string candidate)
    {
        var result = Run(OneField(tolerance), $"id,v\nk,{Quote(reference)}\n", $"id,v\nk,{Quote(candidate)}\n");
        if (result.Metrics is null)
        {
            return "ERR " + string.Join(" | ", result.Errors);
        }

        var item = result.Metrics.Items.Single();
        return item.Differences.Count == 0 ? "같음" : item.Differences.Single().Result;
    }

    public static string Errors(CompareResult result) =>
        result.Metrics is null ? string.Join(" | ", result.Errors) : "errors=0";

    public static string Counts(CompareResult result) =>
        result.Metrics?.Counts is { } c
            ? $"ref={c.Reference} cand={c.Candidate} match={c.Match} mismatch={c.Mismatch} missing={c.Missing} extra={c.Extra} within={c.Within} acc={Program.Show(result.Metrics.AccuracyPct)} miss={Program.Show(result.Metrics.MissingRatePct)}"
            : result.Metrics is { } m ? $"status={m.Status} reason={m.Reason}" : "ERR " + string.Join(" | ", result.Errors);
}
