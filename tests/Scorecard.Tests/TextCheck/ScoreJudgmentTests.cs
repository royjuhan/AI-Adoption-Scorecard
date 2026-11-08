using System.Globalization;
using System.Text.Json;
using Scorecard.Core.Judgment;
using Scorecard.Tests.Judgment;

namespace Scorecard.Tests.TextCheck;

// 스펙 5절 판정: 5.1 점수 문턱 기본값, 5.2 SCORE 확인·문턱 0 문장, 5.3 판정 행, 5.4 읽은 문턱, 5.5 다음 할 일, 5.6 정의와 대조 결과.
// Core 규칙 상수는 internal이다. 표의 틀을 장면 값으로 채운 문장이 Core 출력과 같은지 본다(judgment 테스트와 같은 방식).
// 장면 값(평균 글·점수 수·발견 수)은 metrics.json scores에서 읽는다. 카드 수용 기준 3.
[Trait("Spec", "text-check")]
public class ScoreJudgmentTests
{
    // 5.3·5.4 자리 칸.
    private const string AfterFour = "4 뒤";
    private const string FiveToEight = "5~8";

    private const string Score = "SCORE";
    private const string Revealed = "revealed";
    private const string Met = "met";
    private const string NotMet = "not_met";
    private const string ScoreGapMax = "score_gap_max";
    private const string ConfirmThresholds = "confirm_thresholds";
    private const string RescoreBlind = "rescore_blind";
    private const string AddGroup = "add_group";
    private const string ReviewItems = "review_items";

    // 문턱 0 문장 표의 "확인"·"판정".
    private const string CheckSentence = "확인";
    private const string VerdictSentence = "판정";

    // 장면 "바꾼 곳"(4.4 머리말 모양). 출처 바꾸기는 가림 키를 그대로 두고 ai·human만 맞바꾼다(AI 평균이 더 높아진다).
    private const string SwapSources = "`scores/sources.csv`: `,ai\\n` → `,x\\n`, `,human\\n` → `,ai\\n`, `,x\\n` → `,human\\n`";
    private const string RevealOne = "`scores/sources.csv`, `scores/sheet.csv`: `b01,` → `b01-ai,`";
    private const string RevealScoreOnly = "`scores/sources.csv`, `scores/sheet.csv`: `b02,` → `human-02,`";
    private const string GapPoint3 = "`case.yaml`: `  score_gap_max: 0.5\\n` → `  score_gap_max: 0.3\\n`";
    private const string NoGap = "`case.yaml`: `  score_gap_max: 0.5\\n` → ``";
    private const string NoMissing = "`case.yaml`: `  missing_max: 5\\n` → ``";
    private const string NoTime = "`case.yaml`: `  time_must_drop: false\\n` → ``";
    private const string TimeOn = "`case.yaml`: `  time_must_drop: false\\n` → `  time_must_drop: true\\n`";
    private const string NoUnusedThresholds = "`case.yaml`: `  accuracy_min: 80\\n  missing_max: 5\\n` → ``";
    private const string NoGroup = "`case.yaml`: `  group: topic\\n` → ``";
    private const string AllChecklistFound = "`candidate/replies.csv`: `문의하신 건을` → `문의하신 q397 건을`, `처리 절차를` → `환불 접수 절차를`";
    private const string NoThresholds = "`case.yaml`: `thresholds:\\n  accuracy_min: 80\\n  missing_max: 5\\n  time_must_drop: false\\n  score_gap_max: 0.5\\n` → ``";
    private const string NoScores = "`case.yaml`: `scores:\\n  sheet: scores/sheet*.csv\\n  sources: scores/sources.csv\\n  min: 1\\n  max: 5\\n` → ``, `  score_gap_max: 0.5\\n` → ``";
    private const string RenameItem = "`case.yaml`: `{item: ticket, column: ticket}` → `{item: number, column: ticket}`";

    // 6.1 "점수표만, 대조 열 없음"의 바꾼 곳(체크리스트 지우기).
    private static string NoChecklist => JudgmentSceneTests.SceneChange("점수표만, 대조 열 없음");

