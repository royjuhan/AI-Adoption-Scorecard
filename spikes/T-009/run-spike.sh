#!/usr/bin/env bash
# T-009 스파이크: 4번째 공개 예제 signoff-final을 clone에 넣고 빌드·형식·전체 테스트, 바뀌어야 하는 기존 테스트,
# 기존 expected 6개 무변경, 블록 = 파일, C#·Python 교차 확인, 결정론, 음성 대조, core.autocrlf를 확인한다.
# 머지 대상 아님. 저장소의 src/, tests/, examples/, docs/README-instructions.md는 건드리지 않는다.
# 변형(예제 파일, 지시문 제안 문구, 테스트 변형, 음성 대조)은 작업 폴더 안의 clone에만 넣는다.
# 작업 트리의 docs/spec/ 문서 넷(classification.md, synthetic-examples.md, metrics.md, signoff-final.md)을 읽기만 하고 clone에 복사한다.
# 예제 파일은 clone에서 스펙 블록을 옮겨 만든다(extract-example.py). 엔진 출력을 복사하지 않는다.
# 아무것도 지우지 않는다. 작업 폴더는 없거나 비어 있어야 한다. 네트워크는 NuGet 패키지 받기(restore)에만 쓴다.
# S6의 임시 git 저장소는 작업 폴더 안에만 만든다(저장소에 커밋하지 않는다).
# 사용: run-spike.sh <작업폴더> <로그폴더>
set -u

SPIKE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(git -C "$SPIKE" rev-parse --show-toplevel)"
WORK="${1:?작업 폴더를 주세요}"
LOGS="${2:?로그 폴더를 주세요}"
SLN=AIAdoptionScorecard.sln
DOCS=(docs/spec/classification.md docs/spec/synthetic-examples.md docs/spec/metrics.md docs/spec/signoff-final.md)
OLD=(table-compare form-fill mixed-exceptions)
EX=signoff-final
SYN=docs/spec/synthetic-examples.md

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
  grep -E '^(RESULT|XCHECK|PROBE|SHA|ERR) |warning [A-Z]+[0-9]+|error [A-Z]+[0-9]+|Passed!|Failed!|^ +Failed [A-Z]|Unhandled' "$log" \
    | sed -E 's/ \[(< )?[0-9]+ (ms|s|m [0-9]+ s)\]$//; s/, Duration: .*$//' \
    | clean | awk '!seen[$0]++' | head -300 | sed 's/^/    /' | tee -a "$SUMMARY"
  return 0
}

# test <이름> <폴더>: 전체 테스트(trx). 통과하지 않은 테스트 이름을 요약에 남긴다.
run_tests() {
  local name="$1" dir="$2"
  step "$name" "$dir" dotnet test $SLN --no-build -c Release --logger "trx;LogFileName=$name.trx" --results-directory "$LOGS/trx" -- RunConfiguration.TreatNoTestsAsError=true
  python3 -I - "$LOGS/trx/$name.trx" <<'PY' | clean | sed 's/^/    /' | tee -a "$SUMMARY"
import sys, xml.etree.ElementTree as ET
ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
rs = list(ET.parse(sys.argv[1]).getroot().iterfind('.//t:UnitTestResult', ns))
bad = [u for u in rs if u.get('outcome') != 'Passed']
print(f'TESTS total={len(rs)} not-passed={len(bad)}')
for u in sorted(bad, key=lambda u: u.get('testName')):
    print(f"NOT-PASSED {u.get('outcome')} {u.get('testName')}")
PY
}

# 두 trx의 테스트 이름 차이(새로 생긴 것은 이름 무리별 수).
diff_tests() {
  python3 -I - "$LOGS/trx/$1.trx" "$LOGS/trx/$2.trx" <<'PY' | sed 's/^/    /' | tee -a "$SUMMARY"
import collections, re, sys, xml.etree.ElementTree as ET
ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
def names(p):
    return collections.Counter(u.get('testName') for u in ET.parse(p).getroot().iterfind('.//t:UnitTestResult', ns))
a, b = names(sys.argv[1]), names(sys.argv[2])
print(f'TESTS-DIFF before={sum(a.values())} after={sum(b.values())} gone={sum((a - b).values())} new={sum((b - a).values())}')
for n in sorted((a - b).elements()):
    print('GONE', n)
for k, v in sorted(collections.Counter(re.sub(r'\(.*', '', n) for n in (b - a).elements()).items()):
    print(f'NEW {v} {k}')
PY
}

