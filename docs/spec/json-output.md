# json-output (명령 틀과 AI용 입구, M2-1)

- 지금 동작만 적는다. 테스트가 읽는 표·블록은 `docs/spec/data/json-output.md`에 있다. 절 번호가 같다.
- 결정 근거: ADR 0004 결정 3·6(명령 넷, `--json`, 오류 JSON, 종료 코드 0/2/1, 상대 경로), ADR 0010(exe 문답 없음, M2 순서, 명령은 골든 테스트만). 이 카드의 선택은 9절과 STATUS 잠정 결정 T-018 행이다.
- 작업 카드: `docs/tasks/T-018-m2-1-commands.md`.

## 1. 범위

- 실행 파일 `scorecard`의 명령 일곱(사용자 명령 넷, 내부 명령 셋), 인자 규칙, 출력 폴더, 표준 출력, 결과 객체, 오류 JSON, 종료 코드.
- Cli는 Core 공개 API를 부르고 바이트를 쓴다. 분류·대조·판정 로직은 Core에만 있다(CLAUDE.md).
- 범위 밖(8절): `--times` 파일, 입력 사본, Excel. 스코어카드 HTML·`--review`·입력 SHA-256은 M2-3(`report.md`), `build`의 번들·`--step`은 M2-4(`build-request.md`)에서 더했다.

## 2. 명령

- 표는 데이터 2절. 사용자 명령 `start`·`open`·`check`·`build`는 도움말에 보인다. 내부 명령 `classify`·`compare`·`judge`는 보이지 않는다. 테스트·재현·AI용이다.
- 모양: `scorecard <명령> <케이스 폴더> [옵션]`. 케이스 폴더는 모든 명령에 필요하다.
- 묻지 않는다. 표준 입력을 읽지 않는다(ADR 0010 해석 1).
- `start`: `--answers <파일>`(필수)을 `CaseParser.Parse(바이트, 파일 이름)`로 읽고 `StepClassifier.Classify`로 분류해 출력 폴더에 `classification.json`을 쓴다. 케이스 폴더는 있어야 한다. `case.yaml`은 읽지 않는다.
- `check`: `case.yaml`을 읽고 `CompareEngine.CompareDirectory` → `JudgmentEngine.Judge`(시간·사람 입력 null)를 거쳐 `metrics.json`·`judgment.json`을 쓴다. 미검증 케이스도 성공(0)이다.
- `open`: 출력 폴더의 결과 파일로 스코어카드 결과 객체(`scorecard.json`)와 HTML(`scorecard.html`)을 쓴다. `--review <파일>`을 받는다. `--json` 표준 출력은 `scorecard.json` 바이트다. 규칙은 `report.md`(M2-3, T-020). 케이스 폴더는 있어야 한다.
- `build`: `--step`이 없으면 먼저 만들 ② 단계를 추천만 하고(아무것도 쓰지 않음, 0), `--step <번호>`면 제작 의뢰서 번들을 쓴다. 사람 확인(`export-check.md`)이 없으면 초안만 쓰고 2다. 규칙은 `build-request.md`(M2-4, T-021).
- `classify`: `--answers`가 있으면 그 파일, 없으면 `case.yaml`의 `steps`로 분류한다. 두 길은 공개 예제에서 같은 바이트를 낸다.
- `compare`, `judge`: `check`의 앞 반쪽과 전체다.
- 내부 명령은 파일을 쓰지 않는다. 성공하면 결과 문서(classification.json·metrics.json·judgment.json과 같은 바이트)를 표준 출력에 쓴다. `--json`이 없어도 같다. `--json`은 오류 모양만 바꾼다(6절).
- 도움말: `--help`, `-h`, 첫 낱말 `help`. 표준 출력, 종료 0. 내용은 데이터 2.1 블록이다. `--json`이면 데이터 5.2 객체다.

## 3. 인자 규칙

- 옵션은 `--json`, `--help`·`-h`(값 없음)와 `--out <폴더>`, `--answers <파일>`, `--review <파일>`, `--step <번호>`(값 하나)이다. `--이름=값` 모양은 받지 않는다.
- `-`로 시작하는 낱말은 옵션이다. 값 옵션 뒤 낱말이 없거나 `--`로 시작하면 "옵션 값이 없다"다.
- 위치 인자는 명령, 케이스 폴더 순이다. 더 있으면 "남는 인자다".
- 명령마다 받는 옵션은 데이터 2절 표다. 표에 없는 옵션은 "이 명령에 없는 옵션이다"다. 모르는 옵션도 같다.
- 같은 옵션이 두 번이면 오류다. `start`에 `--answers`가 없으면 "필수 옵션이 없다"다.
- 인자 오류는 모두 모아 한 번에 낸다. 인자 오류가 있으면 아무 파일도 읽지 않는다.
- 상대 경로 인자는 프로세스 현재 폴더 기준이다. Core에는 늘 Cli가 받은 경로를 그대로 넘긴다.

