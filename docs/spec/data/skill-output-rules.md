# skill-output-rules 데이터

`docs/spec/skill-output-rules.md`의 표. 절 번호가 같다.

## 3. 채점 표

`규칙` 칸은 `self_check[].rule`에 그대로 쓴다. `수용`이 예인 규칙이 M2-2 수용 기준이다(6절).

| 규칙 | 수용 | 실패로 보는 것 | 보는 곳 |
| --- | --- | --- | --- |
| R1 출처 없는 단정 0건 | 예 | intake.md·대화에 근거가 없는데 단정형인 문장, `source: stated`인 추정 동작(방법·조건을 붙인 동작 포함), 질문·`open_questions`로 물은 내용을 단정·`stated`로 씀 | `ai.reason`, `now`, `change`, `check`, `actions[].source`, `inferred` |
| R2 집계는 stated만 | 예 | 대화나 글에서 추정이 섞인 셈을 확정 셈으로 말함 | 대화, `summary.stated`·`summary.tentative` |
| R3 ①은 손이 안 가는 경우만 | 예 | 사람이 클릭·복사·첨부·이동하는 동작이 있는데 ① | 단계 `ai.assignee`, `actions[].proposed` |
| R4 추천 문장에 이유 | 예 | 이유 없는 결론, "다르다"로 끝나는 문장, 효과·이유 없는 제안 | `ai.reason`, 제안 대화 |
| R5 종료점 문장 | 예 | 자료 없이 끝냈는데 `end_state`가 없거나 다른 문장 | `end_state` |
| R6 동작 단위로 나눈다 | 아니오 | `actions`가 없는 AI 판단 단계 | `ai.actions` |
| R7 ②는 규칙으로 검사할 수 있다까지 | 아니오 | "같은 결과를 낸다" 단정, 규칙으로 안 되는 부분 누락 | ② 단계 `ai.reason` |
| R8 ③도 동작 단위로 | 아니오 | 묶이는 사유까지 ③ | ③ 단계 `actions` |
| R9 담당자 확인은 흐름에 붙인다 | 아니오 | "사람 게이트" 표현, 보낸 뒤 확인, 기존 확인 단계와 겹침, 영향을 모르는데 보내기 전 확인을 확정처럼 씀 | `gate_note`, 대화 |
| R10 사용자 숫자는 준 모양대로 | 아니오 | 바꾼 숫자, 다른 대상의 시간으로 옮김 | `stated_numbers`, `summary` |
| R11 질문은 추천이 바뀌는 것만 3개까지 | 아니오 | 4개 이상, 코드로 고르게 함 | 대화, `open_questions` |
| R12 확인 기록은 실제로 확인한 것만 | 아니오 | 고르지 않은 단계를 `keep`으로 적음, 고른 단계가 없는데 확인 파일을 씀 | 확인 파일 |
| R13 자료 성격을 적는다 | 아니오 | 가상 입력인데 `scenario`가 없음 | `scenario` |
| R14 판단·실행·책임을 나눈다 | 아니오 | AI 판단 단계에 `judgment`·`execution`·`accountability`가 없음, 셋이 같은 말 | `ai.judgment`, `ai.execution`, `ai.accountability`, `ai.roles` |
| R15 책임만으로 사람에게 준비·실행을 주지 않는다 | 아니오 | 모으기·요약·목록·기록 같은 준비·실행을 책임 때문에 `human`이나 ④로 둠 | `ai.roles.human`, `ai.assignee` |
| R16 배분 근거는 다섯 질문으로 | 아니오 | `basis`가 없거나 결론만 있음, `varies`가 입력값인지 맥락 해석인지 말하지 않음 | `ai.basis`, `ai.varies` |
| R17 도구 제안은 열한 칸을 채운다 | 아니오 | 예외 조건·기존 기능 범위·부담 검토가 비었거나 "없음"만 있음, 일이 줄어드는지 보지 않음 | `tools` |
| R18 역할 전환은 근거를 적는다 | 아니오 | `evidence`가 사례·검증 결과·운영 데이터가 아님, AI 자신감을 근거로 씀 | `transitions` |
| R19 측정 없는 점수·절감률 0건 | 아니오 | 엔진 JSON·`stated_numbers`에 없는 점수·절감률·퍼센트·절약 시간 | `tools`, `transitions`, `basis`, 대화 |
| R20 세 역할을 억지로 채우지 않는다 | 아니오 | 근거 없는 역할을 채움, 모든 단계에 세 역할이 다 있음 | `ai.roles` |
| R21 업무 이해는 사실과 가정을 나눈다 | 아니오 | `facts`에 추정이 섞임, 목적·완료 조건이 없음 | `purpose`, `done_when`, `facts`, `assumptions` |
