using System.Text;

namespace Scorecard.Core.Cases;

// case.yaml을 읽고 검증한다(docs/spec/case-schema.md).
// 순서: 바이트 → 문자열(4.8) → YAML 트리(4.7) → 스키마 검사(4.3~4.6) → 결과(4.12).
// 입력은 읽기만 한다. 파일을 쓰지 않는다. 결과에 절대 경로를 넣지 않는다(4.10).
public static class CaseParser
{
    public const string FileName = "case.yaml";

    // BOM을 쓰지 않고, 잘못된 바이트에서 예외를 던진다. StreamReader·Encoding.UTF8의 BOM 처리 함정을 피한다(4.8).
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    // <caseDirectory>/case.yaml을 읽는다. 오류의 Source는 "case.yaml"이다. 폴더의 다른 파일은 보지 않는다.
    public static CaseParseResult ParseDirectory(string caseDirectory)
    {
        ArgumentNullException.ThrowIfNull(caseDirectory);

        byte[] content;
        try
        {
            content = File.ReadAllBytes(Path.Combine(caseDirectory, FileName));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return FileError("파일이 없다");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 예외 Message에는 절대 경로가 있다. 타입 이름만 쓴다.
            return FileError("파일을 읽을 수 없다: " + ex.GetType().Name);
        }

        return Parse(content, FileName);
    }

    // 바이트를 검증한다. sourceName은 오류의 Source(상대 이름)다. content는 바꾸지 않는다.
    public static CaseParseResult Parse(byte[] content, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(sourceName);

        var errors = new ErrorSink(sourceName);
        var text = Decode(content, errors);
        var root = text is null ? null : YamlTree.Read(text, errors);
        var definition = root is null ? null : new CaseReader(errors).Read(root);
        return errors.Count == 0 && definition is not null
            ? new CaseParseResult(definition, [])
            : new CaseParseResult(null, errors.Sorted());
    }

    private static CaseParseResult FileError(string message) =>
        new(null, [new CaseError(FileName, 0, 0, ErrorCode.File, string.Empty, message)]);

    // UTF-8만 받는다. BOM 하나는 뗀다. UTF-16 BOM과 잘못된 바이트는 encoding 오류다(4.8).
    // 잘못된 바이트의 줄은 앞선 LF 수 + 1이다. 열은 없다. 바이트 위치는 파일 처음부터 0으로 센다(BOM 포함).
    private static string? Decode(byte[] content, ErrorSink errors)
    {
        if (content is [0xFF, 0xFE, ..] or [0xFE, 0xFF, ..])
        {
            errors.Fatal(1, 0, ErrorCode.Encoding, string.Empty, "UTF-16이다. UTF-8로 저장한다");
            return null;
        }

        var start = content is [0xEF, 0xBB, 0xBF, ..] ? 3 : 0;
        try
        {
            return StrictUtf8.GetString(content, start, content.Length - start);
        }
        catch (DecoderFallbackException ex)
        {
            var offset = Math.Clamp(start + ex.Index, 0, content.Length);
            var line = 1 + content.AsSpan(0, offset).Count((byte)'\n');
            errors.Fatal(line, 0, ErrorCode.Encoding, string.Empty, FormattableString.Invariant($"UTF-8이 아니다 (바이트 {offset})"));
            return null;
        }
    }
}
