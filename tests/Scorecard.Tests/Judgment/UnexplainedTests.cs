using Scorecard.Core.Judgment;

namespace Scorecard.Tests.Judgment;

// 스펙 4.9 "설명 안 되는 검토 항목"(T-012 11.9에서 옮겼다. 안 U-B 그대로)과 6.4 표. 카드 수용 기준 2(T-012 카드 수용 기준 1).
// 엔진은 검토 항목(4.9: 불일치·추가 행·중복 행)이 설명되는지 알 수 없다. 사람이 적은 키 목록만 받는다.
// null이면 아직 안 봄(not_reviewed → 조건부, 4.3 순서 7), 빈 목록이면 모두 설명함(explained), 키가 있으면 found → 보류.
// 검토 항목이 아닌 키·같은 키 두 번은 ArgumentException(거절)이다. API 쪽 경우는 JudgmentApiShapeTests도 본다(5절).
[Trait("Spec", "judgment")]
public partial class UnexplainedTests
{
    private const string Rejected = "거절";

    // 6.4 표의 행 번호.
    public static TheoryData<int> UnexplainedRows => new(Enumerable.Range(0, JudgmentSpec.Table(JudgmentSpec.UnexplainedHeader).Count));

    // 6.4: 불일치·추가 행·중복 행 수와 사람 입력(없음 = null, [] = 빈 목록, "불일치 N개" = 불일치 항목 키 N개, …)에서
    // UNEXPLAINED 결과와 확인 문장(4.4 틀). found이면 보류 UNEXPLAINED(4.3 순서 4), not_reviewed이면 조건부 UNEXPLAINED(순서 7).
    // 판정 문장은 4.3 틀이다. 거절은 ArgumentException.
    // 장면(judgment.md 12.5 test-engineer 2, 6.4 첫 글머리): 중복 행은 차이 없는 기준 키를 결과 끝에 한 번 더 둔 것이다.
    // 그 키는 결과 쪽에 두 번 나와 값 불일치다(T-013, metrics.md 4.3). 그래서 "불일치 칸 − 중복 행 칸"만큼 보통 불일치
    // (값 불일치·누락을 번갈아)를 두고, 나머지 불일치는 중복 행으로 만든다. "불일치 N개"는 보통 불일치 키부터 앞에서 고른다.
    [Theory]
    [MemberData(nameof(UnexplainedRows))]
    public void Unexplained_table_row_gives_its_result(int index)
    {
        var c = JudgmentSpec.Table(JudgmentSpec.UnexplainedHeader)[index].Select(JudgmentSpec.Cell).ToArray();
        var (bad, extras, duplicates) = (JudgmentSpec.Int(c[0]), JudgmentSpec.Int(c[1]), JudgmentSpec.Int(c[2]));
        var plain = bad - duplicates;
        Assert.True(plain >= 0, $"6.4 표 {index}행: 불일치 칸은 중복 행 칸 이상이어야 한다(중복 행의 키도 불일치다).");
        var rows = Enumerable.Range(0, 10)
            .Select(i => new SceneRow(JudgmentInput.Key(i), [], i >= plain ? SceneRow.Match : i % 2 == 0 ? SceneRow.Mismatch : SceneRow.Missing))
            .ToArray();
        var extraRows = Enumerable.Range(0, extras).Select(i => new SceneRow(JudgmentInput.ExtraKey(i), [], SceneRow.Match)).ToArray();
        var duplicateRows = Enumerable.Range(0, duplicates).Select(i => rows[^(i + 1)]).ToArray();
        Assert.All(duplicateRows, r => Assert.False(r.IsBad));
        var duplicateKeys = duplicateRows.Select(r => r.Key).ToArray();
        var keys = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["불일치"] = rows.Where(r => r.IsBad || duplicateKeys.Contains(r.Key)).Select(r => r.Key).ToArray(),
            ["추가 행"] = extraRows.Select(r => r.Key).ToArray(),
            ["중복 행"] = duplicateKeys,
            ["일치"] = rows.Where(r => !r.IsBad).Select(r => r.Key).Except(duplicateKeys).ToArray(),
        };
        var scene = new JudgmentScene(
            Columns: [],
            Rows: rows,
            Extras: extraRows,
            AccuracyMin: "0",
            MissingMax: "100",
            TimeMustDrop: "false",
            Unexplained: Input(c[3], keys))
        {
            Duplicates = duplicateRows,
        };
        Assert.Equal(bad, scene.Bad);

