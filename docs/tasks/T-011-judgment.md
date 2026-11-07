# T-011 판정 (통과·조건부·보류·미검증)

- 마일스톤: M1 (지시문 3.4, metrics.md 11절)
- 상태: 스펙 채택(ADR 0005 결정 3). PR (a) oracle 판정 진행 중

## 목표

대조 결과(`CompareMetrics`)와 케이스의 `thresholds`로 판정을 낸다. 판정은 통과·조건부·보류·미검증 넷이다(지시문 3.4). 판정마다 근거 문장을 Core가 만든다. 미검증이면 "무엇을 재면 되는지"를 적는다. 판정 로직은 Core에만 둔다.

## 수용 기준

1. 예제 4종의 판정이 지시문 12절과 맞는다: table-compare 조건부, form-fill 통과, mixed-exceptions 미검증, signoff-final(정확도 80.0, 문턱 미달)은 스펙이 정한 값.
2. 판정 규칙 표(문턱 경계, 시간 조건, "특정 유형에만 불일치가 몰림", "설명 안 되는 불일치")가 스펙과 테스트에서 맞는다. 경계는 셈(`counts`)으로 비교한다(metrics.md 4.8 권고).
3. 시간 자료가 없을 때의 판정과 표시가 정해져 있고 테스트가 있다.
4. 판정 결과를 담는 파일·속성(새 파일 또는 metrics.json 속성)과 expected가 스펙 블록에서 정해진다. expected 추가·변경은 ADR 0005에 따라 추천안을 채택해 STATUS.md에 적고 ADR 0004 expected 변경 규칙을 따른다.
5. 시작 전 main 통과 수는 1282다. 끝난 뒤 그 이상이고, 삭제·Skip된 테스트가 없다.
6. dotnet test가 ubuntu·windows CI에서 녹색이다.
7. oracle(`tools/oracle/`, prototyper만)이 판정도 낸다. oracle 잡이 `selftest.py`도 돌리고(ci-dev), `selftest.py`의 고정 파일 수(`len(base) == 8` 같은 것)를 없앤다(T-010 리뷰 사소 8).
8. PR을 나눈다(T-010 3차 리뷰 6). oracle이 판정을 내는 변경(`tools/oracle/`)과 새 `expected/` 파일은 같은 PR에 두지 않는다(`oracle-and-expected` 규칙). 순서: (a) oracle 판정 + selftest CI PR → (b) 판정 엔진 + expected PR. (a)의 `ci.yml` 변경에는 새 ADR과 STATUS.md "원칙 변경" 행을 같이 둔다(`ci-changed` 규칙).

## 폴더 범위

`docs/spec/`, `src/Scorecard.Core/`, `tests/`, `examples/*/expected/`(juhan 승인 뒤), `examples/*/case.yaml`(필요하면, juhan 승인 뒤)

## 메모

- 출발점: 지시문 3.4 판정 문장, metrics.md 11절(넘겨받는 것, 권고), case-schema의 `thresholds`(승인 질문 11), T-005 4.7(table-compare 불일치가 `irregular`에 몰림).
- "몰림"에 쓸 열 이름은 `case.yaml`에 없다(metrics.md 승인 질문 16). 스키마에 키를 더할지 스펙 질문으로 묻는다.
- 시간 지표(처리·검토 시간)는 내부 명령 `measure`(M2)의 몫이다. 이 카드에서 시간 입력을 어디서 받을지, 없으면 판정이 어떻게 되는지 정한다.
- AI 과잉 후보는 분류 카드(T-004)에 이미 있다.

## 잠정 결정 (ADR 0005 결정 3, 리드 채택 2026-11-07)

판정 스펙(`docs/spec/judgment.md`) 승인 질문 1~12의 추천안을 채택했다.

1. 판정 표와 우선순위: 미검증 → 정확도 미달 보류 → 누락률 초과 보류 → 설명 안 되는 불일치 보류 → 시간 조건부 → 몰림 조건부 → 통과. 문턱 비교는 셈.
2. 정확도 충족·누락률 초과: 보류(`MISSING`).
3. 문턱 기본값: 95·2·`true`(12.1 값). 기본값이면 표시 + `confirm_thresholds`.
4. 시간: T1. `time_must_drop: true`이고 시간이 없으면 조건부 `TIME` + `measure_time`.
5. 몰림: M-B + F1. 기준 머리 행의 키·`compare` 밖 열, 불일치 2건 이상이 모두 한 값이면 `found`. 스키마·예제 그대로.
6. 설명 안 되는 불일치: U-B. `review`가 적은 키 목록만 받는다. 아직 안 봤으면 `not_reviewed` + `review_mismatches`.
7. 결과를 담는 곳: O-B. 새 `judgment.json`, `judge --json` = 그 파일. 기존 expected 8개 그대로. 지시문 5절 목록에 `judgment.json`.
8. 예제 4종 expected/judgment.json: judgment.md 7.2~7.5 블록을 정답으로 채택. 새 파일만 담은 `expected migration:` 커밋. `Example_folder_has_exactly_the_planned_files` 목록에 한 줄.
9. signoff-final 판정: 보류 `ACCURACY`(80.0% < 95%). 누락률은 `not_met`으로 남긴다.
10. Core API·진행 순서: judgment.md 4.13 그대로. 규칙은 `internal` `JudgmentRules.cs` 한 파일.
11. G2·G3: `judge-case-valid`(샘플 `case-valid`). `Real_targets_use_case_samples_not_basic`의 정확한 쌍 목록에 한 쌍을 더한다(축소 아님).
12. 문장 틀·다음 할 일 문구: judgment.md 표 그대로.
