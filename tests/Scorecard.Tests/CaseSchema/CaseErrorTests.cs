using Scorecard.Core.Cases;

namespace Scorecard.Tests.CaseSchema;

// 스펙 4.3·4.9·5·6.3~6.5·7.3: 오류 코드, 위치, 경로, 문구, 모으기·순서.
// 문구는 문자열로 비교한다(승인 결정 9). 문구를 바꾸면 스펙과 테스트를 같이 고친다.
[Trait("Spec", "case-schema")]
public class CaseErrorTests
{
    // 4.9 표의 코드마다 최소 예시 하나와 그 오류 줄 전체. file은 폴더 입력이라 따로 만든다(CaseDirectoryTests도 본다).
    private static readonly Dictionary<string, (Func<CaseParseResult> Run, string[] Expected)> CodeExamples = new(StringComparer.Ordinal)
    {
        ["file"] = (ParseEmptyDirectory, ["case.yaml: file: 파일이 없다"]),
        ["encoding"] = (
            () => CaseInput.Parse([0xFF, 0xFE, .. System.Text.Encoding.Unicode.GetBytes(SpecSamples.Minimal)]),
            ["case.yaml:1: encoding: UTF-16이다. UTF-8로 저장한다"]),
        ["empty"] = (() => CaseInput.Parse(string.Empty), ["case.yaml: empty: 내용이 없다"]),
        ["syntax"] = (
            () => CaseInput.Parse("name: sample-syntax\ntask: 문법 오류\nsteps: [\n  {name: a\n"),
            ["case.yaml:5:1: syntax: YAML 문법 오류: While parsing a flow mapping, did not find expected ',' or '}'."]),
        ["depth"] = (
            () => CaseInput.Parse("name: a\ntask: t\nsteps: " + new string('[', 20) + new string(']', 20) + "\n"),
            ["case.yaml:3:23: depth steps" + string.Concat(Enumerable.Repeat("[1]", 15)) + ": 너무 깊다. 16단계까지 쓴다"]),
        ["multi-document"] = (
            () => CaseInput.Parse("name: sample-multi\ntask: 문서 둘\nsteps:\n  - name: a\n---\nname: sample-multi-2\n"),
            ["case.yaml:5:1: multi-document: 문서가 둘 이상이다. 하나만 쓴다"]),
        ["alias"] = (
            () => CaseInput.Parse("name: a\ntask: &t t\nsteps:\n  - name: s\n"),
            ["case.yaml:2:7: alias task: 앵커(&)는 쓰지 않는다"]),
        ["tag"] = (
            () => CaseInput.Parse("name: a\ntask: !x t\nsteps:\n  - name: s\n"),
            ["case.yaml:2:7: tag task: 태그(!)는 쓰지 않는다"]),
        ["duplicate-key"] = (
            () => CaseInput.Parse("name: a\ntask: t\nname: b\nsteps:\n  - name: s\n"),
            ["case.yaml:3:1: duplicate-key name: 중복된 키다 (처음: 1줄)"]),
        ["type"] = (() => CaseInput.Parse("- name: a\n- name: b\n"), ["case.yaml:1:1: type: 매핑이어야 한다"]),
        ["unknown-field"] = (
            () => CaseInput.Parse("name: a\ntask: t\nowner: x\nsteps:\n  - name: s\n"),
            ["case.yaml:3:1: unknown-field owner: 알 수 없는 필드다"]),
        ["required"] = (() => CaseInput.Parse("task: t\nsteps:\n  - name: s\n"), ["case.yaml:1:1: required name: 필수 필드가 없다"]),
        ["enum"] = (
            () => CaseInput.Parse("name: a\ntask: t\nai_usage: always\nsteps:\n  - name: s\n"),
            ["case.yaml:3:11: enum ai_usage: 선택지 밖의 값 'always'. 가능한 값: build-time, runtime, hybrid"]),
        ["format"] = (
            () => CaseInput.Parse("name: Sample_Bad\ntask: t\nsteps:\n  - name: s\n"),
            ["case.yaml:1:7: format name: 소문자·숫자·'-'만, 64자까지 쓴다"]),
        ["range"] = (
            () => CaseInput.Parse("name: a\ntask: t\nsteps:\n  - name: s\n    answers: {batch_size: 0}\n"),
            ["case.yaml:5:27: range steps[1].answers.batch_size: 범위 밖이다 (1 이상 2147483647 이하): 0"]),
        ["duplicate"] = (
            () => CaseInput.Parse("name: a\ntask: t\nsteps:\n  - name: s\n  - name: s\n"),
            ["case.yaml:5:11: duplicate steps[2].name: 단계 이름이 중복된다 (처음: steps[1])"]),
        ["conflict"] = (
            () => CaseInput.Parse(
                "name: a\ntask: t\nsteps:\n  - name: s\ninputs: {reference: r.csv, candidate: c.csv, key: k}\n"
                + "compare:\n  - {field: x, tolerance: 1, tolerance_pct: 2}\n"),
            ["case.yaml:7:5: conflict compare[1]: tolerance와 tolerance_pct 중 하나만 쓴다"]),
    };

