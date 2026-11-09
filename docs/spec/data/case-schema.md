# case-schema 데이터

`docs/spec/case-schema.md`의 표·블록·이름표다. 테스트와 oracle이 읽는다. 규칙 글은 본문에 있다.

## 4. 규칙

### 4.2 YamlDotNet 패키지와 G1

| ID | 메타데이터 표 | 금지 조건 (Core만) |
| --- | --- | --- |
| Y1 (제안) | TypeRef | 네임스페이스가 `YamlDotNet`이거나 `YamlDotNet.`으로 시작하는데, `YamlDotNet.Core`와 같지도 않고 `YamlDotNet.Core.`으로 시작하지도 않는다 |

### 4.3 문서 구조 (최상위)

| 키 | 형식 | 필수 | 없을 때 | 규칙 |
| --- | --- | --- | --- | --- |
| `name` | 문자열 | 예 | `required` | `^[a-z0-9]+(-[a-z0-9]+)*\z`, 64자 이하 [P:case name]. 폴더 이름과 같은지는 보지 않는다(G2 작업 폴더 이름이 `input`이다) |
| `task` | 문자열 | 예 | `required` | 자유 서술. 저장만 |
| `before` | 문자열 | 아니오 | null | 자유 서술. 저장만 |
| `after` | 문자열 | 아니오 | null | 자유 서술. 저장만 |
| `ai_usage` | 열거 | 아니오 | null (미입력. 기본값 없음) | `build-time` / `runtime` / `hybrid` |
| `steps` | 목록 | 예 | `required` | 1개 이상. 항목은 4.4 |
| `inputs` | 매핑 | 아니오 | null | 4.5 |
| `compare` | 목록 | 아니오 | 빈 목록 | 4.5 |
| `thresholds` | 매핑 | 아니오 | 세 값 모두 null | 4.5 |
| `scenario` | 열거 | 아니오 | null | T-022. `virtual` / `real`. 스킬이 적는다. 4.15 |
| `summary` | 매핑 | 아니오 | null | T-022. 업무 요약 네 줄 `scale`·`current`·`exception_rate`·`delivery`(문자열, 모두 선택). 4.15 |
| `stated_numbers` | 문자열 목록 | 아니오 | 빈 목록 | T-022. 사용자가 준 숫자 문장 그대로. 4.15 |
| `self_check` | 목록 | 아니오 | 빈 목록 | T-022. 스킬 자기 검사 `{rule, result, note}`(result는 pass 또는 fail). 4.15 |
| `open_questions` | 문자열 목록 | 아니오 | 빈 목록 | T-022. 확인하면 바뀔 수 있는 질문. 4.15 |
| `end_state` | 문자열 | 아니오 | null | T-022. 끝맺는 문장. 4.15 |

### 4.4 단계와 답 10개

| 키 | 형식 | 필수 | 규칙 |
| --- | --- | --- | --- |
| `name` | 문자열 | 예 | 케이스 안에서 서수 비교로 중복 금지(`duplicate`). 유니코드 정규화는 하지 않는다. NFC `가`와 NFD `가`는 다른 이름이다 [P:nfc-nfd] |
| `answers` | 매핑 | 아니오 | 없거나 null이면 10개 모두 기본값 |
| `inferred` | 문자열 목록 | 아니오 | T-008. AI가 추정한 답의 키. 원소는 아래 `answers` 표의 키이고, 그 답은 `answers`에 값이 있어야 한다. 없거나 null이면 추정 답이 없다. 4.13 |
| `ai` | 매핑 | 아니오 | M2-2. AI가 적은 단계 담당(`assignee`: ① ② ②M ③ ④)과 근거(`reason`). 둘 다 필수. 없거나 null이면 AI 판단이 없다. 4.14. T-022 선택 키 `actions`·`now`·`change`·`check`·`gate_note`는 4.15 |

| 키 | 질문 | 형식 | 선택지·범위 | 기본값 |
| --- | --- | --- | --- | --- |
| `input_kind` | 입력은 어떤 형태인가 | 열거 | table / fixed_form / free_doc / image / conversation | `fixed_form` |
| `rule_expressible` | 판단 기준을 글로 쓸 수 있나 | 열거 | yes / partial / no | `partial` |
| `frequency` | 얼마나 자주 반복되나 | 열거 | daily / weekly / monthly / rare | `weekly` |
| `batch_size` | 한 번에 몇 건인가 | 정수 | 1 이상 | `10` |
| `exception_rate` | 예외·이상 건 비율(%) | 숫자 | 0 이상 100 이하 | `20` |
| `current_actor` | 지금 누가 하나 | 열거 | human / ai_each_time / program / mixed | `human` |
| `minutes_per_item` | 건당 걸리는 시간(분) | 숫자 | 0 이상 | `null` |
| `failure_impact` | 틀리면 무슨 일이 생기나 | 열거 | internal_rework / external / safety_legal | `internal_rework` |
| `existing_feature` | 기존 프로그램에 같은 기능이 있나 | 열거 | yes / no / unknown | `unknown` |
| `human_signoff` | 사람이 최종 확인·서명하는 단계인가 | 열거 | yes / no | `no` |

### 4.5 inputs, compare, thresholds

`inputs` (있으면 `reference`·`candidate`·`key`는 필수, `group`은 선택. `group`은 T-012, 10절):

