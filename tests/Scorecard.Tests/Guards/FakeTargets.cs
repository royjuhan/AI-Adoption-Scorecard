using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Scorecard.Tests.Guards;

// G2·G3 도우미 자기 검사용 가짜 대상. docs/spec/guard-tests.md 4.6, docs/spec/case-schema.md 4.11.
// good은 G2·G3를 모두 통과해야 하고, 나머지는 해당 검사에서 차이가 나야 한다.
// clock-today, string-hashcode, enum-order는 넣지 않는다. 도우미가 못 잡는 것을
// "통과해야 한다"로 굳히지 않기 위해서다. 한계는 스펙 4.5에 있다.
internal static class FakeTargets
{
    private const string Sample = "basic";
    private const string SummaryFile = "summary.txt";

    // input-touch-mtime이 넣는 수정 시각. 고정 시각(2000-01-01)과 다르기만 하면 된다.
    private static readonly DateTime TouchedTime = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly GuardTarget[] All =
    [
        // 정상.
        new("good", Sample, Good),

        // G2 위반: 실행마다 다른 값, 경로, 문화권.
        new("clock-utcnow", Sample, (i, o) =>
        {
            Good(i, o);
            AppendLine(o, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        }),
        new("guid", Sample, (i, o) =>
        {
            Good(i, o);
            AppendLine(o, Guid.NewGuid().ToString());
        }),
        new("abs-path", Sample, (i, o) =>
        {
            Good(i, o);
            AppendLine(o, Path.GetFullPath(i));
        }),
        new("culture-format", Sample, CultureFormat),

        // G2 위반: 출력이 없거나 0바이트뿐이다(T-003 스펙 4.11 빈 출력 규칙).
        new("no-output", Sample, (i, o) => Directory.CreateDirectory(o)),
        new("empty-file", Sample, (i, o) =>
        {
            Directory.CreateDirectory(o);
            File.WriteAllBytes(Path.Combine(o, SummaryFile), []);
        }),

        // G3 위반: 입력·작업 폴더를 건드린다.
        new("input-append", Sample, (i, o) =>
        {
            File.AppendAllText(Path.Combine(i, "case.yaml"), "# x\n");
            Good(i, o);
        }),
        new("input-delete", Sample, (i, o) =>
        {
            File.Delete(Path.Combine(i, "notes.md"));
            Good(i, o);
        }),
        new("input-new-file", Sample, (i, o) =>
        {
            File.WriteAllText(Path.Combine(i, "extra.tmp"), "x");
            Good(i, o);
        }),
        new("write-sibling", Sample, (i, o) =>
        {
            File.WriteAllText(Path.Combine(i, "..", "sibling.txt"), "x");
            Good(i, o);
        }),
        new("input-rewrite-same", Sample, (i, o) =>
        {
            var path = Path.Combine(i, "case.yaml");
            File.WriteAllBytes(path, File.ReadAllBytes(path));
            Good(i, o);
        }),
        new("input-temp-file", Sample, (i, o) =>
        {
            var path = Path.Combine(i, "work.tmp");
            File.WriteAllText(path, "x");
            File.Delete(path);
            Good(i, o);
        }),
        new("input-touch-mtime", Sample, (i, o) =>
        {
            File.SetLastWriteTimeUtc(Path.Combine(i, "case.yaml"), TouchedTime);
            Good(i, o);
        }),
    ];

    public static GuardTarget Get(string name) => All.Single(t => t.Name == name);

    // 정상 대상. 파일을 서수 순으로 읽고, 숫자는 InvariantCulture로 읽고 쓴다.
    // 출력에는 입력 폴더 기준 상대 경로만 쓴다.
    private static void Good(string input, string output)
    {
        Directory.CreateDirectory(output);
        var text = new StringBuilder();
        foreach (var relative in RelativeFiles(input))
        {
            var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(input, relative))));
            text.Append(relative).Append(' ').Append(sha).Append('\n');
        }

        var sum = SumValues(input, CultureInfo.InvariantCulture);
        text.Append("sum ").Append(sum.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        File.WriteAllText(Path.Combine(output, SummaryFile), text.ToString(), Utf8NoBom);
    }

    // 문화권 의존 대상. 현재 문화권으로 숫자를 읽고 쓴다.
    private static void CultureFormat(string input, string output)
    {
        Directory.CreateDirectory(output);
        var sum = SumValues(input, CultureInfo.CurrentCulture);
        File.WriteAllText(
            Path.Combine(output, SummaryFile),
            "sum " + sum.ToString(CultureInfo.CurrentCulture) + "\n",
            Utf8NoBom);
    }

    private static string[] RelativeFiles(string input) =>
        Directory.EnumerateFiles(input, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(input, f).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

    // data/sample.csv의 value 열 합.
    private static double SumValues(string input, IFormatProvider provider) =>
        File.ReadAllLines(Path.Combine(input, "data", "sample.csv"))
            .Skip(1)
            .Select(line => double.Parse(line.Split(',')[1], NumberStyles.Float | NumberStyles.AllowThousands, provider))
            .Sum();

    private static void AppendLine(string output, string line) =>
        File.AppendAllText(Path.Combine(output, SummaryFile), line + "\n", Utf8NoBom);
}
