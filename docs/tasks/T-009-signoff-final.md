# T-009 4번째 공개 예제 signoff-final (서명 단계)

- 마일스톤: M1 (ADR 0003 남은 질문 2, 2026-11-07 juhan 승인)
- 상태: 테스트·리뷰 (스펙 승인됨 2026-11-07)

## 목표

지시문 12.1 비공개 예시와 같은 구조(단계 7개, 마지막 단계가 사람이 최종 확인·서명하는 단계)를 가진 공개 합성 예제 `examples/signoff-final/`을 더한다. 마지막 단계는 `human_signoff: yes`이고 ④ `SIGNOFF` 한 행으로 분류된다. 다른 예제 3종과 같은 파일(`intake.md`, `case.yaml`, `answers.yaml`, `reference/`, `candidate/`, `expected/classification.json`, `expected/metrics.json`)을 둔다.

## 수용 기준

1. `examples/signoff-final/`의 파일이 스펙 블록과 바이트까지 같다. 자료는 T-005 합성 생성기(시드 고정)로 만든다. 실제 값 패턴 검사를 통과한다.
2. `expected/classification.json`·`expected/metrics.json`은 스펙 블록에서 옮긴다. 엔진 출력에서 복사하지 않는다. 골든 테스트가 바이트까지 같다. 7단계는 ④ `SIGNOFF` 한 행이고 게이트 행이 없다.
3. 기존 예제 3종의 파일과 expected 6개는 바이트 그대로다.
4. 지시문 12절과 M1 G1의 "공개 예제 3종"을 "4종"으로, 12절 표에 `signoff-final` 행을, "`answers.yaml`(질문 9개 답)"을 답 10개로 고친다(T-008 남은 일).
5. 시작 전 main 통과 수는 1196이다. 끝난 뒤 그 이상이고, 삭제·Skip된 테스트가 없다. 예제 목록을 고정한 기존 테스트를 바꿔야 하면 juhan이 직접 승인한다.
6. dotnet test가 ubuntu·windows CI에서 녹색이다.

## 폴더 범위

`docs/spec/`, `docs/README-instructions.md`(5절 폴더 트리, 9절 M1 G1·M2 G2, 12절, 사람 결정 뒤 리드), `examples/signoff-final/`, `tests/`, `src/Scorecard.Core/`(필요할 때만)

## 메모

- 12.1 블록 자체와 TestData 합성 사본은 바꾸지 않는다. 공개 예제는 이름·문장·값을 새로 합성한다.
- expected 새 파일 추가는 상시 승인 예외다. juhan이 스펙 블록을 직접 확정한다. 커밋은 ADR 0004 expected 변경 규칙(`expected migration:`, expected만, 통과 수 기록, reviewer가 diff 전문)을 따른다.
- 판정(조건부·통과·미검증)은 아직 없다. 이 예제의 기대 판정 칸은 판정 카드에서 채운다.
- 검증 자료 구조(기준·결과 건수, 심을 차이)는 prototyper가 제안하고 juhan이 정한다.
