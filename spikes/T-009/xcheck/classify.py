#!/usr/bin/env python3
# T-009 스파이크 교차 확인(머지 대상 아님). classification.md·case-schema.md의 표와 글만 보고 짠 분류기다.
# C# Core(StepClassifier, ClassificationJson)를 쓰지 않고 읽지도 않는다. YAML 파서도 쓰지 않는다(공개 예제 모양만 다룬다).
# 표: case-schema 4.4 답 표(키 순서·기본값), classification 4.2 조건 표, 4.5 문장 틀·문구 표, 4.6 읽은 답 표, 4.7 확인 항목 표.
# 글(표 밖, 코드에 옮겼다): 4.1 "처음 걸리는 곳에서 멈춘다", PARTIAL은 ELSE에만, GATE는 SIGNOFF 뒤에 없다,
#   4.3 행 순서, 4.6 표시 문자열, 4.8 AI 과잉 후보, 3.2 JSON 모양, 3.3 바이트 규칙.
# 사용: classify.py <classification.md> <case-schema.md> <answers.yaml> <출력파일>
# 표준 라이브러리만 쓴다. 네트워크 없음.
import hashlib
import re
import sys
from pathlib import Path

DEFAULT_MARK = ' (기본값 사용, 확인 필요)'


def cells(row):
    t = row.strip()
    return [c.strip() for c in t[1:-1].split('|')]


def table(lines, header):
    i = [k for k, l in enumerate(lines) if l.strip() == header]
    assert len(i) == 1, header
    rows = []
    for l in lines[i[0] + 1:]:
        if not l.lstrip().startswith('|'):
            break
        c = cells(l)
        if all(x and set(x) <= set('-:') for x in c):
            continue
        rows.append(c)
    return rows


def spans(text):
    return text.split('`')[1::2]


def unq(c):
    return c.strip().strip('`')


def load_rules(cls_md, schema_md):
    cl = Path(cls_md).read_text(encoding='utf-8').splitlines()
    sc = Path(schema_md).read_text(encoding='utf-8').splitlines()
    keys, defaults = [], {}
    for r in table(sc, '| 키 | 질문 | 형식 | 선택지·범위 | 기본값 |'):
        k = unq(r[0])
        keys.append(k)
        d = unq(r[4])
        defaults[k] = None if d == 'null' else (int(d) if re.fullmatch(r'[0-9]+', d) else d)
    cond = {}
    for r in table(cl, '| 근거 키 | 키 | 비교 | 값 |'):
        cond.setdefault(unq(r[0]), []).append((unq(r[1]), r[2].strip('`'), spans(r[3])))
    templates = {}
    for r in table(cl, '| 근거 키 | 담당 | 행 종류 | 문장 틀 |'):
        templates[unq(r[0])] = (r[1], unq(r[2]), unq(r[3]))
    phrases = {(unq(r[0]), unq(r[1])): r[2] for r in table(cl, '| 키 | 값 | 문구 |')}
    used = {}
    for r in table(cl, '| 근거 키 | 읽은 답 |'):
        used[unq(r[0]).replace('`', '')] = spans(r[1])
    checks = []
    for r in table(cl, '| 키 | 조건 | 뜻 |'):
        s = spans(r[1])
        checks.append((unq(r[0]), s[0], None if 'null' in r[1] else s[1]))
    return keys, defaults, cond, templates, phrases, used, checks


def scalar(v):
    v = v.strip()
    if re.fullmatch(r'-?[0-9]+', v):
        return int(v)
    if re.fullmatch(r'-?[0-9]+\.[0-9]+', v):
        return v
    return v


def read_answers(path):
    text = Path(path).read_text(encoding='utf-8')
    name = re.search(r'^name: (.+)$', text, re.M).group(1).strip()
    steps = []
    for m in re.finditer(r'^  - name: (.+)\n    answers: \{(.*)\}$', text, re.M):
        answers = {}
        for part in m.group(2).split(', '):
            k, v = part.split(': ', 1)
            answers[k] = scalar(v)
        steps.append((m.group(1).strip(), answers))
    return name, steps


def holds(rule, a, cond):
    for key, op, values in cond[rule]:
        v = a[key]
        if op == '=' and not (str(v) == values[0]):
            return False
        if op == 'in' and str(v) not in values:
            return False
        if op == '<' and not (int(v) < int(values[0])):
            return False
    return True


