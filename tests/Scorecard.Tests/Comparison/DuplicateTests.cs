using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Judgment;

namespace Scorecard.Tests.Comparison;

// 스펙 4.3·12.2 결과 쪽 키 중복 = 항목 duplicate(T-012, judgment.md 질문 2). 메모리 바이트로 대조한다(8절).
// T-013(13.2): 결과 쪽에 두 번 이상 나온 기준 키는 값 불일치다. 대표 행(첫 행)과 duplicate 행 모두 기준과 대조해 차이를 적는다.
// 셈(counts·fields[])은 결과 행의 줄 순서와 상관없다(13.3 표). 기준 쪽 중복·빈 키는 그대로 오류다(KeyTests, MultiFileTests).
[Trait("Spec", "metrics")]
public class DuplicateTests
{
    private static readonly CaseDefinition Definition = CompareInput.Case(CompareInput.TextCase);

    // 13.3 줄 순서 표의 행 번호.
    public static TheoryData<int> LineOrderRows => new(Enumerable.Range(0, MetricsSpec.Table(MetricsSpec.LineOrderHeader).Count));

    // 결과 쪽 같은 키의 첫 행이 대표 행이다. 그 키는 값 불일치다(13.2). 기준 항목에는 대표 행의 차이를 적는다.
    // 둘째 행은 duplicate이고 그 행과 기준 행의 차이를 적는다(여기서는 같은 값이라 []). reference는 그 키의 기준 행 위치다.
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

    // T-013(13.2): 둘째 행도 기준과 대조한다. 이름은 T-012 때 것이다(이름을 바꾸면 빠진 테스트로 센다). 뜻이 바뀌었다.
    // 대표 행이 기준과 같아도 그 키는 값 불일치이고 차이는 [], duplicate 항목에 둘째 행의 차이(a → zzz)를 적는다.
    // fields[]는 기준에 키가 있는 결과 행마다 센다(compared 2, outside 1).
    [Fact]
    public void Duplicate_row_values_are_not_compared()
    {
        var metrics = CompareInput.Measured(CompareInput.Run(Definition, "id,v\nk1,a\n", "id,v\nk1,a\nk1,zzz\n"));

        Assert.Equal(["k1 mismatch r2 c2 0", "k1 duplicate r2 c3 1"], metrics.Items.Select(Show));
        var difference = Assert.Single(Assert.Single(metrics.Items, i => i.Status == CompareEngine.ItemDuplicate).Differences);
        Assert.Equal(("v", CompareEngine.ResultOutside, "a", "zzz"), (difference.Field, difference.Result, difference.Reference, difference.Candidate));
        var field = Assert.Single(metrics.Fields);
        Assert.Equal((2, 1, 0, 0), (field.Compared, field.Outside, field.Within, field.NotNumber));
        Assert.Equal("ref=1 cand=2 match=0 mismatch=1 missing=0 extra=0 within=0 acc=0.0 miss=0.0", CompareInput.Counts(metrics));
    }

    // 기준에 없는 키가 결과에 두 번이면 첫 행은 extra, 둘째 행부터 duplicate(reference null).
    [Fact]
    public void Extra_key_repeated_gives_extra_then_duplicate()
    {
        var metrics = CompareInput.Measured(CompareInput.Run(Definition, "id,v\nk1,a\n", "id,v\nx1,p\nk1,a\nx1,q\n"));

        Assert.Equal(["k1 match r2 c3 0", "x1 extra - c2 0", "x1 duplicate - c4 0"], metrics.Items.Select(Show));
        Assert.Equal("ref=1 cand=3 match=1 mismatch=0 missing=0 extra=1 within=0 acc=100.0 miss=0.0", CompareInput.Counts(metrics));
    }

    // 셋째 행도 하나씩 duplicate다. 항목은 둘째 행부터 하나씩이다. 모든 행이 기준과 같아도 그 키는 값 불일치다(T-013, 13.2).
    // compared는 기준에 키가 있는 결과 행 셋이다.
    [Fact]
    public void Each_repeat_is_its_own_duplicate_item()
    {
        var metrics = CompareInput.Measured(CompareInput.Run(Definition, "id,v\nk1,a\n", "id,v\nk1,a\nk1,a\n k1 ,a\n"));

        Assert.Equal(["k1 mismatch r2 c2 0", "k1 duplicate r2 c3 0", "k1 duplicate r2 c4 0"], metrics.Items.Select(Show));
        Assert.Equal((1, 3), (metrics.Counts!.Reference, metrics.Counts.Candidate));
        Assert.Equal((0, 1), (metrics.Counts.Match, metrics.Counts.Mismatch));
        Assert.Equal((3, 0), (Assert.Single(metrics.Fields).Compared, metrics.Fields[0].Outside));
    }

