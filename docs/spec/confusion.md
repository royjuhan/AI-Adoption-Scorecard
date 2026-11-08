# confusion (T-015 스펙: 범주 필드 혼동행렬)

- 상태: 채택(ADR 0005 결정 3, ADR 0010). 2026-11-08 prototyper 초안, 리드가 끝의 추천안 1~11을 채택했다. 카드 "잠정 결정"과 `docs/STATUS.md`에 적었다.
- 작업 카드: `docs/tasks/T-015-confusion-matrix.md`. 빈 곳 6(`docs/cases-public/README.md` 2절 6, 4절 3항 순서 둘째).
- 출발점: 지시문 2절 원칙(엔진에 AI 없음, 실제 자료·회사 자료 금지), 3.4 판정, 12절 예제 [읽음]. `docs/cases-public/README.md` 2절 6 "범주 필드: 오판·놓침·정밀도·재현율. 판정 기준 필드는 case.yaml", 1절에서 빈 곳 6을 단 공개 사례 13건(4, 10, 11, 12, 13, 14, 24, 28, 29, 34, 44, 45, 47번. 검증 유형 C 범주 판정), 4절 [읽음]. `docs/cases-public/gaps.md` 15(집합·순위, 이름 겹침) [읽음]. `case-schema.md`·`metrics.md`·`judgment.md`·`text-check.md`·`synthetic-examples.md`, ADR 0004·0005·0008·0009, `docs/STATUS.md` 잠정 결정 [읽음].
- 확인 대상 커밋: `8b32499`. `src/`·`tests/`·`examples/`는 이 커밋 그대로다. `src/`는 읽지 않았다(ADR 0005 결정 9).
- 확인 환경: Ubuntu 24.04 x64, Python 3.13(oracle·스파이크), .NET SDK 10.0.114(스파이크 `gap-runner`만).
- 근거: oracle(`tools/oracle/`, 이 문서 글만 보고 고쳤다). 이 문서의 블록·SHA-256·셈·판정은 oracle이 낸 것이다 `[T15:O]`. 예제 자료는 스파이크 생성기가 냈다 `[T15:G]`. selftest가 7.2 계획 표로 CSV를 다시 만들어 7.3 블록과 바이트로 맞춘다. 점검 줄은 `python3 -I -B tools/oracle/selftest.py --repo .`이다 `[T15:selftest]`.
- 스파이크: `spikes/gap-06/`(남긴다, `docs/cases-public/README.md` 4절 2). 생성기 `gen.py`, 예제 초안 `defect-labels/`, 지금 엔진에 넣은 사본 `today/`, `gap-runner` 결과 `result.txt`·`out/` `[gap-06]`.
- 고르는 기준(리드 지시): 사용자에게 맞나(쓰기 쉬움) → 그다음 바이트 보존.

## 0. 표기

- `[읽음]`: 커밋된 파일을 읽어서 확인한 사실. `[T15:O]`·`[T15:G]`·`[T15:selftest]`·`[gap-06]`: 위 머리 글. `[T15:S0]`~`[T15:S4]`: 스파이크 단계(10절).
- **미확인**: 실험하지 않았거나 이 환경에서 확인할 수 없는 것.
- 범주 필드: 행마다 범주 하나를 글로 적는 열(결함 종류, 문의 유형, 승인·거절). `compare`에 허용 오차 없이 적은 필드다.
- 범주: 범주 필드 칸의 글(정규화 뒤, `metrics.md` 4.6).
- 관심 범주: 사용자가 놓침·오판을 보고 싶은 범주(`confusion.positive`). 예: 결함 종류 가운데 `긁힘`, `찍힘`.
- 결과 범주: 기준 행과 짝지은 결과 행의 범주. 짝이 없으면(누락) 또는 결과 쪽에 같은 키가 둘 이상이면(`metrics.md` 4.3) "결과 없음"(null)이다.
- 관심 범주 하나에 대해: 맞힘 = 기준 범주와 결과 범주가 둘 다 그것. 놓침 = 기준은 그것인데 결과가 아님(결과 없음 포함). 오판 = 결과는 그것인데 기준이 아님.
- 재현율 = 맞힘 / 기준 행(기준 범주가 그것인 행). 정밀도 = 맞힘 / 결과 행(결과 범주가 그것인 행).
- `CaseDefinition`·`CaseError`는 `case-schema.md`, `CompareMetrics`는 `metrics.md`, `JudgmentEngine`은 `judgment.md`의 이름이다.

## 1. 범위

카드 수용 기준 1~7을 다룬다. 8(테스트 수, CI 녹색, lead_check)은 PR에서 확인한다.

| 카드 수용 기준 | 이 문서 | 질문 |
| --- | --- | --- |
| 1 `case.yaml`에 범주 필드와 관심 범주를 적는 자리 | 4.1 | 1, 2, 3 |
| 2 혼동행렬 셈(오판·놓침·정밀도·재현율), 짝 없는 행, 출력 자리 | 4.2, 3.1 | 2, 4, 5 |
| 3 판정 기준 필드는 `case.yaml`(문턱), 판정 연결 | 5절 | 6, 7, 8 |
| 4 합성 예제 하나 | 7절 | 9, 10 |
| 5 `schema_version` | 3.3 | 5 |
| 6 단계 질문 12개 상한 | 4.1 끝 | — |
| 7 oracle이 새 지표·판정을 낸다. oracle 변경과 새 expected는 PR을 나눈다 | 3절, 5절, 8절, 10절 | 11 |

범위 밖:

- 집합·순위 결과의 정밀도·재현율(빈 곳 15, `gaps.md` 15). 이 카드는 행마다 범주가 하나인 필드다. 이름은 같지만 셈이 다르다.
- 범주마다 다른 문턱, 놓침·오판의 비용 가중. 문턱은 관심 범주 모두에 하나씩이다(질문 6).
- 확률·신뢰도 점수와 문턱 곡선. 결과 자료는 범주 글만 준다.
- 한 행에 범주가 여럿(다중 레이블).
- 범주 이름 묶기(동의어, 대소문자, 띄어쓰기). 칸 글 그대로 본다(4.2 규칙 2).
- 표본 크기·신뢰 구간(빈 곳 16). 4건 중 3건(75.0%)도 셈 그대로 본다.
- 명령의 인자·화면 문구(M2). `--json` 출력이 결과 파일과 같다는 규칙만 적용한다(3.4).

## 2. 입력

- `case.yaml`의 새 최상위 키 `confusion`과 `thresholds`의 새 키 둘(4.1). 모두 선택이다. 없으면 지금과 같다.
- 자료: 지금의 `inputs` 그대로다. 범주 필드는 기준·결과 자료에 다 있는 열이고, `compare`에 이미 적은 텍스트 필드다. 새 파일이 없다.
- CSV 읽기, 파일 찾기, 키 짝짓기, 칸 정규화는 `metrics.md` 4.1~4.3·4.6 그대로다.

## 3. 출력

### 3.1 metrics.json에 더하는 속성

설정했을 때만 쓴다. `confusion`이 없으면 `compare.confusion`이 없다. 그래서 예제 5종 `expected/metrics.json`은 바이트 그대로다 [T15:O].

```json
{
  "schema_version": 1,
  "case": "<케이스 이름>",
  "compare": {
    "...": "metrics.md 3.2 그대로",
    "fields": [],
    "checklist": "text-check.md 3.1 (있을 때만)",
    "confusion": {"field": "<범주 필드>", "classes": ["<범주>"],
                  "cells": [{"reference": "<기준 범주>", "candidate": "<결과 범주> | null", "count": 0}],
                  "positive": [{"class": "<관심 범주>", "reference": 0, "predicted": 0, "hit": 0, "miss": 0,
                                "false_alarm": 0, "recall_pct": 0.0, "precision_pct": 0.0}]},
    "items": []
  }
}
```

- 위 블록은 모양 설명이다. 실제 바이트는 7.4 블록처럼 배열 원소도 한 줄에 하나다.
- 자리: `compare.confusion`은 `fields` 뒤(체크리스트가 있으면 `checklist` 뒤)·`items` 앞. `metrics.md` 3.2 "지표는 `compare` 아래에 둔다"를 따른다 [읽음].

혼동 새 속성 표. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 순서는 defect-labels metrics.json에서 처음 나오는 순서다. `metrics.md` 3.2 표와 `text-check.md` 3.1 표에 있는 경로는 다시 적지 않는다.

| 혼동 새 속성 | 형식 | 뜻 |
| --- | --- | --- |
| `compare.confusion` | 객체 | 혼동행렬(4.2). `confusion`이 있을 때만 |
| `compare.confusion.field` | 문자열 | 범주 필드 이름(`confusion.field`) |
| `compare.confusion.classes` | 문자열 배열 | 범주 목록. 서수 순(4.2 규칙 3) |
| `compare.confusion.cells` | 객체 배열 | 0이 아닌 칸. 4.2 규칙 4 순서 |
| `compare.confusion.cells[]` | 객체 | 칸 하나 |
| `compare.confusion.cells[].reference` | 문자열 | 기준 범주 |
| `compare.confusion.cells[].candidate` | 문자열 또는 null | 결과 범주. 결과 없음이면 null |
| `compare.confusion.cells[].count` | 정수 | 그 칸의 기준 행 수 |
| `compare.confusion.positive` | 객체 배열 | 관심 범주별 셈. `confusion.positive` 순서 |
| `compare.confusion.positive[]` | 객체 | 관심 범주 하나 |
| `compare.confusion.positive[].class` | 문자열 | 관심 범주 |
| `compare.confusion.positive[].reference` | 정수 | 기준 범주가 그것인 행 수 |
| `compare.confusion.positive[].predicted` | 정수 | 결과 범주가 그것인 행 수(기준 행과 짝지은 것만) |
| `compare.confusion.positive[].hit` | 정수 | 맞힘 |
| `compare.confusion.positive[].miss` | 정수 | 놓침 = `reference` − `hit` |
| `compare.confusion.positive[].false_alarm` | 정수 | 오판 = `predicted` − `hit` |
| `compare.confusion.positive[].recall_pct` | 숫자 또는 null | 재현율(%). 소수 첫째 자리(4.2 규칙 6). `reference`가 0이면 null |
| `compare.confusion.positive[].precision_pct` | 숫자 또는 null | 정밀도(%). `predicted`가 0이면 null |

- 쓰지 않는 것: 0인 칸, 관심 범주가 아닌 범주의 재현율·정밀도(칸에서 셀 수 있다), 추가 행의 범주, 칸마다 키 목록(항목은 `items`에 있다).

### 3.2 judgment.json에 더하는 속성

`confusion`이 있을 때만: `thresholds.recall_min`·`thresholds.precision_min`(문턱 셋 뒤, 점수표가 있으면 `score_gap_max` 뒤), `checks`의 `RECALL`·`PRECISION`(5.2).