    public static TheoryData<string> Codes => new(SpecSamples.ErrorCodes);

    // 코드 17개마다 예시가 있고, 예시는 그 코드의 오류 줄을 정확히 낸다.
    [Theory]
    [MemberData(nameof(Codes))]
    public void Each_error_code_has_spec_message_and_position(string code)
    {
        Assert.True(CodeExamples.ContainsKey(code), $"코드 {code}의 예시가 없다.");
        var (run, expected) = CodeExamples[code];

        var result = run();

        CaseInput.AssertErrors(result, expected);
        Assert.All(result.Errors, e => Assert.Equal(code, e.Code));
    }

    [Fact]
    public void Code_examples_cover_exactly_the_spec_codes()
    {
        Assert.Equal(SpecSamples.ErrorCodes.Order(StringComparer.Ordinal), CodeExamples.Keys.Order(StringComparer.Ordinal));
    }

    // 6.3 필드 누락. 카드 수용 기준 2: 어떤 필드가 빠졌는지 알려 준다.
    [Fact]
    public void Missing_required_sample_reports_each_missing_field()
    {
        CaseInput.AssertErrors(CaseInput.Parse(CaseInput.Sample(SpecSamples.MissingRequired)), SpecSamples.MissingRequiredErrors);
    }

    // 6.4 형식 오류 18건. 첫 오류에서 멈추지 않고 모두 모은다.
    [Fact]
    public void Type_errors_sample_collects_all_eighteen_errors()
    {
        CaseInput.AssertErrors(CaseInput.Parse(CaseInput.Sample(SpecSamples.TypeErrors)), SpecSamples.TypeErrorsErrors);
    }

    // 6.5 alias-tag: 태그, 앵커, 별칭, 병합 키(<<)는 일반 키라서 unknown-field.
    [Fact]
    public void Alias_tag_sample_reports_tags_anchors_aliases_and_merge_key()
    {
        CaseInput.AssertErrors(SpecSamples.AliasTag, SpecSamples.AliasTagErrors);
    }

    // 6.5 duplicates: 같은 매핑의 같은 키, 단계 이름 중복, 비교 필드 중복.
    [Fact]
    public void Duplicates_sample_reports_duplicate_keys_and_names()
    {
        CaseInput.AssertErrors(SpecSamples.Duplicates, SpecSamples.DuplicatesErrors);
    }

    // 7.3 G2·G3 오류 표본. 첫 줄이 주석이라 루트 매핑은 2줄에서 시작한다.
    [Fact]
    public void Guard_invalid_sample_reports_seven_errors()
    {
        var bytes = File.ReadAllBytes(Structure.RepoRoot.Combine("tests/Scorecard.Tests/TestData/guard/case-invalid/case.yaml"));

        CaseInput.AssertErrors(CaseInput.Parse(bytes), SpecSamples.GuardInvalidErrors);
    }

