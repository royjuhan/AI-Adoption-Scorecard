using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;

namespace Scorecard.Tests.Comparison;

// 스펙 4.4 항목과 셈(승인 질문 7). T-005 P2: 항목 = 기준 행, 정확도 = 일치 / 기준 행, 누락률 = 누락 / 기준 행.
// 추가는 분모 밖이고 counts.extra로만. 값 불일치 = 필드 하나 이상이 outside·not_number.
// 허용오차 안 차이는 일치로 세고 counts.within으로 따로. 필드별 셈은 칸 수다.
[Trait("Spec", "metrics")]
public class ItemCountTests
{
    // 숫자 a(tolerance 1), 백분율 b(1%), 텍스트 c.
    private static readonly CaseDefinition Definition = CompareInput.WithFields(
        new CompareField("a", 1m, null),
        new CompareField("b", null, 1.0m),
        new CompareField("c", null, null));

    private const string Reference =
        "id,a,b,c,note\n" +
        "k1,10,100,x,n1\n" + // 같음
        "k2,10,100,x,n2\n" + // a within → 일치(허용오차 안 차이)
        "k3,10,100,x,n3\n" + // a within, b outside → 값 불일치
        "k4,10,100,x,n4\n" + // a not_number, c outside → 값 불일치
        "k5,10,100,x,n5\n";  // 누락

    private const string Candidate =
        "id,a,b,c,note\n" +
        "k1,10,100,x,other\n" +
        "k2,11,100,x,n2\n" +
        "k3,10.5,102,x,n3\n" +
        "k4,N/A,100,y,n4\n" +
        "k6,1,1,z,n6\n" + // 추가
        "k7,1,1,z,n7\n";  // 추가

    [Fact]
    public void Counts_follow_reference_row_definition()
    {
        var metrics = Measure();

        Assert.Equal(new CompareCounts(Reference: 5, Candidate: 6, Match: 2, Mismatch: 2, Missing: 1, Extra: 2, Within: 1), metrics.Counts);
        Assert.Equal((decimal?)40.0m, metrics.AccuracyPct);
        Assert.Equal((decimal?)20.0m, metrics.MissingRatePct);
        Assert.Equal(metrics.Counts!.Reference, metrics.Counts.Match + metrics.Counts.Mismatch + metrics.Counts.Missing);
        Assert.Equal(metrics.Counts.Candidate, metrics.Counts.Match + metrics.Counts.Mismatch + metrics.Counts.Extra);
    }

    // 필드별 셈: compared = 일치 + 값 불일치. outside·within·not_number는 칸 수(값 불일치 항목 안의 within 칸도 센다).
    // kind는 허용 오차가 있으면 number, 없으면 text. 허용 오차 값은 case.yaml 값 그대로다.
    [Fact]
    public void Field_summaries_count_cells()
    {
        Assert.Equal(
            [
                new FieldSummary("a", CompareEngine.KindNumber, 1m, null, Compared: 4, Outside: 0, Within: 2, NotNumber: 1),
                new FieldSummary("b", CompareEngine.KindNumber, null, 1.0m, Compared: 4, Outside: 1, Within: 0, NotNumber: 0),
                new FieldSummary("c", CompareEngine.KindText, null, null, Compared: 4, Outside: 1, Within: 0, NotNumber: 0),
            ],
            Measure().Fields);
    }

    // 메모리 결과 Items에는 모든 항목이 있다(차이 없는 일치 포함). 순서는 기준 항목, 그다음 추가 항목.
    // 차이는 compare 순서이고 차이가 있는 필드만. 누락은 결과 쪽이 null, 추가는 기준 쪽이 null, 둘 다 차이가 없다.
    [Fact]
    public void Items_hold_every_item_with_differences_in_compare_order()
    {
        var items = Measure().Items;

        Assert.Equal(
            [
                "k1 match",
                "k2 match a:within:10->11",
                "k3 mismatch a:within:10->10.5 b:outside:100->102",
                "k4 mismatch a:not_number:10->N/A c:outside:x->y",
                "k5 missing",
                "k6 extra",
                "k7 extra",
            ],
            items.Select(i => string.Join(" ", new[] { i.Key, i.Status }.Concat(i.Differences.Select(d => $"{d.Field}:{d.Result}:{d.Reference}->{d.Candidate}")))));
        Assert.Null(items[4].Candidate);
        Assert.NotNull(items[4].Reference);
        Assert.Null(items[5].Reference);
        Assert.NotNull(items[5].Candidate);
    }

    // compare에 없는 열(note)은 비교하지 않는다. 메모리 CsvRow에는 남는다(판정 카드가 쓸 수 있다, 승인 질문 16).
    [Fact]
    public void Columns_outside_compare_are_kept_in_rows_but_not_compared()
    {
        var first = Measure().Items[0];

        Assert.Equal(CompareEngine.ItemMatch, first.Status);
        Assert.Empty(first.Differences);
        Assert.Equal(("reference/a.csv", 2), (first.Reference!.File, first.Reference.Line));
        Assert.Equal(["id", "a", "b", "c", "note"], first.Reference.Header);
        Assert.Equal(["k1", "10", "100", "x", "n1"], first.Reference.Cells);
        Assert.Equal(["k1", "10", "100", "x", "other"], first.Candidate!.Cells);
        Assert.Equal(("candidate/a.csv", 2), (first.Candidate.File, first.Candidate.Line));
    }

    // 추가 행은 분모에 들지 않는다. 기준 2행이 모두 일치하면 추가가 몇 행이어도 100.0이다.
    [Fact]
    public void Extra_rows_are_outside_the_denominator()
    {
        var metrics = CompareInput.Measured(CompareInput.Run(
            CompareInput.Case(CompareInput.TextCase),
            "id,v\nk1,a\nk2,b\n",
            "id,v\nk1,a\nk2,b\nk3,c\nk4,d\nk5,e\n"));

        Assert.Equal("ref=2 cand=5 match=2 mismatch=0 missing=0 extra=3 within=0 acc=100.0 miss=0.0", CompareInput.Counts(metrics));
    }

    private static CompareMetrics Measure() => CompareInput.Measured(CompareInput.Run(Definition, Reference, Candidate));
}
