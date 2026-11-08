using System.Globalization;
using System.Text;
using Scorecard.Core.Cases;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.TextCheck;

// 스펙 4.3 가림 점수표(파일 열 표, 평균 표, 집계)와 4.5 점수 오류(코드 표, 위치, 순서). 카드 수용 기준 3.
[Trait("Spec", "text-check")]
public class ScoreSheetTests
{
    private const string Sheet = "scores/sheet.csv";
    private const string Sources = "scores/sources.csv";

    // 4.3 파일 열 표의 "파일" 칸 `scores.sheet`·`scores.sources` → 예제의 파일.
    private static readonly IReadOnlyDictionary<string, string> FileOf = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["scores.sheet"] = Sheet,
        ["scores.sources"] = Sources,
    };

    public static TheoryData<string, string> FileColumns
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var c in TextCheckSpec.Table(TextCheckSpec.ScoreFileHeader))
            {
                var file = FileOf[Assert.Single(SpecDocument.CodeSpans(c[0]))];
                foreach (var column in SpecDocument.CodeSpans(c[1]))
                {
                    data.Add(file, column);
                }
            }

            return data;
        }
    }

    public static TheoryData<int, int, string> MeanRows
    {
        get
        {
            var data = new TheoryData<int, int, string>();
            foreach (var c in TextCheckSpec.Table(TextCheckSpec.MeanHeader))
            {
                data.Add(TextCheckSpec.Int(c[0]), TextCheckSpec.Int(c[1]), TextCheckSpec.Cell(c[2]));
            }

            return data;
        }
    }

    // 파일 열 표: 열 이름은 정해져 있다. 머리 행에서 그 열 이름을 바꾸면 그 파일 머리 행의 column 오류다(경로 = 열 이름).
    [Theory]
    [MemberData(nameof(FileColumns))]
    public void Fixed_score_columns_are_required(string file, string column)
    {
        using var folder = TextCheckInput.Folder(TextCheckSpec.Example);
        var lines = TextCheckInput.Read(folder.Root, file).Split('\n');
        var header = lines[0].Split(',');
        Assert.Contains(column, header);
        lines[0] = string.Join(',', header.Select(h => h == column ? h + "_x" : h));
        TextCheckInput.Write(folder.Root, file, string.Join('\n', lines));

        var run = TextCheckInput.Run(folder.Root);

        Assert.False(run.IsValid);
        Assert.Contains(run.Errors, e => (e.Source, e.Line, e.Code, e.Path, e.Message) == (file, 1, "column", column, "머리 행에 열이 없다"));
    }

    // 평균 표. 채점자 n명이 AI 결과 하나와 사람 결과 하나를 매긴다(건 하나, 0~100점). AI 점수 합이 표의 합이다.
    // 평균은 소수 둘째 자리 글이다(정수 셈, 0.005에서 올림). 사람 결과는 모두 0점이라 평균 0.00이다.
    [Theory]
    [MemberData(nameof(MeanRows))]
    public void Mean_table_row_holds(int sum, int count, string mean)
    {
        using var folder = MeanFolder(sum, count);
        var scores = TextCheckInput.Valid(TextCheckInput.Run(folder.Root)).MetricsJson.GetProperty("scores");
        var ai = scores.GetProperty("sources")[0];
        var human = scores.GetProperty("sources")[1];

        Assert.Equal((TextCheckSpec.SourceAi, sum, count), (ai.GetProperty("source").GetString(), ai.GetProperty("sum").GetInt32(), ai.GetProperty("ratings").GetInt32()));
        Assert.Equal(mean, ai.GetProperty("mean").GetRawText());
        Assert.Equal((TextCheckSpec.SourceHuman, "0.00"), (human.GetProperty("source").GetString(), human.GetProperty("mean").GetRawText()));
        Assert.Equal(101, ai.GetProperty("distribution").GetArrayLength());
        Assert.Equal(count, scores.GetProperty("raters").GetArrayLength());
    }

    // 4.3 규칙 4~6과 6.3: reply-drafts 집계. 채점자 3명 × 결과 24개 = 72점. AI 139/36(3.86), 사람 151/36(4.19).
    // 건별 AI 높음 3, 같음 0, 낮음 9. 채점자마다 AI 12개·사람 12개. 분포는 min~max 점수마다 하나씩이고 합이 점수 수다.
    [Fact]
    public void Reply_drafts_score_summary_matches_section_6_3()
    {
        var scores = TextCheckInput.Valid(TextCheckInput.Run(TextCheckSpec.Example, TextCheckSpec.NoChange)).MetricsJson.GetProperty("scores");
        var sources = scores.GetProperty("sources").EnumerateArray().ToArray();

        Assert.Equal((12, 72), (scores.GetProperty("items").GetInt32(), scores.GetProperty("ratings").GetInt32()));
        Assert.Equal(["p1", "p2", "p3"], TextCheckInput.Strings(scores.GetProperty("raters")));
        Assert.Equal([(TextCheckSpec.SourceAi, 12, 36, 139, "3.86"), (TextCheckSpec.SourceHuman, 12, 36, 151, "4.19")],
            sources.Select(s => (s.GetProperty("source").GetString(), s.GetProperty("results").GetInt32(), s.GetProperty("ratings").GetInt32(), s.GetProperty("sum").GetInt32(), s.GetProperty("mean").GetRawText())));
        Assert.All(sources, s =>
        {
            Assert.Equal([1, 2, 3, 4, 5], s.GetProperty("distribution").EnumerateArray().Select(d => d.GetProperty("score").GetInt32()));
            Assert.Equal(s.GetProperty("ratings").GetInt32(), s.GetProperty("distribution").EnumerateArray().Sum(d => d.GetProperty("count").GetInt32()));
        });
        var paired = scores.GetProperty("paired");
        Assert.Equal((3, 0, 9), (paired.GetProperty("better").GetInt32(), paired.GetProperty("same").GetInt32(), paired.GetProperty("worse").GetInt32()));
        Assert.All(scores.GetProperty("by_rater").EnumerateArray(), r => Assert.Equal((12, 12), (r.GetProperty("ai_ratings").GetInt32(), r.GetProperty("human_ratings").GetInt32())));
        Assert.Equal([Sheet], TextCheckInput.Strings(scores.GetProperty("sheet_files")));
        Assert.Equal([Sources], TextCheckInput.Strings(scores.GetProperty("sources_files")));
    }

    // 4.3 규칙 6: 매긴 것이 없는 출처의 채점자별 평균은 null이다. p1의 사람 결과 행을 점수 파일 밖으로 옮긴다.
    [Fact]
    public void Rater_without_ratings_of_a_source_has_null_mean()
    {
        var run = TextCheckInput.Valid(TextCheckInput.Run(TextCheckSpec.Example, "`scores/sheet.csv`의 채점자 `p1` 행 → `scores/sheet-p1.csv`; `scores/sheet-p1.csv`의 출처 `human` 행 → `scores/old.csv`"));
        var p1 = run.MetricsJson.GetProperty("scores").GetProperty("by_rater")[0];

        Assert.Equal(("p1", 12, 0), (p1.GetProperty("rater").GetString(), p1.GetProperty("ai_ratings").GetInt32(), p1.GetProperty("human_ratings").GetInt32()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, p1.GetProperty("human_mean").ValueKind);
        Assert.Equal(["scores/sheet-p1.csv", Sheet], TextCheckInput.Strings(run.MetricsJson.GetProperty("scores").GetProperty("sheet_files")));
    }

    // 4.5 점수 코드 표의 메시지 틀: 점수 자료 장면 표의 오류 장면과 아래 오류 입력에서 나온 점수 쪽 오류는 그 코드의 틀 가운데 하나와 맞는다.
    // 장면 표의 오류 코드는 모두 점수 코드 표나 metrics.md 4.7 코드다.
    [Fact]
    public void Score_error_messages_follow_code_table()
    {
        var templates = TextCheckSpec.Table(TextCheckSpec.ScoreCodeHeader)
            .ToDictionary(c => TextCheckSpec.Cell(c[0]), c => SpecDocument.CodeSpans(c[3]), StringComparer.Ordinal);
        var changes = TextCheckSpec.Table(TextCheckSpec.ScoreSceneHeader).Where(c => TextCheckSpec.Cell(c[1]) == TextCheckSpec.Rejected).Select(c => c[0])
            .Concat([SheetDuplicate, CaseChange("  sheet: scores/sheet*.csv\\n", "  sheet: scores/*.csv\\n"), "`scores/sheet.csv`: `p1,d11,b01,` → `p1,d07,b01,`"])
            .ToArray();
        var checkedCodes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var change in changes)
        {
            var errors = TextCheckInput.Run(TextCheckSpec.Example, change).Errors;
            Assert.NotEmpty(errors);
            foreach (var error in errors.Where(e => e.Source.StartsWith("scores/", StringComparison.Ordinal) && templates.ContainsKey(e.Code)))
            {
                Assert.Contains(templates[error.Code], t => TextCheckSpec.MessagePattern(t).IsMatch(error.Message));
                checkedCodes.Add(error.Code);
            }
        }

        Assert.Superset(new HashSet<string>(["score", "source", "blind", "pair", "duplicate-key", "overlap"], StringComparer.Ordinal), checkedCodes);
    }

    // score: 그 칸(점수 열), 경로 score, 메시지 "점수는 <min>~<max>의 정수다: <값>". 4점인 모든 행.
    [Fact]
    public void Score_errors_point_to_each_score_cell()
    {
        var expected = LinesOf(Sheet, l => l.EndsWith(",4", StringComparison.Ordinal)).Select(n => (Sheet, n, 12, "score", "점수는 1~5의 정수다: 6"));
        var errors = TextCheckInput.Run(TextCheckSpec.Example, "`scores/sheet.csv`: `,4\\n` → `,6\\n`").Errors;

        Assert.Equal(expected, errors.Where(e => e.Code == "score").Select(e => (e.Source, e.Line, e.Column, e.Path, e.Message)));
    }

    // source: 그 칸(출처 열), 경로 source. pair: 출처 표의 그 건 첫 행 item 칸, 메시지에 ai·human 수.
    [Fact]
    public void Source_and_pair_errors_point_to_sources_cells()
    {
        var sourceErrors = TextCheckInput.Run(TextCheckSpec.Example, "`scores/sources.csv`: `,ai\\n` → `,AI\\n`").Errors.Where(e => e.Code == "source");
        var pairErrors = TextCheckInput.Run(TextCheckSpec.Example, "`scores/sources.csv`: `,human\\n` → `,ai\\n`").Errors.Where(e => e.Code == "pair");
        var rows = SourceRows();
        var firstOfItem = rows.GroupBy(r => r.Item, StringComparer.Ordinal).Select(g => g.First()).OrderBy(r => r.Line);

        Assert.Equal(
            rows.Where(r => r.Source == TextCheckSpec.SourceAi).Select(r => (Sources, r.Line, r.Blind.Length + r.Item.Length + 3, "source", "출처는 ai 또는 human이다: AI")),
            sourceErrors.Select(e => (e.Source, e.Line, e.Column, e.Path, e.Message)));
        Assert.Equal(
            firstOfItem.Select(r => (Sources, r.Line, r.Blind.Length + 2, "item", $"건마다 ai 하나와 human 하나가 있어야 한다: {r.Item} (ai 2, human 0)")),
            pairErrors.Select(e => (e.Source, e.Line, e.Column, e.Path, e.Message)));
    }

    // duplicate-key: 출처 표의 같은 가림 키(둘째 칸, 처음 위치), 점수표의 같은 채점자·가림 키.
    [Fact]
    public void Duplicate_blind_keys_and_ratings_are_errors()
    {
        var sources = TextCheckInput.Run(TextCheckSpec.Example, "`scores/sources.csv`: `b02,` → `b01,`").Errors.Where(e => e.Code == "duplicate-key");
        var sheet = TextCheckInput.Run(TextCheckSpec.Example, SheetDuplicate).Errors.Where(e => e.Code == "duplicate-key");

        Assert.Equal([(Sources, 3, 1, "blind", $"가림 키가 중복된다: b01 (처음: {Sources}:2)")], sources.Select(e => (e.Source, e.Line, e.Column, e.Path, e.Message)));
        Assert.Equal([(Sheet, 3, 8, "blind", $"점수가 중복된다: p1 b01 (처음: {Sheet}:2)")], sheet.Select(e => (e.Source, e.Line, e.Column, e.Path, e.Message)));
    }

    // blind 넷: 출처 표에 없는 가림 키(점수표 그 칸), 건이 다름(점수표 item 칸), 점수가 없는 키(출처 표 그 행 가림 키 칸, 마지막),
    // 대조 measured인데 기준 자료에 없는 건(출처 표 그 행 건 칸).
    [Fact]
    public void Blind_errors_follow_code_table_positions()
    {
        var unknown = TextCheckInput.Run(TextCheckSpec.Example, "`scores/sheet.csv`: `,b24,` → `,b99,`").Errors.Where(e => e.Code == "blind");
        var item = TextCheckInput.Run(TextCheckSpec.Example, "`scores/sheet.csv`: `p1,d11,b01,` → `p1,d07,b01,`").Errors.Where(e => e.Code == "blind");
        var foreign = TextCheckInput.Run(TextCheckSpec.Example, "`scores/sources.csv`, `scores/sheet.csv`: `,d` → `,z`").Errors.Where(e => e.Code == "blind").ToArray();
        var b24 = Assert.Single(SourceRows(), r => r.Blind == "b24");

        Assert.Equal(
            LinesOf(Sheet, l => l.Contains(",b24,", StringComparison.Ordinal)).Select(n => (Sheet, n, 8, "blind", "출처 표에 없는 가림 키다: b99"))
                .Append((Sources, b24.Line, 1, "blind", "점수가 하나도 없다: b24")),
            unknown.Select(e => (e.Source, e.Line, e.Column, e.Path, e.Message)));
        Assert.Equal([(Sheet, 2, 4, "item", "출처 표의 건과 다르다: d07 (출처 표: d11)")], item.Select(e => (e.Source, e.Line, e.Column, e.Path, e.Message)));
        Assert.NotEmpty(foreign);
        Assert.Equal((Sources, 2, 5, "item", "기준 자료에 없는 건이다: z11"), (foreign[0].Source, foreign[0].Line, foreign[0].Column, foreign[0].Path, foreign[0].Message));
        Assert.All(foreign, e => Assert.Equal((Sources, "item"), (e.Source, e.Path)));
    }

    // 파일이 정해지기 전 오류(file·no-rows)는 Source case.yaml, 경로 scores.sheet·scores.sources(metrics.md 3.4와 같다).
    // overlap: 점수 파일끼리 겹침, 대조 measured일 때 inputs 파일과 겹침. 점수표만(inputs 없음)은 inputs와 견주지 않는다.
    [Fact]
    public void File_level_score_errors_use_case_yaml_and_overlap_message()
    {
        var noFile = TextCheckInput.Run(TextCheckSpec.Example, CaseChange("  sheet: scores/sheet*.csv\\n", "  sheet: scores/none*.csv\\n"));
        using var empty = TextCheckInput.Folder(TextCheckSpec.Example);
        TextCheckInput.Write(empty.Root, Sources, "blind,item,source\n");
        var noRows = TextCheckInput.Run(empty.Root);
        var scoreOverlap = TextCheckInput.Run(TextCheckSpec.Example, CaseChange("  sheet: scores/sheet*.csv\\n", "  sheet: scores/*.csv\\n"));
        var inputsOverlap = TextCheckInput.Run(TextCheckSpec.Example, CaseChange("  sources: scores/sources.csv\\n", "  sources: reference/replies.csv\\n"));

        Assert.Contains("case.yaml: file scores.sheet: 일치하는 파일이 없다", noFile.Errors.Select(e => e.ToString()));
        Assert.Contains("case.yaml: no-rows scores.sources: 출처 표에 데이터 행이 없다", noRows.Errors.Select(e => e.ToString()));
        Assert.Contains(scoreOverlap.Errors, e => e.Code == "overlap" && e.Message == "같은 파일이 두 번 나온다(패턴이 겹친다)");
        Assert.Contains(inputsOverlap.Errors, e => e.Code == "overlap" && e.Message == "같은 파일이 두 번 나온다(패턴이 겹친다)");
    }

    // 4.5 순서: 대조 쪽 → 출처 표 → 점수표 → 점수가 없는 키. 대조 쪽 오류가 있으면 "기준 자료에 없는 건"은 보지 않는다.
    // 기준 자료의 d02를 d01로 바꿔(duplicate-key) 출처 표의 d02가 기준에 없게 하고, 점수 하나를 범위 밖으로 바꾼다.
    [Fact]
    public void Errors_are_ordered_compare_then_sources_then_sheet()
    {
        var errors = TextCheckInput.Run(TextCheckSpec.Example, "`reference/replies.csv`: `d02,` → `d01,`; `scores/sheet.csv`: `p1,d11,b01,4\\n` → `p1,d11,b01,9\\n`; `scores/sheet.csv`: `p3,d07,b02,` → `p3,d07,b99,`").Errors;
        var lines = errors.Select(e => e.ToString()).ToArray();
        var groups = errors.Select(e => e.Source switch
        {
            "reference/replies.csv" => 0,
            Sources => 1,
            Sheet => 2,
            _ => -1,
        }).ToArray();

        Assert.Equal("reference/replies.csv:3:1: duplicate-key reply_id: 키가 중복된다: d01 (처음: reference/replies.csv:2)", lines[0]);
        Assert.Contains($"{Sheet}:2:12: score score: 점수는 1~5의 정수다: 9", lines);
        Assert.Contains(errors, e => e.Code == "blind" && e.Message == "출처 표에 없는 가림 키다: b99");
        Assert.DoesNotContain(errors, e => e.Message.StartsWith("기준 자료에 없는 건이다", StringComparison.Ordinal));
        Assert.Equal(groups.Order(), groups);
        Assert.DoesNotContain(-1, groups);
    }

    private const string SheetDuplicate = "`scores/sheet.csv`: `p1,d11,b01,4\\n` → `p1,d11,b01,4\\np1,d11,b01,3\\n`";

    private static string CaseChange(string old, string @new) => $"`case.yaml`: `{old}` → `{@new}`";

    // 7.3 scores/sheet.csv에서 조건에 맞는 줄 번호(1부터).
    private static IEnumerable<int> LinesOf(string file, Func<string, bool> match) =>
        TextCheckInput.Text(TextCheckSpec.FileBlock(file)).Split('\n').Select((l, i) => (Text: l, Line: i + 1)).Where(x => x.Line > 1 && x.Text.Length > 0 && match(x.Text)).Select(x => x.Line);

    // 7.3 scores/sources.csv 데이터 행.
    private static (int Line, string Blind, string Item, string Source)[] SourceRows() =>
        TextCheckInput.Text(TextCheckSpec.FileBlock(Sources)).Split('\n').Skip(1).Where(l => l.Length > 0)
            .Select((l, i) => l.Split(',') is [var b, var it, var s] ? (i + 2, b, it, s) : throw new InvalidOperationException(l))
            .ToArray();

    // 점수표만 케이스(min 0, max 100). 건 하나(i1)의 AI 결과 b1·사람 결과 b2를 채점자 count명이 매긴다.
    // AI 점수는 합이 sum이 되게 고르게 나눈다(앞 사람부터 1점씩 더). 사람 점수는 0.
    private static TextCheckFolder MeanFolder(int sum, int count)
    {
        var sheet = new StringBuilder("rater,item,blind,score\n");
        for (var k = 0; k < count; k++)
        {
            var rater = "p" + k.ToString("D3", CultureInfo.InvariantCulture);
            var score = (sum / count) + (k < sum % count ? 1 : 0);
            sheet.Append(CultureInfo.InvariantCulture, $"{rater},i1,b1,{score}\n{rater},i1,b2,0\n");
        }

        return TextCheckFolder.Create(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [CaseParser.FileName] = TextCheckInput.Bytes("name: mean\ntask: t\nsteps:\n  - name: s\nscores:\n  sheet: scores/sheet.csv\n  sources: scores/sources.csv\n  min: 0\n  max: 100\n"),
            [Sources] = TextCheckInput.Bytes("blind,item,source\nb1,i1,ai\nb2,i1,human\n"),
            [Sheet] = TextCheckInput.Bytes(sheet.ToString()),
        });
    }
}
