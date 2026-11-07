using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Scorecard.Core.Classification;

// classification.json 바이트. docs/spec/classification.md 3.2·3.3.
// Utf8JsonWriter로 속성 순서를 직접 정한다. 리플렉션 직렬화는 쓰지 않는다.
// 들여쓰기 2칸, 줄바꿈 "\n", UnsafeRelaxedJsonEscaping, UTF-8(BOM 없음), 마지막 } 뒤에 "\n" 하나.
// 첫 키는 schema_version(정수, ADR 0004 결정 2).
// 경로·입력 해시·Used는 쓰지 않는다. 문자열은 케이스 이름, 단계 이름, 고정 값, 근거 문장뿐이다.
public static class ClassificationJson
{
    public const string FileName = "classification.json";
    public const int SchemaVersion = 1;

    private static readonly JsonWriterOptions Options = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static byte[] Render(ClassificationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer, Options))
        {
            w.WriteStartObject();
            w.WriteNumber("schema_version", SchemaVersion);
            w.WriteString("case", result.Case);
            w.WriteStartArray("steps");
            foreach (var step in result.Steps)
            {
                WriteStep(w, step);
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        // Utf8JsonWriter는 끝 줄바꿈을 쓰지 않는다. "\n" 하나를 더한다.
        var bytes = new byte[buffer.WrittenCount + 1];
        buffer.WrittenSpan.CopyTo(bytes);
        bytes[^1] = (byte)'\n';
        return bytes;
    }

    private static void WriteStep(Utf8JsonWriter w, StepClassification step)
    {
        w.WriteStartObject();
        w.WriteNumber("number", step.Number);
        w.WriteString("name", step.Name);
        WriteStrings(w, "defaults", step.Defaults);
        WriteStrings(w, "checks", step.Checks);
        w.WriteBoolean("ai_overuse_candidate", step.AiOveruseCandidate);
        w.WriteStartArray("rows");
        foreach (var row in step.Rows)
        {
            w.WriteStartObject();
            w.WriteString("kind", row.Kind);
            w.WriteString("assignee", row.Assignee);
            w.WriteString("rule", row.Rule);
            w.WriteString("reason", row.Reason);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
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
