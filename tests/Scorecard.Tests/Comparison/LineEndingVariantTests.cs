using Scorecard.Core.Comparison;
using Scorecard.Tests.Synthetic;

namespace Scorecard.Tests.Comparison;

// 스펙 3.3 [P:eol]: 예제 CSV를 메모리에서 바꿔도(줄바꿈 CRLF·CR, BOM, 따옴표, 마지막 줄바꿈 없음, 끝 빈 줄, 칸 앞뒤 공백)
// 기준·결과 양쪽 또는 결과만 바꿔도 expected/metrics.json과 같은 바이트다. 카드 수용 기준 2(인코딩·줄바꿈 변형).
// 변형 바이트는 커밋하지 않는다(examples/에 \r을 두지 않는다).
[Trait("Spec", "metrics")]
public class LineEndingVariantTests
{
    private static readonly string[] VariantNames =
        ["crlf", "cr", "bom", "bom-crlf", "quoted", "bom-crlf-quoted", "no-final-newline", "trailing-blank-lines", "cell-spaces"];

    public static TheoryData<string, string> Cases
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var example in SyntheticInput.DataExamples)
            {
                foreach (var variant in VariantNames)
                {
                    data.Add(example, variant);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Variant_gives_expected_bytes(string example, string variant)
    {
        var definition = SyntheticInput.CaseOf(example);
        var sources = CompareInput.ExampleSources(example);
        var expected = CompareInput.ExpectedBytes(example);

        var both = new CompareSources(sources.Reference.Select(f => Transform(f, variant)).ToArray(), sources.Candidate.Select(f => Transform(f, variant)).ToArray());
        var candidateOnly = new CompareSources(sources.Reference, sources.Candidate.Select(f => Transform(f, variant)).ToArray());

        Assert.Equal(CompareInput.Text(expected), CompareInput.Text(MetricsJson.Render(CompareInput.Measured(CompareEngine.Compare(definition, both)))));
        Assert.Equal(expected, MetricsJson.Render(CompareInput.Measured(CompareEngine.Compare(definition, candidateOnly))));
    }

    // 변형이 실제로 바이트를 바꾼다(빈 통과 방지).
    [Theory]
    [MemberData(nameof(Cases))]
    public void Variant_changes_bytes(string example, string variant)
    {
        var file = CompareInput.ExampleSources(example).Candidate[0];

        Assert.NotEqual(file.Content, Transform(file, variant).Content);
    }

    private static SourceFile Transform(SourceFile file, string variant)
    {
        byte[] bom = [0xEF, 0xBB, 0xBF];
        var text = CompareInput.Text(file.Content);
        var lines = text.TrimEnd('\n').Split('\n');
        string Join(IEnumerable<string> rows, string newline) => string.Concat(rows.Select(r => r + newline));
        string Quoted() => Join(lines.Select(l => string.Join(",", l.Split(',').Select(CompareInput.Quote))), "\n");
        byte[] changed = variant switch
        {
            "crlf" => CompareInput.Bytes(Join(lines, "\r\n")),
            "cr" => CompareInput.Bytes(Join(lines, "\r")),
            "bom" => [.. bom, .. file.Content],
            "bom-crlf" => [.. bom, .. CompareInput.Bytes(Join(lines, "\r\n"))],
            "quoted" => CompareInput.Bytes(Quoted()),
            "bom-crlf-quoted" => [.. bom, .. CompareInput.Bytes(Quoted().Replace("\n", "\r\n", StringComparison.Ordinal))],
            "no-final-newline" => CompareInput.Bytes(text.TrimEnd('\n')),
            "trailing-blank-lines" => CompareInput.Bytes(text + "\n\n,,,,\n"),
            "cell-spaces" => CompareInput.Bytes(Join(lines.Select(l => string.Join(",", l.Split(',').Select(c => " " + c + "\t"))), "\n")),
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null),
        };
        return file with { Content = changed };
    }
}
