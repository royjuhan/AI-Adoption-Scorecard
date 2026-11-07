using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Core.Comparison;
using Scorecard.Core.Synthetic;

// T-009 스파이크 도구(머지 대상 아님). 현재 Core의 공개 API만 쓴다. Core를 바꾸지 않는다.
// gen <synthetic-examples.md> <예제> <case.yaml> <출력폴더> : 4.3·4.4 표의 계획 → Generate → Render. reference/·candidate/ CSV를 쓴다.
// planted <synthetic-examples.md> <예제> <case.yaml> : 심은 행(키, 선택 열 값, 심은 것, 기준 → 결과).
// seed <synthetic-examples.md> <예제> <case.yaml> <선택 열> <값> <단계 번호> : 기준 행 중 <선택 열> = <값> 수가 그 단계 batch_size인 가장 작은 시드.
// seeds <synthetic-examples.md> <예제> <case.yaml> : 시드 0~1999 생성 성공 수, 대조 셈이 하나인지.
// classify <yaml> <out.json> : 파서(이름 = 파일 이름) → 분류 → classification.json
// metrics <케이스 폴더> <out.json> : case.yaml → 대조 → metrics.json
// rows <yaml> : 단계마다 행·Used·defaults·checks·문장
var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
// SPIKE_KEEP_CULTURE=1이면 프로세스 문화권(LANG)을 그대로 둔다(결정론 확인용).
if (Environment.GetEnvironmentVariable("SPIKE_KEEP_CULTURE") != "1")
{
    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
}

switch (args.FirstOrDefault())
{
    case "gen":
        return Gen(args[1], args[2], args[3], args[4]);
    case "planted":
        return Planted(args[1], args[2], args[3]);
    case "seed":
        return Seed(args[1], args[2], args[3], args[4], args[5], int.Parse(args[6], CultureInfo.InvariantCulture));
    case "seeds":
        return Seeds(args[1], args[2], args[3]);
    case "classify":
        return Classify(args[1], args[2]);
    case "metrics":
        return Metrics(args[1], args[2]);
    case "rows":
        return Rows(args[1]);
    default:
        Console.Error.WriteLine("사용: gen|planted|seed|seeds|classify|metrics|rows");
        return 2;
}

static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

static CaseDefinition? Parse(string yamlPath)
{
    var parsed = CaseParser.Parse(File.ReadAllBytes(yamlPath), Path.GetFileName(yamlPath));
    if (!parsed.IsValid)
    {
        foreach (var e in parsed.Errors)
        {
            Console.WriteLine($"ERR {e}");
        }

        return null;
    }

    return parsed.Case!;
}

int Gen(string spec, string example, string caseYaml, string outDir)
{
    var definition = Parse(caseYaml);
    if (definition is null)
    {
        return 2;
    }

    var plan = PlanTable.Read(spec, example);
    var tables = SyntheticGenerator.Generate(definition, plan);
    foreach (var (folder, rows) in new[] { (SyntheticCsv.ReferenceFolder, tables.Reference), (SyntheticCsv.CandidateFolder, tables.Candidate) })
    {
        var bytes = SyntheticCsv.Render(tables.Header, rows);
        var dir = Path.Combine(outDir, folder);
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, tables.FileName), bytes);
        Console.WriteLine($"SHA {Sha(bytes)} {bytes.Length} {folder}/{tables.FileName} rows={rows.Count}");
    }

    return 0;
}

int Planted(string spec, string example, string caseYaml)
{
    var definition = Parse(caseYaml)!;
    var plan = PlanTable.Read(spec, example);
    var tables = SyntheticGenerator.Generate(definition, plan);
    var reference = tables.Reference.ToDictionary(r => r.Key, StringComparer.Ordinal);
    var choice = plan.Columns.Select((c, i) => (c, i)).First(x => x.c.Kind == SyntheticColumn.KindChoice);
    Console.WriteLine($"RESULT choice-column {choice.c.Name} " + string.Join(" ", choice.c.Choices.Select(v => $"{v}={tables.Reference.Count(r => r.Values[choice.i] == v)}")));
    Console.WriteLine($"RESULT {choice.c.Name}={choice.c.Choices[1]} keys " + string.Join(",", tables.Reference.Where(r => r.Values[choice.i] == choice.c.Choices[1]).Select(r => r.Key)));
    foreach (var r in tables.Reference.Where(r => r.Planted != SyntheticGenerator.PlantedNone))
    {
        Console.WriteLine($"RESULT planted {r.Key} {r.Values[choice.i]} {r.Planted} 결과에 없음");
    }

    foreach (var c in tables.Candidate.Where(r => r.Planted != SyntheticGenerator.PlantedNone))
    {
        if (c.Planted == SyntheticGenerator.PlantedExtra)
        {
            Console.WriteLine($"RESULT planted {c.Key} {c.Values[choice.i]} {c.Planted} 기준에 없음 position={tables.Candidate.ToList().IndexOf(c) + 1}");
            continue;
        }

        var name = c.Planted[(c.Planted.IndexOf(':', StringComparison.Ordinal) + 1)..];
        var i = plan.Columns.Select((col, n) => (col, n)).Single(x => x.col.Name == name).n;
        var from = reference[c.Key].Values[i];
        var to = c.Values[i];
        var pct = plan.Columns[i].Kind == SyntheticColumn.KindDecimal
            ? " " + Math.Round(Math.Abs(decimal.Parse(to, CultureInfo.InvariantCulture) - decimal.Parse(from, CultureInfo.InvariantCulture)) * 100m / decimal.Parse(from, CultureInfo.InvariantCulture), 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture) + "%"
            : "";
        Console.WriteLine($"RESULT planted {c.Key} {c.Values[choice.i]} {c.Planted} {from} -> {to}{pct}");
    }

    Console.WriteLine($"RESULT candidate-first {tables.Candidate[0].Key}");
    return 0;
}

