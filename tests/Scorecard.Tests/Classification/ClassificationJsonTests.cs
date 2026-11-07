using System.Globalization;
using System.Text.Json;
using Scorecard.Core.Classification;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Classification;

// 스펙 3.2·3.3 classification.json 바이트(승인 결정 10).
// UTF-8(BOM 없음), LF, 들여쓰기 2칸, 마지막 } 뒤에 \n 하나, 속성 순서, 이스케이프 표.
// 경로·입력 해시·Used는 쓰지 않는다.
[Trait("Spec", "classification")]
public class ClassificationJsonTests
{
    private const string ShapeHeading = "### 3.2 classification.json";

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
    [Fact]
    public void Json_is_indented_by_two_spaces_one_element_per_line()
    {
        var lines = ClassificationInput.Text(Render(ClassificationInput.MarkDefault(ClassificationInput.Base, "minutes_per_item"))).Split('\n');

        Assert.Equal("{", lines[0]);
        Assert.Equal("  \"case\": \"p\",", lines[1]);
        Assert.Contains("      \"defaults\": [", lines);
        Assert.Contains("        \"minutes_per_item\"", lines);
        Assert.All(lines, l => Assert.Equal(0, (l.Length - l.TrimStart(' ').Length) % 2));
    }

    // 속성 순서는 3.2 모양 블록과 같다: 최상위, 단계, 행.
    [Fact]
    public void Property_order_matches_spec_shape_block()
    {
        using var shape = JsonDocument.Parse(Assert.Single(SpecDocument.CodeBlocks(ClassificationSpec.Document, ShapeHeading)));
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
