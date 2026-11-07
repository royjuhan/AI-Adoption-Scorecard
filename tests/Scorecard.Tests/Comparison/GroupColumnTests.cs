using Scorecard.Core.Comparison;
using Scorecard.Tests.Synthetic;

namespace Scorecard.Tests.Comparison;

// 스펙 12.3 inputs.group 열 확인(T-012, judgment.md 질문 7). 기준 파일마다 머리 행에서 그 열을 찾는다.
// 찾는 순서: 키 열 → compare 필드 → group 열. 결과 파일은 보지 않는다. 그 열의 값은 비교하지 않고 metrics.json에 쓰지 않는다.
[Trait("Spec", "metrics")]
public class GroupColumnTests
{
    private const string GroupCase =
        SyntheticInput.OneStepCase + "inputs: {reference: reference/*.csv, candidate: candidate/*.csv, key: id, group: q}\ncompare:\n  - {field: v}\n";

    private static readonly Scorecard.Core.Cases.CaseDefinition Definition = CompareInput.Case(GroupCase);

    private static readonly Scorecard.Core.Cases.CaseDefinition Plain = CompareInput.Case(CompareInput.TextCase);

    // 기준 머리 행에 group 열이 없으면 column(경로 = 열 이름, 위치 = 머리 행 줄). 데이터 행은 보지 않는다.
    [Fact]
    public void Missing_group_column_in_reference_is_a_column_error()
    {
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "id,v\nk1,a\n,b\nk1,c\n", "id,v\nk1,a\n"),
            "reference/a.csv:1: column q: 머리 행에 열이 없다");
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "\nid,v\nk1,a\n", "id,v\nk1,a\n"),
            "reference/a.csv:2: column q: 머리 행에 열이 없다");
    }

    // group 열이 둘 이상이면 duplicate-column(둘째 칸). 메시지는 4.7 표 그대로다.
    [Fact]
    public void Repeated_group_column_is_a_duplicate_column_error()
    {
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "id,q,q,v\nk1,a,b,x\n", "id,v\nk1,x\n"),
            "reference/a.csv:1:6: duplicate-column q: 머리 행에 같은 열이 둘 이상 있다 (처음: 2번째 칸)");
    }

    // 머리 행만 있는 기준 파일도 본다. 다른 기준 파일에 열이 있어도 그 파일마다 본다.
    [Fact]
    public void Header_only_reference_file_is_checked()
    {
        CompareInput.AssertErrors(
            CompareInput.Run(
                Definition,
                [CompareInput.Source("reference/a.csv", "id,q,v\nk1,p,a\n"), CompareInput.Source("reference/b.csv", "id,v\n")],
                [CompareInput.Source("candidate/a.csv", "id,v\nk1,a\n")]),
            "reference/b.csv:1: column q: 머리 행에 열이 없다");
    }

    // 같은 머리 행에서 열이 여럿 없으면 찾은 순서다: 키 열 → compare 필드 → group 열.
    [Fact]
    public void Column_errors_follow_key_then_compare_then_group()
    {
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "a,b\nk1,x\n", "id,v\nk1,a\n"),
            "reference/a.csv:1: column id: 머리 행에 열이 없다",
            "reference/a.csv:1: column v: 머리 행에 열이 없다",
            "reference/a.csv:1: column q: 머리 행에 열이 없다");
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "id,v,q,v,q\nk1,a,p,a,p\n", "id,v\nk1,a\n"),
            "reference/a.csv:1:8: duplicate-column v: 머리 행에 같은 열이 둘 이상 있다 (처음: 2번째 칸)",
            "reference/a.csv:1:10: duplicate-column q: 머리 행에 같은 열이 둘 이상 있다 (처음: 3번째 칸)");
    }

    // 결과 파일은 group 열을 보지 않는다. 없어도, 둘이어도 오류가 아니다. 결과 쪽 키·compare 열은 지금처럼 본다.
    [Fact]
    public void Candidate_files_are_not_checked_for_group()
    {
        Assert.Equal(
            "ref=1 cand=1 match=1 mismatch=0 missing=0 extra=0 within=0 acc=100.0 miss=0.0",
            CompareInput.Counts(CompareInput.Run(Definition, "id,q,v\nk1,p,a\n", "id,v\nk1,a\n")));
        Assert.Equal(
            "ref=1 cand=1 match=1 mismatch=0 missing=0 extra=0 within=0 acc=100.0 miss=0.0",
            CompareInput.Counts(CompareInput.Run(Definition, "id,q,v\nk1,p,a\n", "id,q,q,v\nk1,r,s,a\n")));
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "id,q,v\nk1,p,a\n", "id,q\nk1,p\n"),
            "candidate/a.csv:1: column v: 머리 행에 열이 없다");
    }

    // group 열 값은 비교하지 않는다(결과 쪽 값이 달라도 일치). 메모리 CsvRow에는 그 칸이 있다(판정이 읽는다, judgment.md 11.8).
    [Fact]
    public void Group_values_are_kept_in_rows_but_not_compared()
    {
        var metrics = CompareInput.Measured(CompareInput.Run(Definition, "id,q,v\nk1, p ,a\n", "id,q,v\nk1,other,a\n"));

        var item = Assert.Single(metrics.Items);
        Assert.Equal(CompareEngine.ItemMatch, item.Status);
        Assert.Equal(["id", "q", "v"], item.Reference!.Header);
        Assert.Equal(["k1", "p", "a"], item.Reference.Cells);
    }

    // metrics.json은 group이 있어도 없어도 바이트가 같다(12.4, 속성이 없다).
    [Fact]
    public void Metrics_json_is_the_same_with_or_without_group()
    {
        const string Reference = "id,q,v\nk1,p,a\nk2,p,b\nk3,r,c\n";
        const string Candidate = "id,v\nk1,a\nk2,x\nk4,d\n";

        var withGroup = MetricsJson.Render(CompareInput.Measured(CompareInput.Run(Definition, Reference, Candidate)));
        var without = MetricsJson.Render(CompareInput.Measured(CompareInput.Run(Plain, Reference, Candidate)));

        Assert.Equal(without, withGroup);
        Assert.DoesNotContain("\"q\"", CompareInput.Text(withGroup), StringComparison.Ordinal);
    }
}
