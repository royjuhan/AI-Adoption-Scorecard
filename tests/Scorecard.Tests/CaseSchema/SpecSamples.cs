namespace Scorecard.Tests.CaseSchema;

// 스펙 6절·7.3의 예시와 기대 오류 줄. 테스트 코드에 문자열로 둔다(승인 결정 9).
// 문구를 바꾸면 스펙과 이 파일을 같이 고친다. SpecDocumentTests가 스펙 블록과 같은지 본다.
// 파일 샘플(TestData/case-schema/…, TestData/guard/…)도 SpecDocumentTests가 스펙 블록과 비교한다.
internal static class SpecSamples
{
    // TestData/case-schema/ 아래 샘플 폴더 이름.
    public const string ValidFull = "valid-full";
    public const string MissingRequired = "missing-required";
    public const string TypeErrors = "type-errors";
    public const string Example121 = "example-12-1";

    // 6.2 최소.
    public const string Minimal =
        "name: sample-minimal\n" +
        "task: 최소 합성 예시\n" +
        "steps:\n" +
        "  - name: 한 단계\n";

    // 6.5 첫 예시(alias-tag).
    public const string AliasTag =
        "name: sample-alias\n" +
        "task: !custom 태그\n" +
        "base: &common {frequency: weekly}\n" +
        "steps:\n" +
        "  - name: a\n" +
        "    answers: *common\n" +
        "  - name: b\n" +
        "    answers:\n" +
        "      <<: *common\n" +
        "      batch_size: !!int 3\n";

    // 6.5 둘째 예시(duplicates).
    public const string Duplicates =
        "name: sample-dup\n" +
        "task: 중복 합성 예시\n" +
        "name: sample-dup-2\n" +
        "steps:\n" +
        "  - name: 같은 이름\n" +
        "    answers: {batch_size: 1, batch_size: 2}\n" +
        "  - name: 같은 이름\n" +
        "compare:\n" +
        "  - {field: x}\n" +
        "  - {field: x}\n" +
        "inputs: {reference: r/*.csv, candidate: c/*.csv, key: k}\n";

    // 6.3 필드 누락의 오류 4건.
    public static readonly string[] MissingRequiredErrors =
    [
        "case.yaml:1:1: required name: 필수 필드가 없다",
        "case.yaml:3:5: required steps[1].name: 필수 필드가 없다",
        "case.yaml:4:11: required steps[2].name: 값이 비어 있다",
        "case.yaml:7:3: required inputs.key: 필수 필드가 없다",
    ];

    // 6.4 형식 오류의 오류 18건.
    public static readonly string[] TypeErrorsErrors =
    [
        "case.yaml:1:7: format name: 소문자·숫자·'-'만, 64자까지 쓴다",
        "case.yaml:3:11: enum ai_usage: 선택지 밖의 값 'always'. 가능한 값: build-time, runtime, hybrid",
        "case.yaml:7:19: enum steps[1].answers.input_kind: 선택지 밖의 값 'Table'. 가능한 값: table, fixed_form, free_doc, image, conversation",
        "case.yaml:8:18: enum steps[1].answers.frequency: 선택지 밖의 값 'Weekly'. 가능한 값: daily, weekly, monthly, rare",
        "case.yaml:9:19: format steps[1].answers.batch_size: 정수가 아니다: ten",
        "case.yaml:10:23: range steps[1].answers.exception_rate: 범위 밖이다 (0 이상 100 이하): 120",
        "case.yaml:11:25: format steps[1].answers.minutes_per_item: 숫자가 아니다: 5 (따옴표 없이 쓴다)",
        "case.yaml:12:25: enum steps[1].answers.existing_feature: 선택지 밖의 값 'maybe'. 가능한 값: yes, no, unknown",
        "case.yaml:15:19: range steps[2].answers.batch_size: 범위 밖이다 (1 이상 2147483647 이하): 0",
        "case.yaml:16:23: format steps[2].answers.exception_rate: 숫자가 아니다: 1,5",
        "case.yaml:17:22: type steps[2].answers.current_actor: 스칼라 값이어야 한다",
        "case.yaml:18:5: type steps[3]: 매핑이어야 한다",
        "case.yaml:20:5: conflict compare[1]: tolerance와 tolerance_pct 중 하나만 쓴다",
        "case.yaml:21:27: range compare[2].tolerance: 범위 밖이다 (0 이상): -1",
        "case.yaml:23:14: format inputs.reference: 케이스 폴더 기준 상대 경로를 '/'로 쓴다 ('..', ':', '\\' 없음)",
        "case.yaml:24:14: format inputs.candidate: 케이스 폴더 기준 상대 경로를 '/'로 쓴다 ('..', ':', '\\' 없음)",
        "case.yaml:27:17: range thresholds.accuracy_min: 범위 밖이다 (0 이상 100 이하): 101",
        "case.yaml:28:19: format thresholds.time_must_drop: true 또는 false로 쓴다: yes",
    ];

    // 6.5 alias-tag의 오류 7건.
    public static readonly string[] AliasTagErrors =
    [
        "case.yaml:2:7: tag task: 태그(!)는 쓰지 않는다",
        "case.yaml:3:1: unknown-field base: 알 수 없는 필드다",
        "case.yaml:3:7: alias base: 앵커(&)는 쓰지 않는다",
        "case.yaml:6:14: alias steps[1].answers: 별칭(*)은 쓰지 않는다",
        "case.yaml:9:7: unknown-field steps[2].answers.<<: 알 수 없는 필드다",
        "case.yaml:9:11: alias steps[2].answers.<<: 별칭(*)은 쓰지 않는다",
        "case.yaml:10:19: tag steps[2].answers.batch_size: 태그(!)는 쓰지 않는다",
    ];

    // 6.5 duplicates의 오류 4건.
    public static readonly string[] DuplicatesErrors =
    [
        "case.yaml:3:1: duplicate-key name: 중복된 키다 (처음: 1줄)",
        "case.yaml:6:30: duplicate-key steps[1].answers.batch_size: 중복된 키다 (처음: 6줄)",
        "case.yaml:7:11: duplicate steps[2].name: 단계 이름이 중복된다 (처음: steps[1])",
        "case.yaml:10:13: duplicate compare[2].field: 비교 필드가 중복된다 (처음: compare[1])",
    ];

    // 7.3 TestData/guard/case-invalid/case.yaml의 오류 7건.
    public static readonly string[] GuardInvalidErrors =
    [
        "case.yaml:2:1: required name: 필수 필드가 없다",
        "case.yaml:4:5: required steps[1].name: 필수 필드가 없다",
        "case.yaml:4:26: enum steps[1].answers.frequency: 선택지 밖의 값 'Weekly'. 가능한 값: daily, weekly, monthly, rare",
        "case.yaml:4:46: format steps[1].answers.batch_size: 정수가 아니다: 2.5",
        "case.yaml:7:23: format steps[2].answers.exception_rate: 숫자가 아니다: 1,5",
        "case.yaml:8:7: unknown-field steps[2].answers.frequncy: 알 수 없는 필드다",
        "case.yaml:10:19: format thresholds.time_must_drop: true 또는 false로 쓴다: yes",
    ];

    // 4.9 표의 오류 코드 17개. 표 순서.
    public static readonly string[] ErrorCodes =
    [
        "file",
        "encoding",
        "empty",
        "syntax",
        "depth",
        "multi-document",
        "alias",
        "tag",
        "duplicate-key",
        "type",
        "unknown-field",
        "required",
        "enum",
        "format",
        "range",
        "duplicate",
        "conflict",
    ];
}
