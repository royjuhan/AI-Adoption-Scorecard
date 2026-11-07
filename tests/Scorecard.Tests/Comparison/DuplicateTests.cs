using System.Security.Cryptography;
using System.Text;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Comparison;

// 스펙 12.2 결과 쪽 키 중복 = 항목 duplicate(T-012, judgment.md 질문 2). 메모리 바이트로 대조한다(8절).
// 기준 쪽 중복·빈 키는 그대로 오류다(KeyTests, MultiFileTests).
[Trait("Spec", "metrics")]
public class DuplicateTests
{
    private static readonly CaseDefinition Definition = CompareInput.Case(CompareInput.TextCase);

    // 결과 쪽 같은 키는 첫 행으로 대조한다. 첫 행이 다르고 둘째 행이 같아도 값 불일치다.
    // 둘째 행은 duplicate이고 값을 대조하지 않는다(differences []). reference는 그 키의 기준 행 위치다.
    [Fact]
    public void First_candidate_row_is_compared_and_later_rows_are_duplicates()
    {
        var metrics = CompareInput.Measured(CompareInput.Run(Definition, "id,v\nk1,a\nk2,b\n", "id,v\nk1,a\nk2,x\nk2,b\n"));

        Assert.Equal(
            ["k1 match r2 c2 0", "k2 mismatch r3 c3 1", "k2 duplicate r3 c4 0"],
            metrics.Items.Select(Show));
        var duplicate = Assert.Single(metrics.Items, i => i.Status == CompareEngine.ItemDuplicate);
        Assert.Equal((CompareInput.ReferencePath, CompareInput.CandidatePath), (duplicate.Reference!.File, duplicate.Candidate!.File));
        Assert.Equal("ref=2 cand=3 match=1 mismatch=1 missing=0 extra=0 within=0 acc=50.0 miss=0.0", CompareInput.Counts(metrics));
    }

    // 둘째 행 값이 기준과 달라도 duplicate다. fields[]는 첫 행만 센다(compared = 일치 + 값 불일치).
    [Fact]
    public void Duplicate_row_values_are_not_compared()
    {
        var metrics = CompareInput.Measured(CompareInput.Run(Definition, "id,v\nk1,a\n", "id,v\nk1,a\nk1,zzz\n"));

        Assert.Equal(["k1 match r2 c2 0", "k1 duplicate r2 c3 0"], metrics.Items.Select(Show));
        var field = Assert.Single(metrics.Fields);
        Assert.Equal((1, 0, 0, 0), (field.Compared, field.Outside, field.Within, field.NotNumber));
    }

    // 기준에 없는 키가 결과에 두 번이면 첫 행은 extra, 둘째 행부터 duplicate(reference null).
    [Fact]
    public void Extra_key_repeated_gives_extra_then_duplicate()
    {
        var metrics = CompareInput.Measured(CompareInput.Run(Definition, "id,v\nk1,a\n", "id,v\nx1,p\nk1,a\nx1,q\n"));

        Assert.Equal(["k1 match r2 c3 0", "x1 extra - c2 0", "x1 duplicate - c4 0"], metrics.Items.Select(Show));
        Assert.Equal("ref=1 cand=3 match=1 mismatch=0 missing=0 extra=1 within=0 acc=100.0 miss=0.0", CompareInput.Counts(metrics));
    }

    // 셋째 행도 하나씩 duplicate다. 항목은 둘째 행부터 하나씩이다.
    [Fact]
    public void Each_repeat_is_its_own_duplicate_item()
    {
        var metrics = CompareInput.Measured(CompareInput.Run(Definition, "id,v\nk1,a\n", "id,v\nk1,a\nk1,a\n k1 ,a\n"));

        Assert.Equal(["k1 match r2 c2 0", "k1 duplicate r2 c3 0", "k1 duplicate r2 c4 0"], metrics.Items.Select(Show));
        Assert.Equal((1, 3), (metrics.Counts!.Reference, metrics.Counts.Candidate));
    }

