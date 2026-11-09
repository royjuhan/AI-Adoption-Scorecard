# T-022 첫 실사용 반영: 스킬 판단 깊이와 스코어카드 문구

- 마일스톤: M2 뒤 고침(M2-2 스킬, M2-3 HTML에 바로 적용). 지시문: juhan 첫 실사용(supplier-carbon-survey) 결과 반영.
- 상태: 스펙+구현 한 PR(ADR 0010 B-12). 결정은 추천안 채택(ADR 0005 결정 3), ADR 0016, STATUS 잠정 결정 T-022 행. **juhan 확인(2026-11-09 07:19, 결정 카드 "AI 판단 단계에만 붙이기": 결정 1 그대로)**.
- 바탕: main `dd5781a`.

## 목표

스킬 `/scorecard`가 단계마다 사람 손이 가는 동작을 적고, 말하지 않은 것을 사실로 쓰지 않고, 결론부터 말하게 한다. 스코어카드는 상태·결론을 먼저 보이고, 추정에 기댄 셈을 따로 세고, 코드·해시를 펼쳐 보기 안에 둔다. 엔진(분류 트리·대조·판정)과 `examples/**/expected/**`는 바꾸지 않는다.

## 수용 기준

1. 스킬 선택 항목(단계 `ai.actions`·`now`·`change`·`check`·`gate_note`, 최상위 `scenario`·`summary`·`stated_numbers`·`self_check`·`open_questions`·`end_state`)을 Core가 받는다. 형식·오류는 case-schema.md 4.15와 데이터 4.15 장면 표.
2. classification.json schema 2 그대로. 선택 항목은 그 단계에 있을 때만 `rows` 뒤에 쓴다. 분류 결과는 같다. `examples/**/expected/**` 변경 0.
3. scorecard.json schema 2: 상태(`status`), 결론(`conclusion`), 업무 요약, 말한 것만·확인하면 바뀔 수 있음 셈, 단계 네 칸·가정·동작, 담당자 확인 문장(`gates[].text`), 판단이 갈린 단계·개선 후보·남는 위험 분리, 할 일 묶음. 표는 데이터 report.md 3·4·6절.
4. 확인 파일 `confirmed_in: conversation`(선택). HTML 단계 상태 "대화에서 확인됨". 다른 값은 `enum` 오류.
5. HTML: 상태 네 줄, 결론 세 줄, 네 칸 단계 표, 먼저 확인할 것(효과 문장), 담당자 확인("사람 게이트" 없음), 할 일 세 묶음과 담당자 안내, 비교 자료 문구, 입력된 시간, 가상 시나리오 줄. 화면 글에 담당 코드·근거 키·해시·schema 없음. 390·900·1236px 가로 스크롤 없음.
6. `docs/spec/skill-output-rules.md`(R1~R13, 200줄 이내)와 데이터 채점 표. `SKILL.md`가 규칙을 가리키고 따른다.
7. ADR 0016(0011은 이미 있음), STATUS 원칙 변경 행(`.claude/` 변경)과 잠정 결정 행.
8. 수용 채점(지시문 8.2): 공개 사례 5건(case-01, case-20, case-22, case-31, case-48)을 스킬에 넣고 카드를 본 적 없는 새 reviewer가 R1~R5로 채점한다. 다섯 모두 통과. **리드가 돌린다(이 PR 밖).** 결과를 이 카드 "채점" 절에 적는다.
9. `docs/cases-public/review-01.md`(지시문 8.3, 가상 시나리오 표시). **대기**: juhan이 입력·독립 검토를 주면 리드가 더한다.
10. 삭제·Skip 없음, 테스트 수 2246 이상. oracle 전부 MATCH. `dotnet format` 통과. 지시문 2절 8·10 그대로.

## 폴더 범위

`src/Scorecard.Core/Cases/`, `src/Scorecard.Core/Classification/`, `src/Scorecard.Core/Report/`, `src/Scorecard.Cli/ScorecardHtml.cs`, `tests/Scorecard.Tests/{CaseSchema,Classification,Report,Structure}/`, `tests/Scorecard.Tests/TestData/{classification,report}/`, `docs/spec/{case-schema,classification,report,skill-output-rules}.md`, `docs/spec/data/` 같은 이름 파일, `docs/decisions/0016-*`, `docs/tasks/`(이 카드), `docs/STATUS.md`, `docs/LATER.md`, `.claude/skills/scorecard/SKILL.md`.
바꾸지 않음: `tools/oracle/`, `tools/ci/`, `.github/`, `CLAUDE.md`, 지시문, `examples/**`, `src/Scorecard.Core/Build/`.

## 바꾼 기존 테스트

