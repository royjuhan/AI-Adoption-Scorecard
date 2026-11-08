# T-013 정리 (T-011·T-012 리뷰에서 미룬 것)

- 마일스톤: M1 마무리
- 상태: 끝. PR #16 머지(2026-11-08)

## 목표

T-011a·T-012a·T-012b 리뷰와 M1 [재검토]에서 미룬 것을 한 카드로 끝낸다. 동작이 바뀌는 것은 1·2뿐이다. 예제 expected는 바꾸지 않는다(예제에 결과 쪽 중복이 없다).

## 수용 기준

1. 결과 쪽 키 중복: 맞는 답이 앞에 오면 일치로 세는 문제를 없앤다(T-012 PR (a) 리뷰 1). 스펙이 정한다. 줄 순서를 바꿔도 셈이 같다는 테스트가 있다. `duplicate` 항목이 검토자에게 다른 값을 보인다.
2. `Judge`에 `Inputs` 없는 정의와 measured 결과를 섞으면 어떻게 할지 정하고 테스트한다(T-012 PR (b) 리뷰 3).
3. `tools/oracle/judge_v1.py`와 `check.py`의 판 고르기를 지운다(ADR 0007 결정 4).
4. 스펙의 옛 T-011 표·글을 새 규칙 자리로 옮기고 옛 것을 지운다(judgment.md 4.3·4.4·4.6·4.7·7.1, metrics.md 4.3·4.7, case-schema.md 10.5. T-012 잠정 결정 12). 테스트가 읽는 표 이름은 한 번만 나온다.
5. 지시문 `docs/README-instructions.md` 5절 expected 목록에 `judgment.json`, 3.4에 "아직 안 본 검토 항목 → 조건부"를 맞춘다. 민감 경로라 새 ADR 0008과 STATUS 원칙 변경 행을 같이 둔다. 2절 8·10은 건드리지 않는다.
6. 테스트 보강: 몰림 셈에서 `duplicate` 기준 행을 세지 않는 장면(JudgmentEngine.cs:287 변이 생존), 따옴표 칸 안 CRLF 뒤 다음 행 줄 번호(CsvText.cs:71 변이 생존).
7. T-011a 미룬 문구: `tools/oracle/README.md` 이모지 문구, `selftest.py`의 `EXAMPLE_NAMES` 고정, `.github/workflows/ci.yml`·`tools/ci/README.md`의 oracle-head 설명 문구(ci-dev, 같은 ADR 0008).
8. 시작 전 main 1637. 끝난 뒤 그 이상, 삭제·Skip 없음. CI 7개 녹색, main 도구 lead_check 종료 0. 민감 경로가 있으니 새 reviewer가 전체 diff를 본다.

## 폴더 범위

`docs/spec/`, `docs/README-instructions.md`(5절·3.4만), `docs/decisions/`, `docs/STATUS.md`, `tools/oracle/`, `tools/ci/README.md`, `.github/workflows/ci.yml`(주석·설명 문구만), `src/Scorecard.Core/`, `tests/`, 이 카드.

## 메모

- `oracle-and-expected` 규칙: 이 카드는 expected를 바꾸지 않으므로 oracle과 같은 PR에 둘 수 있다. 1의 새 규칙이 예제 바이트를 바꾸면 PR을 나눈다.
- 근거: T-011 카드 "T-011a 리뷰 지적", T-012 카드 "PR (a) 리뷰 지적"·"PR (b) 리뷰 지적", STATUS 재검토 기록(M1).

## 바꾼 기존 테스트

test-engineer. 지운 테스트·Skip 없음. [Fact]/[Theory] 수는 늘기만 했다. 이름은 그대로 두었다(바꾸면 빠진 테스트로 센다).

