using System.Globalization;

namespace Scorecard.Tests.CaseSchema;

// 스펙 4.6 값 형식: 정수·숫자·불리언·null·이름. 받는 값과 받지 않는 값.
// 정수·숫자·불리언은 따옴표 없이 쓴다. InvariantCulture로 읽는다.
[Trait("Spec", "case-schema")]
public class CaseValueFormatTests
{
    private const string BatchSize = "case.yaml:6:19: ";
    private const string ExceptionRate = "case.yaml:6:23: ";
    private const string TimeMustDrop = "case.yaml:6:19: ";

    // 정수 batch_size: 따옴표 없는 ^-?(0|[1-9][0-9]*)$, 범위 1 이상(int 최댓값 이하).
    [Theory]
    [InlineData("10", 10)]
    [InlineData("1", 1)]
    [InlineData("2147483647", 2147483647)]
    public void Integer_accepts_plain_decimal_digits(string written, int expected)
    {
        var answers = CaseInput.Valid(CaseInput.OneStepWithAnswers + $"      batch_size: {written}\n").Steps[0].Answers;

        Assert.Equal(expected, answers.BatchSize.Value);
        Assert.False(answers.BatchSize.IsDefault);
    }

    [Theory]
    [InlineData("010", "format steps[1].answers.batch_size: 정수가 아니다: 010")]
    [InlineData("+1", "format steps[1].answers.batch_size: 정수가 아니다: +1")]
    [InlineData("1e3", "format steps[1].answers.batch_size: 정수가 아니다: 1e3")]
    [InlineData(".5", "format steps[1].answers.batch_size: 정수가 아니다: .5")]
    [InlineData("1.", "format steps[1].answers.batch_size: 정수가 아니다: 1.")]
    [InlineData("1.50", "format steps[1].answers.batch_size: 정수가 아니다: 1.50")]
    [InlineData("0x10", "format steps[1].answers.batch_size: 정수가 아니다: 0x10")]
    [InlineData(".inf", "format steps[1].answers.batch_size: 정수가 아니다: .inf")]
    [InlineData("1_000", "format steps[1].answers.batch_size: 정수가 아니다: 1_000")]
    [InlineData("ten", "format steps[1].answers.batch_size: 정수가 아니다: ten")]
    [InlineData("\"10\"", "format steps[1].answers.batch_size: 정수가 아니다: 10 (따옴표 없이 쓴다)")]
    [InlineData("'10'", "format steps[1].answers.batch_size: 정수가 아니다: 10 (따옴표 없이 쓴다)")]
    [InlineData("-0", "range steps[1].answers.batch_size: 범위 밖이다 (1 이상 2147483647 이하): -0")]
    [InlineData("0", "range steps[1].answers.batch_size: 범위 밖이다 (1 이상 2147483647 이하): 0")]
    [InlineData("-1", "range steps[1].answers.batch_size: 범위 밖이다 (1 이상 2147483647 이하): -1")]
    [InlineData("2147483648", "range steps[1].answers.batch_size: 범위 밖이다 (1 이상 2147483647 이하): 2147483648")]
    [InlineData("99999999999999999999", "range steps[1].answers.batch_size: 범위 밖이다 (1 이상 2147483647 이하): 99999999999999999999")]
    public void Integer_rejects_other_forms(string written, string expected)
    {
        CaseInput.AssertErrors(CaseInput.OneStepWithAnswers + $"      batch_size: {written}\n", BatchSize + expected);
    }

