---
name: scorecard
description: 사용자의 업무를 단계와 동작으로 나누고 단계마다 담당(①~④, ②M)을 판단해 스코어카드를 만들 때. /scorecard 입구
---
# /scorecard

사용자의 업무를 단계로 나누고, 단계마다 사람 손이 가는 동작을 적고, 그중 규칙으로 할 수 있는 것을 판단한다. 엔진(`scorecard` 명령)이 규칙 의견을 같이 적고, 검증 숫자와 판정을 낸다.
근거: ADR 0010 A(AI 분류 + 규칙 검증), ADR 0013(classification.json schema 2), ADR 0016(첫 실사용 반영), `docs/spec/classification.md` 4.13, `docs/spec/case-schema.md` 4.4·4.13·4.14·4.15, `docs/spec/report.md` 12절, `docs/spec/json-output.md`.
출력 규칙: `docs/spec/skill-output-rules.md`(R1~R13). 결과를 내기 전에 모두 본다.

판단 기준 한 줄: **이 단계에서 사람 손이 가는 동작이 무엇이고, 그중 규칙으로 할 수 있는 것은 무엇인가.** 분류 코드를 고르는 일이 아니라 이 질문에 답하는 일이다.

## 지킬 것

- 숫자를 스킬이 셈하지 않는다. 정확도·누락률·시간·판정·행 수·단계 셈은 명령의 JSON(`classification.json`, `metrics.json`, `judgment.json`, `scorecard.json`)에 있는 값만 옮긴다. 엔진 숫자·판정을 고치거나 다시 셈하지 않는다.
- 엔진은 묻지 않는다. 묻는 것은 이 스킬 대화뿐이다. 명령은 파일 입력과 옵션만 받는다.
- 사용자 확인(맞음/바꾸기) 전에는 `answers.yaml`을 쓰지 않고 명령을 부르지 않는다.
- 어떤 자료를 AI에 보낼지는 사용자가 정한다. 처음에 아래 안내를 그대로 보여 준다.

> AI에 보내면 안 되는 자료는 넣지 마세요. 구조만 설명해도 분류는 됩니다.

- 케이스 폴더는 `cases/<케이스 이름>/`이다. `cases/`는 커밋하지 않는다. 케이스 이름은 소문자·숫자·`-`만 쓴다(64자까지).
- 입력 파일은 읽기만 한다. 결과는 출력 폴더(기본 `<케이스>/out`)에만 쓴다.
- 말하지 않은 것을 사실로 쓰지 않는다(R1). 문장마다 출처가 `stated`(사용자가 말함), `inferred`(AI 추정), `default`(기본값) 중 하나다. `inferred`·`default`는 문장을 추정형으로 쓴다: "현재 방식은 확인되지 않았다. AI가 하고 있다면 규칙으로 바꿀 후보다."
- 화면과 대화에는 업무 말을 쓴다. 분류 코드(①~④, `Q3`, `PARTIAL`)와 질문 키(`rule_expressible` 등)는 사용자가 물을 때만 쓴다.

## 1. 업무 설명 받기

1. 사용자에게 업무를 자유롭게 적어 달라고 한다. 무엇을 받아서, 무엇을 하고, 결과를 어디로 보내는지.
2. 받은 글을 고치지 않고 `cases/<케이스>/intake.md`에 그대로 저장한다.
3. 사용자가 준 숫자는 준 모양대로 적어 둔다(R10, `stated_numbers`). "연 1회, 집중 기간 10주, 주 10건"을 주기 하나로 바꾸지 않는다. "예외 1건 처리에 15분"은 예외 1건 전체의 시간이지 한 단계의 시간이 아니다.
4. 업무 요약 네 줄을 사용자 말로 만든다(`summary`: `scale` 규모, `current` 현재 처리, `exception_rate` 예외율, `delivery` 결과 전달).
5. 가상 시나리오·연습 입력이면 `scenario: virtual`, 사용자가 자기 업무라고 하면 `real`이다. 모르면 적지 않는다(R13).
6. 이 단계에서는 묻지 않는다. 질문은 2절 판단을 마친 뒤 한 번에 묻는다(4절).

