using Scorecard.Core.Classification;
using Scorecard.Core.Synthetic;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Classification;
using Scorecard.Tests.Comparison;

namespace Scorecard.Tests.Synthetic;

// 4번째 공개 예제 signoff-final의 예제별 기대값(docs/spec/signoff-final.md 5.3 N1, T-009).
// 공통 골든·블록 = 파일·줄바꿈 변형·결정론 테스트는 예제 목록(V1~V5)으로 이 예제도 돈다. 여기서는 이 예제만의 사실을 본다.
// 분류는 answers.yaml과 case.yaml 둘 다에서 본다(두 파일의 name·task·steps는 같아야 한다, CaseYamlTests).
[Trait("Spec", "signoff-final")]
public class SignoffFinalExampleTests
{
    internal const string Example = "signoff-final";

    private const string AnswersFile = "answers.yaml";

    private const string DefaultMark = "(기본값 사용, 확인 필요)";

    // answers.yaml 분류와 case.yaml 분류.
    private static IEnumerable<ClassificationResult> Classifications()
    {
        yield return StepClassifier.Classify(ClassificationInput.ParseAnswers(File.ReadAllBytes(ClassificationInput.ExamplePath(Example, AnswersFile))));
        yield return StepClassifier.Classify(SyntheticInput.CaseOf(Example));
    }

    // 카드 수용 기준 2: 7단계는 ④ SIGNOFF step 한 행이다. 게이트·예외 검토 행이 없다. 다른 단계에는 SIGNOFF가 없다. 행 9.
    // 7단계는 failure_impact: external이라 SIGNOFF가 없으면 게이트가 붙는다. 그래서 "게이트 없음"이 SIGNOFF 때문임을 같이 본다.
    [Fact]
    public void Step_seven_is_a_single_signoff_row_without_gate()
    {
        foreach (var result in Classifications())
        {
            Assert.Equal(7, result.Steps.Count);
            var step = Assert.Single(result.Steps, s => s.Number == 7);
            var row = Assert.Single(step.Rows);
            Assert.Equal(("④", "SIGNOFF", StepClassifier.KindStep), (row.Assignee, row.Rule, row.Kind));
            Assert.Equal(["human_signoff"], row.Used);
            Assert.False(step.AiOveruseCandidate);
            Assert.All(result.Steps.Where(s => s.Number != 7), s => Assert.DoesNotContain(s.Rows, r => r.Rule == "SIGNOFF"));
            Assert.Equal(9, result.Steps.Sum(s => s.Rows.Count));
        }

        var answers = SyntheticInput.CaseOf(Example).Steps.Single(s => s.Number == 7).Answers;
        Assert.Equal(("yes", false), (answers.HumanSignoff.Value, answers.HumanSignoff.IsDefault));
        Assert.Equal(("external", false), (answers.FailureImpact.Value, answers.FailureImpact.IsDefault));
    }

    // 12.1과 같은 구조: 1~6단계의 근거 키가 TestData 12.1 합성 사본(바꾸지 않음)과 같다. 7단계만 Q2+GATE → SIGNOFF.
    [Fact]
    public void Steps_one_to_six_match_the_12_1_copy()
    {
        static string[] Rules(ClassificationResult r) => r.Steps.Select(s => string.Join("+", s.Rows.Select(x => x.Rule))).ToArray();
        var copy = Rules(StepClassifier.Classify(CaseInput.Valid(CaseInput.Parse(CaseInput.Sample(SpecSamples.Example121)))));

        Assert.Equal(7, copy.Length);
        foreach (var result in Classifications())
        {
            var example = Rules(result);
            Assert.Equal(7, example.Length);
            Assert.Equal(copy[..6], example[..6]);
            Assert.Equal(("Q2+GATE", "SIGNOFF"), (copy[6], example[6]));
        }
    }

    // 모든 단계에 답 10개 중 minutes_per_item 말고 모두 적었다(human_signoff 포함). 기본값은 minutes_per_item뿐이다(시간 자료 없음).
    // 그래서 문장에 기본값 표시가 없고, 확인 항목은 measure_minutes_per_item 하나다.
    [Fact]
    public void Every_step_writes_all_ten_answers_except_minutes()
    {
        foreach (var result in Classifications())
        {
            Assert.All(result.Steps, s => Assert.Equal(["minutes_per_item"], s.Defaults));
            Assert.All(result.Steps, s => Assert.Equal(["measure_minutes_per_item"], s.Checks));
            Assert.All(result.Steps, s => Assert.Empty(s.Inferred));
            Assert.All(result.Steps.SelectMany(s => s.Rows), r => Assert.DoesNotContain(DefaultMark, r.Reason, StringComparison.Ordinal));
        }

        Assert.All(SyntheticInput.CaseOf(Example).Steps, s => Assert.False(s.Answers.HumanSignoff.IsDefault));
    }

    // 자료 구조 안 A(juhan J3): 정확도 80.0, 누락률 5.0. case.yaml 문턱(95·2)에 못 미친다. 판정(보류 등)은 판정 카드가 정한다.
    // 문턱 비교는 퍼센트가 아니라 셈으로도 본다(metrics.md 4.8 권고). CaseYamlTests.Structure_meets_thresholds(V6)의 반대 사실이다.
    [Fact]
    public void Metrics_are_below_thresholds()
    {
        var metrics = CompareInput.Measured(CompareInput.Example(Example));
        var thresholds = SyntheticInput.CaseOf(Example).Thresholds;
        var c = metrics.Counts!;

        Assert.Equal((20, 20, 16, 3, 1, 1, 2), (c.Reference, c.Candidate, c.Match, c.Mismatch, c.Missing, c.Extra, c.Within));
        Assert.Equal((80.0m, 5.0m), (metrics.AccuracyPct!.Value, metrics.MissingRatePct!.Value));
        Assert.Equal((95m, 2m, false), (thresholds.AccuracyMin!.Value, thresholds.MissingMax!.Value, thresholds.TimeMustDrop!.Value));
        Assert.True(c.Match * 100m < thresholds.AccuracyMin * c.Reference);
        Assert.True(c.Missing * 100m > thresholds.MissingMax * c.Reference);
    }

    // synthetic-examples 4.2 시드 고르기(L2): 기준 20행 중 record_kind = unsteady 수가 answers.yaml 3단계 batch_size(6)인 가장 작은 시드.
    // 커밋된 기준 CSV의 unsteady 수도 같다.
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

        // 커밋된 CSV는 따옴표 없는 ASCII·LF다(synthetic-examples 3.3). 쉼표로 나눠 센다.
        var lines = File.ReadAllLines(SyntheticInput.ExamplePath(Example, SyntheticInput.CsvRelative(Example, SyntheticCsv.ReferenceFolder)));
        var k = Array.IndexOf(lines[0].Split(','), "record_kind");
        Assert.True(k > 0, "기준 CSV에 record_kind 열이 없다.");
        Assert.Equal(20, lines.Length - 1);
        Assert.Equal(target, lines.Skip(1).Count(l => l.Split(',')[k] == "unsteady"));
    }
}
