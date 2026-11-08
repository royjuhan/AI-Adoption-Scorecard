# judgment (T-011 스펙: 판정)

- 상태: 채택됨(2026-11-07, 리드가 추천안 1~12 채택. ADR 0005 결정 3. 기록은 카드 "잠정 결정"과 `docs/STATUS.md`). 이전 상태: 초안. ADR 0005 결정 3에 따라 승인 질문은 juhan에게 묻지 않고 추천안을 채택한다. 추천안은 회귀를 막는 쪽(기존 expected 바이트 유지, 기존 테스트 축소 없음)으로 골랐다. 질문과 채택 기록은 문서 끝.
- T-012 개정: 채택(ADR 0005 결정 3, STATUS 잠정 결정 T-012), 2026-11-07. 검토 항목(불일치 + 추가 행 + 결과 쪽 중복 행), 사람이 아직 보지 않은 검토 항목은 조건부 `UNEXPLAINED`, 몰림 M-A(`inputs.group`), judgment.json `schema_version` 2. 왜 바꿨는지는 11절(기록).
- T-013 개정(초안, 12절): T-012 표·글을 4·6·7절 제자리로 옮기고 T-011 표·블록을 지웠다(T-012 잠정 결정 12). 5절에 정의·대조 결과가 어긋나면 `ArgumentException`(12.2). 지금 규칙은 1~10절이다.
- 작업 카드: `docs/tasks/T-011-judgment.md`. 판정 카드는 T-010에서 T-011로 번호가 바뀌었다(ADR 0005 결정 1).
- 출발점: 지시문 3.4 판정 문장·3.5 4구역·6구역·12절 표 [읽음], `metrics.md` 3.1(`CsvRow`)·4.8 권고·11절·승인 질문 16 [읽음], `case-schema.md` 4.5(`thresholds`)·승인 질문 11 [읽음], `synthetic-examples.md` 4.7 [읽음], `signoff-final.md` [읽음], ADR 0004 결정 2·6과 expected 변경 규칙 [읽음], `src/Scorecard.Core/Comparison/` [읽음].
- 작성: prototyper, 2026-11-07
- 확인 대상 커밋: `bf2568e`. `src/`·`tests/`·`examples/`는 `7cd767b`(T-010 커밋. ADR 0005, `CoreGuardTargetTests` 정확한 목록 비교, 판정 카드 T-011)와 같다. `src/`·`examples/`는 main `f2dace2`와도 같다. 스파이크는 이 커밋을 clone하고 작업 트리의 이 문서·`spikes/T-011/`을 복사해 돌렸다(`summary.txt`의 `repo HEAD` 줄).
- 확인 환경: Ubuntu 24.04 x64, .NET SDK 10.0.114, YamlDotNet 18.1.0, xUnit 2.9.3. 독립 구현 대조에만 Python 3.13.
- 스파이크: `spikes/T-011/` (머지 대상 아님. 머지 전에 빠진다). 실행은 `spikes/T-011/run-spike.sh <작업폴더> <로그폴더>`. 요약 원문은 `spikes/T-011/summary.txt`. 이 문서의 JSON 블록, SHA-256, 문장은 스파이크 후보(`spikes/T-011/core/Judgment/`)가 실제로 낸 것이다.

## 0. 표기

- `[읽음]`: 커밋된 파일을 읽어서 확인한 사실.
- `[T11:S0]`~`[T11:S8]`: 스파이크 단계(10절). `[T11:P:무리]`: S4 `PROBE` 줄. `[T11:spec 표]`: S8 `PROBE spec` 줄.
- **미확인**: 실험하지 않았거나 이 환경에서 확인할 수 없는 것.
- 판정 값: `pass`(통과), `conditional`(조건부), `hold`(보류), `not_verified`(미검증). 지시문 3.4의 넷이다.
- 불일치 = 값 불일치(`mismatch`) + 누락(`missing`) 항목. 기준 행만 센다. 추가 행(`extra`)·중복 행(`duplicate`)은 넣지 않는다. `metrics.md` 4.4의 항목이다.
- 검토 항목 = 불일치 + 추가 행 + 결과 쪽 중복 행(4.9). 사람이 보고 설명이 되는지 적는 대상이다.
- 유형 열 = `case.yaml` `inputs.group`에 적은 기준 자료의 열 하나(4.8). T-011의 "키·대조 열 밖의 열 전부"(M-B)는 T-012에서 없앴다.
- `CompareMetrics`·`CompareItem`·`CsvRow`·`MetricsJson`은 `metrics.md` 4.11, `CaseDefinition`·`CaseThresholds`는 `case-schema.md`의 이름이다. G1·G2·G3, D1, R1~R4는 `docs/spec/guard-tests.md`의 이름이다.

## 1. 범위

카드 수용 기준 1~4와 카드 메모를 다룬다. 수용 기준 5(1282 이상, 삭제·Skip 없음)는 스파이크와 PR에서, 6(CI 녹색)은 PR에서 확인한다.

| 카드 수용 기준·메모 | 이 문서 | 질문 |
| --- | --- | --- |
| 1 예제 4종 판정이 12절과 맞는다(signoff-final은 스펙이 정함) | 7절 | 9 |
| 2 판정 규칙 표(문턱 경계, 시간, 몰림, 설명 안 되는 불일치). 경계는 셈으로 | 4.3~4.10, 6절 | 1~6 |
| 3 시간 자료가 없을 때의 판정과 표시 | 4.10 | 4 |
| 4 판정 결과를 담는 곳과 expected | 3.2, 4.11, 7절 | 7, 8 |
| 메모: 몰림에 쓸 열 이름이 `case.yaml`에 없다 | 4.8 | 5 |
| 메모: 시간 입력을 어디서 받을지 | 2절, 4.10 | 4 |
| 리드 지시: Core API, G1·G2·G3, 기존 테스트 영향 | 4.12, 4.13, 8절 | 10, 11 |
| 리드 지시(ADR 0005): 추천안은 회귀를 막는 쪽, expected는 독립 구현이 같은 바이트를 낸다 | 4.11, 7.1, 문서 끝 | 전부 |

범위 밖:

- 시간 재기와 `times.yaml` 모양. 내부 명령 `measure`(사용자 명령 `check --times`, M2-5). 이 문서는 판정이 받는 시간 값(`JudgmentTimes`)만 정한다(4.10).
- 사람이 불일치를 보고 "설명 안 됨"을 적는 화면과 `review.yaml` 모양. 확인 한 화면(`review`, M2-4). 이 문서는 판정이 받는 키 목록만 정한다(4.9).
- 명령 `judge`·`check`의 인자·화면 문구, `--json`을 쓰는 코드. M2(3.4는 적용만).
- 스코어카드 4구역(검증 결과)·6구역(다음 할 일)의 HTML 문구와 `report-outline.json`. M2-3(`report.md`).
- 재작업률, 완료당 비용(지시문 3.4 표). 판정 문장에 쓰이지 않는다.
- AI 과잉 후보. 분류 카드(T-004)에 이미 있다(`classification.md` 4.8).

## 2. 입력

- `CaseDefinition` 하나. 쓰는 것: `Name`, `Inputs`(`Key`, `Group`), `Compare`(필드 이름), `Thresholds`(`AccuracyMin`, `MissingMax`, `TimeMustDrop`). `Steps`는 읽지 않는다. 대조 결과가 이 정의에서 나온 것이어야 한다(5절).
- `CompareMetrics` 하나(`CompareEngine` 결과, `metrics.md` 3.1). 쓰는 것: `Status`, `Reason`, `Counts`, `AccuracyPct`, `MissingRatePct`, `Items`(상태와 기준 행 `CsvRow`). 오류가 있어 `Metrics`가 null이면 판정하지 않는다(5절).
- 시간 `JudgmentTimes`(선택). 건당 분. 적용 전, 처리, 검토 셋(4.10). 재지 않았으면 null. M2 `measure`가 만든다.
- 사람 입력 "설명 안 되는 검토 항목 키"(선택). 검토 항목의 키 목록(4.9). 사람이 아직 보지 않았으면 null, 보고 모두 설명했으면 빈 목록. M2-4 `review`가 만든다.
- 파일을 읽지 않는다. 판정은 순수 함수다(4.12).
- 같은 `CompareMetrics`가 metrics.json의 원본이다. 판정은 metrics.json 파일을 다시 읽지 않는다(`metrics.md` 9절 8의 답: `CompareResult`를 그대로 받는다). 유형 열 값은 metrics.json에 없고 메모리 결과 `CsvRow`에만 있기 때문이다(`metrics.md` 승인 질문 16).

## 3. 출력

### 3.1 메모리 결과

`JudgmentResult` 하나(4.13). 판정, 근거 키, 판정 문장, 쓴 문턱, 기본값을 쓴 문턱 키, 확인 다섯(미검증이면 없음), 다음 할 일.

### 3.2 judgment.json

Core가 바이트를 만든다(`JudgmentJson.Render`). 모양:

```json
{
  "schema_version": 2,
  "case": "<케이스 이름>",
  "verdict": "pass | conditional | hold | not_verified",
  "rule": "NOT_VERIFIED | ACCURACY | MISSING | UNEXPLAINED | TIME | FOCUS | PASS",
  "reason": "<판정 문장, 4.3. 기본값 표시 포함>",
  "thresholds": {"accuracy_min": 0, "missing_max": 0, "time_must_drop": true},
  "defaults": ["<기본값을 쓴 문턱 키, 4.2 표 순서>"],
  "checks": [{"rule": "ACCURACY", "result": "met", "reason": "<확인 문장, 4.4>"}],
  "todo": [{"key": "<다음 할 일 키, 4.7>", "text": "<문구>"}]
}
```

- 위 블록은 모양 설명이다. 실제 바이트는 7절 블록처럼 배열 원소도 한 줄에 하나다.
- 첫 키는 `schema_version`이다. 값은 정수 `2`(ADR 0004 결정 2). T-012가 확인 결과의 뜻과 다음 할 일 키를 바꿔 1에서 올렸다(11.2). 올리는 기준은 ADR 0008 결정 1이다. 올리는 카드는 expected를 같이 고친다(ADR 0004 expected 변경 규칙).
- 쓰지 않는 것: 절대 경로, 입력 해시, 대조 셈(`counts`. metrics.json에 있다), 시간 값 원본, 사람 입력 원본. 숫자는 확인 문장 안에 글자로 들어간다.

속성 표. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 순서는 파일 안 순서다. `[]`는 배열 원소다.

| 속성 | 형식 | 뜻 |
| --- | --- | --- |
| `schema_version` | 정수 | 파일 모양 버전. 지금 `2`. 늘 첫 키 |
| `case` | 문자열 | `case.yaml`의 `name` |
| `verdict` | 문자열 | `pass`, `conditional`, `hold`, `not_verified` |
| `rule` | 문자열 | 판정 근거 키(4.3) |
| `reason` | 문자열 | 판정 문장(4.3 틀). 기본값 표시(4.6) 포함 |
| `thresholds` | 객체 | 판정에 쓴 문턱. 기본값을 쓴 것도 값으로 적는다 |
| `thresholds.accuracy_min` | 숫자 | `case.yaml` 값 그대로(자릿수 유지) 또는 기본값(4.2) |
| `thresholds.missing_max` | 숫자 | 같음 |
| `thresholds.time_must_drop` | 불리언 | 같음 |
| `defaults` | 문자열 배열 | 기본값을 쓴 문턱 키. 4.2 표 순서. 없으면 `[]` |
| `checks` | 객체 배열 | 확인 다섯(4.4 순서). 미검증이면 `[]` |
| `checks[]` | 객체 | 확인 하나 |
| `checks[].rule` | 문자열 | `ACCURACY`, `MISSING`, `UNEXPLAINED`, `TIME`, `FOCUS` |
| `checks[].result` | 문자열 | 4.4 표의 결과 |
| `checks[].reason` | 문자열 | 확인 문장(4.4 틀). 기본값 표시(4.6) 포함 |
| `todo` | 객체 배열 | 다음 할 일(4.7 순서). 없으면 `[]` |
| `todo[]` | 객체 | 할 일 하나 |
| `todo[].key` | 문자열 | 4.7 표의 키 |
| `todo[].text` | 문자열 | 4.7 표의 문구 |

### 3.3 바이트 규칙

`classification.md` 3.3·`metrics.md` 3.3과 같다: `Utf8JsonWriter`(리플렉션 직렬화 없음), 들여쓰기 2칸, 줄바꿈 `\n`(CR 0개), 끝에 `\n` 하나, UTF-8(BOM 없음), `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`.

- `schema_version`은 `WriteNumber(int)`로 `2`. 문턱 숫자는 `decimal` 그대로 `WriteNumber(decimal)`. 자릿수가 남는다: `85`, `1.0`, `66.6`, `0.0` [T11:P:decimals].
- 문장 안의 숫자도 `decimal`을 `InvariantCulture`로 쓴 글자다. 퍼센트는 대조 결과 값(`metrics.md` 4.8, 늘 소수 한 자리)을 그대로 쓴다. 문턱은 `case.yaml` 글자 그대로다(`85`는 `85`, `90.0`은 `90.0`).
- 이스케이프는 `classification.md` 3.3 표와 같다. 유형 값에 큰따옴표가 있으면 `\"`다 [T11:P:escape]. 이모지(U+1F600)는 `\uD83D\uDE00`(서로게이트 쌍)이다(`classification.md` 3.3 [P:escape]). T-011a에서 고쳤다: 이전 글은 글자 그대로라고 적었다.
- 같은 입력이면 같은 바이트다. 확인: 다른 프로세스, 다른 경로 사본, `LANG=de_DE.UTF-8` 프로세스 [T11:S3 det-cmp]. 문화권 7종(Invariant, de-DE, tr-TR, ar-SA, ko-KR, fr-CH, th-TH)에서 SHA-256이 하나 [T11:S3 culture]. Python 독립 구현이 예제 4종과 G2 표본에서 같은 바이트를 냈다 [T11:S3 xcheck].

### 3.4 명령과 AI용 입구 (ADR 0004 적용)

이 카드는 명령을 만들지 않는다. 명령이 판정을 낼 때는 ADR 0004 결정 3·6과 지시문 3.1을 따른다 [읽음]. 명령 쪽 바이트는 이 스파이크가 만들지 않았다(**미확인**, M2-1·M2-5).

- 내부 명령 `judge`의 `--json` 표준 출력은 그 케이스의 judgment.json과 바이트까지 같다(ADR 0004 결정 6 규칙 5. `classify`·`compare`와 같은 방식). 그래서 7절 expected가 AI용 입구의 정답도 된다.
- 사용자 명령 `check`는 `compare` → `measure` → `judge` → `review` → `report` 순서다 [읽음 ADR 0004 표]. `check --json`의 결과는 스코어카드 결과 객체(M2-3)다. 그 검증 부분은 judgment.json 모양을 쓴다.
- 종료 코드: 판정 넷 모두 0이다(ADR 0004 결정 6 규칙 7: 보류·미검증이어도 0). 대조 오류가 있으면 판정 전에 2로 끝난다(`metrics.md` 3.4). 4.13의 인자 예외(5절)는 부르는 쪽 잘못이라 1이다.

## 4. 규칙

### 4.1 개요

지시문 3.4 원문 [읽음]:

```text
판정(케이스의 `thresholds`로 숫자 조정): 통과 = 정확도 ≥ `accuracy_min` 그리고 누락률 ≤ `missing_max` 그리고 (처리+검토 시간) < 적용 전 시간. 조건부 = 정확도는 충족하나 시간이 줄지 않았거나 특정 유형에만 불일치가 몰림. 보류 = 정확도 미달 또는 설명 안 되는 불일치. 미검증 = 자료 없음(무엇을 재면 되는지 적는다).
```

해석:

1. 대조가 미검증(`metrics.md` 4.4)이면 판정도 미검증이다. 확인을 하지 않는다.
2. 확인 다섯을 늘 모두 계산한다(4.4): 정확도, 누락률, 설명 안 되는 검토 항목, 시간, 몰림.
3. 판정 표(4.3)를 위에서부터 보고 처음 걸리는 행에서 멈춘다. 보류 → 조건부 → 통과 순서다. 보류가 조건부보다 앞이다(조건부는 "정확도는 충족하나"로 시작한다). 사람이 아직 보지 않은 검토 항목은 조건부의 마지막이다(4.3 순서 7).
4. 문턱 비교는 셈(`counts`)으로 한다. 표시 퍼센트로 비교하지 않는다(`metrics.md` 4.8 권고). 1799/2000은 `90.0`으로 보이지만 문턱 90에 못 미친다 [T11:P:count-vs-display].
5. 누락률 초과는 원문에 보류·조건부 어느 쪽에도 없다. 통과의 조건이므로 통과는 아니다. 보류로 둔다(**해석**, 질문 2).
6. 판정 문장·확인 문장·다음 할 일 문구는 Core가 만든다(CLAUDE.md "판정·분류 로직은 Scorecard.Core에만").
7. T-013(ADR 0008 결정 3): 지시문 3.4를 해석 3·4.9에 맞췄다. 조건부에 "또는 사람이 아직 안 본 검토 항목(불일치·추가 행·중복 행)이 있음", 보류의 "설명 안 되는 불일치" 뒤에 "(사람이 설명할 수 없다고 적은 검토 항목)". 위 블록은 T-011 때 읽은 원문 그대로 둔다.

### 4.2 문턱 기본값

`case.yaml`에 `thresholds` 키가 없으면(null) 아래 값을 쓰고 `defaults`에 키를 적는다. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 키 순서가 `defaults` 순서다.

| 키 | 기본값 | 근거 |
| --- | --- | --- |
| `accuracy_min` | `95` | 지시문 12.1 비공개 케이스 예시의 값. 지시문에 있는 유일한 값이다 |
| `missing_max` | `2` | 같음 |
| `time_must_drop` | `true` | 같음. 지시문 3.4 통과 조건에 시간이 들어 있다 |

- 공개 예제 4종 가운데 `thresholds`가 없는 것은 mixed-exceptions뿐이다(`inputs`도 없어 미검증) [읽음]. 나머지 셋은 세 키를 모두 적었다. 그래서 기본값이 예제 판정을 바꾸지 않는다.
- 기본값을 쓴 문턱은 문장 끝에 기본값 표시를 붙이고(4.6), 다음 할 일에 `confirm_thresholds`를 더한다(4.7).
- `case-schema.md` 승인 질문 11의 답이다: 기본값 하나로 12.1(95)과 table-compare(90%에서 조건부)를 맞출 수 없어, 예제는 문턱을 적고 기본값은 12.1 값으로 둔다. → 질문 3.

### 4.3 판정 표

테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 위에서부터 보고 처음 걸리는 행이 판정이다. "확인 결과"는 그 근거 키의 확인 결과(4.4)다. 문장 틀의 `{…}`는 4.5의 값이다.

| 순서 | 근거 키 | 확인 결과 | 판정 | 판정 문장 틀 |
| --- | --- | --- | --- | --- |
| 1 | `NOT_VERIFIED` | `no_inputs` | `not_verified` | `검증 자료(case.yaml inputs)가 없어 미검증.` |
| 1 | `NOT_VERIFIED` | `no_compare_fields` | `not_verified` | `대조할 열(case.yaml compare)이 없어 미검증.` |
| 2 | `ACCURACY` | `not_met` | `hold` | `정확도 {accuracy_pct}%가 문턱 {accuracy_min}%에 못 미치므로 보류.` |
| 3 | `MISSING` | `not_met` | `hold` | `누락률 {missing_rate_pct}%가 문턱 {missing_max}%를 넘으므로 보류.` |
| 4 | `UNEXPLAINED` | `found` | `hold` | `사람이 설명할 수 없다고 적은 검토 항목 키가 {unexplained}개 있으므로 보류.` |
| 5 | `TIME` | `not_measured` | `conditional` | `정확도·누락률은 문턱을 충족하나 시간을 재지 않아 줄었는지 알 수 없으므로 조건부.` |
| 5 | `TIME` | `not_met` | `conditional` | `정확도·누락률은 문턱을 충족하나 처리·검토 시간 {total}분이 적용 전 {before}분보다 짧지 않으므로 조건부.` |
| 6 | `FOCUS` | `found` | `conditional` | `정확도·누락률은 문턱을 충족하나 불일치 {bad}건이 모두 {group} = {value}인 행이므로 조건부.` |
| 7 | `UNEXPLAINED` | `not_reviewed` | `conditional` | `정확도·누락률은 문턱을 충족하나 검토할 항목 {items}건({breakdown})을 사람이 아직 보지 않았으므로 조건부.` |
| 8 | `PASS` | — | `pass` | `정확도·누락률이 문턱을 충족하고 조건부·보류 조건에 걸리지 않으므로 통과.` |

- 1행의 결과는 대조 결과의 `reason`(`metrics.md` 4.4)이다. 미검증이면 확인이 없다(`checks` = `[]`).
- T-011과 다른 곳(T-012): 4행 문장(불일치 → 검토 항목 키), 새 7행, `PASS`가 순서 8.
- 시간(5)이 몰림(6)보다 앞이다. 둘 다 걸리면 시간이다. 확인 다섯은 `checks`에 모두 남으므로 몰림도 보인다.
- `PASS`는 확인 결과로 보면: 정확도·누락률 `met`, 검토 항목 `none`·`explained`, 시간 `off`·`met`, 몰림 `found` 아님.
- 순서 7을 `FOCUS` 뒤에 둔 이유: 재검토 권고다. 몰림·시간이 걸리면 그 문장이 더 구체적이다. table-compare는 `FOCUS`로 남는다(카드 수용 기준 5). 사람이 안 본 항목은 `checks`와 `review_items`에 늘 남는다.
- 보지 않은 항목을 보류로 두지 않는다(4.9 U-A를 고르지 않은 이유와 같다). 자료만 넣은 첫 판정이 늘 보류가 되면 "설명 안 됨"과 "아직 안 봄"이 구별되지 않는다. → 11.19 질문 4.

### 4.4 확인 다섯

테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 확인 순서는 표에서 처음 나오는 순서(`ACCURACY` → `MISSING` → `UNEXPLAINED` → `TIME` → `FOCUS`)다. "걸리는 조건"의 숫자는 대조 셈(`counts`)과 문턱이다.

| 확인 | 결과 | 걸리는 조건 | 확인 문장 틀 |
| --- | --- | --- | --- |
| `ACCURACY` | `met` | `match × 100 ≥ accuracy_min × reference` | `정확도 {accuracy_pct}%({match}/{reference})가 문턱 {accuracy_min}% 이상이다.` |
| `ACCURACY` | `not_met` | 그 밖 | `정확도 {accuracy_pct}%({match}/{reference})가 문턱 {accuracy_min}%에 못 미친다.` |
| `MISSING` | `met` | `missing × 100 ≤ missing_max × reference` | `누락률 {missing_rate_pct}%({missing}/{reference})가 문턱 {missing_max}% 이하다.` |
| `MISSING` | `not_met` | 그 밖 | `누락률 {missing_rate_pct}%({missing}/{reference})가 문턱 {missing_max}%를 넘는다.` |
| `UNEXPLAINED` | `none` | 검토 항목 0건 | `검토할 항목(불일치·추가 행·중복 행)이 없다.` |
| `UNEXPLAINED` | `not_reviewed` | 검토 항목 1건 이상, 사람 입력 없음(null) | `검토할 항목 {items}건({breakdown})이 설명되는지 사람이 아직 보지 않았다.` |
| `UNEXPLAINED` | `explained` | 사람 입력이 빈 목록 | `검토할 항목 {items}건({breakdown})을 사람이 보고 모두 설명했다.` |
| `UNEXPLAINED` | `found` | 사람 입력에 키가 1개 이상 | `검토할 항목 {items}건({breakdown}) 중 키 {unexplained}개를 사람이 설명할 수 없다고 적었다.` |
| `TIME` | `off` | `time_must_drop`이 `false` | `시간 조건을 쓰지 않는다(time_must_drop: false).` |
| `TIME` | `not_measured` | `time_must_drop`이 `true`, 시간 없음(null) | `시간을 재지 않았다.` |
| `TIME` | `met` | `process + review < before` | `처리 {process}분 + 검토 {review}분 = {total}분이 적용 전 {before}분보다 짧다.` |
| `TIME` | `not_met` | 그 밖(같으면 줄지 않았다) | `처리 {process}분 + 검토 {review}분 = {total}분이 적용 전 {before}분보다 짧지 않다.` |
| `FOCUS` | `few` | 불일치가 `focus_min`(4.5)보다 적음 | `불일치가 {bad}건이라 몰림을 보지 않는다.` |
| `FOCUS` | `off` | `inputs.group`을 적지 않음 | `case.yaml inputs.group에 유형 열을 적지 않아 불일치 {bad}건이 한 유형에 몰렸는지 보지 않는다.` |
| `FOCUS` | `one_value` | 그 열이 기준 자료에서 값 하나뿐 | `{group} 열이 기준 자료에서 값이 하나뿐이라 몰림을 볼 수 없다.` |
| `FOCUS` | `found` | 불일치가 모두 그 열의 한 값(4.8) | `불일치 {bad}건이 모두 {group} = {value}인 행이다({value} {rows_in}행 중 {bad}행, 다른 값 {rows_out}행 중 0행).` |
| `FOCUS` | `spread` | 그 밖 | `불일치 {bad}건이 {group} 열의 한 값에 몰리지 않았다.` |

- T-011과 다른 곳(T-012): `UNEXPLAINED` 네 행, `FOCUS` `off`·`one_value`·`spread` 세 행. `{review}`는 4.5처럼 검토 시간(분)이다. 검토 항목 수는 `{items}`로 따로 둔다.
- `FOCUS`의 `{bad}`는 불일치 수다. 추가 행·중복 행은 몰림에 들지 않는다(4.8 규칙 3).
- 셈은 정수와 decimal이다. 나눗셈을 쓰지 않는다. `reference`는 0이 아니다(기준 행이 0이면 대조가 `no-rows` 오류다, `metrics.md` 4.7).
- 문턱 비교는 decimal 반올림 없이 정확히 한다(곱이 29자리를 넘어도). 예: `accuracy_min`의 소수 자릿수가 길어 `accuracy_min × reference`가 유효 숫자 29자리를 넘어도 반올림한 곱으로 비교하지 않는다. 곱 넘침은 5절.
- 시간 합 `process + review`가 decimal 범위를 넘으면 `not_met`이다. 예외가 새지 않는다 [T11:P:time overflow].
- 시간은 `time_must_drop`이 `false`면 받아도 보지 않는다(`off`).

### 4.5 문장 값과 숫자

문장 틀의 값:

- `{accuracy_pct}`·`{missing_rate_pct}`: 대조 결과의 퍼센트(소수 한 자리). `{match}`·`{missing}`·`{reference}`: `counts`. `{accuracy_min}`·`{missing_max}`: 쓴 문턱(4.2). 숫자는 `InvariantCulture`.
- `{bad}`: 불일치 수(0절). `{unexplained}`: 사람이 적은 키 수(4.9). `{items}`: 검토 항목 수. `{breakdown}`: 검토 항목의 내역(4.9 표).
- `{process}`·`{review}`·`{before}`: 받은 시간. `{total}`: `process + review`(decimal 셈, 자릿수는 decimal 덧셈 그대로. `1.25 + 1.2` = `2.45`) [T11:P:decimals].
- `{group}`: `inputs.group`의 열 이름. `{value}`: 몰린 값. `{rows_in}`: 기준 행 가운데 그 값인 행 수. `{rows_out}`: 나머지 기준 행 수. T-011의 `{columns}`는 T-012에서 없앴다.
- 이름·값은 `metrics.md` 4.7 메시지와 같은 방식으로 보인다: 제어 문자는 `\uXXXX`, 64자를 넘으면 자르고 `…`. 빈 값은 `(빈 칸)`.

숫자. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다.

| 이름 | 값 | 뜻 |
| --- | --- | --- |
| `focus_min` | `2` | 몰림을 보는 최소 불일치 수. 1건은 "몰림"이 아니다 |

- 이 숫자는 지시문에 없다. "몰림"·"에만"은 둘 이상을 뜻한다고 보고 가장 작은 값을 골랐다. → 질문 5.

### 4.6 기본값 표시

- 표시 글자는 `classification.md` 4.6과 같다: 공백 하나 + `(기본값 사용, 확인 필요)`. 문장 끝에 한 번.
- 확인 문장: 그 확인이 쓰는 문턱이 기본값일 때. `ACCURACY` → `accuracy_min`, `MISSING` → `missing_max`, `TIME` → `time_must_drop`. `UNEXPLAINED`·`FOCUS`는 문턱을 쓰지 않는다.
- 판정 문장: 그 행에 오기까지 읽은 문턱(아래 표) 가운데 하나라도 기본값일 때. "정확도는 충족하나"로 시작하는 문장은 앞의 판단에 기댄 것이라 앞 문턱도 읽은 것으로 본다(`classification.md` 4.6의 "앞 분기에서 넘어온 답"과 같다).

읽은 문턱. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 판정 표(4.3)의 순서마다 그 행에 오기까지 읽은 문턱이다. 판정 문장은 이 가운데 하나라도 기본값이면 표시를 붙인다(위 규칙).

| 순서 | 근거 키 | 읽은 문턱 |
| --- | --- | --- |
| 1 | `NOT_VERIFIED` | — |
| 2 | `ACCURACY` | `accuracy_min` |
| 3 | `MISSING` | `accuracy_min`, `missing_max` |
| 4 | `UNEXPLAINED` | `accuracy_min`, `missing_max` |
| 5 | `TIME` | `accuracy_min`, `missing_max`, `time_must_drop` |
| 6 | `FOCUS` | `accuracy_min`, `missing_max`, `time_must_drop` |
| 7 | `UNEXPLAINED` | `accuracy_min`, `missing_max`, `time_must_drop` |
| 8 | `PASS` | `accuracy_min`, `missing_max`, `time_must_drop` |

- 순서 7 문장은 "정확도·누락률은 문턱을 충족하나"로 시작하고 시간(순서 5)을 지나 왔다. 그래서 셋 다 읽었다.
- 확인 문장의 표시(그 확인이 쓰는 문턱)는 위 규칙 그대로다.
- 미검증 문장은 문턱을 읽지 않는다. mixed-exceptions는 기본값 셋을 썼지만 판정 문장에 표시가 없다. 대신 `defaults`와 `confirm_thresholds`에 남는다(7.4).

### 4.7 다음 할 일

지시문 3.4 "미검증 = 자료 없음(무엇을 재면 되는지 적는다)", 3.5 6구역 "다음 할 일 — 확보할 기준 자료, 재야 할 시간, 확인할 질문" [읽음]. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. `todo` 순서는 이 표 순서다.

| 다음 할 일 | 나오는 때 | 문구 |
| --- | --- | --- |
| `add_data` | 미검증 `no_inputs` | `기준 자료(사람이 만든 결과)를 reference/에, 결과 자료(AI 적용 후 결과)를 candidate/에 넣고 case.yaml inputs에 파일 패턴과 키 열을 적는다.` |
| `add_compare` | 미검증(둘 다) | `case.yaml compare에 대조할 열과 허용 오차를 적는다.` |
| `measure_time` | `TIME` = `not_measured`, 또는 미검증이고 `time_must_drop`이 `true` | `건당 처리 시간·검토 시간과 적용 전 시간(분)을 잰다.` |
| `review_items` | `UNEXPLAINED` = `not_reviewed` | `검토할 항목 {items}건({breakdown})을 보고 설명이 되는지 적는다.` |
| `add_group` | `FOCUS` = `off` | `불일치가 한 유형에 몰렸는지 보려면 case.yaml inputs.group에 기준 자료의 유형 열 이름을 적는다.` |
| `confirm_thresholds` | 기본값을 쓴 문턱이 있음 | `문턱에 기본값을 썼다({defaults}). 맞는지 확인하고 case.yaml thresholds에 적는다.` |

