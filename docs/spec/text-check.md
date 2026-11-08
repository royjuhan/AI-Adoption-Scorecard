# text-check (T-014 스펙: 문장 결과 검증)

- 상태: 채택(ADR 0005 결정 3, ADR 0009). 2026-11-08 prototyper 초안, 리드가 끝의 추천안 1~12를 채택했다. PR (a) 리뷰 뒤 고친 곳(5.1~5.5, 6.1, 4.3 규칙 2)은 질문 13~15, 리뷰 2차 뒤 고친 곳(4.2 규칙 10·체크리스트 장면 표, 4.4 `source_column`, 4.5 체크리스트 코드 표, 6.1, 9절 8)은 질문 16~18, 리뷰 3차 뒤 고친 곳(4.4 조각, 4.5 `overlap` 문구, 9절 9~11)은 질문 19~20, [재검토](ADR 0005) 뒤 고친 곳(4.4 `source_pattern`·장면 표, 4.3 규칙 2·4.5 `blind`, 6.1, 9절 8·10·11·12)은 질문 21~22이고 같이 채택했다. 카드 "잠정 결정"과 `docs/STATUS.md`에 적었다.
- 작업 카드: `docs/tasks/T-014-text-result-check.md`. 빈 곳 2(`docs/cases-public/README.md` 2절 2, 4절 3항 순서 첫째).
- 출발점: 지시문 2절 원칙(엔진에 AI 없음, 실제 자료·회사 자료 금지), 3.4 판정, 12절 예제 [읽음]. `docs/cases-public/README.md` 1절의 빈 곳 2 사례 요약과 4절 [읽음]. `docs/cases-public/gaps.md` 16(표본 크기)·17(기준 신뢰도) [읽음]. `case-schema.md`·`metrics.md`·`judgment.md`·`synthetic-examples.md`, ADR 0004·0005·0007·0008, `docs/STATUS.md` 잠정 결정 [읽음].
- 확인 대상 커밋: `61ea067`. `src/`·`tests/`·`examples/`는 이 커밋 그대로다. `src/`는 읽지 않았다(ADR 0005 결정 9).
- 확인 환경: Ubuntu 24.04 x64, Python 3.13(oracle·스파이크), .NET SDK 10.0.114(기존 테스트만).
- 근거: oracle(`tools/oracle/`, 이 문서 글만 보고 고쳤다). 이 문서의 블록·SHA-256·판정은 oracle이 낸 것이다 `[T14:O]`. 예제 자료는 스파이크 생성기가 냈다 `[T14:G]`. selftest가 7.2 계획 표로 CSV를 다시 만들어 7.3 블록과 바이트로 맞춘다. 점검 줄은 `python3 -I -B tools/oracle/selftest.py --repo .`이다 `[T14:selftest]`.
- 스파이크: `spikes/T-014/` (머지 대상 아님. 이 PR에 없다). 첫 판은 스파이크가 생성기·oracle 출력을 이 문서에 채웠다. 리뷰 뒤 고친 곳은 손으로 고쳤다. 저장소에서 다시 확인하는 길은 selftest다: 7.2 계획 표 → CSV = 7.3 블록, 7.4 블록 = oracle 출력, 표 = oracle 상수, 6.1 장면.
- 고르는 기준(리드 지시): 사용자에게 맞나(쓰기 쉬움) → 그다음 바이트 보존.

## 0. 표기

- `[읽음]`: 커밋된 파일을 읽어서 확인한 사실. `[T14:O]`·`[T14:G]`·`[T14:selftest]`: 위 머리 글. `[T14:S0]`~`[T14:S6]`: 스파이크 단계(10절, 기록).
- **미확인**: 실험하지 않았거나 이 환경에서 확인할 수 없는 것.
- 문장 결과: 정답 표가 없는 결과(요약, 답변 초안, 보고서 문장). 결과 자료의 한 칸에 문장이 들어 있다.
- 체크리스트 대조(길 1): 결과 문장마다 꼭 들어갈 항목이 있는지 문자열 규칙으로 본다(4.2).
- 가림 점수표(길 2): 사람이 AI 결과와 사람 결과를 누가 썼는지 모르고 매긴 점수(4.3). 엔진은 모으기만 한다.
- 가림 키: 채점자가 보는 결과 이름. 출처 표: 가림 키 → 건·출처(`ai`·`human`). 채점자에게 주지 않는 표다.
- 길: 판정이 쓰는 자료. "표 대조" = 대조가 `measured`(compare 필드 또는 체크리스트)이고 점수표가 있음. "점수표만" = 대조가 미검증이고 점수표가 있음. 미검증 이유가 `no_compare_fields`(`inputs`를 적었는데 `compare`·`checklist`가 없음)여도 점수표만이다. 그때는 통과가 아니다(5.3).
- `CaseDefinition`·`CaseError`는 `case-schema.md`, `CompareMetrics`·`CsvRow`는 `metrics.md`, `JudgmentEngine`은 `judgment.md`의 이름이다.

## 1. 범위

카드 수용 기준 1~7을 다룬다. 8(테스트 수, CI 녹색, lead_check)은 PR에서 확인한다.

| 카드 수용 기준 | 이 문서 | 질문 |
| --- | --- | --- |
| 1 `case.yaml`에 두 길의 자리. 정답 표 없는 케이스가 미검증이 아니다 | 4.1, 5.3 | 1, 8 |
| 2 체크리스트: 항목·통과 기준·건별 빠진 항목·판정 셈. 문자열 규칙만 | 4.2, 3.1 | 2, 3 |
| 3 점수표: CSV 모양, 평균·분포·AI 대 사람, 판정 연결, 엔진이 볼 수 있는 가림만 | 4.3, 4.4, 5절 | 4, 5, 6, 7 |
| 4 합성 예제 하나 | 7절 | 10, 11 |
| 5 `schema_version` | 3.3 | 9 |
| 6 단계 질문 12개 상한 | 4.1 끝 | — |
| 7 oracle이 새 지표·판정을 낸다. oracle 변경과 새 expected는 PR을 나눈다 | 3절, 5절, 8절, 10절 | 12 |

범위 밖:

- 결과 문장의 뜻을 읽는 것(요약이 맞는지, 말투가 맞는지). 엔진에 자연어 처리가 없다(CLAUDE.md 절대 원칙). 사람이 점수표로 본다.
- 채점자 사이 일치도(카파 등)와 기준 두 벌(gaps.md 17). 점수표는 채점자별 평균만 보인다(3.1 `by_rater`). 일치도는 빈 곳 17 카드다.
- 표본 크기·신뢰 구간(gaps.md 16). 평균 비교는 셈 그대로다. 빈 곳 16 카드다.
- 가림 키 순서로 출처를 짐작하는 것(예: 늘 AI 쪽 키가 앞). 우연과 가르는 숫자가 지시문에 없다(**미확인**, 9절).
- 사람 결과 문장 파일. 점수표는 가림 키만 본다. 문장 원문은 엔진이 읽지 않는다.
- 명령의 인자·화면 문구(M2). `--json` 출력이 결과 파일과 같다는 규칙만 적용한다(3.4).

## 2. 입력

- `case.yaml`의 새 최상위 키 둘(4.1): `checklist`(길 1), `scores`(길 2). 둘 다 선택이다. 둘 다 없으면 지금과 같다.
- 길 1 자료: 지금의 `inputs` 그대로다. 기준 자료(`reference/`)는 사람이 정한 건 목록과 건마다 들어갈 값(예: 문의 번호)이다. 결과 자료(`candidate/`)는 AI 결과 문장이 한 칸에 든 표다. 키 열로 짝을 짓는다(`metrics.md` 4.3).
- 길 2 자료: 점수표 CSV(채점자가 채운다)와 출처 표 CSV(채점을 준비한 사람이 가진다). 모양은 4.3.
- CSV 읽기는 `metrics.md` 4.2 그대로다(UTF-8, BOM 하나 뗌, LF·CRLF·CR, `,`, RFC 4180 따옴표, 칸 정규화 4.6). 문장 칸에 `,`·줄바꿈이 있으면 따옴표로 감싼다(Excel이 그렇게 저장한다).
- 파일 찾기는 `metrics.md` 4.1 패턴 규칙 그대로다. 경로는 케이스 폴더 기준 상대 경로다.

## 3. 출력

### 3.1 metrics.json에 더하는 속성

설정했을 때만 쓴다. `checklist`가 없으면 `compare.checklist`·`items[].missing_items`가 없고, `scores`가 없으면 최상위 `scores`가 없다. 그래서 예제 4종 `expected/metrics.json`은 바이트 그대로다 [T14:O].

```json
{
  "schema_version": 1,
  "case": "<케이스 이름>",
  "compare": {
    "...": "metrics.md 3.2 그대로",
    "fields": [],
    "checklist": {"text": "<문장 열>", "entries": [{"item": "<항목>", "column": "<기준 열> | null", "phrases": ["<문구>"],
                  "required": 0, "found": 0, "absent": 0}]},
    "items": [{"...": "metrics.md 3.2 그대로", "differences": [], "missing_items": ["<빠진 항목>"]}]
  },
  "scores": {
    "sheet_files": ["<경로>"], "sources_files": ["<경로>"], "min": 1, "max": 5,
    "items": 0, "raters": ["<채점자>"], "ratings": 0,
    "blind": {"status": "kept | revealed", "findings": [{"rule": "<4.4 규칙>", "file": "<경로>", "line": 0, "value": "<값>"}]},
    "sources": [{"source": "ai | human", "results": 0, "ratings": 0, "sum": 0, "mean": 0.00,
                 "distribution": [{"score": 1, "count": 0}]}],
    "paired": {"better": 0, "same": 0, "worse": 0},
    "by_rater": [{"rater": "<채점자>", "ai_ratings": 0, "ai_mean": 0.00, "human_ratings": 0, "human_mean": 0.00}]
  }
}
```

- 위 블록은 모양 설명이다. 실제 바이트는 7.4 블록처럼 배열 원소도 한 줄에 하나다.
- 자리: `compare.checklist`는 `fields` 뒤·`items` 앞. `missing_items`는 항목의 `differences` 뒤. `scores`는 `compare` 뒤(최상위 마지막). `metrics.md` 3.2 "지표는 `compare` 아래에 둔다. 다른 지표는 같은 파일의 다른 속성으로"를 따른다 [읽음].

새 속성 표. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 순서는 reply-drafts metrics.json에서 처음 나오는 순서다. `metrics.md` 3.2 표에 있는 경로는 다시 적지 않는다.

| 새 속성 | 형식 | 뜻 |
| --- | --- | --- |
| `compare.checklist` | 객체 | 체크리스트 대조(4.2). `checklist`가 있을 때만 |
| `compare.checklist.text` | 문자열 | 결과 자료의 문장 열 이름(`checklist.text`) |
| `compare.checklist.entries` | 객체 배열 | 항목별 셈. `checklist.items` 순서 |
| `compare.checklist.entries[]` | 객체 | 항목 하나 |
| `compare.checklist.entries[].item` | 문자열 | 항목 이름 |
| `compare.checklist.entries[].column` | 문자열 또는 null | 기준 열 이름(`column` 항목). `any` 항목이면 null |
| `compare.checklist.entries[].phrases` | 문자열 배열 | `any` 문구(쓴 글 그대로). `column` 항목이면 `[]` |
| `compare.checklist.entries[].required` | 정수 | 이 항목을 요구한 결과 행 수 = `found` + `absent` |
| `compare.checklist.entries[].found` | 정수 | 문구가 있었던 결과 행 수 |
| `compare.checklist.entries[].absent` | 정수 | 문구가 없었던 결과 행 수 |
| `compare.items[].missing_items` | 문자열 배열 | 그 결과 행에서 빠진 항목(`checklist.items` 순서). 누락·추가 항목은 `[]`. `checklist`가 있을 때만 |
| `scores` | 객체 | 가림 점수표 집계(4.3). `scores`가 있을 때만 |
| `scores.sheet_files` | 문자열 배열 | 읽은 점수표. 경로 서수 순 |
| `scores.sources_files` | 문자열 배열 | 읽은 출처 표. 경로 서수 순 |
| `scores.min` | 정수 | 점수 하한(`scores.min`) |
| `scores.max` | 정수 | 점수 상한 |
| `scores.items` | 정수 | 출처 표의 건 수 |
| `scores.raters` | 문자열 배열 | 채점자. 서수 순 |
| `scores.ratings` | 정수 | 점수 수(점수표 데이터 행 수) |
| `scores.blind` | 객체 | 가림 확인(4.4) |
| `scores.blind.status` | 문자열 | `kept`(발견 없음) 또는 `revealed` |
| `scores.blind.findings` | 객체 배열 | 출처가 드러난 곳. 4.4 순서. 없으면 `[]` |
| `scores.sources` | 객체 배열 | 출처별 집계. `ai` → `human` |
| `scores.sources[]` | 객체 | 출처 하나 |
| `scores.sources[].source` | 문자열 | `ai` 또는 `human` |
| `scores.sources[].results` | 정수 | 그 출처의 결과 수(출처 표 행 수) |
| `scores.sources[].ratings` | 정수 | 그 출처의 점수 수 |
| `scores.sources[].sum` | 정수 | 점수 합 |
| `scores.sources[].mean` | 숫자 | 평균. 소수 둘째 자리(4.3) |
| `scores.sources[].distribution` | 객체 배열 | 점수별 수. `min`부터 `max`까지 하나씩 |
| `scores.sources[].distribution[]` | 객체 | 점수 하나 |
| `scores.sources[].distribution[].score` | 정수 | 점수 |
| `scores.sources[].distribution[].count` | 정수 | 그 점수를 받은 수 |
| `scores.paired` | 객체 | 건마다 AI 평균과 사람 평균 비교(4.3) |
| `scores.paired.better` | 정수 | AI가 높은 건 수 |
| `scores.paired.same` | 정수 | 같은 건 수 |
| `scores.paired.worse` | 정수 | AI가 낮은 건 수 |
| `scores.by_rater` | 객체 배열 | 채점자별 평균. `raters` 순서 |
| `scores.by_rater[]` | 객체 | 채점자 하나 |
| `scores.by_rater[].rater` | 문자열 | 채점자 |
| `scores.by_rater[].ai_ratings` | 정수 | 그 채점자가 매긴 AI 결과 점수 수 |
| `scores.by_rater[].ai_mean` | 숫자 또는 null | 그 평균. 매긴 것이 없으면 null |
| `scores.by_rater[].human_ratings` | 정수 | 사람 결과 점수 수 |
| `scores.by_rater[].human_mean` | 숫자 또는 null | 그 평균 |

- 가림 발견(`findings[]`)의 속성은 `rule`, `file`, `line`, `value`다(4.4 표). reply-drafts에는 발견이 없어 위 표에 행이 없다. 4.4 장면 표가 모양을 본다.
- 쓰지 않는 것: 문장 원문, 출처 표의 행 목록(가림 키 → 출처), 채점자별 건별 점수. 결과 문장은 결과 파일 위치(`items[].candidate`)로 찾는다.

### 3.2 judgment.json에 더하는 속성

`scores`가 있을 때만: `thresholds.score_gap_max`(문턱 셋 뒤), `checks`의 `SCORE` 확인(다섯 뒤 여섯째, 5.2). 점수표만이면 `checks`는 `TIME`, `SCORE` 둘이다(5.3). `checklist`는 대조 셈으로 들어가 판정 모양을 바꾸지 않는다.

판정 새 속성 표. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. `judgment.md` 3.2 표에 있는 경로는 다시 적지 않는다.

| 판정 새 속성 | 형식 | 뜻 |
| --- | --- | --- |
| `thresholds.score_gap_max` | 숫자 | AI 평균이 사람 평균보다 낮아도 되는 폭(5.1). `case.yaml` 값 그대로(자릿수 유지) 또는 기본값. `scores`가 있을 때만 |

- `checks[].rule`에 `SCORE`, `checks[].result`에 `revealed`·`met`·`not_met`, `todo[].key`에 `rescore_blind`가 는다. 값이 느는 것이다. 경로는 그대로다.
- `rule` `NOT_VERIFIED`가 `verdict` `conditional`과 같이 나올 수 있다(점수표만 + 대조할 열 없음, 5.3). 값은 늘지 않는다. 판정은 `verdict`가 정한다.

### 3.3 바이트 규칙과 `schema_version`

- 바이트 규칙은 `metrics.md` 3.3·`judgment.md` 3.3 그대로다(`Utf8JsonWriter`, 2칸, LF, 끝 LF, BOM 없음, `UnsafeRelaxedJsonEscaping`). 평균은 `decimal` 글(`3.86`, `4.00`)이다.
- `schema_version`은 metrics.json 1, judgment.json 2 그대로다. 기준은 ADR 0008 결정 1이다: 이미 받던 입력에서 속성을 빼거나 이름·형식·뜻을 바꾸면 올린다. 새로 받는 입력 모양(`checklist`·`scores`가 있는 `case.yaml`)에서만 출력이 바뀌고, 그 모양은 릴리스된 적이 없다. 열거 값이 느는 것(`SCORE`, `rescore_blind`)만으로는 올리지 않는다.
- 그래서 migration 커밋이 없다(카드 수용 기준 5). 예제 4종 expected 12개는 바이트 그대로다 [T14:O].
- 대안(추천하지 않음): 늘 `"checklist": null`, `"scores": null`을 쓴다. 읽는 쪽이 키를 늘 찾을 수 있다. 대신 예제 4종 metrics.json·judgment.json 8개 바이트가 바뀌고 `schema_version`을 올려야 한다(ADR 0004 migration). → 질문 9.

### 3.4 명령과 AI용 입구

- 내부 명령 `compare --json` = metrics.json, `judge --json` = judgment.json(ADR 0004 결정 6 규칙 5) 그대로다. 새 명령은 없다. 사용자 명령 `check`가 점수표도 읽는다(M2-5, **미확인**).
- 종료 코드: 4.5 오류는 2(입력 오류). 가림이 깨진 것(`revealed`)은 오류가 아니라 판정이다(0).

## 4. 규칙

### 4.1 `case.yaml` 키

`case-schema.md` 4.3 최상위 표에 두 키를 더한다(11절, PR (b)). 위치·순서는 상관없다(4.3 그대로).

| 키 | 형식 | 필수 | 규칙 |
| --- | --- | --- | --- |
| `checklist` | 매핑 | 아니오 | `text`(문자열, 필수), `items`(목록, 1개 이상). `inputs`가 있어야 한다(`required`, 경로 `inputs`). `compare`와 같이 써도 된다 |
| `checklist.text` | 문자열 | 예 | 결과 자료의 문장 열 이름. `inputs.key`·`inputs.group`·`compare` 필드와 다르다(`conflict`) |
| `checklist.items[]` | 매핑 | — | `item`(문자열, 필수, 서수 중복 금지 `duplicate`)과 `column`·`any` 가운데 꼭 하나(둘 다 `conflict`, 없음 `required`) |
| `checklist.items[].column` | 문자열 | — | 기준 자료의 열 이름. 그 행의 칸 값이 결과 문장에 있어야 한다. 칸이 비면 그 행에는 요구하지 않는다 |
| `checklist.items[].any` | 목록 | — | 문구 1개 이상(빈 목록 `required`). 문구는 비지 않은 문자열(`required`). 하나라도 결과 문장에 있으면 있다 |
| `scores` | 매핑 | 아니오 | `sheet`·`sources`(경로 패턴, 필수, `inputs` 경로와 같은 `format` 규칙), `min`·`max`(정수, 필수, 0 이상 100 이하, `min` < `max`. 아니면 `range`) |
| `thresholds.score_gap_max` | 숫자 | 아니오 | 0 이상 100 이하. `scores`가 없으면 `required`(경로 `scores`) |

