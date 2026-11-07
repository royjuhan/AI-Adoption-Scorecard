#!/usr/bin/env python3
# oracle 판정, T-011 규칙(judgment.json schema_version 1). 동결본이다. 고치지 않는다.
# T-012(judgment.md 11절)에서 judge.py가 schema_version 2 규칙으로 바뀌었다. 이 파일은 T-012 PR (a)의 judge.py 직전 판(main 0f9e810)을
# 그대로 옮긴 것이다(이 머리 주석, main()의 사용 줄, judge() 첫머리의 T-012 가드만 다르다). check.py가 expected/judgment.json의 schema_version이 1일 때만 쓴다
# (judgment.md 11.15). T-012 PR (b)가 머지되면 expected가 모두 2라 쓰이지 않는다. 그 뒤 첫 oracle 변경에서 지운다.
#
# 아래는 원래 머리 주석이다.
# oracle 판정. docs/spec/judgment.md 2절, 3.1~3.3, 4.1~4.10만 보고 짰다(src/는 읽지 않았다, ADR 0005 결정 9).
# 대조는 oracle compare.py의 결과(metrics.md 3.1 메모리 결과: 항목과 기준 행 전부)를 쓴다(judgment.md 4.11).
# 규칙 값(4.2 기본값, 4.3 판정 표, 4.4 확인 표, 4.5 focus_min, 4.6 읽은 문턱, 4.7 다음 할 일)은
# 스펙 표를 손으로 옮겨 이 파일에 적었다. 실행 중에 스펙 문서를 읽지 않는다. selftest.py가 상수와 표를 대조한다.
#
# 사용: python3 tools/oracle/judge_v1.py <케이스 폴더>
#   표준 출력에 judgment.json 바이트(시간·사람 입력 없음. judgment.md 8절 골든과 같다).
#   범위 밖 입력이면 표준 오류에 이유, 종료 코드 2.
# 표준 라이브러리만 쓴다. 네트워크·외부 프로세스 없음.
from __future__ import annotations

import re
import sys
from dataclasses import dataclass
from decimal import Decimal, DecimalException, localcontext
from pathlib import Path

sys.dont_write_bytecode = True  # 저장소에 __pycache__를 쓰지 않는다.
sys.path.insert(0, str(Path(__file__).resolve().parent))

from compare import (DEC_MAX, ITEM_MISMATCH, ITEM_MISSING, STATUS_NOT_VERIFIED, CompareRun, _case_yaml,  # noqa: E402
                     _fits, compare_run)
from oracle_case import Case, parse_case  # noqa: E402
from oracle_common import EXACT, OracleError, ordinal_key, utf16_units, utf8_stdio  # noqa: E402
from oracle_json import JsonNumber, render  # noqa: E402

SCHEMA_VERSION = 1  # judgment.md 3.2 첫 키(ADR 0004 결정 2)
CASE_FILE = 'case.yaml'

# 0절 판정 값.
VERDICT_PASS, VERDICT_CONDITIONAL, VERDICT_HOLD, VERDICT_NOT_VERIFIED = 'pass', 'conditional', 'hold', 'not_verified'

# 4.2 문턱 기본값 표. (키, 기본값 글). 표 순서가 defaults 순서다.
THRESHOLD_DEFAULTS = (
    ('accuracy_min', '95'),
    ('missing_max', '2'),
    ('time_must_drop', 'true'),
)

