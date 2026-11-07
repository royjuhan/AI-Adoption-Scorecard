using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Classification;
using Scorecard.Tests.Comparison;
using Scorecard.Tests.Synthetic;

namespace Scorecard.Tests.Judgment;

// 공개 예제 4종 골든(카드 수용 기준 1·4, 잠정 결정 9). 스펙 7절·8절 "골든", "예제 표", "스펙 블록 = 예제 파일".
// examples/<예제>/ → CaseParser.ParseDirectory → CompareDirectory → Judge(시간·사람 입력 null) → Render가 expected/judgment.json과 바이트까지 같다.
// expected 파일은 스펙 7.2~7.5 블록을 바이트 그대로 옮긴 것이다(엔진 출력이 아니다). 파일이 없으면 실패한다(빈 통과 금지).
[Trait("Spec", "judgment")]
public class JudgmentGoldenTests
{
    public static TheoryData<string> Examples => ClassificationInput.ExampleNames;

    // 7.1 예제 표의 예제 = 공개 예제 4종. 7.2~7.5 제목이 있다.
    [Fact]
    public void Example_table_covers_the_four_examples()
    {
        Assert.Equal(ClassificationInput.Examples, JudgmentSpec.Table(JudgmentSpec.ExampleHeader).Select(c => JudgmentSpec.Cell(c[0])));
        Assert.Equal(ClassificationInput.Examples.Order(StringComparer.Ordinal), JudgmentSpec.ExampleHeadings.Keys.Order(StringComparer.Ordinal));
    }

    // 골든: 예제 폴더에서 만든 judgment.json이 expected와 바이트까지 같다.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Example_gives_expected_judgment_bytes(string example)
    {
        var expected = JudgmentGolden.ExpectedBytes(example);
        var actual = JudgmentGolden.ExampleBytes(example);

        Assert.Equal(CompareInput.Text(expected), CompareInput.Text(actual));
        Assert.Equal(expected, actual);
    }

    // 스펙 블록 = 예제 파일. 7.2~7.5의 블록 하나가 expected/judgment.json이다.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Expected_file_equals_spec_block(string example)
    {
        Assert.Equal(Encoding.UTF8.GetBytes(JudgmentSpec.ExampleBlock(example)), JudgmentGolden.ExpectedBytes(example));
    }

