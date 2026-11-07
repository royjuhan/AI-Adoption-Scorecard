using System.Globalization;
using System.Text.RegularExpressions;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Judgment;

// 채택된 판정 스펙(docs/spec/judgment.md)의 표·블록을 읽는다. Core를 쓰지 않는다.
// 스펙 4.13: Core는 기본값·결과 문자열·문장 틀·문구·focus_min을 internal 상수로 두고, 테스트가 이 표와 대조한다.
// 표 모양(머리 행)은 스펙이 "바꾸지 않는다"고 정했다. 머리 행이 문서에 정확히 한 번 나와야 한다(SpecDocument.Table).
internal static partial class JudgmentSpec
{
    public const string Document = SpecDocument.JudgmentPath;

    // 3.2. metrics.md와 같은 글자다. 문서마다 한 번이다.
    public const string PropertyHeader = "| 속성 | 형식 | 뜻 |";

    // 4.2
    public const string DefaultHeader = "| 키 | 기본값 | 근거 |";

    // 4.3
    public const string VerdictHeader = "| 순서 | 근거 키 | 걸리는 결과 | 판정 | 판정 문장 틀 |";

    // 4.4
    public const string CheckHeader = "| 확인 | 결과 | 조건 | 확인 문장 틀 |";

    // 4.5
    public const string NumberHeader = "| 이름 | 값 | 뜻 |";

    // 4.6
    public const string ReadThresholdHeader = "| 근거 키 | 읽은 문턱 |";

    // 4.7
    public const string TodoHeader = "| 다음 할 일 | 언제 | 문구 |";

    // 4.12. metrics.md·classification.md와 같은 글자다. 문서마다 한 번이다.
    public const string GuardHeader = "| 이름 | 샘플(`TestData/guard/…`) | 어댑터 |";

    // 6.1
    public const string BoundaryHeader =
        "| 기준 행 | 일치 | 누락 | 표시 정확도(%) | accuracy_min | ACCURACY | 표시 누락률(%) | missing_max | MISSING |";

    // 6.2
    public const string TimeHeader = "| time_must_drop | 적용 전 | 처리 | 검토 | TIME |";

    // 6.3
    public const string FocusHeader = "| 기준 행 유형 | 불일치 행 유형 | 추가 행 유형 | 유형 열 | FOCUS |";
    public const string FocusColumnsHeader = "| 열 값(행 1~6) | 불일치 행 | FOCUS | 걸린 열 | 본 열 |";

    // 6.4
    public const string UnexplainedHeader = "| 불일치 | 사람 입력 | UNEXPLAINED |";

    // 6.5
    public const string PriorityHeader = "| ACCURACY | MISSING | UNEXPLAINED | TIME | FOCUS | 판정 | 근거 키 |";

    // 7.1
    public const string ExampleHeader = "| 예제 | 판정 | 근거 키 | 확인 다섯 | 다음 할 일 | 12절 |";

    // 3.2 모양 블록, 4.13 API 블록의 제목.
    public const string ShapeHeading = "### 3.2 judgment.json";
    public const string ApiHeading = "### 4.13 Core 공개 API (제안)";

    // 0절 판정 값 줄. "- 판정 값: `pass`(통과), …"
    public const string VerdictNamesLinePrefix = "- 판정 값:";

    // 4.6. 공백 하나 + "(기본값 사용, 확인 필요)". classification.md 4.6과 같은 글자다.
    public const string DefaultMark = " (기본값 사용, 확인 필요)";

    // 표의 "값 없음"(—), 4.5 빈 값 표시, 4.2 근거 키 PASS의 결과 칸.
    public const string None = "—";
    public const string EmptyValue = "(빈 칸)";

    // 0절 판정 값.
    public const string Pass = "pass";
    public const string Conditional = "conditional";
    public const string Hold = "hold";
    public const string NotVerified = "not_verified";

    // 4.3·4.4 근거 키(확인 이름).
    public const string Accuracy = "ACCURACY";
    public const string Missing = "MISSING";
    public const string Unexplained = "UNEXPLAINED";
    public const string Time = "TIME";
    public const string Focus = "FOCUS";
    public const string PassRule = "PASS";
    public const string NotVerifiedRule = "NOT_VERIFIED";

    // 4.2 표의 키.
    public const string AccuracyMin = "accuracy_min";
    public const string MissingMax = "missing_max";
    public const string TimeMustDrop = "time_must_drop";

