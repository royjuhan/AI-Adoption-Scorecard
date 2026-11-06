// V11: 정의되지 않은 기호의 #if 안에만 HttpClient. 컴파일되지 않는 코드.
namespace Scorecard.Core.Spike;

public static class V11
{
    public static object? Make()
    {
#if NEVER_DEFINED
        return new System.Net.Http.HttpClient();
#else
        return null;
#endif
    }
}
