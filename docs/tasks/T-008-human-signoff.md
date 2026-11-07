# T-008 human_signoff 질문과 추정 답 표시

- 마일스톤: M1 (ADR 0003 결정, ADR 0004 순서 4·질문 16 해석 A)
- 상태: 구현·리뷰 (스펙 승인됨 2026-11-07)

## 목표

단계마다 10번째 답 `human_signoff`(`yes`/`no`, 기본값 `no`)를 받는다. 분류 트리 맨 앞에 `SIGNOFF`를 둔다. `yes` 단계는 ④이고 뒤에 게이트 행을 붙이지 않는다. 같은 카드에서 단계 항목 `inferred`(AI가 추정한 답 키 목록)를 파싱하고 근거 문장에 추정 표시를 한다(ADR 0004 결정 2, 질문 16 해석 A).

## 수용 기준

1. `SIGNOFF` 분기 단위 테스트가 있다. `yes` 단계에 게이트 행이 없다. 결정 트리 표·문장 틀 표·읽은 답 표가 스펙과 테스트에서 맞는다.
2. 예제 3종 `answers.yaml`·`case.yaml`의 모든 단계에 `human_signoff: no`를 적는다. `expected/classification.json` 3개는 바이트 그대로다. 하나라도 바뀌면 멈추고 juhan에게 보고한다(ADR 0003 결정).
3. `inferred` 파싱 오류(`enum`, `duplicate`)와 추정 표시 테스트가 있다(TestData). 공개 예제에는 `inferred`를 쓰지 않는다.
4. 시작 전 main 통과 수는 1066이다. 끝난 뒤 그 이상이고, 삭제·Skip된 테스트가 없다.
5. dotnet test가 ubuntu·windows CI에서 녹색이다.

## 폴더 범위

`docs/spec/`, `docs/README-instructions.md`(2절 6·3.1·3.2·3.3, 사람 결정 뒤 리드), `CLAUDE.md`(절대 원칙 한 줄, juhan 결정 안 A), `src/Scorecard.Core/`, `tests/`, `examples/*/answers.yaml`, `examples/*/case.yaml`

## 메모

- 출발점: ADR 0003 "결정"의 규칙·"결정이 같이 바꾸는 곳", ADR 0004 결정 2와 질문 16.
- 공개 API 변경: `StepAnswers` 10번째 답, `Answer<T>.IsInferred`. 이를 쓰는 기존 테스트(`CaseApiShapeTests` 등)와 분류 기준 답(classification.md 6.1)을 고쳐야 한다. 기존 테스트 변경은 juhan이 직접 승인한다(상시 승인 예외).
- 지시문 2절 6 "모르면 기본값"과 "`human_signoff`는 늘 명시한다"의 관계를 스펙 질문으로 juhan에게 묻는다(2절 원칙 변경은 상시 승인 예외).
- expected 파일은 바꾸지 않는다. 스펙 블록 안의 `answers.yaml`·`case.yaml`과 SHA-256 줄(classification.md 7.2~7.4, synthetic-examples.md 7.2~7.4)은 바뀐다.
- 4번째 예제(12.1 구조, signoff-final)는 다음 카드다.

## 남은 일 (reviewer 사소한 지적, 다음 카드로)

- 지시문 12절 "`answers.yaml`(질문 9개 답)"을 답 10개로. `signoff-final` 카드에서 12절을 고칠 때 같이.
- 지시문 3.3 `SIGNOFF` 줄의 `→` 열을 Q1·Q2 줄과 맞춘다(공백 하나). 다음 지시문 수정 때.
- `tests/Scorecard.Tests/Synthetic/CaseYamlTests.cs:19` 주석 "답 9개" → 10개. 정리 카드(sweeper).
- `ClassificationApiShapeTests`가 `StepClassification` 위치 매개변수 순서를 검사하지 않는다. 필요하면 정리 카드에서 더한다.