## 4. 출력 폴더

- 기본값은 `<케이스 폴더>/out`이다. `--out <폴더>`로 바꾼다.
- `--out`은 케이스 폴더 밖이거나 기본값과 같아야 한다. 케이스 폴더 자신, 그 안의 다른 폴더(`expected/`, `reference/` 등)는 `output` 오류다. 그래서 입력 파일을 덮어쓸 길이 없다(지시문 2절 2). 비교는 `Path.GetFullPath` 뒤 서수다.
- 출력 폴더 자리에 파일이 있으면 `output` 오류다. 폴더가 없으면 만든다.
- 쓰는 파일 이름은 고정이다(데이터 2절 표 "쓰는 파일"). 같은 이름은 덮어쓴다. 같은 입력이면 같은 바이트라 결과가 같다. 다른 파일은 건드리지 않고 지우지 않는다.
- 순서: 인자 → 입력 읽기 → 계산 → 출력 폴더 확인 → 쓰기. 앞에서 오류가 나면 폴더도 만들지 않는다. 쓰기 중 입출력 예외는 `output` "출력 파일을 쓸 수 없다: <예외 타입>"이다.
- `/examples/*/out/`는 `.gitignore`에 있다. 예제 폴더에서 기본값으로 돌려도 커밋되지 않는다.

## 5. 표준 출력과 결과 객체

### 5.1 바이트

- JSON은 모두 `classification.md` 3.3 바이트 규칙이다: `Utf8JsonWriter`, 2칸 들여쓰기, LF, 끝 `\n` 하나, UTF-8(BOM 없음), `UnsafeRelaxedJsonEscaping`. 첫 키는 정수 `schema_version`이다.
- Cli는 표준 출력 스트림에 UTF-8 바이트를 직접 쓴다. 콘솔 인코딩에 기대지 않는다.
- `--json`이면 표준 출력에 JSON 문서 하나만 쓰고 표준 오류에는 쓰지 않는다. 성공·오류 모두 같다.
- `--json`이 없으면 사람용이다. 성공 안내는 표준 출력, 오류 줄(`CaseError.ToString()`, 한 줄에 하나. 출처가 없는 Cli 오류는 앞의 `: `를 뺀다)은 표준 오류다. 명령이 없거나 모르면 오류 줄 뒤에 도움말을 붙인다. 사람용 글은 G2 대상이 아니다.

### 5.2 결과 객체 (사용자 명령)

- 모양은 데이터 5.2 블록이다. 키 셋: `schema_version`(1), `command`(명령 이름), `files`(출력 폴더 기준 상대 경로, 쓴 순서). `open`은 이 세 키 뒤에 스코어카드 내용이 붙는다(`report.md` 3절).
- 출력 폴더 경로는 넣지 않는다. 부른 쪽(스킬)은 자기가 준 `--out`으로 경로를 만든다(ADR 0004 "경로와 G2 R3").
- 결과 숫자·판정은 결과 객체에 다시 적지 않는다. 파일에 있다. 스코어카드 결과 객체(요약, 단계 표, 제안 카드)는 `report.md`(M2-3)가 정하고 이 객체를 넓힌다.
- `help --json`의 객체는 `schema_version`, `command`(`help`), `commands`(보이는 명령 넷)다.
- 내부 명령의 `--json` 출력은 결과 파일 그대로다(ADR 0004 결정 6 규칙 5). 봉투가 없다.

## 6. 오류 JSON과 종료 코드

