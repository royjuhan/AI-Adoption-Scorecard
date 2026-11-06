using System.Diagnostics;
using System.Text;
using YamlDotNet.Core;

// YamlDotNet Parser만 돌려 이벤트를 센다. 시간은 이 컴퓨터 기준 참고값(판정에 쓰지 않는 스파이크).
internal static class Timing
{
    public static int Run()
    {
        foreach (var depth in new[] { 10_000, 20_000, 30_000 })
        {
            var text = "a: " + new string('[', depth) + new string(']', depth) + "\n";
            Console.WriteLine($"TIMING flow-depth={depth} bytes={text.Length} all-events ms={Measure(text, long.MaxValue)}");
        }

        foreach (var depth in new[] { 100_000, 1_000_000 })
        {
            var text = "a: " + new string('[', depth) + new string(']', depth) + "\n";
            Console.WriteLine($"TIMING flow-depth={depth} first-40-events ms={Measure(text, 40)}");
        }

        foreach (var steps in new[] { 1_000, 10_000 })
        {
            var sb = new StringBuilder("name: a\ntask: t\nsteps:\n");
            for (var i = 0; i < steps; i++)
            {
                sb.Append($"  - name: 단계 {i}\n    answers: {{input_kind: table, rule_expressible: yes, frequency: weekly, batch_size: 10, exception_rate: 5, current_actor: program, failure_impact: internal_rework, existing_feature: no}}\n");
            }

            var text = sb.ToString();
            Console.WriteLine($"TIMING flat-steps={steps} bytes={Encoding.UTF8.GetByteCount(text)} all-events ms={Measure(text, long.MaxValue)}");
        }

        return 0;
    }

    private static long Measure(string text, long maxEvents)
    {
        var watch = Stopwatch.StartNew();
        var parser = new Parser(new StringReader(text));
        long count = 0;
        while (count < maxEvents && parser.MoveNext())
        {
            count++;
        }

        return watch.ElapsedMilliseconds;
    }
}
