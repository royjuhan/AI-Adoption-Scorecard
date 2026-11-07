using Scorecard.Core.Cases;
using Scorecard.Core.Synthetic;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Synthetic;

// 스펙 4.2 시드 고르기, 4.3 계획 표, 4.4 열 표, 4.6 case.yaml과 계획 맞추기. 카드 수용 기준 3.
// 표 머리 행·칸 수는 SpecDocument.Table이 본다(머리 행 한 번, 칸 수가 머리 행과 같음).
[Trait("Spec", "synthetic-examples")]
public class PlanTableTests
{
    // 4.6 표 첫 칸(표 순서).
    private static readonly string[] CompareKindLabels = ["`tolerance`(정수 값)", "`tolerance_pct`", "둘 다 없음"];

    // 계획 표의 예제 = 열 표의 예제 = CSV가 있는 예제 = 7.1 파일 표의 CSV 예제.
    [Fact]
    public void Plan_examples_are_the_examples_with_csv()
    {
        Assert.Equal(SyntheticInput.DataExamples, SyntheticSpec.PlanExamples());
        Assert.Equal(
            SyntheticInput.DataExamples,
            SyntheticSpec.Table(SyntheticSpec.ColumnHeader).Select(c => SpecDocument.Unquote(c[0])).Distinct());
        Assert.Equal(
            SyntheticInput.DataExamples,
            SyntheticSpec.Files().Where(f => f.File.EndsWith(".csv", StringComparison.Ordinal)).Select(f => f.Example).Distinct());
        Assert.Equal(
            SyntheticInput.DataExamples.Order(StringComparer.Ordinal),
            Scorecard.Tests.Classification.ClassificationInput.Examples
                .Where(e => Directory.Exists(SyntheticInput.ExamplePath(e, SyntheticCsv.ReferenceFolder)) || Directory.Exists(SyntheticInput.ExamplePath(e, SyntheticCsv.CandidateFolder)))
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            SyntheticInput.NoDataExamples,
            Scorecard.Tests.Classification.ClassificationInput.Examples.Except(SyntheticInput.DataExamples));
    }

    // 7.1 파일 표: CSV는 reference/·candidate/ 아래 계획 표 "파일" 이름 하나씩.
    [Theory]
    [MemberData(nameof(SyntheticInput.DataExampleNames), MemberType = typeof(SyntheticInput))]
    public void File_table_lists_plan_csv_in_both_folders(string example)
    {
        var plan = SyntheticSpec.Plan(example);

        Assert.Equal(
            [$"{SyntheticCsv.ReferenceFolder}/{plan.FileName}", $"{SyntheticCsv.CandidateFolder}/{plan.FileName}"],
            SyntheticSpec.FilesOf(example).Where(f => f.EndsWith(".csv", StringComparison.Ordinal)));
    }

    // 4.3 칸 읽기: 파일 이름은 폴더·':' 없이 .csv로 끝나고, case.yaml inputs(reference/*.csv, candidate/*.csv)에 맞는다.
    // 키 자릿수는 기준+추가 건수를 쓸 수 있다.
    [Theory]
    [MemberData(nameof(SyntheticInput.DataExampleNames), MemberType = typeof(SyntheticInput))]
    public void Plan_cells_fit_case_inputs(string example)
    {
        var plan = SyntheticSpec.Plan(example);
        var inputs = SyntheticInput.CaseOf(example).Inputs;

        Assert.NotNull(inputs);
        Assert.True(plan.FileName.IndexOfAny(['/', '\\', ':']) < 0 && plan.FileName.EndsWith(".csv", StringComparison.Ordinal), plan.FileName);
        Assert.Equal($"{SyntheticCsv.ReferenceFolder}/*.csv", inputs.Reference);
        Assert.Equal($"{SyntheticCsv.CandidateFolder}/*.csv", inputs.Candidate);
        Assert.True((plan.Rows + plan.Extra).ToString(System.Globalization.CultureInfo.InvariantCulture).Length <= plan.KeyDigits);
        Assert.NotEmpty(plan.Columns);
        Assert.DoesNotContain(inputs.Key, plan.Columns.Select(c => c.Name));
    }

    // 4.6 표: compare 항목(tolerance 정수 값, tolerance_pct, 둘 다 없음)마다 열 종류(정수, 소수, 선택)가 정해진다.
    // compare 필드는 모두 계획 열에 있다. 계획 열은 compare에 없어도 된다.
    [Theory]
    [MemberData(nameof(SyntheticInput.DataExampleNames), MemberType = typeof(SyntheticInput))]
    public void Compare_fields_have_column_kind_from_table(string example)
    {
        var plan = SyntheticSpec.Plan(example);
        var kinds = CompareKinds();

        Assert.All(SyntheticInput.CaseOf(example).Compare, field =>
        {
            var column = Assert.Single(plan.Columns, c => c.Name == field.Field);
            Assert.Equal(kinds[CompareLabel(field)], column.Kind);
        });
    }

