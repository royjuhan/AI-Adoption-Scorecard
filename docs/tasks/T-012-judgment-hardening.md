# T-012 판정 보강 (추가 행·결과 쪽 중복·안 본 불일치·몰림 열)

- 마일스톤: M1 끝 [재검토] 결과(2026-11-07, STATUS.md 재검토 기록)
- 상태: 스펙 채택(ADR 0005 결정 3). PR (a) #14 머지. PR (b) 엔진·expected 진행 중

## 목표

M1 [재검토]의 막는 지적 1·2를 고친다. 결과 자료(AI 결과)의 대표 실패인 지어낸 행과 키 중복이 판정에 닿게 한다. 사람이 아직 안 본 불일치가 있으면 통과로 내지 않는다(지시문 3.4 "보류 = 설명 안 되는 불일치"). 몰림은 `case.yaml`에 적은 열로만 본다.

## 수용 기준

1. 추가 행(`counts.extra`)이 판정에 쓰인다. 사람 검토 대상(설명 안 됨 목록)에 추가 행 키를 넣을 수 있다. 지금은 `ArgumentException`이다(JudgmentEngine.cs:44, :61).
2. 결과 쪽 키 중복을 입력 오류(종료 2)가 아니라 항목 결함으로 센다. 기준 쪽 중복은 지금처럼 오류다. 스펙이 정한다.
3. 사람이 아직 안 본 불일치·추가 행이 있으면 통과가 아니다. 판정 순서는 스펙이 정한다(재검토 권고: FOCUS 뒤 조건부).
4. 몰림은 `case.yaml`의 새 키(재검토 권고: `inputs.group`)에 적은 열로만 본다. 안 적으면 몰림을 보지 않는다(M-A). 예제 `case.yaml` 중 몰림이 필요한 것에 한 줄을 더한다.
5. 예제 4종의 판정 값(조건부·통과·미검증·보류)은 그대로다. `expected/judgment.json`의 문장이 바뀌면 ADR 0004 expected 변경 규칙과 ADR 0005로 migration한다. `schema_version`을 올릴지 스펙이 정한다.
6. 탐침 재현: 기준 20행 + 지어낸 30행이 통과가 아니다. 40행 중 39행이 빈 메모 열이 몰림으로 잡히지 않는다. 둘 다 테스트가 있다.
7. 시작 전 main 통과 수는 1558이다. 끝난 뒤 그 이상이고, 삭제·Skip이 없다. 고치는 기존 테스트는 PR 본문에 이유를 적는다.
8. oracle(`tools/oracle/`, prototyper만)이 새 규칙도 낸다. `oracle-and-expected` 규칙 때문에 PR을 나눈다: (a) 스펙 + oracle, (b) 엔진 + 예제 `case.yaml` + expected migration.
9. CI 잡 7개 녹색, main 도구 lead_check 종료 0.

## 폴더 범위

PR (a): `docs/spec/`, `tools/oracle/`, `docs/decisions/`, `docs/STATUS.md`, 이 카드. PR (b): `docs/spec/`, `src/Scorecard.Core/`, `tests/`, `examples/*/case.yaml`, `examples/*/expected/`, `docs/STATUS.md`, 이 카드.

## 메모

- 재검토 근거: metrics.md:308("추가 행을 판정에 쓸지는 판정 카드가 정한다")을 judgment.md:17이 질문 없이 뺐다. M-B 위험은 judgment.md:281에 "미확인"으로만 있었다.
- 추천안을 고르는 기준: 사용자에게 맞나 → 그다음 바이트 보존(재검토 지적 8).
- 같이 고칠 것(작은 것): 지시문 5절 expected 목록에 judgment.json(민감 경로라 ADR 필요. 정리 PR로 미룰 수 있다), T-011 카드 상태 줄.
- 미룬 것: CsvText 따옴표 안 CRLF 뒤 줄 번호 테스트, 엔진-oracle 시드 차분 CI, 사용자에게 보일 확인 문장 고르기(M2-3).

## 잠정 결정 (ADR 0005 결정 3, 리드 채택 2026-11-07)

`docs/spec/judgment.md` 11.19 질문 1~12의 추천안을 채택했다. 한 줄 요지는 STATUS.md 잠정 결정 T-012 행.

1. 추가·중복 행은 검토 항목. 정확도·누락률 셈은 그대로.
2. 결과 쪽 키 중복은 항목 `duplicate`. 기준 쪽은 오류 그대로.
3. metrics.json 새 속성 없음.
4. 안 본 검토 항목이 있으면 조건부 `UNEXPLAINED`(FOCUS 뒤).
5. 사람 입력은 검토 항목 키만.
6. 몰림은 M-A(`inputs.group`), 없으면 off + `add_group`.
7. `group` 검사: 파서 `required`·`type`·`conflict`, 대조 `column`·`duplicate-column`.
8. 예제 `case.yaml` 2개에 `group` 한 줄.
9. 할 일 키 `review_items`, `add_group`.
10. judgment.json schema_version 2.
11. oracle 판 둘(ADR 0007). PR (b) 뒤 `judge_v1` 삭제.
12. PR (a)는 새 표를 11절에만. 제자리 옮김은 PR (b).

## PR (a) 리뷰 지적 (#14, 막는 지적 없음)

