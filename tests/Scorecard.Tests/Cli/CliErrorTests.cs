using System.Text.Json;
using Scorecard.Cli;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Tests.Guards;

namespace Scorecard.Tests.Cli;

// 오류 JSON과 종료 코드(json-output.md 6절, ADR 0004 결정 6 규칙 6·7).
[Trait("Spec", "json-output")]
public class CliErrorTests
{
    private static readonly string[] ErrorKeys = ["code", "source", "line", "column", "path", "message"];

    public static TheoryData<int> ArgumentRows
    {
        get
        {
            var data = new TheoryData<int>();
            for (var i = 0; i < CliInput.Table(CliInput.ArgumentTableHeader).Count; i++)
            {
                data.Add(i);
            }

            return data;
        }
    }

    // 데이터 6.3 표: 행마다 종료 코드와 오류 하나(다섯 값, 줄·열 0). --json이면 오류 JSON, 아니면 표준 오류의 오류 줄.
    [Theory]
    [MemberData(nameof(ArgumentRows))]
    public void Argument_table_row_gives_expected_error(int index)
    {
        var row = CliInput.Table(CliInput.ArgumentTableHeader)[index];
        using var work = CliWorkspace.Create("table-compare");
        var file = work.PathOf("file.txt");
        File.WriteAllText(file, "x");
        var args = Arguments(row[0], work.Case, work.PathOf("missing"), file);
        var exit = int.Parse(row[1], System.Globalization.CultureInfo.InvariantCulture);
        var expected = new CaseError(CliInput.Cell(row[3]), 0, 0, CliInput.Cell(row[2]), CliInput.Cell(row[4]), CliInput.Cell(row[5]));

        var jsonArgs = args.Contains("--json") ? args : [.. args, "--json"];
        var json = CliInput.Run(jsonArgs);
        Assert.Equal(exit, json.Exit);
        Assert.Empty(json.Stderr);
        var errors = ReadErrorJson(json.Stdout, exit);
        Assert.Equal([expected], errors);

        if (!args.Contains("--json"))
        {
            var text = CliInput.Run(args);
            Assert.Equal(exit, text.Exit);
            Assert.Empty(text.Stdout);
            Assert.StartsWith(CliApp.ErrorLine(expected) + "\n", text.Err, StringComparison.Ordinal);
        }

        Assert.False(Directory.Exists(Path.Combine(work.Case, CliApp.DefaultOutFolder)), "오류면 출력 폴더를 만들지 않는다.");
    }

    // 데이터 6.1 블록 = build --json 표준 출력(start 전이라 classification.json 없음, T-021).
    [Fact]
    public void Build_error_json_equals_spec_block()
    {
        using var work = CliWorkspace.Create("table-compare");

        var run = CliInput.Run("build", work.Case, "--json");

        Assert.Equal(2, run.Exit);
        Assert.Equal(CliInput.Blocks(CliInput.ErrorJsonHeading)[0], run.Out);
    }

