#!/usr/bin/env bash
# T-004 스파이크: 분류 트리 후보를 Core clone에 넣고 G1·형식·예제 초안·12.1 비교·결정론·G2/G3 등록을 확인한다.
# 머지 대상 아님. 저장소의 src/, tests/, examples/는 건드리지 않는다. 변형은 작업 폴더 안의 clone에만 넣는다.
# 아무것도 지우지 않는다. 작업 폴더는 없거나 비어 있어야 한다.
# 네트워크는 NuGet 패키지 받기(restore)에만 쓴다.
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

PKGROOT="$(dotnet nuget locals global-packages --list | sed 's/^global-packages: //')"

# 요약에는 절대 경로를 남기지 않는다.
clean() { sed "s|$WORK|<작업>|g; s|$LOGS|<로그>|g; s|$REPO|<저장소>|g; s|${PKGROOT%/}|<패키지>|g; s|${HOME:-<홈 없음>}|<홈>|g"; }
note() { echo "$*" | clean | tee -a "$SUMMARY"; }

# step <이름> <폴더> <명령...>: 종료 코드와 중요한 줄만 요약에 남긴다. 전체 출력은 <이름>.log.
step() {
  local name="$1" dir="$2"; shift 2
  local log="$LOGS/$name.log" rc
  ( cd "$dir" && "$@" ) > "$log" 2>&1
  rc=$?
  note "[$name] rc=$rc cmd: $*"
  grep -E '^(RESULT|ROW|PROBE|ERR|SHA|ASMREF|TYPEREF-NS) |warning [A-Z]+[0-9]+|error [A-Z]+[0-9]+|Passed!|Failed!|\[FAIL\]|^ +(N[1-6]|D1|R1|Y1|G[23]) |Assert\.|^ +(Expected|Actual):|^Collection: |classifier-case-valid' "$log" \
    | clean | awk '!seen[$0]++' | head -150 | sed 's/^/    /' | tee -a "$SUMMARY"
  return 0
}

C="$WORK/c"
note "dotnet SDK: $(dotnet --version)"
note "repo HEAD: $(git -C "$REPO" rev-parse --short HEAD)"

# S0. clone + 스파이크 파일 복사(작업 트리의 spikes/T-004).
git clone -q "$REPO" "$C"
note "[s0-clone] HEAD=$(git -C "$C" rev-parse --short HEAD)"
mkdir -p "$C/spikes/T-004"
cp -R "$SPIKE/." "$C/spikes/T-004/"

# S1. 기준선: 변형 없이 빌드·전체 테스트(통과 수).
step s1-base-build "$C" dotnet build $SLN -c Release -warnaserror
step s1-base-test "$C" dotnet test $SLN --no-build -c Release
cp "$C/src/Scorecard.Core/bin/Release/net10.0/Scorecard.Core.dll" "$WORK/base-Scorecard.Core.dll"

# S2. Core 변형: 분류 후보 3파일을 Core에 넣는다 → Release -warnaserror → G1 → 형식 → 참조.
mkdir -p "$C/src/Scorecard.Core/Classification"
cp "$SPIKE/core/Classification/"*.cs "$C/src/Scorecard.Core/Classification/"
step s2-core-build "$C" dotnet build $SLN -c Release -warnaserror
step s2-core-g1 "$C" dotnet test $SLN --no-build -c Release --filter Rule=G1
step s2-core-format "$C" dotnet format $SLN --verify-no-changes
step s2-core-test "$C" dotnet test $SLN --no-build -c Release

# S3. 스파이크 도구(clone 안, 산출물은 작업 폴더) → Core 참조 목록 → 예제 초안·12.1·guard 표본 분류.
step s3-tool-build "$C" dotnet build spikes/T-004/ClassifySpike/ClassifySpike.csproj -c Release --artifacts-path "$WORK/artifacts" -warnaserror
TOOLDLL="$WORK/artifacts/bin/ClassifySpike/release/ClassifySpike.dll"
tool() { dotnet "$TOOLDLL" "$@"; }
COREDLL="$C/src/Scorecard.Core/bin/Release/net10.0/Scorecard.Core.dll"
step s3-base-core-refs "$C" tool refs "$WORK/base-Scorecard.Core.dll"
step s3-core-refs "$C" tool refs "$COREDLL"

SAMPLES="$C/spikes/T-004/samples"
for s in table-compare form-fill mixed-exceptions table-compare-5steps; do
  step "s3-classify-$s" "$C" tool classify "$SAMPLES/$s/answers.yaml" "$LOGS/out/$s"
done
# 12.1 합성 표본(T-003 TestData)과 guard 정상 표본.
step s3-classify-example-12-1 "$C" tool classify "$C/tests/Scorecard.Tests/TestData/case-schema/example-12-1/case.yaml" "$LOGS/out/example-12-1"
step s3-classify-case-valid "$C" tool classify "$C/tests/Scorecard.Tests/TestData/guard/case-valid/case.yaml" "$LOGS/out/case-valid"

