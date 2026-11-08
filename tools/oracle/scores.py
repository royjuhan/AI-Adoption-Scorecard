# 가림 점수표 집계(text-check.md 4.3·4.4·3.2 scores). 스펙 글만 보고 짰다(src/는 읽지 않았다, ADR 0005 결정 9).
# 엔진은 사람이 매긴 점수를 모으기만 한다. 오류 입력은 OracleError로 멈춘다(오류 줄은 흉내 내지 않는다).
# 표준 라이브러리만 쓴다.
from __future__ import annotations

import re
from pathlib import Path

from oracle_common import OracleError, ordinal_key
from oracle_csv import read_table
from oracle_json import JsonNumber

SOURCE_AI, SOURCE_HUMAN = 'ai', 'human'
SOURCES = (SOURCE_AI, SOURCE_HUMAN)  # 4.3 출처 값. 이 순서가 sources[] 순서다.
SHEET_COLUMNS = ('rater', 'item', 'blind', 'score')  # 4.3 점수표 열
SOURCE_COLUMNS = ('blind', 'item', 'source')  # 4.3 출처 표 열
BLIND_KEPT, BLIND_REVEALED = 'kept', 'revealed'
# 4.4 가림 규칙. 표 순서가 findings 순서다.
RULE_SOURCE_COLUMN, RULE_SOURCE_VALUE, RULE_SOURCE_IN_BLIND = 'source_column', 'source_value', 'source_in_blind'
RULE_SOURCE_PATTERN = 'source_pattern'  # 4.4 표시 몰림(리뷰 4차, 재검토)
BLIND_RULES = (RULE_SOURCE_COLUMN, RULE_SOURCE_VALUE, RULE_SOURCE_IN_BLIND, RULE_SOURCE_PATTERN)
# 4.4 source_pattern 몰림 문턱: 갈린 건 8 이상, 반대쪽은 12건에 1건 이하, 몰린 쪽은 센 건의 1/4 이상.
PATTERN_MIN_SPLIT, PATTERN_MINORITY_PER, PATTERN_SHARE_PER = 8, 12, 4
SCORE_RE = re.compile(r'(?:0|[1-9][0-9]*)', re.ASCII)
# 4.4 조각: ASCII 영문자 묶음과 숫자 묶음. 영문자와 숫자 사이에서도 나눈다(리뷰 3차 B1: ai01·HUMAN03).
_TOKEN = re.compile(r'[A-Za-z]+|[0-9]+', re.ASCII)
_DIGITS = re.compile(r'[0-9]+', re.ASCII)


def ascii_lower(text: str) -> str:
    """A~Z만 a~z로. 문화권과 상관없다(4.4)."""
    return ''.join(chr(ord(c) + 32) if 'A' <= c <= 'Z' else c for c in text)


def blind_tokens(key: str) -> list:
    """4.4 source_in_blind: 가림 키를 ASCII 영문자 묶음과 숫자 묶음으로 나눈 조각(ASCII 소문자로)."""
    return [ascii_lower(t) for t in _TOKEN.findall(key)]


def shape(text: str) -> str:
    """4.4 source_pattern의 틀: ASCII 숫자 묶음(0~9)을 지운 글. 그 밖의 글자는 그대로(대소문자 그대로)."""
    return _DIGITS.sub('', text)


def leaning(a: int, h: int, counted: int) -> bool:
    """4.4 몰림. a·h: AI 쪽·사람 쪽으로 갈린 건 수. counted: 센 건 수(출처 표의 건 수).

    갈린 건 c = a + h ≥ 8, 12 × 반대쪽 ≤ c, 4 × 몰린 쪽 ≥ counted. 가림이 맞으면(건마다 어느 결과가 AI인지 동전)
    한 표시가 이렇게 몰릴 확률은 2/256 이하다(부호 검정, 양쪽: c = 8, 반대쪽 0일 때가 가장 크다).
    """
    c = a + h
    return c >= PATTERN_MIN_SPLIT and PATTERN_MINORITY_PER * min(a, h) <= c and PATTERN_SHARE_PER * max(a, h) >= counted


