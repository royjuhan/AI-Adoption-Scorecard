# 기여 안내 (Contributing)

골격 문서다. M4에서 채운다.

## 원칙

- 실행 시 네트워크·LLM·외부 프로세스 호출 금지.
- 회사 자료·실제 품번·이메일·경로를 올리지 않는다. `cases/`는 커밋하지 않는다.
- 테스트 없는 기능은 받지 않는다.

## 예제 추가

미정. `examples/<name>/`에 `case.yaml`, `intake.md`, `answers.yaml`, `reference/`, `candidate/`, `expected/`를 둔다. 데이터는 합성 생성기로만 만든다.

## 규칙·지표 추가

미정. `examples/*/expected/`에 기대값을 먼저 추가한다.

## 에이전트 팀 사용법

미정. `CLAUDE.md`와 `.claude/agents/`를 참고한다.
