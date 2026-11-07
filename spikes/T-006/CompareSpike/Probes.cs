using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Core.Synthetic;

// 메모리 안 실험. 줄마다 "PROBE <무리> <이름> <결과>".
internal static class Probes
{
    private static void P(string group, string name, string result) => Console.WriteLine($"PROBE {group} {name} {result}");

    public static int Run(string examplesRoot, string work)
    {
        Glob(work);
        Csv();
        Keys();
        Columns();
        Numbers();
        Tolerance();
        Text();
        Percent();
        Json();
        NotVerified();
        Examples(examplesRoot);
        Culture(examplesRoot);
        LineEndings(examplesRoot);
        Order(examplesRoot);
        Immutable(examplesRoot);
        Seeds(examplesRoot);
        Alternatives(examplesRoot);
        Timing();
        return 0;
    }

    // 4.1 패턴: 모양, 이름 맞추기, 실제 폴더 읽기.
    private static void Glob(string work)
    {
        foreach (var pattern in new[] { "reference/*.csv", "reference/logs.csv", "*.csv", "reference/a/b/*.csv", "ref*/a.csv", "reference/**/*.csv", "reference/**.csv", "reference/[ab].csv", "reference/{a,b}.csv", "reference//a.csv", "./reference/a.csv", "reference/./a.csv" })
        {
            P("glob", $"supported '{pattern}'", CompareInputs.IsSupportedPattern(pattern).ToString());
        }

        foreach (var (pattern, name) in new[]
        {
            ("*.csv", "logs.csv"), ("*.csv", "LOGS.CSV"), ("*.csv", "logs.csvx"), ("*.csv", ".csv"), ("*.csv", "logs.csv.bak"),
            ("*.csv", "~$logs.csv"), ("*.csv", ".hidden.csv"), ("log?.csv", "log1.csv"), ("log?.csv", "log10.csv"), ("logs.csv", "logs.csv"),
            ("logs.csv", "Logs.csv"), ("*", "a"), ("a*b*c", "abc"), ("a*b*c", "acb"), ("a*b*c", "aXbYc"), ("*.CSV", "logs.csv"), ("?", ""),
        })
        {
            P("glob", $"match '{pattern}' '{name}'", CompareInputs.Matches(pattern, name).ToString());
        }

        var root = Path.Combine(work, "glob-case");
        Directory.CreateDirectory(Path.Combine(root, "reference", "sub"));
        Directory.CreateDirectory(Path.Combine(root, "Candidate"));
        foreach (var name in new[] { "b.csv", "a.csv", "A.CSV", "x.csvx", ".hidden.csv", "notes.txt" })
        {
            File.WriteAllText(Path.Combine(root, "reference", name), "id,v\nk,1\n");
        }

        File.WriteAllText(Path.Combine(root, "reference", "sub", "c.csv"), "id,v\nk,1\n");
        File.WriteAllText(Path.Combine(root, "Candidate", "a.csv"), "id,v\nk,1\n");
        Directory.CreateDirectory(Path.Combine(root, "reference", "dir.csv"));

        P("glob", "read reference/*.csv", Files(CompareInputs.Read(root, new CaseInputs("reference/*.csv", "reference/a.csv", "id"))));
        P("glob", "read candidate/*.csv (폴더 이름 Candidate)", Files(CompareInputs.Read(root, new CaseInputs("reference/a.csv", "candidate/*.csv", "id"))));
        P("glob", "read reference/*.txt", Files(CompareInputs.Read(root, new CaseInputs("reference/*.txt", "reference/a.csv", "id"))));
        P("glob", "read reference/*.json (일치 없음)", Files(CompareInputs.Read(root, new CaseInputs("reference/*.json", "reference/a.csv", "id"))));
        P("glob", "read missing/*.csv (폴더 없음)", Files(CompareInputs.Read(root, new CaseInputs("missing/*.csv", "reference/a.csv", "id"))));
        P("glob", "read reference/sub/*.csv", Files(CompareInputs.Read(root, new CaseInputs("reference/sub/*.csv", "reference/a.csv", "id"))));
        P("glob", "read ref*/a.csv (지원 밖)", Files(CompareInputs.Read(root, new CaseInputs("ref*/a.csv", "reference/a.csv", "id"))));
        P("glob", "read 케이스 폴더 없음", Files(CompareInputs.Read(Path.Combine(work, "no-such-case"), new CaseInputs("reference/*.csv", "candidate/*.csv", "id"))));

        // 같은 파일을 기준·결과가 가리키면 overlap.
        var definition = Mem.Case("compare:\n  - {field: v}\n");
        var overlap = CompareEngine.CompareDirectory(root, definition with { Inputs = new CaseInputs("reference/a.csv", "reference/*.csv", "id") });
        P("glob", "overlap reference/a.csv vs reference/*.csv", Mem.Errors(overlap));
    }

    private static string Files(CompareSourcesResult result) =>
        result.Sources is { } s
            ? $"ref=[{string.Join(",", s.Reference.Select(f => f.Path))}] cand=[{string.Join(",", s.Candidate.Select(f => f.Path))}]"
            : "ERR " + string.Join(" | ", result.Errors);

