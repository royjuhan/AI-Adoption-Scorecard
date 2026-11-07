# synthetic-examples (T-005 스펙)

- 상태: 승인됨(2026-11-07, 질문 9 외 제안대로. 9는 테스트로 진행하고 원칙 문구는 사람 답 대기). 7절 예제 파일도 제안대로 확정. 승인 결과는 문서 끝.
- T-008 개정(승인됨, 2026-11-07. 승인 결과는 `docs/spec/human-signoff.md` 끝): 7.2~7.4 `case.yaml` 블록의 모든 `answers` 줄 끝에 `, human_signoff: no`를 더하고 SHA-256 줄을 맞췄다(ADR 0003). 그 밖의 블록(CSV, `intake.md`)과 생성기는 그대로다. 문서 끝 "T-008 개정".
- T-009 개정(승인됨, 2026-11-07, `signoff-final.md` 승인 결과): 4번째 공개 예제 `signoff-final`. 4.3·4.4·4.7·4.8·7.1 표에 행, 4.2·4.7·4.8 글머리, 새 6.4·7.5. 생성기 규칙과 Core는 그대로다(계획을 그대로 받는다) [T9:S2 gen]. 기존 블록·SHA-256 줄은 그대로다. 문서 끝 "T-009 개정". 근거·질문·결과는 `docs/spec/signoff-final.md`.
- 작업 카드: `docs/tasks/T-005-synthetic-examples.md`
- 작성: prototyper, 2026-11-07
- 확인 대상 커밋: `8a3afa0`
- 확인 환경: Ubuntu 24.04 x64, .NET SDK 10.0.114, YamlDotNet 18.1.0, xUnit 2.9.3. 독립 구현 대조에만 Python 3.13.18.
- 스파이크: `spikes/T-005/` (머지 대상 아님. 머지 전에 빠진다). 실행은 `spikes/T-005/run-spike.sh <작업폴더> <로그폴더>`. 요약 원문은 `spikes/T-005/summary.txt`. 이 문서의 CSV 블록과 SHA-256은 스파이크 후보(`spikes/T-005/core/Synthetic/`)가 실제로 낸 것이다.

## 0. 표기

- `[읽음]`: 커밋된 파일을 읽어서 확인한 사실.
- `[S0]`~`[S10]`: 스파이크 단계(10절). `[P:무리 이름]`: S4 `PROBE` 줄.
- **미확인**: 실험하지 않았거나 이 환경에서 확인할 수 없는 것.
- 기준 자료 = `reference/`, 결과 자료 = `candidate/`. 기준 행 = 기준 자료의 데이터 행(머리 행 제외).
- G1·G2·G3, D1, R1~R4는 `docs/spec/guard-tests.md`의 이름이다. 행 모델 A는 `docs/spec/classification.md` 4.3이다.
- 예제 이름은 `table-compare`, `form-fill`, `mixed-exceptions`(지시문 12절). T-009에서 `signoff-final`을 더한다.
- `[T9:S0]`~`[T9:S8]`: T-009 스파이크(`spikes/T-009/`) 단계. 단계 목록은 `signoff-final.md` 10절.

## 1. 범위

카드 수용 기준 1~4와 카드 메모를 다룬다. 수용 기준 5(CI 녹색)는 PR에서 확인한다.

| 카드 메모 | 이 문서 | 승인 질문 |
| --- | --- | --- |
| Core는 `System.Random`을 쓸 수 없다(G1 D1). 생성기 위치·명령 | 4.1, 4.2 | 1, 2 |
| pre-commit 검사를 테스트로 할지, git 훅을 둘지 | 4.10 | 9, 10 |
| 검증 자료 형식은 case-schema `inputs`·`compare`를 따른다 | 2절, 3.3, 4.6, 4.8 | 7, 8 |
| 대조·지표·판정은 T-006·판정 카드. `expected/metrics.json`은 T-006 | 4.7, 4.12 | 5, 12 |
| 12절 단계 수 6/5/5는 T-004 행 모델 A로 맞췄다 | 4.8 (단계·답을 바꾸지 않는다) | — |
| T-004 `answers.yaml`·`expected/classification.json`은 바꾸지 않는다 | 4.12 | 12 |

범위 밖:

- 대조·지표·판정 계산. T-006·판정 카드(`metrics.md`). 4.7 구조 표는 스파이크가 센 "심은 구조"다. 지표 정의를 정하지 않는다.
- `expected/metrics.json`, `expected/report-outline.json`(지시문 5절). T-006·평가서 카드.
- Cli 명령. M2(cli-dev).
- 제작 의뢰서 `samples/`, `export-check.md` 문자열 검사(지시문 3.6). 같은 생성기·패턴 표를 쓸 수 있다. 쓸지는 그 카드에서 정한다.
- 12.1 비공개 케이스, `cases/`, ADR 0003(최종 확인 단계).
- `tests/`·`docs/`의 실제 값 패턴 검사(4.10).

## 2. 입력

- `CaseDefinition` 하나(`CaseParser` 결과, `examples/<예제>/case.yaml`). 생성기는 `inputs.key`(키 열 이름)와 `compare`(비교 필드, `tolerance`·`tolerance_pct`)만 쓴다. `steps`, `thresholds`, `inputs.reference`·`inputs.candidate`는 쓰지 않는다 [읽음 후보 코드].
- `SyntheticPlan` 하나. 값의 범위와 심을 차이를 정한다. 공개 예제의 계획은 4.3·4.4 표에 있다. 테스트가 표를 읽어 만든다(8절). Core에는 예제 계획이 없다.
- 생성기는 파일을 읽지 않는다. `inputs`가 없으면 `ArgumentException`이다(5절). 파싱 오류가 있으면 부르는 쪽이 생성하지 않는다.

## 3. 출력

### 3.1 메모리 결과

`SyntheticTables` 하나(4.13). CSV 파일 이름, 머리 행, 기준 행, 결과 행. 행마다 `Key`, `Values`, `Planted`가 있다.

| `Planted` | 어디 | 뜻 |
| --- | --- | --- |
| `none` | 기준·결과 | 심지 않음 |
| `missing` | 기준 | 결과에서 뺀 행 |
| `mismatch:<열>` | 결과 | 그 열을 허용 오차 밖으로 바꾼 행 |
| `within:<열>` | 결과 | 그 열을 허용 오차 안에서만 바꾼 행 |
| `extra` | 결과 | 결과에만 있는 행 |

- `Planted`는 CSV에 쓰지 않는다. 테스트가 구조(4.7)를 확인할 때 쓴다.

### 3.2 예제 폴더

T-005 뒤 `examples/`. "새로"가 T-005에서 더하는 파일이다.

```
examples/
├─ .gitattributes                    T-004 그대로 (* -text)
├─ table-compare/
│  ├─ answers.yaml                   T-004 그대로
│  ├─ case.yaml                      새로 (7.2)
│  ├─ intake.md                      새로 (7.2)
│  ├─ reference/logs.csv             새로, 생성기 (7.2)
│  ├─ candidate/logs.csv             새로, 생성기 (7.2)
│  └─ expected/classification.json   T-004 그대로
├─ form-fill/                        같은 모양. CSV 이름 orders.csv (7.3)
└─ mixed-exceptions/
   ├─ answers.yaml                   T-004 그대로
   ├─ case.yaml                      새로 (7.4)
   ├─ intake.md                      새로 (7.4)
   └─ expected/classification.json   T-004 그대로
```

- `mixed-exceptions`에는 `reference/`·`candidate/`가 없다(자료 없음). → 승인 질문 12.
- `examples/*/.gitkeep` 3개는 지운다. 폴더가 비지 않는다. → 승인 질문 12.
- `expected/`에 새 파일을 넣지 않는다(4.12).

### 3.3 CSV 바이트 규칙

| 항목 | 규칙 | 근거 |
| --- | --- | --- |
| 인코딩 | ASCII 바이트만(UTF-8과 같은 바이트). BOM 없음 | [S3 csv-check], [P:csv] |
| 줄바꿈 | `\n`. CR 0개. 마지막 줄 뒤에 `\n` 하나 | [S3 csv-check] |
| 머리 행 | 첫 줄. 키 열(`inputs.key`) 다음에 4.4 열 표 순서 | [S3 gen header] |
| 구분자 | `,`. 칸 앞뒤 공백 없음 | 후보 코드 |
| 따옴표 | 쓰지 않는다. 값에 `,`·`"`·줄바꿈이 없다(생성기가 막는다) | [P:csv] |
| 값 | ASCII 인쇄 문자(0x20~0x7E). 빈 값 없음 | [P:csv] |
| 정수 | `InvariantCulture`. 천 단위 구분 없음 | 후보 코드, [P:culture] |
| 소수 | 소수점 `.`. 자릿수 고정(4.4 최소값의 자릿수, 예: `F2`) | 후보 코드, [P:culture] |
| 파일 | `reference/`·`candidate/`에 CSV 하나씩, 같은 이름(4.3 "파일") | 4.3 |
| 기준 행 순서 | 키 오름차순(만든 순서) | 4.5 |
| 결과 행 순서 | 섞는다(4.3 "결과 섞기"). 키로 맞춰야 대조된다 | 4.5 |

- RFC 4180은 CRLF를 쓴다. 여기서는 LF로 둔다. 저장소의 다른 예제 파일(`answers.yaml`, `classification.json`)과 같고, `examples/`에 `\r`이 없는지 보는 테스트가 이미 있다 [읽음 `ExampleGoldenTests`]. → 승인 질문 7.
- 같은 입력이면 같은 바이트다. 확인: 다른 프로세스, 다른 경로, `LANG=de_DE.UTF-8` 프로세스, Python 독립 구현 [S3 det-cmp]. 문화권 7종(Invariant, de-DE, tr-TR, ar-SA, ko-KR, fr-CH, th-TH)에서 SHA-256이 하나 [P:culture]. 같은 프로세스에서 두 번 같고, 시드+1이면 다르다 [P:repeat].

## 4. 규칙

### 4.1 생성기 위치와 명령

| 안 | 위치 | 좋은 점 | 나쁜 점 |
| --- | --- | --- | --- |
| A (제안) | `src/Scorecard.Core/Synthetic/` | G1이 D1(난수·시각·환경)과 N1~N6을 검사한다. G2·G3에 등록할 수 있다(4.11). 지시문 3.6 `samples/`("도구가 스키마로 생성한 합성 샘플")도 Core 생성기가 필요하다 | Core 파일이 4개 늘어난다 |
| B | `tests/` 도우미 | Core가 그대로다 | G1이 보지 않는다. 테스트에서 `System.Random`을 써도 걸리지 않는다. `samples/` 카드에서 다시 만든다 |
| C | 새 도구 프로젝트 | Core가 그대로다 | 지시문 5절 저장소 구조와 에이전트 폴더 제한에 없다 |