- 다음 정리 카드로 넘긴다(oracle을 바꾸므로 PR (b)에 둘 수 없다. `oracle-and-expected`):
  1. 결과 쪽 중복은 첫 행으로 대조해 맞는 답이 앞에 오면 일치로 센다. 줄 순서에 따라 정확도가 달라진다. `duplicate` 항목에 차이가 없다(metrics.md 12절). 안: 중복 키는 일치로 세지 않고 모든 행의 차이를 적는다.
  2. `judge_v1` 삭제를 그 카드의 수용 기준에 넣는다(ADR 0007 결정 4).
  3. 지시문 3.4 문구에 "아직 안 본 검토 항목 → 조건부"를 맞춘다(지금은 해석). metrics.json `items[].status` 값이 늘었는데 schema_version 1인 기준을 ADR 0004 쪽에 한 줄.
- PR (b)에서 한다: 스펙 11절 상태 줄 "제안" → "채택"(judgment.md, case-schema.md, metrics.md).

## PR (b) 바꾼 기존 테스트 (test-engineer)

지운 테스트·Skip 없음. `[Fact]`·`[Theory]` 598 → 637. 이름을 바꾼 테스트 없음. 한 줄에 하나씩, 왜 바꿨나.

- `KeyTests.Duplicate_keys_are_reported_from_the_second`: 결과 쪽 둘째 행은 `duplicate-key` 오류가 아니라 항목 `duplicate`다(metrics.md 12.2). 기준 쪽 세 단언은 그대로다. 뜻이 바뀐 것이지 약화가 아니다.
- `CompareApiShapeTests.Constants_equal_spec_api_block`·`Constants_have_spec_values`: 상수 `ItemDuplicate` = `duplicate`가 는다(15 → 16, metrics.md 12.7 5).
- `CaseApiShapeTests.Model_records_have_spec_shape`: `CaseInputs` 세 인자 생성의 `Group`이 null인지 본다(case-schema.md 10.3). 매개변수 넷은 새 `Case_inputs_has_optional_group`이 본다.
- `CaseYamlTests.Case_data_keys_match_spec_table`: synthetic-examples.md 4.8 `inputs` 칸의 넷째 값 "유형 열 `x`"를 `Group`으로 읽는다(case-schema.md 10.5 5). 칸이 세 값이면 지금처럼 `Group` = null.
- `JudgmentApiShapeTests.Constants_have_spec_values`: `JudgmentJson.SchemaVersion`이 2다(11.16, 질문 10).
- `JudgmentApiShapeTests.Unexplained_keys_must_be_distinct_mismatch_keys`: 추가 행·중복 행 키도 받는다(11.9, 카드 수용 기준 1). 거절(일치 키·없는 키·두 번)은 그대로다.
- `JudgmentJsonTests.Objects_follow_shape_block_order_with_integer_schema_version_first`: 첫 키 값이 2다(3.2 모양 블록도 2, 11.17 문서 2).
- `JudgmentJsonTests`의 출력 모음: 검토 항목 셋(불일치·추가 행·중복 행)을 모두 가진 장면을 더했다. 모음을 쓰는 테스트가 그 장면도 본다.
- `JudgmentJsonTests.Type_value_with_quote_and_emoji_is_escaped_in_json`, `CheckTableTests`의 FOCUS 장면: 몰림은 `inputs.group`만 본다(11.8 M-A). 장면에 `group` 한 줄을 넣는다.
- `CheckTableTests`·`VerdictTableTests`·`TodoTests`·`UnexplainedTests`·`ThresholdDefaultTests`·`FocusTests`: 표 머리 행을 11절 것으로 바꿨다(11.17 test-engineer 1). 읽는 칸이 늘었다(순서, `inputs.group`, 추가 행·중복 행, 다음 할 일). 행 수는 모두 같거나 늘었다.
- `VerdictTableTests` 우선순위 테스트: 11.10 표의 "다음 할 일" 칸을 판정 결과의 todo 키와 맞춘다.
- `ThresholdDefaultTests`: 11.6 표에서 `UNEXPLAINED`가 순서 4·7 두 번 나온다. 근거 키 기준 테스트 이름·데이터는 그대로 두고(처음 순서), 순서 7은 새 테스트 둘이 본다.
- `TodoTests`: 할 일 키 `review_mismatches` → `review_items`, `add_type_column` → `add_group`(질문 9).
- `FocusTests`(메서드 이름 그대로): M-B(키·대조 밖 열 전부) → M-A(`inputs.group` 한 열, 기준 값만, 없으면 off + `add_group`). 열이 없는 경우는 대조 오류 `column`, 이름 충돌은 파서 오류 `conflict`로 본다(11.8, metrics.md 12.3).
- `JudgmentGoldenTests.Example_table_matches_expected_file`: 11.12 예제 표(칸 `inputs.group`, `검토 항목`)를 읽는다. `Instructions_section_12_verdicts_hold`: 지시문 12절 표를 직접 읽는다(옛 7.1 표가 11.17 문서 1에서 지워진다).
- `JudgeGuardTargetTests.Sample_judgment_matches_spec`: G2 표본 판정의 todo 키와 UNEXPLAINED 문장이 11.3·11.5 틀이다. 판정·근거 키는 그대로다.