- 오류 JSON: `schema_version`(1), `exit_code`, `errors`. `errors` 원소는 `code`, `source`, `line`, `column`, `path`, `message` 순이다(`CaseError` 여섯 값). 예는 데이터 6.1 블록이다.
- Core 오류(파서 `case-schema.md` 4.9, 대조 `metrics.md` 4.7, 문장 결과 `text-check.md` 4.5)는 값·순서 그대로 쓴다. Cli가 고치지 않는다.
- Cli 오류 코드는 데이터 6.2 표다: `argument`, `file`, `output`(모두 2), `internal`(1). `unavailable`은 T-021에서 뺐다(쓰는 곳이 없다). `source`는 파일과 상관없으면 빈 문자열, 위치가 없으면 `line`·`column`은 0이다.
- `path`는 인자 오류에서 인자 이름(`command`, `case`, `--out` 같은 옵션)이다. 파일 경로가 아니다.
- 답 파일 오류의 `source`는 파일 이름(`Path.GetFileName`)이다. 절대 경로를 쓰지 않는다.
- `internal`의 `message`는 예외 타입 이름만이다. 예외 메시지에는 절대 경로가 들 수 있다.
- 종료 코드: 0 성공(미검증·보류 판정, `build`의 추천만 포함), 2 입력 오류(인자, 파일 없음, 파싱·대조 오류, 출력 폴더, 사람 확인 없는 내보내기), 1 내부 오류(예상하지 못한 예외. `Judge`의 `ArgumentException` 포함, `judgment.md` 3.4).
- 인자 오류는 데이터 6.3 표로 테스트한다. 표의 행마다 종료 코드와 첫 오류의 다섯 값이 같다.

## 7. 결정론·원본 불변·G1·G2·G3

- Cli는 난수·현재 시각을 쓰지 않는다. 출력에 절대 경로가 없다. 문화권과 상관없다(JSON은 Core·`Utf8JsonWriter`, 사람용 숫자는 `InvariantCulture`).
- 입력(케이스 폴더, 답 파일)은 읽기만 한다. 쓰기는 출력 폴더에만 한다.
- G1: `scorecard.dll`은 N1~N6 검사를 그대로 받는다. Cli가 처음 Core를 참조해 N6 허용 목록(Cli)에 `Scorecard.Core`를 더했다. 같은 저장소의 어셈블리이고 Core 자신이 N1~N6·D1·R1 검사를 받는다. 다른 이름은 더하지 않는다.
- G2·G3 대상(데이터 7절 표): 테스트 어댑터가 `CliApp.Run`을 프로세스 안에서 부르고, 출력 폴더 파일과 표준 출력(`stdout.json`)을 본다.
- 테스트는 프로세스를 띄우지 않는다. 진입점은 `CliApp.Run(IReadOnlyList<string> args, Stream stdout, Stream stderr)`이고 종료 코드를 돌려준다. `Program.cs`는 콘솔 스트림을 넘기기만 한다.

## 8. 뒤 카드로 미룬 것

- (해결, T-020) `open`의 HTML 스코어카드와 스코어카드 결과 객체: `report.md`.
- (해결, T-021) `build`의 번들·`export-check.md`·예/아니오/나중에: `build-request.md`.
- `--times` 파일, `check`의 자료 읽기 전용 사본: 뒤 카드. 지금은 "이 명령에 없는 옵션이다"다. `--review`와 입력 SHA-256은 T-020에서 `open`에 더했다(`report.md` 5절, 4.9).
- AI 판단과 규칙 의견(`source`, `rule_opinion`): M2-2.
- 목록은 `docs/LATER.md` "M2-1에서 미룬 것"에도 있다.

## 9. 이 카드의 선택 (추천안 채택, STATUS 잠정 결정 T-018)

1. 출력 폴더: 기본 `<케이스>/out`, `--out`은 케이스 폴더 밖이나 기본값. 같은 이름 파일은 덮어쓴다.
2. 도움말: 사용자 명령 넷과 옵션 넷만 보인다. 내부 명령은 숨긴다.
3. 사용자 명령 `--json`: 결과 객체 `schema_version`·`command`·`files`. 숫자는 파일에, 넓히는 일은 `report.md`.
4. 내부 명령: 파일을 쓰지 않고 결과 문서를 표준 출력에 쓴다.
5. `open`: 결과 파일 목록만 알린다(T-020에서 스코어카드를 쓰게 바뀌었다, `report.md`). `build`: `unavailable`(2)(T-021에서 번들을 쓰게 바뀌었다, `build-request.md`).
6. 인자 해석은 직접 짠다. 지시문 4절의 System.CommandLine은 G1 N6(Cli 허용 목록) 추가가 필요해 쓰지 않는다.
7. 오류 JSON·결과 객체 쓰기는 Cli에 둔다(판정이 없다). 스코어카드 결과 객체는 M2-3에서 Core에 둔다(ADR 0004 결정 6 규칙 8).
