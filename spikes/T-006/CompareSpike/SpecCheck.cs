using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Scorecard.Core.Comparison;

// 스펙 문서(작업 트리 사본)의 표·블록을 읽어 후보 동작·출력과 대조한다. 테스트의 SpecDocument와 같은 규칙.
// 줄마다 "SPEC <표> <결과>". 차이가 있으면 "SPEC-DIFF ...".
internal static class SpecCheck
{
    public const string PatternHeader = "| 패턴 | 받나 |";
    public const string MatchHeader = "| 패턴 | 파일 이름 | 맞나 |";
    public const string NumberHeader = "| 칸 | 숫자인가 | 값 |";
    public const string ToleranceHeader = "| 허용 오차 | 기준 값 | 결과 값 | 판정 |";
    public const string TrimHeader = "| 글자 | 이름 | 앞뒤에서 떼나 |";
    public const string PercentHeader = "| 일치 | 기준 행 | 정확도(%) |";
    public const string ErrorCodeHeader = "| 코드 | 언제 | 위치 | 메시지 |";
    public const string ExampleHeader = "| 예제 | 상태 | 기준 행 | 결과 행 | 일치 | 값 불일치 | 누락 | 추가 | 허용오차 안 차이 | 정확도(%) | 누락률(%) |";
    public const string PropertyHeader = "| 속성 | 형식 | 뜻 |";

    private static int diffs;

    private static void Diff(string table, string what)
    {
        diffs++;
        Console.WriteLine($"SPEC-DIFF {table} {what}");
    }

    public static int Run(string spec, string root)
    {
        Patterns(spec);
        Matches(spec);
        Numbers(spec);
        Tolerances(spec);
        Trims(spec);
        Percents(spec);
        ErrorCodes(spec);
        Examples(spec, root);
        Properties(spec, root);
        Blocks(spec, root);
        Console.WriteLine($"SPEC summary diffs={diffs}");
        return 0;
    }

    private static string U(string cell)
    {
        var v = cell.Trim();
        return v == "(빈 칸)" ? string.Empty : v.Trim('`');
    }

    private static bool Yes(string cell, string table) => U(cell) switch
    {
        "예" => true,
        "아니오" => false,
        _ => throw new InvalidOperationException($"{table}: 예/아니오가 아니다: {cell}"),
    };

    private static void Patterns(string spec)
    {
        var rows = Doc.Table(spec, PatternHeader);
        foreach (var c in rows)
        {
            if (CompareInputs.IsSupportedPattern(U(c[0])) != Yes(c[1], "pattern"))
            {
                Diff("pattern", c[0]);
            }
        }

        Console.WriteLine($"SPEC pattern rows={rows.Count}");
    }

    private static void Matches(string spec)
    {
        var rows = Doc.Table(spec, MatchHeader);
        foreach (var c in rows)
        {
            if (CompareInputs.Matches(U(c[0]), U(c[1])) != Yes(c[2], "match"))
            {
                Diff("match", $"{c[0]} {c[1]}");
            }
        }

        Console.WriteLine($"SPEC match rows={rows.Count}");
    }

    private static void Numbers(string spec)
    {
        var rows = Doc.Table(spec, NumberHeader);
        foreach (var c in rows)
        {
            var cell = U(c[0]);
            var expectedNumber = Yes(c[1], "number");
            var actual = Probes.NumberValue(cell);
            var viaEngine = Mem.Run(Mem.Case("compare:\n  - {field: v, tolerance: 0}\n"), $"id,v\nk,{Mem.Quote(cell)}\n", $"id,v\nk,{Mem.Quote(cell)}\n").Metrics is not null;
            if (viaEngine != expectedNumber || (expectedNumber && actual != U(c[2])) || (!expectedNumber && U(c[2]) != "—"))
            {
                Diff("number", $"'{cell}' engine={viaEngine} value={actual} spec={c[1]}/{c[2]}");
            }
        }

        Console.WriteLine($"SPEC number rows={rows.Count}");
    }

    private static void Tolerances(string spec)
    {
        var rows = Doc.Table(spec, ToleranceHeader);
        foreach (var c in rows)
        {
            var actual = Mem.Judge(U(c[0]), U(c[1]), U(c[2]));
            if (actual != U(c[3]))
            {
                Diff("tolerance", $"[{c[0]}] {c[1]} vs {c[2]}: engine={actual} spec={c[3]}");
            }
        }

        Console.WriteLine($"SPEC tolerance rows={rows.Count}");
    }

