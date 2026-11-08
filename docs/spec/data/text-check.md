# text-check 데이터

`docs/spec/text-check.md`의 표·블록·이름표다. 테스트와 oracle이 읽는다. 규칙 글은 본문에 있다.

## 3. 출력

### 3.1 metrics.json에 더하는 속성

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

### 3.2 judgment.json에 더하는 속성

| 판정 새 속성 | 형식 | 뜻 |
| --- | --- | --- |
| `thresholds.score_gap_max` | 숫자 | AI 평균이 사람 평균보다 낮아도 되는 폭(5.1). `case.yaml` 값 그대로(자릿수 유지) 또는 기본값. `scores`가 있을 때만 |

## 4. 규칙

### 4.1 `case.yaml` 키

| 키 | 형식 | 필수 | 규칙 |
| --- | --- | --- | --- |
| `checklist` | 매핑 | 아니오 | `text`(문자열, 필수), `items`(목록, 1개 이상). `inputs`가 있어야 한다(`required`, 경로 `inputs`). `compare`와 같이 써도 된다 |
| `checklist.text` | 문자열 | 예 | 결과 자료의 문장 열 이름. `inputs.key`·`inputs.group`·`compare` 필드와 다르다(`conflict`) |
| `checklist.items[]` | 매핑 | — | `item`(문자열, 필수, 서수 중복 금지 `duplicate`)과 `column`·`any` 가운데 꼭 하나(둘 다 `conflict`, 없음 `required`) |
| `checklist.items[].column` | 문자열 | — | 기준 자료의 열 이름. 그 행의 칸 값이 결과 문장에 있어야 한다. 칸이 비면 그 행에는 요구하지 않는다 |
| `checklist.items[].any` | 목록 | — | 문구 1개 이상(빈 목록 `required`). 문구는 비지 않은 문자열(`required`). 하나라도 결과 문장에 있으면 있다 |
| `scores` | 매핑 | 아니오 | `sheet`·`sources`(경로 패턴, 필수, `inputs` 경로와 같은 `format` 규칙), `min`·`max`(정수, 필수, 0 이상 100 이하, `min` < `max`. 아니면 `range`) |
| `thresholds.score_gap_max` | 숫자 | 아니오 | 0 이상 100 이하. `scores`가 없으면 `required`(경로 `scores`) |

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

### 4.2 체크리스트 대조

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

| 체크리스트 바꾼 곳 | 결과 | 코드 | 정확도(%) |
| --- | --- | --- | --- |
| (없음) | 받음 | — | 83.3 |
| `case.yaml`: `    - {item: action, column: action}\n` → ``; `reference/replies.csv`: `,q397,` → `,,` | 받음 | — | 100.0 |
| `case.yaml`: `    - {item: action, column: action}\n    - {item: closing, any: [감사합니다, 고맙습니다]}\n` → ``; `reference/replies.csv`: `,q397,` → `,,` | 오류 | `no-check` | — |
| `case.yaml`: `    - {item: ticket, column: ticket}\n` → ``, `    - {item: closing, any: [감사합니다, 고맙습니다]}\n` → ``; `reference/replies.csv`: `,환불 접수\n` → `,\n`, `,교환 접수\n` → `,\n`, `,배송 조회\n` → `,\n` | 오류 | `no-check` | — |

### 4.3 가림 점수표

| 파일 | 열 | 뜻 |
| --- | --- | --- |
| 점수표(`scores.sheet`) | `rater`, `item`, `blind`, `score` | 채점자, 건, 가림 키, 점수. 한 행이 점수 하나 |
| 출처 표(`scores.sources`) | `blind`, `item`, `source` | 가림 키, 건, 출처(`ai` 또는 `human`). 한 행이 결과 하나 |

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

### 4.4 가림 확인

