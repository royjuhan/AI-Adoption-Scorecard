using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;
using Scorecard.Core.Synthetic;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Guards;

// G2·G3의 실제 Core 대상 목록. T-002 승인 결정 7, T-003 스펙 4.11(승인 결정 13).
// 메모리 결과 API는 테스트 쪽 어댑터가 파일로 쓴다(T-002 스펙 4.4).
// 어댑터는 CaseParser.ParseDirectory 결과를 출력 폴더의 parse-result.json 한 파일로 쓴다.
// 오류 표본도 등록한다. 오류 줄에 절대 경로가 섞이면 G2 R3가 잡는다(스펙 4.10).
// T-004 분류(classification.md 4.10, 승인 결정 12): 정상 표본만 등록한다. 오류 표본은 분류할 것이 없다.
// 분류 어댑터는 CaseParser.ParseDirectory → StepClassifier.Classify → ClassificationJson.Render 결과를
// 출력 폴더의 classification.json 한 파일로 쓴다.
// T-005 합성 생성기(synthetic-examples.md 4.11, 승인 결정 11): 정상 표본 case.yaml의 inputs.key·compare를 스키마로 쓴다.
// 어댑터는 CaseParser.ParseDirectory → SyntheticGenerator.Generate(아래 고정 계획) → SyntheticCsv.Render 결과를
// 출력 폴더의 reference/rows.csv, candidate/rows.csv로 쓴다.
// T-006 대조(metrics.md 4.10, 승인 질문 18): 정상 표본 case-valid와 오류 표본 compare-invalid를 등록한다.
// case-valid에는 reference/rows.csv·candidate/rows.csv(아래 고정 계획의 생성 결과)가 있다.
// 어댑터는 CaseParser.ParseDirectory → CompareEngine.CompareDirectory 결과를 쓴다. 오류가 없으면 metrics.json,
// 있으면 오류 줄(LF, 끝 LF)을 compare-errors.txt 한 파일로. 오류 줄의 Source에 절대 경로가 섞이면 G2 R3가 잡는다.
// T-011 판정(judgment.md 4.12, 잠정 결정 11): 정상 표본 case-valid를 등록한다(새 샘플 없음). 판정 오류 표본은 없다.
// 어댑터는 CaseParser.ParseDirectory → CompareEngine.CompareDirectory → JudgmentEngine.Judge(시간·사람 입력 null) →
// JudgmentJson.Render 결과를 출력 폴더의 judgment.json 한 파일로 쓴다. 문장에 숫자가 들어가므로 R4(문화권)가 본다.
// T-014 문장 결과 검증(text-check.md 4.6): 정상 표본 text-check-valid(= 예제 reply-drafts 파일, 새 샘플)를 등록한다.
// 어댑터는 CaseParser.ParseDirectory → CompareEngine.CompareDirectory → JudgmentEngine.Judge(시간·사람 입력 null) 결과를
// 출력 폴더의 metrics.json, judgment.json 두 파일로 쓴다. 한글 문장·평균 글이 들어가므로 R4(문화권)가 본다.
// 새 Core 처리 기능을 만들면 이 목록에 더한다.
internal static class GuardTargets
{
    public const string ResultFile = "parse-result.json";

    public static readonly GuardTarget[] All =
    [
        new("case-parser-valid", "case-valid", ParseCaseToJson),
        new("case-parser-invalid", "case-invalid", ParseCaseToJson),
        new("classifier-case-valid", "case-valid", ClassifyCaseToJson),
        new("synthetic-case-valid", "case-valid", GenerateSyntheticCsv),
        new("compare-case-valid", "case-valid", CompareToFile),
        new("compare-invalid", "compare-invalid", CompareToFile),
        new("judge-case-valid", "case-valid", JudgeToFile),
        new("text-check-case-valid", "text-check-valid", TextCheckToFiles),
    ];

    // T-006 대조 어댑터의 오류 출력 파일(metrics.md 4.10).
    public const string CompareErrorsFile = "compare-errors.txt";

