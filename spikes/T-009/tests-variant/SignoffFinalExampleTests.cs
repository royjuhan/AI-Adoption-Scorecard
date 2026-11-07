using Scorecard.Core.Classification;
using Scorecard.Core.Synthetic;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Comparison;

namespace Scorecard.Tests.Synthetic;

// T-009 스파이크의 새 테스트 제안(spikes/T-009/tests-variant, 머지 대상 아님). test-engineer가 옮길 때 참고한다.
// 4번째 공개 예제 signoff-final(docs/spec/signoff-final.md)의 예제별 기대값. 공통 골든·블록 테스트는 예제 목록(V1~V5)으로 돈다.
[Trait("Spec", "signoff-final")]
public class SignoffFinalExampleTests
{
    private const string Example = "signoff-final";

    // 카드 수용 기준 2: 7단계는 ④ SIGNOFF 한 행이다. 게이트·예외 검토 행이 없다. 다른 단계에는 SIGNOFF가 없다.
    [Fact]
    public void Step_seven_is_a_single_signoff_row_without_gate()
    {
        var result = StepClassifier.Classify(SyntheticInput.CaseOf(Example));

        var row = Assert.Single(Assert.Single(result.Steps, s => s.Number == 7).Rows);
        Assert.Equal(("④", "SIGNOFF", StepClassifier.KindStep), (row.Assignee, row.Rule, row.Kind));
        Assert.All(result.Steps.Where(s => s.Number != 7), s => Assert.DoesNotContain(s.Rows, r => r.Rule == "SIGNOFF"));
        Assert.Equal(9, result.Steps.Sum(s => s.Rows.Count));
    }

    // 12.1과 같은 구조: 1~6단계의 근거 키가 TestData 12.1 합성 사본(바꾸지 않음)과 같다. 7단계만 Q2+GATE → SIGNOFF.
    [Fact]
    public void Steps_one_to_six_match_the_12_1_copy()
    {
        static string[] Rules(ClassificationResult r) => r.Steps.Select(s => string.Join("+", s.Rows.Select(x => x.Rule))).ToArray();
        var copy = Rules(StepClassifier.Classify(CaseInput.Valid(CaseInput.Parse(CaseInput.Sample(SpecSamples.Example121)))));
        var example = Rules(StepClassifier.Classify(SyntheticInput.CaseOf(Example)));

        Assert.Equal(7, example.Length);
        Assert.Equal(copy[..6], example[..6]);
        Assert.Equal(("Q2+GATE", "SIGNOFF"), (copy[6], example[6]));
    }

    // 모든 단계에 답 10개를 적었다. 기본값은 minutes_per_item뿐이다(시간 자료 없음). 문장에 기본값 표시가 없다.
    [Fact]
    public void Every_step_writes_all_ten_answers_except_minutes()
    {
        var result = StepClassifier.Classify(SyntheticInput.CaseOf(Example));

        Assert.All(result.Steps, s => Assert.Equal(["minutes_per_item"], s.Defaults));
        Assert.All(result.Steps.SelectMany(s => s.Rows), r => Assert.DoesNotContain("(기본값 사용, 확인 필요)", r.Reason, StringComparison.Ordinal));
    }

    // 자료 구조 안 A: 정확도 80.0, 누락률 5.0. case.yaml 문턱(95·2)에 못 미친다. 판정(보류 등)은 판정 카드가 정한다.
    // 문턱 비교는 셈으로도 본다(metrics.md 4.8 권고).
    [Fact]
    public void Metrics_are_below_thresholds()
    {
        var metrics = CompareInput.Measured(CompareInput.Example(Example));
        var thresholds = SyntheticInput.CaseOf(Example).Thresholds;
        var c = metrics.Counts!;

        Assert.Equal((80.0m, 5.0m), (metrics.AccuracyPct!.Value, metrics.MissingRatePct!.Value));
        Assert.Equal((95m, 2m, false), (thresholds.AccuracyMin!.Value, thresholds.MissingMax!.Value, thresholds.TimeMustDrop!.Value));
        Assert.True(c.Match * 100m < thresholds.AccuracyMin * c.Reference);
        Assert.True(c.Missing * 100m > thresholds.MissingMax * c.Reference);
    }

    // synthetic-examples 4.2 시드 고르기: 기준 20행 중 record_kind = unsteady 수가 answers.yaml 3단계 batch_size(6)인 가장 작은 시드.
    [Fact]
    public void Seed_is_the_smallest_with_unsteady_count_equal_to_step_three_batch()
    {
        var definition = SyntheticInput.CaseOf(Example);
        var plan = SyntheticSpec.Plan(Example);
        var target = definition.Steps.Single(s => s.Number == 3).Answers.BatchSize.Value;
        var i = plan.Columns.Select((c, n) => (c, n)).Single(x => x.c.Name == "record_kind").n;
        int Unsteady(ulong seed) => SyntheticGenerator.Generate(definition, plan with { Seed = seed }).Reference.Count(r => r.Values[i] == "unsteady");

        Assert.Equal(6, target);
        Assert.Equal(target, Unsteady(plan.Seed));
        for (ulong seed = 0; seed < plan.Seed; seed++)
        {
            Assert.NotEqual(target, Unsteady(seed));
        }
    }
}
