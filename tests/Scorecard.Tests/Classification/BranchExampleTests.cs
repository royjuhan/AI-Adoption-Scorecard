using Scorecard.Core.Cases;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Classification;

// 스펙 6.1 분기 예시와 4.1·4.3 순서(카드 수용 기준 1).
// 6.1 표를 파일에서 읽는다. 행마다 기준 답에서 바꾼 답으로 분류해 기대 행(담당·근거 키, 순서까지)과 대조한다.
// 행 종류는 4.5 문장 틀 표의 근거 키 → 행 종류로 본다.
[Trait("Spec", "classification")]
public class BranchExampleTests
{
    // 지시문 3.3 분기 7개. 6.1 표가 모두 1개 이상 다뤄야 한다(스펙 8절).
    private static readonly string[] Branches = ["Q1", "Q2", "Q3", "Q4", "ELSE", "PARTIAL", "GATE"];

    // 6.1 표를 편 것. "a / b / c" 대안은 하나씩 따로. (바꾼 답, 기대 행 "② ELSE + ④ PARTIAL").
    public static TheoryData<string, string> Examples => ClassificationInput.TheoryPairs(ReadExamples());

    [Theory]
    [MemberData(nameof(Examples))]
    public void Branch_example_gives_expected_rows(string changes, string expectedRows)
    {
        var templates = ClassificationSpec.Templates();
        var step = ClassificationInput.One(ClassificationInput.Answers(changes));
        var expected = expectedRows.Split(" + ");

        Assert.Equal(expected, ClassificationInput.RowLabels(step));
        Assert.Equal(step.Rows.Select(r => templates[r.Rule].Kind), step.Rows.Select(r => r.Kind));
        Assert.Equal(step.Rows.Select(r => templates[r.Rule].Assignee), step.Rows.Select(r => r.Assignee));
    }

    [Fact]
    public void Branch_examples_cover_all_seven_branches()
    {
        var covered = ReadExamples().SelectMany(e => e.Rows.Split(" + ")).Select(r => r.Split(' ')[1]).Distinct().ToArray();

        Assert.All(Branches, b => Assert.Contains(b, covered));
    }

    // 4.1: 처음 걸리는 곳에서 멈춘다. 기준 답의 Q1~Q4가 동시에 참이면 앞의 것이 이긴다.
    [Theory]
    [InlineData("existing_feature: yes, rule_expressible: no, input_kind: free_doc, frequency: rare, batch_size: 1", "Q1")]
    [InlineData("rule_expressible: no, input_kind: free_doc, frequency: rare, batch_size: 1", "Q2")]
    [InlineData("input_kind: free_doc, frequency: rare, batch_size: 1", "Q3")]
    [InlineData("frequency: rare, batch_size: 1", "Q4")]
    public void First_matching_branch_wins(string changes, string expectedRule)
    {
        var step = ClassificationInput.One(ClassificationInput.Answers(changes));

        Assert.Equal(expectedRule, step.Rows[0].Rule);
        Assert.Equal(ClassificationSpec.KindStep, step.Rows[0].Kind);
    }

    // 4.3: 단계마다 step 행 하나가 맨 앞이고, 부가 행은 step → exception_review → human_gate 순서다.
    // 부가 행은 같은 단계 안에 있다(번호·이름은 부모 단계 것). 게이트는 모든 잎 뒤에 붙는다.
    [Theory]
    [InlineData("existing_feature: yes, failure_impact: safety_legal", "① Q1 + ④ GATE")]
    [InlineData("rule_expressible: no, failure_impact: safety_legal", "③ Q2 + ④ GATE")]
    [InlineData("input_kind: conversation, failure_impact: safety_legal", "③ Q3 + ④ GATE")]
    [InlineData("frequency: rare, batch_size: 2, failure_impact: external", "④ Q4 + ④ GATE")]
    [InlineData("rule_expressible: partial, failure_impact: safety_legal", "② ELSE + ④ PARTIAL + ④ GATE")]
    public void Extra_rows_follow_step_row_in_order(string changes, string expectedRows)
    {
        var step = ClassificationInput.One(ClassificationInput.Answers(changes), name: "부모 단계");
        var kindOrder = new[] { ClassificationSpec.KindStep, ClassificationSpec.KindExceptionReview, ClassificationSpec.KindHumanGate };

        Assert.Equal(expectedRows.Split(" + "), ClassificationInput.RowLabels(step));
        Assert.Equal(ClassificationSpec.KindStep, step.Rows[0].Kind);
        Assert.Single(step.Rows, r => r.Kind == ClassificationSpec.KindStep);
        Assert.Equal(step.Rows.Select(r => Array.IndexOf(kindOrder, r.Kind)).Order(), step.Rows.Select(r => Array.IndexOf(kindOrder, r.Kind)));
        Assert.Equal(1, step.Number);
        Assert.Equal("부모 단계", step.Name);
    }

