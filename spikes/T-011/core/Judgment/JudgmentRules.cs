using System.Globalization;

namespace Scorecard.Core.Judgment;

// T-011 스파이크 후보(머지 대상 아님). 판정의 값·문장을 이 파일 한 곳에 둔다(docs/spec/judgment.md 4.10).
// 원본은 스펙 4.2 기본값 표, 4.3 판정 표, 4.4 확인 표, 4.5 숫자 표, 4.6 읽은 문턱 표, 4.7 다음 할 일 표다.
// 실행 중에 스펙 문서를 읽지 않는다. 숫자는 InvariantCulture로 쓴다.
internal static class JudgmentRules
{
    // 4.2 기본값(지시문 12.1 예시의 값).
    public const decimal DefaultAccuracyMin = 95m;
    public const decimal DefaultMissingMax = 2m;
    public const bool DefaultTimeMustDrop = true;

    public const string KeyAccuracyMin = "accuracy_min";
    public const string KeyMissingMax = "missing_max";
    public const string KeyTimeMustDrop = "time_must_drop";

    // 4.5 몰림을 보는 최소 불일치 수.
    public const int FocusMinimum = 2;

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

    // 4.7 다음 할 일 키(표 순서).
    public const string TodoAddData = "add_data";
    public const string TodoAddCompare = "add_compare";
    public const string TodoMeasureTime = "measure_time";
    public const string TodoReviewMismatches = "review_mismatches";
    public const string TodoAddTypeColumn = "add_type_column";
    public const string TodoConfirmThresholds = "confirm_thresholds";

    // 기본값 표시. classification.md 4.6과 같은 글자.
    public const string DefaultMark = " (기본값 사용, 확인 필요)";

    // 빈 유형 값은 문장에 이렇게 쓴다.
    public const string EmptyValue = "(빈 칸)";

    // 4.3 판정 문장 틀.
    public const string NotVerifiedNoInputs = "검증 자료(case.yaml inputs)가 없어 미검증.";
    public const string NotVerifiedNoCompare = "대조할 열(case.yaml compare)이 없어 미검증.";

    public static string HoldAccuracy(decimal accuracyPct, decimal accuracyMin) =>
        Inv($"정확도 {accuracyPct}%가 문턱 {accuracyMin}%에 못 미치므로 보류.");

    public static string HoldMissing(decimal missingRatePct, decimal missingMax) =>
        Inv($"누락률 {missingRatePct}%가 문턱 {missingMax}%를 넘으므로 보류.");

    public static string HoldUnexplained(int unexplained) =>
        Inv($"사람이 설명할 수 없다고 적은 불일치가 {unexplained}건 있으므로 보류.");

    public const string ConditionalTimeNotMeasured = "정확도·누락률은 문턱을 충족하나 시간을 재지 않아 줄었는지 알 수 없으므로 조건부.";

    public static string ConditionalTimeNotMet(decimal total, decimal before) =>
        Inv($"정확도·누락률은 문턱을 충족하나 처리·검토 시간 {total}분이 적용 전 {before}분보다 짧지 않으므로 조건부.");

    public static string ConditionalFocus(int bad, string group, string value) =>
        Inv($"정확도·누락률은 문턱을 충족하나 불일치 {bad}건이 모두 {group} = {value}인 행이므로 조건부.");

    public const string Pass = "정확도·누락률이 문턱을 충족하고 조건부·보류 조건에 걸리지 않으므로 통과.";

    // 4.4 확인 문장 틀.
    public static string AccuracyMet(decimal pct, int match, int reference, decimal min) =>
        Inv($"정확도 {pct}%({match}/{reference})가 문턱 {min}% 이상이다.");

    public static string AccuracyNotMet(decimal pct, int match, int reference, decimal min) =>
        Inv($"정확도 {pct}%({match}/{reference})가 문턱 {min}%에 못 미친다.");

    public static string MissingMet(decimal pct, int missing, int reference, decimal max) =>
        Inv($"누락률 {pct}%({missing}/{reference})가 문턱 {max}% 이하다.");

    public static string MissingNotMet(decimal pct, int missing, int reference, decimal max) =>
        Inv($"누락률 {pct}%({missing}/{reference})가 문턱 {max}%를 넘는다.");

    public const string UnexplainedNone = "불일치가 없다.";

    public static string UnexplainedNotReviewed(int bad) => Inv($"불일치 {bad}건이 설명되는지 사람이 아직 보지 않았다.");

    public static string UnexplainedExplained(int bad) => Inv($"불일치 {bad}건을 사람이 보고 모두 설명했다.");

    public static string UnexplainedFound(int bad, int unexplained) => Inv($"불일치 {bad}건 중 {unexplained}건을 사람이 설명할 수 없다고 적었다.");

    public const string TimeOff = "시간 조건을 쓰지 않는다(time_must_drop: false).";
    public const string TimeNotMeasured = "시간을 재지 않았다.";

    public static string TimeMet(decimal process, decimal review, decimal total, decimal before) =>
        Inv($"처리 {process}분 + 검토 {review}분 = {total}분이 적용 전 {before}분보다 짧다.");

    public static string TimeNotMet(decimal process, decimal review, decimal total, decimal before) =>
        Inv($"처리 {process}분 + 검토 {review}분 = {total}분이 적용 전 {before}분보다 짧지 않다.");

    public static string FocusFew(int bad) => Inv($"불일치가 {bad}건이라 몰림을 보지 않는다.");

    public static string FocusOff(int bad) => Inv($"기준 자료에 키·대조 열 밖의 열이 없어 불일치 {bad}건이 한 유형에 몰렸는지 볼 수 없다.");

    public static string FocusOneValue(string columns) => Inv($"키·대조 열 밖의 열({columns})이 기준 자료에서 모두 값이 하나뿐이라 몰림을 볼 수 없다.");

    public static string FocusFound(int bad, string group, string value, int rowsIn, int rowsOut) =>
        Inv($"불일치 {bad}건이 모두 {group} = {value}인 행이다({value} {rowsIn}행 중 {bad}행, 다른 값 {rowsOut}행 중 0행).");

    public static string FocusSpread(int bad, string columns) => Inv($"불일치 {bad}건이 한 값에 몰린 열이 없다(본 열: {columns}).");

    // 4.7 다음 할 일 문구.
    public const string AddDataText = "기준 자료(사람이 만든 결과)를 reference/에, 결과 자료(AI 적용 후 결과)를 candidate/에 넣고 case.yaml inputs에 파일 패턴과 키 열을 적는다.";
    public const string AddCompareText = "case.yaml compare에 대조할 열과 허용 오차를 적는다.";
    public const string MeasureTimeText = "건당 처리 시간·검토 시간과 적용 전 시간(분)을 잰다.";

    public static string ReviewMismatchesText(int bad) => Inv($"불일치 {bad}건을 보고 설명이 되는지 적는다.");

    public const string AddTypeColumnText = "불일치가 한 유형에 몰렸는지 보려면 기준 자료에 유형 열(키·대조 열 밖의 열)을 더한다.";

    public static string ConfirmThresholdsText(string defaults) => Inv($"문턱에 기본값을 썼다({defaults}). 맞는지 확인하고 case.yaml thresholds에 적는다.");

    public static string Bool(bool value) => value ? "true" : "false";

    public static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
