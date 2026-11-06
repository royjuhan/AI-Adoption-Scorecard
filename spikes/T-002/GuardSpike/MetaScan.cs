using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace GuardSpike;

// 컴파일된 어셈블리의 메타데이터 표(AssemblyRef, TypeRef, MemberRef, MethodDef의 P/Invoke, ModuleRef)를 읽어
// 금지 참조를 찾는다. 어셈블리를 로드하지 않는다(PEReader로 파일만 읽는다).
internal static class MetaScan
{
    // N1: 이름이 System.Net 이거나 System.Net. 으로 시작하는 어셈블리 참조.
    // N2: 그 밖의 금지 어셈블리 참조.
    private static readonly string[] DeniedAssemblies =
    [
        "System.Diagnostics.Process",
        "System.Runtime.Loader",
    ];

    // N3: 금지 타입(전체 이름). 작업 카드의 목록 + 같은 역할.
    private static readonly string[] DeniedTypes =
    [
        "System.Net.Http.HttpClient",
        "System.Net.Sockets.Socket",
        "System.Net.WebClient",
        "System.Diagnostics.Process",
        "System.Diagnostics.ProcessStartInfo",
        "System.Runtime.InteropServices.NativeLibrary",
    ];

    // N3: 금지 네임스페이스(같거나 하위).
    private static readonly string[] DeniedNamespaces =
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

