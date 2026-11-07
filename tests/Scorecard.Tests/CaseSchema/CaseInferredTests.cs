using System.Text;
using Scorecard.Core.Cases;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.CaseSchema;

// 스펙 4.13 추정 답 목록 inferred(T-008, ADR 0004 결정 2)와 6.7 오류 표본. 카드 수용 기준 3.
// 오류 문구는 문자열로 비교한다(승인 결정 9). 6.7 블록과 이 파일의 기대 줄이 같은지도 본다.
// 4.4 human_signoff 글머리(T-008, ADR 0003)의 값 형식도 여기서 본다.
[Trait("Spec", "case-schema")]
public class CaseInferredTests
{
    private const string InferredErrorsHeading = "### 6.7 추정 답 목록 오류 (T-008)";
    private const string InferredErrorsSample = "inferred-errors";
    private const string NeedsValue = "inferred에 적은 답은 answers에 값이 있어야 한다";

    // 단계 하나에 answers 한 줄(5줄째). 아래에 inferred 줄을 붙이면 6줄째다.
    // "    inferred: [" 다음 첫 원소는 16열이다.
    private const string WithFrequency = CaseInput.OneStep + "    answers: {frequency: weekly}\n";

    // 6.7 오류 8건. 블록과 같은지는 아래 테스트가 본다.
    private static readonly string[] InferredErrors =
    [
        "case.yaml:7:27: enum steps[1].inferred[2]: 선택지 밖의 값 'freq'. 가능한 값: input_kind, rule_expressible, frequency, batch_size, exception_rate, current_actor, minutes_per_item, failure_impact, existing_feature, human_signoff",
        "case.yaml:7:33: enum steps[1].inferred[3]: 선택지 밖의 값 'Batch_size'. 가능한 값: input_kind, rule_expressible, frequency, batch_size, exception_rate, current_actor, minutes_per_item, failure_impact, existing_feature, human_signoff",
        "case.yaml:7:45: enum steps[1].inferred[4]: 선택지 밖의 값 'name'. 가능한 값: input_kind, rule_expressible, frequency, batch_size, exception_rate, current_actor, minutes_per_item, failure_impact, existing_feature, human_signoff",
        "case.yaml:10:27: duplicate steps[2].inferred[2]: 추정 답 키가 중복된다 (처음: steps[2].inferred[1])",
        "case.yaml:13:16: required steps[3].answers.batch_size: inferred에 적은 답은 answers에 값이 있어야 한다",
        "case.yaml:13:28: required steps[3].answers.minutes_per_item: inferred에 적은 답은 answers에 값이 있어야 한다",
        "case.yaml:13:46: required steps[3].inferred[3]: 값이 비어 있다",
        "case.yaml:16:15: type steps[4].inferred: 목록이어야 한다",
    ];

    // 4.4 표의 키(표 순서). 키마다 그 키만 inferred로 적는다.
    public static TheoryData<string> AnswerKeys => new(DefaultRow.ReadAll().Select(r => r.Key));

    // 6.7 표본 파일 = 스펙 블록(바이트).
    [Fact]
    public void Inferred_errors_sample_matches_spec_block()
    {
        var expected = Encoding.UTF8.GetBytes(SpecDocument.CodeBlocks(InferredErrorsHeading)[0]);

        Assert.Equal(expected, CaseInput.Sample(InferredErrorsSample));
    }

    // 6.7 오류 블록 = 이 파일의 기대 줄.
    [Fact]
    public void Inferred_expected_errors_match_spec_block()
    {
        Assert.Equal(SpecDocument.BlockLines(SpecDocument.CodeBlocks(InferredErrorsHeading)[1]), InferredErrors);
    }

