using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Core.Comparison;

// T-008 스파이크 도구(머지 대상 아님). 공개 API만 쓴다.
// classify <yaml> <out.json> : 파일 하나를 파서(이름 = 파일 이름) → 분류 → classification.json
// metrics <케이스 폴더> <out.json> : case.yaml → 대조 → metrics.json
// rows <yaml> : 단계마다 행·Used·defaults·inferred·checks·문장
// spec-rows <yaml> <out.txt> : 스펙 블록 모양 "<단계> <담당> <근거 키>: <문장>" 줄과 "inferred <단계>: <키, …>" 줄
// errors <yaml> <out.txt> : 파서 오류 줄(ToString) 그대로
// probe : 메모리 안 실험(PROBE 줄)
var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
// SPIKE_KEEP_CULTURE=1이면 프로세스 문화권(LANG)을 그대로 둔다(결정론 확인용).
if (Environment.GetEnvironmentVariable("SPIKE_KEEP_CULTURE") != "1")
{
    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
}

switch (args.FirstOrDefault())
{
    case "classify":
        return Classify(args[1], args[2]);
    case "metrics":
        return Metrics(args[1], args[2]);
    case "rows":
        return Rows(args[1]);
    case "spec-rows":
        return SpecRows(args[1], args[2]);
    case "errors":
        return Errors(args[1], args[2]);
    case "probe":
        Probe.Run();
        return 0;
    default:
        Console.Error.WriteLine("사용: classify|metrics|rows|spec-rows|errors|probe");
        return 2;
}

int Classify(string yamlPath, string outPath)
{
    var parsed = CaseParser.Parse(File.ReadAllBytes(yamlPath), Path.GetFileName(yamlPath));
    if (!parsed.IsValid)
    {
        foreach (var e in parsed.Errors)
        {
            Console.WriteLine($"ERR {e}");
        }

        return 2;
    }

    var bytes = ClassificationJson.Render(StepClassifier.Classify(parsed.Case!));
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
    File.WriteAllBytes(outPath, bytes);
    Console.WriteLine($"SHA {Convert.ToHexStringLower(SHA256.HashData(bytes))} {bytes.Length} {Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(yamlPath)))}/{Path.GetFileName(yamlPath)}");
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

    var bytes = MetricsJson.Render(result.Metrics!);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
    File.WriteAllBytes(outPath, bytes);
    Console.WriteLine($"SHA {Convert.ToHexStringLower(SHA256.HashData(bytes))} {bytes.Length} {Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(caseDirectory)))}/metrics.json");
    return 0;
}

int Rows(string yamlPath)
{
    var parsed = CaseParser.Parse(File.ReadAllBytes(yamlPath), Path.GetFileName(yamlPath));
    if (!parsed.IsValid)
    {
        foreach (var e in parsed.Errors)
        {
            Console.WriteLine($"ERR {e}");
        }

        return 2;
    }

    var result = StepClassifier.Classify(parsed.Case!);
    var rows = 0;
    var marked = 0;
    foreach (var s in result.Steps)
    {
        Console.WriteLine($"RESULT step {s.Number} {s.Name} defaults=[{string.Join(",", s.Defaults)}] inferred=[{string.Join(",", s.Inferred)}] checks=[{string.Join(",", s.Checks)}] overuse={s.AiOveruseCandidate}");
        foreach (var r in s.Rows)
        {
            rows++;
            marked += r.Reason.EndsWith("(기본값 사용, 확인 필요)", StringComparison.Ordinal) ? 1 : 0;
            Console.WriteLine($"RESULT   {r.Assignee} {r.Rule} {r.Kind} used=[{string.Join(",", r.Used)}] {r.Reason}");
        }
    }

    Console.WriteLine($"RESULT total steps={result.Steps.Count} rows={rows} default-marked={marked}");
    return 0;
}

int SpecRows(string yamlPath, string outPath)
{
    var parsed = CaseParser.Parse(File.ReadAllBytes(yamlPath), Path.GetFileName(yamlPath));
    if (!parsed.IsValid)
    {
        foreach (var e in parsed.Errors)
        {
            Console.WriteLine($"ERR {e}");
        }

        return 2;
    }

    var lines = new List<string>();
    var result = StepClassifier.Classify(parsed.Case!);
    foreach (var s in result.Steps)
    {
        lines.AddRange(s.Rows.Select(r => $"{s.Number} {r.Assignee} {r.Rule}: {r.Reason}"));
    }

    foreach (var s in result.Steps.Where(s => s.Inferred.Count > 0))
    {
        lines.Add($"inferred {s.Number}: {string.Join(", ", s.Inferred)}");
    }

    File.WriteAllText(outPath, string.Concat(lines.Select(l => l + "\n")), utf8);
    var json = ClassificationJson.Render(result);
    Console.WriteLine($"RESULT spec-rows lines={lines.Count} json-has-inferred={utf8.GetString(json).Contains("\"inferred\"", StringComparison.Ordinal)}");
    return 0;
}

