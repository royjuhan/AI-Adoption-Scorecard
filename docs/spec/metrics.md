# metrics (T-006 스펙: 대조와 지표)

- 상태: 초안. 사람 승인 전. 대조와 지표만 정한다. 판정(통과·조건부·보류·미검증)은 판정 카드에서 정한다(11절).
- 작업 카드: `docs/tasks/T-006-compare-engine.md`
- 작성: prototyper, 2026-11-07
- 확인 대상 커밋: `e6ff614` (`src/`·`tests/`·`examples/`는 `7735f8a`와 같다)
- 확인 환경: Ubuntu 24.04 x64, .NET SDK 10.0.114, YamlDotNet 18.1.0, xUnit 2.9.3. 독립 구현 대조에만 Python 3.13.
- 스파이크: `spikes/T-006/` (머지 대상 아님. 머지 전에 빠진다). 실행은 `spikes/T-006/run-spike.sh <작업폴더> <로그폴더>`. 요약 원문은 `spikes/T-006/summary.txt`. 이 문서의 JSON 블록, SHA-256, 오류 줄은 스파이크 후보(`spikes/T-006/core/Comparison/`)가 실제로 낸 것이다.

## 0. 표기

- `[읽음]`: 커밋된 파일을 읽어서 확인한 사실.
- `[S0]`~`[S8]`: 스파이크 단계(10절). `[P:무리 이름]`: S4 `PROBE` 줄. `[TIMING]`: S4 `TIMING` 줄.
- **미확인**: 실험하지 않았거나 이 환경에서 확인할 수 없는 것.
- 기준 자료 = `reference/`(사람이 만든 결과), 결과 자료 = `candidate/`(AI 적용 후 결과). 기준 행 = 기준 자료의 데이터 행(머리 행 제외).
- 항목·일치·값 불일치·누락·추가·허용오차 안 차이는 4.4의 뜻이다. T-005 4.7(P2)에서 왔다.
- `CaseDefinition`·`CaseInputs`·`CompareField`·`CaseError`는 `docs/spec/case-schema.md`(T-003)의 이름이다. G1·G2·G3, D1, R1~R4는 `docs/spec/guard-tests.md`의 이름이다.
- 예제 이름은 `table-compare`, `form-fill`, `mixed-exceptions`(지시문 12절).

## 1. 범위

카드 수용 기준 1~4와 카드 메모를 다룬다. 수용 기준 5(CI 녹색)는 PR에서 확인한다.

| 카드 수용 기준·메모 | 이 문서 | 승인 질문 |
| --- | --- | --- |
| 1 예제 3종 출력 = `expected/metrics.json`. 90.0%·3.3%, 100%·0%, 미검증 | 3.2, 4.4, 4.8, 7절 | 7, 12, 13, 17 |
| 2 키 중복, 빈 키, 형식 오류(열 없음, 숫자 아님, 인코딩·줄바꿈 변형) 테스트 | 4.2, 4.3, 4.7 | 4, 5, 6, 9, 14 |
| 3 허용오차 경계, 텍스트 정규화 단위 테스트 | 4.5, 4.6 | 8, 10, 11 |
| 4 G2·G3에 대조 대상 등록 | 4.10 | 18 |
| 메모: P2를 출발점으로 확정. 추가 행을 어떻게 셀지 | 4.4 | 7 |
| 메모: `compare`에 없는 열(`signal_type`), 허용 오차가 없는 필드(`item`) | 4.4, 4.6 | 10, 16 |
| 메모: `metrics.json`에 무엇을 넣을지 | 3.2 | 15, 16 |
| 메모: decimal, InvariantCulture, 퍼센트 반올림 | 4.5, 4.8 | 8, 12 |
| 리드 지시: 파일을 Core가 읽을지, 부르는 쪽이 바이트를 넘길지. glob, 여러 파일, CSV | 2절, 4.1, 4.2 | 1, 2, 3, 4 |
| 리드 지시: 오류 모델과 메시지 | 4.7 | 14 |
| 리드 지시: G1 충돌, 결정론, Core API | 4.10, 4.11 | 1, 19 |
| 메모: T-004·T-005 예제 파일은 바꾸지 않는다 | 7.1 | 17 |

범위 밖:

- 판정: 통과·조건부·보류·미검증 판정, `thresholds` 사용과 기본값, "특정 유형에만 불일치가 몰림". 판정 카드에서 정한다(11절).
- 시간 지표(건당 처리 시간, 검토 시간, 재작업률, 완료당 비용). `scorecard measure`·판정 카드.
- `scorecard compare` 명령, 출력 폴더 위치, 화면 문구. M2(cli-dev).
- 입력 SHA-256 기록. 지시문 2절 2는 "평가서에 기록"이다 [읽음]. classification.md와 같이 평가서 카드로 둔다. metrics.json에는 넣지 않는다. Core API가 읽은 바이트를 돌려주므로 평가서가 같은 바이트로 해시를 낼 수 있다(4.11).
- 자료 투입 단계의 "읽기 전용 복사"(지시문 3.1 4단계). M2.
- CSV가 아닌 형식(Excel 파일 등). 이 카드는 CSV만 읽는다.

## 2. 입력

- `CaseDefinition` 하나(`CaseParser` 결과). 쓰는 것은 `Name`, `Inputs`(`Reference`, `Candidate`, `Key`), `Compare`(`Field`, `Tolerance`, `TolerancePct`)다. `Thresholds`·`Steps`는 읽지 않는다.
- `inputs`가 없거나(null) `compare`가 비면 파일을 읽지 않고 "미검증"이다(4.4). 케이스 폴더가 없어도 오류가 아니다 [P:not-verified no-inputs directory not read].
- 파일은 두 길로 받는다(제안 C).

| 안 | 누가 파일을 읽나 | 좋은 점 | 나쁜 점 |
| --- | --- | --- | --- |
| A | Core(`CompareDirectory`)만 | Cli가 짧다. glob 규칙이 Core 한 곳에 있다 | 테스트가 늘 파일을 만들어야 한다 |
| B | 부르는 쪽(Cli)이 바이트를 넘긴다 | Core가 순수 함수다 | glob 규칙이 Cli에 간다. Core 테스트·G2·G3가 파일 찾기를 보지 않는다 |
| C (제안) | 둘 다. `CompareDirectory` = `CompareInputs.Read`(파일 찾기·읽기) + `Compare`(순수 함수, 바이트) | glob은 Core. 테스트는 메모리 바이트로 `Compare`를 부른다. 평가서가 같은 바이트로 해시를 낸다 | 공개 API가 하나 더 있다 |

- Core가 파일을 읽는 것은 G1과 충돌하지 않는다. `CaseParser.ParseDirectory`가 이미 `File.ReadAllBytes`·`Directory.EnumerateFileSystemEntries`를 쓴다 [읽음]. D1 목록에 파일 읽기 멤버는 없다 [읽음 guard-tests.md D1 표]. 후보를 넣고 G1이 통과했다 [S2].
- 읽기만 한다. 쓰는 API(`File.Write*`, `Create`, `Delete`, `Move`)를 쓰지 않는다. G3 통과 [S5b]. 같은 바이트 배열을 넘기면 대조 뒤에도 바이트가 같다 [P:immutable].
- 파일 경로는 케이스 폴더 기준 상대 경로(`/` 구분)로만 다룬다(`SourceFile.Path`). 절대 경로를 결과·오류에 넣지 않는다. 예외 메시지에 절대 경로가 있어 예외 타입 이름만 쓴다(4.7).
- `caseDirectory`가 상대 경로면 프로세스 현재 폴더 기준이다. Core는 현재 폴더를 읽지 않는다(D1 `Environment::get_CurrentDirectory`). 부르는 쪽이 전체 경로를 넘기길 권한다.

## 3. 출력

### 3.1 메모리 결과

`CompareResult(Metrics, Errors)` 하나. 오류가 하나라도 있으면 `Metrics`는 null이다(`CaseParseResult`와 같은 모양). 오류가 있으면 지표를 내지 않는다(4.7).

- `CompareMetrics`: 3.2의 내용과 같다. 다른 점은 `Items`에 모든 항목(차이 없는 일치 포함)이 있다는 것과, 항목의 `CsvRow`가 그 행의 머리 행·칸 전부(정규화 뒤, `compare`에 없는 열 포함)를 가진다는 것이다. 판정 카드가 `signal_type` 같은 열을 쓸 수 있다(승인 질문 16).
- 항목 순서: 기준 항목(기준 파일 경로 서수 → 줄 순서), 그다음 추가 항목(결과 파일 경로 서수 → 줄 순서) [P:key multi-file-order].

### 3.2 metrics.json

Core가 바이트를 만든다(`MetricsJson.Render`). 모양:

```json
{
  "case": "<케이스 이름>",
  "compare": {
    "status": "measured | not_verified",
    "reason": "no_inputs | no_compare_fields | null",
    "key": "<inputs.key> | null",
    "reference_files": ["<케이스 폴더 기준 상대 경로>"],
    "candidate_files": ["<…>"],
    "counts": {"reference": 0, "candidate": 0, "match": 0, "mismatch": 0, "missing": 0, "extra": 0, "within": 0},
    "accuracy_pct": 0.0,
    "missing_rate_pct": 0.0,
    "fields": [
      {"field": "<compare.field>", "kind": "number | text", "tolerance": 0, "tolerance_pct": null,
       "compared": 0, "outside": 0, "within": 0, "not_number": 0}
    ],
    "items": [
      {"key": "<키>", "status": "match | mismatch | missing | extra",
       "reference": {"file": "<경로>", "line": 0}, "candidate": null,
       "differences": [{"field": "<필드>", "result": "within | outside | not_number", "reference": "<칸>", "candidate": "<칸>"}]}
    ]
  }
}
```

- 위 블록은 모양 설명이다. 실제 바이트는 7절 블록처럼 배열 원소도 한 줄에 하나다.
- 지표는 `compare` 아래에 둔다. 판정 카드가 시간 지표·판정을 같은 파일의 다른 속성으로 더할 수 있게 하려는 것이다(11절).

속성 표. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 순서는 파일 안 순서(table-compare에서 처음 나오는 순서)다. `[]`는 배열 원소다.

