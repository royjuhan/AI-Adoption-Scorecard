#!/usr/bin/env bash
# T-002 스파이크: 네트워크 금지 검사 방법(텍스트 vs 메타데이터)과 결정론·원본 불변 도우미의 탐지력을 확인한다.
# 머지 대상 아님. 저장소의 src/, tests/는 건드리지 않는다. 변형은 작업 폴더 안의 clone에만 넣는다.
# 아무것도 지우지 않는다. 작업 폴더는 없거나 비어 있어야 한다.
# 사용: run-spike.sh <작업폴더> <로그폴더>
set -u

SPIKE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(git -C "$SPIKE" rev-parse --show-toplevel)"
WORK="${1:?작업 폴더를 주세요}"
LOGS="${2:?로그 폴더를 주세요}"
SLN=AIAdoptionScorecard.sln

if [ -e "$WORK" ] && [ -n "$(ls -A "$WORK")" ]; then
  echo "작업 폴더가 비어 있지 않다. 새 폴더를 주세요." >&2
  exit 2
fi
mkdir -p "$WORK" "$LOGS"
WORK="$(cd "$WORK" && pwd)"
LOGS="$(cd "$LOGS" && pwd)"
SUMMARY="$LOGS/summary.txt"
: > "$SUMMARY"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# 요약에는 절대 경로를 남기지 않는다.
clean() { sed "s|$WORK|<작업>|g; s|$REPO|<저장소>|g; s|${HOME:-<홈 없음>}|<홈>|g"; }
note() { echo "$*" | clean | tee -a "$SUMMARY"; }

# step <이름> <폴더> <명령...>: 종료 코드와 중요한 줄만 요약에 남긴다. 전체 출력은 <이름>.log.
step() {
  local name="$1" dir="$2"; shift 2
  local log="$LOGS/$name.log" rc
  ( cd "$dir" && "$@" ) > "$log" 2>&1
  rc=$?
  note "[$name] rc=$rc cmd: $*"
  grep -E '^(FIND|RESULT|GUARD|CULTURE|HASH|ENUM|READONLY|SHA) |warning [A-Z]+[0-9]+|error [A-Z]+[0-9]+|^ +[0-9]+ (Warning|Error)\(s\)|Passed!|Failed!|No test is available|No data found|\[FAIL\]' "$log" \
    | clean | awk '!seen[$0]++' | head -60 | sed 's/^/    /' | tee -a "$SUMMARY"
  return 0
}

clone() {
  git clone -q "$REPO" "$WORK/$1"
  note "[clone $1] HEAD=$(git -C "$WORK/$1" rev-parse --short HEAD)"
}

note "dotnet SDK: $(dotnet --version)"
note "repo HEAD: $(git -C "$REPO" rev-parse --short HEAD)"
note "uid: $(id -u)"

# S0. 스파이크 도구 빌드 (산출물은 작업 폴더로)
step s0-tool-build "$SPIKE" dotnet build GuardSpike/GuardSpike.csproj --artifacts-path "$WORK/artifacts"
TOOLDLL="$WORK/artifacts/bin/GuardSpike/debug/GuardSpike.dll"
tool() { dotnet "$TOOLDLL" "$@"; }

# S1. 기준선: 변형 없는 clone. CI와 같은 Release 빌드.
clone base
step s1-build "$WORK/base" dotnet build $SLN -c Release -warnaserror
note "    test bin Scorecard*: $(ls "$WORK/base/tests/Scorecard.Tests/bin/Release/net10.0" | grep -i scorecard | tr '\n' ' ')"
step s1-meta-core-srcbin "$WORK/base" tool meta src/Scorecard.Core/bin/Release/net10.0/Scorecard.Core.dll --core
step s1-meta-core-testbin "$WORK/base" tool meta tests/Scorecard.Tests/bin/Release/net10.0/Scorecard.Core.dll --core
step s1-meta-cli "$WORK/base" tool meta src/Scorecard.Cli/bin/Release/net10.0/scorecard.dll
step s1-text "$WORK/base" tool text src --project-files

# S2. Core 변형. 각 변형은 새 clone에 파일을 더하고 Core만 빌드한다.
# -warnaserror 빌드(=CI 빌드 플래그)를 먼저 하고, 그다음 일반 빌드 결과를 검사한다.
scan_core() {
  local v="$1" cfg="$2" c="$WORK/$1"
  step "$v-$cfg-build-warnaserror" "$c" dotnet build src/Scorecard.Core/Scorecard.Core.csproj -c "$cfg" --no-incremental -warnaserror
  step "$v-$cfg-build" "$c" dotnet build src/Scorecard.Core/Scorecard.Core.csproj -c "$cfg"
  step "$v-$cfg-meta" "$c" tool meta "src/Scorecard.Core/bin/$cfg/net10.0/Scorecard.Core.dll" --core
}
scan_text() {
  local v="$1" c="$WORK/$1"
  step "$v-text-cs" "$c" tool text src
  step "$v-text-projfiles" "$c" tool text src --project-files
}
core_variant() {
  local v="$1"; shift
  clone "$v"
  for f in "$@"; do cp "$SPIKE/variants/$f" "$WORK/$v/src/Scorecard.Core/"; done
}

