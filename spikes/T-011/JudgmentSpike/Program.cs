// T-011 스파이크 도구(머지 대상 아님). 후보 Core(Scorecard.Core.Judgment)의 공개 API만 쓴다.
// 명령:
//   judge <케이스폴더> <출력 judgment.json>         예제 판정.
//   spec <judgment.md>                              스펙 표를 읽어 후보와 대조(S8). 표마다 PROBE spec 줄.
//   probe                                           표 밖 실험(PROBE 줄).
//   culture <케이스폴더>...                          문화권 7종에서 judgment.json SHA-256.
// 표준 출력 줄 머리: RESULT, PROBE, DIFF, ERR, SHA. 네트워크·외부 프로세스 없음.
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;

return args[0] switch
{
    "judge" => Tool.JudgeCase(args[1], args[2]),
    "spec" => Tool.Spec(args[1]),
    "probe" => Tool.Probe(),
    "culture" => Tool.Culture(args[1..]),
    _ => 2,
};

internal static class Tool
{
    private static readonly UTF8Encoding Utf8 = new(false);
    private static int diffs;

    // ---------- judge ----------
    public static int JudgeCase(string caseDir, string output)
    {
        var parsed = CaseParser.ParseDirectory(caseDir);
        if (parsed.Case is not { } definition)
        {
            foreach (var e in parsed.Errors)
            {
                Console.WriteLine($"ERR {e}");
            }

            return 2;
        }

        var compared = CompareEngine.CompareDirectory(caseDir, definition);
        if (compared.Metrics is not { } metrics)
        {
            foreach (var e in compared.Errors)
            {
                Console.WriteLine($"ERR {e}");
            }

            return 2;
        }

        var result = JudgmentEngine.Judge(definition, metrics);
        var bytes = JudgmentJson.Render(result);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllBytes(output, bytes);
        Console.WriteLine(
            $"RESULT {result.Case} verdict={result.Verdict} rule={result.Rule} " +
            $"checks={string.Join(",", result.Checks.Select(c => $"{c.Rule}:{c.Result}"))} todo={string.Join(",", result.Todo.Select(t => t.Key))} " +
            $"defaults={string.Join(",", result.Defaults)} sha256={Convert.ToHexStringLower(SHA256.HashData(bytes))} bytes={bytes.Length}");
        Console.WriteLine($"RESULT {result.Case} reason={result.Reason}");
        foreach (var c in result.Checks)
        {
            Console.WriteLine($"RESULT {result.Case} check {c.Rule} {c.Result}: {c.Reason}");
        }

        return 0;
    }