    // 4.3·4.6: 값 불일치 열은 compare 필드, 허용오차 안 차이 열은 tolerance_pct 필드, 몰림 값은 그 열의 선택지.
    [Theory]
    [MemberData(nameof(SyntheticInput.DataExampleNames), MemberType = typeof(SyntheticInput))]
    public void Planted_columns_fit_compare(string example)
    {
        var plan = SyntheticSpec.Plan(example);
        var compare = SyntheticInput.CaseOf(example).Compare.ToDictionary(f => f.Field, StringComparer.Ordinal);

        Assert.All(plan.MismatchColumns, c => Assert.True(compare.ContainsKey(c), $"값 불일치 열 {c}가 compare 필드가 아니다."));
        Assert.Equal(plan.MismatchColumns.Count, plan.MismatchColumns.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(plan.Within > 0, plan.WithinColumn is not null);
        if (plan.WithinColumn is not null)
        {
            Assert.NotNull(compare[plan.WithinColumn].TolerancePct);
        }

        Assert.Equal(plan.FocusColumn is null, plan.FocusValue is null);
        if (plan.FocusColumn is not null)
        {
            Assert.Contains(plan.FocusValue, Assert.Single(plan.Columns, c => c.Name == plan.FocusColumn).Choices);
        }
    }

    // 4.4: 정수·소수 열은 최소 ≤ 최대, 소수 자릿수는 최소값의 자릿수, 선택 열은 선택지가 둘 이상이고 겹치지 않는다.
    [Theory]
    [MemberData(nameof(SyntheticInput.DataExampleNames), MemberType = typeof(SyntheticInput))]
    public void Column_table_values_are_well_formed(string example)
    {
        Assert.All(SyntheticSpec.Columns(example), c =>
        {
            if (c.Kind == SyntheticColumn.KindChoice)
            {
                Assert.True(c.Choices.Count >= 2, c.Name);
                Assert.Equal(c.Choices.Count, c.Choices.Distinct(StringComparer.Ordinal).Count());
                Assert.All(c.Choices, SyntheticCsv.CheckCell);
            }
            else
            {
                Assert.True(c.Min <= c.Max, c.Name);
            }
        });
        Assert.Equal(2, Assert.Single(SyntheticSpec.Columns("table-compare"), c => c.Name == "mean_value").Scale);
    }

    // 4.2 시드 고르기(table-compare): 기준 30행 중 irregular가 answers.yaml 2단계 exception_rate(40%)만큼(12행)인 가장 작은 시드.
    [Fact]
    public void Table_compare_seed_is_smallest_with_focus_share()
    {
        const string example = "table-compare";
        var plan = SyntheticSpec.Plan(example);
        var definition = SyntheticInput.CaseOf(example);
        var step = Assert.Single(definition.Steps, s => s.Number == 2);
        Assert.Contains("불규칙", step.Name, StringComparison.Ordinal);
        var share = step.Answers.ExceptionRate.Value;
        Assert.False(step.Answers.ExceptionRate.IsDefault);
        var target = plan.Rows * share / 100m;
        Assert.Equal(decimal.Truncate(target), target);

        var focus = SyntheticInput.IndexOf(SyntheticInput.Generate(example).Header, plan.FocusColumn!) - 1;
        int FocusRows(ulong seed) =>
            SyntheticGenerator.Generate(definition, plan with { Seed = seed }).Reference.Count(r => r.Values[focus] == plan.FocusValue);

        Assert.Equal((int)target, FocusRows(plan.Seed));
        for (ulong seed = 0; seed < plan.Seed; seed++)
        {
            Assert.NotEqual((int)target, FocusRows(seed));
        }
    }

    // 4.2 시드 고르기(form-fill): 선택지가 기준 자료에 모두 나오는 가장 작은 시드.
    [Fact]
    public void Form_fill_seed_is_smallest_with_all_choices()
    {
        const string example = "form-fill";
        var plan = SyntheticSpec.Plan(example);
        var definition = SyntheticInput.CaseOf(example);

        bool AllChoices(ulong seed)
        {
            var tables = SyntheticGenerator.Generate(definition, plan with { Seed = seed });
            return plan.Columns.Select((c, i) => (c, i))
                .Where(x => x.c.Kind == SyntheticColumn.KindChoice)
                .All(x => x.c.Choices.All(v => tables.Reference.Any(r => r.Values[x.i] == v)));
        }

        Assert.True(AllChoices(plan.Seed));
        for (ulong seed = 0; seed < plan.Seed; seed++)
        {
            Assert.False(AllChoices(seed));
        }
    }

    // 4.6 표를 읽는다. 첫 칸이 CompareKindLabels와 같아야 한다. 라벨 → SyntheticColumn.Kind.
    public static Dictionary<string, string> CompareKinds()
    {
        var rows = SyntheticSpec.Table(SyntheticSpec.CompareKindHeader);
        Assert.Equal(CompareKindLabels, rows.Select(c => c[0]));
        return rows.ToDictionary(c => c[0], c => SyntheticSpec.KindOf(c[1].Trim()), StringComparer.Ordinal);
    }

    // compare 필드 하나의 4.6 표 라벨.
    public static string CompareLabel(CompareField field) =>
        field.Tolerance is not null ? CompareKindLabels[0] : field.TolerancePct is not null ? CompareKindLabels[1] : CompareKindLabels[2];
}
