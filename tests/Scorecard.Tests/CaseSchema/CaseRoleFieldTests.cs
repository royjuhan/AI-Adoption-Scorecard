using Scorecard.Core.Cases;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.CaseSchema;

// 스펙 4.16 역할 배분 선택 항목(T-024, ADR 0018). 데이터 4.16 장면 표를 읽어 행마다 받음·오류를 본다.
// 단계 둘인 케이스에 넣는다. ai: 로 시작하면 첫 단계의 ai 값, 아니면 최상위 줄 하나를 더한다.
[Trait("Spec", "case-schema")]
public class CaseRoleFieldTests
{
    private const string TableHeader = "| T-024 장면 | 코드 | 경로 | 결과·메시지 |";
    private const string NoError = "—";
    private const string AiPrefix = "ai: ";
    private const string Head = "name: a\ntask: t\nsteps:\n  - name: s\n    answers: {human_signoff: no}\n";
    private const string Tail = "  - name: u\n";
    private const string RolesSample = "tests/Scorecard.Tests/TestData/classification/ai-roles/answers.yaml";

    public static TheoryData<int> Rows => new(Enumerable.Range(0, SpecDocument.Table(SpecDocument.CaseSchemaPath, TableHeader).Count));

    [Theory]
    [MemberData(nameof(Rows))]
    public void Role_field_scene_gives_spec_result(int index)
    {
        var c = SpecDocument.Table(SpecDocument.CaseSchemaPath, TableHeader)[index];
        var scene = SpecDocument.Unquote(c[0]);
        var yaml = scene.StartsWith(AiPrefix, StringComparison.Ordinal)
            ? Head + "    " + scene + "\n" + Tail
            : scene + "\n" + Head + Tail;

        var result = CaseInput.Parse(yaml);

        if (c[1].Trim() == NoError)
        {
            var definition = CaseInput.Valid(result);
            var expect = SpecDocument.Unquote(c[3]);
            if (expect == "단계 Allocation 있음")
            {
                Assert.NotNull(definition.Steps[0].Ai?.Detail?.Allocation);
                Assert.Null(definition.Notes);
            }
            else
            {
                Assert.Equal("케이스 Allocation 있음", expect);
                Assert.NotNull(definition.Notes?.Allocation);
            }

            return;
        }

        Assert.Null(result.Case);
        var error = Assert.Single(result.Errors);
        Assert.Equal(SpecDocument.Unquote(c[1]), error.Code);
        Assert.Equal(SpecDocument.Unquote(c[2]), error.Path);
        Assert.Equal(SpecDocument.Unquote(c[3]), error.Message);
    }

