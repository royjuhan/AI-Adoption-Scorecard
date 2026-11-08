using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.TextCheck;

// 채택된 문장 결과 스펙(docs/spec/text-check.md)의 표·블록을 읽는다. Core를 쓰지 않는다.
// 표 모양(머리 행)은 스펙이 "바꾸지 않는다"고 정했다. 머리 행이 문서에 정확히 한 번 나와야 한다(SpecDocument.Table).
internal static partial class TextCheckSpec
{
    public const string Document = SpecDocument.TextCheckPath;

    public const string Example = "reply-drafts";
    public const string ScoreOnly = "score-only";

    // 3.1·3.2
    public const string PropertyHeader = "| 새 속성 | 형식 | 뜻 |";
    public const string JudgmentPropertyHeader = "| 판정 새 속성 | 형식 | 뜻 |";

    // 4.1
    public const string ParserSceneHeader = "| case.yaml 바꾼 곳 | 결과 | 코드 | 메시지 |";

    // 4.2
    public const string MatchHeader = "| 항목 | 기준 칸 | 결과 문장 | 결과 |";
    public const string ChecklistSceneHeader = "| 체크리스트 바꾼 곳 | 결과 | 코드 | 정확도(%) |";

    // 4.3
    public const string ScoreFileHeader = "| 파일 | 열 | 뜻 |";
    public const string MeanHeader = "| 점수 합 | 점수 수 | 평균 |";

    // 4.4
    public const string BlindRuleHeader = "| 가림 규칙 | 보는 곳 | 걸리는 조건 | 발견 위치·값 |";
    public const string GlyphHeader = "| 글 | 틀 | 첫 글자 | 끝 글자 |";
    public const string KeyMarkHeader = "| 가림 키 | 꼴 | 글자 수 | 글자 종류 | 숫자 줄 |";
    public const string LowerHeader = "| 키 하나 | 키 둘 | 작은 쪽 |";
    public const string LeanThresholdHeader = "| 몰림 문턱 | 값 | 조건 |";
    public const string LeanExampleHeader = "| AI 쪽 건 | 사람 쪽 건 | 센 건 | 걸리나 |";
    public const string ScoreSceneHeader = "| 점수 자료 바꾼 곳 | 결과 | 가림 발견 | 코드 |";

    // 4.5
    public const string ScoreCodeHeader = "| 점수 코드 | 언제 | 위치 | 메시지 |";
    public const string ChecklistCodeHeader = "| 체크리스트 코드 | 언제 | 위치 | 메시지 |";

    // 5.1~5.5
    public const string ScoreDefaultHeader = "| 점수 문턱 | 기본값 | 근거 |";
    public const string ScoreCheckHeader = "| 점수 확인 | 결과 | 걸리는 조건 | 읽는 문턱 | 확인 문장 틀 |";
    public const string ZeroSentenceHeader = "| 문턱 0 문장 | 근거 키 | 결과 | 문장 틀 |";
    public const string ScoreVerdictHeader = "| 자리 | 근거 키 | 확인 결과 | 길 | 판정 | 판정 문장 틀 |";
    public const string ScoreReadHeader = "| 자리 | 근거 키 | 확인 결과 | 길 | 읽은 문턱 |";
    public const string ScoreTodoHeader = "| 점수 할 일 | 나오는 때 | 문구 |";

    // 6.1
    public const string JudgmentSceneHeader = "| 장면 | 바탕 | 바꾼 곳 | 판정 | 근거 키 | 확인 결과 | 다음 할 일 |";

    // 7.1·7.2
    public const string NewExampleHeader = "| 새 예제 | inputs.group | 판정 | 근거 키 | 확인 여섯 | 다음 할 일 | 검토 항목 |";
    public const string ExampleFileHeader = "| 예제 파일 | 만드는 곳 |";
    public const string PlanHeader = "| 계획 | 값 |";

    // 6.2 score-only case.yaml 블록 앞 줄.
    public const string ScoreOnlyCaseLine = "`score-only/case.yaml`:";

    // 표 글. "(없음)"은 바꾸지 않음, "—"는 값 없음, "(빈 칸)"은 빈 글.
    public const string NoChange = "(없음)";
    public const string None = "—";
    public const string EmptyCell = "(빈 칸)";
    public const string Accepted = "받음";
    public const string Rejected = "오류";
    public const string Yes = "예";
    public const string No = "아니오";

    // 6.1·5.3 "길" 칸.
    public const string PathTable = "표 대조";
    public const string PathScoresOnly = "점수표만";
    public const string PathAll = "모두";

    // 4.3 출처 값.
    public const string SourceAi = "ai";
    public const string SourceHuman = "human";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static IReadOnlyList<string[]> Table(string header) => SpecDocument.Table(Document, header);

    // 표 칸의 값. 앞뒤 공백을 떼고, 칸 전체를 감싼 `코드` 표시를 뗀다. "(빈 칸)"은 빈 글이다.
    public static string Cell(string cell)
    {
        var trimmed = cell.Trim();
        if (trimmed == EmptyCell)
        {
            return string.Empty;
        }

        return trimmed.Length >= 2 && trimmed[0] == '`' && trimmed[^1] == '`' ? trimmed[1..^1] : trimmed;
    }