- 제안 A. 생성기는 판정에 쓰지 않는다. 합성 자료 만들기 전용이다. 수열은 시드로만 정해진다(4.2). CLAUDE.md "난수·현재시각을 판정에 쓰지 않음"과 맞는다.
- 후보 4파일을 Core에 넣어도 AssemblyRef·TypeRef 이름공간 목록이 기준선과 같다(차이 0줄) [S3 refs-diff].
- Release `-warnaserror` 빌드, G1 61개, `dotnet format --verify-no-changes`, 전체 테스트가 통과한다 [S2].
- 명령: T-005에는 명령이 없다(Cli는 M2). 생성기를 부르는 곳은 테스트다. 테스트가 4.3·4.4 표와 `examples/<예제>/case.yaml`로 생성하고, 커밋된 CSV와 바이트를 비교한다. 테스트는 파일을 쓰지 않는다(8절).
- 예제 CSV를 바꾸는 절차: 계획 표를 고친다 → prototyper가 스파이크 도구(`gen`)로 바이트를 만들어 7절 블록을 고친다 → 사람 승인 → test-engineer가 블록을 파일로 옮긴다. T-004 7.1과 같다.
- 합성 샘플을 만드는 Cli 명령은 M2 또는 `samples/` 카드에서 정한다.
- → 승인 질문 1.

### 4.2 수열과 시드

- 수열: SplitMix64. 64비트 부호 없는 덧셈·곱셈·시프트만 쓴다(`unchecked`).
  - 상태 `s`에 `0x9E3779B97F4A7C15`를 더한다. `z = s`.
  - `z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9`, `z = (z ^ (z >> 27)) * 0x94D049BB133111EB`.
  - 값은 `z ^ (z >> 31)`.
- `NextBelow(n)`: `Next() × n`의 상위 64비트(`Math.BigMul`). 0 이상 n 미만. 한 번에 `Next()`를 정확히 한 번 쓴다. 다시 뽑기가 없어 뽑는 횟수가 늘 같다. 결과 하나의 확률 차이는 2^-64 미만이다.
- 시드 0의 처음 세 값: `e220a8397b1dcdaf`, `6e789e6aa1b965f4`, `06c45d188009454f` [P:prng]. Python 독립 구현도 같다 [S3 xcheck].
- `SplitMix64`는 `internal`이다. Core 공개 API로도 수열이 보인다. 시드 0, 정수 열 하나(`0`~`999`), 3행, 심기·섞기 없음으로 생성하면 기준 값이 `883`, `431`, `26`이다. 처음 세 값 × 1000의 상위 64비트와 같다 [P:prng core-via-generate].
- 공개된 참조 구현의 값과 같은지는 **미확인**이다(네트워크 없이 대조할 자료가 없다).
- `System.Random`을 쓰지 않는다(G1 D1). 테스트도 수열을 따로 만들지 않고 Core를 쓴다.
- 시드 자리: 4.3 계획 표의 "시드" 칸. 예제마다 하나.
  - `case.yaml`에 넣지 않는다. 승인된 스키마(case-schema 4.3)가 바뀐다. 시드는 업무 자료가 아니다.
  - Core 상수에 넣지 않는다. 예제 계획은 제품 동작이 아니다.
- 시드 고르기: 조건을 만족하는 가장 작은 시드(0부터).
  - table-compare `8`: 기준 30행 중 `irregular`가 12행(40%)인 가장 작은 시드 [P:seeds first-seed-with-12]. 40%는 `answers.yaml` 2단계(불규칙 신호 구간 선택)의 `exception_rate: 40`이다. 시드 0~1999에서 `irregular`는 6~23행이다 [P:seeds].
  - form-fill `0`: 선택지 5개가 기준 자료에 모두 나오는 가장 작은 시드 [P:seeds first-seed-all-choices].
  - signoff-final `47`(T-009): 기준 20행 중 `unsteady`가 6행인 가장 작은 시드 [T9:S2 seed]. 6은 `answers.yaml` 3단계(기준 구간 고르기(흔들린 기록))의 `batch_size: 6`이다. 2단계(안정 기록) `batch_size: 14`와 합이 20이다. 시드 0~1999 모두 생성에 성공하고 셈(4.7)이 하나다 [T9:S2 seeds].
- 시드 0~1999 모두 생성에 성공한다. 셈(4.7)이 계획과 같다. 대조 변형 3종 모두 같다 [P:seeds]. 시드를 바꾸면 값만 바뀌고 구조는 같다.
- → 승인 질문 2.

### 4.3 생성 계획 표

테스트가 이 표를 읽는다(8절). 표 모양을 바꾸지 않는다.

| 예제 | 시드 | 파일 | 키 접두어 | 키 자릿수 | 기준 건수 | 값 불일치 열 | 누락 | 추가 | 허용오차 안 차이 | 몰림 | 결과 섞기 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `table-compare` | `8` | `logs.csv` | `log` | 2 | 30 | `length`, `mean_value` | 1 | 1 | `mean_value` 4 | `signal_type` = `irregular` | 예 |
| `form-fill` | `0` | `orders.csv` | `ord` | 2 | 20 | 없음 | 0 | 0 | 없음 | 없음 | 예 |
| `signoff-final` | `47` | `records.csv` | `rec` | 2 | 20 | `range_start`, `range_end`, `range_avg` | 1 | 1 | `range_avg` 2 | 없음 | 예 |

칸 읽기:

- `시드`: 0 이상 정수(`ulong`).
- `파일`: `reference/`·`candidate/` 아래 CSV 이름. 폴더·`:` 없이 `.csv`로 끝난다. `case.yaml`의 `inputs.reference`·`inputs.candidate`(`reference/*.csv`, `candidate/*.csv`)에 맞는다.
- `키 접두어`·`키 자릿수`: 키 = 접두어 + 행 번호(1부터, 앞을 0으로 채움). 예: `log01`~`log31`. 키 열 이름은 `case.yaml`의 `inputs.key`다.
- `기준 건수`: 기준 행 수.
- `값 불일치 열`: 열마다 한 행을 허용 오차 밖으로 바꾼다. 적힌 순서대로. `compare` 필드여야 한다. `없음`이면 0개.
- `누락`: 결과에서 뺄 기준 행 수.
- `추가`: 결과에만 있는 행 수. 키는 기준 건수+1부터.
- `허용오차 안 차이`: `` `열` n ``. 그 열을 허용 오차의 절반 안에서만 바꾼 행 수. `tolerance_pct`가 있는 필드여야 한다. `없음`이면 0.
- `몰림`: `` `열` = `값` ``. 누락·값 불일치 행을 이 값을 가진 기준 행에서만 고른다. `없음`이면 모든 기준 행에서 고른다.
- `결과 섞기`: `예`·`아니오`. 결과 행 순서를 섞는다.

### 4.4 열 표

테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다. 예제마다 적힌 순서가 키 열 다음의 CSV 열 순서다.

| 예제 | 열 | 종류 | 값 |
| --- | --- | --- | --- |
| `table-compare` | `signal_type` | 선택 | `normal`, `irregular` |
| `table-compare` | `start_index` | 정수 | `0` ~ `9000` |
| `table-compare` | `length` | 정수 | `50` ~ `500` |
| `table-compare` | `mean_value` | 소수 | `10.00` ~ `99.99` |
| `form-fill` | `item` | 선택 | `item-a`, `item-b`, `item-c`, `item-d`, `item-e` |
| `form-fill` | `quantity` | 정수 | `1` ~ `50` |
| `signoff-final` | `record_kind` | 선택 | `steady`, `unsteady` |
| `signoff-final` | `range_start` | 정수 | `0` ~ `2000` |
| `signoff-final` | `range_end` | 정수 | `3000` ~ `6000` |
| `signoff-final` | `range_avg` | 소수 | `100.0` ~ `999.9` |

- `정수`: `` `최소` ~ `최대` ``. 양 끝 포함.
- `소수`: 같다. 소수 자릿수는 최소값의 자릿수다(`10.00` → 2).
- `선택`: 선택지. 적힌 순서가 뽑는 순서다.
- `compare`에 없는 열도 둔다(`signal_type`). 대조에 쓰이지 않는다. 몰림(4.3)과 판정 카드의 "특정 유형" 확인에 쓴다(4.7).
- 값과 이름은 합성이다. 업종을 특정하지 않는다(지시문 12절).

### 4.5 뽑는 순서

수열 하나를 아래 순서로만 쓴다. 순서를 바꾸면 바이트가 바뀐다. Python 독립 구현(`spikes/T-005/xcheck/generate.py`)이 같은 순서로 같은 바이트를 냈다 [S3 det-cmp python].

1. 기준 행. 행 1..기준 건수, 열 표 순서대로 한 칸씩.
   - 정수: `최소 + NextBelow(최대 − 최소 + 1)`.
   - 소수: 자릿수 단위의 정수로 같은 방식으로 뽑고 나눈다.
   - 선택: `선택지[NextBelow(개수)]`.
2. 누락·값 불일치 행.
   - 후보: 몰림 조건에 맞는 기준 행(오름차순).
   - 앞쪽 부분 섞기: j = 0..k−1마다 `j + NextBelow(후보 수 − j)` 자리와 바꾼다. k = 누락 + 값 불일치 열 수.
   - 앞 `누락`개가 누락, 그다음이 값 불일치 열 순서의 행이다.
3. 허용오차 안 차이 행. 후보는 2에서 고르지 않은 기준 행(오름차순). 같은 부분 섞기.
4. 값 불일치 값. 값 불일치 열 순서대로.
   - 정수(`tolerance` t): 부호 `NextBelow(2)`, 크기 `t + 1 + NextBelow(20)`. 범위를 벗어나면 부호를 뒤집는다.
   - 소수(`tolerance_pct` p): 부호, 배수 `2 + NextBelow(3)`. `기준 × (1 ± p × 배수 / 100)`을 자릿수로 반올림한다(0에서 먼 쪽). 열 범위(4.4)를 벗어나면 부호를 뒤집는다. 뒤집어도 벗어나거나 허용 오차 안이면 예외.
   - 선택(허용 오차 없음): `(지금 위치 + 1 + NextBelow(개수 − 1)) % 개수`. 자기 자신은 나오지 않는다.
5. 허용 오차 안 값. 3에서 고른 순서대로. 부호, `n = 1 + NextBelow(4)`. `기준 × (1 ± p × n / 1000)`을 반올림한다. 열 범위를 벗어나면 부호를 뒤집는다. 뒤집어도 벗어나거나, 기준과 같거나, 차이가 p/2를 넘으면 예외.
6. 추가 행. 키는 기준 건수+1부터. 값은 1과 같은 방식.
7. 결과 행. 기준 순서에서 누락을 빼고 추가 행을 뒤에 붙인다. `결과 섞기`가 `예`면 Fisher–Yates(i = 끝..1마다 `NextBelow(i + 1)` 자리와 바꿈).

- 상수(정수 폭 20, 소수 배수 2~4, 허용 오차 안 0.1~0.4배)는 뽑는 순서의 일부다. 계획 표에 넣지 않는다. 분류·판정 숫자(지시문 13절)가 아니다. 바꾸면 바이트가 바뀌어 골든 테스트가 잡는다.
- 경계값(허용 오차와 꼭 같은 차이)은 심지 않는다. 허용오차 안 차이는 p/2 이하, 값 불일치는 2p 이상(정수는 t+1 이상)이다. 그래서 `≤`로 비교하든 `<`로 비교하든, %를 기준값에 곱하든 결과값에 곱하든 셈이 같다 [S3 oracle base·strict·candbase]. 경계 판단은 T-006 단위 테스트가 맡는다. → 승인 질문 6.
- → 승인 질문 4.

