using System.Text.Json;
using Scorecard.Cli;

namespace Scorecard.Tests.Cli;

// 도움말과 명령 표(json-output.md 2절, 데이터 2절·2.1).
[Trait("Spec", "json-output")]
public class CliHelpTests
{
    private static readonly string[] HiddenCommands = ["classify", "compare", "judge"];

    // 데이터 2.1 블록 = --help 표준 출력 바이트. -h, help, 명령 뒤 --help도 같다. 종료 0, 표준 오류 없음.
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    [InlineData("check", "--help")]
    [InlineData("frobnicate", "-h")]
    public void Help_prints_spec_block(params string[] args)
    {
        var block = CliInput.Blocks(CliInput.HelpHeading).Single();

        var run = CliInput.Run(args);

        Assert.Equal(0, run.Exit);
        Assert.Empty(run.Stderr);
        Assert.Equal(block, run.Out);
        Assert.Equal(CliApp.HelpText, block);
    }

    // 도움말에는 사용자 명령 넷만 보인다. 내부 명령 이름은 없다.
    [Fact]
    public void Help_shows_only_the_four_user_commands()
    {
        var lines = CliApp.HelpText.Split('\n');
        var shown = lines.Where(l => l.StartsWith("  ", StringComparison.Ordinal) && !l.TrimStart().StartsWith('-'))
            .Select(l => l.TrimStart().Split(' ')[0])
            .ToArray();

        Assert.Equal(["start", "open", "check", "build"], shown);
        Assert.All(HiddenCommands, c => Assert.DoesNotContain(c, CliApp.HelpText, StringComparison.Ordinal));
    }

    // 명령 표의 "도움말 예" 행 = 도움말 명령 = help --json의 commands. 표 순서.
    [Fact]
    public void Command_table_visible_rows_match_help_json()
    {
        var table = CliInput.Table(CliInput.CommandTableHeader);
        var visible = table.Where(r => r[1] == "예").Select(r => CliInput.Cell(r[0])).ToArray();
        var hidden = table.Where(r => r[1] == "아니오").Select(r => CliInput.Cell(r[0])).ToArray();

        Assert.Equal(["start", "open", "check", "build"], visible);
        Assert.Equal(HiddenCommands, hidden);

        var run = CliInput.Run("help", "--json");
        Assert.Equal(0, run.Exit);
        Assert.Equal(CliInput.Blocks(CliInput.ResultHeading)[1], run.Out);
        using var json = JsonDocument.Parse(run.Stdout);
        Assert.Equal(visible, json.RootElement.GetProperty("commands").EnumerateArray().Select(e => e.GetString()));
    }

    // 명령 표의 옵션 칸: 그 밖의 옵션은 "이 명령에 없는 옵션이다"다. 표의 옵션은 그 오류를 내지 않는다.
    [Fact]
    public void Command_table_options_are_the_accepted_options()
    {
        string[] all = ["--answers", "--review", "--out", "--json"];
        foreach (var row in CliInput.Table(CliInput.CommandTableHeader))
        {
            var command = CliInput.Cell(row[0]);
            var accepted = CliInput.List(row[2]);
            foreach (var option in all)
            {
                string[] args = option == "--json" ? [command, "no-such-case-folder", option] : [command, "no-such-case-folder", option, "v", "--json"];
                var errors = CliErrorTests.ReadErrorJson(CliInput.Run(args).Stdout, 2)
                    .Where(e => e.Path == option && e.Message == "이 명령에 없는 옵션이다");
                Assert.True(accepted.Contains(option) == !errors.Any(), $"{command} {option}");
            }
        }
    }
}
