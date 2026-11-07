#!/usr/bin/env python3
# T-006 스파이크 교차 확인(머지 대상 아님). 스펙 metrics.md 3·4절의 글만 보고 C# 후보와 따로 짠 구현이다.
# 같은 예제에서 같은 metrics.json 바이트가 나오면, 정의·순서·바이트 규칙 설명이 빠짐없다는 근거가 된다.
# CSV는 표준 csv 모듈, 숫자는 decimal, 퍼센트는 정수 셈. 공개 예제 모양(따옴표 없음, 짧은 case.yaml)만 다룬다.
# 사용: compare.py <케이스폴더> <출력파일>
# 표준 라이브러리만 쓴다. 네트워크 없음.
import csv
import io
import re
import sys
from decimal import Decimal, InvalidOperation
from pathlib import Path


def read_case(path):
    text = Path(path).read_text(encoding='utf-8')
    name = re.search(r'^name: (.+)$', text, re.M).group(1).strip()
    inputs = None
    m = re.search(r'^inputs:\n  reference: (.+)\n  candidate: (.+)\n  key: (.+)$', text, re.M)
    if m:
        inputs = (m.group(1).strip(), m.group(2).strip(), m.group(3).strip())
    compare = []
    for f in re.finditer(r'^  - \{field: ([a-z_]+)(?:, (tolerance|tolerance_pct): ([0-9.]+))?\}$', text, re.M):
        compare.append((f.group(1), f.group(2), f.group(3)))
    return name, inputs, compare


def files(case_dir, pattern):
    folder, name = pattern.rsplit('/', 1)
    rx = re.compile('^' + re.escape(name).replace(r'\*', '.*').replace(r'\?', '.') + '$', re.S)
    d = Path(case_dir) / folder
    return [f'{folder}/{p.name}' for p in sorted(d.iterdir(), key=lambda p: p.name) if p.is_file() and rx.match(p.name)]


def norm(s):
    return s.replace('\r\n', '\n').replace('\r', '\n').strip()


def number(s):
    if not re.fullmatch(r'[+-]?([0-9]+\.?[0-9]*|\.[0-9]+)([eE][+-]?[0-9]+)?', s):
        return None
    try:
        return Decimal(s)
    except InvalidOperation:
        return None


def rows(case_dir, paths, key):
    out = []
    for path in paths:
        text = (Path(case_dir) / path).read_bytes().decode('utf-8-sig')
        records = [r for r in csv.reader(io.StringIO(text, newline='')) if any(norm(c) for c in r)]
        header = [norm(c) for c in records[0]]
        # 줄 번호: 이 예제들은 여러 줄 칸·빈 줄이 없어 레코드 번호 + 1이 줄이다.
        for i, r in enumerate(records[1:], start=2):
            cells = [norm(c) for c in r]
            out.append((cells[header.index(key)], path, i, dict(zip(header, cells))))
    return out


def judge(kind, tol, r, c):
    if kind is None:
        return None if r == c else 'outside'
    rv, cv = Decimal(r), number(c)
    if cv is None:
        return 'not_number'
    diff = abs(cv - rv)
    if diff == 0:
        return None
    t = Decimal(tol)
    ok = diff <= t if kind == 'tolerance' else diff * 100 <= abs(rv) * t
    return 'within' if ok else 'outside'


def pct(part, whole):
    tenths = (2 * part * 1000 + whole) // (2 * whole)
    return f'{tenths // 10}.{tenths % 10}'


class Raw(str):
    pass


def dump(v, ind=0):
    pad = '  ' * ind
    if v is None:
        return 'null'
    if isinstance(v, Raw):
        return str(v)
    if isinstance(v, bool):
        return 'true' if v else 'false'
    if isinstance(v, int):
        return str(v)
    if isinstance(v, str):
        return '"' + v.replace('\\', '\\\\').replace('"', '\\"') + '"'
    if isinstance(v, list):
        if not v:
            return '[]'
        return '[\n' + ',\n'.join(pad + '  ' + dump(x, ind + 1) for x in v) + '\n' + pad + ']'
    if isinstance(v, dict):
        return '{\n' + ',\n'.join(f'{pad}  "{k}": {dump(x, ind + 1)}' for k, x in v.items()) + '\n' + pad + '}'
    raise TypeError(v)


def main(case_dir, out):
    name, inputs, compare = read_case(Path(case_dir) / 'case.yaml')
    if inputs is None or not compare:
        body = {'status': 'not_verified', 'reason': 'no_inputs' if inputs is None else 'no_compare_fields', 'key': None,
                'reference_files': [], 'candidate_files': [], 'counts': None, 'accuracy_pct': None,
                'missing_rate_pct': None, 'fields': [], 'items': []}
    else:
        ref_pat, cand_pat, key = inputs
        rf, cf = files(case_dir, ref_pat), files(case_dir, cand_pat)
        ref, cand = rows(case_dir, rf, key), rows(case_dir, cf, key)
        by_key = {k: (p, ln, cells) for k, p, ln, cells in cand}
        ref_keys = {k for k, *_ in ref}
        stats = [[0, 0, 0] for _ in compare]  # outside, within, not_number
        items, match, mismatch, missing, within_items = [], 0, 0, 0, 0
        for k, p, ln, cells in ref:
            if k not in by_key:
                missing += 1
                items.append({'key': k, 'status': 'missing', 'reference': {'file': p, 'line': ln}, 'candidate': None, 'differences': []})
                continue
            cp, cln, ccells = by_key[k]
            diffs = []
            for i, (field, kind, tol) in enumerate(compare):
                res = judge(kind, tol, cells[field], ccells[field])
                if res:
                    stats[i][['outside', 'within', 'not_number'].index(res)] += 1
                    diffs.append({'field': field, 'result': res, 'reference': cells[field], 'candidate': ccells[field]})
            bad = any(d['result'] != 'within' for d in diffs)
            if bad:
                mismatch += 1
            else:
                match += 1
                within_items += 1 if diffs else 0
            if bad or diffs:
                items.append({'key': k, 'status': 'mismatch' if bad else 'match', 'reference': {'file': p, 'line': ln},
                              'candidate': {'file': cp, 'line': cln}, 'differences': diffs})
        extra = 0
        for k, p, ln, _ in cand:
            if k not in ref_keys:
                extra += 1
                items.append({'key': k, 'status': 'extra', 'reference': None, 'candidate': {'file': p, 'line': ln}, 'differences': []})
        compared = len(ref) - missing
        fields = [{'field': f, 'kind': 'text' if kind is None else 'number',
                   'tolerance': Raw(tol) if kind == 'tolerance' else None,
                   'tolerance_pct': Raw(tol) if kind == 'tolerance_pct' else None,
                   'compared': compared, 'outside': s[0], 'within': s[1], 'not_number': s[2]}
                  for (f, kind, tol), s in zip(compare, stats)]
        body = {'status': 'measured', 'reason': None, 'key': key, 'reference_files': rf, 'candidate_files': cf,
                'counts': {'reference': len(ref), 'candidate': len(cand), 'match': match, 'mismatch': mismatch,
                           'missing': missing, 'extra': extra, 'within': within_items},
                'accuracy_pct': Raw(pct(match, len(ref))), 'missing_rate_pct': Raw(pct(missing, len(ref))),
                'fields': fields, 'items': items}
    Path(out).parent.mkdir(parents=True, exist_ok=True)
    Path(out).write_bytes((dump({'case': name, 'compare': body}) + '\n').encode('utf-8'))
    print('ok')


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
