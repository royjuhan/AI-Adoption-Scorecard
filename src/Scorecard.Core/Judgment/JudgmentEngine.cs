using System.Numerics;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using static Scorecard.Core.Judgment.JudgmentRules;

namespace Scorecard.Core.Judgment;

// 판정. docs/spec/judgment.md 4.1~4.10, 6.5, 지시문 3.4.
// 대조가 미검증이면 판정도 미검증이다. 아니면 확인 다섯(정확도, 누락률, 설명 안 되는 검토 항목, 시간, 몰림)을 모두 계산하고
// 판정 표(4.3)를 위에서부터 보고 처음 걸리는 행에서 멈춘다: 보류 → 조건부 → 통과.
// 검토 항목(4.9)은 불일치(값 불일치·누락) + 추가 행 + 결과 쪽 중복 행이다. 몰림은 불일치만 본다.
// 문턱 비교는 셈(counts)으로 한다. 표시 퍼센트로 비교하지 않는다. 나눗셈을 쓰지 않는다.
// 점수표(text-check.md 5절)가 있으면 SCORE가 여섯째 확인이다. 판정은 순서 4 뒤에 SCORE 보류 두 행이 끼인다.
// 대조가 미검증이고 점수표가 있으면(점수표만) 확인은 TIME, SCORE 둘이고 판정은 5.3 점수표만 행이다.
// 순수 함수다. 파일·난수·시각·환경·현재 문화권을 쓰지 않는다. 입력을 바꾸지 않는다. 값·문장은 JudgmentRules에서 읽는다.
public static class JudgmentEngine
{
    public const string VerdictPass = "pass";
    public const string VerdictConditional = "conditional";
    public const string VerdictHold = "hold";
    public const string VerdictNotVerified = "not_verified";
    public const string RuleNotVerified = "NOT_VERIFIED";
    public const string RuleAccuracy = "ACCURACY";
    public const string RuleMissing = "MISSING";
    public const string RuleUnexplained = "UNEXPLAINED";
    public const string RuleTime = "TIME";
    public const string RuleFocus = "FOCUS";
    public const string RulePass = "PASS";
    // T-014 근거 키. 스펙 API 블록(judgment.md 4.13)에 없어 공개하지 않는다(text-check.md 5절).
    internal const string RuleScore = "SCORE";

    // 순수 함수. times: 재지 않았으면 null. unexplainedKeys: 사람이 아직 보지 않았으면 null. 키는 검토 항목(4.9)의 키다(4.9).
    // 대조 오류가 있으면(CompareResult.Metrics가 null) 부르는 쪽이 판정하지 않는다(5절).
    // 대조 결과가 이 정의에서 나온 것이 아니면 ArgumentException이다(5절 "정의와 대조 결과", T-013).
    public static JudgmentResult Judge(CaseDefinition definition, CompareMetrics metrics, JudgmentTimes? times = null, IReadOnlyList<string>? unexplainedKeys = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(metrics);
        if (!string.Equals(metrics.Case, definition.Name, StringComparison.Ordinal))
        {
            throw new ArgumentException("대조 결과의 케이스 이름이 case.yaml name과 다르다.", nameof(metrics));
        }

        CheckConsistent(definition, metrics, nameof(metrics));
        var scoresOnly = metrics.Status == CompareEngine.StatusNotVerified && metrics.Scores is not null;
        if (scoresOnly && unexplainedKeys is not null)
        {
            // text-check.md 5.3 끝 글머리: 점수표만에는 검토 항목이 없다.
            throw new ArgumentException("점수표만인 판정은 검토 항목 키를 받지 않는다.", nameof(unexplainedKeys));
        }

        if (times is not null && (times.BeforeMinutes < 0 || times.ProcessMinutes < 0 || times.ReviewMinutes < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(times), times, "시간은 0 이상이다.");
        }

        // 4.9 검토 항목: 항목 상태가 표의 상태인 것. 수는 표 순서(불일치, 추가 행, 중복 행)로 센다.
        // 불일치 = 값 불일치 + 누락 항목(기준 행). 몰림({bad})은 불일치만 본다(4.4 둘째 글머리).
        var review = new ReviewItemSet(
            metrics.Items.Where(i => ReviewItems.Any(r => r.Statuses.Contains(i.Status))).ToArray(),
            ReviewItems.Select(r => metrics.Items.Count(i => r.Statuses.Contains(i.Status))).ToArray(),
            metrics.Items.Where(i => BadStatuses.Contains(i.Status)).ToArray());
        CheckUnexplainedKeys(unexplainedKeys, review.Items, nameof(unexplainedKeys));

        var (thresholds, defaults) = ThresholdsOf(definition.Thresholds, definition.Scores is not null);
        if (scoresOnly)
        {
            // text-check.md 5.1: 점수표만은 이 길이 읽는 문턱만 defaults에 적는다.
            return ScoresOnly(metrics, thresholds, defaults.Where(ScoresOnlyThresholds.Contains).ToArray(), times);
        }

        return metrics.Status == CompareEngine.StatusNotVerified
            ? NotVerified(metrics, thresholds, defaults)
            : Measured(definition, metrics, thresholds, defaults, review, times, unexplainedKeys);
    }

