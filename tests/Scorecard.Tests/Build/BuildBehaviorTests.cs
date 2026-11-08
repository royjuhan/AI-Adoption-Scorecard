using System.Globalization;
using System.Text;
using System.Text.Json;
using Scorecard.Core.Build;
using Scorecard.Core.Comparison;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Cli;
using Scorecard.Tests.Guards;

namespace Scorecard.Tests.Build;

// build의 동작: 사람 확인(4.5), 결정론·원본 불변(4.7), 번들 밖 파일(4.6), 추천만(2절).
[Trait("Spec", "build-request")]
public class BuildBehaviorTests
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    // 4.5 상태 표: 상태마다 CheckExport의 상태·코드·메시지가 표와 같다. confirmed는 오류가 없다.
    [Fact]
    public void Export_check_statuses_follow_table()
    {
        var table = BuildInput.Table(BuildInput.StatusTableHeader).ToDictionary(r => SpecDocument.Unquote(r[0]), r => r, StringComparer.Ordinal);
        Assert.Equal(["confirmed", "missing", "unsigned", "changed", "hits"], table.Keys);

        var bundle = new BuildRequestBundle("c", 1, "s", [new SourceFile("spec.md", Utf8.GetBytes("# 합성\n"))], [], Utf8.GetBytes("# 확인\n\n확인한 사람:\n"));
        var cases = new (string Status, BuildRequestBundle Bundle, string? File)[]
        {
            ("confirmed", bundle, "# 확인\n\n확인한 사람: 검토자\n"),
            ("missing", bundle, null),
            ("unsigned", bundle, "# 확인\n\n확인한 사람:   \n"),
            ("changed", bundle, "# 확인 바꿈\n\n확인한 사람: 검토자\n"),
            ("hits", bundle with { Hits = [new ScanHit("email", "spec.md", 1, "x")] }, "# 확인\n\n확인한 사람: 검토자\n"),
        };

        foreach (var (status, b, file) in cases)
        {
            var row = table[status];
            var check = BuildRequestBuilder.CheckExport(b, file is null ? null : Utf8.GetBytes(file));
            Assert.Equal(status, check.Status);
            if (status == "confirmed")
            {
                Assert.True(check.IsConfirmed);
                Assert.Equal("검토자", check.Signer);
                Assert.Equal("—", row[2]);
                continue;
            }

            Assert.False(check.IsConfirmed);
            Assert.Equal(CliInput.Cell(row[2]), check.Error!.Code);
            Assert.Equal("export-check.md", check.Error.Source);
            Assert.Equal(CliInput.Cell(row[3]).Replace("{count}", "1", StringComparison.Ordinal), check.Error.Message);
        }
    }

    // 확인 줄이 둘이면(또는 없으면) changed. BOM·CRLF는 맞춘 뒤 비교한다(확인으로 본다).
    [Fact]
    public void Signature_line_must_be_single_and_line_endings_are_normalized()
    {
        var bundle = new BuildRequestBundle("c", 1, "s", [], [], Utf8.GetBytes("# 확인\n\n확인한 사람:\n"));

        Assert.Equal("changed", BuildRequestBuilder.CheckExport(bundle, Utf8.GetBytes("# 확인\n확인한 사람: a\n확인한 사람: b\n")).Status);
        Assert.Equal("changed", BuildRequestBuilder.CheckExport(bundle, Utf8.GetBytes("# 확인\n\n")).Status);
        Assert.Equal("confirmed", BuildRequestBuilder.CheckExport(bundle, [0xEF, 0xBB, 0xBF, .. Utf8.GetBytes("# 확인\r\n\r\n확인한 사람: 검토자\r\n")]).Status);
    }

    // 걸린 문자열이 있으면(답 파일 task에 심은 이메일) 확인 줄을 적어도 내보내지 않는다. 초안에 걸린 것을 적는다.
    [Fact]
    public void Planted_value_blocks_export_even_after_signing()
    {
        using var work = CliWorkspace.Create("form-fill");
        var answers = Path.Combine(work.Case, "answers.yaml");
        var planted = "someone" + "@" + "example.com";
        var lines = File.ReadAllLines(answers, Utf8).Select(l => l.StartsWith("task:", StringComparison.Ordinal) ? "task: 주문서 집계 " + planted : l);
        File.WriteAllText(answers, string.Join('\n', lines) + "\n", Utf8);
        var output = BuildInput.Prepare(work, work.PathOf("o"));

        var first = CliInput.Run("build", work.Case, "--step", "2", "--out", output, "--json");
        Assert.Equal(2, first.Exit);
        var draft = BuildInput.Text(File.ReadAllBytes(Path.Combine(output, "export-check.md")));
        Assert.Contains("- `email` `spec.md`:3: `" + planted + "`", draft, StringComparison.Ordinal);
        Assert.Contains("- `email` `context.json`:", draft, StringComparison.Ordinal);

        BuildInput.Sign(output);
        var again = CliInput.Run("build", work.Case, "--step", "2", "--out", output, "--json");
        Assert.Equal(2, again.Exit);
        Assert.Contains("검사에 걸린 문자열이 2개 있다", again.Out, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(output, "build-request")));
    }

    // 확인 뒤 답이 바뀌면 옛 확인은 무효(changed)다. 새 초안을 쓰고 번들은 그대로 둔다.
    [Fact]
    public void Changed_answers_invalidate_the_old_confirmation()
    {
        using var work = CliWorkspace.Create("table-compare");
        var output = BuildInput.Prepare(work, work.PathOf("o"));
        Assert.Equal(2, CliInput.Run("build", work.Case, "--step", "1", "--out", output, "--json").Exit);
        BuildInput.Sign(output);
        Assert.Equal(0, CliInput.Run("build", work.Case, "--step", "1", "--out", output, "--json").Exit);
        var bundle = BuildInput.Files(Path.Combine(output, "build-request"));

        var answers = Path.Combine(work.Case, "answers.yaml");
        File.WriteAllText(answers, File.ReadAllText(answers, Utf8).Replace("batch_size: 30", "batch_size: 31", StringComparison.Ordinal), Utf8);
        Assert.Equal(0, CliInput.Run("start", work.Case, "--answers", answers, "--out", output, "--json").Exit);
        var run = CliInput.Run("build", work.Case, "--step", "1", "--out", output, "--json");

        Assert.Equal(2, run.Exit);
        Assert.Contains("export-check.md가 지금 번들과 다르다", run.Out, StringComparison.Ordinal);
        Assert.EndsWith("\n확인한 사람:\n", BuildInput.Text(File.ReadAllBytes(Path.Combine(output, "export-check.md"))), StringComparison.Ordinal);
        var after = BuildInput.Files(Path.Combine(output, "build-request"));
        Assert.Equal(bundle.Keys, after.Keys);
        Assert.All(bundle, b => Assert.Equal(b.Value, after[b.Key]));
    }

    // 사람이 CRLF·BOM으로 저장한 확인 파일도 받는다. 번들의 export-check.md는 받은 바이트 그대로다.
    [Fact]
    public void Crlf_signed_file_is_accepted_and_copied_as_is()
    {
        using var work = CliWorkspace.Create("form-fill");
        var output = BuildInput.Prepare(work, work.PathOf("o"));
        Assert.Equal(2, CliInput.Run("build", work.Case, "--step", "1", "--out", output, "--json").Exit);
        BuildInput.Sign(output);
        var path = Path.Combine(output, "export-check.md");
        byte[] crlf = [0xEF, 0xBB, 0xBF, .. Utf8.GetBytes(BuildInput.Text(File.ReadAllBytes(path)).Replace("\n", "\r\n", StringComparison.Ordinal))];
        File.WriteAllBytes(path, crlf);

        Assert.Equal(0, CliInput.Run("build", work.Case, "--step", "1", "--out", output, "--json").Exit);
        Assert.Equal(crlf, File.ReadAllBytes(Path.Combine(output, "build-request", "export-check.md")));
    }

    // build-request/에 번들 밖 파일이 있으면 output 오류. 지우지 않는다.
    [Fact]
    public void Stray_file_in_bundle_folder_is_an_output_error()
    {
        using var work = CliWorkspace.Create("form-fill");
        var output = BuildInput.Prepare(work, work.PathOf("o"));
        Assert.Equal(2, CliInput.Run("build", work.Case, "--step", "1", "--out", output, "--json").Exit);
        BuildInput.Sign(output);
        var stray = Path.Combine(output, "build-request", "old.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(stray)!);
        File.WriteAllText(stray, "x");

        var run = CliInput.Run("build", work.Case, "--step", "1", "--out", output, "--json");

        Assert.Equal(2, run.Exit);
        using var doc = JsonDocument.Parse(run.Stdout);
        var error = Assert.Single(doc.RootElement.GetProperty("errors").EnumerateArray());
        Assert.Equal("output", error.GetProperty("code").GetString());
        Assert.Equal("build-request 폴더에 번들 밖 파일이 있다: old.txt. 폴더를 비우고 다시 돌린다", error.GetProperty("message").GetString());
        Assert.True(File.Exists(stray));
        Assert.False(File.Exists(Path.Combine(output, "build-request", "spec.md")));
    }

    // 추천만(--step 없음)은 아무것도 쓰지 않는다. ② 단계가 없으면 proposal null, candidates [].
    [Fact]
    public void Choice_writes_nothing_and_handles_no_software_step()
    {
        using var work = CliWorkspace.Create("mixed-exceptions");
        var answers = Path.Combine(work.Case, "answers.yaml");
        File.WriteAllText(answers, File.ReadAllText(answers, Utf8).Replace("human_signoff: no", "human_signoff: yes", StringComparison.Ordinal), Utf8);
        var output = work.PathOf("o");
        Assert.Equal(0, CliInput.Run("start", work.Case, "--answers", answers, "--out", output, "--json").Exit);
        var before = FolderSnapshot.Take(output);

        var run = CliInput.Run("build", work.Case, "--out", output, "--json");
        var text = CliInput.Run("build", work.Case, "--out", output);

        Assert.Equal(0, run.Exit);
        using var doc = JsonDocument.Parse(run.Stdout);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("proposal").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("step").ValueKind);
        Assert.Equal(0, doc.RootElement.GetProperty("candidates").GetArrayLength());
        Assert.Equal(0, text.Exit);
        Assert.Equal("만들 ② 단계가 없다. 아무것도 쓰지 않았다.\n", text.Out);
        Assert.Empty(FolderSnapshot.Compare(before, FolderSnapshot.Take(output), compareTime: true));
    }

    // 결정론: 다른 경로의 사본, de-DE 문화권에서 같은 흐름 → 같은 표준 출력과 같은 번들 바이트. 경로가 출력에 없다.
    [Theory]
    [InlineData("table-compare")]
    [InlineData("mixed-exceptions")]
    public void Same_input_gives_same_bundle_across_paths_and_cultures(string example)
    {
        var first = Flow(example, null);
        var german = Flow(example, CultureInfo.GetCultureInfo("de-DE"));

        Assert.Equal(first, german);
    }

    // 원본 불변: 기본 출력 폴더로 흐름을 돌려도 케이스 폴더는 out/ 말고 그대로다.
    [Fact]
    public void Case_folder_is_unchanged_except_out()
    {
        using var work = CliWorkspace.Create("signoff-final");
        var before = FolderSnapshot.Take(work.Case);
        var output = Path.Combine(work.Case, "out");
        Assert.Equal(0, CliInput.Run("start", work.Case, "--answers", Path.Combine(work.Case, "answers.yaml"), "--json").Exit);
        Assert.Equal(0, CliInput.Run("check", work.Case, "--json").Exit);
        Assert.Equal(0, CliInput.Run("build", work.Case, "--json").Exit);
        Assert.Equal(2, CliInput.Run("build", work.Case, "--step", "3", "--json").Exit);
        BuildInput.Sign(output);
        Assert.Equal(0, CliInput.Run("build", work.Case, "--step", "3", "--json").Exit);

        Assert.Empty(FolderSnapshot.Compare(before, FolderSnapshot.Take(work.Case, exclude: "out"), compareTime: true));
        Assert.True(File.Exists(Path.Combine(output, "build-request", "spec.md")));
    }

    private static string[] Flow(string example, CultureInfo? culture)
    {
        var saved = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            if (culture is not null)
            {
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }

            using var work = CliWorkspace.Create(example);
            var output = BuildInput.Prepare(work, work.PathOf("o"));
            var step = BuildInput.ProposedStep(example).ToString(CultureInfo.InvariantCulture);
            var runs = new List<CliRun> { CliInput.Run("build", work.Case, "--out", output, "--json") };
            runs.Add(CliInput.Run("build", work.Case, "--step", step, "--out", output, "--json"));
            BuildInput.Sign(output);
            runs.Add(CliInput.Run("build", work.Case, "--step", step, "--out", output, "--json"));
            var files = BuildInput.Files(output).Select(f => f.Key + "\n" + BuildInput.Text(f.Value));
            var text = runs.Select(r => r.Exit + "\n" + r.Out + r.Err).Concat(files).ToArray();
            Assert.All(text, t => Assert.DoesNotContain(work.Root, t, StringComparison.Ordinal));
            return text;
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = saved;
        }
    }

}
