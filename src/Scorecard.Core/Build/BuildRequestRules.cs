using System.Text.RegularExpressions;

namespace Scorecard.Core.Build;

// 제작 의뢰서 번들의 값·문장을 이 파일 한 곳에 둔다(classification.md 4.9와 같은 방식). docs/spec/build-request.md, 데이터 docs/spec/data/build-request.md.
// 값을 바꾸려면 스펙 표를 먼저 고친다. 테스트가 스펙 표를 읽어 대조한다.
internal static class BuildRequestRules
{
    // io-schema.json·context.json·build 결과 객체의 schema_version(새 문서라 1, ADR 0008 결정 1).
    public const int SchemaVersion = 1;

    // 번들 폴더(출력 폴더 안)와 파일 이름. 지시문 3.6 표 순서.
    public const string FolderName = "build-request";
    public const string SpecFile = "spec.md";
    public const string IoSchemaFile = "io-schema.json";
    public const string AcceptanceFile = "acceptance.md";
    public const string SamplesReadme = "samples/README.md";
    public const string SamplesCsv = "samples/output.csv";
    public const string ContextFile = "context.json";
    public const string ExportCheckFile = "export-check.md";

    // 오류 코드(데이터 4.6 표). 모두 입력 오류(2)다.
    public const string StepError = "step";
    public const string ExportError = "export-check";
    public const string StepPath = "--step";

    public const string StepMissingText = "없는 단계다: {n}";
    public const string StepNotSoftwareText = "② 단계가 아니다: {n}단계는 {a}";

    // 확인 상태(데이터 4.5 표).
    public const string Confirmed = "confirmed";
    public const string Missing = "missing";
    public const string Unsigned = "unsigned";
    public const string Changed = "changed";
    public const string Hits = "hits";

    public const string MissingText = "사람 확인이 없다. 출력 폴더의 export-check.md를 읽고 '확인한 사람:' 줄에 적은 뒤 다시 돌린다";
    public const string UnsignedText = MissingText;
    public const string ChangedText = "export-check.md가 지금 번들과 다르다. 새로 쓴 export-check.md를 다시 확인한다";
    public const string HitsText = "검사에 걸린 문자열이 {count}개 있다. 답 파일·case.yaml에서 실제 값을 빼고 다시 돌린다";

    // 사람 확인 줄. 초안은 이 글자만 있고 사람이 뒤에 적는다.
    public const string SignaturePrefix = "확인한 사람:";

    // 합성 샘플(데이터 4.3).
    public const int SampleRows = 10;
    public const string SampleKeyPrefix = "s";
    public static readonly string[] SampleTexts = ["value-a", "value-b", "value-c"];
    public static readonly string[] SampleGroups = ["type-a", "type-b"];
    public const string SampleChecklistPrefix = "text-";

    // 열 역할·형식(데이터 4.2).
    public const string RoleKey = "key";
    public const string RoleCompare = "compare";
    public const string RoleChecklist = "checklist";
    public const string RoleGroup = "group";
    public const string TypeText = "text";
    public const string TypeNumber = "number";

    // 문자열 검사 패턴. 이름·정규식은 synthetic-examples.md 데이터 4.10 표와 같다(테스트가 대조한다).
    public static readonly (string Name, string Pattern)[] PatternSource =
    [
        ("email", @"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+"),
        ("url", @"[A-Za-z][A-Za-z0-9+.-]*://"),
        ("windows-path", @"(?<![A-Za-z0-9])[A-Za-z]:[\\/]"),
        ("unc-path", @"\\\\[A-Za-z0-9._$-]+\\"),
        ("posix-path", @"(?<![\w.~-])/[A-Za-z0-9._-]+/"),
        ("home-path", @"(?<!\w)~[/\\]"),
        ("part-letters-digits", @"(?<![A-Za-z0-9])[A-Za-z]{1,4}[-_]?[0-9]{4,}(?![0-9])"),
        ("part-digit-groups", @"(?<![0-9-])[0-9]{2,}-[0-9]{3,}(-[0-9]+)*(?![0-9])"),
        ("part-long-code", @"(?<![A-Za-z0-9])(?=[A-Z0-9]*[A-Z])(?=[A-Z0-9]*[0-9])[A-Z0-9]{8,}(?![A-Za-z0-9])"),
    ];

    public static readonly (string Name, Regex Regex)[] Patterns =
        PatternSource.Select(p => (p.Name, new Regex(p.Pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))).ToArray();

    // 제작 게이트 세 질문(ADR 0004 결정 5, 데이터 4.4 표). 질문, 언제, 볼 것.
    public static readonly (string Question, string When, string Look)[] Gates =
    [
        ("이렇게 만들까요?", "스펙 승인", "spec.md의 입력→처리→출력과 규칙 초안, io-schema.json, 이 문서의 기준 숫자"),
        ("결과를 넣을까요?", "PR 머지", "만든 도구의 테스트 보고서, samples/로 돌린 대조 결과(정확도·누락률), 아래 시나리오 통과"),
        ("2주 써 보고 괜찮나요?", "인수", "2주 동안의 결과를 scorecard check로 다시 잰 정확도·누락률·시간, 사람 검토로 넘긴 건수"),
    ];
}