혼동 판정 새 속성 표. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. `judgment.md` 3.2 표와 `text-check.md` 3.2 표에 있는 경로는 다시 적지 않는다.

| 혼동 판정 새 속성 | 형식 | 뜻 |
| --- | --- | --- |
| `thresholds.recall_min` | 숫자 | 관심 범주 재현율 문턱(%, 5.1). `case.yaml` 값 그대로(자릿수 유지) 또는 기본값. `confusion`이 있을 때만 |
| `thresholds.precision_min` | 숫자 | 관심 범주 정밀도 문턱(%, 5.1). 같다 |

- `checks[].rule`·`rule`에 `RECALL`·`PRECISION`, `checks[].result`에 `no_rows`, `todo[].key`에 `add_class_rows`가 는다. 값이 느는 것이다. 경로는 그대로다.
- 확인은 일곱(`judgment.md` 4.4 다섯 + `RECALL`·`PRECISION`)이다. 점수표도 있으면 여덟(`SCORE` 다음)이다.

### 3.3 바이트 규칙과 `schema_version`

- 바이트 규칙은 `metrics.md` 3.3·`judgment.md` 3.3 그대로다(`Utf8JsonWriter`, 2칸, LF, 끝 LF, BOM 없음, `UnsafeRelaxedJsonEscaping`). 퍼센트는 늘 소수 한 자리 글(`75.0`, `100.0`)이다. 범주 글은 이스케이프 표대로다(한글은 그대로).
- `schema_version`은 metrics.json 1, judgment.json 2 그대로다. 기준은 ADR 0008 결정 1이다: 이미 받던 입력에서 속성을 빼거나 이름·형식·뜻을 바꾸면 올린다. 새로 받는 입력 모양(`confusion`이 있는 `case.yaml`)에서만 출력이 바뀌고, 그 모양은 릴리스된 적이 없다. 열거 값이 느는 것(`RECALL`, `no_rows`, `add_class_rows`)만으로는 올리지 않는다.
- 그래서 migration 커밋이 없다(카드 수용 기준 5). 예제 5종 expected 15개는 바이트 그대로다 [T15:O].
- 대안(추천하지 않음): 늘 `"confusion": null`을 쓴다. 읽는 쪽이 키를 늘 찾을 수 있다. 대신 예제 5종 metrics.json 5개 바이트가 바뀌고 `schema_version`을 올려야 한다(ADR 0004 migration). → 질문 5.

### 3.4 명령과 AI용 입구

- 내부 명령 `compare --json` = metrics.json, `judge --json` = judgment.json(ADR 0004 결정 6 규칙 5) 그대로다. 새 명령은 없다.
- 종료 코드: 4.1 파서 오류는 2(입력 오류). 재현율·정밀도 미달은 오류가 아니라 판정이다(0).

## 4. 규칙

### 4.1 `case.yaml` 키

`case-schema.md` 4.3 최상위 표에 키 하나, 4.5 `thresholds` 표에 키 둘을 더한다(11절, PR (b)). 위치·순서는 상관없다(4.3 그대로).

| 키 | 형식 | 필수 | 규칙 |
| --- | --- | --- | --- |
| `confusion` | 매핑 | 아니오 | `field`(문자열, 필수), `positive`(목록, 필수). 다른 키는 `unknown-field` |
| `confusion.field` | 문자열 | 예 | `compare`에 있는 필드(없으면 `required`). 허용 오차(`tolerance`·`tolerance_pct`)가 없는 필드(있으면 `conflict`) |
| `confusion.positive` | 목록 | 예 | 관심 범주 1개 이상(빈 목록 `required`). 원소는 비지 않은 스칼라 글(`required`), 서수 중복 금지(`duplicate`). 숫자처럼 보이는 글(`1`)도 글로 받는다 |
| `thresholds.recall_min` | 숫자 | 아니오 | 0 이상 100 이하(`range`). `confusion`이 없으면 `required`(경로 `confusion`) |
| `thresholds.precision_min` | 숫자 | 아니오 | 같다 |

- 오류 코드는 `case-schema.md` 4.9의 17개 그대로다. 새 코드가 없다. "언제"가 는다(11절).
- `compare`가 있으려면 `inputs`가 있어야 한다(`case-schema.md` 4.9 `required`). 그래서 `confusion`도 `inputs`가 있어야 쓸 수 있다. 따로 검사하지 않는다.
- 관심 범주는 쓴 글 그대로 칸과 서수로 견준다. 칸은 앞뒤 공백을 떼지만(정규화) 관심 범주 글은 바꾸지 않는다. 따옴표로 앞뒤 공백을 넣은 글(`" 긁힘"`)은 어느 칸과도 맞지 않아 `no_rows`로 드러난다(5.2).
- 단계 질문은 그대로 10개다(4절 4항 상한 12 안). 혼동행렬은 케이스 수준 키라 단계 답을 늘리지 않는다.
- 사용자에게 맞나: 범주 이름은 사용자 자료의 글 그대로(한글 가능). 관심 범주만 적으면 된다. 범주 목록 전체를 적게 하지 않는다(자료에서 센다).

파서 장면. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 바탕은 7.3 `case.yaml` 블록이다. "바꾼 곳"의 두 글(`옛 글` → `새 글`)은 바탕에 꼭 한 번 있는 글을 바꾼다. 글 안의 `\n`은 줄바꿈이다. `(없음)`은 바탕 그대로다.

| case.yaml 바꾼 곳 | 결과 | 코드 | 메시지 |
| --- | --- | --- | --- |
| (없음) | 받음 | — | — |
| `  recall_min: 90\n  precision_min: 80\n` → `` | 받음 | — | — |
| `  positive: [긁힘, 찍힘]\n` → `  positive: [찍힘]\n` | 받음 | — | — |
| `[긁힘, 찍힘]` → `[긁힘, 찍힘, 1]` | 받음 | — | — |
| `  field: result\n` → `  field: line\n` | 오류 | `required` | `confusion.field는 compare에 있는 필드다` |
| `compare:\n  - {field: result}\n` → `` | 오류 | `required` | `confusion.field는 compare에 있는 필드다` |
| `{field: result}` → `{field: result, tolerance: 0}` | 오류 | `conflict` | `confusion.field는 허용 오차가 없는 compare 필드다` |
| `[긁힘, 찍힘]` → `[긁힘, 찍힘, 긁힘]` | 오류 | `duplicate` | `범주가 중복된다 (처음: confusion.positive[1])` |
| `[긁힘, 찍힘]` → `[]` | 오류 | `required` | `범주가 1개 이상 있어야 한다` |
| `[긁힘, 찍힘]` → `[긁힘, ""]` | 오류 | `required` | `값이 비어 있다` |
| `  positive: [긁힘, 찍힘]\n` → `` | 오류 | `required` | `필수 필드가 없다` |
| `  positive: [긁힘, 찍힘]\n` → `  positive: 찍힘\n` | 오류 | `type` | `목록이어야 한다` |
| `  positive: [긁힘, 찍힘]\n` → `  positive: [긁힘, 찍힘]\n  negative: [정상]\n` | 오류 | `unknown-field` | `알 수 없는 필드다` |
| `  recall_min: 90\n` → `  recall_min: 101\n` | 오류 | `range` | `범위 밖이다 (0 이상 100 이하): 101` |
| `confusion:\n  field: result\n  positive: [긁힘, 찍힘]\n` → `` | 오류 | `required` | `recall_min이 있으면 confusion이 있어야 한다` |

- 위치는 `case-schema.md` 4.9 규칙대로다: 값 오류는 그 값, `positive` 없음은 `confusion` 매핑 시작, `confusion` 없음은 `recall_min`·`precision_min` 값(둘 다 있으면 둘 다 오류), 중복은 둘째 값(경로 `confusion.positive[3]`, 1부터). `confusion.field`의 `required`·`conflict`는 그 값이다.
- 메시지 글은 이 문서가 정한다. `range` 메시지 모양(`범위 밖이다 (<범위>): <값>`)은 기존 그대로다.

### 4.2 혼동행렬 셈

혼동행렬은 대조가 짝지은 기준 행을 범주별로 나눠 센 것이다. 대조(`metrics.md` 4.3·4.4)는 그대로다.

규칙:

1. 기준 행마다 결과 범주를 하나 정한다. 결과 쪽에 그 키가 한 번 있으면 그 행의 범주 필드 칸이다. 결과에 없으면(누락) 또는 결과 쪽에 두 번 이상이면(`metrics.md` 4.3, 값 불일치) 결과 없음(null)이다. 결과 쪽 중복에서 대표 행을 고르지 않는다. 어느 행이 답인지 모르기 때문이다(`metrics.md` 13.1과 같은 까닭).
2. 범주는 정규화(`metrics.md` 4.6) 뒤 칸 글이다. 비교는 서수다. 대소문자·NFC·띄어쓰기가 다르면 다른 범주다. 빈 칸도 범주 하나다(문장에는 `(빈 칸)`, `judgment.md` 4.5).
3. 범주 목록(`classes`) = 기준 범주 ∪ 결과 범주(null 빼고) ∪ 관심 범주. 서수 순(UTF-16 단위, `metrics.md` 4.1 정렬과 같다). 추가 행(기준에 없는 키)의 범주는 넣지 않는다.
4. 칸(`cells`) = (기준 범주, 결과 범주) 쌍마다 기준 행 수. 0인 쌍은 쓰지 않는다. 순서는 기준 범주(목록 순) → 결과 범주(목록 순, 결과 없음은 끝)다. 칸 수의 합은 기준 행 수와 같다.
5. 관심 범주마다(`positive` 순서): `reference` = 기준 범주가 그것인 행, `predicted` = 결과 범주가 그것인 행, `hit` = 둘 다 그것인 행, `miss` = `reference` − `hit`, `false_alarm` = `predicted` − `hit`. 결과 없음은 그 기준 범주의 놓침에 들고 어느 정밀도 분모에도 들지 않는다.
6. 재현율 = `hit` / `reference`, 정밀도 = `hit` / `predicted`. 퍼센트 셈은 `metrics.md` 4.8 그대로다(소수 첫째 자리, 0.05는 올림, 정수 셈). 분모가 0이면 null이다. 판정은 퍼센트 글이 아니라 셈으로 문턱과 견준다(5.2).
7. 추가 행은 행렬에 들지 않는다. 그 수는 지금처럼 `counts.extra`이고 검토 항목(`judgment.md` 4.9)이다. 기준 범주가 없어 칸을 정할 수 없다. → 질문 4.
8. 정확도·누락률·검토 항목·몰림·`items`는 그대로다. 범주가 다르면 지금처럼 값 불일치(`outside`)다. `compare`에 다른 필드가 있으면 그 필드는 행렬과 상관없다.
9. 오류가 새로 생기지 않는다. 범주 필드는 `compare` 필드라 열 없음 등은 이미 `metrics.md` 4.7 오류다.

