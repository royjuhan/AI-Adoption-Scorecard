namespace Scorecard.Core.Cases;

// 스파이크 후보 모델. 이름과 모양은 스펙 제안(docs/spec/case-schema.md)을 따른다.
// 열거 값은 YAML 문자열 그대로 둔다(서수 비교).
public sealed record CaseDefinition(
    string Name,
    string Task,
    string? Before,
    string? After,
    string? AiUsage,
    IReadOnlyList<CaseStep> Steps,
    CaseInputs? Inputs,
    IReadOnlyList<CompareField> Compare,
    CaseThresholds Thresholds);

// Number는 1부터. 오류 경로 steps[n]의 n과 같다.
public sealed record CaseStep(int Number, string Name, StepAnswers Answers);

public sealed record StepAnswers(
    Answer<string> InputKind,
    Answer<string> RuleExpressible,
    Answer<string> Frequency,
    Answer<int> BatchSize,
    Answer<decimal> ExceptionRate,
    Answer<string> CurrentActor,
    Answer<decimal?> MinutesPerItem,
    Answer<string> FailureImpact,
    Answer<string> ExistingFeature);

// IsDefault: 답이 없어서(키 없음·null) 기본값을 넣었다. "기본값 사용, 확인 필요" 표시 근거.
public sealed record Answer<T>(T Value, bool IsDefault);

public sealed record CaseInputs(string Reference, string Candidate, string Key);

public sealed record CompareField(string Field, decimal? Tolerance, decimal? TolerancePct);

// 없으면 null(미입력). 기본값은 판정 카드에서 정한다(스펙 승인 질문).
public sealed record CaseThresholds(decimal? AccuracyMin, decimal? MissingMax, bool? TimeMustDrop);

// Line·Column은 1부터. 0이면 위치 없음.
public sealed record CaseError(string Source, int Line, int Column, string Code, string Path, string Message)
{
    public override string ToString()
    {
        var position = Line == 0 ? Source : Column == 0 ? $"{Source}:{Line}" : $"{Source}:{Line}:{Column}";
        var path = Path.Length == 0 ? string.Empty : " " + Path;
        return $"{position}: {Code}{path}: {Message}";
    }
}

public sealed record CaseParseResult(CaseDefinition? Case, IReadOnlyList<CaseError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

internal sealed class ErrorSink(string source) : List<CaseError>
{
    public string Source { get; } = source;

    public void Add(int line, int column, string code, string path, string message) =>
        Add(new CaseError(Source, line, column, code, path, message));

    // 위치 순(줄, 열). 같은 위치는 찾은 순서(안정 정렬).
    public IReadOnlyList<CaseError> Sorted() =>
        this.OrderBy(e => e.Line).ThenBy(e => e.Column).ToArray();
}