    // ---------- culture ----------
    public static int Culture(string[] caseDirs)
    {
        string[] cultures = ["", "de-DE", "tr-TR", "ar-SA", "ko-KR", "fr-CH", "th-TH"];
        var before = CultureInfo.CurrentCulture;
        try
        {
            foreach (var dir in caseDirs)
            {
                var definition = CaseParser.ParseDirectory(dir).Case!;
                var metrics = CompareEngine.CompareDirectory(dir, definition).Metrics!;
                var hashes = new HashSet<string>();
                foreach (var name in cultures)
                {
                    CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
                    hashes.Add(Convert.ToHexStringLower(SHA256.HashData(JudgmentJson.Render(JudgmentEngine.Judge(definition, metrics)))));
                }

                CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = before;
                Console.WriteLine($"PROBE culture {definition.Name} cultures={cultures.Length} distinct-sha256={hashes.Count}");
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = before;
        }

        return 0;
    }

    // ---------- 장면 만들기 ----------
    // 기준 자료: id, grp, v(HasType이 false면 id, v). groups[i]가 i번째 행의 유형 값. mismatch 행은 결과 v=2, missing 행은 결과에 없다.
    internal sealed record Scn(
        string[] Groups, int[] Mismatch, int[] Missing, bool HasType,
        decimal? AccMin, decimal? MissMax, bool? Tmd, JudgmentTimes? Times, string[]? Unexplained,
        string[]? ExtraGroups = null);

    internal static string Key(int i) => $"k{i:D4}";

    private static string N(decimal d) => d.ToString(CultureInfo.InvariantCulture);

    internal static CaseDefinition Definition(Scn s, bool inputs = true, bool compare = true)
    {
        var y = new StringBuilder("name: probe\ntask: t\nsteps:\n  - name: s\n");
        if (inputs)
        {
            y.Append("inputs: {reference: reference/a.csv, candidate: candidate/a.csv, key: id}\n");
        }

        if (compare)
        {
            y.Append("compare:\n  - {field: v, tolerance: 0}\n");
        }

        if (s.AccMin is not null || s.MissMax is not null || s.Tmd is not null)
        {
            y.Append("thresholds:\n");
            if (s.AccMin is { } a)
            {
                y.Append("  accuracy_min: ").Append(N(a)).Append('\n');
            }

            if (s.MissMax is { } m)
            {
                y.Append("  missing_max: ").Append(N(m)).Append('\n');
            }

            if (s.Tmd is { } t)
            {
                y.Append("  time_must_drop: ").Append(t ? "true" : "false").Append('\n');
            }
        }

        var parsed = CaseParser.Parse(Utf8.GetBytes(y.ToString()), "case.yaml");
        return parsed.Case ?? throw new InvalidOperationException(string.Join("; ", parsed.Errors) + "\n" + y);
    }

    internal static (CaseDefinition Def, CompareMetrics Metrics) Build(Scn s, bool inputs = true, bool compare = true)
    {
        var definition = Definition(s, inputs, compare);
        var header = s.HasType ? "id,grp,v\n" : "id,v\n";
        string G(string g) => s.HasType ? g + "," : string.Empty;
        var reference = new StringBuilder(header);
        var candidate = new StringBuilder(header);
        for (var i = 0; i < s.Groups.Length; i++)
        {
            reference.Append(Key(i)).Append(',').Append(G(s.Groups[i])).Append("1\n");
            if (!s.Missing.Contains(i))
            {
                candidate.Append(Key(i)).Append(',').Append(G(s.Groups[i])).Append(s.Mismatch.Contains(i) ? "2\n" : "1\n");
            }
        }

        for (var j = 0; j < (s.ExtraGroups?.Length ?? 0); j++)
        {
            candidate.Append($"x{j:D3},").Append(G(s.ExtraGroups![j])).Append("1\n");
        }

        var result = CompareEngine.Compare(definition, new CompareSources(
            [new SourceFile("reference/a.csv", Utf8.GetBytes(reference.ToString()))],
            [new SourceFile("candidate/a.csv", Utf8.GetBytes(candidate.ToString()))]));
        return (definition, result.Metrics ?? throw new InvalidOperationException(string.Join("; ", result.Errors)));
    }

    internal static JudgmentResult Run(Scn s, bool inputs = true, bool compare = true)
    {
        var (d, m) = Build(s, inputs, compare);
        return JudgmentEngine.Judge(d, m, s.Times, s.Unexplained);
    }

    // 원하는 확인 결과 다섯으로 장면을 만든다(10행, 유형 a×5·b×5).
    internal static Scn Combo(string a, string m, string u, string t, string f)
    {
        var groups = f == "one_value" ? Enumerable.Repeat("a", 10).ToArray() : [.. Enumerable.Repeat("a", 5), .. Enumerable.Repeat("b", 5)];
        int[] bad = f switch
        {
            "few" => u == "none" ? [] : [0],
            "found" => [0, 1],
            "spread" or "off" => [0, 5],
            "one_value" => [0, 1],
            _ => throw new ArgumentException(f),
        };
        int[] missing = m == "not_met" ? [bad[0]] : bad.Length >= 2 ? [bad[1]] : [];
        var mismatch = bad.Except(missing).ToArray();
        var (tmd, times) = t switch
        {
            "off" => (false, (JudgmentTimes?)null),
            "not_measured" => (true, null),
            "met" => (true, new JudgmentTimes(10m, 1m, 1m)),
            "not_met" => (true, new JudgmentTimes(1m, 1m, 1m)),
            _ => throw new ArgumentException(t),
        };
        string[]? unexplained = u switch
        {
            "none" or "not_reviewed" => null,
            "explained" => [],
            "found" => [Key(bad[0])],
            _ => throw new ArgumentException(u),
        };
        return new Scn(groups, mismatch, missing, f != "off", a == "met" ? 0m : 100m, m == "met" ? 100m : 0m, tmd, times, unexplained);
    }

    // ---------- 스펙 대조(S8) ----------
    public static int Spec(string doc)
    {
        var lines = File.ReadAllLines(doc);
        SpecDefaults(lines);
        SpecNumbers(lines);
        SpecVerdict(lines);
        SpecChecks(lines);
        SpecReads(lines);
        SpecTodo(lines);
        SpecBoundary(lines);
        SpecTime(lines);
        SpecFocus(lines);
        SpecFocusColumns(lines);
        SpecUnexplained(lines);
        SpecPriority(lines);
        Console.WriteLine($"PROBE spec total-diff={diffs}");
        return 0;
    }

    private static void Expect(string table, string row, string what, string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            diffs++;
            Console.WriteLine($"DIFF {table} [{row}] {what}: expected={expected} actual={actual}");
        }
    }

    private static string U(string cell) => cell.Trim().Trim('`');

    private static string Fill(string template, Dictionary<string, string> values) =>
        Regex.Replace(U(template), @"\{([a-z_]+)\}", m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : throw new KeyNotFoundException(m.Value));