셈 장면. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 바탕은 7.3 defect-labels 파일이다. "바꾼 곳"은 `파일`: `옛 글` → `새 글`(그 파일의 모든 곳)이고, 파일이 다른 묶음은 `; `로 잇는다(`text-check.md` 4.4 장면 표와 같다). "범주"는 `classes`, "칸"은 `기준→결과 수`(결과 없음은 `(결과 없음)`), "관심 범주"는 `범주 기준/결과/맞힘`(= `reference`/`predicted`/`hit`)이다. 빈 범주는 `(빈 칸)`이다.

| 셈 장면 | 바꾼 곳 | 범주 | 칸 | 관심 범주 |
| --- | --- | --- | --- | --- |
| 예제 그대로 | (없음) | `긁힘`, `정상`, `찍힘` | `긁힘→긁힘 4`, `정상→긁힘 1`, `정상→정상 30`, `정상→(결과 없음) 1`, `찍힘→정상 1`, `찍힘→찍힘 3` | `긁힘 4/5/4`, `찍힘 4/3/3` |
| 결과 쪽 중복 | `candidate/parts.csv`: `p19,정상\n` → `p19,정상\np04,찍힘\n` | `긁힘`, `정상`, `찍힘` | `긁힘→긁힘 4`, `정상→긁힘 1`, `정상→정상 30`, `정상→(결과 없음) 1`, `찍힘→정상 1`, `찍힘→찍힘 2`, `찍힘→(결과 없음) 1` | `긁힘 4/5/4`, `찍힘 4/2/2` |
| 결함 행 누락 | `candidate/parts.csv`: `p04,찍힘\n` → `` | `긁힘`, `정상`, `찍힘` | `긁힘→긁힘 4`, `정상→긁힘 1`, `정상→정상 30`, `정상→(결과 없음) 1`, `찍힘→정상 1`, `찍힘→찍힘 2`, `찍힘→(결과 없음) 1` | `긁힘 4/5/4`, `찍힘 4/2/2` |
| 추가 행 | `candidate/parts.csv`: `p19,정상\n` → `p19,정상\np41,긁힘\n` | `긁힘`, `정상`, `찍힘` | `긁힘→긁힘 4`, `정상→긁힘 1`, `정상→정상 30`, `정상→(결과 없음) 1`, `찍힘→정상 1`, `찍힘→찍힘 3` | `긁힘 4/5/4`, `찍힘 4/3/3` |
| 결과에만 있는 범주 | `candidate/parts.csv`: `p21,긁힘` → `p21,깨짐` | `긁힘`, `깨짐`, `정상`, `찍힘` | `긁힘→긁힘 4`, `정상→깨짐 1`, `정상→정상 30`, `정상→(결과 없음) 1`, `찍힘→정상 1`, `찍힘→찍힘 3` | `긁힘 4/4/4`, `찍힘 4/3/3` |
| 기준에 없는 관심 범주 | `case.yaml`: `  positive: [긁힘, 찍힘]\n` → `  positive: [긁힘, 찍힘, 깨짐]\n` | `긁힘`, `깨짐`, `정상`, `찍힘` | `긁힘→긁힘 4`, `정상→긁힘 1`, `정상→정상 30`, `정상→(결과 없음) 1`, `찍힘→정상 1`, `찍힘→찍힘 3` | `긁힘 4/5/4`, `찍힘 4/3/3`, `깨짐 0/0/0` |
| 빈 범주 | `reference/parts.csv`: `p02,A,정상\n` → `p02,A,\n`; `candidate/parts.csv`: `p02,정상\n` → `p02,\n` | `(빈 칸)`, `긁힘`, `정상`, `찍힘` | `(빈 칸)→(빈 칸) 1`, `긁힘→긁힘 4`, `정상→긁힘 1`, `정상→정상 29`, `정상→(결과 없음) 1`, `찍힘→정상 1`, `찍힘→찍힘 3` | `긁힘 4/5/4`, `찍힘 4/3/3` |
| 결과만 빈 칸 | `candidate/parts.csv`: `p02,정상\n` → `p02,\n` | `(빈 칸)`, `긁힘`, `정상`, `찍힘` | `긁힘→긁힘 4`, `정상→(빈 칸) 1`, `정상→긁힘 1`, `정상→정상 29`, `정상→(결과 없음) 1`, `찍힘→정상 1`, `찍힘→찍힘 3` | `긁힘 4/5/4`, `찍힘 4/3/3` |
| 결과 칸 앞뒤 공백 | `candidate/parts.csv`: `p04,찍힘\n` → `p04, 찍힘 \n` | `긁힘`, `정상`, `찍힘` | `긁힘→긁힘 4`, `정상→긁힘 1`, `정상→정상 30`, `정상→(결과 없음) 1`, `찍힘→정상 1`, `찍힘→찍힘 3` | `긁힘 4/5/4`, `찍힘 4/3/3` |
| 결과를 고침 | `candidate/parts.csv`: `p21,긁힘` → `p21,정상`, `p38,정상` → `p38,찍힘`, `p19,정상\n` → `p19,정상\np01,정상\n` | `긁힘`, `정상`, `찍힘` | `긁힘→긁힘 4`, `정상→정상 32`, `찍힘→찍힘 4` | `긁힘 4/4/4`, `찍힘 4/4/4` |

- "결과 쪽 중복": `p04`가 결과에 두 번 나와 값 불일치이고(`metrics.md` 4.3), 행렬에서는 결과 없음이다. 두 행이 모두 `찍힘`이어도 맞힘이 아니다. 정확도 36/40 = 90.0 [T15:O].
- "결함 행 누락"은 "결과 쪽 중복"과 행렬이 같다. 둘 다 그 결함을 결과가 하나로 말하지 못했다.
- "추가 행": `p41`(기준에 없는 키)의 `긁힘`은 행렬·정밀도에 들지 않는다. `counts.extra` 1, 검토 항목 "추가 행 1건"이다.
- "결과에만 있는 범주": 기준에 없는 `깨짐`도 범주 목록에 든다. 그 행은 `긁힘` 오판이 아니라 `깨짐` 결과라 `긁힘` 정밀도가 오른다.
- "기준에 없는 관심 범주": 분모가 0이라 `깨짐`의 재현율·정밀도가 null이다. 판정은 5.2 `no_rows`다.

### 4.3 오류

`CaseError`(`case-schema.md` 4.9)와 대조 오류(`metrics.md` 4.7) 그대로다. 새 코드가 없다.

- 파서 오류는 4.1 표다. 대조에서는 새 오류가 없다(4.2 규칙 9).
- 기준에 없는 관심 범주는 오류가 아니라 판정(`no_rows`, 5.2)이다. 이번 자료에 그 범주가 없을 수 있다(작은 표본, 드문 결함). 이름을 잘못 적은 것일 수도 있어 다음 할 일 문구가 이름 확인을 먼저 말한다(5.5). → 질문 8.
- 사용자에게 맞나: 사람 자료(`case.yaml`)의 모양 결함은 오류로 멈춰 고치게 한다. 자료에서만 알 수 있는 것(범주가 없음, 결과가 틀림)은 셈과 판정이다.

### 4.4 결정론·원본 불변·G1·G2·G3

- 모든 비교는 서수, 셈은 정수, 문턱 비교는 정수와 `decimal`·`InvariantCulture`다. 난수·시각·환경 변수를 쓰지 않는다. `Dictionary` 열거 순서를 출력에 쓰지 않는다(범주 목록은 정렬, 관심 범주는 `case.yaml` 순서). 파일은 읽기만 한다.
- oracle은 문화권·해시 시드를 바꿔도 같은 바이트를 낸다 [T15:selftest 결정론]. defect-labels CSV를 CRLF·CR·BOM·따옴표·끝 줄바꿈 없음·끝 빈 행·칸 앞뒤 공백으로 바꿔도 metrics.json·judgment.json 바이트가 같다 [T15:selftest CSV 변형].
- G2·G3 등록(제안, PR (b) test-engineer): 이름 `confusion-case-valid`, 표본 `TestData/guard/confusion-valid`(= 7.3 defect-labels 파일, expected 빼고. 새 샘플), 어댑터 `CaseParser.ParseDirectory` → `CompareEngine.CompareDirectory` → `JudgmentEngine.Judge`(시간·사람 입력 null) → 출력 폴더의 `metrics.json`, `judgment.json`. `Real_targets_use_case_samples_not_basic`의 정확한 쌍 목록에 한 쌍, 샘플 목록에 `confusion-valid`를 더한다(대상을 더함, 축소 아님). 한글 범주·퍼센트 글이 출력에 들어가므로 R4(문화권)를 본다.

## 5. 판정 (`judgment.md` 4절에 더한다)

`judgment.md`·`text-check.md`의 표·문장은 그대로다. `confusion`이 있을 때만 아래 행이 더해진다. 없으면 판정 바이트가 지금과 같다 [T15:O].

### 5.1 혼동 문턱 기본값

`confusion`이 있고 문턱이 없으면 기본값을 쓰고 `defaults`에 적는다(`judgment.md` 4.2 표 뒤, 점수표가 있으면 `score_gap_max` 뒤 순서). 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다.

| 혼동 문턱 | 기본값 | 근거 |
| --- | --- | --- |
| `recall_min` | `95` | 지시문에 값이 없다. `accuracy_min` 기본값(`judgment.md` 4.2)과 같다. 관심 범주는 놓치면 비싼 범주라 정확도보다 느슨하게 두지 않는다. 기본값 표시로 확인을 받는다 |
| `precision_min` | `95` | 같은 까닭. 오판이 사람 일만 늘리는 경우 사용자가 낮춰 적는다(defect-labels는 80) |

- `confirm_thresholds` 문구의 `{defaults}`에 `recall_min 95, precision_min 95`가 든다.
- 문턱 하나가 관심 범주 모두에 쓰인다. 범주마다 다른 문턱은 범위 밖이다(질문 6).
- `confusion`이 없으면 `thresholds`·`defaults` 모양이 그대로다(예제 바이트 그대로).

### 5.2 확인 `RECALL`·`PRECISION`

`judgment.md` 4.4 다섯 뒤(점수표가 있으면 `SCORE` 뒤)에 이 순서로 둔다. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. "읽는 문턱"이 기본값이면 확인 문장 끝에 기본값 표시(`judgment.md` 4.6)를 붙인다.