for v in V01-using-new V02-fully-qualified V03-using-alias V05-comment-only V06-string-only \
         V07-reflection-concat V08-process-instance V09-tcpclient V10-pinvoke V11-if-never \
         V14-allowed V16-clock-random V17-webclient V18-libraryimport; do
  core_variant "$v" "$v.cs"
  scan_core "$v" Release
  scan_text "$v"
done

# V04: 별칭을 Directory.Build.props에 둔다.
core_variant V04-props-alias V04-props-alias.cs
cp "$SPIKE/variants/V04-Directory.Build.props" "$WORK/V04-props-alias/src/Scorecard.Core/Directory.Build.props"
scan_core V04-props-alias Release
scan_text V04-props-alias

# V12: #if DEBUG. Debug와 Release를 따로 본다.
core_variant V12-if-debug V12-if-debug.cs
scan_core V12-if-debug Release
scan_core V12-if-debug Debug
scan_text V12-if-debug

# V13: 가짜 LLM SDK를 vendor/에 두고 Core가 참조한다(NuGet 패키지 흉내).
core_variant V13-vendor V13-vendor-call.cs
mkdir -p "$WORK/V13-vendor/vendor/FakeLlm"
cp "$SPIKE/variants/V13-vendor/FakeLlm.csproj" "$SPIKE/variants/V13-vendor/ChatClient.cs" "$WORK/V13-vendor/vendor/FakeLlm/"
step V13-add-ref "$WORK/V13-vendor" dotnet add src/Scorecard.Core/Scorecard.Core.csproj reference vendor/FakeLlm/FakeLlm.csproj
scan_core V13-vendor Release
step V13-vendor-meta-allowlisted "$WORK/V13-vendor" tool meta src/Scorecard.Core/bin/Release/net10.0/Scorecard.Core.dll --core --allow FakeLlm
scan_text V13-vendor

# V01 추가: 테스트 프로젝트 출력 폴더의 Core 복사본에도 변경이 반영되는가, 지금 테스트는 통과하는가.
step V01-tests-build "$WORK/V01-using-new" dotnet build tests/Scorecard.Tests/Scorecard.Tests.csproj -c Release -warnaserror
step V01-meta-testbin "$WORK/V01-using-new" tool meta tests/Scorecard.Tests/bin/Release/net10.0/Scorecard.Core.dll --core
step V01-tests-run "$WORK/V01-using-new" dotnet test tests/Scorecard.Tests/Scorecard.Tests.csproj --no-build -c Release -- RunConfiguration.TreatNoTestsAsError=true

# S3. 테스트 프로젝트가 Cli(실행 파일 프로젝트)를 참조하면 scorecard.dll이 테스트 출력에 오는가.
clone cliref
step s3-add-ref "$WORK/cliref" dotnet add tests/Scorecard.Tests/Scorecard.Tests.csproj reference src/Scorecard.Cli/Scorecard.Cli.csproj
step s3-build "$WORK/cliref" dotnet build $SLN -c Release -warnaserror
note "    test bin Scorecard*/scorecard*: $(ls "$WORK/cliref/tests/Scorecard.Tests/bin/Release/net10.0" | grep -i scorecard | tr '\n' ' ')"
step s3-test "$WORK/cliref" dotnet test $SLN --no-build -c Release -- RunConfiguration.TreatNoTestsAsError=true
step s3-meta-cli-testbin "$WORK/cliref" tool meta tests/Scorecard.Tests/bin/Release/net10.0/scorecard.dll
cp "$SPIKE/variants/V15-cli-http.cs" "$WORK/cliref/src/Scorecard.Cli/"
step s3-v15-build "$WORK/cliref" dotnet build $SLN -c Release -warnaserror
step s3-v15-meta-cli-testbin "$WORK/cliref" tool meta tests/Scorecard.Tests/bin/Release/net10.0/scorecard.dll

# S4. 대상 0개인 Theory.
clone xunit
cp "$SPIKE/variants/X01-empty-theory.cs" "$WORK/xunit/tests/Scorecard.Tests/"
step s4-build "$WORK/xunit" dotnet build tests/Scorecard.Tests/Scorecard.Tests.csproj -c Release -warnaserror
step s4-test-filter "$WORK/xunit" dotnet test tests/Scorecard.Tests/Scorecard.Tests.csproj --no-build -c Release --filter "FullyQualifiedName~SpikeEmptyTheory"
step s4-test-all "$WORK/xunit" dotnet test tests/Scorecard.Tests/Scorecard.Tests.csproj --no-build -c Release -- RunConfiguration.TreatNoTestsAsError=true

