#!/usr/bin/env python3
"""mutation 잡: PR에서 바뀐 src/**/*.cs만 Stryker.NET으로 돌려 head·base 점수를 낸다(ADR 0005 원문 2, T-010 수용 기준 2).

- 바뀐 파일: base..head 트리 차이(git diff -M)에서 추가·수정·이름 변경된 src/ 아래 .cs. 삭제된 파일은 뺀다.
- head·base 커밋을 각각 git worktree로 꺼내 같은 파일 목록으로 돌린다. 이름이 바뀐 파일은 base 쪽 옛 경로로,
  base에 없는 새 파일은 base 점수 없음(null)이다.
- 파일이 속한 프로젝트(가장 가까운 상위 폴더의 .csproj)마다 Stryker를 한 번 돌린다. --mutate는 파일 절대 경로.
  테스트 프로젝트는 tests/Scorecard.Tests.
- Stryker 버전은 .config/dotnet-tools.json(dotnet tool manifest)에 고정한다. base worktree에도 같은 manifest를
  복사해 양쪽을 같은 버전으로 잰다.
- 점수 = (Killed + Timeout) / (Killed + Timeout + Survived + NoCoverage). CompileError·Ignored 등은 빼고 센다.
  유효 돌연변이가 0이면 점수는 null.

출력: mutation.json(--out), job summary(--summary, 기본 $GITHUB_STEP_SUMMARY).
종료 코드: 0 측정함 또는 해당 없음, 1 Stryker 실행 오류, 2 실행할 수 없음(base·head가 없음 등).
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time
from fractions import Fraction

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import cigit  # noqa: E402

SRC_PREFIX = "src/"
TEST_PROJECT_DIR = "tests/Scorecard.Tests"
TOOL_MANIFEST = ".config/dotnet-tools.json"
TOOL_PACKAGE = "dotnet-stryker"
RUN_TIMEOUT_SECONDS = 60 * 60

# mutation-testing-elements 상태 → mutation.json 키
STATUS_KEYS = {
    "Killed": "killed",
    "Timeout": "timeout",
    "Survived": "survived",
    "NoCoverage": "no_coverage",
    "CompileError": "compile_error",
    "RuntimeError": "runtime_error",
    "Ignored": "ignored",
    "Pending": "pending",
}


def changed_src_files(repo: str, base: str, head: str) -> list[dict]:
    """바뀐 src .cs 파일. [{path, base_path}] (path는 head 쪽, base_path는 base 쪽 또는 None)."""
    files = []
    for c in cigit.diff_changes(repo, base, head, [SRC_PREFIX], renames=True):
        if not c.path.endswith(".cs") or "/obj/" in c.path or "/bin/" in c.path:
            continue
        if c.status == "D":
            continue
        if c.status in ("R", "C"):
            base_path = c.old_path
        elif c.status == "A":
            base_path = None
        else:
            base_path = c.path
        files.append({"path": c.path, "base_path": base_path})
    return sorted(files, key=lambda f: f["path"])


def find_project(repo: str, rev: str, path: str) -> str | None:
    """rev 트리에서 path의 가장 가까운 상위 폴더에 있는 .csproj(하나일 때)."""
    parts = path.split("/")[:-1]
    while parts:
        folder = "/".join(parts)
        out = cigit.git(repo, "ls-tree", "-z", "--name-only", rev, "--", folder + "/")
        projects = sorted(p for p in out.split("\0") if p.endswith(".csproj"))
        if len(projects) == 1:
            return projects[0]
        if len(projects) > 1:
            return None
        parts.pop()
    return None


def score_stats(statuses: list[str]) -> dict:
    stats = {key: 0 for key in STATUS_KEYS.values()}
    stats["other"] = 0
    for s in statuses:
        key = STATUS_KEYS.get(s)
        if key is None:
            stats["other"] += 1
        else:
            stats[key] += 1
    stats["total"] = len(statuses)
    stats["score"] = score_value(stats)
    return stats


def score_fraction(stats: dict | None) -> Fraction | None:
    if not stats:
        return None
    detected = stats["killed"] + stats["timeout"]
    valid = detected + stats["survived"] + stats["no_coverage"]
    return None if valid == 0 else Fraction(detected, valid)


def score_value(stats: dict | None) -> float | None:
    fr = score_fraction(stats)
    return None if fr is None else round(float(fr) * 100, 2)


def read_report(report_path: str, root: str, wanted: list[str]) -> dict[str, dict | None]:
    """Stryker JSON 보고서에서 wanted(저장소 상대 경로) 파일의 상태 셈. 보고서에 없으면 None."""
    with open(report_path, encoding="utf-8") as f:
        report = json.load(f)
    real_root = os.path.realpath(root)
    by_rel: dict[str, list[str]] = {}
    for key, entry in (report.get("files") or {}).items():
        absolute = key if os.path.isabs(key) else os.path.join(report.get("projectRoot") or real_root, key)
        rel = os.path.relpath(os.path.realpath(absolute), real_root).replace(os.sep, "/")
        by_rel[rel] = [m.get("status", "") for m in entry.get("mutants") or []]
    return {path: (score_stats(by_rel[path]) if path in by_rel else None) for path in wanted}


def run_stryker(root: str, project: str, files: list[str], out_dir: str, log_path: str) -> tuple[int, float]:
    """root(worktree)에서 Stryker를 한 번 돌린다. (종료 코드, 초)"""
    cmd = ["dotnet", "tool", "run", TOOL_PACKAGE, "--project", os.path.basename(project)]
    for path in files:
        cmd += ["--mutate", os.path.join(os.path.realpath(root), path)]
    cmd += ["--reporter", "Json", "--reporter", "ClearText", "--output", out_dir, "--skip-version-check"]
    cwd = os.path.join(root, TEST_PROJECT_DIR)
    start = time.monotonic()
    with open(log_path, "w", encoding="utf-8") as log:
        log.write("$ " + " ".join(cmd) + "\n")
        log.flush()
        try:
            proc = subprocess.run(cmd, cwd=cwd, stdout=log, stderr=subprocess.STDOUT, timeout=RUN_TIMEOUT_SECONDS)
            code = proc.returncode
        except subprocess.TimeoutExpired:
            log.write(f"\n시간 초과({RUN_TIMEOUT_SECONDS}초)\n")
            code = 124
    return code, round(time.monotonic() - start, 1)


def tool_version(repo: str) -> str | None:
    try:
        with open(os.path.join(repo, TOOL_MANIFEST), encoding="utf-8") as f:
            return json.load(f)["tools"][TOOL_PACKAGE]["version"]
    except (OSError, KeyError, ValueError):
        return None


def tail(path: str, count: int = 25) -> str:
    try:
        with open(path, encoding="utf-8", errors="replace") as f:
            return "".join(f.readlines()[-count:])
    except OSError:
        return ""


def measure(repo: str, rng: cigit.Range, files: list[dict], work: str) -> dict:
    """양쪽 worktree를 만들고 프로젝트마다 Stryker를 돌려 files에 head·base 셈을 채운다."""
    errors: list[str] = []
    runs: list[dict] = []
    manifest = os.path.join(repo, TOOL_MANIFEST)
    if not os.path.isfile(manifest):
        return {"errors": [f"{TOOL_MANIFEST}가 없다"], "runs": runs}
    os.makedirs(os.path.join(work, "logs"), exist_ok=True)
    for side, rev, key in (("head", rng.head, "path"), ("base", rng.base, "base_path")):
        side_files = [f for f in files if f[key] is not None]
        if not side_files:
            continue
        root = os.path.join(work, side)
        if os.path.exists(root):
            cigit.git(repo, "worktree", "remove", "--force", root, check=False)
            shutil.rmtree(root, ignore_errors=True)
        cigit.git(repo, "worktree", "add", "--detach", root, rev)
        try:
            os.makedirs(os.path.join(root, ".config"), exist_ok=True)
            shutil.copyfile(manifest, os.path.join(root, TOOL_MANIFEST))
            restore = subprocess.run(["dotnet", "tool", "restore"], cwd=root, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
            if restore.returncode != 0:
                errors.append(f"{side}: dotnet tool restore 실패: {restore.stdout.decode('utf-8', 'replace')[-500:]}")
                continue
            if not os.path.isdir(os.path.join(root, TEST_PROJECT_DIR)):
                errors.append(f"{side}: 테스트 프로젝트가 없다: {TEST_PROJECT_DIR}")
                continue
            groups: dict[str, list[dict]] = {}
            for f in side_files:
                project = find_project(repo, rev, f[key])
                if project is None:
                    errors.append(f"{side}: {f[key]}의 .csproj를 찾지 못했다")
                    f[side] = None
                    continue
                f.setdefault("project", project)
                groups.setdefault(project, []).append(f)
            for project, group in sorted(groups.items()):
                paths = [f[key] for f in group]
                label = f"{side}-{project.replace('/', '_')}"
                out_dir = os.path.join(work, "out", label)
                log_path = os.path.join(work, "logs", label + ".log")
                code, seconds = run_stryker(root, project, paths, out_dir, log_path)
                report = os.path.join(out_dir, "reports", "mutation-report.json")
                runs.append({"side": side, "project": project, "files": paths, "exit_code": code, "seconds": seconds})
                print(f"--- Stryker {side} {project}: 종료 {code}, {seconds}초 ---")
                print(tail(log_path))
                if code != 0 or not os.path.isfile(report):
                    errors.append(f"{side}: {project} Stryker 실패(종료 {code}). 로그 {log_path}")
                    for f in group:
                        f[side] = None
                    continue
                stats = read_report(report, root, paths)
                for f in group:
                    f[side] = stats[f[key]]
                    if stats[f[key]] is None:
                        errors.append(f"{side}: {f[key]}가 Stryker 보고서에 없다")
        finally:
            cigit.git(repo, "worktree", "remove", "--force", root, check=False)
    return {"errors": errors, "runs": runs}


def build_result(rng: cigit.Range, files: list[dict], errors: list[str], runs: list[dict], version: str | None) -> dict:
    entries = []
    head_total = [0, 0]
    base_total = [0, 0]
    for f in files:
        head = f.get("head")
        base = f.get("base") if f["base_path"] is not None else None
        entries.append({
            "path": f["path"],
            "base_path": f["base_path"],
            "project": f.get("project"),
            "head_score": score_value(head),
            "base_score": score_value(base),
            "head": head,
            "base": base,
        })
        if head and base and score_fraction(head) is not None and score_fraction(base) is not None:
            for total, stats in ((head_total, head), (base_total, base)):
                detected = stats["killed"] + stats["timeout"]
                total[0] += detected
                total[1] += detected + stats["survived"] + stats["no_coverage"]
    if not files:
        status = "not_applicable"
    elif errors:
        status = "error"
    else:
        status = "measured"
    return {
        "schema_version": 1,
        "status": status,
        "base": rng.base,
        "head": rng.head,
        "range_note": rng.note,
        "stryker_version": version,
        "files": entries,
        # 합계 점수: base에도 있는 파일(점수를 비교할 수 있는 파일)만 더한다. 새 파일은 files[]의 head_score에만 있다.
        "head_score": round(head_total[0] / head_total[1] * 100, 2) if head_total[1] else None,
        "base_score": round(base_total[0] / base_total[1] * 100, 2) if base_total[1] else None,
        "runs": runs,
        "errors": errors,
    }


def render_summary(data: dict) -> str:
    def fmt(v: float | None) -> str:
        return "—" if v is None else f"{v:.2f}%"

    out = ["## mutation (Stryker.NET)", ""]
    out.append(f"범위: `{data['base'][:12]}..{data['head'][:12]}` ({data['range_note']}). Stryker {data.get('stryker_version')}")
    out.append("")
    if data["status"] == "not_applicable":
        out.append("해당 없음: 바뀐 `src/**/*.cs` 파일이 없다.")
        return "\n".join(out) + "\n"
    out += ["| 파일 | base | head | 변화 | head 셈(killed/timeout/survived/no_coverage) |", "| --- | --- | --- | --- | --- |"]
    for f in data["files"]:
        h, b = f["head_score"], f["base_score"]
        if f["base_path"] is None:
            change = "새 파일"
        elif h is None or b is None:
            change = "비교 불가"
        else:
            change = f"{h - b:+.2f}"
        stats = f["head"] or {}
        counts = "/".join(str(stats.get(k, "—")) for k in ("killed", "timeout", "survived", "no_coverage"))
        name = f["path"] if f["base_path"] in (None, f["path"]) else f"{f['path']} (base: {f['base_path']})"
        out.append(f"| `{name}` | {fmt(b)} | {fmt(h)} | {change} | {counts} |")
    out.append("")
    out.append(f"합계(비교 가능한 파일): base {fmt(data['base_score'])}, head {fmt(data['head_score'])}")
    for r in data.get("runs") or []:
        out.append(f"- Stryker {r['side']} `{r['project']}`: 종료 {r['exit_code']}, {r['seconds']}초")
    for e in data.get("errors") or []:
        out.append(f"- 오류: {e}")
    return "\n".join(out) + "\n"


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description="바뀐 src 파일만 Stryker.NET으로 돌린다(ADR 0005)")
    ap.add_argument("--repo", default=".")
    ap.add_argument("--base", default=os.environ.get("CI_BASE", ""), help="base 커밋. 비거나 없으면 head의 첫 부모")
    ap.add_argument("--head", default="HEAD")
    ap.add_argument("--out", default="mutation.json")
    ap.add_argument("--work", help="worktree·보고서·로그 폴더(기본: 임시 폴더)")
    ap.add_argument("--summary", default=os.environ.get("GITHUB_STEP_SUMMARY"))
    args = ap.parse_args(argv)
    repo = os.path.abspath(args.repo)
    try:
        rng = cigit.resolve_range(repo, args.base, args.head)
        files = changed_src_files(repo, rng.base, rng.head)
    except cigit.GitError as e:
        print(f"mutation: 실행할 수 없다: {e}", file=sys.stderr)
        return 2
    print(f"mutation: {rng.note}, head {rng.head[:12]}. 바뀐 src .cs {len(files)}개")
    for f in files:
        print(f"  {f['path']}" + ("" if f["base_path"] == f["path"] else f" (base: {f['base_path']})"))
    errors: list[str] = []
    runs: list[dict] = []
    if files:
        work = args.work or tempfile.mkdtemp(prefix="mutation-", dir=os.environ.get("RUNNER_TEMP"))
        measured = measure(repo, rng, files, work)
        errors, runs = measured["errors"], measured["runs"]
    data = build_result(rng, files, errors, runs, tool_version(repo))
    with open(args.out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")
    summary = render_summary(data)
    print(summary)
    if args.summary:
        with open(args.summary, "a", encoding="utf-8") as f:
            f.write(summary)
    return 1 if data["status"] == "error" else 0


if __name__ == "__main__":
    sys.exit(main())