| 키 | 형식 | 규칙 |
| --- | --- | --- |
| `reference` | 문자열 | 케이스 폴더 기준 상대 경로. `/`로 시작하지 않고, `\`·`:`·`..` 조각이 없다(`format`) [S3 type-errors] |
| `candidate` | 문자열 | 같음 |
| `key` | 문자열 | 비어 있지 않음 |
| `group` | 문자열 | 선택. 몰림을 볼 기준 자료의 유형 열 이름(`judgment.md` 4.8). 비어 있지 않음(`required`). `key`·`compare` 필드와 다른 이름(`conflict`). 키 없음·null = 적지 않음. 그 열이 자료에 있는지는 대조가 본다(`metrics.md` 4.7) |

`compare` 항목:

| 키 | 형식 | 필수 | 규칙 |
| --- | --- | --- | --- |
| `field` | 문자열 | 예 | `compare` 안에서 서수 비교로 중복 금지(`duplicate`) |
| `tolerance` | 숫자 | 아니오 | 0 이상. 절대 허용 오차 |
| `tolerance_pct` | 숫자 | 아니오 | 0 이상. 백분율 허용 오차 |

`thresholds`:

| 키 | 형식 | 범위 | 없을 때 |
| --- | --- | --- | --- |
| `accuracy_min` | 숫자 | 0 이상 100 이하 | null (미입력) |
| `missing_max` | 숫자 | 0 이상 100 이하 | null (미입력) |
| `time_must_drop` | 불리언 | `true`·`false` | null (미입력) |

### 4.6 값 형식

| 형식 | 받는 값 | 받지 않는 값 [P:integer][P:number][P:boolean] |
| --- | --- | --- |
| 문자열 | 스칼라. 따옴표 여부 상관없음. null이면 값 없음 | 매핑·목록 → `type` |
| 열거 | 문자열이 선택지와 서수로 같다(대소문자 구분). 따옴표 허용(`"yes"`) | `Weekly`, `Table` → `enum` |
| 정수 | 따옴표 없는 `^-?(0\|[1-9][0-9]*)\z`. `InvariantCulture`로 읽는다 | `010`, `+1`, `1e3`, `.5`, `1.`, `1.50`, `0x10`, `.inf`, `1_000`, `"10"`, `'10'` → `format`. `-0`, `2147483648` → `range` |
| 숫자 | 따옴표 없는 `^-?(0\|[1-9][0-9]*)(\.[0-9]+)?\z`. `decimal`, `InvariantCulture` | `1,5`, `010`, `+1`, `1e3`, `.5`, `1.`, `0x10`, `.inf`, `1_000`, `"10"` → `format`. 범위 밖 → `range` |
| 불리언 | 따옴표 없는 `true`, `false` | `True`, `TRUE`, `yes`, `on`, `"true"`, `1` → `format` |
| null | 따옴표 없는 빈 값, `~`, `null`, `Null`, `NULL` (YAML 1.2 core 스키마) | `""`, `"null"`, `''`은 문자열 |

### 4.7 YAML 기능 제한

| 기능 | 처리 | 근거 |
| --- | --- | --- |
| 문서 둘 이상(`---`) | `multi-document`, 둘째 문서 시작 위치. 첫 문서는 계속 검사 | [S3 multi-doc] |
| 앵커 `&` | `alias` "앵커(&)는 쓰지 않는다" | [S3 alias-tag] |
| 별칭 `*` | `alias` "별칭(*)은 쓰지 않는다". 그 자리는 더 검사하지 않는다 | [S3 alias-tag] |
| 병합 키 `<<` | 특별 취급 없음. 일반 키라서 `unknown-field` | [S3 alias-tag] |
| 태그 `!x`, `!!int` | `tag` | [S3 alias-tag] |
| 같은 매핑에 같은 키 | `duplicate-key`, 둘째 키 위치. 첫 값을 쓴다 | [S3 duplicates] |
| 키가 스칼라가 아님(`? [x]`) | `type` "키는 문자열이어야 한다", 그 키 위치 | [P:edge complex-key] |
| 깊이 16 초과 (루트 매핑이 1단계, 스칼라는 세지 않는다) | `depth`, 그 한 건만 내고 읽기를 멈춘다 | [P:deep-nesting] |

### 4.8 인코딩·줄바꿈

| 입력 바이트 | 결과 | 근거 |
| --- | --- | --- |
| UTF-8, BOM 없음, LF | 정상 | [P:encoding lf] |
| UTF-8 BOM(`EF BB BF`) 하나 | BOM을 떼고 정상. LF 결과와 바이트까지 같음 | [P:encoding bom] |
| CRLF, CR만, BOM+CRLF | 정상. LF 결과와 바이트까지 같음. 줄 번호도 같음 | [P:encoding crlf/cr/bom-crlf] |
| BOM 둘 | 둘째 BOM이 글자(U+FEFF)로 남아 `syntax` (2:5) | [P:encoding double-bom] |
| UTF-16 BOM(`FF FE`, `FE FF`) | `encoding` "UTF-16이다. UTF-8로 저장한다", 1줄 | [P:encoding utf16le-bom/utf16be-bom] |
| UTF-32 BE BOM(`00 00 FE FF`) | `encoding` "UTF-8이 아니다 (바이트 2)" | [P:encoding utf32be-bom] |
| 잘못된 UTF-8(예: CP949 한글) | `encoding` "UTF-8이 아니다 (바이트 N)". 줄 번호는 앞선 LF 수 + 1. 열 없음 | [P:encoding cp949-line3] (3줄, 바이트 70), [P:encoding bom-then-bad] |
| 한글 포함 UTF-16, BOM 없음 | `encoding` (바이트 5) | [P:encoding utf16le-nobom] |
| ASCII만 있는 UTF-16, BOM 없음 | `encoding`이 아니라 `syntax` (1:3) | [P:encoding utf16le-ascii-nobom] |
| 값 안의 NUL 바이트 | `syntax` (2:9) | [P:encoding nul-in-value] |

### 4.9 오류

```
<Source>:<Line>:<Column>: <Code> <Path>: <Message>
```

| 코드 | 언제 | 위치 | 메시지(스파이크 출력) |
| --- | --- | --- | --- |
| `file` | `case.yaml`이 없음 / 읽을 수 없음 | 없음 | `파일이 없다` / `파일을 읽을 수 없다: <예외 타입 이름>` |
| `encoding` | 4.8 | 줄만 | `UTF-16이다. UTF-8로 저장한다` / `UTF-8이 아니다 (바이트 N)` |
| `empty` | 문서가 없음(빈 파일, 주석만, 루트가 null) | 없음 | `내용이 없다` |
| `syntax` | YamlDotNet `YamlException` / 다른 파서 예외(아래 글머리) | 예외의 시작 위치 / 다른 파서 예외는 위치 없음 | `YAML 문법 오류: <영어 메시지>` |
| `depth` | 깊이 16 초과 | 그 노드 | `너무 깊다. 16단계까지 쓴다` |
| `multi-document` | 문서 둘 이상 | 둘째 문서 시작 | `문서가 둘 이상이다. 하나만 쓴다` |
| `alias` | 앵커·별칭 | 그 노드 | `앵커(&)는 쓰지 않는다` / `별칭(*)은 쓰지 않는다` |
| `tag` | 태그 | 그 노드 | `태그(!)는 쓰지 않는다` |
| `duplicate-key` | 같은 매핑에 같은 키 | 둘째 키 | `중복된 키다 (처음: N줄)` |
| `type` | 매핑·목록·스칼라 자리가 틀림 | 그 값 | `매핑이어야 한다` / `목록이어야 한다` / `스칼라 값이어야 한다` |
| `unknown-field` | 표에 없는 키 | 그 키 | `알 수 없는 필드다` |
| `required` | 필수 필드 없음 / 값이 빔 / 단계 0개 / `compare`에 `inputs` 없음 / `inferred` 원소가 null / `inferred`에 적은 답에 값이 없음(T-008) | 없음: 부모 매핑 시작. 빔: 그 값. 단계 0개: 그 목록. `inputs`: `compare` 값. `inferred`: 그 원소(경로는 null 원소면 `steps[n].inferred[m]`, 값이 없으면 `steps[n].answers.<키>`) | `필수 필드가 없다` / `값이 비어 있다` / `단계가 1개 이상 있어야 한다` / `compare가 있으면 inputs가 있어야 한다` / `inferred에 적은 답은 answers에 값이 있어야 한다` |
| `enum` | 선택지 밖 / `inferred` 원소가 답 키가 아님(T-008) | 그 값 / 그 원소 | `선택지 밖의 값 '<값>'. 가능한 값: <목록>` (`inferred`의 목록은 4.4 답 키 10개) |
| `format` | 정수·숫자·불리언·이름·경로 모양 | 그 값 | `정수가 아니다: <값>` / `숫자가 아니다: <값>` (따옴표가 있으면 ` (따옴표 없이 쓴다)`) / `true 또는 false로 쓴다: <값>` / `소문자·숫자·'-'만, 64자까지 쓴다` / 경로 문구 |
| `range` | 범위 밖 | 그 값 | `범위 밖이다 (<범위>): <값>` |
| `duplicate` | 단계 이름·비교 필드·`inferred` 키(T-008) 중복 | 둘째 값 | `단계 이름이 중복된다 (처음: steps[n])` / `비교 필드가 중복된다 (처음: compare[n])` / `추정 답 키가 중복된다 (처음: steps[n].inferred[m])` |
| `conflict` | `tolerance`와 `tolerance_pct` 같이 / `inputs.group`이 `key` 또는 `compare` 필드와 같음(T-012, 10.2) | 그 항목 시작 / 그 값 | `tolerance와 tolerance_pct 중 하나만 쓴다` / `group은 key·compare 필드와 다른 열을 쓴다` |

- `syntax`의 다른 형태(T-003 리뷰 보강): 파서가 `YamlException`이 아닌 예외를 내면 위치 없이(0:0) `YAML 문법 오류: 파서가 읽지 못했다: <예외 타입 이름>` 한 건을 낸다. 예외 메시지는 쓰지 않는다. 예: 흐름 목록의 plain 스칼라 안 U+2028(YamlDotNet 18.1.0이 `InvalidOperationException`).

### 4.11 G2·G3 등록과 빈 출력 규칙

| 이름 | 샘플(`TestData/guard/…`) | 내용 |
| --- | --- | --- |
| `case-parser-valid` | `case-valid/case.yaml` | 6.1의 정상 예시 |
| `case-parser-invalid` | `case-invalid/case.yaml` | 필드 누락·형식 오류·알 수 없는 필드를 섞은 합성 예시(7.3) |

### 4.12 Core 공개 API (제안)

```csharp
public static class CaseParser
{
    public const string FileName = "case.yaml";

