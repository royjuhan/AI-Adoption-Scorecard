using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Scorecard.Core.Cases;
using Scorecard.Core.Classification;

// T-004 스파이크 도구(머지 대상 아님).
// classify <answers.yaml> <출력폴더> : 파싱 → 분류 → classification.json. 요약 줄을 낸다.
// probe                                : 메모리 안 실험(분기, 기본값 표시, 문장, 이스케이프, 문화권, 줄바꿈).
// 출력 줄 머리: RESULT, ROW, SHA, PROBE, ERR. 절대 경로를 찍지 않는다.
internal static class Program
{
    private static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        return args switch
        {
            ["classify", var file, var outDir] => Classify(file, outDir),
            ["probe"] => Probes.Run(),
            ["refs", var dll] => Refs(dll),
            ["spec", var md] => SpecCheck.Run(md),
            _ => Usage(),
        };
    }

    private static int Usage()
    {
        Console.Error.WriteLine("사용: classify <answers.yaml> <출력폴더> | probe");
        return 2;
    }

    private static int Classify(string file, string outDir)
    {
        var label = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(file))) ?? "?";
        var parsed = CaseParser.Parse(File.ReadAllBytes(file), Path.GetFileName(file));
        if (!parsed.IsValid || parsed.Case is null)
        {
            foreach (var e in parsed.Errors)
            {
                Console.WriteLine($"ERR {label} {e}");
            }

            return 1;
        }

        var result = StepClassifier.Classify(parsed.Case);
        var bytes = ClassificationJson.Render(result);
        Directory.CreateDirectory(outDir);
        File.WriteAllBytes(Path.Combine(outDir, "classification.json"), bytes);
        Console.WriteLine($"SHA {label} {Sha(bytes)}");
        Report.Print(label, result);
        return 0;
    }

    // dll의 AssemblyRef와 TypeRef 네임스페이스(중복 없이, 서수 정렬).
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

    internal static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

internal static class Report
{
    // 행 모델 A(예외 검토·게이트 모두 행), B(게이트만 행, 예외 검토는 단계 속성), C(둘 다 속성)의 행 수.
    public static void Print(string label, ClassificationResult result)
    {
        foreach (var s in result.Steps)
        {
            foreach (var r in s.Rows)
            {
                Console.WriteLine($"ROW {label} {s.Number} {r.Kind} {r.Assignee} {r.Rule} mark={r.Reason.EndsWith("(기본값 사용, 확인 필요)", StringComparison.Ordinal)} | {s.Name} | {r.Reason}");
            }

            if (s.Checks.Count > 0 || s.AiOveruseCandidate || s.Defaults.Count > 0)
            {
                Console.WriteLine($"ROW {label} {s.Number} extra checks=[{string.Join(",", s.Checks)}] defaults=[{string.Join(",", s.Defaults)}] overuse={s.AiOveruseCandidate}");
            }
        }

        var rows = result.Steps.SelectMany(s => s.Rows).ToArray();
        var modelA = rows.Length;
        var modelB = rows.Count(r => r.Kind != StepClassifier.KindExceptionReview);
        var modelC = result.Steps.Count;
        var seq = string.Join(" ", rows.Select(r => r.Assignee));
        var perStep = string.Join(" ", result.Steps.Select(s => $"{s.Number}:{string.Join("+", s.Rows.Select(r => r.Assignee + (r.Kind == StepClassifier.KindStep ? "" : r.Kind == StepClassifier.KindHumanGate ? "g" : "e")))}"));
        Console.WriteLine($"RESULT {label} rows A={modelA} B={modelB} C={modelC} seq=[{seq}]");
        Console.WriteLine($"RESULT {label} per-step {perStep}");
        Console.WriteLine($"RESULT {label} overuse={result.Steps.Count(s => s.AiOveruseCandidate)} confirm={result.Steps.Count(s => s.Checks.Contains("confirm_existing_feature"))} measure={result.Steps.Count(s => s.Checks.Contains("measure_minutes_per_item"))} marked={rows.Count(r => r.Reason.EndsWith("확인 필요)", StringComparison.Ordinal))} rules=[{string.Join(" ", rows.Select(r => r.Rule))}]");
    }
}

