using System.Security.Cryptography;
using System.Text;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Classification;
using Scorecard.Tests.Structure;
using Scorecard.Tests.TextCheck;

namespace Scorecard.Tests.Synthetic;

// 스펙 7절 블록 = 예제 파일(승인 결정 12·13), 3.2 예제 폴더, 4.12 examples/ 관리. 카드 수용 기준 3·4.
// 블록은 7.1 파일 표 순서다. 블록 줄마다 LF, 끝에 LF(SpecDocument.CodeBlocks). 파일이 없으면 실패한다.
[Trait("Spec", "synthetic-examples")]
public class ExampleFileTests
{
    public static TheoryData<string, string> SpecFiles
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var (example, file) in SyntheticSpec.Files())
            {
                data.Add(example, file);
            }

            return data;
        }
    }

    // 7.1 파일 표의 예제는 공개 예제 4종이고, 7.2~7.5 제목이 있다(이름의 "three"는 T-009 L4로 그대로 둔다).
    [Fact]
    public void File_table_covers_the_three_examples()
    {
        Assert.Equal(ClassificationInput.Examples, SyntheticSpec.Files().Select(f => f.Example).Distinct());
        Assert.Equal(ClassificationInput.Examples.Order(StringComparer.Ordinal), SyntheticSpec.ExampleHeadings.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(SyntheticSpec.Files().Length, SyntheticSpec.Files().Distinct().Count());
    }

    // 7.2~7.4의 블록 수 = 7.1 표의 그 예제 파일 수. 블록 i = 표의 i번째 파일(바이트 그대로).
    [Theory]
    [MemberData(nameof(SyntheticInput.AllExampleNames), MemberType = typeof(SyntheticInput))]
    public void Example_files_equal_spec_blocks(string example)
    {
        var files = SyntheticSpec.FilesOf(example);
        var blocks = SpecDocument.CodeBlocks(SyntheticSpec.Document, SyntheticSpec.ExampleHeadings[example]);

        Assert.Equal(files.Length, blocks.Count);
        for (var i = 0; i < files.Length; i++)
        {
            var actual = SyntheticInput.ExampleBytes(example, files[i]);
            Assert.True(
                Encoding.UTF8.GetBytes(blocks[i]).AsSpan().SequenceEqual(actual),
                $"{SyntheticInput.ExampleRelative(example, files[i])}가 스펙 {SyntheticSpec.ExampleHeadings[example]}의 {i + 1}번째 블록과 다르다.");
        }
    }

    // 스펙의 "`examples/<예제>/<파일>` (SHA-256 `…`)" 줄 = 파일의 SHA-256 = 블록의 SHA-256.
    [Theory]
    [MemberData(nameof(SpecFiles))]
    public void File_hash_matches_spec_line(string example, string file)
    {
        var relative = SyntheticInput.ExampleRelative(example, file);
        var spans = SpecDocument.CodeSpans(SpecDocument.Line(SyntheticSpec.Document, $"`{relative}` (SHA-256 "));
        var bytes = SyntheticInput.ExampleBytes(example, file);

        Assert.Equal(relative, spans[0]);
        Assert.Matches("^[0-9a-f]{64}$", spans[1]);
        Assert.Equal(spans[1], Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    // 3.2·4.12: 예제 폴더의 파일 = T-004 파일(answers.yaml, expected/classification.json) + 7.1 표의 파일.
    // .gitkeep이 없고(승인 결정 12), expected/에 새 파일이 없고, 자료 없는 예제에는 reference/·candidate/가 없다.
    // T-006(metrics.md 7.1 4, 승인 질문 17): 계획에 expected/metrics.json을 더한다. 세 예제 모두(미검증 모양 포함).
    [Theory]
    [MemberData(nameof(SyntheticInput.AllExampleNames), MemberType = typeof(SyntheticInput))]
    public void Example_folder_has_exactly_the_planned_files(string example)
    {
        var root = SyntheticInput.ExampleDirectory(example);
        var actual = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal);
        // T-011(judgment.md 4.11·7.1, 잠정 결정 8): 계획에 expected/judgment.json을 더한다. 네 예제 모두(미검증 모양 포함).
        var expected = SyntheticInput.ClassificationFiles.Concat(SyntheticSpec.FilesOf(example)).Append("expected/metrics.json").Append("expected/judgment.json").Order(StringComparer.Ordinal);

        Assert.Equal(expected, actual);
    }

    // T-014(text-check.md 7.1): 5번째 예제 reply-drafts의 계획 = text-check 7.1 예제 파일 표(scores/ 두 파일 포함).
    // 위 테스트의 계획(synthetic-examples 7.1 표)에는 이 예제가 없다(text-check 11절). 대상을 더한 것이다.
    [Theory]
    [MemberData(nameof(TextCheckExampleNames))]
    public void Text_check_example_folder_has_exactly_the_planned_files(string example)
    {
        var root = SyntheticInput.ExampleDirectory(example);
        var actual = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal);

        Assert.Equal(TextCheckSpec.ExampleFiles().Order(StringComparer.Ordinal), actual);
    }

    public static TheoryData<string> TextCheckExampleNames => new(ClassificationInput.TextCheckExamples);

    // examples/ 바로 아래는 .gitattributes(T-004)와 예제 폴더뿐이다.
    // T-014: 예제 폴더 목록을 예제 4종 + reply-drafts(ClassificationInput.AllExamples)로 늘린다. 대상을 더한 것이다.
    [Fact]
    public void Examples_root_has_only_gitattributes_and_example_folders()
    {
        var root = RepoRoot.Combine(ClassificationInput.ExamplesRelativePath);

        Assert.Equal([".gitattributes"], Directory.EnumerateFiles(root).Select(f => Path.GetFileName(f)));
        Assert.Equal(
            ClassificationInput.AllExamples.Order(StringComparer.Ordinal),
            Directory.EnumerateDirectories(root).Select(d => Path.GetFileName(d)).Order(StringComparer.Ordinal));
        Assert.Empty(Directory.EnumerateFiles(root, ".gitkeep", SearchOption.AllDirectories));
    }

    // 3.3·4.9: examples/ 텍스트 파일은 UTF-8(BOM 없음)이고 끝이 LF다. CR 검사는 기존 Example_text_files_have_no_carriage_return이 한다.
    [Fact]
    public void Example_text_files_are_utf8_without_bom_ending_with_lf()
    {
        var strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        var files = Directory.EnumerateFiles(RepoRoot.Combine(ClassificationInput.ExamplesRelativePath), "*", SearchOption.AllDirectories)
            .Where(f => !File.ReadAllBytes(f).AsSpan().Contains((byte)0))
            .ToArray();
        Assert.Contains(files, f => f.EndsWith("intake.md", StringComparison.Ordinal));
        Assert.Contains(files, f => f.EndsWith(".csv", StringComparison.Ordinal));

        var wrong = new List<string>();
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file);
            var relative = RepoRoot.ToRelative(file);
            if (bytes is [0xEF, 0xBB, 0xBF, ..])
            {
                wrong.Add($"{relative}: BOM");
            }

            if (bytes.Length == 0 || bytes[^1] != (byte)'\n')
            {
                wrong.Add($"{relative}: 끝이 LF가 아니다");
            }

            try
            {
                strict.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                wrong.Add($"{relative}: UTF-8이 아니다");
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }
}
