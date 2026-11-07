# tools/ci — 회귀를 막는 CI 잡

ADR 0005 결정 5(사람 승인 대신 CI와 독립 검증)의 잡 셋이다. 워크플로는 `.github/workflows/ci.yml`. 기존 `build-test`(ubuntu·windows)는 그대로다.

| 잡 | 하는 일 | 결과 파일(artifact) | 실패하는 때 |
| --- | --- | --- | --- |
| `mutation` | 바뀐 `src/**/*.cs`만 Stryker.NET으로 head·base 양쪽에서 잰다 | `mutation-result/mutation.json`, `mutation-reports/`(Stryker 보고서·로그) | Stryker 실행 오류·시간 초과. 점수가 내려간 것만으로는 실패하지 않는다 |
| `oracle` | base 커밋의 `tools/oracle/check.py`가 head 예제 `expected/`와 같은 바이트를 내는지 본다 | `oracle-result/`(`oracle.json`, `oracle-source.json`, 있으면 `oracle-head.json`) | `check.py` 종료 코드가 0이 아님(하나라도 match가 아님). `tools/oracle`이 바뀌었으면 head oracle도 |
| `regression-guard` | base·head에서 같은 명령으로 테스트를 돌리고(`testrun.py`), base 대비 회귀 신호를 보고, 신호가 있으면 증거를 요구한다 | `regression-guard-result/`(`regression-guard.json`, `testrun.json`), `testrun-logs/` | 아래 규칙 위반, 또는 자기 검사(단위 테스트) 실패 |