internal static class Probes
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static int Run()
    {
        Branches();
        Order();
        Marks();
        Sentences();
        Overuse();
        Escaping();
        Cultures();
        LineEndings();
        return 0;
    }

    // 기준 답: 모두 직접 쓴 값. ELSE ②.
    public static StepAnswers Make(
        string ik = "table", string re = "yes", string fr = "weekly", int bs = 10, string fi = "internal_rework", string ef = "no") =>
        Base(ik: ik, re: re, fr: fr, bs: bs, fi: fi, ef: ef);

    public static StepClassification OneFor(StepAnswers a) => One(a);

    private static StepAnswers Base(
        string ik = "table", string re = "yes", string fr = "weekly", int bs = 10, decimal er = 0, string ca = "human",
        decimal? mpi = 1, string fi = "internal_rework", string ef = "no", string defaults = "") =>
        new(
            new(ik, defaults.Contains("ik", StringComparison.Ordinal)),
            new(re, defaults.Contains("re", StringComparison.Ordinal)),
            new(fr, defaults.Contains("fr", StringComparison.Ordinal)),
            new(bs, defaults.Contains("bs", StringComparison.Ordinal)),
            new(er, defaults.Contains("er", StringComparison.Ordinal)),
            new(ca, defaults.Contains("ca", StringComparison.Ordinal)),
            new(mpi, defaults.Contains("mpi", StringComparison.Ordinal)),
            new(fi, defaults.Contains("fi", StringComparison.Ordinal)),
            new(ef, defaults.Contains("ef", StringComparison.Ordinal)));

    private static StepClassification One(StepAnswers a, string name = "s") =>
        StepClassifier.Classify(new CaseDefinition("p", "t", null, null, null, [new CaseStep(1, name, a)], null, [], new CaseThresholds(null, null, null))).Steps[0];

    private static string Rows(StepClassification s) =>
        string.Join(" + ", s.Rows.Select(r => $"{r.Assignee} {r.Rule}{(r.Reason.EndsWith("확인 필요)", StringComparison.Ordinal) ? " [mark]" : "")}"));

    private static void P(string group, string name, StepClassification s) =>
        Console.WriteLine($"PROBE {group} {name} → {Rows(s)} | used={string.Join(",", s.Rows[0].Used)} | checks={string.Join(",", s.Checks)} | overuse={s.AiOveruseCandidate}");

    private static void Branches()
    {
        P("branch", "base", One(Base()));
        P("branch", "Q1 ef=yes", One(Base(ef: "yes")));
        P("branch", "Q2 re=no", One(Base(re: "no")));
        P("branch", "Q3 ik=free_doc", One(Base(ik: "free_doc")));
        P("branch", "Q3 ik=image", One(Base(ik: "image")));
        P("branch", "Q3 ik=conversation", One(Base(ik: "conversation")));
        P("branch", "Q4 rare bs=4", One(Base(fr: "rare", bs: 4)));
        P("branch", "Q4 rare bs=1", One(Base(fr: "rare", bs: 1)));
        P("branch", "boundary rare bs=5", One(Base(fr: "rare", bs: 5)));
        P("branch", "boundary weekly bs=1", One(Base(fr: "weekly", bs: 1)));
        P("branch", "boundary monthly bs=4", One(Base(fr: "monthly", bs: 4)));
        P("branch", "ELSE ik=fixed_form", One(Base(ik: "fixed_form")));
        P("branch", "ELSE daily", One(Base(fr: "daily")));
        P("branch", "PARTIAL re=partial", One(Base(re: "partial")));
        P("branch", "GATE fi=external", One(Base(fi: "external")));
        P("branch", "GATE fi=safety_legal", One(Base(fi: "safety_legal")));
        P("branch", "ef=unknown", One(Base(ef: "unknown")));
        P("branch", "mpi=null(explicit-null-not-default)", One(Base(mpi: null)));
    }

    private static void Order()
    {
        P("order", "Q1>Q2 ef=yes re=no", One(Base(ef: "yes", re: "no")));
        P("order", "Q2>Q3 re=no ik=free_doc", One(Base(re: "no", ik: "free_doc")));
        P("order", "Q3>Q4 ik=image rare bs=1", One(Base(ik: "image", fr: "rare", bs: 1)));
        P("order", "Q3 re=partial(no PARTIAL)", One(Base(ik: "free_doc", re: "partial")));
        P("order", "Q4 re=partial(no PARTIAL)", One(Base(fr: "rare", bs: 1, re: "partial")));
        P("order", "Q1+GATE", One(Base(ef: "yes", fi: "external")));
        P("order", "Q2+GATE", One(Base(re: "no", fi: "external")));
        P("order", "Q3+GATE", One(Base(ik: "conversation", fi: "safety_legal")));
        P("order", "Q4+GATE", One(Base(fr: "rare", bs: 2, fi: "external")));
        P("order", "PARTIAL+GATE", One(Base(re: "partial", fi: "external")));
        P("order", "Q4 ca=program", One(Base(fr: "rare", bs: 2, ca: "program")));
    }

    private static void Marks()
    {
        // 파서를 거친 진짜 기본값: answers 없는 단계.
        var parsed = CaseParser.Parse(Utf8NoBom.GetBytes("name: p\ntask: t\nsteps:\n  - name: 답 없음\n"), "answers.yaml");
        var all = StepClassifier.Classify(parsed.Case!).Steps[0];
        P("mark", "parser-all-defaults", all);
        Console.WriteLine($"PROBE mark parser-all-defaults defaults={string.Join(",", all.Defaults)}");
        foreach (var r in all.Rows)
        {
            Console.WriteLine($"PROBE mark parser-all-defaults row {r.Kind} used={string.Join(",", r.Used)} reason={r.Reason}");
        }

        P("mark", "only mpi default", One(Base(mpi: null, defaults: "mpi")));
        P("mark", "only er default", One(Base(er: 20, defaults: "er")));
        P("mark", "only ca default", One(Base(defaults: "ca")));
        P("mark", "only fi default", One(Base(defaults: "fi")));
        P("mark", "bs default weekly", One(Base(defaults: "bs")));
        P("mark", "bs default rare", One(Base(fr: "rare", defaults: "bs")));
        P("mark", "fr default", One(Base(defaults: "fr")));
        P("mark", "re default partial", One(Base(re: "partial", defaults: "re")));
        P("mark", "ik default Q1", One(Base(ef: "yes", defaults: "ik")));
        P("mark", "ik default ELSE", One(Base(ik: "fixed_form", defaults: "ik")));
        P("mark", "ef default unknown", One(Base(ef: "unknown", defaults: "ef")));
        P("mark", "re default Q2-not-possible(re=no explicit) ik default", One(Base(re: "no", defaults: "ik")));
    }

    private static void Sentences()
    {
        const string Example33 = "판단 기준을 글로 쓸 수 있고(yes), 입력이 표 형식이며(table), 매주 반복되므로 규칙 기반 소프트웨어로 분류.";
        var s = One(Base(re: "yes", ik: "table", fr: "weekly"));
        Console.WriteLine($"PROBE sentence example-3.3 equal={s.Rows[0].Reason == Example33}");
        foreach (var (name, a) in new (string, StepAnswers)[]
        {
            ("Q1", Base(ef: "yes")),
            ("Q2", Base(re: "no")),
            ("Q3-free_doc", Base(ik: "free_doc")),
            ("Q3-image", Base(ik: "image")),
            ("Q3-conversation", Base(ik: "conversation")),
            ("Q4", Base(fr: "rare", bs: 3)),
            ("ELSE-yes-table-weekly", Base()),
            ("ELSE-partial-fixed_form-daily", Base(re: "partial", ik: "fixed_form", fr: "daily")),
            ("ELSE-monthly", Base(fr: "monthly")),
            ("ELSE-rare-12", Base(fr: "rare", bs: 12)),
            ("GATE-external", Base(fi: "external")),
            ("GATE-safety_legal", Base(fi: "safety_legal")),
        })
        {
            foreach (var r in One(a).Rows)
            {
                Console.WriteLine($"PROBE sentence {name} {r.Rule} {r.Reason}");
            }
        }
    }

    private static void Overuse()
    {
        P("overuse", "ai_each_time ELSE", One(Base(ca: "ai_each_time")));
        P("overuse", "ai_each_time PARTIAL", One(Base(ca: "ai_each_time", re: "partial")));
        P("overuse", "ai_each_time Q1", One(Base(ca: "ai_each_time", ef: "yes")));
        P("overuse", "ai_each_time Q2", One(Base(ca: "ai_each_time", re: "no")));
        P("overuse", "ai_each_time Q3", One(Base(ca: "ai_each_time", ik: "free_doc")));
        P("overuse", "ai_each_time Q4", One(Base(ca: "ai_each_time", fr: "rare", bs: 1)));
        P("overuse", "mixed ELSE", One(Base(ca: "mixed")));
    }

    private static void Escaping()
    {
        foreach (var name in new[] { "①②③④", "한글 단계", "😀 이모지", "따옴표\"", "역슬래시\\", "<>&'+`", "탭\t", "줄\u2028구분", "DEL\u007f", "제어\u0001", "NFD \u1100\u1161" })
        {
            var r = new ClassificationResult("p", [One(Base(), name)]);
            var text = Encoding.UTF8.GetString(ClassificationJson.Render(r));
            var line = text.Split('\n').Single(l => l.TrimStart().StartsWith("\"name\"", StringComparison.Ordinal));
            Console.WriteLine($"PROBE escape {Escape(name)} → {line.Trim()}");
        }

        var json = Encoding.UTF8.GetString(ClassificationJson.Render(new ClassificationResult("p", [One(Base(re: "partial", fi: "external"))])));
        Console.WriteLine($"PROBE escape assignee-raw={json.Contains("\"②\"", StringComparison.Ordinal)} hangul-raw={json.Contains("판단", StringComparison.Ordinal)} cr={json.Contains('\r')} bom={json.StartsWith('﻿')} ends-lf={json.EndsWith("}\n", StringComparison.Ordinal)}");
    }

    private static string Escape(string s) => string.Concat(s.Select(c => c < 0x20 || c == 0x7f || c == '\u2028' ? $"U+{(int)c:X4}" : c.ToString()));

    private static void Cultures()
    {
        var steps = new[]
        {
            One(Base(fr: "rare", bs: 1234567)),
            One(Base(fr: "rare", bs: 3, fi: "external")),
            One(Base(re: "partial", ca: "ai_each_time")),
        };
        var r = new ClassificationResult("p", steps.Select((s, i) => s with { Number = i + 1 }).ToArray());
        foreach (var c in new[] { "", "de-DE", "tr-TR", "ar-SA", "ko-KR", "fr-CH" })
        {
            var prev = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(c);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(c);
                var again = new ClassificationResult("p", new[]
                {
                    One(Base(fr: "rare", bs: 1234567)),
                    One(Base(fr: "rare", bs: 3, fi: "external")) with { Number = 2 },
                    One(Base(re: "partial", ca: "ai_each_time")) with { Number = 3 },
                });
                Console.WriteLine($"PROBE culture '{c}' sha={Program.Sha(ClassificationJson.Render(again))} n1234567={1234567.ToString(CultureInfo.CurrentCulture)}");
            }
            finally
            {
                CultureInfo.CurrentCulture = prev;
                CultureInfo.CurrentUICulture = prev;
            }
        }

        Console.WriteLine($"PROBE culture reference sha={Program.Sha(ClassificationJson.Render(r))}");
    }

    private static void LineEndings()
    {
        const string Yaml = "# 합성\nname: p\ntask: t\nsteps:\n  - name: 단계\n    answers: {input_kind: table, rule_expressible: partial, failure_impact: external}\n  - name: 둘째\n";
        foreach (var (label, bytes) in new (string, byte[])[]
        {
            ("lf", Utf8NoBom.GetBytes(Yaml)),
            ("crlf", Utf8NoBom.GetBytes(Yaml.Replace("\n", "\r\n", StringComparison.Ordinal))),
            ("cr", Utf8NoBom.GetBytes(Yaml.Replace("\n", "\r", StringComparison.Ordinal))),
            ("bom", [0xEF, 0xBB, 0xBF, .. Utf8NoBom.GetBytes(Yaml)]),
        })
        {
            var parsed = CaseParser.Parse(bytes, "answers.yaml");
            Console.WriteLine($"PROBE eol {label} valid={parsed.IsValid} sha={Program.Sha(ClassificationJson.Render(StepClassifier.Classify(parsed.Case!)))}");
        }
    }
}

