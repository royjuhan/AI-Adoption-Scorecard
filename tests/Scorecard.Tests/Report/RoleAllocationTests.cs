using System.Text;
using System.Text.Json;
using Scorecard.Core.Report;
using Scorecard.Tests.Cli;

namespace Scorecard.Tests.Report;

// T-024(ADR 0018): 역할 배분 메모가 스코어카드에 나타나는 모양. 있을 때만 키·구역이 생기고, 값은 답 파일 그대로다.
// 셈·판정·문장을 더하지 않는다. 점수·절감률이 없다(데이터 report.md 3·6절, report.md 13절).
[Trait("Spec", "report")]
public class RoleAllocationTests
{
    public const string TopOptionalHeader = "| 있을 때만 속성 | 형식 | 뜻 |";
    public const string StepOptionalHeader = "| 있을 때만 단계 속성 | 형식 | 뜻 |";
    public const string RoleZoneHeader = "| 역할 배분 구역 | id | 내용 | 결과 객체 |";

    public static TheoryData<string> Golden => ReportInput.GoldenNames;

    private static JsonDocument GoldenJson(string name) => JsonDocument.Parse(File.ReadAllBytes(ReportInput.GoldenPath(name, ScorecardJson.FileName)));

    private static string GoldenHtml(string name) => File.ReadAllText(ReportInput.GoldenPath(name, "scorecard.html"), Encoding.UTF8);

    private static string[] Column(string header, int index) => ReportInput.Table(header).Select(r => CliInput.Cell(r[index])).ToArray();

    // "있을 때만" 키는 메모가 있는 표본(ai-roles)에만 있다. 최상위는 self_check 뒤, 단계는 gate_note 뒤다.
    [Theory]
    [MemberData(nameof(Golden))]
    public void Optional_keys_appear_only_with_allocation(string name)
    {
        using var doc = GoldenJson(name);
        var top = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        var roles = name == ReportInput.RolesSample;

        Assert.Equal(["allocation"], Column(TopOptionalHeader, 0));
        Assert.Equal(["allocation"], Column(StepOptionalHeader, 0));
        Assert.Equal(roles, top.Contains("allocation"));
        if (roles)
        {
            Assert.Equal(top.IndexOf("self_check") + 1, top.IndexOf("allocation"));
        }

        foreach (var step in doc.RootElement.GetProperty("steps").EnumerateArray())
        {
            var names = step.EnumerateObject().Select(p => p.Name).ToArray();
            Assert.Equal(roles, names.Contains("allocation"));
            Assert.Equal(roles ? "allocation" : "gate_note", names[^1]);
        }
    }

