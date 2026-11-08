using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;
using Scorecard.Core.Report;

namespace Scorecard.Core.Build;

// 번들의 JSON 파일(io-schema.json, context.json)과 build 결과 객체(build --json). docs/spec/build-request.md 3·4절, 데이터 3절 블록.
// 바이트 규칙은 classification.md 3.3과 같다. 첫 키는 정수 schema_version.
public static class BuildRequestJson
{
    public const int SchemaVersion = BuildRequestRules.SchemaVersion;

    private static readonly JsonWriterOptions Options = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // build 결과 객체(3.1). 앞 세 키는 json-output.md 5.2와 같다. step은 --step 값(없으면 null). files는 출력 폴더 기준 상대 경로.
    public static byte[] Result(ScorecardResult scorecard, int? step, IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(scorecard);
        ArgumentNullException.ThrowIfNull(files);

        return Render(w =>
        {
            w.WriteString("command", "build");
            w.WriteStartArray("files");
            foreach (var f in files)
            {
                w.WriteStringValue(f);
            }

            w.WriteEndArray();
            w.WriteString("case", scorecard.Case);
            if (step is { } n)
            {
                w.WriteNumber("step", n);
            }
            else
            {
                w.WriteNull("step");
            }

            if (scorecard.Proposal is { } p)
            {
                w.WriteStartObject("proposal");
                w.WriteNumber("step", p.Step);
                w.WriteString("name", p.Name);
                w.WriteString("reason", p.Reason);
                w.WriteEndObject();
            }
            else
            {
                w.WriteNull("proposal");
            }

            w.WriteStartArray("candidates");
            foreach (var c in BuildRequestBuilder.Candidates(scorecard))
            {
                w.WriteNumberValue(c);
            }

            w.WriteEndArray();
        });
    }

    // io-schema.json(4.2).
    internal static byte[] IoSchema(BundleView v) => Render(w =>
    {
        w.WriteString("case", v.Scorecard.Case);
        w.WriteNumber("step", v.Step.Number);
        w.WriteStartObject("input");
        NullableString(w, "kind", v.Answers?.InputKind.Value);
        w.WriteEndObject();
        w.WriteStartObject("output");
        NullableString(w, "key", v.Columns.Count > 0 ? v.Columns[0].Name : null);
        w.WriteStartArray("columns");
        foreach (var c in v.Columns)
        {
            w.WriteStartObject();
            w.WriteString("name", c.Name);
            w.WriteString("role", c.Role);
            w.WriteString("type", c.Type);
            NullableNumber(w, "tolerance", c.Tolerance);
            NullableNumber(w, "tolerance_pct", c.TolerancePct);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
    });

    // context.json(4.4): 집계값과 스코어카드 요약. 자료 파일의 값은 없다.
    internal static byte[] Context(BundleView v) => Render(w =>
    {
        var s = v.Step;
        var a = v.Answers;
        w.WriteString("case", v.Scorecard.Case);
        NullableString(w, "task", v.Scorecard.Task);
        w.WriteStartObject("step");
        w.WriteNumber("number", s.Number);
        w.WriteString("name", s.Name);
        w.WriteString("assignee", s.Assignee);
        w.WriteString("rule", s.Rule);
        w.WriteString("source", s.Source);
        NullableString(w, "current_actor", a?.CurrentActor.Value);
        NullableString(w, "input_kind", a?.InputKind.Value);
        NullableString(w, "frequency", a?.Frequency.Value);
        if (a is not null)
        {
            w.WriteNumber("batch_size", a.BatchSize.Value);
        }
        else
        {
            w.WriteNull("batch_size");
        }

        NullableNumber(w, "exception_rate", a?.ExceptionRate.Value);
        NullableNumber(w, "minutes_per_item", a?.MinutesPerItem.Value);
        NullableString(w, "failure_impact", a?.FailureImpact.Value);
        w.WriteEndObject();

        var sum = v.Scorecard.Summary;
        w.WriteStartObject("summary");
        w.WriteNumber("steps", sum.Steps);
        w.WriteNumber("software", sum.Software);
        w.WriteNumber("differs", sum.Differs);
        w.WriteNumber("ai_assist", sum.AiAssist);
        w.WriteNumber("ai_overuse", sum.AiOveruse);
        w.WriteEndObject();

        var ver = v.Scorecard.Verification;
        w.WriteStartObject("verification");
        w.WriteString("status", ver.Status);
        if (ver.Counts is { } c)
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

        NullableNumber(w, "accuracy_pct", ver.AccuracyPct);
        NullableNumber(w, "missing_rate_pct", ver.MissingRatePct);
        NullableString(w, "verdict", ver.Verdict);
        NullableString(w, "verdict_rule", ver.VerdictRule);
        if (ver.Thresholds is { } t)
        {
            w.WriteStartObject("thresholds");
            w.WriteNumber("accuracy_min", t.AccuracyMin);
            w.WriteNumber("missing_max", t.MissingMax);
            w.WriteBoolean("time_must_drop", t.TimeMustDrop);
            w.WriteEndObject();
        }
        else
        {
            w.WriteNull("thresholds");
        }

        w.WriteEndObject();
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

    private static void NullableString(Utf8JsonWriter w, string name, string? value)
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

    private static void NullableNumber(Utf8JsonWriter w, string name, decimal? value)
    {
        if (value is { } d)
        {
            w.WriteNumber(name, d);
        }
        else
        {
            w.WriteNull(name);
        }
    }
}
