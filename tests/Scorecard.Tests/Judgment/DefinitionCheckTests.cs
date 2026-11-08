using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;
using Scorecard.Tests.Comparison;

namespace Scorecard.Tests.Judgment;

// 스펙 5절 "정의와 대조 결과"(T-013 카드 수용 기준 2, judgment.md 12.2). Judge는 대조 결과가 그 정의에서 나온 것인지 본다.
// 아니면 ArgumentException(부르는 쪽 잘못, 종료 1). 미검증 이유, measured면 키 열·compare 필드 이름(순서),
// inputs.group이 있으면 기준 행 머리 행에 그 열이 꼭 한 번 있는지. 문턱은 보지 않는다.
// 대조 결과는 정의 칸과 같은 모양의 다른 장면에서 만든다(5절 표 머리 글). 장면 이름(case.yaml name)은 모두 p다.
[Trait("Spec", "judgment")]
public class DefinitionCheckTests
{
    private const string Judged = "판정";
    private const string Rejected = "거절";

    // 5절 정의 칸.
    private const string NoInputs = "`inputs` 없음";
    private const string NoCompare = "`compare` 빔";
    private const string Measured = "`inputs`·`compare` 있음";
    private const string WithGroup = "`inputs.group` 있음";
    private const string NameRow = "(이름)";

    // 5절 표의 행 번호.
    public static TheoryData<int> DefinitionRows => new(Enumerable.Range(0, JudgmentSpec.Table(JudgmentSpec.DefinitionHeader).Count));

    // 5절 표: 행마다 정의와 대조 결과로 Judge를 부른다. "판정"이면 예외가 없고 미검증 여부가 대조 결과와 같다. "거절"이면 ArgumentException.
    [Theory]
    [MemberData(nameof(DefinitionRows))]
    public void Definition_table_row_gives_its_result(int index)
    {
        var c = JudgmentSpec.Table(JudgmentSpec.DefinitionHeader)[index];
        var (definitionCell, resultCell, expected) = (c[0].Trim(), c[1].Trim(), c[2].Trim());
        var definition = DefinitionOf(definitionCell);
        var metrics = MetricsOf(definitionCell, resultCell);

        if (expected == Rejected)
        {
            Assert.Throws<ArgumentException>(() => JudgmentEngine.Judge(definition, metrics));
            return;
        }

        Assert.Equal(Judged, expected);
        var judged = JudgmentEngine.Judge(definition, metrics);
        Assert.Equal(metrics.Status == CompareEngine.StatusNotVerified, judged.Verdict == JudgmentEngine.VerdictNotVerified);
    }

    // 5절 표 모양(빈 통과 방지): 칸은 "판정"·"거절"뿐이다. 정의 넷마다 판정 행 하나(같은 모양의 대조 결과)와 거절 행이 있다.
    // 이름 행은 거절이다.
    [Fact]
    public void Definition_table_covers_every_definition()
    {
        var rows = JudgmentSpec.Table(JudgmentSpec.DefinitionHeader).Select(r => r.Select(x => x.Trim()).ToArray()).ToArray();

        Assert.All(rows, r => Assert.True(r[2] is Judged or Rejected, r[2]));
        foreach (var definition in new[] { NoInputs, NoCompare, Measured, WithGroup })
        {
            Assert.Single(rows, r => r[0] == definition && r[2] == Judged);
            Assert.Contains(rows, r => r[0] == definition && r[2] == Rejected);
        }

        Assert.Equal(Rejected, Assert.Single(rows, r => r[0] == NameRow)[2]);
    }

    // 5절 규칙: inputs.group 열은 기준 행 머리 행에 꼭 한 번 있어야 한다. 두 번이면 거절이다(대조 결과는 group 없는 정의로 만든다).
    [Fact]
    public void Group_column_twice_in_reference_header_is_rejected()
    {
        var twice = new JudgmentScene(
            Columns: [JudgmentInput.Group, JudgmentInput.Group],
            Rows: JudgmentInput.Rows(10, mismatch: 2, types: i => i < 2 ? ["a", "a"] : ["b", "b"]),
            Extras: [],
            AccuracyMin: "0",
            MissingMax: "100",
            TimeMustDrop: "false");
        var metrics = twice.Metrics();
        Assert.All(metrics.Items.Select(i => i.Reference!), r => Assert.Equal(2, r.Header.Count(h => h == JudgmentInput.Group)));

        Assert.Throws<ArgumentException>(() => JudgmentEngine.Judge(Grouped().Definition, metrics));
        Assert.Equal(JudgmentEngine.VerdictConditional, JudgmentEngine.Judge(twice.Definition, metrics).Verdict);
    }

    // 5절 규칙: compare 필드 이름은 순서까지 같아야 한다. 같은 필드를 다른 순서로 적은 정의의 대조 결과는 거절이다.
    [Fact]
    public void Compare_field_order_must_match()
    {
        const string Thresholds = "thresholds:\n  time_must_drop: false\n";
        var vw = CompareInput.Case(CompareInput.InputsCase + "compare:\n  - {field: v}\n  - {field: w}\n" + Thresholds);
        var wv = CompareInput.Case(CompareInput.InputsCase + "compare:\n  - {field: w}\n  - {field: v}\n" + Thresholds);
        const string Csv = "id,v,w\nk1,a,b\nk2,c,d\n";
        var metrics = CompareInput.Measured(CompareInput.Run(wv, Csv, Csv));

        Assert.Throws<ArgumentException>(() => JudgmentEngine.Judge(vw, metrics));
        Assert.Equal(JudgmentEngine.VerdictPass, JudgmentEngine.Judge(wv, metrics).Verdict);
    }

