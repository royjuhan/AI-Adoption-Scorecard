using System.Globalization;
using System.Text.RegularExpressions;
using Scorecard.Core.Synthetic;

namespace Scorecard.Tests.TextCheck;

// 스펙 7.2 문장 생성기. 계획 표(| 계획 | 값 |)로 Core 생성기를 돌려 7.3 CSV 넷과 바이트까지 같다(T-005 방식). 질문 11, 카드 수용 기준 4.
// 생성기 이름 SyntheticReplies는 스펙의 제안 이름이다. 계획·결과 레코드(SyntheticReplyPlan, SyntheticReplyFiles)와
// Generate 서명은 스펙에 없어 테스트가 지었다(PR (b) 보고 목록). 계획 레코드의 칸은 계획 표의 행 순서다.
[Trait("Spec", "text-check")]
public partial class SyntheticRepliesTests
{
    private static readonly string[] CsvFiles = ["reference/replies.csv", "candidate/replies.csv", "scores/sources.csv", "scores/sheet.csv"];

    // 계획 표의 행 이름(순서가 뜻이다).
    private static readonly string[] PlanRows =
    [
        "시드", "행 수", "키", "유형", "처리 안내", "빠뜨릴 유형", "문의 번호", "인사", "번호 문장", "번호 빠진 문장",
        "안내 문장", "안내 빠진 문장", "덧붙임", "끝인사", "채점자", "가림 키", "AI 점수", "사람 점수", "빠진 항목 감점",
    ];

    public static TheoryData<int> FileIndexes => new(Enumerable.Range(0, CsvFiles.Length));

    // 계획 표의 행 = 위 목록(순서 포함).
    [Fact]
    public void Plan_table_has_the_planned_rows()
    {
        Assert.Equal(PlanRows, TextCheckSpec.Table(TextCheckSpec.PlanHeader).Select(c => c[0].Trim()));
    }

    // 계획 표로 돌린 결과 = 7.3 블록(reference·candidate·sources·sheet 순서).
    [Theory]
    [MemberData(nameof(FileIndexes))]
    public void Generator_reproduces_example_csv(int index)
    {
        var files = Parts(SyntheticReplies.Generate(Plan()));

        Assert.Equal(TextCheckInput.Text(TextCheckSpec.FileBlock(CsvFiles[index])), TextCheckInput.Text(files[index]));
        Assert.Equal(TextCheckSpec.FileBlock(CsvFiles[index]), files[index]);
    }

    // 같은 시드면 같은 바이트다(두 번 돌린다).
    [Fact]
    public void Same_seed_gives_same_bytes()
    {
        var first = Parts(SyntheticReplies.Generate(Plan()));
        var second = Parts(SyntheticReplies.Generate(Plan()));

        Assert.All(Enumerable.Range(0, CsvFiles.Length), i => Assert.Equal(first[i], second[i]));
    }

    // 시드는 조건(세 유형 모두 2행 이상, AI 평균 < 사람 평균, 차이 0.5 이하)을 만족하는 가장 작은 값이다.
    // 시드 0은 점수 차이가 0.5를 넘고(136/36, 157/36), 시드 1은 배송이 1행이다. 시드 2(표 값)는 조건을 만족한다.
    [Fact]
    public void Plan_seed_is_the_smallest_seed_meeting_the_conditions()
    {
        var plan = Plan();
        Assert.Equal(2UL, plan.Seed);

        var zero = Parts(SyntheticReplies.Generate(plan with { Seed = 0 }));
        Assert.Equal((136, 157), (Sum(zero, TextCheckSpec.SourceAi), Sum(zero, TextCheckSpec.SourceHuman)));
        Assert.False(Meets(zero, plan));

        var one = Parts(SyntheticReplies.Generate(plan with { Seed = 1 }));
        Assert.Equal(1, Topics(one).Count(t => t == "배송"));
        Assert.False(Meets(one, plan));

        Assert.True(Meets(Parts(SyntheticReplies.Generate(plan)), plan));
    }

    // 4: 빠뜨릴 유형의 행이 2보다 적으면 예외(시드 1의 배송은 1행).
    [Fact]
    public void Too_few_rows_of_the_omitted_topic_throw()
    {
        var plan = Plan() with { Seed = 1, OmitTopic = "배송" };

        Assert.ThrowsAny<Exception>(() => SyntheticReplies.Generate(plan));
    }

    // CSV 칸에 `,`·`"`·LF·CR·탭·앞뒤 공백이 들어가거나 칸이 비면 예외.
    [Theory]
    [InlineData("안녕하세요,")]
    [InlineData("\"안녕하세요.\"")]
    [InlineData("안녕\n하세요.")]
    [InlineData("안녕\r하세요.")]
    [InlineData("안녕\t하세요.")]
    [InlineData(" 안녕하세요.")]
    public void Unsafe_cells_throw(string greeting)
    {
        var plan = Plan() with { Greetings = [greeting, greeting] };

        Assert.ThrowsAny<Exception>(() => SyntheticReplies.Generate(plan));
    }

