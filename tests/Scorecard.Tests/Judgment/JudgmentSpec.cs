using System.Globalization;
using System.Text.RegularExpressions;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Judgment;

// 채택된 판정 스펙(docs/spec/judgment.md)의 표·블록을 읽는다. Core를 쓰지 않는다.
// 스펙 4.13: Core는 기본값·결과 문자열·문장 틀·문구·focus_min을 internal 상수로 두고, 테스트가 이 표와 대조한다.
// 표 모양(머리 행)은 스펙이 "바꾸지 않는다"고 정했다. 머리 행이 문서에 정확히 한 번 나와야 한다(SpecDocument.Table).
// T-012(PR (b))는 판정·확인·읽은 문턱·다음 할 일·몰림·설명 안 됨·우선순위·예제 표를 11절에서 읽었다.
// T-013이 표를 4·6·7절 자리로 옮겼다(11.3 옮긴 곳 표). 머리 행은 그대로다. 아래 주석의 절 번호는 지금 자리다.
internal static partial class JudgmentSpec
{
    public const string Document = SpecDocument.JudgmentPath;

    // 3.2. metrics.md와 같은 글자다. 문서마다 한 번이다.
    public const string PropertyHeader = "| 속성 | 형식 | 뜻 |";

    // 4.2
    public const string DefaultHeader = "| 키 | 기본값 | 근거 |";

    // 4.3
    public const string VerdictHeader = "| 순서 | 근거 키 | 확인 결과 | 판정 | 판정 문장 틀 |";

    // 4.4
    public const string CheckHeader = "| 확인 | 결과 | 걸리는 조건 | 확인 문장 틀 |";

    // 4.5
    public const string NumberHeader = "| 이름 | 값 | 뜻 |";

    // 4.6. 근거 키 대신 판정 표의 순서로 적는다(UNEXPLAINED가 순서 4·7 두 곳).
    public const string ReadThresholdHeader = "| 순서 | 근거 키 | 읽은 문턱 |";

    // 4.7
    public const string TodoHeader = "| 다음 할 일 | 나오는 때 | 문구 |";

    // 4.9 검토 항목. 표 순서가 {breakdown} 순서다.
    public const string ReviewItemHeader = "| 검토 항목 | 항목 상태 | {breakdown} 이름 |";

    // 5절(T-013) 정의와 대조 결과. "판정"은 예외 없이 판정, "거절"은 ArgumentException.
    public const string DefinitionHeader = "| 정의 | 대조 결과 | Judge |";

    // 4.12. metrics.md·classification.md와 같은 글자다. 문서마다 한 번이다.
    public const string GuardHeader = "| 이름 | 샘플(`TestData/guard/…`) | 어댑터 |";

    // 6.1
    public const string BoundaryHeader =
        "| 기준 행 | 일치 | 누락 | 표시 정확도(%) | accuracy_min | ACCURACY | 표시 누락률(%) | missing_max | MISSING |";

    // 6.2
    public const string TimeHeader = "| time_must_drop | 적용 전 | 처리 | 검토 | TIME |";

    // 6.3(M-A) 몰림 두 표
    public const string FocusHeader = "| 기준 행 유형 | 불일치 행 유형 | 추가 행 유형 | inputs.group | FOCUS |";
    public const string FocusColumnsHeader = "| 열 값(행 1~6) | inputs.group | 불일치 행 | FOCUS | 걸린 값 |";

    // 6.4
    public const string UnexplainedHeader = "| 불일치 | 추가 행 | 중복 행 | 사람 입력 | UNEXPLAINED |";

    // 6.5
    public const string PriorityHeader = "| ACCURACY | MISSING | UNEXPLAINED | TIME | FOCUS | 판정 | 근거 키 | 다음 할 일 |";

    // 6.6 탐침 재현.
    public const string ProbeHeader = "| 탐침 | 장면 | 사람 입력 | T-011 | T-012 |";

    // 7.1 예제 case.yaml에 더하는 줄, 예제 표, expected 영향.
    public const string ExampleCaseHeader = "| 예제 | 더하는 줄 | 지금 SHA-256 | 더한 뒤 SHA-256 | 바이트 |";
    public const string ExampleHeader = "| 예제 | inputs.group | 판정 | 근거 키 | 확인 다섯 | 다음 할 일 | 검토 항목 |";
    public const string ExpectedImpactHeader = "| 파일 | 지금 SHA-256 | 새 SHA-256 | 바뀐 줄 |";

