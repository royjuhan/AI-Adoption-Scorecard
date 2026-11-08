using System.Globalization;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Judgment;

// 스펙 11.11 탐침 재현(T-012 카드 수용 기준 6). M1 [재검토] 막는 지적 1·2의 탐침을 엔진으로 다시 돌린다.
// 장면: 머리 행 id,…,v, 키 id, compare v(tolerance 0), 문턱 accuracy_min 95, missing_max 2, time_must_drop false.
// 기준 키는 r0000부터, 추가 키는 x000부터. "T-011" 칸은 judge_v1 결과라 엔진으로 보지 않는다. "T-012" 칸만 엔진으로 본다.
[Trait("Spec", "judgment")]
public class ProbeTests
{
    private const string Rejected = "거절";
    private const string NoInput = "없음";

    // 11.11 표의 행 번호.
    public static TheoryData<int> ProbeRows => new(Enumerable.Range(0, JudgmentSpec.Table(JudgmentSpec.ProbeHeader).Count));

    // 11.11: 행마다 장면과 사람 입력에서 판정·근거 키가 "T-012" 칸이다. 판정 문장은 11.4 틀을 장면 값으로 채운 것이다.
    [Theory]
    [MemberData(nameof(ProbeRows))]
    public void Probe_row_gives_its_T012_verdict(int index)
    {
        var c = JudgmentSpec.Table(JudgmentSpec.ProbeHeader)[index];
        var probe = JudgmentSpec.Cell(c[0]);
        var expected = SpecDocument.CodeSpans(c[4]);
        Assert.Equal(2, expected.Length);
        var scene = Scene(probe, c[2]);

        var judged = scene.Judge();

        Assert.Equal((expected[0], expected[1]), (judged.Verdict, judged.Rule));
        var result = expected[1] == JudgmentSpec.PassRule ? null : JudgmentInput.CheckOf(judged, expected[1]).Result;
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindVerdict(expected[1], result).Template, scene.Slots()), judged.Reason);
    }

    // 11.11 둘째 글머리: P1 첫 행(지어낸 30행)이 지적 1, P2 첫 행(빈 메모 열)이 지적 2의 탐침이다.
    // 사람이 아직 보지 않았을 때 둘 다 통과·FOCUS가 아니다. T-011 칸과 다르다.
    [Fact]
    public void Review_probes_are_neither_pass_nor_focus()
    {
        var rows = JudgmentSpec.Table(JudgmentSpec.ProbeHeader);
        var p1 = rows.First(r => JudgmentSpec.Cell(r[0]) == "P1");
        var p2 = rows.First(r => JudgmentSpec.Cell(r[0]) == "P2");

        Assert.Equal(["pass", "PASS"], SpecDocument.CodeSpans(p1[3]));
        Assert.Equal(["conditional", "FOCUS"], SpecDocument.CodeSpans(p2[3]));

        Assert.Equal((NoInput, NoInput), (p1[2].Trim(), p2[2].Trim()));
        var fabricated = Scene("P1", p1[2]).Judge();
        Assert.NotEqual(JudgmentEngine.VerdictPass, fabricated.Verdict);
        Assert.Equal(JudgmentInput.NotReviewed, JudgmentInput.CheckOf(fabricated, JudgmentSpec.Unexplained).Result);
        Assert.Contains("추가 행 30건", fabricated.Reason, StringComparison.Ordinal);

        var memo = Scene("P2", p2[2]).Judge();
        Assert.NotEqual(JudgmentEngine.RuleFocus, memo.Rule);
        Assert.Equal(JudgmentInput.Off, JudgmentInput.CheckOf(memo, JudgmentSpec.Focus).Result);
        Assert.Contains("add_group", JudgmentInput.TodoKeys(memo));
    }

    // 11.11: 탐침 이름은 이 테스트가 장면을 아는 다섯(P1, P2, P2g, P3, P4)이다. "T-011" 칸은 판정 두 글자나 거절이다.
    // P1·P3 사람 입력(추가 행 키)과 P4(결과 쪽 중복)는 T-011에서 거절이었다. T-012는 받는다(카드 수용 기준 1).
    [Fact]
    public void Probe_table_uses_known_probes_and_T011_forms()
    {
        var rows = JudgmentSpec.Table(JudgmentSpec.ProbeHeader);

        Assert.Equal(["P1", "P2", "P2g", "P3", "P4"], rows.Select(r => JudgmentSpec.Cell(r[0])).Distinct());
        Assert.All(rows, r => Assert.True(r[3].Trim() == Rejected || SpecDocument.CodeSpans(r[3]).Length == 2, r[3]));
        Assert.All(rows.Where(r => JudgmentSpec.Cell(r[0]) is "P3" or "P4" or "P2g"), r => Assert.Equal(Rejected, r[3].Trim()));
        Assert.Contains(rows, r => JudgmentSpec.Cell(r[0]) == "P1" && r[3].Trim() == Rejected);
    }

    // 6.6 P4: 결과 쪽 키 중복은 대조 오류가 아니라 항목 duplicate다(metrics.md 4.3). r0003의 둘째 행이 duplicate이고
    // 기준 행 위치를 가진다. T-013(metrics.md 13.2): 결과 쪽에 두 번 나온 r0003은 두 행이 모두 기준과 같아도 값 불일치다(차이 []).
    // 정확도는 19/20 = 95.0으로 문턱 95를 충족하고 누락률은 0.0이다. 그래서 판정은 6.6 표 그대로 조건부·보류 UNEXPLAINED다.
    [Fact]
    public void Candidate_side_duplicate_becomes_a_duplicate_item()
    {
        var scene = Scene("P4", NoInput);

        var metrics = scene.Metrics();

        var duplicate = Assert.Single(metrics.Items, i => i.Status == CompareEngine.ItemDuplicate);
        Assert.Equal("r0003", duplicate.Key);
        Assert.Empty(duplicate.Differences);
        Assert.NotNull(duplicate.Reference);
        Assert.NotNull(duplicate.Candidate);
        Assert.Equal(22, duplicate.Candidate.Line);
        Assert.Equal(5, duplicate.Reference.Line);
        var item = Assert.Single(metrics.Items, i => i.Key == "r0003" && i.Status != CompareEngine.ItemDuplicate);
        Assert.Equal(CompareEngine.ItemMismatch, item.Status);
        Assert.Empty(item.Differences);
        Assert.Equal((20, 21, 19, 1, 0), (metrics.Counts!.Reference, metrics.Counts.Candidate, metrics.Counts.Match, metrics.Counts.Mismatch, metrics.Counts.Extra));
        Assert.Equal(("95.0", "0.0"), (JudgmentInput.Text(metrics.AccuracyPct!.Value), JudgmentInput.Text(metrics.MissingRatePct!.Value)));
        Assert.Equal(["r0003"], scene.BadKeys);
    }

    // 11.11 장면. 문턱 95·2·false. 사람 입력 칸으로 Unexplained를 채운다.
    private static JudgmentScene Scene(string probe, string input)
    {
        var scene = probe switch
        {
            // 기준 20행, 결과 = 기준 20행 그대로 + 지어낸 키 30행.
            "P1" => Base(20) with { Extras = Extras(30) },

            // 기준 40행(열 memo: 39행 빈 칸, 1행 "확인"), 빈 칸 행 2개 불일치(값 불일치 둘), inputs.group 없음.
            "P2" => Memo(),

            // P2에 inputs.group: memo. 불일치 둘이 모두 memo = (빈 칸)이라 found.
            "P2g" => Memo() with { Group = "memo", FocusValue = string.Empty },

            // 기준 20행, 결과 = 기준 20행 + 추가 1행(x000).
            "P3" => Base(20) with { Extras = Extras(1) },

            // 기준 20행, 결과 = 기준 20행 + r0003 한 행 더.
            "P4" => WithDuplicate(Base(20), 3),
            _ => throw new ArgumentOutOfRangeException(nameof(probe), probe, "11.11 표의 탐침이 아니다. 표가 바뀌었으면 장면을 더한다."),
        };

        return scene with { Unexplained = Input(input, scene) };
    }

    // 11.11 "사람 입력" 칸: 없음 = null, `[]` = 빈 목록, "추가 행 키 전부" = 장면의 추가 행 키, `키` = 그 키.
    private static IReadOnlyList<string>? Input(string cell, JudgmentScene scene) => cell.Trim() switch
    {
        NoInput => null,
        "`[]`" => [],
        "추가 행 키 전부" => scene.Extras.Select(r => r.Key).ToArray(),
        var other when other.StartsWith('`') => SpecDocument.CodeSpans(other),
        var other => throw new InvalidOperationException($"11.11 '사람 입력' 칸 '{other}'을 모른다."),
    };

    // 결과 자료 끝에 기준 행 index를 한 번 더 넣는다(첫 행은 앞에 그대로 있다).
    private static JudgmentScene WithDuplicate(JudgmentScene scene, int index) => scene with { Duplicates = [scene.Rows[index]] };

    private static JudgmentScene Base(int count) =>
        new(Columns: [], Rows: Rows(count, _ => []), Extras: [], AccuracyMin: "95", MissingMax: "2", TimeMustDrop: "false");

    private static JudgmentScene Memo() =>
        new(Columns: ["memo"], Rows: Rows(40, i => [i < 39 ? string.Empty : "확인"], mismatch: 2), Extras: [], AccuracyMin: "95", MissingMax: "2", TimeMustDrop: "false");

    // 기준 키 r0000부터. 앞 mismatch개는 값 불일치(누락이 아니다: 누락이면 누락률이 문턱 2를 넘는다).
    private static SceneRow[] Rows(int count, Func<int, string[]> types, int mismatch = 0) =>
        Enumerable.Range(0, count)
            .Select(i => new SceneRow("r" + i.ToString("D4", CultureInfo.InvariantCulture), types(i), i < mismatch ? SceneRow.Mismatch : SceneRow.Match))
            .ToArray();

    // 추가 키 x000부터. 유형 열이 없는 장면(P1·P3)에서만 쓴다.
    private static SceneRow[] Extras(int count) =>
        Enumerable.Range(0, count).Select(i => new SceneRow("x" + i.ToString("D3", CultureInfo.InvariantCulture), [], SceneRow.Match)).ToArray();
}
