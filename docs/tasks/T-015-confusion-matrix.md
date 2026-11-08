# T-015 범주 필드 혼동행렬 (빈 곳 6)

- 마일스톤: 빈 곳 카드(`docs/cases-public/README.md` 2절 6, 4절 3항 순서 둘째)
- 상태: 스펙 채택(ADR 0005 결정 3, confusion.md 질문 1~11, ADR 0010). PR (a) 스펙·oracle

## 목표

행마다 범주 하나를 적는 결과(결함 종류, 문의 유형, 승인·거절)를 범주별로 잰다. 정확도 하나로는 놓침(결함을 정상으로)과 오판(정상을 결함으로)이 섞여, 정상이 대부분인 자료에서 결함을 놓쳐도 통과한다. 사용자가 `case.yaml`에 범주 필드와 관심 범주를 적으면 엔진이 혼동행렬과 관심 범주별 놓침·오판·재현율·정밀도를 세고, `case.yaml`의 문턱으로 판정에 넣는다. 엔진에 AI가 없다(셈만).

## 수용 기준

1. `case.yaml`에 범주 필드와 관심 범주를 적는 자리가 있다(스펙이 정한다). 없으면 지금과 같다.
2. 혼동행렬 셈: 범주 목록, 칸(기준 범주 × 결과 범주), 관심 범주별 맞힘·놓침·오판·재현율·정밀도가 정해진다. 누락·결과 쪽 중복·추가 행을 어떻게 세는지 정해진다. 출력 자리가 정해진다.
3. 판정 기준 필드는 `case.yaml`이다(문턱, 기본값). 판정(지시문 3.4)에 들어가는 자리와 문장이 정해진다. 기존 판정 문장은 그대로다.
4. 합성 예제를 `examples/`에 하나 더한다(스펙이 정한다). 자료는 합성 생성기로 만들고, expected는 스펙 블록에서 옮긴다(ADR 0004). 블록은 oracle이 재현한다.
5. 출력 파일의 `schema_version`이 바뀌면 올리고 migration 커밋을 따로 둔다(4절 5항).
6. 단계 질문을 더하면 12개 상한 안이다(4절 4항).
7. oracle이 새 지표·판정을 낸다. oracle 변경과 새 expected는 PR을 나눈다(`oracle-and-expected`).
8. 시작 전 main 테스트 수 이상, 삭제·Skip 없음. CI 7개 녹색, main 도구 lead_check 종료 0.

## 폴더 범위

PR (a): `docs/spec/`, `docs/README-instructions.md`(3.4 문단), `docs/decisions/`(ADR 0010), `tools/oracle/`, `spikes/gap-06/`, `docs/STATUS.md`, 이 카드. PR (b): `docs/spec/`(필요하면), `src/Scorecard.Core/`, `tests/`, `examples/defect-labels/`, `docs/STATUS.md`, 이 카드.

## PR 나눔

- PR (a) 스펙·oracle: `docs/spec/confusion.md`(새), `case-schema.md` 12절·`metrics.md` 15절·`judgment.md` 14절(가리키기만), 지시문 3.4 문단, ADR 0010, STATUS 원칙 변경 0010 행·잠정 결정 T-015 행, `tools/oracle/confusion.py`(새)·`oracle_case.py`·`compare.py`·`judge.py`·`selftest.py`·`README.md`, `spikes/gap-06/`. `examples/`와 `src/`·`tests/`는 바꾸지 않는다. 기존 expected 15개는 바이트 그대로다.
- PR (b) 엔진·테스트·예제: core-dev가 파서·대조·판정·범주 생성기(confusion.md 8절), test-engineer가 테스트와 `examples/defect-labels/`(confusion.md 7.3·7.4 블록을 바이트 그대로). `tools/oracle/`은 바꾸지 않는다. 새 expected 셋은 PR (a) 머지 뒤 base oracle이 재현한다(`expected-added`).
- 정리 카드로 미루는 것: `case-schema.md` 4.3·4.5·4.9 표 옮기기(T-014 몫과 같이), 지시문 12절 예제 표 `defect-labels` 행과 제목.

## 잠정 결정 (ADR 0005 결정 3, 리드 채택 2026-11-08)

`docs/spec/confusion.md` 질문 1~11의 추천안을 채택했다(ADR 0010). 한 줄 요지는 STATUS.md 잠정 결정 T-015 행.

1. 자리: `case.yaml` 최상위 `confusion`(`field`, `positive`). 단계 질문 그대로(10개).
2. 범주 필드는 허용 오차 없는 `compare` 필드. 범주는 정규화 뒤 칸 글(서수), 빈 칸도 범주.
3. 관심 범주 `positive` 1개 이상. 재현율·정밀도·판정은 관심 범주만, 행렬은 모든 범주.
4. 누락·결과 쪽 중복은 결과 없음(놓침에 듦), 추가 행은 행렬 밖.
5. 출력: metrics.json `compare.confusion`, judgment.json 문턱 둘·확인 둘. 설정했을 때만. 0이 아닌 칸만. schema_version 그대로(ADR 0008), migration 없음.
6. 문턱 `recall_min`·`precision_min`, 기본값 95·95, 관심 범주 모두에 하나씩.
7. 확인 `RECALL`·`PRECISION`은 다섯(점수표가 있으면 `SCORE`) 뒤. 미달은 순서 3 뒤 보류, `RECALL` 먼저.
8. 관심 범주의 기준 행 없음 → `no_rows`, 순서 7 뒤 조건부 + `add_class_rows`. 결과 0건 정밀도는 걸리지 않는다.
9. 새 예제 `defect-labels`. 기존 예제 그대로.
10. 범주 생성기를 Core에(T-005·T-014 방식). 테스트가 7.2 계획 표로 다시 만들어 블록과 비교.
11. 규칙 자리는 `docs/spec/confusion.md`. 다른 스펙은 "T-015 개정" 절에서 가리킨다.

## 메모

- 근거: `docs/cases-public/README.md` 1절의 빈 곳 6 사례 13건(번호만), `docs/cases-public/gaps.md` 15(집합·순위, 이름이 겹치나 다른 카드), 16(표본 크기).
- 스파이크 `spikes/gap-06/`: 지금 엔진은 `confusion` 없는 사본을 통과시키고(사람이 모두 설명), `defect-labels`는 `unknown-field confusion`으로 막는다(`result.txt`).
- 회사 이름은 `docs/cases-public/README.md`에만. 예제·테스트·스펙·oracle·스파이크에는 넣지 않는다.
