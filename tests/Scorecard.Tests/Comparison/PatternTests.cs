using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Tests.CaseSchema;

namespace Scorecard.Tests.Comparison;

// 스펙 4.1 파일 찾기(승인 질문 2·3). 패턴 모양 표·이름 맞추기 표, 임시 폴더로 CompareInputs.Read, 여러 파일.
// 이름·폴더 비교는 서수다. Windows(대소문자 무시 파일 시스템)에서도 같은 결과가 나오게
// 대소문자만 다른 이름 두 개를 한 폴더에 두지 않는다.
[Trait("Spec", "metrics")]
public sealed class PatternTests : IDisposable
{
    private const string PatternMessage = "지원하지 않는 패턴이다. 폴더는 글자 그대로, 마지막 조각에만 '*'·'?'를 쓴다";

    private readonly string root = Directory.CreateTempSubdirectory("scorecard-compare-").FullName;

    public void Dispose() => CaseInput.TryDeleteDirectory(root);

    // 패턴 모양 표의 행마다 IsSupportedPattern.
    [Fact]
    public void Pattern_table_rows_match_IsSupportedPattern()
    {
        var rows = MetricsSpec.Table(MetricsSpec.PatternHeader);
        Assert.NotEmpty(rows);

        var wrong = rows
            .Where(c => CompareInputs.IsSupportedPattern(MetricsSpec.Cell(c[0])) != MetricsSpec.YesNo(c[1]))
            .Select(c => $"{c[0]}: 표 {c[1]}")
            .ToArray();
        Assert.True(wrong.Length == 0, string.Join("\n", wrong));
        Assert.Contains(rows, c => MetricsSpec.YesNo(c[1]));
        Assert.Contains(rows, c => !MetricsSpec.YesNo(c[1]));
    }

    // 이름 맞추기 표의 행마다 Matches.
    [Fact]
    public void Match_table_rows_match_Matches()
    {
        var rows = MetricsSpec.Table(MetricsSpec.MatchHeader);
        Assert.NotEmpty(rows);

        var wrong = rows
            .Where(c => CompareInputs.Matches(MetricsSpec.Cell(c[0]), MetricsSpec.Cell(c[1])) != MetricsSpec.YesNo(c[2]))
            .Select(c => $"{c[0]} {c[1]}: 표 {c[2]}")
            .ToArray();
        Assert.True(wrong.Length == 0, string.Join("\n", wrong));
    }

    // 지원 밖 패턴은 파일을 찾지 않고 pattern 오류. Source = case.yaml, Path = inputs.reference·inputs.candidate, 줄·열 0(3.4).
    [Fact]
    public void Unsupported_patterns_give_pattern_error_on_case_yaml()
    {
        var unsupported = MetricsSpec.Table(MetricsSpec.PatternHeader).Where(c => !MetricsSpec.YesNo(c[1])).Select(c => MetricsSpec.Cell(c[0])).ToArray();
        Assert.NotEmpty(unsupported);
        Write("reference/a.csv", "id,v\nk,1\n");

        foreach (var pattern in unsupported)
        {
            AssertReadErrors(new CaseInputs(pattern, "reference/a.csv", "id"), $"case.yaml: pattern inputs.reference: {PatternMessage}");
            AssertReadErrors(new CaseInputs("reference/a.csv", pattern, "id"), $"case.yaml: pattern inputs.candidate: {PatternMessage}");
        }

        var error = Assert.Single(CompareInputs.Read(root, new CaseInputs("ref*/a.csv", "reference/a.csv", "id")).Errors);
        Assert.Equal(new CaseError(CaseParser.FileName, 0, 0, "pattern", "inputs.reference", PatternMessage), error);
    }

    // 찾은 파일은 이름 서수 순이다. '.'·'~$'로 시작하는 이름도 맞으면 읽는다. 폴더·하위 폴더는 보지 않는다.
    // 경로는 케이스 폴더 기준 상대 경로('/' 구분). 내용은 파일 바이트 그대로다.
    [Fact]
    public void Read_lists_matching_files_in_ordinal_order_without_subfolders()
    {
        foreach (var name in new[] { "b.csv", "a.csv", "C.CSV", "_x.csv", ".hidden.csv", "~$a.csv", "x.csvx", "notes.txt", "sub/c.csv" })
        {
            Write("reference/" + name, $"id,v\n{name},1\n");
        }

        Directory.CreateDirectory(Path.Combine(root, "reference", "dir.csv"));
        Write("candidate/a.csv", "id,v\nk,1\n");

        var sources = Valid(new CaseInputs("reference/*.csv", "candidate/*.csv", "id"));

        Assert.Equal(
            ["reference/.hidden.csv", "reference/_x.csv", "reference/a.csv", "reference/b.csv", "reference/~$a.csv"],
            sources.Reference.Select(f => f.Path));
        Assert.Equal(["candidate/a.csv"], sources.Candidate.Select(f => f.Path));
        Assert.All(sources.Reference.Concat(sources.Candidate), f => Assert.Equal(File.ReadAllBytes(Path.Combine(root, f.Path)), f.Content));
    }

