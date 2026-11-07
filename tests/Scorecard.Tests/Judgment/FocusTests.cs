using System.Text.RegularExpressions;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;
using Scorecard.Tests.Comparison;

namespace Scorecard.Tests.Judgment;

// 스펙 4.8 "특정 유형에만 불일치가 몰림"(잠정 결정 5, 안 M-B + F1)과 6.3 표. 카드 수용 기준 2.
// 유형 열 = 기준 머리 행의 열 가운데 키·compare가 아닌 열. 이름 서수 순으로 보고, 불일치 focus_min건 이상이 모두 한 값이면 found.
// 값은 기준 행의 칸만 본다. 결과 자료·추가 행은 보지 않는다.
[Trait("Spec", "judgment")]
public partial class FocusTests
{
    // 머리 행 id,…,v, 키 id, compare v(tolerance 0). 문턱은 늘 met(정확도 0, 누락률 100), 시간 off.
    private const string FocusCase = Scorecard.Tests.Synthetic.SyntheticInput.OneStepCase +
        "inputs: {reference: reference/*.csv, candidate: candidate/*.csv, key: id}\ncompare:\n  - {field: v, tolerance: 0}\n" +
        "thresholds:\n  accuracy_min: 0\n  missing_max: 100\n  time_must_drop: false\n";

    // 6.3 첫 표·둘째 표의 행 번호.
    public static TheoryData<int> FocusRows => new(Enumerable.Range(0, JudgmentSpec.Table(JudgmentSpec.FocusHeader).Count));

    public static TheoryData<int> FocusColumnRows => new(Enumerable.Range(0, JudgmentSpec.Table(JudgmentSpec.FocusColumnsHeader).Count));

