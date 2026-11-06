using System.Globalization;
using Scorecard.Core.Cases;

namespace Scorecard.Tests.CaseSchema;

// 스펙 4.6·4.10 결정론: 같은 바이트 → 같은 모델·같은 오류 문자열.
// 두 번 실행, 문화권 6종(Invariant 2회, de-DE, tr-TR, ko-KR, ar-SA), 다른 경로 사본.
// G2(CoreGuardTargetTests)도 같은 것을 작업 폴더 단위로 본다.
[Trait("Spec", "case-schema")]
public class CaseDeterminismTests
{
    private static readonly string[] Cultures = ["", "", "de-DE", "tr-TR", "ko-KR", "ar-SA"];

    // 정상·오류 표본 모두 본다. 오류 표본이 있어야 오류 문자열의 경로·문화권 의존을 잡는다(4.10).
    [Theory]
    [InlineData(SpecSamples.ValidFull)]
    [InlineData(SpecSamples.TypeErrors)]
    [InlineData(SpecSamples.MissingRequired)]
    public void Same_bytes_give_same_output_twice(string sample)
    {
        var bytes = CaseInput.Sample(sample);

        Assert.Equal(ParseResultJson.Render(CaseInput.Parse(bytes)), ParseResultJson.Render(CaseInput.Parse(bytes)));
    }

    // 문화권을 바꿔도 결과 바이트가 같다. 문화권은 finally로 되돌린다.
    [Theory]
    [InlineData(SpecSamples.ValidFull)]
    [InlineData(SpecSamples.TypeErrors)]
    public void Output_does_not_depend_on_culture(string sample)
    {
        var bytes = CaseInput.Sample(sample);
        var outputs = Cultures.Select(name => Convert.ToHexStringLower(ParseResultJson.Render(ParseUnder(name, bytes)))).ToArray();

        Assert.Single(outputs.Distinct());
    }

    // 소수 값이 문화권 소수점(de-DE ',')에 따라 바뀌지 않는다. 오류 문구의 숫자도 같다.
    [Fact]
    public void Decimal_values_and_messages_use_invariant_culture()
    {
        var yaml = CaseInput.Bytes(CaseInput.OneStepWithAnswers + "      exception_rate: 62.5\n      minutes_per_item: 1.50\n");
        var invalid = CaseInput.Bytes(CaseInput.OneStepWithAnswers + "      exception_rate: 100.5\n");

        var answers = CaseInput.Valid(ParseUnder("de-DE", yaml)).Steps[0].Answers;
        var error = Assert.Single(ParseUnder("de-DE", invalid).Errors);

        Assert.Equal(62.5m, answers.ExceptionRate.Value);
        Assert.Equal("1.50", answers.MinutesPerItem.Value?.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("범위 밖이다 (0 이상 100 이하): 100.5", error.Message);
    }

    // 다른 경로의 사본 두 개를 ParseDirectory로 읽어도 같다.
    [Theory]
    [InlineData(SpecSamples.ValidFull)]
    [InlineData(SpecSamples.TypeErrors)]
    public void Output_does_not_depend_on_folder_path(string sample)
    {
        var a = Directory.CreateTempSubdirectory("scorecard-case-a-").FullName;
        var b = Directory.CreateTempSubdirectory("scorecard-case-b-").FullName;
        try
        {
            var deeper = Path.Combine(b, "x", "깊은 폴더");
            Directory.CreateDirectory(deeper);
            File.WriteAllBytes(Path.Combine(a, CaseParser.FileName), CaseInput.Sample(sample));
            File.WriteAllBytes(Path.Combine(deeper, CaseParser.FileName), CaseInput.Sample(sample));

            Assert.Equal(
                ParseResultJson.Render(CaseParser.ParseDirectory(a)),
                ParseResultJson.Render(CaseParser.ParseDirectory(deeper)));
        }
        finally
        {
            CaseInput.TryDeleteDirectory(a);
            CaseInput.TryDeleteDirectory(b);
        }
    }

    // 빈 이름은 InvariantCulture.
    private static CaseParseResult ParseUnder(string cultureName, byte[] bytes)
    {
        var culture = cultureName.Length == 0 ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(cultureName);
        var (saved, savedUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            return CaseInput.Parse(bytes);
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
            CultureInfo.CurrentUICulture = savedUi;
        }
    }
}
