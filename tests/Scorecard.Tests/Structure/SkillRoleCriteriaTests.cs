using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Structure;

// 역할 배분 기준(T-024, ADR 0018). 스킬 출력은 AI라 골든이 없다.
// 스킬 본문이 새 키·네 부분 출력 형식·다섯 질문을 쓰는지, 표본 self_check가 표 이름을 쓰는지만 본다. 문장 내용은 reviewer가 채점한다.
[Trait("Spec", "classification")]
public class SkillRoleCriteriaTests
{
    private const string SkillPath = ".claude/skills/scorecard/SKILL.md";
    private const string DataPath = "docs/spec/data/skill-output-rules.md";
    private const string ScoreTableHeader = "| 규칙 | 수용 | 실패로 보는 것 | 보는 곳 |";

    // 스킬 본문은 역할 배분 키(case-schema.md 4.16)를 모두 쓴다.
    [Fact]
    public void Skill_names_every_role_key()
    {
        var skill = File.ReadAllText(RepoRoot.Combine(SkillPath));

        Assert.All(
            new[]
            {
                "roles", "judgment", "execution", "accountability", "basis", "varies", "purpose", "done_when", "facts", "assumptions", "tools", "transitions",
                "function", "input", "process", "output", "reduces", "exceptions", "existing", "new", "burden", "condition", "evidence",
            },
            word => Assert.Contains(word, skill, StringComparison.Ordinal));
    }

    // 지시 6절 출력 형식: 네 부분 제목과 단계별 역할 배분 표 머리 행, 다섯 질문.
    [Fact]
    public void Skill_uses_the_four_part_output_and_five_questions()
    {
        var lines = File.ReadAllLines(RepoRoot.Combine(SkillPath));
        var text = string.Join('\n', lines);

        string[] parts = ["**① 업무 이해**", "**② 단계별 역할 배분**", "**③ 개선 제안**", "**④ 검증 방법과 역할 전환 조건**"];
        var at = parts.Select(p => text.IndexOf(p, StringComparison.Ordinal)).ToArray();
        Assert.All(at, i => Assert.True(i >= 0));
        Assert.Equal(at.Order(), at);
        Assert.Contains("| 업무 단계 | 필요한 판단과 실행 | 인간의 역할 | AI의 역할 | 소프트웨어의 역할 | 배분 근거 |", lines);
        Assert.All(
            new[]
            {
                "무엇이 상황에 따라 달라지는가?", "달라지는 것은 입력값인가, 맥락 해석과 판단인가?", "어떤 부분을 명확한 규칙으로 표현할 수 있는가?",
                "어떤 부분에 인간의 의사와 책임이 필요한가?", "결과가 적절한지 누가, 어떤 방법으로 확인할 수 있는가?",
            },
            q => Assert.Contains(q, text, StringComparison.Ordinal));
    }

    // T-024 표본의 self_check 규칙 이름은 채점 표 이름 그대로다.
    [Fact]
    public void Roles_sample_self_check_uses_table_names()
    {
        var table = SpecDocument.Table(DataPath, ScoreTableHeader).Select(r => Cli.CliInput.Cell(r[0])).ToArray();
        var bytes = File.ReadAllBytes(RepoRoot.Combine("tests/Scorecard.Tests/TestData/classification/ai-roles/answers.yaml"));
        var rules = Scorecard.Core.Cases.CaseParser.Parse(bytes, "answers.yaml").Case!.Notes!.SelfCheck.Select(c => c.Rule).ToArray();

        Assert.NotEmpty(rules);
        Assert.All(rules, r => Assert.Contains(r, table));
    }
}