    // judgment.md 12.2: 문턱(thresholds)은 대조가 쓰지 않으므로 보지 않는다. 같은 대조 결과로 문턱만 바꿔 판정하는 것은 된다.
    [Fact]
    public void Thresholds_are_not_checked_against_the_compare_result()
    {
        var scene = Plain();
        var metrics = scene.Metrics();
        var strict = scene with { AccuracyMin = "100" };
        var unset = scene with { AccuracyMin = null, MissingMax = null, TimeMustDrop = null };

        Assert.Equal(JudgmentEngine.VerdictConditional, JudgmentEngine.Judge(scene.Definition, metrics).Verdict);
        Assert.Equal((JudgmentEngine.VerdictHold, JudgmentEngine.RuleAccuracy), (JudgmentEngine.Judge(strict.Definition, metrics).Verdict, JudgmentEngine.Judge(strict.Definition, metrics).Rule));
        Assert.Equal(JudgmentEngine.VerdictHold, JudgmentEngine.Judge(unset.Definition, metrics).Verdict);
    }

    // 정의 칸 → 정의.
    private static CaseDefinition DefinitionOf(string cell) => cell switch
    {
        NoInputs => JudgmentInput.NoInputsCase(),
        NoCompare => JudgmentInput.NoCompareCase(),
        Measured or NameRow => Plain().Definition,
        WithGroup => Grouped().Definition,
        _ => throw new InvalidOperationException($"5절 표 정의 칸 '{cell}'을 모른다. 표가 바뀌었으면 장면을 더한다."),
    };

    // 대조 결과 칸 → 대조 결과. "같은 정의"는 정의 칸의 장면 그대로다. 나머지는 같은 모양의 다른 장면이다.
    private static CompareMetrics MetricsOf(string definitionCell, string cell) => cell switch
    {
        "미검증 `no_inputs`" => JudgmentInput.NotVerifiedMetrics(JudgmentInput.NoInputsCase()),
        "미검증 `no_compare_fields`" => JudgmentInput.NotVerifiedMetrics(JudgmentInput.NoCompareCase()),
        "대조함" => Plain().Metrics(),
        "대조함(같은 정의)" => definitionCell == WithGroup ? Grouped().Metrics() : Plain().Metrics(),
        "대조함, 키 열이 다름" => Run(Plain(), yaml => yaml.Replace("key: id", "key: kid", StringComparison.Ordinal), header => "k" + header),
        "대조함, `compare` 필드가 다름" => Run(Plain(), yaml => yaml.Replace("{field: v,", "{field: w,", StringComparison.Ordinal), header => header[..^1] + "w"),
        "대조함, 기준 행 머리 행에 그 열이 없음" => NoColumn().Metrics(),
        "`metrics.Case`가 `definition.Name`과 다름" => Run(Plain(), yaml => yaml.Replace("name: p\n", "name: q\n", StringComparison.Ordinal), header => header),
        _ => throw new InvalidOperationException($"5절 표 대조 결과 칸 '{cell}'을 모른다. 표가 바뀌었으면 장면을 더한다."),
    };

    // 기준 10행(grp: 앞 둘 a, 나머지 b), 앞 둘 값 불일치. inputs.group 없음. 문턱은 늘 met(0·100), 시간 off.
    private static JudgmentScene Plain() =>
        new(
            Columns: [JudgmentInput.Group],
            Rows: JudgmentInput.Rows(10, mismatch: 2, types: i => [i < 2 ? "a" : "b"]),
            Extras: [],
            AccuracyMin: "0",
            MissingMax: "100",
            TimeMustDrop: "false");

    // Plain에 inputs.group: grp. 불일치 둘이 모두 a라 found.
    private static JudgmentScene Grouped() => Plain() with { Group = JudgmentInput.Group, FocusValue = "a" };

    // Plain에서 grp 열을 뺀 장면(머리 행 id,v).
    private static JudgmentScene NoColumn() => Plain() with
    {
        Columns = [],
        Rows = JudgmentInput.Rows(10, mismatch: 2),
    };

    // 장면의 case.yaml과 두 CSV의 머리 행을 바꿔 대조한다(다른 장면).
    private static CompareMetrics Run(JudgmentScene scene, Func<string, string> yaml, Func<string, string> header)
    {
        string Csv(string csv)
        {
            var end = csv.IndexOf('\n', StringComparison.Ordinal);
            return header(csv[..end]) + csv[end..];
        }

        var definition = CompareInput.Case(yaml(scene.CaseYaml));
        Assert.NotEqual(scene.CaseYaml, yaml(scene.CaseYaml));
        return CompareInput.Measured(CompareInput.Run(definition, Csv(scene.ReferenceCsv), Csv(scene.CandidateCsv)));
    }
}
