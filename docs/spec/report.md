# report (스코어카드 결과 객체와 HTML, M2-3)

- 상태: 스펙+구현 한 PR(ADR 0010 B-12). 결정은 추천안 채택(ADR 0005 결정 3), STATUS 잠정 결정 T-020 행. 작업 카드 `docs/tasks/T-020-m2-3-html-scorecard.md`.
- 표·블록은 `docs/spec/data/report.md`(절 번호가 같다). 테스트가 읽는다.
- 원문 요구: 지시문 3.5(일곱 구역), 2절 2(입력 해시), ADR 0004 결정 6 규칙 8(스코어카드 결과 객체는 Core, HTML은 Cli가 그 객체로만 쓴다). 시안: `/mnt/project-files/m2-draft/`.

## 1. 범위

- `scorecard open <케이스> [--review <파일>] [--out <폴더>] [--json]`: 저장된 결과로 스코어카드 결과 객체(`scorecard.json`)와 HTML 한 파일(`scorecard.html`)을 쓴다.
- 확인 파일(`--review`): 단계마다 맞음/바꾸기. 결과 객체에 기록만 한다. 다시 분류·판정하지 않는다.
- 입력 SHA-256: Core가 바이트에서 낸다. 결과 객체와 HTML 7구역에 쓴다.
- 범위 밖(10절): `start`·`check`가 HTML을 쓰는 일, `--times`와 절약 추정, `build`·`--step`, Excel, 확인 뒤 다시 판정.

## 2. 읽는 것

- 출력 폴더(기본 `<케이스>/out`, `--out`)의 결과 파일: `classification.json`(필수), `metrics.json`, `judgment.json`(있으면). 결과 파일은 같은 판의 명령이 쓴 것만 받는다. `schema_version`이 Core 값(classification 2, metrics 1, judgment 2)과 다르거나 모양이 틀리면 `result` 오류(2).
- 케이스 폴더의 입력: `case.yaml`, `answers.yaml`, `intake.md`(있는 것만), `case.yaml` `inputs`·`scores` 패턴에 맞는 파일(`CompareInputs.Read`, metrics.md 4.1). 읽기만 한다.
- 확인 파일: `--review`로 준 파일 하나.
- 숫자·판정은 결과 파일 값을 옮기기만 한다. Core가 다시 계산하지 않고, Cli·HTML은 계산하지 않는다(CLAUDE.md 절대 원칙 4). 막대 길이는 셈 그대로 `flex` 값으로 쓴다(나눗셈 없음).

## 3. 결과 객체

- `ScorecardBuilder.Build`(Core, 순수 함수)가 `ScorecardResult`를 만들고 `ScorecardJson.Render`가 바이트를 만든다. 바이트 규칙은 classification.md 3.3과 같다.
- `open --json`의 표준 출력은 `scorecard.json`과 바이트까지 같다. 봉투가 없다(ADR 0004 결정 6 규칙 5와 같은 방식). json-output.md 5.2 결과 객체의 세 키(`schema_version`, `command`, `files`)가 앞에 있고 내용이 뒤에 붙는다.
- `schema_version`은 2다. 1에서 시작했고(ADR 0008 결정 1), T-022에서 최상위 키 순서와 묶음이 바뀌어 올렸다(ADR 0016, 12절). 결과 객체는 tests TestData 골든만 있어 expected migration이 아니다.
- 속성 표 둘은 데이터 3절이다(최상위, 단계).

## 4. 규칙

### 4.1 요약(1구역)

- `steps` 단계 수, `software` 담당 ②·②M 단계 수, `assignees` 다섯 담당의 단계 수(단계 행 담당), `differs` AI 판단과 규칙 의견이 다른 단계 수, `ai_assist` ③ 단계 수, `ai_overuse` AI 과잉 후보 수, `kept`·`changed` 확인 결과 수.
- `saving`은 지금 늘 `time_needed`다. 시간 입력(`--times`)이 없어 절약을 추정하지 않는다.

### 4.2 단계(2구역)

- classification.json 단계 순서. 단계 행(첫 행)의 담당·근거 키·문장을 단계 값으로, 나머지 행을 `extra`로 둔다.
- `source`·`rule_opinion`·`differs`는 classification.json 값 그대로다. HTML은 AI 판단 옆에 규칙 의견을 두고, 다르면 "다름"을 표시한다.
- 기본값 표시는 `defaults`, AI 추정 표시는 `inferred`로 단계마다 보인다. 근거 문장 끝의 표시도 그대로 둔다.

### 4.3 답 파일

