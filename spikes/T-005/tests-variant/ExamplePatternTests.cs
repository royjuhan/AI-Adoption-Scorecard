using System.Text;
using System.Text.RegularExpressions;
using Scorecard.Tests.CaseSchema;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.Examples;

// T-005 스파이크 변형(머지 대상 아님). 스펙 4.10 패턴 표로 examples/를 검사하는 테스트의 모양.
// 표를 문서에서 읽는다(SpecDocument). 패턴마다 잡는 예·잡지 않는 예를 먼저 보고(자기 검사), 그다음 파일을 본다.
[Trait("Spec", "synthetic-examples")]
public class ExamplePatternTests
{
    private const string SpecPath = "docs/spec/synthetic-examples.md";
    private const string Header = "| 패턴 | 무엇 | 정규식 | 잡는 예 | 잡지 않는 예 |";

    private static (string Name, Regex Regex, string[] Hits, string[] Misses)[] Patterns() =>
        SpecDocument.Table(SpecPath, Header)
            .Select(c => (
                SpecDocument.Unquote(c[0]),
                new Regex(SpecDocument.Unquote(c[2]), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)),
                SpecDocument.CodeSpans(c[3]),
                SpecDocument.CodeSpans(c[4])))
            .ToArray();

    [Fact]
    public void Patterns_catch_their_examples_and_skip_the_others()
    {
        var wrong = new List<string>();
        foreach (var (name, regex, hits, misses) in Patterns())
        {
            wrong.AddRange(hits.Where(h => !regex.IsMatch(h)).Select(h => $"{name}: 잡아야 하는데 못 잡음 '{h}'"));
            wrong.AddRange(misses.Where(m => regex.IsMatch(m)).Select(m => $"{name}: 잡으면 안 되는데 잡음 '{m}'"));
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    [Fact]
    public void Examples_have_no_real_value_patterns()
    {
        var patterns = Patterns();
        var files = Directory.EnumerateFiles(RepoRoot.Combine("examples"), "*", SearchOption.AllDirectories)
            .Select(f => (Full: f, Rel: RepoRoot.ToRelative(f)))
            .OrderBy(f => f.Rel, StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(files);

        var hits = new List<string>();
        foreach (var (full, rel) in files)
        {
            hits.AddRange(patterns.Where(p => p.Regex.IsMatch(rel)).Select(p => $"{p.Name} {rel} (경로)"));
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
                    hits.AddRange(p.Regex.Matches(lines[i]).Select(m => $"{p.Name} {rel}:{i + 1}: {m.Value}"));
                }
            }
        }

        // 걸린 것을 모두 보인다(xUnit 컬렉션 출력은 잘린다). 고칠 곳은 패턴이 아니라 합성 값이다(스펙 4.10).
        Assert.True(hits.Count == 0, "실제 값 패턴이 있다:\n" + string.Join("\n", hits));
    }
}
