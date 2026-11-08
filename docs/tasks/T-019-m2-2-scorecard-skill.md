# T-019 M2-2 스킬 `/scorecard`와 분류 schema 2 (AI 판단 + 규칙 의견)

- 마일스톤: M2-2(ADR 0010 B-12)
- 상태: 두 PR(ADR 0013 결정 5, ADR 0007 방식). 결정은 추천안 채택(ADR 0005 결정 3), STATUS 잠정 결정 T-019 행
- 출발점: ADR 0010 A-2(AI 판단 + 규칙 의견, `source`·`rule_opinion`, schema_version 올림), STATUS 잠정 결정 T-017 sweeper 행(카드 5 `confirm_existing_feature`), `docs/LATER.md` #20 행(사용자 확인, ②M 정의)

## 목표

스킬 `/scorecard`가 사용자 업무를 단계로 나누고 단계마다 ①~④(②M 포함) 판단과 근거를 쓴다. 엔진은 그 판단을 받아 classification.json에 적고, 트리 결과를 규칙 의견으로 같이 적는다. 다르면 표시한다(`differs`). 숫자·판정은 그대로 Core가 낸다.

## 왜 두 PR인가

classification.json이 바뀌면 oracle과 expected가 같이 바뀐다. CI `oracle-and-expected`가 한 PR에서 둘을 막는다. 그래서 PR (a)가 스펙·oracle을, PR (b)가 Core·expected를 바꾼다(ADR 0013 결정 5·6).

## 수용 기준: PR (a) 스펙·oracle·ADR

1. `docs/spec/classification.md` 9절과 `docs/spec/data/classification.md` 9절: 입력 `ai: {assignee, reason}`(②M 포함), 출력 schema 2(`source`, `rule_opinion`, `differs`), 부가 행과 AI 판단의 관계, `confirm_existing_feature` 삭제, 바이트 규칙 그대로, 예제 다섯의 schema 2 블록·SHA-256, TestData 표본 `ai-judgment` 블록, 사용자 확인(B안). 본문 200줄 이내.
2. `docs/spec/case-schema.md`·데이터 4.14: `ai` 키 규칙과 오류 장면 표.
3. `tools/oracle/classify.py`가 스펙만 보고 schema 2를 낸다. `check.py`가 expected `schema_version`으로 판을 고른다. `selftest.py`가 schema 2(`ai`, ②M, `differs`, `ai` 없음, 9절 블록 = oracle 출력, PR (b) 모의)를 본다.
4. ADR 0013(채택), STATUS 원칙 변경 1행·잠정 결정 T-019 행.
5. `python3 -I -B tools/oracle/selftest.py --repo .` 통과(1763 이상), `check.py` 15 MATCH, `dotnet test -c Release` 녹색(시작 전 2084 그대로).
6. `src/`·`tests/`·`examples/`·`tools/ci/`·`.github/`를 바꾸지 않는다. C# 테스트가 읽는 머리 행·블록·SHA 줄은 그대로다.

## 수용 기준: PR (b) Core·expected·스킬

1. Core 파서가 `ai`를 받는다(`case-schema.md` 4.14 표대로. 지금은 `unknown-field`). `CaseStep`(또는 단계 모델)에 `Ai`.
2. `StepClassifier`·`ClassificationJson`이 9절대로 낸다: `Source`·`RuleOpinion`·`Differs`, `ClassificationJson.SchemaVersion` 2, `ClassificationRules`에서 `confirm_existing_feature` 삭제, AI 과잉 후보는 비교 값 ②.
3. `expected migration:` 커밋: `examples/*/expected/classification.json` 다섯을 데이터 9.6 블록 바이트로. 그 커밋에는 expected 파일과 스펙 블록·SHA 줄만(ADR 0004). 줄 수는 데이터 9.7 표와 같다.
4. 본문 9.9대로 블록·표를 제자리(3.2 모양, 4.7 확인 항목, 7.2~7.5 블록·SHA 줄, text-check 데이터 7.4 reply-drafts 블록)로 옮긴다. `tools/oracle/`는 바꾸지 않는다(판 고르기가 schema 2 판을 쓴다).
5. TestData `tests/Scorecard.Tests/TestData/classification/ai-judgment/answers.yaml` = 데이터 9.5 블록. 분류 결과 = 그 블록.
6. 테스트: 9.3 비교 값 표·장면 표, 4.14 장면 표, 표본 골든, 예제 골든(schema 2). 바꾸는 기존 테스트와 이유는 PR 본문에: `ClassificationSpec.SchemaVersion`, 3.2 모양, form-fill 확인 항목 단언(`ExampleGoldenTests`), 4.7 표 행 수.
7. 스킬 `.claude/skills/scorecard/SKILL.md`: 단계 나누기, 질문 10개 체크리스트, 단계마다 `ai` 판단·근거, 추정 답 `inferred`, 사용자 확인(맞음/바꾸기) 뒤에만 `answers.yaml`을 쓰고 `scorecard start --answers`를 부른다. 엔진 숫자·판정을 바꾸지 않는다. 골든 테스트만(ADR 0010 B-10).
8. Cli 골든: `classify --json`·`start` 출력이 schema 2 expected와 같다. Cli 코드는 Core를 부르기만 한다.
9. 지시문: 3.1 "모든 JSON은 첫 키 `schema_version: 1`", 3.2 `existing_feature` 기본값 칸 "확인 항목으로 남김", 12절 form-fill 기대값, ②M 정의를 9.1로 가리킨다(2절은 바꾸지 않는다).
10. 시작 전 main 테스트 수, 끝난 뒤 그 이상. 삭제·Skip 없음. oracle 15 MATCH.
11. PR (b) 머지 뒤 첫 oracle 변경에서 schema 1 판과 판 고르기를 지운다(ADR 0013 결정 6).

## 폴더 범위

- PR (a): `docs/spec/classification.md`, `docs/spec/data/classification.md`, `docs/spec/case-schema.md`, `docs/spec/data/case-schema.md`, `docs/decisions/0013-*.md`, `docs/tasks/`(이 카드), `docs/STATUS.md`, `tools/oracle/`.
- PR (b): `src/Scorecard.Core/`(파서·분류), `src/Scorecard.Cli/`(필요하면), `tests/Scorecard.Tests/`, `examples/*/expected/classification.json`, `docs/spec/`(블록 옮기기), `docs/README-instructions.md`(위 9), `.claude/skills/scorecard/`, `docs/STATUS.md`, `docs/LATER.md`.
