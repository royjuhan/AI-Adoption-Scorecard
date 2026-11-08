namespace Scorecard.Tests.Structure;

// 스킬 /scorecard(M2-2, ADR 0010 A·B-10, ADR 0013). 스킬은 골든 테스트만 둔다.
// 파일이 있고, 흐름의 절 제목과 README 안내 문장, 명령 줄이 있는지만 본다. 문장 내용은 리뷰가 본다.
[Trait("Spec", "classification")]
public class ScorecardSkillTests
{
    private const string SkillPath = ".claude/skills/scorecard/SKILL.md";

    // ADR 0010 A-5: README와 같은 한 줄.
    private const string DataNotice = "AI에 보내면 안 되는 자료는 넣지 마세요. 구조만 설명해도 분류는 됩니다.";

    // 흐름 순서대로의 절 제목. 사용자 확인(4) 뒤에 answers.yaml과 start(5)가 온다.
    private static readonly string[] Headings =
    [
        "# /scorecard",
        "## 지킬 것",
        "## 1. 업무 설명 받기",
        "## 2. 단계 나누기와 판단",
        "## 3. 서명 단계 묻기",
        "## 4. 사용자 확인",
        "## 5. answers.yaml 쓰기와 start",
        "## 6. 결과 보여 주기",
        "## 7. 검증 안내 (check)",
    ];

    [Fact]
    public void Skill_has_name_and_flow_headings_in_order()
    {
        var lines = Lines();

        Assert.Equal("---", lines[0]);
        Assert.Contains("name: scorecard", lines);
        var at = Headings.Select(h => Array.IndexOf(lines, h)).ToArray();
        Assert.All(Headings.Zip(at), x => Assert.True(x.Second >= 0, $"{SkillPath}에 제목 '{x.First}'이 없다."));
        Assert.Equal(at.Order(), at);
    }

    [Fact]
    public void Skill_quotes_readme_data_notice_exactly()
    {
        Assert.Contains($"> {DataNotice}", Lines());
        Assert.Contains(DataNotice, File.ReadAllLines(RepoRoot.Combine("README.md")));
    }

    // 명령 줄은 json-output.md 2절 명령 모양이다. start는 --answers와 --json, check는 --json.
    [Fact]
    public void Skill_calls_start_and_check_with_json()
    {
        var lines = Lines();

        Assert.Contains("scorecard start cases/<케이스> --answers cases/<케이스>/answers.yaml --json", lines);
        Assert.Contains("scorecard check cases/<케이스> --json", lines);
    }

    // 담당 다섯(②M 포함)과 schema 2 속성 이름, inferred·human_signoff를 쓴다.
    [Fact]
    public void Skill_names_assignees_and_schema_two_fields()
    {
        var text = File.ReadAllText(RepoRoot.Combine(SkillPath));

        Assert.All(new[] { "①", "②", "②M", "③", "④", "rule_opinion", "differs", "inferred", "human_signoff", "ai_overuse_candidate" }, word =>
            Assert.Contains(word, text, StringComparison.Ordinal));
    }

    private static string[] Lines()
    {
        var path = RepoRoot.Combine(SkillPath);
        Assert.True(File.Exists(path), $"{SkillPath}가 없다.");
        return File.ReadAllLines(path);
    }
}
