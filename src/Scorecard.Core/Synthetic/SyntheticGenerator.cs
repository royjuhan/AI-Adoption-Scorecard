using System.Globalization;
using Scorecard.Core.Cases;

namespace Scorecard.Core.Synthetic;

// 합성 reference·candidate 생성기. docs/spec/synthetic-examples.md 4.1~4.6, 5절.
// 순수 함수다. 파일·시각·환경 변수·System.Random을 쓰지 않는다(G1 D1). 같은 (case, 계획)이면 같은 결과다.
// case.yaml에서는 inputs.key와 compare만 쓴다(2절). 판정에는 쓰지 않는다. 합성 자료 만들기 전용이다.
// 수열은 SplitMix64 하나(4.2). 뽑는 순서는 4.5의 1~7로 고정한다. 순서나 아래 상수를 바꾸면 바이트가 바뀌고 골든 테스트가 잡는다.
// 비교는 서수, 숫자는 InvariantCulture다.
public static class SyntheticGenerator
{
    // 3.1 Planted 값.
    public const string PlantedNone = "none";
    public const string PlantedMissing = "missing";
    public const string PlantedExtra = "extra";
    public const string PlantedMismatchPrefix = "mismatch:";
    public const string PlantedWithinPrefix = "within:";

    // 4.5 뽑는 순서의 상수. 이 파일의 이 자리에만 둔다. 분류·판정 숫자(지시문 13절)가 아니다.
    // 정수 값 불일치 크기: 허용 오차 + 1 ~ 허용 오차 + 20.
    private const int IntegerMismatchSpread = 20;

    // 소수 값 불일치 배수: 허용 오차(%)의 2·3·4배.
    private const int DecimalMismatchFactorMin = 2;
    private const int DecimalMismatchFactorCount = 3;

    // 허용 오차 안 차이: 허용 오차(%)의 0.1·0.2·0.3·0.4배(1/10 단위 1~4).
    private const int WithinTenthsMin = 1;
    private const int WithinTenthsCount = 4;

    // 소수 열 자릿수 상한. 자릿수 단위 정수(최대 − 최소)가 int 안에 들게 한다(NextBelow).
    private const int DecimalScaleMax = 6;

    // 계획·케이스가 틀렸을 때 ArgumentException의 매개변수 이름.
    private const string PlanParameter = "plan";

    public static SyntheticTables Generate(CaseDefinition definition, SyntheticPlan plan)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(plan);
        var inputs = definition.Inputs
            ?? throw new ArgumentException("case.yaml에 inputs가 없다. 자료가 없는 케이스는 생성하지 않는다.", nameof(definition));
        var compare = definition.Compare;
        Validate(inputs.Key, compare, plan);

        var rng = new SplitMix64(plan.Seed);
        var columns = plan.Columns;

        // 1. 기준 행. 행 1..Rows, 열 순서대로 한 칸씩.
        var reference = new string[plan.Rows][];
        for (var i = 0; i < plan.Rows; i++)
        {
            reference[i] = Draw(columns, rng);
        }

        // 2. 누락·값 불일치 행. 몰림 조건에 맞는 기준 행(오름차순)에서 앞쪽 부분 섞기로 고른다.
        //    앞 Missing개가 누락, 그다음이 MismatchColumns 순서의 값 불일치 행이다.
        var focus = plan.FocusColumn is null ? -1 : IndexOf(columns, plan.FocusColumn);
        var pool = Enumerable.Range(0, plan.Rows)
            .Where(i => focus < 0 || string.Equals(reference[i][focus], plan.FocusValue, StringComparison.Ordinal))
            .ToList();
        var defects = plan.Missing + plan.MismatchColumns.Count;
        if (pool.Count < defects)
        {
            var where = focus < 0 ? "기준 행" : $"몰림 {plan.FocusColumn} = {plan.FocusValue}인 기준 행";
            throw Error($"{where}이 {pool.Count}개뿐이라 누락·값 불일치 {defects}행을 고를 수 없다. 시드나 계획을 바꾼다.");
        }

        PartialShuffle(pool, defects, rng);
        var isMissing = new bool[plan.Rows];
        var isDefect = new bool[plan.Rows];
        for (var j = 0; j < defects; j++)
        {
            isDefect[pool[j]] = true;
            isMissing[pool[j]] = j < plan.Missing;
        }

