using System.Numerics;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using static Scorecard.Core.Judgment.JudgmentRules;

namespace Scorecard.Core.Judgment;

// 판정. docs/spec/judgment.md 4.1~4.10, 지시문 3.4.
// 대조가 미검증이면 판정도 미검증이다. 아니면 확인 다섯(정확도, 누락률, 설명 안 되는 불일치, 시간, 몰림)을 모두 계산하고
// 판정 표(4.3)를 위에서부터 보고 처음 걸리는 행에서 멈춘다: 보류 → 조건부 → 통과.
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

    // 순수 함수. times: 재지 않았으면 null. unexplainedKeys: 사람이 아직 보지 않았으면 null.
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

        // 불일치 = 값 불일치 + 누락 항목(기준 행). 추가 행은 넣지 않는다(0절).
        var bad = metrics.Items.Where(i => i.Status is CompareEngine.ItemMismatch or CompareEngine.ItemMissing).ToArray();
        CheckUnexplainedKeys(unexplainedKeys, bad, nameof(unexplainedKeys));

        var (thresholds, defaults) = ThresholdsOf(definition.Thresholds);
        return metrics.Status == CompareEngine.StatusNotVerified
            ? NotVerified(metrics, thresholds, defaults)
            : Measured(definition, metrics, thresholds, defaults, bad, times, unexplainedKeys);
    }

    // 4.9: 사람 입력 키는 불일치 항목의 키여야 하고 중복이 없어야 한다. 아니면 부르는 쪽 잘못이다.
    private static void CheckUnexplainedKeys(IReadOnlyList<string>? keys, CompareItem[] bad, string paramName)
    {
        if (keys is null)
        {
            return;
        }

        var allowed = bad.Select(i => i.Key).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (key is null || !allowed.Contains(key))
            {
                throw new ArgumentException($"불일치 항목의 키가 아니다: {key}", paramName);
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

    // 4.3 1행: 확인을 하지 않는다. 미검증 문장은 문턱을 읽지 않는다(4.6). 다음 할 일은 4.7.
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
        CompareItem[] bad, JudgmentTimes? times, IReadOnlyList<string>? unexplainedKeys)
    {
        if (metrics.Status != CompareEngine.StatusMeasured || metrics.Counts is not { } counts
            || metrics.AccuracyPct is not { } accuracyPct || metrics.MissingRatePct is not { } missingRatePct)
        {
            throw new ArgumentException("대조 결과가 measured 모양이 아니다(셈·퍼센트가 없다).", nameof(metrics));
        }

        var reference = Number(counts.Reference);
        var badText = Number(bad.Length);
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

        // 4.4 UNEXPLAINED(4.9): 불일치가 없으면 none. 있으면 사람 입력으로 정한다.
        var unexplainedCount = Number(unexplainedKeys?.Count ?? 0);
        var unexplained = bad.Length == 0 ? new JudgmentCheck(RuleUnexplained, None, UnexplainedNoneCheck)
            : unexplainedKeys is null ? new JudgmentCheck(RuleUnexplained, NotReviewed, UnexplainedNotReviewedCheck(badText))
            : unexplainedKeys.Count == 0 ? new JudgmentCheck(RuleUnexplained, Explained, UnexplainedExplainedCheck(badText))
            : new JudgmentCheck(RuleUnexplained, Found, UnexplainedFoundCheck(badText, unexplainedCount));

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

        var focus = FocusOf(definition, metrics, bad);
        var checks = new[] { accuracy, missing, unexplained, time, focus.Check };

        // 4.3 판정 표. 처음 걸리는 행. 판정 문장은 그 행에 오기까지 읽은 문턱(4.6)이 기본값이면 표시를 붙인다.
        var (verdict, rule, reason) =
            !accuracyMet ? (VerdictHold, RuleAccuracy, AccuracyVerdict(Number(accuracyPct), Number(t.AccuracyMin)))
            : !missingMet ? (VerdictHold, RuleMissing, MissingVerdict(Number(missingRatePct), Number(t.MissingMax)))
            : unexplained.Result == Found ? (VerdictHold, RuleUnexplained, UnexplainedVerdict(unexplainedCount))
            : time.Result == NotMeasured ? (VerdictConditional, RuleTime, TimeNotMeasuredVerdict)
            : time.Result == NotMet ? (VerdictConditional, RuleTime, TimeNotMetVerdict(total!, Number(times!.BeforeMinutes)))
            : focus.Check.Result == Found ? (VerdictConditional, RuleFocus, FocusVerdict(badText, focus.Group!, focus.Value!))
            : (VerdictPass, RulePass, PassVerdict);
        if (ReadThresholds(rule).Any(defaults.Contains))
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
            todo.Add(new JudgmentTodo(TodoReviewMismatches, ReviewMismatchesText(badText)));
        }

        if (focus.Check.Result == Off)
        {
            todo.Add(new JudgmentTodo(TodoAddTypeColumn, AddTypeColumnText));
        }

        AddConfirmThresholds(todo, t, defaults);
        return new JudgmentResult(metrics.Case, verdict, rule, reason, t, defaults, checks, todo.ToArray());
    }

    // 4.6 읽은 문턱 표. "정확도는 충족하나"로 시작하는 문장은 앞 문턱도 읽은 것으로 본다.
    private static string[] ReadThresholds(string rule) => rule switch
    {
        RuleAccuracy => [KeyAccuracyMin],
        RuleMissing or RuleUnexplained => [KeyAccuracyMin, KeyMissingMax],
        _ => [KeyAccuracyMin, KeyMissingMax, KeyTimeMustDrop],
    };

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

    // 4.8 몰림(M-B + F1). Group·Value는 found일 때 판정 문장에 쓰는 표시 글자다.
    private static FocusResult FocusOf(CaseDefinition definition, CompareMetrics metrics, CompareItem[] bad)
    {
        var badText = Number(bad.Length);

        // 규칙 4: 불일치가 focus_min보다 적으면 few. 유형 열이 없어도 few가 먼저다.
        if (bad.Length < FocusMin)
        {
            return new FocusResult(new JudgmentCheck(RuleFocus, Few, FocusFewCheck(badText)), null, null);
        }

        // 규칙 3: 값은 기준 행의 칸이다. 추가 행(기준 행 없음)은 보지 않는다.
        // 머리 행은 기준 파일마다 하나다. 기준 행(CsvRow)에서 읽는다. 정규화 뒤 글자다(metrics.md 4.6).
        var rows = metrics.Items.Where(i => i.Reference is not null).Select(i => i.Reference!).ToArray();
        var headers = rows.GroupBy(r => r.File, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Header, StringComparer.Ordinal);
        var columns = TypeColumns(definition, metrics, headers.Values);
        if (columns.Count == 0)
        {
            return new FocusResult(new JudgmentCheck(RuleFocus, Off, FocusOffCheck(badText)), null, null);
        }

        // 규칙 5·6: 열 이름 서수 순. 값이 하나뿐인 열은 건너뛴다. 불일치 행의 값이 모두 같으면 found(처음 열에서 멈춘다).
        var seen = new List<string>();
        foreach (var column in columns)
        {
            var indexByFile = headers.ToDictionary(h => h.Key, h => IndexOf(h.Value, column), StringComparer.Ordinal);
            string Value(CsvRow row) => row.Cells[indexByFile[row.File]];

            if (rows.Select(Value).Distinct(StringComparer.Ordinal).Take(2).Count() < 2)
            {
                continue;
            }

            seen.Add(column);
            var value = Value(bad[0].Reference!);
            if (bad.All(b => string.Equals(Value(b.Reference!), value, StringComparison.Ordinal)))
            {
                var rowsIn = rows.Count(r => string.Equals(Value(r), value, StringComparison.Ordinal));
                var (group, shown) = (Show(column), Show(value));
                var check = FocusFoundCheck(badText, group, shown, Number(rowsIn), Number(rows.Length - rowsIn));
                return new FocusResult(new JudgmentCheck(RuleFocus, Found, check), group, shown);
            }
        }

        return seen.Count > 0
            ? new FocusResult(new JudgmentCheck(RuleFocus, Spread, FocusSpreadCheck(badText, Names(seen))), null, null)
            : new FocusResult(new JudgmentCheck(RuleFocus, OneValue, FocusOneValueCheck(Names(columns))), null, null);
    }

    // 4.8 규칙 1·2: 모든 기준 파일의 머리 행에 꼭 한 번 있는 이름. 키 열, compare 필드, 빈 이름은 뺀다. 이름 서수 순.
    private static List<string> TypeColumns(CaseDefinition definition, CompareMetrics metrics, IEnumerable<IReadOnlyList<string>> headers)
    {
        var excluded = definition.Compare.Select(c => c.Field).ToHashSet(StringComparer.Ordinal);
        if ((definition.Inputs?.Key ?? metrics.Key) is { } key)
        {
            excluded.Add(key);
        }

        HashSet<string>? common = null;
        foreach (var header in headers)
        {
            var once = header.GroupBy(n => n, StringComparer.Ordinal).Where(g => g.Count() == 1).Select(g => g.Key);
            if (common is null)
            {
                common = once.ToHashSet(StringComparer.Ordinal);
            }
            else
            {
                common.IntersectWith(once);
            }
        }

        return (common ?? [])
            .Where(n => n.Length > 0 && !excluded.Contains(n))
            .Order(StringComparer.Ordinal)
            .ToList();
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

    // 4.5 {columns}: 열 이름을 ", "로 잇는다. 이름은 이미 서수 순이다.
    private static string Names(IEnumerable<string> columns) => string.Join(", ", columns.Select(Show));

    private sealed record FocusResult(JudgmentCheck Check, string? Group, string? Value);
}
