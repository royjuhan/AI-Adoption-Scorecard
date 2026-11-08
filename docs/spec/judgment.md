# judgment (판정)

- 지금 동작만 적는다. 표·JSON 블록·예시·SHA-256 줄은 `docs/spec/data/judgment.md`에 있다. 절 번호가 같다. 테스트와 oracle이 그 파일을 읽는다.
- 결정 근거: 지시문 3.4, ADR 0004(`schema_version`, `--json`, 종료 코드), ADR 0005·0008(`schema_version` 기준)·0009(점수 확인)·0010. 바뀐 기록은 git 기록에 있다.
- 판정 값: `pass`(통과), `conditional`(조건부), `hold`(보류), `not_verified`(미검증).
- 불일치 = 일치가 아닌 기준 항목(값 불일치 + 누락, `metrics.md` 4.4).

## 1. 범위

- 판정, 근거 키, 판정 문장, 문턱과 기본값, 확인 다섯, 몰림, 검토 항목, 시간 비교, 다음 할 일, judgment.json.
- 점수표가 있을 때의 확인 `SCORE`와 판정 행은 `text-check.md` 5절이 더한다.
- 범위 밖: 시간 재기와 `times.yaml`(M2), 검토 화면과 `review.yaml`(M2), 명령 인자와 화면 문구, 스코어카드 HTML, 재작업률·완료당 비용, AI 과잉 후보(`classification.md` 4.8).

## 2. 입력

- `CaseDefinition` 하나. 쓰는 것: `Name`, `Inputs`(`Key`, `Group`), `Compare`, `Thresholds`(`AccuracyMin`, `MissingMax`, `TimeMustDrop`, `ScoreGapMax`).
- `CompareMetrics` 하나(`metrics.md` 3.1). 쓰는 것: `Status`, `Reason`, `Counts`, 퍼센트, `Items`(상태와 기준 행 `CsvRow`). 대조 오류가 있으면 판정하지 않는다.
- 시간 `JudgmentTimes`(선택). 건당 분. 적용 전, 처리, 검토. 재지 않았으면 null.
- 사람 입력 "설명 안 되는 검토 항목 키"(선택). null = 아직 보지 않음, 빈 목록 = 모두 설명함.
- 파일을 읽지 않는다. metrics.json을 다시 읽지 않고 `CompareMetrics`를 그대로 받는다. 유형 열 값은 메모리 `CsvRow`에만 있다.

## 3. 출력

### 3.1 메모리 결과

- `JudgmentResult` 하나(4.13). 판정, 근거 키, 판정 문장, 쓴 문턱, 기본값을 쓴 문턱 키, 확인(미검증이면 없음), 다음 할 일.

### 3.2 judgment.json

- Core가 바이트를 만든다(`JudgmentJson.Render`). 모양 블록과 속성 표는 데이터 3.2다. 실제 바이트는 배열 원소도 한 줄에 하나다.
- 첫 키는 정수 `"schema_version": 2`다. 올리는 기준은 ADR 0008 결정 1이다.
- 쓰지 않는 것: 절대 경로, 입력 해시, 대조 셈(metrics.json에 있다), 시간 원본, 사람 입력 원본. 숫자는 문장 안에 글자로 들어간다.

### 3.3 바이트 규칙

- `classification.md` 3.3과 같다. `schema_version`은 `WriteNumber(int)`, 문턱은 `decimal` 그대로(`85`, `1.0`).
- 문장 안 숫자는 `decimal`을 `InvariantCulture`로 쓴 글자다. 퍼센트는 대조 결과 값, 문턱은 `case.yaml` 글자 그대로다.
- 같은 입력이면 같은 바이트다. 프로세스, 경로, 문화권과 상관없다.

### 3.4 명령과 AI용 입구

- 내부 명령 `judge --json`의 표준 출력은 judgment.json과 바이트까지 같다. 그래서 데이터 7절 블록이 AI용 입구의 정답이다.
- 사용자 명령 `check`는 `compare` → `measure` → `judge` → `review` → `report` 순서다.
- 종료 코드: 판정 넷 모두 0. 대조 오류는 판정 전에 2. 인자 예외(5절)는 1.

## 4. 규칙

### 4.1 개요

