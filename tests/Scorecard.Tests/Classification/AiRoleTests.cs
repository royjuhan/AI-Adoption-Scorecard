using System.Text;
using System.Text.Json;
using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.Classification;

// classification.md 3.2 역할 배분(T-024, ADR 0018). ai에 적은 역할 배분만 gate_note 뒤에 그대로 쓴다.
// 분류(행·규칙 의견·differs)는 역할 배분과 상관없다. 공개 예제 바이트는 ExampleGoldenTests가 본다.
[Trait("Spec", "classification")]
public class AiRoleTests
{
    private const string RoleHeader = "| 역할 선택 속성 | 형식 | 값 |";
    private static readonly string Sample = RepoRoot.Combine("tests/Scorecard.Tests/TestData/classification/ai-roles/answers.yaml");
    private static readonly string[] RoleKeys = ["roles", "judgment", "execution", "accountability", "basis", "varies"];

    // 데이터 3.2 셋째 표의 속성이 표 순서로 T-022 선택 항목 뒤에 온다. roles 안은 적은 역할만 human, ai, software 순이다.
    [Fact]
    public void Role_properties_follow_detail_in_table_order()
    {
        var order = ClassificationSpec.Table(RoleHeader).Select(r => SpecDocument.Unquote(r[0])).ToArray();
        using var doc = Render(File.ReadAllText(Sample));
        var steps = doc.RootElement.GetProperty("steps").EnumerateArray().ToArray();

        Assert.Equal(RoleKeys, order);
        Assert.Equal(["actions", .. RoleKeys], Names(steps[0]).SkipWhile(n => n != "actions"));
        Assert.Equal(["actions", "gate_note", .. RoleKeys], Names(steps[3]).SkipWhile(n => n != "actions"));
        Assert.Equal(["ai", "software"], Names(steps[0].GetProperty("roles")));
        Assert.Equal(["human", "ai", "software"], Names(steps[2].GetProperty("roles")));
        Assert.Equal(["software"], Names(steps[3].GetProperty("roles")));
        Assert.Equal(2, doc.RootElement.GetProperty("schema_version").GetInt32());
    }

    // 역할 배분 키를 지워도 행·규칙 의견·differs·확인 항목은 바이트까지 같다(역할 배분 줄만 빠진다).
    [Fact]
    public void Roles_do_not_change_classification()
    {
        var rich = StepClassifier.Classify(Parse(File.ReadAllText(Sample)));
        var bare = rich with { Steps = rich.Steps.Select(s => s with { Detail = s.Detail! with { Allocation = null } }).ToArray() };
        var stripped = StepClassifier.Classify(Parse(StripRoles(File.ReadAllText(Sample))));

        Assert.Equal(ClassificationJson.Render(bare), ClassificationJson.Render(stripped));
        Assert.All(stripped.Steps, s => Assert.Null(s.Detail!.Allocation));
        Assert.Equal(rich.Steps.Select(s => (s.Source, s.RuleOpinion, s.Differs, s.Rows.Count)), stripped.Steps.Select(s => (s.Source, s.RuleOpinion, s.Differs, s.Rows.Count)));
    }

    // 역할 배분만 적은 단계도 Detail이 있고 그 키만 쓴다.
    [Fact]
    public void Role_only_step_writes_only_role_keys()
    {
        using var doc = Render("name: a\ntask: t\nsteps:\n  - name: s\n    answers: {human_signoff: no}\n    ai: {assignee: ④, reason: r, accountability: 담당자가 승인한다}\n");
        var step = doc.RootElement.GetProperty("steps")[0];

        Assert.Equal("rows", Names(step)[^2]);
        Assert.Equal("accountability", Names(step)[^1]);
        Assert.Equal("담당자가 승인한다", step.GetProperty("accountability").GetString());
    }

    private static CaseDefinition Parse(string yaml) => CaseInput.Valid(CaseParser.Parse(Encoding.UTF8.GetBytes(yaml), "answers.yaml"));

    private static JsonDocument Render(string yaml) => JsonDocument.Parse(ClassificationJson.Render(StepClassifier.Classify(Parse(yaml))));

    private static string[] Names(JsonElement e) => e.EnumerateObject().Select(p => p.Name).ToArray();

    // ai 매핑 안의 역할 배분 키 줄과 그 아래 roles 줄(들여쓰기 8칸)을 지운다.
    private static string StripRoles(string yaml)
    {
        var lines = new List<string>();
        var skipping = false;
        foreach (var line in yaml.Split('\n'))
        {
            if (RoleKeys.Any(k => line.StartsWith("      " + k + ":", StringComparison.Ordinal)))
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
