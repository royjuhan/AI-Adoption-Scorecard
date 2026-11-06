// V13: 실제 SDK라면 여기서 HTTP로 LLM API를 부른다. 이 가짜는 부르지 않고 참조만 둔다.
namespace Vendor.Llm;

public sealed class ChatClient
{
    private static readonly System.Net.Http.HttpClient Http = new();

    public string Complete(string prompt) => Http.Timeout.Ticks > 0 ? prompt : string.Empty;
}
