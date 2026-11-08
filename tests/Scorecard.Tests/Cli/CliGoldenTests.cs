using Scorecard.Core.Classification;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;

namespace Scorecard.Tests.Cli;

// 골든: 명령 출력 = 공개 예제 5종의 expected 파일(json-output.md 2절, 5.2, ADR 0004 결정 6 규칙 5, ADR 0010 B-10).
// 내부 명령은 저장소 예제 폴더를 그대로 읽는다(쓰지 않는다). 사용자 명령은 임시 사본과 임시 출력 폴더를 쓴다.
[Trait("Spec", "json-output")]
public class CliGoldenTests
{
    public static TheoryData<string> Examples => CliInput.AllExamples;

    [Theory]
    [MemberData(nameof(Examples))]
    public void Classify_json_equals_expected_classification(string example)
    {
        var run = CliInput.Run("classify", CliInput.Example(example), "--json");

        AssertDocument(run, CliInput.Expected(example, ClassificationJson.FileName));
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void Classify_answers_json_equals_expected_classification(string example)
    {
        var folder = CliInput.Example(example);
        var run = CliInput.Run("classify", folder, "--answers", Path.Combine(folder, "answers.yaml"), "--json");

        AssertDocument(run, CliInput.Expected(example, ClassificationJson.FileName));
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void Compare_json_equals_expected_metrics(string example)
    {
        var run = CliInput.Run("compare", CliInput.Example(example), "--json");

        AssertDocument(run, CliInput.Expected(example, MetricsJson.FileName));
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void Judge_json_equals_expected_judgment(string example)
    {
        var run = CliInput.Run("judge", CliInput.Example(example), "--json");

        AssertDocument(run, CliInput.Expected(example, JudgmentJson.FileName));
    }

    // 내부 명령은 --json이 없어도 결과 문서를 그대로 쓴다(2절). --json은 오류 모양만 바꾼다.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Internal_commands_print_the_same_document_without_json(string example)
    {
        var folder = CliInput.Example(example);

        AssertDocument(CliInput.Run("classify", folder), CliInput.Expected(example, ClassificationJson.FileName));
        AssertDocument(CliInput.Run("compare", folder), CliInput.Expected(example, MetricsJson.FileName));
        AssertDocument(CliInput.Run("judge", folder), CliInput.Expected(example, JudgmentJson.FileName));
    }

    // start --answers: 출력 폴더의 classification.json = expected. 결과 객체 files는 그 이름 하나.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Start_writes_expected_classification(string example)
    {
        using var work = CliWorkspace.Create(example);
        var output = work.PathOf("out1");

        var run = CliInput.Run("start", work.Case, "--answers", Path.Combine(work.Case, "answers.yaml"), "--out", output, "--json");

        Assert.Equal(0, run.Exit);
        Assert.Empty(run.Stderr);
        Assert.Equal([ClassificationJson.FileName], OutputFiles(output));
        Assert.Equal(CliInput.Expected(example, ClassificationJson.FileName), File.ReadAllBytes(Path.Combine(output, ClassificationJson.FileName)));
        Assert.Equal(ResultObject("start", ClassificationJson.FileName), run.Out);
    }

    // check: 출력 폴더의 metrics.json·judgment.json = expected. 미검증(mixed-exceptions)도 종료 0.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Check_writes_expected_metrics_and_judgment(string example)
    {
        using var work = CliWorkspace.Create(example);
        var output = work.PathOf("out1");

        var run = CliInput.Run("check", work.Case, "--out", output, "--json");

        Assert.Equal(0, run.Exit);
        Assert.Empty(run.Stderr);
        Assert.Equal([JudgmentJson.FileName, MetricsJson.FileName], OutputFiles(output));
        Assert.Equal(CliInput.Expected(example, MetricsJson.FileName), File.ReadAllBytes(Path.Combine(output, MetricsJson.FileName)));
        Assert.Equal(CliInput.Expected(example, JudgmentJson.FileName), File.ReadAllBytes(Path.Combine(output, JudgmentJson.FileName)));
        Assert.Equal(ResultObject("check", MetricsJson.FileName, JudgmentJson.FileName), run.Out);
    }

    // 데이터 5.2 첫 블록 = check --json 표준 출력(table-compare).
    [Fact]
    public void Check_result_object_equals_spec_block()
    {
        using var work = CliWorkspace.Create("table-compare");

        var run = CliInput.Run("check", work.Case, "--out", work.PathOf("o"), "--json");

        Assert.Equal(CliInput.Blocks(CliInput.ResultHeading)[0], run.Out);
    }

    // 기본 출력 폴더는 <케이스>/out(4절). start와 check가 같은 폴더에 쓴다. open은 세 파일을 고정 순서로 알린다.
    [Fact]
    public void Default_output_folder_collects_results_and_open_lists_them()
    {
        using var work = CliWorkspace.Create("table-compare");
        var output = Path.Combine(work.Case, "out");

        Assert.Equal(0, CliInput.Run("open", work.Case, "--json").Exit);
        Assert.Equal(ResultObject("open"), CliInput.Run("open", work.Case, "--json").Out);
        Assert.False(Directory.Exists(output), "open은 출력 폴더를 만들지 않는다.");

        Assert.Equal(0, CliInput.Run("check", work.Case).Exit);
        Assert.Equal(0, CliInput.Run("start", work.Case, "--answers", Path.Combine(work.Case, "answers.yaml")).Exit);

        Assert.Equal([ClassificationJson.FileName, JudgmentJson.FileName, MetricsJson.FileName], OutputFiles(output));
        var open = CliInput.Run("open", work.Case, "--json");
        Assert.Equal(0, open.Exit);
        Assert.Equal(ResultObject("open", ClassificationJson.FileName, MetricsJson.FileName, JudgmentJson.FileName), open.Out);

        // --out이 기본값과 같은 폴더면 받는다.
        Assert.Equal(open.Out, CliInput.Run("open", work.Case, "--out", output, "--json").Out);
    }

    // 사람용 안내(--json 없음): 표준 출력에 쓴 파일이 나오고 표준 오류는 비어 있다.
    [Fact]
    public void Human_output_names_written_files()
    {
        using var work = CliWorkspace.Create("table-compare");

        var run = CliInput.Run("check", work.Case, "--out", work.PathOf("o"));

        Assert.Equal(0, run.Exit);
        Assert.Empty(run.Stderr);
        Assert.Contains(MetricsJson.FileName, run.Out, StringComparison.Ordinal);
        Assert.Contains(JudgmentJson.FileName, run.Out, StringComparison.Ordinal);
        Assert.Contains("conditional", run.Out, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', run.Out);
    }

    // 같은 이름 파일은 덮어쓰고 다른 파일은 그대로 둔다(4절).
    [Fact]
    public void Output_folder_keeps_other_files_and_overwrites_results()
    {
        using var work = CliWorkspace.Create("form-fill");
        var output = work.PathOf("o");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "note.txt"), "keep");
        File.WriteAllText(Path.Combine(output, MetricsJson.FileName), "old");

        Assert.Equal(0, CliInput.Run("check", work.Case, "--out", output).Exit);

        Assert.Equal("keep", File.ReadAllText(Path.Combine(output, "note.txt")));
        Assert.Equal(CliInput.Expected("form-fill", MetricsJson.FileName), File.ReadAllBytes(Path.Combine(output, MetricsJson.FileName)));
    }

    private static void AssertDocument(CliRun run, byte[] expected)
    {
        Assert.Equal(0, run.Exit);
        Assert.Empty(run.Stderr);
        Assert.Equal(CliInput.Text(expected), run.Out);
        Assert.Equal(expected, run.Stdout);
    }

    private static string[] OutputFiles(string output) =>
        Directory.EnumerateFileSystemEntries(output).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!;

    // 결과 객체(5.2) 글. 데이터 블록과 같은 모양이다.
    private static string ResultObject(string command, params string[] files)
    {
        var list = files.Length == 0
            ? "  \"files\": []\n"
            : "  \"files\": [\n" + string.Join(",\n", files.Select(f => $"    \"{f}\"")) + "\n  ]\n";
        return "{\n  \"schema_version\": 1,\n  \"command\": \"" + command + "\",\n" + list + "}\n";
    }
}
