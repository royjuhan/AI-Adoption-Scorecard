namespace Scorecard.Tests.Structure;

// T-001 R5: .gitignore가 루트의 cases/를 막는다. (docs/spec/solution-skeleton.md 4절 R5, 승인 결정 5)
// 결정 5: 패턴은 루트만 막는 `/cases/`다. 수용 기준 5는 이 줄로 충족한다.
// git check-ignore 같은 외부 프로세스는 부르지 않는다. 파일 텍스트만 본다.
[Trait("Rule", "R5")]
public class GitIgnoreTests
{
    // 루트 고정이 없는 패턴. 어느 깊이의 cases/도 막고,
    // core.ignorecase=true clone에서는 src/Scorecard.Core/Cases/ 같은 코드 폴더까지 막는다(스펙 R5 [I]).
    private static readonly string[] UnanchoredCasesPatterns =
    [
        "cases/",
        "cases",
        "**/cases/",
        "**/cases",
    ];

    [Fact]
    public void Gitignore_has_root_cases_line()
    {
        // 앞뒤 공백을 뺀 줄이 정확히 `/cases/`와 같아야 한다. ReadAllLines가 \n, \r\n을 모두 끊는다.
        Assert.Contains("/cases/", ReadTrimmedLines());
    }

    [Fact]
    public void Gitignore_has_no_unanchored_cases_pattern()
    {
        var found = ReadTrimmedLines()
            .Where(l => UnanchoredCasesPatterns.Contains(l, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(found);
    }

    private static string[] ReadTrimmedLines() =>
        File.ReadAllLines(RepoRoot.Combine(".gitignore")).Select(l => l.Trim()).ToArray();
}
