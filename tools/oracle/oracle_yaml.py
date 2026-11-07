# YAML 부분집합 읽기. 예제 case.yaml·answers.yaml이 쓰는 모양만 받는다(README "받는 YAML").
# 받지 않는 것(앵커·별칭·태그·여러 문서·여러 줄 흐름 모음·여러 줄 plain·접는 블록 '>')은 OracleError로 멈춘다.
# case-schema 4.7이 오류로 정한 기능은 받지 않는다. 받지 않는 기능을 엔진이 어떻게 읽든 oracle은 답을 내지 않는다.
# 표준 라이브러리만 쓴다.
from __future__ import annotations

from dataclasses import dataclass

from oracle_common import OracleError, decode_utf8

# YAML 1.2 core 스키마 null 낱말(case-schema 4.4·4.6). plain일 때만 null이다.
NULL_WORDS = ('', '~', 'null', 'Null', 'NULL')

# plain 스칼라가 시작할 수 없는 글자(지시자). 받지 않는 기능도 여기서 걸린다.
_INDICATORS = '-?:,[]{}#&*!|>\'"%@`'

# 글 안에 있으면 받지 않는 글자. YAML이 인쇄할 수 없는 글자로 보거나(C0·DEL·C1),
# YAML 1.1 줄바꿈(U+0085·U+2028·U+2029)이거나, 두 번째 BOM(U+FEFF)이다. case-schema 4.8 표 참고.
_REFUSED = {chr(c) for c in range(0x00, 0x20) if c not in (0x09, 0x0A)}
_REFUSED |= {chr(c) for c in range(0x7F, 0xA0)}
_REFUSED |= {' ', ' ', '﻿', '￾', '￿'}


@dataclass(frozen=True)
class Scalar:
    text: str
    style: str  # 'plain' | 'single' | 'double' | 'literal'
    line: int  # 1부터

    @property
    def is_null(self) -> bool:
        return self.style == 'plain' and self.text in NULL_WORDS


class Mapping(dict):
    """키 순서를 지키는 매핑. line은 1부터."""

    def __init__(self, line: int):
        super().__init__()
        self.line = line


class Sequence(list):
    def __init__(self, line: int):
        super().__init__()
        self.line = line


def load(data: bytes, source: str):
    """바이트 → 루트 매핑. 루트가 매핑이 아니면 OracleError."""
    text = decode_utf8(data, source)
    # case-schema 4.8: LF·CRLF·CR 모두 한 줄.
    text = text.replace('\r\n', '\n').replace('\r', '\n')
    for ch in text:
        if ch in _REFUSED:
            raise OracleError(f'{source}: 받지 않는 글자 U+{ord(ch):04X} (범위 밖)')
    root = _Parser(text, source).parse()
    if not isinstance(root, Mapping):
        raise OracleError(f'{source}: 루트가 매핑이 아니다')
    return root


