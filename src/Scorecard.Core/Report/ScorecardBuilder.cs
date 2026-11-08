using System.Security.Cryptography;
using System.Text;
using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;

namespace Scorecard.Core.Report;

// 저장된 결과와 케이스 입력으로 스코어카드 결과 객체를 만든다. docs/spec/report.md 2·4절.
// Build는 순수 함수다(바이트만 받는다). ReadDirectory가 파일을 찾아 읽는다(읽기만 한다).
// 분류·대조·판정을 다시 하지 않는다. 결과 파일 값을 그대로 옮기고, 셈(요약)과 문장(제안·위험·할 일)만 더한다.
public static class ScorecardBuilder
{
    public const string FileError = "file";

    // 출력 폴더에서 읽는 결과 파일. 이 순서로 results에 적는다. classification.json만 꼭 있어야 한다.
    public static readonly IReadOnlyList<string> ResultFiles = [ClassificationJson.FileName, MetricsJson.FileName, JudgmentJson.FileName];

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    // 케이스 폴더의 입력 파일과 출력 폴더의 결과 파일, 확인 파일을 읽는다(4.9).
    // 입력: case.yaml, answers.yaml, intake.md(있으면), case.yaml inputs·scores 패턴에 맞는 파일. 경로는 케이스 폴더 기준 상대 경로다.
    // 오류(읽기 실패, 확인 파일 없음)는 file 오류다. 예외 메시지에는 절대 경로가 있어 타입 이름만 쓴다.
    public static (ScorecardSources? Sources, IReadOnlyList<CaseError> Errors) ReadDirectory(string caseDirectory, string outputDirectory, string? reviewPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(caseDirectory);
        ArgumentException.ThrowIfNullOrEmpty(outputDirectory);

        var errors = new List<CaseError>();
        var results = new List<SourceFile>();
        var outputNames = Names(outputDirectory);
        foreach (var name in ResultFiles.Where(outputNames.Contains))
        {
            if (TryRead(Path.Combine(outputDirectory, name), name, errors) is { } bytes)
            {
                results.Add(new SourceFile(name, bytes));
            }
        }

        var inputs = new List<SourceFile>();
        var caseNames = Names(caseDirectory);
        foreach (var name in new[] { CaseParser.FileName, ReportRules.AnswersFile, ReportRules.IntakeFile }.Where(caseNames.Contains))
        {
            if (TryRead(Path.Combine(caseDirectory, name), name, errors) is { } bytes)
            {
                inputs.Add(new SourceFile(name, bytes));
            }
        }

        if (inputs.FirstOrDefault(f => f.Path == CaseParser.FileName) is { } caseFile
            && CaseParser.Parse(caseFile.Content, CaseParser.FileName).Case is { } definition)
        {
            // 패턴에 맞는 자료 파일. 대조 오류는 check가 낸다. 여기서는 찾은 파일만 해시한다.
            if (definition.Inputs is { } caseInputs && CompareInputs.Read(caseDirectory, caseInputs).Sources is { } data)
            {
                inputs.AddRange(data.Reference);
                inputs.AddRange(data.Candidate);
            }

            if (definition.Scores is { } scores
                && CompareInputs.Read(caseDirectory, new CaseInputs(scores.Sheet, scores.Sources, string.Empty)).Sources is { } sheets)
            {
                inputs.AddRange(sheets.Reference);
                inputs.AddRange(sheets.Candidate);
            }
        }

        SourceFile? review = null;
        if (reviewPath is not null)
        {
            var name = Path.GetFileName(reviewPath);
            if (!File.Exists(reviewPath))
            {
                errors.Add(new CaseError(name, 0, 0, FileError, string.Empty, "파일이 없다"));
            }
            else if (TryRead(reviewPath, name, errors) is { } bytes)
            {
                review = new SourceFile(name, bytes);
            }
        }

        return errors.Count > 0 ? (null, errors) : (new ScorecardSources(results, inputs, review), []);
    }

