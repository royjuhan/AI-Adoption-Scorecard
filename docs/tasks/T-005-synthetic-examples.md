# T-005 합성 예제 생성기와 예제 3종

- 마일스톤: M1 (지시문 11절)
- 상태: 구현·리뷰 완료(2회차 승인), PR 대기

## 목표

스키마에서 합성 `reference/`·`candidate/`를 만드는 생성기(시드 고정)를 만든다. 지시문 12절의 예제 3종을 생성기로 채우고, 사람이 `expected/`를 확정한다.

## 수용 기준

1. 생성기를 두 번 돌려도 같은 파일(바이트)이 나온다. 다른 경로·문화권에서도 같다.
2. 예제에 실제 값 패턴(품번·이메일·경로)이 없다. 검사(지시문 2절 10 "pre-commit 검사")가 통과한다.
3. 예제 3종이 지시문 12절 표의 검증 자료 구조를 가진다. table-compare는 30건·불일치 3·누락 1, form-fill은 20건·전부 일치, mixed-exceptions는 자료 없음.
4. 예제마다 `case.yaml`, `intake.md`가 있고, `case.yaml` 단계·답은 T-004 `answers.yaml`과 같다(classification 스펙 7.1의 5).
5. dotnet test가 ubuntu·windows CI에서 녹색이다.

## 폴더 범위

`docs/spec/`, `src/Scorecard.Core/`, `tests/`, `examples/`

## 샘플 자료

공개 예제 3종(지시문 12절). 모두 합성. `expected/`는 사람이 확정한 뒤 커밋한다(지시문 14절 8). 상시 승인(2026-11-07)으로 추천값을 확정값으로 쓴다.

## 메모

- 시드 고정이라도 Core는 `System.Random`을 쓸 수 없다(G1 D1). 생성기를 어디에 둘지(Core 안 자체 결정론 수열, 또는 테스트·도구 쪽), 어떤 명령으로 돌릴지 스펙에서 정한다. Cli 명령은 M2다.
- pre-commit 검사는 아직 없다. 검사를 무엇으로 할지(테스트로 대신할지, git 훅을 둘지) 스펙에서 정한다.
- 검증 자료 형식(CSV 열, 키, 허용오차)은 case-schema의 `inputs`·`compare`를 따른다. 대조·지표·판정 계산은 T-006·판정 카드다. 이 카드는 자료와 `case.yaml`만 만든다. `expected/metrics.json`은 T-006에서 만든다.
- 12절 "단계 수 6/5/5"는 T-004 행 모델 A의 행 수로 이미 맞췄다.
- T-004 `examples/*/answers.yaml`·`expected/classification.json`은 바꾸지 않는다.
