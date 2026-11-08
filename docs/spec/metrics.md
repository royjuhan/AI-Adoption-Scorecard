# metrics (대조와 지표)

- 지금 동작만 적는다. 표·JSON 블록·예시 파일·SHA-256 줄은 `docs/spec/data/metrics.md`에 있다. 절 번호가 같다. 테스트와 oracle이 그 파일을 읽는다.
- 결정 근거: ADR 0004(`schema_version`, `--json`, 종료 코드), ADR 0005·0008(`schema_version` 기준)·0009(문장 결과)·0010. 바뀐 기록은 git 기록에 있다.
- 기준 자료 = `reference/`(사람이 만든 결과), 결과 자료 = `candidate/`(AI 적용 후 결과). 기준 행 = 기준 자료의 데이터 행.
- 이 문서는 대조와 지표만 정한다. 판정은 `judgment.md`, 체크리스트·점수표는 `text-check.md`다.

## 1. 범위

- `inputs` 파일 찾기, CSV 읽기, 키 맞추기, 허용 오차, 정확도·누락률, 오류, metrics.json.
- 범위 밖: 판정과 문턱, 시간 지표(`measure`), 명령 인자와 화면 문구(M2), 입력 SHA-256(스코어카드), CSV가 아닌 형식.

## 2. 입력

- `CaseDefinition` 하나. 쓰는 것은 `Name`, `Inputs`(`Reference`, `Candidate`, `Key`, `Group`), `Compare`다.
- `inputs`가 없거나 `compare`가 비면 파일을 읽지 않고 미검증이다(4.4). 케이스 폴더가 없어도 오류가 아니다.
- 파일은 두 길로 받는다. `CompareDirectory` = `CompareInputs.Read`(파일 찾기·읽기) + `Compare`(순수 함수, 바이트). 테스트는 메모리 바이트로 `Compare`를 부른다.
- 읽기만 한다. 쓰는 API를 쓰지 않는다. 넘긴 바이트 배열을 바꾸지 않는다.
- 파일 경로는 케이스 폴더 기준 상대 경로(`/` 구분)로만 다룬다. 절대 경로를 결과·오류에 넣지 않는다.
- Core는 현재 폴더를 읽지 않는다. 부르는 쪽이 전체 경로를 넘긴다.

## 3. 출력

### 3.1 메모리 결과

- `CompareResult(Metrics, Errors)` 하나. 오류가 하나라도 있으면 `Metrics`는 null이다.
- `CompareMetrics`는 3.2와 같은 내용이다. 다른 점: `Items`에 모든 항목(차이 없는 일치 포함)이 있고, 항목의 `CsvRow`가 그 행의 머리 행·칸 전부(정규화 뒤)를 가진다. 판정이 유형 열을 여기서 읽는다.
- 항목 순서: 기준 항목(기준 파일 경로 서수 → 줄), 그다음 결과 쪽 항목(`extra`·`duplicate`, 결과 파일 경로 서수 → 줄).

### 3.2 metrics.json

- Core가 바이트를 만든다(`MetricsJson.Render`). 모양 블록과 속성 표는 데이터 3.2다. 실제 바이트는 배열 원소도 한 줄에 하나다.
- 첫 키는 정수 `"schema_version": 1`이다. 올리는 기준은 ADR 0008 결정 1이다. 올리는 카드는 expected를 같이 고친다(ADR 0004 expected 변경 규칙).
- 지표는 `compare` 아래에 둔다. 체크리스트·점수표 속성은 설정했을 때만 있다(`text-check.md` 3.1).
- `items`에 쓰는 항목: 값 불일치, 누락, 추가, 중복, 그리고 허용오차 안 차이가 있는 일치. 차이 없는 일치는 쓰지 않는다. 같은 자료면 `items`가 `[]`다.
- 칸 값은 정규화한 텍스트 문자열이다. 숫자로 바꿔 쓰지 않는다(`1e3`은 `1e3`).
- 쓰지 않는 것: 절대 경로, 입력 해시, 정규화 전 원문, `compare`에 없는 열, 차이 없는 일치, 오류.
- 미검증 모양: `status`·`reason`만 값이 있고 `key`·`counts`·퍼센트는 null, 배열은 `[]`다(데이터 7.4).

### 3.3 바이트 규칙

