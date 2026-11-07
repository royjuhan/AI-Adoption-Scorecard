# case-schema (T-003 스펙)

- 상태: 승인됨 (2026-11-06, juhan "제안대로"). 본문과 맨 끝 "사람 승인 결과"가 다르면 승인 결과가 우선한다.
- T-008 개정(승인됨, 2026-11-07. 승인 결과는 `docs/spec/human-signoff.md` 끝): 10번째 답 `human_signoff`(ADR 0003)와 단계 항목 `inferred`(ADR 0004 결정 2, 질문 16 해석 A). 바뀐 곳: 4.4, 4.9, 4.12, 새 4.13, 6.1·6.2 글, 새 6.7, 7.2, 문서 끝 "T-008 개정". 질문과 스파이크 결과는 `docs/spec/human-signoff.md`.
- 작업 카드: `docs/tasks/T-003-case-schema.md`
- 작성: prototyper, 2026-11-06
- 확인 대상 커밋: `abd34d0`
- 확인 환경: Ubuntu 24.04.5 LTS x64, .NET SDK 10.0.114, YamlDotNet 18.1.0, xUnit 2.9.3
- 스파이크: `spikes/T-003/` (머지 대상 아님. 머지 전에 빠진다. 이 문서의 `spikes/T-003/…` 경로는 스펙 초안 커밋 `c52cd56` 기준이다. main에는 없다). 실행은 `spikes/T-003/run-spike.sh <작업폴더> <로그폴더>`. 요약 원문은 `spikes/T-003/summary.txt`. 이 문서의 오류 줄은 스파이크 후보 파서(`spikes/T-003/core/C-events/`)가 실제로 낸 문자열이다.

## 0. 표기

- `[읽음]`: 커밋된 파일을 읽어서 확인한 사실.
- `[S0]`~`[S8]`: 스파이크 단계(9절). `[P:이름]`: S4 메모리 실험의 `PROBE 이름` 줄. `[T]`: S4 `TIMING` 줄(이 컴퓨터 기준 참고 시간).
- `[NuGet]`: nuget.org API로 2026-11-06에 조회한 사실.
- `[T8:S0]`~`[T8:S8]`, `[T8:P:무리]`: T-008 스파이크(`spikes/T-008/`) 단계와 S4 `PROBE <무리>` 줄. 단계 목록은 `human-signoff.md` 10절.
- **미확인**: 실험하지 않았거나 이 환경에서 확인할 수 없는 것.
- 후보: **A** 리플렉션 역직렬화(`DeserializerBuilder`), **B** 정적 생성기(`StaticDeserializerBuilder`), **C** 저수준 `Parser` 이벤트 + 손으로 쓴 스키마 검사.
- G1·G2·G3, N1~N6·D1·R1은 `docs/spec/guard-tests.md`의 이름이다.

## 1. 범위

카드 수용 기준 1~3과 카드 메모 4건을 다룬다. 수용 기준 4(CI 녹색)는 PR에서 확인한다.

- `case.yaml`의 필드·형식·필수 여부·기본값(지시문 3.2, 12.1).
- Core 파서: 읽기, 검증, 오류(어떤 필드인지, 줄·열).
- 카드 메모(T-002에서 넘어온 것):

| 메모 | 이 문서 | 승인 질문 |
| --- | --- | --- |
| 파서를 `GuardTargets`에 등록(T-002 승인 결정 7) | 4.11 등록 | 13 |
| 빈 출력끼리 비교해 통과하는 문제(T-002 reviewer 지적 7) | 4.11 빈 출력 규칙 | 12 |
| YamlDotNet을 N6 허용 목록에, PR 본문에 적음(T-002 승인 결정 3) | 4.2 | 2, 3 |
| 출력에는 입력 폴더 기준 상대 경로만(T-002 승인 결정 9) | 4.9 `Source`, 4.10 | 9, 13 |

범위 밖:

- 분류 트리, 근거 문장, "(기본값 사용, 확인 필요)" 문장. T-004. 이 카드는 그 근거인 `IsDefault`만 낸다.
- `answers.yaml` 형식, `interview`가 `case.yaml`을 쓰는 일. M2(cli-dev).
- `inputs`의 glob 해석, 대조, 허용 오차 계산. T-006.
- `thresholds` 기본값과 판정. 판정 카드(`docs/spec/metrics.md`).
- 입력 SHA-256 기록. 평가서 카드.
- `case.yaml` 쓰기(직렬화).

## 2. 입력

- `<케이스 폴더>/case.yaml` 한 파일. 읽기만 한다(`File.ReadAllBytes`). 폴더의 다른 파일은 열지 않는다(이름 목록만 본다). 이름은 정확히 `case.yaml`이어야 한다: 폴더 항목 이름을 서수로 비교하고, `Case.YAML`처럼 대소문자만 다르면 모든 OS에서 "파일이 없다". 목록을 읽을 권한이 없는 폴더는 "파일을 읽을 수 없다"가 된다(T-003 리뷰 판단 H1, 추천안. 사람 답 대기).
- 바이트 규칙은 4.8.

## 3. 출력

- 메모리 결과 `CaseParseResult` 하나(4.12). 모델 또는 오류 목록. 파서는 파일을 쓰지 않는다.
- G2·G3용 파일은 테스트 쪽 어댑터가 쓴다(4.11). T-002 스펙 4.4의 "메모리 결과 API는 테스트 쪽 어댑터가 파일로 쓴다"를 따른다 [읽음].

## 4. 규칙

### 4.1 파서 방식

세 후보를 같은 clone 방식으로 Core에 넣어 봤다 [S2]. 오류 모양은 A만 따로 실험했다 [P:A …].

| 항목 | A 리플렉션 | B 정적 생성기 | C Parser 이벤트 |
| --- | --- | --- | --- |
| 추가 패키지 | YamlDotNet | YamlDotNet, Vecc.YamlDotNet.Analyzers.StaticGenerator 18.1.0 | YamlDotNet |
| Release `-warnaserror` 빌드 | 통과 | 통과 | 통과 |
| Core G1 (허용 목록 전) | N6 `YamlDotNet` 1건 | 같음 | 같음 |
| Core G1 (허용 목록 후) | 0건, G1 테스트 42개 통과 | 같음 | 같음 |
| Core가 참조하는 YamlDotNet 네임스페이스 | `Serialization`, `Serialization.NamingConventions` | `Serialization` 계열 4개, `Core` | `Core`, `Core.Events`만 |
| 필수 필드 누락 | 예외 없음. 값이 null. 위치 없음 | 미확인 | `required` 오류, 줄·열 |
| 알 수 없는 키 | 예외. 메시지에 C# 타입 이름(`… not found on type 'ReflectionCase'`) | 미확인 | `unknown-field` 오류 |
| 형식 오류 | `Exception during deserialization` + 내부 `FormatException`. 첫 오류에서 멈춤 | 미확인 | 오류마다 한 줄. 모두 모음 |
| 불리언에 `yes` | `true`로 받음 | 미확인 | `format` 오류 |
| 빈 파일 | 예외 없음. 결과 null | 미확인 | `empty` 오류 |

- G1은 A·B·C를 가르지 못한다. 셋 다 Core에 남는 항목은 N6 하나다 [S2]. YamlDotNet 안의 리플렉션은 YamlDotNet.dll에 있고, G1은 Core·Cli dll만 본다(4.2).
- C는 필수 필드·위치·모든 오류 모음·엄격한 형식을 직접 정한다. A는 누락을 null로 삼키고, 위치를 잃고, 메시지에 내부 타입 이름이 나온다 [P:A missing-name][P:A unknown-key].
- C에는 정적 생성기가 필요 없다. 객체를 만들지 않기 때문이다.

결정(제안): C. → 승인 질문 1.

C의 구조(스파이크 코드 기준):

1. 바이트 → 문자열(4.8).
2. `YamlDotNet.Core.Parser`의 이벤트를 읽어 작은 트리(매핑·목록·스칼라)를 만든다. 이때 앵커·별칭·태그·중복 키·깊이를 본다(4.7).
3. 트리를 스키마 순서대로 걷는다. 필드마다 형식·필수 여부를 보고 오류를 모은다(4.3~4.6).
4. 오류가 없으면 모델을 돌려준다. 하나라도 있으면 모델 없이 오류 목록만 돌려준다.

### 4.2 YamlDotNet 패키지와 G1

| 항목 | 사실 |
| --- | --- |
| 최신 안정판 | 18.1.0, 2026-06-26 게시 [NuGet] |
| 라이선스 | MIT [S1] |
| 의존 패키지 | 0개. 대상 `net10.0`, `net8.0`, `net47`, `netstandard2.0`, `netstandard2.1` [S1] |
| 어셈블리 이름·버전 | `YamlDotNet`, 18.0.0.0 (패키지 18.1.0) [S2] |
| `YamlDotNet.dll`의 참조 | 프레임워크 어셈블리 12개뿐. `System.Net*`·`Process` 없음 [S1] |
| `YamlDotNet.dll`의 G1 항목(참고) | N1~N4 0건. N5 3건(`Activator::CreateInstance`, `Assembly::GetType`, `Type::GetType`), D1 1건(`System.Random`), R1 1건(`MethodBase::Invoke`) [S1] |
| 그 항목을 부르는 곳 | `YamlDotNet.Serialization.*`, `YamlDotNet.Helpers`, `YamlDotNet.RepresentationModel`(`Random`). `YamlDotNet.Core`·`YamlDotNet.Core.Events`에는 없다 [S1]. IL 바이트 단순 검색이다. C가 이 코드에 실제로 닿는지(호출 도달성)는 **미확인** |
| Cli | `scorecard.dll` G1 0건. YamlDotNet 참조 없음. 테스트 출력 폴더에 `YamlDotNet.dll`이 복사된다 [S2] |
| 전체 테스트 | C + 허용 목록에서 77개 통과 [S2] |
| 형식 | C 코드를 넣은 Core에서 `dotnet format --verify-no-changes` 종료 코드 0 [S2] |

