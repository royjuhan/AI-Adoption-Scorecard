using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Classification;

// 스펙 4.1~4.8의 SIGNOFF(T-008, ADR 0003). 카드 수용 기준 1.
// human_signoff == yes면 트리 맨 앞에서 멈춘다: ④ step 행 하나, 예외 검토·게이트 행 없음, 읽은 답은 human_signoff 하나.
// 확인 항목은 트리 밖이라 그대로 달린다. AI 과잉 후보가 아니다.
// 6.1 표의 SIGNOFF 행·4.2·4.5·4.6 표의 SIGNOFF 행은 기존 표 대조 테스트가 그대로 읽는다. 여기서는 나머지 규칙을 본다.
[Trait("Spec", "classification")]
public class SignoffTests
{
    private const string Signoff = "SIGNOFF";

    // human_signoff: yes 단계 하나. 다른 답 9개(4.5 조합 2,160개 × exception_rate·current_actor·minutes_per_item 변형)를
    // 모두 바꿔도 ④ SIGNOFF step 행 하나이고, 문장은 4.5 문장 틀 그대로(표시 없음), 읽은 답은 [human_signoff]다.
    [Fact]
    public void Signoff_yes_gives_one_person_row_whatever_the_other_answers()
    {
        var template = ClassificationSpec.Templates()[Signoff];
        var phrases = ClassificationSpec.Phrases();
        var differences = new List<string>();
        var combinations = ClassificationInput.AllCombinations().Where(a => a.HumanSignoff.Value == "yes").ToArray();
        var extras =
            from er in new[] { "0", "60" }
            from ca in ClassificationInput.Choices("current_actor")
            from mpi in new[] { "null", "7.5" }
            select new[] { ("exception_rate", er), ("current_actor", ca), ("minutes_per_item", mpi) };
        var extraSets = extras.ToArray();

        foreach (var combination in combinations)
        {
            foreach (var extra in extraSets)
            {
                var a = ClassificationInput.With(combination, extra);
                var step = ClassificationInput.One(a);
                var row = step.Rows.Count == 1 ? step.Rows[0] : null;
                var expected = ClassificationSpec.Render(template.Text, a, phrases);
                if (row is null
                    || row.Kind != ClassificationSpec.KindStep
                    || row.Assignee != template.Assignee
                    || row.Rule != Signoff
                    || !row.Used.SequenceEqual(["human_signoff"])
                    || row.Reason != expected
                    || step.AiOveruseCandidate)
                {
                    differences.Add($"{string.Join(",", ClassificationInput.Values(a).Select(v => v.Value))}: [{string.Join(" | ", step.Rows.Select(r => $"{r.Kind} {r.Assignee} {r.Rule} [{string.Join(",", r.Used)}] {r.Reason}"))}] overuse={step.AiOveruseCandidate}");
                }
            }
        }

        Assert.Equal(2160, combinations.Length);
        Assert.Equal("④", template.Assignee);
        Assert.Equal(ClassificationSpec.KindStep, template.Kind);
        Assert.True(differences.Count == 0, $"차이 {differences.Count}건(앞 20건):\n" + string.Join("\n", differences.Take(20)));
    }

    // 4.3: SIGNOFF 단계 뒤에는 게이트를 두지 않는다(failure_impact가 external·safety_legal이어도). 예외 검토도 없다.
    [Theory]
    [InlineData("human_signoff: yes, failure_impact: external")]
    [InlineData("human_signoff: yes, failure_impact: safety_legal")]
    [InlineData("human_signoff: yes, rule_expressible: partial")]
    [InlineData("human_signoff: yes, rule_expressible: partial, failure_impact: safety_legal")]
    [InlineData("human_signoff: yes, existing_feature: yes, failure_impact: external")]
    [InlineData("human_signoff: yes, rule_expressible: no, failure_impact: external")]
    [InlineData("human_signoff: yes, input_kind: image, failure_impact: external")]
    [InlineData("human_signoff: yes, frequency: rare, batch_size: 1, failure_impact: external")]
    public void Signoff_step_has_no_gate_or_exception_review(string changes)
    {
        var step = ClassificationInput.One(ClassificationInput.Answers(changes));

        Assert.Equal(["④ SIGNOFF"], ClassificationInput.RowLabels(step));
        Assert.DoesNotContain(step.Rows, r => r.Kind is ClassificationSpec.KindHumanGate or ClassificationSpec.KindExceptionReview);
    }

