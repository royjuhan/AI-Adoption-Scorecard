#!/usr/bin/env bash
# 스파이크. 남긴다(docs/cases-public/README.md 4절 2). 잠재 빈 곳 14~25 합성 예제를 지금 엔진으로 돌린다.
# 쓰기: spikes/gap-runner/run-gaps.sh <작업폴더> [gap-NN ...]
# - 커밋된 HEAD의 src/Scorecard.Core를 작업 폴더에 풀어 그 사본을 참조한다(작업 트리의 진행 중 수정을 섞지 않는다).
# - 각 spikes/gap-NN/runs.txt의 줄(<상대 경로> [옵션])마다 gap-runner를 부른다.
# - 결과: spikes/gap-NN/result.txt(요약 줄), spikes/gap-NN/out/<경로>/ (엔진 JSON 바이트 그대로).
set -euo pipefail

work="${1:?작업 폴더}"
shift || true
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
head_sha="$(git -C "$repo" rev-parse --short HEAD)"

rm -rf "$work/core" "$work/bin"
mkdir -p "$work/core" "$work/bin"
git -C "$repo" archive HEAD src/Scorecard.Core | tar -x -C "$work/core"

dotnet build "$here/GapRunner.csproj" -c Release --nologo -v q \
  -p:CoreDir="$work/core/src/Scorecard.Core" -o "$work/bin" > "$work/build.log" 2>&1 \
  || { cat "$work/build.log"; exit 1; }

runner="$work/bin/GapRunner"

if [ "$#" -gt 0 ]; then
  gaps=("$@")
else
  mapfile -t gaps < <(cd "$repo/spikes" && ls -d gap-[0-9]* | sort)
fi

for gap in "${gaps[@]}"; do
  gdir="$repo/spikes/$gap"
  [ -f "$gdir/runs.txt" ] || continue
  rm -rf "$gdir/out"
  {
    echo "# $gap. 엔진: HEAD $head_sha (src/Scorecard.Core 사본). 도구: spikes/gap-runner"
    while read -r rel opts; do
      case "$rel" in ''|'#'*) continue ;; esac
      echo "== $rel ${opts:-}"
      out="$gdir/out/${rel%.yaml}"
      # shellcheck disable=SC2086
      (cd "$gdir" && "$runner" "$rel" "$out" ${opts:-}) || echo "  EXIT $?"
    done < "$gdir/runs.txt"
  } > "$gdir/result.txt"
  echo "$gap: $(grep -c '^==' "$gdir/result.txt") runs"
done
