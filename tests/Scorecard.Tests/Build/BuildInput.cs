using System.Text;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Cli;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.Build;

// 제작 의뢰서 번들 테스트 도우미(docs/spec/build-request.md, 데이터 docs/spec/data/build-request.md).
// 골든은 tests TestData/build/<예제>/에 둔다. examples/*/expected가 아니다(expected migration이 아니다).
internal static class BuildInput
{
    public const string SpecPath = "docs/spec/data/build-request.md";

    public const string FileTableHeader = "| 파일 | 형식 | 내용 |";
    public const string SectionTableHeader = "| `spec.md` 절 | 내용 |";
    public const string ColumnTableHeader = "| 역할 | 어디서 | 형식 | 순서 |";
    public const string SampleTableHeader = "| 열 | 값(행 i = 1~10) |";
    public const string GateTableHeader = "| 질문 | 언제 | 답할 때 볼 것 |";
    public const string StatusTableHeader = "| 상태 | 언제 | 코드 | 메시지 |";
    public const string ErrorTableHeader = "| 인자 | start | 코드 | 출처 | 경로 | 메시지 |";
    public const string ExampleTableHeader = "| 예제 | 추천 단계 | 고를 수 있는 단계 | 번들 파일 수 |";

    public const string ResultHeading = "### 3.1 결과 객체";
    public const string IoSchemaHeading = "### 4.2 spec.md, io-schema.json";
    public const string SampleHeading = "### 4.3 samples/";
    public const string ContextHeading = "### 4.4 acceptance.md, context.json";
    public const string ApiHeading = "## 8. 공개 API";

    // 사람 확인 줄. 테스트가 사람 대신 적는 값은 역할 이름이다(실제 이름이 아니다).
    public const string SignaturePrefix = "확인한 사람:";
    public const string Signer = "검토자";

    public const string UpdateVariable = "SCORECARD_UPDATE_GOLDEN";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static IReadOnlyList<string[]> Table(string header) => SpecDocument.Table(SpecPath, header);

    public static IReadOnlyList<string> Blocks(string heading) => SpecDocument.CodeBlocks(SpecPath, heading);

    public static string GoldenPath(string example, string file) => RepoRoot.Combine("tests/Scorecard.Tests/TestData/build/" + example + "/" + file);

    // 데이터 4.7 표: 예제 → 추천 단계(= 골든에서 고르는 단계).
    public static int ProposedStep(string example) =>
        int.Parse(Table(ExampleTableHeader).Single(r => SpecDocument.Unquote(r[0]) == example)[1], System.Globalization.CultureInfo.InvariantCulture);

    // start → check → open. 출력 폴더를 돌려준다.
    public static string Prepare(CliWorkspace work, string output, bool json = true)
    {
        string[] tail = json ? ["--out", output, "--json"] : ["--out", output];
        Assert.Equal(0, CliInput.Run(["start", work.Case, "--answers", Path.Combine(work.Case, "answers.yaml"), .. tail]).Exit);
        Assert.Equal(0, CliInput.Run(["check", work.Case, .. tail]).Exit);
        Assert.Equal(0, CliInput.Run(["open", work.Case, .. tail]).Exit);
        return output;
    }

    // 출력 폴더 export-check.md의 확인 줄에 적는다(사람이 하는 일). 다른 줄은 그대로다.
    public static void Sign(string output, string signer = Signer)
    {
        var path = Path.Combine(output, "export-check.md");
        var lines = Utf8.GetString(File.ReadAllBytes(path)).Split('\n');
        var index = Array.FindIndex(lines, l => l == SignaturePrefix);
        Assert.True(index >= 0, "초안에 빈 확인 줄이 있어야 한다.");
        lines[index] = SignaturePrefix + " " + signer;
        File.WriteAllBytes(path, Utf8.GetBytes(string.Join('\n', lines)));
    }

    // 폴더 안 파일(상대 경로 '/', 서수 순) → 바이트.
    public static SortedDictionary<string, byte[]> Files(string folder) =>
        new(Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(folder, f).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllBytes), StringComparer.Ordinal);

    public static string Text(byte[] bytes) => Utf8.GetString(bytes);

    public static bool Updating => Environment.GetEnvironmentVariable(UpdateVariable) == "1";

    // 골든과 바이트 비교. SCORECARD_UPDATE_GOLDEN=1이면 다시 쓴다. 그때는 BuildEndToEndTests.Golden_update_mode_is_off가 실패한다
    // (사람이 diff를 본 뒤 변수 없이 다시 돌린다).
    public static void AssertGolden(string example, string file, byte[] actual)
    {
        var path = GoldenPath(example, file);
        if (Updating)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, actual);
            return;
        }

        Assert.True(File.Exists(path), $"골든이 없다: {path}");
        Assert.Equal(Text(File.ReadAllBytes(path)), Text(actual));
        Assert.Equal(File.ReadAllBytes(path), actual);
    }
}
