# case-schema (`case.yaml` 스키마와 파서)

- 지금 동작만 적는다. 표·예시 블록·오류 줄은 `docs/spec/data/case-schema.md`에 있다. 절 번호가 같다. 테스트와 oracle이 그 파일을 읽는다.
- 결정 근거: ADR 0002·0003·0004·0005·0008·0009·0010. 바뀐 기록은 git 기록에 있다.
- 이름: G1·G2·G3, N6·D1·R1은 `docs/spec/guard-tests.md`의 이름이다.

## 1. 범위

- `case.yaml`의 필드·형식·필수 여부·기본값.
- Core 파서: 읽기, 검증, 오류(어떤 필드인지, 줄·열).
- 범위 밖: 분류 트리와 근거 문장(`classification.md`), `inputs` 파일 찾기와 대조(`metrics.md`), 문턱 기본값과 판정(`judgment.md`), 입력 SHA-256 기록(스코어카드), `case.yaml` 쓰기.

## 2. 입력

- `<케이스 폴더>/case.yaml` 한 파일. 읽기만 한다. 폴더의 다른 파일은 열지 않는다(이름 목록만 본다).
- 이름은 정확히 `case.yaml`이다. 폴더 항목 이름을 서수로 비교한다. `Case.YAML`처럼 대소문자만 다르면 모든 OS에서 "파일이 없다"다.
- 목록을 읽을 권한이 없는 폴더는 "파일을 읽을 수 없다"다.

## 3. 출력

- 메모리 결과 `CaseParseResult` 하나(4.12). 모델 또는 오류 목록. 파서는 파일을 쓰지 않는다.
- G2·G3용 파일은 테스트 쪽 어댑터가 쓴다(4.11).

## 4. 규칙

### 4.1 파서 방식

1. 바이트 → 문자열(4.8).
2. `YamlDotNet.Core.Parser` 이벤트를 읽어 작은 트리(매핑·목록·스칼라)를 만든다. 이때 앵커·별칭·태그·중복 키·깊이를 본다(4.7).
3. 트리를 스키마 순서대로 걷는다. 필드마다 형식·필수 여부를 보고 오류를 모은다(4.3~4.6).
4. 오류가 없으면 모델을 돌려준다. 하나라도 있으면 모델 없이 오류 목록만 돌려준다.

- 리플렉션 역직렬화와 `YamlStream`은 쓰지 않는다.

### 4.2 YamlDotNet 패키지와 G1

- Core csproj에 `YamlDotNet` 18.1.0을 정확히 고정한다.
- G1 N6 허용 목록(`NetworkGuardTests.AllowedThirdPartyAssemblies`)에 `YamlDotNet`이 있다.
- Y1 규칙(데이터 4.2 표): Core는 `YamlDotNet.Core` 밖의 YamlDotNet 타입을 참조하지 않는다. 항목 문자열은 `Y1 type <전체 이름>`이다.
- 카나리아: 테스트 프로젝트가 `YamlDotNet.Serialization.DeserializerBuilder`, `YamlDotNet.RepresentationModel.YamlStream`을 참조해 Y1이 그것을 잡는지 본다.

### 4.3 문서 구조 (최상위)

- 키 표는 데이터 4.3. 표에 없는 키는 `unknown-field`다. 키 순서는 상관없다.
- 필수 문자열이 비면(따옴표 없는 빈 값, `~`, `""`, 공백뿐) `required` "값이 비어 있다".
- 자유 서술은 저장만 한다. 앞뒤 공백을 지우거나 정규화하지 않는다. 블록 값(`|`)의 줄바꿈은 파일 줄바꿈과 상관없이 `\n`이다.
- 최상위 키 `checklist`·`scores`와 `thresholds.score_gap_max`는 `text-check.md` 4.1이 정한다. 둘 다 선택이다. 없으면 이 문서대로다.

### 4.4 단계와 답 10개

