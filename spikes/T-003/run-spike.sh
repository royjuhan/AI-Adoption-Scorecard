#!/usr/bin/env bash
# T-003 스파이크: YamlDotNet을 Core에 넣었을 때 G1 결과, 후보 파서의 오류·인코딩·결정론, G2·G3 등록 모양을 확인한다.
# 머지 대상 아님. 저장소의 src/, tests/는 건드리지 않는다. 변형은 작업 폴더 안의 clone에만 넣는다.
# 아무것도 지우지 않는다. 작업 폴더는 없거나 비어 있어야 한다.
# 네트워크는 NuGet 패키지 받기(restore, dotnet add package)에만 쓴다.
# 사용: run-spike.sh <작업폴더> <로그폴더>
set -u

SPIKE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(git -C "$SPIKE" rev-parse --show-toplevel)"
WORK="${1:?작업 폴더를 주세요}"
LOGS="${2:?로그 폴더를 주세요}"
SLN=AIAdoptionScorecard.sln
YAML_VERSION=18.1.0

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

PKGROOT="$(dotnet nuget locals global-packages --list | sed 's/^global-packages: //')"

# 요약에는 절대 경로를 남기지 않는다.
clean() { sed "s|$WORK|<작업>|g; s|$REPO|<저장소>|g; s|${PKGROOT%/}|<패키지>|g; s|${HOME:-<홈 없음>}|<홈>|g"; }
note() { echo "$*" | clean | tee -a "$SUMMARY"; }

# step <이름> <폴더> <명령...>: 종료 코드와 중요한 줄만 요약에 남긴다. 전체 출력은 <이름>.log.
step() {
  local name="$1" dir="$2"; shift 2
  local log="$LOGS/$name.log" rc
  ( cd "$dir" && "$@" ) > "$log" 2>&1
  rc=$?
  note "[$name] rc=$rc cmd: $*"
  grep -E '^(FIND|RESULT|PROBE|ERR|SHA|ASMREF|TYPEREF|TIMING) |warning [A-Z]+[0-9]+|error [A-Z]+[0-9]+|^ +[0-9]+ (Warning|Error)\(s\)|Passed!|Failed!|\[FAIL\]|^ +(Failed|Passed) [A-Za-z]|^ +(N[1-6]|D1|R1) |금지 참조|Assert\.|Collection: |^(USER|G[23]) ' "$log" \
    | clean | awk '!seen[$0]++' | head -80 | sed 's/^/    /' | tee -a "$SUMMARY"
  return 0
}

clone() {
  git clone -q "$REPO" "$WORK/$1"
  note "[clone $1] HEAD=$(git -C "$WORK/$1" rev-parse --short HEAD)"
}

allow_yaml() {
  sed -i 's/AllowedThirdPartyAssemblies = \[\];/AllowedThirdPartyAssemblies = ["YamlDotNet"];/' \
    "$WORK/$1/tests/Scorecard.Tests/Guards/NetworkGuardTests.cs"
  note "[allow $1] $(grep -n 'AllowedThirdPartyAssemblies =' "$WORK/$1/tests/Scorecard.Tests/Guards/NetworkGuardTests.cs" | sed 's/^ *//')"
}

note "dotnet SDK: $(dotnet --version)"
note "repo HEAD: $(git -C "$REPO" rev-parse --short HEAD)"

# S0. 스파이크 도구 빌드 (산출물은 작업 폴더로)
step s0-tool-build "$SPIKE" dotnet build CaseSpike/CaseSpike.csproj --artifacts-path "$WORK/artifacts" -warnaserror
TOOLDLL="$WORK/artifacts/bin/CaseSpike/debug/CaseSpike.dll"
tool() { dotnet "$TOOLDLL" "$@"; }

