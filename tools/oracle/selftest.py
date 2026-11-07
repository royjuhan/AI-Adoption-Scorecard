#!/usr/bin/env python3
# oracle 자기 점검. oracle을 고친 prototyper가 돌린다. CI도 돌린다(T-011 수용 기준 7): tools/oracle이 바뀐 PR에서
# oracle-head 잡과 머지 전 tools/ci/lead_check.py가 head의 이 파일을 `python3 -I -B tools/oracle/selftest.py --repo <checkout>`로
# 돌린다. base의 이 파일은 어디서도 돌리지 않는다(oracle 잡은 base check.py만). 실패하면 그 잡이 실패한다.
# 출력 줄 모양은 check.py의 CI 계약과 따로다. 저장소의 docs/spec·examples·TestData를 읽으므로 --repo로 checkout을 준다.
#
#   python3 tools/oracle/selftest.py [--repo <루트>]
#
# 1. 스펙 표·블록 대조: oracle이 스펙 문서의 예시 표와 같은 답을 내는지 본다. 이 점검만 스펙 문서를 읽는다.
#    classification.md 6.1 분기 표, 6.2 기본값 표시 표·"답 없는 단계" 블록, 6.5 TestData 표본(추정 표시).
#    metrics.md 4.1 패턴 표 2개, 4.5 숫자 모양·칸 판정 표, 4.6 앞뒤 글자 표, 4.8 퍼센트 표.
#    TestData: valid-full 값, guard case-valid 분류(기본값 표시 5행)·metrics(4.10 SHA-256·크기), 오류 표본은 멈춤.
#    judgment.md 3.2 속성 표, 4.2~4.7 표(상수 = 표), 4.8 예제 열 표, 6.1~6.5 표(합성 장면), 7.1 예제 표,
#    7.2~7.5 블록·SHA-256 줄(oracle 출력 = 블록), 4.12 G2 표본 판정.
#    변형: 예제 CSV의 CRLF·CR·BOM·따옴표·마지막 줄바꿈 없음·끝 빈 행·칸 앞뒤 공백, answers.yaml의 CRLF·CR·BOM이 같은 바이트.
# 2. 음성 대조: 예제 사본에서 expected 한 바이트, expected 삭제, 결과 CSV 값, answers.yaml 답을 바꾸면
#    check.py가 DIFF·MISSING과 종료 코드 1을 낸다. 사본 그대로면 모두 MATCH, 종료 코드 0.
#    파일 수는 고정하지 않는다. 사본 examples의 expected 파일 목록(check.py 대상 종류)에서 센다.
#    judgment.json: 사본에 7.2~7.5 블록을 넣으면 그 종류가 대상이 되고 모두 MATCH. 한 바이트, 한 파일 삭제,
#    case.yaml 문턱을 바꾸면 judgment.json만 DIFF·MISSING. 모두 지우면 대상에서 빠진다.
# 3. 결정론: 문화권·해시 시드 환경 변수를 바꿔 check.py를 돌려도 표준 출력·보고서 바이트가 같다(judgment.json 사본 포함).
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

import json  # noqa: E402

from check import OPTIONAL_TARGETS, TARGETS  # noqa: E402
from classify import DEFAULT_MARK, INFERRED_MARK, classification_bytes, classify, to_json_value  # noqa: E402
from compare import DEC_MAX, compare_run, is_supported_pattern, judge, matches, metrics_bytes, parse_number, percent  # noqa: E402
from judge import (CHECK_TABLE, CHECK_THRESHOLD, FOCUS_MIN, READ_THRESHOLDS, THRESHOLD_DEFAULTS, TODO_TABLE,  # noqa: E402
                   VERDICT_TABLE, Times, _type_columns, judgment_bytes, judgment_value)
from judge import DEFAULT_MARK as JUDGMENT_MARK  # noqa: E402
from judge import judge as judge_case  # noqa: E402
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


# ---- 판정 점검 (judgment.md) ----

EXAMPLE_NAMES = ('table-compare', 'form-fill', 'mixed-exceptions', 'signoff-final')
J_BASE = {'accuracy_min': '0', 'missing_max': '100', 'time_must_drop': 'false'}  # 정확도·누락률이 늘 met인 문턱


def tick(text: str) -> str:
    return text.strip('`')


def judgment_block(spec: list, name: str):
    """7.2~7.5: (블록 바이트, SHA-256 줄의 값)."""
    marker = f'`examples/{name}/expected/judgment.json` (SHA-256 `'
    line = next(x for x in spec if x.startswith(marker))
    sha = re.search(r'SHA-256 `([0-9a-f]{64})`', line).group(1)
    return ('\n'.join(block_after(spec, marker)) + '\n').encode('utf-8'), sha


