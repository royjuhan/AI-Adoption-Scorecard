#!/usr/bin/env bash
# T-001 스파이크: 새 clone에서 restore/build/test 동작, 테스트 0개일 때 종료 코드를 확인한다.
# 머지 대상 아님. 메인 작업 트리의 src/, tests/에 bin/obj를 만들지 않도록 저장소 밖 작업 폴더에서 돌린다.
# 사용: run-spike.sh <작업폴더> <로그폴더>
set -u

REPO="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
WORK="${1:?작업 폴더를 주세요}"
LOGS="${2:?로그 폴더를 주세요}"
SLN=AIAdoptionScorecard.sln

rm -rf "$WORK"
mkdir -p "$WORK" "$LOGS"
: > "$LOGS/summary.txt"

# 새 PC를 흉내 내려고 빈 NuGet 패키지 캐시를 쓴다.
export NUGET_PACKAGES="$WORK/nuget-packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

note() { echo "$*" | tee -a "$LOGS/summary.txt"; }

# step <이름> <폴더> <명령...>: 종료 코드·시간·경고/오류 요약을 남긴다.
step() {
  local name="$1" dir="$2"; shift 2
  local log="$LOGS/$name.log" start end rc
  start=$(date +%s.%N)
  ( cd "$dir" && "$@" ) > "$log" 2>&1
  rc=$?
  end=$(date +%s.%N)
  note "[$name] rc=$rc time=$(printf '%.1f' "$(echo "$end - $start" | bc)")s cmd: $*"
  grep -E '^ +[0-9]+ (Warning|Error)\(s\)|경고 [0-9]+개|오류 [0-9]+개|warning [A-Z]+[0-9]+|error [A-Z]+[0-9]+|No test is available|Total tests|Passed!|Failed!|성공!|실패!' "$log" \
    | sed 's/^/    /' | sort -u | head -20 | tee -a "$LOGS/summary.txt"
}

clone() {
  git clone -q "$REPO" "$WORK/$1"
  note "[clone $1] HEAD=$(git -C "$WORK/$1" rev-parse --short HEAD)"
}

note "dotnet SDK: $(dotnet --version)"
note "repo HEAD: $(git -C "$REPO" rev-parse --short HEAD)"

# A. 새 clone → build → test (수용 기준 1, 2)
clone a
step a-build "$WORK/a" dotnet build $SLN
step a-build-again-warnaserror "$WORK/a" dotnet build $SLN --no-incremental -warnaserror
step a-test "$WORK/a" dotnet test $SLN
step a-sln-list "$WORK/a" dotnet sln $SLN list
step a-run-cli "$WORK/a" dotnet run --project src/Scorecard.Cli --no-build
ls -la "$WORK/a/src/Scorecard.Cli/bin/Debug/net10.0/" > "$LOGS/a-cli-bin-ls.log" 2>&1
step a-check-ignore "$WORK/a" git check-ignore -v cases/x/case.yaml spikes/T-001/bin/x notebooks/a/.ipynb_checkpoints/x
step a-stop-hook "$WORK/a" bash -c "dotnet test --nologo -v q"

# B. 새 clone → build 없이 test만 (수용 기준 2를 단독으로)
clone b
step b-test-only "$WORK/b" dotnet test $SLN

# C. CI 워크플로 단계 그대로 (수용 기준 3의 로컬 근사)
clone c
step c-restore "$WORK/c" dotnet restore $SLN
step c-build-release "$WORK/c" dotnet build $SLN --no-restore -c Release
step c-test-release "$WORK/c" dotnet test $SLN --no-build -c Release
step c-build-release-warnaserror "$WORK/c" dotnet build $SLN --no-restore -c Release --no-incremental -warnaserror

# D. 테스트 0개를 오류로 보는 설정이 동작하는지
step d-test-treat-no-tests-as-error "$WORK/c" dotnet test $SLN --no-build -c Release -- RunConfiguration.TreatNoTestsAsError=true

# E. 실패 테스트 1개를 넣으면 종료 코드가 0이 아닌지 (CI가 실패를 잡는가)
clone e
cat > "$WORK/e/tests/Scorecard.Tests/SpikeTests.cs" <<'CS'
namespace Scorecard.Tests;

public class SpikeTests
{
    [Fact]
    public void Passes() => Assert.True(true);

    [Fact]
    public void Fails() => Assert.True(false);
}
CS
step e-test-one-fail "$WORK/e" dotnet test $SLN
sed -i 's/Assert.True(false)/Assert.True(true)/' "$WORK/e/tests/Scorecard.Tests/SpikeTests.cs"
step e-test-all-pass "$WORK/e" dotnet test $SLN

