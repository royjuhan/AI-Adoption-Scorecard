#!/usr/bin/env python3
# oracle 자기 점검. oracle을 고친 prototyper가 돌린다(CI 계약 밖. CI는 check.py만 부른다).
#
#   python3 tools/oracle/selftest.py [--repo <루트>]
#
# 1. 스펙 표·블록 대조: oracle이 스펙 문서의 예시 표와 같은 답을 내는지 본다. 이 점검만 스펙 문서를 읽는다.
#    classification.md 6.1 분기 표, 6.2 기본값 표시 표·"답 없는 단계" 블록, 6.5 TestData 표본(추정 표시).
#    metrics.md 4.1 패턴 표 2개, 4.5 숫자 모양·칸 판정 표, 4.6 앞뒤 글자 표, 4.8 퍼센트 표.
#    TestData: valid-full 값, guard case-valid 분류(기본값 표시 5행)·metrics(4.10 SHA-256·크기), 오류 표본은 멈춤.
#    변형: 예제 CSV의 CRLF·CR·BOM·따옴표·마지막 줄바꿈 없음·끝 빈 행·칸 앞뒤 공백, answers.yaml의 CRLF·CR·BOM이 같은 바이트.
# 2. 음성 대조: 예제 사본에서 expected 한 바이트, expected 삭제, 결과 CSV 값, answers.yaml 답을 바꾸면
#    check.py가 DIFF·MISSING과 종료 코드 1을 낸다. 사본 그대로면 모두 MATCH, 종료 코드 0.
# 3. 결정론: 문화권·해시 시드 환경 변수를 바꿔 check.py를 돌려도 표준 출력·보고서 바이트가 같다.
# 실패가 하나라도 있으면 종료 코드 1. 저장소 파일은 읽기만 한다(사본은 임시 폴더).
from __future__ import annotations

import argparse
import os
import re
import shutil
import subprocess
import sys
import tempfile
from decimal import Decimal
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.dont_write_bytecode = True  # 저장소에 __pycache__를 쓰지 않는다.
sys.path.insert(0, str(HERE))

from classify import DEFAULT_MARK, INFERRED_MARK, classification_bytes, classify, to_json_value  # noqa: E402
from compare import is_supported_pattern, judge, matches, metrics_bytes, parse_number, percent  # noqa: E402
from oracle_case import ANSWER_KEYS, CompareField, parse_case  # noqa: E402
from oracle_common import OracleError, sha256_hex, utf8_stdio  # noqa: E402
from oracle_csv import normalize  # noqa: E402
from oracle_json import render  # noqa: E402

FAILS: list = []
COUNT = {'ok': 0}


def expect(cond: bool, label: str) -> None:
    if cond:
        COUNT['ok'] += 1
    else:
        FAILS.append(label)
        print(f'FAIL {label}')


# ---- 스펙 문서 읽기 (점검 전용) ----

def table(lines: list, header: str) -> list:
    hits = [k for k, line in enumerate(lines) if line.strip() == header]
    if len(hits) != 1:
        raise SystemExit(f'selftest: 머리 행이 정확히 한 번 있어야 한다: {header} ({len(hits)}번)')
    rows = []
    for line in lines[hits[0] + 1:]:
        if not line.lstrip().startswith('|'):
            break
        cells = [c.strip() for c in line.strip()[1:-1].split('|')]
        if all(c and set(c) <= set('-: ') for c in cells):
            continue
        rows.append(cells)
    return rows


def spans(text: str) -> list:
    return text.split('`')[1::2]


def cell_value(text: str) -> str:
    return '' if text == '(빈 칸)' else text.strip('`')


def block_after(lines: list, marker: str) -> list:
    k = next(i for i, line in enumerate(lines) if line.startswith(marker))
    start = next(i for i in range(k + 1, len(lines)) if lines[i].startswith('```'))
    end = next(i for i in range(start + 1, len(lines)) if lines[i] == '```')
    return lines[start + 1:end]


# ---- 분류 점검 ----



