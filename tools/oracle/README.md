# oracle: 스펙만 보고 짠 독립 구현

- 근거: ADR 0005 원문 3·결정 9, T-010 수용 기준 3, T-011 수용 기준 7(판정).
- 엔진(`src/`)과 따로 짠 Python 구현이다. `docs/spec/`과 지시문만 보고 짰다. `src/`는 열지 않았다.
- CI `oracle` 잡이 예제마다 `expected/` 바이트를 다시 만들어 비교한다(base `check.py`). `regression-guard`는 기존 expected를 바꾼 커밋을 이 결과로 허용한다.
- `tools/oracle`이 바뀐 PR에서는 `oracle-head` 잡과 머지 전 `tools/ci/lead_check.py`가 head의 `check.py`와 `selftest.py`를 돌린다.
- Python 3.10 이상, 표준 라이브러리만. 네트워크·외부 프로세스 없음(`selftest.py`만 `check.py`를 자식 프로세스로 돌린다).

## 고칠 수 있는 사람

- prototyper만 고친다(ADR 0005 결정 9). 다른 에이전트는 이 폴더를 고치지 않는다.
- 고칠 때도 `src/`를 열지 않는다(grep·cat 포함). 스펙과 지시문만 본다.
- 고친 뒤 `check.py`와 `selftest.py`를 돌린다.

## 실행

| 명령 | 하는 일 |
| --- | --- |
| `python3 tools/oracle/check.py [--repo <루트>] [--json <보고서 경로>]` | CI 계약. 예제 전부 비교 |
| `python3 tools/oracle/classify.py <answers.yaml 또는 case.yaml>` | 표준 출력에 classification.json 바이트(디버깅용) |
| `python3 tools/oracle/compare.py <케이스 폴더>` | 표준 출력에 metrics.json 바이트(디버깅용) |
| `python3 tools/oracle/judge.py <케이스 폴더>` | 표준 출력에 judgment.json 바이트(시간·사람 입력 없음, 디버깅용) |
| `python3 tools/oracle/selftest.py [--repo <루트>]` | 자기 점검: 스펙 표 대조, 음성 대조, 결정론 |

### selftest.py (CI에서 부를 때)

- T-011 수용 기준 7(ci-dev, `2eb99a0`): `tools/oracle`이 바뀐 PR에서 CI `oracle-head` 잡이 head `check.py` 다음에 head `selftest.py`를 돌린다. 머지 전 `tools/ci/lead_check.py`도 같은 단계를 돈다(`run_head_oracle`). 하나라도 실패하면 그 잡·단계가 실패한다.
- base `selftest.py`는 어디서도 돌리지 않는다. `oracle` 잡은 base `check.py`만 돈다(`tools/ci/README.md` "결정한 해석" 10).
- 부르는 모양: `python3 -I -B tools/oracle/selftest.py --repo <checkout 루트>`(`oracle-head`는 `--repo .`, lead_check는 head 트리). `--repo`를 빼면 이 파일의 두 단계 위를 저장소로 본다.
- 읽는 것: `docs/spec/classification.md`·`metrics.md`·`judgment.md`, `examples/`, `tests/Scorecard.Tests/TestData/`. 읽기만 한다.
- 쓰는 것: 임시 폴더(`tempfile`)뿐이다. `check.py`를 자식 프로세스(`sys.executable`)로 돌린다. 네트워크 없음.
- 종료 코드: 실패가 하나도 없으면 0, 있으면 1. 실패마다 `FAIL <이름>` 줄, 끝에 `selftest: 통과 N, 실패 M`.
- 파일 수를 고정하지 않는다. 음성 대조의 기준선은 examples의 expected 파일 목록(`check.py` 대상 종류)에서 센다. judgment.json 4개가 들어와도(PR (b)) 그대로 돈다.

### check.py 출력 (CI 계약, 바꾸지 않는다)

- `examples/` 아래 폴더마다(이름 서수 순) `expected/classification.json`, `expected/metrics.json`을 본다(늘 보는 종류).
- `expected/judgment.json`은 있을 때만 보는 종류다. 어느 예제에든 하나라도 있으면 모든 예제에서 본다(없는 예제는 `MISSING`). 하나도 없으면 대상이 아니고 보고에 나오지 않는다. 지금은 없다. T-011 PR (b)에서 test-engineer가 judgment.md 7.2~7.5 블록을 넣으면 대상이 된다.
- 한 예제 안의 줄 순서: classification.json, metrics.json, judgment.json.
- 표준 출력 한 줄에 파일 하나:
  - `MATCH <상대 경로> <sha256>`
  - `DIFF <상대 경로> expected=<sha256> oracle=<sha256 또는 error>`
  - `MISSING <상대 경로> oracle=<sha256 또는 error>`: expected 파일이 없다. 건너뛰지 않는다.
