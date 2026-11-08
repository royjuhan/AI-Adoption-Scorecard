# classification (규칙 트리 분류)

- 지금 동작만 적는다. 표·JSON 블록·예제 블록은 `docs/spec/data/classification.md`에 있다. 절 번호가 같다. 테스트와 oracle이 그 파일을 읽는다.
- 결정 근거: ADR 0003(`human_signoff`, `SIGNOFF`), ADR 0004(`schema_version`, 추정 표시), ADR 0010(AI 분류 + 규칙 의견). 바뀐 기록은 git 기록에 있다.
- 이 엔진의 결과는 ADR 0010의 "규칙 의견"이다. AI 판단과 함께 적는 schema 2(`source`, `rule_opinion`)는 9절이다(M2-2, ADR 0013). 1~8절은 PR (b) 전까지 Core의 지금 동작이다.
- 담당: ① 기존 기능·설정, ② 규칙 기반 소프트웨어, ③ 실행 시 AI 보조, ④ 사람.
- 질문 10개의 키·선택지·기본값은 `case-schema.md` 4.4 표(데이터 파일)에만 있다.

## 1. 범위

- 단계마다 담당·근거 키·근거 문장, 기본값 표시, 추정 표시, 확인 항목, AI 과잉 후보.
- 범위 밖: 명령과 출력 폴더(M2), 대조·지표·판정(`metrics.md`, `judgment.md`), 입력 SHA-256(스코어카드), 화면 문구.

## 2. 입력

- `CaseDefinition` 하나(`CaseParser` 결과). 분류기는 파일을 읽지 않는다. 파싱 오류가 있으면 분류하지 않는다.
- 답 10개는 YAML 문자열 그대로(`"fixed_form"`), 숫자는 `int`·`decimal`이다. 기본값이 든 답은 `IsDefault`, `inferred` 목록의 답은 `IsInferred`다.
- 공개 예제의 `answers.yaml`은 `case.yaml` 스키마의 부분이다(`name`, `task`, `steps`). `CaseParser.Parse(bytes, "answers.yaml")`로 읽는다. 공개 예제에는 `inferred`를 쓰지 않는다.
- `answers.yaml`의 LF·CRLF·CR·BOM 변형은 같은 바이트를 낸다.

## 3. 출력

### 3.1 메모리 결과

- `ClassificationResult` 하나(4.11). 사용자 단계마다 `StepClassification` 하나, 그 안에 행 1~3개.

### 3.2 classification.json

- Core가 바이트를 만든다(`ClassificationJson.Render`). 모양은 데이터 3.2 블록이다. 실제 바이트는 배열 원소도 한 줄에 하나다.
- 첫 키는 정수 `"schema_version": 1`이다.
- 속성 순서는 블록과 같다. 단계 순서는 `case.yaml` 순서, 행 순서는 4.3이다.
- `rule`이 근거 키다. 트리가 읽은 답(`Used`)과 추정 답 키(`Inferred`)는 JSON에 쓰지 않는다. 추정 표시는 `reason` 끝에만 나타난다.
- 경로를 쓰지 않는다. 문자열은 케이스 이름, 단계 이름, 고정 값, 근거 문장뿐이다.
- 단계 수(지시문 12절)는 모든 단계의 `rows` 원소 수 합이다.

### 3.3 바이트 규칙

- 데이터 3.3 표: `Utf8JsonWriter`(리플렉션 직렬화 없음), 2칸 들여쓰기, LF(CR 0개), 끝 `\n` 하나, UTF-8(BOM 없음), `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`. 이스케이프 결과도 같은 절의 표다.
- 같은 입력이면 같은 바이트다. 프로세스, 경로, 문화권과 상관없다.
- 숫자는 `schema_version`과 근거 문장 안의 `batch_size`뿐이다. `InvariantCulture`로 쓴다.

## 4. 규칙

### 4.1 결정 트리

