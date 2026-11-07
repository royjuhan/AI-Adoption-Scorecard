using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;
using Scorecard.Tests.Classification;
using Scorecard.Tests.Comparison;

namespace Scorecard.Tests.Judgment;

// 스펙 3.3·4.12 결정론과 원본 불변. G2·G3(다른 경로·다른 프로세스 문화권)는 CoreGuardTargetTests·JudgeGuardTargetTests가 본다.
// 같은 입력이면 같은 바이트: 두 번, 문화권 7종. 판정은 입력(CompareMetrics, 사람 입력 목록)을 바꾸지 않는다.
[Trait("Spec", "judgment")]
public class JudgmentDeterminismTests
{
    // 3.3 [T11:S3 culture]의 문화권 7종. ""는 InvariantCulture.
    private static readonly string[] Cultures = ["", "de-DE", "tr-TR", "ar-SA", "ko-KR", "fr-CH", "th-TH"];

    public static TheoryData<string> Examples => ClassificationInput.ExampleNames;

    // 같은 입력을 두 번 판정하면 같은 바이트다.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Same_input_twice_gives_same_bytes(string example)
    {
        var (definition, metrics) = JudgmentGolden.Inputs(example);

        Assert.Equal(JudgmentJson.Render(JudgmentEngine.Judge(definition, metrics)), JudgmentJson.Render(JudgmentEngine.Judge(definition, metrics)));
    }

    // 문화권을 바꿔도(소수점이 ','인 de-DE 등) 예제 바이트가 expected와 같다. try/finally로 되돌린다.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Culture_does_not_change_example_bytes(string example)
    {
        var expected = JudgmentGolden.ExpectedBytes(example);

        foreach (var culture in Cultures)
        {
            var actual = CompareInput.WithCulture(culture, () => JudgmentGolden.ExampleBytes(example));
            Assert.True(expected.AsSpan().SequenceEqual(actual), $"{example}: 문화권 '{culture}'에서 바이트가 다르다.");
        }
    }

    // 소수가 많은 장면(문턱 66.6·1.5, 시간 10.5·6.25·1.2, 퍼센트 소수 한 자리)도 문화권 7종에서 같은 바이트다.
    // 문장 안의 숫자는 InvariantCulture 글자다(3.3 둘째 글머리): '.'이고 ','가 아니다.
    [Fact]
    public void Culture_does_not_change_decimal_sentences()
    {
        var scene = new JudgmentScene(
            Columns: [JudgmentInput.Group],
            Rows: JudgmentInput.Rows(30, mismatch: 2, types: i => [i < 2 ? "a" : "b"]),
            Extras: [],
            AccuracyMin: "66.6",
            MissingMax: "1.5",
            TimeMustDrop: "true",
            Times: new JudgmentTimes(10.5m, 6.25m, 1.2m),
            Unexplained: []);

        var invariant = CompareInput.WithCulture(string.Empty, scene.Render);
        var text = CompareInput.Text(invariant);

        Assert.Contains("정확도 93.3%(28/30)가 문턱 66.6% 이상이다.", text, StringComparison.Ordinal);
        Assert.Contains("처리 6.25분 + 검토 1.2분 = 7.45분이 적용 전 10.5분보다 짧다.", text, StringComparison.Ordinal);
        Assert.Contains("\n    \"missing_max\": 1.5,\n", text, StringComparison.Ordinal);
        Assert.All(Cultures, c => Assert.True(invariant.AsSpan().SequenceEqual(CompareInput.WithCulture(c, scene.Render)), $"문화권 '{c}'에서 바이트가 다르다."));
    }

    // 4.12: 입력을 바꾸지 않는다. 판정 전후 metrics.json 바이트·항목이 같다(예제 4종).
    [Theory]
    [MemberData(nameof(Examples))]
    public void Judge_does_not_change_compare_metrics(string example)
    {
        var (definition, metrics) = JudgmentGolden.Inputs(example);
        var before = MetricsJson.Render(metrics);
        var items = metrics.Items.Select(i => (i.Key, i.Status, i.Reference?.Cells.Count, i.Differences.Count)).ToArray();

        JudgmentEngine.Judge(definition, metrics);

        Assert.Equal(before, MetricsJson.Render(metrics));
        Assert.Equal(items, metrics.Items.Select(i => (i.Key, i.Status, i.Reference?.Cells.Count, i.Differences.Count)));
        Assert.Equal(CompareInput.ExpectedBytes(example), before);
    }

    // 4.12: 사람 입력 목록과 시간도 바꾸지 않는다.
    [Fact]
    public void Judge_does_not_change_human_input_or_times()
    {
        var scene = JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.NotReviewed, JudgmentInput.Met, JudgmentInput.Spread);
        string[] keys = [scene.BadKeys[1], scene.BadKeys[0]];
        var times = new JudgmentTimes(10m, 6m, 3m);
        var definition = scene.Definition;
        var metrics = scene.Metrics();

        JudgmentEngine.Judge(definition, metrics, times, keys);

        Assert.Equal([scene.BadKeys[1], scene.BadKeys[0]], keys);
        Assert.Equal(new JudgmentTimes(10m, 6m, 3m), times);
    }
}