C="$WORK/c"
T="spikes/T-009/SignoffFinalSpike/bin/Release/net10.0/SignoffFinalSpike"
note "dotnet SDK: $(dotnet --version)"
note "python: $(python3 --version 2>&1)"
note "repo HEAD: $(git -C "$REPO" rev-parse --short HEAD)"
for d in "${DOCS[@]}"; do
  note "spec(작업 트리) $d sha256: $(sha256sum "$REPO/$d" | cut -c1-16)"
done

# S0. clone + 스파이크 파일 복사. 문서는 S2에서 복사한다(S1은 HEAD 그대로).
git clone -q "$REPO" "$C"
note "[s0-clone] HEAD=$(git -C "$C" rev-parse --short HEAD)"
mkdir -p "$C/spikes/T-009"
cp -R "$SPIKE/." "$C/spikes/T-009/"

# S1. 기준선(HEAD). 카드: 1196.
step s1-restore "$C" dotnet restore $SLN
step s1-build "$C" dotnet build $SLN --no-restore -c Release -warnaserror
run_tests s1-test "$C"

# S2. 작업 트리 문서 → clone. 예제 파일 = 스펙 블록(SHA-256 줄 확인). 지시문 제안 문구.
for d in "${DOCS[@]}"; do cp "$REPO/$d" "$C/$d"; done
step s2-extract "$C" python3 -I spikes/T-009/extract-example.py .
step s2-instructions "$C" python3 -I spikes/T-009/instructions-variant.py .
step s2-tool-build "$C" dotnet build spikes/T-009/SignoffFinalSpike/SignoffFinalSpike.csproj -c Release -warnaserror

# S2 생성: 4.3·4.4 표 + 블록 case.yaml → Core 생성기. 블록 CSV와 바이트 비교. 시드 규칙, 시드 0~1999.
G="$WORK/gen/$EX"
mkdir -p "$G"
step s2-gen "$C" "$T" gen $SYN $EX examples/$EX/case.yaml "$G"
step s2-planted "$C" "$T" planted $SYN $EX examples/$EX/case.yaml
step s2-seed "$C" "$T" seed $SYN $EX examples/$EX/case.yaml record_kind unsteady 3
step s2-seeds "$C" "$T" seeds $SYN $EX examples/$EX/case.yaml
for f in reference/records.csv candidate/records.csv; do
  note "[s2-gen-cmp] $f block-vs-core: $(cmp -s "$C/examples/$EX/$f" "$G/$f" && echo same || echo DIFF)"
done

# S2 분류·대조: 블록 answers.yaml·case.yaml·CSV → main Core. 블록 expected와 바이트 비교.
O="$WORK/out"
step s2-classify-answers "$C" "$T" classify examples/$EX/answers.yaml "$O/answers/classification.json"
step s2-classify-case "$C" "$T" classify examples/$EX/case.yaml "$O/case/classification.json"
step s2-rows "$C" "$T" rows examples/$EX/answers.yaml
step s2-metrics "$C" "$T" metrics examples/$EX "$O/metrics.json"
note "[s2-cmp] classification answers.yaml block-vs-core: $(cmp -s "$C/examples/$EX/expected/classification.json" "$O/answers/classification.json" && echo same || echo DIFF)"
note "[s2-cmp] classification case.yaml block-vs-core: $(cmp -s "$C/examples/$EX/expected/classification.json" "$O/case/classification.json" && echo same || echo DIFF)"
note "[s2-cmp] metrics block-vs-core: $(cmp -s "$C/examples/$EX/expected/metrics.json" "$O/metrics.json" && echo same || echo DIFF)"

