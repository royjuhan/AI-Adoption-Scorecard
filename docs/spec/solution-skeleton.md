# solution-skeleton (T-001 스펙)

- 상태: 승인됨 (2026-11-06, juhan). 본문과 맨 끝 "사람 승인 결과"가 다르면 승인 결과가 우선한다.
- 작업 카드: `docs/tasks/T-001-solution-skeleton.md`
- 작성: prototyper, 2026-11-06
- 확인 대상 커밋: `d953e48` (리드 세션이 만든 골격)
- 확인 환경: Linux ubuntu 24.04 x64, .NET SDK 10.0.114, VSTest 18.0.2
- 스파이크: 리드 세션 로컬에서만 실행했고 저장소에 넣지 않는다(머지 대상 아님). 요약은 이 문서 9절.

## 0. 표기

- `[읽음]`: 커밋된 파일을 읽어서 확인한 사실.
- `[A]`~`[K]`: 스파이크 단계에서 실행해 확인한 사실. 단계 이름은 9절.
- `[GH]`: GitHub REST API로 2026-11-06에 조회한 사실.
- **미확인**: 실험하지 않았거나 이 환경에서 확인할 수 없는 것.

## 1. 범위

이 스펙은 카드의 수용 기준 1~6만 다룬다.

범위 밖: `.claude/` 훅·에이전트 정의의 동작, `docs/spec/`의 다른 스펙 본문, `examples/`, `dotnet publish`(win-x64 단일 exe).

## 2. 입력

| 입력 | 내용 |
| --- | --- |
| 새 clone | `git clone` 직후 작업 트리. `bin/`, `obj/` 없음. |
| .NET SDK | 10.x. 저장소에 `global.json`이 없어서 SDK 버전을 고정하지 않는다 [읽음]. |
| NuGet | restore 때 nuget.org에서 패키지 14개를 받는다 [G]. 캐시가 비어 있어도 된다 [G]. |
| CI | `.github/workflows/ci.yml` [읽음] |

restore가 받는 패키지 [G]: coverlet.collector 6.0.4, Microsoft.CodeCoverage 17.14.1, Microsoft.NET.Test.Sdk 17.14.1, Microsoft.TestPlatform.ObjectModel 17.14.1, Microsoft.TestPlatform.TestHost 17.14.1, Newtonsoft.Json 13.0.3, xunit 2.9.3, xunit.abstractions 2.0.3, xunit.analyzers 1.18.0, xunit.assert 2.9.3, xunit.core 2.9.3, xunit.extensibility.core 2.9.3, xunit.extensibility.execution 2.9.3, xunit.runner.visualstudio 3.1.4.

## 3. 출력

| 명령 | 출력 |
| --- | --- |
| `dotnet build AIAdoptionScorecard.sln` | `src/Scorecard.Core/bin/Debug/net10.0/Scorecard.Core.dll`, `src/Scorecard.Cli/bin/Debug/net10.0/scorecard.dll`과 실행 파일 `scorecard`, `tests/Scorecard.Tests/bin/Debug/net10.0/Scorecard.Tests.dll` [A] |
| `dotnet run --project src/Scorecard.Cli` | `scorecard: 아직 명령이 없습니다 (M0).` 한 줄, 종료 코드 0 [A] |
| `dotnet test AIAdoptionScorecard.sln` | `No test is available in .../Scorecard.Tests.dll. ...` 한 줄, 종료 코드 0 [A][B] |

Windows에서 실행 파일 이름이 `scorecard.exe`가 되는지: **미확인** (Linux에서는 확장자 없는 `scorecard` [A]).

## 4. 규칙

수용 기준 번호와 규칙 번호를 맞춘다.

### R1. 새 clone에서 빌드가 경고·오류 0개로 끝난다

- 명령: `dotnet build AIAdoptionScorecard.sln`
- 통과 조건: 종료 코드 0, 요약 줄 `0 Warning(s)`, `0 Error(s)`.
- 확인: Debug [A], Release [C], 빈 캐시 [G] 모두 경고 0, 오류 0.
- 같은 빌드에 `-warnaserror`를 붙여도 종료 코드 0 [A][C][G]. 즉 지금 숨은 경고가 없다.
- 판정 방법: `-warnaserror`를 붙인 빌드의 종료 코드 0. 요약 줄을 눈으로 보는 것보다 기계적이다.
- 주의: 지금 `ci.yml`의 build 단계에는 `-warnaserror`가 없다 [읽음]. 경고 1개를 일부러 넣으면 CI와 같은 명령의 종료 코드는 0이다 [K]. 즉 CI는 R1을 강제하지 않는다. → 승인 질문 3.

