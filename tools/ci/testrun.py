#!/usr/bin/env python3
"""test-run: base와 head에서 같은 명령으로 dotnet test를 돌리고 TRX를 읽는다(regression-guard의 실행 기반 검사).

소스를 세는 검사(csharp_scan)는 컴파일러가 아니다. 그래서 실제로 돈 테스트를 base와 비교한다.

- base·head 커밋을 각각 git worktree로 꺼내 아래 명령을 돌린다. 명령은 이 파일에 고정한다.
    dotnet test AIAdoptionScorecard.sln -c Release --settings <빈 runsettings> --filter <모두 통과하는 필터>
      --logger trx --results-directory <폴더>
  --settings·--filter를 명령 인자로 주므로 저장소의 runsettings(RunSettingsFilePath)·VSTestTestCaseFilter 속성은
  쓰이지 않는다(명령 인자가 MSBuild 전역 속성이라 프로젝트 속성보다 앞선다).
- 결과 폴더의 *.trx를 모두 읽어 테스트 이름(testName)별 결과를 센다. Passed = 통과, Failed·Error·Timeout·Aborted =
  실패, 그 밖(NotExecuted 등, xUnit Skip) = 건너뜀.
- regression-guard가 testrun.json을 읽어 base 통과 이름이 head 통과 이름에 모두 있는지, 건너뜀 수가 늘지 않았는지,
  전체 수가 줄지 않았는지 본다.

출력: testrun.json(--out). 종료 코드: 0 양쪽 모두 TRX를 읽음, 1 한쪽이라도 못 읽음(빌드 실패·시간 초과 등),
2 실행할 수 없음(base·head가 없음 등).
"""

from __future__ import annotations

import argparse
import glob
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time
import xml.etree.ElementTree as ET
from collections import Counter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import cigit  # noqa: E402

SOLUTION = "AIAdoptionScorecard.sln"
# 어떤 테스트도 거르지 않는 필터. 저장소가 VSTestTestCaseFilter를 넣어도 이 값이 앞선다.
NEUTRAL_FILTER = "FullyQualifiedName!=RegressionGuard.NoSuchTest"
NEUTRAL_RUNSETTINGS = '<?xml version="1.0" encoding="utf-8"?>\n<RunSettings>\n  <RunConfiguration />\n</RunSettings>\n'
SIDE_TIMEOUT_SECONDS = 18 * 60
TRX_NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
PASSED = {"Passed"}
FAILED = {"Failed", "Error", "Timeout", "Aborted"}


def command(settings: str, results_dir: str) -> list[str]:
    """고정 명령. 저장소 파일에서 아무것도 읽지 않는다."""
    return [
        "dotnet", "test", SOLUTION, "-c", "Release",
        "--settings", settings,
        "--filter", NEUTRAL_FILTER,
        "--logger", "trx",
        "--results-directory", results_dir,
    ]


def _results(node: ET.Element) -> list[ET.Element]:
    """UnitTestResult 하나. InnerResults가 있으면(데이터 행 묶음) 안쪽 결과를 쓴다."""
    inner = node.find(f"{TRX_NS}InnerResults")
    if inner is not None:
        out: list[ET.Element] = []
        for child in inner.findall(f"{TRX_NS}UnitTestResult"):
            out += _results(child)
        if out:
            return out
    return [node]


def parse_trx(paths: list[str]) -> dict:
    """TRX 파일들을 읽어 셈과 이름 목록을 돌려준다. 이름 목록은 정렬하고 중복을 남긴다(같은 이름 두 번 = 두 개)."""
    passed: list[str] = []
    failed: list[str] = []
    skipped: list[str] = []
    outcomes: Counter[str] = Counter()
    for path in sorted(paths):
        root = ET.parse(path).getroot()
        results = root.find(f"{TRX_NS}Results")
        if results is None:
            continue
        for node in results.findall(f"{TRX_NS}UnitTestResult"):
            for r in _results(node):
                name = r.get("testName") or ""
                outcome = r.get("outcome") or ""
                outcomes[outcome] += 1
                if outcome in PASSED:
                    passed.append(name)
                elif outcome in FAILED:
                    failed.append(name)
                else:
                    skipped.append(name)
    return {
        "total": len(passed) + len(failed) + len(skipped),
        "passed": len(passed),
        "failed": len(failed),
        "skipped": len(skipped),
        "outcomes": dict(sorted(outcomes.items())),
        "passed_names": sorted(passed),
        "failed_names": sorted(failed),
        "skipped_names": sorted(skipped),
    }


