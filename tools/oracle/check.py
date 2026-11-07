#!/usr/bin/env python3
# oracle 잡의 진입점(ADR 0005 원문 3, T-010 수용 기준 3). CI가 아래처럼 부른다. 이 계약을 바꾸지 않는다.
#
#   python3 tools/oracle/check.py [--repo <루트>] [--json <보고서 경로>]
#
# examples/ 아래 예제 폴더마다 expected/classification.json, expected/metrics.json을 독립 구현으로 다시 만들어
# 바이트로 비교한다. 줄마다 하나:
#   MATCH <상대 경로> <sha256>
#   DIFF <상대 경로> expected=<sha256> oracle=<sha256 또는 error>
#   MISSING <상대 경로> oracle=<sha256 또는 error>
# 모두 MATCH면 종료 코드 0, 아니면 1. 이유·요약은 표준 오류에 쓴다.
# --json이면 {"files": [{"expected_sha256", "oracle_sha256", "path", "status"}]}를 쓴다(키 정렬, LF).
# 표준 라이브러리만 쓴다. 네트워크·외부 프로세스 없음. 저장소 파일은 읽기만 한다.
from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from classify import classification_bytes  # noqa: E402
from compare import metrics_bytes  # noqa: E402
from oracle_common import OracleError, ordinal_key, sha256_hex, utf8_stdio  # noqa: E402

# 대상 파일 종류. examples에 expected가 아예 없는 종류(report-outline.json 등)는 대상이 아니다.
TARGETS = ('classification.json', 'metrics.json')


def _is_file(folder: Path, name: str) -> bool:
    """폴더 안에 이 이름(서수)의 일반 파일이 있나. 대소문자를 무시하는 파일 시스템에서도 서수로 본다."""
    try:
        with os.scandir(folder) as it:
            return any(e.name == name and e.is_file(follow_symlinks=False) for e in it)
    except OSError:
        return False


class _Disagree(Exception):
    """answers.yaml과 case.yaml이 다른 분류를 낸다. 보고에는 expected와 다른 쪽을 쓴다."""

    def __init__(self, message: str, first: bytes, second: bytes):
        super().__init__(message)
        self.first = first
        self.second = second


def build_classification(example: Path) -> bytes:
    """answers.yaml이 있으면 그것으로, 없으면 case.yaml의 steps로 분류한다.

    둘 다 있으면 둘 다 분류해 바이트가 같아야 한다(classification 7.1 5, synthetic-examples: 단계·답이 같다).
    """
    sources = [n for n in ('answers.yaml', 'case.yaml') if _is_file(example, n)]
    if not sources:
        raise OracleError('answers.yaml도 case.yaml도 없다')
    outputs = [(n, classification_bytes((example / n).read_bytes(), n)) for n in sources]
    first_name, first = outputs[0]
    for name, out in outputs[1:]:
        if out != first:
            raise _Disagree(f'{name}의 steps로 낸 분류가 {first_name}과 다르다', first, out)
    return first


def build_metrics(example: Path) -> bytes:
    return metrics_bytes(example)


BUILDERS = {'classification.json': build_classification, 'metrics.json': build_metrics}


def check(repo: Path):
    examples = repo / 'examples'
    if not examples.is_dir():
        raise SystemExit(f'oracle: examples 폴더가 없다: {examples}')
    with os.scandir(examples) as it:
        folders = sorted((e.name for e in it if e.is_dir(follow_symlinks=False)), key=ordinal_key)
    results = []
    for name in folders:
        example = examples / name
        for target in TARGETS:
            rel = f'examples/{name}/expected/{target}'
            expected_path = example / 'expected' / target
            expected = expected_path.read_bytes() if _is_file(example / 'expected', target) else None
            expected_sha = sha256_hex(expected) if expected is not None else None
            oracle = None
            note = None
            try:
                oracle = BUILDERS[target](example)
            except _Disagree as d:
                note = str(d)
                oracle = d.second if expected is not None and d.first == expected else d.first
            except OracleError as e:
                note = str(e)
            oracle_sha = sha256_hex(oracle) if oracle is not None else None
            if expected is None:
                status = 'missing'
            elif note is None and oracle == expected:
                status = 'match'
            else:
                status = 'diff'
            results.append({'path': rel, 'status': status, 'expected_sha256': expected_sha,
                            'oracle_sha256': oracle_sha, 'note': note})
    return results


def main(argv: list) -> int:
    utf8_stdio()
    default_repo = Path(__file__).resolve().parents[2]
    ap = argparse.ArgumentParser(description='oracle: examples/*/expected/를 독립 구현으로 다시 만들어 바이트로 비교한다.')
    ap.add_argument('--repo', default=str(default_repo), help='저장소 루트(기본: 이 파일의 두 단계 위)')
    ap.add_argument('--json', dest='json_path', help='보고서 JSON을 쓸 경로')
    args = ap.parse_args(argv)

    results = check(Path(args.repo))
    for r in results:
        oracle = r['oracle_sha256'] or 'error'
        if r['status'] == 'match':
            print(f"MATCH {r['path']} {r['expected_sha256']}")
        elif r['status'] == 'diff':
            print(f"DIFF {r['path']} expected={r['expected_sha256']} oracle={oracle}")
        else:
            print(f"MISSING {r['path']} oracle={oracle}")
        if r['note']:
            print(f"oracle: {r['path']}: {r['note']}", file=sys.stderr)
    sys.stdout.flush()

    if args.json_path:
        report = {'files': [{k: r[k] for k in ('path', 'status', 'expected_sha256', 'oracle_sha256')} for r in results]}
        data = (json.dumps(report, sort_keys=True, indent=2, ensure_ascii=False) + '\n').encode('utf-8')
        out = Path(args.json_path)
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_bytes(data)

    counts = {s: sum(1 for r in results if r['status'] == s) for s in ('match', 'diff', 'missing')}
    print(f"oracle: 파일 {len(results)}개, match {counts['match']}, diff {counts['diff']}, missing {counts['missing']}",
          file=sys.stderr)
    if not results:
        print('oracle: 비교한 파일이 없다(빈 통과 금지)', file=sys.stderr)
        return 1
    return 0 if counts['match'] == len(results) else 1


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