def answers_yaml(name: str, steps: list) -> bytes:
    """[(단계 이름, {키: 글}, [추정 키])] → answers.yaml 바이트(흐름 매핑)."""
    out = [f'name: {name}', 'task: selftest 합성 단계', 'steps:']
    for step_name, answers, inferred in steps:
        out.append(f'  - name: {step_name}')
        if answers:
            out.append('    answers: {' + ', '.join(f'{k}: {v}' for k, v in answers.items()) + '}')
        if inferred:
            out.append('    inferred: [' + ', '.join(inferred) + ']')
    return ('\n'.join(out) + '\n').encode('utf-8')


def pairs(text: str) -> dict:
    out = {}
    for part in text.split(', '):
        k, v = part.split(': ', 1)
        out[k] = v
    return out


def classify_one(answers: dict, inferred=()) -> dict:
    case = parse_case(answers_yaml('selftest', [('단계', answers, list(inferred))]), 'answers.yaml')
    return classify(case)[0]


def check_classification(spec: list) -> None:
    base_line = next(line for line in spec if line.startswith('기준 답: `'))
    base = pairs(spans(base_line)[0])
    expect(list(base) == list(ANSWER_KEYS), '6.1 기준 답 키 = case-schema 4.4 순서')

    # 6.1 분기 표
    for changed, rows_text in table(spec, '| 바꾼 답 | 행 |'):
        want = re.findall(r'([①②③④]) `([A-Z0-9]+)`', rows_text)
        if changed == '(없음)':
            variants = [{}]
        elif ' / ' in changed:
            first, *rest = spans(changed)
            k, v = first.split(': ', 1)
            variants = [{k: v}] + [{k: alt} for alt in rest]
        else:
            variants = [pairs(spans(changed)[0])]
        for change in variants:
            got = classify_one({**base, **change})
            got_rows = [(r['assignee'], r['rule']) for r in got['rows']]
            expect(got_rows == want, f'6.1 {change}: {got_rows} != {want}')
            expect(not any(r['reason'].endswith((DEFAULT_MARK, INFERRED_MARK)) for r in got['rows']),
                   f'6.1 {change}: 직접 쓴 답인데 표시가 붙었다')

    # 6.2 기본값 표시 표: 해당 키를 빼서(기본값) 분류한다.
    for default_cell, mark_cell in table(spec, '| 기본값인 답 | 문장 표시 |'):
        sp = spans(default_cell)
        key, changes = sp[0], {}
        for s in sp[1:]:
            if ': ' in s:
                changes.update(pairs(s))
        answers = {k: v for k, v in {**base, **changes}.items() if k != key}
        got = classify_one(answers)
        marked = set() if mark_cell.startswith('없음') else \
            {s for s in spans(mark_cell.split('. ')[0]) if s in ('step', 'exception_review', 'human_gate')}
        for r in got['rows']:
            expect(r['reason'].endswith(DEFAULT_MARK) == (r['kind'] in marked),
                   f"6.2 {default_cell}: {r['kind']} 행 표시가 표와 다르다")
            expect(r['reason'].count('(기본값 사용, 확인 필요)') <= 1, f'6.2 {default_cell}: 표시가 두 번')
        expect(marked <= {r['kind'] for r in got['rows']}, f'6.2 {default_cell}: 표가 말한 행이 없다')
        for check_name in re.findall(r'확인 항목 `([a-z_]+)`', mark_cell):
            expect(check_name in got['checks'], f'6.2 {default_cell}: 확인 항목 {check_name} 없음')
        expect(got['defaults'] == [key], f"6.2 {default_cell}: defaults {got['defaults']}")

    # 6.2 "답 없는 단계" 블록
    block = block_after(spec, '파서를 거친 "답 없는 단계"')
    got = classify_one({})
    lines = [f"{r['assignee']} {r['rule']}: {r['reason']}" for r in got['rows']]
    expect(lines == block[:2], f'6.2 답 없는 단계 행: {lines}')
    expect(got['defaults'] == list(ANSWER_KEYS), '6.2 답 없는 단계 defaults 10개')
    expect(block[2] == 'defaults: 10개 모두. checks: ' + ', '.join(got['checks']), '6.2 답 없는 단계 checks')


