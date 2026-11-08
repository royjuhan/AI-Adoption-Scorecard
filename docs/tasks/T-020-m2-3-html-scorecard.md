# T-020 M2-3 HTML 스코어카드 (`open`, `--review`, 입력 SHA-256)

- 마일스톤: M2-3(ADR 0010 B-12)
- 상태: 스펙+구현 한 PR(ADR 0010 B-12). 결정은 추천안 채택(ADR 0005 결정 3), STATUS 잠정 결정 T-020 행
- 바탕: M2-2 PR (b)(T-019) 위에 쌓는다.

## 목표

`scorecard open <case>`가 저장된 결과(classification.json, 있으면 metrics.json·judgment.json)로 스코어카드 결과 객체와 HTML 한 파일을 쓴다. 시안 `/mnt/project-files/m2-draft/`를 따른다. 숫자·판정은 Core JSON 값 그대로, HTML·Cli는 계산하지 않는다.

## 수용 기준

1. 결과 객체(`scorecard.json` = `open --json` 표준 출력)는 Core가 만든다. 속성 표(`docs/spec/data/report.md` 3절)를 테스트가 읽는다.
2. HTML은 지시문 3.5 일곱 구역. 단계마다 AI 판단 옆에 규칙 의견, 다르면 표시. 기본값·AI 추정 표시. 검증 수치·판정은 결과 파일 값. 확인 항목, 다음 할 일, 입력 SHA-256.
3. HTML은 결정론(시각·난수 id 없음)이고 외부 로드가 없다(인라인 CSS, 글꼴·스크립트 없음).
4. `--review <파일>`: 맞음/바꾸기, 바꾸기는 새 담당과 사유 한 줄. 사유 없는 바꾸기는 확정하지 않는다(입력 오류 2). 결과 객체에 기록만 하고 다시 판정하지 않는다.
5. 입력 SHA-256은 Core가 바이트에서 낸다. 출력 폴더 정책을 스펙에 적는다(`<케이스>/out` 그대로 + 해시 기록).
6. 골든 HTML·JSON 바이트: 공개 예제 5종 + TestData 표본 `ai-judgment`, `tests/Scorecard.Tests/TestData/report/`. 결정론, 확인 파일 오류·성공, `open --json` 결과 객체, `http://`·`https://` 로드 없음을 테스트한다.
7. `tools/html-check/check.mjs`(Playwright)로 390·900·1236px에서 `scrollWidth <= innerWidth`.
8. `tools/oracle`·`tools/ci`·`.github`·`examples/*/expected`를 바꾸지 않는다. oracle 15 MATCH. 삭제·Skip 없음.

## 폴더 범위

`docs/spec/report.md`, `docs/spec/data/report.md`, `docs/spec/json-output.md`, `docs/spec/data/json-output.md`, `docs/tasks/`(이 카드), `docs/STATUS.md`, `docs/LATER.md`, `src/Scorecard.Core/Report/`(새), `src/Scorecard.Core/Cases/CaseParser.cs`(`Decode`를 internal로, 확인 파일 읽기), `src/Scorecard.Cli/`, `tests/Scorecard.Tests/Report/`(새), `tests/Scorecard.Tests/TestData/report/`(새), `tests/Scorecard.Tests/Cli/`, `tests/Scorecard.Tests/Guards/`(대상 등록), `tools/html-check/`(새).

## 바꾼 기존 테스트

- `CliGoldenTests`: `open`이 결과 파일 목록만 알리던 테스트를 `Default_output_folder_collects_results_and_open_writes_scorecard`로 바꿨다. `open`의 동작이 바뀌었다(스코어카드를 쓴다). 결과 파일 셋을 기본 폴더에 모으는 확인은 그대로다.
- `CliDeterminismTests`: 더해진 파일 목록에 `out/scorecard.html`, `out/scorecard.json`을 더했다. 같은 비교를 새 파일에도 한다.
- `CliHelpTests`: 옵션 목록에 `--review`를 더했다(도움말 블록이 바뀌었다).
- `GuardTargetAdapterTests.Real_targets_use_case_samples_not_basic`(`CoreGuardTargetTests`): 대상·샘플 쌍에 `cli-open-case-valid=case-valid`를 더했다. 대상을 더함, 축소 아님.

## 결과

- 테스트 2198 통과(시작 수는 M2-2 PR (b) 위라 따로 재지 않았다. 지운 테스트·Skip 없음). oracle 15 MATCH. html-check 세 폭 통과.
