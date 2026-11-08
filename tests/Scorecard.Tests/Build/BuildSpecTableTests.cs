using System.Globalization;
using System.Text.Json;
using Scorecard.Core.Build;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Cli;
using Scorecard.Tests.Synthetic;

namespace Scorecard.Tests.Build;

// 데이터 docs/spec/data/build-request.md의 표·블록과 Core·Cli를 대조한다.
[Trait("Spec", "build-request")]
public class BuildSpecTableTests
{
    public static TheoryData<int> ErrorRows
    {
        get
        {
            var data = new TheoryData<int>();
            for (var i = 0; i < BuildInput.Table(BuildInput.ErrorTableHeader).Count; i++)
            {
                data.Add(i);
            }

            return data;
        }
    }

    // 4.5: 문자열 검사 패턴 = synthetic-examples.md 데이터 4.10 표(이름·정규식 글자, 순서).
    [Fact]
    public void Scan_patterns_equal_the_example_pattern_table()
    {
        var table = SyntheticSpec.Table(SyntheticSpec.PatternHeader)
            .Select(c => (SpecDocument.Unquote(c[0]), SpecDocument.CodeSpans(c[2]).Single()))
            .ToArray();

        Assert.Equal(table, BuildRequestBuilder.Patterns);
    }

    // 3.2 표: 번들 파일과 순서 = build --json의 files(출력 열이 있는 예제).
    [Fact]
    public void Bundle_file_table_matches_result_files()
    {
        var table = BuildInput.Table(BuildInput.FileTableHeader).Select(r => "build-request/" + SpecDocument.Unquote(r[0])).ToArray();
        using var result = JsonDocument.Parse(File.ReadAllBytes(BuildInput.GoldenPath("table-compare", "build.json")));

        Assert.Equal(table, result.RootElement.GetProperty("files").EnumerateArray().Select(f => f.GetString()));
    }

    // 3.1 블록 = form-fill 추천 --json, 4.2·4.4 블록 = form-fill 번들의 io-schema.json·context.json, 4.3 블록 = table-compare samples/output.csv.
    [Fact]
    public void Spec_blocks_equal_golden_bytes()
    {
        Assert.Equal(BuildInput.Blocks(BuildInput.ResultHeading)[0], Golden("form-fill", "choice.json"));
        Assert.Equal(BuildInput.Blocks(BuildInput.IoSchemaHeading)[0], Golden("form-fill", "build-request/io-schema.json"));
        Assert.Equal(BuildInput.Blocks(BuildInput.ContextHeading)[0], Golden("form-fill", "build-request/context.json"));
        Assert.Equal(BuildInput.Blocks(BuildInput.SampleHeading)[0], Golden("table-compare", "build-request/samples/output.csv"));
    }

    // 4.2 첫 표: spec.md 절 제목과 순서.
    [Fact]
    public void Spec_md_sections_follow_table()
    {
        var table = BuildInput.Table(BuildInput.SectionTableHeader).Select(r => SpecDocument.Unquote(r[0])).ToArray();
        foreach (var example in ClassificationExamples())
        {
            var headings = Golden(example, "build-request/spec.md").Split('\n').Where(l => l.StartsWith("## ", StringComparison.Ordinal)).Select(l => l[3..]);
            Assert.Equal(table, headings);
        }
    }

    // 4.2 둘째 표: 열 역할 순서(key → compare → checklist → group)와 형식.
    [Fact]
    public void Io_schema_columns_follow_role_table()
    {
        var roles = BuildInput.Table(BuildInput.ColumnTableHeader).Select(r => SpecDocument.Unquote(r[0])).ToArray();
        Assert.Equal(["key", "compare", "checklist", "group"], roles);
        foreach (var example in ClassificationExamples())
        {
            using var doc = JsonDocument.Parse(Golden(example, "build-request/io-schema.json"));
            var columns = doc.RootElement.GetProperty("output").GetProperty("columns").EnumerateArray().ToArray();
            var order = columns.Select(c => Array.IndexOf(roles, c.GetProperty("role").GetString())).ToArray();
            Assert.DoesNotContain(-1, order);
            Assert.Equal(order.Order(), order);
            Assert.All(columns, c => Assert.Equal(
                c.GetProperty("tolerance").ValueKind != JsonValueKind.Null || c.GetProperty("tolerance_pct").ValueKind != JsonValueKind.Null ? "number" : "text",
                c.GetProperty("type").GetString()));
        }
    }

