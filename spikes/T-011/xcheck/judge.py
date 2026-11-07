#!/usr/bin/env python3
# T-011 스파이크 교차 확인(머지 대상 아님). judgment.md의 글(4절 규칙)과 표(4.2·4.3·4.4·4.5·4.7)만 보고 짰다. C# 코드를 쓰지 않는다.
# 대조 부분은 T-006 Python 독립 구현(compare.py, metrics.md 3·4절만 보고 짠 것)을 가져와 기준 행의 칸을 남기게 고쳤다.
# 공개 예제 모양(따옴표 없음, 짧은 case.yaml)과 G2 표본 case-valid만 다룬다. 시간·사람 입력은 없다(None).
# 유형 열(4.8 안 M-B)은 기준 파일마다 머리 행을 따로 보고 정한다.
# 사용: judge.py <judgment.md> <케이스폴더> <출력파일>
# 표준 라이브러리만 쓴다. 네트워크 없음.
import csv
import hashlib
import io
import re
import sys
from decimal import Decimal
from pathlib import Path


# ---------- judgment.md 표 ----------
def table(lines, header):
    starts = [i for i, l in enumerate(lines) if l.strip() == header]
    assert len(starts) == 1, (header, len(starts))
    rows = []
    for l in lines[starts[0] + 1:]:
        if not l.lstrip().startswith('|'):
            break
        cells = [c.strip() for c in l.strip().strip('|').split('|')]
        if all(c and set(c) <= set('-:') for c in cells):
            continue
        rows.append(cells)
    return rows


def u(cell):
    return cell.strip().strip('`')


def fill(template, values):
    return re.sub(r'\{([a-z_]+)\}', lambda m: values[m.group(1)], u(template))


# ---------- case.yaml (예제 모양만) ----------
def read_case(path):
    text = Path(path).read_text(encoding='utf-8')
    name = re.search(r'^name: (.+)$', text, re.M).group(1).strip()
    inputs = None
    m = re.search(r'^inputs:\n  reference: (.+)\n  candidate: (.+)\n  key: (.+)\n', text, re.M)
    if m:
        inputs = (m.group(1).strip(), m.group(2).strip(), m.group(3).strip())
    compare = []
    for f in re.finditer(r'^  - \{field: ([a-z_]+)(?:, (tolerance|tolerance_pct): ([0-9.]+))?\}$', text, re.M):
        compare.append((f.group(1), f.group(2), f.group(3)))
    thresholds = {}
    t = re.search(r'^thresholds:\n((?:  .+\n)+)', text, re.M)
    if t:
        for line in t.group(1).splitlines():
            k, v = line.strip().split(': ')
            thresholds[k] = v
    return name, inputs, compare, thresholds


# ---------- 대조(T-006 compare.py에서) ----------
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
    return Decimal(s)


def rows(case_dir, paths, key, headers=None):
    out = []
    for path in paths:
        text = (Path(case_dir) / path).read_bytes().decode('utf-8-sig')
        records = [r for r in csv.reader(io.StringIO(text, newline='')) if any(norm(c) for c in r)]
        header = [norm(c) for c in records[0]]
        if headers is not None:
            headers.append(header)
        for r in records[1:]:
            cells = [norm(c) for c in r]
            out.append((cells[header.index(key)], dict(zip(header, cells))))
    return out


def cell_judge(kind, tol, r, c):
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


def compare_case(case_dir, inputs, compare):
    ref_pat, cand_pat, key = inputs
    headers = []
    ref = rows(case_dir, files(case_dir, ref_pat), key, headers)
    cand = {k: cells for k, cells in rows(case_dir, files(case_dir, cand_pat), key)}
    status = []  # (key, 상태, 기준 칸)
    for k, cells in ref:
        if k not in cand:
            status.append((k, 'missing', cells))
            continue
        results = [cell_judge(kind, tol, cells[f], cand[k][f]) for f, kind, tol in compare]
        status.append((k, 'mismatch' if any(r not in (None, 'within') for r in results) else 'match', cells))
    # 유형 열: 모든 기준 파일 머리 행에 꼭 한 번 있고, 키·compare 필드·빈 이름이 아닌 열. 이름 서수(코드 단위) 순.
    used = {key} | {f for f, _, _ in compare}
    common = None
    for h in headers:
        once = {c for c in h if h.count(c) == 1}
        common = once if common is None else common & once
    types = sorted((c for c in common if c and c not in used), key=lambda c: c.encode('utf-16-be'))
    return status, types


# ---------- 판정(judgment.md 4절) ----------
class Raw(str):
    pass


def dump(v, ind=0):
    pad = '  ' * ind
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


MARK = ' (기본값 사용, 확인 필요)'