- classification.json에는 `inferred`·`current_actor`가 없다. 케이스 폴더의 `answers.yaml`에서 읽는다.
- `used`: 파싱되고 단계 수·번호·이름이 classification.json과 같다. `missing`: 파일이 없다. `invalid`: 파싱 오류. `mismatch`: 단계가 다르다(다른 답 파일로 `start`를 돌린 경우).
- `used`가 아니면 `inferred`는 `[]`, `current_actor`는 null이다. 오류가 아니다. 결과 객체 `answers`에 상태를 적는다.

### 4.4 제안(3구역)

- 담당 ②·②M 단계 가운데 지금 하는 쪽 순서(`ai_each_time` → `human` → `mixed` → `program` → 모름)로 첫 단계, 같으면 번호가 작은 단계. 없으면 null.
- 이유 문장은 지금 하는 쪽마다 하나(`ReportRules.ProposalOrder`). 예외 검토 행이 있으면 "애매한 건은 사람 검토로 남긴다."를 붙인다.
- 공개 예제 표는 데이터 4.4절이다.

### 4.5 검증(4구역)

- `status`: metrics.json `compare.status`(`measured`, `not_verified`). metrics.json이 없으면 `not_checked`.
- `counts`·`accuracy_pct`·`missing_rate_pct`·`time`은 metrics.json, `verdict`·`verdict_rule`·`verdict_reason`·`thresholds`·`checks`는 judgment.json 값 그대로다.
- `items`는 metrics.json `items` 앞 20개, 나머지는 `items_hidden`에 수만 둔다(HTML 크기).

### 4.6 게이트(5구역)

- 규칙 의견이 `SIGNOFF`이고 담당이 ④인 단계(AI 판단 단계 포함)는 `signoff`, 부가 행 `exception_review`·`human_gate`는 그 종류로 단계 순서대로 둔다. `reason`은 근거 문장 그대로, `text`는 화면 문장(데이터 4.6, 12.4).

### 4.7 남는 위험(5구역)

- 순서: 다름(`differs`) → AI 과잉 후보(`ai_overuse`) → 실행 시 AI 보조(`ai_assist`) → 확인에서 바꾼 단계(`review_changed`) → 판정 보류·조건부(`verdict`). 같은 키 안은 단계 순서. 문장 틀은 `ReportRules`.

### 4.8 다음 할 일(6구역)

- 묶음 순서 `now` → `improve` → `verify` → `implementer`(데이터 6절 묶음 표, 12.5). 묶음 안 키 순서는 표 순서다. judgment.json `todo`는 `verify`에 그대로 둔다. 빈 항목은 넣지 않는다.

### 4.9 입력 SHA-256(7구역)

- `inputs`: 2절 입력 파일마다 `path`(케이스 폴더 기준 상대 경로, `/`)와 `sha256`(소문자 16진). 경로 서수 순, 같은 파일은 한 번.
- `results`: 읽은 결과 파일(classification → metrics → judgment), `review`: 확인 파일(경로는 파일 이름).
- Core가 읽은 바이트로 낸다(`ScorecardBuilder.Hash`, SHA-256). 절대 경로는 쓰지 않는다(G2 R3).

## 5. 확인 파일(`--review`)

```yaml
steps:
  - {number: 2, answer: keep}
  - {number: 6, answer: change, assignee: ④, reason: 해석 문장은 담당자가 직접 쓴다}
```

- YAML(UTF-8). 최상위 매핑 키는 `steps` 하나, 값은 목록. 원소 키 `number`(정수, 있는 단계), `answer`(`keep` 맞음, `change` 바꾸기), `assignee`, `reason`.
- 맞음: `assignee`를 적지 않는다. `reason`은 있어도 된다.
- 바꾸기: `assignee`(① ② ②M ③ ④)와 `reason` 한 줄(비지 않음, 줄바꿈 없음)이 있어야 확정된다. 사유가 없으면 확정하지 않고 입력 오류(2)다. 스코어카드를 쓰지 않는다.
- 적지 않은 단계는 `pending`(확인 전). 같은 단계가 두 번이면 오류.
- 오류는 `CaseError`(case-schema 4.9). 출처는 파일 이름, 경로는 `steps[k].키`. 표는 데이터 5절이다.
- 기록만 한다. 단계의 담당·근거·요약 셈·제안·검증은 바꾸지 않는다. `review` 속성, `kept`·`changed` 셈, 위험 `review_changed`, 할 일 `review_steps`에만 나타난다. HTML은 바꾼 단계에 확인 전후 담당과 사유를 보인다.

## 6. HTML