| 혼동 확인 | 결과 | 걸리는 조건 | 읽는 문턱 | 확인 문장 틀 |
| --- | --- | --- | --- | --- |
| `RECALL` | `met` | 모든 관심 범주가 `reference` > 0이고 `hit × 100 ≥ recall_min × reference` | `recall_min` | `관심 범주의 재현율이 모두 문턱 {recall_min}% 이상이다: {recall_all}.` |
| `RECALL` | `not_met` | `reference` > 0이고 `hit × 100 < recall_min × reference`인 관심 범주가 있음 | `recall_min` | `재현율이 문턱 {recall_min}%에 못 미치는 관심 범주가 있다: {recall_low}.` |
| `RECALL` | `no_rows` | `not_met`이 아니고 `reference` = 0인 관심 범주가 있음 | `recall_min` | `기준 자료에 행이 없는 관심 범주가 있어 재현율을 잴 수 없다: {no_rows}.` |
| `PRECISION` | `met` | 그 밖(`predicted` = 0인 관심 범주는 걸리지 않는다) | `precision_min` | `관심 범주의 정밀도가 모두 문턱 {precision_min}% 이상이다: {precision_all}.` |
| `PRECISION` | `not_met` | `predicted` > 0이고 `hit × 100 < precision_min × predicted`인 관심 범주가 있음 | `precision_min` | `정밀도가 문턱 {precision_min}%에 못 미치는 관심 범주가 있다: {precision_low}.` |

- 셈은 정수와 `decimal`이다(곱셈, 나눗셈 없음, 반올림 없음). 같으면 `met`이다(경계 포함). 퍼센트 글(`75.0`)은 문장에만 쓴다.
- `not_met`이 `no_rows`보다 앞이다. 둘 다 있으면 결과는 `not_met`이고, 행이 없는 관심 범주는 다음 할 일 `add_class_rows`(5.5)에 남는다.
- 결과 0건(`predicted` = 0)인 관심 범주는 정밀도를 잴 수 없지만 오판도 없다. 걸리지 않는다. 그 범주에 기준 행이 있으면 재현율 0이라 `RECALL`이 걸리고, 없으면 `no_rows`다. → 질문 8.

문장 값:

- `{recall_min}`·`{precision_min}`: 쓴 문턱 글(`case.yaml` 글 그대로 또는 `95`).
- `{recall_all}`: `reference` > 0인 관심 범주(`positive` 순서)의 범주 글을 `, `로 잇는다. `{recall_low}`: 그 가운데 미달인 것.
- `{precision_all}`: 모든 관심 범주(`positive` 순서). `{precision_low}`: `predicted` > 0이고 미달인 것.
- `{recall_low_classes}`·`{precision_low_classes}`·`{no_rows}`: 범주 이름만 `, `로 잇는다.
- 범주 이름은 `judgment.md` 4.5와 같은 방식으로 보인다(제어 문자 `\uXXXX`, 64자를 넘으면 자르고 `…`, 빈 값 `(빈 칸)`).

범주 글. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. `{…}`는 3.1 `positive[]`의 값이다.

| 범주 글 | 조건 | 모양 |
| --- | --- | --- |
| 재현율 | — | `{class} {recall_pct}%({hit}/{reference}, 놓침 {miss}건)` |
| 정밀도 | `predicted` > 0 | `{class} {precision_pct}%({hit}/{predicted}, 오판 {false_alarm}건)` |
| 정밀도 | `predicted` = 0 | `{class} 결과 0건` |

### 5.3 판정 행

`judgment.md` 4.3 표에 더한다. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. "자리"는 `judgment.md` 4.3의 순서다. `3 뒤`는 순서 3(`MISSING`) 뒤·순서 4 앞, `7 뒤`는 순서 7 뒤·순서 8(`PASS`) 앞이다.

| 자리 | 근거 키 | 확인 결과 | 판정 | 판정 문장 틀 |
| --- | --- | --- | --- | --- |
| `3 뒤` | `RECALL` | `not_met` | `hold` | `관심 범주 {recall_low_classes}의 재현율이 문턱 {recall_min}%에 못 미치므로 보류.` |
| `3 뒤` | `PRECISION` | `not_met` | `hold` | `관심 범주 {precision_low_classes}의 정밀도가 문턱 {precision_min}%에 못 미치므로 보류.` |
| `7 뒤` | `RECALL` | `no_rows` | `conditional` | `정확도·누락률은 문턱을 충족하나 기준 자료에 관심 범주 {no_rows}의 행이 없어 재현율을 잴 수 없으므로 조건부.` |

- 재현율·정밀도 미달은 보류다. 지시문 3.4 "보류 = 정확도 미달"과 같은 갈래(품질 미달)다. 전체 정확도가 문턱을 넘어도 관심 범주에서 미달이면 보류다. 이 카드가 메우는 빈 곳이다(6.1 "예제 그대로", 10절).
- 자리 `3 뒤`: 정확도·누락률(전체)이 먼저다. 재현율은 정확도를 관심 범주로 좁힌 것이라 바로 뒤에 둔다. 사람이 설명할 수 없다고 적은 항목(순서 4)보다 앞이다. 둘 다 보류이고, 혼동 문장이 어느 범주인지 더 구체적이다.
- `RECALL`이 `PRECISION`보다 앞이다. 놓침은 결함이 그대로 나가고, 오판은 사람이 다시 보는 일을 늘린다. 놓침이 더 비싸다(defect-labels `intake.md`).
- `7 뒤` 조건부: 관심 범주의 행이 기준 자료에 없으면 그 범주를 잰 적이 없다. 통과가 아니다. 시간·몰림·안 본 검토 항목(순서 5~7)이 더 구체적이라 그 뒤다. 확인은 `checks`에 늘 남는다.
- 점수표도 있으면: `3 뒤`(혼동 보류) → 순서 4 → `4 뒤`(`SCORE` 보류, `text-check.md` 5.3) → 순서 5~7 → `7 뒤` → 8.
- → 질문 7, 8.

### 5.4 읽은 문턱

판정 문장의 기본값 표시(`judgment.md` 4.6). 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. "바탕"은 그 행이 이 문서 없이 읽는 문턱이다: `순서 3`·`순서 8`은 `judgment.md` 4.6 표의 그 순서(점수표가 있으면 `text-check.md` 5.4 표 대조 행), `그 행`은 걸린 행이 `judgment.md` 4.6·`text-check.md` 5.4대로 읽는 문턱이다. "더하는 문턱"을 바탕 뒤에 더한다. 자리 `4~8`은 순서 4, `4 뒤`(`SCORE`), 순서 5~8의 모든 행이다.

| 자리 | 근거 키 | 확인 결과 | 바탕 | 더하는 문턱 |
| --- | --- | --- | --- | --- |
| `3 뒤` | `RECALL` | `not_met` | `순서 3` | `recall_min` |
| `3 뒤` | `PRECISION` | `not_met` | `순서 3` | `recall_min`, `precision_min` |
| `4~8` | — | — | `그 행` | `recall_min`, `precision_min` |
| `7 뒤` | `RECALL` | `no_rows` | `순서 8` | `recall_min`, `precision_min` |

- `PRECISION` 보류는 `RECALL`을 지나 왔다(재현율은 충족). 그래서 `recall_min`도 읽었다. 순서 4 이후 행은 혼동 확인 둘을 모두 지나 왔다.
- `confusion`이 없으면 `judgment.md` 4.6·`text-check.md` 5.4 그대로다.

### 5.5 다음 할 일

`judgment.md` 4.7 표에 더한다. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 자리는 `add_group` 뒤(점수표가 있으면 `rescore_blind` 뒤), `confirm_thresholds` 앞이다.

| 혼동 할 일 | 나오는 때 | 문구 |
| --- | --- | --- |
| `add_class_rows` | `reference` = 0인 관심 범주가 있음(`RECALL` 결과와 상관없이) | `기준 자료에 관심 범주 {no_rows}의 행이 없다. case.yaml confusion.positive의 이름을 확인하고, 맞으면 그 범주의 행을 기준 자료와 결과 자료에 넣어 재현율을 잰다.` |

- 재현율·정밀도 미달에는 새 할 일이 없다. 틀린 행은 지금처럼 검토 항목(`review_items`)에 있다.

### 5.6 정의와 대조 결과

`judgment.md` 5절 표에 더한다(거절 = `ArgumentException`).

- `confusion`이 있으면 대조 결과가 `measured`이고, `compare.confusion.field`와 관심 범주 목록(순서)이 정의와 같아야 한다.
- `confusion`이 없으면 대조 결과에 `confusion`이 없어야 한다.

## 6. 예시

### 6.1 판정 장면

테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 바탕은 7.3 defect-labels 파일이다. "바꾼 곳"의 모양은 4.2 셈 장면 표와 같다. 시간·사람 입력은 null이다. "확인 결과"는 `checks` 순서다.

| 판정 장면 | 바꾼 곳 | 판정 | 근거 키 | 확인 결과 | 다음 할 일 |
| --- | --- | --- | --- | --- | --- |
| 예제 그대로 | (없음) | `hold` | `RECALL` | `met`, `met`, `not_reviewed`, `off`, `spread`, `not_met`, `met` | `review_items` |
| 재현율 문턱 75 | `case.yaml`: `  recall_min: 90\n` → `  recall_min: 75\n` | `conditional` | `UNEXPLAINED` | `met`, `met`, `not_reviewed`, `off`, `spread`, `met`, `met` | `review_items` |
| 정밀도 문턱 81 | `case.yaml`: `  recall_min: 90\n  precision_min: 80\n` → `  recall_min: 75\n  precision_min: 81\n` | `hold` | `PRECISION` | `met`, `met`, `not_reviewed`, `off`, `spread`, `met`, `not_met` | `review_items` |
| 혼동 문턱 기본값 | `case.yaml`: `  recall_min: 90\n  precision_min: 80\n` → `` | `hold` | `RECALL` | `met`, `met`, `not_reviewed`, `off`, `spread`, `not_met`, `not_met` | `review_items`, `confirm_thresholds` |
| 정확도 미달이 먼저 | `case.yaml`: `  accuracy_min: 90\n` → `  accuracy_min: 95\n` | `hold` | `ACCURACY` | `not_met`, `met`, `not_reviewed`, `off`, `spread`, `not_met`, `met` | `review_items` |
| 유형 열 없음 | `case.yaml`: `  group: line\n` → `` | `hold` | `RECALL` | `met`, `met`, `not_reviewed`, `off`, `off`, `not_met`, `met` | `review_items`, `add_group` |
| 기준에 없는 관심 범주 | `case.yaml`: `  positive: [긁힘, 찍힘]\n` → `  positive: [긁힘, 찍힘, 깨짐]\n` | `hold` | `RECALL` | `met`, `met`, `not_reviewed`, `off`, `spread`, `not_met`, `met` | `review_items`, `add_class_rows` |
| 기준에 없는 관심 범주, 재현율 문턱 75 | `case.yaml`: `  positive: [긁힘, 찍힘]\n` → `  positive: [긁힘, 찍힘, 깨짐]\n`, `  recall_min: 90\n` → `  recall_min: 75\n` | `conditional` | `UNEXPLAINED` | `met`, `met`, `not_reviewed`, `off`, `spread`, `no_rows`, `met` | `review_items`, `add_class_rows` |
| 기준에 없는 범주를 결과에 적음 | `case.yaml`: `  positive: [긁힘, 찍힘]\n` → `  positive: [긁힘, 찍힘, 깨짐]\n`, `  recall_min: 90\n` → `  recall_min: 75\n`; `candidate/parts.csv`: `p21,긁힘` → `p21,깨짐` | `hold` | `PRECISION` | `met`, `met`, `not_reviewed`, `off`, `spread`, `no_rows`, `not_met` | `review_items`, `add_class_rows` |
| 결과를 고침 | `candidate/parts.csv`: `p21,긁힘` → `p21,정상`, `p38,정상` → `p38,찍힘`, `p19,정상\n` → `p19,정상\np01,정상\n` | `pass` | `PASS` | `met`, `met`, `none`, `off`, `few`, `met`, `met` | — |
| 결과를 고침, 기준에 없는 관심 범주 | `candidate/parts.csv`: `p21,긁힘` → `p21,정상`, `p38,정상` → `p38,찍힘`, `p19,정상\n` → `p19,정상\np01,정상\n`; `case.yaml`: `  positive: [긁힘, 찍힘]\n` → `  positive: [긁힘, 찍힘, 깨짐]\n` | `conditional` | `RECALL` | `met`, `met`, `none`, `off`, `few`, `no_rows`, `met` | `add_class_rows` |
| 결과를 고침, 관심 범주 하나가 기준에 없음 | `candidate/parts.csv`: `p21,긁힘` → `p21,정상`, `p38,정상` → `p38,찍힘`, `p19,정상\n` → `p19,정상\np01,정상\n`; `case.yaml`: `  positive: [긁힘, 찍힘]\n` → `  positive: [깨짐]\n` | `conditional` | `RECALL` | `met`, `met`, `none`, `off`, `few`, `no_rows`, `met` | `add_class_rows` |