# S1. 패키지 사실: 라이선스, 의존성, 대상 프레임워크, YamlDotNet.dll 자체의 참조와 G1 항목(참고용).
PKG="$PKGROOT/yamldotnet/$YAML_VERSION"
note "[s1-nuspec] $(grep -oE '<license[^>]*>[^<]*</license>' "$PKG/yamldotnet.nuspec")"
note "[s1-nuspec] dependency groups: $(grep -oE 'targetFramework="[^"]*"' "$PKG/yamldotnet.nuspec" | tr '\n' ' ')"
note "[s1-nuspec] dependency 항목 수: $(grep -c '<dependency ' "$PKG/yamldotnet.nuspec")"
note "[s1-lib] $(ls "$PKG/lib" | tr '\n' ' ')"
step s1-yamldotnet-refs "$SPIKE" tool refs "$PKG/lib/net10.0/YamlDotNet.dll"
step s1-yamldotnet-meta "$SPIKE" tool meta "$PKG/lib/net10.0/YamlDotNet.dll"
step s1-yamldotnet-users "$SPIKE" tool users "$PKG/lib/net10.0/YamlDotNet.dll"

# S2. Core 변형. 각 변형은 새 clone. CI와 같은 Release -warnaserror 빌드.
# 기준선: 변형 없음.
clone base
step s2-base-build "$WORK/base" dotnet build $SLN -c Release -warnaserror
step s2-base-g1 "$WORK/base" dotnet test $SLN --no-build -c Release --filter Rule=G1

variant() {
  local v="$1" src="$2" dest="$3"; shift 3
  clone "$v"
  step "$v-add-yaml" "$WORK/$v" dotnet add src/Scorecard.Core/Scorecard.Core.csproj package YamlDotNet --version $YAML_VERSION
  for extra in "$@"; do
    step "$v-add-$extra" "$WORK/$v" dotnet add src/Scorecard.Core/Scorecard.Core.csproj package "$extra" --version $YAML_VERSION
  done
  note "[$v-csproj] $(grep -E 'PackageReference|PrivateAssets|IncludeAssets' "$WORK/$v/src/Scorecard.Core/Scorecard.Core.csproj" | sed 's/^ *//' | tr '\n' ' ')"
  mkdir -p "$WORK/$v/src/Scorecard.Core/$dest"
  cp "$SPIKE/core/$src/"*.cs "$WORK/$v/src/Scorecard.Core/$dest/"
  step "$v-build" "$WORK/$v" dotnet build $SLN -c Release -warnaserror
  step "$v-meta-core" "$WORK/$v" tool meta tests/Scorecard.Tests/bin/Release/net10.0/Scorecard.Core.dll
  step "$v-refs-core" "$WORK/$v" tool refs tests/Scorecard.Tests/bin/Release/net10.0/Scorecard.Core.dll
  step "$v-g1-before-allow" "$WORK/$v" dotnet test $SLN --no-build -c Release --filter Rule=G1
  allow_yaml "$v"
  step "$v-build-allow" "$WORK/$v" dotnet build $SLN -c Release -warnaserror
  step "$v-g1-after-allow" "$WORK/$v" dotnet test $SLN --no-build -c Release --filter Rule=G1
}

# A: 리플렉션 역직렬화(DeserializerBuilder).
variant A-reflection A-reflection Spike
# B: 정적 생성기(StaticDeserializerBuilder + 소스 생성기).
variant B-static B-static Spike Vecc.YamlDotNet.Analyzers.StaticGenerator
# C: 저수준 Parser 이벤트 + 손으로 쓴 스키마 검사(후보).
variant C-events C-events Cases

note "[C-events test-bin] $(ls "$WORK/C-events/tests/Scorecard.Tests/bin/Release/net10.0" | grep -iE '^(yaml|scorecard)' | tr '\n' ' ')"
step C-events-meta-cli "$WORK/C-events" tool meta tests/Scorecard.Tests/bin/Release/net10.0/scorecard.dll
step C-events-test-all "$WORK/C-events" dotnet test $SLN --no-build -c Release -- RunConfiguration.TreatNoTestsAsError=true
step C-events-format "$WORK/C-events" dotnet format src/Scorecard.Core/Scorecard.Core.csproj --verify-no-changes

# S3. 표본 파싱. 오류 줄과 결과 해시.
for s in "$SPIKE"/samples/*/; do
  n="$(basename "$s")"
  step "s3-parse-$n" "$SPIKE" tool parse "samples/$n" "$WORK/s3-$n.json"
done

for n in case-valid case-invalid; do
  step "s3-parse-guard-$n" "$SPIKE" tool parse "tests-variant/guard/$n" "$WORK/s3-guard-$n.json"
done

# S4. 결정론: 프로세스를 따로, 문화권 de-DE, 다른 경로 사본.
for n in valid-full type-errors missing-required; do
  mkdir -p "$WORK/copy-elsewhere/deeper/$n"
  cp "$SPIKE/samples/$n/case.yaml" "$WORK/copy-elsewhere/deeper/$n/"
  step "s4-$n-run1" "$SPIKE" tool parse "samples/$n" "$WORK/s4-$n-1.json"
  step "s4-$n-run2" "$SPIKE" tool parse "samples/$n" "$WORK/s4-$n-2.json"
  step "s4-$n-de" "$SPIKE" tool parse "samples/$n" "$WORK/s4-$n-de.json" --culture de-DE
  step "s4-$n-moved" "$WORK" tool parse "copy-elsewhere/deeper/$n" "$WORK/s4-$n-moved.json"
  note "[s4-$n] sha256: $(cd "$WORK" && sha256sum s4-$n-1.json s4-$n-2.json s4-$n-de.json s4-$n-moved.json | cut -c1-16 | sort -u | tr '\n' ' ')"
done
step s4-probes "$SPIKE" tool probe samples
step s4-timing "$SPIKE" tool timing

# S5. G2·G3 등록 모양: C clone에 테스트 쪽 어댑터·샘플·빈 출력 규칙 패치를 넣는다.
GT="$WORK/C-events/tests/Scorecard.Tests"
cp "$SPIKE/tests-variant/CoreGuardTargets.cs" "$GT/Guards/"
cp -r "$SPIKE/tests-variant/guard/case-valid" "$SPIKE/tests-variant/guard/case-invalid" "$GT/TestData/guard/"
step s5-apply-patch "$WORK/C-events" git apply --verbose "$SPIKE/patches/empty-output.patch"
step s5-build "$WORK/C-events" dotnet build $SLN -c Release -warnaserror
step s5-g2-g3 "$WORK/C-events" dotnet test $SLN --no-build -c Release --filter "Rule=G2|Rule=G3" --logger "console;verbosity=normal"
step s5-test-all "$WORK/C-events" dotnet test $SLN --no-build -c Release -- RunConfiguration.TreatNoTestsAsError=true

# S6. 같은 어댑터가 출력을 쓰지 않으면 새 규칙이 잡는가(어댑터 한 줄을 빼 본다).
sed -i 's|        File.WriteAllBytes(|        _ = (|' "$GT/Guards/CoreGuardTargets.cs"
note "[s6-adapter] $(grep -n '_ = (' "$GT/Guards/CoreGuardTargets.cs" | sed 's/^ *//')"
step s6-build "$WORK/C-events" dotnet build $SLN -c Release
step s6-g2-g3 "$WORK/C-events" dotnet test $SLN --no-build -c Release --filter "Rule=G2|Rule=G3"

# S7. 파서가 오류 줄에 절대 경로를 쓰면 G2가 잡는가. 어댑터를 되돌리고 Core 한 줄을 바꾼다.
cp "$SPIKE/tests-variant/CoreGuardTargets.cs" "$GT/Guards/"
sed -i 's|return Parse(bytes, FileName);|return Parse(bytes, Path.GetFullPath(Path.Combine(caseDirectory, FileName)));|' \
  "$WORK/C-events/src/Scorecard.Core/Cases/CaseParser.cs"
note "[s7-core] $(grep -n 'GetFullPath' "$WORK/C-events/src/Scorecard.Core/Cases/CaseParser.cs" | sed 's/^ *//')"
step s7-build "$WORK/C-events" dotnet build $SLN -c Release
step s7-g2-g3 "$WORK/C-events" dotnet test $SLN --no-build -c Release --filter "Rule=G2|Rule=G3"

# S8. 제안 규칙 Y1의 카나리아: 테스트 프로젝트가 YamlDotNet 타입을 직접 참조할 수 있는가(Core 경유 전이 참조).
cp "$SPIKE/tests-variant/y1/YamlCanarySpike.cs" "$GT/Guards/"
step s8-build "$WORK/C-events" dotnet build $SLN -c Release -warnaserror
step s8-refs-tests "$WORK/C-events" tool refs tests/Scorecard.Tests/bin/Release/net10.0/Scorecard.Tests.dll

note "끝."
