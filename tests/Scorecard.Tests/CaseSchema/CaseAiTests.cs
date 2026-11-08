using Scorecard.Core.Cases;

namespace Scorecard.Tests.CaseSchema;

// 스펙 4.14 AI 판단 ai(M2-2, ADR 0013). 데이터 4.14 장면 표를 읽어 행마다 받음·오류를 본다.
// 단계 하나(steps[1])의 ai 값만 바꾼다. 오류는 코드·경로·메시지가 표와 같은 한 건이다.
[Trait("Spec", "case-schema")]
public class CaseAiTests
{
    private const string TableHeader = "| `ai` 장면 | 코드 | 경로 | 메시지 |";
    private const string NoError = "—";
    private const string Prefix = "ai: ";

    public static TheoryData<int> Rows => new(Enumerable.Range(0, SpecDocument.Table(SpecDocument.CaseSchemaPath, TableHeader).Count));

    // 장면 표의 한 행. 코드가 —이면 받고, 메시지 칸의 `담당`이 Ai.Assignee다(없으면 Ai가 null).
    [Theory]
    [MemberData(nameof(Rows))]
    public void Ai_scene_gives_spec_result(int index)
    {
        var c = SpecDocument.Table(SpecDocument.CaseSchemaPath, TableHeader)[index];
        var scene = SpecDocument.Unquote(c[0]);
        Assert.StartsWith(Prefix, scene, StringComparison.Ordinal);

        var result = CaseInput.Parse(StepWith(scene[Prefix.Length..]));

        if (c[1].Trim() == NoError)
        {
            var step = Assert.Single(CaseInput.Valid(result).Steps);
            var assignee = SpecDocument.CodeSpans(c[3]);
            if (assignee.Length == 0)
            {
                Assert.Null(step.Ai);
            }
            else
            {
                Assert.Equal(Assert.Single(assignee), step.Ai?.Assignee);
            }

            return;
        }

        Assert.Null(result.Case);
        var error = Assert.Single(result.Errors);
        Assert.Equal(SpecDocument.Unquote(c[1]), error.Code);
        Assert.Equal(SpecDocument.Unquote(c[2]), error.Path);
        Assert.Equal(SpecDocument.Unquote(c[3]), error.Message);
    }

    // 장면 표가 다섯 담당 중 ②·②M·④ 받음과 type·required·enum·unknown-field 오류를 모두 쓴다.
    [Fact]
    public void Ai_table_covers_accepted_and_error_codes()
    {
        var rows = SpecDocument.Table(SpecDocument.CaseSchemaPath, TableHeader);

        Assert.Contains(rows, r => r[1].Trim() == NoError && SpecDocument.CodeSpans(r[3]).SequenceEqual(["②M"]));
        Assert.Equal(
            ["enum", "required", "type", "unknown-field"],
            rows.Where(r => r[1].Trim() != NoError).Select(r => SpecDocument.Unquote(r[1])).Distinct().Order(StringComparer.Ordinal));
    }

    // 다섯 담당 모두 받는다. 흐름 매핑과 블록 매핑이 같은 결과다. 근거는 쓴 글 그대로다(앞뒤 공백 유지).
    [Theory]
    [InlineData("①")]
    [InlineData("②")]
    [InlineData("②M")]
    [InlineData("③")]
    [InlineData("④")]
    public void Every_assignee_is_accepted_in_flow_and_block_form(string assignee)
    {
        var flow = Assert.Single(CaseInput.Valid(StepWith($"{{assignee: {assignee}, reason: \"  근거 \"}}")).Steps).Ai;
        var block = Assert.Single(CaseInput.Valid(StepWith($"\n      assignee: {assignee}\n      reason: \"  근거 \"")).Steps).Ai;

        Assert.Equal(new StepAi(assignee, "  근거 "), flow);
        Assert.Equal(flow, block);
    }

    // ai는 답·inferred와 상관없다. ai가 있어도 답과 IsInferred는 그대로다.
    [Fact]
    public void Ai_does_not_change_answers()
    {
        var yaml = "name: a\ntask: t\nsteps:\n  - name: s\n    answers: {frequency: daily, human_signoff: no}\n    inferred: [frequency]\n";
        var plain = Assert.Single(CaseInput.Valid(yaml).Steps);
        var withAi = Assert.Single(CaseInput.Valid(yaml + "    ai: {assignee: ③, reason: r}\n").Steps);

        Assert.Equal(plain.Answers, withAi.Answers);
        Assert.Null(plain.Ai);
        Assert.Equal(new StepAi("③", "r"), withAi.Ai);
    }

    // 오류가 있는 ai는 오류 위치가 그 값(빔)이나 부모 매핑 시작(없음)이다(4.9 규칙).
    [Fact]
    public void Ai_error_positions_follow_error_rules()
    {
        CaseInput.AssertErrors(
            StepWith("{reason: 근거}"),
            "case.yaml:6:9: required steps[1].ai.assignee: 필수 필드가 없다");
        CaseInput.AssertErrors(
            StepWith("{assignee: ②, reason: \"\"}"),
            "case.yaml:6:31: required steps[1].ai.reason: 값이 비어 있다");
        CaseInput.AssertErrors(
            StepWith("{assignee: ⑤, reason: 근거}"),
            "case.yaml:6:20: enum steps[1].ai.assignee: 선택지 밖의 값 '⑤'. 가능한 값: ①, ②, ②M, ③, ④");
    }

    private static string StepWith(string aiValue) =>
        $"name: a\ntask: t\nsteps:\n  - name: s\n    answers: {{human_signoff: no}}\n    ai: {aiValue}\n";
}
