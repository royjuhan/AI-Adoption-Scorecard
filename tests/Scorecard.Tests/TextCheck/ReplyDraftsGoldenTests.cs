using System.Security.Cryptography;
using Scorecard.Core.Classification;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Classification;
using Scorecard.Tests.Structure;
using Scorecard.Tests.Synthetic;

namespace Scorecard.Tests.TextCheck;

// 스펙 7절 reply-drafts. 골든(예제 폴더 → 파서 → 분류·대조·판정 → expected 세 파일 바이트), 블록 = 파일, SHA-256·바이트 수 줄,
// 7.1 새 예제 표·예제 파일 표. expected는 7.4 블록을 바이트 그대로 옮긴 것이다(엔진 출력이 아니다). 카드 수용 기준 4.
// 파일이 없으면 실패한다(빈 통과 금지).
[Trait("Spec", "text-check")]
public class ReplyDraftsGoldenTests
{
    private const string Classification = "expected/" + ClassificationJson.FileName;
    private const string Metrics = "expected/" + MetricsJson.FileName;
    private const string Judgment = "expected/" + JudgmentJson.FileName;

    public static TheoryData<string> Files => new(TextCheckSpec.ExampleFiles());

    // 예제 이름은 ClassificationInput.TextCheckExamples 하나이고 7.1 새 예제 표의 이름과 같다.
    [Fact]
    public void New_example_table_names_the_text_check_example()
    {
        Assert.Equal(ClassificationInput.TextCheckExamples, TextCheckSpec.Table(TextCheckSpec.NewExampleHeader).Select(c => TextCheckSpec.Cell(c[0])));
        Assert.Equal([TextCheckSpec.Example], ClassificationInput.TextCheckExamples);
        Assert.DoesNotContain(TextCheckSpec.Example, ClassificationInput.Examples);
    }

    // 골든: answers.yaml → 분류 = expected/classification.json. case.yaml의 steps로 분류해도 같은 바이트다(단계·답이 같다).
    [Fact]
    public void Classification_from_answers_and_case_equals_expected()
    {
        var expected = Bytes(Classification);
        var fromAnswers = ClassificationInput.RenderAnswers(Bytes("answers.yaml"));
        var fromCase = ClassificationJson.Render(StepClassifier.Classify(SyntheticInput.CaseOf(TextCheckSpec.Example)));

        Assert.Equal(TextCheckInput.Text(expected), TextCheckInput.Text(fromAnswers));
        Assert.Equal(expected, fromAnswers);
        Assert.Equal(expected, fromCase);
    }

    // 골든: 예제 폴더 → CompareDirectory → Render = expected/metrics.json, Judge(시간·사람 입력 null) → Render = expected/judgment.json.
    [Fact]
    public void Metrics_and_judgment_equal_expected()
    {
        var root = SyntheticInput.ExampleDirectory(TextCheckSpec.Example);
        var definition = SyntheticInput.CaseOf(TextCheckSpec.Example);
        var compared = CompareEngine.CompareDirectory(root, definition);
        Assert.True(compared.IsValid, string.Join("\n", compared.Errors.Select(e => e.ToString())));

        var metrics = MetricsJson.Render(compared.Metrics!);
        var judgment = JudgmentJson.Render(JudgmentEngine.Judge(definition, compared.Metrics!));

        Assert.Equal(TextCheckInput.Text(Bytes(Metrics)), TextCheckInput.Text(metrics));
        Assert.Equal(Bytes(Metrics), metrics);
        Assert.Equal(TextCheckInput.Text(Bytes(Judgment)), TextCheckInput.Text(judgment));
        Assert.Equal(Bytes(Judgment), judgment);
    }