- 지시문 3.4 원문은 데이터 4.1 블록이다.
1. 대조가 미검증이면 판정도 미검증이다. 확인을 하지 않는다(점수표만인 경우는 `text-check.md` 5.3).
2. 확인 다섯을 늘 모두 계산한다: 정확도, 누락률, 설명 안 되는 검토 항목, 시간, 몰림.
3. 판정 표(4.3)를 위에서부터 보고 처음 걸리는 행에서 멈춘다. 보류 → 조건부 → 통과 순서다.
4. 문턱 비교는 셈(`counts`)으로 한다. 표시 퍼센트로 비교하지 않는다.
5. 누락률 초과는 보류다.
6. 판정 문장·확인 문장·다음 할 일 문구는 Core가 만든다.

### 4.2 문턱 기본값

- `thresholds` 키가 없으면 데이터 4.2 표의 값(12.1 예시 값)을 쓰고 `defaults`에 키를 적는다. 표 순서가 `defaults` 순서다.
- 기본값을 쓴 문턱은 문장 끝에 기본값 표시를 붙이고(4.6), 다음 할 일에 `confirm_thresholds`를 더한다.

### 4.3 판정 표

- 표는 데이터 4.3. "확인 결과"는 그 근거 키의 확인 결과(4.4)다. 문장 틀의 `{…}`는 4.5의 값이다.
- 1행의 결과는 대조 결과의 `reason`이다. 미검증이면 `checks`가 `[]`다.
- 시간(5)이 몰림(6)보다 앞이다. 확인 다섯은 `checks`에 모두 남는다.
- 순서 7(사람이 안 본 검토 항목)은 `FOCUS` 뒤다. 보지 않은 항목을 보류로 두지 않는다. "설명 안 됨"과 "아직 안 봄"을 가르기 위해서다.

### 4.4 확인 다섯

- 표는 데이터 4.4. 확인 순서는 `ACCURACY` → `MISSING` → `UNEXPLAINED` → `TIME` → `FOCUS`다.
- 셈은 정수와 decimal이다. 나눗셈을 쓰지 않는다. 문턱 비교는 반올림 없이 정확히 한다.
- `FOCUS`의 `{bad}`는 불일치 수다. 추가 행·중복 행은 몰림에 들지 않는다.
- 시간 합 `process + review`가 decimal 범위를 넘으면 `not_met`이다.
- `time_must_drop`이 `false`면 시간을 받아도 보지 않는다(`off`).

### 4.5 문장 값과 숫자

- `{accuracy_pct}`·`{missing_rate_pct}`: 대조 결과 퍼센트. `{match}`·`{missing}`·`{reference}`: `counts`. `{accuracy_min}`·`{missing_max}`: 쓴 문턱.
- `{bad}`: 불일치 수. `{unexplained}`: 사람이 적은 키 수. `{items}`·`{breakdown}`: 4.9.
- `{process}`·`{review}`·`{before}`: 받은 시간. `{total}`: `process + review`(decimal 덧셈 그대로).
- `{group}`: `inputs.group` 열 이름. `{value}`: 몰린 값. `{rows_in}`·`{rows_out}`: 그 값인 기준 행 수와 나머지.
- 이름·값은 `metrics.md` 4.7 메시지 방식으로 보인다(제어 문자 `\uXXXX`, 64자 자름, 빈 값 `(빈 칸)`).
- 숫자 표(`focus_min` 등)는 데이터 4.5.

### 4.6 기본값 표시

- 표시 글자는 `classification.md` 4.6과 같다(데이터 4.6 글머리).
- 확인 문장: 그 확인이 쓰는 문턱이 기본값일 때. `UNEXPLAINED`·`FOCUS`는 문턱을 쓰지 않는다.
- 판정 문장: 그 행에 오기까지 읽은 문턱(데이터 4.6 표) 가운데 하나라도 기본값일 때.
- 미검증 문장은 문턱을 읽지 않는다. 기본값은 `defaults`와 `confirm_thresholds`에 남는다.

### 4.7 다음 할 일

- 표는 데이터 4.7. `todo` 순서는 표 순서다. 통과·할 일 없음이면 `[]`다.
- `{defaults}`: 기본값을 쓴 키와 값을 4.2 순서로 `, `로 잇는다.
- `add_group`은 불일치가 `focus_min` 이상이고 `inputs.group`이 없을 때만 나온다.
- `no_compare_fields` 미검증은 `add_data` 없이 `add_compare`부터다.

### 4.8 "특정 유형에만 불일치가 몰림"

1. 불일치가 `focus_min`보다 적으면 `few`. 열을 보지 않는다.
2. `inputs.group`을 적지 않았으면 `off`.
3. 열은 `inputs.group` 하나다. 값은 기준 행의 그 칸(정규화 뒤)이다. 결과 자료의 값, 추가 행, 중복 행은 보지 않는다.
4. 기준 행에서 그 열의 값이 하나뿐이면 `one_value`.
5. 불일치 행의 값이 모두 같으면 `found`. 아니면 `spread`.