- 단계 항목 표와 답 10개 표(키·질문·형식·선택지·기본값)는 데이터 4.4. 기본값 표는 저장소에 이 하나뿐이다. 다른 스펙은 이 표를 가리킨다.
- 키가 없거나 값이 null(따옴표 없는 빈 값, `~`, `null`, `Null`, `NULL`)이면 기본값을 넣고 `IsDefault = true`다. "기본값 사용, 확인 필요" 표시의 근거다.
- 따옴표 있는 `""`, `"null"`은 문자열이다. 열거 값이 아니면 `enum` 오류다.
- 값을 직접 쓰면 `IsDefault = false`다. `existing_feature: unknown`을 직접 써도 false다.
- 값이 잘못되면 오류다. 기본값으로 바꿔 넣지 않는다.
- 범위: `batch_size` 1 이상, `exception_rate` 0 이상 100 이하(소수 허용), `minutes_per_item` 0 이상.
- `human_signoff`: 다른 열거 답과 같다. 없거나 null이면 `no`, `IsDefault = true`. `Yes`·`true`·`""`·`unknown`은 `enum` 오류다.
- `human_signoff`는 서명 단계를 한 번 물어 모든 단계에 적는다(ADR 0003). 그래서 AI가 만든 파일에서는 기본값이 되지 않는다.

### 4.5 inputs, compare, thresholds