# S4b. 테스트 프로젝트 안 카나리아: -warnaserror 빌드, 기존 테스트 통과, 같은 스캐너로 잡히는지.
clone testcanary
cp "$SPIKE/variants/X03-test-canary.cs" "$WORK/testcanary/tests/Scorecard.Tests/"
step s4b-build "$WORK/testcanary" dotnet build $SLN -c Release -warnaserror
step s4b-test "$WORK/testcanary" dotnet test $SLN --no-build -c Release -- RunConfiguration.TreatNoTestsAsError=true
step s4b-meta-testsdll "$WORK/testcanary" tool meta tests/Scorecard.Tests/bin/Release/net10.0/Scorecard.Tests.dll --core
step s4b-meta-core "$WORK/testcanary" tool meta tests/Scorecard.Tests/bin/Release/net10.0/Scorecard.Core.dll --core

# S5. 카나리아: 검사 도구 자신의 어셈블리를 검사한다.
step s5-canary "$WORK" tool meta "$TOOLDLL" --core

# S6. 결정론·원본 불변 도우미 실험.
step s6-guard "$WORK" tool guard "$WORK/guard"

# S7. string.GetHashCode가 프로세스마다 다른가.
step s7-hash-1 "$WORK" tool hash abc
step s7-hash-2 "$WORK" tool hash abc

# S8. 열거 순서, 읽기 전용 속성.
step s8-enumorder "$WORK" tool enumorder "$WORK/enumorder"
step s8-readonly "$WORK" tool readonly "$WORK/readonly"

# S9. core.autocrlf=true clone에서 TestData 바이트가 바뀌는가, 하위 .gitattributes로 막히는가.
g="$WORK/crlf-src"
mkdir -p "$g/tests/Scorecard.Tests/TestData/guard"
printf 'id,value\n1,1.5\n' > "$g/tests/Scorecard.Tests/TestData/guard/sample.csv"
printf '\000\001\002\n\377' > "$g/tests/Scorecard.Tests/TestData/guard/blob.bin"
git -C "$g" init -q
git -C "$g" add .
git -C "$g" -c user.name=spike -c user.email=spike commit -qm "crlf 1"
git clone -q -c core.autocrlf=true "$g" "$WORK/crlf-c1"
step s9-sha-src "$WORK" tool sha crlf-src/tests/Scorecard.Tests/TestData/guard/sample.csv crlf-src/tests/Scorecard.Tests/TestData/guard/blob.bin
step s9-sha-autocrlf "$WORK" tool sha crlf-c1/tests/Scorecard.Tests/TestData/guard/sample.csv crlf-c1/tests/Scorecard.Tests/TestData/guard/blob.bin
printf '* -text\n' > "$g/tests/Scorecard.Tests/TestData/.gitattributes"
git -C "$g" add .
git -C "$g" -c user.name=spike -c user.email=spike commit -qm "crlf 2"
git clone -q -c core.autocrlf=true "$g" "$WORK/crlf-c2"
step s9-sha-autocrlf-gitattributes "$WORK" tool sha crlf-c2/tests/Scorecard.Tests/TestData/guard/sample.csv crlf-c2/tests/Scorecard.Tests/TestData/guard/blob.bin

# S10. 대안: 빌드 시 금지 API 분석기(BannedApiAnalyzers).
core_variant analyzer V01-using-new.cs
cp "$SPIKE/variants/X02-Directory.Build.props" "$WORK/analyzer/src/Scorecard.Core/Directory.Build.props"
cp "$SPIKE/variants/X02-BannedSymbols.txt" "$WORK/analyzer/src/Scorecard.Core/BannedSymbols.txt"
step s10-analyzer-v01-build "$WORK/analyzer" dotnet build src/Scorecard.Core/Scorecard.Core.csproj -c Release -warnaserror
core_variant analyzer-pragma X02-pragma.cs
cp "$SPIKE/variants/X02-Directory.Build.props" "$WORK/analyzer-pragma/src/Scorecard.Core/Directory.Build.props"
cp "$SPIKE/variants/X02-BannedSymbols.txt" "$WORK/analyzer-pragma/src/Scorecard.Core/BannedSymbols.txt"
step s10-analyzer-pragma-build "$WORK/analyzer-pragma" dotnet build src/Scorecard.Core/Scorecard.Core.csproj -c Release -warnaserror
step s10-analyzer-pragma-meta "$WORK/analyzer-pragma" tool meta src/Scorecard.Core/bin/Release/net10.0/Scorecard.Core.dll --core

note "끝"
