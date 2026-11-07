using System.Xml.Linq;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.CaseSchema;

// 스펙 4.2: Core csproj에 YamlDotNet 18.1.0을 정확히 고정한다(승인 결정 2).
// 범위·와일드카드(18.*, [18.1,))를 쓰지 않는다. csproj XML을 직접 읽는다(외부 프로세스 없음).
[Trait("Spec", "case-schema")]
public class CorePackageTests
{
    private const string CoreProject = "src/Scorecard.Core/Scorecard.Core.csproj";
    private const string TestsProject = "tests/Scorecard.Tests/Scorecard.Tests.csproj";
    private const string PackageId = "YamlDotNet";
    private const string PinnedVersion = "18.1.0";

    [Fact]
    public void Core_pins_YamlDotNet_exact_version()
    {
        Assert.Equal([PinnedVersion], YamlDotNetVersions(CoreProject));
    }

    // 테스트 프로젝트가 Y1 카나리아용으로 YamlDotNet을 직접 참조하면 Core와 같은 버전이어야 한다.
    // 다르면 테스트가 Core가 빌드된 버전과 다른 YamlDotNet.dll로 돈다(syntax 문구 등이 달라질 수 있다).
    [Fact]
    public void Tests_YamlDotNet_reference_matches_Core_version()
    {
        var core = YamlDotNetVersions(CoreProject);

        Assert.All(YamlDotNetVersions(TestsProject), version => Assert.Equal(core, new[] { version }));
    }

    // PackageReference Include가 YamlDotNet(NuGet ID라 대소문자 무시)인 항목의 Version. 속성 또는 자식 요소.
    private static string[] YamlDotNetVersions(string project) =>
        XDocument.Load(RepoRoot.Combine(project)).Descendants()
            .Where(e => e.Name.LocalName == "PackageReference")
            .Where(e => string.Equals((string?)e.Attribute("Include"), PackageId, StringComparison.OrdinalIgnoreCase))
            .Select(e => (string?)e.Attribute("Version") ?? e.Elements().FirstOrDefault(c => c.Name.LocalName == "Version")?.Value ?? "<없음>")
            .Select(v => v.Trim())
            .ToArray();
}
