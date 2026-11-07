# judgment (T-011 스펙: 판정)

- 상태: 채택됨(2026-11-07, 리드가 추천안 1~12 채택. ADR 0005 결정 3. 기록은 카드 "잠정 결정"과 `docs/STATUS.md`). 이전 상태: 초안. ADR 0005 결정 3에 따라 승인 질문은 juhan에게 묻지 않고 추천안을 채택한다. 추천안은 회귀를 막는 쪽(기존 expected 바이트 유지, 기존 테스트 축소 없음)으로 골랐다. 질문과 채택 기록은 문서 끝.
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
- 불일치 = 값 불일치(`mismatch`) + 누락(`missing`) 항목. 기준 행만 센다. 추가 행(`extra`)은 넣지 않는다. `metrics.md` 4.4의 항목이다.
- 유형 열 = 기준 자료 머리 행의 열 가운데 키 열·`compare` 필드가 아닌 열(4.8).
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

- `CaseDefinition` 하나. 쓰는 것: `Name`, `Inputs.Key`, `Compare`(필드 이름), `Thresholds`(`AccuracyMin`, `MissingMax`, `TimeMustDrop`). `Steps`는 읽지 않는다.
- `CompareMetrics` 하나(`CompareEngine` 결과, `metrics.md` 3.1). 쓰는 것: `Status`, `Reason`, `Counts`, `AccuracyPct`, `MissingRatePct`, `Items`(상태와 기준 행 `CsvRow`). 오류가 있어 `Metrics`가 null이면 판정하지 않는다(5절).
- 시간 `JudgmentTimes`(선택). 건당 분. 적용 전, 처리, 검토 셋(4.10). 재지 않았으면 null. M2 `measure`가 만든다.
- 사람 입력 "설명 안 되는 불일치 키"(선택). 불일치 항목의 키 목록(4.9). 사람이 아직 보지 않았으면 null, 보고 모두 설명했으면 빈 목록. M2-4 `review`가 만든다.
- 파일을 읽지 않는다. 판정은 순수 함수다(4.12).
- 같은 `CompareMetrics`가 metrics.json의 원본이다. 판정은 metrics.json 파일을 다시 읽지 않는다(`metrics.md` 9절 8의 답: `CompareResult`를 그대로 받는다). 유형 열 값은 metrics.json에 없고 메모리 결과 `CsvRow`에만 있기 때문이다(`metrics.md` 승인 질문 16).

## 3. 출력

### 3.1 메모리 결과

`JudgmentResult` 하나(4.13). 판정, 근거 키, 판정 문장, 쓴 문턱, 기본값을 쓴 문턱 키, 확인 다섯(미검증이면 없음), 다음 할 일.

### 3.2 judgment.json

Core가 바이트를 만든다(`JudgmentJson.Render`). 모양:

```json
{
  "schema_version": 1,
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
- 첫 키는 `schema_version`이다. 값은 정수 `1`(ADR 0004 결정 2). 속성을 빼거나 이름·뜻을 바꾸는 카드가 값을 올리고 expected를 같이 고친다(ADR 0004 expected 변경 규칙).
- 쓰지 않는 것: 절대 경로, 입력 해시, 대조 셈(`counts`. metrics.json에 있다), 시간 값 원본, 사람 입력 원본. 숫자는 확인 문장 안에 글자로 들어간다.

속성 표. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 순서는 파일 안 순서다. `[]`는 배열 원소다.

| 속성 | 형식 | 뜻 |
| --- | --- | --- |
| `schema_version` | 정수 | 파일 모양 버전. 지금 `1`. 늘 첫 키 |
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

- `schema_version`은 `WriteNumber(int)`로 `1`. 문턱 숫자는 `decimal` 그대로 `WriteNumber(decimal)`. 자릿수가 남는다: `85`, `1.0`, `66.6`, `0.0` [T11:P:decimals].
- 문장 안의 숫자도 `decimal`을 `InvariantCulture`로 쓴 글자다. 퍼센트는 대조 결과 값(`metrics.md` 4.8, 늘 소수 한 자리)을 그대로 쓴다. 문턱은 `case.yaml` 글자 그대로다(`85`는 `85`, `90.0`은 `90.0`).
- 이스케이프는 `classification.md` 3.3 표와 같다. 유형 값에 큰따옴표가 있으면 `\"`, 이모지는 `😀`이다 [T11:P:escape].
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
2. 확인 다섯을 늘 모두 계산한다(4.4): 정확도, 누락률, 설명 안 되는 불일치, 시간, 몰림.
3. 판정 표(4.3)를 위에서부터 보고 처음 걸리는 행에서 멈춘다. 보류 → 조건부 → 통과 순서다. 보류가 조건부보다 앞이다(조건부는 "정확도는 충족하나"로 시작한다).
4. 문턱 비교는 셈(`counts`)으로 한다. 표시 퍼센트로 비교하지 않는다(`metrics.md` 4.8 권고). 1799/2000은 `90.0`으로 보이지만 문턱 90에 못 미친다 [T11:P:count-vs-display].
5. 누락률 초과는 원문에 보류·조건부 어느 쪽에도 없다. 통과의 조건이므로 통과는 아니다. 보류로 둔다(**해석**, 질문 2).
6. 판정 문장·확인 문장·다음 할 일 문구는 Core가 만든다(CLAUDE.md "판정·분류 로직은 Scorecard.Core에만").

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

테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 위에서부터 보고 처음 걸리는 행이 판정이다. "걸리는 결과"는 그 근거 키의 확인 결과(4.4)다. 같은 순서의 행은 둘 중 하나가 걸린다. 문장 틀의 `{…}`는 4.5의 값으로 바꾼다.