- 파일: `cigit.py`(범위·git 도우미), `mutation.py`, `testrun.py`(base·head `dotnet test`와 TRX), `regression_guard.py`, `csharp_scan.py`(C# 토큰 세기), `test_regression_guard.py`(자기 검사).
- 세 잡은 **base 커밋의 `tools/ci`·`tools/oracle`**을 돈다(아래 "도구 출처"). PR이 검증 도구를 약하게 바꿔 자기 PR을 통과시키지 못하게 한다.
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

## 도구 출처: base 도구와 부트스트랩

세 잡의 "base 도구 꺼내기" 단계(`ci.yml`, 셸 몇 줄)가 고른다.

1. base를 `cigit.resolve_range`와 같은 규칙으로 정한다: `CI_BASE`가 커밋이면 그것, 아니면 `HEAD^1`.
2. base에 `tools/ci/regression_guard.py`가 있으면 `git archive <base> tools/ci | tar -x`로 `$RUNNER_TEMP/base-tools/`에 꺼내 그것을 돈다(`CI_TOOLS`). 없으면 head의 `tools/ci`(부트스트랩).
3. `tools/oracle`도 같다. base에 `tools/oracle/check.py`가 있으면 base 것(`CI_ORACLE`), 없으면 head 것.
4. 부트스트랩은 base main에 `tools/ci`가 없는 T-010 PR 하나뿐이다. T-010이 머지되면 그 뒤 PR은 늘 base 도구로 검증된다.

- base 도구는 head 작업 트리(`--repo .`)와 head·base git 개체를 읽는다. 도구 코드만 base에서 온다.
- regression-guard는 `--require-base-tools`로 돈다. 돌고 있는 폴더의 `*.py`가 base 커밋 `tools/ci/`의 blob과 하나라도 다르면 `tools-source` FAIL이다(꺼내기 단계의 실수를 잡는다). base에 guard가 없으면 INFO(부트스트랩).
- 자기 검사: 돌릴 `tools/ci`(base 또는 부트스트랩의 head)의 단위 테스트를 먼저 돌린다. base로 돌 때 head의 `tools/ci`가 바뀌었으면 head 단위 테스트도 돌린다(바꾼 도구가 깨진 채 머지되지 않게).
- mutation 잡의 Stryker 버전도 base 커밋의 `.config/dotnet-tools.json`에서 온다(아래).
- 도구를 바꾸는 PR은 `ci-changed` 규칙(아래 C)으로 ADR과 STATUS.md 원칙 변경 행이 있어야 통과한다. 바꾼 도구는 머지된 다음 PR부터 쓰인다.

## mutation

1. 바뀐 파일: `git diff -M base head -- src/`에서 추가(A)·수정(M)·이름 변경(R)된 `.cs`. 삭제(D)는 뺀다. `obj/`·`bin/` 아래는 뺀다.
2. 바뀐 파일이 없으면 `status: not_applicable`("해당 없음")로 쓰고 녹색으로 끝난다.
3. head와 base를 각각 `git worktree`로 꺼낸다. base 커밋의 `.config/dotnet-tools.json`(없으면 head 작업 트리의 것, 부트스트랩)을 양쪽 worktree에 써서 같은 Stryker 버전으로 잰다. PR이 manifest를 바꿔도 base 버전으로 잰다. 어느 쪽을 썼는지 `tool_manifest`에 적는다.
   - worktree의 `tests/Scorecard.Tests/stryker-config.json`·`.yaml`·`.yml`은 지우고 돌린다. 설정 파일로 돌연변이를 빼서(`ignore-methods` 등) 점수를 올리지 못하게 한다. 명령 인자만 쓴다.
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
  "status": "measured | not_applicable | error | timeout",
  "base": "<40자>", "head": "<40자>", "range_note": "…", "stryker_version": "5.0.0", "tool_manifest": "base | head",
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
- 시간: 한 번 실행은 50분(`RUN_TIMEOUT_SECONDS`), `mutation.py` 전체는 `--budget-minutes 100`. head·base 두 번(50 × 2)이 전체 안에 들고, 전체가 잡 제한 120분보다 짧다. 실행을 시작할 때 남은 시간이 2분보다 적으면 시작하지 않는다. 시간이 넘으면 `status: timeout`, `errors`에 "시간 초과"를 쓰고 mutation.json을 남긴다. regression-guard는 "mutation 측정이 시간 안에 끝나지 않았다(status=timeout)"로 증거를 거절한다. 단위 테스트가 `ci.yml`의 잡 제한·예산과 이 값을 대조한다.
- 종료 코드: 0 측정함·해당 없음, 1 Stryker 실행 오류(`status: error`)·시간 초과(`status: timeout`), 2 실행할 수 없음.

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
python3 "$CI_ORACLE/check.py" --repo . --json oracle.json   # CI_ORACLE = base 커밋의 tools/oracle(없으면 head)
```

- 계약: 종료 0 = 모든 파일 match. `oracle.json` = `{"files": [{"path": "examples/x/expected/y.json", "status": "match | diff | missing", "expected_sha256": "…", "oracle_sha256": "…"}]}`.
- 출처: 잡이 `oracle-source.json` = `{"schema_version": 1, "source": "base | head", "rev": "<oracle을 꺼낸 커밋>", "tree": "<그 커밋의 tools/oracle 트리 id>"}`을 같이 올린다. regression-guard는 base에 oracle이 있으면 `source: base`, `rev` = base, `tree` = base의 `tools/oracle`일 때만 oracle.json을 증거로 쓴다.
- `tools/oracle`이 바뀐 PR은 head oracle로도 돌린다(`oracle-head.json`). 잡은 둘 다 match여야 녹색이다. regression-guard는 head oracle 결과를 증거로 쓰지 않는다.
- 그래서 oracle을 고쳐야 재현되는 expected는 두 PR로 나눈다: (1) oracle만 고침(ADR·STATUS 원칙 변경 행, 기존 expected는 head oracle로도 match), (2) 머지 뒤 expected 추가·migration(이제 base oracle이 재현).
- `tools/oracle/`은 prototyper만 고친다(ADR 0005 결정 9). 이 폴더의 잡은 `check.py`를 부르기만 한다.
- `oracle.json`은 실패해도 올린다(`if: always()`). regression-guard가 파일별 결과를 읽는다.

## test-run (`testrun.py`)

소스를 세는 검사(`csharp_scan.py`)는 컴파일러가 아니다. 그래서 regression-guard 잡이 base·head에서 실제로 테스트를 돌려 비교한다.

```
(각 worktree 루트에서) dotnet test AIAdoptionScorecard.sln -c Release --settings <빈 runsettings> \
  --filter "FullyQualifiedName!=RegressionGuard.NoSuchTest" --logger trx --results-directory <폴더>
```

- 명령은 `testrun.py`에 고정한다. `--settings`·`--filter`는 MSBuild 전역 속성이라 저장소의 `RunSettingsFilePath`·`VSTestTestCaseFilter`(csproj·`Directory.Build.*`에 넣은 것)보다 앞선다. 그래서 runsettings·필터를 바꿔 테스트를 빼지 못한다.
- 결과 폴더의 `*.trx`를 모두 읽는다. `testName`(정규화 이름, theory는 인자 포함)별로 Passed = 통과, Failed·Error·Timeout·Aborted = 실패, 그 밖(NotExecuted 등, xUnit Skip) = 건너뜀. `InnerResults`가 있으면 안쪽 결과를 쓴다.
- `testrun.json`: `status`(`measured | error`), `base`, `head`, `command`, `sides.base`·`sides.head`(`status`, `total`, `passed`, `failed`, `skipped`, `passed_names`, `failed_names`, `skipped_names`, `seconds`, `exit_code`), `errors`. 이름 목록은 정렬하고 중복을 남긴다(xUnit이 긴 인자를 잘라 같은 이름이 둘일 수 있다).
- 한쪽이라도 TRX가 없으면(빌드 실패, 한쪽 18분 넘음) `status: error`, 종료 1. 단계는 `continue-on-error`라 guard가 이어 돌아 `test-run` FAIL로 알린다.
- 이 환경(4코어)에서 T-010 범위(base main, 1282개)는 양쪽 합 약 40초였다(NuGet 캐시가 찬 상태). 잡 제한은 45분.

## regression-guard

`needs: [mutation, oracle]`, `if: always()`. 앞 잡이 실패해도 돈다. 두 artifact를 `continue-on-error`로 받고, 없으면 "증거 없음"으로 본다. 앞 잡의 결과(`needs.*.result`)는 이유 줄에 적는다. 순서: base 도구 꺼내기 → 자기 검사(단위 테스트) → test-run → artifact 받기 → `regression_guard.py --require-base-tools`.

### C. CI 도구 변경 (`ci-changed`, 증거: ADR)

| 걸리는 것 | 통과 조건 |
| --- | --- |
| base..head에서 `.github/**`(워크플로·액션 등), `tools/ci/**`, `tools/oracle/**`, `.config/dotnet-tools.json`의 추가·수정·삭제 | 같은 PR이 (1) `docs/decisions/`에 새 `.md` 파일(ADR)을 더하고, (2) `docs/STATUS.md`의 "## 원칙 변경" 절 표에 행을 더하고, (3) 더한 행이 새 ADR 번호(파일 이름 앞 숫자, 예 `0006`)를 적는다 |

- 부트스트랩: base에 `tools/ci/regression_guard.py`가 없으면 적용하지 않고 WARN으로 남긴다(T-010 PR).
- 행 비교는 줄 그대로다. 기존 ADR 파일 수정, 다른 절 표의 행은 세지 않는다.
- mutation 증거로 넘어가지 않는다. ADR만 증거다.

### A. 테스트 쪽 신호 (증거: mutation)

| 규칙 | 걸리는 것 |
| --- | --- |
| `test-file-deleted` | `git diff -M base head -- tests/`에서 삭제(D)된 파일. `.cs`만이 아니라 `TestData` 등 추적하는 파일 모두 |
| `skip-added` | `tests/**/*.cs` 파일마다 Skip 표시의 종류별 수가 base보다 늘었다. 이름이 바뀐 파일은 base의 옛 파일과 비교한다 |
| `test-count-decreased` | 프로젝트(`.csproj` 폴더) 안 `tests/**/*.cs` 전체의 `[Fact]`·`[Theory]` 수가 줄었다(아래 "세기") |
| `guard-shrunk` | `GuardTargets.All`에 등록된 대상 이름 수(중복 뺌)가 줄었다, 또는 `tests/Scorecard.Tests/TestData/guard/` 바로 아래 폴더 수가 줄었다 |
| `test-infra-changed` | 테스트 빌드·실행 설정 파일의 추가·수정·삭제: `tests/**/*.csproj`, 어디든 `Directory.Build.*`, `*.runsettings`, `xunit.runner.json`. `<Compile Remove>`·필터·표시 이름 설정 등 소스 셈이 못 보는 길이다 |
| `test-run-missing` | test-run에서 base에 통과한 테스트 이름(중복 수 포함)이 head에서 통과하지 않는다. 이름마다 한 줄(50개까지, 나머지는 수만) |
| `test-run-skipped` | test-run의 건너뛴 수가 base보다 많다. 새로 건너뛴 이름을 적는다 |
| `test-run-total-decreased` | test-run의 전체 수가 base보다 적다 |

- `test-run`(FAIL, 증거로 넘어가지 않음): `--testrun`을 줬는데 파일이 없거나, 범위가 다르거나, `status`가 `measured`가 아니거나, 한쪽 결과가 없다. `--testrun`을 주지 않으면 WARN만 남긴다(로컬 실행). CI는 늘 준다.

Skip 표시(`csharp_scan.py`):

- 식별자와 일반 문자열의 `\uXXXX`·`\UXXXXXXXX`(문자열은 `\x`·단순 이스케이프도)를 풀고 본다. `Sk\u0069p = "…"`는 `Skip =`, `Trait("Category", "\u0053kip")`는 `Trait(skip)`이다.
- `Skip =`, `Skip=`, `SkipWhen =`·`SkipUnless =` 같은 `Skip*` 대입(`[Fact]`·`[Theory]`·`[InlineData]` 등 특성 인자, 객체 초기자 어디서든).
- `Skip*` 속성 선언·재정의: `Skip =>`, `Skip {`(접근자), `Skip ??=`.
- `*FactAttribute`·`*TheoryAttribute`(또는 그 별칭, `Fact`·`Theory`)를 상속한 클래스(`derived-attribute`). 재정의로 Skip을 걸 수 있다.
- 이름이 `Fact`·`Theory`·`FactAttribute`·`TheoryAttribute`인 클래스(`shadow-attribute`). 같은 이름공간의 `[Fact]`를 가린다.
- `using X = …FactAttribute;`·`…TheoryAttribute;`(global 포함) 별칭, 별칭 이름이 `Fact` 등인 `using`(`using-alias`). Fact·Theory 별칭 `[X]`는 `[Fact]`로 센다.
- 특성 인자 `Explicit =`(xUnit v3 명시 실행).
- `Trait(…)`의 문자열 인자에 `skip`(대소문자 무시)이 있음. 예: `[Trait("Category", "Skip")]`.
- `Assert.Skip(`·`Assert.SkipWhen(`·`Assert.SkipUnless(`, `Skip.If(`·`Skip.IfNot(`, `SkipException`, `[SkippableFact]`·`[SkippableTheory]`.
- `#if` 지시문(조건과 상관없이). `#if false`·`#if NEVER`로 테스트를 감싸는 것을 막는다. 지금 `tests/`에 `#if`가 없다.
- LINQ `.Skip(1)`은 대입이 아니라서 세지 않는다.

`[Fact]`·`[Theory]` 세기:

- `tests/` 아래 `.csproj`가 있는 폴더(프로젝트) 안의 파일만 센다. `obj/`·`bin/`은 뺀다. 프로젝트 밖 파일(예: `tests/Notes.cs`)의 Fact는 빌드에 들지 않으므로 세지 않고 `test-count-outside` INFO로 적는다. 파일을 프로젝트 밖으로 옮기면 감소로 걸린다. Skip 표시는 프로젝트 밖 파일도 본다.
- 특성 구역(`[` … `]`)의 이름 자리만 센다: `Fact`, `Theory`, `FactAttribute`, `TheoryAttribute`, 한정 이름(`Xunit.Fact`, `global::Xunit.Fact`), 대상 지정(`[method: Fact]`), 한 구역에 여럿(`[Fact, Trait(…)]`).
- 세지 않는다: 주석(`//`, `/* */`) 안, 문자열(일반·`@`·`$`·원시 `"""`)·문자 리터럴 안, 거짓으로 확정된 `#if` 구역 안(`#if false`, `#if !true`, 파일의 `#undef` 기호).

증거(위 A 규칙이 하나라도 걸리면 필요하다. 넷 모두 맞아야 한다):

1. `mutation.json`이 있고, 범위(base·head)가 이 잡과 같고, `status: measured`다.
2. base에도 있는 바뀐 src 파일 가운데 점수를 비교한 파일이 하나 이상이다.
3. 그런 파일마다 head 점수 ≥ base 점수(분수로 정확히 비교), 그리고 합계도 head ≥ base.
4. head 점수가 없는 파일이 없다. 한쪽만 유효 돌연변이가 0이면 비교할 수 없어 실패다.

- 바뀐 src가 없으면(`not_applicable`) 증거가 없으므로 실패다. 새 src 파일만 바뀐 경우도 비교할 base가 없어 실패다.
- 증거로 통과하면 신호 줄은 `WARN … mutation 증거로 통과`로 남는다. 테스트 삭제·수 감소면 `status-note` 줄이 "STATUS.md에 적는다"를 알린다(ADR 0005 남은 위험).

### B. `examples/**/expected/**`

경로 조각에 `examples/<…>/expected/`가 있는 파일. 이름 변경 감지 없이 본다(`--no-renames`). oracle 증거는 **base oracle**의 결과만 쓴다(`oracle-source`).

| 변경 | 규칙 | 통과 조건 |
| --- | --- | --- |
| base에 있던 파일 수정(M) | `expected-changed` | (1) base..head에서 그 파일을 바꾼 커밋의 메시지가 모두 `expected migration:`으로 시작한다. (2) base oracle의 `oracle.json`에서 그 파일이 `match`이고, `expected_sha256`·`oracle_sha256`이 둘 다 있고 head 파일의 SHA-256과 같다(null이면 실패) |
| base에 있던 파일 삭제(D), 이름 변경(옛 경로 삭제) | `expected-deleted` | 늘 실패. 지운 파일은 oracle이 재현할 대상이 없다 |
| 새 파일(A) | `expected-added` | 커밋 메시지는 보지 않는다. base oracle의 `oracle.json`에서 `match`(위 SHA-256 조건 포함) |
| `tools/oracle/**`과 기존 expected(M·D)를 같은 PR에서 바꿈 | `oracle-and-expected` | base oracle(head가 아님)이 head expected 바이트를 재현한다. base에 oracle이 없으면 실패. migration 메시지가 있어도 이 조건은 따로 본다 |
| oracle 결과의 출처 | `oracle-source` | expected 변경이 있을 때만 본다. base에 oracle이 있으면 `oracle-source.json`이 `source: base`, `rev` = base, `tree` = base `tools/oracle`. 없으면(부트스트랩) head oracle을 가리키거나 파일이 없어도 된다. 어긋나면 FAIL이고 oracle 증거를 모두 거절한다 |
| `expected migration:`으로 시작하는 커밋의 내용 | `migration-commit-scope` | 그 커밋이 바꾼 파일이 `examples/**/expected/**`와 `docs/spec/**`뿐이다(ADR 0004 expected 변경 규칙). 병합 커밋은 모든 부모와 다른 파일만 본다. expected를 건드리지 않은 그런 커밋도 본다 |

expected migration의 증거가 base oracle에서 나와야 하는 이유: 같은 PR이 oracle을 바꾸면 head oracle은 바꾼 expected를 그대로 "재현"할 수 있다(리뷰 재현 e6·e6b). base oracle은 PR이 손댈 수 없다.

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
7. test-run의 이름 비교는 base 통과 이름이 head 통과 이름에 모두(중복 수까지) 있어야 한다. 테스트 이름만 바꿔도, theory 인자(스펙 표에서 읽는 행 등)를 바꿔도 base 이름이 빠지므로 A 신호다. src를 같이 바꿔 mutation 증거를 내야 통과한다. 의도한 축소인지 사람이 볼 수 있게 이름을 하나씩 적는다.
8. test-run·`ci-changed`·`oracle-source`·`migration-commit-scope`는 mutation 증거로 넘어가지 않는다. mutation이 증명하는 것(바뀐 src의 테스트 강도)과 상관없는 신호다.
9. 새 ADR 행의 번호 확인은 파일 이름 앞 숫자가 행 안에 따로 서 있는지만 본다(`0006`이 `10006`에 섞이면 세지 않는다).

## 알려진 한계

- **`ci.yml` 자체는 head 것이 돈다.** GitHub는 `pull_request` 이벤트에서 PR 쪽 워크플로 파일을 돌린다. `ci.yml`을 바꾼 PR은 "base 도구 꺼내기"나 guard 단계를 빼거나 바꿀 수 있다. 그때 `ci-changed`는 base guard가 실제로 돌아야만 걸린다. 막으려면 저장소 설정에서 main의 워크플로를 강제하는 규칙(ruleset "Require workflows to pass", 조직 설정)이 필요하다. 지금은 없다. 그래서 `.github/**`가 바뀐 PR은 reviewer가 `git diff base head -- .github tools/ci tools/oracle .config`를 직접 보고, 리드는 그 PR의 guard 출력에 `ci-changed` 줄이 있는지 확인한다.
- `oracle-source.json`도 같은 워크플로가 쓴다. 워크플로를 믿는 만큼만 믿는다. guard는 rev·tree가 git의 base와 맞는지만 다시 본다.
- **주석만 바꾼 src로 증거를 만들 수 있다.** e2e 확인: StepClassifier.cs에 주석 한 줄 + 관계없는 테스트에 `Skip` → head·base 모두 100%라 통과했다. 지운·Skip한 테스트가 바뀐 파일의 돌연변이를 죽이지 않으면 점수가 그대로다. ADR 0005 "남은 위험" 그대로다. 막으려면 Core 전체 mutation(돌연변이 약 2000개)을 base와 비교해야 한다. 지금은 하지 않는다.
- 소스 셈이 못 보는 축소(`[InlineData]`·`MemberData` 행 삭제, 테스트를 public이 아니게, 빌드에서 빼기 등)는 test-run이 이름·수로 잡는다. test-run도 못 보는 것: 테스트 본문·`Assert`를 지워 늘 통과하게 만들기, 같은 이름의 약한 테스트로 바꾸기. 이것은 바뀐 src가 있을 때만 mutation이 보고, 바뀐 src가 없으면 아무 잡도 못 본다(ADR 0005 남은 위험).
- test-run은 head 코드를 돌린다. 테스트가 TRX 결과를 꾸미는 코드(예: 결과 폴더의 TRX를 덮어쓰기)는 막지 못한다. TRX는 `--results-directory`로 worktree 밖 폴더에 쓴다.
- test-run 이름 비교는 `testName`만 본다. 같은 이름의 테스트를 다른 내용으로 바꾸면 통과다(위).
- `csharp_scan.py`는 C# 컴파일러가 아니다. 테스트 코드에 쓰는 문법만 다룬다. 프로젝트가 정의하는 기호(`DEBUG` 등)는 모른다. 모르는 기호의 `#if` 구역은 센다(대신 `#if` 자체가 Skip 표시로 걸린다).
- GuardTargets는 `class GuardTargets` 안 `All = [ … ]`(또는 `new GuardTarget[] { … }`)의 `new(…)`에서 첫 두 인자가 문자열 리터럴일 때만 센다. 상수·`nameof`로 바꾸면 0개로 보여 `guard-shrunk`로 걸린다.
- Stryker: 프로젝트 전체를 빌드하고 `--mutate`로 거른다. Safe Mode가 컴파일 오류를 낸 메서드의 돌연변이를 모두 CompileError로 빼므로 그 메서드는 점수에 들지 않는다(StepClassifier.cs는 2개). Timeout 판정은 러너 속도에 따라 다를 수 있다.
- PR 병합 커밋의 첫 부모가 이벤트의 `base.sha`와 다를 수 있다(이벤트 뒤 main이 움직인 드문 경우). 그때는 main의 새 커밋이 범위에 들어간다.
- main과 PR이 같은 expected 파일을 서로 다르게 바꿔 자동 병합되면 병합 커밋이 그 파일을 바꾼 커밋으로 잡혀 실패한다(보수적).
- 세 잡은 ubuntu에서만 돈다.
- `.gitignore`에 `__pycache__/`가 있다. CI는 그래도 `PYTHONDONTWRITEBYTECODE=1`로 돈다. 로컬에서도 그렇게 돌리길 권한다(`tools-source` 비교는 `*.py`만 본다).

## 로컬에서 돌리기

```bash
export PYTHONDONTWRITEBYTECODE=1
python3 -m unittest discover -s tools/ci -p "test_*.py"
python3 tools/oracle/check.py --repo . --json oracle.json
python3 tools/ci/testrun.py --base origin/main --head HEAD --out testrun.json --work /tmp/testrun      # worktree를 만든다
python3 tools/ci/mutation.py --base origin/main --head HEAD --out mutation.json --work /tmp/mutation   # worktree를 만든다
python3 tools/ci/regression_guard.py --base origin/main --head HEAD \
  [--mutation mutation.json] [--oracle oracle.json --oracle-source oracle-source.json] [--testrun testrun.json]
```

- 로컬에서는 head의 `tools/ci`가 돈다. T-010 뒤 base 것과 다르면 `tools-source` WARN이 나온다(`--require-base-tools`를 주면 FAIL). CI와 같게 보려면 `git archive origin/main tools/ci tools/oracle | tar -x -C /tmp/base-tools`로 꺼내 그 스크립트를 돌린다.
- `--oracle-source`를 주지 않으면 base에 oracle이 있는 한 expected 변경은 `oracle-source` FAIL이다.
