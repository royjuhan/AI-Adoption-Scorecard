#!/usr/bin/env bash
# 스파이크. 남긴다(docs/cases-public/README.md 4절 1). README 1절 표의 사례 이름 칸과 출처 줄에서 회사 이름을 뽑아
# 주어진 경로에 하나라도 있는지 본다. 사용: name-check.sh <검사할 경로>...
set -euo pipefail
readme="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)/docs/cases-public/README.md"
names="$(mktemp)"
# 1절 표(## 1. ~ ## 2. 사이)만 본다: 둘째 칸 앞쪽의 한글이 아닌 글자(라틴 문자·공백·'·-)가 회사 이름이다.
awk -F'|' '/^## 1\./ {on=1} /^## 2\./ {on=0} on && /^\| [0-9]+ \|/ {print $3}' "$readme" \
  | sed -E 's/^ +//; s/[가-힣].*$//; s/ +$//' | sort -u > "$names"
# 출처 줄의 이름(괄호 앞 영문). 이 파일에 이름을 적지 않는다.
grep '^출처:' "$readme" | grep -oP '[A-Za-z][A-Za-z ]*?(?=( 고객 사례)?\()' | sed -E 's/ +$//' >> "$names"
# 여러 낱말 이름은 3자 이상 낱말도 따로 본다.
tr ' ' '\n' < "$names" | awk 'length($0) >= 3' >> "$names"
sort -u -o "$names" "$names"
echo "이름 $(wc -l < "$names")개"
hits=0
while IFS= read -r n; do
  [ -z "$n" ] && continue
  if grep -rniwF -- "$n" "$@" >/dev/null 2>&1; then
    echo "HIT $n"; grep -rniwF -- "$n" "$@" | sed -n 1,3p; hits=$((hits+1))
  fi
done < "$names"
echo "hits=$hits"
rm -f "$names"
[ "$hits" -eq 0 ]