def mean_text(total: int, n: int) -> JsonNumber:
    """4.3 평균: 소수 둘째 자리, 0.005는 올림, 정수 셈. 늘 두 자리. n은 1 이상."""
    hundredths = (2 * total * 100 + n) // (2 * n)
    return JsonNumber(f'{hundredths // 100}.{hundredths % 100:02d}')


def _columns(header: list, names, rel: str) -> dict:
    index = {}
    for name in names:
        pos = [k for k, h in enumerate(header) if h == name]
        if not pos:
            raise OracleError(f'{rel}: 머리 행에 열이 없다: {name} (column 오류 입력, 범위 밖)')
        if len(pos) > 1:
            raise OracleError(f'{rel}: 머리 행에 같은 열이 둘 이상: {name} (duplicate-column 오류 입력, 범위 밖)')
        index[name] = pos[0]
    return index


def _read(case_dir: Path, rel: str):
    try:
        data = (case_dir / rel).read_bytes()
    except OSError as e:
        raise OracleError(f'{rel}: 파일을 읽을 수 없다: {type(e).__name__} (file 오류 입력, 범위 밖)') from None
    return read_table(data, rel)


def marks_of(text: str) -> set:
    """4.4 source_pattern: 글 하나의 표시. (종류, 값): 글 그대로, 틀(ASCII 숫자 묶음을 지운 글),
    첫 글자·끝 글자(ASCII 숫자가 아닐 때, 유니코드 문자 하나). 빈 값은 표시가 아니다."""
    out = set()
    if text != '':
        out.add(('text', text))
    if shape(text) != '':
        out.add(('shape', shape(text)))
    if text != '' and not '0' <= text[0] <= '9':
        out.add(('first', text[0]))
    if text != '' and not '0' <= text[-1] <= '9':
        out.add(('last', text[-1]))
    return out


def form(text: str) -> str:
    """4.4 키의 꼴: ASCII 숫자는 9, ASCII 대문자는 A, ASCII 소문자는 a로 바꾼 글. 그 밖의 글자는 그대로."""
    return ''.join('9' if '0' <= ch <= '9' else 'A' if 'A' <= ch <= 'Z' else 'a' if 'a' <= ch <= 'z' else ch
                   for ch in text)


