using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Scorecard.Core.Cases;
using Scorecard.Core.Comparison;
using Scorecard.Core.Synthetic;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Guards;

namespace Scorecard.Tests.Comparison;

// 스펙 4.10 G2·G3 등록(승인 질문 18). 카드 수용 기준 4. G2·G3 검사 자체는 CoreGuardTargetTests가 GuardTargets 전부에 돈다.
// 여기서는 등록 내용, 표본 파일(6.4 블록, T-005 고정 계획 CSV), 어댑터 출력 모양, 표본이 대조 기능을 고루 쓰는지(빈 통과 방지),
// G2 탐지력(음성 대조)을 본다.
[Trait("Spec", "metrics")]
public class CompareGuardTargetTests
{
    private const string ValidTarget = "compare-case-valid";
    private const string InvalidTarget = "compare-invalid";

    // 4.10 표의 이름·샘플이 GuardTargets에 있다. 어댑터 칸의 출력 파일 이름도 같다.
    [Fact]
    public void Spec_guard_table_entries_are_registered()
    {
        var rows = MetricsSpec.Table(MetricsSpec.GuardHeader);

        Assert.Equal([ValidTarget, InvalidTarget], rows.Select(c => MetricsSpec.Cell(c[0])));
        foreach (var cells in rows)
        {
            Assert.Equal(SpecDocument.CodeSpans(cells[1])[0], GuardTargets.Get(MetricsSpec.Cell(cells[0])).Sample);
        }

        Assert.Contains($"`{MetricsJson.FileName}`", rows[0][2], StringComparison.Ordinal);
        Assert.Contains($"`{GuardTargets.CompareErrorsFile}`", rows[1][2], StringComparison.Ordinal);
    }

    // case-valid의 두 CSV는 T-005 G2 고정 계획(GuardTargets.SyntheticGuardPlan)으로 SyntheticGenerator가 만든 바이트다.
    // SHA-256 앞자리는 4.10 줄의 값이다(`66d83729…`, `9be987a9…`).
    [Fact]
    public void Case_valid_csv_files_are_the_fixed_guard_plan_output()
    {
        var sample = GuardWorkspace.SamplePath("case-valid");
        var tables = SyntheticGenerator.Generate(CaseInput.Valid(CaseParser.ParseDirectory(sample)), GuardTargets.SyntheticGuardPlan);
        var spans = SpecDocument.CodeSpans(SpecDocument.Line(MetricsSpec.Document, MetricsSpec.GuardCsvLinePrefix));

        foreach (var (folder, rows) in new[] { (SyntheticCsv.ReferenceFolder, tables.Reference), (SyntheticCsv.CandidateFolder, tables.Candidate) })
        {
            var relative = $"{folder}/{tables.FileName}";
            var path = Path.Combine(sample, folder, tables.FileName);
            Assert.True(File.Exists(path), $"{GuardWorkspace.SamplesRelativePath}/case-valid/{relative}가 없다.");
            var bytes = File.ReadAllBytes(path);

            Assert.Equal(SyntheticCsv.Render(tables.Header, rows), bytes);
            var prefix = spans[Array.IndexOf(spans, relative) + 1];
            Assert.EndsWith("…", prefix, StringComparison.Ordinal);
            Assert.StartsWith(prefix.TrimEnd('…'), Convert.ToHexStringLower(SHA256.HashData(bytes)), StringComparison.Ordinal);
        }

        Assert.Equal(
            ["candidate/rows.csv", "case.yaml", "reference/rows.csv"],
            Directory.EnumerateFiles(sample, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(sample, f).Replace(Path.DirectorySeparatorChar, '/'))
                .Order(StringComparer.Ordinal));
    }

    // compare-invalid 표본 = 6.4의 파일 블록(바이트 그대로). 폴더에는 블록의 파일만 있다.
    [Fact]
    public void Compare_invalid_sample_equals_spec_blocks()
    {
        var blocks = MetricsSpec.LabeledBlocks(MetricsSpec.ErrorExampleHeading)
            .Where(b => b.Label is not null && b.Label.StartsWith(MetricsSpec.ErrorSamplePrefix, StringComparison.Ordinal))
            .Select(b => (Relative: b.Label![MetricsSpec.ErrorSamplePrefix.Length..], b.Content))
            .ToArray();
        Assert.Equal(5, blocks.Length);

        var sample = GuardWorkspace.SamplePath(InvalidTarget);
        foreach (var (relative, content) in blocks)
        {
            var path = Path.Combine(sample, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"{GuardWorkspace.SamplesRelativePath}/{InvalidTarget}/{relative}가 없다.");
            Assert.True(Encoding.UTF8.GetBytes(content).AsSpan().SequenceEqual(File.ReadAllBytes(path)), $"{relative}가 스펙 6.4 블록과 다르다.");
        }

        Assert.Equal(
            blocks.Select(b => b.Relative).Order(StringComparer.Ordinal),
            Directory.EnumerateFiles(sample, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(sample, f).Replace(Path.DirectorySeparatorChar, '/'))
                .Order(StringComparer.Ordinal));
    }

