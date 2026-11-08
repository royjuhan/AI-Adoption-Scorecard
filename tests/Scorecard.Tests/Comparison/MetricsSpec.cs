using System.Globalization;
using System.Text.RegularExpressions;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.Comparison;

// 승인된 대조 스펙(docs/spec/metrics.md)의 표·블록을 읽는다. Core를 쓰지 않는다.
// 스펙 4.9(승인 질문 9): Core는 값·문구를 상수로 두고, 테스트가 이 표와 대조한다.
// 표 모양(머리 행)은 스펙이 "바꾸지 않는다"고 정했다. 머리 행이 문서에 정확히 한 번 나와야 한다(SpecDocument.Table).
internal static partial class MetricsSpec
{
    public const string Document = SpecDocument.MetricsPath;

    // 3.2
    public const string PropertyHeader = "| 속성 | 형식 | 뜻 |";

    // 4.1
    public const string PatternHeader = "| 패턴 | 받나 |";
    public const string MatchHeader = "| 패턴 | 파일 이름 | 맞나 |";

    // 4.4
    public const string NotVerifiedHeader = "| 조건 | `status` | `reason` | 파일을 읽나 |";

    // 4.5
    public const string NumberHeader = "| 칸 | 숫자인가 | 값 |";
    public const string ToleranceHeader = "| 허용 오차 | 기준 값 | 결과 값 | 판정 |";

    // 4.6
    public const string TrimHeader = "| 글자 | 이름 | 앞뒤에서 떼나 |";

    // 4.7
    public const string ErrorCodeHeader = "| 코드 | 언제 | 위치 | 메시지 |";

    // 4.8
    public const string PercentHeader = "| 일치 | 기준 행 | 정확도(%) |";

    // 4.10. classification.md·synthetic-examples.md와 같은 글자다. 문서마다 한 번이다.
    public const string GuardHeader = "| 이름 | 샘플(`TestData/guard/…`) | 어댑터 |";

    // 13.3(T-013) 결과 쪽 중복의 줄 순서 표. 12.5 compare-duplicate의 r2 두 행.
    public const string LineOrderHeader = "| r2 결과 행 | 기준 항목 차이 | 중복 항목 차이 | 일치 | 값 불일치 | compared | outside |";

    // 7.1
    public const string ExampleHeader =
        "| 예제 | 상태 | 기준 행 | 결과 행 | 일치 | 값 불일치 | 누락 | 추가 | 허용오차 안 차이 | 정확도(%) | 누락률(%) |";

    // 3.2 모양 블록의 제목.
    public const string ShapeHeading = "### 3.2 metrics.json";

    // 4.5 칸 판정 표의 판정 "같음"(차이 없음). 4.6 표의 "예"·"아니오". 칸이 빈 칸이라는 표시. 값이 없다는 표시(null).
    public const string Same = "같음";
    public const string Yes = "예";
    public const string No = "아니오";
    public const string EmptyCell = "(빈 칸)";
    public const string None = "—";

    // 4.5 칸 판정 표의 "허용 오차" 칸: 텍스트 필드.
    public const string NoTolerance = "없음";

    // 6.4 오류 예시. 표본 파일 블록 앞 줄은 "`TestData/guard/compare-invalid/<상대 경로>`:" 모양이다.
    public const string ErrorExampleHeading = "### 6.4 오류 예시 (`compare-invalid`)";
    public const string ErrorSamplePrefix = "TestData/guard/compare-invalid/";

    // 4.10 guard 표본 CSV 줄. "- `case-valid`의 두 CSV는 …"
    public const string GuardCsvLinePrefix = "- `case-valid`의 두 CSV는";

