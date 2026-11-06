// V17: WebClient. 사용 중단(obsolete) 경고 SYSLIB0014가 나는지 본다.
namespace Scorecard.Core.Spike;

public static class V17
{
    public static object Make() => new System.Net.WebClient();
}