| 순서 | 근거 키 | 걸리는 결과 | 판정 | 판정 문장 틀 |
| --- | --- | --- | --- | --- |
| 1 | `NOT_VERIFIED` | `no_inputs` | `not_verified` | `검증 자료(case.yaml inputs)가 없어 미검증.` |
| 1 | `NOT_VERIFIED` | `no_compare_fields` | `not_verified` | `대조할 열(case.yaml compare)이 없어 미검증.` |
| 2 | `ACCURACY` | `not_met` | `hold` | `정확도 {accuracy_pct}%가 문턱 {accuracy_min}%에 못 미치므로 보류.` |
| 3 | `MISSING` | `not_met` | `hold` | `누락률 {missing_rate_pct}%가 문턱 {missing_max}%를 넘으므로 보류.` |
| 4 | `UNEXPLAINED` | `found` | `hold` | `사람이 설명할 수 없다고 적은 불일치가 {unexplained}건 있으므로 보류.` |
| 5 | `TIME` | `not_measured` | `conditional` | `정확도·누락률은 문턱을 충족하나 시간을 재지 않아 줄었는지 알 수 없으므로 조건부.` |
| 5 | `TIME` | `not_met` | `conditional` | `정확도·누락률은 문턱을 충족하나 처리·검토 시간 {total}분이 적용 전 {before}분보다 짧지 않으므로 조건부.` |
| 6 | `FOCUS` | `found` | `conditional` | `정확도·누락률은 문턱을 충족하나 불일치 {bad}건이 모두 {group} = {value}인 행이므로 조건부.` |
| 7 | `PASS` | — | `pass` | `정확도·누락률이 문턱을 충족하고 조건부·보류 조건에 걸리지 않으므로 통과.` |

- 1행의 결과는 대조 결과의 `reason`(`metrics.md` 4.4)이다. 미검증이면 확인이 없다(`checks` = `[]`).
- `PASS`는 위의 어느 행에도 걸리지 않을 때다. 확인 결과로 보면: 정확도·누락률 `met`, 설명 안 되는 불일치 `found` 아님, 시간 `off`·`met`, 몰림 `found` 아님.
- 시간(5)이 몰림(6)보다 앞이다. 둘 다 걸리면 시간이다. 확인 다섯은 `checks`에 모두 남으므로 몰림도 보인다.
- → 질문 1, 2.

### 4.4 확인 다섯

테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 확인 순서는 표에서 처음 나오는 순서(`ACCURACY` → `MISSING` → `UNEXPLAINED` → `TIME` → `FOCUS`)다. "조건"의 숫자는 대조 셈(`counts`)과 문턱이다.

| 확인 | 결과 | 조건 | 확인 문장 틀 |
| --- | --- | --- | --- |
| `ACCURACY` | `met` | `match × 100 ≥ accuracy_min × reference` | `정확도 {accuracy_pct}%({match}/{reference})가 문턱 {accuracy_min}% 이상이다.` |
| `ACCURACY` | `not_met` | 그 밖 | `정확도 {accuracy_pct}%({match}/{reference})가 문턱 {accuracy_min}%에 못 미친다.` |
| `MISSING` | `met` | `missing × 100 ≤ missing_max × reference` | `누락률 {missing_rate_pct}%({missing}/{reference})가 문턱 {missing_max}% 이하다.` |
| `MISSING` | `not_met` | 그 밖 | `누락률 {missing_rate_pct}%({missing}/{reference})가 문턱 {missing_max}%를 넘는다.` |
| `UNEXPLAINED` | `none` | 불일치 0건 | `불일치가 없다.` |
| `UNEXPLAINED` | `not_reviewed` | 불일치 1건 이상, 사람 입력 없음(null) | `불일치 {bad}건이 설명되는지 사람이 아직 보지 않았다.` |
| `UNEXPLAINED` | `explained` | 사람 입력이 빈 목록 | `불일치 {bad}건을 사람이 보고 모두 설명했다.` |
| `UNEXPLAINED` | `found` | 사람 입력에 키가 1개 이상 | `불일치 {bad}건 중 {unexplained}건을 사람이 설명할 수 없다고 적었다.` |
| `TIME` | `off` | `time_must_drop`이 `false` | `시간 조건을 쓰지 않는다(time_must_drop: false).` |
| `TIME` | `not_measured` | `time_must_drop`이 `true`, 시간 없음(null) | `시간을 재지 않았다.` |
| `TIME` | `met` | `process + review < before` | `처리 {process}분 + 검토 {review}분 = {total}분이 적용 전 {before}분보다 짧다.` |
| `TIME` | `not_met` | 그 밖(같으면 줄지 않았다) | `처리 {process}분 + 검토 {review}분 = {total}분이 적용 전 {before}분보다 짧지 않다.` |
| `FOCUS` | `few` | 불일치가 `focus_min`(4.5)보다 적음 | `불일치가 {bad}건이라 몰림을 보지 않는다.` |
| `FOCUS` | `off` | 유형 열이 없음(4.8) | `기준 자료에 키·대조 열 밖의 열이 없어 불일치 {bad}건이 한 유형에 몰렸는지 볼 수 없다.` |
| `FOCUS` | `one_value` | 모든 유형 열이 기준 자료에서 값 하나뿐 | `키·대조 열 밖의 열({columns})이 기준 자료에서 모두 값이 하나뿐이라 몰림을 볼 수 없다.` |
| `FOCUS` | `found` | 어떤 유형 열에서 불일치가 모두 한 값(4.8) | `불일치 {bad}건이 모두 {group} = {value}인 행이다({value} {rows_in}행 중 {bad}행, 다른 값 {rows_out}행 중 0행).` |
| `FOCUS` | `spread` | 그 밖 | `불일치 {bad}건이 한 값에 몰린 열이 없다(본 열: {columns}).` |

