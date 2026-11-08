# build-request 데이터

`docs/spec/build-request.md`의 표·블록이다. 테스트가 읽는다. 규칙 글은 본문에 있다. 절 번호가 같다.

## 3. 출력

### 3.1 결과 객체

`build <케이스> --json` 표준 출력(예제 `form-fill`, start → check 뒤, `--step` 없음):

```json
{
  "schema_version": 1,
  "command": "build",
  "files": [],
  "case": "form-fill",
  "step": null,
  "proposal": {
    "step": 2,
    "name": "주문서 양식 읽기",
    "reason": "AI 과잉 후보다. 지금 매번 AI에 맡기지만 규칙으로 된다."
  },
  "candidates": [
    1,
    2,
    3,
    4
  ]
}
```

### 3.2 번들 파일

`<출력 폴더>/build-request/` 안 경로. 순서는 결과 객체 `files` 순서다(지시문 3.6 표 순서). `samples/output.csv`는 출력 열이 있을 때만 있다.

| 파일 | 형식 | 내용 |
| --- | --- | --- |
| `spec.md` | Markdown | 만들 단계의 입력→처리→출력, 규칙 초안, 예외 처리, 사람 게이트, 실행 조건 |
| `io-schema.json` | JSON | 입력 형태, 출력 열 이름·역할·형식·허용 오차 |
| `acceptance.md` | Markdown | 기준 숫자, 지금 검증, 테스트 시나리오, 제작 게이트 세 질문 |
| `samples/README.md` | Markdown | 합성 샘플 설명(실제 값 없음) |
| `samples/output.csv` | CSV | 출력 열의 합성 10행 |
| `context.json` | JSON | 고른 단계의 답 집계값, 스코어카드 요약 셈, 검증 셈·판정·문턱 |
| `export-check.md` | Markdown | 번들 파일 SHA-256, 문자열 검사 결과, 사람의 확인 줄(사람이 적은 파일 그대로) |

## 4. 규칙

### 4.2 spec.md, io-schema.json

`spec.md`의 `##` 절(이 순서).

| `spec.md` 절 | 내용 |
| --- | --- |
| `입력 → 처리 → 출력` | 답의 `input_kind`·`batch_size`·`frequency`, 근거 문장을 규칙으로, 출력 열 수와 키 |
| `규칙 초안` | `compare` 필드마다 허용 오차 또는 글자 비교, 체크리스트 문장 열, 기본값·추정 답 키 |
| `예외 처리` | `exception_rate`, 예외 검토 행(없으면 멈추고 사람에게) |
| `사람 게이트` | 이 단계 뒤 사람 게이트 행, 업무의 다른 사람 확인(스코어카드 `gates`) |
| `실행 조건` | 실행 시 AI·네트워크·외부 프로세스 없음, 결정론, 입력 읽기만 |

`io-schema.json` 출력 열(`output.columns`). 같은 이름은 처음 것만 둔다.

| 역할 | 어디서 | 형식 | 순서 |
| --- | --- | --- | --- |
| `key` | `inputs.key` | `text` | 1 |
| `compare` | `compare` 필드 | `tolerance`·`tolerance_pct`가 있으면 `number`, 없으면 `text` | 2 |
| `checklist` | `checklist.text` | `text` | 3 |
| `group` | `inputs.group` | `text` | 4 |

`io-schema.json`(예제 `form-fill`, 2단계):

```json
{
  "schema_version": 1,
  "case": "form-fill",
  "step": 2,
  "input": {
    "kind": "fixed_form"
  },
  "output": {
    "key": "order_id",
    "columns": [
      {
        "name": "order_id",
        "role": "key",
        "type": "text",
        "tolerance": null,
        "tolerance_pct": null
      },
      {
        "name": "item",
        "role": "compare",
        "type": "text",
        "tolerance": null,
        "tolerance_pct": null
      },
      {
        "name": "quantity",
        "role": "compare",
        "type": "number",
        "tolerance": 0,
        "tolerance_pct": null
      }
    ]
  }
}
```

### 4.3 samples/