- 모두 MATCH면 종료 코드 0, 아니면 1. 비교한 파일이 0개여도 1(빈 통과 금지).
- `oracle=error`: oracle이 그 입력을 범위 밖으로 보고 멈췄다. 이유는 표준 오류에 적는다.
- `--json`: `{"files": [{"expected_sha256", "oracle_sha256", "path", "status"}]}`. `status`는 `match`·`diff`·`missing`. 없는 값은 null. 키 정렬, 들여쓰기 2칸, UTF-8, LF, 끝에 LF.
- 대상은 이 세 종류뿐이다. oracle이 만들지 못하는 종류(`report-outline.json` 등)는 보고에 나오지 않는다. 그런 파일을 바꾸면 oracle이 재현하지 못한 것이다.
- 판정은 judgment.md 8절 골든과 같이 시간·사람 입력 없이(null) 만든다.
- 분류는 `answers.yaml`로 만든다(없으면 `case.yaml`). 둘 다 있으면 `case.yaml`의 `steps`로 낸 바이트도 같아야 MATCH다. 다르면 DIFF이고 expected와 다른 쪽의 sha256을 적는다.
- 표준 출력·오류는 UTF-8, LF로 고정한다. 저장소 파일은 읽기만 하고 `__pycache__`도 쓰지 않는다.

## 무엇을 보고 짰나

| 파일 | 스펙 |
| --- | --- |
| `classify.py` | classification.md 3.2(JSON 모양), 4.1(트리 순서), 4.2(조건 표), 4.3(행 만들기), 4.4(값), 4.5(문장 틀·문구 표), 4.6(읽은 답 표, 기본값 표시), 4.7(확인 항목), 4.8(AI 과잉 후보), 4.12(추정 표시). 지시문 3.3 |
| `compare.py` | metrics.md 2절(미검증이면 파일을 읽지 않음), 3.1(항목 순서, 메모리 결과의 기준 행 전부·머리 행), 3.2(속성 표, `items`에 쓰는 항목), 4.1(패턴, 이름 맞추기, 서수 정렬), 4.3(키), 4.4(항목·셈, 미검증 표), 4.5(숫자 모양, 허용 오차, 넘침), 4.8(퍼센트) |
| `judge.py` | judgment.md 2절(입력), 3.2(속성 표), 3.3(바이트 규칙, 문턱 자릿수), 4.1(해석), 4.2(기본값 표), 4.3(판정 표), 4.4(확인 표), 4.5(문장 값, 숫자 표 `focus_min`), 4.6(기본값 표시, 읽은 문턱 표), 4.7(다음 할 일 표), 4.8(유형 열 규칙 1~6, F1), 4.9(U-B), 4.10(T1), 5절(예외). 대조는 `compare.py`의 메모리 결과(4.11) |
| `oracle_csv.py` | metrics.md 4.2(CSV 읽기), 4.6(정규화) |
| `oracle_case.py` | case-schema.md 4.3(최상위), 4.4(단계, 답 10개 표, 기본값), 4.5(inputs·compare·thresholds. 문턱은 쓴 글 그대로 둔다), 4.6(값 형식), 4.13(inferred) |
| `oracle_yaml.py` | case-schema.md 4.6(null 낱말, yes·no는 글), 4.7(받지 않는 기능), 4.8(인코딩·줄바꿈) |
| `oracle_json.py` | classification.md 3.3(바이트 규칙, 이스케이프 표). metrics.md 3.3이 같은 표를 쓴다 |
| `check.py` | ADR 0005 원문 3, T-010 수용 기준 3, T-011 수용 기준 7·8, classification.md 7.1 5(case.yaml 단계 = answers.yaml) |
| `selftest.py` | classification.md 4.10·6.1·6.2·6.5, metrics.md 3.3(변형)·4.1·4.5·4.6·4.8·4.10, case-schema.md 6.1, judgment.md 3.2·4.2~4.8·4.12·6.1~6.5·7.1~7.5 |

### 규칙 값은 코드에 옮겨 적었다

