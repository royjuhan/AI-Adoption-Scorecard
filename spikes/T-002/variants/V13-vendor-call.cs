// V13: 제3자 라이브러리(가짜 LLM SDK)를 부른다. Core 소스와 Core 메타데이터에 네트워크 타입이 없다.
namespace Scorecard.Core.Spike;

public static class V13
{
    public static string Ask() => new Vendor.Llm.ChatClient().Complete("x");
}
