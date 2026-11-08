#!/usr/bin/env python3
# oracle 대조·지표. docs/spec/metrics.md 2절, 3.1~3.3, 4.1~4.8, 12절(T-012)·13절(T-013)만 보고 짰다(src/는 읽지 않았다, ADR 0005 결정 9).
# 출발점은 T-006 스파이크 xcheck/compare.py(befaa0a, metrics.md 글만 보고 짠 것)다. 바꾼 곳은 README.
# 오류 입력(4.7 오류 코드 13개)은 다루지 않는다. 만나면 OracleError로 멈춘다.
# T-012(metrics.md 12절): 결과 쪽 키 중복은 오류가 아니라 항목 duplicate(둘째 이후 행, 4.3). 기준 쪽 중복은 오류 그대로.
# T-013(metrics.md 13절): 결과 쪽에 두 번 이상 나온 기준 키는 값과 상관없이 값 불일치다. 첫 행은 기준 항목에,
#   둘째 이후 행은 duplicate 항목에 기준과의 차이를 적는다. 필드 셈은 기준에 키가 있는 결과 행마다 센다(줄 순서와 상관없다).
# inputs.group 열은 기준 파일에서만 확인한다(12.3).
#   inputs.group을 적었으면 기준 파일마다 그 열이 머리 행에 꼭 한 번 있어야 한다(column·duplicate-column, 기준 쪽만).
# T-014(text-check.md 3.2·4.2): checklist가 있으면 결과 문장 열에 항목 문구가 있는지 본다(문자열 규칙만, checklist.py).
#   빠진 항목이 있으면 값 불일치다. checklist 객체와 items[].missing_items는 checklist가 있을 때만 쓴다.
#   scores가 있으면 metrics.json 최상위에 scores(scores.py)를 더한다. 둘 다 없으면 바이트가 T-013과 같다.
#   리뷰 2차 B1(text-check.md 4.2 규칙 10): compare 필드가 없고 체크리스트가 그 기준 행에 요구하는 항목이 하나도 없으면
#   (any 항목이 없고 column 항목 칸이 모두 빔) no-check 오류다. 그 행은 어떤 결과 문장이든 일치가 되기 때문이다.
# T-015(confusion.md 3.1·4.2): confusion이 있으면 그 범주 필드로 혼동행렬과 관심 범주별 재현율·정밀도를 센다(confusion.py).
#   compare.confusion은 confusion이 있을 때만 쓴다(fields·checklist 뒤, items 앞). 없으면 바이트가 T-014와 같다.
#
# 사용: python3 tools/oracle/compare.py <케이스 폴더>
#   표준 출력에 metrics.json 바이트를 쓴다. 범위 밖 입력이면 표준 오류에 이유, 종료 코드 2.
# 표준 라이브러리만 쓴다. 네트워크·외부 프로세스 없음.
from __future__ import annotations

import os
import re
import sys
from dataclasses import dataclass
from decimal import Decimal, DecimalException, localcontext
from pathlib import Path

sys.dont_write_bytecode = True  # 저장소에 __pycache__를 쓰지 않는다.
sys.path.insert(0, str(Path(__file__).resolve().parent))

from checklist import ABSENT, FOUND, SKIPPED, item_result  # noqa: E402
from confusion import confusion_body  # noqa: E402
from oracle_case import Case, CompareField, parse_case  # noqa: E402
from oracle_common import EXACT, OracleError, ordinal_key, utf16_units, utf8_stdio  # noqa: E402
from oracle_csv import read_table  # noqa: E402
from oracle_json import JsonNumber, render  # noqa: E402

SCHEMA_VERSION = 1  # metrics.md 3.2 첫 키(ADR 0004 결정 2)
CASE_FILE = 'case.yaml'

# text-check.md 4.5 체크리스트 코드 표(리뷰 2차 B1). (코드, 메시지 틀)
NO_CHECK = ('no-check', '확인할 항목이 없다: {key} (column 항목 칸이 모두 비었다)')

