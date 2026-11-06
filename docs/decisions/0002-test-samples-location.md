# 0002. test-engineer 샘플 자료 위치

- 날짜: 2026-11-06
- 상태: 승인 (juhan)

## 배경

지시문 6절은 test-engineer가 샘플 자료를 `cases/<case>/`에 만들라고 한다. 그런데 `cases/`는 gitignore 대상이다. 커밋되지 않고 CI에도 없어서 테스트가 재현되지 않는다.

## 결정

- 테스트용 합성 샘플은 `tests/Scorecard.Tests/TestData/`에 둔다. test-engineer의 폴더 범위(tests/) 안이다.
- 공개 골든 예제는 계속 `examples/`에 둔다.
- `cases/`는 비공개 실제 케이스 전용으로 남긴다. `.gitignore`는 루트만 막도록 `/cases/`로 바꾼다.

## 영향

`.claude/agents/test-engineer.md`의 2번 문장을 이 결정에 맞게 고친다.
