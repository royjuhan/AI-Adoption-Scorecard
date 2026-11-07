# STATUS

ADR 0005(완전 자동 진행 모드)에 따라 리드가 머지할 때마다 갱신한다. juhan이 이 파일의 잠정 결정을 뒤집으면, 그 결정에 기대 머지된 PR부터 되돌린다(ADR 0005 결정 8).

## 원칙 변경 (맨 위에 둔다)

| 날짜 | ADR | 바뀐 것 | 결정한 사람 | PR |
| --- | --- | --- | --- | --- |
| 2026-11-07 | 0007 | oracle이 expected judgment.json의 schema_version으로 판정 판을 고른다(T-011 동결본 `judge_v1`, 거절 가드 하나). PR (b) 뒤 지운다. 가드를 약하게 하지 않는다 | 리드(ADR 0005 결정 3) | #14 |
| 2026-11-07 | 0006 | `oracle-head` 잡과 lead_check가 head `selftest.py`도 돌린다. guard가 잡 넷(mutation, oracle, oracle-head, test-run) 결과를 모두 본다. 가드를 약하게 하지 않는다 | 리드(ADR 0005 결정 3) | #12 |
| 2026-11-07 | 0005 | 승인 질문을 묻지 않고 추천안 채택(T-010 뒤). 사람 게이트 대신 CI 잡(build-test, regression-guard, mutation, oracle)과 [재검토]. 지시문 2절 4·5·11, 13절, 14절 6~8, CLAUDE.md. 2절 8·10(실제 자료·회사 자료 금지)은 바꾸지 않는다 | juhan 지시 | #11 |
| 2026-11-07 | 0004 | 사용을 단순하게(입구 둘·엔진 하나·HTML 한 장·명령 넷·AI용 JSON 입구). 2절 1·3·6·7 | juhan 승인 | #6 |
| 2026-11-07 | 0003 | 질문 `human_signoff`, 트리 맨 앞 `SIGNOFF` | juhan 승인 | #6, #9 |
| 2026-11-07 | — | 2절 6 끝에 "`human_signoff`는 비워 두지 않는다"(안 A) | juhan 결정 | #9 |
| 2026-11-07 | — | 2절 10 "pre-commit 또는 테스트에서" | juhan 결정 | #6 |

## 지금

- 진행 중: 없음. 명령 틀(M2-1) 순서는 juhan 결정 카드를 기다린다.
- 다음: T-011a 리뷰 지적 정리 PR(새 ADR 0008 필요) → `docs/cases-public/README.md` 4절(사례 답 20개, 잠재 빈 곳 14~25 스파이크, 빈 곳 카드 2 → 6 → …). M2는 빈 곳 6 뒤.
- 보류: 없음.
- 기록: `docs/cases-public/README.md`의 공개 고객 사례 회사 이름은 juhan 직접 지시(2026-11-07 15:19)로 이 파일에만 둔다. 회사 자료(2절 10)가 아니라 공개된 사례의 이름이다. `examples/`·테스트·스펙에는 넣지 않는다. 2절 8·10과 13절은 그대로다.

## 머지된 카드

| 카드 | PR | 머지 | 테스트 수(머지 뒤) | mutation(바뀐 src) | 메모 |
| --- | --- | --- | --- | --- | --- |
| T-001 솔루션 골격 | #1 | 2026-11-06 | 20 | — | CI ubuntu·windows, `-warnaserror` |
| T-002 가드 테스트 | #2 | 2026-11-06 | 77 | — | G1·G2·G3 |
| T-003 case.yaml 스키마·파서 | #3 | 2026-11-07 | 457 | — | |
| T-004 분류 | #4 | 2026-11-07 | 630 | — | 행 모델 A |
| T-005 합성 예제 생성기 | #5 | 2026-11-07 | 830 | — | |
| ADR 0003·0004 | #6 | 2026-11-07 | 830 | — | 문서만 |
| T-007 classification.json schema_version | #7 | 2026-11-07 | 850 | — | expected migration |
| T-006 대조·지표 | #8 | 2026-11-07 | 1066 | — | expected/metrics.json 추가 |
| T-008 human_signoff·inferred | #9 | 2026-11-07 | 1196 | — | expected 그대로 |
| T-009 예제 signoff-final | #10 | 2026-11-07 | 1282 | — | expected 2개 추가, 정확도 80.0 |
| T-010 자동 진행 준비 | #11 | 2026-11-07 | 1282 | 해당 없음(src 변경 없음) | juhan 승인 머지. CI 잡 7개, lead_check, tools/ci 단위 테스트 143개 |
| T-011a 판정 oracle·selftest CI | #12 | 2026-11-07 | 1282 | 해당 없음(src 변경 없음) | ADR 0006. oracle selftest 848, 판정 블록 4개 일치 |
| T-011b 판정 엔진·예제 4종 judgment.json | #13 | 2026-11-07 | 1558 | JudgmentEngine 86.87%, JudgmentJson 98.00%, JudgmentRules 88.89% | 자동 머지(CI 7개 녹색, lead_check 종료 0, 리뷰 2회차 막는 지적 없음). oracle 12 MATCH |
| T-012a 판정 보강 스펙·oracle | #14 | 2026-11-07 | 1558 | 해당 없음(src 변경 없음) | ADR 0007. oracle selftest 1402, 12 MATCH. 새 reviewer 전체 diff 막는 지적 없음 |
| T-012b 판정 보강 엔진·judgment.json v2 | #15 | 2026-11-07 | 1637 | JudgmentEngine 88.00%, JudgmentRules 90.32%, JudgmentJson 98.00%, CompareEngine 94.44%, CaseReader 94.42% | 자동 머지(CI 7개 녹색, lead_check 종료 0, 리뷰 막는 지적 없음). expected migration judgment.json 4개 |