    // 장면에서 문장 틀 값을 따로 센다(Core 결과를 쓰지 않는다. 퍼센트만 대조 결과 값을 쓴다).
    private static Dictionary<string, string> Values(Scn s, CompareMetrics metrics, JudgmentThresholds th)
    {
        var bad = s.Mismatch.Concat(s.Missing).ToArray();
        var badGroups = bad.Select(i => s.Groups[i]).ToArray();
        var distinct = badGroups.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        string Show(string v) => v.Length == 0 ? "(빈 칸)" : v;
        var v = new Dictionary<string, string>
        {
            ["accuracy_pct"] = N(metrics.AccuracyPct ?? 0),
            ["missing_rate_pct"] = N(metrics.MissingRatePct ?? 0),
            ["match"] = (s.Groups.Length - bad.Length).ToString(CultureInfo.InvariantCulture),
            ["missing"] = s.Missing.Length.ToString(CultureInfo.InvariantCulture),
            ["reference"] = s.Groups.Length.ToString(CultureInfo.InvariantCulture),
            ["accuracy_min"] = N(th.AccuracyMin),
            ["missing_max"] = N(th.MissingMax),
            ["bad"] = bad.Length.ToString(CultureInfo.InvariantCulture),
            ["unexplained"] = (s.Unexplained?.Length ?? 0).ToString(CultureInfo.InvariantCulture),
            ["group"] = "grp",
            ["value"] = distinct.Length == 1 ? Show(distinct[0]) : "-",
            ["rows_in"] = distinct.Length == 1 ? s.Groups.Count(g => g == distinct[0]).ToString(CultureInfo.InvariantCulture) : "-",
            ["rows_out"] = distinct.Length == 1 ? s.Groups.Count(g => g != distinct[0]).ToString(CultureInfo.InvariantCulture) : "-",
            ["columns"] = "grp",
        };
        if (s.Times is { } t)
        {
            v["process"] = N(t.ProcessMinutes);
            v["review"] = N(t.ReviewMinutes);
            v["total"] = N(t.ProcessMinutes + t.ReviewMinutes);
            v["before"] = N(t.BeforeMinutes);
        }

        return v;
    }

    private static IReadOnlyList<string[]> Table(string[] lines, string header)
    {
        var starts = Enumerable.Range(0, lines.Length).Where(i => lines[i].Trim() == header).ToArray();
        if (starts.Length != 1)
        {
            diffs++;
            Console.WriteLine($"DIFF table '{header}' appears {starts.Length} times");
            return [];
        }

        var rows = new List<string[]>();
        for (var i = starts[0] + 1; i < lines.Length && lines[i].TrimStart().StartsWith('|'); i++)
        {
            var cells = lines[i].Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            if (cells.All(c => c.Length > 0 && c.All(ch => ch is '-' or ':')))
            {
                continue;
            }

            rows.Add(cells);
        }

        return rows;
    }

    private const string DefaultMark = " (기본값 사용, 확인 필요)";

    private static void SpecDefaults(string[] lines)
    {
        var rows = Table(lines, "| 키 | 기본값 | 근거 |");
        var r = Run(Combo("met", "met", "none", "met", "few") with { AccMin = null, MissMax = null, Tmd = null });
        var expected = rows.ToDictionary(c => U(c[0]), c => U(c[1]));
        Expect("defaults", "accuracy_min", "value", expected["accuracy_min"], N(r.Thresholds.AccuracyMin));
        Expect("defaults", "missing_max", "value", expected["missing_max"], N(r.Thresholds.MissingMax));
        Expect("defaults", "time_must_drop", "value", expected["time_must_drop"], r.Thresholds.TimeMustDrop ? "true" : "false");
        Expect("defaults", "order", "defaults", string.Join(",", rows.Select(c => U(c[0]))), string.Join(",", r.Defaults));
        var explicitRun = Run(Combo("met", "met", "none", "met", "few") with { AccMin = 95m, MissMax = 2m, Tmd = true });
        Expect("defaults", "explicit", "defaults", string.Empty, string.Join(",", explicitRun.Defaults));
        Console.WriteLine($"PROBE spec defaults rows={rows.Count}");
    }

    private static void SpecNumbers(string[] lines)
    {
        var rows = Table(lines, "| 이름 | 값 | 뜻 |");
        var min = int.Parse(U(Assert1(rows, "focus_min")[1]), CultureInfo.InvariantCulture);
        var groups = Enumerable.Repeat("a", 10).Concat(Enumerable.Repeat("b", 10)).ToArray();
        string FocusOf(int bad) => Run(new Scn(groups, Enumerable.Range(0, bad).ToArray(), [], true, 0m, 100m, false, null, null))
            .Checks.Single(c => c.Rule == "FOCUS").Result;
        Expect("numbers", "focus_min", $"bad={min - 1}", "few", FocusOf(min - 1));
        Expect("numbers", "focus_min", $"bad={min}", "found", FocusOf(min));
        Console.WriteLine($"PROBE spec numbers rows={rows.Count} focus_min={min}");
    }

    private static string[] Assert1(IReadOnlyList<string[]> rows, string key)
    {
        var found = rows.Where(c => U(c[0]) == key).ToArray();
        if (found.Length != 1)
        {
            diffs++;
            Console.WriteLine($"DIFF row '{key}' appears {found.Length} times");
            return ["", "0", ""];
        }

        return found[0];
    }

    // 판정 표 행 → 그 행이 걸리는 장면.
    private static readonly Dictionary<(string, string), Scn> VerdictScenes = new()
    {
        [("ACCURACY", "not_met")] = Combo("not_met", "met", "not_reviewed", "off", "spread"),
        [("MISSING", "not_met")] = Combo("met", "not_met", "not_reviewed", "off", "spread"),
        [("UNEXPLAINED", "found")] = Combo("met", "met", "found", "off", "spread"),
        [("TIME", "not_measured")] = Combo("met", "met", "not_reviewed", "not_measured", "spread"),
        [("TIME", "not_met")] = Combo("met", "met", "not_reviewed", "not_met", "spread"),
        [("FOCUS", "found")] = Combo("met", "met", "not_reviewed", "off", "found"),
        [("PASS", "—")] = Combo("met", "met", "none", "off", "few"),
    };

