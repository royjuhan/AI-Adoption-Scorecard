# 0009. 문장 결과 검증 두 길(체크리스트·점수표)과 판정 SCORE

- 날짜: 2026-11-08
- 상태: 결정(리드, ADR 0005 결정 3에 따라 추천안 채택). T-014 PR (a).
- 작성: 리드(prototyper 요약, `docs/spec/text-check.md` 12절을 바탕으로)
- 관련: `docs/cases-public/README.md` 2절 2(juhan 지시), ADR 0004, ADR 0005, ADR 0008, `docs/tasks/T-014-text-result-check.md`

## 배경

정답 표가 없는 문장 결과(요약, 답변 초안)는 지금 "미검증"이다. 빈 곳 2는 엔진에 AI 없이 이것을 재라고 한다.

## 결정

1. `case.yaml` 최상위에 `checklist`(필수 항목 체크리스트)와 `scores`(사람 블라인드 점수표)를 둔다. 단계 질문은 늘지 않는다.
2. 체크리스트는 대조의 한 종류다. 문자열 규칙(서수 부분 문자열, 공백 접기)만 쓴다. 정규식·자연어 처리는 없다.
3. 점수표는 점수 CSV와 출처 CSV 두 파일이다. 엔진은 집계만 한다. 출처 가림이 깨진 것은 판정 보류다.
4. 판정에 확인 `SCORE`를 더한다. 출력 속성은 설정했을 때만 생긴다. 그래서 기존 예제 바이트와 schema_version은 그대로다(ADR 0008 결정 1).
   - PR (a) 리뷰 뒤(text-check.md 질문 13~15): 대조 없이 점수표만 있으면 점수로 판정한다. 그런데 `inputs`를 적고 대조할 열(`compare`·`checklist`)이 없으면 통과가 아니라 조건부(`NOT_VERIFIED` `no_compare_fields`, 점수 보류 뒤) + `add_compare`다. 사용자가 준 기준·결과 자료가 말없이 버려지지 않게 한다. 점수표만은 읽지 않는 문턱(`accuracy_min`·`missing_max`)을 기본값 표시·확인 항목에 넣지 않는다. `score_gap_max`가 0이면 "0점 넘게" 없는 문장을 쓴다.
5. oracle에 `checklist.py`·`scores.py`를 더한다. 새 예제 `reply-drafts`는 PR (b)에서 넣는다(`oracle-and-expected`).

가드를 약하게 하는 변경은 없다.

## 영향 받는 곳

- 지시문(`docs/README-instructions.md`) 3.4: 문장 결과의 두 길, 점수 미달·가림 깨짐 보류, 점수표만 판정, 대조할 열 없음 조건부를 한 문단으로 더했다(PR (a)). T-013에서 미룬 조건부 문장 범위 다듬기(괄호로 묶기)도 같이 넣었다. 2절(8·10 포함)은 바꾸지 않는다.
- 지시문 12절: 예제 표 다섯째 행 `reply-drafts`와 제목 "공개 예제 4종"은 PR (b)에서 고친다. 테스트가 그 표의 행마다 `examples/`를 읽어, 예제가 없는 PR (a)에서 넣으면 실패한다.
- 스펙: `docs/spec/text-check.md`(규칙의 자리). `case-schema.md` 11절, `metrics.md` 14절, `judgment.md` 13절은 가리키기만 한다. 다른 스펙의 표는 PR (b)에서 `case-schema.md`만 옮긴다.
- oracle: `tools/oracle/checklist.py`·`scores.py`(새), `oracle_case.py`·`compare.py`·`judge.py`·`selftest.py`. `check.py` 계약은 그대로다.

## 남은 위험

- 부분 문자열 규칙은 표현이 다른 같은 뜻을 놓친다. 그 판단은 점수표(사람)가 맡는다.
- 점수표의 채점자 수가 적으면 평균 차이가 우연일 수 있다(빈 곳 16·17, 뒤 카드).