- 오류 코드는 `case-schema.md` 4.9의 17개 그대로다. 새 코드가 없다. "언제"가 는다(11절).
- 단계 질문은 그대로 10개다(4절 4항 상한 12 안). 두 길은 케이스 수준 키라 단계 답을 늘리지 않는다.
- 사용자에게 맞나: 문구·열 이름은 사용자 낱말 그대로 쓴다(한글 항목 이름 가능). 정규식은 쓰지 않는다. 쓰기 쉽고, 틀린 정규식이 조용히 통과시키는 일이 없다.

파서 장면. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 바탕은 7.3 `case.yaml` 블록이다. "바꾼 곳"의 두 글(`옛 글` → `새 글`)은 바탕에 꼭 한 번 있는 글을 바꾼다. 글 안의 `\n`은 줄바꿈이다. `(없음)`은 바탕 그대로다.

| case.yaml 바꾼 곳 | 결과 | 코드 | 메시지 |
| --- | --- | --- | --- |
| (없음) | 받음 | — | — |
| `  score_gap_max: 0.5\n` → `` | 받음 | — | — |
| `    - {item: closing, any: [감사합니다, 고맙습니다]}\n` → `    - {item: closing, any: [감사합니다, 고맙습니다]}\n    - {item: 끝인사, any: [감사합니다]}\n` | 받음 | — | — |
| `  text: reply\n` → `  text: reply_id\n` | 오류 | `conflict` | `checklist.text는 key·compare 필드·group과 다른 열을 쓴다` |
| `  text: reply\n` → `  text: topic\n` | 오류 | `conflict` | `checklist.text는 key·compare 필드·group과 다른 열을 쓴다` |
| `{item: action, column: action}` → `{item: ticket, column: action}` | 오류 | `duplicate` | `체크리스트 항목이 중복된다 (처음: checklist.items[1])` |
| `{item: ticket, column: ticket}` → `{item: ticket, column: ticket, any: [q]}` | 오류 | `conflict` | `column과 any 중 하나만 쓴다` |
| `{item: ticket, column: ticket}` → `{item: ticket}` | 오류 | `required` | `column 또는 any가 있어야 한다` |
| `any: [감사합니다, 고맙습니다]` → `any: []` | 오류 | `required` | `문구가 1개 이상 있어야 한다` |
| `any: [감사합니다, 고맙습니다]` → `any: [감사합니다, ""]` | 오류 | `required` | `값이 비어 있다` |
| `  min: 1\n` → `  min: 5\n` | 오류 | `range` | `범위 밖이다 (min보다 커야 한다): 5` |
| `  max: 5\n` → `  max: 101\n` | 오류 | `range` | `범위 밖이다 (0 이상 100 이하): 101` |
| `  sheet: scores/sheet*.csv\n` → `  sheet: /scores/sheet.csv\n` | 오류 | `format` | `케이스 폴더 기준 상대 경로를 '/'로 쓴다 ('..', ':', '\' 없음)` |
| `  max: 5\n` → `  max: 5\n  scale: 5\n` | 오류 | `unknown-field` | `알 수 없는 필드다` |
| `inputs:\n  reference: reference/*.csv\n  candidate: candidate/*.csv\n  key: reply_id\n  group: topic\n` → `` | 오류 | `required` | `checklist가 있으면 inputs가 있어야 한다` |
| `scores:\n  sheet: scores/sheet*.csv\n  sources: scores/sources.csv\n  min: 1\n  max: 5\n` → `` | 오류 | `required` | `score_gap_max가 있으면 scores가 있어야 한다` |

- 위치는 `case-schema.md` 4.9 규칙대로다: 값 오류는 그 값, `conflict`·`required`(항목)는 그 항목 시작, `inputs`·`scores` 없음은 `checklist` 값·`score_gap_max` 값, `min` ≥ `max`는 `max` 값. `format` 메시지는 `inputs` 경로와 같은 글이다(`case-schema.md` 6.4 블록 [읽음]).
- `range` 메시지의 범위 글(`min보다 커야 한다`, `0 이상 100 이하`)은 이 문서가 정한다. 기존 `range` 메시지 모양(`범위 밖이다 (<범위>): <값>`) 그대로다.

### 4.2 체크리스트 대조

체크리스트는 대조의 한 종류다. `inputs`의 키로 기준 행과 결과 행을 짝짓고(`metrics.md` 4.3), 짝지은 결과 행마다 항목을 본다.

1. 열: 기준 파일마다 `column` 항목의 열이, 결과 파일마다 `checklist.text` 열이 머리 행에 꼭 한 번 있어야 한다. 없으면 `column`, 둘 이상이면 `duplicate-column`이다(`metrics.md` 4.7 그대로). 찾는 순서는 키 열 → `compare` 필드 → `inputs.group` → 체크리스트 열(`column` 항목 순서, 같은 열은 한 번)이다. 기준 자료에 문장 열이, 결과 자료에 `column` 열이 없어도 된다.
2. 글: 문장 칸과 기준 칸은 `metrics.md` 4.6 정규화 뒤의 글이다. 비교 전에 둘 다(그리고 `any` 문구도) 공백 글자(`char.IsWhiteSpace`)가 이어진 곳을 U+0020 하나로 접고 앞뒤를 뗀다. 줄바꿈·탭·전각 공백이 섞여도 같은 문구다.
3. 있다: 접은 문구가 접은 문장의 부분 문자열이면(서수) 있다(`found`). 대소문자·유니코드 정규화(NFC)·전각을 맞추지 않는다. `metrics.md` 4.6과 같은 까닭이다(같아 보여도 다른 바이트면 다른 결과다).
4. `column` 항목: 그 기준 행의 칸이 문구다. 칸이 비면 그 행에는 요구하지 않는다(`skipped`, 셈에 들지 않는다). 모든 문의에 주문 번호가 있지는 않기 때문이다. 단 그 행에 확인할 것이 하나도 남지 않으면 오류다(규칙 10).
5. `any` 항목: 문구 가운데 하나라도 있으면 있다.
6. 빠진 항목(`absent`)이 하나라도 있으면 그 기준 항목은 값 불일치(`mismatch`)다. `compare` 필드와 같이 쓰면 둘 중 하나라도 걸리면 값 불일치다. 셈·정확도·누락률·몰림·검토 항목은 `metrics.md`·`judgment.md` 그대로다.
7. 결과 쪽 중복(`metrics.md` 4.3, T-013): 결과 쪽에 두 번 나온 기준 키는 값 불일치다. 대표 행의 빠진 항목은 기준 항목에, 둘째 이후 행의 빠진 항목은 `duplicate` 항목에 적는다. 기준에 없는 키의 행(추가·그 중복)은 항목을 보지 않는다(`[]`).
8. 항목 셈(`entries[]`)은 기준에 키가 있는 결과 행마다 센다(`fields[].compared`와 같은 행). `required` = `found` + `absent`.
9. 알려진 한계: 부분 문자열이라 `q397`은 `q3970` 안에서도 있다. 문구 앞뒤 글자를 보지 않는다. 낱말 경계는 언어마다 달라 문자열 규칙 하나로 정할 수 없다. 사용자가 문구를 길게 쓰면 줄어든다(예: `q397 건`).
10. 확인할 것이 없는 행: `compare` 필드가 없고, 체크리스트에 `any` 항목이 없고, 그 기준 행의 `column` 항목 칸이 모두 비면 `no-check` 오류다(4.5). 기준 행마다 본다(그 키의 결과 행이 있든 없든). 그 행은 결과 문장이 비어도 일치가 되기 때문이다. 이 조건은 기준 자료와 `case.yaml`(사람이 만든 것)만으로 정해진다. 그래서 대조 셈이 아니라 오류로 멈춰 고치게 한다(4.5 끝 글머리). 고치는 길: 그 칸을 채운다, 모든 행에 요구할 `any` 항목(예: 끝인사)을 더한다, `compare` 필드를 더한다, 또는 확인할 것이 없는 행을 기준 자료에서 뺀다. reply-drafts는 `closing`(`any`)이 있어 모든 행에 요구가 있다.
    - 다른 안(리뷰 2차 B1, 질문 16): 일치로 세지 않고 값 불일치로 센다(AI 탓이 아닌 것을 AI 탓으로 센다). 셈에서 뺀다(`counts` 모양과 정확도 정의 `metrics.md` 4.4가 바뀐다). 요구 합이 0일 때만 조건부(일부 행만 빈 경우를 못 막는다. 리뷰 재현에서 빈 답변 6건이 일치로 세어져 정확도 50.0).

맞추기. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. "항목"의 `column`은 기준 칸이 문구인 항목, `any: […]`는 그 문구 목록이다. 글 안의 `\n`·`\t`는 줄바꿈·탭, ` `은 그 글자다. `(빈 칸)`은 빈 글이다.

| 항목 | 기준 칸 | 결과 문장 | 결과 |
| --- | --- | --- | --- |
| `column` | `q397` | `문의하신 q397 건을 확인했습니다.` | `found` |
| `column` | `q397` | `문의하신 Q397 건을 확인했습니다.` | `absent` |
| `column` | `q397` | `문의하신 q3970 건` | `found` |
| `column` | `q397` | `문의하신 q 397 건` | `absent` |
| `column` | `환불 접수` | `환불  접수 절차를 안내드립니다.` | `found` |
| `column` | `환불 접수` | `환불\n접수 절차를 안내드립니다.` | `found` |
| `column` | `환불 접수` | `환불 접수 절차를 안내드립니다.` | `found` |
| `column` | `환불  접수` | `환불 접수 절차를 안내드립니다.` | `found` |
| `column` | `환불 접수` | `환불접수 절차를 안내드립니다.` | `absent` |
| `column` | (빈 칸) | `안녕하세요.` | `skipped` |
| `column` | `q397` | (빈 칸) | `absent` |
| `any: [감사합니다, 고맙습니다]` | — | `고맙습니다.` | `found` |
| `any: [감사합니다, 고맙습니다]` | — | `감사 합니다.` | `absent` |
| `any: [감사합니다, 고맙습니다]` | — | (빈 칸) | `absent` |
| `any: [감사  합니다]` | — | `감사 합니다.` | `found` |
| `any: [Thanks]` | — | `thanks.` | `absent` |

체크리스트 장면(규칙 10). 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 바탕은 7.3 reply-drafts 파일이다. "바꾼 곳"은 4.4 장면 표와 같은 모양이고, 파일이 다른 묶음은 `; `로 잇는다. "정확도(%)"는 받았을 때 metrics.json의 `accuracy_pct`다.

| 체크리스트 바꾼 곳 | 결과 | 코드 | 정확도(%) |
| --- | --- | --- | --- |
| (없음) | 받음 | — | 83.3 |
| `case.yaml`: `    - {item: action, column: action}\n` → ``; `reference/replies.csv`: `,q397,` → `,,` | 받음 | — | 100.0 |
| `case.yaml`: `    - {item: action, column: action}\n    - {item: closing, any: [감사합니다, 고맙습니다]}\n` → ``; `reference/replies.csv`: `,q397,` → `,,` | 오류 | `no-check` | — |
| `case.yaml`: `    - {item: ticket, column: ticket}\n` → ``, `    - {item: closing, any: [감사합니다, 고맙습니다]}\n` → ``; `reference/replies.csv`: `,환불 접수\n` → `,\n`, `,교환 접수\n` → `,\n`, `,배송 조회\n` → `,\n` | 오류 | `no-check` | — |

- 둘째 행: `d12`의 번호 칸이 비어 `ticket`은 그 행에 요구하지 않는다. `closing`(`any`)이 남아 확인할 것이 있다. 처리 안내 항목을 지워 `d03`도 일치다(12/12).
- 셋째 행: 번호 항목만 남기고 `d12`의 번호 칸을 비웠다. `d12`에 확인할 것이 없다(`reference/replies.csv:13`). 리뷰 2차 B1의 "일부 행만 빈 경우"다.
- 넷째 행: 처리 안내 항목만 남기고 그 칸을 모두 비웠다. 모든 기준 행이 오류다(첫 오류 `reference/replies.csv:2`). 리뷰 2차 B1의 "모두 빈 경우"다.
- 결과 문장은 바꾸지 않았다. 규칙 10은 결과 자료를 보지 않는다. 빈 답변이 일치로 세어지지 않는 것은 6.1 "번호 칸 빈 행, 빈 답변" 장면이 본다.

### 4.3 가림 점수표

파일 둘. 열 이름은 정해져 있다(서수, 정규화 뒤). 다른 열이 있어도 된다(4.4 `source_column`은 예외).

| 파일 | 열 | 뜻 |
| --- | --- | --- |
| 점수표(`scores.sheet`) | `rater`, `item`, `blind`, `score` | 채점자, 건, 가림 키, 점수. 한 행이 점수 하나 |
| 출처 표(`scores.sources`) | `blind`, `item`, `source` | 가림 키, 건, 출처(`ai` 또는 `human`). 한 행이 결과 하나 |

규칙:

1. 출처 표: 가림 키는 쪽 전체(모든 출처 표 파일)에서 하나씩(`duplicate-key`). 출처는 `ai`·`human`(서수, `source`). 건마다 `ai` 하나, `human` 하나(`pair`).
2. 점수표: 점수 칸은 따옴표를 풀고 정규화(`metrics.md` 4.6, 앞뒤 공백 뗌)한 값이 정수 글 `0` 또는 `[1-9][0-9]*`이고 `min` 이상 `max` 이하(`score`). 다른 칸과 같은 CSV 규칙이다(4.6 CSV 변형). `"4"`·` 4 `는 `4`이고, `4.0`·`04`·`+4`는 정수 글이 아니다. 가림 키는 출처 표에 있어야 하고, 건은 출처 표의 그 키의 건과 같아야 한다(`blind`). 대조가 `inputs` 파일을 읽으면(대조 `measured`) 출처 표의 건은 기준 자료의 키여야 한다(`blind`, 경로 `item`. 리뷰 4차 N1: 다른 케이스의 점수표를 말없이 쓰지 않게). 그래서 점수표의 건도 기준 자료의 키다. 점수표만(대조 미검증)은 볼 기준이 없어 보지 않는다. 같은 채점자·같은 가림 키는 한 번(`duplicate-key`).
3. 빠진 점수: 출처 표의 가림 키마다 점수가 하나 이상 있어야 한다(`blind`). 점수표는 사람이 만든 자료다. 빠진 칸은 자료 오류로 보고 채우게 한다(`metrics.md` 12.1의 "기준 자료는 사람이 확정한 정답"과 같은 갈래).
4. 집계: 출처마다 결과 수, 점수 수, 합, 평균, 분포(`min`~`max` 점수마다 수). 평균은 소수 둘째 자리로, 셋째 자리가 5 이상이면 올린다. 정수로 셈한다: `hundredths = (2 × 합 × 100 + 수) div (2 × 수)`. 늘 두 자리다(`4.00`).
5. 건마다 비교(`paired`): 그 건의 AI 점수 합 × 사람 점수 수와 사람 점수 합 × AI 점수 수를 비교한다. 크면 `better`, 같으면 `same`, 작으면 `worse`. 나눗셈이 없다.
6. 채점자별(`by_rater`): 채점자마다 출처별 점수 수와 평균. 매긴 것이 없으면 평균 null.
7. 판정 비교는 평균 글이 아니라 합·수 셈으로 한다(5.2). `metrics.md` 4.8 권고와 같다.
8. 순서: 파일은 경로 서수, 행은 파일 → 줄. `raters`는 서수. 출처는 `ai` → `human`.

평균. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다.

| 점수 합 | 점수 수 | 평균 |
| --- | --- | --- |
| 139 | 36 | 3.86 |
| 151 | 36 | 4.19 |
| 12 | 3 | 4.00 |
| 5 | 2 | 2.50 |
| 1 | 3 | 0.33 |
| 2 | 3 | 0.67 |
| 1 | 8 | 0.13 |
| 1 | 200 | 0.01 |
| 1 | 201 | 0.00 |
| 0 | 5 | 0.00 |
| 500 | 5 | 100.00 |

- 1/8 = 0.125 → 0.13, 1/200 = 0.005 → 0.01. 0.005 경계에서 올린다.

### 4.4 가림 확인

엔진은 점수표와 출처 표 글만 본다. 채점자가 실제로 무엇을 봤는지는 모른다. 그래서 글로 드러난 것만 찾는다. 두 갈래다. 출처 낱말(앞 세 규칙: `source`, `ai`, `human`)과 표시 몰림(`source_pattern`)이다. 표시 몰림은 채점자에게 보이는 표시(가림 키, 점수표의 다른 열, 점수 파일)가 한 출처의 결과에 몰렸는지 센다. 몰리면 채점자가 그 표시로 결과를 묶어 출처를 짐작할 수 있다. 엔진은 출처 표(정답)를 알아 몰림을 직접 셀 수 있다([재검토], 리뷰 4차 B1). 하나라도 있으면 `blind.status` = `revealed`이고 판정이 보류다(5.3). 오류가 아니다. 이미 매긴 점수는 파일을 고쳐도 가림이 되돌아오지 않기 때문이다(`metrics.md` 12.1과 같은 갈래).

가림 규칙. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 표 순서가 `findings` 순서의 첫 열쇠다.

| 가림 규칙 | 보는 곳 | 걸리는 조건 | 발견 위치·값 |
| --- | --- | --- | --- |
| `source_column` | 점수표 머리 행 | 열 이름을 ASCII 소문자로 바꾼 글이 `source`(출처 표의 출처 열 이름) | 그 파일, 머리 행 줄, 열 이름 그대로 |
| `source_value` | 점수표 데이터 칸(`score` 열 밖) | 칸을 ASCII 소문자로 바꾼 글이 `ai` 또는 `human` | 그 파일, 그 줄, 칸 글 그대로 |
| `source_in_blind` | 출처 표의 가림 키 | 키를 ASCII 영문자 묶음과 숫자 묶음으로 나눈 조각 가운데 ASCII 소문자로 `ai` 또는 `human`이 있음 | 그 출처 표, 그 줄, 키 |
| `source_pattern` | 채점자에게 보이는 표시: 가림 키, 점수표의 다른 열 칸(`rater`·`item`·`blind`·`score` 밖), 점수 파일 | 한 표시가 건마다 두 결과 가운데 한쪽에만 있는 것을 세어 몰림 문턱 셋을 모두 넘음(아래 "표시 몰림") | 그 표시가 있는 첫 칸. 키: 출처 표 그 줄, 키. 열: 점수표 그 줄, 칸 글. 파일: 그 파일 머리 행 줄, 경로 |

