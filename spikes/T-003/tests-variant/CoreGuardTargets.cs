using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Scorecard.Core.Cases;

namespace Scorecard.Tests.Guards;

// 스파이크(T-003 S5): 실제 Core 대상을 G2·G3에 등록하는 모양. 머지 대상 아님.
// 어댑터는 파서 결과를 출력 폴더의 JSON 파일 하나로 쓴다. 직렬화는 테스트 쪽 System.Text.Json.
internal static class GuardTargets
{
    public const string ResultFile = "parse-result.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static readonly GuardTarget[] All =
    [
        new("case-parser-valid", "case-valid", ParseToJson),
        new("case-parser-invalid", "case-invalid", ParseToJson),
    ];

    public static TheoryData<string> Names => new(All.Select(t => t.Name));

    public static GuardTarget Get(string name) => All.Single(t => t.Name == name);

    private static void ParseToJson(string input, string output)
    {
        var result = CaseParser.ParseDirectory(input);
        var payload = new { @case = result.Case, errors = result.Errors.Select(e => e.ToString()).ToArray() };
        Directory.CreateDirectory(output);
        File.WriteAllBytes(
            Path.Combine(output, ResultFile),
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions) + "\n"));
    }
}

[Trait("Rule", "G2")]
public class CoreDeterminismSpikeTests
{
    [Theory]
    [MemberData(nameof(GuardTargets.Names), MemberType = typeof(GuardTargets))]
    public void Core_target_is_deterministic(string name)
    {
        Assert.Empty(GuardChecks.Determinism(GuardTargets.Get(name)));
    }
}

[Trait("Rule", "G3")]
public class CoreImmutabilitySpikeTests
{
    [Theory]
    [MemberData(nameof(GuardTargets.Names), MemberType = typeof(GuardTargets))]
    public void Core_target_leaves_input_unchanged(string name)
    {
        Assert.Empty(GuardChecks.Immutability(GuardTargets.Get(name)));
    }
}
