using Scorecard.Core.Comparison;

namespace Scorecard.Tests.TextCheck;

// 스펙 4.4 표시 몰림의 표시 값: 글자 표시 표, 키 표시 표, 작은 쪽 표, 몰림 문턱 표, 몰림 예 표 = Core.
// Core 규칙 상수는 internal이다. 테스트가 표를 직접 대조하도록 BlindMarks를 공개한다(metrics.md 4.11 IsSupportedPattern·Matches와 같은 까닭).
// BlindMarks는 스펙에 이름이 없다. 테스트가 지은 이름이다(PR (b) 보고 목록). 몰림 예·문턱은 엔진 출력으로도 본다(BlindCheckTests).
[Trait("Spec", "text-check")]
public class BlindMarkTests
{
    public static TheoryData<string, string, string, string> GlyphRows => Rows(TextCheckSpec.GlyphHeader);

    public static TheoryData<string, string, string, string> KeyRows
    {
        get
        {
            var data = new TheoryData<string, string, string, string>();
            foreach (var c in TextCheckSpec.Table(TextCheckSpec.KeyMarkHeader))
            {
                data.Add(TextCheckSpec.Cell(c[0]), TextCheckSpec.Cell(c[1]), c[3].Trim(), c[4].Trim());
            }

            return data;
        }
    }

    public static TheoryData<string, string, string> LowerRows
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach (var c in TextCheckSpec.Table(TextCheckSpec.LowerHeader))
            {
                data.Add(TextCheckSpec.Cell(c[0]), TextCheckSpec.Cell(c[1]), TextCheckSpec.Cell(c[2]));
            }

            return data;
        }
    }

    public static TheoryData<int, int, int, bool> LeanRows
    {
        get
        {
            var data = new TheoryData<int, int, int, bool>();
            foreach (var c in TextCheckSpec.Table(TextCheckSpec.LeanExampleHeader))
            {
                data.Add(TextCheckSpec.Int(c[0]), TextCheckSpec.Int(c[1]), TextCheckSpec.Int(c[2]), TextCheckSpec.YesNo(c[3]));
            }

            return data;
        }
    }

    // 글자 표시 표: 틀(ASCII 숫자 묶음을 지운 글), 첫 글자·끝 글자(ASCII 숫자가 아닐 때, 코드 포인트 하나). "—"는 없음(null).
    [Theory]
    [MemberData(nameof(GlyphRows))]
    public void Glyph_marks_match_table(string text, string shape, string first, string last)
    {
        Assert.Equal(Mark(shape), BlindMarks.Shape(text));
        Assert.Equal(Mark(first), BlindMarks.First(text));
        Assert.Equal(Mark(last), BlindMarks.Last(text));
    }

    // 키 표시 표: 꼴, 글자 수(UTF-16 단위 = string.Length), 글자 종류(A·a·9 순서), 숫자 줄(앞 0을 뗀 정수 글. "—"는 묶음 없음).
    [Theory]
    [MemberData(nameof(KeyRows))]
    public void Key_marks_match_table(string key, string form, string kinds, string numbers)
    {
        var row = Assert.Single(TextCheckSpec.Table(TextCheckSpec.KeyMarkHeader), r => TextCheckSpec.Cell(r[0]) == key);

        Assert.Equal(form, BlindMarks.Form(key));
        Assert.Equal(TextCheckSpec.Int(row[2]), key.Length);
        Assert.Equal(TextCheckSpec.List(kinds), BlindMarks.Kinds(key));
        Assert.Equal(numbers == TextCheckSpec.None ? [] : numbers.Split(", "), BlindMarks.NumberRow(key));
    }

    // 작은 쪽 표. 순서와 상관없다(키 하나·키 둘을 바꿔도 같다). "—"는 없음(null).
    [Theory]
    [MemberData(nameof(LowerRows))]
    public void Lower_side_matches_table(string one, string two, string lower)
    {
        Assert.Equal(Mark(lower), BlindMarks.Lower(one, two));
        Assert.Equal(Mark(lower), BlindMarks.Lower(two, one));
    }

    // 몰림 문턱 표 = Core 상수(상수라 xUnit2000에 따라 expected 자리에 둔다).
    [Fact]
    public void Lean_thresholds_match_table()
    {
        var values = TextCheckSpec.Table(TextCheckSpec.LeanThresholdHeader).ToDictionary(r => TextCheckSpec.Cell(r[0]), r => TextCheckSpec.Int(r[1]), StringComparer.Ordinal);

        Assert.Equal(BlindMarks.MinSplit, values["min_split"]);
        Assert.Equal(BlindMarks.MinorityPer, values["minority_per"]);
        Assert.Equal(BlindMarks.SharePer, values["share_per"]);
    }

    // 몰림 예 표 = Core 셈.
    [Theory]
    [MemberData(nameof(LeanRows))]
    public void Lean_examples_match_core(int ai, int human, int counted, bool leans)
    {
        Assert.Equal(leans, BlindMarks.Leans(ai, human, counted));
    }

    // 4.4 글머리: 서로게이트 쌍은 한 글자, 결합 문자도 한 글자(NFD 가의 첫 글자는 U+1100), 전각 숫자는 표시다. 키의 글자 수와 다르다.
    [Fact]
    public void First_and_last_glyphs_are_code_points()
    {
        Assert.Equal("\U0001F600", BlindMarks.First("\U0001F60001"));
        Assert.Equal(4, "\U0001F60001".Length);
        Assert.Equal("ᄀ", BlindMarks.First("가"));
        Assert.Equal("ᅡ", BlindMarks.Last("가"));
        Assert.Equal("１", BlindMarks.First("１2"));
        Assert.Null(BlindMarks.Last("１2"));
    }

    // 숫자 줄은 자릿수가 길어도 넘치지 않는다(앞 0을 떼고 길이·글로 견준다, 8절 core-dev 3).
    [Fact]
    public void Number_rows_compare_without_overflow()
    {
        var big = "b" + new string('9', 40);
        var bigger = "b1" + new string('0', 40);

        Assert.Equal([new string('9', 40)], BlindMarks.NumberRow(big));
        Assert.Equal(["1" + new string('0', 40)], BlindMarks.NumberRow(bigger));
        Assert.Equal(big, BlindMarks.Lower(big, bigger));
        Assert.Equal("b0007", BlindMarks.Lower("b0007", "b8"));
    }

    private static TheoryData<string, string, string, string> Rows(string header)
    {
        var data = new TheoryData<string, string, string, string>();
        foreach (var c in TextCheckSpec.Table(header))
        {
            data.Add(TextCheckSpec.Cell(c[0]), c[1].Trim(), c[2].Trim(), c[3].Trim());
        }

        return data;
    }

    // 표 칸 → 표시 값. "—"는 없음(null). `코드`를 뗀다.
    private static string? Mark(string cell) => cell.Trim() == TextCheckSpec.None ? null : TextCheckSpec.Cell(cell);
}