# 4.4 미검증 표, 3.2 속성 표의 값.
STATUS_MEASURED = 'measured'
STATUS_NOT_VERIFIED = 'not_verified'
REASON_NO_INPUTS = 'no_inputs'
REASON_NO_COMPARE_FIELDS = 'no_compare_fields'
ITEM_MATCH, ITEM_MISMATCH, ITEM_MISSING, ITEM_EXTRA = 'match', 'mismatch', 'missing', 'extra'
ITEM_DUPLICATE = 'duplicate'  # T-012 (metrics.md 12.2)
KIND_NUMBER, KIND_TEXT = 'number', 'text'
RESULT_WITHIN, RESULT_OUTSIDE, RESULT_NOT_NUMBER = 'within', 'outside', 'not_number'

# .NET decimal의 범위(metrics 4.5 숫자 모양 표: 79228162514264337593543950335는 숫자, 하나 더 크면 아니다).
DEC_MAX = Decimal('79228162514264337593543950335')
DEC_MANTISSA_LIMIT = 1 << 96
DEC_MAX_SCALE = 28

# 4.5 숫자 읽기: 부호·소수점·지수, ASCII 숫자만. 천 단위·통화·%·NaN·무한대·16진·전각·아랍 숫자는 아니다.
NUMBER_RE = re.compile(r'[+-]?(?:[0-9]+\.?[0-9]*|\.[0-9]+)(?:[eE][+-]?[0-9]+)?', re.ASCII)


# ---- 숫자 (4.5) ----
# 셈은 모두 EXACT 문맥에서 한다. 파이썬 기본 문맥(28자리)은 abs()·−·×에서 조용히 반올림하기 때문이다.
# EXACT는 반올림이 생기면 예외를 낸다. 절댓값은 문맥을 타지 않는 copy_abs()로 구한다.

def _fits(x: Decimal) -> bool:
    """x를 .NET decimal로 반올림 없이 담을 수 있나: 가수 < 2^96, 소수 자릿수 ≤ 28."""
    if x.copy_abs() > DEC_MAX:
        return False
    _sign, digits, exp = x.as_tuple()
    mant = int(''.join(map(str, digits))) if digits else 0
    if exp >= 0:
        return mant * 10 ** exp < DEC_MANTISSA_LIMIT
    scale = -exp
    while scale > 0 and mant % 10 == 0:
        mant //= 10
        scale -= 1
    return scale <= DEC_MAX_SCALE and mant < DEC_MANTISSA_LIMIT


def _near_max(x: Decimal) -> bool:
    """DEC_MAX보다 크고 DEC_MAX + 1보다 작다. .NET이 반올림해 담을지 넘침으로 볼지 스펙이 정하지 않았다."""
    with localcontext(EXACT):
        return DEC_MAX < x.copy_abs() < DEC_MAX + 1


def parse_number(text: str):
    """정규화 뒤 칸 → Decimal 또는 None(숫자 아님). .NET이 반올림할 칸은 범위 밖으로 멈춘다."""
    if not NUMBER_RE.fullmatch(text):
        return None
    value = Decimal(text)  # 생성은 정확하다(문맥 정밀도와 상관없다).
    if _near_max(value):
        raise OracleError(f'decimal 최댓값 바로 위의 칸 {text!r}는 반올림 방향을 스펙이 정하지 않았다')
    if value.copy_abs() > DEC_MAX:
        return None  # 4.5: decimal 범위를 넘으면 숫자가 아니다(1e400 등).
    if not _fits(value):
        raise OracleError(f'decimal로 반올림해야 하는 칸 {text!r}는 oracle 범위 밖')
    return value


def _checked(x: Decimal, what: str) -> Decimal:
    if not _fits(x):
        raise OracleError(f'{what}를 decimal로 반올림해야 한다 (oracle 범위 밖)')
    return x


def judge(f: CompareField, ref: str, cand: str):
    """칸 판정(4.5 칸 판정 표). None = 같음(차이 아님)."""
    if not f.is_number:
        return None if ref == cand else RESULT_OUTSIDE  # 4.6 텍스트 비교: 정규화 뒤 서수.
    rv = parse_number(ref)
    if rv is None:
        raise OracleError(f'기준 칸이 숫자가 아니다: {ref!r} (number 오류 입력, 범위 밖)')
    cv = parse_number(cand)
    if cv is None:
        return RESULT_NOT_NUMBER
    try:
        with localcontext(EXACT):
            return _judge_number(f, rv, cv)
    except DecimalException as e:
        raise OracleError(f'decimal 셈에서 반올림·넘침이 생겼다: {type(e).__name__} (oracle 범위 밖)') from None


