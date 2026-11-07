#!/usr/bin/env python3
# T-009 스파이크(머지 대상 아님). test-engineer가 할 "블록을 바이트 그대로 옮기기"를 clone에서 흉내 낸다.
# 스펙 세 문서의 "### 7.5 signoff-final" 절 블록을 읽어 examples/signoff-final/<파일>로 쓴다. 엔진 출력에서 복사하지 않는다.
# 블록 읽기는 테스트 도우미 SpecDocument.CodeBlocks와 같다: 펜스 사이 줄을 LF로 잇고 끝에 LF 하나.
# SHA-256 줄이 있는 파일은 블록 바이트의 SHA-256이 그 줄과 같은지 본다. 다르면 실패한다.
# 사용: extract-example.py <저장소>
import hashlib
import re
import sys
from pathlib import Path

EXAMPLE = 'signoff-final'
HEADING = '### 7.5 signoff-final'
DOCS = {
    'docs/spec/classification.md': ['answers.yaml', 'expected/classification.json'],
    'docs/spec/synthetic-examples.md': ['case.yaml', 'intake.md', 'reference/records.csv', 'candidate/records.csv'],
    'docs/spec/metrics.md': ['expected/metrics.json'],
}


def blocks(lines, heading):
    start = lines.index(heading)
    out, cur = [], None
    for line in lines[start + 1:]:
        if cur is None and line.startswith('#'):
            break
        if line.startswith('```'):
            if cur is None:
                cur = []
            else:
                out.append(cur)
                cur = None
            continue
        if cur is not None:
            cur.append(line)
    return out


def main(repo):
    for doc, files in DOCS.items():
        lines = (Path(repo) / doc).read_text(encoding='utf-8').split('\n')
        found = blocks(lines, HEADING)
        assert len(found) == len(files), (doc, len(found))
        for name, block in zip(files, found):
            data = ('\n'.join(block) + '\n').encode('utf-8')
            sha = hashlib.sha256(data).hexdigest()
            label = f'`examples/{EXAMPLE}/{name}` (SHA-256 `'
            sha_lines = [l for l in lines if l.startswith(label)]
            if sha_lines:
                assert len(sha_lines) == 1, (doc, name)
                written = re.search(r'SHA-256 `([0-9a-f]{64})`', sha_lines[0]).group(1)
                assert written == sha, (doc, name, written, sha)
            path = Path(repo) / 'examples' / EXAMPLE / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
            print(f'RESULT extract {name} sha256={sha} bytes={len(data)} sha-line={"same" if sha_lines else "none"}')


if __name__ == '__main__':
    main(sys.argv[1])