    // 빈 칸도 예외(첫 유형의 처리 안내를 비운다. 그 유형의 기준 행 action 칸이 빈다).
    [Fact]
    public void Empty_cells_throw()
    {
        var plan = Plan();
        plan = plan with { Actions = [string.Empty, .. plan.Actions.Skip(1)] };

        Assert.ThrowsAny<Exception>(() => SyntheticReplies.Generate(plan));
    }

    // 계획 표 → 계획 레코드. 칸 안의 `코드`를 읽는다. "2자리"·"NextBelow(900)"는 글에서 수를 읽는다.
    internal static SyntheticReplyPlan Plan()
    {
        var rows = TextCheckSpec.Table(TextCheckSpec.PlanHeader).ToDictionary(c => c[0].Trim(), c => c[1], StringComparer.Ordinal);
        string[] Spans(string name) => TextCheckSpec.List(rows[name]);
        int Number(string name) => int.Parse(Spans(name).Single(), NumberStyles.None, CultureInfo.InvariantCulture);
        int Digits(string name) => int.Parse(DigitCount().Match(rows[name]).Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture);

        var key = Spans("키");
        var topics = Spans("유형");
        var actions = Spans("처리 안내");
        Assert.Equal(topics.Length * 2, actions.Length);
        var actionByTopic = Enumerable.Range(0, topics.Length).ToDictionary(i => actions[2 * i], i => actions[(2 * i) + 1], StringComparer.Ordinal);
        var ticket = Spans("문의 번호");
        var span = int.Parse(NextBelow().Match(ticket[2]).Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture);

        return new SyntheticReplyPlan(
            Seed: ulong.Parse(Spans("시드").Single(), NumberStyles.None, CultureInfo.InvariantCulture),
            Rows: Number("행 수"),
            KeyColumn: key[0],
            KeyPrefix: key[1],
            KeyDigits: Digits("키"),
            Topics: topics,
            Actions: topics.Select(t => actionByTopic[t]).ToArray(),
            OmitTopic: Spans("빠뜨릴 유형").Single(),
            TicketPrefix: ticket[0],
            TicketMin: int.Parse(ticket[1], NumberStyles.None, CultureInfo.InvariantCulture),
            TicketSpan: span,
            Greetings: Spans("인사"),
            TicketSentence: Spans("번호 문장").Single(),
            NoTicketSentence: Spans("번호 빠진 문장").Single(),
            ActionSentence: Spans("안내 문장").Single(),
            NoActionSentence: Spans("안내 빠진 문장").Single(),
            Fillers: Spans("덧붙임"),
            Closings: Spans("끝인사"),
            Raters: Spans("채점자"),
            BlindPrefix: Spans("가림 키")[0],
            BlindDigits: Digits("가림 키"),
            AiScores: Spans("AI 점수").Select(s => int.Parse(s, NumberStyles.None, CultureInfo.InvariantCulture)).ToArray(),
            HumanScores: Spans("사람 점수").Select(s => int.Parse(s, NumberStyles.None, CultureInfo.InvariantCulture)).ToArray(),
            Penalty: Number("빠진 항목 감점"));
    }

    private static byte[][] Parts(SyntheticReplyFiles files) => [files.Reference, files.Candidate, files.Sources, files.Sheet];

    // 출처별 점수 합(sources: blind,item,source / sheet: rater,item,blind,score).
    private static int Sum(byte[][] files, string source)
    {
        var sources = Rows(files[2]).ToDictionary(c => c[0], c => c[2], StringComparer.Ordinal);
        return Rows(files[3]).Where(c => sources[c[2]] == source).Sum(c => int.Parse(c[3], NumberStyles.None, CultureInfo.InvariantCulture));
    }

    private static int Count(byte[][] files, string source)
    {
        var sources = Rows(files[2]).ToDictionary(c => c[0], c => c[2], StringComparer.Ordinal);
        return Rows(files[3]).Count(c => sources[c[2]] == source);
    }

    private static string[] Topics(byte[][] files) => Rows(files[0]).Select(c => c[1]).ToArray();

    // 7.2 시드 조건. 셈으로: ai.sum × h.n < h.sum × a.n, 그리고 (h.sum × a.n − ai.sum × h.n) × 2 ≤ a.n × h.n.
    private static bool Meets(byte[][] files, SyntheticReplyPlan plan)
    {
        var topics = Topics(files);
        var (aiSum, humanSum) = (Sum(files, TextCheckSpec.SourceAi), Sum(files, TextCheckSpec.SourceHuman));
        var (aiN, humanN) = (Count(files, TextCheckSpec.SourceAi), Count(files, TextCheckSpec.SourceHuman));
        var gap = (humanSum * aiN) - (aiSum * humanN);
        return plan.Topics.All(t => topics.Count(x => x == t) >= 2) && gap > 0 && gap * 2 <= aiN * humanN;
    }

    private static IEnumerable<string[]> Rows(byte[] csv) =>
        TextCheckInput.Text(csv).Split('\n').Skip(1).Where(l => l.Length > 0).Select(l => l.Split(','));

    [GeneratedRegex(@"(\d+)자리", RegexOptions.CultureInvariant)]
    private static partial Regex DigitCount();

    [GeneratedRegex(@"^NextBelow\((\d+)\)\z", RegexOptions.CultureInvariant)]
    private static partial Regex NextBelow();
}