### R2. 새 clone에서 테스트가 종료 코드 0으로 끝난다

- 명령: `dotnet test AIAdoptionScorecard.sln`
- 통과 조건: 종료 코드 0.
- 확인: build 뒤 test [A], build 없이 test만 [B], CI와 같은 `--no-build -c Release` [C] 모두 0.
- 테스트 0개일 때 VSTest는 `No test is available ...` 한 줄을 내고 종료 코드 0을 돌려준다 [A]. 이 줄은 정상으로 본다.
- `-- RunConfiguration.TreatNoTestsAsError=true`를 붙이면 같은 상태에서 종료 코드 1이다 [D]. 지금은 쓰지 않는다. → 승인 질문 8.
- 테스트 1개가 실패하면 종료 코드 1, 모두 통과하면 0이다 [E]. 즉 종료 코드로 성공·실패를 가를 수 있다.
- 걸린 시간: 새 clone에서 3~5초 [A][B]. Stop 훅 명령 `dotnet test --nologo -v q`도 루트에서 종료 코드 0, 약 3초 [A].

### R3. GitHub Actions `ci` 워크플로가 PR에서 녹색이다

- `ci.yml` 내용 [읽음]: 이름 `ci`, 트리거 `push`(main)와 `pull_request`, 잡 `build-test`, `ubuntu-latest`, `actions/checkout@v4`, `actions/setup-dotnet@v4`(`dotnet-version: 10.0.x`). 단계는 restore → `build --no-restore -c Release` → `test --no-build -c Release`.
- 같은 세 단계를 로컬 새 clone에서 돌리면 모두 종료 코드 0 [C].
- 통과 조건: T-001 PR의 체크 `build-test (ubuntu-latest)`와 `build-test (windows-latest)`가 모두 success (승인 결정 6으로 matrix).
- GitHub 현재 상태 [GH]: 저장소 public, 기본 브랜치 main, 등록된 워크플로 0개, 실행 0건, PR 0건.
- `origin/main`(`58284b7`)에는 `.github/workflows/ci.yml`이 없다 [읽음: `git ls-tree origin/main`]. 작업 브랜치 push는 트리거(`push`는 main만)에 걸리지 않는다 [읽음]. 워크플로 0개의 원인이 이것인지는 **미확인**.
- GitHub에서 실제로 녹색인지: **미확인**. PR을 열어야 확인된다. → 승인 질문 2.
- 저장소의 Actions 활성화 설정: **미확인** (조회가 프록시에서 막힘 [GH]).
- CI의 setup-dotnet이 받는 SDK 정확한 버전: **미확인**. → 승인 질문 7.
- `checkout@v4`·`setup-dotnet@v4`가 Actions에서 런타임 관련 경고(annotation)를 내는지: **미확인**.

### R4. 루트 문서 4개가 있다

| 파일 | 통과 조건 | 현재 [읽음] |
| --- | --- | --- |
| `LICENSE` | 첫 줄 `MIT License`, MIT 본문 | 맞음. 저작권 줄 `Copyright (c) 2026 royjuhan` → 승인 질문 9 |
| `README.md` | 있음. `README.ko.md` 링크 | `[한국어](README.ko.md)` 있음 |
| `README.ko.md` | 있음. `README.md` 링크 | `[English](README.md)` 있음 |
| `CONTRIBUTING.md` | 있음 | 골격(“M4에서 채운다”) |

- "골격"의 최소 내용은 위 표까지로 정한다. 제목·섹션 문구는 검사하지 않는다.

### R5. `.gitignore`에 `cases/`가 있다

- 통과 조건: `.gitignore`의 한 줄이 앞뒤 공백을 빼고 `cases/`와 같다.
- 현재: 2번째 줄 `cases/` [읽음]. `git check-ignore`로 `cases/x/case.yaml`이 무시됨 [A].
- 함께 있는 줄 [A]: `spikes/**/bin/`, `notebooks/**/.ipynb_checkpoints/` (지시문 5절과 일치).
- 패턴 범위 [I][J]:

| 경로 | `cases/` (현재), ignorecase=false | `cases/` (현재), ignorecase=true | `/cases/`, ignorecase=true |
| --- | --- | --- | --- |
| `cases/x/case.yaml` | 무시 | 무시 | 무시 |
| `tests/Scorecard.Tests/cases/x.yaml` | 무시 | 무시 | 무시 안 함 |
| `examples/table-compare/cases/x` | 무시 | 무시 | 무시 안 함 |
| `src/Scorecard.Core/Cases/CaseModel.cs` | 무시 안 함 | **무시** | 무시 안 함 |

- 즉 현재 패턴은 어느 깊이의 `cases/`도 무시한다. `core.ignorecase=true`인 clone에서는 `Cases/` 폴더까지 무시한다.
- Windows·macOS clone의 기본값이 `core.ignorecase=true`인지: **미확인** (이 스파이크는 `git -c core.ignorecase=true`로 흉내만 냈다). → 승인 질문 5.

### R6. 솔루션에 세 프로젝트가 있다

- `dotnet sln AIAdoptionScorecard.sln list` 출력이 정확히 아래 3줄 [A]:

```
src/Scorecard.Cli/Scorecard.Cli.csproj
src/Scorecard.Core/Scorecard.Core.csproj
tests/Scorecard.Tests/Scorecard.Tests.csproj
```

| 프로젝트 | 통과 조건 | 현재 [읽음] |
| --- | --- | --- |
| `Scorecard.Core` | 라이브러리, `net10.0` | 맞음. 소스 파일 0개. 빈 어셈블리로 빌드됨 [A] |
| `Scorecard.Cli` | `OutputType` Exe, `AssemblyName` `scorecard`, `net10.0`, Core 참조 | 맞음. `RootNamespace` `Scorecard.Cli` |
| `Scorecard.Tests` | xUnit, `net10.0`, Core 참조 | 맞음. 소스 파일 0개. Cli는 참조하지 않음 |

- 세 프로젝트 모두 `ImplicitUsings`, `Nullable` enable [읽음].
- 솔루션 형식은 `.sln`(Format Version 12.00) [읽음]. `dotnet format --verify-no-changes`도 종료 코드 0 [F].

## 5. 예외

| 상황 | 결과 | 근거 |
| --- | --- | --- |
| 테스트 0개 | 종료 코드 0. `No test is available` 줄은 정상 | [A] |
| 테스트 0개 + `TreatNoTestsAsError=true` | 종료 코드 1 | [D] |
| 테스트 1개 실패 | 종료 코드 1 | [E] |
| 빌드 경고 1개, `-warnaserror` 없음 (지금 CI) | 종료 코드 0, `1 Warning(s)` → CI 녹색으로 남음 | [K] |
| 빌드 경고 1개, `-warnaserror` 있음 | 종료 코드 1, 경고가 `error CS1030`으로 바뀜 | [K] |
| NuGet 캐시·HTTP 캐시가 빈 새 PC | restore 성공. 취약점 감사 데이터도 받음. NU19xx 경고 없음 | [G] |
| nuget.org에 접근할 수 없는 PC | **미확인** (실험 안 함) | — |
| Windows·macOS에서 build·test | **미확인** (Linux에서만 실행) | — |

## 6. 예시

새 clone에서 수용 기준 1·2·6을 확인하는 명령 (출력 줄은 [A][G]의 로그에서 옮김, 경로는 줄임):

```
$ git clone <repo> && cd AI-Adoption-Scorecard
$ dotnet build AIAdoptionScorecard.sln -warnaserror
Build succeeded.
    0 Warning(s)
    0 Error(s)
$ echo $?
0
$ dotnet test AIAdoptionScorecard.sln
VSTest version 18.0.2 (x64)
Starting test execution, please wait...
A total of 1 test files matched the specified pattern.
No test is available in .../tests/Scorecard.Tests/bin/Debug/net10.0/Scorecard.Tests.dll. ...
$ echo $?
0
$ dotnet sln AIAdoptionScorecard.sln list
Project(s)
----------
src/Scorecard.Cli/Scorecard.Cli.csproj
src/Scorecard.Core/Scorecard.Core.csproj
tests/Scorecard.Tests/Scorecard.Tests.csproj
```

`case.yaml` 예시: 해당 없음 (이 카드는 케이스를 다루지 않는다).

## 7. 테스트로 옮기는 방법 (test-engineer 참고)