    // 장면 표가 받음 둘과 오류 코드 여덟을 모두 쓴다.
    [Fact]
    public void Scene_table_covers_accepted_and_error_codes()
    {
        var rows = SpecDocument.Table(SpecDocument.CaseSchemaPath, TableHeader);

        Assert.Equal(
            ["단계 Allocation 있음", "케이스 Allocation 있음"],
            rows.Where(r => r[1].Trim() == NoError).Select(r => SpecDocument.Unquote(r[3])).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(
            ["conflict", "duplicate", "enum", "format", "range", "required", "type", "unknown-field"],
            rows.Where(r => r[1].Trim() != NoError).Select(r => SpecDocument.Unquote(r[1])).Distinct().Order(StringComparer.Ordinal));
    }

    // 데이터 4.16 블록(name·task를 앞에 붙인다): 모든 값이 쓴 글 그대로 모델에 들어간다. 빈 역할은 null이다.
    [Fact]
    public void Block_values_are_kept_as_written()
    {
        var block = Assert.Single(SpecDocument.CodeBlocks("### 4.16 역할 배분 선택 항목 (T-024)"));
        var definition = CaseInput.Valid(CaseInput.Parse("name: a\ntask: t\n" + block));

        var a = definition.Notes!.Allocation!;
        Assert.Equal(("고객 문의에 답을 빠짐없이 보낸다.", "모든 문의에 답이 나가고 이력에 남는다."), (a.Purpose, a.DoneWhen));
        Assert.Equal(["하루 30건쯤 온다"], a.Facts);
        Assert.Equal(["자주 오는 문의가 묶이는지 확인되지 않았다"], a.Assumptions);
        var tool = Assert.Single(a.Tools);
        Assert.Equal(
            ("기록 조회 도구", 1, "기록을 찾아 붙인다", "고객 번호 형식", "조회 → 저장", "붙인 기록", "AI의 조회 반복", "번호가 없으면 AI로 넘긴다", "메일 분류 규칙", "조회 연결", "연결 유지보수"),
            (tool.Name, Assert.Single(tool.Steps), tool.Function, tool.Input, tool.Process, tool.Output, tool.Reduces, tool.Exceptions, tool.Existing, tool.New, tool.Burden));
        Assert.Equal([new RoleTransition(1, "②", "③", "번호 없는 메일이 늘어난다", "붙이지 못한 메일 수와 사유")], a.Transitions);
        Assert.Null(definition.Notes.Scenario);
        Assert.Empty(definition.Notes.SelfCheck);

        var step = Assert.Single(definition.Steps).Ai!.Detail!;
        Assert.Empty(step.Actions);
        Assert.Equal(
            new StepAllocation(new StepRoles(null, "번호 없는 메일의 고객 추정", "기록 조회와 붙이기"), "어느 고객의 문의인가.", "소프트웨어가 붙인다.", "이 단계에는 없다.", "입력은 달라도 규칙은 같다.", "입력값이 달라진다."),
            step.Allocation);
    }

    // 역할 배분 항목은 답·담당·근거·T-022 선택 항목을 바꾸지 않는다. 적지 않으면 Allocation은 null이다.
    [Fact]
    public void Role_fields_do_not_change_answers_or_judgment()
    {
        var plain = CaseInput.Valid(CaseInput.Parse(Head + "    ai: {assignee: ②, reason: r, now: 손으로 한다}\n" + Tail));
        var rich = CaseInput.Valid(CaseInput.Parse("purpose: p\n" + Head + "    ai: {assignee: ②, reason: r, now: 손으로 한다, basis: b}\n" + Tail));

        Assert.Null(plain.Notes);
        Assert.Null(plain.Steps[0].Ai!.Detail!.Allocation);
        Assert.Equal(plain.Steps.Select(s => s.Answers), rich.Steps.Select(s => s.Answers));
        Assert.Equal((plain.Steps[0].Ai!.Assignee, plain.Steps[0].Ai!.Reason, plain.Steps[0].Ai!.Detail!.Now), (rich.Steps[0].Ai!.Assignee, rich.Steps[0].Ai!.Reason, rich.Steps[0].Ai!.Detail!.Now));
        Assert.Equal("p", rich.Notes!.Allocation!.Purpose);
        Assert.Empty(rich.Notes.Allocation.Tools);
        Assert.Empty(rich.Notes.Allocation.Transitions);
        Assert.Null(rich.Notes.Scenario);
    }

    // 표본 TestData/classification/ai-roles/answers.yaml(T-024 리포트 골든 입력)은 오류 없이 읽힌다.
    // 세 역할을 모두 채운 단계는 3단계 하나뿐이다(세 역할을 억지로 채우지 않는다).
    [Fact]
    public void Roles_sample_parses()
    {
        var definition = CaseInput.Valid(CaseParser.Parse(File.ReadAllBytes(RepoRoot.Combine(RolesSample)), "answers.yaml"));

        Assert.Equal("virtual", definition.Notes!.Scenario);
        var a = definition.Notes.Allocation!;
        Assert.Equal((2, 2, 3, 2), (a.Tools.Count, a.Transitions.Count, a.Facts.Count, a.Assumptions.Count));
        var roles = definition.Steps.Select(s => s.Ai!.Detail!.Allocation!.Roles!).ToArray();
        Assert.Equal([2, 2, 3, 1], roles.Select(r => new[] { r.Human, r.Ai, r.Software }.Count(x => x is not null)));
        Assert.All(definition.Steps, s => Assert.NotNull(s.Ai!.Detail!.Allocation!.Accountability));
    }
}
