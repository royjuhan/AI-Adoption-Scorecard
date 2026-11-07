#!/usr/bin/env bash
# T-006 스파이크: 대조 엔진 후보를 Core clone에 넣고 G1·형식·예제 지표·결정론·CSV 변형·G2/G3·음성 대조·스펙 표를 확인한다.
# 머지 대상 아님. 저장소의 src/, tests/, examples/는 건드리지 않는다. 변형은 작업 폴더 안의 clone에만 넣는다.
# 작업 트리의 docs/spec/metrics.md를 읽기만 하고, clone에 복사해 쓴다.
# 아무것도 지우지 않는다(clone 파일 되돌리기는 복사해 둔 원본으로 한다). 작업 폴더는 없거나 비어 있어야 한다.
# 네트워크는 NuGet 패키지 받기(restore)에만 쓴다.
# 사용: run-spike.sh <작업폴더> <로그폴더>
set -u

SPIKE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(git -C "$SPIKE" rev-parse --show-toplevel)"
WORK="${1:?작업 폴더를 주세요}"
LOGS="${2:?로그 폴더를 주세요}"
SLN=AIAdoptionScorecard.sln
SPEC_REL=docs/spec/metrics.md
EXAMPLES=(table-compare form-fill mixed-exceptions)
GUARD=tests/Scorecard.Tests/TestData/guard

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
export GIT_AUTHOR_NAME=spike GIT_AUTHOR_EMAIL=spike@invalid GIT_COMMITTER_NAME=spike GIT_COMMITTER_EMAIL=spike@invalid

PKGROOT="$(dotnet nuget locals global-packages --list | sed 's/^global-packages: //')"

# 요약에는 절대 경로를 남기지 않는다.
clean() { sed "s|$WORK|<작업>|g; s|$LOGS|<로그>|g; s|$REPO|<저장소>|g; s|${PKGROOT%/}|<패키지>|g; s|${HOME:-<홈 없음>}|<홈>|g; s|/tmp/scorecard-guard-[A-Za-z0-9]*|<임시>|g"; }
note() { echo "$*" | clean | tee -a "$SUMMARY"; }

# step <이름> <폴더> <명령...>: 종료 코드와 중요한 줄만 요약에 남긴다. 전체 출력은 <이름>.log.
step() {
  local name="$1" dir="$2"; shift 2
  local log="$LOGS/$name.log" rc
  ( cd "$dir" && "$@" ) > "$log" 2>&1
  rc=$?
  note "[$name] rc=$rc cmd: $*"
  grep -E '^(RESULT|PROBE|SHA|ERR|ASMREF|TYPEREF-NS|SPEC|SPEC-DIFF|BLOCK|TIMING) |warning [A-Z]+[0-9]+|error [A-Z]+[0-9]+|Passed!|Failed!|\[FAIL\]|^ +(N[1-6]|D1|R1|Y1) |^ +G[23] |Assert\.|^ +(Expected|Actual):|compare-case-valid|compare-invalid|Real_targets|Example_folder_has|^Total tests:|Unhandled' "$log" \
    | clean | awk '!seen[$0]++' | head -300 | sed 's/^/    /' | tee -a "$SUMMARY"
  return 0
}

C="$WORK/c"
note "dotnet SDK: $(dotnet --version)"
note "python: $(python3 --version 2>&1)"
note "repo HEAD: $(git -C "$REPO" rev-parse --short HEAD)"
note "spec(작업 트리) sha256: $(sha256sum "$REPO/$SPEC_REL" | cut -c1-16)"

# S0. clone + 스파이크 파일·스펙 복사.
git clone -q "$REPO" "$C"
note "[s0-clone] HEAD=$(git -C "$C" rev-parse --short HEAD)"
mkdir -p "$C/spikes/T-006"
cp -R "$SPIKE/." "$C/spikes/T-006/"
cp "$REPO/$SPEC_REL" "$C/$SPEC_REL"
SPEC="$C/$SPEC_REL"

# S1. 기준선.
step s1-base-build "$C" dotnet build $SLN -c Release -warnaserror
step s1-base-test "$C" dotnet test $SLN --no-build -c Release
cp "$C/src/Scorecard.Core/bin/Release/net10.0/Scorecard.Core.dll" "$WORK/base-Scorecard.Core.dll"

# S2. Core에 대조 후보 6파일 → Release -warnaserror → G1 → 형식 → 전체 테스트.
mkdir -p "$C/src/Scorecard.Core/Comparison"
cp "$SPIKE/core/Comparison/"*.cs "$C/src/Scorecard.Core/Comparison/"
note "[s2-files] $(ls "$C/src/Scorecard.Core/Comparison" | tr '\n' ' ')"
step s2-core-build "$C" dotnet build $SLN -c Release -warnaserror
step s2-core-g1 "$C" dotnet test $SLN --no-build -c Release --filter Rule=G1
step s2-core-format "$C" dotnet format $SLN --verify-no-changes
step s2-core-test "$C" dotnet test $SLN --no-build -c Release