// 스펙 문서(classification.md)의 표를 읽어 후보 분류기와 맞는지 본다. 테스트로 옮길 수 있는 모양인지 확인용.
// 표 읽기는 tests/…/SpecDocument.Table과 같은 규칙(머리 행 정확히 1번, 구분 행 제외, 칸 앞뒤 공백 제거, "\\|"는 칸 구분 아님).
internal static class SpecCheck
{
    public const string ConditionHeader = "| 근거 키 | 키 | 비교 | 값 |";
    public const string TemplateHeader = "| 근거 키 | 담당 | 행 종류 | 문장 틀 |";
    public const string PhraseHeader = "| 키 | 값 | 문구 |";
    public const string UsedHeader = "| 근거 키 | 읽은 답 |";

    private static readonly Dictionary<string, string[]> Choices = new(StringComparer.Ordinal)
    {
        ["input_kind"] = ["table", "fixed_form", "free_doc", "image", "conversation"],
        ["rule_expressible"] = ["yes", "partial", "no"],
        ["frequency"] = ["daily", "weekly", "monthly", "rare"],
        ["current_actor"] = ["human", "ai_each_time", "program", "mixed"],
        ["failure_impact"] = ["internal_rework", "external", "safety_legal"],
        ["existing_feature"] = ["yes", "no", "unknown"],
    };

