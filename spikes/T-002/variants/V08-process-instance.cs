// V08: Process.Start(...) 정적 호출 대신 인스턴스의 Start(). "Process.Start" 문자열이 없다.
using System.Diagnostics;

namespace Scorecard.Core.Spike;

public static class V08
{
    public static void Run()
    {
        var p = new Process();
        p.StartInfo.FileName = "x";
        p.Start();
    }
}
