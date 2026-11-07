# T-004 질문 9개·기본값·분류 트리

- 마일스톤: M1 (지시문 11절)
- 상태: 구현·리뷰 완료, PR 대기

## 목표

지시문 3.2·3.3을 `docs/spec/classification.md`로 확정하고 Core에 분류를 구현한다.

## 수용 기준

1. 결정 트리의 분기마다 단위 테스트가 1개 이상 있다.
2. 기본값이 쓰인 답은 근거 문장 끝에 "(기본값 사용, 확인 필요)"가 붙는다.
3. 공개 예제 3종(`examples/table-compare`, `form-fill`, `mixed-exceptions`)의 `answers.yaml`을 넣으면 `expected/classification.json`과 일치한다.
4. dotnet test가 ubuntu·windows CI에서 녹색이다.
5. 단계마다 AI 과잉 후보(`current_actor: ai_each_time`이고 단계 행이 ②)를 낸다(스펙 승인 결정 8).

## 폴더 범위

`docs/spec/`, `src/Scorecard.Core/`, `tests/`, `examples/`

## 샘플 자료

공개 예제 3종(지시문 12절). 모두 합성. `expected/`는 사람이 확정한 뒤 커밋한다(지시문 14절 8, 12절).

## 메모

- 질문 9개와 기본값 표는 `docs/spec/case-schema.md` 4.4에만 있다(T-003 승인 결정 5). `classification.md`는 그 표를 가리킨다.
- 파서는 `Scorecard.Core.Cases.CaseParser`. 기본값은 `Answer<T>.IsDefault`로 온다.
- 분류 숫자(예: Q4의 `batch_size < 5`)를 코드에 박지 않는다(지시문 13절). 어디서 읽을지 스펙에서 정한다.
- 근거 문장도 Core에서 만든다(CLAUDE.md).
- T-005(합성 예제 생성기)보다 먼저 하므로, 예제 3종의 `answers.yaml`·`expected/classification.json`을 이 카드에서 만들지, 어떻게 사람이 확정할지 스펙에서 정한다.
- T-003에서 넘어온 항목(오류 상한·제어 문자·테스트 보강)은 이 카드 범위 밖이다. 별도 카드로 연다.