# 4.3 판정 표. (순서, 근거 키, 걸리는 결과, 판정, 판정 문장 틀). 위에서부터 처음 걸리는 행. PASS는 결과 없음(None).
VERDICT_TABLE = (
    (1, 'NOT_VERIFIED', 'no_inputs', 'not_verified', '검증 자료(case.yaml inputs)가 없어 미검증.'),
    (1, 'NOT_VERIFIED', 'no_compare_fields', 'not_verified', '대조할 열(case.yaml compare)이 없어 미검증.'),
    (2, 'ACCURACY', 'not_met', 'hold', '정확도 {accuracy_pct}%가 문턱 {accuracy_min}%에 못 미치므로 보류.'),
    (3, 'MISSING', 'not_met', 'hold', '누락률 {missing_rate_pct}%가 문턱 {missing_max}%를 넘으므로 보류.'),
    (4, 'UNEXPLAINED', 'found', 'hold', '사람이 설명할 수 없다고 적은 불일치가 {unexplained}건 있으므로 보류.'),
    (5, 'TIME', 'not_measured', 'conditional',
     '정확도·누락률은 문턱을 충족하나 시간을 재지 않아 줄었는지 알 수 없으므로 조건부.'),
    (5, 'TIME', 'not_met', 'conditional',
     '정확도·누락률은 문턱을 충족하나 처리·검토 시간 {total}분이 적용 전 {before}분보다 짧지 않으므로 조건부.'),
    (6, 'FOCUS', 'found', 'conditional',
     '정확도·누락률은 문턱을 충족하나 불일치 {bad}건이 모두 {group} = {value}인 행이므로 조건부.'),
    (7, 'PASS', None, 'pass', '정확도·누락률이 문턱을 충족하고 조건부·보류 조건에 걸리지 않으므로 통과.'),
)

# 4.4 확인 표. (확인, 결과, 확인 문장 틀). 확인 순서는 표에서 처음 나오는 순서다.
CHECK_TABLE = (
    ('ACCURACY', 'met', '정확도 {accuracy_pct}%({match}/{reference})가 문턱 {accuracy_min}% 이상이다.'),
    ('ACCURACY', 'not_met', '정확도 {accuracy_pct}%({match}/{reference})가 문턱 {accuracy_min}%에 못 미친다.'),
    ('MISSING', 'met', '누락률 {missing_rate_pct}%({missing}/{reference})가 문턱 {missing_max}% 이하다.'),
    ('MISSING', 'not_met', '누락률 {missing_rate_pct}%({missing}/{reference})가 문턱 {missing_max}%를 넘는다.'),
    ('UNEXPLAINED', 'none', '불일치가 없다.'),
    ('UNEXPLAINED', 'not_reviewed', '불일치 {bad}건이 설명되는지 사람이 아직 보지 않았다.'),
    ('UNEXPLAINED', 'explained', '불일치 {bad}건을 사람이 보고 모두 설명했다.'),
    ('UNEXPLAINED', 'found', '불일치 {bad}건 중 {unexplained}건을 사람이 설명할 수 없다고 적었다.'),
    ('TIME', 'off', '시간 조건을 쓰지 않는다(time_must_drop: false).'),
    ('TIME', 'not_measured', '시간을 재지 않았다.'),
    ('TIME', 'met', '처리 {process}분 + 검토 {review}분 = {total}분이 적용 전 {before}분보다 짧다.'),
    ('TIME', 'not_met', '처리 {process}분 + 검토 {review}분 = {total}분이 적용 전 {before}분보다 짧지 않다.'),
    ('FOCUS', 'few', '불일치가 {bad}건이라 몰림을 보지 않는다.'),
    ('FOCUS', 'off', '기준 자료에 키·대조 열 밖의 열이 없어 불일치 {bad}건이 한 유형에 몰렸는지 볼 수 없다.'),
    ('FOCUS', 'one_value', '키·대조 열 밖의 열({columns})이 기준 자료에서 모두 값이 하나뿐이라 몰림을 볼 수 없다.'),
    ('FOCUS', 'found',
     '불일치 {bad}건이 모두 {group} = {value}인 행이다({value} {rows_in}행 중 {bad}행, 다른 값 {rows_out}행 중 0행).'),
    ('FOCUS', 'spread', '불일치 {bad}건이 한 값에 몰린 열이 없다(본 열: {columns}).'),
)
CHECK_ORDER = tuple(dict.fromkeys(c for c, _r, _t in CHECK_TABLE))

# 4.5 숫자 표.
FOCUS_MIN = 2

