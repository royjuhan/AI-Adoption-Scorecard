using System.Globalization;
using System.Text.RegularExpressions;
using Scorecard.Core.Synthetic;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Synthetic;

// 승인된 합성 예제 스펙(docs/spec/synthetic-examples.md)의 표를 읽는다.
// 스펙 8절: 4.3 계획, 4.4 열, 4.7 구조, 4.10 패턴, 7.1 파일 표를 파일에서 읽어 대조한다.
// 표 모양(머리 행)은 스펙이 "바꾸지 않는다"고 정했다. 머리 행이 문서에 정확히 한 번 나와야 한다(SpecDocument.Table).
// 계획 만들기(Plan)만 Core 레코드를 쓴다. 칸 읽기 규칙은 스펙 4.3 "칸 읽기"·4.4 아래 목록을 따른다.
internal static partial class SyntheticSpec
{
    public const string Document = SpecDocument.SyntheticExamplesPath;

    // 3.1
    public const string PlantedHeader = "| `Planted` | 어디 | 뜻 |";

    // 4.3
    public const string PlanHeader =
        "| 예제 | 시드 | 파일 | 키 접두어 | 키 자릿수 | 기준 건수 | 값 불일치 열 | 누락 | 추가 | 허용오차 안 차이 | 몰림 | 결과 섞기 |";

    // 4.4
    public const string ColumnHeader = "| 예제 | 열 | 종류 | 값 |";

    // 4.6
    public const string CompareKindHeader = "| `compare` 항목 | 열 종류 |";

    // 4.7
    public const string TermHeader = "| 용어 | 뜻 |";

    public const string StructureHeader =
        "| 예제 | 기준 행 | 결과 행 | 일치 | 값 불일치 | 누락 | 추가 | 허용오차 안 차이 | 정확도(%) | 누락률(%) |";

    // 4.8
    public const string CaseKeysHeader = "| 예제 | `inputs` | `compare` | `thresholds` |";

    // 4.10
    public const string PatternHeader = "| 패턴 | 무엇 | 정규식 | 잡는 예 | 잡지 않는 예 |";

    // 4.11
    public const string GuardHeader = "| 이름 | 샘플(`TestData/guard/…`) | 어댑터 |";

    // 5절
    public const string ErrorHeader = "| 상황 | 결과 | 근거 |";

    // 6.1
    public const string PlantedRowsHeader = "| 키 | `signal_type` | 심은 것 | 기준 → 결과 |";

    // 7.1
    public const string FilesHeader = "| 예제 | 파일 |";

    // 4.4 "종류" 칸 → Core 열 종류.
    public const string KindIntegerLabel = "정수";
    public const string KindDecimalLabel = "소수";
    public const string KindChoiceLabel = "선택";

    // 칸이 비었다는 표시(4.3 "`없음`이면 0개").
    public const string None = "없음";