| 열 | 값(행 i = 1~10) |
| --- | --- |
| `key` | `s` + i 두 자리(`s01`~`s10`) |
| `compare` 숫자, `tolerance_pct` | 10 + i × 12.25, 소수 두 자리 |
| `compare` 숫자, `tolerance` | i × 10 |
| `compare` 글자 | `value-a`, `value-b`, `value-c`를 차례로 |
| `checklist` | `text-` + i 두 자리 |
| `group` | `type-a`, `type-b`를 차례로 |

`samples/output.csv`(예제 `table-compare`):

```csv
log_id,start_index,length,mean_value,signal_type
s01,10,10,22.25,type-a
s02,20,20,34.50,type-b
s03,30,30,46.75,type-a
s04,40,40,59.00,type-b
s05,50,50,71.25,type-a
s06,60,60,83.50,type-b
s07,70,70,95.75,type-a
s08,80,80,108.00,type-b
s09,90,90,120.25,type-a
s10,100,100,132.50,type-b
```

### 4.4 acceptance.md, context.json

제작 게이트 세 질문(ADR 0004 결정 5). `acceptance.md` 끝의 표와 같다.

| 질문 | 언제 | 답할 때 볼 것 |
| --- | --- | --- |
| "이렇게 만들까요?" | 스펙 승인 | spec.md의 입력→처리→출력과 규칙 초안, io-schema.json, 이 문서의 기준 숫자 |
| "결과를 넣을까요?" | PR 머지 | 만든 도구의 테스트 보고서, samples/로 돌린 대조 결과(정확도·누락률), 아래 시나리오 통과 |
| "2주 써 보고 괜찮나요?" | 인수 | 2주 동안의 결과를 scorecard check로 다시 잰 정확도·누락률·시간, 사람 검토로 넘긴 건수 |

`context.json`(예제 `form-fill`, 2단계):

```json
{
  "schema_version": 1,
  "case": "form-fill",
  "task": "고정 양식 주문서를 받아 품목·수량을 집계하고 주간 보고서를 만든다",
  "step": {
    "number": 2,
    "name": "주문서 양식 읽기",
    "assignee": "②",
    "rule": "ELSE",
    "source": "rule",
    "current_actor": "ai_each_time",
    "input_kind": "fixed_form",
    "frequency": "weekly",
    "batch_size": 20,
    "exception_rate": 5,
    "minutes_per_item": null,
    "failure_impact": "internal_rework"
  },
  "summary": {
    "steps": 4,
    "software": 4,
    "differs": 0,
    "ai_assist": 0,
    "ai_overuse": 1
  },
  "verification": {
    "status": "measured",
    "counts": {
      "reference": 20,
      "candidate": 20,
      "match": 20,
      "mismatch": 0,
      "missing": 0,
      "extra": 0,
      "within": 0
    },
    "accuracy_pct": 100.0,
    "missing_rate_pct": 0.0,
    "verdict": "pass",
    "verdict_rule": "PASS",
    "thresholds": {
      "accuracy_min": 85,
      "missing_max": 5,
      "time_must_drop": false
    }
  }
}
```

### 4.5 사람 확인 상태

`—`는 오류가 없다. `{count}`는 걸린 것의 수다.

| 상태 | 언제 | 코드 | 메시지 |
| --- | --- | --- | --- |
| `confirmed` | 확인 줄 하나, 값이 있음, 나머지가 초안과 같음, 걸린 것 0 | — | — |
| `missing` | 출력 폴더에 `export-check.md`가 없다 | `export-check` | `사람 확인이 없다. 출력 폴더의 export-check.md를 읽고 '확인한 사람:' 줄에 적은 뒤 다시 돌린다` |
| `unsigned` | 확인 줄 값이 비었다 | `export-check` | `사람 확인이 없다. 출력 폴더의 export-check.md를 읽고 '확인한 사람:' 줄에 적은 뒤 다시 돌린다` |
| `changed` | 확인 줄이 하나가 아니거나, 확인 줄 값을 비운 글이 지금 초안과 다르다 | `export-check` | `export-check.md가 지금 번들과 다르다. 새로 쓴 export-check.md를 다시 확인한다` |
| `hits` | 문자열 검사에 걸린 것이 있다(확인 줄과 상관없음) | `export-check` | `검사에 걸린 문자열이 {count}개 있다. 답 파일·case.yaml에서 실제 값을 빼고 다시 돌린다` |