- 셈은 정수와 decimal이다. 나눗셈을 쓰지 않는다. `reference`는 0이 아니다(기준 행이 0이면 대조가 `no-rows` 오류다, `metrics.md` 4.7).
- 시간 합 `process + review`가 decimal 범위를 넘으면 `not_met`이다. 예외가 새지 않는다 [T11:P:time overflow].
- 시간은 `time_must_drop`이 `false`면 받아도 보지 않는다(`off`).

### 4.5 문장 값과 숫자

문장 틀의 값:

- `{accuracy_pct}`·`{missing_rate_pct}`: 대조 결과의 퍼센트(소수 한 자리). `{match}`·`{missing}`·`{reference}`: `counts`. `{accuracy_min}`·`{missing_max}`: 쓴 문턱(4.2). 숫자는 `InvariantCulture`.
- `{bad}`: 불일치 수(0절). `{unexplained}`: 사람이 적은 키 수.
- `{process}`·`{review}`·`{before}`: 받은 시간. `{total}`: `process + review`(decimal 셈, 자릿수는 decimal 덧셈 그대로. `1.25 + 1.2` = `2.45`) [T11:P:decimals].
- `{group}`: 몰린 유형 열 이름. `{value}`: 그 값. `{rows_in}`: 기준 행 가운데 그 값인 행 수. `{rows_out}`: 나머지 기준 행 수. `{columns}`: 유형 열 이름을 이름 서수 순으로 `, `로 이은 것(`spread`는 값이 둘 이상인 열만, `one_value`는 모든 유형 열).
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

읽은 문턱. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다.

| 근거 키 | 읽은 문턱 |
| --- | --- |
| `NOT_VERIFIED` | — |
| `ACCURACY` | `accuracy_min` |
| `MISSING` | `accuracy_min`, `missing_max` |
| `UNEXPLAINED` | `accuracy_min`, `missing_max` |
| `TIME` | `accuracy_min`, `missing_max`, `time_must_drop` |
| `FOCUS` | `accuracy_min`, `missing_max`, `time_must_drop` |
| `PASS` | `accuracy_min`, `missing_max`, `time_must_drop` |

- 미검증 문장은 문턱을 읽지 않는다. mixed-exceptions는 기본값 셋을 썼지만 판정 문장에 표시가 없다. 대신 `defaults`와 `confirm_thresholds`에 남는다(7.4).

### 4.7 다음 할 일

지시문 3.4 "미검증 = 자료 없음(무엇을 재면 되는지 적는다)", 3.5 6구역 "다음 할 일 — 확보할 기준 자료, 재야 할 시간, 확인할 질문" [읽음]. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. `todo` 순서는 이 표 순서다.

| 다음 할 일 | 언제 | 문구 |
| --- | --- | --- |
| `add_data` | 미검증 `no_inputs` | `기준 자료(사람이 만든 결과)를 reference/에, 결과 자료(AI 적용 후 결과)를 candidate/에 넣고 case.yaml inputs에 파일 패턴과 키 열을 적는다.` |
| `add_compare` | 미검증(둘 다) | `case.yaml compare에 대조할 열과 허용 오차를 적는다.` |
| `measure_time` | `TIME` = `not_measured`, 또는 미검증이고 `time_must_drop`이 `true` | `건당 처리 시간·검토 시간과 적용 전 시간(분)을 잰다.` |
| `review_mismatches` | `UNEXPLAINED` = `not_reviewed` | `불일치 {bad}건을 보고 설명이 되는지 적는다.` |
| `add_type_column` | `FOCUS` = `off` | `불일치가 한 유형에 몰렸는지 보려면 기준 자료에 유형 열(키·대조 열 밖의 열)을 더한다.` |
| `confirm_thresholds` | 기본값을 쓴 문턱이 있음 | `문턱에 기본값을 썼다({defaults}). 맞는지 확인하고 case.yaml thresholds에 적는다.` |

- `{defaults}`: 기본값을 쓴 키와 값을 4.2 순서로 `, `로 잇는다. 예: `accuracy_min 95, missing_max 2, time_must_drop true`.
- `no_compare_fields` 미검증은 `add_data` 없이 `add_compare`부터다(`inputs`는 있다).
- 통과·할 일 없음이면 `todo`는 `[]`다(form-fill, 7.3).

### 4.8 "특정 유형에만 불일치가 몰림"

`metrics.md` 승인 질문 16은 어느 열로 몰림을 볼지를 이 카드로 넘겼다 [읽음]. 예제 열 [읽음]:

| 예제 | 기준 머리 행 | 키 | `compare` | 남는 열(유형 열) |
| --- | --- | --- | --- | --- |
| table-compare | `log_id,signal_type,start_index,length,mean_value` | `log_id` | `start_index`, `length`, `mean_value` | `signal_type` |
| form-fill | `order_id,item,quantity` | `order_id` | `item`, `quantity` | 없음 |
| signoff-final | `record_id,record_kind,range_start,range_end,range_avg` | `record_id` | `range_start`, `range_end`, `range_avg` | `record_kind` |
| G2 표본 case-valid | `row_id,start,mean,label` | `row_id` | `start`, `mean`, `label` | 없음 |

어느 열을 볼지(안):

| 안 | 유형 열 | `case.yaml`·파서 | 예제 파일 | 기존 테스트 | 예제 판정 |
| --- | --- | --- | --- | --- | --- |
| M-A | `inputs.group`(새 선택 키)에 적은 열 하나 | 바뀐다(`CaseInputs`, `CaseReader`, `CompareEngine`이 열 확인) | table-compare·signoff-final `case.yaml`에 한 줄. `synthetic-examples.md` 7.2·7.5 블록·SHA-256 줄, `signoff-final.md` 파일 표가 같이 바뀐다 | 2건 실패(`CaseYamlTests.Case_data_keys_match_spec_table` × 2) [T11:S7c] | 적으면 M-B와 같다. 적지 않으면 table-compare가 통과(12절과 다름) |
| M-B (추천) | 기준 머리 행의 열 가운데 키 열·`compare` 필드가 아닌 열 전부 | 그대로 | 그대로 | 그대로 [T11:S2] | 12절과 같다(7절) |
| M-C | M-A + 키가 없으면 M-B | M-A와 같다 | 그대로 | M-A와 같다 | M-B와 같다 |