    // 결과 객체를 만든다. 오류가 있으면 Scorecard는 null이다.
    public static ScorecardBuildResult Build(ScorecardSources sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var errors = new List<CaseError>();
        if (Find(sources.Results, ClassificationJson.FileName) is not { } classificationBytes)
        {
            return new ScorecardBuildResult(null, [new CaseError(ClassificationJson.FileName, 0, 0, FileError, string.Empty, "파일이 없다. start를 먼저 돌린다")]);
        }

        var (classification, classificationError) = ResultReader.Classification(classificationBytes);
        Add(errors, classificationError);
        MetricsView? metrics = null;
        if (Find(sources.Results, MetricsJson.FileName) is { } metricsBytes)
        {
            (metrics, var e) = ResultReader.Metrics(metricsBytes);
            Add(errors, e);
        }

        JudgmentResult? judgment = null;
        if (Find(sources.Results, JudgmentJson.FileName) is { } judgmentBytes)
        {
            (judgment, var e) = ResultReader.Judgment(judgmentBytes);
            Add(errors, e);
        }

        if (errors.Count > 0 || classification is null)
        {
            return new ScorecardBuildResult(null, errors);
        }

        var reviews = new Dictionary<int, ReviewEntry>();
        if (sources.Review is { } reviewFile)
        {
            var (entries, reviewErrors) = ReviewParser.Parse(reviewFile.Content, reviewFile.Path, classification.Steps.Select(s => s.Number).ToArray());
            if (entries is null)
            {
                return new ScorecardBuildResult(null, reviewErrors);
            }

            foreach (var entry in entries)
            {
                reviews[entry.Number] = entry;
            }
        }

        var (answersStatus, answers, task) = ReadAnswers(sources.Inputs, classification);
        task ??= Find(sources.Inputs, CaseParser.FileName) is { } caseBytes ? CaseParser.Parse(caseBytes, CaseParser.FileName).Case?.Task : null;
        var intake = Find(sources.Inputs, ReportRules.IntakeFile) is { } intakeBytes ? Text(intakeBytes) : null;

        var steps = classification.Steps.Select((s, i) => Step(s, answers?[i], reviews.GetValueOrDefault(s.Number))).ToArray();
        var scorecard = new ScorecardResult(
            classification.Case,
            task,
            intake,
            answersStatus,
            Summary(steps),
            steps,
            Proposal(steps),
            Verification(metrics, judgment),
            Gates(steps),
            Risks(steps, judgment),
            Todo(steps, metrics, judgment),
            sources.Inputs.GroupBy(f => f.Path, StringComparer.Ordinal).Select(g => Hash(g.First())).OrderBy(h => h.Path, StringComparer.Ordinal).ToArray(),
            ResultFiles.Select(n => sources.Results.FirstOrDefault(f => f.Path == n)).OfType<SourceFile>().Select(Hash).ToArray(),
            sources.Review is { } r ? Hash(r) : null);
        return new ScorecardBuildResult(scorecard, []);
    }

    // SHA-256, 소문자 16진. 바이트에서 낸다(4.9).
    public static FileHash Hash(SourceFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new FileHash(file.Path, Convert.ToHexStringLower(SHA256.HashData(file.Content)));
    }

    // answers.yaml이 classification.json과 같은 단계(수·번호·이름)면 단계마다 답을 쓴다(4.3).
    private static (string Status, StepAnswerView[]? Answers, string? Task) ReadAnswers(IReadOnlyList<SourceFile> inputs, ClassificationResult classification)
    {
        if (Find(inputs, ReportRules.AnswersFile) is not { } bytes)
        {
            return (ReportRules.AnswersMissing, null, null);
        }

        if (CaseParser.Parse(bytes, ReportRules.AnswersFile).Case is not { } definition)
        {
            return (ReportRules.AnswersInvalid, null, null);
        }

        var same = definition.Steps.Count == classification.Steps.Count
            && definition.Steps.Zip(classification.Steps).All(p => p.First.Number == p.Second.Number && p.First.Name == p.Second.Name);
        if (!same)
        {
            return (ReportRules.AnswersMismatch, null, null);
        }

        // 지금 하는 쪽은 메모리 분류에 없어 답에서 읽는다. 추정 키는 분류기의 Inferred(case-schema 4.4 순서)다.
        var classified = StepClassifier.Classify(definition).Steps
            .Zip(definition.Steps, (c, d) => new StepAnswerView(c.Inferred, d.Answers.CurrentActor.IsDefault ? null : d.Answers.CurrentActor.Value))
            .ToArray();
        return (ReportRules.AnswersUsed, classified, definition.Task);
    }

    // answer: 답에서 읽은 추정 키와 지금 하는 쪽(기본값이면 null).
    private static ScorecardStep Step(StepClassification s, StepAnswerView? answer, ReviewEntry? review)
    {
        var main = s.Rows[0];
        var actor = answer?.Actor;
        var reviewed = review switch
        {
            null => new StepReview(ReportRules.ReviewPending, null, null),
            { Answer: ReviewParser.Keep } => new StepReview(ReportRules.ReviewKept, null, review.Reason),
            _ => new StepReview(ReportRules.ReviewChanged, review.Assignee, review.Reason),
        };
        return new ScorecardStep(
            s.Number,
            s.Name,
            s.Source,
            main.Assignee,
            main.Rule,
            main.Reason,
            s.RuleOpinion,
            s.Differs,
            actor,
            s.Defaults,
            answer?.Inferred ?? [],
            s.Checks,
            s.AiOveruseCandidate,
            s.Rows.Skip(1).Select(r => new ScorecardRow(r.Kind, r.Assignee, r.Rule, r.Reason)).ToArray(),
            reviewed);
    }