- `classification.md` 3.3과 같다: `Utf8JsonWriter`, 2칸, LF, 끝 `\n`, UTF-8(BOM 없음), `UnsafeRelaxedJsonEscaping`.
- `schema_version`은 `WriteNumber(int)`, 그 밖의 숫자는 `decimal` 그대로다. 자릿수가 남는다(`90.0`, `1.50`, `0`). 퍼센트는 늘 소수 한 자리다.
- 같은 입력이면 같은 바이트다. 프로세스, 경로, 문화권, `CompareSources`의 파일 순서와 상관없다. CSV의 줄바꿈·BOM·따옴표·끝 줄바꿈·칸 앞뒤 공백 변형도 같은 바이트다.
- 결과 행 순서를 바꾸면 셈은 같고 줄 번호가 바뀐다.

### 3.4 명령과 AI용 입구

- 사용자 명령은 `scorecard check <case>`다. 대조는 그 안의 내부 명령 `compare`가 한다.
- `compare --json`의 표준 출력은 그 케이스의 metrics.json과 바이트까지 같다(봉투 없음). 그래서 데이터 7절 블록이 AI용 입구의 정답이다.
- 종료 코드: 0은 `measured`·미검증. 2는 4.7 오류가 하나라도 있을 때. 1은 예상하지 못한 예외(부르는 쪽이 바꾼다).
- 오류 JSON의 `errors` 원소는 `CaseError` 여섯 값(`code`, `source`, `line`, `column`, `path`, `message`)이다. 순서는 4.7 순서다.
- `source`는 케이스 폴더 기준 상대 경로다. 파일이 정해지기 전 오류는 `case.yaml`이다. CSV 오류는 `reference/a.csv`처럼 폴더를 붙인다.
- `path`는 필드 경로다. CSV 오류는 열 이름, `inputs` 오류는 `inputs.reference`·`inputs.candidate`, 열과 상관없으면 빈 문자열이다. `inputs` 오류의 줄·열은 0이다.

## 4. 규칙

### 4.1 파일 찾기 (`inputs` 패턴)

- 패턴은 `/`로 나눈 조각이다. 폴더 조각은 글자 그대로, 마지막 조각에만 `*`(0자 이상)·`?`(UTF-16 한 단위)를 쓴다.
- 받지 않는 것: 빈 조각, `.` 조각, 폴더 조각의 와일드카드, `**`, `[` `]` `{` `}` → `pattern` 오류. 표는 데이터 4.1.
- 이름 비교는 서수다(대소문자 구분). OS 검색 패턴을 쓰지 않고 이름 목록을 받아 직접 맞춘다. 폴더 조각도 서수로 찾는다.
- 하위 폴더는 보지 않는다. `.`·`~$`로 시작하는 파일도 이름이 맞으면 읽는다.
- 찾은 파일은 이름 서수 순이다. 맞는 파일이 없으면 `file` `일치하는 파일이 없다`, 폴더가 없으면 `폴더가 없다: <폴더>`.
- 여러 파일: 한쪽 데이터 행을 모두 합친다(경로 서수 → 줄). 파일마다 머리 행이 있고 열 순서는 달라도 된다. 키 중복은 쪽 전체에서 본다. 같은 파일이 양쪽에 나오면 `overlap`이다.

### 4.2 CSV 읽기

- 규칙 표는 데이터 4.2: UTF-8만(BOM 하나 뗌), LF·CRLF·CR, 구분자 `,`만, RFC 4180 따옴표, 빈 행 건너뜀, 칸 수 검사, 줄·열 번호(UTF-16 단위, 1부터).
- 따옴표로 시작하지 않는 칸 안의 `"`는 글자 그대로다.
- BOM이 두 번이면 둘째 BOM이 첫 열 이름에 붙어 `column` 오류다. BOM 없는 UTF-16은 `column` 오류로 드러난다.
- 결과 자료에 머리 행만 있으면 오류가 아니다. 모든 항목이 누락이다. 기준 자료에 데이터 행이 없으면 `no-rows` 오류다.
- 크기 상한은 없다.

### 4.3 키