- 조건 표, 문장 틀·문구 표, 읽은 답 표, 답 10개 표, 퍼센트 셈은 스펙 표를 손으로 옮겨 상수로 두었다. 실행 중에 스펙 문서를 읽지 않는다.
- 판정도 같다: 기본값 표, 판정 표, 확인 표, `focus_min`, 읽은 문턱 표, 다음 할 일 표를 `judge.py` 상수로 옮겼다.
- 이유: 스펙 표와 expected를 같이 바꾸면 oracle이 저절로 따라가지 않게 하려는 것이다. 표가 바뀌면 prototyper가 oracle을 고쳐야 녹색이 된다. expected 변경에 독립된 확인이 한 번 더 든다.
- `selftest.py`만 스펙 문서를 읽는다. 상수와 스펙 예시 표가 어긋나면 실패한다. 판정 상수는 표 자체와도 바로 대조한다(4.2~4.7).

### 출발점과 다시 짠 곳

- 출발점: `befaa0a:spikes/T-006/xcheck/compare.py`, `a65f02e:spikes/T-009/xcheck/{classify,compare}.py`. 둘 다 머리 주석에 "스펙 글만 보고 짰다, C#을 읽지 않았다"가 있다. `src/`를 읽은 흔적은 찾지 못했다.
- 그래도 스펙이 정하지 않은 동작을 넣은 곳은 스펙으로 다시 짰다:
  - JSON 이스케이프: T-009 `classify.py`의 `\n` 같은 짧은 꼴, U+2029 이스케이프는 3.3 표에 없다. 표가 정하지 않은 글자는 멈춘다(아래 "범위 밖").
  - 파일 찾기: 정규식과 코드 포인트 정렬을 4.1의 UTF-16 서수 맞추기·정렬로 바꿨다. 폴더 조각도 이름 목록에서 서수로 찾는다.
  - CSV: 표준 `csv` 모듈과 "레코드 번호 + 1 = 줄" 가정을 버렸다. 4.2대로 직접 읽는다(행이 시작한 물리 줄, 따옴표 안 줄바꿈, CR).
  - YAML: 정규식으로 줄을 뽑던 것을 부분집합 파서와 스키마 검사로 바꿨다.
  - 숫자: 반올림하면 예외를 내는 decimal 문맥(`EXACT`)과 `copy_abs()`를 쓴다. 파이썬 기본 문맥(28자리)이 `abs()`에서 decimal 최댓값을 반올림하는 것을 `selftest.py`가 잡았다.
  - 분류 규칙: 실행 중 스펙 표를 읽던 것을 상수로 옮겼다(위 이유).
- `generate.py`(합성 생성기)는 옮기지 않았다. oracle 대상은 classification.json·metrics.json·judgment.json이다.
- `judge.py`는 새로 짰다. `spikes/T-011/xcheck/judge.py`는 출발점으로 쓰지 않았다(judgment.md 3·4절과 이 폴더의 대조 결과만 봤다). 대조 쪽은 `compare.py`를 그대로 쓰고, 판정이 쓰는 기준 행 전부·기준 파일마다 머리 행을 메모리 결과(`CompareRun`)로 더 내게 했다. metrics.json 바이트는 그대로다.

## 받는 YAML

예제 `case.yaml`·`answers.yaml`과 case-schema 6.1 블록이 쓰는 모양만 받는다.

- 바이트: UTF-8(앞의 BOM 하나는 뗀다). 줄바꿈 LF·CRLF·CR.
- 주석: 줄 전체 `#`, 값 뒤의 공백 + `#`.
- 블록 매핑 `키: 값`. 키는 plain 글.
- 블록 목록 `- 값`. `키:` 아래 같은 열의 목록, `- 키: 값`으로 시작하는 항목 매핑도 받는다.
- 한 줄 흐름 매핑 `{a: 1, b: 2}`, 한 줄 흐름 목록 `[a, b]`.
- 스칼라: plain(한 줄), 큰따옴표(한 줄, YAML 이스케이프), 작은따옴표(한 줄, `''`), 블록 `|`·`|-`·`|+`.
- null: plain의 빈 값, `~`, `null`, `Null`, `NULL`. 따옴표 `""`·`"null"`은 글이다.
- 글은 형을 바꾸지 않는다. `yes`·`no`는 글이다. 형은 스키마(case-schema 4.4~4.6)가 정한다.
- 스키마 검사: 모르는 키, 필수 값, 열거 선택지, 정수·숫자 모양과 범위, 이름 모양, 경로 모양, 중복, `inferred` 규칙을 본다. 어긋나면 멈춘다.
- 받지 않는다(멈춘다): 앵커 `&`, 별칭 `*`, 태그 `!`, 여러 문서 `---`·`...`, 지시 `%`, 접는 블록 `>`, 들여쓰기 숫자, 여러 줄 plain·따옴표, 여러 줄 흐름 모음, 복합 키 `?`, 따옴표 블록 키, 탭 들여쓰기, 중복 키, 제어 글자, U+0085·U+2028·U+2029·U+FEFF.