    // 5절 "정의와 대조 결과"(T-013, 12.2): 대조 결과가 이 정의에서 나온 것인지 본다. 아니면 부르는 쪽 잘못이다(ArgumentException).
    // inputs 없음 → 미검증 no_inputs. inputs가 있고 compare가 비고 checklist가 없음 → 미검증 no_compare_fields(text-check.md 5.6).
    // 그 밖 → measured, 키 열 = inputs.key, fields[]의 필드 이름(순서) = compare 필드 이름.
    // checklist가 있으면 대조 결과의 checklist 문장 열·항목 이름(순서)이 같다. 없으면 대조 결과에 checklist가 없다.
    // 점수표 집계는 정의에 scores가 있을 때만 있다(text-check.md 5.6).
    // inputs.group이 있으면 모든 기준 행 머리 행에 그 열이 꼭 한 번 있다. 문턱은 대조가 쓰지 않으므로 보지 않는다.
    private static void CheckConsistent(CaseDefinition definition, CompareMetrics metrics, string paramName)
    {
        if ((metrics.Scores is null) != (definition.Scores is null))
        {
            throw new ArgumentException("대조 결과의 점수표 집계가 case.yaml scores와 맞지 않는다.", paramName);
        }

        var inputs = definition.Inputs;
        var reason = inputs is null ? CompareEngine.ReasonNoInputs
            : definition.Compare.Count == 0 && definition.Checklist is null ? CompareEngine.ReasonNoCompareFields
            : null;
        if (reason is not null)
        {
            if (metrics.Status != CompareEngine.StatusNotVerified || !string.Equals(metrics.Reason, reason, StringComparison.Ordinal)
                || metrics.Checklist is not null)
            {
                throw new ArgumentException($"대조 결과가 정의와 맞지 않는다: 미검증 {reason}이어야 한다.", paramName);
            }

            return;
        }

        if (metrics.Status != CompareEngine.StatusMeasured)
        {
            throw new ArgumentException("대조 결과가 정의와 맞지 않는다: measured여야 한다.", paramName);
        }

        if (!string.Equals(metrics.Key, inputs!.Key, StringComparison.Ordinal))
        {
            throw new ArgumentException("대조 결과의 키 열이 case.yaml inputs.key와 다르다.", paramName);
        }

        if (!metrics.Fields.Select(f => f.Field).SequenceEqual(definition.Compare.Select(c => c.Field), StringComparer.Ordinal))
        {
            throw new ArgumentException("대조 결과의 compare 필드가 case.yaml compare와 다르다.", paramName);
        }

        var sameChecklist = (definition.Checklist, metrics.Checklist) switch
        {
            (null, null) => true,
            ({ } d, { } m) => string.Equals(d.Text, m.Text, StringComparison.Ordinal)
                && d.Items.Select(i => i.Item).SequenceEqual(m.Entries.Select(e => e.Item), StringComparer.Ordinal),
            _ => false,
        };
        if (!sameChecklist)
        {
            throw new ArgumentException("대조 결과의 체크리스트가 case.yaml checklist와 다르다.", paramName);
        }

        if (inputs.Group is { } group
            && metrics.Items.Any(i => i.Reference is { } row && row.Header.Count(h => string.Equals(h, group, StringComparison.Ordinal)) != 1))
        {
            throw new ArgumentException("기준 행 머리 행에 case.yaml inputs.group 열이 꼭 한 번 있어야 한다.", paramName);
        }
    }

