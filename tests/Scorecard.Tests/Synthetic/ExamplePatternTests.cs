using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Scorecard.Core.Synthetic;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Classification;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.Synthetic;

// 스펙 4.10 실제 값 패턴 검사(승인 결정 9·10). 카드 수용 기준 2. 지시문 2절 10 "pre-commit 검사"를 xUnit 테스트로 한다.
// git 훅은 만들지 않는다(승인 결정 9). 범위는 examples/만: 파일 내용의 줄마다, 저장소 기준 상대 경로에도 건다.
// NUL 바이트가 있는 파일은 내용을 보지 않는다(기존 \r 테스트와 같은 판정). 경로는 본다.
// 오탐은 패턴을 고치거나 예외 목록을 두지 않고 합성 값을 바꿔 피한다(4.10).
[Trait("Spec", "synthetic-examples")]
public class ExamplePatternTests
{
    // 4.10 표 → 패턴. 정규식은 RegexOptions.CultureInvariant, 시간 제한 1초. 칸의 백틱 안 글자가 정규식 그대로다.
    public static IReadOnlyList<Pattern> Patterns() =>
        SyntheticSpec.Table(SyntheticSpec.PatternHeader)
            .Select(c => new Pattern(
                SpecDocument.Unquote(c[0]),
                new Regex(SingleSpan(c[2]), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)),
                SpecDocument.CodeSpans(c[3]),
                SpecDocument.CodeSpans(c[4])))
            .ToArray();

    // root 아래 모든 파일을 검사한다. 걸린 것을 "패턴 경로:줄: 값"(경로 자체는 "패턴 경로 (경로)") 모양으로 모두 돌려준다.
    // relativeTo: 경로를 이 폴더 기준 상대 경로('/' 구분)로 적는다.
    public static IReadOnlyList<string> Scan(IReadOnlyList<Pattern> patterns, string root, string relativeTo)
    {
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => (Full: f, Relative: Path.GetRelativePath(relativeTo, f).Replace(Path.DirectorySeparatorChar, '/')))
            .OrderBy(f => f.Relative, StringComparer.Ordinal)
            .ToArray();

        var hits = new List<string>();
        foreach (var (full, relative) in files)
        {
            hits.AddRange(patterns.Where(p => p.Regex.IsMatch(relative)).Select(p => $"{p.Name} {relative} (경로)"));
            var bytes = File.ReadAllBytes(full);
            if (bytes.AsSpan().Contains((byte)0))
            {
                continue;
            }

            var lines = Encoding.UTF8.GetString(bytes).Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (var p in patterns)
                {
                    hits.AddRange(p.Regex.Matches(lines[i]).Select(m => $"{p.Name} {relative}:{i + 1}: {m.Value}"));
                }
            }
        }

        return hits;
    }

    [Fact]
    public void Pattern_table_has_the_nine_spec_patterns()
    {
        Assert.Equal(
            ["email", "url", "windows-path", "unc-path", "posix-path", "home-path", "part-letters-digits", "part-digit-groups", "part-long-code"],
            Patterns().Select(p => p.Name));
        Assert.All(Patterns(), p =>
        {
            Assert.NotEmpty(p.Hits);
            Assert.NotEmpty(p.Misses);
        });
    }

    // 자기 검사: 잡는 예는 모두 걸리고, 잡지 않는 예는 하나도 걸리지 않는다.
    [Fact]
    public void Patterns_catch_their_examples_and_skip_the_others()
    {
        var wrong = new List<string>();
        foreach (var p in Patterns())
        {
            wrong.AddRange(p.Hits.Where(h => !p.Regex.IsMatch(h)).Select(h => $"{p.Name}: 잡아야 하는데 못 잡음 '{h}'"));
            wrong.AddRange(p.Misses.Where(m => p.Regex.IsMatch(m)).Select(m => $"{p.Name}: 잡으면 안 되는데 잡음 '{m}'"));
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    // 카드 수용 기준 2: examples/ 모든 파일(내용 줄, 상대 경로)에 걸리는 것이 0건. 걸리면 모두 보인다.
    // 빈 통과를 막으려고 7.1 표의 파일과 T-004 파일이 검사 대상에 있는지 먼저 본다.
    [Fact]
    public void Examples_have_no_real_value_patterns()
    {
        var root = RepoRoot.Combine(ClassificationInput.ExamplesRelativePath);
        var scanned = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(RepoRoot.ToRelative).ToHashSet(StringComparer.Ordinal);
        var planned = SyntheticSpec.Files().Select(f => SyntheticInput.ExampleRelative(f.Example, f.File))
            .Concat(ClassificationInput.Examples.SelectMany(e => SyntheticInput.ClassificationFiles.Select(f => SyntheticInput.ExampleRelative(e, f))));
        Assert.All(planned, f => Assert.Contains(f, scanned));

        var hits = Scan(Patterns(), root, RepoRoot.FullPath);

        // xUnit의 컬렉션 출력은 잘린다. 메시지로 모두 보인다. 고칠 곳은 패턴이 아니라 합성 값이다(4.10).
        Assert.True(hits.Count == 0, "실제 값 패턴이 있다:\n" + string.Join("\n", hits));
    }

    // 음성 대조: 패턴마다 잡는 예를 심은 파일을 검사하면 모든 패턴이 걸린다. 파일 이름(경로)에 심은 이메일도 걸린다.
    // NUL 바이트가 있는 파일은 내용을 보지 않는다. 걸린 줄 모양은 "패턴 경로:줄: 값"이다.
    [Fact]
    public void Planted_values_are_all_reported()
    {
        var patterns = Patterns();
        var dir = Directory.CreateTempSubdirectory("scorecard-pattern-").FullName;
        try
        {
            var sub = Path.Combine(dir, "examples", "planted");
            Directory.CreateDirectory(sub);
            File.WriteAllText(Path.Combine(sub, "case.yaml"), "name: p\n" + string.Join("\n", patterns.SelectMany(p => p.Hits)) + "\n", new UTF8Encoding(false));
            File.WriteAllBytes(Path.Combine(sub, "binary.dat"), [.. Encoding.ASCII.GetBytes("someone@example.com"), 0]);
            File.WriteAllText(Path.Combine(sub, "someone@example.com.txt"), "ok\n");

            var hits = Scan(patterns, Path.Combine(dir, "examples"), dir);

            Assert.All(patterns, p => Assert.Contains(hits, h => h.StartsWith(p.Name + " examples/planted/case.yaml:", StringComparison.Ordinal)));
            Assert.Contains("email examples/planted/case.yaml:2: " + patterns[0].Hits[0], hits);
            Assert.Contains("email examples/planted/someone@example.com.txt (경로)", hits);
            Assert.DoesNotContain(hits, h => h.Contains("binary.dat:", StringComparison.Ordinal));
            Assert.DoesNotContain(hits, h => h.Contains(Path.GetFileName(dir), StringComparison.Ordinal));
        }
        finally
        {
            CaseInput.TryDeleteDirectory(dir);
        }
    }

    // 4.10: 생성기가 만드는 값 모양(계획 표 키 1~99, 선택지, 범위 끝)은 어떤 패턴에도 걸리지 않는다.
    [Fact]
    public void Generator_value_shapes_match_no_pattern()
    {
        var samples = new List<string>();
        foreach (var example in SyntheticInput.DataExamples)
        {
            var plan = SyntheticSpec.Plan(example);
            samples.AddRange(Enumerable.Range(1, 99).Select(i =>
                plan.KeyPrefix + i.ToString("D" + plan.KeyDigits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)));
            foreach (var c in plan.Columns)
            {
                samples.AddRange(c.Kind == SyntheticColumn.KindChoice
                    ? c.Choices
                    : [c.Min.ToString(CultureInfo.InvariantCulture), c.Max.ToString(CultureInfo.InvariantCulture)]);
            }

            samples.Add(plan.FileName);
        }

        var patterns = Patterns();
        var hits = samples.SelectMany(s => patterns.Where(p => p.Regex.IsMatch(s)).Select(p => $"{p.Name}: {s}")).ToArray();

        Assert.True(hits.Length == 0, string.Join("\n", hits));
    }

    // 4.10: .NET의 \w는 한글도 포함한다. 그래서 한글 뒤의 '/'는 posix-path에 걸리지 않는다. 앞이 공백이면 걸린다.
    [Fact]
    public void Posix_path_lookbehind_treats_hangul_as_word()
    {
        var posix = Assert.Single(Patterns(), p => p.Name == "posix-path").Regex;

        Assert.DoesNotMatch(posix, "센서/로그/");
        Assert.Matches(posix, " /data/x");
    }

    // 정규식 칸은 `…` 하나다(백틱 안이 정규식 그대로).
    private static string SingleSpan(string cell)
    {
        var spans = SpecDocument.CodeSpans(cell);
        Assert.True(spans.Length == 1, $"4.10 정규식 칸 '{cell}'에 `…`가 하나여야 한다.");
        return spans[0];
    }

    public sealed record Pattern(string Name, Regex Regex, string[] Hits, string[] Misses);
}
