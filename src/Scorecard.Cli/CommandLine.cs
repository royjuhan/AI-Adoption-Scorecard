using Scorecard.Core.Cases;

namespace Scorecard.Cli;

// 명령 하나의 모양. docs/spec/data/json-output.md 2절 표.
internal sealed record CommandSpec(string Name, bool Visible, IReadOnlyList<string> Options, IReadOnlyList<string> Required);

// 해석한 인자. 인자 오류가 있으면 Errors가 비어 있지 않다.
internal sealed record ParsedArguments(
    CommandSpec? Command,
    string? CaseDirectory,
    IReadOnlyDictionary<string, string?> Options,
    bool Help,
    IReadOnlyList<CaseError> Errors)
{
    public string? Option(string name) => Options.TryGetValue(name, out var value) ? value : null;

    public bool Has(string name) => Options.ContainsKey(name);
}

// 인자 규칙. docs/spec/json-output.md 3절. 직접 짠다(9절 6: System.CommandLine은 G1 N6 허용 목록이 필요하다).
internal static class CommandLine
{
    public const string Start = "start";
    public const string Open = "open";
    public const string Check = "check";
    public const string Build = "build";
    public const string Classify = "classify";
    public const string Compare = "compare";
    public const string Judge = "judge";
    public const string HelpCommand = "help";

    public const string Json = "--json";
    public const string Out = "--out";
    public const string Answers = "--answers";
    public const string Review = "--review";
    public const string Step = "--step";
    public const string Help = "--help";
    public const string HelpShort = "-h";

    // 인자 오류의 path 값(위치 인자 이름).
    public const string CommandPath = "command";
    public const string CasePath = "case";

    public static readonly IReadOnlyList<CommandSpec> All =
    [
        new(Start, true, [Answers, Out, Json], [Answers]),
        new(Open, true, [Review, Out, Json], []),
        new(Check, true, [Out, Json], []),
        new(Build, true, [Step, Out, Json], []),
        new(Classify, false, [Answers, Json], []),
        new(Compare, false, [Json], []),
        new(Judge, false, [Json], []),
    ];

    private static readonly string[] ValueOptions = [Out, Answers, Review, Step];

    // 도움말에 보이는 명령(표 순서).
    public static IReadOnlyList<string> VisibleNames => All.Where(c => c.Visible).Select(c => c.Name).ToArray();

    // --json이 있는가. 인자 오류도 JSON으로 내려고 해석 전에 본다.
    public static bool WantsJson(IReadOnlyList<string> args) => args.Any(a => string.Equals(a, Json, StringComparison.Ordinal));

    public static ParsedArguments Parse(IReadOnlyList<string> args)
    {
        var errors = new List<CaseError>();
        var positional = new List<string>();
        var options = new Dictionary<string, string?>(StringComparer.Ordinal);
        var optionOrder = new List<string>();
        var help = false;

        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i];
            if (token is Help or HelpShort)
            {
                help = true;
                continue;
            }

            if (!token.StartsWith('-') || token.Length == 1)
            {
                positional.Add(token);
                continue;
            }

            string? value = null;
            if (ValueOptions.Contains(token, StringComparer.Ordinal))
            {
                if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    errors.Add(Argument(token, "옵션 값이 없다"));
                    continue;
                }

                value = args[++i];
            }

            if (options.ContainsKey(token))
            {
                errors.Add(Argument(token, "같은 옵션이 두 번 나온다"));
                continue;
            }

            options[token] = value;
            optionOrder.Add(token);
        }

        if (help || (positional.Count > 0 && positional[0] == HelpCommand))
        {
            return new ParsedArguments(null, null, options, true, []);
        }

        if (positional.Count == 0)
        {
            errors.Add(Argument(CommandPath, "명령이 없다"));
            return new ParsedArguments(null, null, options, false, errors);
        }

        var command = All.FirstOrDefault(c => c.Name == positional[0]);
        if (command is null)
        {
            errors.Add(Argument(CommandPath, "모르는 명령이다: " + positional[0]));
            return new ParsedArguments(null, null, options, false, errors);
        }

        if (positional.Count < 2)
        {
            errors.Add(Argument(CasePath, "케이스 폴더를 적지 않았다"));
        }

        foreach (var extra in positional.Skip(2))
        {
            errors.Add(Argument(string.Empty, "남는 인자다: " + extra));
        }

        foreach (var option in optionOrder.Where(o => !command.Options.Contains(o, StringComparer.Ordinal)))
        {
            errors.Add(Argument(option, "이 명령에 없는 옵션이다"));
        }

        // 값이 없어 받지 못한 옵션은 이미 오류다. 필수 옵션 오류를 또 내지 않는다.
        foreach (var required in command.Required.Where(r => !options.ContainsKey(r) && !errors.Any(e => e.Path == r)))
        {
            errors.Add(Argument(required, "필수 옵션이 없다"));
        }

        return new ParsedArguments(command, positional.Count >= 2 ? positional[1] : null, options, false, errors);
    }

    private static CaseError Argument(string path, string message) =>
        new(string.Empty, 0, 0, CliErrors.Argument, path, message);
}
