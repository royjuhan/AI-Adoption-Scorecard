using System.Globalization;
using System.Numerics;
using Scorecard.Core.Comparison;

namespace Scorecard.Core.Judgment;

// 판정의 값·숫자·문장을 이 파일 한 곳에만 둔다(docs/spec/judgment.md 4.13, classification.md 4.9와 같은 방식).
// 원본은 스펙 4.2 기본값 표, 4.5 숫자 표, 4.6 기본값 표시와 T-012 11절 표다:
// 11.3 검토 항목, 11.4 판정 표, 11.5 확인 표, 11.6 읽은 문턱, 11.7 다음 할 일.
// 값을 바꾸려면 스펙 표를 먼저 고친다. 테스트가 스펙 표를 읽어 Core 동작과 대조한다.
// 실행 중에 스펙 문서를 읽지 않는다. 비교는 서수, 숫자는 InvariantCulture다.
internal static class JudgmentRules
{
    // 4.2 문턱 키. 이 순서가 defaults 순서다.
    public const string KeyAccuracyMin = "accuracy_min";
    public const string KeyMissingMax = "missing_max";
    public const string KeyTimeMustDrop = "time_must_drop";

    // 4.2 기본값. 지시문 12.1 값이다.
    public const decimal DefaultAccuracyMin = 95m;
    public const decimal DefaultMissingMax = 2m;
    public const bool DefaultTimeMustDrop = true;

    // 4.5 몰림을 보는 최소 불일치 수.
    public const int FocusMin = 2;

    // 4.4 확인 결과.
    public const string Met = "met";
    public const string NotMet = "not_met";
    public const string None = "none";
    public const string NotReviewed = "not_reviewed";
    public const string Explained = "explained";
    public const string Found = "found";
    public const string Off = "off";
    public const string NotMeasured = "not_measured";
    public const string Few = "few";
    public const string OneValue = "one_value";
    public const string Spread = "spread";

    // 11.7 다음 할 일 키.
    public const string TodoAddData = "add_data";
    public const string TodoAddCompare = "add_compare";
    public const string TodoMeasureTime = "measure_time";
    public const string TodoReviewItems = "review_items";
    public const string TodoAddGroup = "add_group";
    public const string TodoConfirmThresholds = "confirm_thresholds";

    // 11.3 검토 항목: {breakdown} 이름과 그 항목 상태. 이 순서가 {breakdown} 순서다. match는 검토 항목이 아니다.
    public static readonly (string Name, string[] Statuses)[] ReviewItems =
    [
        ("불일치", [CompareEngine.ItemMismatch, CompareEngine.ItemMissing]),
        ("추가 행", [CompareEngine.ItemExtra]),
        ("중복 행", [CompareEngine.ItemDuplicate]),
    ];

    // 몰림({bad})·몰림 값에 쓰는 불일치 항목 상태(11.5 둘째 글머리). 추가 행·중복 행은 넣지 않는다.
    public static readonly string[] BadStatuses = [CompareEngine.ItemMismatch, CompareEngine.ItemMissing];

    // 몰림 값을 읽는 기준 행의 항목 상태(11.8 규칙 3). 중복 항목도 기준 행을 가리키지만 같은 행이라 넣지 않는다.
    public static readonly string[] ReferenceRowStatuses = [CompareEngine.ItemMatch, CompareEngine.ItemMismatch, CompareEngine.ItemMissing];

    // 11.4 판정 표의 순서. 11.6 읽은 문턱 표가 이 순서로 적는다(UNEXPLAINED는 4·7 두 곳).
    public const int OrderNotVerified = 1;
    public const int OrderAccuracy = 2;
    public const int OrderMissing = 3;
    public const int OrderUnexplainedFound = 4;
    public const int OrderTime = 5;
    public const int OrderFocus = 6;
    public const int OrderUnexplainedNotReviewed = 7;
    public const int OrderPass = 8;

    // 4.6 기본값 표시. 공백 하나 + 문구. classification.md 4.6과 같은 글자다.
    public const string DefaultMark = " (기본값 사용, 확인 필요)";

    // 4.5 빈 값 표시.
    public const string EmptyValue = "(빈 칸)";