class Scenes:
    """합성 케이스 폴더(judgment.md 8절 Build 도우미와 같은 모양: 머리 행 id,<유형 열>,v, compare는 v tolerance: 0).

    값 불일치는 결과 v를 바꾸고, 누락은 결과 행을 뺀다. 저장소 밖 임시 폴더에만 쓴다.
    """

    def __init__(self, root: Path):
        self.root = root
        self.n = 0

    def make(self, n: int, cols=(), missing=(), mismatch=(), extra=(), thresholds=None, *, inputs=True, compare=True,
             ref_files=None) -> Path:
        """cols: [(열 이름, 행마다 값)]. extra: 추가 행의 첫 유형 열 값. thresholds: {키: 글}(None이면 키 없음)."""
        self.n += 1
        folder = self.root / f's{self.n:04d}'
        (folder / 'reference').mkdir(parents=True)
        (folder / 'candidate').mkdir()
        out = ['name: selftest', 'task: selftest 합성 판정', 'steps:', '  - name: 단계']
        if inputs:
            out += ['inputs:', '  reference: reference/*.csv', '  candidate: candidate/*.csv', '  key: id']
        if compare:
            out += ['compare:', '  - {field: v, tolerance: 0}']
        if thresholds is not None:
            out += ['thresholds:'] + [f'  {k}: {v}' for k, v in thresholds.items()]
        (folder / 'case.yaml').write_bytes(('\n'.join(out) + '\n').encode('utf-8'))
        if ref_files is not None:  # 여러 기준 파일: {이름: 바이트}. 결과 자료는 ref_files에 맞춰 부르는 쪽이 준다.
            for name, data in ref_files.items():
                (folder / 'reference' / name).write_bytes(data)
            return folder
        header = ['id'] + [c for c, _ in cols] + ['v']
        ref = [header] + [[f'r{i:04d}'] + [vals[i] for _, vals in cols] + ['1'] for i in range(n)]
        cand = [header] + [[f'r{i:04d}'] + [vals[i] for _, vals in cols] + ['2' if i in mismatch else '1']
                           for i in range(n) if i not in missing]
        cand += [[f'x{j:03d}'] + [(t if k == 0 else 'z') for k, _ in enumerate(cols)] + ['1'] for j, t in enumerate(extra)]
        for side, rows in (('reference', ref), ('candidate', cand)):
            (folder / side / 'a.csv').write_bytes(('\n'.join(','.join(r) for r in rows) + '\n').encode('utf-8'))
        return folder


def alternate(bad: list):
    """불일치 행 번호 → (누락, 값 불일치). 누락부터 번갈아 둔다(6.3 "번갈아")."""
    return set(bad[0::2]), set(bad[1::2])


def judge_folder(folder: Path, times=None, unexplained=None):
    case = parse_case((folder / 'case.yaml').read_bytes(), 'case.yaml')
    run = compare_run(folder, case)
    return judge_case(case, run, times, unexplained), case, run


def results_of(res: dict) -> dict:
    return {c['rule']: c['result'] for c in res['checks']}


def bad_keys_of(run) -> list:
    return [it['key'] for it in run.body['items'] if it['status'] in ('mismatch', 'missing')]


def expect_refused(label: str, fn) -> None:
    try:
        fn()
        expect(False, f'{label}: 멈추지 않았다')
    except OracleError:
        expect(True, '')


def check_judgment_tables(spec: list) -> None:
    """상수 = 스펙 표(4.2~4.7). oracle은 실행 중에 스펙을 읽지 않으므로 여기서 대조한다."""
    rows = table(spec, '| 키 | 기본값 | 근거 |')
    expect([(tick(k), tick(v)) for k, v, _ in rows] == list(THRESHOLD_DEFAULTS), '4.2 기본값 표 = THRESHOLD_DEFAULTS')
    rows = table(spec, '| 순서 | 근거 키 | 걸리는 결과 | 판정 | 판정 문장 틀 |')
    got = [(int(o), tick(k), None if r == '—' else tick(r), tick(v), tick(t)) for o, k, r, v, t in rows]
    expect(got == list(VERDICT_TABLE), '4.3 판정 표 = VERDICT_TABLE')
    rows = table(spec, '| 확인 | 결과 | 조건 | 확인 문장 틀 |')
    expect([(tick(c), tick(r), tick(t)) for c, r, _cond, t in rows] == list(CHECK_TABLE), '4.4 확인 표 = CHECK_TABLE')
    rows = table(spec, '| 이름 | 값 | 뜻 |')
    expect([(tick(k), int(tick(v))) for k, v, _ in rows] == [('focus_min', FOCUS_MIN)], '4.5 숫자 표 = FOCUS_MIN')
    rows = table(spec, '| 근거 키 | 읽은 문턱 |')
    got = {tick(k): tuple(spans(v)) for k, v in rows}
    expect(got == READ_THRESHOLDS and list(got) == list(READ_THRESHOLDS), '4.6 읽은 문턱 표 = READ_THRESHOLDS')
    line = next(x for x in spec if x.startswith('- 확인 문장: '))
    expect(dict(re.findall(r'`([A-Z]+)` → `([a-z_]+)`', line)) == CHECK_THRESHOLD, '4.6 확인 문장 문턱 = CHECK_THRESHOLD')
    line = next(x for x in spec if x.startswith('- 표시 글자는 '))
    expect('공백 하나 + `' + JUDGMENT_MARK[1:] + '`' in line and JUDGMENT_MARK.startswith(' '), '4.6 표시 글자')
    rows = table(spec, '| 다음 할 일 | 언제 | 문구 |')
    expect([(tick(k), tick(t)) for k, _w, t in rows] == list(TODO_TABLE), '4.7 다음 할 일 표 = TODO_TABLE')


