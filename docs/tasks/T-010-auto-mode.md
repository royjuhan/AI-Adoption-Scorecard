# T-010 자동 진행 준비 (ADR 0005)

- 마일스톤: M1 (운영)
- 상태: 진행 중. 이 카드까지는 juhan이 승인한다(ADR 0005 결정 2).

## 목표

사람 승인 없이 카드가 돌 수 있게 CI와 독립 검증을 만든다. ADR 0005 원문 1~6을 담는다.

## 수용 기준

1. CI 잡 `regression-guard`가 PR base 대비 아래를 막는다. 증거가 있으면 통과한다.
   - tests/ 파일 삭제, Skip 추가, `[Fact]`·`[Theory]` 수 감소, guard 샘플 목록 축소. 증거: 바뀐 src 코드의 mutation 점수가 base 이상.
   - `examples/**/expected/**` 기존 파일 수정·삭제. 증거: 그 파일을 바꾼 커밋의 메시지가 `expected migration:`으로 시작하고, oracle 잡이 같은 바이트를 재현한다.
   - 각 경우를 일부러 만든 음성 대조로 잡이 실패하는지 확인한다.
2. CI 잡 `mutation`이 Stryker.NET으로 PR에서 바뀐 `src/` 파일만 돌린다. 점수를 잡 결과로 남기고 리드가 머지 때 `docs/STATUS.md`에 적는다.
3. `tools/oracle/`에 Python 독립 구현(분류, 대조·지표)이 있다. prototyper가 스펙만 보고 짠다(`src/`를 읽지 않는다). CI 잡 `oracle`이 예제 4종 모두에서 `expected/` 바이트와 같은지 본다.
4. `CoreGuardTargetTests.Real_targets_use_case_samples_not_basic`이 샘플 목록을 정확한 목록으로 비교한다.
5. `.claude/settings.json`의 `permissions.allow`에 dotnet, git, gh pr 명령이 있다. main 직접 push는 없다.
6. `docs/STATUS.md`가 있다: 머지된 카드, 잠정 결정, 원칙 변경, 재검토 기록, 테스트 수·mutation 점수 추이, 카드별 토큰.
7. 지시문·CLAUDE.md가 ADR 0005와 맞는다. 2절 8·10은 바꾸지 않는다.
8. 시작 전 main 통과 수는 1282다. 끝난 뒤 그 이상이고, 삭제·Skip된 테스트가 없다.

## 폴더 범위

`.github/workflows/`, `tools/`, `.claude/settings.json`, `docs/STATUS.md`, `docs/decisions/0005-auto-mode.md`, `docs/README-instructions.md`(2·13·14절), `CLAUDE.md`, `tests/Scorecard.Tests/Guards/CoreGuardTargetTests.cs`, `.config/dotnet-tools.json`, `.gitignore`, `.claude/agents/prototyper.md`. 같은 PR에 문서만 실린다: `docs/tasks/T-011-judgment.md`, `docs/spec/judgment.md`(T-011 초안), `docs/cases-public/README.md`(juhan 지시 원문)

## 메모

- oracle은 T-006 `spikes/T-006/xcheck/compare.py`(커밋 `befaa0a`)와 T-009 `spikes/T-009/xcheck/`(커밋 `a65f02e`)에서 출발한다.
- 판정 카드는 T-011로 옮겼다.

## 남은 일 (3차 리뷰 막지 않는 지적, 뒤 카드 ci-dev)

- TRX 위조(n8): testrun이 한쪽마다 정해진 TRX 하나만 읽고(`trx_files != 1`이면 오류), base 결과 폴더를 head 실행 전에 지우고, lead_check가 test-run INFO 줄을 보인다.
- lead_check head 단계 환경: `HOME`을 임시 폴더로, `PWD`·`OLDPWD`·`GH_TOKEN`·`GITHUB_TOKEN`·`SSH_AUTH_SOCK`를 뺀다.
- ci-tamper에 ADR·STATUS 출구(또는 WARN + reviewer 확인).
- guard `--job`에 oracle·mutation 결과를 더한다.