## 받는 CSV

- UTF-8(앞의 BOM 하나는 뗀다). 행 끝 LF·CRLF·CR. 마지막 줄바꿈은 없어도 된다.
- 구분자 `,`만. RFC 4180 따옴표(안의 `""`, `,`, 줄바꿈). 따옴표로 시작하지 않는 칸 안의 `"`는 글자다.
- 정규화 뒤 모두 빈 행은 건너뛴다. 줄 번호는 센다. 머리 행은 남은 첫 행이다.
- 칸 정규화: 칸 안 CRLF·CR → LF, 앞뒤 공백(`char.IsWhiteSpace` 집합) 떼기.
- 한쪽에 파일 여럿: 파일 경로 서수 → 줄 순서로 합친다. 파일마다 열 순서가 달라도 된다.
- 패턴: 폴더 조각은 글자 그대로, 마지막 조각에만 `*`·`?`(UTF-16 한 단위). 하위 폴더는 보지 않는다.

## 범위 밖 (멈춘다)

- 오류 입력 전부. case-schema 4.9의 오류, metrics.md 4.7의 오류 코드 13개. oracle은 오류 줄·오류 JSON을 만들지 않는다. 엔진의 오류 출력은 oracle이 확인하지 않는다.
- .NET decimal로 반올림해야 하는 숫자 칸(유효 숫자 29자리 넘음, 소수 28자리 넘음, `1e-400` 등)과 decimal 최댓값 바로 위의 값. 스펙 4.5 숫자 모양 표의 두 행(`0.1234567890123456789012345678901`, `1e-400`)이 여기에 든다.
- 이스케이프를 3.3 표가 정하지 않은 글자: LF·CR·BS·FF, U+0020이 아닌 공백(Zs), U+2029, C1 제어, 서식(Cf), 사용자 정의(Co), 미할당(Cn).
- `case.yaml`의 음수 0 숫자(`-0`, `-0.0`). 출력 모양을 스펙이 정하지 않았다.
- 판정 문장에 들어가는 이름·값(유형 열 이름과 값) 가운데 제어 문자가 있거나 64자(UTF-16 단위)를 넘는 것. judgment.md 4.5·metrics.md 4.7이 "제어 문자는 `\uXXXX`, 64자를 넘으면 자르고 `…`"라고만 해서 16진 대소문자, 제어 문자의 범위, 자른 뒤 길이를 정하지 않았다. 64자 이하·제어 문자 없음은 그대로 쓰고, 빈 값은 `(빈 칸)`이다.
- 시간(`Times`): 음수(5절 `ArgumentOutOfRangeException`), 음수 0, decimal에 그대로 담기지 않는 값, 처리 + 검토 합이 decimal 범위를 넘는 것. 넘침의 결과는 `not_met`이지만(4.4) 문장의 `{total}` 모양을 스펙이 정하지 않았다.
- 사람 입력 키가 불일치 항목이 아니거나 중복(5절 `ArgumentException`).
- 미검증인데 시간·사람 입력을 받은 경우. 4.7 `measure_time`의 "미검증이고 `time_must_drop`이 `true`"가 시간을 받았을 때도 해당하는지 스펙이 정하지 않았다.
- 대조 오류가 있는 케이스의 판정(5절: 판정하지 않는다).
- 심볼릭 링크(스펙 미확인).
- 멈추면: `classify.py`·`compare.py`는 종료 코드 2와 이유. `check.py`는 그 파일을 `oracle=error`로 적고 종료 코드 1.

## 해석 (스펙 글이 다 정하지 않아 oracle이 고른 것)