- R4·R5·R6은 xUnit 구조 검사 테스트로 옮길 수 있다. 테스트 안에서 `AppContext.BaseDirectory`부터 위로 올라가 `AIAdoptionScorecard.sln`이 있는 폴더를 찾으면 루트 파일을 읽을 수 있다. Debug·Release 모두 통과 [H]. 스파이크에서 쓴 검사: `LICENSE` 첫 줄, README 두 벌·CONTRIBUTING 존재, `.gitignore`의 `cases/` 줄, Cli csproj의 `AssemblyName`.
- R1·R2·R3은 xUnit 안에서 검사하지 않는다. CI 단계의 종료 코드가 곧 검사다.
- 골격이 이미 있으므로 구조 검사 테스트는 처음부터 통과한다. 그리고 테스트를 넣는 순간 "테스트 0개" 상태가 끝난다. → 승인 질문 1.
- GitHub 러너의 checkout 경로에서도 위 루트 찾기가 되는지: **미확인** (로컬 clone에서만 확인).

## 8. 미확인 목록

1. GitHub Actions에서 `ci / build-test`가 실제로 녹색인지.
2. CI의 setup-dotnet `10.0.x`가 받는 SDK 버전과, 로컬 10.0.114와의 차이.
3. 저장소의 Actions 활성화 설정.
4. `checkout@v4`·`setup-dotnet@v4`의 런타임 관련 경고 여부.
5. Windows·macOS에서 build·test, Windows 실행 파일 이름 `scorecard.exe`.
6. Windows·macOS clone의 `core.ignorecase` 기본값.
7. nuget.org 접근 불가 환경의 restore 동작.
8. GitHub 러너에서 구조 검사 테스트의 루트 찾기.

## 9. 스파이크 단계 목록

`spikes/T-001/run-spike.sh <작업폴더> <로그폴더>`. 저장소를 로컬 clone해서 저장소 밖 작업 폴더에서 돌린다. 메인 작업 트리의 `src/`, `tests/`는 건드리지 않는다. 빈 `NUGET_PACKAGES`를 쓴다.

| 단계 | 한 일 |
| --- | --- |
| A | 새 clone → build(Debug) → `-warnaserror` 재빌드 → test → `sln list` → CLI 실행 → `check-ignore` → Stop 훅 명령 |
| B | 새 clone → build 없이 test만 |
| C | 새 clone → CI 세 단계(restore, Release build, Release test) → Release `-warnaserror` |
| D | C 결과에 `TreatNoTestsAsError=true` |
| E | 새 clone에 통과 1·실패 1 테스트 → test, 다시 둘 다 통과로 → test |
| F | `dotnet format --verify-no-changes` |
| G | 패키지 캐시·HTTP 캐시 모두 빈 상태로 restore(-v n) → build `-warnaserror` → test |
| H | 루트 찾기 구조 검사 테스트 1개 → Debug·Release test |
| I | `cases/` 패턴 범위, `core.ignorecase` false·true |
| J | `/cases/`로 바꿨을 때 범위 (스크래치 clone에서만) |
| K | Core에 `#warning` 1개 → CI 명령 build, `-warnaserror` build |

---

## 사람 승인 필요

