using Scorecard.Core.Cases;

namespace Scorecard.Tests.CaseSchema;

// 스펙 4.12 Core 공개 API, 4.9 오류 문자열 형식.
// 이 파일은 컴파일 자체가 API 모양 검사다. 타입·이름·매개변수 순서가 다르면 컴파일되지 않는다.
// test-engineer 단계에서는 Core에 API가 없어 컴파일이 실패한다(승인 결정 14).
[Trait("Spec", "case-schema")]
public class CaseApiShapeTests
{
    private const string CasesNamespace = "Scorecard.Core.Cases";

    // 4.12의 공개 타입 전부. 트리·검사 코드는 internal이어야 한다.
    private static readonly string[] PublicTypes =
    [
        // T-014(text-check.md)
        "Answer`1",
        "CaseChecklist",
        "CaseDefinition",
        "CaseError",
        "CaseInputs",
        "CaseParseResult",
        "CaseParser",
        "CaseScores",
        "CaseStep",
        "CaseThresholds",
        "ChecklistItem",
        "CompareField",
        "StepAnswers",
    ];

    [Fact]
    public void File_name_is_case_yaml_constant()
    {
        const string fileName = CaseParser.FileName;

        Assert.Equal("case.yaml", fileName);
    }

    // 공개 메서드 두 개의 서명.
    [Fact]
    public void Parser_entry_points_have_spec_signatures()
    {
        Func<string, CaseParseResult> parseDirectory = CaseParser.ParseDirectory;
        Func<byte[], string, CaseParseResult> parse = CaseParser.Parse;

        Assert.NotNull(parseDirectory);
        Assert.NotNull(parse);
    }

    // 모델 레코드의 위치 매개변수 순서와 형식(열거는 문자열, 숫자는 decimal).
    [Fact]
    public void Model_records_have_spec_shape()
    {
        var answers = new StepAnswers(
            InputKind: new Answer<string>("fixed_form", true),
            RuleExpressible: new Answer<string>("partial", true),
            Frequency: new Answer<string>("weekly", true),
            BatchSize: new Answer<int>(10, true),
            ExceptionRate: new Answer<decimal>(20m, true),
            CurrentActor: new Answer<string>("human", true),
            MinutesPerItem: new Answer<decimal?>(null, true),
            FailureImpact: new Answer<string>("internal_rework", true),
            ExistingFeature: new Answer<string>("unknown", true),
            HumanSignoff: new Answer<string>("no", true));
        var step = new CaseStep(Number: 1, Name: "s", Answers: answers);
        var definition = new CaseDefinition(
            Name: "a",
            Task: "t",
            Before: null,
            After: null,
            AiUsage: null,
            Steps: [step],
            Inputs: new CaseInputs(Reference: "r/*.csv", Candidate: "c/*.csv", Key: "k"),
            Compare: [new CompareField(Field: "x", Tolerance: 0m, TolerancePct: null)],
            Thresholds: new CaseThresholds(AccuracyMin: 90m, MissingMax: null, TimeMustDrop: true));
        var result = new CaseParseResult(Case: definition, Errors: []);

        Assert.True(result.IsValid);
        Assert.Same(definition, result.Case);
        Assert.Equal(1, Assert.Single(definition.Steps).Number);
        Assert.Equal(10, definition.Steps[0].Answers.BatchSize.Value);
        Assert.True(definition.Steps[0].Answers.BatchSize.IsDefault);
        Assert.Null(definition.Steps[0].Answers.MinutesPerItem.Value);
        Assert.Equal("x", Assert.Single(definition.Compare).Field);
        Assert.Equal("k", definition.Inputs?.Key);
        Assert.Null(definition.Inputs?.Group);
        Assert.True(definition.Thresholds.TimeMustDrop);
    }

    // T-012(10.3): CaseInputs 넷째 위치 매개변수 Group(string?, 기본값 null). 세 인자 생성은 그대로 컴파일되고 Group = null이다.
    // 레코드 같음 비교에 Group이 든다.
    [Fact]
    public void Case_inputs_has_optional_group()
    {
        var withGroup = new CaseInputs(Reference: "r/*.csv", Candidate: "c/*.csv", Key: "k", Group: "g");
        var parameters = typeof(CaseInputs).GetConstructor([typeof(string), typeof(string), typeof(string), typeof(string)])!.GetParameters();

        Assert.Equal(["Reference", "Candidate", "Key", "Group"], parameters.Select(p => p.Name));
        Assert.True(parameters[3].HasDefaultValue);
        Assert.Null(parameters[3].DefaultValue);
        Assert.Equal("g", withGroup.Group);
        Assert.Equal(new CaseInputs("r/*.csv", "c/*.csv", "k", null), new CaseInputs("r/*.csv", "c/*.csv", "k"));
        Assert.NotEqual(withGroup, new CaseInputs("r/*.csv", "c/*.csv", "k"));
        Assert.Equal(withGroup, new CaseInputs("r/*.csv", "c/*.csv", "k") with { Group = "g" });
    }