# F. dotnet format 검사 (스타일 원칙. 수용 기준 밖)
step f-format-verify "$WORK/a" dotnet format $SLN --verify-no-changes --no-restore

# G. 패키지 캐시와 HTTP 캐시를 모두 비우고 restore → 실제로 nuget.org에서 받는지, 감사(audit) 경고가 있는지
clone g
step g-restore-cold "$WORK/g" env NUGET_PACKAGES="$WORK/g-packages" NUGET_HTTP_CACHE_PATH="$WORK/g-http-cache" \
  dotnet restore $SLN -v n
grep -c -E 'OK https://api.nuget.org' "$LOGS/g-restore-cold.log" | sed 's/^/    nuget.org OK 응답 수: /' | tee -a "$LOGS/summary.txt"
grep -E 'vulnerabilit|NU19[0-9][0-9]' "$LOGS/g-restore-cold.log" | sed 's/^/    /' | sort -u | head -5 | tee -a "$LOGS/summary.txt"
step g-build-cold "$WORK/g" env NUGET_PACKAGES="$WORK/g-packages" dotnet build $SLN --no-restore -warnaserror
step g-test-cold "$WORK/g" env NUGET_PACKAGES="$WORK/g-packages" dotnet test $SLN --no-build

# H. 테스트 실행 중에 저장소 루트(.sln 위치)를 찾아 루트 파일을 읽을 수 있는지 (구조 검사 테스트가 가능한지)
clone h
cat > "$WORK/h/tests/Scorecard.Tests/SpikeRootProbe.cs" <<'CS'
using System.Xml.Linq;

namespace Scorecard.Tests;

public class SpikeRootProbe
{
    static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AIAdoptionScorecard.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    [Fact]
    public void RootFilesReadable()
    {
        var root = FindRoot();
        Assert.StartsWith("MIT License", File.ReadAllText(Path.Combine(root, "LICENSE")));
        foreach (var f in new[] { "README.md", "README.ko.md", "CONTRIBUTING.md" })
            Assert.True(File.Exists(Path.Combine(root, f)), f);
        var lines = File.ReadAllLines(Path.Combine(root, ".gitignore")).Select(l => l.Trim());
        Assert.Contains("cases/", lines);
        var asm = XDocument.Load(Path.Combine(root, "src/Scorecard.Cli/Scorecard.Cli.csproj"))
            .Descendants("AssemblyName").Single().Value;
        Assert.Equal("scorecard", asm);
    }
}
CS
step h-test-root-probe "$WORK/h" dotnet test $SLN
step h-test-root-probe-release "$WORK/h" dotnet test $SLN -c Release

# I. .gitignore의 cases/ 패턴이 어디까지 걸리는지 (하위 폴더, 대소문자 무시 환경)
probe_ignore() {
  for p in cases/x/case.yaml tests/Scorecard.Tests/cases/x.yaml examples/table-compare/cases/x src/Scorecard.Core/Cases/CaseModel.cs; do
    if git -C "$WORK/a" "$@" check-ignore -q "$p"; then echo "    ignored     $p"; else echo "    not-ignored $p"; fi
  done
}
note "[i-ignore core.ignorecase=false]"; probe_ignore -c core.ignorecase=false | tee -a "$LOGS/summary.txt"
note "[i-ignore core.ignorecase=true]";  probe_ignore -c core.ignorecase=true  | tee -a "$LOGS/summary.txt"

# J. 대안 패턴 /cases/ (루트만) 비교. 스크래치 clone a의 .gitignore만 바꾼다.
sed -i 's|^cases/$|/cases/|' "$WORK/a/.gitignore"
note "[j-ignore '/cases/' core.ignorecase=true]"; probe_ignore -c core.ignorecase=true | tee -a "$LOGS/summary.txt"
git -C "$WORK/a" checkout -q -- .gitignore

# K. 경고 1개가 생기면 CI 단계(경고를 오류로 바꾸지 않음)의 종료 코드는? -warnaserror면?
clone k
printf 'namespace Scorecard.Core;\n#warning T001-spike\ninternal static class SpikeWarn { }\n' > "$WORK/k/src/Scorecard.Core/SpikeWarn.cs"
step k-restore "$WORK/k" dotnet restore $SLN
step k-build-release-one-warning "$WORK/k" dotnet build $SLN --no-restore -c Release
step k-build-release-one-warning-warnaserror "$WORK/k" dotnet build $SLN --no-restore -c Release --no-incremental -warnaserror

note "done"