    // 정상 표본 어댑터 출력은 metrics.json 한 파일. UTF-8(BOM 없음), LF, 끝에 \n. 작업 폴더 경로가 없다.
    // 4.10: 바이트는 Python 독립 구현과 같았다(SHA-256 `c5906d45…`).
    [Fact]
    public void Valid_adapter_writes_single_metrics_file_without_paths()
    {
        var bytes = RunSingle(ValidTarget, MetricsJson.FileName, out var workspaceRoot);

        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "BOM이 없어야 한다.");
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal((byte)'\n', bytes[^1]);
        AssertNoPath(bytes, workspaceRoot);

        // 4.10 줄의 '…'로 끝나는 SHA-256 앞자리는 차례로 reference/rows.csv, candidate/rows.csv, metrics.json이다.
        var line = SpecDocument.Line(MetricsSpec.Document, MetricsSpec.GuardCsvLinePrefix);
        var prefixes = SpecDocument.CodeSpans(line).Where(s => s.EndsWith('…')).ToArray();
        Assert.Equal(3, prefixes.Length);
        Assert.StartsWith(prefixes[2].TrimEnd('…'), Convert.ToHexStringLower(SHA256.HashData(bytes)), StringComparison.Ordinal);
        Assert.Contains($"{bytes.Length.ToString("N0", CultureInfo.InvariantCulture)}바이트)", line, StringComparison.Ordinal);
    }

    // 오류 표본 어댑터 출력은 compare-errors.txt 한 파일 = 6.4 오류 블록(7건). 작업 폴더 경로가 없다.
    [Fact]
    public void Invalid_adapter_writes_error_lines_equal_to_spec_block()
    {
        var bytes = RunSingle(InvalidTarget, GuardTargets.CompareErrorsFile, out var workspaceRoot);
        var block = Assert.Single(MetricsSpec.LabeledBlocks(MetricsSpec.ErrorExampleHeading), b => b.Label is null).Content;

        Assert.Equal(block, CompareInput.Text(bytes));
        Assert.Equal(7, SpecDocument.BlockLines(block).Length);
        AssertNoPath(bytes, workspaceRoot);
    }

    // 4.10: 정상 표본은 심은 구조를 그대로 낸다. 12행, 일치 8(허용오차 안 차이 2), 값 불일치 3(start·mean·label 하나씩),
    // 누락 1, 추가 1, 정확도 66.7, 누락률 8.3. 표본이 바뀌어 대조 기능 일부만 쓰게 되면 G2·G3가 약해지므로 여기서 알린다.
    [Fact]
    public void Valid_sample_exercises_all_item_statuses_and_results()
    {
        var sample = GuardWorkspace.SamplePath("case-valid");
        var metrics = CompareInput.Measured(CompareEngine.CompareDirectory(sample, CaseInput.Valid(CaseParser.ParseDirectory(sample))));

        Assert.Equal("ref=12 cand=12 match=8 mismatch=3 missing=1 extra=1 within=2 acc=66.7 miss=8.3", CompareInput.Counts(metrics));
        Assert.Equal(
            [CompareEngine.ItemExtra, CompareEngine.ItemMatch, CompareEngine.ItemMismatch, CompareEngine.ItemMissing],
            metrics.Items.Select(i => i.Status).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(
            ["label", "mean", "start"],
            metrics.Items.Where(i => i.Status == CompareEngine.ItemMismatch).SelectMany(i => i.Differences).Where(d => d.Result == CompareEngine.ResultOutside).Select(d => d.Field).Order(StringComparer.Ordinal));
        Assert.Equal([CompareEngine.KindNumber, CompareEngine.KindNumber, CompareEngine.KindText], metrics.Fields.Select(f => f.Kind));
        Assert.Contains(metrics.Items, i => i.Status == CompareEngine.ItemMatch && i.Differences.Any(d => d.Result == CompareEngine.ResultWithin));
    }

    // 오류 표본은 6.4의 코드 여섯 가지를 모두 낸다(숫자 아님·키 중복·빈 키·칸 수·따옴표·열 없음).
    [Fact]
    public void Invalid_sample_exercises_error_codes()
    {
        var sample = GuardWorkspace.SamplePath(InvalidTarget);
        var result = CompareEngine.CompareDirectory(sample, CaseInput.Valid(CaseParser.ParseDirectory(sample)));

        Assert.Null(result.Metrics);
        Assert.Equal(
            ["column", "csv-syntax", "duplicate-key", "empty-key", "field-count", "number"],
            result.Errors.Select(e => e.Code).Distinct().Order(StringComparer.Ordinal));
        Assert.All(result.Errors, e => Assert.False(Path.IsPathRooted(e.Source), e.ToString()));
    }

    // 4.10 음성 대조(S6 b): 어댑터가 SourceFile.Path에 절대 경로를 넣으면 G2 R3가 잡는다(정상·오류 표본 모두).
    [Theory]
    [InlineData("case-valid", MetricsJson.FileName)]
    [InlineData(InvalidTarget, GuardTargets.CompareErrorsFile)]
    public void Absolute_source_path_is_caught_by_G2(string sample, string file)
    {
        var leaking = new GuardTarget("compare-absolute-path", sample, (input, output) =>
        {
            var definition = CaseInput.Valid(CaseParser.ParseDirectory(input));
            var read = CompareInputs.Read(input, definition.Inputs!);
            var sources = read.Sources!;
            CompareSources Absolute(CompareSources s) => new(
                s.Reference.Select(f => f with { Path = Path.GetFullPath(Path.Combine(input, f.Path)) }).ToArray(),
                s.Candidate.Select(f => f with { Path = Path.GetFullPath(Path.Combine(input, f.Path)) }).ToArray());
            Write(output, CompareEngine.Compare(definition, Absolute(sources)));
        });

        Assert.Contains($"G2 R3 다른 경로: out/{file} SHA-256 다름", GuardChecks.Determinism(leaking));
    }

    // 4.10 음성 대조(S6 a의 테스트 쪽 판): 결과 자료의 소수 칸을 현재 문화권으로 다시 써서 넘기면 G2 R4(de-DE)가 잡는다.
    [Fact]
    public void Culture_dependent_input_is_caught_by_G2()
    {
        var leaking = new GuardTarget("compare-culture-rewrite", "case-valid", (input, output) =>
        {
            var definition = CaseInput.Valid(CaseParser.ParseDirectory(input));
            var sources = CompareInputs.Read(input, definition.Inputs!).Sources!;
            var candidate = sources.Candidate
                .Select(f => f with
                {
                    Content = CompareInput.Bytes(string.Join("\n", CompareInput.Text(f.Content).Split('\n').Select(line => string.Join(",", line.Split(',').Select(Rewrite))))),
                })
                .ToArray();
            Write(output, CompareEngine.Compare(definition, new CompareSources(sources.Reference, candidate)));
        });

        var differences = GuardChecks.Determinism(leaking);

        Assert.NotEmpty(differences);
        Assert.All(differences, d => Assert.StartsWith("G2 R4 문화권: out/", d, StringComparison.Ordinal));
    }

    // 소수 칸("50.71")을 InvariantCulture로 읽어 현재 문화권으로 쓴다. de-DE에서는 "50,71"이 된다.
    private static string Rewrite(string cell) =>
        cell.Contains('.', StringComparison.Ordinal) && decimal.TryParse(cell, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            ? value.ToString(CultureInfo.CurrentCulture)
            : cell;

    // GuardTargets 어댑터와 같은 모양으로 쓴다(지표면 metrics.json, 오류면 compare-errors.txt).
    private static void Write(string output, CompareResult result)
    {
        Directory.CreateDirectory(output);
        if (result.Metrics is { } metrics)
        {
            File.WriteAllBytes(Path.Combine(output, MetricsJson.FileName), MetricsJson.Render(metrics));
        }
        else
        {
            File.WriteAllBytes(Path.Combine(output, GuardTargets.CompareErrorsFile), GuardTargets.ErrorLines(result.Errors));
        }
    }

    // 대상을 작업 폴더에서 돌리고 출력 폴더의 파일이 expectedFile 하나인지 본다. 그 바이트를 돌려준다.
    private static byte[] RunSingle(string name, string expectedFile, out string workspaceRoot)
    {
        var target = GuardTargets.Get(name);
        using var workspace = GuardWorkspace.Create(target.Sample, reverseCopy: false);
        var output = workspace.PathOf("out");

        target.Run(workspace.Input, output);

        var files = Directory.EnumerateFileSystemEntries(output, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(output, f))
            .ToArray();
        Assert.Equal([expectedFile], files);
        workspaceRoot = workspace.Root;
        return File.ReadAllBytes(Path.Combine(output, expectedFile));
    }

    // 출력에 작업 폴더 경로가 없다. JSON은 '\'를 '\\'로 쓰므로 임시 폴더 이름 조각(무작위 이름)도 찾는다.
    private static void AssertNoPath(byte[] bytes, string workspaceRoot)
    {
        var text = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain(workspaceRoot, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetFileName(Path.TrimEndingDirectorySeparator(workspaceRoot)), text, StringComparison.Ordinal);
    }
}
