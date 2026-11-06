using System.Runtime.InteropServices;

namespace GuardSpike;

// 스캐너 자기 검사용 카나리아. 어디서도 호출하지 않는다. 메타데이터에 참조만 남긴다.
// "검사 도구가 들어 있는 어셈블리를 검사하면 이것들이 잡히는가"를 본다.
// MetaScan.cs 목록의 항목마다 하나씩 둔다. 단 N3 네임스페이스 중 WebSockets·Mail·NetworkInformation·Quic·Security는 없다.
internal static partial class Canary
{
    // N1·N3: 네트워크
    internal static object Http() => new System.Net.Http.HttpClient();

    internal static object Sock() => new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);

    internal static object Tcp() => new System.Net.Sockets.TcpClient();

#pragma warning disable SYSLIB0014 // WebClient는 사용 중단(obsolete) 경고가 난다.
    internal static object Web() => new System.Net.WebClient();
#pragma warning restore SYSLIB0014

    // N2·N3: 외부 프로세스
    internal static void Proc() => System.Diagnostics.Process.Start("x");

    internal static object ProcInfo() => new System.Diagnostics.ProcessStartInfo("x");

    // N3·N5: 문자열로 찾아 로드·생성
    internal static object? Reflect() => Activator.CreateInstance(Type.GetType("A" + "B")!);

    internal static object? Reflect2() => typeof(Canary).Assembly.GetType("x") ?? typeof(Canary).Module.GetType("x");

    internal static object Load() => System.Reflection.Assembly.Load("x").GetName().Name + System.Reflection.Assembly.LoadFrom("x").FullName + System.Reflection.Assembly.LoadFile("x").FullName + System.Reflection.Assembly.UnsafeLoadFrom("x").FullName;

    internal static object DomainLoad() => AppDomain.CurrentDomain.Load("x");

    internal static object Alc() => System.Runtime.Loader.AssemblyLoadContext.Default;

    internal static IntPtr Native() => NativeLibrary.Load("x");

    internal static Delegate FnPtr(IntPtr p) => Marshal.GetDelegateForFunctionPointer(p, typeof(Action));

    internal static object? Com() => Type.GetTypeFromProgID("x") ?? Type.GetTypeFromCLSID(Guid.Empty);

    // 허용: 제네릭 new T()는 Activator.CreateInstance<T>()가 된다.
    internal static T Generic<T>()
        where T : new() => new T();

    // D1: 난수·현재시각·실행 환경
    internal static string Clock() => DateTime.Now.ToString("O") + DateTime.UtcNow.Ticks + DateTime.Today.Ticks + DateTimeOffset.Now.Ticks + DateTimeOffset.UtcNow.Ticks;

    internal static string Random() => Guid.NewGuid().ToString() + Guid.CreateVersion7() + new Random().Next() + System.Security.Cryptography.RandomNumberGenerator.GetInt32(10);

    internal static string Env() => Environment.TickCount + Environment.TickCount64 + Environment.MachineName + Environment.UserName + Environment.GetEnvironmentVariable("x") + Environment.GetEnvironmentVariables().Count;

    internal static object Timing() => System.Diagnostics.Stopwatch.StartNew().Elapsed + TimeProvider.System.GetUtcNow().ToString();

    // 허용: System.Net 네임스페이스지만 System.Runtime에 있는 것.
    internal static string Allowed(string s) => System.Net.WebUtility.HtmlEncode(s) + new Uri("file:///x").Scheme;

    // 관찰: string.GetHashCode()가 메타데이터에 어떻게 남는가.
    internal static int StringHash(string s) => s.GetHashCode();

    // N4: P/Invoke
    [DllImport("libc", EntryPoint = "getpid")]
    internal static extern int GetPid();

    [LibraryImport("libc", EntryPoint = "getppid")]
    internal static partial int GetParentPid();
}
