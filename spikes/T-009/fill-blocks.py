#!/usr/bin/env python3
# T-009 스파이크(머지 대상 아님). 스펙 세 문서의 "### 7.5 signoff-final" 절을 파일에서 채운다.
# 절 안에는 머리 글 한 줄, 파일마다 "`examples/signoff-final/<파일>` (SHA-256 `…`):" 줄과 블록 하나만 둔다(테스트가 절 안 블록 수를 센다).
# 절이 없으면 "## 8." 제목 바로 앞에 넣는다. 있으면 그 절(다음 제목 전까지)을 바꾼다. 그 밖의 줄은 건드리지 않는다.
# 사용: fill-blocks.py <저장소> <예제폴더>
import hashlib
import sys
from pathlib import Path

EXAMPLE = 'signoff-final'
HEADING = '### 7.5 signoff-final'

# 문서 → (파일, 블록 언어, SHA-256 줄을 쓰나). 순서 = 절 안 블록 순서(synthetic-examples는 7.1 파일 표 순서).
INTRO = {
    'docs/spec/classification.md': 'T-009 초안(사람 확정 필요). 만드는 방법은 7.1 항목 8, 근거와 질문은 `docs/spec/signoff-final.md`.',
    'docs/spec/synthetic-examples.md': 'T-009 초안(사람 확정 필요). 만드는 방법은 7.1 T-009 글머리, 근거와 질문은 `docs/spec/signoff-final.md`.',
    'docs/spec/metrics.md': 'T-009 초안(사람 확정 필요). 만드는 방법은 7.1 항목 10, 근거와 질문은 `docs/spec/signoff-final.md`.',
}

DOCS = {
    'docs/spec/classification.md': [('answers.yaml', 'yaml', False), ('expected/classification.json', 'json', True)],
    'docs/spec/synthetic-examples.md': [('case.yaml', 'yaml', True), ('intake.md', 'text', True),
                                        ('reference/records.csv', 'csv', True), ('candidate/records.csv', 'csv', True)],
    'docs/spec/metrics.md': [('expected/metrics.json', 'json', True)],
}


def section(doc, folder, files):
    out = [HEADING, '', INTRO[doc], '']
    for name, lang, sha in files:
        data = (Path(folder) / name).read_bytes()
        text = data.decode('utf-8')
        assert text.endswith('\n') and '\r' not in text and '```' not in text, name
        label = f'`examples/{EXAMPLE}/{name}`'
        out.append(f'{label} (SHA-256 `{hashlib.sha256(data).hexdigest()}`):' if sha else f'{label}:')
        out += ['', '```' + lang] + text[:-1].split('\n') + ['```', '']
    return out


# 다음 제목 줄. 펜스 안의 '#' 줄(YAML 주석 등)은 제목이 아니다.
def next_heading(lines, i):
    fence = False
    for k in range(i, len(lines)):
        if lines[k].startswith('```'):
            fence = not fence
        elif not fence and lines[k].startswith('#'):
            return k
    return len(lines)


def main(repo, folder):
    for doc, files in DOCS.items():
        path = Path(repo) / doc
        lines = path.read_text(encoding='utf-8').split('\n')
        new = section(doc, folder, files)
        if HEADING in lines:
            start = lines.index(HEADING)
            end = next_heading(lines, start + 1)
        else:
            start = end = next(i for i, l in enumerate(lines) if l.startswith('## 8. '))
        lines[start:end] = new
        path.write_text('\n'.join(lines), encoding='utf-8')
        print(f'RESULT fill {doc} {HEADING} blocks={len(files)}')


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
