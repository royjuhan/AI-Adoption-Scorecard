using System.Text.RegularExpressions;

namespace GuardSpike;

// 소스 텍스트 검색. 작업 카드에 적힌 이름을 정규식으로 찾는다. 주석·문자열을 구분하지 않는다.
internal static class TextScan
{
    private static readonly (string Name, Regex Pattern)[] Patterns =
    [
        ("HttpClient", new Regex(@"\bHttpClient\b", RegexOptions.CultureInvariant)),
        ("Socket", new Regex(@"\bSocket\b", RegexOptions.CultureInvariant)),
        ("WebClient", new Regex(@"\bWebClient\b", RegexOptions.CultureInvariant)),
        ("Process.Start", new Regex(@"\bProcess\.Start\b", RegexOptions.CultureInvariant)),
        ("LLM", new Regex(@"\b(OpenAI|Anthropic)\b|api\.openai\.com|api\.anthropic\.com", RegexOptions.CultureInvariant)),
    ];

    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("text <폴더> [--project-files]");
            return 2;
        }

        var root = Path.GetFullPath(args[0]);
        string[] extensions = args.Contains("--project-files")
            ? [".cs", ".csproj", ".props", ".targets"]
            : [".cs"];

        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(r => extensions.Contains(Path.GetExtension(r), StringComparer.OrdinalIgnoreCase))
            .Where(r => !r.Split('/').Any(s => s is "bin" or "obj"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var count = 0;
        foreach (var rel in files)
        {
            var lines = File.ReadAllLines(Path.Combine(root, rel));
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (var (name, pattern) in Patterns)
                {
                    if (pattern.IsMatch(lines[i]))
                    {
                        Console.WriteLine($"FIND {rel}:{i + 1} {name} | {lines[i].Trim()}");
                        count++;
                    }
                }
            }
        }

        Console.WriteLine($"RESULT files={files.Length} findings={count}");
        return count == 0 ? 0 : 1;
    }
}