def check_judgment_examples(repo: Path, spec: list) -> None:
    # 7.2~7.5: oracle 출력 = 스펙 블록 = SHA-256 줄. 예제 파일이 이미 있으면(PR (b) 뒤) 그것도 같아야 한다.
    for name in EXAMPLE_NAMES:
        block, sha = judgment_block(spec, name)
        got = judgment_bytes(repo / 'examples' / name)
        expect(got == block, f'7절 {name}: oracle 출력 = 스펙 블록')
        expect(sha256_hex(block) == sha, f'7절 {name}: 블록 SHA-256 = SHA-256 줄')
        expect(sha256_hex(got) == sha, f'7절 {name}: oracle SHA-256 = SHA-256 줄')
        print(f'  judgment {name}: oracle {sha256_hex(got)} spec {sha} {"same" if got == block else "DIFF"}')
        file = repo / 'examples' / name / 'expected/judgment.json'
        if file.exists():
            expect(file.read_bytes() == block, f'7절 {name}: expected/judgment.json = 스펙 블록')

    # 7.1 예제 표.
    for name, verdict, rule, checks, todo, _s12 in table(spec, '| 예제 | 판정 | 근거 키 | 확인 다섯 | 다음 할 일 | 12절 |'):
        value = json.loads(judgment_bytes(repo / 'examples' / tick(name)))
        expect(value['verdict'] == tick(verdict) and value['rule'] == tick(rule), f'7.1 {name} 판정·근거 키')
        expect([c['result'] for c in value['checks']] == ([] if checks == '—' else spans(checks)), f'7.1 {name} 확인 다섯')
        expect([t['key'] for t in value['todo']] == ([] if todo == '—' else spans(todo)), f'7.1 {name} 다음 할 일')

    # 3.2 속성 표: 경로가 table-compare 출력에 처음 나오는 순서, 형식.
    kinds = {'정수': lambda v: type(v) is int, '문자열': lambda v: isinstance(v, str), '객체': lambda v: isinstance(v, dict),
             '숫자': lambda v: type(v) in (int, float), '불리언': lambda v: isinstance(v, bool),
             '문자열 배열': lambda v: isinstance(v, list) and all(isinstance(x, str) for x in v),
             '객체 배열': lambda v: isinstance(v, list) and all(isinstance(x, dict) for x in v)}
    rows = table(spec, '| 속성 | 형식 | 뜻 |')
    seen: dict = {}

    def walk(value, prefix: str) -> None:
        for k, v in value.items():
            path = prefix + k
            seen.setdefault(path, []).append(v)
            if isinstance(v, dict):
                walk(v, path + '.')
            elif isinstance(v, list):
                for x in v:
                    if isinstance(x, dict):  # 표는 객체 원소만 '[]'로 적는다(문자열 배열 defaults의 원소는 없다).
                        seen.setdefault(path + '[]', []).append(x)
                        walk(x, path + '[].')

    for name in EXAMPLE_NAMES:
        before = list(seen)
        walk(json.loads(judgment_bytes(repo / 'examples' / name)), '')
        if name == 'table-compare':
            expect(list(seen) == [tick(r[0]) for r in rows], f'3.2 속성 경로 순서 = 표 ({list(seen)})')
        else:
            expect(list(seen)[:len(before)] == before and len(seen) == len(before), f'3.2 {name}: 표 밖의 속성이 없다')
    for path, form, _ in rows:
        expect(all(kinds[form](v) for v in seen.get(tick(path), [])), f'3.2 {path} 형식 {form}')
    first = judgment_bytes(repo / 'examples/table-compare')
    expect(first.startswith(b'{\n  "schema_version": 1,\n'), '3.2 첫 키 schema_version = 정수 1')

    # 4.8 예제 열 표: 유형 열(규칙 1·2).
    for name, header, key, _cmp, rest in table(spec, '| 예제 | 기준 머리 행 | 키 | `compare` | 남는 열(유형 열) |'):
        folder = repo / 'tests/Scorecard.Tests/TestData/guard/case-valid' if 'case-valid' in name else repo / 'examples' / name
        case = parse_case((folder / 'case.yaml').read_bytes(), 'case.yaml')
        run = compare_run(folder, case)
        expect([','.join(h) for _f, h in run.reference_headers] == [tick(header)], f'4.8 {name} 기준 머리 행')
        expect(case.inputs.key == tick(key), f'4.8 {name} 키')
        expect(_type_columns(case, run) == ([] if rest == '없음' else spans(rest)), f'4.8 {name} 유형 열')

    # 4.12: G2 표본 case-valid 판정.
    value = json.loads(judgment_bytes(repo / 'tests/Scorecard.Tests/TestData/guard/case-valid'))
    res = {c['rule']: c['result'] for c in value['checks']}
    expect(value['verdict'] == 'hold' and value['rule'] == 'ACCURACY' and '66.7%' in value['reason'] and '90%' in value['reason'],
           '4.12 G2 표본: 보류 ACCURACY(66.7% < 90)')
    expect(res['TIME'] == 'not_measured' and res['FOCUS'] == 'off' and len(value['todo']) == 3, '4.12 G2 표본: 시간·몰림·할 일 셋')
    expect_refused('대조 오류 표본 guard/compare-invalid 판정',
                   lambda: judgment_bytes(repo / 'tests/Scorecard.Tests/TestData/guard/compare-invalid'))