    // 항목 순서: 기준 항목(기준 경로 서수 → 줄), 그다음 결과 쪽 extra·duplicate(결과 경로 서수 → 줄).
    // 중복은 결과 파일을 합쳐서 본다. 대표 행은 경로 서수로 앞 파일의 행이다. 넘긴 파일 순서와 상관없다.
    // T-013(13.2): k2는 결과 쪽에 두 번 나와 값 불일치다. 대표 행(a.csv, b)은 기준과 같아 차이가 없고, duplicate(b.csv, zz)에 차이 하나.
    [Fact]
    public void Duplicates_across_candidate_files_follow_path_order()
    {
        var reference = new[] { CompareInput.Source("reference/a.csv", "id,v\nk1,a\nk2,b\nk3,c\n") };
        var a = CompareInput.Source("candidate/a.csv", "id,v\nk2,b\nx1,p\n");
        var b = CompareInput.Source("candidate/b.csv", "id,v\nk1,a\nk2,zz\nx1,p\nk3,c\n");
        string[] expected =
        [
            "k1 match reference/a.csv:2 candidate/b.csv:2 0",
            "k2 mismatch reference/a.csv:3 candidate/a.csv:2 0",
            "k3 match reference/a.csv:4 candidate/b.csv:5 0",
            "x1 extra - candidate/a.csv:3 0",
            "k2 duplicate reference/a.csv:3 candidate/b.csv:3 1",
            "x1 duplicate - candidate/b.csv:4 0",
        ];

        foreach (var candidate in new[] { new[] { a, b }, [b, a] })
        {
            var metrics = CompareInput.Measured(CompareInput.Run(Definition, reference, candidate));

            Assert.Equal(expected, metrics.Items.Select(i => $"{i.Key} {i.Status} {Where(i.Reference)} {Where(i.Candidate)} {i.Differences.Count}"));
            Assert.Equal(["candidate/a.csv", "candidate/b.csv"], metrics.CandidateFiles);
            Assert.Equal("ref=3 cand=6 match=2 mismatch=1 missing=0 extra=1 within=0 acc=66.7 miss=0.0", CompareInput.Counts(metrics));
            Assert.Equal((4, 1), (Assert.Single(metrics.Fields).Compared, metrics.Fields[0].Outside));
        }
    }