# S3. 도구 빌드 → Core 참조 비교 → 예제 3종 대조 → 바이트 확인 → 결정론 → Python 독립 구현.
step s3-tool-build "$C" dotnet build spikes/T-006/CompareSpike/CompareSpike.csproj -c Release --artifacts-path "$WORK/artifacts" -warnaserror
TOOLDLL="$WORK/artifacts/bin/CompareSpike/release/CompareSpike.dll"
tool() { dotnet "$TOOLDLL" "$@"; }
COREDLL="$C/src/Scorecard.Core/bin/Release/net10.0/Scorecard.Core.dll"
step s3-base-core-refs "$C" tool refs "$WORK/base-Scorecard.Core.dll"
step s3-core-refs "$C" tool refs "$COREDLL"
diff <(dotnet "$TOOLDLL" refs "$WORK/base-Scorecard.Core.dll") <(dotnet "$TOOLDLL" refs "$COREDLL") > "$LOGS/s3-refs-diff.log"
note "[s3-refs-diff] $(grep -c '^[<>]' "$LOGS/s3-refs-diff.log") 줄: $(grep '^[<>]' "$LOGS/s3-refs-diff.log" | tr '\n' ';')"

EX="$WORK/ex"
for e in "${EXAMPLES[@]}"; do
  step "s3-run-$e" "$C" tool run "examples/$e" "$EX/$e/expected/metrics.json"
done
for e in "${EXAMPLES[@]}"; do
  f="$EX/$e/expected/metrics.json"
  note "[s3-json-check] $e bytes=$(wc -c < "$f") lines=$(wc -l < "$f") cr=$(tr -cd '\r' < "$f" | wc -c) first3=$(head -c 3 "$f" | od -An -tx1 | tr -d ' ') last=$(tail -c 1 "$f" | od -An -tx1 | tr -d ' ') non-ascii=$(LC_ALL=C tr -d '\000-\177' < "$f" | wc -c)"
done
(cd "$EX" && find . -type f | LC_ALL=C sort | xargs sha256sum) > "$LOGS/s3-sha.txt"
note "[s3-sha] metrics.json SHA-256:"
sed 's/^/    /' "$LOGS/s3-sha.txt" | tee -a "$SUMMARY"

for e in "${EXAMPLES[@]}"; do
  mkdir -p "$WORK/moved/a/b"
  cp -R "$C/examples/$e" "$WORK/moved/a/b/$e"
  step "s3-det-again-$e" "$C" tool run "examples/$e" "$WORK/again/$e.json"
  step "s3-det-moved-$e" "$WORK" dotnet "$TOOLDLL" run "$WORK/moved/a/b/$e" "$WORK/moved-out/$e.json"
  step "s3-det-de-$e" "$C" env SPIKE_KEEP_CULTURE=1 LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8 DOTNET_SYSTEM_GLOBALIZATION_PREDEFINED_CULTURES_ONLY=false dotnet "$TOOLDLL" run "examples/$e" "$WORK/de/$e.json"
  step "s3-xcheck-$e" "$C" python3 -I "$C/spikes/T-006/xcheck/compare.py" "examples/$e" "$WORK/py/$e.json"
  r() { cmp -s "$EX/$e/expected/metrics.json" "$1" && echo same || echo DIFF; }
  note "[s3-det-cmp] $e again: $(r "$WORK/again/$e.json") moved: $(r "$WORK/moved-out/$e.json") de: $(r "$WORK/de/$e.json") python: $(r "$WORK/py/$e.json")"
done

# S4. 메모리 안 실험.
mkdir -p "$WORK/probe"
step s4-probe "$C" tool probe examples "$WORK/probe"

# S5. G2·G3 등록: 샘플(case-valid에 CSV 2개, compare-invalid 새 폴더) + GuardTargets 변형.
#     먼저 기존 CoreGuardTargetTests 그대로(Real_targets_use_case_samples_not_basic가 깨지는지), 다음에 변형.
step s5-guard-samples "$C" tool guard-samples "$GUARD/case-valid"
cp -R "$SPIKE/samples/guard/compare-invalid" "$C/$GUARD/compare-invalid"
cp "$SPIKE/tests-variant/GuardTargets.cs" "$C/tests/Scorecard.Tests/Guards/GuardTargets.cs"
step s5a-build "$C" dotnet build $SLN -c Release -warnaserror
step s5a-test "$C" dotnet test $SLN --no-build -c Release
cp "$SPIKE/tests-variant/CoreGuardTargetTests.cs" "$C/tests/Scorecard.Tests/Guards/CoreGuardTargetTests.cs"
step s5b-build "$C" dotnet build $SLN -c Release -warnaserror
step s5b-format "$C" dotnet format $SLN --verify-no-changes
step s5b-g2g3 "$C" dotnet test $SLN --no-build -c Release --filter "Rule=G2|Rule=G3" --logger "console;verbosity=normal"
step s5b-test "$C" dotnet test $SLN --no-build -c Release
mkdir -p "$EX/guard"
cp -R "$SPIKE/samples/guard/compare-invalid" "$EX/guard/sample"
step s5-run-case-valid "$C" tool run "$GUARD/case-valid" "$EX/guard/case-valid-metrics.json"
step s5-run-compare-invalid "$C" tool run "$GUARD/compare-invalid" "$EX/guard/compare-invalid.txt"

