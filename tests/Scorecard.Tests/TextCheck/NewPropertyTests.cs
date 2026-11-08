using System.Text.Json;
using Scorecard.Tests.Comparison;
using Scorecard.Tests.Judgment;

namespace Scorecard.Tests.TextCheck;

// 스펙 3.1·3.2 새 속성 표. reply-drafts 출력에서 metrics.md 3.2·judgment.md 3.2 표에 없는 경로 = 새 표(처음 나온 순서), 형식 칸.
// 설정했을 때만 쓴다(checklist·scores가 없으면 새 속성이 없다). 카드 수용 기준 2·3·5.
[Trait("Spec", "text-check")]
public class NewPropertyTests
{
    public const string FromBlock = "block";
    public const string FromEngine = "engine";

    // 형식 칸 → JSON 값 종류.
    private static readonly Dictionary<string, Func<JsonElement, bool>> Formats = new(StringComparer.Ordinal)
    {
        ["정수"] = v => v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out _) && !v.GetRawText().Contains('.', StringComparison.Ordinal),
        ["숫자"] = v => v.ValueKind == JsonValueKind.Number,
        ["문자열"] = v => v.ValueKind == JsonValueKind.String,
        ["객체"] = v => v.ValueKind == JsonValueKind.Object,
        ["문자열 배열"] = v => v.ValueKind == JsonValueKind.Array && v.EnumerateArray().All(x => x.ValueKind == JsonValueKind.String),
        ["객체 배열"] = v => v.ValueKind == JsonValueKind.Array && v.EnumerateArray().All(x => x.ValueKind == JsonValueKind.Object),
        ["문자열 또는 null"] = v => v.ValueKind is JsonValueKind.String or JsonValueKind.Null,
        ["숫자 또는 null"] = v => v.ValueKind is JsonValueKind.Number or JsonValueKind.Null,
    };

    public static TheoryData<string> Sources => new(FromBlock, FromEngine);

    [Theory]
    [MemberData(nameof(Sources))]
    public void Metrics_new_paths_match_table_in_order(string from)
    {
        var known = MetricsSpec.Table(MetricsSpec.PropertyHeader).Select(c => MetricsSpec.Cell(c[0])).ToHashSet(StringComparer.Ordinal);
        AssertNewPaths(Output(from).Metrics, known, TextCheckSpec.PropertyHeader);
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void Judgment_new_paths_match_table_in_order(string from)
    {
        var known = JudgmentSpec.Table(JudgmentSpec.PropertyHeader).Select(c => JudgmentSpec.Cell(c[0])).ToHashSet(StringComparer.Ordinal);
        AssertNewPaths(Output(from).Judgment, known, TextCheckSpec.JudgmentPropertyHeader);
    }

    // 3.1 자리: compare.checklist는 fields 뒤·items 앞, missing_items는 differences 뒤, scores는 최상위 마지막.
    // 3.2 자리: thresholds.score_gap_max는 문턱 셋 뒤, SCORE는 확인 다섯 뒤 여섯째.
    [Fact]
    public void New_properties_sit_where_section_3_says()
    {
        var run = TextCheckInput.Valid(TextCheckInput.Run(TextCheckSpec.Example, TextCheckSpec.NoChange));
        var metrics = run.MetricsJson;
        var judgment = run.JudgmentJson;
        var compareNames = TextCheckInput.Names(metrics.GetProperty("compare"));

        Assert.Equal(["schema_version", "case", "compare", "scores"], TextCheckInput.Names(metrics));
        Assert.Equal(["fields", "checklist", "items"], compareNames[^3..]);
        Assert.All(metrics.GetProperty("compare").GetProperty("items").EnumerateArray(), i => Assert.Equal(["differences", "missing_items"], TextCheckInput.Names(i)[^2..]));
        Assert.Equal(["accuracy_min", "missing_max", "time_must_drop", "score_gap_max"], TextCheckInput.Names(judgment.GetProperty("thresholds")));
        Assert.Equal(["ACCURACY", "MISSING", "UNEXPLAINED", "TIME", "FOCUS", "SCORE"], judgment.GetProperty("checks").EnumerateArray().Select(c => c.GetProperty("rule").GetString()));
        Assert.Equal((1, 2), (metrics.GetProperty("schema_version").GetInt32(), judgment.GetProperty("schema_version").GetInt32()));
    }

    // 3.1 "설정했을 때만": checklist·scores·score_gap_max를 지우고 compare 필드 하나를 둔 사본은 새 속성이 없고 확인이 다섯이다.
    [Fact]
    public void Without_checklist_and_scores_no_new_properties_appear()
    {
        using var folder = TextCheckInput.Folder(TextCheckSpec.Example, "`case.yaml`: `  score_gap_max: 0.5\\n` → ``");
        var text = TextCheckInput.Read(folder.Root, "case.yaml");
        var cut = text.IndexOf("checklist:\n", StringComparison.Ordinal);
        text = text[..cut] + text[text.IndexOf("# 시간 측정", StringComparison.Ordinal)..] + "compare:\n  - {field: ticket}\n";
        TextCheckInput.Write(folder.Root, "case.yaml", text);
        TextCheckInput.Write(folder.Root, "candidate/replies.csv", TextCheckInput.Read(folder.Root, "reference/replies.csv"));

        var run = TextCheckInput.Valid(TextCheckInput.Run(folder.Root));

        Assert.DoesNotContain("scores", TextCheckInput.Names(run.MetricsJson));
        Assert.DoesNotContain("checklist", TextCheckInput.Names(run.MetricsJson.GetProperty("compare")));
        Assert.All(run.MetricsJson.GetProperty("compare").GetProperty("items").EnumerateArray(), i => Assert.DoesNotContain("missing_items", TextCheckInput.Names(i)));
        Assert.DoesNotContain("score_gap_max", TextCheckInput.Names(run.JudgmentJson.GetProperty("thresholds")));
        Assert.Equal(5, run.JudgmentJson.GetProperty("checks").GetArrayLength());
    }

    // 6.2 score-only metrics.json 모양: 대조는 미검증 no_inputs(metrics.md 7.4 모양), 그 뒤 scores.
    [Fact]
    public void Score_only_metrics_have_not_verified_compare_then_scores()
    {
        var metrics = TextCheckInput.Valid(TextCheckInput.Run(TextCheckSpec.ScoreOnly, TextCheckSpec.NoChange)).MetricsJson;

        Assert.Equal(["schema_version", "case", "compare", "scores"], TextCheckInput.Names(metrics));
        Assert.Equal(("not_verified", "no_inputs"), (metrics.GetProperty("compare").GetProperty("status").GetString(), metrics.GetProperty("compare").GetProperty("reason").GetString()));
        Assert.DoesNotContain("checklist", TextCheckInput.Names(metrics.GetProperty("compare")));
    }

    private static (JsonElement Metrics, JsonElement Judgment) Output(string from)
    {
        if (from == FromBlock)
        {
            return (TextCheckInput.Json(TextCheckSpec.FileBlock("expected/metrics.json")), TextCheckInput.Json(TextCheckSpec.FileBlock("expected/judgment.json")));
        }

        var run = TextCheckInput.Valid(TextCheckInput.Run(TextCheckSpec.Example, TextCheckSpec.NoChange));
        return (run.MetricsJson, run.JudgmentJson);
    }

    private static void AssertNewPaths(JsonElement root, IReadOnlySet<string> known, string header)
    {
        var rows = TextCheckSpec.Table(header);
        var paths = TextCheckInput.Paths(root);
        var added = paths.Where(p => !known.Contains(p.Path)).ToArray();

        Assert.Equal(rows.Select(r => TextCheckSpec.Cell(r[0])), added.Select(p => p.Path));
        foreach (var r in rows)
        {
            var (path, format) = (TextCheckSpec.Cell(r[0]), r[1].Trim());
            Assert.True(Formats.ContainsKey(format), $"새 속성 표 {path}의 형식 '{format}'을 모른다.");
            Assert.All(added.Single(p => p.Path == path).Values, v => Assert.True(Formats[format](v), $"{path} = {v.GetRawText()}, 형식 {format}"));
        }
    }
}
