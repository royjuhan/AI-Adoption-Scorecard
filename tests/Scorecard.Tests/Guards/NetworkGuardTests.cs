using System.Reflection.Metadata;

namespace Scorecard.Tests.Guards;

// T-002 G1: src/ 컴파일 결과에 네트워크·외부 프로세스·LLM·비결정 참조가 없다.
// (docs/spec/guard-tests.md 4.1~4.3, 승인 결정 1~6. N6 허용 목록·Y1은 docs/spec/case-schema.md 4.2, T-003 승인 결정 2·3)
// 텍스트가 아니라 테스트 출력 폴더의 dll 메타데이터를 본다. 그래서 테스트가 도는 구성(CI는 Release)만 검사한다.
// 외부 프로세스는 부르지 않는다. 파일만 읽는다.
[Trait("Rule", "G1")]
public class NetworkGuardTests
{
    private const string CoreDll = "Scorecard.Core.dll";
    private const string CliDll = "scorecard.dll";
    private const string TestsDll = "Scorecard.Tests.dll";

    // N6 허용 목록. 런타임 폴더에 없는 제3자 어셈블리 중 사람이 확인한 이름만 넣는다(T-002 승인 결정 3).
    // YamlDotNet: T-003 승인 결정 2. 패키지 18.1.0(정확히 고정), MIT, 의존 패키지 0개.
    // YamlDotNet.dll 안의 N5·D1·R1 항목은 G1 밖이다. Core가 쓰는 범위는 Y1이 묶는다.
    private static readonly string[] AllowedThirdPartyAssemblies = ["YamlDotNet"];

    // Core·Cli 공통 규칙. D1(승인 결정 5)과 R1, Y1(T-003 승인 결정 3)은 Core에만 적용한다.
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

    // R1: 판정 로직이 리플렉션으로 임의 메서드를 부르지 않는다. 목록 밖 경로로 N5·D1을 돌아가는 것을 막는다.
    [Fact]
    public void Core_has_no_reflection_invoke_references()
    {
        AssertNoFindings(CoreDll, "R1");
    }

