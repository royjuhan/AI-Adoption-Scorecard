# 0010. 범주 필드 혼동행렬(confusion)과 판정 RECALL·PRECISION

- 날짜: 2026-11-08
- 상태: 결정(리드, ADR 0005 결정 3에 따라 추천안 채택). T-015 PR (a).
- 작성: prototyper 초안(`docs/spec/confusion.md` 12절을 바탕으로), 리드 채택
- 관련: `docs/cases-public/README.md` 2절 6(juhan 지시), ADR 0004, ADR 0005, ADR 0008, ADR 0009, `docs/tasks/T-015-confusion-matrix.md`

## 배경

행마다 범주 하나를 적는 결과(결함 종류, 문의 유형, 승인·거절)는 지금 정확도 하나로만 잰다. 정확도는 놓침(결함을 정상으로)과 오판(정상을 결함으로)을 한 줄로 섞는다. 정상이 대부분인 자료에서 결함을 놓쳐도 정확도가 문턱을 넘어 통과한다. 스파이크 `spikes/gap-06/`에서 지금 엔진은 결함 `찍힘` 4건 중 1건을 놓친 결과를 정확도 92.5%로 통과시켰다(사람이 모두 설명한 경우). 빈 곳 6은 "범주 필드: 오판·놓침·정밀도·재현율. 판정 기준 필드는 case.yaml"이라 한다. 공개 사례 13건이 이 빈 곳을 단다(검증 유형 C).

## 결정

1. `case.yaml` 최상위에 `confusion`(`field`, `positive`)을 둔다. 범주 필드는 허용 오차가 없는 `compare` 필드다. 관심 범주(`positive`)는 1개 이상 적는다. 단계 질문은 늘지 않는다(10개). 새 오류 코드는 없다.
2. 혼동행렬은 대조가 짝지은 기준 행을 범주별로 센 것이다. 범주는 정규화 뒤 칸 글(서수)이다. 누락과 결과 쪽 중복은 결과 없음(놓침에 듦), 추가 행은 행렬 밖이다. 관심 범주마다 맞힘·놓침·오판·재현율·정밀도를 낸다. 퍼센트는 metrics.md 4.8 셈이고 분모가 0이면 null이다.
3. 출력은 설정했을 때만 있는 속성이다: metrics.json `compare.confusion`, judgment.json `thresholds.recall_min`·`precision_min`과 확인 `RECALL`·`PRECISION`. `schema_version`은 metrics.json 1, judgment.json 2 그대로다(ADR 0008 결정 1). 기존 예제 5종 expected 15개는 바이트 그대로다. migration 커밋이 없다.
4. 판정 기준 필드는 `case.yaml` `thresholds.recall_min`·`precision_min`(기본값 95·95, 관심 범주 모두에 하나씩)이다. 관심 범주의 재현율 또는 정밀도가 문턱에 못 미치면 정확도가 문턱을 넘어도 보류다(자리: judgment.md 4.3 순서 3 뒤, `RECALL` 먼저). 관심 범주의 기준 행이 없으면 순서 7 뒤 조건부 + 다음 할 일 `add_class_rows`다. 결과 0건인 관심 범주의 정밀도는 걸리지 않는다. 문턱 비교는 셈(곱셈)이다. 기존 판정 표·문장은 그대로다.
5. oracle에 `confusion.py`를 더하고 `oracle_case.py`·`compare.py`·`judge.py`·`selftest.py`를 넓힌다(confusion.md 글만 보고, `src/`는 읽지 않음). `check.py` 계약은 그대로다.
6. 새 공개 합성 예제 `defect-labels`(결함 종류, 정확도 92.5% 충족 + `찍힘` 재현율 75.0% → 보류 `RECALL`)를 PR (b)에서 넣는다(`oracle-and-expected`). 자료는 범주 생성기(confusion.md 7.2)로 만들고, 생성기는 PR (b)에서 Core에 둔다. 기존 예제는 바꾸지 않는다.

가드를 약하게 하는 변경은 없다. selftest는 점검이 늘기만 한다(1763 → 2110).

## 영향 받는 곳

- 지시문(`docs/README-instructions.md`) 3.4: 범주 결과 문단을 더했다(PR (a)). 표는 그대로다. 2절(8·10 포함)은 바꾸지 않는다.
- 지시문 12절: 예제 표 여섯째 행 `defect-labels`와 제목은 T-014 몫과 같이 정리 카드에서 고친다. 테스트가 그 표의 행마다 `examples/`를 읽는다.
- 스펙: `docs/spec/confusion.md`(규칙의 자리). `case-schema.md` 12절, `metrics.md` 15절, `judgment.md` 14절은 가리키기만 한다. 다른 스펙의 표는 바꾸지 않는다.
- oracle: `tools/oracle/confusion.py`(새), `oracle_case.py`·`compare.py`·`judge.py`·`selftest.py`·`README.md`.
- 스파이크: `spikes/gap-06/`(남긴다, `docs/cases-public/README.md` 4절 2).

## 남은 위험

- 범주 글 흔들림(대소문자, 띄어쓰기, 같은 뜻 다른 이름)은 서수 비교라 다른 범주가 된다. 사람은 `classes` 목록으로 본다. 이름 묶기는 다음 카드(confusion.md 9절 2).
- 문턱은 관심 범주 모두에 하나다. 범주마다 다른 문턱, 놓침·오판 비용 가중은 없다(9절 3).
- 관심 범주의 기준 행이 적으면(4건) 재현율 하나가 크게 흔들린다(빈 곳 16, 9절 4).
- 점수표와 혼동행렬을 같이 쓴 예제·장면이 없다. 판정 행 자리는 스펙 글과 oracle 코드로만 맞췄다(9절 5).
- 결과 쪽 중복을 결과 없음으로 센다. 두 행이 같은 범주여도 맞힘이 아니다(9절 6).
