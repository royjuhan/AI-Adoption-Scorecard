using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Comparison;

// 스펙 4.7 오류(승인 질문 14·20), 3.4 source·path. 카드 수용 기준 2.
// 오류 하나는 CaseError(Source, Line, Column, Code, Path, Message). 모두 모은다. 오류가 하나라도 있으면 지표가 없다.
// 순서: 기준 쪽 → 결과 쪽. 쪽 안에서는 파일이 정해지기 전 오류(case.yaml) → 파일 경로 서수. 파일 안에서는 (줄, 열). 같으면 찾은 순서.
[Trait("Spec", "metrics")]
public sealed class ErrorCodeTests : IDisposable
{
    private static readonly CaseDefinition Definition = CompareInput.Case(CompareInput.NumberAndTextCase);

    private readonly string root = Directory.CreateTempSubdirectory("scorecard-compare-").FullName;

    // 임시 케이스 폴더에는 candidate/a.csv만 있다. 그래서 아래 pattern·file 입력은 기준 쪽 오류 하나만 낸다.
    public ErrorCodeTests()
    {
        Directory.CreateDirectory(Path.Combine(root, "candidate"));
        File.WriteAllBytes(Path.Combine(root, "candidate", "a.csv"), CompareInput.Bytes(CompareInput.GoodCsv));
    }

    public void Dispose() => CaseInput.TryDeleteDirectory(root);

    // 코드마다 그 오류를 내는 입력 하나.
    private IReadOnlyDictionary<string, Func<IReadOnlyList<CaseError>>> Producers => new Dictionary<string, Func<IReadOnlyList<CaseError>>>(StringComparer.Ordinal)
    {
        ["pattern"] = () => CompareInputs.Read(root, new CaseInputs("ref*/a.csv", "candidate/a.csv", "id")).Errors,
        ["file"] = () => CompareInputs.Read(root, new CaseInputs("missing/*.csv", "candidate/*.csv", "id")).Errors,
        ["overlap"] = () => CompareInput.Run(Definition, [CompareInput.Source("x/a.csv", CompareInput.GoodCsv)], [CompareInput.Source("x/a.csv", CompareInput.GoodCsv)]).Errors,
        ["encoding"] = () => CompareInput.Run(Definition, [0xFF, 0xFE, 0x41, 0x00], CompareInput.Bytes(CompareInput.GoodCsv)).Errors,
        ["empty"] = () => CompareInput.Run(Definition, string.Empty, CompareInput.GoodCsv).Errors,
        ["csv-syntax"] = () => CompareInput.Run(Definition, "id,n,t\n\"k1,1,a\n", CompareInput.GoodCsv).Errors,
        ["field-count"] = () => CompareInput.Run(Definition, "id,n,t\nk1,1\n", CompareInput.GoodCsv).Errors,
        ["column"] = () => CompareInput.Run(Definition, "id,n\nk1,1\n", CompareInput.GoodCsv).Errors,
        ["duplicate-column"] = () => CompareInput.Run(Definition, "id,n,t,t\nk1,1,a,a\n", CompareInput.GoodCsv).Errors,
        ["empty-key"] = () => CompareInput.Run(Definition, "id,n,t\n,1,a\n", CompareInput.GoodCsv).Errors,
        ["duplicate-key"] = () => CompareInput.Run(Definition, "id,n,t\nk1,1,a\nk1,2,b\n", CompareInput.GoodCsv).Errors,
        ["number"] = () => CompareInput.Run(Definition, "id,n,t\nk1,x,a\n", CompareInput.GoodCsv).Errors,
        ["no-rows"] = () => CompareInput.Run(Definition, "id,n,t\n", CompareInput.GoodCsv).Errors,
    };

    // 오류 코드 표의 코드마다 그 오류를 내는 입력이 있다. 메시지는 표의 메시지 틀 하나와 맞는다("<…>"는 값 자리).
    [Fact]
    public void Every_error_code_in_table_is_produced_with_table_message()
    {
        var table = MetricsSpec.ErrorCodes();
        Assert.Equal(13, table.Count);
        Assert.Equal(table.Select(t => t.Code).Order(StringComparer.Ordinal), Producers.Keys.Order(StringComparer.Ordinal));

        foreach (var (code, templates) in table)
        {
            Assert.NotEmpty(templates);
            var errors = Producers[code]();
            var error = Assert.Single(errors, e => e.Code == code);
            Assert.True(
                templates.Any(t => MetricsSpec.MessagePattern(t).IsMatch(error.Message)),
                $"{code}: 메시지 '{error.Message}'가 표의 틀 [{string.Join(" / ", templates)}] 어디에도 맞지 않는다.");
        }
    }

