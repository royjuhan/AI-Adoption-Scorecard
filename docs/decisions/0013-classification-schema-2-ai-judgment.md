# 0013. classification.json schema 2(AI 판단 + 규칙 의견), M2-2 두 PR, oracle 분류 판 고르기

- 날짜: 2026-11-08
- 상태: 채택(리드, ADR 0005 결정 3에 따라 추천안 채택). M2-2(T-019) PR (a).
- 작성: prototyper 초안(`docs/spec/classification.md` 9절, `docs/spec/case-schema.md` 4.14를 바탕으로). 리드가 채택한다.
- 관련: ADR 0004 expected 변경 규칙, ADR 0005 결정 3·9, ADR 0007(두 PR, 판 고르기), ADR 0008 결정 1(schema_version 기준), ADR 0010 A-2·B-10·B-12, STATUS 잠정 결정 T-017 sweeper 행(카드 5를 M2-2로), `docs/LATER.md` #20 행(엔진 전 사용자 확인, ②M 정의), `docs/tasks/T-019-m2-2-scorecard-skill.md`

## 배경

ADR 0010 A-2(juhan): AI가 단계마다 ①~④(②M 포함) 판단과 근거를 쓰고, 규칙 트리 결과는 "규칙 의견"으로 같이 적는다. 다르면 스코어카드에 표시한다. classification.json 단계마다 `source: ai | rule`, `rule_opinion`을 더하고 `schema_version`을 올린다(expected migration). sweeper 카드 5(트리 전용 할 일 키 `confirm_existing_feature`를 출력에서 뺀다)도 이 카드로 왔다.

classification.json이 바뀌면 oracle(`tools/oracle/classify.py`)과 expected가 같이 바뀐다. `oracle-and-expected` 규칙 때문에 한 PR에 둘 수 없다. ADR 0010 B-12는 카드마다 PR 하나지만, 같은 ADR이 이 충돌은 M2-2에서 정하라고 적었다.

## 결정

