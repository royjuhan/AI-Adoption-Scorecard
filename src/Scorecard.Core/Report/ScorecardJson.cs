using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;
using Scorecard.Core.Judgment;

namespace Scorecard.Core.Report;

// scorecard.json = open --json 표준 출력. docs/spec/report.md 3절, 데이터 3절 속성 표.
// 바이트 규칙은 classification.md 3.3과 같다. 앞 세 키(schema_version, command, files)는 json-output.md 5.2 결과 객체와 같다.
public static class ScorecardJson
{
    public const string FileName = "scorecard.json";
    public const int SchemaVersion = ReportRules.SchemaVersion;

    private static readonly JsonWriterOptions Options = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static byte[] Render(ScorecardResult result, string command, IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(files);

        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer, Options))
        {
            w.WriteStartObject();
            w.WriteNumber("schema_version", SchemaVersion);
            w.WriteString("command", command);
            Strings(w, "files", files);
            w.WriteString("case", result.Case);
            NullableString(w, "task", result.Task);
            NullableString(w, "intake", result.Intake);
            w.WriteString("answers", result.Answers);
            Summary(w, result.Summary);
            w.WriteStartArray("steps");
            foreach (var s in result.Steps)
            {
                Step(w, s);
            }

            w.WriteEndArray();
            if (result.Proposal is { } p)
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

            Verification(w, result.Verification);
            w.WriteStartArray("gates");
            foreach (var g in result.Gates)
            {
                w.WriteStartObject();
                w.WriteNumber("step", g.Step);
                w.WriteString("name", g.Name);
                w.WriteString("kind", g.Kind);
                w.WriteString("reason", g.Reason);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            Notes(w, "risks", result.Risks);
            Notes(w, "todo", result.Todo);
            Hashes(w, "inputs", result.Inputs);
            Hashes(w, "results", result.Results);
            if (result.Review is { } review)
            {
                w.WritePropertyName("review");
                Hash(w, review);
            }
            else
            {
                w.WriteNull("review");
            }

            w.WriteEndObject();
        }

        var bytes = new byte[buffer.WrittenCount + 1];
        buffer.WrittenSpan.CopyTo(bytes);
        bytes[^1] = (byte)'\n';
        return bytes;
    }

    private static void Summary(Utf8JsonWriter w, ScorecardSummary s)
    {
        w.WriteStartObject("summary");
        w.WriteNumber("steps", s.Steps);
        w.WriteNumber("software", s.Software);
        w.WriteStartArray("assignees");
        foreach (var a in s.Assignees)
        {
            w.WriteStartObject();
            w.WriteString("assignee", a.Assignee);
            w.WriteNumber("count", a.Count);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteNumber("differs", s.Differs);
        w.WriteNumber("ai_assist", s.AiAssist);
        w.WriteNumber("ai_overuse", s.AiOveruse);
        w.WriteNumber("kept", s.Kept);
        w.WriteNumber("changed", s.Changed);
        w.WriteString("saving", s.Saving);
        w.WriteEndObject();
    }

    private static void Step(Utf8JsonWriter w, ScorecardStep s)
    {
        w.WriteStartObject();
        w.WriteNumber("number", s.Number);
        w.WriteString("name", s.Name);
        w.WriteString("source", s.Source);
        w.WriteString("assignee", s.Assignee);
        w.WriteString("rule", s.Rule);
        w.WriteString("reason", s.Reason);
        w.WriteStartObject("rule_opinion");
        w.WriteString("assignee", s.RuleOpinion.Assignee);
        w.WriteString("rule", s.RuleOpinion.Rule);
        w.WriteEndObject();
        w.WriteBoolean("differs", s.Differs);
        NullableString(w, "current_actor", s.CurrentActor);
        Strings(w, "defaults", s.Defaults);
        Strings(w, "inferred", s.Inferred);
        Strings(w, "checks", s.Checks);
        w.WriteBoolean("ai_overuse_candidate", s.AiOveruseCandidate);
        w.WriteStartArray("extra");
        foreach (var r in s.Extra)
        {
            w.WriteStartObject();
            w.WriteString("kind", r.Kind);
            w.WriteString("assignee", r.Assignee);
            w.WriteString("rule", r.Rule);
            w.WriteString("reason", r.Reason);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteStartObject("review");
        w.WriteString("status", s.Review.Status);
        NullableString(w, "assignee", s.Review.Assignee);
        NullableString(w, "reason", s.Review.Reason);
        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void Verification(Utf8JsonWriter w, ScorecardVerification v)
    {
        w.WriteStartObject("verification");
        w.WriteString("status", v.Status);
        NullableString(w, "reason", v.Reason);
        if (v.Counts is { } c)
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

        NullableNumber(w, "accuracy_pct", v.AccuracyPct);
        NullableNumber(w, "missing_rate_pct", v.MissingRatePct);
        NullableString(w, "verdict", v.Verdict);
        NullableString(w, "verdict_rule", v.VerdictRule);
        NullableString(w, "verdict_reason", v.VerdictReason);
        if (v.Thresholds is { } t)
        {
            w.WriteStartObject("thresholds");
            w.WriteNumber("accuracy_min", t.AccuracyMin);
            w.WriteNumber("missing_max", t.MissingMax);
            w.WriteBoolean("time_must_drop", t.TimeMustDrop);
            if (t.ScoreGapMax is { } gap)
            {
                w.WriteNumber("score_gap_max", gap);
            }

            w.WriteEndObject();
        }
        else
        {
            w.WriteNull("thresholds");
        }

        if (v.Time is { } time)
        {
            w.WritePropertyName("time");
            Check(w, time);
        }
        else
        {
            w.WriteNull("time");
        }

        w.WriteStartArray("checks");
        foreach (var check in v.Checks)
        {
            Check(w, check);
        }

        w.WriteEndArray();
        w.WriteStartArray("items");
        foreach (var item in v.Items)
        {
            w.WriteStartObject();
            w.WriteString("key", item.Key);
            w.WriteString("status", item.Status);
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
        w.WriteNumber("items_hidden", v.ItemsHidden);
        w.WriteEndObject();
    }

    private static void Check(Utf8JsonWriter w, JudgmentCheck check)
    {
        w.WriteStartObject();
        w.WriteString("rule", check.Rule);
        w.WriteString("result", check.Result);
        w.WriteString("reason", check.Reason);
        w.WriteEndObject();
    }

    private static void Notes(Utf8JsonWriter w, string name, IReadOnlyList<ScorecardNote> notes)
    {
        w.WriteStartArray(name);
        foreach (var n in notes)
        {
            w.WriteStartObject();
            w.WriteString("key", n.Key);
            w.WriteNumber("step", n.Step);
            w.WriteString("text", n.Text);
            w.WriteEndObject();
        }

        w.WriteEndArray();
    }

    private static void Hashes(Utf8JsonWriter w, string name, IReadOnlyList<FileHash> hashes)
    {
        w.WriteStartArray(name);
        foreach (var h in hashes)
        {
            Hash(w, h);
        }

        w.WriteEndArray();
    }

    private static void Hash(Utf8JsonWriter w, FileHash h)
    {
        w.WriteStartObject();
        w.WriteString("path", h.Path);
        w.WriteString("sha256", h.Sha256);
        w.WriteEndObject();
    }

    private static void Strings(Utf8JsonWriter w, string name, IReadOnlyList<string> values)
    {
        w.WriteStartArray(name);
        foreach (var value in values)
        {
            w.WriteStringValue(value);
        }

        w.WriteEndArray();
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
        if (value is { } v)
        {
            w.WriteNumber(name, v);
        }
        else
        {
            w.WriteNull(name);
        }
    }
}
