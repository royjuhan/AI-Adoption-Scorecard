using System.Globalization;
using System.Text.Json;
using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Guards;

namespace Scorecard.Tests.Classification;

// 스펙 3.2·3.3 classification.json 바이트(승인 결정 10).
// UTF-8(BOM 없음), LF, 들여쓰기 2칸, 마지막 } 뒤에 \n 하나, 속성 순서, 이스케이프 표.
// 경로·입력 해시·Used는 쓰지 않는다.
// T-007(ADR 0004 결정 2): 첫 키는 "schema_version", 값은 정수 1. 3.2 모양 블록의 첫 줄과 같다.
[Trait("Spec", "classification")]
public class ClassificationJsonTests
{
    // 첫 키 검사에 쓰는 출력. 빈 결과, 빈 이름, 이스케이프가 필요한 이름, 키처럼 보이는 케이스 이름,
    // 분기 전부(Q1~Q4, ELSE, PARTIAL, GATE)와 기본값 표시, 공개 예제 3종, G2·G3 표본 case-valid.
    private static readonly (string Label, Func<byte[]> Render)[] SchemaVersionOutputs =
    [
        ("empty-steps", () => ClassificationJson.Render(new ClassificationResult("p", []))),
        ("empty-case-name", () => ClassificationJson.Render(new ClassificationResult("", [ClassificationInput.One(ClassificationInput.Base)]))),
        ("case-name-like-key", () => ClassificationJson.Render(new ClassificationResult(
            "\",\n  \"schema_version\": 2,\n  \"case\": \"",
            [ClassificationInput.One(ClassificationInput.Base, "\"schema_version\": 2")]))),
        ("all-branches-and-marks", () => ClassificationJson.Render(StepClassifier.Classify(ClassificationInput.Definition(
            "p",
            ("Q1", ClassificationInput.Answers("existing_feature: yes, failure_impact: external")),
            ("Q2", ClassificationInput.Answers("rule_expressible: no")),
            ("Q3", ClassificationInput.Answers("input_kind: image")),
            ("Q4", ClassificationInput.Answers("frequency: rare, batch_size: 3")),
            ("ELSE+PARTIAL+GATE", ClassificationInput.Answers("rule_expressible: partial, failure_impact: safety_legal")),
            ("기본값", ClassificationInput.MarkDefault(ClassificationInput.Answers("existing_feature: unknown"), "existing_feature")))))),
        .. ClassificationInput.Examples.Select(e => (
            $"example-{e}",
            (Func<byte[]>)(() => ClassificationInput.RenderAnswers(File.ReadAllBytes(ClassificationInput.ExamplePath(e, "answers.yaml")))))),
        ("guard-case-valid", () => ClassificationJson.Render(StepClassifier.Classify(
            CaseInput.Valid(CaseParser.ParseDirectory(GuardWorkspace.SamplePath("case-valid")))))),
    ];

    // 3.3 이스케이프 표(7행)를 옮긴 것. 단계 이름 → JSON 안 모양(따옴표 안).
    // 표 칸이 사람이 읽는 설명이라 값을 여기에 적는다. 표 행 수가 바뀌면 아래 테스트가 알린다.
    public static TheoryData<string, string> Escapes => new()
    {
        { "①②③④", "①②③④" },
        { "한글 단계", "한글 단계" },
        { "<>&'+`", "<>&'+`" },
        { "따옴표\"", "따옴표\\\"" },
        { "역슬래시\\", "역슬래시\\\\" },
        { "탭\t", "탭\\t" },
        { "제어\u0001", "제어\\u0001" },
        { "DEL\u007F", "DEL\\u007F" },
        { "줄\u2028구분", "줄\\u2028구분" },
        { "\U0001F600 이모지", "\\uD83D\\uDE00 이모지" },
        { "NFD \u1100\u1161", "NFD \u1100\u1161" },
    };

    public static TheoryData<string> SchemaVersionOutputLabels => new(SchemaVersionOutputs.Select(o => o.Label));

    // 이스케이프 표의 단계 이름만.
    public static TheoryData<string> EscapeNames => new(Escapes.Select(row => (string)row[0]!));

    [Fact]
    public void Escape_table_has_seven_rows()
    {
        Assert.Equal(7, ClassificationSpec.Table(ClassificationSpec.EscapeHeader).Count);
    }