- 키 이름을 바꾼 이유: `review_mismatches`는 추가 행·중복 행을 담지 못한다. `add_type_column`은 "자료에 열을 더하라"였다. M-A에서는 있는 열 이름을 `case.yaml`에 적는 일이다. 뜻이 바뀐 키를 옛 이름으로 두면 스킬·스코어카드가 옛 뜻으로 읽는다. → 11.19 질문 9.
- `add_group`은 불일치가 `focus_min` 이상일 때만 나온다(`few`가 먼저다). 예제 form-fill은 나오지 않는다.
- `{defaults}`: 기본값을 쓴 키와 값을 4.2 순서로 `, `로 잇는다. 예: `accuracy_min 95, missing_max 2, time_must_drop true`.
- `no_compare_fields` 미검증은 `add_data` 없이 `add_compare`부터다(`inputs`는 있다).
- 통과·할 일 없음이면 `todo`는 `[]`다(form-fill, 7.3).

### 4.8 "특정 유형에만 불일치가 몰림" (M-A)

몰림은 `case.yaml` `inputs.group`에 적은 기준 자료의 열 하나만 본다(M-A, T-012). 예시 표는 6.3이다.

규칙:

1. 불일치가 `focus_min`보다 적으면 `few`. 열을 보지 않는다.
2. `inputs.group`을 적지 않았으면(키 없음·null) `off`.
3. 열은 `inputs.group` 하나다. 값은 기준 행의 그 칸(정규화 뒤, `metrics.md` 4.6)이다. 결과 자료의 값, 추가 행, 중복 행은 보지 않는다.
4. 기준 행에서 그 열의 값이 하나뿐이면 `one_value`.
5. 불일치 행의 값이 모두 같으면 `found`. 아니면 `spread`.

- 열이 있는지는 대조가 본다. 모든 기준 파일의 머리 행에 꼭 한 번 있어야 한다. 없으면 `column`, 둘 이상이면 `duplicate-column` 대조 오류다(`metrics.md` 12.3). 그래서 판정은 늘 열을 찾는다. 결과 자료에는 이 열이 없어도 된다.
- `inputs.group`이 키 열이나 `compare` 필드와 같으면 파서 오류(`conflict`)다(`case-schema.md` 10.2). 키는 행마다 다르고, 대조 열은 결과 값과 섞여 몰림 근거가 흐려진다. T-011의 "유형 열 = 키·대조 열 밖의 열"을 그대로 둔다.
- 우연 몰림: 사람이 고른 열에서도 불일치 2건이 우연히 한 값일 수 있다(예: 40행 중 39행이 같은 값). M-A는 그 열을 사용자가 골랐다는 점만 다르다. 비율 조건(T-011 F3: 불일치 가운데 한 값의 비율)을 더하지 않는다. 지시문에 없는 숫자가 생긴다(**미확인**: 실제 자료에서의 빈도). → 11.19 질문 6.
- 적지 않으면 몰림을 보지 않는다. 그래서 table-compare에서 `group`을 빼면 판정이 `FOCUS`에서 `UNEXPLAINED`(사람이 안 봄)로 바뀐다. 사람이 모두 설명하면 통과가 된다(12절 "조건부"와 다르다). 그래서 예제에 한 줄을 더한다(7.1). → 11.19 질문 8.
- 결과 쪽 중복(T-013, `metrics.md` 4.3): 결과 쪽에 두 번 나온 기준 키는 값 불일치라 그 기준 행이 불일치로 한 번 든다. 그 키의 `duplicate` 항목은 더 들지 않는다(규칙 3). 그래서 이미 불일치인 키를 결과 쪽에 한 번 더 두어도 `{bad}`가 그대로다. 일치하던 키를 한 번 더 두면 그 키가 불일치로 든다 [T13:selftest].
- T-011의 M-B·F1~F3 비교와 M-A 영향 표는 T-013에서 지웠다. 근거는 T-011 카드와 git 기록(`b8f8326` 이 문서 4.8)이다.

### 4.9 "설명 안 되는 불일치"와 검토 항목

검토 항목은 사람이 보고 설명이 되는지 적어야 하는 대조 항목이다. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 표 순서가 `{breakdown}`의 순서다.

| 검토 항목 | 항목 상태 | {breakdown} 이름 |
| --- | --- | --- |
| 불일치 | `mismatch`, `missing` | 불일치 |
| 추가 행 | `extra` | 추가 행 |
| 중복 행 | `duplicate` | 중복 행 |

- 항목 상태는 `metrics.md` 4.3·4.4의 `items[].status`다. 허용오차 안 차이가 있는 일치(`match`)는 검토 항목이 아니다.
- `{items}`: 검토 항목 수. `{breakdown}`: 위 표의 이름마다 수를 세어 0이 아닌 것만 `이름 N건`으로 쓰고 `, `로 잇는다. 예: `불일치 3건, 추가 행 1건`.
- 정확도·누락률은 그대로다. 분모는 기준 행, 일치·누락만 센다(`metrics.md` 4.4). 추가 행·중복 행은 문턱 셈에 들지 않는다. 지어낸 행은 사람 검토로 판정에 닿는다(4.3 순서 4·7). → 11.19 질문 1.
- 사람 입력의 키는 검토 항목의 키다(아래). 한 키에 항목이 둘 이상일 수 있다(결과 쪽에 세 번 나온 키, 추가 키의 중복). 그래서 `{unexplained}`는 키 수다.

- 엔진은 검토 항목이 "설명되는지" 알 수 없다. 설명은 업무 지식이다(예: "불규칙 신호라 사람도 구간이 다르다"). 자료만으로 정하면 엔진이 사용자 대신 판단하게 된다(지시문 13절). 그래서 사람 입력으로 받는다(U-B, T-011에서 고름).

| 안 | 내용 | table-compare | 문제 |
| --- | --- | --- | --- |
| U-A | 사람이 보지 않은 불일치를 "설명 안 됨"으로 본다 | 보류 | 12절 "조건부"와 다르다. 자료만 넣은 첫 판정이 늘 보류가 된다 |
| U-B (추천) | 사람이 적은 키만. 보지 않았으면 `not_reviewed`(보류 아님) | 조건부 | 사람이 보기 전까지는 다음 할 일로만 남는다 |
| U-C | 몰린 유형 밖의 불일치를 "설명 안 됨"으로 본다 | 조건부 | 몰림과 설명을 같은 것으로 센다. signoff-final처럼 흩어지면 모두 설명 안 됨이 된다. 근거가 없다 |

- 사람 입력: 검토 항목(위 표)의 키 목록. null = 아직 보지 않음, 빈 목록 = 보고 모두 설명함, 키가 있음 = 그 키는 설명할 수 없음.
- 키는 검토 항목의 키여야 한다(불일치·추가 행·중복 행). 차이 없는 일치·허용오차 안 차이만 있는 키, 없는 키, 같은 키 두 번은 `ArgumentException`(부르는 쪽 잘못). T-011과 같은 방식이다.
- 추가 행 키를 적을 수 있다(T-012 카드 수용 기준 1). T-011은 `ArgumentException`이었다.
- 키 하나가 항목 여럿을 덮는다(위 글머리). 판정은 키 수를 센다.
- `review`(M2-4) 화면은 검토 항목 셋을 모두 보여야 한다(**미확인**, M2-4). → 11.19 질문 5.
- 예시 표와 `compare-duplicate` 판정 블록은 6.4다.

### 4.10 시간

지시문 3.4: 통과 = … 그리고 (처리+검토 시간) < 적용 전 시간. 조건부 = 정확도는 충족하나 시간이 줄지 않았거나 … [읽음]. 지금 시간 자료는 없다(예제 4종 모두 `time_must_drop: false`, "시간 측정 자료가 없는 예제다" 주석) [읽음].

| 안 | 시간 없음 + `time_must_drop: true` | 통과가 되나 | 원문과 |
| --- | --- | --- | --- |
| T1 (추천) | 조건부(`TIME` = `not_measured`) | 시간을 재야 통과 | 원문 그대로. 시간을 재지 않으면 "줄었다"를 보일 수 없다 |
| T2 | 통과 + 표시("시간 측정 필요") | 된다 | 원문의 통과 조건 셋째를 빼는 것이다 |
| T3 | 판정 전체를 미검증 | 안 된다 | 정확도는 쟀는데 "자료 없음"이라 하게 된다 |

- 추천 T1. 시간 조건을 쓰지 않으려면 사용자가 `time_must_drop: false`를 적는다(예제 4종이 그렇다). 그때 시간은 판정에 쓰지 않는다(`off`).
- 비교: `process + review < before`. 같으면 줄지 않았다(`not_met`). 세 값은 건당 분, decimal, 0 이상(음수면 `ArgumentOutOfRangeException`).
- `measure`(M2)와의 경계: `measure`가 `times.yaml`(`check --times`)을 읽어 `JudgmentTimes`(적용 전, 처리, 검토)를 만든다. 판정은 그 값만 받는다. `times.yaml` 모양, 건당 평균 내는 법, 적용 전 시간을 `answers`의 `minutes_per_item` 합으로 볼지는 M2-5에서 정한다(**미확인**).
- 시간이 생겨도 예제 expected는 그대로다(예제가 `time_must_drop: false`).
- → 질문 4.

### 4.11 결과를 어디에 쓰나

지시문 5절: "`examples/*/expected/`에는 `classification.json`, `metrics.json`, `report-outline.json`이 들어간다. 테스트는 도구 출력과 이 세 파일을 비교한다" [읽음]. ADR 0004 결정 6 규칙 5: 내부 명령의 `--json` = 그 결과 파일 [읽음].

| 안 | 어디 | 기존 expected | 지시문 5절 | 지금 정답을 둘 수 있나 |
| --- | --- | --- | --- | --- |
| O-A | metrics.json에 `judgment` 속성 | 4개 바뀐다(`metrics.json`). `metrics.md` 3.2 속성 표·7절 블록·SHA-256 줄도 | 목록 그대로 | 예 |
| O-B (추천) | 새 파일 `judgment.json` | 그대로(8개 바이트 그대로) | `judgment.json`을 목록에 더한다(문구 한 줄) | 예 |
| O-C | 스코어카드 결과 객체(`report-outline.json`, M2-3)에만 | 그대로 | 그대로 | 아니오. M2-3 전까지 판정 정답이 없다(지시문 2절 11 "기대값을 먼저"와 어긋남) |

- 추천 O-B. 기존 expected 바이트를 바꾸지 않는다(ADR 0005 regression-guard가 막는 "기존 expected 수정"이 없다). `compare`(대조) = metrics.json, `judge`(판정) = judgment.json으로 내부 명령과 파일이 하나씩 맞는다(ADR 0004 규칙 5).
- 판정이 대조와 따로 바뀌어도 metrics.json을 건드리지 않는다. 판정 규칙을 고치는 카드는 judgment.json만 바꾼다.
- expected 영향: 새 파일 `examples/*/expected/judgment.json` 4개 추가. 기존 파일 수정·삭제 없음 [T11:S7 examples]. 새 파일을 넣으면 기존 테스트 `Example_folder_has_exactly_the_planned_files`가 4건 실패한다 [T11:S7a]. 목록에 `expected/judgment.json`을 더하면 통과한다(계획에 새 파일을 더하는 변경. 테스트 수는 그대로 1,284) [T11:S7b].
- 지시문 5절 문구(T-013에서 고쳤다, ADR 0008 결정 2): "`classification.json`(단계별 분류·근거 키), `metrics.json`(정확도·누락률 등), `judgment.json`(판정·근거 문장·다음 할 일), `report-outline.json`(평가서 섹션별 핵심 값)". "이 세 파일"은 "이 파일들"로.
- oracle(ADR 0005, `tools/oracle/`): judgment.json은 이 문서 4절의 표와 글만으로 같은 바이트를 낼 수 있게 썼다. Python 독립 구현(`spikes/T-011/xcheck/judge.py`)이 예제 4종에서 같은 바이트를 냈다 [T11:S3 xcheck]. oracle에 옮길 때는 새 모듈(예: `tools/oracle/judgment.py`)로 더하고, 대조는 oracle의 `compare`를 쓴다. 유형 열(4.8 규칙 1)을 정하려면 기준 파일마다 머리 행이 필요하다. oracle은 prototyper가 스펙만 보고 고친다(ADR 0005 결정 9).
- → 질문 7, 8.

### 4.12 결정론·원본 불변·G1·G2·G3

- `Judge`는 순수 함수다. 난수·시각·환경 변수·현재 폴더·파일을 쓰지 않는다. 비교는 서수, 숫자는 decimal·`InvariantCulture`. `Dictionary`·`HashSet`은 찾기에만 쓰고 출력 순서는 표 순서·열 이름 서수다.
- 입력(`CaseDefinition`, `CompareMetrics`)을 바꾸지 않는다. 판정 전후 metrics.json 바이트가 같다 [T11:P:determinism].
- 기준선(`bf2568e` clone): Release `-warnaserror` 빌드, 전체 테스트 1,282개 통과 [T11:S1].
- G1: 후보 4파일을 Core에 넣고 Release `-warnaserror` 빌드, `dotnet format --verify-no-changes`, 전체 테스트 1,282개(G1 포함)가 통과했다. 테스트 이름 차이 0 [T11:S2]. 쓰는 프레임워크 어셈블리는 `MetricsJson`과 같다(`System.Text.Json`, `System.Text.Encodings.Web`).
- G2·G3 등록(제안):

| 이름 | 샘플(`TestData/guard/…`) | 어댑터 |
| --- | --- | --- |
| `judge-case-valid` | `case-valid` (기존. 바꾸지 않는다) | `CaseParser.ParseDirectory` → `CompareEngine.CompareDirectory` → `JudgmentEngine.Judge`(시간·사람 입력 null) → 출력 폴더의 `judgment.json` 한 파일 |

- 표본 `case-valid`(T-012에서 바꾸지 않았다). 표본 `case-valid`에는 `inputs.group`이 없다. 표본 파일은 바꾸지 않는다.
- 표본 판정(T-012): 보류 `ACCURACY`(66.7% < 90). 확인 다섯 `not_met`, `not_met`, `not_reviewed`(검토 항목 5건: 불일치 4건, 추가 행 1건), `not_measured`, `off`. 다음 할 일 `measure_time`, `review_items`, `add_group` [T12:O].
- 출력 바이트가 바뀐다(SHA-256 `c955e9e236ba726db289aafe0729d880fbc98c988a30c2daf62ea91086412fb1`). G2·G3는 같은 실행 안의 비교라 그대로 통과한다(T-012b에서 확인).
- 숫자가 문장에 들어가므로 문화권(R4)을 본다.
- 결과 [T11:S5]: 전체 테스트 1,284개 통과(1,282 + G2·G3 각 1). 새 샘플이 없다. 기준선 `bf2568e`의 `Real_targets_use_case_samples_not_basic`은 샘플 목록(중복 뺀 것)만 봐서, 고치지 않고 통과했다. 그 뒤 T-010 리뷰(`9ae47a2`)가 이 테스트에 `이름=샘플` 쌍의 정확한 목록 비교를 더했다 [읽음 `CoreGuardTargetTests.cs`]. 그래서 쌍 목록에 `judge-case-valid=case-valid` 한 쌍을 더한다(축소 아님). 샘플 목록 `case-invalid`, `case-valid`, `compare-invalid`는 그대로다.
- 판정 오류 표본은 없다. 판정에는 입력 오류가 없다(대조 오류는 `compare-invalid`가 본다. 인자 예외는 5절).
- 음성 대조 [T11:S6]: (a) 문장 숫자를 `CultureInfo.CurrentCulture`로 쓰면 빌드·G1은 통과하고 G2가 `G2 R4 문화권: out/judgment.json SHA-256 다름`으로 잡는다(`Core_target_is_deterministic(judge-case-valid)` 1건 실패). (b) 판정 안에서 `DateTime.Now`를 읽으면 G1(D1)이 잡는다(`Core_has_no_clock_random_or_environment_references` 1건 실패).

### 4.13 Core 공개 API (제안)

네임스페이스 `Scorecard.Core.Judgment`. 스파이크 후보와 같은 모양이다.

