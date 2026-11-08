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
BLIND_RULES = (RULE_SOURCE_COLUMN, RULE_SOURCE_VALUE, RULE_SOURCE_IN_BLIND)
SCORE_RE = re.compile(r'(?:0|[1-9][0-9]*)', re.ASCII)
_TOKEN = re.compile(r'[A-Za-z0-9]+', re.ASCII)


def ascii_lower(text: str) -> str:
    """A~Z만 a~z로. 문화권과 상관없다(4.4)."""
    return ''.join(chr(ord(c) + 32) if 'A' <= c <= 'Z' else c for c in text)


def blind_tokens(key: str) -> list:
    """4.4 source_in_blind: 가림 키를 ASCII 영문자·숫자가 아닌 글자로 나눈 조각(ASCII 소문자로)."""
    return [ascii_lower(t) for t in _TOKEN.findall(key)]


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


def score_body(case_dir: Path, case, taken: set) -> dict:
    """metrics.json의 scores(3.2). taken: 이미 쓴 파일(inputs). 겹치면 overlap 오류다."""
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
    for rel in sheet_files:
        header_line, header, rows = _read(case_dir, rel)
        ix = _columns(header, SHEET_COLUMNS, rel)
        if 'source' in header:
            findings.append((0, rel, header_line, 'source'))
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
