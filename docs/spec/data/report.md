# report 데이터

`docs/spec/report.md`의 표·블록이다. 테스트가 읽는다. 규칙 글은 본문에 있다. 절 번호가 같다.

## 3. 결과 객체 (scorecard.json = `open --json`)

최상위 속성. 순서는 파일 안 순서다.

| 속성 | 형식 | 뜻 |
| --- | --- | --- |
| `schema_version` | 정수 | 결과 객체 판. 지금 2(T-022, ADR 0016) |
| `command` | 문자열 | `open` |
| `files` | 문자열 배열 | 출력 폴더에 쓴 파일: `scorecard.json`, `scorecard.html` |
| `case` | 문자열 | classification.json의 `case` |
| `task` | 문자열 또는 null | answers.yaml(없으면 case.yaml)의 `task` |
| `scenario` | 문자열 또는 null | 답 파일 `scenario`(`virtual`, `real`). 없으면 null(4.13) |
| `status` | 객체 | 상태 네 줄: `material`(`virtual`·`real`·`unknown`), `classification`(`ai_draft`·`rule_tree`), `confirmed`, `steps`, `confirmed_in`, `test`(4.10) |
| `conclusion` | 객체 | 결론 세 줄 `keep`, `first_check`, `undecided`와 `end_state`(답 파일 그대로, 없으면 null)(4.11) |
| `task_summary` | 객체 또는 null | 답 파일 `summary` 그대로: `scale`, `current`, `exception_rate`, `delivery` |
| `intake` | 문자열 또는 null | intake.md 글 그대로(BOM 뗌, 줄바꿈 LF, 끝 줄바꿈 뗌) |
| `answers` | 문자열 | answers.yaml을 썼나: `used`, `missing`, `invalid`, `mismatch`(4.3) |
| `summary` | 객체 | 1구역 셈: `steps`, `software`, `assignees`(① ② ②M ③ ④ 다섯), `differs`, `ai_assist`, `ai_overuse`, `kept`, `changed`, `saving`, `stated`·`tentative`(각각 `steps`, `software`, `ai_assist`, `ai_overuse`), `minutes`(`step`, `minutes`) |
| `steps` | 객체 배열 | 2구역 단계(아래 표) |
| `proposal` | 객체 또는 null | 3구역: `step`, `name`, `reason`, `check`, `effect`, `tentative`. ② 단계가 없으면 null |
| `verification` | 객체 | 4구역: `status`, `reason`, `counts`, `accuracy_pct`, `missing_rate_pct`, `verdict`, `verdict_rule`, `verdict_reason`, `thresholds`, `time`, `checks`, `items`, `items_hidden` |
| `gates` | 객체 배열 | 5구역 담당자 확인: `step`, `name`, `kind`(`signoff`, `exception_review`, `human_gate`), `reason`(엔진 문장), `text`(화면 문장) |
| `risks` | 객체 배열 | 5구역 남는 위험: `key`, `step`(없으면 0), `text` |
| `opinions` | 객체 배열 | 판단이 갈린 단계: `key`(`differs`), `step`, `text` |
| `candidates` | 객체 배열 | 개선 후보: `key`(`ai_overuse`), `step`, `text` |
| `todo` | 객체 배열 | 6구역 다음 할 일: `group`, `key`, `step`, `text` |
| `stated_numbers` | 문자열 배열 | 답 파일 `stated_numbers` 그대로 |
| `open_questions` | 문자열 배열 | 답 파일 `open_questions` 그대로 |
| `self_check` | 객체 배열 | 답 파일 `self_check` 그대로: `rule`, `result`, `note` |
| `inputs` | 객체 배열 | 7구역 입력 SHA-256: `path`(케이스 폴더 기준), `sha256`. 경로 서수 순 |
| `results` | 객체 배열 | 읽은 결과 파일 SHA-256: classification.json, metrics.json, judgment.json 순(있는 것만) |
| `review` | 객체 또는 null | 확인 파일: `path`(파일 이름), `sha256`. `--review`가 없으면 null |

단계 속성(`steps[]`).

