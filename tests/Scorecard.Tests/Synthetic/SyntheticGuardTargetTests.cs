using System.Globalization;
using System.Text;
using Scorecard.Core.Cases;
using Scorecard.Core.Synthetic;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Guards;

namespace Scorecard.Tests.Synthetic;

// 스펙 4.11 G2·G3 등록(승인 결정 11). G2·G3 검사 자체는 CoreGuardTargetTests가 GuardTargets 전부에 돈다.
// 여기서는 등록 내용, 어댑터 출력 모양, 고정 계획이 생성 기능을 고루 쓰는지(빈 통과 방지), G2 탐지력을 본다.
// 기존 G2·G3 테스트는 바꾸지 않는다(Real_targets_use_case_samples_not_basic는 이미 중복을 빼고 비교한다).
[Trait("Spec", "synthetic-examples")]
public class SyntheticGuardTargetTests
{
    private const string TargetName = "synthetic-case-valid";

    // 4.11 표의 이름·샘플이 GuardTargets에 있다. 어댑터 칸의 출력 파일(`…csv`)도 표에서 읽는다.
    [Fact]
    public void Spec_guard_table_entries_are_registered()
    {
        var rows = SyntheticSpec.Table(SyntheticSpec.GuardHeader);

        Assert.NotEmpty(rows);
        foreach (var cells in rows)
        {
            Assert.Equal(SpecDocument.CodeSpans(cells[1])[0], GuardTargets.Get(SpecDocument.Unquote(cells[0])).Sample);
        }

        Assert.Equal("case-valid", GuardTargets.Get(TargetName).Sample);
    }

    // 어댑터 출력은 4.11 표의 두 파일(reference/rows.csv, candidate/rows.csv)뿐이다. CSV 바이트 규칙(3.3)을 지키고 작업 폴더 경로가 없다.
    [Fact]
    public void Adapter_writes_reference_and_candidate_csv_without_paths()
    {
        var expectedFiles = SpecDocument.CodeSpans(Assert.Single(SyntheticSpec.Table(SyntheticSpec.GuardHeader), c => SpecDocument.Unquote(c[0]) == TargetName)[2])
            .Where(s => s.EndsWith(".csv", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            [$"{SyntheticCsv.CandidateFolder}/{GuardTargets.SyntheticGuardPlan.FileName}", $"{SyntheticCsv.ReferenceFolder}/{GuardTargets.SyntheticGuardPlan.FileName}"],
            expectedFiles);

        var target = GuardTargets.Get(TargetName);
        using var workspace = GuardWorkspace.Create(target.Sample, reverseCopy: false);
        var output = workspace.PathOf("out");

        target.Run(workspace.Input, output);

        var files = Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(output, f).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedFiles, files);

        var key = CaseInput.Valid(CaseParser.ParseDirectory(GuardWorkspace.SamplePath(target.Sample))).Inputs!.Key;
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(Path.Combine(output, file));
            var csv = SyntheticInput.ParseCsv(bytes, file);
            Assert.Equal([key, .. GuardTargets.SyntheticGuardPlan.Columns.Select(c => c.Name)], csv.Header);

            var text = Encoding.ASCII.GetString(bytes);
            Assert.DoesNotContain(workspace.Root, text, StringComparison.Ordinal);
            Assert.DoesNotContain(Path.GetFileName(Path.TrimEndingDirectorySeparator(workspace.Root)), text, StringComparison.Ordinal);
        }
    }

    // 4.11: 고정 계획은 세 열 종류와 심기 다섯 가지(none, missing, mismatch, within, extra)를 모두 쓴다.
    // 계획이 바뀌어 생성 기능 일부만 쓰게 되면 G2·G3가 약해지므로 여기서 알린다. 심은 행은 계획대로다.
    [Fact]
    public void Guard_plan_exercises_all_column_kinds_and_plantings()
    {
        var plan = GuardTargets.SyntheticGuardPlan;
        var definition = CaseInput.Valid(CaseParser.ParseDirectory(GuardWorkspace.SamplePath("case-valid")));
        var tables = SyntheticGenerator.Generate(definition, plan);

        Assert.Equal(
            [SyntheticColumn.KindChoice, SyntheticColumn.KindDecimal, SyntheticColumn.KindInteger],
            plan.Columns.Select(c => c.Kind).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(definition.Compare.Select(f => f.Field).Order(StringComparer.Ordinal), plan.MismatchColumns.Order(StringComparer.Ordinal));

        var planted = tables.Reference.Concat(tables.Candidate)
            .Select(r => r.Planted.Split(':')[0] + (r.Planted.Contains(':', StringComparison.Ordinal) ? ":" : ""))
            .Distinct()
            .Order(StringComparer.Ordinal);
        Assert.Equal(
            new[]
            {
                SyntheticGenerator.PlantedExtra,
                SyntheticGenerator.PlantedMismatchPrefix,
                SyntheticGenerator.PlantedMissing,
                SyntheticGenerator.PlantedNone,
                SyntheticGenerator.PlantedWithinPrefix,
            }.Order(StringComparer.Ordinal),
            planted);

        StructureTests.AssertPlantedRows(definition, plan, tables);
        var counts = Variants.All.Select(v => SyntheticInput.Count(definition, tables, v)).Append(StructureTests.PlantedTally(tables)).Distinct();
        Assert.Equal(new Tally(12, 12, 8, 3, 1, 1, 2), Assert.Single(counts));
    }

    // 4.11 음성 대조(S6): 생성 결과의 소수를 현재 문화권으로 다시 읽으면 G1은 못 잡고 G2 R4(de-DE)가 잡는다.
    [Fact]
    public void Culture_dependent_reparse_is_caught_by_G2()
    {
        var leaking = new GuardTarget("synthetic-culture-reparse", "case-valid", (input, output) =>
        {
            var definition = CaseInput.Valid(CaseParser.ParseDirectory(input));
            var tables = SyntheticGenerator.Generate(definition, GuardTargets.SyntheticGuardPlan);
            var mean = SyntheticInput.IndexOf(tables.Header, "mean") - 1;
            var rows = tables.Candidate
                .Select(r => r with { Values = [.. r.Values.Select((v, i) => i == mean ? Reparse(v) : v)] })
                .ToArray();
            Directory.CreateDirectory(Path.Combine(output, SyntheticCsv.CandidateFolder));
            File.WriteAllBytes(Path.Combine(output, SyntheticCsv.CandidateFolder, tables.FileName), SyntheticCsv.Render(tables.Header, rows));
        });

        var differences = GuardChecks.Determinism(leaking);

        Assert.NotEmpty(differences);
        Assert.All(differences, d => Assert.StartsWith("G2 R4 문화권: out/", d, StringComparison.Ordinal));
    }

    // 현재 문화권으로 읽고 InvariantCulture로 쓴다. de-DE에서는 '.'이 천 단위 구분이라 값이 바뀐다.
    private static string Reparse(string value) =>
        decimal.Parse(value, NumberStyles.Number, CultureInfo.CurrentCulture).ToString("F2", CultureInfo.InvariantCulture);
}
