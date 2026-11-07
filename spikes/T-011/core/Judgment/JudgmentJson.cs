using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Scorecard.Core.Judgment;

// T-010 스파이크 후보(머지 대상 아님). judgment.json 바이트. docs/spec/judgment.md 3.2·3.3.
// 바이트 규칙은 classification.md 3.3과 같다: Utf8JsonWriter, 들여쓰기 2칸, "\n", UnsafeRelaxedJsonEscaping, UTF-8(BOM 없음), 끝에 "\n".
// 최상위 키 순서: schema_version → case → verdict → rule → reason → thresholds → defaults → checks → todo.
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
            foreach (var c in result.Checks)
            {
                w.WriteStartObject();
                w.WriteString("rule", c.Rule);
                w.WriteString("result", c.Result);
                w.WriteString("reason", c.Reason);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartArray("todo");
            foreach (var t in result.Todo)
            {
                w.WriteStartObject();
                w.WriteString("key", t.Key);
                w.WriteString("text", t.Text);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        var bytes = new byte[buffer.WrittenCount + 1];
        buffer.WrittenSpan.CopyTo(bytes);
        bytes[^1] = (byte)'\n';
        return bytes;
    }
}