int Errors(string yamlPath, string outPath)
{
    var parsed = CaseParser.Parse(File.ReadAllBytes(yamlPath), Path.GetFileName(yamlPath));
    File.WriteAllText(outPath, string.Concat(parsed.Errors.Select(e => e + "\n")), utf8);
    Console.WriteLine($"RESULT errors count={parsed.Errors.Count}");
    return 0;
}

// 메모리 안 실험. 줄 모양: PROBE <무리> <이름>: <결과>
internal static class Probe
{
    // classification.md 6.1 기준 답 + human_signoff: no. 4.4 표 순서.
    private static readonly (string Key, string Value)[] Base =
    [
        ("input_kind", "table"),
        ("rule_expressible", "yes"),
        ("frequency", "weekly"),
        ("batch_size", "10"),
        ("exception_rate", "0"),
        ("current_actor", "human"),
        ("minutes_per_item", "1"),
        ("failure_impact", "internal_rework"),
        ("existing_feature", "no"),
        ("human_signoff", "no"),
    ];

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static void Run()
    {
        Signoff();
        Marks();
        Inferred();
        InferredErrors();
        Json();
        Cultures();
    }

    private static void Signoff()
    {
        foreach (var changes in new[]
        {
            "",
            "human_signoff=yes",
            "human_signoff=yes failure_impact=external",
            "human_signoff=yes failure_impact=safety_legal",
            "human_signoff=yes existing_feature=yes",
            "human_signoff=yes rule_expressible=no",
            "human_signoff=yes input_kind=image",
            "human_signoff=yes frequency=rare batch_size=1",
            "human_signoff=yes rule_expressible=partial",
            "human_signoff=yes rule_expressible=partial failure_impact=external",
            "human_signoff=yes current_actor=ai_each_time",
            "human_signoff=yes existing_feature=unknown minutes_per_item=-",
            "human_signoff=no failure_impact=external",
            "human_signoff=no current_actor=ai_each_time",
        })
        {
            Line("signoff", changes, Step(Answers(changes)));
        }
    }

    private static void Marks()
    {
        foreach (var changes in new[]
        {
            "human_signoff=-",
            "human_signoff=- failure_impact=external",
            "human_signoff=- rule_expressible=partial",
            "human_signoff=- existing_feature=yes",
            "human_signoff=- frequency=rare batch_size=3",
            "human_signoff=~",
            "human_signoff=null",
            "human_signoff=\"yes\"",
            "human_signoff='no'",
        })
        {
            Line("mark", changes, Step(Answers(changes)));
        }

        Line("mark", "답 없는 단계", Step(null));
        foreach (var bad in new[] { "human_signoff=Yes", "human_signoff=true", "human_signoff=\"\"", "human_signoff=unknown" })
        {
            Line("mark", bad, Step(Answers(bad)));
        }
    }

    private static void Inferred()
    {
        foreach (var (changes, inferred) in new[]
        {
            ("", "[frequency]"),
            ("failure_impact=external", "[failure_impact]"),
            ("rule_expressible=partial", "[rule_expressible]"),
            ("rule_expressible=partial failure_impact=external", "[rule_expressible, failure_impact]"),
            ("", "[exception_rate]"),
            ("current_actor=ai_each_time", "[current_actor]"),
            ("", "[minutes_per_item]"),
            ("", "[batch_size]"),
            ("frequency=rare batch_size=10", "[batch_size]"),
            ("frequency=rare batch_size=3", "[batch_size]"),
            ("human_signoff=yes", "[human_signoff]"),
            ("", "[human_signoff]"),
            ("human_signoff=yes", "[frequency, failure_impact]"),
            ("existing_feature=yes", "[existing_feature]"),
            ("existing_feature=yes", "[input_kind]"),
            ("existing_feature=-", "[frequency]"),
            ("human_signoff=-", "[frequency]"),
            ("human_signoff=- failure_impact=external", "[failure_impact]"),
            ("", "[input_kind, frequency]"),
            ("", "[frequency, input_kind]"),
            ("", "[]"),
            ("", "~"),
            ("", ""),
            ("", "[\"frequency\"]"),
            ("", "\n      - frequency\n      - input_kind"),
        })
        {
            Line("inferred", $"{(changes.Length == 0 ? "기준" : changes)} inferred:{inferred.Replace("\n", "\\n")}", Step(Answers(changes), inferred));
        }
    }

    private static void InferredErrors()
    {
        foreach (var (changes, inferred) in new[]
        {
            ("", " frequency"),
            ("", " {frequency: yes}"),
            ("", " [freq]"),
            ("", " [Frequency]"),
            ("", " [name]"),
            ("", " [yes]"),
            ("", " [frequency, frequency]"),
            ("", " [frequency, batch_size, frequency, frequency]"),
            ("frequency=-", " [frequency]"),
            ("frequency=~", " [frequency]"),
            ("minutes_per_item=-", " [minutes_per_item]"),
            ("minutes_per_item=null", " [minutes_per_item]"),
            ("", " [~]"),
            ("", " [[frequency]]"),
            ("", " [frequency, freq, ~, frequency]"),
            ("frequency=-", " [frequency, frequency]"),
            ("rule_expressible=maybe", " [rule_expressible]"),
        })
        {
            Line("inferred-error", $"{(changes.Length == 0 ? "기준" : changes)} inferred:{inferred}", Step(Answers(changes), inferred.TrimStart()));
        }

        Line("inferred-error", "answers 없음 inferred: [frequency]", Doc("  - name: s\n    inferred: [frequency]\n"));
        Line("inferred-error", "answers 목록 inferred: [frequency]", Doc("  - name: s\n    answers: [a]\n    inferred: [frequency]\n"));
        Line("inferred-error", "inferred가 answers 앞", Doc("  - name: s\n    inferred: [frequency, freq]\n    answers: {frequency: weekly}\n"));
    }

