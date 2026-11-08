using System.Globalization;
using System.Text;
using Scorecard.Core.Cases;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.TextCheck;

// 스펙 4.4 가림 확인. 점수 자료 장면 표(바탕 7.3 reply-drafts), 가림 규칙 표의 순서·발견 위치,
// 몰림 예·몰림 문턱 표(엔진 출력으로 본다). 표시 하나하나의 값(글자 표시·키 표시·작은 쪽)은 BlindMarkTests. 카드 수용 기준 3.
[Trait("Spec", "text-check")]
public class BlindCheckTests
{
    private const string Kept = "kept";
    private const string Revealed = "revealed";
    private const string Pattern = "source_pattern";

    public static TheoryData<string, string, string, string> SceneRows
    {
        get
        {
            var data = new TheoryData<string, string, string, string>();
            foreach (var c in TextCheckSpec.Table(TextCheckSpec.ScoreSceneHeader))
            {
                data.Add(c[0], TextCheckSpec.Cell(c[1]), c[2].Trim(), TextCheckSpec.Cell(c[3]));
            }

            return data;
        }
    }

    // 몰림 예 표 + 몰림 문턱 표의 값으로 만든 경계(문턱 하나만 넘지 못함). (AI 쪽, 사람 쪽, 센 건, 걸리나).
    public static TheoryData<int, int, int, bool> LeanRows
    {
        get
        {
            var (split, minority, share) = Thresholds();
            var rows = TextCheckSpec.Table(TextCheckSpec.LeanExampleHeader)
                .Select(c => (TextCheckSpec.Int(c[0]), TextCheckSpec.Int(c[1]), TextCheckSpec.Int(c[2]), TextCheckSpec.YesNo(c[3])))
                .Concat(
                [
                    (split, 0, split, true),
                    (split - 1, 0, split - 1, false),
                    (minority - 1, 1, minority, true),
                    (minority - 2, 1, minority - 1, false),
                    (split, 0, share * split, true),
                    (split, 0, (share * split) + 1, false),
                ]);
            var data = new TheoryData<int, int, int, bool>();
            foreach (var (a, h, n, leans) in rows.Distinct())
            {
                data.Add(a, h, n, leans);
            }

            return data;
        }
    }

    // 점수 자료 장면 표. 받으면 blind.status와 규칙별 발견 수가 표와 같다. 발견의 속성은 rule, file, line, value 순서다.
    // 오류면 출력이 없고 그 코드의 오류가 있다.
    [Theory]
    [MemberData(nameof(SceneRows))]
    public void Score_scene_matches_table(string change, string result, string findings, string code)
    {
        var run = TextCheckInput.Run(TextCheckSpec.Example, change);
        if (result == TextCheckSpec.Rejected)
        {
            Assert.False(run.IsValid);
            Assert.Contains(code, run.Codes);
            return;
        }

        TextCheckInput.Valid(run);
        var blind = run.MetricsJson.GetProperty("scores").GetProperty("blind");
        Assert.Equal(result, blind.GetProperty("status").GetString());
        Assert.Equal(TextCheckInput.FindingCounts(findings), TextCheckInput.FindingCounts(run.MetricsJson));
        Assert.Equal(result == Kept, blind.GetProperty("findings").GetArrayLength() == 0);
        Assert.All(blind.GetProperty("findings").EnumerateArray(), f => Assert.Equal(["rule", "file", "line", "value"], TextCheckInput.Names(f)));
    }

    // 장면 표는 가림 규칙 넷을 모두 쓰고, 받는 장면과 오류 장면이 모두 있다(빈 통과 방지).
    [Fact]
    public void Score_scene_table_covers_all_rules()
    {
        var rows = TextCheckSpec.Table(TextCheckSpec.ScoreSceneHeader);
        var rules = rows.SelectMany(r => TextCheckInput.FindingCounts(r[2]).Keys).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(Rules().Order(StringComparer.Ordinal), rules.Order(StringComparer.Ordinal));
        Assert.Contains(rows, r => TextCheckSpec.Cell(r[1]) == Kept);
        Assert.Contains(rows, r => TextCheckSpec.Cell(r[1]) == TextCheckSpec.Rejected);
    }

