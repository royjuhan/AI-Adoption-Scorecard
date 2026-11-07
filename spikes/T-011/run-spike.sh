#!/usr/bin/env bash
# T-011 스파이크: 판정 후보 Core(Scorecard.Core.Judgment)를 clone에 넣고 빌드·형식·전체 테스트, 예제 4종 판정,
# 결정론·문화권, 경계 PROBE, G2·G3 등록, 음성 대조, expected/judgment.json 추가와 기존 expected 무변경,
# 안 M-A(case.yaml inputs.group)의 영향, 스펙 표 대조, Python 독립 구현 교차 확인을 한다.
# 머지 대상 아님. 저장소의 src/, tests/, examples/, docs/는 건드리지 않는다(문서는 읽기만 하고 clone에 복사한다).
# 변형은 작업 폴더 안의 clone에만 넣는다. 아무것도 지우지 않는다. 작업 폴더는 없거나 비어 있어야 한다.
# 네트워크는 NuGet 패키지 받기(restore)에만 쓴다.
# 사용: run-spike.sh <작업폴더> <로그폴더>
set -u

SPIKE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(git -C "$SPIKE" rev-parse --show-toplevel)"
WORK="${1:?작업 폴더를 주세요}"
LOGS="${2:?로그 폴더를 주세요}"
SLN=AIAdoptionScorecard.sln
DOC=docs/spec/judgment.md
EX=(table-compare form-fill mixed-exceptions signoff-final)
GUARD=tests/Scorecard.Tests/TestData/guard/case-valid

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
  grep -E '^(RESULT|XCHECK|PROBE|DIFF|BLOCK|PATCH|VARIANT|NEGATIVE|ERR) |warning [A-Z]+[0-9]+|error [A-Z]+[0-9]+|Passed!|Failed!|^ +Failed [A-Z]|Unhandled' "$log" \
    | sed -E 's/ \[(< )?[0-9]+ (ms|s|m [0-9]+ s)\]$//; s/, Duration: .*$//' \
    | clean | awk '!seen[$0]++' | head -300 | sed 's/^/    /' | tee -a "$SUMMARY"
  return 0
}

# run_tests <이름> <폴더>: 전체 테스트(trx). 통과하지 않은 테스트 이름을 요약에 남긴다.
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

# diff_tests <이전> <이후>: 두 trx의 테스트 이름 차이(새로 생긴 것은 이름 무리별 수).
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

# fresh <폴더>: HEAD clone + 스파이크 파일 + 후보 Core.
fresh() {
  git clone -q "$REPO" "$1"
  mkdir -p "$1/spikes/T-011"
  cp -R "$SPIKE/." "$1/spikes/T-011/"
  cp -R "$SPIKE/core/Judgment" "$1/src/Scorecard.Core/"
}

# expected_unchanged <폴더>: clone의 examples/ 기존 파일이 HEAD와 바이트까지 같은지(새 파일은 따로 센다).
expected_unchanged() {
  local changed added expected
  changed="$(git -C "$1" status --porcelain -- examples | grep -v '^??' | sed 's/^...//' | tr '\n' ' ')"
  expected="$(git -C "$1" status --porcelain -- 'examples/*/expected' | grep -vc '^??')"
  added="$(git -C "$1" status --porcelain --untracked-files=all -- examples | grep '^??' | sed 's/^?? //' | tr '\n' ' ')"
  note "[$2] 기존 expected 변경=$expected 기존 파일 변경=${changed:-없음} 새 파일=${added:-없음}"
}

C="$WORK/c"
T="spikes/T-011/JudgmentSpike/bin/Release/net10.0/JudgmentSpike"
note "dotnet SDK: $(dotnet --version)"
note "python: $(python3 --version 2>&1)"
note "repo HEAD: $(git -C "$REPO" rev-parse --short HEAD)"
note "spec(작업 트리) $DOC sha256: $(sha256sum "$REPO/$DOC" | cut -c1-16)"

# S0. clone + 스파이크 파일 복사. 후보 Core는 S2에서 넣는다(S1은 HEAD 그대로).
git clone -q "$REPO" "$C"
note "[s0-clone] HEAD=$(git -C "$C" rev-parse --short HEAD)"
mkdir -p "$C/spikes/T-011"
cp -R "$SPIKE/." "$C/spikes/T-011/"

# S1. 기준선(HEAD). 카드: 1282.
step s1-restore "$C" dotnet restore $SLN
step s1-build "$C" dotnet build $SLN --no-restore -c Release -warnaserror
run_tests s1-test "$C"

# S2. 후보 Core(Judgment 4파일) → 빌드 → 형식 → 전체 테스트(G1 포함). 기존 테스트·예제는 그대로.
cp -R "$SPIKE/core/Judgment" "$C/src/Scorecard.Core/"
step s2-build "$C" dotnet build $SLN --no-restore -c Release -warnaserror
step s2-format "$C" dotnet format $SLN --verify-no-changes --no-restore
run_tests s2-test "$C"
diff_tests s1-test s2-test
step s2-tool-build "$C" dotnet build spikes/T-011/JudgmentSpike/JudgmentSpike.csproj -c Release -warnaserror
expected_unchanged "$C" s2-examples

