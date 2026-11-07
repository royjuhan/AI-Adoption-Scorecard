using Scorecard.Core.Judgment;

namespace Scorecard.Tests.Judgment;

// 스펙 4.9 "설명 안 되는 불일치"(잠정 결정 6, 안 U-B)와 6.4 표. 카드 수용 기준 2.
// 엔진은 불일치가 설명되는지 알 수 없다. 사람이 적은 키 목록만 받는다. null이면 아직 안 봄(not_reviewed, 보류 아님),
// 빈 목록이면 모두 설명함(explained), 키가 있으면 found → 보류. 잘못된 키·중복은 JudgmentApiShapeTests가 본다(5절).
[Trait("Spec", "judgment")]
public class UnexplainedTests
{
    // 6.4 표의 행 번호.
    public static TheoryData<int> UnexplainedRows => new(Enumerable.Range(0, JudgmentSpec.Table(JudgmentSpec.UnexplainedHeader).Count));

    // 6.4: 불일치 수와 사람 입력(없음 = null, [] = 빈 목록, N건 = 불일치 키 N개)에서 UNEXPLAINED 결과와 확인 문장(4.4 틀).
    // 불일치는 값 불일치·누락을 번갈아 둔다. found이면 판정은 보류 UNEXPLAINED이고 판정 문장은 4.3 틀이다.
    [Theory]
    [MemberData(nameof(UnexplainedRows))]
    public void Unexplained_table_row_gives_its_result(int index)
    {
        var c = JudgmentSpec.Table(JudgmentSpec.UnexplainedHeader)[index].Select(JudgmentSpec.Cell).ToArray();
        var bad = JudgmentSpec.Int(c[0]);
        var rows = Enumerable.Range(0, 10)
            .Select(i => new SceneRow(JudgmentInput.Key(i), [], i >= bad ? SceneRow.Match : i % 2 == 0 ? SceneRow.Mismatch : SceneRow.Missing))
            .ToArray();
        var badKeys = rows.Where(r => r.IsBad).Select(r => r.Key).ToArray();
        IReadOnlyList<string>? input = c[1] switch
        {
            "없음" => null,
            "[]" => [],
            var n when n.EndsWith('건') => badKeys.Take(JudgmentSpec.Int(n[..^1])).ToArray(),
            var other => throw new InvalidOperationException($"6.4 '사람 입력' 칸 '{other}'을 모른다."),
        };
        var scene = new JudgmentScene(
            Columns: [],
            Rows: rows,
            Extras: [],
            AccuracyMin: "0",
            MissingMax: "100",
            TimeMustDrop: "false",
            Unexplained: input);

        var judged = scene.Judge();
        var check = JudgmentInput.CheckOf(judged, JudgmentSpec.Unexplained);
        var slots = scene.Slots();

        Assert.Equal(c[2], check.Result);
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindCheck(JudgmentSpec.Unexplained, c[2]).Template, slots), check.Reason);
        if (c[2] == JudgmentInput.Found)
        {
            Assert.Equal((JudgmentEngine.VerdictHold, JudgmentEngine.RuleUnexplained), (judged.Verdict, judged.Rule));
            Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindVerdict(JudgmentSpec.Unexplained, JudgmentInput.Found).Template, slots), judged.Reason);
        }
        else
        {
            Assert.NotEqual(JudgmentEngine.VerdictHold, judged.Verdict);
        }
    }

    // 6.4 표가 UNEXPLAINED 결과 넷을 모두 쓴다(빈 통과 방지).
    [Fact]
    public void Unexplained_table_covers_all_results()
    {
        Assert.Equal(
            JudgmentSpec.CheckRows().Where(r => r.Check == JudgmentSpec.Unexplained).Select(r => r.Result).Order(StringComparer.Ordinal),
            JudgmentSpec.Table(JudgmentSpec.UnexplainedHeader).Select(r => JudgmentSpec.Cell(r[2])).Distinct().Order(StringComparer.Ordinal));
    }

    // 4.9: 사람이 아직 보지 않은 불일치(not_reviewed)는 판정을 막지 않고, 다음 할 일 review_mismatches로 남는다.
    // 사람 입력이 들어오면 판정이 바뀔 수 있다(조건부 → 보류). 사람이 보고 모두 설명했으면 review_mismatches가 없다.
    [Fact]
    public void Not_reviewed_is_a_todo_not_a_hold()
    {
        var notReviewed = JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.NotReviewed, JudgmentInput.Off, JudgmentInput.Found);
        var found = notReviewed with { Unexplained = [notReviewed.BadKeys[1]] };
        var explained = notReviewed with { Unexplained = [] };

        var first = notReviewed.Judge();
        Assert.Equal((JudgmentEngine.VerdictConditional, JudgmentEngine.RuleFocus), (first.Verdict, first.Rule));
        Assert.Contains("review_mismatches", JudgmentInput.TodoKeys(first));

        var second = found.Judge();
        Assert.Equal((JudgmentEngine.VerdictHold, JudgmentEngine.RuleUnexplained), (second.Verdict, second.Rule));
        Assert.DoesNotContain("review_mismatches", JudgmentInput.TodoKeys(second));

        var third = explained.Judge();
        Assert.Equal((JudgmentEngine.VerdictConditional, JudgmentEngine.RuleFocus), (third.Verdict, third.Rule));
        Assert.DoesNotContain("review_mismatches", JudgmentInput.TodoKeys(third));
    }

    // 4.9: 사람 입력 키의 순서는 결과에 영향이 없다(건수만 문장에 쓴다).
    [Fact]
    public void Unexplained_key_order_does_not_change_bytes()
    {
        var scene = JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.NotReviewed, JudgmentInput.Off, JudgmentInput.Spread);

        var forward = (scene with { Unexplained = scene.BadKeys }).Render();
        var reversed = (scene with { Unexplained = Enumerable.Reverse(scene.BadKeys).ToArray() }).Render();

        Assert.Equal(forward, reversed);
    }
}
