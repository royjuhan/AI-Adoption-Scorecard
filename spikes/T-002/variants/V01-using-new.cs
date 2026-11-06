// V01: 평범한 사용. using + new HttpClient(). 수용 기준 2의 "일부러 HttpClient를 넣는다"와 같은 모양.
using System.Net.Http;

namespace Scorecard.Core.Spike;

public static class V01
{
    public static object Make() => new HttpClient();
}
