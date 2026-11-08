using System.Text.RegularExpressions;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Comparison;

namespace Scorecard.Tests.Judgment;

// 스펙 11.8 "특정 유형에만 불일치가 몰림"(T-012 M-A, 4.8 M-B를 대신한다)과 11.8 두 표. 카드 수용 기준 2(T-012 카드 수용 기준 2).
// 열은 case.yaml inputs.group 하나다. 적지 않으면 off. 불일치가 focus_min보다 적으면 few(열을 보지 않는다).
// 값은 기준 행의 그 칸만 본다. 결과 자료·추가 행·중복 행은 보지 않는다. 열이 없거나 둘이면 대조 오류다(metrics.md 12.3).
[Trait("Spec", "judgment")]
public partial class FocusTests
{
    // 11.8 첫 표·둘째 표의 행 번호.
    public static TheoryData<int> FocusRows => new(Enumerable.Range(0, JudgmentSpec.Table(JudgmentSpec.FocusHeader).Count));

    public static TheoryData<int> FocusColumnRows => new(Enumerable.Range(0, JudgmentSpec.Table(JudgmentSpec.FocusColumnsHeader).Count));

    // 11.8 첫 표(기준 자료에 늘 grp 열): 행마다 FOCUS 결과와 확인 문장(11.5 틀). "inputs.group"이 없음이면 키를 적지 않는다.
    [Theory]
    [MemberData(nameof(FocusRows))]
    public void Single_type_column_row_gives_its_focus(int index)
    {
        var c = JudgmentSpec.Table(JudgmentSpec.FocusHeader)[index];
        var reference = Expand(JudgmentSpec.List(c[0]));
        var bad = Expand(JudgmentSpec.List(c[1]));
        var extras = Expand(JudgmentSpec.List(c[2]));
        var group = GroupCell(c[3]);
        Assert.True(group is null or JudgmentInput.Group, $"11.8 첫 표 inputs.group 칸 '{c[3]}'은 grp나 없음이어야 한다.");
        var expected = JudgmentSpec.Cell(c[4]);

        // 불일치 행: 그 유형 값의 아직 쓰지 않은 기준 행을 앞에서부터 고른다. 값 불일치·누락을 번갈아 둔다.
        var kinds = Enumerable.Repeat(SceneRow.Match, reference.Length).ToArray();
        for (var i = 0; i < bad.Length; i++)
        {
            var row = Enumerable.Range(0, reference.Length).First(r => reference[r] == bad[i] && kinds[r] == SceneRow.Match);
            kinds[row] = i % 2 == 0 ? SceneRow.Mismatch : SceneRow.Missing;
        }

        var scene = new JudgmentScene(
            Columns: [JudgmentInput.Group],
            Rows: reference.Select((v, i) => new SceneRow(JudgmentInput.Key(i), [v], kinds[i])).ToArray(),
            Extras: extras.Select((v, i) => new SceneRow(JudgmentInput.ExtraKey(i), [v], SceneRow.Match)).ToArray(),
            AccuracyMin: "0",
            MissingMax: "100",
            TimeMustDrop: "false")
        {
            Group = group,
            FocusValue = expected == JudgmentInput.Found ? bad[0] : null,
        };

        var judged = scene.Judge();
        var focus = JudgmentInput.CheckOf(judged, JudgmentSpec.Focus);

        Assert.Equal(expected, focus.Result);
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindCheck(JudgmentSpec.Focus, expected).Template, scene.Slots()), focus.Reason);
        Assert.Equal(expected == JudgmentInput.Off, JudgmentInput.TodoKeys(judged).Contains(AddGroup));
    }

    // 11.8 둘째 표(열 여럿): 머리 행 id,<열들>,v, 키 id, compare v. inputs.group에 적은 열 하나만 본다.
    // 행마다 FOCUS 결과와 걸린 값({group} = {value}). "대조 오류"·"파서 오류" 행은 판정까지 가지 않고 그 오류가 난다.
    [Theory]
    [MemberData(nameof(FocusColumnRows))]
    public void Multiple_type_columns_row_gives_its_focus(int index)
    {
        var c = JudgmentSpec.Table(JudgmentSpec.FocusColumnsHeader)[index];
        var columns = JudgmentSpec.List(c[0]).Select(ParseColumn).ToArray();
        Assert.All(columns, col => Assert.Equal(6, col.Values.Length));
        var group = GroupCell(c[1]);
        var bad = JudgmentSpec.Cell(c[2]).Split(',').Select(s => JudgmentSpec.Int(s) - 1).ToArray();
        var expected = JudgmentSpec.Cell(c[3]);
        var hit = JudgmentSpec.Cell(c[4]);

        var kinds = Enumerable.Repeat(SceneRow.Match, 6).ToArray();
        for (var i = 0; i < bad.Length; i++)
        {
            kinds[bad[i]] = i % 2 == 0 ? SceneRow.Mismatch : SceneRow.Missing;
        }

        string? value = null;
        if (hit != JudgmentSpec.None)
        {
            var parts = hit.Split(" = ");
            Assert.Equal(2, parts.Length);
            Assert.Equal(group, parts[0]);
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
            Group = group,
            FocusValue = value,
        };

        // "대조 오류 `코드`": 기준 파일 머리 행의 group 열 확인(metrics.md 12.3). 경로는 그 열 이름이다.
        if (expected.StartsWith(CompareErrorPrefix, StringComparison.Ordinal))
        {
            var code = Assert.Single(SpecDocument.CodeSpans(expected));
            var result = CompareInput.Run(scene.Definition, scene.ReferenceCsv, scene.CandidateCsv);
            var error = Assert.Single(result.Errors);
            Assert.Equal((code, group, CompareInput.ReferencePath, 1), (error.Code, error.Path, error.Source, error.Line));
            Assert.Null(result.Metrics);
            Assert.Equal(JudgmentSpec.None, hit);
            return;
        }

        // "파서 오류 `코드`": group이 키·compare 필드와 같다(case-schema.md 10.2). 경로는 inputs.group이다.
        if (expected.StartsWith(ParserErrorPrefix, StringComparison.Ordinal))
        {
            var code = Assert.Single(SpecDocument.CodeSpans(expected));
            var parsed = CaseInput.Parse(scene.CaseYaml);
            var error = Assert.Single(parsed.Errors);
            Assert.Equal((code, GroupPath), (error.Code, error.Path));
            Assert.Null(parsed.Case);
            Assert.Equal(JudgmentSpec.None, hit);
            return;
        }

        var focus = JudgmentInput.CheckOf(scene.Judge(), JudgmentSpec.Focus);

        Assert.Equal(expected, focus.Result);
        Assert.Equal(expected == JudgmentInput.Found, hit != JudgmentSpec.None);
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindCheck(JudgmentSpec.Focus, expected).Template, scene.Slots()), focus.Reason);
    }

    // 11.8 두 표가 FOCUS 결과 다섯을 모두 쓴다(빈 통과 방지). 둘째 표에 대조 오류 둘(column, duplicate-column)과 파서 오류(conflict)가 있다.
    [Fact]
    public void Focus_tables_cover_all_focus_results()
    {
        var second = JudgmentSpec.Table(JudgmentSpec.FocusColumnsHeader).Select(r => JudgmentSpec.Cell(r[3])).ToArray();
        var results = JudgmentSpec.Table(JudgmentSpec.FocusHeader).Select(r => JudgmentSpec.Cell(r[4]))
            .Concat(second.Where(r => !r.StartsWith(CompareErrorPrefix, StringComparison.Ordinal) && !r.StartsWith(ParserErrorPrefix, StringComparison.Ordinal)));

        Assert.Equal(
            JudgmentSpec.CheckRows().Where(r => r.Check == JudgmentSpec.Focus).Select(r => r.Result).Order(StringComparer.Ordinal),
            results.Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(
            ["`column`", "`duplicate-column`"],
            second.Where(r => r.StartsWith(CompareErrorPrefix, StringComparison.Ordinal)).Select(r => r[CompareErrorPrefix.Length..]).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(["`conflict`"], second.Where(r => r.StartsWith(ParserErrorPrefix, StringComparison.Ordinal)).Select(r => r[ParserErrorPrefix.Length..]).Distinct());
    }

    // 11.8 첫 글머리(metrics.md 12.3): group 열은 모든 기준 파일의 머리 행에 꼭 한 번 있어야 한다.
    // 한 파일에만 있는 열(q)을 적으면 다른 파일(b.csv)에서 대조 오류 column이다. 판정까지 가지 않는다.
    // 모든 파일에 있는 p를 적으면 p만 본다(흩어짐 → spread). q가 걸릴 모양이어도 보지 않는다.
    // 기준 파일 순서를 뒤집어 넘겨도 같은 결과다.
    [Fact]
    public void Column_in_only_one_reference_file_is_not_a_type_column()
    {
        SourceFile[] reference =
        [
            CompareInput.Source("reference/a.csv", "id,p,q,v\nk1,a,x,1\nk2,b,x,1\nk3,a,y,1\nk4,b,y,1\n"),
            CompareInput.Source("reference/b.csv", "id,v,p\nk5,1,a\nk6,1,b\n"),
        ];
        SourceFile[] candidate = [CompareInput.Source("candidate/a.csv", "id,v\nk1,2\nk3,1\nk4,1\nk5,1\nk6,1\n")];

        foreach (var files in new[] { reference, Enumerable.Reverse(reference).ToArray() })
        {
            var byP = CompareInput.Case(FocusCase("p"));
            var judged = JudgmentEngine.Judge(byP, CompareInput.Measured(CompareInput.Run(byP, files, candidate)));
            var focus = JudgmentInput.CheckOf(judged, JudgmentSpec.Focus);

            Assert.Equal(JudgmentInput.Spread, focus.Result);
            Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindCheck(JudgmentSpec.Focus, JudgmentInput.Spread).Template, new Dictionary<string, string> { ["bad"] = "2", ["group"] = "p" }), focus.Reason);

            CompareInput.AssertErrors(
                CompareInput.Run(CompareInput.Case(FocusCase("q")), files, candidate),
                "reference/b.csv:1: column q: 머리 행에 열이 없다");
        }
    }

    // 11.8 규칙 3: 열 순서가 파일마다 달라도 같은 이름의 열이다. 값은 모든 기준 파일의 기준 행에서 센다.
    [Fact]
    public void Type_column_values_are_counted_across_reference_files()
    {
        var definition = CompareInput.Case(FocusCase("p"));
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

    // 11.8 규칙 3·5: 몰린 값은 불일치 행의 값이다. 첫 기준 행의 값이 아니다(T-011b 리뷰 B1).
    // 기준 k1·k2는 grp = a, k3·k4는 b. 결과에서 k3는 값 불일치, k4는 누락이다. 불일치 둘이 모두 b라 found(grp = b).
    // 첫 기준 행 k1은 a이고 일치다. 다른 장면은 불일치 행이 앞에 있어 이 차이를 보지 못한다.
    [Fact]
    public void Found_value_comes_from_mismatch_rows_not_first_reference_row()
    {
        var definition = CompareInput.Case(FocusCase(JudgmentInput.Group));
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

    // 11.8 규칙 2: inputs.group을 적지 않으면 열이 무엇이든 off다. 빈 이름 열의 값이 불일치 행에서 모두 같아도 off이고 add_group이 남는다.
    // (빈 이름은 inputs.group에 적을 수도 없다: 파서 required, case-schema.md 10.2.)
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
        Assert.Contains(AddGroup, JudgmentInput.TodoKeys(judged));

        var parsed = CaseInput.Parse(CompareInput.InputsCase.Replace("key: id}", "key: id, group: \"\"}", StringComparison.Ordinal) + "compare:\n  - {field: v, tolerance: 0}\n");
        var error = Assert.Single(parsed.Errors);
        Assert.Equal(("required", GroupPath), (error.Code, error.Path));
    }

    // 11.8 규칙 1: 불일치가 focus_min보다 적으면 inputs.group이 없어도 few다(off가 아니다). add_group도 없다. form-fill(7.3)과 같다(11.7 둘째 글머리).
    [Fact]
    public void Few_comes_before_off()
    {
        var scene = new JudgmentScene(Columns: [], Rows: JudgmentInput.Rows(6, mismatch: 1), Extras: [], AccuracyMin: "0", MissingMax: "100", TimeMustDrop: "false");

        var judged = scene.Judge();

        Assert.Equal(JudgmentInput.Few, JudgmentInput.CheckOf(judged, JudgmentSpec.Focus).Result);
        Assert.DoesNotContain(AddGroup, JudgmentInput.TodoKeys(judged));
    }

    // 11.4 6행: 몰림이 found이고 앞 행에 걸리지 않으면 조건부 FOCUS. 판정 문장의 {group} = {value}는 확인 문장과 같다.
    [Fact]
    public void Focus_found_makes_conditional_with_group_and_value()
    {
        var scene = JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.Explained, JudgmentInput.Off, JudgmentInput.Found);

        var judged = scene.Judge();

        Assert.Equal((JudgmentEngine.VerdictConditional, JudgmentEngine.RuleFocus), (judged.Verdict, judged.Rule));
        Assert.Contains($"{JudgmentInput.Group} = a인 행", judged.Reason, StringComparison.Ordinal);
    }

    // 11.8 규칙 3: 결과 자료의 group 열 값과 중복 행은 몰림에 들지 않는다. 결과 쪽 grp를 모두 a로 바꾸고 중복 행을 더해도
    // 기준 행 값(불일치 a·b)으로 보아 spread다. 결과 자료에 group 열이 없어도 된다(metrics.md 12.3).
    [Fact]
    public void Candidate_values_and_duplicate_rows_do_not_count_for_focus()
    {
        var definition = CompareInput.Case(FocusCase(JudgmentInput.Group));
        const string reference = "id,grp,v\nk1,a,1\nk2,b,1\nk3,a,1\nk4,b,1\n";

        foreach (var candidate in new[] { "id,grp,v\nk1,a,2\nk2,a,2\nk3,a,1\nk4,a,1\nk3,a,9\nk3,a,9\n", "id,v\nk1,2\nk2,2\nk3,1\nk4,1\nk3,9\nk3,9\n" })
        {
            var metrics = CompareInput.Measured(CompareInput.Run(definition, reference, candidate));
            var focus = JudgmentInput.CheckOf(JudgmentEngine.Judge(definition, metrics), JudgmentSpec.Focus);

            Assert.Equal(2, metrics.Items.Count(i => i.Status == CompareEngine.ItemDuplicate));
            Assert.Equal(JudgmentInput.Spread, focus.Result);
        }
    }

    // 4.8 결과 쪽 중복 글머리(T-013, 카드 수용 기준 6): 이미 불일치인 키를 결과 쪽에 한 번 더 두어도 {bad}가 그대로다.
    // 그 키의 duplicate 항목은 기준 행 위치를 가지지만 몰림 셈({bad}, {rows_in}, {rows_out})에 들지 않는다(규칙 3).
    // 확인 문장이 중복 행이 없는 장면과 같다: "a 2행 중 2행, 다른 값 8행 중 0행".
    [Fact]
    public void Duplicate_of_a_mismatch_key_does_not_change_focus_counts()
    {
        var plain = DuplicateFocusScene(mismatch: [0, 5]);
        var repeated = plain with { Duplicates = [plain.Rows[0]] };

        var before = JudgmentInput.CheckOf(plain.Judge(), JudgmentSpec.Focus);
        var after = JudgmentInput.CheckOf(repeated.Judge(), JudgmentSpec.Focus);

        Assert.Equal(JudgmentInput.Found, after.Result);
        Assert.Equal(before, after);
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindCheck(JudgmentSpec.Focus, JudgmentInput.Found).Template, repeated.Slots()), after.Reason);
        Assert.Contains("(a 2행 중 2행, 다른 값 8행 중 0행)", after.Reason, StringComparison.Ordinal);
        Assert.Single(repeated.Metrics().Items, i => i.Status == CompareEngine.ItemDuplicate && i.Reference is not null);
    }

    // 4.8 결과 쪽 중복 글머리(T-013): 일치하던 기준 키를 결과 쪽에 한 번 더 두면 그 키가 값 불일치로 몰림에 든다(한 번만).
    // 불일치 1건(few)이던 장면이 불일치 2건(둘 다 a)이 되어 found다. 기준 행 수는 그대로다: "a 2행 중 2행, 다른 값 8행 중 0행".
    [Fact]
    public void Duplicate_of_a_match_key_counts_that_key_once_for_focus()
    {
        var plain = DuplicateFocusScene(mismatch: [0]);
        var repeated = plain with { Duplicates = [plain.Rows[5]] };

        Assert.Equal(JudgmentInput.Few, JudgmentInput.CheckOf(plain.Judge(), JudgmentSpec.Focus).Result);
        var judged = repeated.Judge();
        var focus = JudgmentInput.CheckOf(judged, JudgmentSpec.Focus);

        Assert.Equal([plain.Rows[0].Key, plain.Rows[5].Key], repeated.BadKeys);
        Assert.Equal(JudgmentInput.Found, focus.Result);
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindCheck(JudgmentSpec.Focus, JudgmentInput.Found).Template, repeated.Slots()), focus.Reason);
        Assert.Contains("불일치 2건이 모두 grp = a인 행이다(a 2행 중 2행, 다른 값 8행 중 0행)", focus.Reason, StringComparison.Ordinal);
        Assert.Equal((JudgmentEngine.VerdictConditional, JudgmentEngine.RuleFocus), (judged.Verdict, judged.Rule));
    }

    // 4.8 중복 장면: 기준 10행, grp는 0행·5행만 a(나머지 b). mismatch 행은 값 불일치(누락이면 중복 행을 둘 수 없다). inputs.group = grp.
    private static JudgmentScene DuplicateFocusScene(int[] mismatch) =>
        new(
            Columns: [JudgmentInput.Group],
            Rows: Enumerable.Range(0, 10)
                .Select(i => new SceneRow(JudgmentInput.Key(i), [i is 0 or 5 ? "a" : "b"], mismatch.Contains(i) ? SceneRow.Mismatch : SceneRow.Match))
                .ToArray(),
            Extras: [],
            AccuracyMin: "0",
            MissingMax: "100",
            TimeMustDrop: "false")
        {
            Group = JudgmentInput.Group,
            FocusValue = "a",
        };

    private const string AddGroup = "add_group";
    private const string GroupPath = "inputs.group";
    private const string CompareErrorPrefix = "대조 오류 ";
    private const string ParserErrorPrefix = "파서 오류 ";

    // 머리 행 id,…,v, 키 id, compare v(tolerance 0). 문턱은 늘 met(정확도 0, 누락률 100), 시간 off. inputs.group = group.
    private static string FocusCase(string group) => Scorecard.Tests.Synthetic.SyntheticInput.OneStepCase +
        $"inputs: {{reference: reference/*.csv, candidate: candidate/*.csv, key: id, group: {group}}}\ncompare:\n  - {{field: v, tolerance: 0}}\n" +
        "thresholds:\n  accuracy_min: 0\n  missing_max: 100\n  time_must_drop: false\n";

    // 11.8 "inputs.group" 칸: `열 이름` 또는 없음(null).
    private static string? GroupCell(string cell) => cell.Trim() == "없음" ? null : JudgmentSpec.Cell(cell);

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