# S6. 음성 대조. (a) 숫자를 현재 문화권으로 읽음 → G1 → G2. (b) 입력 파일 경로를 절대 경로로 → G2.
RULES="$C/src/Scorecard.Core/Comparison/CompareRules.cs"
INPUTS="$C/src/Scorecard.Core/Comparison/CompareInputs.cs"
cp "$RULES" "$WORK/CompareRules.cs.orig"
cp "$INPUTS" "$WORK/CompareInputs.cs.orig"
sed -i 's/decimal.TryParse(normalized, NumberStyle, CultureInfo.InvariantCulture, out value)/decimal.TryParse(normalized, NumberStyle, CultureInfo.CurrentCulture, out value)/' "$RULES"
note "[s6a-variant] 바뀐 줄 수: $(diff "$WORK/CompareRules.cs.orig" "$RULES" | grep -c '^>')"
step s6a-build "$C" dotnet build $SLN -c Release -warnaserror
step s6a-g1 "$C" dotnet test $SLN --no-build -c Release --filter Rule=G1
step s6a-g2 "$C" dotnet test $SLN --no-build -c Release --filter "FullyQualifiedName~CoreDeterminismGuardTests" --logger "console;verbosity=normal"
cp "$WORK/CompareRules.cs.orig" "$RULES"
sed -i 's/files.Add(new SourceFile(path, File.ReadAllBytes(Path.Combine(folder, name))));/files.Add(new SourceFile(Path.Combine(folder, name), File.ReadAllBytes(Path.Combine(folder, name))));/' "$INPUTS"
note "[s6b-variant] 바뀐 줄 수: $(diff "$WORK/CompareInputs.cs.orig" "$INPUTS" | grep -c '^>')"
step s6b-build "$C" dotnet build $SLN -c Release -warnaserror
step s6b-g1 "$C" dotnet test $SLN --no-build -c Release --filter Rule=G1
step s6b-g2 "$C" dotnet test $SLN --no-build -c Release --filter "FullyQualifiedName~CoreDeterminismGuardTests" --logger "console;verbosity=normal"
cp "$WORK/CompareInputs.cs.orig" "$INPUTS"
step s6-restore-build "$C" dotnet build $SLN -c Release -warnaserror

# S7. metrics.json을 clone examples/*/expected/에 넣고 기존 테스트 → 폴더 파일 목록 테스트 변형 → core.autocrlf.
for e in "${EXAMPLES[@]}"; do
  cp "$EX/$e/expected/metrics.json" "$C/examples/$e/expected/metrics.json"
done
step s7a-test "$C" dotnet test $SLN --no-build -c Release
EFT="$C/tests/Scorecard.Tests/Synthetic/ExampleFileTests.cs"
cp "$EFT" "$WORK/ExampleFileTests.cs.orig"
sed -i 's/var expected = SyntheticInput.ClassificationFiles.Concat(SyntheticSpec.FilesOf(example)).Order(StringComparer.Ordinal);/var expected = SyntheticInput.ClassificationFiles.Concat(SyntheticSpec.FilesOf(example)).Append("expected\/metrics.json").Order(StringComparer.Ordinal);/' "$EFT"
note "[s7b-variant] ExampleFileTests 바뀐 줄 수: $(diff "$WORK/ExampleFileTests.cs.orig" "$EFT" | grep -c '^>')"
step s7b-build "$C" dotnet build $SLN -c Release -warnaserror
step s7b-test "$C" dotnet test $SLN --no-build -c Release
step s7b-pattern "$C" dotnet test $SLN --no-build -c Release --filter "FullyQualifiedName~ExamplePatternTests|FullyQualifiedName~Example_text_files" --logger "console;verbosity=normal"
E="$WORK/eol"
mkdir -p "$E/src/plain" "$E/src/attr"
for d in plain attr; do
  cp "$EX/table-compare/expected/metrics.json" "$E/src/$d/metrics.json"
done
printf '* -text\n' > "$E/src/attr/.gitattributes"
git -C "$E/src" init -q
git -C "$E/src" add -A
git -C "$E/src" commit -q -m eol
git clone -q -c core.autocrlf=true "$E/src" "$E/crlf"
for d in plain attr; do
  note "[s7-eol] $d/metrics.json original=$(wc -c < "$E/src/$d/metrics.json") autocrlf-clone=$(wc -c < "$E/crlf/$d/metrics.json") same=$(cmp -s "$E/src/$d/metrics.json" "$E/crlf/$d/metrics.json" && echo yes || echo no)"
done

# S8. 스펙 대조: 표(패턴·이름·숫자·허용 오차·앞뒤 글자·퍼센트·오류 코드·예제·속성), 7절 블록·SHA-256, 6절 오류 블록.
step s8-spec "$C" tool spec "$SPEC" "$EX"

note "끝."