    // 가림 규칙 표 순서가 findings의 첫 열쇠다. 규칙 셋이 한 장면에 같이 있으면 source_column → source_value → source_in_blind.
    // 발견 위치: 열 이름은 점수표 머리 행 줄·열 이름 그대로, 칸은 그 줄·칸 글, 키는 출처 표 그 줄·키.
    [Fact]
    public void Findings_are_ordered_by_rule_table_then_position()
    {
        var run = TextCheckInput.Valid(TextCheckInput.Run(TextCheckSpec.Example,
            "`scores/sources.csv`, `scores/sheet.csv`: `b05,` → `b05-ai,`; `scores/sheet.csv`: `\\n` → `,Source\\n`, `\\n` → `,human\\n`"));
        var findings = Findings(run);
        var order = Rules();
        var b05 = Array.FindIndex(TextCheckInput.Text(TextCheckSpec.FileBlock("scores/sources.csv")).Split('\n'), l => l.StartsWith("b05,", StringComparison.Ordinal)) + 1;

        Assert.Equal(("source_column", "scores/sheet.csv", 1, "Source"), findings[0]);
        Assert.Equal(("source_value", "scores/sheet.csv", 2, "human"), findings[1]);
        Assert.Equal(("source_in_blind", "scores/sources.csv", b05, "b05-ai"), findings[^1]);
        Assert.Equal(1 + 72 + 1, findings.Length);
        var ranks = findings.Select(f => Array.IndexOf(order, f.Rule)).ToArray();
        Assert.Equal(ranks.Order(), ranks);
        Assert.DoesNotContain(-1, ranks);
    }

    // 4.4 장면 글머리: AI 키를 x로 바꾸면 발견 둘(x01, b03), 출처 표의 그 줄. AI 행을 다른 파일로 옮기면 두 파일의 머리 행(값은 경로).
    [Fact]
    public void Pattern_findings_point_to_first_cell_with_the_mark()
    {
        var keys = Findings(TextCheckInput.Valid(TextCheckInput.Run(TextCheckSpec.Example, "`scores/sources.csv`, `scores/sheet.csv` (출처 `ai` 행): `b` → `x`")));
        var files = Findings(TextCheckInput.Valid(TextCheckInput.Run(TextCheckSpec.Example, "`scores/sheet.csv`의 출처 `ai` 행 → `scores/sheet2.csv`")));

        Assert.Equal([(Pattern, "scores/sources.csv", 2, "x01"), (Pattern, "scores/sources.csv", 4, "b03")], keys);
        Assert.Equal([(Pattern, "scores/sheet.csv", 1, "scores/sheet.csv"), (Pattern, "scores/sheet2.csv", 1, "scores/sheet2.csv")], files);
    }

    // 몰림 예·몰림 문턱(엔진 출력). 건 N개, 건마다 AI 결과·사람 결과. 가림 키는 출처마다 고르게(작은 쪽이 번갈아) 붙여 키로는 갈리지 않는다.
    // writer 열에 w를 AI 행 a건, 사람 행 h건에 쓰고 나머지는 빈 칸이다(빈 칸은 표시가 아니다). 채점자 하나가 모든 결과를 매긴다.
    // 걸리면 writer 칸 하나가 source_pattern이다(표시 넷이 같은 칸을 가리켜 한 번).
    [Theory]
    [MemberData(nameof(LeanRows))]
    public void Lean_examples_and_thresholds_hold_on_engine_output(int ai, int human, int counted, bool leans)
    {
        using var folder = LeanFolder(ai, human, counted);
        var run = TextCheckInput.Valid(TextCheckInput.Run(folder.Root));
        var findings = Findings(run);

        // 첫 w 칸: AI 쪽 건이 있으면 건 1의 AI 행(2줄), 없으면 건 1의 사람 행(3줄).
        var expected = leans ? new[] { (Pattern, "scores/sheet.csv", ai > 0 ? 2 : 3, "w") } : [];

        Assert.Equal(leans ? Revealed : Kept, run.MetricsJson.GetProperty("scores").GetProperty("blind").GetProperty("status").GetString());
        Assert.Equal(expected, findings);
    }

