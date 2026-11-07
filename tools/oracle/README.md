# oracle: 스펙만 보고 짠 독립 구현

- 근거: ADR 0005 원문 3·결정 9, T-010 수용 기준 3.
- 엔진(`src/`)과 따로 짠 Python 구현이다. `docs/spec/`과 지시문만 보고 짰다. `src/`는 열지 않았다.
- CI `oracle` 잡이 예제마다 `expected/` 바이트를 다시 만들어 비교한다. `regression-guard`는 기존 expected를 바꾼 커밋을 이 결과로 허용한다.
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
| `python3 tools/oracle/selftest.py [--repo <루트>]` | 자기 점검: 스펙 표 대조, 음성 대조, 결정론 |

### check.py 출력 (CI 계약, 바꾸지 않는다)

- `examples/` 아래 폴더마다(이름 서수 순) `expected/classification.json`, `expected/metrics.json` 두 파일을 본다.
- 표준 출력 한 줄에 파일 하나:
  - `MATCH <상대 경로> <sha256>`
  - `DIFF <상대 경로> expected=<sha256> oracle=<sha256 또는 error>`
  - `MISSING <상대 경로> oracle=<sha256 또는 error>`: expected 파일이 없다. 건너뛰지 않는다.
- 모두 MATCH면 종료 코드 0, 아니면 1. 비교한 파일이 0개여도 1(빈 통과 금지).
- `oracle=error`: oracle이 그 입력을 범위 밖으로 보고 멈췄다. 이유는 표준 오류에 적는다.
- `--json`: `{"files": [{"expected_sha256", "oracle_sha256", "path", "status"}]}`. `status`는 `match`·`diff`·`missing`. 없는 값은 null. 키 정렬, 들여쓰기 2칸, UTF-8, LF, 끝에 LF.
- 대상은 이 두 종류뿐이다. examples에 expected가 아예 없는 종류(`report-outline.json` 등)는 보고에 나오지 않는다. 그런 파일을 바꾸면 oracle이 재현하지 못한 것이다.
- 분류는 `answers.yaml`로 만든다(없으면 `case.yaml`). 둘 다 있으면 `case.yaml`의 `steps`로 낸 바이트도 같아야 MATCH다. 다르면 DIFF이고 expected와 다른 쪽의 sha256을 적는다.
- 표준 출력·오류는 UTF-8, LF로 고정한다. 저장소 파일은 읽기만 하고 `__pycache__`도 쓰지 않는다.

## 무엇을 보고 짰나

| 파일 | 스펙 |
| --- | --- |
| `classify.py` | classification.md 3.2(JSON 모양), 4.1(트리 순서), 4.2(조건 표), 4.3(행 만들기), 4.4(값), 4.5(문장 틀·문구 표), 4.6(읽은 답 표, 기본값 표시), 4.7(확인 항목), 4.8(AI 과잉 후보), 4.12(추정 표시). 지시문 3.3 |
| `compare.py` | metrics.md 2절(미검증이면 파일을 읽지 않음), 3.1(항목 순서), 3.2(속성 표, `items`에 쓰는 항목), 4.1(패턴, 이름 맞추기, 서수 정렬), 4.3(키), 4.4(항목·셈, 미검증 표), 4.5(숫자 모양, 허용 오차, 넘침), 4.8(퍼센트) |
| `oracle_csv.py` | metrics.md 4.2(CSV 읽기), 4.6(정규화) |
| `oracle_case.py` | case-schema.md 4.3(최상위), 4.4(단계, 답 10개 표, 기본값), 4.5(inputs·compare·thresholds), 4.6(값 형식), 4.13(inferred) |
| `oracle_yaml.py` | case-schema.md 4.6(null 낱말, yes·no는 글), 4.7(받지 않는 기능), 4.8(인코딩·줄바꿈) |
| `oracle_json.py` | classification.md 3.3(바이트 규칙, 이스케이프 표). metrics.md 3.3이 같은 표를 쓴다 |
| `check.py` | ADR 0005 원문 3, T-010 수용 기준 3, classification.md 7.1 5(case.yaml 단계 = answers.yaml) |
| `selftest.py` | classification.md 4.10·6.1·6.2·6.5, metrics.md 3.3(변형)·4.1·4.5·4.6·4.8·4.10, case-schema.md 6.1 |

### 규칙 값은 코드에 옮겨 적었다

