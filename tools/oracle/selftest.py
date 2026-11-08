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
#    judgment.md(judge.py 하나, T-013에서 T-011 판을 지웠다): 3.2 속성 표, 4.2~4.7·4.9 표(상수 = 표), 6.1~6.6 표(합성 장면·탐침),
#    7.1 예제 case.yaml·예제 표, 7.2~7.5 블록(oracle 출력 = 블록), 4.12 G2 표본, 5절 정의·대조 결과 표(T-013).
#    예제 이름은 고정하지 않는다. examples/의 폴더 이름(서수)이고, 폴더마다 7절 블록이 하나 있어야 한다.
#    metrics.md 12.5 compare-duplicate 블록, 13절 결과 쪽 중복 순서 표(T-013), case-schema.md 10.2 inputs.group 표.
#    변형: 예제 CSV의 CRLF·CR·BOM·따옴표·마지막 줄바꿈 없음·끝 빈 행·칸 앞뒤 공백, answers.yaml의 CRLF·CR·BOM이 같은 바이트.
#    text-check.md(T-014): 4.1~4.5·5.1~5.5 표(상수 = 표, 파서·맞추기·체크리스트 장면·평균·점수 자료 장면, 4.5 체크리스트 코드 표,
#    5.2 문턱 0 문장 표), 3.1·3.2 새 속성 표,
#    6.1 판정 장면(점수표만 + 대조할 열 없음 = 조건부, 점수표만 defaults = 읽는 문턱만), 7.2 계획 표로 CSV를 다시 만들어
#    7.3 블록과 비교, 7.4 블록 = oracle 출력, CSV 변형(점수 칸 따옴표·공백 포함), PR (b) 모의
#    (예제 사본 + reply-drafts 블록 → check.py 모두 MATCH, 바꾸면 그 파일만 DIFF). examples/reply-drafts가 있으면 파일 = 블록.
# 2. 음성 대조: 예제 사본에서 expected 한 바이트, expected 삭제, 결과 CSV 값, answers.yaml 답을 바꾸면
#    check.py가 DIFF·MISSING과 종료 코드 1을 낸다. 사본 그대로면 모두 MATCH, 종료 코드 0.
#    파일 수는 고정하지 않는다. 사본 examples의 expected 파일 목록(check.py 대상 종류)에서 센다.
#    judgment.json: 사본에 7.2~7.5 블록을 넣으면 모두 MATCH. 한 바이트, 한 파일 삭제, group 줄 삭제, case.yaml 문턱,
#    옛 판(schema_version 1) 파일 하나는 그 파일만 DIFF·MISSING. 모두 지우면 대상에서 빠진다.
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
from dataclasses import replace
from decimal import Decimal
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.dont_write_bytecode = True  # 저장소에 __pycache__를 쓰지 않는다.
sys.path.insert(0, str(HERE))

import json  # noqa: E402

import judge as J2  # noqa: E402  판정(judgment.md 4절). T-013에서 T-011 판(judge_v1)을 지웠다.
from check import OPTIONAL_TARGETS, TARGETS  # noqa: E402
from classify import DEFAULT_MARK, INFERRED_MARK, classification_bytes, classify, to_json_value  # noqa: E402
from compare import DEC_MAX, compare_run, is_supported_pattern, judge, matches, metrics_bytes, parse_number, percent  # noqa: E402
from oracle_case import ANSWER_KEYS, CompareField, parse_case  # noqa: E402
from oracle_common import OracleError, ordinal_key, sha256_hex, utf8_stdio  # noqa: E402
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

def example_names(examples: Path) -> list:
    """예제 이름 = examples/ 아래 폴더 이름(서수). 고정 목록을 두지 않는다(T-011a 지적, T-013)."""
    return sorted((p.name for p in examples.iterdir() if p.is_dir()), key=ordinal_key)


J_BASE = {'accuracy_min': '0', 'missing_max': '100', 'time_must_drop': 'false'}  # 정확도·누락률이 늘 met인 문턱


def tick(text: str) -> str:
    return text.strip('`')


class Scenes:
    """합성 케이스 폴더(judgment.md 8절 Build 도우미와 같은 모양: 머리 행 id,<유형 열>,v, compare는 v tolerance: 0).

    값 불일치는 결과 v를 바꾸고, 누락은 결과 행을 뺀다. 저장소 밖 임시 폴더에만 쓴다.
    """

    def __init__(self, root: Path):
        self.root = root
        self.n = 0

    def make(self, n: int, cols=(), missing=(), mismatch=(), extra=(), thresholds=None, *, inputs=True, compare=True,
             ref_files=None, group=None, dup=()) -> Path:
        """cols: [(열 이름, 행마다 값)]. extra: 추가 행의 첫 유형 열 값. thresholds: {키: 글}(None이면 키 없음).

        group: case.yaml inputs.group(T-012, None이면 줄 없음). dup: 결과 쪽에 한 번 더 둘 기준 행 번호(T-012 중복 행).
        """
        self.n += 1
        folder = self.root / f's{self.n:04d}'
        (folder / 'reference').mkdir(parents=True)
        (folder / 'candidate').mkdir()
        out = ['name: selftest', 'task: selftest 합성 판정', 'steps:', '  - name: 단계']
        if inputs:
            out += ['inputs:', '  reference: reference/*.csv', '  candidate: candidate/*.csv', '  key: id']
            if group is not None:
                out.append(f'  group: {group}')
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
        cand += [[f'r{i:04d}'] + [vals[i] for _, vals in cols] + ['1'] for i in dup]
        for side, rows in (('reference', ref), ('candidate', cand)):
            (folder / side / 'a.csv').write_bytes(('\n'.join(','.join(r) for r in rows) + '\n').encode('utf-8'))
        return folder


def alternate(bad: list):
    """불일치 행 번호 → (누락, 값 불일치). 누락부터 번갈아 둔다(6.3 "번갈아")."""
    return set(bad[0::2]), set(bad[1::2])


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


# ---- 판정 점검 (judgment.md 4절 규칙, judge.py) ----

V2_EXAMPLE_CASE_HEAD = '| 예제 | 더하는 줄 | 지금 SHA-256 | 더한 뒤 SHA-256 | 바이트 |'


def v2_block(spec: list, prefix: str):
    """prefix로 시작하는 줄 뒤의 블록 가운데 schema_version 2인 것 하나: (바이트, 그 줄의 SHA-256).

    7.2~7.5 블록과 6.4 compare-duplicate 블록이다. 같은 블록·SHA-256이 두 곳에 있으면 하나로 본다. 다르면 실패한다.
    """
    found = []
    for k, line in enumerate(spec):
        if not line.startswith(prefix):
            continue
        start = next(i for i in range(k + 1, len(spec)) if spec[i].startswith('```'))
        end = next(i for i in range(start + 1, len(spec)) if spec[i] == '```')
        data = ('\n'.join(spec[start + 1:end]) + '\n').encode('utf-8')
        try:
            version = json.loads(data).get('schema_version')
        except ValueError:
            continue
        m = re.search(r'SHA-256 `([0-9a-f]{64})`', line)
        if version == J2.SCHEMA_VERSION and m and (data, m.group(1)) not in found:
            found.append((data, m.group(1)))
    if len(found) != 1:
        raise SystemExit(f'selftest: schema_version {J2.SCHEMA_VERSION} 블록이 정확히 하나여야 한다: {prefix} ({len(found)}개)')
    return found[0]


def v2_examples(repo: Path, spec: list, root: Path) -> Path:
    """examples 사본에 7.1 표의 줄을 key 줄 바로 뒤에 넣는다. 이미 들어 있으면(PR (b) 뒤) 그대로 쓴다."""
    dst = root / 'examples'
    shutil.copytree(repo / 'examples', dst)
    rows = table(spec, V2_EXAMPLE_CASE_HEAD)
    expect([tick(r[0]) for r in rows] == ['table-compare', 'signoff-final'], f'7.1 case.yaml 표 예제 {rows}')
    for name, line, before, after, size in rows:
        path = dst / tick(name) / 'case.yaml'
        data = path.read_bytes()
        if sha256_hex(data) == tick(before):
            lines = data.split(b'\n')
            at = [i for i, x in enumerate(lines) if x.startswith(b'  key: ')]
            expect(len(at) == 1, f'7.1 {name}: key 줄이 하나')
            lines.insert(at[0] + 1, tick(line).encode('utf-8'))
            data = b'\n'.join(lines)
            path.write_bytes(data)
        expect(sha256_hex(data) == tick(after), f'7.1 {name}: case.yaml SHA-256 {sha256_hex(data)}')
        expect(f'{len(data):,}' == size.split(' → ')[1], f'7.1 {name}: case.yaml 바이트 {len(data)}')
    return dst


def judge_folder2(folder: Path, times=None, unexplained=None):
    case = parse_case((folder / 'case.yaml').read_bytes(), 'case.yaml')
    run = compare_run(folder, case)
    return J2.judge(case, run, times, unexplained), case, run


def items_of(run, *statuses) -> list:
    return [it['key'] for it in run.body['items'] if it['status'] in statuses]