### 4.6 오류

`<case>`는 예제 `signoff-final`을 임시 폴더에 복사한 케이스 폴더, `<missing>`은 없는 폴더다. 모두 `--out <출력> --json`을 붙여 돌린다. `start`가 `예`면 먼저 `start --answers`를 돌린다. 종료 코드는 모두 2, 줄·열은 0, 오류는 하나다. 오류면 `export-check.md`·`build-request/`를 쓰지 않는다.

| 인자 | start | 코드 | 출처 | 경로 | 메시지 |
| --- | --- | --- | --- | --- | --- |
| `build <case>` | 아니오 | `file` | `classification.json` | — | `파일이 없다. start를 먼저 돌린다` |
| `build <missing> --step 1` | 아니오 | `file` | — | `case` | `케이스 폴더가 없다` |
| `build <case> --step x` | 예 | `argument` | — | `--step` | `단계 번호는 1 이상의 정수다` |
| `build <case> --step -1` | 예 | `argument` | — | `--step` | `단계 번호는 1 이상의 정수다` |
| `build <case> --step 12` | 예 | `step` | — | `--step` | `없는 단계다: 12` |
| `build <case> --step 6` | 예 | `step` | — | `--step` | `② 단계가 아니다: 6단계는 ③` |
| `build <case> --step 7` | 예 | `step` | — | `--step` | `② 단계가 아니다: 7단계는 ④` |

### 4.7 예제

`start --answers` → `check` → `open` 뒤. 추천 단계는 스코어카드 제안(`report.md` 데이터 4.4)과 같고, end-to-end 골든은 이 단계를 고른다. 번들 파일 수는 `export-check.md`를 센다.

| 예제 | 추천 단계 | 고를 수 있는 단계 | 번들 파일 수 |
| --- | --- | --- | --- |
| `table-compare` | 1 | 1, 2, 3, 4 | 7 |
| `form-fill` | 2 | 1, 2, 3, 4 | 7 |
| `mixed-exceptions` | 1 | 1, 4 | 6 |
| `reply-drafts` | 1 | 1 | 7 |
| `signoff-final` | 3 | 1, 2, 3, 4, 5 | 7 |

## 6. G2·G3 대상

`json-output.md` 데이터 7절 표의 `cli-build-case-valid` 행이다(명령 대상 표는 한 곳에 둔다).

## 8. 공개 API

```csharp
namespace Scorecard.Core.Build;

public static class BuildRequestBuilder
{
    public const string FolderName = "build-request";
    public const string ExportCheckFile = "export-check.md";
    public const string StepError = "step";
    public const string ExportError = "export-check";

    // 문자열 검사 패턴(이름, 정규식 글자). synthetic-examples.md 데이터 4.10 표와 같다.
    public static IReadOnlyList<(string Name, string Pattern)> Patterns { get; }

    public static IReadOnlyList<int> Candidates(ScorecardResult scorecard);
    public static BuildRequestResult Create(ScorecardResult scorecard, ScorecardSources sources, int step);
    public static ExportCheck CheckExport(BuildRequestBundle bundle, byte[]? exportCheck);
    public static IReadOnlyList<ScanHit> Scan(IReadOnlyList<SourceFile> files);
}

public static class BuildRequestJson
{
    public const int SchemaVersion = 1;
    public static byte[] Result(ScorecardResult scorecard, int? step, IReadOnlyList<string> files);
}

public sealed record BuildRequestBundle(string Case, int Step, string Name, IReadOnlyList<SourceFile> Files, IReadOnlyList<ScanHit> Hits, byte[] ExportCheckDraft);
public sealed record ScanHit(string Pattern, string Path, int Line, string Value);
public sealed record BuildRequestResult(BuildRequestBundle? Bundle, IReadOnlyList<CaseError> Errors);
public sealed record ExportCheck(string Status, string? Signer, CaseError? Error);
```
