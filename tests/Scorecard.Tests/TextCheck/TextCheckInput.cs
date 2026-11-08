using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.TextCheck;

// 문장 결과 테스트 도우미. 임시 케이스 폴더, 장면 표 "바꾼 곳" 적용, 파서 → 대조 → 판정 실행.
// 새 출력 속성(compare.checklist, scores 등)은 Core 형식이 아니라 metrics.json·judgment.json 바이트에서 읽는다.
internal static partial class TextCheckInput
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static byte[] Bytes(string text) => Utf8NoBom.GetBytes(text);

    public static string Text(byte[] bytes) => Utf8NoBom.GetString(bytes);

    // 바탕(7.3 reply-drafts 또는 6.2 score-only) 파일을 임시 폴더에 쓴다.
    public static TextCheckFolder Folder(string baseName) => TextCheckFolder.Create(TextCheckSpec.Base(baseName));

    // 바탕에 장면 표 "바꾼 곳"을 적용한 폴더.
    public static TextCheckFolder Folder(string baseName, string change)
    {
        var folder = Folder(baseName);
        try
        {
            Change(folder.Root, change);
            return folder;
        }
        catch
        {
            folder.Dispose();
            throw;
        }
    }

    // 4.4 장면 표 머리말의 "바꾼 곳". 묶음은 `; `로 잇고 차례로 한다. 옛 글이 없거나 옮길 행이 없으면 장면이 틀렸다(실패).
    // - `파일`, `파일`: `옛 글` → `새 글`[, …]: 파일마다 모든 곳을 바꾼다.
    // - `파일`[, …] (출처 `ai` 행): …: 그 출처의 데이터 행(행 끝 줄바꿈 포함)에서만 바꾼다. (채점자 `p2` 행)도 같다.
    //   행의 출처는 그 묶음 전 scores/sources.csv에서 그 행 가림 키의 출처다.
    // - `파일`의 출처 `ai` 행 → `새 파일`: 그 행을 새 파일로 옮긴다. 새 파일의 머리 행은 원래 파일과 같다.
    public static void Change(string root, string cell)
    {
        if (cell.Trim() == TextCheckSpec.NoChange)
        {
            return;
        }

        foreach (var group in GroupSeparator().Split(cell.Trim()))
        {
            var move = MoveGroup().Match(group);
            if (move.Success)
            {
                var (name, kind, value, target) = (move.Groups[1].Value, move.Groups[2].Value, move.Groups[3].Value, move.Groups[4].Value);
                var (rows, tail) = Scoped(Read(root, name), kind, value, SourcesOf(root));
                var moved = rows.Where(r => r.Hit).Select(r => r.Line).ToArray();
                Assert.True(moved.Length > 0, $"옮길 행이 없다: {group}");
                Assert.False(File.Exists(Path.Combine(root, target)), $"옮길 파일이 이미 있다: {group}");
                Write(root, name, string.Concat(rows.Where(r => !r.Hit).Select(r => r.Line)) + tail);
                Write(root, target, rows[0].Line + string.Concat(moved));
                continue;
            }

            var replace = ReplaceGroup().Match(group);
            Assert.True(replace.Success, $"바꾼 곳을 읽을 수 없다: {group}");
            var names = SpecDocument.CodeSpans(replace.Groups[1].Value);
            var pairs = PairPattern().Matches(replace.Groups[4].Value)
                .Select(m => (Old: TextCheckSpec.Unescape(m.Groups[1].Value), New: TextCheckSpec.Unescape(m.Groups[2].Value)))
                .ToArray();
            Assert.True(names.Length > 0 && pairs.Length > 0, $"바꾼 곳을 읽을 수 없다: {group}");
            var scopedKind = replace.Groups[2].Success ? replace.Groups[2].Value : null;
            var sources = scopedKind is null ? null : SourcesOf(root);
            foreach (var name in names)
            {
                var text = Read(root, name);
                if (scopedKind is null)
                {
                    foreach (var (old, @new) in pairs)
                    {
                        Assert.Contains(old, text, StringComparison.Ordinal);
                        text = text.Replace(old, @new, StringComparison.Ordinal);
                    }
                }
                else
                {
                    var (rows, tail) = Scoped(text, scopedKind, replace.Groups[3].Value, sources!);
                    foreach (var (old, @new) in pairs)
                    {
                        Assert.Contains(rows, r => r.Hit && r.Line.Contains(old, StringComparison.Ordinal));
                        rows = rows.Select(r => r.Hit ? (r.Line.Replace(old, @new, StringComparison.Ordinal), true) : r).ToList();
                    }

                    text = string.Concat(rows.Select(r => r.Line)) + tail;
                }

                Write(root, name, text);
            }
        }
    }

    // 파서 → 대조 → 판정. 파싱·대조 오류면 거기서 멈춘다(오류가 있으면 출력이 없다, 4.5).
    public static TextCheckRun Run(string root, JudgmentTimes? times = null, IReadOnlyList<string>? unexplained = null)
    {
        var parsed = CaseParser.ParseDirectory(root);
        if (parsed.Case is not { } definition)
        {
            return new TextCheckRun(parsed.Errors, null, null, null, null);
        }

        var compared = CompareEngine.CompareDirectory(root, definition);
        if (compared.Metrics is not { } metrics)
        {
            return new TextCheckRun(compared.Errors, definition, null, null, null);
        }

        var judged = JudgmentEngine.Judge(definition, metrics, times, unexplained);
        return new TextCheckRun([], definition, metrics, judged, (MetricsJson.Render(metrics), JudgmentJson.Render(judged)));
    }

    public static TextCheckRun Run(string baseName, string change)
    {
        using var folder = Folder(baseName, change);
        return Run(folder.Root);
    }

    // 오류 없이 끝나야 한다. 아니면 오류 줄을 보이고 실패한다.
    public static TextCheckRun Valid(TextCheckRun run)
    {
        Assert.True(run.IsValid, "오류가 없어야 한다:\n" + string.Join("\n", run.Errors.Select(e => e.ToString())));
        return run;
    }

    public static JsonElement Json(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        return document.RootElement.Clone();
    }

    // 속성 경로(처음 나온 순서). 배열 원소 객체는 "경로[]". 값은 그 경로의 모든 값.
    public static IReadOnlyList<(string Path, List<JsonElement> Values)> Paths(JsonElement root)
    {
        var order = new List<string>();
        var values = new Dictionary<string, List<JsonElement>>(StringComparer.Ordinal);
        void Add(string path, JsonElement value)
        {
            if (!values.TryGetValue(path, out var list))
            {
                order.Add(path);
                values[path] = list = [];
            }

            list.Add(value);
        }

        void Walk(JsonElement element, string prefix)
        {
            foreach (var property in element.EnumerateObject())
            {
                var path = prefix + property.Name;
                Add(path, property.Value);
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    Walk(property.Value, path + ".");
                }
                else if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in property.Value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object))
                    {
                        Add(path + "[]", item);
                        Walk(item, path + "[].");
                    }
                }
            }
        }

        Walk(root, string.Empty);
        return order.Select(p => (p, values[p])).ToArray();
    }

    public static string[] Names(JsonElement element) => element.EnumerateObject().Select(p => p.Name).ToArray();

    public static string[] Strings(JsonElement array) => array.EnumerateArray().Select(x => x.GetString()!).ToArray();

    // metrics.json scores.blind.findings의 규칙별 수.
    public static Dictionary<string, int> FindingCounts(JsonElement metrics) =>
        metrics.GetProperty("scores").GetProperty("blind").GetProperty("findings").EnumerateArray()
            .GroupBy(f => f.GetProperty("rule").GetString()!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

    // "`source_pattern` 2건" → {source_pattern: 2}. "—"는 빈 사전.
    public static Dictionary<string, int> FindingCounts(string cell) =>
        FindingCount().Matches(cell).ToDictionary(m => m.Groups["rule"].Value, m => int.Parse(m.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture), StringComparer.Ordinal);

    // 4.6 CSV 변형 7종. 같은 바이트의 metrics.json·judgment.json을 내야 한다.
    public static IReadOnlyDictionary<string, byte[]> CsvVariants(byte[] data)
    {
        var text = Text(data);
        var lines = text.EndsWith('\n') ? text.Split('\n')[..^1] : text.Split('\n');
        var quoted = string.Join('\n', lines.Select(l => string.Join(',', l.Split(',').Select(c => "\"" + c.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"")))) + "\n";
        var spaced = string.Join('\n', lines.Select(l => string.Join(',', l.Split(',').Select(c => " " + c + "\t")))) + "\n";
        return new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["crlf"] = Bytes(text.Replace("\n", "\r\n", StringComparison.Ordinal)),
            ["cr"] = Bytes(text.Replace('\n', '\r')),
            ["bom"] = [0xEF, 0xBB, 0xBF, .. data],
            ["quoted"] = Bytes(quoted),
            ["no-final-newline"] = Bytes(text.TrimEnd('\n')),
            ["blank-lines-at-end"] = Bytes(text + "\n\n,,\n"),
            ["spaces-around-cells"] = Bytes(spaced),
        };
    }

    public static string Read(string root, string relative) => Text(File.ReadAllBytes(Path.Combine(root, relative)));

    public static void Write(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Bytes(text));
    }

    // 그때의 scores/sources.csv: 가림 키 → 출처. 바탕 CSV는 따옴표가 없다(7.2).
    private static Dictionary<string, string> SourcesOf(string root)
    {
        var lines = Read(root, "scores/sources.csv").Split('\n');
        var header = lines[0].Split(',');
        var (blind, source) = (Array.IndexOf(header, "blind"), Array.IndexOf(header, "source"));
        return lines.Skip(1).Where(l => l.Length > 0).Select(l => l.Split(',')).ToDictionary(c => c[blind], c => c[source], StringComparer.Ordinal);
    }

    // 데이터 행(줄바꿈 포함)마다 범위 안인지. 출처 `x` 행은 가림 키의 출처, 채점자 `x` 행은 rater 칸으로 본다.
    private static (List<(string Line, bool Hit)> Rows, string Tail) Scoped(string text, string kind, string value, IReadOnlyDictionary<string, string> sources)
    {
        var lines = text.Split('\n');
        var column = Array.IndexOf(lines[0].Split(','), kind == "출처" ? "blind" : "rater");
        Assert.True(column >= 0, $"범위 열이 없다: {kind}");
        var rows = new List<(string, bool)>();
        for (var k = 0; k < lines.Length - 1; k++)
        {
            var cells = lines[k].Split(',');
            var cellValue = column < cells.Length ? cells[column] : null;
            var hit = k > 0 && lines[k].Length > 0 && cellValue is not null &&
                (kind == "출처" ? sources.TryGetValue(cellValue, out var s) && s == value : cellValue == value);
            rows.Add((lines[k] + "\n", hit));
        }

        return (rows, lines[^1]);
    }

    [GeneratedRegex(@"(?<=`); (?=`)", RegexOptions.CultureInvariant)]
    private static partial Regex GroupSeparator();

    [GeneratedRegex(@"^`([^`]+)`의 (출처|채점자) `([^`]+)` 행 → `([^`]+)`\z", RegexOptions.CultureInvariant)]
    private static partial Regex MoveGroup();

    [GeneratedRegex(@"^((?:`[^`]+`(?:, )?)+?)(?: \((출처|채점자) `([^`]+)` 행\))?: (`.*)\z", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex ReplaceGroup();

    [GeneratedRegex(@"`([^`]*)` → `([^`]*)`", RegexOptions.CultureInvariant)]
    private static partial Regex PairPattern();

    [GeneratedRegex(@"`(?<rule>[a-z_]+)` (?<n>\d+)건", RegexOptions.CultureInvariant)]
    private static partial Regex FindingCount();
}

// 한 번 돌린 결과. 오류가 있으면 Metrics·Judgment·Output이 null이다.
internal sealed record TextCheckRun(
    IReadOnlyList<CaseError> Errors,
    CaseDefinition? Definition,
    CompareMetrics? Metrics,
    JudgmentResult? Judgment,
    (byte[] Metrics, byte[] Judgment)? Output)
{
    public bool IsValid => Output is not null;

    public JsonElement MetricsJson => TextCheckInput.Json(Output!.Value.Metrics);

    public JsonElement JudgmentJson => TextCheckInput.Json(Output!.Value.Judgment);

    public string[] Codes => Errors.Select(e => e.Code).ToArray();
}

// 임시 케이스 폴더. Dispose에서 지운다.
internal sealed class TextCheckFolder : IDisposable
{
    private TextCheckFolder(string root)
    {
        Root = root;
    }

    public string Root { get; }

    public static TextCheckFolder Create(IReadOnlyDictionary<string, byte[]> files)
    {
        var folder = new TextCheckFolder(Directory.CreateTempSubdirectory("scorecard-text-check-").FullName);
        foreach (var (relative, bytes) in files)
        {
            var path = Path.Combine(folder.Root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }

        return folder;
    }

    public void Dispose() => CaseInput.TryDeleteDirectory(Root);
}