    // 항목 순서: 기준 항목(기준 경로 서수 → 줄), 그다음 결과 쪽 extra·duplicate(결과 경로 서수 → 줄).
    // 중복은 결과 파일을 합쳐서 본다. 첫 행은 경로 서수로 앞 파일의 행이다. 넘긴 파일 순서와 상관없다.
    [Fact]
    public void Duplicates_across_candidate_files_follow_path_order()
    {
        var reference = new[] { CompareInput.Source("reference/a.csv", "id,v\nk1,a\nk2,b\nk3,c\n") };
        var a = CompareInput.Source("candidate/a.csv", "id,v\nk2,b\nx1,p\n");
        var b = CompareInput.Source("candidate/b.csv", "id,v\nk1,a\nk2,zz\nx1,p\nk3,c\n");
        string[] expected =
        [
            "k1 match reference/a.csv:2 candidate/b.csv:2",
            "k2 match reference/a.csv:3 candidate/a.csv:2",
            "k3 match reference/a.csv:4 candidate/b.csv:5",
            "x1 extra - candidate/a.csv:3",
            "k2 duplicate reference/a.csv:3 candidate/b.csv:3",
            "x1 duplicate - candidate/b.csv:4",
        ];

        foreach (var candidate in new[] { new[] { a, b }, [b, a] })
        {
            var metrics = CompareInput.Measured(CompareInput.Run(Definition, reference, candidate));

            Assert.Equal(expected, metrics.Items.Select(i => $"{i.Key} {i.Status} {Where(i.Reference)} {Where(i.Candidate)}"));
            Assert.Equal(["candidate/a.csv", "candidate/b.csv"], metrics.CandidateFiles);
        }
    }

    // counts.candidate는 결과 데이터 행 전부다(일치 + 값 불일치 + 추가 + 중복 행). 정확도·누락률·다른 셈은 중복이 없을 때와 같다.
    [Fact]
    public void Duplicates_count_as_candidate_rows_only()
    {
        const string Reference = "id,v\nk1,a\nk2,b\nk3,c\n";
        var plain = CompareInput.Measured(CompareInput.Run(Definition, Reference, "id,v\nk1,a\nk2,x\nx1,p\n"));
        var repeated = CompareInput.Measured(CompareInput.Run(Definition, Reference, "id,v\nk1,a\nk2,x\nx1,p\nk1,a\nk2,b\nx1,p\n"));

        Assert.Equal("ref=3 cand=3 match=1 mismatch=1 missing=1 extra=1 within=0 acc=33.3 miss=33.3", CompareInput.Counts(plain));
        Assert.Equal("ref=3 cand=6 match=1 mismatch=1 missing=1 extra=1 within=0 acc=33.3 miss=33.3", CompareInput.Counts(repeated));
        Assert.Equal(3, repeated.Items.Count(i => i.Status == CompareEngine.ItemDuplicate));
        Assert.Equal(repeated.Counts!.Candidate, repeated.Counts.Match + repeated.Counts.Mismatch + repeated.Counts.Extra + repeated.Items.Count(i => i.Status == CompareEngine.ItemDuplicate));
        Assert.Equal(plain.Fields, repeated.Fields);
    }

