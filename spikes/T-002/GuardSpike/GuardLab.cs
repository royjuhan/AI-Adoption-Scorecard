using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GuardSpike;

// 결정론·원본 불변 도우미 실험. 가짜 처리 대상(입력 폴더 → 출력 폴더)을 돌려
// 어떤 비교 방법이 어떤 위반을 잡는지 표로 낸다.
// 작업 폴더 이름은 고정 규칙으로 만든다. 지우지 않는다(빈 작업 폴더를 받아야 한다).
internal static class GuardLab
{
    private sealed record Entry(string Path, bool IsDir, long Size, string Sha, long MtimeTicks);

    // 입력 파일·폴더의 수정 시각을 이 값으로 맞춘다. 쓰기가 일어나면 현재 시각으로 바뀌어 차이가 난다.
    private static readonly DateTime FixedTime = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private static readonly (string Name, Action<string, string> Run)[] Fakes =
    [
        ("good", Good),
        ("culture-format", CultureFormat),
        ("clock-today", (i, o) => { Good(i, o); Append(o, DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); }),
        ("clock-utcnow", (i, o) => { Good(i, o); Append(o, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture)); }),
        ("guid", (i, o) => { Good(i, o); Append(o, Guid.NewGuid().ToString()); }),
        ("abs-path", (i, o) => { Good(i, o); Append(o, Path.GetFullPath(i)); }),
        ("enum-order", EnumOrder),
        ("string-hashcode", (i, o) => { Good(i, o); Append(o, "abc".GetHashCode().ToString(CultureInfo.InvariantCulture)); }),
        ("input-rewrite-same", (i, o) => { var f = Path.Combine(i, "case.yaml"); File.WriteAllBytes(f, File.ReadAllBytes(f)); Good(i, o); }),
        ("input-append", (i, o) => { File.AppendAllText(Path.Combine(i, "case.yaml"), "# x\n"); Good(i, o); }),
        ("input-new-file", (i, o) => { File.WriteAllText(Path.Combine(i, "extra.tmp"), "x"); Good(i, o); }),
        ("input-temp-file", (i, o) => { var f = Path.Combine(i, "work.tmp"); File.WriteAllText(f, "x"); File.Delete(f); Good(i, o); }),
        ("input-delete", (i, o) => { var f = Path.Combine(i, "notes.md"); if (File.Exists(f)) { File.Delete(f); } Good(i, o); }),
        ("input-touch-mtime", (i, o) => { File.SetLastWriteTimeUtc(Path.Combine(i, "case.yaml"), DateTime.UtcNow); Good(i, o); }),
        ("write-sibling", (i, o) => { File.WriteAllText(Path.Combine(i, "..", "sibling.txt"), "x"); Good(i, o); }),
    ];

    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("guard <빈 작업폴더>");
            return 2;
        }

        var root = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(root);
        if (Directory.EnumerateFileSystemEntries(root).Any())
        {
            Console.Error.WriteLine("작업 폴더가 비어 있지 않다. 새 폴더를 주세요.");
            return 2;
        }

        foreach (var name in new[] { "", "ko-KR", "de-DE", "en-US" })
        {
            var c = CultureInfo.GetCultureInfo(name);
            var parsed = double.TryParse("1.5", NumberStyles.Float | NumberStyles.AllowThousands, c, out var v) ? v.ToString(CultureInfo.InvariantCulture) : "실패";
            Console.WriteLine($"CULTURE [{(name.Length == 0 ? "invariant" : name)}] 1.5.ToString()={1.5.ToString(c)} Parse(\"1.5\")={parsed} date={new DateTime(2026, 11, 6).ToString(c)}");
        }

        Console.WriteLine($"CULTURE current={CultureInfo.CurrentCulture.Name} ui={CultureInfo.CurrentUICulture.Name}");

        var n = 0;
        foreach (var (name, run) in Fakes)
        {
            var im = Immutability(Path.Combine(root, $"{n:00}-{name}-im"), run);
            var same = DetSame(Path.Combine(root, $"{n:00}-{name}-same"), run);
            var move = DetMove(Path.Combine(root, $"{n:00}-{name}-move-a"), Path.Combine(root, $"{n:00}-{name}-move-b"), run);
            var culture = DetCulture(Path.Combine(root, $"{n:00}-{name}-culture"), run);
            Console.WriteLine($"GUARD {name,-20} I-hash={im.Hash,-4} I-full={im.Full,-4} I-mtime={im.Mtime,-4} D-same={same,-4} D-move={move,-4} D-culture={culture,-4}");
            n++;
        }

        return 0;
    }

    // 원본 불변: 작업 폴더(출력 폴더 제외)를 실행 전후로 비교한다.
    // I-hash: 원래 있던 파일의 SHA-256만 (카드 문구 그대로). I-full: 항목 목록·크기·SHA-256. I-mtime: I-full + 수정 시각.
    private static (string Hash, string Full, string Mtime) Immutability(string work, Action<string, string> run)
    {
        var input = Path.Combine(work, "input");
        var output = Path.Combine(work, "out");
        MakeSample(input, reverse: false);
        FixTimes(work);
        var before = Snapshot(work, exclude: "out");
        var error = Try(() => run(input, output));
        var after = Snapshot(work, exclude: "out");

        var afterByPath = after.ToDictionary(e => e.Path, StringComparer.Ordinal);
        var hashOk = before.Where(e => !e.IsDir).All(e => afterByPath.TryGetValue(e.Path, out var a) && a.Sha == e.Sha);
        var fullOk = before.Select(e => e with { MtimeTicks = 0 }).SequenceEqual(after.Select(e => e with { MtimeTicks = 0 }));
        var mtimeOk = before.SequenceEqual(after);
        return (Mark(hashOk, error), Mark(fullOk, error), Mark(mtimeOk, error));
    }

    // 결정론 1: 같은 작업 폴더에서 두 번, 출력 폴더만 다르게.
    private static string DetSame(string work, Action<string, string> run)
    {
        var input = Path.Combine(work, "input");
        MakeSample(input, reverse: false);
        var e1 = Try(() => run(input, Path.Combine(work, "out1")));
        var e2 = Try(() => run(input, Path.Combine(work, "out2")));
        return Mark(Same(Path.Combine(work, "out1"), Path.Combine(work, "out2")), e1 ?? e2);
    }

    // 결정론 2: 다른 경로의 작업 폴더 두 곳. 두 번째는 입력 파일을 거꾸로 된 순서로 만든다.
    private static string DetMove(string workA, string workB, Action<string, string> run)
    {
        MakeSample(Path.Combine(workA, "input"), reverse: false);
        MakeSample(Path.Combine(workB, "input"), reverse: true);
        var e1 = Try(() => run(Path.Combine(workA, "input"), Path.Combine(workA, "out")));
        var e2 = Try(() => run(Path.Combine(workB, "input"), Path.Combine(workB, "out")));
        return Mark(Same(Path.Combine(workA, "out"), Path.Combine(workB, "out")), e1 ?? e2);
    }

    // 결정론 3: 같은 작업 폴더에서 문화권만 다르게 (invariant, de-DE).
    private static string DetCulture(string work, Action<string, string> run)
    {
        var input = Path.Combine(work, "input");
        MakeSample(input, reverse: false);
        var e1 = WithCulture(CultureInfo.InvariantCulture, () => Try(() => run(input, Path.Combine(work, "out1"))));
        var e2 = WithCulture(CultureInfo.GetCultureInfo("de-DE"), () => Try(() => run(input, Path.Combine(work, "out2"))));
        return Mark(Same(Path.Combine(work, "out1"), Path.Combine(work, "out2")), e1 ?? e2);
    }

    private static bool Same(string outA, string outB) =>
        Snapshot(outA, exclude: null).Select(e => e with { MtimeTicks = 0 })
            .SequenceEqual(Snapshot(outB, exclude: null).Select(e => e with { MtimeTicks = 0 }));

    private static string Mark(bool ok, string? error) => error is not null ? "ERR" : ok ? "ok" : "FAIL";

    private static string? Try(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception ex)
        {
            return ex.GetType().Name;
        }
    }

    private static T WithCulture<T>(CultureInfo culture, Func<T> func)
    {
        var (c, ui) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        try
        {
            return func();
        }
        finally
        {
            CultureInfo.CurrentCulture = c;
            CultureInfo.CurrentUICulture = ui;
        }
    }

    // 합성 입력. 실제 값 없음. 텍스트는 LF, 바이너리 1개 포함.
    private static void MakeSample(string input, bool reverse)
    {
        (string Rel, byte[] Bytes)[] files =
        [
            ("blob.bin", Enumerable.Range(0, 256).Select(i => (byte)i).ToArray()),
            ("case.yaml", Utf8NoBom.GetBytes("name: guard-sample\nsteps: 2\n")),
            ("data/sample.csv", Utf8NoBom.GetBytes("id,value\n1,1.5\n2,2.25\n")),
            ("notes.md", Utf8NoBom.GetBytes("# 합성 메모\n실제 값 없음.\n")),
        ];

        foreach (var (rel, bytes) in reverse ? files.Reverse() : files)
        {
            var path = Path.Combine(input, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }
    }

    // 작업 폴더 아래 모든 파일·폴더의 수정 시각을 고정값으로. 깊은 것부터.
    private static void FixTimes(string root)
    {
        foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetLastWriteTimeUtc(f, FixedTime);
        }

        foreach (var d in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
        {
            Directory.SetLastWriteTimeUtc(d, FixedTime);
        }

        Directory.SetLastWriteTimeUtc(root, FixedTime);
    }

    // 폴더 아래 항목 목록. 경로는 '/' 구분, 서수 정렬. 폴더도 항목으로 넣는다.
    private static List<Entry> Snapshot(string root, string? exclude)
    {
        var list = new List<Entry>();
        if (!Directory.Exists(root))
        {
            return list;
        }

        foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (exclude is not null && (rel == exclude || rel.StartsWith(exclude + "/", StringComparison.Ordinal)))
            {
                continue;
            }

            if (Directory.Exists(path))
            {
                list.Add(new Entry(rel + "/", true, 0, "", Directory.GetLastWriteTimeUtc(path).Ticks));
            }
            else
            {
                var bytes = File.ReadAllBytes(path);
                list.Add(new Entry(rel, false, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)), File.GetLastWriteTimeUtc(path).Ticks));
            }
        }

        list.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        return list;
    }

    private static string[] RelFiles(string input) =>
        Directory.EnumerateFiles(input, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(input, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

    // 정상 대상: 파일을 서수 정렬로 읽고, 값은 InvariantCulture로 읽고 쓴다.
    private static void Good(string input, string output)
    {
        Directory.CreateDirectory(output);
        var sb = new StringBuilder();
        foreach (var rel in RelFiles(input))
        {
            sb.Append(rel).Append(' ').Append(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(input, rel))))).Append('\n');
        }

        var sum = Values(input, CultureInfo.InvariantCulture).Sum();
        sb.Append("sum ").Append(sum.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        File.WriteAllText(Path.Combine(output, "summary.txt"), sb.ToString(), Utf8NoBom);
    }

    // 문화권 의존 대상: 현재 문화권으로 값을 읽고 쓴다.
    private static void CultureFormat(string input, string output)
    {
        Directory.CreateDirectory(output);
        var sum = Values(input, CultureInfo.CurrentCulture).Sum();
        File.WriteAllText(Path.Combine(output, "summary.txt"), "sum " + sum.ToString() + "\n", Utf8NoBom);
    }

    // 열거 순서 의존 대상: 정렬하지 않고 EnumerateFiles 순서대로 쓴다.
    private static void EnumOrder(string input, string output)
    {
        Directory.CreateDirectory(output);
        var names = Directory.EnumerateFiles(input).Select(Path.GetFileName);
        File.WriteAllText(Path.Combine(output, "order.txt"), string.Join("\n", names) + "\n", Utf8NoBom);
    }

    private static IEnumerable<double> Values(string input, IFormatProvider provider) =>
        File.ReadAllLines(Path.Combine(input, "data", "sample.csv"))
            .Skip(1)
            .Select(l => double.Parse(l.Split(',')[1], NumberStyles.Float | NumberStyles.AllowThousands, provider));

    private static void Append(string output, string line) =>
        File.AppendAllText(Path.Combine(output, "summary.txt"), line + "\n", Utf8NoBom);
}