        if (c[4] == Rejected)
        {
            Assert.ThrowsAny<ArgumentException>(() => scene.Judge());
            return;
        }

        var judged = scene.Judge();
        var check = JudgmentInput.CheckOf(judged, JudgmentSpec.Unexplained);
        var slots = scene.Slots();

        Assert.Equal(c[4], check.Result);
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindCheck(JudgmentSpec.Unexplained, c[4]).Template, slots), check.Reason);
        switch (c[4])
        {
            case JudgmentInput.Found:
                Assert.Equal((JudgmentEngine.VerdictHold, JudgmentEngine.RuleUnexplained), (judged.Verdict, judged.Rule));
                Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindVerdict(JudgmentSpec.Unexplained, JudgmentInput.Found).Template, slots), judged.Reason);
                break;
            case JudgmentInput.NotReviewed:
                Assert.Equal((JudgmentEngine.VerdictConditional, JudgmentEngine.RuleUnexplained), (judged.Verdict, judged.Rule));
                Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindVerdict(JudgmentSpec.Unexplained, JudgmentInput.NotReviewed).Template, slots), judged.Reason);
                break;
            default:
                Assert.Equal((JudgmentEngine.VerdictPass, JudgmentEngine.RulePass), (judged.Verdict, judged.Rule));
                break;
        }
    }

    // 6.4 표가 UNEXPLAINED 결과 넷과 거절을 모두 쓴다(빈 통과 방지). 추가 행만, 중복 행 키만 적은 found, 셋 다인 행이 있다.
    // T-013(judgment.md 12.5 test-engineer 2): 기준 키의 중복 행은 그 키를 불일치로 센다. 그래서 "불일치 0·추가 행 0·중복 행 ≠ 0" 행은
    // 없다. 뜻(중복 행만 적어도 found)은 "중복 행 ≠ 0이고 사람 입력이 중복 행 키뿐인 found 행"으로 본다(6.4 `1 | 0 | 1 | 중복 행 1개`).
    // 중복 행이 있는 행은 불일치 칸이 중복 행 칸 이상이다.
    [Fact]
    public void Unexplained_table_covers_all_results()
    {
        var rows = JudgmentSpec.Table(JudgmentSpec.UnexplainedHeader).Select(r => r.Select(JudgmentSpec.Cell).ToArray()).ToArray();

        Assert.Equal(
            JudgmentSpec.CheckRows().Where(r => r.Check == JudgmentSpec.Unexplained).Select(r => r.Result).Order(StringComparer.Ordinal),
            rows.Select(r => r[4]).Where(r => r != Rejected).Distinct().Order(StringComparer.Ordinal));
        Assert.Contains(rows, r => r[4] == Rejected);
        Assert.Contains(rows, r => r[0] == "0" && r[1] != "0" && r[2] == "0" && r[4] == JudgmentInput.Found);
        Assert.Contains(rows, r => r[2] != "0" && r[3].Split(", ").All(p => p.StartsWith("중복 행 ", StringComparison.Ordinal)) && r[4] == JudgmentInput.Found);
        Assert.Contains(rows, r => r[0] != "0" && r[1] != "0" && r[2] != "0");
        Assert.All(rows, r => Assert.True(JudgmentSpec.Int(r[0]) >= JudgmentSpec.Int(r[2]), string.Join(" | ", r)));
    }

    // 4.3·4.7: 사람이 아직 보지 않은 검토 항목(not_reviewed)은 보류가 아니다. 다음 할 일 review_items로 남는다.
    // 몰림이 걸리면 판정은 FOCUS다(순서 6이 7보다 앞). 사람 입력이 들어오면 판정이 바뀔 수 있다(조건부 → 보류).
    // 사람이 보고 모두 설명했으면 review_items가 없다.
    [Fact]
    public void Not_reviewed_is_a_todo_not_a_hold()
    {
        var notReviewed = JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.NotReviewed, JudgmentInput.Off, JudgmentInput.Found);
        var found = notReviewed with { Unexplained = [notReviewed.BadKeys[1]] };
        var explained = notReviewed with { Unexplained = [] };

        var first = notReviewed.Judge();
        Assert.Equal((JudgmentEngine.VerdictConditional, JudgmentEngine.RuleFocus), (first.Verdict, first.Rule));
        Assert.Contains("review_items", JudgmentInput.TodoKeys(first));

        var second = found.Judge();
        Assert.Equal((JudgmentEngine.VerdictHold, JudgmentEngine.RuleUnexplained), (second.Verdict, second.Rule));
        Assert.DoesNotContain("review_items", JudgmentInput.TodoKeys(second));

        var third = explained.Judge();
        Assert.Equal((JudgmentEngine.VerdictConditional, JudgmentEngine.RuleFocus), (third.Verdict, third.Rule));
        Assert.DoesNotContain("review_items", JudgmentInput.TodoKeys(third));
    }

    // 4.9: 사람 입력 키의 순서는 결과에 영향이 없다(키 수만 문장에 쓴다).
    [Fact]
    public void Unexplained_key_order_does_not_change_bytes()
    {
        var scene = JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.NotReviewed, JudgmentInput.Off, JudgmentInput.Spread);

        var forward = (scene with { Unexplained = scene.BadKeys }).Render();
        var reversed = (scene with { Unexplained = Enumerable.Reverse(scene.BadKeys).ToArray() }).Render();

        Assert.Equal(forward, reversed);
    }

    // 4.9 키 하나가 항목 여럿을 덮는다: 결과 쪽에 세 번 나온 기준 키(중복 행 둘)도 키 하나다. {unexplained}는 키 수(1)이고
    // {items}·{breakdown}은 항목 수다. T-013(metrics.md 4.3): 그 키는 값 불일치이기도 해서 항목은 셋(불일치 1건, 중복 행 2건)이다.
    [Fact]
    public void One_key_covers_several_duplicate_items()
    {
        var rows = JudgmentInput.Rows(10);
        var scene = new JudgmentScene(Columns: [], Rows: rows, Extras: [], AccuracyMin: "0", MissingMax: "100", TimeMustDrop: "false", Unexplained: [rows[3].Key])
        {
            Duplicates = [rows[3], rows[3]],
        };

        var judged = scene.Judge();
        var slots = scene.Slots();

        Assert.Equal("1", slots["unexplained"]);
        Assert.Equal("3", slots["items"]);
        Assert.Equal("불일치 1건, 중복 행 2건", slots["breakdown"]);
        Assert.Equal((JudgmentEngine.VerdictHold, JudgmentEngine.RuleUnexplained), (judged.Verdict, judged.Rule));
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindCheck(JudgmentSpec.Unexplained, JudgmentInput.Found).Template, slots), JudgmentInput.CheckOf(judged, JudgmentSpec.Unexplained).Reason);
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindVerdict(JudgmentSpec.Unexplained, JudgmentInput.Found).Template, slots), judged.Reason);
    }

    // 6.4 "사람 입력" 칸 → 키 목록. 없음 = null, [] = 빈 목록, "불일치 N개, 추가 행 N개" = 그 항목 키 앞에서 N개, "… 두 번" = 같은 키 두 번.
    private static IReadOnlyList<string>? Input(string cell, IReadOnlyDictionary<string, string[]> keys)
    {
        switch (cell)
        {
            case "없음":
                return null;
            case "[]":
                return [];
        }

        var list = new List<string>();
        foreach (var part in cell.Split(", "))
        {
            var m = InputPart().Match(part);
            Assert.True(m.Success, $"6.4 '사람 입력' 칸 '{cell}'의 '{part}'을 모른다.");
            var taken = keys[m.Groups["kind"].Value].Take(JudgmentSpec.Int(m.Groups["count"].Value)).ToArray();
            Assert.Equal(JudgmentSpec.Int(m.Groups["count"].Value), taken.Length);
            list.AddRange(taken);
            if (m.Groups["twice"].Success)
            {
                list.AddRange(taken);
            }
        }

        return list;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^(?<kind>불일치|추가 행|중복 행|일치) (?<count>[0-9]+)개(?<twice> 두 번)?\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex InputPart();
}