1. **입력.** `answers.yaml`(·`case.yaml`) 단계 항목에 선택 키 `ai: {assignee, reason}`을 둔다. 담당은 `①` `②` `②M` `③` `④`, 근거는 비지 않은 문자열이다. 파서 규칙은 `case-schema.md` 4.14. `②M`은 학습 모델(결정론): 미리 학습해 고정한 모델을 규칙 SW처럼 돌리고 실행 때 AI를 부르지 않는다. ②의 한 갈래라 규칙 의견과 비교할 때 ②로 본다.
2. **출력(schema 2).** 단계마다 `source`(`ai`·`rule`), `rule_opinion`(트리 `step` 행의 `assignee`·`rule`), `differs`(AI 담당의 비교 값 ≠ 규칙 의견 담당)를 `name` 뒤에 둔다. `source`가 `ai`면 `step` 행만 AI 행(`rule` `AI`, 근거 그대로, 표시 없음)으로 바꾼다. 부가 행(`PARTIAL`·`GATE`)은 트리 것을 그대로 둔다. 게이트는 "항상" 규칙이라 AI 판단으로 빠지지 않는다. 규칙 의견이 `SIGNOFF`여도 AI 담당의 비교 값이 ④가 아니면 `GATE` 조건일 때 트리 `GATE` 행을 붙인다(`classification.md` 9.3 규칙 5). `SIGNOFF` 뒤 게이트를 빼는 근거(단계 자체가 ④)가 없어지기 때문이다.
3. **확인 항목.** `confirm_existing_feature`를 뺀다. `measure_minutes_per_item`은 측정 권고라 남긴다. AI 과잉 후보는 `rows[0]` 담당의 비교 값이 ②일 때(②M 포함)다.
4. **schema_version 1 → 2.** ADR 0008 결정 1의 "받던 입력에서 값이 빠지거나 뜻이 바뀜"이다(예제 다섯의 `checks`, 단계 속성 셋). juhan 지시도 올림이다. expected는 ADR 0004 expected 변경 규칙을 따른다. 바뀌는 줄 수는 `classification.md` 데이터 9.7 표다.
5. **두 PR(ADR 0007 방식).** PR (a): 스펙(`classification.md` 9절, `case-schema.md` 4.14), oracle, 이 ADR. PR (b): Core(파서·분류기·JSON), `expected migration:` 커밋, TestData 표본, 테스트, 스킬 `/scorecard`, Cli 골든. ADR 0010 B-12(카드마다 PR 하나)의 예외다.
6. **oracle 분류 판.** `classify.py`는 schema 1(M2-2 전 규칙, 동결)과 schema 2 두 판이다. schema 1 판은 `ai`가 있는 입력을 거절한다. `check.py`는 읽을 수 있는 expected classification.json의 `schema_version`이 모두 1이면 schema 1 판, 모두 2이거나 없으면 schema 2 판으로 재현한다. 섞이면 classification.json을 모두 DIFF로 낸다. CI 계약(인자, 출력 줄, 보고서)은 그대로다. PR (b) 뒤 첫 oracle 변경에서 schema 1 판과 판 고르기를 지운다.
7. **사용자 확인(LATER #20).** 확인은 스킬 대화에서 한다. 스킬이 단계마다 AI 판단과 추정 답을 보여 주고 사용자가 맞음/바꾸기를 고른 뒤에만 `answers.yaml`을 쓰고 `start --answers`를 부른다. 엔진은 묻지 않고 파일의 `ai`를 확인된 판단으로 쓴다.
8. **예제.** 공개 예제 다섯에는 `ai`를 넣지 않는다(모든 단계 `source: rule`). AI 판단은 TestData 표본 `ai-judgment`로 확인한다.

가드를 약하게 하는 변경은 없다. `tools/ci`·workflows·지시문 2절은 바꾸지 않는다. 네트워크 금지 가드(G1)는 이 카드에서 옮기지 않는다. 스킬은 Core 밖이고 Core는 여전히 AI가 필요 없다.

## 영향

- PR (a): 예제 expected 15개 바이트 그대로. `check.py`는 schema 1 판으로 15 MATCH. `selftest.py`가 PR (b) 모의(사본에 schema 2 블록 → 15 MATCH)를 돈다. C# 테스트는 그대로다(9절은 새 머리 행·표시 줄만 쓴다).
- PR (b): `CaseStep.Ai`, `StepClassification`에 `Source`·`RuleOpinion`·`Differs`, `ClassificationJson.SchemaVersion` 2, `ClassificationRules`에서 `confirm_existing_feature`. 바뀌는 기존 테스트(이유는 PR 본문): 3.2 모양·`SchemaVersion` 상수, form-fill 확인 항목 단언(`Instructions_expectations_hold_for_examples`), 4.7 표 행 수. 지시문 3.1 "모든 JSON은 첫 키 `schema_version: 1`", 3.2 `existing_feature` 기본값 칸 "확인 항목으로 남김", 12절 form-fill 행도 리드가 PR (b)에서 고친다(2절 아님).

## 남은 위험

- AI와 규칙이 같은 쪽으로 틀리면 `differs`가 없다(ADR 0010 남은 위험 그대로).
- 부가 행을 트리에서 가져오므로 AI ④ 단계 뒤에도 게이트가 남는다. 사람 확인이 두 번 보일 수 있다(4.3과 같은 선택).
- `②M`이 실제로 결정론인지 엔진은 모른다. 납품물 검사(지시문 2절 8, M2-4)가 본다.
- 판이 둘인 동안 oracle 분류 코드가 갈린다. PR (b) 뒤 지워서 끝낸다. PR (b) 전에 누가 expected를 schema 1로 되돌려도 schema 1 판이 재현한다. 막는 것은 C# 골든·블록 = 파일 테스트와 expected migration 규칙이다.
- 사용자 확인 여부는 파일에 남지 않는다. 확인 없이 쓴 `ai`도 엔진은 받는다.
