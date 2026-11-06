using System.Security.Cryptography;
using Scorecard.Core.Cases;

namespace Scorecard.Tests.CaseSchema;

// 스펙 2·4.9·4.10·5: ParseDirectory. 읽기만 하고, case.yaml 한 파일만 보고, 출력에 절대 경로가 없다.
// 원본 불변(입력 파일 해시가 실행 전후 같음)은 여기서 직접 보고, G3(CoreGuardTargetTests)도 본다.
[Trait("Spec", "case-schema")]
public sealed class CaseDirectoryTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("scorecard-case-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void Missing_file_is_file_error_without_absolute_path()
    {
        var result = CaseParser.ParseDirectory(root);

        CaseInput.AssertErrors(result, "case.yaml: file: 파일이 없다");
        AssertNoPath(result, root);
    }

    [Fact]
    public void Missing_directory_is_file_error_without_absolute_path()
    {
        var missing = Path.Combine(root, "no-such-folder");

        var result = CaseParser.ParseDirectory(missing);

        CaseInput.AssertErrors(result, "case.yaml: file: 파일이 없다");
        AssertNoPath(result, missing);
    }

    // 읽을 수 없음: 예외 Message에는 절대 경로가 있어서 쓰지 않고 예외 타입 이름만 쓴다.
    // case.yaml 자리에 폴더를 두면 OS와 상관없이 읽기가 실패한다.
    [Fact]
    public void Unreadable_file_reports_exception_type_name_only()
    {
        Directory.CreateDirectory(Path.Combine(root, CaseParser.FileName));

        var result = CaseParser.ParseDirectory(root);

        var error = Assert.Single(result.Errors);
        Assert.Matches("^case\\.yaml: file: 파일을 읽을 수 없다: [A-Za-z]+Exception$", error.ToString());
        Assert.Equal((0, 0, "file", string.Empty), (error.Line, error.Column, error.Code, error.Path));
        Assert.Null(result.Case);
        AssertNoPath(result, root);
    }

    // ParseDirectory의 Source는 늘 "case.yaml"이다. 폴더 경로를 결과에 넣지 않는다.
    [Fact]
    public void Errors_use_file_name_as_source()
    {
        WriteCase(CaseInput.Sample(SpecSamples.TypeErrors));

        var result = CaseParser.ParseDirectory(root);

        CaseInput.AssertErrors(result, SpecSamples.TypeErrorsErrors);
        Assert.All(result.Errors, e => Assert.Equal(CaseParser.FileName, e.Source));
        AssertNoPath(result, root);
    }

    [Fact]
    public void Directory_and_bytes_give_same_result()
    {
        var bytes = CaseInput.Sample(SpecSamples.ValidFull);
        WriteCase(bytes);

        Assert.Equal(ParseResultJson.Render(CaseInput.Parse(bytes)), ParseResultJson.Render(CaseParser.ParseDirectory(root)));
    }

    // 폴더의 다른 파일은 보지 않는다.
    [Fact]
    public void Other_files_in_folder_are_ignored()
    {
        var bytes = CaseInput.Sample(SpecSamples.ValidFull);
        WriteCase(bytes);
        var expected = ParseResultJson.Render(CaseParser.ParseDirectory(root));

        File.WriteAllText(Path.Combine(root, "case.yml"), "name: Bad\n");
        File.WriteAllText(Path.Combine(root, "answers.yaml"), "steps: [\n");
        Directory.CreateDirectory(Path.Combine(root, "reference"));
        File.WriteAllText(Path.Combine(root, "reference", "case.yaml"), "x: 1\n");

        Assert.Equal(expected, ParseResultJson.Render(CaseParser.ParseDirectory(root)));
    }

    // 원본 불변: 실행 전후 폴더 안 모든 파일의 내용 해시와 수정 시각이 같고, 새 파일이 없다.
    [Theory]
    [InlineData(SpecSamples.ValidFull)]
    [InlineData(SpecSamples.TypeErrors)]
    public void Parse_directory_leaves_input_unchanged(string sample)
    {
        WriteCase(CaseInput.Sample(sample));
        File.WriteAllText(Path.Combine(root, "notes.md"), "합성 메모\n");
        var before = Snapshot();

        _ = CaseParser.ParseDirectory(root);

        Assert.Equal(before, Snapshot());
    }

    // Parse는 넘긴 바이트 배열을 바꾸지 않는다.
    [Fact]
    public void Parse_leaves_byte_array_unchanged()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. CaseInput.Sample(SpecSamples.ValidFull)];
        var copy = bytes.ToArray();

        _ = CaseInput.Parse(bytes);

        Assert.Equal(copy, bytes);
    }

    private void WriteCase(byte[] bytes) => File.WriteAllBytes(Path.Combine(root, CaseParser.FileName), bytes);

    private string[] Snapshot() =>
        Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(p => File.Exists(p)
                ? $"{Path.GetRelativePath(root, p)} {Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p)))} {File.GetLastWriteTimeUtc(p).Ticks}"
                : $"{Path.GetRelativePath(root, p)}/ {Directory.GetLastWriteTimeUtc(p).Ticks}")
            .ToArray();

    // 결과(모델과 오류 줄)에 폴더 경로가 없다. 임시 폴더 이름 조각도 찾는다.
    private static void AssertNoPath(CaseParseResult result, string path)
    {
        var text = ParseResultJson.RenderText(result);

        Assert.DoesNotContain(path, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetFileName(Path.TrimEndingDirectorySeparator(path)), text, StringComparison.Ordinal);
    }
}