def check_v2_tables(spec: list) -> None:
    """상수 = 스펙 표. 4.3~4.9과 T-012에서 그대로인 4.2·4.5·4.6."""
    rows = table(spec, '| 키 | 기본값 | 근거 |')
    expect([(tick(k), tick(v)) for k, v, _ in rows] == list(J2.THRESHOLD_DEFAULTS), 'T-012 4.2 기본값 표 = THRESHOLD_DEFAULTS')
    rows = table(spec, '| 검토 항목 | 항목 상태 | {breakdown} 이름 |')
    got = [(tuple(spans(st)), name) for _item, st, name in rows]
    expect(got == [(tuple(a), b) for a, b in J2.BREAKDOWN_PARTS], f'4.9 검토 항목 표 = BREAKDOWN_PARTS {got}')
    expect(tuple(x for st, _ in got for x in st) == J2.REVIEW_STATUSES, '4.9 검토 항목 상태 = REVIEW_STATUSES')
    rows = table(spec, '| 순서 | 근거 키 | 확인 결과 | 판정 | 판정 문장 틀 |')
    got = [(int(o), tick(k), None if r == '—' else tick(r), tick(v), tick(t)) for o, k, r, v, t in rows]
    expect(got == list(J2.VERDICT_TABLE), '4.3 판정 표 = VERDICT_TABLE')
    rows = table(spec, '| 확인 | 결과 | 걸리는 조건 | 확인 문장 틀 |')
    expect([(tick(c), tick(r), tick(t)) for c, r, _cond, t in rows] == list(J2.CHECK_TABLE), '4.4 확인 표 = CHECK_TABLE')
    rows = table(spec, '| 이름 | 값 | 뜻 |')
    expect([(tick(k), int(tick(v))) for k, v, _ in rows] == [('focus_min', J2.FOCUS_MIN)], 'T-012 4.5 숫자 표 = FOCUS_MIN')
    rows = table(spec, '| 순서 | 근거 키 | 읽은 문턱 |')
    got = {int(o): (() if v == '—' else tuple(spans(v))) for o, _k, v in rows}
    expect(got == J2.READ_THRESHOLDS and list(got) == list(J2.READ_THRESHOLDS), '4.6 읽은 문턱 표 = READ_THRESHOLDS')
    rules = {o: k for o, k, *_ in J2.VERDICT_TABLE}
    expect(all(rules[int(o)] == tick(k) for o, k, _v in rows), '4.6 읽은 문턱 표의 근거 키 = 판정 표의 그 순서')
    line = next(x for x in spec if x.startswith('- 확인 문장: '))
    expect(dict(re.findall(r'`([A-Z]+)` → `([a-z_]+)`', line)) == J2.CHECK_THRESHOLD, 'T-012 4.6 확인 문장 문턱 = CHECK_THRESHOLD')
    line = next(x for x in spec if x.startswith('- 표시 글자는 '))
    expect('공백 하나 + `' + J2.DEFAULT_MARK[1:] + '`' in line and J2.DEFAULT_MARK.startswith(' '), 'T-012 4.6 표시 글자')
    rows = table(spec, '| 다음 할 일 | 나오는 때 | 문구 |')
    expect([(tick(k), tick(t)) for k, _w, t in rows] == list(J2.TODO_TABLE), '4.7 다음 할 일 표 = TODO_TABLE')
    expect(J2.SCHEMA_VERSION == 2, '3.2 schema_version 2')


def check_v2_examples(repo: Path, spec: list, met_spec: list, examples: Path, tc_spec: list) -> None:
    names = example_names(examples)
    expect(len(names) > 0, '예제 폴더가 하나 이상')
    head = '| 예제 | inputs.group | 판정 | 근거 키 | 확인 다섯 | 다음 할 일 | 검토 항목 |'
    listed = [tick(r[0]) for r in table(spec, head)]
    # T-014: text-check.md 7.1 새 예제는 PR (b) 전이면 examples/에 없다. 있으면 확인 여섯이라 check_t014가 본다.
    new = [n for n in tc_new_examples(tc_spec) if n in names]
    expect(sorted(listed + new, key=ordinal_key) == names and len(set(listed + new)) == len(listed + new),
           f'7.1 예제 표의 이름(+ text-check.md 새 예제) = examples/ 폴더 이름 {listed} {new} {names}')
    for name in names:
        block, sha = judgment_block(spec, tc_spec, name)
        got = J2.judgment_bytes(examples / name)
        expect(got == block, f'7.2~7.5 {name}: oracle 출력 = 스펙 블록')
        expect(sha256_hex(block) == sha and sha256_hex(got) == sha, f'7.2~7.5 {name}: SHA-256 줄 {sha}')
        print(f'  judgment(T-012) {name}: oracle {sha256_hex(got)} spec {sha} {"same" if got == block else "DIFF"}')
        file = repo / 'examples' / name / 'expected/judgment.json'
        if file.exists() and b'"schema_version": 2,' in file.read_bytes()[:40]:
            expect(file.read_bytes() == block, f'7.2~7.5 {name}: expected/judgment.json(2) = 스펙 블록')
        # 한 줄을 더해도 metrics.json·classification.json은 그대로다(7.1).
        expect(metrics_bytes(examples / name) == (repo / 'examples' / name / 'expected/metrics.json').read_bytes(),
               f'7.1 {name}: group 줄을 넣은 사본의 metrics.json = expected')
        want_cls = (repo / 'examples' / name / 'expected/classification.json').read_bytes()
        expect(classification_bytes((examples / name / 'case.yaml').read_bytes(), 'case.yaml') == want_cls,
               f'7.1 {name}: group 줄을 넣은 사본의 case.yaml 분류 = expected')

    head = '| 예제 | inputs.group | 판정 | 근거 키 | 확인 다섯 | 다음 할 일 | 검토 항목 |'
    for name, group, verdict, rule, checks, todo, review in table(spec, head):
        res, case, run = judge_folder2(examples / tick(name))
        got_group = case.inputs.group if case.inputs else None
        expect(got_group == (None if group == '없음' else tick(group)), f'7.1 {name} inputs.group {got_group}')
        expect((res['verdict'], res['rule']) == (tick(verdict), tick(rule)), f'7.1 {name} 판정·근거 키')
        expect([c['result'] for c in res['checks']] == ([] if checks == '—' else spans(checks)), f'7.1 {name} 확인 다섯')
        expect([t['key'] for t in res['todo']] == ([] if todo == '—' else spans(todo)), f'7.1 {name} 다음 할 일')
        items = [it for it in run.body['items'] if it['status'] in J2.REVIEW_STATUSES]
        expect(J2.breakdown(items) == ('' if review == '—' else review), f'7.1 {name} 검토 항목 {J2.breakdown(items)}')

    # 3.2 속성 표(T-012 그대로): 경로 순서·형식. 첫 키는 정수 2.
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
                    if isinstance(x, dict):
                        seen.setdefault(path + '[]', []).append(x)
                        walk(x, path + '[].')

    walk(json.loads(J2.judgment_bytes(examples / 'table-compare')), '')
    expect(list(seen) == [tick(r[0]) for r in rows], f'3.2 속성 경로 순서 = 표 ({list(seen)})')
    allowed = [tick(r[0]) for r in table(tc_spec, '| 판정 새 속성 | 형식 | 뜻 |')]
    for name in names:
        before = list(seen)
        walk(json.loads(J2.judgment_bytes(examples / name)), '')
        extra = [p for p in seen if p not in before]
        expect(all(p in allowed for p in extra) and (not extra or name in new), f'3.2 {name}: 표 밖의 속성이 없다 {extra}')
        for p in extra:
            del seen[p]
    kinds = {'정수': lambda v: type(v) is int, '문자열': lambda v: isinstance(v, str), '객체': lambda v: isinstance(v, dict),
             '숫자': lambda v: type(v) in (int, float), '불리언': lambda v: isinstance(v, bool),
             '문자열 배열': lambda v: isinstance(v, list) and all(isinstance(x, str) for x in v),
             '객체 배열': lambda v: isinstance(v, list) and all(isinstance(x, dict) for x in v)}
    for path, form, _ in rows:
        expect(all(kinds[form](v) for v in seen.get(tick(path), [])), f'3.2 {path} 형식 {form}')
    expect(J2.judgment_bytes(examples / 'table-compare').startswith(b'{\n  "schema_version": 2,\n'), '3.2 첫 키 schema_version = 정수 2')

    # 4.12 G2 표본.
    guard = repo / 'tests/Scorecard.Tests/TestData/guard'
    out = J2.judgment_bytes(guard / 'case-valid')
    value = json.loads(out)
    line = next(x for x in spec if x.startswith('- 출력 바이트가 바뀐다(SHA-256 `'))
    expect(sha256_hex(out) in line, f'4.12 G2 표본 SHA-256 {sha256_hex(out)}')
    expect((value['verdict'], value['rule']) == ('hold', 'ACCURACY'), '4.12 G2 표본 보류 ACCURACY')
    expect([c['result'] for c in value['checks']] == ['not_met', 'not_met', 'not_reviewed', 'not_measured', 'off'],
           '4.12 G2 표본 확인 다섯')
    expect([t['key'] for t in value['todo']] == ['measure_time', 'review_items', 'add_group'], '4.12 G2 표본 다음 할 일')
    expect('검토할 항목 5건(불일치 4건, 추가 행 1건)' in value['checks'][2]['reason'], '4.12 G2 표본 검토 항목 5건')
    expect_refused('T-012 대조 오류 표본 guard/compare-invalid 판정', lambda: J2.judgment_bytes(guard / 'compare-invalid'))

    # metrics.md 12.5 compare-duplicate: 블록 → 폴더 → metrics.json = 블록, 판정 = judgment.md 6.4 블록.
    with tempfile.TemporaryDirectory() as tmp:
        folder = Path(tmp) / 'compare-duplicate'
        for rel in ('case.yaml', 'reference/rows.csv', 'candidate/rows.csv'):
            (folder / rel).parent.mkdir(parents=True, exist_ok=True)
            (folder / rel).write_bytes(('\n'.join(block_after(met_spec, f'`compare-duplicate/{rel}`:')) + '\n').encode('utf-8'))
        marker = '`compare-duplicate` metrics.json (SHA-256 `'
        line = next(x for x in met_spec if x.startswith(marker))
        want = ('\n'.join(block_after(met_spec, marker)) + '\n').encode('utf-8')
        got = metrics_bytes(folder)
        expect(got == want and sha256_hex(got) in line, f'metrics 12.5 compare-duplicate metrics.json {sha256_hex(got)}')
        run = compare_run(folder, parse_case((folder / 'case.yaml').read_bytes(), 'case.yaml'))
        c = run.body['counts']
        expect(c['candidate'] == c['match'] + c['mismatch'] + c['extra'] + len(items_of(run, 'duplicate')),
               'metrics 12.2 결과 행 = 일치 + 값 불일치 + 추가 + 중복 행')
        jblock, jsha = v2_block(spec, '`compare-duplicate` judgment.json (')
        got = J2.judgment_bytes(folder)
        expect(got == jblock and sha256_hex(got) == jsha, f'6.4 compare-duplicate judgment.json {sha256_hex(got)}')
        # 기준 쪽 중복은 지금처럼 오류다.
        (folder / 'reference/rows.csv').write_bytes(b'row_id,kind,value\nr1,a,10\nr1,a,10\n')
        expect_refused('metrics 12.2 기준 쪽 중복은 오류', lambda: metrics_bytes(folder))
        # 결과 쪽 빈 키도 오류 그대로다.
        (folder / 'reference/rows.csv').write_bytes(b'row_id,kind,value\nr1,a,10\n')
        (folder / 'candidate/rows.csv').write_bytes(b'row_id,value\nr1,10\n ,10\n')
        expect_refused('metrics 12.2 결과 쪽 빈 키는 오류', lambda: metrics_bytes(folder))
        # 12.3: 기준 파일의 group 열 확인. 결과 파일은 보지 않는다.
        (folder / 'candidate/rows.csv').write_bytes(b'row_id,value\nr1,10\n')
        expect(json.loads(metrics_bytes(folder))['compare']['counts']['match'] == 1, 'metrics 12.3 결과 파일에 group 열이 없어도 된다')
        (folder / 'reference/rows.csv').write_bytes(b'row_id,value\nr1,10\n')
        expect_refused('metrics 12.3 기준 파일에 group 열 없음(column)', lambda: metrics_bytes(folder))
        (folder / 'reference/rows.csv').write_bytes(b'row_id,kind,value,kind\nr1,a,10,a\n')
        expect_refused('metrics 12.3 기준 파일에 group 열 둘(duplicate-column)', lambda: metrics_bytes(folder))
        (folder / 'reference/rows.csv').write_bytes(b'row_id,kind,value\nr1,a,10\n')
        (folder / 'reference/head.csv').write_bytes(b'row_id,value\n')
        expect_refused('metrics 12.3 머리 행만 있는 기준 파일도 본다', lambda: metrics_bytes(folder))