    // <caseDirectory>/case.yaml을 읽는다. 오류의 Source는 "case.yaml".
    public static CaseParseResult ParseDirectory(string caseDirectory);

    // 바이트를 검증한다. sourceName은 오류의 Source(상대 이름).
    public static CaseParseResult Parse(byte[] content, string sourceName);
}

// 오류가 하나라도 있으면 Case는 null.
public sealed record CaseParseResult(CaseDefinition? Case, IReadOnlyList<CaseError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record CaseError(string Source, int Line, int Column, string Code, string Path, string Message);
// ToString()은 4.9 형식.

public sealed record CaseDefinition(
    string Name, string Task, string? Before, string? After, string? AiUsage,
    IReadOnlyList<CaseStep> Steps, CaseInputs? Inputs,
    IReadOnlyList<CompareField> Compare, CaseThresholds Thresholds,
    CaseChecklist? Checklist = null, CaseScores? Scores = null, CaseNotes? Notes = null);   // Notes는 T-022(4.15)

public sealed record CaseStep(int Number, string Name, StepAnswers Answers, StepAi? Ai = null);   // Number는 1부터. Ai는 M2-2(4.14)
public sealed record StepAi(string Assignee, string Reason, StepAiDetail? Detail = null);       // Detail은 T-022(4.15)

// T-022(4.15). 스킬이 적고 Core는 옮기기만 한다.
public sealed record StepAiDetail(IReadOnlyList<StepAction> Actions, string? Now, string? Change, string? Check, string? GateNote);
public sealed record StepAction(string Action, string WhoNow, string Proposed, string Source);
public sealed record CaseNotes(string? Scenario, CaseTaskSummary? Summary, IReadOnlyList<string> StatedNumbers,
    IReadOnlyList<SelfCheckItem> SelfCheck, IReadOnlyList<string> OpenQuestions, string? EndState);
public sealed record CaseTaskSummary(string? Scale, string? Current, string? ExceptionRate, string? Delivery);
public sealed record SelfCheckItem(string Rule, string Result, string? Note);

// T-008: 10번째 HumanSignoff. 위치 매개변수라 기존 9인자 생성은 컴파일되지 않는다.
public sealed record StepAnswers(
    Answer<string> InputKind, Answer<string> RuleExpressible, Answer<string> Frequency,
    Answer<int> BatchSize, Answer<decimal> ExceptionRate, Answer<string> CurrentActor,
    Answer<decimal?> MinutesPerItem, Answer<string> FailureImpact, Answer<string> ExistingFeature,
    Answer<string> HumanSignoff);

// T-008: IsInferred는 선택 인자(기본 false). 기존 2인자 생성은 그대로 컴파일된다 [T8:S2].
public sealed record Answer<T>(T Value, bool IsDefault, bool IsInferred = false);

public sealed record CaseInputs(string Reference, string Candidate, string Key, string? Group = null);
public sealed record CompareField(string Field, decimal? Tolerance, decimal? TolerancePct);
public sealed record CaseThresholds(decimal? AccuracyMin, decimal? MissingMax, bool? TimeMustDrop);
```

### 4.13 추정 답 목록 `inferred` (T-008)

```yaml
steps:
  - name: 주문서 양식 읽기
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: weekly, batch_size: 20, human_signoff: no}
    inferred: [frequency, batch_size]