규칙:

- Core csproj에 `<PackageReference Include="YamlDotNet" Version="18.1.0" />`. 버전은 정확히 고정한다.
- G1 N6 허용 목록(`NetworkGuardTests.AllowedThirdPartyAssemblies`)에 `"YamlDotNet"`을 더한다. 더하기 전 G1 Core 테스트는 `N6 non-framework asmref YamlDotNet` 1건으로 실패하고, 더한 뒤 통과한다 [S2]. PR 본문에 적는다(T-002 승인 결정 3). → 승인 질문 2.
- 허용 목록은 YamlDotNet 안의 N5·D1·R1을 G1 밖에 둔다. 그래서 Core가 쓰는 YamlDotNet 범위를 G1에 새 규칙으로 묶는 안을 둔다:

| ID | 메타데이터 표 | 금지 조건 (Core만) |
| --- | --- | --- |
| Y1 (제안) | TypeRef | 네임스페이스가 `YamlDotNet`이거나 `YamlDotNet.`으로 시작하는데, `YamlDotNet.Core`와 같지도 않고 `YamlDotNet.Core.`으로 시작하지도 않는다 |

- 항목 문자열은 `Y1 type <전체 이름>`. C는 0건이다(C의 TypeRef는 `YamlDotNet.Core`, `YamlDotNet.Core.Events`뿐 [S2]). A·B는 `YamlDotNet.Serialization…`이 걸린다 [S2].
- 자기 검사 카나리아: 테스트 프로젝트는 Core의 패키지 참조를 거쳐 `YamlDotNet.Serialization.DeserializerBuilder`, `YamlDotNet.RepresentationModel.YamlStream`을 직접 참조할 수 있다 [S8]. → 승인 질문 3.

### 4.3 문서 구조 (최상위)

| 키 | 형식 | 필수 | 없을 때 | 규칙 |
| --- | --- | --- | --- | --- |
| `name` | 문자열 | 예 | `required` | `^[a-z0-9]+(-[a-z0-9]+)*\z`, 64자 이하 [P:case name]. 폴더 이름과 같은지는 보지 않는다(G2 작업 폴더 이름이 `input`이다) |
| `task` | 문자열 | 예 | `required` | 자유 서술. 저장만 |
| `before` | 문자열 | 아니오 | null | 자유 서술. 저장만 |
| `after` | 문자열 | 아니오 | null | 자유 서술. 저장만 |
| `ai_usage` | 열거 | 아니오 | null (미입력. 기본값 없음) | `build-time` / `runtime` / `hybrid` |
| `steps` | 목록 | 예 | `required` | 1개 이상. 항목은 4.4 |
| `inputs` | 매핑 | 아니오 | null | 4.5 |
| `compare` | 목록 | 아니오 | 빈 목록 | 4.5 |
| `thresholds` | 매핑 | 아니오 | 세 값 모두 null | 4.5 |

- 위 표에 없는 키는 `unknown-field` 오류다. 키 순서는 상관없다.
- 필수 문자열이 비어 있으면(따옴표 없는 빈 값, `~`, `""`, 공백뿐인 `"  "`) `required` "값이 비어 있다" [P:step name].
- 자유 서술은 저장만 한다. 앞뒤 공백을 지우거나 정규화하지 않는다(`" 공백 "`은 그대로 [P:step name]). 블록 값(`|`)의 줄바꿈은 파일 줄바꿈(LF·CRLF·CR)과 상관없이 `\n`이다 [P:block-scalar].
- → 승인 질문 4.

### 4.4 단계와 답 10개

T-008 전 제목은 "단계와 질문 9개"였다. T-008이 `human_signoff`(표 끝 1행)와 단계 항목 `inferred`를 더했다.

단계 항목(`steps[n]`):

| 키 | 형식 | 필수 | 규칙 |
| --- | --- | --- | --- |
| `name` | 문자열 | 예 | 케이스 안에서 서수 비교로 중복 금지(`duplicate`). 유니코드 정규화는 하지 않는다. NFC `가`와 NFD `가`는 다른 이름이다 [P:nfc-nfd] |
| `answers` | 매핑 | 아니오 | 없거나 null이면 10개 모두 기본값 |
| `inferred` | 문자열 목록 | 아니오 | T-008. AI가 추정한 답의 키. 원소는 아래 `answers` 표의 키이고, 그 답은 `answers`에 값이 있어야 한다. 없거나 null이면 추정 답이 없다. 4.13 |

`answers`의 키(지시문 3.2와 같은 순서·같은 기본값). 테스트가 이 표를 읽는다(7절). 표 모양을 바꾸지 않는다.

| 키 | 질문 | 형식 | 선택지·범위 | 기본값 |
| --- | --- | --- | --- | --- |
| `input_kind` | 입력은 어떤 형태인가 | 열거 | table / fixed_form / free_doc / image / conversation | `fixed_form` |
| `rule_expressible` | 판단 기준을 글로 쓸 수 있나 | 열거 | yes / partial / no | `partial` |
| `frequency` | 얼마나 자주 반복되나 | 열거 | daily / weekly / monthly / rare | `weekly` |
| `batch_size` | 한 번에 몇 건인가 | 정수 | 1 이상 | `10` |
| `exception_rate` | 예외·이상 건 비율(%) | 숫자 | 0 이상 100 이하 | `20` |
| `current_actor` | 지금 누가 하나 | 열거 | human / ai_each_time / program / mixed | `human` |
| `minutes_per_item` | 건당 걸리는 시간(분) | 숫자 | 0 이상 | `null` |
| `failure_impact` | 틀리면 무슨 일이 생기나 | 열거 | internal_rework / external / safety_legal | `internal_rework` |
| `existing_feature` | 기존 프로그램에 같은 기능이 있나 | 열거 | yes / no / unknown | `unknown` |
| `human_signoff` | 사람이 최종 확인·서명하는 단계인가 | 열거 | yes / no | `no` |

기본값 규칙:

- 키가 없거나 값이 null(따옴표 없는 빈 값, `~`, `null`, `Null`, `NULL`)이면 기본값을 넣고 `IsDefault = true` [P:null rule_expressible]. 이것이 "기본값 사용, 확인 필요" 표시의 근거다(지시문 2절 6).
- 따옴표 있는 `""`, `"null"`은 문자열이다. 열거 값이 아니므로 `enum` 오류다 [P:null rule_expressible].
- 값을 직접 쓰면 `IsDefault = false`. `existing_feature: unknown`을 직접 써도 false다. "확인 항목"·"측정 권고" 처리는 T-004다.
- 값이 잘못되면 오류다. 기본값으로 바꿔 넣지 않는다.
- 지시문 12.1 예시는 모든 단계에서 `minutes_per_item`을 빼고 있다 [읽음]. 같은 구조의 합성 예시가 오류 없이 읽히고, 그 값은 `null`, `IsDefault = true`다 [S3 example-12-1].
- 3.2에 없는 범위를 정했다: `batch_size` 1 이상, `exception_rate` 소수 허용, `minutes_per_item` 0 이상. → 승인 질문 6.
- 기본값 표는 이 문서에 하나만 둔다. T-004의 `classification.md`는 이 표를 가리킨다. → 승인 질문 5.
- `human_signoff`(T-008, ADR 0003): 파서 규칙은 다른 열거 답과 같다. 없거나 null이면 `no`, `IsDefault = true`. `Yes`·`true`·`""`·`unknown`은 `enum` 오류다 [T8:P:mark]. `yes`·`no`는 YAML 1.1 불리언 낱말이지만 문자열로 들어온다(4.6). 따옴표가 있어도 같다 [T8:P:mark].
- `human_signoff`는 단계마다 묻지 않는다. 문답(exe 마법사·스킬)은 "사람이 최종 확인·서명하는 단계가 어디인가"를 한 번 묻고, 고른 단계는 `yes`, 나머지는 `no`로 모든 단계에 적는다(ADR 0003 결정). 그래서 문답으로 만든 파일에서는 기본값이 되지 않는다. 파일을 손으로 쓰고 키를 빼면 기본값이고, 분류 문장에 기본값 표시가 붙는다(classification.md 4.6). 지시문 2절 6에 이 예외를 적었다(juhan 결정 안 A, `human-signoff.md` 승인 결과).

### 4.5 inputs, compare, thresholds

`inputs` (있으면 세 키 모두 필수):