def run_side(repo: str, rev: str, root: str, work: str, side: str, timeout: float) -> dict:
    """rev를 root에 worktree로 꺼내 고정 명령을 돌리고 TRX를 읽는다."""
    if os.path.exists(root):
        cigit.git(repo, "worktree", "remove", "--force", root, check=False)
        shutil.rmtree(root, ignore_errors=True)
    results_dir = os.path.join(work, "results", side)
    shutil.rmtree(results_dir, ignore_errors=True)
    os.makedirs(results_dir, exist_ok=True)
    settings = os.path.join(work, "neutral.runsettings")
    with open(settings, "w", encoding="utf-8", newline="\n") as f:
        f.write(NEUTRAL_RUNSETTINGS)
    log_path = os.path.join(work, "logs", f"{side}.log")
    os.makedirs(os.path.dirname(log_path), exist_ok=True)
    cmd = command(settings, results_dir)
    entry: dict = {"status": "error", "rev": rev, "exit_code": None, "seconds": None, "trx_files": 0, "error": None, "log": log_path}
    cigit.git(repo, "worktree", "add", "--detach", root, rev)
    try:
        start = time.monotonic()
        with open(log_path, "w", encoding="utf-8") as log:
            log.write("$ " + " ".join(cmd) + "\n")
            log.flush()
            try:
                proc = subprocess.run(cmd, cwd=root, stdout=log, stderr=subprocess.STDOUT, timeout=timeout)
                entry["exit_code"] = proc.returncode
            except subprocess.TimeoutExpired:
                log.write(f"\n시간 초과({int(timeout)}초)\n")
                entry["status"] = "timeout"
                entry["error"] = f"시간 초과({int(timeout)}초)"
        entry["seconds"] = round(time.monotonic() - start, 1)
        if entry["status"] == "timeout":
            return entry
        trx = sorted(glob.glob(os.path.join(results_dir, "**", "*.trx"), recursive=True))
        entry["trx_files"] = len(trx)
        if not trx:
            entry["error"] = f"TRX가 없다(종료 {entry['exit_code']}). 빌드 실패일 수 있다. 로그 {log_path}"
            return entry
        try:
            entry.update(parse_trx(trx))
        except ET.ParseError as e:
            entry["error"] = f"TRX를 읽을 수 없다: {e}"
            return entry
        entry["status"] = "ok"
        return entry
    finally:
        cigit.git(repo, "worktree", "remove", "--force", root, check=False)


def tail(path: str, count: int = 15) -> str:
    try:
        with open(path, encoding="utf-8", errors="replace") as f:
            return "".join(f.readlines()[-count:])
    except OSError:
        return ""


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description="base·head에서 같은 명령으로 dotnet test를 돌려 TRX를 비교할 자료를 만든다")
    ap.add_argument("--repo", default=".")
    ap.add_argument("--base", default=os.environ.get("CI_BASE", ""), help="base 커밋. 비거나 없으면 head의 첫 부모")
    ap.add_argument("--head", default="HEAD")
    ap.add_argument("--out", default="testrun.json")
    ap.add_argument("--work", help="worktree·TRX·로그 폴더(기본: 임시 폴더)")
    ap.add_argument("--side-timeout-minutes", type=float, default=SIDE_TIMEOUT_SECONDS / 60)
    args = ap.parse_args(argv)
    repo = os.path.abspath(args.repo)
    try:
        rng = cigit.resolve_range(repo, args.base, args.head)
    except cigit.GitError as e:
        print(f"testrun: 실행할 수 없다: {e}", file=sys.stderr)
        return 2
    work = os.path.abspath(args.work or tempfile.mkdtemp(prefix="testrun-", dir=os.environ.get("RUNNER_TEMP")))
    os.makedirs(work, exist_ok=True)
    print(f"testrun: {rng.note}, head {rng.head[:12]}")
    sides: dict[str, dict] = {}
    errors: list[str] = []
    for side, rev in (("base", rng.base), ("head", rng.head)):
        try:
            entry = run_side(repo, rev, os.path.join(work, side), work, side, args.side_timeout_minutes * 60)
        except Exception as e:  # noqa: BLE001 — 어떤 실패든 testrun.json(status: error)을 남긴다
            entry = {"status": "error", "rev": rev, "error": f"예외: {type(e).__name__}: {e}"}
        sides[side] = entry
        print(f"--- {side} {rev[:12]}: {entry['status']}, 전체 {entry.get('total')}, 통과 {entry.get('passed')}, "
              f"실패 {entry.get('failed')}, 건너뜀 {entry.get('skipped')}, {entry.get('seconds')}초 ---")
        if entry["status"] != "ok":
            errors.append(f"{side}: {entry.get('error')}")
            print(tail(entry.get("log", "")))
    data = {
        "schema_version": 1,
        "status": "measured" if not errors else "error",
        "base": rng.base,
        "head": rng.head,
        "range_note": rng.note,
        "command": command("<빈 runsettings>", "<결과 폴더>"),
        "sides": sides,
        "errors": errors,
    }
    with open(args.out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")
    return 0 if not errors else 1


if __name__ == "__main__":
    sys.exit(main())
