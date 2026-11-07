# JSON 바이트 쓰기. classification.md 3.3 바이트 규칙(metrics.md 3.3이 같은 표를 쓴다)을 보고 짰다.
# 들여쓰기 2칸, 줄바꿈 LF, 끝에 LF 하나, UTF-8 BOM 없음, 배열 원소는 한 줄에 하나, 빈 배열은 [].
# 이스케이프는 3.3 이스케이프 표대로다. 표가 모양을 정하지 않은 글자는 OracleError로 멈춘다(README "이스케이프").
from __future__ import annotations

import re
import unicodedata

from oracle_common import OracleError

_NUMBER_TEXT = re.compile(r'-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?', re.ASCII)

# 그대로 쓰는 범주. 표의 ①②③④(No), 한글(Lo), NFD 한글 자모(Lo), <>&'+·백틱(ASCII 기호)에서
# 넓혀 읽었다(해석): BMP의 문자(L*)·결합 기호(M*)·숫자(N*)·문장 부호(P*)·기호(S*)는 그대로 쓴다.
_RAW_CATEGORIES = ('L', 'M', 'N', 'P', 'S')


class JsonNumber:
    """decimal 글을 그대로 쓰는 숫자(metrics 3.3 "decimal 그대로": 90.0, 1.0, 1.50, 0)."""

    __slots__ = ('text',)

    def __init__(self, text: str):
        if not _NUMBER_TEXT.fullmatch(text):
            raise OracleError(f'JSON 숫자 글 모양이 아니다: {text!r}')
        self.text = text


def _escape(text: str) -> str:
    out = ['"']
    for ch in text:
        o = ord(ch)
        if ch == '"':
            out.append('\\"')
        elif ch == '\\':
            out.append('\\\\')
        elif ch == '\t':
            out.append('\\t')
        elif ch in '\n\r\b\f':
            # 3.3 표에는 탭과 U+0001만 있다. 짧은 꼴(\n)인지 \u000A인지 표가 정하지 않았다.
            raise OracleError(f'이스케이프 모양을 스펙이 정하지 않은 글자 U+{o:04X}')
        elif o < 0x20 or o == 0x7F or o == 0x2028:
            out.append(f'\\u{o:04X}')
        elif o > 0xFFFF:
            v = o - 0x10000
            out.append(f'\\u{0xD800 + (v >> 10):04X}\\u{0xDC00 + (v & 0x3FF):04X}')
        elif o == 0x20 or unicodedata.category(ch)[0] in _RAW_CATEGORIES:
            out.append(ch)
        else:
            # Zs(U+0020 밖), Zp(U+2029), C1 제어, Cf, Co, Cn. 3.3 표에 없다.
            raise OracleError(f'이스케이프 여부를 스펙이 정하지 않은 글자 U+{o:04X}')
    out.append('"')
    return ''.join(out)


def _dump(value, depth: int) -> str:
    pad = '  ' * depth
    inner = '  ' * (depth + 1)
    if value is None:
        return 'null'
    if value is True:
        return 'true'
    if value is False:
        return 'false'
    if isinstance(value, int):
        return str(value)
    if isinstance(value, JsonNumber):
        return value.text
    if isinstance(value, str):
        return _escape(value)
    if isinstance(value, list):
        if not value:
            return '[]'
        return '[\n' + ',\n'.join(inner + _dump(v, depth + 1) for v in value) + '\n' + pad + ']'
    if isinstance(value, dict):
        if not value:
            return '{}'
        parts = [f'{inner}{_escape(k)}: {_dump(v, depth + 1)}' for k, v in value.items()]
        return '{\n' + ',\n'.join(parts) + '\n' + pad + '}'
    raise OracleError(f'JSON으로 쓸 수 없는 값: {type(value).__name__}')


def render(value) -> bytes:
    """값 → JSON 바이트. 끝에 LF 하나."""
    return (_dump(value, 0) + '\n').encode('utf-8')
