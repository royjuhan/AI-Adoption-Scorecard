using System.Globalization;
using System.Text;

namespace Scorecard.Core.Synthetic;

// 문장 결과 합성 생성기(docs/spec/text-check.md 7.2). 예제 reply-drafts의 reference·candidate·scores CSV 넷을 만든다.
// SplitMix64와 NextBelow(synthetic-examples.md 4.2)를 그대로 쓴다. 한 번 뽑을 때 NextBelow를 한 번 쓴다.
// 숫자·선택 열이 아니라 문장 조각을 고른다. 같은 계획이면 같은 바이트다. 판정에는 쓰지 않는다.
// 칸은 비지 않고 앞뒤 공백·','·'"'·LF·CR·탭이 없다(어기면 ArgumentException). synthetic-examples.md 3.3과 달리 한글을 받는다.
public static class SyntheticReplies
{
    // 7.2 파일 모양의 열 이름.
    public const string TopicColumn = "topic";
    public const string TicketColumn = "ticket";
    public const string ActionColumn = "action";
    public const string ReplyColumn = "reply";
    public const string BlindColumn = "blind";
    public const string ItemColumn = "item";
    public const string SourceColumn = "source";
    public const string RaterColumn = "rater";
    public const string ScoreColumn = "score";
    public const string SourceAi = "ai";
    public const string SourceHuman = "human";

    // 문장 틀의 자리.
    private const string TicketSlot = "{ticket}";
    private const string ActionSlot = "{action}";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static SyntheticReplyFiles Generate(SyntheticReplyPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentOutOfRangeException.ThrowIfLessThan(plan.Rows, 1);
        if (plan.Topics.Count != plan.Actions.Count)
        {
            throw new ArgumentException("유형과 처리 안내의 수가 같아야 한다.", nameof(plan));
        }

        var random = new SplitMix64(plan.Seed);
        var n = plan.Rows;
        var keys = Enumerable.Range(1, n).Select(i => plan.KeyPrefix + Padded(i, plan.KeyDigits)).ToArray();

        // 1. 유형. 2. 문의 번호. 3. 인사·덧붙임·끝인사(행마다 이 순서).
        var topic = Enumerable.Range(0, n).Select(_ => random.NextBelow(plan.Topics.Count)).ToArray();
        var tickets = Enumerable.Range(0, n)
            .Select(_ => plan.TicketPrefix + (plan.TicketMin + random.NextBelow(plan.TicketSpan)).ToString(CultureInfo.InvariantCulture))
            .ToArray();
        var parts = Enumerable.Range(0, n)
            .Select(_ => (Greeting: random.NextBelow(plan.Greetings.Count), Filler: random.NextBelow(plan.Fillers.Count), Closing: random.NextBelow(plan.Closings.Count)))
            .ToArray();

        // 4. 빠뜨릴 유형의 행(키 순서)에서 번호를 빠뜨릴 행, 그 행을 뺀 목록에서 안내를 빠뜨릴 행.
        var pool = Enumerable.Range(0, n).Where(i => string.Equals(plan.Topics[topic[i]], plan.OmitTopic, StringComparison.Ordinal)).ToList();
        if (pool.Count < 2)
        {
            throw new ArgumentException($"빠뜨릴 유형의 행이 2보다 적다: {plan.OmitTopic} {pool.Count}행", nameof(plan));
        }

        var noTicket = pool[random.NextBelow(pool.Count)];
        pool.Remove(noTicket);
        var noAction = pool[random.NextBelow(pool.Count)];

        // 5. 결과 행 순서(Fisher–Yates).
        var order = Shuffle(Enumerable.Range(0, n).ToArray(), random);

        // 6. 가림 키: (행, ai), (행, human)을 행 순서로 늘어놓고 5와 같이 섞는다. k번째가 접두 + (k + 1).
        var results = Shuffle(Enumerable.Range(0, n).SelectMany(i => new[] { (Row: i, Source: SourceAi), (Row: i, Source: SourceHuman) }).ToArray(), random);
        var blinds = results.Select((_, k) => plan.BlindPrefix + Padded(k + 1, plan.BlindDigits)).ToArray();

        // 7. 점수: 채점자 순서 → 가림 키 순서. AI 결과는 빠뜨린 행이면 감점을 뺀다.
        var sheet = new List<string[]>();
        foreach (var rater in plan.Raters)
        {
            for (var k = 0; k < results.Length; k++)
            {
                var (row, source) = results[k];
                var score = source == SourceAi
                    ? plan.AiScores[random.NextBelow(plan.AiScores.Count)] - (row == noTicket || row == noAction ? plan.Penalty : 0)
                    : plan.HumanScores[random.NextBelow(plan.HumanScores.Count)];
                sheet.Add([rater, keys[row], blinds[k], score.ToString(CultureInfo.InvariantCulture)]);
            }
        }

        string Sentence(int i)
        {
            var ticket = i == noTicket ? plan.NoTicketSentence : plan.TicketSentence.Replace(TicketSlot, tickets[i], StringComparison.Ordinal);
            var action = i == noAction ? plan.NoActionSentence : plan.ActionSentence.Replace(ActionSlot, plan.Actions[topic[i]], StringComparison.Ordinal);
            return string.Join(' ', plan.Greetings[parts[i].Greeting], ticket, action, plan.Fillers[parts[i].Filler], plan.Closings[parts[i].Closing]);
        }

        var reference = Render(
            [plan.KeyColumn, TopicColumn, TicketColumn, ActionColumn],
            Enumerable.Range(0, n).Select(i => new[] { keys[i], plan.Topics[topic[i]], tickets[i], plan.Actions[topic[i]] }));
        var candidate = Render([plan.KeyColumn, ReplyColumn], order.Select(i => new[] { keys[i], Sentence(i) }));
        var sources = Render([BlindColumn, ItemColumn, SourceColumn], results.Select((r, k) => new[] { blinds[k], keys[r.Row], r.Source }));
        var scores = Render([RaterColumn, ItemColumn, BlindColumn, ScoreColumn], sheet);
        return new SyntheticReplyFiles(reference, candidate, sources, scores);
    }