    private static ScorecardSummary Summary(IReadOnlyList<ScorecardStep> steps) => new(
        steps.Count,
        steps.Count(s => ReportRules.SoftwareAssignees.Contains(s.Assignee)),
        ReportRules.AssigneeOrder.Select(a => new AssigneeCount(a, steps.Count(s => s.Assignee == a))).ToArray(),
        steps.Count(s => s.Differs),
        steps.Count(s => s.Assignee == "③"),
        steps.Count(s => s.AiOveruseCandidate),
        steps.Count(s => s.Review.Status == ReportRules.ReviewKept),
        steps.Count(s => s.Review.Status == ReportRules.ReviewChanged),
        ReportRules.SavingTimeNeeded);

    // 먼저 만들 ② 단계(4.4): ②·②M 단계 가운데 지금 하는 쪽 순서(ProposalOrder), 같으면 번호가 작은 것.
    private static ScorecardProposal? Proposal(IReadOnlyList<ScorecardStep> steps)
    {
        var pick = steps
            .Where(s => ReportRules.SoftwareAssignees.Contains(s.Assignee))
            .OrderBy(s => Rank(s.CurrentActor))
            .ThenBy(s => s.Number)
            .FirstOrDefault();
        if (pick is null)
        {
            return null;
        }

        var rank = Rank(pick.CurrentActor);
        var text = rank < ReportRules.ProposalOrder.Length ? ReportRules.ProposalOrder[rank].Text : ReportRules.ProposalUnknown;
        if (pick.Extra.Any(r => r.Kind == StepClassifier.KindExceptionReview))
        {
            text += ReportRules.ProposalExceptionTail;
        }

        return new ScorecardProposal(pick.Number, pick.Name, text);
    }

    private static int Rank(string? actor)
    {
        var index = Array.FindIndex(ReportRules.ProposalOrder, p => p.Actor == actor);
        return index < 0 ? ReportRules.ProposalOrder.Length : index;
    }

    private static ScorecardVerification Verification(MetricsView? metrics, JudgmentResult? judgment)
    {
        var items = metrics?.Items ?? [];
        return new ScorecardVerification(
            metrics?.Status ?? ReportRules.NotChecked,
            metrics?.Reason,
            metrics?.Counts,
            metrics?.AccuracyPct,
            metrics?.MissingRatePct,
            judgment?.Verdict,
            judgment?.Rule,
            judgment?.Reason,
            judgment?.Thresholds,
            judgment?.Checks.FirstOrDefault(c => c.Rule == "TIME"),
            judgment?.Checks ?? [],
            items.Take(ReportRules.ItemLimit).ToArray(),
            Math.Max(0, items.Count - ReportRules.ItemLimit));
    }

    // 사람 게이트(4.6): 서명 단계(규칙 의견 SIGNOFF이고 담당 ④, AI 판단 단계 포함), 예외 검토 행, 사람 게이트 행. 단계 순서, 단계 안은 행 순서.
    private static ScorecardGate[] Gates(IReadOnlyList<ScorecardStep> steps) =>
        steps.SelectMany(s =>
            (s.RuleOpinion.Rule == "SIGNOFF" && s.Assignee == "④" ? [new ScorecardGate(s.Number, s.Name, ReportRules.GateSignoff, s.Reason)] : Array.Empty<ScorecardGate>())
            .Concat(s.Extra.Select(r => new ScorecardGate(s.Number, s.Name, r.Kind, r.Reason))))
        .ToArray();

    // 남는 위험(4.7). 단계 순서로 다름 → AI 과잉 → AI 보조 → 확인에서 바꿈, 끝에 판정.
    private static ScorecardNote[] Risks(IReadOnlyList<ScorecardStep> steps, JudgmentResult? judgment)
    {
        var notes = new List<ScorecardNote>();
        foreach (var s in steps)
        {
            if (s.Differs)
            {
                notes.Add(new(ReportRules.RiskDiffers, s.Number, Fill(ReportRules.RiskDiffersText, s, ("{a}", s.Assignee), ("{r}", s.RuleOpinion.Assignee))));
            }

            if (s.AiOveruseCandidate)
            {
                notes.Add(new(ReportRules.RiskOveruse, s.Number, Fill(ReportRules.RiskOveruseText, s)));
            }

            if (s.Assignee == "③")
            {
                notes.Add(new(ReportRules.RiskAiAssist, s.Number, Fill(ReportRules.RiskAiAssistText, s)));
            }

            if (s.Review.Status == ReportRules.ReviewChanged)
            {
                notes.Add(new(ReportRules.RiskReviewChanged, s.Number, Fill(ReportRules.RiskReviewChangedText, s, ("{old}", s.Assignee), ("{new}", s.Review.Assignee!))));
            }
        }

        if (judgment is not null && ReportRules.RiskVerdicts.FirstOrDefault(v => v.Verdict == judgment.Verdict).Name is { } name)
        {
            notes.Add(new(ReportRules.RiskVerdict, 0, ReportRules.RiskVerdictText.Replace("{verdict}", name, StringComparison.Ordinal).Replace("{reason}", judgment.Reason, StringComparison.Ordinal)));
        }

        return notes.ToArray();
    }

