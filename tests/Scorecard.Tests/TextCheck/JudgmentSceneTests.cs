using Scorecard.Tests.Judgment;

namespace Scorecard.Tests.TextCheck;

// 스펙 6.1 판정 장면 표와 그 아래 글머리의 판정 문장. 시간·사람 입력은 null이다. 카드 수용 기준 1·3.
// 바탕은 7.3 reply-drafts 파일 또는 6.2 score-only 폴더. "바꾼 곳"은 4.4 장면 표 머리말 모양이다(TextCheckInput.Change).
[Trait("Spec", "text-check")]
public class JudgmentSceneTests
{
    private const string ScoreGapMax = "score_gap_max";

    // 5.5 점수표만에서 나올 수 있는 할 일.
    private static readonly string[] ScoreOnlyTodo = ["add_compare", "measure_time", "rescore_blind", "confirm_thresholds"];

    public static TheoryData<string> Scenes => new(TextCheckSpec.Table(TextCheckSpec.JudgmentSceneHeader).Select(c => c[0].Trim()));

    // 6.1 표의 장면 이름 → 바꾼 곳 칸.
    internal static string SceneChange(string scene) => Row(scene)[2].Trim();

    // 행마다 판정·근거 키·확인 결과(checks 순서)·다음 할 일(todo 순서).
    // 점수표만(대조 미검증 + 점수표)이면 checks는 TIME·SCORE, defaults ⊆ {time_must_drop, score_gap_max}, 할 일은 5.5의 넷 안이다.
    [Theory]
    [MemberData(nameof(Scenes))]
    public void Judgment_scene_matches_table(string scene)
    {
        var c = Row(scene);
        var run = TextCheckInput.Valid(TextCheckInput.Run(TextCheckSpec.Cell(c[1]), c[2].Trim()));
        var judgment = run.JudgmentJson;
        var checks = judgment.GetProperty("checks").EnumerateArray().ToArray();
        var todo = judgment.GetProperty("todo").EnumerateArray().Select(t => t.GetProperty("key").GetString()!).ToArray();

        Assert.Equal(
            (TextCheckSpec.Cell(c[3]), TextCheckSpec.Cell(c[4])),
            (judgment.GetProperty("verdict").GetString(), judgment.GetProperty("rule").GetString()));
        Assert.Equal(TextCheckSpec.List(c[5]), checks.Select(x => x.GetProperty("result").GetString()));
        Assert.Equal(TextCheckSpec.List(c[6]), todo);

        if (run.MetricsJson.GetProperty("compare").GetProperty("status").GetString() != "measured")
        {
            Assert.Equal([JudgmentSpec.Time, "SCORE"], checks.Select(x => x.GetProperty("rule").GetString()));
            Assert.All(TextCheckInput.Strings(judgment.GetProperty("defaults")), d => Assert.Contains(d, new[] { JudgmentSpec.TimeMustDrop, ScoreGapMax }));
            Assert.All(todo, t => Assert.Contains(t, ScoreOnlyTodo));
        }
        else
        {
            Assert.Equal([.. JudgmentSpec.CheckOrder(), "SCORE"], checks.Select(x => x.GetProperty("rule").GetString()));
        }
    }

    // 표의 장면 이름은 서로 다르고, 판정 넷 가운데 미검증을 뺀 셋과 근거 키 SCORE·NOT_VERIFIED·PASS가 모두 나온다.
    [Fact]
    public void Scene_table_covers_score_rules()
    {
        var rows = TextCheckSpec.Table(TextCheckSpec.JudgmentSceneHeader);

        Assert.Equal(rows.Count, rows.Select(c => c[0].Trim()).Distinct().Count());
        Assert.Equal(
            new[] { JudgmentSpec.Conditional, JudgmentSpec.Hold, JudgmentSpec.Pass }.Order(StringComparer.Ordinal),
            rows.Select(c => TextCheckSpec.Cell(c[3])).Distinct().Order(StringComparer.Ordinal));
        Assert.All(new[] { "SCORE", JudgmentSpec.NotVerifiedRule, JudgmentSpec.PassRule, JudgmentSpec.Focus, JudgmentSpec.Time }, r => Assert.Contains(rows, c => TextCheckSpec.Cell(c[4]) == r));
        Assert.All(rows, c => Assert.Contains(TextCheckSpec.Cell(c[1]), new[] { TextCheckSpec.Example, TextCheckSpec.ScoreOnly }));
    }