def expand_types(cell: str) -> list:
    out = []
    for s in ([] if cell == '(없음)' else spans(cell)):
        v, _, cnt = s.partition('×')
        out += [cell_value(v)] * (int(cnt) if cnt else 1)
    return out


def human_keys(text: str, run):
    """6.4 "사람 입력" 칸 → 키 목록. 불일치·추가 행·중복 행·일치 N개, '두 번'."""
    if text == '없음':
        return None
    if text == '`[]`':
        return []
    review = set(items_of(run, *J2.REVIEW_STATUSES))
    pools = {'불일치': items_of(run, 'mismatch', 'missing'), '추가 행': items_of(run, 'extra'),
             '중복 행': items_of(run, 'duplicate'), '일치': [r.key for r in run.reference_rows if r.key not in review]}
    keys = []
    for part in text.split(', '):
        twice = part.endswith(' 두 번')
        kind, _, cnt = part.removesuffix(' 두 번').rpartition(' ')
        pick = pools[kind][:int(cnt.rstrip('개'))]
        keys += pick * (2 if twice else 1)
    return keys


def check_v2_scenes(spec: list) -> None:
    with tempfile.TemporaryDirectory() as tmp:
        sc = Scenes(Path(tmp))

        # 6.1 문턱 경계(T-012 그대로).
        head = '| 기준 행 | 일치 | 누락 | 표시 정확도(%) | accuracy_min | ACCURACY | 표시 누락률(%) | missing_max | MISSING |'
        for ref, match, miss, _ap, acc_min, acc_res, _mp, miss_max, miss_res in table(spec, head):
            n, m, ms = int(ref), int(match), int(miss)
            folder = sc.make(n, missing=set(range(ms)), mismatch=set(range(ms, n - m)),
                             thresholds={'accuracy_min': acc_min, 'missing_max': miss_max, 'time_must_drop': 'false'})
            got = results_of(judge_folder2(folder)[0])
            expect((got['ACCURACY'], got['MISSING']) == (tick(acc_res), tick(miss_res)), f'T-012 6.1 {ref}/{match}/{miss}')

        # 6.2 시간(T-012 그대로).
        for tmd, before, proc, rev, want in table(spec, '| time_must_drop | 적용 전 | 처리 | 검토 | TIME |'):
            folder = sc.make(10, thresholds={**J_BASE, 'time_must_drop': tick(tmd)})
            times = None if before == '없음' else J2.Times(Decimal(before), Decimal(proc), Decimal(rev))
            expect(results_of(judge_folder2(folder, times)[0])['TIME'] == tick(want), f'T-012 6.2 {tmd} {before} {proc} {rev}')
        folder = sc.make(10, thresholds={**J_BASE, 'time_must_drop': 'true'})
        res = judge_folder2(folder, J2.Times(Decimal('10'), Decimal('1.25'), Decimal('1.2')))[0]
        expect('처리 1.25분 + 검토 1.2분 = 2.45분이' in res['checks'][3]['reason'], 'T-012 4.5 {review}는 검토 시간, {total} 2.45')
        res = judge_folder2(folder, J2.Times(Decimal('7'), Decimal('7'), Decimal('3')))[0]
        expect(res['rule'] == 'TIME' and '처리·검토 시간 10분이 적용 전 7분보다 짧지 않으므로' in res['reason'], '4.3 TIME not_met 문장')
        expect_refused('5절 시간 음수', lambda: judge_folder2(folder, J2.Times(Decimal('10'), Decimal('-1'), Decimal('0'))))
        expect_refused('4.4 시간 합 넘침({total} 모양 미정)',
                       lambda: judge_folder2(folder, J2.Times(Decimal('1'), DEC_MAX, Decimal('1'))))
        # 3.3 decimal 자릿수: 문턱은 case.yaml 글 그대로.
        folder = sc.make(3, mismatch={0}, thresholds={'accuracy_min': '66.6', 'missing_max': '0.0', 'time_must_drop': 'false'})
        _res, case, run = judge_folder2(folder)
        out = render(J2.judgment_value(case, run))
        expect(b'"accuracy_min": 66.6,' in out and b'"missing_max": 0.0,' in out and '문턱 66.6% 이상'.encode() in out,
               '3.3 문턱 자릿수 66.6·0.0')

        # 6.3 몰림(열 하나). 기준 자료에는 늘 grp 열이 있다. inputs.group이 없음이면 줄을 넣지 않는다.
        head = '| 기준 행 유형 | 불일치 행 유형 | 추가 행 유형 | inputs.group | FOCUS |'
        for ref_t, bad_t, extra_t, group, want in table(spec, head):
            groups, used, bad = expand_types(ref_t), set(), []
            for t in expand_types(bad_t):
                i = next(k for k, g in enumerate(groups) if g == t and k not in used)
                used.add(i)
                bad.append(i)
            missing, mismatch = alternate(bad)
            folder = sc.make(len(groups), [('grp', groups)], missing, mismatch, expand_types(extra_t), J_BASE,
                             group=None if group == '없음' else tick(group))
            res = judge_folder2(folder)[0]
            expect(results_of(res)['FOCUS'] == tick(want), f'6.3 {ref_t} | {bad_t} | {extra_t} | {group}: {results_of(res)["FOCUS"]}')
            todo = [t['key'] for t in res['todo']]
            expect(('add_group' in todo) == (tick(want) == 'off'), f'6.3 {ref_t} | {group}: add_group {todo}')

        # 6.3 몰림(열 여럿).
        head = '| 열 값(행 1~6) | inputs.group | 불일치 행 | FOCUS | 걸린 값 |'
        for col_t, group, bad_t, want, hit in table(spec, head):
            cols = []
            for s in spans(col_t):
                name, vals = s.split('=')
                cols.append((name, ['' if c == '_' else c for c in vals]))
            missing, mismatch = alternate([int(x) - 1 for x in tick(bad_t).split(',')])
            label = f'6.3 {col_t} | {group} | {bad_t}'
            folder = sc.make(6, cols, missing, mismatch, (), J_BASE, group=None if group == '없음' else tick(group))
            if want.startswith(('대조 오류', '파서 오류')):
                expect_refused(f'{label}: {want}', lambda f=folder: judge_folder2(f))
                continue
            focus = judge_folder2(folder)[0]['checks'][4]
            expect(focus['result'] == tick(want), f'{label}: {focus["result"]}')
            if hit != '—':
                expect(f'모두 {tick(hit)}인 행이다(' in focus['reason'], f'{label}: 걸린 값 {focus["reason"]}')
            if focus['result'] in ('spread', 'one_value'):
                expect(focus['reason'].startswith(f'불일치 2건이 {tick(group)} 열의' if focus['result'] == 'spread'
                                                  else f'{tick(group)} 열이 기준 자료에서'), f'{label}: {focus["reason"]}')

        # 6.4 설명 안 되는 검토 항목.
        head = '| 불일치 | 추가 행 | 중복 행 | 사람 입력 | UNEXPLAINED |'
        for nbad, nextra, ndup, given, want in table(spec, head):
            # T-013(metrics.md 4.3): 결과 쪽에 두 번 나온 기준 키는 불일치다. 불일치 칸은 그 키도 센다.
            missing, mismatch = alternate(list(range(int(nbad) - int(ndup))))
            folder = sc.make(10, missing=missing, mismatch=mismatch, extra=('',) * int(nextra),
                             thresholds=J_BASE, dup=list(range(9, 9 - int(ndup), -1)))
            _res, _c, run = judge_folder2(folder)
            human = human_keys(given, run)
            label = f'6.4 {nbad}/{nextra}/{ndup} {given}'
            if want == '거절':
                expect_refused(label, lambda f=folder, h=human: judge_folder2(f, None, h))
                continue
            res = judge_folder2(folder, None, human)[0]
            check = res['checks'][2]
            expect(check['result'] == tick(want), f'{label}: {check["result"]}')
            n = int(nbad) + int(nextra) + int(ndup)
            parts = [f'{k} {v}건' for k, v in (('불일치', nbad), ('추가 행', nextra), ('중복 행', ndup)) if int(v)]
            if n:
                expect(f'검토할 항목 {n}건({", ".join(parts)})' in check['reason'], f'{label}: 문장 {check["reason"]}')
            if tick(want) == 'found':
                expect(res['rule'] == 'UNEXPLAINED' and res['verdict'] == 'hold' and
                       f'키가 {len(human)}개 있으므로 보류.' in res['reason'], f'{label}: 판정 {res["reason"]}')
            if tick(want) == 'not_reviewed':
                expect(res['rule'] == 'UNEXPLAINED' and res['verdict'] == 'conditional', f'{label}: 조건부 {res["rule"]}')
        folder = sc.make(10, missing={0}, mismatch={1}, extra=('',), thresholds=J_BASE)
        expect_refused('6.4 없는 키', lambda: judge_folder2(folder, None, ['nope']))
        res = judge_folder2(folder, None, ['x000'])[0]
        expect(res['rule'] == 'UNEXPLAINED' and res['verdict'] == 'hold', '6.4 추가 행 키를 적을 수 있다(카드 수용 기준 1)')

        # 6.5 우선순위.
        head = '| ACCURACY | MISSING | UNEXPLAINED | TIME | FOCUS | 판정 | 근거 키 | 다음 할 일 |'
        for acc, miss, unexp, tim, focus, verdict, rule, todo in table(spec, head):
            acc, miss, unexp, tim, focus = map(tick, (acc, miss, unexp, tim, focus))
            bad = [] if unexp == 'none' else [0] if focus == 'few' else [0, 5] if focus == 'spread' else [0, 1]
            groups = ['a'] * 10 if focus == 'one_value' else ['a'] * 5 + ['b'] * 5
            missing, mismatch = alternate(bad)
            th = {'accuracy_min': '0' if acc == 'met' else '100', 'missing_max': '100' if miss == 'met' else '0',
                  'time_must_drop': 'false' if tim == 'off' else 'true'}
            folder = sc.make(10, [('grp', groups)], missing, mismatch, (), th, group=None if focus == 'off' else 'grp')
            times = {'met': J2.Times(Decimal(10), Decimal(6), Decimal(3)),
                     'not_met': J2.Times(Decimal(10), Decimal(7), Decimal(3))}.get(tim)
            human = {'explained': [], 'found': [f'r{bad[0]:04d}'] if bad else None}.get(unexp)
            res = judge_folder2(folder, times, human)[0]
            got = results_of(res)
            label = f'6.5 {acc} {miss} {unexp} {tim} {focus}'
            expect([got[k] for k in ('ACCURACY', 'MISSING', 'UNEXPLAINED', 'TIME', 'FOCUS')] == [acc, miss, unexp, tim, focus],
                   f'{label}: 장면 {got}')
            expect((res['verdict'], res['rule']) == (tick(verdict), tick(rule)), f'{label}: {res["verdict"]} {res["rule"]}')
            expect([t['key'] for t in res['todo']] == ([] if todo == '—' else spans(todo)), f'{label}: 다음 할 일 {res["todo"]}')

        # 4.6 읽은 문턱: 기본값과 같은 값을 적은 장면에서 키 하나씩 뺀다(판정은 그대로). 순서마다.
        defaults = dict(J2.THRESHOLD_DEFAULTS)
        g5 = [('grp', ['a'] * 50 + ['b'] * 50)]
        met_time = J2.Times(Decimal(10), Decimal(6), Decimal(3))
        plans = {  # 순서 → (행 수, 유형 열, group, 누락, 값 불일치, 시간, 사람 입력)
            2: (10, (), None, {0}, {1}, None, None),
            3: (100, (), None, {0, 1, 2}, set(), None, None),
            4: (100, (), None, {0}, {1}, None, ['r0000']),
            5: (100, (), None, {0}, {1}, None, None),
            6: (100, g5, 'grp', {0}, {1}, met_time, None),
            7: (100, (), None, {0}, {1}, met_time, None),
            8: (100, (), None, set(), set(), met_time, None),
        }
        rows = {}
        for o, k, r, *_ in J2.VERDICT_TABLE:
            rows.setdefault(o, (k, r))  # 같은 순서의 첫 행(순서 5는 not_measured 장면이다)
        for order, (n, cols, group, missing, mismatch, times, human) in plans.items():
            rule, result = rows[order]
            for drop in (None,) + tuple(defaults):
                th = {k: v for k, v in defaults.items() if k != drop}
                res = judge_folder2(sc.make(n, cols, missing, mismatch, (), th, group=group), times, human)[0]
                label = f'4.6 순서 {order} {rule} 기본값 {drop}'
                expect(res['rule'] == rule and (result is None or results_of(res)[rule] == result), f'{label}: {res["rule"]}')
                expect(res['reason'].endswith(J2.DEFAULT_MARK) == (drop in J2.READ_THRESHOLDS[order]), f'{label}: 판정 문장 표시')
                for c in res['checks']:
                    expect(c['reason'].endswith(J2.DEFAULT_MARK) == (drop is not None and J2.CHECK_THRESHOLD.get(c['rule']) == drop),
                           f'{label}: {c["rule"]} 확인 문장 표시')
                expect(res['defaults'] == ([] if drop is None else [drop]), f'{label}: defaults')

        # 미검증(T-012 그대로).
        for inputs, reason, want_todo in ((False, 'no_inputs', ['add_data', 'add_compare', 'measure_time', 'confirm_thresholds']),
                                          (True, 'no_compare_fields', ['add_compare', 'measure_time', 'confirm_thresholds'])):
            res = judge_folder2(sc.make(1, inputs=inputs, compare=False))[0]
            expect((res['verdict'], res['rule'], res['checks']) == ('not_verified', 'NOT_VERIFIED', []), f'T-012 미검증 {reason}')
            expect([t['key'] for t in res['todo']] == want_todo, f'T-012 미검증 {reason}: 다음 할 일')
        res = judge_folder2(sc.make(1, inputs=True, compare=False, group='grp'))[0]
        expect(res['rule'] == 'NOT_VERIFIED', 'T-012 compare가 비면 group이 있어도 미검증(열을 읽지 않는다)')

        # 이름·값 표시(4.5 그대로): 큰따옴표, 제어 문자·65자는 범위 밖, 64자는 그대로.
        res, case, run = judge_folder2(sc.make(4, [('grp', ['a"b', 'a"b', 'c', 'c'])], {0}, {1}, (), J_BASE, group='grp'))
        expect(b'= a\\"b' in render(J2.judgment_value(case, run)), 'T-012 3.3 유형 값의 큰따옴표 \\"')
        expect_refused('T-012 4.5 제어 문자 값',
                       lambda: judge_folder2(sc.make(4, [('grp', ['a\x01', 'a\x01', 'c', 'c'])], {0}, {1}, (), J_BASE, group='grp')))
        expect_refused('T-012 4.5 65자 값',
                       lambda: judge_folder2(sc.make(4, [('grp', ['x' * 65] * 2 + ['c'] * 2)], {0}, {1}, (), J_BASE, group='grp')))
        res = judge_folder2(sc.make(4, [('grp', ['y' * 64] * 2 + ['c'] * 2)], {0}, {1}, (), J_BASE, group='grp'))[0]
        expect(('= ' + 'y' * 64 + '인') in res['checks'][4]['reason'], 'T-012 4.5 64자 값은 그대로')

        # 결정론·원본 불변.
        folder = sc.make(10, [('grp', ['a'] * 5 + ['b'] * 5)], {0}, {1}, ('a',), J_BASE, group='grp', dup=[9])
        m1 = metrics_bytes(folder)
        expect(J2.judgment_bytes(folder) == J2.judgment_bytes(folder) and metrics_bytes(folder) == m1,
               'T-012 같은 입력 같은 바이트, metrics.json 그대로')