    // counts.candidate는 결과 데이터 행 전부다(일치 + 값 불일치 + 추가 + 중복 행). 중복 행은 분모(기준 행)에 들지 않는다:
    // 기준 행·누락·추가 수는 중복이 없을 때와 같다. T-013(13.2): 결과 쪽에 두 번 나온 기준 키(k1)는 맞는 행만 있어도 값 불일치이고,
    // fields[].compared는 기준에 키가 있는 결과 행 수다(= 결과 행 − 추가 − 기준에 없는 키의 중복 행).
    [Fact]
    public void Duplicates_count_as_candidate_rows_only()
    {
        const string Reference = "id,v\nk1,a\nk2,b\nk3,c\n";
        var plain = CompareInput.Measured(CompareInput.Run(Definition, Reference, "id,v\nk1,a\nk2,x\nx1,p\n"));
        var repeated = CompareInput.Measured(CompareInput.Run(Definition, Reference, "id,v\nk1,a\nk2,x\nx1,p\nk1,a\nk2,b\nx1,p\n"));

        Assert.Equal("ref=3 cand=3 match=1 mismatch=1 missing=1 extra=1 within=0 acc=33.3 miss=33.3", CompareInput.Counts(plain));
        Assert.Equal("ref=3 cand=6 match=0 mismatch=2 missing=1 extra=1 within=0 acc=0.0 miss=33.3", CompareInput.Counts(repeated));
        var duplicates = repeated.Items.Where(i => i.Status == CompareEngine.ItemDuplicate).ToArray();
        Assert.Equal(3, duplicates.Length);
        Assert.Equal(repeated.Counts!.Candidate, repeated.Counts.Match + repeated.Counts.Mismatch + repeated.Counts.Extra + duplicates.Length);
        var unreferenced = duplicates.Count(i => i.Reference is null);
        Assert.Equal(1, unreferenced);
        Assert.Equal((2, 1), (Assert.Single(plain.Fields).Compared, plain.Fields[0].Outside));
        Assert.Equal((4, 1), (Assert.Single(repeated.Fields).Compared, repeated.Fields[0].Outside));
        Assert.Equal(repeated.Counts.Candidate - repeated.Counts.Extra - unreferenced, repeated.Fields[0].Compared);
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

    // 13.3 줄 순서 표: 12.5 compare-duplicate에서 결과 파일 r2 두 행(3·4줄)의 value만 표 첫 칸 값으로 바꾼다(T-013 카드 수용 기준 1).
    // 행마다 r2 기준 항목은 늘 mismatch이고, 기준 항목·중복 항목의 차이와 일치·값 불일치, value의 compared·outside가 표와 같다.
    // 그 밖의 셈(기준·결과 행, 누락, 추가, 허용오차 안 차이, 정확도, 누락률)은 12.5 블록과 같다(13.3 둘째 글머리).
    // 판정은 judgment.md 6.4 compare-duplicate 블록과 바이트까지 같다.
    [Theory]
    [MemberData(nameof(LineOrderRows))]
    public void Line_order_table_row_gives_its_counts_and_differences(int index)
    {
        var c = MetricsSpec.Table(MetricsSpec.LineOrderHeader)[index];
        var values = SpecDocument.CodeSpans(c[0]);
        Assert.Equal(2, values.Length);
        var definition = CompareDuplicate.Definition();

        var metrics = CompareInput.Measured(CompareEngine.Compare(definition, CompareDuplicate.SourcesWithR2(values[0], values[1])));

        var reference = Assert.Single(metrics.Items, i => i.Key == "r2" && i.Status != CompareEngine.ItemDuplicate);
        var duplicate = Assert.Single(metrics.Items, i => i.Key == "r2" && i.Status == CompareEngine.ItemDuplicate);
        Assert.Equal(CompareEngine.ItemMismatch, reference.Status);
        Assert.Equal((3, 4), (reference.Candidate!.Line, duplicate.Candidate!.Line));
        Assert.Equal((3, 3), (reference.Reference!.Line, duplicate.Reference!.Line));
        Assert.Equal(LineOrderDifferences(c[1]), reference.Differences.Select(ShowDifference));
        Assert.Equal(LineOrderDifferences(c[2]), duplicate.Differences.Select(ShowDifference));
        Assert.Equal((MetricsSpec.Int(c[3]), MetricsSpec.Int(c[4])), (metrics.Counts!.Match, metrics.Counts.Mismatch));
        var field = Assert.Single(metrics.Fields);
        Assert.Equal((MetricsSpec.Int(c[5]), MetricsSpec.Int(c[6])), (field.Compared, field.Outside));
        Assert.Equal(CompareDuplicate.BlockOtherCounts(), OtherCounts(metrics));

        var judgment = JudgmentJson.Render(JudgmentEngine.Judge(definition, metrics));
        Assert.Equal(SpecDocument.BlockAfter(JudgmentSpec.Document, JudgmentSpec.CompareDuplicateLinePrefix), CompareInput.Text(judgment));
    }

    // 13.3 표 모양(빈 통과 방지): 첫 행이 12.5 블록의 r2 두 행이다. T-012 리뷰 지적 1의 장면(맞는 행이 앞, 다른 값은 중복 항목에만)이 있다.
    // 같은 두 값을 줄 순서만 바꾼 행들은 셈 칸(일치·값 불일치·compared·outside)이 같다. 일치·값 불일치·compared는 넷 모두 같다.
    // 판정 블록은 judgment.md 6.4 제목 아래 블록 하나다.
    [Fact]
    public void Line_order_table_covers_the_review_scene()
    {
        var rows = MetricsSpec.Table(MetricsSpec.LineOrderHeader);
        var values = rows.Select(r => SpecDocument.CodeSpans(r[0])).ToArray();

        Assert.Equal(CompareDuplicate.BlockR2Values(), values[0]);
        Assert.Contains(rows, r => r[1].Trim() == MetricsSpec.None && r[2].Trim() != MetricsSpec.None);
        Assert.Contains(rows, r => r[1].Trim() != MetricsSpec.None && r[2].Trim() != MetricsSpec.None);
        Assert.Contains(rows, r => r[1].Trim() == MetricsSpec.None && r[2].Trim() == MetricsSpec.None);
        var swapped = rows.Select((r, i) => (Values: string.Join(",", values[i].Order(StringComparer.Ordinal)), Counts: string.Join("|", r[3..].Select(MetricsSpec.Cell))))
            .GroupBy(r => r.Values, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .ToArray();
        Assert.NotEmpty(swapped);
        Assert.All(swapped, g => Assert.Single(g.Select(r => r.Counts).Distinct(StringComparer.Ordinal)));
        Assert.Single(rows.Select(r => string.Join("|", new[] { r[3], r[4], r[5] }.Select(MetricsSpec.Cell))).Distinct(StringComparer.Ordinal));
        Assert.Equal(
            SpecDocument.BlockAfter(JudgmentSpec.Document, JudgmentSpec.CompareDuplicateLinePrefix),
            Assert.Single(SpecDocument.CodeBlocks(JudgmentSpec.Document, JudgmentSpec.ReviewHeading)));
    }

    // 13.3 셋째 글머리: 기준에 없는 r5 두 행(5·7줄)의 값을 서로 바꿔도 extra 1 + duplicate 1, 둘 다 차이 []다.
    // 줄 번호도 그대로라 metrics.json이 12.5 블록과 바이트까지 같다.
    [Fact]
    public void Swapping_values_of_an_extra_key_changes_nothing()
    {
        var lines = CompareDuplicate.CandidateLines();
        Assert.Equal(["r5,50", "r5,55"], new[] { lines[4], lines[6] });
        (lines[4], lines[6]) = (lines[6], lines[4]);

        var metrics = CompareInput.Measured(CompareEngine.Compare(CompareDuplicate.Definition(), CompareDuplicate.SourcesWith(lines)));

        Assert.Equal(
            ["r5 extra - c5 0", "r5 duplicate - c7 0"],
            metrics.Items.Where(i => i.Key == "r5").Select(Show));
        Assert.Equal(CompareDuplicate.MetricsBlock(), CompareInput.Text(MetricsJson.Render(metrics)));
    }

    // 13.2·13.5: 결과 데이터 행을 거꾸로 하거나 돌려도(머리 행은 그대로) counts·정확도·누락률·fields[]가 같다.
    // 줄 순서가 바꾸는 것은 줄 번호와 대표 행뿐이다. 표본 둘: 12.5 compare-duplicate, 그리고 허용오차 안 차이·숫자 아님이 섞인 중복.
    [Fact]
    public void Candidate_row_order_does_not_change_counts_or_fields()
    {
        var mixed = CompareInput.Case(CompareInput.InputsCase + "compare:\n  - {field: v, tolerance: 1}\n");
        const string MixedReference = "id,v\nk1,10\nk2,20\nk3,30\nk4,40\nk5,50\nk6,60\n";
        const string MixedCandidate = "id,v\nk1,10\nk2,25\nk1,11\nk3,30.5\nk5,50.5\nk2,20\nx1,1\nk4,abc\nx1,2\nk3,30\n";
        var samples = new (CaseDefinition Definition, string Reference, string[] Candidate)[]
        {
            (CompareDuplicate.Definition(), CompareDuplicate.ReferenceText(), CompareDuplicate.CandidateLines()),
            (mixed, MixedReference, MixedCandidate.TrimEnd('\n').Split('\n')),
        };

        // 셈 관계(13.2): 일치 1(k5, 허용오차 안), 값 불일치 4(k1·k2·k3 결과 쪽 두 번, k4 숫자 아님), 누락 1(k6), 추가 1(x1).
        // compared 8 = 결과 10 − 추가 1 − 기준에 없는 키의 중복 1. within 3(k1 둘째 행, k3 대표 행, k5), not_number 1.
        var forward = CompareInput.Measured(CompareInput.Run(mixed, MixedReference, MixedCandidate));
        Assert.Equal("ref=6 cand=10 match=1 mismatch=4 missing=1 extra=1 within=1 acc=16.7 miss=16.7", CompareInput.Counts(forward));
        Assert.Equal((8, 1, 3, 1), (forward.Fields[0].Compared, forward.Fields[0].Outside, forward.Fields[0].Within, forward.Fields[0].NotNumber));

        foreach (var (definition, reference, candidate) in samples)
        {
            var header = candidate[0];
            var data = candidate[1..];
            var expected = Summary(CompareInput.Measured(CompareInput.Run(definition, reference, Csv(header, data))));
            var orders = Enumerable.Range(1, data.Length - 1).Select(k => data[k..].Concat(data[..k]).ToArray()).Append(Enumerable.Reverse(data).ToArray());

            foreach (var order in orders)
            {
                Assert.Equal(expected, Summary(CompareInput.Measured(CompareInput.Run(definition, reference, Csv(header, order)))));
            }
        }
    }

    // "키 상태 r<기준 줄> c<결과 줄> 차이 수". 위치가 없으면 -.
    private static string Show(CompareItem item) =>
        $"{item.Key} {item.Status} {(item.Reference is { } r ? "r" + r.Line : "-")} {(item.Candidate is { } c ? "c" + c.Line : "-")} {item.Differences.Count}";

    private static string Where(CsvRow? row) => row is null ? "-" : $"{row.File}:{row.Line}";

    // 13.3 차이 칸: "—"는 [], `기준 → 결과`는 value 필드의 outside 차이 하나.
    private static string[] LineOrderDifferences(string cell) =>
        cell.Trim() == MetricsSpec.None ? [] : SpecDocument.CodeSpans(cell).Select(span => "value outside " + span).ToArray();

    private static string ShowDifference(FieldDifference d) => $"{d.Field} {d.Result} {d.Reference} → {d.Candidate}";

    // 13.3 "그 밖의 셈": 기준·결과 행, 누락, 추가, 허용오차 안 차이, 정확도, 누락률(InvariantCulture).
    private static string OtherCounts(CompareMetrics m) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"ref={m.Counts!.Reference} cand={m.Counts.Candidate} missing={m.Counts.Missing} extra={m.Counts.Extra} within={m.Counts.Within} acc={m.AccuracyPct} miss={m.MissingRatePct}");

    // 줄 순서와 상관없어야 하는 것: counts 전부, 퍼센트, fields[] 전부.
    private static string Summary(CompareMetrics m) =>
        CompareInput.Counts(m) + " | " + string.Join(
            " | ",
            m.Fields.Select(f => string.Create(CultureInfo.InvariantCulture, $"{f.Field} compared={f.Compared} outside={f.Outside} within={f.Within} not_number={f.NotNumber}")));

    private static string Csv(string header, IEnumerable<string> rows) => header + "\n" + string.Concat(rows.Select(r => r + "\n"));
}

// 스펙 metrics.md 12.5 compare-duplicate 표본(합성). 블록을 메모리 바이트로 넘긴다. judgment.md 6.4 판정 블록도 이 표본이다.
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