    // 결과 객체의 역할 배분은 답 파일 값 그대로다(셈·문장을 더하지 않는다).
    [Fact]
    public void Allocation_is_copied_from_answers()
    {
        var bytes = File.ReadAllBytes(Path.Combine(ReportInput.SampleFolder(ReportInput.RolesSample), "answers.yaml"));
        var definition = Scorecard.Core.Cases.CaseParser.Parse(bytes, "answers.yaml").Case!;
        using var doc = GoldenJson(ReportInput.RolesSample);
        var a = doc.RootElement.GetProperty("allocation");
        var notes = definition.Notes!.Allocation!;

        Assert.Equal(notes.Purpose, a.GetProperty("purpose").GetString());
        Assert.Equal(notes.DoneWhen, a.GetProperty("done_when").GetString());
        Assert.Equal(notes.Facts, a.GetProperty("facts").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(notes.Assumptions, a.GetProperty("assumptions").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(notes.Tools.Select(t => (t.Name, string.Join(",", t.Steps), t.Exceptions, t.Existing, t.New, t.Burden)),
            a.GetProperty("tools").EnumerateArray().Select(t => (t.GetProperty("name").GetString()!, string.Join(",", t.GetProperty("steps").EnumerateArray().Select(n => n.GetInt32())),
                t.GetProperty("exceptions").GetString()!, t.GetProperty("existing").GetString()!, t.GetProperty("new").GetString()!, t.GetProperty("burden").GetString()!)));
        Assert.Equal(notes.Transitions.Select(t => (t.Step, t.From, t.To, t.Evidence)),
            a.GetProperty("transitions").EnumerateArray().Select(t => (t.GetProperty("step").GetInt32(), t.GetProperty("from").GetString()!, t.GetProperty("to").GetString()!, t.GetProperty("evidence").GetString()!)));

        var steps = doc.RootElement.GetProperty("steps").EnumerateArray().ToArray();
        for (var i = 0; i < steps.Length; i++)
        {
            var expected = definition.Steps[i].Ai!.Detail!.Allocation!;
            var actual = steps[i].GetProperty("allocation");
            var r = actual.GetProperty("roles");
            Assert.Equal((expected.Roles!.Human, expected.Roles.Ai, expected.Roles.Software), (Nullable(r, "human"), Nullable(r, "ai"), Nullable(r, "software")));
            Assert.Equal((expected.Judgment, expected.Execution, expected.Accountability, expected.Basis, expected.Varies),
                (Nullable(actual, "judgment"), Nullable(actual, "execution"), Nullable(actual, "accountability"), Nullable(actual, "basis"), Nullable(actual, "varies")));
        }
    }

    // 데이터 6절 역할 배분 구역 표: 메모가 있는 표본만 세 구역 id를 한 번씩 가진다. 다른 골든에는 없다.
    [Theory]
    [MemberData(nameof(Golden))]
    public void Role_zones_appear_only_with_allocation(string name)
    {
        var html = GoldenHtml(name);
        var ids = Column(RoleZoneHeader, 1);

        Assert.Equal(["role-understanding", "role-tools", "role-transitions"], ids);
        Assert.All(ids, id => Assert.Equal(name == ReportInput.RolesSample ? 1 : 0, html.Split("id=\"" + id + "\"").Length - 1));
        Assert.Equal(name == ReportInput.RolesSample, html.Contains(".roles3{", StringComparison.Ordinal));
    }

    // 화면 순서: 1구역 → 업무 이해 → 2구역 → 개선 제안 → 4구역 → 역할 전환 조건 → 6구역.
    // 단계마다 세 역할 이름표, 없는 역할은 "역할 없음", 판단·실행·책임·배분 근거, 도구 칸 이름, 전환 근거가 보인다.
    [Fact]
    public void Html_shows_roles_tools_and_transitions_in_order()
    {
        var html = GoldenHtml(ReportInput.RolesSample);
        string[] order = ["id=\"zone-1\"", "id=\"role-understanding\"", "id=\"zone-2\"", "id=\"role-tools\"", "id=\"zone-4\"", "id=\"role-transitions\"", "id=\"zone-6\""];
        var at = order.Select(o => html.IndexOf(o, StringComparison.Ordinal)).ToArray();

        Assert.All(at, i => Assert.True(i >= 0));
        Assert.Equal(at.Order(), at);
        Assert.Equal(4, html.Split("<div class=\"alloc\">").Length - 1);
        Assert.Equal(4, html.Split("<span class=\"none\">역할 없음</span>").Length - 1);
        Assert.All(new[] { ">인간<", ">AI<", ">소프트웨어<", ">판단<", ">실행<", ">책임<", ">배분 근거<", ">달라지는 것<", "확인된 사실", "가정(확인 필요)", "완료 조건" }, w => Assert.Contains(w, html, StringComparison.Ordinal));
        Assert.All(new[] { ">기능<", ">입력<", ">처리<", ">결과<", ">줄어드는 작업<", ">다시 넘길 예외<", ">기존 기능으로 해결<", ">새로 개발<", ">비용·부담 검토<", ">바꾸는 조건<", ">필요한 근거<" }, w => Assert.Contains(w, html, StringComparison.Ordinal));
        Assert.Contains("AI의 자신감만으로 맡기는 범위를 넓히지 않는다.", html, StringComparison.Ordinal);
    }

    // 역할 배분 메모를 지우면 결과 객체와 HTML에 역할 배분 키·구역·CSS가 없다(T-022 모양 그대로).
    [Fact]
    public void Without_allocation_keys_scorecard_has_no_role_parts()
    {
        using var work = ReportInput.Workspace(ReportInput.RolesSample);
        var answers = Path.Combine(work.Case, "answers.yaml");
        string[] top = ["purpose:", "done_when:", "facts:", "assumptions:", "tools:", "transitions:"];
        string[] step = ["      roles:", "      judgment:", "      execution:", "      accountability:", "      basis:", "      varies:"];
        // 최상위 키는 다음 0칸 줄까지, 단계 키는 다음 6칸 이하 줄까지 지운다.
        var lines = new List<string>();
        string? mode = null;
        foreach (var line in File.ReadAllText(answers).Split('\n'))
        {
            if (top.Any(k => line.StartsWith(k, StringComparison.Ordinal)))
            {
                mode = "top";
                continue;
            }

            if (step.Any(k => line.StartsWith(k, StringComparison.Ordinal)))
            {
                mode = "step";
                continue;
            }

            if ((mode == "top" && line.StartsWith(' ')) || (mode == "step" && line.StartsWith("        ", StringComparison.Ordinal)))
            {
                continue;
            }

            mode = null;
            lines.Add(line);
        }

        File.WriteAllText(answers, string.Join('\n', lines));
        var (open, output) = ReportInput.Produce(work, ReportInput.RolesSample);

        Assert.Equal(0, open.Exit);
        Assert.DoesNotContain("\"allocation\"", CliInput.Text(open.Stdout), StringComparison.Ordinal);
        var html = File.ReadAllText(Path.Combine(output, "scorecard.html"));
        Assert.DoesNotContain("role-", html, StringComparison.Ordinal);
        Assert.DoesNotContain(".roles3", html, StringComparison.Ordinal);
        Assert.DoesNotContain("역할 없음", html, StringComparison.Ordinal);
    }

    private static string? Nullable(JsonElement e, string name) => e.GetProperty(name).ValueKind == JsonValueKind.Null ? null : e.GetProperty(name).GetString();
}