### 4.6 case.yaml과 계획 맞추기

| `compare` 항목 | 열 종류 |
| --- | --- |
| `tolerance`(정수 값) | 정수 |
| `tolerance_pct` | 소수 |
| 둘 다 없음 | 선택 |

- `compare` 필드는 모두 계획 열에 있어야 한다. 계획 열은 `compare`에 없어도 된다.
- 값 불일치 열은 `compare` 필드다. 허용오차 안 차이 열은 `tolerance_pct` 필드다.
- 키 열 이름(`inputs.key`)은 계획 열 이름과 겹치지 않는다.
- 맞지 않으면 `ArgumentException`(5절). 9가지 경우를 확인했다 [P:validate].

### 4.7 대조 용어와 심은 구조

지시문 3.4 [읽음]: 정확도 = "기준과 허용오차 안에서 일치한 항목 / 전체 항목", 누락률 = "기준에는 있고 결과에 없는 항목 / 기준 항목". 12절 table-compare [읽음]: "reference 30건 / candidate 30건, 불일치 3건·누락 1건 심어 둠 → 정확도 90%, 누락률 3.3%, 판정 조건부".

용어(제안). case-schema에는 없다. 대조 정의는 T-006이 확정한다.

| 용어 | 뜻 |
| --- | --- |
| 항목 | 기준 행 하나(키 하나) |
| 일치 | 키가 결과에 있고, `compare` 필드가 모두 허용 오차 안 |
| 값 불일치 | 키가 결과에 있고, `compare` 필드 하나 이상이 허용 오차 밖 |
| 누락 | 기준 키가 결과에 없음 |
| 추가 | 결과 키가 기준에 없음. 정확도·누락률 분모에 들지 않는다 |
| 허용오차 안 차이 | 일치이지만 값이 기준과 다름 |
| 불일치(12절) | 일치가 아닌 기준 항목. 값 불일치 + 누락 |

- 정확도 = 일치 / 기준 행. 누락률 = 누락 / 기준 행.
- 12절 숫자 셋(결과 30건, 정확도 90%, 누락률 3.3%)을 모두 맞추는 읽기는 P2뿐이다 [P:alt].

| 안 | 심은 것 | 결과 행 | 정확도(%) | 누락률(%) | 12절과 |
| --- | --- | --- | --- | --- | --- |
| P1 | 값 불일치 3 + 누락 1 + 추가 1 | 30 | 86.7 | 3.3 | 정확도 다름 |
| P2 (제안) | 값 불일치 2 + 누락 1 + 추가 1 | 30 | 90.0 | 3.3 | 같음 |
| P3 | 값 불일치 3 + 누락 1 | 29 | 86.7 | 3.3 | 결과 건수·정확도 다름 |
| P4 | 값 불일치 2 + 누락 1 | 29 | 90.0 | 3.3 | 결과 건수 다름 |

- P2에서 "불일치 3건"은 일치가 아닌 기준 항목 3건(값 불일치 2 + 누락 1)이다. "누락 1건"은 그중 하나다. 결과 30건을 맞추려고 추가 행 1건을 둔다.
- 다른 읽기도 확인했다 [S3 oracle]. 항목을 칸(기준 행 × `compare` 필드 3개)으로 읽으면 P2 정확도가 94.4%다. 분모를 기준과 결과에 모두 있는 행으로 읽으면 93.1%다. 둘 다 90%가 아니다. 그래서 항목 = 기준 행으로 읽는다.
- → 승인 질문 5.

구조 표. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다.

| 예제 | 기준 행 | 결과 행 | 일치 | 값 불일치 | 누락 | 추가 | 허용오차 안 차이 | 정확도(%) | 누락률(%) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `table-compare` | 30 | 30 | 27 | 2 | 1 | 1 | 4 | 90.0 | 3.3 |
| `form-fill` | 20 | 20 | 20 | 0 | 0 | 0 | 0 | 100.0 | 0.0 |
| `signoff-final` | 20 | 20 | 16 | 3 | 1 | 1 | 2 | 80.0 | 5.0 |

- 생성 파일을 키로 맞춰 센 값이 이 표와 같다 [S10 structure]. 대조 변형 3종(`≤`, `<`, 결과값 기준 %)이 같은 값을 낸다 [S3 oracle].
- 이 표는 T-005의 자료 구조 확인이다. `expected/metrics.json`은 T-006이 만든다. T-006 정의가 이 표와 다르게 세면, 자료와 정의 중 무엇을 바꿀지 그 카드에서 사람이 정한다.

판정과의 관계(판정은 판정 카드):

- 지시문 3.4 [읽음]: 조건부 = "정확도는 충족하나 시간이 줄지 않았거나 특정 유형에만 불일치가 몰림". 통과는 시간 조건도 본다.
- 예제에는 시간 자료가 없다(`minutes_per_item`을 적지 않았고 시간 열도 없다). 그래서 `time_must_drop: false`로 시간 조건을 끈다(4.8). table-compare의 "조건부"는 "특정 유형에만 몰림"에서 나와야 한다.
- 그래서 불일치 3건(값 불일치 2 + 누락 1)을 모두 `signal_type = irregular` 행에 심었다. `irregular` 12행 중 3행(25%)이 불일치이고, `normal` 18행은 불일치가 0이다 [S3 gen focus]. 허용오차 안 차이 4행은 일치라 몰림과 상관없다.
- "몰림"을 알아내는 규칙(어느 열이 유형인지, 몇 %면 몰림인지)은 case.yaml 스키마에 없다. 판정 카드가 정한다(9절 6).
- form-fill은 일치 20/20(100%), 누락 0%다. `accuracy_min` 85 이상, `missing_max` 5 이하, 시간 조건 꺼짐이라 "통과"가 될 수 있다.
- T-009 signoff-final(자료 구조 안 A, juhan 결정 2026-11-07): 일치 16/20(80.0%), 누락 1/20(5.0%)이다. `accuracy_min: 95`, `missing_max: 2`에 둘 다 못 미친다. 공개 예제에서 처음으로 문턱 미달이다(지시문 3.4 "보류 = 정확도 미달"의 모양). 불일치 4건(값 불일치 3 + 누락 1) 중 3건이 `unsteady`, 1건이 `steady`다. 몰림을 두지 않았다 [T9:S2 planted]. 판정은 판정 카드가 정한다. 다른 안은 `signoff-final.md` 4.3.

### 4.8 case.yaml

- `name`, `task`, `steps`는 T-004 `answers.yaml`과 같다. `answers.yaml`의 `name:` 줄부터 끝까지가 `case.yaml` 안에 바이트 그대로 들어 있다. 앞 주석 줄만 다르다 [S3 case-check].
- T-008: 두 파일의 같은 `answers` 줄에 `, human_signoff: no`를 더한다. 그래서 위 관계는 그대로다 [T8:S3 case-in-answers].
- 확인: 파싱 오류 0건, `name`·`task`·`steps`가 같다, `case.yaml`을 분류한 결과 = `expected/classification.json` 바이트 [S3 case-check]. 카드 수용 기준 4.
- 더하는 키:

| 예제 | `inputs` | `compare` | `thresholds` |
| --- | --- | --- | --- |
| `table-compare` | `reference/*.csv`, `candidate/*.csv`, 키 `log_id` | `start_index` `tolerance: 0`, `length` `tolerance: 0`, `mean_value` `tolerance_pct: 1.0` | `accuracy_min: 85`, `missing_max: 5`, `time_must_drop: false` |
| `form-fill` | `reference/*.csv`, `candidate/*.csv`, 키 `order_id` | `item`(허용 오차 없음), `quantity` `tolerance: 0` | 같음 |
| `mixed-exceptions` | 없음 | 없음 | 없음 |
| `signoff-final` | `reference/*.csv`, `candidate/*.csv`, 키 `record_id` | `range_start` `tolerance: 0`, `range_end` `tolerance: 0`, `range_avg` `tolerance_pct: 1.0` | `accuracy_min: 95`, `missing_max: 2`, `time_must_drop: false` |

- `compare` 모양은 지시문 12.1 예시(구간 시작·끝 0, `mean_value` 1.0%)를 따랐다 [읽음].
- `accuracy_min: 85`: 90%와 100%가 모두 충족한다. 90에 두면 90.0이 경계에 걸린다. 경계는 판정 카드 단위 테스트로 본다. case-schema 4.5 "`table-compare`는 `accuracy_min`이 90 이하여야" [읽음]와 맞다.
- `missing_max: 5`: 3.3%가 충족한다.
- `time_must_drop: false`: 4.7. 실제 케이스(12.1)는 `true`다. `case.yaml`에 주석 한 줄로 이유를 적는다.
- `before`·`after`·`ai_usage`는 넣지 않는다. 12절에 값이 없다. 평가서 카드가 필요하면 그때 더한다.
- mixed-exceptions는 `inputs`·`compare`·`thresholds`가 없다 [S3 case-check inputs=null]. 판정 카드에서 "자료 없음 → 미검증"이 된다(12절).
- T-009 signoff-final: `compare`와 `thresholds` 숫자는 12.1 예시와 같다(구간 시작·끝 0, 평균 1.0%, 95, 2) [읽음]. 열 이름은 새로 지었다. `time_must_drop`은 시간 자료가 없어 `false`다(table-compare와 같은 이유, 주석 한 줄). 4.8 표의 행은 위 행이 `없음`이라 `같음`을 쓰지 않았다.
- → 승인 질문 8.

### 4.9 intake.md

- 사용자의 자유 서술 예시다(지시문 12절). 저장만 하고 구조화하지 않는다(CLAUDE.md "자유 서술은 저장만"). 어떤 테스트도 내용을 해석하지 않는다.
- 쓰는 법(제안):
  - 한글 짧은 문장. 문단만 쓴다(제목·목록 없음).
  - 업종·회사·사람 이름·코드 모양 값을 쓰지 않는다.
  - 건수·빈도는 `answers.yaml`과 맞춘다. table-compare 매주 30개, form-fill 매주 20장, mixed-exceptions 하루 15통.
  - 답이 어디서 왔는지 보이게 쓴다. form-fill "이 기능이 있는지는 확인해 보지 않았다" → `existing_feature` 기본값 `unknown`. "매번 AI를 쓴다" → `ai_each_time`.
- UTF-8, BOM 없음, LF, 끝에 `\n` [S3 text-check].
- 실제 값 패턴 검사(4.10) 대상이다.
- 사람이 직접 읽고 확정한다. → 승인 질문 13.

### 4.10 실제 값 패턴 검사

지시문 2절 10: "pre-commit에서 품번·이메일·경로 패턴을 검사한다" [읽음]. 카드 수용 기준 2.

방식 비교:

