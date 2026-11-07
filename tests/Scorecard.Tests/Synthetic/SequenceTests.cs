using System.Globalization;
using Scorecard.Core.Synthetic;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Synthetic;

// 스펙 4.2 수열(SplitMix64, NextBelow)과 4.5의 1번(기준 행 뽑기). 카드 수용 기준 1.
// SplitMix64는 internal이다. 테스트는 수열을 따로 만들지 않고 Core 공개 API(Generate)로 본다(4.2).
// 기대값은 스펙에 적힌 시드 0의 처음 세 값에서 NextBelow(n) = (값 × n)의 상위 64비트로 계산한다.
[Trait("Spec", "synthetic-examples")]
public class SequenceTests
{
    // 4.2 "- 시드 0의 처음 세 값: `…`, `…`, `…` [P:prng]."
    private const string FirstValuesPrefix = "- 시드 0의 처음 세 값: ";

    // 4.2 "- `SplitMix64`는 `internal`이다. … 정수 열 하나(`0`~`999`), 3행 … 기준 값이 `883`, `431`, `26`이다."
    private const string ViaGeneratePrefix = "- `SplitMix64`는 `internal`이다.";

    // 시드 0의 처음 세 값(스펙 줄에서 읽는다).
    private static ulong[] FirstValues()
    {
        var spans = SpecDocument.CodeSpans(SpecDocument.Line(SyntheticSpec.Document, FirstValuesPrefix));
        Assert.Equal(3, spans.Length);
        Assert.All(spans, s => Assert.Matches("^[0-9a-f]{16}$", s));
        return spans.Select(s => ulong.Parse(s, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)).ToArray();
    }

    // NextBelow(bound) = Next() × bound의 상위 64비트(4.2).
    private static int Below(ulong value, int bound) => (int)Math.BigMul(value, (ulong)bound, out _);

    // 심기·섞기 없는 계획. 키 k1, k2, …
    private static SyntheticPlan Plain(ulong seed, int rows, params SyntheticColumn[] columns) =>
        new(seed, "x.csv", "k", 1, rows, columns, [], 0, 0, null, 0, null, null, false);

    private static string[][] Reference(SyntheticPlan plan) =>
        SyntheticGenerator.Generate(SyntheticInput.Case(SyntheticInput.NoCompareCase), plan).Reference.Select(r => r.Values.ToArray()).ToArray();

    // 4.2: 시드 0, 정수 열 하나(0~999), 3행, 심기·섞기 없음 → 기준 값이 스펙에 적힌 세 값.
    [Fact]
    public void Seed_zero_integer_column_gives_spec_values()
    {
        var spans = SpecDocument.CodeSpans(SpecDocument.Line(SyntheticSpec.Document, ViaGeneratePrefix));
        Assert.Equal(["SplitMix64", "internal", "0", "999"], spans[..4]);
        var expected = spans[4..];
        Assert.Equal(3, expected.Length);

        var values = Reference(Plain(0, 3, SyntheticColumn.Integer("v", 0, 999))).Select(r => Assert.Single(r));

        Assert.Equal(expected, values);
    }

    // 스펙 안 두 줄이 서로 맞는다: 처음 세 값 × 1000의 상위 64비트 = 883, 431, 26.
    [Fact]
    public void Spec_first_values_and_generate_values_agree()
    {
        var spans = SpecDocument.CodeSpans(SpecDocument.Line(SyntheticSpec.Document, ViaGeneratePrefix));

        Assert.Equal(spans[4..], FirstValues().Select(v => Below(v, 1000).ToString(CultureInfo.InvariantCulture)));
    }

    // 4.5의 1: 정수는 최소 + NextBelow(최대 − 최소 + 1). 최소가 0이 아니어도 같다.
    [Fact]
    public void Integer_draw_is_min_plus_next_below_width()
    {
        var expected = FirstValues().Select(v => (5 + Below(v, 10)).ToString(CultureInfo.InvariantCulture));

        Assert.Equal(expected, Reference(Plain(0, 3, SyntheticColumn.Integer("v", 5, 14))).Select(r => r[0]));
    }

    // 4.5의 1: 소수는 자릿수 단위의 정수로 뽑고 나눈다. 자릿수 고정(F2)으로 쓴다(3.3).
    [Fact]
    public void Decimal_draw_uses_scale_units()
    {
        var expected = FirstValues().Select(v => ((1000 + Below(v, 9000)) / 100m).ToString("F2", CultureInfo.InvariantCulture));

        Assert.Equal(expected, Reference(Plain(0, 3, SyntheticColumn.Decimal("v", 10.00m, 99.99m, 2))).Select(r => r[0]));
    }

    // 4.5의 1: 선택은 선택지[NextBelow(개수)]. 적힌 순서가 뽑는 순서다(4.4).
    [Fact]
    public void Choice_draw_indexes_choices_in_order()
    {
        string[] choices = ["item-a", "item-b", "item-c", "item-d", "item-e"];
        var expected = FirstValues().Select(v => choices[Below(v, choices.Length)]);

        Assert.Equal(expected, Reference(Plain(0, 3, SyntheticColumn.Choice("v", choices))).Select(r => r[0]));
    }

    // 4.5의 1: 행 하나 안에서 열 표 순서대로 한 칸씩 뽑는다. NextBelow 한 번에 Next() 한 번.
    [Fact]
    public void One_row_draws_columns_in_order()
    {
        var v = FirstValues();
        string[] choices = ["a", "b", "c"];
        var plan = Plain(
            0,
            1,
            SyntheticColumn.Integer("n", 0, 999),
            SyntheticColumn.Decimal("d", 10.00m, 99.99m, 2),
            SyntheticColumn.Choice("c", choices));

        var row = Assert.Single(Reference(plan));

        Assert.Equal(
            [
                Below(v[0], 1000).ToString(CultureInfo.InvariantCulture),
                ((1000 + Below(v[1], 9000)) / 100m).ToString("F2", CultureInfo.InvariantCulture),
                choices[Below(v[2], 3)],
            ],
            row);
    }

    // 4.3 키 = 접두어 + 행 번호(1부터, 앞을 0으로 채움). 심기·섞기 없으면 결과 = 기준.
    [Fact]
    public void Keys_are_prefix_and_zero_padded_row_number()
    {
        var plan = Plain(0, 3, SyntheticColumn.Integer("v", 0, 999)) with { KeyPrefix = "log", KeyDigits = 2 };
        var tables = SyntheticGenerator.Generate(SyntheticInput.Case(SyntheticInput.NoCompareCase), plan);

        Assert.Equal(["log01", "log02", "log03"], tables.Reference.Select(r => r.Key));
        Assert.Equal(SyntheticInput.Cells(tables.Reference), SyntheticInput.Cells(tables.Candidate));
        Assert.Equal(["id", "v"], tables.Header);
        Assert.Equal("x.csv", tables.FileName);
        Assert.All(tables.Reference.Concat(tables.Candidate), r => Assert.Equal(SyntheticGenerator.PlantedNone, r.Planted));
    }
}