    private static readonly Scn NoData = new(["a"], [], [], false, 95m, 2m, true, null, null);

    private static void SpecVerdict(string[] lines)
    {
        var rows = Table(lines, "| 순서 | 근거 키 | 걸리는 결과 | 판정 | 판정 문장 틀 |");
        var order = new List<string>();
        foreach (var c in rows)
        {
            var (rule, result, verdict) = (U(c[1]), U(c[2]), U(c[3]));
            order.Add(rule);
            var id = $"{rule}/{result}";
            if (rule == "NOT_VERIFIED")
            {
                var r = Run(NoData, inputs: result != "no_inputs", compare: false);
                Expect("verdict", id, "verdict", verdict, r.Verdict);
                Expect("verdict", id, "rule", rule, r.Rule);
                Expect("verdict", id, "reason", U(c[4]), r.Reason);
                Expect("verdict", id, "checks", "0", r.Checks.Count.ToString(CultureInfo.InvariantCulture));
                continue;
            }

            var s = VerdictScenes[(rule, result)];
            var (d, m) = Build(s);
            var j = JudgmentEngine.Judge(d, m, s.Times, s.Unexplained);
            Expect("verdict", id, "verdict", verdict, j.Verdict);
            Expect("verdict", id, "rule", rule, j.Rule);
            Expect("verdict", id, "reason", Fill(c[4], Values(s, m, j.Thresholds)), j.Reason);
        }

        Expect("verdict", "order", "rules", "NOT_VERIFIED,NOT_VERIFIED,ACCURACY,MISSING,UNEXPLAINED,TIME,TIME,FOCUS,PASS", string.Join(",", order));
        Console.WriteLine($"PROBE spec verdict rows={rows.Count}");
    }

    private static Scn CheckScene(string check, string result) => (check, result) switch
    {
        ("ACCURACY", _) => Combo(result, "met", "not_reviewed", "off", "spread"),
        ("MISSING", _) => Combo("met", result, "not_reviewed", "off", "spread"),
        ("UNEXPLAINED", "none") => Combo("met", "met", "none", "off", "few"),
        ("UNEXPLAINED", _) => Combo("met", "met", result, "off", "spread"),
        ("TIME", _) => Combo("met", "met", "not_reviewed", result, "spread"),
        ("FOCUS", "few") => Combo("met", "met", "not_reviewed", "off", "few"),
        ("FOCUS", _) => Combo("met", "met", "not_reviewed", "off", result),
        _ => throw new ArgumentException(check),
    };

    private static void SpecChecks(string[] lines)
    {
        var rows = Table(lines, "| 확인 | 결과 | 조건 | 확인 문장 틀 |");
        var seen = new List<string>();
        foreach (var c in rows)
        {
            var (check, result) = (U(c[0]), U(c[1]));
            seen.Add(check);
            var s = CheckScene(check, result);
            var (d, m) = Build(s);
            var j = JudgmentEngine.Judge(d, m, s.Times, s.Unexplained);
            var got = j.Checks.Single(x => x.Rule == check);
            Expect("check", $"{check}/{result}", "result", result, got.Result);
            Expect("check", $"{check}/{result}", "reason", Fill(c[3], Values(s, m, j.Thresholds)), got.Reason);
        }

        var r0 = Run(Combo("met", "met", "none", "off", "few"));
        Expect("check", "order", "rules", string.Join(",", seen.Distinct()), string.Join(",", r0.Checks.Select(x => x.Rule)));
        Console.WriteLine($"PROBE spec checks rows={rows.Count}");
    }

    // 읽은 문턱 표: 기본값 하나씩 바꿔도 같은 판정이 되도록 100행 장면(문턱 = 기본값과 같은 값)을 쓴다.
    private static Scn Robust(string rule)
    {
        var groups = Enumerable.Repeat("a", 50).Concat(Enumerable.Repeat("b", 50)).ToArray();
        var met = new JudgmentTimes(10m, 1m, 1m);
        return rule switch
        {
            "NOT_VERIFIED" => NoData,
            "ACCURACY" => new Scn(groups, [0, 50, 1, 51, 2], [52], true, 95m, 2m, true, met, null),
            "MISSING" => new Scn(groups, [], [0, 50, 1], true, 95m, 2m, true, met, null),
            "UNEXPLAINED" => new Scn(groups, [0, 50], [], true, 95m, 2m, true, met, [Key(0)]),
            "TIME" => new Scn(groups, [0, 50], [], true, 95m, 2m, true, null, null),
            "FOCUS" => new Scn(groups, [0, 1], [], true, 95m, 2m, true, met, null),
            "PASS" => new Scn(groups, [0, 50], [], true, 95m, 2m, true, met, null),
            _ => throw new ArgumentException(rule),
        };
    }

