# T-024 역할 배분 기준: 인간·AI·소프트웨어

- 지시: juhan(2026-11-10 07:39, 프로젝트 채팅) "이걸 스코어카드 판단기준에 적용해". 결정은 ADR 0018.
- 상태: 스펙+구현 한 PR(ADR 0010 B-12). 결정은 추천안 채택(ADR 0005 결정 3). **범위는 juhan 확인(2026-11-10 07:41, 결정 카드 "스킬과 스코어카드에만 적용")**.
- 바탕: main `45e0180`.

## 목표

스킬 `/scorecard`가 단계마다 판단·실행·책임을 나눠 인간·AI·소프트웨어의 역할을 배분하고, AI 작업을 줄일 도구와 역할 전환 조건을 제안한다. 스코어카드는 그 메모를 업무 이해·단계별 세 역할·개선 제안·역할 전환 조건으로 보인다. 분류 트리·엔진·`examples/**/expected/**`·oracle은 바꾸지 않는다.

## 수용 기준

1. 답 파일 선택 키(단계 `ai.roles`·`judgment`·`execution`·`accountability`·`basis`·`varies`, 최상위 `purpose`·`done_when`·`facts`·`assumptions`·`tools`·`transitions`)를 Core가 받는다. 형식·오류는 case-schema.md 4.16과 데이터 4.16 장면 표.
2. classification.json schema 2 그대로. 역할 배분은 적은 단계에만 `gate_note` 뒤에 쓴다. 분류 결과는 같다. `examples/**/expected/**` 변경 0.
3. scorecard.json schema 2 그대로. 메모가 있을 때만 최상위·단계 `allocation`. 기존 골든 7종 바이트 그대로.
4. HTML: 메모가 있을 때만 업무 이해, 단계별 세 역할과 판단·실행·책임·배분 근거, 개선 제안 카드, 검증 방법과 역할 전환 조건. 코드는 펼쳐 보기 안. 점수·절감률 없음. 390·900·1236px 가로 스크롤 없음.
5. `SKILL.md`가 지시 1~6절을 따른다. `skill-output-rules.md` R14~R21(200줄 이내)과 데이터 채점 표. 수용 규칙은 R1~R5 그대로(ADR 0018 결정 5).
6. 합성 표본 `ai-roles`(가상 시나리오, 메일 답변 업무)와 리포트 골든.
7. ADR 0018, STATUS 원칙 변경 행(`.claude/` 변경)과 잠정 결정 행, LATER.
8. 삭제·Skip 없음, 테스트 수 2336 이상. oracle 전부 MATCH. `dotnet format` 통과. 지시문 2절 8·10 그대로.

## 폴더 범위

`src/Scorecard.Core/Cases/`, `src/Scorecard.Core/Classification/ClassificationJson.cs`, `src/Scorecard.Core/Report/`, `src/Scorecard.Cli/ScorecardHtml.cs`, `tests/Scorecard.Tests/{CaseSchema,Classification,Report,Structure}/`, `tests/Scorecard.Tests/TestData/{classification,report}/ai-roles/`, `docs/spec/{case-schema,classification,report,skill-output-rules}.md`, `docs/spec/data/` 같은 이름 파일, `docs/decisions/0018-*`, `docs/tasks/`(이 카드), `docs/STATUS.md`, `docs/LATER.md`, `.claude/skills/scorecard/SKILL.md`.
바꾸지 않음: 분류 트리(`StepClassifier`·`ClassificationRules`), `src/Scorecard.Core/{Comparison,Judgment,Build}/`, `tools/`, `.github/`, `CLAUDE.md`, 지시문, `examples/**`.

## 바꾼 기존 테스트

- `CaseApiShapeTests`: 공개 형식 목록에 `CaseAllocation`, `RoleTool`, `RoleTransition`, `StepAllocation`, `StepRoles`를 더했다. 더함, 축소 아님.
- `ReportInput`(도우미): 표본 목록에 `ai-roles`를 더했다. 골든·구역·코드 없음 등 골든 이론이 표본 하나 늘어 돈다.
- `ScorecardOpenTests.Open_is_deterministic_across_paths_and_cultures`: `InlineData(ai-roles)` 하나를 더했다.
- 데이터 report.md 4.1 표에 `ai-roles` 행을 더했다(`Tentative_table_matches_golden`이 표본 수와 행 수를 맞춘다). 기존 행 그대로.

## 새 테스트

- `CaseSchema/CaseRoleFieldTests`(장면 표 26행, 코드 덮기, 블록 값 그대로, 분류·답 불변, 표본 읽기·세 역할을 다 채운 단계 하나).
- `Classification/AiRoleTests`(표 순서로 `gate_note` 뒤, 역할 배분을 지워도 분류 바이트 같음, 역할 배분만 적은 단계).
- `Report/RoleAllocationTests`(있을 때만 키, 답 파일 값 그대로, 역할 구역 id는 `ai-roles`에만, 화면 순서·칸 이름·"역할 없음", 메모를 지우면 역할 키·구역·CSS 없음).
- `Structure/SkillRoleCriteriaTests`(새 키, 네 부분 출력·표 머리 행·다섯 질문, 표본 self_check 이름).

## 채점

- 이 PR 밖. 다음 수용 채점(리드, 새 reviewer)에서 R1~R5는 통과/실패, R14~R21은 지적으로 적는다(ADR 0018 결정 5).
