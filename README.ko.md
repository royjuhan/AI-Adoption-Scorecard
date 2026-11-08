# AI-Adoption-Scorecard

[English](README.md)

회사의 AI 도입이 정말 효과가 있었는지 검증하고, 업무 중 소프트웨어로 끝낼 수 있는 구간을 찾아 에이전트 팀이 만들어 주는 도구. AI로 만들고, 실행할 때는 AI를 쓰지 않는다.

> 상태: M0(저장소 세팅). 아직 쓸 수 있는 기능 없음.

## 하는 일

1. 업무를 말하면 정해진 질문으로 단계를 나눈다.
2. 단계마다 담당을 근거와 함께 정한다: ① 기존 기능, ② 규칙 기반 소프트웨어, ③ 실행 시 AI, ④ 사람.
3. 이미 AI를 쓰는 업무면 "정확한가 · 줄었나 · AI가 꼭 필요했나"를 숫자로 잰다.
4. 스코어카드(HTML 한 장)를 내고, 예라고 하면 제작 의뢰서 번들을 만든다.

## 설치

미정 (M4: 단일 `scorecard.exe`, win-x64).

## 5분 예제

`dotnet run --project src/Scorecard.Cli -- check examples/table-compare --json`이 예제를 대조하고 판정한다(결과는 `examples/table-compare/out/`).
명령은 `start`, `open`, `check`, `build` 넷이다. 묻지 않는다. `--json`이면 JSON 문서 하나를 낸다. `docs/spec/json-output.md` 참고.

## 사례

미정 (숫자 요약만 올린다. 비공개 자료 없음).

## 라이선스

MIT. [LICENSE](LICENSE) 참고.
