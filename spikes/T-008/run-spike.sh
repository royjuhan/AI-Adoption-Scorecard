#!/usr/bin/env bash
# T-008 스파이크: human_signoff·SIGNOFF·inferred 후보를 Core clone에 넣고 빌드·형식·전체 테스트·G1·G2·G3,
# 예제 expected 바이트, 스펙 블록, 음성 대조를 확인한다.
# 머지 대상 아님. 저장소의 src/, tests/, examples/, docs/README-instructions.md는 건드리지 않는다.
# 변형(후보 Core, 테스트 변형, 지시문 제안 문구, 예제 human_signoff: no)은 작업 폴더 안의 clone에만 넣는다.
# 작업 트리의 docs/spec/ 세 문서(case-schema.md, classification.md, synthetic-examples.md)를 읽기만 하고 clone에 복사해 쓴다.
# 아무것도 지우지 않는다. 작업 폴더는 없거나 비어 있어야 한다. 네트워크는 NuGet 패키지 받기(restore)에만 쓴다.
# 사용: run-spike.sh <작업폴더> <로그폴더>
set -u

SPIKE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(git -C "$SPIKE" rev-parse --show-toplevel)"
WORK="${1:?작업 폴더를 주세요}"
LOGS="${2:?로그 폴더를 주세요}"
SLN=AIAdoptionScorecard.sln
DOCS=(docs/spec/case-schema.md docs/spec/classification.md docs/spec/synthetic-examples.md)
EXAMPLES=(table-compare form-fill mixed-exceptions)
CORE=src/Scorecard.Core
TESTS=tests/Scorecard.Tests
GUARD=$TESTS/TestData/guard

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
clean() { sed "s|$WORK|<작업>|g; s|$LOGS|<로그>|g; s|$REPO|<저장소>|g; s|${PKGROOT%/}|<패키지>|g; s|${HOME:-<홈 없음>}|<홈>|g; s|/tmp/scorecard-guard-[A-Za-z0-9]*|<임시>|g"; }
note() { echo "$*" | clean | tee -a "$SUMMARY"; }

# step <이름> <폴더> <명령...>: 종료 코드와 중요한 줄만 요약에 남긴다. 전체 출력은 <이름>.log.
step() {
  local name="$1" dir="$2"; shift 2
  local log="$LOGS/$name.log" rc
  ( cd "$dir" && "$@" ) > "$log" 2>&1
  rc=$?
  note "[$name] rc=$rc cmd: $*"
  grep -E '^(RESULT|PROBE|SHA|ERR|BLOCK) |warning [A-Z]+[0-9]+|error [A-Z]+[0-9]+|Passed!|Failed!|^ +Failed [A-Z]|^Total tests:|Unhandled' "$log" \
    | sed -E 's/ \[(< )?[0-9]+ (ms|s|m [0-9]+ s)\]$//; s/, Duration: .*$//' \
    | clean | awk '!seen[$0]++' | head -300 | sed 's/^/    /' | tee -a "$SUMMARY"
  return 0
}

C="$WORK/c"
note "dotnet SDK: $(dotnet --version)"
note "python: $(python3 --version 2>&1)"
note "repo HEAD: $(git -C "$REPO" rev-parse --short HEAD)"
for d in "${DOCS[@]}"; do
  note "spec(작업 트리) $d sha256: $(sha256sum "$REPO/$d" | cut -c1-16)"
done

# S0. clone + 스파이크 파일 복사. 문서는 S2b에서 복사한다(S1·S2a는 HEAD 문서).
git clone -q "$REPO" "$C"
note "[s0-clone] HEAD=$(git -C "$C" rev-parse --short HEAD)"
mkdir -p "$C/spikes/T-008"
cp -R "$SPIKE/." "$C/spikes/T-008/"

# S1. 기준선.
step s1-base-build "$C" dotnet build $SLN -c Release -warnaserror
step s1-base-test "$C" dotnet test $SLN --no-build -c Release

