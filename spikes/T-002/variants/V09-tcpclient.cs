// V09: 카드 목록에 없는 네트워크 타입(TcpClient).
namespace Scorecard.Core.Spike;

public static class V09
{
    public static object Make() => new System.Net.Sockets.TcpClient();
}