    // 숫자 exception_rate: 따옴표 없는 ^-?(0|[1-9][0-9]*)(\.[0-9]+)?$, decimal, 범위 0 이상 100 이하.
    // decimal은 소수 자릿수를 보존한다. 1.50은 "1.50", 1.5는 "1.5"로 남는다.
    [Theory]
    [InlineData("10", "10")]
    [InlineData("0", "0")]
    [InlineData("100", "100")]
    [InlineData("62.5", "62.5")]
    [InlineData("1.50", "1.50")]
    [InlineData("1.5", "1.5")]
    [InlineData("0.25", "0.25")]
    public void Number_accepts_plain_decimal_and_keeps_scale(string written, string expected)
    {
        var answers = CaseInput.Valid(CaseInput.OneStepWithAnswers + $"      exception_rate: {written}\n").Steps[0].Answers;

        Assert.Equal(expected, answers.ExceptionRate.Value.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), answers.ExceptionRate.Value);
        Assert.False(answers.ExceptionRate.IsDefault);
    }

    // -0은 모양이 맞고 값이 0이라 받는다.
    [Fact]
    public void Number_accepts_negative_zero_as_zero()
    {
        var answers = CaseInput.Valid(CaseInput.OneStepWithAnswers + "      exception_rate: -0\n").Steps[0].Answers;

        Assert.Equal(0m, answers.ExceptionRate.Value);
    }

    [Theory]
    [InlineData("1,5", "format steps[1].answers.exception_rate: 숫자가 아니다: 1,5")]
    [InlineData("010", "format steps[1].answers.exception_rate: 숫자가 아니다: 010")]
    [InlineData("+1", "format steps[1].answers.exception_rate: 숫자가 아니다: +1")]
    [InlineData("1e3", "format steps[1].answers.exception_rate: 숫자가 아니다: 1e3")]
    [InlineData(".5", "format steps[1].answers.exception_rate: 숫자가 아니다: .5")]
    [InlineData("1.", "format steps[1].answers.exception_rate: 숫자가 아니다: 1.")]
    [InlineData("0x10", "format steps[1].answers.exception_rate: 숫자가 아니다: 0x10")]
    [InlineData(".inf", "format steps[1].answers.exception_rate: 숫자가 아니다: .inf")]
    [InlineData(".nan", "format steps[1].answers.exception_rate: 숫자가 아니다: .nan")]
    [InlineData("1_000", "format steps[1].answers.exception_rate: 숫자가 아니다: 1_000")]
    [InlineData("\"10\"", "format steps[1].answers.exception_rate: 숫자가 아니다: 10 (따옴표 없이 쓴다)")]
    [InlineData("'10'", "format steps[1].answers.exception_rate: 숫자가 아니다: 10 (따옴표 없이 쓴다)")]
    [InlineData("120", "range steps[1].answers.exception_rate: 범위 밖이다 (0 이상 100 이하): 120")]
    [InlineData("100.01", "range steps[1].answers.exception_rate: 범위 밖이다 (0 이상 100 이하): 100.01")]
    [InlineData("-1", "range steps[1].answers.exception_rate: 범위 밖이다 (0 이상 100 이하): -1")]
    [InlineData("-0.5", "range steps[1].answers.exception_rate: 범위 밖이다 (0 이상 100 이하): -0.5")]
    [InlineData("2147483648", "range steps[1].answers.exception_rate: 범위 밖이다 (0 이상 100 이하): 2147483648")]
    public void Number_rejects_other_forms(string written, string expected)
    {
        CaseInput.AssertErrors(CaseInput.OneStepWithAnswers + $"      exception_rate: {written}\n", ExceptionRate + expected);
    }

    // minutes_per_item: 숫자, 0 이상, 위 한도 없음. 없거나 null이면 null(측정 권고 근거).
    [Theory]
    [InlineData("0", "0")]
    [InlineData("7.5", "7.5")]
    [InlineData("100000", "100000")]
    public void Minutes_per_item_accepts_zero_or_more(string written, string expected)
    {
        var minutes = CaseInput.Valid(CaseInput.OneStepWithAnswers + $"      minutes_per_item: {written}\n").Steps[0].Answers.MinutesPerItem;

        Assert.Equal(expected, minutes.Value?.ToString(CultureInfo.InvariantCulture));
        Assert.False(minutes.IsDefault);
    }

    [Fact]
    public void Minutes_per_item_rejects_negative()
    {
        CaseInput.AssertErrors(
            CaseInput.OneStepWithAnswers + "      minutes_per_item: -1\n",
            "case.yaml:6:25: range steps[1].answers.minutes_per_item: 범위 밖이다 (0 이상): -1");
    }

    // 불리언 time_must_drop: 따옴표 없는 true·false만.
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void Boolean_accepts_lowercase_true_false(string written, bool expected)
    {
        var c = CaseInput.Valid(CaseInput.OneStep + $"thresholds:\n  time_must_drop: {written}\n");

        Assert.Equal(expected, c.Thresholds.TimeMustDrop);
    }

    [Theory]
    [InlineData("True", "True")]
    [InlineData("TRUE", "TRUE")]
    [InlineData("False", "False")]
    [InlineData("yes", "yes")]
    [InlineData("no", "no")]
    [InlineData("on", "on")]
    [InlineData("1", "1")]
    [InlineData("\"true\"", "true")]
    [InlineData("'false'", "false")]
    public void Boolean_rejects_other_forms(string written, string shown)
    {
        CaseInput.AssertErrors(
            CaseInput.OneStep + $"thresholds:\n  time_must_drop: {written}\n",
            TimeMustDrop + $"format thresholds.time_must_drop: true 또는 false로 쓴다: {shown}");
    }

    [Theory]
    [InlineData("time_must_drop: [true]", "case.yaml:6:19: type thresholds.time_must_drop: 스칼라 값이어야 한다")]
    [InlineData("accuracy_min: {a: 1}", "case.yaml:6:17: type thresholds.accuracy_min: 스칼라 값이어야 한다")]
    public void Scalar_field_rejects_collection(string line, string expected)
    {
        CaseInput.AssertErrors(CaseInput.OneStep + "thresholds:\n  " + line + "\n", expected);
    }

    // 케이스 이름: ^[a-z0-9]+(-[a-z0-9]+)*$, 64자 이하. 따옴표 여부는 상관없다.
    [Theory]
    [InlineData("a")]
    [InlineData("a-1")]
    [InlineData("123")]
    [InlineData("sample-full-2")]
    [InlineData("\"quoted-name\"")]
    public void Case_name_accepts_lowercase_digits_and_single_hyphens(string written)
    {
        var c = CaseInput.Valid($"name: {written}\ntask: t\nsteps:\n  - name: s\n");

        Assert.Equal(written.Trim('"'), c.Name);
    }

    [Fact]
    public void Case_name_accepts_sixty_four_characters()
    {
        var name = new string('a', 64);

        Assert.Equal(name, CaseInput.Valid($"name: {name}\ntask: t\nsteps:\n  - name: s\n").Name);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("Sample")]
    [InlineData("a_b")]
    [InlineData("a--b")]
    [InlineData("-a")]
    [InlineData("a-")]
    [InlineData("a b")]
    [InlineData("가")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Case_name_rejects_other_forms(string written)
    {
        CaseInput.AssertErrors(
            $"name: {written}\ntask: t\nsteps:\n  - name: s\n",
            "case.yaml:1:7: format name: 소문자·숫자·'-'만, 64자까지 쓴다");
    }
}
