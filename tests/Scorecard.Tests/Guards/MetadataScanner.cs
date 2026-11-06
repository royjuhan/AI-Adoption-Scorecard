using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace Scorecard.Tests.Guards;

// G1 검사기. docs/spec/guard-tests.md 4.1·4.2.
// 컴파일된 dll의 메타데이터 표만 읽는다. 어셈블리를 로드하지 않는다(PEReader).
// 읽는 표: AssemblyRef, TypeRef, MemberRef, MethodDef(PinvokeImpl), ModuleRef.
// 결과는 "N1 asmref System.Net.Http" 같은 문자열 목록이다. 서수 정렬, 중복 없음.
// 이름은 모두 서수(대소문자 구분)로 비교한다.
internal static class MetadataScanner
{
    // N2: 금지 어셈블리 참조. (N1은 System.Net 접두어로 따로 본다.)
    internal static readonly string[] DeniedAssemblies =
    [
        "System.Diagnostics.Process",
        "System.Runtime.Loader",
    ];

    // N3: 금지 타입(전체 이름).
    internal static readonly string[] DeniedTypes =
    [
        "System.Net.Http.HttpClient",
        "System.Net.Sockets.Socket",
        "System.Net.WebClient",
        "System.Diagnostics.Process",
        "System.Diagnostics.ProcessStartInfo",
        "System.Runtime.InteropServices.NativeLibrary",
    ];

    // N3: 금지 네임스페이스. 같거나 하위면 걸린다.
    internal static readonly string[] DeniedNamespaces =
    [
        "System.Net.Http",
        "System.Net.Sockets",
        "System.Net.WebSockets",
        "System.Net.Mail",
        "System.Net.NetworkInformation",
        "System.Net.Quic",
        "System.Net.Security",
        "System.Runtime.Loader",
    ];

    // N5: 문자열로 타입·어셈블리를 찾거나 만드는 멤버. "부모 타입::멤버 이름".
    internal static readonly string[] DeniedMembers =
    [
        "System.Type::GetType",
        "System.Type::GetTypeFromProgID",
        "System.Type::GetTypeFromCLSID",
        "System.Reflection.Assembly::Load",
        "System.Reflection.Assembly::LoadFrom",
        "System.Reflection.Assembly::LoadFile",
        "System.Reflection.Assembly::UnsafeLoadFrom",
        "System.Reflection.Assembly::GetType",
        "System.Reflection.Module::GetType",
        "System.AppDomain::Load",
        ActivatorCreateInstance,
        "System.Runtime.InteropServices.Marshal::GetDelegateForFunctionPointer",
    ];

    // D1: 난수·현재시각·실행 환경 타입. Core에만 적용한다(적용은 테스트가 고른다).
    internal static readonly string[] DeterminismTypes =
    [
        "System.Random",
        "System.Security.Cryptography.RandomNumberGenerator",
        "System.Diagnostics.Stopwatch",
    ];

    // D1: 난수·현재시각·실행 환경 멤버.
    internal static readonly string[] DeterminismMembers =
    [
        "System.DateTime::get_Now",
        "System.DateTime::get_UtcNow",
        "System.DateTime::get_Today",
        "System.DateTimeOffset::get_Now",
        "System.DateTimeOffset::get_UtcNow",
        "System.Guid::NewGuid",
        "System.Guid::CreateVersion7",
        "System.Environment::get_TickCount",
        "System.Environment::get_TickCount64",
        "System.Environment::get_MachineName",
        "System.Environment::get_UserName",
        "System.Environment::GetEnvironmentVariable",
        "System.Environment::GetEnvironmentVariables",
        "System.TimeProvider::get_System",
    ];

    // N5 예외 1. 제네릭 CreateInstance<T>()는 new T()가 만든다. 이것은 허용한다(스펙 4.2 N5).
    private const string ActivatorCreateInstance = "System.Activator::CreateInstance";

    // N5 예외 2. Type에는 인스턴스 GetType()도 있다(자기 타입을 돌려줄 뿐). 정적 Type.GetType(string...)만 금지한다.
    private const string TypeGetType = "System.Type::GetType";

    // N6 프레임워크 판정용. 런타임 폴더의 dll 이름을 한 번만 읽어 서수 집합에 담는다.
    // File.Exists는 Windows에서 대소문자를 무시한다. 스펙 4.2 "서수 비교"를 OS와 무관하게 지키려고 집합으로 비교한다.
    private static readonly Lazy<IReadOnlySet<string>> RuntimeAssemblyNames =
        new(() => AssemblyNamesIn(RuntimeEnvironment.GetRuntimeDirectory()));

