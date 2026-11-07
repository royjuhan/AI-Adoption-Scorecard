#!/usr/bin/env python3
"""mutation 잡: PR에서 바뀐 src/**/*.cs만 Stryker.NET으로 돌려 head·base 점수를 낸다(ADR 0005 원문 2, T-010 수용 기준 2).

- 바뀐 파일: base..head 트리 차이(git diff -M)에서 추가·수정·이름 변경된 src/ 아래 .cs. 삭제된 파일은 뺀다.
- head·base 커밋을 각각 git worktree로 꺼내 같은 파일 목록으로 돌린다. 이름이 바뀐 파일은 base 쪽 옛 경로로,
  base에 없는 새 파일은 base 점수 없음(null)이다.
- 파일이 속한 프로젝트(가장 가까운 상위 폴더의 .csproj)마다 Stryker를 한 번 돌린다. --mutate는 파일 절대 경로.
  테스트 프로젝트는 tests/Scorecard.Tests.
- Stryker 버전은 .config/dotnet-tools.json(dotnet tool manifest)에 고정한다. base 커밋의 manifest를 양쪽 worktree에
  써서 같은 버전으로 잰다(PR이 버전을 바꿔도 base 버전으로 잰다). base에 없으면 head 커밋의 것을 쓴다(부트스트랩).
- Stryker의 처음 테스트 실행에서 실패한 테스트가 있으면("N tests are failing") 그 실행의 점수는 증거가 아니다.
  실패한 테스트는 돌연변이를 잡지 않으므로 점수가 틀린다. 오류로 적는다(status: error).
- worktree의 테스트 프로젝트 폴더에 stryker-config.json·.yaml·.yml이 있으면 지우고 돌린다. PR이 설정 파일로
  돌연변이를 빼서(ignore-methods 등) 점수를 올리지 못하게 한다. 명령 인자만 쓴다.
- 시간: 한 번 실행은 RUN_TIMEOUT_SECONDS(50분) 안, 전체는 --budget-minutes(기본 100분) 안. 잡 제한(120분)보다 짧다.
  시간이 넘으면 status: timeout으로 mutation.json을 쓴다.
- 점수 = (Killed + Timeout) / (Killed + Timeout + Survived + NoCoverage). CompileError·Ignored 등은 빼고 센다.
  유효 돌연변이가 0이면 점수는 null.

출력: mutation.json(--out), job summary(--summary, 기본 $GITHUB_STEP_SUMMARY).
종료 코드: 0 측정함 또는 해당 없음, 1 Stryker 실행 오류·시간 초과, 2 실행할 수 없음(base·head가 없음 등).
"""

from __future__ import annotations

import argparse
import json
import os
import re
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
RUN_TIMEOUT_SECONDS = 50 * 60  # 한 번 실행. head·base 두 번이 전체 시간 안에 들어간다
DEFAULT_BUDGET_MINUTES = 100  # mutation.py 전체. 잡 제한 120분에서 준비·올리기 시간을 뺀 값
MIN_RUN_SECONDS = 120  # 남은 시간이 이보다 적으면 실행을 시작하지 않는다
STRYKER_CONFIG_NAMES = ("stryker-config.json", "stryker-config.yaml", "stryker-config.yml")
TIMEOUT_EXIT = 124
# Stryker.NET 로그: "[..] 1 tests are failing. Stryker will continue but outcome will be impacted."
INITIAL_FAILING = re.compile(r"(\d+) tests? (?:are|is) failing", re.IGNORECASE)

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


def run_stryker(root: str, project: str, files: list[str], out_dir: str, log_path: str, timeout: float = RUN_TIMEOUT_SECONDS) -> tuple[int, float]:
    """root(worktree)에서 Stryker를 한 번 돌린다. (종료 코드, 초). 시간 초과면 종료 코드 124."""
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
            proc = subprocess.run(cmd, cwd=cwd, stdout=log, stderr=subprocess.STDOUT, timeout=timeout)
            code = proc.returncode
        except subprocess.TimeoutExpired:
            log.write(f"\n시간 초과({int(timeout)}초)\n")
            code = TIMEOUT_EXIT
    return code, round(time.monotonic() - start, 1)


def tool_manifest(repo: str, rng: cigit.Range) -> tuple[bytes | None, str]:
    """쓸 dotnet tool manifest와 출처. base 커밋에 있으면 base, 없으면 head 커밋(부트스트랩)."""
    data = cigit.read_file(repo, rng.base, TOOL_MANIFEST)
    if data is not None:
        return data, "base"
    data = cigit.read_file(repo, rng.head, TOOL_MANIFEST)
    if data is not None:
        return data, "head"
    return None, "없음"


def initial_failures(log_path: str) -> int:
    """Stryker 로그에서 처음 테스트 실행의 실패 수. 없으면 0."""
    try:
        with open(log_path, encoding="utf-8", errors="replace") as f:
            text = f.read()
    except OSError:
        return 0
    return max((int(m.group(1)) for m in INITIAL_FAILING.finditer(text)), default=0)


def tool_version(manifest: bytes | None) -> str | None:
    try:
        return json.loads((manifest or b"").decode("utf-8"))["tools"][TOOL_PACKAGE]["version"]
    except (KeyError, ValueError, TypeError, UnicodeDecodeError):
        return None


