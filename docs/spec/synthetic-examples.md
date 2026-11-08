# synthetic-examples (합성 생성기와 공개 예제 파일)

- 지금 동작만 적는다. 표·예제 파일 블록·SHA-256 줄은 `docs/spec/data/synthetic-examples.md`에 있다. 절 번호가 같다. 테스트와 oracle이 그 파일을 읽는다.
- 결정 근거: ADR 0002·0003·0004·0005·0010, 지시문 2절 10(실제 자료 금지)·12절(예제). 바뀐 기록은 git 기록에 있다.
- 기준 자료 = `reference/`(사람이 만든 결과), 결과 자료 = `candidate/`(AI 적용 후 결과). 기준 행 = 기준 자료의 데이터 행.

## 1. 범위

- 공개 예제의 `reference/`·`candidate/` CSV를 만드는 합성 생성기(Core).
- 예제 폴더의 `case.yaml`·`intake.md`·CSV 파일.
- `examples/`의 실제 값 패턴 검사.
- 범위 밖: 대조 정의와 `expected/metrics.json`(`metrics.md`), 판정(`judgment.md`), 문장 결과 예제 reply-drafts(`text-check.md` 7절), 제작 의뢰서 `samples/`.

## 2. 입력

- `CaseDefinition` 하나(`examples/<예제>/case.yaml`). 생성기는 `inputs.key`와 `compare`(필드, `tolerance`·`tolerance_pct`)만 쓴다.
- `SyntheticPlan` 하나. 값의 범위와 심을 차이를 정한다. 공개 예제의 계획은 데이터 4.3·4.4 표다. 테스트가 표를 읽어 만든다. Core에는 예제 계획이 없다.
- 생성기는 파일을 읽지 않는다. `inputs`가 없으면 `ArgumentException`이다.

## 3. 출력

### 3.1 메모리 결과

- `SyntheticTables` 하나. CSV 파일 이름, 머리 행, 기준 행, 결과 행. 행마다 `Key`, `Values`, `Planted`가 있다.
- `Planted` 값 표는 데이터 3.1. CSV에 쓰지 않는다. 테스트가 심은 구조를 확인할 때 쓴다.

### 3.2 예제 폴더

- 예제마다 `answers.yaml`, `case.yaml`, `intake.md`, `reference/`·`candidate/`의 CSV, `expected/`(`classification.json`, `metrics.json`, `judgment.json`)가 있다.
- mixed-exceptions에는 `reference/`·`candidate/`가 없다(자료 없음 → 미검증). 빈 폴더를 두지 않는다.

### 3.3 CSV 바이트 규칙

- 데이터 3.3 표: ASCII, BOM 없음, LF, 끝 `\n`, `,`, 따옴표 없음, 빈 값 없음, `InvariantCulture`, 소수 자릿수 고정.
- 기준 행은 키 오름차순, 결과 행은 섞는다. 키로 맞춰야 대조된다.
- 같은 입력이면 같은 바이트다. 프로세스, 경로, 문화권과 상관없다. 시드가 다르면 바이트가 다르다.

## 4. 규칙

### 4.1 생성기 위치

- 생성기는 Core에 있다(`Scorecard.Core.Synthetic`). 판정에 쓰지 않는다. 합성 자료 만들기 전용이다.
- 명령은 없다. 테스트가 데이터 4.3·4.4 표와 `case.yaml`로 생성하고, 커밋된 CSV와 바이트를 비교한다. 테스트는 파일을 쓰지 않는다.
- 예제 CSV를 바꾸려면 계획 표를 고치고, 생성기 출력으로 데이터 7절 블록과 SHA-256을 고치고, 파일을 블록대로 옮긴다.

### 4.2 수열과 시드

- 수열은 SplitMix64다. 64비트 부호 없는 덧셈·곱셈·시프트만 쓴다(`unchecked`).
  - 상태 `s`에 `0x9E3779B97F4A7C15`를 더한다. `z = s`.
  - `z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9`, `z = (z ^ (z >> 27)) * 0x94D049BB133111EB`.
  - 값은 `z ^ (z >> 31)`.
- `NextBelow(n)`: `Next() × n`의 상위 64비트(`Math.BigMul`). 한 번에 `Next()`를 정확히 한 번 쓴다. 다시 뽑기가 없다.
- 확인 값(시드 0의 처음 세 값, 공개 API로 본 값)은 데이터 4.2.
- `System.Random`을 쓰지 않는다(G1 D1). 테스트도 수열을 따로 만들지 않고 Core를 쓴다.
- 시드는 계획 표의 "시드" 칸이다. `case.yaml`과 Core 상수에 넣지 않는다.
- 시드는 조건을 만족하는 가장 작은 값이다. table-compare `8`: 기준 30행 중 `irregular`가 12행. form-fill `0`: 선택지 5개가 모두 나옴. signoff-final `47`: 기준 20행 중 `unsteady`가 6행.