```csharp
public static class JudgmentEngine
{
    public const string VerdictPass = "pass";
    public const string VerdictConditional = "conditional";
    public const string VerdictHold = "hold";
    public const string VerdictNotVerified = "not_verified";
    public const string RuleNotVerified = "NOT_VERIFIED";
    public const string RuleAccuracy = "ACCURACY";
    public const string RuleMissing = "MISSING";
    public const string RuleUnexplained = "UNEXPLAINED";
    public const string RuleTime = "TIME";
    public const string RuleFocus = "FOCUS";
    public const string RulePass = "PASS";

    // 순수 함수. times: 재지 않았으면 null. unexplainedKeys: 사람이 아직 보지 않았으면 null.
    public static JudgmentResult Judge(CaseDefinition definition, CompareMetrics metrics, JudgmentTimes? times = null, IReadOnlyList<string>? unexplainedKeys = null);
}

public static class JudgmentJson
{
    public const string FileName = "judgment.json";
    public const int SchemaVersion = 2;

    // 3.2·3.3 바이트 규칙.
    public static byte[] Render(JudgmentResult result);
}

public sealed record JudgmentTimes(decimal BeforeMinutes, decimal ProcessMinutes, decimal ReviewMinutes);
public sealed record JudgmentThresholds(decimal AccuracyMin, decimal MissingMax, bool TimeMustDrop);
public sealed record JudgmentCheck(string Rule, string Result, string Reason);
public sealed record JudgmentTodo(string Key, string Text);
public sealed record JudgmentResult(
    string Case, string Verdict, string Rule, string Reason, JudgmentThresholds Thresholds,
    IReadOnlyList<string> Defaults, IReadOnlyList<JudgmentCheck> Checks, IReadOnlyList<JudgmentTodo> Todo);
```

- 기본값·결과 문자열·문장 틀·문구·`focus_min`은 `internal` `JudgmentRules.cs` 한 파일에 둔다(`classification.md` 4.9와 같은 방식). 테스트가 이 문서의 표를 읽어 Core 동작과 대조한다. 실행 중에 이 문서를 읽지 않는다.
- Core 파일 4개: `JudgmentModel.cs`, `JudgmentRules.cs`, `JudgmentEngine.cs`, `JudgmentJson.cs`. 값 표시는 기존 `internal` `CompareRules.Show`를 쓴다(같은 어셈블리). T-012가 `CaseInputs.Group`(`case-schema.md` 4.12)과 `CompareEngine.ItemDuplicate`(`metrics.md` 4.11)를 더했다.
- `Judge`는 검토 항목을 `CompareMetrics.Items`의 상태로 센다. 몰림은 `definition.Inputs.Group`과 기준 행 `CsvRow`의 그 칸만 본다.
- → 질문 10.

## 5. 예외

| 상황 | 결과 | 근거 |
| --- | --- | --- |
| `definition`·`metrics`가 null | `ArgumentNullException` | [T11:P:args null] |
| `metrics.Case`가 `definition.Name`과 다름 | `ArgumentException` | [T11:P:args name-mismatch] |
| 시간 값이 음수 | `ArgumentOutOfRangeException` | [T11:P:args negative-time] |
| 사람 입력 키가 검토 항목의 키가 아니거나 중복 | `ArgumentException` | 4.9 |
| 대조 결과가 정의에서 나온 것이 아님(아래 표) | `ArgumentException` | 12.2 |
| 대조 오류(`CompareResult.Metrics`가 null) | 판정하지 않는다. 부르는 쪽이 대조 오류를 보인다(종료 코드 2) | 2절 |
| `inputs` 없음 / `compare` 빔 | 미검증. `checks` = `[]`. 다음 할 일 4.7 | 4.3 |
| `thresholds` 일부만 적음 | 빈 키만 기본값. 그 키만 `defaults`·표시 | 4.2, 4.6 |
| 시간 합이 decimal 범위를 넘음 | `TIME` = `not_met` | [T11:P:time overflow] |
| 시간을 받았는데 `time_must_drop: false` | 시간을 보지 않는다(`off`) | 4.4 |
| 유형 열 값에 큰따옴표·이모지·제어 문자 | 문장에는 `CompareRules.Show` 표시, JSON은 3.3 이스케이프 | [T11:P:escape] |
| 유형 열 값이 빈 칸 | `(빈 칸)`으로 보인다. 빈 칸끼리 같은 값 | 6.3 |
| 불일치는 있는데 추가 행만 다른 유형 | 추가 행은 보지 않는다 | 4.8 규칙 3 |

정의와 대조 결과(T-013). `Judge`는 대조 결과가 이 정의에서 나온 것인지 본다. 아니면 `ArgumentException`(부르는 쪽 잘못, 종료 1)이다. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. "판정"은 예외 없이 판정한다는 뜻이다. "대조함"은 `measured`, "미검증"은 `not_verified`와 그 `reason`이다. 대조 결과는 정의 칸과 같은 모양의 다른 장면에서 만든다.

| 정의 | 대조 결과 | Judge |
| --- | --- | --- |
| `inputs` 없음 | 미검증 `no_inputs` | 판정 |
| `inputs` 없음 | 미검증 `no_compare_fields` | 거절 |
| `inputs` 없음 | 대조함 | 거절 |
| `compare` 빔 | 미검증 `no_compare_fields` | 판정 |
| `compare` 빔 | 미검증 `no_inputs` | 거절 |
| `compare` 빔 | 대조함 | 거절 |
| `inputs`·`compare` 있음 | 대조함(같은 정의) | 판정 |
| `inputs`·`compare` 있음 | 미검증 `no_inputs` | 거절 |
| `inputs`·`compare` 있음 | 대조함, 키 열이 다름 | 거절 |
| `inputs`·`compare` 있음 | 대조함, `compare` 필드가 다름 | 거절 |
| `inputs.group` 있음 | 대조함(같은 정의) | 판정 |
| `inputs.group` 있음 | 대조함, 기준 행 머리 행에 그 열이 없음 | 거절 |
| (이름) | `metrics.Case`가 `definition.Name`과 다름 | 거절 |

- 규칙: `Inputs`가 null이면 미검증 `no_inputs`, `Inputs`가 있고 `Compare`가 비면 미검증 `no_compare_fields`, 그 밖이면 `measured`이고 `metrics.Key` = `Inputs.Key`, `Fields`의 필드 이름(순서) = `Compare`의 필드 이름이어야 한다. `Inputs.Group`이 있으면 모든 기준 행 `CsvRow.Header`에 그 이름이 꼭 한 번 있어야 한다.
- `CompareEngine.CompareDirectory(dir, definition)`의 결과를 같은 `definition`으로 넘기면 늘 맞다. 거절은 부르는 쪽이 다른 정의를 섞었을 때뿐이다.
- 이름 행은 C#만 본다. oracle의 대조 결과에는 케이스 이름이 없다.

- 문턱 곱 넘침(카드 잠정 결정 17): 4.4 조건의 곱이 decimal 범위를 넘어도 예외 없이 부호대로 비교한다. `case.yaml`을 파서로 읽으면 생기지 않는다(문턱 0 이상 100 이하, `case-schema.md` 4.5. 셈은 `int`).

## 6. 예시

### 6.1 문턱 경계

셈으로 비교한다(4.4). 표시 퍼센트가 문턱과 같아 보여도 셈이 다를 수 있다. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 불일치는 누락 외에는 값 불일치다.

| 기준 행 | 일치 | 누락 | 표시 정확도(%) | accuracy_min | ACCURACY | 표시 누락률(%) | missing_max | MISSING |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 30 | 27 | 1 | 90.0 | 85 | `met` | 3.3 | 5 | `met` |
| 30 | 27 | 1 | 90.0 | 90 | `met` | 3.3 | 3.3 | `not_met` |
| 30 | 27 | 1 | 90.0 | 90.1 | `not_met` | 3.3 | 3.4 | `met` |
| 2000 | 1799 | 0 | 90.0 | 90 | `not_met` | 0.0 | 0 | `met` |
| 2000 | 1800 | 0 | 90.0 | 90 | `met` | 0.0 | 0 | `met` |
| 20 | 19 | 1 | 95.0 | 95 | `met` | 5.0 | 5 | `met` |
| 3 | 2 | 0 | 66.7 | 66.7 | `not_met` | 0.0 | 0 | `met` |
| 3 | 2 | 0 | 66.7 | 66.6 | `met` | 0.0 | 0 | `met` |
| 1 | 1 | 0 | 100.0 | 100 | `met` | 0.0 | 0 | `met` |
| 20 | 16 | 1 | 80.0 | 95 | `not_met` | 5.0 | 2 | `not_met` |
| 3000 | 2999 | 1 | 100.0 | 100 | `not_met` | 0.0 | 0 | `not_met` |
| 30 | 30 | 0 | 100.0 | 100 | `met` | 0.0 | 0 | `met` |

- 둘째 행: 누락률이 `3.3`으로 보여 문턱 `3.3`과 같지만 1 × 100 = 100 > 3.3 × 30 = 99라 넘는다.
- 3000행 줄: 2999/3000은 `100.0`, 1/3000은 `0.0`으로 보이지만 둘 다 문턱을 못 맞춘다.

### 6.2 시간

테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 시간은 건당 분이다. `없음`은 시간을 재지 않음(null)이다.

| time_must_drop | 적용 전 | 처리 | 검토 | TIME |
| --- | --- | --- | --- | --- |
| `true` | 10 | 6 | 3 | `met` |
| `true` | 10 | 7 | 3 | `not_met` |
| `true` | 10 | 7.5 | 2.4999 | `met` |
| `true` | 0 | 0 | 0 | `not_met` |
| `true` | 5 | 0 | 0 | `met` |
| `true` | 1.5 | 1 | 0.5 | `not_met` |
| `true` | 12.25 | 10 | 2.24 | `met` |
| `true` | 없음 | 없음 | 없음 | `not_measured` |
| `false` | 10 | 20 | 20 | `off` |
| `false` | 없음 | 없음 | 없음 | `off` |

### 6.3 몰림

몰림(열 하나). 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. `a×5`는 값 `a`인 행 5개, `(빈 칸)`은 빈 값, `(없음)`은 행이 없음이다. 불일치는 값 불일치·누락을 번갈아 둔다. 기준 자료에는 늘 `grp` 열이 있다. "inputs.group"이 `없음`이면 `case.yaml`에 키를 적지 않았다.

| 기준 행 유형 | 불일치 행 유형 | 추가 행 유형 | inputs.group | FOCUS |
| --- | --- | --- | --- | --- |
| `a×5`, `b×5` | (없음) | (없음) | `grp` | `few` |
| `a×5`, `b×5` | `a` | (없음) | `grp` | `few` |
| `a×5`, `b×5` | `a`, `a` | (없음) | 없음 | `off` |
| `a×5`, `b×5` | `a`, `a` | (없음) | `grp` | `found` |
| `a×10` | `a`, `a` | (없음) | `grp` | `one_value` |
| `a×10` | `a`, `a` | (없음) | 없음 | `off` |
| `a×5`, `b×5` | `a`, `b` | (없음) | `grp` | `spread` |
| `a×5`, `b×5` | `a×5` | (없음) | `grp` | `found` |
| `a×9`, `b×1` | `a×9` | (없음) | `grp` | `found` |
| `(빈 칸)×5`, `b×5` | `(빈 칸)`, `(빈 칸)` | (없음) | `grp` | `found` |
| `a×5`, `A×5` | `a`, `A` | (없음) | `grp` | `spread` |
| `a×5`, `b×5` | `a`, `a` | `b`, `b`, `b` | `grp` | `found` |
| `a×5`, `b×5` | `a`, `b` | `a`, `a`, `a` | `grp` | `spread` |
| `(빈 칸)×39`, `x×1` | `(빈 칸)`, `(빈 칸)` | (없음) | 없음 | `off` |
| `(빈 칸)×39`, `x×1` | `(빈 칸)`, `(빈 칸)` | (없음) | `grp` | `found` |

- 끝 두 행이 탐침 P2(6.6)의 모양이다. 적지 않으면 잡히지 않는다. 사용자가 그 열을 직접 고르면 잡힌다.

몰림(열 여럿). 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. `p=aaabbb`는 열 `p`의 행 1~6 값이다(글자 하나가 한 행, `_`는 빈 칸). 머리 행은 `id,<열들>,v`, 키 `id`, `compare`는 `v`다. 불일치 행은 1부터 센다. "걸린 값"은 `found` 문장의 `{group} = {value}`다. `대조 오류`·`파서 오류`는 판정하지 않는다(5절).

| 열 값(행 1~6) | inputs.group | 불일치 행 | FOCUS | 걸린 값 |
| --- | --- | --- | --- | --- |
| `p=aaabbb` | `p` | `1,2` | `found` | `p = a` |
| `p=aaabbb` | `p` | `1,4` | `spread` | — |
| `p=aaaaaa` | `p` | `1,2` | `one_value` | — |
| `q=aaabbb`, `p=ababab` | `p` | `1,2` | `spread` | — |
| `q=aaabbb`, `p=ababab` | `q` | `1,2` | `found` | `q = a` |
| `q=aaabbb`, `p=ababab` | 없음 | `1,2` | `off` | — |
| `p=aaaaaa`, `q=bbbbbb` | `q` | `1,2` | `one_value` | — |
| `b=aabbcc`, `B=xxyyzz` | `b` | `1,2` | `found` | `b = a` |
| `b=aabbcc`, `B=xxyyzz` | `B` | `1,2` | `found` | `B = x` |
| `p=__abab` | `p` | `1,2` | `found` | `p = (빈 칸)` |
| `p=aaabbb` | `q` | `1,2` | 대조 오류 `column` | — |
| `p=aaabbb`, `p=aaabbb` | `p` | `1,2` | 대조 오류 `duplicate-column` | — |
| `p=aaabbb` | `id` | `1,2` | 파서 오류 `conflict` | — |
| `p=aaabbb` | `v` | `1,2` | 파서 오류 `conflict` | — |

- 넷째 행: `q`는 걸릴 모양이지만 적은 열 `p`만 본다. T-011(M-B)과 다른 곳이다.
- 여덟째·아홉째 행: 대소문자가 다른 열 이름은 다른 열이다(서수).

### 6.4 설명 안 되는 검토 항목

설명 안 됨. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. "사람 입력"의 `없음`은 null, `[]`는 빈 목록이다. `불일치 1개`는 불일치 항목의 키 1개, `일치 1개`는 차이 없는 일치 키 1개다. `두 번`은 같은 키를 두 번 적은 것이다. `거절`은 `ArgumentException`이다.

| 불일치 | 추가 행 | 중복 행 | 사람 입력 | UNEXPLAINED |
| --- | --- | --- | --- | --- |
| 0 | 0 | 0 | 없음 | `none` |
| 0 | 0 | 0 | `[]` | `none` |
| 3 | 0 | 0 | 없음 | `not_reviewed` |
| 3 | 0 | 0 | `[]` | `explained` |
| 3 | 0 | 0 | 불일치 1개 | `found` |
| 3 | 0 | 0 | 불일치 3개 | `found` |
| 0 | 2 | 0 | 없음 | `not_reviewed` |
| 0 | 2 | 0 | `[]` | `explained` |
| 0 | 2 | 0 | 추가 행 1개 | `found` |
| 1 | 0 | 1 | 없음 | `not_reviewed` |
| 1 | 0 | 1 | 중복 행 1개 | `found` |
| 2 | 1 | 1 | 없음 | `not_reviewed` |
| 2 | 1 | 1 | 불일치 1개, 추가 행 1개, 중복 행 1개 | `found` |
| 0 | 0 | 0 | 일치 1개 | 거절 |
| 3 | 0 | 0 | 불일치 1개 두 번 | 거절 |

- 중복 행은 차이 없는 기준 키를 결과 쪽에 한 번 더 둔 것이다. 그 키는 결과 쪽에 두 번 나와 값 불일치다(T-013, `metrics.md` 4.3). 그래서 "불일치" 칸은 그 키도 센다. 장면은 불일치 칸에서 중복 행 칸을 뺀 수만큼 보통 불일치(누락·값 불일치를 번갈아)를 두고, 나머지는 중복 행으로 만든다. T-012 표는 이 행들을 `0 | 0 | 1`처럼 적었다.
- 그 키는 불일치이면서 중복 행이라 사람이 적을 수 있다. "중복 행 1개"는 `duplicate` 항목의 키다.