    // 파일이 정해지기 전 오류(pattern, 폴더·파일 없음, no-rows): Source = case.yaml, Path = inputs.reference·inputs.candidate, 줄·열 0(3.4).
    [Fact]
    public void Errors_before_files_point_to_case_yaml_inputs()
    {
        var errors = new[] { "pattern", "file", "no-rows" }.Select(code => Assert.Single(Producers[code]())).ToArray();
        var candidateSide = Assert.Single(CompareInputs.Read(root, new CaseInputs("candidate/*.csv", "nothing/*.csv", "id")).Errors);

        Assert.Equal(
            [
                new CaseError("case.yaml", 0, 0, "pattern", "inputs.reference", "지원하지 않는 패턴이다. 폴더는 글자 그대로, 마지막 조각에만 '*'·'?'를 쓴다"),
                new CaseError("case.yaml", 0, 0, "file", "inputs.reference", "폴더가 없다: missing"),
                new CaseError("case.yaml", 0, 0, "no-rows", "inputs.reference", "기준 자료에 데이터 행이 없다"),
            ],
            errors);
        Assert.Equal(new CaseError("case.yaml", 0, 0, "file", "inputs.candidate", "폴더가 없다: nothing"), candidateSide);
    }

    // CSV 오류: Source = 케이스 폴더 기준 상대 경로(폴더 포함), Path = 열 이름. 열과 상관없으면 빈 문자열.
    [Fact]
    public void Csv_errors_use_relative_path_and_column_name()
    {
        Assert.Equal(new CaseError("reference/a.csv", 2, 4, "number", "n", "숫자가 아니다: x"), Producers["number"]().Single());
        Assert.Equal(new CaseError("reference/a.csv", 2, 0, "field-count", string.Empty, "칸 수가 머리 행과 다르다 (머리 행 3칸, 이 행 2칸)"), Producers["field-count"]().Single());
        Assert.Equal(new CaseError("reference/a.csv", 1, 0, "column", "t", "머리 행에 열이 없다"), Producers["column"]().Single());
        Assert.Equal(new CaseError("reference/a.csv", 1, 8, "duplicate-column", "t", "머리 행에 같은 열이 둘 이상 있다 (처음: 3번째 칸)"), Producers["duplicate-column"]().Single());
        Assert.Equal(new CaseError("reference/a.csv", 2, 1, "empty-key", "id", "키가 비어 있다"), Producers["empty-key"]().Single());
        Assert.Equal(new CaseError("reference/a.csv", 0, 0, "empty", string.Empty, "머리 행이 없다"), Producers["empty"]().Single());
        Assert.Equal(new CaseError("reference/a.csv", 1, 0, "encoding", string.Empty, "UTF-16이다. UTF-8로 저장한다"), Producers["encoding"]().Single());
        Assert.Equal(new CaseError("reference/a.csv", 2, 1, "csv-syntax", string.Empty, "따옴표가 닫히지 않았다"), Producers["csv-syntax"]().Single());
        Assert.Equal(new CaseError("x/a.csv", 0, 0, "overlap", string.Empty, "같은 파일이 두 번 나온다(기준·결과 패턴이 겹친다)"), Producers["overlap"]().Single());
    }