| 키 | 형식 | 규칙 |
| --- | --- | --- |
| `reference` | 문자열 | 케이스 폴더 기준 상대 경로. `/`로 시작하지 않고, `\`·`:`·`..` 조각이 없다(`format`) [S3 type-errors] |
| `candidate` | 문자열 | 같음 |
| `key` | 문자열 | 비어 있지 않음 |

- `*` 같은 glob 문자는 그대로 저장한다. 해석은 T-006.

`compare` 항목:

| 키 | 형식 | 필수 | 규칙 |
| --- | --- | --- | --- |
| `field` | 문자열 | 예 | `compare` 안에서 서수 비교로 중복 금지(`duplicate`) |
| `tolerance` | 숫자 | 아니오 | 0 이상. 절대 허용 오차 |
| `tolerance_pct` | 숫자 | 아니오 | 0 이상. 백분율 허용 오차 |

- `tolerance`와 `tolerance_pct`를 같이 쓰면 `conflict` [S3 type-errors].
- 둘 다 없으면 허용한다. 뜻(정확 일치·텍스트 정규화)은 T-006에서 정한다.
- `compare`에 항목이 있는데 `inputs` 키가 없거나 값이 null이면 `required` 경로 `inputs`, 위치는 `compare` 값 [P:edge compare-without-inputs].

`thresholds`:

| 키 | 형식 | 범위 | 없을 때 |
| --- | --- | --- | --- |
| `accuracy_min` | 숫자 | 0 이상 100 이하 | null (미입력) |
| `missing_max` | 숫자 | 0 이상 100 이하 | null (미입력) |
| `time_must_drop` | 불리언 | `true`·`false` | null (미입력) |

- T-003은 읽기만 한다. 기본값은 정하지 않는다. 공개 예제 `table-compare`(정확도 90% → "조건부")는 `accuracy_min`이 90 이하여야 하고, 12.1 예시는 95다 [읽음]. 기본값 하나로 둘을 맞출 수 없어 판정 카드에서 정한다. → 승인 질문 11.
- → `inputs`·`compare` 규칙은 승인 질문 10.

### 4.6 값 형식

| 형식 | 받는 값 | 받지 않는 값 [P:integer][P:number][P:boolean] |
| --- | --- | --- |
| 문자열 | 스칼라. 따옴표 여부 상관없음. null이면 값 없음 | 매핑·목록 → `type` |
| 열거 | 문자열이 선택지와 서수로 같다(대소문자 구분). 따옴표 허용(`"yes"`) | `Weekly`, `Table` → `enum` |
| 정수 | 따옴표 없는 `^-?(0\|[1-9][0-9]*)\z`. `InvariantCulture`로 읽는다 | `010`, `+1`, `1e3`, `.5`, `1.`, `1.50`, `0x10`, `.inf`, `1_000`, `"10"`, `'10'` → `format`. `-0`, `2147483648` → `range` |
| 숫자 | 따옴표 없는 `^-?(0\|[1-9][0-9]*)(\.[0-9]+)?\z`. `decimal`, `InvariantCulture` | `1,5`, `010`, `+1`, `1e3`, `.5`, `1.`, `0x10`, `.inf`, `1_000`, `"10"` → `format`. 범위 밖 → `range` |
| 불리언 | 따옴표 없는 `true`, `false` | `True`, `TRUE`, `yes`, `on`, `"true"`, `1` → `format` |
| null | 따옴표 없는 빈 값, `~`, `null`, `Null`, `NULL` (YAML 1.2 core 스키마) | `""`, `"null"`, `''`은 문자열 |

- YAML 1.1 불리언 낱말(`yes`, `no`)은 열거 값으로 그대로 들어온다. 따옴표가 있어도 없어도 같다 [P:yes-no]. 12.1의 `rule_expressible: yes`, `existing_feature: no`가 그대로 읽힌다 [S3 example-12-1].
- `decimal`은 소수 자릿수를 보존한다. `1.50`은 `1.50`, `1.5`는 `1.5`로 남는다 [P:number]. 값은 같고(`==`), 출력 문자열은 다르다. 같은 입력이면 같은 출력이다.
- 문화권: 6개 문화권(Invariant 2회, de-DE, tr-TR, ko-KR, ar-SA)에서 결과 바이트가 같다 [P:determinism]. 정규식은 `CultureInvariant`, 이름 비교는 서수다.
- → 승인 질문 6.

### 4.7 YAML 기능 제한

| 기능 | 처리 | 근거 |
| --- | --- | --- |
| 문서 둘 이상(`---`) | `multi-document`, 둘째 문서 시작 위치. 첫 문서는 계속 검사 | [S3 multi-doc] |
| 앵커 `&` | `alias` "앵커(&)는 쓰지 않는다" | [S3 alias-tag] |
| 별칭 `*` | `alias` "별칭(*)은 쓰지 않는다". 그 자리는 더 검사하지 않는다 | [S3 alias-tag] |
| 병합 키 `<<` | 특별 취급 없음. 일반 키라서 `unknown-field` | [S3 alias-tag] |
| 태그 `!x`, `!!int` | `tag` | [S3 alias-tag] |
| 같은 매핑에 같은 키 | `duplicate-key`, 둘째 키 위치. 첫 값을 쓴다 | [S3 duplicates] |
| 키가 스칼라가 아님(`? [x]`) | `type` "키는 문자열이어야 한다", 그 키 위치 | [P:edge complex-key] |
| 깊이 16 초과 (루트 매핑이 1단계, 스칼라는 세지 않는다) | `depth`, 그 한 건만 내고 읽기를 멈춘다 | [P:deep-nesting] |

- 별칭 폭탄(20단계 × 4배)은 펼치지 않고 오류 117건으로 끝난다 [P:alias-bomb].
- 깊이: YamlDotNet `Parser`는 흐름 중첩(`[[[…]]]`)이 깊어질수록 깊이 비례보다 빨리 느려진다. 이벤트를 끝까지 읽는 데 깊이 1만 536ms, 2만 1,456ms, 3만 5,757ms [T]. 처음 40개 이벤트만 읽으면 깊이 10만·100만도 2ms 이하 [T]. 그래서 깊이를 넘으면 건너뛰지 않고(`SkipThisAndNestedEvents`) 바로 멈춘다. 이렇게 하면 깊이 10만 입력도 2초 안에 `depth` 1건으로 끝난다 [P:deep-nesting 100000].
- 평평한 큰 파일은 빠르다. 단계 1천 개(약 0.2 MB) 25ms, 1만 개(약 2.1 MB) 227ms [T]. 그래서 파일 크기 상한은 두지 않는다.
- 고수준 `YamlStream`은 중복 키에서 `YamlException "Duplicate key a"`로 멈추고, 병합 키를 펼치지 않는다 [P:yamlstream duplicate]. C는 `YamlStream`을 쓰지 않는다.
- → 승인 질문 7.

### 4.8 인코딩·줄바꿈

| 입력 바이트 | 결과 | 근거 |
| --- | --- | --- |
| UTF-8, BOM 없음, LF | 정상 | [P:encoding lf] |
| UTF-8 BOM(`EF BB BF`) 하나 | BOM을 떼고 정상. LF 결과와 바이트까지 같음 | [P:encoding bom] |
| CRLF, CR만, BOM+CRLF | 정상. LF 결과와 바이트까지 같음. 줄 번호도 같음 | [P:encoding crlf/cr/bom-crlf] |
| BOM 둘 | 둘째 BOM이 글자(U+FEFF)로 남아 `syntax` (2:5) | [P:encoding double-bom] |
| UTF-16 BOM(`FF FE`, `FE FF`) | `encoding` "UTF-16이다. UTF-8로 저장한다", 1줄 | [P:encoding utf16le-bom/utf16be-bom] |
| UTF-32 BE BOM(`00 00 FE FF`) | `encoding` "UTF-8이 아니다 (바이트 2)" | [P:encoding utf32be-bom] |
| 잘못된 UTF-8(예: CP949 한글) | `encoding` "UTF-8이 아니다 (바이트 N)". 줄 번호는 앞선 LF 수 + 1. 열 없음 | [P:encoding cp949-line3] (3줄, 바이트 70), [P:encoding bom-then-bad] |
| 한글 포함 UTF-16, BOM 없음 | `encoding` (바이트 5) | [P:encoding utf16le-nobom] |
| ASCII만 있는 UTF-16, BOM 없음 | `encoding`이 아니라 `syntax` (1:3) | [P:encoding utf16le-ascii-nobom] |
| 값 안의 NUL 바이트 | `syntax` (2:9) | [P:encoding nul-in-value] |

- 바이트 N은 파일 처음부터 0으로 센 위치다. BOM 3바이트를 포함한다 [P:encoding bom-then-bad].
- 디코딩은 `new UTF8Encoding(false, throwOnInvalidBytes: true)`로 한다. 피해야 할 함정 두 가지:
  - `StreamReader`(BOM 감지 켬)는 UTF-16 BOM 파일을 오류 없이 UTF-16으로 읽는다 [P:streamreader utf16le-bom]. "UTF-8만"을 지키려면 BOM을 직접 본다.
  - `Encoding.UTF8.GetString`은 BOM을 U+FEFF 글자로 남긴다 [P:utf8.getstring bom]. YamlDotNet은 이 글자를 떼지 않는다. 첫 키가 U+FEFF로 시작하는 `name`이 되거나, 첫 줄이 주석이면 문법 오류가 난다 [P:yamlstream bom-char].
- CR만 쓰는 파일의 `encoding` 줄 번호는 LF로 세므로 YAML 줄 번호와 다를 수 있다(설계상 한계. 실험 안 함).
- → 승인 질문 8.

### 4.9 오류

오류 하나는 `CaseError(Source, Line, Column, Code, Path, Message)`다.

- `Source`: 파일 이름. `ParseDirectory`는 늘 `case.yaml`. 절대 경로를 쓰지 않는다 [P:missing-file].
- `Line`·`Column`: 1부터. 0이면 없음. 줄바꿈은 LF·CRLF·CR 모두 한 줄. 열은 UTF-16 코드 단위다. 한글 한 글자는 1칸, 이모지(서로게이트 쌍)는 2칸이다 [P:column].
- `Path`: 일반 경로. 매핑은 `.`, 목록 항목은 `[n]`이고 n은 1부터다. 예: `steps[2].answers.batch_size`. 문서 전체 오류는 빈 문자열.
- `Message`: 한국어. `syntax`는 YamlDotNet의 영어 메시지를 "YAML 문법 오류: " 뒤에 그대로 붙인다.

문자열 형식(`ToString()`):

```
<Source>:<Line>:<Column>: <Code> <Path>: <Message>
```

- 열이 0이면 `<Source>:<Line>:`, 줄도 0이면 `<Source>:`. 경로가 비면 `<Code>: <Message>`.

코드:

| 코드 | 언제 | 위치 | 메시지(스파이크 출력) |
| --- | --- | --- | --- |
| `file` | `case.yaml`이 없음 / 읽을 수 없음 | 없음 | `파일이 없다` / `파일을 읽을 수 없다: <예외 타입 이름>` |
| `encoding` | 4.8 | 줄만 | `UTF-16이다. UTF-8로 저장한다` / `UTF-8이 아니다 (바이트 N)` |
| `empty` | 문서가 없음(빈 파일, 주석만, 루트가 null) | 없음 | `내용이 없다` |
| `syntax` | YamlDotNet `YamlException` / 다른 파서 예외(아래 글머리) | 예외의 시작 위치 / 다른 파서 예외는 위치 없음 | `YAML 문법 오류: <영어 메시지>` |
| `depth` | 깊이 16 초과 | 그 노드 | `너무 깊다. 16단계까지 쓴다` |
| `multi-document` | 문서 둘 이상 | 둘째 문서 시작 | `문서가 둘 이상이다. 하나만 쓴다` |
| `alias` | 앵커·별칭 | 그 노드 | `앵커(&)는 쓰지 않는다` / `별칭(*)은 쓰지 않는다` |
| `tag` | 태그 | 그 노드 | `태그(!)는 쓰지 않는다` |
| `duplicate-key` | 같은 매핑에 같은 키 | 둘째 키 | `중복된 키다 (처음: N줄)` |
| `type` | 매핑·목록·스칼라 자리가 틀림 | 그 값 | `매핑이어야 한다` / `목록이어야 한다` / `스칼라 값이어야 한다` |
| `unknown-field` | 표에 없는 키 | 그 키 | `알 수 없는 필드다` |
| `required` | 필수 필드 없음 / 값이 빔 / 단계 0개 / `compare`에 `inputs` 없음 / `inferred` 원소가 null / `inferred`에 적은 답에 값이 없음(T-008) | 없음: 부모 매핑 시작. 빔: 그 값. 단계 0개: 그 목록. `inputs`: `compare` 값. `inferred`: 그 원소(경로는 null 원소면 `steps[n].inferred[m]`, 값이 없으면 `steps[n].answers.<키>`) | `필수 필드가 없다` / `값이 비어 있다` / `단계가 1개 이상 있어야 한다` / `compare가 있으면 inputs가 있어야 한다` / `inferred에 적은 답은 answers에 값이 있어야 한다` |
| `enum` | 선택지 밖 / `inferred` 원소가 답 키가 아님(T-008) | 그 값 / 그 원소 | `선택지 밖의 값 '<값>'. 가능한 값: <목록>` (`inferred`의 목록은 4.4 답 키 10개) |
| `format` | 정수·숫자·불리언·이름·경로 모양 | 그 값 | `정수가 아니다: <값>` / `숫자가 아니다: <값>` (따옴표가 있으면 ` (따옴표 없이 쓴다)`) / `true 또는 false로 쓴다: <값>` / `소문자·숫자·'-'만, 64자까지 쓴다` / 경로 문구 |
| `range` | 범위 밖 | 그 값 | `범위 밖이다 (<범위>): <값>` |
| `duplicate` | 단계 이름·비교 필드·`inferred` 키(T-008) 중복 | 둘째 값 | `단계 이름이 중복된다 (처음: steps[n])` / `비교 필드가 중복된다 (처음: compare[n])` / `추정 답 키가 중복된다 (처음: steps[n].inferred[m])` |
| `conflict` | `tolerance`와 `tolerance_pct` 같이 | 그 항목 시작 | `tolerance와 tolerance_pct 중 하나만 쓴다` |

모으기·순서:

- `file`, `encoding`, `empty`, `syntax`, `depth`는 그 한 건만 낸다. 다른 검사를 하지 않는다 [S3 empty, yaml-syntax][P:deep-nesting].
- 나머지는 모두 모은다. 첫 오류에서 멈추지 않는다 [S3 type-errors 18건].
- `syntax`의 다른 형태(T-003 리뷰 보강): 파서가 `YamlException`이 아닌 예외를 내면 위치 없이(0:0) `YAML 문법 오류: 파서가 읽지 못했다: <예외 타입 이름>` 한 건을 낸다. 예외 메시지는 쓰지 않는다. 예: 흐름 목록의 plain 스칼라 안 U+2028(YamlDotNet 18.1.0이 `InvalidOperationException`).
- 순서는 (줄, 열) 오름차순. 같은 위치는 찾은 순서(안정 정렬). 찾는 순서는 4.3 표 순서다. 예: 부모 매핑 하나에 필수 필드 둘이 없으면 표 순서로 나온다.
- 오류 개수 상한은 없다(별칭 폭탄 117건 [P:alias-bomb]).
- → 승인 질문 9.

### 4.10 결정론·원본 불변·상대 경로

- 같은 바이트 → 같은 모델·같은 오류 문자열. 확인: 프로세스 두 번, de-DE, 다른 경로 사본 [S4], 문화권 6종 [P:determinism], G2 R1~R4 [S5]. 스크립트를 여러 번 돌렸을 때 같은 실험의 결과 해시가 매번 같았다(9절).
- 파서는 난수·시각·환경 변수·현재 폴더를 쓰지 않는다. 숫자는 `InvariantCulture`, 문자열 비교는 서수.
- 파서는 입력을 읽기만 한다. G3 통과 [S5].
- 출력(오류 문자열 포함)에 절대 경로를 쓰지 않는다(T-002 승인 결정 9). `ParseDirectory`는 `caseDirectory`를 결과에 넣지 않는다. 입출력 예외의 `Message`는 절대 경로를 담으므로 쓰지 않고 예외 타입 이름만 쓴다.
- 확인: 파서가 오류 줄의 `Source`에 절대 경로를 쓰게 바꾸면 G2가 오류 표본에서 `G2 R3 다른 경로: out/parse-result.json SHA-256 다름`으로 잡는다. 정상 표본은 오류 줄이 없어 통과한다 [S7]. 그래서 오류 표본도 G2 대상이어야 한다.

### 4.11 G2·G3 등록과 빈 출력 규칙

등록(카드 수용 기준 3, T-002 승인 결정 7):

- 테스트 쪽 `GuardTargets`에 대상 둘을 둔다 [S5].

| 이름 | 샘플(`TestData/guard/…`) | 내용 |
| --- | --- | --- |
| `case-parser-valid` | `case-valid/case.yaml` | 6.1의 정상 예시 |
| `case-parser-invalid` | `case-invalid/case.yaml` | 필드 누락·형식 오류·알 수 없는 필드를 섞은 합성 예시(7.3) |

- 어댑터: `CaseParser.ParseDirectory(input)` 결과를 출력 폴더의 `parse-result.json` 한 파일로 쓴다. 내용은 `{ "case": <모델 또는 null>, "errors": [<ToString() 문자열>…] }`. `System.Text.Json`, `WriteIndented = true`, `NewLine = "\n"`, `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, UTF-8(BOM 없음), 끝에 `\n`. 테스트 코드라 G1 대상이 아니다.
- 결과 [S5]: 두 대상 모두 G2(R1~R4)·G3 차이 0건. 전체 테스트 83개 통과.
- `TestData/guard/basic/case.yaml`은 그대로 둔다. T-002 가짜 대상의 바이트 샘플이고, 이 스키마에 맞지 않는다(`steps: 2`) [읽음].

