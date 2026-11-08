# build-request (제작 의뢰서 번들, M2-4)

- 상태: 스펙+구현 한 PR(ADR 0010 B-12). 결정은 추천안 채택(ADR 0005 결정 3), STATUS 잠정 결정 T-021 행. 작업 카드 `docs/tasks/T-021-m2-4-build-bundle.md`.
- 표·블록은 `docs/spec/data/build-request.md`(절 번호가 같다). 테스트가 읽는다.
- 원문 요구: 지시문 3.1 `build` 행, 3.6 번들 표, 2절 8(번들에 실제 자료 없음, 사람이 확인한 뒤에만 내보내기), 10절 M2 G2(end-to-end), ADR 0004 결정 3·5·6·7과 질문 15(사람 확인 줄은 사람이 적는다).

## 1. 범위

- `scorecard build <케이스> [--step <번호>] [--out <폴더>] [--json]`.
- `--step`이 없으면 먼저 만들 ② 단계를 추천만 한다. 아무것도 쓰지 않는다.
- `--step <번호>`면 그 단계의 번들을 만든다. 사람 확인(`export-check.md`)이 있을 때만 `build-request/`에 쓴다.
- 범위 밖(9절): 납품물 템플릿(`scorecard-factory-template`)과 가드 옮기기(M3), 확인 파일(`--review`)로 바뀐 담당 반영, 입력 자료 사본, 여러 단계 한 번에.

## 2. "이 단계 만들까요?" (묻지 않는다)

- exe는 묻지 않는다(ADR 0010 해석 1). 답은 인자로 받는다.
  - 예: `build <케이스> --step <번호>`.
  - 아니오·나중에: `--step` 없이 끝낸다. 아무것도 쓰지 않고 종료 0(ADR 0004 결정 7).
- 추천은 스코어카드 제안 카드와 같다(`report.md` 4.4, `ScorecardBuilder`의 `proposal`). 고를 수 있는 단계(`candidates`)는 단계 행 담당이 ② 또는 ②M인 단계, 번호 순이다.
- 추천 말고 다른 ② 단계를 골라도 된다. ②·②M이 아닌 단계는 `step` 오류(2)다. 실행 시 AI 없는 소프트웨어만 의뢰한다(지시문 1절).
- `--step` 값은 1 이상의 정수(`NumberStyles.None`)다. 아니면 `argument` 오류(2)이고 아무 파일도 읽지 않는다.

## 3. 출력

### 3.1 결과 객체 (`build --json`)

- 성공 표준 출력. 앞 세 키는 json-output.md 5.2와 같다(`schema_version` 1, `command` `build`, `files`). 뒤에 `case`, `step`(고른 번호, 없으면 null), `proposal`(스코어카드 값 그대로, 없으면 null), `candidates`(정수 배열)가 붙는다.
- 추천만 하면 `files`는 `[]`, 번들을 쓰면 출력 폴더 기준 경로(`build-request/…`, 3.2 순서)다.
- 바이트 규칙은 classification.md 3.3과 같다. Core(`BuildRequestJson.Result`)가 쓴다. 예는 데이터 3.1 블록이다.

### 3.2 번들 (`<출력 폴더>/build-request/`)

- 지시문 3.6은 `cases/<case>/build-request/`라고 적는다. 출력은 늘 출력 폴더(기본 `<케이스>/out`, json-output.md 4절)에 쓰므로 `<케이스>/out/build-request/`다. 케이스 폴더 안의 입력을 건드리지 않는다(2절 2).
- 파일과 순서는 데이터 3.2 표다: `spec.md`, `io-schema.json`, `acceptance.md`, `samples/README.md`, `samples/output.csv`(출력 열이 있을 때만), `context.json`, `export-check.md`.
- 바이트는 Core(`BuildRequestBuilder.Create`)가 모두 만든다. Cli는 쓰기만 한다. `export-check.md`만 사람이 확인 줄을 적은 파일 그대로다.
- 글 파일은 UTF-8(BOM 없음), LF, 끝 `\n` 하나. 줄을 `"\n"`으로만 잇는다(여러 줄 원시 문자열을 쓰지 않는다. Windows 체크아웃 CRLF와 상관없이 같은 바이트).
- JSON 파일은 classification.md 3.3 바이트 규칙, 첫 키 `schema_version` 1(새 문서, ADR 0008 결정 1).

## 4. 규칙

### 4.1 무엇을 읽나

- `open`과 같다(`ScorecardBuilder.ReadDirectory`, `report.md` 2절): 출력 폴더의 결과 파일, 케이스 폴더의 `case.yaml`·`answers.yaml`·`intake.md`. 확인 파일은 읽지 않는다.
- 스코어카드 결과 객체(`ScorecardBuilder.Build`)를 만들고 그 값을 옮긴다. 다시 분류·대조·판정하지 않는다.
- 고른 단계의 답: `answers.yaml`, 단계가 맞지 않으면 `case.yaml`(단계 수·번호·이름이 classification.json과 같은 것). 둘 다 아니면 답 칸은 null·"답이 없다"다.
- `case.yaml`이 있는데 파싱 오류면 그 오류를 그대로 낸다(2). 없으면 출력 열이 없는 번들이다.
- 자료 파일(`reference/`·`candidate/`)의 값은 번들에 넣지 않는다. 열 이름은 `case.yaml`에서만 온다. 숫자는 셈·퍼센트·문턱(집계값)만 간다(2절 8).