- 렌더러는 Cli(`ScorecardHtml`)다. 결과 객체만 받아 쓴다. 판정·셈이 없다(ADR 0004 결정 6 규칙 8). 문자열은 `HtmlEncoder`로 쓴다.
- 한 파일. CSS는 `<style>` 안에 둔다. 스크립트·외부 글꼴·이미지·`http://`·`https://` 로드가 없다(네트워크 없음).
- 결정론: 시각·난수 id가 없다. UTF-8(BOM 없음), LF, 끝 `\n`. 같은 결과 객체면 같은 바이트.
- 구역 일곱(지시문 3.5)은 데이터 6절. 넓은 화면은 2열(오른쪽 칸에 3·5구역), 좁은 화면은 1열.
- 폭 390·900·1236px에서 가로 스크롤이 없다(`scrollWidth <= innerWidth`). `tools/html-check/check.mjs`(Playwright)로 잰다. CI에는 아직 넣지 않았다(10절).
- 문구는 한국어. 담당·지금 하는 쪽·판정·항목 상태는 HTML 안의 이름표로 바꿔 보인다(값은 결과 객체 그대로).

## 7. 명령과 오류

- `open`: 케이스 폴더 확인 → 출력 폴더 정하기(json-output.md 4절) → 읽기(2절) → `Build` → `scorecard.json`, `scorecard.html` 쓰기. `--json`이면 표준 출력이 `scorecard.json` 바이트다.
- 오류(모두 2): 케이스 폴더 없음(`file`, 경로 `case`), classification.json 없음(`file` `파일이 없다. start를 먼저 돌린다`), 확인 파일 없음(`file`, 출처 = 파일 이름), 결과 파일 모양(`result`), 확인 파일 오류(5절). 오류면 아무것도 쓰지 않는다.
- `--review`는 `open`만 받는다. 다른 명령에서는 "이 명령에 없는 옵션이다"(json-output.md 데이터 6.3).
- G2·G3 대상 `cli-open-case-valid`(샘플 `case-valid`): start → check → open `--json`. 출력 폴더 파일과 `stdout.json`. json-output.md 데이터 7절.

## 8. 출력 폴더와 입력 기록

- 기본 `<케이스>/out`을 그대로 쓴다(T-018 1). 같은 이름 파일은 덮어쓴다. 실행마다 새 폴더를 만들지 않는다.
- 대신 스코어카드가 읽은 입력과 결과 파일의 SHA-256을 남긴다(4.9). 덮어써도 어떤 입력에서 나온 스코어카드인지 확인할 수 있다. 지시문 2절 2 "입력 해시를 기록"을 이 길로 채운다.
- "출력은 새 폴더에"(2절 2)의 실행마다 새 폴더는 LATER다. `--out`으로 사용자가 새 폴더를 줄 수 있다.

## 9. 테스트

- 골든: 공개 예제 5종과 TestData 표본 `ai-judgment`(AI 판단, 확인 파일)의 `scorecard.json`·`scorecard.html`을 `tests/Scorecard.Tests/TestData/report/<이름>/`에 둔다. `examples/*/expected`가 아니다(expected migration이 아니다). 다시 쓸 때만 `SCORECARD_UPDATE_GOLDEN=1`.
- 결정론(다른 경로·문화권), 외부 로드 없음, 구역 일곱, 속성 표, 숫자가 결과 파일 값과 같음, 기본값·추정 표시, 입력 해시, 확인 파일 오류 표·성공, 결과 파일 없음·깨짐.

## 10. 뒤로 미룬 것 (`docs/LATER.md` "M2-3(T-020)에서 미룬 것")

- `start`·`check`가 HTML을 같이 쓰는 일. 지금은 `open`만 쓴다.
- `--times`(시간 입력)와 절약 추정(`saving`), 사람 입력 판정.
- 확인 뒤 다시 분류·판정, 스킬 `/scorecard`가 확인 파일을 쓰는 길.
- 실행마다 새 출력 폴더, `build --step`, Excel, html-check의 CI 잡.

## 11. 이 카드의 선택 (추천안 채택, STATUS 잠정 결정 T-020)