- 순서: 규칙(표 순서) → 파일 경로 서수 → 줄 → 찾은 순서(칸 순서).
- ASCII 소문자: `A`~`Z`만 `a`~`z`로. 문화권과 상관없다. `AI`, `Ai`도 잡는다. 열 이름도 같다: `Source`, `SOURCE`도 `source_column`이다(리뷰 2차 N1. 값만 대소문자를 보지 않고 열 이름은 정확히 같을 때만 보던 것을 맞췄다).
- 조각: 이어진 ASCII 영문자(`A`~`Z`, `a`~`z`)가 한 조각, 이어진 숫자(`0`~`9`)가 한 조각이다. 그 밖의 글자(`-`, `_`, `.`, 한글 등)는 조각을 나누기만 한다. 영문자와 숫자 사이에서도 나눈다(리뷰 3차 B1. 전에는 `ai01`을 한 조각으로 봐서 놓쳤다).
- `b01-ai`, `B01_AI`, `ai.3`, `ai01`(`ai`·`01`), `HUMAN03`(`human`·`03`), `b01ai`(`b`·`01`·`ai`)는 걸린다. `bai05`(`bai`·`05`), `mail05`(`mail`·`05`), `rain`은 걸리지 않는다(조각 하나가 `ai`가 아니다). reply-drafts 키 `b01`~`b24`는 `b`·숫자라 걸리지 않는다.
- 채점자 이름이 `ai`이면 `source_value`에 걸린다. AI를 채점자로 썼다면 출처가 드러난 것은 아니지만, 이름을 바꾸게 한다(사람 채점과 섞이지 않게).
- 보지 않는 것: 점수표 행 순서(9절 3), 결과마다 다른 글로 나타낸 출처(9절 12), 의견 칸 한 곳의 문장(예: "AI 같다"), 칸 안의 조각(`ai-draft` 한 칸, 9절 10), 한 결과에만 붙은 한국어 표기(`사람05`, 9절 11). 같은 표시가 여러 결과에 붙어 한 출처로 몰리면 `source_pattern`이 잡는다.

표시 몰림(`source_pattern`, [재검토]·리뷰 4차 B1):

- 표시: 글 하나에서 넷을 본다. 글 그대로, 틀(ASCII 숫자 묶음 `0`~`9`를 지운 글), 첫 글자, 끝 글자(ASCII 숫자가 아닐 때만, 유니코드 문자 하나). 표시는 종류와 값의 짝이다. 값은 서수로 비교한다(대소문자·기호·한글 그대로). 빈 글은 표시가 없다(틀이 빈 글이면 틀이 없다).
- 키의 표시: 가림 키에서는 넷을 더 본다(키 표시 표). 꼴(ASCII 숫자는 `9`, ASCII 대문자는 `A`, ASCII 소문자는 `a`로 바꾼 글. 그 밖의 글자는 그대로), 글자 수(UTF-16 단위, 4.2 열 번호와 같다), 글자 종류(ASCII 대문자 `A`·소문자 `a`·숫자 `9`가 키에 있는지. 종류마다 표시 하나), 작은 쪽(건의 두 키가 모두 숫자 묶음이 있고 숫자 줄이 다르면 숫자 줄이 작은 키에 붙는다). 숫자 줄은 숫자 묶음을 앞에서부터 정수로 읽은 줄이다. 앞 묶음부터 견주고, 앞이 같고 묶음이 적으면 작다. 키는 사람이 만든 이름표다. 자릿수·대소문자·번호 크기가 출처를 따라 갈리면 그대로 가림이 깨진 것이다. 열 칸에는 이 넷을 더하지 않는다. 열의 숫자·꼴은 채점자가 쓴 값(시간, 의견)일 수 있어 품질을 따라 갈린다.
- 보는 곳 셋: 결과의 가림 키, 점수표 행의 다른 열 칸(열 이름과 짝지어 본다. 빈 칸은 표시가 없다), 점수표 행이 든 파일(경로 하나가 표시). `rater`·`item`·`blind`·`score` 열 칸은 보지 않는다. 채점자 쏠림은 9절 8의 일이고, 건은 두 결과가 같다.
- 센 건: 출처 표의 건 수 N이다(건마다 AI 결과 하나, 사람 결과 하나, 4.3).
- 건이 갈림, 키: 그 건의 두 가림 키를 견준다. 채점자와 상관없다. 키는 출처 표에서 한 번 정해지고 누가 매기든 그대로 쓰인다.
- 건이 갈림, 열·파일: 그 건의 AI 결과 행들과 사람 결과 행들을 견준다. 두 출처를 모두 매긴 채점자(AI 결과를 하나 이상, 사람 결과를 하나 이상 매긴 채점자)의 행만 본다. 같은 채점자가 그 건의 두 결과를 다 매기지 않아도 된다(채점자마다 건 하나에 결과 하나만 보는 배정도 본다). 한쪽 출처만 매긴 채점자는 견줄 것이 없어 그 표시로 출처를 가를 수 없다. 그 치우침은 9절 8의 일이다. 두 결과 모두 그런 행이 있어야 견준다.
- AI 쪽: 표시가 AI 키에만 있거나, AI 결과 행 어디엔가 있고 사람 결과 행 어디에도 없다. 사람 쪽은 반대다. 한쪽만 맞으면 그 건은 그쪽으로 갈렸다. 둘 다 맞거나 둘 다 아니면 갈리지 않았다.
- 몰림: 표시마다 AI 쪽으로 갈린 건 수 a, 사람 쪽 h, 센 건 수 N. 갈린 건 c = a + h, 몰린 쪽 M = max(a, h), 반대쪽 m = min(a, h). 아래 몰림 문턱 셋을 모두 넘으면 걸린다: c ≥ 8, 12 × m ≤ c, 4 × M ≥ N. 정수 셈이다.
- 까닭: 가림이 맞으면 가림 키와 점수표는 출처를 모르고 만든다. 그러면 건마다 두 결과 가운데 어느 쪽이 AI인지는 동전 던지기와 같다. 그때 한 표시가 이렇게 몰릴 확률은 표시 하나마다 2/256(0.78%) 이하다(부호 검정, 양쪽. c = 8, m = 0일 때가 가장 크다. c = 12, m = 1은 26/4096). 12 × m ≤ c는 몇 건을 섞어 넣어 몰림을 숨기지 못하게 하고, 4 × M ≥ N은 건이 많을 때 작은 우연 묶음을 걸지 않게 한다.
- 발견: 걸린 표시마다 그 표시가 있는 첫 칸 하나다. 키는 출처 표(파일 경로 서수 → 줄)에서 그 표시가 있는 첫 행(작은 쪽은 건의 작은 키인 첫 행), 값은 키. 열은 점수표(파일 경로 서수 → 줄 → 칸)의 그 행, 값은 칸 글. 파일은 그 파일 머리 행 줄, 값은 경로. 같은 칸을 가리키면 한 번만 쓴다.
- 예: reply-drafts 키 `b01`~`b24`는 모든 건에서 두 키의 표시(틀 `b`, 첫 글자 `b`, 꼴 `a99`)가 같다. 작은 쪽은 AI 키 6건, 사람 키 6건이라 몰리지 않았다. AI 키만 `x01`처럼 바꾸면 `x`가 12건 모두 AI 쪽, `b`가 12건 모두 사람 쪽이라 둘 다 걸린다(발견 `x01`, `b03`). 모든 행에 같은 값인 열(`메모`)은 갈리지 않는다. 채점자마다 나눈 파일은 그 건의 두 결과 행이 같은 파일들에 있어 갈리지 않는다.

키 표시. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. "글자 수"는 UTF-16 단위다(이모지는 2). "글자 종류"는 키에 있는 종류(`A`·`a`·`9` 순서), "숫자 줄"의 `—`는 숫자 묶음이 없음이다.

| 가림 키 | 꼴 | 글자 수 | 글자 종류 | 숫자 줄 |
| --- | --- | --- | --- | --- |
| `b01` | `a99` | 3 | `a`, `9` | 1 |
| `B05` | `A99` | 3 | `A`, `9` | 5 |
| `b001` | `a999` | 4 | `a`, `9` | 1 |
| `x2-07` | `a9-99` | 5 | `a`, `9` | 2, 7 |
| `Ab3` | `Aa9` | 3 | `A`, `a`, `9` | 3 |
| `abc` | `aaa` | 3 | `a` | — |
| `0042` | `9999` | 4 | `9` | 42 |
| `기계01` | `기계99` | 4 | `9` | 1 |
| `b01😀` | `a99😀` | 5 | `a`, `9` | 1 |

작은 쪽. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. `—`는 작은 쪽이 없음이다.

| 키 하나 | 키 둘 | 작은 쪽 |
| --- | --- | --- |
| `b01` | `b13` | `b01` |
| `b2` | `b10` | `b2` |
| `b101` | `b013` | `b013` |
| `r1-5` | `r1-12` | `r1-5` |
| `r1-9` | `r2` | `r1-9` |
| `r1` | `r1-1` | `r1` |
| `b01` | `b1` | — |
| `abc` | `b01` | — |

몰림 문턱. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다.

| 몰림 문턱 | 값 | 조건 |
| --- | --- | --- |
| `min_split` | 8 | 갈린 건 c ≥ 8 |
| `minority_per` | 12 | 12 × 반대쪽 m ≤ c |
| `share_per` | 4 | 4 × 몰린 쪽 M ≥ 센 건 N |

몰림 예. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다.

| AI 쪽 건 | 사람 쪽 건 | 센 건 | 걸리나 |
| --- | --- | --- | --- |
| 12 | 0 | 12 | 예 |
| 8 | 0 | 12 | 예 |
| 7 | 0 | 12 | 아니오 |
| 0 | 8 | 8 | 예 |
| 11 | 1 | 12 | 예 |
| 10 | 1 | 12 | 아니오 |
| 22 | 2 | 24 | 예 |
| 21 | 3 | 24 | 아니오 |
| 6 | 6 | 12 | 아니오 |
| 12 | 0 | 48 | 예 |
| 11 | 0 | 48 | 아니오 |
| 3 | 0 | 4 | 아니오 |

점수 자료 장면. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 바탕은 7.3 reply-drafts(표 대조와 점수표)다. "바꾼 곳"은 `파일`: `옛 글` → `새 글`이고 그 파일의 모든 곳을 바꾼다(`,`로 이은 파일 둘은 둘 다). 글 안의 `\n`은 줄바꿈이다. 묶음은 `; `로 잇고 차례로 한다. 파일 뒤에 `(출처 `ai` 행)`을 붙이면 그 출처의 데이터 행(행 끝 줄바꿈 포함)에서만 바꾸고, `(채점자 `p2` 행)`은 그 채점자의 행에서만 바꾼다. 행의 출처는 그 묶음 전 `scores/sources.csv`에서 그 행 가림 키의 출처다. "`파일`의 출처 `ai` 행 → `새 파일`"(또는 채점자 `p2` 행)은 그 행을 새 파일로 옮긴다. 새 파일의 머리 행은 원래 파일과 같다. `scores/sheet*.csv`에 맞지 않는 이름이면 읽지 않는 파일이 된다. 옛 글이 없거나 옮길 행이 없으면 장면이 틀린 것이다. "가림 발견"은 규칙과 수다.

| 점수 자료 바꾼 곳 | 결과 | 가림 발견 | 코드 |
| --- | --- | --- | --- |
| (없음) | `kept` | — | — |
| `scores/sources.csv`, `scores/sheet.csv`: `b05,` → `b05-ai,` | `revealed` | `source_in_blind` 1건 | — |
| `scores/sources.csv`, `scores/sheet.csv`: `b05,` → `B05_AI,` | `revealed` | `source_in_blind` 1건 | — |
| `scores/sources.csv`, `scores/sheet.csv`: `b05,` → `b05-HUMAN,` | `revealed` | `source_in_blind` 1건 | — |
| `scores/sources.csv`, `scores/sheet.csv`: `b05,` → `ai01,` | `revealed` | `source_in_blind` 1건 | — |
| `scores/sources.csv`, `scores/sheet.csv`: `b05,` → `HUMAN03,` | `revealed` | `source_in_blind` 1건 | — |
| `scores/sources.csv`, `scores/sheet.csv`: `b05,` → `bai05,` | `kept` | — | — |
| `scores/sources.csv`, `scores/sheet.csv`: `b05,` → `mail05,` | `kept` | — | — |
| `scores/sheet.csv`: `p2,` → `AI,` | `revealed` | `source_value` 24건 | — |
| `scores/sheet.csv`: `\n` → `,source\n` | `revealed` | `source_column` 1건 | — |
| `scores/sheet.csv`: `\n` → `,Source\n` | `revealed` | `source_column` 1건 | — |
| `scores/sheet.csv`: `\n` → `,메모\n` | `kept` | — | — |
| `scores/sheet.csv`: `\n` → `,human\n` | `revealed` | `source_value` 72건 | — |
| `scores/sources.csv`, `scores/sheet.csv` (출처 `ai` 행): `b` → `x` | `revealed` | `source_pattern` 2건 | — |
| `scores/sources.csv`, `scores/sheet.csv` (출처 `ai` 행): `b` → `gpt`; `scores/sources.csv`, `scores/sheet.csv` (출처 `human` 행): `b` → `staff` | `revealed` | `source_pattern` 2건 | — |
| `scores/sources.csv`, `scores/sheet.csv` (출처 `ai` 행): `b` → `B` | `revealed` | `source_pattern` 2건 | — |
| `scores/sources.csv`, `scores/sheet.csv` (출처 `ai` 행): `b` → `x`; `scores/sources.csv`, `scores/sheet.csv`: `x01,` → `b01,`, `b24,` → `x24,` | `revealed` | `source_pattern` 2건 | — |
| `scores/sources.csv`, `scores/sheet.csv`: `b01,` → `x01,`, `b02,` → `x02,`, `b04,` → `x04,`, `b05,` → `x05,`, `b07,` → `x07,`, `b09,` → `x09,`, `b11,` → `x11,` | `kept` | — | — |
| `scores/sources.csv`, `scores/sheet.csv`: `b01,` → `x01,`, `b02,` → `x02,`, `b04,` → `x04,`, `b05,` → `x05,`, `b07,` → `x07,`, `b09,` → `x09,`, `b11,` → `x11,`, `b12,` → `x12,` | `revealed` | `source_pattern` 2건 | — |
| `scores/sheet.csv`: `score\n` → `score,writer\n`; `scores/sheet.csv` (출처 `ai` 행): `\n` → `,gpt\n`; `scores/sheet.csv` (출처 `human` 행): `\n` → `,staff\n` | `revealed` | `source_pattern` 2건 | — |
| `scores/sheet.csv`: `score\n` → `score,AI\n`; `scores/sheet.csv` (출처 `ai` 행): `\n` → `,Y\n`; `scores/sheet.csv` (출처 `human` 행): `\n` → `,N\n` | `revealed` | `source_pattern` 2건 | — |
| `scores/sheet.csv`: `score\n` → `score,is_ai\n`; `scores/sheet.csv` (출처 `ai` 행): `\n` → `,Y\n`; `scores/sheet.csv` (출처 `human` 행): `\n` → `,N\n` | `revealed` | `source_pattern` 2건 | — |
| `scores/sheet.csv`: `score\n` → `score,writer\n`; `scores/sheet.csv` (출처 `ai` 행): `\n` → `,gpt\n`; `scores/sheet.csv` (출처 `human` 행): `\n` → `,\n` | `revealed` | `source_pattern` 1건 | — |
| `scores/sheet.csv`의 출처 `ai` 행 → `scores/sheet2.csv` | `revealed` | `source_pattern` 2건 | — |
| `scores/sheet.csv`의 채점자 `p2` 행 → `scores/sheet2.csv` | `kept` | — | — |
| `scores/sheet.csv`의 채점자 `p3` 행 → `scores/old.csv`; `scores/sheet.csv`의 채점자 `p1` 행 → `scores/sheet-p1.csv`; `scores/sheet-p1.csv`의 출처 `human` 행 → `scores/old1.csv`; `scores/sheet.csv`의 출처 `ai` 행 → `scores/old2.csv` | `kept` | — | — |
| `scores/sources.csv`, `scores/sheet.csv` (출처 `ai` 행): `b` → `k`; `scores/sources.csv`, `scores/sheet.csv` (출처 `human` 행): `b` → `l`; `scores/sheet.csv`의 채점자 `p3` 행 → `scores/old.csv`; `scores/sheet.csv`의 채점자 `p1` 행 → `scores/sheet-p1.csv`; `scores/sheet-p1.csv`의 출처 `human` 행 → `scores/old1.csv`; `scores/sheet.csv`의 출처 `ai` 행 → `scores/old2.csv` | `revealed` | `source_pattern` 2건 | — |
| `scores/sheet.csv` (출처 `ai` 행): `p1,d01,` → `q,d01,`, `p1,d02,` → `q,d02,`, `p1,d03,` → `q,d03,`, `p1,d04,` → `q,d04,`, `p1,d05,` → `q,d05,`, `p1,d06,` → `q,d06,`; `scores/sheet.csv` (출처 `human` 행): `p1,d07,` → `q,d07,`, `p1,d08,` → `q,d08,`, `p1,d09,` → `q,d09,`, `p1,d10,` → `q,d10,`, `p1,d11,` → `q,d11,`, `p1,d12,` → `q,d12,`; `scores/sheet.csv`의 채점자 `p2` 행 → `scores/old2.csv`; `scores/sheet.csv`의 채점자 `p3` 행 → `scores/old3.csv`; `scores/sheet.csv`: `score\n` → `score,writer\n`; `scores/sheet.csv` (출처 `ai` 행): `\n` → `,gpt\n`; `scores/sheet.csv` (출처 `human` 행): `\n` → `,staff\n` | `revealed` | `source_pattern` 2건 | — |
| `scores/sheet.csv` (출처 `ai` 행): `p1,d01,` → `q,d01,`, `p1,d02,` → `q,d02,`, `p1,d03,` → `q,d03,`, `p1,d04,` → `q,d04,`, `p1,d05,` → `q,d05,`, `p1,d06,` → `q,d06,`; `scores/sheet.csv` (출처 `human` 행): `p1,d07,` → `q,d07,`, `p1,d08,` → `q,d08,`, `p1,d09,` → `q,d09,`, `p1,d10,` → `q,d10,`, `p1,d11,` → `q,d11,`, `p1,d12,` → `q,d12,`; `scores/sheet.csv`의 채점자 `p2` 행 → `scores/old2.csv`; `scores/sheet.csv`의 채점자 `p3` 행 → `scores/old3.csv`; `scores/sheet.csv`의 채점자 `q` 행 → `scores/sheet-q.csv` | `kept` | — | — |
| `scores/sheet.csv` (출처 `human` 행): `p1,d04,` → `p4,d04,`, `p1,d05,` → `p4,d05,`, `p1,d06,` → `p4,d06,`, `p1,d07,` → `p4,d07,`, `p1,d08,` → `p4,d08,`, `p1,d09,` → `p4,d09,`, `p1,d10,` → `p4,d10,`, `p1,d11,` → `p4,d11,`, `p1,d12,` → `p4,d12,`; `scores/sheet.csv`의 채점자 `p2` 행 → `scores/old2.csv`; `scores/sheet.csv`의 채점자 `p3` 행 → `scores/old3.csv` | `kept` | — | — |
| `scores/sources.csv`, `scores/sheet.csv` (출처 `ai` 행): `b` → `b0`; `scores/sources.csv`, `scores/sheet.csv` (출처 `human` 행): `b` → `b1` | `revealed` | `source_pattern` 1건 | — |
| `scores/sources.csv`, `scores/sheet.csv` (출처 `ai` 행): `b` → `b0` | `revealed` | `source_pattern` 2건 | — |
| `scores/sheet.csv`: `,4\n` → `,6\n` | 오류 | — | `score` |
| `scores/sheet.csv`: `,4\n` → `,4.0\n` | 오류 | — | `score` |
| `scores/sheet.csv`: `,4\n` → `,04\n` | 오류 | — | `score` |
| `scores/sheet.csv`: `,4\n` → `,"4"\n` | `kept` | — | — |
| `scores/sheet.csv`: `,b24,` → `,b99,` | 오류 | — | `blind` |
| `scores/sources.csv`: `,human\n` → `,ai\n` | 오류 | — | `pair` |
| `scores/sources.csv`: `,ai\n` → `,AI\n` | 오류 | — | `source` |
| `scores/sources.csv`: `b02,` → `b01,` | 오류 | — | `duplicate-key` |
| `scores/sources.csv`, `scores/sheet.csv`: `,d` → `,z` | 오류 | — | `blind` |