def _judge_number(f: CompareField, rv: Decimal, cv: Decimal):
    d = (cv - rv).copy_abs()
    if d == 0:
        return None
    if _near_max(d):
        raise OracleError('차이가 decimal 최댓값 바로 위다. 스펙이 결과를 정하지 않았다')
    if d > DEC_MAX:
        return RESULT_OUTSIDE  # 넘침: d가 decimal 범위를 넘으면 outside.
    _checked(d, '차이')
    if f.tolerance_text is not None:
        tol = _checked(Decimal(f.tolerance_text), 'tolerance')
        return RESULT_WITHIN if d <= tol else RESULT_OUTSIDE  # 경계 포함.
    pct = _checked(Decimal(f.tolerance_pct_text), 'tolerance_pct')
    # 기준 값 기준. d × 100 ≤ |기준| × tolerance_pct. 나눗셈 없음. 경계 포함.
    lhs = d * 100
    rhs = rv.copy_abs() * pct
    if _near_max(lhs) or _near_max(rhs):
        raise OracleError('곱이 decimal 최댓값 바로 위다. 스펙이 결과를 정하지 않았다')
    lhs_over, rhs_over = lhs > DEC_MAX, rhs > DEC_MAX
    if not lhs_over:
        _checked(lhs, '차이 × 100')
    if not rhs_over:
        _checked(rhs, '|기준| × tolerance_pct')
    if not lhs_over and not rhs_over:
        return RESULT_WITHIN if lhs <= rhs else RESULT_OUTSIDE
    if lhs_over and not rhs_over:
        return RESULT_OUTSIDE  # 곱 d × 100만 넘침.
    if rhs_over and not lhs_over:
        return RESULT_WITHIN  # |기준| × tolerance_pct만 넘침.
    q = _checked(pct / 100, 'tolerance_pct / 100')  # 둘 다 넘침: d ≤ |기준| × (tolerance_pct / 100).
    p = rv.copy_abs() * q
    if p > DEC_MAX:
        raise OracleError('|기준| × (tolerance_pct / 100)이 넘친다. 스펙이 결과를 정하지 않았다')
    return RESULT_WITHIN if d <= _checked(p, '|기준| × (tolerance_pct / 100)') else RESULT_OUTSIDE


# ---- 파일 찾기 (4.1) ----

def is_supported_pattern(pattern: str) -> bool:
    """4.1 패턴 모양. 폴더 조각은 글자 그대로, 마지막 조각에만 '*'·'?'."""
    segs = pattern.split('/')
    for k, seg in enumerate(segs):
        if seg in ('', '.', '..'):
            return False
        if any(c in seg for c in '[]{}\\:'):
            return False
        if k < len(segs) - 1 and ('*' in seg or '?' in seg):
            return False
    return '**' not in segs[-1]


def matches(name_pattern: str, name: str) -> bool:
    """'*' 0자 이상, '?' UTF-16 한 단위. 서수(대소문자 구분)."""
    p, s = utf16_units(name_pattern), utf16_units(name)
    star, qmark = ord('*'), ord('?')
    # 동적 계획: ok[j] = 패턴 앞부분이 이름 앞 j 단위와 맞나.
    ok = [True] + [False] * len(s)
    for c in p:
        if c == star:
            for j in range(1, len(s) + 1):
                ok[j] = ok[j] or ok[j - 1]
        else:
            nxt = [False] * (len(s) + 1)
            for j in range(1, len(s) + 1):
                nxt[j] = ok[j - 1] and (c == qmark or c == s[j - 1])
            ok = nxt
    return ok[len(s)]


def _entries(folder: Path):
    try:
        with os.scandir(folder) as it:
            return list(it)
    except OSError as e:
        raise OracleError(f'폴더를 읽을 수 없다: {type(e).__name__} (file 오류 입력, 범위 밖)') from None


