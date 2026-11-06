using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Scorecard.Core.Cases;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

// 메모리 안 실험. 줄마다 "PROBE 이름: 결과". 절대 경로를 출력하지 않는다.
internal static class Probes
{
    public static int Run(string samples)
    {
        var full = File.ReadAllBytes(Path.Combine(samples, "valid-full", "case.yaml"));
        Encodings(full);
        BlockScalarLineBreaks();
        Determinism(full);
        Columns();
        DeepNesting();
        AliasBomb();
        Normalization();
        MissingFile(samples);
        PlainWords();
        ScalarForms();
        EdgeShapes();
        YamlStreamDuplicate();
        ReflectionComparison();
        return 0;
    }

    private static void Print(string name, string result) => Console.WriteLine($"PROBE {name}: {result}");

    private static string Hash(CaseParseResult r) => Convert.ToHexStringLower(SHA256.HashData(Output.Render(r)))[..16];

    private static string Summary(CaseParseResult r) =>
        r.IsValid ? $"valid sha={Hash(r)}" : $"errors={r.Errors.Count} first=[{r.Errors[0]}] sha={Hash(r)}";

    // BOM, CRLF, CR, UTF-16, CP949, 잘못된 바이트.
    private static void Encodings(byte[] lf)
    {
        var text = Encoding.UTF8.GetString(lf);
        var crlf = Encoding.UTF8.GetBytes(text.Replace("\n", "\r\n"));
        var cr = Encoding.UTF8.GetBytes(text.Replace("\n", "\r"));
        byte[] bom = [0xEF, 0xBB, 0xBF, .. lf];
        byte[] bomCrlf = [0xEF, 0xBB, 0xBF, .. crlf];
        byte[] utf16le = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(text)];
        byte[] utf16be = [.. Encoding.BigEndianUnicode.GetPreamble(), .. Encoding.BigEndianUnicode.GetBytes(text)];
        byte[] utf16noBom = Encoding.Unicode.GetBytes(text);