- `\n` → `,human\n`은 머리 행에 `human` 열을 더하고 모든 칸에 `human`을 쓴다. 열 이름 `human`은 `source_column`이 아니다(열 이름은 ASCII 소문자로 `source`인 것만 본다). 칸 72개가 `source_value`다.
- `\n` → `,Source\n`은 열 이름 `Source`가 `source_column`이다(값 `Source`). 칸의 `Source`는 `ai`·`human`이 아니라 `source_value`가 아니다.
- `b24`를 바꾸면 그 키는 출처 표에 없다(`blind`, 첫 오류). 점수가 빠진 키(`b24`)도 `blind`다.
- 점수 칸 `"4"`는 따옴표를 푼 값이 `4`라 받는다. `04`·`4.0`은 정수 글이 아니라 `score` 오류다(4.3 규칙 2).
- `(출처 `ai` 행): `b` → `x``는 AI 키 12개를 `x01`처럼 바꾼다. 틀·첫 글자 `x`가 12건 모두 AI 쪽, `b`가 12건 모두 사람 쪽이라 발견이 둘(`x01`, `b03`)이다. `gpt`/`staff`, 대소문자 `B`/`b`도 같다([재검토] 리뷰 4차 B1 재현. 고치기 전에는 `kept`).
- 섞은 장면: AI 키 하나(`b01`)를 `b`로 되돌리고 같은 건의 사람 키(`b24`)를 `x24`로 바꿔도 `x`가 11건 AI 쪽, 1건 사람 쪽이라 걸린다(12 × 1 ≤ 12).
- AI 키 7개만 `x`면 갈린 건이 7이라 걸리지 않는다(`min_split` 8). 8개면 걸린다. 가림이 맞아도 표시 하나가 7건 모두 한쪽에 몰릴 확률은 1/64이다.
- 열 장면: `writer` 열에 AI 행 `gpt`, 사람 행 `staff`(또는 `AI`·`is_ai` 열에 `Y`·`N`)를 쓰면 12건이 모두 갈린다. 열 이름 `AI`는 `source_column`이 아니다. 사람 행을 비우면 빈 칸은 표시가 아니라 `gpt` 하나만 걸린다.
- 파일 장면: AI 행을 `scores/sheet2.csv`로 옮기면 두 파일이 모두 걸린다(값은 경로). 채점자 `p2`의 행을 옮기면 그 건의 두 결과 행이 같은 파일들(`sheet.csv`·`sheet2.csv`)에 있어 갈리지 않는다.
- 한쪽만 매긴 채점자 장면: `p3` 행을 점수 파일 밖(`scores/old.csv`, 4.3 파일 이름에 맞지 않음)으로 빼고, `p1`은 AI 행만(`scores/sheet-p1.csv`), `p2`는 사람 행만(`scores/sheet.csv`) 남긴다. 파일이 출처마다 갈려도 두 출처를 매긴 채점자가 없어 열·파일은 견주지 않는다. 채점자마다 한 출처만 봐 견줄 것이 없다. 가림 확인이 아니라 9절 8의 일이다. 같은 배정에서 키를 출처마다 `k`·`l`로 바꾸면 걸린다. 키는 채점자와 상관없이 본다.
- 건 하나에 결과 하나 장면: 채점자 `q`는 `d01`~`d06`의 AI 결과와 `d07`~`d12`의 사람 결과를, `p1`은 그 나머지를 매긴다(`p2`·`p3` 행은 점수 파일 밖). 둘 다 두 출처를 매겼지만 한 건의 두 결과를 다 매긴 채점자는 없다. `writer` 열의 `gpt`·`staff`는 12건 모두 갈려 걸린다([재검토] 뒤 스스로 만든 우회. 그 전 초안은 "두 결과를 다 매긴 채점자가 있는 건"만 세서 `kept`였다). `q`의 행을 `scores/sheet-q.csv`로 옮기면 그 파일은 AI 쪽 6건, 사람 쪽 6건이라 걸리지 않는다.
- 한 결과만 한쪽 채점자 장면: `p1`의 `d04`~`d12` 사람 결과 행을 채점자 `p4`로 바꾸고 `p2`·`p3` 행을 빼면, 그 9건의 사람 결과는 사람 결과만 매긴 `p4`만 봤다. 두 결과 모두 두 출처를 매긴 채점자의 행이 있어야 열·파일을 견주므로 그 9건은 견주지 않는다(한쪽이 빈 채 견주면 모든 행에 있는 파일 `scores/sheet.csv`가 9건 AI 쪽으로 잘못 걸린다).
- 번호 크기 장면: AI 키를 `b001`~`b024`, 사람 키를 `b103`~`b124`로 바꾼다. 틀·첫 글자·꼴은 같지만 12건 모두 AI 키가 작다(작은 쪽, 발견 `b001` 1건). 출처 표를 AI 결과부터 차례로 번호 매긴 실수와 같은 모양이다. 자릿수 장면: AI 키만 `b001`처럼 세 자리면 꼴 `a999`·`a99`가 갈린다(발견 둘).
- `,d` → `,z`는 출처 표·점수표의 건을 `z01`~`z12`로 바꾼다. reply-drafts는 대조가 `measured`라 기준 자료 키(`d01`~`d12`)에 없는 건이 `blind` 오류다(4.3 규칙 2, 리뷰 4차 N1. 고치기 전에는 통과 `PASS`). 점수표만(6.2)은 기준이 없어 보지 않는다.

### 4.5 오류

`CaseError`(`case-schema.md` 4.9) 그대로다. 문자열 형식·`Source`(케이스 폴더 기준 상대 경로)·`Path`(열 이름 또는 필드 경로) 규칙은 `metrics.md` 4.7·3.4와 같다. 오류가 하나라도 있으면 metrics.json·judgment.json을 내지 않는다(종료 2).

점수 오류 코드. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. `metrics.md` 4.7의 코드는 같은 뜻으로 점수 파일에도 쓴다(`file`, `pattern`, `overlap`, `encoding`, `empty`, `csv-syntax`, `field-count`, `column`, `duplicate-column`, `empty-key`, `duplicate-key`, `no-rows`). 아래는 새 코드와 점수 파일에서 문구가 다른 것이다.

| 점수 코드 | 언제 | 위치 | 메시지 |
| --- | --- | --- | --- |
| `score` | 점수가 정수 글이 아니거나 `min`~`max` 밖 | 그 칸, 경로 = `score` | `점수는 <min>~<max>의 정수다: <값>` |
| `source` | 출처가 `ai`·`human`이 아님 | 그 칸, 경로 = `source` | `출처는 ai 또는 human이다: <값>` |
| `blind` | 점수표의 가림 키가 출처 표에 없음 / 건이 출처 표와 다름 / 출처 표의 키에 점수가 없음 / 대조 `measured`인데 출처 표의 건이 기준 자료 키에 없음 | 그 칸(경로 `blind`) / 그 칸(경로 `item`) / 출처 표 그 행의 가림 키 칸(경로 `blind`) / 출처 표 그 행의 건 칸(경로 `item`) | `출처 표에 없는 가림 키다: <키>` / `출처 표의 건과 다르다: <건> (출처 표: <건>)` / `점수가 하나도 없다: <키>` / `기준 자료에 없는 건이다: <건>` |
| `pair` | 한 건에 `ai`·`human`이 하나씩이 아님 | 출처 표의 그 건 첫 행 `item` 칸, 경로 = `item` | `건마다 ai 하나와 human 하나가 있어야 한다: <건> (ai <N>, human <M>)` |
| `duplicate-key` | 출처 표에 같은 가림 키 / 점수표에 같은 채점자·가림 키 | 둘째 칸, 경로 = `blind` | `가림 키가 중복된다: <키> (처음: <파일>:<줄>)` / `점수가 중복된다: <채점자> <키> (처음: <파일>:<줄>)` |
| `no-rows` | 출처 표 데이터 행이 0 | `case.yaml`, 경로 = `scores.sources` | `출처 표에 데이터 행이 없다` |
| `overlap` | 점수 파일이 다른 점수 파일과 같음. 또는 대조가 `inputs` 파일을 읽을 때만(대조 `measured`), 점수 파일이 그 파일과 같음 | 둘째 파일 | `같은 파일이 두 번 나온다(패턴이 겹친다)` |

- 파일이 정해지기 전 오류(`pattern`, 폴더·파일 없음, `no-rows`)의 `Source`는 `case.yaml`, 경로는 `scores.sheet`·`scores.sources`다(`metrics.md` 3.4와 같은 방식).
- 순서: 대조 쪽(기준 → 결과, `metrics.md` 4.7) → 출처 표 → 점수표 → 점수가 없는 키(출처 표 순서). 쪽 안에서는 파일 경로 서수 → (줄, 열) → 찾은 순서.
- 체크리스트 열이 없으면 `column`이다(4.2 규칙 1). 체크리스트의 새 코드는 아래 `no-check` 하나다.
- 사용자에게 맞나: 사람 자료(출처 표·점수표, 기준 자료와 체크리스트 정의)의 결함은 오류로 멈춰 고치게 한다. AI 결과(결과 문장)의 결함은 오류가 아니라 셈과 판정이다. 가림이 깨진 것은 고쳐도 되돌아오지 않아 판정이다(4.4).

체크리스트 코드. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 리뷰 2차 B1에서 더했다(4.2 규칙 10).

| 체크리스트 코드 | 언제 | 위치 | 메시지 |
| --- | --- | --- | --- |
| `no-check` | `compare` 필드가 없고 `any` 항목이 없는 체크리스트에서, 기준 행의 `column` 항목 칸이 모두 빔 | 기준 파일:그 행 줄(열 0), 경로 = 빈 문자열 | `확인할 항목이 없다: <키> (column 항목 칸이 모두 비었다)` |

- `no-check`는 기준 쪽 오류다. 순서는 `metrics.md` 4.7 그대로(기준 파일 안 (줄, 열)). `field-count`·`empty-key`가 난 행과, 머리 행 오류로 데이터 행을 보지 않는 파일은 보지 않는다.

### 4.6 결정론·원본 불변·G1·G2·G3

- 모든 비교는 서수, 숫자는 정수와 `decimal`·`InvariantCulture`다. 난수·시각·환경 변수를 쓰지 않는다. 파일은 읽기만 한다.
- oracle은 문화권·해시 시드를 바꿔도 같은 바이트를 낸다 [T14:selftest 결정론]. reply-drafts CSV를 CRLF·CR·BOM·따옴표·끝 줄바꿈 없음·끝 빈 행·칸 앞뒤 공백으로 바꿔도 metrics.json·judgment.json 바이트가 같다 [T14:selftest CSV 변형].
- G2·G3 등록(제안, PR (b) test-engineer): 이름 `text-check-case-valid`, 표본 `TestData/guard/text-check-valid`(= 7.3 reply-drafts 파일. 새 샘플), 어댑터 `CaseParser.ParseDirectory` → `CompareEngine.CompareDirectory` → `JudgmentEngine.Judge`(시간·사람 입력 null) → 출력 폴더의 `metrics.json`, `judgment.json`. `Real_targets_use_case_samples_not_basic`의 정확한 쌍 목록에 한 쌍, 샘플 목록에 `text-check-valid`를 더한다(대상을 더함, 축소 아님).
- 한글 문장·평균 글이 출력에 들어가므로 R4(문화권)를 본다.

## 5. 판정 (`judgment.md` 4절에 더한다)

`judgment.md`의 표·문장은 그대로다. 점수표가 있을 때만 아래 행이 더해진다. 점수표가 없으면 판정 바이트가 지금과 같다 [T14:O]. 체크리스트는 대조 셈(정확도·누락률·검토 항목·몰림)으로 판정에 들어간다. 새 행이 필요 없다.

### 5.1 점수 문턱 기본값

`scores`가 있고 `thresholds.score_gap_max`가 없으면 기본값을 쓰고 `defaults`에 적는다(`judgment.md` 4.2 표 뒤 순서). 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다.

| 점수 문턱 | 기본값 | 근거 |
| --- | --- | --- |
| `score_gap_max` | `0` | 지시문에 값이 없다. "AI 결과 평균이 사람 결과 평균보다 낮지 않다"가 가장 보수적이다. 기본값 표시로 확인을 받는다 |

- `confirm_thresholds` 문구의 `{defaults}`에 `score_gap_max 0`이 든다.
- 점수표만(5.3)이면 `defaults`에는 이 길이 읽는 문턱만 적는다: 5.4 표의 점수표만 행에 나오는 `time_must_drop`, `score_gap_max`. `accuracy_min`·`missing_max`는 이 길이 읽지 않으므로 기본값이어도 `defaults`·`confirm_thresholds`·기본값 표시에 넣지 않는다. 대조를 더하면 그때 대조 길이 읽어 표시한다.
- `thresholds` 객체 모양은 그대로다(`judgment.md` 3.2 세 키 + `score_gap_max`). 점수표만에서 `accuracy_min`·`missing_max` 값(적은 값 또는 기본값)은 판정에 쓰이지 않는다.
- 표 대조와 점수표가 없는 판정은 `judgment.md` 4.2 그대로다(미검증 mixed-exceptions의 기본값 셋 포함). 예제 바이트가 그대로다.
- → 질문 14.

### 5.2 확인 `SCORE`

`judgment.md` 4.4 다섯 뒤 여섯째다. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. "읽는 문턱"이 기본값이면 확인 문장 끝에 기본값 표시(`judgment.md` 4.6)를 붙인다.

| 점수 확인 | 결과 | 걸리는 조건 | 읽는 문턱 | 확인 문장 틀 |
| --- | --- | --- | --- | --- |
| `SCORE` | `revealed` | `blind.status`가 `revealed` | — | `점수표에서 출처가 드러난 곳이 {findings}군데 있어 점수를 비교하지 않는다.` |
| `SCORE` | `met` | `ai.sum × human.ratings + score_gap_max × ai.ratings × human.ratings ≥ human.sum × ai.ratings` | `score_gap_max` | `AI 결과 평균 {ai_mean}점({ai_ratings}개)이 사람 결과 평균 {human_mean}점({human_ratings}개)보다 {score_gap_max}점 넘게 낮지 않다.` |
| `SCORE` | `not_met` | 그 밖 | `score_gap_max` | `AI 결과 평균 {ai_mean}점({ai_ratings}개)이 사람 결과 평균 {human_mean}점({human_ratings}개)보다 {score_gap_max}점 넘게 낮다.` |

- 셈은 정수와 `decimal`이다. 나눗셈이 없다. 같으면 `met`이다(경계 포함).
- 문장 값: `{findings}` = 발견 수. `{ai_mean}`·`{human_mean}` = 3.1의 평균 글. `{ai_ratings}`·`{human_ratings}` = 점수 수. `{score_gap_max}` = 쓴 문턱 글(`case.yaml` 글 그대로 또는 `0`).
- 평균이 사람 쪽만 높은지(한쪽 검정)만 본다. AI가 더 높아도 `met`이다.

문턱 0 문장. `score_gap_max` 값이 0이면(`0`, `0.0` 모두 값으로 본다) 위 확인 문장과 5.3 `SCORE` `not_met` 판정 문장 대신 이 틀을 쓴다. "0점 넘게"는 뜻이 없어서다. 걸리는 조건·읽는 문턱·기본값 표시는 위 표 그대로다. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다.

| 문턱 0 문장 | 근거 키 | 결과 | 문장 틀 |
| --- | --- | --- | --- |
| 확인 | `SCORE` | `met` | `AI 결과 평균 {ai_mean}점({ai_ratings}개)이 사람 결과 평균 {human_mean}점({human_ratings}개)보다 낮지 않다.` |
| 확인 | `SCORE` | `not_met` | `AI 결과 평균 {ai_mean}점({ai_ratings}개)이 사람 결과 평균 {human_mean}점({human_ratings}개)보다 낮다.` |
| 판정 | `SCORE` | `not_met` | `AI 결과 평균 {ai_mean}점이 사람 결과 평균 {human_mean}점보다 낮으므로 보류.` |

- `revealed` 문장은 문턱을 쓰지 않아 그대로다.
- 표시 평균은 반올림한 글이다. 문턱 0에서 두 평균 글이 같아도(예: 둘 다 `3.86`) 합·수 셈으로는 AI가 낮을 수 있다. 그때 문장은 "낮다"다. 셈이 기준이다(4.3 규칙 7).
- → 질문 15.

### 5.3 판정 행

`judgment.md` 4.3 표에 더한다. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. "자리"는 `judgment.md` 4.3의 순서다. `4 뒤`는 순서 4(`UNEXPLAINED` `found`) 뒤, 순서 5 앞이다. 길이 `점수표만`인 행은 그 순서의 기존 행 대신 쓴다.

| 자리 | 근거 키 | 확인 결과 | 길 | 판정 | 판정 문장 틀 |
| --- | --- | --- | --- | --- | --- |
| `4 뒤` | `SCORE` | `revealed` | `모두` | `hold` | `점수표에서 출처가 드러난 곳이 {findings}군데 있어 점수를 쓸 수 없으므로 보류.` |
| `4 뒤` | `SCORE` | `not_met` | `모두` | `hold` | `AI 결과 평균 {ai_mean}점이 사람 결과 평균 {human_mean}점보다 {score_gap_max}점 넘게 낮으므로 보류.` |
| `4 뒤` | `NOT_VERIFIED` | `no_compare_fields` | `점수표만` | `conditional` | `점수는 문턱을 충족하나 대조할 열(case.yaml compare·checklist)이 없어 기준·결과 자료(case.yaml inputs)를 쓰지 않았으므로 조건부.` |
| `5` | `TIME` | `not_measured` | `점수표만` | `conditional` | `점수는 문턱을 충족하나 시간을 재지 않아 줄었는지 알 수 없으므로 조건부.` |
| `5` | `TIME` | `not_met` | `점수표만` | `conditional` | `점수는 문턱을 충족하나 처리·검토 시간 {total}분이 적용 전 {before}분보다 짧지 않으므로 조건부.` |
| `8` | `PASS` | — | `점수표만` | `pass` | `점수가 문턱을 충족하고 조건부·보류 조건에 걸리지 않으므로 통과.` |

