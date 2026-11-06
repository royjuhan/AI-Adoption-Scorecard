using System.Globalization;
using Scorecard.Core.Cases;

namespace Scorecard.Tests.CaseSchema;

// 스펙 4.4 단계와 질문 9개, 기본값 규칙. 승인 결정 5.
// 기본값은 코드에 박지 않고 스펙 4.4 표를 파일에서 읽어 대조한다(지시문 13절).
[Trait("Spec", "case-schema")]
public class CaseDefaultsTests
{
    // 4.4 표 행마다: 키, 선택지·범위, 기본값. Theory 행이 표 행과 같은지는 아래 테스트가 본다.
    public static TheoryData<string> DefaultKeys => new(DefaultRow.ReadAll().Select(r => r.Key));

    public static TheoryData<string> EnumKeys => new(DefaultRow.ReadAll().Where(r => r.IsEnum).Select(r => r.Key));

    // 답 9개를 4.4 표 순서로 (키, 값 문자열, IsDefault)로 편다. 값은 InvariantCulture, null은 "null".
    internal static IReadOnlyList<(string Key, string Value, bool IsDefault)> AllAnswers(StepAnswers a) =>
    [
        ("input_kind", a.InputKind.Value, a.InputKind.IsDefault),
        ("rule_expressible", a.RuleExpressible.Value, a.RuleExpressible.IsDefault),
        ("frequency", a.Frequency.Value, a.Frequency.IsDefault),
        ("batch_size", a.BatchSize.Value.ToString(CultureInfo.InvariantCulture), a.BatchSize.IsDefault),
        ("exception_rate", a.ExceptionRate.Value.ToString(CultureInfo.InvariantCulture), a.ExceptionRate.IsDefault),
        ("current_actor", a.CurrentActor.Value, a.CurrentActor.IsDefault),
        ("minutes_per_item", a.MinutesPerItem.Value?.ToString(CultureInfo.InvariantCulture) ?? "null", a.MinutesPerItem.IsDefault),
        ("failure_impact", a.FailureImpact.Value, a.FailureImpact.IsDefault),
        ("existing_feature", a.ExistingFeature.Value, a.ExistingFeature.IsDefault),
    ];

    // answers가 없는 단계의 9개 값이 4.4 표 다섯째 칸과 같고 모두 IsDefault다.
    [Fact]
    public void Step_without_answers_matches_spec_defaults_table()
    {
        var table = DefaultRow.ReadAll();
        var answers = AllAnswers(CaseInput.Valid(CaseInput.OneStep).Steps[0].Answers);

        Assert.Equal(table.Select(r => (r.Key, r.Default, true)), answers);
    }

    // 4.4: answers가 null이면 없을 때와 같다.
    [Theory]
    [InlineData("    answers:\n")]
    [InlineData("    answers: ~\n")]
    [InlineData("    answers: null\n")]
    [InlineData("    answers: {}\n")]
    public void Null_or_empty_answers_give_all_defaults(string answersLine)
    {
        var table = DefaultRow.ReadAll();
        var answers = AllAnswers(CaseInput.Valid(CaseInput.OneStep + answersLine).Steps[0].Answers);

        Assert.Equal(table.Select(r => (r.Key, r.Default, true)), answers);
    }

    // 4.4: 키가 없거나 값이 null(빈 값, ~, null, Null, NULL)이면 기본값, IsDefault true. 키마다 본다.
    [Theory]
    [MemberData(nameof(DefaultKeys))]
    public void Null_value_gives_default_for_each_key(string key)
    {
        var row = DefaultRow.ReadAll().Single(r => r.Key == key);
        foreach (var form in new[] { "", " ~", " null", " Null", " NULL" })
        {
            var answers = AllAnswers(CaseInput.Valid(CaseInput.OneStepWithAnswers + $"      {key}:{form}\n").Steps[0].Answers);

            Assert.Equal((key, row.Default, true), answers.Single(a => a.Key == key));
        }
    }

    // 4.4: 값을 직접 쓰면 기본값과 같아도 IsDefault false. existing_feature: unknown도 false다.
    [Fact]
    public void Explicit_default_values_are_not_marked_default()
    {
        var table = DefaultRow.ReadAll();
        var lines = string.Concat(table.Where(r => r.Default != "null").Select(r => $"      {r.Key}: {r.Default}\n"));
        var answers = AllAnswers(CaseInput.Valid(CaseInput.OneStepWithAnswers + lines).Steps[0].Answers);

        Assert.All(answers.Where(a => a.Key != "minutes_per_item"), a => Assert.False(a.IsDefault, a.Key));
        Assert.Equal(table.Select(r => (r.Key, r.Default)), answers.Select(a => (a.Key, a.Value)));
        Assert.Equal(new Answer<string>("unknown", false), CaseInput.Valid(CaseInput.OneStepWithAnswers + "      existing_feature: unknown\n").Steps[0].Answers.ExistingFeature);
    }