| 가림 규칙 | 보는 곳 | 걸리는 조건 | 발견 위치·값 |
| --- | --- | --- | --- |
| `source_column` | 점수표 머리 행 | 열 이름을 ASCII 소문자로 바꾼 글이 `source`(출처 표의 출처 열 이름) | 그 파일, 머리 행 줄, 열 이름 그대로 |
| `source_value` | 점수표 데이터 칸(`score` 열 밖) | 칸을 ASCII 소문자로 바꾼 글이 `ai` 또는 `human` | 그 파일, 그 줄, 칸 글 그대로 |
| `source_in_blind` | 출처 표의 가림 키 | 키를 ASCII 영문자 묶음과 숫자 묶음으로 나눈 조각 가운데 ASCII 소문자로 `ai` 또는 `human`이 있음 | 그 출처 표, 그 줄, 키 |
| `source_pattern` | 채점자에게 보이는 표시: 가림 키, 점수표의 다른 열 칸(`rater`·`item`·`blind`·`score` 밖), 점수 파일 | 한 표시가 건마다 두 결과 가운데 한쪽에만 있는 것을 세어 몰림 문턱 셋을 모두 넘음(아래 "표시 몰림") | 그 표시가 있는 첫 칸. 키: 출처 표 그 줄, 키. 열: 점수표 그 줄, 칸 글. 파일: 그 파일 머리 행 줄, 경로 |

| 글 | 틀 | 첫 글자 | 끝 글자 |
| --- | --- | --- | --- |
| `b01` | `b` | `b` | — |
| `01x` | `x` | — | `x` |
| `1b` | `b` | — | `b` |
| `😀01` | `😀` | `😀` | — |
| `b01😀` | `b😀` | `b` | `😀` |
| `x2-07` | `x-` | `x` | — |
| `기계01` | `기계` | `기` | — |
| `-7` | `-` | `-` | — |
| `x` | `x` | `x` | `x` |
| `１２` | `１２` | `１` | `２` |
| `0042` | — | — | — |
| (빈 칸) | — | — | — |

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

| 몰림 문턱 | 값 | 조건 |
| --- | --- | --- |
| `min_split` | 8 | 갈린 건 c ≥ 8 |
| `minority_per` | 12 | 12 × 반대쪽 m ≤ c |
| `share_per` | 4 | 4 × 몰린 쪽 M ≥ 센 건 N |

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

### 4.5 오류

| 점수 코드 | 언제 | 위치 | 메시지 |
| --- | --- | --- | --- |
| `score` | 점수가 정수 글이 아니거나 `min`~`max` 밖 | 그 칸, 경로 = `score` | `점수는 <min>~<max>의 정수다: <값>` |
| `source` | 출처가 `ai`·`human`이 아님 | 그 칸, 경로 = `source` | `출처는 ai 또는 human이다: <값>` |
| `blind` | 점수표의 가림 키가 출처 표에 없음 / 건이 출처 표와 다름 / 출처 표의 키에 점수가 없음 / 대조 `measured`인데 출처 표의 건이 기준 자료 키에 없음 | 그 칸(경로 `blind`) / 그 칸(경로 `item`) / 출처 표 그 행의 가림 키 칸(경로 `blind`) / 출처 표 그 행의 건 칸(경로 `item`) | `출처 표에 없는 가림 키다: <키>` / `출처 표의 건과 다르다: <건> (출처 표: <건>)` / `점수가 하나도 없다: <키>` / `기준 자료에 없는 건이다: <건>` |
| `pair` | 한 건에 `ai`·`human`이 하나씩이 아님 | 출처 표의 그 건 첫 행 `item` 칸, 경로 = `item` | `건마다 ai 하나와 human 하나가 있어야 한다: <건> (ai <N>, human <M>)` |
| `duplicate-key` | 출처 표에 같은 가림 키 / 점수표에 같은 채점자·가림 키 | 둘째 칸, 경로 = `blind` | `가림 키가 중복된다: <키> (처음: <파일>:<줄>)` / `점수가 중복된다: <채점자> <키> (처음: <파일>:<줄>)` |
| `no-rows` | 출처 표 데이터 행이 0 | `case.yaml`, 경로 = `scores.sources` | `출처 표에 데이터 행이 없다` |
| `overlap` | 점수 파일이 다른 점수 파일과 같음. 또는 대조가 `inputs` 파일을 읽을 때만(대조 `measured`), 점수 파일이 그 파일과 같음 | 둘째 파일 | `같은 파일이 두 번 나온다(패턴이 겹친다)` |

