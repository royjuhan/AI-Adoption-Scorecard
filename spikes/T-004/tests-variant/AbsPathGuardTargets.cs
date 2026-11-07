using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Guards;

// T-004 스파이크 음성 대조(머지 대상 아님). 분류 어댑터가 출력에 입력 폴더의 절대 경로를 섞으면
// G2 R3가 잡는지 본다. 이 파일은 GuardTargets.cs를 대신한다.
internal static class GuardTargets
{
    public const string ResultFile = "parse-result.json";
    public const string ClassificationFile = "classification.json";

    public static readonly GuardTarget[] All =
    [
        new("case-parser-valid", "case-valid", ParseCaseToJson),
        new("case-parser-invalid", "case-invalid", ParseCaseToJson),
        new("classifier-case-valid", "case-valid", ClassifyWithAbsolutePath),
    ];

    public static TheoryData<string> Names => new(All.Select(t => t.Name));

    public static GuardTarget Get(string name) => All.Single(t => t.Name == name);

    private static void ParseCaseToJson(string input, string output)
    {
        var result = CaseParser.ParseDirectory(input);
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, ResultFile), ParseResultJson.Render(result));
    }

    // 케이스 이름 자리에 입력 폴더의 절대 경로를 넣는다(위반 흉내).
    private static void ClassifyWithAbsolutePath(string input, string output)
    {
        var definition = CaseParser.ParseDirectory(input).Case!;
        var result = StepClassifier.Classify(definition with { Name = Path.GetFullPath(input) });
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, ClassificationFile), ClassificationJson.Render(result));
    }
}
