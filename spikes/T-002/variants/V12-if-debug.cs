// V12: #if DEBUG 안에만 HttpClient. Debug 빌드에만 들어간다.
namespace Scorecard.Core.Spike;

public static class V12
{
    public static object? Make()
    {
#if DEBUG
        return new System.Net.Http.HttpClient();
#else
        return null;
#endif
    }
}