# S2 교차 확인(Python, C# 코드를 쓰지 않음): 생성기·분류·대조. 기존 예제 3종도 같은 스크립트로 확인한다.
P="$WORK/py"
step s2-xcheck-generate "$C" python3 -I spikes/T-009/xcheck/generate.py $SYN $EX examples/$EX/case.yaml "$P/gen/$EX"
for f in reference/records.csv candidate/records.csv; do
  note "[s2-xcheck-gen-cmp] $f block-vs-python: $(cmp -s "$C/examples/$EX/$f" "$P/gen/$EX/$f" && echo same || echo DIFF)"
done
for e in "${OLD[@]}" $EX; do
  step "s2-xcheck-classify-$e" "$C" python3 -I spikes/T-009/xcheck/classify.py docs/spec/classification.md docs/spec/case-schema.md examples/$e/answers.yaml "$P/classify/$e.json"
  note "[s2-xcheck-classify-cmp] $e block-vs-python: $(cmp -s "$C/examples/$e/expected/classification.json" "$P/classify/$e.json" && echo same || echo DIFF)"
  step "s2-xcheck-compare-$e" "$C" python3 -I spikes/T-009/xcheck/compare.py examples/$e "$P/compare/$e.json"
  note "[s2-xcheck-compare-cmp] $e block-vs-python: $(cmp -s "$C/examples/$e/expected/metrics.json" "$P/compare/$e.json" && echo same || echo DIFF)"
