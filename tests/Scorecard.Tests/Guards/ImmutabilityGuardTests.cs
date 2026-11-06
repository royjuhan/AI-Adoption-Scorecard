using Scorecard.Tests.Structure;

namespace Scorecard.Tests.Guards;

// T-002 G3: 처리 전후로 입력이 그대로다.
// (docs/spec/guard-tests.md 4.5~4.7, 승인 결정 7·8·10)
// 작업 폴더 전체(출력 폴더 제외)의 목록·크기·SHA-256·수정 시각을 비교한다. 카드 문구보다 엄격하다.
// 이 파일은 가짜 대상으로 도우미(GuardChecks.Immutability)의 탐지력을 본다.
// 실제 Core 대상(GuardTargets)은 CoreGuardTargetTests에서 Assert.Empty로 검사한다(T-003).
[Trait("Rule", "G3")]
public class ImmutabilityGuardTests
{
    private const string TestDataRelativePath = "tests/Scorecard.Tests/TestData";

    [Fact]
    public void Good_target_leaves_workspace_unchanged()
    {
        Assert.Empty(GuardChecks.Immutability(FakeTargets.Get("good")));
    }

    // 위반 가짜 대상 7종. 각 위반이 기대한 줄로 보고되는지 본다(스펙 4.5 표, 6.4 형식).
    [Theory]
    [InlineData("input-append", "G3 변경(크기, SHA-256, 수정 시각): input/case.yaml")]
    [InlineData("input-delete", "G3 삭제: input/notes.md")]
    [InlineData("input-new-file", "G3 추가: input/extra.tmp")]
    [InlineData("write-sibling", "G3 추가: sibling.txt")]
    [InlineData("input-rewrite-same", "G3 변경(수정 시각): input/case.yaml")]
    [InlineData("input-temp-file", "G3 변경(수정 시각): input")]
    [InlineData("input-touch-mtime", "G3 변경(수정 시각): input/case.yaml")]
    public void Fake_target_change_is_reported(string fake, string expected)
    {
        var differences = GuardChecks.Immutability(FakeTargets.Get(fake));

        Assert.Contains(expected, differences);
        Assert.DoesNotContain(differences, d => d.StartsWith("G3 예외:", StringComparison.Ordinal));
    }

    // core.autocrlf=true clone에서 텍스트 샘플이 CRLF로 바뀌지 않았는지 본다(스펙 4.7, 승인 결정 10).
    // TestData/.gitattributes(`* -text`)가 막는다. NUL 바이트가 있는 파일은 바이너리로 보고 뺀다(git과 같은 판정).
    [Fact]
    public void Sample_text_files_have_no_carriage_return()
    {
        var textFiles = Directory.EnumerateFiles(RepoRoot.Combine(TestDataRelativePath), "*", SearchOption.AllDirectories)
            .Where(f => !File.ReadAllBytes(f).AsSpan().Contains((byte)0))
            .ToArray();
        Assert.NotEmpty(textFiles);

        var withCarriageReturn = textFiles
            .Where(f => File.ReadAllBytes(f).AsSpan().Contains((byte)'\r'))
            .Select(RepoRoot.ToRelative)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(withCarriageReturn);
    }
}