- 키 표 셋은 데이터 4.5.
- `inputs`가 있으면 `reference`·`candidate`·`key`가 필수다. `group`은 선택이다. 형식 규칙은 `key`와 같다.
- `reference`·`candidate`는 케이스 폴더 기준 상대 경로다. `/`로 시작, `\`·`:`·`..` 조각은 `format`이다. `*` 같은 glob 문자는 그대로 저장한다. 해석은 `metrics.md` 4.1.
- `group`이 `key` 또는 `compare` 필드와 같으면(서수) `conflict`다. 위치는 그 값, 경로는 `inputs.group`이다. `compare`를 다 읽은 뒤 찾는다. 그 열이 자료에 있는지는 대조가 본다.
- `group` 장면 표(데이터 10.2)는 `examples/table-compare/case.yaml`의 `  key: log_id` 줄 뒤에 줄을 넣은 결과다.
- `compare` 항목에 `tolerance`와 `tolerance_pct`를 같이 쓰면 `conflict`다. 둘 다 없어도 된다(텍스트 비교, `metrics.md` 4.6).
- `compare`에 항목이 있는데 `inputs`가 없거나 null이면 `required` 경로 `inputs`, 위치는 `compare` 값이다.
- `thresholds`는 읽기만 한다. 없는 값은 null이다. 기본값은 `judgment.md` 4.2가 정한다.

### 4.6 값 형식

- 형식별로 받는 값과 받지 않는 값은 데이터 4.6.
- YAML 1.1 불리언 낱말(`yes`, `no`)은 열거 값으로 그대로 들어온다. 따옴표가 있어도 없어도 같다.
- `decimal`은 소수 자릿수를 보존한다. `1.50`은 `1.50`이다. 값 비교는 같고 출력 글은 다르다.
- 정규식은 `CultureInvariant`, 이름 비교는 서수, 숫자는 `InvariantCulture`다.

### 4.7 YAML 기능 제한

- 기능별 처리는 데이터 4.7. 다중 문서·앵커·별칭·태그는 오류다. 병합 키는 일반 키라 `unknown-field`다.
- 깊이 16을 넘으면 `depth` 한 건을 내고 바로 멈춘다. 깊은 흐름 중첩에서 파서가 느려지기 때문이다.
- 파일 크기 상한은 없다. 평평한 큰 파일은 빠르다.

### 4.8 인코딩·줄바꿈

- 바이트별 결과는 데이터 4.8. UTF-8만 받는다. 앞의 BOM 하나는 뗀다. LF·CRLF·CR은 같은 결과다.
- 디코딩은 `new UTF8Encoding(false, throwOnInvalidBytes: true)`다. UTF-16 BOM은 직접 보고 `encoding` 오류를 낸다.
- 바이트 N은 파일 처음부터 0으로 센 위치다(BOM 포함).
- CR만 쓰는 파일의 `encoding` 줄 번호는 LF로 세므로 YAML 줄 번호와 다를 수 있다.

### 4.9 오류

- 오류 하나는 `CaseError(Source, Line, Column, Code, Path, Message)`다. 문자열 모양과 코드 17개 표는 데이터 4.9.
- `Source`: 파일 이름. `ParseDirectory`는 늘 `case.yaml`이다. 절대 경로를 쓰지 않는다.
- `Line`·`Column`: 1부터. 0이면 없음. 열은 UTF-16 코드 단위다(한글 1칸, 이모지 2칸).
- `Path`: 매핑은 `.`, 목록 항목은 `[n]`(1부터). 예: `steps[2].answers.batch_size`. 문서 전체 오류는 빈 문자열.
- `Message`: 한국어. `syntax`는 YamlDotNet의 영어 메시지를 "YAML 문법 오류: " 뒤에 붙인다.
- 열이 0이면 `<Source>:<Line>:`, 줄도 0이면 `<Source>:`. 경로가 비면 `<Code>: <Message>`.
- `file`, `encoding`, `empty`, `syntax`, `depth`는 그 한 건만 낸다. 나머지는 모두 모은다.
- 순서는 (줄, 열) 오름차순. 같은 위치는 찾은 순서(안정 정렬)다. 찾는 순서는 4.3 표 순서다.
- 오류 개수 상한은 없다.

### 4.10 결정론·원본 불변·상대 경로

- 같은 바이트 → 같은 모델·같은 오류 문자열. 난수·시각·환경 변수·현재 폴더를 쓰지 않는다.
- 입력을 읽기만 한다.
- 출력(오류 글 포함)에 절대 경로를 쓰지 않는다. 입출력 예외는 메시지 대신 예외 타입 이름만 쓴다.

### 4.11 G2·G3 등록과 빈 출력 규칙

- 대상 둘(데이터 4.11 표): `case-parser-valid`, `case-parser-invalid`.
- 어댑터는 `ParseDirectory` 결과를 `parse-result.json` 한 파일로 쓴다: `{ "case": <모델 또는 null>, "errors": [<ToString()>…] }`. 2칸 들여쓰기, LF, `UnsafeRelaxedJsonEscaping`, UTF-8(BOM 없음), 끝 `\n`.
- 오류 표본도 G2 대상이다. 오류 줄의 `Source`에 절대 경로가 새면 R3가 잡는다.
- 빈 출력 규칙: 출력 폴더에 1바이트 이상인 파일이 없으면 G2가 `G2 R1 기준: 출력 파일 없음`을 차이로 적는다. G3에는 넣지 않는다.
- `TestData/guard/basic/case.yaml`은 가짜 대상의 바이트 샘플이다. 이 스키마에 맞지 않는다.

### 4.12 Core 공개 API

- 네임스페이스 `Scorecard.Core.Cases`. 모양은 데이터 4.12 블록이다.
- 열거 값은 YAML 문자열 그대로다. 숫자는 `decimal`이다. 오류 `Code`는 4.9 표의 문자열이다.
- YamlDotNet 타입은 공개 API에 나오지 않는다. 트리·검사 코드는 `internal`이다.
- 답마다 `IsDefault`와 `IsInferred`가 있다. `CaseInputs.Group`은 기본값 매개변수다.

### 4.13 추정 답 목록 `inferred`

- 단계 항목의 선택 키다. 없거나 null, `[]`이면 추정 답이 없다. 흐름 목록과 블록 목록 모두 받는다.
- 목록이 아니면 `type` `목록이어야 한다`, 경로 `steps[n].inferred`.
- 원소가 매핑·목록이면 `type` `스칼라 값이어야 한다`. null 원소는 `required` `값이 비어 있다`. 경로 `steps[n].inferred[m]`(1부터).
- 원소는 답 키 10개 중 하나다(서수). 아니면 `enum`이고 가능한 값은 4.4 표 순서의 키 10개다.
- 같은 키가 둘 이상이면 둘째부터 `duplicate`.
- 적은 키의 답은 `answers`에 값이 있어야 한다. 없으면 `required`, 위치는 그 원소, 경로는 `steps[n].answers.<키>`. 값이 없는 답은 기본값이지 추정이 아니다.
- 값이 있지만 잘못된 답은 그 답의 오류 한 건만 난다.
- 결과: 목록에 있는 답만 `IsInferred = true`. 그래서 한 답이 기본값이면서 추정일 수 없다.
- `inferred`는 분류를 바꾸지 않는다. 근거 문장 표시만 바꾼다(`classification.md` 4.12). 공개 예제에는 쓰지 않는다.

### 4.14 AI 판단 `ai` (M2-2)

- 상태: 채택(ADR 0013). Core 파서와 oracle이 받는다.
- 단계 항목의 선택 키다. AI(스킬 `/scorecard`)가 그 단계의 담당과 근거를 적는다. 없거나 null이면 AI 판단이 없다.
- 매핑이다. 키는 `assignee`, `reason` 둘이고 둘 다 필수다. 다른 키는 `unknown-field`다. 흐름 매핑과 블록 매핑 모두 받는다.
- `assignee`: `①` `②` `②M` `③` `④` 중 하나(서수). 아니면 `enum`이고 가능한 값은 그 다섯이다. 뜻은 `classification.md` 4.13.
- `reason`: 문자열. 비면(null, `""`, 공백뿐) `required` `값이 비어 있다`. 저장만 한다(앞뒤 공백·정규화 없음).
- 오류 코드·경로·메시지는 4.9 표 그대로다. 장면 표는 데이터 4.14. 위치는 4.9 규칙(빔: 그 값, 없음: 부모 매핑 시작)이다.
- 결과: `CaseStep.Ai`(담당, 근거). 없으면 null이다. `answers`·`inferred`와 상관없다.
- 단계 항목 표(데이터 4.4)에 `ai` 행이 있다.

### 4.15 스킬 선택 항목 (T-022)

- 상태: 추천안 채택(ADR 0016, juhan 확인 2026-11-09). Core 파서가 받는다. oracle은 받지 않는다(공개 예제에 없다, ADR 0016 결정 4).
- 스킬 `/scorecard`가 적는다. Core는 분류·대조·판정에 쓰지 않고 그대로 옮긴다. 스코어카드의 "확인하면 바뀔 수 있음" 셈만 `actions[].source`를 본다(report.md 12.3).
- 단계 `ai`의 선택 키: `actions`(동작 목록), `now`·`change`·`check`(단계 표 세 칸), `gate_note`(담당자 확인 메모). 하나라도 있으면 `StepAi.Detail`, 없으면 null.
- 최상위 선택 키: `scenario`, `summary`, `stated_numbers`, `self_check`, `open_questions`, `end_state`. 하나라도 있으면 `CaseDefinition.Notes`, 없으면 null.
- 형식·선택지는 데이터 4.15 표, 오류는 4.9 표 그대로(`type`, `required`, `enum`, `unknown-field`). 장면 표는 데이터 4.15.
- 문장은 저장만 한다(앞뒤 공백 유지). 사용자가 준 숫자는 바꾸지 않는다(지시문 0016 2.4).

### 4.16 역할 배분 선택 항목 (T-024)

- 상태: 추천안 채택(ADR 0018, juhan 확인 2026-11-10 07:41). Core 파서가 받는다. oracle은 받지 않는다(공개 예제에 없다).
- 스킬 `/scorecard`가 juhan 지시(2026-11-10, 인간·AI·소프트웨어 역할 배분 기준)에 따라 적는다. Core는 분류·대조·판정·셈에 쓰지 않고 그대로 옮긴다.
- 단계 `ai`의 선택 키: `roles`(`human`·`ai`·`software`, 세 역할), `judgment`(판단), `execution`(실행), `accountability`(책임 인수 지점), `basis`(배분 근거), `varies`(달라지는 것). 하나라도 있으면 `StepAiDetail.Allocation`, 없으면 null. 이것만 있어도 `Detail`이 있다.
- 최상위 선택 키: `purpose`(목적), `done_when`(완료 조건), `facts`(확인된 사실), `assumptions`(가정), `tools`(도구 제안), `transitions`(역할 전환 조건). 하나라도 있으면 `CaseNotes.Allocation`, 이것만 있어도 `Notes`가 있다.
- 세 역할은 억지로 채우지 않는다. 없는 역할은 적지 않거나 빈 글로 둔다. 셋 다 없으면 `required`다.
- `tools[].steps`·`transitions[].step`은 그 파일에 있는 단계 번호다(`range`). 전환의 `from`·`to`는 담당 다섯 가운데 하나이고 서로 다르다(`conflict`).
- 형식·장면 표는 데이터 4.16. 오류 코드는 4.9 표 그대로다.

## 5. 예외

- 경우별 결과는 데이터 5절 표.

## 6. 예시

- 모두 합성이다. 블록은 데이터 6절: 정상(6.1, = `TestData/case-schema/valid-full` = `guard/case-valid`), 최소(6.2), 필드 누락(6.3), 형식 오류(6.4), YAML 기능·중복(6.5), 추정 답 목록 오류(6.7). 오류 블록은 파서가 내는 줄 그대로다.

## 7. 테스트 표본

- 폴더 `tests/Scorecard.Tests/CaseSchema/`. 샘플은 `tests/Scorecard.Tests/TestData/case-schema/<이름>/case.yaml`(UTF-8, BOM 없음, LF). BOM·CRLF·UTF-16 같은 변형은 커밋하지 않고 테스트 코드가 만든다.
- 가드 표본 `TestData/guard/case-invalid/case.yaml`과 그 오류 줄은 데이터 7.3.
- 테스트는 데이터의 표 머리 행과 블록 제목을 읽는다. 머리 행은 그 파일에 한 번만 나온다. 표 모양을 바꾸면 테스트와 Core를 같이 고친다.
