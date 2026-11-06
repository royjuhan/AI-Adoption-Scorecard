using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Scorecard.Core.Spike;

// 스파이크 후보 B: 정적 생성기(Vecc.YamlDotNet.Analyzers.StaticGenerator). G1 메타데이터 비교용 최소 코드.
public static class StaticProbe
{
    public static string? ReadName(string yaml)
    {
        var deserializer = new StaticDeserializerBuilder(new ProbeContext())
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        return deserializer.Deserialize<StaticCase>(yaml)?.Name;
    }
}

[YamlStaticContext]
public partial class ProbeContext : StaticContext
{
}

[YamlSerializable]
public sealed class StaticCase
{
    public string? Name { get; set; }

    public List<StaticStep>? Steps { get; set; }
}

[YamlSerializable]
public sealed class StaticStep
{
    public string? Name { get; set; }

    public Dictionary<string, string>? Answers { get; set; }
}
