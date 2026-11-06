using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Scorecard.Tests.Structure;

// T-001 R6: 솔루션에 세 프로젝트가 있다. (docs/spec/solution-skeleton.md 4절 R6)
// dotnet sln list, dotnet build 같은 외부 프로세스는 부르지 않는다.
// .sln 텍스트와 .csproj XML을 직접 읽는다. 그래서 Directory.Build.props 등으로
// 옮긴 속성은 보지 못한다. 그런 변경을 하면 이 테스트도 같이 고친다.
[Trait("Rule", "R6")]
public partial class SolutionStructureTests
{
    private const string CoreProject = "src/Scorecard.Core/Scorecard.Core.csproj";
    private const string CliProject = "src/Scorecard.Cli/Scorecard.Cli.csproj";
    private const string TestsProject = "tests/Scorecard.Tests/Scorecard.Tests.csproj";

    // `dotnet sln list` 출력과 같은 모양(구분자 '/', 정렬)으로 쓴다(스펙 R6 [A]).
    private static readonly string[] ExpectedSolutionProjects =
    [
        CliProject,
        CoreProject,
        TestsProject,
    ];

    // xUnit 테스트를 dotnet test로 찾고 돌리는 데 필요한 패키지.
    private static readonly string[] XunitTestPackages =
    [
        "Microsoft.NET.Test.Sdk",
        "xunit",
        "xunit.runner.visualstudio",
    ];

    // .sln의 솔루션 폴더 종류 GUID. 이 항목은 프로젝트가 아니다(`dotnet sln list`에도 안 나온다).
    private const string SolutionFolderTypeGuid = "2150E333-8FDC-42A3-9474-1A3956D46DE8";

    // 이 파서가 가정하는 .sln 형식(스펙 R6 [읽음]).
    private const string SolutionHeader = "Microsoft Visual Studio Solution File, Format Version 12.00";

    // .sln의 Project 줄: Project("{종류}") = "이름", "경로", "{GUID}"
    [GeneratedRegex(
        @"^Project\(""\{(?<type>[^}]+)\}""\)\s*=\s*""(?<name>[^""]*)""\s*,\s*""(?<path>[^""]*)""\s*,\s*""\{(?<id>[^}]+)\}""\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SolutionProjectLine();

    [Fact]
    public void Solution_lists_exactly_the_three_projects()
    {
        Assert.Equal(ExpectedSolutionProjects, ReadSolutionProjects());
    }

    [Theory]
    [InlineData(CoreProject)]
    [InlineData(CliProject)]
    [InlineData(TestsProject)]
    public void Project_targets_net10(string project)
    {
        Assert.Equal("net10.0", Property(LoadProject(project), "TargetFramework"));
    }

    [Fact]
    public void Core_is_class_library()
    {
        // OutputType이 없으면 기본값 Library다.
        var outputType = Property(LoadProject(CoreProject), "OutputType");

        Assert.True(
            outputType is null || string.Equals(outputType, "Library", StringComparison.OrdinalIgnoreCase),
            $"Scorecard.Core의 OutputType이 Library가 아니다: {outputType}");
    }

    [Fact]
    public void Cli_is_executable()
    {
        Assert.Equal("Exe", Property(LoadProject(CliProject), "OutputType"), ignoreCase: true);
    }

    [Fact]
    public void Cli_assembly_name_is_scorecard()
    {
        // 실행 파일 이름. Linux에서는 대소문자가 그대로 파일 이름이 되므로 정확히 비교한다.
        Assert.Equal("scorecard", Property(LoadProject(CliProject), "AssemblyName"));
    }

    [Fact]
    public void Cli_references_Core()
    {
        Assert.Contains(CoreProject, ProjectReferences(CliProject));
    }

    [Fact]
    public void Tests_references_Core()
    {
        Assert.Contains(CoreProject, ProjectReferences(TestsProject));
    }

    [Fact]
    public void Tests_uses_xunit_test_packages()
    {
        var packages = PackageReferences(TestsProject);

        // NuGet 패키지 ID는 대소문자를 가리지 않는다.
        foreach (var package in XunitTestPackages)
        {
            Assert.Contains(package, packages, StringComparer.OrdinalIgnoreCase);
        }
    }

    // .sln에서 솔루션 폴더를 뺀 프로젝트 경로. 구분자 '/', 순서는 서수 정렬.
    private static string[] ReadSolutionProjects()
    {
        var lines = File.ReadAllLines(RepoRoot.Combine(RepoRoot.SolutionFileName));
        Assert.Contains(SolutionHeader, lines.Select(l => l.Trim()));

        return lines
            .Select(l => SolutionProjectLine().Match(l.Trim()))
            .Where(m => m.Success)
            .Where(m => !string.Equals(m.Groups["type"].Value, SolutionFolderTypeGuid, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Groups["path"].Value.Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static XDocument LoadProject(string project) =>
        XDocument.Load(RepoRoot.Combine(project));

    // PropertyGroup 바로 아래 속성 값. 없으면 null. 두 번 이상 나오면 실패로 본다.
    private static string? Property(XDocument project, string name)
    {
        var values = ChildrenNamed(project.Root!, "PropertyGroup")
            .SelectMany(g => ChildrenNamed(g, name))
            .Select(e => e.Value.Trim())
            .ToArray();

        Assert.True(values.Length <= 1, $"{name} 속성이 {values.Length}번 나온다.");
        return values.SingleOrDefault();
    }

    // ProjectReference의 대상. 루트 기준 경로(구분자 '/')로 돌려준다.
    // csproj의 Include는 보통 '\'로 쓰므로 OS 구분자로 바꾼 뒤 전체 경로를 구한다.
    private static string[] ProjectReferences(string project)
    {
        var projectDir = Path.GetDirectoryName(RepoRoot.Combine(project))!;

        return ItemIncludes(LoadProject(project), "ProjectReference")
            .Select(include => include.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar))
            .Select(include => Path.GetFullPath(Path.Combine(projectDir, include)))
            .Select(RepoRoot.ToRelative)
            .ToArray();
    }

    private static string[] PackageReferences(string project) =>
        ItemIncludes(LoadProject(project), "PackageReference").ToArray();

    private static IEnumerable<string> ItemIncludes(XDocument project, string itemType) =>
        ChildrenNamed(project.Root!, "ItemGroup")
            .SelectMany(g => ChildrenNamed(g, itemType))
            .Select(e => ((string?)e.Attribute("Include"))?.Trim() ?? string.Empty);

    // SDK 형식 csproj는 XML 네임스페이스가 없지만, 옛 형식처럼 있어도 찾도록 LocalName으로 비교한다.
    private static IEnumerable<XElement> ChildrenNamed(XElement parent, string localName) =>
        parent.Elements().Where(e => e.Name.LocalName == localName);
}