    // 레코드라서 값으로 비교된다.
    [Fact]
    public void Answer_and_error_are_value_records()
    {
        Assert.Equal(new Answer<int>(10, true), new Answer<int>(10, true));
        Assert.NotEqual(new Answer<int>(10, true), new Answer<int>(10, false));
        Assert.Equal(
            new CaseError("case.yaml", 1, 2, "type", "steps", "목록이어야 한다"),
            new CaseError("case.yaml", 1, 2, "type", "steps", "목록이어야 한다"));
    }

    [Fact]
    public void Result_with_errors_is_not_valid()
    {
        var error = new CaseError(Source: "case.yaml", Line: 1, Column: 1, Code: "required", Path: "name", Message: "필수 필드가 없다");
        var result = new CaseParseResult(null, [error]);

        Assert.False(result.IsValid);
        Assert.Same(error, Assert.Single(result.Errors));
    }

    // 4.9 문자열 형식: <Source>:<Line>:<Column>: <Code> <Path>: <Message>
    // 열이 0이면 <Source>:<Line>:, 줄도 0이면 <Source>:. 경로가 비면 <Code>: <Message>.
    [Theory]
    [InlineData(3, 8, "required", "steps", "단계가 1개 이상 있어야 한다", "case.yaml:3:8: required steps: 단계가 1개 이상 있어야 한다")]
    [InlineData(1, 1, "type", "", "매핑이어야 한다", "case.yaml:1:1: type: 매핑이어야 한다")]
    [InlineData(3, 0, "encoding", "", "UTF-8이 아니다 (바이트 70)", "case.yaml:3: encoding: UTF-8이 아니다 (바이트 70)")]
    [InlineData(0, 0, "file", "", "파일이 없다", "case.yaml: file: 파일이 없다")]
    [InlineData(0, 0, "empty", "", "내용이 없다", "case.yaml: empty: 내용이 없다")]
    [InlineData(10, 19, "tag", "steps[2].answers.batch_size", "태그(!)는 쓰지 않는다", "case.yaml:10:19: tag steps[2].answers.batch_size: 태그(!)는 쓰지 않는다")]
    public void Error_string_follows_spec_format(int line, int column, string code, string path, string message, string expected)
    {
        Assert.Equal(expected, new CaseError("case.yaml", line, column, code, path, message).ToString());
    }

    // Source는 넘긴 이름 그대로다(상대 이름).
    [Fact]
    public void Error_string_uses_given_source_name()
    {
        Assert.Equal(
            "sub/other.yaml:2:7: tag task: 태그(!)는 쓰지 않는다",
            new CaseError("sub/other.yaml", 2, 7, "tag", "task", "태그(!)는 쓰지 않는다").ToString());
    }

    // 4.12: 공개 타입은 위 목록뿐이고, 공개 API에 YamlDotNet 타입이 나오지 않는다.
    [Fact]
    public void Cases_namespace_exports_only_spec_types()
    {
        var exported = typeof(CaseParser).Assembly.GetExportedTypes()
            .Where(t => t.Namespace == CasesNamespace)
            .Select(t => t.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(PublicTypes, exported);
    }

    [Fact]
    public void Public_api_does_not_expose_YamlDotNet_types()
    {
        var leaks = typeof(CaseParser).Assembly.GetExportedTypes()
            .SelectMany(t => PublicSignatureTypes(t).Select(s => $"{t.FullName}: {s.FullName}"))
            .Where(s => s.Contains(": YamlDotNet", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(leaks);
    }

    // 공개 멤버 서명에 나오는 타입(기반 타입, 인터페이스, 속성·필드·메서드·생성자 매개변수와 반환형). 제네릭 인수도 편다.
    private static IEnumerable<Type> PublicSignatureTypes(Type type)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.DeclaredOnly;

        var direct = new List<Type>();
        if (type.BaseType is not null)
        {
            direct.Add(type.BaseType);
        }

        direct.AddRange(type.GetInterfaces());
        direct.AddRange(type.GetProperties(flags).Select(p => p.PropertyType));
        direct.AddRange(type.GetFields(flags).Select(f => f.FieldType));
        foreach (var method in type.GetMethods(flags))
        {
            direct.Add(method.ReturnType);
            direct.AddRange(method.GetParameters().Select(p => p.ParameterType));
        }

        foreach (var constructor in type.GetConstructors(flags))
        {
            direct.AddRange(constructor.GetParameters().Select(p => p.ParameterType));
        }

        return direct.SelectMany(Expand);
    }

    private static IEnumerable<Type> Expand(Type type)
    {
        var element = type.HasElementType ? type.GetElementType()! : type;
        yield return element;
        if (element.IsGenericType)
        {
            foreach (var argument in element.GetGenericArguments().SelectMany(Expand))
            {
                yield return argument;
            }
        }
    }
}