- 점수표만(대조가 미검증, 점수표 있음): 순서 1(`NOT_VERIFIED`)에 그 자리에서 걸리지 않는다. 대조 확인 넷(`ACCURACY`, `MISSING`, `UNEXPLAINED`, `FOCUS`)이 없다. `checks`는 `TIME`, `SCORE`다. 판정은 `SCORE` 두 행 → `NOT_VERIFIED` `no_compare_fields`(위 행) → `TIME`(위 두 행) → `PASS`(위 행) 순서다.
- 대조할 열 없음(`NOT_VERIFIED` `no_compare_fields`, 점수표만): 사용자가 `inputs`에 기준·결과 자료를 적었는데 `compare`·`checklist`가 없다. 그 자료를 쓰지 않은 채 통과를 내면 자료가 말없이 버려진다. 그래서 통과가 아니라 조건부이고 다음 할 일 `add_compare`가 나온다(5.5). "확인 결과" 칸은 `judgment.md` 4.3 순서 1처럼 대조 결과의 `reason`이다. 근거 키·결과 값은 순서 1의 것을 그대로 쓴다(새 값이 없다).
- 이 행의 자리: 점수 보류(`SCORE` 두 행) 뒤다. 보류 → 조건부 → 통과 순서(`judgment.md` 4.1 해석 3)를 지킨다. 점수가 미달이면 보류가 먼저 보이고 `add_compare`는 할 일에 남는다. `TIME`보다 앞이다. 시간은 `checks`의 `TIME`에 남지만 자료를 쓰지 않은 것은 확인이 없어 판정 문장이 아니면 보이지 않는다(`judgment.md` 4.3 "확인 다섯은 checks에 모두 남으므로"와 같은 까닭).
- `inputs`가 없는 점수표만(`no_inputs`)은 사용자가 점수표만으로 재겠다고 정한 것이다. 이 행에 걸리지 않고 지금처럼 `TIME`·`PASS`로 간다.
- 표 대조 + 점수표: `checks`는 다섯 + `SCORE`. 판정은 `judgment.md` 4.3 순서 2~4 → `SCORE` 두 행 → 순서 5~8(기존 문장 그대로)이다. 정확도 미달(2)이 가림(4 뒤)보다 앞이다. 둘 다 보류이고, 정확도 문장이 더 구체적이다.
- 가림이 깨지면 정확도·시간보다 늦게, 조건부보다 앞에 보류다. 점수를 쓸 수 없는데 통과·조건부를 내면 안 된다.
- 점수표만이면서 사람 입력(검토 항목 키)을 받으면 `ArgumentException`이다. 검토 항목이 없다.
- → 질문 8, 13.

### 5.4 읽은 문턱

판정 문장의 기본값 표시(`judgment.md` 4.6). 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 길 `표 대조`는 대조함 + 점수표, `점수표만`은 5.3과 같다. "확인 결과"가 `—`이면 결과와 상관없다. 자리 `5~8`은 `judgment.md` 4.3 순서 5~8 모든 행이다(그 행의 읽은 문턱 대신).

| 자리 | 근거 키 | 확인 결과 | 길 | 읽은 문턱 |
| --- | --- | --- | --- | --- |
| `4 뒤` | `SCORE` | `revealed` | `표 대조` | `accuracy_min`, `missing_max` |
| `4 뒤` | `SCORE` | `not_met` | `표 대조` | `accuracy_min`, `missing_max`, `score_gap_max` |
| `5~8` | — | — | `표 대조` | `accuracy_min`, `missing_max`, `time_must_drop`, `score_gap_max` |
| `4 뒤` | `SCORE` | `revealed` | `점수표만` | — |
| `4 뒤` | `SCORE` | `not_met` | `점수표만` | `score_gap_max` |
| `4 뒤` | `NOT_VERIFIED` | `no_compare_fields` | `점수표만` | `score_gap_max` |
| `5` | `TIME` | — | `점수표만` | `time_must_drop`, `score_gap_max` |
| `8` | `PASS` | — | `점수표만` | `time_must_drop`, `score_gap_max` |

- 점수표가 없으면 `judgment.md` 4.6 표 그대로다.
- 점수표만의 `defaults`는 이 표의 점수표만 행에 나오는 문턱만 본다(5.1).

### 5.5 다음 할 일

`judgment.md` 4.7 표에 더한다. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 자리는 `add_group` 뒤, `confirm_thresholds` 앞이다.

| 점수 할 일 | 나오는 때 | 문구 |
| --- | --- | --- |
| `rescore_blind` | `SCORE` = `revealed` | `출처가 드러나지 않게 가림 키를 새로 붙이고 점수를 다시 매긴다.` |

- 점수표만이면 대조 쪽 할 일(`add_data`, `review_items`, `add_group`)은 나오지 않는다. `add_compare`(미검증 이유가 `no_compare_fields`일 때, 판정과 상관없이), `measure_time`(`TIME` = `not_measured`), `rescore_blind`, `confirm_thresholds`(5.1의 읽는 문턱만)가 나온다. `inputs`가 없으면 점수표만으로 잰다고 사용자가 정한 것이다.
- `add_compare` 문구는 `judgment.md` 4.7 그대로다("case.yaml compare에 대조할 열과 허용 오차를 적는다."). 미검증 예제 mixed-exceptions의 바이트가 그 문구를 담고 있다. `checklist`도 된다는 것은 판정 문장이 말한다.

### 5.6 정의와 대조 결과

`judgment.md` 5절 표에 더한다(거절 = `ArgumentException`).

- `checklist`가 있으면 대조 결과가 `measured`이고 `compare.checklist.text`·항목 이름(순서)이 정의와 같아야 한다. 없으면 대조 결과에 `checklist`가 없어야 한다.
- `inputs`가 있고 `compare`가 비고 `checklist`가 없을 때만 미검증 `no_compare_fields`다.
- 점수표 집계는 정의에 `scores`가 있을 때만 있어야 한다.

## 6. 예시

### 6.1 판정 장면

테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. "바탕"은 7.3 reply-drafts 파일 또는 6.2 `score-only` 폴더다. "바꾼 곳"의 모양은 4.4 장면 표와 같다(`case.yaml`은 그 파일). 파일이 다른 묶음은 `; `로 잇는다(4.2 체크리스트 장면 표와 같다). 시간·사람 입력은 null이다. "확인 결과"는 `checks` 순서다.

| 장면 | 바탕 | 바꾼 곳 | 판정 | 근거 키 | 확인 결과 | 다음 할 일 |
| --- | --- | --- | --- | --- | --- | --- |
| 예제 그대로 | `reply-drafts` | (없음) | `conditional` | `FOCUS` | `met`, `met`, `not_reviewed`, `off`, `found`, `met` | `review_items` |
| 점수 문턱 기본값 | `reply-drafts` | `case.yaml`: `  score_gap_max: 0.5\n` → `` | `hold` | `SCORE` | `met`, `met`, `not_reviewed`, `off`, `found`, `not_met` | `review_items`, `confirm_thresholds` |
| 가림 키에 출처 | `reply-drafts` | `scores/sources.csv`, `scores/sheet.csv`: `b01,` → `b01-ai,` | `hold` | `SCORE` | `met`, `met`, `not_reviewed`, `off`, `found`, `revealed` | `review_items`, `rescore_blind` |
| 정확도 미달이 먼저 | `reply-drafts` | `case.yaml`: `  accuracy_min: 80\n` → `  accuracy_min: 90\n` | `hold` | `ACCURACY` | `not_met`, `met`, `not_reviewed`, `off`, `found`, `met` | `review_items` |
| 유형 열 없음 | `reply-drafts` | `case.yaml`: `  group: topic\n` → `` | `conditional` | `UNEXPLAINED` | `met`, `met`, `not_reviewed`, `off`, `off`, `met` | `review_items`, `add_group` |
| 빠진 항목 하나 | `reply-drafts` | `candidate/replies.csv`: `문의하신 건을` → `문의하신 q397 건을` | `conditional` | `UNEXPLAINED` | `met`, `met`, `not_reviewed`, `off`, `few`, `met` | `review_items` |
| 체크리스트 다 있음 | `reply-drafts` | `candidate/replies.csv`: `문의하신 건을` → `문의하신 q397 건을`, `처리 절차를` → `환불 접수 절차를` | `pass` | `PASS` | `met`, `met`, `none`, `off`, `few`, `met` | — |
| 점수표만 | `score-only` | (없음) | `pass` | `PASS` | `off`, `met` | — |
| 점수표만, 문턱 기본값 | `score-only` | `case.yaml`: `  score_gap_max: 0.5\n` → `` | `hold` | `SCORE` | `off`, `not_met` | `confirm_thresholds` |
| 점수표만, 시간 조건 | `score-only` | `case.yaml`: `  time_must_drop: false\n` → `  time_must_drop: true\n` | `conditional` | `TIME` | `not_measured`, `met` | `measure_time` |
| 점수표만, 가림 깨짐 | `score-only` | `scores/sources.csv`, `scores/sheet.csv`: `b02,` → `human-02,` | `hold` | `SCORE` | `off`, `revealed` | `rescore_blind` |
| 점수표만, 출처마다 다른 키 | `score-only` | `scores/sources.csv`, `scores/sheet.csv` (출처 `ai` 행): `b` → `x`; `scores/sources.csv`, `scores/sheet.csv` (출처 `human` 행): `b` → `y` | `hold` | `SCORE` | `off`, `revealed` | `rescore_blind` |
| 점수표만, 출처마다 다른 열 | `score-only` | `scores/sheet.csv`: `score\n` → `score,writer\n`; `scores/sheet.csv` (출처 `ai` 행): `\n` → `,gpt\n`; `scores/sheet.csv` (출처 `human` 행): `\n` → `,staff\n` | `hold` | `SCORE` | `off`, `revealed` | `rescore_blind` |
| 점수표만, 출처마다 다른 파일 | `score-only` | `scores/sheet.csv`의 출처 `ai` 행 → `scores/sheet2.csv` | `hold` | `SCORE` | `off`, `revealed` | `rescore_blind` |
| 점수표만, 쓰지 않는 문턱 기본값 | `score-only` | `case.yaml`: `  accuracy_min: 80\n  missing_max: 5\n` → `` | `pass` | `PASS` | `off`, `met` | — |
| 점수표만, 대조 열 없음 | `reply-drafts` | `case.yaml`: `checklist:\n  text: reply\n  items:\n    - {item: ticket, column: ticket}\n    - {item: action, column: action}\n    - {item: closing, any: [감사합니다, 고맙습니다]}\n` → `` | `conditional` | `NOT_VERIFIED` | `off`, `met` | `add_compare` |
| 점수표만, 대조 열 없음, 시간 조건 | `reply-drafts` | `case.yaml`: `checklist:\n  text: reply\n  items:\n    - {item: ticket, column: ticket}\n    - {item: action, column: action}\n    - {item: closing, any: [감사합니다, 고맙습니다]}\n` → ``, `  time_must_drop: false\n` → `  time_must_drop: true\n` | `conditional` | `NOT_VERIFIED` | `not_measured`, `met` | `add_compare`, `measure_time` |
| 점수표만, 대조 열 없음, 점수 미달 | `reply-drafts` | `case.yaml`: `checklist:\n  text: reply\n  items:\n    - {item: ticket, column: ticket}\n    - {item: action, column: action}\n    - {item: closing, any: [감사합니다, 고맙습니다]}\n` → ``, `  score_gap_max: 0.5\n` → `` | `hold` | `SCORE` | `off`, `not_met` | `add_compare`, `confirm_thresholds` |
| 번호 칸 빈 행, 빈 답변 | `reply-drafts` | `case.yaml`: `    - {item: action, column: action}\n` → ``; `reference/replies.csv`: `,q397,` → `,,`; `candidate/replies.csv`: `d12,안녕하세요. 문의하신 건을 확인했습니다. 환불 접수 절차를 안내드립니다. 추가로 보낼 서류는 없습니다. 감사합니다.` → `d12,` | `conditional` | `UNEXPLAINED` | `met`, `met`, `not_reviewed`, `off`, `few`, `met` | `review_items` |

- "빠진 항목 하나": `d12`에 문의 번호를 넣으면 불일치가 `d03` 하나다. `focus_min`(2)보다 적어 `few`다. 정확도 11/12 = 91.7%.
- "점수 문턱 기본값": 판정 문장과 `SCORE` 확인 문장 끝에 기본값 표시가 붙는다. 문턱이 0이라 문턱 0 문장(5.2)이다. 판정 문장은 `AI 결과 평균 3.86점이 사람 결과 평균 4.19점보다 낮으므로 보류. (기본값 사용, 확인 필요)`다 [T14:O].
- "점수표만, 쓰지 않는 문턱 기본값": `accuracy_min`·`missing_max`를 지워 기본값(95·2)이 되어도 점수표만은 그 둘을 읽지 않는다. `defaults`가 `[]`이고 `confirm_thresholds`가 없다(5.1) [T14:O].
- "점수표만, 대조 열 없음": PR (a) 리뷰에서 찾은 장면이다. 체크리스트를 지우면 `inputs`는 있는데 대조할 열이 없다(미검증 `no_compare_fields`). 고치기 전에는 통과 `PASS`, 할 일 없음이었다. 이제 판정 문장은 `점수는 문턱을 충족하나 대조할 열(case.yaml compare·checklist)이 없어 기준·결과 자료(case.yaml inputs)를 쓰지 않았으므로 조건부.`다 [T14:O].
- "…시간 조건": 대조할 열 없음이 `TIME`보다 앞이다. `TIME` `not_measured`는 `checks`와 `measure_time`에 남는다. "…점수 미달": 점수 보류가 먼저이고 `add_compare`는 할 일에 남는다(5.3 자리).
- "점수표만": `checks`가 둘이다. `NOT_VERIFIED`가 아니다(카드 수용 기준 1).
- "점수표만, 출처마다 다른 키·열·파일": [재검토]에서 다시 연 장면이다(리뷰 4차 B1). 가림 키를 출처마다 `x`·`y`로, `writer` 열을 출처마다 `gpt`·`staff`로, 점수표를 출처마다 다른 파일로 나눴다. 고치기 전에는 셋 다 가림 `kept`, 통과 `PASS`였다. 이제 `source_pattern`이 둘씩 걸려 판정 문장은 `점수표에서 출처가 드러난 곳이 2군데 있어 점수를 쓸 수 없으므로 보류.`다 [T14:O].
- "번호 칸 빈 행, 빈 답변": 리뷰 2차 B1에서 찾은 장면의 받는 쪽이다. `d12`의 번호 칸이 비어 `ticket`은 요구하지 않지만 `closing`(`any`)이 남는다. 빈 답변은 `closing`이 없어 값 불일치다(`missing_items` = `closing`). 정확도 11/12 = 91.7% [T14:O]. 체크리스트가 그 행에 아무것도 요구하지 않으면(번호 항목만 남김) 일치로 세지 않고 `no-check` 오류다(4.2 규칙 10, 체크리스트 장면 표). 고치기 전에는 그 행이 결과 문장과 상관없이 일치였다.

### 6.2 `score-only`

합성 예시. 실제 값 없음. 7.3 reply-drafts의 `scores/` 두 파일과 아래 `case.yaml`로 만든다. 예제가 아니다(테스트·oracle 점검용).

`score-only/case.yaml`:

```yaml
# 합성 예시. 실제 업무·값 없음. 점수표만 있는 케이스(T-014). 점수 자료는 reply-drafts와 같다.
name: score-only
task: 점수표만 있는 합성 예시
steps:
  - name: 답변 초안 쓰기
scores:
  sheet: scores/sheet*.csv
  sources: scores/sources.csv
  min: 1
  max: 5
thresholds:
  accuracy_min: 80
  missing_max: 5
  time_must_drop: false
  score_gap_max: 0.5
```

- 대조는 미검증 `no_inputs`다. metrics.json `compare`는 `metrics.md` 7.4 모양, 그 뒤에 `scores`다.

### 6.3 reply-drafts 결과

- 체크리스트: 12건 중 `d03`(처리 안내 `action` 빠짐), `d12`(문의 번호 `ticket` 빠짐)가 값 불일치다. 둘 다 `topic = 환불`이다. 정확도 10/12 = 83.3%, 누락 0 [T14:O].
- 점수표: 채점자 3명 × 결과 24개 = 72점. AI 평균 3.86(139/36), 사람 평균 4.19(151/36). 차이 0.33이 문턱 0.5 안이라 `met`. 건별로 AI가 높음 3, 같음 0, 낮음 9 [T14:O].
- 판정: 조건부 `FOCUS`. 빠진 항목이 환불 답변에만 있다. 점수는 문턱 안이지만 건별로는 AI가 낮은 건이 많다(사람이 `paired`를 본다).
- 생성기가 빠진 항목이 있는 AI 답변 점수에서 1점을 뺐다(7.2). 그래서 두 길이 같은 결함을 본다.

## 7. 예제 reply-drafts

### 7.1 만드는 방법과 확정 절차

1. mixed-exceptions는 바꾸지 않는다. 지시문 12절이 "자료 없음 → 미검증"으로 정한 예제이고, 바꾸면 expected 3개가 바뀐다. 새 예제 `reply-drafts`를 더한다. 같은 일(문의 메일 답변)을 AI 적용 뒤에 잰 모양이다. → 질문 10.
2. 자료 파일(`reference/`, `candidate/`, `scores/`)은 합성 생성기(7.2)가 만든다(지시문 2절 10). `case.yaml`·`answers.yaml`·`intake.md`는 손으로 쓴 글이다(`synthetic-examples.md` 4.8·4.9와 같다).
3. expected 블록 셋(7.4)은 oracle이 7.3 파일에서 낸 바이트다 [T14:O]. 엔진 출력이 아니다. C# 엔진은 PR (b)에서 이 블록에 맞춘다(지시문 2절 11 "기대값을 먼저").
4. PR (a)(이 스펙·oracle)에는 `examples/`가 없다. PR (b)에서 test-engineer가 7.3·7.4 블록을 바이트 그대로 `examples/reply-drafts/`로 옮긴다. 기존 expected 12개는 그대로라 migration 커밋이 없다. 새 expected 셋은 새 파일(A)이라 regression-guard `expected-added` 규칙만 받는다: base oracle(이 PR (a)가 머지된 main)이 재현해야 한다. 커밋 메시지 조건은 없다 [읽음 `tools/ci/README.md`].
5. oracle-and-expected: PR (b)는 oracle을 바꾸지 않는다. PR (b)의 base oracle(이 PR (a)가 머지된 main)이 새 expected 셋을 재현한다 [T14:S4 PR (b) 모의: 15 MATCH].
6. `examples/.gitattributes`(`* -text`)가 새 파일에도 걸린다. 한글 CSV도 `\r` 없음 검사(`Example_text_files_have_no_carriage_return`)와 실제 값 패턴 검사(`synthetic-examples.md` 4.10)에 걸리지 않게 만들었다(문의 번호 `q` + 숫자 3자리) [T14:S6].

새 예제 표. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 이 표의 예제는 `examples/`에 없어도 된다(PR (b) 전). 있으면 파일이 7.3·7.4 블록과 같아야 한다.

| 새 예제 | inputs.group | 판정 | 근거 키 | 확인 여섯 | 다음 할 일 | 검토 항목 |
| --- | --- | --- | --- | --- | --- | --- |
| `reply-drafts` | `topic` | `conditional` | `FOCUS` | `met`, `met`, `not_reviewed`, `off`, `found`, `met` | `review_items` | 불일치 2건 |

예제 파일. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 블록은 7.3·7.4에 이 순서로 있다.

| 예제 파일 | 만드는 곳 |
| --- | --- |
| `case.yaml` | 손으로 쓴 글(7.3) |
| `answers.yaml` | 손으로 쓴 글(7.3). 단계·답은 `case.yaml`과 같다 |
| `intake.md` | 손으로 쓴 글(7.3) |
| `reference/replies.csv` | 생성기(7.2) |
| `candidate/replies.csv` | 생성기(7.2) |
| `scores/sources.csv` | 생성기(7.2) |
| `scores/sheet.csv` | 생성기(7.2) |
| `expected/classification.json` | oracle(7.4) |
| `expected/metrics.json` | oracle(7.4) |
| `expected/judgment.json` | oracle(7.4) |