def check_judgment_scenes(spec: list) -> None:
    with tempfile.TemporaryDirectory() as tmp:
        sc = Scenes(Path(tmp))

        # 6.1 문턱 경계: 셈으로 비교. 표시 퍼센트도 표와 같다.
        head = '| 기준 행 | 일치 | 누락 | 표시 정확도(%) | accuracy_min | ACCURACY | 표시 누락률(%) | missing_max | MISSING |'
        for ref, match, miss, acc_pct, acc_min, acc_res, miss_pct, miss_max, miss_res in table(spec, head):
            n, m, ms = int(ref), int(match), int(miss)
            folder = sc.make(n, missing=set(range(ms)), mismatch=set(range(ms, n - m)),
                             thresholds={'accuracy_min': acc_min, 'missing_max': miss_max, 'time_must_drop': 'false'})
            res, _case, run = judge_folder(folder)
            got = results_of(res)
            expect(run.body['accuracy_pct'].text == acc_pct and run.body['missing_rate_pct'].text == miss_pct,
                   f'6.1 {ref}/{match}/{miss} 표시 퍼센트')
            expect(got['ACCURACY'] == tick(acc_res) and got['MISSING'] == tick(miss_res),
                   f'6.1 {ref}/{match}/{miss} {acc_min}·{miss_max}: {got["ACCURACY"]}, {got["MISSING"]}')

        # 3.3 decimal 자릿수: 문턱은 case.yaml 글 그대로.
        folder = sc.make(3, mismatch={0}, thresholds={'accuracy_min': '66.6', 'missing_max': '0.0', 'time_must_drop': 'false'})
        res, case, run = judge_folder(folder)
        out = render(judgment_value(case, run))
        expect(b'"accuracy_min": 66.6,' in out and b'"missing_max": 0.0,' in out and '문턱 66.6% 이상'.encode() in out,
               '3.3 문턱 자릿수 66.6·0.0')

        # 6.2 시간.
        for tmd, before, proc, rev, want in table(spec, '| time_must_drop | 적용 전 | 처리 | 검토 | TIME |'):
            folder = sc.make(10, thresholds={**J_BASE, 'time_must_drop': tick(tmd)})
            times = None if before == '없음' else Times(Decimal(before), Decimal(proc), Decimal(rev))
            res, _c, _r = judge_folder(folder, times)
            expect(results_of(res)['TIME'] == tick(want), f'6.2 {tmd} {before} {proc} {rev}: {results_of(res)["TIME"]}')
        folder = sc.make(10, thresholds={**J_BASE, 'time_must_drop': 'true'})
        res, _c, _r = judge_folder(folder, Times(Decimal('10'), Decimal('1.25'), Decimal('1.2')))
        expect('= 2.45분이' in res['checks'][3]['reason'], '4.5 {total}: 1.25 + 1.2 = 2.45')
        res, _c, _r = judge_folder(folder, Times(Decimal('7'), Decimal('7'), Decimal('3')))
        expect(res['rule'] == 'TIME' and '처리·검토 시간 10분이 적용 전 7분보다 짧지 않으므로' in res['reason'], '4.3 TIME not_met 문장')
        expect_refused('5절 시간 음수', lambda: judge_folder(folder, Times(Decimal('10'), Decimal('-1'), Decimal('0'))))
        expect_refused('4.4 시간 합 넘침({total} 모양 미정)', lambda: judge_folder(folder, Times(Decimal('1'), DEC_MAX, Decimal('1'))))

        # 6.3 몰림(유형 열 하나).
        def expand(cell: str) -> list:
            out = []
            for s in ([] if cell == '(없음)' else spans(cell)):
                v, _, cnt = s.partition('×')
                out += [cell_value(v)] * (int(cnt) if cnt else 1)
            return out

        for ref_t, bad_t, extra_t, has, want in table(spec, '| 기준 행 유형 | 불일치 행 유형 | 추가 행 유형 | 유형 열 | FOCUS |'):
            groups, used, bad = expand(ref_t), set(), []
            for t in expand(bad_t):
                i = next(k for k, g in enumerate(groups) if g == t and k not in used)
                used.add(i)
                bad.append(i)
            missing, mismatch = alternate(bad)
            cols = [('grp', groups)] if has == '있음' else []
            folder = sc.make(len(groups), cols, missing, mismatch, expand(extra_t), J_BASE)
            res, _c, _r = judge_folder(folder)
            expect(results_of(res)['FOCUS'] == tick(want), f'6.3 {ref_t} | {bad_t} | {extra_t} | {has}: {results_of(res)["FOCUS"]}')

        # 6.3 유형 열 여럿.
        for col_t, bad_t, want, hit, shown in table(spec, '| 열 값(행 1~6) | 불일치 행 | FOCUS | 걸린 열 | 본 열 |'):
            cols = []
            for s in spans(col_t):
                name, vals = s.split('=')
                cols.append((name, ['' if c == '_' else c for c in vals]))
            missing, mismatch = alternate([int(x) - 1 for x in tick(bad_t).split(',')])
            res, _c, _r = judge_folder(sc.make(6, cols, missing, mismatch, (), J_BASE))
            focus = res['checks'][4]
            label = f'6.3 {col_t} | {bad_t}'
            expect(focus['result'] == tick(want), f'{label}: {focus["result"]}')
            if hit != '—':
                expect(f'모두 {tick(hit)}인 행이다(' in focus['reason'], f'{label}: 걸린 열 {focus["reason"]}')
            if shown != '—':
                text = f'(본 열: {tick(shown)})' if focus['result'] == 'spread' else f'열({tick(shown)})이'
                expect(text in focus['reason'], f'{label}: 본 열 {focus["reason"]}')

        # 4.8 규칙 1: 여러 기준 파일. 한 파일에만 있는 열은 유형 열이 아니다. 열 순서가 달라도 된다.
        a = 'id,grp,v\nr1,a,1\nr2,a,1\nr3,b,1\n'.encode()
        b = 'id,v,only_b,grp\nr4,1,q,b\nr5,1,q,b\nr6,1,w,b\n'.encode()
        folder = sc.make(0, thresholds=J_BASE, ref_files={'a.csv': a, 'b.csv': b})
        (folder / 'candidate/a.csv').write_bytes('id,v\nr2,2\nr3,1\nr4,1\nr5,1\nr6,1\n'.encode())
        res, case, run = judge_folder(folder)
        expect(_type_columns(case, run) == ['grp'], '4.8 여러 기준 파일: 유형 열 grp만')
        expect(res['checks'][4]['result'] == 'found' and '모두 grp = a인 행이다(a 2행 중 2행, 다른 값 4행 중 0행)' in res['checks'][4]['reason'],
               f'4.8 여러 기준 파일: found {res["checks"][4]["reason"]}')
        # 빈 이름 열은 유형 열이 아니다.
        res, case, run = judge_folder(sc.make(4, [('', ['a', 'a', 'b', 'b'])], {0}, {1}, (), J_BASE))
        expect(_type_columns(case, run) == [] and res['checks'][4]['result'] == 'off', '4.8 빈 이름 열은 유형 열이 아니다')

        # 5절 이스케이프·표시: 큰따옴표는 JSON에서 \\". 제어 문자·64자 넘는 값은 표시 모양을 스펙이 정하지 않아 멈춘다.
        res, case, run = judge_folder(sc.make(4, [('grp', ['a"b', 'a"b', 'c', 'c'])], {0}, {1}, (), J_BASE))
        expect(b'= a\\"b' in render(judgment_value(case, run)), '3.3 유형 값의 큰따옴표 \\"')
        expect_refused('4.5 제어 문자 값', lambda: judge_folder(sc.make(4, [('grp', ['a\x01', 'a\x01', 'c', 'c'])], {0}, {1}, (), J_BASE)))
        long_value = 'x' * 65
        expect_refused('4.5 65자 값', lambda: judge_folder(sc.make(4, [('grp', [long_value] * 2 + ['c'] * 2)], {0}, {1}, (), J_BASE)))
        res, _c, _r = judge_folder(sc.make(4, [('grp', ['y' * 64] * 2 + ['c'] * 2)], {0}, {1}, (), J_BASE))
        expect(('= ' + 'y' * 64 + '인') in res['reason'], '4.5 64자 값은 그대로')

        # 6.4 설명 안 되는 불일치.
        for nbad, given, want in table(spec, '| 불일치 | 사람 입력 | UNEXPLAINED |'):
            missing, mismatch = alternate(list(range(int(nbad))))
            folder = sc.make(10, missing=missing, mismatch=mismatch, thresholds=J_BASE)
            _res, _c, run = judge_folder(folder)
            keys = bad_keys_of(run)
            human = None if given == '없음' else [] if given == '`[]`' else keys[:int(given.rstrip('건'))]
            res, _c, _r = judge_folder(folder, None, human)
            expect(results_of(res)['UNEXPLAINED'] == tick(want), f'6.4 {nbad} {given}: {results_of(res)["UNEXPLAINED"]}')
            if tick(want) == 'found':
                expect(res['rule'] == 'UNEXPLAINED' and f'불일치가 {len(human)}건 있으므로 보류.' in res['reason'], f'6.4 {nbad} {given} 판정')
        folder = sc.make(10, missing={0}, mismatch={1}, thresholds=J_BASE)
        expect_refused('5절 불일치가 아닌 키', lambda: judge_folder(folder, None, ['r0009']))
        expect_refused('5절 중복 키', lambda: judge_folder(folder, None, ['r0000', 'r0000']))

        # 6.5 우선순위: 확인 다섯을 표대로 만드는 장면을 짓고 판정을 본다.
        head = '| ACCURACY | MISSING | UNEXPLAINED | TIME | FOCUS | 판정 | 근거 키 |'
        for acc, miss, unexp, tim, focus, verdict, rule in table(spec, head):
            acc, miss, unexp, tim, focus = map(tick, (acc, miss, unexp, tim, focus))
            bad = [] if unexp == 'none' else [0] if focus == 'few' else [0, 5] if focus == 'spread' else [0, 1]
            cols = [] if focus == 'off' else [('grp', ['a'] * 10 if focus == 'one_value' else ['a'] * 5 + ['b'] * 5)]
            missing, mismatch = alternate(bad)
            th = {'accuracy_min': '0' if acc == 'met' else '100', 'missing_max': '100' if miss == 'met' else '0',
                  'time_must_drop': 'false' if tim == 'off' else 'true'}
            folder = sc.make(10, cols, missing, mismatch, (), th)
            times = {'met': Times(Decimal(10), Decimal(6), Decimal(3)), 'not_met': Times(Decimal(10), Decimal(7), Decimal(3))}.get(tim)
            human = {'explained': [], 'found': [f'r{bad[0]:04d}'] if bad else None}.get(unexp)
            res, _c, _r = judge_folder(folder, times, human)
            got = results_of(res)
            label = f'6.5 {acc} {miss} {unexp} {tim} {focus}'
            expect([got[k] for k in ('ACCURACY', 'MISSING', 'UNEXPLAINED', 'TIME', 'FOCUS')] == [acc, miss, unexp, tim, focus],
                   f'{label}: 장면 {got}')
            expect((res['verdict'], res['rule']) == (tick(verdict), tick(rule)), f'{label}: {res["verdict"]} {res["rule"]}')
            # 4.7 "언제" 칸: 측정한 판정의 다음 할 일.
            want_todo = [k for k, cond in (('measure_time', got['TIME'] == 'not_measured'),
                                           ('review_mismatches', got['UNEXPLAINED'] == 'not_reviewed'),
                                           ('add_type_column', got['FOCUS'] == 'off')) if cond]
            expect([t['key'] for t in res['todo']] == want_todo, f'{label}: 다음 할 일 {[t["key"] for t in res["todo"]]}')
            if 'review_mismatches' in want_todo:
                expect(res['todo'][want_todo.index('review_mismatches')]['text'] == f'불일치 {len(bad)}건을 보고 설명이 되는지 적는다.',
                       f'{label}: review_mismatches 문구')

        # 4.6 기본값 표시와 4.7 confirm_thresholds: 기본값과 같은 값을 적은 장면에서 키 하나씩 뺀다(판정은 그대로).
        read = {tick(k): tuple(spans(v)) for k, v in table(spec, '| 근거 키 | 읽은 문턱 |')}
        defaults = dict(THRESHOLD_DEFAULTS)
        full = {k: v for k, v in THRESHOLD_DEFAULTS}
        g5 = [('grp', ['a'] * 50 + ['b'] * 50)]
        met_time = Times(Decimal(10), Decimal(6), Decimal(3))
        plans = {  # 근거 키 → (행 수, 유형 열, 누락, 값 불일치, 시간, 사람 입력)
            'ACCURACY': (10, (), {0}, {1}, None, None),
            'MISSING': (100, (), {0, 1, 2}, set(), None, None),
            'UNEXPLAINED': (100, (), {0}, {1}, None, ['r0000']),
            'TIME': (100, (), {0}, {1}, None, None),
            'FOCUS': (100, g5, {0}, {1}, met_time, None),
            'PASS': (100, (), set(), set(), met_time, None),
        }
        for rule, (n, cols, missing, mismatch, times, human) in plans.items():
            for drop in (None,) + tuple(defaults):
                th = {k: v for k, v in full.items() if k != drop}
                res, _c, _r = judge_folder(sc.make(n, cols, missing, mismatch, (), th), times, human)
                label = f'4.6 {rule} 기본값 {drop}'
                expect(res['rule'] == rule, f'{label}: 근거 키 {res["rule"]}')
                expect(res['reason'].endswith(JUDGMENT_MARK) == (drop in read[rule]), f'{label}: 판정 문장 표시')
                for c in res['checks']:
                    expect(c['reason'].endswith(JUDGMENT_MARK) == (drop is not None and CHECK_THRESHOLD.get(c['rule']) == drop),
                           f'{label}: {c["rule"]} 확인 문장 표시')
                expect(all(x['reason'].count(JUDGMENT_MARK) <= 1 for x in [res] + res['checks']), f'{label}: 표시 한 번')
                expect(res['defaults'] == ([] if drop is None else [drop]), f'{label}: defaults {res["defaults"]}')
                conf = [t['text'] for t in res['todo'] if t['key'] == 'confirm_thresholds']
                want = [] if drop is None else [f'문턱에 기본값을 썼다({drop} {defaults[drop]}). 맞는지 확인하고 case.yaml thresholds에 적는다.']
                expect(conf == want, f'{label}: confirm_thresholds {conf}')

        # 미검증(4.3 1행, 4.7): 판정 문장은 문턱을 읽지 않는다. thresholds가 없으면 셋 다 기본값.
        for inputs, reason, want_todo in ((False, 'no_inputs', ['add_data', 'add_compare', 'measure_time', 'confirm_thresholds']),
                                          (True, 'no_compare_fields', ['add_compare', 'measure_time', 'confirm_thresholds'])):
            res, _c, _r = judge_folder(sc.make(1, inputs=inputs, compare=False))
            label = f'미검증 {reason}'
            expect(res['verdict'] == 'not_verified' and res['rule'] == 'NOT_VERIFIED' and res['checks'] == [], f'{label}: 판정')
            expect(res['reason'] == next(r[4] for r in VERDICT_TABLE if r[2] == reason), f'{label}: 문장(표시 없음)')
            expect(res['defaults'] == [k for k, _ in THRESHOLD_DEFAULTS], f'{label}: defaults 4.2 순서')
            expect([t['key'] for t in res['todo']] == want_todo, f'{label}: 다음 할 일 {[t["key"] for t in res["todo"]]}')
            res, _c, _r = judge_folder(sc.make(1, inputs=inputs, compare=False, thresholds={**J_BASE, 'time_must_drop': 'false'}))
            expect([t['key'] for t in res['todo']] == want_todo[:-2], f'{label}: time_must_drop false면 measure_time 없음')
            res, _c, _r = judge_folder(sc.make(1, inputs=inputs, compare=False, thresholds={'missing_max': '3'}))
            expect(res['defaults'] == ['accuracy_min', 'time_must_drop'] and
                   res['todo'][-1]['text'].startswith('문턱에 기본값을 썼다(accuracy_min 95, time_must_drop true).'),
                   f'{label}: 일부만 적으면 빈 키만 기본값')

        # 4.12 결정론·원본 불변: 같은 입력 두 번 같은 바이트, 판정 뒤 metrics.json 바이트 그대로.
        folder = sc.make(10, [('grp', ['a'] * 5 + ['b'] * 5)], {0}, {1}, (), J_BASE)
        m1 = metrics_bytes(folder)
        j1, j2 = judgment_bytes(folder), judgment_bytes(folder)
        expect(j1 == j2 and metrics_bytes(folder) == m1, '4.12 같은 입력 같은 바이트, metrics.json 그대로')


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
            want_judgment = judgment_bytes(src)
            csvs = sorted(p.relative_to(src) for p in src.rglob('*.csv'))
            for label in _csv_variants(b'a\n'):
                work = Path(tmp) / f'{name}-{label}'
                shutil.copytree(src, work)
                for rel in csvs:
                    (work / rel).write_bytes(_csv_variants((src / rel).read_bytes())[label])
                expect(metrics_bytes(work) == want, f'CSV 변형 {name} {label}')
                expect(judgment_bytes(work) == want_judgment, f'CSV 변형 {name} {label} judgment.json')
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


