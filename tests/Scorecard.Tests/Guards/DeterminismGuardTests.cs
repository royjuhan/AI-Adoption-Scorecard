namespace Scorecard.Tests.Guards;

// T-002 G2: 같은 입력을 여러 번 처리하면 같은 출력이 나온다.
// (docs/spec/guard-tests.md 4.4~4.6, 승인 결정 7·9. 빈 출력 규칙은 docs/spec/case-schema.md 4.11)
// 이 파일은 가짜 대상으로 도우미(GuardChecks.Determinism)의 탐지력을 본다.
// 실제 Core 대상(GuardTargets)은 CoreGuardTargetTests에서 Assert.Empty로 검사한다(T-003).
[Trait("Rule", "G2")]
public class DeterminismGuardTests
{
    private static readonly string[] ComparedRuns = ["R2", "R3", "R4"];

    [Fact]
    public void Good_target_has_no_differences()
    {
        Assert.Empty(GuardChecks.Determinism(FakeTargets.Get("good")));
    }

    // expectedRuns: R1과 달라야 하는 실행(공백 구분). 나머지 실행은 R1과 같아야 한다(스펙 4.6).
    [Theory]
    [InlineData("clock-utcnow", "R2 R3 R4")]
    [InlineData("guid", "R2 R3 R4")]
    [InlineData("abs-path", "R3")]
    [InlineData("culture-format", "R4")]
    public void Fake_target_differs_only_in_expected_runs(string fake, string expectedRuns)
    {
        var differences = GuardChecks.Determinism(FakeTargets.Get(fake));

        var differingRuns = ComparedRuns
            .Where(run => differences.Any(d => d.StartsWith($"G2 {run} ", StringComparison.Ordinal)))
            .ToArray();

        Assert.Equal(expectedRuns, string.Join(' ', differingRuns));

        // 예외·전제 실패 줄이 섞이면 탐지가 아니라 오류다.
        Assert.All(differences, d => Assert.DoesNotContain(": 예외 ", d, StringComparison.Ordinal));
        Assert.All(differences, d => Assert.Matches("^G2 R[234] ", d));
    }

    // 빈 출력 규칙(T-003 스펙 4.11, T-002 reviewer 지적 7). 출력이 없거나 0바이트뿐이면 이 줄 하나만 낸다.
    // R2~R4는 빈 출력끼리 같아서 차이가 없다.
    [Theory]
    [InlineData("no-output")]
    [InlineData("empty-file")]
    public void Fake_target_without_output_is_reported(string fake)
    {
        Assert.Equal([GuardChecks.NoOutput], GuardChecks.Determinism(FakeTargets.Get(fake)));
    }

    // R4 전제: de-DE 소수점이 ','다. 아니면 R4가 문화권 의존을 못 잡는다(스펙 4.5).
    [Fact]
    public void German_culture_uses_comma_decimal_separator()
    {
        Assert.Null(GuardChecks.GermanCulturePrecondition());
    }
}
