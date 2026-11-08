using Scorecard.Core.Cases;
using Scorecard.Core.Synthetic;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Synthetic;

// 스펙 4.13 Core 공개 API(승인 결정 14)와 5절 null 예외.
// 이 파일은 컴파일 자체가 API 모양 검사다. 타입·이름·매개변수 순서가 다르면 컴파일되지 않는다.
// test-engineer 단계에서는 Core에 Scorecard.Core.Synthetic이 없어 컴파일이 실패한다(승인 결정 14).
[Trait("Spec", "synthetic-examples")]
public class SyntheticApiShapeTests
{
    private const string SyntheticNamespace = "Scorecard.Core.Synthetic";

    // 4.13의 공개 타입 전부. SplitMix64는 internal이다.
    private static readonly string[] PublicTypes =
    [
        // T-014(text-check.md)
        "SyntheticColumn",
        "SyntheticCsv",
        "SyntheticGenerator",
        "SyntheticPlan",
        "SyntheticReplies",
        "SyntheticReplyFiles",
        "SyntheticReplyPlan",
        "SyntheticRow",
        "SyntheticTables",
    ];

    [Fact]
    public void Constants_have_spec_values()
    {
        const string none = SyntheticGenerator.PlantedNone;
        const string missing = SyntheticGenerator.PlantedMissing;
        const string extra = SyntheticGenerator.PlantedExtra;
        const string mismatch = SyntheticGenerator.PlantedMismatchPrefix;
        const string within = SyntheticGenerator.PlantedWithinPrefix;
        const string reference = SyntheticCsv.ReferenceFolder;
        const string candidate = SyntheticCsv.CandidateFolder;
        const string integer = SyntheticColumn.KindInteger;
        const string @decimal = SyntheticColumn.KindDecimal;
        const string choice = SyntheticColumn.KindChoice;

        Assert.Equal(["none", "missing", "extra", "mismatch:", "within:"], new[] { none, missing, extra, mismatch, within });
        Assert.Equal(["reference", "candidate"], new[] { reference, candidate });
        Assert.Equal(["integer", "decimal", "choice"], new[] { integer, @decimal, choice });
    }

    // 3.1 Planted 표의 값과 상수가 같다. "<열>"은 열 이름 자리다.
    [Fact]
    public void Planted_constants_match_spec_table()
    {
        var values = SyntheticSpec.Table(SyntheticSpec.PlantedHeader).Select(c => SpecDocument.Unquote(c[0])).ToArray();

        Assert.Equal(
            [
                SyntheticGenerator.PlantedNone,
                SyntheticGenerator.PlantedMissing,
                SyntheticGenerator.PlantedMismatchPrefix + "<열>",
                SyntheticGenerator.PlantedWithinPrefix + "<열>",
                SyntheticGenerator.PlantedExtra,
            ],
            values);
    }

    // 공개 메서드 세 개의 서명.
    [Fact]
    public void Entry_points_have_spec_signatures()
    {
        Func<CaseDefinition, SyntheticPlan, SyntheticTables> generate = SyntheticGenerator.Generate;
        Func<IReadOnlyList<string>, IReadOnlyList<SyntheticRow>, byte[]> render = SyntheticCsv.Render;
        Action<string> checkCell = SyntheticCsv.CheckCell;
        Func<string, int, int, SyntheticColumn> integer = SyntheticColumn.Integer;
        Func<string, decimal, decimal, int, SyntheticColumn> @decimal = SyntheticColumn.Decimal;
        Func<string, string[], SyntheticColumn> choice = SyntheticColumn.Choice;

        Assert.NotNull(generate);
        Assert.NotNull(render);
        Assert.NotNull(checkCell);
        Assert.NotNull(integer);
        Assert.NotNull(@decimal);
        Assert.NotNull(choice);
    }