def check_signoff_inferred(repo: Path, spec: list) -> None:
    path = repo / 'tests/Scorecard.Tests/TestData/classification/signoff-inferred/answers.yaml'
    data = path.read_bytes()
    want_yaml = block_after(spec, 'TestData 표본(제안) `tests/Scorecard.Tests/TestData/classification/signoff-inferred/answers.yaml`')
    expect(data.decode('utf-8') == '\n'.join(want_yaml) + '\n', '6.5 TestData 표본 = 스펙 블록')
    case = parse_case(data, 'answers.yaml')
    steps = classify(case)
    lines = [f"{s['number']} {r['assignee']} {r['rule']}: {r['reason']}" for s in steps for r in s['rows']]
    lines += [f"inferred {s['number']}: {', '.join(s['inferred'])}" for s in steps]
    want = block_after(spec, '분류 결과. 줄 모양은')
    expect(lines == want, '6.5 분류 결과 = 스펙 결과 블록')
    if lines != want:
        for a, b in zip(lines, want):
            if a != b:
                print(f'  oracle: {a}\n  spec  : {b}')
    expect(steps[3]['ai_overuse_candidate'] is True, '6.5 4단계 AI 과잉 후보')
    expect('confirm_existing_feature' in steps[1]['checks'], '6.5 2단계 confirm_existing_feature')
    with_inf = render(to_json_value(case, steps))
    expect(b'"inferred"' not in with_inf, '6.5 JSON에 inferred 속성 없음')
    # 같은 답에서 inferred 줄만 뺀 파일과 reason 네 줄(1단계 1행, 2단계 3행)만 다르다.
    plain = b''.join(line for line in data.splitlines(keepends=True) if not line.lstrip().startswith(b'inferred:'))
    plain_case = parse_case(plain, 'answers.yaml')
    no_inf = render(to_json_value(plain_case, classify(plain_case)))
    a, b = with_inf.decode('utf-8').split('\n'), no_inf.decode('utf-8').split('\n')
    diff = [k for k in range(max(len(a), len(b))) if k >= len(a) or k >= len(b) or a[k] != b[k]]
    expect(len(a) == len(b) and len(diff) == 4 and all('"reason":' in a[k] for k in diff),
           f'6.5 inferred를 뺀 파일과 reason 4줄만 다름 (다른 줄 {len(diff)})')


# ---- 대조 점검 ----

# 4.5 숫자 모양 표에서 .NET decimal 반올림이 필요한 칸. oracle은 이 칸을 범위 밖으로 멈춘다(README).
REFUSED_NUMBER_CELLS = {'0.1234567890123456789012345678901', '1e-400'}