    // 3.2 모양 블록, 4.13 API 블록의 제목.
    public const string ShapeHeading = "### 3.2 judgment.json";
    public const string ApiHeading = "### 4.13 Core 공개 API (제안)";

    // 6.4 compare-duplicate 판정 블록의 제목과 SHA-256 줄 머리. T-013에서 블록이 11.9에서 6.4로 옮겼다.
    public const string ReviewHeading = "### 6.4 설명 안 되는 검토 항목";
    public const string CompareDuplicateLinePrefix = "`compare-duplicate` judgment.json (schema_version 2, SHA-256 ";

    // 11.2: judgment.json schema_version(T-012에서 1 → 2).
    public const int SchemaVersion = 2;

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

    // 11.4 표. 결과 칸이 —이면 Result는 null(PASS).
    public static IReadOnlyList<VerdictRow> VerdictRows() =>
        Table(VerdictHeader)
            .Select(c => new VerdictRow(Int(c[0]), Cell(c[1]), Cell(c[2]) == None ? null : Cell(c[2]), Cell(c[3]), Cell(c[4])))
            .ToArray();

    public static VerdictRow FindVerdict(string rule, string? result) =>
        Assert.Single(VerdictRows(), r => r.Rule == rule && r.Result == result);

    // 11.5 표.
    public static IReadOnlyList<CheckRow> CheckRows() =>
        Table(CheckHeader).Select(c => new CheckRow(Cell(c[0]), Cell(c[1]), c[2].Trim(), Cell(c[3]))).ToArray();

    public static CheckRow FindCheck(string check, string result) =>
        Assert.Single(CheckRows(), r => r.Check == check && r.Result == result);

    // 11.5 확인 순서: 표에서 처음 나오는 순서(4.4와 같다).
    public static string[] CheckOrder() => CheckRows().Select(r => r.Check).Distinct().ToArray();

    // 4.5 숫자 표의 focus_min.
    public static int FocusMin() => Int(Assert.Single(Table(NumberHeader), c => Cell(c[0]) == "focus_min")[1]);

    // 11.6 읽은 문턱 표: 판정 표의 순서마다 (순서, 근거 키, 읽은 문턱). —이면 빈 목록.
    public static IReadOnlyList<(int Order, string Rule, string[] Read)> ReadThresholdRows() =>
        Table(ReadThresholdHeader).Select(c => (Int(c[0]), Cell(c[1]), List(c[2]))).ToArray();

    public static string[] ReadThresholds(int order) => Assert.Single(ReadThresholdRows(), r => r.Order == order).Read;

    // 근거 키가 처음 나오는 순서(UNEXPLAINED는 4). 근거 키 하나로 장면을 고르는 테스트가 쓴다.
    public static int FirstOrder(string rule) => ReadThresholdRows().First(r => r.Rule == rule).Order;

    // 11.3 표: (검토 항목, 항목 상태들, {breakdown} 이름). 표 순서.
    public static IReadOnlyList<(string Item, string[] Statuses, string Name)> ReviewItems() =>
        Table(ReviewItemHeader).Select(c => (c[0].Trim(), SpecDocument.CodeSpans(c[1]), c[2].Trim())).ToArray();

    // 11.3 {breakdown}: 이름마다 수를 세어 0이 아닌 것만 "이름 N건"으로 쓰고 ", "로 잇는다. 수는 11.3 표 순서다.
    public static string Breakdown(params int[] counts)
    {
        var names = ReviewItems().Select(r => r.Name).ToArray();
        Assert.Equal(names.Length, counts.Length);
        return string.Join(", ", names.Zip(counts).Where(p => p.Second > 0).Select(p => $"{p.First} {p.Second.ToString(CultureInfo.InvariantCulture)}건"));
    }

    // 11.7 표: (키, 나오는 때, 문구 틀). 표 순서가 todo 순서다.
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

// 11.4 판정 표의 한 행.
internal sealed record VerdictRow(int Order, string Rule, string? Result, string Verdict, string Template);

// 11.5 확인 표의 한 행. Condition은 칸 그대로다.
internal sealed record CheckRow(string Check, string Result, string Condition, string Template);
