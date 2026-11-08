# solution-skeleton (T-001 스펙)

- 상태: 승인됨(2026-11-06, juhan "제안대로"). 지금 동작만 남겼다. 승인 질문·스파이크 단계·확인 로그는 저장소 이력에 있다.
- 작업 카드: `docs/tasks/T-001-solution-skeleton.md`

## 1. 범위

새 clone에서 빌드·테스트가 되고, 루트 문서·`.gitignore`·솔루션 구조가 맞는지. `.claude/` 동작, 다른 스펙, `examples/`, `dotnet publish`는 범위 밖이다.

## 2. 입력

| 입력 | 내용 |
| --- | --- |
| 새 clone | `git clone` 직후 작업 트리. `bin/`, `obj/` 없음 |
| .NET SDK | `global.json`으로 고정: `sdk.version` 10.0.100, `rollForward` latestFeature |
| NuGet | restore 때 nuget.org에서 받는다. 캐시가 비어 있어도 된다 |
| CI | `.github/workflows/ci.yml` 잡 `build-test`(ubuntu-latest, windows-latest) |

## 3. 출력

| 명령 | 출력 |
| --- | --- |
| `dotnet build` | `Scorecard.Core.dll`, `scorecard.dll`과 실행 파일 `scorecard`, `Scorecard.Tests.dll` |
| `dotnet test` | 테스트 결과. 종료 코드 0이 통과 |

## 4. 규칙

수용 기준 번호와 규칙 번호를 맞춘다. R1~R3은 CI 단계의 종료 코드가 곧 검사다. R4~R6은 xUnit 구조 검사 테스트(`tests/Scorecard.Tests/Structure/`)다.

### R1. 빌드가 경고·오류 0개로 끝난다

- 명령: `dotnet build AIAdoptionScorecard.sln -c Release -warnaserror`. 종료 코드 0이 통과다.
- CI build 단계에 `-warnaserror`가 있다. 경고 하나도 빌드 실패가 된다.

### R2. 테스트가 종료 코드 0으로 끝난다

- 명령: `dotnet test AIAdoptionScorecard.sln --no-build -c Release -- RunConfiguration.TreatNoTestsAsError=true`.
- 테스트가 하나라도 실패하면 종료 코드 1이다. 테스트 발견이 깨져 0개가 되어도 1이다.

### R3. CI `build-test`가 PR에서 녹색이다

- 단계: restore → `build --no-restore -c Release -warnaserror` → `test --no-build -c Release`(위 R2 설정).
- ubuntu-latest와 windows-latest 둘 다 녹색이어야 한다.

### R4. 루트 문서 4개가 있다

| 파일 | 통과 조건 |
| --- | --- |
| `LICENSE` | 첫 줄 `MIT License`(저작권 줄은 `Copyright (c) 2026 royjuhan`) |
| `README.md` | 있음. `README.ko.md` 링크 |
| `README.ko.md` | 있음. `README.md` 링크 |
| `CONTRIBUTING.md` | 있음 |

- 제목·섹션 문구는 검사하지 않는다.

### R5. `.gitignore`가 루트의 `cases/`를 막는다

- 통과 조건: `.gitignore`의 한 줄이 앞뒤 공백을 빼고 `/cases/`와 같다.
- `/cases/`는 루트만 막는다. `core.ignorecase=true` clone에서도 `src/Scorecard.Core/Cases/` 같은 코드 폴더를 막지 않는다.

### R6. 솔루션에 세 프로젝트가 있다

`dotnet sln AIAdoptionScorecard.sln list`:

```
src/Scorecard.Cli/Scorecard.Cli.csproj
src/Scorecard.Core/Scorecard.Core.csproj
tests/Scorecard.Tests/Scorecard.Tests.csproj
```

| 프로젝트 | 통과 조건 |
| --- | --- |
| `Scorecard.Core` | 라이브러리, `net10.0` |
| `Scorecard.Cli` | `OutputType` Exe, `AssemblyName` `scorecard`, `net10.0`, Core 참조 |
| `Scorecard.Tests` | xUnit, `net10.0`, Core 참조 |

- 세 프로젝트 모두 `ImplicitUsings`, `Nullable` enable.

## 5. 예외

| 상황 | 결과 |
| --- | --- |
| 테스트 1개 실패 | 종료 코드 1 |
| 테스트 0개(`TreatNoTestsAsError=true`) | 종료 코드 1 |
| 빌드 경고 1개(`-warnaserror`) | 종료 코드 1, 경고가 오류로 바뀐다 |
| nuget.org에 접근할 수 없는 PC | **미확인** |

## 6. 예시

```
$ dotnet build AIAdoptionScorecard.sln -c Release -warnaserror
Build succeeded.
    0 Warning(s)
    0 Error(s)
$ dotnet sln AIAdoptionScorecard.sln list
Project(s)
----------
src/Scorecard.Cli/Scorecard.Cli.csproj
src/Scorecard.Core/Scorecard.Core.csproj
tests/Scorecard.Tests/Scorecard.Tests.csproj
```

## 7. 저장소 루트 찾기 (테스트)

- 테스트 실행 폴더(`AppContext.BaseDirectory`)에서 위로 올라가며 `AIAdoptionScorecard.sln`이 있는 첫 폴더를 루트로 본다. Debug·Release 모두 된다.
- git·dotnet 같은 외부 프로세스는 부르지 않는다. 파일만 읽는다(`Structure/RepoRoot.cs`).

## 8. 미확인

1. nuget.org 접근 불가 환경의 restore 동작.
2. macOS에서 build·test.