def check_metrics_tables(spec: list) -> None:
    for pattern, ok in table(spec, '| 패턴 | 받나 |'):
        expect(is_supported_pattern(pattern.strip('`')) == (ok == '예'), f'4.1 패턴 {pattern}')
    for pattern, name, ok in table(spec, '| 패턴 | 파일 이름 | 맞나 |'):
        expect(matches(cell_value(pattern), cell_value(name)) == (ok == '예'), f'4.1 맞추기 {pattern} {name}')

    for cell, is_num, value in table(spec, '| 칸 | 숫자인가 | 값 |'):
        text = cell_value(cell)
        if text in REFUSED_NUMBER_CELLS:
            try:
                parse_number(text)
                expect(False, f'4.5 숫자 {text}: 범위 밖으로 멈추지 않았다')
            except OracleError:
                expect(True, '')
            continue
        got = parse_number(normalize(text))
        if is_num == '예':
            expect(got is not None and got == Decimal(value.strip('`')), f'4.5 숫자 {text} → {got}')
        else:
            expect(got is None, f'4.5 숫자 아님 {text} → {got}')
    expect(parse_number(normalize(' 12 ')) == 12, "4.5 ' 12 '")

    for tol, ref, cand, result in table(spec, '| 허용 오차 | 기준 값 | 결과 값 | 판정 |'):
        if tol == '없음':
            f = CompareField('f', None, None)
        else:
            k, v = tol.strip('`').split(': ')
            f = CompareField('f', v if k == 'tolerance' else None, v if k == 'tolerance_pct' else None)
        got = judge(f, normalize(cell_value(ref)), normalize(cell_value(cand)))
        want = None if result == '같음' else result.strip('`')
        expect(got == want, f'4.5 칸 판정 {tol} {ref} {cand}: {got} != {want}')
    expect(judge(CompareField('f', '0', None), '5', normalize(' 5 ')) is None, "4.5 결과 ' 5 '")
    expect(judge(CompareField('f', None, None), 'item-a', normalize(' item-a ')) is None, "4.5 결과 ' item-a '")

    for char, name, strip in table(spec, '| 글자 | 이름 | 앞뒤에서 떼나 |'):
        ch = chr(int(char.strip('`')[2:], 16))
        expect((normalize(ch + 'a' + ch) == 'a') == (strip == '예'), f'4.6 {char} {name}')
    expect(normalize('a\r\nb') == normalize('a\rb') == 'a\nb', '4.6 칸 안 CRLF·CR → LF')
    expect(normalize('가') != normalize('가'), '4.6 NFC·NFD 다름')

    for part, whole, pct in table(spec, '| 일치 | 기준 행 | 정확도(%) |'):
        expect(percent(int(part), int(whole)).text == pct, f'4.8 {part}/{whole}')


# ---- TestData 표본 ----

def check_testdata(repo: Path, cls_spec: list, met_spec: list) -> None:
    data_dir = repo / 'tests/Scorecard.Tests/TestData'
    # case-schema 6.1 블록 = valid-full = guard/case-valid. 블록 목록·따옴표·~·'|' 블록을 읽는다.
    full = parse_case((data_dir / 'case-schema/valid-full/case.yaml').read_bytes(), 'case.yaml')
    s2, s3, s4 = full.steps[1].answers, full.steps[2].answers, full.steps[3].answers
    expect(s2['exception_rate'].value == Decimal('62.5') and not s2['exception_rate'].is_default, 'valid-full 2단계 exception_rate')
    expect(s3['rule_expressible'].value == 'no' and not s3['rule_expressible'].is_default, 'valid-full 3단계 "no"')
    expect(s3['minutes_per_item'].value is None and s3['minutes_per_item'].is_default, 'valid-full 3단계 ~')
    expect(all(a.is_default for a in s4.values()), 'valid-full 4단계 10개 기본값')

    # classification 4.10(T-008): guard case-valid는 기본값 표시 5행.
    guard = data_dir / 'guard/case-valid'
    out = classification_bytes((guard / 'case.yaml').read_bytes(), 'case.yaml').decode('utf-8')
    expect(out.count(DEFAULT_MARK.strip()) == 5, 'classification 4.10 guard case-valid 기본값 표시 5행')

    # metrics 4.10: guard case-valid metrics.json의 SHA-256 앞자리와 크기.
    m = next(re.search(r'metrics\.json SHA-256 `([0-9a-f]+)…`, ([0-9,]+)바이트', line) for line in met_spec
             if 'xcheck-case-valid' in line)
    got = metrics_bytes(guard)
    expect(sha256_hex(got).startswith(m.group(1)) and len(got) == int(m.group(2).replace(',', '')),
           f'metrics 4.10 guard case-valid metrics.json {sha256_hex(got)[:8]} {len(got)}')

    # 오류 표본은 답을 내지 않고 멈춘다(범위 밖).
    for rel in ('case-schema/type-errors', 'case-schema/missing-required', 'case-schema/inferred-errors',
                'guard/case-invalid'):
        try:
            classification_bytes((data_dir / rel / 'case.yaml').read_bytes(), 'case.yaml')
            expect(False, f'오류 표본 {rel}: 멈추지 않았다')
        except OracleError:
            expect(True, '')
    try:
        metrics_bytes(data_dir / 'guard/compare-invalid')
        expect(False, '오류 표본 guard/compare-invalid: 멈추지 않았다')
    except OracleError:
        expect(True, '')