    // 4.2 CSV 읽기.
    private static void Csv()
    {
        var def = Mem.Case("compare:\n  - {field: n, tolerance: 0}\n  - {field: t}\n");
        const string good = "id,n,t\nk1,1,a\nk2,2,b\n";
        string Try(string reference, string? candidate = null) => Mem.Counts(Mem.Run(def, reference, candidate ?? good));
        string TryBytes(byte[] reference) => Mem.Counts(CompareEngine.Compare(def, new CompareSources([new SourceFile("reference/a.csv", reference)], [new SourceFile("candidate/a.csv", Mem.Bytes(good))])));

        P("csv", "lf", Try(good));
        P("csv", "crlf", Try(good.Replace("\n", "\r\n", StringComparison.Ordinal)));
        P("csv", "cr", Try(good.Replace('\n', '\r')));
        P("csv", "bom", TryBytes([0xEF, 0xBB, 0xBF, .. Mem.Bytes(good)]));
        P("csv", "bom-crlf", TryBytes([0xEF, 0xBB, 0xBF, .. Mem.Bytes(good.Replace("\n", "\r\n", StringComparison.Ordinal))]));
        P("csv", "double-bom", TryBytes([0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF, .. Mem.Bytes(good)]));
        P("csv", "no-final-newline", Try(good.TrimEnd('\n')));
        P("csv", "blank-lines", Try("\n\nid,n,t\n\nk1,1,a\n\n\nk2,2,b\n\n"));
        P("csv", "all-empty-rows", Try("id,n,t\n,,\nk1,1,a\n , ,\t\nk2,2,b\n,,\n"));
        P("csv", "quoted-all", Try("\"id\",\"n\",\"t\"\n\"k1\",\"1\",\"a\"\n\"k2\",\"2\",\"b\"\n"));
        P("csv", "quoted-comma", Try("id,n,t\nk1,1,a\nk2,2,\"b,c\"\n", "id,n,t\nk1,1,a\nk2,2,\"b,c\"\n"));
        P("csv", "quoted-escaped-quote", Try("id,n,t\nk1,1,\"say \"\"hi\"\"\"\nk2,2,b\n", "id,n,t\nk1,1,\"say \"\"hi\"\"\"\nk2,2,b\n"));
        P("csv", "quoted-newline crlf-vs-lf", Try("id,n,t\r\nk1,1,\"x\r\ny\"\r\nk2,2,b\r\n", "id,n,t\nk1,1,\"x\ny\"\nk2,2,b\n"));
        P("csv", "quoted-newline line-of-next-row", Mem.Errors(Mem.Run(def, "id,n,t\nk1,1,\"x\ny\"\nk1,2,b\n", good)));
        P("csv", "unclosed-quote", Try("id,n,t\nk1,1,a\nk2,2,\"b\nk3,3,c\n"));
        P("csv", "char-after-closing-quote", Try("id,n,t\nk1,1,\"a\"x\nk2,2,b\n"));
        P("csv", "quote-inside-unquoted", Try("id,n,t\nk1,1,a\"b\nk2,2,b\n", "id,n,t\nk1,1,a\"b\nk2,2,b\n"));
        P("csv", "space-before-quote", Try("id,n,t\nk1,1, \"a\"\nk2,2,b\n"));
        P("csv", "utf16le-bom", TryBytes([0xFF, 0xFE, .. Encoding.Unicode.GetBytes(good)]));
        P("csv", "utf16be-bom", TryBytes([0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes(good)]));
        P("csv", "utf16le-nobom", TryBytes(Encoding.Unicode.GetBytes(good)));
        // CP949 "가"는 B0 A1. UTF-8로 잘못된 바이트다.
        P("csv", "cp949-line3", TryBytes([.. Mem.Bytes("id,n,t\nk1,1,a\nk2,2,"), 0xB0, 0xA1, (byte)'\n']));
        P("csv", "invalid-utf8-after-bom", TryBytes([0xEF, 0xBB, 0xBF, .. Mem.Bytes("id,n,t\n"), 0xFF, (byte)'\n']));
        P("csv", "nul-in-value", Try("id,n,t\nk1,1,a\0b\nk2,2,b\n", "id,n,t\nk1,1,a\0b\nk2,2,b\n"));
        P("csv", "semicolon-delimiter", Try("id;n;t\nk1;1;a\nk2;2;b\n"));
        P("csv", "header-only", Try("id,n,t\n"));
        P("csv", "empty-file", Try(""));
        P("csv", "bom-only", TryBytes([0xEF, 0xBB, 0xBF]));
        P("csv", "blank-only", Try("\n\n , \n"));
        P("csv", "field-count-short", Try("id,n,t\nk1,1\nk2,2,b\n"));
        P("csv", "field-count-long", Try("id,n,t\nk1,1,a,extra\nk2,2,b\n"));
        P("csv", "trailing-comma-header-and-rows", Try("id,n,t,\nk1,1,a,\nk2,2,b,\n"));
        P("csv", "candidate-header-only", Try(good, "id,n,t\n"));
        P("csv", "candidate-crlf-bom", Mem.Counts(CompareEngine.Compare(def, new CompareSources([new SourceFile("reference/a.csv", Mem.Bytes(good))], [new SourceFile("candidate/a.csv", [0xEF, 0xBB, 0xBF, .. Mem.Bytes(good.Replace("\n", "\r\n", StringComparison.Ordinal))])]))));
        P("csv", "column-of-cell-after-hangul", Mem.Errors(Mem.Run(def, "id,n,t\n가나다,x,a\n", good)));
        P("csv", "column-after-emoji", Mem.Errors(Mem.Run(def, "id,n,t\n\U0001F600,x,a\n", good)));
    }

