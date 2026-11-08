# 범주 필드 혼동행렬(confusion.md 3.1·4.2). 스펙 글만 보고 짰다(src/는 읽지 않았다, ADR 0005 결정 9).
# 대조(compare.py)가 짝지은 기준 행과 결과 행으로 센다. 셈만 쓴다. 표준 라이브러리만 쓴다.
from __future__ import annotations

from oracle_common import OracleError, ordinal_key
from oracle_json import JsonNumber


def percent(part: int, whole: int):
    """metrics.md 4.8과 같은 셈(소수 첫째 자리, 0.05는 올림, 정수 셈). whole이 0이면 null(4.2 규칙 6)."""
    if whole == 0:
        return None
    tenths = (2 * part * 1000 + whole) // (2 * whole)
    return JsonNumber(f'{tenths // 10}.{tenths % 10}')


def result_class(row, field: str, cand_by_key: dict, repeated: set):
    """4.2 규칙 1: 기준 행의 결과 범주. 결과 행이 없거나(누락) 결과 쪽에 키가 두 번 이상이면(metrics.md 4.3) None."""
    c = cand_by_key.get(row.key)
    if c is None or row.key in repeated:
        return None
    return c.cells[field]


def confusion_body(conf, ref_rows: list, cand_by_key: dict, repeated: set) -> dict:
    """compare.confusion 객체(3.1). conf: oracle_case.Confusion. ref_rows: 기준 행(기준 파일 경로 서수 → 줄)."""
    field = conf.field
    pairs = [(r.cells[field], result_class(r, field, cand_by_key, repeated)) for r in ref_rows]
    # 4.2 규칙 3: 범주 목록 = 기준 범주 ∪ 짝지은 결과 범주 ∪ 관심 범주, 서수 순. 추가 행은 보지 않는다.
    classes = sorted({a for a, _ in pairs} | {b for _, b in pairs if b is not None} | set(conf.positive), key=ordinal_key)
    order = {c: k for k, c in enumerate(classes)}
    counts: dict = {}
    for a, b in pairs:
        counts[(a, b)] = counts.get((a, b), 0) + 1
    # 4.2 규칙 4: 0이 아닌 칸만. 기준 범주 순 → 결과 범주 순, null(결과 없음)은 끝.
    cells = [{'reference': a, 'candidate': b, 'count': n}
             for (a, b), n in sorted(counts.items(), key=lambda kv: (order[kv[0][0]], 1 if kv[0][1] is None else 0,
                                                                     order.get(kv[0][1], 0)))]
    positive = []
    for p in conf.positive:
        reference = sum(1 for a, _ in pairs if a == p)
        predicted = sum(1 for _, b in pairs if b == p)
        hit = sum(1 for a, b in pairs if a == p and b == p)
        positive.append({
            'class': p, 'reference': reference, 'predicted': predicted, 'hit': hit,
            'miss': reference - hit, 'false_alarm': predicted - hit,
            'recall_pct': percent(hit, reference), 'precision_pct': percent(hit, predicted),
        })
    if sum(c['count'] for c in cells) != len(ref_rows):
        raise OracleError('혼동행렬 칸 합이 기준 행 수와 다르다(oracle 내부 오류)')
    return {'field': field, 'classes': classes, 'cells': cells, 'positive': positive}