- 추천 M-B. 기존 파일 바이트를 하나도 바꾸지 않고 12절 판정을 낸다(ADR 0005 리드 지시: 회귀를 막는 쪽). 지시문 13절 "카드에 없는 기능을 만들지 않는다"에도 맞다(새 스키마 키가 없다).
- M-B의 위험: 우연히 값이 같은 열(담당자, 날짜 등)로 몰림이 걸릴 수 있다. 걸리면 판정이 통과에서 조건부로 내려간다. 통과를 내지 않는 쪽으로 틀리고, 판정 문장에 열과 값이 나오므로 사람이 본다. 실제 업무 자료에서 얼마나 걸리는지는 **미확인**. 자주 걸리면 그때 M-C(열을 고정하는 `inputs.group`)를 별도 카드로 더한다. 그 변경은 스키마 변경이라 예제 `case.yaml`을 바꾸지 않는 방식(선택 키, 예제에는 적지 않음)으로 한다.
- M-A의 영향(스파이크 S7c, 추천하지 않음): 아래 "M-A 영향" 글머리.

유형 열 규칙(M-B):

1. 후보 열: 모든 기준 파일의 머리 행에 꼭 한 번 있는 이름. 키 열, `compare` 필드, 빈 이름은 뺀다. 한 파일에만 있는 열과 한 머리 행에 둘 이상 있는 이름(쓰지 않는 열의 중복은 대조 오류가 아니다, `metrics.md` 4.7)은 유형 열이 아니다 [T11:P:focus multi-file, dup-column, empty-name].
2. 열 이름 서수 순(UTF-16 코드 단위)으로 본다. 파일 순서·머리 행 순서와 상관없다 [T11:P:focus multi-file files-ba].
3. 값은 기준 행의 칸(정규화 뒤, `metrics.md` 4.6)이다. 결과 자료의 값은 보지 않는다. 추가 행은 보지 않는다 [T11:P:focus extra-rows-ignored].
4. 불일치가 `focus_min`보다 적으면 `few`. 후보 열이 없으면 `off`.
5. 열마다: 기준 행 값이 하나뿐이면 건너뛴다. 불일치 행의 값이 모두 같으면 그 열에서 `found`. 처음 `found`인 열에서 멈춘다.
6. `found`인 열이 없으면, 건너뛰지 않은 열이 있으면 `spread`, 모두 건너뛰었으면 `one_value`.

- "에만"은 불일치가 모두 한 값이고, 그 값이 아닌 기준 행이 있다(값이 하나뿐인 열은 건너뜀)는 뜻으로 읽었다. 그 값의 행이 모두 불일치일 필요는 없다(table-compare: `irregular` 12행 중 3행).

몰림 정의(안):

| 정의 | 내용 | table-compare | signoff-final | 숫자 |
| --- | --- | --- | --- | --- |
| F1 (추천) | 불일치 `focus_min`건 이상이 모두 한 값 | `found`(3건 모두 `irregular`) | `spread`(`unsteady` 3, `steady` 1) | `focus_min` 2 |
| F2 | F1 + 그 값 행의 불일치 비율이 다른 값보다 높음 | `found` | `spread` | 없음. F1이면 늘 참이라 F1과 같다 |
| F3 | 불일치 가운데 한 값의 비율이 기준 이상(예: 75%) | `found` | `found`(4건 중 3건, 75%) | 비율 하나를 새로 정해야 한다 |

- F1을 추천한다. 원문 "에만"에 가장 가깝고, 새 숫자가 `focus_min` 하나다. F3은 지시문에 없는 비율을 만든다. signoff-final은 정확도 미달로 어느 정의든 보류라 판정이 같다.

M-A 영향 [T11:S7c] (따로 clone에 `CaseInputs.Group`·파서·대조 열 확인과 예제 `case.yaml` 두 줄을 넣고 전체 테스트):

- 바뀌는 파일: 예제 `case.yaml` 2개(table-compare `451a22ac…` → `e99d669a…`, 1,640 → 1,661바이트. signoff-final `a12835ea…` → `e11e7b68…`, 2,595 → 2,616바이트), `synthetic-examples.md` 7.2·7.5 블록과 SHA-256 줄, `signoff-final.md` 파일 표 1행. 스키마 문서(`case-schema.md` 4.5 `inputs` 표)도 고쳐야 한다.
- expected 8개(`classification.json`·`metrics.json` × 4)는 바이트 그대로다(골든 테스트 통과).
- 통과하지 않는 기존 테스트 2건: `CaseYamlTests.Case_data_keys_match_spec_table`(table-compare, signoff-final). 이 테스트는 `synthetic-examples.md` 4.8 표의 `inputs` 칸(세 값)으로 `CaseInputs`를 만들어 비교한다. 표와 테스트의 읽는 법을 같이 고쳐야 한다(테스트 변경. 약화는 아님). 나머지 1,280건은 통과했다.
- 예제 `case.yaml` 변경은 regression-guard의 expected 규칙 밖이지만 공개 예제의 바이트를 바꾼다. M-B는 이 변경이 모두 없다.
- → 질문 5.

### 4.9 "설명 안 되는 불일치"

