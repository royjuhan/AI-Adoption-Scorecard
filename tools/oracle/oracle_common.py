# oracle 공통 도구. src/를 읽지 않고 스펙만 보고 짰다(ADR 0005 결정 9).
# 표준 라이브러리만 쓴다. 네트워크·외부 프로세스 없음.
from __future__ import annotations

import decimal
import hashlib
import sys
import unicodedata

# 반올림이 생기면 예외를 내는 decimal 문맥. 파이썬 기본 문맥(28자리)은 abs()·+·×에서 조용히 반올림한다.
# 셈하는 값은 .NET decimal 범위(29자리, 소수 28자리) 안이라 1000자리면 반올림이 생기지 않는다.
EXACT = decimal.Context(
    prec=1000, Emax=decimal.MAX_EMAX, Emin=decimal.MIN_EMIN,
    traps=[decimal.InvalidOperation, decimal.DivisionByZero, decimal.Overflow, decimal.Underflow,
           decimal.Subnormal, decimal.Inexact, decimal.Rounded],
)


class OracleError(Exception):
    """oracle이 다루지 않는 입력. 오류 입력이거나, 스펙이 결과 모양을 정하지 않은 값이다.

    엔진의 오류 출력을 흉내 내지 않는다. 멈추고 이유만 알린다.
    """


def decode_utf8(data: bytes, source: str) -> str:
    """바이트를 글로 바꾼다. case-schema 4.8, metrics 4.2의 인코딩 규칙.

    UTF-16 BOM은 오류. 앞의 UTF-8 BOM 하나는 뗀다. UTF-8이 아닌 바이트는 오류.
    줄바꿈은 바꾸지 않는다(부르는 쪽이 정한다).
    """
    if data[:2] in (b'\xff\xfe', b'\xfe\xff'):
        raise OracleError(f'{source}: UTF-16이다 (encoding 오류 입력, 범위 밖)')
    offset = 0
    if data[:3] == b'\xef\xbb\xbf':
        data = data[3:]
        offset = 3
    try:
        return data.decode('utf-8', errors='strict')
    except UnicodeDecodeError as e:
        raise OracleError(f'{source}: UTF-8이 아니다 (바이트 {e.start + offset}, encoding 오류 입력, 범위 밖)') from None


def ordinal_key(text: str) -> bytes:
    """서수 비교 열쇠. .NET 서수 비교는 UTF-16 코드 단위 순서다. UTF-16BE 바이트 순서와 같다."""
    return text.encode('utf-16-be', 'surrogatepass')


def utf16_units(text: str) -> list[int]:
    """UTF-16 코드 단위 목록. 패턴 '?'와 열 번호가 이 단위로 센다(metrics 4.1, 4.2)."""
    raw = text.encode('utf-16-le', 'surrogatepass')
    return [raw[k] | (raw[k + 1] << 8) for k in range(0, len(raw), 2)]


def is_net_whitespace(ch: str) -> bool:
    """char.IsWhiteSpace와 같은 집합(metrics 4.6). U+0009~U+000D, U+0085, 그리고 Zs·Zl·Zp 범주.

    U+00A0, U+3000, U+2003은 Zs, U+2028은 Zl이라 들어간다. U+200B·U+FEFF(Cf)·U+00AD(Cf)는 아니다.
    파이썬 str.strip()은 U+001C~U+001F도 떼므로 쓰지 않는다.
    """
    o = ord(ch)
    if 0x09 <= o <= 0x0D or o == 0x85:
        return True
    return unicodedata.category(ch) in ('Zs', 'Zl', 'Zp')


def net_trim(text: str) -> str:
    """string.Trim()과 같다: 앞뒤의 is_net_whitespace 글자를 뗀다."""
    start, end = 0, len(text)
    while start < end and is_net_whitespace(text[start]):
        start += 1
    while end > start and is_net_whitespace(text[end - 1]):
        end -= 1
    return text[start:end]


def sha256_hex(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def utf8_stdio() -> None:
    """표준 출력·오류를 UTF-8, LF로 고정한다. 콘솔 인코딩(cp1252 등)·Windows 줄바꿈 변환과 상관없게 하려는 것이다."""
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding='utf-8', errors='backslashreplace', newline='\n')
        except (AttributeError, ValueError):
            pass
