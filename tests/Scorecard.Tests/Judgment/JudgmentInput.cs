using System.Globalization;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;
using Scorecard.Tests.Comparison;
using Scorecard.Tests.Synthetic;

namespace Scorecard.Tests.Judgment;

// 판정 테스트 도우미(스펙 8절). 메모리에서 케이스·CSV를 만들어 CompareEngine.Compare(바이트)로 대조하고 JudgmentEngine.Judge를 부른다.
// 파일을 만들지 않는다. 장면 모양은 스펙 8절 글머리와 같다: 머리 행 id,<유형 열>,v, compare는 v tolerance 0,
// 값 불일치는 결과 v를 바꾸고 누락은 결과 행을 뺀다. 유형 열이 없으면 머리 행은 id,v다.
// T-012(11.17 test-engineer 2): case.yaml inputs.group 한 줄(Group)과 결과 쪽 중복 행(Duplicates)을 더할 수 있다.
internal static class JudgmentInput
{
    // 확인 결과 이름(4.4 표의 "결과" 칸 글자).
    public const string Met = "met";
    public const string NotMet = "not_met";
    public const string NoneResult = "none";
    public const string NotReviewed = "not_reviewed";
    public const string Explained = "explained";
    public const string Found = "found";
    public const string Off = "off";
    public const string NotMeasured = "not_measured";
    public const string Few = "few";
    public const string OneValue = "one_value";
    public const string Spread = "spread";

    // 장면 생성기가 쓰는 유형 열 이름.
    public const string Group = "grp";

    // 시간이 줄었다·줄지 않았다(6.2 첫 두 행과 같은 값).
    public static readonly JudgmentTimes TimesMet = new(10m, 6m, 3m);
    public static readonly JudgmentTimes TimesNotMet = new(10m, 7m, 3m);

    public static string Key(int index) => "k" + (index + 1).ToString("D4", CultureInfo.InvariantCulture);

    // 기준 행 count개. 앞 missing개는 누락, 다음 mismatch개는 값 불일치, 나머지는 일치. types(i)는 i번째 행의 유형 값.
    public static SceneRow[] Rows(int count, int mismatch = 0, int missing = 0, Func<int, string[]>? types = null) =>
        Enumerable.Range(0, count)
            .Select(i => new SceneRow(
                Key(i),
                types?.Invoke(i) ?? Array.Empty<string>(),
                i < missing ? SceneRow.Missing : i < missing + mismatch ? SceneRow.Mismatch : SceneRow.Match))
            .ToArray();

    // 추가 행(결과에만 있는 키) 키. 기준 키(k0001…)와 겹치지 않는다.
    public static string ExtraKey(int index) => "x" + (index + 1).ToString("D4", CultureInfo.InvariantCulture);

    // 확인 다섯의 결과를 받아 그 결과를 내는 장면(11.4·11.5·11.10 테스트). 문턱으로 정확도·누락률을 고른다:
    // 정확도 met = accuracy_min 0, not_met = 100. 누락률 met = missing_max 100, not_met = 0. 시간 off = time_must_drop false.
    // 검토 항목은 불일치만 쓴다. 불일치 수: UNEXPLAINED none이면 0, FOCUS few이면 1, 그 밖은 2. 누락률 not_met이면 첫 불일치가 누락이다.
    // 유형 열 grp는 늘 있다. inputs.group은 FOCUS off일 때만 적지 않는다(11.8 규칙 2). off 장면의 grp는 found 모양이라
    // 열이 있어도 inputs.group이 없으면 몰림을 보지 않는다는 것을 함께 본다(M-B와 다른 곳).
    public static JudgmentScene Checks(string accuracy, string missing, string unexplained, string time, string focus)
    {
        const int count = 10;
        var bad = unexplained == NoneResult ? 0 : focus == Few ? 1 : 2;
        Assert.False(bad == 0 && (accuracy == NotMet || missing == NotMet), "불일치가 없으면 정확도·누락률은 늘 met이다(11.10 표).");
        Assert.False(bad == 0 && focus != Few, "불일치가 없으면 몰림은 few다(11.10 표).");

        string Type(int i) => focus switch
        {
            Off or Found => i < bad ? "a" : "b",
            OneValue => "a",
            Spread => i < bad ? (i == 0 ? "a" : "b") : (i % 2 == 0 ? "a" : "b"),
            Few => i % 2 == 0 ? "a" : "b",
            _ => throw new ArgumentOutOfRangeException(nameof(focus), focus, "11.5 FOCUS 결과가 아니다."),
        };

        var rows = Enumerable.Range(0, count)
            .Select(i => new SceneRow(
                Key(i),
                [Type(i)],
                i >= bad ? SceneRow.Match : i == 0 && missing == NotMet ? SceneRow.Missing : SceneRow.Mismatch))
            .ToArray();
        var badKeys = rows.Where(r => r.IsBad).Select(r => r.Key).ToArray();

        return new JudgmentScene(
            Columns: [Group],
            Rows: rows,
            Extras: [],
            AccuracyMin: accuracy == Met ? "0" : "100",
            MissingMax: missing == Met ? "100" : "0",
            TimeMustDrop: time == Off ? "false" : "true",
            Times: time switch { Met => TimesMet, NotMet => TimesNotMet, _ => null },
            Unexplained: unexplained switch { Explained => [], Found => [badKeys[0]], _ => null })
        {
            Group = focus == Off ? null : Group,
            FocusValue = focus == Found ? "a" : null,
        };
    }