| 안 | 언제 도나 | 건너뛰기 | 설정 | 스파이크 |
| --- | --- | --- | --- | --- |
| T (제안) | `dotnet test`. CI(ubuntu·windows, 모든 PR), Claude 세션 Stop 훅, 로컬 | 테스트를 지우거나 고쳐야 한다. 리뷰에 보인다 | 없음 | 심은 값 9종을 모두 잡는다. 패턴 테스트 2개가 수십 ms [S8] |
| H | `git commit` 전(`core.hooksPath`) | `git commit --no-verify`. 훅을 안 켠 clone | clone마다 `git config core.hooksPath .githooks` | 심은 값이면 커밋이 막힌다. 커밋마다 약 3초(빌드 확인 + 테스트). `--no-verify`면 훅 없이 통과한다 [S9] |
| T + H | 둘 다 | H는 건너뛸 수 있다 | H와 같다 | — |

- 제안 T. CI는 [읽음 `.github/workflows/ci.yml`] 모든 PR에서 ubuntu·windows로 `dotnet test`를 돌린다. Stop 훅도 `dotnet test`를 돌린다 [읽음 `.claude/settings.json`]. 훅은 clone마다 켜야 하고 `--no-verify`로 건너뛴다 [S9].
- 원칙 문구("pre-commit")와 다르다. 원칙을 바꾸는 일이라 사람이 정한다(지시문 2절 "바꾸려면 사람에게 묻는다"). → 승인 질문 9.
- git 훅이 필요하면 테스트만 부르는 얇은 훅(`spikes/T-005/hooks/pre-commit`)을 별도 카드로 더한다. 훅 폴더(`.githooks/`)는 에이전트 폴더 제한 어디에도 없다 [읽음 CLAUDE.md]. 훅은 작업 트리 파일을 본다. 스테이지된 내용이 아니다.

검사 범위:

- `examples/` 아래 모든 파일. NUL 바이트가 있는 파일은 뺀다(기존 `\r` 테스트와 같은 판정).
- 파일 내용의 줄마다, 그리고 저장소 기준 상대 경로에도 건다.
- 저장소 전체로 넓히지 않는다. 스파이크가 clone 전체를 검사하니 맞는 것이 많았다 [S8 scan-repo]. `.sln`의 GUID(`part-long-code`, `part-digit-groups`), `.gitignore`·문서·테스트의 `/cases/`·`/bin/` 같은 줄(`posix-path`), 경고·규칙 코드(`part-letters-digits`), 테스트의 일부러 만든 경로 표본, 이 문서의 패턴 예. 모두 실제 값이 아니다. `src/`는 0건이다. 예외 목록 없이는 넓힐 수 없다.
- `cases/`는 `.gitignore`라 커밋되지 않는다 [읽음].

패턴 표. 테스트가 이 표를 읽는다. 표 모양을 바꾸지 않는다.

| 패턴 | 무엇 | 정규식 | 잡는 예 | 잡지 않는 예 |
| --- | --- | --- | --- | --- |
| `email` | 이메일 주소 | `[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+` | `someone@example.com`, `a.b+c@mail.example.org` | `item-a`, `메일 접수`, `a@b` |
| `url` | URL(경로의 한 종류) | `[A-Za-z][A-Za-z0-9+.-]*://` | `https://intranet.example/x`, `file:///C:/x` | `reference/*.csv`, `time_must_drop: true` |
| `windows-path` | Windows 드라이브 경로 | `(?<![A-Za-z0-9])[A-Za-z]:[\\/]` | `C:\Users\someone`, `D:/share/x` | `key: log_id`, `{field: item}`, `https://x` |
| `unc-path` | 네트워크 공유 경로 | `\\\\[A-Za-z0-9._$-]+\\` | `\\server\share`, `\\10.0.0.1\d$` | `a\b`, `\t` |
| `posix-path` | 절대 경로(`/`로 시작하는 폴더) | `(?<![\w.~-])/[A-Za-z0-9._-]+/` | `/home/someone/x`, `/data/ref.csv`, `"/mnt/share/"` | `reference/*.csv`, `docs/spec/a.md`, `센서/로그/`, `1/2/3` |
| `home-path` | 홈 폴더 경로 | `(?<!\w)~[/\\]` | `~/work`, `~\docs` | `minutes_per_item: ~`, `10~20` |
| `part-letters-digits` | 품번 모양: 영문 1~4자 + 숫자 4자 이상 | `(?<![A-Za-z0-9])[A-Za-z]{1,4}[-_]?[0-9]{4,}(?![0-9])` | `ABC-12345`, `PN123456`, `ab_2024` | `log01`, `ord20`, `SHA-256`, `UTF-8`, `item-a` |
| `part-digit-groups` | 품번·전화번호 모양: 숫자 묶음을 `-`로 이음 | `(?<![0-9-])[0-9]{2,}-[0-9]{3,}(-[0-9]+)*(?![0-9])` | `12-3456-789`, `010-1234-5678` | `2026-11-07`, `10-20`, `0 ~ 9000` |
| `part-long-code` | 품번 모양: 대문자·숫자를 섞은 8자 이상 | `(?<![A-Za-z0-9])(?=[A-Z0-9]*[A-Z])(?=[A-Z0-9]*[0-9])[A-Z0-9]{8,}(?![A-Za-z0-9])` | `A1B2C3D4`, `XK2049Q7Z` | `SEGMENTS`, `20261107`, `log01`, `abc12345x` |

- 정규식은 `RegexOptions.CultureInvariant`, 시간 제한 1초로 만든다. 칸의 백틱 안 글자가 정규식 그대로다.
- 잡는 예는 모두 걸리고, 잡지 않는 예는 하나도 걸리지 않는다 [P:pattern]. 테스트도 이 자기 검사를 먼저 한다(8절).
- .NET의 `\w`는 한글도 포함한다. 그래서 `센서/로그/`는 `posix-path`에 걸리지 않는다 [P:pattern 한글/뒤].
- 생성기가 만드는 값 모양(키, 선택지, 숫자 213개)은 하나도 걸리지 않는다 [P:pattern generator-shapes].
- 예제 파일 16개에서 0건 [S8 scan-examples]. clone의 `examples/` 테스트도 0건으로 통과한다 [S8 pattern-test].
- 일부러 심은 값은 모두 걸린다. 이메일, Windows·UNC·POSIX·홈 경로, URL, 품번 세 모양 [S8 pattern-test-planted]. URL은 `posix-path`에도 걸린다(`//host/` 부분).
- 일부러 넓게 잡는다. 실제 품번 모양은 회사마다 다르다. 이 표는 일반 모양이다. 회사 품번 모양은 사람이 행을 더한다. 공개 문서이므로 실제 모양을 그대로 적지 않고 일반화해서 적는다.
- 오탐 처리(제안): 패턴을 고치거나 예외 목록을 두지 않고, 합성 값을 바꾼다. 예: `ab_2024`가 필요하면 `ab-x`로 쓴다. 예외 목록은 실제 값을 숨기는 길이 된다. 패턴이 정말 틀렸으면 이 표를 고치고 PR 본문에 이유를 쓴다(reviewer 확인).
- 실패 메시지에 걸린 것을 모두 보인다(`패턴 파일:줄: 값`). xUnit의 컬렉션 출력은 잘려서 `Assert.True(…, 메시지)`로 쓴다 [S8].
- → 승인 질문 10.

### 4.11 G1·G2·G3

- G1: 후보 4파일을 넣고 61개 통과 [S2]. `System.Random`·시각·환경 변수를 쓰지 않는다.
- G1이 못 잡는 것: 생성기가 소수를 현재 문화권으로 다시 읽게 바꿔도 G1은 통과한다. G2가 잡는다(`synthetic-case-valid` 실패) [S6]. 그래서 G2 등록이 필요하다.
- G2·G3 등록(제안):

| 이름 | 샘플(`TestData/guard/…`) | 어댑터 |
| --- | --- | --- |
| `synthetic-case-valid` | `case-valid` (기존) | `CaseParser.ParseDirectory` → `SyntheticGenerator.Generate`(테스트 안 고정 계획) → `SyntheticCsv.Render` → 출력 폴더의 `reference/rows.csv`, `candidate/rows.csv` |

- 고정 계획 [읽음 `spikes/T-005/tests-variant/GuardTargets.cs`]: 시드 7, `rows.csv`, 키 `r` 2자리, 12행. 열 `start` 정수 0~999, `mean` 소수 10.00~99.99, `label` 선택 a·b·c. 값 불일치 `start`·`mean`·`label`, 누락 1, 추가 1, 허용오차 안 차이 `mean` 2, 몰림 없음, 섞기. `case-valid`의 `inputs.key`(`row_id`)와 `compare`(`start` 0, `mean` 1.0%, `label`)에 맞췄다. 세 열 종류와 심기 다섯 가지를 모두 쓴다.
- 결과: G2·G3 테스트 28개 통과(`synthetic-case-valid`의 G2·G3 각 1건 포함), 전체 테스트 632개 통과 [S5].
- 기존 테스트는 바뀌지 않는다. `Real_targets_use_case_samples_not_basic`는 T-004에서 이미 중복을 빼고 비교한다 [읽음 `CoreGuardTargetTests.cs`].
- → 승인 질문 11.

### 4.12 examples/ 관리

- T-004 파일(`answers.yaml`, `expected/classification.json`, `.gitattributes`)은 바꾸지 않는다(카드 메모).
- `examples/*/.gitkeep` 3개를 지운다. 폴더에 파일이 생긴다. 지우지 않고 둔 채로도 기존 테스트가 모두 통과한다 [S7]. 지우는 이유는 정리뿐이다.
- `examples/.gitattributes`(`* -text`)가 새 파일에도 걸린다. `core.autocrlf=true` clone에서 이 파일이 없으면 table-compare 4개 파일의 바이트가 바뀌고(`case.yaml` 1,564 → 1,590, `intake.md` 568 → 575, CSV 920 → 951), 있으면 그대로다 [S7].
- 기존 테스트 `Example_text_files_have_no_carriage_return`이 새 CSV·YAML·MD도 본다 [읽음 `ExampleGoldenTests.cs`].
- mixed-exceptions에 `reference/`·`candidate/`를 두지 않는다. 지시문 12절은 "각 예제에 … `reference/`, `candidate/`, `expected/`가 있다"와 mixed-exceptions "자료 없음"을 같이 쓴다 [읽음]. 빈 폴더를 `.gitkeep`으로 남기면 "빈 자료"와 "자료 없음"이 헷갈린다. `inputs`가 없다는 것으로 "자료 없음"을 나타낸다. → 승인 질문 12.
- `expected/`에 새 파일을 넣지 않는다. `metrics.json`은 T-006, `report-outline.json`은 평가서 카드다(카드 메모, 지시문 5절).
- 블록을 파일로 옮기는 것은 test-engineer다(T-004와 같음). CLAUDE.md 에이전트 폴더 제한에는 `examples/`가 없다 [읽음]. 카드 폴더 범위에는 있다 [읽음 카드]. → 승인 질문 12.

### 4.13 Core 공개 API (제안)

네임스페이스 `Scorecard.Core.Synthetic`. 스파이크 후보와 같은 모양이다.

