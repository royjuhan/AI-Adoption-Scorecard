using Scorecard.Core.Cases;

namespace Scorecard.Tests.CaseSchema;

// 스펙 4.3·4.5·6.1·6.2·6.6: 정상 입력에서 읽힌 값.
[Trait("Spec", "case-schema")]
public class CaseValidTests
{
    // 6.1 정상(전체). 오류 0건과 6.1 표의 값·IsDefault.
    [Fact]
    public void Full_sample_reads_top_level_fields()
    {
        var c = CaseInput.Valid(CaseInput.Parse(CaseInput.Sample(SpecSamples.ValidFull)));

        Assert.Equal("sample-full", c.Name);
        Assert.Equal("합성 센서 로그에서 구간을 골라 평균을 낸다", c.Task);
        Assert.Equal("사람이 그래프를 보고 구간을 고른다", c.Before);
        Assert.Equal("프로그램이 구간을 고른다.\n사람이 검토한다.\n", c.After);
        Assert.Equal("build-time", c.AiUsage);
        Assert.Equal(["로그 파일 열기", "구간 선택(불규칙)", "결과 확인", "보고서 채우기"], c.Steps.Select(s => s.Name));
        Assert.Equal([1, 2, 3, 4], c.Steps.Select(s => s.Number));
    }

    [Fact]
    public void Full_sample_reads_inputs_compare_and_thresholds()
    {
        var c = CaseInput.Valid(CaseInput.Parse(CaseInput.Sample(SpecSamples.ValidFull)));

        Assert.Equal(new CaseInputs("reference/*.csv", "candidate/*.csv", "row_id"), c.Inputs);
        Assert.Equal(
            [new CompareField("start", 0m, null), new CompareField("mean", null, 1.0m), new CompareField("label", null, null)],
            c.Compare);
        Assert.Equal(new CaseThresholds(90m, 5m, true), c.Thresholds);
    }

    // 1단계: 8개를 직접 썼고 minutes_per_item만 키가 없다.
    [Fact]
    public void Full_sample_step_1_has_explicit_answers_and_default_minutes()
    {
        var a = CaseInput.Valid(CaseInput.Parse(CaseInput.Sample(SpecSamples.ValidFull))).Steps[0].Answers;

        Assert.Equal(new Answer<string>("fixed_form", false), a.InputKind);
        Assert.Equal(new Answer<string>("yes", false), a.RuleExpressible);
        Assert.Equal(new Answer<string>("weekly", false), a.Frequency);
        Assert.Equal(new Answer<int>(10, false), a.BatchSize);
        Assert.Equal(new Answer<decimal>(5m, false), a.ExceptionRate);
        Assert.Equal(new Answer<string>("program", false), a.CurrentActor);
        Assert.Equal(new Answer<decimal?>(null, true), a.MinutesPerItem);
        Assert.Equal(new Answer<string>("internal_rework", false), a.FailureImpact);
        Assert.Equal(new Answer<string>("no", false), a.ExistingFeature);
    }

    // 2단계: 9개 모두 직접 썼다. 소수 값.
    [Fact]
    public void Full_sample_step_2_has_all_explicit_answers()
    {
        var a = CaseInput.Valid(CaseInput.Parse(CaseInput.Sample(SpecSamples.ValidFull))).Steps[1].Answers;

        Assert.Equal(new Answer<string>("table", false), a.InputKind);
        Assert.Equal(new Answer<string>("partial", false), a.RuleExpressible);
        Assert.Equal(new Answer<string>("weekly", false), a.Frequency);
        Assert.Equal(new Answer<int>(3, false), a.BatchSize);
        Assert.Equal(new Answer<decimal>(62.5m, false), a.ExceptionRate);
        Assert.Equal(new Answer<string>("human", false), a.CurrentActor);
        Assert.Equal(new Answer<decimal?>(7.5m, false), a.MinutesPerItem);
        Assert.Equal(new Answer<string>("internal_rework", false), a.FailureImpact);
        Assert.Equal(new Answer<string>("no", false), a.ExistingFeature);
    }

    // 3단계: 따옴표 "no"는 직접 쓴 값, ~는 기본값, 없는 키는 기본값.
    [Fact]
    public void Full_sample_step_3_mixes_explicit_null_and_missing_answers()
    {
        var a = CaseInput.Valid(CaseInput.Parse(CaseInput.Sample(SpecSamples.ValidFull))).Steps[2].Answers;

        Assert.Equal(new Answer<string>("fixed_form", true), a.InputKind);
        Assert.Equal(new Answer<string>("no", false), a.RuleExpressible);
        Assert.Equal(new Answer<string>("weekly", true), a.Frequency);
        Assert.Equal(new Answer<int>(10, true), a.BatchSize);
        Assert.Equal(new Answer<decimal>(20m, true), a.ExceptionRate);
        Assert.Equal(new Answer<string>("human", true), a.CurrentActor);
        Assert.Equal(new Answer<decimal?>(null, true), a.MinutesPerItem);
        Assert.Equal(new Answer<string>("external", false), a.FailureImpact);
        Assert.Equal(new Answer<string>("unknown", true), a.ExistingFeature);
    }

    // 4단계: answers가 없다. 9개 모두 기본값(기본값 표 대조는 CaseDefaultsTests).
    [Fact]
    public void Full_sample_step_4_without_answers_is_all_default()
    {
        var a = CaseInput.Valid(CaseInput.Parse(CaseInput.Sample(SpecSamples.ValidFull))).Steps[3].Answers;

        Assert.All(CaseDefaultsTests.AllAnswers(a), x => Assert.True(x.IsDefault, x.Key));
    }