    // 6.7 표본의 오류 8건이 순서까지 같다. enum·duplicate·required·type 네 코드가 모두 나온다(카드 수용 기준 3).
    [Fact]
    public void Inferred_errors_sample_gives_spec_errors()
    {
        var result = CaseInput.Parse(CaseInput.Sample(InferredErrorsSample));

        CaseInput.AssertErrors(result, InferredErrors);
        Assert.Equal(["duplicate", "enum", "required", "type"], result.Errors.Select(e => e.Code).Distinct().Order(StringComparer.Ordinal));
    }

    // 4.13: 없거나 null(inferred:, ~, null)이면 추정 답이 없다. 빈 목록 []도 같다. 오류가 없다.
    [Theory]
    [InlineData("")]
    [InlineData("    inferred:\n")]
    [InlineData("    inferred: ~\n")]
    [InlineData("    inferred: null\n")]
    [InlineData("    inferred: []\n")]
    public void Missing_null_or_empty_inferred_means_no_inferred_answers(string inferredLine)
    {
        var a = CaseInput.Valid(WithFrequency + inferredLine).Steps[0].Answers;

        Assert.All(Flags(a), f => Assert.False(f.IsInferred, f.Key));
    }

    // 4.13: 흐름 목록과 블록 목록 모두 받는다. 단계 항목 안의 키 순서는 상관없다(inferred가 answers 앞이어도 된다).
    [Theory]
    [InlineData(CaseInput.OneStep + "    answers: {frequency: weekly, batch_size: 3}\n    inferred: [frequency, batch_size]\n")]
    [InlineData(CaseInput.OneStep + "    answers: {frequency: weekly, batch_size: 3}\n    inferred:\n      - frequency\n      - batch_size\n")]
    [InlineData(CaseInput.OneStep + "    inferred: [frequency, batch_size]\n    answers: {frequency: weekly, batch_size: 3}\n")]
    [InlineData("name: a\ntask: t\nsteps:\n  - inferred: [batch_size, frequency]\n    name: s\n    answers: {frequency: weekly, batch_size: 3}\n")]
    public void Flow_and_block_lists_in_any_key_order_are_accepted(string yaml)
    {
        var a = CaseInput.Valid(yaml).Steps[0].Answers;

        Assert.Equal(["frequency", "batch_size"], Flags(a).Where(f => f.IsInferred).Select(f => f.Key));
        Assert.Equal(new Answer<string>("weekly", false, true), a.Frequency);
        Assert.Equal(new Answer<int>(3, false, true), a.BatchSize);
    }

    // 4.13: 목록이 아니면(스칼라, 매핑) type "목록이어야 한다". 위치는 그 값, 경로 steps[n].inferred.
    [Theory]
    [InlineData("frequency")]
    [InlineData("{frequency: x}")]
    [InlineData("'frequency'")]
    public void Non_list_inferred_is_type_error(string written)
    {
        CaseInput.AssertErrors(
            WithFrequency + $"    inferred: {written}\n",
            "case.yaml:6:15: type steps[1].inferred: 목록이어야 한다");
    }

    // 4.13: 원소가 매핑·목록이면 type "스칼라 값이어야 한다". 경로는 steps[n].inferred[m](m은 1부터).
    [Theory]
    [InlineData("[{frequency: x}]")]
    [InlineData("[[frequency]]")]
    public void Collection_element_is_type_error(string written)
    {
        CaseInput.AssertErrors(
            WithFrequency + $"    inferred: {written}\n",
            "case.yaml:6:16: type steps[1].inferred[1]: 스칼라 값이어야 한다");
    }

    // 4.13: null 원소는 required "값이 비어 있다". 경로 steps[n].inferred[m].
    [Theory]
    [InlineData("[~]")]
    [InlineData("[null]")]
    [InlineData("[NULL]")]
    public void Null_element_is_required_error(string written)
    {
        CaseInput.AssertErrors(
            WithFrequency + $"    inferred: {written}\n",
            "case.yaml:6:16: required steps[1].inferred[1]: 값이 비어 있다");
    }

