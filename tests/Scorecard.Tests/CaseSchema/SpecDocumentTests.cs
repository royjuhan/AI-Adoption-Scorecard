using System.Text;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.CaseSchema;

// T-003 스펙 문서와 테스트 자료가 서로 맞는지 본다. Core를 쓰지 않는다.
// - 4.4 기본값 표는 지시문 3.2와 같은 키·질문·선택지·기본값이다(카드 수용 기준 1).
// - 4.9 오류 코드 표는 17개다(승인 결정 9).
// - 샘플 파일, 테스트 안 예시와 기대 오류 줄은 스펙 6절·7.3 블록과 같다.
[Trait("Spec", "case-schema")]
public class SpecDocumentTests
{
    private const string Instructions121Heading = "### 12.1 비공개 케이스는 `cases/`에 로컬로만 (커밋 금지)";

    // 지시문 3.2 순서.
    private static readonly string[] AnswerKeys =
    [
        "input_kind",
        "rule_expressible",
        "frequency",
        "batch_size",
        "exception_rate",
        "current_actor",
        "minutes_per_item",
        "failure_impact",
        "existing_feature",
        "human_signoff",
    ];

    // 12.1 예시에서 합성 샘플이 바꿔도 되는 줄(이름·문장). 나머지 줄은 같아야 한다.
    private static readonly string[] FreeTextPrefixes = ["name: ", "task: ", "before: ", "after: ", "  - name: "];

    private static readonly Dictionary<string, string> InlineSamples = new(StringComparer.Ordinal)
    {
        [nameof(SpecSamples.Minimal)] = SpecSamples.Minimal,
        [nameof(SpecSamples.AliasTag)] = SpecSamples.AliasTag,
        [nameof(SpecSamples.Duplicates)] = SpecSamples.Duplicates,
    };

    private static readonly Dictionary<string, string[]> ExpectedErrors = new(StringComparer.Ordinal)
    {
        [nameof(SpecSamples.MissingRequiredErrors)] = SpecSamples.MissingRequiredErrors,
        [nameof(SpecSamples.TypeErrorsErrors)] = SpecSamples.TypeErrorsErrors,
        [nameof(SpecSamples.AliasTagErrors)] = SpecSamples.AliasTagErrors,
        [nameof(SpecSamples.DuplicatesErrors)] = SpecSamples.DuplicatesErrors,
        [nameof(SpecSamples.GuardInvalidErrors)] = SpecSamples.GuardInvalidErrors,
    };

    [Fact]
    public void Defaults_table_lists_ten_answer_keys_in_instruction_order()
    {
        Assert.Equal(AnswerKeys, DefaultRow.ReadAll().Select(r => r.Key));
    }

    // 지시문 3.2의 기본값 칸은 "null (측정 권고로 표시)"처럼 설명이 붙는다. 첫 낱말만 비교한다.
    [Fact]
    public void Defaults_table_matches_instructions_3_2()
    {
        var instructions = SpecDocument.Table(SpecDocument.InstructionsPath, SpecDocument.InstructionsQuestionTableHeader)
            .Select(c => (Key: SpecDocument.Unquote(c[0]), Question: c[1], Choices: c[2], Default: c[3].Split(' ')[0]))
            .ToArray();
        var spec = DefaultRow.ReadAll();

        Assert.Equal(instructions.Select(i => i.Key), spec.Select(r => r.Key));
        Assert.Equal(instructions.Select(i => i.Question), spec.Select(r => r.Question));
        Assert.Equal(instructions.Select(i => i.Default), spec.Select(r => r.Default));
        Assert.All(spec.Where(r => r.IsEnum), r => Assert.Equal(instructions.Single(i => i.Key == r.Key).Choices, r.Choices));
    }

    // 열거 7개, 정수 1개, 숫자 2개. 형식 칸이 바뀌면 파서 테스트의 값 변환도 바뀌어야 한다.
    [Fact]
    public void Defaults_table_kinds_are_enum_integer_or_number()
    {
        Assert.Equal(
            ["열거", "열거", "열거", "정수", "숫자", "열거", "숫자", "열거", "열거", "열거"],
            DefaultRow.ReadAll().Select(r => r.Kind));
    }

    [Fact]
    public void Error_code_table_lists_seventeen_codes()
    {
        var codes = SpecDocument.Table(SpecDocument.CaseSchemaPath, SpecDocument.ErrorCodeTableHeader)
            .Select(c => SpecDocument.Unquote(c[0]));

        Assert.Equal(SpecSamples.ErrorCodes, codes);
    }