    // 4.3 키.
    private static void Keys()
    {
        var def = Mem.Case("compare:\n  - {field: v}\n");
        P("key", "dup-same-file", Mem.Errors(Mem.Run(def, "id,v\nk1,a\nk2,b\nk1,c\n", "id,v\nk1,a\n")));
        P("key", "dup-candidate", Mem.Errors(Mem.Run(def, "id,v\nk1,a\n", "id,v\nk1,a\nk1,a\n")));
        P("key", "dup-across-files", Mem.Errors(Mem.Run(def,
            [("reference/a.csv", Mem.Bytes("id,v\nk1,a\nk2,b\n")), ("reference/b.csv", Mem.Bytes("id,v\nk3,c\nk2,d\n"))],
            [("candidate/a.csv", Mem.Bytes("id,v\nk1,a\n"))])));
        P("key", "dup-after-trim", Mem.Errors(Mem.Run(def, "id,v\nk1,a\n k1 ,b\n", "id,v\nk1,a\n")));
        P("key", "empty-key", Mem.Errors(Mem.Run(def, "id,v\nk1,a\n,b\n", "id,v\nk1,a\n")));
        P("key", "space-key", Mem.Errors(Mem.Run(def, "id,v\nk1,a\n  ,b\n", "id,v\nk1,a\n")));
        P("key", "empty-key-candidate", Mem.Errors(Mem.Run(def, "id,v\nk1,a\n", "id,v\nk1,a\n,b\n")));
        P("key", "trim-matches", Mem.Counts(Mem.Run(def, "id,v\nk1,a\n", "id,v\n\" k1\t\",a\n")));
        P("key", "case-differs", Mem.Counts(Mem.Run(def, "id,v\nlog01,a\n", "id,v\nLOG01,a\n")));
        P("key", "nfc-vs-nfd", Mem.Counts(Mem.Run(def, "id,v\né,a\n", "id,v\né,a\n")));
        P("key", "numeric-text-key", Mem.Counts(Mem.Run(def, "id,v\n1,a\n", "id,v\n1.0,a\n")));
        P("key", "multi-file-order", Order2(def));
    }

    private static string Order2(CaseDefinition def)
    {
        var r = Mem.Run(def,
            [("reference/b.csv", Mem.Bytes("id,v\nk3,c\n")), ("reference/a.csv", Mem.Bytes("id,v\nk1,a\nk2,b\n"))],
            [("candidate/a.csv", Mem.Bytes("id,v\nk9,z\nk2,x\n"))]);
        return r.Metrics is { } m ? $"ref-files=[{string.Join(",", m.ReferenceFiles)}] items=[{string.Join(" ", m.Items.Select(i => $"{i.Key}:{i.Status}@{Program.Loc(i.Reference ?? i.Candidate)}"))}]" : Mem.Errors(r);
    }

    // 4.2 머리 행·열.
    private static void Columns()
    {
        var def = Mem.Case("compare:\n  - {field: n, tolerance: 0}\n  - {field: t}\n");
        const string good = "id,n,t\nk1,1,a\n";
        P("column", "missing-in-candidate", Mem.Errors(Mem.Run(def, good, "id,n\nk1,1\n")));
        P("column", "missing-key-in-reference", Mem.Errors(Mem.Run(def, "key,n,t\nk1,1,a\n", good)));
        P("column", "order-differs", Mem.Counts(Mem.Run(def, good, "t,id,n\na,k1,1\n")));
        P("column", "extra-columns-ignored", Mem.Counts(Mem.Run(def, "id,x,n,t\nk1,zz,1,a\n", "id,n,t,y\nk1,1,a,ww\n")));
        P("column", "header-spaces-trimmed", Mem.Counts(Mem.Run(def, " id , n ,t\nk1,1,a\n", good)));
        P("column", "header-case-differs", Mem.Errors(Mem.Run(def, "ID,n,t\nk1,1,a\n", good)));
        P("column", "duplicate-used-column", Mem.Errors(Mem.Run(def, "id,n,t,n\nk1,1,a,2\n", good)));
        P("column", "duplicate-unused-column", Mem.Counts(Mem.Run(def, "id,n,t,x,x\nk1,1,a,p,q\n", good)));
        P("column", "headers-differ-between-files", Mem.Counts(Mem.Run(def,
            [("reference/a.csv", Mem.Bytes("id,n,t\nk1,1,a\n")), ("reference/b.csv", Mem.Bytes("t,n,id,extra\nb,2,k2,z\n"))],
            [("candidate/a.csv", Mem.Bytes("id,n,t\nk1,1,a\nk2,2,b\n"))])));
        P("column", "all-errors-collected", Mem.Errors(Mem.Run(def, "id,n,t\nk1,x,a\nk1,1,a\n,2,b\nk4,4\nk5,y,c\n", "id,n\nk1,1\n")));
    }

