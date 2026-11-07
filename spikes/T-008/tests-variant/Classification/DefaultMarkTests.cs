using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Classification;

// 스펙 4.6 기본값 표시(카드 수용 기준 2, 승인 결정 6)와 6.2 예시.
// 규칙: 행을 정할 때 트리가 읽은 답(4.6 읽은 답 표) 중 하나라도 IsDefault면 그 행 문장 끝에 " (기본값 사용, 확인 필요)".
// defaults에는 답 10개 중 IsDefault인 키를 모두 적는다(4.4 표 순서). 문장 표시와 상관없다.
[Trait("Spec", "classification")]
public class DefaultMarkTests
{
    private const string AllDefaultsHeading = "### 6.2 기본값 표시 예시";

    private static readonly string[] Kinds =
        [ClassificationSpec.KindStep, ClassificationSpec.KindExceptionReview, ClassificationSpec.KindHumanGate];

    public static TheoryData<int> MarkExampleRows =>
        new(Enumerable.Range(0, SpecDocument.Table(ClassificationSpec.Document, ClassificationSpec.MarkHeader).Count));

    // 4.6 읽은 답 표의 근거 키는 4.5 문장 틀 표와 같다(ELSE는 frequency = rare 줄이 하나 더 있다).
    [Fact]
    public void Used_table_covers_every_rule()
    {
        var entries = ClassificationSpec.UsedEntries();
        var keys = ClassificationInput.Keys;

        Assert.Equal(ClassificationSpec.Templates().Keys.Order(StringComparer.Ordinal), entries.Select(e => e.Rule).Distinct().Order(StringComparer.Ordinal));
        Assert.All(entries, e => Assert.All(e.Keys, k => Assert.Contains(k, keys)));
        Assert.All(entries, e => Assert.Equal(e.Keys.OrderBy(k => Array.IndexOf(keys, k)), e.Keys));
    }

    // 4.6 표와 Core의 Used가 조합 4,320개의 모든 행에서 같다. Used는 4.4 표 순서다.
    [Fact]
    public void Used_matches_spec_table_for_all_combinations()
    {
        var entries = ClassificationSpec.UsedEntries();
        var differences = new List<string>();
        var rows = 0;

        foreach (var a in ClassificationInput.AllCombinations())
        {
            foreach (var row in ClassificationInput.One(a).Rows)
            {
                rows++;
                var expected = ClassificationSpec.UsedFor(entries, row.Rule, a);
                if (!expected.SequenceEqual(row.Used))
                {
                    differences.Add($"{row.Rule} {ClassificationInput.Value(a, "frequency")}: 스펙 [{string.Join(",", expected)}] Core [{string.Join(",", row.Used)}]");
                }
            }
        }

        Assert.Equal(5928, rows);
        Assert.True(differences.Count == 0, $"차이 {differences.Count}건(앞 20건):\n" + string.Join("\n", differences.Take(20)));
    }

    // 4.6 규칙 전체: 조합 4,320개 × 키 10개. 키 하나만 IsDefault(값은 그대로)일 때,
    // 읽은 답에 그 키가 있는 행만 표시가 붙고, defaults는 그 키 하나다. 표시는 문장 끝에 정확히 한 번.
    [Fact]
    public void Mark_appears_exactly_on_rows_that_read_the_default_answer()
    {
        var entries = ClassificationSpec.UsedEntries();
        var templates = ClassificationSpec.Templates();
        var phrases = ClassificationSpec.Phrases();
        var keys = ClassificationInput.Keys;
        var differences = new List<string>();
        var marked = 0;

        foreach (var explicitAnswers in ClassificationInput.AllCombinations())
        {
            foreach (var key in keys)
            {
                var a = ClassificationInput.MarkDefault(explicitAnswers, key);
                var step = ClassificationInput.One(a);
                if (!step.Defaults.SequenceEqual([key]))
                {
                    differences.Add($"{key}: defaults [{string.Join(",", step.Defaults)}]");
                }

                foreach (var row in step.Rows)
                {
                    var mark = ClassificationSpec.UsedFor(entries, row.Rule, a).Contains(key);
                    marked += mark ? 1 : 0;
                    var expected = ClassificationSpec.Render(templates[row.Rule].Text, a, phrases) + (mark ? ClassificationSpec.DefaultMark : string.Empty);
                    if (expected != row.Reason)
                    {
                        differences.Add($"{key} 기본값, {row.Rule}\n  스펙: {expected}\n  Core: {row.Reason}");
                    }
                }
            }
        }

        Assert.True(marked > 0);
        Assert.True(differences.Count == 0, $"차이 {differences.Count}건(앞 20건):\n" + string.Join("\n", differences.Take(20)));
    }

