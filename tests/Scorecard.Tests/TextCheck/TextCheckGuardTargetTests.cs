using System.Text;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;
using Scorecard.Tests.Guards;

namespace Scorecard.Tests.TextCheck;

// 스펙 4.6 G2·G3 등록. G2·G3 검사 자체는 CoreGuardTargetTests가 GuardTargets 전부에 돈다.
// 여기서는 등록 내용, 어댑터 출력 모양(metrics.json·judgment.json 두 파일), 표본 출력 = 7.4 expected(빈 통과 방지)를 본다.
[Trait("Spec", "text-check")]
public class TextCheckGuardTargetTests
{
    private const string Target = "text-check-case-valid";
    private const string Sample = "text-check-valid";

    [Fact]
    public void Target_is_registered_with_its_sample()
    {
        Assert.Equal(Sample, GuardTargets.Get(Target).Sample);
        Assert.True(Directory.Exists(GuardWorkspace.SamplePath(Sample)), $"표본 {Sample}이 없다.");
    }

    // 어댑터 출력은 judgment.json·metrics.json 두 파일. UTF-8(BOM 없음), LF, 끝에 \n. 작업 폴더 경로가 없다. 바이트 = 7.4 블록.
    [Fact]
    public void Adapter_writes_metrics_and_judgment_without_paths()
    {
        var target = GuardTargets.Get(Target);
        using var workspace = GuardWorkspace.Create(target.Sample, reverseCopy: false);
        var output = workspace.PathOf("out");

        target.Run(workspace.Input, output);

        var files = Directory.EnumerateFileSystemEntries(output, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(output, f))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal([JudgmentJson.FileName, MetricsJson.FileName], files);
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(Path.Combine(output, file));
            Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "BOM이 없어야 한다.");
            Assert.DoesNotContain((byte)'\r', bytes);
            Assert.Equal((byte)'\n', bytes[^1]);

            var text = Encoding.UTF8.GetString(bytes);
            Assert.DoesNotContain(workspace.Root, text, StringComparison.Ordinal);
            Assert.DoesNotContain(Path.GetFileName(Path.TrimEndingDirectorySeparator(workspace.Root)), text, StringComparison.Ordinal);
            Assert.Equal(TextCheckSpec.FileBlock("expected/" + file), bytes);
        }
    }
}