- 키 열은 머리 행에서 `inputs.key`와 같은 이름(정규화 뒤 서수)이다. 없으면 `column`(양쪽).
- 키 값은 정규화(4.6) 뒤 서수로 비교한다. 대소문자, NFC·NFD, `1`과 `1.0`은 다른 키다. 다른 키면 누락 1 + 추가 1이다.
- 빈 키는 `empty-key` 오류다(양쪽).
- 기준 쪽에 같은 키가 두 번 이상이면 둘째부터 `duplicate-key` 오류다. 메시지에 처음 위치를 쓴다. 오류 행도 키는 등록한다.
- 결과 쪽에 같은 키(모든 결과 파일을 합쳐서)가 두 번 이상이면 오류가 아니다. 첫 행(경로 서수 → 줄)이 대표 행이고 둘째 행부터 하나씩 항목 `duplicate`다.
  - 기준에 있는 키: 그 기준 항목은 값과 상관없이 값 불일치(`mismatch`)다. 기준 항목에는 대표 행의 위치와 차이를 적는다.
  - `duplicate` 항목: `reference` = 그 키의 기준 행, `candidate` = 그 행, `differences` = 그 행과 기준 행의 차이(4.5).
  - 기준에 없는 키: 대표 행은 `extra`, 둘째 행부터 `duplicate`(`reference` null, `differences` `[]`).
  - 셈은 줄 순서와 상관없다. 줄 순서는 줄 번호와 대표 행만 바꾼다(데이터 13.3 표).
- 예시 `compare-duplicate`는 데이터 12.5다.

### 4.4 항목과 셈

- 용어: 항목 = 기준 행 하나. 일치 = 키가 결과에 있고 `compare` 필드가 모두 같거나 허용 오차 안. 값 불일치 = 필드 하나 이상이 `outside`·`not_number`, 또는 결과 쪽 중복 키. 누락 = 기준 키가 결과에 없음. 추가 = 결과 키가 기준에 없음(대표 행). 중복 = 결과 쪽 같은 키의 둘째 이후 행. 허용오차 안 차이 = 일치이지만 값이 기준과 다름.
- 정확도 = 일치 / 기준 행 × 100. 누락률 = 누락 / 기준 행 × 100. 반올림은 4.8.
- 추가·중복은 분모에 들지 않는다. 판정이 검토 항목으로 쓴다.
- `counts.candidate` = 일치 + 값 불일치 + 추가 + 중복 행.
- 필드별 셈: `compared` = 기준에 키가 있는 결과 행 수(중복 행 포함). `outside`·`within`·`not_number`는 그 행들의 칸 수다. `counts.within`은 결과 쪽 중복 키를 세지 않는다.
- `compare`에 없는 열은 비교하지 않는다. 메모리 결과에는 남는다.
- 미검증 표는 데이터 4.4: `inputs` 없음 → `no_inputs`, `compare`가 빔 → `no_compare_fields`. 둘 다 파일을 읽지 않는다. 체크리스트가 있으면 `compare`가 비어도 대조한다(`text-check.md` 4.2).
- 패턴에 맞는 파일이 없거나 기준 데이터 행이 0이면 미검증이 아니라 오류다.

### 4.5 숫자와 허용 오차

- 숫자 필드: `tolerance` 또는 `tolerance_pct`가 있는 `compare` 필드. `tolerance: 0`도 숫자 필드다.
- 숫자 읽기: 정규화 뒤 `decimal.TryParse(칸, AllowLeadingSign | AllowDecimalPoint | AllowExponent, InvariantCulture)`. 숫자 모양 표는 데이터 4.5.
- 기준 칸이 숫자가 아니면 `number` 오류다(빈 칸 포함). 결과 칸이 숫자가 아니면 칸 결과 `not_number`이고 값 불일치다.
- 차이 d = |결과 − 기준|(decimal). d = 0이면 같음.
- 절대: d ≤ `tolerance`이면 `within`, 아니면 `outside`.
- 백분율: 기준 값 기준. d × 100 ≤ |기준| × `tolerance_pct`이면 `within`. 나눗셈을 쓰지 않는다. 기준이 0이면 같은 값만 통과한다.
- 경계는 포함(≤)이다. 칸 판정 표는 데이터 4.5.
- 넘침: d가 넘치면 `outside`. d × 100만 넘치면 `outside`, |기준| × `tolerance_pct`만 넘치면 `within`, 둘 다 넘치면 d ≤ |기준| × (`tolerance_pct` / 100). 예외가 새지 않는다.

### 4.6 텍스트 정규화

- 모든 칸(머리 행, 키, 숫자 칸, 텍스트 칸)에 같은 정규화를 한 번 한다. 그 뒤 비교는 서수다.
- 한다: 칸 안의 CRLF·CR → LF, 앞뒤 공백(`char.IsWhiteSpace`) 떼기. 앞뒤 글자 표는 데이터 4.6.
- 하지 않는다: 대소문자, 유니코드 정규화, 안쪽 공백 합치기, 전각→반각, 숫자 모양 맞추기.
- 허용 오차가 없는 `compare` 필드는 텍스트 비교다. 다르면 `outside`다.

