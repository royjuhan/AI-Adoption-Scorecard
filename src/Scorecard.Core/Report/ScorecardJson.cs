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
            NullableString(w, "scenario", result.Scenario);
            Status(w, result.Status);
            Conclusion(w, result.Conclusion);
            TaskSummary(w, result.TaskSummary);
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
                w.WriteString("check", p.Check);
                w.WriteString("effect", p.Effect);
                w.WriteBoolean("tentative", p.Tentative);
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
                w.WriteString("text", g.Text);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            Notes(w, "risks", result.Risks);
            Notes(w, "opinions", result.Opinions);
            Notes(w, "candidates", result.Candidates);
            w.WriteStartArray("todo");
            foreach (var t in result.Todo)
            {
                w.WriteStartObject();
                w.WriteString("group", t.Group);
                w.WriteString("key", t.Key);
                w.WriteNumber("step", t.Step);
                w.WriteString("text", t.Text);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            Strings(w, "stated_numbers", result.StatedNumbers);
            Strings(w, "open_questions", result.OpenQuestions);
            w.WriteStartArray("self_check");
            foreach (var c in result.SelfCheck)
            {
                w.WriteStartObject();
                w.WriteString("rule", c.Rule);
                w.WriteString("result", c.Result);
                NullableString(w, "note", c.Note);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            if (result.Allocation is { } allocation)
            {
                CaseAllocation(w, allocation);
            }

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
        Counts(w, "stated", s.Stated);
        Counts(w, "tentative", s.Tentative);
        w.WriteStartArray("minutes");
        foreach (var m in s.Minutes)
        {
            w.WriteStartObject();
            w.WriteNumber("step", m.Step);
            w.WriteNumber("minutes", m.Minutes);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void Counts(Utf8JsonWriter w, string name, SummaryCounts c)
    {
        w.WriteStartObject(name);
        w.WriteNumber("steps", c.Steps);
        w.WriteNumber("software", c.Software);
        w.WriteNumber("ai_assist", c.AiAssist);
        w.WriteNumber("ai_overuse", c.AiOveruse);
        w.WriteEndObject();
    }

    private static void Status(Utf8JsonWriter w, ScorecardStatus s)
    {
        w.WriteStartObject("status");
        w.WriteString("material", s.Material);
        w.WriteString("classification", s.Classification);
        w.WriteNumber("confirmed", s.Confirmed);
        w.WriteNumber("steps", s.Steps);
        NullableString(w, "confirmed_in", s.ConfirmedIn);
        w.WriteString("test", s.Test);
        w.WriteEndObject();
    }

    private static void Conclusion(Utf8JsonWriter w, ScorecardConclusion c)
    {
        w.WriteStartObject("conclusion");
        w.WriteString("keep", c.Keep);
        w.WriteString("first_check", c.FirstCheck);
        w.WriteString("undecided", c.Undecided);
        NullableString(w, "end_state", c.EndState);
        w.WriteEndObject();
    }

    private static void TaskSummary(Utf8JsonWriter w, Cases.CaseTaskSummary? s)
    {
        if (s is null)
        {
            w.WriteNull("task_summary");
            return;
        }

        w.WriteStartObject("task_summary");
        NullableString(w, "scale", s.Scale);
        NullableString(w, "current", s.Current);
        NullableString(w, "exception_rate", s.ExceptionRate);
        NullableString(w, "delivery", s.Delivery);
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
        w.WriteBoolean("tentative", s.Tentative);
        w.WriteString("now", s.Now);
        w.WriteString("change", s.Change);
        w.WriteString("check", s.Check);
        Strings(w, "assumptions", s.Assumptions);
        w.WriteStartArray("actions");
        foreach (var a in s.Actions)
        {
            w.WriteStartObject();
            w.WriteString("action", a.Action);
            w.WriteString("who_now", a.WhoNow);
            w.WriteString("proposed", a.Proposed);
            w.WriteString("source", a.Source);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        NullableString(w, "gate_note", s.GateNote);
        if (s.Allocation is { } allocation)
        {
            StepAllocation(w, allocation);
        }

        w.WriteEndObject();
    }

    // 단계 역할 배분(T-024, 데이터 3절 "있을 때만" 표). 있을 때만 gate_note 뒤에 쓴다. 안의 키는 늘 있다(없으면 null).
    private static void StepAllocation(Utf8JsonWriter w, Cases.StepAllocation a)
    {
        w.WriteStartObject("allocation");
        if (a.Roles is { } r)
        {
            w.WriteStartObject("roles");
            NullableString(w, "human", r.Human);
            NullableString(w, "ai", r.Ai);
            NullableString(w, "software", r.Software);
            w.WriteEndObject();
        }
        else
        {
            w.WriteNull("roles");
        }

        NullableString(w, "judgment", a.Judgment);
        NullableString(w, "execution", a.Execution);
        NullableString(w, "accountability", a.Accountability);
        NullableString(w, "basis", a.Basis);
        NullableString(w, "varies", a.Varies);
        w.WriteEndObject();
    }

    // 케이스 역할 배분 메모(T-024). 있을 때만 self_check 뒤에 쓴다. 답 파일 그대로다.
    private static void CaseAllocation(Utf8JsonWriter w, Cases.CaseAllocation a)
    {
        w.WriteStartObject("allocation");
        NullableString(w, "purpose", a.Purpose);
        NullableString(w, "done_when", a.DoneWhen);
        Strings(w, "facts", a.Facts);
        Strings(w, "assumptions", a.Assumptions);
        w.WriteStartArray("tools");
        foreach (var t in a.Tools)
        {
            w.WriteStartObject();
            w.WriteString("name", t.Name);
            w.WriteStartArray("steps");
            foreach (var n in t.Steps)
            {
                w.WriteNumberValue(n);
            }

            w.WriteEndArray();
            w.WriteString("function", t.Function);
            w.WriteString("input", t.Input);
            w.WriteString("process", t.Process);
            w.WriteString("output", t.Output);
            w.WriteString("reduces", t.Reduces);
            w.WriteString("exceptions", t.Exceptions);
            w.WriteString("existing", t.Existing);
            w.WriteString("new", t.New);
            w.WriteString("burden", t.Burden);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteStartArray("transitions");
        foreach (var t in a.Transitions)
        {
            w.WriteStartObject();
            w.WriteNumber("step", t.Step);
            w.WriteString("from", t.From);
            w.WriteString("to", t.To);
            w.WriteString("condition", t.Condition);
            w.WriteString("evidence", t.Evidence);
            w.WriteEndObject();
        }

        w.WriteEndArray();
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
