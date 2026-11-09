# skill-output-rules 데이터

`docs/spec/skill-output-rules.md`의 표. 절 번호가 같다.

## 3. 채점 표

`규칙` 칸은 `self_check[].rule`에 그대로 쓴다. `수용`이 예인 규칙이 M2-2 수용 기준이다(6절).

| 규칙 | 수용 | 실패로 보는 것 | 보는 곳 |
| --- | --- | --- | --- |
| R1 출처 없는 단정 0건 | 예 | intake.md·대화에 근거가 없는데 단정형인 문장, `source: stated`인 추정 동작 | `ai.reason`, `now`, `change`, `check`, `actions[].source`, `inferred` |
| R2 집계는 stated만 | 예 | 대화나 글에서 추정이 섞인 셈을 확정 셈으로 말함 | 대화, `summary.stated`·`summary.tentative` |
| R3 ①은 손이 안 가는 경우만 | 예 | 사람이 클릭·복사·첨부·이동하는 동작이 있는데 ① | 단계 `ai.assignee`, `actions[].proposed` |
| R4 추천 문장에 이유 | 예 | 이유 없는 결론, "다르다"로 끝나는 문장, 효과·이유 없는 제안 | `ai.reason`, 제안 대화 |
| R5 종료점 문장 | 예 | 자료 없이 끝냈는데 `end_state`가 없거나 다른 문장 | `end_state` |
| R6 동작 단위로 나눈다 | 아니오 | `actions`가 없는 AI 판단 단계 | `ai.actions` |
| R7 ②는 규칙으로 검사할 수 있다까지 | 아니오 | "같은 결과를 낸다" 단정, 규칙으로 안 되는 부분 누락 | ② 단계 `ai.reason` |
| R8 ③도 동작 단위로 | 아니오 | 묶이는 사유까지 ③ | ③ 단계 `actions` |
| R9 담당자 확인은 흐름에 붙인다 | 아니오 | "사람 게이트" 표현, 보낸 뒤 확인, 기존 확인 단계와 겹침 | `gate_note`, 대화 |
| R10 사용자 숫자는 준 모양대로 | 아니오 | 바꾼 숫자, 다른 대상의 시간으로 옮김 | `stated_numbers`, `summary` |
| R11 질문은 추천이 바뀌는 것만 3개까지 | 아니오 | 4개 이상, 코드로 고르게 함 | 대화, `open_questions` |
| R12 확인 기록은 실제로 확인한 것만 | 아니오 | 고르지 않은 단계를 `keep`으로 적음 | 확인 파일 |
| R13 자료 성격을 적는다 | 아니오 | 가상 입력인데 `scenario`가 없음 | `scenario` |
