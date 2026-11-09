using Scorecard.Core.Cases;
using Scorecard.Tests.Structure;

namespace Scorecard.Tests.CaseSchema;

// 스펙 4.15 스킬 선택 항목(T-022, ADR 0016). 데이터 4.15 장면 표를 읽어 행마다 받음·오류를 본다.
// ai: 로 시작하는 장면은 단계 하나의 ai 값, 아니면 최상위 줄 하나를 더한다. 오류는 코드·경로·메시지가 표와 같은 한 건이다.
[Trait("Spec", "case-schema")]
public class CaseSkillFieldTests
{
    private const string TableHeader = "| T-022 장면 | 코드 | 경로 | 결과·메시지 |";
    private const string NoError = "—";
    private const string AiPrefix = "ai: ";
    private const string Base = "name: a\ntask: t\nsteps:\n  - name: s\n    answers: {human_signoff: no}\n";

    public static TheoryData<int> Rows => new(Enumerable.Range(0, SpecDocument.Table(SpecDocument.CaseSchemaPath, TableHeader).Count));

    [Theory]
    [MemberData(nameof(Rows))]
    public void Skill_field_scene_gives_spec_result(int index)
    {
        var c = SpecDocument.Table(SpecDocument.CaseSchemaPath, TableHeader)[index];
        var scene = SpecDocument.Unquote(c[0]);
        var yaml = scene.StartsWith(AiPrefix, StringComparison.Ordinal)
            ? Base + "    " + scene + "\n"
            : scene + "\n" + Base;

        var result = CaseInput.Parse(yaml);

        if (c[1].Trim() == NoError)
        {
            var definition = CaseInput.Valid(result);
            var expect = SpecDocument.Unquote(c[3]);
            switch (expect)
            {
                case "Detail 있음":
                    Assert.NotNull(Assert.Single(definition.Steps).Ai?.Detail);
                    break;
                case "Detail 없음":
                    Assert.Null(Assert.Single(definition.Steps).Ai?.Detail);
                    break;
                default:
                    Assert.Equal("Notes 있음", expect);
                    Assert.NotNull(definition.Notes);
                    break;
            }

            return;
        }

        Assert.Null(result.Case);
        var error = Assert.Single(result.Errors);
        Assert.Equal(SpecDocument.Unquote(c[1]), error.Code);
        Assert.Equal(SpecDocument.Unquote(c[2]), error.Path);
        Assert.Equal(SpecDocument.Unquote(c[3]), error.Message);
    }

    // 장면 표가 받음 셋(Detail 있음·없음, Notes 있음)과 type·required·enum·unknown-field 오류를 모두 쓴다.
    [Fact]
    public void Scene_table_covers_accepted_and_error_codes()
    {
        var rows = SpecDocument.Table(SpecDocument.CaseSchemaPath, TableHeader);

        Assert.Equal(
            ["Detail 없음", "Detail 있음", "Notes 있음"],
            rows.Where(r => r[1].Trim() == NoError).Select(r => SpecDocument.Unquote(r[3])).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(
            ["enum", "required", "type", "unknown-field"],
            rows.Where(r => r[1].Trim() != NoError).Select(r => SpecDocument.Unquote(r[1])).Distinct().Order(StringComparer.Ordinal));
    }

    // 데이터 4.15 블록(name·task를 앞에 붙인다): 모든 선택 항목이 쓴 글 그대로 모델에 들어간다(숫자 문장도 바꾸지 않는다).
    [Fact]
    public void Block_values_are_kept_as_written()
    {
        var block = Assert.Single(SpecDocument.CodeBlocks("### 4.15 스킬 선택 항목 (T-022)"));
        var definition = CaseInput.Valid(CaseInput.Parse("name: a\ntask: t\n" + block));

        var notes = definition.Notes!;
        Assert.Equal("virtual", notes.Scenario);
        Assert.Equal(new CaseTaskSummary("연 1회 집중 기간 10주", "담당자가 메일로 받아 정리한다", null, null), notes.Summary);
        Assert.Equal(["주 10건", "예외 1건 처리에 15분"], notes.StatedNumbers);
        Assert.Equal([new SelfCheckItem("R1 출처 없는 단정 0건", "pass", null)], notes.SelfCheck);
        Assert.Equal(["필수 칸을 비우면 제출이 막히나요?"], notes.OpenQuestions);
        Assert.Equal("업무 구조 검토 완료. 지금 제출할 것 없음.", notes.EndState);

        var detail = Assert.Single(definition.Steps).Ai!.Detail!;
        Assert.Equal([new StepAction("첨부 내려받기", "human", "②", "stated")], detail.Actions);
        Assert.Equal(("담당자가 메일을 하나씩 연다.", "회신을 업체별로 모으는 규칙을 둔다.", "메일 도구에 모으기 기능이 있는지 본다.", "보내기 전에 담당자가 읽는다."),
            (detail.Now, detail.Change, detail.Check, detail.GateNote));
    }

    // 선택 항목은 답·inferred·담당·근거를 바꾸지 않는다. 적지 않으면 Notes·Detail은 null이다.
    [Fact]
    public void Skill_fields_do_not_change_answers_or_judgment()
    {
        var plain = CaseInput.Valid(CaseInput.Parse(Base + "    ai: {assignee: ②, reason: r}\n"));
        var rich = CaseInput.Valid(CaseInput.Parse("scenario: real\n" + Base + "    ai: {assignee: ②, reason: r, now: 손으로 한다}\n"));

        Assert.Null(plain.Notes);
        Assert.Null(plain.Steps[0].Ai!.Detail);
        Assert.Equal(plain.Steps[0].Answers, rich.Steps[0].Answers);
        Assert.Equal((plain.Steps[0].Ai!.Assignee, plain.Steps[0].Ai!.Reason), (rich.Steps[0].Ai!.Assignee, rich.Steps[0].Ai!.Reason));
        Assert.Equal("real", rich.Notes!.Scenario);
        Assert.Empty(rich.Notes.StatedNumbers);
        Assert.Empty(rich.Notes.SelfCheck);
        Assert.Null(rich.Notes.Summary);
    }

    // 표본 TestData/classification/ai-actions/answers.yaml(T-022 리포트 골든 입력)은 오류 없이 읽힌다.
    [Fact]
    public void Actions_sample_parses()
    {
        var bytes = File.ReadAllBytes(RepoRoot.Combine("tests/Scorecard.Tests/TestData/classification/ai-actions/answers.yaml"));
        var definition = CaseInput.Valid(CaseParser.Parse(bytes, "answers.yaml"));

        Assert.Equal("virtual", definition.Notes!.Scenario);
        Assert.Equal([4, 3, 4, 0], definition.Steps.Select(s => s.Ai!.Detail?.Actions.Count ?? 0));
        Assert.Equal("업무 구조 검토 완료. 지금 제출할 것 없음.", definition.Notes.EndState);
    }
}