# S2a. 후보 Core 6파일 + HEAD 테스트 → 컴파일 오류. 컴파일만 고친 테스트 3파일 + HEAD 문서·예제 → 깨지는 테스트.
cp "$SPIKE/core/Cases/"*.cs "$C/$CORE/Cases/"
cp "$SPIKE/core/Classification/"*.cs "$C/$CORE/Classification/"
note "[s2-files] Cases: $(ls "$SPIKE/core/Cases" | tr '\n' ' ') Classification: $(ls "$SPIKE/core/Classification" | tr '\n' ' ')"
step s2a-head-tests-build "$C" dotnet build $SLN -c Release -warnaserror
note "[s2a-compile-errors] $(grep -oE '[A-Za-z]+\.cs\([0-9]+,[0-9]+\): error CS[0-9]+' "$LOGS/s2a-head-tests-build.log" | sort -u | tr '\n' ' ')"
cp "$SPIKE/tests-variant/CaseSchema/CaseApiShapeTests.cs" "$C/$TESTS/CaseSchema/"
cp "$SPIKE/tests-variant/Classification/ClassificationApiShapeTests.cs" "$C/$TESTS/Classification/"
python3 -I - "$C/$TESTS/Classification/ClassificationInput.cs" <<'PY'
import sys
p = sys.argv[1]
t = open(p, encoding="utf-8").read()
old = 'new("?", true), new(null, true), new("?", true), new("?", true));'
assert t.count(old) == 1
open(p, "w", encoding="utf-8", newline="\n").write(t.replace(old, 'new("?", true), new(null, true), new("?", true), new("?", true), new("?", true));'))
print("RESULT compile-fix ClassificationInput.cs start 10")
PY
step s2a-compile-fix-build "$C" dotnet build $SLN -c Release -warnaserror
step s2a-compile-fix-test "$C" dotnet test $SLN --no-build -c Release
note "[s2a-failed-count] $(grep -cE '^ +Failed [A-Z]' "$LOGS/s2a-compile-fix-test.log")"

# S2b. 테스트 변형 전부 + 개정 문서 + 지시문 제안 문구 + 예제 human_signoff: no
#      → Release -warnaserror → G1 → 형식 → 전체 테스트 → G2·G3.
cp "$SPIKE/tests-variant/CaseSchema/"*.cs "$C/$TESTS/CaseSchema/"
cp "$SPIKE/tests-variant/Classification/"*.cs "$C/$TESTS/Classification/"
note "[s2b-test-variants] $(cd "$SPIKE/tests-variant" && find . -name '*.cs' | LC_ALL=C sort | tr '\n' ' ')"
for d in "${DOCS[@]}"; do cp "$REPO/$d" "$C/$d"; done
step s2b-instructions "$C" python3 -I "$C/spikes/T-008/instructions-variant.py" "$C/docs/README-instructions.md"
for e in "${EXAMPLES[@]}"; do
  for f in answers.yaml case.yaml; do
    step "s2b-add-signoff-$e-$f" "$C" python3 -I "$C/spikes/T-008/add-signoff.py" "examples/$e/$f" "$WORK/tmp-$e-$f"
    cp "$WORK/tmp-$e-$f" "$C/examples/$e/$f"
  done
done
note "[s2b-changed-files] $(git -C "$C" status --short | grep -v '^?? spikes/' | tr '\n' ';')"
step s2b-build "$C" dotnet build $SLN -c Release -warnaserror
step s2b-g1 "$C" dotnet test $SLN --no-build -c Release --filter Rule=G1
step s2b-format "$C" dotnet format $SLN --verify-no-changes
step s2b-test "$C" dotnet test $SLN --no-build -c Release
step s2b-g2g3 "$C" dotnet test $SLN --no-build -c Release --filter "Rule=G2|Rule=G3"

