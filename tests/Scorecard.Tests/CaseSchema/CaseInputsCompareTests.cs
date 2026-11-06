using Scorecard.Core.Cases;

namespace Scorecard.Tests.CaseSchema;

// 스펙 4.5 inputs, compare, thresholds. 승인 결정 10·11.
[Trait("Spec", "case-schema")]
public class CaseInputsCompareTests
{
    private const string PathMessage = "케이스 폴더 기준 상대 경로를 '/'로 쓴다 ('..', ':', '\\' 없음)";

    private const string Inputs = "inputs:\n  reference: r/*.csv\n  candidate: c/*.csv\n  key: id\n";

    // inputs의 reference 줄이 6줄째, 값은 14열이다.
    private static string WithReference(string reference) =>
        CaseInput.OneStep + $"inputs:\n  reference: {reference}\n  candidate: c/*.csv\n  key: id\n";

    // glob 문자는 그대로 저장한다. 해석은 T-006.
    [Theory]
    [InlineData("reference/*.csv")]
    [InlineData("a/b/c.csv")]
    [InlineData("x")]
    [InlineData("..a/b.csv")]
    [InlineData("a..b/c.csv")]
    [InlineData("./x.csv")]
    [InlineData("한글/자료.csv")]
    public void Relative_path_is_stored_as_written(string reference)
    {
        Assert.Equal(new CaseInputs(reference, "c/*.csv", "id"), CaseInput.Valid(WithReference(reference)).Inputs);
    }

    // '/'로 시작하거나, '\'·':'가 있거나, '..' 조각이 있으면 format.
    [Theory]
    [InlineData("/data/ref.csv")]
    [InlineData("../candidate/*.csv")]
    [InlineData("a/../b.csv")]
    [InlineData("a/..")]
    [InlineData("..")]
    [InlineData("a\\b.csv")]
    [InlineData("C:/data/ref.csv")]
    [InlineData("a:b")]
    public void Path_outside_case_folder_is_format_error(string reference)
    {
        CaseInput.AssertErrors(WithReference(reference), $"case.yaml:6:14: format inputs.reference: {PathMessage}");
    }

    [Fact]
    public void Candidate_path_follows_same_rule()
    {
        CaseInput.AssertErrors(
            CaseInput.OneStep + "inputs:\n  reference: r/*.csv\n  candidate: /c.csv\n  key: id\n",
            $"case.yaml:7:14: format inputs.candidate: {PathMessage}");
    }

    // inputs가 있으면 세 키 모두 필수. 없으면 위치는 inputs 매핑 시작, 표 순서대로.
    [Fact]
    public void Inputs_requires_all_three_keys()
    {
        CaseInput.AssertErrors(
            CaseInput.OneStep + "inputs: {other: x}\n",
            "case.yaml:5:9: required inputs.reference: 필수 필드가 없다",
            "case.yaml:5:9: required inputs.candidate: 필수 필드가 없다",
            "case.yaml:5:9: required inputs.key: 필수 필드가 없다",
            "case.yaml:5:10: unknown-field inputs.other: 알 수 없는 필드다");
    }

    [Fact]
    public void Inputs_key_must_not_be_empty()
    {
        CaseInput.AssertErrors(
            CaseInput.OneStep + "inputs:\n  reference: r/*.csv\n  candidate: c/*.csv\n  key: \"\"\n",
            "case.yaml:8:8: required inputs.key: 값이 비어 있다");
    }

    [Fact]
    public void Inputs_must_be_mapping()
    {
        CaseInput.AssertErrors(CaseInput.OneStep + "inputs: [r.csv]\n", "case.yaml:5:9: type inputs: 매핑이어야 한다");
    }

    // inputs가 없으면 null. compare가 없으면 빈 목록(null 아님).
    [Theory]
    [InlineData("")]
    [InlineData("inputs: ~\ncompare: ~\n")]
    [InlineData("compare: []\n")]
    public void Absent_inputs_and_compare_are_null_and_empty(string extra)
    {
        var c = CaseInput.Valid(CaseInput.OneStep + extra);

        Assert.Null(c.Inputs);
        Assert.NotNull(c.Compare);
        Assert.Empty(c.Compare);
    }