### 4.3 생성 계획 표

- 표는 데이터 4.3. 칸 뜻:
  - `파일`: `reference/`·`candidate/` 아래 CSV 이름. `inputs` 패턴에 맞는다.
  - `키 접두어`·`키 자릿수`: 키 = 접두어 + 행 번호(1부터, 앞을 0으로 채움).
  - `값 불일치 열`: 열마다 한 행을 허용 오차 밖으로 바꾼다. `compare` 필드여야 한다.
  - `누락`: 결과에서 뺄 기준 행 수. `추가`: 결과에만 있는 행 수(키는 기준 건수+1부터).
  - `허용오차 안 차이`: 그 열을 허용 오차의 절반 안에서만 바꾼 행 수. `tolerance_pct` 필드여야 한다.
  - `몰림`: 누락·값 불일치 행을 이 값을 가진 기준 행에서만 고른다.
  - `결과 섞기`: 결과 행 순서를 섞는다.

### 4.4 열 표

- 표는 데이터 4.4. 적힌 순서가 키 열 다음의 CSV 열 순서다.
- `정수`: 양 끝 포함. `소수`: 자릿수는 최소값의 자릿수다. `선택`: 적힌 순서가 뽑는 순서다.
- `compare`에 없는 열(`signal_type`, `record_kind`)도 둔다. 대조에 쓰이지 않고 몰림에 쓴다.

### 4.5 뽑는 순서

수열 하나를 이 순서로만 쓴다. 순서를 바꾸면 바이트가 바뀐다.

1. 기준 행. 행 1..기준 건수, 열 표 순서대로. 정수 `최소 + NextBelow(최대 − 최소 + 1)`. 소수는 자릿수 단위 정수로 뽑고 나눈다. 선택 `선택지[NextBelow(개수)]`.
2. 누락·값 불일치 행. 후보는 몰림 조건에 맞는 기준 행(오름차순). 앞쪽 부분 섞기(j = 0..k−1마다 `j + NextBelow(후보 수 − j)` 자리와 바꿈). k = 누락 + 값 불일치 열 수. 앞 `누락`개가 누락, 그다음이 값 불일치 열 순서의 행이다.
3. 허용오차 안 차이 행. 후보는 2에서 고르지 않은 기준 행. 같은 부분 섞기.
4. 값 불일치 값. 정수(`tolerance` t): 부호 `NextBelow(2)`, 크기 `t + 1 + NextBelow(20)`. 소수(`tolerance_pct` p): 부호, 배수 `2 + NextBelow(3)`, `기준 × (1 ± p × 배수 / 100)`을 반올림(0에서 먼 쪽). 선택: `(지금 위치 + 1 + NextBelow(개수 − 1)) % 개수`. 열 범위를 벗어나면 부호를 뒤집는다. 뒤집어도 안 되면 예외.
5. 허용 오차 안 값. 부호, `n = 1 + NextBelow(4)`, `기준 × (1 ± p × n / 1000)`을 반올림. 기준과 같거나 p/2를 넘으면 예외.
6. 추가 행. 값은 1과 같은 방식.
7. 결과 행. 기준 순서에서 누락을 빼고 추가 행을 뒤에 붙인다. 섞기면 Fisher–Yates(i = 끝..1마다 `NextBelow(i + 1)` 자리와 바꿈).

- 상수(정수 폭 20, 소수 배수 2~4, 허용 오차 안 0.1~0.4배)는 뽑는 순서의 일부다. 분류·판정 숫자가 아니다.
- 경계값은 심지 않는다. 허용오차 안 차이는 p/2 이하, 값 불일치는 2p 이상(정수는 t+1 이상)이다.

### 4.6 case.yaml과 계획 맞추기

- `compare` 필드는 모두 계획 열에 있어야 한다. 열 종류는 데이터 4.6 표와 맞아야 한다.
- 값 불일치 열은 `compare` 필드, 허용오차 안 차이 열은 `tolerance_pct` 필드다.
- 키 열 이름은 계획 열 이름과 겹치지 않는다.
- 맞지 않으면 `ArgumentException`이다.

### 4.7 대조 용어와 심은 구조

