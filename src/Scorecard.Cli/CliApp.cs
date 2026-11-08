using System.Globalization;
using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;

namespace Scorecard.Cli;

// scorecard 명령 틀. docs/spec/json-output.md.
// 묻지 않는다. 표준 입력을 읽지 않는다. 분류·대조·판정은 Core 공개 API만 부른다.
// 테스트는 프로세스를 띄우지 않고 Run을 부른다(G1 N2·N3, 7절).
public static class CliApp
{
    // 기본 출력 폴더 이름(4절).
    public const string DefaultOutFolder = "out";

    // 도움말(데이터 2.1 블록과 바이트까지 같다).
    public const string HelpText =
        "scorecard <명령> <케이스 폴더> [옵션]\n"
        + "\n"
        + "명령:\n"
        + "  start <케이스> --answers <파일>  답 파일로 단계를 분류한다\n"
        + "  open <케이스>                    출력 폴더의 결과 파일을 알린다\n"
        + "  check <케이스>                   기준·결과 자료를 대조하고 판정한다\n"
        + "  build <케이스>                   제작 의뢰서 번들을 만든다(M2-4)\n"
        + "\n"
        + "옵션:\n"
        + "  --answers <파일>  답 파일(answers.yaml). start에 필요하다\n"
        + "  --out <폴더>      결과를 쓸 폴더. 기본값은 <케이스>/out\n"
        + "  --json            결과와 오류를 JSON 문서 하나로 낸다\n"
        + "  --help            이 도움말\n"
        + "\n"
        + "묻지 않는다. 파일 입력과 옵션만 받는다.\n"
        + "종료 코드: 0 성공, 2 입력 오류, 1 내부 오류.\n";

    public const int ExitSuccess = 0;
    public const int ExitInternalError = 1;
    public const int ExitInputError = 2;

    // open이 찾는 결과 파일(이 순서로 알린다, 2절).
    private static readonly string[] ResultFiles = [ClassificationJson.FileName, MetricsJson.FileName, JudgmentJson.FileName];

    // 인자를 받아 명령 하나를 돌린다. 종료 코드를 돌려준다(0 성공, 2 입력 오류, 1 내부 오류).
    // stdout·stderr에는 UTF-8 바이트를 직접 쓴다. --json이면 stdout에 JSON 문서 하나만 쓰고 stderr에는 쓰지 않는다.
    public static int Run(IReadOnlyList<string> args, Stream stdout, Stream stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        var json = CommandLine.WantsJson(args);
        Outcome outcome;
        try
        {
            outcome = Execute(args);
        }
        catch (Exception ex)
        {
            // 예외 메시지에는 절대 경로가 들 수 있다. 타입 이름만 쓴다(6절).
            outcome = Outcome.Fail(ExitInternalError, [new CaseError(string.Empty, 0, 0, CliErrors.Internal, string.Empty, ex.GetType().Name)]);
        }

        if (outcome.ExitCode == ExitSuccess)
        {
            Write(stdout, json ? outcome.Json : outcome.Text);
        }
        else if (json)
        {
            Write(stdout, CliJson.Errors(outcome.ExitCode, outcome.Errors));
        }
        else
        {
            var lines = string.Concat(outcome.Errors.Select(e => ErrorLine(e) + "\n"));
            Write(stderr, CliJson.Utf8.GetBytes(outcome.HelpOnError ? lines + "\n" + HelpText : lines));
        }

        return outcome.ExitCode;
    }

    private static Outcome Execute(IReadOnlyList<string> args)
    {
        var parsed = CommandLine.Parse(args);
        if (parsed.Help)
        {
            return Outcome.Ok(CliJson.Help(CommandLine.VisibleNames), CliJson.Utf8.GetBytes(HelpText));
        }

        if (parsed.Errors.Count > 0)
        {
            return Outcome.Fail(ExitInputError, parsed.Errors, helpOnError: parsed.Command is null);
        }

        var caseDirectory = parsed.CaseDirectory!;
        return parsed.Command!.Name switch
        {
            CommandLine.Start => RunStart(parsed, caseDirectory),
            CommandLine.Open => RunOpen(parsed, caseDirectory),
            CommandLine.Check => RunCheck(parsed, caseDirectory),
            CommandLine.Build => Outcome.Fail(ExitInputError, [new CaseError(string.Empty, 0, 0, CliErrors.Unavailable, string.Empty, "build는 M2-4에서 생긴다")]),
            CommandLine.Classify => RunClassify(parsed, caseDirectory),
            CommandLine.Compare => RunCompare(caseDirectory),
            CommandLine.Judge => RunJudge(caseDirectory),
            _ => throw new InvalidOperationException("명령 표와 분기가 다르다."),
        };
    }