    // BOM 없음, CR 없음, 끝은 "}\n" 하나(마지막 } 뒤에 \n 하나).
    [Fact]
    public void Bytes_are_utf8_without_bom_with_lf_and_single_trailing_newline()
    {
        var bytes = Render(ClassificationInput.Answers("rule_expressible: partial, failure_impact: external"));

        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "BOM이 없어야 한다.");
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.True(bytes is [.., (byte)'}', (byte)'\n'], "끝이 \"}\\n\"이어야 한다.");
        Assert.Equal(ClassificationInput.Bytes(ClassificationInput.Text(bytes)), bytes);
    }

    // 들여쓰기 2칸. 배열 원소도 한 줄에 하나(3.2 아래 글머리).
    // "case" 줄 앞의 머리 줄("{", T-007부터 schema_version 줄)은 3.2 모양 블록과 같다. "case" 줄은 블록과 같은 자리다.
    [Fact]
    public void Json_is_indented_by_two_spaces_one_element_per_line()
    {
        var lines = ClassificationInput.Text(Render(ClassificationInput.MarkDefault(ClassificationInput.Base, "minutes_per_item"))).Split('\n');
        var head = ShapeHeadLines();

        Assert.Equal("{", lines[0]);
        Assert.Equal(head, lines[..head.Length]);
        Assert.Equal("  \"case\": \"p\",", lines[head.Length]);
        Assert.Contains("      \"defaults\": [", lines);
        Assert.Contains("        \"minutes_per_item\"", lines);
        Assert.All(lines, l => Assert.Equal(0, (l.Length - l.TrimStart(' ').Length) % 2));
    }

    // 속성 순서는 3.2 모양 블록과 같다: 최상위, 단계, 행.
    [Fact]
    public void Property_order_matches_spec_shape_block()
    {
        using var shape = JsonDocument.Parse(ClassificationSpec.ShapeBlock());
        var bytes = Render(ClassificationInput.Answers("rule_expressible: partial, failure_impact: external"));
        using var json = JsonDocument.Parse(bytes);

        Assert.Equal(Names(shape.RootElement), Names(json.RootElement));
        var shapeStep = shape.RootElement.GetProperty("steps")[0];
        var step = json.RootElement.GetProperty("steps")[0];
        Assert.Equal(Names(shapeStep), Names(step));
        Assert.Equal(Names(shapeStep.GetProperty("rows")[0]), Names(step.GetProperty("rows")[0]));
        Assert.All(step.GetProperty("rows").EnumerateArray(), r => Assert.Equal(Names(shapeStep.GetProperty("rows")[0]), Names(r)));
        Assert.Equal(JsonValueKind.Number, step.GetProperty("number").ValueKind);
        Assert.Equal(JsonValueKind.False, step.GetProperty("ai_overuse_candidate").ValueKind);
    }

    // T-007(ADR 0004 결정 2): 3.2 모양 블록의 첫 키가 schema_version(정수 1)이다. 머리 줄은 "{"와 그 한 줄이다.
    // 예제 expected 3종도 같은 머리 줄로 시작한다(블록 = 파일은 ExampleGoldenTests가 본다).
    [Fact]
    public void Spec_shape_block_starts_with_schema_version_one()
    {
        using var shape = JsonDocument.Parse(ClassificationSpec.ShapeBlock());
        var first = shape.RootElement.EnumerateObject().First();
        var version = ClassificationSpec.SchemaVersion.ToString(CultureInfo.InvariantCulture);

        Assert.Equal(["{", $"  \"{ClassificationSpec.SchemaVersionKey}\": {version},"], ShapeHeadLines());
        Assert.Equal(ClassificationSpec.SchemaVersionKey, first.Name);
        Assert.Equal(JsonValueKind.Number, first.Value.ValueKind);
        Assert.Equal(version, first.Value.GetRawText());
        Assert.All(ClassificationInput.Examples, e => Assert.StartsWith(
            ShapeHead(),
            ClassificationInput.Text(File.ReadAllBytes(ClassificationInput.ExamplePath(e, "expected/" + ClassificationJson.FileName))),
            StringComparison.Ordinal));
    }

    // T-007: 모든 출력의 첫 키가 schema_version이고 값은 정수 1(문자열·소수 아님)이다. 3.2 블록의 머리 줄과 바이트까지 같다.
    [Theory]
    [MemberData(nameof(SchemaVersionOutputLabels))]
    public void Schema_version_is_first_key_with_integer_one(string label)
    {
        AssertSchemaVersionFirst(Assert.Single(SchemaVersionOutputs, o => o.Label == label).Render());
    }

    // T-007: 3.3 이스케이프 표의 이름을 케이스 이름과 단계 이름에 넣어도 첫 키는 schema_version이다.
    [Theory]
    [MemberData(nameof(EscapeNames))]
    public void Schema_version_is_first_key_for_escaped_names(string name)
    {
        AssertSchemaVersionFirst(ClassificationJson.Render(new ClassificationResult(name, [ClassificationInput.One(ClassificationInput.Base, name)])));
    }

    // 3.2: 행 값은 메모리 결과와 같고, Used는 JSON에 없다.
    [Fact]
    public void Json_values_match_memory_result_without_used()
    {
        var a = ClassificationInput.With(ClassificationInput.Answers("rule_expressible: partial, failure_impact: external"), "current_actor", "ai_each_time");
        var result = StepClassifier.Classify(ClassificationInput.Definition("케이스", ("단계", a)));
        using var json = JsonDocument.Parse(ClassificationJson.Render(result));
        var root = json.RootElement;
        var step = root.GetProperty("steps")[0];
        var memory = result.Steps[0];

        Assert.Equal("케이스", root.GetProperty("case").GetString());
        Assert.Equal(1, step.GetProperty("number").GetInt32());
        Assert.Equal("단계", step.GetProperty("name").GetString());
        Assert.Equal(memory.Defaults, step.GetProperty("defaults").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(memory.Checks, step.GetProperty("checks").EnumerateArray().Select(e => e.GetString()));
        Assert.True(step.GetProperty("ai_overuse_candidate").GetBoolean());
        Assert.Equal(
            memory.Rows.Select(r => (r.Kind, r.Assignee, r.Rule, r.Reason)),
            step.GetProperty("rows").EnumerateArray().Select(r => (
                r.GetProperty("kind").GetString()!,
                r.GetProperty("assignee").GetString()!,
                r.GetProperty("rule").GetString()!,
                r.GetProperty("reason").GetString()!)));
        Assert.All(step.GetProperty("rows").EnumerateArray(), r => Assert.False(r.TryGetProperty("used", out _)));
    }

    // 3.3 이스케이프 표. 이름은 바꾸지 않는다(정규화 안 함). JSON으로 다시 읽으면 원래 이름이다.
    [Theory]
    [MemberData(nameof(Escapes))]
    public void Step_name_is_escaped_as_spec_table(string name, string expectedInQuotes)
    {
        var step = ClassificationInput.One(ClassificationInput.Base, name);
        var bytes = ClassificationJson.Render(new ClassificationResult("p", [step]));
        var line = Assert.Single(ClassificationInput.Text(bytes).Split('\n'), l => l.TrimStart().StartsWith("\"name\": ", StringComparison.Ordinal));
        using var json = JsonDocument.Parse(bytes);

        Assert.Equal(name, step.Name);
        Assert.Equal($"\"name\": \"{expectedInQuotes}\",", line.Trim());
        Assert.Equal(name, json.RootElement.GetProperty("steps")[0].GetProperty("name").GetString());
    }

    // 4.4: 담당 ①②③④와 한글 문장은 이스케이프하지 않는다.
    [Fact]
    public void Assignees_and_hangul_are_written_raw()
    {
        var text = ClassificationInput.Text(Render(ClassificationInput.Answers("rule_expressible: partial, failure_impact: external")));

        Assert.Contains("\"assignee\": \"②\"", text, StringComparison.Ordinal);
        Assert.Contains("\"assignee\": \"④\"", text, StringComparison.Ordinal);
        Assert.Contains("판단 기준을", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u", text, StringComparison.Ordinal);
    }

    // 3.3 결정론: 같은 입력이면 같은 바이트. 두 번 분류·두 번 직렬화.
    [Fact]
    public void Same_input_gives_same_bytes_twice()
    {
        var definition = ClassificationInput.ParseAnswers(File.ReadAllBytes(ClassificationInput.ExamplePath("form-fill", "answers.yaml")));

        Assert.Equal(ClassificationJson.Render(StepClassifier.Classify(definition)), ClassificationJson.Render(StepClassifier.Classify(definition)));
    }

    // 3.3 문화권 6종(Invariant, de-DE, tr-TR, ar-SA, ko-KR, fr-CH)에서 같은 바이트. 건수 1234567도 InvariantCulture.
    // 문화권은 finally로 되돌린다.
    [Fact]
    public void Output_does_not_depend_on_culture()
    {
        var definition = ClassificationInput.Definition(
            "p",
            ("큰 건수", ClassificationInput.Answers("frequency: rare, batch_size: 1234567")),
            ("Q4", ClassificationInput.Answers("frequency: rare, batch_size: 3, failure_impact: external")),
            ("예외", ClassificationInput.With(ClassificationInput.Answers("rule_expressible: partial"), "current_actor", "ai_each_time")),
            ("기본값", ClassificationInput.MarkDefault(ClassificationInput.Answers("input_kind: image"), "existing_feature")));
        var outputs = new[] { "", "de-DE", "tr-TR", "ar-SA", "ko-KR", "fr-CH" }
            .Select(name => Convert.ToHexStringLower(RenderUnder(name, definition)))
            .ToArray();
        var text = ClassificationInput.Text(RenderUnder("", definition));

        Assert.Single(outputs.Distinct());
        Assert.Contains("한 번에 1234567건이므로", text, StringComparison.Ordinal);
        Assert.Contains("한 번에 3건이므로", text, StringComparison.Ordinal);
    }

    private static byte[] Render(Scorecard.Core.Cases.StepAnswers a) =>
        ClassificationJson.Render(new ClassificationResult("p", [ClassificationInput.One(a)]));

    private static string[] Names(JsonElement element) => element.EnumerateObject().Select(p => p.Name).ToArray();

    // 3.2 모양 블록에서 "case" 줄 앞까지의 줄. 자리표시자가 없는 고정 줄이다("{", schema_version 줄).
    private static string[] ShapeHeadLines()
    {
        var shape = ClassificationSpec.ShapeBlock().Split('\n');
        var caseLine = Array.FindIndex(shape, l => l.StartsWith("  \"case\": ", StringComparison.Ordinal));
        Assert.True(caseLine > 0, "3.2 모양 블록에 \"case\" 줄이 없다.");
        return shape[..caseLine];
    }

    // 머리 줄을 LF로 이은 것(끝에 LF).
    private static string ShapeHead() => string.Concat(ShapeHeadLines().Select(l => l + "\n"));

    // (1) 바이트 머리가 3.2 블록의 머리 줄과 같다. (2) 첫 속성이 schema_version이고 정수 1이다(원문 "1").
    // (3) 최상위 속성 이름·순서가 3.2 블록과 같다(schema_version은 한 번). (4) 단계·행 안에는 schema_version이 없다.
    private static void AssertSchemaVersionFirst(byte[] bytes)
    {
        Assert.StartsWith(ShapeHead(), ClassificationInput.Text(bytes), StringComparison.Ordinal);

        using var json = JsonDocument.Parse(bytes);
        var root = json.RootElement;
        var first = root.EnumerateObject().First();
        Assert.Equal(ClassificationSpec.SchemaVersionKey, first.Name);
        Assert.Equal(JsonValueKind.Number, first.Value.ValueKind);
        Assert.True(first.Value.TryGetInt32(out var version), "schema_version이 정수가 아니다.");
        Assert.Equal(ClassificationSpec.SchemaVersion, version);
        Assert.Equal(version.ToString(CultureInfo.InvariantCulture), first.Value.GetRawText());
        Assert.Equal(ClassificationSpec.ShapeTopLevelNames(), Names(root));

        foreach (var step in root.GetProperty("steps").EnumerateArray())
        {
            Assert.DoesNotContain(ClassificationSpec.SchemaVersionKey, Names(step));
            Assert.All(step.GetProperty("rows").EnumerateArray(), r => Assert.DoesNotContain(ClassificationSpec.SchemaVersionKey, Names(r)));
        }
    }

    // 빈 이름은 InvariantCulture. 분류와 직렬화를 모두 그 문화권에서 한다.
    private static byte[] RenderUnder(string cultureName, Scorecard.Core.Cases.CaseDefinition definition)
    {
        var culture = cultureName.Length == 0 ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(cultureName);
        var (saved, savedUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            return ClassificationJson.Render(StepClassifier.Classify(definition));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
            CultureInfo.CurrentUICulture = savedUi;
        }
    }
}
