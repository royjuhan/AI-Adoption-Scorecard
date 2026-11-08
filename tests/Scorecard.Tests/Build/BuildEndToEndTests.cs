using System.Globalization;
using System.Text.Json;
using Scorecard.Tests.Cli;
using Scorecard.Tests.Synthetic;

namespace Scorecard.Tests.Build;

// 지시문 10절 M2 G2: 공개 예제마다 start --answers → check → open → build까지 end-to-end(사람용과 --json 둘 다).
// build-request.md 6절. 골든은 tests TestData/build/<예제>/.
[Trait("Spec", "build-request")]
public class BuildEndToEndTests
{
    public static TheoryData<string> Examples => CliInput.AllExamples;

    // --json: 추천(choice.json) → --step 확인 전(2, 초안만) → 사람이 확인 줄을 적음 → --step(0, build.json + build-request/ 골든).
    [Theory]
    [MemberData(nameof(Examples))]
    public void Json_flow_from_start_to_build_matches_golden(string example)
    {
        using var work = CliWorkspace.Create(example);
        var output = BuildInput.Prepare(work, work.PathOf("o"));
        var step = BuildInput.ProposedStep(example).ToString(CultureInfo.InvariantCulture);

        var choice = CliInput.Run("build", work.Case, "--out", output, "--json");
        Assert.Equal(0, choice.Exit);
        Assert.Empty(choice.Stderr);
        BuildInput.AssertGolden(example, "choice.json", choice.Stdout);
        Assert.False(File.Exists(Path.Combine(output, "export-check.md")), "추천만 하면 아무것도 쓰지 않는다.");

        var unsigned = CliInput.Run("build", work.Case, "--step", step, "--out", output, "--json");
        Assert.Equal(2, unsigned.Exit);
        Assert.Empty(unsigned.Stderr);
        using (var errors = JsonDocument.Parse(unsigned.Stdout))
        {
            var error = Assert.Single(errors.RootElement.GetProperty("errors").EnumerateArray());
            Assert.Equal("export-check", error.GetProperty("code").GetString());
            Assert.Equal("export-check.md", error.GetProperty("source").GetString());
        }

        Assert.True(File.Exists(Path.Combine(output, "export-check.md")), "확인 전이면 초안을 쓴다.");
        Assert.False(Directory.Exists(Path.Combine(output, "build-request")), "확인 전이면 번들을 내보내지 않는다.");

        BuildInput.Sign(output);
        var signedBytes = File.ReadAllBytes(Path.Combine(output, "export-check.md"));
        var built = CliInput.Run("build", work.Case, "--step", step, "--out", output, "--json");
        Assert.Equal(0, built.Exit);
        Assert.Empty(built.Stderr);
        BuildInput.AssertGolden(example, "build.json", built.Stdout);

        var bundle = BuildInput.Files(Path.Combine(output, "build-request"));
        Assert.Equal(signedBytes, bundle["export-check.md"]);
        var golden = BuildInput.GoldenPath(example, "build-request");
        if (!BuildInput.Updating)
        {
            Assert.Equal(BuildInput.Files(golden).Keys, bundle.Keys);
        }

        foreach (var (path, bytes) in bundle)
        {
            BuildInput.AssertGolden(example, "build-request/" + path, bytes);
        }

        // 결과 객체 files = 쓴 파일(출력 폴더 기준, 3.2 순서).
        using var result = JsonDocument.Parse(built.Stdout);
        var files = result.RootElement.GetProperty("files").EnumerateArray().Select(f => f.GetString()!).ToArray();
        Assert.Equal(bundle.Keys.Select(k => "build-request/" + k).Order(StringComparer.Ordinal), files.Order(StringComparer.Ordinal));
    }

    // 사람용(--json 없음): 같은 흐름, 같은 번들 바이트. 추천 안내에 --step 명령이 있고, 확인 전 오류는 표준 오류에 쓴다.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Human_flow_writes_the_same_bundle(string example)
    {
        using var work = CliWorkspace.Create(example);
        var output = BuildInput.Prepare(work, work.PathOf("o"), json: false);
        var step = BuildInput.ProposedStep(example).ToString(CultureInfo.InvariantCulture);

        var choice = CliInput.Run("build", work.Case, "--out", output);
        Assert.Equal(0, choice.Exit);
        Assert.Contains("--step " + step + "\n", choice.Out, StringComparison.Ordinal);
        Assert.Contains("이 단계 만들까요?", choice.Out, StringComparison.Ordinal);

        var unsigned = CliInput.Run("build", work.Case, "--step", step, "--out", output);
        Assert.Equal(2, unsigned.Exit);
        Assert.Empty(unsigned.Stdout);
        Assert.StartsWith("export-check.md: export-check: ", unsigned.Err, StringComparison.Ordinal);

        BuildInput.Sign(output);
        var built = CliInput.Run("build", work.Case, "--step", step, "--out", output);
        Assert.Equal(0, built.Exit);
        Assert.Empty(built.Stderr);
        Assert.Contains("(확인: " + BuildInput.Signer + ")", built.Out, StringComparison.Ordinal);
        Assert.Contains("/build-request/spec.md\n", built.Out, StringComparison.Ordinal);

        var bundle = BuildInput.Files(Path.Combine(output, "build-request"));
        var golden = BuildInput.Files(BuildInput.GoldenPath(example, "build-request"));
        Assert.Equal(golden.Keys, bundle.Keys);
        Assert.All(golden, g => Assert.Equal(g.Value, bundle[g.Key]));
    }

    // 골든을 다시 쓰는 중이면 실패한다. 다시 쓴 골든은 사람이 diff로 본 뒤 변수 없이 돌린다.
    [Fact]
    public void Golden_update_mode_is_off() =>
        Assert.False(BuildInput.Updating, $"{BuildInput.UpdateVariable}=1이다. 골든 diff를 보고 변수 없이 다시 돌린다.");

    // 지시문 2절 8·10: 공개 예제 번들 골든(문서·샘플)에 실제 값 패턴이 없다. 패턴은 synthetic-examples.md 4.10 표(테스트 쪽 정규식).
    [Fact]
    public void Example_bundles_have_no_real_value_patterns()
    {
        var root = BuildInput.GoldenPath(string.Empty, string.Empty).TrimEnd('/');
        Assert.True(Directory.Exists(root), "골든 폴더가 있어야 한다.");
        var hits = ExamplePatternTests.Scan(ExamplePatternTests.Patterns(), root, root)
            .Where(h => !h.Contains("export-check.md", StringComparison.Ordinal))
            .ToArray();

        Assert.True(hits.Length == 0, "번들에 실제 값 패턴이 있다:\n" + string.Join("\n", hits));
    }
}
