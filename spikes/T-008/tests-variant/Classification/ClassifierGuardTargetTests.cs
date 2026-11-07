using System.Text;
using System.Text.Json;
using Scorecard.Core.Cases;
using Scorecard.Core.Classification;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Guards;

namespace Scorecard.Tests.Classification;

// 스펙 4.10 G2·G3 등록(승인 결정 12). G2·G3 검사 자체는 CoreGuardTargetTests가 GuardTargets 전부에 돈다.
// 여기서는 등록 내용, 어댑터 출력 모양, 표본이 분류 기능을 고루 쓰는지(빈 통과 방지), G2 탐지력을 본다.
[Trait("Spec", "classification")]
public class ClassifierGuardTargetTests
{
    private const string TargetName = "classifier-case-valid";

    // 4.10 표의 이름·샘플이 GuardTargets에 있다.
    [Fact]
    public void Spec_guard_table_entries_are_registered()
    {
        var rows = ClassificationSpec.Table(ClassificationSpec.GuardHeader);

        Assert.NotEmpty(rows);
        foreach (var cells in rows)
        {
            var name = SpecDocument.Unquote(cells[0]);
            var sample = SpecDocument.CodeSpans(cells[1])[0];
            Assert.Equal(sample, GuardTargets.Get(name).Sample);
        }

        Assert.Equal("case-valid", GuardTargets.Get(TargetName).Sample);
    }

    // 어댑터 출력은 classification.json 한 파일. UTF-8(BOM 없음), LF, 끝에 \n. 작업 폴더 경로가 없다.
    [Fact]
    public void Adapter_writes_single_classification_file_without_paths()
    {
        var target = GuardTargets.Get(TargetName);
        using var workspace = GuardWorkspace.Create(target.Sample, reverseCopy: false);
        var output = workspace.PathOf("out");

        target.Run(workspace.Input, output);

        var files = Directory.EnumerateFileSystemEntries(output, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(output, f))
            .ToArray();
        Assert.Equal([ClassificationJson.FileName], files);

        var bytes = File.ReadAllBytes(Path.Combine(output, ClassificationJson.FileName));
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "BOM이 없어야 한다.");
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal((byte)'\n', bytes[^1]);

        // 최상위 속성은 스펙 3.2 모양 블록과 이름·순서까지 같다(T-007: schema_version, case, steps).
        using var json = JsonDocument.Parse(bytes);
        Assert.Equal(ClassificationSpec.ShapeTopLevelNames(), json.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.NotEqual(0, json.RootElement.GetProperty("steps").GetArrayLength());

        // JSON은 '\'를 '\\'로 쓰므로 Windows 전체 경로는 그대로 나오지 않는다. 임시 폴더 이름 조각도 찾는다.
        var text = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain(workspace.Root, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetFileName(Path.TrimEndingDirectorySeparator(workspace.Root)), text, StringComparison.Ordinal);
    }

    // 4.10: 정상 표본 case-valid는 ②·③·④(예외 검토·게이트), 기본값 표시 5행, 확인 항목 2종을 모두 낸다.
    // T-008: 표본에 human_signoff가 없어(기본값) 1·2단계 ELSE 행에도 표시가 붙는다(3 → 5행).
    // 표본이 바뀌어 분류 기능 일부만 쓰게 되면 G2·G3가 약해지므로 여기서 알린다.
    [Fact]
    public void Guard_sample_exercises_all_row_kinds_marks_and_checks()
    {
        var parsed = CaseParser.ParseDirectory(GuardWorkspace.SamplePath("case-valid"));
        var result = StepClassifier.Classify(CaseInput.Valid(parsed));
        var rows = result.Steps.SelectMany(s => s.Rows).ToArray();

        Assert.Equal(["②", "③", "④"], rows.Select(r => r.Assignee).Distinct().Order(StringComparer.Ordinal));
        Assert.Contains(rows, r => r.Kind == StepClassifier.KindExceptionReview);
        Assert.Contains(rows, r => r.Kind == StepClassifier.KindHumanGate);
        Assert.Equal(5, rows.Count(r => r.Reason.EndsWith(ClassificationSpec.DefaultMark, StringComparison.Ordinal)));
        Assert.Equal(ClassificationSpec.CheckKeys(), ClassificationSpec.CheckKeys().Where(k => result.Steps.Any(s => s.Checks.Contains(k))));
    }

    // 4.10 음성 대조(S6): 어댑터가 케이스 이름 자리에 입력 폴더 절대 경로를 넣으면 G2 R3가 잡는다.
    [Fact]
    public void Absolute_path_in_output_is_caught_by_G2()
    {
        var leaking = new GuardTarget("classifier-absolute-path", "case-valid", (input, output) =>
        {
            var definition = CaseInput.Valid(CaseParser.ParseDirectory(input));
            Directory.CreateDirectory(output);
            File.WriteAllBytes(
                Path.Combine(output, ClassificationJson.FileName),
                ClassificationJson.Render(StepClassifier.Classify(definition with { Name = Path.GetFullPath(input) })));
        });

        Assert.Contains($"G2 R3 다른 경로: out/{ClassificationJson.FileName} SHA-256 다름", GuardChecks.Determinism(leaking));
    }
}