    // 4.5 숫자 모양(기준 칸). 결과 칸은 같은 규칙으로 not_number.
    public static readonly string[] NumberCells =
    [
        "12", "-0.5", "+3", ".5", "5.", "007", "1.50", "1e3", "1.5E-2", "1E+2", "-0", " 12 ", "79228162514264337593543950335",
        "0.1234567890123456789012345678901", "1e-400",
        "1,5", "1 000", "1_000", "NaN", "Infinity", "∞", "0x10", "12%", "$5", "１２", "", "-", ".", "e3", "1e", "1e400",
        "79228162514264337593543950336", "--1", "+-1", "1.2.3", "٣",
    ];

    private static void Numbers()
    {
        var def = Mem.Case("compare:\n  - {field: v, tolerance: 0}\n");
        foreach (var cell in NumberCells)
        {
            var r = Mem.Run(def, $"id,v\nk,{Mem.Quote(cell)}\n", $"id,v\nk,{Mem.Quote(cell)}\n");
            var shown = r.Metrics is not null ? "숫자 " + NumberValue(cell) : "아님 " + Mem.Errors(r);
            P("number", $"'{cell}'", shown);
        }

        // 결과 칸이 숫자가 아니면 오류가 아니라 not_number(값 불일치).
        P("number", "candidate-not-number", Mem.Counts(Mem.Run(def, "id,v\nk1,1\nk2,2\n", "id,v\nk1,N/A\nk2,2\n")));
        P("number", "candidate-empty", Mem.Counts(Mem.Run(def, "id,v\nk1,1\nk2,2\n", "id,v\nk1,\nk2,2\n")));
        P("number", "reference-empty", Mem.Errors(Mem.Run(def, "id,v\nk1,\nk2,2\n", "id,v\nk1,1\nk2,2\n")));
    }

    // 정규화 뒤 decimal 값(InvariantCulture).
    public static string NumberValue(string cell) =>
        decimal.TryParse(cell.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var v)
            ? v.ToString(CultureInfo.InvariantCulture)
            : "-";

    // 4.5 허용 오차.
    public static readonly (string Tolerance, string Reference, string Candidate)[] ToleranceRows =
    [
        ("tolerance: 0", "5", "5"), ("tolerance: 0", "1.5", "1.50"), ("tolerance: 0", "5", "5.0001"), ("tolerance: 0", "-0", "0"), ("tolerance: 0", "1e3", "1000"),
        ("tolerance: 2", "10", "12"), ("tolerance: 2", "10", "8"), ("tolerance: 2", "10", "12.0001"), ("tolerance: 2", "10", "7.9999"),
        ("tolerance_pct: 1.0", "30.77", "31.0777"), ("tolerance_pct: 1.0", "30.77", "30.4623"), ("tolerance_pct: 1.0", "30.77", "31.0778"),
        ("tolerance_pct: 1.0", "100", "101"), ("tolerance_pct: 1.0", "100", "99"), ("tolerance_pct: 1.0", "100", "101.01"),
        ("tolerance_pct: 1.0", "99", "100"), ("tolerance_pct: 1.0", "0", "0"), ("tolerance_pct: 1.0", "0", "0.0001"),
        ("tolerance_pct: 2", "-50", "-51"), ("tolerance_pct: 2", "-50", "-51.01"), ("tolerance_pct: 0", "10", "10.0"), ("tolerance_pct: 0", "10", "10.0001"),
        ("tolerance_pct: 0.1", "12345.678", "12358.023678"), ("tolerance_pct: 0.1", "12345.678", "12358.023679"),
        ("tolerance_pct: 1.0", "30.77", "약 31"), ("tolerance: 0", "5", ""), ("tolerance: 0", "5", "N/A"), ("tolerance: 0", "5", " 5 "),
        ("없음", "item-a", "item-a"), ("없음", "item-a", " item-a "), ("없음", "item-a", "Item-A"), ("없음", "item-a", "item-b"),
        ("없음", "10", "10.0"), ("없음", "", ""), ("없음", "a  b", "a b"),
    ];

    private static void Tolerance()
    {
        foreach (var (t, r, c) in ToleranceRows)
        {
            P("tolerance", $"[{t}] '{r}' vs '{c}'", Mem.Judge(t, r, c));
        }

        // 넘침: 차이·곱이 decimal 범위를 넘는 경우. 예외가 새지 않는다.
        const string max = "79228162514264337593543950335";
        P("tolerance", "overflow diff abs", Mem.Judge("tolerance: 1", max, "-" + max));
        P("tolerance", "overflow diff pct", Mem.Judge("tolerance_pct: 1.0", max, "-" + max));
        P("tolerance", "overflow right pct huge", Mem.Judge("tolerance_pct: 79228162514264337593543950335", "1e10", "0"));
        P("tolerance", "overflow left only", Mem.Judge("tolerance_pct: 1.0", "1", "1e27"));
        P("tolerance", "overflow both", Mem.Judge("tolerance_pct: 1000000000000000000000000000", "1e27", "-1e27"));
        P("tolerance", "max vs max", Mem.Judge("tolerance_pct: 1.0", max, max));
    }