    private static void Trims(string spec)
    {
        var rows = Doc.Table(spec, TrimHeader);
        foreach (var c in rows)
        {
            var cp = U(c[0]);
            var ch = char.ConvertFromUtf32(int.Parse(cp[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            var same = Mem.Judge("없음", "item-a", ch + "item-a" + ch) == "같음";
            if (same != Yes(c[2], "trim"))
            {
                Diff("trim", cp);
            }
        }

        Console.WriteLine($"SPEC trim rows={rows.Count}");
    }

    private static void Percents(string spec)
    {
        var rows = Doc.Table(spec, PercentHeader);
        foreach (var c in rows)
        {
            var actual = Probes.PercentViaEngine(int.Parse(U(c[0]), CultureInfo.InvariantCulture), int.Parse(U(c[1]), CultureInfo.InvariantCulture));
            if (actual != U(c[2]))
            {
                Diff("percent", $"{c[0]}/{c[1]} engine={actual} spec={c[2]}");
            }
        }

        Console.WriteLine($"SPEC percent rows={rows.Count}");
    }

    // 표의 코드 집합 = Core CompareRules.Code* 상수 집합(리플렉션, 스파이크에서만).
    private static void ErrorCodes(string spec)
    {
        var rows = Doc.Table(spec, ErrorCodeHeader);
        var specCodes = rows.Select(c => U(c[0])).Order(StringComparer.Ordinal).ToArray();
        var rules = typeof(CompareEngine).Assembly.GetType("Scorecard.Core.Comparison.CompareRules")!;
        var coreCodes = rules.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.Name.StartsWith("Code", StringComparison.Ordinal))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!specCodes.SequenceEqual(coreCodes))
        {
            Diff("error-code", $"spec=[{string.Join(",", specCodes)}] core=[{string.Join(",", coreCodes)}]");
        }

        Console.WriteLine($"SPEC error-code rows={rows.Count} core={coreCodes.Length}");
    }

    private static void Examples(string spec, string root)
    {
        var rows = Doc.Table(spec, ExampleHeader);
        foreach (var c in rows)
        {
            var example = U(c[0]);
            var m = Parse(root, example);
            var compare = m.RootElement.GetProperty("compare");
            string N(string name) => compare.GetProperty(name).ValueKind == JsonValueKind.Null ? "—" : compare.GetProperty(name).GetRawText();
            string C(string name) => compare.GetProperty("counts").ValueKind == JsonValueKind.Null ? "—" : compare.GetProperty("counts").GetProperty(name).GetRawText();
            var actual = new[] { compare.GetProperty("status").GetString()!, C("reference"), C("candidate"), C("match"), C("mismatch"), C("missing"), C("extra"), C("within"), N("accuracy_pct"), N("missing_rate_pct") };
            var expected = c.Skip(1).Select(U).ToArray();
            if (!actual.SequenceEqual(expected))
            {
                Diff("example", $"{example} actual=[{string.Join(",", actual)}] spec=[{string.Join(",", expected)}]");
            }
        }

        Console.WriteLine($"SPEC example rows={rows.Count}");
    }

    private static JsonDocument Parse(string root, string example) =>
        JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, example, "expected", MetricsJson.FileName)));

    // 속성 표의 경로 순서 = table-compare metrics.json을 걸으며 처음 만난 경로 순서(배열은 []로).
    // mixed-exceptions의 경로는 table-compare 경로의 앞부분(배열 안 제외)과 같은 순서여야 한다.
    private static void Properties(string spec, string root)
    {
        var rows = Doc.Table(spec, PropertyHeader);
        var specPaths = rows.Select(c => U(c[0])).ToArray();
        var walked = new List<string>();
        Walk(Parse(root, "table-compare").RootElement, string.Empty, walked);
        if (!specPaths.SequenceEqual(walked))
        {
            Diff("property", $"spec=[{string.Join(",", specPaths)}] json=[{string.Join(",", walked)}]");
        }

        var nv = new List<string>();
        Walk(Parse(root, "mixed-exceptions").RootElement, string.Empty, nv);
        var top = walked.Where(p => !p.Contains("[]", StringComparison.Ordinal) && !p.StartsWith("compare.counts.", StringComparison.Ordinal)).ToArray();
        if (!nv.SequenceEqual(top))
        {
            Diff("property", $"not-verified=[{string.Join(",", nv)}] expected=[{string.Join(",", top)}]");
        }

        Console.WriteLine($"SPEC property rows={rows.Count} walked={walked.Count}");
    }

    private static void Walk(JsonElement e, string path, List<string> paths)
    {
        if (path.Length > 0 && !paths.Contains(path))
        {
            paths.Add(path);
        }

        if (e.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in e.EnumerateObject())
            {
                Walk(p.Value, path.Length == 0 ? p.Name : path + "." + p.Name, paths);
            }
        }
        else if (e.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in e.EnumerateArray())
            {
                if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    Walk(item, path + "[]", paths);
                }
            }
        }
    }

    // 7.2~7.4 블록 = 생성 metrics.json 바이트, SHA-256 줄 = 파일. 6.6 오류 블록 = guard 오류 출력.
    private static void Blocks(string spec, string root)
    {
        foreach (var example in Program.Examples)
        {
            var heading = Doc.Lines(spec).Single(l => l.StartsWith("### 7.", StringComparison.Ordinal) && l.EndsWith(" " + example, StringComparison.Ordinal));
            var blocks = Doc.CodeBlocks(spec, heading);
            var file = File.ReadAllBytes(Path.Combine(root, example, "expected", MetricsJson.FileName));
            var sha = Program.Sha(file);
            var line = Doc.Lines(spec).Where(l => l.StartsWith($"`examples/{example}/expected/metrics.json` (SHA-256 `", StringComparison.Ordinal)).ToArray();
            var lineOk = line.Length == 1 && line[0].Contains(sha, StringComparison.Ordinal);
            var blockOk = blocks.Count == 1 && Encoding.UTF8.GetBytes(blocks[0]).AsSpan().SequenceEqual(file);
            Console.WriteLine($"BLOCK {example} blocks={blocks.Count} block-equal={blockOk} sha-line={lineOk} sha={sha}");
            if (!lineOk || !blockOk)
            {
                Diff("block", example);
            }
        }

        var errorFile = Path.Combine(root, "guard", "compare-invalid.txt");
        if (File.Exists(errorFile))
        {
            var heading = Doc.Lines(spec).Single(l => l.StartsWith("### 6.", StringComparison.Ordinal) && l.Contains("compare-invalid", StringComparison.Ordinal));
            var blocks = Doc.CodeBlocks(spec, heading);
            var actual = File.ReadAllBytes(errorFile);
            var ok = blocks.Any(b => Encoding.UTF8.GetBytes(b).AsSpan().SequenceEqual(actual));
            Console.WriteLine($"BLOCK guard compare-invalid errors-block-equal={ok} blocks={blocks.Count}");
            if (!ok)
            {
                Diff("block", "compare-invalid");
            }

            // 표본 파일 블록: "`TestData/guard/compare-invalid/<상대 경로>`:" 줄 다음 블록 = <출력 루트>/guard/sample/<상대 경로>.
            const string prefix = "`TestData/guard/compare-invalid/";
            var lines = Doc.Lines(spec);
            var start = Array.IndexOf(lines, heading);
            string? label = null;
            List<string>? current = null;
            var checkedFiles = 0;
            for (var i = start + 1; i < lines.Length && (current is not null || !lines[i].StartsWith('#')); i++)
            {
                if (current is null && lines[i].StartsWith(prefix, StringComparison.Ordinal) && lines[i].EndsWith("`:", StringComparison.Ordinal))
                {
                    label = lines[i][prefix.Length..^2];
                }
                else if (lines[i].StartsWith("```", StringComparison.Ordinal))
                {
                    if (current is null)
                    {
                        current = [];
                    }
                    else
                    {
                        if (label is not null)
                        {
                            var expected = File.ReadAllBytes(Path.Combine(root, "guard", "sample", label));
                            var same = Encoding.UTF8.GetBytes(string.Concat(current.Select(l => l + "\n"))).AsSpan().SequenceEqual(expected);
                            checkedFiles++;
                            if (!same)
                            {
                                Diff("block", "compare-invalid sample " + label);
                            }
                        }

                        current = null;
                        label = null;
                    }
                }
                else
                {
                    current?.Add(lines[i]);
                }
            }

            var sampleCount = Directory.EnumerateFiles(Path.Combine(root, "guard", "sample"), "*", SearchOption.AllDirectories).Count();
            Console.WriteLine($"BLOCK guard compare-invalid sample-blocks={checkedFiles} sample-files={sampleCount}");
            if (checkedFiles != sampleCount)
            {
                Diff("block", "compare-invalid sample count");
            }
        }
    }
}

// 스펙 문서 읽기. 테스트의 SpecDocument와 같은 규칙(머리 행이 정확히 한 번, '\|'는 칸 구분 아님).
internal static class Doc
{
    public static string[] Lines(string path) => File.ReadAllLines(path);

    public static IReadOnlyList<string[]> Table(string path, string header)
    {
        var lines = Lines(path);
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

    public static IReadOnlyList<string> CodeBlocks(string path, string heading)
    {
        var lines = Lines(path);
        var start = Array.FindIndex(lines, l => l.TrimEnd() == heading);
        var blocks = new List<string>();
        List<string>? current = null;
        for (var i = start + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (current is null && line.StartsWith('#'))
            {
                break;
            }

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                if (current is null)
                {
                    current = [];
                }
                else
                {
                    blocks.Add(string.Concat(current.Select(l => l + "\n")));
                    current = null;
                }

                continue;
            }

            current?.Add(line);
        }

        return blocks;
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