    // 4.4 표: 제작 게이트 세 질문 = acceptance.md 표(예제 다섯 모두).
    [Fact]
    public void Acceptance_has_the_three_gate_questions()
    {
        var rows = BuildInput.Table(BuildInput.GateTableHeader).Select(r => "| " + string.Join(" | ", r) + " |").ToArray();
        Assert.Equal(3, rows.Length);
        foreach (var example in ClassificationExamples())
        {
            var text = Golden(example, "build-request/acceptance.md");
            Assert.All(rows, r => Assert.Contains("\n" + r + "\n", text, StringComparison.Ordinal));
        }
    }

    // 4.7 표: 예제마다 추천 단계, 고를 수 있는 단계, 번들 파일 수(export-check.md 포함).
    [Fact]
    public void Example_table_matches_golden_results()
    {
        var table = BuildInput.Table(BuildInput.ExampleTableHeader);
        Assert.Equal(ClassificationExamples().Order(StringComparer.Ordinal), table.Select(r => SpecDocument.Unquote(r[0])).Order(StringComparer.Ordinal));
        foreach (var row in table)
        {
            var example = SpecDocument.Unquote(row[0]);
            using var choice = JsonDocument.Parse(File.ReadAllBytes(BuildInput.GoldenPath(example, "choice.json")));
            Assert.Equal(int.Parse(row[1], CultureInfo.InvariantCulture), choice.RootElement.GetProperty("proposal").GetProperty("step").GetInt32());
            Assert.Equal(row[2], string.Join(", ", choice.RootElement.GetProperty("candidates").EnumerateArray().Select(c => c.GetInt32().ToString(CultureInfo.InvariantCulture))));
            using var built = JsonDocument.Parse(File.ReadAllBytes(BuildInput.GoldenPath(example, "build.json")));
            Assert.Equal(int.Parse(row[3], CultureInfo.InvariantCulture), built.RootElement.GetProperty("files").GetArrayLength());
        }
    }

    // 4.6 표: 행마다 종료 코드 2와 오류 하나. start 칸이 "예"면 start를 먼저 돌린다. 오류면 build-request/·export-check.md를 쓰지 않는다.
    [Theory]
    [MemberData(nameof(ErrorRows))]
    public void Error_table_row_gives_expected_error(int index)
    {
        var row = BuildInput.Table(BuildInput.ErrorTableHeader)[index];
        using var work = CliWorkspace.Create("signoff-final");
        var output = work.PathOf("o");
        if (row[1] == "예")
        {
            Assert.Equal(0, CliInput.Run("start", work.Case, "--answers", Path.Combine(work.Case, "answers.yaml"), "--out", output, "--json").Exit);
        }

        var args = SpecDocument.Unquote(row[0]).Split(' ')
            .Select(a => a switch { "<case>" => work.Case, "<missing>" => work.PathOf("missing"), _ => a })
            .Concat(["--out", output, "--json"])
            .ToArray();
        var run = CliInput.Run(args);

        Assert.Equal(2, run.Exit);
        using var doc = JsonDocument.Parse(run.Stdout);
        var error = Assert.Single(doc.RootElement.GetProperty("errors").EnumerateArray());
        Assert.Equal(CliInput.Cell(row[2]), error.GetProperty("code").GetString());
        Assert.Equal(CliInput.Cell(row[3]), error.GetProperty("source").GetString());
        Assert.Equal(CliInput.Cell(row[4]), error.GetProperty("path").GetString());
        Assert.Equal(CliInput.Cell(row[5]), error.GetProperty("message").GetString());
        Assert.False(File.Exists(Path.Combine(output, "export-check.md")));
        Assert.False(Directory.Exists(Path.Combine(output, "build-request")));
    }

    private static string Golden(string example, string file) => BuildInput.Text(File.ReadAllBytes(BuildInput.GoldenPath(example, file)));

    private static IEnumerable<string> ClassificationExamples() => Classification.ClassificationInput.AllExamples;
}