    // Fisher–Yates: i = n−1..1마다 NextBelow(i + 1) 자리와 바꾼다(synthetic-examples.md 4.5 7과 같다).
    private static T[] Shuffle<T>(T[] items, SplitMix64 random)
    {
        for (var i = items.Length - 1; i >= 1; i--)
        {
            var j = random.NextBelow(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }

        return items;
    }

    private static string Padded(int value, int digits) => value.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0');

    // UTF-8(BOM 없음), LF, 끝 LF, 머리 행 먼저, 따옴표 없음.
    private static byte[] Render(IReadOnlyList<string> header, IEnumerable<string[]> rows)
    {
        var text = new StringBuilder();
        foreach (var cells in rows.Prepend([.. header]))
        {
            foreach (var cell in cells)
            {
                CheckCell(cell);
            }

            text.Append(string.Join(',', cells)).Append('\n');
        }

        return Utf8NoBom.GetBytes(text.ToString());
    }

    private static void CheckCell(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0 || char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]) || value.Any(ch => ch is ',' or '"' or '\n' or '\r' or '\t'))
        {
            throw new ArgumentException($"CSV 칸은 비지 않고 앞뒤 공백·','·'\"'·LF·CR·탭이 없어야 한다: '{value.Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal)}'", nameof(value));
        }
    }
}

// 7.2 계획 표 한 장. 칸은 계획 표의 행 순서다. Actions는 Topics와 같은 순서의 처리 안내다.
// 문의 번호는 TicketPrefix + (TicketMin + NextBelow(TicketSpan)), 키는 KeyPrefix + KeyDigits자리, 가림 키는 BlindPrefix + BlindDigits자리다.
public sealed record SyntheticReplyPlan(
    ulong Seed,
    int Rows,
    string KeyColumn,
    string KeyPrefix,
    int KeyDigits,
    IReadOnlyList<string> Topics,
    IReadOnlyList<string> Actions,
    string OmitTopic,
    string TicketPrefix,
    int TicketMin,
    int TicketSpan,
    IReadOnlyList<string> Greetings,
    string TicketSentence,
    string NoTicketSentence,
    string ActionSentence,
    string NoActionSentence,
    IReadOnlyList<string> Fillers,
    IReadOnlyList<string> Closings,
    IReadOnlyList<string> Raters,
    string BlindPrefix,
    int BlindDigits,
    IReadOnlyList<int> AiScores,
    IReadOnlyList<int> HumanScores,
    int Penalty);

// 생성 결과: reference/replies.csv, candidate/replies.csv, scores/sources.csv, scores/sheet.csv 바이트.
public sealed record SyntheticReplyFiles(byte[] Reference, byte[] Candidate, byte[] Sources, byte[] Sheet);