| 체크리스트 코드 | 언제 | 위치 | 메시지 |
| --- | --- | --- | --- |
| `no-check` | `compare` 필드가 없고 `any` 항목이 없는 체크리스트에서, 기준 행의 `column` 항목 칸이 모두 빔 | 기준 파일:그 행 줄(열 0), 경로 = 빈 문자열 | `확인할 항목이 없다: <키> (column 항목 칸이 모두 비었다)` |

## 5. 판정 (`judgment.md` 4절에 더한다)

### 5.1 점수 문턱 기본값

| 점수 문턱 | 기본값 | 근거 |
| --- | --- | --- |
| `score_gap_max` | `0` | 지시문에 값이 없다. "AI 결과 평균이 사람 결과 평균보다 낮지 않다"가 가장 보수적이다. 기본값 표시로 확인을 받는다 |

### 5.2 확인 `SCORE`

| 점수 확인 | 결과 | 걸리는 조건 | 읽는 문턱 | 확인 문장 틀 |
| --- | --- | --- | --- | --- |
| `SCORE` | `revealed` | `blind.status`가 `revealed` | — | `점수표에서 출처가 드러난 곳이 {findings}군데 있어 점수를 비교하지 않는다.` |
| `SCORE` | `met` | `ai.sum × human.ratings + score_gap_max × ai.ratings × human.ratings ≥ human.sum × ai.ratings` | `score_gap_max` | `AI 결과 평균 {ai_mean}점({ai_ratings}개)이 사람 결과 평균 {human_mean}점({human_ratings}개)보다 {score_gap_max}점 넘게 낮지 않다.` |
| `SCORE` | `not_met` | 그 밖 | `score_gap_max` | `AI 결과 평균 {ai_mean}점({ai_ratings}개)이 사람 결과 평균 {human_mean}점({human_ratings}개)보다 {score_gap_max}점 넘게 낮다.` |

| 문턱 0 문장 | 근거 키 | 결과 | 문장 틀 |
| --- | --- | --- | --- |
| 확인 | `SCORE` | `met` | `AI 결과 평균 {ai_mean}점({ai_ratings}개)이 사람 결과 평균 {human_mean}점({human_ratings}개)보다 낮지 않다.` |
| 확인 | `SCORE` | `not_met` | `AI 결과 평균 {ai_mean}점({ai_ratings}개)이 사람 결과 평균 {human_mean}점({human_ratings}개)보다 낮다.` |
| 판정 | `SCORE` | `not_met` | `AI 결과 평균 {ai_mean}점이 사람 결과 평균 {human_mean}점보다 낮으므로 보류.` |

### 5.3 판정 행

| 자리 | 근거 키 | 확인 결과 | 길 | 판정 | 판정 문장 틀 |
| --- | --- | --- | --- | --- | --- |
| `4 뒤` | `SCORE` | `revealed` | `모두` | `hold` | `점수표에서 출처가 드러난 곳이 {findings}군데 있어 점수를 쓸 수 없으므로 보류.` |
| `4 뒤` | `SCORE` | `not_met` | `모두` | `hold` | `AI 결과 평균 {ai_mean}점이 사람 결과 평균 {human_mean}점보다 {score_gap_max}점 넘게 낮으므로 보류.` |
| `4 뒤` | `NOT_VERIFIED` | `no_compare_fields` | `점수표만` | `conditional` | `점수는 문턱을 충족하나 대조할 열(case.yaml compare·checklist)이 없어 기준·결과 자료(case.yaml inputs)를 쓰지 않았으므로 조건부.` |
| `5` | `TIME` | `not_measured` | `점수표만` | `conditional` | `점수는 문턱을 충족하나 시간을 재지 않아 줄었는지 알 수 없으므로 조건부.` |
| `5` | `TIME` | `not_met` | `점수표만` | `conditional` | `점수는 문턱을 충족하나 처리·검토 시간 {total}분이 적용 전 {before}분보다 짧지 않으므로 조건부.` |
| `8` | `PASS` | — | `점수표만` | `pass` | `점수가 문턱을 충족하고 조건부·보류 조건에 걸리지 않으므로 통과.` |

