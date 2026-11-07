using System.Globalization;
using System.Text;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Synthetic;

namespace Scorecard.Tests.Comparison;

// 대조 테스트 도우미. 메모리에서 케이스·CSV를 만들어 CompareEngine.Compare(바이트)로 대조한다(스펙 8절).
// 파일을 만들지 않는다. 폴더가 필요한 테스트(4.1 패턴)만 임시 폴더를 쓴다.
internal static class CompareInput
{
    public const string ReferencePath = "reference/a.csv";
    public const string CandidatePath = "candidate/a.csv";

    // 단계 하나 + inputs(키 열 id). compare는 아래에 붙인다.
    public const string InputsCase = SyntheticInput.OneStepCase + "inputs: {reference: reference/*.csv, candidate: candidate/*.csv, key: id}\n";

    // 숫자 n(tolerance 0)과 텍스트 t. 4.2 CSV 읽기 확인용.
    public const string NumberAndTextCase = InputsCase + "compare:\n  - {field: n, tolerance: 0}\n  - {field: t}\n";

    // 텍스트 필드 v 하나.
    public const string TextCase = InputsCase + "compare:\n  - {field: v}\n";

    // NumberAndTextCase의 정상 자료. 두 행 모두 일치한다.
    public const string GoodCsv = "id,n,t\nk1,1,a\nk2,2,b\n";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static byte[] Bytes(string text) => Utf8NoBom.GetBytes(text);

    public static string Text(byte[] bytes) => Utf8NoBom.GetString(bytes);

    public static CaseDefinition Case(string yaml) => CaseInput.Valid(yaml);

    // compare 필드를 직접 정한다(파서 범위와 상관없이 값을 넣을 때).
    public static CaseDefinition WithFields(params CompareField[] fields) => Case(InputsCase) with { Compare = fields };

    // 4.5 칸 판정 표의 "허용 오차" 칸 → 필드 v 하나. "tolerance: 0", "tolerance_pct: 1.0", "없음".
    public static CompareField Field(string tolerance, string name = "v")
    {
        if (tolerance == MetricsSpec.NoTolerance)
        {
            return new CompareField(name, null, null);
        }

        var parts = tolerance.Split(": ");
        Assert.True(parts.Length == 2, $"허용 오차 칸 '{tolerance}'은 'tolerance: n'이나 'tolerance_pct: n' 모양이어야 한다.");
        var value = decimal.Parse(parts[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        return parts[0] switch
        {
            "tolerance" => new CompareField(name, value, null),
            "tolerance_pct" => new CompareField(name, null, value),
            _ => throw new InvalidOperationException($"허용 오차 칸 '{tolerance}'의 키를 모른다."),
        };
    }

    // RFC 4180 따옴표 칸. 안의 '"'는 '""'.
    public static string Quote(string cell) => "\"" + cell.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    public static SourceFile Source(string path, string text) => new(path, Bytes(text));

    public static CompareResult Run(CaseDefinition definition, string reference, string candidate) =>
        CompareEngine.Compare(definition, new CompareSources([Source(ReferencePath, reference)], [Source(CandidatePath, candidate)]));

    public static CompareResult Run(CaseDefinition definition, byte[] reference, byte[] candidate) =>
        CompareEngine.Compare(definition, new CompareSources([new SourceFile(ReferencePath, reference)], [new SourceFile(CandidatePath, candidate)]));

    public static CompareResult Run(CaseDefinition definition, IEnumerable<SourceFile> reference, IEnumerable<SourceFile> candidate) =>
        CompareEngine.Compare(definition, new CompareSources(reference.ToArray(), candidate.ToArray()));

    // 오류 줄(ToString) 목록.
    public static string[] Lines(IEnumerable<CaseError> errors) => errors.Select(e => e.ToString()).ToArray();

    // 오류가 없어야 한다. 있으면 오류 줄을 보이고 실패한다. 오류가 없으면 지표가 있다(3.1).
    public static CompareMetrics Measured(CompareResult result)
    {
        if (result.Errors.Count > 0)
        {
            Assert.Fail("오류가 없어야 한다:\n  " + string.Join("\n  ", Lines(result.Errors)));
        }

        Assert.True(result.IsValid);
        Assert.NotNull(result.Metrics);
        return result.Metrics;
    }

    // 오류 줄이 expected와 순서까지 같아야 한다. 오류가 있으면 지표는 없다(3.1).
    public static void AssertErrors(CompareResult result, params string[] expected)
    {
        Assert.Equal(expected, Lines(result.Errors));
        Assert.False(result.IsValid);
        Assert.Null(result.Metrics);
    }

    // 셈 일곱 개와 정확도·누락률(InvariantCulture). 미검증이면 null.
    public static string Counts(CompareMetrics metrics) =>
        metrics.Counts is { } c
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"ref={c.Reference} cand={c.Candidate} match={c.Match} mismatch={c.Mismatch} missing={c.Missing} extra={c.Extra} within={c.Within} acc={metrics.AccuracyPct} miss={metrics.MissingRatePct}")
            : "null";

    public static string Counts(CompareResult result) => Counts(Measured(result));

    // 칸 하나의 판정(4.5 칸 판정 표의 "판정" 칸 모양): "같음", within, outside, not_number.
    // 한 행짜리 CSV 두 개를 공개 API로 대조한다. 칸은 따옴표 칸으로 넣어 앞뒤 글자를 그대로 둔다.
    public static string Judge(CompareField field, string reference, string candidate)
    {
        var result = Run(WithFields(field), $"id,{field.Field}\nk,{Quote(reference)}\n", $"id,{field.Field}\nk,{Quote(candidate)}\n");
        if (result.Metrics is null)
        {
            return "오류: " + string.Join(" | ", Lines(result.Errors));
        }

        var item = Assert.Single(result.Metrics.Items);
        return item.Differences.Count == 0 ? MetricsSpec.Same : Assert.Single(item.Differences).Result;
    }

    public static string Judge(string tolerance, string reference, string candidate) => Judge(Field(tolerance), reference, candidate);

    // 공개 예제를 케이스 폴더에서 대조한다(골든과 같은 길).
    public static CompareResult Example(string example) =>
        CompareEngine.CompareDirectory(SyntheticInput.ExampleDirectory(example), SyntheticInput.CaseOf(example));

    public static byte[] ExampleBytes(string example) => MetricsJson.Render(Measured(Example(example)));

    // 공개 예제의 입력 파일 바이트(4.1 Read). inputs가 없는 예제에는 쓰지 않는다.
    public static CompareSources ExampleSources(string example)
    {
        var definition = SyntheticInput.CaseOf(example);
        var read = CompareInputs.Read(SyntheticInput.ExampleDirectory(example), definition.Inputs!);
        Assert.True(read.IsValid, string.Join("\n", Lines(read.Errors)));
        return read.Sources!;
    }

    // 예제 expected/metrics.json(커밋된 바이트). 없으면 실패한다.
    public static byte[] ExpectedBytes(string example) => SyntheticInput.ExampleBytes(example, "expected/" + MetricsJson.FileName);

    // 문화권을 바꿔 돌리고 되돌린다.
    public static T WithCulture<T>(string name, Func<T> run)
    {
        var (culture, ui) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
            return run();
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = ui;
        }
    }
}