```csharp
public static class SyntheticGenerator
{
    public const string PlantedNone = "none";
    public const string PlantedMissing = "missing";
    public const string PlantedExtra = "extra";
    public const string PlantedMismatchPrefix = "mismatch:";
    public const string PlantedWithinPrefix = "within:";

    // 순수 함수. 파일·시각·환경 변수·System.Random을 쓰지 않는다. 4.5 순서.
    public static SyntheticTables Generate(CaseDefinition definition, SyntheticPlan plan);
}

public static class SyntheticCsv
{
    public const string ReferenceFolder = "reference";
    public const string CandidateFolder = "candidate";

    // 3.3 바이트 규칙. 규칙에 맞지 않는 값은 ArgumentException.
    public static byte[] Render(IReadOnlyList<string> header, IReadOnlyList<SyntheticRow> rows);
    public static void CheckCell(string value);
}

public sealed record SyntheticPlan(
    ulong Seed, string FileName, string KeyPrefix, int KeyDigits, int Rows,
    IReadOnlyList<SyntheticColumn> Columns, IReadOnlyList<string> MismatchColumns,
    int Missing, int Extra, string? WithinColumn, int Within,
    string? FocusColumn, string? FocusValue, bool ShuffleCandidate);

public sealed record SyntheticColumn(
    string Name, string Kind, decimal Min, decimal Max, int Scale, IReadOnlyList<string> Choices)
{
    public const string KindInteger = "integer";
    public const string KindDecimal = "decimal";
    public const string KindChoice = "choice";

    public static SyntheticColumn Integer(string name, int min, int max);
    public static SyntheticColumn Decimal(string name, decimal min, decimal max, int scale);
    public static SyntheticColumn Choice(string name, params string[] choices);
}

public sealed record SyntheticTables(
    string FileName, IReadOnlyList<string> Header,
    IReadOnlyList<SyntheticRow> Reference, IReadOnlyList<SyntheticRow> Candidate);

public sealed record SyntheticRow(string Key, IReadOnlyList<string> Values, string Planted);

// internal sealed class SplitMix64 { ulong Next(); int NextBelow(int bound); }
```

- 상수(4.5)는 `SyntheticGenerator` 안 `private const`다.
- 예제 CSV 이름은 계획 표(4.3)에 있다. Core 상수가 아니다.
- Core에는 `InternalsVisibleTo`가 없다 [읽음]. 테스트는 공개 API로 수열을 본다(4.2).
- → 승인 질문 14.

## 5. 예외

| 상황 | 결과 | 근거 |
| --- | --- | --- |
| `definition`·`plan`이 null | `ArgumentNullException` | 후보 코드 |
| `case.yaml`에 `inputs`가 없음 | `ArgumentException` | [P:validate case-without-inputs] |
| `compare` 필드가 계획 열에 없음 | `ArgumentException` | [P:validate compare-field-not-in-columns] |
| 값 불일치 열이 `compare` 필드가 아님 | `ArgumentException` | [P:validate mismatch-on-non-compare] |
| 허용오차 안 차이 열에 `tolerance_pct`가 없음 | `ArgumentException` | [P:validate within-on-integer] |
| `tolerance_pct` 필드가 정수 열 | `ArgumentException` | [P:validate pct-on-integer-column] |
| 키 자릿수가 모자람(기준+추가 건수의 자릿수보다 작음) | `ArgumentException` | [P:validate key-digits-too-small] |
| 선택지에 `,`나 한글 | `ArgumentException` | [P:validate choice-with-comma, non-ascii-choice] |
| 파일 이름에 폴더 | `ArgumentException` | [P:validate file-name-with-folder] |
| 몰림 행이 누락 + 값 불일치보다 적음 | `ArgumentException`. 시드나 계획을 바꾼다 | [P:seeds small-focus] (시드 50개 중 39개) |
| 범위가 좁아 불일치 값을 못 만듦 | `ArgumentException` | 후보 코드 |
| CSV 값에 `,` `"` `\n` `\r` `\t`, 한글, 빈 값 | `ArgumentException` | [P:csv] |
| 같은 열 이름 | `ArgumentException` | 후보 코드 |

- 메시지는 한글이고, 어느 필드·값인지 적는다 [P:validate].
- 예외는 계획이나 `case.yaml`이 틀렸다는 뜻이다. 예제 계획(4.3)은 시드 0~1999에서 예외가 없다 [P:seeds].

## 6. 예시

### 6.1 table-compare에 심은 행

[S3 gen-table-compare]

| 키 | `signal_type` | 심은 것 | 기준 → 결과 |
| --- | --- | --- | --- |
| `log27` | irregular | 누락 | 결과에 없음 |
| `log08` | irregular | 값 불일치 `length` | 274 → 268 (허용 오차 0) |
| `log07` | irregular | 값 불일치 `mean_value` | 30.77 → 31.69 (2.99%, 허용 1.0%) |
| `log31` | irregular | 추가 | 기준에 없음 |
| `log20` | irregular | 허용오차 안 `mean_value` | 84.15 → 84.07 (0.10%) |
| `log04` | normal | 허용오차 안 `mean_value` | 62.69 → 62.82 (0.21%) |
| `log26` | normal | 허용오차 안 `mean_value` | 55.76 → 55.54 (0.39%) |
| `log23` | normal | 허용오차 안 `mean_value` | 63.13 → 62.94 (0.30%) |

- `irregular` 기준 행 12개: `log01`, `log06`, `log07`, `log08`, `log11`, `log19`, `log20`, `log21`, `log22`, `log25`, `log27`, `log28`. 불일치 3건(`log07`, `log08`, `log27`)이 모두 여기 있다.
- 결과 행 순서는 섞였다. 첫 행은 `log20`, 추가 행 `log31`은 25번째다.

### 6.2 form-fill

- 20행 모두 기준과 결과가 같다. 결과 행 순서만 섞였다 [S3 gen-form-fill].
- 품목 5종이 모두 나온다. `item-a` 4, `item-b` 4, `item-c` 4, `item-d` 2, `item-e` 6.

### 6.3 mixed-exceptions

- CSV가 없다. `case.yaml`에 `inputs`·`compare`·`thresholds`가 없다 [S3 case-check].

### 6.4 signoff-final에 심은 행 (T-009)

[T9:S2 planted]

| 키 | `record_kind` | 심은 것 | 기준 → 결과 |
| --- | --- | --- | --- |
| `rec14` | steady | 누락 | 결과에 없음 |
| `rec04` | unsteady | 값 불일치 `range_start` | 345 → 358 (허용 오차 0) |
| `rec18` | unsteady | 값 불일치 `range_end` | 3416 → 3400 (허용 오차 0) |
| `rec20` | unsteady | 값 불일치 `range_avg` | 785.9 → 762.3 (3.00%, 허용 1.0%) |
| `rec21` | unsteady | 추가 | 기준에 없음 |
| `rec06` | steady | 허용오차 안 `range_avg` | 392.2 → 391.8 (0.10%) |
| `rec11` | steady | 허용오차 안 `range_avg` | 582.6 → 583.8 (0.21%) |

- `unsteady` 기준 행 6개: `rec03`, `rec04`, `rec12`, `rec18`, `rec19`, `rec20`. `steady`는 14개다.
- 몰림이 없다(4.3 `몰림` 칸 `없음`). 누락·값 불일치 행은 모든 기준 행에서 골랐다.
- 결과 자료도 행 순서가 섞였다. 첫 행은 `rec04`, 추가 행 `rec21`은 9번째다.
- 이 표와 위 줄은 테스트가 읽지 않는다. 머리 행이 6.1과 다르다(`record_kind`). test-engineer가 6.1처럼 읽게 할 수 있다. 그때도 6.1 줄과 같은 앞부분(`- 결과 행 순서는 섞였다.`)을 쓰지 않는다. `SpecDocument.Line`은 문서에서 한 번만 나오는 줄을 찾는다. 같은 앞부분으로 쓴 중간 초안에서 `Table_compare_focus_and_order_lines_match_section_6_1`이 실패했다.

## 7. 예제 파일 초안 (사람 확정 필요)

### 7.1 만드는 방법과 확정 절차 (제안)

1. 이 절의 블록은 prototyper 초안이다. CSV 블록은 스파이크 후보가 4.3·4.4 표와 초안 `case.yaml`로 실제 낸 바이트다 [S3]. `case.yaml`·`intake.md`는 prototyper가 썼다.
2. 사람이 승인할 때 블록 내용을 확정한다. 고치면 prototyper가 스파이크를 다시 돌려 블록과 SHA-256을 맞춘다.
3. 승인 뒤 test-engineer가 블록을 바이트 그대로 `examples/<예제>/<파일>`로 옮긴다(블록 줄마다 LF, 끝에 LF). 테스트가 블록·SHA-256 줄과 파일이 같은지 본다(T-004 방식).
4. 블록 순서는 아래 파일 표 순서다. 테스트가 이 표를 읽는다. 7.2~7.4에는 이 블록만 둔다.

| 예제 | 파일 |
| --- | --- |
| `table-compare` | `case.yaml` |
| `table-compare` | `intake.md` |
| `table-compare` | `reference/logs.csv` |
| `table-compare` | `candidate/logs.csv` |
| `form-fill` | `case.yaml` |
| `form-fill` | `intake.md` |
| `form-fill` | `reference/orders.csv` |
| `form-fill` | `candidate/orders.csv` |
| `mixed-exceptions` | `case.yaml` |
| `mixed-exceptions` | `intake.md` |
| `signoff-final` | `case.yaml` |
| `signoff-final` | `intake.md` |
| `signoff-final` | `reference/records.csv` |
| `signoff-final` | `candidate/records.csv` |

- 블록과 생성 파일이 같고, SHA-256 줄이 파일과 같다 [S10 blocks].
- T-009(승인됨): 파일 표 끝에 `signoff-final` 4행, 새 7.5. 블록은 prototyper 초안이다. `case.yaml`·`intake.md`는 prototyper가 썼고, CSV 블록은 main Core 생성기가 4.3·4.4 표와 이 `case.yaml`로 낸 바이트다. T-005 Python 독립 구현(4.5의 범위 뒤집기를 더함, `spikes/T-009/xcheck/generate.py`)도 같은 바이트를 냈다 [T9:S2 xcheck]. juhan이 확정한 뒤 test-engineer가 블록을 옮긴다.
- T-008: `case.yaml` 블록 셋과 SHA-256 줄만 바꿨다. 스파이크 `spikes/T-008/add-signoff.py`가 `examples/*/case.yaml`의 `    answers: {…}` 줄 끝에 키를 더하고, `fill-blocks.py`가 블록과 SHA-256 줄을 채웠다. 바꾼 `case.yaml`을 분류하면 `expected/classification.json`과 바이트가 같고, 대조하면 `expected/metrics.json`과 바이트가 같다 [T8:S3]. CSV·`intake.md` 블록은 그대로다.

### 7.2 table-compare

`examples/table-compare/case.yaml` (SHA-256 `451a22acd60c6b650aaf86a424acc72fe1884838592b9ca6819dd2491e764ce2`):