    // 5.3 판정 행마다의 장면: (근거 키, 확인 결과, 길) → (바탕, 바꾼 곳, 시간).
    private static readonly Dictionary<(string Rule, string Result, string Path), (string Base, string Change, JudgmentTimes? Times)> VerdictScenes = new()
    {
        [(Score, Revealed, TextCheckSpec.PathTable)] = (TextCheckSpec.Example, RevealOne, null),
        [(Score, Revealed, TextCheckSpec.PathScoresOnly)] = (TextCheckSpec.ScoreOnly, RevealScoreOnly, null),
        [(Score, NotMet, TextCheckSpec.PathTable)] = (TextCheckSpec.Example, GapPoint3, null),
        [(Score, NotMet, TextCheckSpec.PathScoresOnly)] = (TextCheckSpec.ScoreOnly, GapPoint3, null),
        [(JudgmentSpec.NotVerifiedRule, "no_compare_fields", TextCheckSpec.PathScoresOnly)] = (TextCheckSpec.Example, string.Empty, null),
        [(JudgmentSpec.Time, "not_measured", TextCheckSpec.PathScoresOnly)] = (TextCheckSpec.ScoreOnly, TimeOn, null),
        [(JudgmentSpec.Time, NotMet, TextCheckSpec.PathScoresOnly)] = (TextCheckSpec.ScoreOnly, TimeOn, JudgmentInput.TimesNotMet),
        [(JudgmentSpec.PassRule, TextCheckSpec.None, TextCheckSpec.PathScoresOnly)] = (TextCheckSpec.ScoreOnly, TextCheckSpec.NoChange, null),
    };

    // 5.3 표의 행을 길로 편다(모두 → 표 대조·점수표만).
    public static TheoryData<string, string, string> VerdictRows
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach (var c in TextCheckSpec.Table(TextCheckSpec.ScoreVerdictHeader))
            {
                var path = TextCheckSpec.Cell(c[3]);
                foreach (var p in path == TextCheckSpec.PathAll ? new[] { TextCheckSpec.PathTable, TextCheckSpec.PathScoresOnly } : [path])
                {
                    data.Add(TextCheckSpec.Cell(c[1]), c[2].Trim() == TextCheckSpec.None ? TextCheckSpec.None : TextCheckSpec.Cell(c[2]), p);
                }
            }