    // 4.1: PARTIAL은 ELSE일 때만 붙는다. Q3·Q4에서 멈추면 붙지 않는다(원문 들여쓰기). Q2(no)는 partial이 아니다.
    [Theory]
    [InlineData("rule_expressible: partial, existing_feature: yes", "① Q1")]
    [InlineData("rule_expressible: partial, input_kind: image", "③ Q3")]
    [InlineData("rule_expressible: partial, frequency: rare, batch_size: 4", "④ Q4")]
    [InlineData("rule_expressible: partial, frequency: rare, batch_size: 5", "② ELSE + ④ PARTIAL")]
    public void Exception_review_only_after_else(string changes, string expectedRows)
    {
        Assert.Equal(expectedRows.Split(" + "), ClassificationInput.RowLabels(ClassificationInput.One(ClassificationInput.Answers(changes))));
    }

    // 4.1: exception_rate, current_actor, minutes_per_item은 트리가 쓰지 않는다.
    // 예외 비율이 60%여도 rule_expressible: yes면 예외 검토 행이 없다.
    [Theory]
    [InlineData("")]
    [InlineData("existing_feature: yes")]
    [InlineData("rule_expressible: no")]
    [InlineData("input_kind: free_doc")]
    [InlineData("frequency: rare, batch_size: 1")]
    [InlineData("rule_expressible: partial, failure_impact: external")]
    public void Tree_ignores_exception_rate_current_actor_and_minutes(string changes)
    {
        var baseline = Rows(ClassificationInput.Answers(changes));
        var variants =
            from er in new[] { "0", "60", "100" }
            from ca in ClassificationInput.Choices("current_actor")
            from mpi in new[] { "null", "0", "7.5" }
            select ClassificationInput.With(
                ClassificationInput.Answers(changes),
                [("exception_rate", er), ("current_actor", ca), ("minutes_per_item", mpi)]);

        Assert.All(variants, a => Assert.Equal(baseline, Rows(a)));
    }

    // 5절: Q4인데 current_actor: program이어도 ④ Q4이고 문장은 같다(승인 결정 3).
    [Fact]
    public void Q4_with_program_actor_is_still_person()
    {
        var human = ClassificationInput.One(ClassificationInput.Answers("frequency: rare, batch_size: 2, current_actor: human"));
        var program = ClassificationInput.One(ClassificationInput.Answers("frequency: rare, batch_size: 2, current_actor: program"));

        Assert.Equal(["④ Q4"], ClassificationInput.RowLabels(program));
        Assert.Equal(human.Rows[0].Reason, program.Rows[0].Reason);
    }

    // 행 비교용 문자열. 확인 항목·AI 과잉 후보는 트리 밖이라 넣지 않는다.
    private static string[] Rows(StepAnswers a) =>
        ClassificationInput.One(a).Rows.Select(r => $"{r.Kind} {r.Assignee} {r.Rule} [{string.Join(",", r.Used)}] {r.Reason}").ToArray();

    // 6.1 표 행을 (바꾼 답, 기대 행)으로 편다.
    // 바꾼 답 칸: "(없음)", "`k: v, k: v`", "`k: v` / `v2` / `v3`"(같은 키의 대안).
    // 행 칸: "② `ELSE` + ④ `PARTIAL`". 뒤의 "(예외 검토 없음)" 같은 설명은 뺀다.
    private static IEnumerable<(string Changes, string Rows)> ReadExamples()
    {
        foreach (var cells in SpecDocument.Table(ClassificationSpec.Document, ClassificationSpec.BranchHeader))
        {
            var rows = ExpectedRows(cells[1]);
            if (cells[0] == "(없음)")
            {
                yield return (string.Empty, rows);
                continue;
            }

            var spans = SpecDocument.CodeSpans(cells[0]);
            yield return (spans[0], rows);
            if (spans.Length > 1)
            {
                Assert.Contains(" / ", cells[0], StringComparison.Ordinal);
                var key = Assert.Single(ClassificationInput.Pairs(spans[0])).Key;
                foreach (var alternative in spans.Skip(1))
                {
                    yield return ($"{key}: {alternative}", rows);
                }
            }
        }
    }

    private static string ExpectedRows(string cell)
    {
        var text = cell.Split(" (")[0];
        var parts = text.Split(" + ").Select(p =>
        {
            var pieces = p.Trim().Split(' ');
            Assert.True(pieces.Length == 2, $"6.1 행 칸 '{cell}'을 읽을 수 없다.");
            return $"{pieces[0]} {SpecDocument.Unquote(pieces[1])}";
        });
        return string.Join(" + ", parts);
    }
}