- 엔진은 불일치가 "설명되는지" 알 수 없다. 설명은 업무 지식이다(예: "불규칙 신호라 사람도 구간이 다르다"). 자료만으로 정하면 엔진이 사용자 대신 판단하게 된다(지시문 13절 "엔진은 사용자 서술을 … 이해하지 않는다").
- 그래서 사람 입력으로 받는다(안 U-B, 추천). 확인 한 화면(`review`, M2-4)에서 사람이 불일치(값 불일치·누락) 목록을 보고 설명이 안 되는 키를 적는다. 판정은 그 키 목록만 받는다.
  - 키 목록이 null(아직 보지 않음): `not_reviewed`. 보류하지 않는다. 다음 할 일 `review_mismatches`.
  - 빈 목록: `explained`. 키가 있으면 `found` → 보류(4.3 4행).
  - 키는 불일치 항목의 키여야 하고 중복이 없어야 한다. 아니면 `ArgumentException`(부르는 쪽 잘못) [T11:P:args].

| 안 | 내용 | table-compare | 문제 |
| --- | --- | --- | --- |
| U-A | 사람이 보지 않은 불일치를 "설명 안 됨"으로 본다 | 보류 | 12절 "조건부"와 다르다. 자료만 넣은 첫 판정이 늘 보류가 된다 |
| U-B (추천) | 사람이 적은 키만. 보지 않았으면 `not_reviewed`(보류 아님) | 조건부 | 사람이 보기 전까지는 다음 할 일로만 남는다 |
| U-C | 몰린 유형 밖의 불일치를 "설명 안 됨"으로 본다 | 조건부 | 몰림과 설명을 같은 것으로 센다. signoff-final처럼 흩어지면 모두 설명 안 됨이 된다. 근거가 없다 |

- U-B에서 판정은 사람 입력이 들어오면 바뀔 수 있다(조건부 → 보류). 그래서 `checks`에 `not_reviewed`를 남기고, 스코어카드 6구역에 `review_mismatches`가 나온다.
- → 질문 6.

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
- 지시문 5절 문구(리드가 고친다, 제안): "`classification.json`(단계별 분류·근거 키), `metrics.json`(정확도·누락률 등), `judgment.json`(판정·근거 문장·다음 할 일), `report-outline.json`(평가서 섹션별 핵심 값)". "이 세 파일"은 "이 파일들"로.
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

- 표본 판정: 보류 `ACCURACY`(66.7% < 90), 시간 `not_measured`, 몰림 `off`(유형 열 없음), 다음 할 일 셋 [T11:S3 judge-case-valid]. 숫자가 문장에 들어가므로 문화권(R4)을 본다.
- 결과 [T11:S5]: 전체 테스트 1,284개 통과(1,282 + G2·G3 각 1). 새 샘플이 없다. 샘플 목록(중복 뺀 것)이 `case-invalid`, `case-valid`, `compare-invalid` 그대로라 `Real_targets_use_case_samples_not_basic`(T-010이 정확한 목록 비교로 되돌림)을 고치지 않고 통과한다.
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
    public const int SchemaVersion = 1;

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
- Core 파일 4개: `JudgmentModel.cs`, `JudgmentRules.cs`, `JudgmentEngine.cs`, `JudgmentJson.cs`. 기존 Core 파일은 바꾸지 않는다(M-B). 값 표시는 기존 `internal` `CompareRules.Show`를 쓴다(같은 어셈블리).
- → 질문 10.

## 5. 예외

| 상황 | 결과 | 근거 |
| --- | --- | --- |
| `definition`·`metrics`가 null | `ArgumentNullException` | [T11:P:args null] |
| `metrics.Case`가 `definition.Name`과 다름 | `ArgumentException` | [T11:P:args name-mismatch] |
| 시간 값이 음수 | `ArgumentOutOfRangeException` | [T11:P:args negative-time] |
| 사람 입력 키가 불일치 항목이 아니거나 중복 | `ArgumentException` | [T11:P:args unexplained-*] |
| 대조 오류(`CompareResult.Metrics`가 null) | 판정하지 않는다. 부르는 쪽이 대조 오류를 보인다(종료 코드 2) | 2절 |
| `inputs` 없음 / `compare` 빔 | 미검증. `checks` = `[]`. 다음 할 일 4.7 | 4.3 |
| `thresholds` 일부만 적음 | 빈 키만 기본값. 그 키만 `defaults`·표시 | 4.2, 4.6 |
| 시간 합이 decimal 범위를 넘음 | `TIME` = `not_met` | [T11:P:time overflow] |
| 시간을 받았는데 `time_must_drop: false` | 시간을 보지 않는다(`off`) | 4.4 |
| 유형 열 값에 큰따옴표·이모지·제어 문자 | 문장에는 `CompareRules.Show` 표시, JSON은 3.3 이스케이프 | [T11:P:escape] |
| 유형 열 값이 빈 칸 | `(빈 칸)`으로 보인다. 빈 칸끼리 같은 값 | 6.3 |
| 머리 행이 다른 기준 파일 여럿 | 모든 파일에 꼭 한 번 있는 열만 유형 열 | [T11:P:focus multi-file] |
| 불일치는 있는데 추가 행만 다른 유형 | 추가 행은 보지 않는다 | [T11:P:focus extra-rows-ignored] |

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

유형 열 하나(`grp`). 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. `a×5`는 값 `a`인 행 5개, `(빈 칸)`은 빈 값, `(없음)`은 행이 없음이다. "유형 열"이 `없음`이면 기준 자료에 `grp` 열이 없다(키·`compare` 열뿐). 불일치는 값 불일치·누락을 번갈아 둔다.

| 기준 행 유형 | 불일치 행 유형 | 추가 행 유형 | 유형 열 | FOCUS |
| --- | --- | --- | --- | --- |
| `a×5`, `b×5` | (없음) | (없음) | 있음 | `few` |
| `a×5`, `b×5` | `a` | (없음) | 있음 | `few` |
| `a×5`, `b×5` | `a`, `a` | (없음) | 없음 | `off` |
| `a×5`, `b×5` | `a`, `a` | (없음) | 있음 | `found` |
| `a×10` | `a`, `a` | (없음) | 있음 | `one_value` |
| `a×5`, `b×5` | `a`, `b` | (없음) | 있음 | `spread` |
| `a×5`, `b×5` | `a×5` | (없음) | 있음 | `found` |
| `a×9`, `b×1` | `a×9` | (없음) | 있음 | `found` |
| `(빈 칸)×5`, `b×5` | `(빈 칸)`, `(빈 칸)` | (없음) | 있음 | `found` |
| `a×5`, `A×5` | `a`, `A` | (없음) | 있음 | `spread` |
| `a×5`, `b×5` | `a`, `a` | `b`, `b`, `b` | 있음 | `found` |

