using System.Globalization;
using Scorecard.Core.Cases;
using Scorecard.Core.Synthetic;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Guards;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.Synthetic;

// 스펙 3.3 "같은 입력이면 같은 바이트", 4.2 시드. 카드 수용 기준 1(두 번 같은 바이트, 다른 경로·문화권에서도 같다).
// 다른 경로·문화권은 G2(synthetic-case-valid)도 본다. 여기서는 공개 예제 계획으로 본다.
[Trait("Spec", "synthetic-examples")]
public class SyntheticDeterminismTests
{
    // 3.3 문화권 7종(Invariant, de-DE, tr-TR, ar-SA, ko-KR, fr-CH, th-TH). 빈 이름은 InvariantCulture.
    public static TheoryData<string, string> ExampleCultures
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var example in SyntheticInput.DataExamples)
            {
                foreach (var culture in new[] { "", "de-DE", "tr-TR", "ar-SA", "ko-KR", "fr-CH", "th-TH" })
                {
                    data.Add(example, culture);
                }
            }

            return data;
        }
    }

    // 같은 프로세스에서 두 번. 케이스·계획 객체를 새로 만들어도 같은 바이트.
    [Theory]
    [MemberData(nameof(SyntheticInput.DataExampleNames), MemberType = typeof(SyntheticInput))]
    public void Same_input_twice_gives_same_bytes(string example)
    {
        var first = SyntheticInput.Render(SyntheticInput.Generate(example));
        var second = SyntheticInput.Render(SyntheticInput.Generate(example));

        Assert.Equal(first.Reference, second.Reference);
        Assert.Equal(first.Candidate, second.Candidate);
    }

    // 현재 문화권을 바꿔도 같은 바이트. 파싱·생성·쓰기를 모두 그 문화권에서 한다. 문화권은 finally로 되돌린다.
    [Theory]
    [MemberData(nameof(ExampleCultures))]
    public void Output_does_not_depend_on_culture(string example, string culture)
    {
        Assert.Null(GuardChecks.GermanCulturePrecondition());
        var baseline = Concat(SyntheticInput.Render(SyntheticInput.Generate(example)));

        Assert.Equal(baseline, RenderUnder(culture, example));
    }

    // 4.2: 시드를 바꾸면 값이 바뀐다(시드가 실제로 쓰인다).
    [Theory]
    [MemberData(nameof(SyntheticInput.DataExampleNames), MemberType = typeof(SyntheticInput))]
    public void Next_seed_gives_different_bytes(string example)
    {
        var definition = SyntheticInput.CaseOf(example);
        var plan = SyntheticSpec.Plan(example);

        var current = SyntheticInput.Render(SyntheticGenerator.Generate(definition, plan));
        var next = SyntheticInput.Render(SyntheticGenerator.Generate(definition, plan with { Seed = plan.Seed + 1 }));

        Assert.NotEqual(current.Reference, next.Reference);
        Assert.NotEqual(current.Candidate, next.Candidate);
    }

    // 다른 폴더에 둔 같은 case.yaml → 같은 바이트(생성기는 경로를 쓰지 않는다).
    [Theory]
    [MemberData(nameof(SyntheticInput.DataExampleNames), MemberType = typeof(SyntheticInput))]
    public void Same_case_in_different_folders_gives_same_bytes(string example)
    {
        var bytes = SyntheticInput.ExampleBytes(example, CaseParser.FileName);
        var outputs = new List<byte[]>();
        foreach (var prefix in new[] { "scorecard-synthetic-a-", "scorecard-synthetic-b-" })
        {
            var dir = Directory.CreateTempSubdirectory(prefix).FullName;
            try
            {
                var nested = Path.Combine(dir, "x", example);
                Directory.CreateDirectory(nested);
                File.WriteAllBytes(Path.Combine(nested, CaseParser.FileName), bytes);
                var definition = CaseInput.Valid(CaseParser.ParseDirectory(nested));
                outputs.Add(Concat(SyntheticInput.Render(SyntheticGenerator.Generate(definition, SyntheticSpec.Plan(example)))));
            }
            finally
            {
                CaseInput.TryDeleteDirectory(dir);
            }
        }

        Assert.Equal(outputs[0], outputs[1]);
        Assert.Equal(Concat(SyntheticInput.Render(SyntheticInput.Generate(example))), outputs[0]);
    }

    // 원본 불변: 예제를 읽어 생성해도 examples/ 파일(경로·크기·SHA-256·수정 시각)이 그대로다.
    [Fact]
    public void Generating_examples_leaves_example_files_unchanged()
    {
        var root = RepoRoot.Combine("examples");
        var before = FolderSnapshot.Take(root);
        Assert.NotEmpty(before);

        foreach (var example in SyntheticInput.DataExamples)
        {
            SyntheticInput.Render(SyntheticInput.Generate(example));
        }

        Assert.Empty(FolderSnapshot.Compare(before, FolderSnapshot.Take(root), compareTime: true));
    }

    private static byte[] Concat((byte[] Reference, byte[] Candidate) csv) => [.. csv.Reference, .. csv.Candidate];

    private static byte[] RenderUnder(string cultureName, string example)
    {
        var culture = cultureName.Length == 0 ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(cultureName);
        var (saved, savedUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            return Concat(SyntheticInput.Render(SyntheticInput.Generate(example)));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
            CultureInfo.CurrentUICulture = savedUi;
        }
    }
}
