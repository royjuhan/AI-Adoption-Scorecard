# 0012. sweeper가 스펙을 "지금 동작"만 남기고 줄인다

- 날짜: 2026-11-08
- 상태: 채택(juhan 지시, 2026-11-08 08:31 sweeper 카드 [C]). 새 동작은 없다.
- 작성: sweeper, 리드 기록.
- 관련: ADR 0010(스펙 200줄 이내), ADR 0005 결정 9(oracle은 스펙만 본다). `tools/ci/README.md` ci-changed 규칙.

## 배경

스펙 여섯(case-schema, classification, synthetic-examples, metrics, judgment, text-check)이 900~1,700줄이었다. 스파이크 기록, 승인 질문과 결과, 카드별 개정 기록, 트리 정교화·빈 곳 제안이 섞여 있어 "지금 동작"을 찾기 어렵다. ADR 0010은 스펙을 200줄 이내로 둔다.

그런데 C# 테스트(`SpecDocument`)와 oracle(`tools/oracle/selftest.py`)이 스펙의 표·코드 블록·SHA-256 줄을 바이트 그대로 읽는다. 표를 지우면 가드가 약해진다. 그래서 줄이는 대신 나눈다.

## 결정

1. 스펙마다 두 파일로 나눈다.
   - `docs/spec/<이름>.md`: 지금 동작을 글로. 200줄 이내. 절 번호는 그대로 두고 표·블록은 "데이터 N.N"으로 가리킨다.
   - `docs/spec/data/<이름>.md`: 테스트·oracle이 읽는 표, 코드 블록, SHA-256 줄, 읽는 쪽이 찾는 절 제목과 글머리. 바이트 그대로 옮겼다. 다른 글은 없다.
2. 읽는 경로만 바꾼다. `SpecDocument.cs`의 상수 6개, `selftest.py`의 경로 5개를 `docs/spec/data/`로. `selftest.py`의 줄 선택 하나는 지운 스파이크 태그 대신 줄 머리로 고른다. 확인 내용·개수는 그대로다(테스트 1995, selftest 1763, check 15 MATCH).
3. 지운 것: 스파이크 태그와 단계 목록, `[읽음]` 표시, 사람 승인 필요·승인 결과·채택 절, T-008·T-009·T-012·T-013·T-014 개정 기록, 안 비교 표, 테스트로 옮기는 방법 표, 미확인 목록, 빈 곳·트리 정교화 제안. 제목은 `docs/LATER.md` "sweeper가 지운 스펙 절"에 한 줄씩, 원문은 git 기록에.
4. `tools/oracle/`이 바뀌므로(경로와 줄 선택 하나, README 한 줄) ci-changed 규칙에 따라 이 ADR과 STATUS 원칙 변경 행을 둔다. oracle의 판단 로직은 바꾸지 않는다.
5. 같은 sweeper PR에서 다른 에이전트가 `docs/README-instructions.md`를 줄이고(exe 문답, 질문 12개 상한, 고급 질문, 트리 확장. 2절 원칙 8·10은 바이트 그대로) `spikes/`를 지운다.

## 남은 위험

- base `selftest.py`는 옛 경로를 읽지만 어디서도 돌지 않는다(`tools/ci/README.md` "결정한 해석" 10). base `check.py`는 스펙을 읽지 않는다.
- expected 변경 커밋은 `docs/spec/**`의 json·yaml·csv 블록과 SHA 값만 바꿀 수 있다. 데이터 파일도 `docs/spec/` 아래라 그대로 맞는다.
- 본문과 데이터가 어긋날 수 있다. 규칙 표는 데이터에만 두고 본문은 가리키기만 한다.