    // start: 답 파일 → 분류 → classification.json.
    private static Outcome RunStart(ParsedArguments parsed, string caseDirectory)
    {
        if (!Directory.Exists(caseDirectory))
        {
            return Outcome.Fail(ExitInputError, [MissingCaseFolder()]);
        }

        var answers = ReadAnswers(parsed.Option(CommandLine.Answers)!);
        if (answers.Case is not { } definition)
        {
            return Outcome.Fail(ExitInputError, answers.Errors);
        }

        var classification = StepClassifier.Classify(definition);
        var header = string.Create(CultureInfo.InvariantCulture, $"분류했다: {classification.Case} ({classification.Steps.Count}단계)\n");
        return WriteOutputs(parsed, caseDirectory, CommandLine.Start, header, [(ClassificationJson.FileName, ClassificationJson.Render(classification))]);
    }

    // check: case.yaml → 대조 → 판정(시간·사람 입력 null) → metrics.json, judgment.json.
    private static Outcome RunCheck(ParsedArguments parsed, string caseDirectory)
    {
        var judged = CompareAndJudge(caseDirectory, out var metrics, out var judgment);
        if (judged is not null)
        {
            return judged;
        }

        var header = $"판정: {judgment!.Verdict} ({judgment.Rule})\n{judgment.Reason}\n";
        return WriteOutputs(parsed, caseDirectory, CommandLine.Check, header,
        [
            (MetricsJson.FileName, MetricsJson.Render(metrics!)),
            (JudgmentJson.FileName, JudgmentJson.Render(judgment)),
        ]);
    }

    // open: 출력 폴더의 결과 파일을 알린다. 아무것도 쓰지 않는다. HTML은 M2-3.
    private static Outcome RunOpen(ParsedArguments parsed, string caseDirectory)
    {
        if (!Directory.Exists(caseDirectory))
        {
            return Outcome.Fail(ExitInputError, [MissingCaseFolder()]);
        }

        var outputError = ResolveOutput(caseDirectory, parsed.Option(CommandLine.Out), out var output);
        if (outputError is not null)
        {
            return Outcome.Fail(ExitInputError, [outputError]);
        }

        // 이름 목록에서 서수로 찾는다. 대소문자를 무시하는 파일 시스템에서도 OS마다 결과가 같다(CaseParser와 같은 방식).
        var names = Directory.Exists(output)
            ? Directory.EnumerateFiles(output).Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal)
            : [];
        var found = ResultFiles.Where(names.Contains).ToArray();

