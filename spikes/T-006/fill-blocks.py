#!/usr/bin/env python3
# T-006 스파이크(머지 대상 아님). 스파이크가 낸 파일로 스펙 블록을 채운다.
# - "### 7.x <예제>" 아래 첫 코드 블록 = <출력루트>/<예제>/expected/metrics.json
# - "`examples/<예제>/expected/metrics.json` (SHA-256 `...`)" 줄의 해시
# - "### 6.x ...compare-invalid..." 아래 ```text 블록 = <출력루트>/guard/compare-invalid.txt
# - "`TestData/guard/compare-invalid/<경로>`:" 줄 다음 블록 = <출력루트>/guard/sample/<경로>
# 사용: fill-blocks.py <spec.md> <출력루트>
import hashlib
import sys
from pathlib import Path

EXAMPLES = ['table-compare', 'form-fill', 'mixed-exceptions']


def find(lines, pred):
    found = [i for i, l in enumerate(lines) if pred(l)]
    assert len(found) == 1, f'줄이 {len(found)}개'
    return found[0]


def replace_block(lines, start, fence_pred, content):
    # start 줄 다음에서 fence_pred에 맞는 여는 줄을 찾는다. 블록 밖의 '#' 줄(다음 제목)을 만나면 멈춘다.
    i, inside = start + 1, False
    while True:
        line = lines[i]
        if line.startswith('```'):
            if not inside and fence_pred(line):
                break
            inside = not inside
        elif line.startswith('#') and not inside:
            raise SystemExit(f'블록이 없다: {lines[start]}')
        i += 1
    j = i + 1
    while not lines[j].startswith('```'):
        j += 1
    body = content.decode('utf-8').split('\n')
    assert body[-1] == '', '끝이 LF가 아니다'
    return lines[:i + 1] + body[:-1] + lines[j:]


def main(spec, root):
    path = Path(spec)
    lines = path.read_text(encoding='utf-8').split('\n')
    for e in EXAMPLES:
        data = (Path(root) / e / 'expected' / 'metrics.json').read_bytes()
        lines = replace_block(lines, find(lines, lambda l: l.startswith('### 7.') and l.endswith(' ' + e)), lambda f: True, data)
        sha = hashlib.sha256(data).hexdigest()
        prefix = f'`examples/{e}/expected/metrics.json` (SHA-256 `'
        idx = [i for i, l in enumerate(lines) if l.startswith(prefix)]
        assert len(idx) == 1, f'SHA 줄이 {len(idx)}개: {e}'
        rest = lines[idx[0]][len(prefix):]
        lines[idx[0]] = prefix + sha + rest[rest.index('`'):]
        print(f'{e} {sha} {len(data)}')
    errors = (Path(root) / 'guard' / 'compare-invalid.txt').read_bytes()
    lines = replace_block(lines, find(lines, lambda l: l.startswith('### 6.') and 'compare-invalid' in l), lambda f: f.strip() == '```text', errors)
    print(f'compare-invalid {len(errors)}')
    prefix = '`TestData/guard/compare-invalid/'
    for rel in [l[len(prefix):-2] for l in lines if l.startswith(prefix) and l.endswith('`:')]:
        data = (Path(root) / 'guard' / 'sample' / rel).read_bytes()
        lines = replace_block(lines, find(lines, lambda l: l == prefix + rel + '`:'), lambda f: True, data)
        print(f'sample {rel} {len(data)}')
    path.write_text('\n'.join(lines), encoding='utf-8')


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