- "예제 그대로": 정확도 92.5%가 문턱 90을 넘는다. `찍힘` 4건 중 1건을 `정상`으로 적어 재현율 75.0%가 문턱 90에 못 미친다. 판정 문장은 `관심 범주 찍힘의 재현율이 문턱 90%에 못 미치므로 보류.`다 [T15:O].
- "예제 그대로"에 사람 입력 "모두 설명"(빈 목록)을 주어도 보류 `RECALL`이다. 지금 엔진은 같은 자료(`confusion` 없는 사본)로 통과 `PASS`다 [gap-06]. 이 카드가 메우는 빈 곳이다.
- "재현율 문턱 75": 75.0%가 경계에서 `met`이다(3 × 100 = 75 × 4). 정밀도 80.0%도 문턱 80 경계에서 `met`이다(4 × 100 = 80 × 5).
- "혼동 문턱 기본값": 두 문턱이 95가 되어 둘 다 `not_met`이다. `RECALL`이 먼저다. `defaults`는 `recall_min`, `precision_min`이다. 판정 문장은 `관심 범주 찍힘의 재현율이 문턱 95%에 못 미치므로 보류. (기본값 사용, 확인 필요)`다 [T15:O].
- "기준에 없는 관심 범주": `찍힘` 미달(`not_met`)이 `no_rows`보다 앞이다. `깨짐`은 할 일 `add_class_rows`에 남는다.
- "기준에 없는 범주를 결과에 적음": `p21`(기준 `정상`)을 `깨짐`으로 적었다. `깨짐`의 정밀도 0.0%(0/1, 오판 1건)가 걸린다. 재현율은 `no_rows`다.
- "결과를 고침, 기준에 없는 관심 범주": 판정 문장은 `정확도·누락률은 문턱을 충족하나 기준 자료에 관심 범주 깨짐의 행이 없어 재현율을 잴 수 없으므로 조건부.`다 [T15:O].

### 6.2 defect-labels 결과

- 기준 40행(`정상` 32, `긁힘` 4, `찍힘` 4), 결과 39행. 누락 1(`p01`, `정상`), 값 불일치 2(`p21` `정상`→`긁힘`, `p38` `찍힘`→`정상`). 정확도 37/40 = 92.5, 누락률 1/40 = 2.5 [T15:O].
- 혼동행렬: `긁힘` 재현율 100.0(4/4), 정밀도 80.0(4/5, 오판 1건). `찍힘` 재현율 75.0(3/4, 놓침 1건), 정밀도 100.0(3/3) [T15:O].
- 판정: 보류 `RECALL`. 정확도로는 문턱을 넘지만 결함 `찍힘`을 4건 중 1건 놓쳤다. 불일치 3건이 `line` A·B에 섞여 몰림은 `spread`다.
- 생성기가 결함 하나를 놓치고(`찍힘`→`정상`), 정상 하나를 결함으로 적고(`정상`→`긁힘`), 정상 하나를 뺐다(7.2). 놓침과 오판이 정확도에서는 똑같이 불일치 하나로 보인다.

## 7. 예제 defect-labels

### 7.1 만드는 방법과 확정 절차

1. 기존 예제 5종은 바꾸지 않는다. 범주 필드가 있는 예제(form-fill `item`)에 `confusion`을 더하면 expected 2개가 바뀌고, 모두 일치라 놓침·오판이 보이지 않는다. 새 예제 `defect-labels`를 더한다. → 질문 9.
2. 자료 파일(`reference/`, `candidate/`)은 합성 생성기(7.2)가 만든다(지시문 2절 10). `case.yaml`·`answers.yaml`·`intake.md`는 손으로 쓴 글이다(`synthetic-examples.md` 4.8·4.9와 같다).
3. expected 블록 셋(7.4)은 oracle이 7.3 파일에서 낸 바이트다 [T15:O]. 엔진 출력이 아니다. C# 엔진은 PR (b)에서 이 블록에 맞춘다(지시문 2절 11 "기대값을 먼저").
4. PR (a)(이 스펙·oracle)에는 `examples/defect-labels/`가 없다. PR (b)에서 test-engineer가 7.3·7.4 블록을 바이트 그대로 `examples/defect-labels/`로 옮긴다. 기존 expected 15개는 그대로라 migration 커밋이 없다. 새 expected 셋은 새 파일(A)이라 regression-guard `expected-added` 규칙만 받는다: base oracle(이 PR (a)가 머지된 main)이 재현해야 한다 [읽음 `tools/ci/README.md`].
5. oracle-and-expected: PR (b)는 oracle을 바꾸지 않는다. PR (b)의 base oracle이 새 expected 셋을 재현한다 [T15:selftest PR (b) 모의: 18 MATCH].
6. `examples/.gitattributes`(`* -text`)가 새 파일에도 걸린다. 한글 CSV도 `\r` 없음 검사와 실제 값 패턴 검사(`synthetic-examples.md` 4.10 표 9종)에 걸리지 않는다(키 `p` + 숫자 2자리) [T15:S3].

새 예제 표. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 이 표의 예제는 `examples/`에 없어도 된다(PR (b) 전). 있으면 파일이 7.3·7.4 블록과 같아야 한다.

| 새 예제 | inputs.group | 판정 | 근거 키 | 확인 일곱 | 다음 할 일 | 검토 항목 |
| --- | --- | --- | --- | --- | --- | --- |
| `defect-labels` | `line` | `hold` | `RECALL` | `met`, `met`, `not_reviewed`, `off`, `spread`, `not_met`, `met` | `review_items` | 불일치 3건 |

예제 파일. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 블록은 7.3·7.4에 이 순서로 있다.

| 예제 파일 | 만드는 곳 |
| --- | --- |
| `case.yaml` | 손으로 쓴 글(7.3) |
| `answers.yaml` | 손으로 쓴 글(7.3). 단계·답은 `case.yaml`과 같다 |
| `intake.md` | 손으로 쓴 글(7.3) |
| `reference/parts.csv` | 생성기(7.2) |
| `candidate/parts.csv` | 생성기(7.2) |
| `expected/classification.json` | oracle(7.4) |
| `expected/metrics.json` | oracle(7.4) |
| `expected/judgment.json` | oracle(7.4) |

### 7.2 합성 생성기

`synthetic-examples.md` 4.2의 SplitMix64와 `NextBelow`를 그대로 쓴다. 범주 생성기는 그 생성기와 다른 것이다(숫자 열이 아니라 범주 글을 고르고 놓침·오판을 심는다). PR (b)에서 core-dev가 Core에 둔다(제안 이름 `Scorecard.Core.Synthetic.SyntheticClasses`). 테스트가 다시 만들어 7.3 블록과 바이트로 비교한다(T-005·T-014 방식). → 질문 10.

계획. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 목록은 `, `로 잇고 순서가 뜻이다. `값×N`은 같은 값 N개다.

| 계획 | 값 |
| --- | --- |
| 시드 | `9` |
| 행 수 | `40` |
| 키 | `part_id`, `p` + 2자리(`p01`~`p40`) |
| 유형 열 | `line` |
| 유형 | `A`, `B` |
| 범주 열 | `result` |
| 범주 뽑기 | `정상×8`, `긁힘`, `찍힘` |
| 놓침 | `찍힘` → `정상` |
| 오판 | `정상` → `긁힘` |
| 누락 | `정상` |
| 시드 조건 | `긁힘` = `4`, `찍힘` ≥ `2` |

뽑는 순서(한 번에 `NextBelow`를 한 번 쓴다):

1. 행마다(키 순서) 유형 `유형[NextBelow(2)]`, 범주 `범주 뽑기[NextBelow(10)]`(이 순서).
2. 놓칠 행: 범주가 `찍힘`인 행(키 순서) 목록에서 `NextBelow(수)`번째. 결과 범주를 `정상`으로 적는다. 목록이 비면 그 시드는 조건을 만족하지 않는다.
3. 오판 행: 범주가 `정상`인 행 목록에서 `NextBelow(수)`번째. 결과 범주를 `긁힘`으로 적는다.
4. 누락 행: 범주가 `정상`이고 오판 행이 아닌 행 목록에서 `NextBelow(수)`번째. 결과 자료에서 뺀다.
5. 결과 행 순서: 누락 행을 뺀 39행(키 순서)을 Fisher–Yates로 섞는다(i = 38..1마다 `NextBelow(i + 1)` 자리와 바꿈, `synthetic-examples.md` 4.5 7과 같다).

파일 모양:

- `reference/parts.csv`: `part_id,line,result`, 키 순서. `candidate/parts.csv`: `part_id,result`, 5의 순서.
- CSV 바이트: UTF-8(BOM 없음), LF, 끝 LF, 머리 행 먼저, 따옴표 없음. 칸은 비지 않고, 앞뒤 공백·`,`·`"`·LF·CR·탭이 없다(어기면 예외). `text-check.md` 7.2와 같다(한글을 받는다).
- 시드: 조건을 만족하는 가장 작은 값이다(`synthetic-examples.md`와 같다). 조건: `긁힘`이 꼭 4행, `찍힘`이 2행 이상. `긁힘` 4행이면 오판 1건이 정밀도 80.0%로 딱 떨어져 문턱 80의 경계(`met`)를 예제가 보인다. 시드 0~8은 조건을 만족하지 않는다 [T15:G].
- 같은 시드면 같은 바이트다. 두 번 돌려 `diff`가 없다 [T15:S1].

