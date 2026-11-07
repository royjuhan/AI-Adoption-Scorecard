#!/usr/bin/env bash
# T-005 스파이크: 합성 예제 생성기 후보를 Core clone에 넣고 G1·형식·결정론·예제 초안·실제 값 패턴 검사·G2/G3·훅을 확인한다.
# 머지 대상 아님. 저장소의 src/, tests/, examples/는 건드리지 않는다. 변형은 작업 폴더 안의 clone에만 넣는다.
# 작업 트리의 docs/spec/synthetic-examples.md를 읽기만 하고, clone에 복사해 쓴다.
# 아무것도 지우지 않는다(작업 폴더 안의 clone 파일 되돌리기는 git checkout으로 한다). 작업 폴더는 없거나 비어 있어야 한다.
# 네트워크는 NuGet 패키지 받기(restore)에만 쓴다.
# 사용: run-spike.sh <작업폴더> <로그폴더>
set -u

SPIKE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(git -C "$SPIKE" rev-parse --show-toplevel)"
WORK="${1:?작업 폴더를 주세요}"
LOGS="${2:?로그 폴더를 주세요}"
SLN=AIAdoptionScorecard.sln
SPEC_REL=docs/spec/synthetic-examples.md
EXAMPLES=(table-compare form-fill mixed-exceptions)
DATA_EXAMPLES=(table-compare form-fill)

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
# clone 안 커밋용(S9). 명령 줄에 남지 않게 환경 변수로 준다.
export GIT_AUTHOR_NAME=spike GIT_AUTHOR_EMAIL=spike@invalid GIT_COMMITTER_NAME=spike GIT_COMMITTER_EMAIL=spike@invalid

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
  grep -E '^(RESULT|PROBE|HIT|BLOCK|SHA|ERR|ASMREF|TYPEREF-NS) |warning [A-Z]+[0-9]+|error [A-Z]+[0-9]+|Passed!|Failed!|\[FAIL\]|^ +(N[1-6]|D1|R1|Y1|G[23]) |Assert\.|^ +(Expected|Actual):|^ +(email|url|windows-path|unc-path|posix-path|home-path|part-)|synthetic-case-valid|ExamplePatternTests|^Total tests:|^ +(Passed|Failed): |^(email|url|windows-path|unc-path|posix-path|home-path|part-[a-z-]+) |Unhandled' "$log" \
    | clean | awk '!seen[$0]++' | head -200 | sed 's/^/    /' | tee -a "$SUMMARY"
  return 0
}

ms() { date +%s%3N; }

C="$WORK/c"
note "dotnet SDK: $(dotnet --version)"
note "python: $(python3 --version 2>&1)"
note "repo HEAD: $(git -C "$REPO" rev-parse --short HEAD)"
note "spec(작업 트리) sha256: $(sha256sum "$REPO/$SPEC_REL" | cut -c1-16)"

# S0. clone + 스파이크 파일·스펙 복사.
git clone -q "$REPO" "$C"
note "[s0-clone] HEAD=$(git -C "$C" rev-parse --short HEAD)"
mkdir -p "$C/spikes/T-005"
cp -R "$SPIKE/." "$C/spikes/T-005/"
cp "$REPO/$SPEC_REL" "$C/$SPEC_REL"
SPEC="$C/$SPEC_REL"

# S1. 기준선.
step s1-base-build "$C" dotnet build $SLN -c Release -warnaserror
step s1-base-test "$C" dotnet test $SLN --no-build -c Release
cp "$C/src/Scorecard.Core/bin/Release/net10.0/Scorecard.Core.dll" "$WORK/base-Scorecard.Core.dll"

# S2. Core에 생성기 후보 4파일 → Release -warnaserror → G1 → 형식 → 전체 테스트.
mkdir -p "$C/src/Scorecard.Core/Synthetic"
cp "$SPIKE/core/Synthetic/"*.cs "$C/src/Scorecard.Core/Synthetic/"
step s2-core-build "$C" dotnet build $SLN -c Release -warnaserror
step s2-core-g1 "$C" dotnet test $SLN --no-build -c Release --filter Rule=G1
step s2-core-format "$C" dotnet format $SLN --verify-no-changes
step s2-core-test "$C" dotnet test $SLN --no-build -c Release