    // compare 항목: field 필수, tolerance·tolerance_pct는 선택. 둘 다 없으면 허용한다.
    [Fact]
    public void Compare_reads_fields_and_tolerances()
    {
        var c = CaseInput.Valid(
            CaseInput.OneStep + Inputs
            + "compare:\n  - {field: a, tolerance: 0}\n  - {field: b, tolerance_pct: 2.50}\n  - {field: c}\n  - field: d\n    tolerance: 0.001\n");

        Assert.Equal(
            [
                new CompareField("a", 0m, null),
                new CompareField("b", null, 2.50m),
                new CompareField("c", null, null),
                new CompareField("d", 0.001m, null),
            ],
            c.Compare);
        Assert.Equal("2.50", c.Compare[1].TolerancePct?.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    // 비교 필드 이름은 서수 비교로 중복 금지. 대소문자가 다르면 다른 이름이다.
    [Fact]
    public void Compare_field_names_compare_ordinally()
    {
        var c = CaseInput.Valid(CaseInput.OneStep + Inputs + "compare:\n  - {field: x}\n  - {field: X}\n");

        Assert.Equal(["x", "X"], c.Compare.Select(f => f.Field));
    }

    [Theory]
    [InlineData("  - {tolerance: 1}\n", "case.yaml:10:5: required compare[1].field: 필수 필드가 없다")]
    [InlineData("  - {field: x, tolerance: -1}\n", "case.yaml:10:27: range compare[1].tolerance: 범위 밖이다 (0 이상): -1")]
    [InlineData("  - {field: x, tolerance_pct: -0.5}\n", "case.yaml:10:31: range compare[1].tolerance_pct: 범위 밖이다 (0 이상): -0.5")]
    [InlineData("  - {field: x, tolerance: abc}\n", "case.yaml:10:27: format compare[1].tolerance: 숫자가 아니다: abc")]
    [InlineData("  - {field: x, tolerance: \"1\"}\n", "case.yaml:10:27: format compare[1].tolerance: 숫자가 아니다: 1 (따옴표 없이 쓴다)")]
    [InlineData("  - {field: x, tolerance: 1, tolerance_pct: 2}\n", "case.yaml:10:5: conflict compare[1]: tolerance와 tolerance_pct 중 하나만 쓴다")]
    [InlineData("  - x\n", "case.yaml:10:5: type compare[1]: 매핑이어야 한다")]
    public void Compare_item_errors(string item, string expected)
    {
        CaseInput.AssertErrors(CaseInput.OneStep + Inputs + "compare:\n" + item, expected);
    }

    [Fact]
    public void Compare_must_be_list()
    {
        CaseInput.AssertErrors(CaseInput.OneStep + Inputs + "compare:\n  field: x\n", "case.yaml:10:3: type compare: 목록이어야 한다");
    }

    // compare에 항목이 있는데 inputs 키가 없으면 required inputs. 위치는 compare 값.
    [Fact]
    public void Compare_without_inputs_requires_inputs()
    {
        CaseInput.AssertErrors(
            CaseInput.OneStep + "compare:\n  - {field: x}\n",
            "case.yaml:6:3: required inputs: compare가 있으면 inputs가 있어야 한다");
    }

    // thresholds: 읽기만. 없으면 세 값 null(기본값은 판정 카드에서 정한다, 승인 결정 11).
    [Theory]
    [InlineData("")]
    [InlineData("thresholds:\n")]
    [InlineData("thresholds: ~\n")]
    [InlineData("thresholds: {}\n")]
    [InlineData("thresholds: {accuracy_min: ~, missing_max: null, time_must_drop: ~}\n")]
    public void Absent_thresholds_are_null(string extra)
    {
        Assert.Equal(new CaseThresholds(null, null, null), CaseInput.Valid(CaseInput.OneStep + extra).Thresholds);
    }

    [Fact]
    public void Thresholds_read_written_values()
    {
        var c = CaseInput.Valid(CaseInput.OneStep + "thresholds:\n  accuracy_min: 0\n  missing_max: 100\n  time_must_drop: false\n");

        Assert.Equal(new CaseThresholds(0m, 100m, false), c.Thresholds);
    }

    [Theory]
    [InlineData("accuracy_min: 101", "case.yaml:6:17: range thresholds.accuracy_min: 범위 밖이다 (0 이상 100 이하): 101")]
    [InlineData("accuracy_min: -1", "case.yaml:6:17: range thresholds.accuracy_min: 범위 밖이다 (0 이상 100 이하): -1")]
    [InlineData("missing_max: 100.5", "case.yaml:6:16: range thresholds.missing_max: 범위 밖이다 (0 이상 100 이하): 100.5")]
    [InlineData("missing_max: \"5\"", "case.yaml:6:16: format thresholds.missing_max: 숫자가 아니다: 5 (따옴표 없이 쓴다)")]
    public void Threshold_number_errors(string line, string expected)
    {
        CaseInput.AssertErrors(CaseInput.OneStep + "thresholds:\n  " + line + "\n", expected);
    }

    [Fact]
    public void Thresholds_must_be_mapping()
    {
        CaseInput.AssertErrors(CaseInput.OneStep + "thresholds: 90\n", "case.yaml:5:13: type thresholds: 매핑이어야 한다");
    }
}