빈 출력 규칙(T-002 reviewer 지적 7):

- G2: R1 출력 폴더에 1바이트 이상인 파일이 하나도 없으면 `G2 R1 기준: 출력 파일 없음`을 차이로 적는다. R2~R4 비교는 그대로 한다.
- 근거 [S5][S6]: 가짜 대상 `no-output`(빈 폴더만 만듦)과 `empty-file`(0바이트 파일 하나)이 이 줄 하나만 낸다. 기존 가짜 대상 12종의 결과는 바뀌지 않는다(모두 출력을 쓴다 [읽음 FakeTargets.cs]). 어댑터가 파일을 쓰지 않게 바꾸면 실제 대상 둘이 이 규칙으로 실패한다 [S6].
- G3에는 넣지 않는다. G3는 입력이 그대로인지를 본다. 출력이 없어도 입력 보존은 참이다. 같은 대상이 G2에도 등록되므로 G2가 잡는다.
- 0바이트 파일을 출력으로 치지 않는 이유: 0바이트 파일끼리 비교해도 빈 통과다 [S5 empty-file].
- 기존 테스트를 약화하지 않는다. 검사를 더하는 변경이다.
- → 승인 질문 12·13.

### 4.12 Core 공개 API (제안)

네임스페이스 `Scorecard.Core.Cases`. 스파이크 C와 같은 모양이다(`spikes/T-003/core/C-events/CaseModel.cs`).

