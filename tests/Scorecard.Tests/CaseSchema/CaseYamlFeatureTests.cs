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

    // 경로 비용(T-003 reviewer 비차단 1). 긴 명시 키(? )를 14단계 쌓고 그 밑에 넓은 흐름 목록을 둔다.
    // 묵시 키는 1024자까지라서(YAML 1.2) 긴 키는 명시 키로 쓴다. 깊이는 15라서 depth 오류가 아니다.
    // 오류는 루트의 알 수 없는 키 하나와 목록 끝 항목의 태그 하나뿐이다. 다른 스칼라 299,999개는 오류가 없다.
    // 끝 항목의 태그는 목록을 끝까지 읽었는지, 깊은 경로가 맞는지 보려고 둔다.
    // 경로 문자열을 노드마다 미리 만들면 일이 (경로 길이 약 14만 자 × 항목 30만 개)로 커진다. 입력은 약 1 MB다.
    // 키 1천 자로는 증폭이 상한 안에 들어온다(1천 자 × 14단계 × 3만 개: 약 0.4초). 그래서 키를 1만 자, 항목을 30만 개로 키웠다.
    // 참고(이 저장소 개발 컴퓨터, 2026-11-06): 경로를 노드마다 만드는 구현 약 36초, Parser 이벤트만 읽기 약 0.4초.
    // 상한 10초는 정상 구현(1초 안팎)의 10배다. 시간은 테스트 쪽에서 잰다(테스트 dll은 G1 D1 대상이 아니다).
    // 할당 상한도 둔다(T-003 reviewer 2회차 비차단 N2). 빠른 컴퓨터에서는 노드마다 경로를 만들어도 10초 안에 끝날 수 있다.
    // 할당량은 컴퓨터 속도와 무관하다. Task.Run 안에서 같은 스레드로 동기로 읽으며 잰다. 시간 상한은 그대로 둔다.
    // 참고(이 저장소 개발 컴퓨터, 2026-11-06): 지금 구현 약 137 MB. 노드마다 경로를 만드는 구현은
    // 항목 3만 개에서 이미 약 17 GB였다(30만 개면 약 170 GB). 상한 1 GiB는 지금 구현의 약 7.8배다.
    [Fact]
    public async Task Long_explicit_keys_over_wide_list_do_not_multiply_path_cost()
    {
        const int Levels = 14;
        const int KeyLength = 10_000;
        const int Items = 300_000;
        const long AllocationLimit = 1L << 30;
        var limit = TimeSpan.FromSeconds(10);

        // OneStep은 4줄이다. 단계 l(0부터)의 키는 5 + 2l줄, 목록은 5 + 2·14 = 33줄이다.
        var yaml = new System.Text.StringBuilder(CaseInput.OneStep);
        var keys = new List<string>();
        for (var level = 0; level < Levels; level++)
        {
            var key = new string((char)('a' + level), KeyLength);
            keys.Add(key);
            yaml.Append(' ', 2 * level).Append("? ").Append(key).Append('\n');
            yaml.Append(' ', 2 * level).Append(":\n");
        }

        // 목록 들여쓰기 28칸. '['가 29열, 항목 i(1부터)는 30 + 3(i - 1)열이다(항목 사이 ", ").
        yaml.Append(' ', 2 * Levels).Append('[');
        for (var i = 1; i < Items; i++)
        {
            yaml.Append("x, ");
        }

        yaml.Append("!t x]\n");
        var bytes = CaseInput.Bytes(yaml.ToString());
        var listLine = 5 + (2 * Levels);
        var lastColumn = (2 * Levels) + 2 + (3 * (Items - 1));
        var deepPath = string.Join(".", keys) + $"[{Items}]";

        var parse = Task.Run(() => CaseInput.ParseCountingAllocations(bytes));
        var finished = await Task.WhenAny(parse, Task.Delay(limit)) == parse;
        Assert.True(finished, $"입력 {bytes.Length}바이트를 {limit.TotalSeconds}초 안에 읽지 못했다. 경로 문자열을 노드마다 만드는지 본다.");
        var (result, allocated) = await parse;

        // 경로가 길어서 문자열 전체 비교 대신 길이와 일치 여부를 본다. 실패 출력이 수십만 자가 되지 않게 한다.
        Assert.Null(result.Case);
        Assert.Equal(
            [(5, 3, "unknown-field", KeyLength, "알 수 없는 필드다"), (listLine, lastColumn, "tag", deepPath.Length, "태그(!)는 쓰지 않는다")],
            result.Errors.Select(e => (e.Line, e.Column, e.Code, e.Path.Length, e.Message)));
        Assert.True(result.Errors[0].Path == keys[0], "unknown-field 경로가 첫 키와 다르다.");
        Assert.True(result.Errors[1].Path == deepPath, "tag 경로가 키 14개를 '.'로 이은 뒤 [300000]이 아니다.");
        Assert.True(
            allocated <= AllocationLimit,
            $"입력 {bytes.Length}바이트를 읽으며 {allocated}바이트를 할당했다(상한 {AllocationLimit}바이트). 경로 문자열을 노드마다 만드는지 본다.");
    }

    // 같은 경로의 오류가 많을 때 경로 문자열 비용(T-003 reviewer 2회차 비차단 N1).
    // 긴 명시 키 하나 아래 매핑에 별칭 키(*a : 1)를 많이 둔다. 별칭 키의 오류 경로는 그 매핑의 경로, 곧 긴 키다.
    // 키 자리 오류의 경로가 부모 매핑 경로인 것은 Non_scalar_key_is_type_error와 같다. 별칭 키는 alias만 내고 type은 내지 않는다.
    // 별칭은 앵커 없이 써도 Parser가 읽는다. 그 자리는 더 검사하지 않으므로(4.7) 값 1은 오류가 없다.
    // 오류마다 경로 문자열을 새로 만들면 (키 길이 × 오류 수)만큼 쓴다. 결과가 그 문자열을 모두 쥐어서 GC로 줄지 않는다.
    // 할당량은 컴퓨터 속도와 무관하다. 깊이 3이라 멈출 걱정이 없어 시간 보호 없이 같은 스레드에서 동기로 읽는다.
    // 참고(이 저장소 개발 컴퓨터, 2026-11-06, 키 20만 자 × 별칭 2,500개, 입력 약 220 KB):
    //   오류마다 StringBuilder로 경로를 만드는 지금 구현 약 2.0 GB(결과가 쥐는 경로 문자열만 약 1.0 GB, 최대 작업 집합 약 1.4 GB),
    //   경로 문자열을 노드마다 한 번만 만드는 구현 약 4 MB.
    // 상한 256 MiB: 오류마다 딱 맞는 크기로 한 번만 만들어도(약 1.0 GB) 약 4배 넘는다. 1 GiB로 두면 그 구현이 통과한다.
    // 입력 크기는 지금 구현이 상한을 넘되 테스트 호스트가 메모리 부족으로 죽지 않게(수 GB 이내) 잡았다.
    [Fact]
    public void Many_errors_under_one_long_key_do_not_multiply_path_cost()
    {
        const int KeyLength = 200_000;
        const int Aliases = 2_500;
        const long AllocationLimit = 256L << 20;

        // OneStep은 4줄이다. 키는 5줄 3열, ':'는 6줄, 별칭 키 i(1부터)는 6 + i줄 3열이다.
        var key = new string('k', KeyLength);
        var yaml = new System.Text.StringBuilder(CaseInput.OneStep).Append("? ").Append(key).Append("\n:\n");
        for (var i = 0; i < Aliases; i++)
        {
            yaml.Append("  *a : 1\n");
        }

        var bytes = CaseInput.Bytes(yaml.ToString());

        var (result, allocated) = CaseInput.ParseCountingAllocations(bytes);

        // 오류는 루트의 알 수 없는 키 하나와 별칭 키마다 하나다. 경로가 길어서 길이와 일치 여부를 본다.
        Assert.Null(result.Case);
        Assert.Equal(1 + Aliases, result.Errors.Count);
        Assert.Equal(
            Enumerable.Range(1, Aliases)
                .Select(i => (6 + i, 3, "alias", KeyLength, "별칭(*)은 쓰지 않는다"))
                .Prepend((5, 3, "unknown-field", KeyLength, "알 수 없는 필드다")),
            result.Errors.Select(e => (e.Line, e.Column, e.Code, e.Path.Length, e.Message)));
        Assert.True(result.Errors.All(e => e.Path == key), "오류 경로가 모두 긴 키와 같지는 않다.");
        Assert.True(
            result.Errors[0].ToString() == $"case.yaml:5:3: unknown-field {key}: 알 수 없는 필드다",
            "첫 오류 줄이 'case.yaml:5:3: unknown-field <키>: 알 수 없는 필드다'가 아니다.");
        Assert.True(
            result.Errors[1].ToString() == $"case.yaml:7:3: alias {key}: 별칭(*)은 쓰지 않는다",
            "둘째 오류 줄이 'case.yaml:7:3: alias <키>: 별칭(*)은 쓰지 않는다'가 아니다.");
        Assert.True(
            allocated <= AllocationLimit,
            $"입력 {bytes.Length}바이트를 읽으며 {allocated}바이트를 할당했다(상한 {AllocationLimit}바이트). 같은 경로의 오류마다 경로 문자열을 새로 만드는지 본다.");
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