# S3. 도구 빌드 → 예제 분류(answers.yaml, case.yaml)·대조 → expected와 cmp. HEAD answers.yaml과 비교. 결정론.
step s3-tool-build "$C" dotnet build spikes/T-008/SignoffSpike/SignoffSpike.csproj -c Release --artifacts-path "$WORK/artifacts" -warnaserror
TOOLDLL="$WORK/artifacts/bin/SignoffSpike/release/SignoffSpike.dll"
tool() { dotnet "$TOOLDLL" "$@"; }
O="$WORK/out"
for e in "${EXAMPLES[@]}"; do
  exp="$C/examples/$e/expected/classification.json"
  step "s3-classify-$e-answers" "$C" tool classify "examples/$e/answers.yaml" "$O/$e/answers.json"
  step "s3-classify-$e-case" "$C" tool classify "examples/$e/case.yaml" "$O/$e/case.json"
  step "s3-metrics-$e" "$C" tool metrics "examples/$e" "$O/$e/metrics.json"
  git -C "$REPO" show "HEAD:examples/$e/answers.yaml" > "$WORK/head-$e-answers.yaml"
  mkdir -p "$WORK/head/$e"
  cp "$WORK/head-$e-answers.yaml" "$WORK/head/$e/answers.yaml"
  step "s3-classify-$e-head-answers" "$WORK" tool classify "$WORK/head/$e/answers.yaml" "$O/$e/head-answers.json"
  mkdir -p "$WORK/moved/a/b/$e"
  cp "$C/examples/$e/answers.yaml" "$WORK/moved/a/b/$e/answers.yaml"
  step "s3-det-moved-$e" "$WORK" tool classify "$WORK/moved/a/b/$e/answers.yaml" "$O/$e/moved.json"
  step "s3-det-de-$e" "$C" env SPIKE_KEEP_CULTURE=1 LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8 DOTNET_SYSTEM_GLOBALIZATION_PREDEFINED_CULTURES_ONLY=false dotnet "$TOOLDLL" classify "examples/$e/answers.yaml" "$O/$e/de.json"
  r() { cmp -s "$1" "$2" && echo same || echo DIFF; }
  note "[s3-classify] $e answers=expected: $(r "$exp" "$O/$e/answers.json") case=expected: $(r "$exp" "$O/$e/case.json") metrics=expected: $(r "$C/examples/$e/expected/metrics.json" "$O/$e/metrics.json") moved: $(r "$exp" "$O/$e/moved.json") de: $(r "$exp" "$O/$e/de.json")"
  note "[s3-head-answers] $e HEAD answers.yaml → bytes=$(wc -c < "$O/$e/head-answers.json") (expected $(wc -c < "$exp")) differing-lines=$(diff <(cat "$exp") <(cat "$O/$e/head-answers.json") | grep -c '^>') default-mark-lines=$(grep -c '(기본값 사용, 확인 필요)' "$O/$e/head-answers.json")"
  for f in answers.yaml case.yaml; do
    note "[s3-sha] examples/$e/$f $(sha256sum "$C/examples/$e/$f" | cut -d' ' -f1) bytes=$(wc -c < "$C/examples/$e/$f") cr=$(tr -cd '\r' < "$C/examples/$e/$f" | wc -c)"
  done
  spec_sha="$(grep -E "^\`examples/$e/case.yaml\` \(SHA-256 \`" "$C/docs/spec/synthetic-examples.md" | sed -E 's/.*SHA-256 `([0-9a-f]+)`.*/\1/')"
  note "[s3-spec-sha] $e case.yaml spec=$(echo "$spec_sha" | cut -c1-16) file=$(sha256sum "$C/examples/$e/case.yaml" | cut -c1-16) $( [ "$spec_sha" = "$(sha256sum "$C/examples/$e/case.yaml" | cut -d' ' -f1)" ] && echo same || echo DIFF)"
  step "s3-case-in-answers-$e" "$C" python3 -I -c "import sys; a=open(sys.argv[1],'rb').read(); c=open(sys.argv[2],'rb').read(); i=a.index(b'\nname: ')+1; print('RESULT case-in-answers', sys.argv[3], a[i:] in c)" "examples/$e/answers.yaml" "examples/$e/case.yaml" "$e"
done
note "[s3-examples-diff] $(git -C "$C" diff --stat -- examples | tail -1)"
git -C "$C" diff -- examples > "$LOGS/s3-examples.diff"
note "[s3-examples-diff-lines] +$(grep -c '^+[^+]' "$LOGS/s3-examples.diff") -$(grep -c '^-[^-]' "$LOGS/s3-examples.diff") non-answers-line-changes=$(grep -E '^[+-][^+-]' "$LOGS/s3-examples.diff" | grep -vc '^[+-]    answers: {')"
note "[s3-csv-intake] $(git -C "$C" status --short -- examples | grep -vE 'answers.yaml|case.yaml' | wc -l) other example files changed"

# S4. 메모리 안 실험, 12.1 합성 사본(7단계 yes), guard case-valid.
step s4-probe "$C" tool probe
step s4-example-12-1-head "$C" tool rows "$TESTS/TestData/case-schema/example-12-1/case.yaml"
mkdir -p "$WORK/e121"
step s4-example-12-1-signoff-add "$C" python3 -I "$C/spikes/T-008/add-signoff.py" "$TESTS/TestData/case-schema/example-12-1/case.yaml" "$WORK/e121/case.yaml" 7
step s4-example-12-1-signoff "$C" tool rows "$WORK/e121/case.yaml"
step s4-guard-case-valid "$C" tool rows "$GUARD/case-valid/case.yaml"

# S5. 음성 대조: 후보 StepClassifier에 변형 하나씩 → 빌드 → 전체 테스트 → 잡은 테스트. 끝에 되돌려 다시 전체·G2·G3.
for m in gate-after-signoff signoff-after-q1 inferred-before-default signoff-reads-fi; do
  cp "$SPIKE/core/Classification/StepClassifier.cs" "$C/$CORE/Classification/StepClassifier.cs"
  step "s5-mut-$m-apply" "$C" python3 -I "$C/spikes/T-008/mutate.py" "$C/$CORE/Classification/StepClassifier.cs" "$m"
  step "s5-mut-$m-build" "$C" dotnet build $SLN -c Release -warnaserror
  step "s5-mut-$m-test" "$C" dotnet test $SLN --no-build -c Release
  note "[s5-mut-$m] failed=$(grep -cE '^ +Failed [A-Z]' "$LOGS/s5-mut-$m-test.log")"
done
cp "$SPIKE/core/Classification/StepClassifier.cs" "$C/$CORE/Classification/StepClassifier.cs"
step s5-restore-build "$C" dotnet build $SLN -c Release -warnaserror
step s5-restore-test "$C" dotnet test $SLN --no-build -c Release
step s5-g2g3 "$C" dotnet test $SLN --no-build -c Release --filter "Rule=G2|Rule=G3"

# S8. 스펙 블록: case-schema 6.7(오류 표본 → 오류 줄), classification 6.5(서명·추정 표본 → 행 줄). JSON 차이.
S8="$WORK/s8"
mkdir -p "$S8/inferred-errors" "$S8/signoff-inferred" "$S8/signoff-inferred-noinf"
step s8-extract-6.7-yaml "$C" python3 -I "$C/spikes/T-008/extract-block.py" docs/spec/case-schema.md "### 6.7 추정 답 목록 오류 (T-008)" 0 "$S8/inferred-errors/case.yaml"
step s8-extract-6.7-errors "$C" python3 -I "$C/spikes/T-008/extract-block.py" docs/spec/case-schema.md "### 6.7 추정 답 목록 오류 (T-008)" 1 "$S8/inferred-errors.expected"
step s8-inferred-errors "$C" tool errors "$S8/inferred-errors/case.yaml" "$S8/inferred-errors.actual"
note "[s8-inferred-errors] spec block = parser: $(cmp -s "$S8/inferred-errors.expected" "$S8/inferred-errors.actual" && echo same || echo DIFF)"
step s8-extract-6.5-yaml "$C" python3 -I "$C/spikes/T-008/extract-block.py" docs/spec/classification.md "### 6.5 서명 단계와 추정 표시 (T-008)" 0 "$S8/signoff-inferred/answers.yaml"
step s8-extract-6.5-rows "$C" python3 -I "$C/spikes/T-008/extract-block.py" docs/spec/classification.md "### 6.5 서명 단계와 추정 표시 (T-008)" 1 "$S8/signoff-inferred.expected"
step s8-signoff-inferred "$C" tool spec-rows "$S8/signoff-inferred/answers.yaml" "$S8/signoff-inferred.actual"
note "[s8-signoff-inferred] spec block = classifier: $(cmp -s "$S8/signoff-inferred.expected" "$S8/signoff-inferred.actual" && echo same || echo DIFF)"
step s8-signoff-inferred-rows "$C" tool rows "$S8/signoff-inferred/answers.yaml"
grep -v '^    inferred:' "$S8/signoff-inferred/answers.yaml" > "$S8/signoff-inferred-noinf/answers.yaml"
step s8-json-inferred "$C" tool classify "$S8/signoff-inferred/answers.yaml" "$S8/with.json"
step s8-json-noinf "$C" tool classify "$S8/signoff-inferred-noinf/answers.yaml" "$S8/without.json"
diff "$S8/without.json" "$S8/with.json" > "$LOGS/s8-json.diff"
note "[s8-json-diff] differing-lines=$(grep -c '^>' "$LOGS/s8-json.diff") all-reason=$( [ "$(grep '^>' "$LOGS/s8-json.diff" | grep -vc '"reason": ')" = 0 ] && echo yes || echo no) has-inferred-key=$(grep -c '"inferred"' "$S8/with.json")"
sed 's/^/    /' "$LOGS/s8-json.diff" | tee -a "$SUMMARY" > /dev/null

note "[done] summary=summary.txt"
