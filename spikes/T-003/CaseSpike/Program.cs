using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Scorecard.Core.Cases;
using Scorecard.Tests.Guards;

// T-003 스파이크 도구. 명령:
//   meta <dll> [--allow 이름]...   G1 검사기(tests의 MetadataScanner)로 모든 규칙 결과를 낸다.
//   refs <dll>                     AssemblyRef 목록과 YamlDotNet TypeRef 목록.
//   parse <case폴더> <출력파일> [--culture 이름]   후보 C로 읽고 결과 JSON을 쓴다. 오류 줄도 낸다.
//   probe <samples폴더>            인코딩·줄바꿈·결정론·깊이 등 메모리 안 실험.
//   users <dll>                    G1 항목(N5·D1·R1) 멤버를 IL에서 부르는 타입의 네임스페이스.
//   timing                         YamlDotNet Parser 이벤트 읽기 시간(흐름 중첩 깊이, 평평한 큰 파일).
return args.FirstOrDefault() switch
{
    "meta" => Meta(args[1], Options(args, "--allow")),
    "refs" => Refs(args[1]),
    "parse" => ParseCommand(args[1], args[2], Options(args, "--culture").FirstOrDefault()),
    "probe" => Probes.Run(args[1]),
    "users" => Users.Run(args[1]),
    "timing" => Timing.Run(),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("usage: meta|refs|parse|probe ...");
    return 2;
}

static string[] Options(string[] args, string name) =>
    args.Select((a, i) => (a, i)).Where(x => x.a == name && x.i + 1 < args.Length).Select(x => args[x.i + 1]).ToArray();

static int Meta(string dll, string[] allow)
{
    var findings = MetadataScanner.Scan(dll, allow);
    foreach (var f in findings)
    {
        Console.WriteLine("FIND " + f);
    }

    foreach (var rule in new[] { "N1", "N2", "N3", "N4", "N5", "N6", "D1", "R1" })
    {
        Console.WriteLine($"RESULT {rule}={MetadataScanner.OfRules(findings, rule).Count}");
    }

    return 0;
}

static int Refs(string dll)
{
    using var stream = File.OpenRead(dll);
    using var pe = new PEReader(stream);
    var md = pe.GetMetadataReader();
    foreach (var h in md.AssemblyReferences)
    {
        var r = md.GetAssemblyReference(h);
        Console.WriteLine($"ASMREF {md.GetString(r.Name)} {r.Version}");
    }

    var types = md.TypeReferences
        .Select(h => md.GetTypeReference(h))
        .Select(t => (md.GetString(t.Namespace) + "." + md.GetString(t.Name)).TrimStart('.'))
        .Where(n => n.StartsWith("YamlDotNet", StringComparison.Ordinal))
        .Order(StringComparer.Ordinal);
    foreach (var t in types)
    {
        Console.WriteLine("TYPEREF " + t);
    }

    return 0;
}

static int ParseCommand(string caseDir, string outFile, string? culture)
{
    if (culture is not null)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
    }

    var result = CaseParser.ParseDirectory(caseDir);
    var bytes = Output.Render(result);
    File.WriteAllBytes(outFile, bytes);
    foreach (var e in result.Errors)
    {
        Console.WriteLine("ERR " + e);
    }

    Console.WriteLine($"SHA {Convert.ToHexStringLower(SHA256.HashData(bytes))[..16]} valid={result.IsValid} errors={result.Errors.Count}");
    return 0;
}

// G2 어댑터 후보: 결과를 JSON 한 파일로. 테스트 쪽 코드(System.Text.Json)라 G1 대상이 아니다.
internal static class Output
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static byte[] Render(CaseParseResult result)
    {
        var payload = new { @case = result.Case, errors = result.Errors.Select(e => e.ToString()).ToArray() };
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, Options) + "\n");
    }
}
