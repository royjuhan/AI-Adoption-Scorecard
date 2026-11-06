using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Scorecard.Core.Cases;

// 스파이크 후보 C의 스키마 검사. 트리(YamlTree)를 걸으며 필드마다 형식·필수 여부를 본다.
// 숫자는 정규식으로 모양을 본 뒤 InvariantCulture로 읽는다. 현재 문화권을 쓰지 않는다.
public static class CaseParser
{
    public const string FileName = "case.yaml";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly Regex NamePattern = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant);
    private static readonly Regex IntegerPattern = new("^-?(0|[1-9][0-9]*)$", RegexOptions.CultureInvariant);
    private static readonly Regex NumberPattern = new("^-?(0|[1-9][0-9]*)(\\.[0-9]+)?$", RegexOptions.CultureInvariant);

    private static readonly string[] RootKeys = ["name", "task", "before", "after", "ai_usage", "steps", "inputs", "compare", "thresholds"];
    private static readonly string[] StepKeys = ["name", "answers"];
    private static readonly string[] AnswerKeys =
        ["input_kind", "rule_expressible", "frequency", "batch_size", "exception_rate", "current_actor", "minutes_per_item", "failure_impact", "existing_feature"];
    private static readonly string[] InputKeys = ["reference", "candidate", "key"];
    private static readonly string[] CompareKeys = ["field", "tolerance", "tolerance_pct"];
    private static readonly string[] ThresholdKeys = ["accuracy_min", "missing_max", "time_must_drop"];

    // case.yaml 하나를 읽는다. 오류 메시지에는 파일 이름만 쓴다(절대 경로 없음).
    public static CaseParseResult ParseDirectory(string caseDirectory)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(Path.Combine(caseDirectory, FileName));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return Fail(FileName, "file", "파일이 없다");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail(FileName, "file", "파일을 읽을 수 없다: " + ex.GetType().Name);
        }

        return Parse(bytes, FileName);
    }

    public static CaseParseResult Parse(byte[] content, string sourceName)
    {
        var sink = new ErrorSink(sourceName);
        var text = Decode(content, sink);
        if (text is null)
        {
            return new CaseParseResult(null, sink.Sorted());
        }

        var root = YamlTree.Read(text, sink);
        if (root is null)
        {
            return new CaseParseResult(null, sink.Sorted());
        }

        var model = ReadCase(root, sink);
        return sink.Count == 0 ? new CaseParseResult(model, []) : new CaseParseResult(null, sink.Sorted());
    }

    private static CaseParseResult Fail(string source, string code, string message) =>
        new(null, [new CaseError(source, 0, 0, code, "", message)]);

    // UTF-8만 받는다. BOM은 떼고, UTF-16 BOM과 잘못된 바이트는 오류.
    private static string? Decode(byte[] b, ErrorSink sink)
    {
        if (b.Length >= 2 && ((b[0] == 0xFF && b[1] == 0xFE) || (b[0] == 0xFE && b[1] == 0xFF)))
        {
            sink.Add(1, 0, "encoding", "", "UTF-16이다. UTF-8로 저장한다");
            return null;
        }

        var start = b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF ? 3 : 0;
        try
        {
            return StrictUtf8.GetString(b, start, b.Length - start);
        }
        catch (DecoderFallbackException ex)
        {
            var offset = start + ex.Index;
            var line = 1 + b.AsSpan(0, Math.Max(0, Math.Min(offset, b.Length))).Count((byte)'\n');
            sink.Add(line, 0, "encoding", "", $"UTF-8이 아니다 (바이트 {offset})");
            return null;
        }
    }

    private static CaseDefinition? ReadCase(YNode root, ErrorSink sink)
    {
        if (root is not YMapping map)
        {
            Type(sink, root, "", "매핑");
            return null;
        }

        var f = Fields(map, RootKeys, "", sink);
        var name = RequiredString(f, map, "name", "name", sink);
        if (name is not null && (name.Length > 64 || !NamePattern.IsMatch(name)))
        {
            var node = f["name"];
            sink.Add(node.Line, node.Column, "format", "name", "소문자·숫자·'-'만, 64자까지 쓴다");
        }

        var task = RequiredString(f, map, "task", "task", sink);
        var before = OptionalString(f, "before", "before", sink);
        var after = OptionalString(f, "after", "after", sink);
        var aiUsage = OptionalEnum(f, "ai_usage", "ai_usage", ["build-time", "runtime", "hybrid"], sink);
        var steps = ReadSteps(f, map, sink);
        var inputs = ReadInputs(f, sink);
        var compare = ReadCompare(f, sink);
        if (compare.Count > 0 && inputs is null && !f.ContainsKey("inputs"))
        {
            var node = f["compare"];
            sink.Add(node.Line, node.Column, "required", "inputs", "compare가 있으면 inputs가 있어야 한다");
        }

        var thresholds = ReadThresholds(f, sink);
        return name is null || task is null
            ? null
            : new CaseDefinition(name, task, before, after, aiUsage, steps, inputs, compare, thresholds);
    }

    private static IReadOnlyList<CaseStep> ReadSteps(Dictionary<string, YNode> f, YMapping parent, ErrorSink sink)
    {
        if (!f.TryGetValue("steps", out var node) || node is YScalar { IsNull: true })
        {
            Missing(sink, node ?? parent, "steps", node is null);
            return [];
        }

        if (node is not YSequence seq)
        {
            Type(sink, node, "steps", "목록");
            return [];
        }

        if (seq.Items.Count == 0)
        {
            sink.Add(seq.Line, seq.Column, "required", "steps", "단계가 1개 이상 있어야 한다");
        }

        var steps = new List<CaseStep>();
        var firstNumber = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < seq.Items.Count; i++)
        {
            var path = $"steps[{i + 1}]";
            if (seq.Items[i] is not YMapping item)
            {
                Type(sink, seq.Items[i], path, "매핑");
                continue;
            }

            var sf = Fields(item, StepKeys, path, sink);
            var name = RequiredString(sf, item, "name", path + ".name", sink);
            if (name is not null)
            {
                if (firstNumber.TryGetValue(name, out var first))
                {
                    var n = sf["name"];
                    sink.Add(n.Line, n.Column, "duplicate", path + ".name", $"단계 이름이 중복된다 (처음: steps[{first}])");
                }
                else
                {
                    firstNumber.Add(name, i + 1);
                }
            }

            var answers = ReadAnswers(sf, path + ".answers", sink);
            steps.Add(new CaseStep(i + 1, name ?? string.Empty, answers));
        }

        return steps;
    }

    // 3.2 기본값. 답이 없거나 null이면 이 값을 넣고 IsDefault = true.
    private static StepAnswers ReadAnswers(Dictionary<string, YNode> sf, string path, ErrorSink sink)
    {
        var af = new Dictionary<string, YNode>(StringComparer.Ordinal);
        if (sf.TryGetValue("answers", out var node) && node is not YScalar { IsNull: true })
        {
            if (node is YMapping map)
            {
                af = Fields(map, AnswerKeys, path, sink);
            }
            else
            {
                Type(sink, node, path, "매핑");
            }
        }

        return new StepAnswers(
            EnumAnswer(af, "input_kind", path, ["table", "fixed_form", "free_doc", "image", "conversation"], "fixed_form", sink),
            EnumAnswer(af, "rule_expressible", path, ["yes", "partial", "no"], "partial", sink),
            EnumAnswer(af, "frequency", path, ["daily", "weekly", "monthly", "rare"], "weekly", sink),
            IntegerAnswer(af, "batch_size", path, 1, 10, sink),
            NumberAnswer(af, "exception_rate", path, 0m, 100m, 20m, sink),
            EnumAnswer(af, "current_actor", path, ["human", "ai_each_time", "program", "mixed"], "human", sink),
            NullableNumberAnswer(af, "minutes_per_item", path, 0m, sink),
            EnumAnswer(af, "failure_impact", path, ["internal_rework", "external", "safety_legal"], "internal_rework", sink),
            EnumAnswer(af, "existing_feature", path, ["yes", "no", "unknown"], "unknown", sink));
    }

    private static CaseInputs? ReadInputs(Dictionary<string, YNode> f, ErrorSink sink)
    {
        if (!f.TryGetValue("inputs", out var node) || node is YScalar { IsNull: true })
        {
            return null;
        }

        if (node is not YMapping map)
        {
            Type(sink, node, "inputs", "매핑");
            return null;
        }

        var inf = Fields(map, InputKeys, "inputs", sink);
        var reference = RequiredRelativePath(inf, map, "reference", sink);
        var candidate = RequiredRelativePath(inf, map, "candidate", sink);
        var key = RequiredString(inf, map, "key", "inputs.key", sink);
        return reference is null || candidate is null || key is null ? null : new CaseInputs(reference, candidate, key);
    }

    private static string? RequiredRelativePath(Dictionary<string, YNode> f, YMapping parent, string key, ErrorSink sink)
    {
        var path = "inputs." + key;
        var value = RequiredString(f, parent, key, path, sink);
        if (value is null)
        {
            return null;
        }

        var segments = value.Split('/');
        if (value.Contains('\\') || value.StartsWith('/') || value.Contains(':') || segments.Contains(".."))
        {
            var node = f[key];
            sink.Add(node.Line, node.Column, "format", path, "케이스 폴더 기준 상대 경로를 '/'로 쓴다 ('..', ':', '\\' 없음)");
        }

        return value;
    }

    private static IReadOnlyList<CompareField> ReadCompare(Dictionary<string, YNode> f, ErrorSink sink)
    {
        if (!f.TryGetValue("compare", out var node) || node is YScalar { IsNull: true })
        {
            return [];
        }

        if (node is not YSequence seq)
        {
            Type(sink, node, "compare", "목록");
            return [];
        }

        var result = new List<CompareField>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < seq.Items.Count; i++)
        {
            var path = $"compare[{i + 1}]";
            if (seq.Items[i] is not YMapping item)
            {
                Type(sink, seq.Items[i], path, "매핑");
                continue;
            }

            var cf = Fields(item, CompareKeys, path, sink);
            var field = RequiredString(cf, item, "field", path + ".field", sink);
            if (field is not null)
            {
                if (seen.TryGetValue(field, out var first))
                {
                    var n = cf["field"];
                    sink.Add(n.Line, n.Column, "duplicate", path + ".field", $"비교 필드가 중복된다 (처음: compare[{first}])");
                }
                else
                {
                    seen.Add(field, i + 1);
                }
            }

            var tolerance = OptionalNumber(cf, "tolerance", path + ".tolerance", 0m, null, sink);
            var tolerancePct = OptionalNumber(cf, "tolerance_pct", path + ".tolerance_pct", 0m, null, sink);
            if (tolerance is not null && tolerancePct is not null)
            {
                sink.Add(item.Line, item.Column, "conflict", path, "tolerance와 tolerance_pct 중 하나만 쓴다");
            }

            result.Add(new CompareField(field ?? string.Empty, tolerance, tolerancePct));
        }

        return result;
    }

    private static CaseThresholds ReadThresholds(Dictionary<string, YNode> f, ErrorSink sink)
    {
        if (!f.TryGetValue("thresholds", out var node) || node is YScalar { IsNull: true })
        {
            return new CaseThresholds(null, null, null);
        }

        if (node is not YMapping map)
        {
            Type(sink, node, "thresholds", "매핑");
            return new CaseThresholds(null, null, null);
        }

        var tf = Fields(map, ThresholdKeys, "thresholds", sink);
        return new CaseThresholds(
            OptionalNumber(tf, "accuracy_min", "thresholds.accuracy_min", 0m, 100m, sink),
            OptionalNumber(tf, "missing_max", "thresholds.missing_max", 0m, 100m, sink),
            OptionalBoolean(tf, "time_must_drop", "thresholds.time_must_drop", sink));
    }

    // 매핑의 키를 모은다. 목록에 없는 키는 unknown-field.
    private static Dictionary<string, YNode> Fields(YMapping map, string[] allowed, string path, ErrorSink sink)
    {
        var fields = new Dictionary<string, YNode>(StringComparer.Ordinal);
        foreach (var (key, value) in map.Entries)
        {
            if (!allowed.Contains(key.Value, StringComparer.Ordinal))
            {
                sink.Add(key.Line, key.Column, "unknown-field", Join(path, key.Value), "알 수 없는 필드다");
                continue;
            }

            fields.Add(key.Value, value);
        }

        return fields;
    }

    private static string Join(string path, string key) => path.Length == 0 ? key : path + "." + key;

    private static void Missing(ErrorSink sink, YNode at, string path, bool absent) =>
        sink.Add(at.Line, at.Column, "required", path, absent ? "필수 필드가 없다" : "값이 비어 있다");

    private static void Type(ErrorSink sink, YNode node, string path, string expected)
    {
        if (node is not YInvalid)
        {
            sink.Add(node.Line, node.Column, "type", path, expected + "이어야 한다");
        }
    }

    private static string? RequiredString(Dictionary<string, YNode> f, YMapping parent, string key, string path, ErrorSink sink)
    {
        if (!f.TryGetValue(key, out var node))
        {
            Missing(sink, parent, path, absent: true);
            return null;
        }

        var value = ScalarText(node, path, sink);
        if (value is null || value.Trim().Length == 0)
        {
            if (node is YScalar)
            {
                Missing(sink, node, path, absent: false);
            }

            return null;
        }

        return value;
    }

    private static string? OptionalString(Dictionary<string, YNode> f, string key, string path, ErrorSink sink) =>
        f.TryGetValue(key, out var node) ? ScalarText(node, path, sink) : null;

    // 스칼라의 문자열. null 스칼라면 null. 스칼라가 아니면 type 오류.
    private static string? ScalarText(YNode node, string path, ErrorSink sink)
    {
        if (node is not YScalar s)
        {
            Type(sink, node, path, "스칼라 값");
            return null;
        }

        return s.IsNull ? null : s.Value;
    }

    private static string? OptionalEnum(Dictionary<string, YNode> f, string key, string path, string[] choices, ErrorSink sink)
    {
        if (!f.TryGetValue(key, out var node))
        {
            return null;
        }

        var value = ScalarText(node, path, sink);
        return value is null ? null : CheckChoice(node, value, path, choices, sink);
    }

    private static string? CheckChoice(YNode node, string value, string path, string[] choices, ErrorSink sink)
    {
        if (choices.Contains(value, StringComparer.Ordinal))
        {
            return value;
        }

        sink.Add(node.Line, node.Column, "enum", path, $"선택지 밖의 값 '{value}'. 가능한 값: {string.Join(", ", choices)}");
        return null;
    }

    private static Answer<string> EnumAnswer(Dictionary<string, YNode> af, string key, string path, string[] choices, string fallback, ErrorSink sink)
    {
        if (!af.TryGetValue(key, out var node))
        {
            return new Answer<string>(fallback, true);
        }

        var full = path + "." + key;
        var value = ScalarText(node, full, sink);
        if (value is null)
        {
            return new Answer<string>(fallback, node is YScalar);
        }

        return new Answer<string>(CheckChoice(node, value, full, choices, sink) ?? fallback, false);
    }

    private static Answer<int> IntegerAnswer(Dictionary<string, YNode> af, string key, string path, int min, int fallback, ErrorSink sink)
    {
        if (!af.TryGetValue(key, out var node) || node is YScalar { IsNull: true })
        {
            return new Answer<int>(fallback, true);
        }

        var full = path + "." + key;
        if (!PlainMatch(node, IntegerPattern, full, "정수", sink, out var text))
        {
            return new Answer<int>(fallback, false);
        }

        if (!long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) || value < min || value > int.MaxValue)
        {
            sink.Add(node.Line, node.Column, "range", full, $"범위 밖이다 ({min} 이상 {int.MaxValue} 이하): {text}");
            return new Answer<int>(fallback, false);
        }

        return new Answer<int>((int)value, false);
    }

    private static Answer<decimal> NumberAnswer(Dictionary<string, YNode> af, string key, string path, decimal min, decimal max, decimal fallback, ErrorSink sink)
    {
        if (!af.TryGetValue(key, out var node) || node is YScalar { IsNull: true })
        {
            return new Answer<decimal>(fallback, true);
        }

        var value = Number(node, path + "." + key, min, max, sink);
        return new Answer<decimal>(value ?? fallback, false);
    }

    private static Answer<decimal?> NullableNumberAnswer(Dictionary<string, YNode> af, string key, string path, decimal min, ErrorSink sink)
    {
        if (!af.TryGetValue(key, out var node) || node is YScalar { IsNull: true })
        {
            return new Answer<decimal?>(null, true);
        }

        return new Answer<decimal?>(Number(node, path + "." + key, min, null, sink), false);
    }

    private static decimal? OptionalNumber(Dictionary<string, YNode> f, string key, string path, decimal min, decimal? max, ErrorSink sink)
    {
        if (!f.TryGetValue(key, out var node) || node is YScalar { IsNull: true })
        {
            return null;
        }

        return Number(node, path, min, max, sink);
    }

    private static decimal? Number(YNode node, string path, decimal min, decimal? max, ErrorSink sink)
    {
        if (!PlainMatch(node, NumberPattern, path, "숫자", sink, out var text))
        {
            return null;
        }

        if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            || value < min || (max is not null && value > max))
        {
            var range = max is null ? $"{min.ToString(CultureInfo.InvariantCulture)} 이상" : $"{min.ToString(CultureInfo.InvariantCulture)} 이상 {max.Value.ToString(CultureInfo.InvariantCulture)} 이하";
            sink.Add(node.Line, node.Column, "range", path, $"범위 밖이다 ({range}): {text}");
            return null;
        }

        return value;
    }

    private static bool? OptionalBoolean(Dictionary<string, YNode> f, string key, string path, ErrorSink sink)
    {
        if (!f.TryGetValue(key, out var node) || node is YScalar { IsNull: true })
        {
            return null;
        }

        if (node is YScalar { Plain: true, Value: "true" })
        {
            return true;
        }

        if (node is YScalar { Plain: true, Value: "false" })
        {
            return false;
        }

        if (node is YScalar s)
        {
            sink.Add(node.Line, node.Column, "format", path, $"true 또는 false로 쓴다: {s.Value}");
        }
        else
        {
            Type(sink, node, path, "스칼라 값");
        }

        return null;
    }

    // 따옴표 없는 스칼라이고 정규식에 맞는가. 아니면 format 오류.
    private static bool PlainMatch(YNode node, Regex pattern, string path, string what, ErrorSink sink, out string text)
    {
        text = string.Empty;
        if (node is not YScalar s)
        {
            Type(sink, node, path, "스칼라 값");
            return false;
        }

        if (!s.Plain || !pattern.IsMatch(s.Value))
        {
            var hint = s.Plain ? string.Empty : " (따옴표 없이 쓴다)";
            sink.Add(node.Line, node.Column, "format", path, $"{what}가 아니다: {s.Value}{hint}");
            return false;
        }

        text = s.Value;
        return true;
    }
}