    // 6.3 첫 표(유형 열 하나 grp): 행마다 FOCUS 결과와 확인 문장(4.4 틀).
    [Theory]
    [MemberData(nameof(FocusRows))]
    public void Single_type_column_row_gives_its_focus(int index)
    {
        var c = JudgmentSpec.Table(JudgmentSpec.FocusHeader)[index];
        var reference = Expand(JudgmentSpec.List(c[0]));
        var bad = Expand(JudgmentSpec.List(c[1]));
        var extras = Expand(JudgmentSpec.List(c[2]));
        var hasColumn = JudgmentSpec.Cell(c[3]) switch
        {
            "있음" => true,
            "없음" => false,
            var other => throw new InvalidOperationException($"6.3 '유형 열' 칸 '{other}'을 모른다."),
        };
        var expected = JudgmentSpec.Cell(c[4]);

        // 불일치 행: 그 유형 값의 아직 쓰지 않은 기준 행을 앞에서부터 고른다. 값 불일치·누락을 번갈아 둔다.
        var kinds = Enumerable.Repeat(SceneRow.Match, reference.Length).ToArray();
        for (var i = 0; i < bad.Length; i++)
        {
            var row = Enumerable.Range(0, reference.Length).First(r => reference[r] == bad[i] && kinds[r] == SceneRow.Match);
            kinds[row] = i % 2 == 0 ? SceneRow.Mismatch : SceneRow.Missing;
        }

        var scene = new JudgmentScene(
            Columns: hasColumn ? [JudgmentInput.Group] : [],
            Rows: reference.Select((v, i) => new SceneRow(JudgmentInput.Key(i), hasColumn ? [v] : [], kinds[i])).ToArray(),
            Extras: extras.Select((v, i) => new SceneRow($"x{i + 1:D4}", hasColumn ? [v] : [], SceneRow.Match)).ToArray(),
            AccuracyMin: "0",
            MissingMax: "100",
            TimeMustDrop: "false")
        {
            FocusGroup = expected == JudgmentInput.Found ? JudgmentInput.Group : null,
            FocusValue = expected == JudgmentInput.Found ? bad[0] : null,
            FocusColumns = expected is JudgmentInput.Spread or JudgmentInput.OneValue ? JudgmentInput.Group : null,
        };

        var focus = JudgmentInput.CheckOf(scene.Judge(), JudgmentSpec.Focus);

        Assert.Equal(expected, focus.Result);
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindCheck(JudgmentSpec.Focus, expected).Template, scene.Slots()), focus.Reason);
    }

    // 6.3 둘째 표(유형 열 여럿): 머리 행 id,<열들>,v, 키 id, compare v. 행마다 FOCUS 결과, 걸린 열({group} = {value}), 본 열({columns}).
    [Theory]
    [MemberData(nameof(FocusColumnRows))]
    public void Multiple_type_columns_row_gives_its_focus(int index)
    {
        var c = JudgmentSpec.Table(JudgmentSpec.FocusColumnsHeader)[index];
        var columns = JudgmentSpec.List(c[0]).Select(ParseColumn).ToArray();
        Assert.All(columns, col => Assert.Equal(6, col.Values.Length));
        var bad = JudgmentSpec.Cell(c[1]).Split(',').Select(s => JudgmentSpec.Int(s) - 1).ToArray();
        var expected = JudgmentSpec.Cell(c[2]);
        var hit = JudgmentSpec.Cell(c[3]);
        var seen = JudgmentSpec.Cell(c[4]);

        var kinds = Enumerable.Repeat(SceneRow.Match, 6).ToArray();
        for (var i = 0; i < bad.Length; i++)
        {
            kinds[bad[i]] = i % 2 == 0 ? SceneRow.Mismatch : SceneRow.Missing;
        }

        string? group = null, value = null;
        if (hit != JudgmentSpec.None)
        {
            var parts = hit.Split(" = ");
            Assert.Equal(2, parts.Length);
            group = parts[0];
            value = parts[1] == JudgmentSpec.EmptyValue ? string.Empty : parts[1];
        }

        var scene = new JudgmentScene(
            Columns: columns.Select(col => col.Name).ToArray(),
            Rows: Enumerable.Range(0, 6).Select(r => new SceneRow(JudgmentInput.Key(r), columns.Select(col => col.Values[r]).ToArray(), kinds[r])).ToArray(),
            Extras: [],
            AccuracyMin: "0",
            MissingMax: "100",
            TimeMustDrop: "false")
        {
            FocusGroup = group,
            FocusValue = value,
            FocusColumns = seen == JudgmentSpec.None ? null : seen,
        };

        var focus = JudgmentInput.CheckOf(scene.Judge(), JudgmentSpec.Focus);

        Assert.Equal(expected, focus.Result);
        Assert.Equal(expected == JudgmentInput.Found, hit != JudgmentSpec.None);
        Assert.Equal(expected is JudgmentInput.Spread or JudgmentInput.OneValue, seen != JudgmentSpec.None);
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindCheck(JudgmentSpec.Focus, expected).Template, scene.Slots()), focus.Reason);
    }

    // 6.3 두 표가 FOCUS 결과 다섯을 모두 쓴다(빈 통과 방지).
    [Fact]
    public void Focus_tables_cover_all_focus_results()
    {
        var results = JudgmentSpec.Table(JudgmentSpec.FocusHeader).Select(r => JudgmentSpec.Cell(r[4]))
            .Concat(JudgmentSpec.Table(JudgmentSpec.FocusColumnsHeader).Select(r => JudgmentSpec.Cell(r[2])));

        Assert.Equal(
            JudgmentSpec.CheckRows().Where(r => r.Check == JudgmentSpec.Focus).Select(r => r.Result).Order(StringComparer.Ordinal),
            results.Distinct().Order(StringComparer.Ordinal));
    }

    // 4.8 규칙 1: 유형 열은 모든 기준 파일의 머리 행에 있는 이름이다. 한 파일에만 있는 열(q)은 유형 열이 아니다.
    // q로 보면 불일치 둘이 모두 q = x라 found지만, q는 b.csv에 없어 p만 본다(p는 흩어짐 → spread).
    // 기준 파일 순서를 뒤집어 넘겨도 같은 결과다(규칙 2).
    [Fact]
    public void Column_in_only_one_reference_file_is_not_a_type_column()
    {
        var definition = CompareInput.Case(FocusCase);
        SourceFile[] reference =
        [
            CompareInput.Source("reference/a.csv", "id,p,q,v\nk1,a,x,1\nk2,b,x,1\nk3,a,y,1\nk4,b,y,1\n"),
            CompareInput.Source("reference/b.csv", "id,v,p\nk5,1,a\nk6,1,b\n"),
        ];
        SourceFile[] candidate = [CompareInput.Source("candidate/a.csv", "id,v\nk1,2\nk3,1\nk4,1\nk5,1\nk6,1\n")];

        foreach (var files in new[] { reference, Enumerable.Reverse(reference).ToArray() })
        {
            var judged = JudgmentEngine.Judge(definition, CompareInput.Measured(CompareInput.Run(definition, files, candidate)));
            var focus = JudgmentInput.CheckOf(judged, JudgmentSpec.Focus);

            Assert.Equal(JudgmentInput.Spread, focus.Result);
            Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindCheck(JudgmentSpec.Focus, JudgmentInput.Spread).Template, new Dictionary<string, string> { ["bad"] = "2", ["columns"] = "p" }), focus.Reason);
        }
    }

    // 4.8 규칙 1·3: 열 순서가 파일마다 달라도 같은 이름의 열이다. 값은 모든 기준 파일의 기준 행에서 센다.
    [Fact]
    public void Type_column_values_are_counted_across_reference_files()
    {
        var definition = CompareInput.Case(FocusCase);
        SourceFile[] reference =
        [
            CompareInput.Source("reference/a.csv", "id,p,v\nk1,a,1\nk2,b,1\nk3,b,1\n"),
            CompareInput.Source("reference/b.csv", "id,v,p\nk4,1,a\nk5,1,a\nk6,1,b\n"),
        ];
        SourceFile[] candidate = [CompareInput.Source("candidate/a.csv", "id,v\nk1,2\nk2,1\nk3,1\nk5,1\nk6,1\n")];

        var focus = JudgmentInput.CheckOf(JudgmentEngine.Judge(definition, CompareInput.Measured(CompareInput.Run(definition, reference, candidate))), JudgmentSpec.Focus);

        Assert.Equal(JudgmentInput.Found, focus.Result);
        Assert.Equal(
            JudgmentSpec.Fill(
                JudgmentSpec.FindCheck(JudgmentSpec.Focus, JudgmentInput.Found).Template,
                new Dictionary<string, string> { ["bad"] = "2", ["group"] = "p", ["value"] = "a", ["rows_in"] = "3", ["rows_out"] = "3" }),
            focus.Reason);
    }

    // 4.8 규칙 3·5: 몰린 값은 불일치 행의 값이다. 첫 기준 행의 값이 아니다(T-011b 리뷰 B1).
    // 기준 k1·k2는 grp = a, k3·k4는 b. 결과에서 k3는 값 불일치, k4는 누락이다. 불일치 둘이 모두 b라 found(grp = b).
    // 첫 기준 행 k1은 a이고 일치다. 다른 장면은 불일치 행이 앞에 있어 이 차이를 보지 못한다.
    [Fact]
    public void Found_value_comes_from_mismatch_rows_not_first_reference_row()
    {
        var definition = CompareInput.Case(FocusCase);
        SourceFile[] reference = [CompareInput.Source("reference/a.csv", "id,grp,v\nk1,a,1\nk2,a,1\nk3,b,1\nk4,b,1\n")];
        SourceFile[] candidate = [CompareInput.Source("candidate/a.csv", "id,v\nk1,1\nk2,1\nk3,2\n")];

        var judged = JudgmentEngine.Judge(definition, CompareInput.Measured(CompareInput.Run(definition, reference, candidate)));
        var focus = JudgmentInput.CheckOf(judged, JudgmentSpec.Focus);

        // 4.5 값: 불일치 2, 몰린 열 grp, 값 b, b인 기준 행 2, 나머지 기준 행 2.
        var slots = new Dictionary<string, string> { ["bad"] = "2", ["group"] = "grp", ["value"] = "b", ["rows_in"] = "2", ["rows_out"] = "2" };
        Assert.Equal(JudgmentInput.Found, focus.Result);
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindCheck(JudgmentSpec.Focus, JudgmentInput.Found).Template, slots), focus.Reason);
        Assert.Equal((JudgmentEngine.VerdictConditional, JudgmentEngine.RuleFocus), (judged.Verdict, judged.Rule));
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindVerdict(JudgmentSpec.Focus, JudgmentInput.Found).Template, slots), judged.Reason);
    }

    // 4.8 규칙 1: 빈 이름 열은 유형 열이 아니다. 빈 이름 열의 값이 불일치 행에서 모두 같아도 유형 열이 없어 off다.
    [Fact]
    public void Empty_column_name_is_not_a_type_column()
    {
        var scene = new JudgmentScene(
            Columns: [string.Empty],
            Rows: JudgmentInput.Rows(6, mismatch: 2, types: i => [i < 2 ? "a" : "b"]),
            Extras: [],
            AccuracyMin: "0",
            MissingMax: "100",
            TimeMustDrop: "false");

        var judged = scene.Judge();

        Assert.Equal(JudgmentInput.Off, JudgmentInput.CheckOf(judged, JudgmentSpec.Focus).Result);
        Assert.Contains("add_type_column", JudgmentInput.TodoKeys(judged));
    }

    // 4.8 규칙 4: 불일치가 focus_min보다 적으면 유형 열이 없어도 few다(off가 아니다). form-fill(7.3)과 같다.
    [Fact]
    public void Few_comes_before_off()
    {
        var scene = new JudgmentScene(Columns: [], Rows: JudgmentInput.Rows(6, mismatch: 1), Extras: [], AccuracyMin: "0", MissingMax: "100", TimeMustDrop: "false");

        var judged = scene.Judge();

        Assert.Equal(JudgmentInput.Few, JudgmentInput.CheckOf(judged, JudgmentSpec.Focus).Result);
        Assert.DoesNotContain("add_type_column", JudgmentInput.TodoKeys(judged));
    }

    // 4.3 6행: 몰림이 found이고 앞 행에 걸리지 않으면 조건부 FOCUS. 판정 문장의 {group} = {value}는 확인 문장과 같다.
    [Fact]
    public void Focus_found_makes_conditional_with_group_and_value()
    {
        var scene = JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.Explained, JudgmentInput.Off, JudgmentInput.Found);

        var judged = scene.Judge();

        Assert.Equal((JudgmentEngine.VerdictConditional, JudgmentEngine.RuleFocus), (judged.Verdict, judged.Rule));
        Assert.Contains($"{JudgmentInput.Group} = a인 행", judged.Reason, StringComparison.Ordinal);
    }

    // "`a×5`" → a 다섯. "`(빈 칸)×5`" → 빈 값 다섯. "×N"이 없으면 하나.
    private static string[] Expand(IEnumerable<string> spans) =>
        spans.SelectMany(s =>
        {
            var m = Times().Match(s);
            var value = m.Success ? m.Groups["value"].Value : s;
            var count = m.Success ? JudgmentSpec.Int(m.Groups["count"].Value) : 1;
            return Enumerable.Repeat(value == JudgmentSpec.EmptyValue ? string.Empty : value, count);
        }).ToArray();

    // "p=aaabbb" → (p, [a,a,a,b,b,b]). '_'는 빈 칸.
    private static (string Name, string[] Values) ParseColumn(string span)
    {
        var parts = span.Split('=');
        Assert.Equal(2, parts.Length);
        return (parts[0], parts[1].Select(ch => ch == '_' ? string.Empty : ch.ToString()).ToArray());
    }

    [GeneratedRegex(@"^(?<value>.*)×(?<count>[0-9]+)\z", RegexOptions.CultureInvariant)]
    private static partial Regex Times();
}