    // Y1: Core는 YamlDotNet 타입 중 YamlDotNet.Core(하위 포함)만 참조한다.
    // 허용 목록이 YamlDotNet을 N6 밖에 두므로, 리플렉션 역직렬화(Serialization) 같은 다른 범위는 이 규칙이 막는다.
    [Fact]
    public void Core_uses_only_YamlDotNet_Core_namespaces()
    {
        AssertNoFindings(CoreDll, "Y1");
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
    [InlineData("R1")]
    [InlineData("Y1")]
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

    // Y1 허용 범위(YamlDotNet.Core와 하위)는 잡지 않는다. 카나리아의 YamlCore()·YamlCoreEvents()가 참조한다.
    // TypeRef가 실제로 dll에 있는지 먼저 본다. 빈 통과를 막는다.
    [Theory]
    [InlineData("YamlDotNet.Core.Parser")]
    [InlineData("YamlDotNet.Core.Events.Scalar")]
    public void Scanner_ignores_yaml_core_types(string typeName)
    {
        Assert.Contains(typeName, MetadataScanner.ReadTypeReferences(Path.Combine(AppContext.BaseDirectory, TestsDll)));
        Assert.DoesNotContain("Y1 type " + typeName, Scan(TestsDll));
    }

    // Y1 판정 순수 함수. 경계: 이름 접두어가 같아도 '.' 경계가 아니면 다른 네임스페이스다. 서수 비교.
    [Theory]
    [InlineData("YamlDotNet", true)]
    [InlineData("YamlDotNet.Serialization", true)]
    [InlineData("YamlDotNet.Serialization.NamingConventions", true)]
    [InlineData("YamlDotNet.RepresentationModel", true)]
    [InlineData("YamlDotNet.Helpers", true)]
    [InlineData("YamlDotNet.CoreX", true)]
    [InlineData("YamlDotNet.Core", false)]
    [InlineData("YamlDotNet.Core.Events", false)]
    [InlineData("YamlDotNetX", false)]
    [InlineData("yamldotnet.Serialization", false)]
    [InlineData("Scorecard.Core.Cases", false)]
    [InlineData("", false)]
    public void IsDeniedYamlNamespace_allows_only_core_namespaces(string ns, bool denied)
    {
        Assert.Equal(denied, MetadataScanner.IsDeniedYamlNamespace(ns));
    }

    // 허용 목록의 "YamlDotNet"이 실제 어셈블리 이름과 서수로 같은지 본다. 철자가 틀리면 목록이 아무것도 빼지 못한다.
    // Scorecard.Tests.dll은 카나리아 때문에 YamlDotNet을 참조한다.
    [Fact]
    public void Allow_list_entry_matches_real_YamlDotNet_assembly_name()
    {
        var path = Path.Combine(AppContext.BaseDirectory, TestsDll);

        Assert.Contains("N6 non-framework asmref YamlDotNet", MetadataScanner.Scan(path, []));
        Assert.DoesNotContain("N6 non-framework asmref YamlDotNet", Scan(TestsDll));
    }

    // 금지 멤버와 이름이 가까운 허용 멤버는 잡지 않는다. 카나리아의 AllowedNearDenied()가 참조한다.
    // 키가 실제로 dll에 있는지 먼저 본다. 빈 통과를 막는다.
    [Theory]
    [InlineData("System.Func`1::Invoke")]
    [InlineData("System.TimeZoneInfo::get_Utc")]
    [InlineData("System.IO.Path::GetFileName")]
    public void Scanner_ignores_allowed_member_next_to_denied_name(string key)
    {
        var keys = MetadataScanner.ReadMemberReferences(Path.Combine(AppContext.BaseDirectory, TestsDll)).Select(m => m.Key);

        Assert.Contains(key, keys);
        Assert.DoesNotContain(Scan(TestsDll), f => f.EndsWith(" member " + key, StringComparison.Ordinal));
    }

    // 하위 타입 이름으로 부른 금지 멤버(스펙 4.2 N5·R1). 카나리아는 일부러 하위 타입으로 부른다.
    // 메타데이터에 하위 타입 키가 남으면 목록이 놓친다. 하위 타입 키는 없고, 선언 타입 키가 잡히는지 본다.
    // 컴파일러가 바뀌어 이 테스트가 실패하면 하위 타입 키를 목록에 더한다.
    [Theory]
    [InlineData("System.Reflection.MethodInfo::Invoke", "System.Reflection.MethodBase::Invoke")]
    [InlineData("System.Reflection.TypeInfo::InvokeMember", "System.Type::InvokeMember")]
    [InlineData("System.Reflection.Emit.AssemblyBuilder::LoadWithPartialName", "System.Reflection.Assembly::LoadWithPartialName")]
    public void Subclass_call_leaves_declaring_type_key(string subclassKey, string declaringKey)
    {
        var keys = MetadataScanner.ReadMemberReferences(Path.Combine(AppContext.BaseDirectory, TestsDll)).Select(m => m.Key);

        Assert.DoesNotContain(subclassKey, keys);
        Assert.Contains(Scan(TestsDll), f => f.EndsWith(" member " + declaringKey, StringComparison.Ordinal));
    }

    // N5 예외가 실제 dll에서 갈리는지 본다. 카나리아에는 같은 이름의 MemberRef가 서명만 달리 둘 있다.
    // 결과 문자열("N5 member ...")로는 둘이 구분되지 않는다. 그래서 MemberRef 단위로 본다.
    // 예외 서명(인스턴스 GetType(), 제네릭 CreateInstance<T>())은 허용, 나머지(정적 문자열 오버로드)는 금지여야 한다.
    [Theory]
    [InlineData("System.Type::GetType", SignatureAttributes.Instance)]
    [InlineData("System.Activator::CreateInstance", SignatureAttributes.Generic)]
    public void Scanner_allows_only_exception_signature_of_canary_member(string key, SignatureAttributes exception)
    {
        var members = MetadataScanner.ReadMemberReferences(Path.Combine(AppContext.BaseDirectory, TestsDll))
            .Where(m => m.Key == key)
            .ToArray();
        var allowed = members.Where(m => (m.Header.Attributes & exception) != 0).ToArray();
        var denied = members.Where(m => (m.Header.Attributes & exception) == 0).ToArray();

        // 카나리아가 두 서명을 모두 남겼는지 먼저 본다. 빈 통과를 막는다.
        Assert.NotEmpty(allowed);
        Assert.NotEmpty(denied);
        Assert.All(allowed, m => Assert.False(MetadataScanner.IsDeniedMember(m.Key, m.Header), $"{m.Key} {m.Header}"));
        Assert.All(denied, m => Assert.True(MetadataScanner.IsDeniedMember(m.Key, m.Header), $"{m.Key} {m.Header}"));
        Assert.Contains($"N5 member {key}", Scan(TestsDll));
    }

    // N5 판정 순수 함수(스펙 4.2 N5). 예외는 둘이고 각각 그 이름에만 적용된다.
    [Theory]
    [InlineData("System.Activator::CreateInstance", SignatureAttributes.Generic, false)]
    [InlineData("System.Activator::CreateInstance", SignatureAttributes.None, true)]
    [InlineData("System.Activator::CreateInstance", SignatureAttributes.Instance, true)]
    [InlineData("System.Type::GetType", SignatureAttributes.Instance, false)]
    [InlineData("System.Type::GetType", SignatureAttributes.None, true)]
    [InlineData("System.Type::GetType", SignatureAttributes.Generic, true)]
    [InlineData("System.Reflection.Assembly::GetType", SignatureAttributes.Instance, true)]
    [InlineData("System.Reflection.Module::GetType", SignatureAttributes.Instance, true)]
    [InlineData("System.Object::GetType", SignatureAttributes.Instance, false)]
    [InlineData("System.type::GetType", SignatureAttributes.None, false)]
    public void IsDeniedMember_applies_N5_exceptions(string key, SignatureAttributes attributes, bool denied)
    {
        var header = new SignatureHeader(SignatureKind.Method, SignatureCallingConvention.Default, attributes);

        Assert.Equal(denied, MetadataScanner.IsDeniedMember(key, header));
    }

    // 예외 표지가 없는 서명이면 목록의 모든 멤버가 금지다. 예외가 다른 이름을 삼키지 않는지 본다.
    [Fact]
    public void IsDeniedMember_denies_every_listed_member_without_exception_flags()
    {
        var header = new SignatureHeader(SignatureKind.Method, SignatureCallingConvention.Default, SignatureAttributes.None);

        Assert.All(MetadataScanner.DeniedMembers, m => Assert.True(MetadataScanner.IsDeniedMember(m, header), m));
    }

    // N6 프레임워크 판정은 서수다(스펙 4.2). Windows File.Exists처럼 대소문자를 무시하면 안 된다.
    [Theory]
    [InlineData("System.Runtime", true)]
    [InlineData("System.Reflection.Metadata", true)]
    [InlineData("system.runtime", false)]
    [InlineData("SYSTEM.RUNTIME", false)]
    [InlineData("xunit.core", false)]
    public void Framework_check_compares_runtime_dll_names_ordinally(string name, bool framework)
    {
        Assert.Equal(framework, MetadataScanner.IsFrameworkAssembly(name));
    }

    // 런타임 폴더 이름 집합을 만드는 규칙. 합성 폴더로 본다(ADR 0002).
    // ".dll"로 서수 끝나는 바로 아래 파일만 든다. ".DLL" 파일, "x.dll" 폴더, 하위 폴더 파일은 빠진다.
    [Fact]
    public void Assembly_names_include_only_direct_files_ending_with_dll_ordinally()
    {
        var root = Directory.CreateTempSubdirectory("scorecard-n6-").FullName;
        try
        {
            File.WriteAllBytes(Path.Combine(root, "Fake.Sdk.dll"), []);
            File.WriteAllBytes(Path.Combine(root, "Upper.DLL"), []);
            File.WriteAllBytes(Path.Combine(root, "notes.txt"), []);
            Directory.CreateDirectory(Path.Combine(root, "Folder.dll"));
            Directory.CreateDirectory(Path.Combine(root, "sub"));
            File.WriteAllBytes(Path.Combine(root, "sub", "Nested.dll"), []);

            var names = MetadataScanner.AssemblyNamesIn(root);

            Assert.Equal("Fake.Sdk", Assert.Single(names));
            Assert.DoesNotContain("fake.sdk", names);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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

    // 카나리아에서 나와야 하는 항목. 목록형 규칙(N2·N3·N5·D1·R1)은 검사기 목록에서 만든다.
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
        "R1" => MetadataScanner.ReflectionInvokeMembers.Select(m => new Expected($"R1 member {m}")),
        "Y1" =>
        [
            new("Y1 type YamlDotNet.Serialization.DeserializerBuilder"),
            new("Y1 type YamlDotNet.RepresentationModel.YamlStream"),
        ],
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
