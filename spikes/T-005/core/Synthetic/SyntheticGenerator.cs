using System.Globalization;
using Scorecard.Core.Cases;

namespace Scorecard.Core.Synthetic;

// 합성 reference·candidate 생성기(T-005 스파이크 후보).
// 순수 함수다. 파일·시각·환경 변수·System.Random을 쓰지 않는다. 같은 (case, 계획)이면 같은 결과다.
// 수열은 SplitMix64 하나. 뽑는 순서는 아래 1~7번으로 고정한다. 순서를 바꾸면 바이트가 바뀐다.
public static class SyntheticGenerator
{
    public const string PlantedNone = "none";
    public const string PlantedMissing = "missing";
    public const string PlantedExtra = "extra";
    public const string PlantedMismatchPrefix = "mismatch:";
    public const string PlantedWithinPrefix = "within:";

    // 정수 값 불일치: 허용 오차 + 1 ~ 허용 오차 + 20 만큼 다르게 한다.
    private const int IntegerMismatchSpread = 20;

    // 소수 값 불일치: 허용 오차(%)의 2·3·4배.
    private const int DecimalMismatchFactorMin = 2;
    private const int DecimalMismatchFactorCount = 3;

    // 허용 오차 안 차이: 허용 오차(%)의 0.1·0.2·0.3·0.4배.
    private const int WithinTenthsMin = 1;
    private const int WithinTenthsCount = 4;

    public static SyntheticTables Generate(CaseDefinition definition, SyntheticPlan plan)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(plan);
        var inputs = definition.Inputs ?? throw new ArgumentException("case.yaml에 inputs가 없다.", nameof(definition));
        var compare = definition.Compare.ToDictionary(c => c.Field, StringComparer.Ordinal);
        Validate(inputs, compare, plan);

        var rng = new SplitMix64(plan.Seed);
        var columns = plan.Columns;

        // 1. 기준 행. 행 1..Rows, 열 순서대로 하나씩 뽑는다.
        var reference = new List<string[]>(plan.Rows);
        for (var i = 0; i < plan.Rows; i++)
        {
            reference.Add(Draw(columns, rng));
        }

        // 2. 누락·불일치 행. 몰림 조건에 맞는 기준 행(오름차순)에서 앞쪽 부분 섞기로 고른다.
        //    앞 Missing개가 누락, 그 뒤가 MismatchColumns 순서의 불일치 행이다.
        var focus = plan.FocusColumn is null ? -1 : IndexOf(columns, plan.FocusColumn);
        var pool = Enumerable.Range(0, plan.Rows)
            .Where(i => focus < 0 || string.Equals(reference[i][focus], plan.FocusValue, StringComparison.Ordinal))
            .ToList();
        var defects = plan.Missing + plan.MismatchColumns.Count;
        if (pool.Count < defects)
        {
            throw new ArgumentException($"누락·불일치 {defects}행을 고를 기준 행이 {pool.Count}개뿐이다.", nameof(plan));
        }

        PartialShuffle(pool, defects, rng);
        var missing = pool.Take(plan.Missing).ToHashSet();
        var mismatchRows = pool.Skip(plan.Missing).Take(plan.MismatchColumns.Count).ToArray();

        // 3. 허용 오차 안 차이 행. 2에서 고르지 않은 기준 행(오름차순)에서 고른다.
        var chosen = pool.Take(defects).ToHashSet();
        var rest = Enumerable.Range(0, plan.Rows).Where(i => !chosen.Contains(i)).ToList();
        if (rest.Count < plan.Within)
        {
            throw new ArgumentException($"허용 오차 안 차이 {plan.Within}행을 고를 기준 행이 {rest.Count}개뿐이다.", nameof(plan));
        }

        PartialShuffle(rest, plan.Within, rng);
        var withinRows = rest.Take(plan.Within).ToArray();