# 4.6: 확인 문장이 쓰는 문턱. UNEXPLAINED·FOCUS는 문턱을 쓰지 않는다.
CHECK_THRESHOLD = {'ACCURACY': 'accuracy_min', 'MISSING': 'missing_max', 'TIME': 'time_must_drop'}

# 4.6 읽은 문턱 표. 판정 문장은 이 문턱 가운데 하나라도 기본값이면 표시를 붙인다.
READ_THRESHOLDS = {
    'NOT_VERIFIED': (),
    'ACCURACY': ('accuracy_min',),
    'MISSING': ('accuracy_min', 'missing_max'),
    'UNEXPLAINED': ('accuracy_min', 'missing_max'),
    'TIME': ('accuracy_min', 'missing_max', 'time_must_drop'),
    'FOCUS': ('accuracy_min', 'missing_max', 'time_must_drop'),
    'PASS': ('accuracy_min', 'missing_max', 'time_must_drop'),
}

# 4.6 표시 글자(classification.md 4.6과 같다): 공백 하나 + 괄호 글.
DEFAULT_MARK = ' (기본값 사용, 확인 필요)'

# 4.7 다음 할 일 표. (키, 문구). 표 순서가 todo 순서다. 언제 나오는지는 _todo()가 표의 "언제" 칸대로 정한다.
TODO_TABLE = (
    ('add_data', '기준 자료(사람이 만든 결과)를 reference/에, 결과 자료(AI 적용 후 결과)를 candidate/에 넣고 '
                 'case.yaml inputs에 파일 패턴과 키 열을 적는다.'),
    ('add_compare', 'case.yaml compare에 대조할 열과 허용 오차를 적는다.'),
    ('measure_time', '건당 처리 시간·검토 시간과 적용 전 시간(분)을 잰다.'),
    ('review_mismatches', '불일치 {bad}건을 보고 설명이 되는지 적는다.'),
    ('add_type_column', '불일치가 한 유형에 몰렸는지 보려면 기준 자료에 유형 열(키·대조 열 밖의 열)을 더한다.'),
    ('confirm_thresholds', '문턱에 기본값을 썼다({defaults}). 맞는지 확인하고 case.yaml thresholds에 적는다.'),
)

EMPTY_VALUE = '(빈 칸)'  # 4.5: 빈 값은 (빈 칸)
SHOW_MAX = 64  # 4.5·metrics 4.7: 64자를 넘으면 자른다(자르는 모양은 범위 밖, show() 참고)


@dataclass(frozen=True)
class Times:
    """4.10 JudgmentTimes. 건당 분, decimal, 0 이상."""
    before: Decimal
    process: Decimal
    review: Decimal


# ---- 글 만들기 ----

_SLOT = re.compile(r'\{([a-z_]+)\}')


def fill(template: str, values: dict) -> str:
    """문장 틀의 {이름}을 값으로 바꾼다. 틀에 있는데 값이 없으면 oracle 내부 오류."""
    def one(m):
        name = m.group(1)
        if name not in values:
            raise OracleError(f'문장 틀의 {{{name}}} 값이 없다(oracle 내부 오류)')
        return values[name]
    return _SLOT.sub(one, template)


def show(text: str) -> str:
    """4.5: 이름·값 표시. 빈 값은 (빈 칸).

    metrics 4.7의 "제어 문자는 \\uXXXX, 64자를 넘으면 자르고 …"는 16진 대소문자, 어떤 글자가 제어 문자인지,
    "자"가 UTF-16 단위인지, 자른 뒤 길이가 64인지 65인지를 정하지 않았다. 그래서 그런 값은 범위 밖으로 멈춘다.
    """
    if text == '':
        return EMPTY_VALUE
    for ch in text:
        o = ord(ch)
        if o < 0x20 or 0x7F <= o <= 0x9F:
            raise OracleError(f'제어 문자 U+{o:04X}가 있는 이름·값의 표시 모양은 oracle 범위 밖')
    if len(utf16_units(text)) > SHOW_MAX:
        raise OracleError(f'{SHOW_MAX}자를 넘는 이름·값을 자르는 모양은 oracle 범위 밖')
    return text