- 트리 원문 블록과 해석 표는 데이터 4.1.
- 맨 앞 `SIGNOFF`: `human_signoff == yes`면 ④ 한 행에서 멈춘다. 예외 검토·게이트 행을 붙이지 않는다. 다른 답과 상관없다.
- 그다음 Q1(①) → Q2(③) → Q3(③, 입력 양식 표준화 먼저) → Q4(④, 그대로 두거나 일회성 AI) → ELSE(②) 순서다. 처음 걸리는 곳에서 멈춘다.
- `PARTIAL`(④ 예외 검토)은 `ELSE`일 때만 붙는다. Q3·Q4에서 멈추면 붙지 않는다.
- `GATE`(④ 사람 게이트)는 `SIGNOFF`가 아닌 모든 잎 뒤에 붙는다.
- `existing_feature: unknown`은 Q1에 걸리지 않는다. 아래로 내려가 분류하고 확인 항목을 단다(4.7).
- `exception_rate`, `current_actor`, `minutes_per_item`은 트리가 쓰지 않는다. `current_actor`는 AI 과잉 후보(4.8)에만 쓴다.
- Q2의 "사람 확인"은 ③의 뜻에 들어 있어 행을 더하지 않는다.

### 4.2 조건 표

- 근거 키마다 조건은 데이터 4.2 표다. 같은 근거 키의 행은 모두 참이어야 걸린다. 비교는 서수다.
- 경계: `rare`·4건 → Q4, `rare`·5건 → ELSE.
- `OVERUSE`(4.8)와 `CHECK_EXISTING`(4.7)은 행을 만들지 않는다. `SIGNOFF`에 걸리면 `OVERUSE`도 보지 않는다. `CHECK_EXISTING`은 트리 밖이라 `SIGNOFF` 단계에도 달린다.

### 4.3 행 만들기

- 단계마다 `step` 행 하나. `ELSE`이고 `PARTIAL` 조건이면 `exception_review` 행. `GATE` 조건이면 `human_gate` 행.
- 순서: `step` → `exception_review` → `human_gate`.
- 부가 행의 `number`·`name`은 부모 단계 것이다. `defaults`·`checks`·`ai_overuse_candidate`는 단계에 하나씩이다.
- 앞 단계의 게이트와 다음 `SIGNOFF` 단계가 이어져 사람 확인이 두 번 나와도 그대로 둔다. 트리는 단계마다 따로 정한다.

### 4.4 담당·근거 키·행 종류 값

- 값 표는 데이터 4.4. 담당은 JSON 문자열 `①`~`④`(U+2460~U+2463)이고 이스케이프하지 않는다.

### 4.5 근거 문장

- 문장 틀 표와 문구 표는 데이터 4.5. `{키 문구}`는 문구 표, `{키}`는 답의 값(숫자는 `InvariantCulture`)으로 바꾼다.
- 문장에는 그 잎을 정한 답만 쓴다. Q1: `existing_feature`. Q2: `rule_expressible`. Q3: `input_kind`. Q4: `frequency`·`batch_size`. ELSE: `rule_expressible`·`input_kind`·`frequency`(`rare`면 `batch_size`도). `SIGNOFF`: `human_signoff`.
- 지시문 3.3 예시 문장과 바이트까지 같다. 그래서 `frequency` 문구에는 값 코드를 붙이지 않는다.
- 틀·문구 문자열은 Core 한 파일(`ClassificationRules.cs`)에 둔다. 테스트가 데이터 표와 대조한다.

### 4.6 기본값 표시

- 행을 정할 때 트리가 읽은 답(데이터 4.6 표) 중 하나라도 `IsDefault`면, 문장 끝에 공백 하나와 `(기본값 사용, 확인 필요)`를 붙인다.
- `failure_impact`는 늘 읽는다. `batch_size`는 `frequency`가 `rare`일 때만 읽는다. `human_signoff`는 맨 먼저 읽는다. 그래서 `SIGNOFF`가 아닌 `step` 행은 모두 `human_signoff`를 읽는다.
- `SIGNOFF`는 `human_signoff`만 읽는다. `PARTIAL`은 `rule_expressible`, `GATE`는 `failure_impact`만 읽는다.
- `defaults`에는 단계의 답 10개 중 `IsDefault`인 키를 모두 적는다(`case-schema.md` 4.4 순서). 문장 표시와 상관없다.

### 4.7 확인 항목

- 키 표는 데이터 4.7: `measure_minutes_per_item`(`minutes_per_item`이 null), `confirm_existing_feature`(`existing_feature` = `unknown`).
- 값만 본다. 직접 쓴 값도 기본값도 같다. 순서는 표 순서다.

