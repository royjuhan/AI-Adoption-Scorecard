using System.Security.Cryptography;
using Scorecard.Core.Comparison;
using Scorecard.Tests.Classification;
using Scorecard.Tests.Synthetic;

namespace Scorecard.Tests.Comparison;

// 스펙 3.3·4.10 결정론과 원본 불변. 카드 수용 기준 4(G2·G3는 CompareGuardTargetTests). 다른 경로·다른 프로세스 문화권은 G2가 본다.
// 같은 입력이면 같은 바이트: 두 번, 문화권 7종, CompareSources 파일 순서. 입력 바이트·예제 파일은 바뀌지 않는다.
[Trait("Spec", "metrics")]
public class CompareDeterminismTests
{
    // 3.3 [P:culture]의 문화권 7종. ""는 InvariantCulture.
    private static readonly string[] Cultures = ["", "de-DE", "tr-TR", "ar-SA", "ko-KR", "fr-CH", "th-TH"];

    public static TheoryData<string> Examples => ClassificationInput.ExampleNames;

    // 같은 입력을 두 번 대조하면 같은 바이트다.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Same_input_twice_gives_same_bytes(string example)
    {
        Assert.Equal(CompareInput.ExampleBytes(example), CompareInput.ExampleBytes(example));
    }

    // 문화권을 바꿔도(소수점이 ','인 de-DE, 대소문자 규칙이 다른 tr-TR 등) 같은 바이트다. try/finally로 되돌린다.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Culture_does_not_change_bytes(string example)
    {
        var expected = CompareInput.ExpectedBytes(example);

        foreach (var culture in Cultures)
        {
            var actual = CompareInput.WithCulture(culture, () => CompareInput.ExampleBytes(example));
            Assert.True(expected.AsSpan().SequenceEqual(actual), $"{example}: 문화권 '{culture}'에서 바이트가 다르다.");
        }
    }

    // 문화권은 오류 줄도 바꾸지 않는다(숫자 칸 읽기가 InvariantCulture). de-DE에서 "1.5"는 숫자, "1,5"는 숫자가 아니다.
    [Fact]
    public void Culture_does_not_change_number_reading()
    {
        var definition = CompareInput.Case(CompareInput.NumberAndTextCase);
        string[] Run() => CompareInput.Lines(CompareInput.Run(definition, "id,n,t\nk1,1.5,a\nk2,\"1,5\",b\n", CompareInput.GoodCsv).Errors);

        var invariant = Run();
        Assert.Equal(["reference/a.csv:3:4: number n: 숫자가 아니다: 1,5"], invariant);
        Assert.All(Cultures, c => Assert.Equal(invariant, CompareInput.WithCulture(c, Run)));
    }

    // CompareSources의 파일 순서를 뒤집어 넘겨도 같은 바이트다(경로 서수로 정렬한다).
    [Fact]
    public void Source_order_does_not_change_bytes()
    {
        var definition = CompareInput.Case(CompareInput.TextCase);
        SourceFile[] reference = [CompareInput.Source("reference/b.csv", "id,v\nk3,c\n"), CompareInput.Source("reference/a.csv", "id,v\nk1,a\nk2,b\n")];
        SourceFile[] candidate = [CompareInput.Source("candidate/b.csv", "id,v\nk3,x\n"), CompareInput.Source("candidate/a.csv", "id,v\nk2,b\nk9,z\n")];

        var forward = MetricsJson.Render(CompareInput.Measured(CompareInput.Run(definition, reference, candidate)));
        var reversed = MetricsJson.Render(CompareInput.Measured(CompareInput.Run(definition, Enumerable.Reverse(reference), Enumerable.Reverse(candidate))));

        Assert.Equal(forward, reversed);
    }

    // 예제의 CompareSources를 뒤집어 넘겨도 expected와 같다(table-compare·form-fill은 한쪽에 파일 하나라 쪽 순서만 같다).
    [Theory]
    [MemberData(nameof(SyntheticInput.DataExampleNames), MemberType = typeof(SyntheticInput))]
    public void Example_sources_compared_in_memory_equal_expected(string example)
    {
        var sources = CompareInput.ExampleSources(example);
        var definition = SyntheticInput.CaseOf(example);

        var actual = MetricsJson.Render(CompareInput.Measured(CompareEngine.Compare(definition, new CompareSources(Enumerable.Reverse(sources.Reference).ToArray(), Enumerable.Reverse(sources.Candidate).ToArray()))));

        Assert.Equal(CompareInput.ExpectedBytes(example), actual);
    }

    // 결과 행 순서를 바꾸면 셈은 같고 줄 번호가 바뀌어 바이트는 다르다(3.3). 줄 번호가 실제로 출력에 쓰인다는 확인.
    [Fact]
    public void Candidate_row_order_changes_only_line_numbers()
    {
        var sources = CompareInput.ExampleSources("table-compare");
        var definition = SyntheticInput.CaseOf("table-compare");
        var lines = CompareInput.Text(sources.Candidate[0].Content).TrimEnd('\n').Split('\n');
        var sorted = string.Concat(new[] { lines[0] }.Concat(lines.Skip(1).Order(StringComparer.Ordinal)).Select(l => l + "\n"));

        var original = CompareInput.Measured(CompareEngine.Compare(definition, sources));
        var reordered = CompareInput.Measured(CompareEngine.Compare(definition, new CompareSources(sources.Reference, [sources.Candidate[0] with { Content = CompareInput.Bytes(sorted) }])));

        Assert.Equal(original.Counts, reordered.Counts);
        Assert.Equal(original.Fields, reordered.Fields);
        Assert.NotEqual(MetricsJson.Render(original), MetricsJson.Render(reordered));
    }

    // 원본 불변: Compare는 넘긴 바이트 배열을 바꾸지 않는다. 두 번 불러도 같다.
    [Fact]
    public void Compare_does_not_change_source_bytes()
    {
        var sources = CompareInput.ExampleSources("table-compare");
        var definition = SyntheticInput.CaseOf("table-compare");
        var before = sources.Reference.Concat(sources.Candidate).Select(f => (f.Path, Hash: Convert.ToHexStringLower(SHA256.HashData(f.Content)))).ToArray();

        _ = CompareEngine.Compare(definition, sources);
        _ = CompareEngine.Compare(definition, sources);

        Assert.Equal(before, sources.Reference.Concat(sources.Candidate).Select(f => (f.Path, Hash: Convert.ToHexStringLower(SHA256.HashData(f.Content)))));
    }

    // 원본 불변: CompareDirectory는 예제 폴더의 파일을 바꾸지 않는다(목록·바이트·수정 시각).
    [Theory]
    [MemberData(nameof(Examples))]
    public void CompareDirectory_does_not_change_example_folder(string example)
    {
        var root = SyntheticInput.ExampleDirectory(example);
        (string, string, DateTime)[] Snapshot() => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(f => (Path.GetRelativePath(root, f), Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(f))), File.GetLastWriteTimeUtc(f)))
            .ToArray();
        var before = Snapshot();

        _ = CompareInput.Example(example);

        Assert.Equal(before, Snapshot());
    }
}