done
for e in table-compare form-fill; do
  step "s2-xcheck-generate-$e" "$C" python3 -I spikes/T-009/xcheck/generate.py $SYN $e examples/$e/case.yaml "$P/gen/$e"
  for f in $(cd "$C/examples/$e" && ls reference/*.csv candidate/*.csv); do
    note "[s2-xcheck-gen-cmp] $e/$f committed-vs-python: $(cmp -s "$C/examples/$e/$f" "$P/gen/$e/$f" && echo same || echo DIFF)"
  done
done

# S2 결정론: 다른 경로 사본, LANG=de_DE.UTF-8 프로세스(문화권을 바꾸지 않음).
mkdir -p "$WORK/other"
cp -R "$C/examples/$EX" "$WORK/other/$EX"
step s2-det-other-path "$C" "$T" metrics "$WORK/other/$EX" "$O/other/metrics.json"
step s2-det-other-path-classify "$C" "$T" classify "$WORK/other/$EX/answers.yaml" "$O/other/classification.json"
step s2-det-de "$C" env LANG=de_DE.UTF-8 SPIKE_KEEP_CULTURE=1 "$T" metrics examples/$EX "$O/de/metrics.json"
step s2-det-de-classify "$C" env LANG=de_DE.UTF-8 SPIKE_KEEP_CULTURE=1 "$T" classify examples/$EX/answers.yaml "$O/de/classification.json"
step s2-det-de-gen "$C" env LANG=de_DE.UTF-8 SPIKE_KEEP_CULTURE=1 "$T" gen $SYN $EX examples/$EX/case.yaml "$WORK/gen-de/$EX"
for f in other/metrics.json other/classification.json de/metrics.json de/classification.json; do
  base="$O/$(basename "$f")"; [ "$(basename "$f")" = classification.json ] && base="$O/answers/classification.json"
  note "[s2-det-cmp] $f: $(cmp -s "$base" "$O/$f" && echo same || echo DIFF)"
done
for f in reference/records.csv candidate/records.csv; do
  note "[s2-det-cmp] de gen $f: $(cmp -s "$G/$f" "$WORK/gen-de/$EX/$f" && echo same || echo DIFF)"
done

# S3a. 예제 + 문서 + 지시문 문구, HEAD 테스트 → 깨지는 기존 테스트 목록.
step s3a-build "$C" dotnet build $SLN --no-restore -c Release -warnaserror
run_tests s3a-test "$C"
diff_tests s1-test s3a-test

# S3c. 테스트 변형 V1~V5 + 새 테스트 N1(V6 없음) → 문턱 테스트만 깨지는지.
step s3c-variant "$C" python3 -I spikes/T-009/tests-variant/apply.py . --no-thresholds
step s3c-build "$C" dotnet build $SLN --no-restore -c Release -warnaserror
run_tests s3c-test "$C"

# S3b. V6까지 → 빌드·형식·전체 테스트·G1·G2·G3.
step s3b-variant "$C" python3 -I spikes/T-009/tests-variant/apply.py . --only-thresholds
step s3b-build "$C" dotnet build $SLN --no-restore -c Release -warnaserror
step s3b-format "$C" dotnet format $SLN --verify-no-changes
run_tests s3b-test "$C"
diff_tests s1-test s3b-test
step s3b-guards "$C" dotnet test $SLN --no-build -c Release --filter "Rule=G1|Rule=G2|Rule=G3|FullyQualifiedName~GuardTarget"

# S4. 기존 예제 3종과 expected 6개가 HEAD와 바이트까지 같다. clone의 바뀐 파일 목록.
for e in "${OLD[@]}"; do
  for f in expected/classification.json expected/metrics.json; do
    head_sha="$(git -C "$C" show "HEAD:examples/$e/$f" | sha256sum | cut -c1-64)"
    work_sha="$(sha256sum "$C/examples/$e/$f" | cut -c1-64)"
    note "[s4-expected] $e/$f head=${head_sha:0:16} work=${work_sha:0:16} $([ "$head_sha" = "$work_sha" ] && echo same || echo DIFF)"
  done
done
note "[s4-old-examples] git diff HEAD -- examples/{3종}: $(git -C "$C" diff --quiet HEAD -- examples/table-compare examples/form-fill examples/mixed-exceptions examples/.gitattributes && echo 'no change' || echo CHANGED)"
note "[s4-status] $(git -C "$C" status --short -- examples src tests docs | grep -v '^?? spikes' | tr '\n' ';')"
step s4-diff-stat "$C" git diff --stat HEAD -- docs tests src examples
note "[s4-src] git diff HEAD -- src: $(git -C "$C" diff --quiet HEAD -- src && echo 'no change' || echo CHANGED)"

# S5. 음성 대조. clone 사본마다 변형 하나 → 빌드 → 전체 테스트. 깨지는 테스트가 있어야 한다.
for m in gate-after-signoff step7-no seed-48; do
  M="$WORK/m-$m"
  cp -a "$C" "$M"
  step "s5-$m-mutate" "$M" python3 -I spikes/T-009/mutate.py . $m
  step "s5-$m-build" "$M" dotnet build $SLN --no-restore -c Release -warnaserror
  run_tests "s5-$m-test" "$M"
done

# S6. core.autocrlf=true clone에서 새 예제 파일 바이트(examples/.gitattributes 있음/없음). 임시 저장소는 작업 폴더 안.
for ga in with without; do
  S="$WORK/eol-src-$ga"
  mkdir -p "$S/examples"
  cp -R "$C/examples/$EX" "$S/examples/$EX"
  [ $ga = with ] && cp "$C/examples/.gitattributes" "$S/examples/.gitattributes"
  git -C "$S" init -q
  git -C "$S" add -A
  git -C "$S" -c user.name=spike -c user.email=spike@example.invalid -c core.autocrlf=false commit -q -m "spike eol $ga"
  git -c core.autocrlf=true clone -q "$S" "$WORK/eol-clone-$ga"
  for f in answers.yaml case.yaml intake.md reference/records.csv candidate/records.csv expected/classification.json expected/metrics.json; do
    note "[s6-eol] gitattributes=$ga $f bytes $(stat -c %s "$C/examples/$EX/$f") -> $(stat -c %s "$WORK/eol-clone-$ga/examples/$EX/$f") $(cmp -s "$C/examples/$EX/$f" "$WORK/eol-clone-$ga/examples/$EX/$f" && echo same || echo DIFF)"
  done
done

# S8. 블록 SHA-256 줄 = 파일(s2-extract가 확인). 파일 SHA-256 목록.
for f in answers.yaml case.yaml intake.md reference/records.csv candidate/records.csv expected/classification.json expected/metrics.json; do
  note "[s8-file] examples/$EX/$f sha256=$(sha256sum "$C/examples/$EX/$f" | cut -c1-64) bytes=$(stat -c %s "$C/examples/$EX/$f")"
done
note "[done]"