def find_files(case_dir: Path, pattern: str, label: str) -> list:
    """패턴 → 케이스 폴더 기준 상대 경로 목록(서수 정렬)."""
    if not is_supported_pattern(pattern):
        raise OracleError(f'{label}: 지원하지 않는 패턴 {pattern!r} (pattern 오류 입력, 범위 밖)')
    segs = pattern.split('/')
    folder = case_dir
    for seg in segs[:-1]:
        hit = [e for e in _entries(folder) if e.name == seg]  # 이름 목록에서 서수로 찾는다.
        if not hit or not hit[0].is_dir(follow_symlinks=False):
            if hit and hit[0].is_symlink():
                raise OracleError(f'{label}: 심볼릭 링크 폴더 {seg}는 oracle 범위 밖 (스펙 미확인)')
            raise OracleError(f'{label}: 폴더가 없다: {seg} (file 오류 입력, 범위 밖)')
        folder = folder / seg
    names = []
    for e in _entries(folder):
        if not matches(segs[-1], e.name):
            continue
        if e.is_symlink():
            raise OracleError(f'{label}: 심볼릭 링크 {e.name}는 oracle 범위 밖 (스펙 미확인)')
        if e.is_file(follow_symlinks=False):
            names.append(e.name)  # 이름이 맞아도 폴더는 파일이 아니다.
    if not names:
        raise OracleError(f'{label}: 일치하는 파일이 없다 (file 오류 입력, 범위 밖)')
    names.sort(key=ordinal_key)
    prefix = '/'.join(segs[:-1])
    return [f'{prefix}/{n}' if prefix else n for n in names]


# ---- 행 (4.2·4.3) ----

@dataclass(frozen=True)
class Row:
    key: str
    file: str
    line: int
    cells: dict  # compare 필드 → 칸(정규화 뒤)
    header: tuple = ()  # 그 파일의 머리 행 칸 전부(정규화 뒤). 3.1 CsvRow.Header
    values: tuple = ()  # 그 행의 칸 전부(정규화 뒤, compare에 없는 열 포함). 3.1 CsvRow.Cells


@dataclass(frozen=True)
class CompareRun:
    """대조 결과 본문(metrics.json의 compare)과 판정이 쓰는 메모리 결과(3.1). 미검증이면 행·머리 행이 없다."""
    body: dict
    reference_rows: list  # Row, 기준 파일 경로 서수 → 줄 순서
    reference_headers: list  # (파일, 머리 행 칸 tuple), 기준 파일 경로 서수 순
    scores: dict | None = None  # T-014: metrics.json의 scores(text-check.md 3.2). scores가 없으면 None


def read_side(case_dir: Path, files: list, key: str, fields: list, headers: list | None = None,
              group: str | None = None, allow_duplicates: bool = False, side_columns=()) -> list:
    """한쪽의 데이터 행(파일 경로 서수 → 줄). group: 기준 쪽에서 볼 유형 열(T-012). allow_duplicates: 결과 쪽(T-012).

    side_columns: 그 쪽에만 있어야 하는 열(T-014 체크리스트: 기준 쪽 column 항목, 결과 쪽 문장 열). 찾는 순서는
    키 열 → compare 필드 → group 열 → side_columns다. 값은 Row.cells에 compare 필드와 같이 담는다.
    """
    rows = []
    first = {}  # 키 → (파일, 줄)
    for rel in files:
        try:
            data = (case_dir / rel).read_bytes()
        except OSError as e:
            raise OracleError(f'{rel}: 파일을 읽을 수 없다: {type(e).__name__} (file 오류 입력, 범위 밖)') from None
        _header_line, header, data_rows = read_table(data, rel)
        if headers is not None:
            headers.append((rel, tuple(header)))
        index = {}
        for name in [key] + [f.field for f in fields] + ([group] if group is not None else []) + list(side_columns):
            if name in index:
                continue
            pos = [k for k, h in enumerate(header) if h == name]
            if not pos:
                raise OracleError(f'{rel}: 머리 행에 열이 없다: {name} (column 오류 입력, 범위 밖)')
            if len(pos) > 1:
                raise OracleError(f'{rel}: 머리 행에 같은 열이 둘 이상: {name} (duplicate-column 오류 입력, 범위 밖)')
            index[name] = pos[0]
        for r in data_rows:
            k = r.cells[index[key]]
            if k == '':
                raise OracleError(f'{rel}:{r.line}: 키가 비어 있다 (empty-key 오류 입력, 범위 밖)')
            if k in first and not allow_duplicates:
                raise OracleError(f'{rel}:{r.line}: 키가 중복된다: {k} (duplicate-key 오류 입력, 범위 밖)')
            first.setdefault(k, (rel, r.line))
            cells = {f.field: r.cells[index[f.field]] for f in fields}
            for name in side_columns:
                cells[name] = r.cells[index[name]]
            rows.append(Row(k, rel, r.line, cells, tuple(header), tuple(r.cells)))
    return rows