    // 기준 쪽 중복은 그대로 duplicate-key다. 빈 키는 결과 쪽도 그대로 empty-key다(12.2). 빈 키 뒤 결과 쪽 중복은 오류가 아니다.
    [Fact]
    public void Reference_side_duplicates_and_empty_keys_stay_errors()
    {
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "id,v\nk1,a\nk1,a\n", "id,v\nk1,a\nk1,a\n"),
            "reference/a.csv:3:1: duplicate-key id: 키가 중복된다: k1 (처음: reference/a.csv:2)");
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "id,v\nk1,a\n", "id,v\nk1,a\n,a\nk1,a\n"),
            "candidate/a.csv:3:1: empty-key id: 키가 비어 있다");
    }

    // 12.5 compare-duplicate: 블록 세 파일을 메모리 바이트로 넘겨 대조하면 metrics.json이 블록·SHA-256과 바이트까지 같다.
    [Fact]
    public void Compare_duplicate_sample_matches_spec_block()
    {
        var bytes = MetricsJson.Render(CompareInput.Measured(CompareDuplicate.Run()));

        Assert.Equal(CompareDuplicate.MetricsBlock(), CompareInput.Text(bytes));
        Assert.Equal(CompareDuplicate.MetricsHash(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
        Assert.Equal(CompareDuplicate.MetricsHash(), Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(CompareDuplicate.MetricsBlock()))));
    }

    // 12.5 글머리: r2 첫 행 값 불일치·둘째 행 duplicate, r5 extra·duplicate(reference null), r4 누락. 결과 6 = 2 + 1 + 1 + 2.
    [Fact]
    public void Compare_duplicate_sample_items_follow_spec_bullets()
    {
        var metrics = CompareInput.Measured(CompareDuplicate.Run());

        Assert.Equal(
            ["r1 match r2 c2 0", "r2 mismatch r3 c3 1", "r3 match r4 c6 0", "r4 missing r5 - 0", "r2 duplicate r3 c4 0", "r5 extra - c5 0", "r5 duplicate - c7 0"],
            metrics.Items.Select(Show));
        Assert.Equal("ref=4 cand=6 match=2 mismatch=1 missing=1 extra=1 within=0 acc=50.0 miss=25.0", CompareInput.Counts(metrics));
        Assert.Equal("kind", CompareDuplicate.Definition().Inputs!.Group);
    }

    // "키 상태 r<기준 줄> c<결과 줄> 차이 수". 위치가 없으면 -.
    private static string Show(CompareItem item) =>
        $"{item.Key} {item.Status} {(item.Reference is { } r ? "r" + r.Line : "-")} {(item.Candidate is { } c ? "c" + c.Line : "-")} {item.Differences.Count}";

    private static string Where(CsvRow? row) => row is null ? "-" : $"{row.File}:{row.Line}";
}

// 스펙 metrics.md 12.5 compare-duplicate 표본(합성). 블록을 메모리 바이트로 넘긴다. judgment.md 11.9 판정 블록도 이 표본이다.
internal static class CompareDuplicate
{
    public const string CaseLinePrefix = "`compare-duplicate/case.yaml`:";
    public const string ReferenceLinePrefix = "`compare-duplicate/reference/rows.csv`:";
    public const string CandidateLinePrefix = "`compare-duplicate/candidate/rows.csv`:";
    public const string MetricsLinePrefix = "`compare-duplicate` metrics.json (SHA-256 ";

    public const string ReferencePath = "reference/rows.csv";
    public const string CandidatePath = "candidate/rows.csv";

    public static CaseDefinition Definition() => CompareInput.Case(SpecDocument.BlockAfter(MetricsSpec.Document, CaseLinePrefix));

    public static CompareSources Sources() =>
        new(
            [CompareInput.Source(ReferencePath, SpecDocument.BlockAfter(MetricsSpec.Document, ReferenceLinePrefix))],
            [CompareInput.Source(CandidatePath, SpecDocument.BlockAfter(MetricsSpec.Document, CandidateLinePrefix))]);

    public static CompareResult Run() => CompareEngine.Compare(Definition(), Sources());

    public static string MetricsBlock() => SpecDocument.BlockAfter(MetricsSpec.Document, MetricsLinePrefix);

    // 줄의 마지막 `코드`가 SHA-256이다(첫 `코드`는 표본 이름).
    public static string MetricsHash() => SpecDocument.CodeSpans(SpecDocument.Line(MetricsSpec.Document, MetricsLinePrefix))[^1];
}
