// V03: .cs 안의 using 별칭.
using Web = System.Net.Http.HttpClient;

namespace Scorecard.Core.Spike;

public static class V03
{
    public static object Make() => new Web();
}
