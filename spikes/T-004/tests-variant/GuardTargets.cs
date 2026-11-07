using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Guards;

// T-004 스파이크 변형(머지 대상 아님). 저장소의 GuardTargets.cs에 분류 대상 하나를 더한 모양.
// 분류 어댑터: case.yaml → CaseParser.ParseDirectory → StepClassifier.Classify → ClassificationJson.Render
// → 출력 폴더의 classification.json 한 파일.
internal static class GuardTargets
{
    public const string ResultFile = "parse-result.json";
    public const string ClassificationFile = "classification.json";

    public static readonly GuardTarget[] All =
    [
        new("case-parser-valid", "case-valid", ParseCaseToJson),
        new("case-parser-invalid", "case-invalid", ParseCaseToJson),
        new("classifier-case-valid", "case-valid", ClassifyCaseToJson),
    ];

    public static TheoryData<string> Names => new(All.Select(t => t.Name));

    public static GuardTarget Get(string name) => All.Single(t => t.Name == name);

    private static void ParseCaseToJson(string input, string output)
    {
        var result = CaseParser.ParseDirectory(input);
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, ResultFile), ParseResultJson.Render(result));
    }

    // 정상 표본만 쓴다. 파싱 오류면 분류할 것이 없으므로 예외(G2·G3가 차이로 적는다).
    private static void ClassifyCaseToJson(string input, string output)
    {
        var parsed = CaseParser.ParseDirectory(input);
        var definition = parsed.Case ?? throw new InvalidOperationException("분류 대상 표본이 파싱되지 않는다.");
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, ClassificationFile), ClassificationJson.Render(StepClassifier.Classify(definition)));
    }
}