            return data;
        }
    }

    public static TheoryData<string> CheckResults =>
        new(TextCheckSpec.Table(TextCheckSpec.ScoreCheckHeader).Select(c => TextCheckSpec.Cell(c[1])));

    // 문턱 0 문장 표 × 0 글 둘(0, 0.0 모두 값으로 본다).
    public static TheoryData<string, string, string> ZeroRows
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach (var c in TextCheckSpec.Table(TextCheckSpec.ZeroSentenceHeader))
            {
                foreach (var zero in new[] { "0", "0.0" })
                {
                    data.Add(c[0].Trim(), TextCheckSpec.Cell(c[2]), zero);
                }
            }

            return data;
        }
    }

    // 5.4 장면: 문턱 하나를 기본값으로 두거나(defaults가 있는데 읽지 않는 장면 포함), 점수표만이 쓰지 않는 문턱만 뺀다.
    // 기대 표시는 장면마다 적고, 테스트는 5.4 표로도 다시 구해 둘이 같은지 본다.
    public static TheoryData<string, string, bool> MarkScenes => new()
    {
        { TextCheckSpec.Example, RevealOne + "; " + NoMissing, true },
        { TextCheckSpec.Example, RevealOne + "; " + NoGap, false },
        { TextCheckSpec.Example, RevealOne + "; " + NoTime, false },
        { TextCheckSpec.Example, GapPoint3 + "; " + NoMissing, true },
        { TextCheckSpec.Example, GapPoint3 + "; " + NoTime, false },
        { TextCheckSpec.Example, NoGap, true },
        { TextCheckSpec.Example, NoTime, true },
        { TextCheckSpec.Example, SwapSources + "; " + NoGap, true },
        { TextCheckSpec.Example, SwapSources + "; " + NoGap + "; " + AllChecklistFound, true },
        { TextCheckSpec.ScoreOnly, RevealScoreOnly + "; " + NoGap, false },
        { TextCheckSpec.ScoreOnly, RevealScoreOnly + "; " + NoTime, false },
        { TextCheckSpec.ScoreOnly, GapPoint3 + "; " + NoTime, false },
        { TextCheckSpec.ScoreOnly, NoGap, true },
        { TextCheckSpec.ScoreOnly, NoTime, true },
        { TextCheckSpec.ScoreOnly, SwapSources + "; " + NoGap, true },
        { TextCheckSpec.ScoreOnly, NoUnusedThresholds, false },
        { TextCheckSpec.Example, "@nochecklist; " + NoTime, false },
        { TextCheckSpec.Example, "@nochecklist; " + SwapSources + "; " + NoGap, true },
        { TextCheckSpec.Example, "@nochecklist; " + GapPoint3, false },
    };

    // 5.1: score_gap_max가 없으면 기본값 0을 쓰고 defaults에 적는다. confirm_thresholds의 {defaults}에 "score_gap_max 0".
    [Fact]
    public void Score_gap_default_is_table_value_and_listed_in_defaults()
    {
        var row = Assert.Single(TextCheckSpec.Table(TextCheckSpec.ScoreDefaultHeader));
        Assert.Equal(ScoreGapMax, TextCheckSpec.Cell(row[0]));
        var value = TextCheckSpec.Cell(row[1]);

        var judgment = Run(TextCheckSpec.Example, NoGap).JudgmentJson;

        Assert.Equal(value, judgment.GetProperty("thresholds").GetProperty(ScoreGapMax).GetRawText());
        Assert.Equal([ScoreGapMax], TextCheckInput.Strings(judgment.GetProperty("defaults")));
        Assert.Equal(Confirm($"{ScoreGapMax} {value}"), TodoText(judgment, ConfirmThresholds));
    }

    // 5.1 "judgment.md 4.2 표 뒤 순서": 문턱을 모두 빼면 defaults는 4.2 표의 셋 + score_gap_max.
    [Fact]
    public void Score_gap_default_comes_after_the_three_defaults()
    {
        var judgment = Run(TextCheckSpec.Example, NoThresholds).JudgmentJson;
        var defaults = JudgmentSpec.Defaults().Append((ScoreGapMax, ScoreGapValue())).ToArray();

        Assert.Equal(defaults.Select(d => d.Item1), TextCheckInput.Strings(judgment.GetProperty("defaults")));
        Assert.Equal(defaults.Select(d => d.Item1), TextCheckInput.Names(judgment.GetProperty("thresholds")));
        Assert.Equal(Confirm(string.Join(", ", defaults.Select(d => $"{d.Item1} {d.Item2}"))), TodoText(judgment, ConfirmThresholds));
    }

    // 5.1: 점수표만은 그 길이 읽는 문턱(5.4 점수표만 행의 time_must_drop·score_gap_max)만 defaults에 적는다.
    // thresholds 객체 모양은 그대로다(accuracy_min·missing_max는 기본값이 들어 있지만 쓰이지 않는다).
    [Fact]
    public void Score_only_defaults_hold_only_thresholds_it_reads()
    {
        // 5.4 점수표만 행에 나오는 문턱(4.2 표 순서 + score_gap_max).
        var listed = TextCheckSpec.Table(TextCheckSpec.ScoreReadHeader)
            .Where(c => TextCheckSpec.Cell(c[3]) == TextCheckSpec.PathScoresOnly)
            .SelectMany(c => TextCheckSpec.List(c[4]))
            .ToHashSet(StringComparer.Ordinal);
        var read = JudgmentSpec.Defaults().Select(d => d.Key).Append(ScoreGapMax).Where(listed.Contains).ToArray();
        Assert.Equal([JudgmentSpec.TimeMustDrop, ScoreGapMax], read);

        var judgment = Run(TextCheckSpec.ScoreOnly, NoThresholds).JudgmentJson;
        var thresholds = judgment.GetProperty("thresholds");

        Assert.Equal(read, TextCheckInput.Strings(judgment.GetProperty("defaults")));
        Assert.Equal(JudgmentSpec.DefaultOf(JudgmentSpec.AccuracyMin), thresholds.GetProperty(JudgmentSpec.AccuracyMin).GetRawText());
        Assert.Equal(JudgmentSpec.DefaultOf(JudgmentSpec.MissingMax), thresholds.GetProperty(JudgmentSpec.MissingMax).GetRawText());
        Assert.Equal(Confirm($"{JudgmentSpec.TimeMustDrop} {JudgmentSpec.DefaultOf(JudgmentSpec.TimeMustDrop)}, {ScoreGapMax} {ScoreGapValue()}"), TodoText(judgment, ConfirmThresholds));
    }

    // 5.2 표: 결과마다 SCORE 확인 문장 = 틀을 장면 값으로 채운 것. SCORE는 확인 다섯 뒤 여섯째.
    [Theory]
    [MemberData(nameof(CheckResults))]
    public void Score_check_sentence_matches_table(string result)
    {
        var row = Assert.Single(TextCheckSpec.Table(TextCheckSpec.ScoreCheckHeader), c => TextCheckSpec.Cell(c[1]) == result);
        Assert.Equal(Score, TextCheckSpec.Cell(row[0]));
        var change = result switch
        {
            Revealed => RevealOne,
            NotMet => GapPoint3,
            _ => TextCheckSpec.NoChange,
        };

        var run = Run(TextCheckSpec.Example, change);
        var check = run.JudgmentJson.GetProperty("checks").EnumerateArray().Last();

        Assert.Equal((Score, result), (check.GetProperty("rule").GetString(), check.GetProperty("result").GetString()));
        Assert.Equal(TextCheckSpec.Fill(TextCheckSpec.Cell(row[4]), Slots(run, GapText(run))), check.GetProperty("reason").GetString());
    }

    // 5.2 "읽는 문턱": met·not_met은 score_gap_max를 읽어 기본값이면 표시가 붙고, revealed는 읽지 않아 붙지 않는다.
    [Fact]
    public void Score_check_default_mark_follows_read_threshold()
    {
        var reads = TextCheckSpec.Table(TextCheckSpec.ScoreCheckHeader).ToDictionary(c => TextCheckSpec.Cell(c[1]), c => TextCheckSpec.List(c[3]), StringComparer.Ordinal);
        Assert.Empty(reads[Revealed]);
        Assert.Equal([ScoreGapMax], reads[Met]);
        Assert.Equal([ScoreGapMax], reads[NotMet]);

        var notMet = ScoreCheck(Run(TextCheckSpec.Example, NoGap));
        var revealed = ScoreCheck(Run(TextCheckSpec.Example, RevealOne + "; " + NoGap));
        var met = ScoreCheck(Run(TextCheckSpec.Example, SwapSources + "; " + NoGap));

        Assert.Equal(NotMet, notMet.GetProperty("result").GetString());
        Assert.EndsWith(JudgmentSpec.DefaultMark, notMet.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Equal(Met, met.GetProperty("result").GetString());
        Assert.EndsWith(JudgmentSpec.DefaultMark, met.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Equal(Revealed, revealed.GetProperty("result").GetString());
        Assert.DoesNotContain(JudgmentSpec.DefaultMark, revealed.GetProperty("reason").GetString(), StringComparison.Ordinal);
    }

    // 5.2 걸리는 조건의 경계: 같으면 met. AI 점수 셋을 1점씩 올리면 AI 합 142/36, 사람 151/36이고 차이가 정확히 0.25다.
    // 글 그대로: 0.250이면 문장에도 0.250이다.
    [Theory]
    [InlineData("0.25", Met)]
    [InlineData("0.250", Met)]
    [InlineData("0.24", NotMet)]
    [InlineData("0.26", Met)]
    public void Score_gap_boundary_is_inclusive(string gap, string result)
    {
        using var folder = TextCheckInput.Folder(TextCheckSpec.ScoreOnly, $"`case.yaml`: `  score_gap_max: 0.5\\n` → `  score_gap_max: {gap}\\n`");
        RaiseAiScores(folder.Root, 3);

        var run = TextCheckInput.Valid(TextCheckInput.Run(folder.Root));
        var ai = SourceOf(run.MetricsJson, TextCheckSpec.SourceAi);
        var check = ScoreCheck(run);
        var template = TextCheckSpec.Cell(Assert.Single(TextCheckSpec.Table(TextCheckSpec.ScoreCheckHeader), c => TextCheckSpec.Cell(c[1]) == result)[4]);

        Assert.Equal((142, "3.94"), (ai.GetProperty("sum").GetInt32(), ai.GetProperty("mean").GetRawText()));
        Assert.Equal(result, check.GetProperty("result").GetString());
        Assert.Equal(TextCheckSpec.Fill(template, Slots(run, gap)), check.GetProperty("reason").GetString());
    }

    // 5.2 "셈이 기준": 표시 평균의 차이(4.19 − 3.86 = 0.33)가 문턱과 같아도 합·수 셈(12/36)으로는 넘는다.
    [Theory]
    [InlineData("0.33", NotMet)]
    [InlineData("0.34", Met)]
    public void Score_check_uses_sums_not_displayed_means(string gap, string result)
    {
        var run = Run(TextCheckSpec.ScoreOnly, $"`case.yaml`: `  score_gap_max: 0.5\\n` → `  score_gap_max: {gap}\\n`");

        Assert.Equal(result, ScoreCheck(run).GetProperty("result").GetString());
    }

    // 문턱 0 문장 표: score_gap_max가 0(0, 0.0)이면 "0점 넘게" 대신 이 틀을 쓴다. 확인 met은 출처를 맞바꿔 AI가 높은 장면이다.
    [Theory]
    [MemberData(nameof(ZeroRows))]
    public void Zero_gap_sentences_match_table(string kind, string result, string zero)
    {
        var row = Assert.Single(TextCheckSpec.Table(TextCheckSpec.ZeroSentenceHeader), c => c[0].Trim() == kind && TextCheckSpec.Cell(c[2]) == result);
        Assert.Equal(Score, TextCheckSpec.Cell(row[1]));
        var change = $"`case.yaml`: `  score_gap_max: 0.5\\n` → `  score_gap_max: {zero}\\n`" + (result == Met ? "; " + SwapSources : string.Empty);

        var run = Run(TextCheckSpec.Example, change);
        var judgment = run.JudgmentJson;
        var expected = TextCheckSpec.Fill(TextCheckSpec.Cell(row[3]), Slots(run, zero));

        Assert.Equal(result, ScoreCheck(run).GetProperty("result").GetString());
        if (kind == CheckSentence)
        {
            Assert.Equal(expected, ScoreCheck(run).GetProperty("reason").GetString());
        }
        else
        {
            Assert.Equal(VerdictSentence, kind);
            Assert.Equal((JudgmentSpec.Hold, Score, expected), (judgment.GetProperty("verdict").GetString(), judgment.GetProperty("rule").GetString(), judgment.GetProperty("reason").GetString()));
        }

        Assert.DoesNotContain("점 넘게", judgment.GetRawText(), StringComparison.Ordinal);
    }

    // 문턱 0 문장 표에 확인 met·not_met과 판정 not_met이 있다. revealed는 문턱을 쓰지 않아 그대로다.
    [Fact]
    public void Zero_gap_table_has_three_rows()
    {
        Assert.Equal(
            [(CheckSentence, Met), (CheckSentence, NotMet), (VerdictSentence, NotMet)],
            TextCheckSpec.Table(TextCheckSpec.ZeroSentenceHeader).Select(c => (c[0].Trim(), TextCheckSpec.Cell(c[2]))));
    }

    // 5.3 판정 행마다: 그 장면의 판정·근거 키·판정 문장(틀을 장면 값으로 채움), 확인 목록(점수표만은 TIME·SCORE 둘).
    [Theory]
    [MemberData(nameof(VerdictRows))]
    public void Score_verdict_row_gives_its_verdict_and_sentence(string rule, string result, string path)
    {
        var row = Assert.Single(
            TextCheckSpec.Table(TextCheckSpec.ScoreVerdictHeader),
            c => TextCheckSpec.Cell(c[1]) == rule && (c[2].Trim() == TextCheckSpec.None ? TextCheckSpec.None : TextCheckSpec.Cell(c[2])) == result &&
                TextCheckSpec.Cell(c[3]) is var p && (p == path || p == TextCheckSpec.PathAll));
        Assert.True(VerdictScenes.TryGetValue((rule, result, path), out var scene), $"5.3 행 ({rule}, {result}, {path})의 장면이 없다.");
        var change = scene.Change.Length == 0 ? NoChecklist : scene.Change;

        using var folder = TextCheckInput.Folder(scene.Base, change);
        var run = TextCheckInput.Valid(TextCheckInput.Run(folder.Root, scene.Times));
        var judgment = run.JudgmentJson;
        var slots = Slots(run, GapText(run));
        if (scene.Times is { } times)
        {
            slots["total"] = JudgmentInput.Text(times.ProcessMinutes + times.ReviewMinutes);
            slots["before"] = JudgmentInput.Text(times.BeforeMinutes);
        }

        Assert.Equal(
            (TextCheckSpec.Cell(row[4]), rule, TextCheckSpec.Fill(TextCheckSpec.Cell(row[5]), slots)),
            (judgment.GetProperty("verdict").GetString(), judgment.GetProperty("rule").GetString(), judgment.GetProperty("reason").GetString()));
        Assert.Equal(path, PathOf(run));
        string[] checks = path == TextCheckSpec.PathScoresOnly ? [JudgmentSpec.Time, Score] : [.. JudgmentSpec.CheckOrder(), Score];
        Assert.Equal(checks, judgment.GetProperty("checks").EnumerateArray().Select(c => c.GetProperty("rule").GetString()));
    }

    // 5.3 표의 자리: 4 뒤(점수 보류 → 대조할 열 없음) → 5(TIME) → 8(PASS). 보류가 조건부보다 앞이다.
    [Fact]
    public void Score_verdict_rows_keep_hold_before_conditional_before_pass()
    {
        var rows = TextCheckSpec.Table(TextCheckSpec.ScoreVerdictHeader)
            .Select(c => (Place: TextCheckSpec.Cell(c[0]), Rule: TextCheckSpec.Cell(c[1]), Verdict: TextCheckSpec.Cell(c[4])))
            .ToArray();

        Assert.Equal(
            [(AfterFour, Score), (AfterFour, Score), (AfterFour, JudgmentSpec.NotVerifiedRule), ("5", JudgmentSpec.Time), ("5", JudgmentSpec.Time), ("8", JudgmentSpec.PassRule)],
            rows.Select(r => (r.Place, r.Rule)));
        Assert.Equal(
            [JudgmentSpec.Hold, JudgmentSpec.Hold, JudgmentSpec.Conditional, JudgmentSpec.Conditional, JudgmentSpec.Conditional, JudgmentSpec.Pass],
            rows.Select(r => r.Verdict));
    }

    // 5.4: 판정 문장의 기본값 표시는 그 판정 행이 읽은 문턱(5.4 표) 가운데 기본값이 있을 때만 붙는다. 한 번만.
    [Theory]
    [MemberData(nameof(MarkScenes))]
    public void Verdict_default_mark_follows_read_threshold_table(string baseName, string change, bool mark)
    {
        var run = Run(baseName, change.Replace("@nochecklist", NoChecklist, StringComparison.Ordinal));
        var judgment = run.JudgmentJson;
        var defaults = TextCheckInput.Strings(judgment.GetProperty("defaults"));

        var read = ReadThresholds(run);
        var reason = judgment.GetProperty("reason").GetString()!;

        Assert.Equal(mark, read.Intersect(defaults, StringComparer.Ordinal).Any());
        Assert.Equal(mark, reason.EndsWith(JudgmentSpec.DefaultMark, StringComparison.Ordinal));
        Assert.True(reason.IndexOf(JudgmentSpec.DefaultMark, StringComparison.Ordinal) == reason.LastIndexOf(JudgmentSpec.DefaultMark, StringComparison.Ordinal));
        if (PathOf(run) == TextCheckSpec.PathScoresOnly)
        {
            Assert.All(defaults, d => Assert.Contains(d, new[] { JudgmentSpec.TimeMustDrop, ScoreGapMax }));
        }
    }

    // 5.4 표 모양: 표 대조 3행·점수표만 5행. 5~8은 근거 키·결과와 상관없다(—).
    [Fact]
    public void Read_threshold_table_covers_both_paths()
    {
        var rows = TextCheckSpec.Table(TextCheckSpec.ScoreReadHeader);

        Assert.Equal(3, rows.Count(c => TextCheckSpec.Cell(c[3]) == TextCheckSpec.PathTable));
        Assert.Equal(5, rows.Count(c => TextCheckSpec.Cell(c[3]) == TextCheckSpec.PathScoresOnly));
        var fiveToEight = Assert.Single(rows, c => TextCheckSpec.Cell(c[0]) == FiveToEight);
        Assert.Equal((TextCheckSpec.None, TextCheckSpec.None), (fiveToEight[1].Trim(), fiveToEight[2].Trim()));
        Assert.Equal([JudgmentSpec.AccuracyMin, JudgmentSpec.MissingMax, JudgmentSpec.TimeMustDrop, ScoreGapMax], TextCheckSpec.List(fiveToEight[4]));
    }

    // 5.5: rescore_blind 문구 = 표, 자리는 add_group 뒤·confirm_thresholds 앞(judgment.md 4.7 순서에 끼운다).
    [Fact]
    public void Rescore_blind_todo_text_and_place_match_table()
    {
        var row = Assert.Single(TextCheckSpec.Table(TextCheckSpec.ScoreTodoHeader));
        Assert.Equal(RescoreBlind, TextCheckSpec.Cell(row[0]));
        var order = JudgmentSpec.TodoOrder().ToList();
        order.Insert(order.IndexOf(AddGroup) + 1, RescoreBlind);
        Assert.Equal(order.IndexOf(ConfirmThresholds) - 1, order.IndexOf(RescoreBlind));

        var judgment = Run(TextCheckSpec.Example, NoGroup + "; " + RevealOne + "; " + NoGap).JudgmentJson;
        var keys = judgment.GetProperty("todo").EnumerateArray().Select(t => t.GetProperty("key").GetString()!).ToArray();

        Assert.Equal([ReviewItems, AddGroup, RescoreBlind, ConfirmThresholds], keys);
        Assert.Equal(order.Where(keys.Contains), keys);
        Assert.Equal(TextCheckSpec.Cell(row[2]), TodoText(judgment, RescoreBlind));
    }

    // 5.5: rescore_blind는 SCORE = revealed일 때만. 가림이 지켜지면 없다.
    [Fact]
    public void Rescore_blind_appears_only_when_revealed()
    {
        Assert.DoesNotContain(RescoreBlind, TodoKeys(Run(TextCheckSpec.Example, TextCheckSpec.NoChange).JudgmentJson));
        Assert.DoesNotContain(RescoreBlind, TodoKeys(Run(TextCheckSpec.Example, GapPoint3).JudgmentJson));
        Assert.Contains(RescoreBlind, TodoKeys(Run(TextCheckSpec.ScoreOnly, RevealScoreOnly).JudgmentJson));
    }

    // 5.6: 정의와 대조 결과가 맞지 않으면 ArgumentException. 점수표 있음/없음, 체크리스트 있음/없음, 항목 이름.
    [Theory]
    [InlineData(TextCheckSpec.NoChange, NoScores)]
    [InlineData(NoScores, TextCheckSpec.NoChange)]
    [InlineData(TextCheckSpec.NoChange, "@nochecklist")]
    [InlineData("@nochecklist", TextCheckSpec.NoChange)]
    [InlineData(TextCheckSpec.NoChange, RenameItem)]
    [InlineData(RenameItem, TextCheckSpec.NoChange)]
    public void Mismatched_definition_and_metrics_are_rejected(string definitionChange, string metricsChange)
    {
        var definition = Run(TextCheckSpec.Example, Expand(definitionChange)).Definition!;
        var metrics = Run(TextCheckSpec.Example, Expand(metricsChange)).Metrics!;

        Assert.Throws<ArgumentException>(() => JudgmentEngine.Judge(definition, metrics));
    }

    // 5.6 짝: 같은 정의로 만든 대조 결과는 받는다(위 거절이 아무 정의나 거절하는 것이 아님을 보인다).
    [Theory]
    [InlineData(TextCheckSpec.NoChange)]
    [InlineData(NoScores)]
    [InlineData("@nochecklist")]
    [InlineData(RenameItem)]
    public void Matching_definition_and_metrics_are_judged(string change)
    {
        var run = Run(TextCheckSpec.Example, Expand(change));

        Assert.NotNull(JudgmentEngine.Judge(run.Definition!, run.Metrics!));
    }

    // 5.3 끝 글머리: 점수표만이면서 사람 입력(검토 항목 키)을 받으면 ArgumentException. 표 대조 + 점수표는 받는다.
    [Fact]
    public void Score_only_rejects_unexplained_keys()
    {
        var scoreOnly = Run(TextCheckSpec.ScoreOnly, TextCheckSpec.NoChange);
        var example = Run(TextCheckSpec.Example, TextCheckSpec.NoChange);

        Assert.Throws<ArgumentException>(() => JudgmentEngine.Judge(scoreOnly.Definition!, scoreOnly.Metrics!, null, ["d03"]));
        Assert.Equal(JudgmentSpec.Hold, JudgmentEngine.Judge(example.Definition!, example.Metrics!, null, ["d03"]).Verdict);
    }

    // 장면 바탕에 바꾼 곳을 적용해 돌린다. 오류가 있으면 실패한다.
    internal static TextCheckRun Run(string baseName, string change) => TextCheckInput.Valid(TextCheckInput.Run(baseName, change));

    // 문장 틀 값: 평균 글(3.1)·점수 수·발견 수·쓴 문턱 글.
    internal static Dictionary<string, string> Slots(TextCheckRun run, string gap)
    {
        var (ai, human) = (SourceOf(run.MetricsJson, TextCheckSpec.SourceAi), SourceOf(run.MetricsJson, TextCheckSpec.SourceHuman));
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ai_mean"] = ai.GetProperty("mean").GetRawText(),
            ["ai_ratings"] = ai.GetProperty("ratings").GetRawText(),
            ["human_mean"] = human.GetProperty("mean").GetRawText(),
            ["human_ratings"] = human.GetProperty("ratings").GetRawText(),
            ["findings"] = run.MetricsJson.GetProperty("scores").GetProperty("blind").GetProperty("findings").GetArrayLength().ToString(CultureInfo.InvariantCulture),
            [ScoreGapMax] = gap,
        };
    }

    // 쓴 문턱 글: case.yaml 글 그대로 또는 기본값 0(judgment.json thresholds의 글).
    private static string GapText(TextCheckRun run) => run.JudgmentJson.GetProperty("thresholds").GetProperty(ScoreGapMax).GetRawText();

    private static string ScoreGapValue() => TextCheckSpec.Cell(Assert.Single(TextCheckSpec.Table(TextCheckSpec.ScoreDefaultHeader))[1]);

    private static JsonElement SourceOf(JsonElement metrics, string source) =>
        Assert.Single(metrics.GetProperty("scores").GetProperty("sources").EnumerateArray(), s => s.GetProperty("source").GetString() == source);

    private static JsonElement ScoreCheck(TextCheckRun run) =>
        Assert.Single(run.JudgmentJson.GetProperty("checks").EnumerateArray(), c => c.GetProperty("rule").GetString() == Score);

    private static string Confirm(string defaults) =>
        JudgmentSpec.Fill(JudgmentSpec.TodoTemplate(ConfirmThresholds), new Dictionary<string, string> { ["defaults"] = defaults });

    private static string TodoText(JsonElement judgment, string key) =>
        Assert.Single(judgment.GetProperty("todo").EnumerateArray(), t => t.GetProperty("key").GetString() == key).GetProperty("text").GetString()!;

    private static string[] TodoKeys(JsonElement judgment) =>
        judgment.GetProperty("todo").EnumerateArray().Select(t => t.GetProperty("key").GetString()!).ToArray();

    // 5.3 길: 대조함 + 점수표 = 표 대조, 대조 미검증 + 점수표 = 점수표만.
    private static string PathOf(TextCheckRun run) =>
        run.MetricsJson.GetProperty("compare").GetProperty("status").GetString() == "measured" ? TextCheckSpec.PathTable : TextCheckSpec.PathScoresOnly;

    // 5.4 표에서 이 판정 행이 읽은 문턱.
    private static string[] ReadThresholds(TextCheckRun run)
    {
        var judgment = run.JudgmentJson;
        var rule = judgment.GetProperty("rule").GetString()!;
        var path = PathOf(run);
        var rows = TextCheckSpec.Table(TextCheckSpec.ScoreReadHeader);
        string[] Row(string place, string ruleCell, string result) =>
            TextCheckSpec.List(Assert.Single(rows, c => TextCheckSpec.Cell(c[0]) == place && TextCheckSpec.Cell(c[1]) == ruleCell &&
                TextCheckSpec.Cell(c[2]) == result && TextCheckSpec.Cell(c[3]) == path)[4]);

        return rule switch
        {
            Score => Row(AfterFour, Score, ScoreCheck(run).GetProperty("result").GetString()!),
            JudgmentSpec.NotVerifiedRule => Row(AfterFour, rule, run.MetricsJson.GetProperty("compare").GetProperty("reason").GetString()!),
            _ when path == TextCheckSpec.PathTable => Row(FiveToEight, TextCheckSpec.None, TextCheckSpec.None),
            JudgmentSpec.Time => Row("5", rule, TextCheckSpec.None),
            _ => Row("8", rule, TextCheckSpec.None),
        };
    }

    private static string Expand(string change) => change.Replace("@nochecklist", NoChecklist, StringComparison.Ordinal);

    // scores/sheet.csv에서 출처 ai인 점수 n개(5 미만, 위에서부터)를 1점씩 올린다.
    private static void RaiseAiScores(string root, int count)
    {
        var sources = TextCheckInput.Read(root, "scores/sources.csv").Split('\n').Skip(1).Where(l => l.Length > 0)
            .Select(l => l.Split(',')).ToDictionary(c => c[0], c => c[2], StringComparer.Ordinal);
        var lines = TextCheckInput.Read(root, "scores/sheet.csv").Split('\n');
        var raised = 0;
        for (var i = 1; i < lines.Length && raised < count; i++)
        {
            var cells = lines[i].Split(',');
            if (cells.Length == 4 && sources[cells[2]] == TextCheckSpec.SourceAi && cells[3] != "5")
            {
                cells[3] = (int.Parse(cells[3], CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture);
                lines[i] = string.Join(',', cells);
                raised++;
            }
        }

        Assert.Equal(count, raised);
        TextCheckInput.Write(root, "scores/sheet.csv", string.Join('\n', lines));
    }
}