def expected_files(examples: Path) -> list:
    """사본 examples의 expected 파일 가운데 check.py 대상 종류인 것. 고정 수를 쓰지 않으려는 것이다(T-011 수용 기준 7)."""
    out = []
    for folder in sorted((p for p in examples.iterdir() if p.is_dir()), key=lambda p: p.name):
        for kind in TARGETS + OPTIONAL_TARGETS:
            if (folder / 'expected' / kind).is_file():
                out.append(f'examples/{folder.name}/expected/{kind}')
    return out


def scenario(copy: Path, report: Path, base: dict, label: str, mutate, restore, want: dict) -> None:
    """mutate 뒤 check.py: want의 경로는 그 상태, 나머지 base 경로는 모두 MATCH, 종료 코드 1. 끝나면 restore."""
    mutate()
    try:
        rc2, out2, rep = run_check(copy, report)
        st = statuses(out2)
        expect(rc2 == 1, f'{label}: 종료 코드 {rc2}')
        expect(set(st) == set(base), f'{label}: 보고한 파일 {sorted(set(st) ^ set(base))}')
        for path, status in want.items():
            expect(st.get(path) == status, f'{label}: {path} = {st.get(path)}')
            expect(f'"status": "{status.lower()}"'.encode() in rep, f'{label}: 보고서 status')
        others = {p: x for p, x in st.items() if p not in want}
        expect(set(others.values()) <= {'MATCH'} and len(others) == len(base) - len(want), f'{label}: 다른 파일 {others}')
        print(f'  {label}: rc={rc2} ' + ', '.join(f'{x} {p}' for p, x in want.items()))
    finally:
        restore()