    // 블록 = 파일: 7.3·7.4의 "`examples/reply-drafts/<파일>` (SHA-256 `…`, N바이트):" 줄 뒤 블록 = 예제 파일 바이트.
    // 줄의 SHA-256·바이트 수 = 파일의 SHA-256·바이트 수.
    [Theory]
    [MemberData(nameof(Files))]
    public void Example_file_equals_spec_block_and_line(string file)
    {
        var bytes = Bytes(file);
        var (sha256, length) = TextCheckSpec.FileLineValues(file);

        Assert.Equal(TextCheckInput.Text(TextCheckSpec.FileBlock(file)), TextCheckInput.Text(bytes));
        Assert.Equal(TextCheckSpec.FileBlock(file), bytes);
        Assert.Equal(sha256, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        Assert.Equal(length, bytes.Length);
    }

    // 7.1 예제 파일 표: "블록은 7.3·7.4에 이 순서로 있다". 표에 없는 reply-drafts 파일 줄이 문서에 없다.
    [Fact]
    public void File_lines_follow_example_file_table_order()
    {
        var lines = File.ReadAllLines(RepoRoot.Combine(TextCheckSpec.Document));
        var prefix = $"`examples/{TextCheckSpec.Example}/";
        var found = lines.Where(l => l.StartsWith(prefix, StringComparison.Ordinal) && l.Contains("` (SHA-256 `", StringComparison.Ordinal))
            .Select(l => l[prefix.Length..l.IndexOf("` (SHA-256 ", StringComparison.Ordinal)])
            .ToArray();

        Assert.Equal(TextCheckSpec.ExampleFiles(), found);
        Assert.Equal(10, found.Length);
    }

    // 7.1 새 예제 표 = expected/judgment.json의 값 = 엔진 출력. 확인 여섯은 checks 순서, 검토 항목은 UNEXPLAINED 문장의 {breakdown}.
    [Fact]
    public void New_example_table_matches_expected_and_engine()
    {
        var c = Assert.Single(TextCheckSpec.Table(TextCheckSpec.NewExampleHeader));
        var definition = SyntheticInput.CaseOf(TextCheckSpec.Example);
        var run = TextCheckInput.Valid(TextCheckInput.Run(SyntheticInput.ExampleDirectory(TextCheckSpec.Example)));

        Assert.Equal(TextCheckSpec.Cell(c[1]), definition.Inputs!.Group);
        foreach (var judgment in new[] { TextCheckInput.Json(Bytes(Judgment)), run.JudgmentJson })
        {
            var checks = judgment.GetProperty("checks").EnumerateArray().ToArray();
            Assert.Equal(
                (TextCheckSpec.Cell(c[2]), TextCheckSpec.Cell(c[3])),
                (judgment.GetProperty("verdict").GetString(), judgment.GetProperty("rule").GetString()));
            Assert.Equal(TextCheckSpec.List(c[4]), checks.Select(x => x.GetProperty("result").GetString()));
            Assert.Equal(TextCheckSpec.List(c[5]), judgment.GetProperty("todo").EnumerateArray().Select(t => t.GetProperty("key").GetString()));
            var unexplained = Assert.Single(checks, x => x.GetProperty("rule").GetString() == JudgmentEngine.RuleUnexplained);
            Assert.Contains($"({c[6].Trim()})", unexplained.GetProperty("reason").GetString(), StringComparison.Ordinal);
        }
    }

    // 6.3: 체크리스트 d03(action)·d12(ticket) 값 불일치, 정확도 83.3%. 점수표 72점, AI 3.86(139/36), 사람 4.19(151/36).
    [Fact]
    public void Reply_drafts_results_match_section_6_3()
    {
        var compare = TextCheckInput.Json(Bytes(Metrics)).GetProperty("compare");
        var items = compare.GetProperty("items").EnumerateArray()
            .Select(i => (i.GetProperty("key").GetString(), i.GetProperty("status").GetString(), string.Join(",", TextCheckInput.Strings(i.GetProperty("missing_items")))))
            .ToArray();

        Assert.Equal([("d03", "mismatch", "action"), ("d12", "mismatch", "ticket")], items);
        Assert.Equal("83.3", compare.GetProperty("accuracy_pct").GetRawText());
        Assert.Equal("0.0", compare.GetProperty("missing_rate_pct").GetRawText());
    }

    // 4.6 G2·G3 표본 text-check-valid = 7.3 바탕 파일(expected 제외) 바이트 그대로.
    [Fact]
    public void Guard_sample_equals_example_base_files()
    {
        var root = RepoRoot.Combine("tests/Scorecard.Tests/TestData/guard/text-check-valid");
        var actual = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var files = TextCheckSpec.BaseFiles();

        Assert.Equal(files.Keys.Order(StringComparer.Ordinal), actual);
        Assert.All(files, f => Assert.Equal(f.Value, File.ReadAllBytes(Path.Combine(root, f.Key))));
    }

    private static byte[] Bytes(string relative) => SyntheticInput.ExampleBytes(TextCheckSpec.Example, relative);
}
