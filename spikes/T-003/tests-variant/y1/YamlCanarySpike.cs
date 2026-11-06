namespace Scorecard.Tests.Guards;

// 스파이크(T-003 S8): 테스트 프로젝트가 Core의 YamlDotNet 참조를 거쳐 YamlDotNet 타입을 쓸 수 있는가.
// 제안 규칙 Y1의 카나리아 모양. 아무도 호출하지 않는다. 머지 대상 아님.
internal static class YamlCanarySpike
{
    public static object Serialization() => new YamlDotNet.Serialization.DeserializerBuilder();

    public static object RepresentationModel() => new YamlDotNet.RepresentationModel.YamlStream();
}
