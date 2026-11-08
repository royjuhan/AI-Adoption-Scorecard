using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.Classification;

// 스펙 4.13 AI 판단과 규칙 의견(M2-2, ADR 0013). 데이터 6.6 비교 값 표·장면 표, 6.7 AI 판단 표본.
// 장면 표는 기준 답(6.1 줄)에서 "바꾼 답"만 바꾸고 단계에 ai를 붙여 분류한다.
[Trait("Spec", "classification")]
public partial class AiJudgmentTests
{
    private const string None = "(없음)";
    private const string SamplePath = "tests/Scorecard.Tests/TestData/classification/ai-judgment/answers.yaml";

    public static TheoryData<int> Scenes => new(Enumerable.Range(0, ClassificationSpec.Table(ClassificationSpec.AiSceneHeader).Count));

    // 6.6 비교 값 표: 담당 다섯. ②M만 ②로 비교하고 나머지는 그대로다(case-schema 4.14 선택지와 같은 순서).
    [Fact]
    public void Compare_table_maps_learned_model_to_rule_software()
    {
        var rows = ClassificationSpec.Table(ClassificationSpec.AiCompareHeader)
            .Select(c => (Ai: SpecDocument.Unquote(c[0]), Compare: SpecDocument.Unquote(c[1])))
            .ToArray();

        Assert.Equal(["①", "②", "②M", "③", "④"], rows.Select(r => r.Ai));
        Assert.All(rows, r => Assert.Equal(r.Ai == "②M" ? "②" : r.Ai, r.Compare));
    }

    // 6.6 비교 값 표를 엔진으로 본다. 규칙 의견 ② ELSE(기준 답)에서 비교 값이 ②인 담당만 differs가 false다.
    [Fact]
    public void Differs_uses_compare_value()
    {
        foreach (var c in ClassificationSpec.Table(ClassificationSpec.AiCompareHeader))
        {
            var step = WithAi(ClassificationInput.Base, SpecDocument.Unquote(c[0]));

            Assert.Equal(SpecDocument.Unquote(c[1]) != "②", step.Differs);
        }
    }

    // 6.6 장면 표의 한 행: 규칙 의견, differs, AI 과잉 후보, 행(담당·근거 키)이 표와 같다.
    [Theory]
    [MemberData(nameof(Scenes))]
    public void Scene_gives_spec_result(int index)
    {
        var c = ClassificationSpec.Table(ClassificationSpec.AiSceneHeader)[index];
        var changes = c[0].Trim() == None ? string.Empty : SpecDocument.Unquote(c[0]);
        var assignee = c[1].Trim() == None ? null : SpecDocument.Unquote(c[1]);
        var a = ClassificationInput.Answers(changes);

        var step = assignee is null ? ClassificationInput.One(a) : WithAi(a, assignee);

        var opinion = Assert.Single(Labels(c[2]));
        Assert.Equal(new RuleOpinion(opinion.Assignee, opinion.Rule), step.RuleOpinion);
        Assert.Equal(c[3].Trim() == "true", step.Differs);
        Assert.Equal(c[4].Trim() == "true", step.AiOveruseCandidate);
        Assert.Equal(Labels(c[5]).Select(l => $"{l.Assignee} {l.Rule}"), ClassificationInput.RowLabels(step));
        Assert.Equal(assignee is null ? StepClassifier.SourceRule : StepClassifier.SourceAi, step.Source);
    }

    // 장면 표가 담당 다섯, differs 둘, ai 없음, SIGNOFF 뒤 게이트 있음·없음을 모두 쓴다.
    [Fact]
    public void Scene_table_covers_assignees_differs_and_signoff_gate()
    {
        var rows = ClassificationSpec.Table(ClassificationSpec.AiSceneHeader);

        Assert.Equal(["①", "②", "②M", "③", "④"], rows.Where(r => r[1].Trim() != None).Select(r => SpecDocument.Unquote(r[1])).Distinct().Order(StringComparer.Ordinal));
        Assert.Contains(rows, r => r[1].Trim() == None);
        Assert.Equal(["false", "true"], rows.Select(r => r[3].Trim()).Distinct().Order(StringComparer.Ordinal));
        Assert.Contains(rows, r => Labels(r[2])[0].Rule == "SIGNOFF" && r[5].Contains("`GATE`", StringComparison.Ordinal));
        Assert.Contains(rows, r => Labels(r[2])[0].Rule == "SIGNOFF" && r[0].Contains("external", StringComparison.Ordinal) && !r[5].Contains("`GATE`", StringComparison.Ordinal));
    }

    // 4.13 규칙 3: AI 행은 근거 그대로, 표시 없음, Used 빈 목록. 부가 행의 표시는 트리대로 남는다.
    [Fact]
    public void Ai_row_keeps_reason_without_marks()
    {
        var a = ClassificationInput.MarkDefault(ClassificationInput.Answers("rule_expressible: partial"), "rule_expressible");
        var step = WithAi(a, "③", "  쓴 글 그대로 ");

        Assert.Equal(new ClassificationRow(StepClassifier.KindStep, "③", "AI", step.Rows[0].Used, "  쓴 글 그대로 "), step.Rows[0]);
        Assert.Empty(step.Rows[0].Used);
        Assert.EndsWith(ClassificationSpec.DefaultMark, step.Rows[1].Reason, StringComparison.Ordinal);
        Assert.Equal(["rule_expressible"], step.Defaults);
    }