    // 4.13: 원소는 4.4 답 키 10개 중 하나(서수, 대소문자 구분). 아니면 enum. 가능한 값은 4.4 표 순서의 키 10개.
    // name·yes·Frequency도 enum이다. 따옴표 있는 ""는 문자열이다(4.4).
    [Theory]
    [InlineData("freq", "freq")]
    [InlineData("Frequency", "Frequency")]
    [InlineData("FREQUENCY", "FREQUENCY")]
    [InlineData("name", "name")]
    [InlineData("answers", "answers")]
    [InlineData("inferred", "inferred")]
    [InlineData("yes", "yes")]
    [InlineData("\"\"", "")]
    [InlineData("' frequency'", " frequency")]
    public void Element_that_is_not_an_answer_key_is_enum_error(string written, string shown)
    {
        var keys = string.Join(", ", DefaultRow.ReadAll().Select(r => r.Key));

        CaseInput.AssertErrors(
            WithFrequency + $"    inferred: [{written}]\n",
            $"case.yaml:6:16: enum steps[1].inferred[1]: 선택지 밖의 값 '{shown}'. 가능한 값: {keys}");
    }

    // 4.13: 따옴표가 있어도 키로 읽는다.
    [Theory]
    [InlineData("\"frequency\"")]
    [InlineData("'frequency'")]
    public void Quoted_answer_key_is_accepted(string written)
    {
        var a = CaseInput.Valid(WithFrequency + $"    inferred: [{written}]\n").Steps[0].Answers;

        Assert.Equal(new Answer<string>("weekly", false, true), a.Frequency);
    }

    // 4.13: 같은 키가 둘 이상이면 둘째부터 duplicate. 메시지에 처음 위치를 쓴다.
    [Fact]
    public void Duplicate_keys_are_errors_from_the_second()
    {
        CaseInput.AssertErrors(
            CaseInput.OneStep + "    answers: {frequency: weekly, batch_size: 3}\n    inferred: [frequency, batch_size, frequency, frequency]\n",
            "case.yaml:6:39: duplicate steps[1].inferred[3]: 추정 답 키가 중복된다 (처음: steps[1].inferred[1])",
            "case.yaml:6:50: duplicate steps[1].inferred[4]: 추정 답 키가 중복된다 (처음: steps[1].inferred[1])");
    }

    // 4.13: 적은 키의 답은 answers에 값이 있어야 한다. 키가 없거나 null이거나 answers가 없으면 required.
    // 위치는 그 원소, 경로는 steps[n].answers.<키>. 값이 없는 답은 기본값이지 추정이 아니다(ADR 0004).
    [Theory]
    [InlineData(WithFrequency + "    inferred: [batch_size]\n", "case.yaml:6:16", "batch_size")]
    [InlineData(CaseInput.OneStep + "    answers: {frequency: ~}\n    inferred: [frequency]\n", "case.yaml:6:16", "frequency")]
    [InlineData(CaseInput.OneStep + "    answers: {minutes_per_item: null}\n    inferred: [minutes_per_item]\n", "case.yaml:6:16", "minutes_per_item")]
    [InlineData(CaseInput.OneStep + "    inferred: [human_signoff]\n", "case.yaml:5:16", "human_signoff")]
    [InlineData(CaseInput.OneStep + "    answers: ~\n    inferred: [frequency]\n", "case.yaml:6:16", "frequency")]
    [InlineData(CaseInput.OneStep + "    answers: {}\n    inferred: [frequency]\n", "case.yaml:6:16", "frequency")]
    public void Inferred_answer_without_value_is_required_error(string yaml, string position, string key)
    {
        CaseInput.AssertErrors(yaml, $"{position}: required steps[1].answers.{key}: {NeedsValue}");
    }

