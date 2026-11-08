using System.Text;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Core.Report;

namespace Scorecard.Core.Build;

// 제작 의뢰서 번들(build-request/)을 만든다. docs/spec/build-request.md.
// 순수 함수다. 스코어카드 결과 객체와 open이 읽는 입력 바이트(ScorecardSources)만 받는다. 파일·시각·난수를 쓰지 않는다.
// 숫자·판정은 결과 객체 값을 옮기기만 한다. 자료 파일(reference/·candidate/)의 값은 번들에 넣지 않는다(지시문 2절 8).
public static class BuildRequestBuilder
{
    public const string FolderName = BuildRequestRules.FolderName;
    public const string ExportCheckFile = BuildRequestRules.ExportCheckFile;
    public const string StepError = BuildRequestRules.StepError;
    public const string ExportError = BuildRequestRules.ExportError;

    // 문자열 검사 패턴(검사 순서): 이름과 정규식 글자. synthetic-examples.md 데이터 4.10 표와 같다.
    public static IReadOnlyList<(string Name, string Pattern)> Patterns { get; } = BuildRequestRules.PatternSource;

    // 고를 수 있는 단계: 단계 행 담당이 ② 또는 ②M(report.md 4.4와 같은 집합). 번호 순.
    public static IReadOnlyList<int> Candidates(ScorecardResult scorecard)
    {
        ArgumentNullException.ThrowIfNull(scorecard);
        return scorecard.Steps.Where(s => ReportRules.SoftwareAssignees.Contains(s.Assignee)).Select(s => s.Number).ToArray();
    }

    // 고른 단계(--step)로 번들을 만든다. 단계가 없거나 ②·②M이 아니면 step 오류. case.yaml 파싱 오류는 그대로 낸다.
    public static BuildRequestResult Create(ScorecardResult scorecard, ScorecardSources sources, int step)
    {
        ArgumentNullException.ThrowIfNull(scorecard);
        ArgumentNullException.ThrowIfNull(sources);

        var chosen = scorecard.Steps.FirstOrDefault(s => s.Number == step);
        if (chosen is null)
        {
            return Fail(StepMessage(BuildRequestRules.StepMissingText, step, string.Empty));
        }

        if (!ReportRules.SoftwareAssignees.Contains(chosen.Assignee))
        {
            return Fail(StepMessage(BuildRequestRules.StepNotSoftwareText, step, chosen.Assignee));
        }

        CaseDefinition? definition = null;
        if (Find(sources.Inputs, CaseParser.FileName) is { } caseBytes)
        {
            var parsed = CaseParser.Parse(caseBytes, CaseParser.FileName);
            if (parsed.Case is null)
            {
                return new BuildRequestResult(null, parsed.Errors);
            }

            definition = parsed.Case;
        }

        var answers = FindAnswers(sources, definition, scorecard)?[step - 1].Answers;
        var columns = BuildRequestText.Columns(definition);
        var view = new BundleView(scorecard, chosen, answers, definition, columns);

        var files = new List<SourceFile>
        {
            Text(BuildRequestRules.SpecFile, BuildRequestText.Spec(view)),
            new(BuildRequestRules.IoSchemaFile, BuildRequestJson.IoSchema(view)),
            Text(BuildRequestRules.AcceptanceFile, BuildRequestText.Acceptance(view)),
            Text(BuildRequestRules.SamplesReadme, BuildRequestText.SamplesReadme(columns.Count > 0)),
        };
        if (columns.Count > 0)
        {
            files.Add(new SourceFile(BuildRequestRules.SamplesCsv, BuildRequestText.SampleCsv(columns)));
        }

        files.Add(new SourceFile(BuildRequestRules.ContextFile, BuildRequestJson.Context(view)));

        var hits = Scan(files);
        var draft = BuildRequestText.ExportCheck(view, files, hits);
        return new BuildRequestResult(new BuildRequestBundle(scorecard.Case, chosen.Number, chosen.Name, files, hits, Utf8.GetBytes(draft)), []);
    }

