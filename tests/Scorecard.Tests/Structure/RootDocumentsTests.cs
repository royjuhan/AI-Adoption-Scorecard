namespace Scorecard.Tests.Structure;

// T-001 R4: 루트 문서 4개가 있다. (docs/spec/solution-skeleton.md 4절 R4)
// 제목·섹션 문구는 검사하지 않는다. 스펙 표의 통과 조건만 본다.
[Trait("Rule", "R4")]
public class RootDocumentsTests
{
    // MIT 본문. 줄바꿈 위치는 상관없이 공백을 하나로 모아 비교한다.
    private const string MitPermissionText =
        "Permission is hereby granted, free of charge, to any person obtaining a copy "
        + "of this software and associated documentation files (the \"Software\"), to deal "
        + "in the Software without restriction, including without limitation the rights "
        + "to use, copy, modify, merge, publish, distribute, sublicense, and/or sell "
        + "copies of the Software, and to permit persons to whom the Software is "
        + "furnished to do so, subject to the following conditions: "
        + "The above copyright notice and this permission notice shall be included in all "
        + "copies or substantial portions of the Software. "
        + "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR "
        + "IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, "
        + "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE "
        + "AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER "
        + "LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, "
        + "OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE "
        + "SOFTWARE.";

    // 이름은 대소문자까지 정확히 맞아야 한다. Linux clone에서는 대소문자가 다르면 링크가 깨진다.
    [Theory]
    [InlineData("LICENSE")]
    [InlineData("README.md")]
    [InlineData("README.ko.md")]
    [InlineData("CONTRIBUTING.md")]
    public void Root_document_exists_with_exact_name(string fileName)
    {
        Assert.Contains(fileName, RepoRoot.FileNames());
    }

    [Fact]
    public void License_first_line_is_MIT_License()
    {
        var firstLine = File.ReadLines(RepoRoot.Combine("LICENSE")).FirstOrDefault();

        Assert.Equal("MIT License", firstLine?.Trim());
    }

    [Fact]
    public void License_contains_MIT_notice_and_permission_text()
    {
        var lines = File.ReadAllLines(RepoRoot.Combine("LICENSE"));

        // MIT 양식의 저작권 줄. 이름·연도는 검사하지 않는다(승인 결정 9).
        Assert.Contains(lines, l => l.TrimStart().StartsWith("Copyright (c) ", StringComparison.Ordinal));
        Assert.Contains(MitPermissionText, CollapseWhitespace(string.Join('\n', lines)));
    }

    [Fact]
    public void Readme_links_to_Korean_readme()
    {
        var text = File.ReadAllText(RepoRoot.Combine("README.md"));

        Assert.Matches(@"\]\((\./)?README\.ko\.md\)", text);
    }

    [Fact]
    public void Korean_readme_links_to_English_readme()
    {
        var text = File.ReadAllText(RepoRoot.Combine("README.ko.md"));

        Assert.Matches(@"\]\((\./)?README\.md\)", text);
    }

    // 줄바꿈(\n, \r\n)과 연속 공백을 공백 하나로 바꾼다.
    private static string CollapseWhitespace(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