int Seed(string spec, string example, string caseYaml, string column, string value, int stepNumber)
{
    var definition = Parse(caseYaml)!;
    var plan = PlanTable.Read(spec, example);
    var target = definition.Steps.Single(s => s.Number == stepNumber).Answers.BatchSize.Value;
    var i = plan.Columns.Select((c, n) => (c, n)).Single(x => x.c.Name == column).n;
    var found = new List<ulong>();
    for (ulong seed = 0; seed < 2000 && found.Count < 5; seed++)
    {
        var tables = SyntheticGenerator.Generate(definition, plan with { Seed = seed });
        if (tables.Reference.Count(r => r.Values[i] == value) == target)
        {
            var defects = tables.Reference.Where(r => r.Planted == SyntheticGenerator.PlantedMissing).Select(r => r.Key)
                .Concat(tables.Candidate.Where(r => r.Planted.StartsWith(SyntheticGenerator.PlantedMismatchPrefix, StringComparison.Ordinal)).Select(r => r.Key))
                .ToArray();
            var refByKey = tables.Reference.ToDictionary(r => r.Key, StringComparer.Ordinal);
            var kinds = defects.Select(k => refByKey[k].Values[i]).ToArray();
            Console.WriteLine($"RESULT seed {seed} {column}={value} count={target} defects={string.Join(",", defects.Zip(kinds, (k, v) => $"{k}:{v}"))}");
            found.Add(seed);
        }
    }

    Console.WriteLine($"RESULT smallest-seed {(found.Count > 0 ? found[0].ToString(CultureInfo.InvariantCulture) : "none")} target={target} plan-seed={plan.Seed}");
    return 0;
}

int Seeds(string spec, string example, string caseYaml)
{
    var definition = Parse(caseYaml)!;
    var plan = PlanTable.Read(spec, example);
    var ok = 0;
    var counts = new HashSet<string>(StringComparer.Ordinal);
    var failures = new List<string>();
    var flips = new Dictionary<string, int>(StringComparer.Ordinal) { ["mismatch"] = 0, ["within"] = 0 };
    var planSeedFlips = new List<string>();
    for (ulong seed = 0; seed < 2000; seed++)
    {
        try
        {
            var tables = SyntheticGenerator.Generate(definition, plan with { Seed = seed });
            var sources = new CompareSources(
                [new SourceFile($"{SyntheticCsv.ReferenceFolder}/{tables.FileName}", SyntheticCsv.Render(tables.Header, tables.Reference))],
                [new SourceFile($"{SyntheticCsv.CandidateFolder}/{tables.FileName}", SyntheticCsv.Render(tables.Header, tables.Candidate))]);
            var result = CompareEngine.Compare(definition, sources);
            var c = result.Metrics!.Counts!;
            CountFlips(definition, plan, seed, tables, flips, planSeedFlips);
            counts.Add($"{c.Reference}/{c.Candidate}/{c.Match}/{c.Mismatch}/{c.Missing}/{c.Extra}/{c.Within} {result.Metrics.AccuracyPct}/{result.Metrics.MissingRatePct}");
            ok++;
        }
        catch (ArgumentException ex)
        {
            if (failures.Count < 5)
            {
                failures.Add($"{seed}: {ex.Message}");
            }
        }
    }

    Console.WriteLine($"RESULT seeds 0..1999 ok={ok} distinct-counts={counts.Count} [{string.Join(" | ", counts)}]");
    foreach (var f in failures)
    {
        Console.WriteLine($"RESULT seed-failure {f}");
    }

    // StructureTests.Decimal_planted_values_stay_in_column_range_for_every_seed_below_2000과 같은 셈(가장 작은 배수, 양쪽 부호).
    Console.WriteLine($"RESULT decimal-flip-rows mismatch={flips["mismatch"]} within={flips["within"]} plan-seed={plan.Seed} plan-seed-flips={(planSeedFlips.Count == 0 ? "none" : string.Join(",", planSeedFlips))}");
    return 0;
}