    // 6.2 표의 코드 = Cli가 내는 코드. 6.3 표가 종료 2 코드를 모두 쓴다. internal은 아래 테스트.
    // T-021: build가 생겨 unavailable을 뺐다(쓰는 곳이 없다, build-request.md 10절 11). 표와 같은지 보는 단언은 그대로다.
    [Fact]
    public void Cli_error_codes_match_spec_table()
    {
        var table = CliInput.Table(CliInput.CliErrorTableHeader)
            .ToDictionary(r => CliInput.Cell(r[0]), r => int.Parse(r[2], System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(
            new Dictionary<string, int>
            {
                ["argument"] = 2,
                ["file"] = 2,
                ["output"] = 2,
                ["internal"] = 1,
            },
            table);
        var used = CliInput.Table(CliInput.ArgumentTableHeader).Select(r => CliInput.Cell(r[2])).ToHashSet(StringComparer.Ordinal);
        Assert.All(table.Where(t => t.Value == 2).Select(t => t.Key), code => Assert.Contains(code, used));
    }

    // 예상하지 못한 예외는 internal, 종료 1, 메시지는 예외 타입 이름만.
    [Fact]
    public void Unexpected_exception_is_internal_error_with_type_name()
    {
        var json = CliInput.Run("check", null!, "--json");
        Assert.Equal(1, json.Exit);
        Assert.Empty(json.Stderr);
        var error = Assert.Single(ReadErrorJson(json.Stdout, 1));
        Assert.Equal("internal", error.Code);
        Assert.Equal(string.Empty, error.Source);
        Assert.Matches("^[A-Za-z]+Exception$", error.Message);

        var text = CliInput.Run("check", null!);
        Assert.Equal(1, text.Exit);
        Assert.StartsWith("internal: ", text.Err, StringComparison.Ordinal);
    }

    // Core 오류는 값·순서 그대로(6절). 파서 오류 표본과 대조 오류 표본.
    [Theory]
    [InlineData("check", "case-invalid")]
    [InlineData("compare", "case-invalid")]
    [InlineData("judge", "compare-invalid")]
    [InlineData("check", "compare-invalid")]
    public void Core_errors_pass_through_unchanged(string command, string sample)
    {
        using var workspace = GuardWorkspace.Create(sample, reverseCopy: false);
        var parsed = CaseParser.ParseDirectory(workspace.Input);
        var expected = parsed.Case is { } definition
            ? CompareEngine.CompareDirectory(workspace.Input, definition).Errors
            : parsed.Errors;
        Assert.NotEmpty(expected);

        // 출력 폴더 옵션은 check만 받는다(데이터 2절 표).
        string[] args = command == "check" ? [command, workspace.Input, "--out", workspace.PathOf("out")] : [command, workspace.Input];
        var json = CliInput.Run([.. args, "--json"]);
        Assert.Equal(2, json.Exit);
        Assert.Equal(expected, ReadErrorJson(json.Stdout, 2));

        var text = CliInput.Run(args);
        Assert.Equal(2, text.Exit);
        Assert.Equal(string.Concat(expected.Select(e => e + "\n")), text.Err);
        Assert.False(Directory.Exists(workspace.PathOf("out")));
    }

    // 답 파일 파싱 오류의 source는 파일 이름(절대 경로가 아니다).
    [Fact]
    public void Answers_parse_error_uses_file_name_as_source()
    {
        using var work = CliWorkspace.Create("table-compare");
        var answers = work.PathOf("bad-answers.yaml");
        File.WriteAllText(answers, "name: x\nsteps: 3\n");

        var run = CliInput.Run("start", work.Case, "--answers", answers, "--json");

        Assert.Equal(2, run.Exit);
        var errors = ReadErrorJson(run.Stdout, 2);
        Assert.NotEmpty(errors);
        Assert.All(errors, e => Assert.Equal("bad-answers.yaml", e.Source));
        Assert.DoesNotContain(work.Root, run.Out, StringComparison.Ordinal);
    }

    // 인자 오류는 모두 모아 한 번에 낸다(3절).
    [Fact]
    public void Argument_errors_are_collected()
    {
        var run = CliInput.Run("check", "a", "b", "--verbose", "--answers", "x", "--json");

        Assert.Equal(2, run.Exit);
        Assert.Equal(
            ["argument  남는 인자다: b", "argument --verbose 이 명령에 없는 옵션이다", "argument --answers 이 명령에 없는 옵션이다"],
            ReadErrorJson(run.Stdout, 2).Select(e => $"{e.Code} {e.Path} {e.Message}"));
    }

    // 명령이 없거나 모르면 사람용 오류 뒤에 도움말을 붙인다(표준 오류).
    [Fact]
    public void Missing_command_shows_help_on_stderr()
    {
        var run = CliInput.Run();

        Assert.Equal(2, run.Exit);
        Assert.Empty(run.Stdout);
        Assert.EndsWith(CliApp.HelpText, run.Err, StringComparison.Ordinal);
    }

    // 오류 JSON 모양: schema_version 1, exit_code, errors(여섯 키 순서). 바이트 규칙(BOM·CR 없음, 끝 LF).
    internal static IReadOnlyList<CaseError> ReadErrorJson(byte[] bytes, int exitCode)
    {
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "BOM이 없어야 한다.");
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal((byte)'\n', bytes[^1]);
        using var json = JsonDocument.Parse(bytes);
        var root = json.RootElement;
        Assert.Equal(["schema_version", "exit_code", "errors"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.Equal(exitCode, root.GetProperty("exit_code").GetInt32());
        return root.GetProperty("errors").EnumerateArray().Select(e =>
        {
            Assert.Equal(ErrorKeys, e.EnumerateObject().Select(p => p.Name));
            return new CaseError(
                e.GetProperty("source").GetString()!,
                e.GetProperty("line").GetInt32(),
                e.GetProperty("column").GetInt32(),
                e.GetProperty("code").GetString()!,
                e.GetProperty("path").GetString()!,
                e.GetProperty("message").GetString()!);
        }).ToArray();
    }

    // 표 칸의 인자 → 낱말 목록. 자리표시를 임시 경로로 바꾼다. "(없음)"은 인자 없음.
    private static string[] Arguments(string cell, string caseFolder, string missing, string file)
    {
        var value = CliInput.Cell(cell);
        if (value == "(없음)")
        {
            return [];
        }

        return value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Replace("<case>", caseFolder, StringComparison.Ordinal)
                .Replace("<missing>", missing, StringComparison.Ordinal)
                .Replace("<file>", file, StringComparison.Ordinal))
            .ToArray();
    }
}