    // N5: 문자열로 타입·어셈블리를 찾거나 만드는 멤버. 금지 타입을 이름 없이 쓰는 우회로를 막는다.
    private static readonly string[] DeniedMembers =
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
        "System.Activator::CreateInstance", // 제네릭 CreateInstance<T>()는 new T()가 만든다. 아래에서 제외.
        "System.Runtime.InteropServices.Marshal::GetDelegateForFunctionPointer",
    ];

    // D1 (--core일 때만): 난수·현재시각·실행 환경에 따라 값이 바뀌는 타입·멤버.
    private static readonly string[] CoreDeniedTypes =
    [
        "System.Random",
        "System.Security.Cryptography.RandomNumberGenerator",
        "System.Diagnostics.Stopwatch",
    ];

    private static readonly string[] CoreDeniedMembers =
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

    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("meta <dll> [--core] [--allow <어셈블리>]...");
            return 2;
        }

        var path = args[0];
        var core = args.Contains("--core");
        var allow = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length - 1; i++)
        {
            if (args[i] == "--allow")
            {
                allow.Add(args[i + 1]);
            }
        }

        if (!File.Exists(path))
        {
            Console.WriteLine($"RESULT error=파일 없음 {Path.GetFileName(path)}");
            return 2;
        }

        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata)
        {
            Console.WriteLine("RESULT error=메타데이터 없음");
            return 2;
        }

        var md = pe.GetMetadataReader();
        var refs = new List<string>();
        var findings = new SortedSet<string>(StringComparer.Ordinal);

        // 프레임워크 어셈블리인지는 현재 런타임 폴더에 같은 이름의 dll이 있는지로 본다.
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();

        foreach (var h in md.AssemblyReferences)
        {
            var r = md.GetAssemblyReference(h);
            var name = md.GetString(r.Name);
            var framework = File.Exists(Path.Combine(runtimeDir, name + ".dll"));
            refs.Add($"REF asm {name} {r.Version} framework={framework}");

            if (name == "System.Net" || name.StartsWith("System.Net.", StringComparison.Ordinal))
            {
                findings.Add($"N1 asmref {name}");
            }

            if (DeniedAssemblies.Contains(name))
            {
                findings.Add($"N2 asmref {name}");
            }

            if (!framework && !allow.Contains(name))
            {
                findings.Add($"N6 non-framework asmref {name}");
            }
        }

        foreach (var h in md.TypeReferences)
        {
            var full = TypeRefName(md, h);
            var scope = TypeRefScope(md, h);
            refs.Add($"REF type [{scope}]{full}");

            if (DeniedTypes.Contains(full))
            {
                findings.Add($"N3 type {full}");
            }

            var ns = Namespace(full);
            foreach (var denied in DeniedNamespaces)
            {
                if (ns == denied || ns.StartsWith(denied + ".", StringComparison.Ordinal))
                {
                    findings.Add($"N3 namespace {full}");
                }
            }

            if (core && CoreDeniedTypes.Contains(full))
            {
                findings.Add($"D1 type {full}");
            }
        }

        foreach (var h in md.MemberReferences)
        {
            var m = md.GetMemberReference(h);
            var parent = ParentName(md, m.Parent);
            var name = md.GetString(m.Name);
            var header = md.GetBlobReader(m.Signature).ReadSignatureHeader();
            var key = $"{parent}::{name}";
            refs.Add($"REF member {key}{(header.IsGeneric ? "<T>" : "")}");

            if (DeniedMembers.Contains(key) && !(key == "System.Activator::CreateInstance" && header.IsGeneric))
            {
                findings.Add($"N5 member {key}");
            }

            if (core && CoreDeniedMembers.Contains(key))
            {
                findings.Add($"D1 member {key}");
            }
        }

        // N4: P/Invoke. DllImport(LibraryImport가 만든 것 포함)는 MethodDef에 PinvokeImpl 플래그와 ModuleRef를 남긴다.
        foreach (var h in md.MethodDefinitions)
        {
            var m = md.GetMethodDefinition(h);
            if ((m.Attributes & MethodAttributes.PinvokeImpl) != 0)
            {
                var import = m.GetImport();
                var module = import.Module.IsNil ? "?" : md.GetString(md.GetModuleReference(import.Module).Name);
                findings.Add($"N4 pinvoke {md.GetString(m.Name)} -> {module}!{md.GetString(import.Name)}");
            }
        }

        var moduleRefs = md.GetTableRowCount(TableIndex.ModuleRef);
        for (var i = 1; i <= moduleRefs; i++)
        {
            var mr = md.GetModuleReference(MetadataTokens.ModuleReferenceHandle(i));
            findings.Add($"N4 moduleref {md.GetString(mr.Name)}");
        }

        foreach (var line in refs.Order(StringComparer.Ordinal))
        {
            Console.WriteLine(line);
        }

        foreach (var f in findings)
        {
            Console.WriteLine("FIND " + f);
        }

        Console.WriteLine($"RESULT {Path.GetFileName(path)} asmrefs={md.AssemblyReferences.Count} typerefs={md.TypeReferences.Count} memberrefs={md.MemberReferences.Count} findings={findings.Count}");
        return findings.Count == 0 ? 0 : 1;
    }

    private static string Namespace(string fullName)
    {
        var outer = fullName.Split('+')[0];
        var dot = outer.LastIndexOf('.');
        return dot < 0 ? string.Empty : outer[..dot];
    }

    private static string TypeRefName(MetadataReader md, TypeReferenceHandle h)
    {
        var t = md.GetTypeReference(h);
        var name = md.GetString(t.Name);
        if (t.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            // 중첩 타입: 바깥 타입 이름 + '+'.
            return TypeRefName(md, (TypeReferenceHandle)t.ResolutionScope) + "+" + name;
        }

        var ns = md.GetString(t.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    private static string TypeRefScope(MetadataReader md, TypeReferenceHandle h)
    {
        var t = md.GetTypeReference(h);
        return t.ResolutionScope.Kind switch
        {
            HandleKind.AssemblyReference => md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)t.ResolutionScope).Name),
            HandleKind.TypeReference => TypeRefScope(md, (TypeReferenceHandle)t.ResolutionScope),
            HandleKind.ModuleReference => "<moduleref>",
            HandleKind.ModuleDefinition => "<self>",
            _ => "<?>",
        };
    }

    private static string TypeDefName(MetadataReader md, TypeDefinitionHandle h)
    {
        var t = md.GetTypeDefinition(h);
        var name = md.GetString(t.Name);
        var declaring = t.GetDeclaringType();
        if (!declaring.IsNil)
        {
            return TypeDefName(md, declaring) + "+" + name;
        }

        var ns = md.GetString(t.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    private static string ParentName(MetadataReader md, EntityHandle parent)
    {
        switch (parent.Kind)
        {
            case HandleKind.TypeReference:
                return TypeRefName(md, (TypeReferenceHandle)parent);
            case HandleKind.TypeDefinition:
                return TypeDefName(md, (TypeDefinitionHandle)parent);
            case HandleKind.TypeSpecification:
                // 제네릭 인스턴스(List<int> 등)면 제네릭 정의 이름으로 본다.
                var spec = md.GetTypeSpecification((TypeSpecificationHandle)parent);
                var blob = md.GetBlobReader(spec.Signature);
                if (blob.ReadSignatureTypeCode() == SignatureTypeCode.GenericTypeInstance)
                {
                    blob.ReadSignatureTypeCode();
                    var generic = blob.ReadTypeHandle();
                    return generic.Kind == HandleKind.TypeReference
                        ? TypeRefName(md, (TypeReferenceHandle)generic)
                        : TypeDefName(md, (TypeDefinitionHandle)generic);
                }

                return "<typespec>";
            case HandleKind.MethodDefinition:
                return "<methoddef>";
            case HandleKind.ModuleReference:
                return "<moduleref>";
            default:
                return "<?>";
        }
    }
}