    private static void SpecReads(string[] lines)
    {
        var rows = Table(lines, "| 근거 키 | 읽은 문턱 |");
        string[] keys = ["accuracy_min", "missing_max", "time_must_drop"];
        foreach (var c in rows)
        {
            var rule = U(c[0]);
            var read = U(c[1]) == "—" ? [] : c[1].Split(',').Select(U).ToArray();
            var s = Robust(rule);
            foreach (var key in keys)
            {
                var variant = key switch
                {
                    "accuracy_min" => s with { AccMin = null },
                    "missing_max" => s with { MissMax = null },
                    _ => s with { Tmd = null },
                };
                var j = Run(variant, inputs: rule != "NOT_VERIFIED", compare: rule != "NOT_VERIFIED");
                Expect("reads", $"{rule}/{key}", "rule", rule, j.Rule);
                Expect("reads", $"{rule}/{key}", "mark", read.Contains(key) ? "yes" : "no", j.Reason.EndsWith(DefaultMark, StringComparison.Ordinal) ? "yes" : "no");
                if (rule != "NOT_VERIFIED")
                {
                    // 확인 문장의 표시: 그 확인이 쓰는 문턱이 기본값일 때만.
                    var owner = key switch { "accuracy_min" => "ACCURACY", "missing_max" => "MISSING", _ => "TIME" };
                    foreach (var check in j.Checks)
                    {
                        Expect("reads", $"{rule}/{key}/{check.Rule}", "check-mark", check.Rule == owner ? "yes" : "no", check.Reason.EndsWith(DefaultMark, StringComparison.Ordinal) ? "yes" : "no");
                    }
                }
            }

            var plain = Run(s, inputs: rule != "NOT_VERIFIED", compare: rule != "NOT_VERIFIED");
            Expect("reads", rule, "no-default-mark", "no", plain.Reason.Contains(DefaultMark, StringComparison.Ordinal) ? "yes" : "no");
        }

        Console.WriteLine($"PROBE spec reads rows={rows.Count}");
    }

    private static void SpecTodo(string[] lines)
    {
        var rows = Table(lines, "| 다음 할 일 | 언제 | 문구 |");
        var table = rows.Select(c => U(c[0])).ToArray();
        foreach (var c in rows)
        {
            var key = U(c[0]);
            Scn s;
            JudgmentResult j;
            CompareMetrics? m = null;
            switch (key)
            {
                case "add_data":
                    s = NoData;
                    j = Run(s, inputs: false, compare: false);
                    break;
                case "add_compare":
                    s = NoData;
                    j = Run(s, inputs: true, compare: false);
                    break;
                case "measure_time":
                    s = Combo("met", "met", "not_reviewed", "not_measured", "spread");
                    (j, m) = RunM(s);
                    break;
                case "review_mismatches":
                    s = Combo("met", "met", "not_reviewed", "off", "spread");
                    (j, m) = RunM(s);
                    break;
                case "add_type_column":
                    s = Combo("met", "met", "not_reviewed", "off", "off");
                    (j, m) = RunM(s);
                    break;
                case "confirm_thresholds":
                    s = Combo("met", "met", "none", "met", "few") with { AccMin = null, MissMax = null, Tmd = null };
                    (j, m) = RunM(s);
                    break;
                default:
                    diffs++;
                    Console.WriteLine($"DIFF todo unknown key {key}");
                    continue;
            }

            var got = j.Todo.Where(t => t.Key == key).ToArray();
            Expect("todo", key, "present", "1", got.Length.ToString(CultureInfo.InvariantCulture));
            if (got.Length == 1)
            {
                var values = m is null ? new Dictionary<string, string>() : Values(s, m, j.Thresholds);
                values["defaults"] = "accuracy_min 95, missing_max 2, time_must_drop true";
                Expect("todo", key, "text", Fill(c[2], values), got[0].Text);
            }

            // 순서는 표 순서다.
            var order = j.Todo.Select(t => Array.IndexOf(table, t.Key)).ToArray();
            Expect("todo", key, "order", "sorted", order.SequenceEqual(order.Order()) && !order.Contains(-1) ? "sorted" : string.Join(",", j.Todo.Select(t => t.Key)));
        }

        var none = Run(Combo("met", "met", "none", "met", "few"));
        Expect("todo", "pass-no-todo", "todo", string.Empty, string.Join(",", none.Todo.Select(t => t.Key)));
        var mixed = Run(NoData with { AccMin = null, MissMax = null, Tmd = null }, inputs: false, compare: false);
        Expect("todo", "no-inputs-defaults", "todo", "add_data,add_compare,measure_time,confirm_thresholds", string.Join(",", mixed.Todo.Select(t => t.Key)));
        var noTime = Run(NoData with { Tmd = false }, inputs: false, compare: false);
        Expect("todo", "no-inputs-time-off", "todo", "add_data,add_compare", string.Join(",", noTime.Todo.Select(t => t.Key)));
        Console.WriteLine($"PROBE spec todo rows={rows.Count}");
    }

    private static (JudgmentResult, CompareMetrics) RunM(Scn s)
    {
        var (d, m) = Build(s);
        return (JudgmentEngine.Judge(d, m, s.Times, s.Unexplained), m);
    }