# ---- 셈 (4.4·4.8) ----

def percent(part: int, whole: int) -> JsonNumber:
    """4.8: 소수 첫째 자리, 0.05는 올림, 정수 셈. 늘 한 자리."""
    tenths = (2 * part * 1000 + whole) // (2 * whole)
    return JsonNumber(f'{tenths // 10}.{tenths % 10}')


def _not_verified(reason: str) -> dict:
    return {
        'status': STATUS_NOT_VERIFIED, 'reason': reason, 'key': None,
        'reference_files': [], 'candidate_files': [], 'counts': None,
        'accuracy_pct': None, 'missing_rate_pct': None, 'fields': [], 'items': [],
    }


def compare_body(case_dir: Path, case: Case) -> dict:
    return compare_run(case_dir, case).body


def compare_run(case_dir: Path, case: Case) -> CompareRun:
    """대조(4절)와 T-014 점수표 집계(text-check.md 4.3). 점수표는 대조가 미검증이어도 읽는다."""
    run = _compare_run(case_dir, case)
    if case.scores is None:
        return run
    if not case_dir.is_dir():
        raise OracleError('케이스 폴더가 없다 (file 오류 입력, 범위 밖)')
    from scores import score_body  # scores.py가 find_files를 이 모듈에서 가져간다.
    taken = set(run.body['reference_files']) | set(run.body['candidate_files'])
    # 4.3 규칙 2(리뷰 4차 N1): 대조가 inputs 파일을 읽었으면 출처 표의 건은 기준 자료의 키여야 한다.
    keys = {r.key for r in run.reference_rows} if run.body['status'] == STATUS_MEASURED else None
    return CompareRun(run.body, run.reference_rows, run.reference_headers, score_body(case_dir, case, taken, keys))