    // 7.2~7.4 제목.
    public static readonly IReadOnlyDictionary<string, string> ExampleHeadings = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["table-compare"] = "### 7.2 table-compare",
        ["form-fill"] = "### 7.3 form-fill",
        ["mixed-exceptions"] = "### 7.4 mixed-exceptions",
        ["signoff-final"] = "### 7.5 signoff-final",
    };

    public static IReadOnlyList<string[]> Table(string header) => SpecDocument.Table(Document, header);

    // 표 칸의 값. `코드` 표시를 떼고, "(빈 칸)"은 빈 문자열이다.
    public static string Cell(string cell)
    {
        var trimmed = cell.Trim();
        if (trimmed == EmptyCell)
        {
            return string.Empty;
        }

        return trimmed.Length >= 2 && trimmed[0] == '`' && trimmed[^1] == '`' ? trimmed[1..^1] : trimmed;
    }

    // "예" → true, "아니오" → false. 다른 글자면 실패한다.
    public static bool YesNo(string cell)
    {
        var value = Cell(cell);
        Assert.True(value is Yes or No, $"'{Yes}'나 '{No}'여야 한다: {cell}");
        return value == Yes;
    }

    public static int Int(string cell) => int.Parse(Cell(cell), NumberStyles.None, CultureInfo.InvariantCulture);

    // 3.2 모양 블록(코드 블록 하나). 줄은 LF로 잇고 끝에 LF 하나(SpecDocument.CodeBlocks).
    public static string ShapeBlock() => Assert.Single(SpecDocument.CodeBlocks(Document, ShapeHeading));

    // 7.2~7.4의 json 블록 하나.
    public static string ExampleBlock(string example) => Assert.Single(SpecDocument.CodeBlocks(Document, ExampleHeadings[example]));

    // heading 아래 코드 블록과 그 블록 앞 줄의 이름표. 이름표는 "`경로`:" 또는 "`경로` (SHA-256 `…`):" 모양 줄의 경로다.
    // 그 모양이 아니면 null. 블록 내용 규칙은 SpecDocument.CodeBlocks와 같다(줄마다 LF).
    public static IReadOnlyList<(string? Label, string Content)> LabeledBlocks(string heading)
    {
        var lines = File.ReadAllLines(RepoRoot.Combine(Document));
        var start = Array.FindIndex(lines, l => l.TrimEnd() == heading);
        Assert.True(start >= 0, $"{Document}에 제목 '{heading}'이 없다.");

        var blocks = new List<(string?, string)>();
        List<string>? current = null;
        string? previous = null;
        string? label = null;
        for (var i = start + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (current is null && line.StartsWith('#'))
            {
                break;
            }

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                if (current is null)
                {
                    current = [];
                    var match = previous is null ? null : BlockLabel().Match(previous);
                    label = match is { Success: true } ? match.Groups["path"].Value : null;
                }
                else
                {
                    blocks.Add((label, string.Concat(current.Select(l => l + "\n"))));
                    current = null;
                    previous = null;
                }

                continue;
            }

            if (current is not null)
            {
                current.Add(line);
            }
            else if (line.Trim().Length > 0)
            {
                previous = line.Trim();
            }
        }

        return blocks;
    }

    // 4.7 오류 코드 표. 코드 → 메시지 틀(" / "로 나눈 `…` 조각).
    public static IReadOnlyList<(string Code, string[] Messages)> ErrorCodes() =>
        Table(ErrorCodeHeader).Select(c => (Cell(c[0]), SpecDocument.CodeSpans(c[3]))).ToArray();

    // 메시지 틀 → 정규식. "<…>"는 아무 글자(빈 칸 포함)다. 나머지는 글자 그대로.
    public static Regex MessagePattern(string template)
    {
        var parts = Placeholder().Split(template).Select(Regex.Escape);
        return new Regex("^" + string.Join(".*", parts) + "\\z", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    }

    [GeneratedRegex(@"^`(?<path>[^`]+)`( \(SHA-256 `[0-9a-f]{64}`\))?:\z", RegexOptions.CultureInvariant)]
    private static partial Regex BlockLabel();

    [GeneratedRegex(@"<[^<>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}
