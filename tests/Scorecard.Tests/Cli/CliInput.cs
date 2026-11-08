using System.Text;
using Scorecard.Cli;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Classification;
using Scorecard.Tests.Synthetic;

namespace Scorecard.Tests.Cli;

// 명령 한 번의 결과. 표준 출력·표준 오류는 받은 바이트 그대로다.
internal sealed record CliRun(int Exit, byte[] Stdout, byte[] Stderr)
{
    public string Out => CliInput.Text(Stdout);

    public string Err => CliInput.Text(Stderr);
}

// 명령 테스트 도우미. 프로세스를 띄우지 않고 CliApp.Run을 부른다(json-output.md 7절, G1 N2·N3).
internal static class CliInput
{
    public const string SpecPath = "docs/spec/data/json-output.md";

    public const string CommandTableHeader = "| 명령 | 도움말 | 옵션 | 표준 출력(`--json`) | 쓰는 파일 |";
    public const string CliErrorTableHeader = "| 코드 | 언제 | 종료 코드 |";
    public const string ArgumentTableHeader = "| 인자 | 종료 코드 | 코드 | 출처 | 경로 | 메시지 |";
    public const string GuardTableHeader = "| 이름 | 샘플(`TestData/guard/…`) | 어댑터 |";

    public const string HelpHeading = "### 2.1 도움말";
    public const string ResultHeading = "### 5.2 결과 객체";
    public const string ErrorJsonHeading = "### 6.1 오류 JSON";

    // 공개 예제 5종(examples/ 폴더 전부).
    public static TheoryData<string> AllExamples => new(ClassificationInput.AllExamples);

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static string Text(byte[] bytes) => Utf8NoBom.GetString(bytes);

    public static CliRun Run(params string[] args)
    {
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        var exit = CliApp.Run(args, stdout, stderr);
        return new CliRun(exit, stdout.ToArray(), stderr.ToArray());
    }

    public static string Example(string example) => SyntheticInput.ExampleDirectory(example);

    public static byte[] Expected(string example, string file) => SyntheticInput.ExampleBytes(example, "expected/" + file);

    // 데이터 표. 머리 행이 문서에 한 번만 있어야 한다.
    public static IReadOnlyList<string[]> Table(string header) => SpecDocument.Table(SpecPath, header);

    public static IReadOnlyList<string> Blocks(string heading) => SpecDocument.CodeBlocks(SpecPath, heading);

    // 표 칸 값. `코드` 표시를 떼고, "—"는 빈 문자열이다.
    public static string Cell(string cell)
    {
        var value = SpecDocument.Unquote(cell);
        return value == "—" ? string.Empty : value;
    }

    // "`a`, `b`" 칸 → [a, b]. "—"는 빈 목록.
    public static string[] List(string cell) =>
        Cell(cell).Length == 0 ? [] : SpecDocument.CodeSpans(cell);
}

// 임시 폴더. 예제 하나를 case/에 복사한다. 저장소의 예제 폴더에는 쓰지 않는다.
internal sealed class CliWorkspace : IDisposable
{
    private CliWorkspace(string root)
    {
        Root = root;
    }

    public string Root { get; }

    public string Case => Path.Combine(Root, "case");

    public string PathOf(string name) => Path.Combine(Root, name);

    public static CliWorkspace Create(string example = "table-compare", string? root = null)
    {
        var workspace = new CliWorkspace(root ?? Directory.CreateTempSubdirectory("scorecard-cli-").FullName);
        var source = SyntheticInput.ExampleDirectory(example);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var to = Path.Combine(workspace.Case, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.WriteAllBytes(to, File.ReadAllBytes(file));
        }

        return workspace;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
