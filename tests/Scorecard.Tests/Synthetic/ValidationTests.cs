using Scorecard.Core.Cases;
using Scorecard.Core.Synthetic;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Synthetic;

// 스펙 5절 예외 표, 4.6 case.yaml과 계획 맞추기, 3.3 CSV 값 규칙.
// 5절 표의 "상황" 칸마다 아래 Cases에 경우가 있어야 한다. 표에 행이 늘면 여기서 실패한다.
// 메시지는 한글이고, 어느 필드·값인지 적는다(5절 아래). 필드·값이 정해지는 경우만 이름을 찾는다.
[Trait("Spec", "synthetic-examples")]
public class ValidationTests
{
    private const string Example = "table-compare";

    // 3.3·5절: CSV 값에 쓸 수 없는 것. Cases보다 먼저 둔다(정적 필드는 적힌 순서로 초기화된다). 0x20~0x7E 밖(제어 문자, DEL)도 넣는다.
    private static readonly string[] BadCells = ["a,b", "a\"b", "a\nb", "a\rb", "a\tb", "\t", "가", "", "a\u007Fb", "a\u0001b"];

    // 상황 → 경우 목록. Expect는 메시지에 있어야 할 필드·값(없으면 null).
    private static readonly Dictionary<string, Case[]> Cases = new(StringComparer.Ordinal)
    {
        ["`definition`·`plan`이 null"] =
        [
            new("definition null", () => SyntheticGenerator.Generate(null!, Plan()), null),
            new("plan null", () => SyntheticGenerator.Generate(TableCompare(), null!), null),
        ],
        ["`case.yaml`에 `inputs`가 없음"] =
        [
            new("mixed-exceptions", () => SyntheticGenerator.Generate(SyntheticInput.CaseOf("mixed-exceptions"), Plan()), null),
        ],
        ["`compare` 필드가 계획 열에 없음"] =
        [
            new("length 열 없음", () => Generate(Plan() with { Columns = [.. Plan().Columns.Where(c => c.Name != "length")], MismatchColumns = ["mean_value"] }), "length"),
        ],
        ["값 불일치 열이 `compare` 필드가 아님"] =
        [
            new("signal_type", () => Generate(Plan() with { MismatchColumns = ["signal_type"] }), "signal_type"),
            new("없는 열", () => Generate(Plan() with { MismatchColumns = ["no_such_column"] }), "no_such_column"),
        ],
        ["허용오차 안 차이 열에 `tolerance_pct`가 없음"] =
        [
            new("정수 열 length", () => Generate(Plan() with { WithinColumn = "length" }), "length"),
            new("compare 밖 열 signal_type", () => Generate(Plan() with { WithinColumn = "signal_type" }), "signal_type"),
            new("열 없이 건수만", () => Generate(Plan() with { WithinColumn = null }), null),
        ],
        ["`tolerance_pct` 필드가 정수 열"] =
        [
            new("mean_value 정수", () => Generate(Plan() with { Columns = Replace(Plan(), SyntheticColumn.Integer("mean_value", 10, 99)) }), "mean_value"),
        ],
        ["키 자릿수가 모자람(기준+추가 건수의 자릿수보다 작음)"] =
        [
            new("31건에 1자리", () => Generate(Plan() with { KeyDigits = 1 }), null),
            new("9+1건에 1자리", () => Generate(Plan() with { KeyDigits = 1, Rows = 9, Extra = 1 }), null),
        ],
        ["선택지에 `,`나 한글"] =
        [
            new("쉼표", () => Generate(Plan() with { Columns = Replace(Plan(), SyntheticColumn.Choice("signal_type", "normal,x", "irregular")) }), "normal,x"),
            new("한글", () => Generate(Plan() with { Columns = Replace(Plan(), SyntheticColumn.Choice("signal_type", "정상", "irregular")) }), "정상"),
        ],
        ["파일 이름에 폴더"] =
        [
            new("sub/", () => Generate(Plan() with { FileName = "sub/logs.csv" }), null),
            new("sub\\", () => Generate(Plan() with { FileName = "sub\\logs.csv" }), null),
            new("../", () => Generate(Plan() with { FileName = "../logs.csv" }), null),
        ],
        ["몰림 행이 누락 + 값 불일치보다 적음"] =
        [
            // irregular 기준 행 12개(6.1). 누락 10 + 값 불일치 2 = 12는 된다(PositiveControls), 11 + 2 = 13은 안 된다.
            new("누락 11", () => Generate(Plan() with { Missing = 11, Within = 0, WithinColumn = null }), null),
        ],
        ["범위가 좁아 불일치 값을 못 만듦"] =
        [
            new("length 50~50", () => Generate(Plan() with { Columns = Replace(Plan(), SyntheticColumn.Integer("length", 50, 50)) }), "length"),
        ],
        ["CSV 값에 `,` `\"` `\\n` `\\r` `\\t`, 한글, 빈 값"] =
        [
            .. BadCells.Select(v => new Case($"Render '{Show(v)}'", () => SyntheticCsv.Render(["k", "v"], [new SyntheticRow("k1", [v], SyntheticGenerator.PlantedNone)]), null)),
            .. BadCells.Select(v => new Case($"CheckCell '{Show(v)}'", () => SyntheticCsv.CheckCell(v), null)),
            new("머리 행", () => SyntheticCsv.Render(["k", "a,b"], []), "a,b"),
            new("키", () => SyntheticCsv.Render(["k", "v"], [new SyntheticRow("k,1", ["1"], SyntheticGenerator.PlantedNone)]), "k,1"),
        ],
        ["같은 열 이름"] =
        [
            new("length 두 번", () => Generate(Plan() with { Columns = [.. Plan().Columns, SyntheticColumn.Integer("length", 50, 500)] }), "length"),
            new("키 열 이름과 같음", () => Generate(Plan() with { Columns = [.. Plan().Columns, SyntheticColumn.Integer("log_id", 0, 9)] }), "log_id"),
        ],
    };