```yaml
# 공개 합성 예제. 실제 업무·값 없음. 지시문 12절 table-compare.
# 단계·답은 answers.yaml과 같다. reference/와 candidate/는 합성 생성기로 만들었다.
name: table-compare
task: 센서 로그에서 구간을 골라 평균을 내고 결과표를 채운다
steps:
  - name: 정상 신호 구간 선택
    answers: {input_kind: table, rule_expressible: yes, frequency: weekly, batch_size: 30, exception_rate: 5, current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 불규칙 신호 구간 선택
    answers: {input_kind: table, rule_expressible: partial, frequency: weekly, batch_size: 30, exception_rate: 40, current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 구간 평균 계산
    answers: {input_kind: table, rule_expressible: yes, frequency: weekly, batch_size: 30, exception_rate: 0, current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 결과표 채우기
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: weekly, batch_size: 30, exception_rate: 0, current_actor: human, failure_impact: external, existing_feature: no, human_signoff: no}
inputs:
  reference: reference/*.csv
  candidate: candidate/*.csv
  key: log_id
compare:
  - {field: start_index, tolerance: 0}
  - {field: length, tolerance: 0}
  - {field: mean_value, tolerance_pct: 1.0}
# 시간 측정 자료가 없는 예제다. 시간 조건은 끄고 정확도·누락률만 본다.
thresholds:
  accuracy_min: 85
  missing_max: 5
  time_must_drop: false
```

`examples/table-compare/intake.md` (SHA-256 `6e4bbac3fa1272264d976a671fb67fb07a88ce672d506a67668b8aabfe88e041`):

```text
매주 센서 로그 파일을 30개쯤 받는다.
로그마다 그래프를 보고 신호가 안정된 구간을 하나 고른다. 구간의 시작 위치와 길이를 적고, 그 구간의 평균값을 낸다.

신호가 불규칙한 로그는 구간을 고르기 어렵다. 오래 걸리고, 사람마다 고르는 곳이 다르다.

고른 구간과 평균값은 정해진 결과표에 채워서 다른 팀에 보낸다.
같은 로그를 AI에게 주고 구간과 평균을 받아 본 결과가 있다. 사람이 만든 결과표와 맞는지 비교하고 싶다.
```

`examples/table-compare/reference/logs.csv` (SHA-256 `dd050a079d26bb800dc822579c335bc743f6935ea03bae8bc30640e3ba75bc9c`):

```csv
log_id,signal_type,start_index,length,mean_value
log01,irregular,5508,360,58.25
log02,normal,3374,480,42.12
log03,normal,3931,332,76.52
log04,normal,1756,358,62.69
log05,normal,525,449,45.01
log06,irregular,1208,207,46.16
log07,irregular,2332,75,30.77
log08,irregular,6117,274,12.65
log09,normal,8838,299,49.41
log10,normal,6132,97,65.65
log11,irregular,3695,51,41.31
log12,normal,2406,458,80.85
log13,normal,2271,148,76.60
log14,normal,3530,183,11.77
log15,normal,2135,283,44.86
log16,normal,6672,434,65.25
log17,normal,8412,355,87.84
log18,normal,8319,473,90.43
log19,irregular,2330,381,92.60
log20,irregular,4195,479,84.15
log21,irregular,2491,57,77.79
log22,irregular,6539,131,75.42
log23,normal,7192,292,63.13
log24,normal,3006,469,80.08
log25,irregular,7529,256,35.56
log26,normal,4812,459,55.76
log27,irregular,5171,318,21.61
log28,irregular,5429,398,95.21
log29,normal,3215,256,26.27
log30,normal,2504,128,93.11
```

`examples/table-compare/candidate/logs.csv` (SHA-256 `c3a923f7bde1409cc39cad9339ec5e55a2438587ca06e7a9355bbe13e6f10736`):

```csv
log_id,signal_type,start_index,length,mean_value
log20,irregular,4195,479,84.07
log15,normal,2135,283,44.86
log17,normal,8412,355,87.84
log02,normal,3374,480,42.12
log19,irregular,2330,381,92.60
log30,normal,2504,128,93.11
log07,irregular,2332,75,31.69
log14,normal,3530,183,11.77
log16,normal,6672,434,65.25
log28,irregular,5429,398,95.21
log12,normal,2406,458,80.85
log09,normal,8838,299,49.41
log25,irregular,7529,256,35.56
log29,normal,3215,256,26.27
log24,normal,3006,469,80.08
log22,irregular,6539,131,75.42
log13,normal,2271,148,76.60
log06,irregular,1208,207,46.16
log18,normal,8319,473,90.43
log04,normal,1756,358,62.82
log01,irregular,5508,360,58.25
log03,normal,3931,332,76.52
log21,irregular,2491,57,77.79
log05,normal,525,449,45.01
log31,irregular,1148,455,54.06
log08,irregular,6117,268,12.65
log11,irregular,3695,51,41.31
log26,normal,4812,459,55.54
log10,normal,6132,97,65.65
log23,normal,7192,292,62.94
```

### 7.3 form-fill

`examples/form-fill/case.yaml` (SHA-256 `9660f3cb0f9ce1e5faea096c50fc5be9a3af64a0614ecd91949ed9fe27dde381`):

```yaml
# 공개 합성 예제. 실제 업무·값 없음. 지시문 12절 form-fill.
# 단계·답은 answers.yaml과 같다. reference/와 candidate/는 합성 생성기로 만들었다.
# 1단계는 existing_feature를 적지 않았다(모름 → 기본값 unknown → 확인 항목).
name: form-fill
task: 고정 양식 주문서를 받아 품목·수량을 집계하고 주간 보고서를 만든다
steps:
  - name: 주문서 파일 모으기
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: weekly, batch_size: 20, exception_rate: 0, current_actor: human, failure_impact: internal_rework, human_signoff: no}
  - name: 주문서 양식 읽기
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: weekly, batch_size: 20, exception_rate: 5, current_actor: ai_each_time, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 품목·수량 집계
    answers: {input_kind: table, rule_expressible: yes, frequency: weekly, batch_size: 20, exception_rate: 0, current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 주간 보고서 만들기
    answers: {input_kind: table, rule_expressible: yes, frequency: weekly, batch_size: 1, exception_rate: 0, current_actor: human, failure_impact: external, existing_feature: no, human_signoff: no}
inputs:
  reference: reference/*.csv
  candidate: candidate/*.csv
  key: order_id
compare:
  - {field: item}
  - {field: quantity, tolerance: 0}
# 시간 측정 자료가 없는 예제다. 시간 조건은 끄고 정확도·누락률만 본다.
thresholds:
  accuracy_min: 85
  missing_max: 5
  time_must_drop: false
```

`examples/form-fill/intake.md` (SHA-256 `d17d834b2051afee4650d49addefc51bacb66fbe37c67249816de45cf71b2def`):

```text
거래처에서 주문서가 매주 20장쯤 온다. 주문서는 모두 같은 양식이다.
주문서 파일을 한곳에 모은다. 지금 쓰는 프로그램에 이 기능이 있는지는 확인해 보지 않았다.

주문서를 한 장씩 AI에게 주고 품목과 수량을 뽑게 한다. 매번 AI를 쓴다.
뽑은 품목·수량을 품목별로 더해서 주간 보고서를 만들고 보낸다.

AI가 뽑은 값이 사람이 읽은 값과 같은지 확인하고 싶다.
```

`examples/form-fill/reference/orders.csv` (SHA-256 `6e332198c66d243687603ed69414b160f334d56e60e7c82a98284109700d47ab`):

```csv
order_id,item,quantity
ord01,item-e,22
ord02,item-a,49
ord03,item-a,17
ord04,item-a,39
ord05,item-b,48
ord06,item-b,39
ord07,item-c,28
ord08,item-d,26
ord09,item-c,39
ord10,item-b,43
ord11,item-e,34
ord12,item-e,17
ord13,item-e,29
ord14,item-c,13
ord15,item-d,47
ord16,item-e,11
ord17,item-a,2
ord18,item-e,21
ord19,item-b,12
ord20,item-c,16
```

`examples/form-fill/candidate/orders.csv` (SHA-256 `ffff6618b9bb14346e9bd6893547f0df7395770c9848ea09891949e5879e7bdd`):

```csv
order_id,item,quantity
ord12,item-e,17
ord10,item-b,43
ord03,item-a,17
ord07,item-c,28
ord15,item-d,47
ord05,item-b,48
ord18,item-e,21
ord20,item-c,16
ord08,item-d,26
ord09,item-c,39
ord17,item-a,2
ord16,item-e,11
ord06,item-b,39
ord04,item-a,39
ord14,item-c,13
ord13,item-e,29
ord19,item-b,12
ord01,item-e,22
ord02,item-a,49
ord11,item-e,34
```

### 7.4 mixed-exceptions

`examples/mixed-exceptions/case.yaml` (SHA-256 `96f04387180ce242879bc91ae1ad516415538c6a84f411006b41616adcad189c`):

```yaml
# 공개 합성 예제. 실제 업무·값 없음. 지시문 12절 mixed-exceptions.
# 단계·답은 answers.yaml과 같다. 검증 자료가 없다(reference/·candidate/·inputs 없음).
name: mixed-exceptions
task: 자유 형식 메일을 접수해 분류하고 답변 초안을 쓴다
steps:
  - name: 메일 접수 기록
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: daily, batch_size: 15, exception_rate: 5, current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 문의 유형 분류
    answers: {input_kind: free_doc, rule_expressible: partial, frequency: daily, batch_size: 15, exception_rate: 30, current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 답변 초안 쓰기
    answers: {input_kind: free_doc, rule_expressible: partial, frequency: daily, batch_size: 15, exception_rate: 30, current_actor: human, failure_impact: external, existing_feature: no, human_signoff: no}
  - name: 처리 결과 기록
    answers: {input_kind: table, rule_expressible: yes, frequency: daily, batch_size: 15, exception_rate: 0, current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
```

`examples/mixed-exceptions/intake.md` (SHA-256 `6487074ad780a8c4b81113b8d6ec7218de77dc123e5d192d3a36c0897d89aa3f`):

```text
문의가 메일로 하루 15통쯤 온다. 형식이 정해져 있지 않다.
메일이 오면 접수 기록을 남긴다. 그다음 문의 유형을 나누고 답변 초안을 쓴다.

유형이 애매한 메일이 꽤 있다. 답변은 밖으로 나가므로 틀리면 곤란하다.
처리가 끝나면 결과를 표에 기록한다.

아직 AI를 써 보지 않았다. 비교할 결과 자료도 없다.
```

### 7.5 signoff-final

T-009(승인됨, juhan 확정 2026-11-07). 만드는 방법은 7.1 T-009 글머리, 근거와 질문은 `docs/spec/signoff-final.md`.

`examples/signoff-final/case.yaml` (SHA-256 `a12835eae7b913d9b0ea855fa01fb47086fd97411ae50bed48f5d01beadf0263`):