### 4.8 AI 과잉 후보

- `current_actor`가 `ai_each_time`이고 `step` 행의 담당이 ②면 `ai_overuse_candidate`가 true다. 부가 행은 보지 않는다.
- Q1~Q4와 `SIGNOFF`는 후보가 아니다.

### 4.9 숫자·값을 어디서 읽나

- 조건 값(`5` 포함), 문장 틀·문구, 표시 글자는 Core `ClassificationRules.cs` 한 파일의 `internal` 상수다.
- 실행 중에 문서를 읽지 않는다. 테스트가 데이터 파일의 표를 읽어 Core와 대조한다. 표를 바꾸면 테스트가 실패하고, 그때 Core를 고친다.
- `case.yaml`의 `thresholds`에 분류 숫자를 넣지 않는다.

### 4.10 결정론·원본 불변·G2·G3

- 분류기는 순수 함수다. 난수·시각·환경 변수·현재 폴더·파일을 쓰지 않는다. 출력 순서에 해시·`Dictionary` 열거 순서를 쓰지 않는다.
- G2·G3 대상 `classifier-case-valid`(데이터 4.10 표): `case-valid` → `ParseDirectory` → `Classify` → `Render` → `classification.json`.
- 표본 `case-valid`는 `human_signoff`를 적지 않아 기본값 표시가 5행이다.

### 4.11 Core 공개 API

- 네임스페이스 `Scorecard.Core.Classification`. 모양은 데이터 4.11 블록이다.
- 질문 키 순서는 Core의 `StepQuestions.Keys`를 쓴다.

### 4.12 추정 표시

- 행을 정할 때 읽은 답(4.6 표) 중 하나라도 `IsInferred`면 문장 끝에 공백 하나와 `(AI 추정, 확인 필요)`를 붙인다. 기본값 표시가 있으면 그 뒤다.
- 세 행 종류 모두 같은 규칙이다. 부가 행은 읽은 답이 키 하나다.
- `Inferred`: 단계의 `IsInferred` 키(`case-schema.md` 4.4 순서). 메모리 결과에만 둔다.
- 분류(담당·근거 키·행), `defaults`, 확인 항목, AI 과잉 후보는 추정과 상관없다.
- 예시 표는 데이터 4.12.

## 5. 예외

- 경우별 결과는 데이터 5절 표. `definition`이 null이면 `ArgumentNullException`이다.

## 6. 예시

- 분기 예시(6.1), 기본값 표시 예시(6.2), 서명 단계와 추정 표시 표본(6.5)은 데이터 파일에 있다. 6.1의 기준 답 줄은 모든 답을 직접 쓴 값이다.
- 6.5 표본 파일은 `tests/Scorecard.Tests/TestData/classification/signoff-inferred/answers.yaml`이다.

## 7. 공개 예제

- 예제 4종(table-compare, form-fill, mixed-exceptions, signoff-final)의 `answers.yaml`과 `expected/classification.json` 블록, SHA-256 줄, 요약 표는 데이터 7절이다.
- 파일은 블록과 바이트까지 같다. `examples/.gitattributes`(`* -text`)가 줄바꿈을 지킨다.
- `expected/` 변경은 ADR 0004의 expected 변경 규칙을 따른다.
- 세 예제는 모든 단계가 `human_signoff: no`다. signoff-final 7단계만 `yes`라 ④ `SIGNOFF` 한 행이다.
- 예제는 `minutes_per_item`을 적지 않았다. 그래서 모든 단계에 `measure_minutes_per_item`이 달린다.

## 8. 테스트

- 폴더 `tests/Scorecard.Tests/Classification/`, Trait `Spec` = `classification`.
- `SpecDocument.Table`이 데이터 파일에서 머리 행으로 표를 찾는다. 머리 행은 그 파일에 한 번만 나온다.
- 골든: `examples/<예제>/answers.yaml` → `Parse` → `Classify` → `Render` = `expected/classification.json`(바이트).
- oracle(`tools/oracle/classify.py`)은 이 문서와 데이터 파일만 보고 같은 바이트를 낸다. `selftest.py`가 데이터 표로 점검한다.

## 9. schema 2: AI 판단과 규칙 의견 (M2-2)

