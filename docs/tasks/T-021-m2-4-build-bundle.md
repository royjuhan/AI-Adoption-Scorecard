# T-021 M2-4 build 의뢰서 번들 (`build`, `--step`, `export-check.md`)

- 마일스톤: M2-4(ADR 0010 B-12). 끝나면 v0.1.0-alpha 태그(릴리스는 리드·release).
- 상태: 스펙+구현 한 PR(ADR 0010 B-12). 결정은 추천안 채택(ADR 0005 결정 3), STATUS 잠정 결정 T-021 행
- 바탕: M2-3 PR(T-020, 브랜치 `m23`) 위에 쌓는다.

## 목표

`scorecard build <case>`가 먼저 만들 ② 단계를 추천하고, `--step <번호>`("이 단계 만들까요?"에 예)면 지시문 3.6 제작 의뢰서 번들을 `<출력 폴더>/build-request/`에 쓴다. 번들은 Core가 만들고 Cli는 쓰기만 한다. `export-check.md`에 사람 확인이 없으면 내보내지 않고 입력 오류(2)로 끝난다. 지시문 10절 M2 G2의 end-to-end(`start --answers` → `check` → `open` → `build`, 사람용·`--json`)를 테스트로 둔다.

## 수용 기준

1. 스펙 `docs/spec/build-request.md`(200줄 이내)와 데이터 `docs/spec/data/build-request.md`. 테스트가 데이터 표·블록을 읽는다.
2. 묻지 않는다. 예 = `--step <번호>`, 아니오·나중에 = `--step` 없이 끝(아무것도 쓰지 않음, 0). `--step` 없는 `--json`은 추천·고를 수 있는 단계를 낸다.
3. 번들 파일은 지시문 3.6 표 여섯 종(`samples/`는 `README.md` + 출력 열이 있으면 `output.csv`). 바이트는 Core, 결정론(경로·문화권), 시각·난수 없음, LF.
4. 자료 파일 값은 번들에 넣지 않는다. 열 이름·허용 오차는 `case.yaml`, 숫자는 집계값만. 합성 샘플은 정해진 식.
5. `export-check.md`: 번들 파일 SHA-256, 문자열 검사(품번·이메일·경로, `synthetic-examples.md` 4.10 패턴), 사람 확인 줄. 확인 없음·빈 확인·초안과 다름·검사 걸림이면 내보내지 않고 2.
6. end-to-end 골든: 공개 예제 5종, `tests/Scorecard.Tests/TestData/build/<예제>/`. 사람용과 `--json`이 같은 번들. 예제 번들에 실제 값 패턴 0.
7. G2·G3 대상 `cli-build-case-valid` 추가. `src/` 가드(네트워크·LLM·프로세스 금지)는 그대로(옮기는 일은 M3, STATUS 잠정 결정).
8. `tools/oracle`·`tools/ci`·`.github`·`.claude`·`CLAUDE.md`·지시문·`examples/*/expected`를 바꾸지 않는다. oracle 15 MATCH. 삭제·Skip 없음, 테스트 수 2198 이상.

## 폴더 범위

`docs/spec/build-request.md`, `docs/spec/data/build-request.md`(새), `docs/spec/json-output.md`, `docs/spec/data/json-output.md`, `docs/tasks/`(이 카드), `docs/STATUS.md`, `docs/LATER.md`, `src/Scorecard.Core/Build/`(새), `src/Scorecard.Cli/`, `tests/Scorecard.Tests/Build/`(새), `tests/Scorecard.Tests/TestData/build/`(새), `tests/Scorecard.Tests/Cli/`, `tests/Scorecard.Tests/Guards/`(대상 등록).

## 바꾼 기존 테스트

- `CliErrorTests.Cli_error_codes_match_spec_table`: 기대 코드에서 `unavailable`을 뺐다. `build`가 생겨 쓰는 곳이 없고 데이터 6.2 표에서도 뺐다. 표와 Cli 코드가 같은지 보는 단언은 그대로다.
- `CliErrorTests.Build_error_json_equals_spec_block`: 코드는 그대로. 데이터 6.1 블록이 `unavailable`에서 `start` 전 `build`의 `file` 오류(classification.json 없음)로 바뀌었다.
- `CliErrorTests.Argument_table_row_gives_expected_error`: 코드는 그대로. 데이터 6.3 표의 `build <case>` 행이 바뀌고 `build`·`--step` 행 다섯이 늘었다.
- `CliDeterminismTests.Input_files_are_unchanged`: `build --json`의 기대 종료 코드 2 → 0(추천만, 아무것도 쓰지 않음). 출력 폴더 인자를 같이 넘긴다. 파일 목록 단언은 그대로다.
- `CliHelpTests`: 코드는 그대로. 도움말 블록의 `build` 줄과 `--step` 옵션 줄이 바뀌었다.
- `GuardTargetAdapterTests.Real_targets_use_case_samples_not_basic`: 대상·샘플 쌍에 `cli-build-case-valid=case-valid`를 더했다. 대상을 더함, 축소 아님.

## 결과

- 테스트 2241 통과(시작 2198, 지운 테스트·Skip 없음). oracle 15 MATCH. `dotnet format --verify-no-changes` 통과.