| 단계 속성 | 형식 | 뜻 |
| --- | --- | --- |
| `number` | 정수 | 단계 번호 |
| `name` | 문자열 | 단계 이름 |
| `source` | 문자열 | `ai`(AI 판단) 또는 `rule`(규칙 트리 그대로) |
| `assignee` | 문자열 | 단계 행 담당(① ② ②M ③ ④) |
| `rule` | 문자열 | 단계 행 근거 키(`AI`, `SIGNOFF`, `Q1`~`Q4`, `ELSE`) |
| `reason` | 문자열 | 단계 행 근거 문장(기본값·추정 표시 포함) |
| `rule_opinion` | 객체 | 규칙 의견 `assignee`, `rule` |
| `differs` | 불리언 | AI 판단과 규칙 의견이 다르다 |
| `current_actor` | 문자열 또는 null | 지금 하는 쪽(answers.yaml). 기본값이거나 answers를 못 쓰면 null |
| `defaults` | 문자열 배열 | 기본값을 쓴 답 키(classification.json) |
| `inferred` | 문자열 배열 | AI가 추정한 답 키(answers.yaml `inferred`, case-schema 4.4 순서) |
| `checks` | 문자열 배열 | 확인 항목 키 |
| `ai_overuse_candidate` | 불리언 | AI 과잉 후보 |
| `extra` | 객체 배열 | 부가 행: `kind`, `assignee`, `rule`, `reason` |
| `review` | 객체 | 확인 결과: `status`(`pending`, `kept`, `changed`), `assignee`, `reason` |
| `tentative` | 불리언 | 판단이 추정·기본값에 기대 확인하면 바뀔 수 있다(4.12) |
| `now` | 문자열 | 지금 하는 방식(AI `now`, 없으면 4.12 문장) |
| `change` | 문자열 | 바꿔야 할 것(AI `change`, 없으면 담당별 문장) |
| `check` | 문자열 | 확인할 것(AI `check`, 없으면 담당별 문장) |
| `assumptions` | 문자열 배열 | 가정한 내용(추정 답, 기본값 답. 업무 말) |
| `actions` | 객체 배열 | AI `actions` 그대로: `action`, `who_now`, `proposed`, `source`. 없으면 `[]` |
| `gate_note` | 문자열 또는 null | AI `gate_note` 그대로 |

## 4. 규칙

### 4.1 확인하면 바뀔 수 있음

골든 표본마다 규칙 자동화 검토 단계(② ②M) 셈. `말한 것만` = `summary.stated.software`, `확인하면 바뀔 수 있음` = `summary.tentative.software`. 둘을 더하면 `summary.software`다. 제안 추정 = `proposal.tentative`.

| 표본 | ② 단계 | 말한 것만 | 확인하면 바뀔 수 있음 | 제안 단계 | 제안 추정 |
| --- | --- | --- | --- | --- | --- |
| `table-compare` | 4 | 4 | 0 | 1 | 아니오 |
| `form-fill` | 4 | 3 | 1 | 2 | 아니오 |
| `mixed-exceptions` | 2 | 2 | 0 | 1 | 아니오 |
| `reply-drafts` | 1 | 1 | 0 | 1 | 아니오 |
| `signoff-final` | 5 | 5 | 0 | 3 | 아니오 |
| `ai-judgment` | 3 | 2 | 1 | 2 | 아니오 |
| `ai-actions` | 2 | 1 | 1 | 2 | 아니오 |

### 4.4 제안

공개 예제의 먼저 만들 ② 단계와 ② 단계 수(`summary.software`).

| 예제 | 제안 단계 | ② 단계 |
| --- | --- | --- |
| `table-compare` | 1 | 4 |
| `form-fill` | 2 | 4 |
| `mixed-exceptions` | 1 | 2 |
| `reply-drafts` | 1 | 1 |
| `signoff-final` | 3 | 5 |

### 4.6 담당자 확인 문장

`gates[].text`. `human_gate`는 단계 `ai.gate_note`가 있으면 그 글 그대로다. 엔진 문장(`reason`)은 펼쳐 보기에만 둔다.

| 확인 종류 | 화면 문장 |
| --- | --- |
| `signoff` | 담당자가 최종 확인·서명한다. |
| `exception_review` | 규칙으로 판단이 안 되는 예외 건은 담당자가 검토한다. |
| `human_gate` | 이 단계 결과를 다음으로 넘기기 전에 담당자가 확인한다. |

## 5. 확인 파일

단계 번호 1~7이 있는 스코어카드에 넣었을 때의 오류. 오류는 하나다. 출처는 늘 파일 이름(`review.yaml`)이다.

