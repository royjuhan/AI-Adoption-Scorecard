using System.Text;
using System.Text.Json;

namespace Scorecard.Tests.Guards;

// T-003: 실제 Core 대상(GuardTargets)의 G2 결정론. 차이가 없어야 한다(T-002 승인 결정 7).
// 빈 출력 규칙(스펙 4.11)이 함께 걸리므로, 어댑터가 파일을 쓰지 않으면 여기서 실패한다.
[Trait("Rule", "G2")]
public class CoreDeterminismGuardTests
{
    [Theory]
    [MemberData(nameof(GuardTargets.Names), MemberType = typeof(GuardTargets))]
    public void Core_target_is_deterministic(string name)
    {
        Assert.Empty(GuardChecks.Determinism(GuardTargets.Get(name)));
    }
}

// T-003: 실제 Core 대상(GuardTargets)의 G3 원본 불변. 작업 폴더가 그대로여야 한다.
[Trait("Rule", "G3")]
public class CoreImmutabilityGuardTests
{
    [Theory]
    [MemberData(nameof(GuardTargets.Names), MemberType = typeof(GuardTargets))]
    public void Core_target_leaves_input_unchanged(string name)
    {
        Assert.Empty(GuardChecks.Immutability(GuardTargets.Get(name)));
    }
}

// 스펙 4.11 어댑터 모양: 출력은 parse-result.json 한 파일. { "case", "errors" }, UTF-8(BOM 없음), LF, 끝에 \n.
// 정상 표본은 모델이 있고 오류가 없다. 오류 표본은 모델이 없고 오류 줄이 있다(빈 통과 방지).
[Trait("Rule", "G2")]
public class GuardTargetAdapterTests
{
    [Theory]
    [InlineData("case-parser-valid", true)]
    [InlineData("case-parser-invalid", false)]
    public void Adapter_writes_single_json_result_file(string name, bool valid)
    {
        var target = GuardTargets.Get(name);
        using var workspace = GuardWorkspace.Create(target.Sample, reverseCopy: false);
        var output = workspace.PathOf("out");

        target.Run(workspace.Input, output);

        var files = Directory.EnumerateFileSystemEntries(output, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(output, f))
            .ToArray();
        Assert.Equal([GuardTargets.ResultFile], files);

        var bytes = File.ReadAllBytes(Path.Combine(output, GuardTargets.ResultFile));
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "BOM이 없어야 한다.");
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal((byte)'\n', bytes[^1]);

        using var json = JsonDocument.Parse(bytes);
        var root = json.RootElement;
        Assert.Equal(["case", "errors"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal(valid ? JsonValueKind.Object : JsonValueKind.Null, root.GetProperty("case").ValueKind);
        Assert.Equal(valid, root.GetProperty("errors").GetArrayLength() == 0);
        Assert.DoesNotContain(workspace.Root, Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    // guard/basic은 T-002 가짜 대상의 바이트 샘플이라 그대로 둔다(스펙 4.11). 실제 대상은 쓰지 않는다.
    [Fact]
    public void Real_targets_use_case_samples_not_basic()
    {
        Assert.Equal(["case-invalid", "case-valid"], GuardTargets.All.Select(t => t.Sample).Order(StringComparer.Ordinal));
    }
}