def check_baseline(copy: Path, report: Path, label: str) -> dict:
    rc, out, _ = run_check(copy, report)
    base = statuses(out)
    files = expected_files(copy / 'examples')
    expect(rc == 0 and files and set(base) == set(files) and len(base) == len(files) and set(base.values()) == {'MATCH'},
           f'{label}: rc={rc} 파일 {len(files)}개, 보고 {base}')
    print(f'  {label}: rc={rc} {len(base)} MATCH')
    return base


def check_negative(repo: Path) -> None:
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        copy = tmp / 'repo'
        shutil.copytree(repo / 'examples', copy / 'examples')
        report = tmp / 'report.json'
        base = check_baseline(copy, report, '음성 대조 기준선')

        target = copy / 'examples/table-compare/expected/metrics.json'
        original = target.read_bytes()
        flipped = bytearray(original)
        flipped[len(flipped) // 2] ^= 0x01
        scenario(copy, report, base, 'expected 한 바이트', lambda: target.write_bytes(bytes(flipped)),
                 lambda: target.write_bytes(original), {'examples/table-compare/expected/metrics.json': 'DIFF'})

        gone = copy / 'examples/form-fill/expected/classification.json'
        kept = gone.read_bytes()
        scenario(copy, report, base, 'expected 삭제', gone.unlink, lambda: gone.write_bytes(kept),
                 {'examples/form-fill/expected/classification.json': 'MISSING'})

        csv_path = copy / 'examples/signoff-final/candidate/records.csv'
        csv_orig = csv_path.read_bytes()
        expect(b'rec07,steady,814,' in csv_orig, '음성 대조 CSV 값 찾기')
        csv_want = {'examples/signoff-final/expected/metrics.json': 'DIFF'}
        if 'examples/signoff-final/expected/judgment.json' in base:  # 셈이 바뀌어 판정 문장도 바뀐다.
            csv_want['examples/signoff-final/expected/judgment.json'] = 'DIFF'
        scenario(copy, report, base, '결과 CSV 값',
                 lambda: csv_path.write_bytes(csv_orig.replace(b'rec07,steady,814,', b'rec07,steady,815,')),
                 lambda: csv_path.write_bytes(csv_orig), csv_want)

        ans = copy / 'examples/mixed-exceptions/answers.yaml'
        ans_orig = ans.read_bytes()
        old = b'current_actor: human, failure_impact: internal_rework, existing_feature: no, human_signoff: no}\n  - name: \xeb\xac\xb8\xec\x9d\x98'
        expect(old in ans_orig, '음성 대조 answers.yaml 답 찾기')
        scenario(copy, report, base, 'answers.yaml 답(case.yaml은 그대로)',
                 lambda: ans.write_bytes(ans_orig.replace(old, old.replace(b'human_signoff: no', b'human_signoff: yes'))),
                 lambda: ans.write_bytes(ans_orig), {'examples/mixed-exceptions/expected/classification.json': 'DIFF'})

        rc3, out3, _ = run_check(copy, report)
        expect(rc3 == 0 and statuses(out3) == base, '음성 대조 뒤 되돌리면 다시 모두 MATCH')


def check_negative_judgment(repo: Path, spec: list) -> None:
    """judgment.json 종류: 사본에 7.2~7.5 블록을 넣으면 대상이 되고, 바꾸면 DIFF·MISSING, 모두 지우면 대상에서 빠진다."""
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        copy = tmp / 'repo'
        shutil.copytree(repo / 'examples', copy / 'examples')
        report = tmp / 'report.json'
        before = expected_files(copy / 'examples')
        paths = {}
        for name in EXAMPLE_NAMES:
            block, _sha = judgment_block(spec, name)
            path = copy / 'examples' / name / 'expected/judgment.json'
            if not path.exists():
                path.write_bytes(block)
            paths[name] = path
        base = check_baseline(copy, report, 'judgment 사본 기준선(블록 4개)')
        expect(len(base) == len([p for p in before if not p.endswith('/judgment.json')]) + len(EXAMPLE_NAMES),
               f'judgment 사본: 종류 하나가 예제마다 더해진다 ({len(base)})')

        target = paths['signoff-final']
        original = target.read_bytes()
        flipped = bytearray(original)
        flipped[len(flipped) // 3] ^= 0x01
        scenario(copy, report, base, 'judgment.json 한 바이트', lambda: target.write_bytes(bytes(flipped)),
                 lambda: target.write_bytes(original), {'examples/signoff-final/expected/judgment.json': 'DIFF'})

        gone = paths['form-fill']
        kept = gone.read_bytes()
        scenario(copy, report, base, 'judgment.json 하나 삭제', gone.unlink, lambda: gone.write_bytes(kept),
                 {'examples/form-fill/expected/judgment.json': 'MISSING'})

        case_path = copy / 'examples/table-compare/case.yaml'
        case_orig = case_path.read_bytes()
        expect(case_orig.count(b'  accuracy_min: 85\n') == 1, 'judgment 음성 대조 문턱 찾기')
        scenario(copy, report, base, 'case.yaml 문턱(85 → 95)',
                 lambda: case_path.write_bytes(case_orig.replace(b'  accuracy_min: 85\n', b'  accuracy_min: 95\n')),
                 lambda: case_path.write_bytes(case_orig), {'examples/table-compare/expected/judgment.json': 'DIFF'})

        rc, out, _ = run_check(copy, report)
        expect(rc == 0 and statuses(out) == base, 'judgment 음성 대조 뒤 되돌리면 다시 모두 MATCH')
        check_determinism(copy, 'judgment 사본')

        for path in paths.values():
            path.unlink()
        after = check_baseline(copy, report, 'judgment.json을 모두 지운 사본')
        expect(not any(p.endswith('/judgment.json') for p in after), 'judgment.json이 하나도 없으면 대상이 아니다')


def check_determinism(repo: Path, label: str = '저장소') -> None:
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
        expect(len(outs) == 1, f'결정론({label}): 환경 4가지에서 출력이 {len(outs)}가지')


def main(argv: list) -> int:
    utf8_stdio()
    ap = argparse.ArgumentParser(description='oracle 자기 점검(스펙 표 대조, 음성 대조, 결정론)')
    ap.add_argument('--repo', default=str(HERE.parents[1]))
    repo = Path(ap.parse_args(argv).repo)
    cls_spec = (repo / 'docs/spec/classification.md').read_text(encoding='utf-8').split('\n')
    met_spec = (repo / 'docs/spec/metrics.md').read_text(encoding='utf-8').split('\n')
    jud_spec = (repo / 'docs/spec/judgment.md').read_text(encoding='utf-8').split('\n')

    check_classification(cls_spec)
    check_signoff_inferred(repo, cls_spec)
    check_metrics_tables(met_spec)
    check_testdata(repo, cls_spec, met_spec)
    check_judgment_tables(jud_spec)
    check_judgment_examples(repo, jud_spec)
    check_judgment_scenes(jud_spec)
    check_variants(repo)
    check_negative(repo)
    check_negative_judgment(repo, jud_spec)
    check_determinism(repo)

    print(f"selftest: 통과 {COUNT['ok']}, 실패 {len(FAILS)}")
    return 1 if FAILS else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