    // 같은 답에서 human_signoff만 no로 바꾸면 트리가 아래로 내려가 게이트·예외 검토가 붙는다(SIGNOFF가 막은 것이 맞는지 대조).
    [Theory]
    [InlineData("failure_impact: external", "② ELSE + ④ GATE")]
    [InlineData("rule_expressible: partial, failure_impact: safety_legal", "② ELSE + ④ PARTIAL + ④ GATE")]
    [InlineData("existing_feature: yes, failure_impact: external", "① Q1 + ④ GATE")]
    public void Same_answers_with_signoff_no_go_down_the_tree(string changes, string expectedRows)
    {
        var no = ClassificationInput.One(ClassificationInput.Answers(changes + ", human_signoff: no"));
        var yes = ClassificationInput.One(ClassificationInput.Answers(changes + ", human_signoff: yes"));

        Assert.Equal(expectedRows.Split(" + "), ClassificationInput.RowLabels(no));
        Assert.Equal(["④ SIGNOFF"], ClassificationInput.RowLabels(yes));
    }

    // 4.6: SIGNOFF는 human_signoff만 읽는다. 다른 답이 모두 기본값이어도 SIGNOFF 문장에는 표시가 없다.
    // defaults·확인 항목에는 남는다(4.7: 확인 항목은 트리 밖이라 SIGNOFF 단계에도 달린다).
    [Fact]
    public void Signoff_sentence_ignores_other_default_answers()
    {
        var yaml = "name: p\ntask: t\nsteps:\n  - name: 서명\n    answers: {human_signoff: yes}\n";
        var step = Assert.Single(StepClassifier.Classify(CaseInput.Valid(CaseParser.Parse(ClassificationInput.Bytes(yaml), "answers.yaml"))).Steps);
        var row = Assert.Single(step.Rows);

        Assert.Equal(Signoff, row.Rule);
        Assert.Equal(ClassificationSpec.Templates()[Signoff].Text, row.Reason);
        Assert.Equal(ClassificationInput.Keys.Where(k => k != "human_signoff"), step.Defaults);
        Assert.Equal([ClassificationSpec.MeasureMinutesPerItem, ClassificationSpec.ConfirmExistingFeature], step.Checks);
        Assert.False(step.AiOveruseCandidate);
    }

    // 4.7: 확인 항목은 값만 본다. SIGNOFF 단계도 같다.
    [Theory]
    [InlineData("human_signoff: yes", new string[0])]
    [InlineData("human_signoff: yes, minutes_per_item: null", new[] { ClassificationSpec.MeasureMinutesPerItem })]
    [InlineData("human_signoff: yes, existing_feature: unknown", new[] { ClassificationSpec.ConfirmExistingFeature })]
    [InlineData("human_signoff: yes, minutes_per_item: null, existing_feature: unknown", new[] { ClassificationSpec.MeasureMinutesPerItem, ClassificationSpec.ConfirmExistingFeature })]
    public void Signoff_step_keeps_checks(string changes, string[] expected)
    {
        Assert.Equal(expected, ClassificationInput.One(ClassificationInput.Answers(changes)).Checks);
    }

    // 4.8: SIGNOFF(④)는 AI 과잉 후보가 아니다. current_actor: ai_each_time이어도 false. no로 바꾸면 ②라서 후보다.
    [Fact]
    public void Signoff_step_is_not_overuse_candidate()
    {
        var yes = ClassificationInput.One(ClassificationInput.Answers("human_signoff: yes, current_actor: ai_each_time"));
        var no = ClassificationInput.One(ClassificationInput.Answers("human_signoff: no, current_actor: ai_each_time"));

        Assert.False(yes.AiOveruseCandidate);
        Assert.True(no.AiOveruseCandidate);
    }