        // 3. 허용 오차 안 차이 행. 2에서 고르지 않은 기준 행(오름차순)에서 같은 부분 섞기로 고른다.
        //    심을 행 수 합이 기준 건수 이하라 후보는 모자라지 않는다(Validate).
        var rest = Enumerable.Range(0, plan.Rows).Where(i => !isDefect[i]).ToList();
        PartialShuffle(rest, plan.Within, rng);

        // 4. 값 불일치 값. MismatchColumns 순서대로.
        var candidate = reference.Select(r => (string[])r.Clone()).ToArray();
        var planted = Enumerable.Repeat(PlantedNone, plan.Rows).ToArray();
        for (var j = 0; j < plan.MismatchColumns.Count; j++)
        {
            var name = plan.MismatchColumns[j];
            var c = IndexOf(columns, name);
            var row = pool[plan.Missing + j];
            candidate[row][c] = Mismatch(columns[c], Field(compare, name), reference[row][c], rng);
            planted[row] = PlantedMismatchPrefix + name;
        }

        // 5. 허용 오차 안 값. 3에서 고른 순서대로.
        if (plan.WithinColumn is { } withinColumn)
        {
            var c = IndexOf(columns, withinColumn);
            var field = Field(compare, withinColumn);
            for (var j = 0; j < plan.Within; j++)
            {
                var row = rest[j];
                candidate[row][c] = Within(columns[c], field, reference[row][c], rng);
                planted[row] = PlantedWithinPrefix + withinColumn;
            }
        }

        // 6. 결과에만 있는 행. 키는 Rows+1부터, 값은 1과 같은 방식으로 뽑는다.
        var extra = new SyntheticRow[plan.Extra];
        for (var e = 0; e < plan.Extra; e++)
        {
            extra[e] = new SyntheticRow(Key(plan, plan.Rows + e), Draw(columns, rng), PlantedExtra);
        }

        // 7. 결과 행. 기준 순서에서 누락을 빼고 추가 행을 뒤에 붙인다. 섞기는 Fisher–Yates(끝에서부터).
        var candidateRows = new List<SyntheticRow>(plan.Rows - plan.Missing + plan.Extra);
        for (var i = 0; i < plan.Rows; i++)
        {
            if (!isMissing[i])
            {
                candidateRows.Add(new SyntheticRow(Key(plan, i), candidate[i], planted[i]));
            }
        }

        candidateRows.AddRange(extra);
        if (plan.ShuffleCandidate)
        {
            for (var i = candidateRows.Count - 1; i > 0; i--)
            {
                var k = rng.NextBelow(i + 1);
                (candidateRows[i], candidateRows[k]) = (candidateRows[k], candidateRows[i]);
            }
        }