```yaml
# 공개 합성 예제. 실제 업무·값 없음. 지시문 12절 signoff-final(12.1과 같은 구조).
# 단계·답은 answers.yaml과 같다. reference/와 candidate/는 합성 생성기로 만들었다.
# 7단계만 사람이 최종 확인·서명하는 단계다(human_signoff: yes). 나머지 단계는 no.
name: signoff-final
task: 기록 파일에서 기준 구간을 골라 값을 내고, 보고서를 써서 사람이 서명해 제출한다
steps:
  - name: 기록 파일 열기·정리
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: weekly, batch_size: 20, exception_rate: 5, current_actor: program, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 기준 구간 고르기(안정 기록)
    answers: {input_kind: table, rule_expressible: yes, frequency: weekly, batch_size: 14, exception_rate: 10, current_actor: program, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 기준 구간 고르기(흔들린 기록)
    answers: {input_kind: table, rule_expressible: partial, frequency: weekly, batch_size: 6, exception_rate: 50, current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 구간 값 계산·옮기기
    answers: {input_kind: table, rule_expressible: yes, frequency: weekly, batch_size: 20, exception_rate: 0, current_actor: program, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 보고서 양식 채우기
    answers: {input_kind: fixed_form, rule_expressible: yes, frequency: weekly, batch_size: 20, exception_rate: 0, current_actor: program, failure_impact: internal_rework, existing_feature: no, human_signoff: no}
  - name: 결과 해석 문장 쓰기
    answers: {input_kind: free_doc, rule_expressible: no, frequency: weekly, batch_size: 20, exception_rate: 40, current_actor: human, failure_impact: external, existing_feature: no, human_signoff: no}
  - name: 최종 확인·서명·제출
    answers: {input_kind: fixed_form, rule_expressible: no, frequency: weekly, batch_size: 20, exception_rate: 0, current_actor: human, failure_impact: external, existing_feature: no, human_signoff: yes}
inputs:
  reference: reference/*.csv
  candidate: candidate/*.csv
  key: record_id
compare:
  - {field: range_start, tolerance: 0}
  - {field: range_end, tolerance: 0}
  - {field: range_avg, tolerance_pct: 1.0}
# 시간 측정 자료가 없는 예제다. 시간 조건은 끄고 정확도·누락률만 본다.
thresholds:
  accuracy_min: 95
  missing_max: 2
  time_must_drop: false
```

`examples/signoff-final/intake.md` (SHA-256 `2066d6823ed5c93ab6c36af39534d34cdeb42c8469bcb185d6cdc93ce52a654e`):

```text
매주 기록 파일이 20개쯤 들어온다. 파일을 열어 필요 없는 줄을 지우고 정리한다.
파일마다 값이 안정된 기준 구간을 하나 고른다. 구간의 시작과 끝을 적고, 그 구간의 평균값을 낸다.
정리, 안정된 기록의 구간 고르기, 값 계산, 보고서 채우기는 지금 직접 만든 매크로가 한다. 쓰는 프로그램에는 이 기능이 없다.

20개 중 6개쯤은 기록이 흔들린다. 이런 파일은 사람이 직접 구간을 고른다. 그중 절반쯤은 어디를 골라야 할지 애매하다.

보고서에는 결과가 무슨 뜻인지 해석 문장을 쓴다. 해석하는 기준은 글로 정리되어 있지 않다. 보고서는 밖으로 나가므로 틀리면 곤란하다.
마지막에 담당자가 보고서를 확인하고 서명해서 제출한다. 서명은 사람이 책임진다.

같은 기록 파일을 AI에게 주고 구간과 평균값을 받아 본 결과가 있다. 사람이 고른 값과 맞는지 비교하고 싶다.
```

`examples/signoff-final/reference/records.csv` (SHA-256 `95db6437676d6cdad6d63ad0e5ddd52249ab0a57b29e8c6852f1edf329ddc771`):

```csv
record_id,record_kind,range_start,range_end,range_avg
rec01,steady,578,3869,277.3
rec02,steady,35,3297,844.8
rec03,unsteady,1039,4625,819.6
rec04,unsteady,345,3812,426.6
rec05,steady,965,4003,823.2
rec06,steady,1021,5269,392.2
rec07,steady,814,5496,703.3
rec08,steady,116,3953,473.3
rec09,steady,720,5021,493.2
rec10,steady,1200,5618,797.3
rec11,steady,1147,5582,582.6
rec12,unsteady,1342,4966,853.3
rec13,steady,679,4637,846.2
rec14,steady,195,3397,900.1
rec15,steady,1019,3847,372.1
rec16,steady,435,3898,530.6
rec17,steady,990,5376,590.6
rec18,unsteady,1685,3416,496.7
rec19,unsteady,1201,3592,928.6
rec20,unsteady,656,3995,785.9
```

`examples/signoff-final/candidate/records.csv` (SHA-256 `5a0a72b75823bf8ea3897adcfe3132b9efa45012ce8363f3926d3650232737f4`):

```csv
record_id,record_kind,range_start,range_end,range_avg
rec04,unsteady,358,3812,426.6
rec07,steady,814,5496,703.3
rec15,steady,1019,3847,372.1
rec06,steady,1021,5269,391.8
rec20,unsteady,656,3995,762.3
rec19,unsteady,1201,3592,928.6
rec01,steady,578,3869,277.3
rec12,unsteady,1342,4966,853.3
rec21,unsteady,1375,4962,381.2
rec09,steady,720,5021,493.2
rec11,steady,1147,5582,583.8
rec10,steady,1200,5618,797.3
rec03,unsteady,1039,4625,819.6
rec18,unsteady,1685,3400,496.7
rec05,steady,965,4003,823.2
rec13,steady,679,4637,846.2
rec16,steady,435,3898,530.6
rec02,steady,35,3297,844.8
rec08,steady,116,3953,473.3
rec17,steady,990,5376,590.6
```

## 8. 테스트로 옮기는 방법 (test-engineer 참고)

- 폴더 `tests/Scorecard.Tests/Synthetic/`, 네임스페이스 `Scorecard.Tests.Synthetic`. Trait `Spec` = `synthetic-examples`.
- 표 읽기는 기존 `SpecDocument.Table(relativePath, header)`(경로 상수 `docs/spec/synthetic-examples.md` 추가). 블록은 `SpecDocument.CodeBlocks`, SHA-256 줄은 `SpecDocument.Line`. 스파이크 도구가 같은 규칙으로 이 문서를 읽었다 [S3 gen], [S10].

| 테스트 | 확인 | 카드 수용 기준 |
| --- | --- | --- |
| 수열 | 4.2의 공개 API 확인(시드 0 → `883`, `431`, `26`) | 1 |
| 결정론 | 같은 입력 두 번 같은 바이트. 문화권을 de-DE·tr-TR로 바꿔도 같은 바이트(`try/finally`). 시드+1이면 다른 바이트 | 1 |
| 골든 | `examples/<예제>/case.yaml` + 4.3·4.4 표 → `Generate` → `Render`가 커밋된 CSV와 바이트까지 같다. 2종 | 1, 3 |
| 구조 | 4.7 구조 표. `Planted` 셈과, 테스트 안의 단순 대조(키 맞춤, 4.7 용어, 허용 오차) 셈이 표와 같다. 누락·값 불일치 행이 모두 몰림 값(`irregular`) | 3 |
| 계획 표 | 4.3·4.4 머리 행·칸 수. 계획 표의 예제 = CSV가 있는 예제 = 7.1 파일 표의 CSV | 3 |
| 블록 = 파일 | 7.2~7.4 블록, SHA-256 줄, 7.1 파일 표가 `examples/` 파일과 같다 | 3, 4 |
| case.yaml 단계 | `case.yaml`의 `name`·`task`·`steps`가 `answers.yaml`과 같다(파싱 결과). `case.yaml` 분류 = `expected/classification.json` 바이트 | 4 |
| 자료 없는 예제 | mixed-exceptions: `inputs` null, `compare` 빈 목록, `reference/`·`candidate/` 폴더 없음 | 3 |
| CSV 바이트 | BOM·CR 없음, 끝 LF, ASCII만, 따옴표 없음, 머리 행 = 키 + 열 표 | — |
| 검사 | 5절 표의 각 행이 `ArgumentException` | — |
| 패턴 자기 검사 | 4.10 표. 잡는 예 모두 걸림, 잡지 않는 예 안 걸림 | 2 |
| 패턴 검사 | `examples/` 모든 파일(내용 줄, 상대 경로)에 걸리는 것 0건. 걸리면 모두 보인다 | 2 |
| G2·G3 | `GuardTargets`에 `synthetic-case-valid`(4.11) | — |
| 줄바꿈 | 기존 `Example_text_files_have_no_carriage_return`이 새 파일도 본다. 새로 쓰지 않는다 | — |

- 표 이름(머리 행)은 이 문서에 정확히 한 번 나온다. `SpecDocument.Table`이 그렇게 검사한다 [읽음].
- 실패 테스트 먼저: `Scorecard.Core.Synthetic`이 없어 새 테스트가 컴파일되지 않는다. T-003 승인 결정 14·T-004와 같이 이것을 "실패하는 테스트"로 보고, 같은 브랜치에서 core-dev가 구현한다.
- 골든·블록·패턴 테스트는 파일이 있어야 돈다. 파일이 없으면 실패해야 한다(빈 통과 금지).
- `spikes/T-005/tests-variant/ExamplePatternTests.cs`를 출발점으로 써도 된다. 빌드와 `dotnet format`이 통과했다 [S8].

## 9. 미확인 목록

1. GitHub ubuntu·windows 러너에서 새 테스트가 녹색인지. windows 러너의 `core.autocrlf`(T-002 미확인 그대로). `.gitattributes`가 막는 것은 확인했다 [S7].
2. publish 단일 exe에서 생성기 동작. Cli가 쓰게 되면 그 카드에서 본다.
3. SplitMix64 수열이 공개 참조 구현의 값과 같은지(4.2). C#과 Python 두 구현이 같음만 확인했다.
4. Windows에서 git 훅(`sh` 스크립트)이 도는지. 훅은 제안에 없다.
5. git 훅이 스테이지된 내용과 작업 트리가 다를 때 놓치는 경우. 실험하지 않았다.
6. 판정 카드가 "특정 유형에만 몰림"을 어떻게 알아낼지. 지금 스키마에는 유형 열을 가리키는 키가 없다.
7. T-006 대조 정의가 4.7 용어와 같은지. 특히 추가 행, `compare`에 없는 열(`signal_type`), 허용 오차 경계, `tolerance`·`tolerance_pct`가 없는 필드(`item`)의 비교.
8. Windows에서 만든 CSV의 SHA-256이 7절 값과 같은지. 같은 바이트면 같아야 한다.

## 10. 스파이크 단계 목록

`spikes/T-005/run-spike.sh <작업폴더> <로그폴더>`. 커밋된 HEAD를 작업 폴더에 clone하고 `spikes/T-005/`와 작업 트리의 이 문서를 복사해 변형을 넣는다. 저장소의 `src/`, `tests/`, `examples/`는 건드리지 않는다. `summary.txt`는 마지막 실행이다.