        // 4. 불일치 값. MismatchColumns 순서대로.
        var candidate = reference.Select(r => (string[])r.Clone()).ToList();
        var planted = Enumerable.Repeat(PlantedNone, plan.Rows).ToArray();
        for (var j = 0; j < mismatchRows.Length; j++)
        {
            var name = plan.MismatchColumns[j];
            var c = IndexOf(columns, name);
            var row = mismatchRows[j];
            candidate[row][c] = Mismatch(columns[c], compare[name], reference[row][c], rng);
            planted[row] = PlantedMismatchPrefix + name;
        }

        // 5. 허용 오차 안 값. 3에서 고른 순서대로.
        if (plan.WithinColumn is not null)
        {
            var c = IndexOf(columns, plan.WithinColumn);
            foreach (var row in withinRows)
            {
                candidate[row][c] = Within(columns[c], compare[plan.WithinColumn], reference[row][c], rng);
                planted[row] = PlantedWithinPrefix + plan.WithinColumn;
            }
        }

        // 6. 결과에만 있는 행. 키는 Rows+1부터, 값은 1과 같은 방식으로 뽑는다.
        var extra = new List<SyntheticRow>(plan.Extra);
        for (var e = 0; e < plan.Extra; e++)
        {
            extra.Add(new SyntheticRow(Key(plan, plan.Rows + e), Draw(columns, rng), PlantedExtra));
        }

        // 7. 결과 행: 기준 순서에서 누락을 빼고, 추가 행을 뒤에 붙인 뒤, 섞기(Fisher–Yates, 끝에서부터).
        var candidateRows = new List<SyntheticRow>(plan.Rows - plan.Missing + plan.Extra);
        for (var i = 0; i < plan.Rows; i++)
        {
            if (!missing.Contains(i))
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
            .Select(i => new SyntheticRow(Key(plan, i), reference[i], missing.Contains(i) ? PlantedMissing : PlantedNone))
            .ToArray();
        string[] header = [inputs.Key, .. columns.Select(c => c.Name)];
        return new SyntheticTables(plan.FileName, header, referenceRows, candidateRows);
    }

    // 앞쪽 부분 섞기: j = 0..count-1마다 j 이상 끝 미만에서 하나를 골라 j와 바꾼다. 앞 count개가 고른 것이다.
    private static void PartialShuffle(List<int> items, int count, SplitMix64 rng)
    {
        for (var j = 0; j < count; j++)
        {
            var k = j + rng.NextBelow(items.Count - j);
            (items[j], items[k]) = (items[k], items[j]);
        }
    }

