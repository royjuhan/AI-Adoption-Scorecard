using System.Reflection;
using System.Text.RegularExpressions;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Comparison;

// 스펙 4.11 Core 공개 API(승인 질문 19)와 5절 예외.
// 이 파일은 컴파일 자체가 API 모양 검사다. 타입·이름·매개변수 순서가 다르면 컴파일되지 않는다.
// test-engineer 단계에서는 Core에 Scorecard.Core.Comparison이 없어 컴파일이 실패한다(승인 질문 19).
[Trait("Spec", "metrics")]
public partial class CompareApiShapeTests
{
    private const string ComparisonNamespace = "Scorecard.Core.Comparison";
    private const string ApiHeading = "### 4.11 Core 공개 API (제안)";

    // 4.11의 공개 타입 전부: 정적 클래스 3개 + 레코드 10개. 규칙·CSV 읽기는 internal이다(4.9).
    private static readonly string[] PublicTypes =
    [
        "CompareCounts",
        "CompareEngine",
        "CompareInputs",
        "CompareItem",
        "CompareMetrics",
        "CompareResult",
        "CompareSources",
        "CompareSourcesResult",
        "CsvRow",
        "FieldDifference",
        "FieldSummary",
        "MetricsJson",
        "SourceFile",
    ];

    // 4.11 블록의 "public const string X = \"y\";"·"public const int X = n;" 줄 = Core 상수. 이름·값·개수가 같다.
    [Fact]
    public void Constants_equal_spec_api_block()
    {
        var block = Assert.Single(SpecDocument.CodeBlocks(MetricsSpec.Document, ApiHeading));
        var spec = ConstLine().Matches(block)
            .Select(m => (Owner: OwnerOf(block, m.Index), Name: m.Groups["name"].Value, Value: m.Groups["value"].Value.Trim('"')))
            .ToArray();
        Assert.Equal(15, spec.Length);

        foreach (var owner in new[] { typeof(CompareEngine), typeof(MetricsJson) })
        {
            var core = owner.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral)
                .Select(f => (f.Name, Value: Convert.ToString(f.GetRawConstantValue(), System.Globalization.CultureInfo.InvariantCulture)!))
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .ToArray();
            var expected = spec.Where(s => s.Owner == owner.Name)
                .Select(s => (s.Name, s.Value))
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(expected, core);
        }
    }

    // 상수 값. 3.2 속성 표·4.4 미검증 표·4.5와 같은 글자다(컴파일 시점에도 이름이 있어야 한다).
    [Fact]
    public void Constants_have_spec_values()
    {
        Assert.Equal(
            ["measured", "not_verified", "no_inputs", "no_compare_fields", "match", "mismatch", "missing", "extra", "number", "text", "within", "outside", "not_number"],
            [
                CompareEngine.StatusMeasured, CompareEngine.StatusNotVerified, CompareEngine.ReasonNoInputs, CompareEngine.ReasonNoCompareFields,
                CompareEngine.ItemMatch, CompareEngine.ItemMismatch, CompareEngine.ItemMissing, CompareEngine.ItemExtra,
                CompareEngine.KindNumber, CompareEngine.KindText, CompareEngine.ResultWithin, CompareEngine.ResultOutside, CompareEngine.ResultNotNumber,
            ]);
        Assert.Equal("metrics.json", MetricsJson.FileName);
        Assert.Equal(1, MetricsJson.SchemaVersion);
    }

    // 공개 메서드 여섯 개의 서명.
    [Fact]
    public void Entry_points_have_spec_signatures()
    {
        Func<string, CaseDefinition, CompareResult> compareDirectory = CompareEngine.CompareDirectory;
        Func<CaseDefinition, CompareSources, CompareResult> compare = CompareEngine.Compare;
        Func<string, CaseInputs, CompareSourcesResult> read = CompareInputs.Read;
        Func<string, bool> isSupported = CompareInputs.IsSupportedPattern;
        Func<string, string, bool> matches = CompareInputs.Matches;
        Func<CompareMetrics, byte[]> render = MetricsJson.Render;

        Assert.All(new Delegate[] { compareDirectory, compare, read, isSupported, matches, render }, d => Assert.NotNull(d));
    }

    // 레코드 10개의 위치 매개변수 순서와 형식. IsValid는 오류가 없을 때 참이다.
    [Fact]
    public void Records_have_spec_shape()
    {
        var file = new SourceFile(Path: "reference/a.csv", Content: [1, 2]);
        var sources = new CompareSources(Reference: [file], Candidate: []);
        var error = new CaseError("reference/a.csv", 1, 0, "empty", string.Empty, "m");
        var row = new CsvRow(File: "reference/a.csv", Line: 2, Header: ["id", "v"], Cells: ["k", "1"]);
        var difference = new FieldDifference(Field: "v", Result: "within", Reference: "1", Candidate: "1.01");
        var item = new CompareItem(Key: "k", Status: "match", Reference: row, Candidate: row, Differences: [difference]);
        var counts = new CompareCounts(Reference: 1, Candidate: 1, Match: 1, Mismatch: 0, Missing: 0, Extra: 0, Within: 1);
        var field = new FieldSummary(Field: "v", Kind: "number", Tolerance: null, TolerancePct: 1.0m, Compared: 1, Outside: 0, Within: 1, NotNumber: 0);
        var metrics = new CompareMetrics(
            Case: "p",
            Status: "measured",
            Reason: null,
            Key: "id",
            ReferenceFiles: ["reference/a.csv"],
            CandidateFiles: ["candidate/a.csv"],
            Counts: counts,
            AccuracyPct: 100.0m,
            MissingRatePct: 0.0m,
            Fields: [field],
            Items: [item]);

        Assert.True(new CompareSourcesResult(Sources: sources, Errors: []).IsValid);
        Assert.False(new CompareSourcesResult(Sources: null, Errors: [error]).IsValid);
        Assert.True(new CompareResult(Metrics: metrics, Errors: []).IsValid);
        Assert.False(new CompareResult(Metrics: null, Errors: [error]).IsValid);
        Assert.Same(file, Assert.Single(sources.Reference));
        Assert.Same(difference, Assert.Single(Assert.Single(metrics.Items).Differences));
        Assert.Equal(new CompareCounts(1, 1, 1, 0, 0, 0, 1), metrics.Counts);
    }

    [Fact]
    public void Comparison_namespace_exports_only_spec_types()
    {
        var exported = typeof(CompareEngine).Assembly.GetExportedTypes()
            .Where(t => t.Namespace == ComparisonNamespace)
            .Select(t => t.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(PublicTypes, exported);
    }

    // 5절: definition·sources·inputs가 null이면 ArgumentNullException. caseDirectory가 null·빈 문자열이면 ArgumentException.
    [Fact]
    public void Null_and_empty_arguments_throw()
    {
        var definition = CompareInput.Case(CompareInput.TextCase);
        var sources = new CompareSources([], []);

        Assert.Throws<ArgumentNullException>(() => CompareEngine.Compare(null!, sources));
        Assert.Throws<ArgumentNullException>(() => CompareEngine.Compare(definition, null!));
        Assert.Throws<ArgumentNullException>(() => CompareEngine.CompareDirectory("case", null!));
        Assert.Throws<ArgumentNullException>(() => CompareInputs.Read("case", null!));
        Assert.ThrowsAny<ArgumentException>(() => CompareEngine.CompareDirectory(null!, definition));
        Assert.ThrowsAny<ArgumentException>(() => CompareEngine.CompareDirectory(string.Empty, definition));
        Assert.ThrowsAny<ArgumentException>(() => CompareInputs.Read(null!, definition.Inputs!));
        Assert.ThrowsAny<ArgumentException>(() => CompareInputs.Read(string.Empty, definition.Inputs!));
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
