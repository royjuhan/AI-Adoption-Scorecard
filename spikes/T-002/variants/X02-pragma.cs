// X02: 분석기 경고를 pragma로 끄고 HttpClient를 쓴다.
namespace Scorecard.Core.Spike;

public static class X02
{
#pragma warning disable RS0030
    public static object Make() => new System.Net.Http.HttpClient();
#pragma warning restore RS0030
}