- 용어 표와 구조 표는 데이터 4.7. 정의는 `metrics.md` 4.4와 같다(항목 = 기준 행).
- 생성 파일을 키로 맞춰 센 값이 구조 표와 같다. 시드 0~1999 모두 생성에 성공하고 셈이 같다.
- table-compare: 불일치 3건(값 불일치 2 + 누락 1)을 모두 `signal_type = irregular` 행에 심었다. 그래서 판정이 "조건부"(몰림)다.
- form-fill: 모두 일치. signoff-final: 일치 16/20, 문턱(95, 2)에 못 미쳐 보류다.

### 4.8 case.yaml

- 예제별 `inputs`·`compare`·`thresholds`는 데이터 4.8 표다.
- `name`, `task`, `steps`는 `answers.yaml`과 같다. `answers.yaml`의 `name:` 줄부터 끝까지가 `case.yaml` 안에 바이트 그대로 들어 있다. 앞 주석 줄만 다르다.
- 예제는 시간 자료가 없어 `time_must_drop: false`다. `case.yaml`에 주석 한 줄로 적는다.
- `before`·`after`·`ai_usage`는 넣지 않는다.
- mixed-exceptions는 `inputs`·`compare`·`thresholds`가 없다.

### 4.9 intake.md

- 사용자의 자유 서술 예시다. 저장만 한다. 어떤 테스트도 내용을 해석하지 않는다.
- 한글 짧은 문장, 문단만. 업종·회사·사람 이름·코드 모양 값을 쓰지 않는다. 건수·빈도는 `answers.yaml`과 맞춘다.
- UTF-8, BOM 없음, LF, 끝 `\n`. 실제 값 패턴 검사 대상이다.

### 4.10 실제 값 패턴 검사

- 패턴 표는 데이터 4.10. 테스트가 `dotnet test`에서 돈다(CI의 build-test 잡).
- 범위: `examples/` 아래 모든 파일(NUL 바이트가 있는 파일은 뺀다). 줄마다, 그리고 저장소 기준 상대 경로에도 건다.
- 정규식은 `RegexOptions.CultureInvariant`, 시간 제한 1초. 칸의 백틱 안 글자가 정규식 그대로다. .NET `\w`는 한글도 포함한다.
- 잡는 예는 모두 걸리고 잡지 않는 예는 하나도 걸리지 않는지 먼저 본다.
- 오탐이면 패턴을 고치거나 예외 목록을 두지 않고 합성 값을 바꾼다.
- 실패 메시지에 걸린 것을 모두 보인다(`패턴 파일:줄: 값`).

### 4.11 G1·G2·G3

- 생성기는 `System.Random`·시각·환경 변수를 쓰지 않는다.
- G2·G3 대상 `synthetic-case-valid`(데이터 4.11 표). 고정 계획: 시드 7, `rows.csv`, 키 `r` 2자리, 12행. 열 `start` 정수 0~999, `mean` 소수 10.00~99.99, `label` 선택 a·b·c. 값 불일치 `start`·`mean`·`label`, 누락 1, 추가 1, 허용오차 안 차이 `mean` 2, 몰림 없음, 섞기.
- 소수를 현재 문화권으로 읽게 바뀌면 G2가 잡는다.

### 4.12 examples/ 관리

- `examples/.gitattributes`(`* -text`)가 모든 파일에 걸린다. `core.autocrlf=true` clone에서도 바이트가 같다.
- `Example_text_files_have_no_carriage_return`이 모든 텍스트 파일의 `\r`을 막는다.
- `Example_folder_has_exactly_the_planned_files`가 예제 폴더의 파일 목록을 고정한다.

### 4.13 Core 공개 API

- 네임스페이스 `Scorecard.Core.Synthetic`. 모양은 데이터 4.13 블록이다.
- 상수(4.5)는 `SyntheticGenerator` 안 `private const`다. 예제 CSV 이름은 계획 표에 있다.

## 5. 예외

- 경우별 결과는 데이터 5절 표. 메시지는 한글이고 어느 필드·값인지 적는다. 예외는 계획이나 `case.yaml`이 틀렸다는 뜻이다.

## 6. 예시

- 심은 행은 데이터 6절: table-compare(6.1), form-fill(6.2), signoff-final(6.4).

## 7. 예제 파일

- 파일 표, `case.yaml`·`intake.md`·CSV 블록, SHA-256 줄은 데이터 7절이다. 파일은 블록과 바이트까지 같다.
- 블록 순서는 파일 표 순서다.
