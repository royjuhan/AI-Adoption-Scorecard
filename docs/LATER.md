# LATER

ADR 0010 B-8·9에 따라 미룬 일을 모은다. 리뷰의 막지 않는 지적, 보강·정리 거리, M2 뒤로 미룬 카드다. 보강·정리 카드는 열지 않는다. M2(v0.1.0-alpha) 뒤에 이 목록으로 카드를 묶는다.

## 정해졌고 다음에 하는 일

- **lead_check mutation 중복 실행.** juhan 결정(2026-11-08): CI mutation 결과 재사용, ADR 0010 PR 뒤 ci-dev 카드로. 지금은 머지 전 lead_check가 CI와 같은 mutation을 다시 돌린다(src를 바꾸는 PR마다 약 40분).

## M2 뒤로 미룬 카드

### T-015 빈 곳 6 혼동행렬

- 작업물: 커밋 `5b21280`. 스펙 `docs/spec/confusion.md`, oracle `tools/oracle/confusion.py`(selftest 2110), 카드 `docs/tasks/T-015-confusion-matrix.md`, 스파이크 `spikes/gap-06/`. PR #19는 닫았고 브랜치에서 `46bce6f`로 되돌렸다.
- 막는 지적 B1(PR #19 리뷰): 결과 쪽 키 중복이 오판을 정밀도에서 숨긴다. 결과 쪽 중복 키는 칸에서 "결과 없음"이라, 오판 행을 한 번 더 적으면 그 오판이 정밀도 분모에서 빠진다(재현: `p21,긁힘`을 두 번 적으면 `긁힘` 정밀도가 80.0(4/5) 대신 100.0(4/4)이 되고, 사람이 모두 설명하면 통과).
- 추천안 (a): 중복 키의 행 가운데 관심 범주 p를 적은 행이 있고 기준이 p가 아니면 p의 오판으로 센다(키당 한 번, 맞힘 아님). 칸은 "결과 없음" 그대로, 예제 블록 그대로. 고치던 작업은 로컬 stash `T-015 B1 fix WIP (abandoned)`에 있다(저장소에는 없다).
- 막지 않는 지적 4건:
  1. 오판 행을 빼면 누락이 되어 통과할 수 있다. confusion.md 9절과 ADR 남은 위험에 적는다.
  2. 점수표(`scores`)와 같이 쓸 때 판정 순서와 `checks`·`thresholds`·`defaults`·`todo` 순서를 장면으로 고정한다(confusion.md 6.3).
  3. 파서 장면: `confusion` 값이 null인 경우, 원소 모양이 틀린 경우(4.1).
  4. `no_rows`일 때 기본값 표시 글머리(5.2).
- 다시 열 때 ADR 번호는 새로 받는다. 0010은 ADR 0010(방향 수정)이 쓴다.

### 빈 곳 카드 순서

- `docs/cases-public/README.md` 4절 3항 순서와 STATUS 잠정 결정 "cases-public" 행(2 → 6 → 8 + A → 12 → …)은 M2 뒤로 간다(ADR 0010 B-7). 2(T-014)는 끝났다. 다음은 6(T-015)이다.

### Excel 내보내기(옛 M2-8)

- juhan의 M2 넷(ADR 0010 B-12)에 없다. 지시문 3.1·3.5의 `open --excel`은 그대로 두었다.

## T-014의 남은 일

- `docs/tasks/T-014-text-result-check.md` "남은 일" 1~4. 요지: `case-schema.md` 표 옮기기, 지시문 12절 `reply-drafts` 행과 제목 "4종", 출처 표를 끝까지 못 읽었을 때 `blind` 검사 안 함(기록), -0 처리(기록).
- `docs/spec/text-check.md` 9절에서 "다음 카드"로 둔 것(채점자 배정 치우침, 다른 행 값, 칸 안 출처 표기 등).

## ADR 0010 PR에서 본 문서 어긋남

- `README.md` 첫 문단 "runs with no AI at run time"은 도구가 아니라 납품물 이야기다(ADR 0010 A-4). `README.ko.md`에는 안내 한 줄이 없다(지시문 2절 9 "README는 한/영 두 벌").
- 지시문 4절 "입구 스킬" 행의 "제품 코드가 아니다"는 A-1(도구는 온라인 AI 도구)과 결이 다르다.
- 지시문 6·7절의 에이전트·CLAUDE.md 템플릿 블록은 실제 파일과 다르다(ADR 0005 뒤부터). 실제 파일이 기준이다.
- 스펙에 남은 옛 M2 번호(`judgment.md`의 "M2-4 `review`" 등). ADR 0010 번호 표로 읽는다.

## 리뷰의 막지 않는 지적 (ADR 0010 뒤 PR)

| 날짜 | PR | 지적 | 어디 |
| --- | --- | --- | --- |
