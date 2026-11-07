using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Comparison;

// 스펙 4.4 미검증과 오류의 경계(승인 질문 13), 5절.
// inputs 없음 → not_verified/no_inputs, compare 빔 → not_verified/no_compare_fields. 둘 다 파일을 읽지 않는다.
// inputs를 적었는데 파일이 없거나 기준 데이터 행이 0이면 오류(file, no-rows).
[Trait("Spec", "metrics")]
public sealed class NotVerifiedTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("scorecard-compare-").FullName;

    public void Dispose() => CaseInput.TryDeleteDirectory(root);

    // 미검증 표의 행마다 상태·이유가 같다. "파일을 읽나"가 아니오면 케이스 폴더가 없어도 미검증이다.
    [Fact]
    public void Not_verified_table_rows_match()
    {
        var rows = MetricsSpec.Table(MetricsSpec.NotVerifiedHeader);
        Assert.Equal(3, rows.Count);
        var missingFolder = Path.Combine(root, "no-such-case");

        foreach (var c in rows)
        {
            var definition = DefinitionFor(c[0]);
            var expectedStatus = MetricsSpec.Cell(c[1]);
            var expectedReason = MetricsSpec.Cell(c[2]) == "null" ? null : MetricsSpec.Cell(c[2]);

            if (MetricsSpec.YesNo(c[3]))
            {
                var result = CompareEngine.CompareDirectory(missingFolder, definition);
                Assert.Contains(result.Errors, e => e.Code == "file");
                var measured = CompareInput.Measured(CompareInput.Run(definition, "id,v\nk,a\n", "id,v\nk,a\n"));
                Assert.Equal((expectedStatus, expectedReason), (measured.Status, measured.Reason));
            }
            else
            {
                var metrics = CompareInput.Measured(CompareEngine.CompareDirectory(missingFolder, definition));
                Assert.Equal((expectedStatus, expectedReason), (metrics.Status, metrics.Reason));
                Assert.False(Directory.Exists(missingFolder));
            }
        }
    }

    // 미검증 모양: 상태·이유만 값이 있고 키·셈·퍼센트는 null, 목록은 비어 있다. 케이스 이름은 있다.
    // Compare(바이트)도 sources를 보지 않고 같은 결과다.
    [Fact]
    public void Not_verified_metrics_have_null_counts_and_empty_lists()
    {
        var noInputs = CaseInput.Valid(CaseInput.OneStep);
        var noCompare = CompareInput.Case(CompareInput.InputsCase);
        var garbage = new CompareSources([new SourceFile("reference/a.csv", [0xFF])], [new SourceFile("candidate/a.csv", [])]);

        foreach (var (definition, reason) in new[] { (noInputs, CompareEngine.ReasonNoInputs), (noCompare, CompareEngine.ReasonNoCompareFields) })
        {
            foreach (var result in new[] { CompareEngine.Compare(definition, garbage), CompareEngine.Compare(definition, new CompareSources([], [])) })
            {
                var m = CompareInput.Measured(result);
                Assert.Equal(new CompareMetrics(definition.Name, CompareEngine.StatusNotVerified, reason, null, m.ReferenceFiles, m.CandidateFiles, null, null, null, m.Fields, m.Items), m);
                Assert.Empty(m.ReferenceFiles);
                Assert.Empty(m.CandidateFiles);
                Assert.Empty(m.Fields);
                Assert.Empty(m.Items);
            }
        }
    }

    // 대조한 결과는 measured, 이유 null, 키는 inputs.key.
    [Fact]
    public void Measured_metrics_have_status_measured_and_key()
    {
        var metrics = CompareInput.Measured(CompareInput.Run(CompareInput.Case(CompareInput.TextCase), "id,v\nk,a\n", "id,v\nk,a\n"));

        Assert.Equal((CompareEngine.StatusMeasured, (string?)null, "id"), (metrics.Status, metrics.Reason, metrics.Key));
        Assert.Equal("p", metrics.Case);
    }

    // inputs를 적었는데 한쪽에 파일이 없으면 file 오류(Source = case.yaml, Path = inputs.*, 줄·열 0).
    [Fact]
    public void No_files_on_a_side_is_a_file_error()
    {
        var definition = CompareInput.Case(CompareInput.TextCase);
        var file = CompareInput.Source("reference/a.csv", "id,v\nk,a\n");

        CompareInput.AssertErrors(
            CompareEngine.Compare(definition, new CompareSources([], [file with { Path = "candidate/a.csv" }])),
            "case.yaml: file inputs.reference: 일치하는 파일이 없다");
        CompareInput.AssertErrors(
            CompareEngine.Compare(definition, new CompareSources([file], [])),
            "case.yaml: file inputs.candidate: 일치하는 파일이 없다");
        CompareInput.AssertErrors(
            CompareEngine.Compare(definition, new CompareSources([], [])),
            "case.yaml: file inputs.reference: 일치하는 파일이 없다",
            "case.yaml: file inputs.candidate: 일치하는 파일이 없다");
    }

    // 기준 자료에 데이터 행이 없으면(머리 행뿐, 빈 행뿐) no-rows. 다른 오류가 있으면 no-rows는 나오지 않는다.
    [Fact]
    public void Reference_without_rows_is_no_rows_error()
    {
        var definition = CompareInput.Case(CompareInput.TextCase);

        CompareInput.AssertErrors(CompareInput.Run(definition, "id,v\n", "id,v\nk,a\n"), "case.yaml: no-rows inputs.reference: 기준 자료에 데이터 행이 없다");
        CompareInput.AssertErrors(CompareInput.Run(definition, "id,v\n\n , \n", "id,v\nk,a\n"), "case.yaml: no-rows inputs.reference: 기준 자료에 데이터 행이 없다");
        CompareInput.AssertErrors(CompareInput.Run(definition, "id,v\n", "id\nk\n"), "candidate/a.csv:1: column v: 머리 행에 열이 없다");

        var error = Assert.Single(CompareInput.Run(definition, "id,v\n", "id,v\n").Errors);
        Assert.Equal(new CaseError(CaseParser.FileName, 0, 0, "no-rows", "inputs.reference", "기준 자료에 데이터 행이 없다"), error);
    }

    // 미검증 표 "조건" 칸 → 케이스. inputs 없음 / inputs는 있고 compare가 빔 / 그 밖.
    private static CaseDefinition DefinitionFor(string condition) => condition.Trim() switch
    {
        "`inputs` 없음" => CaseInput.Valid(CaseInput.OneStep),
        "`inputs`는 있고 `compare`가 빔" => CompareInput.Case(CompareInput.InputsCase),
        "그 밖" => CompareInput.Case(CompareInput.TextCase),
        _ => throw new InvalidOperationException($"4.4 미검증 표의 조건 '{condition}'을 모른다."),
    };
}
