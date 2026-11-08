using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Scorecard.Core.Cases;

namespace Scorecard.Cli;

// Cli 오류 코드. docs/spec/data/json-output.md 6.2 표.
internal static class CliErrors
{
    public const string Argument = "argument";
    public const string File = "file";
    public const string Output = "output";
    public const string Internal = "internal";
}

// 결과 객체·오류 JSON 바이트. docs/spec/json-output.md 5·6절.
// 바이트 규칙은 classification.md 3.3과 같다: 2칸 들여쓰기, LF, 끝 "\n" 하나, UTF-8(BOM 없음), UnsafeRelaxedJsonEscaping.
// 판정·숫자가 없어 Cli에 둔다(9절 7). 첫 키는 정수 schema_version.
internal static class CliJson
{
    public const int SchemaVersion = 1;

    public static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonWriterOptions Options = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // 사용자 명령의 결과 객체(5.2). files는 출력 폴더 기준 상대 경로.
    public static byte[] Result(string command, IReadOnlyList<string> files) =>
        Render(w =>
        {
            w.WriteString("command", command);
            WriteStrings(w, "files", files);
        });

    // help --json(5.2).
    public static byte[] Help(IReadOnlyList<string> commands) =>
        Render(w =>
        {
            w.WriteString("command", CommandLine.HelpCommand);
            WriteStrings(w, "commands", commands);
        });

    // 오류 JSON(6.1). 원소는 CaseError 여섯 값, 순서 code, source, line, column, path, message.
    public static byte[] Errors(int exitCode, IReadOnlyList<CaseError> errors) =>
        Render(w =>
        {
            w.WriteNumber("exit_code", exitCode);
            w.WriteStartArray("errors");
            foreach (var e in errors)
            {
                w.WriteStartObject();
                w.WriteString("code", e.Code);
                w.WriteString("source", e.Source);
                w.WriteNumber("line", e.Line);
                w.WriteNumber("column", e.Column);
                w.WriteString("path", e.Path);
                w.WriteString("message", e.Message);
                w.WriteEndObject();
            }

            w.WriteEndArray();
        });

    private static byte[] Render(Action<Utf8JsonWriter> body)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer, Options))
        {
            w.WriteStartObject();
            w.WriteNumber("schema_version", SchemaVersion);
            body(w);
            w.WriteEndObject();
        }

        var bytes = new byte[buffer.WrittenCount + 1];
        buffer.WrittenSpan.CopyTo(bytes);
        bytes[^1] = (byte)'\n';
        return bytes;
    }

    private static void WriteStrings(Utf8JsonWriter w, string name, IReadOnlyList<string> values)
    {
        w.WriteStartArray(name);
        foreach (var value in values)
        {
            w.WriteStringValue(value);
        }

        w.WriteEndArray();
    }
}
