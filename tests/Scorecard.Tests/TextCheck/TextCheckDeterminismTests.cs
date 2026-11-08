using Scorecard.Tests.Comparison;

namespace Scorecard.Tests.TextCheck;

// 스펙 4.6 결정론: 문화권(de-DE·tr-TR), 같은 입력 두 번, reply-drafts CSV 변형 7종(CRLF·CR·BOM·따옴표·끝 줄바꿈 없음·끝 빈 행·칸 앞뒤 공백).
// 어느 것이든 metrics.json·judgment.json 바이트가 7.4 expected와 같다. 평균 글·한글 문장이 들어가므로 문화권을 본다.
[Trait("Spec", "text-check")]
public class TextCheckDeterminismTests
{
    public static TheoryData<string> Cultures => new("de-DE", "tr-TR");

    public static TheoryData<string> Variants => new(TextCheckInput.CsvVariants(TextCheckInput.Bytes("a\n")).Keys);

    [Theory]
    [MemberData(nameof(Cultures))]
    public void Culture_does_not_change_bytes(string culture)
    {
        var output = CompareInput.WithCulture(culture, () => Output(TextCheckSpec.BaseFiles()));

        AssertExpected(output);
    }

    [Fact]
    public void Same_input_twice_gives_same_bytes()
    {
        var first = Output(TextCheckSpec.BaseFiles());
        var second = Output(TextCheckSpec.BaseFiles());

        Assert.Equal(first.Metrics, second.Metrics);
        Assert.Equal(first.Judgment, second.Judgment);
        AssertExpected(first);
    }

    // CSV 넷(reference·candidate·scores 둘)을 같은 변형으로 바꾼다.
    [Theory]
    [MemberData(nameof(Variants))]
    public void Csv_variant_gives_same_bytes(string variant)
    {
        var files = TextCheckSpec.BaseFiles().ToDictionary(
            f => f.Key,
            f => f.Key.EndsWith(".csv", StringComparison.Ordinal) ? TextCheckInput.CsvVariants(f.Value)[variant] : f.Value,
            StringComparer.Ordinal);
        Assert.Equal(4, files.Keys.Count(k => k.EndsWith(".csv", StringComparison.Ordinal)));

        AssertExpected(Output(files));
    }

    // 원본 불변: 돌린 뒤 입력 파일 바이트가 그대로다.
    [Fact]
    public void Input_files_are_not_changed()
    {
        using var folder = TextCheckFolder.Create(TextCheckSpec.BaseFiles());
        TextCheckInput.Valid(TextCheckInput.Run(folder.Root));

        Assert.All(TextCheckSpec.BaseFiles(), f => Assert.Equal(f.Value, File.ReadAllBytes(Path.Combine(folder.Root, f.Key))));
        Assert.Equal(
            TextCheckSpec.BaseFiles().Keys.Order(StringComparer.Ordinal),
            Directory.EnumerateFiles(folder.Root, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(folder.Root, f).Replace(Path.DirectorySeparatorChar, '/'))
                .Order(StringComparer.Ordinal));
    }

    private static (byte[] Metrics, byte[] Judgment) Output(IReadOnlyDictionary<string, byte[]> files)
    {
        using var folder = TextCheckFolder.Create(files);
        return TextCheckInput.Valid(TextCheckInput.Run(folder.Root)).Output!.Value;
    }

    private static void AssertExpected((byte[] Metrics, byte[] Judgment) output)
    {
        Assert.Equal(TextCheckInput.Text(TextCheckSpec.FileBlock("expected/metrics.json")), TextCheckInput.Text(output.Metrics));
        Assert.Equal(TextCheckSpec.FileBlock("expected/metrics.json"), output.Metrics);
        Assert.Equal(TextCheckSpec.FileBlock("expected/judgment.json"), output.Judgment);
    }
}
