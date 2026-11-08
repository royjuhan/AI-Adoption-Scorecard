# 0008. schema_version 기준, 지시문 5절·3.4 문구, oracle 판 하나, CI 설명 문구

- 날짜: 2026-11-08
- 상태: 결정(리드, ADR 0005 결정 3에 따라 추천안 채택). T-013.
- 작성: prototyper 초안(`docs/spec/judgment.md` 12절, `docs/spec/metrics.md` 13절을 바탕으로). 리드가 채택한다.
- 관련: ADR 0004 결정 2·expected 변경 규칙, ADR 0005, ADR 0006, ADR 0007 결정 4, `docs/tasks/T-013-cleanup.md` 수용 기준 1·3·5·7, T-012 카드 "PR (a) 리뷰 지적" 3, T-011 카드 "T-011a 리뷰의 막지 않는 지적"

## 배경

T-012 리뷰가 넷을 정리 카드로 미뤘다.

- metrics.json `items[].status`에 `duplicate`가 늘었는데 `schema_version`은 1이다. 올리지 않은 기준이 글로 없다. T-013은 결과 쪽 중복의 셈을 또 바꾼다(`metrics.md` 13절).
- 지시문 5절 expected 목록에 `judgment.json`이 없다. 3.4 조건부에 "아직 안 본 검토 항목"이 없다(judgment.md는 해석으로 둔다).
- oracle에 판이 둘이다(ADR 0007). T-012b가 머지됐으니 지운다.
- `ci.yml` 313줄과 `tools/ci/README.md` 160줄이 "단위 테스트가 대조한다"라고만 쓴다. 어느 테스트인지 없다.

지시문과 `tools/oracle/`·`.github/`·`tools/ci/`는 민감 경로다(`ci-changed`). 그래서 이 ADR과 STATUS.md 원칙 변경 행을 같이 둔다.

## 결정

1. **schema_version 기준.** JSON 출력의 `schema_version`은 이미 받던 입력에서 속성이 빠지거나, 속성의 이름·형식·뜻이 바뀔 때 올린다. 올리지 않는 경우는 둘이다.
   - 새로 받게 된 입력 모양에서만 출력이 달라질 때. 그 입력 모양이 릴리스(태그) 전에 처음 생겼으면 이 경우다.
   - 열거 값 하나가 늘 때. 읽는 쪽은 모르는 값을 "검토할 것"으로 다룬다.
   - 그래서 metrics.json은 1이다. `duplicate`(T-012b)와 결과 쪽 중복의 셈(T-013)은 T-012b에서 처음 받은 입력(결과 쪽 키 중복, 그전에는 오류)에서만 나온다. 태그·릴리스는 아직 없다. 예제·G2 표본 바이트도 그대로다.
   - judgment.json 2(T-012)는 이 기준으로도 맞다. 받던 입력(예제 4종)에서 할 일 키 이름과 문장이 바뀌었다.
   - 올리면 지금처럼 ADR 0004 expected 변경 규칙을 따른다. 이 기준은 그 규칙을 바꾸지 않는다.
2. **지시문 5절.** expected 목록에 `judgment.json`(판정·근거 문장·다음 할 일)을 `metrics.json` 다음에 넣는다. "이 세 파일"을 "이 파일들"로 바꾼다. judgment.md 4.11 제안 그대로다.
3. **지시문 3.4.** 조건부 끝에 "또는 사람이 아직 안 본 검토 항목(불일치·추가 행·중복 행)이 있음"을 더한다. 보류의 "설명 안 되는 불일치" 뒤에 "(사람이 설명할 수 없다고 적은 검토 항목)"을 더한다. judgment.md 4.3 판정 표(순서 4·7)와 같은 뜻이다. 판정 값·순서는 바뀌지 않는다.
4. **oracle 판 하나.** `tools/oracle/judge_v1.py`와 `check.py`의 판 고르기를 지운다(ADR 0007 결정 4). `check.py`는 judgment.json을 `judge.py` 하나로 만든다. CI 계약(인자, 출력 줄, 보고서)은 그대로다. 옛 판(schema_version 1) expected는 그 파일만 DIFF다(selftest 음성 대조).
5. **CI 설명 문구(ci-dev).** "단위 테스트가 대조한다"를 테스트 이름으로 바꾼다. `tools/ci/test_regression_guard.py`의 `TestGuardJobsWiring.test_workflow_passes_every_needed_job`이다.
   - `.github/workflows/ci.yml` 313줄(regression-guard 잡 주석)
   - `tools/ci/README.md` 160줄(regression-guard "앞 잡의 결과")
   - `tools/ci/lead_check.py` 58줄(`GUARD_JOBS` 주석)
   - 주석·설명 글만 바꾼다. 잡·needs·인자·스크립트 동작은 그대로다.

가드를 약하게 하는 변경은 없다. 지시문 2절은 건드리지 않는다(8·10 포함).

## 영향

- 예제 expected 12개 바이트 그대로. oracle `check.py` base(main `b8f8326`)·head 모두 12개 MATCH.
- 스펙 블록 하나가 바뀐다: `metrics.md` 12.5 `compare-duplicate` metrics.json(`compared` 3 → 4). 예제 파일이 아니다(TestData 표본은 아직 없다).
- C#: core-dev가 `metrics.md` 13.2와 judgment.md 5절 검사를, test-engineer가 테스트를 맞춘다(judgment.md 12.5). 둘 다 결정 1의 "올리지 않는" 경우다.

## 남은 위험

- 결정 1은 "릴리스 전"에 기대는 기준이다. 첫 태그 뒤에는 새 입력 모양이라도 바뀐 출력을 읽는 쪽이 있을 수 있다. 그때 release 카드가 이 기준을 다시 본다.
- 결정 5는 ci-dev가 같은 PR에 넣어야 끝난다. 빠지면 카드 수용 기준 7이 남는다.