    private static void SpecBoundary(string[] lines)
    {
        var rows = Table(lines, "| 기준 행 | 일치 | 누락 | 표시 정확도(%) | accuracy_min | ACCURACY | 표시 누락률(%) | missing_max | MISSING |");
        foreach (var c in rows)
        {
            var (n, match, miss) = (int.Parse(U(c[0]), CultureInfo.InvariantCulture), int.Parse(U(c[1]), CultureInfo.InvariantCulture), int.Parse(U(c[2]), CultureInfo.InvariantCulture));
            var s = new Scn(Enumerable.Repeat("a", n).ToArray(), Enumerable.Range(miss, n - match - miss).ToArray(), Enumerable.Range(0, miss).ToArray(), false,
                decimal.Parse(U(c[4]), CultureInfo.InvariantCulture), decimal.Parse(U(c[7]), CultureInfo.InvariantCulture), false, null, null);
            var (j, m) = RunM(s);
            var id = string.Join(" ", c.Take(3));
            Expect("boundary", id, "accuracy_pct", U(c[3]), N(m.AccuracyPct!.Value));
            Expect("boundary", id, "ACCURACY", U(c[5]), j.Checks[0].Result);
            Expect("boundary", id, "missing_rate_pct", U(c[6]), N(m.MissingRatePct!.Value));
            Expect("boundary", id, "MISSING", U(c[8]), j.Checks[1].Result);
        }

        Console.WriteLine($"PROBE spec boundary rows={rows.Count}");
    }

    private static void SpecTime(string[] lines)
    {
        var rows = Table(lines, "| time_must_drop | 적용 전 | 처리 | 검토 | TIME |");
        foreach (var c in rows)
        {
            var tmd = U(c[0]) == "true";
            JudgmentTimes? times = U(c[1]) == "없음" ? null : new JudgmentTimes(
                decimal.Parse(U(c[1]), CultureInfo.InvariantCulture), decimal.Parse(U(c[2]), CultureInfo.InvariantCulture), decimal.Parse(U(c[3]), CultureInfo.InvariantCulture));
            var s = new Scn(["a", "b"], [], [], false, 0m, 100m, tmd, times, null);
            var j = Run(s);
            Expect("time", string.Join(" ", c.Take(4)), "TIME", U(c[4]), j.Checks.Single(x => x.Rule == "TIME").Result);
        }

        Console.WriteLine($"PROBE spec time rows={rows.Count}");
    }

    // "a×5, b×5" → [a,a,a,a,a,b,...]. "(빈 칸)"은 빈 값.
    private static string[] Groups(string cell) => U(cell) == "(없음)" ? [] : cell.Split(',').Select(U).SelectMany(t =>
    {
        var parts = t.Split('×');
        var value = parts[0] == "(빈 칸)" ? string.Empty : parts[0];
        return Enumerable.Repeat(value, parts.Length == 2 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 1);
    }).ToArray();

    private static void SpecFocus(string[] lines)
    {
        var rows = Table(lines, "| 기준 행 유형 | 불일치 행 유형 | 추가 행 유형 | 유형 열 | FOCUS |");
        foreach (var c in rows)
        {
            var groups = Groups(c[0]);
            var badGroups = Groups(c[1]);
            var used = new HashSet<int>();
            var bad = new List<int>();
            foreach (var g in badGroups)
            {
                var i = Enumerable.Range(0, groups.Length).First(k => groups[k] == g && !used.Contains(k));
                used.Add(i);
                bad.Add(i);
            }

            // 불일치는 값 불일치·누락을 번갈아 둔다.
            var mismatch = bad.Where((_, k) => k % 2 == 0).ToArray();
            var missing = bad.Where((_, k) => k % 2 == 1).ToArray();
            var s = new Scn(groups, mismatch, missing, U(c[3]) == "있음", 0m, 100m, false, null, null, Groups(c[2]));
            var j = Run(s);
            Expect("focus", string.Join(" / ", c.Take(4)), "FOCUS", U(c[4]), j.Checks.Single(x => x.Rule == "FOCUS").Result);
        }

        Console.WriteLine($"PROBE spec focus rows={rows.Count}");
    }

    // 여러 유형 열. "p=aaabbb"는 열 p의 행 1~6 값(글자 하나가 한 행, "_"는 빈 칸). 불일치 행은 1부터.
    private static void SpecFocusColumns(string[] lines)
    {
        var rows = Table(lines, "| 열 값(행 1~6) | 불일치 행 | FOCUS | 걸린 열 | 본 열 |");
        foreach (var c in rows)
        {
            var columns = c[0].Split(',').Select(U).Select(t => (Name: t.Split('=')[0], Values: t.Split('=')[1].Select(ch => ch == '_' ? string.Empty : ch.ToString()).ToArray())).ToArray();
            var bad = U(c[1]).Split(',').Select(x => int.Parse(x, CultureInfo.InvariantCulture) - 1).ToArray();
            var header = "id," + string.Concat(columns.Select(x => x.Name + ",")) + "v\n";
            var reference = new StringBuilder(header);
            var candidate = new StringBuilder("id,v\n");
            for (var i = 0; i < 6; i++)
            {
                reference.Append(Key(i)).Append(',').Append(string.Concat(columns.Select(x => x.Values[i] + ","))).Append("1\n");
                if (Array.IndexOf(bad, i) is var k && k >= 0 && k % 2 == 1)
                {
                    continue; // 홀수 번째 불일치는 누락
                }

                candidate.Append(Key(i)).Append(bad.Contains(i) ? ",2\n" : ",1\n");
            }

            var definition = Definition(new Scn([], [], [], false, 0m, 100m, false, null, null));
            var metrics = CompareEngine.Compare(definition, new CompareSources(
                [new SourceFile("reference/a.csv", Utf8.GetBytes(reference.ToString()))],
                [new SourceFile("candidate/a.csv", Utf8.GetBytes(candidate.ToString()))])).Metrics!;
            var focus = JudgmentEngine.Judge(definition, metrics).Checks.Single(x => x.Rule == "FOCUS");
            var id = string.Join(" / ", c.Take(2));
            Expect("focus-columns", id, "FOCUS", U(c[2]), focus.Result);
            var hit = Regex.Match(focus.Reason, @"모두 (.+) = (.+)인 행이다\(");
            Expect("focus-columns", id, "hit", U(c[3]), hit.Success ? $"{hit.Groups[1].Value} = {hit.Groups[2].Value}" : "—");
            var seen = Regex.Match(focus.Reason, @"(?:본 열: |밖의 열\()([^)]*)\)");
            Expect("focus-columns", id, "seen", U(c[4]), seen.Success ? seen.Groups[1].Value : "—");
        }

        Console.WriteLine($"PROBE spec focus-columns rows={rows.Count}");
    }

