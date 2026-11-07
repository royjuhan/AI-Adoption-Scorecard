using System.Text;
using Scorecard.Core.Cases;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.CaseSchema;

// 파서 테스트 도우미. 입력 바이트를 만들고, 결과를 오류 줄로 바꾼다.
// 테스트 안 YAML은 LF로 쓴다. BOM·CRLF·CR 변형은 이 바이트에서 만든다(스펙 7.1).
internal static class CaseInput
{
    public const string SamplesRelativePath = "tests/Scorecard.Tests/TestData/case-schema";

    // 단계 하나짜리 문서. 아래에 answers 줄을 붙이면 첫 답이 6줄째, 들여쓰기 6칸이다.
    public const string OneStepWithAnswers = "name: a\ntask: t\nsteps:\n  - name: s\n    answers:\n";

    // 단계 하나짜리 최소 문서. 아래에 최상위 키를 붙이면 5줄째부터다.
    public const string OneStep = "name: a\ntask: t\nsteps:\n  - name: s\n";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static byte[] Bytes(string yaml) => Utf8NoBom.GetBytes(yaml);

    public static string SamplePath(string name) => RepoRoot.Combine($"{SamplesRelativePath}/{name}/case.yaml");

    public static string SampleDirectory(string name) => RepoRoot.Combine($"{SamplesRelativePath}/{name}");

    public static byte[] Sample(string name) => File.ReadAllBytes(SamplePath(name));

    public static string SampleText(string name) => Utf8NoBom.GetString(Sample(name));

    public static CaseParseResult Parse(string yaml) => CaseParser.Parse(Bytes(yaml), CaseParser.FileName);

    public static CaseParseResult Parse(byte[] bytes) => CaseParser.Parse(bytes, CaseParser.FileName);

    // 같은 스레드에서 동기로 읽고, 그동안 이 스레드가 할당한 바이트 수를 함께 돌려준다.
    // 할당량은 컴퓨터 속도와 무관하다. 스레드 단위라서 병렬로 도는 다른 테스트의 할당이 섞이지 않는다.
    // Task.Run 안에서 불러도 된다. Parse가 동기라서 처음부터 끝까지 한 스레드에서 돈다.
    public static (CaseParseResult Result, long AllocatedBytes) ParseCountingAllocations(byte[] bytes)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = Parse(bytes);
        return (result, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    // 오류 줄(ToString) 목록.
    public static string[] Lines(CaseParseResult result) => result.Errors.Select(e => e.ToString()).ToArray();

    // 오류가 없어야 한다. 있으면 오류 줄을 보이고 실패한다.
    public static CaseDefinition Valid(CaseParseResult result)
    {
        if (result.Errors.Count > 0)
        {
            Assert.Fail("오류가 없어야 한다:\n  " + string.Join("\n  ", Lines(result)));
        }

        Assert.True(result.IsValid);
        Assert.NotNull(result.Case);
        return result.Case;
    }

    public static CaseDefinition Valid(string yaml) => Valid(Parse(yaml));

    // 오류 줄이 expected와 순서까지 같아야 한다. 오류가 있으면 모델은 없다(스펙 4.12).
    public static void AssertErrors(CaseParseResult result, params string[] expected)
    {
        Assert.Equal(expected, Lines(result));
        Assert.False(result.IsValid);
        Assert.Null(result.Case);
    }

    public static void AssertErrors(string yaml, params string[] expected) => AssertErrors(Parse(yaml), expected);

    // 단계 하나짜리 문서의 첫 답(answers 아래 6줄째) 값 위치. "case.yaml:6:<열>".
    public static string AnswerPosition(string key) => $"case.yaml:6:{6 + key.Length + 2 + 1}";

    // 테스트 임시 폴더를 지운다. 정리 실패는 판정과 무관하다(GuardWorkspace.Dispose와 같은 방식).
    // Windows에서 백신·색인이 파일을 잠깐 잡으면 IOException·UnauthorizedAccessException이 난다. 남아도 다른 실행에 영향이 없다.
    public static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
