// 스파이크(머지 대상 아님). docs/cases-public/answers/*.yaml을 엔진에 넣어 단계별 담당·근거 키를 찍는다.
// 쓰는 API는 docs/spec/classification.md 4.11, case-schema.md 4.12뿐이다. 엔진 코드는 읽지 않았다.
// 사용: dotnet run -- <yaml 폴더>
using System.Security.Cryptography;
using System.Text;
using Scorecard.Core.Cases;
using Scorecard.Core.Classification;

if (args.Length != 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("사용: dotnet run -- <yaml 폴더>");
    return 2;
}

var files = Directory.GetFiles(args[0], "*.yaml")
    .OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal)
    .ToList();
var sb = new StringBuilder();
var bad = 0;
foreach (var path in files)
{
    var file = Path.GetFileName(path);
    var parsed = CaseParser.Parse(File.ReadAllBytes(path), "answers.yaml");
    if (!parsed.IsValid)
    {
        bad++;
        sb.Append("FILE ").Append(file).Append(" errors=").Append(parsed.Errors.Count).Append('\n');
        foreach (var e in parsed.Errors)
        {
            sb.Append("  ERR ").Append(e.ToString()).Append('\n');
        }
        continue;
    }

    var result = StepClassifier.Classify(parsed.Case!);
    var json = ClassificationJson.Render(result);
    var sha = Convert.ToHexString(SHA256.HashData(json)).ToLowerInvariant();
    sb.Append("FILE ").Append(file).Append(" case=").Append(result.Case)
      .Append(" errors=0 steps=").Append(result.Steps.Count)
      .Append(" json_sha256=").Append(sha[..16]).Append('\n');
    foreach (var s in result.Steps)
    {
        var rows = string.Join(" + ", s.Rows.Select(r => r.Assignee + " " + r.Rule));
        sb.Append("  STEP ").Append(s.Number).Append(' ').Append(s.Name)
          .Append(" | ").Append(rows)
          .Append(" | overuse=").Append(s.AiOveruseCandidate ? "true" : "false").Append('\n');
        sb.Append("    defaults: ").Append(string.Join(", ", s.Defaults)).Append('\n');
        sb.Append("    inferred: ").Append(string.Join(", ", s.Inferred)).Append('\n');
        sb.Append("    checks: ").Append(string.Join(", ", s.Checks)).Append('\n');
        foreach (var r in s.Rows)
        {
            sb.Append("    ").Append(r.Assignee).Append(' ').Append(r.Rule).Append(' ').Append(r.Kind)
              .Append(": ").Append(r.Reason).Append('\n');
        }
    }
}

sb.Append("SUMMARY files=").Append(files.Count).Append(" parse_failed=").Append(bad).Append('\n');
Console.Out.Write(sb.ToString());
return bad == 0 ? 0 : 1;