- `ScorecardOpenTests.Open_result_object_starts_like_the_command_result_object`: `schema_version` 기대값 1 → 2(ADR 0016 결정 3). 앞 세 키·`command`·`files` 단언은 그대로.
- `ScorecardOpenTests.Property_tables_match_scorecard_json`: 코드 그대로. 데이터 3절 속성 표 두 개가 새 키 순서로 바뀌었다.
- `ScorecardOpenTests.Open_writes_golden_json_and_html`: 코드 그대로. 골든 7종을 다시 썼다(`SCORECARD_UPDATE_GOLDEN=1`, diff 확인). 표본 `ai-actions`가 늘었다.
- `ScorecardOpenTests.Ai_judgment_differs_and_review_are_shown`·`Default_and_inferred_marks_come_from_files`: 코드 그대로. 펼쳐 보기 안의 같은 문구를 본다.
- `CaseApiShapeTests`: 공개 형식 목록에 `CaseNotes`, `CaseTaskSummary`, `SelfCheckItem`, `StepAction`, `StepAiDetail`을 더했다. 더함, 축소 아님.
- `ReportInput`(도우미): 표본 하나(`ai-judgment`)를 표본 목록(`ai-judgment`, `ai-actions`)으로 넓혔다. 표본 폴더 파일을 모두 복사한다.
- 데이터 report.md 5절 오류 표에 `confirmed_in` 행을 더했다. 기존 행 그대로.
- 채점 1회 뒤: 데이터 report.md 4.1 표의 `ai-actions` 행을 `2 | 0 | 2 | 1 | 예` → `2 | 1 | 1 | 2 | 아니오`로 고쳤다(AI 단계 `tentative` 정의 변경, report.md 12.3). 골든 `ai-actions` 두 파일을 다시 썼다(diff: 셈·제안 단계·결론 두 줄·추정 표시 하나만). `Tentative_table_matches_golden` 코드는 그대로.

## 새 테스트

- `CaseSchema/CaseSkillFieldTests`(장면 표, 블록, 표본), `Classification/AiDetailTests`(쓰는 순서, 분류 불변, 없으면 키 없음), `Report/SkillOutputTests`(셈 표, 합, 추정 이동, 상태·결론, 대화 확인, 담당자 확인 문장, 할 일 묶음, 모두 확인, 가상 줄, 화면 코드 없음, 입력 시간, 문턱 숨김), `Structure/SkillOutputRulesTests`(규칙 번호·이름, 수용 규칙, 스킬 본문, 표본 self_check).
- 채점 1회 뒤: `Report/SkillOutputTests`에 추정·기본값 담당자 확인은 유지할 것이 아님(2개), AI 단계 tentative 새 정의, answers 상태 ≠ used일 때 tentative.

## 채점 (지시문 8.2)

- 대기. 리드가 새 reviewer로 돌린 뒤 사례마다 R1~R5 통과/실패를 적는다.
- 1회(새 reviewer): 5건 모두 실패. R5 ×5, R2 ×3(01, 22, 31), R1 ×2(01, 22). 고친 것: `end_state`는 `check` 없이 끝나면 늘 적고 마지막 줄(질문은 그 앞), 동작 `stated`는 말 그대로만(붙인 방법은 `inferred`로 나눔), ③ "묶이는지 확인되지 않았다" 문장, 자기 검사에 질문·`open_questions`와 단정 문장 맞대기, 고른 단계가 없으면 확인 파일 없이 `open`, `gate_note`가 보이는 조건과 보내기 순서. Core: 추정·기본값 표시가 붙은 담당자 확인은 `keep`이 아니라 `undecided`, AI 단계 `tentative`는 추정 답 또는 담당을 받치는 말한 동작이 없을 때(answers ≠ used면 stated 아닌 동작이 있을 때). R5 본문을 채점 표에 맞췄다.
- 2회(다른 새 reviewer): case-01·20·22·31 통과, case-48 R1 실패(서명 단계를 물었는데 답 없이 `human_signoff: yes`·`no`를 `inferred` 없이 적음). 고친 것: SKILL 3절 3번(답이 없으면 적지 않거나 짐작이면 모든 단계 `inferred`), 4절 6번 맞대기에 답 키. case-48만 다시 돌려 서명 단계 짐작을 `inferred`로 적고 리드가 R1을 확인했다(아래 3회).
- 3회(리드, case-48만): 세 단계 모두 `inferred`에 `human_signoff`, 3단계 근거에 "최종 서명 단계인지는 확인되지 않았다". R1 통과로 본다. 새 reviewer 채점이 아니라 리드 확인이다. 다섯 건 R1~R5 통과.