- 조건 표, 문장 틀·문구 표, 읽은 답 표, 답 10개 표, 퍼센트 셈은 스펙 표를 손으로 옮겨 상수로 두었다. 실행 중에 스펙 문서를 읽지 않는다.
- 이유: 스펙 표와 expected를 같이 바꾸면 oracle이 저절로 따라가지 않게 하려는 것이다. 표가 바뀌면 prototyper가 oracle을 고쳐야 녹색이 된다. expected 변경에 독립된 확인이 한 번 더 든다.
- `selftest.py`만 스펙 문서를 읽는다. 상수와 스펙 예시 표가 어긋나면 실패한다.

### 출발점과 다시 짠 곳

- 출발점: `befaa0a:spikes/T-006/xcheck/compare.py`, `a65f02e:spikes/T-009/xcheck/{classify,compare}.py`. 둘 다 머리 주석에 "스펙 글만 보고 짰다, C#을 읽지 않았다"가 있다. `src/`를 읽은 흔적은 찾지 못했다.
- 그래도 스펙이 정하지 않은 동작을 넣은 곳은 스펙으로 다시 짰다:
  - JSON 이스케이프: T-009 `classify.py`의 `\n` 같은 짧은 꼴, U+2029 이스케이프는 3.3 표에 없다. 표가 정하지 않은 글자는 멈춘다(아래 "범위 밖").
  - 파일 찾기: 정규식과 코드 포인트 정렬을 4.1의 UTF-16 서수 맞추기·정렬로 바꿨다. 폴더 조각도 이름 목록에서 서수로 찾는다.
  - CSV: 표준 `csv` 모듈과 "레코드 번호 + 1 = 줄" 가정을 버렸다. 4.2대로 직접 읽는다(행이 시작한 물리 줄, 따옴표 안 줄바꿈, CR).
  - YAML: 정규식으로 줄을 뽑던 것을 부분집합 파서와 스키마 검사로 바꿨다.
  - 숫자: 반올림하면 예외를 내는 decimal 문맥(`EXACT`)과 `copy_abs()`를 쓴다. 파이썬 기본 문맥(28자리)이 `abs()`에서 decimal 최댓값을 반올림하는 것을 `selftest.py`가 잡았다.
  - 분류 규칙: 실행 중 스펙 표를 읽던 것을 상수로 옮겼다(위 이유).
- `generate.py`(합성 생성기)는 옮기지 않았다. oracle 대상은 classification.json·metrics.json이다.

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
- 심볼릭 링크(스펙 미확인).
- 멈추면: `classify.py`·`compare.py`는 종료 코드 2와 이유. `check.py`는 그 파일을 `oracle=error`로 적고 종료 코드 1.

## 해석 (스펙 글이 다 정하지 않아 oracle이 고른 것)

- 그대로 쓰는 글자: 3.3 표의 "그대로" 예(①~④, 한글, NFD 자모, `<>&'+`, 백틱)를 BMP의 L·M·N·P·S 범주 전체로 넓혀 읽었다.
- `\uXXXX`: 표의 U+0001·U+007F·U+2028을 C0 제어(탭·LF·CR·BS·FF 빼고), DEL, U+2028로 읽었다. 대문자 16진이다.
- 서로게이트 쌍: 표의 이모지(U+1F600)를 BMP 밖 글자 전체로 넓혀 읽었다.
- decimal 범위: 최댓값 79228162514264337593543950335, 가수 96비트, 소수 28자리(4.5 숫자 모양 표에서 읽음).

## 남은 한계

- 엔진과 oracle이 같은 스펙을 같은 방식으로 잘못 읽으면 둘 다 틀린 바이트를 낸다(ADR 0005 "남은 위험").
- 오류 경로는 oracle이 보지 않는다. 오류 메시지·순서·위치의 회귀는 xUnit 테스트만 막는다.
- 공개 예제 4종에는 Q1·Q4, 이미지·대화 입력, 여러 CSV 파일, 따옴표 CSV, `inferred`가 없다. `check.py`는 이 경로를 보지 않는다. `selftest.py`의 스펙 표 대조와 TestData 표본이 일부를 본다.
- Windows에서는 돌려 보지 않았다(미확인).

## 검증 기록 (2026-11-07, 이 환경)

- `check.py`: 예제 4종 8파일 모두 MATCH, 종료 코드 0.
- `selftest.py`: 통과 327, 실패 0. 음성 대조 4가지(expected 한 바이트, expected 삭제, 결과 CSV 값, answers.yaml 답) 모두 DIFF·MISSING과 종료 코드 1. 되돌리면 다시 모두 MATCH.
- Python 3.10.20, 3.11, 3.12, 3.13.18에서 `check.py` 표준 출력 바이트가 같다. `python3 -I`로도 돈다.