### 7.3 예제 파일

`examples/defect-labels/case.yaml` (SHA-256 `3d5c4630964472106f7d17b95e10bcf523ee485c2eb185f65dc2aa6620de05d2`, 1,475바이트):

```yaml
# 공개 합성 예제. 실제 업무·값 없음. 범주 결과(부품 결함 종류)를 혼동행렬로 잰다(T-015).
# 단계·답은 answers.yaml과 같다. reference/·candidate/는 합성 생성기로 만들었다.
name: defect-labels
task: 부품 사진마다 결함 종류를 적는다. AI가 먼저 적고 사람이 결함 건을 다시 본다
steps:
  - name: 사진 모으기
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: daily, batch_size: 40, exception_rate: 0, current_actor: program, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 결함 종류 적기
    answers: {input_kind: image, rule_expressible: no, frequency: daily, batch_size: 40, exception_rate: 20, current_actor: ai_each_time, failure_impact: external, existing_feature: no, human_signoff: no}
  - name: 결함 건 확인
    answers: {input_kind: image, rule_expressible: no, frequency: daily, batch_size: 8, exception_rate: 10, current_actor: human, failure_impact: external, existing_feature: no, human_signoff: yes}
inputs:
  reference: reference/*.csv
  candidate: candidate/*.csv
  key: part_id
  group: line
compare:
  - {field: result}
confusion:
  field: result
  positive: [긁힘, 찍힘]
# 시간 측정 자료가 없는 예제다. 시간 조건은 끄고 정확도·누락률과 재현율·정밀도를 본다.
thresholds:
  accuracy_min: 90
  missing_max: 5
  time_must_drop: false
  recall_min: 90
  precision_min: 80
```

`examples/defect-labels/answers.yaml` (SHA-256 `eaee54bfb5b8392c2618dadd30b833566780cc33acf22f2e885307aecc1aa894`, 913바이트):

```yaml
# 공개 합성 예제. 실제 업무·값 없음. 범주 결과(T-015).
name: defect-labels
task: 부품 사진마다 결함 종류를 적는다. AI가 먼저 적고 사람이 결함 건을 다시 본다
steps:
  - name: 사진 모으기
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: daily, batch_size: 40, exception_rate: 0, current_actor: program, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 결함 종류 적기
    answers: {input_kind: image, rule_expressible: no, frequency: daily, batch_size: 40, exception_rate: 20, current_actor: ai_each_time, failure_impact: external, existing_feature: no, human_signoff: no}
  - name: 결함 건 확인
    answers: {input_kind: image, rule_expressible: no, frequency: daily, batch_size: 8, exception_rate: 10, current_actor: human, failure_impact: external, existing_feature: no, human_signoff: yes}
```

`examples/defect-labels/intake.md` (SHA-256 `0b2ea8ad48261c1dc0b051bad7bbe8e9ee014b4a3fb2a00d1d7836a59eab5fef`, 600바이트):

```text
부품 사진을 보고 결함 종류를 적는다. 하루 40개쯤 찍는다.
결함 종류는 정상, 긁힘, 찍힘 셋이다.
지금은 AI가 먼저 적고, 사람이 결함으로 적힌 건을 다시 본다.

정상이 대부분이라 전체 정확도는 높게 나온다.
그런데 결함을 정상으로 적으면(놓침) 그대로 나간다. 정상을 결함으로 적으면(오판) 다시 보는 일이 는다.
그래서 결함 종류마다 놓침과 오판을 따로 보고 싶다.

사람이 확정한 결함 종류 표를 기준 자료로, AI가 적은 표를 결과 자료로 넣었다.
```

`examples/defect-labels/reference/parts.csv` (SHA-256 `6791f8d5877033f4617ab2a031dfdaad35b723eef9bd490a6d0b6226f85d65a2`, 540바이트):

```csv
part_id,line,result
p01,B,정상
p02,A,정상
p03,A,정상
p04,B,찍힘
p05,A,정상
p06,B,정상
p07,B,정상
p08,B,긁힘
p09,B,정상
p10,B,정상
p11,B,정상
p12,A,정상
p13,A,정상
p14,B,정상
p15,B,정상
p16,A,정상
p17,A,긁힘
p18,A,찍힘
p19,B,정상
p20,A,긁힘
p21,A,정상
p22,A,정상
p23,B,긁힘
p24,A,정상
p25,A,정상
p26,B,정상
p27,B,정상
p28,B,정상
p29,A,정상
p30,B,정상
p31,A,정상
p32,A,정상
p33,A,정상
p34,A,찍힘
p35,B,정상
p36,B,정상
p37,B,정상
p38,B,찍힘
p39,B,정상
p40,A,정상
```

`examples/defect-labels/candidate/parts.csv` (SHA-256 `bbac4167468b554e18db3d3d0d3f83cc80be34b47f261ea6c793a93faa9aa707`, 444바이트):

```csv
part_id,result
p19,정상
p39,정상
p02,정상
p22,정상
p25,정상
p35,정상
p40,정상
p24,정상
p17,긁힘
p08,긁힘
p15,정상
p36,정상
p16,정상
p29,정상
p05,정상
p32,정상
p12,정상
p14,정상
p09,정상
p38,정상
p31,정상
p18,찍힘
p37,정상
p23,긁힘
p30,정상
p33,정상
p13,정상
p04,찍힘
p28,정상
p27,정상
p07,정상
p10,정상
p06,정상
p11,정상
p34,찍힘
p26,정상
p20,긁힘
p03,정상
p21,긁힘
```

### 7.4 expected

`examples/defect-labels/expected/classification.json` (SHA-256 `13c6e2a79ae6fcf4d30ba629aac973f2a68dbf8fa1411521cb725be978ac125e`, 1,790바이트):

```json
{
  "schema_version": 1,
  "case": "defect-labels",
  "steps": [
    {
      "number": 1,
      "name": "사진 모으기",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "②",
          "rule": "ELSE",
          "reason": "판단 기준을 글로 쓸 수 있고(yes), 입력이 고정 양식이며(fixed_form), 매일 반복되므로 규칙 기반 소프트웨어로 분류."
        }
      ]
    },
    {
      "number": 2,
      "name": "결함 종류 적기",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "③",
          "rule": "Q2",
          "reason": "판단 기준을 글로 쓸 수 없으므로(no) 실행 시 AI 보조 + 사람 확인으로 분류. 자동 승인은 하지 않는다."
        },
        {
          "kind": "human_gate",
          "assignee": "④",
          "rule": "GATE",
          "reason": "틀리면 외부에 영향이 있으므로(external) 이 단계 뒤에 사람 게이트를 둔다."
        }
      ]
    },
    {
      "number": 3,
      "name": "결함 건 확인",
      "defaults": [
        "minutes_per_item"
      ],
      "checks": [
        "measure_minutes_per_item"
      ],
      "ai_overuse_candidate": false,
      "rows": [
        {
          "kind": "step",
          "assignee": "④",
          "rule": "SIGNOFF",
          "reason": "사람이 최종 확인·서명하는 단계이므로(yes) 사람이 하는 것으로 분류."
        }
      ]
    }
  ]
}
```

`examples/defect-labels/expected/metrics.json` (SHA-256 `2de046f15c7ae7aae82d5eccc0b429b0ef32d150bac9eb927f383bcbe98bd651`, 3,159바이트):

```json
{
  "schema_version": 1,
  "case": "defect-labels",
  "compare": {
    "status": "measured",
    "reason": null,
    "key": "part_id",
    "reference_files": [
      "reference/parts.csv"
    ],
    "candidate_files": [
      "candidate/parts.csv"
    ],
    "counts": {
      "reference": 40,
      "candidate": 39,
      "match": 37,
      "mismatch": 2,
      "missing": 1,
      "extra": 0,
      "within": 0
    },
    "accuracy_pct": 92.5,
    "missing_rate_pct": 2.5,
    "fields": [
      {
        "field": "result",
        "kind": "text",
        "tolerance": null,
        "tolerance_pct": null,
        "compared": 39,
        "outside": 2,
        "within": 0,
        "not_number": 0
      }
    ],
    "confusion": {
      "field": "result",
      "classes": [
        "긁힘",
        "정상",
        "찍힘"
      ],
      "cells": [
        {
          "reference": "긁힘",
          "candidate": "긁힘",
          "count": 4
        },
        {
          "reference": "정상",
          "candidate": "긁힘",
          "count": 1
        },
        {
          "reference": "정상",
          "candidate": "정상",
          "count": 30
        },
        {
          "reference": "정상",
          "candidate": null,
          "count": 1
        },
        {
          "reference": "찍힘",
          "candidate": "정상",
          "count": 1
        },
        {
          "reference": "찍힘",
          "candidate": "찍힘",
          "count": 3
        }
      ],
      "positive": [
        {
          "class": "긁힘",
          "reference": 4,
          "predicted": 5,
          "hit": 4,
          "miss": 0,
          "false_alarm": 1,
          "recall_pct": 100.0,
          "precision_pct": 80.0
        },
        {
          "class": "찍힘",
          "reference": 4,
          "predicted": 3,
          "hit": 3,
          "miss": 1,
          "false_alarm": 0,
          "recall_pct": 75.0,
          "precision_pct": 100.0
        }
      ]
    },
    "items": [
      {
        "key": "p01",
        "status": "missing",
        "reference": {
          "file": "reference/parts.csv",
          "line": 2
        },
        "candidate": null,
        "differences": []
      },
      {
        "key": "p21",
        "status": "mismatch",
        "reference": {
          "file": "reference/parts.csv",
          "line": 22
        },
        "candidate": {
          "file": "candidate/parts.csv",
          "line": 40
        },
        "differences": [
          {
            "field": "result",
            "result": "outside",
            "reference": "정상",
            "candidate": "긁힘"
          }
        ]
      },
      {
        "key": "p38",
        "status": "mismatch",
        "reference": {
          "file": "reference/parts.csv",
          "line": 39
        },
        "candidate": {
          "file": "candidate/parts.csv",
          "line": 21
        },
        "differences": [
          {
            "field": "result",
            "result": "outside",
            "reference": "찍힘",
            "candidate": "정상"
          }
        ]
      }
    ]
  }
}
```

`examples/defect-labels/expected/judgment.json` (SHA-256 `199e1416235c2d63854c1174cc75559e0ebd8be4f7d202e6ea583ecb2aa8449d`, 1,621바이트):