    // 4.3 알 수 없는 키. 매핑마다 본다. 키 비교는 서수(대소문자 구분)다.
    [Theory]
    [InlineData("name: a\ntask: t\nsteps:\n  - name: s\n    note: x\n", "case.yaml:5:5: unknown-field steps[1].note: 알 수 없는 필드다")]
    [InlineData("name: a\ntask: t\nsteps:\n  - name: s\n    answers:\n      frequncy: weekly\n", "case.yaml:6:7: unknown-field steps[1].answers.frequncy: 알 수 없는 필드다")]
    [InlineData("name: a\ntask: t\nsteps:\n  - name: s\ninputs: {reference: r.csv, candidate: c.csv, key: k, extra: x}\n", "case.yaml:5:54: unknown-field inputs.extra: 알 수 없는 필드다")]
    [InlineData("name: a\ntask: t\nsteps:\n  - name: s\ninputs: {reference: r.csv, candidate: c.csv, key: k}\ncompare:\n  - {field: x, tol: 1}\n", "case.yaml:7:16: unknown-field compare[1].tol: 알 수 없는 필드다")]
    [InlineData("name: a\ntask: t\nsteps:\n  - name: s\nthresholds:\n  accuracy: 90\n", "case.yaml:6:3: unknown-field thresholds.accuracy: 알 수 없는 필드다")]
    [InlineData("name: a\ntask: t\nTask: u\nsteps:\n  - name: s\n", "case.yaml:3:1: unknown-field Task: 알 수 없는 필드다")]
    public void Unknown_key_is_reported_at_key_with_full_path(string yaml, string expected)
    {
        CaseInput.AssertErrors(yaml, expected);
    }

    // 4.9 순서: 부모 매핑 하나에 필수 필드가 여럿 없으면 4.3 표 순서(name, task, steps)로 나온다.
    [Fact]
    public void Missing_fields_at_same_position_follow_table_order()
    {
        CaseInput.AssertErrors(
            "ai_usage: runtime\n",
            "case.yaml:1:1: required name: 필수 필드가 없다",
            "case.yaml:1:1: required task: 필수 필드가 없다",
            "case.yaml:1:1: required steps: 필수 필드가 없다");
    }

    // 4.9 순서: 찾는 순서와 상관없이 (줄, 열) 오름차순이다.
    [Fact]
    public void Errors_are_sorted_by_line_then_column()
    {
        CaseInput.AssertErrors(
            "steps:\n  - name: s\n    answers: {batch_size: 0, frequency: Weekly}\nname: Bad\ntask: t\n",
            "case.yaml:3:27: range steps[1].answers.batch_size: 범위 밖이다 (1 이상 2147483647 이하): 0",
            "case.yaml:3:41: enum steps[1].answers.frequency: 선택지 밖의 값 'Weekly'. 가능한 값: daily, weekly, monthly, rare",
            "case.yaml:4:7: format name: 소문자·숫자·'-'만, 64자까지 쓴다");
    }

    // 4.9: file·encoding·empty·syntax·depth는 그 한 건만 낸다. 다른 검사를 하지 않는다.
    [Theory]
    [InlineData("name: Bad_Name\ntask: !x t\nsteps: [\n", "syntax")]
    [InlineData("name: Bad_Name\ntask: !x t\nowner: x\nsteps: [[[[[[[[[[[[[[[[[[[[]]]]]]]]]]]]]]]]]]]]\n", "depth")]
    public void Fatal_error_is_the_only_error(string yaml, string code)
    {
        var result = CaseInput.Parse(yaml);

        Assert.Equal(code, Assert.Single(result.Errors).Code);
        Assert.Null(result.Case);
    }

    // 4.3·4.9 required: 필수 문자열이 비면(빈 값, ~, "", 공백뿐) "값이 비어 있다", 위치는 그 값.
    [Theory]
    [InlineData("name: a\ntask: t\nsteps:\n  - name: \"  \"\n", "case.yaml:4:11: required steps[1].name: 값이 비어 있다")]
    [InlineData("name: a\ntask: t\nsteps:\n  - name: ~\n", "case.yaml:4:11: required steps[1].name: 값이 비어 있다")]
    [InlineData("name: a\ntask: t\nsteps:\n  - name: \"\"\n", "case.yaml:4:11: required steps[1].name: 값이 비어 있다")]
    [InlineData("name: a\ntask: \"\"\nsteps:\n  - name: s\n", "case.yaml:2:7: required task: 값이 비어 있다")]
    [InlineData("name: a\ntask: ~\nsteps:\n  - name: s\n", "case.yaml:2:7: required task: 값이 비어 있다")]
    [InlineData("name: ''\ntask: t\nsteps:\n  - name: s\n", "case.yaml:1:7: required name: 값이 비어 있다")]
    [InlineData("name: a\ntask: t\nsteps: ~\n", "case.yaml:3:8: required steps: 값이 비어 있다")]
    public void Empty_required_value_is_reported_at_value(string yaml, string expected)
    {
        CaseInput.AssertErrors(yaml, expected);
    }

