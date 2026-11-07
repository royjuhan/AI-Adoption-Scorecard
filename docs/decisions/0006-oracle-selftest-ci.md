# 0006. oracle 자체 검사(selftest)를 CI에서 돌리고, guard가 잡 넷의 결과를 본다

- 날짜: 2026-11-07
- 상태: 결정(리드, ADR 0005 결정 3에 따라 추천안 채택). T-011 PR (a).
- 작성: 리드(ci-dev 요약을 바탕으로)
- 관련: ADR 0005, `docs/tasks/T-011-judgment.md` 수용 기준 7·8, T-010 3차 리뷰 막지 않는 지적 4, `tools/ci/README.md` "결정한 해석" 10·11

## 배경

T-011은 oracle(`tools/oracle/`)이 판정(`judgment.json`)도 내게 한다. oracle의 `check.py`는 examples에 expected가 있는 파일만 비교한다. 그래서 새 판정 코드가 스펙 블록과 맞는지는 `selftest.py`만 본다. 지금까지 CI는 `selftest.py`를 돌리지 않았다.

## 결정

1. `oracle-head` 잡은 `tools/oracle`이 바뀐 PR에서 head `check.py`와 head `selftest.py`를 모두 돌린다. 둘 중 하나라도 실패하면 잡이 실패한다. head에 `selftest.py`가 없어도 실패한다. `lead_check.py`도 같은 순서로 돈다.
2. `oracle` 잡은 지금처럼 base `check.py`만 돈다. base selftest는 돌리지 않는다. 이유는 셋이다.
   - base selftest는 base oracle만 점검한다.
   - head 예제를 base oracle이 재현하는지는 `oracle` 잡이 이미 본다.
   - oracle이 바뀐 PR에서는 바른 변경에도 base selftest가 실패할 수 있다(예제 글자·TestData 경로를 고정해 둔다).
3. regression-guard는 `needs`에 있는 잡 넷(mutation, oracle, oracle-head, test-run)의 결과를 모두 `--job`으로 받는다. 하나라도 `success`가 아니면 막는다. `skipped`·`cancelled`도 막는다. mutation과 oracle은 이미 "CI 잡 모두 녹색" 머지 조건에 들어 있다. 그래서 막는 범위는 그대로이고, guard 결과 하나만 보면 된다.

가드를 약하게 하는 변경은 없다.

## 남은 위험

- head selftest는 head 코드라 PR이 꾸밀 수 있다. 증거가 아니라, oracle을 정직하게 고칠 때 생기는 실수를 잡는 점검이다.
- `tools/oracle`이 바뀌지 않은 PR이 스펙 표만 바꾸면 selftest가 돌지 않는다. 이것은 reviewer가 본다.
