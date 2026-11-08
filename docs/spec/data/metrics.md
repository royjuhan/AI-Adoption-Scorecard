# metrics 데이터

`docs/spec/metrics.md`의 표·블록·이름표다. 테스트와 oracle이 읽는다. 규칙 글은 본문에 있다.

## 3. 출력

### 3.2 metrics.json

```json
{
  "schema_version": 1,
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

| 속성 | 형식 | 뜻 |
| --- | --- | --- |
| `schema_version` | 정수 | 파일 모양 버전. 지금 `1`. 늘 첫 키 |
| `case` | 문자열 | `case.yaml`의 `name` |
| `compare` | 객체 | 대조 결과 |
| `compare.status` | 문자열 | `measured`(대조함) 또는 `not_verified`(미검증, 4.4) |
| `compare.reason` | 문자열 또는 null | 미검증 이유 `no_inputs`, `no_compare_fields`. `measured`면 null |
| `compare.key` | 문자열 또는 null | `inputs.key`. 미검증이면 null |
| `compare.reference_files` | 문자열 배열 | 읽은 기준 파일. 경로 서수 순. 미검증이면 `[]` |
| `compare.candidate_files` | 문자열 배열 | 읽은 결과 파일. 같음 |
| `compare.counts` | 객체 또는 null | 항목 셈(4.4). 미검증이면 null |
| `compare.counts.reference` | 정수 | 기준 행 수 = 일치 + 값 불일치 + 누락 |
| `compare.counts.candidate` | 정수 | 결과 행 수 = 일치 + 값 불일치 + 추가 + 중복 행 |
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
| `compare.fields[].compared` | 정수 | 대조한 결과 행 수. 기준에 키가 있는 결과 행(중복 행 포함). 결과 쪽 중복이 없으면 일치 + 값 불일치 |
| `compare.fields[].outside` | 정수 | 허용 오차 밖 칸 수 |
| `compare.fields[].within` | 정수 | 허용 오차 안 차이 칸 수 |
| `compare.fields[].not_number` | 정수 | 결과 칸이 숫자가 아닌 칸 수 |
| `compare.items` | 객체 배열 | 차이가 있는 항목만(아래). 3.1 순서 |
| `compare.items[]` | 객체 | 항목 하나 |
| `compare.items[].key` | 문자열 | 키(정규화 뒤) |
| `compare.items[].status` | 문자열 | `match`, `mismatch`, `missing`, `extra`, `duplicate` |
| `compare.items[].reference` | 객체 또는 null | 기준 행 위치. 추가 항목과 기준에 없는 키의 중복 항목이면 null |
| `compare.items[].reference.file` | 문자열 | 기준 파일 경로 |
| `compare.items[].reference.line` | 정수 | 행이 시작한 줄(1부터, 4.2) |
| `compare.items[].candidate` | 객체 또는 null | 결과 행 위치. 누락 항목이면 null |
| `compare.items[].candidate.file` | 문자열 | 결과 파일 경로 |
| `compare.items[].candidate.line` | 정수 | 같음 |
| `compare.items[].differences` | 객체 배열 | 그 결과 행과 기준 행의 차이가 있는 `compare` 필드. `compare` 순서. 누락·추가와 기준에 없는 키의 중복은 `[]` |
| `compare.items[].differences[]` | 객체 | 칸 차이 하나 |
| `compare.items[].differences[].field` | 문자열 | 필드 이름 |
| `compare.items[].differences[].result` | 문자열 | `within`, `outside`, `not_number`(4.5) |
| `compare.items[].differences[].reference` | 문자열 | 기준 칸(정규화 뒤 텍스트) |
| `compare.items[].differences[].candidate` | 문자열 | 결과 칸(같음) |

## 4. 규칙

### 4.1 파일 찾기 (`inputs` 패턴)

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

### 4.2 CSV 읽기

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

### 4.4 항목과 셈

| 용어 | metrics.json | 뜻 |
| --- | --- | --- |
| 항목 | `items[]` | 기준 행 하나(키 하나). 추가 행·중복 행도 `items`에 쓰지만 항목 셈(분모)에는 들지 않는다 |
| 일치 | `match` | 키가 결과에 있고, `compare` 필드가 모두 같거나 허용 오차 안 |
| 값 불일치 | `mismatch` | 키가 결과에 있고, `compare` 필드 하나 이상이 `outside` 또는 `not_number`. 또는 키가 결과 쪽에 두 번 이상(4.3) |
| 누락 | `missing` | 기준 키가 결과에 없음 |
| 추가 | `extra` | 결과 키가 기준에 없음(그 키의 대표 행) |
| 중복 | `duplicate` | 결과 쪽에 두 번 이상 나온 키의 둘째 이후 행(4.3) |
| 허용오차 안 차이 | `within` | 일치이지만 어떤 필드 값이 기준과 다름(모두 허용 오차 안) |

| 조건 | `status` | `reason` | 파일을 읽나 |
| --- | --- | --- | --- |
| `inputs` 없음 | `not_verified` | `no_inputs` | 아니오 |
| `inputs`는 있고 `compare`가 빔 | `not_verified` | `no_compare_fields` | 아니오 |
| 그 밖 | `measured` | null | 예 |

### 4.5 숫자와 허용 오차

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

- 경계 행(`10`→`12`, `30.77`→`31.0777`, `100`→`101`, `-50`→`-51`, `12345.678`→`12358.023678`)이 정확히 같은 차이다. 바로 다음 값이 `outside`다. 카드 수용 기준 3.

### 4.6 텍스트 정규화

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

### 4.7 오류

| 코드 | 언제 | 위치 | 메시지 |
| --- | --- | --- | --- |
| `pattern` | 패턴 모양이 4.1 밖 | `case.yaml`, 경로 = `inputs.reference`·`inputs.candidate` | `지원하지 않는 패턴이다. 폴더는 글자 그대로, 마지막 조각에만 '*'·'?'를 쓴다` |
| `file` | 폴더 없음 / 맞는 파일 없음 / 읽기 실패 / 케이스 폴더 없음 | `case.yaml`, 경로 = `inputs.reference`·`inputs.candidate`. 파일 읽기 실패는 그 파일 | `폴더가 없다: <폴더>` / `일치하는 파일이 없다` / `파일을 읽을 수 없다: <예외 타입>` / `폴더를 읽을 수 없다: <예외 타입>` / `케이스 폴더가 없다` |
| `overlap` | 같은 파일이 두 번 나옴(기준·결과 패턴이 겹침) | 둘째 파일 | `같은 파일이 두 번 나온다(기준·결과 패턴이 겹친다)` |
| `encoding` | UTF-16 BOM / UTF-8이 아닌 바이트 | 파일:1 / 파일:그 바이트가 있는 줄 | `UTF-16이다. UTF-8로 저장한다` / `UTF-8이 아니다 (바이트 <N>)` |
| `empty` | 빈 파일, BOM만, 빈 행만 | 파일 | `머리 행이 없다` |
| `csv-syntax` | 따옴표가 닫히지 않음 / 닫는 따옴표 뒤에 다른 글자 | 여는 따옴표 / 그 글자 | `따옴표가 닫히지 않았다` / `닫는 따옴표 뒤에 다른 글자가 있다` |
| `field-count` | 데이터 행의 칸 수가 머리 행과 다름 | 파일:줄 | `칸 수가 머리 행과 다르다 (머리 행 <N>칸, 이 행 <M>칸)` |
| `column` | 머리 행에 키 열이나 `compare` 필드 열이 없음(기준 쪽은 `inputs.group` 열도) | 파일:머리 행 줄, 경로 = 열 이름 | `머리 행에 열이 없다` |
| `duplicate-column` | 쓰는 열(키·`compare`, 기준 쪽은 `inputs.group`도)이 머리 행에 둘 이상 | 둘째 칸, 경로 = 열 이름 | `머리 행에 같은 열이 둘 이상 있다 (처음: <N>번째 칸)` |
| `empty-key` | 키 칸이 정규화 뒤 빔 | 키 칸, 경로 = 키 열 | `키가 비어 있다` |
| `duplicate-key` | 기준 쪽에 같은 키(결과 쪽은 항목 `duplicate`, 4.3) | 둘째 키 칸, 경로 = 키 열 | `키가 중복된다: <키> (처음: <파일>:<줄>)` |
| `number` | 기준 자료의 숫자 필드 칸이 숫자가 아님 | 그 칸, 경로 = 필드 | `숫자가 아니다: <값>` |
| `no-rows` | 다른 오류가 없는데 기준 데이터 행이 0 | `case.yaml`, 경로 = `inputs.reference` | `기준 자료에 데이터 행이 없다` |

### 4.8 퍼센트

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

### 4.10 결정론·원본 불변·G1·G2·G3

| 이름 | 샘플(`TestData/guard/…`) | 어댑터 |
| --- | --- | --- |
| `compare-case-valid` | `case-valid` (기존. `reference/rows.csv`·`candidate/rows.csv`를 더한다) | `CaseParser.ParseDirectory` → `CompareEngine.CompareDirectory` → 출력 폴더의 `metrics.json` 한 파일 |
| `compare-invalid` | `compare-invalid` (새) | 같음. 오류면 오류 줄(LF, 끝 LF)을 `compare-errors.txt` 한 파일로 |

- `case-valid`의 두 CSV는 T-005 G2 고정 계획(`GuardTargets.SyntheticGuardPlan`과 같은 값: 시드 7, `rows.csv`, 12행)으로 `SyntheticGenerator`가 만든 바이트다(SHA-256 `reference/rows.csv` `66d83729…`, `candidate/rows.csv` `9be987a9…`, 각 216바이트). 대조하면 심은 구조가 그대로 나온다: 12행, 일치 8(허용오차 안 차이 2), 값 불일치 3(`start`·`mean`·`label` 하나씩), 누락 1, 추가 1, 정확도 66.7, 누락률 8.3. Python 독립 구현도 같은 수치와 같은 바이트(metrics.json SHA-256 `c5906d45…`, 3,803바이트)를 냈다.

### 4.11 Core 공개 API (제안)

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
    public const string ItemDuplicate = "duplicate";
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
    public const int SchemaVersion = 1; // 첫 키 "schema_version"(3.2)

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

## 5. 예외

| 상황 | 결과 | 근거 |
| --- | --- | --- |
| `definition`·`sources`·`inputs`가 null | `ArgumentNullException` | 후보 코드 |
| `caseDirectory`가 null·빈 문자열 | `ArgumentException` | 후보 코드 |
| `inputs` 없음 | 미검증 `no_inputs`. 폴더를 읽지 않는다 | [P:not-verified] |
| `compare`가 빔(`inputs`는 있음) | 미검증 `no_compare_fields`. 폴더를 읽지 않는다 | [P:not-verified] |
| 케이스 폴더가 없음(`inputs` 있음) | `case.yaml: file inputs.reference: 케이스 폴더가 없다`, `inputs.candidate`도 같음(양쪽) | [P:glob read 케이스 폴더 없음] |
| `Compare`에 파일 0개인 쪽 | `case.yaml: file inputs.<쪽>: 일치하는 파일이 없다` | [P:not-verified candidate-no-files, reference-no-files] |
| 결과 자료가 머리 행뿐 | 오류 아님. 모두 누락 | [P:csv candidate-header-only] |
| 기준 자료가 머리 행뿐 | `case.yaml: no-rows inputs.reference: 기준 자료에 데이터 행이 없다` | [P:csv header-only] |
| 파일·폴더 읽기 중 `IOException`·`UnauthorizedAccessException` | `file` `파일을 읽을 수 없다: <타입>` / `폴더를 읽을 수 없다: <타입>` | 후보 코드. 실제로 일으키지는 않았다(**미확인**) |
| Excel이 연 파일(`~$` 잠금 파일, 잠긴 CSV) | `*.csv`에 맞으면 읽는다. 어떤 오류가 나는지 **미확인** | 4.1 |
| `;` 구분 CSV(유럽 문화권 Excel) | `column` 오류로 드러난다 | [P:csv semicolon-delimiter] |
| 심볼릭 링크 | **미확인** | — |
| 큰 자료 | 10만 행 대조·JSON 1초 안. 모든 행에 허용오차 안 차이가 있으면 metrics.json이 약 44.7MB(`items`가 행마다 하나) | [TIMING] |
| 같은 이름을 대소문자만 바꾼 파일 둘(`a.csv`, `A.CSV`) | Linux에서는 서로 다른 파일이고 `*.csv`는 `a.csv`만 맞는다. 대소문자를 무시하는 파일 시스템에서는 둘이 같이 있을 수 없다 | [P:glob read reference/*.csv] |

## 6. 예시

### 6.1 table-compare 차이

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

### 6.4 오류 예시 (`compare-invalid`)

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

```text
reference/a.csv:3:8: number mean: 숫자가 아니다: x1
reference/a.csv:4:1: duplicate-key row_id: 키가 중복된다: r01 (처음: reference/a.csv:2)
reference/a.csv:5:1: empty-key row_id: 키가 비어 있다
reference/a.csv:6: field-count: 칸 수가 머리 행과 다르다 (머리 행 4칸, 이 행 3칸)
reference/b.csv:2:1: duplicate-key row_id: 키가 중복된다: r02 (처음: reference/a.csv:3)
reference/c.csv:2:1: csv-syntax: 따옴표가 닫히지 않았다
candidate/a.csv:1: column label: 머리 행에 열이 없다
```

## 7. 예제 4종 `expected/metrics.json`

### 7.1 요약

| 예제 | 상태 | 기준 행 | 결과 행 | 일치 | 값 불일치 | 누락 | 추가 | 허용오차 안 차이 | 정확도(%) | 누락률(%) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `table-compare` | `measured` | 30 | 30 | 27 | 2 | 1 | 1 | 4 | 90.0 | 3.3 |
| `form-fill` | `measured` | 20 | 20 | 20 | 0 | 0 | 0 | 0 | 100.0 | 0.0 |
| `mixed-exceptions` | `not_verified` | — | — | — | — | — | — | — | — | — |
| `signoff-final` | `measured` | 20 | 20 | 16 | 3 | 1 | 1 | 2 | 80.0 | 5.0 |

### 7.2 table-compare

`examples/table-compare/expected/metrics.json` (SHA-256 `d250fbeeb8bbed24d22f2d8c9d4dadab99ddbd6c794bb1039b1502edc450aa84`):

```json
{
  "schema_version": 1,
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

`examples/form-fill/expected/metrics.json` (SHA-256 `6dd5b7c6868ed30a3b55ac6d9cece82062fd36900ba015b6c1ff98959c7b1afc`):

```json
{
  "schema_version": 1,
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

`examples/mixed-exceptions/expected/metrics.json` (SHA-256 `372e25df94129fd6254866022a0a0b4b9d5ae706410122e6f7d50d8bad8f3f65`):

```json
{
  "schema_version": 1,
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

### 7.5 signoff-final

`examples/signoff-final/expected/metrics.json` (SHA-256 `95546bad43c246a9b0b397fc28e9eb66cb8b3caaaa097b204ff4d055f644cb2b`):

```json
{
  "schema_version": 1,
  "case": "signoff-final",
  "compare": {
    "status": "measured",
    "reason": null,
    "key": "record_id",
    "reference_files": [
      "reference/records.csv"
    ],
    "candidate_files": [
      "candidate/records.csv"
    ],
    "counts": {
      "reference": 20,
      "candidate": 20,
      "match": 16,
      "mismatch": 3,
      "missing": 1,
      "extra": 1,
      "within": 2
    },
    "accuracy_pct": 80.0,
    "missing_rate_pct": 5.0,
    "fields": [
      {
        "field": "range_start",
        "kind": "number",
        "tolerance": 0,
        "tolerance_pct": null,
        "compared": 19,
        "outside": 1,
        "within": 0,
        "not_number": 0
      },
      {
        "field": "range_end",
        "kind": "number",
        "tolerance": 0,
        "tolerance_pct": null,
        "compared": 19,
        "outside": 1,
        "within": 0,
        "not_number": 0
      },
      {
        "field": "range_avg",
        "kind": "number",
        "tolerance": null,
        "tolerance_pct": 1.0,
        "compared": 19,
        "outside": 1,
        "within": 2,
        "not_number": 0
      }
    ],
    "items": [
      {
        "key": "rec04",
        "status": "mismatch",
        "reference": {
          "file": "reference/records.csv",
          "line": 5
        },
        "candidate": {
          "file": "candidate/records.csv",
          "line": 2
        },
        "differences": [
          {
            "field": "range_start",
            "result": "outside",
            "reference": "345",
            "candidate": "358"
          }
        ]
      },
      {
        "key": "rec06",
        "status": "match",
        "reference": {
          "file": "reference/records.csv",
          "line": 7
        },
        "candidate": {
          "file": "candidate/records.csv",
          "line": 5
        },
        "differences": [
          {
            "field": "range_avg",
            "result": "within",
            "reference": "392.2",
            "candidate": "391.8"
          }
        ]
      },
      {
        "key": "rec11",
        "status": "match",
        "reference": {
          "file": "reference/records.csv",
          "line": 12
        },
        "candidate": {
          "file": "candidate/records.csv",
          "line": 12
        },
        "differences": [
          {
            "field": "range_avg",
            "result": "within",
            "reference": "582.6",
            "candidate": "583.8"
          }
        ]
      },
      {
        "key": "rec14",
        "status": "missing",
        "reference": {
          "file": "reference/records.csv",
          "line": 15
        },
        "candidate": null,
        "differences": []
      },
      {
        "key": "rec18",
        "status": "mismatch",
        "reference": {
          "file": "reference/records.csv",
          "line": 19
        },
        "candidate": {
          "file": "candidate/records.csv",
          "line": 15
        },
        "differences": [
          {
            "field": "range_end",
            "result": "outside",
            "reference": "3416",
            "candidate": "3400"
          }
        ]
      },
      {
        "key": "rec20",
        "status": "mismatch",
        "reference": {
          "file": "reference/records.csv",
          "line": 21
        },
        "candidate": {
          "file": "candidate/records.csv",
          "line": 6
        },
        "differences": [
          {
            "field": "range_avg",
            "result": "outside",
            "reference": "785.9",
            "candidate": "762.3"
          }
        ]
      },
      {
        "key": "rec21",
        "status": "extra",
        "reference": null,
        "candidate": {
          "file": "candidate/records.csv",
          "line": 10
        },
        "differences": []
      }
    ]
  }
}
```

## 12. 결과 쪽 키 중복

### 12.5 예시 `compare-duplicate`

`compare-duplicate/case.yaml`:

```yaml
# 합성 예시. 실제 업무·값 없음. 결과 쪽 키 중복과 inputs.group(T-012)을 보인다.
name: compare-duplicate
task: 결과 쪽 키 중복 합성 예시
steps:
  - name: 한 단계
inputs:
  reference: reference/*.csv
  candidate: candidate/*.csv
  key: row_id
  group: kind
compare:
  - {field: value, tolerance: 0}
thresholds:
  accuracy_min: 50
  missing_max: 50
  time_must_drop: false
```

`compare-duplicate/reference/rows.csv`:

```csv
row_id,kind,value
r1,a,10
r2,a,20
r3,b,30
r4,b,40
```

`compare-duplicate/candidate/rows.csv`:

```csv
row_id,value
r1,10
r2,21
r2,20
r5,50
r3,30
r5,55
```

`compare-duplicate` metrics.json (SHA-256 `2a4c4770faeab9ba89e19497d1c070055356d4e553cfc032e0158ea01930b202`):

```json
{
  "schema_version": 1,
  "case": "compare-duplicate",
  "compare": {
    "status": "measured",
    "reason": null,
    "key": "row_id",
    "reference_files": [
      "reference/rows.csv"
    ],
    "candidate_files": [
      "candidate/rows.csv"
    ],
    "counts": {
      "reference": 4,
      "candidate": 6,
      "match": 2,
      "mismatch": 1,
      "missing": 1,
      "extra": 1,
      "within": 0
    },
    "accuracy_pct": 50.0,
    "missing_rate_pct": 25.0,
    "fields": [
      {
        "field": "value",
        "kind": "number",
        "tolerance": 0,
        "tolerance_pct": null,
        "compared": 4,
        "outside": 1,
        "within": 0,
        "not_number": 0
      }
    ],
    "items": [
      {
        "key": "r2",
        "status": "mismatch",
        "reference": {
          "file": "reference/rows.csv",
          "line": 3
        },
        "candidate": {
          "file": "candidate/rows.csv",
          "line": 3
        },
        "differences": [
          {
            "field": "value",
            "result": "outside",
            "reference": "20",
            "candidate": "21"
          }
        ]
      },
      {
        "key": "r4",
        "status": "missing",
        "reference": {
          "file": "reference/rows.csv",
          "line": 5
        },
        "candidate": null,
        "differences": []
      },
      {
        "key": "r2",
        "status": "duplicate",
        "reference": {
          "file": "reference/rows.csv",
          "line": 3
        },
        "candidate": {
          "file": "candidate/rows.csv",
          "line": 4
        },
        "differences": []
      },
      {
        "key": "r5",
        "status": "extra",
        "reference": null,
        "candidate": {
          "file": "candidate/rows.csv",
          "line": 5
        },
        "differences": []
      },
      {
        "key": "r5",
        "status": "duplicate",
        "reference": null,
        "candidate": {
          "file": "candidate/rows.csv",
          "line": 7
        },
        "differences": []
      }
    ]
  }
}
```

## 13. 결과 쪽 중복의 셈

### 13.3 줄 순서 표

| r2 결과 행 | 기준 항목 차이 | 중복 항목 차이 | 일치 | 값 불일치 | compared | outside |
| --- | --- | --- | --- | --- | --- | --- |
| `21`, `20` | `20 → 21` | — | 2 | 1 | 4 | 1 |
| `20`, `21` | — | `20 → 21` | 2 | 1 | 4 | 1 |
| `20`, `20` | — | — | 2 | 1 | 4 | 0 |
| `21`, `22` | `20 → 21` | `20 → 22` | 2 | 1 | 4 | 2 |
