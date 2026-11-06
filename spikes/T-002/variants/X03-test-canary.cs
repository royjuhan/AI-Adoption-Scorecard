// X03: 테스트 프로젝트 안 카나리아. tests/Scorecard.Tests/에 넣는다(스크래치 clone만).
// CI와 같은 -warnaserror로 빌드되는지, 같은 스캐너로 Scorecard.Tests.dll을 검사하면 잡히는지 본다. 호출하지 않는다.
using System.Runtime.InteropServices;

namespace Scorecard.Tests;

internal static class SpikeCanary
{
    internal static object Http() => new System.Net.Http.HttpClient();

    internal static object Sock() => new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);

#pragma warning disable SYSLIB0014
    internal static object Web() => new System.Net.WebClient();
#pragma warning restore SYSLIB0014

    internal static void Proc() => System.Diagnostics.Process.Start("x");

    internal static string Clock() => DateTime.Now.ToString("O");

    [DllImport("libc", EntryPoint = "getpid")]
    internal static extern int GetPid();
}

// 테스트 프로젝트에서 System.Reflection.Metadata를 패키지 추가 없이 쓸 수 있는지 본다.
internal static class SpikeSrmProbe
{
    internal static int TypeRefCount(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
        return System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe).TypeReferences.Count;
    }
}