    // 4.4 표의 선택지는 모두 받는다. 직접 쓴 값이라 IsDefault false.
    [Theory]
    [MemberData(nameof(EnumKeys))]
    public void Each_listed_choice_is_accepted(string key)
    {
        var row = DefaultRow.ReadAll().Single(r => r.Key == key);
        Assert.NotEmpty(row.ChoiceList);

        foreach (var choice in row.ChoiceList)
        {
            var answers = AllAnswers(CaseInput.Valid(CaseInput.OneStepWithAnswers + $"      {key}: {choice}\n").Steps[0].Answers);

            Assert.Equal((key, choice, false), answers.Single(a => a.Key == key));
        }
    }

    // 선택지 밖 값은 enum 오류. 가능한 값은 4.4 표 순서로 ", "로 잇는다. 기본값으로 바꿔 넣지 않는다.
    [Theory]
    [MemberData(nameof(EnumKeys))]
    public void Value_outside_choices_is_enum_error_listing_spec_choices(string key)
    {
        var row = DefaultRow.ReadAll().Single(r => r.Key == key);

        CaseInput.AssertErrors(
            CaseInput.OneStepWithAnswers + $"      {key}: maybe-not\n",
            $"{CaseInput.AnswerPosition(key)}: enum steps[1].answers.{key}: 선택지 밖의 값 'maybe-not'. 가능한 값: {string.Join(", ", row.ChoiceList)}");
    }

    // 4.4·4.6: 따옴표 있는 "", "null", ''은 문자열이다. null이 아니므로 열거 값 밖 → enum.
    [Theory]
    [InlineData("\"\"", "")]
    [InlineData("\"null\"", "null")]
    [InlineData("''", "")]
    [InlineData("'~'", "~")]
    public void Quoted_null_like_value_is_string_not_null(string written, string value)
    {
        CaseInput.AssertErrors(
            CaseInput.OneStepWithAnswers + $"      rule_expressible: {written}\n",
            $"case.yaml:6:25: enum steps[1].answers.rule_expressible: 선택지 밖의 값 '{value}'. 가능한 값: yes, partial, no");
    }

    // 4.6: 열거는 대소문자를 구분한다(서수). 따옴표는 허용한다.
    [Theory]
    [InlineData("Weekly", false)]
    [InlineData("WEEKLY", false)]
    [InlineData("\"weekly\"", true)]
    [InlineData("'weekly'", true)]
    [InlineData("weekly", true)]
    public void Enum_compares_ordinally_and_allows_quotes(string written, bool accepted)
    {
        var result = CaseInput.Parse(CaseInput.OneStepWithAnswers + $"      frequency: {written}\n");

        if (accepted)
        {
            Assert.Equal(new Answer<string>("weekly", false), CaseInput.Valid(result).Steps[0].Answers.Frequency);
        }
        else
        {
            Assert.Equal("enum", Assert.Single(result.Errors).Code);
        }
    }

    // 4.6: YAML 1.1 불리언 낱말 yes·no는 열거 값으로 그대로 들어온다. 따옴표가 있어도 없어도 같다.
    [Fact]
    public void Yaml_1_1_boolean_words_are_enum_strings()
    {
        var c = CaseInput.Valid(
            "name: a\ntask: t\nsteps:\n"
            + "  - name: s\n    answers: {rule_expressible: yes, existing_feature: no}\n"
            + "  - name: q\n    answers: {rule_expressible: \"yes\", existing_feature: 'no'}\n");

        Assert.All(c.Steps, s => Assert.Equal(new Answer<string>("yes", false), s.Answers.RuleExpressible));
        Assert.All(c.Steps, s => Assert.Equal(new Answer<string>("no", false), s.Answers.ExistingFeature));
    }

    // 4.4: 값이 잘못되면 오류다. 기본값으로 바꿔 넣고 통과시키지 않는다.
    [Theory]
    [InlineData("batch_size: ten")]
    [InlineData("exception_rate: 120")]
    [InlineData("minutes_per_item: -1")]
    [InlineData("input_kind: Table")]
    public void Invalid_answer_is_error_not_default(string line)
    {
        var result = CaseInput.Parse(CaseInput.OneStepWithAnswers + "      " + line + "\n");

        Assert.Single(result.Errors);
        Assert.Null(result.Case);
    }
}