```

### 4.14 AI 판단 `ai` (M2-2)

```yaml
steps:
  - name: 유형 고르기
    answers: {input_kind: table, rule_expressible: partial, frequency: daily, batch_size: 30, human_signoff: no}
    ai:
      assignee: ②M
      reason: 지난 기록으로 학습한 분류 모델을 고정해 쓴다.
```

장면. 단계 하나(`steps[1]`)의 `ai` 값만 바꾼다. 코드가 `—`이면 오류가 아니다. 메시지는 4.9 표의 문구다.

| `ai` 장면 | 코드 | 경로 | 메시지 |
| --- | --- | --- | --- |
| `ai: ~` | — | — | AI 판단 없음 |
| `ai: {assignee: ②, reason: 표를 옮긴다}` | — | — | 담당 `②` |
| `ai: {assignee: ②M, reason: 고정한 모델}` | — | — | 담당 `②M` |
| `ai: {assignee: "④", reason: "사람이 한다"}` | — | — | 담당 `④` |
| `ai: [②]` | `type` | `steps[1].ai` | `매핑이어야 한다` |
| `ai: ②` | `type` | `steps[1].ai` | `매핑이어야 한다` |
| `ai: {reason: 근거}` | `required` | `steps[1].ai.assignee` | `필수 필드가 없다` |
| `ai: {assignee: ②}` | `required` | `steps[1].ai.reason` | `필수 필드가 없다` |
| `ai: {assignee: ②, reason: ""}` | `required` | `steps[1].ai.reason` | `값이 비어 있다` |
| `ai: {assignee: ②, reason: ~}` | `required` | `steps[1].ai.reason` | `값이 비어 있다` |
| `ai: {assignee: ~, reason: 근거}` | `required` | `steps[1].ai.assignee` | `값이 비어 있다` |
| `ai: {assignee: ⑤, reason: 근거}` | `enum` | `steps[1].ai.assignee` | `선택지 밖의 값 '⑤'. 가능한 값: ①, ②, ②M, ③, ④` |
| `ai: {assignee: ②m, reason: 근거}` | `enum` | `steps[1].ai.assignee` | `선택지 밖의 값 '②m'. 가능한 값: ①, ②, ②M, ③, ④` |
| `ai: {assignee: 2, reason: 근거}` | `enum` | `steps[1].ai.assignee` | `선택지 밖의 값 '2'. 가능한 값: ①, ②, ②M, ③, ④` |
| `ai: {assignee: [②], reason: 근거}` | `type` | `steps[1].ai.assignee` | `스칼라 값이어야 한다` |
| `ai: {assignee: ②, reason: [근거]}` | `type` | `steps[1].ai.reason` | `스칼라 값이어야 한다` |
| `ai: {assignee: ②, reason: 근거, note: 메모}` | `unknown-field` | `steps[1].ai.note` | `알 수 없는 필드다` |

### 4.15 스킬 선택 항목 (T-022)

```yaml
scenario: virtual
summary: {scale: 연 1회 집중 기간 10주, current: 담당자가 메일로 받아 정리한다}
stated_numbers: [주 10건, 예외 1건 처리에 15분]
self_check: [{rule: R1 출처 없는 단정 0건, result: pass}]
open_questions: [필수 칸을 비우면 제출이 막히나요?]
end_state: 업무 구조 검토 완료. 지금 제출할 것 없음.
steps:
  - name: 회신 모으기
    answers: {human_signoff: no}
    ai:
      assignee: ②
      reason: 메일을 열고 첨부를 모으는 동작은 규칙으로 할 수 있다.
      actions:
        - {action: 첨부 내려받기, who_now: human, proposed: ②, source: stated}
      now: 담당자가 메일을 하나씩 연다.
      change: 회신을 업체별로 모으는 규칙을 둔다.
      check: 메일 도구에 모으기 기능이 있는지 본다.
      gate_note: 보내기 전에 담당자가 읽는다.
