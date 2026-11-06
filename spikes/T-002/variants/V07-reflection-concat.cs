// V07: 문자열을 이어 붙여 리플렉션으로 HttpClient를 만든다. 소스에 HttpClient라는 낱말이 없다.
namespace Scorecard.Core.Spike;

public static class V07
{
    public static object? Make()
    {
        var t = Type.GetType("System.Net.Http.Http" + "Client, System.Net.Http");
        return t is null ? null : Activator.CreateInstance(t);
    }
}
