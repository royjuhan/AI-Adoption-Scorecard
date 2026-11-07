using System.Text.Json;
using Scorecard.Core.Judgment;
using Scorecard.Tests.Classification;
using Scorecard.Tests.Comparison;

namespace Scorecard.Tests.Judgment;

// 스펙 3.2·3.3 judgment.json(잠정 결정 7). 카드 수용 기준 4. ADR 0004 결정 2: 첫 키 "schema_version" = 정수 1.
// 바이트 규칙은 classification.md 3.3·metrics.md 3.3과 같다: UTF-8(BOM 없음), LF, 들여쓰기 2칸, 끝에 \n 하나, UnsafeRelaxedJsonEscaping.
// 쓰지 않는 것: 절대 경로, 입력 해시, 대조 셈(counts), 시간 값 원본, 사람 입력 원본.
[Trait("Spec", "judgment")]
public class JudgmentJsonTests
{
    // 형식 칸 → 그 경로에 올 수 있는 JSON 값 종류.
    private static readonly Dictionary<string, Func<JsonElement, bool>> Formats = new(StringComparer.Ordinal)
    {
        ["정수"] = e => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out _) && !e.GetRawText().Contains('.', StringComparison.Ordinal),
        ["숫자"] = e => e.ValueKind == JsonValueKind.Number,
        ["문자열"] = e => e.ValueKind == JsonValueKind.String,
        ["불리언"] = e => e.ValueKind is JsonValueKind.True or JsonValueKind.False,
        ["객체"] = e => e.ValueKind == JsonValueKind.Object,
        ["문자열 배열"] = e => e.ValueKind == JsonValueKind.Array && e.EnumerateArray().All(x => x.ValueKind == JsonValueKind.String),
        ["객체 배열"] = e => e.ValueKind == JsonValueKind.Array && e.EnumerateArray().All(x => x.ValueKind == JsonValueKind.Object),
    };

    // 출력 모음: 공개 예제 4종 + 판정 표의 장면 일곱 + 기본값 장면 + 미검증 no_compare_fields.
    private static IEnumerable<(string Label, byte[] Bytes)> Outputs()
    {
        foreach (var example in ClassificationInput.Examples)
        {
            yield return (example, JudgmentGolden.ExampleBytes(example));
        }

        foreach (var row in JudgmentSpec.VerdictRows().Where(r => r.Rule != JudgmentSpec.NotVerifiedRule))
        {
            yield return ($"{row.Rule} {row.Result}", VerdictTableTests.SceneFor(row.Rule, row.Result ?? JudgmentSpec.None).Render());
        }

        yield return ("defaults", new JudgmentScene(Columns: [], Rows: JudgmentInput.Rows(40, mismatch: 2), Extras: [], null, null, null).Render());
        yield return ("no_compare_fields", JudgmentJson.Render(JudgmentInput.JudgeNotVerified(JudgmentInput.NoCompareCase())));
    }

    // 속성 표의 경로 순서 = table-compare 출력을 걸으며 처음 만난 경로 순서(배열 원소는 []).
    [Fact]
    public void Property_table_paths_follow_table_compare_output_order()
    {
        var table = JudgmentSpec.Table(JudgmentSpec.PropertyHeader).Select(c => JudgmentSpec.Cell(c[0])).ToArray();
        Assert.NotEmpty(table);

        foreach (var bytes in new[] { JudgmentGolden.ExampleBytes("table-compare"), JudgmentGolden.ExpectedBytes("table-compare") })
        {
            using var json = JsonDocument.Parse(bytes);
            Assert.Equal(table, Paths(json.RootElement));
        }
    }

    // 속성 표의 형식 칸: 출력 모음의 모든 값이 그 경로의 형식이다. 표에 없는 경로가 없다.
    [Fact]
    public void Values_have_property_table_formats()
    {
        var formats = JudgmentSpec.Table(JudgmentSpec.PropertyHeader).ToDictionary(c => JudgmentSpec.Cell(c[0]), c => c[1].Trim(), StringComparer.Ordinal);
        var wrong = new List<string>();

        foreach (var (label, bytes) in Outputs())
        {
            using var json = JsonDocument.Parse(bytes);
            foreach (var (path, value) in Values(json.RootElement, string.Empty))
            {
                if (!formats.TryGetValue(path, out var format))
                {
                    wrong.Add($"{label}: 표에 없는 경로 {path}");
                    continue;
                }

                Assert.True(Formats.ContainsKey(format), $"속성 표 {path}의 형식 '{format}'을 모른다.");
                if (!Formats[format](value))
                {
                    wrong.Add($"{label}: {path} = {value.GetRawText()}, 형식 {format}");
                }
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    // 값의 글자: verdict는 판정 넷, rule은 4.3 근거 키, checks[].rule·result는 4.4 표, todo[].key는 4.7 표의 키.
    [Fact]
    public void String_values_come_from_spec_tables()
    {
        var verdicts = JudgmentSpec.VerdictRows().Select(r => r.Verdict).ToHashSet(StringComparer.Ordinal);
        var rules = JudgmentSpec.VerdictRows().Select(r => r.Rule).ToHashSet(StringComparer.Ordinal);
        var checks = JudgmentSpec.CheckRows();
        var todo = JudgmentSpec.TodoOrder();

        foreach (var (label, bytes) in Outputs())
        {
            using var json = JsonDocument.Parse(bytes);
            var root = json.RootElement;
            Assert.True(verdicts.Contains(root.GetProperty("verdict").GetString()!), label);
            Assert.True(rules.Contains(root.GetProperty("rule").GetString()!), label);
            Assert.All(root.GetProperty("checks").EnumerateArray(), c =>
                Assert.Contains(checks, r => r.Check == c.GetProperty("rule").GetString() && r.Result == c.GetProperty("result").GetString()));
            Assert.All(root.GetProperty("todo").EnumerateArray(), t => Assert.Contains(t.GetProperty("key").GetString(), todo));
        }
    }

    // 첫 키는 "schema_version", 값은 정수 1(JudgmentJson.SchemaVersion). 객체 속성 이름·순서는 3.2 모양 블록과 같다.
    [Fact]
    public void Objects_follow_shape_block_order_with_integer_schema_version_first()
    {
        using var shape = JsonDocument.Parse(JudgmentSpec.ShapeBlock());
        var top = Names(shape.RootElement);
        var thresholds = Names(shape.RootElement.GetProperty("thresholds"));
        var check = Names(shape.RootElement.GetProperty("checks")[0]);
        var todo = Names(shape.RootElement.GetProperty("todo")[0]);
        Assert.Equal(["schema_version", "case", "verdict", "rule", "reason", "thresholds", "defaults", "checks", "todo"], top);
        Assert.Equal(1, shape.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal(1, JudgmentJson.SchemaVersion);

        foreach (var (label, bytes) in Outputs())
        {
            var text = CompareInput.Text(bytes);
            Assert.True(text.StartsWith("{\n  \"schema_version\": 1,\n  \"case\": ", StringComparison.Ordinal), $"{label}의 첫 키가 \"schema_version\": 1이 아니다.");
            using var json = JsonDocument.Parse(bytes);
            var root = json.RootElement;
            Assert.True(top.SequenceEqual(Names(root)), label);
            Assert.True(thresholds.SequenceEqual(Names(root.GetProperty("thresholds"))), label);
            Assert.All(root.GetProperty("checks").EnumerateArray(), c => Assert.Equal(check, Names(c)));
            Assert.All(root.GetProperty("todo").EnumerateArray(), t => Assert.Equal(todo, Names(t)));
        }
    }

    // BOM 없음, CR 없음, 끝은 "}\n" 하나. 들여쓰기는 2칸 단위다.
    [Fact]
    public void Bytes_are_utf8_without_bom_with_lf_and_single_trailing_newline()
    {
        foreach (var (label, bytes) in Outputs())
        {
            Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], $"{label}: BOM이 없어야 한다.");
            Assert.DoesNotContain((byte)'\r', bytes);
            Assert.True(bytes is [.., (byte)'\n', (byte)'}', (byte)'\n'], $"{label}: 끝이 \"\\n}}\\n\"이어야 한다.");
            Assert.All(CompareInput.Text(bytes).TrimEnd('\n').Split('\n'), line => Assert.Equal(0, (line.Length - line.TrimStart(' ').Length) % 2));
        }
    }

    // 3.3 이스케이프(classification.md 3.3 [P:escape]과 같다): 유형 값의 큰따옴표는 \", 이모지(U+1F600)는 \uD83D\uDE00(서로게이트 쌍).
    // 한글·<>&'+`는 그대로. 메모리 문장에는 글자 그대로 있다(5절: 문장에는 CompareRules.Show 표시, JSON은 3.3 이스케이프).
    [Fact]
    public void Type_value_with_quote_and_emoji_is_escaped_in_json()
    {
        const string value = "값\"<>&'+`\U0001F600";
        var scene = new JudgmentScene(
            Columns: [JudgmentInput.Group],
            Rows: JudgmentInput.Rows(6, mismatch: 2, types: i => [i < 2 ? value : "b"]),
            Extras: [],
            AccuracyMin: "0",
            MissingMax: "100",
            TimeMustDrop: "false",
            Unexplained: []);

        var judged = scene.Judge();
        var text = CompareInput.Text(JudgmentJson.Render(judged));

        Assert.Equal((JudgmentEngine.VerdictConditional, JudgmentEngine.RuleFocus), (judged.Verdict, judged.Rule));
        Assert.Contains($"{JudgmentInput.Group} = {value}인 행", judged.Reason, StringComparison.Ordinal);
        Assert.Contains("\n  \"reason\": \"정확도·누락률은 문턱을 충족하나 불일치 2건이 모두 grp = 값\\\"<>&'+`\\uD83D\\uDE00인 행이므로 조건부.\",\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\U0001F600", text, StringComparison.Ordinal);
    }

    // 3.3: 문턱 숫자는 decimal 그대로(자릿수 유지): 85, 1.0, 66.6, 0.0. 문장 안의 문턱도 case.yaml 글자 그대로다.
    [Theory]
    [InlineData("66.6", "1.0")]
    [InlineData("85", "0.0")]
    public void Threshold_decimals_keep_their_scale(string accuracyMin, string missingMax)
    {
        var scene = new JudgmentScene(Columns: [], Rows: JudgmentInput.Rows(3), Extras: [], accuracyMin, missingMax, "false");
        var judged = scene.Judge();
        var text = CompareInput.Text(JudgmentJson.Render(judged));

        Assert.Contains($"\n    \"accuracy_min\": {accuracyMin},\n", text, StringComparison.Ordinal);
        Assert.Contains($"\n    \"missing_max\": {missingMax},\n", text, StringComparison.Ordinal);
        Assert.Contains("\n    \"time_must_drop\": false\n", text, StringComparison.Ordinal);
        Assert.Contains($"문턱 {accuracyMin}% 이상이다.", JudgmentInput.CheckOf(judged, JudgmentSpec.Accuracy).Reason, StringComparison.Ordinal);
        Assert.Contains($"문턱 {missingMax}% 이하다.", JudgmentInput.CheckOf(judged, JudgmentSpec.Missing).Reason, StringComparison.Ordinal);
    }

    // 빈 배열은 한 줄 []다: table-compare defaults, mixed-exceptions checks, form-fill todo.
    [Fact]
    public void Empty_arrays_are_written_on_one_line()
    {
        Assert.Contains("\n  \"defaults\": [],\n", CompareInput.Text(JudgmentGolden.ExampleBytes("table-compare")), StringComparison.Ordinal);
        Assert.Contains("\n  \"checks\": [],\n", CompareInput.Text(JudgmentGolden.ExampleBytes("mixed-exceptions")), StringComparison.Ordinal);
        Assert.Contains("\n  \"todo\": []\n", CompareInput.Text(JudgmentGolden.ExampleBytes("form-fill")), StringComparison.Ordinal);
    }

    // 3.2 "쓰지 않는 것": 대조 셈 속성(counts 등), 시간 값 원본, 사람 입력 원본(키)이 속성으로 나오지 않는다.
    [Fact]
    public void Counts_times_and_human_input_are_not_written_as_properties()
    {
        var scene = JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.Found, JudgmentInput.Met, JudgmentInput.Spread);
        var text = CompareInput.Text(scene.Render());

        Assert.DoesNotContain("\"counts\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"times\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain(scene.Unexplained![0], text, StringComparison.Ordinal);
        Assert.DoesNotContain("reference/", text, StringComparison.Ordinal);
    }

    // 3.2: 판정 결과의 값이 JSON에 그대로 쓰인다(메모리 결과 = 파일).
    [Fact]
    public void Render_writes_result_values()
    {
        var judged = new JudgmentScene(Columns: [], Rows: JudgmentInput.Rows(40, mismatch: 2), Extras: [], null, null, null).Judge();
        using var json = JsonDocument.Parse(JudgmentJson.Render(judged));
        var root = json.RootElement;

        Assert.Equal(judged.Case, root.GetProperty("case").GetString());
        Assert.Equal(judged.Verdict, root.GetProperty("verdict").GetString());
        Assert.Equal(judged.Rule, root.GetProperty("rule").GetString());
        Assert.Equal(judged.Reason, root.GetProperty("reason").GetString());
        Assert.Equal(judged.Thresholds.AccuracyMin, root.GetProperty("thresholds").GetProperty("accuracy_min").GetDecimal());
        Assert.Equal(judged.Thresholds.MissingMax, root.GetProperty("thresholds").GetProperty("missing_max").GetDecimal());
        Assert.Equal(judged.Thresholds.TimeMustDrop, root.GetProperty("thresholds").GetProperty("time_must_drop").GetBoolean());
        Assert.Equal(judged.Defaults, root.GetProperty("defaults").EnumerateArray().Select(e => e.GetString()!));
        Assert.Equal(judged.Checks.Select(c => (c.Rule, c.Result, c.Reason)), root.GetProperty("checks").EnumerateArray().Select(c => (c.GetProperty("rule").GetString()!, c.GetProperty("result").GetString()!, c.GetProperty("reason").GetString()!)));
        Assert.Equal(judged.Todo.Select(t => (t.Key, t.Text)), root.GetProperty("todo").EnumerateArray().Select(t => (t.GetProperty("key").GetString()!, t.GetProperty("text").GetString()!)));
    }

    private static string[] Names(JsonElement element) => element.EnumerateObject().Select(p => p.Name).ToArray();

    // 경로: 처음 만난 순서. 객체는 "a.b", 배열 안 객체·배열은 "a[]". 문자열 배열 원소는 경로를 만들지 않는다.
    private static List<string> Paths(JsonElement root)
    {
        var paths = new List<string>();
        foreach (var (path, _) in Values(root, string.Empty))
        {
            if (!paths.Contains(path))
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    // 모든 (경로, 값). 걷는 순서는 문서 순서다.
    private static IEnumerable<(string Path, JsonElement Value)> Values(JsonElement element, string path)
    {
        if (path.Length > 0)
        {
            yield return (path, element);
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in element.EnumerateObject())
            {
                foreach (var x in Values(p.Value, path.Length == 0 ? p.Name : path + "." + p.Name))
                {
                    yield return x;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray().Where(i => i.ValueKind is JsonValueKind.Object or JsonValueKind.Array))
            {
                foreach (var x in Values(item, path + "[]"))
                {
                    yield return x;
                }
            }
        }
    }
}