| 확인 파일 | 코드 | 줄 | 열 | 경로 | 메시지 |
| --- | --- | --- | --- | --- | --- |
| `steps: [{number: 3, answer: change, assignee: ④}]` | `required` | 1 | 9 | `steps[1].reason` | `바꾸기에는 사유 한 줄이 필요하다. 사유가 없으면 확정하지 않는다` |
| `steps: [{number: 3, answer: change, assignee: ④, reason: "  "}]` | `required` | 1 | 58 | `steps[1].reason` | `바꾸기에는 사유 한 줄이 필요하다. 사유가 없으면 확정하지 않는다` |
| `steps: [{number: 3, answer: change, reason: 다르다}]` | `required` | 1 | 9 | `steps[1].assignee` | `바꾸기에는 새 담당이 필요하다` |
| `steps: [{number: 3, answer: change, assignee: ④, reason: "a\nb"}]` | `format` | 1 | 58 | `steps[1].reason` | `사유는 한 줄이다` |
| `steps: [{number: 2, answer: change, assignee: ⑤, reason: x}]` | `enum` | 1 | 47 | `steps[1].assignee` | `①, ②, ②M, ③, ④ 가운데 하나여야 한다` |
| `steps: [{number: 2, answer: keep, assignee: ②}]` | `conflict` | 1 | 45 | `steps[1].assignee` | `맞음(keep)에는 담당을 적지 않는다` |
| `steps: [{number: 2, answer: maybe}]` | `enum` | 1 | 29 | `steps[1].answer` | `keep 또는 change여야 한다` |
| `steps: [{number: 9, answer: keep}]` | `range` | 1 | 18 | `steps[1].number` | `없는 단계다: 9` |
| `steps: [{number: two, answer: keep}]` | `type` | 1 | 18 | `steps[1].number` | `정수여야 한다` |
| `steps: [{answer: keep}]` | `required` | 1 | 9 | `steps[1].number` | `필수 키가 없다` |
| `steps: [{number: 2, answer: keep, note: x}]` | `unknown-field` | 1 | 35 | `steps[1].note` | `모르는 키다` |
| `steps: [{number: 2, answer: keep}, {number: 2, answer: change, assignee: ④, reason: x}]` | `duplicate` | 1 | 36 | `steps[2].number` | `같은 단계가 두 번 나온다: 2 (처음: steps[1])` |
| `steps: 3` | `type` | 1 | 8 | `steps` | `목록이어야 한다` |
| `{}` | `required` | 1 | 1 | `steps` | `필수 키가 없다` |
| `[]` | `type` | 1 | 1 | — | `매핑이어야 한다` |
| `{confirmed_in: chat, steps: []}` | `enum` | 1 | 16 | `confirmed_in` | `conversation이어야 한다` |

## 6. HTML 구역

지시문 3.5의 일곱 구역. `id`는 HTML 요소 id다. 화면 순서는 1·2·3·5(오른쪽 칸)·4·6·7이다. 분류 코드·근거 키·파일·해시·schema는 `<details>` 펼쳐 보기 안에만 둔다(T-022).

| 구역 | id | 내용 | 결과 객체 |
| --- | --- | --- | --- |
| 1 요약 | `zone-1` | 가상 시나리오 줄, 상태 네 줄, 결론 세 줄(+확인하면 바뀔 수 있음 질문, 끝맺는 문장), 네 칸: 규칙 자동화를 검토할 단계, 실제 자료 시험, 절감 효과, AI 활용 유지 제안 | `scenario`, `status`, `conclusion`, `open_questions`, `summary`, `verification` |
| 2 단계 표 | `zone-2` | 단계마다 지금 하는 방식 · 추천 · 바꿔야 할 것 · 확인할 것, 확인 상태 글자. 펼쳐 보기: 동작 목록, 코드·근거 키·규칙 의견·다름, 기본값·추정 키, 엔진 부가 행 | `steps` |
| 3 제안 카드 | `zone-3` | 먼저 확인할 것: 단계, 확인 → 판단 → 의뢰서 순서, 효과 문장. 펼쳐 보기: 고른 규칙, `scorecard build` | `proposal` |
| 4 검증 | `zone-4` | 실제 자료 시험: 셈·막대·판정·문턱(시험했을 때만), 아니면 비교 자료를 넣지 않았다는 줄 | `verification` |
| 5 게이트·위험 | `zone-5` | 담당자 확인(화면 문장, 펼쳐 보기에 엔진 문장)과 남는 위험. 판단이 갈린 단계·개선 후보는 다른 카드 | `gates`, `risks`, `opinions`, `candidates` |
| 6 다음 할 일 | `zone-6` | 지금 할 일 / 개선을 진행하려면 / 실제 효과까지 검증하려면(선택). 펼쳐 보기: 구현·검증 담당자 안내 | `todo` |
| 7 개요·입력 기록 | `zone-7` | 업무 요약 네 줄, 사용자가 준 숫자, 입력 원문 보기(접음), 단계 흐름. 펼쳐 보기: 자기 검사, 입력·결과·확인 파일 SHA-256, schema | `task_summary`, `stated_numbers`, `intake`, `steps`, `self_check`, `inputs`, `results`, `review` |

할 일 묶음(4.8).

| 묶음 | 화면 제목 | 키 |
| --- | --- | --- |
| `now` | 지금 할 일 | `review_steps` |
| `improve` | 개선을 진행하려면 | `first_check`, `confirm_inferred`, `confirm_defaults` |
| `verify` | 실제 효과까지 검증하려면(선택) | judgment.json `todo` 키, `run_check`, `define_errors`, `measure_minutes_per_item` |
| `implementer` | 구현·검증 담당자 안내(펼쳐 보기) | `check_command`, `review_file`, `build_command` |
