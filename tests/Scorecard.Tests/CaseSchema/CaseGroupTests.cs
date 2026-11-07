using System.Security.Cryptography;
using System.Text;
using Scorecard.Core.Cases;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.CaseSchema;

// 스펙 10절 inputs.group(T-012, judgment.md 질문 7). 10.2 표를 읽어 줄마다 받음·오류를 본다.
// 표본: examples/table-compare/case.yaml에서 "  group:" 줄을 뺀 것(T-011 바이트, 10.2 머리말)의 "  key: log_id" 줄 바로 뒤에 줄을 넣는다.
[Trait("Spec", "case-schema")]
public class CaseGroupTests
{
    private const string TableHeader = "| inputs.group 줄 | 결과 | 코드 | 메시지 |";
    private const string NoLine = "(줄 없음)";
    private const string Accepted = "받음";
    private const string Rejected = "오류";
    private const string KeyLine = "  key: log_id";
    private const string GroupPath = "inputs.group";

    // judgment.md 11.12 표의 table-compare "지금 SHA-256"(group 줄을 넣기 전 바이트).
    private const string BaseHash = "451a22acd60c6b650aaf86a424acc72fe1884838592b9ca6819dd2491e764ce2";

    public static TheoryData<int> Rows => new(Enumerable.Range(0, SpecDocument.Table(SpecDocument.CaseSchemaPath, TableHeader).Count));

    // 10.2 표의 한 행. 받음이면 Group 값(줄 없음·null은 null), 오류면 그 값 위치의 오류 한 줄(경로 inputs.group).
    [Theory]
    [MemberData(nameof(Rows))]
    public void Group_line_gives_spec_result(int index)
    {
        var c = SpecDocument.Table(SpecDocument.CaseSchemaPath, TableHeader)[index];
        var line = c[0].Trim() == NoLine ? null : SpecDocument.Unquote(c[0]);
        var (yaml, groupLine) = WithLine(line);

        var result = CaseInput.Parse(yaml);

        switch (c[1].Trim())
        {
            case Accepted:
                Assert.Equal("—", c[2].Trim());
                Assert.Equal(ExpectedGroup(line), CaseInput.Valid(result).Inputs!.Group);
                break;
            case Rejected:
                Assert.NotNull(line);
                Assert.True(line.StartsWith("group: ", StringComparison.Ordinal), line);
                var column = 2 + "group: ".Length + 1;
                CaseInput.AssertErrors(result, $"{CaseParser.FileName}:{groupLine}:{column}: {SpecDocument.Unquote(c[2])} {GroupPath}: {SpecDocument.Unquote(c[3])}");
                break;
            default:
                Assert.Fail($"10.2 '결과' 칸 '{c[1]}'을 모른다.");
                break;
        }
    }

    // 10.2 표: 받음·오류가 모두 있고, 오류 코드는 required·type·conflict 셋이다(4.9 표 코드, 코드 수는 그대로).
    [Fact]
    public void Group_table_covers_accepted_and_three_error_codes()
    {
        var rows = SpecDocument.Table(SpecDocument.CaseSchemaPath, TableHeader);

        Assert.Contains(rows, r => r[1].Trim() == Accepted && r[0].Trim() == NoLine);
        Assert.Equal(
            ["conflict", "required", "type"],
            rows.Where(r => r[1].Trim() == Rejected).Select(r => SpecDocument.Unquote(r[2])).Distinct().Order(StringComparer.Ordinal));
    }

    // 비교는 서수다. key 대소문자만 다른 Log_id는 받는다. compare 필드와 같은 이름은 conflict다.
    // 10.3: 세 인자 생성은 Group = null이고, 같음 비교에 Group이 든다.
    [Fact]
    public void Group_is_part_of_case_inputs()
    {
        var (withGroup, _) = WithLine("group: signal_type");
        var (without, _) = WithLine(null);

        var a = CaseInput.Valid(withGroup).Inputs!;
        var b = CaseInput.Valid(without).Inputs!;

        Assert.Equal("signal_type", a.Group);
        Assert.Null(b.Group);
        Assert.NotEqual(a, b);
        Assert.Equal(b, a with { Group = null });
        Assert.Equal(new CaseInputs(b.Reference, b.Candidate, b.Key), b);
    }

    // 표본 바탕: 지금 예제 파일에서 "  group:" 줄을 빼면 T-011 바이트(451a22ac…)다. 예제에 줄이 있든 없든 같은 바탕이다.
    [Fact]
    public void Base_sample_is_table_compare_without_group_line()
    {
        var text = BaseText();

        Assert.Equal(BaseHash, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))));
        Assert.Single(text.Split('\n'), l => l == KeyLine);
    }

    // 바탕 글의 "  key: log_id" 줄 뒤에 "  " + line을 넣는다. 돌려주는 줄 번호는 넣은 줄(1부터)이다.
    private static (string Yaml, int GroupLine) WithLine(string? line)
    {
        var lines = BaseText().Split('\n').ToList();
        var at = lines.IndexOf(KeyLine);
        Assert.True(at >= 0, "표본에 key 줄이 없다.");
        if (line is not null)
        {
            lines.Insert(at + 1, "  " + line);
        }

        return (string.Join('\n', lines), at + 2);
    }

    private static string BaseText() =>
        string.Concat(
            File.ReadAllText(RepoRoot.Combine("examples/table-compare/case.yaml"), Encoding.UTF8)
                .Split('\n')
                .Where(l => !l.StartsWith("  group:", StringComparison.Ordinal))
                .Select((l, i) => i == 0 ? l : "\n" + l));

    private static string? ExpectedGroup(string? line)
    {
        if (line is null)
        {
            return null;
        }

        var value = line["group: ".Length..];
        return value == "null" ? null : value;
    }
}