# S3. 도구 빌드 → 참조 → 예제 조립(answers.yaml·expected는 clone의 examples, case.yaml·intake.md는 초안) → 생성 → 확인.
step s3-tool-build "$C" dotnet build spikes/T-005/SynthSpike/SynthSpike.csproj -c Release --artifacts-path "$WORK/artifacts" -warnaserror
TOOLDLL="$WORK/artifacts/bin/SynthSpike/release/SynthSpike.dll"
tool() { dotnet "$TOOLDLL" "$@"; }
COREDLL="$C/src/Scorecard.Core/bin/Release/net10.0/Scorecard.Core.dll"
step s3-base-core-refs "$C" tool refs "$WORK/base-Scorecard.Core.dll"
step s3-core-refs "$C" tool refs "$COREDLL"
diff <(dotnet "$TOOLDLL" refs "$WORK/base-Scorecard.Core.dll") <(dotnet "$TOOLDLL" refs "$COREDLL") > "$LOGS/s3-refs-diff.log"
note "[s3-refs-diff] $(grep -c '^[<>]' "$LOGS/s3-refs-diff.log") 줄: $(grep '^[<>]' "$LOGS/s3-refs-diff.log" | tr '\n' ';')"

EX="$WORK/ex"
for e in "${EXAMPLES[@]}"; do
  mkdir -p "$EX/$e/expected"
  cp "$C/examples/$e/answers.yaml" "$EX/$e/answers.yaml"
  cp "$C/examples/$e/expected/classification.json" "$EX/$e/expected/classification.json"
  cp "$SPIKE/drafts/$e/case.yaml" "$SPIKE/drafts/$e/intake.md" "$EX/$e/"
done
for e in "${DATA_EXAMPLES[@]}"; do
  step "s3-gen-$e" "$C" tool gen "$SPEC" "$e" "$EX/$e" "$EX/$e"
done
for e in "${EXAMPLES[@]}"; do
  step "s3-case-check-$e" "$C" tool case-check "$EX/$e"
