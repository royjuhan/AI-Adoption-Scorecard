// V10: P/Invoke로 운영체제의 socket 함수를 직접 부른다. 관리 코드 네트워크 타입이 없다.
using System.Runtime.InteropServices;

namespace Scorecard.Core.Spike;

public static class V10
{
    [DllImport("libc", EntryPoint = "socket")]
    public static extern int Open(int domain, int type, int protocol);
}
