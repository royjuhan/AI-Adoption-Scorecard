# T-014 문장 결과 검증 (빈 곳 2)

- 마일스톤: 빈 곳 카드(`docs/cases-public/README.md` 2절 2, 4절 3항 순서 첫째)
- 상태: 스펙 채택(ADR 0005 결정 3, text-check.md 질문 1~12, ADR 0009). PR (a) 스펙·oracle

## 목표

정답 표가 없는 문장 결과(요약, 답변 초안, 보고서 문장)를 엔진이 AI 없이 검증한다. 두 길이다. (1) 필수 항목 체크리스트 대조: 결과 문장마다 꼭 들어가야 할 항목이 있는지 사람이 정한 규칙으로 본다. (2) 사람 블라인드 점수표 CSV 집계: 사람이 AI 결과와 사람 결과를 누가 썼는지 모르고 매긴 점수를 모아 비교한다. 엔진은 집계만 한다.

## 수용 기준

1. `case.yaml`에 두 길을 적는 자리가 있다(스펙이 정한다). 지금 mixed-exceptions처럼 정답 표가 없는 케이스가 "미검증"이 아니라 이 길로 잴 수 있다.
2. 체크리스트 대조: 항목·통과 기준·결과(건별 빠진 항목)가 정해지고, 판정(지시문 3.4)에 들어갈 셈이 있다. 엔진에 AI·자연어 처리가 없다(문자열 규칙만, CLAUDE.md 절대 원칙).
3. 점수표 집계: CSV 모양(채점자, 건, 출처 가림 키, 점수), 집계(평균·분포, AI 대 사람), 판정 연결이 정해진다. 출처 가림이 실제로 지켜졌는지 엔진이 볼 수 있는 것만 본다.
4. 합성 예제를 `examples/`에 하나 더한다(스펙이 정한다. 기존 mixed-exceptions를 바꿀지 새 예제를 둘지). 예제 expected는 스펙 블록에서 옮긴다(ADR 0004).
5. 출력 파일의 `schema_version`이 바뀌면 올리고 migration 커밋을 따로 둔다(4절 5항).
6. 단계 질문을 더하면 12개 상한 안이다(4절 4항).
7. oracle이 새 지표·판정을 낸다. oracle 변경과 새 expected는 PR을 나눈다(`oracle-and-expected`).
8. 시작 전 main 테스트 수 이상, 삭제·Skip 없음. CI 7개 녹색, main 도구 lead_check 종료 0.

## 폴더 범위

PR (a): `docs/spec/`, `tools/oracle/`, `spikes/`, `docs/STATUS.md`, `docs/decisions/`(필요하면), 이 카드. PR (b): `docs/spec/`, `src/Scorecard.Core/`, `tests/`, `examples/`, `docs/STATUS.md`, 이 카드.

## 메모

- 근거: `docs/cases-public/README.md` 1절의 빈 곳 2 사례, `docs/cases-public/diff.md`, `docs/cases-public/gaps.md`(16 표본 크기, 17 기준 신뢰도가 점수표와 닿는다).
- 회사 이름은 `docs/cases-public/README.md`에만. 예제·테스트·스펙에는 넣지 않는다.