```csharp
public static class CaseParser
{
    public const string FileName = "case.yaml";

    // <caseDirectory>/case.yaml을 읽는다. 오류의 Source는 "case.yaml".
    public static CaseParseResult ParseDirectory(string caseDirectory);

    // 바이트를 검증한다. sourceName은 오류의 Source(상대 이름).
    public static CaseParseResult Parse(byte[] content, string sourceName);
}

// 오류가 하나라도 있으면 Case는 null.
public sealed record CaseParseResult(CaseDefinition? Case, IReadOnlyList<CaseError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record CaseError(string Source, int Line, int Column, string Code, string Path, string Message);
// ToString()은 4.9 형식.

public sealed record CaseDefinition(
    string Name, string Task, string? Before, string? After, string? AiUsage,
    IReadOnlyList<CaseStep> Steps, CaseInputs? Inputs,
    IReadOnlyList<CompareField> Compare, CaseThresholds Thresholds);

public sealed record CaseStep(int Number, string Name, StepAnswers Answers);   // Number는 1부터

// T-008: 10번째 HumanSignoff. 위치 매개변수라 기존 9인자 생성은 컴파일되지 않는다.
public sealed record StepAnswers(
    Answer<string> InputKind, Answer<string> RuleExpressible, Answer<string> Frequency,
    Answer<int> BatchSize, Answer<decimal> ExceptionRate, Answer<string> CurrentActor,
    Answer<decimal?> MinutesPerItem, Answer<string> FailureImpact, Answer<string> ExistingFeature,
    Answer<string> HumanSignoff);

// T-008: IsInferred는 선택 인자(기본 false). 기존 2인자 생성은 그대로 컴파일된다 [T8:S2].
public sealed record Answer<T>(T Value, bool IsDefault, bool IsInferred = false);

public sealed record CaseInputs(string Reference, string Candidate, string Key);
public sealed record CompareField(string Field, decimal? Tolerance, decimal? TolerancePct);
public sealed record CaseThresholds(decimal? AccuracyMin, decimal? MissingMax, bool? TimeMustDrop);
```

- 열거 값은 YAML 문자열 그대로(`"fixed_form"`) 둔다. C# enum으로 바꾸지 않는다. 스펙 표와 1:1로 대조된다.
- 숫자는 `decimal`. 이진 소수 반올림이 허용 오차 비교(T-006)에 섞이지 않게 한다.
- 오류 목록 `Code`는 4.9 표의 문자열.
- YamlDotNet 타입은 공개 API에 나오지 않는다. 트리·검사 코드는 `internal`.
- → 승인 질문 14.
- T-008 개정: 공개 타입 목록은 그대로다(새 타입 없음). `CaseStep`은 바꾸지 않는다. 추정 여부는 답마다 `IsInferred`로 둔다(ADR 0004 "추정 답 표시"). 레코드 값 비교에 `IsInferred`가 들어간다. `inferred`가 없는 파일은 모든 답이 false라 기존 비교(`new Answer<string>("no", false)` 등)가 그대로 맞는다 [T8:S2].

### 4.13 추정 답 목록 `inferred` (T-008)

ADR 0004 결정 2: "단계마다 선택 항목 `inferred: [키 목록]`을 두고, case-schema 4.4에 같은 PR에서 추가한다. 예제에는 넣지 않는다" [읽음]. 질문 16 해석 A로 T-008이 맡는다.

```yaml
steps:
  - name: 주문서 양식 읽기
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: weekly, batch_size: 20, human_signoff: no}
    inferred: [frequency, batch_size]
```

규칙(제안):

- 단계 항목의 선택 키다. 없거나 null(`inferred:`, `~`)이면 추정 답이 없다. 빈 목록 `[]`도 같다 [T8:P:inferred].
- 흐름 목록(`[a, b]`)과 블록 목록(`- a`) 모두 받는다. 단계 항목 안의 키 순서는 상관없다(`inferred`가 `answers` 앞에 있어도 된다) [T8:P:inferred-error].
- 목록이 아니면(스칼라, 매핑) `type` `목록이어야 한다`. 위치는 그 값, 경로 `steps[n].inferred`.
- 원소는 스칼라 문자열이다. 매핑·목록이면 `type` `스칼라 값이어야 한다`. null 원소(`[~]`)는 `required` `값이 비어 있다`. 경로는 `steps[n].inferred[m]`(m은 1부터).
- 원소는 4.4 `answers` 표의 키 10개 중 하나다(서수, 대소문자 구분, 따옴표 상관없음). 아니면 `enum`. 메시지의 가능한 값은 4.4 표 순서의 키 10개다. `name`·`yes`·`Frequency`도 `enum`이다 [T8:P:inferred-error].
- 같은 키가 둘 이상이면 둘째부터 `duplicate` `추정 답 키가 중복된다 (처음: steps[n].inferred[m])`.
- 적은 키의 답은 `answers`에 값이 있어야 한다. 키가 없거나 null(`answers`가 없는 단계 포함)이면 `required` `inferred에 적은 답은 answers에 값이 있어야 한다`. 위치는 그 원소, 경로는 `steps[n].answers.<키>`. 값이 없는 답은 기본값이지 추정이 아니기 때문이다(ADR 0004). 경로·위치 모양은 `compare가 있으면 inputs가 있어야 한다`(경로 `inputs`, 위치 `compare` 값)와 같다.
- 값이 있지만 잘못된 답(예: `rule_expressible: maybe`)은 그 답의 오류 한 건만 난다. `inferred` 쪽 오류는 더하지 않는다 [T8:P:inferred-error].
- `answers`가 매핑이 아니면 `answers`의 `type` 오류와 `inferred` 원소마다의 `required`가 같이 난다(값이 없으므로) [T8:P:inferred-error].
- 결과: 목록에 있는 답만 `IsInferred = true`. 값·`IsDefault`는 그대로다. 그래서 한 답이 `IsDefault`이면서 `IsInferred`일 수 없다.
- 순서: 쓴 순서와 상관없다. 분류 결과의 키 목록은 4.4 표 순서다(classification.md 4.12) [T8:P:inferred].
- `inferred`는 분류(담당·근거 키·행)를 바꾸지 않는다. 근거 문장 표시만 바꾼다(classification.md 4.12).
- 공개 예제 3종에는 넣지 않는다(결정 2). 테스트는 TestData 표본(6.7, classification.md 6.5)으로 한다.
- → `human-signoff.md` 승인 질문.

## 5. 예외

| 상황 | 결과 | 근거 |
| --- | --- | --- |
| `ParseDirectory`에 빈 문자열·null | `ArgumentException`·`ArgumentNullException` 예외. 현재 폴더를 쓰지 않는다(4.10). 상대 경로(`.`)는 받는다 | T-003 리뷰 2회차 |
| `case.yaml` 없음, 폴더 없음 | `case.yaml: file: 파일이 없다`. 절대 경로 없음 | [P:missing-file] |
| 빈 파일, 주석만 | `case.yaml: empty: 내용이 없다` | [S3 empty, comment-only] |
| 루트가 목록 | `case.yaml:1:1: type: 매핑이어야 한다` | [S3 root-list] |
| `steps: []` | `case.yaml:3:8: required steps: 단계가 1개 이상 있어야 한다` | [P:edge empty-steps] |
| `steps`가 매핑 | `type steps: 목록이어야 한다` | [P:edge steps-mapping] |
| 문법 오류 | `syntax` 한 건 | [S3 yaml-syntax] |
| 탭 들여쓰기 | `syntax` "found invalid tab as indentation". 위치가 탭 자리가 아니라 문서 앞(1:1)이다 | [P:edge tab-indent-root, tab-indent-nested] |
| UTF-16·CP949 | `encoding` 한 건 | [P:encoding …] |
| 깊이 10만 | `depth` 한 건, 2초 안 | [P:deep-nesting 100000] |
| 오류와 정상 값이 섞임 | 모델 없음, 오류만 | 4.12 설계 |
| NFC·NFD로 같은 단계 이름 | 다른 이름. 중복 오류 없음 | [P:nfc-nfd] |
| 매우 큰 평평한 파일(약 2.1 MB) | `Parser` 이벤트 읽기 227ms | [T] |
| publish 단일 exe(`win-x64`)에서 동작 | **미확인** | — |

## 6. 예시

모두 합성이다. 실제 업무·값 없음.

### 6.1 정상 (전체)

`spikes/T-003/samples/valid-full/case.yaml`. 결과 오류 0건 [S3].

```yaml
# 합성 예시. 실제 업무·값 없음.
name: sample-full
task: 합성 센서 로그에서 구간을 골라 평균을 낸다
before: 사람이 그래프를 보고 구간을 고른다
after: |
  프로그램이 구간을 고른다.
  사람이 검토한다.
ai_usage: build-time
steps:
  - name: 로그 파일 열기
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: weekly, batch_size: 10, exception_rate: 5, current_actor: program, failure_impact: internal_rework, existing_feature: no}
  - name: 구간 선택(불규칙)
    answers:
      input_kind: table
      rule_expressible: partial
      frequency: weekly
      batch_size: 3
      exception_rate: 62.5
      current_actor: human
      minutes_per_item: 7.5
      failure_impact: internal_rework
      existing_feature: no
  - name: 결과 확인
    answers:
      rule_expressible: "no"
      failure_impact: external
      minutes_per_item: ~
  - name: 보고서 채우기
inputs:
  reference: reference/*.csv
  candidate: candidate/*.csv
  key: row_id
compare:
  - {field: start, tolerance: 0}
  - {field: mean, tolerance_pct: 1.0}
  - {field: label}
thresholds:
  accuracy_min: 90
  missing_max: 5
  time_must_drop: true
```

읽힌 값 일부 [S3 결과 JSON]:

