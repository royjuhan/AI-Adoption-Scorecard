using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Classification;
using Scorecard.Tests.Synthetic;

namespace Scorecard.Tests.Comparison;

// 공개 예제 3종 골든(카드 수용 기준 1, 승인 질문 15·17).
// examples/<예제>/ → CaseParser.ParseDirectory → CompareDirectory → Render가 expected/metrics.json과 바이트까지 같다.
// expected 파일은 스펙 7.2~7.4 블록을 바이트 그대로 옮긴 것이다(juhan 확정). 파일이 없으면 실패한다(빈 통과 금지).
[Trait("Spec", "metrics")]
public class MetricsGoldenTests
{
    public static TheoryData<string> Examples => ClassificationInput.ExampleNames;

    // 7.1 예제 표의 예제 = 공개 예제 3종, 7.2~7.4 제목이 있다.
    [Fact]
    public void Example_table_covers_the_three_examples()
    {
        Assert.Equal(ClassificationInput.Examples, MetricsSpec.Table(MetricsSpec.ExampleHeader).Select(c => MetricsSpec.Cell(c[0])));
        Assert.Equal(ClassificationInput.Examples.Order(StringComparer.Ordinal), MetricsSpec.ExampleHeadings.Keys.Order(StringComparer.Ordinal));
    }

    // 골든: 예제 폴더에서 만든 metrics.json이 expected와 바이트까지 같다.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Example_gives_expected_metrics_bytes(string example)
    {
        var expected = CompareInput.ExpectedBytes(example);
        var actual = CompareInput.ExampleBytes(example);

        Assert.Equal(CompareInput.Text(expected), CompareInput.Text(actual));
        Assert.Equal(expected, actual);
    }

    // 스펙 블록 = 예제 파일. 7.2~7.4의 블록 하나가 expected/metrics.json이다.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Expected_file_equals_spec_block(string example)
    {
        Assert.Equal(Encoding.UTF8.GetBytes(MetricsSpec.ExampleBlock(example)), CompareInput.ExpectedBytes(example));
    }

