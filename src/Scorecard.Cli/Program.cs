// scorecard CLI 진입점. 콘솔 스트림을 CliApp.Run에 넘기기만 한다(docs/spec/json-output.md 7절).
using Scorecard.Cli;

using var stdout = Console.OpenStandardOutput();
using var stderr = Console.OpenStandardError();
return CliApp.Run(args, stdout, stderr);