    // 스펙의 "`examples/<예제>/expected/judgment.json` (SHA-256 `…`)" 줄 = 파일의 SHA-256 = 블록의 SHA-256.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Expected_file_hash_matches_spec_line(string example)
    {
        var relative = $"examples/{example}/expected/{JudgmentJson.FileName}";
        var spans = SpecDocument.CodeSpans(SpecDocument.Line(JudgmentSpec.Document, $"`{relative}` (SHA-256 "));
        var bytes = JudgmentGolden.ExpectedBytes(example);

        Assert.Equal(relative, spans[0]);
        Assert.Matches("^[0-9a-f]{64}$", spans[1]);
        Assert.Equal(spans[1], Convert.ToHexStringLower(SHA256.HashData(bytes)));
        Assert.Equal(spans[1], Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JudgmentSpec.ExampleBlock(example)))));
    }

    // 7.1 예제 표의 값 = expected/judgment.json의 값. 엔진 출력도 같다. "확인 다섯"·"다음 할 일"의 —는 빈 배열이다.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Example_table_matches_expected_file(string example)
    {
        var cells = Assert.Single(JudgmentSpec.Table(JudgmentSpec.ExampleHeader), c => JudgmentSpec.Cell(c[0]) == example);

        foreach (var bytes in new[] { JudgmentGolden.ExpectedBytes(example), JudgmentGolden.ExampleBytes(example) })
        {
            using var json = JsonDocument.Parse(bytes);
            var root = json.RootElement;

            Assert.Equal(JudgmentSpec.Cell(cells[1]), root.GetProperty("verdict").GetString());
            Assert.Equal(JudgmentSpec.Cell(cells[2]), root.GetProperty("rule").GetString());
            Assert.Equal(JudgmentSpec.List(cells[3]), root.GetProperty("checks").EnumerateArray().Select(c => c.GetProperty("result").GetString()!));
            Assert.Equal(JudgmentSpec.List(cells[4]), root.GetProperty("todo").EnumerateArray().Select(t => t.GetProperty("key").GetString()!));
            Assert.Equal(example, root.GetProperty("case").GetString());
        }
    }

    // 7.1 "12절 대조": 12절 칸이 판정 이름(0절)이면 판정이 그 값이다. table-compare 조건부, form-fill 통과, mixed-exceptions 미검증.
    // signoff-final은 12절이 판정을 정하지 않았다(칸이 판정 이름이 아니다). 셋 이상이 대조돼야 한다(빈 통과 방지).
    [Fact]
    public void Instructions_section_12_verdicts_hold()
    {
        var names = JudgmentSpec.VerdictNames();
        var compared = 0;

        foreach (var cells in JudgmentSpec.Table(JudgmentSpec.ExampleHeader))
        {
            if (names.TryGetValue(JudgmentSpec.Cell(cells[5]), out var verdict))
            {
                Assert.Equal(verdict, JudgmentGolden.Judge(JudgmentSpec.Cell(cells[0])).Verdict);
                compared++;
            }
        }

        Assert.Equal(3, compared);
        Assert.Equal((JudgmentEngine.VerdictConditional, JudgmentEngine.RuleFocus), Pair(JudgmentGolden.Judge("table-compare")));
        Assert.Equal((JudgmentEngine.VerdictPass, JudgmentEngine.RulePass), Pair(JudgmentGolden.Judge("form-fill")));
        Assert.Equal((JudgmentEngine.VerdictNotVerified, JudgmentEngine.RuleNotVerified), Pair(JudgmentGolden.Judge("mixed-exceptions")));
        Assert.Equal((JudgmentEngine.VerdictHold, JudgmentEngine.RuleAccuracy), Pair(JudgmentGolden.Judge("signoff-final")));
    }

    // 7.1: table-compare가 조건부인 근거는 불일치 3건(log07, log08, log27)이 모두 signal_type = irregular인 것이다.
    // signoff-final은 몰림이 spread(record_kind: unsteady 3건, steady 1건)다.
    [Fact]
    public void Example_focus_uses_the_type_column()
    {
        var table = JudgmentInput.CheckOf(JudgmentGolden.Judge("table-compare"), JudgmentSpec.Focus);
        Assert.Equal(JudgmentInput.Found, table.Result);
        Assert.Contains("signal_type = irregular", table.Reason, StringComparison.Ordinal);

        var signoff = JudgmentInput.CheckOf(JudgmentGolden.Judge("signoff-final"), JudgmentSpec.Focus);
        Assert.Equal(JudgmentInput.Spread, signoff.Result);
        Assert.Contains("record_kind", signoff.Reason, StringComparison.Ordinal);
    }

    // 4.6 끝 글머리: mixed-exceptions는 기본값 셋을 썼지만 판정 문장에 표시가 없다. defaults와 confirm_thresholds에 남는다.
    [Fact]
    public void Mixed_exceptions_keeps_defaults_without_mark_in_sentence()
    {
        var judged = JudgmentGolden.Judge("mixed-exceptions");

        Assert.Equal(JudgmentSpec.Defaults().Select(d => d.Key), judged.Defaults);
        Assert.DoesNotContain(JudgmentSpec.DefaultMark, judged.Reason, StringComparison.Ordinal);
        Assert.Equal("confirm_thresholds", judged.Todo[^1].Key);
        Assert.Empty(judged.Checks);
    }

    // examples/.gitattributes(`* -text`)가 새 파일에도 걸린다(7.1 5). expected 파일에 CR이 없다.
    [Theory]
    [MemberData(nameof(Examples))]
    public void Expected_file_has_no_carriage_return(string example)
    {
        Assert.DoesNotContain((byte)'\r', JudgmentGolden.ExpectedBytes(example));
        Assert.Contains("* -text", File.ReadAllLines(Scorecard.Tests.Structure.RepoRoot.Combine("examples/.gitattributes")).Select(l => l.Trim()));
        Assert.True(File.Exists(SyntheticInput.ExamplePath(example, CaseParser.FileName)));
    }

    private static (string Verdict, string Rule) Pair(JudgmentResult result) => (result.Verdict, result.Rule);
}

// 골든 길(스펙 8절): 예제 폴더 → 파서 → 대조 → 판정(시간·사람 입력 null) → judgment.json.
internal static class JudgmentGolden
{
    public static (CaseDefinition Definition, CompareMetrics Metrics) Inputs(string example)
    {
        var definition = SyntheticInput.CaseOf(example);
        var metrics = CompareInput.Measured(CompareEngine.CompareDirectory(SyntheticInput.ExampleDirectory(example), definition));
        return (definition, metrics);
    }

    public static JudgmentResult Judge(string example)
    {
        var (definition, metrics) = Inputs(example);
        return JudgmentEngine.Judge(definition, metrics);
    }

    public static byte[] ExampleBytes(string example) => JudgmentJson.Render(Judge(example));

    // 예제 expected/judgment.json(커밋된 바이트). 없으면 실패한다.
    public static byte[] ExpectedBytes(string example) => SyntheticInput.ExampleBytes(example, "expected/" + JudgmentJson.FileName);
}
