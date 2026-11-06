using System.Globalization;
using System.Security.Cryptography;

namespace GuardSpike;

// 작은 확인들. 각각 한 가지 사실만 본다.
internal static class Probes
{
    // string.GetHashCode는 프로세스마다 다른가. 같은 문자열로 두 번 실행해 값을 비교한다.
    public static int Hash(string[] args)
    {
        var s = args.Length > 0 ? args[0] : "abc";
        Console.WriteLine($"HASH {s} first={s.GetHashCode()} again={s.GetHashCode()}");
        return 0;
    }

    // Directory.EnumerateFiles 순서가 만든 순서·이름 순서와 같은가.
    public static int EnumOrder(string[] args)
    {
        var root = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(root);
        if (Directory.EnumerateFileSystemEntries(root).Any())
        {
            Console.Error.WriteLine("작업 폴더가 비어 있지 않다.");
            return 2;
        }

        Report(root, "small-cab", ["c.txt", "a.txt", "b.txt"]);
        Report(root, "small-abc", ["a.txt", "b.txt", "c.txt"]);
        var many = Enumerable.Range(0, 200).Select(i => $"f{i:000}.txt").ToArray();
        Report(root, "many-asc", many);
        Report(root, "many-desc", many.Reverse().ToArray());
        return 0;
    }

    private static void Report(string root, string name, string[] createOrder)
    {
        var dir = Path.Combine(root, name);
        Directory.CreateDirectory(dir);
        foreach (var f in createOrder)
        {
            File.WriteAllText(Path.Combine(dir, f), f);
        }

        var listed = Directory.EnumerateFiles(dir).Select(Path.GetFileName).ToArray();
        var sorted = listed.Order(StringComparer.Ordinal).ToArray();
        Console.WriteLine($"ENUM {name} n={listed.Length} sorted={listed.SequenceEqual(sorted)} asCreated={listed.SequenceEqual(createOrder)} first5={string.Join(",", listed.Take(5))}");
    }

    // 읽기 전용 속성을 준 파일에 쓰기가 막히는가. (실행 계정에 따라 다를 수 있다.)
    public static int ReadOnly(string[] args)
    {
        var root = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(root);
        var f = Path.Combine(root, "ro.txt");
        File.WriteAllText(f, "a");
        File.SetAttributes(f, FileAttributes.ReadOnly);
        var mode = OperatingSystem.IsWindows() ? "n/a" : File.GetUnixFileMode(f).ToString();
        string result;
        try
        {
            File.AppendAllText(f, "b");
            result = "write-succeeded";
        }
        catch (Exception ex)
        {
            result = "blocked:" + ex.GetType().Name;
        }

        Console.WriteLine($"READONLY attrs={File.GetAttributes(f)} mode={mode} append={result} content={File.ReadAllText(f)}");
        File.SetAttributes(f, FileAttributes.Normal);
        return 0;
    }

    public static int Sha(string[] args)
    {
        foreach (var f in args)
        {
            var bytes = File.ReadAllBytes(f);
            Console.WriteLine($"SHA {Convert.ToHexStringLower(SHA256.HashData(bytes))} size={bytes.Length.ToString(CultureInfo.InvariantCulture)} {f.Replace('\\', '/')}");
        }

        return 0;
    }
}