### 7.2 합성 생성기

`synthetic-examples.md` 4.2의 SplitMix64와 `NextBelow`를 그대로 쓴다. 문장 생성기는 그 생성기와 다른 것이다(숫자·선택 열이 아니라 문장 조각을 고른다). PR (b)에서 core-dev가 Core에 둔다(제안 이름 `Scorecard.Core.Synthetic.SyntheticReplies`). 테스트가 다시 만들어 7.3 블록과 바이트로 비교한다(T-005 방식). → 질문 11.

계획. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 목록은 `, `로 잇고 순서가 뜻이다.

| 계획 | 값 |
| --- | --- |
| 시드 | `2` |
| 행 수 | `12` |
| 키 | `reply_id`, `d` + 2자리(`d01`~`d12`) |
| 유형 | `배송`, `환불`, `교환` |
| 처리 안내 | `배송` → `배송 조회`, `환불` → `환불 접수`, `교환` → `교환 접수` |
| 빠뜨릴 유형 | `환불` |
| 문의 번호 | `q` + (`100` + `NextBelow(900)`) |
| 인사 | `안녕하세요.`, `반갑습니다.` |
| 번호 문장 | `문의하신 {ticket} 건을 확인했습니다.` |
| 번호 빠진 문장 | `문의하신 건을 확인했습니다.` |
| 안내 문장 | `{action} 절차를 안내드립니다.` |
| 안내 빠진 문장 | `처리 절차를 안내드립니다.` |
| 덧붙임 | `진행 상황은 문자로 알려 드립니다.`, `추가로 보낼 서류는 없습니다.`, `궁금한 점은 이 메일에 회신해 주세요.` |
| 끝인사 | `감사합니다.`, `고맙습니다.` |
| 채점자 | `p1`, `p2`, `p3` |
| 가림 키 | `b` + 2자리(`b01`~`b24`) |
| AI 점수 | `3`, `4`, `4`, `5` |
| 사람 점수 | `3`, `4`, `5`, `5` |
| 빠진 항목 감점 | `1` |

뽑는 순서(한 번에 `NextBelow`를 한 번 쓴다):

1. 유형: 행마다 `유형[NextBelow(3)]`.
2. 문의 번호: 행마다.
3. 행마다 인사 `NextBelow(2)`, 덧붙임 `NextBelow(3)`, 끝인사 `NextBelow(2)`(이 순서).
4. 빠뜨릴 행: 유형이 `환불`인 행(키 순서) 목록에서 `NextBelow(수)`번째가 번호를 빠뜨린다. 그 행을 뺀 목록에서 `NextBelow(수)`번째가 안내를 빠뜨린다. 목록이 2보다 짧으면 예외.
5. 결과 행 순서: Fisher–Yates(i = 11..1마다 `NextBelow(i + 1)` 자리와 바꿈, `synthetic-examples.md` 4.5 7과 같다).
6. 가림 키: (행, `ai`), (행, `human`)을 행 순서로 24개 늘어놓고 5와 같이 섞는다. k번째(0부터)가 `b` + (k + 1) 2자리다.
7. 점수: 채점자 순서 → 가림 키 순서마다. AI 결과는 `AI 점수[NextBelow(4)]`(빠뜨린 행이면 감점을 뺀다), 사람 결과는 `사람 점수[NextBelow(4)]`.

파일 모양:

- 문장 = 인사, 번호 문장, 안내 문장, 덧붙임, 끝인사를 공백 하나로 잇는다.
- `reference/replies.csv`: `reply_id,topic,ticket,action`, 키 순서. `candidate/replies.csv`: `reply_id,reply`, 5의 순서. `scores/sources.csv`: `blind,item,source`, 가림 키 순서. `scores/sheet.csv`: `rater,item,blind,score`, 7의 순서.
- CSV 바이트: UTF-8(BOM 없음), LF, 끝 LF, 머리 행 먼저, 따옴표 없음. 칸은 비지 않고, 앞뒤 공백·`,`·`"`·LF·CR·탭이 없다(어기면 예외). `synthetic-examples.md` 3.3과 다른 점은 한글을 받는 것 하나다.
- 시드: 조건을 만족하는 가장 작은 값이다(`synthetic-examples.md`와 같다). 조건: 세 유형이 모두 2행 이상, AI 평균 < 사람 평균, 그리고 차이가 0.5 이하(셈으로). 시드 0은 점수 차이가 0.5를 넘는다(136/36, 157/36). 시드 1은 `배송`이 1행이다 [T14:G].
- 같은 시드면 같은 바이트다. 두 번 돌려 `diff`가 없다 [T14:S1].

### 7.3 예제 파일

`examples/reply-drafts/case.yaml` (SHA-256 `c44a4d0792df380f6e638d6bcdc17bc4155936b4cc723b0ea81d7e3d31e56390`, 1,640바이트):

```yaml
# 공개 합성 예제. 실제 업무·값 없음. 정답 표가 없는 문장 결과(답변 초안)를 체크리스트와 가림 점수표로 잰다(T-014).
# 단계·답은 answers.yaml과 같다. reference/·candidate/·scores/는 합성 생성기로 만들었다.
name: reply-drafts
task: 문의 메일에 AI가 답변 초안을 쓰고 사람이 고쳐 보낸다
steps:
  - name: 문의 접수 기록
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: daily, batch_size: 12, exception_rate: 5, current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 답변 초안 쓰기
    answers: {input_kind: free_doc, rule_expressible: partial, frequency: daily, batch_size: 12, exception_rate: 20, current_actor: ai_each_time, failure_impact: external, existing_feature: no, human_signoff: no}
  - name: 답변 확인·발송
    answers: {input_kind: free_doc, rule_expressible: no, frequency: daily, batch_size: 12, exception_rate: 10, current_actor: human, failure_impact: external, existing_feature: no, human_signoff: yes}
inputs:
  reference: reference/*.csv
  candidate: candidate/*.csv
  key: reply_id
  group: topic
checklist:
  text: reply
  items:
    - {item: ticket, column: ticket}
    - {item: action, column: action}
    - {item: closing, any: [감사합니다, 고맙습니다]}
scores:
  sheet: scores/sheet*.csv
  sources: scores/sources.csv
  min: 1
  max: 5
# 시간 측정 자료가 없는 예제다. 시간 조건은 끄고 체크리스트·점수만 본다.
thresholds:
  accuracy_min: 80
  missing_max: 5
  time_must_drop: false
  score_gap_max: 0.5
```

`examples/reply-drafts/answers.yaml` (SHA-256 `141c3d469c5a8fd486125ccd7b14d29f304528bca839c67d71430ab56859c246`, 919바이트):

```yaml
# 공개 합성 예제. 실제 업무·값 없음. 정답 표가 없는 문장 결과(T-014).
name: reply-drafts
task: 문의 메일에 AI가 답변 초안을 쓰고 사람이 고쳐 보낸다
steps:
  - name: 문의 접수 기록
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: daily, batch_size: 12, exception_rate: 5, current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 답변 초안 쓰기
    answers: {input_kind: free_doc, rule_expressible: partial, frequency: daily, batch_size: 12, exception_rate: 20, current_actor: ai_each_time, failure_impact: external, existing_feature: no, human_signoff: no}
  - name: 답변 확인·발송
    answers: {input_kind: free_doc, rule_expressible: no, frequency: daily, batch_size: 12, exception_rate: 10, current_actor: human, failure_impact: external, existing_feature: no, human_signoff: yes}
```

`examples/reply-drafts/intake.md` (SHA-256 `d98b6780ac12a5f37f5628a84a92ccd362301eb67ea21e0639911b2700301665`, 515바이트):

```text
문의 메일에 답변을 쓴다. 하루 12통쯤 온다.
지금은 AI가 답변 초안을 쓰고 사람이 고쳐서 보낸다.

답변에는 문의 번호와 처리 안내가 꼭 들어가야 한다. 끝인사도 있어야 한다.
정답 표가 없어 결과를 표처럼 대조할 수 없다.

그래서 메일마다 꼭 들어갈 항목을 표로 적었다.
같은 메일에 사람이 쓴 답변과 AI 답변을 섞어 가림 키를 붙였다. 세 사람이 누가 썼는지 모른 채 1~5점으로 매겼다.
```

`examples/reply-drafts/reference/replies.csv` (SHA-256 `01664fa32a7f091a29e0ca14341c9de3776cb1b168faa07e1282b8becea5eb2f`, 389바이트):

```csv
reply_id,topic,ticket,action
d01,환불,q600,환불 접수
d02,교환,q436,교환 접수
d03,환불,q938,환불 접수
d04,교환,q283,교환 접수
d05,배송,q280,배송 조회
d06,환불,q427,환불 접수
d07,교환,q440,교환 접수
d08,교환,q289,교환 접수
d09,배송,q143,배송 조회
d10,교환,q574,교환 접수
d11,환불,q442,환불 접수
d12,환불,q397,환불 접수
```

`examples/reply-drafts/candidate/replies.csv` (SHA-256 `bf0e04985c041e04870a3c68910e084a701e849751afdd6a067a4b4e0a31bbd6`, 2,102바이트):

```csv
reply_id,reply
d03,반갑습니다. 문의하신 q938 건을 확인했습니다. 처리 절차를 안내드립니다. 추가로 보낼 서류는 없습니다. 감사합니다.
d07,안녕하세요. 문의하신 q440 건을 확인했습니다. 교환 접수 절차를 안내드립니다. 궁금한 점은 이 메일에 회신해 주세요. 고맙습니다.
d09,안녕하세요. 문의하신 q143 건을 확인했습니다. 배송 조회 절차를 안내드립니다. 추가로 보낼 서류는 없습니다. 감사합니다.
d10,반갑습니다. 문의하신 q574 건을 확인했습니다. 교환 접수 절차를 안내드립니다. 진행 상황은 문자로 알려 드립니다. 고맙습니다.
d08,안녕하세요. 문의하신 q289 건을 확인했습니다. 교환 접수 절차를 안내드립니다. 궁금한 점은 이 메일에 회신해 주세요. 고맙습니다.
d12,안녕하세요. 문의하신 건을 확인했습니다. 환불 접수 절차를 안내드립니다. 추가로 보낼 서류는 없습니다. 감사합니다.
d02,반갑습니다. 문의하신 q436 건을 확인했습니다. 교환 접수 절차를 안내드립니다. 진행 상황은 문자로 알려 드립니다. 감사합니다.
d05,안녕하세요. 문의하신 q280 건을 확인했습니다. 배송 조회 절차를 안내드립니다. 진행 상황은 문자로 알려 드립니다. 고맙습니다.
d06,안녕하세요. 문의하신 q427 건을 확인했습니다. 환불 접수 절차를 안내드립니다. 궁금한 점은 이 메일에 회신해 주세요. 감사합니다.
d01,반갑습니다. 문의하신 q600 건을 확인했습니다. 환불 접수 절차를 안내드립니다. 궁금한 점은 이 메일에 회신해 주세요. 고맙습니다.
d04,안녕하세요. 문의하신 q283 건을 확인했습니다. 교환 접수 절차를 안내드립니다. 궁금한 점은 이 메일에 회신해 주세요. 감사합니다.
d11,반갑습니다. 문의하신 q442 건을 확인했습니다. 환불 접수 절차를 안내드립니다. 진행 상황은 문자로 알려 드립니다. 고맙습니다.
```

`examples/reply-drafts/scores/sources.csv` (SHA-256 `104105d671c7fe633e848a4f116913ac0692085280eb3e4fde379cdf98012022`, 318바이트):

```csv
blind,item,source
b01,d11,ai
b02,d07,ai
b03,d12,human
b04,d01,ai
b05,d09,ai
b06,d06,human
b07,d08,ai
b08,d10,human
b09,d04,ai
b10,d05,human
b11,d10,ai
b12,d12,ai
b13,d02,human
b14,d01,human
b15,d02,ai
b16,d03,human
b17,d09,human
b18,d07,human
b19,d06,ai
b20,d04,human
b21,d05,ai
b22,d03,ai
b23,d08,human
b24,d11,human
```

`examples/reply-drafts/scores/sheet.csv` (SHA-256 `8d1878c67c264a20da2e6dc6e72d29cb070a9e423eee335acef5666a468ff8cb`, 959바이트):

```csv
rater,item,blind,score
p1,d11,b01,4
p1,d07,b02,5
p1,d12,b03,3
p1,d01,b04,4
p1,d09,b05,4
p1,d06,b06,5
p1,d08,b07,4
p1,d10,b08,5
p1,d04,b09,4
p1,d05,b10,4
p1,d10,b11,4
p1,d12,b12,4
p1,d02,b13,3
p1,d01,b14,5
p1,d02,b15,4
p1,d03,b16,3
p1,d09,b17,4
p1,d07,b18,5
p1,d06,b19,4
p1,d04,b20,5
p1,d05,b21,5
p1,d03,b22,2
p1,d08,b23,5
p1,d11,b24,4
p2,d11,b01,4
p2,d07,b02,3
p2,d12,b03,4
p2,d01,b04,4
p2,d09,b05,5
p2,d06,b06,4
p2,d08,b07,4
p2,d10,b08,4
p2,d04,b09,4
p2,d05,b10,4
p2,d10,b11,4
p2,d12,b12,3
p2,d02,b13,5
p2,d01,b14,5
p2,d02,b15,4
p2,d03,b16,3
p2,d09,b17,3
p2,d07,b18,5
p2,d06,b19,4
p2,d04,b20,5
p2,d05,b21,4
p2,d03,b22,4
p2,d08,b23,5
p2,d11,b24,5
p3,d11,b01,4
p3,d07,b02,5
p3,d12,b03,4
p3,d01,b04,3
p3,d09,b05,3
p3,d06,b06,5
p3,d08,b07,4
p3,d10,b08,3
p3,d04,b09,3
p3,d05,b10,5
p3,d10,b11,3
p3,d12,b12,3
p3,d02,b13,3
p3,d01,b14,3
p3,d02,b15,5
p3,d03,b16,5
p3,d09,b17,3
p3,d07,b18,5
p3,d06,b19,4
p3,d04,b20,5
p3,d05,b21,5
p3,d03,b22,2
p3,d08,b23,3
p3,d11,b24,4
```

### 7.4 expected

`examples/reply-drafts/expected/classification.json` (SHA-256 `7ede39aafd1785287aed52b21ecbf9b26d48a6ee7584663bad6ea7e52fbaaba0`, 1,853바이트):