### 5.4 읽은 문턱

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

### 5.5 다음 할 일

| 점수 할 일 | 나오는 때 | 문구 |
| --- | --- | --- |
| `rescore_blind` | `SCORE` = `revealed` | `출처가 드러나지 않게 가림 키를 새로 붙이고 점수를 다시 매긴다.` |

## 6. 예시

### 6.1 판정 장면

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
- "점수 문턱 기본값": 판정 문장과 `SCORE` 확인 문장 끝에 기본값 표시가 붙는다. 문턱이 0이라 문턱 0 문장(5.2)이다. 판정 문장은 `AI 결과 평균 3.86점이 사람 결과 평균 4.19점보다 낮으므로 보류. (기본값 사용, 확인 필요)`다.
- "점수표만, 쓰지 않는 문턱 기본값": `accuracy_min`·`missing_max`를 지워 기본값(95·2)이 되어도 점수표만은 그 둘을 읽지 않는다. `defaults`가 `[]`이고 `confirm_thresholds`가 없다(5.1).
- "점수표만, 대조 열 없음": PR (a) 리뷰에서 찾은 장면이다. 체크리스트를 지우면 `inputs`는 있는데 대조할 열이 없다(미검증 `no_compare_fields`). 고치기 전에는 통과 `PASS`, 할 일 없음이었다. 이제 판정 문장은 `점수는 문턱을 충족하나 대조할 열(case.yaml compare·checklist)이 없어 기준·결과 자료(case.yaml inputs)를 쓰지 않았으므로 조건부.`다.
- "…시간 조건": 대조할 열 없음이 `TIME`보다 앞이다. `TIME` `not_measured`는 `checks`와 `measure_time`에 남는다. "…점수 미달": 점수 보류가 먼저이고 `add_compare`는 할 일에 남는다(5.3 자리).
- "점수표만": `checks`가 둘이다. `NOT_VERIFIED`가 아니다(카드 수용 기준 1).
- "점수표만, 출처마다 다른 키·열·파일": [재검토]에서 다시 연 장면이다(리뷰 4차 B1). 가림 키를 출처마다 `x`·`y`로, `writer` 열을 출처마다 `gpt`·`staff`로, 점수표를 출처마다 다른 파일로 나눴다. 고치기 전에는 셋 다 가림 `kept`, 통과 `PASS`였다. 이제 `source_pattern`이 둘씩 걸려 판정 문장은 `점수표에서 출처가 드러난 곳이 2군데 있어 점수를 쓸 수 없으므로 보류.`다.
- "번호 칸 빈 행, 빈 답변": 리뷰 2차 B1에서 찾은 장면의 받는 쪽이다. `d12`의 번호 칸이 비어 `ticket`은 요구하지 않지만 `closing`(`any`)이 남는다. 빈 답변은 `closing`이 없어 값 불일치다(`missing_items` = `closing`). 정확도 11/12 = 91.7%. 체크리스트가 그 행에 아무것도 요구하지 않으면(번호 항목만 남김) 일치로 세지 않고 `no-check` 오류다(4.2 규칙 10, 체크리스트 장면 표). 고치기 전에는 그 행이 결과 문장과 상관없이 일치였다.

### 6.2 `score-only`

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

## 7. 예제 reply-drafts

### 7.1 요약

| 새 예제 | inputs.group | 판정 | 근거 키 | 확인 여섯 | 다음 할 일 | 검토 항목 |
| --- | --- | --- | --- | --- | --- | --- |
| `reply-drafts` | `topic` | `conditional` | `FOCUS` | `met`, `met`, `not_reviewed`, `off`, `found`, `met` | `review_items` | 불일치 2건 |

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

- `reference/replies.csv`: `reply_id,topic,ticket,action`, 키 순서. `candidate/replies.csv`: `reply_id,reply`, 5의 순서. `scores/sources.csv`: `blind,item,source`, 가림 키 순서. `scores/sheet.csv`: `rater,item,blind,score`, 7의 순서.

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