    // ai가 있어도 없어도 규칙 의견, 부가 행, defaults, checks가 같다. 바뀌는 것은 step 행·source·differs·과잉 후보뿐이다.
    [Fact]
    public void Ai_does_not_change_rule_opinion_or_extra_rows()
    {
        var a = ClassificationInput.Answers("rule_expressible: partial, failure_impact: external, minutes_per_item: null");
        var plain = ClassificationInput.One(a);
        var withAi = WithAi(a, "④");

        Assert.Equal(plain.RuleOpinion, withAi.RuleOpinion);
        Assert.Equal(plain.Rows.Skip(1).Select(Shape), withAi.Rows.Skip(1).Select(Shape));
        Assert.Equal(plain.Defaults, withAi.Defaults);
        Assert.Equal(plain.Checks, withAi.Checks);
        Assert.Equal(new RuleOpinion(plain.Rows[0].Assignee, plain.Rows[0].Rule), plain.RuleOpinion);
    }

    // 6.7 표본 파일 = 스펙 블록(바이트).
    [Fact]
    public void Sample_file_equals_spec_block()
    {
        var block = SpecDocument.BlockAfter(ClassificationSpec.Document, ClassificationSpec.AiSampleYamlMarker);

        Assert.Equal(Encoding.UTF8.GetBytes(block), File.ReadAllBytes(RepoRoot.Combine(SamplePath)));
    }

    // 6.7 표본 분류 결과 = 스펙 JSON 블록(바이트). SHA-256·크기 줄도 같다.
    [Fact]
    public void Sample_classification_equals_spec_json_block()
    {
        var expected = Encoding.UTF8.GetBytes(SpecDocument.BlockAfter(ClassificationSpec.Document, ClassificationSpec.AiSampleJsonMarker));
        var actual = ClassificationInput.RenderAnswers(File.ReadAllBytes(RepoRoot.Combine(SamplePath)));
        var line = SpecDocument.Line(ClassificationSpec.Document, ClassificationSpec.AiSampleJsonMarker);

        Assert.Equal(ClassificationInput.Text(expected), ClassificationInput.Text(actual));
        Assert.Equal(SpecDocument.CodeSpans(line)[0], Convert.ToHexStringLower(SHA256.HashData(actual)));
        Assert.Contains($", {actual.Length.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}바이트", line, StringComparison.Ordinal);
    }

    // 6.7 글머리: source·differs·②M·과잉 후보·4단계 확인 항목.
    [Fact]
    public void Sample_steps_follow_spec_bullets()
    {
        var steps = StepClassifier.Classify(ClassificationInput.ParseAnswers(File.ReadAllBytes(RepoRoot.Combine(SamplePath)))).Steps;

        Assert.Equal(["ai", "ai", "ai", "rule", "ai"], steps.Select(s => s.Source));
        Assert.Equal([false, false, true, false, false], steps.Select(s => s.Differs));
        Assert.Equal("②M", steps[1].Rows[0].Assignee);
        Assert.Equal([false, true, false, false, false], steps.Select(s => s.AiOveruseCandidate));
        Assert.Equal([ClassificationSpec.MeasureMinutesPerItem], steps[3].Checks);
        Assert.Equal(["minutes_per_item", "existing_feature"], steps[3].Defaults);
        Assert.Equal([1, 3, 2, 1, 1], steps.Select(s => s.Rows.Count));
    }

    // 표본에서 ai 줄을 모두 빼면 모든 단계가 rule이고, 규칙 의견·부가 행은 그대로다.
    [Fact]
    public void Sample_without_ai_keeps_rule_opinion_and_extra_rows()
    {
        var text = ClassificationInput.Text(File.ReadAllBytes(RepoRoot.Combine(SamplePath)));
        var kept = text.Split('\n').Where(l => l != "    ai:" && !l.StartsWith("      ", StringComparison.Ordinal));
        var withAi = StepClassifier.Classify(ClassificationInput.ParseAnswers(Encoding.UTF8.GetBytes(text))).Steps;
        var plain = StepClassifier.Classify(ClassificationInput.ParseAnswers(Encoding.UTF8.GetBytes(string.Join('\n', kept)))).Steps;

        Assert.All(plain, s => Assert.Equal(StepClassifier.SourceRule, s.Source));
        Assert.All(plain, s => Assert.False(s.Differs));
        Assert.Equal(withAi.Select(s => s.RuleOpinion), plain.Select(s => s.RuleOpinion));
        Assert.Equal(withAi.Select(s => string.Join(" / ", s.Rows.Skip(1).Select(Shape))), plain.Select(s => string.Join(" / ", s.Rows.Skip(1).Select(Shape))));
    }

    private static StepClassification WithAi(StepAnswers a, string assignee, string reason = "근거") =>
        Assert.Single(StepClassifier.Classify(new CaseDefinition(
            "p", "t", null, null, null, [new CaseStep(1, "s", a, new StepAi(assignee, reason))], null, [], new CaseThresholds(null, null, null))).Steps);

    // 행 비교용 글. Used는 목록이라 레코드 같음 비교가 참조를 본다.
    private static string Shape(ClassificationRow r) => $"{r.Kind}|{r.Assignee}|{r.Rule}|{string.Join(",", r.Used)}|{r.Reason}";

    // "②M `AI` + ④ `GATE`" → [(②M, AI), (④, GATE)].
    private static (string Assignee, string Rule)[] Labels(string cell) =>
        RowLabel().Matches(cell).Select(m => (m.Groups["a"].Value, m.Groups["r"].Value)).ToArray();

    [GeneratedRegex(@"(?<a>[①②③④]M?) `(?<r>[A-Z0-9]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex RowLabel();
}