# S3. 예제 4종 + G2 표본 판정. 결정론: 다시(다른 프로세스), 다른 경로 사본, LANG=de_DE.UTF-8 프로세스, 문화권 7종.
O="$WORK/out"
for e in "${EX[@]}"; do
  step "s3-judge-$e" "$C" "$T" judge "examples/$e" "$O/a/$e/judgment.json"
  step "s3-again-$e" "$C" "$T" judge "examples/$e" "$O/b/$e/judgment.json"
done
step s3-judge-case-valid "$C" "$T" judge "$GUARD" "$O/a/case-valid/judgment.json"
mkdir -p "$WORK/copy"
cp -R "$C/examples" "$WORK/copy/examples"
for e in "${EX[@]}"; do
  ( cd "$C" && "$T" judge "$WORK/copy/examples/$e" "$O/path/$e/judgment.json" > /dev/null )
  ( cd "$C" && LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8 "$T" judge "examples/$e" "$O/de/$e/judgment.json" > /dev/null )
  r1=$(cmp -s "$O/a/$e/judgment.json" "$O/b/$e/judgment.json" && echo same || echo DIFF)
  r2=$(cmp -s "$O/a/$e/judgment.json" "$O/path/$e/judgment.json" && echo same || echo DIFF)
  r3=$(cmp -s "$O/a/$e/judgment.json" "$O/de/$e/judgment.json" && echo same || echo DIFF)
  note "[s3-det-cmp] $e again=$r1 other-path=$r2 de-DE-process=$r3"
done
step s3-culture "$C" "$T" culture examples/table-compare examples/form-fill examples/mixed-exceptions examples/signoff-final "$GUARD"

# S3 교차 확인(Python, C# 코드를 쓰지 않음): 작업 트리 judgment.md의 표와 글만 보고 짰다.
cp "$REPO/$DOC" "$C/$DOC"
P="$WORK/py"
for e in "${EX[@]}"; do
  step "s3-xcheck-$e" "$C" python3 -I spikes/T-011/xcheck/judge.py $DOC "examples/$e" "$P/$e/judgment.json"
  note "[s3-xcheck-cmp] $e csharp-vs-python: $(cmp -s "$O/a/$e/judgment.json" "$P/$e/judgment.json" && echo same || echo DIFF)"
done
step s3-xcheck-case-valid "$C" python3 -I spikes/T-011/xcheck/judge.py $DOC "$GUARD" "$P/case-valid/judgment.json"
note "[s3-xcheck-cmp] case-valid csharp-vs-python: $(cmp -s "$O/a/case-valid/judgment.json" "$P/case-valid/judgment.json" && echo same || echo DIFF)"

# S4. 표 밖 실험.
step s4-probe "$C" "$T" probe

# S5. G2·G3에 judge-case-valid(샘플 case-valid) 등록 → 빌드 → 전체 테스트.
step s5-guard-variant "$C" python3 -I spikes/T-011/patch/guard-variant.py .
step s5-build "$C" dotnet build $SLN --no-restore -c Release -warnaserror
step s5-format "$C" dotnet format $SLN --verify-no-changes --no-restore
run_tests s5-test "$C"
diff_tests s2-test s5-test

# S6. 음성 대조(따로 clone): (a) 문장 숫자를 CurrentCulture로 → G2 R4, (b) DateTime.Now → G1.
for k in culture clock; do
  N="$WORK/n-$k"
  fresh "$N"
  ( cd "$N" && python3 -I spikes/T-011/patch/guard-variant.py . > /dev/null )
  step "s6-$k-patch" "$N" python3 -I spikes/T-011/patch/negative.py . $k
  step "s6-$k-restore" "$N" dotnet restore $SLN
  step "s6-$k-build" "$N" dotnet build $SLN --no-restore -c Release -warnaserror
  run_tests "s6-$k-test" "$N"
done

# S7. expected/judgment.json 4개 = 스펙 블록(옮김) → 전체 테스트(a) → 파일 목록 테스트 변형(b). 기존 expected 무변경.
step s7-extract "$C" python3 -I spikes/T-011/blocks.py extract $DOC .
expected_unchanged "$C" s7-examples
run_tests s7a-test "$C"
step s7-tests-variant "$C" python3 -I spikes/T-011/tests-variant.py .
step s7b-build "$C" dotnet build $SLN --no-restore -c Release -warnaserror
run_tests s7b-test "$C"
diff_tests s5-test s7b-test
for e in "${EX[@]}"; do
  note "[s7-expected-vs-engine] $e: $(cmp -s "$C/examples/$e/expected/judgment.json" "$O/a/$e/judgment.json" && echo same || echo DIFF)"
done

# S7c. 안 M-A 영향(따로 clone): CaseInputs.Group·파서·대조 + 예제 case.yaml 둘·스펙 블록 → 전체 테스트.
M="$WORK/m-a"
fresh "$M"
step s7c-patch-core "$M" python3 -I spikes/T-011/patch/apply-group.py .
step s7c-patch-examples "$M" python3 -I spikes/T-011/patch/examples-variant.py .
expected_unchanged "$M" s7c-examples
step s7c-restore "$M" dotnet restore $SLN
step s7c-build "$M" dotnet build $SLN --no-restore -c Release -warnaserror
run_tests s7c-test "$M"

# S8. 작업 트리 judgment.md의 표를 읽어 후보와 대조. 7절 블록·SHA-256 줄 = S3 엔진 출력.
step s8-spec "$C" "$T" spec $DOC
step s8-blocks "$C" python3 -I spikes/T-011/blocks.py check $DOC "$O/a"
note "done"
