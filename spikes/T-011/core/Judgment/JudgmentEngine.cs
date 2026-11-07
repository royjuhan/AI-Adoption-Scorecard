using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using R = Scorecard.Core.Judgment.JudgmentRules;

namespace Scorecard.Core.Judgment;

// T-010 스파이크 후보(머지 대상 아님). 대조 결과와 thresholds로 판정을 낸다. docs/spec/judgment.md 4절.
// 순수 함수다. 파일·시각·환경 변수·난수를 쓰지 않는다. 비교는 서수, 숫자는 decimal·InvariantCulture.
// 문턱 비교는 셈(counts)으로 한다. 표시용 퍼센트(accuracy_pct 등)로 비교하지 않는다(metrics.md 4.8 권고).
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

    // times: 시간을 재지 않았으면 null. unexplainedKeys: 사람이 "설명 안 됨"으로 적은 불일치 키. 사람이 아직 보지 않았으면 null.
    public static JudgmentResult Judge(CaseDefinition definition, CompareMetrics metrics, JudgmentTimes? times = null, IReadOnlyList<string>? unexplainedKeys = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(metrics);
        if (!string.Equals(definition.Name, metrics.Case, StringComparison.Ordinal))
        {
            throw new ArgumentException("대조 결과의 케이스 이름이 case.yaml name과 다르다.", nameof(metrics));
        }

        if (times is not null && (times.BeforeMinutes < 0 || times.ProcessMinutes < 0 || times.ReviewMinutes < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(times), "시간은 0 이상이어야 한다.");
        }

        var (thresholds, defaults) = Resolve(definition.Thresholds);
        if (metrics.Status == CompareEngine.StatusNotVerified)
        {
            return NotVerified(definition.Name, metrics.Reason, thresholds, defaults);
        }

        var counts = metrics.Counts ?? throw new ArgumentException("measured인데 counts가 없다.", nameof(metrics));
        var bad = metrics.Items.Where(i => i.Status is CompareEngine.ItemMismatch or CompareEngine.ItemMissing).ToArray();
        var unexplained = CheckUnexplainedKeys(bad, unexplainedKeys);
        string Mark(string text, params string[] keys) => keys.Any(defaults.Contains) ? text + R.DefaultMark : text;

        // 4.4 확인 다섯(표 순서).
        var accuracyMet = counts.Match * 100m >= thresholds.AccuracyMin * counts.Reference;
        var accuracy = new JudgmentCheck(
            RuleAccuracy,
            accuracyMet ? R.Met : R.NotMet,
            Mark(
                accuracyMet
                    ? R.AccuracyMet(metrics.AccuracyPct!.Value, counts.Match, counts.Reference, thresholds.AccuracyMin)
                    : R.AccuracyNotMet(metrics.AccuracyPct!.Value, counts.Match, counts.Reference, thresholds.AccuracyMin),
                R.KeyAccuracyMin));

        var missingMet = counts.Missing * 100m <= thresholds.MissingMax * counts.Reference;
        var missing = new JudgmentCheck(
            RuleMissing,
            missingMet ? R.Met : R.NotMet,
            Mark(
                missingMet
                    ? R.MissingMet(metrics.MissingRatePct!.Value, counts.Missing, counts.Reference, thresholds.MissingMax)
                    : R.MissingNotMet(metrics.MissingRatePct!.Value, counts.Missing, counts.Reference, thresholds.MissingMax),
                R.KeyMissingMax));

        var unexplainedCheck = bad.Length == 0 ? new JudgmentCheck(RuleUnexplained, R.None, R.UnexplainedNone)
            : unexplained is null ? new JudgmentCheck(RuleUnexplained, R.NotReviewed, R.UnexplainedNotReviewed(bad.Length))
            : unexplained.Count == 0 ? new JudgmentCheck(RuleUnexplained, R.Explained, R.UnexplainedExplained(bad.Length))
            : new JudgmentCheck(RuleUnexplained, R.Found, R.UnexplainedFound(bad.Length, unexplained.Count));

        var (timeResult, timeReason, total) = Time(thresholds.TimeMustDrop, times);
        var time = new JudgmentCheck(RuleTime, timeResult, Mark(timeReason, R.KeyTimeMustDrop));

        var focus = Focus(definition.Inputs?.Group, metrics, bad, out var focusValue);

        // 4.3 판정 표(순서대로, 처음 걸리는 곳에서 멈춘다).
        string verdict, rule, reason;
        if (accuracy.Result == R.NotMet)
        {
            (verdict, rule, reason) = (VerdictHold, RuleAccuracy, Mark(R.HoldAccuracy(metrics.AccuracyPct!.Value, thresholds.AccuracyMin), R.KeyAccuracyMin));
        }
        else if (missing.Result == R.NotMet)
        {
            (verdict, rule, reason) = (VerdictHold, RuleMissing, Mark(R.HoldMissing(metrics.MissingRatePct!.Value, thresholds.MissingMax), R.KeyAccuracyMin, R.KeyMissingMax));
        }
        else if (unexplainedCheck.Result == R.Found)
        {
            (verdict, rule, reason) = (VerdictHold, RuleUnexplained, Mark(R.HoldUnexplained(unexplained!.Count), R.KeyAccuracyMin, R.KeyMissingMax));
        }
        else if (time.Result is R.NotMeasured or R.NotMet)
        {
            var text = time.Result == R.NotMeasured ? R.ConditionalTimeNotMeasured : R.ConditionalTimeNotMet(total!.Value, times!.BeforeMinutes);
            (verdict, rule, reason) = (VerdictConditional, RuleTime, Mark(text, R.KeyAccuracyMin, R.KeyMissingMax, R.KeyTimeMustDrop));
        }
        else if (focus.Result == R.Found)
        {
            (verdict, rule, reason) = (VerdictConditional, RuleFocus, Mark(R.ConditionalFocus(bad.Length, Show(definition.Inputs!.Group!), focusValue!), R.KeyAccuracyMin, R.KeyMissingMax, R.KeyTimeMustDrop));
        }
        else
        {
            (verdict, rule, reason) = (VerdictPass, RulePass, Mark(R.Pass, R.KeyAccuracyMin, R.KeyMissingMax, R.KeyTimeMustDrop));
        }

        // 4.7 다음 할 일(표 순서).
        var todo = new List<JudgmentTodo>();
        if (time.Result == R.NotMeasured)
        {
            todo.Add(new JudgmentTodo(R.TodoMeasureTime, R.MeasureTimeText));
        }

        if (unexplainedCheck.Result == R.NotReviewed)
        {
            todo.Add(new JudgmentTodo(R.TodoReviewMismatches, R.ReviewMismatchesText(bad.Length)));
        }

        if (focus.Result == R.Off)
        {
            todo.Add(new JudgmentTodo(R.TodoSetGroup, R.SetGroupText));
        }

        AddConfirmThresholds(todo, thresholds, defaults);
        return new JudgmentResult(definition.Name, verdict, rule, reason, thresholds, defaults, [accuracy, missing, unexplainedCheck, time, focus], todo);
    }

    // 4.2: 없는 문턱은 기본값. 기본값을 쓴 키는 thresholds 표 순서로.
    private static (JudgmentThresholds Thresholds, string[] Defaults) Resolve(CaseThresholds t)
    {
        var defaults = new List<string>();
        if (t.AccuracyMin is null)
        {
            defaults.Add(R.KeyAccuracyMin);
        }

        if (t.MissingMax is null)
        {
            defaults.Add(R.KeyMissingMax);
        }

        if (t.TimeMustDrop is null)
        {
            defaults.Add(R.KeyTimeMustDrop);
        }

        return (new JudgmentThresholds(t.AccuracyMin ?? R.DefaultAccuracyMin, t.MissingMax ?? R.DefaultMissingMax, t.TimeMustDrop ?? R.DefaultTimeMustDrop), defaults.ToArray());
    }

    private static JudgmentResult NotVerified(string name, string? reason, JudgmentThresholds thresholds, string[] defaults)
    {
        var todo = new List<JudgmentTodo>();
        if (reason == CompareEngine.ReasonNoInputs)
        {
            todo.Add(new JudgmentTodo(R.TodoAddData, R.AddDataText));
        }

        todo.Add(new JudgmentTodo(R.TodoAddCompare, R.AddCompareText));
        if (thresholds.TimeMustDrop)
        {
            todo.Add(new JudgmentTodo(R.TodoMeasureTime, R.MeasureTimeText));
        }

        AddConfirmThresholds(todo, thresholds, defaults);
        var text = reason == CompareEngine.ReasonNoInputs ? R.NotVerifiedNoInputs : R.NotVerifiedNoCompare;
        return new JudgmentResult(name, VerdictNotVerified, RuleNotVerified, text, thresholds, defaults, [], todo);
    }

    private static void AddConfirmThresholds(List<JudgmentTodo> todo, JudgmentThresholds thresholds, string[] defaults)
    {
        if (defaults.Length == 0)
        {
            return;
        }

        var shown = defaults.Select(k => k switch
        {
            R.KeyAccuracyMin => $"{k} {R.Number(thresholds.AccuracyMin)}",
            R.KeyMissingMax => $"{k} {R.Number(thresholds.MissingMax)}",
            _ => $"{k} {R.Bool(thresholds.TimeMustDrop)}",
        });
        todo.Add(new JudgmentTodo(R.TodoConfirmThresholds, R.ConfirmThresholdsText(string.Join(", ", shown))));
    }

    // 사람이 적은 키는 불일치(값 불일치·누락) 항목의 키여야 하고 중복이 없어야 한다. 아니면 부르는 쪽 잘못이다.
    private static IReadOnlyList<string>? CheckUnexplainedKeys(CompareItem[] bad, IReadOnlyList<string>? keys)
    {
        if (keys is null)
        {
            return null;
        }

        var badKeys = bad.Select(i => i.Key).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (!badKeys.Contains(key) || !seen.Add(key))
            {
                throw new ArgumentException("설명 안 됨 키는 불일치 항목의 키여야 하고 중복이 없어야 한다.", nameof(keys));
            }
        }

        return keys;
    }

    // 4.4 TIME. 처리 + 검토 < 적용 전이면 met. 같으면 줄지 않았다.
    private static (string Result, string Reason, decimal? Total) Time(bool mustDrop, JudgmentTimes? times)
    {
        if (!mustDrop)
        {
            return (R.Off, R.TimeOff, null);
        }

        if (times is null)
        {
            return (R.NotMeasured, R.TimeNotMeasured, null);
        }

        decimal total;
        try
        {
            total = times.ProcessMinutes + times.ReviewMinutes;
        }
        catch (OverflowException)
        {
            total = decimal.MaxValue;
        }

        return total < times.BeforeMinutes
            ? (R.Met, R.TimeMet(times.ProcessMinutes, times.ReviewMinutes, total, times.BeforeMinutes), total)
            : (R.NotMet, R.TimeNotMet(times.ProcessMinutes, times.ReviewMinutes, total, times.BeforeMinutes), total);
    }

    // 4.4 FOCUS. 불일치(값 불일치 + 누락)의 기준 행 유형 값을 본다. 추가 행은 보지 않는다.
    private static JudgmentCheck Focus(string? group, CompareMetrics metrics, CompareItem[] bad, out string? value)
    {
        value = null;
        if (bad.Length < R.FocusMinimum)
        {
            return new JudgmentCheck(RuleFocus, R.Few, R.FocusFew(bad.Length));
        }

        if (group is null)
        {
            return new JudgmentCheck(RuleFocus, R.Off, R.FocusOff(bad.Length));
        }

        var all = metrics.Items.Where(i => i.Reference is not null).Select(i => GroupValue(i.Reference!, group)).ToArray();
        var shownGroup = Show(group);
        if (all.Distinct(StringComparer.Ordinal).Count() == 1)
        {
            return new JudgmentCheck(RuleFocus, R.OneValue, R.FocusOneValue(shownGroup));
        }

        var badValues = bad.Select(i => GroupValue(i.Reference!, group)).ToArray();
        var distinct = badValues.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (distinct.Length == 1)
        {
            var rowsIn = all.Count(v => string.Equals(v, distinct[0], StringComparison.Ordinal));
            value = Show(distinct[0]);
            return new JudgmentCheck(RuleFocus, R.Found, R.FocusFound(bad.Length, shownGroup, value, rowsIn, all.Length - rowsIn));
        }

        var counts = string.Join(", ", distinct.Select(v => R.FocusCount(Show(v), badValues.Count(b => string.Equals(b, v, StringComparison.Ordinal)))));
        return new JudgmentCheck(RuleFocus, R.Spread, R.FocusSpread(bad.Length, shownGroup, counts));
    }

    private static string GroupValue(CsvRow row, string group)
    {
        var index = -1;
        for (var i = 0; i < row.Header.Count; i++)
        {
            if (string.Equals(row.Header[i], group, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        return index < 0 ? throw new ArgumentException($"기준 행에 유형 열이 없다: {group}") : row.Cells[index];
    }

    // 문장 안의 값. 빈 칸은 "(빈 칸)". 제어 문자는 \uXXXX, 64자 넘으면 자른다(metrics.md 4.7과 같다).
    private static string Show(string value) => value.Length == 0 ? R.EmptyValue : CompareRules.Show(value);
}