# ---- 줄바꿈·인코딩 변형 (metrics 3.3 [P:eol], classification 2절 [P:eol]) ----

def _csv_variants(data: bytes) -> dict:
    text = data.decode('utf-8')
    lines = text.split('\n')[:-1] if text.endswith('\n') else text.split('\n')
    quoted = '\n'.join(','.join('"' + c.replace('"', '""') + '"' for c in line.split(',')) for line in lines) + '\n'
    spaced = '\n'.join(','.join(' ' + c + '\t' for c in line.split(',')) for line in lines) + '\n'
    return {
        'crlf': text.replace('\n', '\r\n').encode('utf-8'),
        'cr': text.replace('\n', '\r').encode('utf-8'),
        'bom': b'\xef\xbb\xbf' + data,
        'quoted': quoted.encode('utf-8'),
        'no-final-newline': text.rstrip('\n').encode('utf-8'),
        'blank-lines-at-end': (text + '\n\n,,\n').encode('utf-8'),
        'spaces-around-cells': spaced.encode('utf-8'),
    }


def check_variants(repo: Path) -> None:
    with tempfile.TemporaryDirectory() as tmp:
        for name in ('table-compare', 'signoff-final', 'form-fill'):
            src = repo / 'examples' / name
            want = (src / 'expected/metrics.json').read_bytes()
            csvs = sorted(p.relative_to(src) for p in src.rglob('*.csv'))
            for label in _csv_variants(b'a\n'):
                work = Path(tmp) / f'{name}-{label}'
                shutil.copytree(src, work)
                for rel in csvs:
                    (work / rel).write_bytes(_csv_variants((src / rel).read_bytes())[label])
                expect(metrics_bytes(work) == want, f'CSV 변형 {name} {label}')
            ans = (src / 'answers.yaml').read_bytes()
            want_cls = (src / 'expected/classification.json').read_bytes()
            for label, data in (('crlf', ans.replace(b'\n', b'\r\n')), ('cr', ans.replace(b'\n', b'\r')),
                                ('bom', b'\xef\xbb\xbf' + ans), ('bom-crlf', b'\xef\xbb\xbf' + ans.replace(b'\n', b'\r\n'))):
                expect(classification_bytes(data, 'answers.yaml') == want_cls, f'answers.yaml 변형 {name} {label}')


# ---- check.py 음성 대조·결정론 ----

def run_check(repo: Path, report: Path, env=None):
    proc = subprocess.run([sys.executable, str(HERE / 'check.py'), '--repo', str(repo), '--json', str(report)],
                          capture_output=True, env=env)
    return proc.returncode, proc.stdout.decode('utf-8'), report.read_bytes() if report.exists() else b''


def statuses(stdout: str) -> dict:
    return {line.split(' ')[1]: line.split(' ')[0] for line in stdout.splitlines()}