    // 11.5 표의 한 행(확인, 결과)을 내는 장면. 나머지 확인은 정확도·누락률 met, 사람 입력 없음, 시간 off, 몰림 spread다.
    public static JudgmentScene CheckScene(string check, string result)
    {
        string accuracy = Met, missing = Met, unexplained = NotReviewed, time = Off, focus = Spread;
        switch (check)
        {
            case JudgmentSpec.Accuracy: accuracy = result; break;
            case JudgmentSpec.Missing: missing = result; break;
            case JudgmentSpec.Unexplained: unexplained = result; break;
            case JudgmentSpec.Time: time = result; break;
            case JudgmentSpec.Focus: focus = result; break;
            default: throw new ArgumentOutOfRangeException(nameof(check), check, "11.5 확인이 아니다.");
        }

        if (unexplained == NoneResult)
        {
            focus = Few;
        }

        return Checks(accuracy, missing, unexplained, time, focus);
    }

    // 문턱이 없는 미검증 케이스(inputs 없음). thresholds 줄은 따로 붙인다.
    public static CaseDefinition NoInputsCase(string thresholds = "") => CompareInput.Case(SyntheticInput.OneStepCase + thresholds);

    // inputs는 있고 compare가 빈 미검증 케이스.
    public static CaseDefinition NoCompareCase(string thresholds = "") => CompareInput.Case(SyntheticInput.NoCompareCase + thresholds);

    // 미검증 케이스는 파일을 읽지 않는다(metrics.md 4.4). 빈 자료로 대조한다.
    public static CompareMetrics NotVerifiedMetrics(CaseDefinition definition) =>
        CompareInput.Measured(CompareEngine.Compare(definition, new CompareSources([], [])));

    public static JudgmentResult JudgeNotVerified(CaseDefinition definition) => JudgmentEngine.Judge(definition, NotVerifiedMetrics(definition));

    // thresholds 블록. null인 키는 쓰지 않는다. 셋 다 null이면 thresholds 키가 없다.
    public static string ThresholdsYaml(string? accuracyMin, string? missingMax, string? timeMustDrop)
    {
        var lines = new[] { (JudgmentSpec.AccuracyMin, accuracyMin), (JudgmentSpec.MissingMax, missingMax), (JudgmentSpec.TimeMustDrop, timeMustDrop) }
            .Where(p => p.Item2 is not null)
            .Select(p => $"  {p.Item1}: {p.Item2}\n")
            .ToArray();
        return lines.Length == 0 ? string.Empty : "thresholds:\n" + string.Concat(lines);
    }

    public static JudgmentCheck CheckOf(JudgmentResult result, string rule) => Assert.Single(result.Checks, c => c.Rule == rule);

    public static string[] Results(JudgmentResult result) => result.Checks.Select(c => c.Result).ToArray();

    public static string[] TodoKeys(JudgmentResult result) => result.Todo.Select(t => t.Key).ToArray();

