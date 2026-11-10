using System.Text.Json;
using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;

namespace Scorecard.Core.Report;

// 저장된 결과 파일(classification.json, metrics.json, judgment.json)을 읽는다. docs/spec/report.md 2절.
// 스코어카드에 보일 값만 읽는다. 값을 다시 계산하지 않는다.
// 파일이 틀리면 그 파일에 result 오류 하나를 낸다(Source = 파일 이름). 위치는 없다.
internal static class ResultReader
{
    public const string ErrorCode = "result";

    public static (ClassificationResult? Value, CaseError? Error) Classification(byte[] bytes) =>
        Read(bytes, ClassificationJson.FileName, ClassificationJson.SchemaVersion, root => new ClassificationResult(
            Str(root, "case"),
            Arr(root, "steps").Select(ReadStep).ToArray()));

    public static (MetricsView? Value, CaseError? Error) Metrics(byte[] bytes) =>
        Read(bytes, MetricsJson.FileName, MetricsJson.SchemaVersion, root =>
        {
            var compare = Obj(root, "compare");
            var counts = compare.GetProperty("counts");
            return new MetricsView(
                Str(compare, "status"),
                NullableStr(compare, "reason"),
                counts.ValueKind == JsonValueKind.Null ? null : new CompareCounts(
                    Int(counts, "reference"), Int(counts, "candidate"), Int(counts, "match"), Int(counts, "mismatch"),
                    Int(counts, "missing"), Int(counts, "extra"), Int(counts, "within")),
                NullableDecimal(compare, "accuracy_pct"),
                NullableDecimal(compare, "missing_rate_pct"),
                Arr(compare, "items").Select(i => new VerificationItem(
                    Str(i, "key"),
                    Str(i, "status"),
                    Arr(i, "differences").Select(d => new FieldDifference(Str(d, "field"), Str(d, "result"), Str(d, "reference"), Str(d, "candidate"))).ToArray()))
                .ToArray());
        });

    public static (JudgmentResult? Value, CaseError? Error) Judgment(byte[] bytes) =>
        Read(bytes, JudgmentJson.FileName, JudgmentJson.SchemaVersion, root =>
        {
            var t = Obj(root, "thresholds");
            var thresholds = new JudgmentThresholds(
                Dec(t, "accuracy_min"),
                Dec(t, "missing_max"),
                t.GetProperty("time_must_drop").GetBoolean(),
                t.TryGetProperty("score_gap_max", out var gap) ? gap.GetDecimal() : null);
            return new JudgmentResult(
                Str(root, "case"),
                Str(root, "verdict"),
                Str(root, "rule"),
                Str(root, "reason"),
                thresholds,
                Strings(root, "defaults"),
                Arr(root, "checks").Select(c => new JudgmentCheck(Str(c, "rule"), Str(c, "result"), Str(c, "reason"))).ToArray(),
                Arr(root, "todo").Select(c => new JudgmentTodo(Str(c, "key"), Str(c, "text"))).ToArray());
        });

    private static StepClassification ReadStep(JsonElement step)
    {
        var opinion = Obj(step, "rule_opinion");
        return new StepClassification(
            Int(step, "number"),
            Str(step, "name"),
            Str(step, "source"),
            new RuleOpinion(Str(opinion, "assignee"), Str(opinion, "rule")),
            step.GetProperty("differs").GetBoolean(),
            Strings(step, "defaults"),
            [],
            Strings(step, "checks"),
            step.GetProperty("ai_overuse_candidate").GetBoolean(),
            Arr(step, "rows").Select(r => new ClassificationRow(Str(r, "kind"), Str(r, "assignee"), Str(r, "rule"), [], Str(r, "reason"))).ToArray(),
            ReadDetail(step));
    }

    // AI 선택 항목(classification.md 3.2, T-022). 적은 키만 있다. 하나도 없으면 null.
    private static StepAiDetail? ReadDetail(JsonElement step)
    {
        StepAction[] actions = step.TryGetProperty("actions", out _)
            ? Arr(step, "actions").Select(a => new StepAction(Str(a, "action"), Str(a, "who_now"), Str(a, "proposed"), Str(a, "source"))).ToArray()
            : [];
        var now = Optional(step, "now");
        var change = Optional(step, "change");
        var check = Optional(step, "check");
        var gateNote = Optional(step, "gate_note");
        var allocation = ReadAllocation(step);
        return actions.Length == 0 && now is null && change is null && check is null && gateNote is null && allocation is null
            ? null
            : new StepAiDetail(actions, now, change, check, gateNote, allocation);
    }

    // 역할 배분(classification.md 3.2, T-024). 적은 키만 있다. 하나도 없으면 null.
    private static StepAllocation? ReadAllocation(JsonElement step)
    {
        var roles = step.TryGetProperty("roles", out var r)
            ? new StepRoles(Optional(r, "human"), Optional(r, "ai"), Optional(r, "software"))
            : null;
        var judgment = Optional(step, "judgment");
        var execution = Optional(step, "execution");
        var accountability = Optional(step, "accountability");
        var basis = Optional(step, "basis");
        var varies = Optional(step, "varies");
        return roles is null && judgment is null && execution is null && accountability is null && basis is null && varies is null
            ? null
            : new StepAllocation(roles, judgment, execution, accountability, basis, varies);
    }

    private static string? Optional(JsonElement e, string name) => e.TryGetProperty(name, out _) ? Str(e, name) : null;

    // 첫 키 schema_version이 그 파일의 지금 판과 같아야 한다. 모양이 틀리면 오류 하나.
    private static (T? Value, CaseError? Error) Read<T>(byte[] bytes, string name, int schemaVersion, Func<JsonElement, T> read)
        where T : class
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schema_version", out var version)
                || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != schemaVersion)
            {
                return (null, Error(name, FormattableString.Invariant($"schema_version이 {schemaVersion}이 아니다. 같은 판의 명령으로 다시 만든다")));
            }

            return (read(root), null);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return (null, Error(name, "결과 파일 모양이 틀리다. 명령으로 다시 만든다"));
        }
    }

    private static CaseError Error(string name, string message) => new(name, 0, 0, ErrorCode, string.Empty, message);

    private static JsonElement Obj(JsonElement e, string name)
    {
        var value = e.GetProperty(name);
        return value.ValueKind == JsonValueKind.Object ? value : throw new FormatException(name);
    }

    private static JsonElement.ArrayEnumerator Arr(JsonElement e, string name)
    {
        var value = e.GetProperty(name);
        return value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : throw new FormatException(name);
    }

    private static string Str(JsonElement e, string name) => e.GetProperty(name).GetString() ?? throw new FormatException(name);

    private static string? NullableStr(JsonElement e, string name)
    {
        var value = e.GetProperty(name);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
    }

    private static int Int(JsonElement e, string name) => e.GetProperty(name).GetInt32();

    private static decimal Dec(JsonElement e, string name) => e.GetProperty(name).GetDecimal();

    private static decimal? NullableDecimal(JsonElement e, string name)
    {
        var value = e.GetProperty(name);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetDecimal();
    }

    private static string[] Strings(JsonElement e, string name) => Arr(e, name).Select(v => v.GetString() ?? throw new FormatException(name)).ToArray();
}

// metrics.json에서 스코어카드가 쓰는 값. Items는 파일에 적힌 항목(차이가 있는 것) 전부다.
internal sealed record MetricsView(
    string Status,
    string? Reason,
    CompareCounts? Counts,
    decimal? AccuracyPct,
    decimal? MissingRatePct,
    IReadOnlyList<VerificationItem> Items);