    private static string Key(SyntheticPlan plan, int index) =>
        plan.KeyPrefix + (index + 1).ToString("D" + plan.KeyDigits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    private static string[] Draw(IReadOnlyList<SyntheticColumn> columns, SplitMix64 rng)
    {
        var values = new string[columns.Count];
        for (var c = 0; c < columns.Count; c++)
        {
            var col = columns[c];
            values[c] = col.Kind switch
            {
                SyntheticColumn.KindInteger => FormatInteger((long)col.Min + rng.NextBelow(checked((int)(col.Max - col.Min) + 1))),
                SyntheticColumn.KindDecimal => FormatDecimal(DrawDecimal(col, rng), col.Scale),
                SyntheticColumn.KindChoice => col.Choices[rng.NextBelow(col.Choices.Count)],
                _ => throw new ArgumentOutOfRangeException(nameof(columns), col.Kind, "알 수 없는 열 종류"),
            };
        }

        return values;
    }

    // Min~Max를 소수 Scale자리 단위 정수로 바꿔 고른다.
    private static decimal DrawDecimal(SyntheticColumn col, SplitMix64 rng)
    {
        var unit = Pow10(col.Scale);
        var low = (long)(col.Min * unit);
        var high = (long)(col.Max * unit);
        return (low + rng.NextBelow(checked((int)(high - low) + 1))) / unit;
    }

    // 허용 오차 밖으로 바꾼다. 뽑는 순서: 부호, 크기(선택은 다른 선택지 하나).
    private static string Mismatch(SyntheticColumn col, CompareField field, string value, SplitMix64 rng) =>
        col.Kind switch
        {
            SyntheticColumn.KindInteger => MismatchInteger(col, field, value, rng),
            SyntheticColumn.KindDecimal => MismatchDecimal(col, field, value, rng),
            _ => MismatchChoice(col, value, rng),
        };

    // 허용 오차 + 1 ~ + 20 만큼 더하거나 뺀다. 범위를 벗어나면 부호를 바꾼다.
    private static string MismatchInteger(SyntheticColumn col, CompareField field, string value, SplitMix64 rng)
    {
        var r = long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        var sign = rng.NextBelow(2) == 0 ? 1 : -1;
        var delta = (long)field.Tolerance!.Value + 1 + rng.NextBelow(IntegerMismatchSpread);
        var c = r + (sign * delta);
        if (c < col.Min || c > col.Max)
        {
            c = r - (sign * delta);
        }

        if (c < col.Min || c > col.Max)
        {
            throw new ArgumentException($"{col.Name}: 범위 안에서 불일치 값을 만들 수 없다.");
        }

        return FormatInteger(c);
    }

    // 기준값 × (1 ± 허용 오차(%) × 2~4 / 100)을 소수 Scale자리로 반올림(0에서 먼 쪽)한다.
    private static string MismatchDecimal(SyntheticColumn col, CompareField field, string value, SplitMix64 rng)
    {
        var r = ParseDecimal(value);
        var p = field.TolerancePct!.Value;
        var sign = rng.NextBelow(2) == 0 ? 1 : -1;
        var factor = DecimalMismatchFactorMin + rng.NextBelow(DecimalMismatchFactorCount);
        var c = Math.Round(r * (1 + (sign * p * factor / 100m)), col.Scale, MidpointRounding.AwayFromZero);
        if (Math.Abs(c - r) * 100m <= p * Math.Abs(r))
        {
            throw new ArgumentException($"{col.Name}: 불일치 값이 허용 오차 안에 든다 ({value} → {c}).");
        }

        return FormatDecimal(c, col.Scale);
    }

    // 지금 선택지 다음부터 n-1개 중 하나(자기 자신은 나오지 않는다).
    private static string MismatchChoice(SyntheticColumn col, string value, SplitMix64 rng)
    {
        var i = IndexOf(col.Choices, value);
        var n = col.Choices.Count;
        return col.Choices[(i + 1 + rng.NextBelow(n - 1)) % n];
    }

    // 허용 오차(%)의 절반 이하로만 바꾼다. 0이 아닌 차이. 뽑는 순서: 부호, 크기.
    private static string Within(SyntheticColumn col, CompareField field, string value, SplitMix64 rng)
    {
        var r = ParseDecimal(value);
        var p = field.TolerancePct!.Value;
        var sign = rng.NextBelow(2) == 0 ? 1 : -1;
        var tenths = WithinTenthsMin + rng.NextBelow(WithinTenthsCount);
        var c = Math.Round(r * (1 + (sign * p * tenths / 1000m)), col.Scale, MidpointRounding.AwayFromZero);
        if (c == r || Math.Abs(c - r) * 200m > p * Math.Abs(r))
        {
            throw new ArgumentException($"{col.Name}: 허용 오차 안 차이를 만들 수 없다 ({value} → {c}).");
        }

        return FormatDecimal(c, col.Scale);
    }

    private static void Validate(CaseInputs inputs, Dictionary<string, CompareField> compare, SyntheticPlan plan)
    {
        if (plan.Rows < 1 || plan.Missing < 0 || plan.Extra < 0 || plan.Within < 0 || plan.KeyDigits < 1)
        {
            throw new ArgumentException("건수는 0 이상, 기준 건수와 키 자릿수는 1 이상이다.", nameof(plan));
        }

        if ((plan.Rows + plan.Extra).ToString(CultureInfo.InvariantCulture).Length > plan.KeyDigits)
        {
            throw new ArgumentException("키 자릿수가 모자란다.", nameof(plan));
        }

        if (plan.Missing + plan.MismatchColumns.Count + plan.Within > plan.Rows)
        {
            throw new ArgumentException("심을 행이 기준 건수보다 많다.", nameof(plan));
        }

        SyntheticCsv.CheckCell(plan.KeyPrefix);
        SyntheticCsv.CheckCell(plan.FileName);
        if (plan.FileName.IndexOfAny(['/', '\\', ':']) >= 0 || !plan.FileName.EndsWith(".csv", StringComparison.Ordinal))
        {
            throw new ArgumentException("파일 이름은 폴더 없이 '.csv'로 끝난다.", nameof(plan));
        }
        var names = plan.Columns.Select(c => c.Name).Prepend(inputs.Key).ToArray();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length)
        {
            throw new ArgumentException("열 이름이 겹친다.", nameof(plan));
        }

        foreach (var col in plan.Columns)
        {
            SyntheticCsv.CheckCell(col.Name);
            CheckColumn(col);
        }

        foreach (var field in compare.Values)
        {
            var col = plan.Columns.SingleOrDefault(c => c.Name == field.Field)
                ?? throw new ArgumentException($"compare 필드 {field.Field}가 계획의 열에 없다.", nameof(plan));
            var ok = (field.Tolerance, field.TolerancePct) switch
            {
                ({ } t, null) => col.Kind == SyntheticColumn.KindInteger && t == decimal.Truncate(t),
                (null, not null) => col.Kind == SyntheticColumn.KindDecimal,
                _ => col.Kind == SyntheticColumn.KindChoice,
            };
            if (!ok)
            {
                throw new ArgumentException($"compare 필드 {field.Field}의 허용 오차와 열 종류({col.Kind})가 맞지 않는다.", nameof(plan));
            }
        }

        foreach (var name in plan.MismatchColumns)
        {
            if (!compare.ContainsKey(name))
            {
                throw new ArgumentException($"불일치 열 {name}은 compare 필드여야 한다.", nameof(plan));
            }
        }

        if (plan.Within > 0 && (plan.WithinColumn is null || !compare.TryGetValue(plan.WithinColumn, out var w) || w.TolerancePct is null))
        {
            throw new ArgumentException("허용 오차 안 차이 열은 tolerance_pct가 있는 compare 필드여야 한다.", nameof(plan));
        }

        if (plan.FocusColumn is not null && (plan.FocusValue is null || IndexOf(plan.Columns, plan.FocusColumn) < 0))
        {
            throw new ArgumentException("몰림 열과 값이 맞지 않는다.", nameof(plan));
        }
    }