    // 몰림 문턱 표의 이름은 셋이고 조건 칸이 그 이름의 셈을 적는다.
    [Fact]
    public void Lean_threshold_table_has_three_named_rows()
    {
        var rows = TextCheckSpec.Table(TextCheckSpec.LeanThresholdHeader);

        Assert.Equal(["min_split", "minority_per", "share_per"], rows.Select(r => TextCheckSpec.Cell(r[0])));
        Assert.Equal([8, 12, 4], rows.Select(r => TextCheckSpec.Int(r[1])));
        Assert.All(rows, r => Assert.Contains(TextCheckSpec.Cell(r[1]), r[2], StringComparison.Ordinal));
    }

    // 4.4 "건이 7건 이하이면 어떤 표시도 걸리지 않는다": AI 키를 x, 사람 키를 b로 나누고 writer 열·파일도 출처마다 나눠도 kept.
    [Fact]
    public void Seven_items_or_fewer_never_lean()
    {
        using var folder = LeanFolder(7, 0, 7);
        var root = folder.Root;
        TextCheckInput.Change(root, "`scores/sources.csv`, `scores/sheet.csv` (출처 `ai` 행): `b` → `x`; `scores/sheet.csv`의 출처 `ai` 행 → `scores/sheet2.csv`");

        var run = TextCheckInput.Valid(TextCheckInput.Run(root));

        Assert.Equal(Kept, run.MetricsJson.GetProperty("scores").GetProperty("blind").GetProperty("status").GetString());
    }

    private static string[] Rules() => TextCheckSpec.Table(TextCheckSpec.BlindRuleHeader).Select(r => TextCheckSpec.Cell(r[0])).ToArray();

    private static (int Split, int Minority, int Share) Thresholds()
    {
        var values = TextCheckSpec.Table(TextCheckSpec.LeanThresholdHeader).ToDictionary(r => TextCheckSpec.Cell(r[0]), r => TextCheckSpec.Int(r[1]), StringComparer.Ordinal);
        return (values["min_split"], values["minority_per"], values["share_per"]);
    }

    private static (string Rule, string File, int Line, string Value)[] Findings(TextCheckRun run) =>
        run.MetricsJson.GetProperty("scores").GetProperty("blind").GetProperty("findings").EnumerateArray()
            .Select(f => (f.GetProperty("rule").GetString()!, f.GetProperty("file").GetString()!, f.GetProperty("line").GetInt32(), f.GetProperty("value").GetString()!))
            .ToArray();

    // 점수표만 케이스. 건 i(1부터)의 키는 b(2i-1)·b(2i)(3자리)이고 홀수 건은 AI 키가, 짝수 건은 사람 키가 작다.
    private static TextCheckFolder LeanFolder(int ai, int human, int counted)
    {
        var sources = new StringBuilder("blind,item,source\n");
        var sheet = new StringBuilder("rater,item,blind,score,writer\n");
        for (var i = 1; i <= counted; i++)
        {
            var (odd, even) = ("b" + ((2 * i) - 1).ToString("D3", CultureInfo.InvariantCulture), "b" + (2 * i).ToString("D3", CultureInfo.InvariantCulture));
            var (aiKey, humanKey) = i % 2 == 1 ? (odd, even) : (even, odd);
            var item = "i" + i.ToString("D3", CultureInfo.InvariantCulture);
            sources.Append(CultureInfo.InvariantCulture, $"{aiKey},{item},ai\n{humanKey},{item},human\n");
            var (aiWriter, humanWriter) = (i <= ai ? "w" : string.Empty, i > ai && i <= ai + human ? "w" : string.Empty);
            sheet.Append(CultureInfo.InvariantCulture, $"p1,{item},{aiKey},3,{aiWriter}\np1,{item},{humanKey},3,{humanWriter}\n");
        }

        return TextCheckFolder.Create(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [CaseParser.FileName] = TextCheckInput.Bytes("name: lean\ntask: t\nsteps:\n  - name: s\nscores:\n  sheet: scores/sheet*.csv\n  sources: scores/sources.csv\n  min: 1\n  max: 5\n"),
            ["scores/sources.csv"] = TextCheckInput.Bytes(sources.ToString()),
            ["scores/sheet.csv"] = TextCheckInput.Bytes(sheet.ToString()),
        });
    }
}
