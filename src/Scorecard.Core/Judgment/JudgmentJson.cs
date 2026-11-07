using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Scorecard.Core.Judgment;

// judgment.json 바이트. docs/spec/judgment.md 3.2·3.3. 바이트 규칙은 classification.md 3.3·metrics.md 3.3과 같다.
// 속성 순서: schema_version(첫 키, 정수) → case → verdict → rule → reason → thresholds → defaults → checks → todo.
// Utf8JsonWriter로 순서를 직접 정한다. 들여쓰기 2칸, "\n", UnsafeRelaxedJsonEscaping, UTF-8(BOM 없음), 끝에 "\n".
// 문턱 숫자는 decimal 그대로 쓴다(자릿수 유지). 경로·입력 해시·대조 셈·시간 원본·사람 입력 원본은 쓰지 않는다.
public static class JudgmentJson
{
    public const string FileName = "judgment.json";
    public const int SchemaVersion = 1;

    private static readonly JsonWriterOptions Options = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // 3.2·3.3 바이트 규칙.
    public static byte[] Render(JudgmentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer, Options))
        {
            w.WriteStartObject();
            w.WriteNumber("schema_version", SchemaVersion);
            w.WriteString("case", result.Case);
            w.WriteString("verdict", result.Verdict);
            w.WriteString("rule", result.Rule);
            w.WriteString("reason", result.Reason);
            w.WriteStartObject("thresholds");
            w.WriteNumber("accuracy_min", result.Thresholds.AccuracyMin);
            w.WriteNumber("missing_max", result.Thresholds.MissingMax);
            w.WriteBoolean("time_must_drop", result.Thresholds.TimeMustDrop);
            w.WriteEndObject();
            w.WriteStartArray("defaults");
            foreach (var key in result.Defaults)
            {
                w.WriteStringValue(key);
            }

            w.WriteEndArray();
            w.WriteStartArray("checks");
            foreach (var check in result.Checks)
            {
                w.WriteStartObject();
                w.WriteString("rule", check.Rule);
                w.WriteString("result", check.Result);
                w.WriteString("reason", check.Reason);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartArray("todo");
            foreach (var todo in result.Todo)
            {
                w.WriteStartObject();
                w.WriteString("key", todo.Key);
                w.WriteString("text", todo.Text);
                w.WriteEndObject();
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
}