def net_decimal_text(x: Decimal, what: str) -> str:
    """decimal을 InvariantCulture 글로(3.3·4.5). 지수 꼴 없이 자릿수를 그대로 쓴다."""
    if not _fits(x):
        raise OracleError(f'{what}를 decimal로 그대로 담을 수 없다 (oracle 범위 밖)')
    if x.is_signed() and x == 0:
        raise OracleError(f'{what}가 음수 0이다. 글 모양을 스펙이 정하지 않았다 (oracle 범위 밖)')
    return format(x, 'f')


# ---- 문턱 (4.2) ----

def effective_thresholds(case: Case):
    """(값 글 dict, 기본값을 쓴 키 목록). 값 글: accuracy_min·missing_max는 case.yaml 글 그대로, time_must_drop은 'true'·'false'."""
    t = case.thresholds
    given = {
        'accuracy_min': t.accuracy_min_text if t else None,
        'missing_max': t.missing_max_text if t else None,
        'time_must_drop': (None if t is None or t.time_must_drop is None else ('true' if t.time_must_drop else 'false')),
    }
    values, defaults = {}, []
    for key, default in THRESHOLD_DEFAULTS:
        if given[key] is None:
            values[key] = default
            defaults.append(key)
        else:
            values[key] = given[key]
    return values, defaults


# ---- 확인 다섯 (4.4) ----

def _type_columns(case: Case, run: CompareRun) -> list:
    """4.8 유형 열 규칙 1·2: 모든 기준 파일의 머리 행에 꼭 한 번 있는 이름. 키·compare·빈 이름은 뺀다. 이름 서수 순."""
    if not run.reference_headers:
        return []
    exclude = {case.inputs.key} | {f.field for f in case.compare} | {''}
    common = None
    for _file, header in run.reference_headers:
        once = {h for h in header if header.count(h) == 1}
        common = once if common is None else common & once
    return sorted(common - exclude, key=ordinal_key)


def _cell(row, column: str) -> str:
    return row.values[row.header.index(column)]


def _focus(case: Case, run: CompareRun, bad_rows: list):
    """4.8 규칙 3~6. (결과, 문장 값 dict)."""
    bad = len(bad_rows)
    if bad < FOCUS_MIN:
        return 'few', {}
    columns = _type_columns(case, run)
    if not columns:
        return 'off', {}
    seen = []
    for col in columns:
        ref_values = [_cell(r, col) for r in run.reference_rows]  # 기준 행만. 결과 자료·추가 행은 보지 않는다.
        if len(set(ref_values)) == 1:
            continue  # 값이 하나뿐인 열은 건너뛴다.
        seen.append(col)
        bad_values = {_cell(r, col) for r in bad_rows}
        if len(bad_values) == 1:
            value = next(iter(bad_values))
            rows_in = sum(1 for v in ref_values if v == value)
            return 'found', {'group': show(col), 'value': show(value), 'rows_in': str(rows_in),
                             'rows_out': str(len(ref_values) - rows_in)}
    if seen:
        return 'spread', {'columns': ', '.join(show(c) for c in seen)}
    return 'one_value', {'columns': ', '.join(show(c) for c in columns)}


def _time(values: dict, times: Times | None):
    if values['time_must_drop'] == 'false':
        return 'off', {}  # 받아도 보지 않는다.
    if times is None:
        return 'not_measured', {}
    texts = {}
    for name in ('before', 'process', 'review'):
        x = getattr(times, name)
        if not isinstance(x, Decimal) or not x.is_finite():
            raise OracleError(f'시간 {name}이 decimal이 아니다 (oracle 범위 밖)')
        if x < 0:
            raise OracleError(f'시간 {name}이 음수다 (ArgumentOutOfRangeException 입력, 범위 밖)')
        texts[name] = net_decimal_text(x, f'시간 {name}')
    try:
        with localcontext(EXACT):
            total = times.process + times.review
    except DecimalException:
        raise OracleError('시간 합 셈에서 반올림이 생겼다 (oracle 범위 밖)') from None
    if total > DEC_MAX:
        # 4.4: 결과는 not_met이지만 문장의 {total}을 스펙이 정하지 않았다.
        raise OracleError('시간 합이 decimal 범위를 넘는다. 문장의 {total} 모양을 스펙이 정하지 않았다 (oracle 범위 밖)')
    texts['total'] = net_decimal_text(total, '시간 합')
    return ('met' if total < times.before else 'not_met'), texts  # 같으면 줄지 않았다.