    // 기준 쪽 → 결과 쪽. 결과 쪽의 case.yaml 오류는 기준 쪽 CSV 오류 뒤다(이름 순이 아니다). 파일은 경로 서수, 넘긴 순서와 상관없다.
    [Fact]
    public void Errors_are_ordered_by_side_then_file_then_position()
    {
        CompareInput.AssertErrors(
            CompareInput.Run(
                Definition,
                [CompareInput.Source("reference/b.csv", "id,n,t\n,1,a\n"), CompareInput.Source("reference/a.csv", "id,n,t\nk1,x,a\nk2,1\n")],
                []),
            "reference/a.csv:2:4: number n: 숫자가 아니다: x",
            "reference/a.csv:3: field-count: 칸 수가 머리 행과 다르다 (머리 행 3칸, 이 행 2칸)",
            "reference/b.csv:2:1: empty-key id: 키가 비어 있다",
            "case.yaml: file inputs.candidate: 일치하는 파일이 없다");

        CompareInput.AssertErrors(
            CompareInput.Run(Definition, [], [CompareInput.Source("candidate/a.csv", "id,n,t\n,1,a\n")]),
            "case.yaml: file inputs.reference: 일치하는 파일이 없다",
            "candidate/a.csv:2:1: empty-key id: 키가 비어 있다");
    }

    // 파일 안에서는 (줄, 열) 순이다. 같은 행의 키 오류(열 1)가 숫자 오류(열 4)보다 앞이다.
    [Fact]
    public void Errors_in_a_file_are_ordered_by_line_and_column()
    {
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "id,n,t\nk1,x,a\nk1,y,a\n", CompareInput.GoodCsv),
            "reference/a.csv:2:4: number n: 숫자가 아니다: x",
            "reference/a.csv:3:1: duplicate-key id: 키가 중복된다: k1 (처음: reference/a.csv:2)",
            "reference/a.csv:3:4: number n: 숫자가 아니다: y");
    }

    // 위치가 같으면 찾은 순서: 머리 행의 열 없음은 키 열, 그다음 compare 순서다.
    [Fact]
    public void Same_position_errors_keep_found_order()
    {
        var definition = CompareInput.WithFields(new CompareField("t", null, null), new CompareField("n", 0m, null));

        CompareInput.AssertErrors(
            CompareInput.Run(definition, "x\nk1\n", "id,n,t\nk1,1,a\n"),
            "reference/a.csv:1: column id: 머리 행에 열이 없다",
            "reference/a.csv:1: column t: 머리 행에 열이 없다",
            "reference/a.csv:1: column n: 머리 행에 열이 없다");
    }

    // 오류는 모두 모은다(한 번에 고칠 수 있게).
    [Fact]
    public void All_errors_are_collected()
    {
        CompareInput.AssertErrors(
            CompareInput.Run(Definition, "id,n,t\nk1,x,a\nk1,1,a\n,2,b\nk4,4\nk5,y,c\n", "id,n\nk1,1\n"),
            "reference/a.csv:2:4: number n: 숫자가 아니다: x",
            "reference/a.csv:3:1: duplicate-key id: 키가 중복된다: k1 (처음: reference/a.csv:2)",
            "reference/a.csv:4:1: empty-key id: 키가 비어 있다",
            "reference/a.csv:5: field-count: 칸 수가 머리 행과 다르다 (머리 행 3칸, 이 행 2칸)",
            "reference/a.csv:6:4: number n: 숫자가 아니다: y",
            "candidate/a.csv:1: column t: 머리 행에 열이 없다");
    }

    // 한 파일에서 encoding·empty·csv-syntax가 나면 그 파일의 나머지는 보지 않는다. 다른 파일은 본다.
    [Fact]
    public void Fatal_file_errors_stop_only_that_file()
    {
        CompareInput.AssertErrors(
            CompareInput.Run(
                Definition,
                [
                    CompareInput.Source("reference/a.csv", "id,n,t\n\"k1\"x,1,a\nk1,y,a\n,z,b\n"),
                    CompareInput.Source("reference/b.csv", "id,n,t\nk2,w,a\n"),
                ],
                [CompareInput.Source("candidate/a.csv", CompareInput.GoodCsv)]),
            "reference/a.csv:2:5: csv-syntax: 닫는 따옴표 뒤에 다른 글자가 있다",
            "reference/b.csv:2:4: number n: 숫자가 아니다: w");
    }

    // 오류가 하나라도 있으면 지표를 내지 않는다(3.1). 결과 쪽 오류 하나여도 같다.
    [Fact]
    public void Any_error_means_no_metrics()
    {
        var result = CompareInput.Run(Definition, CompareInput.GoodCsv, "id,n,t\nk1,1,a\nk1,1,a\n");

        Assert.Null(result.Metrics);
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
    }
}
