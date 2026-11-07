using System.Globalization;
using System.Text.RegularExpressions;
using Scorecard.Core.Cases;
using Scorecard.Core.Synthetic;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Classification;

namespace Scorecard.Tests.Synthetic;

// 스펙 4.7 심은 구조(승인 결정 5·6)와 6.1·6.2 예시. 카드 수용 기준 3.
// 셈은 두 가지로 한다: 생성 결과의 Planted 셈, 테스트 안 단순 대조(키 맞춤, 4.7 용어, 허용 오차).
// 둘 다 4.7 구조 표와 같아야 한다. 대조 정의는 T-006이 확정한다(승인 결정 5).
[Trait("Spec", "synthetic-examples")]
public partial class StructureTests
{
    // 4.5 상수: 정수 값 불일치 폭 20. 소수 값 불일치는 허용 오차의 2배 이상, 허용오차 안 차이는 절반 이하.
    private const int IntegerMismatchSpread = 20;

    // 3.1 Planted 표(값, 어디). 시드 2000개를 볼 때 문서를 한 번만 읽는다.
    private static readonly Lazy<(string Value, string Where)[]> PlantedTable = new(() =>
        SyntheticSpec.Table(SyntheticSpec.PlantedHeader).Select(c => (SpecDocument.Unquote(c[0]), c[1])).ToArray());