1. **수용 기준 2의 "(테스트 0개)" 해석.** (a) 지금 상태 설명일 뿐이고 요구는 "종료 코드 0"이다. test-engineer가 T-001에서 R4·R5·R6 구조 검사 테스트를 넣는다. (b) T-001 머지까지 테스트 0개를 유지한다. test-engineer는 T-001에서 테스트를 쓰지 않는다. 카드 메모가 "스펙→테스트→구현→리뷰→PR 완주"이므로 prototyper는 (a)를 제안한다. (a)라면, 골격이 이미 있어 처음부터 통과하는 테스트를 "실패 테스트 먼저" 원칙의 예외로 인정하는가(PR 본문에 적는 조건)?
2. **수용 기준 3의 판정 시점.** 지금 GitHub에 워크플로 등록 0개, 실행 0건, PR 0건이다. T-001 PR의 첫 `ci / build-test` 결과로 판정하는 데 동의하는가? 그 실행에서 annotation 경고(예: 액션 런타임)가 나오면 T-001 안에서 고치는가, 별도 카드로 넘기는가?
3. **"경고 없이"를 CI가 강제할지.** 지금 CI는 경고가 있어도 녹색이다 [K]. (a) `ci.yml` build 단계에 `-warnaserror` 추가, (b) 루트 `Directory.Build.props`에 `TreatWarningsAsErrors`(로컬 빌드도 강제), (c) 강제하지 않고 로그를 사람이 본다. 어느 것인가?
4. **루트·`.github/` 수정 담당.** T-001 폴더 범위는 루트, `src/`, `tests/`, `.github/`인데, 에이전트 폴더 제한상 루트 파일과 `.github/`를 고칠 빌더가 없다(core-dev는 `src/Scorecard.Core/`, cli-dev는 `src/Scorecard.Cli/`). 질문 3·5·6·7에서 루트나 `ci.yml`을 바꾸게 되면 리드 세션이 고치는 것으로 정해도 되는가?
5. **`.gitignore`의 `cases/` 범위.** 현재 패턴은 어느 깊이의 `cases/`도 무시하고, `core.ignorecase=true` clone에서는 `src/Scorecard.Core/Cases/` 같은 코드 폴더까지 무시한다 [I]. `/cases/`(루트만)로 바꾸는가 [J]? 바꾼다면 수용 기준 5 "`cases/`가 있다"를 `/cases/` 줄로도 충족으로 보는가?
6. **Windows 확인 방법.** 배포 대상은 win-x64인데 이번 확인은 Linux에서만 했다. (a) `ci.yml`에 `windows-latest`를 추가한다, (b) G0에서 사람이 Windows PC로 clone → build → test를 확인한다, (c) 둘 다. 어느 것인가?
7. **SDK 버전 고정.** 저장소에 `global.json`이 없다. 로컬은 10.0.114, CI는 `10.0.x`(받는 버전 미확인). `global.json`으로 고정하는가, 고정하지 않는가? 고정한다면 정확한 내용은 승인 뒤 스파이크로 확인한다.
8. **테스트 0개를 오류로 보는 설정.** `TreatNoTestsAsError=true`면 테스트 발견이 깨졌을 때 CI가 빨강이 된다 [D]. 지금 켜면 수용 기준 2와 충돌한다. 첫 테스트가 들어오는 시점(질문 1이 (a)면 T-001, (b)면 T-002)에 `ci.yml`에 켜는가?
9. **LICENSE 저작권자.** `Copyright (c) 2026 royjuhan`이 맞는 이름·연도인가?
10. **(T-001 범위 밖, 지시문 내부 충돌)** 지시문 6절 test-engineer는 "샘플 자료를 `cases/<case>/`에 만든다"인데 `cases/`는 gitignore라 커밋되지 않고 CI에도 없다. T-002 전에 test-engineer 샘플 위치(예: `examples/` 또는 `tests/` 안)를 정해 주겠는가?

## 사람 승인 결과 (2026-11-06, juhan: "제안대로")

리드 세션이 질문마다 제안한 답을 사람이 그대로 승인했다.

1. (a). test-engineer가 T-001에서 R4·R5·R6 구조 검사 테스트를 넣는다. 골격이 이미 있어 처음부터 통과하는 것은 "실패 테스트 먼저"의 예외로 인정하고, PR 본문에 적는다.
2. T-001 PR의 첫 `ci` 실행 결과로 판정한다. 실패가 아닌 annotation 경고는 별도 카드로 넘긴다.
3. (a). `ci.yml` build 단계에 `-warnaserror`를 붙인다.
4. 루트 파일과 `.github/`는 리드 세션이 고친다.
5. `.gitignore`를 `/cases/`로 바꾼다. 수용 기준 5는 `/cases/` 줄로 충족한다.
6. (c). `ci.yml`에 `windows-latest`를 추가하고, G0에서 사람이 Windows PC로도 확인한다.
7. `global.json`으로 고정한다. `sdk.version` 10.0.100, `rollForward` latestFeature.
8. 첫 테스트가 들어오는 T-001에서 `ci.yml` test 단계에 `TreatNoTestsAsError=true`를 켠다.
9. `Copyright (c) 2026 royjuhan` 그대로 둔다.
10. test-engineer의 샘플은 `tests/Scorecard.Tests/TestData/`에 합성 데이터로 둔다. 공개 골든 예제는 계속 `examples/`. 지시문 6절과 다른 결정이므로 ADR로 남긴다(docs/decisions/0002-test-samples-location.md).