```

| 선택 항목 | 형식 | 규칙 |
| --- | --- | --- |
| `ai.actions` | 목록 | 원소는 매핑 `{action, who_now, proposed, source}`. 넷 다 필수. `who_now`: human / program / ai_each_time / mixed / unknown. `proposed`: ① ② ②M ③ ④. `source`: stated / inferred / default. 빈 목록은 `required` |
| `ai.now`, `ai.change`, `ai.check`, `ai.gate_note` | 문자열 | 선택. 비면(`""`·공백뿐) `required`. 저장만 |
| `summary.*` | 문자열 | 선택. 비면 `required`. 저장만 |
| `stated_numbers`, `open_questions` | 문자열 목록 | 원소가 비면 `required`. 저장만 |
| `self_check` | 목록 | 원소는 매핑 `{rule, result, note}`. `rule`·`result` 필수, `result`: pass / fail |

장면. `ai:`로 시작하면 단계 하나(`steps[1]`)의 `ai` 값이고, 아니면 최상위 줄 하나를 더한다. 코드가 `—`이면 오류가 아니고 결과 칸은 `Detail`(ai 선택 항목)·`Notes`(케이스 메모)가 있는지다.

| T-022 장면 | 코드 | 경로 | 결과·메시지 |
| --- | --- | --- | --- |
| `ai: {assignee: ②, reason: r, now: 손으로 한다}` | — | — | `Detail 있음` |
| `ai: {assignee: ②, reason: r, actions: [{action: 열기, who_now: human, proposed: ②, source: stated}]}` | — | — | `Detail 있음` |
| `ai: {assignee: ③, reason: r, gate_note: 보내기 전에 읽는다}` | — | — | `Detail 있음` |
| `ai: {assignee: ②, reason: r, now: ~}` | — | — | `Detail 없음` |
| `ai: {assignee: ②, reason: r, actions: []}` | `required` | `steps[1].ai.actions` | `동작이 1개 이상 있어야 한다` |
| `ai: {assignee: ②, reason: r, actions: 열기}` | `type` | `steps[1].ai.actions` | `목록이어야 한다` |
| `ai: {assignee: ②, reason: r, actions: [열기]}` | `type` | `steps[1].ai.actions[1]` | `매핑이어야 한다` |
| `ai: {assignee: ②, reason: r, actions: [{action: 열기, who_now: robot, proposed: ②, source: stated}]}` | `enum` | `steps[1].ai.actions[1].who_now` | `선택지 밖의 값 'robot'. 가능한 값: human, program, ai_each_time, mixed, unknown` |
| `ai: {assignee: ②, reason: r, actions: [{action: 열기, who_now: human, proposed: ⑤, source: stated}]}` | `enum` | `steps[1].ai.actions[1].proposed` | `선택지 밖의 값 '⑤'. 가능한 값: ①, ②, ②M, ③, ④` |
| `ai: {assignee: ②, reason: r, actions: [{action: 열기, who_now: human, proposed: ②, source: guess}]}` | `enum` | `steps[1].ai.actions[1].source` | `선택지 밖의 값 'guess'. 가능한 값: stated, inferred, default` |
| `ai: {assignee: ②, reason: r, actions: [{action: 열기, who_now: human, proposed: ②}]}` | `required` | `steps[1].ai.actions[1].source` | `필수 필드가 없다` |
| `ai: {assignee: ②, reason: r, actions: [{action: 열기, who_now: human, proposed: ②, source: stated, note: x}]}` | `unknown-field` | `steps[1].ai.actions[1].note` | `알 수 없는 필드다` |
| `ai: {assignee: ②, reason: r, gate_note: ""}` | `required` | `steps[1].ai.gate_note` | `값이 비어 있다` |
| `ai: {assignee: ②, reason: r, check: [a]}` | `type` | `steps[1].ai.check` | `스칼라 값이어야 한다` |
| `scenario: virtual` | — | — | `Notes 있음` |
| `scenario: demo` | `enum` | `scenario` | `선택지 밖의 값 'demo'. 가능한 값: virtual, real` |
| `summary: {scale: 주 10건}` | — | — | `Notes 있음` |
| `summary: {size: 10}` | `unknown-field` | `summary.size` | `알 수 없는 필드다` |
| `summary: 주 10건` | `type` | `summary` | `매핑이어야 한다` |
| `stated_numbers: [주 10건, 15분]` | — | — | `Notes 있음` |
| `stated_numbers: 주 10건` | `type` | `stated_numbers` | `목록이어야 한다` |
| `stated_numbers: [""]` | `required` | `stated_numbers[1]` | `값이 비어 있다` |
| `open_questions: [막히나요]` | — | — | `Notes 있음` |
| `self_check: [{rule: R1, result: pass}]` | — | — | `Notes 있음` |
| `self_check: [{rule: R1, result: ok}]` | `enum` | `self_check[1].result` | `선택지 밖의 값 'ok'. 가능한 값: pass, fail` |
| `self_check: [{result: pass}]` | `required` | `self_check[1].rule` | `필수 필드가 없다` |
| `end_state: 업무 구조 검토 완료` | — | — | `Notes 있음` |
| `end_state: ""` | `required` | `end_state` | `값이 비어 있다` |

## 5. 예외

| 상황 | 결과 | 근거 |
| --- | --- | --- |
| `ParseDirectory`에 빈 문자열·null | `ArgumentException`·`ArgumentNullException` 예외. 현재 폴더를 쓰지 않는다(4.10). 상대 경로(`.`)는 받는다 | T-003 리뷰 2회차 |
| `case.yaml` 없음, 폴더 없음 | `case.yaml: file: 파일이 없다`. 절대 경로 없음 | [P:missing-file] |
| 빈 파일, 주석만 | `case.yaml: empty: 내용이 없다` | [S3 empty, comment-only] |
| 루트가 목록 | `case.yaml:1:1: type: 매핑이어야 한다` | [S3 root-list] |
| `steps: []` | `case.yaml:3:8: required steps: 단계가 1개 이상 있어야 한다` | [P:edge empty-steps] |
| `steps`가 매핑 | `type steps: 목록이어야 한다` | [P:edge steps-mapping] |
| 문법 오류 | `syntax` 한 건 | [S3 yaml-syntax] |
| 탭 들여쓰기 | `syntax` "found invalid tab as indentation". 위치가 탭 자리가 아니라 문서 앞(1:1)이다 | [P:edge tab-indent-root, tab-indent-nested] |
| UTF-16·CP949 | `encoding` 한 건 | [P:encoding …] |
| 깊이 10만 | `depth` 한 건, 2초 안 | [P:deep-nesting 100000] |
| 오류와 정상 값이 섞임 | 모델 없음, 오류만 | 4.12 설계 |
| NFC·NFD로 같은 단계 이름 | 다른 이름. 중복 오류 없음 | [P:nfc-nfd] |
| 매우 큰 평평한 파일(약 2.1 MB) | `Parser` 이벤트 읽기 227ms | [T] |
| publish 단일 exe(`win-x64`)에서 동작 | **미확인** | — |

## 6. 예시

### 6.1 정상 (전체)

```yaml
# 합성 예시. 실제 업무·값 없음.
name: sample-full
task: 합성 센서 로그에서 구간을 골라 평균을 낸다
before: 사람이 그래프를 보고 구간을 고른다
after: |
  프로그램이 구간을 고른다.
  사람이 검토한다.