```json
{
  "schema_version": 2,
  "case": "defect-labels",
  "verdict": "hold",
  "rule": "RECALL",
  "reason": "관심 범주 찍힘의 재현율이 문턱 90%에 못 미치므로 보류.",
  "thresholds": {
    "accuracy_min": 90,
    "missing_max": 5,
    "time_must_drop": false,
    "recall_min": 90,
    "precision_min": 80
  },
  "defaults": [],
  "checks": [
    {
      "rule": "ACCURACY",
      "result": "met",
      "reason": "정확도 92.5%(37/40)가 문턱 90% 이상이다."
    },
    {
      "rule": "MISSING",
      "result": "met",
      "reason": "누락률 2.5%(1/40)가 문턱 5% 이하다."
    },
    {
      "rule": "UNEXPLAINED",
      "result": "not_reviewed",
      "reason": "검토할 항목 3건(불일치 3건)이 설명되는지 사람이 아직 보지 않았다."
    },
    {
      "rule": "TIME",
      "result": "off",
      "reason": "시간 조건을 쓰지 않는다(time_must_drop: false)."
    },
    {
      "rule": "FOCUS",
      "result": "spread",
      "reason": "불일치 3건이 line 열의 한 값에 몰리지 않았다."
    },
    {
      "rule": "RECALL",
      "result": "not_met",
      "reason": "재현율이 문턱 90%에 못 미치는 관심 범주가 있다: 찍힘 75.0%(3/4, 놓침 1건)."
    },
    {
      "rule": "PRECISION",
      "result": "met",
      "reason": "관심 범주의 정밀도가 모두 문턱 80% 이상이다: 긁힘 80.0%(4/5, 오판 1건), 찍힘 100.0%(3/3, 오판 0건)."
    }
  ],
  "todo": [
    {
      "key": "review_items",
      "text": "검토할 항목 3건(불일치 3건)을 보고 설명이 되는지 적는다."
    }
  ]
}
```

## 8. 테스트로 옮기는 방법 (PR (b))

- 폴더 `tests/Scorecard.Tests/Confusion/`, 네임스페이스 `Scorecard.Tests.Confusion`. Trait `Spec` = `confusion`. 표 읽기는 `SpecDocument.Table`(경로 상수 `docs/spec/confusion.md` 추가), 블록은 `SpecDocument.BlockAfter`(SHA-256 줄 뒤).

test-engineer(`tests/`, `examples/`):

| 테스트 | 확인 | 카드 수용 기준 |
| --- | --- | --- |
| 파서(4.1) | 머리 행 `\| case.yaml 바꾼 곳 \| 결과 \| 코드 \| 메시지 \|`. 행마다 받음 또는 코드·메시지 | 1 |
| 셈(4.2) | 머리 행 `\| 셈 장면 \| 바꾼 곳 \| 범주 \| 칸 \| 관심 범주 \|`. 행마다 `classes`·`cells`·`positive`. 칸 합 = 기준 행, `miss`·`false_alarm`·퍼센트 = 4.2 규칙 5·6 | 2 |
| 새 속성(3.1·3.2) | 머리 행 `\| 혼동 새 속성 \| 형식 \| 뜻 \|`, `\| 혼동 판정 새 속성 \| 형식 \| 뜻 \|`. defect-labels 출력의 새 경로 = 표(순서) | 2, 3 |
| 판정(5절) | 머리 행 `\| 혼동 문턱 \| 기본값 \| 근거 \|`, `\| 혼동 확인 \| 결과 \| 걸리는 조건 \| 읽는 문턱 \| 확인 문장 틀 \|`, `\| 범주 글 \| 조건 \| 모양 \|`, `\| 자리 \| 근거 키 \| 확인 결과 \| 판정 \| 판정 문장 틀 \|`, `\| 자리 \| 근거 키 \| 확인 결과 \| 바탕 \| 더하는 문턱 \|`, `\| 혼동 할 일 \| 나오는 때 \| 문구 \|` = Core 상수 | 3 |
| 판정 장면(6.1) | 머리 행 `\| 판정 장면 \| 바꾼 곳 \| 판정 \| 근거 키 \| 확인 결과 \| 다음 할 일 \|`. 6.1 글머리의 판정 문장 셋, `defaults`, 사람 입력 "모두 설명" | 3 |
| 골든 | `examples/defect-labels/` → 파서 → 대조 → 판정 → 세 파일이 expected와 바이트까지 같다 | 4 |
| 블록 = 파일 | 7.3·7.4 블록·SHA-256 줄 = `examples/defect-labels/` 파일. 머리 행 `\| 새 예제 \| inputs.group \| 판정 \| 근거 키 \| 확인 일곱 \| …`, `\| 예제 파일 \| 만드는 곳 \|` | 4 |
| 생성기(7.2) | 머리 행 `\| 계획 \| 값 \|`로 Core 생성기를 돌려 7.3 CSV 둘과 바이트까지 같다 | 4 |
| 설정 없음 | `confusion`·문턱 둘을 지운 사본은 새 속성이 없고 확인이 다섯이다 | 5 |
| 결정론 | de-DE·tr-TR, 같은 입력 두 번, CSV 변형 7종(4.4) | — |
| G2·G3 | `confusion-case-valid`(4.4) | — |
| 기존 목록 | 예제 이름 목록(`ClassificationInput.Examples` 등)에 `defect-labels`. 대상을 더한다(축소 아님) | 4 |

core-dev(`src/Scorecard.Core/`):

1. 파서: `CaseDefinition`에 `Confusion`(위치 매개변수 끝, 기본값 null. 제안 형식 `CaseConfusion(string Field, IReadOnlyList<string> Positive)`), `CaseThresholds`에 `RecallMin`·`PrecisionMin`(기본값 null, 쓴 글 유지). 4.1 검사. 기존 생성 코드가 그대로 컴파일된다(T-012 `Group`, T-014 `Checklist`와 같은 방식).
2. 대조: 4.2. 새 공개 형식 `ConfusionSummary`(제안: `Field`, `Classes`, `Cells`(`ConfusionCell(Reference, Candidate?, Count)`), `Positive`(`ClassScore(Class, Reference, Predicted, Hit, Miss, FalseAlarm, RecallPct?, PrecisionPct?)`)). `CompareMetrics`에 `Confusion`(끝, 기본값 null). `MetricsJson.Render`가 3.1 자리에 쓴다. 범주 목록은 `string.CompareOrdinal` 정렬.
3. 판정: 5절. `JudgmentRules.cs`에 5.1~5.5 상수(범주 글 표 포함). `JudgmentJson`이 3.2를 쓴다. 정의 검사(5.6).
4. 생성기: 7.2 `SyntheticClasses`(`internal` SplitMix64 재사용).
5. 규칙 값은 `internal` 한 파일씩(`classification.md` 4.9 방식). 실행 중에 문서를 읽지 않는다.

- 시작 전 main 테스트 수 이상, 삭제·Skip 없음(카드 수용 기준 8). 바꾸는 기존 테스트는 목록에 예제·대상을 더하는 것뿐이다.
- `tests/`·`src/`에 `tools/oracle` 글자를 쓰지 않는다(regression-guard `ci-tamper`). PR (b)는 `tools/oracle/`을 바꾸지 않는다(`oracle-and-expected`).
- 이 스펙(PR (a))은 기존 C# 테스트가 읽는 표·블록·줄을 바꾸지 않는다. 다른 스펙에는 끝에 가리키는 절만 더한다(11절).

## 9. 미확인 목록

1. GitHub ubuntu·windows 러너에서 새 테스트가 녹색인지(PR (b)).
2. 실제 업무 범주 글의 흔들림(대소문자, 띄어쓰기, 같은 뜻 다른 이름). 서수 비교라 다른 범주가 된다. 사람은 `classes` 목록과 "결과에만 있는 범주"로 본다. 이름 묶기 규칙은 다음 카드에서 정한다.
3. 관심 범주마다 다른 문턱, 놓침과 오판의 비용 가중(질문 6). 공개 사례 13건이 어떤 문턱을 쓰는지 사례 글에 없다.
4. 관심 범주의 기준 행이 적을 때(예: 4건) 재현율을 믿을 수 있는지(빈 곳 16).
5. 점수표(`scores`)와 혼동행렬을 같이 쓴 예제가 없다. 5.3·5.4의 점수표 자리는 oracle 코드와 이 문서 글로만 맞췄다. selftest 장면이 없다.
6. 결과 쪽 중복을 결과 없음으로 보는 것(4.2 규칙 1)이 사용자에게 맞는지. 두 행이 같은 범주여도 맞힘이 아니다(4.2 "결과 쪽 중복"). `metrics.md` 13.1과 같은 까닭이지만 실제 자료에서 얼마나 나오는지 모른다.
7. `check` 명령·스코어카드 화면이 행렬을 어떻게 보이는지(M2).
8. 집합·순위 결과(빈 곳 15)가 같은 `RECALL`·`PRECISION` 이름을 쓸지. 그 카드에서 정한다.

## 10. 스파이크 단계 목록

스파이크 `spikes/gap-06/`은 남긴다(`docs/cases-public/README.md` 4절 2). 저장소에서 다시 확인하는 길은 selftest다: 7.2 계획 표로 CSV를 다시 만들어 7.3 블록과 맞추고, 7.4 블록 = oracle 출력, PR (b) 모의, 표 = oracle 상수, 4.2·6.1 장면을 본다.

| 단계 | 한 일 |
| --- | --- |
| S0 | 시드 찾기(`python3 -I spikes/gap-06/gen.py --find-seed` → `9`) |
| S1 | 생성기 두 번(`gen.py 9 <폴더>`) → `diff` 없음 |
| S2 | oracle로 expected 셋. `answers.yaml`·`case.yaml` 분류 같음 |
| S3 | 예제 파일(줄·경로)에 실제 값 패턴(`synthetic-examples.md` 4.10 표 9종) 0건. 회사 이름 검사(`spikes/cases-public/name-check.sh`, `spikes/gap-runner/check-names.py`) 0건 |
| S4 | `spikes/gap-runner/run-gaps.sh <작업폴더> gap-06`: 커밋 `8b32499`의 엔진 사본에 `today`(`confusion` 없는 사본)와 `defect-labels`를 넣었다. 결과는 `spikes/gap-06/result.txt` |

S4 결과 [gap-06]:

- `today --reviewed`(사람이 모두 설명): 통과 `PASS`. 정확도 92.5%가 문턱 90을 넘고, `찍힘` 재현율 75%는 어디에도 나오지 않는다. 이 카드가 메우는 빈 곳이다.
- `today`(사람 입력 없음): 조건부 `UNEXPLAINED`. 불일치 3건이 `정상`→`긁힘`, `찍힘`→`정상`, 누락으로만 보인다.
- `defect-labels`: 막힘. `case.yaml:19:1: unknown-field confusion`, `case.yaml:27:3: unknown-field thresholds.recall_min`, `case.yaml:28:3: unknown-field thresholds.precision_min`.

## 11. 다른 문서 개정 (PR (b)에서 옮길 곳)

이 PR (a)에서는 다른 스펙의 표·블록을 바꾸지 않는다. C# 테스트와 selftest가 그 표를 읽기 때문이다. 각 문서 끝에 짧은 "T-015 개정" 절을 두어 이 문서를 가리킨다. 규칙의 자리는 이 문서다. → 질문 11.

