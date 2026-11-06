// V16: Core에 현재시각·난수. 네트워크 검사 목록에는 없다.
namespace Scorecard.Core.Spike;

public static class V16
{
    public static string Stamp() => DateTime.Now.ToString("O");

    public static int Pick() => new Random().Next();

    public static string Id() => Guid.NewGuid().ToString();
}