    private static void CheckColumn(SyntheticColumn col)
    {
        switch (col.Kind)
        {
            case SyntheticColumn.KindInteger when col.Min == decimal.Truncate(col.Min) && col.Max == decimal.Truncate(col.Max) && col.Min <= col.Max:
            case SyntheticColumn.KindDecimal when col.Scale is >= 0 and <= 6 && col.Min <= col.Max:
                return;
            case SyntheticColumn.KindChoice when col.Choices.Count >= 2:
                foreach (var choice in col.Choices)
                {
                    SyntheticCsv.CheckCell(choice);
                }

                if (col.Choices.Distinct(StringComparer.Ordinal).Count() != col.Choices.Count)
                {
                    throw new ArgumentException($"{col.Name}: 선택지가 겹친다.");
                }

                return;
            default:
                throw new ArgumentException($"{col.Name}: 열 정의가 맞지 않는다({col.Kind}).");
        }
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

    private static decimal Pow10(int scale)
    {
        var unit = 1m;
        for (var i = 0; i < scale; i++)
        {
            unit *= 10m;
        }

        return unit;
    }

    private static string FormatInteger(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string FormatDecimal(decimal value, int scale) =>
        value.ToString("F" + scale.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    // SPIKE-CULTURE-MARKER: S6 음성 대조가 이 줄을 현재 문화권 읽기로 바꾼다.
    private static decimal ParseDecimal(string value) => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
}
