using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Judgment;

// 스펙 4.13 Core 공개 API(잠정 결정 10)와 5절 예외.
// 이 파일은 컴파일 자체가 API 모양 검사다. 타입·이름·매개변수 순서가 다르면 컴파일되지 않는다.
// test-engineer 단계에서는 Core에 Scorecard.Core.Judgment가 없어 컴파일이 실패한다(T-003 승인 결정 14).
[Trait("Spec", "judgment")]
public partial class JudgmentApiShapeTests
{
    private const string JudgmentNamespace = "Scorecard.Core.Judgment";

    // 4.13의 공개 타입 전부: 정적 클래스 2개 + 레코드 5개. 규칙은 internal JudgmentRules다.
    private static readonly string[] PublicTypes =
    [
        "JudgmentCheck",
        "JudgmentEngine",
        "JudgmentJson",
        "JudgmentResult",
        "JudgmentThresholds",
        "JudgmentTimes",
        "JudgmentTodo",
    ];

    // 4.13 블록의 "public const string X = \"y\";"·"public const int X = n;" 줄 = Core 상수. 이름·값·개수가 같다.
    [Fact]
    public void Constants_equal_spec_api_block()
    {
        var block = Assert.Single(SpecDocument.CodeBlocks(JudgmentSpec.Document, JudgmentSpec.ApiHeading));
        var spec = ConstLine().Matches(block)
            .Select(m => (Owner: OwnerOf(block, m.Index), Name: m.Groups["name"].Value, Value: m.Groups["value"].Value.Trim('"')))
            .ToArray();
        Assert.Equal(13, spec.Length);

        foreach (var owner in new[] { typeof(JudgmentEngine), typeof(JudgmentJson) })
        {
            var core = owner.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral)
                .Select(f => (f.Name, Value: Convert.ToString(f.GetRawConstantValue(), CultureInfo.InvariantCulture)!))
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .ToArray();
            var expected = spec.Where(s => s.Owner == owner.Name)
                .Select(s => (s.Name, s.Value))
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(expected, core);
        }
    }

    // 판정 값 넷 = 0절의 넷 = 11.4 판정 칸. 근거 키 일곱 = 11.4 근거 키 칸(표 순서, 처음 나오는 순서).
    // T-012(11.2·11.16): judgment.json schema_version은 2다. 확인 결과의 뜻과 다음 할 일 키가 바뀌었다(ADR 0004 결정 2).
    [Fact]
    public void Constants_have_spec_values()
    {
        Assert.Equal(
            JudgmentSpec.VerdictNames().Values.Order(StringComparer.Ordinal),
            new[] { JudgmentEngine.VerdictPass, JudgmentEngine.VerdictConditional, JudgmentEngine.VerdictHold, JudgmentEngine.VerdictNotVerified }.Order(StringComparer.Ordinal));
        Assert.Equal(
            JudgmentSpec.VerdictRows().Select(r => r.Verdict).Distinct().Order(StringComparer.Ordinal),
            new[] { JudgmentEngine.VerdictPass, JudgmentEngine.VerdictConditional, JudgmentEngine.VerdictHold, JudgmentEngine.VerdictNotVerified }.Order(StringComparer.Ordinal));
        Assert.Equal(
            JudgmentSpec.VerdictRows().Select(r => r.Rule).Distinct(),
            [
                JudgmentEngine.RuleNotVerified, JudgmentEngine.RuleAccuracy, JudgmentEngine.RuleMissing, JudgmentEngine.RuleUnexplained,
                JudgmentEngine.RuleTime, JudgmentEngine.RuleFocus, JudgmentEngine.RulePass,
            ]);
        Assert.Equal("judgment.json", JudgmentJson.FileName);
        Assert.Equal(JudgmentSpec.SchemaVersion, JudgmentJson.SchemaVersion);
        Assert.Equal(2, JudgmentJson.SchemaVersion);
    }

    // 공개 메서드 둘의 서명. Judge의 시간·사람 입력은 선택 인자다(null이 기본).
    [Fact]
    public void Entry_points_have_spec_signatures()
    {
        Func<CaseDefinition, CompareMetrics, JudgmentTimes?, IReadOnlyList<string>?, JudgmentResult> judge = JudgmentEngine.Judge;
        Func<JudgmentResult, byte[]> render = JudgmentJson.Render;
        Assert.All(new Delegate[] { judge, render }, d => Assert.NotNull(d));

        var parameters = typeof(JudgmentEngine).GetMethod(nameof(JudgmentEngine.Judge))!.GetParameters();
        Assert.Equal(["definition", "metrics", "times", "unexplainedKeys"], parameters.Select(p => p.Name));
        Assert.All(parameters.Skip(2), p =>
        {
            Assert.True(p.IsOptional, $"{p.Name}은 선택 인자여야 한다.");
            Assert.Null(p.DefaultValue);
        });

        // 두 인자로 부르면 시간·사람 입력이 없는 것과 같다(8절 골든).
        var scene = JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.NotReviewed, JudgmentInput.NotMeasured, JudgmentInput.Spread);
        var definition = scene.Definition;
        var metrics = scene.Metrics();
        Assert.Equal(JudgmentJson.Render(JudgmentEngine.Judge(definition, metrics, null, null)), JudgmentJson.Render(JudgmentEngine.Judge(definition, metrics)));
    }

    // 레코드 다섯의 위치 매개변수 순서와 형식.
    [Fact]
    public void Records_have_spec_shape()
    {
        var times = new JudgmentTimes(BeforeMinutes: 10m, ProcessMinutes: 6m, ReviewMinutes: 3m);
        var thresholds = new JudgmentThresholds(AccuracyMin: 95m, MissingMax: 2m, TimeMustDrop: true);
        var check = new JudgmentCheck(Rule: "ACCURACY", Result: "met", Reason: "r");
        var todo = new JudgmentTodo(Key: "measure_time", Text: "t");
        var result = new JudgmentResult(
            Case: "p",
            Verdict: "pass",
            Rule: "PASS",
            Reason: "r",
            Thresholds: thresholds,
            Defaults: ["accuracy_min"],
            Checks: [check],
            Todo: [todo]);

        Assert.Equal((10m, 6m, 3m), (times.BeforeMinutes, times.ProcessMinutes, times.ReviewMinutes));
        Assert.Same(thresholds, result.Thresholds);
        Assert.Same(check, Assert.Single(result.Checks));
        Assert.Same(todo, Assert.Single(result.Todo));
        Assert.Equal(["accuracy_min"], result.Defaults);
        Assert.Equal(new JudgmentThresholds(95m, 2m, true), result.Thresholds);
    }

    [Fact]
    public void Judgment_namespace_exports_only_spec_types()
    {
        var exported = typeof(JudgmentEngine).Assembly.GetExportedTypes()
            .Where(t => t.Namespace == JudgmentNamespace)
            .Select(t => t.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(PublicTypes, exported);
    }

    // 5절: definition·metrics가 null이면 ArgumentNullException.
    [Fact]
    public void Null_arguments_throw()
    {
        var scene = JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.NotReviewed, JudgmentInput.Off, JudgmentInput.Spread);
        var definition = scene.Definition;
        var metrics = scene.Metrics();

        Assert.Throws<ArgumentNullException>(() => JudgmentEngine.Judge(null!, metrics));
        Assert.Throws<ArgumentNullException>(() => JudgmentEngine.Judge(definition, null!));
    }

    // 5절: metrics.Case가 definition.Name과 다르면 ArgumentException.
    [Fact]
    public void Case_name_mismatch_throws()
    {
        var scene = JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.NotReviewed, JudgmentInput.Off, JudgmentInput.Spread);
        var definition = scene.Definition;
        var metrics = scene.Metrics();

        Assert.ThrowsAny<ArgumentException>(() => JudgmentEngine.Judge(definition, metrics with { Case = definition.Name + "-other" }));
        Assert.ThrowsAny<ArgumentException>(() => JudgmentEngine.Judge(definition with { Name = "other" }, metrics));
    }

    // 5절·4.10: 시간 값이 음수면 ArgumentOutOfRangeException. 세 값 각각. time_must_drop: true인 측정 장면에서 본다.
    [Theory]
    [InlineData("-1", "6", "3")]
    [InlineData("10", "-0.5", "3")]
    [InlineData("10", "6", "-0.0001")]
    public void Negative_time_throws(string before, string process, string review)
    {
        var scene = JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.NotReviewed, JudgmentInput.Met, JudgmentInput.Spread);
        var definition = scene.Definition;
        var metrics = scene.Metrics();
        var times = new JudgmentTimes(JudgmentSpec.Decimal(before), JudgmentSpec.Decimal(process), JudgmentSpec.Decimal(review));

        Assert.Throws<ArgumentOutOfRangeException>(() => JudgmentEngine.Judge(definition, metrics, times));
    }

    // 잠정 결정 15: 음수 시간은 time_must_drop과 상관없이 예외다. time_must_drop: false라 시간을 보지 않는 장면에서도 그렇다.
    [Theory]
    [InlineData("-1", "6", "3")]
    [InlineData("10", "-0.5", "3")]
    [InlineData("10", "6", "-0.0001")]
    public void Negative_time_throws_even_when_time_must_drop_is_false(string before, string process, string review)
    {
        var scene = JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.NotReviewed, JudgmentInput.Off, JudgmentInput.Spread);
        var definition = scene.Definition;
        var metrics = scene.Metrics();
        var times = new JudgmentTimes(JudgmentSpec.Decimal(before), JudgmentSpec.Decimal(process), JudgmentSpec.Decimal(review));
        Assert.False(definition.Thresholds.TimeMustDrop);

        Assert.Throws<ArgumentOutOfRangeException>(() => JudgmentEngine.Judge(definition, metrics, times));

        // 0 이상이면 예외가 없고 시간은 보지 않는다(빈 통과 방지).
        var fine = new JudgmentTimes(10m, 6m, 3m);
        Assert.Equal(JudgmentInput.Off, JudgmentInput.CheckOf(JudgmentEngine.Judge(definition, metrics, fine), JudgmentSpec.Time).Result);
    }

    // 잠정 결정 15: 사람 입력 키는 미검증에도 확인한다. 미검증 대조에는 불일치 항목이 없어 키가 하나라도 있으면 ArgumentException.
    // no_inputs·no_compare_fields 둘 다. 키 하나, 같은 키 둘.
    [Fact]
    public void Unexplained_keys_are_checked_even_when_not_verified()
    {
        foreach (var definition in new[] { JudgmentInput.NoInputsCase(), JudgmentInput.NoCompareCase() })
        {
            var metrics = JudgmentInput.NotVerifiedMetrics(definition);
            Assert.Equal(CompareEngine.StatusNotVerified, metrics.Status);

            Assert.ThrowsAny<ArgumentException>(() => JudgmentEngine.Judge(definition, metrics, null, ["k0001"]));
            Assert.ThrowsAny<ArgumentException>(() => JudgmentEngine.Judge(definition, metrics, null, ["k0001", "k0001"]));

            // 사람 입력이 없으면(null) 예외 없이 미검증이다(빈 통과 방지).
            Assert.Equal(JudgmentEngine.VerdictNotVerified, JudgmentEngine.Judge(definition, metrics).Verdict);
        }
    }

    // 5절·11.9: 사람 입력 키가 검토 항목(불일치·추가 행·중복 행, 11.3)의 키가 아니거나 중복이면 ArgumentException.
    // 차이 없는 일치 항목의 키, 없는 키, 중복 키. 불일치가 없는 장면에 키를 넣어도 그렇다.
    // T-012(카드 수용 기준 1): 추가 행 키와 결과 쪽 중복 행 키는 받는다. T-011은 추가 행 키도 거절했다.
    // T-013(metrics.md 4.3): 결과 쪽에 두 번 나온 기준 키는 값 불일치이기도 하다. 그래서 장면의 불일치 키는 셋(보통 둘 + 중복 행 키)이다.
    [Fact]
    public void Unexplained_keys_must_be_distinct_mismatch_keys()
    {
        var plain = JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.NotReviewed, JudgmentInput.Off, JudgmentInput.Spread);
        var duplicated = plain.Rows[^1];
        var scene = plain with
        {
            Extras = [new SceneRow("x0001", ["a"], SceneRow.Match)],
            Duplicates = [duplicated],
        };
        var definition = scene.Definition;
        var metrics = scene.Metrics();
        var bad = scene.BadKeys;
        var match = scene.Rows.First(r => !r.IsBad && r.Key != duplicated.Key).Key;
        Assert.Equal(3, bad.Length);
        Assert.Equal(duplicated.Key, bad[^1]);
        Assert.False(duplicated.IsBad);

        Assert.ThrowsAny<ArgumentException>(() => JudgmentEngine.Judge(definition, metrics, null, [match]));
        Assert.ThrowsAny<ArgumentException>(() => JudgmentEngine.Judge(definition, metrics, null, ["x0001", "x0001"]));
        Assert.ThrowsAny<ArgumentException>(() => JudgmentEngine.Judge(definition, metrics, null, ["no-such-key"]));
        Assert.ThrowsAny<ArgumentException>(() => JudgmentEngine.Judge(definition, metrics, null, [bad[0], bad[0]]));
        Assert.ThrowsAny<ArgumentException>(() => JudgmentEngine.Judge(definition, metrics, null, [bad[0], match]));

        var clean = JudgmentInput.Checks(JudgmentInput.Met, JudgmentInput.Met, JudgmentInput.NoneResult, JudgmentInput.Off, JudgmentInput.Few);
        Assert.ThrowsAny<ArgumentException>(() => JudgmentEngine.Judge(clean.Definition, clean.Metrics(), null, [clean.Rows[0].Key]));

        // 바른 입력은 예외가 없다(빈 통과 방지). 불일치 키, 추가 행 키, 중복 행 키(결과 쪽에 두 번 나온 기준 키), 셋을 섞은 것.
        foreach (IReadOnlyList<string> keys in new[] { bad, ["x0001"], [duplicated.Key], [bad[0], "x0001", duplicated.Key] })
        {
            var judged = JudgmentEngine.Judge(definition, metrics, null, keys);
            Assert.Equal(JudgmentInput.Found, JudgmentInput.CheckOf(judged, JudgmentSpec.Unexplained).Result);
            Assert.Equal((JudgmentEngine.VerdictHold, JudgmentEngine.RuleUnexplained), (judged.Verdict, judged.Rule));
        }
    }

    // const 줄이 속한 클래스. 블록에서 그 줄 앞의 마지막 "public static class X" 줄.
    private static string OwnerOf(string block, int index)
    {
        var owner = ClassLine().Matches(block[..index]).LastOrDefault();
        Assert.NotNull(owner);
        return owner.Groups["name"].Value;
    }

    [GeneratedRegex(@"public const (string|int) (?<name>[A-Za-z]+) = (?<value>""[^""]*""|[0-9]+);", RegexOptions.CultureInvariant)]
    private static partial Regex ConstLine();

    [GeneratedRegex(@"public static class (?<name>[A-Za-z]+)", RegexOptions.CultureInvariant)]
    private static partial Regex ClassLine();
}