    // dll 하나를 검사한다. allowedAssemblies는 N6 허용 목록(제3자 어셈블리 이름).
    // 파일이 없거나 메타데이터가 없으면 예외를 던진다. 빈 통과를 막는다.
    public static IReadOnlyList<string> Scan(string dllPath, IEnumerable<string> allowedAssemblies)
    {
        var allowed = new HashSet<string>(allowedAssemblies, StringComparer.Ordinal);

        return Read(dllPath, md =>
        {
            var findings = new SortedSet<string>(StringComparer.Ordinal);
            ScanAssemblyReferences(md, allowed, findings);
            ScanTypeReferences(md, findings);
            ScanMemberReferences(md, findings);
            ScanPlatformInvoke(md, findings);
            return findings.ToArray();
        });
    }

    // dll의 MemberRef를 서명 머리와 함께 모두 돌려준다. 순서는 표 순서.
    // 같은 이름이 서명만 달리 여럿일 수 있다. 문자열 결과로는 구분되지 않아 자기 검사가 이것을 본다.
    internal static IReadOnlyList<MemberReferenceInfo> ReadMemberReferences(string dllPath) =>
        Read(dllPath, md => MemberReferences(md).ToArray());

    // 규칙 ID(N1~N6, D1)만 고른다. 항목 문자열은 "규칙 ID + 공백"으로 시작한다.
    public static IReadOnlyList<string> OfRules(IEnumerable<string> findings, params string[] rules) =>
        findings.Where(f => rules.Any(r => f.StartsWith(r + " ", StringComparison.Ordinal))).ToArray();

    // N5 판정. 순수 함수. key는 "부모 타입::멤버 이름", header는 그 MemberRef의 서명 머리.
    // 예외는 둘이고 각각 그 이름에만 적용한다. Assembly·Module의 GetType(string)은 인스턴스여도 금지다.
    internal static bool IsDeniedMember(string key, SignatureHeader header)
    {
        if (!DeniedMembers.Contains(key, StringComparer.Ordinal))
        {
            return false;
        }

        if (key == ActivatorCreateInstance && header.IsGeneric)
        {
            return false;
        }

        if (key == TypeGetType && header.IsInstance)
        {
            return false;
        }

        return true;
    }

    // N6 프레임워크 판정. 런타임 폴더에 이름이 서수로 같은 dll이 있는가(스펙 4.2 N6).
    internal static bool IsFrameworkAssembly(string name) => RuntimeAssemblyNames.Value.Contains(name);

    // 폴더 바로 아래 파일 중 이름이 ".dll"(서수)로 끝나는 것. 확장자를 뺀 이름을 서수 집합으로 돌려준다.
    // 하위 폴더와 "x.dll"이라는 이름의 폴더는 넣지 않는다(File.Exists와 같은 범위).
    internal static IReadOnlySet<string> AssemblyNamesIn(string directory)
    {
        const string Extension = ".dll";
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(directory))
        {
            var file = Path.GetFileName(path);
            if (file.EndsWith(Extension, StringComparison.Ordinal))
            {
                names.Add(file[..^Extension.Length]);
            }
        }

