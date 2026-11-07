# T-012 판정 보강 (추가 행·결과 쪽 중복·안 본 불일치·몰림 열)

- 마일스톤: M1 끝 [재검토] 결과(2026-11-07, STATUS.md 재검토 기록)
- 상태: 스펙 작성 중

## 목표

M1 [재검토]의 막는 지적 1·2를 고친다. 결과 자료(AI 결과)의 대표 실패인 지어낸 행과 키 중복이 판정에 닿게 한다. 사람이 아직 안 본 불일치가 있으면 통과로 내지 않는다(지시문 3.4 "보류 = 설명 안 되는 불일치"). 몰림은 `case.yaml`에 적은 열로만 본다.

## 수용 기준

1. 추가 행(`counts.extra`)이 판정에 쓰인다. 사람 검토 대상(설명 안 됨 목록)에 추가 행 키를 넣을 수 있다. 지금은 `ArgumentException`이다(JudgmentEngine.cs:44, :61).
2. 결과 쪽 키 중복을 입력 오류(종료 2)가 아니라 항목 결함으로 센다. 기준 쪽 중복은 지금처럼 오류다. 스펙이 정한다.
3. 사람이 아직 안 본 불일치·추가 행이 있으면 통과가 아니다. 판정 순서는 스펙이 정한다(재검토 권고: FOCUS 뒤 조건부).
4. 몰림은 `case.yaml`의 새 키(재검토 권고: `inputs.group`)에 적은 열로만 본다. 안 적으면 몰림을 보지 않는다(M-A). 예제 `case.yaml` 중 몰림이 필요한 것에 한 줄을 더한다.
5. 예제 4종의 판정 값(조건부·통과·미검증·보류)은 그대로다. `expected/judgment.json`의 문장이 바뀌면 ADR 0004 expected 변경 규칙과 ADR 0005로 migration한다. `schema_version`을 올릴지 스펙이 정한다.
6. 탐침 재현: 기준 20행 + 지어낸 30행이 통과가 아니다. 40행 중 39행이 빈 메모 열이 몰림으로 잡히지 않는다. 둘 다 테스트가 있다.
7. 시작 전 main 통과 수는 1558이다. 끝난 뒤 그 이상이고, 삭제·Skip이 없다. 고치는 기존 테스트는 PR 본문에 이유를 적는다.
8. oracle(`tools/oracle/`, prototyper만)이 새 규칙도 낸다. `oracle-and-expected` 규칙 때문에 PR을 나눈다: (a) 스펙 + oracle, (b) 엔진 + 예제 `case.yaml` + expected migration.
9. CI 잡 7개 녹색, main 도구 lead_check 종료 0.

## 폴더 범위

PR (a): `docs/spec/`, `tools/oracle/`, `docs/STATUS.md`, 이 카드. PR (b): `docs/spec/`, `src/Scorecard.Core/`, `tests/`, `examples/*/case.yaml`, `examples/*/expected/`, `docs/STATUS.md`, 이 카드.

## 메모

- 재검토 근거: metrics.md:308("추가 행을 판정에 쓸지는 판정 카드가 정한다")을 judgment.md:17이 질문 없이 뺐다. M-B 위험은 judgment.md:281에 "미확인"으로만 있었다.
- 추천안을 고르는 기준: 사용자에게 맞나 → 그다음 바이트 보존(재검토 지적 8).
- 같이 고칠 것(작은 것): 지시문 5절 expected 목록에 judgment.json(민감 경로라 ADR 필요. 정리 PR로 미룰 수 있다), T-011 카드 상태 줄.
- 미룬 것: CsvText 따옴표 안 CRLF 뒤 줄 번호 테스트, 엔진-oracle 시드 차분 CI, 사용자에게 보일 확인 문장 고르기(M2-3).
