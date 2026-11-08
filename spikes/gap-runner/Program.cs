// 스파이크 도구. 머지 대상 아님. 엔진 공개 API만 부른다(엔진 코드는 읽지 않는다).
// API 출처: classification.md 4.11, metrics.md 4.11, judgment.md 4.13, case-schema.md 4.12.
//
// 쓰기:
//   gap-runner <케이스 폴더> <출력 폴더> [--reviewed] [--unexplained k1,k2] [--times before,process,review]
//   gap-runner <answers.yaml> <출력 폴더>          (분류만)
//
// 케이스 폴더: CaseParser.ParseDirectory → StepClassifier.Classify
//              → CompareEngine.CompareDirectory → JudgmentEngine.Judge.
// 출력 폴더에 classification.json, metrics.json, judgment.json을 쓴다(엔진 Render 바이트 그대로).
// 표준 출력에는 한 줄씩 요약을 찍는다. 절대 경로는 찍지 않는다.
using System.Globalization;
using System.Text;
using System.Text.Json;
using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: gap-runner <case-dir|answers.yaml> <out-dir> [--reviewed] [--unexplained k1,k2] [--times b,p,r]");
    return 2;
}

var input = Path.GetFullPath(args[0]);
var outDir = Path.GetFullPath(args[1]);
IReadOnlyList<string>? unexplained = null;
JudgmentTimes? times = null;
for (var i = 2; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--reviewed":
            unexplained = Array.Empty<string>();
            break;
        case "--unexplained":
            unexplained = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries);
            break;
        case "--times":
            var t = args[++i].Split(',').Select(s => decimal.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            times = new JudgmentTimes(t[0], t[1], t[2]);
            break;
        default:
            Console.Error.WriteLine($"unknown option {args[i]}");
            return 2;
    }
}

Directory.CreateDirectory(outDir);

string Clean(string s) => s.Replace(input, "<input>");

void Out(string line) => Console.WriteLine(Clean(line));

CaseParseResult parsed;
var isAnswersFile = File.Exists(input);
try
{
    parsed = isAnswersFile
        ? CaseParser.Parse(File.ReadAllBytes(input), Path.GetFileName(input))
        : CaseParser.ParseDirectory(input);
}
catch (Exception ex)
{
    Out($"PARSE exception {ex.GetType().Name}");
    return 1;
}

if (!parsed.IsValid || parsed.Case is null)
{
    Out($"PARSE errors {parsed.Errors.Count}");
    foreach (var e in parsed.Errors)
    {
        Out($"  ERROR {e}");
    }

    return 0;
}

var def = parsed.Case;
Out($"PARSE ok case={def.Name} steps={def.Steps.Count} inputs={(def.Inputs is null ? "null" : "set")} compare={def.Compare.Count}");

// 분류
var cls = StepClassifier.Classify(def);
File.WriteAllBytes(Path.Combine(outDir, ClassificationJson.FileName), ClassificationJson.Render(cls));
var rowCount = 0;
foreach (var step in cls.Steps)
{
    Out($"STEP {step.Number} {step.Name} | defaults=[{string.Join(",", step.Defaults)}] checks=[{string.Join(",", step.Checks)}] overuse={step.AiOveruseCandidate.ToString().ToLowerInvariant()}");
    foreach (var row in step.Rows)
    {
        rowCount++;
        Out($"  ROW {step.Number} {row.Assignee} {row.Rule} {row.Kind}: {row.Reason}");
    }
}

Out($"CLASSIFY rows={rowCount}");

if (isAnswersFile)
{
    return 0;
}

// 대조
CompareResult cmp;
try
{
    cmp = CompareEngine.CompareDirectory(input, def);
}
catch (Exception ex)
{
    Out($"COMPARE exception {ex.GetType().Name}");
    return 1;
}

if (!cmp.IsValid || cmp.Metrics is null)
{
    Out($"COMPARE errors {cmp.Errors.Count}");
    foreach (var e in cmp.Errors)
    {
        Out($"  ERROR {e}");
    }

    return 0;
}

var metricsBytes = MetricsJson.Render(cmp.Metrics);
File.WriteAllBytes(Path.Combine(outDir, MetricsJson.FileName), metricsBytes);
using (var doc = JsonDocument.Parse(metricsBytes))
{
    var c = doc.RootElement.GetProperty("compare");
    var counts = c.GetProperty("counts");
    var countText = counts.ValueKind == JsonValueKind.Null
        ? "null"
        : string.Join(" ", counts.EnumerateObject().Select(p => $"{p.Name}={p.Value.GetRawText()}"));
    Out($"COMPARE status={c.GetProperty("status").GetString()} reason={c.GetProperty("reason").GetRawText()} accuracy_pct={c.GetProperty("accuracy_pct").GetRawText()} missing_rate_pct={c.GetProperty("missing_rate_pct").GetRawText()}");
    Out($"  COUNTS {countText}");
    Out($"  FILES reference=[{string.Join(",", c.GetProperty("reference_files").EnumerateArray().Select(x => x.GetString()))}] candidate=[{string.Join(",", c.GetProperty("candidate_files").EnumerateArray().Select(x => x.GetString()))}]");
    foreach (var item in c.GetProperty("items").EnumerateArray())
    {
        var diffs = string.Join(";", item.GetProperty("differences").EnumerateArray()
            .Select(d => $"{d.GetProperty("field").GetString()} {d.GetProperty("result").GetString()} {d.GetProperty("reference").GetString()}->{d.GetProperty("candidate").GetString()}"));
        Out($"  ITEM {item.GetProperty("key").GetString()} {item.GetProperty("status").GetString()} {diffs}");
    }
}

// 판정
JudgmentResult jr;
try
{
    jr = JudgmentEngine.Judge(def, cmp.Metrics, times, unexplained);
}
catch (Exception ex)
{
    Out($"JUDGE exception {ex.GetType().Name}");
    return 1;
}

File.WriteAllBytes(Path.Combine(outDir, JudgmentJson.FileName), JudgmentJson.Render(jr));
Out($"JUDGE verdict={jr.Verdict} rule={jr.Rule}: {jr.Reason}");
foreach (var ch in jr.Checks)
{
    Out($"  CHECK {ch.Rule} {ch.Result}: {ch.Reason}");
}

foreach (var td in jr.Todo)
{
    Out($"  TODO {td.Key}: {td.Text}");
}

Out($"  DEFAULTS [{string.Join(",", jr.Defaults)}]");
return 0;
