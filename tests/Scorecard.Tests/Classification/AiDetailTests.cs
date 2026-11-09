using System.Text;
using System.Text.Json;
using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.Classification;

// classification.md 3.2 AI 선택 항목(T-022, ADR 0016). ai에 적은 선택 항목만 rows 뒤에 그대로 쓴다.
// 분류(행·규칙 의견·differs)는 선택 항목과 상관없다. 공개 예제 바이트는 ExampleGoldenTests가 본다.
[Trait("Spec", "classification")]
public class AiDetailTests
{
    private const string OptionalHeader = "| AI 선택 속성 | 형식 | 값 |";
    private static readonly string Sample = RepoRoot.Combine("tests/Scorecard.Tests/TestData/classification/ai-actions/answers.yaml");

    // 데이터 3.2 둘째 표의 속성이 표 순서로 rows 뒤에 온다. 적은 것만 있다.
    [Fact]
    public void Optional_properties_follow_rows_in_table_order()
    {
        var order = ClassificationSpec.Table(OptionalHeader).Select(r => SpecDocument.Unquote(r[0])).ToArray();
        using var doc = JsonDocument.Parse(ClassificationJson.Render(StepClassifier.Classify(Parse(File.ReadAllBytes(Sample)))));
        var steps = doc.RootElement.GetProperty("steps").EnumerateArray().ToArray();

        Assert.Equal(["actions", "now", "change", "check", "gate_note"], order);
        Assert.Equal([.. Base, "actions", "now", "change", "check"], Names(steps[0]));
        Assert.Equal([.. Base, "actions", "check"], Names(steps[1]));
        Assert.Equal([.. Base, "actions", "gate_note"], Names(steps[2]));
        Assert.Equal(Base, Names(steps[3]));
        Assert.Equal("업체별 폴더에 넣기", steps[0].GetProperty("actions")[2].GetProperty("action").GetString());
        Assert.Equal("inferred", steps[0].GetProperty("actions")[2].GetProperty("source").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("schema_version").GetInt32());
    }

    // 선택 항목을 지워도 행·규칙 의견·differs·확인 항목은 바이트까지 같다(선택 키 줄만 빠진다).
    [Fact]
    public void Detail_does_not_change_classification()
    {
        var rich = StepClassifier.Classify(Parse(File.ReadAllBytes(Sample)));
        var bare = rich with { Steps = rich.Steps.Select(s => s with { Detail = null }).ToArray() };
        var stripped = StepClassifier.Classify(Parse(Encoding.UTF8.GetBytes(StripDetail(File.ReadAllText(Sample)))));

        Assert.Equal(ClassificationJson.Render(bare), ClassificationJson.Render(stripped));
        Assert.All(stripped.Steps, s => Assert.Null(s.Detail));
        Assert.Equal(rich.Steps.Select(s => (s.Source, s.RuleOpinion, s.Differs)), stripped.Steps.Select(s => (s.Source, s.RuleOpinion, s.Differs)));
    }

    // 선택 항목이 없는 AI 단계와 규칙 단계에는 키가 없다(ai-judgment 표본 = 데이터 6.7과 같은 모양).
    [Fact]
    public void Steps_without_detail_have_no_optional_keys()
    {
        var bytes = File.ReadAllBytes(RepoRoot.Combine("tests/Scorecard.Tests/TestData/classification/ai-judgment/answers.yaml"));
        using var doc = JsonDocument.Parse(ClassificationJson.Render(StepClassifier.Classify(Parse(bytes))));

        Assert.All(doc.RootElement.GetProperty("steps").EnumerateArray(), s => Assert.Equal(Base, Names(s)));
    }

    private static readonly string[] Base =
        ["number", "name", "source", "rule_opinion", "differs", "defaults", "checks", "ai_overuse_candidate", "rows"];

    private static CaseDefinition Parse(byte[] bytes) => CaseInput.Valid(CaseParser.Parse(bytes, "answers.yaml"));

    private static string[] Names(JsonElement e) => e.EnumerateObject().Select(p => p.Name).ToArray();

    // ai 매핑 안의 선택 키 줄과 그 아래 목록 줄을 지운다(들여쓰기 6칸 키, 8칸 목록 원소).
    private static string StripDetail(string yaml)
    {
        string[] keys = ["actions:", "now:", "change:", "check:", "gate_note:"];
        var lines = new List<string>();
        var skipping = false;
        foreach (var line in yaml.Split('\n'))
        {
            if (keys.Any(k => line.StartsWith("      " + k, StringComparison.Ordinal)))
            {
                skipping = true;
                continue;
            }

            if (skipping && line.StartsWith("        ", StringComparison.Ordinal))
            {
                continue;
            }

            skipping = false;
            lines.Add(line);
        }

        return string.Join('\n', lines);
    }
}