    public static int Run(string md)
    {
        var lines = File.ReadAllLines(md);
        var conditions = Table(lines, ConditionHeader);
        var templates = Table(lines, TemplateHeader);
        var phrases = Table(lines, PhraseHeader);
        var used = Table(lines, UsedHeader);
        Console.WriteLine($"PROBE spec tables conditions={conditions.Count} templates={templates.Count} phrases={phrases.Count} used={used.Count}");

        // 1. 조건 표: 행마다 걸리는 값과 안 걸리는 값.
        var bad = 0;
        foreach (var c in conditions)
        {
            var (rule, key, op, values) = (U(c[0]), U(c[1]), U(c[2]), Spans(c[3]));
            foreach (var probe in CandidateValues(key, op, values))
            {
                var got = Hit(rule, key, probe.Value);
                var ok = got == probe.Hit;
                bad += ok ? 0 : 1;
                Console.WriteLine($"PROBE spec cond {rule} {key} {op} [{string.Join(",", values)}] value={probe.Value} expect-hit={probe.Hit} got-hit={got} {(ok ? "ok" : "DIFF")}");
            }
        }

        // 2. 문장 틀 + 문구 표로 만든 문장 == 후보 분류기 문장(기본값 표시 없음).
        var phraseMap = phrases.ToDictionary(p => (U(p[0]), U(p[1])), p => p[2]);
        var templateMap = templates.ToDictionary(t => U(t[0]), t => (Assignee: t[1], Kind: U(t[2]), Text: U(t[3])));
        var combos = 0;
        foreach (var a in AllCombos())
        {
            var step = Probes.OneFor(a);
            foreach (var row in step.Rows)
            {
                combos++;
                var t = templateMap[row.Rule];
                var expected = Render(t.Text, a, phraseMap);
                if (expected != row.Reason || t.Assignee != row.Assignee || t.Kind != row.Kind)
                {
                    bad++;
                    Console.WriteLine($"PROBE spec sentence DIFF {row.Rule}\n  spec: {expected} {t.Assignee} {t.Kind}\n  code: {row.Reason} {row.Assignee} {row.Kind}");
                }
            }
        }

        // 3. 읽은 답 표 == 후보의 Used.
        var usedMap = used.ToDictionary(u => U(u[0]), u => Spans(u[1]));
        var usedChecked = 0;
        foreach (var a in AllCombos())
        {
            foreach (var row in Probes.OneFor(a).Rows)
            {
                usedChecked++;
                var spec = usedMap[row.Rule == "ELSE" && a.Frequency.Value == "rare" ? "ELSE (frequency = rare)" : row.Rule];
                if (!spec.SequenceEqual(row.Used))
                {
                    bad++;
                    Console.WriteLine($"PROBE spec used DIFF {row.Rule} spec=[{string.Join(",", spec)}] code=[{string.Join(",", row.Used)}]");
                }
            }
        }

        Console.WriteLine($"PROBE spec summary sentence-rows={combos} used-rows={usedChecked} diffs={bad}");
        return bad == 0 ? 0 : 1;
    }

