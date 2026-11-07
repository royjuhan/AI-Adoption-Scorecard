using Scorecard.Core.Comparison;

namespace Scorecard.Tests.Comparison;

// 스펙 4.1 여러 파일(승인 질문 3), 3.1 항목 순서. 메모리 바이트로 Compare를 부른다.
// 한쪽 파일의 데이터 행을 합친다(경로 서수 → 줄). 파일마다 머리 행이 있고 열 순서는 달라도 된다.
// 키 중복은 쪽 전체에서 본다. 같은 파일이 양쪽에 나오면 overlap.
[Trait("Spec", "metrics")]
public class MultiFileTests
{
    // 파일 목록은 경로 서수 순. 항목은 기준 항목(기준 파일 경로 → 줄), 그다음 추가 항목(결과 파일 경로 → 줄).
    // 넘긴 파일 순서와 상관없다.
    [Fact]
    public void Rows_are_merged_in_path_then_line_order()
    {
        var metrics = CompareInput.Measured(CompareInput.Run(
            CompareInput.Case(CompareInput.TextCase),
            [CompareInput.Source("reference/b.csv", "id,v\nk3,c\n"), CompareInput.Source("reference/a.csv", "id,v\nk1,a\nk2,b\n")],
            [CompareInput.Source("candidate/b.csv", "id,v\nk8,y\n"), CompareInput.Source("candidate/a.csv", "id,v\nk9,z\nk2,x\n")]));

        Assert.Equal(["reference/a.csv", "reference/b.csv"], metrics.ReferenceFiles);
        Assert.Equal(["candidate/a.csv", "candidate/b.csv"], metrics.CandidateFiles);
        Assert.Equal(
            [
                "k1 missing reference/a.csv:2 -",
                "k2 mismatch reference/a.csv:3 candidate/a.csv:3",
                "k3 missing reference/b.csv:2 -",
                "k9 extra - candidate/a.csv:2",
                "k8 extra - candidate/b.csv:2",
            ],
            metrics.Items.Select(i => $"{i.Key} {i.Status} {Where(i.Reference)} {Where(i.Candidate)}"));
        Assert.Equal("ref=3 cand=3 match=0 mismatch=1 missing=2 extra=2 within=0 acc=0.0 miss=66.7", CompareInput.Counts(metrics));
    }

    // 키 중복은 같은 쪽 모든 파일을 합쳐서 본다. 메시지의 처음 위치는 앞 파일이다.
    [Fact]
    public void Duplicate_key_across_files_is_reported()
    {
        CompareInput.AssertErrors(
            CompareInput.Run(
                CompareInput.Case(CompareInput.TextCase),
                [CompareInput.Source("reference/a.csv", "id,v\nk1,a\nk2,b\n"), CompareInput.Source("reference/b.csv", "id,v\nk3,c\nk2,d\n")],
                [CompareInput.Source("candidate/a.csv", "id,v\nk1,a\n")]),
            "reference/b.csv:3:1: duplicate-key id: 키가 중복된다: k2 (처음: reference/a.csv:3)");
    }

    // 열 순서는 파일마다 달라도 된다. 쓰지 않는 열이 더 있어도 된다.
    [Fact]
    public void Column_order_may_differ_between_files()
    {
        var metrics = CompareInput.Measured(CompareInput.Run(
            CompareInput.Case(CompareInput.NumberAndTextCase),
            [CompareInput.Source("reference/a.csv", "id,n,t\nk1,1,a\n"), CompareInput.Source("reference/b.csv", "t,n,id,extra\nb,2,k2,z\n")],
            [CompareInput.Source("candidate/a.csv", "id,n,t\nk1,1,a\nk2,2,b\n")]));

        Assert.Equal("ref=2 cand=2 match=2 mismatch=0 missing=0 extra=0 within=0 acc=100.0 miss=0.0", CompareInput.Counts(metrics));
    }

    // 같은 파일이 기준·결과 양쪽에 나오면 overlap(둘째 파일). 위치는 그 파일이고 줄·열은 없다.
    [Fact]
    public void Same_file_on_both_sides_is_overlap()
    {
        CompareInput.AssertErrors(
            CompareInput.Run(
                CompareInput.Case(CompareInput.TextCase),
                [CompareInput.Source("shared/a.csv", "id,v\nk1,a\n")],
                [CompareInput.Source("shared/a.csv", "id,v\nk1,a\n")]),
            "shared/a.csv: overlap: 같은 파일이 두 번 나온다(기준·결과 패턴이 겹친다)");
    }

    private static string Where(CsvRow? row) => row is null ? "-" : $"{row.File}:{row.Line}";
}
