using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Core.Judgment;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Comparison;
using Scorecard.Tests.Guards;

namespace Scorecard.Tests.Judgment;

// 스펙 4.12 G2·G3 등록(잠정 결정 11). G2·G3 검사 자체는 CoreGuardTargetTests가 GuardTargets 전부에 돈다.
// 여기서는 등록 내용, 어댑터 출력 모양, 표본 판정이 4.12 글머리와 같은지(빈 통과 방지), G2 탐지력(음성 대조)을 본다.
[Trait("Spec", "judgment")]
public partial class JudgeGuardTargetTests
{
    private const string Target = "judge-case-valid";
    private const string Sample = "case-valid";

    // 4.12 표의 이름·샘플이 GuardTargets에 있다. 어댑터 칸의 출력 파일 이름도 같다.
    [Fact]
    public void Spec_guard_table_entry_is_registered()
    {
        var row = Assert.Single(JudgmentSpec.Table(JudgmentSpec.GuardHeader));

        Assert.Equal(Target, JudgmentSpec.Cell(row[0]));
        Assert.Equal(Sample, SpecDocument.CodeSpans(row[1])[0]);
        Assert.Equal(Sample, GuardTargets.Get(Target).Sample);
        Assert.Contains($"`{JudgmentJson.FileName}`", row[2], StringComparison.Ordinal);
    }

    // 어댑터 출력은 judgment.json 한 파일. UTF-8(BOM 없음), LF, 끝에 \n. 작업 폴더 경로가 없다.
    // 바이트는 같은 표본을 메모리 길(파서 → 대조 → 판정)로 만든 것과 같다.
    [Fact]
    public void Adapter_writes_single_judgment_file_without_paths()
    {
        var target = GuardTargets.Get(Target);
        using var workspace = GuardWorkspace.Create(target.Sample, reverseCopy: false);
        var output = workspace.PathOf("out");

        target.Run(workspace.Input, output);

        var files = Directory.EnumerateFileSystemEntries(output, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(output, f)).ToArray();
        Assert.Equal([JudgmentJson.FileName], files);
        var bytes = File.ReadAllBytes(Path.Combine(output, JudgmentJson.FileName));
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "BOM이 없어야 한다.");
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal((byte)'\n', bytes[^1]);

        var text = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain(workspace.Root, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetFileName(Path.TrimEndingDirectorySeparator(workspace.Root)), text, StringComparison.Ordinal);
        Assert.Equal(JudgmentJson.Render(JudgeSample()), bytes);
    }

    // 4.12 표본 판정: 보류 ACCURACY(66.7% < 90), 시간 not_measured, 몰림 off(유형 열 없음), 다음 할 일 셋.
    // 문장은 4.3·4.4 틀을 표본 값으로 채운 것이다. 표본이 바뀌어 판정 기능 일부만 쓰게 되면 여기서 알린다.
    [Fact]
    public void Sample_judgment_matches_spec()
    {
        var judged = JudgeSample();
        var slots = new Dictionary<string, string> { ["accuracy_pct"] = "66.7", ["accuracy_min"] = "90" };

        Assert.Equal((JudgmentEngine.VerdictHold, JudgmentEngine.RuleAccuracy), (judged.Verdict, judged.Rule));
        Assert.Equal(JudgmentSpec.Fill(JudgmentSpec.FindVerdict(JudgmentSpec.Accuracy, JudgmentInput.NotMet).Template, slots), judged.Reason);
        Assert.Equal(JudgmentInput.NotMeasured, JudgmentInput.CheckOf(judged, JudgmentSpec.Time).Result);
        Assert.Equal(JudgmentInput.Off, JudgmentInput.CheckOf(judged, JudgmentSpec.Focus).Result);
        Assert.Equal(["measure_time", "review_mismatches", "add_type_column"], JudgmentInput.TodoKeys(judged));
        Assert.Empty(judged.Defaults);
        Assert.Contains(judged.Checks, c => c.Reason.Contains("66.7", StringComparison.Ordinal));
    }

    // 4.12 음성 대조(S6 a의 테스트 쪽 판): 문장 숫자를 현재 문화권으로 다시 쓰면 G2 R4(de-DE)가 잡는다.
    // '.'가 ','로 바뀌어 크기는 같고 SHA-256만 다르다.
    [Fact]
    public void Culture_dependent_sentence_is_caught_by_G2()
    {
        var leaking = new GuardTarget("judge-culture-rewrite", Sample, (input, output) =>
        {
            var judged = Judge(input);
            var rewritten = judged with
            {
                Reason = Rewrite(judged.Reason),
                Checks = judged.Checks.Select(c => c with { Reason = Rewrite(c.Reason) }).ToArray(),
            };
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, JudgmentJson.FileName), JudgmentJson.Render(rewritten));
        });

        Assert.Equal([$"G2 R4 문화권: out/{JudgmentJson.FileName} SHA-256 다름"], GuardChecks.Determinism(leaking));
    }

    private static JudgmentResult JudgeSample() => Judge(GuardWorkspace.SamplePath(Sample));

    private static JudgmentResult Judge(string directory)
    {
        var definition = CaseInput.Valid(CaseParser.ParseDirectory(directory));
        return JudgmentEngine.Judge(definition, CompareInput.Measured(CompareEngine.CompareDirectory(directory, definition)));
    }

    // 소수("66.7")를 InvariantCulture로 읽어 현재 문화권으로 쓴다. de-DE에서는 "66,7"이 된다.
    private static string Rewrite(string sentence) =>
        DecimalNumber().Replace(sentence, m => decimal.Parse(m.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture).ToString(CultureInfo.CurrentCulture));

    [GeneratedRegex(@"[0-9]+\.[0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex DecimalNumber();
}