    [Theory]
    [InlineData("### 6.1 정상 (전체)", 0, "case-schema/valid-full")]
    [InlineData("### 6.3 필드 누락", 0, "case-schema/missing-required")]
    [InlineData("### 6.4 형식 오류", 0, "case-schema/type-errors")]
    [InlineData("### 7.3 가드 테스트 변경 (tests/Guards)", 0, "guard/case-invalid")]
    public void Sample_file_matches_spec_block(string heading, int block, string sample)
    {
        var expected = Encoding.UTF8.GetBytes(SpecDocument.CodeBlocks(heading)[block]);

        Assert.Equal(expected, File.ReadAllBytes(SampleFile(sample)));
    }

    // 스펙 7.3: guard/case-valid는 6.1과 같다.
    [Fact]
    public void Guard_valid_sample_equals_full_valid_sample()
    {
        Assert.Equal(File.ReadAllBytes(SampleFile("case-schema/valid-full")), File.ReadAllBytes(SampleFile("guard/case-valid")));
    }

    [Theory]
    [InlineData("### 6.2 최소", 0, nameof(SpecSamples.Minimal))]
    [InlineData("### 6.5 YAML 기능·중복", 0, nameof(SpecSamples.AliasTag))]
    [InlineData("### 6.5 YAML 기능·중복", 2, nameof(SpecSamples.Duplicates))]
    public void Inline_sample_matches_spec_block(string heading, int block, string name)
    {
        Assert.Equal(SpecDocument.CodeBlocks(heading)[block], InlineSamples[name]);
    }

    [Theory]
    [InlineData("### 6.3 필드 누락", 1, nameof(SpecSamples.MissingRequiredErrors))]
    [InlineData("### 6.4 형식 오류", 1, nameof(SpecSamples.TypeErrorsErrors))]
    [InlineData("### 6.5 YAML 기능·중복", 1, nameof(SpecSamples.AliasTagErrors))]
    [InlineData("### 6.5 YAML 기능·중복", 3, nameof(SpecSamples.DuplicatesErrors))]
    [InlineData("### 7.3 가드 테스트 변경 (tests/Guards)", 1, nameof(SpecSamples.GuardInvalidErrors))]
    public void Expected_errors_match_spec_block(string heading, int block, string name)
    {
        Assert.Equal(SpecDocument.BlockLines(SpecDocument.CodeBlocks(heading)[block]), ExpectedErrors[name]);
    }

    // 스펙 6.6: 12.1 예시와 키·단계 수·답이 같고 이름·문장만 합성이다. 첫 줄 주석은 뺀다.
    [Fact]
    public void Example_12_1_sample_differs_from_instructions_only_in_names_and_sentences()
    {
        var instructions = SpecDocument.BlockLines(SpecDocument.CodeBlocks(SpecDocument.InstructionsPath, Instructions121Heading)[0]);
        var sample = SpecDocument.BlockLines(File.ReadAllText(SampleFile("case-schema/" + SpecSamples.Example121)))
            .Where(l => !l.StartsWith('#'))
            .ToArray();

        Assert.Equal(instructions.Length, sample.Length);
        for (var i = 0; i < instructions.Length; i++)
        {
            var prefix = FreeTextPrefixes.FirstOrDefault(p => instructions[i].StartsWith(p, StringComparison.Ordinal));
            if (prefix is null)
            {
                Assert.Equal(instructions[i], sample[i]);
            }
            else
            {
                Assert.StartsWith(prefix, sample[i], StringComparison.Ordinal);
            }
        }
    }

    // 4.9 모으기·순서의 syntax 다른 형태 글머리. 문구 틀의 <예외 타입 이름>에 글머리 예시의 타입 이름을 넣고,
    // 위치 없음(0:0)·경로 없음의 ToString 규칙("case.yaml: syntax: ")을 앞에 붙이면 SpecSamples의 오류 줄이다.
    [Fact]
    public void Parser_fault_error_matches_spec_bullet()
    {
        const string placeholder = "<예외 타입 이름>";
        var bullet = SpecDocument.Line(SpecDocument.CaseSchemaPath, "- `syntax`의 다른 형태");
        var spans = SpecDocument.CodeSpans(bullet);
        var template = Assert.Single(spans, s => s.EndsWith(": " + placeholder, StringComparison.Ordinal));

        Assert.Contains("위치 없이(0:0)", bullet, StringComparison.Ordinal);
        Assert.Contains("U+2028", bullet, StringComparison.Ordinal);
        Assert.Contains('\u2028', SpecSamples.ParserFault);
        Assert.Contains(SpecSamples.ParserFaultTypeName, spans);
        Assert.Equal(
            SpecSamples.ParserFaultError,
            "case.yaml: syntax: " + template.Replace(placeholder, SpecSamples.ParserFaultTypeName, StringComparison.Ordinal));
    }

    private static string SampleFile(string sample) => RepoRoot.Combine($"tests/Scorecard.Tests/TestData/{sample}/case.yaml");
}
