namespace Scorecard.Tests.CaseSchema;

// 스펙 4.7 YAML 기능 제한. 표 각 행.
// 문서 1개, 앵커·별칭·태그 금지, 병합 키는 일반 키, 중복 키 오류(첫 값을 씀), 키는 스칼라, 깊이 16.
[Trait("Spec", "case-schema")]
public class CaseYamlFeatureTests
{
    private const string DepthMessage = "너무 깊다. 16단계까지 쓴다";

    // 문서 둘 이상: 둘째 문서 시작 위치. 첫 문서는 계속 검사한다.
    [Fact]
    public void Second_document_is_reported_and_first_document_is_still_checked()
    {
        CaseInput.AssertErrors(
            "name: a\nsteps:\n  - name: s\n---\nname: b\n",
            "case.yaml:1:1: required task: 필수 필드가 없다",
            "case.yaml:4:1: multi-document: 문서가 둘 이상이다. 하나만 쓴다");
    }

    // 앵커(&)와 별칭(*). 별칭 자리는 더 검사하지 않는다(그 자리의 필수·형식 오류가 없다).
    [Fact]
    public void Anchor_and_alias_are_reported_at_node()
    {
        CaseInput.AssertErrors(
            "name: a\ntask: t\nsteps:\n  - &first\n    name: s\n  - *first\n",
            "case.yaml:4:5: alias steps[1]: 앵커(&)는 쓰지 않는다",
            "case.yaml:6:5: alias steps[2]: 별칭(*)은 쓰지 않는다");
    }

    // 병합 키 <<는 특별 취급하지 않는다. 일반 키라서 unknown-field. 값이 별칭이면 alias도.
    [Fact]
    public void Merge_key_is_unknown_field()
    {
        CaseInput.AssertErrors(
            CaseInput.OneStepWithAnswers + "      <<: {frequency: weekly}\n",
            "case.yaml:6:7: unknown-field steps[1].answers.<<: 알 수 없는 필드다");
    }

    // 태그 !x, !!int, !!str.
    [Theory]
    [InlineData("batch_size: !!int 3", "case.yaml:6:19: tag steps[1].answers.batch_size: 태그(!)는 쓰지 않는다")]
    [InlineData("frequency: !!str weekly", "case.yaml:6:18: tag steps[1].answers.frequency: 태그(!)는 쓰지 않는다")]
    [InlineData("frequency: !custom weekly", "case.yaml:6:18: tag steps[1].answers.frequency: 태그(!)는 쓰지 않는다")]
    public void Tag_is_reported_at_node(string line, string expected)
    {
        CaseInput.AssertErrors(CaseInput.OneStepWithAnswers + "      " + line + "\n", expected);
    }

    // 같은 매핑에 같은 키: 둘째 키 위치. 첫 값을 쓴다(둘째 값은 검사하지 않는다).
    [Fact]
    public void Duplicate_key_keeps_first_value()
    {
        CaseInput.AssertErrors(
            "name: sample-a\ntask: t\nname: Bad_Name\nsteps:\n  - name: s\n",
            "case.yaml:3:1: duplicate-key name: 중복된 키다 (처음: 1줄)");
        CaseInput.AssertErrors(
            "name: Bad_Name\ntask: t\nname: sample-a\nsteps:\n  - name: s\n",
            "case.yaml:1:7: format name: 소문자·숫자·'-'만, 64자까지 쓴다",
            "case.yaml:3:1: duplicate-key name: 중복된 키다 (처음: 1줄)");
    }

    // 키가 스칼라가 아님(? [x]): 그 키 위치에 type.
    [Fact]
    public void Non_scalar_key_is_type_error()
    {
        CaseInput.AssertErrors(
            CaseInput.OneStep + "? [x]\n: 1\n",
            "case.yaml:5:3: type: 키는 문자열이어야 한다");
    }

    // 깊이 경계. 루트 매핑이 1단계다. 모음(매핑·목록)이 16단계까지는 깊이 오류가 없다.
    // steps 값 목록이 2단계라서 "[" 15개면 가장 안쪽이 16단계, 16개면 17단계다.
    [Fact]
    public void Collections_up_to_depth_sixteen_are_read()
    {
        CaseInput.AssertErrors(
            "name: a\ntask: t\nsteps: " + new string('[', 15) + new string(']', 15) + "\n",
            "case.yaml:3:9: type steps[1]: 매핑이어야 한다");
    }

    [Fact]
    public void Collection_at_depth_seventeen_is_depth_error()
    {
        CaseInput.AssertErrors(
            "name: a\ntask: t\nsteps: " + new string('[', 16) + new string(']', 16) + "\n",
            "case.yaml:3:23: depth steps" + string.Concat(Enumerable.Repeat("[1]", 15)) + ": " + DepthMessage);
    }

    // 깊이 10만: 건너뛰지 않고 바로 멈춰 depth 한 건으로 끝난다(5절).
    // 시간은 재지 않는다. 멈춤 방지 상한만 둔다(건너뛰기 구현은 이 상한을 크게 넘는다).
    [Fact]
    public async Task Deep_nesting_stops_with_single_depth_error()
    {
        var yaml = "name: a\ntask: t\nsteps: " + new string('[', 100_000) + new string(']', 100_000) + "\n";

        var result = await Task.Run(() => CaseInput.Parse(yaml)).WaitAsync(TimeSpan.FromSeconds(60));

        CaseInput.AssertErrors(
            result,
            "case.yaml:3:23: depth steps" + string.Concat(Enumerable.Repeat("[1]", 15)) + ": " + DepthMessage);
    }

    // 블록 매핑 중첩도 같은 깊이 규칙이다. answers 아래에 매핑을 깊게 쌓는다.
    [Fact]
    public void Deep_block_mapping_is_depth_error()
    {
        var yaml = new System.Text.StringBuilder(CaseInput.OneStepWithAnswers);
        for (var level = 0; level < 20; level++)
        {
            yaml.Append(new string(' ', 6 + (2 * level))).Append("k:\n");
        }

        yaml.Append(new string(' ', 46)).Append("v: 1\n");

        var error = Assert.Single(CaseInput.Parse(yaml.ToString()).Errors);
        Assert.Equal("depth", error.Code);
        Assert.Equal(DepthMessage, error.Message);
    }
}
