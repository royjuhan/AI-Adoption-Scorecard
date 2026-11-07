#!/usr/bin/env python3
# T-005 스파이크 교차 확인(머지 대상 아님). 스펙 4.2·4.5의 글만 보고 C# 후보와 따로 짠 구현이다.
# 같은 계획에서 같은 CSV 바이트가 나오면, 생성 순서 설명이 빠짐없고 수열이 플랫폼·언어와 상관없다는 근거가 된다.
# 사용: generate.py <spec.md> <예제> <case.yaml> <출력폴더>
# 표준 라이브러리만 쓴다. 네트워크 없음.
import re
import sys
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path

M64 = (1 << 64) - 1


class SplitMix64:
    def __init__(self, seed):
        self.x = seed & M64

    def next(self):
        self.x = (self.x + 0x9E3779B97F4A7C15) & M64
        z = self.x
        z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & M64
        z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & M64
        return z ^ (z >> 31)

    def below(self, n):
        return (self.next() * n) >> 64


def cells(row):
    t = row.strip()
    t = t[1:-1] if t.endswith('|') else t[1:]
    return [c.strip() for c in t.split('|')]


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


def plan(spec, example):
    lines = Path(spec).read_text(encoding='utf-8').splitlines()
    p = [r for r in table(lines, '| 예제 | 시드 | 파일 | 키 접두어 | 키 자릿수 | 기준 건수 | 값 불일치 열 | 누락 | 추가 | 허용오차 안 차이 | 몰림 | 결과 섞기 |') if unq(r[0]) == example][0]
    cols = []
    for r in table(lines, '| 예제 | 열 | 종류 | 값 |'):
        if unq(r[0]) != example:
            continue
        s = spans(r[3])
        if r[2] == '정수':
            cols.append(('integer', unq(r[1]), int(s[0]), int(s[1]), 0, []))
        elif r[2] == '소수':
            scale = len(s[0]) - s[0].index('.') - 1 if '.' in s[0] else 0
            cols.append(('decimal', unq(r[1]), Decimal(s[0]), Decimal(s[1]), scale, []))
        else:
            cols.append(('choice', unq(r[1]), 0, 0, 0, s))
    within = (None, 0) if p[9] == '없음' else (spans(p[9])[0], int(p[9].split(' ')[-1]))
    focus = (None, None) if p[10] == '없음' else tuple(spans(p[10])[:2])
    return dict(seed=int(unq(p[1])), file=unq(p[2]), prefix=unq(p[3]), digits=int(unq(p[4])), rows=int(unq(p[5])),
                cols=cols, mismatch=[] if p[6] == '없음' else spans(p[6]), missing=int(unq(p[7])), extra=int(unq(p[8])),
                within_col=within[0], within=within[1], focus_col=focus[0], focus_val=focus[1], shuffle=p[11] == '예')


def case_schema(path):
    text = Path(path).read_text(encoding='utf-8')
    key = re.search(r'^  key: (\S+)$', text, re.M).group(1)
    compare = {}
    for m in re.finditer(r'^  - \{field: ([a-z_]+)(?:, (tolerance|tolerance_pct): ([0-9.]+))?\}$', text, re.M):
        compare[m.group(1)] = (m.group(2), Decimal(m.group(3)) if m.group(3) else None)
    return key, compare


def fmt(d, scale):
    return f'{d:.{scale}f}'


def draw(cols, rng):
    out = []
    for kind, name, lo, hi, scale, choices in cols:
        if kind == 'integer':
            out.append(str(lo + rng.below(hi - lo + 1)))
        elif kind == 'decimal':
            unit = 10 ** scale
            low, high = int(lo * unit), int(hi * unit)
            out.append(fmt(Decimal(low + rng.below(high - low + 1)) / Decimal(unit), scale))
        else:
            out.append(choices[rng.below(len(choices))])
    return out


def rnd(x, scale):
    return x.quantize(Decimal(1).scaleb(-scale), rounding=ROUND_HALF_UP)


def partial_shuffle(pool, m, rng):
    for j in range(m):
        k = j + rng.below(len(pool) - j)
        pool[j], pool[k] = pool[k], pool[j]


def generate(spec, example, case_yaml):
    p = plan(spec, example)
    key, compare = case_schema(case_yaml)
    cols = p['cols']
    names = [c[1] for c in cols]
    rng = SplitMix64(p['seed'])
    ref = [draw(cols, rng) for _ in range(p['rows'])]
    fi = names.index(p['focus_col']) if p['focus_col'] else -1
    pool = [i for i in range(p['rows']) if fi < 0 or ref[i][fi] == p['focus_val']]
    nd = p['missing'] + len(p['mismatch'])
    partial_shuffle(pool, nd, rng)
    missing = set(pool[:p['missing']])
    mm_rows = pool[p['missing']:nd]
    chosen = set(pool[:nd])
    rest = [i for i in range(p['rows']) if i not in chosen]
    partial_shuffle(rest, p['within'], rng)
    within_rows = rest[:p['within']]
    cand = [list(r) for r in ref]
    for j, name in enumerate(p['mismatch']):
        c = names.index(name)
        kind, _, lo, hi, scale, choices = cols[c]
        row = mm_rows[j]
        r = ref[row][c]
        if kind == 'integer':
            rv = int(r)
            sign = 1 if rng.below(2) == 0 else -1
            delta = int(compare[name][1]) + 1 + rng.below(20)
            v = rv + sign * delta
            if v < lo or v > hi:
                v = rv - sign * delta
            cand[row][c] = str(v)
        elif kind == 'decimal':
            rv = Decimal(r)
            pct = compare[name][1]
            sign = 1 if rng.below(2) == 0 else -1
            factor = 2 + rng.below(3)
            cand[row][c] = fmt(rnd(rv * (1 + sign * pct * factor / Decimal(100)), scale), scale)
        else:
            i = choices.index(r)
            n = len(choices)
            cand[row][c] = choices[(i + 1 + rng.below(n - 1)) % n]
    if p['within_col']:
        c = names.index(p['within_col'])
        scale = cols[c][4]
        pct = compare[p['within_col']][1]
        for row in within_rows:
            rv = Decimal(ref[row][c])
            sign = 1 if rng.below(2) == 0 else -1
            tenths = 1 + rng.below(4)
            cand[row][c] = fmt(rnd(rv * (1 + sign * pct * tenths / Decimal(1000)), scale), scale)
    k = lambda i: p['prefix'] + str(i + 1).zfill(p['digits'])
    extra = [[k(p['rows'] + e)] + draw(cols, rng) for e in range(p['extra'])]
    rows = [[k(i)] + cand[i] for i in range(p['rows']) if i not in missing] + extra
    if p['shuffle']:
        for i in range(len(rows) - 1, 0, -1):
            j = rng.below(i + 1)
            rows[i], rows[j] = rows[j], rows[i]
    header = [key] + names
    render = lambda rs: (''.join(','.join(r) + '\n' for r in [header] + rs)).encode('ascii')
    return p['file'], render([[k(i)] + ref[i] for i in range(p['rows'])]), render(rows)


if __name__ == '__main__':
    spec, example, case_yaml, out = sys.argv[1:5]
    name, ref, cand = generate(spec, example, case_yaml)
    for folder, data in (('reference', ref), ('candidate', cand)):
        d = Path(out) / folder
        d.mkdir(parents=True, exist_ok=True)
        (d / name).write_bytes(data)
    # SplitMix64 시드 0의 첫 값 세 개.
    r = SplitMix64(0)
    print('PROBE xcheck python seed=0 first3=' + ','.join(f'{r.next():016x}' for _ in range(3)))
