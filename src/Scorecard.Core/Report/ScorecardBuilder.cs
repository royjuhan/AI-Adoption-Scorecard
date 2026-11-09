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
        string? confirmedIn = null;
        if (sources.Review is { } reviewFile)
        {
            var (file, reviewErrors) = ReviewParser.ParseFile(reviewFile.Content, reviewFile.Path, classification.Steps.Select(s => s.Number).ToArray());
            if (file is null)
            {
                return new ScorecardBuildResult(null, reviewErrors);
            }

            confirmedIn = file.ConfirmedIn;
            foreach (var entry in file.Entries)
            {
                reviews[entry.Number] = entry;
            }
        }

        var (answersStatus, answers, task, answerNotes) = ReadAnswers(sources.Inputs, classification);
        var caseDefinition = Find(sources.Inputs, CaseParser.FileName) is { } caseBytes ? CaseParser.Parse(caseBytes, CaseParser.FileName).Case : null;
        task ??= caseDefinition?.Task;
        var notes = answerNotes ?? caseDefinition?.Notes;
        var intake = Find(sources.Inputs, ReportRules.IntakeFile) is { } intakeBytes ? Text(intakeBytes) : null;

        var steps = classification.Steps.Select((s, i) => Step(s, answers?[i], reviews.GetValueOrDefault(s.Number))).ToArray();
        var summary = Summary(steps, answers);
        var proposal = Proposal(steps);
        var verification = Verification(metrics, judgment);
        var gates = Gates(steps);
        var questions = notes?.OpenQuestions ?? [];
        var scorecard = new ScorecardResult(
            classification.Case,
            task,
            notes?.Scenario,
            new ScorecardStatus(
                notes?.Scenario ?? ReportRules.MaterialUnknown,
                steps.Any(s => s.Source == StepClassifier.SourceAi) ? ReportRules.ClassificationAiDraft : ReportRules.ClassificationRuleTree,
                summary.Kept + summary.Changed,
                summary.Steps,
                confirmedIn,
                verification.Status),
            Conclusion(steps, summary, proposal, gates, questions, verification, notes?.EndState),
            notes?.Summary,
            intake,
            answersStatus,
            summary,
            steps,
            proposal,
            verification,
            gates,
            Risks(steps, judgment),
            Opinions(steps),
            Candidates(steps),
            Todo(classification.Case, steps, metrics, judgment, proposal, verification),
            notes?.StatedNumbers ?? [],
            questions,
            notes?.SelfCheck ?? [],
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
    // 케이스 메모(scenario 등, 4.13)는 파일을 읽을 수 있으면 단계가 달라도 쓴다(가상 시나리오 표시가 빠지지 않게).
    private static (string Status, StepAnswerView[]? Answers, string? Task, CaseNotes? Notes) ReadAnswers(IReadOnlyList<SourceFile> inputs, ClassificationResult classification)
    {
        if (Find(inputs, ReportRules.AnswersFile) is not { } bytes)
        {
            return (ReportRules.AnswersMissing, null, null, null);
        }

        if (CaseParser.Parse(bytes, ReportRules.AnswersFile).Case is not { } definition)
        {
            return (ReportRules.AnswersInvalid, null, null, null);
        }

        var same = definition.Steps.Count == classification.Steps.Count
            && definition.Steps.Zip(classification.Steps).All(p => p.First.Number == p.Second.Number && p.First.Name == p.Second.Name);
        if (!same)
        {
            return (ReportRules.AnswersMismatch, null, null, definition.Notes);
        }

        // 지금 하는 쪽은 메모리 분류에 없어 답에서 읽는다. 추정 키는 분류기의 Inferred(case-schema 4.4 순서)다.
        var classified = StepClassifier.Classify(definition).Steps
            .Zip(definition.Steps, (c, d) => new StepAnswerView(
                c.Inferred,
                d.Answers.CurrentActor.IsDefault ? null : d.Answers.CurrentActor.Value,
                d.Answers.MinutesPerItem is { IsDefault: false, Value: { } minutes } ? minutes : null,
                d.Answers))
            .ToArray();
        return (ReportRules.AnswersUsed, classified, definition.Task, definition.Notes);
    }

    // answer: 답에서 읽은 추정 키와 지금 하는 쪽(기본값이면 null).
    private static ScorecardStep Step(StepClassification s, StepAnswerView? answer, ReviewEntry? review)
    {
        var main = s.Rows[0];
        var actor = answer?.Actor;
        var inferred = answer?.Inferred ?? [];
        var detail = s.Detail;
        var reviewed = review switch
        {
            null => new StepReview(ReportRules.ReviewPending, null, null),
            { Answer: ReviewParser.Keep } => new StepReview(ReportRules.ReviewKept, null, review.Reason),
            _ => new StepReview(ReportRules.ReviewChanged, review.Assignee, review.Reason),
        };

        // 12.3 확인하면 바뀔 수 있음: 규칙 트리 단계는 근거 문장에 기본값·추정 표시가 있으면(트리가 읽은 답),
        // AI 판단 단계는 AiTentative.
        var tentative = s.Source == StepClassifier.SourceAi
            ? AiTentative(main.Assignee, inferred, detail?.Actions ?? [], answer is not null)
            : HasMark(main.Reason);

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
            inferred,
            s.Checks,
            s.AiOveruseCandidate,
            s.Rows.Skip(1).Select(r => new ScorecardRow(r.Kind, r.Assignee, r.Rule, r.Reason)).ToArray(),
            reviewed,
            tentative,
            detail?.Now ?? NowText(actor, inferred.Contains(StepQuestions.CurrentActor.Key)),
            detail?.Change ?? Lookup(ReportRules.ChangeTexts, main.Assignee),
            detail?.Check ?? Lookup(ReportRules.CheckTexts, main.Assignee),
            Assumptions(inferred, s.Defaults, answer?.Answers),
            detail?.Actions ?? [],
            detail?.GateNote);
    }

    // AI 판단 단계(12.3): 추정 답이 있으면 참. 동작이 있으면, 출처 stated이고 proposed가 단계 담당과 같은 동작이 없을 때 참.
    // 답 파일을 못 쓰면(answers ≠ used) 추정 답을 알 수 없으니 stated가 아닌 동작이 하나라도 있으면 참. 동작이 없으면 추정 답만 본다.
    private static bool AiTentative(string assignee, IReadOnlyList<string> inferred, IReadOnlyList<StepAction> actions, bool answersUsed)
    {
        if (inferred.Count > 0)
        {
            return true;
        }

        if (actions.Count == 0)
        {
            return false;
        }

        if (!answersUsed && actions.Any(a => a.Source != ActionStated))
        {
            return true;
        }

        return !actions.Any(a => a.Source == ActionStated && a.Proposed == assignee);
    }

    // 근거 문장 끝에 기본값·추정 표시가 있다(classification.md 4.6·4.12).
    private static bool HasMark(string reason) =>
        reason.Contains(ClassificationRules.DefaultMark, StringComparison.Ordinal) || reason.Contains(ClassificationRules.InferredMark, StringComparison.Ordinal);

    private static string NowText(string? actor, bool inferred)
    {
        if (actor is null)
        {
            return ReportRules.NowUnknownText;
        }

        var (_, name, text) = ReportRules.Actors.FirstOrDefault(a => a.Actor == actor);
        return inferred ? ReportRules.NowInferredText.Replace("{actor}", name ?? actor, StringComparison.Ordinal) : text ?? actor;
    }

    private static string Lookup((string Assignee, string Text)[] table, string assignee) =>
        table.FirstOrDefault(t => t.Assignee == assignee).Text ?? string.Empty;

    // 가정한 내용(4.12): 추정 답(값은 답 파일), 그다음 기본값 답(값은 기본값). 건당 시간 기본값(null)은 뺀다(측정 할 일이 따로 있다).
    private static string[] Assumptions(IReadOnlyList<string> inferred, IReadOnlyList<string> defaults, StepAnswers? answers)
    {
        var lines = new List<string>();
        if (answers is not null)
        {
            lines.AddRange(inferred.Select(k => Words(ReportRules.AssumedText, k, AnswerValue(answers, k))));
        }

        lines.AddRange(defaults.Where(k => k != ReportRules.MinutesPerItem).Select(k => Words(ReportRules.DefaultedText, k, DefaultValue(k))));
        return lines.ToArray();
    }

    private static string Words(string template, string key, string value) =>
        template.Replace("{label}", ReportRules.AnswerLabel(key), StringComparison.Ordinal)
            .Replace("{value}", ReportRules.AnswerValue(key, value), StringComparison.Ordinal);

    // 답 값의 글자(숫자는 InvariantCulture). 키는 StepQuestions.Keys 가운데 하나다.
    private static string AnswerValue(StepAnswers a, string key) => key switch
    {
        "input_kind" => a.InputKind.Value,
        "rule_expressible" => a.RuleExpressible.Value,
        "frequency" => a.Frequency.Value,
        "batch_size" => a.BatchSize.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "exception_rate" => a.ExceptionRate.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "current_actor" => a.CurrentActor.Value,
        "minutes_per_item" => a.MinutesPerItem.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
        "failure_impact" => a.FailureImpact.Value,
        "existing_feature" => a.ExistingFeature.Value,
        _ => a.HumanSignoff.Value,
    };

    // 기본값(case-schema 4.4 표, StepQuestions).
    private static string DefaultValue(string key) => key switch
    {
        "input_kind" => StepQuestions.InputKind.Default,
        "rule_expressible" => StepQuestions.RuleExpressible.Default,
        "frequency" => StepQuestions.Frequency.Default,
        "batch_size" => StepQuestions.BatchSize.Default.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "exception_rate" => StepQuestions.ExceptionRate.Default.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "current_actor" => StepQuestions.CurrentActor.Default,
        "failure_impact" => StepQuestions.FailureImpact.Default,
        "existing_feature" => StepQuestions.ExistingFeature.Default,
        _ => StepQuestions.HumanSignoff.Default,
    };

    // 요약(4.1). Stated·Tentative는 단계의 Tentative로 나눈다. AI 과잉 후보는 지금 하는 쪽이 추정이어도 Tentative다.
    private static ScorecardSummary Summary(IReadOnlyList<ScorecardStep> steps, StepAnswerView[]? answers)
    {
        SummaryCounts Count(Func<ScorecardStep, bool> side, Func<ScorecardStep, bool> overuseSide) => new(
            steps.Count(side),
            steps.Count(s => side(s) && ReportRules.SoftwareAssignees.Contains(s.Assignee)),
            steps.Count(s => side(s) && s.Assignee == "③"),
            steps.Count(s => s.AiOveruseCandidate && overuseSide(s)));

        return new ScorecardSummary(
            steps.Count,
            steps.Count(s => ReportRules.SoftwareAssignees.Contains(s.Assignee)),
            ReportRules.AssigneeOrder.Select(a => new AssigneeCount(a, steps.Count(s => s.Assignee == a))).ToArray(),
            steps.Count(s => s.Differs),
            steps.Count(s => s.Assignee == "③"),
            steps.Count(s => s.AiOveruseCandidate),
            steps.Count(s => s.Review.Status == ReportRules.ReviewKept),
            steps.Count(s => s.Review.Status == ReportRules.ReviewChanged),
            ReportRules.SavingTimeNeeded,
            Count(s => !s.Tentative, s => !OveruseTentative(s)),
            Count(s => s.Tentative, OveruseTentative),
            steps.Zip(answers ?? new StepAnswerView?[steps.Count])
                .Where(p => p.Second?.Minutes is not null)
                .Select(p => new StepMinutes(p.First.Number, p.Second!.Minutes!.Value))
                .ToArray());
    }

    private static bool OveruseTentative(ScorecardStep s) => s.Tentative || s.Inferred.Contains(StepQuestions.CurrentActor.Key);

    // 먼저 확인할 ② 단계(4.4): ②·②M 단계 가운데 확인하면 바뀔 수 없는 단계 먼저, 지금 하는 쪽 순서(ProposalOrder), 같으면 번호가 작은 것.
    private static ScorecardProposal? Proposal(IReadOnlyList<ScorecardStep> steps)
    {
        var pick = steps
            .Where(s => ReportRules.SoftwareAssignees.Contains(s.Assignee))
            .OrderBy(s => s.Tentative ? 1 : 0)
            .ThenBy(s => Rank(s.CurrentActor))
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

        var effect = pick.CurrentActor == ReportRules.ProposalOrder[0].Actor && !OveruseTentative(pick) ? ReportRules.ProposalEffectOveruse : ReportRules.ProposalEffect;
        if (pick.Tentative)
        {
            effect += ReportRules.ProposalTentativeTail;
        }

        return new ScorecardProposal(pick.Number, pick.Name, text, pick.Check, effect, pick.Tentative);
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

    // 담당자 확인(4.6): 서명 단계(규칙 의견 SIGNOFF이고 담당 ④, AI 판단 단계 포함), 예외 검토 행, 사람 게이트 행. 단계 순서, 단계 안은 행 순서.
    // 화면 문장(Text): 사람 게이트 행은 그 단계 AI 판단의 gate_note, 없으면 종류마다 정한 문장(T-022, 지시문 4.1·4.4).
    private static ScorecardGate[] Gates(IReadOnlyList<ScorecardStep> steps) =>
        steps.SelectMany(s =>
            (s.RuleOpinion.Rule == "SIGNOFF" && s.Assignee == "④" ? [Gate(s, ReportRules.GateSignoff, s.Reason)] : Array.Empty<ScorecardGate>())
            .Concat(s.Extra.Select(r => Gate(s, r.Kind, r.Reason))))
        .ToArray();

    private static ScorecardGate Gate(ScorecardStep s, string kind, string reason)
    {
        var text = kind == StepClassifier.KindHumanGate && s.GateNote is { } note
            ? note
            : ReportRules.GateTexts.FirstOrDefault(g => g.Kind == kind).Text ?? reason;
        return new ScorecardGate(s.Number, s.Name, kind, reason, text);
    }

    // 결론 세 줄(4.11).
    private static ScorecardConclusion Conclusion(
        IReadOnlyList<ScorecardStep> steps, ScorecardSummary summary, ScorecardProposal? proposal, IReadOnlyList<ScorecardGate> gates,
        IReadOnlyList<string> questions, ScorecardVerification verification, string? endState)
    {
        var keep = new List<string>();
        var human = steps.Where(s => s.Assignee == "④").Select(s => s.Number).ToArray();
        if (human.Length > 0)
        {
            keep.Add(ReportRules.KeepHumanText.Replace("{steps}", ReportRules.StepList(human), StringComparison.Ordinal));
        }

        var existing = steps.Where(s => s.Assignee == "①").Select(s => s.Number).ToArray();
        if (existing.Length > 0)
        {
            keep.Add(ReportRules.KeepExistingText.Replace("{steps}", ReportRules.StepList(existing), StringComparison.Ordinal));
        }

        // 담당자 확인(서명 단계 빼고). 엔진 문장에 기본값·추정 표시가 있으면 유지할 것이 아니라 아직 판단하지 않은 것이다(12.2).
        var checks = gates.Where(g => g.Kind != ReportRules.GateSignoff).ToArray();
        var firmChecks = checks.Count(g => !HasMark(g.Reason));
        var tentativeChecks = checks.Length - firmChecks;
        if (firmChecks > 0)
        {
            keep.Add(ReportRules.KeepGatesText.Replace("{n}", N(firmChecks), StringComparison.Ordinal));
        }

        var first = proposal is not null
            ? ReportRules.FirstCheckText.Replace("{n}", N(proposal.Step), StringComparison.Ordinal)
                .Replace("{name}", proposal.Name, StringComparison.Ordinal).Replace("{check}", proposal.Check, StringComparison.Ordinal)
            : questions.Count > 0 ? questions[0] : ReportRules.FirstCheckNoneText;

        var undecided = new List<string>();
        if (summary.Tentative.Steps > 0)
        {
            undecided.Add(ReportRules.UndecidedTentativeText.Replace("{n}", N(summary.Tentative.Steps), StringComparison.Ordinal));
        }

        if (tentativeChecks > 0)
        {
            undecided.Add(ReportRules.UndecidedGatesText.Replace("{n}", N(tentativeChecks), StringComparison.Ordinal));
        }

        if (questions.Count > 0)
        {
            undecided.Add(ReportRules.UndecidedQuestionsText.Replace("{n}", N(questions.Count), StringComparison.Ordinal));
        }

        if (verification.Status != MeasuredStatus)
        {
            undecided.Add(ReportRules.UndecidedTestText);
        }

        undecided.Add(ReportRules.UndecidedSavingText);
        return new ScorecardConclusion(
            keep.Count > 0 ? string.Join(" ", keep) : ReportRules.KeepNoneText,
            first,
            string.Join(" ", undecided),
            endState);
    }

    // 남는 위험(4.7). 무엇이 틀릴 수 있고 어떻게 잡는가만. 단계 순서로 AI 보조 → 확인에서 바꿈, 끝에 판정.
    private static ScorecardNote[] Risks(IReadOnlyList<ScorecardStep> steps, JudgmentResult? judgment)
    {
        var notes = new List<ScorecardNote>();
        foreach (var s in steps)
        {
            if (s.Assignee == "③")
            {
                notes.Add(new(ReportRules.RiskAiAssist, s.Number, Fill(ReportRules.RiskAiAssistText, s)));
            }

            if (s.Review.Status == ReportRules.ReviewChanged)
            {
                notes.Add(new(ReportRules.RiskReviewChanged, s.Number, Fill(ReportRules.RiskReviewChangedText, s, ("{old}", ReportRules.AssigneeName(s.Assignee)), ("{new}", ReportRules.AssigneeName(s.Review.Assignee!)))));
            }
        }

        if (judgment is not null && ReportRules.RiskVerdicts.FirstOrDefault(v => v.Verdict == judgment.Verdict).Name is { } name)
        {
            notes.Add(new(ReportRules.RiskVerdict, 0, ReportRules.RiskVerdictText.Replace("{verdict}", name, StringComparison.Ordinal).Replace("{reason}", judgment.Reason, StringComparison.Ordinal)));
        }

        return notes.ToArray();
    }

    // 의견 차이(4.7): AI 판단과 규칙 의견이 다른 단계. 추천 결론과 이유를 먼저 쓴다.
    private static ScorecardNote[] Opinions(IReadOnlyList<ScorecardStep> steps) =>
        steps.Where(s => s.Differs)
            .Select(s => new ScorecardNote(ReportRules.OpinionDiffers, s.Number, Fill(ReportRules.OpinionText, s, ("{a}", ReportRules.AssigneeName(s.Assignee)), ("{reason}", s.Reason))))
            .ToArray();

    // 개선 후보(4.7): AI 과잉 후보. 지금 하는 쪽이 추정이면 추정형 문장.
    private static ScorecardNote[] Candidates(IReadOnlyList<ScorecardStep> steps) =>
        steps.Where(s => s.AiOveruseCandidate)
            .Select(s => new ScorecardNote(ReportRules.CandidateOveruse, s.Number, Fill(OveruseTentative(s) ? ReportRules.CandidateOveruseTentativeText : ReportRules.CandidateOveruseText, s)))
            .ToArray();

    // 다음 할 일(4.8): 묶음 순서 now → improve → verify → implementer. 묶음 안은 아래 순서.
    private static ScorecardTodo[] Todo(
        string caseName, IReadOnlyList<ScorecardStep> steps, MetricsView? metrics, JudgmentResult? judgment, ScorecardProposal? proposal, ScorecardVerification verification)
    {
        var notes = new List<ScorecardTodo>();
        var pending = steps.Where(s => s.Review.Status == ReportRules.ReviewPending).Select(s => s.Number).ToArray();
        if (pending.Length > 0)
        {
            notes.Add(new(ReportRules.TodoNow, ReportRules.TodoReviewSteps, 0, ReportRules.TodoReviewStepsText.Replace("{steps}", ReportRules.StepList(pending), StringComparison.Ordinal)));
        }

        if (proposal is not null)
        {
            notes.Add(new(ReportRules.TodoImprove, ReportRules.TodoFirstCheck, proposal.Step, ReportRules.TodoFirstCheckText));
        }

        var inferred = KeyList(steps, s => s.Inferred);
        if (inferred.Length > 0)
        {
            notes.Add(new(ReportRules.TodoImprove, ReportRules.TodoConfirmInferred, 0, ReportRules.TodoConfirmInferredText.Replace("{list}", inferred, StringComparison.Ordinal)));
        }

        var defaults = KeyList(steps, s => s.Defaults.Where(k => k != ReportRules.MinutesPerItem).ToArray());
        if (defaults.Length > 0)
        {
            notes.Add(new(ReportRules.TodoImprove, ReportRules.TodoConfirmDefaults, 0, ReportRules.TodoConfirmDefaultsText.Replace("{list}", defaults, StringComparison.Ordinal)));
        }

        notes.AddRange((judgment?.Todo ?? []).Select(t => new ScorecardTodo(ReportRules.TodoVerify, t.Key, 0, t.Text)));
        if (metrics is null)
        {
            notes.Add(new(ReportRules.TodoVerify, ReportRules.TodoRunCheck, 0, ReportRules.TodoRunCheckText));
        }

        if (verification.Status != MeasuredStatus)
        {
            notes.Add(new(ReportRules.TodoVerify, ReportRules.TodoDefineErrors, 0, ReportRules.TodoDefineErrorsText));
        }

        var measure = steps.Where(s => s.Checks.Contains(MeasureCheck)).Select(s => s.Number).ToArray();
        if (measure.Length > 0)
        {
            notes.Add(new(ReportRules.TodoVerify, ReportRules.TodoMeasureMinutes, 0, ReportRules.TodoMeasureMinutesText.Replace("{steps}", ReportRules.StepList(measure), StringComparison.Ordinal)));
        }

        if (verification.Status != MeasuredStatus)
        {
            notes.Add(new(ReportRules.TodoImplementer, ReportRules.TodoCheckCommand, 0, ReportRules.TodoCheckCommandText));
        }

        if (pending.Length > 0)
        {
            notes.Add(new(ReportRules.TodoImplementer, ReportRules.TodoReviewFile, 0, ReportRules.TodoReviewFileText));
        }

        if (proposal is not null)
        {
            notes.Add(new(ReportRules.TodoImplementer, ReportRules.TodoBuildCommand, proposal.Step, ReportRules.TodoBuildCommandText.Replace("{n}", N(proposal.Step), StringComparison.Ordinal).Replace("{case}", caseName, StringComparison.Ordinal)));
        }

        return notes.ToArray();
    }

    private static string N(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // "1단계 기존 기능이 있나; 3단계 처리 주기, 한 번에 처리하는 건수"(키는 업무 말 이름으로)
    private static string KeyList(IReadOnlyList<ScorecardStep> steps, Func<ScorecardStep, IReadOnlyList<string>> keys) =>
        string.Join("; ", steps.Select(s => (s.Number, Keys: keys(s))).Where(p => p.Keys.Count > 0)
            .Select(p => ReportRules.StepList([p.Number]) + " " + string.Join(", ", p.Keys.Select(ReportRules.AnswerLabel))));

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

    // 단계 하나의 답에서 쓰는 값. Minutes는 사용자가 준 건당 시간(기본값이면 null).
    private sealed record StepAnswerView(IReadOnlyList<string> Inferred, string? Actor, decimal? Minutes, StepAnswers Answers);

    private const string ActionStated = "stated";
    private const string MeasuredStatus = "measured";

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