    // 사람 확인(4.5). exportCheck는 출력 폴더의 export-check.md 바이트(없으면 null).
    // 걸린 문자열이 있으면 확인과 상관없이 내보내지 않는다. 초안과 확인 줄 값만 다르고 나머지가 같을 때만 확인으로 본다.
    public static ExportCheck CheckExport(BuildRequestBundle bundle, byte[]? exportCheck)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        if (bundle.Hits.Count > 0)
        {
            var text = BuildRequestRules.HitsText.Replace("{count}", bundle.Hits.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
            return Refuse(BuildRequestRules.Hits, text);
        }

        if (exportCheck is null)
        {
            return Refuse(BuildRequestRules.Missing, BuildRequestRules.MissingText);
        }

        var lines = Normalize(exportCheck).Split('\n');
        var signatures = Enumerable.Range(0, lines.Length).Where(i => lines[i].StartsWith(BuildRequestRules.SignaturePrefix, StringComparison.Ordinal)).ToArray();
        if (signatures.Length != 1)
        {
            return Refuse(BuildRequestRules.Changed, BuildRequestRules.ChangedText);
        }

        var index = signatures[0];
        var signer = lines[index][BuildRequestRules.SignaturePrefix.Length..].Trim();
        lines[index] = BuildRequestRules.SignaturePrefix;
        if (!string.Equals(string.Join('\n', lines), Utf8.GetString(bundle.ExportCheckDraft), StringComparison.Ordinal))
        {
            return Refuse(BuildRequestRules.Changed, BuildRequestRules.ChangedText);
        }

        return signer.Length == 0
            ? Refuse(BuildRequestRules.Unsigned, BuildRequestRules.UnsignedText)
            : new ExportCheck(BuildRequestRules.Confirmed, signer, null);
    }

    // 문자열 검사(4.5). 파일 순서 → 줄 → 패턴 순서 → 줄 안 위치.
    public static IReadOnlyList<ScanHit> Scan(IReadOnlyList<SourceFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var hits = new List<ScanHit>();
        foreach (var file in files)
        {
            var lines = Normalize(file.Content).Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (var (name, regex) in BuildRequestRules.Patterns)
                {
                    hits.AddRange(regex.Matches(lines[i]).Select(m => new ScanHit(name, file.Path, i + 1, m.Value)));
                }
            }
        }

        return hits;
    }

    // 답: answers.yaml, 없거나 단계가 다르면 case.yaml. 단계 수·번호·이름이 결과 객체와 같아야 쓴다(report.md 4.3과 같은 비교).
    private static IReadOnlyList<CaseStep>? FindAnswers(ScorecardSources sources, CaseDefinition? definition, ScorecardResult scorecard)
    {
        var fromAnswers = Find(sources.Inputs, ReportRules.AnswersFile) is { } bytes ? CaseParser.Parse(bytes, ReportRules.AnswersFile).Case : null;
        return new[] { fromAnswers, definition }.OfType<CaseDefinition>().Select(d => d.Steps).FirstOrDefault(steps => Same(steps, scorecard));
    }

    private static bool Same(IReadOnlyList<CaseStep> steps, ScorecardResult scorecard) =>
        steps.Count == scorecard.Steps.Count && steps.Zip(scorecard.Steps).All(p => p.First.Number == p.Second.Number && p.First.Name == p.Second.Name);

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    // UTF-8(앞 BOM 하나 뗌), 줄바꿈은 LF로.
    private static string Normalize(byte[] bytes)
    {
        var start = bytes is [0xEF, 0xBB, 0xBF, ..] ? 3 : 0;
        return Utf8.GetString(bytes, start, bytes.Length - start).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    private static SourceFile Text(string path, string text) => new(path, Utf8.GetBytes(text));

    private static byte[]? Find(IReadOnlyList<SourceFile> files, string path) => files.FirstOrDefault(f => f.Path == path)?.Content;

    private static string StepMessage(string template, int step, string assignee) =>
        template.Replace("{n}", step.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{a}", assignee, StringComparison.Ordinal);

    private static BuildRequestResult Fail(string message) =>
        new(null, [new CaseError(string.Empty, 0, 0, BuildRequestRules.StepError, BuildRequestRules.StepPath, message)]);

    private static ExportCheck Refuse(string status, string message) =>
        new(status, null, new CaseError(BuildRequestRules.ExportCheckFile, 0, 0, BuildRequestRules.ExportError, string.Empty, message));
}

// 번들 한 벌을 쓸 때 보는 값. Answers는 고른 단계의 답(쓸 수 없으면 null), Columns는 io-schema.json 출력 열.
internal sealed record BundleView(
    ScorecardResult Scorecard,
    ScorecardStep Step,
    StepAnswers? Answers,
    CaseDefinition? Definition,
    IReadOnlyList<BundleColumn> Columns);

// 출력 열 하나(4.2). Role: key, compare, checklist, group. Type: text, number.
internal sealed record BundleColumn(string Name, string Role, string Type, decimal? Tolerance, decimal? TolerancePct);
