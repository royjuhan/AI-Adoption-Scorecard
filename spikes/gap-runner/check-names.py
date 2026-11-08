#!/usr/bin/env python3
"""회사 이름 검사. 스파이크(머지 대상 아님).

이름 목록은 docs/cases-public/README.md 1절에서 읽는다(이 파일에 이름을 적지 않는다).
- 표 "사례" 칸의 앞 영문 이름
- "출처:" 줄의 괄호 앞 영문 이름
대상: spikes/gap-*/ 아래 커밋될 파일(zip이면 안의 파일도), docs/cases-public/gaps.md.
영문 대소문자 무시, 앞뒤가 영문·숫자가 아닐 때만 맞음으로 본다.
쓰기: python3 -I spikes/gap-runner/check-names.py   (저장소 루트에서)
"""
import pathlib
import re
import subprocess
import sys
import zipfile

root = pathlib.Path(__file__).resolve().parents[2]
readme = (root / "docs/cases-public/README.md").read_text(encoding="utf-8")
sec1 = readme.split("## 1.", 1)[1].split("## 2.", 1)[0]

names = set()
rows = 0
for line in sec1.splitlines():
    m = re.match(r"\| \d+ \| ([^|]+)\|", line)
    if m:
        rows += 1
        lead = re.match(r"[A-Za-z0-9][A-Za-z0-9'\- .]*", m.group(1).strip())
        if lead:
            names.add(lead.group(0).strip())
    if line.startswith("출처:"):
        for m in re.finditer(r"([A-Za-z][A-Za-z ]*?)(?: 고객 사례)?\(", line):
            names.add(m.group(1).strip())
# 여러 낱말 이름은 첫 낱말(4자 이상)도 따로 찾는다(예: 사례 칸 "이름 + 일반 낱말").
names |= {n.split()[0] for n in list(names) if " " in n and len(n.split()[0]) >= 4}

# 커밋될 파일만 본다(.gitignore의 bin/·obj/는 뺀다).
listed = subprocess.run(["git", "-C", str(root), "ls-files", "-co", "--exclude-standard", "--", "spikes/gap-*"],
                        capture_output=True, text=True, check=True).stdout.split("\n")
targets = sorted(root / f for f in listed if f)
targets.append(root / "docs/cases-public/gaps.md")


def texts(p):
    b = p.read_bytes()
    yield str(p.relative_to(root)), b.decode("utf-8", errors="ignore")
    if zipfile.is_zipfile(p):
        with zipfile.ZipFile(p) as z:
            for n in z.namelist():
                yield f"{p.relative_to(root)}!{n}", z.read(n).decode("utf-8", errors="ignore")


# .NET 프로젝트 파일의 SDK 식별자(Sdk="<이름>.NET.Sdk")는 도구 이름이라 뺀다. 빼면 그 수를 따로 찍는다.
SDK_TAIL = ".NET.Sdk"
hits = []
sdk = 0
pats = {n: re.compile(r"(?<![A-Za-z0-9])" + re.escape(n) + r"(?![A-Za-z0-9])", re.IGNORECASE) for n in names}
for p in targets:
    for where, text in texts(p):
        for n, pat in pats.items():
            for m in pat.finditer(text):
                if text[m.end():m.end() + len(SDK_TAIL)] == SDK_TAIL:
                    sdk += 1
                    continue
                hits.append((where, n))
                break

print(f"rows {rows}, names {len(names)}, files {len(targets)}, hits {len(hits)}, sdk-id skipped {sdk}")
for where, n in hits:
    print(f"  HIT {where}: {n}")
sys.exit(1 if hits else 0)