    // 4.9 required: 필드가 없으면 위치는 부모 매핑 시작.
    [Theory]
    [InlineData("name: a\nsteps:\n  - name: s\n", "case.yaml:1:1: required task: 필수 필드가 없다")]
    [InlineData("name: a\ntask: t\n", "case.yaml:1:1: required steps: 필수 필드가 없다")]
    [InlineData("name: a\ntask: t\nsteps:\n  - answers: {}\n", "case.yaml:4:5: required steps[1].name: 필수 필드가 없다")]
    [InlineData("# 주석\n\nname: a\nsteps:\n  - name: s\n", "case.yaml:3:1: required task: 필수 필드가 없다")]
    public void Missing_required_field_is_reported_at_parent_mapping(string yaml, string expected)
    {
        CaseInput.AssertErrors(yaml, expected);
    }

    // 5절 예외 표의 구조 오류.
    [Theory]
    [InlineData("name: a\ntask: t\nsteps: []\n", "case.yaml:3:8: required steps: 단계가 1개 이상 있어야 한다")]
    [InlineData("name: a\ntask: t\nsteps:\n  name: s\n", "case.yaml:4:3: type steps: 목록이어야 한다")]
    [InlineData("name: a\ntask: t\nsteps: 2\n", "case.yaml:3:8: type steps: 목록이어야 한다")]
    [InlineData("name: a\ntask: t\nsteps:\n  - 첫 단계\n", "case.yaml:4:5: type steps[1]: 매핑이어야 한다")]
    [InlineData("name: a\ntask: t\nsteps:\n  - name: s\n    answers: []\n", "case.yaml:5:14: type steps[1].answers: 매핑이어야 한다")]
    [InlineData("name: a\ntask: [t]\nsteps:\n  - name: s\n", "case.yaml:2:7: type task: 스칼라 값이어야 한다")]
    [InlineData("name: a\ntask: t\nbefore: {x: 1}\nsteps:\n  - name: s\n", "case.yaml:3:9: type before: 스칼라 값이어야 한다")]
    [InlineData("name: a\ntask: t\nsteps:\n  - name: [s]\n", "case.yaml:4:11: type steps[1].name: 스칼라 값이어야 한다")]
    [InlineData("hello\n", "case.yaml:1:1: type: 매핑이어야 한다")]
    public void Structure_error_reports_expected_shape(string yaml, string expected)
    {
        CaseInput.AssertErrors(yaml, expected);
    }

    // 5절: 탭 들여쓰기는 syntax. 위치가 탭 자리가 아니라 문서 앞(1:1)이다.
    [Theory]
    [InlineData("name: a\ntask: t\nsteps:\n\t- name: s\n")]
    [InlineData("name: a\ntask: t\nsteps:\n  - name: s\n    answers:\n\t  batch_size: 1\n")]
    public void Tab_indentation_is_syntax_error_at_document_start(string yaml)
    {
        CaseInput.AssertErrors(yaml, "case.yaml:1:1: syntax: YAML 문법 오류: While scanning a mapping, found invalid tab as indentation.");
    }

    // 4.9 syntax의 다른 형태: 파서가 YamlException이 아닌 예외를 내면 위치 없이(0:0) syntax 한 건이다.
    // 예외 메시지는 쓰지 않고 타입 이름만 쓴다. 예외가 Parse 밖으로 나가지 않는다.
    // 다른 검사도 하지 않는다(x는 알 수 없는 필드, name·task·steps가 없지만 내지 않는다).
    [Fact]
    public void Parser_fault_other_than_yaml_exception_is_single_syntax_error_without_position()
    {
        CaseParseResult? result = null;

        var thrown = Record.Exception(() => result = CaseInput.Parse(SpecSamples.ParserFault));

        Assert.Null(thrown);
        Assert.NotNull(result);
        CaseInput.AssertErrors(result, SpecSamples.ParserFaultError);
        var error = Assert.Single(result.Errors);
        Assert.Equal("syntax", error.Code);
        Assert.Equal(0, error.Line);
        Assert.Equal(0, error.Column);
        Assert.Equal(string.Empty, error.Path);
    }