| 속성 | 형식 | 뜻 |
| --- | --- | --- |
| `case` | 문자열 | `case.yaml`의 `name` |
| `compare` | 객체 | 대조 결과 |
| `compare.status` | 문자열 | `measured`(대조함) 또는 `not_verified`(미검증, 4.4) |
| `compare.reason` | 문자열 또는 null | 미검증 이유 `no_inputs`, `no_compare_fields`. `measured`면 null |
| `compare.key` | 문자열 또는 null | `inputs.key`. 미검증이면 null |
| `compare.reference_files` | 문자열 배열 | 읽은 기준 파일. 경로 서수 순. 미검증이면 `[]` |
| `compare.candidate_files` | 문자열 배열 | 읽은 결과 파일. 같음 |
| `compare.counts` | 객체 또는 null | 항목 셈(4.4). 미검증이면 null |
| `compare.counts.reference` | 정수 | 기준 행 수 = 일치 + 값 불일치 + 누락 |
| `compare.counts.candidate` | 정수 | 결과 행 수 = 일치 + 값 불일치 + 추가 |
| `compare.counts.match` | 정수 | 일치 항목 수 |
| `compare.counts.mismatch` | 정수 | 값 불일치 항목 수 |
| `compare.counts.missing` | 정수 | 누락 항목 수 |
| `compare.counts.extra` | 정수 | 추가 행 수 |
| `compare.counts.within` | 정수 | 일치 항목 중 허용오차 안 차이가 있는 항목 수 |
| `compare.accuracy_pct` | 숫자 또는 null | 정확도(%). 소수 첫째 자리(4.8). 미검증이면 null |
| `compare.missing_rate_pct` | 숫자 또는 null | 누락률(%). 같음 |
| `compare.fields` | 객체 배열 | `compare` 필드별 셈. `compare` 순서. 미검증이면 `[]` |
| `compare.fields[]` | 객체 | 필드 하나 |
| `compare.fields[].field` | 문자열 | 필드 이름 |
| `compare.fields[].kind` | 문자열 | `number`(허용 오차가 있음) 또는 `text`(없음) |
| `compare.fields[].tolerance` | 숫자 또는 null | `case.yaml` 값 그대로(자릿수 유지) |
| `compare.fields[].tolerance_pct` | 숫자 또는 null | 같음 |
| `compare.fields[].compared` | 정수 | 양쪽에 있는 항목 수(일치 + 값 불일치) |
| `compare.fields[].outside` | 정수 | 허용 오차 밖 칸 수 |
| `compare.fields[].within` | 정수 | 허용 오차 안 차이 칸 수 |
| `compare.fields[].not_number` | 정수 | 결과 칸이 숫자가 아닌 칸 수 |
| `compare.items` | 객체 배열 | 차이가 있는 항목만(아래). 3.1 순서 |
| `compare.items[]` | 객체 | 항목 하나 |
| `compare.items[].key` | 문자열 | 키(정규화 뒤) |
| `compare.items[].status` | 문자열 | `match`, `mismatch`, `missing`, `extra` |
| `compare.items[].reference` | 객체 또는 null | 기준 행 위치. 추가 항목이면 null |
| `compare.items[].reference.file` | 문자열 | 기준 파일 경로 |
| `compare.items[].reference.line` | 정수 | 행이 시작한 줄(1부터, 4.2) |
| `compare.items[].candidate` | 객체 또는 null | 결과 행 위치. 누락 항목이면 null |
| `compare.items[].candidate.file` | 문자열 | 결과 파일 경로 |
| `compare.items[].candidate.line` | 정수 | 같음 |
| `compare.items[].differences` | 객체 배열 | 차이가 있는 `compare` 필드. `compare` 순서. 누락·추가는 `[]` |
| `compare.items[].differences[]` | 객체 | 칸 차이 하나 |
| `compare.items[].differences[].field` | 문자열 | 필드 이름 |
| `compare.items[].differences[].result` | 문자열 | `within`, `outside`, `not_number`(4.5) |
| `compare.items[].differences[].reference` | 문자열 | 기준 칸(정규화 뒤 텍스트) |
| `compare.items[].differences[].candidate` | 문자열 | 결과 칸(같음) |

- `items`에 쓰는 항목: 값 불일치, 누락, 추가, 그리고 허용오차 안 차이가 있는 일치. 차이 없는 일치는 쓰지 않는다. 그래서 같은 자료면 `items`가 `[]`다 [S3 form-fill].
- 칸 값은 문자열로 쓴다. 숫자 칸도 정규화한 텍스트 그대로다(`1e3`은 `1e3`). 숫자로 바꿔 쓰지 않는다.
- 쓰지 않는 것: 절대 경로, 입력 해시, 정규화 전 원문, `compare`에 없는 열(`signal_type`), 차이 없는 일치 항목, 오류. → 승인 질문 15, 16.
- 오류가 있으면 metrics.json이 없다(3.1). 오류를 어디에 어떻게 보일지는 Cli(M2)가 정한다.
- 미검증 모양(mixed-exceptions): `status`·`reason`만 값이 있고 `key`·`counts`·퍼센트는 null, 배열은 `[]`다(7.4) [P:json empty-arrays].

### 3.3 바이트 규칙

`docs/spec/classification.md` 3.3과 같다. `Utf8JsonWriter`, 들여쓰기 2칸, 줄바꿈 `\n`(CR 0개), 끝에 `\n` 하나, UTF-8(BOM 없음), `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` [S3 json-check].

- 숫자는 `decimal` 그대로 쓴다(`WriteNumber(decimal)`). 자릿수가 남는다: `90.0`, `1.0`, `1.50`, `0` [P:json decimal]. 퍼센트는 늘 소수 한 자리다(4.8).
- 이스케이프는 classification.md 3.3 표와 같다. 키에 큰따옴표, 역슬래시, 탭, U+2028, U+1F600, `<>&'+`, 백틱을 넣으면 차례로 `\"`, `\\`, `\t`, `\u2028`, `\uD83D\uDE00`, 그대로, 그대로가 된다 [P:json escape key].
- 빈 배열은 `[]`다(한 줄) [P:json empty-arrays].
- 같은 입력이면 같은 바이트다. 확인: 다른 프로세스, 다른 경로 사본, `LANG=de_DE.UTF-8` 프로세스 [S3 det-cmp]. 문화권 7종(Invariant, de-DE, tr-TR, ar-SA, ko-KR, fr-CH, th-TH)에서 SHA-256이 하나 [P:culture]. Python 독립 구현(`spikes/T-006/xcheck/compare.py`, 이 문서 3·4절만 보고 짰다)이 예제 3종에서 같은 바이트를 냈다 [S3 det-cmp python].
- 기준·결과 CSV의 줄바꿈(CRLF, CR), BOM, 따옴표, 마지막 줄바꿈 없음, 끝 빈 줄, 칸 앞뒤 공백 변형 9종을 기준·결과 양쪽 또는 결과만 바꿔도 같은 바이트다 [P:eol].
- `CompareSources`의 파일 순서와 상관없다 [P:order reference files given in reverse]. 결과 행 순서를 바꾸면 셈은 같고, 줄 번호가 바뀌어 바이트는 다르다 [P:order table-compare candidate rows sorted].

## 4. 규칙

### 4.1 파일 찾기 (`inputs` 패턴)