```json
{
  "schema_version": 1,
  "case": "reply-drafts",
  "steps": [
    {
      "number": 1,
      "name": "문의 접수 기록",
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
      "name": "답변 초안 쓰기",
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
          "rule": "Q3",
          "reason": "입력이 자유 형식 문서이므로(free_doc) 입력 양식 표준화를 먼저 검토하고, 안 되면 실행 시 AI 보조로 분류. 해석만 AI, 계산·저장은 코드로 한다."
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
      "name": "답변 확인·발송",
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

`examples/reply-drafts/expected/metrics.json` (SHA-256 `223ebc125fbc84c555f0c941fc1cee4e0edf60d1c28925d1a1de367f92c4b50f`, 3,820바이트):

```json
{
  "schema_version": 1,
  "case": "reply-drafts",
  "compare": {
    "status": "measured",
    "reason": null,
    "key": "reply_id",
    "reference_files": [
      "reference/replies.csv"
    ],
    "candidate_files": [
      "candidate/replies.csv"
    ],
    "counts": {
      "reference": 12,
      "candidate": 12,
      "match": 10,
      "mismatch": 2,
      "missing": 0,
      "extra": 0,
      "within": 0
    },
    "accuracy_pct": 83.3,
    "missing_rate_pct": 0.0,
    "fields": [],
    "checklist": {
      "text": "reply",
      "entries": [
        {
          "item": "ticket",
          "column": "ticket",
          "phrases": [],
          "required": 12,
          "found": 11,
          "absent": 1
        },
        {
          "item": "action",
          "column": "action",
          "phrases": [],
          "required": 12,
          "found": 11,
          "absent": 1
        },
        {
          "item": "closing",
          "column": null,
          "phrases": [
            "감사합니다",
            "고맙습니다"
          ],
          "required": 12,
          "found": 12,
          "absent": 0
        }
      ]
    },
    "items": [
      {
        "key": "d03",
        "status": "mismatch",
        "reference": {
          "file": "reference/replies.csv",
          "line": 4
        },
        "candidate": {
          "file": "candidate/replies.csv",
          "line": 2
        },
        "differences": [],
        "missing_items": [
          "action"
        ]
      },
      {
        "key": "d12",
        "status": "mismatch",
        "reference": {
          "file": "reference/replies.csv",
          "line": 13
        },
        "candidate": {
          "file": "candidate/replies.csv",
          "line": 7
        },
        "differences": [],
        "missing_items": [
          "ticket"
        ]
      }
    ]
  },
  "scores": {
    "sheet_files": [
      "scores/sheet.csv"
    ],
    "sources_files": [
      "scores/sources.csv"
    ],
    "min": 1,
    "max": 5,
    "items": 12,
    "raters": [
      "p1",
      "p2",
      "p3"
    ],
    "ratings": 72,
    "blind": {
      "status": "kept",
      "findings": []
    },
    "sources": [
      {
        "source": "ai",
        "results": 12,
        "ratings": 36,
        "sum": 139,
        "mean": 3.86,
        "distribution": [
          {
            "score": 1,
            "count": 0
          },
          {
            "score": 2,
            "count": 2
          },
          {
            "score": 3,
            "count": 7
          },
          {
            "score": 4,
            "count": 21
          },
          {
            "score": 5,
            "count": 6
          }
        ]
      },
      {
        "source": "human",
        "results": 12,
        "ratings": 36,
        "sum": 151,
        "mean": 4.19,
        "distribution": [
          {
            "score": 1,
            "count": 0
          },
          {
            "score": 2,
            "count": 0
          },
          {
            "score": 3,
            "count": 10
          },
          {
            "score": 4,
            "count": 9
          },
          {
            "score": 5,
            "count": 17
          }
        ]
      }
    ],
    "paired": {
      "better": 3,
      "same": 0,
      "worse": 9
    },
    "by_rater": [
      {
        "rater": "p1",
        "ai_ratings": 12,
        "ai_mean": 4.00,
        "human_ratings": 12,
        "human_mean": 4.25
      },
      {
        "rater": "p2",
        "ai_ratings": 12,
        "ai_mean": 3.92,
        "human_ratings": 12,
        "human_mean": 4.33
      },
      {
        "rater": "p3",
        "ai_ratings": 12,
        "ai_mean": 3.67,
        "human_ratings": 12,
        "human_mean": 4.00
      }
    ]
  }
}
```

`examples/reply-drafts/expected/judgment.json` (SHA-256 `8b602267560e4d2b6192b1696f9aab1abe2fb9aa6d82619c5e541ee5c088bd65`, 1,477바이트):

```json
{
  "schema_version": 2,
  "case": "reply-drafts",
  "verdict": "conditional",
  "rule": "FOCUS",
  "reason": "정확도·누락률은 문턱을 충족하나 불일치 2건이 모두 topic = 환불인 행이므로 조건부.",
  "thresholds": {
    "accuracy_min": 80,
    "missing_max": 5,
    "time_must_drop": false,
    "score_gap_max": 0.5
  },
  "defaults": [],
  "checks": [
    {
      "rule": "ACCURACY",
      "result": "met",
      "reason": "정확도 83.3%(10/12)가 문턱 80% 이상이다."
    },
    {
      "rule": "MISSING",
      "result": "met",
      "reason": "누락률 0.0%(0/12)가 문턱 5% 이하다."
    },
    {
      "rule": "UNEXPLAINED",
      "result": "not_reviewed",
      "reason": "검토할 항목 2건(불일치 2건)이 설명되는지 사람이 아직 보지 않았다."
    },
    {
      "rule": "TIME",
      "result": "off",
      "reason": "시간 조건을 쓰지 않는다(time_must_drop: false)."
    },
    {
      "rule": "FOCUS",
      "result": "found",
      "reason": "불일치 2건이 모두 topic = 환불인 행이다(환불 5행 중 2행, 다른 값 7행 중 0행)."
    },
    {
      "rule": "SCORE",
      "result": "met",
      "reason": "AI 결과 평균 3.86점(36개)이 사람 결과 평균 4.19점(36개)보다 0.5점 넘게 낮지 않다."
    }
  ],
  "todo": [
    {
      "key": "review_items",
      "text": "검토할 항목 2건(불일치 2건)을 보고 설명이 되는지 적는다."
    }
  ]
}
```

- classification: 1단계 ② `ELSE`, 2단계 ③ `Q3` + ④ `GATE`(`free_doc`·`partial`·`external`), 3단계 ④ `SIGNOFF`. `answers.yaml`과 `case.yaml`이 같은 바이트를 낸다 [T14:O].

## 8. 테스트로 옮기는 방법 (PR (b))

- 폴더 `tests/Scorecard.Tests/TextCheck/`, 네임스페이스 `Scorecard.Tests.TextCheck`. Trait `Spec` = `text-check`. 표 읽기는 `SpecDocument.Table`(경로 상수 `docs/spec/text-check.md` 추가), 블록은 `SpecDocument.BlockAfter`(SHA-256 줄 뒤).

test-engineer(`tests/`, `examples/`):

| 테스트 | 확인 | 카드 수용 기준 |
| --- | --- | --- |
| 파서(4.1) | 머리 행 `\| case.yaml 바꾼 곳 \| 결과 \| 코드 \| 메시지 \|`. 행마다 받음 또는 코드·메시지 | 1 |
| 맞추기(4.2) | 머리 행 `\| 항목 \| 기준 칸 \| 결과 문장 \| 결과 \|`. 메모리 바이트로 대조해 `missing_items`·`entries[]` 셈 | 2 |
| 체크리스트 장면(4.2 규칙 10) | 머리 행 `\| 체크리스트 바꾼 곳 \| 결과 \| 코드 \| 정확도(%) \|`. 행마다 받음과 정확도, 또는 `no-check`. "바꾼 곳"의 `; `는 파일이 다른 묶음 | 2 |
| 평균(4.3) | 머리 행 `\| 점수 합 \| 점수 수 \| 평균 \|` | 3 |
| 가림·점수 오류(4.4·4.5) | 머리 행 `\| 가림 규칙 \| 보는 곳 \| 걸리는 조건 \| 발견 위치·값 \|`, `\| 가림 키 \| 꼴 \| 글자 수 \| 글자 종류 \| 숫자 줄 \|`, `\| 키 하나 \| 키 둘 \| 작은 쪽 \|`, `\| 몰림 문턱 \| 값 \| 조건 \|`, `\| AI 쪽 건 \| 사람 쪽 건 \| 센 건 \| 걸리나 \|`, `\| 점수 자료 바꾼 곳 \| 결과 \| 가림 발견 \| 코드 \|`, `\| 점수 코드 \| 언제 \| 위치 \| 메시지 \|`, `\| 체크리스트 코드 \| 언제 \| 위치 \| 메시지 \|`. 장면 표 "바꾼 곳"의 출처·채점자 범위와 행 옮기기는 4.4 머리말 | 2, 3 |
| 새 속성(3.1·3.2) | 머리 행 `\| 새 속성 \| 형식 \| 뜻 \|`, `\| 판정 새 속성 \| 형식 \| 뜻 \|`. reply-drafts 출력의 새 경로 = 표(순서) | 2, 3 |
| 판정(5절) | 머리 행 `\| 점수 문턱 \| 기본값 \| 근거 \|`, `\| 점수 확인 \| 결과 \| 걸리는 조건 \| 읽는 문턱 \| 확인 문장 틀 \|`, `\| 문턱 0 문장 \| 근거 키 \| 결과 \| 문장 틀 \|`, `\| 자리 \| 근거 키 \| 확인 결과 \| 길 \| 판정 \| 판정 문장 틀 \|`, `\| 자리 \| 근거 키 \| 확인 결과 \| 길 \| 읽은 문턱 \|`, `\| 점수 할 일 \| 나오는 때 \| 문구 \|` = Core 상수 | 3 |
| 판정 장면(6.1) | 머리 행 `\| 장면 \| 바탕 \| 바꾼 곳 \| 판정 \| 근거 키 \| 확인 결과 \| 다음 할 일 \|`(바꾼 곳의 `; `는 4.2 장면 표와 같다). 점수표만이면 `defaults` ⊆ {`time_must_drop`, `score_gap_max`}. 6.1 글머리의 판정 문장 둘 | 1, 3 |
| 골든 | `examples/reply-drafts/` → 파서 → 대조 → 판정 → 세 파일이 expected와 바이트까지 같다 | 4 |
| 블록 = 파일 | 7.3·7.4 블록·SHA-256 줄 = `examples/reply-drafts/` 파일. 머리 행 `\| 새 예제 \| inputs.group \| …`, `\| 예제 파일 \| 만드는 곳 \|` | 4 |
| 생성기(7.2) | 머리 행 `\| 계획 \| 값 \|`로 Core 생성기를 돌려 7.3 CSV 넷과 바이트까지 같다 | 4 |
| 결정론 | de-DE·tr-TR, 같은 입력 두 번, CSV 변형 7종(4.6) | — |
| G2·G3 | `text-check-case-valid`(4.6) | — |
| 기존 목록 | 예제 이름 목록(`ClassificationInput.Examples` 등)에 `reply-drafts`, `Example_folder_has_exactly_the_planned_files` 목록에 `scores/` 두 파일. 대상을 더한다(축소 아님) | 4 |

core-dev(`src/Scorecard.Core/`):

1. 파서: `CaseDefinition`에 `Checklist`·`Scores`(위치 매개변수 끝, 기본값 null), `CaseThresholds`에 `ScoreGapMax`(기본값 null). 4.1 검사. 기존 생성 코드가 그대로 컴파일된다(T-012 `Group`과 같은 방식).
2. 대조: 4.2(체크리스트), 3.1 `checklist`·`missing_items`. 열 찾기 순서(4.2 규칙 1). 확인할 것이 없는 기준 행은 `no-check`(4.2 규칙 10, 4.5 체크리스트 코드 표).
3. 점수표: 4.3·4.4·4.5. 표시 몰림(4.4 `source_pattern`)의 문턱 셋은 상수(몰림 문턱 표), 셈은 정수. 키 글자 수는 `string.Length`(UTF-16), 숫자 줄은 숫자 묶음마다 정수(자릿수가 길면 앞 0을 떼고 길이·글로 견주면 넘치지 않는다). 새 공개 형식 `ScoreSummary`(제안). `CompareMetrics`에 `Scores`(끝, 기본값 null). `MetricsJson.Render`가 3.1 자리에 쓴다.
4. 판정: 5절. `JudgmentRules.cs`에 5.1~5.5 상수(5.2 문턱 0 문장 표 포함). 점수표만의 `defaults`는 5.1, 대조할 열 없음은 5.3 `NOT_VERIFIED` 행. `JudgmentJson`이 3.2를 쓴다.
5. 생성기: 7.2 `SyntheticReplies`(`internal` SplitMix64 재사용).
6. 규칙 값은 `internal` 한 파일씩(`classification.md` 4.9 방식). 실행 중에 문서를 읽지 않는다.

- 시작 전 main 테스트 수 이상, 삭제·Skip 없음(카드 수용 기준 8). 바꾸는 기존 테스트는 목록에 예제·파일·대상을 더하는 것뿐이다.
- `tests/`·`src/`에 `tools/oracle` 글자를 쓰지 않는다(regression-guard `ci-tamper`). PR (b)는 `tools/oracle/`을 바꾸지 않는다(`oracle-and-expected`, 바꾸면 `ci-changed`).
- 이 스펙(PR (a))은 기존 C# 테스트가 읽는 표·블록·줄을 바꾸지 않는다. 작업 트리 사본(`src/`·`tests/`는 커밋 그대로)에서 `dotnet test -c Release` 1,664개가 모두 통과한다 [T14:S5]. 리뷰 뒤, 리뷰 2차 뒤 고친 작업 트리에서도 1,664개가 모두 통과한다(`src/`·`tests/`는 그대로).

## 9. 미확인 목록

1. GitHub ubuntu·windows 러너에서 새 테스트가 녹색인지(PR (b)).
2. 실제 업무 답변 문장에서 부분 문자열 규칙의 오탐(4.2 규칙 9) 빈도. 공개 사례 대조(`docs/cases-public/`)로 다음 카드에서 본다.
3. 점수표 행 순서로 출처를 짐작하는 일(건마다 늘 AI 행이 앞)의 빈도. 가림 키의 번호 크기는 `source_pattern`(4.4)의 작은 쪽 표시가 몰림 문턱으로 우연과 가른다([재검토], 질문 21. 질문 6의 "키 순서"를 고쳤다). 행 순서는 보지 않는다. 채점자가 표를 정렬·거르면 파일의 행 순서는 채점자가 본 순서가 아니다.
4. 채점자 일치도와 기준 두 벌(gaps.md 17). 점수표 집계와 같은 자료 모양이 쓰일 수 있다(채점자 = 기준 작성자).
5. 표본이 작을 때 평균 차이를 믿을 수 있는지(gaps.md 16).
6. `check` 명령이 점수표를 어떻게 받는지, `review`(M2-4) 화면이 빠진 항목을 어떻게 보이는지.
7. 출처 표를 채점자에게서 숨기는 것은 사람의 일이다. 엔진은 볼 수 없다.
8. 채점자 배정의 치우침(리뷰 2차 N2, 질문 18). 채점자가 한쪽 출처만 매겨도 지금은 평균을 그대로 비교한다. 후한 채점자가 AI 결과만, 엄한 채점자가 사람 결과만 매기면 AI가 높게 나온다(리뷰 재현: 통과 `PASS`). 엔진이 볼 수 있는 조건은 셈이다: 채점자마다 AI 점수 수 = 사람 점수 수, 그리고 건마다 AI 결과 점수 수 = 사람 결과 점수 수이면 채점자·건의 더하기 꼴 차이(후한 채점자, 어려운 건)가 두 평균의 차이에서 지워진다. reply-drafts는 둘 다 맞는다(채점자 3명이 24개를 모두 매김). 어긋나면 조건부로 하는 안은 `SCORE` 결과 값·판정 행·할 일이 늘고, 채점자마다 일부만 매기는 설계가 늘 조건부가 된다. 얼마나 어긋나도 되는지는 표본 크기(gaps.md 16)·채점자 일치도(gaps.md 17)와 같이 정한다. 그때까지 사람은 3.1 `by_rater`(채점자별 출처별 점수 수)로 본다. 가림 확인 `source_pattern`(4.4)의 열·파일 표시는 두 출처를 모두 매긴 채점자의 행만 본다. 그래서 한쪽 출처만 매긴 채점자의 파일·열이 출처마다 달라도 가림 확인에 걸리지 않고 이 항목에 남는다(4.4 장면 "한쪽만 매긴 채점자"). 가림 키는 채점자와 상관없이 본다. 엔진은 `rater` 칸이 사람 한 명을 가리킨다고 믿는다. 같은 사람을 출처마다 다른 이름(`p1`, `p1x`)으로 적으면 한쪽만 매긴 채점자 둘로 보여 열·파일 표시를 견주지 않는다.
9. 체크리스트는 항목이 있는지만 본다(리뷰 3차 N1). 12건 답변을 모든 번호·안내·끝인사를 넣은 같은 문장으로 바꿔도 정확도 100.0, 통과다. 다른 행의 번호(다른 고객 번호)가 답변에 들어가도 막지 못한다 [T14:O]. 다른 행의 값을 보는 규칙(예: 그 행의 것이 아닌 `column` 값이 있으면 값 불일치)은 다음 카드에서 정한다. 그때까지 문장 품질은 점수표(사람)가 본다.
10. 칸 값의 출처 표기(리뷰 3차 N2, 질문 20). 여러 결과에 같은 표시로 붙으면 `source_pattern`(4.4)이 잡는다: `writer` 열의 `ai-draft`(글 그대로), `AI 답변 01`·`AI 답변 02`(틀 `AI 답변 `), 첫 글자·끝 글자가 같은 값. 남는 것은 칸 한두 곳에만 쓴 표기(갈린 건 8 미만)다. `source_value`는 그대로 칸 전체가 `ai`·`human`일 때 한 칸이라도 잡는다. 조각 규칙을 칸에 쓰는 것은 여전히 하지 않는다(의견 칸·채점자 이름 `Ai`에서 걸린다).
11. 한국어 출처 표기(리뷰 3차 N3). 여러 결과에 붙으면 `source_pattern`이 잡는다: 가림 키 `기계01`·`사람01`(틀·첫 글자), 열 `출처`·`작성자`의 값 `사람`·`기계`. 남는 것은 한 결과에만 붙은 표기(`b05` → `사람05`)와 열 이름 `출처`다. 낱말 규칙은 ASCII `ai`·`human`·`source`만 본다. 고정 한국어 낱말 목록은 다음 카드에서 정한다.
12. 표시 몰림(`source_pattern`)의 한계([재검토], 질문 21). 보지 않는 것: 번호 홀짝만 갈린 키(작은 쪽은 섞임), 무작위 키 가운데 글자 하나, 한 출처에 값 여러 개를 섞어 쓴 열(작성자 `gpt`·`claude`·`gemini` 대 `kim`·`lee`·`park`, 값마다 4건), 갈린 건이 8 미만이거나 건 수의 1/4 미만인 몰림, 점수표 행 순서(9절 3), 같은 사람을 출처마다 다른 채점자 이름으로 적은 점수표(9절 8). 스스로 만든 같은 갈래 우회는 걸린다 [T14:O]: 키 `x`/`y`·`gpt`/`staff`·대소문자·구분자·끝 글자·한글·자릿수·글자 수, 번호를 AI 결과부터 차례로 매김, 건마다 AI 키 먼저, 무작위 hex 대문자 대 소문자, 건 하나에 결과 하나 배정의 키·작성자 열·파일 나눔. 오탐: 가림이 맞으면(키와 점수표를 출처 모르고 만듦) 표시 하나가 문턱을 넘을 확률은 0.78% 이하다. 모의(가림 맞는 표본 2,000개씩)로 12건에서 0.1~1%, 채점자마다 파일을 나누고 건 하나에 결과 하나만 보는 배정은 0.6~1.6%, 24건 이상은 0.9% 이하다. 차례 번호 키(`b01`~)는 24건 이상에서 0이다 [T14:O 모의]. 채점자가 쓴 의견 열은 표시로 본다. 품질 때문에 같은 의견(`나쁨`)이 거의 한 출처에만 붙으면 걸린다. 의견은 점수표 밖에 두길 권한다. 낱말 규칙 `source_in_blind`는 무작위 영숫자 키 안의 `ai` 조각(`k7ai3x`)에도 걸린다(6자 키 24개에 1.5%). 무작위 키는 숫자만 쓰거나 같은 머리 글자 + 번호로 만들길 권한다.

## 10. 스파이크 단계 목록

스파이크는 머지 대상이 아니라 이 PR에 없다(`spikes/T-014/`, 작업 폴더에서만 돌렸다). 아래 표는 첫 판을 만들 때 한 일의 기록이다. 저장소에서 다시 확인하는 길은 selftest다: 7.2 계획 표로 CSV를 다시 만들어 7.3 블록과 맞추고(S0·S1 대신), 7.4 블록 = oracle 출력(S2), PR (b) 모의(S4), 표 = oracle 상수, 6.1 장면을 본다. 리뷰 뒤 고친 곳은 손으로 고쳤다(S3의 `fill.py` 판과 다르다).

| 단계 | 한 일 |
| --- | --- |
| S0 | 시드 찾기(`gen.py --find-seed`) |
| S1 | 생성기 두 번 → `diff` 없음 |
| S2 | oracle로 expected 셋. `answers.yaml`·`case.yaml` 분류 같음 |
| S3 | 이 문서 채우기(`fill.py --check`) → 저장소의 이 문서가 채운 결과와 바이트까지 같음 |
| S4 | 저장소 `check.py` 12 MATCH, `selftest.py` 통과. PR (b) 모의: 사본 `examples/`에 7.3·7.4 블록 → `check.py` 15 MATCH, `selftest.py` 통과 |
| S5 | 작업 트리 사본(`src/`·`tests/`는 커밋 그대로)에서 `dotnet test -c Release` |
| S6 | `scan.py`: 예제 파일(줄·경로)에 실제 값 패턴(`synthetic-examples.md` 4.10 표 9종) 0건. 회사 이름(`docs/cases-public/README.md` 1절 "사례" 칸 앞 낱말)이 이 문서·예제·`spikes/T-014/`·`tools/oracle/`에 0건. 이름은 출력하지 않는다 |

## 11. 다른 문서 개정 (PR (b)에서 옮길 곳)

이 PR (a)에서는 다른 스펙의 표·블록을 바꾸지 않는다. C# 테스트와 selftest가 그 표를 읽기 때문이다. 각 문서 끝에 짧은 "T-014 개정" 절을 두어 이 문서를 가리킨다. 규칙의 자리는 이 문서다. → 질문 12.

1. `case-schema.md`: 4.3 최상위 표에 `checklist`·`scores` 행, 4.5 `thresholds` 표에 `score_gap_max` 행, 4.9 표의 `required`·`conflict`·`duplicate`·`range` "언제"에 4.1 경우, 4.12 API. PR (b)에서 옮긴다. 4.3·4.5 표를 읽는 테스트가 있으면 같이 고친다.
2. `metrics.md`: 3.2 표는 그대로다(table-compare 경로 순서를 보는 테스트가 있다). 새 경로는 이 문서 3.1 표다. 4.7 오류 표도 그대로, 점수·체크리스트 코드는 4.5.
3. `judgment.md`: 4.3·4.4·4.6·4.7 표는 그대로다. 이 문서 5절 표를 더해서 읽는다. 7.1 예제 표에 reply-drafts를 넣지 않는다(확인이 여섯이라 "확인 다섯" 칸과 맞지 않는다). 이 문서 7.1 새 예제 표가 그 자리다.
4. `synthetic-examples.md`: 문장 생성기는 이 문서 7.2다. 4.3 계획 표에 넣지 않는다(열 모양이 다르다).
5. 지시문(`docs/README-instructions.md`, 리드·ADR 0009): 3.4에 문장 결과의 두 길, 점수 미달·가림 깨짐 보류, 점수표만 판정과 대조할 열 없음 조건부를 PR (a)에서 더했다. 12절 예제 표의 다섯째 `reply-drafts`(정답 표 없는 문장 결과, 조건부)와 제목 "4종"은 PR (b)에서 고친다. 테스트(`Instructions_section_12_verdicts_hold` 등)가 그 표의 행마다 `examples/`를 읽어 PR (a)에서 넣으면 실패한다. 2절 원칙은 바꾸지 않는다.

## 12. ADR 0009 초안 요지 (리드가 쓴다)

이 PR (a)는 `tools/oracle/`을 바꾼다. regression-guard `ci-changed`가 새 ADR과 `docs/STATUS.md` "원칙 변경" 행(새 ADR 번호)을 요구한다 [읽음 `tools/ci/README.md` C].

- 제목: 문장 결과 검증(체크리스트·가림 점수표)과 oracle.
- 배경: 빈 곳 2. 정답 표가 없는 문장 결과는 지금 미검증뿐이다.
- 결정 1: `case.yaml`에 `checklist`·`scores`(케이스 수준). 엔진은 문자열 규칙과 셈만 쓴다(AI·자연어 처리 없음).
- 결정 2: 출력은 설정했을 때만 있는 속성이다. `schema_version`은 그대로(ADR 0008 결정 1 적용).
- 결정 3: 판정에 확인 `SCORE`와 행 여섯(5.3, 리뷰 뒤 대조할 열 없음 조건부 한 행). 기존 표·문장·예제 바이트는 그대로다.
- 결정 4: oracle에 `checklist.py`·`scores.py`를 더하고 `oracle_case.py`·`compare.py`·`judge.py`·`selftest.py`를 넓힌다(이 문서 글만 보고). `check.py` 계약은 그대로다.
- 결정 5: 새 예제 `reply-drafts`는 PR (b). 지시문 12절 문구는 리드가 고친다(5절 목록 그대로).
- 영향: 기존 expected 12개 바이트 그대로. 가드를 약하게 하는 변경은 없다. selftest는 점검이 늘기만 한다.
- 남은 위험: 부분 문자열 오탐(4.2 규칙 9), 가림 키 순서(9절 3), 채점자 배정 치우침(9절 8), 체크리스트는 있는지만 봄(9절 9), 출처 표기(9절 10·11), 표시 몰림의 한계와 오탐(9절 12).
- STATUS 원칙 변경 행: `| 2026-11-08 | 0009 | 문장 결과 검증 두 길(checklist·scores), 판정 SCORE, oracle checklist.py·scores.py | 리드(ADR 0005 결정 3) | PR (a) 번호 |`.

## 승인 질문 (ADR 0005: 추천안 채택)

추천안은 "사용자에게 맞나 → 그다음 바이트 보존" 순서로 골랐다. "ADR 0004 기준"은 ADR 0005 전이었다면 누가 정했을지다(`judgment.md` 12.6과 같다). 질문은 20개다. 13~15는 PR (a) 리뷰 지적, 16~18은 리뷰 2차, 19~20은 리뷰 3차 지적에서 나왔다.

1. **두 길의 자리(4.1).** ADR 0004 기준: juhan 직접(`case.yaml` 스키마).
   - 추천: 최상위 `checklist`·`scores` 두 키. `inputs`·`compare`는 그대로. 단계 질문은 늘지 않는다.
   - 대안: `compare` 항목에 `contains:` 같은 종류를 둔다. 문장 열이 기준 자료에도 있어야 하고, 건마다 다른 값과 모든 건에 같은 문구를 한 곳에 적기 어렵다.
2. **체크리스트를 대조의 한 종류로(4.2 규칙 6).** ADR 0004 기준: 리드.
   - 추천: 빠진 항목이 있으면 값 불일치. 정확도·누락률·검토 항목·몰림·판정 표가 그대로 쓰인다. 사용자는 같은 문턱(`accuracy_min`)으로 본다.
   - 대안: 항목 충족률을 따로 세고 새 문턱을 둔다. 판정 행·문턱이 늘고 표 대조와 말이 달라진다.
3. **문자열 규칙(4.2 규칙 2~5).** ADR 0004 기준: 리드.
   - 추천: 서수 부분 문자열, 공백 접기, 대소문자·NFC 그대로, `column` 칸이 비면 요구하지 않음, `any`는 하나라도. 정규식 없음.
   - 대안: 정규식. 표현은 넓지만 사용자가 쓰기 어렵고, 틀린 식이 조용히 통과시킨다.
4. **점수표 모양(4.3).** ADR 0004 기준: juhan 직접(입력 모양).
   - 추천: 점수표(`rater`, `item`, `blind`, `score`)와 출처 표(`blind`, `item`, `source`) 두 파일. 열 이름 고정, 다른 열 허용. 점수는 정수 `min`~`max`. 채점자 파일을 여럿 둘 수 있다(`sheet` 패턴).
   - 대안: 한 파일에 출처 열. 채점자가 출처를 볼 수 있게 된다.
5. **점수 자료 결함은 오류, 가림 깨짐은 판정(4.4·4.5).** ADR 0004 기준: 리드.
   - 추천: 형식·중복·짝·빠진 점수는 오류(종료 2, 사람 자료라 고치게 한다). 출처가 드러난 것은 `revealed` → 보류(고쳐도 되돌아오지 않는다). 다른 결과(체크리스트)는 그대로 보인다.
   - 대안: 모두 오류. 체크리스트 결과까지 안 보인다.
6. **가림 확인 범위(4.4).** ADR 0004 기준: 리드.
   - 추천: 세 규칙(`source` 열, 칸 값 `ai`·`human`, 가림 키 조각). 키 순서·의견 문장은 보지 않는다(9절 3).
   - 대안: 키 순서 규칙을 더한다. 우연과 가를 숫자가 지시문에 없다.
7. **집계와 셈(4.3, 5.2).** ADR 0004 기준: 리드.
   - 추천: 모든 점수의 출처별 평균(소수 둘째, 정수 셈), 분포, 건별 비교, 채점자별 평균. 판정은 합·수 곱셈으로 `score_gap_max`(기본값 0) 비교.
   - 대안: 건별 승패 비율로 판정. 같은 점수 차이도 건 수에 따라 판정이 흔들린다.
8. **판정 자리(5.3).** ADR 0004 기준: 리드.
   - 추천: `SCORE`는 확인 여섯째. `revealed`·`not_met`은 순서 4 뒤 보류. 점수표만이면 `NOT_VERIFIED`가 아니고 `TIME`·`PASS` 문장을 "점수는"으로 쓴다. 기존 문장은 그대로. (리뷰 뒤: `inputs`를 적고 대조할 열이 없는 점수표만은 질문 13.)
   - 대안: 점수 미달을 조건부로. 지시문 3.4 "보류 = 정확도 미달"과 같은 갈래(품질 미달)라 보류가 맞다.
9. **출력과 `schema_version`(3.1~3.3).** ADR 0004 기준: juhan 직접(expected 모양).
   - 추천: metrics.json·judgment.json에 설정했을 때만 있는 속성. `schema_version` 그대로(ADR 0008 결정 1). 기존 expected 12개 바이트 그대로, migration 없음.
   - 대안: 늘 null 속성 + `schema_version` 올림(3.3), 또는 새 파일 `text-check.json`(내부 명령·expected 목록·`check.py` 대상이 는다).
10. **예제(7.1).** ADR 0004 기준: juhan 직접(예제 추가).
    - 추천: mixed-exceptions 그대로, 새 예제 `reply-drafts`(체크리스트 + 점수표, 조건부 `FOCUS`).
    - 대안: mixed-exceptions에 자료를 넣는다. 지시문 12절 "미검증" 예제가 사라지고 expected 3개가 바뀐다.
11. **합성 생성기(7.2).** ADR 0004 기준: 리드.
    - 추천: 문장 생성기를 Core에 두고(T-005 방식) 테스트가 다시 만들어 블록과 비교한다.
    - 대안: 스파이크 생성기만 두고 블록을 정답으로. 지시문 2절 10 "합성 생성기로만"을 테스트가 확인하지 못한다.
12. **문서 자리(11절).** ADR 0004 기준: 리드.
    - 추천: 규칙의 자리는 이 문서. 다른 스펙의 표는 바꾸지 않고 "T-014 개정" 절에서 가리킨다. PR (b)에서 `case-schema.md` 표만 옮긴다(파서 규칙은 한 곳에 있어야 찾기 쉽다).
    - 대안: `judgment.md`·`metrics.md` 표에 행을 넣는다. 표를 읽는 기존 테스트·selftest가 같이 바뀌고, PR (b)는 oracle을 바꿀 수 없다.
13. **점수표만인데 `inputs`가 있고 대조할 열이 없음(5.3, 리뷰 막는 지적 2).** ADR 0004 기준: 리드.
    - 추천: 조건부 `NOT_VERIFIED`(`no_compare_fields`) + `add_compare`. 자리는 점수 보류 뒤, `TIME` 앞. 근거 키·결과 값·할 일 키가 모두 있던 것이다. 보류 → 조건부 → 통과 순서를 지킨다. 점수는 그대로 보인다.
    - 대안 1: 미검증(순서 1 그대로). 이번에는 점수표가 말없이 버려진다. 가림이 깨져도 보류가 나오지 않는다.
    - 대안 2: 파서 오류(`inputs`가 있으면 `compare`·`checklist` 필수). 지금 받는 `case.yaml`(`inputs`만 있고 `compare`가 빈 미검증)을 거절하게 된다.
14. **점수표만이 읽지 않는 문턱(5.1, 리뷰 지적 b).** ADR 0004 기준: 리드.
    - 추천: `defaults`·`confirm_thresholds`·기본값 표시에는 이 길이 읽는 문턱(`time_must_drop`, `score_gap_max`)만. `thresholds` 객체 모양은 그대로.
    - 대안: 점수표만이면 `thresholds`에서도 `accuracy_min`·`missing_max`를 뺀다. 쓰지 않는 값이 안 보이지만, 읽는 쪽이 키가 있는지 늘 봐야 하고 `judgment.md` 3.2 모양이 길마다 달라진다.
15. **문턱 0 문장(5.2, 리뷰 지적 h).** ADR 0004 기준: 리드.
    - 추천: `score_gap_max` 값이 0이면 "{score_gap_max}점 넘게"가 없는 틀(5.2 문턱 0 문장 표). 기존 틀·reply-drafts 블록은 그대로.
    - 대안: 틀 하나를 "차이가 문턱 {score_gap_max}점을 넘으므로"로 바꾼다. 문턱 0도 읽히지만 reply-drafts expected 블록이 바뀐다.
16. **체크리스트가 그 행에 아무것도 요구하지 않음(4.2 규칙 10, 리뷰 2차 막는 지적 B1).** ADR 0004 기준: 리드.
    - 추천: `compare` 필드도 `any` 항목도 없고 그 기준 행의 `column` 칸이 모두 비면 `no-check` 오류(종료 2). 기준 자료와 `case.yaml`만으로 정해지는 사람 자료 결함이라 질문 5와 같은 갈래다. 일부 행만 빈 경우와 모두 빈 경우를 함께 막는다. 셈·판정·출력 모양이 그대로라 reply-drafts 블록과 expected 12개가 바이트 그대로다.
    - 대안 1: 그 행을 일치로 세지 않고 값 불일치로 센다. AI 결과와 상관없는 것을 AI 탓으로 세고, 빠진 항목 없는 불일치가 검토 항목에 뜬다.
    - 대안 2: 그 행을 셈에서 뺀다. `counts` 모양이나 정확도 정의(`metrics.md` 4.4)가 바뀌고, 빠진 행이 말없이 사라진다.
    - 대안 3: 체크리스트 요구 합(`required`)이 0이면 5.3 대조할 열 없음처럼 조건부 `NOT_VERIFIED` + `add_compare`. 일부 행만 빈 경우(리뷰 재현: 빈 답변 6건이 일치, 정확도 50.0)를 막지 못한다.
17. **가림 확인 `source_column`의 대소문자(4.4, 리뷰 2차 N1).** ADR 0004 기준: 리드.
    - 추천: 열 이름도 ASCII 소문자로 본다(`Source`, `SOURCE`). 발견 값은 쓴 열 이름 그대로. 칸 값(`source_value`)과 같은 규칙이다. reply-drafts에는 발견이 없어 블록 그대로다.
    - 대안: 정확히 `source`만. 표 프로그램이 첫 글자를 대문자로 바꾼 머리 행을 놓친다.
18. **채점자가 한쪽 출처만 매김(4.3 규칙 6, 리뷰 2차 N2).** ADR 0004 기준: 리드.
    - 추천: 이 카드에서는 막지 않고 9절 8에 적는다. 엔진이 볼 수 있는 조건(채점자마다·건마다 AI·사람 점수 수가 같음)과 그 까닭을 적어 두고, 얼마나 어긋나도 되는지는 표본 크기·채점자 일치도 카드(gaps.md 16·17)에서 정한다. 사람은 `by_rater`로 본다.
    - 대안: 지금 조건부로 막는다. `SCORE` 결과 값·판정 행(두 길)·할 일이 늘고, 채점자마다 일부만 매기는 설계가 늘 조건부가 된다. 한쪽만 매긴 채점자만 잡는 약한 판은 11 대 1 같은 치우침을 놓친다.
19. **가림 키 조각(4.4, 리뷰 3차 막는 지적 B1).** ADR 0004 기준: 리드.
    - 추천: 조각은 ASCII 영문자 묶음과 숫자 묶음이다. 영문자와 숫자 사이에서도 나눈다. `ai01`·`HUMAN03`·`b01ai`를 잡고, `bai05`·`mail05`·`rain`은 그대로 잡지 않는다. reply-drafts 키(`b01`~`b24`)는 걸리지 않아 블록 그대로다.
    - 대안: 키 안 어디든 `ai`가 있으면(부분 문자열). `bai05`·`mail05`·`rain`까지 걸려 가림이 깨지지 않은 점수표가 보류된다.
20. **칸 값의 출처 표기(4.4 `source_value`, 리뷰 3차 N2).** ADR 0004 기준: 리드.
    - 추천: 이 카드에서는 칸 전체가 `ai`·`human`일 때만 잡는다(그대로). 9절 10에 적고, 한국어 낱말(9절 11)과 같이 다음 카드에서 정한다. 조각 규칙을 칸에 쓰는 것은 안전하지 않다: 의견 칸·채점자 이름(`Ai`)·건 이름에서 걸려 보류가 잘못 나온다.
    - 대안: 점수표의 정해진 넷 밖 열에만 조각 규칙. `writer` 열은 잡지만 `메모` 열의 `AI 같다`(채점자의 짐작)도 걸린다. 4.4 "의견 칸의 문장은 보지 않는다"와 맞지 않는다.
21. **표시 몰림(4.4 `source_pattern`, [재검토], 리뷰 4차 막는 지적 B1).** ADR 0004 기준: 리드. 리뷰 1~3차가 같은 갈래(낱말 목록을 비켜 가 통과가 잘못 남)라 낱말이 아니라 구조로 닫는다.
    - 추천: 엔진이 출처 표(정답)로 직접 센다. 보는 곳은 가림 키, 점수표의 다른 열 칸(`rater`·`item`·`blind`·`score` 밖), 점수 파일. 표시는 글 그대로·틀(ASCII 숫자 묶음을 지운 글)·첫 글자·끝 글자, 키에는 꼴·글자 수·글자 종류·작은 쪽(번호 크기)을 더한다. 건마다 한쪽 결과에만 있는 표시를 센다. 키는 채점자와 상관없이 견주고, 열·파일은 두 출처를 모두 매긴 채점자의 행만 견준다(한쪽만 매긴 채점자는 9절 8, 채점자 이름은 표시로 보지 않음). 센 건은 출처 표의 건 수다. 문턱은 갈린 건 8 이상, 반대쪽 12건에 1건 이하, 몰린 쪽이 센 건의 1/4 이상. 가림이 맞으면 표시 하나가 걸릴 확률이 0.78% 이하다. 빈 값은 표시가 아니다. 낱말 규칙 셋은 그대로 둔다. 질문 6의 "키 순서는 보지 않는다"는 이 문턱으로 우연과 가를 수 있게 되어 키 번호 크기만 본다(행 순서는 그대로 보지 않는다, 9절 3). reply-drafts는 몰린 표시가 없어(작은 쪽 6:6) 7.4 블록과 expected 12개가 바이트 그대로다. 출력 모양도 그대로다(`findings[].rule` 값이 하나 는다).
    - 대안 1(리뷰 안 그대로): 숫자를 지운 키·다른 열 값·파일 가운데 결과 둘 이상에 나온 값이 한 출처에만 나오면 걸림. 무작위 키는 틀이 우연히 겹쳐 가림이 맞아도 보류된다(모의, 12건: hex 4자 78%, hex 8자 39%). 채점자마다 나눈 파일이 한쪽만 매긴 채점자(9절 8)와 섞인다. 사람 키 하나에 같은 표시를 섞으면(`x` 11개 AI + 1개 사람) 값이 두 출처에 다 나와 비켜 간다.
    - 대안 2: 낱말 목록을 늘린다(`gpt`, `staff`, `사람` …). 이름을 바꾸면 또 빠진다.
    - 대안 3(이 [재검토]의 첫 초안): 두 결과를 다 매긴 채점자가 있는 건만 센다. 채점자마다 건 하나에 결과 하나만 보는 배정(흔한 설계)에서 센 건이 0이라 키 `x`/`y`, 작성자 열, 파일 나눔을 모두 놓친다(스스로 만든 우회, 4.4 장면 "건 하나에 결과 하나").
    - 대안 1이 잡고 추천이 놓치는 것: 한 출처에 값 여러 개를 섞어 쓴 열(작성자 `gpt`·`claude`·`gemini` 대 `kim`·`lee`·`park`, 값마다 4건). 값 하나하나는 우연과 가를 만큼 갈리지 않는다. 값 묶음을 한꺼번에 보는 검정은 다음 카드에서 정한다(9절 12).
22. **점수표의 건과 기준 자료 키(4.3 규칙 2, 리뷰 4차 N1).** ADR 0004 기준: 리드.
    - 추천: 대조가 `measured`이면 출처 표의 건은 기준 자료의 키여야 한다. 아니면 `blind` 오류(경로 `item`, `기준 자료에 없는 건이다: <건>`). 다른 케이스의 점수표를 말없이 쓰는 것은 사람 자료 결함이라 오류다(질문 5와 같은 갈래). 점수표만은 볼 기준이 없어 보지 않는다. 기준 키가 모두 점수표에 있어야 하는지는 묻지 않는다(점수는 일부 건만 매길 수 있다).
    - 대안 1: 새 코드 `item`. 코드 표가 늘고, 같은 연결 오류인 `blind`의 "건이 출처 표와 다름"과 갈린다.
    - 대안 2: 조건부 판정. AI 결과의 결함이 아닌데 판정으로 넘기고, 고칠 곳(사람 자료)이 흐려진다.

## 채택

- ADR 0005 결정 3에 따라 리드가 추천안 1~12를 채택했다(ADR 0009). PR (a) 리뷰 뒤 13~15, 리뷰 2차 뒤 16~18, 리뷰 3차 뒤 19~20, [재검토] 뒤 21~22도 추천안을 채택했다. 카드 "잠정 결정"과 `docs/STATUS.md` 잠정 결정 T-014 행에 적었다.
