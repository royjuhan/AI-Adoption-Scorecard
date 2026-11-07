using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Guards;

// G2·G3의 실제 Core 대상 목록. T-002 승인 결정 7, T-003 스펙 4.11(승인 결정 13).
// 메모리 결과 API는 테스트 쪽 어댑터가 파일로 쓴다(T-002 스펙 4.4).
// 어댑터는 CaseParser.ParseDirectory 결과를 출력 폴더의 parse-result.json 한 파일로 쓴다.
// 오류 표본도 등록한다. 오류 줄에 절대 경로가 섞이면 G2 R3가 잡는다(스펙 4.10).
// T-004 분류(classification.md 4.10, 승인 결정 12): 정상 표본만 등록한다. 오류 표본은 분류할 것이 없다.
// 분류 어댑터는 CaseParser.ParseDirectory → StepClassifier.Classify → ClassificationJson.Render 결과를
// 출력 폴더의 classification.json 한 파일로 쓴다.
// 새 Core 처리 기능을 만들면 이 목록에 더한다.
internal static class GuardTargets
{
    public const string ResultFile = "parse-result.json";

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

    // 정상 표본만 쓴다. 파싱 오류면 분류할 것이 없으므로 예외를 던진다(G2·G3가 차이로 적는다).
    private static void ClassifyCaseToJson(string input, string output)
    {
        var parsed = CaseParser.ParseDirectory(input);
        var definition = parsed.Case ?? throw new InvalidOperationException("분류 대상 표본이 파싱되지 않는다.");
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, ClassificationJson.FileName), ClassificationJson.Render(StepClassifier.Classify(definition)));
    }
}
