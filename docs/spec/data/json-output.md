# json-output 데이터

`docs/spec/json-output.md`의 표·블록이다. 테스트가 읽는다. 규칙 글은 본문에 있다. 절 번호가 같다.

## 2. 명령

`도움말`이 `예`면 도움말에 보인다. `옵션`은 그 명령이 받는 옵션 전부다. `쓰는 파일`은 출력 폴더 안 이름이다.

| 명령 | 도움말 | 옵션 | 표준 출력(`--json`) | 쓰는 파일 |
| --- | --- | --- | --- | --- |
| `start` | 예 | `--answers`, `--out`, `--json` | 결과 객체 | `classification.json` |
| `open` | 예 | `--review`, `--out`, `--json` | 스코어카드 결과 객체(`scorecard.json` 바이트) | `scorecard.json`, `scorecard.html` |
| `check` | 예 | `--out`, `--json` | 결과 객체 | `metrics.json`, `judgment.json` |
| `build` | 예 | `--json` | 오류 JSON(`unavailable`) | — |
| `classify` | 아니오 | `--answers`, `--json` | `classification.json` 바이트 | — |
| `compare` | 아니오 | `--json` | `metrics.json` 바이트 | — |
| `judge` | 아니오 | `--json` | `judgment.json` 바이트 | — |

- `--help`·`-h`는 모든 명령에서 받는다(표에 적지 않는다).

### 2.1 도움말

`scorecard --help`의 표준 출력 바이트(UTF-8, LF):

```text
scorecard <명령> <케이스 폴더> [옵션]

명령:
  start <케이스> --answers <파일>  답 파일로 단계를 분류한다
  open <케이스>                    저장된 결과로 스코어카드 HTML을 쓴다
  check <케이스>                   기준·결과 자료를 대조하고 판정한다
  build <케이스>                   제작 의뢰서 번들을 만든다(M2-4)

옵션:
  --answers <파일>  답 파일(answers.yaml). start에 필요하다
  --review <파일>   확인 파일(review.yaml). open에서 받는다
  --out <폴더>      결과를 쓸 폴더. 기본값은 <케이스>/out
  --json            결과와 오류를 JSON 문서 하나로 낸다
  --help            이 도움말

묻지 않는다. 파일 입력과 옵션만 받는다.
종료 코드: 0 성공, 2 입력 오류, 1 내부 오류.
```

## 5. 표준 출력과 결과 객체

### 5.2 결과 객체

`check <케이스> --json` 표준 출력(예제 `table-compare`):

```json
{
  "schema_version": 1,
  "command": "check",
  "files": [
    "metrics.json",
    "judgment.json"
  ]
}
```

`help --json` 표준 출력:

```json
{
  "schema_version": 1,
  "command": "help",
  "commands": [
    "start",
    "open",
    "check",
    "build"
  ]
}
```

## 6. 오류 JSON과 종료 코드

### 6.1 오류 JSON

`build <케이스> --json` 표준 출력(종료 코드 2):

```json
{
  "schema_version": 1,
  "exit_code": 2,
  "errors": [
    {
      "code": "unavailable",
      "source": "",
      "line": 0,
      "column": 0,
      "path": "",
      "message": "build는 M2-4에서 생긴다"
    }
  ]
}
```

### 6.2 Cli 오류 코드

| 코드 | 언제 | 종료 코드 |
| --- | --- | --- |
| `argument` | 명령 없음·모름, 케이스 폴더 인자 없음, 남는 인자, 이 명령에 없는 옵션, 옵션 값 없음, 같은 옵션 두 번, 필수 옵션 없음 | 2 |
| `file` | 케이스 폴더 없음(`start`·`open`·`classify --answers`), 답 파일 없음·읽기 실패, `open`의 classification.json 없음·확인 파일 없음 | 2 |
| `output` | 출력 폴더가 케이스 폴더 안(기본값 밖), 출력 폴더 자리에 파일, 쓰기 실패 | 2 |
| `unavailable` | 아직 없는 기능(`build`, M2-4) | 2 |
| `internal` | 예상하지 못한 예외. 메시지는 예외 타입 이름 | 1 |

