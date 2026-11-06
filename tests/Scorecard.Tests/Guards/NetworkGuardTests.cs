namespace Scorecard.Tests.Guards;

// T-002 G1: src/ 컴파일 결과에 네트워크·외부 프로세스·LLM·비결정 참조가 없다.
// (docs/spec/guard-tests.md 4.1~4.3, 승인 결정 1~6)
// 텍스트가 아니라 테스트 출력 폴더의 dll 메타데이터를 본다. 그래서 테스트가 도는 구성(CI는 Release)만 검사한다.
// 외부 프로세스는 부르지 않는다. 파일만 읽는다.
[Trait("Rule", "G1")]
public class NetworkGuardTests
{
    private const string CoreDll = "Scorecard.Core.dll";
    private const string CliDll = "scorecard.dll";
    private const string TestsDll = "Scorecard.Tests.dll";

    // N6 허용 목록. 런타임 폴더에 없는 제3자 어셈블리 중 사람이 확인한 이름만 넣는다.
    // 지금은 비어 있다(승인 결정 3). 패키지를 처음 쓰는 카드에서 test-engineer가 더하고 PR 본문에 적는다.
    private static readonly string[] AllowedThirdPartyAssemblies = [];

    // Core·Cli 공통 규칙. D1은 Core에만 적용한다(승인 결정 5).
    private static readonly string[] NetworkRules = ["N1", "N2", "N3", "N4", "N5", "N6"];

    [Theory]
    [InlineData(CoreDll)]
    [InlineData(CliDll)]
    public void Guarded_assembly_exists_in_test_output(string fileName)
    {
        // 빈 통과를 막는다. Windows의 File.Exists는 대소문자를 무시하므로 이름 목록과 서수 비교한다.
        var names = Directory.EnumerateFiles(AppContext.BaseDirectory).Select(f => Path.GetFileName(f));

        Assert.Contains(fileName, names);
    }

    [Fact]
    public void Core_has_no_network_process_or_unlisted_references()
    {
        AssertNoFindings(CoreDll, NetworkRules);
    }

    [Fact]
    public void Cli_has_no_network_process_or_unlisted_references()
    {
        AssertNoFindings(CliDll, NetworkRules);
    }

    [Fact]
    public void Core_has_no_clock_random_or_environment_references()
    {
        AssertNoFindings(CoreDll, "D1");
    }

    // 자기 검사(스펙 4.3). 같은 검사기로 Scorecard.Tests.dll의 ScannerCanary를 잡는지 본다.
    // 검사기가 망가져 늘 0건을 내는 경우를 막는다. 기대 항목이 모두 있는지만 본다.
    [Theory]
    [InlineData("N1")]
    [InlineData("N2")]
    [InlineData("N3")]
    [InlineData("N4")]
    [InlineData("N5")]
    [InlineData("N6")]
    [InlineData("D1")]
    public void Scanner_catches_canary_for_rule(string rule)
    {
        var findings = Scan(TestsDll);

        var missing = ExpectedCanaryFindings(rule)
            .Where(expected => !findings.Any(expected.Matches))
            .Select(expected => expected.ToString())
            .ToArray();

        if (missing.Length > 0)
        {
            Assert.Fail($"검사기가 {TestsDll}의 카나리아에서 {rule} 항목 {missing.Length}건을 놓쳤다:\n  "
                + string.Join("\n  ", missing));
        }
    }

    // 허용 API(스펙 4.1 V14)는 잡지 않는다. 카나리아의 Allowed()가 참조한다.
    [Theory]
    [InlineData("System.Net.WebUtility")]
    [InlineData("System.Uri")]
    public void Scanner_ignores_allowed_api(string typeName)
    {
        var findings = Scan(TestsDll);

        Assert.DoesNotContain(findings, f => f.Contains(typeName, StringComparison.Ordinal));
    }

    // N6 허용 목록이 동작하는지 본다. 목록에 넣은 이름만 빠진다.
    [Fact]
    public void Allow_list_removes_only_listed_assembly_from_N6()
    {
        var path = Path.Combine(AppContext.BaseDirectory, TestsDll);

        var findings = MetadataScanner.Scan(path, ["xunit.core"]);

        Assert.DoesNotContain("N6 non-framework asmref xunit.core", findings);
        Assert.Contains("N6 non-framework asmref xunit.assert", findings);
    }

    private static IReadOnlyList<string> Scan(string fileName) =>
        MetadataScanner.Scan(Path.Combine(AppContext.BaseDirectory, fileName), AllowedThirdPartyAssemblies);

    // 실패 메시지 형식은 스펙 6.1: 첫 줄에 dll 이름과 건수, 다음 줄부터 항목 하나씩.
    private static void AssertNoFindings(string fileName, params string[] rules)
    {
        var findings = MetadataScanner.OfRules(Scan(fileName), rules);

        if (findings.Count > 0)
        {
            Assert.Fail($"{fileName}에 금지 참조 {findings.Count}건:\n  " + string.Join("\n  ", findings));
        }
    }

    // 카나리아에서 나와야 하는 항목. 목록형 규칙(N2·N3·N5·D1)은 검사기 목록에서 만든다.
    // 그래서 목록에 이름을 더하고 카나리아에 참조를 더하지 않으면 이 테스트가 실패한다.
    private static IEnumerable<Expected> ExpectedCanaryFindings(string rule) => rule switch
    {
        "N1" =>
        [
            new("N1 asmref System.Net.Http"),
            new("N1 asmref System.Net.Mail"),
            new("N1 asmref System.Net.Ping"),
            new("N1 asmref System.Net.Quic"),
            new("N1 asmref System.Net.Security"),
            new("N1 asmref System.Net.Sockets"),
            new("N1 asmref System.Net.WebClient"),
            new("N1 asmref System.Net.WebSockets.Client"),
        ],
        "N2" => MetadataScanner.DeniedAssemblies.Select(a => new Expected($"N2 asmref {a}")),
        "N3" => MetadataScanner.DeniedTypes.Select(t => new Expected($"N3 type {t}"))
            .Concat(MetadataScanner.DeniedNamespaces.Select(n => new Expected($"N3 namespace {n}.", Prefix: true))),
        "N4" =>
        [
            new("N4 moduleref libc"),
            new("N4 pinvoke GetPid -> libc!getpid"),
        ],
        "N5" => MetadataScanner.DeniedMembers.Select(m => new Expected($"N5 member {m}")),
        "N6" =>
        [
            new("N6 non-framework asmref xunit.assert"),
            new("N6 non-framework asmref xunit.core"),
        ],
        "D1" => MetadataScanner.DeterminismTypes.Select(t => new Expected($"D1 type {t}"))
            .Concat(MetadataScanner.DeterminismMembers.Select(m => new Expected($"D1 member {m}"))),
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "알 수 없는 규칙"),
    };

    // 기대 항목 하나. Prefix면 그 문자열로 시작하는 항목이 하나 이상 있으면 된다(N3 네임스페이스).
    private sealed record Expected(string Text, bool Prefix = false)
    {
        public bool Matches(string finding) =>
            Prefix ? finding.StartsWith(Text, StringComparison.Ordinal) : finding == Text;

        public override string ToString() => Prefix ? Text + "*" : Text;
    }
}
