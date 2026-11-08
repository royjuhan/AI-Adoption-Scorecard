using System.Globalization;
using System.Text;

namespace Scorecard.Core.Comparison;

// 가림 확인 source_pattern의 표시 값(docs/spec/text-check.md 4.4 "표시 몰림"). 글자 표시 표·키 표시 표·작은 쪽 표·몰림 문턱 표.
// 테스트가 스펙 표를 직접 대조하도록 공개한다(metrics.md 4.11 IsSupportedPattern·Matches와 같은 까닭).
// 첫 글자·끝 글자는 유니코드 코드 포인트 하나다(서로게이트 쌍도 한 글자). 키의 글자 수는 UTF-16 단위다(string.Length).
// ASCII 숫자·영문자만 따로 본다. 문화권과 상관없다. 비교는 서수다.
public static class BlindMarks
{
    // 몰림 문턱(4.4 몰림 문턱 표): 갈린 건 c ≥ 8, 12 × 반대쪽 ≤ c, 4 × 몰린 쪽 ≥ 센 건.
    public const int MinSplit = 8;
    public const int MinorityPer = 12;
    public const int SharePer = 4;

    // 틀: ASCII 숫자 묶음(0~9)을 지운 글. 빈 글이면 표시가 없다(null).
    public static string? Shape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var shape = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (!IsAsciiDigit(ch))
            {
                shape.Append(ch);
            }
        }

        return shape.Length == 0 ? null : shape.ToString();
    }

    // 첫 글자: 코드 포인트 하나. 빈 글이거나 ASCII 숫자면 표시가 없다(null).
    public static string? First(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return null;
        }

        Rune.DecodeFromUtf16(text, out _, out var consumed);
        var glyph = text[..Math.Max(consumed, 1)];
        return glyph.Length == 1 && IsAsciiDigit(glyph[0]) ? null : glyph;
    }

    // 끝 글자: 코드 포인트 하나. 빈 글이거나 ASCII 숫자면 표시가 없다(null).
    public static string? Last(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return null;
        }

        Rune.DecodeLastFromUtf16(text, out _, out var consumed);
        var glyph = text[^Math.Max(consumed, 1)..];
        return glyph.Length == 1 && IsAsciiDigit(glyph[0]) ? null : glyph;
    }

    // 꼴: ASCII 숫자는 9, ASCII 대문자는 A, ASCII 소문자는 a. 그 밖의 글자는 그대로.
    public static string Form(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return string.Create(key.Length, key, static (span, k) =>
        {
            for (var i = 0; i < k.Length; i++)
            {
                var ch = k[i];
                span[i] = IsAsciiDigit(ch) ? '9' : ch is >= 'A' and <= 'Z' ? 'A' : ch is >= 'a' and <= 'z' ? 'a' : ch;
            }
        });
    }

    // 글자 종류: 키에 있는 종류(ASCII 대문자 A, 소문자 a, 숫자 9). 이 순서다.
    public static IReadOnlyList<string> Kinds(string key)
    {
        var form = Form(key);
        return KindOrder.Where(k => form.Contains(k, StringComparison.Ordinal)).Select(k => k.ToString()).ToArray();
    }

    // 숫자 줄: ASCII 숫자 묶음을 앞에서부터 정수로 읽은 줄. 앞 0을 뗀 정수 글이다(모두 0이면 "0"). 자릿수 상한이 없다.
    public static IReadOnlyList<string> NumberRow(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var row = new List<string>();
        var i = 0;
        while (i < key.Length)
        {
            if (!IsAsciiDigit(key[i]))
            {
                i++;
                continue;
            }

            var start = i;
            while (i < key.Length && IsAsciiDigit(key[i]))
            {
                i++;
            }

            var digits = key[start..i].TrimStart('0');
            row.Add(digits.Length == 0 ? "0" : digits);
        }

        return row;
    }

    // 작은 쪽: 두 키가 모두 숫자 묶음이 있고 숫자 줄이 다르면 숫자 줄이 작은 키. 아니면 null.
    // 앞 묶음부터 견준다(길이 → 서수, 앞 0을 뗀 글이라 정수 크기와 같다). 앞이 같고 묶음이 적으면 작다. 순서와 상관없다.
    public static string? Lower(string one, string two)
    {
        var (a, b) = (NumberRow(one), NumberRow(two));
        if (a.Count == 0 || b.Count == 0)
        {
            return null;
        }

        var order = CompareRows(a, b);
        return order == 0 ? null : order < 0 ? one : two;
    }

    // 몰림: AI 쪽 건 ai, 사람 쪽 건 human, 센 건 counted. 정수 셈이다.
    public static bool Leans(int ai, int human, int counted)
    {
        var split = (long)ai + human;
        return split >= MinSplit
            && (long)MinorityPer * Math.Min(ai, human) <= split
            && (long)SharePer * Math.Max(ai, human) >= counted;
    }

    // 글 하나의 표시(4.4): 글 그대로, 틀, 첫 글자, 끝 글자. 빈 값은 표시가 아니다. (종류, 값) 짝이다.
    internal static IEnumerable<(string Kind, string Value)> MarksOf(string text)
    {
        if (text.Length > 0)
        {
            yield return ("text", text);
        }

        if (Shape(text) is { } shape)
        {
            yield return ("shape", shape);
        }

        if (First(text) is { } first)
        {
            yield return ("first", first);
        }

        if (Last(text) is { } last)
        {
            yield return ("last", last);
        }
    }

    // 가림 키의 표시: 글 하나의 표시 넷 + 꼴, 글자 수(UTF-16 단위), 글자 종류.
    internal static IEnumerable<(string Kind, string Value)> KeyMarksOf(string key)
    {
        foreach (var mark in MarksOf(key))
        {
            yield return mark;
        }

        if (key.Length > 0)
        {
            yield return ("form", Form(key));
            yield return ("length", key.Length.ToString(CultureInfo.InvariantCulture));
        }

        foreach (var kind in Kinds(key))
        {
            yield return ("class", kind);
        }
    }

    private static readonly char[] KindOrder = ['A', 'a', '9'];

    private static bool IsAsciiDigit(char ch) => ch is >= '0' and <= '9';

    private static int CompareRows(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        for (var i = 0; i < Math.Min(a.Count, b.Count); i++)
        {
            var order = a[i].Length != b[i].Length ? a[i].Length.CompareTo(b[i].Length) : string.CompareOrdinal(a[i], b[i]);
            if (order != 0)
            {
                return order;
            }
        }

        return a.Count.CompareTo(b.Count);
    }
}