### 6.3 인자·입력 오류 표

`<case>`는 예제 `table-compare`를 임시 폴더에 복사한 케이스 폴더, `<missing>`은 없는 폴더, `<file>`은 케이스 폴더 밖의 파일이다. `(없음)`은 인자가 없다. `—`는 빈 문자열이다. 줄·열은 모두 0이다. 오류는 하나다.

| 인자 | 종료 코드 | 코드 | 출처 | 경로 | 메시지 |
| --- | --- | --- | --- | --- | --- |
| (없음) | 2 | `argument` | — | `command` | `명령이 없다` |
| `frobnicate <case>` | 2 | `argument` | — | `command` | `모르는 명령이다: frobnicate` |
| `check` | 2 | `argument` | — | `case` | `케이스 폴더를 적지 않았다` |
| `check <case> extra` | 2 | `argument` | — | — | `남는 인자다: extra` |
| `check <case> --verbose` | 2 | `argument` | — | `--verbose` | `이 명령에 없는 옵션이다` |
| `check <case> --answers <case>/answers.yaml` | 2 | `argument` | — | `--answers` | `이 명령에 없는 옵션이다` |
| `compare <case> --out <file>` | 2 | `argument` | — | `--out` | `이 명령에 없는 옵션이다` |
| `start <case> --answers` | 2 | `argument` | — | `--answers` | `옵션 값이 없다` |
| `start <case> --answers --json` | 2 | `argument` | — | `--answers` | `옵션 값이 없다` |
| `check <case> --json --json` | 2 | `argument` | — | `--json` | `같은 옵션이 두 번 나온다` |
| `start <case>` | 2 | `argument` | — | `--answers` | `필수 옵션이 없다` |
| `start <missing> --answers <case>/answers.yaml` | 2 | `file` | — | `case` | `케이스 폴더가 없다` |
| `start <case> --answers <case>/nope.yaml` | 2 | `file` | `nope.yaml` | — | `파일이 없다` |
| `classify <case> --answers <case>/nope.yaml` | 2 | `file` | `nope.yaml` | — | `파일이 없다` |
| `open <missing>` | 2 | `file` | — | `case` | `케이스 폴더가 없다` |
| `open <case>` | 2 | `file` | `classification.json` | — | `파일이 없다. start를 먼저 돌린다` |
| `check <case> --review <file>` | 2 | `argument` | — | `--review` | `이 명령에 없는 옵션이다` |
| `check <missing>` | 2 | `file` | `case.yaml` | — | `파일이 없다` |
| `judge <missing>` | 2 | `file` | `case.yaml` | — | `파일이 없다` |
| `check <case> --out <case>` | 2 | `output` | — | `--out` | `출력 폴더는 케이스 폴더 밖이나 기본 out 폴더여야 한다` |
| `check <case> --out <case>/expected` | 2 | `output` | — | `--out` | `출력 폴더는 케이스 폴더 밖이나 기본 out 폴더여야 한다` |
| `start <case> --answers <case>/answers.yaml --out <file>` | 2 | `output` | — | `--out` | `출력 폴더 자리에 파일이 있다` |
| `build <case>` | 2 | `unavailable` | — | — | `build는 M2-4에서 생긴다` |

## 7. G2·G3 대상

| 이름 | 샘플(`TestData/guard/…`) | 어댑터 |
| --- | --- | --- |
| `cli-start-text-check-valid` | `text-check-valid` | `start <입력> --answers <입력>/answers.yaml --out <출력> --json` → 출력 폴더 파일 + `stdout.json` |
| `cli-check-case-valid` | `case-valid` | `check <입력> --out <출력> --json` → 출력 폴더 파일 + `stdout.json` |
| `cli-open-case-valid` | `case-valid` | `start <입력> --answers <입력>/case.yaml --out <출력>`, `check <입력> --out <출력>`, `open <입력> --out <출력> --json` → 출력 폴더 파일 + `stdout.json` |
| `cli-check-case-invalid` | `case-invalid` | `check <입력> --out <출력> --json` → 오류 JSON을 `stdout.json` 한 파일로 |
