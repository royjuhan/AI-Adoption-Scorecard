#!/usr/bin/env python3
# T-011 스파이크(머지 대상 아님). 안 M-A(case.yaml inputs.group)의 영향을 재려고 clone의 예제 case.yaml 둘과
# 스펙 블록(synthetic-examples.md 7.2·7.5), SHA-256 줄, signoff-final.md 파일 표를 바꾼다. apply-group.py와 같이 쓴다.
# 추천안(M-B)은 이 변형을 쓰지 않는다. 바꿀 줄이 정확히 한 번 있어야 한다.
# 사용: examples-variant.py <clone 루트>
import hashlib
import sys
from pathlib import Path

root = Path(sys.argv[1])
syn = root / 'docs/spec/synthetic-examples.md'
sf = root / 'docs/spec/signoff-final.md'
plan = [('table-compare', '  key: log_id\n', '  group: signal_type\n'),
        ('signoff-final', '  key: record_id\n', '  group: record_kind\n')]
s = syn.read_text(encoding='utf-8')
t = sf.read_text(encoding='utf-8')
for name, key_line, group_line in plan:
    p = root / 'examples' / name / 'case.yaml'
    old = p.read_bytes()
    text = old.decode('utf-8')
    assert text.count(key_line) == 1, (name, 'case.yaml')
    new = text.replace(key_line, key_line + group_line).encode('utf-8')
    p.write_bytes(new)
    old_sha, new_sha = hashlib.sha256(old).hexdigest(), hashlib.sha256(new).hexdigest()
    assert s.count(old_sha) == 1 and s.count(key_line) >= 1, (name, 'synthetic-examples')
    s = s.replace(old_sha, new_sha)
    # 블록 안의 key 줄: 그 예제 SHA-256 줄 뒤 처음 나오는 것.
    at = s.index(new_sha)
    k = s.index(key_line, at)
    s = s[:k] + key_line + group_line + s[k + len(key_line):]
    if t.count(old_sha):
        t = t.replace(old_sha, new_sha).replace(f'| {len(old):,} |', f'| {len(new):,} |')
    print(f'VARIANT {name} case.yaml sha256 {old_sha[:8]}→{new_sha[:8]} bytes {len(old)}→{len(new)}')
syn.write_text(s, encoding='utf-8')
sf.write_text(t, encoding='utf-8')