- 상태: 채택(ADR 0005 결정 3, ADR 0013). 카드 `docs/tasks/T-019-m2-2-scorecard-skill.md`. 표·블록은 데이터 9절이다. oracle은 이 절대로 낸다. Core·expected·스킬은 PR (b)가 바꾸고, 이 절을 1~8절 제자리로 옮긴다(9.9).

### 9.1 입력: 단계의 `ai`

- AI(스킬 `/scorecard`)가 업무 설명을 읽고 단계마다 담당과 근거를 `answers.yaml` 단계 항목 `ai: {assignee, reason}`에 적는다. 파서 규칙은 `case-schema.md` 4.14.
- 담당은 `①` `②` `②M` `③` `④`다. `②M`은 학습 모델(결정론)이다. 미리 학습해 고정한 모델을 규칙 SW처럼 돌린다. 실행 때 AI를 부르지 않고 같은 입력이면 같은 결과다. ②의 한 갈래다.
- 트리는 `②M`을 내지 않는다(가르는 질문이 없다). 그래서 규칙 의견과 비교할 때 `②M`은 ②로 본다(데이터 9.3 첫 표).
- 답(`answers`)·`inferred`는 그대로이고, 트리는 `ai`가 있어도 답으로 규칙 의견을 낸다. 단계마다 따로라 `ai`가 있는 단계와 없는 단계가 섞여도 된다.

### 9.2 classification.json schema 2

- 모양은 데이터 9.2 블록이다. 첫 키는 정수 `"schema_version": 2`다.
- 단계 속성 순서: `number`, `name`, `source`, `rule_opinion`, `differs`, `defaults`, `checks`, `ai_overuse_candidate`, `rows`.
- `source`: 그 단계에 `ai`가 있으면 `ai`, 없으면 `rule`.
- `rule_opinion`: 트리 `step` 행의 `assignee`·`rule`. `source`와 상관없이 늘 쓴다. `rule`이면 `rows[0]`의 두 값과 같다.
- `differs`: `source`가 `ai`이고 AI 담당의 비교 값(데이터 9.3)이 `rule_opinion.assignee`와 다르면 true. `rule`이면 늘 false. 스코어카드는 true인 단계를 표시한다.
- 바이트 규칙(3.3)은 그대로다. AI 근거는 쓴 글 그대로 쓰고 3.3 이스케이프만 한다.

### 9.3 행

1. 트리는 1~8절대로 행을 만든다(`step` 행과 부가 행). 장면은 데이터 9.3 둘째 표다(기준 답은 6.1 줄).
2. `source`가 `rule`이면 그 행이 `rows`다. 1~8절과 같다.
3. `source`가 `ai`면 `step` 행만 AI 행으로 바꾼다: `kind` `step`, `assignee` AI 담당, `rule` `AI`, `reason` `ai.reason` 그대로. 기본값·추정 표시를 붙이지 않는다(트리가 읽은 답이 아니다). 메모리 `Used`는 빈 목록이다.
4. 부가 행(`PARTIAL`·`GATE`)은 트리 것을 그대로 둔다. `differs`는 부가 행을 보지 않는다.
5. 규칙 의견이 `SIGNOFF`이고 AI 담당의 비교 값이 ④가 아니면, `GATE` 조건(4.2)일 때 트리 `GATE` 행(④ `human_gate`)을 붙인다. 비교 값이 ④면 1~8절대로 붙이지 않는다. `PARTIAL`은 그대로 `ELSE`일 때만이다.
- 4·5의 이유: 게이트는 지시문 3.3의 "항상" 규칙이고 `failure_impact` 답으로 정해지는 안전 장치다. 누가 하느냐의 판단이 아니다. 예외 검토도 `rule_expressible` 답에서 온다. 그래서 AI가 단계 담당을 바꿔도 사람 확인이 빠지지 않는다. AI ④ 단계 뒤에 `GATE`가 남아 사람 확인이 두 번 나와도 그대로 둔다(4.3과 같다). `SIGNOFF` 뒤에 게이트가 없는 것은 단계 자체가 ④(사람 서명)이기 때문이다(4.3). AI가 그 단계를 ④가 아닌 담당(②M 포함)으로 바꾸면 그 근거가 없다. 붙이지 않으면 `external`·`safety_legal` 단계에서 사람 행이 모두 빠진다. 그래서 행 수는 `SIGNOFF` 단계에서만 AI 판단에 따라 하나 늘 수 있다.
- 대안(추천하지 않음): 부가 행을 `rule_opinion`에만 둔다. AI 판단 단계에서 게이트가 `rows`에서 빠진다.