def check_v2_probes(spec: list) -> None:
    """6.6 탐침. 장면은 탐침 이름으로 짓는다. T-012 칸만 judge로 본다. T-011 칸은 기록이다(T-013에서 judge_v1을 지웠다)."""
    th = {'accuracy_min': '95', 'missing_max': '2', 'time_must_drop': 'false'}
    memo = [('memo', [''] * 39 + ['확인'])]
    with tempfile.TemporaryDirectory() as tmp:
        sc = Scenes(Path(tmp))
        scenes = {
            'P1': sc.make(20, extra=('',) * 30, thresholds=th),
            'P2': sc.make(40, memo, set(), {0, 1}, (), th),
            'P2g': sc.make(40, memo, set(), {0, 1}, (), th, group='memo'),
            'P3': sc.make(20, extra=('',), thresholds=th),
            'P4': sc.make(20, thresholds=th, dup=[3]),
        }
        rows = table(spec, '| 탐침 | 장면 | 사람 입력 | T-011 | T-012 |')
        expect({tick(r[0]) for r in rows} == set(scenes), f'6.6 탐침 이름 {[r[0] for r in rows]}')
        for probe, _scene, given, _old, new in rows:
            folder = scenes[tick(probe)]
            case = parse_case((folder / 'case.yaml').read_bytes(), 'case.yaml')
            run = compare_run(folder, case)
            if given == '없음':
                human = None
            elif given == '`[]`':
                human = []
            elif given == '추가 행 키 전부':
                human = items_of(run, 'extra')
            else:
                human = spans(given)
            label = f'6.6 {probe} {given}'
            res = J2.judge(case, run, None, human)
            expect([res['verdict'], res['rule']] == spans(new), f'{label}: T-012 {res["verdict"]} {res["rule"]}')
        # 카드 수용 기준 6: 지어낸 30행은 통과가 아니고, 39행이 빈 메모 열은 몰림이 아니다.
        p1 = judge_folder2(scenes['P1'])[0]
        expect(p1['verdict'] != 'pass' and '추가 행 30건' in p1['reason'], '6.6 P1 지어낸 30행 → 통과 아님')
        p2 = judge_folder2(scenes['P2'])[0]
        expect(results_of(p2)['FOCUS'] == 'off' and p2['rule'] != 'FOCUS', '6.6 P2 메모 열 → 몰림 아님')
        p4 = compare_run(scenes['P4'], parse_case((scenes['P4'] / 'case.yaml').read_bytes(), 'case.yaml'))
        expect(items_of(p4, 'duplicate') == ['r0003'] and p4.body['counts']['candidate'] == 21, '6.6 P4 결과 쪽 중복 = 항목')


def check_v2_parser(repo: Path, cs_spec: list) -> None:
    """case-schema 10.2: table-compare case.yaml의 key 줄 뒤에 줄을 넣고 읽는다. 받음은 모델, 오류는 멈춤."""
    data = (repo / 'examples/table-compare/case.yaml').read_bytes()
    lines = [x for x in data.split(b'\n') if not x.startswith(b'  group: ')]
    at = [i for i, x in enumerate(lines) if x.startswith(b'  key: ')]
    expect(len(at) == 1, 'case-schema 10.2 표본 key 줄')
    for line, result, _code, _msg in table(cs_spec, '| inputs.group 줄 | 결과 | 코드 | 메시지 |'):
        body = list(lines)
        if line != '(줄 없음)':
            body.insert(at[0] + 1, b'  ' + tick(line).encode('utf-8'))
        doc = b'\n'.join(body)
        label = f'case-schema 10.2 {line}'
        if result == '받음':
            case = parse_case(doc, 'case.yaml')
            raw = tick(line).split(': ', 1)[1] if line != '(줄 없음)' else 'null'
            expect(case.inputs.group == (None if raw == 'null' else raw), f'{label}: group {case.inputs.group!r}')
        else:
            expect_refused(label, lambda d=doc: parse_case(d, 'case.yaml'))