    // 리뷰 1회차 B1·N2: 5절 표에 행이 없는 입력 검사. 4.3 칸 읽기, 4.4 열 종류, 4.6 맞추기, 3.3 칸 값, 4.3 "열마다 한 행", 4.5 "예외".
    // 경우마다 그 조건 하나만 어긋나게 만든다. 다른 검사(몰림 행 수, 심을 행 수, 뽑기 폭 등)가 대신 걸리면 메시지 확인에서 실패한다.
    // Expect: 메시지에 모두 있어야 할 필드·값. 파서가 막는 입력(compare 중복, tolerance·tolerance_pct 동시, CSV 규칙 밖 키 열 이름)은 모델을 직접 만든다.
    // 짝(경계 바로 안쪽은 생성된다): Input_check_controls_generate.
    private static readonly Dictionary<string, Check> InputChecks = new(StringComparer.Ordinal)
    {
        // B1. compare 필드는 한 번씩(4.6).
        ["compare 필드 두 번(length)"] = new(() => SyntheticGenerator.Generate(WithCompare([.. CompareOf(), new CompareField("length", 0m, null)]), Plan()), ["length"]),

        // B1. tolerance와 tolerance_pct는 하나만(4.6 표). 다른 종류 검사로 넘어가지 않는다(메시지에 tolerance_pct).
        ["tolerance와 tolerance_pct 동시(mean_value)"] = new(
            () => SyntheticGenerator.Generate(WithCompare([.. CompareOf().Select(f => f.Field == "mean_value" ? f with { Tolerance = 0m } : f)]), Plan()),
            ["mean_value", "tolerance_pct"]),

        // B1. 키 열 이름(inputs.key)도 머리 행 칸이다(3.3).
        ["키 열 이름에 쉼표(log,id)"] = new(() => SyntheticGenerator.Generate(WithKey("log,id"), Plan()), ["log,id"]),
        ["키 열 이름에 한글(로그번호)"] = new(() => SyntheticGenerator.Generate(WithKey("로그번호"), Plan()), ["로그번호"]),
        ["키 열 이름 빈 값"] = new(() => SyntheticGenerator.Generate(WithKey(""), Plan()), []),

        // B1. 뽑기 폭(최대 − 최소 + 1, 소수는 자릿수 단위)은 int 안(4.5의 1, NextBelow). 폭 = int.MaxValue부터 안 된다.
        ["정수 뽑기 폭 int 초과(start_index 0 ~ 2147483647)"] = new(() => Generate(WithColumn(SyntheticColumn.Integer("start_index", 0, int.MaxValue))), ["start_index", "2147483647"]),
        ["소수 뽑기 폭 int 초과(ratio 0.00 ~ 21474836.47)"] = new(() => Generate(WithExtraColumn(SyntheticColumn.Decimal("ratio", 0.00m, 21474836.47m, 2))), ["ratio", "21474836.47"]),

        // B1. 소수 최소·최대의 자릿수는 Scale 이하(4.4 "소수 자릿수는 최소값의 자릿수").
        ["소수 최소값 자릿수 > Scale(ratio 0.001)"] = new(() => Generate(WithExtraColumn(SyntheticColumn.Decimal("ratio", 0.001m, 1.00m, 2))), ["ratio", "0.001"]),
        ["소수 최대값 자릿수 > Scale(ratio 1.005)"] = new(() => Generate(WithExtraColumn(SyntheticColumn.Decimal("ratio", 0.00m, 1.005m, 2))), ["ratio", "1.005"]),

        // B1. 허용오차 안 차이 열은 건수가 0이어도 tolerance_pct가 있는 compare 필드다(4.3 칸 읽기).
        ["허용오차 안 열이 정수 열, 건수 0(length)"] = new(() => Generate(Plan() with { WithinColumn = "length", Within = 0 }), ["length"]),
        ["허용오차 안 열이 compare 밖, 건수 0(signal_type)"] = new(() => Generate(Plan() with { WithinColumn = "signal_type", Within = 0 }), ["signal_type"]),
        ["허용오차 안 열이 없는 열, 건수 0(no_such_column)"] = new(() => Generate(Plan() with { WithinColumn = "no_such_column", Within = 0 }), ["no_such_column"]),

        // B1. 몰림은 열과 값이 같이 있다(4.3 "`열` = `값`").
        ["몰림 값만 있고 열이 없음(irregular)"] = new(() => Generate(Plan() with { FocusColumn = null, FocusValue = "irregular" }), ["irregular"]),

        // N2. 몰림 열이 있으면 값도 있다. 누락·값 불일치 0행으로 몰림 행 수 검사가 대신 걸리지 않게 한다.
        ["몰림 열만 있고 값이 없음(signal_type)"] = new(() => Generate(Plan() with { FocusValue = null, Missing = 0, MismatchColumns = [] }), ["signal_type"]),

        // N2. 몰림 열은 계획 열이다.
        ["몰림 열이 계획 열에 없음(no_such_column)"] = new(() => Generate(Plan() with { FocusColumn = "no_such_column" }), ["no_such_column"]),

        // N2. 선택지는 2개 이상, 서로 다르다(4.4, 4.5의 4 "자기 자신은 나오지 않는다"). 몰림·값 불일치에 안 쓰는 열을 더해 본다.
        ["선택지 1개(extra_label)"] = new(() => Generate(WithExtraColumn(SyntheticColumn.Choice("extra_label", "p"))), ["extra_label"]),
        ["선택지 0개(extra_label)"] = new(() => Generate(WithExtraColumn(SyntheticColumn.Choice("extra_label"))), ["extra_label"]),
        ["선택지 두 번(extra_label dup-x)"] = new(() => Generate(WithExtraColumn(SyntheticColumn.Choice("extra_label", "dup-x", "dup-x"))), ["extra_label", "dup-x"]),

        // N2. 정수 범위: 최소·최대가 정수, 최소 ≤ 최대(4.4). 값 불일치를 심지 않는 열(start_index)로 본다.
        ["정수 최소값이 정수 아님(start_index 0.5)"] = new(() => Generate(WithColumn(new SyntheticColumn("start_index", SyntheticColumn.KindInteger, 0.5m, 9000m, 0, []))), ["start_index", "0.5"]),
        ["정수 최대값이 정수 아님(start_index 9000.5)"] = new(() => Generate(WithColumn(new SyntheticColumn("start_index", SyntheticColumn.KindInteger, 0m, 9000.5m, 0, []))), ["start_index", "9000.5"]),
        ["정수 최소 > 최대(start_index 9000 ~ 0)"] = new(() => Generate(WithColumn(SyntheticColumn.Integer("start_index", 9000, 0))), ["start_index", "9000"]),

        // N2. 소수: 최소 ≤ 최대, 자릿수 0 ~ 6.
        ["소수 최소 > 최대(ratio 1.00 ~ 0.00)"] = new(() => Generate(WithExtraColumn(SyntheticColumn.Decimal("ratio", 1.00m, 0.00m, 2))), ["ratio", "1.00"]),
        ["소수 자릿수 7(ratio)"] = new(() => Generate(WithExtraColumn(SyntheticColumn.Decimal("ratio", 0m, 1m, 7))), ["ratio", "7"]),
        ["소수 자릿수 -1(ratio)"] = new(() => Generate(WithExtraColumn(SyntheticColumn.Decimal("ratio", 0m, 1m, -1))), ["ratio", "-1"]),

        // N2. 건수 하한(4.3 칸 읽기). 기준 건수 0은 심을 행도 0으로 둬 "심을 행 > 기준 건수"가 대신 걸리지 않게 한다.
        ["기준 건수 0"] = new(() => Generate(Empty() with { Rows = 0 }), ["기준 건수", "0"]),
        ["키 자릿수 0"] = new(() => Generate(Plan() with { KeyDigits = 0 }), ["키 자릿수", "0"]),
        ["누락 -1"] = new(() => Generate(Plan() with { Missing = -1 }), ["누락", "-1"]),
        ["추가 -1"] = new(() => Generate(Plan() with { Extra = -1 }), ["추가", "-1"]),
        ["허용오차 안 차이 -1"] = new(() => Generate(Plan() with { Within = -1 }), ["허용오차 안 차이", "-1"]),

        // N2. 심을 행(누락 + 값 불일치 + 허용오차 안 차이)은 기준 건수 이하. 몰림을 빼서 몰림 행 수 검사가 대신 걸리지 않게 한다.
        ["심을 행 8 > 기준 건수 7"] = new(() => Generate(Plan() with { Rows = 7, Within = 5, FocusColumn = null, FocusValue = null }), ["기준 건수", "7"]),

        // N2. 키 접두어와 열 이름도 CSV 칸 규칙(3.3)을 지킨다. 몰림·compare에 안 쓰는 열을 더해 본다.
        ["키 접두어에 쉼표(l,g)"] = new(() => Generate(Plan() with { KeyPrefix = "l,g" }), ["l,g"]),
        ["키 접두어에 한글(로그)"] = new(() => Generate(Plan() with { KeyPrefix = "로그" }), ["로그"]),
        ["열 이름에 쉼표(ex,tra)"] = new(() => Generate(WithExtraColumn(SyntheticColumn.Choice("ex,tra", "p", "q"))), ["ex,tra"]),
        ["열 이름에 한글(추가열)"] = new(() => Generate(WithExtraColumn(SyntheticColumn.Choice("추가열", "p", "q"))), ["추가열"]),

        // N2. 값 불일치 열은 열마다 한 행(4.3 "열마다 한 행을 허용 오차 밖으로"). 같은 열을 두 번 적으면 안 된다.
        ["값 불일치 열 두 번(length)"] = new(() => Generate(Plan() with { MismatchColumns = ["length", "length"] }), ["length"]),

        // 4.5의 4·5 "예외". 범위 50.00 ~ 50.00이면 어느 부호로도 범위를 벗어난다(리뷰 1회차 N1: 소수도 범위를 지킨다).
        ["소수 값 불일치가 뒤집어도 범위 밖(mean_value 50.00 ~ 50.00)"] = new(
            () => Generate(Plan() with { Columns = Replace(Plan(), SyntheticColumn.Decimal("mean_value", 50.00m, 50.00m, 2)), MismatchColumns = ["mean_value"], WithinColumn = null, Within = 0 }),
            ["mean_value"]),
        ["소수 허용오차 안 값이 뒤집어도 범위 밖(mean_value 50.00 ~ 50.00)"] = new(
            () => Generate(Plan() with { Columns = Replace(Plan(), SyntheticColumn.Decimal("mean_value", 50.00m, 50.00m, 2)), MismatchColumns = ["length"] }),
            ["mean_value"]),

        // 4.5의 4·5 "예외". 값이 작아(0.01 ~ 0.05) 반올림하면 기준과 같다: 값 불일치는 허용 오차 안, 허용오차 안 값은 기준과 같음.
        ["소수 값 불일치가 허용 오차 안(mean_value 0.01 ~ 0.05)"] = new(
            () => Generate(Plan() with { Columns = Replace(Plan(), SyntheticColumn.Decimal("mean_value", 0.01m, 0.05m, 2)), MismatchColumns = ["mean_value"], WithinColumn = null, Within = 0 }),
            ["mean_value"]),
        ["소수 허용오차 안 값이 기준과 같음(mean_value 0.01 ~ 0.05)"] = new(
            () => Generate(Plan() with { Columns = Replace(Plan(), SyntheticColumn.Decimal("mean_value", 0.01m, 0.05m, 2)), MismatchColumns = ["length"] }),
            ["mean_value"]),
    };