| 단계 | 한 일 |
| --- | --- |
| S0 | clone, 스파이크 파일·이 문서 복사 |
| S1 | 기준선 Release `-warnaserror` 빌드, 전체 테스트 |
| S2 | Core에 생성기 후보 4파일 → 빌드 → G1 → `dotnet format --verify-no-changes` → 전체 테스트 |
| S3 | 도구 빌드, Core 참조 목록 비교. 예제 조립(T-004 파일 + 초안 `case.yaml`·`intake.md`) → 4.3·4.4 표로 CSV 생성 → `case.yaml` 확인 → 대조 셈 3종 → 바이트 확인. 결정론(다른 프로세스, 다른 경로, de-DE 프로세스, Python 독립 구현) |
| S4 | 메모리 안 실험 `PROBE`: 수열, 문화권 7종, 반복, 대안 P1~P4, 시드 0~1999, 검사, 패턴, CSV |
| S5 | `GuardTargets`에 `synthetic-case-valid` → G2·G3 → 전체 테스트 |
| S6 | 음성 대조: 소수를 현재 문화권으로 읽게 바꿈 → G1 → G2 |
| S7 | 예제 파일을 clone `examples/`에 넣고 전체 테스트. `core.autocrlf=true` clone에서 바이트(`.gitattributes` 유무) |
| S8 | 패턴 검사: 도구로 예제·clone 전체 → 테스트 변형 빌드·형식 → 테스트 → 값을 심어 테스트 → 되돌려 테스트 → 전체 테스트 |
| S9 | git 훅(`core.hooksPath`): 심은 값 커밋, 정상 커밋, `--no-verify`, 훅 끔. 걸린 시간 |
| S10 | 이 문서 7절 블록·SHA-256 = 생성 파일, 4.7 구조 표 = 대조 셈 |

- 기대한 실패(`rc=1`): S6 `g2`(음성 대조), S8 `scan-repo`(저장소 전체에 맞는 것이 있다), S8 `pattern-test-planted`, S9 `hook-planted`(커밋이 막힌다).

---

## 사람 승인 필요

질문마다 prototyper의 제안을 적었다. "제안대로"로 답할 수 있다. 질문 13은 예제 파일 확정이라 사람이 직접 봐야 한다. 질문 9는 원칙(지시문 2절 10) 문구와 다르다.

1. **생성기 위치와 명령.** Core는 `System.Random`을 쓸 수 없다(G1 D1).
   - 제안: `src/Scorecard.Core/Synthetic/`에 둔다(4.1 안 A). 자체 수열로 시드가 결과를 정한다. T-005에는 Cli 명령이 없다. 테스트가 생성해 커밋 파일과 비교한다. 예제 파일을 바꿀 때는 7.1 절차(스파이크 도구 → 블록 → 승인 → 옮김)를 따른다. Cli 명령은 M2나 `samples/` 카드에서 정한다.
2. **수열과 시드.**
   - 제안: SplitMix64와 `NextBelow`(곱셈 상위 64비트, 다시 뽑기 없음)(4.2). 시드는 4.3 계획 표 칸에 둔다. `case.yaml`·Core 상수에 넣지 않는다. 시드는 조건을 만족하는 가장 작은 값이다(table-compare 8, form-fill 0).
3. **계획 표·열 표.**
   - 제안: 4.3·4.4 표 그대로. 테스트가 표를 읽어 계획을 만든다. 표를 바꾸면 7절 블록과 파일을 같이 바꾼다.
4. **뽑는 순서와 상수 고정.**
   - 제안: 4.5의 1~7 순서와 상수(정수 폭 20, 소수 배수 2~4, 허용 오차 안 0.1~0.4배)를 고정한다. 바꾸면 바이트가 바뀌고 골든 테스트가 잡는다. 바꿀 때는 블록과 파일을 같이 바꾸고 PR 본문에 이유를 쓴다.
5. **대조 용어와 12절 "불일치 3건·누락 1건".**
   - 제안: 4.7의 P2. 항목 = 기준 행. "불일치 3건" = 일치가 아닌 기준 행 3건(값 불일치 2 + 누락 1). 결과 30건을 맞추려고 추가 1건을 둔다. 정확도 = 일치 / 기준 행 = 90.0%, 누락률 = 누락 / 기준 행 = 3.3%. 다른 읽기(P1·P3·P4, 칸 단위 94.4%, 양쪽에 있는 행 기준 93.1%)는 12절 숫자와 맞지 않는다. T-006이 이 정의를 확정한다. 다르면 그 카드에서 자료와 정의 중 무엇을 바꿀지 정한다.
6. **심는 방식: 몰림, 허용오차 안 차이, 섞기, 경계값 없음.**
   - 제안: table-compare 불일치 3건을 모두 `signal_type = irregular` 행에 심어 "특정 유형에만 몰림"(조건부)의 근거로 쓴다. 허용오차 안 차이 4행(일치로 센다)과 결과 행 섞기를 둔다. 경계값(허용 오차와 꼭 같은 차이)은 심지 않는다. 경계는 T-006 단위 테스트가 본다. 몰림을 알아내는 규칙(유형 열, 비율)은 판정 카드가 정한다.
7. **CSV 형식.**
   - 제안: 3.3 그대로. ASCII, BOM 없음, LF(RFC 4180의 CRLF가 아님), 끝 LF, 머리 행, 따옴표 없음, 소수 고정 자릿수.
8. **case.yaml 내용.**
   - 제안: 4.8 그대로. 단계·답은 `answers.yaml`과 같다(앞 주석만 다름). table-compare·form-fill에 `inputs`·`compare`·`thresholds`(`accuracy_min: 85`, `missing_max: 5`, `time_must_drop: false`)를 둔다. 시간 자료가 없어 시간 조건을 끄고, 주석으로 이유를 적는다. `before`·`after`·`ai_usage`는 넣지 않는다. mixed-exceptions에는 `inputs`·`compare`·`thresholds`가 없다.
9. **pre-commit 검사를 테스트로 대신하는가 (원칙 2절 10 문구와 다름).**
   - 제안: xUnit 테스트로 한다(4.10 안 T). CI(ubuntu·windows), Claude Stop 훅, 로컬에서 돌고 `--no-verify`로 건너뛸 수 없다. git 훅은 이 카드에서 두지 않는다. 필요하면 테스트만 부르는 훅을 별도 카드로 더한다(훅 폴더는 에이전트 폴더 제한에 없다). 원칙 문구를 "pre-commit 또는 테스트에서"로 고칠지 사람이 정한다.
10. **패턴 표, 범위, 오탐.**
    - 제안: 4.10 패턴 9개 그대로. 범위는 `examples/`만(파일 내용과 상대 경로). 오탐은 합성 값을 바꿔 피한다. 예외 목록을 두지 않는다. 패턴이 틀렸으면 표를 고치고 PR 본문에 이유를 쓴다. 회사 품번 모양은 사람이 일반화해서 더한다.
11. **G2·G3 등록.**
    - 제안: `synthetic-case-valid`(샘플 `case-valid`, 4.11 고정 계획, 출력 `reference/rows.csv`·`candidate/rows.csv`)를 `GuardTargets`에 더한다. 기존 테스트는 바뀌지 않는다.
12. **examples/ 폴더.**
    - 제안: `.gitkeep` 3개를 지운다. mixed-exceptions에는 `reference/`·`candidate/`를 두지 않는다. `expected/`에 새 파일을 넣지 않는다. T-004 파일은 그대로 둔다. 블록을 파일로 옮기는 것은 test-engineer가 한다. CLAUDE.md 에이전트 폴더 제한에 `examples/`가 없어 이 카드에 한해 허용이 필요하다(T-004와 같다).
13. **예제 파일 확정 (사람이 직접 확인).** 7.2~7.4 블록 10개. 확인할 점:
    - table-compare `case.yaml`: `compare` 3필드, `thresholds` 85·5·false. CSV 기준 30행·결과 30행, 심은 행은 6.1.
    - form-fill `case.yaml`: 키 `order_id`, `compare` `item`·`quantity`. CSV 20행·20행, 모두 일치.
    - mixed-exceptions `case.yaml`: 자료 키 없음.
    - `intake.md` 3개: 실제 업무처럼 읽히는지, 업종·회사가 드러나지 않는지, `answers.yaml` 답과 맞는지.
    - 제안: 초안대로 확정한다. 고칠 곳을 적어 주면 prototyper가 스파이크를 다시 돌려 블록·SHA-256을 맞춘다.
14. **Core 공개 API와 진행 순서.**
    - 제안: 4.13 그대로(`SyntheticGenerator.Generate`, `SyntheticCsv.Render`·`CheckCell`, 레코드 4개, `SplitMix64`는 `internal`). test-engineer 단계의 컴파일 실패를 "실패하는 테스트"로 보고, 같은 브랜치에서 core-dev가 구현한다(T-003 승인 결정 14, T-004와 같다). PR 본문에 적는다.

## 승인 결과

- 2026-11-07. juhan의 상시 승인("끝날때까지 추천값으로 머지하고 진행해 계속")에 따라 1~8, 10~14는 제안대로.
- 9: 제안대로 xUnit 테스트로 검사한다. 다만 원칙(지시문 2절 10) 문구를 바꾸는 일이라, git 훅을 더할지와 원칙 문구는 juhan에게 따로 묻는다. 답이 오기 전에는 원칙 문구를 고치지 않는다.
- 12: 이 카드에 한해 test-engineer가 `examples/`에 7절 블록을 바이트 그대로 옮기고 `.gitkeep` 3개를 지운다.
- 13: 7.2~7.4 블록을 정답으로 확정한다.
- 5: 대조 정의(P2)는 T-006이 확정한다.
- 리뷰 1회차(2026-11-07): 4.5 4·5에 "소수도 열 범위를 벗어나면 부호를 뒤집는다"를 더했다(리드). 범위 안 값은 바뀌지 않아 7절 블록 바이트는 그대로다.
- 승인 결정 9 확정(2026-11-07, juhan 카드 선택): 테스트로만 검사한다. 지시문 2절 10을 "pre-commit 또는 테스트에서"로 고쳤다.

## T-008 개정 (승인됨, 2026-11-07)

- 작업 카드 `docs/tasks/T-008-human-signoff.md`. 결정 출발점: ADR 0003 "결정"(예제 3종은 모든 단계 `human_signoff: no`, expected 바이트 그대로) [읽음].
- 바꾼 곳: 머리 상태 글머리, 4.8 글머리, 7.1 글머리, 7.2~7.4 `case.yaml` 블록과 SHA-256 줄(table-compare `451a22ac…`, form-fill `9660f3cb…`, mixed-exceptions `96f04387…`).
- 바꾸지 않은 곳: 생성기 규칙(4.1~4.7), CSV 블록·SHA-256, `intake.md` 블록, 4.10 패턴 표.
- 결과와 질문은 `docs/spec/human-signoff.md`. `examples/*/case.yaml`을 블록대로 바꾸는 것은 승인됐다(`human-signoff.md` 승인 결과 juhan 3).

## T-009 개정 (승인됨, 2026-11-07)

- 작업 카드 `docs/tasks/T-009-signoff-final.md`. 출발점: ADR 0003 남은 질문 2 [읽음].
- 확인 대상 커밋 `ce6c4f0`. 스파이크 `spikes/T-009/`(머지 대상 아님). 근거·질문·결과는 `docs/spec/signoff-final.md`.
- 바꾼 곳: 머리 글머리, 0절 표기, 4.2 글머리, 4.3 계획 표 1행, 4.4 열 표 4행, 4.7 구조 표 1행·글머리, 4.8 표 1행·글머리, 새 6.4, 7.1 파일 표 4행·글머리, 새 7.5.
- 바꾸지 않은 곳: 생성기 규칙(4.1·4.2 수열, 4.5 뽑는 순서, 4.6), 4.10 패턴 표, 7.2~7.4 블록·SHA-256 줄, Core.