    // 폴더 조각은 글자 그대로. 하위 폴더를 패턴에 적으면 그 폴더를 본다. 파일 이름만 적어도 된다. 케이스 폴더 바로 아래도 된다.
    [Fact]
    public void Read_follows_literal_folder_segments()
    {
        Write("reference/sub/c.csv", "id,v\nk,1\n");
        Write("reference/deep/er/z.csv", "id,v\nk,1\n");
        Write("reference/logs.csv", "id,v\nk,1\n");
        Write("root.csv", "id,v\nk,1\n");
        Write("case.yaml", "name: p\n");

        Assert.Equal(["reference/sub/c.csv"], Valid(new CaseInputs("reference/sub/*.csv", "root.csv", "id")).Reference.Select(f => f.Path));
        Assert.Equal(["reference/deep/er/z.csv"], Valid(new CaseInputs("reference/deep/er/*.csv", "root.csv", "id")).Reference.Select(f => f.Path));
        Assert.Equal(["reference/logs.csv"], Valid(new CaseInputs("reference/logs.csv", "root.csv", "id")).Reference.Select(f => f.Path));
        Assert.Equal(["root.csv"], Valid(new CaseInputs("reference/logs.csv", "*.csv", "id")).Candidate.Select(f => f.Path));
    }

    // 폴더 이름도 서수로 찾는다. 폴더가 Candidate이고 패턴이 candidate/*.csv면 대소문자를 무시하는 파일 시스템에서도 폴더가 없다.
    [Fact]
    public void Folder_name_is_matched_ordinally()
    {
        Write("reference/a.csv", "id,v\nk,1\n");
        Write("Candidate/a.csv", "id,v\nk,1\n");

        AssertReadErrors(new CaseInputs("reference/a.csv", "candidate/*.csv", "id"), "case.yaml: file inputs.candidate: 폴더가 없다: candidate");
    }

    // 맞는 파일이 없으면 "일치하는 파일이 없다". 폴더가 없으면 "폴더가 없다: <폴더>"(케이스 폴더 기준 상대 경로).
    [Fact]
    public void Missing_files_and_folders_give_file_errors()
    {
        Write("reference/a.csv", "id,v\nk,1\n");
        Write("reference/A.TXT", "x\n");

        AssertReadErrors(new CaseInputs("reference/*.json", "reference/a.csv", "id"), "case.yaml: file inputs.reference: 일치하는 파일이 없다");
        AssertReadErrors(new CaseInputs("reference/*.txt", "reference/a.csv", "id"), "case.yaml: file inputs.reference: 일치하는 파일이 없다");
        AssertReadErrors(new CaseInputs("reference/A.csv", "reference/a.csv", "id"), "case.yaml: file inputs.reference: 일치하는 파일이 없다");
        AssertReadErrors(new CaseInputs("missing/*.csv", "reference/a.csv", "id"), "case.yaml: file inputs.reference: 폴더가 없다: missing");
        AssertReadErrors(new CaseInputs("reference/nope/*.csv", "reference/a.csv", "id"), "case.yaml: file inputs.reference: 폴더가 없다: reference/nope");
        AssertReadErrors(
            new CaseInputs("missing/*.csv", "candidate/*.csv", "id"),
            "case.yaml: file inputs.reference: 폴더가 없다: missing",
            "case.yaml: file inputs.candidate: 폴더가 없다: candidate");
    }

    // 5절: 케이스 폴더가 없으면 양쪽 모두 "케이스 폴더가 없다". 줄·열은 0이고 절대 경로가 없다.
    [Fact]
    public void Missing_case_directory_gives_error_on_both_sides()
    {
        var missing = Path.Combine(root, "no-such-case");
        var read = CompareInputs.Read(missing, new CaseInputs("reference/*.csv", "candidate/*.csv", "id"));

        Assert.Null(read.Sources);
        Assert.False(read.IsValid);
        Assert.Equal(
            ["case.yaml: file inputs.reference: 케이스 폴더가 없다", "case.yaml: file inputs.candidate: 케이스 폴더가 없다"],
            CompareInput.Lines(read.Errors));
        Assert.All(read.Errors, e => Assert.DoesNotContain(root, e.ToString(), StringComparison.Ordinal));
    }

    // CompareDirectory도 같은 오류를 지표 없이 돌려준다(3.1).
    [Fact]
    public void CompareDirectory_returns_read_errors_without_metrics()
    {
        Write("reference/a.csv", "id,v\nk,1\n");
        var definition = CompareInput.Case(CompareInput.TextCase);

        CompareInput.AssertErrors(CompareEngine.CompareDirectory(root, definition), "case.yaml: file inputs.candidate: 폴더가 없다: candidate");
    }

    // 같은 파일이 기준·결과 양쪽에 나오면(패턴이 겹침) overlap. 둘째 파일(결과 쪽)에 건다.
    [Fact]
    public void Overlapping_patterns_give_overlap_error()
    {
        Write("reference/a.csv", "id,v\nk1,a\n");
        Write("reference/b.csv", "id,v\nk2,b\n");
        var definition = CompareInput.Case(CompareInput.TextCase) with { Inputs = new CaseInputs("reference/a.csv", "reference/*.csv", "id") };

        CompareInput.AssertErrors(
            CompareEngine.CompareDirectory(root, definition),
            "reference/a.csv: overlap: 같은 파일이 두 번 나온다(기준·결과 패턴이 겹친다)");
    }

    private CompareSources Valid(CaseInputs inputs)
    {
        var read = CompareInputs.Read(root, inputs);
        Assert.True(read.IsValid, string.Join("\n", CompareInput.Lines(read.Errors)));
        Assert.Empty(read.Errors);
        return read.Sources!;
    }

    private void AssertReadErrors(CaseInputs inputs, params string[] expected)
    {
        var read = CompareInputs.Read(root, inputs);

        Assert.Equal(expected, CompareInput.Lines(read.Errors));
        Assert.False(read.IsValid);
        Assert.Null(read.Sources);
        Assert.All(read.Errors, e => Assert.Equal((0, 0), (e.Line, e.Column)));
    }

    private void Write(string relative, string text)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, CompareInput.Bytes(text));
    }
}