def sentence(rule, a, templates, phrases):
    text = templates[rule][2]

    def sub(m):
        inner = m.group(1)
        if inner.endswith(' 문구'):
            k = inner[:-3]
            return phrases[(k, str(a[k]))].replace('{batch_size}', str(a['batch_size']))
        return str(a[inner])

    return re.sub(r'\{([a-z_]+(?: 문구)?)\}', sub, text)


def classify(rules, steps):
    keys, defaults, cond, templates, phrases, used, checks = rules
    out = []
    for number, (name, given) in enumerate(steps, start=1):
        a = {k: given.get(k, defaults[k]) for k in keys}
        is_default = {k: k not in given for k in keys}
        rows = []

        def row(rule, used_key):
            assignee, kind, _ = templates[rule]
            reason = sentence(rule, a, templates, phrases)
            if any(is_default[k] for k in used[used_key]):
                reason += DEFAULT_MARK
            rows.append({'kind': kind, 'assignee': assignee, 'rule': rule, 'reason': reason})

        # 4.1: 처음 걸리는 곳에서 멈춘다. 순서는 4.2 조건 표의 단계 행 순서(SIGNOFF, Q1~Q4) 뒤 ELSE.
        leaf = next((r for r in ['SIGNOFF', 'Q1', 'Q2', 'Q3', 'Q4'] if holds(r, a, cond)), 'ELSE')
        row(leaf, 'ELSE (frequency = rare)' if leaf == 'ELSE' and a['frequency'] == 'rare' else leaf)
        if leaf == 'ELSE' and holds('PARTIAL', a, cond):
            row('PARTIAL', 'PARTIAL')
        if leaf != 'SIGNOFF' and holds('GATE', a, cond):
            row('GATE', 'GATE')
        step_checks = [c for c, k, v in checks if (a[k] is None if v is None else str(a[k]) == v)]
        overuse = leaf != 'SIGNOFF' and holds('OVERUSE', a, cond) and rows[0]['assignee'] == '②'
        out.append({'number': number, 'name': name, 'defaults': [k for k in keys if is_default[k]],
                    'checks': step_checks, 'ai_overuse_candidate': overuse, 'rows': rows})
    return out


def esc(s):
    out = []
    for ch in s:
        o = ord(ch)
        if ch == '"':
            out.append('\\"')
        elif ch == '\\':
            out.append('\\\\')
        elif o < 0x20 or o == 0x7f or o in (0x2028, 0x2029):
            out.append({'\t': '\\t', '\n': '\\n', '\r': '\\r', '\b': '\\b', '\f': '\\f'}.get(ch, f'\\u{o:04X}'))
        elif o > 0xFFFF:
            raise ValueError('서로게이트 쌍은 이 교차 확인 범위 밖이다')
        else:
            out.append(ch)
    return '"' + ''.join(out) + '"'


def dump(v, ind=0):
    pad = '  ' * ind
    if isinstance(v, bool):
        return 'true' if v else 'false'
    if isinstance(v, int):
        return str(v)
    if isinstance(v, str):
        return esc(v)
    if isinstance(v, list):
        if not v:
            return '[]'
        return '[\n' + ',\n'.join(pad + '  ' + dump(x, ind + 1) for x in v) + '\n' + pad + ']'
    return '{\n' + ',\n'.join(f'{pad}  "{k}": {dump(x, ind + 1)}' for k, x in v.items()) + '\n' + pad + '}'


def main(cls_md, schema_md, answers, out):
    rules = load_rules(cls_md, schema_md)
    name, steps = read_answers(answers)
    result = classify(rules, steps)
    data = (dump({'schema_version': 1, 'case': name, 'steps': result}) + '\n').encode('utf-8')
    Path(out).parent.mkdir(parents=True, exist_ok=True)
    Path(out).write_bytes(data)
    rows = [r for s in result for r in s['rows']]
    print(f"XCHECK classify {name} steps={len(result)} rows={len(rows)} "
          f"rules={','.join(str(s['number']) + ':' + '+'.join(r['rule'] for r in s['rows']) for s in result)} "
          f"measure={sum('measure_minutes_per_item' in s['checks'] for s in result)} "
          f"confirm={sum('confirm_existing_feature' in s['checks'] for s in result)} "
          f"overuse={sum(s['ai_overuse_candidate'] for s in result)} "
          f"default-marked={sum(r['reason'].endswith(DEFAULT_MARK) for r in rows)} "
          f"sha256={hashlib.sha256(data).hexdigest()} bytes={len(data)}")


if __name__ == '__main__':
    main(*sys.argv[1:5])
