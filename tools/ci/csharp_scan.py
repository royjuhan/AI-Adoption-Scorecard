r"""C# 테스트 소스를 대충이 아니라 토큰으로 읽어 세는 도구(regression-guard용).

센다:
- 테스트 특성 `[Fact]`·`[Theory]`(이름 끝 Attribute, Xunit. 한정 이름 포함). 특성 구역 안의 이름 자리만 센다.
  `using X = Xunit.FactAttribute;` 별칭 `[X]`도 센다.
- Skip 표시: `Skip =`·`SkipWhen =` 같은 Skip* 대입, Skip* 속성 선언·재정의(`Skip =>`, `Skip {`, `Skip ??=`),
  특성 인자 `Explicit =`, `Trait(...)` 문자열 인자에 skip, `Assert.Skip(`·`Skip.If(` 호출, `SkipException`,
  `SkippableFact`·`SkippableTheory`, `#if` 지시문, `*FactAttribute`·`*TheoryAttribute`를 상속한 클래스,
  `Fact`·`FactAttribute` 같은 이름의 클래스(가림), Fact·Theory 특성을 가리키는 `using` 별칭.
- `GuardTargets` 클래스의 `All` 목록에 등록된 대상 이름.

식별자와 일반 문자열의 `\uXXXX`·`\UXXXXXXXX` 이스케이프는 풀고 본다(`Sk\u0069p`는 `Skip`이다).

세지 않는다: 주석 안, 문자열·문자 리터럴 안, 거짓으로 확정된 `#if` 구역 안(`#if false` 등).
정의되지 않은 기호(`#if NEVER`)는 참·거짓을 모른다고 보고 구역을 센다. 대신 `#if` 자체가 Skip 표시로 걸린다.

C# 컴파일러가 아니다. 테스트 코드에서 쓰는 문법만 다룬다. 한계는 README에 적는다.
"""

from __future__ import annotations

from dataclasses import dataclass, field

TEST_ATTRIBUTES = {"Fact", "Theory", "FactAttribute", "TheoryAttribute"}
SKIPPABLE_ATTRIBUTES = {"SkippableFact", "SkippableTheory", "SkippableFactAttribute", "SkippableTheoryAttribute"}
TRAIT_ATTRIBUTES = {"Trait", "TraitAttribute"}
# 이 이름으로 끝나는 기반 클래스를 상속하면 Skip 표시다(재정의로 Skip을 걸 수 있다).
TEST_ATTRIBUTE_BASE_SUFFIXES = ("FactAttribute", "TheoryAttribute")
HEX_DIGITS = set("0123456789abcdefABCDEF")
SIMPLE_ESCAPES = {"n": "\n", "r": "\r", "t": "\t", "0": "\0", "a": "\a", "b": "\b", "f": "\f", "v": "\v", "e": "\x1b"}
ATTRIBUTE_TARGETS = {"assembly", "module", "field", "event", "method", "param", "property", "return", "type", "typevar"}