static void CountFlips(CaseDefinition definition, SyntheticPlan plan, ulong seed, SyntheticTables tables, Dictionary<string, int> flips, List<string> planSeedFlips)
{
    var reference = tables.Reference.ToDictionary(r => r.Key, StringComparer.Ordinal);
    foreach (var row in tables.Candidate.Where(r => r.Planted.Contains(':', StringComparison.Ordinal)))
    {
        var mismatch = row.Planted.StartsWith(SyntheticGenerator.PlantedMismatchPrefix, StringComparison.Ordinal);
        var name = row.Planted[(row.Planted.IndexOf(':', StringComparison.Ordinal) + 1)..];
        var i = plan.Columns.Select((c, n) => (c, n)).Single(x => x.c.Name == name).n;
        var column = plan.Columns[i];
        if (column.Kind != SyntheticColumn.KindDecimal)
        {
            continue;
        }

        var r = decimal.Parse(reference[row.Key].Values[i], CultureInfo.InvariantCulture);
        var pct = definition.Compare.Single(f => f.Field == name).TolerancePct!.Value;
        var ratio = mismatch ? pct * 2m / 100m : pct * 1m / 1000m;
        var up = Math.Round(r * (1 + ratio), column.Scale, MidpointRounding.AwayFromZero);
        var down = Math.Round(r * (1 - ratio), column.Scale, MidpointRounding.AwayFromZero);
        if (up > column.Max || down < column.Min)
        {
            flips[mismatch ? "mismatch" : "within"]++;
            if (seed == plan.Seed)
            {
                planSeedFlips.Add(row.Key);
            }
        }
    }
}

int Classify(string yamlPath, string outPath)
{
    var definition = Parse(yamlPath);
    if (definition is null)
    {
        return 2;
    }

    var bytes = ClassificationJson.Render(StepClassifier.Classify(definition));
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
    File.WriteAllBytes(outPath, bytes);
    Console.WriteLine($"SHA {Sha(bytes)} {bytes.Length} {Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(yamlPath)))}/{Path.GetFileName(yamlPath)}");
    return 0;
}

int Metrics(string caseDirectory, string outPath)
{
    var parsed = CaseParser.ParseDirectory(caseDirectory);
    if (!parsed.IsValid)
    {
        foreach (var e in parsed.Errors)
        {
            Console.WriteLine($"ERR {e}");
        }

        return 2;
    }

    var result = CompareEngine.CompareDirectory(caseDirectory, parsed.Case!);
    if (!result.IsValid)
    {
        foreach (var e in result.Errors)
        {
            Console.WriteLine($"ERR {e}");
        }

        return 2;
    }

    var m = result.Metrics!;
    var bytes = MetricsJson.Render(m);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
    File.WriteAllBytes(outPath, bytes);
    var c = m.Counts;
    Console.WriteLine($"RESULT metrics {m.Case} status={m.Status} counts={(c is null ? "null" : $"reference={c.Reference} candidate={c.Candidate} match={c.Match} mismatch={c.Mismatch} missing={c.Missing} extra={c.Extra} within={c.Within}")} accuracy={m.AccuracyPct?.ToString(CultureInfo.InvariantCulture) ?? "null"} missing-rate={m.MissingRatePct?.ToString(CultureInfo.InvariantCulture) ?? "null"} items={m.Items.Count(i => i.Status != CompareEngine.ItemMatch || i.Differences.Count > 0)}");
    Console.WriteLine($"SHA {Sha(bytes)} {bytes.Length} {Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(caseDirectory)))}/metrics.json");
    return 0;
}

int Rows(string yamlPath)
{
    var definition = Parse(yamlPath);
    if (definition is null)
    {
        return 2;
    }

    var result = StepClassifier.Classify(definition);
    var rows = 0;
    var marked = 0;
    foreach (var s in result.Steps)
    {
        Console.WriteLine($"RESULT step {s.Number} {s.Name} defaults=[{string.Join(",", s.Defaults)}] checks=[{string.Join(",", s.Checks)}] overuse={s.AiOveruseCandidate}");
        foreach (var r in s.Rows)
        {
            rows++;
            marked += r.Reason.EndsWith("(기본값 사용, 확인 필요)", StringComparison.Ordinal) ? 1 : 0;
            Console.WriteLine($"RESULT   {r.Assignee} {r.Rule} {r.Kind} used=[{string.Join(",", r.Used)}] {r.Reason}");
        }
    }

    var all = result.Steps.SelectMany(s => s.Rows).ToArray();
    Console.WriteLine(
        $"RESULT summary steps={result.Steps.Count} rows={rows} ②={all.Count(r => r.Assignee == "②")} ③={all.Count(r => r.Assignee == "③")} " +
        $"exception_review={all.Count(r => r.Kind == StepClassifier.KindExceptionReview)} human_gate={all.Count(r => r.Kind == StepClassifier.KindHumanGate)} " +
        $"signoff={all.Count(r => r.Rule == "SIGNOFF")} measure={result.Steps.Count(s => s.Checks.Contains("measure_minutes_per_item"))} " +
        $"confirm={result.Steps.Count(s => s.Checks.Contains("confirm_existing_feature"))} overuse={result.Steps.Count(s => s.AiOveruseCandidate)} default-marked={marked}");
    return 0;
}