- 열이 있는지는 대조가 본다(`metrics.md` 4.7). 그래서 판정은 늘 열을 찾는다.
- 결과 쪽에 두 번 나온 기준 키는 불일치로 한 번 든다. 그 `duplicate` 항목은 더 들지 않는다.
- 비율 조건은 두지 않는다. 사용자가 열을 고른다.

### 4.9 "설명 안 되는 불일치"와 검토 항목

- 검토 항목 표는 데이터 4.9: 불일치(`mismatch`, `missing`), 추가 행(`extra`), 중복 행(`duplicate`). 허용오차 안 차이만 있는 일치는 검토 항목이 아니다.
- `{items}`·`{breakdown}`의 정의는 데이터 4.9 글머리다.
- 정확도·누락률은 그대로다. 추가 행·중복 행은 문턱 셈에 들지 않고 사람 검토로 판정에 닿는다(4.3 순서 4·7).
- 엔진은 항목이 "설명되는지" 알 수 없다. 그래서 사람 입력으로 받는다.
- 사람 입력의 키는 검토 항목의 키여야 한다. 아니면, 또는 같은 키 두 번이면 `ArgumentException`이다. 한 키가 항목 여럿을 덮을 수 있어 `{unexplained}`는 키 수다.

### 4.10 시간

- `time_must_drop: false`면 시간을 판정에 쓰지 않는다(`off`).
- 비교: `process + review < before`. 같으면 `not_met`. 세 값은 건당 분, decimal, 0 이상(음수면 `ArgumentOutOfRangeException`).
- `times.yaml`을 읽어 `JudgmentTimes`를 만드는 일은 M2 `measure`다.

### 4.11 결과를 어디에 쓰나

- 판정은 `examples/*/expected/judgment.json`이다. 대조(metrics.json)와 파일이 따로다. 판정 규칙을 고치는 카드는 judgment.json만 바꾼다.
- oracle(`tools/oracle/judge.py`)은 이 문서와 데이터 파일만 보고 같은 바이트를 낸다.

### 4.12 결정론·원본 불변·G1·G2·G3

- `Judge`는 순수 함수다. 난수·시각·환경 변수·현재 폴더·파일을 쓰지 않는다. 출력 순서는 표 순서·열 이름 서수다.
- 입력을 바꾸지 않는다.
- G2·G3 대상 `judge-case-valid`(데이터 4.12 표). 시간·사람 입력은 null이다. 표본 판정은 보류 `ACCURACY`(66.7% < 90)다. 출력 SHA-256은 데이터 4.12 글머리다.
- 숫자가 문장에 들어가므로 G2가 문화권(R4)을 본다.

### 4.13 Core 공개 API

- 네임스페이스 `Scorecard.Core.Judgment`. 모양은 데이터 4.13 블록이다.
- 기본값·결과 문자열·문장 틀·문구·`focus_min`은 `internal` `JudgmentRules.cs` 한 파일에 둔다. 테스트가 데이터 표와 대조한다.

## 5. 예외

- 정의와 대조 결과 표는 데이터 5절. `Judge`는 대조 결과가 이 정의에서 나온 것인지 본다. 아니면 `ArgumentException`(종료 1)이다.
- `Inputs`가 null이면 `no_inputs`, `Inputs`가 있고 대조할 열이 없으면 `no_compare_fields`, 그 밖이면 `measured`이고 키·필드 이름(순서)·`checklist`가 정의와 같아야 한다.
- 문턱 곱이 decimal 범위를 넘어도 예외 없이 부호대로 비교한다.

## 6. 예시

- 문턱 경계(6.1), 시간(6.2), 몰림(6.3), 검토 항목과 `compare-duplicate` 판정 블록(6.4), 우선순위(6.5), 탐침(6.6)은 데이터 6절 표다.

## 7. 예제 4종 `expected/judgment.json`

- 예제 `case.yaml`에 더한 줄 표, 예제 표, 블록, SHA-256 줄은 데이터 7절이다. 파일은 블록과 바이트까지 같다.
- table-compare는 조건부 `FOCUS`(불일치 3건이 모두 `signal_type = irregular`), form-fill은 통과, mixed-exceptions는 미검증, signoff-final은 보류 `ACCURACY`다.
- `expected/` 변경은 ADR 0004의 expected 변경 규칙을 따른다.
