using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Structure;

// 스킬 출력 자기 검사 규칙(T-022, ADR 0016, docs/spec/skill-output-rules.md). 스킬 출력은 AI라 골든이 없다.
// 규칙 번호·이름이 문서·채점 표·스킬 본문에서 같은지, 표본 self_check가 표 이름을 쓰는지만 본다. 문장 내용은 reviewer가 채점한다.
[Trait("Spec", "classification")]
public class SkillOutputRulesTests
{
    private const string RulesPath = "docs/spec/skill-output-rules.md";
    private const string DataPath = "docs/spec/data/skill-output-rules.md";
    private const string SkillPath = ".claude/skills/scorecard/SKILL.md";
    private const string ScoreTableHeader = "| 규칙 | 수용 | 실패로 보는 것 | 보는 곳 |";

    private static string[] TableRules() => SpecDocument.Table(DataPath, ScoreTableHeader).Select(r => Cli.CliInput.Cell(r[0])).ToArray();

    // 채점 표의 규칙 = 문서의 "### R" 제목(순서까지). 번호는 R1부터 빈 곳 없이.
    [Fact]
    public void Score_table_matches_rule_headings()
    {
        var headings = File.ReadAllLines(RepoRoot.Combine(RulesPath)).Where(l => l.StartsWith("### R", StringComparison.Ordinal)).Select(l => l[4..]).ToArray();
        var table = TableRules();

        Assert.Equal(headings, table);
        Assert.Equal(Enumerable.Range(1, table.Length).Select(i => "R" + i), table.Select(t => t.Split(' ')[0]));
    }

    // 수용 규칙은 지시문 8.1의 다섯(R1~R5)이다.
    [Fact]
    public void Acceptance_rules_are_r1_to_r5()
    {
        var rows = SpecDocument.Table(DataPath, ScoreTableHeader);
        Assert.Equal(["R1", "R2", "R3", "R4", "R5"], rows.Where(r => Cli.CliInput.Cell(r[1]) == "예").Select(r => Cli.CliInput.Cell(r[0]).Split(' ')[0]));
    }

    // 스킬 본문은 규칙 문서를 가리키고 규칙 번호를 모두 쓴다. 종료점 문장과 가상 시나리오 줄은 글자 그대로다.
    [Fact]
    public void Skill_points_to_rules_and_names_every_rule()
    {
        var skill = File.ReadAllText(RepoRoot.Combine(SkillPath));

        Assert.Contains(RulesPath, skill, StringComparison.Ordinal);
        Assert.All(TableRules(), rule => Assert.Matches("(?<![A-Za-z0-9])" + rule.Split(' ')[0] + "(?![0-9])", skill));
        Assert.Contains("업무 구조 검토 완료. 지금 제출할 것 없음.", skill, StringComparison.Ordinal);
        Assert.All(new[] { "actions", "who_now", "proposed", "source", "gate_note", "self_check", "stated_numbers", "open_questions", "end_state", "scenario", "confirmed_in: conversation" }, word =>
            Assert.Contains(word, skill, StringComparison.Ordinal));
    }

    // T-022 표본의 self_check 규칙 이름은 채점 표 이름 그대로다.
    [Fact]
    public void Sample_self_check_uses_table_names()
    {
        var bytes = File.ReadAllBytes(RepoRoot.Combine("tests/Scorecard.Tests/TestData/classification/ai-actions/answers.yaml"));
        var parsed = Scorecard.Core.Cases.CaseParser.Parse(bytes, "answers.yaml");

        Assert.Empty(parsed.Errors);
        var rules = parsed.Case!.Notes!.SelfCheck.Select(c => c.Rule).ToArray();
        Assert.NotEmpty(rules);
        Assert.All(rules, r => Assert.Contains(r, TableRules()));
    }
}
