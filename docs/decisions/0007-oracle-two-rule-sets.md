# 0007. oracle이 expected judgment.json의 schema_version으로 판정 판을 고른다

- 날짜: 2026-11-07
- 상태: 결정(리드, ADR 0005 결정 3에 따라 추천안 채택). T-012 PR (a).
- 작성: 리드(prototyper 요약, `docs/spec/judgment.md` 11.15를 바탕으로)
- 관련: ADR 0004 expected 변경 규칙, ADR 0005, ADR 0006, `docs/tasks/T-012-judgment-hardening.md` 수용 기준 8, STATUS.md 재검토 기록(M1)

## 배경

M1 [재검토]가 판정 규칙 두 곳을 고치라고 했다(추가 행·결과 쪽 중복·안 본 불일치, 몰림 열). 판정 규칙이 바뀌면 oracle과 expected judgment.json이 같이 바뀐다. `oracle-and-expected` 규칙 때문에 한 PR에 둘 수 없다. PR (a)에서 oracle만 새 규칙으로 바꾸면 기존 expected(schema_version 1)를 재현하지 못해 `oracle-head`가 빨갛고, 머지 뒤 PR (b) 전까지 main의 `oracle` 잡도 빨갛다.

## 결정

1. `tools/oracle/judge_v1.py`는 T-011 규칙의 동결본이다. 첫머리에 가드가 있다. `inputs.group`이 있거나 결과 쪽 키 중복이 있는 케이스는 거절한다.
2. `tools/oracle/judge.py`는 T-012 규칙이다(judgment.json schema_version 2).
3. `check.py`는 예제의 expected judgment.json이 모두 `"schema_version": 1`이면 `judge_v1`, 그 밖이면 `judge`로 재현한다. 예제마다 판이 섞이면 judgment.json을 모두 DIFF로 낸다. CI 계약(인자, 출력 줄, 보고서)은 그대로다.
4. PR (b)가 머지된 뒤 첫 oracle 변경에서 `judge_v1.py`와 판 고르기를 지운다.

가드를 약하게 하는 변경은 없다. `tools/ci`·workflows는 바꾸지 않는다.

## 남은 위험

- 누가 expected를 schema_version 1 블록으로 되돌리면 `judge_v1`이 재현한다. 막는 것은 셋이다. 예제 `case.yaml`의 `group` 줄(`judge_v1`이 거절), C# 골든·블록 = 파일 테스트, 되돌림도 expected 수정이라 migration 규칙.
- 판이 둘인 동안 oracle 코드가 두 배다. PR (b) 뒤 지워서 끝낸다.
