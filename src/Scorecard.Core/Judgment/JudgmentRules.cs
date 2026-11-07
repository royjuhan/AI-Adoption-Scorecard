using System.Globalization;
using System.Numerics;
using Scorecard.Core.Comparison;

namespace Scorecard.Core.Judgment;

// 판정의 값·숫자·문장을 이 파일 한 곳에만 둔다(docs/spec/judgment.md 4.13, classification.md 4.9와 같은 방식).
// 원본은 스펙 4.2 기본값 표, 4.3 판정 표, 4.4 확인 표, 4.5 숫자 표, 4.6 기본값 표시, 4.7 다음 할 일 표다.
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

    // 4.7 다음 할 일 키.
    public const string TodoAddData = "add_data";
    public const string TodoAddCompare = "add_compare";
    public const string TodoMeasureTime = "measure_time";
    public const string TodoReviewMismatches = "review_mismatches";
    public const string TodoAddTypeColumn = "add_type_column";
    public const string TodoConfirmThresholds = "confirm_thresholds";

    // 4.6 기본값 표시. 공백 하나 + 문구. classification.md 4.6과 같은 글자다.
    public const string DefaultMark = " (기본값 사용, 확인 필요)";

    // 4.5 빈 값 표시.
    public const string EmptyValue = "(빈 칸)";

    // 4.3 판정 문장 틀.
    public const string NoInputsVerdict = "검증 자료(case.yaml inputs)가 없어 미검증.";
    public const string NoCompareFieldsVerdict = "대조할 열(case.yaml compare)이 없어 미검증.";
    public const string TimeNotMeasuredVerdict = "정확도·누락률은 문턱을 충족하나 시간을 재지 않아 줄었는지 알 수 없으므로 조건부.";
    public const string PassVerdict = "정확도·누락률이 문턱을 충족하고 조건부·보류 조건에 걸리지 않으므로 통과.";

    public static string AccuracyVerdict(string accuracyPct, string accuracyMin) =>
        $"정확도 {accuracyPct}%가 문턱 {accuracyMin}%에 못 미치므로 보류.";

    public static string MissingVerdict(string missingRatePct, string missingMax) =>
        $"누락률 {missingRatePct}%가 문턱 {missingMax}%를 넘으므로 보류.";

    public static string UnexplainedVerdict(string unexplained) =>
        $"사람이 설명할 수 없다고 적은 불일치가 {unexplained}건 있으므로 보류.";

    public static string TimeNotMetVerdict(string total, string before) =>
        $"정확도·누락률은 문턱을 충족하나 처리·검토 시간 {total}분이 적용 전 {before}분보다 짧지 않으므로 조건부.";

    public static string FocusVerdict(string bad, string group, string value) =>
        $"정확도·누락률은 문턱을 충족하나 불일치 {bad}건이 모두 {group} = {value}인 행이므로 조건부.";

    // 4.4 확인 문장 틀.
    public static string AccuracyCheck(bool met, string accuracyPct, string match, string reference, string accuracyMin) => met
        ? $"정확도 {accuracyPct}%({match}/{reference})가 문턱 {accuracyMin}% 이상이다."
        : $"정확도 {accuracyPct}%({match}/{reference})가 문턱 {accuracyMin}%에 못 미친다.";

    public static string MissingCheck(bool met, string missingRatePct, string missing, string reference, string missingMax) => met
        ? $"누락률 {missingRatePct}%({missing}/{reference})가 문턱 {missingMax}% 이하다."
        : $"누락률 {missingRatePct}%({missing}/{reference})가 문턱 {missingMax}%를 넘는다.";

    public const string UnexplainedNoneCheck = "불일치가 없다.";

    public static string UnexplainedNotReviewedCheck(string bad) => $"불일치 {bad}건이 설명되는지 사람이 아직 보지 않았다.";

    public static string UnexplainedExplainedCheck(string bad) => $"불일치 {bad}건을 사람이 보고 모두 설명했다.";

    public static string UnexplainedFoundCheck(string bad, string unexplained) =>
        $"불일치 {bad}건 중 {unexplained}건을 사람이 설명할 수 없다고 적었다.";

    public const string TimeOffCheck = "시간 조건을 쓰지 않는다(time_must_drop: false).";
    public const string TimeNotMeasuredCheck = "시간을 재지 않았다.";

    public static string TimeCheck(bool met, string process, string review, string total, string before) => met
        ? $"처리 {process}분 + 검토 {review}분 = {total}분이 적용 전 {before}분보다 짧다."
        : $"처리 {process}분 + 검토 {review}분 = {total}분이 적용 전 {before}분보다 짧지 않다.";

    public static string FocusFewCheck(string bad) => $"불일치가 {bad}건이라 몰림을 보지 않는다.";

    public static string FocusOffCheck(string bad) =>
        $"기준 자료에 키·대조 열 밖의 열이 없어 불일치 {bad}건이 한 유형에 몰렸는지 볼 수 없다.";

    public static string FocusOneValueCheck(string columns) =>
        $"키·대조 열 밖의 열({columns})이 기준 자료에서 모두 값이 하나뿐이라 몰림을 볼 수 없다.";

    public static string FocusFoundCheck(string bad, string group, string value, string rowsIn, string rowsOut) =>
        $"불일치 {bad}건이 모두 {group} = {value}인 행이다({value} {rowsIn}행 중 {bad}행, 다른 값 {rowsOut}행 중 0행).";

    public static string FocusSpreadCheck(string bad, string columns) => $"불일치 {bad}건이 한 값에 몰린 열이 없다(본 열: {columns}).";

    // 4.7 다음 할 일 문구.
    public const string AddDataText =
        "기준 자료(사람이 만든 결과)를 reference/에, 결과 자료(AI 적용 후 결과)를 candidate/에 넣고 case.yaml inputs에 파일 패턴과 키 열을 적는다.";

    public const string AddCompareText = "case.yaml compare에 대조할 열과 허용 오차를 적는다.";
    public const string MeasureTimeText = "건당 처리 시간·검토 시간과 적용 전 시간(분)을 잰다.";
    public const string AddTypeColumnText = "불일치가 한 유형에 몰렸는지 보려면 기준 자료에 유형 열(키·대조 열 밖의 열)을 더한다.";

    public static string ReviewMismatchesText(string bad) => $"불일치 {bad}건을 보고 설명이 되는지 적는다.";

    public static string ConfirmThresholdsText(string defaults) =>
        $"문턱에 기본값을 썼다({defaults}). 맞는지 확인하고 case.yaml thresholds에 적는다.";

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

    // decimal의 정수 부분(96비트)을 scale 자리 소수로 맞춘 값.
    private static BigInteger Mantissa(decimal value, int scale)
    {
        var bits = decimal.GetBits(value);
        var mantissa = (new BigInteger((uint)bits[2]) << 64) | (new BigInteger((uint)bits[1]) << 32) | new BigInteger((uint)bits[0]);
        return mantissa * BigInteger.Pow(10, scale - value.Scale);
    }
}
