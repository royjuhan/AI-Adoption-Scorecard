using System.Runtime.InteropServices;

namespace Scorecard.Tests.Guards;

// G1 자기 검사용 카나리아. docs/spec/guard-tests.md 4.3.
// 아무도 호출하지 않는다. Scorecard.Tests.dll 메타데이터에 금지 참조를 남기기만 한다.
// MetadataScanner 목록의 항목마다 참조를 하나 이상 둔다. 목록 이름에 오타가 있으면 자기 검사가 실패한다.
// 이 파일은 tests/에만 있다. src/에는 이런 참조가 없어야 한다.
internal static class ScannerCanary
{
    // N1·N3: 네트워크 타입. N3 네임스페이스 8개를 모두 하나씩 참조한다.
    internal static object Http() => new System.Net.Http.HttpClient();

    internal static object Sock() => new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);

    internal static object Tcp() => new System.Net.Sockets.TcpClient();

#pragma warning disable SYSLIB0014 // WebClient는 사용 중단 경고가 난다. 카나리아에서만 끈다.
    internal static object Web() => new System.Net.WebClient();
#pragma warning restore SYSLIB0014

    internal static Type[] OtherNetworkNamespaces() =>
    [
        typeof(System.Net.WebSockets.ClientWebSocket),
        typeof(System.Net.Mail.MailAddress),
        typeof(System.Net.NetworkInformation.Ping),
        typeof(System.Net.Quic.QuicError),
        typeof(System.Net.Security.SslStream),
    ];

    // N2·N3: 외부 프로세스.
    internal static object? Proc() => System.Diagnostics.Process.Start("x");

    internal static object ProcInfo() => new System.Diagnostics.ProcessStartInfo("x");

    // N2·N3: 어셈블리 로드 문맥.
    internal static object Alc() => System.Runtime.Loader.AssemblyLoadContext.Default;

    // N3: 네이티브 라이브러리 직접 로드.
    internal static IntPtr Native() => NativeLibrary.Load("x");

    // N5: 문자열로 타입·어셈블리를 찾거나 만드는 멤버.
    internal static object? Reflect() => Activator.CreateInstance(Type.GetType("x")!);

    internal static object? ReflectByAssembly() =>
        typeof(ScannerCanary).Assembly.GetType("x") ?? typeof(ScannerCanary).Module.GetType("x");

    internal static string? Load() =>
        System.Reflection.Assembly.Load("x").FullName
        + System.Reflection.Assembly.LoadFrom("x").FullName
        + System.Reflection.Assembly.LoadFile("x").FullName
        + System.Reflection.Assembly.UnsafeLoadFrom("x").FullName;

    internal static object DomainLoad() => AppDomain.CurrentDomain.Load("x");

    internal static Delegate FunctionPointer(IntPtr p) => Marshal.GetDelegateForFunctionPointer(p, typeof(Action));

#pragma warning disable CA1416 // Windows 전용 API다(CA1416). 카나리아에서만 끈다.
    internal static object? Com() => Type.GetTypeFromProgID("x") ?? Type.GetTypeFromCLSID(Guid.Empty);
#pragma warning restore CA1416

    // D1: 난수·현재시각·실행 환경.
    internal static string Clock() =>
        DateTime.Now.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + DateTime.UtcNow.Ticks
        + DateTime.Today.Ticks
        + DateTimeOffset.Now.Ticks
        + DateTimeOffset.UtcNow.Ticks;

    internal static string Randomness() =>
        Guid.NewGuid().ToString()
        + Guid.CreateVersion7()
        + new Random().Next()
        + System.Security.Cryptography.RandomNumberGenerator.GetInt32(10);

    internal static string MachineState() =>
        Environment.TickCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + Environment.TickCount64
        + Environment.MachineName
        + Environment.UserName
        + Environment.GetEnvironmentVariable("x")
        + Environment.GetEnvironmentVariables().Count;

    internal static string Timing() =>
        System.Diagnostics.Stopwatch.StartNew().Elapsed.ToString()
        + TimeProvider.System.GetUtcNow().Ticks;

    // N4: P/Invoke. LibraryImport는 AllowUnsafeBlocks가 필요해 쓰지 않는다(스펙 4.3).
    [DllImport("libc", EntryPoint = "getpid")]
    internal static extern int GetPid();

    // 허용(오탐이 없어야 함, 스펙 4.1 V14): System.Runtime에 있는 System.Net 타입, Uri, 제네릭 new T(), 인스턴스 Type.GetType().
    internal static string Allowed(string s) =>
        System.Net.WebUtility.HtmlEncode(s) + new Uri("file:///x").Scheme;

    // new T()는 제네릭 Activator.CreateInstance<T>() MemberRef를 남긴다. 위 Reflect()의 비제네릭과 이름이 같다.
    internal static T Generic<T>()
        where T : new() => new T();

    // 정적 형식이 Type인 식의 GetType()은 인스턴스 System.Type::GetType MemberRef를 남긴다.
    // 위 Reflect()의 정적 Type.GetType(string)과 이름이 같고 서명만 다르다. 이것은 허용한다(N5 예외).
    internal static Type InstanceGetType() => typeof(ScannerCanary).GetType();
}