    // 칸 안의 `코드` 목록. "—"·"(없음)"이면 빈 목록.
    public static string[] List(string cell) =>
        cell.Trim() is None or NoChange ? [] : SpecDocument.CodeSpans(cell);

    public static int Int(string cell) => int.Parse(Cell(cell), NumberStyles.None, CultureInfo.InvariantCulture);

    public static bool YesNo(string cell)
    {
        var value = Cell(cell);
        Assert.True(value is Yes or No, $"'{Yes}'나 '{No}'여야 한다: {cell}");
        return value == Yes;
    }

    // 표 글의 \n·\t·\uXXXX(4.1·4.2 표 머리말).
    public static string Unescape(string text) =>
        Escape().Replace(text, m => m.Groups[1].Value switch
        {
            "n" => "\n",
            "t" => "\t",
            var u => ((char)int.Parse(u[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString(),
        });

    // 7.1 예제 파일 표: 예제 안 상대 경로(표 순서).
    public static string[] ExampleFiles() => Table(ExampleFileHeader).Select(c => Cell(c[0])).ToArray();

    // 7.3·7.4의 "`examples/reply-drafts/<경로>` (SHA-256 `…`, N바이트):" 줄.
    public static string FileLinePrefix(string relative) => $"`examples/{Example}/{relative}` (SHA-256 ";

    public static string FileLine(string relative) => SpecDocument.Line(Document, FileLinePrefix(relative));

    // 그 줄 뒤 블록의 바이트(줄마다 LF).
    public static byte[] FileBlock(string relative) => Utf8NoBom.GetBytes(SpecDocument.BlockAfter(Document, FileLinePrefix(relative)));

    // 그 줄의 SHA-256과 바이트 수.
    public static (string Sha256, int Bytes) FileLineValues(string relative)
    {
        var match = FileLineShape().Match(FileLine(relative));
        Assert.True(match.Success, $"{Document}의 {relative} 줄 모양이 다르다: {FileLine(relative)}");
        return (match.Groups["sha"].Value, int.Parse(match.Groups["bytes"].Value.Replace(",", string.Empty, StringComparison.Ordinal), NumberStyles.None, CultureInfo.InvariantCulture));
    }

    // 7.3 바탕 파일(expected/ 제외): 상대 경로 → 바이트.
    public static IReadOnlyDictionary<string, byte[]> BaseFiles() =>
        ExampleFiles().Where(f => !f.StartsWith("expected/", StringComparison.Ordinal)).ToDictionary(f => f, FileBlock, StringComparer.Ordinal);

    // 6.2 score-only: 그 case.yaml 블록 + 7.3 scores/ 두 파일.
    public static IReadOnlyDictionary<string, byte[]> ScoreOnlyFiles() =>
        new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["case.yaml"] = Utf8NoBom.GetBytes(SpecDocument.BlockAfter(Document, ScoreOnlyCaseLine)),
            ["scores/sources.csv"] = FileBlock("scores/sources.csv"),
            ["scores/sheet.csv"] = FileBlock("scores/sheet.csv"),
        };

    // 6.1 "바탕" 칸 → 파일.
    public static IReadOnlyDictionary<string, byte[]> Base(string name) => name switch
    {
        Example => BaseFiles(),
        ScoreOnly => ScoreOnlyFiles(),
        _ => throw new ArgumentException($"알 수 없는 바탕: {name}", nameof(name)),
    };

    // 6.1 글머리 '- "<장면>": …'의 마지막 `코드`(판정 문장).
    public static string SceneSentence(string scene) => SpecDocument.CodeSpans(SpecDocument.Line(Document, $"- \"{scene}\": ")).Last();

    // 메시지 틀 → 정규식. "<…>"는 아무 글자(빈 칸 포함)다. 나머지는 글자 그대로.
    public static Regex MessagePattern(string template)
    {
        var parts = Placeholder().Split(template).Select(Regex.Escape);
        return new Regex("^" + string.Join(".*", parts) + "\\z", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    }

    // 문장 틀의 {이름}을 값으로 바꾼다. 틀에 있는데 값이 없으면 실패한다.
    public static string Fill(string template, IReadOnlyDictionary<string, string> slots) =>
        Slot().Replace(template, m =>
        {
            var name = m.Groups["name"].Value;
            Assert.True(slots.ContainsKey(name), $"문장 틀 '{template}'의 {{{name}}} 값이 장면에 없다.");
            return slots[name];
        });

    [GeneratedRegex(@"\\(n|t|u[0-9A-Fa-f]{4})", RegexOptions.CultureInvariant)]
    private static partial Regex Escape();

    [GeneratedRegex(@"\(SHA-256 `(?<sha>[0-9a-f]{64})`, (?<bytes>[0-9,]+)바이트\):\z", RegexOptions.CultureInvariant)]
    private static partial Regex FileLineShape();

    [GeneratedRegex(@"<[^<>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\{(?<name>[a-z_]+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex Slot();
}