class _Parser:
    def __init__(self, text: str, source: str):
        self.source = source
        self.lines = text.split('\n')
        self.n = len(self.lines)
        # 목록 항목 '- key: v'의 'key: v'를 그 열의 매핑 줄로 다시 읽을 때 쓴다: 줄 번호 → (들여쓰기, 내용).
        self.virtual: dict[int, tuple[int, str]] = {}

    # ---- 줄 도구 ----

    def fail(self, i: int, message: str):
        raise OracleError(f'{self.source}:{i + 1}: {message} (oracle YAML 범위 밖)')

    def indent_of(self, i: int) -> int:
        if i in self.virtual:
            return self.virtual[i][0]
        raw = self.lines[i]
        return len(raw) - len(raw.lstrip(' '))

    def content(self, i: int) -> str:
        if i in self.virtual:
            return self.virtual[i][1]
        raw = self.lines[i]
        c = raw.lstrip(' ')
        if c.startswith('\t'):
            self.fail(i, '들여쓰기에 탭을 썼다')
        return c.rstrip(' \t')

    def is_blank_or_comment(self, i: int) -> bool:
        if i in self.virtual:
            return False
        c = self.lines[i].strip(' \t')
        return c == '' or c.startswith('#')

    def next_sig(self, i: int) -> int:
        while i < self.n and self.is_blank_or_comment(i):
            i += 1
        return i

    def is_seq_line(self, i: int) -> bool:
        c = self.content(i)
        return c == '-' or c.startswith('- ')

    # ---- 문서 ----

    def parse(self):
        for i, raw in enumerate(self.lines):
            if raw.rstrip(' ') in ('---', '...') or raw.startswith(('--- ', '... ', '%')):
                self.fail(i, '문서 표시(---, ...)·지시(%)는 받지 않는다')
        i = self.next_sig(0)
        if i >= self.n:
            raise OracleError(f'{self.source}: 내용이 없다 (empty 오류 입력, 범위 밖)')
        if self.indent_of(i) != 0:
            self.fail(i, '첫 줄이 들여 쓰였다')
        node, j = self.parse_block(i, 0)
        j = self.next_sig(j)
        if j < self.n:
            self.fail(j, '읽지 못한 줄이 남았다')
        return node

    def parse_block(self, i: int, indent: int):
        if self.is_seq_line(i):
            return self.parse_seq(i, indent)
        return self.parse_map(i, indent)

    def parse_map(self, i: int, indent: int):
        m = Mapping(i + 1)
        while True:
            i = self.next_sig(i)
            if i >= self.n:
                return m, i
            ind = self.indent_of(i)
            if ind < indent:
                return m, i
            if ind > indent:
                self.fail(i, '들여쓰기가 맞지 않는다')
            if self.is_seq_line(i):
                self.fail(i, '매핑 자리에 목록 항목이 있다')
            key, rest = self.split_key(i, self.content(i))
            if key in m:
                raise OracleError(f'{self.source}:{i + 1}: 중복된 키 {key} (duplicate-key 오류 입력, 범위 밖)')
            if rest == '' or rest.startswith('#'):
                j = self.next_sig(i + 1)
                if j < self.n and (self.indent_of(j) > indent or (self.indent_of(j) == indent and self.is_seq_line(j))):
                    value, i = self.parse_block(j, self.indent_of(j))
                else:
                    value, i = Scalar('', 'plain', i + 1), i + 1
            elif rest[0] in '|>':
                value, i = self.parse_literal(i, rest, indent)
            else:
                value = self.parse_inline(i, rest)
                i += 1
            m[key] = value

    def parse_seq(self, i: int, indent: int):
        s = Sequence(i + 1)
        while True:
            i = self.next_sig(i)
            if i >= self.n:
                return s, i
            ind = self.indent_of(i)
            if ind < indent:
                return s, i
            if ind > indent:
                self.fail(i, '목록 항목 줄이 아니다')
            if not self.is_seq_line(i):
                # '키:' 바로 아래 같은 열에 쓴 목록('steps:\n- a')이 끝났다. 부모 매핑이 이어 읽는다.
                return s, i
            c = self.content(i)
            rest = c[1:].lstrip(' ')
            col = indent + (len(c) - len(rest))
            if rest == '' or rest.startswith('#'):
                j = self.next_sig(i + 1)
                if j < self.n and self.indent_of(j) > indent:
                    value, i = self.parse_block(j, self.indent_of(j))
                else:
                    value, i = Scalar('', 'plain', i + 1), i + 1
            elif rest == '-' or rest.startswith('- '):
                self.fail(i, '한 줄 안의 겹친 목록(- - x)은 받지 않는다')
            elif self.looks_like_entry(rest):
                # '- name: x' : 그 열에서 시작하는 매핑으로 읽는다.
                self.virtual[i] = (col, rest)
                value, i = self.parse_map(i, col)
            else:
                value = self.parse_inline(i, rest)
                i += 1
            s.append(value)

    def looks_like_entry(self, text: str) -> bool:
        if text[0] in _INDICATORS:
            return False
        k = self.find_key_colon(text)
        return k is not None

    @staticmethod
    def find_key_colon(text: str):
        """plain 키 뒤의 ':' 위치. ':' 뒤가 공백이거나 줄 끝이어야 한다. 없으면 None."""
        pos = 0
        while True:
            k = text.find(':', pos)
            if k < 0:
                return None
            if k + 1 == len(text) or text[k + 1] == ' ':
                return k
            pos = k + 1

    def split_key(self, i: int, text: str):
        if text[0] in _INDICATORS:
            self.fail(i, f'키가 지시자 {text[0]!r}로 시작한다(따옴표 키·복합 키·앵커 등)')
        k = self.find_key_colon(text)
        if k is None:
            self.fail(i, '매핑 줄(키: 값)이 아니다')
        key = text[:k].rstrip(' ')
        if key == '' or ' #' in key or '\t#' in key:
            self.fail(i, '키를 읽지 못했다')
        return key, text[k + 1:].lstrip(' ')

    def parse_literal(self, i: int, header: str, indent: int):
        """블록 스칼라 '|' (clip), '|-' (strip), '|+' (keep). '>'와 들여쓰기 숫자는 받지 않는다."""
        head = header.split('#', 1)[0].rstrip(' ') if ' #' in header else header
        if head not in ('|', '|-', '|+'):
            self.fail(i, f'블록 스칼라 머리 {head!r}는 받지 않는다')
        chomp = head[1:]
        j = i + 1
        body_indent = None
        collected = []
        while j < self.n:
            raw = self.lines[j]
            if raw.strip(' ') == '':
                collected.append('')
                j += 1
                continue
            ind = len(raw) - len(raw.lstrip(' '))
            if body_indent is None:
                if ind <= indent:
                    break
                body_indent = ind
            if ind < body_indent:
                break
            collected.append(raw[body_indent:])
            j += 1
        # 끝의 빈 줄은 블록 뒤의 빈 줄일 수도 있다. 내용 줄 뒤에서 자른다.
        while collected and collected[-1] == '' and chomp != '+':
            collected.pop()
        if not collected:
            text = ''
        elif chomp == '-':
            text = '\n'.join(collected)
        else:
            text = '\n'.join(collected) + '\n'
        return Scalar(text, 'literal', i + 1), j

    # ---- 한 줄 값 ----

    def parse_inline(self, i: int, text: str):
        value, pos = self.parse_value(i, text, 0, flow=False)
        rest = text[pos:]
        if rest.strip(' \t') == '':
            return value
        # 주석은 공백 뒤의 '#'. plain 값은 공백을 먹고 '#' 앞에서 멈추므로 앞 글자도 본다.
        if rest.lstrip(' \t').startswith('#') and (rest[0] in ' \t' or (pos > 0 and text[pos - 1] in ' \t')):
            return value
        self.fail(i, f'값 뒤에 읽지 못한 글자 {rest!r}')

    def parse_value(self, i: int, text: str, pos: int, flow: bool):
        while pos < len(text) and text[pos] == ' ':
            pos += 1
        if pos >= len(text):
            if flow:
                self.fail(i, '흐름 모음이 한 줄 안에서 닫히지 않았다')
            return Scalar('', 'plain', i + 1), pos
        ch = text[pos]
        if ch == '{':
            return self.parse_flow_map(i, text, pos)
        if ch == '[':
            return self.parse_flow_seq(i, text, pos)
        if ch == '"':
            return self.parse_double(i, text, pos)
        if ch == "'":
            return self.parse_single(i, text, pos)
        if ch in '&*!':
            raise OracleError(f'{self.source}:{i + 1}: 앵커·별칭·태그 {ch!r} (alias·tag 오류 입력, 범위 밖)')
        if flow and ch in ',}':
            return Scalar('', 'plain', i + 1), pos
        if ch == '#':
            if flow:
                self.fail(i, '흐름 모음 안의 주석은 받지 않는다')
            return Scalar('', 'plain', i + 1), pos
        if ch in '%@`|>]},':
            self.fail(i, f'값이 지시자 {ch!r}로 시작한다')
        if ch in '-?:' and (pos + 1 == len(text) or text[pos + 1] in ' ,]}'):
            self.fail(i, f'값이 지시자 {ch!r}로 시작한다')
        return self.parse_plain(i, text, pos, flow)

    def parse_plain(self, i: int, text: str, pos: int, flow: bool):
        start = pos
        while pos < len(text):
            ch = text[pos]
            if ch == '#' and pos > start and text[pos - 1] in ' \t':
                break
            if ch == ':' and (pos + 1 == len(text) or text[pos + 1] == ' ' or (flow and text[pos + 1] in ',]}')):
                if flow:
                    break  # 흐름 매핑 키 끝. 값 자리에서 만나면 부르는 쪽이 오류로 본다.
                self.fail(i, '값 안에 ": "가 있다(매핑 값 자리에 매핑)')
            if flow and ch in ',[]{}':
                if ch in '[{':
                    self.fail(i, 'plain 값 안에 흐름 지시자가 있다')
                break
            pos += 1
        value = text[start:pos].rstrip(' \t')
        return Scalar(value, 'plain', i + 1), pos

    def parse_double(self, i: int, text: str, pos: int):
        escapes = {
            '0': '\0', 'a': '\a', 'b': '\b', 't': '\t', '\t': '\t', 'n': '\n', 'v': '\v', 'f': '\f',
            'r': '\r', 'e': '\x1b', ' ': ' ', '"': '"', '/': '/', '\\': '\\', 'N': '\x85', '_': '\xa0',
            'L': ' ', 'P': ' ',
        }
        out = []
        pos += 1
        while True:
            if pos >= len(text):
                self.fail(i, '큰따옴표가 이 줄에서 닫히지 않았다(여러 줄 따옴표 값은 받지 않는다)')
            ch = text[pos]
            if ch == '"':
                return Scalar(''.join(out), 'double', i + 1), pos + 1
            if ch == '\\':
                if pos + 1 >= len(text):
                    self.fail(i, '줄 끝 역슬래시(줄 이음)는 받지 않는다')
                e = text[pos + 1]
                if e in escapes:
                    out.append(escapes[e])
                    pos += 2
                    continue
                width = {'x': 2, 'u': 4, 'U': 8}.get(e)
                digits = text[pos + 2:pos + 2 + width] if width else ''
                if not width or len(digits) != width or any(c not in '0123456789abcdefABCDEF' for c in digits):
                    self.fail(i, f'모르는 이스케이프 \\{e}')
                code = int(digits, 16)
                if 0xD800 <= code <= 0xDFFF or code > 0x10FFFF:
                    self.fail(i, '서로게이트·범위 밖 코드 포인트 이스케이프')
                out.append(chr(code))
                pos += 2 + width
                continue
            out.append(ch)
            pos += 1

    def parse_single(self, i: int, text: str, pos: int):
        out = []
        pos += 1
        while True:
            if pos >= len(text):
                self.fail(i, '작은따옴표가 이 줄에서 닫히지 않았다(여러 줄 따옴표 값은 받지 않는다)')
            ch = text[pos]
            if ch == "'":
                if pos + 1 < len(text) and text[pos + 1] == "'":
                    out.append("'")
                    pos += 2
                    continue
                return Scalar(''.join(out), 'single', i + 1), pos + 1
            out.append(ch)
            pos += 1

    def skip_spaces(self, text: str, pos: int) -> int:
        while pos < len(text) and text[pos] == ' ':
            pos += 1
        return pos

    def parse_flow_map(self, i: int, text: str, pos: int):
        m = Mapping(i + 1)
        pos += 1
        while True:
            pos = self.skip_spaces(text, pos)
            if pos >= len(text):
                self.fail(i, '흐름 매핑이 한 줄 안에서 닫히지 않았다')
            if text[pos] == '}':
                return m, pos + 1
            # 키
            if text[pos] in '"\'':
                key_node, pos = (self.parse_double if text[pos] == '"' else self.parse_single)(i, text, pos)
                key = key_node.text
            else:
                if text[pos] in _INDICATORS:
                    self.fail(i, f'흐름 매핑 키가 지시자 {text[pos]!r}로 시작한다')
                key_node, pos = self.parse_plain(i, text, pos, flow=True)
                key = key_node.text
            pos = self.skip_spaces(text, pos)
            if pos >= len(text) or text[pos] != ':':
                self.fail(i, '흐름 매핑 항목에 ":"가 없다')
            pos += 1
            if pos < len(text) and text[pos] not in ' ,}':
                self.fail(i, '흐름 매핑 ":" 뒤에 공백이 없다')
            if key == '':
                self.fail(i, '빈 키')
            if key in m:
                raise OracleError(f'{self.source}:{i + 1}: 중복된 키 {key} (duplicate-key 오류 입력, 범위 밖)')
            value, pos = self.parse_value(i, text, pos, flow=True)
            if isinstance(value, Scalar) and value.style == 'plain' and pos < len(text) and text[pos] == ':':
                self.fail(i, '흐름 매핑 값 안에 ":"가 있다')
            m[key] = value
            pos = self.skip_spaces(text, pos)
            if pos >= len(text):
                self.fail(i, '흐름 매핑이 한 줄 안에서 닫히지 않았다')
            if text[pos] == ',':
                pos += 1
                continue
            if text[pos] == '}':
                return m, pos + 1
            self.fail(i, f'흐름 매핑에서 읽지 못한 글자 {text[pos]!r}')

    def parse_flow_seq(self, i: int, text: str, pos: int):
        s = Sequence(i + 1)
        pos += 1
        while True:
            pos = self.skip_spaces(text, pos)
            if pos >= len(text):
                self.fail(i, '흐름 목록이 한 줄 안에서 닫히지 않았다')
            if text[pos] == ']':
                return s, pos + 1
            if text[pos] == ',':
                self.fail(i, '흐름 목록에 빈 항목이 있다')
            value, pos = self.parse_value(i, text, pos, flow=True)
            if isinstance(value, Scalar) and value.style == 'plain' and pos < len(text) and text[pos] == ':':
                self.fail(i, '흐름 목록 안의 한 쌍 매핑은 받지 않는다')
            s.append(value)
            pos = self.skip_spaces(text, pos)
            if pos >= len(text):
                self.fail(i, '흐름 목록이 한 줄 안에서 닫히지 않았다')
            if text[pos] == ',':
                pos += 1
                continue
            if text[pos] == ']':
                return s, pos + 1
            self.fail(i, f'흐름 목록에서 읽지 못한 글자 {text[pos]!r}')
