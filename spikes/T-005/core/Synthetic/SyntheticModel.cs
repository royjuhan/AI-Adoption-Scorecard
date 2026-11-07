namespace Scorecard.Core.Synthetic;

// 합성 기준 자료(reference)와 결과 자료(candidate)를 만드는 계획.
// 키 열 이름과 비교 필드·허용 오차는 case.yaml(inputs.key, compare)에서 온다. 계획은 값의 범위와 심을 차이만 정한다.
// Seed: SplitMix64 시드. FileName: reference/·candidate/ 아래 CSV 이름.
// KeyPrefix·KeyDigits: 키 모양(예: log + 2자리 → log01). Rows: 기준 건수.
// Columns: 키 뒤의 열(순서대로). MismatchColumns: 값 불일치를 심을 열(하나에 한 행).
// Missing: 결과에서 뺄 기준 행 수. Extra: 결과에만 있는 행 수(키는 Rows+1부터).
// WithinColumn·Within: 허용 오차 안에서만 다르게 할 열과 행 수.
// FocusColumn·FocusValue: 누락·불일치 행을 이 값을 가진 기준 행에서만 고른다(없으면 null).
// ShuffleCandidate: 결과 행 순서를 섞는다(키로 맞추는지 보려고).
public sealed record SyntheticPlan(
    ulong Seed,
    string FileName,
    string KeyPrefix,
    int KeyDigits,
    int Rows,
    IReadOnlyList<SyntheticColumn> Columns,
    IReadOnlyList<string> MismatchColumns,
    int Missing,
    int Extra,
    string? WithinColumn,
    int Within,
    string? FocusColumn,
    string? FocusValue,
    bool ShuffleCandidate);

// 열 하나. Kind는 "integer"(Min~Max 정수), "decimal"(Min~Max, 소수 Scale자리), "choice"(Choices 중 하나).
public sealed record SyntheticColumn(string Name, string Kind, decimal Min, decimal Max, int Scale, IReadOnlyList<string> Choices)
{
    public const string KindInteger = "integer";
    public const string KindDecimal = "decimal";
    public const string KindChoice = "choice";

    public static SyntheticColumn Integer(string name, int min, int max) => new(name, KindInteger, min, max, 0, []);

    public static SyntheticColumn Decimal(string name, decimal min, decimal max, int scale) => new(name, KindDecimal, min, max, scale, []);

    public static SyntheticColumn Choice(string name, params string[] choices) => new(name, KindChoice, 0, 0, 0, choices);
}

// 생성 결과. Header는 키 열 + Columns 이름. 행 값은 CSV에 쓸 문자열 그대로다(InvariantCulture).
// Reference는 키 순서, Candidate는 계획대로(섞으면 섞인 순서).
public sealed record SyntheticTables(
    string FileName,
    IReadOnlyList<string> Header,
    IReadOnlyList<SyntheticRow> Reference,
    IReadOnlyList<SyntheticRow> Candidate);

// Planted: 심은 것. 기준 행은 "none" 또는 "missing". 결과 행은 "none", "mismatch:<열>", "within:<열>", "extra".
public sealed record SyntheticRow(string Key, IReadOnlyList<string> Values, string Planted);