def check_negative_v2(repo: Path, spec: list, examples: Path, tc_spec: list) -> None:
    """예제 사본 + 7.2~7.5 블록 → check.py 모두 MATCH. 바꾸면 그 파일만 DIFF·MISSING. 모두 지우면 대상이 아니다."""
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        copy = tmp / 'repo'
        shutil.copytree(examples, copy / 'examples')
        report = tmp / 'report.json'
        names = example_names(copy / 'examples')
        paths = {}
        for name in names:
            block, _sha = judgment_block(spec, tc_spec, name)
            paths[name] = copy / 'examples' / name / 'expected/judgment.json'
            paths[name].write_bytes(block)
        base = check_baseline(copy, report, f'judgment 사본 기준선(group 줄 + 블록 {len(names)}개)')
        others = [p for p in expected_files(copy / 'examples') if not p.endswith('/judgment.json')]
        expect(len(base) == len(others) + len(names), f'judgment 사본: 종류 하나가 예제마다 더해진다 ({len(base)})')

        target = paths['table-compare']
        original = target.read_bytes()
        flipped = bytearray(original)
        flipped[len(flipped) // 2] ^= 0x01
        scenario(copy, report, base, 'T-012 judgment.json 한 바이트', lambda: target.write_bytes(bytes(flipped)),
                 lambda: target.write_bytes(original), {'examples/table-compare/expected/judgment.json': 'DIFF'})

        gone = paths['signoff-final']
        kept = gone.read_bytes()
        scenario(copy, report, base, 'T-012 judgment.json 하나 삭제', gone.unlink, lambda: gone.write_bytes(kept),
                 {'examples/signoff-final/expected/judgment.json': 'MISSING'})

        case_path = copy / 'examples/table-compare/case.yaml'
        case_orig = case_path.read_bytes()
        expect(case_orig.count(b'  group: signal_type\n') == 1, 'T-012 음성 대조 group 줄 찾기')
        scenario(copy, report, base, 'T-012 case.yaml group 줄 삭제',
                 lambda: case_path.write_bytes(case_orig.replace(b'  group: signal_type\n', b'')),
                 lambda: case_path.write_bytes(case_orig), {'examples/table-compare/expected/judgment.json': 'DIFF'})

        th_orig = case_orig
        expect(th_orig.count(b'  accuracy_min: 85\n') == 1, 'judgment 음성 대조 문턱 찾기')
        scenario(copy, report, base, 'case.yaml 문턱(85 → 95)',
                 lambda: case_path.write_bytes(th_orig.replace(b'  accuracy_min: 85\n', b'  accuracy_min: 95\n')),
                 lambda: case_path.write_bytes(th_orig), {'examples/table-compare/expected/judgment.json': 'DIFF'})

        # T-013: 판 고르기가 없다. 옛 판(schema_version 1) 파일은 그 파일만 DIFF다(ADR 0007 결정 4).
        old = paths['form-fill']
        old_orig = old.read_bytes()
        expect(old_orig.count(b'"schema_version": 2,') == 1, 'T-013 음성 대조 schema_version 찾기')
        scenario(copy, report, base, 'T-013 옛 판 expected 하나(form-fill만 schema_version 1)',
                 lambda: old.write_bytes(old_orig.replace(b'"schema_version": 2,', b'"schema_version": 1,')),
                 lambda: old.write_bytes(old_orig), {'examples/form-fill/expected/judgment.json': 'DIFF'})

        rc, out, _ = run_check(copy, report)
        expect(rc == 0 and statuses(out) == base, 'judgment 음성 대조 뒤 되돌리면 다시 모두 MATCH')
        check_determinism(copy, 'judgment 사본')

        for path in paths.values():
            path.unlink()
        after = check_baseline(copy, report, 'judgment.json을 모두 지운 사본')
        expect(not any(p.endswith('/judgment.json') for p in after), 'judgment.json이 하나도 없으면 대상이 아니다')



# ---- T-013 (metrics.md 13절 결과 쪽 중복, judgment.md 5절 정의·대조 결과) ----

DUP_ORDER_HEAD = '| r2 결과 행 | 기준 항목 차이 | 중복 항목 차이 | 일치 | 값 불일치 | compared | outside |'
CONSISTENT_HEAD = '| 정의 | 대조 결과 | Judge |'


def _diff_text(item) -> str:
    return ', '.join(f"{d['reference']} → {d['candidate']}" for d in item['differences']) or '—'


def check_t013(spec: list, met_spec: list) -> None:
    jblock, _jsha = v2_block(spec, '`compare-duplicate` judgment.json (')
    with tempfile.TemporaryDirectory() as tmp:
        folder = Path(tmp) / 'compare-duplicate'
        for rel in ('case.yaml', 'reference/rows.csv', 'candidate/rows.csv'):
            (folder / rel).parent.mkdir(parents=True, exist_ok=True)
            (folder / rel).write_bytes(('\n'.join(block_after(met_spec, f'`compare-duplicate/{rel}`:')) + '\n').encode('utf-8'))
        cand = (folder / 'candidate/rows.csv').read_bytes()
        expect(cand.count(b'\nr2,21\nr2,20\n') == 1, 'metrics 13.3 compare-duplicate r2 두 행 찾기')
        case = parse_case((folder / 'case.yaml').read_bytes(), 'case.yaml')
        rows = table(met_spec, DUP_ORDER_HEAD)
        expect(len(rows) >= 4, f'metrics 13.3 순서 표 {len(rows)}행')
        for vals, ref_diff, dup_diff, match, mismatch, compared, outside in rows:
            a, b = [tick(x) for x in spans(vals)]
            (folder / 'candidate/rows.csv').write_bytes(cand.replace(b'\nr2,21\nr2,20\n', f'\nr2,{a}\nr2,{b}\n'.encode()))
            body = compare_run(folder, case).body
            r2 = [it for it in body['items'] if it['key'] == 'r2']
            label = f'metrics 13.3 r2 {a}, {b}'
            expect([it['status'] for it in r2] == ['mismatch', 'duplicate'], f'{label}: 항목 상태 {[it["status"] for it in r2]}')
            expect([_diff_text(it) for it in r2] == [ref_diff.replace('`', ''), dup_diff.replace('`', '')],
                   f'{label}: 차이 {[_diff_text(it) for it in r2]}')
            c, f = body['counts'], body['fields'][0]
            expect((c['match'], c['mismatch'], f['compared'], f['outside']) == (int(match), int(mismatch), int(compared), int(outside)),
                   f'{label}: 셈 {c["match"]} {c["mismatch"]} {f["compared"]} {f["outside"]}')
            expect(J2.judgment_bytes(folder) == jblock, f'{label}: 판정 = 6.4 compare-duplicate 블록')
        # 키가 기준에 없는 중복(r5)은 줄 순서를 바꿔도 extra 1 + duplicate 1이다.
        (folder / 'candidate/rows.csv').write_bytes(cand.replace(b'r5,50\n', b'r5,X\n').replace(b'r5,55\n', b'r5,50\n')
                                                    .replace(b'r5,X\n', b'r5,55\n'))
        body = compare_run(folder, case).body
        expect([(it['status'], it['differences']) for it in body['items'] if it['key'] == 'r5'] == [('extra', []), ('duplicate', [])],
               'metrics 13.2 기준에 없는 키의 중복은 extra + duplicate(차이 없음)')

    # judgment.md 4.8: 결과 쪽 중복 행은 몰림에 더 들지 않는다. 그 키의 기준 행은 불일치로 한 번 든다(T-012b 리뷰 2).
    with tempfile.TemporaryDirectory() as tmp:
        sc = Scenes(Path(tmp))
        grp = [('grp', ['a'] * 5 + ['b'] * 5)]
        res = judge_folder2(sc.make(10, grp, {0}, {1}, (), J_BASE, group='grp', dup=[1]))[0]
        focus = res['checks'][4]
        expect(focus['result'] == 'found' and focus['reason'].startswith('불일치 2건이 모두 grp = a인 행이다'),
               f'4.8 불일치 키의 중복 행은 몰림에 더 들지 않는다: {focus["reason"]}')
        res = judge_folder2(sc.make(10, grp, {0}, {1}, (), J_BASE, group='grp', dup=[9]))[0]
        expect(res['checks'][4]['result'] == 'spread' and res['checks'][4]['reason'].startswith('불일치 3건이'),
               f'4.8 일치 키를 결과 쪽에 한 번 더 두면 그 키가 불일치로 든다: {res["checks"][4]["reason"]}')

        # judgment.md 5절: 대조 결과가 정의에서 나온 것이 아니면 거절(ArgumentException).
        folders = {
            'none': sc.make(4, inputs=False, compare=False, thresholds=J_BASE),
            'nocmp': sc.make(4, compare=False, thresholds=J_BASE),
            'full': sc.make(4, mismatch={0}, thresholds=J_BASE),
            'group': sc.make(4, [('grp', ['a', 'a', 'b', 'b'])], {0}, {1}, (), J_BASE, group='grp'),
        }
        cases = {k: parse_case((f / 'case.yaml').read_bytes(), 'case.yaml') for k, f in folders.items()}
        runs = {k: compare_run(folders[k], cases[k]) for k in folders}
        full = cases['full']
        defs = {'`inputs` 없음': cases['none'], '`compare` 빔': cases['nocmp'], '`inputs`·`compare` 있음': full,
                '`inputs.group` 있음': cases['group']}
        made = {
            ('`inputs`·`compare` 있음', '대조함, 키 열이 다름'): (replace(full, inputs=replace(full.inputs, key='w')), runs['full']),
            ('`inputs`·`compare` 있음', '대조함, `compare` 필드가 다름'):
                (replace(full, compare=[replace(full.compare[0], field='w')]), runs['full']),
            ('`inputs.group` 있음', '대조함, 기준 행 머리 행에 그 열이 없음'):
                (replace(full, inputs=replace(full.inputs, group='grp')), runs['full']),
            ('`inputs.group` 있음', '대조함(같은 정의)'): (cases['group'], runs['group']),
            ('`inputs`·`compare` 있음', '대조함(같은 정의)'): (full, runs['full']),
        }
        run_of = {'미검증 `no_inputs`': runs['none'], '미검증 `no_compare_fields`': runs['nocmp'], '대조함': runs['full']}
        rows = table(spec, CONSISTENT_HEAD)
        expect(len(rows) >= 10, f'5절 정의·대조 결과 표 {len(rows)}행')
        for d, r, want in rows:
            if d == '(이름)':
                continue  # 케이스 이름은 oracle 대조 결과에 없다. C# 쪽만 본다.
            case, run = made.get((d, r)) or (defs[d], run_of[r])
            label = f'5절 {d} + {r}'
            if want == '거절':
                expect_refused(label, lambda c=case, x=run: J2.judge(c, x))
            else:
                expect(J2.judge(case, run)['verdict'] in ('pass', 'conditional', 'hold', 'not_verified'), f'{label}: 판정')

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


def check_variants(examples: Path) -> None:
    """examples: group 줄을 넣은 예제 사본(7.1). judgment.json은 T-012 판으로 본다."""
    with tempfile.TemporaryDirectory() as tmp:
        for name in ('table-compare', 'signoff-final', 'form-fill'):
            src = examples / name
            want = (src / 'expected/metrics.json').read_bytes()
            want_judgment = J2.judgment_bytes(src)
            csvs = sorted(p.relative_to(src) for p in src.rglob('*.csv'))
            for label in _csv_variants(b'a\n'):
                work = Path(tmp) / f'{name}-{label}'
                shutil.copytree(src, work)
                for rel in csvs:
                    (work / rel).write_bytes(_csv_variants((src / rel).read_bytes())[label])
                expect(metrics_bytes(work) == want, f'CSV 변형 {name} {label}')
                expect(J2.judgment_bytes(work) == want_judgment, f'CSV 변형 {name} {label} judgment.json')
            ans = (src / 'answers.yaml').read_bytes()
            want_cls = (src / 'expected/classification.json').read_bytes()
            for label, data in (('crlf', ans.replace(b'\n', b'\r\n')), ('cr', ans.replace(b'\n', b'\r')),
                                ('bom', b'\xef\xbb\xbf' + ans), ('bom-crlf', b'\xef\xbb\xbf' + ans.replace(b'\n', b'\r\n'))):
                expect(classification_bytes(data, 'answers.yaml') == want_cls, f'answers.yaml 변형 {name} {label}')


# ---- T-014 (text-check.md: 체크리스트·가림 점수표) ----

TC_NEW_EXAMPLE_HEAD = '| 새 예제 | inputs.group | 판정 | 근거 키 | 확인 여섯 | 다음 할 일 | 검토 항목 |'
TC_FILES_HEAD = '| 예제 파일 | 만드는 곳 |'
TC_EXAMPLE = 'reply-drafts'


def tc_unescape(text: str) -> str:
    """text-check.md 표 글의 \\n·\\t·\\uXXXX."""
    return re.sub(r'\\(n|t|u[0-9A-Fa-f]{4})',
                  lambda m: {'n': '\n', 't': '\t'}.get(m.group(1)) or chr(int(m.group(1)[1:], 16)), text)


def tc_new_examples(tc_spec: list) -> list:
    return [tick(r[0]) for r in table(tc_spec, TC_NEW_EXAMPLE_HEAD)]


def judgment_block(spec: list, tc_spec: list, name: str):
    """예제의 judgment.json 블록: text-check.md 새 예제면 그 문서, 아니면 judgment.md."""
    source = tc_spec if name in tc_new_examples(tc_spec) else spec
    return v2_block(source, f'`examples/{name}/expected/judgment.json` (')


def walk_paths(value, prefix: str = '', seen=None) -> dict:
    """JSON 값의 속성 경로(처음 나온 순서) → 값 목록. 배열 원소 객체는 경로[]."""
    seen = {} if seen is None else seen
    for k, v in value.items():
        path = prefix + k
        seen.setdefault(path, []).append(v)
        if isinstance(v, dict):
            walk_paths(v, path + '.', seen)
        elif isinstance(v, list):
            for x in v:
                if isinstance(x, dict):
                    seen.setdefault(path + '[]', []).append(x)
                    walk_paths(x, path + '[].', seen)
    return seen


def tc_files(tc_spec: list) -> dict:
    """7.3·7.4 블록: {예제 안 상대 경로: 바이트}. SHA-256·바이트 수 줄도 본다."""
    out = {}
    for rel, _where in table(tc_spec, TC_FILES_HEAD):
        rel = tick(rel)
        marker = f'`examples/{TC_EXAMPLE}/{rel}` ('
        lines = [x for x in tc_spec if x.startswith(marker)]
        expect(len(lines) == 1, f'T-014 7.3 {rel}: 줄이 한 번')
        data = ('\n'.join(block_after(tc_spec, marker)) + '\n').encode('utf-8')
        m = re.search(r'SHA-256 `([0-9a-f]{64})`, ([0-9,]+)바이트', lines[0])
        expect(bool(m) and m.group(1) == sha256_hex(data) and m.group(2) == f'{len(data):,}', f'T-014 7.3 {rel}: SHA-256·바이트 줄')
        out[rel] = data
    return out


def tc_write(folder: Path, files: dict, skip_expected: bool = True) -> Path:
    for rel, data in files.items():
        if skip_expected and rel.startswith('expected/'):
            continue
        (folder / rel).parent.mkdir(parents=True, exist_ok=True)
        (folder / rel).write_bytes(data)
    return folder


def tc_change(folder: Path, cell: str) -> None:
    """바꾼 곳: `파일`, `파일`: `옛 글` → `새 글`[, …][; `파일`: …]. 파일마다 모든 곳을 바꾼다. 옛 글이 없으면 실패.

    파일이 다른 묶음은 `; `로 잇는다(리뷰 2차 B1 장면, text-check.md 4.2·6.1).
    """
    if cell == '(없음)':
        return
    for group in re.split(r'(?<=`); (?=`)', cell):
        at = group.index('`: `')
        names = spans(group[:at + 1])
        pairs_ = [(tc_unescape(a), tc_unescape(b)) for a, b in re.findall(r'`([^`]*)` → `([^`]*)`', group[at + 3:])]
        expect(len(names) > 0 and len(pairs_) > 0, f'T-014 바꾼 곳 읽기: {group}')
        for name in names:
            path = folder / name
            text = path.read_bytes().decode('utf-8')
            for old, new in pairs_:
                expect(old in text, f'T-014 바꾼 곳 {name}: {old!r} 없음')
                text = text.replace(old, new)
            path.write_bytes(text.encode('utf-8'))


def tc_generate(tc_spec: list) -> dict:
    """7.2 계획 표와 글만으로 CSV 넷을 다시 만든다(synthetic-examples.md 4.2 SplitMix64·NextBelow)."""
    plan = {k: v for k, v in table(tc_spec, '| 계획 | 값 |')}
    mask = (1 << 64) - 1
    state = [int(tick(plan['시드']))]

    def below(n: int) -> int:
        state[0] = (state[0] + 0x9E3779B97F4A7C15) & mask
        z = state[0]
        z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & mask
        z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & mask
        return ((z ^ (z >> 31)) * n) >> 64

    def keyed(cell: str) -> tuple:
        s = spans(cell)
        digits = int(re.search(r'(\d+)자리', cell).group(1))
        return s[-3] if len(s) == 4 else s[0], digits

    def shuffle(xs: list) -> None:
        for k in range(len(xs) - 1, 0, -1):
            j = below(k + 1)
            xs[k], xs[j] = xs[j], xs[k]

    rows = int(tick(plan['행 수']))
    key_col = spans(plan['키'])[0]
    kp, kd = keyed(plan['키'])
    topics = spans(plan['유형'])
    actions = dict(re.findall(r'`([^`]+)` → `([^`]+)`', plan['처리 안내']))
    focus = tick(plan['빠뜨릴 유형'])
    tp, tmin, tspan = spans(plan['문의 번호'])
    tmin, tspan = int(tmin), int(re.fullmatch(r'NextBelow\((\d+)\)', tspan).group(1))
    greets, fillers, closings = spans(plan['인사']), spans(plan['덧붙임']), spans(plan['끝인사'])
    raters = spans(plan['채점자'])
    bp, bd = keyed(plan['가림 키'])
    ai_scores = [int(x) for x in spans(plan['AI 점수'])]
    human_scores = [int(x) for x in spans(plan['사람 점수'])]
    penalty = int(tick(plan['빠진 항목 감점']))

    keys = [f'{kp}{i + 1:0{kd}d}' for i in range(rows)]
    topic = [topics[below(len(topics))] for _ in range(rows)]
    ticket = [f'{tp}{tmin + below(tspan)}' for _ in range(rows)]
    pick = [(below(len(greets)), below(len(fillers)), below(len(closings))) for _ in range(rows)]
    pool = [i for i in range(rows) if topic[i] == focus]
    expect(len(pool) >= 2, 'T-014 7.2 빠뜨릴 유형 행이 2개 이상')
    omit_ticket = pool[below(len(pool))]
    pool2 = [i for i in pool if i != omit_ticket]
    omit_action = pool2[below(len(pool2))]
    order = list(range(rows))
    shuffle(order)
    pairs_ = [(i, s) for i in range(rows) for s in ('ai', 'human')]
    shuffle(pairs_)
    blinds = [f'{bp}{k + 1:0{bd}d}' for k in range(len(pairs_))]
    sheet = []
    for rater in raters:
        for k, (i, src) in enumerate(pairs_):
            if src == 'ai':
                v = ai_scores[below(len(ai_scores))] - (penalty if i in (omit_ticket, omit_action) else 0)
            else:
                v = human_scores[below(len(human_scores))]
            sheet.append([rater, keys[i], blinds[k], str(v)])

    def reply(i: int) -> str:
        g, f, c = pick[i]
        return ' '.join([greets[g],
                         tick(plan['번호 빠진 문장']) if i == omit_ticket else tick(plan['번호 문장']).replace('{ticket}', ticket[i]),
                         tick(plan['안내 빠진 문장']) if i == omit_action else tick(plan['안내 문장']).replace('{action}', actions[topic[i]]),
                         fillers[f], closings[c]])

    line = next(x for x in tc_spec if x.startswith('- `reference/replies.csv`: '))
    heads = dict(zip(spans(line)[0::2], [h.split(',') for h in spans(line)[1::2]]))
    expect(heads['reference/replies.csv'][0] == key_col, 'T-014 7.2 키 열 = 기준 머리 행 첫 칸')
    body = {
        'reference/replies.csv': [[keys[i], topic[i], ticket[i], actions[topic[i]]] for i in range(rows)],
        'candidate/replies.csv': [[keys[i], reply(i)] for i in order],
        'scores/sources.csv': [[blinds[k], keys[i], s] for k, (i, s) in enumerate(pairs_)],
        'scores/sheet.csv': sheet,
    }
    out = {}
    for rel, data_rows in body.items():
        cells = heads[rel] + [c for r in data_rows for c in r]
        expect(all(c and c == c.strip() and not any(x in c for x in ',"\n\r\t') for c in cells), f'T-014 7.2 {rel} 칸 규칙')
        out[rel] = ''.join(','.join(r) + '\n' for r in [heads[rel]] + data_rows).encode('utf-8')
    return out


def tc_judge(folder: Path, times=None, unexplained=None):
    case = parse_case((folder / 'case.yaml').read_bytes(), 'case.yaml')
    run = compare_run(folder, case)
    return J2.judge(case, run, times, unexplained), case, run


def check_t014(repo: Path, tc_spec: list, jud_spec: list, met_spec: list) -> None:
    from checklist import ABSENT, FOUND, SKIPPED, item_result
    from oracle_case import ChecklistItem
    import compare as C2
    import scores as SC

    start = COUNT['ok']
    # 상수 = 표(5.1~5.5, 4.3·4.4).
    rows = table(tc_spec, '| 점수 문턱 | 기본값 | 근거 |')
    expect([(tick(k), tick(v)) for k, v, _ in rows] == list(J2.SCORE_THRESHOLD_DEFAULTS), 'T-014 5.1 = SCORE_THRESHOLD_DEFAULTS')
    rows = table(tc_spec, '| 점수 확인 | 결과 | 걸리는 조건 | 읽는 문턱 | 확인 문장 틀 |')
    expect([(tick(c), tick(r), tick(t)) for c, r, _w, _th, t in rows] == list(J2.SCORE_CHECK_TABLE), 'T-014 5.2 = SCORE_CHECK_TABLE')
    expect({tick(r): (() if th == '—' else tuple(spans(th))) for _c, r, _w, th, _t in rows} == J2.SCORE_CHECK_READS,
           'T-014 5.2 읽는 문턱 = SCORE_CHECK_READS')
    rows = table(tc_spec, '| 문턱 0 문장 | 근거 키 | 결과 | 문장 틀 |')
    expect([(w, tick(k), tick(r), tick(t)) for w, k, r, t in rows] == list(J2.SCORE_ZERO_TABLE), 'T-014 5.2 문턱 0 문장 = SCORE_ZERO_TABLE')
    rows = table(tc_spec, '| 자리 | 근거 키 | 확인 결과 | 길 | 판정 | 판정 문장 틀 |')
    got = [(tick(a), tick(k), None if r == '—' else tick(r), tick(p), tick(v), tick(t)) for a, k, r, p, v, t in rows]
    expect(got == list(J2.SCORE_VERDICT_ROWS), 'T-014 5.3 = SCORE_VERDICT_ROWS')
    rows = table(tc_spec, '| 자리 | 근거 키 | 확인 결과 | 길 | 읽은 문턱 |')
    got = [(tick(a), None if k == '—' else tick(k), None if r == '—' else tick(r), tick(p), () if th == '—' else tuple(spans(th)))
           for a, k, r, p, th in rows]
    expect(got == list(J2.SCORE_READ_THRESHOLDS), 'T-014 5.4 = SCORE_READ_THRESHOLDS')
    rows = table(tc_spec, '| 점수 할 일 | 나오는 때 | 문구 |')
    expect([(tick(k), tick(t)) for k, _w, t in rows] == list(J2.SCORE_TODO_TABLE), 'T-014 5.5 = SCORE_TODO_TABLE')
    rows = table(tc_spec, '| 가림 규칙 | 보는 곳 | 걸리는 조건 | 발견 위치·값 |')
    expect(tuple(tick(r[0]) for r in rows) == SC.BLIND_RULES, 'T-014 4.4 가림 규칙 = BLIND_RULES')
    rows = {tick(f): tuple(spans(c)) for f, c, _ in table(tc_spec, '| 파일 | 열 | 뜻 |')}
    expect(list(rows.values()) == [SC.SHEET_COLUMNS, SC.SOURCE_COLUMNS], 'T-014 4.3 파일 열 = SHEET_COLUMNS·SOURCE_COLUMNS')

    # 평균(4.3).
    for total, n, mean in table(tc_spec, '| 점수 합 | 점수 수 | 평균 |'):
        expect(SC.mean_text(int(total), int(n)).text == mean, f'T-014 4.3 평균 {total}/{n} = {mean}')

    # 맞추기(4.2).
    want_of = {'`found`': FOUND, '`absent`': ABSENT, '`skipped`': SKIPPED}
    for item, ref, text, result in table(tc_spec, '| 항목 | 기준 칸 | 결과 문장 | 결과 |'):
        if item == '`column`':
            it, ref_cell = ChecklistItem('x', 'c', ()), tc_unescape(cell_value(ref))
        else:
            it, ref_cell = ChecklistItem('x', None, tuple(tick(item)[len('any: ['):-1].split(', '))), None
        got = item_result(it, normalize(tc_unescape(cell_value(text))), None if ref_cell is None else normalize(ref_cell))
        expect(got == want_of[result], f'T-014 4.2 맞추기 {item} {ref} {text}: {got}')

    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        files = tc_files(tc_spec)
        base = tc_write(tmp / 'base' / TC_EXAMPLE, files)

        # 생성기(7.2) = CSV 블록.
        for rel, data in tc_generate(tc_spec).items():
            expect(files.get(rel) == data, f'T-014 7.2 생성기 = 7.3 {rel} 블록')

        # expected = oracle 출력(7.4). answers.yaml·case.yaml 분류가 같다.
        cls = classification_bytes(files['answers.yaml'], 'answers.yaml')
        expect(classification_bytes(files['case.yaml'], 'case.yaml') == cls, 'T-014 7.4 answers.yaml·case.yaml 분류 같음')
        outputs = {'expected/classification.json': cls, 'expected/metrics.json': metrics_bytes(base),
                   'expected/judgment.json': J2.judgment_bytes(base)}
        for rel, data in outputs.items():
            expect(files.get(rel) == data, f'T-014 7.4 oracle 출력 = {rel} 블록 {sha256_hex(data)}')
            print(f'  T-014 {rel}: oracle {sha256_hex(data)} {"same" if files.get(rel) == data else "DIFF"}')
        real = repo / 'examples' / TC_EXAMPLE
        if real.is_dir():  # PR (b) 뒤: 파일 = 블록, 표 밖 파일 없음.
            got = sorted(str(p.relative_to(real)).replace(os.sep, '/') for p in real.rglob('*') if p.is_file())
            expect(got == sorted(files), f'T-014 examples/{TC_EXAMPLE} 파일 목록 = 7.1 표 {got}')
            for rel, data in files.items():
                expect((real / rel).is_file() and (real / rel).read_bytes() == data, f'T-014 examples/{TC_EXAMPLE}/{rel} = 블록')

        # 새 예제 표(7.1).
        for name, group, verdict, rule, checks, todo, review in table(tc_spec, TC_NEW_EXAMPLE_HEAD):
            expect(tick(name) == TC_EXAMPLE, f'T-014 7.1 새 예제 이름 {name}')
            res, case, run = tc_judge(base)
            expect(case.inputs.group == tick(group), 'T-014 7.1 inputs.group')
            expect((res['verdict'], res['rule']) == (tick(verdict), tick(rule)), 'T-014 7.1 판정·근거 키')
            expect([c['result'] for c in res['checks']] == spans(checks), 'T-014 7.1 확인 여섯')
            expect([t['key'] for t in res['todo']] == ([] if todo == '—' else spans(todo)), 'T-014 7.1 다음 할 일')
            items = [it for it in run.body['items'] if it['status'] in J2.REVIEW_STATUSES]
            expect(J2.breakdown(items) == review, f'T-014 7.1 검토 항목 {J2.breakdown(items)}')

        # 새 속성 표(3.1·3.2): 기존 표에 없는 경로 = 새 표(순서), 형식.
        kinds = {'정수': lambda v: type(v) is int, '문자열': lambda v: isinstance(v, str), '객체': lambda v: isinstance(v, dict),
                 '숫자': lambda v: type(v) in (int, float), '불리언': lambda v: isinstance(v, bool),
                 '문자열 배열': lambda v: isinstance(v, list) and all(isinstance(x, str) for x in v),
                 '객체 배열': lambda v: isinstance(v, list) and all(isinstance(x, dict) for x in v),
                 '문자열 또는 null': lambda v: v is None or isinstance(v, str),
                 '숫자 또는 null': lambda v: v is None or type(v) in (int, float)}
        for doc, head, data in ((met_spec, '| 새 속성 | 형식 | 뜻 |', outputs['expected/metrics.json']),
                                (jud_spec, '| 판정 새 속성 | 형식 | 뜻 |', outputs['expected/judgment.json'])):
            old = {tick(r[0]) for r in table(doc, '| 속성 | 형식 | 뜻 |')}
            seen = walk_paths(json.loads(data))
            rows = table(tc_spec, head)
            new = [p for p in seen if p not in old]
            expect(new == [tick(r[0]) for r in rows], f'T-014 {head} 경로 = 출력의 새 경로 {new}')
            for path, form, _ in rows:
                expect(form in kinds and all(kinds[form](v) for v in seen.get(tick(path), [])), f'T-014 {path} 형식 {form}')

        # 파서 장면(4.1).
        case_text = files['case.yaml'].decode('utf-8')
        for cell, result, code, message in table(tc_spec, '| case.yaml 바꾼 곳 | 결과 | 코드 | 메시지 |'):
            text = case_text
            if cell != '(없음)':
                old, new = (tc_unescape(x) for x in spans(cell))
                expect(text.count(old) == 1, f'T-014 4.1 바탕에 한 번: {old!r}')
                text = text.replace(old, new)
            try:
                parse_case(text.encode('utf-8'), 'case.yaml')
                expect(result == '받음', f'T-014 4.1 {cell}: 받았다')
            except OracleError as e:
                expect(result == '오류' and tick(message) in str(e), f'T-014 4.1 {cell}: {e}')

        # 점수 자료 장면(4.4).
        for k, (cell, result, found, code) in enumerate(table(tc_spec, '| 점수 자료 바꾼 곳 | 결과 | 가림 발견 | 코드 |')):
            folder = tmp / f'score{k:02d}' / TC_EXAMPLE
            shutil.copytree(base, folder)
            tc_change(folder, cell)
            try:
                blind = json.loads(metrics_bytes(folder))['scores']['blind']
                rules = {}
                for f in blind['findings']:
                    expect(list(f) == ['rule', 'file', 'line', 'value'], f'T-014 4.4 발견 속성 {list(f)}')
                    rules[f['rule']] = rules.get(f['rule'], 0) + 1
                want = {} if found == '—' else {tick(m[0]): int(m[1]) for m in re.findall(r'(`[a-z_]+`) (\d+)건', found)}
                expect(blind['status'] == tick(result) and rules == want, f'T-014 4.4 {cell}: {blind["status"]} {rules}')
            except OracleError as e:
                expect(result == '오류' and f'({tick(code)} 오류 입력' in str(e), f'T-014 4.4 {cell}: {e}')

        # 체크리스트 장면(4.2 규칙 10, 리뷰 2차 B1): 확인할 것이 없는 기준 행은 no-check 오류. 정확도는 받았을 때만.
        rows = table(tc_spec, '| 체크리스트 코드 | 언제 | 위치 | 메시지 |')
        expect([(tick(c), tick(m).replace('<키>', '{key}')) for c, _w, _p, m in rows] == [C2.NO_CHECK],
               'T-014 4.5 체크리스트 코드 = NO_CHECK')
        for k, (cell, result, code, accuracy) in enumerate(table(tc_spec, '| 체크리스트 바꾼 곳 | 결과 | 코드 | 정확도(%) |')):
            folder = tmp / f'check{k:02d}' / TC_EXAMPLE
            shutil.copytree(base, folder)
            tc_change(folder, cell)
            try:
                value = json.loads(metrics_bytes(folder))['compare']
                expect(result == '받음' and str(value['accuracy_pct']) == accuracy, f'T-014 4.2 장면 {cell}: {value["accuracy_pct"]}')
            except OracleError as e:
                expect(result == '오류' and f'({tick(code)} 오류 입력' in str(e), f'T-014 4.2 장면 {cell}: {e}')

        # 판정 장면(6.1). 바탕 score-only = 6.2 case.yaml + reply-drafts scores/.
        only = tmp / 'only' / 'score-only'
        (only / 'scores').mkdir(parents=True)
        (only / 'case.yaml').write_bytes(('\n'.join(block_after(tc_spec, '`score-only/case.yaml`:')) + '\n').encode('utf-8'))
        for rel in ('scores/sources.csv', 'scores/sheet.csv'):
            (only / rel).write_bytes(files[rel])
        bases = {'reply-drafts': base, 'score-only': only}
        scene_head = '| 장면 | 바탕 | 바꾼 곳 | 판정 | 근거 키 | 확인 결과 | 다음 할 일 |'
        reasons, judged = {}, {}
        for k, (scene, src, cell, verdict, rule, checks, todo) in enumerate(table(tc_spec, scene_head)):
            folder = tmp / f'scene{k:02d}' / tick(src)
            shutil.copytree(bases[tick(src)], folder)
            tc_change(folder, cell)
            res, _case, run = tc_judge(folder)
            got = (res['verdict'], res['rule'], [c['result'] for c in res['checks']], [t['key'] for t in res['todo']])
            want = (tick(verdict), tick(rule), spans(checks), [] if todo == '—' else spans(todo))
            expect(got == want, f'T-014 6.1 {scene}: {got}')
            reasons[scene], judged[scene] = res['reason'], res
            if run.body['status'] == 'not_verified':  # 5.1: 점수표만은 읽는 문턱만 defaults에 적는다.
                expect(all(d in J2.SCORES_ONLY_READ for d in res['defaults']), f'T-014 5.1 {scene}: defaults {res["defaults"]}')
            gap = res['thresholds'].get('score_gap_max')
            if gap is not None and Decimal(gap.text) == 0:  # 5.2 문턱 0 문장: "점 넘게"가 없다.
                expect(all('점 넘게' not in x for x in [res['reason']] + [c['reason'] for c in res['checks']]),
                       f'T-014 5.2 {scene}: 문턱 0 문장')
        for scene in ('점수 문턱 기본값', '점수표만, 대조 열 없음'):
            line = next(x for x in tc_spec if x.startswith(f'- "{scene}": '))
            expect(spans(line)[-1] == reasons.get(scene), f'T-014 6.1 {scene} 판정 문장 {reasons.get(scene)}')
        unused = judged.get('점수표만, 쓰지 않는 문턱 기본값', {})
        expect(unused.get('defaults') == [] and unused.get('thresholds', {}).get('accuracy_min') is not None,
               'T-014 5.1 점수표만: 쓰지 않는 문턱은 defaults에 없고 thresholds 모양은 그대로')
        # 리뷰 지적 2: inputs가 있는데 대조할 열이 없으면 점수표만이어도 통과가 아니다.
        for scene, res in judged.items():
            if res['rule'] == 'NOT_VERIFIED':
                expect(res['verdict'] == 'conditional' and 'add_compare' in [t['key'] for t in res['todo']],
                       f'T-014 5.3 {scene}: 대조할 열 없음은 조건부 + add_compare')
        expect_refused('T-014 5.3 점수표만 + 검토 항목 키', lambda: tc_judge(only, None, []))
        value = json.loads(metrics_bytes(only))
        expect(list(value) == ['schema_version', 'case', 'compare', 'scores'] and value['compare']['reason'] == 'no_inputs',
               'T-014 6.2 score-only metrics.json 모양')

        # CSV 변형(4.6): 같은 바이트.
        for label in _csv_variants(b'a\n'):
            work = tmp / f'var-{label}' / TC_EXAMPLE
            shutil.copytree(base, work)
            for rel in [r for r in files if r.endswith('.csv')]:
                (work / rel).write_bytes(_csv_variants(files[rel])[label])
            expect(metrics_bytes(work) == outputs['expected/metrics.json'], f'T-014 CSV 변형 {label} metrics.json')
            expect(J2.judgment_bytes(work) == outputs['expected/judgment.json'], f'T-014 CSV 변형 {label} judgment.json')

        # 점수표가 없으면 바이트가 지금과 같다: scores·checklist를 지운 사본은 새 속성이 없다.
        plain = tmp / 'plain' / TC_EXAMPLE
        shutil.copytree(base, plain)
        tc_change(plain, '`case.yaml`: `  score_gap_max: 0.5\\n` → ``')
        text = (plain / 'case.yaml').read_text(encoding='utf-8')
        cut = text.index('checklist:\n')
        text = text[:cut] + text[text.index('# 시간 측정'):]
        (plain / 'case.yaml').write_text(text + 'compare:\n  - {field: ticket}\n', encoding='utf-8')
        (plain / 'candidate/replies.csv').write_bytes(files['reference/replies.csv'])
        mvalue, jvalue = json.loads(metrics_bytes(plain)), json.loads(J2.judgment_bytes(plain))
        expect('scores' not in mvalue and 'checklist' not in mvalue['compare'] and
               all('missing_items' not in it for it in mvalue['compare']['items']), 'T-014 3.1 설정 없으면 새 속성 없음')
        expect('score_gap_max' not in jvalue['thresholds'] and len(jvalue['checks']) == 5, 'T-014 3.2 설정 없으면 SCORE 없음')

        # PR (b) 모의: 예제 사본에 reply-drafts(블록) → check.py 모두 MATCH, 바꾸면 그 파일만 DIFF.
        copy = tmp / 'repo'
        shutil.copytree(repo / 'examples', copy / 'examples')
        tc_write(copy / 'examples' / TC_EXAMPLE, files, skip_expected=False)
        report = tmp / 'report.json'
        before = expected_files(repo / 'examples')
        allx = check_baseline(copy, report, f'T-014 PR (b) 모의(examples + {TC_EXAMPLE})')
        expect(len(allx) == len([p for p in before if f'/{TC_EXAMPLE}/' not in p]) + 3, f'T-014 PR (b) 모의: 대상 {len(allx)}개')
        target = copy / 'examples' / TC_EXAMPLE / 'expected/metrics.json'
        original = target.read_bytes()
        flipped = bytearray(original)
        flipped[len(flipped) // 2] ^= 0x01
        scenario(copy, report, allx, 'T-014 metrics.json 한 바이트', lambda: target.write_bytes(bytes(flipped)),
                 lambda: target.write_bytes(original), {f'examples/{TC_EXAMPLE}/expected/metrics.json': 'DIFF'})
        sheet = copy / 'examples' / TC_EXAMPLE / 'scores/sheet.csv'
        sheet_orig = sheet.read_bytes()
        first = sheet_orig.split(b'\n')[1]
        bumped = first[:-1] + bytes([first[-1] + (1 if first[-1:] < b'5' else -1)])
        scenario(copy, report, allx, 'T-014 점수 하나', lambda: sheet.write_bytes(sheet_orig.replace(first, bumped, 1)),
                 lambda: sheet.write_bytes(sheet_orig),
                 {f'examples/{TC_EXAMPLE}/expected/metrics.json': 'DIFF', f'examples/{TC_EXAMPLE}/expected/judgment.json': 'DIFF'})
        cand = copy / 'examples' / TC_EXAMPLE / 'candidate/replies.csv'
        cand_orig = cand.read_bytes()
        scenario(copy, report, allx, 'T-014 결과 문장(빠진 번호 채움)',
                 lambda: cand.write_bytes(cand_orig.replace('문의하신 건을'.encode(), '문의하신 q397 건을'.encode())),
                 lambda: cand.write_bytes(cand_orig),
                 {f'examples/{TC_EXAMPLE}/expected/metrics.json': 'DIFF', f'examples/{TC_EXAMPLE}/expected/judgment.json': 'DIFF'})
        check_determinism(copy, 'T-014 PR (b) 모의')
    print(f"  T-014 점검 통과 {COUNT['ok'] - start}건")


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

    cs_spec = (repo / 'docs/spec/case-schema.md').read_text(encoding='utf-8').split('\n')
    tc_spec = (repo / 'docs/spec/text-check.md').read_text(encoding='utf-8').split('\n')

    check_classification(cls_spec)
    check_signoff_inferred(repo, cls_spec)
    check_metrics_tables(met_spec)
    check_testdata(repo, cls_spec, met_spec)

    # 판정(judge.py 하나). 예제 사본은 7.1 표의 group 줄이 들어간 것이다(저장소 예제에 이미 있으면 그대로).
    with tempfile.TemporaryDirectory() as tmp:
        examples_v2 = v2_examples(repo, jud_spec, Path(tmp))
        check_v2_tables(jud_spec)
        check_v2_examples(repo, jud_spec, met_spec, examples_v2, tc_spec)
        check_v2_scenes(jud_spec)
        check_v2_probes(jud_spec)
        check_v2_parser(repo, cs_spec)
        check_variants(examples_v2)
        check_t013(jud_spec, met_spec)
        check_negative_v2(repo, jud_spec, examples_v2, tc_spec)
    check_t014(repo, tc_spec, jud_spec, met_spec)
    check_negative(repo)
    check_determinism(repo)

    print(f"selftest: 통과 {COUNT['ok']}, 실패 {len(FAILS)}")
    return 1 if FAILS else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