    // inferred가 있어도 classification.json에는 inferred 속성이 없다. 문장 표시만 다르다.
    private static void Json()
    {
        var plain = Render(Doc(StepLines(Answers("failure_impact=external"), null)));
        var marked = Render(Doc(StepLines(Answers("failure_impact=external"), "[failure_impact]")));
        var plainLines = Utf8.GetString(plain).Split('\n');
        var markedLines = Utf8.GetString(marked).Split('\n');
        var diff = plainLines.Zip(markedLines).Count(p => p.First != p.Second);
        Console.WriteLine($"PROBE json inferred-property: {Utf8.GetString(marked).Contains("\"inferred\"", StringComparison.Ordinal)}");
        Console.WriteLine($"PROBE json lines plain={plainLines.Length} marked={markedLines.Length} differing={diff}");
        foreach (var (a, b) in plainLines.Zip(markedLines).Where(p => p.First != p.Second))
        {
            Console.WriteLine($"PROBE json   - {a.Trim()}");
            Console.WriteLine($"PROBE json   + {b.Trim()}");
        }
    }

    private static void Cultures()
    {
        var doc = Doc(StepLines(Answers("human_signoff=- rule_expressible=partial failure_impact=external frequency=rare batch_size=1234567"), "[rule_expressible, failure_impact, batch_size]")
            + StepLines(Answers("human_signoff=yes"), "[human_signoff]", "서명"));
        var hashes = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var name in new[] { "", "de-DE", "tr-TR", "ar-SA", "ko-KR", "fr-CH", "th-TH" })
        {
            var saved = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            try
            {
                hashes.Add(Convert.ToHexStringLower(SHA256.HashData(Render(doc))));
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        Console.WriteLine($"PROBE culture 7종 SHA-256 수={hashes.Count} {hashes.First()[..16]}");
    }

    // "k=v k=v": 기준 답에서 바꾼다. 값 "-"는 그 키를 뺀다(기본값). 그 밖의 값은 YAML에 그대로 쓴다.
    private static (string Key, string Value)[] Answers(string changes)
    {
        var list = Base.ToList();
        foreach (var part in changes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            var index = list.FindIndex(p => p.Key == kv[0]);
            if (kv[1] == "-")
            {
                list.RemoveAt(index);
            }
            else
            {
                list[index] = (kv[0], kv[1]);
            }
        }

        return list.ToArray();
    }

    private static string StepLines((string Key, string Value)[]? answers, string? inferred, string name = "s")
    {
        var text = $"  - name: {name}\n";
        if (answers is not null)
        {
            text += "    answers: {" + string.Join(", ", answers.Select(a => $"{a.Key}: {a.Value}")) + "}\n";
        }

        if (inferred is not null)
        {
            text += $"    inferred:{(inferred.StartsWith('\n') || inferred.Length == 0 ? "" : " ")}{inferred}\n";
        }

        return text;
    }

    private static string Doc(string stepLines) => "name: p\ntask: t\nsteps:\n" + stepLines;

    private static CaseParseResult Step((string Key, string Value)[]? answers, string? inferred = null) =>
        Parse(Doc(StepLines(answers, inferred)));

    private static CaseParseResult Parse(string yaml) => CaseParser.Parse(Utf8.GetBytes(yaml), "answers.yaml");

    private static byte[] Render(string yaml) => ClassificationJson.Render(StepClassifier.Classify(Parse(yaml).Case!));

    private static void Line(string group, string name, string yaml) => Line(group, name, Parse(yaml));

    private static void Line(string group, string name, CaseParseResult parsed)
    {
        var label = name.Length == 0 ? "기준" : name;
        if (!parsed.IsValid)
        {
            Console.WriteLine($"PROBE {group} {label}: 오류 {parsed.Errors.Count}건");
            foreach (var e in parsed.Errors)
            {
                Console.WriteLine($"PROBE {group}   {e}");
            }

            return;
        }

        var s = StepClassifier.Classify(parsed.Case!).Steps[0];
        Console.WriteLine($"PROBE {group} {label}: {string.Join(" + ", s.Rows.Select(r => $"{r.Assignee} {r.Rule}"))} defaults=[{string.Join(",", s.Defaults)}] inferred=[{string.Join(",", s.Inferred)}] checks=[{string.Join(",", s.Checks)}] overuse={s.AiOveruseCandidate}");
        foreach (var r in s.Rows)
        {
            Console.WriteLine($"PROBE {group}   {r.Rule} {r.Kind} used=[{string.Join(",", r.Used)}] {r.Reason}");
        }
    }
}