        // CP949 "값" = B0 AA. 3번째 줄 값 자리에 넣는다.
        var lines = text.Split('\n');
        var head = Encoding.UTF8.GetBytes(string.Join('\n', lines.Take(2)) + "\nbefore: ");
        byte[] cp949 = [.. head, 0xB0, 0xAA, (byte)'\n', .. Encoding.UTF8.GetBytes(string.Join('\n', lines.Skip(3)))];
        byte[] bomThenBad = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("name: a\ntask: "), 0xC3, 0x28, (byte)'\n'];
        byte[] doubleBom = [0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF, .. lf];
        const string ascii = "name: a\ntask: t\nsteps:\n  - name: s\n";
        byte[] nulInValue = Encoding.UTF8.GetBytes("name: a\ntask: t\0x\nsteps:\n  - name: s\n");
        byte[] utf16AsciiNoBom = Encoding.Unicode.GetBytes(ascii);
        byte[] utf32beBom = [.. new UTF32Encoding(bigEndian: true, byteOrderMark: true).GetPreamble(), .. new UTF32Encoding(true, false).GetBytes(ascii)];

        var baseline = Hash(CaseParser.Parse(lf, CaseParser.FileName));
        foreach (var (name, bytes) in new[]
        {
            ("lf", lf), ("bom", bom), ("crlf", crlf), ("cr", cr), ("bom-crlf", bomCrlf),
            ("utf16le-bom", utf16le), ("utf16be-bom", utf16be), ("utf16le-nobom", utf16noBom),
            ("cp949-line3", cp949), ("bom-then-bad", bomThenBad), ("double-bom", doubleBom),
            ("nul-in-value", nulInValue), ("utf16le-ascii-nobom", utf16AsciiNoBom), ("utf32be-bom", utf32beBom),
        })
        {
            var r = CaseParser.Parse(bytes, CaseParser.FileName);
            Print("encoding " + name, Summary(r) + (r.IsValid ? $" same-as-lf={Hash(r) == baseline}" : string.Empty));
        }

        // StreamReader 기본 동작과 GetString의 BOM 처리 (Core가 피해야 할 함정).
        using var sr = new StreamReader(new MemoryStream(utf16le), new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        var decoded = sr.ReadToEnd();
        Print("streamreader utf16le-bom", $"no-exception decoded-as={sr.CurrentEncoding.WebName} text-ok={decoded.StartsWith('#')}");
        Print("utf8.getstring bom", $"first-char=U+{(int)Encoding.UTF8.GetString(bom)[0]:X4}");
        foreach (var (name, yaml) in new[] { ("key-first", "\uFEFFname: a\n"), ("comment-first", "\uFEFF# x\nname: a\n") })
        {
            try
            {
                var ys = new YamlStream();
                ys.Load(new StringReader(yaml));
                var firstKey = ((YamlMappingNode)ys.Documents[0].RootNode).Children.Keys.First().ToString();
                Print("yamlstream bom-char " + name, $"first-key-starts-with-U+FEFF={firstKey.StartsWith('\uFEFF')}");
            }
            catch (YamlException ex)
            {
                Print("yamlstream bom-char " + name, $"{ex.GetType().Name} at {ex.Start.Line}:{ex.Start.Column} msg={ex.Message}");
            }
        }
    }

    // 블록 스칼라(|)의 줄바꿈이 CRLF 파일에서 어떻게 들어오는가.
    private static void BlockScalarLineBreaks()
    {
        const string yaml = "name: a\ntask: |\n  첫 줄\n  둘째 줄\nsteps:\n  - name: s\n";
        foreach (var (name, text) in new[] { ("lf", yaml), ("crlf", yaml.Replace("\n", "\r\n")), ("cr", yaml.Replace("\n", "\r")) })
        {
            var r = CaseParser.Parse(Encoding.UTF8.GetBytes(text), CaseParser.FileName);
            var task = r.Case?.Task ?? "<null>";
            Print("block-scalar " + name, $"task={Escape(task)}");
        }
    }

    // 같은 바이트를 여러 번, 문화권을 바꿔서.
    private static void Determinism(byte[] lf)
    {
        var invalid = Encoding.UTF8.GetBytes("name: X\ntask: t\nsteps:\n  - name: a\n    answers: {batch_size: 1.5, exception_rate: 2.5, frequency: Weekly}\n");
        foreach (var (label, bytes) in new[] { ("valid", lf), ("invalid", invalid) })
        {
            var hashes = new List<string>();
            foreach (var culture in new[] { "", "", "de-DE", "tr-TR", "ko-KR", "ar-SA" })
            {
                var (saved, savedUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
                try
                {
                    var c = culture.Length == 0 ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(culture);
                    CultureInfo.CurrentCulture = c;
                    CultureInfo.CurrentUICulture = c;
                    hashes.Add(Hash(CaseParser.Parse(bytes, CaseParser.FileName)));
                }
                finally
                {
                    CultureInfo.CurrentCulture = saved;
                    CultureInfo.CurrentUICulture = savedUi;
                }
            }

            Print("determinism " + label, $"distinct={hashes.Distinct().Count()} runs={hashes.Count} (invariant×2, de-DE, tr-TR, ko-KR, ar-SA)");
        }
    }

    // 열 번호 단위: 한글은 1칸, 이모지(서로게이트 쌍)는 2칸인가.
    private static void Columns()
    {
        var r = CaseParser.Parse(Encoding.UTF8.GetBytes("name: a\ntask: t\nsteps:\n  - {name: 가나다, answers: {frequency: Weekly}}\n  - {name: 😀😀, answers: {frequency: Weekly}}\n"), CaseParser.FileName);
        foreach (var e in r.Errors)
        {
            Print("column", e.ToString());
        }
    }

    // 깊이 10만 중첩. 프로세스가 죽지 않는가.
    private static void DeepNesting()
    {
        foreach (var depth in new[] { 20, 100_000 })
        {
            var text = "name: a\ntask: t\nsteps: " + new string('[', depth) + new string(']', depth) + "\n";
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var r = CaseParser.Parse(Encoding.UTF8.GetBytes(text), CaseParser.FileName);
            Print($"deep-nesting {depth}", $"{Summary(r)} under-2s={watch.ElapsedMilliseconds < 2000}");
        }
    }

    // 별칭 폭탄(billion laughs). 별칭을 거부하므로 펼치지 않는다.
    private static void AliasBomb()
    {
        var sb = new StringBuilder("name: a\ntask: t\nsteps: []\nx0: &a0 [lol]\n");
        for (var i = 1; i < 20; i++)
        {
            sb.Append($"x{i}: &a{i} [*a{i - 1}, *a{i - 1}, *a{i - 1}, *a{i - 1}]\n");
        }

        var r = CaseParser.Parse(Encoding.UTF8.GetBytes(sb.ToString()), CaseParser.FileName);
        Print("alias-bomb", $"errors={r.Errors.Count} codes={string.Join(',', r.Errors.Select(e => e.Code).Distinct())}");
    }

    // NFC와 NFD 한글 이름. 정규화하지 않으면 다른 이름으로 본다.
    private static void Normalization()
    {
        var nfc = "가".Normalize(NormalizationForm.FormC);
        var nfd = "가".Normalize(NormalizationForm.FormD);
        var text = $"name: a\ntask: t\nsteps:\n  - name: {nfc}\n  - name: {nfd}\n";
        var r = CaseParser.Parse(Encoding.UTF8.GetBytes(text), CaseParser.FileName);
        Print("nfc-nfd step names", $"valid={r.IsValid} names={string.Join('|', r.Case?.Steps.Select(s => s.Name.Length.ToString(CultureInfo.InvariantCulture)) ?? [])} (UTF-16 길이)");
    }

    // 파일 없음 오류에 절대 경로가 들어가지 않는가.
    private static void MissingFile(string samples)
    {
        var missing = Path.Combine(Path.GetFullPath(samples), "no-such-folder");
        var r = CaseParser.ParseDirectory(missing);
        Print("missing-file", $"[{r.Errors[0]}] contains-abs-path={r.Errors[0].ToString().Contains(missing, StringComparison.Ordinal)}");
    }

    // YAML 1.1 불리언 낱말(yes/no/on/off)이 열거 값으로 그대로 들어오는가.
    private static void PlainWords()
    {
        var text = "name: a\ntask: t\nsteps:\n  - name: s\n    answers: {rule_expressible: yes, existing_feature: no}\n  - name: q\n    answers: {rule_expressible: \"yes\", existing_feature: 'no'}\n";
        var r = CaseParser.Parse(Encoding.UTF8.GetBytes(text), CaseParser.FileName);
        Print("yes-no plain/quoted", r.IsValid
            ? string.Join(" ", r.Case!.Steps.Select(s => $"{s.Answers.RuleExpressible.Value}/{s.Answers.ExistingFeature.Value}"))
            : r.Errors[0].ToString());
    }

    // 정수·숫자·불리언·null·이름 형식. 값마다 받은 값 또는 오류 코드.
    private static void ScalarForms()
    {
        static CaseParseResult One(string answers, string extra = "", string name = "a") =>
            CaseParser.Parse(Encoding.UTF8.GetBytes($"name: {name}\ntask: t\nsteps:\n  - name: s\n    answers: {{{answers}}}\n{extra}"), CaseParser.FileName);
        static string Code(CaseParseResult r) => string.Join('+', r.Errors.Select(e => e.Code));

        string[] numbers = ["10", "010", "-0", "+1", "1e3", ".5", "1.", "1.50", "0x10", ".inf", "1_000", "\"10\"", "'10'", "2147483648"];
        Print("integer batch_size", string.Join(" ", numbers.Select(v =>
        {
            var r = One($"batch_size: {v}");
            return $"[{v}]=" + (r.IsValid ? r.Case!.Steps[0].Answers.BatchSize.Value.ToString(CultureInfo.InvariantCulture) : Code(r));
        })));
        Print("number exception_rate", string.Join(" ", numbers.Select(v =>
        {
            var r = One($"exception_rate: {v}");
            return $"[{v}]=" + (r.IsValid ? r.Case!.Steps[0].Answers.ExceptionRate.Value.ToString(CultureInfo.InvariantCulture) : Code(r));
        })));

        string[] bools = ["true", "false", "True", "TRUE", "yes", "on", "\"true\"", "1"];
        Print("boolean time_must_drop", string.Join(" ", bools.Select(v =>
        {
            var r = One("", $"thresholds: {{time_must_drop: {v}}}\n");
            return $"[{v}]=" + (r.IsValid ? r.Case!.Thresholds.TimeMustDrop?.ToString() ?? "null" : Code(r));
        })));

        string[] nulls = ["~", "null", "NULL", "", "\"\"", "\"null\"", "''"];
        Print("null rule_expressible", string.Join(" ", nulls.Select(v =>
        {
            var r = One($"rule_expressible: {v}");
            var a = r.Case?.Steps[0].Answers.RuleExpressible;
            return $"[{v}]=" + (r.IsValid ? $"{a!.Value}/default={a.IsDefault}" : Code(r));
        })));

        string[] names = ["a", "a-1", "A", "a_b", "a--b", "-a", "가", new string('a', 64), new string('a', 65)];
        Print("case name", string.Join(" ", names.Select(v =>
        {
            var r = One("", name: v);
            return $"[{(v.Length > 10 ? v.Length + "자" : v)}]=" + (r.IsValid ? "ok" : Code(r));
        })));

        string[] stepNames = ["\"  \"", "\" 공백 \"", "~"];
        Print("step name", string.Join(" ", stepNames.Select(v =>
        {
            var r = CaseParser.Parse(Encoding.UTF8.GetBytes($"name: a\ntask: t\nsteps:\n  - name: {v}\n"), CaseParser.FileName);
            return $"[{v}]=" + (r.IsValid ? $"'{r.Case!.Steps[0].Name}'" : string.Join(" | ", r.Errors));
        })));
    }

    // 구조 경계: 복합 키, inputs 없는 compare, 빈 steps, 매핑 steps, 탭 들여쓰기.
    private static void EdgeShapes()
    {
        foreach (var (name, yaml) in new[]
        {
            ("complex-key", "name: a\ntask: t\nsteps:\n  - name: s\n? [x]\n: 1\n"),
            ("compare-without-inputs", "name: a\ntask: t\nsteps:\n  - name: s\ncompare:\n  - {field: x}\n"),
            ("empty-steps", "name: a\ntask: t\nsteps: []\n"),
            ("steps-mapping", "name: a\ntask: t\nsteps:\n  name: s\n"),
            ("tab-indent-root", "name: a\ntask: t\nsteps:\n\t- name: s\n"),
            ("tab-indent-nested", "name: a\ntask: t\nsteps:\n  - name: s\n    answers:\n\t  batch_size: 1\n"),
        })
        {
            var r = CaseParser.Parse(Encoding.UTF8.GetBytes(yaml), CaseParser.FileName);
            Print("edge " + name, r.IsValid ? "valid" : string.Join(" | ", r.Errors));
        }
    }

    // 참고: 고수준 YamlStream은 중복 키에서 예외로 멈춘다.
    private static void YamlStreamDuplicate()
    {
        try
        {
            new YamlStream().Load(new StringReader("a: 1\nb: 2\na: 3\n"));
            Print("yamlstream duplicate", "no exception");
        }
        catch (YamlException ex)
        {
            Print("yamlstream duplicate", $"{ex.GetType().Name} at {ex.Start.Line}:{ex.Start.Column} msg={ex.Message}");
        }
    }

    // 참고: 후보 A(리플렉션 역직렬화)의 오류 모양.
    private static void ReflectionComparison()
    {
        var deserializer = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
        foreach (var (name, yaml) in new[]
        {
            ("missing-name", "task: t\nsteps:\n  - name: s\n"),
            ("unknown-key", "name: a\nowner: x\n"),
            ("bad-int", "name: a\nsteps:\n  - name: s\n    answers: {batch_size: ten}\n"),
            ("yes-into-bool", "name: a\ntime_must_drop: yes\n"),
            ("empty", ""),
        })
        {
            try
            {
                var c = deserializer.Deserialize<ReflectionCase>(yaml);
                Print("A " + name, $"no exception name={c?.Name ?? "<null>"} time_must_drop={c?.TimeMustDrop?.ToString() ?? "<null>"}");
            }
            catch (YamlException ex)
            {
                Print("A " + name, $"{ex.GetType().Name} at {ex.Start.Line}:{ex.Start.Column} msg={ex.Message} inner={ex.InnerException?.GetType().Name}");
            }
        }
    }

    private static string Escape(string s) => s.Replace("\r", "\\r").Replace("\n", "\\n");
}

public sealed class ReflectionCase
{
    public string? Name { get; set; }

    public string? Task { get; set; }

    public bool? TimeMustDrop { get; set; }

    public List<ReflectionStep>? Steps { get; set; }
}

public sealed class ReflectionStep
{
    public string? Name { get; set; }

    public Dictionary<string, int>? Answers { get; set; }
}