// synthetic-examples.md 4.3 계획 표·4.4 열 표를 읽는다. 칸 읽기는 테스트 도우미 SyntheticSpec.Plan과 같은 규칙(4.3 "칸 읽기").
internal static partial class PlanTable
{
    private const string PlanHeader =
        "| 예제 | 시드 | 파일 | 키 접두어 | 키 자릿수 | 기준 건수 | 값 불일치 열 | 누락 | 추가 | 허용오차 안 차이 | 몰림 | 결과 섞기 |";

    private const string ColumnHeader = "| 예제 | 열 | 종류 | 값 |";

    public static SyntheticPlan Read(string spec, string example)
    {
        var lines = File.ReadAllLines(spec);
        var c = Table(lines, PlanHeader).Single(r => Unquote(r[0]) == example);
        var columns = Table(lines, ColumnHeader).Where(r => Unquote(r[0]) == example).Select(r => Column(Unquote(r[1]), r[2], r[3])).ToArray();
        string? withinColumn = null;
        var within = 0;
        if (c[9] != "없음")
        {
            var m = WithinCell().Match(c[9]);
            (withinColumn, within) = (m.Groups["column"].Value, int.Parse(m.Groups["count"].Value, CultureInfo.InvariantCulture));
        }

        string? focusColumn = null, focusValue = null;
        if (c[10] != "없음")
        {
            var m = FocusCell().Match(c[10]);
            (focusColumn, focusValue) = (m.Groups["column"].Value, m.Groups["value"].Value);
        }

        return new SyntheticPlan(
            ulong.Parse(Unquote(c[1]), CultureInfo.InvariantCulture),
            Unquote(c[2]),
            Unquote(c[3]),
            Int(c[4]),
            Int(c[5]),
            columns,
            c[6] == "없음" ? [] : Spans(c[6]),
            Int(c[7]),
            Int(c[8]),
            withinColumn,
            within,
            focusColumn,
            focusValue,
            c[11] == "예");
    }

    private static SyntheticColumn Column(string name, string kind, string values)
    {
        var s = Spans(values);
        return kind switch
        {
            "정수" => SyntheticColumn.Integer(name, int.Parse(s[0], CultureInfo.InvariantCulture), int.Parse(s[1], CultureInfo.InvariantCulture)),
            "소수" => SyntheticColumn.Decimal(name, decimal.Parse(s[0], CultureInfo.InvariantCulture), decimal.Parse(s[1], CultureInfo.InvariantCulture), s[0].Length - s[0].IndexOf('.', StringComparison.Ordinal) - 1),
            "선택" => SyntheticColumn.Choice(name, s),
            _ => throw new InvalidOperationException(kind),
        };
    }

    private static List<string[]> Table(string[] lines, string header)
    {
        var start = Enumerable.Range(0, lines.Length).Single(i => lines[i].Trim() == header);
        var rows = new List<string[]>();
        for (var i = start + 1; i < lines.Length && lines[i].TrimStart().StartsWith('|'); i++)
        {
            var t = lines[i].Trim();
            var cells = t[1..^1].Split('|').Select(x => x.Trim()).ToArray();
            if (cells.All(x => x.Length > 0 && x.All(ch => ch is '-' or ':')))
            {
                continue;
            }

            rows.Add(cells);
        }

        return rows;
    }

    private static string[] Spans(string cell) => cell.Split('`').Where((_, i) => i % 2 == 1).ToArray();

    private static string Unquote(string cell) => cell.Trim().Trim('`');

    private static int Int(string cell) => int.Parse(Unquote(cell), CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^`(?<column>[^`]+)` (?<count>[0-9]+)\z", RegexOptions.CultureInvariant)]
    private static partial Regex WithinCell();

    [GeneratedRegex(@"^`(?<column>[^`]+)` = `(?<value>[^`]+)`\z", RegexOptions.CultureInvariant)]
    private static partial Regex FocusCell();
}
