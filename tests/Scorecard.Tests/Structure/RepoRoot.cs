namespace Scorecard.Tests.Structure;

// 저장소 루트 찾기. 스펙 docs/spec/solution-skeleton.md 7절의 방법을 쓴다.
// 테스트 실행 폴더(AppContext.BaseDirectory)에서 위로 올라가며
// AIAdoptionScorecard.sln이 있는 첫 폴더를 루트로 본다.
// git, dotnet 같은 외부 프로세스는 부르지 않는다. 파일만 읽는다.
internal static class RepoRoot
{
    public const string SolutionFileName = "AIAdoptionScorecard.sln";

    private static readonly Lazy<string> root = new(Find);

    // 루트 폴더의 전체 경로.
    public static string FullPath => root.Value;

    // 루트 기준 경로를 만든다. 조각은 "src/Scorecard.Cli/x.csproj"처럼 '/'로 써도 된다.
    // 구분자는 Path.Combine이 OS에 맞게 붙인다.
    public static string Combine(string relativePath)
    {
        var parts = relativePath.Split('/', '\\');
        return Path.Combine([FullPath, .. parts]);
    }

    // 전체 경로를 루트 기준 상대 경로로 바꾼다. 구분자는 OS와 상관없이 '/'.
    public static string ToRelative(string fullPath) =>
        Path.GetRelativePath(FullPath, fullPath).Replace(Path.DirectorySeparatorChar, '/');

    // 루트 바로 아래 파일 이름. 디스크에 있는 대소문자 그대로 돌려준다.
    // Windows·macOS의 File.Exists는 대소문자를 무시하므로, 정확한 이름 비교에는 이 목록을 쓴다.
    public static string[] FileNames() =>
        Directory.EnumerateFiles(FullPath)
            .Select(f => Path.GetFileName(f))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException(
            $"저장소 루트를 찾지 못했다. {SolutionFileName}이 있는 상위 폴더가 없다. 시작 폴더: {AppContext.BaseDirectory}");
    }
}