1. `case-schema.md`: 4.3 최상위 표에 `confusion` 행, 4.5 `thresholds` 표에 `recall_min`·`precision_min` 행, 4.9 표의 `required`·`conflict`·`duplicate`·`range`·`type`·`unknown-field` "언제"에 4.1 경우. T-014와 같이 다음 정리 카드에서 옮긴다(표 머리가 바뀌면 기존 테스트가 같이 바뀐다).
2. `metrics.md`: 3.2 표는 그대로다(table-compare 경로 순서를 보는 테스트가 있다). 새 경로는 이 문서 3.1 표다.
3. `judgment.md`: 4.3·4.4·4.6·4.7 표는 그대로다. 이 문서 5절 표를 더해서 읽는다. 7.1 예제 표에 defect-labels를 넣지 않는다(확인이 일곱이라 "확인 다섯" 칸과 맞지 않는다). 이 문서 7.1 새 예제 표가 그 자리다.
4. `synthetic-examples.md`: 범주 생성기는 이 문서 7.2다. 4.3 계획 표에 넣지 않는다(열 모양이 다르다).
5. 지시문(`docs/README-instructions.md`, ADR 0010): 3.4 끝에 범주 결과 문단을 더했다(PR (a), T-014 문장 결과 문단 바로 뒤). 표는 바꾸지 않았다. 12절 예제 표의 여섯째 행 `defect-labels`(범주 결과, 보류)와 제목은 T-014 몫과 같이 정리 카드에서 고친다(테스트가 그 표의 행마다 `examples/`를 읽어, 예제가 없는 PR (a)에서 넣으면 실패한다). 2절(8·10 포함)은 바꾸지 않는다.

## 12. ADR 0010 요지

이 PR (a)는 `tools/oracle/`과 지시문 3.4를 바꾼다. regression-guard `ci-changed`가 새 ADR과 `docs/STATUS.md` "원칙 변경" 행(새 ADR 번호)을 요구한다 [읽음 `tools/ci/README.md` C]. 전문은 `docs/decisions/0010-confusion-matrix.md`다.

- 제목: 범주 필드 혼동행렬(`confusion`)과 판정 `RECALL`·`PRECISION`.
- 배경: 빈 곳 6. 정확도는 놓침과 오판을 한 줄로 섞는다. 정상이 대부분인 자료에서 결함을 놓쳐도 정확도가 문턱을 넘어 통과한다(10절 S4).
- 결정 1: `case.yaml` 최상위 `confusion`(`field`, `positive`). 범주 필드는 허용 오차 없는 `compare` 필드다. 단계 질문은 늘지 않는다. 새 오류 코드는 없다.
- 결정 2: 혼동행렬은 대조가 짝지은 기준 행을 범주별로 센 것이다. 누락·결과 쪽 중복은 결과 없음, 추가 행은 행렬 밖.
- 결정 3: 출력은 설정했을 때만 있는 속성이다. `schema_version`은 그대로(ADR 0008 결정 1). migration 없음.
- 결정 4: 판정 기준 필드는 `case.yaml` `thresholds.recall_min`·`precision_min`(기본 95). 확인 `RECALL`·`PRECISION`, 미달은 순서 3 뒤 보류, 관심 범주의 행이 없으면 순서 7 뒤 조건부 + `add_class_rows`. 기존 표·문장·예제 바이트는 그대로다.
- 결정 5: oracle에 `confusion.py`를 더하고 `oracle_case.py`·`compare.py`·`judge.py`·`selftest.py`를 넓힌다(이 문서 글만 보고). `check.py` 계약은 그대로다.
- 결정 6: 새 예제 `defect-labels`와 범주 생성기는 PR (b). 지시문 3.4에 범주 결과 문단(11절 5, PR (a)). 12절 예제 표는 정리 카드.
- 영향: 기존 expected 15개 바이트 그대로. 가드를 약하게 하는 변경은 없다. selftest는 점검이 늘기만 한다.
- 남은 위험: 범주 글 흔들림(9절 2), 문턱 하나(9절 3), 작은 표본(9절 4), 점수표와 같이 쓴 예제 없음(9절 5).
- STATUS: 원칙 변경 0010 행(T-015 (a)), 잠정 결정 T-015 행 1~11.

## 승인 질문 (ADR 0005: 추천안 채택)

추천안은 "사용자에게 맞나 → 그다음 바이트 보존" 순서로 골랐다. "ADR 0004 기준"은 ADR 0005 전이었다면 누가 정했을지다(`judgment.md` 12.6과 같다). 질문은 11개다.

1. **자리(4.1).** ADR 0004 기준: juhan 직접(`case.yaml` 스키마).
   - 추천: 최상위 `confusion`(`field`, `positive`). `compare`·`inputs`는 그대로. 단계 질문은 늘지 않는다.
   - 대안: `compare` 항목에 `kind: class`와 `positive`를 둔다. 필드마다 혼동행렬을 둘 수 있지만, 허용 오차 키와 섞여 항목 모양이 길어지고 한 케이스에 행렬이 여럿이면 판정 문장·문턱이 필드마다 갈린다.
2. **범주 필드와 범주 값(4.1, 4.2 규칙 2).** ADR 0004 기준: 리드.
   - 추천: 허용 오차 없는 `compare` 필드만. 범주는 정규화 뒤 칸 글 그대로(서수). 빈 칸도 범주. 대조와 같은 글 비교라 정확도와 행렬이 어긋나지 않는다.
   - 대안: 범주 이름을 묶는다(대소문자 무시, 동의어 표). 사용자가 묶음을 또 적어야 하고, 묶음이 틀리면 놓침이 조용히 사라진다.
3. **관심 범주(4.1).** ADR 0004 기준: 리드.
   - 추천: `positive`를 적게 한다(1개 이상). 재현율·정밀도·판정은 관심 범주만. 행렬(`cells`)은 모든 범주를 센다. 정상이 대부분인 자료에서 `정상`의 재현율은 늘 높아 판정에 쓸모가 적다.
   - 대안: 모든 범주의 재현율·정밀도를 판정에 쓴다. 적을 것은 줄지만, 드문 범주 하나(1건)가 늘 판정을 흔든다.
4. **짝 없는 행(4.2 규칙 1·7).** ADR 0004 기준: 리드.
   - 추천: 누락·결과 쪽 중복은 결과 없음(놓침에 듦, 정밀도 분모 밖). 추가 행은 행렬 밖(`counts.extra`·검토 항목에 그대로).
   - 대안: 추가 행의 결과 범주가 관심 범주면 오판으로 센다. 기준 범주가 없어 칸을 정할 수 없고, 칸 합이 기준 행 수와 어긋난다.
5. **출력과 `schema_version`(3.1~3.3).** ADR 0004 기준: juhan 직접(expected 모양).
   - 추천: metrics.json `compare.confusion`(`fields`·`checklist` 뒤), judgment.json 문턱 둘·확인 둘. 설정했을 때만 있는 속성. 0이 아닌 칸만 목록으로. 퍼센트는 `metrics.md` 4.8 셈, 분모 0은 null. `schema_version` 그대로(ADR 0008 결정 1). 기존 expected 15개 바이트 그대로, migration 없음.
   - 대안: 늘 `"confusion": null` + `schema_version` 올림(3.3), 또는 2차원 배열 행렬(0인 칸도 씀). 2차원 배열은 범주가 많으면 길고 null 열의 자리가 어색하다.
6. **문턱(5.1).** ADR 0004 기준: juhan 직접(판정 기준).
   - 추천: `thresholds.recall_min`·`precision_min`, 기본값 95·95(`accuracy_min` 기본과 같음), 관심 범주 모두에 하나씩. 기본값이면 표시 + `confirm_thresholds`.
   - 대안: 범주마다 문턱(`positive: [{class: 긁힘, recall_min: 90}]`). 맞춤은 되지만 모양이 커진다. 필요해지면 다음 카드에서 범주별 값을 더한다(지금 모양과 함께 쓸 수 있다).
7. **판정 자리(5.3).** ADR 0004 기준: 리드.
   - 추천: 확인 `RECALL`·`PRECISION`을 다섯(점수표가 있으면 `SCORE`) 뒤에. 미달은 보류, 자리는 순서 3(`MISSING`) 뒤·순서 4 앞, `RECALL`이 먼저. 기존 행과 문장은 그대로.
   - 대안 1: 미달을 조건부로. 지시문 3.4 "보류 = 정확도 미달"과 같은 갈래(품질 미달)라 보류가 맞다. 조건부면 결함을 놓친 결과가 "추가 확인"으로만 보인다.
   - 대안 2: 정확도보다 앞(순서 2 앞). 전체 정확도 미달이 더 넓은 문제라 먼저 보이는 게 낫다.
8. **분모 0(5.2·5.3).** ADR 0004 기준: 리드.
   - 추천: 관심 범주의 기준 행이 없으면 `RECALL` `no_rows` → 순서 7 뒤 조건부 + `add_class_rows`(문구가 이름 확인을 먼저 말한다). 결과 0건인 관심 범주의 정밀도는 걸리지 않는다(오판이 없다).
   - 대안 1: 파서·대조 오류. 이번 자료에 드문 결함이 없을 수도 있어 멈추면 다른 결과까지 안 보인다.
   - 대안 2: 그 범주를 건너뛴다(`met`). 관심 범주 이름을 잘못 적어도 통과해 놓침이 사라진다.
9. **예제(7.1).** ADR 0004 기준: juhan 직접(예제 추가).
   - 추천: 기존 예제 그대로, 새 예제 `defect-labels`(결함 종류, 정확도 92.5% 충족 + `찍힘` 재현율 75.0% → 보류 `RECALL`).
   - 대안: form-fill `item`에 `confusion`을 더한다. 모두 일치라 놓침·오판이 없고 expected 2개가 바뀐다.
10. **합성 생성기(7.2).** ADR 0004 기준: 리드.
    - 추천: 범주 생성기를 Core에 두고(T-005·T-014 방식) 테스트가 다시 만들어 블록과 비교한다.
    - 대안: 스파이크 생성기만 두고 블록을 정답으로. 지시문 2절 10 "합성 생성기로만"을 테스트가 확인하지 못한다.
11. **문서 자리(11절).** ADR 0004 기준: 리드.
    - 추천: 규칙의 자리는 이 문서. 다른 스펙의 표는 바꾸지 않고 "T-015 개정" 절에서 가리킨다. `case-schema.md` 표 옮기기는 T-014 몫과 같이 다음 정리 카드에서.
    - 대안: `judgment.md`·`metrics.md` 표에 행을 넣는다. 표를 읽는 기존 테스트·selftest가 같이 바뀌고, PR (b)는 oracle을 바꿀 수 없다.

## 채택

- ADR 0005 결정 3에 따라 리드가 추천안 1~11을 채택했다(ADR 0010). 카드 "잠정 결정"과 `docs/STATUS.md` 잠정 결정 T-015 행에 적었다.
