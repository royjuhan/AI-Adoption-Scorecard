using Scorecard.Core.Synthetic;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Synthetic;

// 스펙 5절 예외 표, 4.6 case.yaml과 계획 맞추기, 3.3 CSV 값 규칙.
// 5절 표의 "상황" 칸마다 아래 Cases에 경우가 있어야 한다. 표에 행이 늘면 여기서 실패한다.
// 메시지는 한글이고, 어느 필드·값인지 적는다(5절 아래). 필드·값이 정해지는 경우만 이름을 찾는다.
[Trait("Spec", "synthetic-examples")]
public class ValidationTests
{
    private const string Example = "table-compare";

    // 3.3·5절: CSV 값에 쓸 수 없는 것. Cases보다 먼저 둔다(정적 필드는 적힌 순서로 초기화된다). 0x20~0x7E 밖(제어 문자, DEL)도 넣는다.
    private static readonly string[] BadCells = ["a,b", "a\"b", "a\nb", "a\rb", "a\tb", "\t", "가", "", "a\u007Fb", "a\u0001b"];

    // 상황 → 경우 목록. Expect는 메시지에 있어야 할 필드·값(없으면 null).
    private static readonly Dictionary<string, Case[]> Cases = new(StringComparer.Ordinal)
    {
        ["`definition`·`plan`이 null"] =
        [
            new("definition null", () => SyntheticGenerator.Generate(null!, Plan()), null),
            new("plan null", () => SyntheticGenerator.Generate(TableCompare(), null!), null),
        ],
        ["`case.yaml`에 `inputs`가 없음"] =
        [
            new("mixed-exceptions", () => SyntheticGenerator.Generate(SyntheticInput.CaseOf("mixed-exceptions"), Plan()), null),
        ],
        ["`compare` 필드가 계획 열에 없음"] =
        [
            new("length 열 없음", () => Generate(Plan() with { Columns = [.. Plan().Columns.Where(c => c.Name != "length")], MismatchColumns = ["mean_value"] }), "length"),
        ],
        ["값 불일치 열이 `compare` 필드가 아님"] =
        [
            new("signal_type", () => Generate(Plan() with { MismatchColumns = ["signal_type"] }), "signal_type"),
            new("없는 열", () => Generate(Plan() with { MismatchColumns = ["no_such_column"] }), "no_such_column"),
        ],
        ["허용오차 안 차이 열에 `tolerance_pct`가 없음"] =
        [
            new("정수 열 length", () => Generate(Plan() with { WithinColumn = "length" }), "length"),
            new("compare 밖 열 signal_type", () => Generate(Plan() with { WithinColumn = "signal_type" }), "signal_type"),
            new("열 없이 건수만", () => Generate(Plan() with { WithinColumn = null }), null),
        ],
        ["`tolerance_pct` 필드가 정수 열"] =
        [
            new("mean_value 정수", () => Generate(Plan() with { Columns = Replace(Plan(), SyntheticColumn.Integer("mean_value", 10, 99)) }), "mean_value"),
        ],
        ["키 자릿수가 모자람(기준+추가 건수의 자릿수보다 작음)"] =
        [
            new("31건에 1자리", () => Generate(Plan() with { KeyDigits = 1 }), null),
            new("9+1건에 1자리", () => Generate(Plan() with { KeyDigits = 1, Rows = 9, Extra = 1 }), null),
        ],
        ["선택지에 `,`나 한글"] =
        [
            new("쉼표", () => Generate(Plan() with { Columns = Replace(Plan(), SyntheticColumn.Choice("signal_type", "normal,x", "irregular")) }), "normal,x"),
            new("한글", () => Generate(Plan() with { Columns = Replace(Plan(), SyntheticColumn.Choice("signal_type", "정상", "irregular")) }), "정상"),
        ],
        ["파일 이름에 폴더"] =
        [
            new("sub/", () => Generate(Plan() with { FileName = "sub/logs.csv" }), null),
            new("sub\\", () => Generate(Plan() with { FileName = "sub\\logs.csv" }), null),
            new("../", () => Generate(Plan() with { FileName = "../logs.csv" }), null),
        ],
        ["몰림 행이 누락 + 값 불일치보다 적음"] =
        [
            // irregular 기준 행 12개(6.1). 누락 10 + 값 불일치 2 = 12는 된다(PositiveControls), 11 + 2 = 13은 안 된다.
            new("누락 11", () => Generate(Plan() with { Missing = 11, Within = 0, WithinColumn = null }), null),
        ],
        ["범위가 좁아 불일치 값을 못 만듦"] =
        [
            new("length 50~50", () => Generate(Plan() with { Columns = Replace(Plan(), SyntheticColumn.Integer("length", 50, 50)) }), "length"),
        ],
        ["CSV 값에 `,` `\"` `\\n` `\\r` `\\t`, 한글, 빈 값"] =
        [
            .. BadCells.Select(v => new Case($"Render '{Show(v)}'", () => SyntheticCsv.Render(["k", "v"], [new SyntheticRow("k1", [v], SyntheticGenerator.PlantedNone)]), null)),
            .. BadCells.Select(v => new Case($"CheckCell '{Show(v)}'", () => SyntheticCsv.CheckCell(v), null)),
            new("머리 행", () => SyntheticCsv.Render(["k", "a,b"], []), "a,b"),
            new("키", () => SyntheticCsv.Render(["k", "v"], [new SyntheticRow("k,1", ["1"], SyntheticGenerator.PlantedNone)]), "k,1"),
        ],
        ["같은 열 이름"] =
        [
            new("length 두 번", () => Generate(Plan() with { Columns = [.. Plan().Columns, SyntheticColumn.Integer("length", 50, 500)] }), "length"),
            new("키 열 이름과 같음", () => Generate(Plan() with { Columns = [.. Plan().Columns, SyntheticColumn.Integer("log_id", 0, 9)] }), "log_id"),
        ],
    };

