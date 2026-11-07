using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Scorecard.Core.Classification;

// T-004 스파이크 후보(머지 대상 아님). classification.json 바이트.
// Utf8JsonWriter로 속성 순서를 직접 정한다(리플렉션 직렬화 없음).
// 들여쓰기 2칸, 줄바꿈 "\n", UnsafeRelaxedJsonEscaping, UTF-8(BOM 없음), 끝에 "\n".
// 경로를 쓰지 않는다. 케이스 이름과 단계 이름(자유 서술)만 문자열로 나간다.
// Used(트리가 읽은 답)는 근거 키로 정해지므로 쓰지 않는다. 기본값 표시 판단에만 쓴다.
public static class ClassificationJson
{
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
            w.WriteString("case", result.Case);
            w.WriteStartArray("steps");
            foreach (var s in result.Steps)
            {
                w.WriteStartObject();
                w.WriteNumber("number", s.Number);
                w.WriteString("name", s.Name);
                Strings(w, "defaults", s.Defaults);
                Strings(w, "checks", s.Checks);
                w.WriteBoolean("ai_overuse_candidate", s.AiOveruseCandidate);
                w.WriteStartArray("rows");
                foreach (var r in s.Rows)
                {
                    w.WriteStartObject();
                    w.WriteString("kind", r.Kind);
                    w.WriteString("assignee", r.Assignee);
                    w.WriteString("rule", r.Rule);
                    w.WriteString("reason", r.Reason);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
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

    private static void Strings(Utf8JsonWriter w, string name, IReadOnlyList<string> values)
    {
        w.WriteStartArray(name);
        foreach (var v in values)
        {
            w.WriteStringValue(v);
        }

        w.WriteEndArray();
    }
}