mutation 점수는 T-010의 `mutation` 잡부터 잰다.

## 잠정 결정 (ADR 0005 결정 3. 리드가 추천안을 채택한 것)

T-010 뒤 카드부터 쓴다. 한 줄에 하나. 뒤집으면 "PR" 칸의 PR부터 되돌린다.

| 날짜 | 카드 | 질문 | 채택한 추천안 | PR |
| --- | --- | --- | --- | --- |
| 2026-11-07 | T-011 | 1 판정 표와 우선순위 | 미검증 → 정확도 미달 보류 → 누락률 초과 보류 → 설명 안 되는 불일치 보류 → 시간 조건부 → 몰림 조건부 → 통과. 문턱 비교는 셈 | #12 (a)·(b) |
| 2026-11-07 | T-011 | 2 정확도 충족·누락률 초과 | 보류(`MISSING`) | #12 (a)·(b) |
| 2026-11-07 | T-011 | 3 문턱 기본값 | 95·2·`true`(12.1 값). 기본값이면 표시 + `confirm_thresholds` | #12 (a)·(b) |
| 2026-11-07 | T-011 | 4 시간 | T1. `time_must_drop: true`이고 시간이 없으면 조건부 `TIME` + `measure_time` | #12 (a)·(b) |
| 2026-11-07 | T-011 | 5 몰림 | M-B + F1. 기준 머리 행의 키·`compare` 밖 열, 불일치 2건 이상이 모두 한 값이면 `found`. 스키마·예제 그대로 | #12 (a)·(b) |
| 2026-11-07 | T-011 | 6 설명 안 되는 불일치 | U-B. `review`가 적은 키 목록만 받는다. 아직 안 봤으면 `not_reviewed` + `review_mismatches` | #12 (a)·(b) |
| 2026-11-07 | T-011 | 7 결과를 담는 곳 | O-B. 새 `judgment.json`, `judge --json` = 그 파일. 기존 expected 8개 그대로. 지시문 5절 목록에 `judgment.json` | #12 (a)·(b) |
| 2026-11-07 | T-011 | 8 예제 4종 expected/judgment.json | judgment.md 7.2~7.5 블록을 정답으로 채택. 새 파일만 담은 `expected migration:` 커밋. `Example_folder_has_exactly_the_planned_files` 목록에 한 줄 | #12 (a)·(b) |
| 2026-11-07 | T-011 | 9 signoff-final 판정 | 보류 `ACCURACY`(80.0% < 95%). 누락률은 `not_met`으로 남긴다 | #12 (a)·(b) |
| 2026-11-07 | T-011 | 10 Core API·진행 순서 | judgment.md 4.13 그대로. 규칙은 `internal` `JudgmentRules.cs` 한 파일 | #12 (a)·(b) |
| 2026-11-07 | T-011 | 11 G2·G3 | `judge-case-valid`(샘플 `case-valid`). `Real_targets_use_case_samples_not_basic`의 정확한 쌍 목록에 한 쌍을 더한다(축소 아님) | #12 (a)·(b) |
| 2026-11-07 | T-011 | 12 문장 틀·다음 할 일 문구 | judgment.md 표 그대로 | #12 (a)·(b) |
| 2026-11-07 | T-011 | 13 시간 합 넘침 문장 `{total}` | 예외 없이 `not_met`. 글자는 정확한 합(BigInteger, 큰 쪽 소수 자릿수) | (b) |
| 2026-11-07 | T-011 | 14 머리 행만 있는 기준 파일 | 엔진은 기준 행의 `CsvRow` 머리 행만 본다(교집합에 안 든다). 테스트·예제 밖. 뒤 카드에서 `CompareMetrics`에 파일별 머리 행 | (b) |
| 2026-11-07 | T-011 | 15 인자 확인 시점 | 음수 시간은 `time_must_drop`과 상관없이 예외. 사람 입력 키는 미검증에도 확인 | (b) |
| 2026-11-07 | T-011 | 16 미검증 + 시간 입력 | `time_must_drop: true`면 `measure_time`. 받은 시간은 보지 않는다 | (b) |
| 2026-11-07 | T-011 | 17 문턱 곱 넘침 | 예외 없이 부호대로 비교(파서를 거치면 생기지 않는다) | (b) |
| 2026-11-07 | T-012 | 1 추가·중복 행 | 검토 항목에 넣는다. 정확도·누락률 셈은 그대로 | #14·#15 |
| 2026-11-07 | T-012 | 2 결과 쪽 키 중복 | 대조 오류가 아니라 항목 `duplicate`. 기준 쪽 중복·빈 키는 오류 그대로 | #14·#15 |
| 2026-11-07 | T-012 | 3 metrics.json | 새 속성 없음. schema_version 1, 예제 바이트 그대로 | #14·#15 |
| 2026-11-07 | T-012 | 4 안 본 검토 항목 | 조건부 `UNEXPLAINED`(순서 7, FOCUS 뒤) | #14·#15 |
| 2026-11-07 | T-012 | 5 사람 입력 | 검토 항목의 키. 그 밖의 키·중복은 `ArgumentException` | #14·#15 |
| 2026-11-07 | T-012 | 6 몰림 | M-A. `inputs.group` 열 하나, 없으면 off + `add_group` | #14·#15 |
| 2026-11-07 | T-012 | 7 `group` 검사 | 파서 `required`·`type`·`conflict`, 대조는 기준 파일마다 `column`·`duplicate-column` | #14·#15 |
| 2026-11-07 | T-012 | 8 예제 `case.yaml` | table-compare `group: signal_type`, signoff-final `group: record_kind` | #14·#15 |
| 2026-11-07 | T-012 | 9 할 일 키 | `review_items`, `add_group` | #14·#15 |
| 2026-11-07 | T-012 | 10 judgment.json | schema_version 2 | #14·#15 |
| 2026-11-07 | T-012 | 11 oracle 판 둘 | expected로 고른다. PR (b) 뒤 첫 oracle 변경에서 `judge_v1` 삭제(ADR 0007) | #14·#15 |
| 2026-11-07 | T-012 | 12 PR (a) 스펙 | 옛 표를 두고 새 표는 judgment.md 11절에. 제자리로 옮기는 것은 PR (b) | #14·#15 |

