using Scorecard.Core.Cases;

namespace Scorecard.Tests.CaseSchema;

// 사람 판단 H1(T-003 reviewer, 추천안으로 미리 준비). 승인 전이다.
// 안: case.yaml은 이름이 정확히 같을 때만 읽는다. 폴더의 이름 목록을 서수로 비교한다.
// 그래서 폴더에 Case.YAML만 있으면 모든 OS에서 "파일이 없다"(스펙 4.9 file 코드 문구)다.
// 대소문자를 무시하는 파일 시스템(Windows·macOS 기본)에서 File.ReadAllBytes("case.yaml")는 Case.YAML을 연다.
// 같은 폴더가 OS에 따라 다른 결과를 내지 않게 하려는 것이다(결정론 원칙).
// 대소문자를 구분하는 파일 시스템(Linux 기본)에서는 이름을 바로 여는 구현도 이 테스트를 통과한다. 차이는 Windows CI에서 드러난다.
// 사람이 다른 안을 고르면 이 클래스(Trait Decision=H1)를 뺀다. 다른 테스트는 이 안에 기대지 않는다.
[Trait("Spec", "case-schema")]
[Trait("Decision", "H1")]
public sealed class CaseFileNameDecisionTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("scorecard-case-h1-").FullName;

    public void Dispose() => CaseInput.TryDeleteDirectory(root);

    // 이름만 대소문자가 다른 파일 하나. 내용은 정상 표본이다. 읽혔다면 오류 0건이 나와 실패한다.
    [Theory]
    [InlineData("Case.YAML")]
    [InlineData("CASE.YAML")]
    [InlineData("Case.yaml")]
    [InlineData("case.Yaml")]
    [InlineData("case.YAML")]
    public void Differently_cased_file_only_is_missing_file(string fileName)
    {
        File.WriteAllBytes(Path.Combine(root, fileName), CaseInput.Sample(SpecSamples.ValidFull));

        // 폴더 이름 목록에 그 이름 하나만 있는지 먼저 본다(서수).
        Assert.Equal([fileName], Directory.EnumerateFileSystemEntries(root).Select(p => Path.GetFileName(p)));

        CaseInput.AssertErrors(CaseParser.ParseDirectory(root), "case.yaml: file: 파일이 없다");
    }

    // 다른 대소문자 이름이 하위 폴더 이름이어도 같다. case.yaml 자리를 찾을 때 폴더 이름도 서수로 본다.
    [Fact]
    public void Differently_cased_folder_only_is_missing_file()
    {
        Directory.CreateDirectory(Path.Combine(root, "Case.Yaml"));

        CaseInput.AssertErrors(CaseParser.ParseDirectory(root), "case.yaml: file: 파일이 없다");
    }

    // 정확한 이름이면 읽는다. 대소문자만 다른 다른 파일이 옆에 있어도(같은 폴더에 둘 수 있는 파일 시스템일 때) 결과가 같다.
    [Fact]
    public void Exact_name_is_read_even_next_to_differently_cased_names()
    {
        var bytes = CaseInput.Sample(SpecSamples.ValidFull);
        File.WriteAllBytes(Path.Combine(root, CaseParser.FileName), bytes);
        var expected = ParseResultJson.Render(CaseInput.Parse(bytes));
        Assert.Equal(expected, ParseResultJson.Render(CaseParser.ParseDirectory(root)));

        // 대소문자를 무시하는 파일 시스템이면 같은 파일을 덮어쓰게 되므로 그때는 더하지 않는다.
        if (!File.Exists(Path.Combine(root, "CASE.YAML")))
        {
            File.WriteAllText(Path.Combine(root, "CASE.YAML"), "name: Bad_Name\n");
            File.WriteAllText(Path.Combine(root, "Case.yaml"), "steps: [\n");
        }

        Assert.Equal(expected, ParseResultJson.Render(CaseParser.ParseDirectory(root)));
    }
}