    // 7.2~7.4 제목. 블록은 7.1 파일 표 순서다.
    public static readonly IReadOnlyDictionary<string, string> ExampleHeadings = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["table-compare"] = "### 7.2 table-compare",
        ["form-fill"] = "### 7.3 form-fill",
        ["mixed-exceptions"] = "### 7.4 mixed-exceptions",
    };

    public static IReadOnlyList<string[]> Table(string header) => SpecDocument.Table(Document, header);

    // 4.4 "종류" 칸 → SyntheticColumn.Kind 값.
    public static string KindOf(string label) => label switch
    {
        KindIntegerLabel => SyntheticColumn.KindInteger,
        KindDecimalLabel => SyntheticColumn.KindDecimal,
        KindChoiceLabel => SyntheticColumn.KindChoice,
        _ => throw new InvalidOperationException($"4.4 열 표의 종류 '{label}'을 모른다."),
    };

    // 4.3 계획 표의 그 예제 행.
    public static string[] PlanRow(string example) =>
        Assert.Single(Table(PlanHeader), c => SpecDocument.Unquote(c[0]) == example);

    // 4.3 계획 표의 예제(표 순서).
    public static string[] PlanExamples() => Table(PlanHeader).Select(c => SpecDocument.Unquote(c[0])).ToArray();

    // 4.4 열 표의 그 예제 열(표 순서 = CSV 열 순서).
    public static SyntheticColumn[] Columns(string example) =>
        Table(ColumnHeader)
            .Where(c => SpecDocument.Unquote(c[0]) == example)
            .Select(c => Column(SpecDocument.Unquote(c[1]), c[2].Trim(), c[3]))
            .ToArray();

    // 4.3·4.4 표 → SyntheticPlan. 칸 모양이 스펙 4.3 "칸 읽기"와 다르면 실패한다.
    public static SyntheticPlan Plan(string example)
    {
        var c = PlanRow(example);
        var seed = SpecDocument.Unquote(c[1]);
        Assert.Matches(Digits(), seed);

        var mismatch = c[6] == None ? [] : SpanList(c[6], "값 불일치 열");

        string? withinColumn = null;
        var within = 0;
        if (c[9] != None)
        {
            var m = WithinCell().Match(c[9]);
            Assert.True(m.Success, $"4.3 '{example}' 허용오차 안 차이 칸 '{c[9]}'은 '`열` n' 모양이어야 한다.");
            (withinColumn, within) = (m.Groups["column"].Value, int.Parse(m.Groups["count"].Value, CultureInfo.InvariantCulture));
        }

        string? focusColumn = null, focusValue = null;
        if (c[10] != None)
        {
            var m = FocusCell().Match(c[10]);
            Assert.True(m.Success, $"4.3 '{example}' 몰림 칸 '{c[10]}'은 '`열` = `값`' 모양이어야 한다.");
            (focusColumn, focusValue) = (m.Groups["column"].Value, m.Groups["value"].Value);
        }

        var shuffle = c[11] switch
        {
            "예" => true,
            "아니오" => false,
            _ => throw new InvalidOperationException($"4.3 '{example}' 결과 섞기 칸 '{c[11]}'은 '예'나 '아니오'여야 한다."),
        };

        return new SyntheticPlan(
            Seed: ulong.Parse(seed, CultureInfo.InvariantCulture),
            FileName: SpecDocument.Unquote(c[2]),
            KeyPrefix: SpecDocument.Unquote(c[3]),
            KeyDigits: Int(c[4]),
            Rows: Int(c[5]),
            Columns: Columns(example),
            MismatchColumns: mismatch,
            Missing: Int(c[7]),
            Extra: Int(c[8]),
            WithinColumn: withinColumn,
            Within: within,
            FocusColumn: focusColumn,
            FocusValue: focusValue,
            ShuffleCandidate: shuffle);
    }

    // 4.7 구조 표의 그 예제 행. 셈 일곱 개와 정확도·누락률 칸(문자열 그대로).
    public static (Tally Counts, string Accuracy, string MissingRate) Structure(string example)
    {
        var c = Assert.Single(Table(StructureHeader), r => SpecDocument.Unquote(r[0]) == example);
        return (new Tally(Int(c[1]), Int(c[2]), Int(c[3]), Int(c[4]), Int(c[5]), Int(c[6]), Int(c[7])), c[8].Trim(), c[9].Trim());
    }

    // 7.1 파일 표. (예제, 파일) 표 순서.
    public static (string Example, string File)[] Files() =>
        Table(FilesHeader).Select(c => (SpecDocument.Unquote(c[0]), SpecDocument.Unquote(c[1]))).ToArray();

    public static string[] FilesOf(string example) => Files().Where(f => f.Example == example).Select(f => f.File).ToArray();

    // "`a`, `b`" → [a, b]. 칸이 정확히 그 모양이 아니면 실패한다(설명 글이 섞이지 않게).
    public static string[] SpanList(string cell, string what)
    {
        var spans = SpecDocument.CodeSpans(cell);
        Assert.True(
            spans.Length > 0 && cell.Trim() == string.Join(", ", spans.Select(s => $"`{s}`")),
            $"{what} 칸 '{cell}'은 '`a`, `b`' 모양이어야 한다.");
        return spans;
    }

    private static SyntheticColumn Column(string name, string kind, string values)
    {
        switch (kind)
        {
            case KindIntegerLabel:
                {
                    var m = IntegerRange().Match(values.Trim());
                    Assert.True(m.Success, $"4.4 '{name}' 정수 값 칸 '{values}'은 '`최소` ~ `최대`' 모양이어야 한다.");
                    return SyntheticColumn.Integer(name, Int(m.Groups["min"].Value), Int(m.Groups["max"].Value));
                }

            case KindDecimalLabel:
                {
                    var m = DecimalRange().Match(values.Trim());
                    Assert.True(m.Success, $"4.4 '{name}' 소수 값 칸 '{values}'은 '`최소` ~ `최대`'(소수점 있음) 모양이어야 한다.");
                    var min = m.Groups["min"].Value;

                    // 소수 자릿수는 최소값의 자릿수다(4.4 "`10.00` → 2").
                    var scale = min.Length - min.IndexOf('.', StringComparison.Ordinal) - 1;
                    return SyntheticColumn.Decimal(
                        name,
                        decimal.Parse(min, CultureInfo.InvariantCulture),
                        decimal.Parse(m.Groups["max"].Value, CultureInfo.InvariantCulture),
                        scale);
                }

            case KindChoiceLabel:
                return SyntheticColumn.Choice(name, SpanList(values, $"4.4 '{name}' 선택"));

            default:
                Assert.Fail($"4.4 '{name}' 종류 '{kind}'을 모른다. 정수·소수·선택 중 하나여야 한다.");
                return null!;
        }
    }

    private static int Int(string cell) => int.Parse(SpecDocument.Unquote(cell), NumberStyles.None, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^[0-9]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex Digits();

    [GeneratedRegex(@"^`(?<column>[^`]+)` (?<count>[0-9]+)\z", RegexOptions.CultureInvariant)]
    private static partial Regex WithinCell();

    [GeneratedRegex(@"^`(?<column>[^`]+)` = `(?<value>[^`]+)`\z", RegexOptions.CultureInvariant)]
    private static partial Regex FocusCell();

    [GeneratedRegex(@"^`(?<min>[0-9]+)` ~ `(?<max>[0-9]+)`\z", RegexOptions.CultureInvariant)]
    private static partial Regex IntegerRange();

    [GeneratedRegex(@"^`(?<min>[0-9]+\.[0-9]+)` ~ `(?<max>[0-9]+\.[0-9]+)`\z", RegexOptions.CultureInvariant)]
    private static partial Regex DecimalRange();
}