    private static void SpecUnexplained(string[] lines)
    {
        var rows = Table(lines, "| 불일치 | 사람 입력 | UNEXPLAINED |");
        foreach (var c in rows)
        {
            var n = int.Parse(U(c[0]), CultureInfo.InvariantCulture);
            string[]? keys = U(c[1]) switch
            {
                "없음" => null,
                "[]" => [],
                var x => Enumerable.Range(0, int.Parse(x.Replace("건", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture)).Select(Key).ToArray(),
            };
            var s = new Scn(Enumerable.Repeat("a", 10).ToArray(), Enumerable.Range(0, n).ToArray(), [], false, 0m, 100m, false, null, keys);
            var j = Run(s);
            Expect("unexplained", string.Join(" ", c.Take(2)), "UNEXPLAINED", U(c[2]), j.Checks.Single(x => x.Rule == "UNEXPLAINED").Result);
        }

        Console.WriteLine($"PROBE spec unexplained rows={rows.Count}");
    }

    private static void SpecPriority(string[] lines)
    {
        var rows = Table(lines, "| ACCURACY | MISSING | UNEXPLAINED | TIME | FOCUS | 판정 | 근거 키 |");
        foreach (var c in rows)
        {
            var r = c.Select(U).ToArray();
            var j = Run(Combo(r[0], r[1], r[2], r[3], r[4]));
            var id = string.Join(",", r.Take(5));
            Expect("priority", id, "checks", id, string.Join(",", j.Checks.Select(x => x.Result)));
            Expect("priority", id, "verdict", r[5], j.Verdict);
            Expect("priority", id, "rule", r[6], j.Rule);
        }

        Console.WriteLine($"PROBE spec priority rows={rows.Count}");
    }

    // ---------- 표 밖 실험 ----------
    public static int Probe()
    {
        var baseScn = Combo("met", "met", "not_reviewed", "off", "found");
        var (d, m) = Build(baseScn);

        Throws("args name-mismatch", () => JudgmentEngine.Judge(d with { Name = "other" }, m));
        Throws("args negative-time", () => JudgmentEngine.Judge(d, m, new JudgmentTimes(-1m, 0m, 0m)));
        Throws("args unexplained-not-bad", () => JudgmentEngine.Judge(d, m, null, [Key(9)]));
        Throws("args unexplained-dup", () => JudgmentEngine.Judge(d, m, null, [Key(0), Key(0)]));
        Throws("args null", () => JudgmentEngine.Judge(null!, m));

        // 추가 행(결과에만 있는 키)은 몰림을 보지 않는다.
        var extra = Run(baseScn with { ExtraGroups = ["b", "b", "b"] });
        Console.WriteLine($"PROBE focus extra-rows-ignored focus={extra.Checks[4].Result} verdict={extra.Verdict}");

        // 표시 퍼센트 vs 셈: 1799/2000은 90.0으로 보이지만 문턱 90 미달.
        var p = Run(new Scn(Enumerable.Repeat("a", 2000).ToArray(), Enumerable.Range(0, 201).ToArray(), [], false, 90m, 100m, false, null, null));
        Console.WriteLine($"PROBE count-vs-display 1799/2000 min=90 accuracy={p.Checks[0].Result} reason={p.Checks[0].Reason}");

        // 시간 합 넘침.
        var big = Run(Combo("met", "met", "none", "met", "few") with { Times = new JudgmentTimes(decimal.MaxValue, decimal.MaxValue, 1m) });
        Console.WriteLine($"PROBE time overflow result={big.Checks[3].Result}");

        // 소수 문턱·시간 자릿수.
        var dec = Run(new Scn(["a", "b", "c"], [0], [], false, 66.6m, 0.0m, true, new JudgmentTimes(2.50m, 1.25m, 1.2m), null));
        Console.WriteLine($"PROBE decimals accuracy={dec.Checks[0].Reason} | missing={dec.Checks[1].Reason} | time={dec.Checks[3].Reason}");
        Console.WriteLine($"PROBE decimals json={Utf8.GetString(JudgmentJson.Render(dec)).Split('\n')[6..10].Aggregate((a, b) => a + " " + b.Trim())}");

        // 유형 값 이스케이프(문장·JSON).
        var esc = Run(new Scn(["a\"b", "a\"b", "b", "b"], [0, 1], [], true, 0m, 100m, false, null, null));
        var escJson = Utf8.GetString(JudgmentJson.Render(esc));
        Console.WriteLine($"PROBE escape quote reason={esc.Reason}");
        Console.WriteLine($"PROBE escape quote json-line={escJson.Split('\n').First(l => l.Contains("\"reason\": \"정확도·누락률은", StringComparison.Ordinal)).Trim()}");
        var emoji = Run(new Scn(["😀", "😀", "b", "b"], [0, 1], [], true, 0m, 100m, false, null, null));
        Console.WriteLine($"PROBE escape emoji json-line={Utf8.GetString(JudgmentJson.Render(emoji)).Split('\n').First(l => l.Contains("\"reason\": \"정확도·누락률은", StringComparison.Ordinal)).Trim()}");

        // 같은 입력 두 번 = 같은 바이트. 입력 객체는 바뀌지 않는다.
        var before = MetricsJson.Render(m);
        var a1 = JudgmentJson.Render(JudgmentEngine.Judge(d, m));
        var a2 = JudgmentJson.Render(JudgmentEngine.Judge(d, m));
        Console.WriteLine($"PROBE determinism same-bytes={a1.AsSpan().SequenceEqual(a2)} metrics-unchanged={before.AsSpan().SequenceEqual(MetricsJson.Render(m))}");

        // 여러 기준 파일: 한 파일에만 있는 열은 유형 열이 아니다. 파일 순서와 상관없다.
        var def2 = CaseParser.Parse(Utf8.GetBytes("name: a\ntask: t\nsteps:\n  - name: s\ninputs: {reference: reference/*.csv, candidate: candidate/a.csv, key: id}\ncompare:\n  - {field: v, tolerance: 0}\n"), "case.yaml").Case!;
        var refA = Utf8.GetBytes("id,p,q,v\nk1,a,x,1\nk2,b,x,1\nk3,a,y,1\n");
        var refB = Utf8.GetBytes("id,v,p\nk4,1,b\nk5,1,b\n");
        var cand = Utf8.GetBytes("id,v\nk1,2\nk3,1\nk4,1\nk5,1\n");
        foreach (var (name, files) in new[]
        {
            ("files-ab", new[] { new SourceFile("reference/a.csv", refA), new SourceFile("reference/b.csv", refB) }),
            ("files-ba", new[] { new SourceFile("reference/b.csv", refB), new SourceFile("reference/a.csv", refA) }),
            ("file-a-only", new[] { new SourceFile("reference/a.csv", refA) }),
        })
        {
            var mm = CompareEngine.Compare(def2, new CompareSources(files, [new SourceFile("candidate/a.csv", cand)])).Metrics!;
            var jj = JudgmentEngine.Judge(def2, mm);
            Console.WriteLine($"PROBE focus multi-file {name} focus={jj.Checks[4].Result} reason={jj.Checks[4].Reason} sha256={Convert.ToHexStringLower(SHA256.HashData(JudgmentJson.Render(jj)))}");
        }

        // 머리 행에 같은 이름이 둘(쓰지 않는 열 중복은 대조 오류가 아니다) → 그 열은 유형 열이 아니다. 빈 이름 열도 아니다.
        foreach (var (name, header, rowsText) in new[]
        {
            ("dup-column", "id,p,p,v", "k1,a,a,2|k2,a,a,2|k3,b,b,1"),
            ("empty-name", "id,,v", "k1,a,2|k2,a,2|k3,b,1"),
            ("dup-and-q", "id,p,p,q,v", "k1,a,a,x,2|k2,a,a,x,2|k3,b,b,y,1"),
        })
        {
            var reference = Utf8.GetBytes(header + "\n" + rowsText.Replace('|', '\n') + "\n");
            var candidateBytes = Utf8.GetBytes("id,v\nk1,1\nk2,1\nk3,1\n");
            var d3 = Definition(new Scn([], [], [], false, 0m, 100m, false, null, null));
            var mm = CompareEngine.Compare(d3, new CompareSources([new SourceFile("reference/a.csv", reference)], [new SourceFile("candidate/a.csv", candidateBytes)])).Metrics!;
            var jj = JudgmentEngine.Judge(d3, mm);
            Console.WriteLine($"PROBE focus {name} focus={jj.Checks[4].Result} reason={jj.Checks[4].Reason}");
        }

        // 기준 자료의 값이 결과 자료와 달라도(유형 열은 결과에 없어도) 기준 값으로 본다.
        var refOnly = Run(new Scn(["a", "a", "b", "b"], [0, 1], [], true, 0m, 100m, false, null, null));
        Console.WriteLine($"PROBE focus reference-values verdict={refOnly.Verdict} reason={refOnly.Reason}");

        return 0;
    }

    private static void Throws(string name, Action action)
    {
        try
        {
            action();
            Console.WriteLine($"PROBE {name} no-exception");
        }
        catch (Exception e)
        {
            Console.WriteLine($"PROBE {name} {e.GetType().Name}");
        }
    }
}
