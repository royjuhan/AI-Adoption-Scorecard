# T-006 대조 엔진

- 마일스톤: M1 (지시문 11절)
- 상태: 스펙 승인됨(2026-11-07). 테스트·구현 중

## 목표

`reference/`와 `candidate/`를 키로 맞춰 일치·불일치·누락(·추가)을 낸다. 수치 허용오차와 텍스트 정규화를 적용한다. 결과를 `expected/metrics.json`과 같은 바이트로 낸다.

## 수용 기준

1. 예제 3종의 출력이 `examples/*/expected/metrics.json`과 바이트까지 같다. table-compare 정확도 90.0%·누락률 3.3%, form-fill 100%·0%, mixed-exceptions는 전 항목 "미검증"(지시문 12절).
2. 키 중복, 키 누락(빈 키), 형식 오류(열 없음, 숫자 아님, 인코딩·줄바꿈 변형)에 대한 테스트가 있다.
3. 허용오차 경계(정확히 같은 차이)와 텍스트 정규화 규칙에 단위 테스트가 있다.
4. 결정론·원본 불변(G2·G3)에 대조 대상이 등록된다.
5. dotnet test가 ubuntu·windows CI에서 녹색이다.

## 폴더 범위

`docs/spec/`, `src/Scorecard.Core/`, `tests/`, `examples/`

## 샘플 자료

T-005가 만든 예제 3종의 `case.yaml`·`reference/`·`candidate/`. `expected/metrics.json`은 사람이 확정한 뒤 커밋한다(지시문 14절 8). expected는 상시 승인 범위 밖이다. juhan이 직접 확정한다(2026-11-07 사람 결정). Core 구현과 `expected/metrics.json` 커밋은 T-007 머지 뒤에 한다(T-007은 PR #7로 머지됨).

## 메모

- 대조 용어는 `docs/spec/synthetic-examples.md` 4.7(P2)을 출발점으로 확정한다. 항목 = 기준 행, 정확도 = 일치 / 기준 행, 누락률 = 누락 / 기준 행. 추가 행을 어떻게 셀지 정한다.
- `compare`에 없는 열(예: `signal_type`), 허용 오차가 없는 필드(예: `item`)를 어떻게 비교할지 정한다.
- 판정(통과·조건부·실패)과 "특정 유형에만 몰림"은 판정 카드 몫이다. 이 카드는 대조와 지표만. `metrics.json`에 무엇을 넣을지 스펙에서 정한다(`docs/spec/metrics.md`).
- 숫자 비교는 decimal, InvariantCulture. 퍼센트 반올림 규칙을 정한다.
- T-004·T-005 예제 파일은 바꾸지 않는다.
- metrics.json 첫 키는 `"schema_version": 1`(정수)이다(ADR 0004 결정 2). 처음부터 넣는다.
- 같은 입력이면 `--json` 출력이 바이트까지 같아야 한다(ADR 0004). metrics.json 바이트 규칙이 그 바탕이다.

## 남은 일 (정리 카드로)

reviewer 비차단 지적(2026-11-07):
- CR만 쓰는 파일에서 `encoding` 오류 줄 번호가 1로 나온다(`CsvText.cs`, `CaseParser.cs`도 같은 식). CR도 줄로 세고 테스트를 더한다.
- 메시지 값 64자 자르기가 서로게이트 쌍 가운데서 자를 수 있다(`CompareRules.cs`).
- 기준 빈 칸의 `number` 메시지가 `숫자가 아니다: `로 끝난다. 스펙 개정 때 `(빈 칸)` 표시를 검토한다.