유형 열 여럿. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. `p=aaabbb`는 열 `p`의 행 1~6 값이다(글자 하나가 한 행, `_`는 빈 칸). 머리 행은 `id,<열들>,v`, 키 `id`, `compare`는 `v`다. 불일치 행은 1부터 센다. "걸린 열"은 `found` 문장의 `{group} = {value}`, "본 열"은 `spread`·`one_value` 문장의 `{columns}`다.

| 열 값(행 1~6) | 불일치 행 | FOCUS | 걸린 열 | 본 열 |
| --- | --- | --- | --- | --- |
| `p=aaabbb` | `1,2` | `found` | `p = a` | — |
| `p=aaabbb` | `1,4` | `spread` | — | `p` |
| `p=aaaaaa` | `1,2` | `one_value` | — | `p` |
| `q=aaabbb`, `p=ababab` | `1,2` | `found` | `q = a` | — |
| `p=aabbcc`, `q=xxyyzz` | `1,2` | `found` | `p = a` | — |
| `p=aaaaaa`, `q=ababab` | `1,2` | `spread` | — | `q` |
| `p=aaaaaa`, `q=bbbbbb` | `1,2` | `one_value` | — | `p, q` |
| `p=aaabbb`, `q=ababab` | `1,4` | `spread` | — | `p, q` |
| `b=aabbcc`, `B=xxyyzz` | `1,2` | `found` | `B = x` | — |
| `p=__abab` | `1,2` | `found` | `p = (빈 칸)` | — |
| `p=aaabbb`, `p=aaabbb` | `1,2` | `off` | — | — |

- 넷째 행: `p`를 먼저 보지만 흩어져 있고 `q`에서 걸린다. 다섯째 행: 둘 다 걸리면 이름 서수로 앞인 `p`. 아홉째 행: 서수로 `B`(U+0042)가 `b`(U+0061)보다 앞이다(문화권 비교가 아니다).
- 셋째 행: 키 열 `id`(값이 행마다 다름)와 `compare` 열 `v`는 유형 열이 아니다. 그래서 `one_value`이고 본 열이 `p`뿐이다.
- 마지막 행: 같은 이름 둘은 유형 열이 아니다.

### 6.4 설명 안 되는 불일치

테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. "사람 입력"의 `없음`은 null, `[]`는 빈 목록, `N건`은 불일치 키 N개다.

| 불일치 | 사람 입력 | UNEXPLAINED |
| --- | --- | --- |
| 0 | 없음 | `none` |
| 0 | `[]` | `none` |
| 3 | 없음 | `not_reviewed` |
| 3 | `[]` | `explained` |
| 3 | 1건 | `found` |
| 3 | 3건 | `found` |

### 6.5 우선순위

확인 다섯의 결과 조합과 판정. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다.

| ACCURACY | MISSING | UNEXPLAINED | TIME | FOCUS | 판정 | 근거 키 |
| --- | --- | --- | --- | --- | --- | --- |
| `met` | `met` | `none` | `off` | `few` | `pass` | `PASS` |
| `met` | `met` | `none` | `met` | `few` | `pass` | `PASS` |
| `met` | `met` | `none` | `not_measured` | `few` | `conditional` | `TIME` |
| `met` | `met` | `none` | `not_met` | `few` | `conditional` | `TIME` |
| `met` | `met` | `not_reviewed` | `off` | `few` | `pass` | `PASS` |
| `met` | `met` | `not_reviewed` | `off` | `spread` | `pass` | `PASS` |
| `met` | `met` | `not_reviewed` | `off` | `off` | `pass` | `PASS` |
| `met` | `met` | `not_reviewed` | `off` | `one_value` | `pass` | `PASS` |
| `met` | `met` | `not_reviewed` | `off` | `found` | `conditional` | `FOCUS` |
| `met` | `met` | `not_reviewed` | `not_measured` | `found` | `conditional` | `TIME` |
| `met` | `met` | `explained` | `met` | `found` | `conditional` | `FOCUS` |
| `met` | `met` | `explained` | `met` | `spread` | `pass` | `PASS` |
| `met` | `met` | `found` | `off` | `few` | `hold` | `UNEXPLAINED` |
| `met` | `met` | `found` | `not_met` | `found` | `hold` | `UNEXPLAINED` |
| `met` | `not_met` | `not_reviewed` | `off` | `spread` | `hold` | `MISSING` |
| `met` | `not_met` | `found` | `not_measured` | `found` | `hold` | `MISSING` |
| `not_met` | `met` | `not_reviewed` | `off` | `found` | `hold` | `ACCURACY` |
| `not_met` | `not_met` | `found` | `not_met` | `found` | `hold` | `ACCURACY` |

- `UNEXPLAINED` = `none`(불일치 0건)이면 정확도·누락률은 늘 `met`이고 몰림은 `few`다. 그래서 그런 조합은 표에 없다.
- 사람이 보지 않은 불일치(`not_reviewed`)는 판정을 막지 않는다(4.9). 몰림·시간이 없으면 통과다.

## 7. 예제 4종 `expected/judgment.json` 초안

### 7.1 만드는 방법과 확정 절차