    // 스펙의 "`examples/<예제>/expected/metrics.json` (SHA-256 `…`)" 줄 = 파일의 SHA-256 = 블록의 SHA-256.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Expected_file_hash_matches_spec_line(string example)
    {
        var relative = $"examples/{example}/expected/{MetricsJson.FileName}";
        var spans = SpecDocument.CodeSpans(SpecDocument.Line(MetricsSpec.Document, $"`{relative}` (SHA-256 "));
        var bytes = CompareInput.ExpectedBytes(example);

        Assert.Equal(relative, spans[0]);
        Assert.Matches("^[0-9a-f]{64}$", spans[1]);
        Assert.Equal(spans[1], Convert.ToHexStringLower(SHA256.HashData(bytes)));
        Assert.Equal(spans[1], Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(MetricsSpec.ExampleBlock(example)))));
    }

    // 7.1 예제 표의 값 = expected/metrics.json의 값(—는 null). 엔진 출력도 같다.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Example_table_matches_expected_file(string example)
    {
        var cells = Assert.Single(MetricsSpec.Table(MetricsSpec.ExampleHeader), c => MetricsSpec.Cell(c[0]) == example).Skip(1).Select(MetricsSpec.Cell).ToArray();

        foreach (var bytes in new[] { CompareInput.ExpectedBytes(example), CompareInput.ExampleBytes(example) })
        {
            using var json = JsonDocument.Parse(bytes);
            var compare = json.RootElement.GetProperty("compare");
            var counts = compare.GetProperty("counts");
            string Count(string name) => counts.ValueKind == JsonValueKind.Null ? MetricsSpec.None : counts.GetProperty(name).GetRawText();
            string Raw(string name) => compare.GetProperty(name).ValueKind == JsonValueKind.Null ? MetricsSpec.None : compare.GetProperty(name).GetRawText();

            string[] actual =
            [
                compare.GetProperty("status").GetString()!,
                Count("reference"), Count("candidate"), Count("match"), Count("mismatch"), Count("missing"), Count("extra"), Count("within"),
                Raw("accuracy_pct"), Raw("missing_rate_pct"),
            ];
            Assert.Equal(cells, actual);
        }
    }

    // 지시문 12절: table-compare 90.0%·3.3%, form-fill 100%·0%, mixed-exceptions 미검증(카드 수용 기준 1).
    [Fact]
    public void Instructions_expectations_hold()
    {
        var table = CompareInput.Measured(CompareInput.Example("table-compare"));
        Assert.Equal((90.0m, 3.3m), (table.AccuracyPct!.Value, table.MissingRatePct!.Value));

        var form = CompareInput.Measured(CompareInput.Example("form-fill"));
        Assert.Equal((100.0m, 0.0m), (form.AccuracyPct!.Value, form.MissingRatePct!.Value));

        var mixed = CompareInput.Measured(CompareInput.Example("mixed-exceptions"));
        Assert.Equal((CompareEngine.StatusNotVerified, CompareEngine.ReasonNoInputs), (mixed.Status, mixed.Reason));
    }

    // 6.1: table-compare 일치가 아닌 기준 항목 3건(log07, log08, log27)은 모두 signal_type = irregular다. 추가 log31도 그렇다.
    // metrics.json에는 없고 메모리 CsvRow에 있다(판정 카드가 쓴다, 승인 질문 16).
    [Fact]
    public void Table_compare_problem_items_are_irregular_in_memory_rows()
    {
        var metrics = CompareInput.Measured(CompareInput.Example("table-compare"));
        var problems = metrics.Items.Where(i => i.Status != CompareEngine.ItemMatch).ToArray();

        Assert.Equal(["log07", "log08", "log27", "log31"], problems.Select(i => i.Key));
        Assert.All(problems, i =>
        {
            var row = i.Reference ?? i.Candidate!;
            Assert.Equal("irregular", row.Cells[SyntheticInput.IndexOf(row.Header, "signal_type")]);
        });
    }

    // T-005 생성기가 심은 구조 = 대조가 센 구조(4.4 [P:seeds]의 공개 예제 판). 키마다 상태가 심은 것과 같다.
    [Theory]
    [MemberData(nameof(SyntheticInput.DataExampleNames), MemberType = typeof(SyntheticInput))]
    public void Planted_structure_equals_compared_structure(string example)
    {
        var tables = SyntheticInput.Generate(example);
        var metrics = CompareInput.Measured(CompareInput.Example(example));

        string[] Keys(IEnumerable<string> keys) => keys.Order(StringComparer.Ordinal).ToArray();
        string[] Status(string status, bool differs = false) =>
            Keys(metrics.Items.Where(i => i.Status == status && (!differs || i.Differences.Count > 0)).Select(i => i.Key));

        Assert.Equal(Keys(tables.Reference.Where(r => r.Planted == Scorecard.Core.Synthetic.SyntheticGenerator.PlantedMissing).Select(r => r.Key)), Status(CompareEngine.ItemMissing));
        Assert.Equal(Keys(tables.Candidate.Where(r => r.Planted == Scorecard.Core.Synthetic.SyntheticGenerator.PlantedExtra).Select(r => r.Key)), Status(CompareEngine.ItemExtra));
        Assert.Equal(
            Keys(tables.Candidate.Where(r => r.Planted.StartsWith(Scorecard.Core.Synthetic.SyntheticGenerator.PlantedMismatchPrefix, StringComparison.Ordinal)).Select(r => r.Key)),
            Status(CompareEngine.ItemMismatch));
        Assert.Equal(
            Keys(tables.Candidate.Where(r => r.Planted.StartsWith(Scorecard.Core.Synthetic.SyntheticGenerator.PlantedWithinPrefix, StringComparison.Ordinal)).Select(r => r.Key)),
            Status(CompareEngine.ItemMatch, differs: true));
    }

    // 미검증 예제(mixed-exceptions)는 inputs가 없다. 폴더의 자료를 읽지 않는다.
    [Fact]
    public void Mixed_exceptions_has_no_inputs()
    {
        var definition = SyntheticInput.CaseOf("mixed-exceptions");

        Assert.Null(definition.Inputs);
        Assert.Equal(
            CompareInput.ExpectedBytes("mixed-exceptions"),
            MetricsJson.Render(CompareInput.Measured(CompareEngine.Compare(definition, new CompareSources([], [])))));
    }

    // examples/.gitattributes(`* -text`)가 새 파일에도 걸린다(7.1 5). expected 파일에 CR이 없다.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Expected_file_has_no_carriage_return(string example)
    {
        Assert.DoesNotContain((byte)'\r', CompareInput.ExpectedBytes(example));
        Assert.Contains("* -text", File.ReadAllLines(Scorecard.Tests.Structure.RepoRoot.Combine("examples/.gitattributes")).Select(l => l.Trim()));
        Assert.True(File.Exists(SyntheticInput.ExamplePath(example, CaseParser.FileName)));
    }
}