def key_marks(key: str) -> set:
    """4.4 가림 키의 표시: 글 하나의 표시 넷 + 꼴, 글자 수, 글자 종류(ASCII 대문자 A·소문자 a·숫자 9가 있는지)."""
    out = marks_of(key)
    if key != '':
        out.add(('form', form(key)))
        out.add(('length', len(key.encode('utf-16-le')) // 2))  # 글자 수: UTF-16 단위(4.2 열 번호와 같다)
    out |= {('class', c) for c in 'Aa9' if c in form(key)}
    return out


def number_row(key: str) -> tuple:
    """4.4 숫자 줄: 키의 ASCII 숫자 묶음을 앞에서부터 정수로 읽은 것."""
    return tuple(int(d) for d in _DIGITS.findall(key))


def lower_key(ka: str, kh: str):
    """4.4 작은 쪽: 두 키가 모두 숫자 묶음이 있고 숫자 줄이 다르면 숫자 줄이 작은 키(앞 묶음부터 견줌.
    앞이 같고 묶음이 적으면 작다). 아니면 None."""
    na, nh = number_row(ka), number_row(kh)
    if not na or not nh or na == nh:
        return None
    return ka if na < nh else kh


def _row_marks(row: tuple) -> set:
    """점수표 한 행(파일, 줄, 머리 행, 칸)이 채점자에게 보이는 표시: 파일, 다른 열 칸의 표시."""
    rel, _line, header, cells = row
    out = {('file', rel)}
    for k, name in enumerate(header):
        if name not in SHEET_COLUMNS:
            out |= {('column', name) + m for m in marks_of(cells[k])}
    return out


def _pattern_findings(sources: dict, blind_pos: dict, row_of: dict, header_line_of: dict) -> list:
    """4.4 source_pattern. 건마다 두 결과의 표시를 견주어 한쪽에만 있는 표시를 센다. [(파일, 줄, 칸 자리, 값)] 순서대로.

    키는 채점자와 상관없이 그 건의 두 키를 견준다. 열·파일은 두 출처를 모두 매긴 채점자의 행만 보고,
    그 건의 AI 결과 행 표시 합과 사람 결과 행 표시 합을 견준다(같은 채점자가 두 결과를 다 매기지 않아도 된다).
    """
    pair = {}
    for blind, (item, source) in sources.items():
        pair.setdefault(item, {})[source] = blind
    seen = {}  # 채점자 → 매긴 출처
    for rater, blind in row_of:
        seen.setdefault(rater, set()).add(sources[blind][1])
    mixed = sorted((r for r, got in seen.items() if len(got) == len(SOURCES)), key=ordinal_key)
    lean = {}  # 표시 → [AI 쪽 건, 사람 쪽 건]
    counted = len(pair)  # 센 건: 출처 표의 건 수(건마다 AI 결과 하나, 사람 결과 하나)
    for item in sorted(pair, key=ordinal_key):
        ka, kh = pair[item][SOURCE_AI], pair[item][SOURCE_HUMAN]
        a_side = {('key',) + m for m in key_marks(ka) - key_marks(kh)}
        h_side = {('key',) + m for m in key_marks(kh) - key_marks(ka)}
        low = lower_key(ka, kh)
        if low is not None:
            (a_side if low == ka else h_side).add(('key', 'low'))
        ua = set().union(*(_row_marks(row_of[(r, ka)]) for r in mixed if (r, ka) in row_of))
        uh = set().union(*(_row_marks(row_of[(r, kh)]) for r in mixed if (r, kh) in row_of))
        if ua and uh:  # 두 결과 모두 두 출처를 매긴 채점자의 행이 있을 때만 열·파일을 견준다.
            a_side |= ua - uh
            h_side |= uh - ua
        for mark in a_side - h_side:
            lean.setdefault(mark, [0, 0])[0] += 1
        for mark in h_side - a_side:
            lean.setdefault(mark, [0, 0])[1] += 1
    lower = {lower_key(p[SOURCE_AI], p[SOURCE_HUMAN]) for p in pair.values()} - {None}
    found = {}  # (파일 서수, 줄, 칸 자리) → (파일, 줄, 칸 자리, 값). 같은 칸은 한 번.
    for mark, (a, h) in lean.items():
        if not leaning(a, h, counted):
            continue
        if mark[0] == 'key':  # 출처 표에서 그 표시가 있는 첫 키 행(작은 쪽은 건의 작은 키인 첫 행). 값은 키.
            has = (lambda b: b in lower) if mark == ('key', 'low') else (lambda b: mark[1:] in key_marks(b))
            rel, line, key = min(((rel, line, b) for b, (rel, line) in blind_pos.items() if has(b)),
                                 key=lambda x: (ordinal_key(x[0]), x[1]))
            hit = (rel, line, 0, key)
        elif mark[0] == 'file':  # 그 파일의 머리 행. 값은 경로.
            hit = (mark[1], header_line_of[mark[1]], -1, mark[1])
        else:  # 그 열 칸에 그 표시가 있는 첫 행. 값은 칸 글.
            hit = min(((rel, line, k, cells[k]) for rel, line, header, cells in row_of.values()
                       for k, name in enumerate(header) if name == mark[1] and mark[2:] in marks_of(cells[k])),
                      key=lambda x: (ordinal_key(x[0]), x[1], x[2]))
        found[(ordinal_key(hit[0]), hit[1], hit[2])] = hit
    return [found[k] for k in sorted(found)]


def score_body(case_dir: Path, case, taken: set, reference_keys=None) -> dict:
    """metrics.json의 scores(3.2). taken: 이미 쓴 파일(inputs). 겹치면 overlap 오류다.

    reference_keys: 대조가 inputs 파일을 읽었으면(measured) 기준 자료의 키. 출처 표의 건은 그 가운데 있어야 한다
    (4.3 규칙 2, 리뷰 4차 N1). 점수표만이면 None.
    """
    from compare import find_files  # compare.py가 이 모듈을 부른다. 순환을 피해 여기서 가져온다.

    sc = case.scores
    sheet_files = find_files(case_dir, sc.sheet, 'scores.sheet')
    source_files = find_files(case_dir, sc.sources, 'scores.sources')
    seen = set(taken)
    for rel in source_files + sheet_files:
        if rel in seen:
            raise OracleError(f'같은 파일이 두 번 나온다: {rel} (overlap 오류 입력, 범위 밖)')
        seen.add(rel)

    # 출처 표: 가림 키 → (건, 출처, 파일, 줄). 4.3.
    sources, item_rows, blind_pos = {}, {}, {}
    for rel in source_files:
        _line, header, rows = _read(case_dir, rel)
        ix = _columns(header, SOURCE_COLUMNS, rel)
        for row in rows:
            blind, item, source = (row.cells[ix[c]] for c in SOURCE_COLUMNS)
            if blind == '' or item == '':
                raise OracleError(f'{rel}:{row.line}: 키가 비어 있다 (empty-key 오류 입력, 범위 밖)')
            if source not in SOURCES:
                raise OracleError(f'{rel}:{row.line}: 출처는 ai 또는 human이다: {source} (source 오류 입력, 범위 밖)')
            if blind in sources:
                raise OracleError(f'{rel}:{row.line}: 가림 키가 중복된다: {blind} (duplicate-key 오류 입력, 범위 밖)')
            if reference_keys is not None and item not in reference_keys:
                raise OracleError(f'{rel}:{row.line}: 기준 자료에 없는 건이다: {item} (blind 오류 입력, 범위 밖)')
            sources[blind] = (item, source)
            blind_pos[blind] = (rel, row.line)
            item_rows.setdefault(item, []).append(source)
    if not sources:
        raise OracleError('출처 표에 데이터 행이 없다 (no-rows 오류 입력, 범위 밖)')
    for item, got in item_rows.items():
        if sorted(got) != sorted(SOURCES):
            raise OracleError(f'건마다 ai 하나와 human 하나가 있어야 한다: {item} (pair 오류 입력, 범위 밖)')

    # 점수표: (채점자, 가림 키) → 점수. 4.3.
    findings = []
    ratings = []  # (채점자, 건, 출처, 점수)
    pairs = set()
    row_of = {}  # (채점자, 가림 키) → (파일, 줄, 머리 행, 칸). 4.4 source_pattern
    header_line_of = {}  # 점수 파일 → 머리 행 줄
    for rel in sheet_files:
        header_line, header, rows = _read(case_dir, rel)
        header_line_of[rel] = header_line
        ix = _columns(header, SHEET_COLUMNS, rel)
        for name in header:  # 4.4 source_column: 열 이름도 ASCII 소문자로 본다(리뷰 2차 N1). 값은 쓴 글 그대로.
            if ascii_lower(name) == 'source':
                findings.append((0, rel, header_line, name))
        for row in rows:
            rater, item, blind, score = (row.cells[ix[c]] for c in SHEET_COLUMNS)
            if rater == '' or item == '' or blind == '':
                raise OracleError(f'{rel}:{row.line}: 키가 비어 있다 (empty-key 오류 입력, 범위 밖)')
            if not SCORE_RE.fullmatch(score) or not sc.min <= int(score) <= sc.max:
                raise OracleError(f'{rel}:{row.line}: 점수는 {sc.min}~{sc.max}의 정수다: {score} (score 오류 입력, 범위 밖)')
            if blind not in sources:
                raise OracleError(f'{rel}:{row.line}: 출처 표에 없는 가림 키다: {blind} (blind 오류 입력, 범위 밖)')
            if sources[blind][0] != item:
                raise OracleError(f'{rel}:{row.line}: 출처 표의 건과 다르다: {item} (blind 오류 입력, 범위 밖)')
            if (rater, blind) in pairs:
                raise OracleError(f'{rel}:{row.line}: 점수가 중복된다: {rater} {blind} (duplicate-key 오류 입력, 범위 밖)')
            pairs.add((rater, blind))
            row_of[(rater, blind)] = (rel, row.line, tuple(header), tuple(row.cells))
            score_col = ix['score']
            for k, cell in enumerate(row.cells):
                if k != score_col and ascii_lower(cell) in SOURCES:
                    findings.append((1, rel, row.line, cell))
            ratings.append((rater, item, sources[blind][1], int(score)))
    rated = {b for _r, b in pairs}
    for blind in sources:
        if blind not in rated:
            raise OracleError(f'점수가 하나도 없다: {blind} (blind 오류 입력, 범위 밖)')
    for blind, (rel, line) in blind_pos.items():
        if any(t in SOURCES for t in blind_tokens(blind)):
            findings.append((2, rel, line, blind))
    findings.extend((3, rel, line, value) for rel, line, _col, value in
                    _pattern_findings(sources, blind_pos, row_of, header_line_of))
    # 4.4: 규칙 순서 → 파일 경로 서수 → 줄. 같은 줄은 찾은 순서(안정 정렬).
    findings.sort(key=lambda f: (f[0], ordinal_key(f[1]), f[2]))

    per_source = {s: [] for s in SOURCES}
    for _rater, _item, source, score in ratings:
        per_source[source].append(score)
    source_list = []
    for s in SOURCES:
        vals = per_source[s]
        source_list.append({
            'source': s,
            'results': sum(1 for _i, src in sources.values() if src == s),
            'ratings': len(vals),
            'sum': sum(vals),
            'mean': mean_text(sum(vals), len(vals)),
            'distribution': [{'score': v, 'count': sum(1 for x in vals if x == v)} for v in range(sc.min, sc.max + 1)],
        })

    # 4.3 건마다 비교: AI 평균과 사람 평균을 곱셈으로(나눗셈 없음).
    better = same = worse = 0
    for item in sorted(item_rows, key=ordinal_key):
        a = [x for _r, i, s, x in ratings if i == item and s == SOURCE_AI]
        h = [x for _r, i, s, x in ratings if i == item and s == SOURCE_HUMAN]
        left, right = sum(a) * len(h), sum(h) * len(a)
        if left > right:
            better += 1
        elif left == right:
            same += 1
        else:
            worse += 1

    raters = sorted({r for r, _i, _s, _x in ratings}, key=ordinal_key)
    by_rater = []
    for rater in raters:
        entry = {'rater': rater}
        for s in SOURCES:
            vals = [x for r, _i, src, x in ratings if r == rater and src == s]
            entry[f'{s}_ratings'] = len(vals)
            entry[f'{s}_mean'] = mean_text(sum(vals), len(vals)) if vals else None
        by_rater.append(entry)

    return {
        'sheet_files': sheet_files,
        'sources_files': source_files,
        'min': sc.min,
        'max': sc.max,
        'items': len(item_rows),
        'raters': raters,
        'ratings': len(ratings),
        'blind': {
            'status': BLIND_REVEALED if findings else BLIND_KEPT,
            'findings': [{'rule': BLIND_RULES[r], 'file': f, 'line': ln, 'value': v} for r, f, ln, v in findings],
        },
        'sources': source_list,
        'paired': {'better': better, 'same': same, 'worse': worse},
        'by_rater': by_rater,
    }