    // 7.2~7.5 제목.
    public static readonly IReadOnlyDictionary<string, string> ExampleHeadings = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["table-compare"] = "### 7.2 table-compare",
        ["form-fill"] = "### 7.3 form-fill",
        ["mixed-exceptions"] = "### 7.4 mixed-exceptions",
        ["signoff-final"] = "### 7.5 signoff-final",
    };

    // 4.6 확인 문장: 그 확인이 쓰는 문턱. UNEXPLAINED·FOCUS는 문턱을 쓰지 않는다(4.6 둘째 글머리).
    public static readonly IReadOnlyDictionary<string, string> CheckThreshold = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Accuracy] = AccuracyMin,
        [Missing] = MissingMax,
        [Time] = TimeMustDrop,
    };

    public static IReadOnlyList<string[]> Table(string header) => SpecDocument.Table(Document, header);

    // 표 칸의 값. 앞뒤 공백을 떼고, 칸 전체를 감싼 `코드` 표시를 뗀다.
    public static string Cell(string cell)
    {
        var trimmed = cell.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '`' && trimmed[^1] == '`' ? trimmed[1..^1] : trimmed;
    }

    // 칸 안의 `코드` 목록. "—"·"(없음)"이면 빈 목록.
    public static string[] List(string cell) =>
        cell.Trim() is None or "(없음)" ? [] : SpecDocument.CodeSpans(cell);

    public static int Int(string cell) => int.Parse(Cell(cell), NumberStyles.None, CultureInfo.InvariantCulture);

    public static decimal Decimal(string cell) =>
        decimal.Parse(Cell(cell), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

    // 4.2 표: (키, 기본값 글). 표 순서가 defaults 순서다.
    public static IReadOnlyList<(string Key, string Value)> Defaults() =>
        Table(DefaultHeader).Select(c => (Cell(c[0]), Cell(c[1]))).ToArray();

    public static string DefaultOf(string key) => Assert.Single(Defaults(), d => d.Key == key).Value;

    // 4.3 표. 결과 칸이 —이면 Result는 null(PASS).
    public static IReadOnlyList<VerdictRow> VerdictRows() =>
        Table(VerdictHeader)
            .Select(c => new VerdictRow(Int(c[0]), Cell(c[1]), Cell(c[2]) == None ? null : Cell(c[2]), Cell(c[3]), Cell(c[4])))
            .ToArray();

    public static VerdictRow FindVerdict(string rule, string? result) =>
        Assert.Single(VerdictRows(), r => r.Rule == rule && r.Result == result);

    // 4.4 표.
    public static IReadOnlyList<CheckRow> CheckRows() =>
        Table(CheckHeader).Select(c => new CheckRow(Cell(c[0]), Cell(c[1]), c[2].Trim(), Cell(c[3]))).ToArray();

    public static CheckRow FindCheck(string check, string result) =>
        Assert.Single(CheckRows(), r => r.Check == check && r.Result == result);

    // 4.4 확인 순서: 표에서 처음 나오는 순서.
    public static string[] CheckOrder() => CheckRows().Select(r => r.Check).Distinct().ToArray();

    // 4.5 숫자 표의 focus_min.
    public static int FocusMin() => Int(Assert.Single(Table(NumberHeader), c => Cell(c[0]) == "focus_min")[1]);

    // 4.6 읽은 문턱 표. —이면 빈 목록.
    public static IReadOnlyDictionary<string, string[]> ReadThresholds() =>
        Table(ReadThresholdHeader).ToDictionary(c => Cell(c[0]), c => List(c[1]), StringComparer.Ordinal);

    // 4.7 표: (키, 언제, 문구 틀). 표 순서가 todo 순서다.
    public static IReadOnlyList<(string Key, string When, string Template)> TodoRows() =>
        Table(TodoHeader).Select(c => (Cell(c[0]), c[1].Trim(), Cell(c[2]))).ToArray();

    public static string TodoTemplate(string key) => Assert.Single(TodoRows(), r => r.Key == key).Template;

    public static string[] TodoOrder() => TodoRows().Select(r => r.Key).ToArray();

    // 0절 "판정 값: `pass`(통과), `conditional`(조건부), …" → 한글 이름 → 판정 값.
    public static IReadOnlyDictionary<string, string> VerdictNames()
    {
        var line = SpecDocument.Line(Document, VerdictNamesLinePrefix);
        var names = VerdictName().Matches(line).ToDictionary(m => m.Groups["name"].Value, m => m.Groups["value"].Value, StringComparer.Ordinal);
        Assert.Equal(4, names.Count);
        return names;
    }

    // 3.2 모양 블록(코드 블록 하나).
    public static string ShapeBlock() => Assert.Single(SpecDocument.CodeBlocks(Document, ShapeHeading));

    // 7.2~7.5의 json 블록 하나. 줄은 LF로 잇고 끝에 LF 하나(SpecDocument.CodeBlocks).
    public static string ExampleBlock(string example) => Assert.Single(SpecDocument.CodeBlocks(Document, ExampleHeadings[example]));

    // 문장 틀의 {이름}을 값으로 바꾼다. 틀에 있는데 값이 없으면 실패한다(테스트가 틀을 잘못 읽었다는 뜻).
    public static string Fill(string template, IReadOnlyDictionary<string, string> slots) =>
        Slot().Replace(template, m =>
        {
            var name = m.Groups["name"].Value;
            if (!slots.TryGetValue(name, out var value))
            {
                Assert.Fail($"문장 틀 '{template}'의 {{{name}}} 값이 장면에 없다.");
            }

            return value!;
        });

    // 틀에 쓰인 {이름} 목록.
    public static string[] SlotNames(string template) => Slot().Matches(template).Select(m => m.Groups["name"].Value).ToArray();

    [GeneratedRegex(@"\{(?<name>[a-z_]+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex Slot();

    [GeneratedRegex(@"`(?<value>[a-z_]+)`\((?<name>[^()]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex VerdictName();
}

// 4.3 판정 표의 한 행.
internal sealed record VerdictRow(int Order, string Rule, string? Result, string Verdict, string Template);

// 4.4 확인 표의 한 행. Condition은 칸 그대로다.
internal sealed record CheckRow(string Check, string Result, string Condition, string Template);