    // 6.2 표 각 행. 기준 답에서 그 키만 IsDefault로 둔다(괄호 안 값이 있으면 그 값으로).
    [Theory]
    [MemberData(nameof(MarkExampleRows))]
    public void Mark_example_row(int index)
    {
        var cells = SpecDocument.Table(ClassificationSpec.Document, ClassificationSpec.MarkHeader)[index];
        var (answers, key) = MarkExampleAnswers(cells[0]);
        var spans = SpecDocument.CodeSpans(cells[1]);
        var expectedMarked = cells[1].StartsWith("없음", StringComparison.Ordinal) ? [] : spans.Where(Kinds.Contains).ToArray();
        var expectedChecks = spans.Where(ClassificationSpec.CheckKeys().Contains).ToArray();
        Assert.True(cells[1].StartsWith("없음", StringComparison.Ordinal) || expectedMarked.Length > 0, $"6.2 문장 표시 칸 '{cells[1]}'을 읽을 수 없다.");

        var step = ClassificationInput.One(answers);
        var marked = step.Rows.Where(r => r.Reason.EndsWith(ClassificationSpec.DefaultMark, StringComparison.Ordinal)).Select(r => r.Kind);

        Assert.Equal(expectedMarked, marked);
        Assert.Equal([key], step.Defaults);
        Assert.All(expectedChecks, c => Assert.Contains(c, step.Checks));
        Assert.All(step.Rows, r => Assert.True(Occurrences(r.Reason, ClassificationSpec.DefaultMark.Trim()) <= 1, r.Reason));
    }

    // 6.2 블록: 파서를 거친 "답 없는 단계". 두 행 모두 표시, defaults 10개 모두, 확인 항목 2종.
    [Fact]
    public void Step_without_answers_matches_spec_block()
    {
        var block = SpecDocument.BlockLines(Assert.Single(SpecDocument.CodeBlocks(ClassificationSpec.Document, AllDefaultsHeading)));
        var definition = CaseInput.Valid(CaseParser.Parse(ClassificationInput.Bytes("name: p\ntask: t\nsteps:\n  - name: 답 없음\n"), "answers.yaml"));
        var step = Assert.Single(StepClassifier.Classify(definition).Steps);

        Assert.Equal(3, block.Length);
        Assert.Equal(block.Take(2), step.Rows.Select(r => $"{r.Assignee} {r.Rule}: {r.Reason}"));
        Assert.StartsWith("defaults: 10개 모두. checks: ", block[2], StringComparison.Ordinal);
        Assert.Equal(ClassificationInput.Keys, step.Defaults);
        Assert.Equal(block[2].Split("checks: ")[1].Split(", "), step.Checks);
        Assert.All(step.Rows, r => Assert.Equal(1, Occurrences(r.Reason, ClassificationSpec.DefaultMark)));
        Assert.All(step.Rows, r => Assert.EndsWith(ClassificationSpec.DefaultMark, r.Reason, StringComparison.Ordinal));
    }

    // 4.6: 표시는 기본값에만 붙는다. 값이 기본값과 같아도 직접 쓴 답이면 붙지 않는다.
    [Fact]
    public void Explicit_values_equal_to_defaults_are_not_marked()
    {
        var yaml = "name: p\ntask: t\nsteps:\n  - name: s\n    answers: {input_kind: fixed_form, rule_expressible: partial, frequency: weekly, batch_size: 10, exception_rate: 20, current_actor: human, minutes_per_item: null, failure_impact: internal_rework, existing_feature: unknown, human_signoff: no}\n";
        var step = Assert.Single(StepClassifier.Classify(CaseInput.Valid(CaseParser.Parse(ClassificationInput.Bytes(yaml), "answers.yaml"))).Steps);

        Assert.Equal(["minutes_per_item"], step.Defaults);
        Assert.All(step.Rows, r => Assert.DoesNotContain(ClassificationSpec.DefaultMark.Trim(), r.Reason, StringComparison.Ordinal));
        Assert.Equal([ClassificationSpec.MeasureMinutesPerItem, ClassificationSpec.ConfirmExistingFeature], step.Checks);
    }

    // 6.2 기본값인 답 칸. 첫 `키`가 기본값인 답이다. 바로 뒤 "(null)" 또는 (`값`)이 그 값이다.
    // ", `k: v`"는 함께 바꾸는 답이다. 그 밖의 글(예: "(10건 → ELSE)", "ELSE")은 설명이다.
    private static (StepAnswers Answers, string Key) MarkExampleAnswers(string cell)
    {
        var spans = SpecDocument.CodeSpans(cell);
        var key = spans[0];
        Assert.Contains(key, ClassificationInput.Keys);
        var a = ClassificationInput.Base;
        foreach (var change in spans.Skip(1).Where(s => s.Contains(": ", StringComparison.Ordinal)))
        {
            a = ClassificationInput.With(a, ClassificationInput.Pairs(change));
        }

        var afterKey = cell[(cell.IndexOf('`' + key + '`', StringComparison.Ordinal) + key.Length + 2)..];
        if (afterKey.StartsWith("(null)", StringComparison.Ordinal))
        {
            a = ClassificationInput.With(a, key, "null");
        }
        else if (afterKey.StartsWith("(`", StringComparison.Ordinal))
        {
            a = ClassificationInput.With(a, key, spans[1]);
        }

        return (ClassificationInput.MarkDefault(a, key), key);
    }

    private static int Occurrences(string text, string part)
    {
        var count = 0;
        for (var i = text.IndexOf(part, StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + part.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