## 2. 단계 나누기와 판단

1. 업무를 단계로 나눈다. 단계 이름은 사용자의 말로 짧게 쓴다. 케이스 안에서 같은 이름을 두 번 쓰지 않는다.
2. 단계마다 사람이 실제로 하는 동작을 먼저 적는다(R6, `actions`). 받기·열기·찾기·읽기·옮겨 적기·비교하기·판단하기·쓰기·붙이기·보내기·기록하기 수준이다.
   - 동작마다 `action`(동작), `who_now`(지금 하는 쪽: `human`, `program`, `ai_each_time`, `mixed`, `unknown`), `proposed`(추천 담당), `source`(출처)를 적는다.
   - 설명에 없는 동작은 "이 단계에서 보통 사람이 하는 동작"으로 추정하고 `source: inferred`로 적는다.
   - `source: stated`는 동작이 사용자가 준 말 그대로일 때만이다. 방법·조건을 붙이면("규정에 맞춰", "상황에 맞춰") 말한 동작(`stated`)과 붙인 방법(`inferred`) 두 동작으로 나눈다. 내용을 질문이나 `open_questions`로 묻는 동작은 `stated`가 될 수 없다.
3. 동작마다 담당을 정하고, 단계 담당은 동작 분류의 합으로 정한다(`ai: {assignee, reason}`). 예: "검토 완료 자료 전달" = 파일 모으기 ② + 수신자 고르기 ② + 메일 쓰기 ②(양식) + 보내기 전 확인 ④ + 보내기 ②.
   - ① 기존 기능·설정. **지금 이미 사람 손 없이 돌아갈 때만**이다(R3). 사람이 도구를 손으로 조작하면(클릭·복사·첨부·폴더 이동) ② 후보다. "기존 도구에 기능이 있을 수 있다"는 ①이 아니라 `check`(확인할 것)에 적는다.
   - ② 규칙 기반 소프트웨어. AI로 만들고 실행 때 AI가 없다. "같은 결과를 낸다"고 쓰지 않는다. "규칙으로 검사할 수 있다"까지 쓰고, 규칙으로 안 되는 부분을 같이 쓴다(R7).
   - ②M 학습 모델(결정론). 미리 학습해 고정한 모델을 규칙 SW처럼 돌린다. 실행 때 AI를 부르지 않고 같은 입력이면 같은 결과다. ②의 한 갈래다(`classification.md` 4.13).
   - ③ 실행 시 AI 보조. 사람이 확인한다. 자동 승인은 하지 않는다. 사유가 몇 가지로 묶이면 양식(②)이고, 묶이지 않는 건만 ③이다(R8).
     - 사유·상황이 몇 가지로 묶이는지 모르면 "묶이지 않아"라고 쓰지 않는다. "묶이는지 확인되지 않았다. 묶이면 양식(②), 안 묶이는 건만 ③"으로 쓴다. 화면 글(`reason`)에서는 코드 대신 "양식(규칙)", "AI 초안 + 담당자 확인"으로 쓴다.
   - ④ 사람이 한다.
