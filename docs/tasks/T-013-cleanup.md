# T-013 정리 (T-011·T-012 리뷰에서 미룬 것)

- 마일스톤: M1 마무리
- 상태: 스펙 작성 중

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