| 위치 | 값 | IsDefault |
| --- | --- | --- |
| 1단계 `minutes_per_item` | null | true (키 없음) |
| 2단계 `exception_rate` | 62.5 | false |
| 3단계 `input_kind` | fixed_form | true (키 없음) |
| 3단계 `rule_expressible` | no | false (`"no"` 따옴표) |
| 3단계 `minutes_per_item` | null | true (`~`) |
| 4단계 10개 전부 | 3.2 기본값 | true (`answers` 없음) |
| 1~4단계 `human_signoff` | no | true (키 없음, T-008) |
| `after` | `프로그램이 구간을 고른다.\n사람이 검토한다.\n` | — |

- T-008: 위 블록은 `human_signoff`를 적지 않았다. 그대로 둔다. 이 블록은 `TestData/case-schema/valid-full`·`TestData/guard/case-valid`와 같아야 한다(T-003 테스트). 블록을 바꾸면 G2·G3 표본까지 바뀐다. 그대로 두면 표본이 "`human_signoff` 기본값 → 기본값 표시" 경로를 탄다. 분류 결과의 기본값 표시는 3행에서 5행이 된다(classification.md 4.10) [T8:S4 guard].

### 6.2 최소

```yaml
name: sample-minimal
task: 최소 합성 예시
steps:
  - name: 한 단계
```

오류 0건. `ai_usage`·`before`·`after`·`inputs` null, `compare` 빈 목록, `thresholds` 세 값 null, 답 9개 모두 기본값 [S3 valid-minimal]. T-008 뒤에는 `human_signoff`까지 10개 모두 기본값이다.

### 6.3 필드 누락

```yaml
task: 필수 필드가 빠진 합성 예시
steps:
  - answers: {input_kind: table}
  - name: ""
  - name: 둘째 단계
inputs:
  reference: reference/*.csv
  candidate: candidate/*.csv
```

오류 [S3 missing-required]:

```
case.yaml:1:1: required name: 필수 필드가 없다
case.yaml:3:5: required steps[1].name: 필수 필드가 없다
case.yaml:4:11: required steps[2].name: 값이 비어 있다
case.yaml:7:3: required inputs.key: 필수 필드가 없다
```

### 6.4 형식 오류

`spikes/T-003/samples/type-errors/case.yaml`:

```yaml
name: Sample_Bad
task: 형식 오류 합성 예시
ai_usage: always
steps:
  - name: 첫 단계
    answers:
      input_kind: Table
      frequency: Weekly
      batch_size: ten
      exception_rate: 120
      minutes_per_item: "5"
      existing_feature: maybe
  - name: 둘째 단계
    answers:
      batch_size: 0
      exception_rate: 1,5
      current_actor: [human]
  - 셋째 단계
compare:
  - {field: a, tolerance: 1, tolerance_pct: 2}
  - {field: b, tolerance: -1}
inputs:
  reference: /data/ref.csv
  candidate: ../candidate/*.csv
  key: id
thresholds:
  accuracy_min: 101
  time_must_drop: yes
```

오류 18건 [S3 type-errors]:

```
case.yaml:1:7: format name: 소문자·숫자·'-'만, 64자까지 쓴다
case.yaml:3:11: enum ai_usage: 선택지 밖의 값 'always'. 가능한 값: build-time, runtime, hybrid
case.yaml:7:19: enum steps[1].answers.input_kind: 선택지 밖의 값 'Table'. 가능한 값: table, fixed_form, free_doc, image, conversation
case.yaml:8:18: enum steps[1].answers.frequency: 선택지 밖의 값 'Weekly'. 가능한 값: daily, weekly, monthly, rare
case.yaml:9:19: format steps[1].answers.batch_size: 정수가 아니다: ten
case.yaml:10:23: range steps[1].answers.exception_rate: 범위 밖이다 (0 이상 100 이하): 120
case.yaml:11:25: format steps[1].answers.minutes_per_item: 숫자가 아니다: 5 (따옴표 없이 쓴다)
case.yaml:12:25: enum steps[1].answers.existing_feature: 선택지 밖의 값 'maybe'. 가능한 값: yes, no, unknown
case.yaml:15:19: range steps[2].answers.batch_size: 범위 밖이다 (1 이상 2147483647 이하): 0
case.yaml:16:23: format steps[2].answers.exception_rate: 숫자가 아니다: 1,5
case.yaml:17:22: type steps[2].answers.current_actor: 스칼라 값이어야 한다
case.yaml:18:5: type steps[3]: 매핑이어야 한다
case.yaml:20:5: conflict compare[1]: tolerance와 tolerance_pct 중 하나만 쓴다
case.yaml:21:27: range compare[2].tolerance: 범위 밖이다 (0 이상): -1
case.yaml:23:14: format inputs.reference: 케이스 폴더 기준 상대 경로를 '/'로 쓴다 ('..', ':', '\' 없음)
case.yaml:24:14: format inputs.candidate: 케이스 폴더 기준 상대 경로를 '/'로 쓴다 ('..', ':', '\' 없음)
case.yaml:27:17: range thresholds.accuracy_min: 범위 밖이다 (0 이상 100 이하): 101
case.yaml:28:19: format thresholds.time_must_drop: true 또는 false로 쓴다: yes
```

### 6.5 YAML 기능·중복

`alias-tag`:

```yaml
name: sample-alias
task: !custom 태그
base: &common {frequency: weekly}
steps:
  - name: a
    answers: *common
  - name: b
    answers:
      <<: *common
      batch_size: !!int 3
```

오류 [S3]:

```
case.yaml:2:7: tag task: 태그(!)는 쓰지 않는다
case.yaml:3:1: unknown-field base: 알 수 없는 필드다
case.yaml:3:7: alias base: 앵커(&)는 쓰지 않는다
case.yaml:6:14: alias steps[1].answers: 별칭(*)은 쓰지 않는다
case.yaml:9:7: unknown-field steps[2].answers.<<: 알 수 없는 필드다
case.yaml:9:11: alias steps[2].answers.<<: 별칭(*)은 쓰지 않는다
case.yaml:10:19: tag steps[2].answers.batch_size: 태그(!)는 쓰지 않는다
```

`duplicates`:

```yaml
name: sample-dup
task: 중복 합성 예시
name: sample-dup-2
steps:
  - name: 같은 이름
    answers: {batch_size: 1, batch_size: 2}
  - name: 같은 이름
compare:
  - {field: x}
  - {field: x}
inputs: {reference: r/*.csv, candidate: c/*.csv, key: k}
```

오류 [S3]:

```
case.yaml:3:1: duplicate-key name: 중복된 키다 (처음: 1줄)
case.yaml:6:30: duplicate-key steps[1].answers.batch_size: 중복된 키다 (처음: 6줄)
case.yaml:7:11: duplicate steps[2].name: 단계 이름이 중복된다 (처음: steps[1])
case.yaml:10:13: duplicate compare[2].field: 비교 필드가 중복된다 (처음: compare[1])
```

### 6.6 지시문 12.1 구조

`spikes/T-003/samples/example-12-1/case.yaml`은 12.1 예시와 키·단계 수·답이 같고 이름·문장만 합성으로 바꿨다. 오류 0건 [S3]. 비공개 케이스(`cases/`)는 저장소에 넣지 않는다.

### 6.7 추정 답 목록 오류 (T-008)

TestData 표본 후보 `tests/Scorecard.Tests/TestData/case-schema/inferred-errors/case.yaml`. 실제 값 없음. `enum`·`duplicate`·`required`·`type`을 모두 낸다(T-008 카드 수용 기준 3).

```yaml
# 합성 오류 예시. 실제 업무·값 없음. 추정 답 목록(inferred)의 오류를 섞었다.
name: inferred-errors
task: 추정 답 목록 오류 합성 예시
steps:
  - name: 답 키가 아님
    answers: {frequency: weekly, batch_size: 10, human_signoff: no}
    inferred: [frequency, freq, Batch_size, name]
  - name: 중복
    answers: {frequency: weekly, human_signoff: no}
    inferred: [frequency, frequency]
  - name: 값 없음
    answers: {frequency: weekly, minutes_per_item: ~, human_signoff: no}
    inferred: [batch_size, minutes_per_item, ~]
  - name: 목록 아님
    answers: {human_signoff: no}
    inferred: human_signoff
```

오류 8건 [T8:S8 inferred-errors]:

```text
case.yaml:7:27: enum steps[1].inferred[2]: 선택지 밖의 값 'freq'. 가능한 값: input_kind, rule_expressible, frequency, batch_size, exception_rate, current_actor, minutes_per_item, failure_impact, existing_feature, human_signoff
case.yaml:7:33: enum steps[1].inferred[3]: 선택지 밖의 값 'Batch_size'. 가능한 값: input_kind, rule_expressible, frequency, batch_size, exception_rate, current_actor, minutes_per_item, failure_impact, existing_feature, human_signoff
case.yaml:7:45: enum steps[1].inferred[4]: 선택지 밖의 값 'name'. 가능한 값: input_kind, rule_expressible, frequency, batch_size, exception_rate, current_actor, minutes_per_item, failure_impact, existing_feature, human_signoff
case.yaml:10:27: duplicate steps[2].inferred[2]: 추정 답 키가 중복된다 (처음: steps[2].inferred[1])
case.yaml:13:16: required steps[3].answers.batch_size: inferred에 적은 답은 answers에 값이 있어야 한다
case.yaml:13:28: required steps[3].answers.minutes_per_item: inferred에 적은 답은 answers에 값이 있어야 한다
case.yaml:13:46: required steps[3].inferred[3]: 값이 비어 있다
case.yaml:16:15: type steps[4].inferred: 목록이어야 한다
```

