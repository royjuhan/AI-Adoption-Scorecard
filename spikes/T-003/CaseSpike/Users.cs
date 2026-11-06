using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Scorecard.Tests.Guards;

// G1 항목 멤버(N5·D1·R1)와 D1 타입의 멤버를 어느 타입의 메서드 본문이 부르는가.
// IL 바이트에서 call·callvirt·newobj·ldftn(0x28, 0x6F, 0x73, FE 06) 뒤 4바이트 토큰을 찾는다.
// 피연산자 바이트와 우연히 겹칠 수 있는 단순 검색이다(스파이크용). 결과는 "USER 항목 <- 네임스페이스" 서수 정렬.
internal static class Users
{
    public static int Run(string dll)
    {
        using var stream = File.OpenRead(dll);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();

        var targets = new Dictionary<int, string>();
        foreach (var handle in md.MemberReferences)
        {
            var member = md.GetMemberReference(handle);
            var parent = ParentName(md, member.Parent);
            var key = $"{parent}::{md.GetString(member.Name)}";
            var header = md.GetBlobReader(member.Signature).ReadSignatureHeader();
            string? label = null;
            if (MetadataScanner.IsDeniedMember(key, header)) label = "N5 " + key;
            else if (MetadataScanner.DeterminismMembers.Contains(key)) label = "D1 " + key;
            else if (MetadataScanner.ReflectionInvokeMembers.Contains(key)) label = "R1 " + key;
            else if (MetadataScanner.DeterminismTypes.Contains(parent)) label = "D1 " + key;
            if (label is not null) targets[MetadataTokens.GetToken(handle)] = label;
        }

        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var typeHandle in md.TypeDefinitions)
        {
            var type = md.GetTypeDefinition(typeHandle);
            var ns = OuterNamespace(md, typeHandle);
            foreach (var methodHandle in type.GetMethods())
            {
                var method = md.GetMethodDefinition(methodHandle);
                if (method.RelativeVirtualAddress == 0) continue;
                var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes() ?? [];
                for (var i = 0; i + 4 < il.Length; i++)
                {
                    var opLength = il[i] is 0x28 or 0x6F or 0x73 ? 1 : (il[i] == 0xFE && il[i + 1] == 0x06 ? 2 : 0);
                    if (opLength == 0 || i + opLength + 4 > il.Length) continue;
                    var token = BitConverter.ToInt32(il, i + opLength);
                    if (targets.TryGetValue(token, out var label)) found.Add($"USER {label} <- {ns}");
                }
            }
        }

        foreach (var line in found) Console.WriteLine(line);
        Console.WriteLine($"RESULT users={found.Count}");
        return 0;
    }

    private static string OuterNamespace(MetadataReader md, TypeDefinitionHandle handle)
    {
        var type = md.GetTypeDefinition(handle);
        while (!type.GetDeclaringType().IsNil) type = md.GetTypeDefinition(type.GetDeclaringType());
        var ns = md.GetString(type.Namespace);
        return ns.Length == 0 ? "<전역>" : ns;
    }

    private static string ParentName(MetadataReader md, EntityHandle parent)
    {
        switch (parent.Kind)
        {
            case HandleKind.TypeReference:
                var t = md.GetTypeReference((TypeReferenceHandle)parent);
                var n = md.GetString(t.Namespace);
                return n.Length == 0 ? md.GetString(t.Name) : n + "." + md.GetString(t.Name);
            case HandleKind.TypeSpecification:
                var blob = md.GetBlobReader(md.GetTypeSpecification((TypeSpecificationHandle)parent).Signature);
                if (blob.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance) return "<typespec>";
                blob.ReadSignatureTypeCode();
                var g = blob.ReadTypeHandle();
                return g.Kind == HandleKind.TypeReference ? ParentName(md, g) : "<typedef>";
            default:
                return "<?>";
        }
    }
}