    public static TheoryData<string, string> ExampleVariants
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var example in SyntheticInput.DataExamples)
            {
                foreach (var variant in Variants.All)
                {
                    data.Add(example, variant);
                }
            }

            return data;
        }
    }

    [Fact]
    public void Structure_table_examples_are_data_examples()
    {
        Assert.Equal(
            SyntheticInput.DataExamples,
            SyntheticSpec.Table(SyntheticSpec.StructureHeader).Select(c => SpecDocument.Unquote(c[0])));
    }

    // 4.7 용어 표가 이 테스트의 셈과 같은 용어를 쓴다(Tally의 칸 이름과 짝).
    [Fact]
    public void Term_table_lists_the_counted_terms()
    {
        Assert.Equal(
            ["항목", "일치", "값 불일치", "누락", "추가", "허용오차 안 차이", "불일치(12절)"],
            SyntheticSpec.Table(SyntheticSpec.TermHeader).Select(c => c[0]));
    }

    // 커밋된 CSV를 키로 맞춰 센 값 = 4.7 구조 표. 대조 변형 3종(≤, <, 결과값 기준 %)이 같다(경계값 없음, 4.5).
    [Theory]
    [MemberData(nameof(ExampleVariants))]
    public void Committed_files_match_structure_table(string example, string variant)
    {
        var definition = SyntheticInput.CaseOf(example);
        var reference = SyntheticInput.ParseCsv(SyntheticInput.CommittedCsv(example, SyntheticCsv.ReferenceFolder), example);
        var candidate = SyntheticInput.ParseCsv(SyntheticInput.CommittedCsv(example, SyntheticCsv.CandidateFolder), example);
        Assert.Equal(reference.Header, candidate.Header);

        var tally = SyntheticInput.Count(definition, reference.Header, reference.Rows, candidate.Rows, variant);
        var (expected, accuracy, missingRate) = SyntheticSpec.Structure(example);

        Assert.Equal(expected, tally);
        Assert.Equal(accuracy, tally.Accuracy);
        Assert.Equal(missingRate, tally.MissingRate);
    }

    // Planted 셈 = 4.7 구조 표. 일치 = 기준 행 − 누락 − 값 불일치.
    [Theory]
    [MemberData(nameof(SyntheticInput.DataExampleNames), MemberType = typeof(SyntheticInput))]
    public void Planted_counts_match_structure_table(string example)
    {
        Assert.Equal(SyntheticSpec.Structure(example).Counts, PlantedTally(SyntheticInput.Generate(example)));
    }

    // 3.1 Planted 값은 표의 "어디"(기준·결과)에만 나온다. 심은 행은 계획대로다(4.5).
    [Theory]
    [MemberData(nameof(SyntheticInput.DataExampleNames), MemberType = typeof(SyntheticInput))]
    public void Planted_rows_follow_plan(string example)
    {
        AssertPlantedRows(SyntheticInput.CaseOf(example), SyntheticSpec.Plan(example), SyntheticInput.Generate(example));
    }

    // 4.7: 누락·값 불일치 행은 모두 몰림 값(`irregular`) 기준 행에 있다. 다른 값의 기준 행은 불일치가 0이다.
    [Theory]
    [MemberData(nameof(SyntheticInput.DataExampleNames), MemberType = typeof(SyntheticInput))]
    public void Defects_are_only_in_focus_rows(string example)
    {
        AssertFocus(SyntheticSpec.Plan(example), SyntheticInput.Generate(example));
    }

    // 4.2: 시드 0~1999 모두 생성에 성공하고, 셈(Planted·대조 3종)이 4.7 표와 같고, 심은 행·몰림이 계획대로다. 시드를 바꾸면 값만 바뀐다.
    [Theory]
    [MemberData(nameof(SyntheticInput.DataExampleNames), MemberType = typeof(SyntheticInput))]
    public void Every_seed_below_2000_keeps_the_structure(string example)
    {
        var definition = SyntheticInput.CaseOf(example);
        var plan = SyntheticSpec.Plan(example);
        var expected = SyntheticSpec.Structure(example).Counts;
        var wrong = new List<string>();

        for (ulong seed = 0; seed < 2000; seed++)
        {
            try
            {
                var tables = SyntheticGenerator.Generate(definition, plan with { Seed = seed });
                var counts = Variants.All.Select(v => SyntheticInput.Count(definition, tables, v)).Prepend(PlantedTally(tables)).Distinct().ToArray();
                if (counts.Length != 1 || counts[0] != expected)
                {
                    wrong.Add($"시드 {seed}: {string.Join(" / ", counts)}");
                }

                AssertPlantedRows(definition, plan, tables);
                AssertFocus(plan, tables);
            }
            catch (Exception ex) when (ex is ArgumentException or Xunit.Sdk.XunitException)
            {
                wrong.Add($"시드 {seed}: {ex.Message}");
            }

            if (wrong.Count > 10)
            {
                break;
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    // 카드 수용 기준 3·지시문 12절 "검증 자료" 칸: table-compare 30건/30건·불일치 3·누락 1·정확도 90%·누락률 3.3%,
    // form-fill 20건/20건·전부 일치·정확도 100%. "불일치"는 값 불일치 + 누락이다(4.7 용어 "불일치(12절)", 승인 결정 5).
    [Theory]
    [MemberData(nameof(SyntheticInput.DataExampleNames), MemberType = typeof(SyntheticInput))]
    public void Structure_matches_instructions_section_12(string example)
    {
        var cell = Assert.Single(
            SpecDocument.Table(SpecDocument.InstructionsPath, ClassificationSpec.InstructionsExampleHeader),
            c => SpecDocument.Unquote(c[0]) == example)[4];
        var (counts, accuracy, _) = SyntheticSpec.Structure(example);

        var sizes = InstructionSizes().Match(cell);
        Assert.True(sizes.Success, $"지시문 12절 {example} 검증 자료 칸에 'reference N건 / candidate M건'이 없다: {cell}");
        Assert.Equal(Int(sizes.Groups["reference"].Value), counts.Reference);
        Assert.Equal(Int(sizes.Groups["candidate"].Value), counts.Candidate);

        var accuracyMatch = InstructionAccuracy().Match(cell);
        Assert.True(accuracyMatch.Success, $"지시문 12절 {example} 검증 자료 칸에 '정확도 N%'가 없다: {cell}");
        Assert.Equal(SyntheticInput.Number(accuracyMatch.Groups["value"].Value), SyntheticInput.Number(accuracy));

        var defects = InstructionDefects().Match(cell);
        if (cell.Contains("전부 일치", StringComparison.Ordinal))
        {
            Assert.False(defects.Success);
            Assert.Equal(counts.Reference, counts.Match);
            Assert.Equal(0, counts.Missing);
        }
        else
        {
            Assert.True(defects.Success, $"지시문 12절 {example} 검증 자료 칸에 '불일치 N건·누락 M건'이 없다: {cell}");
            Assert.Equal(Int(defects.Groups["defects"].Value), counts.Mismatch + counts.Missing);
            Assert.Equal(Int(defects.Groups["missing"].Value), counts.Missing);
            var rate = InstructionMissingRate().Match(cell);
            Assert.True(rate.Success, $"지시문 12절 {example} 검증 자료 칸에 '누락률 N%'가 없다: {cell}");
            Assert.Equal(SyntheticInput.Number(rate.Groups["value"].Value), SyntheticInput.Number(counts.MissingRate));
        }
    }

    // 6.1 표: table-compare에 심은 행 전부(키, signal_type, 심은 것, 기준 → 결과, 차이 %).
    [Fact]
    public void Table_compare_planted_rows_match_section_6_1()
    {
        var tables = SyntheticInput.Generate("table-compare");
        var signal = SyntheticInput.IndexOf(tables.Header, "signal_type") - 1;
        var reference = tables.Reference.ToDictionary(r => r.Key, StringComparer.Ordinal);
        var candidate = tables.Candidate.ToDictionary(r => r.Key, StringComparer.Ordinal);
        var rows = SyntheticSpec.Table(SyntheticSpec.PlantedRowsHeader);

        var plantedKeys = tables.Reference.Where(r => r.Planted != SyntheticGenerator.PlantedNone)
            .Concat(tables.Candidate.Where(r => r.Planted != SyntheticGenerator.PlantedNone))
            .Select(r => r.Key)
            .Order(StringComparer.Ordinal);
        Assert.Equal(plantedKeys, rows.Select(c => SpecDocument.Unquote(c[0])).Order(StringComparer.Ordinal));

        foreach (var c in rows)
        {
            var key = SpecDocument.Unquote(c[0]);
            var planted = PlantedOf(c[2]);
            var change = c[3].Trim();
            var source = planted == SyntheticGenerator.PlantedExtra ? candidate[key] : reference[key];
            Assert.Equal(c[1].Trim(), source.Values[signal]);

            if (planted == SyntheticGenerator.PlantedMissing)
            {
                Assert.Equal(planted, reference[key].Planted);
                Assert.False(candidate.ContainsKey(key));
                Assert.Equal("결과에 없음", change);
                continue;
            }

            Assert.Equal(planted, candidate[key].Planted);
            if (planted == SyntheticGenerator.PlantedExtra)
            {
                Assert.False(reference.ContainsKey(key));
                Assert.Equal("기준에 없음", change);
                continue;
            }

            var column = SyntheticInput.IndexOf(tables.Header, planted[(planted.IndexOf(':', StringComparison.Ordinal) + 1)..]) - 1;
            var m = ChangeCell().Match(change);
            Assert.True(m.Success, $"6.1 '{key}' 기준 → 결과 칸 '{change}'을 읽을 수 없다.");
            Assert.Equal(m.Groups["from"].Value, reference[key].Values[column]);
            Assert.Equal(m.Groups["to"].Value, candidate[key].Values[column]);
            if (m.Groups["pct"].Success)
            {
                var (from, to) = (SyntheticInput.Number(m.Groups["from"].Value), SyntheticInput.Number(m.Groups["to"].Value));
                var pct = Math.Round(Math.Abs(to - from) * 100m / Math.Abs(from), 2, MidpointRounding.AwayFromZero);
                Assert.Equal(m.Groups["pct"].Value, pct.ToString("F2", CultureInfo.InvariantCulture));
            }
        }
    }

    // 6.1 아래 목록: irregular 기준 행 12개, 불일치 3건이 모두 그 안. 결과 첫 행과 추가 행의 위치.
    [Fact]
    public void Table_compare_focus_and_order_lines_match_section_6_1()
    {
        var tables = SyntheticInput.Generate("table-compare");
        var plan = SyntheticSpec.Plan("table-compare");
        var focus = SyntheticInput.IndexOf(tables.Header, plan.FocusColumn!) - 1;

        var line = SpecDocument.Line(SyntheticSpec.Document, $"- `{plan.FocusValue}` 기준 행 ");
        var m = FocusLine().Match(line.Trim());
        Assert.True(m.Success, $"6.1 몰림 줄을 읽을 수 없다: {line}");
        var focusKeys = SpecDocument.CodeSpans(m.Groups["keys"].Value);
        var defectKeys = SpecDocument.CodeSpans(m.Groups["defects"].Value);

        Assert.Equal(Int(m.Groups["count"].Value), focusKeys.Length);
        Assert.Equal(focusKeys, tables.Reference.Where(r => r.Values[focus] == plan.FocusValue).Select(r => r.Key));
        Assert.Equal(Int(m.Groups["defectCount"].Value), defectKeys.Length);
        Assert.Equal(
            defectKeys,
            tables.Reference.Where(r => r.Planted == SyntheticGenerator.PlantedMissing)
                .Concat(tables.Candidate.Where(r => r.Planted.StartsWith(SyntheticGenerator.PlantedMismatchPrefix, StringComparison.Ordinal)))
                .Select(r => r.Key)
                .Order(StringComparer.Ordinal));

        var order = SpecDocument.Line(SyntheticSpec.Document, "- 결과 행 순서는 섞였다.");
        var o = OrderLine().Match(order.Trim());
        Assert.True(o.Success, $"6.1 결과 순서 줄을 읽을 수 없다: {order}");
        Assert.Equal(o.Groups["first"].Value, tables.Candidate[0].Key);
        var extra = tables.Candidate.Select((r, i) => (r, i)).Single(x => x.r.Key == o.Groups["extra"].Value);
        Assert.Equal(SyntheticGenerator.PlantedExtra, extra.r.Planted);
        Assert.Equal(Int(o.Groups["position"].Value), extra.i + 1);
    }

    // 6.2: form-fill은 20행 모두 기준과 결과가 같고 순서만 섞였다. 품목 수.
    [Fact]
    public void Form_fill_matches_section_6_2()
    {
        var tables = SyntheticInput.Generate("form-fill");
        var item = SyntheticInput.IndexOf(tables.Header, "item") - 1;

        var reference = SyntheticInput.Cells(tables.Reference);
        var candidate = SyntheticInput.Cells(tables.Candidate);
        Assert.Equal(20, reference.Length);
        Assert.Equal(
            reference.Select(r => string.Join(",", r)).Order(StringComparer.Ordinal),
            candidate.Select(r => string.Join(",", r)).Order(StringComparer.Ordinal));
        Assert.NotEqual(reference.Select(r => r[0]), candidate.Select(r => r[0]));

        var line = SpecDocument.Line(SyntheticSpec.Document, "- 품목 5종이 모두 나온다.");
        var expected = ItemCount().Matches(line).Select(m => (m.Groups["item"].Value, Int(m.Groups["count"].Value))).ToArray();
        var actual = tables.Reference.GroupBy(r => r.Values[item]).Select(g => (g.Key, g.Count())).OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();

        Assert.Equal(5, expected.Length);
        Assert.Equal(expected, actual);
    }

    // 생성 결과의 Planted로 센 4.7 셈.
    internal static Tally PlantedTally(SyntheticTables tables)
    {
        var missing = tables.Reference.Count(r => r.Planted == SyntheticGenerator.PlantedMissing);
        var mismatch = tables.Candidate.Count(r => r.Planted.StartsWith(SyntheticGenerator.PlantedMismatchPrefix, StringComparison.Ordinal));
        var within = tables.Candidate.Count(r => r.Planted.StartsWith(SyntheticGenerator.PlantedWithinPrefix, StringComparison.Ordinal));
        var extra = tables.Candidate.Count(r => r.Planted == SyntheticGenerator.PlantedExtra);
        return new Tally(tables.Reference.Count, tables.Candidate.Count, tables.Reference.Count - missing - mismatch, mismatch, missing, extra, within);
    }

    // 3.1·4.5: 심은 행이 계획대로인지 행마다 본다. SyntheticGuardTargetTests도 쓴다.
    // - 기준 행은 none·missing, 결과 행은 none·mismatch:<열>·within:<열>·extra(3.1 "어디").
    // - none 결과 행 = 기준 행. mismatch·within 행은 그 열만 다르다. extra 키는 기준에 없다. missing 키는 결과에 없다.
    // - 값 불일치: 정수 t+1~t+20, 소수 허용 오차(%)의 2배 이상(반올림 반 단위 여유), 선택은 다른 선택지.
    // - 허용오차 안 차이: 0이 아니고 허용 오차(%)의 절반 이하.
    internal static void AssertPlantedRows(CaseDefinition definition, SyntheticPlan plan, SyntheticTables tables)
    {
        var allowed = PlantedTable.Value;
        bool Allowed(string planted, string where) => allowed.Any(a =>
            a.Where.Contains(where, StringComparison.Ordinal)
            && (a.Value == planted || (a.Value.EndsWith(":<열>", StringComparison.Ordinal) && planted.StartsWith(a.Value[..^"<열>".Length], StringComparison.Ordinal))));

        Assert.All(tables.Reference, r => Assert.True(Allowed(r.Planted, "기준"), $"기준 행 {r.Key}의 Planted '{r.Planted}'는 3.1 표에서 기준에 없다."));
        Assert.All(tables.Candidate, r => Assert.True(Allowed(r.Planted, "결과"), $"결과 행 {r.Key}의 Planted '{r.Planted}'는 3.1 표에서 결과에 없다."));

        var compare = definition.Compare.ToDictionary(f => f.Field, StringComparer.Ordinal);
        var reference = tables.Reference.ToDictionary(r => r.Key, StringComparer.Ordinal);
        var candidateKeys = tables.Candidate.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(tables.Candidate.Count, candidateKeys.Count);
        Assert.All(tables.Reference.Where(r => r.Planted == SyntheticGenerator.PlantedMissing), r => Assert.DoesNotContain(r.Key, candidateKeys));

        // 값 불일치 열마다 한 행(4.3 "열마다 한 행을 허용 오차 밖으로").
        Assert.Equal(
            plan.MismatchColumns.Order(StringComparer.Ordinal),
            tables.Candidate.Where(r => r.Planted.StartsWith(SyntheticGenerator.PlantedMismatchPrefix, StringComparison.Ordinal))
                .Select(r => r.Planted[SyntheticGenerator.PlantedMismatchPrefix.Length..])
                .Order(StringComparer.Ordinal));
        Assert.All(
            tables.Candidate.Where(r => r.Planted.StartsWith(SyntheticGenerator.PlantedWithinPrefix, StringComparison.Ordinal)),
            r => Assert.Equal(SyntheticGenerator.PlantedWithinPrefix + plan.WithinColumn, r.Planted));

        foreach (var row in tables.Candidate)
        {
            if (row.Planted == SyntheticGenerator.PlantedExtra)
            {
                Assert.False(reference.ContainsKey(row.Key), $"추가 행 {row.Key}가 기준에도 있다.");
                Assert.Equal(plan.Columns.Count, row.Values.Count);
                continue;
            }

            Assert.True(reference.TryGetValue(row.Key, out var r), $"결과 행 {row.Key}가 기준에 없는데 extra가 아니다.");
            Assert.Equal(SyntheticGenerator.PlantedNone, r.Planted);
            var differing = Enumerable.Range(0, plan.Columns.Count).Where(i => r.Values[i] != row.Values[i]).ToArray();
            if (row.Planted == SyntheticGenerator.PlantedNone)
            {
                Assert.Empty(differing);
                continue;
            }

            var name = row.Planted[(row.Planted.IndexOf(':', StringComparison.Ordinal) + 1)..];
            var i = plan.Columns.Select((c, n) => (c, n)).Single(x => x.c.Name == name).n;
            Assert.Equal([i], differing);
            var (column, field) = (plan.Columns[i], compare[name]);
            var (before, after) = (r.Values[i], row.Values[i]);

            if (row.Planted.StartsWith(SyntheticGenerator.PlantedMismatchPrefix, StringComparison.Ordinal))
            {
                AssertMismatch(row.Key, column, field, before, after);
            }
            else
            {
                AssertWithin(row.Key, column, field, before, after);
            }
        }
    }

    // 몰림이 있으면 누락·값 불일치 행은 모두 몰림 값의 기준 행이다.
    internal static void AssertFocus(SyntheticPlan plan, SyntheticTables tables)
    {
        if (plan.FocusColumn is null)
        {
            return;
        }

        var focus = SyntheticInput.IndexOf(tables.Header, plan.FocusColumn) - 1;
        var reference = tables.Reference.ToDictionary(r => r.Key, StringComparer.Ordinal);
        var defects = tables.Reference.Where(r => r.Planted == SyntheticGenerator.PlantedMissing).Select(r => r.Key)
            .Concat(tables.Candidate.Where(r => r.Planted.StartsWith(SyntheticGenerator.PlantedMismatchPrefix, StringComparison.Ordinal)).Select(r => r.Key))
            .ToArray();

        Assert.Equal(plan.Missing + plan.MismatchColumns.Count, defects.Length);
        Assert.All(defects, k => Assert.Equal(plan.FocusValue, reference[k].Values[focus]));
    }

    private static void AssertMismatch(string key, SyntheticColumn column, CompareField field, string before, string after)
    {
        switch (column.Kind)
        {
            case SyntheticColumn.KindInteger:
                {
                    var t = field.Tolerance!.Value;
                    var diff = Math.Abs(SyntheticInput.Number(after) - SyntheticInput.Number(before));
                    Assert.InRange(diff, t + 1, t + IntegerMismatchSpread);
                    Assert.InRange(SyntheticInput.Number(after), column.Min, column.Max);
                    break;
                }

            case SyntheticColumn.KindDecimal:
                {
                    var (r, c, p) = (SyntheticInput.Number(before), SyntheticInput.Number(after), field.TolerancePct!.Value);
                    var halfUnit = 0.5m / Pow10(column.Scale);
                    Assert.True(Math.Abs(c - r) >= (2 * p * Math.Abs(r) / 100m) - halfUnit, $"{key}: 값 불일치 {before} → {after}가 허용 오차 {p}%의 2배보다 작다.");
                    break;
                }

            default:
                Assert.NotEqual(before, after);
                Assert.Contains(after, column.Choices);
                break;
        }
    }

    private static void AssertWithin(string key, SyntheticColumn column, CompareField field, string before, string after)
    {
        Assert.Equal(SyntheticColumn.KindDecimal, column.Kind);
        var (r, c, p) = (SyntheticInput.Number(before), SyntheticInput.Number(after), field.TolerancePct!.Value);
        Assert.True(c != r && Math.Abs(c - r) * 200m <= p * Math.Abs(r), $"{key}: 허용오차 안 차이 {before} → {after}가 0이거나 허용 오차 {p}%의 절반을 넘는다.");
    }

    // "누락" → missing, "추가" → extra, "값 불일치 `열`" → mismatch:열, "허용오차 안 `열`" → within:열.
    private static string PlantedOf(string cell)
    {
        var text = cell.Trim();
        if (text == "누락")
        {
            return SyntheticGenerator.PlantedMissing;
        }

        if (text == "추가")
        {
            return SyntheticGenerator.PlantedExtra;
        }

        var m = PlantedCell().Match(text);
        Assert.True(m.Success, $"6.1 심은 것 칸 '{cell}'을 읽을 수 없다.");
        var prefix = m.Groups["kind"].Value == "값 불일치" ? SyntheticGenerator.PlantedMismatchPrefix : SyntheticGenerator.PlantedWithinPrefix;
        return prefix + m.Groups["column"].Value;
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

    private static int Int(string text) => int.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^(?<kind>값 불일치|허용오차 안) `(?<column>[^`]+)`\z", RegexOptions.CultureInvariant)]
    private static partial Regex PlantedCell();

    [GeneratedRegex(@"^(?<from>-?[0-9]+(\.[0-9]+)?) → (?<to>-?[0-9]+(\.[0-9]+)?) \(((?<pct>[0-9]+\.[0-9]+)%[^)]*|[^)]*)\)\z", RegexOptions.CultureInvariant)]
    private static partial Regex ChangeCell();

    [GeneratedRegex(@"^- `[^`]+` 기준 행 (?<count>[0-9]+)개: (?<keys>[^.]+)\. 불일치 (?<defectCount>[0-9]+)건\((?<defects>[^)]+)\)이 모두 여기 있다\.\z", RegexOptions.CultureInvariant)]
    private static partial Regex FocusLine();

    [GeneratedRegex(@"첫 행은 `(?<first>[^`]+)`, 추가 행 `(?<extra>[^`]+)`은 (?<position>[0-9]+)번째다\.", RegexOptions.CultureInvariant)]
    private static partial Regex OrderLine();

    [GeneratedRegex(@"`(?<item>[^`]+)` (?<count>[0-9]+)", RegexOptions.CultureInvariant)]
    private static partial Regex ItemCount();

    [GeneratedRegex(@"reference (?<reference>[0-9]+)건 / candidate (?<candidate>[0-9]+)건", RegexOptions.CultureInvariant)]
    private static partial Regex InstructionSizes();

    [GeneratedRegex(@"정확도 (?<value>[0-9]+(\.[0-9]+)?)%", RegexOptions.CultureInvariant)]
    private static partial Regex InstructionAccuracy();

    [GeneratedRegex(@"누락률 (?<value>[0-9]+(\.[0-9]+)?)%", RegexOptions.CultureInvariant)]
    private static partial Regex InstructionMissingRate();

    [GeneratedRegex(@"불일치 (?<defects>[0-9]+)건·누락 (?<missing>[0-9]+)건", RegexOptions.CultureInvariant)]
    private static partial Regex InstructionDefects();
}