1. 결과 객체는 Core(`Scorecard.Core.Report`), HTML 렌더러는 Cli(`ScorecardHtml`).
2. `open`은 저장된 결과 JSON을 읽는다. 다시 분류·대조하지 않는다.
3. `inferred`·`current_actor`는 케이스 폴더 `answers.yaml`에서. 단계가 다르면 쓰지 않고 상태만 적는다.
4. 확인 파일은 YAML, 사유 없는 바꾸기는 입력 오류(2).
5. 확인은 기록만. 다시 판정하지 않는다.
6. 출력 폴더는 `<케이스>/out` 그대로, 입력·결과·확인 파일 SHA-256 기록.
7. 결과 객체 `schema_version` 2(T-022에서 1 → 2, ADR 0016 결정 3).
8. 제안 규칙: ②·②M 가운데 지금 하는 쪽 순서 → 번호.
9. 검증 항목은 20개까지 보이고 나머지는 수만.
10. 골든은 tests TestData.
11. G2·G3 대상 `cli-open-case-valid`.

## 12. 첫 실사용 반영 (T-022, ADR 0016)

- 지시문 0016(첫 실사용 결과)을 스킬과 HTML에 반영한다. 엔진(분류 트리·대조·판정)과 `examples/**/expected/**`는 그대로다.
- 스킬 선택 항목(case-schema.md 4.15)은 답 파일에만 있다. 공개 예제에는 없다. 없으면 아래 값은 빈 값·null이다.

### 12.1 상태 네 줄(`status`)

- `material`: 답 파일 `scenario`(`virtual`, `real`), 없으면 `unknown`. `classification`: AI 판단 단계가 하나라도 있으면 `ai_draft`(설명 기반 초안), 아니면 `rule_tree`.
- `confirmed`·`steps`: 확인 결과 `kept`+`changed` 수와 단계 수. `confirmed_in`: 확인 파일의 값. `test`: 4.5 `status`.
- `scenario: virtual`이면 HTML 맨 위에 "가상 시나리오 기반. 실제 운영 수치·검증 결과 아님" 한 줄.

### 12.2 결론 세 줄(`conclusion`)

- `keep` 유지할 것(④ 단계, ① 단계, 담당자 확인 수), `first_check` 먼저 확인할 것 하나(제안 단계의 `check`), `undecided` 아직 판단하지 않은 것(추정 단계 수, 남은 질문 수, 시험·절감 상태). `end_state`는 답 파일 그대로.

### 12.3 말한 것만과 확인하면 바뀔 수 있음

- 단계 `tentative`: AI 판단 단계는 `inferred`가 있거나 동작 하나라도 `source`가 `stated`가 아니면 참. 규칙 단계는 단계 행 문장에 기본값·추정 표시가 있으면 참.
- `summary.stated`는 `tentative`가 거짓인 단계만, `summary.tentative`는 참인 단계만 센다. 둘을 더하면 전체다. AI 과잉 후보는 `current_actor`가 추정이어도 `tentative`로 센다.
- 제안(4.4)은 `tentative`가 거짓인 단계를 먼저 고른다. 모두 참이면 그 가운데서 고르고 `proposal.tentative`를 참으로 두고 효과 문장 끝에 그 뜻을 붙인다. 표는 데이터 4.1.
- 단계의 네 칸 `now`·`change`·`check`는 AI 글이 있으면 그대로, 없으면 지금 하는 쪽·담당별 문장이다. `assumptions`는 추정·기본값 답을 업무 말("가정: 처리 주기 주 1회")로 쓴다.

### 12.4 담당자 확인

- 화면에 "사람 게이트"를 쓰지 않는다. `gates[].text`가 화면 문장이고, `human_gate`는 단계 `ai.gate_note`(기존 확인 단계와 합치는 말, 보내기 전 확인)가 있으면 그 글이다. 엔진 행은 그대로 둔다.
- 판단이 갈린 단계(`opinions`)와 개선 후보(`candidates`)는 남는 위험(`risks`)과 다른 카드다. 위험에는 무엇이 틀릴 수 있고 지금 어떻게 잡는가만 둔다.

### 12.5 할 일과 확인

- 할 일은 지금 할 일 / 개선을 진행하려면 / 실제 효과까지 검증하려면(선택), 파일 경로·명령은 구현·검증 담당자 안내(펼쳐 보기).
- 검증 칸: 비교 자료가 없으면 "이번 실행에는 비교 자료를 넣지 않았다." 문턱은 시험했을 때만 보인다. 입력된 `minutes_per_item`(기본값 아님)은 `summary.minutes`로 절감 칸에 보인다.
- 확인 파일 최상위 `confirmed_in: conversation`(선택)은 스킬 대화에서 확인했다는 기록이다. 단계 상태 글자가 "대화에서 확인됨"이다. 다른 값은 `enum` 오류(데이터 5절).
- 분류 코드·근거 키·파일·해시·schema는 `<details>` 펼쳐 보기 안에만 둔다.