### 9.4 확인 항목과 AI 과잉 후보

- 확인 항목에서 `confirm_existing_feature`를 뺀다(데이터 9.4 표). 질문 답을 확인하라는 트리 전용 할 일이고, B안에서는 AI가 기존 기능 여부를 판단한다(①). 4.2 조건 표의 `CHECK_EXISTING` 행도 PR (b)가 지운다.
- `measure_minutes_per_item`은 남긴다(측정 권고). `existing_feature`가 기본값이면 기본값 표시(4.6)와 `defaults`도 남는다.
- AI 과잉 후보: `current_actor`가 `ai_each_time`이고 `rows[0]` 담당의 비교 값이 ②(② 또는 `②M`)면 true. `source`가 `rule`이면 4.8과 같다.

### 9.5 AI 판단 표본

- 공개 예제에는 `ai`를 넣지 않는다. 넣으면 예제가 AI 판단 한 번을 정답으로 고정한다. 대신 TestData 표본 `tests/Scorecard.Tests/TestData/classification/ai-judgment/answers.yaml`(데이터 9.5)을 둔다. 같음, `②M`, 다름(부가 행 남음), `ai` 없음, 서명 단계가 한 파일에 있다.

### 9.6 공개 예제, schema_version, expected migration

- 예제 다섯의 `answers.yaml`은 그대로다. `ai`가 없어 모든 단계가 `source: rule`, `differs: false`이고 `rule_opinion`은 `rows[0]`과 같다. 새 블록은 데이터 9.6이다.
- `schema_version` 1 → 2. ADR 0008 결정 1: 받던 입력(예제 다섯)에서 `checks` 값이 빠지고 단계 속성 셋이 는다. juhan 지시(ADR 0010 A-2)도 올림이다.
- ADR 0004 expected 변경 규칙을 따른다. `expected migration:` 커밋에는 expected 다섯과 스펙 블록·SHA-256 줄만 둔다. 파일마다 바뀌는 줄은 데이터 9.7 표다(단계마다 6줄 더함, `schema_version` 줄, form-fill `confirm_existing_feature` 줄과 앞 줄 쉼표).

### 9.8 사용자 확인과 oracle 판

- 확인은 스킬 대화에서 한다(B안). 스킬은 단계마다 AI 판단(담당·근거)과 추정 답을 보여 주고, 사용자가 맞음/바꾸기를 고른 뒤에만 `answers.yaml`을 쓰고 `start --answers`를 부른다. 엔진은 묻지 않고, 파일의 `ai`를 확인된 판단으로 쓴다.
- `tools/oracle/classify.py`는 schema 1·2 두 판이다. `check.py`가 expected classification.json의 `schema_version`으로 고른다. schema 1 판은 `ai`가 있는 입력을 거절한다. PR (b) 뒤 첫 oracle 변경에서 schema 1 판과 판 고르기를 지운다(ADR 0013).

### 9.9 PR (b)가 옮길 곳

1. 데이터 9.2 블록 → 3.2 블록. 9.4 표 → 4.7 표(머리 행은 `| 키 | 조건 | 뜻 |` 그대로). 4.2 `CHECK_EXISTING` 행, 6.2 표와 "답 없는 단계" 블록의 `confirm_existing_feature`를 지운다.
2. 데이터 9.6 블록 → 7.2~7.5 expected 블록·SHA-256 줄(`schema 2 ` 머리를 뗀다), reply-drafts는 `text-check.md` 데이터 7.4. 7.1 요약 표 "확인 항목" 칸.
3. 데이터 9.3 표·9.5 표본은 머리 행·표시 줄 그대로 6절 끝으로, 9.7 표는 지운다. 본문 1~8절을 이 절대로 고치고 이 절을 지운다(200줄 이내). `case-schema.md` 4.14 상태 줄과 데이터 4.4 단계 항목 표 `ai` 행.
