# synthetic-examples 데이터

`docs/spec/synthetic-examples.md`의 표·블록·이름표다. 테스트와 oracle이 읽는다. 규칙 글은 본문에 있다.

## 3. 출력

### 3.1 메모리 결과

| `Planted` | 어디 | 뜻 |
| --- | --- | --- |
| `none` | 기준·결과 | 심지 않음 |
| `missing` | 기준 | 결과에서 뺀 행 |
| `mismatch:<열>` | 결과 | 그 열을 허용 오차 밖으로 바꾼 행 |
| `within:<열>` | 결과 | 그 열을 허용 오차 안에서만 바꾼 행 |
| `extra` | 결과 | 결과에만 있는 행 |

### 3.2 예제 폴더

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

## 4. 규칙

### 4.2 수열과 시드

- 시드 0의 처음 세 값: `e220a8397b1dcdaf`, `6e789e6aa1b965f4`, `06c45d188009454f`. Python 독립 구현도 같다.
- `SplitMix64`는 `internal`이다. Core 공개 API로도 수열이 보인다. 시드 0, 정수 열 하나(`0`~`999`), 3행, 심기·섞기 없음으로 생성하면 기준 값이 `883`, `431`, `26`이다. 처음 세 값 × 1000의 상위 64비트와 같다.

### 4.3 생성 계획 표

| 예제 | 시드 | 파일 | 키 접두어 | 키 자릿수 | 기준 건수 | 값 불일치 열 | 누락 | 추가 | 허용오차 안 차이 | 몰림 | 결과 섞기 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `table-compare` | `8` | `logs.csv` | `log` | 2 | 30 | `length`, `mean_value` | 1 | 1 | `mean_value` 4 | `signal_type` = `irregular` | 예 |
| `form-fill` | `0` | `orders.csv` | `ord` | 2 | 20 | 없음 | 0 | 0 | 없음 | 없음 | 예 |
| `signoff-final` | `47` | `records.csv` | `rec` | 2 | 20 | `range_start`, `range_end`, `range_avg` | 1 | 1 | `range_avg` 2 | 없음 | 예 |

### 4.4 열 표

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

### 4.6 case.yaml과 계획 맞추기

| `compare` 항목 | 열 종류 |
| --- | --- |
| `tolerance`(정수 값) | 정수 |
| `tolerance_pct` | 소수 |
| 둘 다 없음 | 선택 |

### 4.7 대조 용어와 심은 구조

| 용어 | 뜻 |
| --- | --- |
| 항목 | 기준 행 하나(키 하나) |
| 일치 | 키가 결과에 있고, `compare` 필드가 모두 허용 오차 안 |
| 값 불일치 | 키가 결과에 있고, `compare` 필드 하나 이상이 허용 오차 밖 |
| 누락 | 기준 키가 결과에 없음 |
| 추가 | 결과 키가 기준에 없음. 정확도·누락률 분모에 들지 않는다 |
| 허용오차 안 차이 | 일치이지만 값이 기준과 다름 |
| 불일치(12절) | 일치가 아닌 기준 항목. 값 불일치 + 누락 |

| 예제 | 기준 행 | 결과 행 | 일치 | 값 불일치 | 누락 | 추가 | 허용오차 안 차이 | 정확도(%) | 누락률(%) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `table-compare` | 30 | 30 | 27 | 2 | 1 | 1 | 4 | 90.0 | 3.3 |
| `form-fill` | 20 | 20 | 20 | 0 | 0 | 0 | 0 | 100.0 | 0.0 |
| `signoff-final` | 20 | 20 | 16 | 3 | 1 | 1 | 2 | 80.0 | 5.0 |

### 4.8 case.yaml

| 예제 | `inputs` | `compare` | `thresholds` |
| --- | --- | --- | --- |
| `table-compare` | `reference/*.csv`, `candidate/*.csv`, 키 `log_id`, 유형 열 `signal_type` | `start_index` `tolerance: 0`, `length` `tolerance: 0`, `mean_value` `tolerance_pct: 1.0` | `accuracy_min: 85`, `missing_max: 5`, `time_must_drop: false` |
| `form-fill` | `reference/*.csv`, `candidate/*.csv`, 키 `order_id` | `item`(허용 오차 없음), `quantity` `tolerance: 0` | 같음 |
| `mixed-exceptions` | 없음 | 없음 | 없음 |
| `signoff-final` | `reference/*.csv`, `candidate/*.csv`, 키 `record_id`, 유형 열 `record_kind` | `range_start` `tolerance: 0`, `range_end` `tolerance: 0`, `range_avg` `tolerance_pct: 1.0` | `accuracy_min: 95`, `missing_max: 2`, `time_must_drop: false` |

### 4.10 실제 값 패턴 검사

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

### 4.11 G1·G2·G3

| 이름 | 샘플(`TestData/guard/…`) | 어댑터 |
| --- | --- | --- |
| `synthetic-case-valid` | `case-valid` (기존) | `CaseParser.ParseDirectory` → `SyntheticGenerator.Generate`(테스트 안 고정 계획) → `SyntheticCsv.Render` → 출력 폴더의 `reference/rows.csv`, `candidate/rows.csv` |

### 4.13 Core 공개 API (제안)

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

## 6. 예시

### 6.1 table-compare에 심은 행

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

- 품목 5종이 모두 나온다. `item-a` 4, `item-b` 4, `item-c` 4, `item-d` 2, `item-e` 6.

### 6.4 signoff-final에 심은 행 (T-009)

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

## 7. 예제 파일

### 7.1 요약

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

### 7.2 table-compare

`examples/table-compare/case.yaml` (SHA-256 `e99d669ab16b6eada60e31c794efaa8cd5baa23db34b2500a68b731914f8e5b1`):

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
  group: signal_type
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

`examples/signoff-final/case.yaml` (SHA-256 `e11e7b68274fa2688027d671ab240be59cbe9ff874be8ffb5214fb8165dfe348`):

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
  group: record_kind
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
