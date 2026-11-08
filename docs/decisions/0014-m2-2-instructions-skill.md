# 0014. M2-2 PR (b): 지시문을 schema 2에 맞추고 스킬 `/scorecard`를 둔다

- 날짜: 2026-11-08
- 상태: 채택(리드, ADR 0005 결정 3). 새 결정은 없다. ADR 0013(juhan 확인 2026-11-08 11:39)을 따라 글을 맞춘다.
- 관련: ADR 0010(스킬 `/scorecard`, M2-2), ADR 0013(classification.json schema 2). `tools/ci/README.md` ci-changed 규칙.

## 배경

M2-2 PR (b)는 ADR 0013을 Core·expected·스킬로 옮긴다. 이때 원칙 파일 `docs/README-instructions.md`와 `.claude/skills/scorecard/SKILL.md`가 바뀐다. ci-changed 규칙은 이런 PR에 새 ADR과 STATUS 원칙 변경 행을 요구한다. ADR 0013은 PR (a)에서 이미 main에 들어갔다.

## 결정

1. 지시문 3.1: JSON 첫 키는 정수 `schema_version`이고 값은 문서마다 정한다(classification.json은 2).
2. 지시문 3.2 표: `existing_feature` 기본값 unknown은 확인 항목이 아니라 기본값 표시로 남는다(`confirm_existing_feature` 삭제, ADR 0013).
3. 지시문 3.3: ②M과 비교 값, AI 서명 단계 게이트는 `classification.md` 4.13을 가리킨다.
4. 지시문 12절 form-fill 행: "확인 항목" → "기본값 표시".
5. 스킬 `.claude/skills/scorecard/SKILL.md`를 더한다. 단계마다 AI 판단과 추정 답을 보이고, 사용자가 맞음/바꾸기를 고른 뒤에만 `answers.yaml`을 쓰고 `start --answers`를 부른다. 숫자·판정은 엔진 JSON 값만 옮긴다.
6. 2절 원칙 8·10은 바이트 그대로다.

## 남은 위험

- 스킬은 골든 테스트(ScorecardSkillTests)만 있다. 대화 품질은 테스트하지 않는다(ADR 0010: 스킬은 골든 테스트만).