- 1단계 `frequency`는 맞는 원소라 오류가 없다. 4단계는 목록이 아니라 원소를 보지 않는다.
- 정상 쪽(`IsInferred` 값, 추정 표시 문장)은 classification.md 6.5 표본이 본다.

## 7. 테스트로 옮기는 방법 (test-engineer 참고)

### 7.1 위치와 샘플

- 폴더 `tests/Scorecard.Tests/CaseSchema/`, 네임스페이스 `Scorecard.Tests.CaseSchema`. Trait `Spec` = `case-schema`.
- 카드의 합성 샘플 3종: `tests/Scorecard.Tests/TestData/case-schema/<이름>/case.yaml`. 정상(6.1), 필드 누락(6.3), 형식 오류(6.4). 스파이크 `samples/`에서 옮겨도 된다. 나머지 경우는 테스트 안 문자열로 충분하다.
- 샘플은 UTF-8(BOM 없음), LF. T-002 테스트 `Sample_text_files_have_no_carriage_return`이 `TestData` 아래 텍스트 파일의 `\r`을 막는다 [읽음]. 그래서 BOM·CRLF·CR·UTF-16·CP949·잘못된 바이트 변형은 커밋하지 않고 테스트 코드에서 정상 샘플 바이트로 만든다(스파이크 `Probes.Encodings`와 같은 방법).
- 기존 `TestData/.gitattributes`(`* -text`)가 새 샘플에도 적용된다 [읽음].

### 7.2 파서 테스트 (제안 목록)

| 테스트 | 확인 |
| --- | --- |
| 정상 전체 | 오류 0건. 6.1 표의 값과 `IsDefault` |
| 정상 최소 | 6.2 |
| 기본값 표 | 이 문서에서 머리 행이 `\| 키 \| 질문 \| 형식 \| 선택지·범위 \| 기본값 \|`인 표(4.4)를 읽는다. 10행(T-008 전 9행)의 다섯째 칸 기본값과, `answers` 없는 단계의 값이 같고 `IsDefault`가 모두 true |
| `inferred` 오류(T-008) | 6.7 표본 = 블록, 오류 줄 = 블록. 4.13 글머리마다 테스트 안 문자열 하나 이상(목록 아님, null·매핑 원소, 키 아님, 중복, 값 없음, `answers` 없음, 빈 목록·null은 오류 없음) |
| `IsInferred`(T-008) | 목록의 답만 true, 값·`IsDefault`는 그대로. `inferred` 없는 파일은 모두 false. 쓴 순서와 상관없다 |
| `human_signoff`(T-008) | 4.4 표 행이 다른 열거 행과 같은 테스트(기본값, 선택지, `enum`)를 그대로 탄다(표에서 읽으므로 새 코드 없이 늘어난다) |
| 필드 누락 | 6.3의 4줄과 `ToString()` 목록이 같다 |
| 형식 오류 | 6.4의 18줄과 같다 |
| 인코딩 변형 | 4.8 표 각 행. BOM·CRLF·CR은 LF와 모델이 같다 |
| YAML 기능 | 4.7 표 각 행(6.5) |
| 값 형식 | 4.6 표의 받는 값·받지 않는 값 |
| null 형식 | `~`, `null`, `NULL`, 빈 값 → 기본값·true. `""`, `"null"` → `enum` |
| 문화권 | de-DE로 바꿔도 결과가 같다(`try/finally`로 되돌림) |
| 파일 없음 | `case.yaml: file: 파일이 없다`, 절대 경로 없음 |
| 깊이 | 깊이 10만 입력이 `depth` 1건 |

### 7.3 가드 테스트 변경 (tests/Guards)

- G1: `AllowedThirdPartyAssemblies`에 `"YamlDotNet"`. 승인 질문 3이 "예"면 Y1 규칙, 카나리아(Serialization·RepresentationModel 타입 참조, 호출 없음), 자기 검사 Theory 행을 더한다.
- G2·G3: `GuardTargets`(4.11)와 Theory 두 개(`Assert.Empty`). 스파이크 `spikes/T-003/tests-variant/CoreGuardTargets.cs`가 모양이다.
- 샘플: `TestData/guard/case-valid/case.yaml`(6.1과 같음), `TestData/guard/case-invalid/case.yaml`:

```yaml
# 합성 오류 예시. 실제 업무·값 없음. 필드 누락·형식 오류·알 수 없는 필드를 섞었다.
task: 오류 합성 예시
steps:
  - answers: {frequency: Weekly, batch_size: 2.5}
  - name: 둘째 단계
    answers:
      exception_rate: 1,5
      frequncy: weekly
thresholds:
  time_must_drop: yes
```

  이 샘플의 오류 7건 [S3 guard-case-invalid]:

```
case.yaml:2:1: required name: 필수 필드가 없다
case.yaml:4:5: required steps[1].name: 필수 필드가 없다
case.yaml:4:26: enum steps[1].answers.frequency: 선택지 밖의 값 'Weekly'. 가능한 값: daily, weekly, monthly, rare
case.yaml:4:46: format steps[1].answers.batch_size: 정수가 아니다: 2.5
case.yaml:7:23: format steps[2].answers.exception_rate: 숫자가 아니다: 1,5
case.yaml:8:7: unknown-field steps[2].answers.frequncy: 알 수 없는 필드다
case.yaml:10:19: format thresholds.time_must_drop: true 또는 false로 쓴다: yes
```

  첫 줄이 주석이라 루트 매핑은 2줄에서 시작한다. 그래서 `name` 누락 위치가 2:1이다.
- 빈 출력 규칙: `GuardChecks.Determinism`에 4.11 규칙, `FakeTargets`에 `no-output`·`empty-file`, 테스트 1개. 스파이크 `spikes/T-003/patches/empty-output.patch`가 모양이다(23줄 추가) [S5].

### 7.4 실패 테스트 먼저

- test-engineer 단계에서는 Core에 `Scorecard.Core.Cases`가 없다. 새 테스트는 컴파일되지 않는다. 이 상태를 "실패하는 테스트"로 보고, 같은 브랜치에서 core-dev가 구현한다. → 승인 질문 14.
- 빈 출력 규칙 패치는 `tests/Guards`만 고치고 Core 타입을 쓰지 않는다 [S5 패치]. N6는 dll의 AssemblyRef 중 목록에 없는 것만 잡으므로, 목록에 이름만 먼저 있어도 G1은 그대로 통과한다 [읽음 MetadataScanner.cs].

## 8. 미확인 목록

1. GitHub ubuntu·windows 러너에서 파서·G1·G2·G3 테스트가 녹색인지. Windows에서 열 번호·줄 번호가 같은지.
2. B(정적 생성기)의 오류 모양. 빌드와 G1만 봤다.
3. C가 YamlDotNet 안의 N5·D1·R1 코드에 실제로 닿는지(호출 도달성). 바이트 검색으로 부르는 네임스페이스만 봤다.
4. publish 단일 exe(`win-x64 --self-contained -p:PublishSingleFile=true`)에서 YamlDotNet 동작. 트리밍은 쓰지 않는다(지시문 4절 명령에 없음 [읽음]).
5. CR만 쓰는 파일의 `encoding` 줄 번호.
6. YamlDotNet의 `syntax` 메시지가 다른 버전에서 같은지. 버전을 고정하므로 같은 버전 안에서는 같다 [S4].
7. 탭 들여쓰기 오류 위치가 버전마다 같은지.
8. 매우 긴 줄, 매우 많은 오류(수천 건)의 실행 시간.
9. 다른 컴퓨터(특히 Windows)에서 [T] 시간. 시간은 참고값이고 테스트로 두지 않는다.

## 9. 스파이크 단계 목록

`spikes/T-003/run-spike.sh <작업폴더> <로그폴더>`. 커밋된 HEAD를 작업 폴더에 clone해서 변형을 넣는다. 저장소의 `src/`, `tests/`는 건드리지 않는다. 스크립트를 여러 번 돌리며 실험을 더했다. 같은 실험의 결과 해시(`SHA`, `sha=`)는 매번 같았다. `summary.txt`는 마지막 실행이다.

| 단계 | 한 일 |
| --- | --- |
| S0 | 스파이크 도구 빌드(`-warnaserror`, 산출물은 작업 폴더) |
| S1 | YamlDotNet 18.1.0 nuspec, `YamlDotNet.dll`의 참조·G1 항목, 항목을 부르는 네임스페이스(IL 단순 검색) |
| S2 | clone 4개(기준선, A, B, C). Core에 패키지·코드 → Release `-warnaserror` → Core 메타데이터·참조 → G1 테스트 → 허용 목록 → G1 테스트. C는 Cli 검사, 전체 테스트, `dotnet format` |
| S3 | `samples/` 13종과 guard 샘플 2종 파싱 |
| S4 | 결정론(프로세스 두 번, de-DE, 다른 경로), 메모리 실험 `PROBE`, `Parser` 시간 `TIMING` |
| S5 | C clone에 G2·G3 어댑터·샘플·빈 출력 패치 → G2·G3, 전체 테스트 |
| S6 | 어댑터가 파일을 안 쓰게 바꿈 → G2 |
| S7 | 파서가 오류 줄에 절대 경로를 쓰게 바꿈 → G2 |
| S8 | 테스트 프로젝트에서 YamlDotNet Serialization·RepresentationModel 타입 참조(Y1 카나리아 모양) |

