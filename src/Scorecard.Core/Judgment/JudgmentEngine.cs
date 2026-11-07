using System.Numerics;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using static Scorecard.Core.Judgment.JudgmentRules;

namespace Scorecard.Core.Judgment;

// 판정. docs/spec/judgment.md 4.1~4.10과 T-012 11절(11.3~11.10), 지시문 3.4.
// 대조가 미검증이면 판정도 미검증이다. 아니면 확인 다섯(정확도, 누락률, 설명 안 되는 검토 항목, 시간, 몰림)을 모두 계산하고
// 판정 표(11.4)를 위에서부터 보고 처음 걸리는 행에서 멈춘다: 보류 → 조건부 → 통과.
// 검토 항목(11.3)은 불일치(값 불일치·누락) + 추가 행 + 결과 쪽 중복 행이다. 몰림은 불일치만 본다.
// 문턱 비교는 셈(counts)으로 한다. 표시 퍼센트로 비교하지 않는다. 나눗셈을 쓰지 않는다.
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

    // 순수 함수. times: 재지 않았으면 null. unexplainedKeys: 사람이 아직 보지 않았으면 null. 키는 검토 항목(11.3)의 키다(11.9).
    // 대조 오류가 있으면(CompareResult.Metrics가 null) 부르는 쪽이 판정하지 않는다(5절).
    public static JudgmentResult Judge(CaseDefinition definition, CompareMetrics metrics, JudgmentTimes? times = null, IReadOnlyList<string>? unexplainedKeys = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(metrics);
        if (!string.Equals(metrics.Case, definition.Name, StringComparison.Ordinal))
        {
            throw new ArgumentException("대조 결과의 케이스 이름이 case.yaml name과 다르다.", nameof(metrics));
        }

        if (times is not null && (times.BeforeMinutes < 0 || times.ProcessMinutes < 0 || times.ReviewMinutes < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(times), times, "시간은 0 이상이다.");
        }

        // 11.3 검토 항목: 항목 상태가 표의 상태인 것. 수는 표 순서(불일치, 추가 행, 중복 행)로 센다.
        // 불일치 = 값 불일치 + 누락 항목(기준 행). 몰림({bad})은 불일치만 본다(11.5 둘째 글머리).
        var review = new ReviewItemSet(
            metrics.Items.Where(i => ReviewItems.Any(r => r.Statuses.Contains(i.Status))).ToArray(),
            ReviewItems.Select(r => metrics.Items.Count(i => r.Statuses.Contains(i.Status))).ToArray(),
            metrics.Items.Where(i => BadStatuses.Contains(i.Status)).ToArray());
        CheckUnexplainedKeys(unexplainedKeys, review.Items, nameof(unexplainedKeys));

        var (thresholds, defaults) = ThresholdsOf(definition.Thresholds);
        return metrics.Status == CompareEngine.StatusNotVerified
            ? NotVerified(metrics, thresholds, defaults)
            : Measured(definition, metrics, thresholds, defaults, review, times, unexplainedKeys);
    }

    // 11.9: 사람 입력 키는 검토 항목(불일치·추가 행·중복 행)의 키여야 하고 중복이 없어야 한다. 아니면 부르는 쪽 잘못이다.
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

    // 4.2: 빈 키만 기본값. 기본값을 쓴 키는 4.2 표 순서로 적는다.
    private static (JudgmentThresholds Thresholds, string[] Defaults) ThresholdsOf(CaseThresholds given)
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
        return (new JudgmentThresholds(accuracyMin, missingMax, timeMustDrop), defaults.ToArray());
    }

    // 11.4 1행: 확인을 하지 않는다. 미검증 문장은 문턱을 읽지 않는다(11.6). 다음 할 일은 11.7.
    private static JudgmentResult NotVerified(CompareMetrics metrics, JudgmentThresholds thresholds, string[] defaults)
    {
        var noInputs = metrics.Reason switch
        {
            CompareEngine.ReasonNoInputs => true,
            CompareEngine.ReasonNoCompareFields => false,
            _ => throw new ArgumentException($"미검증 이유가 4.3 표에 없다: {metrics.Reason}", nameof(metrics)),
        };

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

        // 11.5 UNEXPLAINED(11.9): 검토 항목이 없으면 none. 있으면 사람 입력으로 정한다. {unexplained}는 키 수다.
        var unexplainedCount = Number(unexplainedKeys?.Count ?? 0);
        var unexplained = review.Items.Length == 0 ? new JudgmentCheck(RuleUnexplained, None, UnexplainedNoneCheck)
            : unexplainedKeys is null ? new JudgmentCheck(RuleUnexplained, NotReviewed, UnexplainedNotReviewedCheck(itemsText, breakdown))
            : unexplainedKeys.Count == 0 ? new JudgmentCheck(RuleUnexplained, Explained, UnexplainedExplainedCheck(itemsText, breakdown))
            : new JudgmentCheck(RuleUnexplained, Found, UnexplainedFoundCheck(itemsText, breakdown, unexplainedCount));

        // 4.4 TIME(4.10): time_must_drop이 false면 시간을 받아도 보지 않는다. 합이 decimal 범위를 넘으면 not_met.
        string? total = null;
        JudgmentCheck time;
        if (!t.TimeMustDrop)
        {
            time = new JudgmentCheck(RuleTime, Off, TimeOffCheck);
        }
        else if (times is null)
        {
            time = new JudgmentCheck(RuleTime, NotMeasured, Marked(TimeNotMeasuredCheck, KeyTimeMustDrop));
        }
        else
        {
            (var timeMet, total) = Total(times);
            var sentence = TimeCheck(timeMet, Number(times.ProcessMinutes), Number(times.ReviewMinutes), total, Number(times.BeforeMinutes));
            time = new JudgmentCheck(RuleTime, timeMet ? Met : NotMet, Marked(sentence, KeyTimeMustDrop));
        }

        var focus = FocusOf(definition, metrics, review.Bad);
        var checks = new[] { accuracy, missing, unexplained, time, focus.Check };

        // 11.4 판정 표. 처음 걸리는 행. 판정 문장은 그 행(순서)에 오기까지 읽은 문턱(11.6)이 기본값이면 표시를 붙인다.
        // 사람이 아직 보지 않은 검토 항목은 몰림·시간 뒤의 조건부다(순서 7). 보류가 아니다.
        var (verdict, rule, reason, order) =
            !accuracyMet ? (VerdictHold, RuleAccuracy, AccuracyVerdict(Number(accuracyPct), Number(t.AccuracyMin)), OrderAccuracy)
            : !missingMet ? (VerdictHold, RuleMissing, MissingVerdict(Number(missingRatePct), Number(t.MissingMax)), OrderMissing)
            : unexplained.Result == Found ? (VerdictHold, RuleUnexplained, UnexplainedVerdict(unexplainedCount), OrderUnexplainedFound)
            : time.Result == NotMeasured ? (VerdictConditional, RuleTime, TimeNotMeasuredVerdict, OrderTime)
            : time.Result == NotMet ? (VerdictConditional, RuleTime, TimeNotMetVerdict(total!, Number(times!.BeforeMinutes)), OrderTime)
            : focus.Check.Result == Found ? (VerdictConditional, RuleFocus, FocusVerdict(badText, focus.Group!, focus.Value!), OrderFocus)
            : unexplained.Result == NotReviewed ? (VerdictConditional, RuleUnexplained, UnexplainedNotReviewedVerdict(itemsText, breakdown), OrderUnexplainedNotReviewed)
            : (VerdictPass, RulePass, PassVerdict, OrderPass);
        if (ReadThresholds(order).Any(defaults.Contains))
        {
            reason += DefaultMark;
        }

        // 11.7 다음 할 일. 표 순서.
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

        AddConfirmThresholds(todo, t, defaults);
        return new JudgmentResult(metrics.Case, verdict, rule, reason, t, defaults, checks, todo.ToArray());
    }

    // 11.7 confirm_thresholds. {defaults}: 기본값을 쓴 키와 값을 4.2 순서로 ", "로 잇는다.
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

    // 11.8 몰림(M-A). 열은 case.yaml inputs.group 하나다. Group·Value는 found일 때 판정 문장에 쓰는 표시 글자다.
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

    // 11.3 검토 항목. Items: 검토 항목 전부(대조 항목 순서). Counts: ReviewItems 표 순서의 수. Bad: 불일치 항목(값 불일치·누락).
    private sealed record ReviewItemSet(CompareItem[] Items, int[] Counts, CompareItem[] Bad);
}