    // 4.9: 사람 입력 키는 검토 항목(불일치·추가 행·중복 행)의 키여야 하고 중복이 없어야 한다. 아니면 부르는 쪽 잘못이다.
    // 키 하나가 항목 여럿을 덮을 수 있다(결과 쪽에 여러 번 나온 키).
    private static void CheckUnexplainedKeys(IReadOnlyList<string>? keys, CompareItem[] review, string paramName)
    {
        if (keys is null)
        {
            return;
        }

        var allowed = review.Select(i => i.Key).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (key is null || !allowed.Contains(key))
            {
                throw new ArgumentException($"검토 항목의 키가 아니다: {key}", paramName);
            }

            if (!seen.Add(key))
            {
                throw new ArgumentException($"키가 두 번 나온다: {key}", paramName);
            }
        }
    }

    // 4.2: 빈 키만 기본값. 기본값을 쓴 키는 4.2 표 순서로 적는다. scores가 있으면 score_gap_max를 그 뒤에 본다(text-check.md 5.1).
    private static (JudgmentThresholds Thresholds, string[] Defaults) ThresholdsOf(CaseThresholds given, bool scores)
    {
        var defaults = new List<string>();
        T Pick<T>(T? value, string key, T fallback)
            where T : struct
        {
            if (value is { } v)
            {
                return v;
            }

            defaults.Add(key);
            return fallback;
        }

        var accuracyMin = Pick(given.AccuracyMin, KeyAccuracyMin, DefaultAccuracyMin);
        var missingMax = Pick(given.MissingMax, KeyMissingMax, DefaultMissingMax);
        var timeMustDrop = Pick(given.TimeMustDrop, KeyTimeMustDrop, DefaultTimeMustDrop);
        decimal? scoreGapMax = scores ? Pick(given.ScoreGapMax, KeyScoreGapMax, DefaultScoreGapMax) : null;
        return (new JudgmentThresholds(accuracyMin, missingMax, timeMustDrop, scoreGapMax), defaults.ToArray());
    }

    // 4.3 1행: 확인을 하지 않는다. 미검증 문장은 문턱을 읽지 않는다(4.6). 다음 할 일은 4.7.
    private static JudgmentResult NotVerified(CompareMetrics metrics, JudgmentThresholds thresholds, string[] defaults)
    {
        // 미검증 이유는 CheckConsistent가 정의와 맞춰 보았다(no_inputs 또는 no_compare_fields, 5절).
        var noInputs = string.Equals(metrics.Reason, CompareEngine.ReasonNoInputs, StringComparison.Ordinal);

        var todo = new List<JudgmentTodo>();
        if (noInputs)
        {
            todo.Add(new JudgmentTodo(TodoAddData, AddDataText));
        }

        todo.Add(new JudgmentTodo(TodoAddCompare, AddCompareText));
        if (thresholds.TimeMustDrop)
        {
            todo.Add(new JudgmentTodo(TodoMeasureTime, MeasureTimeText));
        }

        AddConfirmThresholds(todo, thresholds, defaults);
        return new JudgmentResult(
            metrics.Case, VerdictNotVerified, RuleNotVerified, noInputs ? NoInputsVerdict : NoCompareFieldsVerdict,
            thresholds, defaults, [], todo.ToArray());
    }

    private static JudgmentResult Measured(
        CaseDefinition definition, CompareMetrics metrics, JudgmentThresholds t, string[] defaults,
        ReviewItemSet review, JudgmentTimes? times, IReadOnlyList<string>? unexplainedKeys)
    {
        if (metrics.Status != CompareEngine.StatusMeasured || metrics.Counts is not { } counts
            || metrics.AccuracyPct is not { } accuracyPct || metrics.MissingRatePct is not { } missingRatePct)
        {
            throw new ArgumentException("대조 결과가 measured 모양이 아니다(셈·퍼센트가 없다).", nameof(metrics));
        }

        var reference = Number(counts.Reference);
        var badText = Number(review.Bad.Length);
        var itemsText = Number(review.Items.Length);
        var breakdown = Breakdown(review.Counts);
        string Marked(string sentence, string key) => defaults.Contains(key) ? sentence + DefaultMark : sentence;

        // 4.4 ACCURACY: match × 100 ≥ accuracy_min × reference.
        var accuracyMet = CompareScaled(counts.Match, t.AccuracyMin, counts.Reference) >= 0;
        var accuracy = new JudgmentCheck(
            RuleAccuracy, accuracyMet ? Met : NotMet,
            Marked(AccuracyCheck(accuracyMet, Number(accuracyPct), Number(counts.Match), reference, Number(t.AccuracyMin)), KeyAccuracyMin));

        // 4.4 MISSING: missing × 100 ≤ missing_max × reference.
        var missingMet = CompareScaled(counts.Missing, t.MissingMax, counts.Reference) <= 0;
        var missing = new JudgmentCheck(
            RuleMissing, missingMet ? Met : NotMet,
            Marked(MissingCheck(missingMet, Number(missingRatePct), Number(counts.Missing), reference, Number(t.MissingMax)), KeyMissingMax));

        // 4.4 UNEXPLAINED(4.9): 검토 항목이 없으면 none. 있으면 사람 입력으로 정한다. {unexplained}는 키 수다.
        var unexplainedCount = Number(unexplainedKeys?.Count ?? 0);
        var unexplained = review.Items.Length == 0 ? new JudgmentCheck(RuleUnexplained, None, UnexplainedNoneCheck)
            : unexplainedKeys is null ? new JudgmentCheck(RuleUnexplained, NotReviewed, UnexplainedNotReviewedCheck(itemsText, breakdown))
            : unexplainedKeys.Count == 0 ? new JudgmentCheck(RuleUnexplained, Explained, UnexplainedExplainedCheck(itemsText, breakdown))
            : new JudgmentCheck(RuleUnexplained, Found, UnexplainedFoundCheck(itemsText, breakdown, unexplainedCount));

        var (time, total) = TimeOf(t, times, defaults);
        var focus = FocusOf(definition, metrics, review.Bad);
        var score = metrics.Scores is { } scores ? ScoreOf(scores, t, defaults) : null;
        JudgmentCheck[] checks = score is null
            ? [accuracy, missing, unexplained, time, focus.Check]
            : [accuracy, missing, unexplained, time, focus.Check, score.Check];

        // 4.3 판정 표. 처음 걸리는 행. 판정 문장은 그 행(순서)에 오기까지 읽은 문턱(4.6)이 기본값이면 표시를 붙인다.
        // 사람이 아직 보지 않은 검토 항목은 몰림·시간 뒤의 조건부다(순서 7). 보류가 아니다.
        var (verdict, rule, reason, order) =
            !accuracyMet ? (VerdictHold, RuleAccuracy, AccuracyVerdict(Number(accuracyPct), Number(t.AccuracyMin)), OrderAccuracy)
            : !missingMet ? (VerdictHold, RuleMissing, MissingVerdict(Number(missingRatePct), Number(t.MissingMax)), OrderMissing)
            : unexplained.Result == Found ? (VerdictHold, RuleUnexplained, UnexplainedVerdict(unexplainedCount), OrderUnexplainedFound)
            : score?.Check.Result == Revealed ? (VerdictHold, RuleScore, score.HoldVerdict, OrderScoreRevealed)
            : score?.Check.Result == NotMet ? (VerdictHold, RuleScore, score.HoldVerdict, OrderScoreNotMet)
            : time.Result == NotMeasured ? (VerdictConditional, RuleTime, TimeNotMeasuredVerdict, OrderTime)
            : time.Result == NotMet ? (VerdictConditional, RuleTime, TimeNotMetVerdict(total!, Number(times!.BeforeMinutes)), OrderTime)
            : focus.Check.Result == Found ? (VerdictConditional, RuleFocus, FocusVerdict(badText, focus.Group!, focus.Value!), OrderFocus)
            : unexplained.Result == NotReviewed ? (VerdictConditional, RuleUnexplained, UnexplainedNotReviewedVerdict(itemsText, breakdown), OrderUnexplainedNotReviewed)
            : (VerdictPass, RulePass, PassVerdict, OrderPass);
        if (ReadThresholds(order, score is not null).Any(defaults.Contains))
        {
            reason += DefaultMark;
        }

        // 4.7 다음 할 일. 표 순서.
        var todo = new List<JudgmentTodo>();
        if (time.Result == NotMeasured)
        {
            todo.Add(new JudgmentTodo(TodoMeasureTime, MeasureTimeText));
        }

        if (unexplained.Result == NotReviewed)
        {
            todo.Add(new JudgmentTodo(TodoReviewItems, ReviewItemsText(itemsText, breakdown)));
        }

        if (focus.Check.Result == Off)
        {
            todo.Add(new JudgmentTodo(TodoAddGroup, AddGroupText));
        }

        if (score?.Check.Result == Revealed)
        {
            todo.Add(new JudgmentTodo(TodoRescoreBlind, RescoreBlindText));
        }

        AddConfirmThresholds(todo, t, defaults);
        return new JudgmentResult(metrics.Case, verdict, rule, reason, t, defaults, checks, todo.ToArray());
    }

    // text-check.md 5.3 점수표만: 대조가 미검증이고 점수표가 있다. 확인은 TIME, SCORE 둘이다.
    // 판정: SCORE 보류 두 행 → 대조할 열 없음(no_compare_fields, 조건부) → TIME 두 행(조건부) → PASS.
    // 다음 할 일: add_compare(no_compare_fields), measure_time, rescore_blind, confirm_thresholds(이 길이 읽는 문턱만).
    private static JudgmentResult ScoresOnly(CompareMetrics metrics, JudgmentThresholds t, string[] defaults, JudgmentTimes? times)
    {
        var (time, total) = TimeOf(t, times, defaults);
        var score = ScoreOf(metrics.Scores!, t, defaults);
        var noCompareFields = string.Equals(metrics.Reason, CompareEngine.ReasonNoCompareFields, StringComparison.Ordinal);

        var (verdict, rule, reason, order) =
            score.Check.Result == Revealed ? (VerdictHold, RuleScore, score.HoldVerdict, OrderScoreRevealed)
            : score.Check.Result == NotMet ? (VerdictHold, RuleScore, score.HoldVerdict, OrderScoreNotMet)
            : noCompareFields ? (VerdictConditional, RuleNotVerified, ScoresNoCompareFieldsVerdict, OrderNotVerified)
            : time.Result == NotMeasured ? (VerdictConditional, RuleTime, ScoresTimeNotMeasuredVerdict, OrderTime)
            : time.Result == NotMet ? (VerdictConditional, RuleTime, ScoresTimeNotMetVerdict(total!, Number(times!.BeforeMinutes)), OrderTime)
            : (VerdictPass, RulePass, ScoresPassVerdict, OrderPass);
        if (ScoresOnlyReadThresholds(order).Any(defaults.Contains))
        {
            reason += DefaultMark;
        }

        var todo = new List<JudgmentTodo>();
        if (noCompareFields)
        {
            todo.Add(new JudgmentTodo(TodoAddCompare, AddCompareText));
        }

        if (time.Result == NotMeasured)
        {
            todo.Add(new JudgmentTodo(TodoMeasureTime, MeasureTimeText));
        }

        if (score.Check.Result == Revealed)
        {
            todo.Add(new JudgmentTodo(TodoRescoreBlind, RescoreBlindText));
        }

        AddConfirmThresholds(todo, t, defaults);
        return new JudgmentResult(metrics.Case, verdict, rule, reason, t, defaults, [time, score.Check], todo.ToArray());
    }

    // 4.4 TIME(4.10): time_must_drop이 false면 시간을 받아도 보지 않는다. 합이 decimal 범위를 넘으면 not_met.
    // Total은 시간을 쟀을 때 {total} 글이다.
    private static (JudgmentCheck Check, string? Total) TimeOf(JudgmentThresholds t, JudgmentTimes? times, string[] defaults)
    {
        string Marked(string sentence) => defaults.Contains(KeyTimeMustDrop) ? sentence + DefaultMark : sentence;
        if (!t.TimeMustDrop)
        {
            return (new JudgmentCheck(RuleTime, Off, TimeOffCheck), null);
        }

        if (times is null)
        {
            return (new JudgmentCheck(RuleTime, NotMeasured, Marked(TimeNotMeasuredCheck)), null);
        }

        var (met, total) = Total(times);
        var sentence = TimeCheck(met, Number(times.ProcessMinutes), Number(times.ReviewMinutes), total, Number(times.BeforeMinutes));
        return (new JudgmentCheck(RuleTime, met ? Met : NotMet, Marked(sentence)), total);
    }

    // text-check.md 5.2 SCORE. 가림이 깨지면 revealed(문턱을 읽지 않는다). 아니면 합·수 셈으로 견준다:
    // ai.sum × human.ratings + gap × ai.ratings × human.ratings ≥ human.sum × ai.ratings 이면 met(같으면 met).
    // 정수와 decimal 가수로 정확히 셈한다. 나눗셈이 없다. 문턱이 0이면(0, 0.0) 문턱 0 문장을 쓴다.
    // HoldVerdict는 그 결과가 보류일 때(revealed, not_met)의 판정 문장이다.
    private static ScoreResult ScoreOf(ScoreSummary scores, JudgmentThresholds t, string[] defaults)
    {
        if (scores.Blind.Status == ScoreSheet.BlindRevealed)
        {
            var findings = Number(scores.Blind.Findings.Count);
            return new ScoreResult(new JudgmentCheck(RuleScore, Revealed, ScoreRevealedCheck(findings)), ScoreRevealedVerdict(findings));
        }

        var ai = scores.Sources.Single(s => s.Source == ScoreSheet.SourceAi);
        var human = scores.Sources.Single(s => s.Source == ScoreSheet.SourceHuman);
        var gap = t.ScoreGapMax ?? DefaultScoreGapMax;
        var scale = gap.Scale;
        var unit = BigInteger.Pow(10, scale);
        var left = (new BigInteger(ai.Sum) * human.Ratings * unit) + (Mantissa(gap, scale) * ai.Ratings * human.Ratings);
        var right = new BigInteger(human.Sum) * ai.Ratings * unit;
        var met = left >= right;

        var (aiMean, humanMean) = (Number(ai.Mean), Number(human.Mean));
        var (aiRatings, humanRatings) = (Number(ai.Ratings), Number(human.Ratings));
        var zero = gap == 0m;
        var sentence = zero
            ? ScoreZeroCheck(met, aiMean, aiRatings, humanMean, humanRatings)
            : ScoreCheck(met, aiMean, aiRatings, humanMean, humanRatings, Number(gap));
        if (defaults.Contains(KeyScoreGapMax))
        {
            sentence += DefaultMark;
        }

        var hold = zero ? ScoreZeroNotMetVerdict(aiMean, humanMean) : ScoreNotMetVerdict(aiMean, humanMean, Number(gap));
        return new ScoreResult(new JudgmentCheck(RuleScore, met ? Met : NotMet, sentence), hold);
    }

    // 4.7 confirm_thresholds. {defaults}: 기본값을 쓴 키와 값을 4.2 순서로 ", "로 잇는다.
    private static void AddConfirmThresholds(List<JudgmentTodo> todo, JudgmentThresholds t, string[] defaults)
    {
        if (defaults.Length == 0)
        {
            return;
        }

        var text = string.Join(", ", defaults.Select(key => key + " " + key switch
        {
            KeyAccuracyMin => Number(t.AccuracyMin),
            KeyMissingMax => Number(t.MissingMax),
            KeyScoreGapMax => Number(t.ScoreGapMax ?? DefaultScoreGapMax),
            _ => Bool(t.TimeMustDrop),
        }));
        todo.Add(new JudgmentTodo(TodoConfirmThresholds, ConfirmThresholdsText(text)));
    }

    // count × 100과 threshold × reference의 부호(4.4). decimal 곱은 29자리를 넘으면 반올림·넘침이 있어 쓰지 않는다.
    // 정수 셈으로 정확히 비교한다. threshold = 가수 / 10^자릿수라 양쪽에 10^자릿수를 곱한다.
    // count × 100 × 10^자릿수 ↔ 가수 × reference. 곱이 커도 넘치지 않아 예외가 없다(잠정 결정 17).
    private static int CompareScaled(int count, decimal threshold, int reference)
    {
        var scale = threshold.Scale;
        var left = new BigInteger(count) * 100 * BigInteger.Pow(10, scale);
        var right = Mantissa(threshold, scale) * reference;
        return left.CompareTo(right);
    }

    // 4.4 TIME: process + review < before. 합이 decimal 범위를 넘으면 줄지 않았다(not_met). 예외를 내보내지 않는다.
    private static (bool Met, string Total) Total(JudgmentTimes times)
    {
        try
        {
            var total = times.ProcessMinutes + times.ReviewMinutes;
            return (total < times.BeforeMinutes, Number(total));
        }
        catch (OverflowException)
        {
            return (false, ExactSum(times.ProcessMinutes, times.ReviewMinutes));
        }
    }

    // 4.8 몰림(M-A). 열은 case.yaml inputs.group 하나다. Group·Value는 found일 때 판정 문장에 쓰는 표시 글자다.
    private static FocusResult FocusOf(CaseDefinition definition, CompareMetrics metrics, CompareItem[] bad)
    {
        var badText = Number(bad.Length);

        // 규칙 1: 불일치가 focus_min보다 적으면 few. 열을 보지 않는다.
        if (bad.Length < FocusMin)
        {
            return new FocusResult(new JudgmentCheck(RuleFocus, Few, FocusFewCheck(badText)), null, null);
        }

        // 규칙 2: inputs.group을 적지 않았으면 off.
        if (definition.Inputs?.Group is not { } column)
        {
            return new FocusResult(new JudgmentCheck(RuleFocus, Off, FocusOffCheck(badText)), null, null);
        }

        // 규칙 3: 값은 기준 행의 그 칸이다(정규화 뒤). 결과 자료의 값, 추가 행, 중복 행은 보지 않는다.
        // 그 열은 모든 기준 파일 머리 행에 꼭 한 번 있다(대조가 확인했다, metrics.md 12.3). 열 위치는 파일마다 다를 수 있다.
        var rows = metrics.Items.Where(i => ReferenceRowStatuses.Contains(i.Status)).Select(i => i.Reference!).ToArray();
        var indexByFile = rows
            .GroupBy(r => r.File, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => IndexOf(g.First().Header, column), StringComparer.Ordinal);
        string Value(CsvRow row) => row.Cells[indexByFile[row.File]];
        var group = Show(column);

        // 규칙 4: 기준 행에서 값이 하나뿐이면 one_value.
        if (rows.Select(Value).Distinct(StringComparer.Ordinal).Take(2).Count() < 2)
        {
            return new FocusResult(new JudgmentCheck(RuleFocus, OneValue, FocusOneValueCheck(group)), null, null);
        }

        // 규칙 5: 불일치 행의 값이 모두 같으면 found. 아니면 spread. 몰린 값은 불일치 행의 값이다.
        var value = Value(bad[0].Reference!);
        if (bad.All(b => string.Equals(Value(b.Reference!), value, StringComparison.Ordinal)))
        {
            var rowsIn = rows.Count(r => string.Equals(Value(r), value, StringComparison.Ordinal));
            var shown = Show(value);
            var check = FocusFoundCheck(badText, group, shown, Number(rowsIn), Number(rows.Length - rowsIn));
            return new FocusResult(new JudgmentCheck(RuleFocus, Found, check), group, shown);
        }

        return new FocusResult(new JudgmentCheck(RuleFocus, Spread, FocusSpreadCheck(badText, group)), null, null);
    }

    private static int IndexOf(IReadOnlyList<string> header, string name)
    {
        for (var i = 0; i < header.Count; i++)
        {
            if (string.Equals(header[i], name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new InvalidOperationException($"유형 열이 머리 행에 없다: {name}");
    }

    private sealed record FocusResult(JudgmentCheck Check, string? Group, string? Value);

    private sealed record ScoreResult(JudgmentCheck Check, string HoldVerdict);

    // 4.9 검토 항목. Items: 검토 항목 전부(대조 항목 순서). Counts: ReviewItems 표 순서의 수. Bad: 불일치 항목(값 불일치·누락).
    private sealed record ReviewItemSet(CompareItem[] Items, int[] Counts, CompareItem[] Bad);
}