    // 4.13: 값이 있지만 잘못된 답은 그 답의 오류 한 건만 난다. inferred 쪽 오류는 더하지 않는다.
    [Fact]
    public void Invalid_inferred_answer_gives_only_the_answer_error()
    {
        CaseInput.AssertErrors(
            CaseInput.OneStep + "    answers: {rule_expressible: maybe}\n    inferred: [rule_expressible]\n",
            "case.yaml:5:33: enum steps[1].answers.rule_expressible: 선택지 밖의 값 'maybe'. 가능한 값: yes, partial, no");
    }

    // 4.13: answers가 매핑이 아니면 answers의 type 오류와 inferred 원소마다의 required가 같이 난다(값이 없으므로).
    [Fact]
    public void Non_mapping_answers_gives_type_and_required_for_each_element()
    {
        CaseInput.AssertErrors(
            CaseInput.OneStep + "    answers: [frequency]\n    inferred: [frequency, batch_size]\n",
            "case.yaml:5:14: type steps[1].answers: 매핑이어야 한다",
            $"case.yaml:6:16: required steps[1].answers.frequency: {NeedsValue}",
            $"case.yaml:6:27: required steps[1].answers.batch_size: {NeedsValue}");
    }

    // 4.13: 단계마다 따로 본다. 경로의 n은 그 단계 번호다.
    [Fact]
    public void Inferred_is_checked_per_step()
    {
        CaseInput.AssertErrors(
            "name: a\ntask: t\nsteps:\n"
            + "  - name: s\n    answers: {frequency: weekly}\n    inferred: [frequency]\n"
            + "  - name: q\n    answers: {batch_size: 2}\n    inferred: [frequency]\n",
            $"case.yaml:9:16: required steps[2].answers.frequency: {NeedsValue}");
    }

    // 4.13 결과: 키마다, 목록에 있는 답만 IsInferred = true. 값·IsDefault는 그대로다.
    // 같은 파일에서 inferred 줄만 뺀 모델과 IsInferred만 다르다.
    [Theory]
    [MemberData(nameof(AnswerKeys))]
    public void Only_listed_answer_is_inferred(string key)
    {
        var answers = "    answers: {input_kind: table, rule_expressible: yes, frequency: daily, batch_size: 7, exception_rate: 2.5, "
            + "current_actor: program, minutes_per_item: 0, failure_impact: external, existing_feature: no, human_signoff: no}\n";
        var plain = CaseInput.Valid(CaseInput.OneStep + answers).Steps[0].Answers;
        var marked = CaseInput.Valid(CaseInput.OneStep + answers + $"    inferred: [{key}]\n").Steps[0].Answers;

        Assert.Equal(DefaultRow.ReadAll().Select(r => (r.Key, r.Key == key)), Flags(marked).Select(f => (f.Key, f.IsInferred)));
        Assert.All(Flags(plain), f => Assert.False(f.IsInferred, f.Key));
        Assert.Equal(CaseDefaultsTests.AllAnswers(plain), CaseDefaultsTests.AllAnswers(marked));
    }

    // 4.13: 쓴 순서와 상관없다. 같은 키 집합이면 같은 모델이다.
    [Fact]
    public void Written_order_does_not_matter()
    {
        const string Answers = "    answers: {frequency: weekly, failure_impact: external, human_signoff: no}\n";
        var forward = CaseInput.Valid(CaseInput.OneStep + Answers + "    inferred: [frequency, failure_impact, human_signoff]\n").Steps[0].Answers;
        var backward = CaseInput.Valid(CaseInput.OneStep + Answers + "    inferred: [human_signoff, failure_impact, frequency]\n").Steps[0].Answers;

        Assert.Equal(forward, backward);
        Assert.Equal(["frequency", "failure_impact", "human_signoff"], Flags(forward).Where(f => f.IsInferred).Select(f => f.Key));
    }