- 그대로 쓰는 글자: 3.3 표의 "그대로" 예(①~④, 한글, NFD 자모, `<>&'+`, 백틱)를 BMP의 L·M·N·P·S 범주 전체로 넓혀 읽었다.
- `\uXXXX`: 표의 U+0001·U+007F·U+2028을 C0 제어(탭·LF·CR·BS·FF 빼고), DEL, U+2028로 읽었다. 대문자 16진이다.
- 서로게이트 쌍: 표의 이모지(U+1F600)를 BMP 밖 글자 전체로 넓혀 읽었다.
- decimal 범위: 최댓값 79228162514264337593543950335, 가수 96비트, 소수 28자리(4.5 숫자 모양 표에서 읽음).
- 판정 이모지: judgment.md 3.3은 "이스케이프는 classification.md 3.3 표와 같다"라고 한 뒤 예로 "이모지는 `😀`"(글자 그대로)라고 적었다. classification.md 3.3 표는 이모지를 `\uD83D\uDE00`(서로게이트 쌍)으로 쓴다. 두 글이 어긋난다. oracle은 표를 따른다(`oracle_json.py` 그대로). 예제 4종에는 이모지가 없어 바이트에 영향이 없다.
- 판정 유형 열 값: 기준 행 `CsvRow`의 칸(정규화 뒤)을 그 파일 머리 행에서 이름으로 찾는다. 유형 열은 모든 기준 파일 머리 행에 꼭 한 번 있는 이름이라 위치가 하나로 정해진다.
- 판정 `{total}`·시간 값 글: decimal을 지수 없이 쓴 글(`format(x, 'f')`). 덧셈은 정확한 셈이라 자릿수는 두 값 중 긴 쪽이다(4.5 `1.25 + 1.2` = `2.45`).

## 남은 한계

- 엔진과 oracle이 같은 스펙을 같은 방식으로 잘못 읽으면 둘 다 틀린 바이트를 낸다(ADR 0005 "남은 위험").
- 오류 경로는 oracle이 보지 않는다. 오류 메시지·순서·위치의 회귀는 xUnit 테스트만 막는다.
- 공개 예제 4종에는 Q1·Q4, 이미지·대화 입력, 여러 CSV 파일, 따옴표 CSV, `inferred`가 없다. `check.py`는 이 경로를 보지 않는다. `selftest.py`의 스펙 표 대조와 TestData 표본이 일부를 본다.
- 판정도 같다. 예제 4종은 모두 `time_must_drop: false`이거나 미검증이고, 시간·사람 입력이 없다. `check.py`는 판정 표 7행 가운데 `NOT_VERIFIED`·`ACCURACY`·`FOCUS`·`PASS`만 본다. `MISSING`·`UNEXPLAINED`·`TIME`, 기본값 표시는 `selftest.py`의 합성 장면(judgment.md 6.1~6.5, 4.6)만 본다.
- Windows에서는 돌려 보지 않았다(미확인).

## 검증 기록 (2026-11-07, 이 환경)

- `check.py`: 예제 4종 8파일 모두 MATCH, 종료 코드 0. judgment.json은 아직 대상이 아니다(파일이 없다).
- `selftest.py`: 통과 848, 실패 0.
  - 판정 블록: 예제 4종의 `judge.py` 출력이 judgment.md 7.2~7.5 블록과 바이트까지 같고, SHA-256도 블록 위 SHA-256 줄과 같다.
  - 음성 대조(check.py): expected 한 바이트, expected 삭제, 결과 CSV 값, answers.yaml 답. 사본에 7.2~7.5 블록을 넣으면 12 MATCH, judgment.json 한 바이트·한 파일 삭제·`case.yaml` 문턱(85 → 95)은 그 파일만 DIFF·MISSING, 모두 지우면 다시 8 MATCH. 되돌리면 모두 MATCH.
  - 음성 대조(판정 규칙): `judge.py`를 일부러 틀리게 바꾸면 실패한다. ACCURACY `≥`를 `>`로 → 실패 5(6.1). 값이 하나뿐인 열을 건너뛰지 않음 → 실패 9(6.3). 시간이 같으면 `met` → 실패 7(6.2·6.5). 판정 순서 TIME·FOCUS를 코드에서 뒤바꿈 → 실패 1(6.5). 되돌리면 통과.
- T-011 PR (b) 모양(사본 examples에 judgment.json 4개를 넣음): `check.py` 12 MATCH, `selftest.py --repo <사본>` 통과 854, 실패 0.
- Python 3.10.20, 3.11.18, 3.12.3, 3.13.18에서 `check.py` 표준 출력 바이트가 같고 `selftest.py`가 통과한다. `python3 -I`로도 돈다.