def main(doc, case_dir, out):
    lines = Path(doc).read_text(encoding='utf-8').splitlines()
    defaults_table = {u(c[0]): u(c[1]) for c in table(lines, '| 키 | 기본값 | 근거 |')}
    focus_min = int(u([c for c in table(lines, '| 이름 | 값 | 뜻 |') if u(c[0]) == 'focus_min'][0][1]))
    verdict_rows = table(lines, '| 순서 | 근거 키 | 걸리는 결과 | 판정 | 판정 문장 틀 |')
    vt = {(u(c[1]), u(c[2])): (u(c[3]), c[4]) for c in verdict_rows}
    ct = {(u(c[0]), u(c[1])): c[3] for c in table(lines, '| 확인 | 결과 | 조건 | 확인 문장 틀 |')}
    reads = {u(c[0]): ([] if u(c[1]) == '—' else [u(x) for x in c[1].split(',')]) for c in table(lines, '| 근거 키 | 읽은 문턱 |')}
    todo_t = {u(c[0]): c[2] for c in table(lines, '| 다음 할 일 | 언제 | 문구 |')}
    todo_order = list(todo_t)

    name, inputs, compare, th = read_case(Path(case_dir) / 'case.yaml')
    order = ['accuracy_min', 'missing_max', 'time_must_drop']
    used = {k: th.get(k, defaults_table[k]) for k in order}
    defaults = [k for k in order if k not in th]
    must_drop = used['time_must_drop'] == 'true'
    thresholds = {'accuracy_min': Raw(used['accuracy_min']), 'missing_max': Raw(used['missing_max']), 'time_must_drop': must_drop}

    def confirm():
        return fill(todo_t['confirm_thresholds'], {'defaults': ', '.join(f'{k} {used[k]}' for k in defaults)})

    if inputs is None or not compare:
        reason = 'no_inputs' if inputs is None else 'no_compare_fields'
        verdict, template = vt[('NOT_VERIFIED', reason)]
        todo = []
        if reason == 'no_inputs':
            todo.append('add_data')
        todo.append('add_compare')
        if must_drop:
            todo.append('measure_time')
        if defaults:
            todo.append('confirm_thresholds')
        body = {'verdict': verdict, 'rule': 'NOT_VERIFIED', 'reason': u(template), 'checks': [],
                'todo': [{'key': k, 'text': confirm() if k == 'confirm_thresholds' else u(todo_t[k])} for k in todo]}
    else:
        status, types = compare_case(case_dir, inputs, compare)
        n = len(status)
        match = sum(1 for s in status if s[1] == 'match')
        missing = sum(1 for s in status if s[1] == 'missing')
        bad = [s for s in status if s[1] != 'match']
        acc_min, miss_max = Decimal(used['accuracy_min']), Decimal(used['missing_max'])
        v = {'accuracy_pct': pct(match, n), 'missing_rate_pct': pct(missing, n), 'match': str(match), 'missing': str(missing),
             'reference': str(n), 'accuracy_min': used['accuracy_min'], 'missing_max': used['missing_max'], 'bad': str(len(bad))}

        def mark(text, keys):
            return text + MARK if any(k in defaults for k in keys) else text

        results = {}
        results['ACCURACY'] = 'met' if match * 100 >= acc_min * n else 'not_met'
        results['MISSING'] = 'met' if missing * 100 <= miss_max * n else 'not_met'
        results['UNEXPLAINED'] = 'none' if not bad else 'not_reviewed'  # 사람 입력 없음
        results['TIME'] = 'not_measured' if must_drop else 'off'  # 시간 입력 없음
        show = lambda x: x if x else '(빈 칸)'
        if len(bad) < focus_min:
            results['FOCUS'] = 'few'
        elif not types:
            results['FOCUS'] = 'off'
        else:
            seen = []
            for col in types:
                values = [s[2][col] for s in status]
                if len(set(values)) == 1:
                    continue
                seen.append(col)
                badv = {s[2][col] for s in bad}
                if len(badv) == 1:
                    (val,) = badv
                    results['FOCUS'] = 'found'
                    v['group'] = show(col)
                    v['value'] = show(val)
                    v['rows_in'] = str(values.count(val))
                    v['rows_out'] = str(len(values) - values.count(val))
                    break
            else:
                results['FOCUS'] = 'spread' if seen else 'one_value'
                v['columns'] = ', '.join(show(c) for c in (seen if seen else types))
        owner = {'ACCURACY': ['accuracy_min'], 'MISSING': ['missing_max'], 'TIME': ['time_must_drop'], 'UNEXPLAINED': [], 'FOCUS': []}
        checks = [{'rule': r, 'result': results[r], 'reason': mark(fill(ct[(r, results[r])], v), owner[r])}
                  for r in ['ACCURACY', 'MISSING', 'UNEXPLAINED', 'TIME', 'FOCUS']]
        # 판정 표 순서대로, 처음 걸리는 행.
        decided = None
        for c in verdict_rows:
            rule, res = u(c[1]), u(c[2])
            if rule == 'NOT_VERIFIED':
                continue
            if rule == 'PASS' or results.get(rule) == res:
                decided = (rule, u(c[3]), c[4])
                break
        rule, verdict, template = decided
        body = {'verdict': verdict, 'rule': rule, 'reason': mark(fill(template, v), reads[rule]), 'checks': checks}
        todo = []
        if results['TIME'] == 'not_measured':
            todo.append('measure_time')
        if results['UNEXPLAINED'] == 'not_reviewed':
            todo.append('review_mismatches')
        if results['FOCUS'] == 'off':
            todo.append('add_type_column')
        if defaults:
            todo.append('confirm_thresholds')
        todo.sort(key=todo_order.index)
        body['todo'] = [{'key': k, 'text': confirm() if k == 'confirm_thresholds' else fill(todo_t[k], v)} for k in todo]

    doc_obj = {'schema_version': 1, 'case': name, 'verdict': body['verdict'], 'rule': body['rule'], 'reason': body['reason'],
               'thresholds': thresholds, 'defaults': defaults, 'checks': body['checks'], 'todo': body['todo']}
    data = (dump(doc_obj) + '\n').encode('utf-8')
    Path(out).parent.mkdir(parents=True, exist_ok=True)
    Path(out).write_bytes(data)
    print(f"XCHECK {name} verdict={body['verdict']} rule={body['rule']} "
          f"checks={','.join(c['rule'] + ':' + c['result'] for c in body['checks'])} "
          f"todo={','.join(t['key'] for t in body['todo'])} sha256={hashlib.sha256(data).hexdigest()} bytes={len(data)}")


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2], sys.argv[3])
