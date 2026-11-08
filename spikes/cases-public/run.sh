#!/usr/bin/env bash
# 스파이크(머지 대상 아님). 사용: spikes/cases-public/run.sh <작업폴더(없는 경로)>
# 커밋된 HEAD를 작업 폴더에 clone하고, 작업 트리의 이 스파이크·answers를 복사해 돌린다.
# 저장소의 src/·tests/·examples/는 건드리지 않는다.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(git -C "$here" rev-parse --show-toplevel)"
work="${1:?작업 폴더를 준다}"
if [ -e "$work" ]; then echo "이미 있다: $work" >&2; exit 2; fi
mkdir -p "$work"
git clone -q "$repo" "$work/repo"
mkdir -p "$work/repo/spikes/cases-public/probes" "$work/repo/docs/cases-public/answers"
cp "$here/CasesPublicSpike.csproj" "$here/Program.cs" "$work/repo/spikes/cases-public/"
cp "$here"/probes/*.yaml "$work/repo/spikes/cases-public/probes/"
cp "$repo"/docs/cases-public/answers/*.yaml "$work/repo/docs/cases-public/answers/"
proj="$work/repo/spikes/cases-public/CasesPublicSpike.csproj"
echo "repo HEAD $(git -C "$work/repo" rev-parse --short HEAD)"
echo "dotnet $(dotnet --version)"
dotnet build -c Release -nologo -v q "$proj" > "$work/build.log" 2>&1 || { cat "$work/build.log"; exit 1; }
echo "== answers"
rc=0
dotnet run -c Release --no-build --project "$proj" -- "$work/repo/docs/cases-public/answers" || rc=$?
echo "answers rc=$rc"
echo "== probes"
dotnet run -c Release --no-build --project "$proj" -- "$work/repo/spikes/cases-public/probes" || echo "probes rc=$?"
exit "$rc"
