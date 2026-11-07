using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Scorecard.Core.Classification;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.Classification;

// 공개 예제 3종 골든(카드 수용 기준 3, 승인 결정 11·13·14).
// examples/<예제>/answers.yaml → CaseParser.Parse(bytes, "answers.yaml") → Classify → Render가
// expected/classification.json과 바이트까지 같다. 파일이 없으면 실패한다(빈 통과 금지).
// 예제 파일은 스펙 7.2~7.4 블록을 바이트 그대로 옮긴 것이다(사람 확정, 승인 결정 14).
[Trait("Spec", "classification")]
public class ExampleGoldenTests
{
    private const string AnswersFile = "answers.yaml";
    private const string ExpectedFile = "expected/" + ClassificationJson.FileName;

    private static readonly Dictionary<string, string> SpecHeadings = new(StringComparer.Ordinal)
    {
        ["table-compare"] = "### 7.2 table-compare",
        ["form-fill"] = "### 7.3 form-fill",
        ["mixed-exceptions"] = "### 7.4 mixed-exceptions",
    };

    public static TheoryData<string> Examples => ClassificationInput.ExampleNames;

    // 줄바꿈 변형(스펙 2절, 5절): LF·CRLF·CR·BOM 모두 같은 바이트.
    public static TheoryData<string, string> LineEndingVariants
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var example in ClassificationInput.Examples)
            {
                foreach (var variant in new[] { "lf", "crlf", "cr", "bom" })
                {
                    data.Add(example, variant);
                }
            }

            return data;
        }
    }

    [Fact]
    public void Examples_are_the_three_spec_examples()
    {
        Assert.Equal(ClassificationInput.Examples.Order(StringComparer.Ordinal), SpecHeadings.Keys.Order(StringComparer.Ordinal));
        Assert.All(ClassificationInput.Examples, e => Assert.True(
            Directory.Exists(RepoRoot.Combine($"{ClassificationInput.ExamplesRelativePath}/{e}")),
            $"examples/{e}/ 폴더가 없다."));
    }

    // 골든: answers.yaml에서 만든 classification.json이 expected와 바이트까지 같다.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Answers_give_expected_classification_bytes(string example)
    {
        var answers = ReadExample(example, AnswersFile);
        var expected = ReadExample(example, ExpectedFile);

        Assert.Equal(ClassificationInput.Text(expected), ClassificationInput.Text(ClassificationInput.RenderAnswers(answers)));
        Assert.Equal(expected, ClassificationInput.RenderAnswers(answers));
    }

    // 스펙 블록 = 예제 파일. 7.2~7.4의 첫 블록은 answers.yaml, 둘째 블록은 expected/classification.json이다.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Example_files_equal_spec_blocks(string example)
    {
        var blocks = SpecDocument.CodeBlocks(ClassificationSpec.Document, SpecHeadings[example]);

        Assert.Equal(2, blocks.Count);
        Assert.Equal(Encoding.UTF8.GetBytes(blocks[0]), ReadExample(example, AnswersFile));
        Assert.Equal(Encoding.UTF8.GetBytes(blocks[1]), ReadExample(example, ExpectedFile));
    }

    // 스펙이 적은 expected 파일의 SHA-256과 같다(스펙 7.2~7.4 "(SHA-256 `…`)").
    [Theory]
    [MemberData(nameof(Examples))]
    public void Expected_file_hash_matches_spec(string example)
    {
        var line = SpecDocument.Line(ClassificationSpec.Document, $"`examples/{example}/{ExpectedFile}` (SHA-256 ");
        var spans = SpecDocument.CodeSpans(line);

        Assert.Equal($"examples/{example}/{ExpectedFile}", spans[0]);
        Assert.Equal(spans[1], Convert.ToHexStringLower(SHA256.HashData(ReadExample(example, ExpectedFile))));
    }

    // 승인 결정 13: answers.yaml은 case.yaml 스키마의 부분(name, task, steps). 다른 최상위 키는 쓰지 않는다.
    // name은 예제 폴더 이름, task는 지시문 12절 "업무" 칸과 같다.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Answers_use_only_name_task_and_steps(string example)
    {
        var definition = ClassificationInput.ParseAnswers(ReadExample(example, AnswersFile));
        var instructions = InstructionsRow(example);

        Assert.Equal(example, definition.Name);
        Assert.Equal(instructions[1], definition.Task);
        Assert.Null(definition.Before);
        Assert.Null(definition.After);
        Assert.Null(definition.AiUsage);
        Assert.Null(definition.Inputs);
        Assert.Empty(definition.Compare);
        Assert.Equal(new Scorecard.Core.Cases.CaseThresholds(null, null, null), definition.Thresholds);
        Assert.NotEmpty(definition.Steps);
    }

    // 지시문 12절 "단계 수" = 모든 단계의 rows 원소 수 합(스펙 3.2, 행 모델 A, 승인 결정 1).
    [Theory]
    [MemberData(nameof(Examples))]
    public void Row_count_equals_instructions_step_count(string example)
    {
        var result = StepClassifier.Classify(ClassificationInput.ParseAnswers(ReadExample(example, AnswersFile)));
        var expected = int.Parse(InstructionsRow(example)[2], CultureInfo.InvariantCulture);

        Assert.Equal(expected, result.Steps.Sum(s => s.Rows.Count));
    }

    // 지시문 12절 기대값 중 분류만으로 정해지는 것(스펙 6.4).
    // form-fill: existing_feature: unknown 1건 → 확인 항목, AI 과잉 후보 1단계. mixed-exceptions: ③ = 입력 양식 표준화(Q3).
    [Fact]
    public void Instructions_expectations_hold_for_examples()
    {
        var formFill = Classify("form-fill");
        Assert.Single(formFill.Steps, s => s.Checks.Contains(ClassificationSpec.ConfirmExistingFeature));
        Assert.Single(formFill.Steps, s => s.AiOveruseCandidate);
        Assert.Contains(formFill.Steps.SelectMany(s => s.Rows), r => r.Kind == ClassificationSpec.KindHumanGate);

        var mixed = Classify("mixed-exceptions");
        var third = mixed.Steps.SelectMany(s => s.Rows).Where(r => r.Assignee == "③").ToArray();
        Assert.Equal(2, third.Length);
        Assert.All(third, r => Assert.Equal("Q3", r.Rule));
        Assert.All(third, r => Assert.Contains("입력 양식 표준화", r.Reason, StringComparison.Ordinal));

        var table = Classify("table-compare");
        Assert.Contains(table.Steps, s => s.Rows.Select(r => r.Assignee).SequenceEqual(["②", "④"]) && s.Rows[1].Kind == ClassificationSpec.KindExceptionReview);
        Assert.Contains(table.Steps, s => s.Rows.Select(r => r.Assignee).SequenceEqual(["②", "④"]) && s.Rows[1].Kind == ClassificationSpec.KindHumanGate);
        Assert.All(new[] { formFill, mixed, table }, r => Assert.DoesNotContain(r.Steps.SelectMany(s => s.Rows), x => x.Assignee == "①"));
    }

    // 스펙 7.1 요약 표의 숫자가 분류 결과와 같다.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Example_summary_table_matches_result(string example)
    {
        var cells = Assert.Single(ClassificationSpec.Table(ClassificationSpec.ExampleSummaryHeader), c => c[0] == example);
        var result = Classify(example);
        var rows = result.Steps.SelectMany(s => s.Rows).ToArray();

        Assert.Equal(Leading(cells[1]), result.Steps.Count);
        Assert.Equal(Leading(cells[2]), rows.Length);
        Assert.Equal(Leading(cells[3]), rows.Count(r => r.Assignee == "②"));
        Assert.Equal(Leading(cells[4]), rows.Count(r => r.Assignee == "③"));
        Assert.Equal(Leading(cells[5]), rows.Count(r => r.Kind == ClassificationSpec.KindExceptionReview));
        Assert.Equal(Leading(cells[6]), rows.Count(r => r.Kind == ClassificationSpec.KindHumanGate));
        Assert.Equal(CheckCounts(cells[7]), CountChecks(result));
        Assert.Equal(Leading(cells[8]), result.Steps.Count(s => s.AiOveruseCandidate));
        Assert.Equal(Leading(cells[9]), rows.Count(r => r.Reason.EndsWith(ClassificationSpec.DefaultMark, StringComparison.Ordinal)));
    }

    // 스펙 2절·5절: answers.yaml의 LF·CRLF·CR·BOM 변형은 같은 classification.json 바이트를 낸다.
    [Theory]
    [MemberData(nameof(LineEndingVariants))]
    public void Line_ending_and_bom_variants_give_same_bytes(string example, string variant)
    {
        var text = ClassificationInput.Text(ReadExample(example, AnswersFile));
        var bytes = variant switch
        {
            "lf" => ClassificationInput.Bytes(text),
            "crlf" => ClassificationInput.Bytes(text.Replace("\n", "\r\n", StringComparison.Ordinal)),
            "cr" => ClassificationInput.Bytes(text.Replace("\n", "\r", StringComparison.Ordinal)),
            "bom" => [0xEF, 0xBB, 0xBF, .. ClassificationInput.Bytes(text)],
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null),
        };

        Assert.Equal(ReadExample(example, ExpectedFile), ClassificationInput.RenderAnswers(bytes));
    }

    // 승인 결정 11: examples/.gitattributes(`* -text`)가 있다. core.autocrlf=true clone에서도 바이트가 그대로다.
    [Fact]
    public void Examples_gitattributes_disables_text_conversion()
    {
        var path = RepoRoot.Combine($"{ClassificationInput.ExamplesRelativePath}/.gitattributes");

        Assert.True(File.Exists(path), "examples/.gitattributes가 없다.");
        Assert.Contains("* -text", File.ReadAllLines(path).Select(l => l.Trim()));
    }

    // examples/ 텍스트 파일에 \r이 없다(TestData의 Sample_text_files_have_no_carriage_return과 같은 방식).
    // NUL 바이트가 있는 파일은 바이너리로 보고 뺀다(git과 같은 판정).
    [Fact]
    public void Example_text_files_have_no_carriage_return()
    {
        var textFiles = Directory.EnumerateFiles(RepoRoot.Combine(ClassificationInput.ExamplesRelativePath), "*", SearchOption.AllDirectories)
            .Where(f => !File.ReadAllBytes(f).AsSpan().Contains((byte)0))
            .ToArray();
        Assert.Contains(textFiles, f => f.EndsWith(ClassificationJson.FileName, StringComparison.Ordinal));

        var withCarriageReturn = textFiles
            .Where(f => File.ReadAllBytes(f).AsSpan().Contains((byte)'\r'))
            .Select(RepoRoot.ToRelative)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(withCarriageReturn);
    }

    // 파일이 없으면 이름을 보이고 실패한다(빈 통과 금지).
    private static byte[] ReadExample(string example, string relative)
    {
        var path = ClassificationInput.ExamplePath(example, relative);
        Assert.True(File.Exists(path), $"examples/{example}/{relative}가 없다.");
        return File.ReadAllBytes(path);
    }

    private static ClassificationResult Classify(string example) =>
        StepClassifier.Classify(ClassificationInput.ParseAnswers(ReadExample(example, AnswersFile)));

    // 지시문 12절 표의 그 예제 행. 첫 칸은 `예제`.
    private static string[] InstructionsRow(string example) =>
        Assert.Single(
            SpecDocument.Table(SpecDocument.InstructionsPath, ClassificationSpec.InstructionsExampleHeader),
            c => SpecDocument.Unquote(c[0]) == example);

    // "1 (2단계)" → 1.
    private static int Leading(string cell) => int.Parse(cell.Split(' ')[0], CultureInfo.InvariantCulture);

    // "`measure` 4, `confirm` 1" → [(measure_minutes_per_item, 4), (confirm_existing_feature, 1)]. 접두어로 키를 찾는다.
    private static (string Key, int Count)[] CheckCounts(string cell) =>
        cell.Split(", ").Select(part =>
        {
            var pieces = part.Split(' ');
            var prefix = SpecDocument.Unquote(pieces[0]);
            var key = Assert.Single(ClassificationSpec.CheckKeys(), k => k.StartsWith(prefix + "_", StringComparison.Ordinal));
            return (key, int.Parse(pieces[1], CultureInfo.InvariantCulture));
        }).ToArray();

    // 확인 항목 키별 단계 수(4.7 표 순서, 0건은 뺀다).
    private static (string Key, int Count)[] CountChecks(ClassificationResult result) =>
        ClassificationSpec.CheckKeys()
            .Select(k => (k, result.Steps.Count(s => s.Checks.Contains(k))))
            .Where(x => x.Item2 > 0)
            .ToArray();
}
