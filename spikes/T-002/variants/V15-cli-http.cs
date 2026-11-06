// V15: Cli에 HttpClient. src/Scorecard.Cli/에 넣는다.
namespace Scorecard.Cli;

internal static class V15
{
    internal static object Make() => new System.Net.Http.HttpClient();
}
