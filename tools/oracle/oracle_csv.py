# CSV 읽기. docs/spec/metrics.md 4.2(CSV 읽기)·4.6(정규화)을 보고 짰다(src/는 읽지 않았다).
# 표준 csv 모듈을 쓰지 않는다. 행이 시작한 물리 줄 번호(따옴표 안 줄바꿈 포함)를 직접 세려는 것이다.
# 오류 입력(encoding, empty, csv-syntax, field-count)은 OracleError로 멈춘다.
from __future__ import annotations

from dataclasses import dataclass

from oracle_common import OracleError, decode_utf8, net_trim


@dataclass(frozen=True)
class CsvRecord:
    line: int  # 행이 시작한 물리 줄, 1부터
    cells: list  # 정규화 뒤 칸


def normalize(cell: str) -> str:
    """4.6: 칸 안 CRLF·CR → LF, 앞뒤 공백(char.IsWhiteSpace) 떼기. 그 밖은 바꾸지 않는다."""
    return net_trim(cell.replace('\r\n', '\n').replace('\r', '\n'))


def _records(text: str, path: str) -> list:
    """RFC 4180 따옴표, 구분자 ','만, 행 끝 LF·CRLF·CR(4.2 표)."""
    out = []
    i, n, line = 0, len(text), 1
    while i < n:
        row_line = line
        cells = []
        while True:
            if i < n and text[i] == '"':
                open_line = line
                i += 1
                buf = []
                while True:
                    if i >= n:
                        raise OracleError(f'{path}:{open_line}: 따옴표가 닫히지 않았다 (csv-syntax 오류 입력, 범위 밖)')
                    ch = text[i]
                    if ch == '"':
                        if i + 1 < n and text[i + 1] == '"':
                            buf.append('"')
                            i += 2
                            continue
                        i += 1
                        break
                    if ch == '\r':
                        if i + 1 < n and text[i + 1] == '\n':
                            buf.append('\r\n')
                            i += 2
                        else:
                            buf.append('\r')
                            i += 1
                        line += 1
                        continue
                    if ch == '\n':
                        line += 1
                    buf.append(ch)
                    i += 1
                cell = ''.join(buf)
                if i < n and text[i] not in ',\r\n':
                    raise OracleError(f'{path}:{line}: 닫는 따옴표 뒤에 다른 글자가 있다 (csv-syntax 오류 입력, 범위 밖)')
            else:
                # 따옴표로 시작하지 않는 칸: 안의 '"'는 글자 그대로(4.2).
                j = i
                while j < n and text[j] not in ',\r\n':
                    j += 1
                cell = text[i:j]
                i = j
            cells.append(cell)
            if i < n and text[i] == ',':
                i += 1
                continue
            break
        if i < n:
            if text[i] == '\r' and i + 1 < n and text[i + 1] == '\n':
                i += 2
            else:
                i += 1
            line += 1
        out.append(CsvRecord(row_line, cells))
    return out


def read_table(data: bytes, path: str):
    """CSV 바이트 → (머리 행 줄, 머리 행 칸, 데이터 행 목록). 칸은 모두 정규화 뒤다."""
    text = decode_utf8(data, path)
    rows = [CsvRecord(r.line, [normalize(c) for c in r.cells]) for r in _records(text, path)]
    # 4.2 빈 행: 모든 칸이 정규화 뒤 빈 행은 건너뛴다. 줄 번호는 센다.
    rows = [r for r in rows if any(c != '' for c in r.cells)]
    if not rows:
        raise OracleError(f'{path}: 머리 행이 없다 (empty 오류 입력, 범위 밖)')
    header, data_rows = rows[0], rows[1:]
    for r in data_rows:
        if len(r.cells) != len(header.cells):
            raise OracleError(f'{path}:{r.line}: 칸 수가 머리 행과 다르다 (field-count 오류 입력, 범위 밖)')
    return header.line, header.cells, data_rows