def check_negative(repo: Path) -> None:
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        copy = tmp / 'repo'
        shutil.copytree(repo / 'examples', copy / 'examples')
        report = tmp / 'report.json'

        rc, out, _ = run_check(copy, report)
        base = statuses(out)
        expect(rc == 0 and len(base) == 8 and set(base.values()) == {'MATCH'}, f'음성 대조 기준선: rc={rc} {base}')

        def scenario(label, mutate, restore, want_path, want_status):
            mutate()
            try:
                rc2, out2, rep = run_check(copy, report)
                st = statuses(out2)
                others = {p: s for p, s in st.items() if p != want_path}
                expect(rc2 == 1, f'{label}: 종료 코드 {rc2}')
                expect(st.get(want_path) == want_status, f'{label}: {want_path} = {st.get(want_path)}')
                expect(set(others.values()) == {'MATCH'} and len(others) == 7, f'{label}: 다른 파일 {others}')
                expect(f'"status": "{want_status.lower()}"'.encode() in rep, f'{label}: 보고서 status')
                print(f'  {label}: rc={rc2} {want_status} {want_path}')
            finally:
                restore()

        target = copy / 'examples/table-compare/expected/metrics.json'
        original = target.read_bytes()
        flipped = bytearray(original)
        flipped[len(flipped) // 2] ^= 0x01
        scenario('expected 한 바이트', lambda: target.write_bytes(bytes(flipped)), lambda: target.write_bytes(original),
                 'examples/table-compare/expected/metrics.json', 'DIFF')

        gone = copy / 'examples/form-fill/expected/classification.json'
        kept = gone.read_bytes()
        scenario('expected 삭제', gone.unlink, lambda: gone.write_bytes(kept),
                 'examples/form-fill/expected/classification.json', 'MISSING')

        csv_path = copy / 'examples/signoff-final/candidate/records.csv'
        csv_orig = csv_path.read_bytes()
        expect(b'rec07,steady,814,' in csv_orig, '음성 대조 CSV 값 찾기')
        scenario('결과 CSV 값', lambda: csv_path.write_bytes(csv_orig.replace(b'rec07,steady,814,', b'rec07,steady,815,')),
                 lambda: csv_path.write_bytes(csv_orig), 'examples/signoff-final/expected/metrics.json', 'DIFF')

        ans = copy / 'examples/mixed-exceptions/answers.yaml'
        ans_orig = ans.read_bytes()
        old = b'current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}\n  - name: \xeb\xac\xb8\xec\x9d\x98'
        expect(old in ans_orig, '음성 대조 answers.yaml 답 찾기')
        scenario('answers.yaml 답(case.yaml은 그대로)',
                 lambda: ans.write_bytes(ans_orig.replace(old, old.replace(b'human_signoff: no', b'human_signoff: yes'))),
                 lambda: ans.write_bytes(ans_orig), 'examples/mixed-exceptions/expected/classification.json', 'DIFF')

        rc3, out3, _ = run_check(copy, report)
        expect(rc3 == 0 and statuses(out3) == base, '음성 대조 뒤 되돌리면 다시 모두 MATCH')


def check_determinism(repo: Path) -> None:
    with tempfile.TemporaryDirectory() as tmp:
        outs = set()
        for k, extra in enumerate([
            {},
            {'LANG': 'de_DE.UTF-8', 'LC_ALL': 'de_DE.UTF-8', 'PYTHONHASHSEED': '1'},
            {'LANG': 'tr_TR.UTF-8', 'LC_ALL': 'tr_TR.UTF-8', 'PYTHONHASHSEED': '12345', 'TZ': 'Asia/Seoul'},
            {'LANG': 'C', 'LC_ALL': 'C', 'PYTHONIOENCODING': 'latin-1', 'PYTHONHASHSEED': 'random'},
        ]):
            env = {**os.environ, **extra}
            rc, out, rep = run_check(repo, Path(tmp) / f'r{k}.json', env=env)
            outs.add((rc, out, rep))
        expect(len(outs) == 1, f'결정론: 환경 4가지에서 출력이 {len(outs)}가지')


def main(argv: list) -> int:
    utf8_stdio()
    ap = argparse.ArgumentParser(description='oracle 자기 점검(스펙 표 대조, 음성 대조, 결정론)')
    ap.add_argument('--repo', default=str(HERE.parents[1]))
    repo = Path(ap.parse_args(argv).repo)
    cls_spec = (repo / 'docs/spec/classification.md').read_text(encoding='utf-8').split('\n')
    met_spec = (repo / 'docs/spec/metrics.md').read_text(encoding='utf-8').split('\n')

    check_classification(cls_spec)
    check_signoff_inferred(repo, cls_spec)
    check_metrics_tables(met_spec)
    check_testdata(repo, cls_spec, met_spec)
    check_variants(repo)
    check_negative(repo)
    check_determinism(repo)

    print(f"selftest: 통과 {COUNT['ok']}, 실패 {len(FAILS)}")
    return 1 if FAILS else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