    public static string Text(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    // 4.5 이름·값 표시(이 테스트가 쓰는 값만): 빈 값은 (빈 칸). 제어 문자·64자 넘는 값은 장면에 넣지 않는다(카드 "PR (b)에 넘기는 것" 1).
    public static string Show(string value) => value.Length == 0 ? JudgmentSpec.EmptyValue : value;

    // 판정 결과를 쓴 문턱(글)과 기본값 키. 장면에 적은 글이 있으면 그 글, 없으면 4.2 표의 기본값.
    public static (Dictionary<string, string> Values, string[] Defaults) Thresholds(string? accuracyMin, string? missingMax, string? timeMustDrop)
    {
        var given = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [JudgmentSpec.AccuracyMin] = accuracyMin,
            [JudgmentSpec.MissingMax] = missingMax,
            [JudgmentSpec.TimeMustDrop] = timeMustDrop,
        };
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var defaults = new List<string>();
        foreach (var (key, value) in JudgmentSpec.Defaults())
        {
            if (given[key] is { } text)
            {
                values[key] = text;
            }
            else
            {
                values[key] = value;
                defaults.Add(key);
            }
        }

        return (values, defaults.ToArray());
    }

    // 4.7 {defaults}: 기본값을 쓴 키와 값을 4.2 순서로 ", "로 잇는다.
    public static string DefaultsText(IReadOnlyDictionary<string, string> values, IEnumerable<string> defaults) =>
        string.Join(", ", defaults.Select(k => $"{k} {values[k]}"));
}

// 장면의 기준 행 하나. Types는 유형 열 값(Columns 순서). Kind는 결과 자료에서 어떻게 되는가.
internal sealed record SceneRow(string Key, IReadOnlyList<string> Types, string Kind)
{
    public const string Match = "match";
    public const string Mismatch = "mismatch";
    public const string Missing = "missing";

    public bool IsBad => Kind != Match;
}