done
for e in "${DATA_EXAMPLES[@]}"; do
  step "s3-oracle-$e" "$C" tool oracle "$EX/$e"
  for f in "$EX/$e"/reference/*.csv "$EX/$e"/candidate/*.csv; do
    note "[s3-csv-check] $e/$(basename "$(dirname "$f")")/$(basename "$f") bytes=$(wc -c < "$f") lines=$(wc -l < "$f") cr=$(tr -cd '\r' < "$f" | wc -c) first3=$(head -c 3 "$f" | od -An -tx1 | tr -d ' ') last=$(tail -c 1 "$f" | od -An -tx1 | tr -d ' ') non-ascii=$(LC_ALL=C tr -d '\000-\177' < "$f" | wc -c)"
  done
done
for e in "${EXAMPLES[@]}"; do
  for f in case.yaml intake.md; do
    note "[s3-text-check] $e/$f bytes=$(wc -c < "$EX/$e/$f") cr=$(tr -cd '\r' < "$EX/$e/$f" | wc -c) first3=$(head -c 3 "$EX/$e/$f" | od -An -tx1 | tr -d ' ') last=$(tail -c 1 "$EX/$e/$f" | od -An -tx1 | tr -d ' ')"
  done
done
(cd "$EX" && find . -type f | LC_ALL=C sort | xargs sha256sum) > "$LOGS/s3-sha.txt"
note "[s3-sha] 예제 파일 SHA-256:"
sed 's/^/    /' "$LOGS/s3-sha.txt" | tee -a "$SUMMARY"

# 결정론: 다른 프로세스, 다른 경로(케이스 폴더를 더 깊은 곳에 복사), de-DE 프로세스, Python 독립 구현.
for e in "${DATA_EXAMPLES[@]}"; do
  mkdir -p "$WORK/moved/a/b/$e"
  cp "$EX/$e/case.yaml" "$WORK/moved/a/b/$e/case.yaml"
  step "s3-det-again-$e" "$C" tool gen "$SPEC" "$e" "$EX/$e" "$WORK/again/$e"
  step "s3-det-moved-$e" "$WORK" dotnet "$TOOLDLL" gen "$SPEC" "$e" "$WORK/moved/a/b/$e" "$WORK/moved-out/$e"
  step "s3-det-de-$e" "$C" env LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8 DOTNET_SYSTEM_GLOBALIZATION_PREDEFINED_CULTURES_ONLY=false dotnet "$TOOLDLL" gen "$SPEC" "$e" "$EX/$e" "$WORK/de/$e"
  step "s3-xcheck-$e" "$C" python3 -I "$C/spikes/T-005/xcheck/generate.py" "$SPEC" "$e" "$EX/$e/case.yaml" "$WORK/py/$e"
  for side in reference candidate; do
    f="$(cd "$EX/$e/$side" && ls *.csv)"
    r() { cmp -s "$EX/$e/$side/$f" "$1/$side/$f" && echo same || echo DIFF; }
    note "[s3-det-cmp] $e/$side/$f again: $(r "$WORK/again/$e") moved: $(r "$WORK/moved-out/$e") de: $(r "$WORK/de/$e") python: $(r "$WORK/py/$e")"
  done
done

# S4. 메모리 안 실험.
step s4-probe "$C" tool probe "$SPEC" "$EX"

# S5. G2·G3 등록: GuardTargets.cs 변형(생성기 대상 하나 추가) → G2·G3 → 전체 테스트.
cp "$SPIKE/tests-variant/GuardTargets.cs" "$C/tests/Scorecard.Tests/Guards/GuardTargets.cs"
step s5-build "$C" dotnet build $SLN -c Release -warnaserror
step s5-g2g3 "$C" dotnet test $SLN --no-build -c Release --filter "Rule=G2|Rule=G3" --logger "console;verbosity=normal"
step s5-test "$C" dotnet test $SLN --no-build -c Release

# S6. 음성 대조: 생성기가 소수를 현재 문화권으로 다시 읽으면(de-DE에서 '.'이 천 단위 구분) G2가 잡는가.
GEN="$C/src/Scorecard.Core/Synthetic/SyntheticGenerator.cs"
cp "$GEN" "$WORK/SyntheticGenerator.cs.orig"
sed -i 's/decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);/decimal.Parse(value, NumberStyles.Number, CultureInfo.CurrentCulture);/' "$GEN"
note "[s6-variant] 바뀐 줄 수: $(diff "$WORK/SyntheticGenerator.cs.orig" "$GEN" | grep -c '^>')"
step s6-build "$C" dotnet build $SLN -c Release -warnaserror
step s6-g1 "$C" dotnet test $SLN --no-build -c Release --filter Rule=G1
step s6-g2 "$C" dotnet test $SLN --no-build -c Release --filter "FullyQualifiedName~CoreDeterminismGuardTests" --logger "console;verbosity=normal"
cp "$WORK/SyntheticGenerator.cs.orig" "$GEN"
step s6-restore-build "$C" dotnet build $SLN -c Release -warnaserror

# S7. 예제 파일을 clone의 examples/에 넣고(.gitkeep 제외) 기존 테스트 → core.autocrlf=true clone 바이트.
for e in "${EXAMPLES[@]}"; do
  cp -R "$EX/$e/." "$C/examples/$e/"
done
note "[s7-gitkeep] clone examples/*/.gitkeep: $(ls "$C"/examples/*/.gitkeep 2>/dev/null | wc -l)개(지우지 않고 그대로 둔 채 테스트)"
step s7-test "$C" dotnet test $SLN --no-build -c Release
step s7-test-examples "$C" dotnet test $SLN --no-build -c Release --filter "Spec=classification&FullyQualifiedName~ExampleGolden" --logger "console;verbosity=normal"
E="$WORK/eol"
mkdir -p "$E/src/plain" "$E/src/attr"
for d in plain attr; do
  cp -R "$EX/table-compare/." "$E/src/$d/"
done
printf '* -text\n' > "$E/src/attr/.gitattributes"
git -C "$E/src" init -q
git -C "$E/src" add -A
git -C "$E/src" commit -q -m eol
git clone -q -c core.autocrlf=true "$E/src" "$E/crlf"
for d in plain attr; do
  for f in case.yaml intake.md reference/logs.csv candidate/logs.csv; do
    note "[s7-eol] $d/$f original=$(wc -c < "$E/src/$d/$f") autocrlf-clone=$(wc -c < "$E/crlf/$d/$f") same=$(cmp -s "$E/src/$d/$f" "$E/crlf/$d/$f" && echo yes || echo no)"
  done
done

# S8. 실제 값 패턴 검사: 도구로 예제·저장소 전체 → 테스트 변형(스펙 표 읽기) → 일부러 심은 값을 잡는가.
step s8-scan-examples "$C" tool scan "$SPEC" "$EX"
step s8-scan-repo "$C" tool scan "$SPEC" "$C" --counts
mkdir -p "$C/tests/Scorecard.Tests/Examples"
cp "$SPIKE/tests-variant/ExamplePatternTests.cs" "$C/tests/Scorecard.Tests/Examples/ExamplePatternTests.cs"
step s8-build "$C" dotnet build $SLN -c Release -warnaserror
step s8-format "$C" dotnet format $SLN --verify-no-changes
step s8-pattern-test "$C" dotnet test $SLN --no-build -c Release --filter "FullyQualifiedName~ExamplePatternTests" --logger "console;verbosity=normal"
INTAKE="$C/examples/form-fill/intake.md"
cp "$INTAKE" "$WORK/intake.md.orig"
printf '담당 someone@example.com\n파일은 C:\\Users\\someone\\orders 와 /home/someone/orders/ 에 있다.\n공유 \\\\server\\share 와 ~/orders 와 https://intranet.example/x 도 있다.\n품번 ABC-12345, 12-3456-789, A1B2C3D4.\n' >> "$INTAKE"
step s8-pattern-test-planted "$C" dotnet test $SLN --no-build -c Release --filter "FullyQualifiedName~ExamplePatternTests" --logger "console;verbosity=normal"
cp "$WORK/intake.md.orig" "$INTAKE"
step s8-pattern-test-restored "$C" dotnet test $SLN --no-build -c Release --filter "FullyQualifiedName~ExamplePatternTests"
step s8-test "$C" dotnet test $SLN --no-build -c Release

# S9. git 훅: core.hooksPath로 켠 pre-commit이 테스트를 돌려 커밋을 막는가, 걸린 시간.
mkdir -p "$C/.githooks"
cp "$SPIKE/hooks/pre-commit" "$C/.githooks/pre-commit"
chmod +x "$C/.githooks/pre-commit"
git -C "$C" config core.hooksPath .githooks
printf '담당 someone@example.com\n' >> "$INTAKE"
git -C "$C" add examples/form-fill/intake.md
t0=$(ms); git -C "$C" commit -q -m planted > "$LOGS/s9-commit-planted.log" 2>&1; rc=$?; t1=$(ms)
note "[s9-hook-planted] commit rc=$rc ms=$((t1 - t0)) HEAD=$(git -C "$C" rev-parse --short HEAD)"
grep -E 'Failed!|Passed!|email ' "$LOGS/s9-commit-planted.log" | clean | head -5 | sed 's/^/    /' | tee -a "$SUMMARY"
cp "$WORK/intake.md.orig" "$INTAKE"
git -C "$C" add -A examples
t0=$(ms); git -C "$C" commit -q -m clean > "$LOGS/s9-commit-clean.log" 2>&1; rc=$?; t1=$(ms)
note "[s9-hook-clean] commit rc=$rc ms=$((t1 - t0))"
grep -E 'Failed!|Passed!' "$LOGS/s9-commit-clean.log" | clean | head -3 | sed 's/^/    /' | tee -a "$SUMMARY"
printf '담당 someone@example.com\n' >> "$INTAKE"
git -C "$C" add examples/form-fill/intake.md
t0=$(ms); git -C "$C" commit -q --no-verify -m bypass > "$LOGS/s9-commit-bypass.log" 2>&1; rc=$?; t1=$(ms)
note "[s9-hook-bypass] commit --no-verify rc=$rc ms=$((t1 - t0)) (훅을 건너뛴다)"
git -C "$C" config --unset core.hooksPath
t0=$(ms); git -C "$C" commit -q --allow-empty -m nohook > "$LOGS/s9-commit-nohook.log" 2>&1; rc=$?; t1=$(ms)
note "[s9-hook-off] core.hooksPath 없이 commit rc=$rc ms=$((t1 - t0)) (훅이 안 돈다)"
cp "$WORK/intake.md.orig" "$INTAKE"

# S10. 스펙 대조: 7절 블록·SHA-256 = 생성 파일, 4.7 구조 표 = oracle.
step s10-blocks "$C" tool blocks "$SPEC" "$EX"
step s10-structure "$C" tool structure "$SPEC" "$EX"

note "끝."