    // 4.9: Message는 한국어, syntax는 YamlDotNet 영어 메시지를 "YAML 문법 오류: " 뒤에 붙인다.
    [Fact]
    public void Syntax_message_has_korean_prefix()
    {
        var error = Assert.Single(CaseInput.Parse("name: a\ntask: [\n").Errors);

        Assert.Equal("syntax", error.Code);
        Assert.StartsWith("YAML 문법 오류: ", error.Message, StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.Path);
        Assert.True(error.Line > 0, error.ToString());
    }

    // 4.9: Source는 Parse에 넘긴 이름 그대로다.
    [Fact]
    public void Parse_uses_given_source_name()
    {
        var result = CaseParser.Parse(CaseInput.Bytes("task: t\nsteps:\n  - name: s\n"), "sub/other.yaml");

        CaseInput.AssertErrors(result, "sub/other.yaml:1:1: required name: 필수 필드가 없다");
    }

    // 4.9 열: UTF-16 코드 단위. 한글 한 글자는 1칸, 이모지(서로게이트 쌍)는 2칸.
    [Fact]
    public void Column_counts_utf16_code_units()
    {
        CaseInput.AssertErrors(
            "name: a\ntask: t\nsteps:\n  - {name: 가나다, answers: {frequency: Weekly}}\n  - {name: 😀😀, answers: {frequency: Weekly}}\n",
            "case.yaml:4:38: enum steps[1].answers.frequency: 선택지 밖의 값 'Weekly'. 가능한 값: daily, weekly, monthly, rare",
            "case.yaml:5:39: enum steps[2].answers.frequency: 선택지 밖의 값 'Weekly'. 가능한 값: daily, weekly, monthly, rare");
    }

    // 4.9 줄: LF·CRLF·CR 모두 한 줄. 오류 줄이 LF 입력과 같다.
    [Theory]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void Line_numbers_do_not_depend_on_line_breaks(string newline)
    {
        var text = CaseInput.SampleText(SpecSamples.TypeErrors).Replace("\n", newline);

        CaseInput.AssertErrors(CaseInput.Parse(text), SpecSamples.TypeErrorsErrors);
    }

    // 오류와 정상 값이 섞이면 모델 없이 오류만 낸다(4.12).
    [Fact]
    public void Mixed_valid_and_invalid_values_give_no_model()
    {
        var result = CaseInput.Parse(SpecSamples.Minimal + "thresholds: {accuracy_min: 101}\n");

        Assert.Null(result.Case);
        Assert.False(result.IsValid);
        Assert.Equal("range", Assert.Single(result.Errors).Code);
    }

    // 별칭 폭탄(20단계 × 4배)을 펼치지 않는다. 오류 117건(steps 0개 1, 알 수 없는 키 20, 앵커 20, 별칭 76).
    [Fact]
    public void Alias_bomb_is_not_expanded()
    {
        var yaml = new System.Text.StringBuilder("name: a\ntask: t\nsteps: []\nx0: &a0 [lol]\n");
        for (var i = 1; i < 20; i++)
        {
            yaml.Append($"x{i}: &a{i} [*a{i - 1}, *a{i - 1}, *a{i - 1}, *a{i - 1}]\n");
        }

        var result = CaseInput.Parse(yaml.ToString());

        Assert.Equal(117, result.Errors.Count);
        Assert.Equal(["alias", "required", "unknown-field"], result.Errors.Select(e => e.Code).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(76, result.Errors.Count(e => e.Message == "별칭(*)은 쓰지 않는다"));
        Assert.Equal(20, result.Errors.Count(e => e.Message == "앵커(&)는 쓰지 않는다"));
    }

    private static CaseParseResult ParseEmptyDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("scorecard-case-").FullName;
        try
        {
            return CaseParser.ParseDirectory(directory);
        }
        finally
        {
            CaseInput.TryDeleteDirectory(directory);
        }
    }
}
