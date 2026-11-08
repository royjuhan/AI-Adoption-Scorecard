# report 데이터

`docs/spec/report.md`의 표·블록이다. 테스트가 읽는다. 규칙 글은 본문에 있다. 절 번호가 같다.

## 3. 결과 객체 (scorecard.json = `open --json`)

최상위 속성. 순서는 파일 안 순서다.

| 속성 | 형식 | 뜻 |
| --- | --- | --- |
| `schema_version` | 정수 | 결과 객체 판. 지금 1 |
| `command` | 문자열 | `open` |
| `files` | 문자열 배열 | 출력 폴더에 쓴 파일: `scorecard.json`, `scorecard.html` |
| `case` | 문자열 | classification.json의 `case` |
| `task` | 문자열 또는 null | answers.yaml(없으면 case.yaml)의 `task` |
| `intake` | 문자열 또는 null | intake.md 글 그대로(BOM 뗌, 줄바꿈 LF, 끝 줄바꿈 뗌) |
| `answers` | 문자열 | answers.yaml을 썼나: `used`, `missing`, `invalid`, `mismatch`(4.3) |
| `summary` | 객체 | 1구역 셈: `steps`, `software`, `assignees`(① ② ②M ③ ④ 다섯), `differs`, `ai_assist`, `ai_overuse`, `kept`, `changed`, `saving` |
| `steps` | 객체 배열 | 2구역 단계(아래 표) |
| `proposal` | 객체 또는 null | 3구역: `step`, `name`, `reason`. ② 단계가 없으면 null |
| `verification` | 객체 | 4구역: `status`, `reason`, `counts`, `accuracy_pct`, `missing_rate_pct`, `verdict`, `verdict_rule`, `verdict_reason`, `thresholds`, `time`, `checks`, `items`, `items_hidden` |
| `gates` | 객체 배열 | 5구역 사람 게이트: `step`, `name`, `kind`(`signoff`, `exception_review`, `human_gate`), `reason` |
| `risks` | 객체 배열 | 5구역 남는 위험: `key`, `step`(없으면 0), `text` |
| `todo` | 객체 배열 | 6구역 다음 할 일: `key`, `step`, `text` |
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

## 4. 규칙

### 4.4 제안

공개 예제의 먼저 만들 ② 단계와 ② 단계 수(`summary.software`).

| 예제 | 제안 단계 | ② 단계 |
| --- | --- | --- |
| `table-compare` | 1 | 4 |
| `form-fill` | 2 | 4 |
| `mixed-exceptions` | 1 | 2 |
| `reply-drafts` | 1 | 1 |
| `signoff-final` | 3 | 5 |

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

## 6. HTML 구역

지시문 3.5의 일곱 구역. `id`는 HTML 요소 id다. 화면 순서는 1·2·3·5(오른쪽 칸)·4·6·7이다.

| 구역 | id | 내용 | 결과 객체 |
| --- | --- | --- | --- |
| 1 요약 | `zone-1` | 대체할 수 있는 단계 m/n, 담당 막대, 정확한가, 시간이 줄었나, AI가 꼭 필요했나 | `summary`, `verification` |
| 2 단계 표 | `zone-2` | 단계마다 지금 → 담당 배지, 근거, 기본값·AI 추정 표시, AI 판단·규칙 의견·다름, 부가 행, 확인 결과 | `steps` |
| 3 제안 카드 | `zone-3` | 먼저 만들 ② 단계와 `scorecard build <case>` | `proposal` |
| 4 검증 | `zone-4` | 셈, 막대, 판정과 문턱, 확인 다섯(여섯), 차이 항목 | `verification` |
| 5 게이트·위험 | `zone-5` | 서명·예외 검토·게이트 행, 남는 위험 | `gates`, `risks` |
| 6 다음 할 일 | `zone-6` | 할 일 목록 | `todo` |
| 7 개요·입력 기록 | `zone-7` | intake.md 그대로, 단계 흐름, 입력·결과·확인 파일 SHA-256 | `intake`, `steps`, `inputs`, `results`, `review` |