4. `reason`에는 결론과 이유를 같이 쓴다(R4). 동작이 섞이면 무엇이 규칙이고 무엇이 아닌지 나누어 쓴다. 한 단계에 두 일이 섞이면 "두 활동이 있다"고 쓰고 건수를 따로 적는다(R11).
5. 단계 표 세 칸을 쓴다: `now`(지금 하는 방식), `change`(바꿔야 할 것), `check`(확인할 것). "담당 ②"처럼 지금인지 추천인지 모를 말을 쓰지 않는다. 지금 방식을 사용자가 말하지 않았으면 `now`를 추정형으로 쓰거나 비운다.
6. 단계마다 질문 10개 체크리스트를 채운다(`answers`). 키와 선택지는 `case-schema.md` 4.4 표다: `input_kind`, `rule_expressible`, `frequency`, `batch_size`, `exception_rate`, `current_actor`, `minutes_per_item`, `failure_impact`, `existing_feature`, `human_signoff`.
   - 모르면 그 키를 비워 둔다(적지 않는다). 엔진이 기본값을 넣고 "기본값 사용, 확인 필요"를 붙인다.
   - 설명에서 AI가 짐작한 답은 그 단계의 `inferred` 목록에 키를 적는다. 짐작한 답은 `answers`에 값이 있어야 한다. 주기·건수를 엔진용 값으로 맞췄으면 그것도 짐작이다.
   - `minutes_per_item`은 사용자가 그 단계의 건당 시간을 말했을 때만 적는다.
7. 체크리스트는 판단을 돕는 참고다. 담당은 AI가 정한다. 엔진은 같은 답으로 규칙 트리를 돌려 "규칙 의견"을 따로 낸다.
8. 담당자 확인을 흐름에 붙인다(R9, `gate_note`). "사람 게이트"라고 쓰지 않는다. 누가·언제·무엇을 확인하는지 쓴다. 이미 사람이 확인하는 단계(원본 대조, 최종 승인)가 있으면 그 단계에 합친다("4단계 원본 대조 때 함께 본다"). 보내기·전달 단계의 확인은 보내기 전에 둔다.
   - `gate_note`는 엔진이 그 단계에 담당자 확인 행(`human_gate`)을 붙일 때만 화면에 나온다. 엔진은 "틀렸을 때 영향"(`failure_impact`)이 외부·안전·법적일 때 붙인다.
   - "틀렸을 때 영향"을 모르는데 보내기 전 확인을 권하면 그 말을 `reason`에 쓰고, "결과가 외부로 나가나"를 질문이나 `open_questions`로 남긴다.
   - 보내기 전 확인을 둘 때는 보내기 동작이 확인할 것을 만드는 동작보다 뒤에 있는지 본다. 앞에 있으면 단계나 동작 순서를 고친다.

## 3. 서명 단계 묻기

1. "이 업무에서 사람이 최종 확인·서명하는 단계가 어디인가요?"를 한 번만 묻는다. 없으면 없다고 받는다. 4절 질문과 같이 물어도 된다(3개 안에 센다).
2. 그 단계는 `human_signoff: yes`, 나머지 모든 단계는 `human_signoff: no`로 적는다. 단계마다 따로 묻지 않는다(ADR 0003).

## 4. 사용자 확인

1. 질문은 답에 따라 추천이 바뀌는 것만, 한 번에 많아야 3개다(R11). 코드로 고르게 하지 않는다: "safety_legal인가요?"가 아니라 "이 자료는 내부 분석에만 쓰나요, 외부 공시나 공식 제출에도 쓰나요?"
2. 묻지 않은 모름 항목은 `open_questions`나 `inferred`로 남긴다. 스코어카드에 "확인하면 바뀔 수 있음"으로 보인다.
3. 단계마다 보여 준다: 단계 이름, 지금 하는 방식, 추천과 이유, 바꿔야 할 것, 확인할 것, 가정한 내용(키 이름이 아니라 "처리 주기를 집중 기간 기준 주 1회로 가정"처럼).
4. 단계마다 사용자가 고른다.
   - 맞음: 그대로 둔다.
   - 바꾸기: 새 담당(또는 답)과 이유 한 줄을 받는다. 이유는 근거(`reason`)에 반영한다.