ai_usage: build-time
steps:
  - name: 로그 파일 열기
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: weekly, batch_size: 10, exception_rate: 5, current_actor: program, failure_impact: internal_rework, existing_feature: no}
  - name: 구간 선택(불규칙)
    answers:
      input_kind: table
      rule_expressible: partial
      frequency: weekly
      batch_size: 3
      exception_rate: 62.5
      current_actor: human
      minutes_per_item: 7.5
      failure_impact: internal_rework
      existing_feature: no
  - name: 결과 확인
    answers:
      rule_expressible: "no"
      failure_impact: external
      minutes_per_item: ~
  - name: 보고서 채우기
inputs:
  reference: reference/*.csv
  candidate: candidate/*.csv
  key: row_id
compare:
  - {field: start, tolerance: 0}
  - {field: mean, tolerance_pct: 1.0}
  - {field: label}
thresholds:
  accuracy_min: 90
  missing_max: 5
  time_must_drop: true
```

| 위치 | 값 | IsDefault |
| --- | --- | --- |
| 1단계 `minutes_per_item` | null | true (키 없음) |
| 2단계 `exception_rate` | 62.5 | false |
| 3단계 `input_kind` | fixed_form | true (키 없음) |
| 3단계 `rule_expressible` | no | false (`"no"` 따옴표) |
| 3단계 `minutes_per_item` | null | true (`~`) |
| 4단계 10개 전부 | 3.2 기본값 | true (`answers` 없음) |
| 1~4단계 `human_signoff` | no | true (키 없음, T-008) |
| `after` | `프로그램이 구간을 고른다.\n사람이 검토한다.\n` | — |

### 6.2 최소

```yaml
name: sample-minimal
task: 최소 합성 예시
steps:
  - name: 한 단계
```

### 6.3 필드 누락

```yaml
task: 필수 필드가 빠진 합성 예시
steps:
  - answers: {input_kind: table}
  - name: ""
  - name: 둘째 단계
inputs:
  reference: reference/*.csv
  candidate: candidate/*.csv
```

```
case.yaml:1:1: required name: 필수 필드가 없다
case.yaml:3:5: required steps[1].name: 필수 필드가 없다
case.yaml:4:11: required steps[2].name: 값이 비어 있다
case.yaml:7:3: required inputs.key: 필수 필드가 없다
```

### 6.4 형식 오류

`tests/Scorecard.Tests/TestData/case-schema/type-errors/case.yaml`:

```yaml
name: Sample_Bad
task: 형식 오류 합성 예시
ai_usage: always
steps:
  - name: 첫 단계
    answers:
      input_kind: Table
      frequency: Weekly
      batch_size: ten
      exception_rate: 120
      minutes_per_item: "5"
      existing_feature: maybe
  - name: 둘째 단계
    answers:
      batch_size: 0
      exception_rate: 1,5
      current_actor: [human]
  - 셋째 단계
compare:
  - {field: a, tolerance: 1, tolerance_pct: 2}
  - {field: b, tolerance: -1}
inputs:
  reference: /data/ref.csv
  candidate: ../candidate/*.csv
  key: id
thresholds:
  accuracy_min: 101
  time_must_drop: yes
```

```
case.yaml:1:7: format name: 소문자·숫자·'-'만, 64자까지 쓴다
case.yaml:3:11: enum ai_usage: 선택지 밖의 값 'always'. 가능한 값: build-time, runtime, hybrid
case.yaml:7:19: enum steps[1].answers.input_kind: 선택지 밖의 값 'Table'. 가능한 값: table, fixed_form, free_doc, image, conversation
case.yaml:8:18: enum steps[1].answers.frequency: 선택지 밖의 값 'Weekly'. 가능한 값: daily, weekly, monthly, rare
case.yaml:9:19: format steps[1].answers.batch_size: 정수가 아니다: ten
case.yaml:10:23: range steps[1].answers.exception_rate: 범위 밖이다 (0 이상 100 이하): 120
case.yaml:11:25: format steps[1].answers.minutes_per_item: 숫자가 아니다: 5 (따옴표 없이 쓴다)
case.yaml:12:25: enum steps[1].answers.existing_feature: 선택지 밖의 값 'maybe'. 가능한 값: yes, no, unknown
case.yaml:15:19: range steps[2].answers.batch_size: 범위 밖이다 (1 이상 2147483647 이하): 0
case.yaml:16:23: format steps[2].answers.exception_rate: 숫자가 아니다: 1,5
case.yaml:17:22: type steps[2].answers.current_actor: 스칼라 값이어야 한다
case.yaml:18:5: type steps[3]: 매핑이어야 한다
case.yaml:20:5: conflict compare[1]: tolerance와 tolerance_pct 중 하나만 쓴다
case.yaml:21:27: range compare[2].tolerance: 범위 밖이다 (0 이상): -1
case.yaml:23:14: format inputs.reference: 케이스 폴더 기준 상대 경로를 '/'로 쓴다 ('..', ':', '\' 없음)
case.yaml:24:14: format inputs.candidate: 케이스 폴더 기준 상대 경로를 '/'로 쓴다 ('..', ':', '\' 없음)
case.yaml:27:17: range thresholds.accuracy_min: 범위 밖이다 (0 이상 100 이하): 101
case.yaml:28:19: format thresholds.time_must_drop: true 또는 false로 쓴다: yes
```

### 6.5 YAML 기능·중복

`alias-tag`:

```yaml
name: sample-alias
task: !custom 태그
base: &common {frequency: weekly}
steps:
  - name: a
    answers: *common
  - name: b
    answers:
      <<: *common
      batch_size: !!int 3
```

```
case.yaml:2:7: tag task: 태그(!)는 쓰지 않는다
case.yaml:3:1: unknown-field base: 알 수 없는 필드다
case.yaml:3:7: alias base: 앵커(&)는 쓰지 않는다
case.yaml:6:14: alias steps[1].answers: 별칭(*)은 쓰지 않는다
case.yaml:9:7: unknown-field steps[2].answers.<<: 알 수 없는 필드다
case.yaml:9:11: alias steps[2].answers.<<: 별칭(*)은 쓰지 않는다
case.yaml:10:19: tag steps[2].answers.batch_size: 태그(!)는 쓰지 않는다
```

`duplicates`:

```yaml
name: sample-dup
task: 중복 합성 예시
name: sample-dup-2
steps:
  - name: 같은 이름
    answers: {batch_size: 1, batch_size: 2}
  - name: 같은 이름
compare:
  - {field: x}
  - {field: x}
inputs: {reference: r/*.csv, candidate: c/*.csv, key: k}
```

```
case.yaml:3:1: duplicate-key name: 중복된 키다 (처음: 1줄)
case.yaml:6:30: duplicate-key steps[1].answers.batch_size: 중복된 키다 (처음: 6줄)
case.yaml:7:11: duplicate steps[2].name: 단계 이름이 중복된다 (처음: steps[1])
case.yaml:10:13: duplicate compare[2].field: 비교 필드가 중복된다 (처음: compare[1])
```

### 6.7 추정 답 목록 오류 (T-008)

```yaml
# 합성 오류 예시. 실제 업무·값 없음. 추정 답 목록(inferred)의 오류를 섞었다.
name: inferred-errors
task: 추정 답 목록 오류 합성 예시
steps:
  - name: 답 키가 아님
    answers: {frequency: weekly, batch_size: 10, human_signoff: no}
    inferred: [frequency, freq, Batch_size, name]
  - name: 중복
    answers: {frequency: weekly, human_signoff: no}
    inferred: [frequency, frequency]
  - name: 값 없음
    answers: {frequency: weekly, minutes_per_item: ~, human_signoff: no}
    inferred: [batch_size, minutes_per_item, ~]
  - name: 목록 아님
    answers: {human_signoff: no}
    inferred: human_signoff
```

```text
case.yaml:7:27: enum steps[1].inferred[2]: 선택지 밖의 값 'freq'. 가능한 값: input_kind, rule_expressible, frequency, batch_size, exception_rate, current_actor, minutes_per_item, failure_impact, existing_feature, human_signoff
case.yaml:7:33: enum steps[1].inferred[3]: 선택지 밖의 값 'Batch_size'. 가능한 값: input_kind, rule_expressible, frequency, batch_size, exception_rate, current_actor, minutes_per_item, failure_impact, existing_feature, human_signoff
case.yaml:7:45: enum steps[1].inferred[4]: 선택지 밖의 값 'name'. 가능한 값: input_kind, rule_expressible, frequency, batch_size, exception_rate, current_actor, minutes_per_item, failure_impact, existing_feature, human_signoff
case.yaml:10:27: duplicate steps[2].inferred[2]: 추정 답 키가 중복된다 (처음: steps[2].inferred[1])
case.yaml:13:16: required steps[3].answers.batch_size: inferred에 적은 답은 answers에 값이 있어야 한다
case.yaml:13:28: required steps[3].answers.minutes_per_item: inferred에 적은 답은 answers에 값이 있어야 한다
case.yaml:13:46: required steps[3].inferred[3]: 값이 비어 있다
case.yaml:16:15: type steps[4].inferred: 목록이어야 한다
```

## 7. 테스트 표본

### 7.3 가드 테스트 변경 (tests/Guards)

```yaml
# 합성 오류 예시. 실제 업무·값 없음. 필드 누락·형식 오류·알 수 없는 필드를 섞었다.
task: 오류 합성 예시
steps:
  - answers: {frequency: Weekly, batch_size: 2.5}
  - name: 둘째 단계
    answers:
      exception_rate: 1,5
      frequncy: weekly
thresholds:
  time_must_drop: yes
```

```
case.yaml:2:1: required name: 필수 필드가 없다
case.yaml:4:5: required steps[1].name: 필수 필드가 없다
case.yaml:4:26: enum steps[1].answers.frequency: 선택지 밖의 값 'Weekly'. 가능한 값: daily, weekly, monthly, rare
case.yaml:4:46: format steps[1].answers.batch_size: 정수가 아니다: 2.5
case.yaml:7:23: format steps[2].answers.exception_rate: 숫자가 아니다: 1,5
case.yaml:8:7: unknown-field steps[2].answers.frequncy: 알 수 없는 필드다
case.yaml:10:19: format thresholds.time_must_drop: true 또는 false로 쓴다: yes
```

## 10. `inputs.group`

### 10.2 검사와 오류

| inputs.group 줄 | 결과 | 코드 | 메시지 |
| --- | --- | --- | --- |
| `group: signal_type` | 받음 | — | — |
| (줄 없음) | 받음 | — | — |
| `group: null` | 받음 | — | — |
| `group: Log_id` | 받음 | — | — |
| `group: ""` | 오류 | `required` | `값이 비어 있다` |
| `group: "  "` | 오류 | `required` | `값이 비어 있다` |
| `group: [signal_type]` | 오류 | `type` | `스칼라 값이어야 한다` |
| `group: {a: b}` | 오류 | `type` | `스칼라 값이어야 한다` |
| `group: log_id` | 오류 | `conflict` | `group은 key·compare 필드와 다른 열을 쓴다` |
| `group: length` | 오류 | `conflict` | `group은 key·compare 필드와 다른 열을 쓴다` |

### 10.3 Core API

```csharp
public sealed record CaseInputs(string Reference, string Candidate, string Key, string? Group = null);
```
