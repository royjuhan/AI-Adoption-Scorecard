using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Classification;

// 스펙 4.11 Core 공개 API(승인 결정 15)와 5절 예외.
// 이 파일은 컴파일 자체가 API 모양 검사다. 타입·이름·매개변수 순서가 다르면 컴파일되지 않는다.
// test-engineer 단계에서는 Core에 Scorecard.Core.Classification이 없어 컴파일이 실패한다(승인 결정 15).
[Trait("Spec", "classification")]
public class ClassificationApiShapeTests
{
    private const string ClassificationNamespace = "Scorecard.Core.Classification";

    // 4.11의 공개 타입 전부. 조건 값·문장 틀·문구 상수는 internal 한 파일에 둔다(4.9).
    private static readonly string[] PublicTypes =
    [
        "ClassificationJson",
        "ClassificationResult",
        "ClassificationRow",
        "RuleOpinion",
        "StepClassification",
        "StepClassifier",
    ];

    [Fact]
    public void Constants_have_spec_values()
    {
        const string step = StepClassifier.KindStep;
        const string exceptionReview = StepClassifier.KindExceptionReview;
        const string humanGate = StepClassifier.KindHumanGate;
        const string fileName = ClassificationJson.FileName;
        const string sourceAi = StepClassifier.SourceAi;
        const string sourceRule = StepClassifier.SourceRule;

        Assert.Equal(ClassificationSpec.KindStep, step);
        Assert.Equal(ClassificationSpec.KindExceptionReview, exceptionReview);
        Assert.Equal(ClassificationSpec.KindHumanGate, humanGate);
        Assert.Equal("classification.json", fileName);
        Assert.Equal("ai", sourceAi);
        Assert.Equal("rule", sourceRule);
    }

    // 4.4 값 표의 행 종류 행과 같다.
    [Fact]
    public void Kinds_match_spec_value_table()
    {
        var row = Assert.Single(ClassificationSpec.Table(ClassificationSpec.ValueHeader), c => c[1] == "행 종류");

        Assert.Equal([StepClassifier.KindStep, StepClassifier.KindExceptionReview, StepClassifier.KindHumanGate], SpecDocument.CodeSpans(row[0]));
    }

    // 공개 메서드 두 개의 서명.
    [Fact]
    public void Entry_points_have_spec_signatures()
    {
        Func<CaseDefinition, ClassificationResult> classify = StepClassifier.Classify;
        Func<ClassificationResult, byte[]> render = ClassificationJson.Render;

        Assert.NotNull(classify);
        Assert.NotNull(render);
    }

    // 결과 레코드 넷의 위치 매개변수 순서와 형식. schema 2(ADR 0013)에서 Source·RuleOpinion·Differs가 Name 뒤에 들어왔다.
    [Fact]
    public void Result_records_have_spec_shape()
    {
        var row = new ClassificationRow(Kind: "step", Assignee: "②", Rule: "ELSE", Used: ["input_kind"], Reason: "r");
        var step = new StepClassification(
            Number: 1,
            Name: "s",
            Source: StepClassifier.SourceRule,
            RuleOpinion: new RuleOpinion(Assignee: "②", Rule: "ELSE"),
            Differs: false,
            Defaults: ["minutes_per_item"],
            Inferred: [],
            Checks: ["measure_minutes_per_item"],
            AiOveruseCandidate: false,
            Rows: [row]);
        var result = new ClassificationResult(Case: "p", Steps: [step]);

        Assert.Same(step, Assert.Single(result.Steps));
        Assert.Same(row, Assert.Single(result.Steps[0].Rows));
        Assert.Equal(new ClassificationRow("step", "②", "ELSE", row.Used, "r"), row);
        Assert.Equal("p", result.Case);
        Assert.Equal(new RuleOpinion("②", "ELSE"), step.RuleOpinion);
    }

    [Fact]
    public void Classification_namespace_exports_only_spec_types()
    {
        var exported = typeof(StepClassifier).Assembly.GetExportedTypes()
            .Where(t => t.Namespace == ClassificationNamespace)
            .Select(t => t.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(PublicTypes, exported);
    }

    // 5절: definition이 null이면 ArgumentNullException.
    [Fact]
    public void Classify_null_throws_argument_null()
    {
        Assert.Throws<ArgumentNullException>(() => StepClassifier.Classify(null!));
    }

    // 4.10: 분류기는 순수 함수다. 입력 모델을 바꾸지 않고, 같은 모델이면 같은 결과다.
    [Fact]
    public void Classify_does_not_change_input_model()
    {
        var definition = ClassificationInput.ParseAnswers(File.ReadAllBytes(ClassificationInput.ExamplePath("table-compare", "answers.yaml")));
        var copy = definition with { Steps = definition.Steps.ToArray() };

        var first = ClassificationJson.Render(StepClassifier.Classify(definition));

        Assert.Equal(copy.Steps, definition.Steps);
        Assert.Equal(first, ClassificationJson.Render(StepClassifier.Classify(copy)));
    }
}
