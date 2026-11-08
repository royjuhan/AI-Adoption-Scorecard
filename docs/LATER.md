# LATER

ADR 0010 B-8·9에 따라 미룬 일을 모은다. 리뷰의 막지 않는 지적, 보강·정리 거리, M2 뒤로 미룬 카드다. 보강·정리 카드는 열지 않는다. M2(v0.1.0-alpha) 뒤에 이 목록으로 카드를 묶는다.

## 정해졌고 다음에 하는 일

- **lead_check mutation 중복 실행.** ADR 0011 PR에서 한다(`--mutation-from`).

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

## M2-1(T-018)에서 미룬 것

- `--review`·`--times` 파일 입력. ADR 0010 번호 표는 `--review`를 M2-1에 두지만 파일 모양 스펙이 없다. 결과 객체가 정해지는 M2-3 쪽 카드에서 모양과 함께 넣는다(STATUS 잠정 결정 T-018 10).
- `check`의 자료 읽기 전용 사본과 입력 SHA-256(지시문 2절 2, 3.1). `start`가 답 파일 사본을 케이스 폴더에 남기는 일(시안 "남는 것 answers.yaml"). 스코어카드 7구역과 함께.
- `open`의 HTML 스코어카드(M2-3). 지금은 결과 파일 목록만 알린다.
- `build`의 추천·"이 단계 만들까요?"·예/아니오/나중에를 받는 길(#20 지적, ADR 0004 표의 `--step <번호>`)과 번들·`export-check.md`(M2-4). T-021에서 했다(`build-request.md`).
- 지시문 4절 CLI 행 "System.CommandLine"과 다르다(인자 해석을 직접 짰다, 잠정 결정 T-018 8). 지시문 5절 `docs/spec/commands.md`는 만들지 않았다. 명령은 `json-output.md`에 있다.
- `--out` 검사는 `Path.GetFullPath` 뒤 서수 비교다. 대소문자를 무시하는 파일 시스템(Windows·macOS)에서 `Expected`처럼 대소문자만 다른 경로를 케이스 폴더 밖으로 볼 수 있다. Windows 러너에서 확인할 것.
- 지시문 2절 2 "출력은 항상 새 폴더에": 기본 `out/`을 명령마다 다시 쓴다(같은 이름은 덮어씀). 실행마다 새 폴더가 필요한지는 M2-3에서 스코어카드 결과와 함께 본다.

## M2-3(T-020)에서 미룬 것

- `start`·`check`가 HTML을 같이 쓰는 일. 지금은 `open`만 `scorecard.html`을 쓴다(시안은 명령마다 HTML을 연다).
- `--times`(시간 입력)와 절약 추정. 결과 객체 `summary.saving`은 늘 `time_needed`다. 사람 입력 판정도 같이.
- `build --step <번호>`와 제안 카드의 "만들까요?" 받기(M2-4). T-021에서 했다.
- Excel 내보내기(`open --excel`).
- 확인(`--review`) 뒤 다시 분류·판정. 지금은 기록만 한다.
- 스킬 `/scorecard`가 확인 파일을 쓰고 `open --review`를 부르는 길. `SKILL.md`는 이 카드에서 바꾸지 않았다.
- 지시문 2절 2 "출력은 항상 새 폴더에": 실행마다 새 폴더를 만들지 않는다. 대신 입력·결과 SHA-256을 남긴다(report.md 8절).
- 검증 항목은 20개까지만 보인다. 전체 목록을 볼 길(접기·별도 파일).
- `answers` 상태가 `mismatch`·`invalid`일 때 어느 단계·어느 오류인지 보이지 않는다.
- `tools/html-check/check.mjs`는 CI 잡이 아니다. Playwright·브라우저가 필요하다. ci-dev 카드에서 잡으로.
- 위 "M2-1(T-018)에서 미룬 것"의 `--review`, 입력 SHA-256, `open`의 HTML은 T-020에서 했다. `--times`, 입력 사본, 새 폴더는 남는다.

## M2-4(T-021)에서 미룬 것

- 가드(네트워크·LLM·외부 프로세스 금지)를 납품물 템플릿 검사로 옮기는 일(ADR 0010, CLAUDE.md). 템플릿이 M3다. `src/` 가드는 그대로(STATUS 잠정 결정 T-021 12).
- `--review`로 바꾼 담당을 build가 따르는 일. 지금은 결과 객체의 단계 행 담당만 본다(사람이 ②를 ④로 바꿔도 `--step`이 받는다).
- 입력 쪽 합성 샘플(입력 형태별 양식). 지금은 출력 열 샘플만이다. 체크리스트 예제(reply-drafts)는 기준 자료 열(`ticket`·`action`)이 샘플에 없다.
- 스킬 `/scorecard`의 build 안내(`--step`, 사람 확인 줄은 사람이 적는다). `.claude/skills/`라 ADR이 필요하다.
- README 샘플 스코어카드 게시, 비공개 사례 요약(10절 M2 G2의 나머지), v0.1.0-alpha 태그.
- 확인 전 실행이 출력 폴더의 `export-check.md`를 덮어쓴다. 사람이 다른 줄에 적은 메모는 사라진다(확인이 무효가 된 경우만).
- 이전 실행의 `build-request/`가 남아 있으면(다른 단계·번들 밖 파일) 사람이 비워야 한다. 실행마다 새 폴더는 M2-3 LATER와 같은 일이다.

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
| 2026-11-08 | #20 | 추정 답은 사용자 확인 뒤에만 엔진에 넣는다(ADR 0004)는 문장이 CLAUDE.md·지시문 2절 6·13절·9절 scorecard 행에서 빠졌는데 ADR 0010에 기록이 없다. B안의 확인 단계를 M2-2에서 정하고 결정을 적는다 | CLAUDE.md, 지시문 |
| 2026-11-08 | #20 | build의 예/아니오/나중에 답을 받는 길(파일·플래그)이 없다. exe는 묻지 않는다(ADR 0010 해석 1). M2-1에서 정한다 | cli-dev.md 4, 지시문 3.1 |
| 2026-11-08 | #20 | review-checklist의 "판정·분류 로직이 Core에만" 문구(reviewer.md는 고쳤다) | .claude/skills/review-checklist |
| 2026-11-08 | #20 | 분류 oracle(classify.py)이 남는지 한 줄로 못 박기("Core oracle 잡"은 바꾸지 않는 것) | ADR 0010 |
| 2026-11-08 | #20 | 가드 옮기는 때(M2-2 또는 M2-4)는 원문 밖 결정이라 해석 절에 둔다. 납품물 템플릿이 M2 안에 있는지 미확인 | ADR 0010, 지시문 10절 |
| 2026-11-08 | #20 | 적용 시점: #18은 옛 규칙(리뷰 3회)으로 머지됐다고 기록 | ADR 0010 상태 줄 |
| 2026-11-08 | #20 | ②M 정의가 cases-public README 표기 줄에만 있다. M2-2 스펙에서 정하고 지시문이 가리킨다 | 지시문 2절 6·9절 |
| 2026-11-08 | #20 | 옛 M2-5 `check`의 읽기 전용 복사·해시·`--times`·판정 갱신이 어느 카드로 가는지 | ADR 0010 번호 표 |
| 2026-11-08 | #20 | [사람 데모 확인] 삭제가 지시문 2절 4·머리말, release.md, sweeper.md 설명에 안 퍼졌다 | 지시문, .claude/agents |
| 2026-11-08 | #20 | 3.1 "모든 JSON은 첫 키 `schema_version: 1`"은 M2-2에서 classification.json이 2가 되면 고친다 | 지시문 3.1 |
| 2026-11-08 | #20 | lead_check mutation 재사용 결정의 juhan 원문이 저장소에 없다(ADR 0011에 날짜만) | ADR 0011 |
| 2026-11-08 | #21 | CI mutation 잡에 killpg가 없어 남은 프로세스가 업로드 전 mutation.json을 바꿀 수 있다(ADR 0011 남은 위험). 로컬도 setsid로 피할 수 있다 | ADR 0011 |
| 2026-11-08 | #21 | 재사용 차단 목록에 `global.json`·`.config/`가 없다(CI는 head global.json으로 SDK 선택) | tools/ci/lead_check.py |
| 2026-11-08 | #21 | 재사용 테스트가 얕다(소스 글자 위치 비교, main() 끝까지 도는 경로 없음) | tools/ci/test_regression_guard.py |
| 2026-11-08 | #21 | `pull/<N>/merge`로 받으면 main이 움직였을 때 다른 병합 커밋이라 로컬 실행으로 간다(안전, 시간만 못 아낌). 문서에 한 줄 | ADR 0011 |
| 2026-11-08 | #21 | reviewer.md 막는 지적 기준에 ADR·STATUS 기록이 없다. ADR 0010 해석으로 한 줄 | reviewer.md |
| 2026-11-08 | #21 | `lead-check.json` `mutation_reuse.from`에 절대 경로(CI 도구용, 참고) | tools/ci/lead_check.py |
| 2026-11-08 | #22 | sweeper가 `tests/`·`tools/oracle/`의 경로만 바꿨다(폴더 제한·ADR 0005 결정 9의 예외). ADR 0012에 한 줄 | ADR 0012 |
| 2026-11-08 | #22 | classification.md 4.1 "`GATE`는 `SIGNOFF`가 아닌 모든 잎 뒤"에 `failure_impact` 조건이 빠졌다 | docs/spec/classification.md |
| 2026-11-08 | #22 | cases-public README 3절 표: 24를 해소됨에 넣고 이유 칸에 "xlsx 읽기는 남는다" | docs/cases-public/README.md |
| 2026-11-08 | #22 | 지시문 6절 cli-dev 템플릿 2항이 실제 파일과 다르다("exe 문답 없음" 빠짐) | 지시문 6절 |
| 2026-11-08 | #22 | 테스트 주석이 지운 문서·옛 경로를 가리킨다(SignoffFinalExampleTests:9, CaseYamlTests:81, ClassificationInput:19, SpecDocument:5) | tests |
| 2026-11-08 | #22 | guard-tests.md 0절의 `[S…]`·`[V…]` 태그, data 파일 글머리의 리뷰 이력 문구(테스트가 읽을 수 있어 그대로) | docs/spec |

## sweeper가 지운 스펙 절 (제목만)

2026-11-08, ADR 0012. 원문은 git 기록에 있다. 표·블록은 `docs/spec/data/`로 옮겼다.

- case-schema: 0절 표기, 1절 범위·카드 메모 표, 4.1 파서 후보 비교(A·B·C 안), 8절 미확인 목록, 9절 스파이크 단계 목록, 사람 승인 필요, 사람 승인 결과, T-008 개정, 11절 T-014 개정
- classification: 0절 표기, 1절 범위·카드 메모 표, 4.3 행 모델 비교(A·B·C), 6.3 지시문 12.1 비공개 예시와 비교, 6.4 지시문 12절 표와 3.3 트리, 7.1 확정 절차, 8절 테스트로 옮기는 방법, 9절 미확인 목록, 10절 스파이크 단계 목록, 사람 승인 필요, 승인 결과, T-008 개정, T-009 개정
- synthetic-examples: 0절 표기, 1절 범위 표, 7.1 확정 절차, 8절 테스트로 옮기는 방법, 9절 미확인 목록, 10절 스파이크 단계 목록, 사람 승인 필요, 승인 결과, T-008 개정, T-009 개정
- metrics: 0절 표기, 1절 범위·카드 수용 기준 표, 2절 파일 읽기 안 비교(A·B·C), 4.4 다른 정의 비교, 7.1 확정 절차, 8절 테스트로 옮기는 방법, 9절 미확인 목록, 10절 스파이크 단계 목록, 11절 판정 자리, 12.1 왜 바꾸나·12.7 PR (b)에서 옮길 곳, 13.1 왜 바꾸나·13.5 C# 영향, 14절 T-014 개정, 사람 승인 필요, 승인 결과, T-009 개정
- judgment: 1절 범위 표, 4.10·4.11 안 비교, 8절 테스트로 옮기는 방법, 9절 미확인 목록, 10절 스파이크 단계 목록, 11절 T-012 개정, 12절 T-013 개정, 13절 T-014 개정, 사람 승인 필요, 채택
- text-check: 0절 표기, 1절 범위 표·빈 곳 16·17 카드 언급, 8절 테스트로 옮기는 방법, 9절 미확인 목록, 10절 스파이크 단계 목록, 11절 다른 문서 개정(PR (b)), 12절 ADR 0009 초안 요지, 승인 질문, 채택
- 여섯 스펙 공통: 스파이크 태그(`[S…]`·`[P:…]`·`[T…:…]`·`[TIMING]`)와 `[읽음]` 표시, 머리의 작성자·확인 커밋·확인 환경 줄

## sweeper가 지운 문서·스파이크·지시문 문장 (제목만)

### spikes/ (git rm, 이력에 있음)

- `spikes/cases-public/`: 공개 사례 20건 분류 실행 도구·결과·PROBE·회사 이름 검사
- `spikes/gap-14/` 중첩 구조 대조 합성 예제
- `spikes/gap-15/` 집합·순위 결과 대조 합성 예제
- `spikes/gap-16/` 표본 크기·신뢰구간(Wilson) 합성 예제
- `spikes/gap-17/` 기준 자료 신뢰도(평가자 두 벌) 합성 예제
- `spikes/gap-18/` 시간에 따른 저하(회차 3번) 합성 예제
- `spikes/gap-19/` 응답 시간·처리량 합성 예제
- `spikes/gap-20/` 오류 되돌림 합성 예제
- `spikes/gap-21/` 개인정보·규제 플래그 합성 예제
- `spikes/gap-22/` 단계 간 오류 전파 합성 예제
- `spikes/gap-23/` ③ 호출 비용 합성 예제
- `spikes/gap-24/` 입력 형식 변환(xlsx) 합성 예제
- `spikes/gap-25/` 케이스 간 비교 합성 예제
- `spikes/gap-runner/`: 빈 곳 실행 도구(make-gaps.py, run-gaps.sh, check-names.py, GapRunner)

### docs/cases-public

- gaps.md: 머리 글머리(재현·도구·사람 입력), 2~4절 빈 곳별 상세, 5절 회사 이름 검사, 6절 우선순위 제안(결론 표 1절만 남김)
- diff.md: 1절 답을 적은 방법, 3절 다른 곳 요약, 4절 새 빈 곳 A·후보 B, 5절 PROBE 표, 6절 1절 빈 곳 칸 대응, 7절 확인(결론 표 2절만 남김)
- README.md 3절: 잠재 빈 곳 12개 표(증상·확인 예제·구현 후보)와 "추가 후보(증거 약함)" 줄 → 두 줄 표로 바꿈

### docs/README-instructions.md

- 3.1 확인 글머리 "exe 화면은 없다(ADR 0010)."
- 3.1 글머리 "exe에는 대화형 문답이 없다(ADR 0010)."
- 3.2 "앞의 9개는 단계마다 묻는다." → "AI가 단계마다 체크리스트로 채운다"
- 6절 cli-dev 템플릿 description의 "번호 마법사, 확인 한 화면"
- 6절 cli-dev 템플릿 2항 번호 문답(Enter면 기본값) → 실제 cli-dev.md 2항 문장
- 10절 M2 행의 "exe 문답 없음,"
- 12.1 끝 "다르면 … 어떤 질문·규칙이 빠졌는지 ADR로 적고 합성 예제를 추가한다"(트리 확장)

### docs/spec 작은 스펙

- human-signoff.md 삭제(T-008 승인·스파이크·테스트 변경 기록. 규칙은 case-schema·classification에 있음)
- signoff-final.md 삭제(T-009 승인·스파이크·테스트 변경 기록. 블록은 classification·synthetic-examples·metrics 7.5)
- guard-tests.md: 텍스트 검사 비교표(V01~V18), 4.4 안 비교, G3·G2 가짜 대상 결과표, 6.2 PR 확인 절차, 7절 테스트 목록, 9절 스파이크 단계, 승인 질문 13개
- solution-skeleton.md: 패키지 목록, R3 GitHub 조회 기록, `cases/` 패턴 비교표, 9절 스파이크 단계, 승인 질문 10개·승인 결과(결과는 본문에 반영)
| 2026-11-08 | #23 | `--out` 검사가 글자 비교라 심볼릭 링크로 우회된다(링크로 연 케이스에 실제 하위 경로를 주면 expected 덮어씀). 경로 조각마다 링크를 풀어 비교 | src/Scorecard.Cli/CliApp.cs |
| 2026-11-08 | #23 | 빈 문자열 인자(`check ""`, `--out ""`)가 `internal`(종료 1). `argument` 오류(2)로 | src/Scorecard.Cli/CommandLine.cs |
| 2026-11-08 | #23 | "케이스 폴더 없음" 오류 문구가 명령마다 다르다(start·open vs check·compare·judge) | json-output.md 6.3 |
| 2026-11-08 | #23 | `--review`는 ADR 0010 표에서 M2-1인데 M2-3으로 미뤘다. M2-3 카드 수용 기준에 적는다 | ADR 0010, T-018 |
| 2026-11-08 | #23 | 기본 `out/` 덮어쓰기, 입력 사본·SHA-256 없음은 2절 2와 다르다. M2-3에서 다룬다 | 지시문 2절 2 |
| 2026-11-08 | #23 | G1 D1(시각·난수·환경 변수·현재 폴더)이 Core에만 걸린다. Cli에도 거는 카드 | tests/Guards |
| 2026-11-08 | #23 | 사람용 안내 문구의 문자열 보간: 숫자를 넣게 되면 InvariantCulture | CliApp.cs:122·135 |
| 2026-11-08 | #23 | ADR 0011 재사용이 이 클라우드 환경에서 안 된다. `gh run download`의 아티팩트 저장소(`productionresultssa*.blob.core.windows.net`)가 네트워크 정책에 막힌다. CI가 mutation 요약을 잡 로그에 찍게 하면 API로 읽을 수 있다 | tools/ci, ci.yml |
| 2026-11-08 | #24 | schema 1로 되돌리는 길이 없다. PR (b) 뒤 oracle schema 1 판을 지우면 옛 expected를 다시 검사할 수 없다(의도대로지만 되돌릴 때는 git 기록에서) | tools/oracle/classify.py |
| 2026-11-08 | #24 | 데이터 9절 번호가 9.6 → 9.8로 9.7이 비어 보인다(PR (b)가 9절을 옮기며 사라짐) | docs/spec/data/classification.md |
| 2026-11-08 | #24 | 스코어카드가 `source: ai`·`differs` 단계를 어떻게 보일지 M2-3에서 정한다 | report.md |
| 2026-11-08 | #24 | `classify.py` 주석의 절 번호가 9절 기준이다. PR (b) 뒤 첫 oracle 변경에서 고친다 | tools/oracle/classify.py |
| 2026-11-08 | #25 | 본문 4.1 "GATE는 SIGNOFF가 아닌 모든 잎 뒤에 붙는다"가 4.13 규칙 5(AI가 ④가 아니면 SIGNOFF 뒤에도 GATE)를 가리키지 않는다. "(AI 판단 예외는 4.13)"을 덧붙인다 | docs/spec/classification.md:52 |
| 2026-11-08 | #25 | form-fill `answers.yaml` 주석이 "→ 확인 항목"으로 schema 1 설명 그대로다(예제 입력 바이트라 SHA 줄과 같이 고쳐야 한다) | examples/form-fill/answers.yaml |
| 2026-11-08 | #25 | migration 커밋이 블록·SHA 줄 말고 데이터 9.6 머리·안내 줄도 지웠다. 옮기기의 결과라 동작 영향은 없지만 ADR 0004 "블록·SHA-256 줄만"과 글자는 다르다 | 7ca2005 |
| 2026-11-08 | #25 | 스킬 7절이 case.yaml 단계를 answers.yaml과 "같게 둔다"고만 한다. 두 파일 단계가 어긋나면 check와 start의 분류가 다를 수 있다. 한쪽에서 읽게 하거나 어긋나면 알린다 | .claude/skills/scorecard/SKILL.md:94 |
| 2026-11-08 | #25 | selftest.py 주석·라벨이 아직 9.x 절 번호다(classify.py와 같은 일, 다음 oracle 변경에서) | tools/oracle/selftest.py |
| 2026-11-08 | #25 | AiJudgmentTests의 블록 비교가 텍스트 비교다. 다른 골든처럼 바이트로 맞추면 실패 메시지가 일관된다 | tests/Scorecard.Tests/Classification/AiJudgmentTests.cs |
| 2026-11-08 | M2-3 | `inferred`·`current_actor`를 케이스 폴더 answers.yaml에서 다시 분류해 읽는다. `start --answers`가 다른 파일을 썼거나 뒤에 답이 바뀌면 classification.json과 어긋난다(번호·이름만 비교). 실제로 쓴 답 파일 해시도 남지 않는다 | src/Scorecard.Core/Report/ScorecardBuilder.cs |
| 2026-11-08 | M2-3 | classification.json 단계의 `rows`가 비면 IndexOutOfRange로 종료 1. `result` 오류(2)로(손으로 고친 파일만) | ScorecardBuilder.cs, ResultReader.cs |
| 2026-11-08 | M2-3 | 자료 패턴 읽기가 실패하면(overlap·파일 없음) 자료 파일 해시를 말없이 뺀다. 입력 기록이 빈 것을 표시 | ScorecardBuilder.cs |
| 2026-11-08 | M2-3 | html-check를 인자 없이 부르면 끝난다. 골든 6개 경로로 돌리는 명령을 카드나 스펙 6절에 적는다 | tools/html-check/check.mjs, report.md 6절 |
| 2026-11-08 | M2-3 | `change`가 지금 담당과 같은 담당을 적어도 받는다("④를 ④로 바꿨다" 위험). 막을지 스펙 5절에서 | src/Scorecard.Core/Report/ReviewParser.cs |
| 2026-11-08 | #27 | 스킬의 케이스 이름 규칙 "소문자·숫자·`-`"가 정규식 `^[a-z0-9]+(-[a-z0-9]+)*`보다 넓다(앞뒤 `-`, `--`). 틀리면 엔진이 막는다 | .claude/skills/scorecard/SKILL.md |
| 2026-11-08 | #27 | 스킬 6.1의 "AI 판단(`rows[0]`)"은 `source: rule` 단계에서는 트리 행이다. source를 보고 표시하라는 한 줄 | .claude/skills/scorecard/SKILL.md |
| 2026-11-08 | #27 | #24 행 "9.6 → 9.8로 9.7 빔"은 데이터 9절을 지워 해당이 없어졌다. 지운다 | docs/LATER.md |
| 2026-11-08 | #27 | 사용자 확인 여부가 파일에 남지 않는다. 확인 없이 쓴 `ai`도 엔진은 받는다(ADR 0013 남은 위험). 스킬 골든 테스트는 제목·명령 줄만 본다 | SKILL.md, ScorecardSkillTests.cs |
| 2026-11-08 | #27 | SIGNOFF + AI ③ + `failure_impact: external`(규칙 5)이 데이터 6.6 장면 표에는 있고 6.7 표본 파일에는 없다 | TestData/classification/ai-judgment/answers.yaml |
| 2026-11-08 | M2-4 | `changed`·`unsigned`일 때 사람이 서명한 `export-check.md`를 새 초안으로 덮어써 서명이 사라진다. 편집기가 끝 줄바꿈만 지워도 `changed`. 끝 빈 줄 차이 허용이나 원인 안내 | src/Scorecard.Cli/CliApp.cs |
| 2026-11-08 | M2-4 | answers.yaml 파싱 오류를 build가 알리지 않고 case.yaml 답으로 넘어간다(내보내는 내용과 확인 내용은 같아 거짓 통과는 아님) | src/Scorecard.Core/Build/BuildRequestBuilder.cs |
| 2026-11-08 | M2-4 | 확인 전·`changed`·`hits` 뒤에도 예전에 확인한 `build-request/`가 새 초안 옆에 남는다. 안내 문구 | docs/spec/build-request.md 4.5 |
| 2026-11-08 | #28 | check.mjs가 외부 요청을 세기만 하고 막지 않는다. context.route로 file: 밖은 abort | tools/html-check/check.mjs |
| 2026-11-08 | #28 | check.mjs 비교가 innerWidth 기준이다. 스크롤바가 보이는 환경을 위해 documentElement.clientWidth로. 안쪽 overflow-x 칸은 보지 않는다 | tools/html-check/check.mjs |
| 2026-11-08 | #28 | Core가 --review 상대 경로를 현재 폴더 기준으로 읽는다(Core는 현재 폴더를 읽지 않는다). Cli에서 Path.GetFullPath로 넘긴다 | src/Scorecard.Core/Report/ScorecardBuilder.cs, CliApp.cs |
| 2026-11-08 | #28 | json-output.md 11절 5 "open: 결과 파일 목록만 알린다"가 지금 동작과 다르다 | docs/spec/json-output.md |