    // 4.3: 트리는 단계마다 따로 정한다. 앞 단계의 게이트와 서명 단계가 이어져 사람 확인이 두 번 나와도 그대로 둔다.
    // 서명 단계의 행은 그 단계 안에 있다(번호·이름은 그 단계 것).
    [Fact]
    public void Steps_are_classified_independently_around_a_signoff_step()
    {
        var definition = ClassificationInput.Definition(
            "p",
            ("보고서 만들기", ClassificationInput.Answers("failure_impact: external")),
            ("최종 확인·서명", ClassificationInput.Answers("human_signoff: yes, failure_impact: external")),
            ("보관", ClassificationInput.Answers("rule_expressible: partial")));
        var result = StepClassifier.Classify(definition);

        Assert.Equal(
            ["② ELSE + ④ GATE", "④ SIGNOFF", "② ELSE + ④ PARTIAL"],
            result.Steps.Select(s => string.Join(" + ", ClassificationInput.RowLabels(s))));
        Assert.Equal([1, 2, 3], result.Steps.Select(s => s.Number));
        Assert.Equal("최종 확인·서명", result.Steps[1].Name);
        Assert.Equal(5, result.Steps.Sum(s => s.Rows.Count));
    }

    // 4.6·5절: human_signoff를 적지 않은 단계(기본값 no)는 SIGNOFF가 아니고, step 행에만 기본값 표시가 붙는다.
    // 부가 행(PARTIAL·GATE)은 읽은 답이 키 하나라 붙지 않는다.
    [Fact]
    public void Missing_human_signoff_marks_only_the_step_row()
    {
        var yaml = "name: p\ntask: t\nsteps:\n  - name: s\n    answers: {input_kind: table, rule_expressible: partial, frequency: weekly, batch_size: 10, "
            + "exception_rate: 0, current_actor: human, minutes_per_item: 1, failure_impact: external, existing_feature: no}\n";
        var step = Assert.Single(StepClassifier.Classify(CaseInput.Valid(CaseParser.Parse(ClassificationInput.Bytes(yaml), "answers.yaml"))).Steps);

        Assert.Equal(["② ELSE", "④ PARTIAL", "④ GATE"], ClassificationInput.RowLabels(step));
        Assert.Equal(["human_signoff"], step.Defaults);
        Assert.Equal(
            [true, false, false],
            step.Rows.Select(r => r.Reason.EndsWith(ClassificationSpec.DefaultMark, StringComparison.Ordinal)));
    }

    // 4.2: SIGNOFF 조건 값은 yes 하나다. 4.4 표의 human_signoff 선택지는 yes / no다(case-schema 4.4).
    [Fact]
    public void Signoff_condition_is_human_signoff_yes()
    {
        var condition = Assert.Single(ClassificationSpec.Conditions(), c => c.Rule == Signoff);

        Assert.Equal("human_signoff", condition.Key);
        Assert.Equal("=", condition.Op);
        Assert.Equal(["yes"], condition.Values);
        Assert.Equal(["yes", "no"], ClassificationInput.Choices("human_signoff"));
        Assert.Equal(Signoff, ClassificationSpec.Conditions()[0].Rule);
    }

    // 4.6: SIGNOFF가 아닌 잎(Q1~Q4, ELSE)은 모두 human_signoff를 읽는다. 부가 행(PARTIAL·GATE)은 읽지 않는다.
    [Fact]
    public void Non_signoff_leaves_read_human_signoff_and_extra_rows_do_not()
    {
        var entries = ClassificationSpec.UsedEntries();

        Assert.All(entries.Where(e => e.Rule is not ("SIGNOFF" or "PARTIAL" or "GATE")), e => Assert.Contains("human_signoff", e.Keys));
        Assert.All(entries.Where(e => e.Rule is "PARTIAL" or "GATE"), e => Assert.DoesNotContain("human_signoff", e.Keys));
        Assert.Equal(["human_signoff"], Assert.Single(entries, e => e.Rule == Signoff).Keys);
    }
}