    // 11.4 판정 문장 틀.
    public const string NoInputsVerdict = "검증 자료(case.yaml inputs)가 없어 미검증.";
    public const string NoCompareFieldsVerdict = "대조할 열(case.yaml compare)이 없어 미검증.";
    public const string TimeNotMeasuredVerdict = "정확도·누락률은 문턱을 충족하나 시간을 재지 않아 줄었는지 알 수 없으므로 조건부.";
    public const string PassVerdict = "정확도·누락률이 문턱을 충족하고 조건부·보류 조건에 걸리지 않으므로 통과.";

    public static string AccuracyVerdict(string accuracyPct, string accuracyMin) =>
        $"정확도 {accuracyPct}%가 문턱 {accuracyMin}%에 못 미치므로 보류.";

    public static string MissingVerdict(string missingRatePct, string missingMax) =>
        $"누락률 {missingRatePct}%가 문턱 {missingMax}%를 넘으므로 보류.";

    public static string UnexplainedVerdict(string unexplained) =>
        $"사람이 설명할 수 없다고 적은 검토 항목 키가 {unexplained}개 있으므로 보류.";

    public static string TimeNotMetVerdict(string total, string before) =>
        $"정확도·누락률은 문턱을 충족하나 처리·검토 시간 {total}분이 적용 전 {before}분보다 짧지 않으므로 조건부.";

    public static string FocusVerdict(string bad, string group, string value) =>
        $"정확도·누락률은 문턱을 충족하나 불일치 {bad}건이 모두 {group} = {value}인 행이므로 조건부.";

    public static string UnexplainedNotReviewedVerdict(string items, string breakdown) =>
        $"정확도·누락률은 문턱을 충족하나 검토할 항목 {items}건({breakdown})을 사람이 아직 보지 않았으므로 조건부.";

    // 11.5 확인 문장 틀.
    public static string AccuracyCheck(bool met, string accuracyPct, string match, string reference, string accuracyMin) => met
        ? $"정확도 {accuracyPct}%({match}/{reference})가 문턱 {accuracyMin}% 이상이다."
        : $"정확도 {accuracyPct}%({match}/{reference})가 문턱 {accuracyMin}%에 못 미친다.";

    public static string MissingCheck(bool met, string missingRatePct, string missing, string reference, string missingMax) => met
        ? $"누락률 {missingRatePct}%({missing}/{reference})가 문턱 {missingMax}% 이하다."
        : $"누락률 {missingRatePct}%({missing}/{reference})가 문턱 {missingMax}%를 넘는다.";

    public const string UnexplainedNoneCheck = "검토할 항목(불일치·추가 행·중복 행)이 없다.";

    public static string UnexplainedNotReviewedCheck(string items, string breakdown) =>
        $"검토할 항목 {items}건({breakdown})이 설명되는지 사람이 아직 보지 않았다.";

    public static string UnexplainedExplainedCheck(string items, string breakdown) =>
        $"검토할 항목 {items}건({breakdown})을 사람이 보고 모두 설명했다.";

    public static string UnexplainedFoundCheck(string items, string breakdown, string unexplained) =>
        $"검토할 항목 {items}건({breakdown}) 중 키 {unexplained}개를 사람이 설명할 수 없다고 적었다.";

    public const string TimeOffCheck = "시간 조건을 쓰지 않는다(time_must_drop: false).";
    public const string TimeNotMeasuredCheck = "시간을 재지 않았다.";

    public static string TimeCheck(bool met, string process, string review, string total, string before) => met
        ? $"처리 {process}분 + 검토 {review}분 = {total}분이 적용 전 {before}분보다 짧다."
        : $"처리 {process}분 + 검토 {review}분 = {total}분이 적용 전 {before}분보다 짧지 않다.";

    public static string FocusFewCheck(string bad) => $"불일치가 {bad}건이라 몰림을 보지 않는다.";

    public static string FocusOffCheck(string bad) =>
        $"case.yaml inputs.group에 유형 열을 적지 않아 불일치 {bad}건이 한 유형에 몰렸는지 보지 않는다.";

    public static string FocusOneValueCheck(string group) => $"{group} 열이 기준 자료에서 값이 하나뿐이라 몰림을 볼 수 없다.";

    public static string FocusFoundCheck(string bad, string group, string value, string rowsIn, string rowsOut) =>
        $"불일치 {bad}건이 모두 {group} = {value}인 행이다({value} {rowsIn}행 중 {bad}행, 다른 값 {rowsOut}행 중 0행).";