# 결정론: 다른 프로세스, 다른 경로 사본(이름은 같게), de-DE 환경 변수.
mkdir -p "$WORK/moved/deeper/table-compare"
cp "$SAMPLES/table-compare/answers.yaml" "$WORK/moved/deeper/table-compare/answers.yaml"
step s3-det-again "$C" tool classify "$SAMPLES/table-compare/answers.yaml" "$LOGS/out-again/table-compare"
step s3-det-moved "$C" tool classify "$WORK/moved/deeper/table-compare/answers.yaml" "$LOGS/out-moved/table-compare"
step s3-det-de "$C" env LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8 DOTNET_SYSTEM_GLOBALIZATION_PREDEFINED_CULTURES_ONLY=false dotnet "$TOOLDLL" classify "$SAMPLES/table-compare/answers.yaml" "$LOGS/out-de/table-compare"
note "[s3-det-cmp] same-process-run: $(cmp -s "$LOGS/out/table-compare/classification.json" "$LOGS/out-again/table-compare/classification.json" && echo same || echo DIFF) moved: $(cmp -s "$LOGS/out/table-compare/classification.json" "$LOGS/out-moved/table-compare/classification.json" && echo same || echo DIFF) de: $(cmp -s "$LOGS/out/table-compare/classification.json" "$LOGS/out-de/table-compare/classification.json" && echo same || echo DIFF)"
note "[s3-json-check] cr=$(grep -c $'\r' "$LOGS/out/table-compare/classification.json") last-byte=$(tail -c 1 "$LOGS/out/table-compare/classification.json" | od -An -tx1 | tr -d ' ') first-bytes=$(head -c 3 "$LOGS/out/table-compare/classification.json" | od -An -tx1 | tr -d ' ') lines=$(wc -l < "$LOGS/out/table-compare/classification.json")"
for s in table-compare form-fill mixed-exceptions; do
  note "[s3-json-lines] $s lines=$(wc -l < "$LOGS/out/$s/classification.json") bytes=$(wc -c < "$LOGS/out/$s/classification.json")"
done

# S4. 메모리 안 실험.
step s4-probe "$C" tool probe

# S5. G2·G3 등록: GuardTargets.cs를 변형으로 바꿔 분류 대상 하나를 더한다 → G2·G3 → 전체 테스트.
cp "$SPIKE/tests-variant/GuardTargets.cs" "$C/tests/Scorecard.Tests/Guards/GuardTargets.cs"
step s5-build "$C" dotnet build $SLN -c Release -warnaserror
step s5-g2g3 "$C" dotnet test $SLN --no-build -c Release --filter "Rule=G2|Rule=G3" --logger "console;verbosity=normal"
step s5-test "$C" dotnet test $SLN --no-build -c Release

# S6. 음성 대조: 분류 어댑터가 출력에 절대 경로를 섞으면 G2가 잡는가.
cp "$SPIKE/tests-variant/AbsPathGuardTargets.cs" "$C/tests/Scorecard.Tests/Guards/GuardTargets.cs"
step s6-build "$C" dotnet build $SLN -c Release -warnaserror
step s6-g2 "$C" dotnet test $SLN --no-build -c Release --filter "FullyQualifiedName~CoreDeterminismGuardTests"

# S7. 줄바꿈: core.autocrlf=true clone에서 examples 파일 바이트가 바뀌는가(.gitattributes 유무).
E="$WORK/eol"
mkdir -p "$E/src/plain/expected" "$E/src/attr/expected"
for d in plain attr; do
  cp "$LOGS/out/table-compare/classification.json" "$E/src/$d/expected/classification.json"
  cp "$SAMPLES/table-compare/answers.yaml" "$E/src/$d/answers.yaml"
done
printf '* -text\n' > "$E/src/attr/.gitattributes"
git -C "$E/src" init -q
git -C "$E/src" add -A
git -C "$E/src" -c user.name=spike -c user.email=spike@invalid commit -q -m eol
git clone -q -c core.autocrlf=true "$E/src" "$E/crlf"
for d in plain attr; do
  for f in expected/classification.json answers.yaml; do
    note "[s7-eol] $d/$f original=$(wc -c < "$E/src/$d/$f") autocrlf-clone=$(wc -c < "$E/crlf/$d/$f") same=$(cmp -s "$E/src/$d/$f" "$E/crlf/$d/$f" && echo yes || echo no)"
  done
done
step s7-classify-crlf "$C" tool classify "$E/crlf/plain/answers.yaml" "$LOGS/out-crlf/plain"
note "[s7-eol] classify(crlf answers.yaml) == classify(lf answers.yaml): $(cmp -s "$LOGS/out/table-compare/classification.json" "$LOGS/out-crlf/plain/classification.json" && echo same || echo DIFF)"

# S8. 스펙 표(작업 트리의 docs/spec/classification.md, 읽기만)를 후보와 대조한다.
step s8-spec "$C" tool spec "$REPO/docs/spec/classification.md"

note "끝."
