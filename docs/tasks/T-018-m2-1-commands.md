# T-018 M2-1 명령 틀 (start·open·check·build, `--json`)

- 마일스톤: M2-1(ADR 0010 B-12)
- 상태: 스펙+구현 한 PR(ADR 0010 B-12). 결정은 추천안 채택(ADR 0005 결정 3), STATUS 잠정 결정 T-018 행

## 목표

`scorecard` 명령 틀을 만든다. 사용자 명령 넷(`start`, `open`, `check`, `build`)과 숨은 내부 명령(`classify`, `compare`, `judge`). 묻지 않는다. 파일 입력과 옵션만 받는다. `--json`이면 JSON 문서 하나를 낸다. 종료 코드 0/2/1. 판정·분류는 Core가 하고 Cli는 부르고 쓰기만 한다.

## 수용 기준

1. 내부 명령 `classify`·`compare`·`judge`의 `--json` 표준 출력이 공개 예제 5종의 `expected/{classification,metrics,judgment}.json`과 바이트까지 같다(골든 테스트).
2. `start <case> --answers <파일>`이 출력 폴더에 `classification.json`을, `check <case>`가 `metrics.json`·`judgment.json`을 쓴다. 파일은 예제 expected와 바이트까지 같다.
3. `open`·`build`는 뒤 카드(M2-3 HTML, M2-4 번들) 없이 할 수 있는 만큼만 한다. 고른 동작을 스펙과 STATUS에 적는다.
4. 오류 JSON(`schema_version`, `exit_code`, `errors[]`의 `CaseError` 여섯 값)과 종료 코드를 스펙 표로 테스트한다.
5. 결정론(같은 입력 → 같은 바이트, 다른 경로·문화권), 입력 파일 불변, 도움말(명령 넷만)을 테스트한다. 명령 대상을 G2·G3에 등록한다.
6. 테스트는 프로세스를 띄우지 않고 `CliApp.Run`을 부른다(G1 N2·N3).
7. Core·`examples/`·`expected/`·`tools/`·`.github/`를 바꾸지 않는다. 시작 전 main 1995, 끝난 뒤 그 이상, 삭제·Skip 없음. oracle 15 MATCH.

## 폴더 범위

`docs/spec/json-output.md`, `docs/spec/data/json-output.md`, `docs/tasks/`(이 카드), `docs/STATUS.md`, `docs/LATER.md`, `src/Scorecard.Cli/`, `tests/Scorecard.Tests/Cli/`, `tests/Scorecard.Tests/Guards/`(대상 등록, Cli N6 허용 목록), `README.md`·`README.ko.md`(한두 줄), `.gitignore`(예제 `out/`).

## 바꾼 기존 테스트

- `GuardTargetAdapterTests.Real_targets_use_case_samples_not_basic`: 대상·샘플 쌍 목록에 명령 대상 셋을 더한다. 샘플 목록은 그대로다(대상을 더함, 축소 아님).
- `NetworkGuardTests`의 Cli N6 허용 목록에 `Scorecard.Core`를 더한다. Cli가 Core를 처음 참조한다. Core 자신은 N1~N6·D1·R1 검사를 그대로 받는다(json-output.md 7절).