`compare-duplicate`(`metrics.md` 12.5) 판정. T-013 규칙에서도 같은 바이트다(`r2`는 T-012에서도 값 불일치였다). `r2` 두 행의 줄 순서를 바꿔도 같다(`metrics.md` 13.3). 결과 쪽 중복 둘(기준에 있는 키 하나, 추가 키 하나), 추가 행 하나, 불일치 둘. 사람 입력 없음.

`compare-duplicate` judgment.json (schema_version 2, SHA-256 `c7a3d29b9827af25ab1369cf886e06fe9099898aa67d69b5d74c6f507e84be3b`):

```json
{
  "schema_version": 2,
  "case": "compare-duplicate",
  "verdict": "conditional",
  "rule": "UNEXPLAINED",
  "reason": "정확도·누락률은 문턱을 충족하나 검토할 항목 5건(불일치 2건, 추가 행 1건, 중복 행 2건)을 사람이 아직 보지 않았으므로 조건부.",
  "thresholds": {
    "accuracy_min": 50,
    "missing_max": 50,
    "time_must_drop": false
  },
  "defaults": [],
  "checks": [
    {
      "rule": "ACCURACY",
      "result": "met",
      "reason": "정확도 50.0%(2/4)가 문턱 50% 이상이다."
    },
    {
      "rule": "MISSING",
      "result": "met",
      "reason": "누락률 25.0%(1/4)가 문턱 50% 이하다."
    },
    {
      "rule": "UNEXPLAINED",
      "result": "not_reviewed",
      "reason": "검토할 항목 5건(불일치 2건, 추가 행 1건, 중복 행 2건)이 설명되는지 사람이 아직 보지 않았다."
    },
    {
      "rule": "TIME",
      "result": "off",
      "reason": "시간 조건을 쓰지 않는다(time_must_drop: false)."
    },
    {
      "rule": "FOCUS",
      "result": "spread",
      "reason": "불일치 2건이 kind 열의 한 값에 몰리지 않았다."
    }
  ],
  "todo": [
    {
      "key": "review_items",
      "text": "검토할 항목 5건(불일치 2건, 추가 행 1건, 중복 행 2건)을 보고 설명이 되는지 적는다."
    }
  ]
}
```

### 6.5 우선순위

확인 다섯의 결과 조합과 판정. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. "다음 할 일"은 문턱을 모두 적은 장면의 `todo` 키다.

| ACCURACY | MISSING | UNEXPLAINED | TIME | FOCUS | 판정 | 근거 키 | 다음 할 일 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `met` | `met` | `none` | `off` | `few` | `pass` | `PASS` | — |
| `met` | `met` | `none` | `met` | `few` | `pass` | `PASS` | — |
| `met` | `met` | `none` | `not_measured` | `few` | `conditional` | `TIME` | `measure_time` |
| `met` | `met` | `none` | `not_met` | `few` | `conditional` | `TIME` | — |
| `met` | `met` | `not_reviewed` | `off` | `few` | `conditional` | `UNEXPLAINED` | `review_items` |
| `met` | `met` | `not_reviewed` | `off` | `spread` | `conditional` | `UNEXPLAINED` | `review_items` |
| `met` | `met` | `not_reviewed` | `off` | `off` | `conditional` | `UNEXPLAINED` | `review_items`, `add_group` |
| `met` | `met` | `not_reviewed` | `off` | `one_value` | `conditional` | `UNEXPLAINED` | `review_items` |
| `met` | `met` | `not_reviewed` | `off` | `found` | `conditional` | `FOCUS` | `review_items` |
| `met` | `met` | `not_reviewed` | `not_measured` | `found` | `conditional` | `TIME` | `measure_time`, `review_items` |
| `met` | `met` | `explained` | `met` | `found` | `conditional` | `FOCUS` | — |
| `met` | `met` | `explained` | `met` | `spread` | `pass` | `PASS` | — |
| `met` | `met` | `explained` | `off` | `off` | `pass` | `PASS` | `add_group` |
| `met` | `met` | `found` | `off` | `few` | `hold` | `UNEXPLAINED` | — |
| `met` | `met` | `found` | `not_met` | `found` | `hold` | `UNEXPLAINED` | — |
| `met` | `not_met` | `not_reviewed` | `off` | `spread` | `hold` | `MISSING` | `review_items` |
| `met` | `not_met` | `found` | `not_measured` | `found` | `hold` | `MISSING` | `measure_time` |
| `not_met` | `met` | `not_reviewed` | `off` | `found` | `hold` | `ACCURACY` | `review_items` |
| `not_met` | `not_met` | `found` | `not_met` | `found` | `hold` | `ACCURACY` | — |

- `UNEXPLAINED` = `none`(검토 항목 0건)이면 정확도·누락률은 늘 `met`이고 몰림은 `few`다. 그래서 그런 조합은 표에 없다.
- T-011과 다른 곳(T-012): `not_reviewed`이고 몰림·시간이 걸리지 않으면 통과가 아니라 조건부 `UNEXPLAINED`다(다섯째~여덟째 행).
- 몰림 `off`이고 사람이 모두 설명하면 통과다. 대신 `add_group`이 남는다(열세째 행).

### 6.6 탐침 재현

T-012 카드 수용 기준 6. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 장면은 6.3 모양(머리 행 `id,…,v`, 키 `id`, `compare`는 `v` `tolerance: 0`)이고 문턱은 `accuracy_min: 95`, `missing_max: 2`, `time_must_drop: false`다. 기준 키는 `r0000`부터, 추가 키는 `x000`부터다. "T-011"은 T-011 규칙의 결과다. 기록이다. T-013에서 `judge_v1`을 지워 테스트·oracle은 T-012 칸만 본다.

| 탐침 | 장면 | 사람 입력 | T-011 | T-012 |
| --- | --- | --- | --- | --- |
| `P1` | 기준 20행, 결과 = 기준 20행 그대로 + 지어낸 키 30행 | 없음 | `pass` `PASS` | `conditional` `UNEXPLAINED` |
| `P1` | 같음 | `[]` | `pass` `PASS` | `pass` `PASS` |
| `P1` | 같음 | 추가 행 키 전부 | 거절 | `hold` `UNEXPLAINED` |
| `P2` | 기준 40행(열 `memo`: 39행 빈 칸, 1행 `확인`), 빈 칸 행 2개 불일치, `inputs.group` 없음 | 없음 | `conditional` `FOCUS` | `conditional` `UNEXPLAINED` |
| `P2` | 같음 | `[]` | `conditional` `FOCUS` | `pass` `PASS` |
| `P2g` | `P2`에 `inputs.group: memo` | 없음 | 거절 | `conditional` `FOCUS` |
| `P3` | 기준 20행, 결과 = 기준 20행 + 추가 1행(`x000`) | `x000` | 거절 | `hold` `UNEXPLAINED` |
| `P4` | 기준 20행, 결과 = 기준 20행 + `r0003` 한 행 더 | 없음 | 거절 | `conditional` `UNEXPLAINED` |
| `P4` | 같음 | `r0003` | 거절 | `hold` `UNEXPLAINED` |

- `거절`: T-011에서 판정까지 가지 않는다. `P1`·`P3` 사람 입력은 `ArgumentException`(불일치 키가 아님). `P2g`는 T-011 스키마에 `inputs.group`이 없다(`unknown-field`). `P4`는 대조 오류 `duplicate-key`(종료 2).
- `P1` 첫 행이 지적 1의 탐침, `P2` 첫 행이 지적 2의 탐침이다. 둘 다 통과·`FOCUS`가 아니다.
- `P2`에서 사람이 모두 설명하면 통과다. T-011은 우연한 메모 열 때문에 조건부였다.

## 7. 예제 4종 `expected/judgment.json`

### 7.1 만드는 방법과 확정 절차