def _compare_run(case_dir: Path, case: Case) -> CompareRun:
    if case.inputs is None:
        return CompareRun(_not_verified(REASON_NO_INPUTS), [], [])  # 파일을 읽지 않는다.
    if not case.compare and case.checklist is None:  # T-014: 체크리스트만 있어도 대조한다.
        return CompareRun(_not_verified(REASON_NO_COMPARE_FIELDS), [], [])
    if not case_dir.is_dir():
        raise OracleError('케이스 폴더가 없다 (file 오류 입력, 범위 밖)')
    inputs, fields = case.inputs, case.compare
    ref_files = find_files(case_dir, inputs.reference, 'inputs.reference')
    cand_files = find_files(case_dir, inputs.candidate, 'inputs.candidate')
    overlap = set(ref_files) & set(cand_files)
    if overlap:
        raise OracleError(f'같은 파일이 기준·결과 양쪽에 나온다: {sorted(overlap, key=ordinal_key)[0]} (overlap 오류 입력, 범위 밖)')
    ref_headers = []
    cl = case.checklist
    ref_side = tuple(dict.fromkeys(i.column for i in cl.items if i.column is not None)) if cl else ()
    cand_side = (cl.text,) if cl else ()
    ref = read_side(case_dir, ref_files, inputs.key, fields, ref_headers, group=inputs.group, side_columns=ref_side)
    cand_all = read_side(case_dir, cand_files, inputs.key, fields, allow_duplicates=True, side_columns=cand_side)
    # 결과 쪽 같은 키의 첫 행(결과 파일 경로 서수 → 줄)이 대표 행, 둘째 이후 행은 항목 duplicate다(4.3, 13.2).
    cand, dup_ids, seen, repeated = [], set(), set(), set()
    for c in cand_all:
        if c.key in seen:
            dup_ids.add(id(c))
            repeated.add(c.key)
        else:
            seen.add(c.key)
            cand.append(c)
    if cl is not None and not fields and all(i.column is not None for i in cl.items):
        # 4.2 규칙 10: compare 필드도 any 항목도 없으면 column 칸이 빈 행에는 확인할 것이 없다. 결과 문장과 상관없다.
        for r in ref:
            if all(item_result(i, '', r.cells[i.column]) == SKIPPED for i in cl.items):
                raise OracleError(f'{r.file}:{r.line}: {NO_CHECK[1].format(key=r.key)} ({NO_CHECK[0]} 오류 입력, 범위 밖)')
    for r in ref:
        for f in fields:
            if f.is_number and parse_number(r.cells[f.field]) is None:
                raise OracleError(f'{r.file}:{r.line}: 숫자가 아니다: {r.cells[f.field]} (number 오류 입력, 범위 밖)')
    if not ref:
        raise OracleError('기준 자료에 데이터 행이 없다 (no-rows 오류 입력, 범위 밖)')

    cand_by_key = {c.key: c for c in cand}  # 찾기에만 쓴다. 출력 순서는 목록 순서. 키마다 첫 행.
    ref_keys = {r.key for r in ref}
    per_field = {f.field: {RESULT_OUTSIDE: 0, RESULT_WITHIN: 0, RESULT_NOT_NUMBER: 0} for f in fields}
    per_item = {i.item: {FOUND: 0, ABSENT: 0} for i in cl.items} if cl else {}
    items = []
    match = mismatch = missing = within_items = compared = 0

    def absent_items(r, c) -> list:
        """T-014 text-check.md 4.2: 이 결과 행에서 빠진 체크리스트 항목(쓴 순서). 항목 셈에 더한다."""
        if cl is None:
            return []
        out = []
        for it in cl.items:
            res = item_result(it, c.cells[cl.text], r.cells[it.column] if it.column is not None else None)
            if res == SKIPPED:
                continue
            per_item[it.item][res] += 1
            if res == ABSENT:
                out.append(it.item)
        return out

    def entry(item: dict, absent: list) -> dict:
        if cl is not None:
            item['missing_items'] = absent
        return item

    def differences(r, c) -> list:
        """4.5 칸 판정. 기준에 키가 있는 결과 행마다 한 번 부르고 필드 셈에 더한다(13.2)."""
        nonlocal compared
        compared += 1
        diffs = []
        for f in fields:
            res = judge(f, r.cells[f.field], c.cells[f.field])
            if res is None:
                continue
            per_field[f.field][res] += 1
            diffs.append({'field': f.field, 'result': res, 'reference': r.cells[f.field], 'candidate': c.cells[f.field]})
        return diffs

    # 기준 항목(기준 파일 경로 서수 → 줄 순서), 그다음 결과 쪽 항목(추가·중복, 결과 파일 경로 서수 → 줄 순서. 3.1, 12.2).
    for r in ref:
        c = cand_by_key.get(r.key)
        ref_pos = {'file': r.file, 'line': r.line}
        if c is None:
            missing += 1
            items.append(entry({'key': r.key, 'status': ITEM_MISSING, 'reference': ref_pos, 'candidate': None,
                                'differences': []}, []))
            continue
        diffs = differences(r, c)
        absent = absent_items(r, c)
        # 13.2: 결과 쪽에 두 번 이상 나온 키는 값과 상관없이 값 불일치다. 맞는 행이 앞에 와도 일치가 아니다.
        # T-014: 체크리스트 항목이 하나라도 빠지면 값 불일치다.
        bad = r.key in repeated or any(d['result'] != RESULT_WITHIN for d in diffs) or bool(absent)
        if bad:
            mismatch += 1
        else:
            match += 1
            if diffs:
                within_items += 1
        if diffs or bad:  # 차이 없는 일치는 items에 쓰지 않는다(3.2). 값 불일치는 차이가 없어도 쓴다(13.2).
            items.append(entry({'key': r.key, 'status': ITEM_MISMATCH if bad else ITEM_MATCH, 'reference': ref_pos,
                                'candidate': {'file': c.file, 'line': c.line}, 'differences': diffs}, absent))
    extra = duplicate = 0
    ref_by_key = {r.key: r for r in ref}
    for c in cand_all:
        if id(c) in dup_ids:
            duplicate += 1  # 13.2: 기준에 그 키가 있으면 기준 위치와 기준과의 차이, 없으면 null과 [].
            r = ref_by_key.get(c.key)
            items.append(entry({'key': c.key, 'status': ITEM_DUPLICATE,
                                'reference': {'file': r.file, 'line': r.line} if r is not None else None,
                                'candidate': {'file': c.file, 'line': c.line},
                                'differences': differences(r, c) if r is not None else []},
                               absent_items(r, c) if r is not None else []))
        elif c.key not in ref_keys:
            extra += 1
            items.append(entry({'key': c.key, 'status': ITEM_EXTRA, 'reference': None,
                                'candidate': {'file': c.file, 'line': c.line}, 'differences': []}, []))

    field_list = [{
        'field': f.field,
        'kind': KIND_NUMBER if f.is_number else KIND_TEXT,
        'tolerance': JsonNumber(f.tolerance_text) if f.tolerance_text is not None else None,
        'tolerance_pct': JsonNumber(f.tolerance_pct_text) if f.tolerance_pct_text is not None else None,
        'compared': compared,
        'outside': per_field[f.field][RESULT_OUTSIDE],
        'within': per_field[f.field][RESULT_WITHIN],
        'not_number': per_field[f.field][RESULT_NOT_NUMBER],
    } for f in fields]
    if match + mismatch + missing != len(ref) or match + mismatch + extra + duplicate != len(cand_all) \
            or compared != len(cand_all) - extra - sum(1 for c in cand_all if id(c) in dup_ids and c.key not in ref_keys):
        raise OracleError('셈이 맞지 않는다(oracle 내부 오류)')  # 3.2 counts·fields[].compared 정의(13.2 중복 포함).
    body = {
        'status': STATUS_MEASURED, 'reason': None, 'key': inputs.key,
        'reference_files': ref_files, 'candidate_files': cand_files,
        'counts': {'reference': len(ref), 'candidate': len(cand_all), 'match': match, 'mismatch': mismatch,
                   'missing': missing, 'extra': extra, 'within': within_items},
        'accuracy_pct': percent(match, len(ref)),
        'missing_rate_pct': percent(missing, len(ref)),
        'fields': field_list,
    }
    if cl is not None:  # T-014 text-check.md 3.2: fields 뒤, items 앞.
        body['checklist'] = {'text': cl.text, 'entries': [{
            'item': it.item,
            'column': it.column,
            'phrases': list(it.phrases),
            'required': per_item[it.item][FOUND] + per_item[it.item][ABSENT],
            'found': per_item[it.item][FOUND],
            'absent': per_item[it.item][ABSENT],
        } for it in cl.items]}
    if case.confusion is not None:  # T-015 confusion.md 3.1: fields·checklist 뒤, items 앞.
        body['confusion'] = confusion_body(case.confusion, ref, cand_by_key, repeated)
    body['items'] = items
    return CompareRun(body, ref, ref_headers)