    public static string ReferenceText() => SpecDocument.BlockAfter(MetricsSpec.Document, ReferenceLinePrefix);

    // 결과 블록의 줄(머리 행 포함, 끝 LF 없이). 줄 번호 n은 [n - 1]이다.
    public static string[] CandidateLines() => SpecDocument.BlockAfter(MetricsSpec.Document, CandidateLinePrefix).TrimEnd('\n').Split('\n');

    // 결과 파일만 lines로 바꾼 자료(줄마다 LF).
    public static CompareSources SourcesWith(IEnumerable<string> lines) =>
        new(
            [CompareInput.Source(ReferencePath, ReferenceText())],
            [CompareInput.Source(CandidatePath, string.Concat(lines.Select(l => l + "\n")))]);

    // 13.3: 결과 파일 3·4줄(r2 두 행)의 value만 바꾼다.
    public static CompareSources SourcesWithR2(string third, string fourth)
    {
        var lines = CandidateLines();
        Assert.True(lines[2].StartsWith("r2,", StringComparison.Ordinal) && lines[3].StartsWith("r2,", StringComparison.Ordinal), "12.5 결과 블록 3·4줄은 r2 행이어야 한다.");
        lines[2] = "r2," + third;
        lines[3] = "r2," + fourth;
        return SourcesWith(lines);
    }