    public static TheoryData<string> Situations => new(Cases.Keys);

    // 5절 표의 상황 = 이 파일의 경우. 결과 칸의 첫 `코드`가 예외 이름이다.
    [Fact]
    public void Error_table_situations_all_have_cases()
    {
        var rows = SyntheticSpec.Table(SyntheticSpec.ErrorHeader);

        Assert.Equal(rows.Select(c => c[0]).Order(StringComparer.Ordinal), Cases.Keys.Order(StringComparer.Ordinal));
        Assert.All(rows, c => Assert.Contains(SpecDocument.CodeSpans(c[1])[0], new[] { nameof(ArgumentNullException), nameof(ArgumentException) }));
    }

    // 5절 표의 행마다: 결과 칸의 예외가 난다. 메시지는 한글이고, 정해진 필드·값이 있으면 그 이름이 있다.
    [Theory]
    [MemberData(nameof(Situations))]
    public void Situation_throws_spec_exception(string situation)
    {
        var row = Assert.Single(SyntheticSpec.Table(SyntheticSpec.ErrorHeader), c => c[0] == situation);
        var expected = SpecDocument.CodeSpans(row[1])[0];
        var wrong = new List<string>();

        foreach (var (label, action, expect) in Cases[situation])
        {
            Exception? caught = null;
            try
            {
                action();
            }
            catch (Exception ex)
            {
                caught = ex;
            }

            if (caught is null)
            {
                wrong.Add($"{label}: 예외 없음");
                continue;
            }

            var ok = expected == nameof(ArgumentNullException) ? caught is ArgumentNullException : caught is ArgumentException;
            if (!ok)
            {
                wrong.Add($"{label}: {expected}가 아니라 {caught.GetType().Name}: {caught.Message}");
                continue;
            }

            if (!caught.Message.Any(ch => ch is >= '가' and <= '힣') && caught is not ArgumentNullException)
            {
                wrong.Add($"{label}: 메시지가 한글이 아니다: {caught.Message}");
            }

            if (expect is not null && !caught.Message.Contains(expect, StringComparison.Ordinal))
            {
                wrong.Add($"{label}: 메시지에 '{expect}'가 없다: {caught.Message}");
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    // 위 경우들이 그 조건 때문에 실패한다는 것을 보이는 짝: 경계 바로 안쪽은 생성된다.
    [Fact]
    public void Positive_controls_generate()
    {
        Generate(Plan());

        // 몰림 행 12 = 누락 10 + 값 불일치 2.
        Assert.Equal(10, Generate(Plan() with { Missing = 10, Within = 0, WithinColumn = null }).Reference.Count(r => r.Planted == SyntheticGenerator.PlantedMissing));

        // 9건 + 추가 0건이면 1자리로 된다.
        Assert.Equal("log9", Generate(Plan() with { KeyDigits = 1, Rows = 9, Extra = 0 }).Reference[^1].Key);

        // 계획 열은 compare에 없어도 된다(4.6). signal_type이 그렇다. 하나 더 둬도 된다.
        Generate(Plan() with { Columns = [.. Plan().Columns, SyntheticColumn.Choice("extra_label", "p", "q")] });

        // CSV 값으로 쓸 수 있는 것.
        foreach (var value in new[] { "log01", "58.25", "item-a", "-3", "a b", "~" })
        {
            SyntheticCsv.CheckCell(value);
        }
    }

    // 4.6 표: compare 항목별로 맞는 열 종류만 생성된다. 나머지 종류는 ArgumentException.
    // 케이스: n(tolerance 0), d(tolerance_pct 1.0), c(허용 오차 없음). 열 하나씩 세 종류로 바꿔 본다.
    [Fact]
    public void Compare_kind_must_match_column_kind()
    {
        var definition = SyntheticInput.Case(SyntheticInput.ThreeKindCase);
        var kinds = PlanTableTests.CompareKinds();
        var wrong = new List<string>();

        foreach (var field in definition.Compare)
        {
            foreach (var kind in new[] { SyntheticColumn.KindInteger, SyntheticColumn.KindDecimal, SyntheticColumn.KindChoice })
            {
                var plan = ThreeKindPlan() with { Columns = Replace(ThreeKindPlan(), Column(field.Field, kind)) };
                var fits = kinds[PlanTableTests.CompareLabel(field)] == kind;
                try
                {
                    SyntheticGenerator.Generate(definition, plan);
                    if (!fits)
                    {
                        wrong.Add($"{field.Field} → {kind}: 예외가 나야 한다");
                    }
                }
                catch (ArgumentException ex)
                {
                    if (fits)
                    {
                        wrong.Add($"{field.Field} → {kind}: 생성되어야 한다({ex.Message})");
                    }
                }
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    // 4.6 "`tolerance`(정수 값)": 정수가 아닌 tolerance는 정수 열과 맞지 않는다.
    [Fact]
    public void Fractional_tolerance_does_not_fit_integer_column()
    {
        var definition = SyntheticInput.Case(SyntheticInput.ThreeKindCase.Replace("{field: n, tolerance: 0}", "{field: n, tolerance: 0.5}", StringComparison.Ordinal));

        Assert.Equal(0.5m, definition.Compare[0].Tolerance);
        Assert.Throws<ArgumentException>(() => SyntheticGenerator.Generate(definition, ThreeKindPlan()));
    }

    // 4.13: 칸 수가 머리 행과 다른 행은 ArgumentException.
    [Fact]
    public void Row_cell_count_must_match_header()
    {
        Assert.Throws<ArgumentException>(() => SyntheticCsv.Render(["k", "a", "b"], [new SyntheticRow("k1", ["1"], SyntheticGenerator.PlantedNone)]));
    }

    // 세 종류 열과 심기 다섯 가지를 쓰는 계획(ThreeKindCase에 맞춤). 4.11 G2 고정 계획과 같은 모양이다.
    public static SyntheticPlan ThreeKindPlan() => new(
        Seed: 7,
        FileName: "rows.csv",
        KeyPrefix: "r",
        KeyDigits: 2,
        Rows: 12,
        Columns: [SyntheticColumn.Integer("n", 0, 999), SyntheticColumn.Decimal("d", 10m, 99.99m, 2), SyntheticColumn.Choice("c", "a", "b", "c")],
        MismatchColumns: ["n", "d", "c"],
        Missing: 1,
        Extra: 1,
        WithinColumn: "d",
        Within: 2,
        FocusColumn: null,
        FocusValue: null,
        ShuffleCandidate: true);

    private static SyntheticColumn Column(string name, string kind) => kind switch
    {
        SyntheticColumn.KindInteger => SyntheticColumn.Integer(name, 0, 999),
        SyntheticColumn.KindDecimal => SyntheticColumn.Decimal(name, 10m, 99.99m, 2),
        _ => SyntheticColumn.Choice(name, "a", "b", "c"),
    };

    private static Scorecard.Core.Cases.CaseDefinition TableCompare() => SyntheticInput.CaseOf(Example);

    private static SyntheticPlan Plan() => SyntheticSpec.Plan(Example);

    private static SyntheticTables Generate(SyntheticPlan plan) => SyntheticGenerator.Generate(TableCompare(), plan);

    // 같은 이름의 열을 바꾼다.
    private static SyntheticColumn[] Replace(SyntheticPlan plan, SyntheticColumn column) =>
        [.. plan.Columns.Select(c => c.Name == column.Name ? column : c)];

    private static string Show(string value) =>
        value.Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);

    private sealed record Case(string Label, Action Action, string? Expect);
}