1. 아래 블록은 두 구현이 공개 예제 파일(`examples/*/case.yaml`·`reference/`·`candidate/`, 바꾸지 않음)에서 따로 낸 바이트가 같은 것이다.
   - (a) 스파이크 후보(C#, `spikes/T-011/core/Judgment/`) [T11:S3 judge]. 블록은 이것으로 채웠다(`spikes/T-011/blocks.py fill`).
   - (b) Python 독립 구현(`spikes/T-011/xcheck/judge.py`) [T11:S3 xcheck]. 이 문서 4절의 표와 글만 보고 짰다. C# 코드를 쓰지 않는다. 대조 부분은 T-006 Python 독립 구현에서 가져왔다(`metrics.md` 7.1).
   - (a)와 (b)가 4종 모두 바이트까지 같다(`cmp`) [T11:S3 xcheck-cmp].
2. ADR 0005 결정 3에 따라 사람 확정 없이 추천안(이 블록)을 채택한다. 채택 기록은 카드 "잠정 결정"과 `docs/STATUS.md`에 적는다(리드).
3. test-engineer가 블록을 바이트 그대로 `examples/<예제>/expected/judgment.json`으로 옮긴다. Core 출력에서 복사하지 않는다(지시문 2절 11 "기대값을 먼저"). 테스트가 블록·SHA-256 줄·파일이 같은지 본다. 카드 폴더 범위에 `examples/*/expected/`가 있다 [읽음 카드].
4. 커밋 모양: 새 파일 4개만 담은 커밋. 기존 expected를 바꾸지 않으므로 regression-guard의 "기존 파일 수정·삭제"에 걸리지 않는다. ADR 0004 규칙을 따라 메시지는 `expected migration:`으로 시작한다(새 파일이지만 같은 모양으로 남긴다). oracle 잡이 같은 바이트를 재현해야 한다(4.11).
5. `examples/.gitattributes`(`* -text`)가 새 파일에도 걸린다(T-004). 기존 테스트 `Example_text_files_have_no_carriage_return`·`ExamplePatternTests`가 새 파일도 본다 [T11:S7b].
6. 기존 expected 8개(`classification.json`·`metrics.json` × 4)와 예제의 다른 파일은 바이트 그대로다 [T11:S7].

예제 표. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. "확인 다섯"은 4.4 순서의 결과, "다음 할 일"은 `todo` 키 순서다.

| 예제 | 판정 | 근거 키 | 확인 다섯 | 다음 할 일 | 12절 |
| --- | --- | --- | --- | --- | --- |
| `table-compare` | `conditional` | `FOCUS` | `met`, `met`, `not_reviewed`, `off`, `found` | `review_mismatches` | 조건부 |
| `form-fill` | `pass` | `PASS` | `met`, `met`, `none`, `off`, `few` | — | 통과 |
| `mixed-exceptions` | `not_verified` | `NOT_VERIFIED` | — | `add_data`, `add_compare`, `measure_time`, `confirm_thresholds` | 미검증 |
| `signoff-final` | `hold` | `ACCURACY` | `not_met`, `not_met`, `not_reviewed`, `off`, `spread` | `review_mismatches` | 판정 카드가 정함(정확도 80%·누락률 5%, 문턱 95%·2% 미달) |

- 12절 대조: table-compare 조건부, form-fill 통과, mixed-exceptions 미검증. 셋 다 같다. signoff-final은 12절이 "판정은 판정 카드에서"라 했고 이 스펙이 보류로 정한다(정확도 미달 → 보류, 지시문 3.4).
- table-compare가 조건부인 근거: 불일치 3건(`log07`, `log08`, `log27`)이 모두 `signal_type = irregular`이고 `normal` 18행에는 없다. `synthetic-examples.md` 4.7이 심은 구조와 같다 [읽음]. 해석이 달랐다면(유형 열을 보지 않음) 통과가 되어 12절과 맞지 않았다(4.8 M-A에서 키를 적지 않은 경우).
- signoff-final은 몰림이 `spread`다(`unsteady` 3건, `steady` 1건 `rec14`). 정확도 미달이 먼저라 판정에 영향이 없다.

### 7.2 table-compare

`examples/table-compare/expected/judgment.json` (SHA-256 `1c32a9c92311e43d19a01633a171240b3b4fa215adcafe7f4d470dc45b4f5c57`):

```json
{
  "schema_version": 1,
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
      "reason": "불일치 3건이 설명되는지 사람이 아직 보지 않았다."
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
      "key": "review_mismatches",
      "text": "불일치 3건을 보고 설명이 되는지 적는다."
    }
  ]
}
```

### 7.3 form-fill

`examples/form-fill/expected/judgment.json` (SHA-256 `08cb0cac5df480e2611d2d92dbfca31cc2ebd6f3fcb9430d98d35343b74a0186`):

```json
{
  "schema_version": 1,
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
      "reason": "불일치가 없다."
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

`examples/mixed-exceptions/expected/judgment.json` (SHA-256 `85e5da47de436644228ca4f1d75ac086891d1739a34c93f441bee71afdc9df11`):

```json
{
  "schema_version": 1,
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

`examples/signoff-final/expected/judgment.json` (SHA-256 `84da5aa2d9aaa6ead84c767235495d96189636987bb78ac24b62ddeda607871b`):

```json
{
  "schema_version": 1,
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
      "reason": "불일치 4건이 설명되는지 사람이 아직 보지 않았다."
    },
    {
      "rule": "TIME",
      "result": "off",
      "reason": "시간 조건을 쓰지 않는다(time_must_drop: false)."
    },
    {
      "rule": "FOCUS",
      "result": "spread",
      "reason": "불일치 4건이 한 값에 몰린 열이 없다(본 열: record_kind)."
    }
  ],
  "todo": [
    {
      "key": "review_mismatches",
      "text": "불일치 4건을 보고 설명이 되는지 적는다."
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
| 판정 표(4.3) | 머리 행 `\| 순서 \| 근거 키 \| 걸리는 결과 \| 판정 \| 판정 문장 틀 \|`. 행마다 그 행에 걸리는 장면의 판정·근거 키·문장 | 2 |
| 확인 표(4.4) | 머리 행 `\| 확인 \| 결과 \| 조건 \| 확인 문장 틀 \|`. 행마다 결과·문장. 확인 순서 | 2 |
| 숫자(4.5) | 머리 행 `\| 이름 \| 값 \| 뜻 \|`. `focus_min`−1은 `few`, `focus_min`은 `found` | 2 |
| 읽은 문턱(4.6) | 머리 행 `\| 근거 키 \| 읽은 문턱 \|`. 문턱 하나씩 기본값으로 바꿔 판정 문장·확인 문장 표시. 표시 글자는 정확히 ` (기본값 사용, 확인 필요)` | 2 |
| 다음 할 일(4.7) | 머리 행 `\| 다음 할 일 \| 언제 \| 문구 \|`. 키마다 나오는 장면과 문구, `todo` 순서 | 3 |
| 문턱 경계(6.1) | 머리 행 `\| 기준 행 \| 일치 \| 누락 \| 표시 정확도(%) \| …`. 표시 퍼센트와 확인 결과 | 2 |
| 시간(6.2) | 머리 행 `\| time_must_drop \| 적용 전 \| 처리 \| 검토 \| TIME \|`. 넘침 1건 더 | 3 |
| 몰림(6.3) | 머리 행 `\| 기준 행 유형 \| 불일치 행 유형 \| 추가 행 유형 \| 유형 열 \| FOCUS \|`, `\| 열 값(행 1~6) \| 불일치 행 \| FOCUS \| 걸린 열 \| 본 열 \|`. 여러 기준 파일(한 파일에만 있는 열), 빈 이름 열 | 2 |
| 설명 안 됨(6.4) | 머리 행 `\| 불일치 \| 사람 입력 \| UNEXPLAINED \|`. 잘못된 키·중복은 예외 | 2 |
| 우선순위(6.5) | 머리 행 `\| ACCURACY \| MISSING \| UNEXPLAINED \| TIME \| FOCUS \| 판정 \| 근거 키 \|`. 장면의 확인 다섯이 표와 같고 판정이 같다 | 2 |
| JSON(3.2·3.3) | 머리 행 `\| 속성 \| 형식 \| 뜻 \|`. 경로 순서 = 표. 첫 키 `schema_version` = 정수 1. BOM·CR 없음, 끝 `\n`, 이스케이프, decimal 자릿수 | 4 |
| 골든 | `examples/<예제>/` → `CaseParser.ParseDirectory` → `CompareDirectory` → `Judge`(시간·사람 입력 null) → `Render`가 `expected/judgment.json`과 바이트까지 같다. 예제 4종 | 1 |
| 예제 표(7.1) | 머리 행 `\| 예제 \| 판정 \| 근거 키 \| 확인 다섯 \| 다음 할 일 \| 12절 \|`. `expected/judgment.json`의 값이 표와 같다 | 1 |
| 스펙 블록 = 예제 파일 | 7.2~7.5 블록, SHA-256 줄, 파일이 같다 | 1, 4 |
| 결정론 | de-DE·tr-TR로 바꿔도 같은 바이트(`try/finally`). 같은 입력 두 번. 입력 metrics.json 바이트 그대로 | — |
| 예외(5절) | 인자 예외 4종 | — |
| G2·G3 | `GuardTargets`에 `judge-case-valid`(4.12). 샘플 목록 테스트는 그대로 | — |
| 예제 폴더 파일 | `Example_folder_has_exactly_the_planned_files`의 목록에 `expected/judgment.json` 추가 | 1 |

- 표 이름(머리 행)이 이 문서에 정확히 한 번 나와야 한다. `SpecDocument.Table`이 그렇게 검사한다 [읽음]. `| 속성 | 형식 | 뜻 |`와 G2 표 머리 행은 `metrics.md`와 같은 글자다. 문서마다 한 번이라 괜찮다.
- 실패 테스트 먼저: test-engineer 단계에서는 `Scorecard.Core.Judgment`가 없어 새 테스트가 컴파일되지 않는다. T-003 승인 결정 14와 같이 이 상태를 "실패하는 테스트"로 보고, 같은 브랜치에서 core-dev가 구현한다.
- 골든 테스트는 `examples/` 파일이 있어야 돈다. 파일이 없으면 실패해야 한다(빈 통과 금지).
- 기존 테스트는 지우거나 완화하지 않는다. 고치는 것은 `Example_folder_has_exactly_the_planned_files` 목록 한 줄뿐이다(계획에 파일을 더함).

## 9. 미확인 목록

1. GitHub ubuntu·windows 러너에서 판정·골든·G2·G3 테스트가 녹색인지.
2. 실제 업무 자료에서 유형 열(M-B)이 우연히 몰림을 내는 빈도(4.8).
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
11. **G2·G3 등록(4.12).** ADR 0004 기준: 리드(기존 테스트를 바꾸지 않는다).
    - 추천: `judge-case-valid`(샘플 `case-valid`). 새 샘플이 없어 샘플 목록 테스트를 고치지 않는다. 판정 오류 표본은 두지 않는다.
12. **문장 틀과 다음 할 일 문구(4.3·4.4·4.7).** ADR 0004 기준: 리드.
    - 추천: 표 그대로. 미검증의 "무엇을 재면 되는지"는 `add_data`·`add_compare`·`measure_time` 문구로 적는다. 문구를 바꾸면 이 표와 expected를 같이 고치고 oracle이 같은 바이트를 내야 한다.

## 채택

- ADR 0005 결정 3에 따라 리드가 1~12의 추천안을 채택하고 카드 "잠정 결정"과 `docs/STATUS.md`에 적는다. 이 문서는 추천안만 적는다.