        var referenceRows = Enumerable.Range(0, plan.Rows)
            .Select(i => new SyntheticRow(Key(plan, i), reference[i], isMissing[i] ? PlantedMissing : PlantedNone))
            .ToArray();
        string[] header = [inputs.Key, .. columns.Select(c => c.Name)];
        return new SyntheticTables(plan.FileName, header, referenceRows, candidateRows.ToArray());
    }

    // 앞쪽 부분 섞기(4.5의 2·3). j = 0..count−1마다 j + NextBelow(후보 수 − j) 자리와 바꾼다. 앞 count개가 고른 것이다.
    private static void PartialShuffle(List<int> items, int count, SplitMix64 rng)
    {
        for (var j = 0; j < count; j++)
        {
            var k = j + rng.NextBelow(items.Count - j);
            (items[j], items[k]) = (items[k], items[j]);
        }
    }

    private static string Key(SyntheticPlan plan, int index) =>
        plan.KeyPrefix + (index + 1).ToString("D" + Text(plan.KeyDigits), CultureInfo.InvariantCulture);

    // 4.5의 1. 정수: 최소 + NextBelow(최대 − 최소 + 1). 소수: 자릿수 단위 정수로 뽑고 나눈다. 선택: 선택지[NextBelow(개수)].
    private static string[] Draw(IReadOnlyList<SyntheticColumn> columns, SplitMix64 rng)
    {
        var values = new string[columns.Count];
        for (var c = 0; c < columns.Count; c++)
        {
            var col = columns[c];
            values[c] = col.Kind switch
            {
                SyntheticColumn.KindInteger => Text((long)col.Min + rng.NextBelow((int)(col.Max - col.Min) + 1)),
                SyntheticColumn.KindDecimal => Text(DrawDecimal(col, rng), col.Scale),
                SyntheticColumn.KindChoice => col.Choices[rng.NextBelow(col.Choices.Count)],
                _ => throw new ArgumentOutOfRangeException(nameof(columns), col.Kind, "4.4에 없는 열 종류"),
            };
        }

        return values;
    }

    private static decimal DrawDecimal(SyntheticColumn col, SplitMix64 rng)
    {
        var unit = Pow10(col.Scale);
        var low = (long)(col.Min * unit);
        var high = (long)(col.Max * unit);
        return (low + rng.NextBelow((int)(high - low) + 1)) / unit;
    }

    // 4.5의 4. 허용 오차 밖으로 바꾼다. 열 종류는 compare 항목과 맞춰 두었다(Validate, 4.6).
    private static string Mismatch(SyntheticColumn col, CompareField field, string value, SplitMix64 rng) =>
        col.Kind switch
        {
            SyntheticColumn.KindInteger => MismatchInteger(col, field.Tolerance!.Value, value, rng),
            SyntheticColumn.KindDecimal => MismatchDecimal(col, field.TolerancePct!.Value, value, rng),
            _ => MismatchChoice(col, value, rng),
        };

    // 정수: 부호 NextBelow(2), 크기 t + 1 + NextBelow(20). 범위를 벗어나면 부호를 뒤집는다.
    private static string MismatchInteger(SyntheticColumn col, decimal tolerance, string value, SplitMix64 rng)
    {
        var r = long.Parse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var sign = rng.NextBelow(2) == 0 ? 1 : -1;
        var delta = (long)tolerance + 1 + rng.NextBelow(IntegerMismatchSpread);
        var c = r + (sign * delta);
        if (c < col.Min || c > col.Max)
        {
            c = r - (sign * delta);
        }

        if (c < col.Min || c > col.Max)
        {
            throw Error($"정수 열 {col.Name}: 범위 {col.Min} ~ {col.Max} 안에서 불일치 값을 만들 수 없다(기준 {value}, 차이 {delta}).");
        }

        return Text(c);
    }

    // 소수: 부호, 배수 2 + NextBelow(3). 기준 × (1 ± p × 배수 / 100)을 자릿수로 반올림한다(0에서 먼 쪽).
    private static string MismatchDecimal(SyntheticColumn col, decimal pct, string value, SplitMix64 rng)
    {
        var r = ParseDecimal(value);
        var sign = rng.NextBelow(2) == 0 ? 1 : -1;
        var factor = DecimalMismatchFactorMin + rng.NextBelow(DecimalMismatchFactorCount);
        var c = Math.Round(r * (1 + (sign * pct * factor / 100m)), col.Scale, MidpointRounding.AwayFromZero);
        if (Math.Abs(c - r) * 100m <= pct * Math.Abs(r))
        {
            throw Error($"소수 열 {col.Name}: 불일치 값이 허용 오차 {pct}% 안에 든다({value} → {Text(c, col.Scale)}).");
        }

        return Text(c, col.Scale);
    }

    // 선택: (지금 위치 + 1 + NextBelow(개수 − 1)) % 개수. 자기 자신은 나오지 않는다.
    private static string MismatchChoice(SyntheticColumn col, string value, SplitMix64 rng)
    {
        var i = IndexOf(col.Choices, value);
        var n = col.Choices.Count;
        return col.Choices[(i + 1 + rng.NextBelow(n - 1)) % n];
    }

    // 4.5의 5. 부호, n = 1 + NextBelow(4). 기준 × (1 ± p × n / 1000)을 반올림한다.
    // 기준과 같거나 차이가 p/2를 넘으면 예외다. 경계값(허용 오차와 꼭 같은 차이)은 심지 않는다.
    private static string Within(SyntheticColumn col, CompareField field, string value, SplitMix64 rng)
    {
        var r = ParseDecimal(value);
        var pct = field.TolerancePct!.Value;
        var sign = rng.NextBelow(2) == 0 ? 1 : -1;
        var tenths = WithinTenthsMin + rng.NextBelow(WithinTenthsCount);
        var c = Math.Round(r * (1 + (sign * pct * tenths / 1000m)), col.Scale, MidpointRounding.AwayFromZero);
        if (c == r || Math.Abs(c - r) * 200m > pct * Math.Abs(r))
        {
            throw Error($"소수 열 {col.Name}: 허용 오차 {pct}%의 절반 안에서 다른 값을 만들 수 없다({value} → {Text(c, col.Scale)}).");
        }

        return Text(c, col.Scale);
    }

    // 4.3 칸 읽기, 4.6 case.yaml과 계획 맞추기, 5절 예외 표. 메시지는 어느 필드·값인지 적는다.
    private static void Validate(string key, IReadOnlyList<CompareField> compare, SyntheticPlan plan)
    {
        AtLeast(plan.Rows, 1, "기준 건수");
        AtLeast(plan.KeyDigits, 1, "키 자릿수");
        AtLeast(plan.Missing, 0, "누락");
        AtLeast(plan.Extra, 0, "추가");
        AtLeast(plan.Within, 0, "허용오차 안 차이");

        var lastKey = plan.Rows + plan.Extra;
        if (Text(lastKey).Length > plan.KeyDigits)
        {
            throw Error($"키 자릿수가 모자란다: {plan.KeyDigits}자리. 기준+추가 {lastKey}건에는 {Text(lastKey).Length}자리가 필요하다.");
        }

        if (plan.Missing + plan.MismatchColumns.Count + plan.Within > plan.Rows)
        {
            throw Error($"심을 행이 기준 건수 {plan.Rows}보다 많다: 누락 {plan.Missing}, 값 불일치 {plan.MismatchColumns.Count}, 허용오차 안 차이 {plan.Within}.");
        }

        SyntheticCsv.CheckCell(plan.FileName);
        if (plan.FileName.IndexOfAny(['/', '\\', ':']) >= 0 || !plan.FileName.EndsWith(".csv", StringComparison.Ordinal))
        {
            throw Error($"파일 이름은 폴더·':' 없이 '.csv'로 끝나야 한다: {plan.FileName}");
        }

        SyntheticCsv.CheckCell(plan.KeyPrefix);
        SyntheticCsv.CheckCell(key);

        // 열 이름은 키 열(inputs.key)과 계획 열이 모두 달라야 한다.
        var names = new List<string> { key };
        foreach (var col in plan.Columns)
        {
            SyntheticCsv.CheckCell(col.Name);
            if (names.Contains(col.Name, StringComparer.Ordinal))
            {
                var which = string.Equals(col.Name, key, StringComparison.Ordinal) ? " (키 열 inputs.key와 같다)" : "";
                throw Error($"같은 열 이름이 두 번 나온다: {col.Name}{which}");
            }

            names.Add(col.Name);
            CheckColumn(col);
        }

        // 4.6 표: compare 필드는 모두 계획 열에 있고, 허용 오차와 열 종류가 맞아야 한다.
        for (var i = 0; i < compare.Count; i++)
        {
            var field = compare[i];
            if (IndexOf(compare, field.Field) != i)
            {
                throw Error($"compare 필드가 두 번 나온다: {field.Field}");
            }

            var c = IndexOf(plan.Columns, field.Field);
            if (c < 0)
            {
                throw Error($"compare 필드가 계획 열에 없다: {field.Field}");
            }

            var (kind, rule) = CompareKind(field);
            if (!string.Equals(plan.Columns[c].Kind, kind, StringComparison.Ordinal))
            {
                throw Error($"compare 필드 {field.Field}: {rule} {kind} 열이어야 한다. 계획 열은 {plan.Columns[c].Kind}다.");
            }

            if (field.Tolerance is { } t && t != decimal.Truncate(t))
            {
                throw Error($"compare 필드 {field.Field}: 정수 열의 tolerance는 정수 값이어야 한다({t}).");
            }
        }

        foreach (var name in plan.MismatchColumns)
        {
            if (IndexOf(compare, name) < 0)
            {
                throw Error($"값 불일치 열은 compare 필드여야 한다: {name}");
            }
        }

        if (plan.WithinColumn is { } within)
        {
            var w = IndexOf(compare, within);
            if (w < 0 || compare[w].TolerancePct is null)
            {
                throw Error($"허용오차 안 차이 열은 tolerance_pct가 있는 compare 필드여야 한다: {within}");
            }
        }
        else if (plan.Within > 0)
        {
            throw Error($"허용오차 안 차이 {plan.Within}행을 심을 열이 없다. tolerance_pct가 있는 compare 필드를 적는다.");
        }

        if (plan.FocusColumn is { } focus)
        {
            if (IndexOf(plan.Columns, focus) < 0)
            {
                throw Error($"몰림 열이 계획 열에 없다: {focus}");
            }

            if (plan.FocusValue is null)
            {
                throw Error($"몰림 열 {focus}에 값이 없다.");
            }
        }
        else if (plan.FocusValue is not null)
        {
            throw Error($"몰림 값만 있고 열이 없다: {plan.FocusValue}");
        }
    }

    // 4.6 표. tolerance(정수 값) → 정수, tolerance_pct → 소수, 둘 다 없음 → 선택.
    private static (string Kind, string Rule) CompareKind(CompareField field) => (field.Tolerance, field.TolerancePct) switch
    {
        (not null, null) => (SyntheticColumn.KindInteger, "tolerance가 있으면"),
        (null, not null) => (SyntheticColumn.KindDecimal, "tolerance_pct가 있으면"),
        (null, null) => (SyntheticColumn.KindChoice, "허용 오차가 없으면"),
        _ => throw Error($"compare 필드 {field.Field}: tolerance와 tolerance_pct 중 하나만 쓴다."),
    };

    // 4.4 열 종류. 뽑기 폭(최대 − 최소 + 1, 소수는 자릿수 단위)은 int 안이어야 한다(NextBelow).
    private static void CheckColumn(SyntheticColumn col)
    {
        switch (col.Kind)
        {
            case SyntheticColumn.KindInteger:
                if (!IsWhole(col.Min) || !IsWhole(col.Max) || col.Min > col.Max || col.Max - col.Min >= int.MaxValue)
                {
                    throw Error($"정수 열 {col.Name}: 범위가 맞지 않는다({col.Min} ~ {col.Max}).");
                }

                return;
            case SyntheticColumn.KindDecimal:
                if (col.Scale is < 0 or > DecimalScaleMax)
                {
                    throw Error($"소수 열 {col.Name}: 자릿수는 0 ~ {DecimalScaleMax}이다({col.Scale}).");
                }

                var unit = Pow10(col.Scale);
                if (!IsWhole(col.Min * unit) || !IsWhole(col.Max * unit) || col.Min > col.Max || (col.Max - col.Min) * unit >= int.MaxValue)
                {
                    throw Error($"소수 열 {col.Name}: 범위가 자릿수 {col.Scale}과 맞지 않는다({col.Min} ~ {col.Max}).");
                }

                return;
            case SyntheticColumn.KindChoice:
                if (col.Choices.Count < 2)
                {
                    throw Error($"선택 열 {col.Name}: 선택지가 2개 이상이어야 한다.");
                }

                for (var i = 0; i < col.Choices.Count; i++)
                {
                    SyntheticCsv.CheckCell(col.Choices[i]);
                    if (IndexOf(col.Choices, col.Choices[i]) != i)
                    {
                        throw Error($"선택 열 {col.Name}: 선택지가 두 번 나온다: {col.Choices[i]}");
                    }
                }

                return;
            default:
                throw Error($"열 {col.Name}: 4.4에 없는 열 종류다: {col.Kind}");
        }
    }

    private static void AtLeast(int value, int min, string label)
    {
        if (value < min)
        {
            throw Error($"{label}: {min} 이상이어야 한다({value}).");
        }
    }

    private static CompareField Field(IReadOnlyList<CompareField> compare, string name) => compare[IndexOf(compare, name)];

    private static int IndexOf(IReadOnlyList<CompareField> compare, string name)
    {
        for (var i = 0; i < compare.Count; i++)
        {
            if (string.Equals(compare[i].Field, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOf(IReadOnlyList<SyntheticColumn> columns, string name)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            if (string.Equals(columns[i].Name, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOf(IReadOnlyList<string> values, string value)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsWhole(decimal value) => value == decimal.Truncate(value);

    private static decimal Pow10(int scale)
    {
        var unit = 1m;
        for (var i = 0; i < scale; i++)
        {
            unit *= 10m;
        }

        return unit;
    }

    private static decimal ParseDecimal(string value) =>
        decimal.Parse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    private static string Text(long value) => value.ToString(CultureInfo.InvariantCulture);

    // 소수 고정 자릿수(3.3, 예: F2).
    private static string Text(decimal value, int scale) =>
        value.ToString("F" + Text(scale), CultureInfo.InvariantCulture);

    // 메시지의 숫자도 InvariantCulture로 쓴다.
    private static ArgumentException Error(FormattableString message) =>
        new(FormattableString.Invariant(message), PlanParameter);
}
