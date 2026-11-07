using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Scorecard.Core.Comparison;

// metrics.json 바이트. docs/spec/metrics.md 3.2·3.3. 바이트 규칙(도구·들여쓰기·줄바꿈·인코딩·이스케이프)은 classification.md 3.3과 같다.
// 최상위 키는 schema_version(첫 키, 정수) → case → compare 순서다.
// Utf8JsonWriter로 속성 순서를 직접 정한다. 들여쓰기 2칸, "\n", UnsafeRelaxedJsonEscaping, UTF-8(BOM 없음), 끝에 "\n".
// 항목(items)은 차이가 있는 것만 쓴다: 값 불일치, 누락, 추가, 허용 오차 안의 차이가 있는 일치.
// 경로는 케이스 폴더 기준 상대 경로뿐이다. 입력 해시·정규화 전 원문·compare에 없는 열은 쓰지 않는다.
public static class MetricsJson
{
    public const string FileName = "metrics.json";

    // 파일 모양의 버전. 첫 키로 쓴다(스펙 3.2, juhan 결정 2026-11-07). 모양을 바꾸면 올린다.
    public const int SchemaVersion = 1;

    private static readonly JsonWriterOptions Options = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static byte[] Render(CompareMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer, Options))
        {
            w.WriteStartObject();
            w.WriteNumber("schema_version", SchemaVersion);
            w.WriteString("case", metrics.Case);
            w.WriteStartObject("compare");
            w.WriteString("status", metrics.Status);
            WriteStringOrNull(w, "reason", metrics.Reason);
            WriteStringOrNull(w, "key", metrics.Key);
            WriteStrings(w, "reference_files", metrics.ReferenceFiles);
            WriteStrings(w, "candidate_files", metrics.CandidateFiles);
            if (metrics.Counts is { } c)
            {
                w.WriteStartObject("counts");
                w.WriteNumber("reference", c.Reference);
                w.WriteNumber("candidate", c.Candidate);
                w.WriteNumber("match", c.Match);
                w.WriteNumber("mismatch", c.Mismatch);
                w.WriteNumber("missing", c.Missing);
                w.WriteNumber("extra", c.Extra);
                w.WriteNumber("within", c.Within);
                w.WriteEndObject();
            }
            else
            {
                w.WriteNull("counts");
            }

            WriteNumberOrNull(w, "accuracy_pct", metrics.AccuracyPct);
            WriteNumberOrNull(w, "missing_rate_pct", metrics.MissingRatePct);
            w.WriteStartArray("fields");
            foreach (var f in metrics.Fields)
            {
                w.WriteStartObject();
                w.WriteString("field", f.Field);
                w.WriteString("kind", f.Kind);
                WriteNumberOrNull(w, "tolerance", f.Tolerance);
                WriteNumberOrNull(w, "tolerance_pct", f.TolerancePct);
                w.WriteNumber("compared", f.Compared);
                w.WriteNumber("outside", f.Outside);
                w.WriteNumber("within", f.Within);
                w.WriteNumber("not_number", f.NotNumber);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartArray("items");
            foreach (var item in metrics.Items.Where(i => i.Status != CompareEngine.ItemMatch || i.Differences.Count > 0))
            {
                w.WriteStartObject();
                w.WriteString("key", item.Key);
                w.WriteString("status", item.Status);
                WriteRow(w, "reference", item.Reference);
                WriteRow(w, "candidate", item.Candidate);
                w.WriteStartArray("differences");
                foreach (var d in item.Differences)
                {
                    w.WriteStartObject();
                    w.WriteString("field", d.Field);
                    w.WriteString("result", d.Result);
                    w.WriteString("reference", d.Reference);
                    w.WriteString("candidate", d.Candidate);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
            w.WriteEndObject();
        }

        // Utf8JsonWriter는 끝 줄바꿈을 쓰지 않는다. "\n" 하나를 더한다.
        var bytes = new byte[buffer.WrittenCount + 1];
        buffer.WrittenSpan.CopyTo(bytes);
        bytes[^1] = (byte)'\n';
        return bytes;
    }

    private static void WriteRow(Utf8JsonWriter w, string name, CsvRow? row)
    {
        if (row is null)
        {
            w.WriteNull(name);
            return;
        }

        w.WriteStartObject(name);
        w.WriteString("file", row.File);
        w.WriteNumber("line", row.Line);
        w.WriteEndObject();
    }

    private static void WriteStringOrNull(Utf8JsonWriter w, string name, string? value)
    {
        if (value is null)
        {
            w.WriteNull(name);
        }
        else
        {
            w.WriteString(name, value);
        }
    }

    // decimal은 소수 자릿수를 그대로 쓴다(1.0 → 1.0, 90.0 → 90.0). 스펙 3.3.
    private static void WriteNumberOrNull(Utf8JsonWriter w, string name, decimal? value)
    {
        if (value is { } v)
        {
            w.WriteNumber(name, v);
        }
        else
        {
            w.WriteNull(name);
        }
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