def _case_yaml(case_dir: Path) -> bytes:
    # case-schema 2절: 이름은 정확히 case.yaml(서수 비교).
    if not case_dir.is_dir() or not any(e.name == CASE_FILE and e.is_file(follow_symlinks=False) for e in _entries(case_dir)):
        raise OracleError(f'{CASE_FILE}이 없다 (file 오류 입력, 범위 밖)')
    return (case_dir / CASE_FILE).read_bytes()


def metrics_bytes(case_dir: Path) -> bytes:
    case = parse_case(_case_yaml(case_dir), CASE_FILE)
    run = compare_run(case_dir, case)
    value = {'schema_version': SCHEMA_VERSION, 'case': case.name, 'compare': run.body}
    if run.scores is not None:  # T-014 text-check.md 3.2: compare 뒤. scores가 없으면 속성도 없다.
        value['scores'] = run.scores
    return render(value)


def main(argv: list) -> int:
    utf8_stdio()
    if len(argv) != 1:
        print('사용: compare.py <케이스 폴더>', file=sys.stderr)
        return 2
    try:
        out = metrics_bytes(Path(argv[0]))
    except OracleError as e:
        print(f'oracle: {e}', file=sys.stderr)
        return 2
    sys.stdout.buffer.write(out)
    sys.stdout.buffer.flush()
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