    // 4.12·4.13: 한 답이 IsDefault이면서 IsInferred일 수 없다. 기본값인 답은 inferred에 적으면 required다.
    // inferred가 없는 정상 표본(6.1, 공개 예제 case.yaml·answers.yaml)은 모든 답이 false다.
    [Theory]
    [InlineData("tests/Scorecard.Tests/TestData/case-schema/valid-full/case.yaml")]
    [InlineData("tests/Scorecard.Tests/TestData/guard/case-valid/case.yaml")]
    [InlineData("examples/table-compare/case.yaml")]
    [InlineData("examples/form-fill/answers.yaml")]
    [InlineData("examples/mixed-exceptions/case.yaml")]
    public void Files_without_inferred_have_no_inferred_answers(string relativePath)
    {
        var c = CaseInput.Valid(CaseInput.Parse(File.ReadAllBytes(RepoRoot.Combine(relativePath))));

        Assert.All(c.Steps.SelectMany(s => Flags(s.Answers)), f => Assert.False(f.IsInferred, f.Key));
    }

    // 공개 API(4.12): Answer<T>의 셋째 위치 매개변수는 IsInferred, 기본값 false. 기존 2인자 생성은 false다.
    // 레코드 값 비교에 IsInferred가 들어간다.
    [Fact]
    public void Answer_has_optional_is_inferred_parameter()
    {
        var explicitAnswer = new Answer<string>("weekly", false);
        var inferredAnswer = new Answer<string>("weekly", IsDefault: false, IsInferred: true);

        Assert.False(explicitAnswer.IsInferred);
        Assert.True(inferredAnswer.IsInferred);
        Assert.NotEqual(explicitAnswer, inferredAnswer);
        Assert.Equal(explicitAnswer, inferredAnswer with { IsInferred = false });
    }

    // 4.4 human_signoff 글머리: 없거나 null이면 no·IsDefault. yes·no는 따옴표가 있어도 없어도 문자열로 들어온다.
    [Theory]
    [InlineData("", "no", true)]
    [InlineData(", human_signoff: ~", "no", true)]
    [InlineData(", human_signoff: yes", "yes", false)]
    [InlineData(", human_signoff: no", "no", false)]
    [InlineData(", human_signoff: \"yes\"", "yes", false)]
    [InlineData(", human_signoff: 'no'", "no", false)]
    public void Human_signoff_reads_yes_no_and_defaults_to_no(string written, string value, bool isDefault)
    {
        var a = CaseInput.Valid(CaseInput.OneStep + $"    answers: {{frequency: weekly{written}}}\n").Steps[0].Answers;

        Assert.Equal(new Answer<string>(value, isDefault), a.HumanSignoff);
    }

    // 4.4 human_signoff 글머리: Yes·true·""·unknown은 enum 오류다. 가능한 값은 yes, no.
    [Theory]
    [InlineData("Yes", "Yes")]
    [InlineData("true", "true")]
    [InlineData("\"\"", "")]
    [InlineData("unknown", "unknown")]
    [InlineData("NO", "NO")]
    public void Human_signoff_other_values_are_enum_errors(string written, string shown)
    {
        CaseInput.AssertErrors(
            CaseInput.OneStepWithAnswers + $"      human_signoff: {written}\n",
            $"case.yaml:6:22: enum steps[1].answers.human_signoff: 선택지 밖의 값 '{shown}'. 가능한 값: yes, no");
    }

    // 답 10개의 IsInferred(4.4 표 순서).
    private static (string Key, bool IsInferred)[] Flags(StepAnswers a) =>
    [
        ("input_kind", a.InputKind.IsInferred),
        ("rule_expressible", a.RuleExpressible.IsInferred),
        ("frequency", a.Frequency.IsInferred),
        ("batch_size", a.BatchSize.IsInferred),
        ("exception_rate", a.ExceptionRate.IsInferred),
        ("current_actor", a.CurrentActor.IsInferred),
        ("minutes_per_item", a.MinutesPerItem.IsInferred),
        ("failure_impact", a.FailureImpact.IsInferred),
        ("existing_feature", a.ExistingFeature.IsInferred),
        ("human_signoff", a.HumanSignoff.IsInferred),
    ];
}