`case.yaml`은 패턴 글자를 그대로 저장한다. `/`로 시작, `\`·`:`·`..` 조각은 이미 `format` 오류다 [읽음 case-schema 4.5]. 이 카드는 나머지를 정한다.

- 패턴은 `/`로 나눈 조각이다. 폴더 조각은 글자 그대로다. 마지막 조각(파일 이름)에만 `*`(0자 이상)·`?`(UTF-16 한 단위)를 쓴다.
- 받지 않는 것: 빈 조각(`a//b`), `.` 조각, 폴더 조각의 와일드카드, `**`, `[` `]` `{` `}`. → `pattern` 오류(4.7).
- 이름 비교는 서수다(대소문자 구분). OS 검색 패턴(`Directory.EnumerateFiles(dir, "*.csv")`)을 쓰지 않는다. 폴더의 이름 목록을 받아 직접 맞춘다. Windows 검색 패턴은 대소문자를 무시하고 8.3 이름도 맞출 수 있어 OS마다 결과가 달라질 수 있기 때문이다(Windows 동작은 **미확인**).
- 폴더 조각도 이름 목록에서 서수로 찾는다. 폴더 이름이 `Candidate`이고 패턴이 `candidate/*.csv`면 대소문자를 무시하는 파일 시스템에서도 `폴더가 없다: candidate`다 [P:glob read candidate/*.csv (폴더 이름 Candidate)].
- 하위 폴더는 보지 않는다 [P:glob read reference/sub/*.csv]. 이름이 맞아도 폴더는 파일이 아니다.
- `.`으로 시작하는 파일, `~$`로 시작하는 파일(Excel 잠금 파일 이름)도 맞으면 읽는다 [P:glob match]. 이름으로 거르지 않는다. 잠금 파일이 실제로 어떻게 읽히는지는 **미확인**.
- 찾은 파일은 이름 서수 순이다.
- 맞는 파일이 없으면 `file` 오류 `일치하는 파일이 없다`. 폴더가 없으면 `폴더가 없다: <폴더>`.

패턴 모양. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다.

| 패턴 | 받나 |
| --- | --- |
| `reference/*.csv` | 예 |
| `reference/logs.csv` | 예 |
| `*.csv` | 예 |
| `reference/a/b/*.csv` | 예 |
| `ref*/a.csv` | 아니오 |
| `reference/**/*.csv` | 아니오 |
| `reference/**.csv` | 아니오 |
| `reference/[ab].csv` | 아니오 |
| `reference/{a,b}.csv` | 아니오 |
| `reference//a.csv` | 아니오 |
| `./reference/a.csv` | 아니오 |
| `reference/./a.csv` | 아니오 |

이름 맞추기. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다.

| 패턴 | 파일 이름 | 맞나 |
| --- | --- | --- |
| `*.csv` | `logs.csv` | 예 |
| `*.csv` | `LOGS.CSV` | 아니오 |
| `*.csv` | `logs.csvx` | 아니오 |
| `*.csv` | `.csv` | 예 |
| `*.csv` | `logs.csv.bak` | 아니오 |
| `*.csv` | `~$logs.csv` | 예 |
| `*.csv` | `.hidden.csv` | 예 |
| `log?.csv` | `log1.csv` | 예 |
| `log?.csv` | `log10.csv` | 아니오 |
| `logs.csv` | `Logs.csv` | 아니오 |
| `*.CSV` | `logs.csv` | 아니오 |
| `a*b*c` | `aXbYc` | 예 |
| `a*b*c` | `acb` | 아니오 |
| `?` | (빈 칸) | 아니오 |

여러 파일(제안):

- 한쪽에 파일이 여럿이면 데이터 행을 모두 합친다. 순서는 파일 경로 서수 → 줄 순서다.
- 파일마다 머리 행이 있어야 한다. 열 순서는 파일마다 달라도 된다 [P:column headers-differ-between-files].
- 키 중복은 같은 쪽 모든 파일을 합쳐서 본다 [P:key dup-across-files].
- 같은 파일이 기준·결과 양쪽에 나오면(패턴이 겹침) `overlap` 오류다 [P:glob overlap].
- → 승인 질문 2, 3.

### 4.2 CSV 읽기

T-005 예제 CSV(synthetic-examples.md 3.3)는 ASCII, BOM 없음, LF, 따옴표 없음이다 [읽음]. 사용자가 넣는 CSV는 Excel 등에서 나온다. 그래서 더 넓게 받는다.

| 항목 | 규칙 | T-005 예제 CSV | 근거 |
| --- | --- | --- | --- |
| 인코딩 | UTF-8만. 앞의 BOM 하나는 뗀다. UTF-16 BOM이면 `encoding` 오류. UTF-8이 아닌 바이트면 `encoding` 오류(바이트 위치) | ASCII, BOM 없음 | [P:csv bom, utf16le-bom, cp949-line3] |
| 줄바꿈 | LF·CRLF·CR 모두 행 끝. 마지막 줄바꿈은 없어도 된다 | LF | [P:csv crlf, cr, no-final-newline] |
| 구분자 | `,`만. `;`·탭은 구분자가 아니다(한 칸이 된다) | `,` | [P:csv semicolon-delimiter] |
| 따옴표 | RFC 4180. 칸이 `"`로 시작하면 따옴표 칸이다. 안의 `""`는 `"`. 안에 `,`·줄바꿈이 있어도 된다. 안의 CRLF·CR은 정규화에서 LF가 된다(4.6) | 없음 | [P:csv quoted-*] |
| 따옴표 오류 | 닫히지 않으면 `csv-syntax`(여는 따옴표 위치). 닫는 따옴표 뒤에 `,`·줄바꿈이 아닌 글자가 오면 `csv-syntax`(그 글자 위치) | — | [P:csv unclosed-quote, char-after-closing-quote] |
| 따옴표로 시작하지 않는 칸 | 안의 `"`는 글자 그대로. 칸 앞 공백 뒤의 `"`도 글자 그대로(따옴표 칸이 아니다) | — | [P:csv quote-inside-unquoted, space-before-quote] |
| 빈 행 | 모든 칸이 정규화(4.6) 뒤 빈 행은 건너뛴다. 줄 번호는 센다 | 없음 | [P:csv blank-lines, all-empty-rows] |
| 머리 행 | 건너뛰고 남은 첫 행. 없으면 `empty` 오류 | 첫 줄 | [P:csv empty-file, bom-only, blank-only] |
| 칸 수 | 데이터 행의 칸 수가 머리 행과 다르면 그 행은 `field-count` 오류. 끝 쉼표도 칸 하나다(머리 행과 같으면 된다) | 같음 | [P:csv field-count-*, trailing-comma-header-and-rows] |
| 줄·열 번호 | 줄은 행(칸)이 시작한 물리 줄, 1부터. 따옴표 안 줄바꿈도 센다. 열은 칸이 시작한 UTF-16 단위, 1부터(한글 1, 이모지 2). case-schema 4.9와 같다 | — | [P:csv quoted-newline line-of-next-row, column-of-cell-after-hangul, column-after-emoji] |
| 제어 문자 | 값에 NUL 등이 있어도 그대로 값이다 | 없음 | [P:csv nul-in-value] |
| 크기 | 상한 없음. 대조와 JSON 만들기 1만 행 0.1초 안팎, 10만 행 1초 안(이 환경) | 30행 | [TIMING] |

- BOM이 두 번이면 두 번째 BOM(U+FEFF)이 첫 열 이름에 붙어 `column` 오류다 [P:csv double-bom]. U+FEFF는 앞뒤 공백이 아니다(4.6).
- BOM 없는 UTF-16은 UTF-8로 읽혀 `column` 오류로 드러난다 [P:csv utf16le-nobom].
- 결과 자료에 머리 행만 있으면 오류가 아니다. 모든 항목이 누락이다(정확도 0.0, 누락률 100.0) [P:csv candidate-header-only]. 기준 자료에 데이터 행이 없으면 `no-rows` 오류다(4.4).
- → 승인 질문 4.

### 4.3 키

- 키 열은 머리 행에서 `inputs.key`와 같은 이름(정규화 뒤 서수)이다. 없으면 `column` 오류(양쪽 모두).
- 키 값은 정규화(4.6) 뒤 서수로 비교한다. 앞뒤 공백은 뗀다 [P:key trim-matches]. 대소문자, NFC·NFD는 다른 키다 [P:key case-differs, nfc-vs-nfd]. 숫자로 보지 않는다: `1`과 `1.0`은 다른 키다 [P:key numeric-text-key]. 다른 키면 누락 1 + 추가 1이 된다.
- 빈 키(정규화 뒤 빈 문자열, 공백만 포함)는 `empty-key` 오류다. 기준·결과 모두 [P:key empty-key, space-key, empty-key-candidate].
- 같은 쪽에 같은 키가 두 번 이상이면 둘째부터 `duplicate-key` 오류다. 메시지에 처음 위치를 쓴다. 같은 파일, 결과 쪽, 파일 사이, 공백을 떼고 같아진 키 모두 [P:key dup-*].
- 오류 행도 키는 등록한다. 숫자 오류가 있는 행의 키와 같은 키가 뒤에 나오면 `duplicate-key`도 난다(6.4).
- → 승인 질문 5.

### 4.4 항목과 셈

T-005 4.7의 P2를 확정한다(제안). 12절 숫자 셋(결과 30건, 90%, 3.3%)을 맞추는 읽기는 P2뿐이다 [읽음 synthetic-examples.md 4.7].

| 용어 | metrics.json | 뜻 |
| --- | --- | --- |
| 항목 | `items[]` | 기준 행 하나(키 하나). 추가 행도 `items`에 쓰지만 항목 셈(분모)에는 들지 않는다 |
| 일치 | `match` | 키가 결과에 있고, `compare` 필드가 모두 같거나 허용 오차 안 |
| 값 불일치 | `mismatch` | 키가 결과에 있고, `compare` 필드 하나 이상이 `outside` 또는 `not_number` |
| 누락 | `missing` | 기준 키가 결과에 없음 |
| 추가 | `extra` | 결과 키가 기준에 없음 |
| 허용오차 안 차이 | `within` | 일치이지만 어떤 필드 값이 기준과 다름(모두 허용 오차 안) |

- 정확도 = 일치 / 기준 행 × 100. 누락률 = 누락 / 기준 행 × 100. 반올림은 4.8.
- 추가는 정확도·누락률의 분모에 들지 않는다. 건수는 `counts.extra`로 낸다. 판정에 쓸지는 판정 카드가 정한다.
- 필드별 셈(`fields[]`): `compared` = 일치 + 값 불일치. `outside`·`within`·`not_number`는 칸 수다. 한 항목에 여러 필드 차이가 있을 수 있다. 값 불일치 항목 안의 `within` 칸도 센다.
- `compare`에 없는 열은 비교하지 않는다. 값이 달라도 일치에 영향이 없다 [P:column extra-columns-ignored]. 3.1 메모리 결과에는 남는다.
- 다른 정의와 비교 [P:alt]:

| 정의 | table-compare 정확도(%) |
| --- | --- |
| 항목 = 기준 행, 분모 = 기준 행 (제안) | 90.0 |
| 항목 = 칸(기준 행 × `compare` 필드) | 94.4 |
| 분모 = 양쪽에 있는 행 | 93.1 |
| 분모 = 기준 행 + 추가 행 | 87.1 |

- T-005 생성기가 심은 구조와 이 대조가 센 구조가 같다. 시드 0~1999에서 table-compare·form-fill·guard 계획 모두, 키마다 대조 결과(일치·허용오차 안 차이·값 불일치 필드·누락·추가)가 심은 것과 같았다(각 2,000/2,000) [P:seeds].

미검증(제안):

| 조건 | `status` | `reason` | 파일을 읽나 |
| --- | --- | --- | --- |
| `inputs` 없음 | `not_verified` | `no_inputs` | 아니오 |
| `inputs`는 있고 `compare`가 빔 | `not_verified` | `no_compare_fields` | 아니오 |
| 그 밖 | `measured` | null | 예 |

- 지시문 3.4 "미검증 = 자료 없음" [읽음]. mixed-exceptions는 `inputs`가 없다(T-005 4.12) → `no_inputs`.
- 패턴에 맞는 파일이 없거나 기준 데이터 행이 0이면 미검증이 아니라 오류다(`file`, `no-rows`). `inputs`를 적었는데 자료가 없는 것은 실수일 가능성이 크다. → 승인 질문 13.
- → 승인 질문 7.

### 4.5 숫자와 허용 오차

- 숫자 필드: `tolerance` 또는 `tolerance_pct`가 있는 `compare` 필드. `tolerance: 0`도 숫자 필드다(`1.5`와 `1.50`이 같다).
- 숫자 읽기: 정규화(4.6) 뒤 칸을 `decimal.TryParse(칸, AllowLeadingSign | AllowDecimalPoint | AllowExponent, InvariantCulture)`로 읽는다. 천 단위 구분, 통화, `%`, `NaN`, 무한대, 16진, 전각·아랍 숫자는 숫자가 아니다. 지수(`1e3`)는 받는다. `decimal` 범위를 넘으면 숫자가 아니다. 소수 29자리 이상은 반올림되고, 아주 작은 값은 0이 된다(아래 표).
- 기준 칸이 숫자가 아니면 `number` 오류다(빈 칸 포함). 기준은 사람이 확정한 정답이라 자료 오류로 본다.
- 결과 칸이 숫자가 아니면 오류가 아니라 칸 결과 `not_number`이고 그 항목은 값 불일치다(빈 칸, `N/A` 포함) [P:number candidate-not-number, candidate-empty]. 결과는 평가 대상이라 틀린 값으로 센다.
- 차이 d = |결과 − 기준| (decimal, 정확한 셈). d = 0이면 같음(차이 아님).
- 절대 허용 오차: d ≤ `tolerance`이면 `within`, 아니면 `outside`.
- 백분율 허용 오차: 기준 값 기준이다. d × 100 ≤ |기준| × `tolerance_pct`이면 `within`. 나눗셈을 쓰지 않는다. 기준이 0이면 같은 값만 통과한다. 결과 값 기준이 아니다: 기준 100·결과 99는 `within`, 기준 99·결과 100은 `outside`.
- 경계는 포함(≤)이다.
- 넘침: d가 decimal 범위를 넘으면 `outside`. 곱 d × 100만 넘치면 `outside`, |기준| × `tolerance_pct`만 넘치면 `within`, 둘 다 넘치면 d ≤ |기준| × (`tolerance_pct` / 100)으로 본다. 예외가 새지 않는다 [P:tolerance overflow *].
- → 승인 질문 8, 9.

숫자 모양. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. "값"은 읽은 decimal을 `InvariantCulture`로 쓴 것이다. 기준 칸이 숫자가 아니면 오류, 결과 칸이면 `not_number`다.

| 칸 | 숫자인가 | 값 |
| --- | --- | --- |
| `12` | 예 | `12` |
| `-0.5` | 예 | `-0.5` |
| `+3` | 예 | `3` |
| `.5` | 예 | `0.5` |
| `5.` | 예 | `5` |
| `007` | 예 | `7` |
| `1.50` | 예 | `1.50` |
| `1e3` | 예 | `1000` |
| `1.5E-2` | 예 | `0.015` |
| `1E+2` | 예 | `100` |
| `-0` | 예 | `0` |
| `79228162514264337593543950335` | 예 | `79228162514264337593543950335` |
| `0.1234567890123456789012345678901` | 예 | `0.1234567890123456789012345679` |
| `1e-400` | 예 | `0.0000000000000000000000000000` |
| `1,5` | 아니오 | — |
| `1 000` | 아니오 | — |
| `1_000` | 아니오 | — |
| `NaN` | 아니오 | — |
| `Infinity` | 아니오 | — |
| `∞` | 아니오 | — |
| `0x10` | 아니오 | — |
| `12%` | 아니오 | — |
| `$5` | 아니오 | — |
| `１２` | 아니오 | — |
| (빈 칸) | 아니오 | — |
| `-` | 아니오 | — |
| `.` | 아니오 | — |
| `e3` | 아니오 | — |
| `1e` | 아니오 | — |
| `1e400` | 아니오 | — |
| `79228162514264337593543950336` | 아니오 | — |
| `--1` | 아니오 | — |
| `+-1` | 아니오 | — |
| `1.2.3` | 아니오 | — |
| `٣` | 아니오 | — |

- 앞뒤 공백이 있는 ` 12 `도 숫자 `12`다(정규화 뒤) [P:number ' 12 '].

칸 판정. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. "허용 오차"는 `compare` 항목의 그 키, `없음`은 텍스트 필드다. "판정"의 `같음`은 차이 없음이다.

| 허용 오차 | 기준 값 | 결과 값 | 판정 |
| --- | --- | --- | --- |
| `tolerance: 0` | `5` | `5` | 같음 |
| `tolerance: 0` | `1.5` | `1.50` | 같음 |
| `tolerance: 0` | `5` | `5.0001` | `outside` |
| `tolerance: 0` | `-0` | `0` | 같음 |
| `tolerance: 0` | `1e3` | `1000` | 같음 |
| `tolerance: 2` | `10` | `12` | `within` |
| `tolerance: 2` | `10` | `8` | `within` |
| `tolerance: 2` | `10` | `12.0001` | `outside` |
| `tolerance: 2` | `10` | `7.9999` | `outside` |
| `tolerance_pct: 1.0` | `30.77` | `31.0777` | `within` |
| `tolerance_pct: 1.0` | `30.77` | `30.4623` | `within` |
| `tolerance_pct: 1.0` | `30.77` | `31.0778` | `outside` |
| `tolerance_pct: 1.0` | `100` | `101` | `within` |
| `tolerance_pct: 1.0` | `100` | `99` | `within` |
| `tolerance_pct: 1.0` | `100` | `101.01` | `outside` |
| `tolerance_pct: 1.0` | `99` | `100` | `outside` |
| `tolerance_pct: 1.0` | `0` | `0` | 같음 |
| `tolerance_pct: 1.0` | `0` | `0.0001` | `outside` |
| `tolerance_pct: 2` | `-50` | `-51` | `within` |
| `tolerance_pct: 2` | `-50` | `-51.01` | `outside` |
| `tolerance_pct: 0` | `10` | `10.0` | 같음 |
| `tolerance_pct: 0` | `10` | `10.0001` | `outside` |
| `tolerance_pct: 0.1` | `12345.678` | `12358.023678` | `within` |
| `tolerance_pct: 0.1` | `12345.678` | `12358.023679` | `outside` |
| `tolerance_pct: 1.0` | `30.77` | `약 31` | `not_number` |
| `tolerance: 0` | `5` | (빈 칸) | `not_number` |
| `tolerance: 0` | `5` | `N/A` | `not_number` |
| 없음 | `item-a` | `item-a` | 같음 |
| 없음 | `item-a` | `Item-A` | `outside` |
| 없음 | `item-a` | `item-b` | `outside` |
| 없음 | `10` | `10.0` | `outside` |
| 없음 | (빈 칸) | (빈 칸) | 같음 |
| 없음 | `a  b` | `a b` | `outside` |

- 결과 ` 5 `(숫자 필드), ` item-a `(텍스트 필드)는 앞뒤 공백을 떼서 같음이다 [P:tolerance].
- 경계 행(`10`→`12`, `30.77`→`31.0777`, `100`→`101`, `-50`→`-51`, `12345.678`→`12358.023678`)이 정확히 같은 차이다. 바로 다음 값이 `outside`다. 카드 수용 기준 3.

### 4.6 텍스트 정규화

모든 칸(머리 행, 키, 숫자 칸, 텍스트 칸)에 같은 정규화를 한 번 한다. 그 뒤 비교는 서수다.

- 한다: 칸 안의 CRLF·CR을 LF로 바꾼다. 앞뒤 공백(`string.Trim()`, `char.IsWhiteSpace`)을 뗀다.
- 하지 않는다: 대소문자 맞추기, 유니코드 정규화(NFC·NFD), 안쪽 공백 합치기, 전각→반각, 숫자 모양 맞추기(텍스트 필드의 `10`과 `10.0`은 다르다).
- 문화권과 상관없다. tr-TR에서도 `I`와 `ı`는 다르다 [P:text turkish-i].
- 허용 오차가 없는 `compare` 필드(form-fill `item`)는 이 규칙의 텍스트 비교다. 다르면 `outside`다 [P:text], [P:tolerance].
- 유니코드 정규화를 하지 않는 이유: 같아 보여도 다른 바이트면 기준과 다른 결과다. 사람이 보게 한다. 실제 업무 파일에서 NFD가 얼마나 나오는지는 **미확인**.
- → 승인 질문 10, 11.

앞뒤 글자. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. "앞뒤에서 떼나"가 `예`면 그 글자를 칸 앞뒤에 붙여도 같음이다.

| 글자 | 이름 | 앞뒤에서 떼나 |
| --- | --- | --- |
| `U+0020` | 공백 | 예 |
| `U+0009` | 탭 | 예 |
| `U+000A` | LF | 예 |
| `U+000D` | CR | 예 |
| `U+00A0` | 줄바꿈 없는 공백 | 예 |
| `U+3000` | 전각 공백 | 예 |
| `U+2028` | 줄 구분자 | 예 |
| `U+2003` | em 공백 | 예 |
| `U+200B` | 폭 없는 공백 | 아니오 |
| `U+FEFF` | BOM 글자 | 아니오 |
| `U+00AD` | 소프트 하이픈 | 아니오 |

- 칸 안의 `a\r\nb`, `a\rb`는 `a\nb`와 같다 [P:text inner-crlf-vs-lf, inner-cr-vs-lf].
- NFC `가`(U+AC00)와 NFD `가`(U+1100 U+1161)는 다르다. `é`도 같다 [P:text nfc-vs-nfd].

### 4.7 오류

오류 하나는 `CaseError(Source, Line, Column, Code, Path, Message)`다(case-schema 4.9를 그대로 쓴다). 문자열 형식도 같다: `<Source>:<Line>:<Column>: <Code> <Path>: <Message>`. 열이 0이면 `<Source>:<Line>:`, 줄도 0이면 `<Source>:`, 경로가 비면 `<Code>: <Message>`.

- `Source`: 케이스 폴더 기준 상대 파일 경로. 파일이 정해지기 전 오류(`pattern`, 폴더·파일 없음, `no-rows`)는 그 패턴(`inputs.reference` 또는 `inputs.candidate` 값)이다.
- `Path`: 열 이름(키 열 또는 `compare` 필드). 열과 상관없는 오류는 빈 문자열.
- `Message`: 한국어. 메시지 안의 값은 제어 문자를 `\uXXXX`로 쓰고, 64자를 넘으면 자르고 `…`를 붙인다.
- 오류는 모두 모은다. 한 번에 고칠 수 있게 하려는 것이다 [P:column all-errors-collected]. 단 한 파일에서 `encoding`·`empty`·`csv-syntax`가 나면 그 파일의 나머지는 보지 않는다. 머리 행에 `column`·`duplicate-column`이 있으면 그 파일의 데이터 행은 보지 않는다.
- 오류가 하나라도 있으면 지표를 내지 않는다(3.1).
- 순서: 기준 쪽 → 결과 쪽. 쪽 안에서는 파일 앞 오류(패턴) → 파일 경로 서수. 파일 안에서는 (줄, 열). 같으면 찾은 순서.
- 오류 수에 상한이 없다. 큰 파일이 모두 틀리면 오류 줄이 행 수만큼 나온다. 상한은 T-003에서도 미뤘다(classification.md 1절). → 승인 질문 14.

오류 코드. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다.

| 코드 | 언제 | 위치 | 메시지 |
| --- | --- | --- | --- |
| `pattern` | 패턴 모양이 4.1 밖 | 패턴 | `지원하지 않는 패턴이다. 폴더는 글자 그대로, 마지막 조각에만 '*'·'?'를 쓴다` |
| `file` | 폴더 없음 / 맞는 파일 없음 / 읽기 실패 / 케이스 폴더 없음 | 패턴, 읽기 실패는 그 파일 | `폴더가 없다: <폴더>` / `일치하는 파일이 없다` / `파일을 읽을 수 없다: <예외 타입>` / `폴더를 읽을 수 없다: <예외 타입>` / `케이스 폴더가 없다` |
| `overlap` | 같은 파일이 두 번 나옴(기준·결과 패턴이 겹침) | 둘째 파일 | `같은 파일이 두 번 나온다(기준·결과 패턴이 겹친다)` |
| `encoding` | UTF-16 BOM / UTF-8이 아닌 바이트 | 파일:1 / 파일:그 바이트가 있는 줄 | `UTF-16이다. UTF-8로 저장한다` / `UTF-8이 아니다 (바이트 <N>)` |
| `empty` | 빈 파일, BOM만, 빈 행만 | 파일 | `머리 행이 없다` |
| `csv-syntax` | 따옴표가 닫히지 않음 / 닫는 따옴표 뒤에 다른 글자 | 여는 따옴표 / 그 글자 | `따옴표가 닫히지 않았다` / `닫는 따옴표 뒤에 다른 글자가 있다` |
| `field-count` | 데이터 행의 칸 수가 머리 행과 다름 | 파일:줄 | `칸 수가 머리 행과 다르다 (머리 행 <N>칸, 이 행 <M>칸)` |
| `column` | 머리 행에 키 열이나 `compare` 필드 열이 없음 | 파일:머리 행 줄, 경로 = 열 이름 | `머리 행에 열이 없다` |
| `duplicate-column` | 쓰는 열(키·`compare`)이 머리 행에 둘 이상 | 둘째 칸, 경로 = 열 이름 | `머리 행에 같은 열이 둘 이상 있다 (처음: <N>번째 칸)` |
| `empty-key` | 키 칸이 정규화 뒤 빔 | 키 칸, 경로 = 키 열 | `키가 비어 있다` |
| `duplicate-key` | 같은 쪽에 같은 키 | 둘째 키 칸, 경로 = 키 열 | `키가 중복된다: <키> (처음: <파일>:<줄>)` |
| `number` | 기준 자료의 숫자 필드 칸이 숫자가 아님 | 그 칸, 경로 = 필드 | `숫자가 아니다: <값>` |
| `no-rows` | 다른 오류가 없는데 기준 데이터 행이 0 | 기준 패턴 | `기준 자료에 데이터 행이 없다` |

- 쓰지 않는 열의 중복(`a,b,b`에서 `b`를 안 쓰면)은 오류가 아니다 [P:column duplicate-unused-column]. 머리 행 이름은 앞뒤 공백을 떼고(`row_id `도 `row_id`), 대소문자는 구분한다 [P:column header-spaces-trimmed, header-case-differs].
- `column`은 기준·결과 모두에 건다. 결과 자료에 `compare` 필드 열이 없으면 오류다(모든 항목을 값 불일치로 세지 않는다) [P:column missing-in-candidate]. → 승인 질문 6.

### 4.8 퍼센트

- 소수 첫째 자리. 둘째 자리가 5 이상이면 올린다(0에서 먼 쪽. 값이 0 이상이라 "반올림"과 같다).
- 정수로 셈한다: `tenths = (2 × part × 1000 + whole) div (2 × whole)`, 퍼센트 = `tenths / 10`(소수 자릿수 1). decimal 나눗셈의 반올림 오차가 없다. whole 1~2000의 모든 (part, whole) 2,003,000쌍에서 `Math.Round(part × 100 / whole, 1, AwayFromZero)`와 같고, `ToEven`과는 1,700쌍이 다르다 [P:percent integer-formula-vs-decimal-round].
- 결과는 늘 소수 한 자리다: `90.0`, `100.0`, `0.0`.
- 주의: 1999/2000(99.95%)은 `100.0`, 1/3000(0.033%)은 `0.0`으로 보인다. 판정 카드는 퍼센트 값이 아니라 셈(`counts`)으로 문턱과 비교하길 권한다(예: 정확도 ≥ 90은 `match × 100 ≥ 90 × reference`). → 승인 질문 12.

퍼센트. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 누락률도 같은 셈이다.

| 일치 | 기준 행 | 정확도(%) |
| --- | --- | --- |
| 27 | 30 | 90.0 |
| 1 | 30 | 3.3 |
| 20 | 20 | 100.0 |
| 0 | 20 | 0.0 |
| 29 | 30 | 96.7 |
| 1 | 3 | 33.3 |
| 2 | 3 | 66.7 |
| 1 | 6 | 16.7 |
| 5 | 6 | 83.3 |
| 1 | 8 | 12.5 |
| 1 | 16 | 6.3 |
| 3 | 16 | 18.8 |
| 1 | 80 | 1.3 |
| 1 | 2000 | 0.1 |
| 1 | 3000 | 0.0 |
| 2 | 2001 | 0.1 |
| 1999 | 2000 | 100.0 |
| 1 | 1 | 100.0 |

- 1/16 = 6.25 → 6.3, 3/16 = 18.75 → 18.8, 1/80 = 1.25 → 1.3, 1/8 = 12.5 → 12.5. 0.05 경계에서 올린다.

### 4.9 값을 어디에 두나

- 지시문 13절 "분류·판정 숫자를 코드에 박지 않는다" [읽음]. T-004 4.9와 같은 방식(제안): 오류 코드, 퍼센트 자릿수, 숫자 모양(`NumberStyles`), 정규화, 메시지 값 표시는 Core `CompareRules.cs` 한 파일(`internal`)에 둔다. 테스트가 이 문서의 표(4.1 두 표, 4.5 두 표, 4.6, 4.7, 4.8, 3.2 속성, 7.1 예제)를 읽어 Core 동작과 대조한다. 스펙 표를 바꾸면 테스트가 실패하고, 그때 Core를 고친다.
- 실행 중에 이 문서를 읽지 않는다.
- 허용 오차 값은 `case.yaml`의 `compare`에서 읽는다. 코드에 기본 허용 오차가 없다.
- 스파이크 S8이 이 문서의 표 9개와 7절 블록, 6.4 블록을 읽어 후보와 대조했다 [S8].

### 4.10 결정론·원본 불변·G1·G2·G3

- `Compare`는 순수 함수다. 난수·시각·환경 변수·현재 폴더를 쓰지 않는다. 비교는 서수, 숫자는 decimal·`InvariantCulture`.
- 파일 목록은 서수로 정렬한다. OS 열거 순서를 쓰지 않는다. `Dictionary`·`HashSet`은 찾기에만 쓴다. 출력 순서는 3.1이다.
- G1: 후보 6파일을 Core에 넣고 Release `-warnaserror` 빌드, G1 테스트 61개, `dotnet format --verify-no-changes`, 전체 테스트 830개가 통과했다 [S2]. 기준선 Core와 AssemblyRef·TypeRef 네임스페이스 목록이 같다(T-004에서 `System.Text.Json`이 이미 들어왔다) [S3 refs-diff].
- G2·G3 등록(제안):

| 이름 | 샘플(`TestData/guard/…`) | 어댑터 |
| --- | --- | --- |
| `compare-case-valid` | `case-valid` (기존. `reference/rows.csv`·`candidate/rows.csv`를 더한다) | `CaseParser.ParseDirectory` → `CompareEngine.CompareDirectory` → 출력 폴더의 `metrics.json` 한 파일 |
| `compare-invalid` | `compare-invalid` (새) | 같음. 오류면 오류 줄(LF, 끝 LF)을 `compare-errors.txt` 한 파일로 |

- `case-valid`의 두 CSV는 T-005 G2 고정 계획(`GuardTargets.SyntheticGuardPlan`과 같은 값: 시드 7, `rows.csv`, 12행)으로 `SyntheticGenerator`가 만든 바이트다(SHA-256 `reference/rows.csv` `66d83729…`, `candidate/rows.csv` `9be987a9…`, 각 216바이트) [S5 guard-samples]. 대조하면 심은 구조가 그대로 나온다: 12행, 일치 8(허용오차 안 차이 2), 값 불일치 3(`start`·`mean`·`label` 하나씩), 누락 1, 추가 1, 정확도 66.7, 누락률 8.3 [S5 run-case-valid].
- `compare-invalid`는 6.4다. 정상 `case.yaml` + 오류 7건을 섞은 CSV 4개다. 오류 줄의 `Source`에 상대 경로가 들어가므로 절대 경로가 새면 R3가 잡는다.
- 결과 [S5b]: `compare-case-valid`·`compare-invalid` G2·G3 차이 0건. 기존 대상(`case-parser-valid`, `classifier-case-valid`, `synthetic-case-valid` 등)도 CSV가 늘어난 `case-valid`에서 그대로 통과한다(G2·G3 32개, 전체 834개).
- 기존 테스트 하나가 깨진다 [S5a]: `GuardTargetAdapterTests.Real_targets_use_case_samples_not_basic`가 샘플 목록(중복 뺀 것)을 `["case-invalid", "case-valid"]`로 고정해 둬서, 새 샘플 `compare-invalid`가 생기면 실패한다. 이 테스트의 뜻은 "실제 대상은 `guard/basic`을 쓰지 않는다"이다. "`basic`이 없다 + `case-valid`가 있다 + 모든 샘플 폴더가 있다"로 바꾼다 [S5b]. 약화가 아니라 새 샘플을 반영하는 변경이다. PR 본문에 적는다. → 승인 질문 18.
- 음성 대조 [S6]: (a) 숫자를 `CultureInfo.CurrentCulture`로 읽게 바꾸면 G1은 통과하고 G2가 잡는다: `G2 R4 문화권: out/metrics.json 삭제`, `out/compare-errors.txt 추가`(de-DE에서 기준 소수 칸이 숫자가 아니게 되어 오류로 바뀐다). `compare-invalid`는 `G2 R4 문화권: out/compare-errors.txt 크기, SHA-256 다름`. (b) `SourceFile.Path`에 절대 경로를 넣으면 G1은 통과하고 `G2 R3 다른 경로: out/metrics.json SHA-256 다름`(`compare-invalid`는 `out/compare-errors.txt`)으로 잡는다.

### 4.11 Core 공개 API (제안)

네임스페이스 `Scorecard.Core.Comparison`. 스파이크 후보와 같은 모양이다.

```csharp
public static class CompareEngine
{
    public const string StatusMeasured = "measured";
    public const string StatusNotVerified = "not_verified";
    public const string ReasonNoInputs = "no_inputs";
    public const string ReasonNoCompareFields = "no_compare_fields";
    public const string ItemMatch = "match";
    public const string ItemMismatch = "mismatch";
    public const string ItemMissing = "missing";
    public const string ItemExtra = "extra";
    public const string KindNumber = "number";
    public const string KindText = "text";
    public const string ResultWithin = "within";
    public const string ResultOutside = "outside";
    public const string ResultNotNumber = "not_number";

    // 케이스 폴더에서 inputs 파일을 찾아 읽고 대조한다. 미검증이면 파일을 읽지 않는다.
    public static CompareResult CompareDirectory(string caseDirectory, CaseDefinition definition);

    // 순수 함수. 바이트로 대조한다. sources를 바꾸지 않는다.
    public static CompareResult Compare(CaseDefinition definition, CompareSources sources);
}

public static class CompareInputs
{
    // 4.1. 파일을 찾아 바이트를 읽는다. 쓰지 않는다.
    public static CompareSourcesResult Read(string caseDirectory, CaseInputs inputs);
    public static bool IsSupportedPattern(string pattern);
    public static bool Matches(string namePattern, string name);
}

public static class MetricsJson
{
    public const string FileName = "metrics.json";

    // 3.2·3.3 바이트 규칙.
    public static byte[] Render(CompareMetrics metrics);
}

public sealed record SourceFile(string Path, byte[] Content);
public sealed record CompareSources(IReadOnlyList<SourceFile> Reference, IReadOnlyList<SourceFile> Candidate);
public sealed record CompareSourcesResult(CompareSources? Sources, IReadOnlyList<CaseError> Errors) { public bool IsValid { get; } }
public sealed record CompareResult(CompareMetrics? Metrics, IReadOnlyList<CaseError> Errors) { public bool IsValid { get; } }

public sealed record CompareMetrics(
    string Case, string Status, string? Reason, string? Key,
    IReadOnlyList<string> ReferenceFiles, IReadOnlyList<string> CandidateFiles,
    CompareCounts? Counts, decimal? AccuracyPct, decimal? MissingRatePct,
    IReadOnlyList<FieldSummary> Fields, IReadOnlyList<CompareItem> Items);

public sealed record CompareCounts(int Reference, int Candidate, int Match, int Mismatch, int Missing, int Extra, int Within);
public sealed record FieldSummary(string Field, string Kind, decimal? Tolerance, decimal? TolerancePct, int Compared, int Outside, int Within, int NotNumber);
public sealed record CompareItem(string Key, string Status, CsvRow? Reference, CsvRow? Candidate, IReadOnlyList<FieldDifference> Differences);
public sealed record FieldDifference(string Field, string Result, string Reference, string Candidate);

// 판정 카드용. Header·Cells는 정규화 뒤 전부(compare에 없는 열 포함). JSON에는 File·Line만 쓴다.
public sealed record CsvRow(string File, int Line, IReadOnlyList<string> Header, IReadOnlyList<string> Cells);
```

- 오류 코드·자릿수·숫자 모양·정규화는 `internal` `CompareRules.cs` 한 파일에 둔다(4.9). CSV 읽기는 `internal` `CsvText.cs`다.
- `IsSupportedPattern`·`Matches`는 테스트가 4.1 표를 직접 대조하려고 공개한다(Core에 `InternalsVisibleTo`가 없다 [읽음]).
- Core 파일 6개: `ComparisonModel.cs`, `CompareRules.cs`, `CsvText.cs`, `CompareInputs.cs`, `CompareEngine.cs`, `MetricsJson.cs`.
- → 승인 질문 1, 19.

## 5. 예외

| 상황 | 결과 | 근거 |
| --- | --- | --- |
| `definition`·`sources`·`inputs`가 null | `ArgumentNullException` | 후보 코드 |
| `caseDirectory`가 null·빈 문자열 | `ArgumentException` | 후보 코드 |
| `inputs` 없음 | 미검증 `no_inputs`. 폴더를 읽지 않는다 | [P:not-verified] |
| `compare`가 빔(`inputs`는 있음) | 미검증 `no_compare_fields`. 폴더를 읽지 않는다 | [P:not-verified] |
| 케이스 폴더가 없음(`inputs` 있음) | `file` `케이스 폴더가 없다` (양쪽) | [P:glob read 케이스 폴더 없음] |
| `Compare`에 파일 0개인 쪽 | `file` `일치하는 파일이 없다` | [P:not-verified candidate-no-files] |
| 결과 자료가 머리 행뿐 | 오류 아님. 모두 누락 | [P:csv candidate-header-only] |
| 기준 자료가 머리 행뿐 | `no-rows` | [P:csv header-only] |
| 파일·폴더 읽기 중 `IOException`·`UnauthorizedAccessException` | `file` `파일을 읽을 수 없다: <타입>` / `폴더를 읽을 수 없다: <타입>` | 후보 코드. 실제로 일으키지는 않았다(**미확인**) |
| Excel이 연 파일(`~$` 잠금 파일, 잠긴 CSV) | `*.csv`에 맞으면 읽는다. 어떤 오류가 나는지 **미확인** | 4.1 |
| `;` 구분 CSV(유럽 문화권 Excel) | `column` 오류로 드러난다 | [P:csv semicolon-delimiter] |
| 심볼릭 링크 | **미확인** | — |
| 큰 자료 | 10만 행 대조·JSON 1초 안. 모든 행에 허용오차 안 차이가 있으면 metrics.json이 약 44.7MB(`items`가 행마다 하나) | [TIMING] |
| 같은 이름을 대소문자만 바꾼 파일 둘(`a.csv`, `A.CSV`) | Linux에서는 서로 다른 파일이고 `*.csv`는 `a.csv`만 맞는다. 대소문자를 무시하는 파일 시스템에서는 둘이 같이 있을 수 없다 | [P:glob read reference/*.csv] |

## 6. 예시

### 6.1 table-compare 차이

[S3 run-table-compare]. 기준 30행, 결과 30행(섞임). `items`에 8개가 나온다.

| 키 | 상태 | 필드 | 기준 → 결과 | 셈 |
| --- | --- | --- | --- | --- |
| `log04` | 일치(허용오차 안) | `mean_value` | 62.69 → 62.82 | 0.13 × 100 = 13 ≤ 62.69 × 1.0 |
| `log07` | 값 불일치 | `mean_value` | 30.77 → 31.69 | 0.92 × 100 = 92 > 30.77 × 1.0 |
| `log08` | 값 불일치 | `length` | 274 → 268 | 6 > 0 |
| `log20` | 일치(허용오차 안) | `mean_value` | 84.15 → 84.07 | 8 ≤ 84.15 |
| `log23` | 일치(허용오차 안) | `mean_value` | 63.13 → 62.94 | 19 ≤ 63.13 |
| `log26` | 일치(허용오차 안) | `mean_value` | 55.76 → 55.54 | 22 ≤ 55.76 |
| `log27` | 누락 | — | — | 결과에 키가 없다 |
| `log31` | 추가 | — | — | 기준에 키가 없다 |

- 정확도 27/30 = 90.0, 누락률 1/30 = 3.3. 지시문 12절과 같다 [읽음].
- 12절 "불일치 3건"은 일치가 아닌 기준 항목 3건(`log07`, `log08`, `log27`)이다. 셋 다 `signal_type = irregular`다. `log31`(추가)도 `irregular`다. metrics.json에는 `signal_type`이 없다. "몰림"은 판정 카드가 3.1 메모리 결과(`CsvRow`)로 볼 수 있다(승인 질문 16).
- `signal_type`은 `compare`에 없어서 비교하지 않는다.

### 6.2 form-fill

- 기준 20행, 결과 20행, 모두 일치. 정확도 100.0, 누락률 0.0. `items`는 `[]` [S3 run-form-fill].
- `item`은 허용 오차가 없어 텍스트 비교, `quantity`는 `tolerance: 0` 숫자 비교다.

### 6.3 mixed-exceptions

- `case.yaml`에 `inputs`가 없다. 미검증 `no_inputs`. 파일을 읽지 않는다 [S3 run-mixed-exceptions].
- 지시문 12절 "자료 없음 → 전 항목 미검증" [읽음]. 평가서의 "측정 방법"은 평가서 카드에서 정한다.

### 6.4 오류 예시 (`compare-invalid`)

G2·G3 오류 표본 후보. 실제 값 없음. 파일 4개와 `case.yaml`.

`TestData/guard/compare-invalid/case.yaml`:

```yaml
# 합성 오류 예시. 실제 업무·값 없음. 대조 입력 오류(숫자 아님·키 중복·빈 키·칸 수·따옴표·열 없음)를 섞었다.
name: compare-invalid
task: 대조 오류 합성 예시
steps:
  - name: 한 단계
inputs:
  reference: reference/*.csv
  candidate: candidate/*.csv
  key: row_id
compare:
  - {field: start, tolerance: 0}
  - {field: mean, tolerance_pct: 1.0}
  - {field: label}
```

`TestData/guard/compare-invalid/reference/a.csv`:

```csv
row_id,start,mean,label
r01,10,20.50,a
r02,11,x1,b
r01,12,21.00,c
 ,13,22.00,a
r05,14,23.00
```

`TestData/guard/compare-invalid/reference/b.csv`:

```csv
row_id,label,mean,start
r02,a,24.00,15
r06,b,25.00,16
```

`TestData/guard/compare-invalid/reference/c.csv`:

```csv
row_id,start,mean,label
"r09,1,2.00,a
```

`TestData/guard/compare-invalid/candidate/a.csv`:

```csv
row_id,start,mean
r01,10,20.50
```

오류 7건 [S5 run-compare-invalid]:

```text
reference/a.csv:3:8: number mean: 숫자가 아니다: x1
reference/a.csv:4:1: duplicate-key row_id: 키가 중복된다: r01 (처음: reference/a.csv:2)
reference/a.csv:5:1: empty-key row_id: 키가 비어 있다
reference/a.csv:6: field-count: 칸 수가 머리 행과 다르다 (머리 행 4칸, 이 행 3칸)
reference/b.csv:2:1: duplicate-key row_id: 키가 중복된다: r02 (처음: reference/a.csv:3)
reference/c.csv:2:1: csv-syntax: 따옴표가 닫히지 않았다
candidate/a.csv:1: column label: 머리 행에 열이 없다
```

- `reference/a.csv` 3행 `r02`는 숫자 오류가 있어도 키를 등록한다. 그래서 `reference/b.csv`의 `r02`가 `duplicate-key`다(4.3).
- `reference/c.csv`는 따옴표가 닫히지 않아 그 파일의 나머지를 보지 않는다.
- `candidate/a.csv`는 `label` 열이 없어 데이터 행을 보지 않는다.

## 7. 예제 3종 `expected/metrics.json` 초안 (사람 확정 필요)

### 7.1 만드는 방법과 확정 절차 (제안)

1. 이 절의 블록은 스파이크 후보가 T-005 예제 파일(`examples/*/case.yaml`·`reference/`·`candidate/`)에서 실제로 낸 바이트다 [S3]. Python 독립 구현도 같은 바이트를 냈다 [S3 det-cmp python].
2. 사람이 이 문서를 승인할 때 블록 내용을 확정한다. 고치면 prototyper가 스파이크를 다시 돌려 블록을 맞춘다.
3. 승인 뒤 test-engineer가 블록을 바이트 그대로 `examples/<예제>/expected/metrics.json`으로 옮긴다. 테스트가 블록·SHA-256 줄·파일이 같은지 본다(T-004·T-005 방식). CLAUDE.md 에이전트 폴더 제한에 `examples/`가 없다 [읽음]. 카드 폴더 범위에는 있다 [읽음 카드]. → 승인 질문 17.
4. 기존 테스트 `Example_folder_has_exactly_the_planned_files`가 예제 폴더 파일 목록을 고정해 둬서, `expected/metrics.json`을 넣으면 3건 실패한다 [S7a]. 목록에 `expected/metrics.json`을 더하면 통과한다 [S7b]. 이 테스트의 뜻은 "계획한 파일만 있다"이다. 계획에 새 파일을 더하는 변경이다. PR 본문에 적는다.
5. T-004·T-005 예제 파일은 바꾸지 않는다(카드 메모). `examples/.gitattributes`(`* -text`)가 새 파일에도 걸린다. `core.autocrlf=true` clone에서 이 파일이 없으면 table-compare `metrics.json`이 4,291바이트에서 4,490바이트로 바뀌고, 있으면 그대로다 [S7 eol].
6. 실제 값 패턴 검사(`ExamplePatternTests`)가 새 파일도 본다. 걸린 것이 없다 [S7b pattern].
7. mixed-exceptions에도 `expected/metrics.json`을 둔다(미검증 모양). 지시문 5절 "expected/에는 classification.json, metrics.json, report-outline.json" [읽음].

예제 표. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. `—`는 null이다.

| 예제 | 상태 | 기준 행 | 결과 행 | 일치 | 값 불일치 | 누락 | 추가 | 허용오차 안 차이 | 정확도(%) | 누락률(%) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `table-compare` | `measured` | 30 | 30 | 27 | 2 | 1 | 1 | 4 | 90.0 | 3.3 |
| `form-fill` | `measured` | 20 | 20 | 20 | 0 | 0 | 0 | 0 | 100.0 | 0.0 |
| `mixed-exceptions` | `not_verified` | — | — | — | — | — | — | — | — | — |

- table-compare·form-fill 셈은 T-005 4.7 구조 표와 같다 [읽음 synthetic-examples.md 4.7].

### 7.2 table-compare

`examples/table-compare/expected/metrics.json` (SHA-256 `40150ae4160c44f30a9a8c961ad23ad01ae14b02f56f1214981296d83ce0d2db`):

```json
{
  "case": "table-compare",
  "compare": {
    "status": "measured",
    "reason": null,
    "key": "log_id",
    "reference_files": [
      "reference/logs.csv"
    ],
    "candidate_files": [
      "candidate/logs.csv"
    ],
    "counts": {
      "reference": 30,
      "candidate": 30,
      "match": 27,
      "mismatch": 2,
      "missing": 1,
      "extra": 1,
      "within": 4
    },
    "accuracy_pct": 90.0,
    "missing_rate_pct": 3.3,
    "fields": [
      {
        "field": "start_index",
        "kind": "number",
        "tolerance": 0,
        "tolerance_pct": null,
        "compared": 29,
        "outside": 0,
        "within": 0,
        "not_number": 0
      },
      {
        "field": "length",
        "kind": "number",
        "tolerance": 0,
        "tolerance_pct": null,
        "compared": 29,
        "outside": 1,
        "within": 0,
        "not_number": 0
      },
      {
        "field": "mean_value",
        "kind": "number",
        "tolerance": null,
        "tolerance_pct": 1.0,
        "compared": 29,
        "outside": 1,
        "within": 4,
        "not_number": 0
      }
    ],
    "items": [
      {
        "key": "log04",
        "status": "match",
        "reference": {
          "file": "reference/logs.csv",
          "line": 5
        },
        "candidate": {
          "file": "candidate/logs.csv",
          "line": 21
        },
        "differences": [
          {
            "field": "mean_value",
            "result": "within",
            "reference": "62.69",
            "candidate": "62.82"
          }
        ]
      },
      {
        "key": "log07",
        "status": "mismatch",
        "reference": {
          "file": "reference/logs.csv",
          "line": 8
        },
        "candidate": {
          "file": "candidate/logs.csv",
          "line": 8
        },
        "differences": [
          {
            "field": "mean_value",
            "result": "outside",
            "reference": "30.77",
            "candidate": "31.69"
          }
        ]
      },
      {
        "key": "log08",
        "status": "mismatch",
        "reference": {
          "file": "reference/logs.csv",
          "line": 9
        },
        "candidate": {
          "file": "candidate/logs.csv",
          "line": 27
        },
        "differences": [
          {
            "field": "length",
            "result": "outside",
            "reference": "274",
            "candidate": "268"
          }
        ]
      },
      {
        "key": "log20",
        "status": "match",
        "reference": {
          "file": "reference/logs.csv",
          "line": 21
        },
        "candidate": {
          "file": "candidate/logs.csv",
          "line": 2
        },
        "differences": [
          {
            "field": "mean_value",
            "result": "within",
            "reference": "84.15",
            "candidate": "84.07"
          }
        ]
      },
      {
        "key": "log23",
        "status": "match",
        "reference": {
          "file": "reference/logs.csv",
          "line": 24
        },
        "candidate": {
          "file": "candidate/logs.csv",
          "line": 31
        },
        "differences": [
          {
            "field": "mean_value",
            "result": "within",
            "reference": "63.13",
            "candidate": "62.94"
          }
        ]
      },
      {
        "key": "log26",
        "status": "match",
        "reference": {
          "file": "reference/logs.csv",
          "line": 27
        },
        "candidate": {
          "file": "candidate/logs.csv",
          "line": 29
        },
        "differences": [
          {
            "field": "mean_value",
            "result": "within",
            "reference": "55.76",
            "candidate": "55.54"
          }
        ]
      },
      {
        "key": "log27",
        "status": "missing",
        "reference": {
          "file": "reference/logs.csv",
          "line": 28
        },
        "candidate": null,
        "differences": []
      },
      {
        "key": "log31",
        "status": "extra",
        "reference": null,
        "candidate": {
          "file": "candidate/logs.csv",
          "line": 26
        },
        "differences": []
      }
    ]
  }
}
```

### 7.3 form-fill

`examples/form-fill/expected/metrics.json` (SHA-256 `84300fe2f8eda6d4744ddf3bfc221f9be60bacf5dd8d553e28eb0a41362a5bd5`):

```json
{
  "case": "form-fill",
  "compare": {
    "status": "measured",
    "reason": null,
    "key": "order_id",
    "reference_files": [
      "reference/orders.csv"
    ],
    "candidate_files": [
      "candidate/orders.csv"
    ],
    "counts": {
      "reference": 20,
      "candidate": 20,
      "match": 20,
      "mismatch": 0,
      "missing": 0,
      "extra": 0,
      "within": 0
    },
    "accuracy_pct": 100.0,
    "missing_rate_pct": 0.0,
    "fields": [
      {
        "field": "item",
        "kind": "text",
        "tolerance": null,
        "tolerance_pct": null,
        "compared": 20,
        "outside": 0,
        "within": 0,
        "not_number": 0
      },
      {
        "field": "quantity",
        "kind": "number",
        "tolerance": 0,
        "tolerance_pct": null,
        "compared": 20,
        "outside": 0,
        "within": 0,
        "not_number": 0
      }
    ],
    "items": []
  }
}
```

### 7.4 mixed-exceptions

`examples/mixed-exceptions/expected/metrics.json` (SHA-256 `cda477c47f0695d5f1de863227d8b43a6c92c3dd93b0410bf8f081098ae585ce`):

```json
{
  "case": "mixed-exceptions",
  "compare": {
    "status": "not_verified",
    "reason": "no_inputs",
    "key": null,
    "reference_files": [],
    "candidate_files": [],
    "counts": null,
    "accuracy_pct": null,
    "missing_rate_pct": null,
    "fields": [],
    "items": []
  }
}
```

## 8. 테스트로 옮기는 방법 (test-engineer 참고)

- 폴더 `tests/Scorecard.Tests/Comparison/`, 네임스페이스 `Scorecard.Tests.Comparison`. Trait `Spec` = `metrics`.
- 표 읽기는 기존 `SpecDocument.Table(relativePath, header)`(경로 상수 `docs/spec/metrics.md` 추가). 블록은 `SpecDocument.CodeBlocks`, SHA-256 줄은 `SpecDocument.Line`. 스파이크 S8이 같은 규칙으로 이 문서를 읽었다.
- 메모리 테스트는 `CompareEngine.Compare`에 바이트를 넘긴다(`SourceFile("reference/a.csv", bytes)`). 파일을 만들지 않는다. 스파이크 `Mem` 도우미와 같다.

| 테스트 | 확인 | 카드 수용 기준 |
| --- | --- | --- |
| 패턴(4.1) | 머리 행 `\| 패턴 \| 받나 \|`, `\| 패턴 \| 파일 이름 \| 맞나 \|`. 표의 행마다 `IsSupportedPattern`·`Matches`. 임시 폴더로 `CompareInputs.Read`: 서수 정렬, 하위 폴더 안 봄, 폴더 이름 대소문자, 일치 없음, 폴더 없음, 지원 밖 | — |
| 여러 파일(4.1) | 합치기 순서, 파일 사이 키 중복, 파일마다 다른 열 순서, `overlap` | 2 |
| CSV(4.2) | 줄바꿈 3종·BOM·마지막 줄바꿈 없음·빈 행이 같은 셈. 따옴표 4종. `csv-syntax` 2종 위치. UTF-16 BOM·잘못된 바이트(`encoding` 줄·바이트). `empty` 3종. `field-count` 2종. 줄·열 번호(따옴표 안 줄바꿈, 한글, 이모지) | 2 |
| 키(4.3) | 중복 4종, 빈 키 3종, 공백 떼고 맞음, 대소문자·NFC·`1`/`1.0`은 누락+추가 | 2 |
| 열(4.7) | 결과·기준 열 없음, 쓰는 열 중복, 안 쓰는 열 중복·추가 열 무시, 머리 행 공백·대소문자 | 2 |
| 숫자(4.5) | 머리 행 `\| 칸 \| 숫자인가 \| 값 \|`. 기준 칸이면 숫자/`number` 오류, 결과 칸이면 같음/`not_number` | 2 |
| 칸 판정(4.5) | 머리 행 `\| 허용 오차 \| 기준 값 \| 결과 값 \| 판정 \|`. 경계 행과 바로 다음 값. 넘침 6종 | 3 |
| 텍스트(4.6) | 머리 행 `\| 글자 \| 이름 \| 앞뒤에서 떼나 \|`. NFC·NFD, 대소문자, tr-TR, 안쪽 줄바꿈 | 3 |
| 오류 코드(4.7) | 머리 행 `\| 코드 \| 언제 \| 위치 \| 메시지 \|`. 표의 코드마다 그 오류를 내는 입력이 하나 이상. 오류 순서. 모두 모음 | 2 |
| 퍼센트(4.8) | 머리 행 `\| 일치 \| 기준 행 \| 정확도(%) \|`. 결과 소수 자릿수 1 | 1 |
| 미검증(4.4) | `no_inputs`(폴더 없어도), `no_compare_fields`, `file`·`no-rows` 오류 | 1 |
| JSON(3.2·3.3) | 머리 행 `\| 속성 \| 형식 \| 뜻 \|`. table-compare 경로 순서 = 표. 미검증 경로. BOM·CR 없음, 끝 `\n`, 이스케이프, decimal 자릿수 | 1 |
| 골든 | `examples/<예제>/` → `CaseParser.ParseDirectory` → `CompareDirectory` → `Render`가 `expected/metrics.json`과 바이트까지 같다. 예제 3종 | 1 |
| 예제 표(7.1) | 머리 행 `\| 예제 \| 상태 \| 기준 행 \| …`. `expected/metrics.json`의 값이 표와 같다 | 1 |
| 스펙 블록 = 예제 파일 | 7.2~7.4 블록, SHA-256 줄, 파일이 같다 | 1 |
| 결정론 | de-DE·tr-TR로 바꿔도 같은 바이트(`try/finally`). `CompareSources` 순서를 뒤집어도 같은 바이트 | 4 |
| 줄바꿈 변형 | 예제 CSV를 메모리에서 CRLF·CR·BOM·따옴표로 바꿔도 같은 바이트 | 2 |
| G2·G3 | `GuardTargets`에 `compare-case-valid`·`compare-invalid`(4.10). `case-valid`에 CSV 2개. `compare-invalid` 샘플 = 6.4 블록. 오류 표본 출력 = 6.4 오류 블록. `Real_targets_use_case_samples_not_basic` 수정 | 4 |
| 예제 폴더 파일 | `Example_folder_has_exactly_the_planned_files`에 `expected/metrics.json` 추가 | 1 |

- 표 이름(머리 행)이 이 문서에 정확히 한 번 나와야 한다. `SpecDocument.Table`이 그렇게 검사한다 [읽음]. G2 표의 머리 행은 classification.md·synthetic-examples.md와 같은 글자다. 문서마다 한 번이라 괜찮다.
- 실패 테스트 먼저: test-engineer 단계에서는 `Scorecard.Core.Comparison`이 없어 새 테스트가 컴파일되지 않는다. T-003 승인 결정 14와 같이 이 상태를 "실패하는 테스트"로 보고, 같은 브랜치에서 core-dev가 구현한다.
- 골든 테스트는 `examples/` 파일이 있어야 돈다. 파일이 없으면 실패해야 한다(빈 통과 금지).

## 9. 미확인 목록

1. GitHub ubuntu·windows 러너에서 대조·골든·G2·G3 테스트가 녹색인지.
2. Windows 파일 시스템에서 4.1 동작(대소문자를 무시하는 폴더 찾기, 열거). 서수로 직접 맞추므로 같아야 한다.
3. Excel이 연 파일(`~$` 잠금 파일, 잠긴 CSV)을 읽을 때 오류 모양.
4. 심볼릭 링크, 네트워크 드라이브, 클라우드 동기화 폴더(파일이 아직 내려오지 않음).
5. 실제 업무 CSV의 인코딩(CP949 Excel 저장 등)과 NFD 비율. CP949는 `encoding` 오류로 막는다.
6. publish 단일 exe(`win-x64`)에서 `Utf8JsonWriter`·`UTF8Encoding(throwOnInvalidBytes)` 동작.
7. 매우 큰 자료의 메모리 사용량. 10만 행까지만 쟀다.
8. 판정 카드가 metrics.json을 읽을지, `CompareResult`를 다시 만들지(11절).
9. Windows에서 만든 metrics.json의 SHA-256이 7.2~7.4와 같은지. 같은 바이트면 같아야 한다.

## 10. 스파이크 단계 목록

`spikes/T-006/run-spike.sh <작업폴더> <로그폴더>`. 커밋된 HEAD를 작업 폴더에 clone하고 `spikes/T-006/`와 이 문서(작업 트리)를 복사해 변형을 넣는다. 저장소의 `src/`, `tests/`, `examples/`는 건드리지 않는다. `summary.txt`는 마지막 실행이다.

| 단계 | 한 일 |
| --- | --- |
| S0 | clone, 스파이크 파일·스펙 복사 |
| S1 | 기준선 Release `-warnaserror` 빌드, 전체 테스트 |
| S2 | Core에 대조 후보 6파일 → 빌드 → G1 → `dotnet format --verify-no-changes` → 전체 테스트 |
| S3 | 스파이크 도구 빌드. 기준선·후보 Core의 참조 목록. 예제 3종 대조 → metrics.json 바이트 확인. 결정론(다시, 다른 경로, de-DE 프로세스), Python 독립 구현 |
| S4 | 메모리 안 실험 `PROBE`: 패턴, CSV, 키, 열, 숫자, 허용 오차, 텍스트, 퍼센트, JSON, 미검증, 예제, 문화권 7종, 줄바꿈 변형, 순서, 원본 불변, 시드 2,000개 × 3, 다른 정의, 시간 |
| S5 | G2·G3 등록: `case-valid`에 CSV 2개, `compare-invalid` 샘플, `GuardTargets` 변형 → 기존 `CoreGuardTargetTests` 그대로(a) → 변형(b) → G2·G3 → 전체 테스트. 표본 두 개 대조 |
| S6 | 음성 대조: (a) 숫자를 현재 문화권으로 → G1 → G2, (b) 파일 경로를 절대 경로로 → G2 |
| S7 | metrics.json을 clone `examples/*/expected/`에 넣고 전체 테스트(a) → 파일 목록 테스트 변형(b) → 패턴·CR 테스트. `core.autocrlf=true` clone 바이트 |
| S8 | 이 문서의 표 9개, 7절 블록·SHA-256, 6.4 블록을 읽어 후보와 대조 |

- `summary.txt`의 S5a(`Real_targets_use_case_samples_not_basic` 1건), S6(음성 대조), S7a(`Example_folder_has_exactly_the_planned_files` 3건)의 실패는 기대한 실패다.

## 11. 판정 (판정 카드에서 정한다)

이 절은 자리만 둔다. 판정 카드의 prototyper가 채운다.

- 정할 것: 통과·조건부·보류·미검증 규칙(지시문 3.4), `thresholds` 기본값과 비교 방식(case-schema 승인 질문 11), "특정 유형에만 불일치가 몰림"의 정의와 쓸 열, "설명 안 되는 불일치", 시간 지표, 미검증일 때 "무엇을 재면 되는지" 문구.
- 이 카드가 넘기는 것: `compare.counts`(정수 셈), `accuracy_pct`·`missing_rate_pct`(표시용), `fields[]`, `items[]`(위치·차이), 메모리 `CsvRow`(모든 열).
- 권고: 문턱 비교는 셈으로 한다(4.8). "몰림"에 쓸 열 이름은 `case.yaml`에 없다. 판정 카드가 스키마에 키를 더할지 정한다(승인 질문 16).
- 예제 판정 기대값(지시문 12절 [읽음]): table-compare "조건부", form-fill "통과", mixed-exceptions "미검증". 이 카드는 판정을 내지 않는다.

---

## 사람 승인 필요

질문마다 prototyper의 제안을 적었다. "제안대로"로 답할 수 있다. 질문 17은 예제 정답 확정이라 사람이 직접 봐야 한다.

1. **파일을 누가 읽나(2절, 4.11).** Core가 읽으면 glob 규칙이 Core에 있고, 부르는 쪽이 바이트를 넘기면 Core가 순수 함수다.
   - 제안: 둘 다(안 C). `CompareDirectory`(Core가 케이스 폴더에서 찾아 읽음) = `CompareInputs.Read` + `Compare`(순수 함수, 바이트). 읽기만 한다. Core가 파일을 읽는 것은 `CaseParser.ParseDirectory`와 같고 G1과 충돌하지 않는다(통과 확인). 입력 해시는 metrics.json에 넣지 않고 평가서 카드가 같은 바이트로 기록한다(지시문 2절 2).
2. **패턴(glob) 규칙(4.1).**
   - 제안: 폴더 조각은 글자 그대로, 마지막 조각에만 `*`·`?`. `**`·`[]`·`{}`·빈 조각·`.` 조각은 `pattern` 오류. 이름·폴더 비교는 서수(대소문자 구분)이고 OS 검색 패턴을 쓰지 않는다. 하위 폴더는 보지 않는다. 숨김·`~$` 파일도 이름이 맞으면 읽는다. 맞는 파일이 없거나 폴더가 없으면 `file` 오류.
3. **여러 파일(4.1).**
   - 제안: 한쪽 파일의 데이터 행을 합친다(경로 서수 → 줄). 파일마다 머리 행이 있고 열 순서는 달라도 된다. 키 중복은 쪽 전체에서 본다. 기준·결과 패턴이 같은 파일을 가리키면 `overlap` 오류.
4. **CSV 읽기(4.2).** T-005 예제(ASCII, LF, 따옴표 없음)보다 넓게 받는다.
   - 제안: UTF-8만(BOM 하나 뗌, UTF-16·CP949는 `encoding` 오류). LF·CRLF·CR. 구분자 `,`만. RFC 4180 따옴표(닫히지 않음·닫은 뒤 글자는 `csv-syntax`). 따옴표로 시작하지 않는 칸 안의 `"`는 글자 그대로(관대). 빈 행 건너뜀. 칸 수가 다르면 `field-count`.
5. **키(4.3).**
   - 제안: 정규화(앞뒤 공백) 뒤 서수 비교. 대소문자·NFC·숫자 모양은 다른 키. 빈 키는 `empty-key`, 같은 쪽 중복은 `duplicate-key`(둘째부터, 처음 위치를 메시지에). 기준·결과 모두.
6. **열(4.7).**
   - 제안: 키 열·`compare` 필드 열이 없으면 기준·결과 모두 `column` 오류(결과에 열이 없을 때 모두 불일치로 세지 않는다). 쓰는 열이 머리 행에 둘이면 `duplicate-column`. 쓰지 않는 열은 무시(중복이어도). 머리 행 이름은 앞뒤 공백을 떼고 대소문자는 구분.
7. **항목·셈(4.4).** T-005 P2를 확정한다.
   - 제안: 항목 = 기준 행. 정확도 = 일치 / 기준 행, 누락률 = 누락 / 기준 행. 추가는 분모 밖이고 `counts.extra`로만 낸다. 값 불일치 = 필드 하나 이상이 `outside`·`not_number`. 허용오차 안 차이는 일치로 세고 `counts.within`으로 따로 낸다. 필드별 셈(`compared`, `outside`, `within`, `not_number`)을 낸다. `compare`에 없는 열은 비교하지 않는다.
8. **허용 오차(4.5).**
   - 제안: 차이 d = |결과 − 기준|(decimal). 절대는 d ≤ `tolerance`. 백분율은 기준 값 기준 d × 100 ≤ |기준| × `tolerance_pct`(나눗셈 없음). 경계 포함. 기준 0이면 같은 값만. 넘침은 4.5 규칙(예외 없음).
9. **숫자 모양과 숫자 아닌 칸(4.5).**
   - 제안: InvariantCulture, 부호·소수점·지수(`1e3`) 허용. 천 단위 구분·`%`·통화·NaN·전각 숫자는 숫자 아님. 기준 칸이 숫자가 아니면 `number` 오류(빈 칸 포함). 결과 칸이 숫자가 아니면 `not_number`로 값 불일치(빈 칸·`N/A` 포함).
10. **허용 오차가 없는 필드(4.4, 4.6).** form-fill `item`.
    - 제안: 텍스트 비교(정규화 뒤 서수). 숫자로 보지 않는다(`10`과 `10.0`은 다름). 숫자로 보고 싶으면 `tolerance: 0`을 적는다.
11. **텍스트 정규화(4.6).**
    - 제안: 모든 칸에 같은 정규화. 칸 안 CRLF·CR → LF, 앞뒤 공백(`char.IsWhiteSpace`) 떼기. 대소문자·유니코드 정규화(NFC)·안쪽 공백·전각은 그대로(다르면 다름). 비교는 서수, 문화권 무관.
12. **퍼센트(4.8).**
    - 제안: 소수 첫째 자리, 0.05는 올림(0에서 먼 쪽), 정수 셈. 늘 한 자리(`90.0`, `100.0`). 99.95% 이상은 `100.0`으로 보이므로 판정 카드는 셈(`counts`)으로 문턱과 비교하길 권한다.
13. **미검증과 오류의 경계(4.4).**
    - 제안: `inputs` 없음 → `not_verified`/`no_inputs`, `compare` 빔 → `not_verified`/`no_compare_fields`(둘 다 파일을 읽지 않음). `inputs`를 적었는데 파일이 없거나 기준 데이터 행이 0이면 오류(`file`, `no-rows`). 결과 자료가 머리 행뿐이면 오류가 아니라 모두 누락.
14. **오류 모델(4.7).**
    - 제안: `CaseError`를 그대로 쓴다(`Source` = 상대 파일 경로 또는 패턴, `Path` = 열 이름). 코드 13개(4.7 표). 모두 모아 한 번에 보인다(치명 오류가 난 파일만 그 파일의 나머지를 건너뜀). 순서는 기준 → 결과, 파일 경로, 줄·열. 오류가 하나라도 있으면 지표를 내지 않는다. 오류 수 상한은 두지 않는다(T-003처럼 뒤 카드로 미룬다).
15. **metrics.json 형식(3.2, 3.3).**
    - 제안: 3.2 속성 표 그대로. 지표는 `compare` 아래. `items`는 차이가 있는 항목만(값 불일치·누락·추가·허용오차 안 차이), 칸 값은 정규화한 텍스트 문자열. 경로는 상대 경로만. 입력 해시·원문·`compare`에 없는 열은 넣지 않는다. 바이트 규칙은 classification.md 3.3과 같다. 골든 테스트는 바이트로 비교한다.
16. **판정 카드가 쓸 정보(`signal_type` 같은 다른 열).** table-compare의 "조건부"는 불일치가 `irregular`에 몰린 것에서 나와야 한다(T-005 4.7).
    - 제안: 이 카드에서는 metrics.json에 넣지 않는다. 메모리 결과 `CsvRow`가 모든 열을 갖고 있다. 어느 열로 몰림을 볼지(`case.yaml`에 키를 더할지)와 metrics.json에 무엇을 더할지는 판정 카드가 정한다. 더하면 그 카드에서 `expected/metrics.json`을 같이 고친다.
17. **예제 3종 `expected/metrics.json` 확정 (사람이 직접 확인).** 7.2~7.4 블록은 초안이다. 확인할 점:
    - table-compare: 30/30, 일치 27(허용오차 안 차이 4), 값 불일치 2(`log07` 평균, `log08` 길이), 누락 1(`log27`), 추가 1(`log31`), 정확도 90.0, 누락률 3.3. `items` 8개.
    - form-fill: 20/20 모두 일치, 100.0·0.0, `items` `[]`.
    - mixed-exceptions: `not_verified`/`no_inputs`, 나머지 null·`[]`.
    - 옮기기: 확정 뒤 test-engineer가 블록을 바이트 그대로 `examples/<예제>/expected/metrics.json`으로 옮긴다(CLAUDE.md 폴더 제한에 `examples/`가 없어 이 카드에 한해 허용 필요). `Example_folder_has_exactly_the_planned_files`의 파일 목록에 `expected/metrics.json`을 더한다(PR 본문에 적음).
    - 제안: 위 초안대로 확정하고, test-engineer가 옮기는 것과 파일 목록 테스트 변경을 허용한다.
18. **G2·G3 등록(4.10).**
    - 제안: `compare-case-valid`(샘플 `case-valid`, 출력 `metrics.json`)와 `compare-invalid`(새 샘플, 출력 `compare-errors.txt`)를 `GuardTargets`에 더한다. `case-valid`에 T-005 고정 계획의 `reference/rows.csv`·`candidate/rows.csv`를 더한다(test-engineer, `tests/`). `Real_targets_use_case_samples_not_basic`는 "`basic`이 없다 + `case-valid`가 있다 + 샘플 폴더가 있다"로 고친다. 뜻은 같다. PR 본문에 적고 reviewer가 확인한다.
19. **Core 공개 API와 진행 순서(4.11).**
    - 제안: 4.11 그대로(`CompareEngine`, `CompareInputs`, `MetricsJson`, 레코드 9개. 규칙은 `internal` `CompareRules.cs` 한 파일). test-engineer 단계의 컴파일 실패를 "실패하는 테스트"로 보고, 같은 브랜치에서 core-dev가 구현한다(T-003 승인 결정 14와 같음). PR 본문에 적는다.