### 4.2 `spec.md`, `io-schema.json`

- `spec.md`: 머리, 입력 → 처리 → 출력, 규칙 초안, 예외 처리, 사람 게이트, 실행 조건. 절 제목은 데이터 4.2 첫 표다.
- 근거 문장·담당·규칙 의견·기본값·추정 키는 결과 객체 값 그대로다.
- 규칙 초안은 `compare` 필드마다 한 줄(허용 오차 또는 글자 비교), 체크리스트가 있으면 문장 열 한 줄이다.
- `io-schema.json`: `case`, `step`, `input.kind`(답의 `input_kind`), `output.key`, `output.columns`. 열 순서·역할·형식은 데이터 4.2 둘째 표다. 숫자 형식은 허용 오차(`tolerance`·`tolerance_pct`)가 있는 `compare` 필드다.

### 4.3 `samples/`

- `samples/output.csv`: `io-schema.json` 출력 열로 만든 합성 10행. 값 식은 데이터 4.3 표다. 난수가 없다. 실제 값이 없다.
- 머리 행 이름은 `case.yaml` 글자 그대로이고, `,`·`"`·줄바꿈·앞뒤 공백이 있을 때만 RFC 4180 따옴표로 감싼다. 값은 ASCII다.
- 합성 생성기(`SyntheticCsv`)는 머리 행도 ASCII만 받아 쓰지 않는다(한글 열 이름).
- 출력 열이 없으면(`inputs` 없음) CSV를 만들지 않고 `samples/README.md`에 그 이유를 적는다.

### 4.4 `acceptance.md`, `context.json`

- `acceptance.md`: 기준 숫자(judgment.json `thresholds`: 정확도 이상, 누락률 이하, 시간 조건), 지금 검증 값, 테스트 시나리오, 제작 게이트 세 질문 표(데이터 4.4, ADR 0004 결정 5).
- `thresholds`가 없으면(check 전) "기준 숫자가 아직 없다"다.
- `context.json`: 고른 단계의 답 집계값(`frequency`, `batch_size`, `exception_rate`, `minutes_per_item` 등), 스코어카드 요약 셈, 검증 셈·퍼센트·판정, 문턱. 모양은 데이터 4.4 블록이다.

### 4.5 `export-check.md`와 사람 확인

- 초안: 머리, 번들 파일 SHA-256 표(3.2 순서, `export-check.md` 뺌), 문자열 검사 결과, 확인 문장, 마지막 줄 `확인한 사람:`.
- 문자열 검사: 번들 파일(`export-check.md` 뺌)의 줄마다 패턴 아홉. 패턴은 `synthetic-examples.md` 데이터 4.10 표와 같다(이메일·URL·경로 넷·품번 셋). 순서는 파일 → 줄 → 패턴 → 줄 안 위치.
- 사람 확인: 사람이 출력 폴더의 `export-check.md`를 읽고 `확인한 사람:` 뒤에 이름이나 역할을 적은 뒤 같은 명령을 다시 돌린다. 스킬·AI는 이 줄을 적지 않는다(ADR 0004 질문 15).
- 판단(`BuildRequestBuilder.CheckExport`, 상태 표는 데이터 4.5):
  - 걸린 문자열이 하나라도 있으면 `hits`. 확인 줄과 상관없이 내보내지 않는다(2절 8).
  - 파일이 없으면 `missing`.
  - `확인한 사람:`으로 시작하는 줄이 꼭 하나가 아니거나, 그 줄 값을 비운 글이 지금 초안과 다르면 `changed`. 번들이 바뀌면(답·단계·결과·판) SHA-256 표가 달라져 옛 확인이 무효다.
  - 값이 비면 `unsigned`, 있으면 `confirmed`.
- 비교 전에 앞 BOM 하나를 떼고 CRLF·CR을 LF로 맞춘다. 줄 끝 공백은 확인 줄 값에서만 뗀다.
- `confirmed`가 아니면: 출력 폴더에 새 초안 `export-check.md`를 쓰고(덮어씀) `export-check` 오류(2)로 끝난다. `build-request/`는 만들지도 바꾸지도 않는다.
  - json-output.md 4절 "오류면 아무것도 쓰지 않는다"의 예외다. 사람이 읽을 초안이 이 오류의 결과물이다.
- `confirmed`면 `build-request/`에 번들과 사람이 적은 `export-check.md`(받은 바이트 그대로)를 쓴다.

### 4.6 오류

- 모두 입력 오류(2). 표는 데이터 4.6이다.
- Cli: `argument`(`--step` 값), `file`(케이스 폴더 없음, classification.json 없음 = `open`과 같은 문구), `output`.
- Core: `step`(없는 단계, ②·②M 아님. 경로 `--step`), `export-check`(4.5, 출처 `export-check.md`), `case.yaml` 파싱 오류, 결과 파일 `result` 오류(`report.md` 7절).
- `build-request/`에 번들 밖 파일이 있으면(이전 판의 `samples/output.csv` 등) `output` 오류다. 지우지 않는다. 사람이 폴더를 비운다. 확인하지 않은 파일이 섞여 나가지 않게 한다.