PUNCT3 = {"??=", "<<=", ">>=", "..."}
PUNCT2 = {"==", "!=", "<=", ">=", "=>", "&&", "||", "::", "??", "?.", "++", "--", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<", "->", ".."}


@dataclass(frozen=True)
class Tok:
    kind: str  # id, str, char, num, punct
    text: str  # 식별자·기호 글자, 문자열이면 값(대략 해석)
    line: int


@dataclass(frozen=True)
class Directive:
    name: str  # if, elif, else, endif, define, ...
    expr: str
    line: int


@dataclass
class FileScan:
    facts: list[tuple[int, str]] = field(default_factory=list)  # (줄, 특성 이름)
    skips: list[tuple[int, str]] = field(default_factory=list)  # (줄, 종류)
    guard_names: list[str] | None = None  # GuardTargets.All 대상 이름. 클래스가 없으면 None

    @property
    def fact_count(self) -> int:
        return len(self.facts)

    def skip_kinds(self) -> dict[str, int]:
        counts: dict[str, int] = {}
        for _, kind in self.skips:
            counts[kind] = counts.get(kind, 0) + 1
        return counts


# ---------------------------------------------------------------- 전처리 식


def _eval_pp(expr: str, defined: dict[str, bool]) -> bool | None:
    """#if 식을 참·거짓·모름(None)으로. 모르는 기호는 None."""
    toks: list[str] = []
    i = 0
    while i < len(expr):
        c = expr[i]
        if c.isspace():
            i += 1
        elif expr.startswith("//", i):
            break
        elif expr[i:i + 2] in ("&&", "||", "==", "!="):
            toks.append(expr[i:i + 2])
            i += 2
        elif c in "!()":
            toks.append(c)
            i += 1
        elif c.isalnum() or c == "_":
            j = i
            while j < len(expr) and (expr[j].isalnum() or expr[j] == "_"):
                j += 1
            toks.append(expr[i:j])
            i = j
        else:
            return None
    pos = 0

    def peek() -> str | None:
        return toks[pos] if pos < len(toks) else None

    def take() -> str:
        nonlocal pos
        t = toks[pos]
        pos += 1
        return t

    def primary() -> bool | None:
        t = take()
        if t == "!":
            v = primary()
            return None if v is None else not v
        if t == "(":
            v = orexpr()
            if take() != ")":
                raise ValueError
            return v
        if t == "true":
            return True
        if t == "false":
            return False
        if t in ("&&", "||", "==", "!=", ")"):
            raise ValueError
        return defined.get(t)

    def eq() -> bool | None:
        v = primary()
        while peek() in ("==", "!="):
            op = take()
            w = primary()
            v = None if v is None or w is None else ((v == w) if op == "==" else (v != w))
        return v

    def andexpr() -> bool | None:
        v = eq()
        while peek() == "&&":
            take()
            w = eq()
            if v is False or w is False:
                v = False
            elif v is None or w is None:
                v = None
            else:
                v = True
        return v

    def orexpr() -> bool | None:
        v = andexpr()
        while peek() == "||":
            take()
            w = andexpr()
            if v is True or w is True:
                v = True
            elif v is None or w is None:
                v = None
            else:
                v = False
        return v

    try:
        if not toks:
            return None
        v = orexpr()
        if pos != len(toks):
            return None
        return v
    except (ValueError, IndexError):
        return None


# ---------------------------------------------------------------- 토큰


def tokenize(src: str) -> tuple[list[Tok], list[Directive]]:
    """주석을 빼고 토큰을 낸다. 거짓 #if 구역은 건너뛴다. 지시문은 따로 돌려준다."""
    if src.startswith("﻿"):
        src = src[1:]
    n = len(src)
    i = 0
    line = 1
    at_line_start = True
    toks: list[Tok] = []
    directives: list[Directive] = []
    defined: dict[str, bool] = {}
    # 프레임: [부모가 활성인가, 앞 갈래가 참이었나(True/False/None), 지금 활성인가]
    stack: list[list] = []

    def active() -> bool:
        return not stack or stack[-1][2]

    def directive(text: str, at: int) -> None:
        body = text.strip()[1:].strip()
        name = ""
        k = 0
        while k < len(body) and body[k].isalpha():
            k += 1
        name, expr = body[:k], body[k:].strip()
        directives.append(Directive(name, expr, at))
        if name == "if":
            parent = active()
            if not parent:
                stack.append([False, True, False])
                return
            v = _eval_pp(expr, defined)
            stack.append([True, v, v is not False])
        elif name == "elif" and stack:
            frame = stack[-1]
            if not frame[0] or frame[1] is True:
                frame[2] = False
                return
            v = _eval_pp(expr, defined)
            frame[2] = v is not False
            if frame[1] is False:
                frame[1] = v
        elif name == "else" and stack:
            frame = stack[-1]
            if not frame[0] or frame[1] is True:
                frame[2] = False
            else:
                frame[2] = True
                if frame[1] is False:
                    frame[1] = True
        elif name == "endif" and stack:
            stack.pop()
        elif name in ("define", "undef") and active():
            sym = expr.split("//")[0].strip()
            if sym:
                defined[sym] = name == "define"

    def read_line_end(j: int) -> int:
        while j < n and src[j] not in "\r\n":
            j += 1
        return j

    def skip_newline(j: int) -> int:
        nonlocal line
        if src.startswith("\r\n", j):
            line += 1
            return j + 2
        if j < n and src[j] in "\r\n":
            line += 1
            return j + 1
        return j

    def count_lines(a: int, b: int) -> None:
        nonlocal line
        seg = src[a:b]
        line += seg.count("\n") + seg.count("\r") - seg.count("\r\n")

    def scan_hole(j: int, close_braces: int) -> int:
        """보간 구멍 {…} 안을 끝까지 넘긴다. j는 구멍 첫 글자. 끝 } 다음 위치를 돌려준다."""
        depth = 0
        while j < n:
            c = src[j]
            if c == '"' or (c in "@$" and j + 1 < n and src[j + 1] in '"@$'):
                j, _ = scan_string(j)
                continue
            if c == "'":
                j = scan_char(j)
                continue
            if c in "([{":
                depth += 1
            elif c in ")]":
                depth -= 1
            elif c == "}":
                if depth == 0:
                    if src.startswith("}" * close_braces, j):
                        return j + close_braces
                    return j + 1
                depth -= 1
            elif c == ":" and depth == 0:
                # 형식 지정자: } 까지 글자 그대로
                k = j
                while k < n and src[k] != "}":
                    k += 1
                count_lines(j, k)
                j = k
                continue
            elif c in "\r\n":
                j = skip_newline(j)
                continue
            j += 1
        return j

    def scan_string(j: int) -> tuple[int, str]:
        """문자열 리터럴 하나. (다음 위치, 값)"""
        dollars = 0
        verbatim = False
        while j < n and src[j] in "$@":
            if src[j] == "$":
                dollars += 1
            else:
                verbatim = True
            j += 1
        # 원시 문자열 """…"""
        q = 0
        while j + q < n and src[j + q] == '"':
            q += 1
        if q >= 3:
            start = j + q
            end = src.find('"' * q, start)
            if end < 0:
                end = n
            count_lines(start, end)
            return min(end + q, n), src[start:end]
        j += 1  # 여는 따옴표
        value: list[str] = []
        while j < n:
            c = src[j]
            if verbatim:
                if c == '"':
                    if j + 1 < n and src[j + 1] == '"':
                        value.append('"')
                        j += 2
                        continue
                    return j + 1, "".join(value)
                if c in "\r\n":
                    j = skip_newline(j)
                    value.append("\n")
                    continue
            else:
                if c == "\\" and j + 1 < n:
                    j, ch = read_escape(j)
                    value.append(ch)
                    continue
                if c == '"':
                    return j + 1, "".join(value)
                if c in "\r\n":  # 닫히지 않은 문자열
                    return j, "".join(value)
            if dollars and c == "{":
                if j + 1 < n and src[j + 1] == "{":
                    value.append("{")
                    j += 2
                    continue
                j = scan_hole(j + 1, 1)
                continue
            if dollars and c == "}" and j + 1 < n and src[j + 1] == "}":
                value.append("}")
                j += 2
                continue
            value.append(c)
            j += 1
        return j, "".join(value)

    def read_escape(j: int) -> tuple[int, str]:
        """j는 역슬래시. 일반 문자열의 이스케이프 하나를 푼다. (다음 위치, 글자)"""
        kind = src[j + 1]
        if kind in "uU":
            got = unicode_escape(j)
            if got is not None:
                return got
        if kind == "x":
            k = j + 2
            while k < n and k < j + 6 and src[k] in HEX_DIGITS:
                k += 1
            if k > j + 2:
                return k, chr(int(src[j + 2:k], 16))
        return j + 2, SIMPLE_ESCAPES.get(kind, kind)

    def unicode_escape(j: int) -> tuple[int, str] | None:
        """j는 역슬래시. \\uXXXX 또는 \\UXXXXXXXX를 푼다. 아니면 None."""
        if j + 1 >= n or src[j + 1] not in "uU":
            return None
        width = 4 if src[j + 1] == "u" else 8
        digits = src[j + 2:j + 2 + width]
        if len(digits) != width or not all(h in HEX_DIGITS for h in digits):
            return None
        code = int(digits, 16)
        if code > 0x10FFFF:
            return None
        return j + 2 + width, chr(code)

    def ident_at(j: int) -> tuple[int, str] | None:
        """j에서 식별자 하나. 유니코드 이스케이프를 푼 이름을 돌려준다. 식별자가 아니면 None."""
        out: list[str] = []
        k = j
        while k < n:
            ch = src[k]
            if ch == "\\":
                got = unicode_escape(k)
                if got is None:
                    break
                nxt, dec = got
                if not (dec.isalpha() or dec == "_" or (out and dec.isalnum())):
                    break
                out.append(dec)
                k = nxt
                continue
            if ch.isalpha() or ch == "_" or (out and ch.isalnum()):
                out.append(ch)
                k += 1
                continue
            break
        return (k, "".join(out)) if out else None

    def scan_char(j: int) -> int:
        j += 1
        if j < n and src[j] == "\\":
            j += 2
        else:
            j += 1
        while j < n and src[j] != "'" and src[j] not in "\r\n":
            j += 1
        return min(j + 1, n)

    while i < n:
        c = src[i]
        if c in "\r\n":
            i = skip_newline(i)
            at_line_start = True
            continue
        if c in " \t\f\v":
            i += 1
            continue
        if at_line_start and c == "#":
            end = read_line_end(i)
            directive(src[i:end], line)
            i = end
            continue
        if not active():
            # 비활성 구역: 다음 줄로 넘긴다(지시문만 본다)
            i = read_line_end(i)
            continue
        at_line_start = False
        if src.startswith("//", i):
            i = read_line_end(i)
            continue
        if src.startswith("/*", i):
            end = src.find("*/", i + 2)
            end = n if end < 0 else end + 2
            count_lines(i, end)
            i = end
            continue
        start_line = line
        if c == '"' or (c in "$@" and i + 1 < n and (src[i + 1] == '"' or (src[i + 1] in "$@" and src[i + 1:].lstrip("$@").startswith('"')))):
            i, value = scan_string(i)
            toks.append(Tok("str", value, start_line))
            continue
        if c == "'":
            i = scan_char(i)
            toks.append(Tok("char", "", start_line))
            continue
        if c == "@" and i + 1 < n and (src[i + 1].isalpha() or src[i + 1] in "_\\"):
            i += 1
            c = src[i]
        if c.isalpha() or c == "_" or c == "\\":
            got = ident_at(i)
            if got is not None:
                i, name = got
                toks.append(Tok("id", name, start_line))
                continue
        if c.isdigit():
            j = i
            while j < n and (src[j].isalnum() or src[j] == "_" or (src[j] == "." and j + 1 < n and src[j + 1].isdigit())):
                j += 1
            toks.append(Tok("num", src[i:j], start_line))
            i = j
            continue
        if src[i:i + 3] in PUNCT3:
            toks.append(Tok("punct", src[i:i + 3], start_line))
            i += 3
            continue
        if src[i:i + 2] in PUNCT2:
            toks.append(Tok("punct", src[i:i + 2], start_line))
            i += 2
            continue
        toks.append(Tok("punct", c, start_line))
        i += 1
    return toks, directives


# ---------------------------------------------------------------- 분석


def _is(tok: Tok | None, text: str) -> bool:
    return tok is not None and tok.kind == "punct" and tok.text == text


def analyze(src: str) -> FileScan:
    toks, directives = tokenize(src)
    scan = FileScan()
    n = len(toks)

    def at(k: int) -> Tok | None:
        return toks[k] if 0 <= k < n else None

    for d in directives:
        if d.name == "if":
            scan.skips.append((d.line, "#if"))

    aliases = _declarations(toks, scan)
    test_names = TEST_ATTRIBUTES | aliases

    # 괄호 쌓기: (글자, 특성 구역인가, 특성 이름)
    stack: list[tuple[str, bool, str | None]] = []
    last_closed_attr = False
    expect_name = False
    i = 0
    while i < n:
        t = toks[i]
        prev = at(i - 1)
        in_attr_top = bool(stack) and stack[-1][0] == "[" and stack[-1][1]

        if expect_name and in_attr_top and t.kind == "id":
            # [target: Name] 의 target
            if t.text in ATTRIBUTE_TARGETS and _is(at(i + 1), ":"):
                i += 2
                continue
            # 한정 이름 a.b::c
            j = i
            name = t.text
            while _is(at(j + 1), ".") or _is(at(j + 1), "::"):
                nxt = at(j + 2)
                if nxt is None or nxt.kind != "id":
                    break
                name = nxt.text
                j += 2
            expect_name = False
            if name in test_names:
                scan.facts.append((t.line, name))
            elif name in SKIPPABLE_ATTRIBUTES:
                scan.skips.append((t.line, name))
            # 인자 괄호에 특성 이름을 붙인다
            if _is(at(j + 1), "("):
                stack.append(("(", False, name))
                i = j + 2
                continue
            i = j + 1
            continue

        if t.kind == "punct":
            if t.text in SKIP_MEMBER_PUNCT and prev is not None and prev.kind == "id" and _is_skip_name(prev.text):
                # Skip 속성 선언·재정의: Skip => "…", Skip { get … }, Skip ??= "…"
                scan.skips.append((prev.line, prev.text + t.text))
            if t.text == "[":
                is_attr = prev is None or (prev.kind == "punct" and prev.text in (";", "{", "}")) or (_is(prev, "]") and last_closed_attr)
                stack.append(("[", is_attr, None))
                expect_name = is_attr
            elif t.text in ("(", "{"):
                stack.append((t.text, False, None))
                expect_name = False
            elif t.text in (")", "]", "}"):
                opener = {")": "(", "]": "[", "}": "{"}[t.text]
                while stack and stack[-1][0] != opener:
                    stack.pop()
                closed = stack.pop() if stack else (opener, False, None)
                last_closed_attr = t.text == "]" and closed[1]
                expect_name = False
            elif t.text == "," and in_attr_top:
                expect_name = True
            elif t.text == "=" and prev is not None and prev.kind == "id":
                # Skip = "…", SkipWhen = …, SkipUnless = … (어디서든), Explicit = true (특성 인자 안)
                in_attr_args = bool(stack) and stack[-1][0] == "(" and stack[-1][2] is not None
                if _is_skip_name(prev.text):
                    scan.skips.append((prev.line, prev.text + "="))
                elif prev.text == "Explicit" and in_attr_args:
                    scan.skips.append((prev.line, "Explicit="))
            i += 1
            continue

        if t.kind == "str" and stack and stack[-1][0] == "(" and stack[-1][2] in TRAIT_ATTRIBUTES:
            if "skip" in t.text.casefold():
                scan.skips.append((t.line, "Trait(skip)"))
        elif t.kind == "id":
            nxt, nxt2, nxt3 = at(i + 1), at(i + 2), at(i + 3)
            if t.text == "Assert" and _is(nxt, ".") and nxt2 is not None and nxt2.text in ("Skip", "SkipWhen", "SkipUnless") and _is(nxt3, "("):
                scan.skips.append((t.line, "Assert." + nxt2.text))
            elif t.text == "Skip" and _is(nxt, ".") and nxt2 is not None and nxt2.text in ("If", "IfNot") and _is(nxt3, "("):
                scan.skips.append((t.line, "Skip." + nxt2.text))
            elif t.text == "SkipException":
                scan.skips.append((t.line, "SkipException"))
            elif t.text == "class" and nxt is not None and nxt.text == "GuardTargets":
                scan.guard_names = _guard_names(toks, i + 2)
        i += 1
    return scan


SKIP_MEMBER_PUNCT = {"=>", "{", "??="}


def _is_skip_name(name: str) -> bool:
    """Skip, SkipWhen, SkipUnless 같은 이름(Skip 뒤가 대문자)."""
    return name == "Skip" or (name.startswith("Skip") and len(name) > 4 and name[4].isupper())


def _qualified_last(toks: list[Tok], k: int, stop: set[str]) -> tuple[str | None, int]:
    """k부터 한정 이름(a.b::c<T>)을 읽어 마지막 조각과 다음 위치를 돌려준다. 이름이 아니면 (None, k)."""
    n = len(toks)
    last = None
    while k < n:
        t = toks[k]
        if t.kind == "id":
            last = t.text
            k += 1
            if k < n and (_is(toks[k], ".") or _is(toks[k], "::")):
                k += 1
                continue
            break
        break
    if last is None:
        return None, k
    # 형식 인자 <…>는 건너뛴다
    if k < n and _is(toks[k], "<"):
        depth = 0
        while k < n:
            if _is(toks[k], "<"):
                depth += 1
            elif _is(toks[k], ">"):
                depth -= 1
                if depth == 0:
                    k += 1
                    break
            elif toks[k].kind == "punct" and toks[k].text in stop:
                break
            k += 1
    return last, k


def _is_test_attribute_type(name: str | None, aliases: set[str]) -> bool:
    return name is not None and (name.endswith(TEST_ATTRIBUTE_BASE_SUFFIXES) or name in TEST_ATTRIBUTES or name in aliases)


def _declarations(toks: list[Tok], scan: FileScan) -> set[str]:
    """using 별칭과 클래스 선언을 본다. Skip 표시를 scan에 더하고, Fact·Theory 별칭 이름을 돌려준다.

    - `using X = …FactAttribute;`(global 포함): 별칭이 Fact·Theory 특성을 가리키거나, 별칭 이름이 Fact 등이면 표시.
    - `class X : …FactAttribute`·`…TheoryAttribute`(별칭 포함): 상속한 특성은 Skip을 재정의할 수 있어 표시.
    - `class Fact`·`class FactAttribute` 등: 같은 이름공간의 [Fact]를 가려 테스트가 사라질 수 있어 표시.
    """
    n = len(toks)
    aliases: set[str] = set()
    for k, t in enumerate(toks):
        if t.kind != "id" or t.text != "using":
            continue
        name_tok = toks[k + 1] if k + 1 < n else None
        if name_tok is None or name_tok.kind != "id" or not (k + 2 < n and _is(toks[k + 2], "=")):
            continue
        target, _ = _qualified_last(toks, k + 3, {";"})
        if target is not None and target.endswith(TEST_ATTRIBUTE_BASE_SUFFIXES):
            aliases.add(name_tok.text)
            scan.skips.append((t.line, "using-alias"))
        elif name_tok.text in TEST_ATTRIBUTES:
            scan.skips.append((t.line, "using-alias"))
    for k, t in enumerate(toks):
        if t.kind != "id" or t.text != "class":
            continue
        name_tok = toks[k + 1] if k + 1 < n else None
        if name_tok is None or name_tok.kind != "id":
            continue
        if name_tok.text in TEST_ATTRIBUTES or name_tok.text in aliases:
            scan.skips.append((name_tok.line, "shadow-attribute"))
        _, m = _qualified_last(toks, k + 1, {"{", ";", ":"})
        if m >= n or not _is(toks[m], ":"):
            continue
        m += 1
        while m < n:
            base, nxt = _qualified_last(toks, m, {"{", ";"})
            if base is None:
                break
            if _is_test_attribute_type(base, aliases):
                scan.skips.append((name_tok.line, "derived-attribute"))
                break
            if nxt < n and _is(toks[nxt], ","):
                m = nxt + 1
                continue
            break
    return aliases


def _guard_names(toks: list[Tok], start: int) -> list[str]:
    """class GuardTargets 뒤에서 `All = [ … ]`(또는 `{ … }`)의 new("이름", "샘플", …) 이름들."""
    n = len(toks)
    i = start
    while i + 1 < n:
        if toks[i].kind == "id" and toks[i].text == "All" and _is(toks[i + 1], "="):
            break
        i += 1
    else:
        return []
    i += 2
    # new GuardTarget[] 같은 앞부분을 건너 첫 [ 또는 { 를 찾는다. [] 는 배열 표시라 넘긴다.
    while i < n:
        if _is(toks[i], "[") and i + 1 < n and _is(toks[i + 1], "]"):
            i += 2
            continue
        if _is(toks[i], "[") or _is(toks[i], "{"):
            break
        if _is(toks[i], ";"):
            return []
        i += 1
    if i >= n:
        return []
    open_text = toks[i].text
    close_text = "]" if open_text == "[" else "}"
    depth = 0
    end = i
    while end < n:
        if _is(toks[end], open_text):
            depth += 1
        elif _is(toks[end], close_text):
            depth -= 1
            if depth == 0:
                break
        end += 1
    names: list[str] = []
    k = i
    while k < end:
        if toks[k].kind == "id" and toks[k].text == "new":
            m = k + 1
            while m < end and toks[m].kind == "id":
                m += 1
            if m < end and _is(toks[m], "("):
                args: list[str] = []
                m += 1
                while m < end and len(args) < 2:
                    if toks[m].kind == "id" and _is(toks[m + 1] if m + 1 < n else None, ":"):
                        m += 2
                        continue
                    if toks[m].kind == "str":
                        args.append(toks[m].text)
                        m += 1
                        if m < end and _is(toks[m], ","):
                            m += 1
                            continue
                    break
                if len(args) == 2:
                    names.append(args[0])
                k = m
                continue
        k += 1
    return names