    // 4.6 텍스트 정규화: 앞뒤 글자, 대소문자, NFC/NFD, 줄바꿈.
    public static readonly (string CodePoint, string Name)[] TrimChars =
    [
        ("U+0020", "공백"), ("U+0009", "탭"), ("U+000A", "LF"), ("U+000D", "CR"), ("U+00A0", "줄바꿈 없는 공백"), ("U+3000", "전각 공백"),
        ("U+2028", "줄 구분자"), ("U+2003", "em 공백"), ("U+200B", "폭 없는 공백"), ("U+FEFF", "BOM 글자"), ("U+00AD", "소프트 하이픈"),
    ];

    private static void Text()
    {
        foreach (var (cp, name) in TrimChars)
        {
            var ch = char.ConvertFromUtf32(int.Parse(cp[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            P("text", $"trim {cp} {name}", Mem.Judge("없음", "item-a", ch + "item-a" + ch));
        }

        P("text", "nfc-vs-nfd 가", Mem.Judge("없음", "가", "가"));
        P("text", "nfc-vs-nfd e-acute", Mem.Judge("없음", "é", "é"));
        P("text", "case", Mem.Judge("없음", "Normal", "normal"));
        P("text", "turkish-i (tr-TR 무관)", WithCulture("tr-TR", () => Mem.Judge("없음", "I", "ı")));
        P("text", "inner-crlf-vs-lf", Mem.Judge("없음", "a\r\nb", "a\nb"));
        P("text", "inner-cr-vs-lf", Mem.Judge("없음", "a\rb", "a\nb"));
        P("text", "inner-space-kept", Mem.Judge("없음", "a b", "a  b"));
        P("text", "fullwidth-digit", Mem.Judge("없음", "12", "１２"));
    }

    private static T WithCulture<T>(string name, Func<T> f)
    {
        var (c, u) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
            return f();
        }
        finally
        {
            CultureInfo.CurrentCulture = c;
            CultureInfo.CurrentUICulture = u;
        }
    }

    // 4.8 퍼센트.
    public static readonly (int Part, int Whole)[] PercentRows =
    [
        (27, 30), (1, 30), (20, 20), (0, 20), (29, 30), (1, 3), (2, 3), (1, 6), (5, 6), (1, 8), (1, 16), (3, 16), (1, 80),
        (1, 2000), (1, 3000), (2, 2001), (1999, 2000), (1, 1),
    ];

    private static void Percent()
    {
        foreach (var (part, whole) in PercentRows)
        {
            P("percent", $"{part}/{whole}", PercentViaEngine(part, whole));
        }

        // 정수 셈(Core 식)과 decimal Math.Round(AwayFromZero)가 전체 1..2000에서 같은가. ToEven과 몇 건이 다른가.
        int diffAway = 0, diffEven = 0, total = 0;
        for (var whole = 1; whole <= 2000; whole++)
        {
            for (var part = 0; part <= whole; part++)
            {
                total++;
                var tenths = ((2L * part * 1000) + whole) / (2L * whole);
                var core = new decimal((int)tenths, 0, 0, false, 1);
                var away = Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero);
                var even = Math.Round(part * 100m / whole, 1, MidpointRounding.ToEven);
                diffAway += core == away ? 0 : 1;
                diffEven += core == even ? 0 : 1;
            }
        }

        P("percent", "integer-formula-vs-decimal-round", $"pairs={total} diff-away-from-zero={diffAway} diff-to-even={diffEven}");
    }

    // 기준 whole행 중 part행을 일치시켜 정확도를 공개 API로 얻는다.
    public static string PercentViaEngine(int part, int whole)
    {
        var def = Mem.Case("compare:\n  - {field: v}\n");
        var reference = new StringBuilder("id,v\n");
        var candidate = new StringBuilder("id,v\n");
        for (var i = 0; i < whole; i++)
        {
            reference.Append(CultureInfo.InvariantCulture, $"k{i},a\n");
            candidate.Append(CultureInfo.InvariantCulture, $"k{i},{(i < part ? "a" : "b")}\n");
        }

        var m = Mem.Run(def, reference.ToString(), candidate.ToString()).Metrics!;
        return Program.Show(m.AccuracyPct);
    }

    // 3.3 JSON 숫자·이스케이프.
    private static void Json()
    {
        var options = new JsonWriterOptions { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        foreach (var d in new[] { 90.0m, 3.3m, 100.0m, 0.0m, 1.0m, 0m, 1.50m, 90m, 0.015m, -0.5m, 79228162514264337593543950335m })
        {
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            using (var w = new Utf8JsonWriter(buffer, options))
            {
                w.WriteNumberValue(d);
            }

            P("json", $"decimal scale={d.Scale} {d.ToString(CultureInfo.InvariantCulture)}", Encoding.UTF8.GetString(buffer.WrittenSpan));
        }

        var def = Mem.Case("compare:\n  - {field: v}\n");
        var key = "키\"\\\t\u2028\U0001F600<>&'+`";
        var r = Mem.Run(def, $"id,v\n{Mem.Quote(key)},a\n", $"id,v\n{Mem.Quote(key)},b\n");
        var text = Encoding.UTF8.GetString(MetricsJson.Render(r.Metrics!));
        var line = text.Split('\n').First(l => l.Contains("\"key\": \"키", StringComparison.Ordinal)).Trim();
        P("json", "escape key", line);
        P("json", "empty-arrays", Encoding.UTF8.GetString(MetricsJson.Render(new CompareMetrics("x", CompareEngine.StatusNotVerified, CompareEngine.ReasonNoInputs, null, [], [], null, null, null, [], []))).Replace("\n", "\\n", StringComparison.Ordinal));
    }

    private static void NotVerified()
    {
        var noInputs = CaseParser.Parse(Mem.Bytes("name: p\ntask: t\nsteps:\n  - name: s\n"), "case.yaml").Case!;
        P("not-verified", "no-inputs", Mem.Counts(CompareEngine.Compare(noInputs, new CompareSources([], []))));
        P("not-verified", "no-inputs directory not read", Mem.Counts(CompareEngine.CompareDirectory("/nonexistent-should-not-be-read", noInputs)));
        var noCompare = Mem.Case(string.Empty);
        P("not-verified", "no-compare-fields", Mem.Counts(CompareEngine.Compare(noCompare, new CompareSources([], []))));
        var def = Mem.Case("compare:\n  - {field: v}\n");
        P("not-verified", "reference-header-only", Mem.Errors(Mem.Run(def, "id,v\n", "id,v\nk,a\n")));
        P("not-verified", "reference-no-files", Mem.Errors(CompareEngine.Compare(def, new CompareSources([], [new SourceFile("candidate/a.csv", Mem.Bytes("id,v\nk,a\n"))]))));
        P("not-verified", "candidate-no-files", Mem.Counts(CompareEngine.Compare(def, new CompareSources([new SourceFile("reference/a.csv", Mem.Bytes("id,v\nk,a\n"))], []))));
    }

    private static CompareResult Example(string root, string example) =>
        CompareEngine.CompareDirectory(Path.Combine(root, example), Program.ParseCase(Path.Combine(root, example)));

    private static void Examples(string root)
    {
        foreach (var example in Program.Examples)
        {
            P("example", example, Mem.Counts(Example(root, example)));
        }
    }

    private static void Culture(string root)
    {
        foreach (var example in Program.Examples)
        {
            var shas = new[] { "", "de-DE", "tr-TR", "ar-SA", "ko-KR", "fr-CH", "th-TH" }
                .Select(c => WithCulture(c, () => Program.Sha(MetricsJson.Render(Example(root, example).Metrics!))))
                .Distinct()
                .ToArray();
            P("culture", $"{example} 7 cultures", $"distinct-sha={shas.Length} {shas[0][..16]}");
        }
    }

    // 예제 CSV 줄바꿈·BOM·따옴표 변형 → 같은 metrics.json 바이트.
    private static void LineEndings(string root)
    {
        foreach (var example in new[] { "table-compare", "form-fill" })
        {
            var dir = Path.Combine(root, example);
            var definition = Program.ParseCase(dir);
            var sources = CompareInputs.Read(dir, definition.Inputs!).Sources!;
            var baseline = MetricsJson.Render(CompareEngine.Compare(definition, sources).Metrics!);
            foreach (var variant in new[] { "crlf", "cr", "bom", "bom-crlf", "quoted", "bom-crlf-quoted", "no-final-newline", "trailing-blank-lines", "cell-spaces" })
            {
                SourceFile Transform(SourceFile f)
                {
                    var text = Encoding.UTF8.GetString(f.Content);
                    var lines = text.TrimEnd('\n').Split('\n');
                    string Join(IEnumerable<string> ls, string nl) => string.Concat(ls.Select(l => l + nl));
                    string Quoted() => Join(lines.Select(l => string.Join(",", l.Split(',').Select(Mem.Quote))), "\n");
                    var changed = variant switch
                    {
                        "crlf" => Mem.Bytes(Join(lines, "\r\n")),
                        "cr" => Mem.Bytes(Join(lines, "\r")),
                        "bom" => [0xEF, 0xBB, 0xBF, .. f.Content],
                        "bom-crlf" => [0xEF, 0xBB, 0xBF, .. Mem.Bytes(Join(lines, "\r\n"))],
                        "quoted" => Mem.Bytes(Quoted()),
                        "bom-crlf-quoted" => [0xEF, 0xBB, 0xBF, .. Mem.Bytes(Quoted().Replace("\n", "\r\n", StringComparison.Ordinal))],
                        "no-final-newline" => Mem.Bytes(text.TrimEnd('\n')),
                        "trailing-blank-lines" => Mem.Bytes(text + "\n\n,,,,\n"),
                        "cell-spaces" => Mem.Bytes(Join(lines.Select(l => string.Join(",", l.Split(',').Select(c => " " + c + "\t"))), "\n")),
                        _ => throw new ArgumentOutOfRangeException(nameof(variant)),
                    };
                    return f with { Content = changed };
                }

                var candidateOnly = new CompareSources(sources.Reference, sources.Candidate.Select(Transform).ToArray());
                var both = new CompareSources(sources.Reference.Select(Transform).ToArray(), sources.Candidate.Select(Transform).ToArray());
                var a = MetricsJson.Render(CompareEngine.Compare(definition, both).Metrics!);
                var b = MetricsJson.Render(CompareEngine.Compare(definition, candidateOnly).Metrics!);
                P("eol", $"{example} {variant}", $"both-same={a.AsSpan().SequenceEqual(baseline)} candidate-only-same={b.AsSpan().SequenceEqual(baseline)}");
            }
        }
    }

    // 파일 순서·행 순서를 바꿔 넘겨도 같은가(Compare가 경로 서수로 정렬).
    private static void Order(string root)
    {
        var def = Mem.Case("compare:\n  - {field: v}\n");
        var files = new[] { ("reference/b.csv", Mem.Bytes("id,v\nk3,c\n")), ("reference/a.csv", Mem.Bytes("id,v\nk1,a\nk2,b\n")) };
        var cand = new[] { ("candidate/a.csv", Mem.Bytes("id,v\nk2,x\nk9,z\n")) };
        var a = MetricsJson.Render(Mem.Run(def, files, cand).Metrics!);
        var b = MetricsJson.Render(Mem.Run(def, files.Reverse(), cand).Metrics!);
        P("order", "reference files given in reverse", $"same={a.AsSpan().SequenceEqual(b)}");
        var dir = Path.Combine(root, "table-compare");
        var definition = Program.ParseCase(dir);
        var sources = CompareInputs.Read(dir, definition.Inputs!).Sources!;
        var lines = Encoding.UTF8.GetString(sources.Candidate[0].Content).TrimEnd('\n').Split('\n');
        var sorted = Mem.Bytes(string.Concat(new[] { lines[0] }.Concat(lines.Skip(1).Order(StringComparer.Ordinal)).Select(l => l + "\n")));
        var x = CompareEngine.Compare(definition, sources).Metrics!;
        var y = CompareEngine.Compare(definition, new CompareSources(sources.Reference, [sources.Candidate[0] with { Content = sorted }])).Metrics!;
        P("order", "table-compare candidate rows sorted by key", $"counts-same={x.Counts == y.Counts} json-same={MetricsJson.Render(x).AsSpan().SequenceEqual(MetricsJson.Render(y))} (줄 번호가 바뀌므로 json은 달라야 한다)");
    }

    private static void Immutable(string root)
    {
        var dir = Path.Combine(root, "table-compare");
        var definition = Program.ParseCase(dir);
        var sources = CompareInputs.Read(dir, definition.Inputs!).Sources!;
        var before = sources.Reference.Concat(sources.Candidate).Select(f => Program.Sha(f.Content)).ToArray();
        _ = CompareEngine.Compare(definition, sources);
        _ = CompareEngine.Compare(definition, sources);
        var after = sources.Reference.Concat(sources.Candidate).Select(f => Program.Sha(f.Content)).ToArray();
        P("immutable", "content bytes unchanged", (before.SequenceEqual(after)).ToString());
    }

    // 합성 생성기(T-005)로 시드를 바꿔 만든 자료에서 대조 셈 = 심은 셈, 키도 같은가.
    private static void Seeds(string root)
    {
        var tc = Program.ParseCase(Path.Combine(root, "table-compare"));
        var ff = Program.ParseCase(Path.Combine(root, "form-fill"));
        var tcPlan = new SyntheticPlan(8, "logs.csv", "log", 2, 30,
            [SyntheticColumn.Choice("signal_type", "normal", "irregular"), SyntheticColumn.Integer("start_index", 0, 9000), SyntheticColumn.Integer("length", 50, 500), SyntheticColumn.Decimal("mean_value", 10.00m, 99.99m, 2)],
            ["length", "mean_value"], 1, 1, "mean_value", 4, "signal_type", "irregular", true);
        var ffPlan = new SyntheticPlan(0, "orders.csv", "ord", 2, 20,
            [SyntheticColumn.Choice("item", "item-a", "item-b", "item-c", "item-d", "item-e"), SyntheticColumn.Integer("quantity", 1, 50)],
            [], 0, 0, null, 0, null, null, true);
        var guardDef = Mem.Case("compare:\n  - {field: start, tolerance: 0}\n  - {field: mean, tolerance_pct: 1.0}\n  - {field: label}\n", "row_id");
        foreach (var (name, def, plan, count) in new[] { ("table-compare", tc, tcPlan, 2000), ("form-fill", ff, ffPlan, 2000), ("guard", guardDef, Program.GuardPlan, 2000) })
        {
            int ok = 0, bad = 0, skipped = 0;
            string? firstBad = null;
            for (ulong seed = 0; seed < (ulong)count; seed++)
            {
                SyntheticTables tables;
                try
                {
                    tables = SyntheticGenerator.Generate(def, plan with { Seed = seed });
                }
                catch (ArgumentException)
                {
                    skipped++;
                    continue;
                }

                var sources = new CompareSources(
                    [new SourceFile("reference/" + tables.FileName, SyntheticCsv.Render(tables.Header, tables.Reference))],
                    [new SourceFile("candidate/" + tables.FileName, SyntheticCsv.Render(tables.Header, tables.Candidate))]);
                var m = CompareEngine.Compare(def, sources).Metrics!;
                var plantedMissing = tables.Reference.Where(r => r.Planted == SyntheticGenerator.PlantedMissing).Select(r => r.Key).Order(StringComparer.Ordinal);
                var plantedMismatch = tables.Candidate.Where(r => r.Planted.StartsWith(SyntheticGenerator.PlantedMismatchPrefix, StringComparison.Ordinal)).Select(r => r.Key).Order(StringComparer.Ordinal);
                var plantedExtra = tables.Candidate.Where(r => r.Planted == SyntheticGenerator.PlantedExtra).Select(r => r.Key).Order(StringComparer.Ordinal);
                var plantedWithin = tables.Candidate.Where(r => r.Planted.StartsWith(SyntheticGenerator.PlantedWithinPrefix, StringComparison.Ordinal)).Select(r => r.Key).Order(StringComparer.Ordinal);
                bool Same(IEnumerable<string> planted, string status, bool within = false) =>
                    planted.SequenceEqual(m.Items.Where(i => i.Status == status && (!within || i.Differences.Count > 0)).Select(i => i.Key).Order(StringComparer.Ordinal));
                var same = Same(plantedMissing, CompareEngine.ItemMissing) && Same(plantedMismatch, CompareEngine.ItemMismatch)
                    && Same(plantedExtra, CompareEngine.ItemExtra) && Same(plantedWithin, CompareEngine.ItemMatch, within: true);
                if (same)
                {
                    ok++;
                }
                else
                {
                    bad++;
                    firstBad ??= seed.ToString(CultureInfo.InvariantCulture);
                }
            }

            P("seeds", $"{name} seeds 0..{count - 1}", $"same={ok} differ={bad} generator-refused={skipped} first-differ={firstBad ?? "-"}");
        }
    }

    // 다른 정의로 셌을 때(table-compare). 제안: 항목 = 기준 행, 기준값 대비 %, 경계 포함.
    private static void Alternatives(string root)
    {
        var dir = Path.Combine(root, "table-compare");
        var definition = Program.ParseCase(dir);
        var m = Example(root, "table-compare").Metrics!;
        var cells = m.Items.Count(i => i.Status != CompareEngine.ItemExtra && i.Status != CompareEngine.ItemMissing) * definition.Compare.Count;
        var badCells = m.Fields.Sum(f => f.Outside + f.NotNumber);
        var missingCells = m.Counts!.Missing * definition.Compare.Count;
        P("alt", "item=reference-row (제안)", $"accuracy={Program.Show(m.AccuracyPct)} missing={Program.Show(m.MissingRatePct)}");
        P("alt", "item=cell", $"accuracy={(cells - badCells) * 100m / (cells + missingCells):F1}");
        P("alt", "denominator=both-present", $"accuracy={m.Counts.Match * 100m / (m.Counts.Reference - m.Counts.Missing):F1}");
        P("alt", "denominator=reference+extra", $"accuracy={m.Counts.Match * 100m / (m.Counts.Reference + m.Counts.Extra):F1}");
    }

    private static void Timing()
    {
        var def = Mem.Case("compare:\n  - {field: a, tolerance: 0}\n  - {field: b, tolerance_pct: 1.0}\n  - {field: c}\n");
        foreach (var rows in new[] { 10_000, 100_000 })
        {
            var reference = new StringBuilder("id,a,b,c\n");
            var candidate = new StringBuilder("id,a,b,c\n");
            for (var i = 0; i < rows; i++)
            {
                reference.Append(CultureInfo.InvariantCulture, $"k{i:D7},{i % 997},{(i % 1000) + 10.25m},x{i % 7}\n");
                candidate.Append(CultureInfo.InvariantCulture, $"k{rows - 1 - i:D7},{(rows - 1 - i) % 997},{((rows - 1 - i) % 1000) + 10.26m},x{(rows - 1 - i) % 7}\n");
            }

            var r = Mem.Bytes(reference.ToString());
            var c = Mem.Bytes(candidate.ToString());
            var sw = Stopwatch.StartNew();
            var result = CompareEngine.Compare(def, new CompareSources([new SourceFile("reference/a.csv", r)], [new SourceFile("candidate/a.csv", c)]));
            var json = MetricsJson.Render(result.Metrics!);
            sw.Stop();
            Console.WriteLine($"TIMING compare rows={rows} bytes={r.Length + c.Length} ms={sw.ElapsedMilliseconds} json-bytes={json.Length} {Mem.Counts(result)}");
        }
    }
}
