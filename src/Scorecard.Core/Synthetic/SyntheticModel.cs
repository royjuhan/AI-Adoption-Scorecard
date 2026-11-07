namespace Scorecard.Core.Synthetic;

// 합성 기준 자료(reference)·결과 자료(candidate)를 만드는 계획. docs/spec/synthetic-examples.md 4.3·4.13.
// 키 열 이름과 비교 필드·허용 오차는 case.yaml(inputs.key, compare)에서 온다. 계획은 값의 범위와 심을 차이만 정한다.
// 공개 예제의 계획은 스펙 4.3·4.4 표에 있다. Core에는 예제 계획이 없다.
// Seed: SplitMix64 시드. FileName: reference/·candidate/ 아래 CSV 이름(폴더 없이 .csv).
// KeyPrefix·KeyDigits: 키 = 접두어 + 행 번호(1부터, 앞을 0으로 채움). 예: log + 2자리 → log01.
// Rows: 기준 건수. Columns: 키 열 다음의 열(적힌 순서가 CSV 열 순서).
// MismatchColumns: 값 불일치를 심을 열. 열마다 한 행, 적힌 순서대로.
// Missing: 결과에서 뺄 기준 행 수. Extra: 결과에만 있는 행 수(키는 Rows+1부터).
// WithinColumn·Within: 허용 오차 안에서만 바꿀 열과 행 수. 없으면 null·0.
// FocusColumn·FocusValue: 누락·값 불일치 행을 이 값을 가진 기준 행에서만 고른다. 없으면 null.
// ShuffleCandidate: 결과 행 순서를 섞는다.
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

// 열 하나(스펙 4.4 열 표). Kind에 따라 쓰는 칸이 다르다.
// integer: Min~Max 정수(양 끝 포함). decimal: Min~Max, 소수 Scale자리. choice: Choices 중 하나(적힌 순서가 뽑는 순서).
public sealed record SyntheticColumn(string Name, string Kind, decimal Min, decimal Max, int Scale, IReadOnlyList<string> Choices)
{
    public const string KindInteger = "integer";
    public const string KindDecimal = "decimal";
    public const string KindChoice = "choice";

    public static SyntheticColumn Integer(string name, int min, int max) => new(name, KindInteger, min, max, 0, []);

    public static SyntheticColumn Decimal(string name, decimal min, decimal max, int scale) => new(name, KindDecimal, min, max, scale, []);

    public static SyntheticColumn Choice(string name, params string[] choices) => new(name, KindChoice, 0, 0, 0, choices);
}

// 생성 결과(스펙 3.1). Header는 키 열 + Columns 이름. 행 값은 CSV에 쓸 문자열 그대로다(InvariantCulture).
// Reference는 키 순서. Candidate는 누락을 빼고 추가 행을 붙인 순서(섞으면 섞인 순서).
public sealed record SyntheticTables(
    string FileName,
    IReadOnlyList<string> Header,
    IReadOnlyList<SyntheticRow> Reference,
    IReadOnlyList<SyntheticRow> Candidate);

// 행 하나. Values는 키 열 다음 칸들이다.
// Planted(3.1 표): 기준 행은 none·missing, 결과 행은 none·mismatch:<열>·within:<열>·extra. CSV에는 쓰지 않는다.
public sealed record SyntheticRow(string Key, IReadOnlyList<string> Values, string Planted);