5. 고른 단계만 확인된 것이다(R12). "검증 자료 없이"를 "모두 맞음"으로 해석하지 않는다. 고르지 않은 단계는 확인 전으로 둔다.
6. 자기 검사를 한다(`skill-output-rules.md` 4절). 문장마다 근거가 intake.md나 사용자가 대화에서 한 말에 있는지 본다. 없으면 출처를 `inferred`로 바꾸고 문장을 추정형으로 고친다. R1~R13을 보고 결과를 `self_check`에 적는다. R1~R5는 늘 적는다.
   - 맞대어 본다: 물은 질문과 `open_questions`에 든 내용이 `reason`·`actions`·`now`에 단정형이나 `source: stated`로 적혀 있지 않은가. 있으면 추정형과 `inferred`로 고친다.

## 5. answers.yaml 쓰기와 start

1. `cases/<케이스>/answers.yaml`을 쓴다. 모양은 `case.yaml`의 부분(`name`, `task`, `steps`)과 케이스 메모(`case-schema.md` 4.15)다. 단계 항목은 `name`, `answers`, `inferred`(있을 때), `ai`다.

```yaml
name: <케이스 이름>
task: <업무 한 줄>
scenario: virtual
summary: {scale: <규모>, current: <현재 처리>, exception_rate: <예외율>, delivery: <결과 전달>}
stated_numbers: [<사용자가 준 숫자 그대로>]
open_questions: [<남은 질문>]
self_check:
  - {rule: R1 출처 없는 단정 0건, result: pass}
  - {rule: R2 집계는 stated만, result: pass}
  - {rule: R3 ①은 손이 안 가는 경우만, result: pass}
  - {rule: R4 추천 문장에 이유, result: pass}
  - {rule: R5 종료점 문장, result: pass}
end_state: 업무 구조 검토 완료. 지금 제출할 것 없음.
steps:
  - name: <단계 이름>
    answers: {input_kind: table, rule_expressible: yes, frequency: weekly, batch_size: 20, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
    inferred: [frequency]
    ai:
      assignee: ②
      reason: <결론과 이유>
      actions:
        - {action: <동작>, who_now: human, proposed: ②, source: stated}
      now: <지금 하는 방식>
      change: <바꿔야 할 것>
      check: <확인할 것>
      gate_note: <누가·언제·무엇을 확인하나>
```

2. `end_state`는 `check`를 돌리지 않고 스코어카드를 내면 늘 "업무 구조 검토 완료. 지금 제출할 것 없음."으로 적는다(R5). 구조만 보자고 했을 때, 검증 자료를 주지 않았을 때, 답을 기다리는 질문이 남았을 때 모두 같다. `check`를 돌렸을 때만 적지 않는다.
3. 분류를 부른다. 저장소 안에서는 `dotnet run --project src/Scorecard.Cli --`가 `scorecard`다.

```text
scorecard start cases/<케이스> --answers cases/<케이스>/answers.yaml --json
```

4. 종료 코드가 2면 오류 JSON의 `errors`(`source`, `line`, `column`, `path`, `message`)를 사용자에게 그대로 보여 주고, 고친 뒤 다시 부른다. 1이면 내부 오류로 알린다.
5. 성공(0)이면 결과 객체의 `files`에 있는 `classification.json`을 출력 폴더에서 읽는다.

## 6. 결과 보여 주기

1. 대화에서 확인한 결과를 확인 파일 `cases/<케이스>/review.yaml`로 쓴다(`report.md` 5절). 최상위에 `confirmed_in: conversation`을 두고, 사용자가 고른 단계만 적는다(R12). 사용자가 고른 단계가 하나도 없으면 확인 파일을 쓰지 않고 `--review` 없이 `open`을 부른다. 빈 `steps`에 `confirmed_in: conversation`을 적으면 대화에서 확인한 것처럼 보인다.

```yaml
confirmed_in: conversation
steps:
  - {number: 1, answer: keep}
  - {number: 3, answer: change, assignee: ④, reason: <사용자가 준 이유 한 줄>}
```

2. 스코어카드를 쓴다. 확인 파일이 없으면 `--review`를 뺀다.

```text
scorecard open cases/<케이스> --review cases/<케이스>/review.yaml --json
```