        var display = DisplayOutput(caseDirectory, parsed.Option(CommandLine.Out));
        var text = found.Length == 0
            ? "결과 파일이 없다. start나 check를 먼저 돌린다.\n"
            : "결과 파일:\n" + string.Concat(found.Select(f => display + "/" + f + "\n"));
        text += "스코어카드 HTML은 M2-3에서 생긴다.\n";
        return Outcome.Ok(CliJson.Result(CommandLine.Open, found), CliJson.Utf8.GetBytes(text));
    }

    // 내부 명령 classify: --answers가 있으면 그 파일, 없으면 case.yaml의 steps.
    private static Outcome RunClassify(ParsedArguments parsed, string caseDirectory)
    {
        CaseParseResult source;
        if (parsed.Option(CommandLine.Answers) is { } answersPath)
        {
            if (!Directory.Exists(caseDirectory))
            {
                return Outcome.Fail(ExitInputError, [MissingCaseFolder()]);
            }

            source = ReadAnswers(answersPath);
        }
        else
        {
            source = CaseParser.ParseDirectory(caseDirectory);
        }

        return source.Case is { } definition
            ? Outcome.Document(ClassificationJson.Render(StepClassifier.Classify(definition)))
            : Outcome.Fail(ExitInputError, source.Errors);
    }

    // 내부 명령 compare: metrics.json 바이트.
    private static Outcome RunCompare(string caseDirectory)
    {
        var parsedCase = CaseParser.ParseDirectory(caseDirectory);
        if (parsedCase.Case is not { } definition)
        {
            return Outcome.Fail(ExitInputError, parsedCase.Errors);
        }

        var compared = CompareEngine.CompareDirectory(caseDirectory, definition);
        return compared.Metrics is { } metrics
            ? Outcome.Document(MetricsJson.Render(metrics))
            : Outcome.Fail(ExitInputError, compared.Errors);
    }

    // 내부 명령 judge: judgment.json 바이트.
    private static Outcome RunJudge(string caseDirectory) =>
        CompareAndJudge(caseDirectory, out _, out var judgment) ?? Outcome.Document(JudgmentJson.Render(judgment!));

    // 파싱 → 대조 → 판정. 입력 오류면 실패 결과를, 아니면 null을 돌려준다.
    // Judge의 ArgumentException은 잡지 않는다(내부 오류 1, judgment.md 3.4).
    private static Outcome? CompareAndJudge(string caseDirectory, out CompareMetrics? metrics, out JudgmentResult? judgment)
    {
        metrics = null;
        judgment = null;
        var parsedCase = CaseParser.ParseDirectory(caseDirectory);
        if (parsedCase.Case is not { } definition)
        {
            return Outcome.Fail(ExitInputError, parsedCase.Errors);
        }

        var compared = CompareEngine.CompareDirectory(caseDirectory, definition);
        if (compared.Metrics is null)
        {
            return Outcome.Fail(ExitInputError, compared.Errors);
        }

        metrics = compared.Metrics;
        judgment = JudgmentEngine.Judge(definition, metrics);
        return null;
    }

    // 답 파일을 읽어 파싱한다. 오류 source는 파일 이름이다(절대 경로를 쓰지 않는다, 6절).
    private static CaseParseResult ReadAnswers(string path)
    {
        var name = Path.GetFileName(path);
        try
        {
            return File.Exists(path)
                ? CaseParser.Parse(File.ReadAllBytes(path), name)
                : FileError(name, "파일이 없다");
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return FileError(name, "파일이 없다");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return FileError(name, "파일을 읽을 수 없다: " + ex.GetType().Name);
        }
    }

    private static CaseParseResult FileError(string source, string message) =>
        new(null, [new CaseError(source, 0, 0, CliErrors.File, string.Empty, message)]);

    private static CaseError MissingCaseFolder() =>
        new(string.Empty, 0, 0, CliErrors.File, CommandLine.CasePath, "케이스 폴더가 없다");

    // 출력 폴더를 확인하고 파일을 쓴다(4절). 모든 계산이 끝난 뒤에만 부른다.
    private static Outcome WriteOutputs(ParsedArguments parsed, string caseDirectory, string command, string header, IReadOnlyList<(string Name, byte[] Bytes)> files)
    {
        var outArgument = parsed.Option(CommandLine.Out);
        var outputError = ResolveOutput(caseDirectory, outArgument, out var output);
        if (outputError is not null)
        {
            return Outcome.Fail(ExitInputError, [outputError]);
        }

        try
        {
            Directory.CreateDirectory(output);
            foreach (var (name, bytes) in files)
            {
                File.WriteAllBytes(Path.Combine(output, name), bytes);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome.Fail(ExitInputError, [OutputError("출력 파일을 쓸 수 없다: " + ex.GetType().Name)]);
        }

        var names = files.Select(f => f.Name).ToArray();
        var display = DisplayOutput(caseDirectory, outArgument);
        var text = header + string.Concat(names.Select(n => display + "/" + n + "\n"));
        return Outcome.Ok(CliJson.Result(command, names), CliJson.Utf8.GetBytes(text));
    }

    // 기본값 <케이스>/out. --out은 케이스 폴더 밖이거나 기본값과 같아야 한다. 자리에 파일이 있으면 안 된다.
    private static CaseError? ResolveOutput(string caseDirectory, string? outArgument, out string output)
    {
        var caseFull = FullPath(caseDirectory);
        var defaultFull = Path.Join(caseFull, DefaultOutFolder);
        output = outArgument is null ? defaultFull : FullPath(outArgument);

        if (!string.Equals(output, defaultFull, StringComparison.Ordinal) && IsSameOrInside(output, caseFull))
        {
            return OutputError("출력 폴더는 케이스 폴더 밖이나 기본 out 폴더여야 한다");
        }

        return File.Exists(output) ? OutputError("출력 폴더 자리에 파일이 있다") : null;
    }

    private static CaseError OutputError(string message) =>
        new(string.Empty, 0, 0, CliErrors.Output, CommandLine.Out, message);

    private static string FullPath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool IsSameOrInside(string path, string folder) =>
        string.Equals(path, folder, StringComparison.Ordinal)
        || path.StartsWith(Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    // 사람용 안내의 출력 폴더. 받은 글자 그대로(사람용 글은 G2 대상이 아니다, 5.1).
    private static string DisplayOutput(string caseDirectory, string? outArgument) =>
        (outArgument ?? caseDirectory.TrimEnd('/', '\\') + "/" + DefaultOutFolder).TrimEnd('/', '\\');

    // 사람용 오류 줄. CaseError.ToString()과 같다. 출처가 없으면(Cli 오류) 앞의 ": "를 뺀다(5.1).
    public static string ErrorLine(CaseError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var line = error.ToString();
        return error.Source.Length == 0 && error.Line == 0 ? line[2..] : line;
    }

    private static void Write(Stream stream, byte[] bytes)
    {
        stream.Write(bytes);
        stream.Flush();
    }

    // 명령 결과. 성공이면 Json(--json)과 Text(사람용), 실패면 종료 코드와 오류.
    private sealed record Outcome(int ExitCode, byte[] Json, byte[] Text, IReadOnlyList<CaseError> Errors, bool HelpOnError)
    {
        public static Outcome Ok(byte[] json, byte[] text) => new(ExitSuccess, json, text, [], false);

        // 내부 명령: --json 유무와 상관없이 결과 문서 그대로(2절).
        public static Outcome Document(byte[] bytes) => Ok(bytes, bytes);

        public static Outcome Fail(int exitCode, IReadOnlyList<CaseError> errors, bool helpOnError = false) =>
            new(exitCode, [], [], errors, helpOnError);
    }
}