### 4.7 결정론·원본 불변

- 같은 입력(케이스 폴더, 결과 파일, `export-check.md`)이면 같은 바이트다. 시각·난수·경로·문화권과 상관없다. 숫자는 `InvariantCulture`.
- 케이스 폴더는 읽기만 한다. 쓰기는 출력 폴더에만 한다.
- Core는 파일을 읽지 않는다(`Create`·`CheckExport`·`Scan`은 바이트만 받는다). Cli가 `export-check.md`를 읽고 쓴다.

## 5. 명령 순서

1. `--step` 값 → 케이스 폴더 → 출력 폴더 확인(json-output.md 4절) → 읽기(4.1) → 결과 객체.
2. `--step`이 없으면 결과 객체(3.1)와 사람용 추천 안내. 끝(0).
3. `Create` → `step` 오류면 끝(2, 아무것도 쓰지 않음).
4. 출력 폴더의 `export-check.md`를 읽고 `CheckExport`. 확인 전이면 초안 쓰고 끝(2).
5. 확인이면 번들 밖 파일 검사 → `build-request/` 쓰기 → 결과 객체(0).

## 6. G2 end-to-end와 G2·G3 대상

- end-to-end(지시문 10절 M2 G2): 공개 예제 5종마다 `start --answers` → `check` → `open` → `build` → `build --step <제안>`(확인 전, 2) → 확인 줄 적기 → `build --step <제안>`(0). 사람용과 `--json` 둘 다 돌리고 출력 파일이 같다.
- 골든: 예제마다 `tests/Scorecard.Tests/TestData/build/<예제>/`에 `choice.json`(추천 `--json`), `build.json`(번들 `--json`), `build-request/` 파일. 다시 쓸 때만 `SCORECARD_UPDATE_GOLDEN=1`. `examples/*/expected`가 아니다.
- G2·G3 대상 `cli-build-case-valid`(샘플 `case-valid`): 데이터 6절 표.

## 7. 테스트

- `tests/Scorecard.Tests/Build/`, Trait `Spec` = `build-request`.
- 골든 end-to-end(6절), 데이터 표(번들 파일 순서, 열 역할, 샘플 값, 확인 상태 문구, 오류, 예제 표, 게이트 세 질문), 패턴 = `synthetic-examples.md` 4.10 표, 결정론(다른 경로·문화권), 입력 불변, 걸린 문자열이면 내보내지 않음, 확인 뒤 번들 바뀜이면 `changed`, 번들 밖 파일.
- 공개 예제 번들에 패턴 걸림이 0이다.

## 8. 공개 API

- 네임스페이스 `Scorecard.Core.Build`. 모양은 데이터 8절 블록이다.

## 9. 뒤로 미룬 것 (`docs/LATER.md` "M2-4(T-021)에서 미룬 것")

- 가드(네트워크·LLM·외부 프로세스 금지)를 납품물 템플릿 검사로 옮기는 일. 템플릿이 M3라 `src/` 가드는 그대로다(STATUS 잠정 결정 T-021).
- `--review`로 바꾼 담당을 build가 따르는 일. 지금은 결과 객체의 단계 행 담당만 본다.
- 입력 쪽 합성 샘플(입력 형태별). 지금은 출력 열 샘플만이다.
- 스킬 `/scorecard`의 build 안내 문구(`.claude/skills/`, ADR 필요)와 README 샘플 스코어카드 게시.

## 10. 이 카드의 선택 (추천안 채택, STATUS 잠정 결정 T-021)

1. 예/아니오/나중에: 예 = `--step <번호>`, 아니오·나중에 = `--step` 없이 끝(0, 아무것도 안 씀).
2. 번들 자리: `<출력 폴더>/build-request/`(기본 `<케이스>/out/build-request/`).
3. 사람 확인: 출력 폴더의 `export-check.md` 마지막 줄 `확인한 사람:`에 사람이 적고 다시 돌린다. 초안과 다른 글이면 무효.
4. 확인 전 실행은 초안만 쓰고 종료 2(오류여도 초안은 쓴다).
5. 문자열 검사에 걸리면 확인과 상관없이 내보내지 않는다.
6. 고를 수 있는 단계는 ②·②M. 추천은 스코어카드 제안 그대로.
7. 합성 샘플은 출력 열 10행, 정해진 식(난수 없음). `SyntheticCsv`를 쓰지 않는다(한글 열 이름).
8. 열 이름·허용 오차는 `case.yaml`만. 자료 파일 값은 읽지 않는다.
9. `build-request/`에 번들 밖 파일이 있으면 `output` 오류.
10. G2·G3 대상 `cli-build-case-valid`, end-to-end 골든은 tests TestData.
11. Cli 오류 코드 `unavailable`을 뺐다(쓰는 곳이 없다).