### 4.7 오류

- 오류 하나는 `CaseError(Source, Line, Column, Code, Path, Message)`다. 문자열 모양은 `case-schema.md` 4.9와 같다. 코드 13개 표는 데이터 4.7.
- `Message`의 값은 제어 문자를 `\uXXXX`로 쓰고, 64자를 넘으면 자르고 `…`를 붙인다.
- 오류는 모두 모은다. 단 한 파일에서 `encoding`·`empty`·`csv-syntax`가 나면 그 파일의 나머지는 보지 않는다. 머리 행에 `column`·`duplicate-column`이 있으면 그 파일의 데이터 행은 보지 않는다.
- 오류가 하나라도 있으면 지표를 내지 않는다.
- 순서: 기준 쪽 → 결과 쪽. 쪽 안에서 파일이 정해지기 전 오류 → 파일 경로 서수 → (줄, 열) → 찾은 순서.
- 결과 자료에 `compare` 필드 열이 없으면 `column` 오류다. 쓰지 않는 열의 중복은 오류가 아니다. 머리 행 이름은 앞뒤 공백을 떼고 대소문자는 구분한다.
- `inputs.group` 열은 기준 파일마다 머리 행에 꼭 한 번 있어야 한다(`column`·`duplicate-column`). 찾는 순서는 키 열 → `compare` 필드 → group 열이다. 결과 파일은 보지 않는다. 그 열의 값은 비교하지 않는다.
- 오류 수 상한은 없다.

### 4.8 퍼센트

- 소수 첫째 자리, 둘째 자리가 5 이상이면 올린다. 정수로 셈한다: `tenths = (2 × part × 1000 + whole) div (2 × whole)`, 퍼센트 = `tenths / 10`.
- 늘 소수 한 자리다(`90.0`, `100.0`, `0.0`). 표는 데이터 4.8.
- 99.95% 이상은 `100.0`으로 보인다. 그래서 판정은 퍼센트가 아니라 셈으로 문턱과 비교한다.

### 4.9 값을 어디에 두나

- 오류 코드, 퍼센트 자릿수, 숫자 모양, 정규화, 메시지 값 표시는 Core `CompareRules.cs` 한 파일(`internal`)에 둔다.
- 허용 오차 값은 `case.yaml`의 `compare`에서 읽는다. 코드에 기본 허용 오차가 없다.
- 실행 중에 문서를 읽지 않는다. 테스트가 데이터 파일의 표를 읽어 Core와 대조한다.

### 4.10 결정론·원본 불변·G1·G2·G3

- `Compare`는 순수 함수다. 비교는 서수, 숫자는 decimal·`InvariantCulture`. 파일 목록은 서수로 정렬한다. `Dictionary`·`HashSet`은 찾기에만 쓴다.
- G2·G3 대상(데이터 4.10 표): `compare-case-valid`(`case-valid` → `metrics.json`), `compare-invalid`(오류 줄 → `compare-errors.txt`).
- `case-valid`의 두 CSV는 합성 생성기의 고정 계획으로 만든 바이트다(데이터 4.10 글머리).

### 4.11 Core 공개 API

- 네임스페이스 `Scorecard.Core.Comparison`. 모양은 데이터 4.11 블록이다.
- `IsSupportedPattern`·`Matches`는 테스트가 4.1 표를 직접 대조하려고 공개한다.
- Core 파일: `ComparisonModel.cs`, `CompareRules.cs`, `CsvText.cs`, `CompareInputs.cs`, `CompareEngine.cs`, `MetricsJson.cs`.

## 5. 예외

- 경우별 결과는 데이터 5절 표. `definition`·`sources`·`inputs`가 null이면 `ArgumentNullException`, `caseDirectory`가 null·빈 문자열이면 `ArgumentException`이다.

## 6. 예시

- table-compare 차이(6.1), 오류 표본 `compare-invalid`(6.4)는 데이터 6절이다. 6.4 블록은 `TestData/guard/compare-invalid/` 파일과 같고, 오류 블록은 엔진이 내는 줄 그대로다.

## 7. 예제 4종 `expected/metrics.json`

- 예제 표, 블록, SHA-256 줄은 데이터 7절이다. 파일은 블록과 바이트까지 같다.
- 정답은 엔진 출력이 아니다. 블록이 먼저이고 Core가 맞춘다. oracle(`tools/oracle/compare.py`)이 같은 바이트를 낸다.
- `expected/` 변경은 ADR 0004의 expected 변경 규칙을 따른다.
