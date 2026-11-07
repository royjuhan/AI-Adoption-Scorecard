using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;

namespace Scorecard.Tests.Comparison;

// 스펙 4.5 숫자 모양 표(승인 질문 9). 카드 수용 기준 2(형식 오류: 숫자 아님).
// 숫자 필드(tolerance·tolerance_pct가 있는 compare 필드)의 칸. InvariantCulture, 부호·소수점·지수.
// 기준 칸이 숫자가 아니면 number 오류(빈 칸 포함), 결과 칸이면 not_number로 값 불일치.
[Trait("Spec", "metrics")]
public class NumberTests
{
    private static readonly CompareField Field = new("v", 0m, null);

    // 표의 행마다: 숫자면 기준·결과 어느 쪽에 두어도 오류가 없고, "값" 칸과 같은 수다(tolerance 0으로 "같음").
    // 숫자가 아니면 기준 칸은 number 오류, 결과 칸은 not_number.
    [Fact]
    public void Number_table_rows_hold_on_both_sides()
    {
        var rows = MetricsSpec.Table(MetricsSpec.NumberHeader);
        Assert.NotEmpty(rows);
        var wrong = new List<string>();

        foreach (var c in rows)
        {
            var cell = MetricsSpec.Cell(c[0]);
            if (MetricsSpec.YesNo(c[1]))
            {
                var value = MetricsSpec.Cell(c[2]);
                Expect(wrong, $"'{cell}' 기준 = 값 {value}", CompareInput.Judge(Field, cell, value), MetricsSpec.Same);
                Expect(wrong, $"'{cell}' 결과 = 값 {value}", CompareInput.Judge(Field, value, cell), MetricsSpec.Same);
            }
            else
            {
                Expect(wrong, $"'{cell}' 값 칸", MetricsSpec.Cell(c[2]), MetricsSpec.None);
                Expect(
                    wrong,
                    $"'{cell}' 기준 칸",
                    string.Join(" | ", CompareInput.Lines(Reference(cell).Errors)),
                    $"reference/a.csv:2:3: number v: 숫자가 아니다: {cell}");
                Expect(wrong, $"'{cell}' 결과 칸", CompareInput.Judge(Field, "1", cell), CompareEngine.ResultNotNumber);
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
        Assert.Contains(rows, c => MetricsSpec.YesNo(c[1]));
        Assert.Contains(rows, c => !MetricsSpec.YesNo(c[1]));
    }

    // 앞뒤 공백이 있는 " 12 "도 숫자 12다(정규화 뒤).
    [Fact]
    public void Spaces_around_number_are_trimmed()
    {
        Assert.Equal(MetricsSpec.Same, CompareInput.Judge(Field, " 12 ", "12"));
        Assert.Equal(MetricsSpec.Same, CompareInput.Judge(Field, "12", "\t12 "));
    }

    // 결과 칸이 숫자가 아니면 오류가 아니라 값 불일치다(빈 칸, N/A 포함). 필드 셈 not_number에 든다.
    [Fact]
    public void Candidate_not_number_is_a_mismatch()
    {
        var definition = CompareInput.WithFields(Field);

        foreach (var cell in new[] { "N/A", string.Empty })
        {
            var metrics = CompareInput.Measured(CompareInput.Run(definition, "id,v\nk1,1\nk2,2\n", $"id,v\nk1,{cell}\nk2,2\n"));

            Assert.Equal("ref=2 cand=2 match=1 mismatch=1 missing=0 extra=0 within=0 acc=50.0 miss=0.0", CompareInput.Counts(metrics));
            var field = Assert.Single(metrics.Fields);
            Assert.Equal((2, 0, 0, 1), (field.Compared, field.Outside, field.Within, field.NotNumber));
            var item = Assert.Single(metrics.Items, i => i.Status == CompareEngine.ItemMismatch);
            Assert.Equal(new FieldDifference("v", CompareEngine.ResultNotNumber, "1", cell), Assert.Single(item.Differences));
        }
    }

    // 기준 칸의 빈 칸도 number 오류다. 텍스트 필드(허용 오차 없음)는 숫자를 보지 않는다.
    [Fact]
    public void Reference_empty_number_cell_is_an_error_but_text_field_is_not_checked()
    {
        CompareInput.AssertErrors(Reference(string.Empty), "reference/a.csv:2:3: number v: 숫자가 아니다: ");

        var text = CompareInput.Measured(CompareInput.Run(CompareInput.WithFields(new CompareField("v", null, null)), "id,v\nk,x1\n", "id,v\nk,x1\n"));
        Assert.Equal(CompareEngine.KindText, Assert.Single(text.Fields).Kind);
    }

    // tolerance: 0도 숫자 필드다. 1.5와 1.50이 같다.
    [Fact]
    public void Zero_tolerance_is_a_number_field()
    {
        var metrics = CompareInput.Measured(CompareInput.Run(CompareInput.WithFields(Field), "id,v\nk,1.5\n", "id,v\nk,1.50\n"));

        Assert.Equal(CompareEngine.KindNumber, Assert.Single(metrics.Fields).Kind);
        Assert.Empty(Assert.Single(metrics.Items).Differences);
    }

    // 메시지 안의 값: 제어 문자는 \uXXXX, 64자를 넘으면 자르고 "…"(4.7).
    [Fact]
    public void Value_in_number_message_is_escaped_and_truncated()
    {
        CompareInput.AssertErrors(Reference("x\ty"), "reference/a.csv:2:3: number v: 숫자가 아니다: x\\u0009y");
        CompareInput.AssertErrors(Reference(new string('x', 65)), $"reference/a.csv:2:3: number v: 숫자가 아니다: {new string('x', 64)}…");
    }

    private static CompareResult Reference(string cell) =>
        CompareInput.Run(CompareInput.WithFields(Field), $"id,v\nk,{CompareInput.Quote(cell)}\n", "id,v\nk,1\n");

    private static void Expect(List<string> wrong, string what, string actual, string expected)
    {
        if (actual != expected)
        {
            wrong.Add($"{what}: '{actual}', 표는 '{expected}'");
        }
    }
}
