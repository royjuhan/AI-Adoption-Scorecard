# 0016. 첫 실사용 반영: 스킬 판단 깊이와 스코어카드 문구

- 날짜: 2026-11-09
- 상태: 추천안 채택(리드, ADR 0005 결정 3). **juhan 확인(2026-11-09 07:19, 결정 카드 "AI 판단 단계에만 붙이기": 결정 1 그대로)**. 결정 1·2는 뒤집기 쉽게 두었다(대안 절).
- 번호: 지시문은 "ADR 0011로 기록"이라 했지만 0011은 이미 있다(`0011-lead-check-reuse-ci-mutation.md`). 그래서 다음 번호 0016으로 둔다. 내용은 그 지시문 그대로다.
- 관련: 지시문(juhan, 첫 실사용 supplier-carbon-survey 결과 반영), ADR 0004(expected 변경 규칙), ADR 0008 결정 1(schema_version 기준), ADR 0010, ADR 0013, 카드 `docs/tasks/T-022-first-use-skill-depth.md`.
- 지시문 2절 8·10(실제 자료·회사 자료 금지)은 바꾸지 않는다. 바꾸자는 제안도 하지 않는다.

## 배경

- 가상 사례 1건(supplier-carbon-survey)을 스킬에 넣어 보니 분류가 겉핥기였다. 사람이 손으로 메일을 열고 첨부를 옮기는 단계를 ①로 넘겼고, 말하지 않은 현재 방식을 사실처럼 쓰고 그 위에 셈을 냈다. 화면은 코드와 "사람 게이트"가 앞에 있고 결론이 뒤에 있었다.
- 지시문은 기능 추가가 아니라 판단 깊이와 말하는 방식의 수정이다. 엔진(대조·지표·판정)은 그대로다(지시문 9절).

## 결정

1. **스킬 선택 항목을 Core가 받아 옮긴다.** 단계 `ai`에 `actions [{action, who_now, proposed, source}]`, `now`, `change`, `check`, `gate_note`. 케이스 최상위에 `scenario`, `summary`, `stated_numbers`, `self_check`, `open_questions`, `end_state`. 모두 선택이다(case-schema.md 4.15). 분류 트리·대조·판정은 이 값을 보지 않는다.
2. **classification.json은 schema 2 그대로다.** 단계 `actions`·`now`·`change`·`check`·`gate_note`는 그 단계에 값이 있을 때만 `rows` 뒤에 쓴다. 받던 입력에서 속성을 빼거나 뜻을 바꾸지 않으므로 ADR 0008 결정 1의 올림 조건이 아니다. 공개 예제에는 `ai`가 없어(classification.md 7절) `examples/**/expected/**`는 바이트까지 그대로다. expected migration이 없다.
3. **스코어카드 결과 객체(scorecard.json)는 schema 1 → 2.** 최상위 키 순서와 묶음(상태, 결론, 업무 요약, 판단이 갈린 단계·개선 후보 분리, 할 일 묶음)이 바뀌었다. 골든은 tests TestData뿐이라 expected migration이 아니다(report.md 9절).
4. **oracle은 바꾸지 않는다.** oracle은 공개 예제만 돌리고 예제에 새 키가 없다. oracle이 새 키를 읽는 일은 LATER다.
5. **셈 규칙.** 단계가 추정·기본값에 기대면(`inferred`가 있거나 동작 출처가 `stated`가 아니거나, 규칙 단계 문장에 기본값·추정 표시) `tentative`다. `summary.stated`는 아닌 단계만, `summary.tentative`는 그런 단계만 센다. 제안은 `tentative`가 아닌 단계를 먼저 고른다(report.md 12.3). Core `ScorecardBuilder`가 한다.
6. **HTML.** 상태 네 줄, 결론 세 줄, 네 칸 단계 표, "먼저 확인할 것", "담당자 확인", 할 일 세 묶음, 가상 시나리오 줄. 코드·근거 키·파일·해시·schema는 펼쳐 보기 안(report.md 12절, 데이터 6절).
7. **스킬.** `SKILL.md`를 동작 단위 판단·출처·자기 검사·질문 3개·확인 파일(`confirmed_in: conversation`)·`open`까지로 다시 쓴다. 자기 검사 규칙은 `docs/spec/skill-output-rules.md`(R1~R13). 확인 파일은 최상위 `confirmed_in`(선택, 값 `conversation`)을 받는다.
8. **수용.** 공개 사례 5건(1, 20, 22, 31, 48)을 스킬에 넣고 새 reviewer가 R1~R5로 채점한다(지시문 8.2). 리드가 돌린다. `docs/cases-public/review-01.md`는 juhan이 입력을 주면 리드가 더한다(지시문 8.3).

## 대안 (juhan이 고르면 바꾼다)

- **A. 동작 목록을 늘 쓴다 → classification.json schema 3.** AI 판단 단계가 아니어도 `actions: []`를 쓰고, `self_check`를 classification.json에 둔다. 지시문 9절 끝 문장("필드가 추가되면 schema_version과 expected migration 규칙을 따른다")을 그대로 읽은 길이다. 대가: 예제 5종 `expected/classification.json` 전부 바뀜(ADR 0004 expected 변경 규칙), oracle `classify.py` schema 3 판, 판 둘 공존. 이 카드는 고르지 않았다. 지금 예제에 동작 목록이 없어 빈 배열만 늘고 얻는 것이 없기 때문이다.
- **B. 스킬 메모를 별도 파일(`notes.yaml`)로.** answers.yaml을 건드리지 않는다. 대가: 입력 파일이 하나 늘고 단계 이름으로 다시 맞춰야 한다. 고르지 않았다.
- 결정 1·2를 A로 바꿔도 Core 모델(`StepAiDetail`, `CaseNotes`)과 스코어카드 쪽은 그대로 쓴다. 바뀌는 곳은 `ClassificationJson`의 쓰기 조건과 expected·oracle이다.

## 남은 위험

- 스킬 출력은 AI라 골든 테스트가 안 된다. 테스트는 Core가 받아 옮기는 모양과 스코어카드 셈·문구만 본다. 판단 깊이는 8번 채점으로만 본다.
- 트리 근거 문장 안의 값 코드(예: "(partial)")는 expected 바이트라 화면에 남는다(LATER).
- 의뢰서 번들(`build`)은 아직 "사람 게이트" 문구를 쓴다(LATER).