3. 결론부터 세 줄로 말한다: 유지할 것 / 먼저 확인할 것 하나 / 아직 판단하지 않은 것. `scorecard.json`의 `conclusion` 값을 옮긴다. 셈은 그 아래에 말한다.
4. 셈은 `summary.stated`(말한 것만)를 말하고, `summary.tentative`는 "확인하면 바뀔 수 있음"으로 따로 말한다(R2). 스킬이 다시 세지 않는다.
5. 먼저 확인할 것(`proposal`)은 기존 기능으로 되는지 확인 → 안 되면 새로 만들지 판단 → 만들기로 하면 의뢰서 순서다. `effect` 문장으로 왜 이것을 먼저 보는지 말한다(R4). 의뢰서(`scorecard build`)와 `export-check`는 사용자가 만들자고 한 뒤에만 안내한다.
6. 단계마다 AI 판단(`rows[0]`의 담당·근거)과 규칙 의견(`rule_opinion`)을 본다. `differs`가 true인 단계는 추천 결론과 이유를 먼저 말한다: "담당자 확인 유지 권장. 숫자 대조는 자동화할 수 있지만 범위 판단이 들어 있다." "다릅니다"로 끝내지 않는다. 사용자가 바꾸면 4절로 돌아가 `answers.yaml`을 다시 쓴다.
7. 부가 행(④ 예외 검토 `PARTIAL`, ④ 엔진 확인 행 `GATE`)은 엔진이 붙인 대로 둔다. 빼지 않는다. 말할 때는 "담당자 확인"과 `gates[].text`(`gate_note`를 쓴 곳은 그 글)로 말한다.
8. 기본값 표시·추정 표시, `checks`(측정 권고), `ai_overuse_candidate`는 파일에 적힌 대로 옮긴다. 추정에 기댄 과잉 후보는 "확인하면 바뀔 수 있음"으로 말한다.
9. `check` 없이 끝내면 마지막 줄은 `end_state` 문장이다. 다시 묻는 질문(답을 기다리는 질문)은 그 앞에 둔다. 다음 할 일은 지금 할 일 / 개선을 진행하려면 / 실제 효과까지 검증하려면(선택) 세 묶음으로 말하고, 파일 경로·열 이름·허용 오차는 구현·검증 담당자 안내로 따로 둔다.

## 7. 검증 안내 (check)

1. 실제 효과까지 보려는 사용자에게만 안내한다(선택). AI를 적용한 단계의 결과를 검증하려면 자료 두 벌이 필요하다.
   - `cases/<케이스>/reference/`: 사람이 만든 결과(기준).
   - `cases/<케이스>/candidate/`: AI 적용 후 결과.
2. 자료는 사용자가 직접 넣는다. 스킬은 자료를 만들거나 고치지 않는다. 사용자가 "있다"고 한 자료를 없다고 말하지 않는다.
3. 먼저 무엇을 오류로 셀지 사용자와 정한다. 그다음 `cases/<케이스>/case.yaml`에 `inputs`(`reference`, `candidate`, `key`), `compare`, `thresholds`를 적는다. 모양은 `case-schema.md` 4.5다. 단계는 `answers.yaml`과 같게 둔다.
4. 대조와 판정을 부른다.

```text
scorecard check cases/<케이스> --json
```

5. 출력 폴더의 `metrics.json`·`judgment.json`에 있는 숫자와 판정을 그대로 보여 준다. 자료를 넣지 않았으면 "이번 실행에는 비교 자료를 넣지 않았다."로 말한다. 적용하지 않은 문턱은 말하지 않는다.
6. 입력된 시간은 그대로 보여 주고, 개선 전후 비교에 필요한 단계별 시간이 없으면 절감은 계산하지 않았다고 말한다. "시간 입력이 없다"고 쓰지 않는다.
7. `check` 뒤에는 6절 2번 `open`을 다시 불러 스코어카드를 새로 쓴다.
