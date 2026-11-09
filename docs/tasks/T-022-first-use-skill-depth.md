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

## 새 테스트

- `CaseSchema/CaseSkillFieldTests`(장면 표, 블록, 표본), `Classification/AiDetailTests`(쓰는 순서, 분류 불변, 없으면 키 없음), `Report/SkillOutputTests`(셈 표, 합, 추정 이동, 상태·결론, 대화 확인, 담당자 확인 문장, 할 일 묶음, 모두 확인, 가상 줄, 화면 코드 없음, 입력 시간, 문턱 숨김), `Structure/SkillOutputRulesTests`(규칙 번호·이름, 수용 규칙, 스킬 본문, 표본 self_check).

## 채점 (지시문 8.2)

- 대기. 리드가 새 reviewer로 돌린 뒤 사례마다 R1~R5 통과/실패를 적는다.
