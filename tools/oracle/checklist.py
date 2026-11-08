# 체크리스트 대조(text-check.md 4.2). 결과 문장 칸에 항목 문구가 있는지 문자열 규칙으로만 본다.
# 스펙 글만 보고 짰다(src/는 읽지 않았다, ADR 0005 결정 9). 자연어 처리·AI 없음.
# 표준 라이브러리만 쓴다.
from __future__ import annotations

from oracle_common import is_net_whitespace

# 4.2 항목 결과. (결과 값, 뜻)
FOUND, ABSENT, SKIPPED = 'found', 'absent', 'skipped'


def fold_spaces(text: str) -> str:
    """4.2: 공백 글자(char.IsWhiteSpace)가 이어진 곳을 U+0020 하나로. 앞뒤 공백은 뗀다.

    칸은 이미 metrics 4.6 정규화(CRLF·CR → LF, 앞뒤 공백 떼기)를 거쳤다. 이 접기는 체크리스트 비교에만 쓴다.
    """
    out, space = [], False
    for ch in text:
        if is_net_whitespace(ch):
            space = True
            continue
        if space and out:
            out.append(' ')
        space = False
        out.append(ch)
    return ''.join(out)


def item_result(item, text: str, reference_cell) -> str:
    """항목 하나의 결과. item: oracle_case.ChecklistItem. text: 결과 문장 칸(정규화 뒤).

    reference_cell: column 항목이면 기준 행의 그 칸(정규화 뒤), any 항목이면 None.
    column 항목의 칸이 비면 그 행에는 요구하지 않는다(skipped). 비교는 서수 부분 문자열이다.
    """
    folded = fold_spaces(text)
    if item.column is not None:
        phrase = fold_spaces(reference_cell)
        if phrase == '':
            return SKIPPED
        return FOUND if phrase in folded else ABSENT
    for p in item.phrases:
        phrase = fold_spaces(p)
        if phrase != '' and phrase in folded:
            return FOUND
    return ABSENT
