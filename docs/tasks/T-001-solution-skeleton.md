# T-001 솔루션 골격

- 마일스톤: M0
- 상태: 리뷰 중 (스펙 승인 2026-11-06)

## 목표

LICENSE(MIT), README 골격, `.gitignore`(cases/), CI 워크플로, .NET 10 솔루션(Core, Cli, Tests)을 갖춘다. `dotnet build`와 `dotnet test`가 성공한다(테스트 0개).

## 수용 기준

1. 새 clone에서 `dotnet build AIAdoptionScorecard.sln`이 경고·오류 없이 통과한다.
2. 새 clone에서 `dotnet test AIAdoptionScorecard.sln`이 종료 코드 0으로 끝난다(테스트 0개).
3. GitHub Actions `ci` 워크플로가 PR에서 녹색이다.
4. 루트에 `LICENSE`(MIT), `README.md`, `README.ko.md`, `CONTRIBUTING.md`가 있다.
5. `.gitignore`에 `cases/`가 있다.
6. 솔루션 `AIAdoptionScorecard.sln`에 `src/Scorecard.Core`, `src/Scorecard.Cli`(실행 파일 이름 `scorecard`), `tests/Scorecard.Tests`(xUnit)가 있다.

## 폴더 범위

루트, `src/`, `tests/`, `.github/`

## 샘플 자료

없음.

## 메모

리드 세션이 14절 2~4단계에서 골격 파일을 먼저 만들었다. 이 카드는 그 골격을 스펙으로 확정하고, 에이전트 흐름(스펙→테스트→구현→리뷰→PR)을 한 번 완주하는 데 쓴다.