def remove_stryker_configs(root: str) -> list[str]:
    """worktree 테스트 프로젝트 폴더의 Stryker 설정 파일을 지운다. 지운 파일 이름."""
    removed = []
    for name in STRYKER_CONFIG_NAMES:
        path = os.path.join(root, TEST_PROJECT_DIR, name)
        if os.path.isfile(path):
            os.remove(path)
            removed.append(name)
    return removed


def tail(path: str, count: int = 25) -> str:
    try:
        with open(path, encoding="utf-8", errors="replace") as f:
            return "".join(f.readlines()[-count:])
    except OSError:
        return ""


def measure(repo: str, rng: cigit.Range, files: list[dict], work: str, manifest: bytes | None,
            deadline: float | None = None) -> dict:
    """양쪽 worktree를 만들고 프로젝트마다 Stryker를 돌려 files에 head·base 셈을 채운다.

    deadline(time.monotonic 기준)을 넘기면 남은 실행을 하지 않고 timed_out을 참으로 돌려준다.
    """
    errors: list[str] = []
    runs: list[dict] = []
    timed_out = False
    if manifest is None:
        return {"errors": [f"{TOOL_MANIFEST}가 없다"], "runs": runs, "timed_out": False}
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
            with open(os.path.join(root, TOOL_MANIFEST), "wb") as f:
                f.write(manifest)
            restore = subprocess.run(["dotnet", "tool", "restore"], cwd=root, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
            if restore.returncode != 0:
                errors.append(f"{side}: dotnet tool restore 실패: {restore.stdout.decode('utf-8', 'replace')[-500:]}")
                continue
            if not os.path.isdir(os.path.join(root, TEST_PROJECT_DIR)):
                errors.append(f"{side}: 테스트 프로젝트가 없다: {TEST_PROJECT_DIR}")
                continue
            for name in remove_stryker_configs(root):
                print(f"--- {side}: {TEST_PROJECT_DIR}/{name}를 지우고 돌린다(명령 인자만 쓴다) ---")
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
                limit = float(RUN_TIMEOUT_SECONDS)
                if deadline is not None:
                    limit = min(limit, deadline - time.monotonic())
                if limit < MIN_RUN_SECONDS:
                    timed_out = True
                    errors.append(f"{side}: {project} 시간 초과. 전체 시간 안에 시작하지 못했다(남은 {max(0, int(limit))}초)")
                    for f in group:
                        f[side] = None
                    continue
                code, seconds = run_stryker(root, project, paths, out_dir, log_path, limit)
                report = os.path.join(out_dir, "reports", "mutation-report.json")
                failing = initial_failures(log_path)
                runs.append({"side": side, "project": project, "files": paths, "exit_code": code, "seconds": seconds,
                             "initial_failures": failing})
                print(f"--- Stryker {side} {project}: 종료 {code}, {seconds}초, 처음 실행 실패 {failing}개 ---")
                print(tail(log_path))
                if failing:
                    errors.append(f"{side}: {project} 처음 테스트 실행에서 {failing}개가 실패했다. 실패한 테스트가 있으면 점수는 증거가 아니다. 로그 {log_path}")
                    for f in group:
                        f[side] = None
                    continue
                if code == TIMEOUT_EXIT:
                    timed_out = True
                    errors.append(f"{side}: {project} 시간 초과({int(limit)}초 안에 끝나지 않았다). 로그 {log_path}")
                    for f in group:
                        f[side] = None
                    continue
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
    return {"errors": errors, "runs": runs, "timed_out": timed_out}


def build_result(rng: cigit.Range, files: list[dict], errors: list[str], runs: list[dict], version: str | None,
                 timed_out: bool = False, manifest_source: str | None = None) -> dict:
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
    elif timed_out:
        status = "timeout"
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
        "tool_manifest": manifest_source,
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
    if data["status"] == "timeout":
        out.append("**시간 초과**: 측정이 시간 안에 끝나지 않았다. 점수는 증거가 되지 않는다.")
        out.append("")
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
        out.append(f"- Stryker {r['side']} `{r['project']}`: 종료 {r['exit_code']}, {r['seconds']}초, 처음 실행 실패 {r.get('initial_failures', 0)}개")
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
    ap.add_argument("--budget-minutes", type=float, default=DEFAULT_BUDGET_MINUTES, help="전체 시간(분). 넘으면 status: timeout")
    args = ap.parse_args(argv)
    deadline = time.monotonic() + args.budget_minutes * 60
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
    timed_out = False
    manifest, manifest_source = tool_manifest(repo, rng)
    if files:
        print(f"mutation: tool manifest는 {manifest_source}의 {TOOL_MANIFEST} (Stryker {tool_version(manifest)})")
        work = args.work or tempfile.mkdtemp(prefix="mutation-", dir=os.environ.get("RUNNER_TEMP"))
        try:
            measured = measure(repo, rng, files, work, manifest, deadline)
            errors, runs, timed_out = measured["errors"], measured["runs"], measured["timed_out"]
        except Exception as e:  # noqa: BLE001 — 어떤 실패든 mutation.json(status: error)을 남긴다
            errors = [f"예외: {type(e).__name__}: {e}"]
    data = build_result(rng, files, errors, runs, tool_version(manifest), timed_out, manifest_source)
    with open(args.out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")
    summary = render_summary(data)
    print(summary)
    if args.summary:
        with open(args.summary, "a", encoding="utf-8") as f:
            f.write(summary)
    return 1 if data["status"] in ("error", "timeout") else 0


if __name__ == "__main__":
    sys.exit(main())