    // 레코드 다섯 개의 위치 매개변수 이름·순서·형식.
    [Fact]
    public void Records_have_spec_shape()
    {
        var column = new SyntheticColumn(Name: "v", Kind: SyntheticColumn.KindInteger, Min: 0m, Max: 9m, Scale: 0, Choices: []);
        var plan = new SyntheticPlan(
            Seed: 1UL,
            FileName: "x.csv",
            KeyPrefix: "k",
            KeyDigits: 1,
            Rows: 1,
            Columns: [column],
            MismatchColumns: [],
            Missing: 0,
            Extra: 0,
            WithinColumn: null,
            Within: 0,
            FocusColumn: null,
            FocusValue: null,
            ShuffleCandidate: false);
        var row = new SyntheticRow(Key: "k1", Values: ["3"], Planted: SyntheticGenerator.PlantedNone);
        var tables = new SyntheticTables(FileName: "x.csv", Header: ["id", "v"], Reference: [row], Candidate: [row]);

        Assert.Same(column, Assert.Single(plan.Columns));
        Assert.Same(row, Assert.Single(tables.Reference));
        Assert.Equal(new SyntheticRow("k1", row.Values, "none"), row);
        Assert.Equal(1UL, plan.Seed);
        Assert.Equal("x.csv", tables.FileName);
    }

    // 열 만들기 도우미 세 개.
    [Fact]
    public void Column_factories_fill_kind_range_and_choices()
    {
        var integer = SyntheticColumn.Integer("n", 50, 500);
        var @decimal = SyntheticColumn.Decimal("d", 10.00m, 99.99m, 2);
        var choice = SyntheticColumn.Choice("c", "normal", "irregular");

        Assert.Equal(("n", SyntheticColumn.KindInteger, 50m, 500m), (integer.Name, integer.Kind, integer.Min, integer.Max));
        Assert.Equal(("d", SyntheticColumn.KindDecimal, 10.00m, 99.99m, 2), (@decimal.Name, @decimal.Kind, @decimal.Min, @decimal.Max, @decimal.Scale));
        Assert.Equal(("c", SyntheticColumn.KindChoice), (choice.Name, choice.Kind));
        Assert.Equal(["normal", "irregular"], choice.Choices);
    }

    [Fact]
    public void Synthetic_namespace_exports_only_spec_types()
    {
        var exported = typeof(SyntheticGenerator).Assembly.GetExportedTypes()
            .Where(t => t.Namespace == SyntheticNamespace)
            .Select(t => t.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(PublicTypes, exported);
    }

    // 4.13: SplitMix64는 internal이다. Core에 InternalsVisibleTo가 없다(테스트는 공개 API로 수열을 본다).
    [Fact]
    public void Sequence_type_is_internal()
    {
        var type = typeof(SyntheticGenerator).Assembly.GetType($"{SyntheticNamespace}.SplitMix64");

        Assert.NotNull(type);
        Assert.False(type.IsPublic);
        Assert.Empty(typeof(SyntheticGenerator).Assembly.GetCustomAttributes(typeof(System.Runtime.CompilerServices.InternalsVisibleToAttribute), inherit: false));
    }

    // 5절: definition·plan이 null이면 ArgumentNullException.
    [Fact]
    public void Generate_null_arguments_throw_argument_null()
    {
        var definition = SyntheticInput.CaseOf("table-compare");
        var plan = SyntheticSpec.Plan("table-compare");

        Assert.Throws<ArgumentNullException>(() => SyntheticGenerator.Generate(null!, plan));
        Assert.Throws<ArgumentNullException>(() => SyntheticGenerator.Generate(definition, null!));
    }

    // 4.13 "규칙에 맞지 않는 값은 ArgumentException". null도 규칙 밖이다(ArgumentNullException도 ArgumentException이다).
    [Fact]
    public void Csv_null_arguments_throw_argument_exception()
    {
        Assert.ThrowsAny<ArgumentException>(() => SyntheticCsv.Render(null!, []));
        Assert.ThrowsAny<ArgumentException>(() => SyntheticCsv.Render(["k"], null!));
        Assert.ThrowsAny<ArgumentException>(() => SyntheticCsv.CheckCell(null!));
    }

    // 4.13: 순수 함수다. 입력 모델·계획을 바꾸지 않는다.
    [Fact]
    public void Generate_does_not_change_inputs()
    {
        var definition = SyntheticInput.CaseOf("table-compare");
        var plan = SyntheticSpec.Plan("table-compare");
        var (compare, columns, mismatch) = (definition.Compare.ToArray(), plan.Columns.ToArray(), plan.MismatchColumns.ToArray());
        var choices = plan.Columns.Select(c => c.Choices.ToArray()).ToArray();

        SyntheticGenerator.Generate(definition, plan);

        Assert.Equal(compare, definition.Compare);
        Assert.Equal(columns, plan.Columns);
        Assert.Equal(mismatch, plan.MismatchColumns);
        Assert.Equal(choices, plan.Columns.Select(c => c.Choices.ToArray()));
    }
}
