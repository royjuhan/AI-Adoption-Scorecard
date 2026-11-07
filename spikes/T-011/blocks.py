#!/usr/bin/env python3
# T-011 스파이크(머지 대상 아님). judgment.md 7절의 expected/judgment.json 블록을 다룬다.
#   extract <judgment.md> <clone 루트>   블록을 바이트 그대로 examples/<예제>/expected/judgment.json으로 옮긴다(SHA-256 줄 확인).
#   check <judgment.md> <출력 폴더>       블록·SHA-256 줄이 <출력 폴더>/<예제>/judgment.json(엔진 출력)과 같은지 본다.
#   fill <judgment.md> <출력 폴더>        블록·SHA-256 줄을 엔진 출력으로 채운다(작업 트리 문서를 고친다).
# 블록 = SHA-256 줄(`examples/<예제>/expected/judgment.json` (SHA-256 `…`):) 바로 뒤의 ```json 블록. 블록 안 끝에 "\n" 하나를 붙인 것이 파일이다.
import hashlib
import re
import sys
from pathlib import Path

EXAMPLES = ['table-compare', 'form-fill', 'mixed-exceptions', 'signoff-final']
mode, doc = sys.argv[1], Path(sys.argv[2])
target = Path(sys.argv[3])
text = doc.read_text(encoding='utf-8')
bad = 0
for ex in EXAMPLES:
    rx = re.compile(r'`examples/' + re.escape(ex) + r'/expected/judgment\.json` \(SHA-256 `([0-9a-f]{64})`\):\n\n```json\n(.*?)\n```\n', re.S)
    found = list(rx.finditer(text))
    if len(found) != 1:
        print(f'BLOCK {ex} found={len(found)}')
        bad += 1
        continue
    m = found[0]
    data = (m.group(2) + '\n').encode('utf-8')
    sha = hashlib.sha256(data).hexdigest()
    if mode == 'extract':
        out = target / 'examples' / ex / 'expected' / 'judgment.json'
        out.write_bytes(data)
        ok = sha == m.group(1)
        bad += not ok
        print(f'BLOCK {ex} extract sha-line={"same" if ok else "DIFF"} bytes={len(data)} sha256={sha}')
    elif mode == 'check':
        out = (target / ex / 'judgment.json').read_bytes()
        ok1, ok2 = out == data, hashlib.sha256(out).hexdigest() == m.group(1)
        bad += not (ok1 and ok2)
        print(f'BLOCK {ex} block-vs-engine={"same" if ok1 else "DIFF"} sha-line-vs-engine={"same" if ok2 else "DIFF"}')
    else:
        out = (target / ex / 'judgment.json').read_bytes()
        assert out.endswith(b'\n') and not out.endswith(b'\n\n')
        body = out[:-1].decode('utf-8')
        new = f'`examples/{ex}/expected/judgment.json` (SHA-256 `{hashlib.sha256(out).hexdigest()}`):\n\n```json\n{body}\n```\n'
        text = text[:m.start()] + new + text[m.end():]
        print(f'BLOCK {ex} filled bytes={len(out)}')
if mode == 'fill':
    doc.write_text(text, encoding='utf-8')
print(f'BLOCK total-bad={bad}')
sys.exit(1 if bad else 0)