    // 6.1 글머리의 판정 문장(마지막 `코드`) = 그 장면의 판정 문장.
    // "점수 문턱 기본값"은 문턱 0 문장에 기본값 표시가 붙는다. "점수표만, 출처마다 다른 키·열·파일"은 세 장면이 같은 문장이다.
    [Theory]
    [InlineData("점수 문턱 기본값", "점수 문턱 기본값")]
    [InlineData("점수표만, 대조 열 없음", "점수표만, 대조 열 없음")]
    [InlineData("점수표만, 출처마다 다른 키·열·파일", "점수표만, 출처마다 다른 키")]
    [InlineData("점수표만, 출처마다 다른 키·열·파일", "점수표만, 출처마다 다른 열")]
    [InlineData("점수표만, 출처마다 다른 키·열·파일", "점수표만, 출처마다 다른 파일")]
    public void Scene_bullet_sentence_is_verdict_reason(string bullet, string scene)
    {
        var judgment = Judge(scene);

        Assert.Equal(TextCheckSpec.SceneSentence(bullet), judgment.GetProperty("reason").GetString());
    }

    // "점수 문턱 기본값": SCORE 확인 문장 끝에도 기본값 표시가 붙고, 문턱이 0이라 "0점 넘게"가 없다.
    [Fact]
    public void Default_gap_scene_marks_score_check_and_drops_zero_points()
    {
        var judgment = Judge("점수 문턱 기본값");
        var score = judgment.GetProperty("checks").EnumerateArray().Last();

        Assert.EndsWith(JudgmentSpec.DefaultMark, score.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("점 넘게", judgment.GetRawText(), StringComparison.Ordinal);
        Assert.Equal([ScoreGapMax], TextCheckInput.Strings(judgment.GetProperty("defaults")));
    }

    // "점수표만, 쓰지 않는 문턱 기본값": accuracy_min·missing_max가 기본값이어도 defaults가 []이고 confirm_thresholds가 없다.
    [Fact]
    public void Score_only_ignores_unused_threshold_defaults()
    {
        var judgment = Judge("점수표만, 쓰지 않는 문턱 기본값");

        Assert.Empty(TextCheckInput.Strings(judgment.GetProperty("defaults")));
        Assert.DoesNotContain(JudgmentSpec.DefaultMark, judgment.GetRawText(), StringComparison.Ordinal);
    }

    // "점수표만, 출처마다 다른 키·열·파일": source_pattern이 둘씩 걸린다.
    [Theory]
    [InlineData("점수표만, 출처마다 다른 키")]
    [InlineData("점수표만, 출처마다 다른 열")]
    [InlineData("점수표만, 출처마다 다른 파일")]
    public void Split_by_source_scenes_find_two_patterns(string scene)
    {
        var metrics = Metrics(scene);

        Assert.Equal(new Dictionary<string, int> { ["source_pattern"] = 2 }, TextCheckInput.FindingCounts(metrics));
    }

    // "빠진 항목 하나": 불일치는 d03 하나, 정확도 91.7%. "번호 칸 빈 행, 빈 답변": d12는 closing만 빠진 값 불일치, 정확도 91.7%.
    [Theory]
    [InlineData("빠진 항목 하나", "d03", "action")]
    [InlineData("번호 칸 빈 행, 빈 답변", "d12", "closing")]
    public void Single_mismatch_scenes_have_one_item(string scene, string key, string missing)
    {
        var compare = Metrics(scene).GetProperty("compare");
        var item = Assert.Single(compare.GetProperty("items").EnumerateArray());

        Assert.Equal("91.7", compare.GetProperty("accuracy_pct").GetRawText());
        Assert.Equal((key, "mismatch"), (item.GetProperty("key").GetString(), item.GetProperty("status").GetString()));
        Assert.Equal([missing], TextCheckInput.Strings(item.GetProperty("missing_items")));
    }

    // "점수표만": checks가 둘이고 NOT_VERIFIED가 아니다(카드 수용 기준 1). 대조는 미검증 no_inputs.
    [Fact]
    public void Score_only_scene_is_not_not_verified()
    {
        var run = TextCheckInput.Valid(TextCheckInput.Run(TextCheckSpec.ScoreOnly, TextCheckSpec.NoChange));

        Assert.Equal("no_inputs", run.MetricsJson.GetProperty("compare").GetProperty("reason").GetString());
        Assert.NotEqual(JudgmentSpec.NotVerified, run.JudgmentJson.GetProperty("verdict").GetString());
        Assert.Equal(2, run.JudgmentJson.GetProperty("checks").GetArrayLength());
    }

    private static string[] Row(string scene) =>
        Assert.Single(TextCheckSpec.Table(TextCheckSpec.JudgmentSceneHeader), c => c[0].Trim() == scene);

    private static TextCheckRun RunScene(string scene)
    {
        var c = Row(scene);
        return TextCheckInput.Valid(TextCheckInput.Run(TextCheckSpec.Cell(c[1]), c[2].Trim()));
    }

    private static System.Text.Json.JsonElement Judge(string scene) => RunScene(scene).JudgmentJson;

    private static System.Text.Json.JsonElement Metrics(string scene) => RunScene(scene).MetricsJson;
}