    // 4.11 고정 계획: 시드 7, rows.csv, 키 r 2자리, 12행. 열 start 정수 0~999, mean 소수 10.00~99.99, label 선택 a·b·c.
    // 값 불일치 start·mean·label, 누락 1, 추가 1, 허용오차 안 차이 mean 2, 몰림 없음, 섞기.
    // case-valid의 inputs.key(row_id)와 compare(start 0, mean 1.0%, label)에 맞췄다. 세 열 종류와 심기 다섯 가지를 모두 쓴다.
    public static readonly SyntheticPlan SyntheticGuardPlan = new(
        Seed: 7,
        FileName: "rows.csv",
        KeyPrefix: "r",
        KeyDigits: 2,
        Rows: 12,
        Columns: [SyntheticColumn.Integer("start", 0, 999), SyntheticColumn.Decimal("mean", 10m, 99.99m, 2), SyntheticColumn.Choice("label", "a", "b", "c")],
        MismatchColumns: ["start", "mean", "label"],
        Missing: 1,
        Extra: 1,
        WithinColumn: "mean",
        Within: 2,
        FocusColumn: null,
        FocusValue: null,
        ShuffleCandidate: true);

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

    // 정상 표본만 쓴다. 파싱 오류면 생성할 것이 없으므로 예외를 던진다(G2·G3가 차이로 적는다).
    private static void GenerateSyntheticCsv(string input, string output)
    {
        var definition = CaseParser.ParseDirectory(input).Case ?? throw new InvalidOperationException("생성 대상 표본이 파싱되지 않는다.");
        var tables = SyntheticGenerator.Generate(definition, SyntheticGuardPlan);
        foreach (var (folder, rows) in new[] { (SyntheticCsv.ReferenceFolder, tables.Reference), (SyntheticCsv.CandidateFolder, tables.Candidate) })
        {
            Directory.CreateDirectory(Path.Combine(output, folder));
            File.WriteAllBytes(Path.Combine(output, folder, tables.FileName), SyntheticCsv.Render(tables.Header, rows));
        }
    }

    // 정상 case.yaml만 쓴다. 파싱 오류면 대조할 것이 없으므로 예외를 던진다(G2·G3가 차이로 적는다).
    // 대조 오류는 예외가 아니다. 오류 줄을 compare-errors.txt로 쓴다.
    private static void CompareToFile(string input, string output)
    {
        var definition = CaseParser.ParseDirectory(input).Case ?? throw new InvalidOperationException("대조 대상 표본이 파싱되지 않는다.");
        var result = CompareEngine.CompareDirectory(input, definition);
        Directory.CreateDirectory(output);
        if (result.Metrics is { } metrics)
        {
            File.WriteAllBytes(Path.Combine(output, MetricsJson.FileName), MetricsJson.Render(metrics));
        }
        else
        {
            File.WriteAllBytes(Path.Combine(output, CompareErrorsFile), ErrorLines(result.Errors));
        }
    }

    // 정상 표본만 쓴다. 파싱·대조 오류면 판정할 것이 없으므로 예외를 던진다(G2·G3가 차이로 적는다, judgment.md 5절).
    private static void JudgeToFile(string input, string output)
    {
        var definition = CaseParser.ParseDirectory(input).Case ?? throw new InvalidOperationException("판정 대상 표본이 파싱되지 않는다.");
        var metrics = CompareEngine.CompareDirectory(input, definition).Metrics ?? throw new InvalidOperationException("판정 대상 표본이 대조되지 않는다.");
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, JudgmentJson.FileName), JudgmentJson.Render(JudgmentEngine.Judge(definition, metrics)));
    }

    // T-014(text-check.md 4.6). 정상 표본만 쓴다. 파싱·대조 오류면 판정할 것이 없으므로 예외를 던진다(G2·G3가 차이로 적는다).
    private static void TextCheckToFiles(string input, string output)
    {
        var definition = CaseParser.ParseDirectory(input).Case ?? throw new InvalidOperationException("문장 결과 표본이 파싱되지 않는다.");
        var metrics = CompareEngine.CompareDirectory(input, definition).Metrics ?? throw new InvalidOperationException("문장 결과 표본이 대조되지 않는다.");
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, MetricsJson.FileName), MetricsJson.Render(metrics));
        File.WriteAllBytes(Path.Combine(output, JudgmentJson.FileName), JudgmentJson.Render(JudgmentEngine.Judge(definition, metrics)));
    }

    // 오류 줄(CaseError.ToString) + LF. UTF-8(BOM 없음).
    public static byte[] ErrorLines(IEnumerable<CaseError> errors) =>
        new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(string.Concat(errors.Select(e => e + "\n")));
}