        return names;
    }

    // 파일을 열어 메타데이터를 읽는다. 어셈블리는 로드하지 않는다(PEReader).
    // 파일이 없거나 메타데이터가 없으면 예외를 던진다.
    private static T Read<T>(string dllPath, Func<MetadataReader, T> read)
    {
        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException($"검사할 dll이 없다: {dllPath}", dllPath);
        }

        using var stream = File.OpenRead(dllPath);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata)
        {
            throw new InvalidOperationException($"메타데이터가 없는 파일이다: {dllPath}");
        }

        return read(pe.GetMetadataReader());
    }

    // N1, N2, N6.
    private static void ScanAssemblyReferences(MetadataReader md, HashSet<string> allowed, SortedSet<string> findings)
    {
        foreach (var handle in md.AssemblyReferences)
        {
            var name = md.GetString(md.GetAssemblyReference(handle).Name);

            if (name == "System.Net" || name.StartsWith("System.Net.", StringComparison.Ordinal))
            {
                findings.Add($"N1 asmref {name}");
            }

            if (DeniedAssemblies.Contains(name, StringComparer.Ordinal))
            {
                findings.Add($"N2 asmref {name}");
            }

            if (!IsFrameworkAssembly(name) && !allowed.Contains(name))
            {
                findings.Add($"N6 non-framework asmref {name}");
            }
        }
    }

    // N3, D1(타입).
    private static void ScanTypeReferences(MetadataReader md, SortedSet<string> findings)
    {
        foreach (var handle in md.TypeReferences)
        {
            var fullName = TypeReferenceName(md, handle);

            if (DeniedTypes.Contains(fullName, StringComparer.Ordinal))
            {
                findings.Add($"N3 type {fullName}");
            }

            var ns = NamespaceOf(fullName);
            if (DeniedNamespaces.Any(d => ns == d || ns.StartsWith(d + ".", StringComparison.Ordinal)))
            {
                findings.Add($"N3 namespace {fullName}");
            }

            if (DeterminismTypes.Contains(fullName, StringComparer.Ordinal))
            {
                findings.Add($"D1 type {fullName}");
            }
        }
    }

    // N5, D1(멤버).
    private static void ScanMemberReferences(MetadataReader md, SortedSet<string> findings)
    {
        foreach (var member in MemberReferences(md))
        {
            if (IsDeniedMember(member.Key, member.Header))
            {
                findings.Add($"N5 member {member.Key}");
            }

            if (DeterminismMembers.Contains(member.Key, StringComparer.Ordinal))
            {
                findings.Add($"D1 member {member.Key}");
            }
        }
    }

    // MemberRef마다 "부모 타입::멤버 이름"과 서명 머리(제네릭·인스턴스 여부)를 읽는다.
    private static IEnumerable<MemberReferenceInfo> MemberReferences(MetadataReader md)
    {
        foreach (var handle in md.MemberReferences)
        {
            var member = md.GetMemberReference(handle);
            var key = $"{ParentName(md, member.Parent)}::{md.GetString(member.Name)}";
            var header = md.GetBlobReader(member.Signature).ReadSignatureHeader();
            yield return new MemberReferenceInfo(key, header);
        }
    }

    // N4: DllImport 메서드(PinvokeImpl 플래그)와 ModuleRef.
    private static void ScanPlatformInvoke(MetadataReader md, SortedSet<string> findings)
    {
        foreach (var handle in md.MethodDefinitions)
        {
            var method = md.GetMethodDefinition(handle);
            if ((method.Attributes & MethodAttributes.PinvokeImpl) == 0)
            {
                continue;
            }

            var import = method.GetImport();
            var module = import.Module.IsNil ? "?" : md.GetString(md.GetModuleReference(import.Module).Name);
            findings.Add($"N4 pinvoke {md.GetString(method.Name)} -> {module}!{md.GetString(import.Name)}");
        }

        var moduleRefCount = md.GetTableRowCount(TableIndex.ModuleRef);
        for (var row = 1; row <= moduleRefCount; row++)
        {
            var moduleRef = md.GetModuleReference(MetadataTokens.ModuleReferenceHandle(row));
            findings.Add($"N4 moduleref {md.GetString(moduleRef.Name)}");
        }
    }

    // 중첩 타입은 바깥 타입의 네임스페이스를 따른다. "A.B.Outer+Inner" → "A.B".
    private static string NamespaceOf(string fullName)
    {
        var outer = fullName.Split('+')[0];
        var dot = outer.LastIndexOf('.');
        return dot < 0 ? string.Empty : outer[..dot];
    }

    // TypeRef 전체 이름. 중첩 타입은 "바깥+안".
    private static string TypeReferenceName(MetadataReader md, TypeReferenceHandle handle)
    {
        var type = md.GetTypeReference(handle);
        var name = md.GetString(type.Name);
        if (type.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            return TypeReferenceName(md, (TypeReferenceHandle)type.ResolutionScope) + "+" + name;
        }

        var ns = md.GetString(type.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    // TypeDef 전체 이름. 중첩 타입은 "바깥+안".
    private static string TypeDefinitionName(MetadataReader md, TypeDefinitionHandle handle)
    {
        var type = md.GetTypeDefinition(handle);
        var name = md.GetString(type.Name);
        var declaring = type.GetDeclaringType();
        if (!declaring.IsNil)
        {
            return TypeDefinitionName(md, declaring) + "+" + name;
        }

        var ns = md.GetString(type.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    // MemberRef 부모 이름. 부모가 제네릭 인스턴스(TypeSpec)면 제네릭 정의 이름을 쓴다.
    private static string ParentName(MetadataReader md, EntityHandle parent)
    {
        switch (parent.Kind)
        {
            case HandleKind.TypeReference:
                return TypeReferenceName(md, (TypeReferenceHandle)parent);
            case HandleKind.TypeDefinition:
                return TypeDefinitionName(md, (TypeDefinitionHandle)parent);
            case HandleKind.TypeSpecification:
                var blob = md.GetBlobReader(md.GetTypeSpecification((TypeSpecificationHandle)parent).Signature);
                if (blob.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance)
                {
                    return "<typespec>";
                }

                // 다음은 class·valuetype 표지, 그다음이 제네릭 정의 핸들이다.
                blob.ReadSignatureTypeCode();
                var generic = blob.ReadTypeHandle();
                return generic.Kind == HandleKind.TypeReference
                    ? TypeReferenceName(md, (TypeReferenceHandle)generic)
                    : TypeDefinitionName(md, (TypeDefinitionHandle)generic);
            case HandleKind.MethodDefinition:
                return "<methoddef>";
            case HandleKind.ModuleReference:
                return "<moduleref>";
            default:
                return "<?>";
        }
    }
}

// MemberRef 하나. Key는 "부모 타입::멤버 이름", Header는 서명 머리(IsGeneric·IsInstance).
internal readonly record struct MemberReferenceInfo(string Key, SignatureHeader Header);