    public static string FocusSpreadCheck(string bad, string group) => $"불일치 {bad}건이 {group} 열의 한 값에 몰리지 않았다.";

    // 11.7 다음 할 일 문구.
    public const string AddDataText =
        "기준 자료(사람이 만든 결과)를 reference/에, 결과 자료(AI 적용 후 결과)를 candidate/에 넣고 case.yaml inputs에 파일 패턴과 키 열을 적는다.";

    public const string AddCompareText = "case.yaml compare에 대조할 열과 허용 오차를 적는다.";
    public const string MeasureTimeText = "건당 처리 시간·검토 시간과 적용 전 시간(분)을 잰다.";
    public const string AddGroupText = "불일치가 한 유형에 몰렸는지 보려면 case.yaml inputs.group에 기준 자료의 유형 열 이름을 적는다.";

    public static string ReviewItemsText(string items, string breakdown) => $"검토할 항목 {items}건({breakdown})을 보고 설명이 되는지 적는다.";

    public static string ConfirmThresholdsText(string defaults) =>
        $"문턱에 기본값을 썼다({defaults}). 맞는지 확인하고 case.yaml thresholds에 적는다.";

    // 11.6 읽은 문턱: 판정 표의 순서마다 그 행에 오기까지 읽은 문턱(4.2 순서).
    // "정확도·누락률은 충족하나"로 시작하는 문장은 앞 문턱도 읽은 것으로 본다. 순서 7은 시간(순서 5)을 지나 왔다.
    public static string[] ReadThresholds(int order) => order switch
    {
        OrderNotVerified => [],
        OrderAccuracy => [KeyAccuracyMin],
        OrderMissing or OrderUnexplainedFound => [KeyAccuracyMin, KeyMissingMax],
        _ => [KeyAccuracyMin, KeyMissingMax, KeyTimeMustDrop],
    };

    // 11.3 {breakdown}: 이름마다 수를 세어 0이 아닌 것만 "이름 N건"으로 쓰고 ", "로 잇는다. counts는 ReviewItems 순서다.
    public static string Breakdown(IReadOnlyList<int> counts) =>
        string.Join(", ", ReviewItems.Select((r, i) => (r.Name, Count: counts[i])).Where(p => p.Count > 0).Select(p => $"{p.Name} {Number(p.Count)}건"));

    // 4.5 숫자 글자. InvariantCulture. decimal은 자릿수를 그대로 쓴다(85는 85, 90.0은 90.0).
    public static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Bool(bool value) => value ? "true" : "false";

    // 4.5 이름·값 표시. metrics.md 4.7 메시지와 같다(제어 문자 \uXXXX, 64자 넘으면 자름). 빈 값은 (빈 칸).
    public static string Show(string value) => value.Length == 0 ? EmptyValue : CompareRules.Show(value);

    // 시간 합이 decimal 범위를 넘을 때 {total}의 글자. 판정은 not_met이다(4.4).
    // 스펙이 글자를 정하지 않았다(카드 "PR (b)에 넘기는 것" 2). 정확한 합을 쓴다. 소수 자릿수는 decimal 덧셈처럼 큰 쪽이다.
    // 두 값은 0 이상이다(Judge가 확인한다).
    public static string ExactSum(decimal a, decimal b)
    {
        var scale = Math.Max(a.Scale, b.Scale);
        var digits = (Mantissa(a, scale) + Mantissa(b, scale)).ToString(CultureInfo.InvariantCulture).PadLeft(scale + 1, '0');
        return scale == 0 ? digits : digits[..^scale] + "." + digits[^scale..];
    }

    // decimal의 정수 가수(96비트, 부호 포함)를 scale 자리 소수로 맞춘 값. value = 결과 / 10^scale이다.
    // scale은 value.Scale 이상이어야 한다. 반올림이 없다.
    public static BigInteger Mantissa(decimal value, int scale)
    {
        var bits = decimal.GetBits(value);
        var mantissa = (new BigInteger((uint)bits[2]) << 64) | (new BigInteger((uint)bits[1]) << 32) | new BigInteger((uint)bits[0]);
        return (value < 0 ? -mantissa : mantissa) * BigInteger.Pow(10, scale - value.Scale);
    }
}