## 재검토 기록 (ADR 0005 [재검토])

| 날짜 | 카드 | 이유(루프 3회 초과·CI 같은 실패 2회·마일스톤 끝) | main 회귀 확인 | 새 reviewer 답 | 고른 길 |
| --- | --- | --- | --- | --- | --- |
| 2026-11-07 | M1(T-001~T-011) | 마일스톤 끝 | main 0f9e810: 빌드 통과, 테스트 1558, oracle 12 MATCH, selftest 854, Core 전체 변이 90.97%(Stryker 로그. `mutation.py`는 전체 실행 때 파일별 점수를 읽지 못했다) | 막는 지적 3: 추가 행·결과 쪽 중복·안 본 불일치가 판정에 닿지 않음, 몰림 열 자동 선택(M-B)이 우연에 걸림, 실행 입구 없이 사례 검증 계획 | 스펙 고침: T-012 판정 보강. 입구 순서(M2-1 앞당김)는 juhan 결정 카드 |

## 카드별 토큰 (서브에이전트 합, 리드 세션 제외)

T-007 이전은 기록하지 않았다.

| 카드 | prototyper | test-engineer | core-dev | reviewer | 그 밖 | 합 |
| --- | --- | --- | --- | --- | --- | --- |
| T-008 | 기록 없음 | 357,409 | 230,038 | 268,531 | — | 855,978 + prototyper |
| T-009 | 221,981 | 214,346 | — | 248,582 | — | 684,909 |
| T-010 | 357,131(oracle) | — | — | 1,093,645 | ci-dev 842,196 | 2,292,972 |
| T-011 | 1,057,064 | 550,764 | 472,966 | 924,065 | ci-dev 297,347 | 3,302,206 |
| T-012 | 243,716 | 326,704 | 343,107 | 620,153 | M1 재검토 reviewer 328,907, 문서(sonnet) 156,839 | 2,019,426 |
