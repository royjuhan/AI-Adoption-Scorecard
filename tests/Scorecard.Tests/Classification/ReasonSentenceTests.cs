using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Classification;

// 스펙 4.5 근거 문장(카드 수용 기준 2, 승인 결정 5).
// 문장 틀·문구 표를 파일에서 읽어 만든 문장이 Core 문장과 같다. 담당·행 종류도 틀 표와 같다.
// 조합 4,320개(4.5)의 모든 행을 본다. 답은 모두 직접 쓴 값이라 기본값 표시가 없다.
[Trait("Spec", "classification")]
public class ReasonSentenceTests
{
    // 지시문 3.3 "분류마다 근거 문장을 템플릿으로 만든다. 예: "…" 기본값이 쓰인 답은 … "(…)"를 붙인다."
    private const string InstructionsReasonLine = "분류마다 근거 문장을 템플릿으로 만든다.";

    // 스펙 4.5 "지시문 3.3 예시 문장과 바이트까지 같다: `…`" 글머리.
    private const string SpecExampleBullet = "- 지시문 3.3 예시 문장과 바이트까지 같다:";

    // 스펙 4.6 규칙 글머리. 공백 하나와 `(기본값 사용, 확인 필요)`.
    private const string SpecMarkBullet = "- 규칙: 행을 정할 때 트리가 읽은 답";

    // 4.5 문장 틀은 8개(SIGNOFF, Q1~Q4, ELSE, PARTIAL, GATE)다.
    [Fact]
    public void Template_table_has_eight_rules()
    {
        Assert.Equal(
            ["SIGNOFF", "Q1", "Q2", "Q3", "Q4", "ELSE", "PARTIAL", "GATE"],
            ClassificationSpec.Table(ClassificationSpec.TemplateHeader).Select(c => SpecDocument.Unquote(c[0])));
    }

    // 4.5 조합 4,320개, 행 5,928개. 문장·담당·행 종류가 표로 만든 것과 같다. 차이를 모아 한 번에 보인다.
    [Fact]
    public void All_combinations_match_templates_and_phrases()
    {
        var templates = ClassificationSpec.Templates();
        var phrases = ClassificationSpec.Phrases();
        var combinations = ClassificationInput.AllCombinations();
        var differences = new List<string>();
        var rows = 0;

        foreach (var a in combinations)
        {
            foreach (var row in ClassificationInput.One(a).Rows)
            {
                rows++;
                Assert.True(templates.ContainsKey(row.Rule), $"4.5 문장 틀 표에 없는 근거 키: {row.Rule}");
                var t = templates[row.Rule];
                var expected = ClassificationSpec.Render(t.Text, a, phrases);
                if (expected != row.Reason || t.Assignee != row.Assignee || t.Kind != row.Kind)
                {
                    differences.Add($"{string.Join(",", ClassificationInput.Values(a).Select(v => v.Value))} {row.Rule}\n  스펙: {t.Assignee} {t.Kind} {expected}\n  Core: {row.Assignee} {row.Kind} {row.Reason}");
                }
            }
        }

        Assert.Equal(4320, combinations.Count);
        Assert.Equal(5928, rows);
        Assert.True(differences.Count == 0, $"차이 {differences.Count}건(앞 20건):\n" + string.Join("\n", differences.Take(20)));
    }

    // 지시문 3.3 예시 문장이 바이트까지 그대로 나온다(rule_expressible: yes, input_kind: table, frequency: weekly).
    [Fact]
    public void Instructions_example_sentence_is_produced_exactly()
    {
        var quotes = InstructionsQuotes();
        var step = ClassificationInput.One(ClassificationInput.Answers("rule_expressible: yes, input_kind: table, frequency: weekly"));

        Assert.Equal(["② ELSE"], ClassificationInput.RowLabels(step));
        Assert.Equal(quotes[0], step.Rows[0].Reason);
    }

    // 스펙 4.5 글머리에 적은 예시 문장이 지시문 3.3과 같다.
    [Fact]
    public void Spec_example_sentence_equals_instructions()
    {
        var bullet = SpecDocument.Line(ClassificationSpec.Document, SpecExampleBullet);

        Assert.Equal(InstructionsQuotes()[0], SpecDocument.CodeSpans(bullet)[0]);
    }

    // 표시 문자열은 지시문 3.3의 "(기본값 사용, 확인 필요)" 앞에 공백 하나다(스펙 4.6 규칙).
    [Fact]
    public void Default_mark_matches_instructions_and_spec()
    {
        var bullet = SpecDocument.Line(ClassificationSpec.Document, SpecMarkBullet);

        Assert.Equal(ClassificationSpec.DefaultMark, " " + InstructionsQuotes()[1]);
        Assert.Contains("공백 하나와 `" + InstructionsQuotes()[1] + "`", bullet, StringComparison.Ordinal);
    }

    // 4.5: 문장에는 그 잎을 정한 답만 쓴다. 앞 분기에서 "아니다"로 넘어온 답과 failure_impact는 쓰지 않는다.
    // 예: Q3 문장은 input_kind만, Q2 문장은 rule_expressible만 바뀐다.
    [Theory]
    [InlineData("rule_expressible: no", "input_kind")]
    [InlineData("rule_expressible: no", "frequency")]
    [InlineData("input_kind: image", "rule_expressible")]
    [InlineData("input_kind: image", "frequency")]
    [InlineData("existing_feature: yes", "rule_expressible")]
    [InlineData("frequency: rare, batch_size: 3", "input_kind")]
    [InlineData("", "failure_impact")]
    public void Sentence_uses_only_answers_that_decided_the_leaf(string changes, string otherKey)
    {
        var a = ClassificationInput.Answers(changes);
        var first = ClassificationInput.One(a).Rows[0];
        var compared = 0;
        foreach (var choice in ClassificationInput.Choices(otherKey))
        {
            var row = ClassificationInput.One(ClassificationInput.With(a, otherKey, choice)).Rows[0];
            if (row.Rule == first.Rule)
            {
                compared++;
                Assert.Equal(first.Reason, row.Reason);
            }
        }

        Assert.True(compared >= 2, $"{otherKey}를 바꿔 같은 근거 키({first.Rule})가 나온 경우가 2개 이상이어야 한다.");
    }

    // 지시문 3.3 줄의 큰따옴표 안 문장 두 개: [예시 문장, 기본값 표시].
    private static string[] InstructionsQuotes()
    {
        var line = SpecDocument.Line(SpecDocument.InstructionsPath, InstructionsReasonLine);
        var parts = line.Split('"');
        Assert.Equal(5, parts.Length);
        return [parts[1], parts[3]];
    }
}
