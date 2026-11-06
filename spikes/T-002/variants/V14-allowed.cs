// V14: 허용해야 하는 것들. 오탐이 없어야 한다.
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Scorecard.Core.Spike;

public static class V14
{
    public static string Encode(string s) => System.Net.WebUtility.HtmlEncode(s);

    public static string Host(string s) => new Uri(s).Host;

    public static string Hash(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));

    public static T Make<T>()
        where T : new() => new T();

    public static string Read(string path) => File.ReadAllText(path);

    public static int Sum(IEnumerable<int> xs) => xs.Sum();

    public static bool Match(string s) => Regex.IsMatch(s, "a+");

    public static string Json(int[] xs) => System.Text.Json.JsonSerializer.Serialize(xs);
}
