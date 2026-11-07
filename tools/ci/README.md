# tools/ci — 회귀를 막는 CI 잡

ADR 0005 결정 5(사람 승인 대신 CI와 독립 검증)의 잡 셋이다. 워크플로는 `.github/workflows/ci.yml`. 기존 `build-test`(ubuntu·windows)는 그대로다.

| 잡 | 하는 일 | 결과 파일(artifact) | 실패하는 때 |
| --- | --- | --- | --- |
| `mutation` | 바뀐 `src/**/*.cs`만 Stryker.NET으로 head·base 양쪽에서 잰다 | `mutation-result/mutation.json`, `mutation-reports/`(Stryker 보고서·로그) | Stryker 실행 오류. 점수가 내려간 것만으로는 실패하지 않는다 |
| `oracle` | `tools/oracle/check.py`가 예제 `expected/`와 같은 바이트를 내는지 본다 | `oracle-result/oracle.json` | `check.py` 종료 코드가 0이 아님(하나라도 match가 아님) |
| `regression-guard` | base 대비 회귀 신호를 보고, 신호가 있으면 증거를 요구한다 | `regression-guard-result/regression-guard.json` | 아래 규칙 위반, 또는 자기 검사(단위 테스트) 실패 |

- 파일: `cigit.py`(범위·git 도우미), `mutation.py`, `regression_guard.py`, `csharp_scan.py`(C# 토큰 세기), `test_regression_guard.py`(자기 검사).
- 표준 라이브러리만 쓴다. CI는 Python 3.12, 로컬 확인은 3.13.
- Stryker 버전은 `.config/dotnet-tools.json`(dotnet tool manifest)에 고정한다. 지금 `dotnet-stryker` 5.0.0.

## 범위: base와 head

두 잡(mutation, regression-guard)이 같은 범위를 본다. 규칙은 `cigit.resolve_range` 한 곳에 있다.

| 이벤트 | base | head | 비교 |
| --- | --- | --- | --- |
| `pull_request` | `github.event.pull_request.base.sha` | checkout한 커밋(`github.sha`). GitHub가 만든 병합 커밋 `refs/pull/N/merge`다 | `git diff base head`(두 점, 트리 대 트리) = 머지하면 main이 바뀌는 만큼 |
| `push`(main) | `github.event.before`(push 전 main 끝) | `github.sha` | 같음. merge commit 방식 머지라 머지된 PR 전체가 범위다 |

- base가 비었거나 0 40개(새 브랜치 push)거나 저장소에 없으면(강제 push 등) head의 첫 부모(`HEAD~1`)로 한다. 결과 줄에 그렇게 적는다.
- push(main)에서도 잡을 건너뛰지 않는다(결정). 머지 뒤 main에서 mutation·oracle·guard를 다시 돈 기록이 된다(ADR 0005 [재검토] 2). push 결과가 빨갛다고 머지가 되돌려지지는 않는다. [재검토] 조건으로 쓴다.
- checkout은 `fetch-depth: 0`. base 커밋과 PR 커밋이 모두 있어야 한다.

## mutation

1. 바뀐 파일: `git diff -M base head -- src/`에서 추가(A)·수정(M)·이름 변경(R)된 `.cs`. 삭제(D)는 뺀다. `obj/`·`bin/` 아래는 뺀다.
2. 바뀐 파일이 없으면 `status: not_applicable`("해당 없음")로 쓰고 녹색으로 끝난다.
3. head와 base를 각각 `git worktree`로 꺼낸다. head의 `.config/dotnet-tools.json`을 base worktree에도 복사해 양쪽을 같은 Stryker 버전으로 잰다.
4. 파일이 속한 프로젝트(가장 가까운 상위 폴더의 `.csproj` 하나)마다 Stryker를 한 번 돌린다. Core 파일이면 `Scorecard.Core.csproj`, Cli 파일이면 `Scorecard.Cli.csproj`. 테스트 프로젝트는 `tests/Scorecard.Tests`.
   ```
   (tests/Scorecard.Tests에서) dotnet tool run dotnet-stryker --project Scorecard.Core.csproj \
     --mutate <파일 절대 경로> [--mutate …] --reporter Json --reporter ClearText --output <폴더> --skip-version-check
   ```
   `--mutate`로 바뀐 파일만 돌연변이를 시험한다. 빌드는 프로젝트 전체다.
5. base에서는 같은 파일 목록을 base 쪽 경로로 돌린다. 이름이 바뀐 파일은 옛 경로, base에 없는 새 파일은 base 점수 없음(`null`)이다.
6. 점수 = (Killed + Timeout) / (Killed + Timeout + Survived + NoCoverage) × 100(mutation-testing-elements 정의). CompileError·RuntimeError·Ignored는 분모에 넣지 않는다. 유효 돌연변이가 0이면 `null`.

`mutation.json`:

```json
{
  "schema_version": 1,
  "status": "measured | not_applicable | error",
  "base": "<40자>", "head": "<40자>", "range_note": "…", "stryker_version": "5.0.0",
  "files": [
    {"path": "src/…/X.cs", "base_path": "src/…/X.cs | null", "project": "src/…/X.csproj",
     "head_score": 100.0, "base_score": 97.5,
     "head": {"killed": 0, "timeout": 0, "survived": 0, "no_coverage": 0, "compile_error": 0, "runtime_error": 0, "ignored": 0, "pending": 0, "other": 0, "total": 0, "score": 100.0},
     "base": {"…": "같은 모양 | null"}}
  ],
  "head_score": 100.0, "base_score": 97.5,
  "runs": [{"side": "head", "project": "…", "files": ["…"], "exit_code": 0, "seconds": 116.3}],
  "errors": []
}
```

- 맨 위 `head_score`·`base_score`는 base에도 있는 파일(점수를 비교할 수 있는 파일)만 더한 값이다. 새 파일의 점수는 `files[].head_score`에만 있다.
- job summary에 파일별 base·head·변화 표가 나온다. 리드가 머지 때 이 값을 `docs/STATUS.md`에 적는다(T-010 수용 기준 2).
- 종료 코드: 0 측정함·해당 없음, 1 Stryker 실행 오류(`status: error`), 2 실행할 수 없음.

로컬 측정(이 환경, 4코어, .NET SDK 10.0.114, dotnet-stryker 5.0.0, 2026-11-07):

| 대상 | 시험한 돌연변이 | 점수 | 한 번 실행 |
| --- | --- | --- | --- |
| `Classification/StepClassifier.cs` | 43 (CompileError 2, Ignored 10) | 100.00% | 약 1분 50초 |
| `Comparison/CompareEngine.cs` + `Classification/ClassificationJson.cs` | 196 | 95.41% | 약 2분 20초 |
| `mutation.py` 전체(StepClassifier 하나, head+base) | 43 + 43 | 100.00% / 100.00% | 약 3분 40초 |

작업 트리 밖 clone에서 일부러 만든 커밋으로 확인한 것(base = 도구를 넣은 커밋):

| 커밋 | mutation(StepClassifier.cs) | regression-guard |
| --- | --- | --- |
| s1: src 주석 한 줄 + 관계없는 테스트에 `Skip` | 100.00% → 100.00% | 통과(증거). mutation.json 없이 돌리면 실패 |
| s2: `Skip`만(src 안 바뀜) | 해당 없음 | 실패(증거 없음) |
| s3: src 주석 + 분류 테스트 파일 3개 삭제(Fact 21개) | 100.00% → 100.00% | 통과(증거). 다른 테스트가 같은 돌연변이를 죽인다 |
| s4: src 주석 + 분류 테스트 파일 11개 삭제(Fact 86개) | 100.00% → 72.09% | 실패 |
| expected 수정, 메시지 `T-099: …` | — | 실패(메시지, oracle diff) |
| expected 수정, 메시지 `expected migration: …`, 엔진·oracle은 그대로 | — | 실패(oracle diff) |
| expected 삭제, 메시지 `expected migration: …` | — | 실패 |

한 번 실행 시간의 대부분은 빌드, 첫 테스트 실행(1282개, 약 25초), 범위 측정(약 17초)이다. 돌연변이 수가 늘면 그만큼 늘어난다. 잡 제한은 120분.

## oracle

```
python3 tools/oracle/check.py --repo . --json oracle.json
```

- 계약: 종료 0 = 모든 파일 match. `oracle.json` = `{"files": [{"path": "examples/x/expected/y.json", "status": "match | diff | missing", "expected_sha256": "…", "oracle_sha256": "…"}]}`.
- `tools/oracle/`은 prototyper만 고친다(ADR 0005 결정 9). 이 폴더의 잡은 `check.py`를 부르기만 한다.
- `oracle.json`은 실패해도 올린다(`if: always()`). regression-guard가 파일별 결과를 읽는다.

## regression-guard

`needs: [mutation, oracle]`, `if: always()`. 앞 잡이 실패해도 돈다. 두 artifact를 `continue-on-error`로 받고, 없으면 "증거 없음"으로 본다. 앞 잡의 결과(`needs.*.result`)는 이유 줄에 적는다. 돌기 전에 자기 검사(`python3 -m unittest discover -s tools/ci -p "test_*.py"`)를 한다.

### A. 테스트 쪽 신호 (증거: mutation)

| 규칙 | 걸리는 것 |
| --- | --- |
| `test-file-deleted` | `git diff -M base head -- tests/`에서 삭제(D)된 파일. `.cs`만이 아니라 `TestData` 등 추적하는 파일 모두 |
| `skip-added` | `tests/**/*.cs` 파일마다 Skip 표시의 종류별 수가 base보다 늘었다. 이름이 바뀐 파일은 base의 옛 파일과 비교한다 |
| `test-count-decreased` | `tests/**/*.cs` 전체의 `[Fact]`·`[Theory]` 수가 줄었다 |
| `guard-shrunk` | `GuardTargets.All`에 등록된 대상 이름 수(중복 뺌)가 줄었다, 또는 `tests/Scorecard.Tests/TestData/guard/` 바로 아래 폴더 수가 줄었다 |

Skip 표시(`csharp_scan.py`):

- `Skip =`, `Skip=`, `SkipWhen =`·`SkipUnless =` 같은 `Skip*` 대입(`[Fact]`·`[Theory]`·`[InlineData]` 등 특성 인자, 객체 초기자 어디서든).
- 특성 인자 `Explicit =`(xUnit v3 명시 실행).
- `Trait(…)`의 문자열 인자에 `skip`(대소문자 무시)이 있음. 예: `[Trait("Category", "Skip")]`.
- `Assert.Skip(`·`Assert.SkipWhen(`·`Assert.SkipUnless(`, `Skip.If(`·`Skip.IfNot(`, `SkipException`, `[SkippableFact]`·`[SkippableTheory]`.
- `#if` 지시문(조건과 상관없이). `#if false`·`#if NEVER`로 테스트를 감싸는 것을 막는다. 지금 `tests/`에 `#if`가 없다.
- LINQ `.Skip(1)`은 대입이 아니라서 세지 않는다.

`[Fact]`·`[Theory]` 세기:

- 특성 구역(`[` … `]`)의 이름 자리만 센다: `Fact`, `Theory`, `FactAttribute`, `TheoryAttribute`, 한정 이름(`Xunit.Fact`, `global::Xunit.Fact`), 대상 지정(`[method: Fact]`), 한 구역에 여럿(`[Fact, Trait(…)]`).
- 세지 않는다: 주석(`//`, `/* */`) 안, 문자열(일반·`@`·`$`·원시 `"""`)·문자 리터럴 안, 거짓으로 확정된 `#if` 구역 안(`#if false`, `#if !true`, 파일의 `#undef` 기호).

증거(넷 중 하나라도 걸리면 필요하다):

1. `mutation.json`이 있고, 범위(base·head)가 이 잡과 같고, `status: measured`다.
2. base에도 있는 바뀐 src 파일 가운데 점수를 비교한 파일이 하나 이상이다.
3. 그런 파일마다 head 점수 ≥ base 점수(분수로 정확히 비교), 그리고 합계도 head ≥ base.
4. head 점수가 없는 파일이 없다. 한쪽만 유효 돌연변이가 0이면 비교할 수 없어 실패다.

- 바뀐 src가 없으면(`not_applicable`) 증거가 없으므로 실패다. 새 src 파일만 바뀐 경우도 비교할 base가 없어 실패다.
- 증거로 통과하면 신호 줄은 `WARN … mutation 증거로 통과`로 남는다. 테스트 삭제·수 감소면 `status-note` 줄이 "STATUS.md에 적는다"를 알린다(ADR 0005 남은 위험).

### B. `examples/**/expected/**`

경로 조각에 `examples/<…>/expected/`가 있는 파일. 이름 변경 감지 없이 본다(`--no-renames`).

| 변경 | 규칙 | 통과 조건 |
| --- | --- | --- |
| base에 있던 파일 수정(M) | `expected-changed` | (1) base..head에서 그 파일을 바꾼 커밋의 메시지가 모두 `expected migration:`으로 시작한다. (2) `oracle.json`에서 그 파일이 `match`이고, `expected_sha256`·`oracle_sha256`이 head 파일의 SHA-256과 같다 |
| base에 있던 파일 삭제(D), 이름 변경(옛 경로 삭제) | `expected-deleted` | 늘 실패. 지운 파일은 oracle이 재현할 대상이 없다 |
| 새 파일(A) | `expected-added` | 커밋 메시지는 보지 않는다. `oracle.json`에서 `match`(위 SHA-256 조건 포함) |

"그 파일을 바꾼 커밋"(`cigit.commits_changing`):

- `git rev-list base..head`의 커밋 가운데, 병합이 아닌 커밋은 첫 부모와 그 파일의 blob이 다르면 바꾼 것이다.
- 병합 커밋은 모든 부모와 blob이 다를 때만 바꾼 것이다(충돌 해결·병합 때 손댐). 한 부모와 같으면 그쪽 커밋이 바꾼 것이라 그 커밋을 본다. 그래서 GitHub의 PR 병합 커밋과 merge commit 방식 머지의 "Merge …" 메시지는 보통 걸리지 않는다.

### 출력

- 줄: `[PASS|FAIL|WARN|INFO] <규칙>: <이유>`, 끝 줄 `regression-guard: 통과|실패`.
- job summary(Markdown 표), `regression-guard.json`(`ok`, `base`, `head`, `findings[]`).
- 종료 코드: 0 통과, 1 규칙 위반, 2 실행할 수 없음(base·head 없음 등).

## 결정한 해석

1. 이름 변경: `tests/`는 `git diff -M`(git 기본 유사도 50%)으로 보고 이름 변경(R)은 삭제가 아니다. 내용을 크게 바꾼 이름 변경은 git이 삭제+추가로 보므로 `test-file-deleted`로 걸린다. 이름을 바꾸며 테스트를 줄이면 `test-count-decreased`가 따로 잡는다.
2. expected 쪽은 이름 변경을 삭제+추가로 본다. base에 있던 경로가 사라지는 것이라 늘 실패다.
3. push(main): 건너뛰지 않고 `event.before`..`github.sha`를 본다. `before`를 쓸 수 없으면 `HEAD~1`.
4. 삭제된 expected 파일은 증거가 있어도 실패다. 지우려면 사람이 규칙을 바꾸는 ADR이 필요하다.
5. mutation 잡은 점수 하락으로 실패하지 않는다. 점수 비교는 regression-guard가 A 신호가 있을 때만 한다. 그래야 테스트를 지우지 않은 일반 PR이 점수 변화만으로 막히지 않는다.
6. GuardTargets 대상은 "수"를 본다(이름 하나를 바꾸면 통과, `INFO`에 바뀐 이름을 적는다). 샘플 폴더도 수를 본다.

## 알려진 한계

- **주석만 바꾼 src로 증거를 만들 수 있다.** e2e 확인: StepClassifier.cs에 주석 한 줄 + 관계없는 테스트에 `Skip` → head·base 모두 100%라 통과했다. 지운·Skip한 테스트가 바뀐 파일의 돌연변이를 죽이지 않으면 점수가 그대로다. ADR 0005 "남은 위험" 그대로다. 막으려면 Core 전체 mutation(돌연변이 약 2000개)을 base와 비교해야 한다. 지금은 하지 않는다.
- 세지 않는 테스트 축소: `[InlineData]`·`MemberData` 행 삭제, 테스트 클래스·메서드를 public이 아니게 바꾸기, 본문·`Assert` 지우기, `[Fact]`를 상속한 사용자 정의 특성. 통과 수는 `build-test`가 보고, 리드가 STATUS.md에 적는다.
- `csharp_scan.py`는 C# 컴파일러가 아니다. 테스트 코드에 쓰는 문법만 다룬다. 프로젝트가 정의하는 기호(`DEBUG` 등)는 모른다. 모르는 기호의 `#if` 구역은 센다(대신 `#if` 자체가 Skip 표시로 걸린다).
- GuardTargets는 `class GuardTargets` 안 `All = [ … ]`(또는 `new GuardTarget[] { … }`)의 `new(…)`에서 첫 두 인자가 문자열 리터럴일 때만 센다. 상수·`nameof`로 바꾸면 0개로 보여 `guard-shrunk`로 걸린다.
- Stryker: 프로젝트 전체를 빌드하고 `--mutate`로 거른다. Safe Mode가 컴파일 오류를 낸 메서드의 돌연변이를 모두 CompileError로 빼므로 그 메서드는 점수에 들지 않는다(StepClassifier.cs는 2개). Timeout 판정은 러너 속도에 따라 다를 수 있다.
- PR 병합 커밋의 첫 부모가 이벤트의 `base.sha`와 다를 수 있다(이벤트 뒤 main이 움직인 드문 경우). 그때는 main의 새 커밋이 범위에 들어간다.
- main과 PR이 같은 expected 파일을 서로 다르게 바꿔 자동 병합되면 병합 커밋이 그 파일을 바꾼 커밋으로 잡혀 실패한다(보수적).
- 세 잡은 ubuntu에서만 돈다.
- `.gitignore`에 `__pycache__/`가 없다. CI는 `PYTHONDONTWRITEBYTECODE=1`로 돈다. 로컬에서도 그렇게 돌린다.

## 로컬에서 돌리기

```bash
export PYTHONDONTWRITEBYTECODE=1
python3 -m unittest discover -s tools/ci -p "test_*.py"
python3 tools/ci/regression_guard.py --base origin/main --head HEAD [--mutation mutation.json] [--oracle oracle.json]
dotnet tool restore
python3 tools/ci/mutation.py --base origin/main --head HEAD --out mutation.json --work /tmp/mutation   # worktree를 만든다
```