def judge(case: Case, run: CompareRun, times: Times | None = None, unexplained_keys=None) -> dict:
    """판정(4.1~4.10). 순수 함수. 반환: judgment.json의 schema_version·case 뒤 속성들(dict, 출력 순서)."""
    # T-012 가드(이 동결본에 더한 유일한 규칙 줄): T-011 스키마에는 inputs.group이 없고, 결과 쪽 키 중복은 오류였다.
    # 그런 입력의 T-011 결과는 정해지지 않았으므로 멈춘다.
    if case.inputs is not None and case.inputs.group is not None:
        raise OracleError('inputs.group이 있는 케이스는 T-011 규칙(schema_version 1) 범위 밖')
    if any(it['status'] == 'duplicate' for it in run.body.get('items', [])):
        raise OracleError('결과 쪽 키 중복이 있는 케이스는 T-011 규칙(schema_version 1) 범위 밖(그때는 입력 오류)')
    values, defaults = effective_thresholds(case)
    body = run.body
    thresholds_json = {
        'accuracy_min': JsonNumber(values['accuracy_min']),
        'missing_max': JsonNumber(values['missing_max']),
        'time_must_drop': values['time_must_drop'] == 'true',
    }

    def mark(text: str, keys) -> str:
        return text + DEFAULT_MARK if any(k in defaults for k in keys) else text

    todo_when = {k: False for k, _ in TODO_TABLE}
    slot = {'defaults': ', '.join(f'{k} {values[k]}' for k in defaults)}

    if body['status'] == STATUS_NOT_VERIFIED:
        if times is not None or unexplained_keys is not None:
            # 4.7 "미검증이고 time_must_drop이 true"가 시간을 받았을 때도 measure_time인지 스펙이 정하지 않았다.
            raise OracleError('미검증인데 시간·사람 입력을 받았다. 결과를 스펙이 정하지 않았다 (oracle 범위 밖)')
        row = next((r for r in VERDICT_TABLE if r[1] == 'NOT_VERIFIED' and r[2] == body['reason']), None)
        if row is None:
            raise OracleError(f"미검증 이유 {body['reason']!r}가 판정 표에 없다")
        _order, rule, _result, verdict, template = row
        reason = mark(template, READ_THRESHOLDS[rule])
        checks = []
        todo_when['add_data'] = body['reason'] == 'no_inputs'
        todo_when['add_compare'] = True
        todo_when['measure_time'] = values['time_must_drop'] == 'true'
    else:
        counts = body['counts']
        bad_items = [it for it in body['items'] if it['status'] in (ITEM_MISMATCH, ITEM_MISSING)]
        bad = len(bad_items)
        if bad != counts['mismatch'] + counts['missing']:
            raise OracleError('불일치 수가 셈과 다르다(oracle 내부 오류)')
        by_key = {r.key: r for r in run.reference_rows}
        bad_rows = [by_key[it['key']] for it in bad_items]
        slot.update({
            'accuracy_pct': body['accuracy_pct'].text, 'missing_rate_pct': body['missing_rate_pct'].text,
            'match': str(counts['match']), 'missing': str(counts['missing']), 'reference': str(counts['reference']),
            'accuracy_min': values['accuracy_min'], 'missing_max': values['missing_max'], 'bad': str(bad),
        })
        results = {}
        try:
            with localcontext(EXACT):
                # 셈으로 비교한다(4.1 4, 4.4). 나눗셈을 쓰지 않는다.
                results['ACCURACY'] = 'met' if Decimal(counts['match'] * 100) >= \
                    Decimal(values['accuracy_min']) * counts['reference'] else 'not_met'
                results['MISSING'] = 'met' if Decimal(counts['missing'] * 100) <= \
                    Decimal(values['missing_max']) * counts['reference'] else 'not_met'
        except DecimalException:
            raise OracleError('문턱 셈에서 반올림이 생겼다 (oracle 범위 밖)') from None

        # 4.9 설명 안 되는 불일치. 사람 입력: None(아직 안 봄) 또는 불일치 키 목록.
        if unexplained_keys is not None:
            keys = list(unexplained_keys)
            bad_keys = {it['key'] for it in bad_items}
            if len(set(keys)) != len(keys) or not set(keys) <= bad_keys:
                raise OracleError('사람 입력 키가 불일치 항목이 아니거나 중복이다 (ArgumentException 입력, 범위 밖)')
            slot['unexplained'] = str(len(keys))
        if bad == 0:
            results['UNEXPLAINED'] = 'none'
        elif unexplained_keys is None:
            results['UNEXPLAINED'] = 'not_reviewed'
        elif not unexplained_keys:
            results['UNEXPLAINED'] = 'explained'
        else:
            results['UNEXPLAINED'] = 'found'

        results['TIME'], time_slot = _time(values, times)
        slot.update(time_slot)
        results['FOCUS'], focus_slot = _focus(case, run, bad_rows)
        slot.update(focus_slot)

        sentences = {(c, r): t for c, r, t in CHECK_TABLE}
        checks = []
        for name in CHECK_ORDER:
            text = fill(sentences[(name, results[name])], slot)
            keys = (CHECK_THRESHOLD[name],) if name in CHECK_THRESHOLD else ()
            checks.append({'rule': name, 'result': results[name], 'reason': mark(text, keys)})

        # 4.3: 순서대로 보고 처음 걸리는 행. PASS는 어느 행에도 걸리지 않을 때다.
        row = next(r for r in VERDICT_TABLE if r[1] != 'NOT_VERIFIED' and (r[1] == 'PASS' or results[r[1]] == r[2]))
        _order, rule, _result, verdict, template = row
        reason = mark(fill(template, slot), READ_THRESHOLDS[rule])
        todo_when['measure_time'] = results['TIME'] == 'not_measured'
        todo_when['review_mismatches'] = results['UNEXPLAINED'] == 'not_reviewed'
        todo_when['add_type_column'] = results['FOCUS'] == 'off'

    todo_when['confirm_thresholds'] = bool(defaults)
    todo = [{'key': k, 'text': fill(t, slot)} for k, t in TODO_TABLE if todo_when[k]]
    return {
        'verdict': verdict, 'rule': rule, 'reason': reason, 'thresholds': thresholds_json,
        'defaults': defaults, 'checks': checks, 'todo': todo,
    }


def judgment_value(case: Case, run: CompareRun, times: Times | None = None, unexplained_keys=None) -> dict:
    return {'schema_version': SCHEMA_VERSION, 'case': case.name, **judge(case, run, times, unexplained_keys)}


def judgment_bytes(case_dir: Path, times: Times | None = None, unexplained_keys=None) -> bytes:
    """케이스 폴더 → judgment.json 바이트. 대조 오류 입력은 OracleError(판정하지 않는다, 5절)."""
    case = parse_case(_case_yaml(case_dir), CASE_FILE)
    run = compare_run(case_dir, case)
    return render(judgment_value(case, run, times, unexplained_keys))


def main(argv: list) -> int:
    utf8_stdio()
    if len(argv) != 1:
        print('사용: judge_v1.py <케이스 폴더>', file=sys.stderr)
        return 2
    try:
        out = judgment_bytes(Path(argv[0]))
    except OracleError as e:
        print(f'oracle: {e}', file=sys.stderr)
        return 2
    sys.stdout.buffer.write(out)
    sys.stdout.buffer.flush()
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