    public static TheoryData<string> Situations => new(Cases.Keys);

    public static TheoryData<string> InputCheckLabels => new(InputChecks.Keys);

    // 5절 표의 상황 = 이 파일의 경우. 결과 칸의 첫 `코드`가 예외 이름이다.
    [Fact]
    public void Error_table_situations_all_have_cases()
    {
        var rows = SyntheticSpec.Table(SyntheticSpec.ErrorHeader);

        Assert.Equal(rows.Select(c => c[0]).Order(StringComparer.Ordinal), Cases.Keys.Order(StringComparer.Ordinal));
        Assert.All(rows, c => Assert.Contains(SpecDocument.CodeSpans(c[1])[0], new[] { nameof(ArgumentNullException), nameof(ArgumentException) }));
    }

    // 5절 표의 행마다: 결과 칸의 예외가 난다. 메시지는 한글이고, 정해진 필드·값이 있으면 그 이름이 있다.
    [Theory]
    [MemberData(nameof(Situations))]
    public void Situation_throws_spec_exception(string situation)
    {
        var row = Assert.Single(SyntheticSpec.Table(SyntheticSpec.ErrorHeader), c => c[0] == situation);
        var expected = SpecDocument.CodeSpans(row[1])[0];
        var wrong = new List<string>();

        foreach (var (label, action, expect) in Cases[situation])
        {
            Exception? caught = null;
            try
            {
                action();
            }
            catch (Exception ex)
            {
                caught = ex;
            }

            if (caught is null)
            {
                wrong.Add($"{label}: 예외 없음");
                continue;
            }

            var ok = expected == nameof(ArgumentNullException) ? caught is ArgumentNullException : caught is ArgumentException;
            if (!ok)
            {
                wrong.Add($"{label}: {expected}가 아니라 {caught.GetType().Name}: {caught.Message}");
                continue;
            }

            if (!caught.Message.Any(ch => ch is >= '가' and <= '힣') && caught is not ArgumentNullException)
            {
                wrong.Add($"{label}: 메시지가 한글이 아니다: {caught.Message}");
            }

            if (expect is not null && !caught.Message.Contains(expect, StringComparison.Ordinal))
            {
                wrong.Add($"{label}: 메시지에 '{expect}'가 없다: {caught.Message}");
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    // 리뷰 1회차 B1·N2(InputChecks): ArgumentException이고(ArgumentNullException·다른 예외 아님), 메시지는 한글이며, Expect가 모두 들어 있다.
    // 생성기 안쪽(NextBelow, 목록 색인)에서 나는 ArgumentOutOfRangeException은 한글·필드가 없어 여기서 걸린다.
    [Theory]
    [MemberData(nameof(InputCheckLabels))]
    public void Input_check_outside_error_table_throws(string label)
    {
        var (action, expect) = InputChecks[label];
        var caught = Record.Exception(action);

        Assert.True(caught is not null, $"{label}: 예외 없음");
        Assert.True(caught is ArgumentException and not ArgumentNullException, $"{label}: ArgumentException이 아니라 {caught.GetType().Name}: {caught.Message}");
        Assert.True(caught.Message.Any(ch => ch is >= '가' and <= '힣'), $"{label}: 메시지가 한글이 아니다: {caught.Message}");
        var absent = expect.Where(e => !caught.Message.Contains(e, StringComparison.Ordinal)).ToArray();
        Assert.True(absent.Length == 0, $"{label}: 메시지에 {string.Join(", ", absent.Select(e => $"'{e}'"))}가 없다: {caught.Message}");
    }

    // 위 InputChecks가 그 조건 때문에 실패한다는 것을 보이는 짝: 경계 바로 안쪽은 생성된다.
    [Fact]
    public void Input_check_controls_generate()
    {
        // 뽑기 폭 = int.MaxValue − 1까지는 된다(정수 0 ~ 2147483646, 소수 0.00 ~ 21474836.46).
        Generate(WithColumn(SyntheticColumn.Integer("start_index", 0, int.MaxValue - 1)));
        Generate(WithExtraColumn(SyntheticColumn.Decimal("ratio", 0.00m, 21474836.46m, 2)));

        // 자릿수 0과 6, 최소 = 최대(값 불일치를 심지 않는 열).
        Generate(WithExtraColumn(SyntheticColumn.Decimal("ratio", 0m, 1m, 0)));
        Generate(WithExtraColumn(SyntheticColumn.Decimal("ratio", 0.000001m, 1m, 6)));
        Generate(WithExtraColumn(SyntheticColumn.Decimal("ratio", 5.00m, 5.00m, 2)));
        Generate(WithColumn(SyntheticColumn.Integer("start_index", 7, 7)));

        // 허용오차 안 열이 tolerance_pct 필드면 건수 0도 된다. 몰림 열·값이 둘 다 없어도 된다.
        Generate(Plan() with { Within = 0 });
        Generate(Plan() with { FocusColumn = null, FocusValue = null });

        // 심을 행 = 기준 건수(1 + 2 + 4 = 7)는 된다. 심을 것이 없으면 기준 1건도 된다.
        Assert.Equal(7, Generate(Plan() with { Rows = 7, Within = 4, FocusColumn = null, FocusValue = null }).Reference.Count);
        Assert.Single(Generate(Empty() with { Rows = 1 }).Reference);

        // 몰림 열만 있고 값이 없는 경우와 같은 계획(누락·값 불일치 0행)에 값을 주면 된다.
        Generate(Plan() with { Missing = 0, MismatchColumns = [] });
    }

    // 리뷰 1회차 N3, 3.3 "칸 앞뒤 공백 없음": 앞뒤 공백이 있는 칸은 CheckCell·Render·Generate(선택지) 모두 ArgumentException.
    // 가운데 공백("a b")은 된다(Positive_controls_generate).
    [Theory]
    [InlineData(" a")]
    [InlineData("a ")]
    [InlineData(" ")]
    [InlineData(" a b ")]
    public void Cell_with_leading_or_trailing_space_is_rejected(string value)
    {
        var actions = new (string Where, Action Action)[]
        {
            ("CheckCell", () => SyntheticCsv.CheckCell(value)),
            ("Render", () => SyntheticCsv.Render(["k", "v"], [new SyntheticRow("k1", [value], SyntheticGenerator.PlantedNone)])),
            ("Generate 선택지", () => Generate(WithExtraColumn(SyntheticColumn.Choice("extra_label", "p", value)))),
        };
        var wrong = new List<string>();

        foreach (var (where, action) in actions)
        {
            var caught = Record.Exception(action);
            if (caught is not ArgumentException || caught is ArgumentNullException)
            {
                wrong.Add($"{where} '{value}': ArgumentException이 아니다({caught?.GetType().Name ?? "예외 없음"})");
            }
            else if (!caught.Message.Any(ch => ch is >= '가' and <= '힣'))
            {
                wrong.Add($"{where} '{value}': 메시지가 한글이 아니다: {caught.Message}");
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    // 위 경우들이 그 조건 때문에 실패한다는 것을 보이는 짝: 경계 바로 안쪽은 생성된다.
    [Fact]
    public void Positive_controls_generate()
    {
        Generate(Plan());

        // 몰림 행 12 = 누락 10 + 값 불일치 2.
        Assert.Equal(10, Generate(Plan() with { Missing = 10, Within = 0, WithinColumn = null }).Reference.Count(r => r.Planted == SyntheticGenerator.PlantedMissing));

        // 9건 + 추가 0건이면 1자리로 된다.
        Assert.Equal("log9", Generate(Plan() with { KeyDigits = 1, Rows = 9, Extra = 0 }).Reference[^1].Key);

        // 계획 열은 compare에 없어도 된다(4.6). signal_type이 그렇다. 하나 더 둬도 된다.
        Generate(Plan() with { Columns = [.. Plan().Columns, SyntheticColumn.Choice("extra_label", "p", "q")] });

        // CSV 값으로 쓸 수 있는 것.
        foreach (var value in new[] { "log01", "58.25", "item-a", "-3", "a b", "~" })
        {
            SyntheticCsv.CheckCell(value);
        }
    }

    // 4.6 표: compare 항목별로 맞는 열 종류만 생성된다. 나머지 종류는 ArgumentException.
    // 케이스: n(tolerance 0), d(tolerance_pct 1.0), c(허용 오차 없음). 열 하나씩 세 종류로 바꿔 본다.
    [Fact]
    public void Compare_kind_must_match_column_kind()
    {
        var definition = SyntheticInput.Case(SyntheticInput.ThreeKindCase);
        var kinds = PlanTableTests.CompareKinds();
        var wrong = new List<string>();

        foreach (var field in definition.Compare)
        {
            foreach (var kind in new[] { SyntheticColumn.KindInteger, SyntheticColumn.KindDecimal, SyntheticColumn.KindChoice })
            {
                var plan = ThreeKindPlan() with { Columns = Replace(ThreeKindPlan(), Column(field.Field, kind)) };
                var fits = kinds[PlanTableTests.CompareLabel(field)] == kind;
                try
                {
                    SyntheticGenerator.Generate(definition, plan);
                    if (!fits)
                    {
                        wrong.Add($"{field.Field} → {kind}: 예외가 나야 한다");
                    }
                }
                catch (ArgumentException ex)
                {
                    if (fits)
                    {
                        wrong.Add($"{field.Field} → {kind}: 생성되어야 한다({ex.Message})");
                    }
                }
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    // 4.6 "`tolerance`(정수 값)": 정수가 아닌 tolerance는 정수 열과 맞지 않는다.
    [Fact]
    public void Fractional_tolerance_does_not_fit_integer_column()
    {
        var definition = SyntheticInput.Case(SyntheticInput.ThreeKindCase.Replace("{field: n, tolerance: 0}", "{field: n, tolerance: 0.5}", StringComparison.Ordinal));

        Assert.Equal(0.5m, definition.Compare[0].Tolerance);
        Assert.Throws<ArgumentException>(() => SyntheticGenerator.Generate(definition, ThreeKindPlan()));
    }

    // 4.13: 칸 수가 머리 행과 다른 행은 ArgumentException.
    [Fact]
    public void Row_cell_count_must_match_header()
    {
        Assert.Throws<ArgumentException>(() => SyntheticCsv.Render(["k", "a", "b"], [new SyntheticRow("k1", ["1"], SyntheticGenerator.PlantedNone)]));
    }

    // 세 종류 열과 심기 다섯 가지를 쓰는 계획(ThreeKindCase에 맞춤). 4.11 G2 고정 계획과 같은 모양이다.
    public static SyntheticPlan ThreeKindPlan() => new(
        Seed: 7,
        FileName: "rows.csv",
        KeyPrefix: "r",
        KeyDigits: 2,
        Rows: 12,
        Columns: [SyntheticColumn.Integer("n", 0, 999), SyntheticColumn.Decimal("d", 10m, 99.99m, 2), SyntheticColumn.Choice("c", "a", "b", "c")],
        MismatchColumns: ["n", "d", "c"],
        Missing: 1,
        Extra: 1,
        WithinColumn: "d",
        Within: 2,
        FocusColumn: null,
        FocusValue: null,
        ShuffleCandidate: true);

    private static SyntheticColumn Column(string name, string kind) => kind switch
    {
        SyntheticColumn.KindInteger => SyntheticColumn.Integer(name, 0, 999),
        SyntheticColumn.KindDecimal => SyntheticColumn.Decimal(name, 10m, 99.99m, 2),
        _ => SyntheticColumn.Choice(name, "a", "b", "c"),
    };

    private static CaseDefinition TableCompare() => SyntheticInput.CaseOf(Example);

    private static SyntheticPlan Plan() => SyntheticSpec.Plan(Example);

    private static SyntheticTables Generate(SyntheticPlan plan) => SyntheticGenerator.Generate(TableCompare(), plan);

    // table-compare 계획에서 심기·몰림을 모두 뺀 것.
    private static SyntheticPlan Empty() => Plan() with
    {
        MismatchColumns = [],
        Missing = 0,
        Extra = 0,
        WithinColumn = null,
        Within = 0,
        FocusColumn = null,
        FocusValue = null,
    };

    // 같은 이름의 열을 바꾼다.
    private static SyntheticColumn[] Replace(SyntheticPlan plan, SyntheticColumn column) =>
        [.. plan.Columns.Select(c => c.Name == column.Name ? column : c)];

    private static SyntheticPlan WithColumn(SyntheticColumn column) => Plan() with { Columns = Replace(Plan(), column) };

    // 몰림·compare에 안 쓰는 열을 끝에 더한다(4.6 "계획 열은 compare에 없어도 된다").
    private static SyntheticPlan WithExtraColumn(SyntheticColumn column) => Plan() with { Columns = [.. Plan().Columns, column] };

    private static CompareField[] CompareOf() => [.. TableCompare().Compare];

    // 파서를 거치지 않고 compare·inputs.key를 바꾼 table-compare 케이스.
    private static CaseDefinition WithCompare(CompareField[] compare) => TableCompare() with { Compare = compare };

    private static CaseDefinition WithKey(string key) => TableCompare() with { Inputs = TableCompare().Inputs! with { Key = key } };

    private static string Show(string value) =>
        value.Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);

    private sealed record Case(string Label, Action Action, string? Expect);

    private sealed record Check(Action Action, string[] Expect);
}