// 메모리 장면 하나. 문턱 글이 null이면 그 키를 case.yaml에 쓰지 않는다(기본값, 4.2).
// Group은 case.yaml inputs.group(null이면 키를 쓰지 않는다, 11.8 규칙 2). 몰림 문장의 {group}이다.
// FocusValue는 found 문장의 {value}다(장면을 만든 쪽이 안다).
// Duplicates는 결과 자료 끝에 한 번 더 넣는 행이다(11.3 중복 행, metrics.md 12.2). 기준 키나 추가 행 키를 쓴다.
// 결과 자료에서 그 키의 첫 행이 앞에 있어야 한다(첫 행으로 대조한다). 누락 행의 키는 쓰지 않는다.
internal sealed record JudgmentScene(
    IReadOnlyList<string> Columns,
    IReadOnlyList<SceneRow> Rows,
    IReadOnlyList<SceneRow> Extras,
    string? AccuracyMin,
    string? MissingMax,
    string? TimeMustDrop,
    JudgmentTimes? Times = null,
    IReadOnlyList<string>? Unexplained = null)
{
    public string? Group { get; init; }

    public string? FocusValue { get; init; }

    public IReadOnlyList<SceneRow> Duplicates { get; init; } = [];

    public string CaseYaml =>
        SyntheticInput.OneStepCase +
        (Group is null
            ? "inputs: {reference: reference/*.csv, candidate: candidate/*.csv, key: id}\n"
            : $"inputs: {{reference: reference/*.csv, candidate: candidate/*.csv, key: id, group: {PlainGroup(Group)}}}\n") +
        "compare:\n  - {field: v, tolerance: 0}\n" + JudgmentInput.ThresholdsYaml(AccuracyMin, MissingMax, TimeMustDrop);

    public CaseDefinition Definition => CompareInput.Case(CaseYaml);

    public string Header => string.Join(",", new[] { "id" }.Concat(Columns.Select(CompareInput.Quote)).Append("v"));

    public string ReferenceCsv => Csv(Rows.Select(r => Line(r, "1")));

    public string CandidateCsv =>
        Csv(Rows.Where(r => r.Kind != SceneRow.Missing).Select(r => Line(r, r.Kind == SceneRow.Mismatch ? "2" : "1"))
            .Concat(Extras.Select(r => Line(r, "1")))
            .Concat(Duplicates.Select(r => Line(r, "1"))));

    public string[] BadKeys => Rows.Where(r => r.IsBad).Select(r => r.Key).ToArray();

    public int Bad => BadKeys.Length;

    // 11.3 검토 항목 수: 불일치 + 추가 행 + 중복 행.
    public int Items => Bad + Extras.Count + Duplicates.Count;

    // 11.3 {breakdown}(표 순서: 불일치, 추가 행, 중복 행).
    public string Breakdown => JudgmentSpec.Breakdown(Bad, Extras.Count, Duplicates.Count);

    public CompareMetrics Metrics() => Metrics(Definition);

    public JudgmentResult Judge()
    {
        var definition = Definition;
        return JudgmentEngine.Judge(definition, Metrics(definition), Times, Unexplained);
    }

    public byte[] Render() => JudgmentJson.Render(Judge());

    // 문장 틀의 값(4.5, 11.6). 대조 결과의 퍼센트·셈, 쓴 문턱, 불일치 수, 검토 항목 수·내역, 사람 입력 수, 시간, 몰림 값, 기본값 목록.
    public Dictionary<string, string> Slots()
    {
        var metrics = Metrics();
        var counts = metrics.Counts!;
        var (values, defaults) = JudgmentInput.Thresholds(AccuracyMin, MissingMax, TimeMustDrop);
        var slots = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["accuracy_pct"] = JudgmentInput.Text(metrics.AccuracyPct!.Value),
            ["missing_rate_pct"] = JudgmentInput.Text(metrics.MissingRatePct!.Value),
            ["match"] = Int(counts.Match),
            ["missing"] = Int(counts.Missing),
            ["reference"] = Int(counts.Reference),
            ["accuracy_min"] = values[JudgmentSpec.AccuracyMin],
            ["missing_max"] = values[JudgmentSpec.MissingMax],
            ["bad"] = Int(counts.Mismatch + counts.Missing),
            ["items"] = Int(Items),
            ["breakdown"] = Breakdown,
            ["defaults"] = JudgmentInput.DefaultsText(values, defaults),
        };

        // 장면이 센 검토 항목 = 대조 항목 상태를 11.3 표로 센 것(장면 도우미가 자료를 바르게 만들었는지).
        Assert.Equal(Bad, counts.Mismatch + counts.Missing);
        Assert.Equal(
            new[] { Bad, Extras.Count, Duplicates.Count },
            JudgmentSpec.ReviewItems().Select(r => metrics.Items.Count(i => r.Statuses.Contains(i.Status))));
        if (Unexplained is not null)
        {
            slots["unexplained"] = Int(Unexplained.Count);
        }

        if (Times is { } t)
        {
            slots["before"] = JudgmentInput.Text(t.BeforeMinutes);
            slots["process"] = JudgmentInput.Text(t.ProcessMinutes);
            slots["review"] = JudgmentInput.Text(t.ReviewMinutes);
            slots["total"] = JudgmentInput.Text(t.ProcessMinutes + t.ReviewMinutes);
        }

        if (Group is not null)
        {
            slots["group"] = JudgmentInput.Show(Group);
        }

        if (Group is not null && FocusValue is not null)
        {
            var index = Columns.ToList().IndexOf(Group);
            Assert.True(index >= 0, $"장면에 유형 열 {Group}이 없다.");
            var rowsIn = Rows.Count(r => r.Types[index] == FocusValue);
            slots["value"] = JudgmentInput.Show(FocusValue);
            slots["rows_in"] = Int(rowsIn);
            slots["rows_out"] = Int(Rows.Count - rowsIn);
        }

        return slots;
    }

    // 쓴 문턱 가운데 기본값인 키(4.2 순서).
    public string[] DefaultKeys => JudgmentInput.Thresholds(AccuracyMin, MissingMax, TimeMustDrop).Defaults;

    private CompareMetrics Metrics(CaseDefinition definition) =>
        CompareInput.Measured(CompareInput.Run(definition, ReferenceCsv, CandidateCsv));

    private string Line(SceneRow row, string v)
    {
        Assert.Equal(Columns.Count, row.Types.Count);
        return string.Join(",", new[] { row.Key }.Concat(row.Types.Select(CompareInput.Quote)).Append(v));
    }

    private string Csv(IEnumerable<string> lines) => Header + "\n" + string.Concat(lines.Select(l => l + "\n"));

    // inputs.group 값은 YAML 따옴표 없이 쓴다. 영문·숫자·밑줄만 받는다(장면 열 이름이 그렇다).
    private static string PlainGroup(string group)
    {
        Assert.True(group.Length > 0 && group.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_'), $"장면 inputs.group '{group}'은 영문·숫자·밑줄이어야 한다.");
        return group;
    }

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);
}