- `DuplicateTests.Duplicate_row_values_are_not_compared`: 둘째 행도 대조한다(metrics.md 13.2). 이름은 T-012 때 것이고 뜻이 바뀌었다. 그 키는 `mismatch`, duplicate 항목에 차이 하나, `compared` 2.
- `DuplicateTests.Each_repeat_is_its_own_duplicate_item`: 모든 행이 기준과 같아도 그 키는 `mismatch`(13.2). `compared` 3을 더 본다.
- `DuplicateTests.Duplicates_across_candidate_files_follow_path_order`: k2는 결과 쪽에 두 번 나와 `mismatch`. 차이 수·셈·`compared` 4를 더 본다.
- `DuplicateTests.Duplicates_count_as_candidate_rows_only`: "중복이 없을 때와 fields가 같다"는 옛 규칙이다. 중복 키는 `mismatch`, `compared`는 기준에 키가 있는 결과 행 수(13.2 셈 관계).
- `DuplicateTests.First_candidate_row_is_compared_and_later_rows_are_duplicates`: 주석만(대표 행, 13.2). 단언은 그대로.
- `KeyTests.Duplicate_keys_are_reported_from_the_second`: 결과 쪽 같은 키 k1은 `mismatch`(4.3). 셈 줄을 고쳤다.
- `ProbeTests.Candidate_side_duplicate_becomes_a_duplicate_item`: P4의 r0003은 일치가 아니라 `mismatch`(정확도 95.0). 6.6 표의 판정은 그대로다.
- `UnexplainedTests.Unexplained_table_row_gives_its_result`: 6.4 장면을 judgment.md 12.5대로 만든다(불일치 칸 − 중복 행 칸만큼 보통 불일치, 나머지는 중복 행).
- `UnexplainedTests.Unexplained_table_covers_all_results`: "불일치 0·추가 0·중복 ≠ 0·found" → "중복 행 ≠ 0이고 사람 입력이 중복 행 키뿐인 found"(12.5). 뜻은 같다. 불일치 칸 ≥ 중복 행 칸을 더 본다.
- `UnexplainedTests.One_key_covers_several_duplicate_items`: 세 번 나온 키는 불일치이기도 하다. 내역 "중복 행 2건" → "불일치 1건, 중복 행 2건", 항목 3.
- `TodoTests.Review_items_text_counts_items_by_kind`: InlineData 두 줄. `(0, 0, 1, "중복 행 1건")`은 이제 나올 수 없다 → `(1, 0, 1, "불일치 1건, 중복 행 1건")`, `(1, 1, 2, …)` → `(3, 1, 2, "불일치 3건, 추가 행 1건, 중복 행 2건")`. 실행 이름(인자)이 바뀐다.
- `JudgmentApiShapeTests.Unexplained_keys_must_be_distinct_mismatch_keys`: 장면의 불일치 키가 둘 → 셋(중복 행 키 포함). 거절·받는 키는 그대로.
- 도우미 `JudgmentScene.BadKeys`(`JudgmentInput.cs`): 결과 쪽에 두 번 나온 기준 키를 불일치 키에 넣는다. 그래서 `ProbeTests` P4 두 행이 새 셈으로 6.6 표와 맞는다.
- `JudgmentSpec.ReviewHeading`: 문서에 없는 `### 11.9 …` → `### 6.4 설명 안 되는 검토 항목`. 새 13.3 테스트가 쓴다. 표 머리 행 주석의 절 번호를 지금 자리로.

## 리뷰 지적 (#16, 막는 지적 없음)

- 문서 글머리·상태 줄·옛 절 번호(metrics.md 5·7·78·87·294·311·480·1644·1665·1667줄, judgment.md 5·1137줄, Core 주석의 11.x 절 번호)와 지시문 3.4 문장 범위: 바로 뒤 문서 정리 커밋에서 고친다.
- 사용자 표시: 차이가 `[]`인 mismatch 항목은 같은 키의 duplicate 항목에 이유가 있다. M2-3·M2-4 화면에서 "결과에 같은 키 N번"을 보인다.
- JudgmentEngine 변이 점수 −2.29: 새 정의 검사의 예외 메시지 변이가 살아남는다. 메시지 문구는 테스트하지 않는다(타입만 본다).
- 지시문 3.4 문장 범위 다듬기(괄호로 묶기)는 되돌렸다. 지시문은 민감 경로라 새 ADR이 필요하다. 다음에 ADR을 쓰는 PR에 같이 넣는다.
