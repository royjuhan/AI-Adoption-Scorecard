using System.Text.Json;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Tests.Classification;
using Scorecard.Tests.Guards;

namespace Scorecard.Tests.Comparison;

// 스펙 3.2·3.3 metrics.json(승인 질문 15). ADR 0004 결정 2: 첫 키 "schema_version" = 정수 1.
// 바이트 규칙은 classification.md 3.3과 같다: UTF-8(BOM 없음), LF, 들여쓰기 2칸, 끝에 \n 하나, UnsafeRelaxedJsonEscaping.
// items에는 차이가 있는 항목만 쓴다. 경로는 상대 경로만. 입력 해시·원문·compare에 없는 열은 쓰지 않는다.
[Trait("Spec", "metrics")]
public class MetricsJsonTests
{
    // 형식 칸 → 그 경로에 올 수 있는 JSON 값 종류.
    private static readonly Dictionary<string, Func<JsonElement, bool>> Formats = new(StringComparer.Ordinal)
    {
        ["정수"] = e => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out _) && !e.GetRawText().Contains('.', StringComparison.Ordinal),
        ["숫자"] = e => e.ValueKind == JsonValueKind.Number,
        ["문자열"] = e => e.ValueKind == JsonValueKind.String,
        ["객체"] = e => e.ValueKind == JsonValueKind.Object,
        ["문자열 배열"] = e => e.ValueKind == JsonValueKind.Array && e.EnumerateArray().All(x => x.ValueKind == JsonValueKind.String),
        ["객체 배열"] = e => e.ValueKind == JsonValueKind.Array && e.EnumerateArray().All(x => x.ValueKind == JsonValueKind.Object),
    };

    public static TheoryData<string> Examples => ClassificationInput.ExampleNames;

    // 출력 모음: 공개 예제 3종 + G2 표본 case-valid.
    private static IEnumerable<(string Label, byte[] Bytes)> Outputs() =>
        ClassificationInput.Examples.Select(e => (e, CompareInput.ExampleBytes(e)))
            .Append(("guard-case-valid", GuardCaseValidBytes()));

    // 속성 표의 경로 순서 = table-compare 출력을 걸으며 처음 만난 경로 순서(배열 원소는 []).
    [Fact]
    public void Property_table_paths_follow_table_compare_output_order()
    {
        var table = MetricsSpec.Table(MetricsSpec.PropertyHeader).Select(c => MetricsSpec.Cell(c[0])).ToArray();
        Assert.NotEmpty(table);

        using var json = JsonDocument.Parse(CompareInput.ExampleBytes("table-compare"));
        Assert.Equal(table, Paths(json.RootElement));
    }

    // 미검증 모양(mixed-exceptions)의 경로 = 표의 경로 중 배열 원소·counts 아래를 뺀 것(순서 그대로).
    [Fact]
    public void Not_verified_output_has_table_paths_without_array_items_and_counts()
    {
        var table = MetricsSpec.Table(MetricsSpec.PropertyHeader).Select(c => MetricsSpec.Cell(c[0]))
            .Where(p => !p.Contains("[]", StringComparison.Ordinal) && !p.StartsWith("compare.counts.", StringComparison.Ordinal))
            .ToArray();

        using var json = JsonDocument.Parse(CompareInput.ExampleBytes("mixed-exceptions"));
        Assert.Equal(table, Paths(json.RootElement));
    }

    // 속성 표의 형식 칸: 출력 모음의 모든 값이 그 경로의 형식이다("X 또는 null"은 null도 된다).
    [Fact]
    public void Values_have_property_table_formats()
    {
        var formats = MetricsSpec.Table(MetricsSpec.PropertyHeader).ToDictionary(c => MetricsSpec.Cell(c[0]), c => c[1].Trim(), StringComparer.Ordinal);
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

                var nullable = format.EndsWith(" 또는 null", StringComparison.Ordinal);
                var kind = nullable ? format[..^" 또는 null".Length] : format;
                Assert.True(Formats.ContainsKey(kind), $"속성 표 {path}의 형식 '{format}'을 모른다.");
                if (!(nullable && value.ValueKind == JsonValueKind.Null) && !Formats[kind](value))
                {
                    wrong.Add($"{label}: {path} = {value.GetRawText()}, 형식 {format}");
                }
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    // 첫 키는 "schema_version", 값은 정수 1(MetricsJson.SchemaVersion). 3.2 모양 블록의 첫 줄과 같다. 미검증 모양에도 있다.
    // 최상위 속성은 schema_version → case → compare.
    [Fact]
    public void First_key_is_integer_schema_version_one()
    {
        using var shape = JsonDocument.Parse(MetricsSpec.ShapeBlock());
        Assert.Equal(["schema_version", "case", "compare"], shape.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(1, shape.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal(1, MetricsJson.SchemaVersion);

        foreach (var (label, bytes) in Outputs())
        {
            var text = CompareInput.Text(bytes);
            Assert.True(text.StartsWith("{\n  \"schema_version\": 1,\n  \"case\": ", StringComparison.Ordinal), $"{label}의 첫 키가 \"schema_version\": 1이 아니다.");
            using var json = JsonDocument.Parse(bytes);
            Assert.Equal(["schema_version", "case", "compare"], json.RootElement.EnumerateObject().Select(p => p.Name));
        }
    }

    // compare 아래 속성 이름·순서는 3.2 모양 블록과 같다(측정·미검증 모두).
    [Fact]
    public void Compare_object_follows_shape_block_order()
    {
        using var shape = JsonDocument.Parse(MetricsSpec.ShapeBlock());
        var names = shape.RootElement.GetProperty("compare").EnumerateObject().Select(p => p.Name).ToArray();

        foreach (var (label, bytes) in Outputs())
        {
            using var json = JsonDocument.Parse(bytes);
            Assert.True(names.SequenceEqual(json.RootElement.GetProperty("compare").EnumerateObject().Select(p => p.Name)), label);
        }
    }

    // BOM 없음, CR 없음, 끝은 "}\n" 하나. 들여쓰기는 2칸 단위다. ASCII 밖 글자는 그대로 UTF-8이다.
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

    // 이스케이프(classification.md 3.3 표와 같다): ", \, 탭, U+2028, 이모지는 이스케이프, 한글·<>&'+`는 그대로.
    [Fact]
    public void Strings_use_relaxed_escaping()
    {
        const string key = "키\"\\\t\u2028\U0001F600<>&'+`";
        var metrics = CompareInput.Measured(CompareInput.Run(
            CompareInput.Case(CompareInput.TextCase),
            $"id,v\n{CompareInput.Quote(key)},a\n",
            $"id,v\n{CompareInput.Quote(key)},b\n"));
        var text = CompareInput.Text(MetricsJson.Render(metrics));

        Assert.Contains("\n        \"key\": \"키\\\"\\\\\\t\\u2028\\uD83D\\uDE00<>&'+`\",\n", text, StringComparison.Ordinal);
    }

    // 숫자는 decimal 그대로(자릿수 유지): 허용 오차 1.50·1.0·0, 퍼센트 늘 한 자리.
    [Fact]
    public void Decimals_keep_their_scale()
    {
        var definition = CompareInput.Case(CompareInput.InputsCase + "compare:\n  - {field: a, tolerance: 1.50}\n  - {field: b, tolerance_pct: 1.0}\n  - {field: c, tolerance: 0}\n");
        var text = CompareInput.Text(MetricsJson.Render(CompareInput.Measured(CompareInput.Run(definition, "id,a,b,c\nk,1,1,1\n", "id,a,b,c\nk,1,1,1\n"))));

        Assert.Contains("\"tolerance\": 1.50,", text, StringComparison.Ordinal);
        Assert.Contains("\"tolerance_pct\": 1.0,", text, StringComparison.Ordinal);
        Assert.Contains("\"tolerance\": 0,", text, StringComparison.Ordinal);
        Assert.Contains("\"accuracy_pct\": 100.0,", text, StringComparison.Ordinal);
        Assert.Contains("\"missing_rate_pct\": 0.0,", text, StringComparison.Ordinal);
    }

    // 빈 배열은 한 줄 []다. 같은 자료면 items가 []다(form-fill).
    [Fact]
    public void Empty_arrays_are_written_on_one_line()
    {
        var notVerified = CompareInput.Text(CompareInput.ExampleBytes("mixed-exceptions"));
        Assert.Contains("\"reference_files\": [],\n", notVerified, StringComparison.Ordinal);
        Assert.Contains("\"fields\": [],\n", notVerified, StringComparison.Ordinal);

        Assert.Contains("\"items\": []\n", CompareInput.Text(CompareInput.ExampleBytes("form-fill")), StringComparison.Ordinal);
    }

    // items에는 차이가 있는 항목만 쓴다(값 불일치·누락·추가·허용오차 안 차이가 있는 일치). 메모리 Items에는 모두 있다.
    [Fact]
    public void Items_list_only_items_with_differences()
    {
        var metrics = CompareInput.Measured(CompareInput.Example("table-compare"));
        using var json = JsonDocument.Parse(MetricsJson.Render(metrics));
        var written = json.RootElement.GetProperty("compare").GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()).ToArray();

        Assert.Equal(31, metrics.Items.Count);
        Assert.Equal(
            metrics.Items.Where(i => i.Status != CompareEngine.ItemMatch || i.Differences.Count > 0).Select(i => i.Key),
            written);
        Assert.Equal(8, written.Length);
    }

    // 쓰지 않는 것: compare에 없는 열(signal_type)과 그 값, 정규화 전 원문. 메모리 CsvRow에는 있다(승인 질문 16).
    [Fact]
    public void Columns_outside_compare_are_not_written()
    {
        var metrics = CompareInput.Measured(CompareInput.Example("table-compare"));
        var text = CompareInput.Text(MetricsJson.Render(metrics));

        Assert.DoesNotContain("signal_type", text, StringComparison.Ordinal);
        Assert.DoesNotContain("irregular", text, StringComparison.Ordinal);
        Assert.Contains("signal_type", metrics.Items[0].Reference!.Header);
    }

    // 미검증 모양: 상태·이유만 값이 있고 나머지는 null·[]. 3.2 "미검증 모양" 문장.
    [Fact]
    public void Not_verified_render_has_nulls_and_empty_arrays()
    {
        var bytes = MetricsJson.Render(new CompareMetrics("x", CompareEngine.StatusNotVerified, CompareEngine.ReasonNoInputs, null, [], [], null, null, null, [], []));

        Assert.Equal(
            "{\n  \"schema_version\": 1,\n  \"case\": \"x\",\n  \"compare\": {\n    \"status\": \"not_verified\",\n    \"reason\": \"no_inputs\",\n    \"key\": null,\n" +
            "    \"reference_files\": [],\n    \"candidate_files\": [],\n    \"counts\": null,\n    \"accuracy_pct\": null,\n    \"missing_rate_pct\": null,\n" +
            "    \"fields\": [],\n    \"items\": []\n  }\n}\n",
            CompareInput.Text(bytes));
    }

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

    private static byte[] GuardCaseValidBytes()
    {
        var sample = GuardWorkspace.SamplePath("case-valid");
        var definition = Scorecard.Tests.CaseSchema.CaseInput.Valid(CaseParser.ParseDirectory(sample));
        return MetricsJson.Render(CompareInput.Measured(CompareEngine.CompareDirectory(sample, definition)));
    }
}