    // 12.5 결과 블록 3·4줄의 r2 value.
    public static string[] BlockR2Values() => CandidateLines()[2..4].Select(l => l["r2,".Length..]).ToArray();

    // 12.5 metrics.json 블록의 "그 밖의 셈"(13.3 둘째 글머리). DuplicateTests.OtherCounts와 같은 모양.
    public static string BlockOtherCounts()
    {
        using var json = JsonDocument.Parse(MetricsBlock());
        var compare = json.RootElement.GetProperty("compare");
        var counts = compare.GetProperty("counts");
        string Raw(JsonElement e, string name) => e.GetProperty(name).GetRawText();
        return $"ref={Raw(counts, "reference")} cand={Raw(counts, "candidate")} missing={Raw(counts, "missing")} extra={Raw(counts, "extra")} " +
            $"within={Raw(counts, "within")} acc={Raw(compare, "accuracy_pct")} miss={Raw(compare, "missing_rate_pct")}";
    }

    public static string MetricsBlock() => SpecDocument.BlockAfter(MetricsSpec.Document, MetricsLinePrefix);

    // 줄의 마지막 `코드`가 SHA-256이다(첫 `코드`는 표본 이름).
    public static string MetricsHash() => SpecDocument.CodeSpans(SpecDocument.Line(MetricsSpec.Document, MetricsLinePrefix))[^1];
}