    private static string Render(string template, StepAnswers a, Dictionary<(string, string), string> phrases)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["input_kind"] = a.InputKind.Value,
            ["rule_expressible"] = a.RuleExpressible.Value,
            ["frequency"] = a.Frequency.Value,
            ["batch_size"] = a.BatchSize.Value.ToString(CultureInfo.InvariantCulture),
            ["failure_impact"] = a.FailureImpact.Value,
        };
        var text = template;
        foreach (var (k, v) in values)
        {
            if (phrases.TryGetValue((k, v), out var phrase))
            {
                text = text.Replace("{" + k + " 문구}", phrase, StringComparison.Ordinal);
            }
        }

        foreach (var (k, v) in values)
        {
            text = text.Replace("{" + k + "}", v, StringComparison.Ordinal);
        }

        return text;
    }

    private static IEnumerable<StepAnswers> AllCombos()
    {
        foreach (var ik in Choices["input_kind"])
        foreach (var re in Choices["rule_expressible"])
        foreach (var fr in Choices["frequency"])
        foreach (var bs in new[] { 1, 4, 5, 12 })
        foreach (var fi in Choices["failure_impact"])
        foreach (var ef in Choices["existing_feature"])
        {
            yield return Probes.Make(ik: ik, re: re, fr: fr, bs: bs, fi: fi, ef: ef);
        }
    }

    // 규칙마다 그 규칙만 보도록 앞 분기를 피한 기준 답에서 키 하나만 바꾼다.
    private static bool Hit(string rule, string key, string value)
    {
        var a = rule switch
        {
            "Q4" when key == "frequency" => With(Probes.Make(bs: 1), key, value),
            "Q4" when key == "batch_size" => With(Probes.Make(fr: "rare"), key, value),
            "OVERUSE" => With(Probes.Make(), key, value),
            _ => With(Probes.Make(), key, value),
        };
        var s = Probes.OneFor(a);
        return rule switch
        {
            "OVERUSE" => s.AiOveruseCandidate,
            "CHECK_EXISTING" => s.Checks.Contains("confirm_existing_feature"),
            _ => s.Rows.Any(r => r.Rule == rule),
        };
    }

    private static StepAnswers With(StepAnswers a, string key, string value) => key switch
    {
        "input_kind" => a with { InputKind = new(value, false) },
        "rule_expressible" => a with { RuleExpressible = new(value, false) },
        "frequency" => a with { Frequency = new(value, false) },
        "batch_size" => a with { BatchSize = new(int.Parse(value, CultureInfo.InvariantCulture), false) },
        "current_actor" => a with { CurrentActor = new(value, false) },
        "failure_impact" => a with { FailureImpact = new(value, false) },
        "existing_feature" => a with { ExistingFeature = new(value, false) },
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, null),
    };

    private static IEnumerable<(string Value, bool Hit)> CandidateValues(string key, string op, string[] values)
    {
        switch (op)
        {
            case "=":
            case "in":
                foreach (var c in Choices[key])
                {
                    yield return (c, values.Contains(c, StringComparer.Ordinal));
                }

                break;
            case "<":
                var n = int.Parse(values.Single(), CultureInfo.InvariantCulture);
                yield return ((n - 1).ToString(CultureInfo.InvariantCulture), true);
                yield return (n.ToString(CultureInfo.InvariantCulture), false);
                yield return ("1", true);
                break;
            default:
                throw new InvalidOperationException("알 수 없는 비교: " + op);
        }
    }

    private static string U(string cell) => cell.Trim().Trim('`');

    private static string[] Spans(string cell)
    {
        var parts = cell.Split('`');
        return parts.Where((_, i) => i % 2 == 1).ToArray();
    }

    private static List<string[]> Table(string[] lines, string header)
    {
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