    // 다음 할 일(4.8): judgment todo 그대로 → check 안 돌림 → 건당 시간 → 기본값 → 추정 → 확인 전 단계.
    private static ScorecardNote[] Todo(IReadOnlyList<ScorecardStep> steps, MetricsView? metrics, JudgmentResult? judgment)
    {
        var notes = new List<ScorecardNote>();
        notes.AddRange((judgment?.Todo ?? []).Select(t => new ScorecardNote(t.Key, 0, t.Text)));
        if (metrics is null)
        {
            notes.Add(new(ReportRules.TodoRunCheck, 0, ReportRules.TodoRunCheckText));
        }

        var measure = steps.Where(s => s.Checks.Contains(MeasureCheck)).Select(s => s.Number).ToArray();
        if (measure.Length > 0)
        {
            notes.Add(new(ReportRules.TodoMeasureMinutes, 0, ReportRules.TodoMeasureMinutesText.Replace("{steps}", ReportRules.StepList(measure), StringComparison.Ordinal)));
        }

        var defaults = KeyList(steps, s => s.Defaults.Where(k => k != ReportRules.MinutesPerItem).ToArray());
        if (defaults.Length > 0)
        {
            notes.Add(new(ReportRules.TodoConfirmDefaults, 0, ReportRules.TodoConfirmDefaultsText.Replace("{list}", defaults, StringComparison.Ordinal)));
        }

        var inferred = KeyList(steps, s => s.Inferred);
        if (inferred.Length > 0)
        {
            notes.Add(new(ReportRules.TodoConfirmInferred, 0, ReportRules.TodoConfirmInferredText.Replace("{list}", inferred, StringComparison.Ordinal)));
        }

        var pending = steps.Where(s => s.Review.Status == ReportRules.ReviewPending).Select(s => s.Number).ToArray();
        if (pending.Length > 0)
        {
            notes.Add(new(ReportRules.TodoReviewSteps, 0, ReportRules.TodoReviewStepsText.Replace("{steps}", ReportRules.StepList(pending), StringComparison.Ordinal)));
        }

        return notes.ToArray();
    }

    // "1단계 existing_feature; 3단계 frequency, batch_size"
    private static string KeyList(IReadOnlyList<ScorecardStep> steps, Func<ScorecardStep, IReadOnlyList<string>> keys) =>
        string.Join("; ", steps.Select(s => (s.Number, Keys: keys(s))).Where(p => p.Keys.Count > 0)
            .Select(p => ReportRules.StepList([p.Number]) + " " + string.Join(", ", p.Keys)));

    private static string Fill(string template, ScorecardStep s, params (string Key, string Value)[] more)
    {
        var text = template
            .Replace("{n}", s.Number.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{name}", s.Name, StringComparison.Ordinal);
        foreach (var (key, value) in more)
        {
            text = text.Replace(key, value, StringComparison.Ordinal);
        }

        return text;
    }

    private static void Add(List<CaseError> errors, CaseError? error)
    {
        if (error is not null)
        {
            errors.Add(error);
        }
    }

    // 단계 하나의 답에서 쓰는 값.
    private sealed record StepAnswerView(IReadOnlyList<string> Inferred, string? Actor);

    private const string MeasureCheck = ClassificationRules.CheckMeasureMinutesPerItem;

    private static byte[]? Find(IReadOnlyList<SourceFile> files, string path) => files.FirstOrDefault(f => f.Path == path)?.Content;

    // UTF-8(앞 BOM 하나 뗌). 줄바꿈은 LF로 맞춘다(OS·clone 설정과 상관없이 같은 바이트).
    private static string Text(byte[] bytes)
    {
        var start = bytes is [0xEF, 0xBB, 0xBF, ..] ? 3 : 0;
        return Utf8.GetString(bytes, start, bytes.Length - start).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd('\n');
    }

    // 폴더 이름 목록(서수). 폴더가 없으면 빈 목록.
    private static HashSet<string> Names(string folder) =>
        Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder).Select(Path.GetFileName).OfType<string>().ToHashSet(StringComparer.Ordinal)
            : [];

    private static byte[]? TryRead(string path, string name, List<CaseError> errors)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errors.Add(new CaseError(name, 0, 0, FileError, string.Empty, "파일을 읽을 수 없다: " + ex.GetType().Name));
            return null;
        }
    }
}