1. T-011 블록(`schema_version` 1)은 두 구현이 공개 예제 파일(`examples/*/case.yaml`·`reference/`·`candidate/`, 바꾸지 않음)에서 따로 낸 바이트가 같은 것이다.
   - (a) 스파이크 후보(C#, `spikes/T-011/core/Judgment/`) [T11:S3 judge]. 블록은 이것으로 채웠다(`spikes/T-011/blocks.py fill`).
   - (b) Python 독립 구현(`spikes/T-011/xcheck/judge.py`) [T11:S3 xcheck]. 이 문서 4절의 표와 글만 보고 짰다. C# 코드를 쓰지 않는다. 대조 부분은 T-006 Python 독립 구현에서 가져왔다(`metrics.md` 7.1).
   - (a)와 (b)가 4종 모두 바이트까지 같다(`cmp`) [T11:S3 xcheck-cmp].
2. ADR 0005 결정 3에 따라 사람 확정 없이 추천안(이 블록)을 채택한다. 채택 기록은 카드 "잠정 결정"과 `docs/STATUS.md`에 적는다(리드).
3. test-engineer가 블록을 바이트 그대로 `examples/<예제>/expected/judgment.json`으로 옮긴다. Core 출력에서 복사하지 않는다(지시문 2절 11 "기대값을 먼저"). 테스트가 블록·SHA-256 줄·파일이 같은지 본다. 카드 폴더 범위에 `examples/*/expected/`가 있다 [읽음 카드].
4. 커밋 모양: 새 파일 4개만 담은 커밋. 기존 expected를 바꾸지 않으므로 regression-guard의 "기존 파일 수정·삭제"에 걸리지 않는다. ADR 0004 규칙을 따라 메시지는 `expected migration:`으로 시작한다(새 파일이지만 같은 모양으로 남긴다). oracle 잡이 같은 바이트를 재현해야 한다(4.11).
5. `examples/.gitattributes`(`* -text`)가 새 파일에도 걸린다(T-004). 기존 테스트 `Example_text_files_have_no_carriage_return`·`ExamplePatternTests`가 새 파일도 본다 [T11:S7b].
6. 기존 expected 8개(`classification.json`·`metrics.json` × 4)와 예제의 다른 파일은 바이트 그대로다 [T11:S7].
7. T-012: oracle이 아래 표대로 `case.yaml`에 한 줄을 더한 사본에서 낸 바이트가 7.2~7.5 블록이다 [T12:O]. T-012b(#15)의 expected migration 커밋이 블록·SHA-256 줄과 `expected/judgment.json` 4개를 이 바이트로 바꿨다. C# 골든 테스트가 같은 바이트를 낸다.
8. T-013: 7.2~7.5 블록과 expected는 그대로다. oracle `check.py`가 12개 MATCH다 [T13:O].

예제 `case.yaml`. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. "더하는 줄"은 `  key: …` 줄 바로 뒤에 넣는다. 다른 줄은 그대로다.

| 예제 | 더하는 줄 | 지금 SHA-256 | 더한 뒤 SHA-256 | 바이트 |
| --- | --- | --- | --- | --- |
| `table-compare` | `  group: signal_type` | `451a22acd60c6b650aaf86a424acc72fe1884838592b9ca6819dd2491e764ce2` | `e99d669ab16b6eada60e31c794efaa8cd5baa23db34b2500a68b731914f8e5b1` | 1,640 → 1,661 |
| `signoff-final` | `  group: record_kind` | `a12835eae7b913d9b0ea855fa01fb47086fd97411ae50bed48f5d01beadf0263` | `e11e7b68274fa2688027d671ab240be59cbe9ff874be8ffb5214fb8165dfe348` | 2,595 → 2,616 |

- T-011 스파이크 S7c의 SHA-256과 같다 [T11:S7c][T12:O].
- form-fill은 유형 열이 없다(`item`은 대조 열). mixed-exceptions는 `inputs`가 없다. 둘 다 그대로다.
- 한 줄을 더해도 `expected/metrics.json`·`expected/classification.json`은 바이트 그대로다. oracle이 더한 사본에서 같은 바이트를 냈다 [T12:O].

예제 표. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. "검토 항목"은 `{breakdown}`이다.

| 예제 | inputs.group | 판정 | 근거 키 | 확인 다섯 | 다음 할 일 | 검토 항목 |
| --- | --- | --- | --- | --- | --- | --- |
| `table-compare` | `signal_type` | `conditional` | `FOCUS` | `met`, `met`, `not_reviewed`, `off`, `found` | `review_items` | 불일치 3건, 추가 행 1건 |
| `form-fill` | 없음 | `pass` | `PASS` | `met`, `met`, `none`, `off`, `few` | — | — |
| `mixed-exceptions` | 없음 | `not_verified` | `NOT_VERIFIED` | — | `add_data`, `add_compare`, `measure_time`, `confirm_thresholds` | — |
| `signoff-final` | `record_kind` | `hold` | `ACCURACY` | `not_met`, `not_met`, `not_reviewed`, `off`, `spread` | `review_items` | 불일치 4건, 추가 행 1건 |

- 판정·근거 키·확인 결과는 T-011과 같다(T-012 카드 수용 기준 5). T-012에서 바뀐 것은 `schema_version`, 문장, 다음 할 일 키다.
- 12절 대조: table-compare 조건부, form-fill 통과, mixed-exceptions 미검증. 셋 다 같다. signoff-final은 12절이 "판정은 판정 카드에서"라 했고 이 스펙이 보류로 정한다(정확도 미달 → 보류, 지시문 3.4).
- table-compare가 조건부인 근거: 불일치 3건(`log07`, `log08`, `log27`)이 모두 `signal_type = irregular`이고 `normal` 18행에는 없다. `synthetic-examples.md` 4.7이 심은 구조와 같다 [읽음]. `group`을 빼면 `UNEXPLAINED`(사람이 안 봄)가 된다(4.8).
- signoff-final은 몰림이 `spread`다(`unsteady` 3건, `steady` 1건 `rec14`). 정확도 미달이 먼저라 판정에 영향이 없다.
- table-compare 추가 행은 `log31`, signoff-final은 `rec21`이다(`metrics.md` 7.2·7.5).

expected 영향(T-012b에서 바꿨다. 기록):

| 파일 | 지금 SHA-256 | 새 SHA-256 | 바뀐 줄 |
| --- | --- | --- | --- |
| `examples/table-compare/expected/judgment.json` | `1c32a9c92311e43d19a01633a171240b3b4fa215adcafe7f4d470dc45b4f5c57` | `b4032a207bb7459e537cb2d77b975d535190719d7adcc3fe415cbfdce7f54865` | `schema_version`, `UNEXPLAINED` 문장, `todo` 키·문구 |
| `examples/form-fill/expected/judgment.json` | `08cb0cac5df480e2611d2d92dbfca31cc2ebd6f3fcb9430d98d35343b74a0186` | `06fbce369498d930718c692f388c542a5c6c6106bbc183237e5cf2711feff5d3` | `schema_version`, `UNEXPLAINED` 문장 |
| `examples/mixed-exceptions/expected/judgment.json` | `85e5da47de436644228ca4f1d75ac086891d1739a34c93f441bee71afdc9df11` | `12aeacf95061c20c952d62ab9f2e688e52e029bf734ce3bee962406dd752371f` | `schema_version`만 |
| `examples/signoff-final/expected/judgment.json` | `84da5aa2d9aaa6ead84c767235495d96189636987bb78ac24b62ddeda607871b` | `4fab1989397ff6b3e22e31501965dc93dbef0934083e7c67d7dc1994fed39252` | `schema_version`, `UNEXPLAINED`·`FOCUS` 문장, `todo` 키·문구 |

- 그 밖의 expected 8개(`classification.json`·`metrics.json` × 4)는 바뀌지 않는다.
- mixed-exceptions는 `schema_version`만 바뀐다. 한 파일 안의 판이 예제마다 다르면 안 되기 때문이다(T-012 기록 11.15).

### 7.2 table-compare

`examples/table-compare/expected/judgment.json` (SHA-256 `b4032a207bb7459e537cb2d77b975d535190719d7adcc3fe415cbfdce7f54865`):

```json
{
  "schema_version": 2,
  "case": "table-compare",
  "verdict": "conditional",
  "rule": "FOCUS",
  "reason": "정확도·누락률은 문턱을 충족하나 불일치 3건이 모두 signal_type = irregular인 행이므로 조건부.",
  "thresholds": {
    "accuracy_min": 85,
    "missing_max": 5,
    "time_must_drop": false
  },
  "defaults": [],
  "checks": [
    {
      "rule": "ACCURACY",
      "result": "met",
      "reason": "정확도 90.0%(27/30)가 문턱 85% 이상이다."
    },
    {
      "rule": "MISSING",
      "result": "met",
      "reason": "누락률 3.3%(1/30)가 문턱 5% 이하다."
    },
    {
      "rule": "UNEXPLAINED",
      "result": "not_reviewed",
      "reason": "검토할 항목 4건(불일치 3건, 추가 행 1건)이 설명되는지 사람이 아직 보지 않았다."
    },
    {
      "rule": "TIME",
      "result": "off",
      "reason": "시간 조건을 쓰지 않는다(time_must_drop: false)."
    },
    {
      "rule": "FOCUS",
      "result": "found",
      "reason": "불일치 3건이 모두 signal_type = irregular인 행이다(irregular 12행 중 3행, 다른 값 18행 중 0행)."
    }
  ],
  "todo": [
    {
      "key": "review_items",
      "text": "검토할 항목 4건(불일치 3건, 추가 행 1건)을 보고 설명이 되는지 적는다."
    }
  ]
}
```

### 7.3 form-fill

`examples/form-fill/expected/judgment.json` (SHA-256 `06fbce369498d930718c692f388c542a5c6c6106bbc183237e5cf2711feff5d3`):

```json
{
  "schema_version": 2,
  "case": "form-fill",
  "verdict": "pass",
  "rule": "PASS",
  "reason": "정확도·누락률이 문턱을 충족하고 조건부·보류 조건에 걸리지 않으므로 통과.",
  "thresholds": {
    "accuracy_min": 85,
    "missing_max": 5,
    "time_must_drop": false
  },
  "defaults": [],
  "checks": [
    {
      "rule": "ACCURACY",
      "result": "met",
      "reason": "정확도 100.0%(20/20)가 문턱 85% 이상이다."
    },
    {
      "rule": "MISSING",
      "result": "met",
      "reason": "누락률 0.0%(0/20)가 문턱 5% 이하다."
    },
    {
      "rule": "UNEXPLAINED",
      "result": "none",
      "reason": "검토할 항목(불일치·추가 행·중복 행)이 없다."
    },
    {
      "rule": "TIME",
      "result": "off",
      "reason": "시간 조건을 쓰지 않는다(time_must_drop: false)."
    },
    {
      "rule": "FOCUS",
      "result": "few",
      "reason": "불일치가 0건이라 몰림을 보지 않는다."
    }
  ],
  "todo": []
}
```

### 7.4 mixed-exceptions

- `inputs`·`thresholds`가 없다. 문턱 셋이 기본값이다. 판정 문장은 문턱을 읽지 않아 표시가 없고, `defaults`와 `confirm_thresholds`에 남는다. `time_must_drop` 기본값이 `true`라 `measure_time`이 나온다.

`examples/mixed-exceptions/expected/judgment.json` (SHA-256 `12aeacf95061c20c952d62ab9f2e688e52e029bf734ce3bee962406dd752371f`):

```json
{
  "schema_version": 2,
  "case": "mixed-exceptions",
  "verdict": "not_verified",
  "rule": "NOT_VERIFIED",
  "reason": "검증 자료(case.yaml inputs)가 없어 미검증.",
  "thresholds": {
    "accuracy_min": 95,
    "missing_max": 2,
    "time_must_drop": true
  },
  "defaults": [
    "accuracy_min",
    "missing_max",
    "time_must_drop"
  ],
  "checks": [],
  "todo": [
    {
      "key": "add_data",
      "text": "기준 자료(사람이 만든 결과)를 reference/에, 결과 자료(AI 적용 후 결과)를 candidate/에 넣고 case.yaml inputs에 파일 패턴과 키 열을 적는다."
    },
    {
      "key": "add_compare",
      "text": "case.yaml compare에 대조할 열과 허용 오차를 적는다."
    },
    {
      "key": "measure_time",
      "text": "건당 처리 시간·검토 시간과 적용 전 시간(분)을 잰다."
    },
    {
      "key": "confirm_thresholds",
      "text": "문턱에 기본값을 썼다(accuracy_min 95, missing_max 2, time_must_drop true). 맞는지 확인하고 case.yaml thresholds에 적는다."
    }
  ]
}
```

### 7.5 signoff-final

`examples/signoff-final/expected/judgment.json` (SHA-256 `4fab1989397ff6b3e22e31501965dc93dbef0934083e7c67d7dc1994fed39252`):

```json
{
  "schema_version": 2,
  "case": "signoff-final",
  "verdict": "hold",
  "rule": "ACCURACY",
  "reason": "정확도 80.0%가 문턱 95%에 못 미치므로 보류.",
  "thresholds": {
    "accuracy_min": 95,
    "missing_max": 2,
    "time_must_drop": false
  },
  "defaults": [],
  "checks": [
    {
      "rule": "ACCURACY",
      "result": "not_met",
      "reason": "정확도 80.0%(16/20)가 문턱 95%에 못 미친다."
    },
    {
      "rule": "MISSING",
      "result": "not_met",
      "reason": "누락률 5.0%(1/20)가 문턱 2%를 넘는다."
    },
    {
      "rule": "UNEXPLAINED",
      "result": "not_reviewed",
      "reason": "검토할 항목 5건(불일치 4건, 추가 행 1건)이 설명되는지 사람이 아직 보지 않았다."
    },
    {
      "rule": "TIME",
      "result": "off",
      "reason": "시간 조건을 쓰지 않는다(time_must_drop: false)."
    },
    {
      "rule": "FOCUS",
      "result": "spread",
      "reason": "불일치 4건이 record_kind 열의 한 값에 몰리지 않았다."
    }
  ],
  "todo": [
    {
      "key": "review_items",
      "text": "검토할 항목 5건(불일치 4건, 추가 행 1건)을 보고 설명이 되는지 적는다."
    }
  ]
}
```

## 8. 테스트로 옮기는 방법 (test-engineer 참고)

- 폴더 `tests/Scorecard.Tests/Judgment/`, 네임스페이스 `Scorecard.Tests.Judgment`. Trait `Spec` = `judgment`.
- 표 읽기는 기존 `SpecDocument.Table(relativePath, header)`(경로 상수 `docs/spec/judgment.md` 추가). 블록은 `SpecDocument.CodeBlocks`, SHA-256 줄은 `SpecDocument.Line`. 스파이크 S8이 같은 규칙으로 이 문서의 표 12개를 읽었다 [T11:S8].
- 메모리 테스트는 `CompareEngine.Compare`에 바이트를 넘겨 `CompareMetrics`를 만들고 `JudgmentEngine.Judge`를 부른다. 파일을 만들지 않는다(스파이크 `Build` 도우미와 같다: 머리 행 `id,grp,v`, `compare`는 `v` `tolerance: 0`, 값 불일치는 결과 `v`를 바꾸고 누락은 결과 행을 뺀다).

| 테스트 | 확인 | 카드 수용 기준 |
| --- | --- | --- |
| 기본값(4.2) | 머리 행 `\| 키 \| 기본값 \| 근거 \|`. 키가 없으면 그 값, `defaults` 순서. 셋 다 적으면 `defaults` = `[]` | 2 |
| 판정 표(4.3) | 머리 행 `\| 순서 \| 근거 키 \| 확인 결과 \| 판정 \| 판정 문장 틀 \|`. 행마다 그 행에 걸리는 장면의 판정·근거 키·문장 | 2 |
| 확인 표(4.4) | 머리 행 `\| 확인 \| 결과 \| 걸리는 조건 \| 확인 문장 틀 \|`. 행마다 결과·문장. 확인 순서 | 2 |
| 숫자(4.5) | 머리 행 `\| 이름 \| 값 \| 뜻 \|`. `focus_min`−1은 `few`, `focus_min`은 `found` | 2 |
| 읽은 문턱(4.6) | 머리 행 `\| 순서 \| 근거 키 \| 읽은 문턱 \|`. 문턱 하나씩 기본값으로 바꿔 판정 문장·확인 문장 표시. 표시 글자는 정확히 ` (기본값 사용, 확인 필요)` | 2 |
| 다음 할 일(4.7) | 머리 행 `\| 다음 할 일 \| 나오는 때 \| 문구 \|`. 키마다 나오는 장면과 문구, `todo` 순서 | 3 |
| 문턱 경계(6.1) | 머리 행 `\| 기준 행 \| 일치 \| 누락 \| 표시 정확도(%) \| …`. 표시 퍼센트와 확인 결과 | 2 |
| 시간(6.2) | 머리 행 `\| time_must_drop \| 적용 전 \| 처리 \| 검토 \| TIME \|`. 넘침 1건 더 | 3 |
| 몰림(6.3) | 머리 행 `\| 기준 행 유형 \| 불일치 행 유형 \| 추가 행 유형 \| inputs.group \| FOCUS \|`, `\| 열 값(행 1~6) \| inputs.group \| 불일치 행 \| FOCUS \| 걸린 값 \|`. 결과 쪽 중복 행(4.8 끝 글머리, T-013) | 2 |
| 검토 항목(4.9) | 머리 행 `\| 검토 항목 \| 항목 상태 \| {breakdown} 이름 \|` | 2 |
| 설명 안 됨(6.4) | 머리 행 `\| 불일치 \| 추가 행 \| 중복 행 \| 사람 입력 \| UNEXPLAINED \|`. 잘못된 키·중복은 예외. 중복 행 장면은 6.4 글머리(T-013) | 2 |
| 우선순위(6.5) | 머리 행 `\| ACCURACY \| MISSING \| UNEXPLAINED \| TIME \| FOCUS \| 판정 \| 근거 키 \| 다음 할 일 \|`. 장면의 확인 다섯이 표와 같고 판정이 같다 | 2 |
| 탐침(6.6) | 머리 행 `\| 탐침 \| 장면 \| 사람 입력 \| T-011 \| T-012 \|`. T-012 칸만 | T-012 6 |
| JSON(3.2·3.3) | 머리 행 `\| 속성 \| 형식 \| 뜻 \|`. 경로 순서 = 표. 첫 키 `schema_version` = 정수 2. BOM·CR 없음, 끝 `\n`, 이스케이프, decimal 자릿수 | 4 |
| 골든 | `examples/<예제>/` → `CaseParser.ParseDirectory` → `CompareDirectory` → `Judge`(시간·사람 입력 null) → `Render`가 `expected/judgment.json`과 바이트까지 같다. 예제 4종 | 1 |
| 예제 표(7.1) | 머리 행 `\| 예제 \| inputs.group \| 판정 \| 근거 키 \| 확인 다섯 \| 다음 할 일 \| 검토 항목 \|`, `\| 예제 \| 더하는 줄 \| …`, `\| 파일 \| 지금 SHA-256 \| …`. `expected/judgment.json`의 값이 표와 같다 | 1 |
| 스펙 블록 = 예제 파일 | 7.2~7.5 블록, SHA-256 줄, 파일이 같다 | 1, 4 |
| 결정론 | de-DE·tr-TR로 바꿔도 같은 바이트(`try/finally`). 같은 입력 두 번. 입력 metrics.json 바이트 그대로 | — |
| 예외(5절) | 인자 예외 4종. 머리 행 `\| 정의 \| 대조 결과 \| Judge \|`(T-013) 행마다 | T-013 2 |
| G2·G3 | `GuardTargets`에 `judge-case-valid`(4.12). `Real_targets_use_case_samples_not_basic`의 정확한 쌍 목록에 `judge-case-valid=case-valid` 한 쌍을 더한다(축소 아님). 샘플 목록은 그대로 | — |
| 예제 폴더 파일 | `Example_folder_has_exactly_the_planned_files`의 목록에 `expected/judgment.json` 추가 | 1 |

- 표 이름(머리 행)이 이 문서에 정확히 한 번 나와야 한다. `SpecDocument.Table`이 그렇게 검사한다 [읽음]. `| 속성 | 형식 | 뜻 |`와 G2 표 머리 행은 `metrics.md`와 같은 글자다. 문서마다 한 번이라 괜찮다.
- 실패 테스트 먼저: test-engineer 단계에서는 `Scorecard.Core.Judgment`가 없어 새 테스트가 컴파일되지 않는다. T-003 승인 결정 14와 같이 이 상태를 "실패하는 테스트"로 보고, 같은 브랜치에서 core-dev가 구현한다.
- 골든 테스트는 `examples/` 파일이 있어야 돈다. 파일이 없으면 실패해야 한다(빈 통과 금지).
- 기존 테스트는 지우거나 완화하지 않는다. 고치는 것은 둘이다. `Example_folder_has_exactly_the_planned_files` 목록에 `expected/judgment.json` 한 줄(계획에 파일을 더함), `Real_targets_use_case_samples_not_basic`의 정확한 쌍 목록에 `judge-case-valid=case-valid` 한 쌍(대상을 더함). 둘 다 축소가 아니다.

## 9. 미확인 목록

1. GitHub ubuntu·windows 러너에서 판정·골든·G2·G3 테스트가 녹색인지.
2. 실제 업무 자료에서 사람이 고른 `inputs.group` 열이 우연히 몰림을 내는 빈도(4.8).
3. `times.yaml` 모양과 적용 전 시간의 출처(`minutes_per_item` 합인지, 따로 재는지). M2-5(4.10).
4. `review.yaml` 모양과 확인 한 화면에서 "설명 안 됨"을 적는 법. M2-4(4.9).
5. 스코어카드 4구역·6구역이 판정 문장·다음 할 일을 어떤 문구로 보일지. M2-3.
6. publish 단일 exe(`win-x64`)에서 판정 바이트. Windows에서 만든 judgment.json의 SHA-256이 7절과 같은지.
7. 지시문 3.4 원문의 누락률 초과(정확도 충족) 처리. 이 문서는 보류로 해석했다(질문 2).

## 10. 스파이크 단계 목록

`spikes/T-011/run-spike.sh <작업폴더> <로그폴더>`. 커밋된 HEAD를 작업 폴더에 clone하고 `spikes/T-011/`과 이 문서(작업 트리)를 복사해 변형을 넣는다. 저장소의 `src/`, `tests/`, `examples/`, `docs/`는 건드리지 않는다. `summary.txt`는 마지막 실행이다.

| 단계 | 한 일 |
| --- | --- |
| S0 | clone, 스파이크 파일 복사 |
| S1 | 기준선 Release `-warnaserror` 빌드, 전체 테스트 |
| S2 | Core에 판정 후보 4파일 → 빌드 → `dotnet format --verify-no-changes` → 전체 테스트(G1 포함). 예제 파일 무변경 |
| S3 | 스파이크 도구 빌드. 예제 4종·G2 표본 판정. 결정론(다시, 다른 경로, de-DE 프로세스), 문화권 7종. Python 독립 구현(`XCHECK` 줄, 바이트 `cmp`) |
| S4 | 메모리 안 실험 `PROBE`: 인자 예외, 추가 행, 셈과 표시, 시간 넘침, decimal 자릿수, 이스케이프, 결정론·원본 불변, 여러 기준 파일, 중복·빈 이름 열 |
| S5 | `GuardTargets`에 `judge-case-valid` → 빌드 → 형식 → 전체 테스트(G2·G3) |
| S6 | 음성 대조(따로 clone): (a) 문장 숫자를 현재 문화권으로 → G2, (b) `DateTime.Now` → G1 |
| S7 | 블록 → clone `examples/*/expected/judgment.json` → 전체 테스트(a) → 파일 목록 테스트 변형(b). 기존 expected 무변경. S7c: 안 M-A(`inputs.group` + 예제 `case.yaml` 둘)를 따로 clone에 넣고 전체 테스트 |
| S8 | 이 문서의 표 12개를 읽어 후보와 대조. 7절 블록·SHA-256 줄 = S3 엔진 출력 |

---

## 사람 승인 필요 (ADR 0005: 추천안 채택)

ADR 0005 결정 3: T-010 뒤 카드의 승인 질문은 juhan에게 묻지 않고 추천안을 채택한다. 리드가 카드 "잠정 결정"과 `docs/STATUS.md`에 한 줄씩 적는다. 아래는 질문마다 추천안이다. "ADR 0004 기준"은 ADR 0005 전이었다면 누가 정했을지를 기록으로 남긴 것이다(juhan 직접 = expected 추가·변경, 기존 테스트 변경, `case.yaml` 스키마·예제 변경, 지시문 2절 원칙).

1. **판정 표와 우선순위(4.3).** ADR 0004 기준: 리드.
   - 추천: 미검증 → 정확도 미달 보류 → 누락률 초과 보류 → 설명 안 되는 불일치 보류 → 시간 조건부 → 몰림 조건부 → 통과. 처음 걸리는 행. 문턱 비교는 셈. 확인 다섯은 늘 모두 남긴다.
2. **정확도는 충족하나 누락률 초과(4.1 5).** 원문에 보류·조건부 어느 쪽에도 없다. ADR 0004 기준: 리드.
   - 추천: 보류(`MISSING`). 통과 조건을 못 맞추고, 조건부의 "정확도는 충족하나 …"에 누락이 없다. 더 엄한 쪽이다. 예제 판정에 영향이 없다(signoff-final은 정확도 미달이 먼저).
3. **문턱 기본값(4.2).** ADR 0004 기준: 리드(`case-schema` 승인 질문 11의 후속).
   - 추천: 12.1 값 95·2·`true`. 기본값을 쓰면 표시와 `confirm_thresholds`. 예제 판정에 영향이 없다(mixed-exceptions만 기본값이고 미검증).
4. **시간(4.10).** ADR 0004 기준: 리드.
   - 추천: T1(원문 그대로). `time_must_drop: true`이고 시간이 없으면 조건부 `TIME` + `measure_time`. 같으면 줄지 않았다. 시간 입력은 M2 `measure`가 `JudgmentTimes`로 넘긴다. 예제는 `time_must_drop: false`라 영향이 없다.
5. **몰림: 유형 열과 정의(4.8).** ADR 0004 기준: M-A·M-C는 juhan 직접(스키마·예제 변경), M-B는 리드.
   - 추천: M-B + F1. 기준 머리 행의 키·`compare` 밖 열을 이름 서수로 보고, 불일치 2건 이상이 모두 한 값이면 `found`. 스키마·파서·예제·기존 테스트를 바꾸지 않는다. 우연한 열로 걸리면 통과가 조건부로 내려간다(엄한 쪽). 자주 걸리면 별도 카드로 M-C(선택 키 `inputs.group`, 예제에는 적지 않음).
6. **설명 안 되는 불일치(4.9).** ADR 0004 기준: 리드.
   - 추천: U-B. 엔진은 알 수 없다. 사람이 `review`(M2-4)에서 적은 키 목록만 받는다. 아직 안 봤으면 `not_reviewed`(보류 아님) + `review_mismatches`.
7. **결과를 담는 곳(4.11).** ADR 0004 기준: 새 expected라 juhan 직접. 지시문 5절 목록 문구도.
   - 추천: O-B. 새 `judgment.json`, 내부 명령 `judge --json` = 그 파일. 기존 expected 8개 바이트 그대로. 리드가 지시문 5절 목록에 `judgment.json`을 더한다(원칙 변경이 아니라 목록 문구).
8. **예제 4종 `expected/judgment.json`(7절).** ADR 0004 기준: juhan 직접(expected 추가, 기존 테스트 목록 변경, 폴더 범위).
   - 추천: 7.2~7.5 블록을 정답으로 채택. test-engineer가 블록을 바이트 그대로 옮긴다. `Example_folder_has_exactly_the_planned_files` 목록에 `expected/judgment.json` 한 줄. 새 파일만 담은 `expected migration:` 커밋. oracle에 판정을 더한다(prototyper, `tools/oracle/`).
9. **signoff-final 판정.** 12절이 판정 카드로 넘겼다. ADR 0004 기준: juhan 직접(expected 값).
   - 추천: 보류 `ACCURACY`(80.0% < 95%). 누락률 5.0% > 2%도 `not_met`으로 남는다.
10. **Core 공개 API와 진행 순서(4.13).** ADR 0004 기준: 리드.
    - 추천: 4.13 그대로. 규칙은 `internal` `JudgmentRules.cs` 한 파일. 기존 Core 파일은 바꾸지 않는다. test-engineer 단계의 컴파일 실패를 "실패하는 테스트"로 본다(T-003 승인 결정 14).
11. **G2·G3 등록(4.12).** ADR 0004 기준: 리드(기존 테스트를 지우거나 완화하지 않는다).
    - 추천: `judge-case-valid`(샘플 `case-valid`). 새 샘플이 없어 샘플 목록은 그대로다. `Real_targets_use_case_samples_not_basic`의 정확한 쌍 목록에 `judge-case-valid=case-valid` 한 쌍을 더한다(축소 아님). 판정 오류 표본은 두지 않는다.
12. **문장 틀과 다음 할 일 문구(4.3·4.4·4.7).** ADR 0004 기준: 리드.
    - 추천: 표 그대로. 미검증의 "무엇을 재면 되는지"는 `add_data`·`add_compare`·`measure_time` 문구로 적는다. 문구를 바꾸면 이 표와 expected를 같이 고치고 oracle이 같은 바이트를 내야 한다.

## 채택

- ADR 0005 결정 3에 따라 리드가 1~12의 추천안을 채택하고 카드 "잠정 결정"과 `docs/STATUS.md`에 적는다. 이 문서는 추천안만 적는다.
- 위 1~12는 T-011 기록이다. T-012가 1(순서 7 `UNEXPLAINED`), 5(M-B → M-A), 6(`not_reviewed`는 조건부), 12(문구 일부)를 바꿨다(11절). T-013이 5절 예외를 더했다(12절).

---

## 11. T-012 개정 (판정 보강)

- 상태: 채택(ADR 0005 결정 3, STATUS 잠정 결정 T-012). 2026-11-07, prototyper 초안. 질문은 11.19, 채택 기록은 11.20. T-013에서 이 절의 표·글을 1~7절로 옮겼다(11.3). 이 절은 T-012가 왜·무엇을 바꿨는지의 기록이다.
- 작업 카드: `docs/tasks/T-012-judgment-hardening.md`. 출발점: M1 [재검토] 막는 지적 1·2(`docs/STATUS.md` 재검토 기록) [읽음]. `metrics.md` 4.4 "추가 행을 판정에 쓸지는 판정 카드가 정한다" [읽음]. 이 문서 4.8 M-A 행과 M-B 위험 글머리 [읽음].
- 확인 대상 커밋: `5f0df03`(main `0f9e810` + 재검토 기록·카드). `src/`·`tests/`·`examples/`는 `0f9e810`과 같다.
- 근거: oracle(`tools/oracle/`, 이 절과 `metrics.md` 12절·`case-schema.md` 10절의 글만 보고 고쳤다. `src/`는 읽지 않았다, ADR 0005 결정 9). 이 절의 블록·SHA-256·문장은 oracle이 실제로 낸 것이다. C# 엔진의 출력은 아직 없다(**미확인**, PR (b)). 스파이크 폴더는 두지 않았다.
- 표기: `[T12:O]`는 oracle 출력, `[T12:selftest]`는 `python3 -I -B tools/oracle/selftest.py --repo .` 점검 줄이다.
- 고르는 기준(카드 메모): 사용자에게 맞나 → 그다음 바이트 보존.

### 11.1 왜 바꾸나

- 지적 1: 결과 자료(AI 결과)의 대표 실패는 지어낸 행(추가 행)과 키 중복이다. T-011에서 추가 행은 판정에 쓰이지 않았다. 결과 쪽 키 중복은 대조 오류(종료 2)라 판정까지 가지 않았다. 사람이 아직 보지 않은 불일치도 통과를 막지 않았다(4.9 U-B).
  - 탐침: 기준 20행에 지어낸 30행을 더한 결과가 통과였다(11.11 P1).
- 지적 2: M-B(4.8)는 키·대조 열 밖의 열을 모두 본다. 메모 열처럼 우연히 값이 같은 열로 몰림이 걸린다.
  - 탐침: 40행 중 39행이 빈 메모 열에서 불일치 2건이 "몰림"으로 잡혀 조건부가 됐다(11.11 P2).
- 지시문 3.4 "보류 = 정확도 미달 또는 설명 안 되는 불일치" [읽음]. 이 절은 "설명"이 필요한 것을 불일치에서 검토 항목(11.3)으로 넓힌다.

### 11.2 바뀌는 것과 그대로인 것

바뀌는 것:

- 검토 항목(11.3): 불일치 + 추가 행 + 결과 쪽 중복 행. `UNEXPLAINED` 확인과 사람 입력이 이것을 본다.
- 결과 쪽 키 중복: 대조 오류가 아니라 항목 `duplicate`다(`metrics.md` 12.2). 기준 쪽 중복은 지금처럼 오류다.
- 판정 표(11.4): 사람이 아직 보지 않은 검토 항목이 있으면 조건부 `UNEXPLAINED`(순서 7, `FOCUS` 뒤). 통과는 순서 8.
- 몰림(11.8): M-A. `case.yaml` `inputs.group`(새 선택 키, `case-schema.md` 10절)에 적은 열 하나만 본다. 적지 않으면 몰림을 보지 않는다(`off`).
- 문장·다음 할 일(11.5~11.7): `UNEXPLAINED` 문장 넷, `FOCUS` `off`·`one_value`·`spread` 문장, 판정 문장 2행. 다음 할 일 키 `review_mismatches` → `review_items`, `add_type_column` → `add_group`.
- 읽은 문턱 표(11.6): `UNEXPLAINED`가 순서 4(보류)와 7(조건부) 두 곳에 나와 근거 키 대신 순서로 적는다. 값은 T-011과 같은 원칙이다.
- judgment.json `schema_version` 1 → 2(11.12). 확인 결과의 뜻(`UNEXPLAINED`, `FOCUS` `off`·`one_value`)과 다음 할 일 키가 바뀌기 때문이다(ADR 0004 결정 2 "이름·뜻을 바꾸는 카드가 값을 올린다").
- 예제 `case.yaml` 2개에 한 줄: table-compare `group: signal_type`, signoff-final `group: record_kind`(11.12).

그대로인 것:

- 판정 값 넷과 예제 4종의 판정(조건부·통과·미검증·보류)과 근거 키. 카드 수용 기준 5.
- 문턱 기본값(4.2), `focus_min`(4.5), 기본값 표시 규칙(4.6), 시간(4.10), 미검증 규칙과 문장.
- 정확도·누락률의 셈과 분모(기준 행). 추가 행·중복 행은 분모에 넣지 않는다(11.3).
- metrics.json 모양과 `schema_version` 1(`metrics.md` 12.4). 예제 4종 `expected/metrics.json`·`expected/classification.json` 바이트 [T12:O].
- 오류 코드 수: 대조 13개, 파서 17개.
- 3.3 바이트 규칙, 4.11(결과 파일 judgment.json), 4.12 G2·G3 등록 이름(`judge-case-valid`).

### 11.3 옮긴 곳 (T-013)

T-012 PR (a)는 새 표를 이 절에만 두었다(11.19 질문 12). T-013이 제자리로 옮기고 T-011 표를 지웠다. 표 머리 행은 그대로다.

| T-012 절 | 지금 자리 |
| --- | --- |
| 11.3 검토 항목 | 4.9 |
| 11.4 판정 표 | 4.3 |
| 11.5 확인 표 | 4.4 |
| 11.6 문장 값과 읽은 문턱 | 4.5, 4.6 |
| 11.7 다음 할 일 | 4.7 |
| 11.8 몰림 규칙 / 표 둘 | 4.8 / 6.3 |
| 11.9 설명 안 되는 검토 항목 / 표·`compare-duplicate` 블록 | 4.9 / 6.4 |
| 11.10 우선순위 | 6.5 |
| 11.11 탐침 재현 | 6.6 |
| 11.12 예제 표 셋 | 7.1 |
| 11.13 예제 블록(schema_version 2) | 7.2~7.5(같은 바이트라 지웠다) |
| 11.14 G2 표본 | 4.12 |
| 11.15 oracle 판 둘 | 지웠다. T-013에서 `judge_v1`과 판 고르기를 없앴다(12.4, ADR 0007 결정 4, ADR 0008) |
| 11.16 Core 바뀌는 곳 | 4.13 |
| 11.17 PR (b) 할 일 | 끝났다(T-012b #15). 남은 옮김은 T-013이 했다 |

### 11.18 미확인

1. C# 엔진이 11.13 블록과 같은 바이트를 내는지. PR (b) 골든 테스트.
2. 사람이 고른 `inputs.group` 열에서 우연 몰림의 빈도(11.8).
3. `review`(M2-4)가 추가 행·중복 행을 어떻게 보이고 키를 받는지.
4. 결과 쪽 빈 키 행. 지금처럼 `empty-key` 대조 오류(종료 2)로 둔다(`metrics.md` 12.2). AI 결과의 결함이지만 키가 없어 사람이 키로 적을 수 없다. 뒤 카드에서 다시 본다.
5. 스코어카드(M2-3)가 `{breakdown}`을 어떻게 보일지.

### 11.19 승인 질문 (ADR 0005: 추천안 채택)

추천안은 "사용자에게 맞나 → 그다음 바이트 보존" 순서로 골랐다(카드 메모). "ADR 0004 기준"은 ADR 0005 전이었다면 누가 정했을지다.

1. **추가 행·중복 행을 어디에 쓰나(11.3).** ADR 0004 기준: 리드.
   - 추천: 검토 항목에 넣는다. 정확도·누락률의 분모·셈은 그대로다. 지어낸 행은 사람 검토(`not_reviewed` 조건부, `found` 보류)로 판정에 닿는다.
   - 대안(추천하지 않음): 추가 행률 문턱 `extra_max`. 지시문에 없는 숫자와 스키마 키가 생기고, 정당한 추가 행(범위가 넓은 결과)도 기계적으로 보류된다.
2. **결과 쪽 키 중복(`metrics.md` 12.2).** ADR 0004 기준: 리드.
   - 추천: 대조 오류가 아니라 항목 `duplicate`. 같은 키의 첫 행으로 대조하고 둘째부터 `duplicate`. 기준 쪽 중복과 빈 키(양쪽)는 지금처럼 오류(종료 2).
3. **metrics.json 모양(`metrics.md` 12.4).** ADR 0004 기준: juhan 직접(expected 모양).
   - 추천: 새 속성 없음, `schema_version` 1 그대로. `items[].status`에 값 `duplicate`가 늘고 `counts.candidate`가 중복 행을 센다. 수는 `items`에서 센다. 예제 4종 metrics.json 바이트 그대로.
4. **사람이 안 본 검토 항목(11.4).** ADR 0004 기준: 리드.
   - 추천: 조건부 `UNEXPLAINED`, 순서 7(`FOCUS` 뒤). 보류로 두지 않는다. 예제 판정 그대로.
5. **사람 입력의 키(11.9).** ADR 0004 기준: 리드.
   - 추천: 검토 항목(불일치·추가 행·중복 행)의 키. 밖의 키·중복은 `ArgumentException`. 문장은 키 수(`키 N개`).
6. **몰림 열(11.8).** ADR 0004 기준: juhan 직접(스키마 변경).
   - 추천: M-A. `inputs.group` 열 하나. 적지 않으면 `off` + `add_group`. 기준 행만 본다. 비율 조건은 더하지 않는다.
7. **`inputs.group` 검사(`case-schema.md` 10.2, `metrics.md` 12.3).** ADR 0004 기준: 리드.
   - 추천: 파서는 빈 값 `required`, 스칼라 아님 `type`, 키·`compare` 필드와 같으면 `conflict`. 대조는 기준 파일마다 `column`·`duplicate-column`(키 → `compare` → group 순서). 결과 자료는 보지 않는다. 오류 코드 수 그대로.
8. **예제 `case.yaml`(11.12).** ADR 0004 기준: juhan 직접(예제 변경).
   - 추천: table-compare `group: signal_type`, signoff-final `group: record_kind` 한 줄씩. form-fill·mixed-exceptions 그대로. 12절 "조건부"를 지키려면 필요하다.
9. **문장과 다음 할 일 키(11.4~11.7).** ADR 0004 기준: 리드.
   - 추천: 표 그대로. `review_mismatches` → `review_items`, `add_type_column` → `add_group`. `{columns}` 없앰, `{items}`·`{breakdown}` 새 값.
10. **judgment.json `schema_version`(11.2, 11.12).** ADR 0004 기준: juhan 직접(expected 변경).
    - 추천: 2로 올린다. 확인 결과의 뜻과 할 일 키가 바뀐다. expected judgment.json 4개 migration. 그 밖의 expected 8개는 그대로.
11. **oracle 판 고르기(11.15).** ADR 0004 기준: 리드(CI 도구, 새 ADR).
    - 추천: `check.py`가 expected judgment.json의 `schema_version`으로 고른다(1 → `judge_v1` 동결본, 그 밖 → `judge`). 섞이면 DIFF. `judge_v1`은 group·결과 쪽 중복을 거절. PR (b) 뒤 첫 oracle 변경에서 지운다. 새 ADR + STATUS 원칙 변경 행(리드).
12. **문서 구조와 PR (b) 순서(11.17).** ADR 0004 기준: 리드.
    - 추천: PR (a)는 옛 표·블록을 두고 이 절에 새 머리 행의 표를 둔다(PR (a)의 C# 테스트가 옛 표를 읽는다). PR (b)가 옮기고 테스트를 새 머리 행으로 바꾼다. 블록·SHA-256 바꿈은 migration 커밋에 넣는다.

### 11.20 채택

- ADR 0005 결정 3에 따라 리드가 추천안을 채택하고 카드와 `docs/STATUS.md`에 적는다. 이 절은 추천안만 적는다.

## 12. T-013 개정 (정리와 남은 지적)

- 상태: 초안(2026-11-07, prototyper). ADR 0005 결정 3에 따라 리드가 12.6 추천안을 채택하고 카드와 `docs/STATUS.md`에 적는다.
- 작업 카드: `docs/tasks/T-013-cleanup.md` 수용 기준 1~5·7. 출발점: T-012 카드 "PR (a) 리뷰 지적"·"PR (b) 리뷰 지적", T-011 카드 "T-011a 리뷰의 막지 않는 지적", ADR 0007 결정 4 [읽음].
- 확인 대상 커밋: `be5171b`(main `b8f8326` + STATUS·카드). `src/`·`tests/`·`examples/`는 `b8f8326`과 같다.
- 근거: oracle(`tools/oracle/`, 이 절·5절·`metrics.md` 4.3·13절 글만 보고 고쳤다. `src/`는 읽지 않았다, ADR 0005 결정 9). `[T13:O]`는 oracle 출력, `[T13:selftest]`는 `python3 -I -B tools/oracle/selftest.py --repo .` 점검 줄이다. `[T13:dotnet]`은 이 작업 트리 사본(`src/`는 main 그대로)에서 돌린 `dotnet test -c Release`다.
- 고르는 기준(카드 메모): 사용자에게 맞나 → 그다음 바이트 보존.

### 12.1 바뀌는 것과 그대로인 것

바뀌는 것(동작은 둘뿐):

- 결과 쪽 키 중복의 셈(`metrics.md` 4.3·13절). 결과 쪽에 두 번 나온 기준 키는 값 불일치다. 중복 항목에 기준과의 차이를 적는다. 셈이 줄 순서와 상관없다. 카드 수용 기준 1.
- 정의와 대조 결과가 어긋나면 `ArgumentException`(5절 표, 12.2). 카드 수용 기준 2.
- 문서: 11절 표·글을 4·6·7절로 옮기고 T-011 표·블록을 지웠다(11.3). `metrics.md` 12.7·`case-schema.md` 10.5의 옮김도 했다. 카드 수용 기준 4.
- 6.4 표의 중복 행 세 행: "불일치" 칸이 그 키를 센다(`0 | 0 | 1` → `1 | 0 | 1`). 결과 쪽 중복 규칙이 바뀌어서다.
- oracle: `judge_v1.py`와 `check.py`의 판 고르기를 지웠다(12.4). 카드 수용 기준 3.
- 지시문 5절 expected 목록에 `judgment.json`, 3.4 조건부에 "아직 안 본 검토 항목"(ADR 0008 결정 2·3). 카드 수용 기준 5.

그대로인 것:

- 예제 4종 expected 12개 바이트(`classification.json`·`metrics.json`·`judgment.json`). oracle `check.py` 12개 MATCH [T13:O]. PR을 나누지 않아도 된다(카드 메모, `oracle-and-expected`).
- judgment.json·metrics.json `schema_version`(2·1), 판정 표·확인 표·다음 할 일·문장, `compare-duplicate` 판정 블록(6.4, `c7a3d29b…`), G2 표본 바이트(4.12, `c955e9e2…`).
- 테스트가 읽는 표 머리 행·블록 머리 줄·SHA-256 줄 머리. 자리만 옮겼고 문서마다 한 번씩이다(12.5).

### 12.2 정의와 대조 결과 (5절)

- T-012b 리뷰 지적 3: 정의에 `Inputs`가 없는데 measured 결과를 넘기면 몰림이 `off`로 판정됐다. 케이스 이름이 다르면 이미 `ArgumentException`이었다.
- 추천: 5절 표대로 거절한다. 대조 결과가 그 정의에서 나온 것이 아니면 판정 문장이 다른 케이스를 말하게 된다. 부르는 쪽 잘못이라 종료 1이다(3.4).
- 보는 것: 미검증 이유가 정의와 맞는지, measured면 키 열·`compare` 필드 이름(순서)이 같은지, `inputs.group`이 있으면 기준 행 머리 행에 그 열이 꼭 한 번 있는지. 문턱(`thresholds`)은 대조가 쓰지 않으므로 보지 않는다. 같은 대조 결과로 문턱만 바꿔 판정하는 것은 된다.
- 예제·G2 표본·`compare-duplicate`는 `CompareDirectory`와 같은 정의라 걸리지 않는다. 바이트가 그대로다.
- oracle은 같은 검사를 한다(`judge.check_consistent`). 이름 행은 oracle 대조 결과에 이름이 없어 C#만 본다.

### 12.3 문서 이동 (11.3)

- 머리 행·블록 머리 줄은 바꾸지 않았다. 그래서 C# 테스트·selftest가 읽는 곳은 자리만 바뀐다.
- 지운 것: T-011 표(4.3·4.4 옛 판정·확인 표, 4.6 근거 키 읽은 문턱, 4.7 옛 다음 할 일, 4.8 예제 열·M-B·F1~F3·M-A 영향, 6.3 옛 두 표, 6.4·6.5 옛 표, 7.1 옛 예제 표), 11.13 블록 넷(7.2~7.5와 같은 바이트), 각 절의 "T-012 개정(제안)" 글머리.
- 남긴 것: 4.9 U-A~U-C 표(고른 근거), 11.1·11.2·11.18~11.20(T-012 기록), "사람 승인 필요"(T-011 기록, 끝에 T-012가 바꾼 것 한 줄).

### 12.4 oracle

- `tools/oracle/judge_v1.py`를 지웠다. `check.py`는 judgment.json을 `judge.py` 하나로 만든다. 옛 판(`schema_version` 1) expected는 그 파일만 DIFF다 [T13:selftest].
- `compare.py`: `metrics.md` 13.2. `judge.py`: `check_consistent`(5절 표).
- `selftest.py`(통과 953, 실패 0 [T13:selftest]): T-011 판 점검을 지웠다. 판과 상관없던 점검(3.2 표 밖의 속성·형식, 3.3 문턱 자릿수, `TIME` `not_met` 문장, 시간 음수·합 넘침 거절, 문턱 85 → 95 DIFF, judgment.json을 모두 지우면 대상 아님)은 남겼다. 예제 이름은 `examples/` 폴더에서 읽는다. 7.1 예제 표 이름 = 폴더 이름이고 폴더마다 7절 블록이 하나 있어야 한다. 새 점검: `metrics.md` 13.3 표, 5절 표, 4.8 끝 글머리(중복 행과 몰림).
- base oracle(main `b8f8326`)도 이 트리에서 12개 MATCH다. 예제·expected가 그대로라서다 [T13:O]. 그래서 `oracle`·`oracle-head` 잡 모두 녹색이어야 한다.
- 6.6 탐침 표의 T-011 칸은 기록이다. selftest·C# 테스트는 T-012 칸만 본다(T-012 PR (b) 테스트와 같다).

### 12.5 C# 영향

core-dev(`src/Scorecard.Core/`):

1. `CompareEngine`: `metrics.md` 13.2(결과 쪽 같은 키가 있는 기준 항목은 `mismatch`, `duplicate` 항목의 `Differences`, `FieldSummary.Compared`와 칸 셈).
2. `JudgmentEngine.Judge`: 5절 표의 검사(`ArgumentException`).

test-engineer(`tests/`, 바꾸는 기존 테스트는 PR 본문에 이유):

1. `metrics.md` 13.5의 `DuplicateTests` 넷(옛 규칙 고정)과 새 13.3 표 테스트, 줄 순서 뒤집기 테스트.
2. 6.4 표를 읽는 테스트(`UnexplainedTests`): 중복 행 장면은 "불일치 칸 − 중복 행 칸"만큼 보통 불일치를 두고 나머지를 중복 행으로 만든다(6.4 글머리). 머리 행은 그대로다. `Unexplained_table_covers_all_results`의 "불일치 0·추가 행 0·중복 행 ≠ 0·`found`" 행 단언은 이제 맞는 행이 없다(기준 키의 중복 행은 그 키를 불일치로 센다). "중복 행 ≠ 0이고 사람 입력이 중복 행 키뿐인 `found` 행"으로 바꾼다(6.4 `1 | 0 | 1 | 중복 행 1개`). 뜻(중복 행만 적어도 `found`)은 같다. PR 본문에 적는다.
3. 5절 정의·대조 결과 표(머리 행 `| 정의 | 대조 결과 | Judge |`) 행마다. 이름 행 포함.
4. 4.8 끝 글머리 장면 둘(이미 불일치인 키의 중복 행은 `{bad}`가 그대로, 일치 키의 중복 행은 불일치로 든다). T-013 카드 수용 기준 6의 앞 장면이다.
5. 표 이동만으로 깨지는 테스트는 없다(머리 행·머리 줄 그대로). 이 트리 사본 + main Core로 `dotnet test -c Release`: 1637개 중 통과 1635, 실패 2 [T13:dotnet]. `DuplicateTests.Compare_duplicate_sample_matches_spec_block`(블록 `compared` 4, Core 3: core-dev 1이 고친다), `UnexplainedTests.Unexplained_table_covers_all_results`(위 2). 그 밖은 통과한다.
6. `JudgmentSpec.ReviewHeading`(`### 11.9 설명 안 되는 검토 항목`)은 이제 문서에 없다. 쓰는 테스트가 없어 깨지지 않는다. 지우거나 `### 6.4 설명 안 되는 검토 항목`으로 바꾼다. 주석의 11.x 절 번호는 11.3 표의 자리로 읽는다.

### 12.6 승인 질문 (ADR 0005: 추천안 채택)

추천안은 "사용자에게 맞나 → 그다음 바이트 보존" 순서로 골랐다(카드 메모). "ADR 0004 기준"은 ADR 0005 전이었다면 누가 정했을지다(11.19와 같다). 질문은 11개다.

1. **결과 쪽 키 중복의 셈(`metrics.md` 13.2).** ADR 0004 기준: 리드.
   - 추천: 결과 쪽에 두 번 나온 기준 키는 값과 상관없이 값 불일치. 기준 항목에 대표 행의 차이, 중복 항목에 그 행의 차이. 셈은 줄 순서와 상관없다.
   - 대안(추천하지 않음): 모든 행이 기준과 맞으면 일치. 같은 행을 두 번 낸 결함이 정확도에 닿지 않는다. 대표 행에 따라 어느 항목에 차이가 적히는지가 더 복잡하다.
2. **`fields[].compared`(`metrics.md` 13.2).** ADR 0004 기준: 리드.
   - 추천: 기준에 키가 있는 결과 행 수(중복 행 포함). 필드마다 `outside` + `within` + `not_number` ≤ `compared`가 늘 맞는다. `compare-duplicate` 블록의 `compared`만 3 → 4.
   - 대안: 일치 + 값 불일치 그대로. 블록 바이트는 남지만 중복 행의 칸 수가 `compared`보다 커질 수 있다.
3. **metrics.json `schema_version`(`metrics.md` 13.4).** ADR 0004 기준: juhan 직접(expected 모양).
   - 추천: 1 그대로. 바뀐 출력은 T-012b에서 처음 받은, 릴리스되지 않은 입력(결과 쪽 중복)에서만 나온다. 기준을 ADR 0008 결정 1에 적는다.
4. **정의·대조 결과 검사(5절, 12.2).** ADR 0004 기준: 리드.
   - 추천: 5절 표대로 `ArgumentException`. 문턱은 보지 않는다.
   - 대안: 검사 없이 판정(지금). 다른 케이스의 결과로 문장을 낼 수 있다.
5. **6.4 표 중복 행 세 행.** ADR 0004 기준: 리드(테스트가 읽는 표의 값).
   - 추천: "불일치" 칸이 그 키를 센다(`1 | 0 | 1`, `2 | 1 | 1`). 머리 행 그대로. 판정 결과 칸은 그대로다.
6. **탐침 표의 T-011 칸(6.6).** ADR 0004 기준: 리드.
   - 추천: 기록으로 남기고 대조하지 않는다. 머리 행을 바꾸면 C# `ProbeTests` 상수가 바뀐다.
7. **문서 구조(11.3, 12.3).** ADR 0004 기준: 리드.
   - 추천: 11절 표를 4·6·7절로 옮기고 11절은 기록과 옮긴 곳 표만 둔다. 머리 행·블록 머리 줄 그대로.
8. **oracle 판 하나(12.4, ADR 0008 결정 4).** ADR 0004 기준: 리드(CI 도구).
   - 추천: `judge_v1.py`와 판 고르기 삭제. 옛 판 expected는 그 파일만 DIFF. ADR 0007 결정 4 그대로다.
9. **지시문 문구(ADR 0008 결정 2·3).** ADR 0004 기준: juhan 직접(지시문). 2절은 건드리지 않는다.
   - 추천: 5절 expected 목록에 `judgment.json`(판정·근거 문장·다음 할 일), "세 파일" → "이 파일들". 3.4 조건부에 "또는 사람이 아직 안 본 검토 항목(불일치·추가 행·중복 행)이 있음"을 더한다. 보류의 "설명 안 되는 불일치"는 "사람이 설명할 수 없다고 적은 검토 항목"으로 읽는다(괄호 한 줄).
10. **selftest 예제 이름(12.4).** ADR 0004 기준: 리드.
    - 추천: `examples/` 폴더 이름(서수)에서 읽는다. 7.1 예제 표 이름 = 폴더 이름, 폴더마다 7절 블록 하나. 예제를 더하면 표·블록이 없을 때 selftest가 실패한다.
11. **CI 설명 문구(ci-dev, ADR 0008 결정 5).** ADR 0004 기준: 리드(`ci-changed`).
    - 추천: `.github/workflows/ci.yml` 313줄과 `tools/ci/README.md` 160줄의 "단위 테스트가 대조한다"를 테스트 이름으로 바꾼다: `tools/ci/test_regression_guard.py`의 `TestGuardJobsWiring.test_workflow_passes_every_needed_job`. `tools/ci/lead_check.py` 58줄 주석도 같다(ci-dev 범위).

### 12.7 채택

- ADR 0005 결정 3에 따라 리드가 추천안을 채택하고 카드와 `docs/STATUS.md`에 적는다. 이 절은 추천안만 적는다.