스크립트를 만들기 전에 저장소 밖 임시 프로젝트에서 API 모양을 먼저 훑었다(`Mark.Line`이 `long`인 것 등). 이 문서의 사실은 모두 스크립트로 다시 확인한 것이다.

---

## 사람 승인 필요

질문마다 prototyper의 제안을 적었다. "제안대로"로 답할 수 있다.

1. **파서 방식.** G1은 A·B·C를 가르지 못한다(셋 다 N6 1건). C만 필수 필드 위치·모든 오류 모음·엄격한 형식을 낸다(4.1).
   - 제안: C. YamlDotNet `Parser` 이벤트로 트리를 만들고 스키마 검사는 Core 코드가 한다. 정적 생성기는 쓰지 않는다.
2. **YamlDotNet 18.1.0과 N6 허용 목록.** MIT, 의존 패키지 0개. YamlDotNet.dll 안에는 N5 3건·D1 1건·R1 1건이 있으나 G1 범위 밖이다(4.2).
   - 제안: Core에 YamlDotNet 18.1.0을 정확히 고정해 넣는다. test-engineer가 `AllowedThirdPartyAssemblies`에 `"YamlDotNet"`을 더하고, PR 본문에 패키지·버전·라이선스·내부 G1 항목을 적는다.
3. **G1 새 규칙 Y1.** 허용 목록만으로는 Core가 나중에 YamlDotNet의 리플렉션 역직렬화를 써도 G1이 모른다.
   - 제안: 넣는다. Core만. "YamlDotNet 타입은 `YamlDotNet.Core`(하위 포함)만 참조한다"(4.2 표). 카나리아와 자기 검사 행을 더한다. T-003 안에서 test-engineer가 한다.
4. **최상위 필드.** 필수는 `name`, `task`, `steps`(1개 이상), `steps[].name`. 나머지는 선택(4.3).
   - 제안: 4.3 표 그대로. `name`은 소문자·숫자·`-`, 64자 이하. `ai_usage`는 없으면 미입력(null)이고 기본값을 두지 않는다. 폴더 이름과 같은지는 보지 않는다.
5. **기본값을 누가 넣는가.** 카드 수용 기준 1은 이 스펙에 기본값을 요구하고, T-004 카드도 "질문 9개·기본값"을 다룬다.
   - 제안: 파서가 3.2 기본값을 넣고 `IsDefault`로 표시한다. 키 없음과 null은 같은 뜻(모름 → 기본값)이다. 기본값 표는 이 문서 4.4에만 두고, T-004의 `classification.md`는 이 표를 가리킨다. 테스트가 4.4 표를 읽어 파서와 대조한다(지시문 13절 "숫자를 코드에 박지 않는다"를 테스트로 묶음).
6. **값의 엄격도.** 알 수 없는 키는 오류, 열거는 대소문자 구분, 숫자·불리언은 따옴표 없이, 불리언은 `true`·`false`만, 정수에 선행 0·부호 `+`·지수 없음(4.6). 3.2에 없는 범위: `batch_size` 1 이상, `exception_rate` 0~100 소수 허용, `minutes_per_item` 0 이상.
   - 제안: 4.6 그대로. `interview`가 `case.yaml`을 쓰므로 사람이 손으로 고칠 때만 걸린다. 오류 메시지가 고칠 방법을 알려 준다.
7. **YAML 기능 제한.** 문서 1개, 앵커·별칭·태그 금지(병합 키는 알 수 없는 필드), 중복 키 오류, 깊이 16 초과는 즉시 멈춤(4.7).
   - 제안: 4.7 그대로. 파일 크기 상한은 두지 않는다(깊이 제한으로 느린 경우를 막았다).
8. **인코딩.** UTF-8만 받는다. BOM 하나는 뗀다. UTF-16 BOM과 잘못된 UTF-8(예: CP949)은 `encoding` 오류. CRLF·CR은 LF와 같은 결과(4.8).
   - 제안: 4.8 그대로. CP949 자동 변환은 하지 않는다(문자 집합 추측은 결정론과 원칙 6에 어긋난다).
9. **오류 형식.** `case.yaml:줄:열: 코드 경로: 메시지`. 목록 번호는 1부터. 모든 오류를 모으고(문법·인코딩·빈 파일·깊이·파일은 한 건), (줄, 열) 순서. 오류가 하나라도 있으면 모델 없음. 문법 오류는 YamlDotNet 영어 메시지를 붙인다(4.9).
   - 제안: 4.9 그대로. 코드 17개와 메시지 문구를 테스트가 문자열로 비교한다. 문구를 바꾸면 스펙과 테스트를 같이 고친다.
10. **inputs·compare.** `inputs`가 있으면 세 키 필수, 경로는 케이스 폴더 기준 상대 경로(`/`, `..`·`:`·`\` 없음). `compare`가 있으면 `inputs` 필수. `tolerance`와 `tolerance_pct`는 둘 중 하나만, 둘 다 없으면 허용(뜻은 T-006)(4.5).
    - 제안: 4.5 그대로.
11. **thresholds 기본값.** 12.1 예시(95)와 공개 예제 `table-compare`(90%에서 "조건부")를 기본값 하나로 맞출 수 없다(4.5).
    - 제안: T-003은 읽기만 하고 없으면 null(미입력). 기본값과 "기본값 사용, 확인 필요" 표시는 판정 카드에서 `metrics.md`에 정한다.
12. **G2 빈 출력 규칙(T-002 reviewer 지적 7).**
    - 제안: G2에만 넣는다. "R1 출력 폴더에 1바이트 이상 파일이 없으면 `G2 R1 기준: 출력 파일 없음`". 가짜 대상 `no-output`·`empty-file`과 테스트 1개를 더한다. G3에는 넣지 않는다. T-003 안에서 test-engineer가 `tests/Guards`를 고친다(검사를 더하는 변경, 약화 아님).
13. **G2·G3 등록 모양.** 테스트 쪽 어댑터가 `parse-result.json` 하나를 쓴다. 대상은 정상·오류 표본 둘(4.11). 오류 표본이 있어야 오류 문자열의 절대 경로를 잡는다 [S7].
    - 제안: 4.11 그대로. 샘플은 `TestData/guard/case-valid/`, `case-invalid/`. `guard/basic/`은 그대로 둔다.
14. **Core 공개 API와 진행 순서.** 4.12의 이름·모양(열거는 문자열, 숫자는 `decimal`, 오류가 있으면 모델 없음). test-engineer 단계에서는 이 API가 없어 새 테스트가 컴파일되지 않는다(7.4).
    - 제안: 4.12 그대로. 컴파일 실패를 "실패하는 테스트"로 보고 같은 브랜치에서 core-dev가 구현한다. PR 본문에 적는다.

## 사람 승인 결과 (2026-11-06, juhan: "제안대로")

질문 1~14 모두 위에 적은 제안대로 승인됐다. 요약:

1. 파서는 방식 C(YamlDotNet `Parser` 이벤트 + Core 스키마 검사). 정적 생성기 없음.
2. YamlDotNet 18.1.0을 정확히 고정. N6 허용 목록에 `YamlDotNet`. PR 본문에 패키지·버전·라이선스·내부 G1 항목.
3. G1 새 규칙 Y1(Core만): YamlDotNet 타입은 `YamlDotNet.Core`(하위 포함)만 참조. 카나리아·자기 검사 포함.
4. 최상위 필드는 4.3 표 그대로.
5. 파서가 3.2 기본값을 넣고 `IsDefault`로 표시. 기본값 표는 4.4에만. 테스트가 4.4 표를 읽어 대조.
6. 값의 엄격도는 4.6 그대로.
7. YAML 기능 제한은 4.7 그대로. 파일 크기 상한 없음.
8. 인코딩은 4.8 그대로. CP949 자동 변환 없음.
9. 오류 형식은 4.9 그대로. 코드·문구를 테스트가 문자열로 비교.
10. inputs·compare는 4.5 그대로.
11. thresholds는 읽기만, 없으면 null. 기본값은 판정 카드에서.
12. G2 빈 출력 규칙을 넣는다(G2만). test-engineer가 `tests/Guards`에 더한다.
13. G2·G3 등록은 4.11 그대로. 샘플 `TestData/guard/case-valid/`, `case-invalid/`.
14. Core 공개 API는 4.12 그대로. test-engineer 단계의 컴파일 실패를 "실패하는 테스트"로 보고, 같은 브랜치에서 core-dev가 구현한다.

## T-008 개정 (승인됨, 2026-11-07)

- 작업 카드 `docs/tasks/T-008-human-signoff.md`. 결정 출발점: ADR 0003 "결정"·"규칙", ADR 0004 결정 2·질문 16 해석 A [읽음].
- 확인 대상 커밋 `32f7692`. 스파이크 `spikes/T-008/`(머지 대상 아님). 결과와 질문은 `docs/spec/human-signoff.md`.
- 바꾼 곳: 4.4(제목, 단계 항목 표 `inferred` 행, `answers` 표 끝 `human_signoff` 행, 글머리 둘), 4.9(`enum`·`required`·`duplicate` 행의 T-008 경우. 코드 수 17개는 그대로), 4.12(`StepAnswers` 10번째 답, `Answer<T>.IsInferred`), 새 4.13, 6.1 표 1행·글머리, 6.2 글, 새 6.7, 7.2 표 3행.
- 바꾸지 않은 곳: 6.1 블록(= `valid-full` = `guard/case-valid`), 오류 코드 표의 코드 목록, 4.3 최상위 필드.
- 위 승인 결과 5("기본값 표는 4.4에만")는 그대로다. `human_signoff` 행도 4.4 표에만 있다.
