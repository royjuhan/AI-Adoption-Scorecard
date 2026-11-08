# T-014 문장 결과 검증 (빈 곳 2)

- 마일스톤: 빈 곳 카드(`docs/cases-public/README.md` 2절 2, 4절 3항 순서 첫째)
- 상태: 스펙 채택(ADR 0005 결정 3, text-check.md 질문 1~15, ADR 0009). PR (a) 스펙·oracle, 리뷰 지적 반영

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

## 잠정 결정 (ADR 0005 결정 3, 리드 채택 2026-11-08)

`docs/spec/text-check.md` 질문 1~15의 추천안을 채택했다(ADR 0009). 13~15는 PR (a) 리뷰 지적에서 나왔다. 한 줄 요지는 STATUS.md 잠정 결정 T-014 행.

1. 자리: `case.yaml` 최상위 `checklist`·`scores`. 단계 질문 그대로(10개).
2. 체크리스트는 대조의 한 종류. 빠진 항목이 있으면 값 불일치.
3. 문자열 규칙: 서수 부분 문자열, 공백 접기, 대소문자·NFC 그대로. 정규식 없음.
4. 점수표: 점수 CSV(`rater`, `item`, `blind`, `score`) + 출처 CSV(`blind`, `item`, `source`).
5. 사람 자료 결함은 오류(종료 2). 출처 드러남은 판정 보류.
6. 가림 확인 규칙 셋. 키 순서는 보지 않는다.
7. 평균은 소수 둘째 자리 정수 셈, 판정은 곱셈 비교, `score_gap_max` 기본 0.
8. 확인 `SCORE` 여섯째. 점수 미달·가림 깨짐은 순서 4 뒤 보류. 점수표만 있어도 미검증 아님.
9. 출력은 설정했을 때만 생기는 속성. schema_version 그대로(ADR 0008).
10. 새 예제 `reply-drafts`. mixed-exceptions 그대로.
11. 문장 생성기를 Core에(T-005 방식).
12. 규칙 자리는 `docs/spec/text-check.md`. 다른 스펙은 "T-014 개정" 절에서 가리킨다.
13. 점수표만인데 `inputs`가 있고 대조할 열이 없으면 통과가 아니라 조건부 `NOT_VERIFIED`(`no_compare_fields`, 점수 보류 뒤·`TIME` 앞) + `add_compare`.
14. 점수표만이 읽지 않는 문턱(`accuracy_min`·`missing_max`)은 `defaults`·`confirm_thresholds`·표시에서 뺀다. `thresholds` 모양은 그대로.
15. `score_gap_max` 값이 0이면 "넘게" 없는 문장(5.2 문턱 0 문장 표).

## PR (a) 리뷰 지적 (#17)

- 막는 지적 2(점수표만 길이 `inputs` 자료를 말없이 버림): 잠정 결정 13. 스펙 5.3·5.4·5.5, 6.1 장면 넷, oracle `judge.py`, selftest. reply-drafts 7.3·7.4 블록과 예제 4종 expected 12개는 바이트 그대로.
- a. 4.3 규칙 2: 점수는 "따옴표를 풀고 정규화한 칸 값이 정수 글"(CSV 변형 점검과 맞춤).
- b. 잠정 결정 14. h. 잠정 결정 15. 6.1 "점수 문턱 기본값" 판정 문장이 바뀌었다(블록 밖).
- c. 지시문 3.4에 문장 결과 문단, T-013에서 미룬 조건부 괄호 묶기. 12절 예제 표는 PR (b)(테스트가 그 표를 읽는다). ADR 0009 "영향 받는 곳", STATUS 원칙 변경 0009 행.
- d. 스펙 상태 줄 "초안" → "채택(ADR 0005 결정 3, ADR 0009)": text-check.md, case-schema.md 11절, judgment.md 13절, metrics.md 14절.
- f. `spikes/T-014`는 머지 대상이 아니다. 스펙 머리 글·10절을 "selftest가 7.2 계획 표로 CSV를 다시 만들어 블록과 맞춘다"로.
- g. `spikes/gap-19`의 처리량을 README 3절 수치에서 둥근 합성 값(분당 1만 건, 하루 1,440만 건)으로. 생성 스크립트·결과·gaps.md.

## 메모

- 근거: `docs/cases-public/README.md` 1절의 빈 곳 2 사례, `docs/cases-public/diff.md`, `docs/cases-public/gaps.md`(16 표본 크기, 17 기준 신뢰도가 점수표와 닿는다).
- 회사 이름은 `docs/cases-public/README.md`에만. 예제·테스트·스펙에는 넣지 않는다.