    // 6.2 최소. 선택 필드는 없음(null)·빈 목록·세 값 null.
    [Fact]
    public void Minimal_sample_leaves_optional_fields_empty()
    {
        var c = CaseInput.Valid(SpecSamples.Minimal);

        Assert.Equal("sample-minimal", c.Name);
        Assert.Equal("최소 합성 예시", c.Task);
        Assert.Null(c.Before);
        Assert.Null(c.After);
        Assert.Null(c.AiUsage);
        Assert.Null(c.Inputs);
        Assert.Empty(c.Compare);
        Assert.Equal(new CaseThresholds(null, null, null), c.Thresholds);
        var step = Assert.Single(c.Steps);
        Assert.Equal(new CaseStep(1, "한 단계", step.Answers), step);
        Assert.All(CaseDefaultsTests.AllAnswers(step.Answers), x => Assert.True(x.IsDefault, x.Key));
    }

    // 6.6: 12.1 구조. minutes_per_item이 모든 단계에서 빠졌다 → null, IsDefault true.
    // YAML 1.1 낱말 yes·no는 열거 값으로 그대로 들어온다(4.6).
    [Fact]
    public void Example_12_1_structure_reads_without_errors()
    {
        var c = CaseInput.Valid(CaseInput.Parse(CaseInput.Sample(SpecSamples.Example121)));

        Assert.Equal(7, c.Steps.Count);
        Assert.Equal("build-time", c.AiUsage);
        Assert.All(c.Steps, s => Assert.Equal(new Answer<decimal?>(null, true), s.Answers.MinutesPerItem));
        Assert.All(c.Steps, s => Assert.Equal(new Answer<string>("no", false), s.Answers.ExistingFeature));
        Assert.Equal(
            ["yes", "yes", "partial", "yes", "yes", "no", "no"],
            c.Steps.Select(s => s.Answers.RuleExpressible.Value));
        Assert.Equal(new CaseInputs("reference/*.csv", "candidate/*.csv", "file_id"), c.Inputs);
        Assert.Equal(new CompareField("mean_value", null, 1.0m), c.Compare[2]);
        Assert.Equal(new CaseThresholds(95m, 2m, true), c.Thresholds);
    }

    // 4.3: 키 순서는 상관없다.
    [Fact]
    public void Top_level_key_order_does_not_matter()
    {
        const string reordered = "steps:\n  - name: 한 단계\ntask: 최소 합성 예시\nname: sample-minimal\n";

        Assert.Equal(
            ParseResultJson.RenderText(CaseInput.Parse(SpecSamples.Minimal)),
            ParseResultJson.RenderText(CaseInput.Parse(reordered)));
    }

    // 4.3: 자유 서술은 저장만. 앞뒤 공백을 지우지 않는다.
    [Fact]
    public void Free_text_is_stored_without_trimming()
    {
        var c = CaseInput.Valid("name: a\ntask: \" 앞뒤 공백 \"\nbefore: '  둘 '\nsteps:\n  - name: \" 공백 \"\n");

        Assert.Equal(" 앞뒤 공백 ", c.Task);
        Assert.Equal("  둘 ", c.Before);
        Assert.Equal(" 공백 ", c.Steps[0].Name);
    }

    // 4.3: 블록 값(|)의 줄바꿈은 파일 줄바꿈과 상관없이 \n이다.
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void Block_scalar_line_breaks_are_lf(string newline)
    {
        var yaml = "name: a\ntask: |\n  첫 줄\n  둘째 줄\nsteps:\n  - name: s\n".Replace("\n", newline);

        Assert.Equal("첫 줄\n둘째 줄\n", CaseInput.Valid(yaml).Task);
    }

    // 4.6: 문자열은 따옴표 여부와 상관없다. null이면 값 없음(선택 필드).
    [Theory]
    [InlineData("before: ~", null)]
    [InlineData("before:", null)]
    [InlineData("before: null", null)]
    [InlineData("before: \"null\"", "null")]
    [InlineData("before: ''", "")]
    [InlineData("before: 123", "123")]
    [InlineData("before: true", "true")]
    public void Optional_string_reads_scalar_text_or_null(string line, string? expected)
    {
        Assert.Equal(expected, CaseInput.Valid(CaseInput.OneStep + line + "\n").Before);
    }

    // 4.3: ai_usage 선택지 세 개. 없으면 null(기본값 없음).
    [Theory]
    [InlineData("build-time")]
    [InlineData("runtime")]
    [InlineData("hybrid")]
    public void Ai_usage_accepts_each_choice(string value)
    {
        Assert.Equal(value, CaseInput.Valid(CaseInput.OneStep + "ai_usage: " + value + "\n").AiUsage);
    }

    // 4.4: 단계 이름은 서수 비교. NFC·NFD 한글은 다른 이름이라 중복이 아니다. 정규화하지 않는다.
    [Fact]
    public void Step_names_are_not_unicode_normalized()
    {
        var nfc = "가".Normalize(System.Text.NormalizationForm.FormC);
        var nfd = "가".Normalize(System.Text.NormalizationForm.FormD);

        var c = CaseInput.Valid($"name: a\ntask: t\nsteps:\n  - name: {nfc}\n  - name: {nfd}\n");

        Assert.Equal([nfc, nfd], c.Steps.Select(s => s.Name));
        Assert.Equal([1, 2], c.Steps.Select(s => s.Name.Length));
    }

    // 4.7: 처음에 --- 하나만 있으면 문서 하나다.
    [Fact]
    public void Leading_document_marker_is_single_document()
    {
        Assert.Equal(
            ParseResultJson.RenderText(CaseInput.Parse(SpecSamples.Minimal)),
            ParseResultJson.RenderText(CaseInput.Parse("---\n" + SpecSamples.Minimal)));
    }
}
