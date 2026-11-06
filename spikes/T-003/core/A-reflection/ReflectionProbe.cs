using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Scorecard.Core.Spike;

// 스파이크 후보 A: 리플렉션 역직렬화. G1 메타데이터 비교용 최소 코드.
public static class ReflectionProbe
{
    public static string? ReadName(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        return deserializer.Deserialize<ProbeCase>(yaml)?.Name;
    }
}

public sealed class ProbeCase
{
    public string? Name { get; set; }

    public List<ProbeStep>? Steps { get; set; }
}

public sealed class ProbeStep
{
    public string? Name { get; set; }

    public Dictionary<string, string>? Answers { get; set; }
}
