#!/usr/bin/env python3
"""lead_check: 리드가 머지 전에 로컬에서 돌리는 독립 검사(T-010 재리뷰 B5).

PR의 ci.yml은 PR이 고칠 수 있다(예: guard 줄 끝에 `|| true`를 붙여 늘 녹색). 그래서 리드는 머지 전에 PR의 ci.yml을
읽지 않고 같은 검사를 이 스크립트로 돈다. 실패하면 0이 아닌 값으로 끝난다.

쓰는 법(저장소 루트에서). 이 파일도 main 것을 꺼내 부른다(PR이 이 파일을 고쳤을 수 있다):
    git fetch origin
    T=$(mktemp -d) && git archive origin/main tools/ci | tar -x -C "$T"
    python3 -I "$T/tools/ci/lead_check.py" --repo . --base origin/main --head origin/<PR 브랜치>
  main에 tools/ci가 없으면(부트스트랩, T-010 PR) PR 브랜치의 tools/ci를 꺼내 부른다. 결과에 부트스트랩 WARN이 나온다.
  안에서 돌리는 도구도 base(main) 커밋에서 다시 꺼낸다. 부른 파일의 폴더에 있는 것은 cigit.py만 쓴다.

하는 일:
1. base(기본 origin/main)와 PR head를 커밋 SHA로 고정한다. head가 base를 품지 않으면 GitHub처럼 병합 커밋을 만든다
   (git merge-tree + commit-tree. 개체만 쓰고 ref·작업 트리는 건드리지 않는다). 충돌이면 종료 2(rebase 필요).
2. 도구: base 커밋의 tools/ci·tools/oracle·.github/workflows/ci.yml을 git archive로 메모리에 꺼낸다. base에 없으면
   head 커밋의 것(부트스트랩. 크게 알린다).
3. 민감한 경로(.github/**, tools/**, .config/**, .claude/**, CLAUDE.md, docs/README-instructions.md,
   docs/decisions/**, global.json)의 변경을 적는다. 있으면 리드는 전체 diff를 새 reviewer에게 보낸다.
4. head 코드를 돌리지 않는 단계: base tools/ci 단위 테스트, base oracle(head 예제를 읽기만 한다).
5. head 코드를 돌리는 단계: build(-warnaserror), test-run(base·head dotnet test, TRX), mutation(바뀐 src가 있을 때만
   Stryker), head oracle check.py·selftest.py(tools/oracle이 바뀌었을 때. ci.yml oracle-head와 같다),
   head tools/ci 단위 테스트(tools/ci가 바뀌었을 때).
   리드 저장소가 아니라 작업 폴더의 일회용 저장소(git init + fetch)에서 돈다. 단계마다 새 세션(프로세스 그룹)으로
   돌리고 끝나면 그룹을 죽인다. 결과 파일은 단계가 끝나자마자 메모리에 읽어 둔다.
6. base regression_guard: 4에서 메모리에 꺼내 둔 도구를 새 폴더에 다시 쓰고(blob id 확인), 리드 저장소의 git 개체와
   메모리의 결과로 돈다(--require-base-tools). 단계 결과는 --job mutation·oracle·oracle-head·test-run으로 넘긴다.

종료 코드: 0 모두 통과, 1 하나라도 실패, 2 실행할 수 없음(커밋 없음, 병합 충돌 등).

남은 위험: 5는 리드 컴퓨터에서 리드 권한으로 돈다. head 테스트가 마음먹고 결과를 위조하거나(예: 단계가 끝난 뒤에도
남는 프로세스가 다른 폴더를 고침) 집 폴더를 건드리면 막지 못한다. 일회용 컨테이너(Claude Code 세션 등)에서 돌린다.
"""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import os
import shutil
import signal
import subprocess
import sys
import tarfile
import tempfile
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import cigit  # noqa: E402

GUARD_SCRIPT = "tools/ci/regression_guard.py"
ORACLE_CHECK = "tools/oracle/check.py"
ORACLE_SELFTEST = "tools/oracle/selftest.py"
# regression_guard에 --job 이름=결과로 넘기는 잡. ci.yml regression-guard 잡의 needs와 같다(`tools/ci/test_regression_guard.py`의 `TestGuardJobsWiring.test_workflow_passes_every_needed_job`이 대조한다).
GUARD_JOBS = ("mutation", "oracle", "oracle-head", "test-run")
TOOL_PATHS = ("tools/ci", "tools/oracle", ".github/workflows/ci.yml")
SOLUTION = "AIAdoptionScorecard.sln"
SENSITIVE_PREFIXES = (".github/", "tools/", ".config/", ".claude/", "docs/decisions/")
SENSITIVE_FILES = ("CLAUDE.md", "docs/README-instructions.md", "global.json")
MERGE_IDENTITY = {
    "GIT_AUTHOR_NAME": "lead_check", "GIT_AUTHOR_EMAIL": "lead-check@example.invalid", "GIT_AUTHOR_DATE": "@0 +0000",
    "GIT_COMMITTER_NAME": "lead_check", "GIT_COMMITTER_EMAIL": "lead-check@example.invalid", "GIT_COMMITTER_DATE": "@0 +0000",
}
STEP_TIMEOUT_SECONDS = 150 * 60


class CheckError(RuntimeError):
    """검사를 시작할 수 없다(종료 2)."""


def is_sensitive(path: str) -> bool:
    return path.startswith(SENSITIVE_PREFIXES) or path in SENSITIVE_FILES


def sensitive_paths(repo: str, base: str, head: str) -> list[str]:
    paths: set[str] = set()
    for c in cigit.diff_changes(repo, base, head, None, renames=True):
        for p in (c.path, c.old_path):
            if p and is_sensitive(p):
                paths.add(p)
    return sorted(paths)


def _git_env(extra: dict[str, str] | None = None) -> dict[str, str]:
    env = cigit.git_env()
    env.update(extra or {})
    return env


def merged_head(repo: str, base: str, tip: str) -> tuple[str, str]:
    """검사할 head 커밋. tip이 base를 품으면 tip, 아니면 GitHub처럼 base에 tip을 병합한 커밋(개체만 만든다)."""
    proc = subprocess.run(["git", "-C", repo, "merge-base", "--is-ancestor", base, tip], env=_git_env())
    if proc.returncode == 0:
        return tip, f"head {tip[:12]}가 base를 품는다(병합 커밋 없음)"
    tree = subprocess.run(["git", "-C", repo, "merge-tree", "--write-tree", "--no-messages", base, tip],
                          stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=_git_env())
    if tree.returncode != 0:
        raise CheckError(f"base {base[:12]}에 head {tip[:12]}를 병합하면 충돌한다. PR을 rebase한 뒤 다시 돈다")
    tree_sha = tree.stdout.decode().split()[0]
    commit = subprocess.run(["git", "-C", repo, "commit-tree", tree_sha, "-p", base, "-p", tip, "-m", f"lead_check: merge {tip} into {base}"],
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=_git_env(MERGE_IDENTITY))
    if commit.returncode != 0:
        raise CheckError("병합 커밋을 만들 수 없다: " + commit.stderr.decode("utf-8", "replace"))
    return commit.stdout.decode().strip(), f"head {tip[:12]}를 base에 병합한 커밋(GitHub refs/pull/N/merge와 같은 모양)"


def archive_bytes(repo: str, rev: str, paths: list[str] | None) -> bytes:
    """rev에서 paths(있는 것만, None이면 트리 전체)를 tar 바이트로."""
    if paths is None:
        present: list[str] = []
    else:
        present = [p for p in paths if cigit.git_bytes(repo, "cat-file", "-e", f"{rev}:{p}") is not None]
        if not present:
            return b""
    data = cigit.git_bytes(repo, "archive", "--format=tar", rev, *(["--", *present] if present else []))
    if data is None:
        raise CheckError(f"git archive {rev[:12]} {' '.join(present)} 실패")
    return data


def extract(data: bytes, dest: str) -> None:
    os.makedirs(dest, exist_ok=True)
    if not data:
        return
    with tarfile.open(fileobj=io.BytesIO(data)) as tar:
        tar.extractall(dest, filter="data")


def blob_ids(folder: str) -> dict[str, str]:
    """folder 아래 파일의 git blob id(상대 경로 → id)."""
    out = {}
    for root, _, files in os.walk(folder):
        for name in files:
            full = os.path.join(root, name)
            with open(full, "rb") as f:
                content = f.read()
            out[os.path.relpath(full, folder).replace(os.sep, "/")] = hashlib.sha1(b"blob %d\0" % len(content) + content).hexdigest()
    return out


def run_step(cmd: list[str], cwd: str, log_path: str, env: dict[str, str], isolate: bool, timeout: float = STEP_TIMEOUT_SECONDS) -> int:
    """명령 하나. isolate면 새 세션으로 돌리고 끝나면 그 프로세스 그룹을 모두 죽인다(남는 head 프로세스를 줄인다)."""
    os.makedirs(os.path.dirname(log_path), exist_ok=True)
    with open(log_path, "w", encoding="utf-8") as log:
        log.write("$ " + " ".join(cmd) + "\n")
        log.flush()
        proc = subprocess.Popen(cmd, cwd=cwd, stdout=log, stderr=subprocess.STDOUT, env=env, start_new_session=isolate)
        try:
            code = proc.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            log.write(f"\n시간 초과({int(timeout)}초)\n")
            code = 124
        finally:
            if isolate and hasattr(os, "killpg"):
                try:
                    os.killpg(proc.pid, signal.SIGKILL)
                except (ProcessLookupError, PermissionError):
                    pass
            if proc.poll() is None:
                proc.kill()
                proc.wait()
    return code


def read_bytes(path: str) -> bytes | None:
    try:
        with open(path, "rb") as f:
            return f.read()
    except OSError:
        return None


def guard_job_args(results: dict[str, bool]) -> list[str]:
    """regression_guard의 --job 인자. GUARD_JOBS마다 하나. results에 없는 잡은 failure다."""
    args: list[str] = []
    for name in GUARD_JOBS:
        args += ["--job", f"{name}={'success' if results.get(name) else 'failure'}"]
    return args


def run_head_oracle(py: str, tree: str, work: str, logs: str, env: dict[str, str], step) -> bool:
    """tools/oracle이 바뀐 PR의 head oracle 단계(ci.yml oracle-head와 같다). head 코드를 돌린다.

    check.py(head 예제 재현)와 selftest.py(스펙 표 대조·음성 대조·결정론)를 둘 다 돈다. head에 selftest.py가 없으면
    실패다. 하나라도 실패하면 False.
    """
    oracle = os.path.join(tree, "tools", "oracle")
    code = run_step([py, "-I", "-B", os.path.join(oracle, "check.py"), "--repo", tree, "--json", os.path.join(work, "oracle-head.json")],
                    work, os.path.join(logs, "oracle-head.log"), env, isolate=True)
    ok = step("oracle-head(head oracle check.py, tools/oracle 바뀜)", code, "로그 logs/oracle-head.log")
    selftest = os.path.join(oracle, "selftest.py")
    if os.path.isfile(selftest):
        code = run_step([py, "-I", "-B", selftest, "--repo", tree], work, os.path.join(logs, "oracle-head-selftest.log"), env, isolate=True)
        detail = "로그 logs/oracle-head-selftest.log"
    else:
        code, detail = None, f"head에 {ORACLE_SELFTEST}가 없다"
    return step("oracle-head(head oracle selftest.py, tools/oracle 바뀜)", code, detail) and ok


def make_sandbox(repo: str, base: str, tip: str, head: str, folder: str) -> str:
    """head 코드를 돌릴 일회용 저장소. 리드 저장소를 원격으로 적지 않는다(FETCH_HEAD를 지운다)."""
    subprocess.run(["git", "init", "-q", folder], check=True, env=_git_env())
    fetch = subprocess.run(["git", "-c", "uploadpack.allowAnySHA1InWant=true", "-C", folder, "fetch", "-q", "--no-tags", repo, base, tip],
                           stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=_git_env())
    if fetch.returncode != 0:
        raise CheckError("일회용 저장소로 가져오지 못했다: " + fetch.stderr.decode("utf-8", "replace"))
    if head != tip:
        again, _ = merged_head(folder, base, tip)
        if again != head:
            raise CheckError(f"일회용 저장소의 병합 커밋이 다르다: {again[:12]} != {head[:12]}")
    for name, sha in (("base", base), ("head", head)):
        cigit.git(folder, "update-ref", f"refs/lead-check/{name}", sha)
    try:
        os.remove(os.path.join(folder, ".git", "FETCH_HEAD"))
    except OSError:
        pass
    return folder


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description="머지 전 독립 검사(PR의 ci.yml을 쓰지 않는다)")
    ap.add_argument("--repo", default=".")
    ap.add_argument("--base", default="origin/main", help="base(기본 origin/main)")
    ap.add_argument("--head", default="HEAD", help="PR head(브랜치·커밋)")
    ap.add_argument("--work", help="작업 폴더(기본: 임시 폴더). 끝나도 지우지 않는다")
    ap.add_argument("--card", action="append", default=[], help="카드 번호(T-xxx). 없으면 --head 이름·커밋 제목에서 찾는다")
    ap.add_argument("--budget-minutes", type=float, default=100, help="mutation 전체 시간(분)")
    ap.add_argument("--json-out", help="결과 JSON 경로(기본: 작업 폴더의 lead-check.json)")
    args = ap.parse_args(argv)
    # 파일로 돌려도 진행이 바로 보이게 줄마다 내보낸다.
    sys.stdout.reconfigure(line_buffering=True)
    os.environ["GIT_NO_REPLACE_OBJECTS"] = "1"
    repo = os.path.abspath(args.repo)
    base = cigit.rev_parse(repo, args.base)
    tip = cigit.rev_parse(repo, args.head)
    if base is None or tip is None:
        print(f"lead_check: 커밋을 찾지 못했다: base {args.base} -> {base}, head {args.head} -> {tip}", file=sys.stderr)
        return 2
    try:
        head, head_note = merged_head(repo, base, tip)
    except CheckError as e:
        print(f"lead_check: {e}", file=sys.stderr)
        return 2
    work = os.path.abspath(args.work or tempfile.mkdtemp(prefix="lead-check-"))
    os.makedirs(work, exist_ok=True)
    py = sys.executable
    steps: list[dict] = []

    def step(name: str, code: int | None, detail: str, ok_codes: tuple[int, ...] = (0,)) -> bool:
        ok = code in ok_codes
        steps.append({"step": name, "ok": ok, "exit_code": code, "detail": detail})
        print(f"[{'PASS' if ok else 'FAIL'}] {name}: {detail} (종료 {code})")
        return ok

    print(f"lead_check: base {base[:12]} ({args.base}), head {tip[:12]} ({args.head}). {head_note}")
    print(f"lead_check: 작업 폴더 {work}")

    # 2. 도구(head 코드가 돌기 전에 메모리에 꺼낸다)
    tools_rev, tools_source = (base, "base") if cigit.blob_id(repo, base, GUARD_SCRIPT) else (head, "head")
    oracle_rev, oracle_source = (base, "base") if cigit.blob_id(repo, base, ORACLE_CHECK) else (head, "head")
    try:
        tools_tar = archive_bytes(repo, tools_rev, ["tools/ci", ".github/workflows/ci.yml"])
        oracle_tar = archive_bytes(repo, oracle_rev, ["tools/oracle"])
        head_tar = archive_bytes(repo, head, None)
    except CheckError as e:
        print(f"lead_check: {e}", file=sys.stderr)
        return 2
    if tools_source == "head":
        print("lead_check: [WARN] 부트스트랩: base에 tools/ci가 없어 head 커밋의 tools/ci로 돈다. 이 결과는 독립 검사가 아니다")
    if oracle_source == "head":
        print("lead_check: [WARN] 부트스트랩: base에 tools/oracle이 없어 head oracle로 돈다")
    tools1 = os.path.join(work, "tools-1")
    shutil.rmtree(tools1, ignore_errors=True)
    extract(tools_tar, tools1)
    extract(oracle_tar, tools1)
    snapshot = blob_ids(tools1)

    # 3. 민감한 경로
    sensitive = sensitive_paths(repo, base, head)
    if sensitive:
        print(f"lead_check: [WARN] 민감한 경로 {len(sensitive)}개가 바뀌었다. 전체 diff(git diff {base[:12]} {head[:12]})를 새 reviewer에게 보낸다:")
        for p in sensitive:
            print(f"    {p}")
    else:
        print("lead_check: 민감한 경로 변경 없음")

    # head 파일(읽기만): base oracle과 자기 검사가 읽는다.
    head_files = os.path.join(work, "head-files")
    shutil.rmtree(head_files, ignore_errors=True)
    extract(head_tar, head_files)
    logs = os.path.join(work, "logs")
    plain_env = cigit.git_env()
    plain_env.pop("PYTHONPATH", None)

    # 4. head 코드를 돌리지 않는 단계
    selftest_env = dict(plain_env, GITHUB_WORKSPACE=head_files)
    ci_dir = os.path.join(tools1, "tools", "ci")
    code = run_step([py, "-I", "-B", "-m", "unittest", "discover", "-s", ci_dir, "-t", ci_dir, "-p", "test_*.py"], work,
                    os.path.join(logs, "selftest-base.log"), selftest_env, isolate=tools_source == "head")
    step("자기 검사(base tools/ci)", code, f"{tools_source} {tools_rev[:12]}. 로그 logs/selftest-base.log")
    oracle_json = os.path.join(work, "oracle.json")
    code = run_step([py, "-I", "-B", os.path.join(tools1, "tools", "oracle", "check.py"), "--repo", head_files, "--json", oracle_json], work,
                    os.path.join(logs, "oracle-base.log"), plain_env, isolate=oracle_source == "head")
    oracle_ok = step("oracle(base oracle, head 예제)", code, f"{oracle_source} {oracle_rev[:12]}")
    oracle_bytes = read_bytes(oracle_json)
    oracle_tree = cigit.git(repo, "rev-parse", f"{oracle_rev}:tools/oracle", check=False).strip()
    oracle_source_bytes = (json.dumps({"schema_version": 1, "source": oracle_source, "rev": oracle_rev, "tree": oracle_tree}) + "\n").encode()

    # 5. head 코드를 돌리는 단계(일회용 저장소)
    try:
        sandbox = make_sandbox(repo, base, tip, head, os.path.join(work, "sandbox"))
    except (CheckError, subprocess.CalledProcessError, cigit.GitError) as e:
        print(f"lead_check: {e}", file=sys.stderr)
        return 2
    head_env = dict(plain_env, MSBUILDDISABLENODEREUSE="1", DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")
    build_tree = os.path.join(work, "build")
    shutil.rmtree(build_tree, ignore_errors=True)
    cigit.git(sandbox, "worktree", "add", "-q", "--detach", build_tree, head)
    try:
        code = run_step(["dotnet", "restore", SOLUTION], build_tree, os.path.join(logs, "build.log"), head_env, isolate=True)
        if code == 0:
            code = run_step(["dotnet", "build", SOLUTION, "--no-restore", "-c", "Release", "-warnaserror"], build_tree,
                            os.path.join(logs, "build-warnaserror.log"), head_env, isolate=True)
        step("build(head, Release -warnaserror)", code, "로그 logs/build*.log")
        changed = {c.path for c in cigit.diff_changes(repo, base, head, None, renames=False)}
        oracle_head_ok = True
        if oracle_source == "base" and any(p.startswith("tools/oracle/") for p in changed):
            oracle_head_ok = run_head_oracle(py, build_tree, work, logs, head_env, step)
        if tools_source == "base" and any(p.startswith("tools/ci/") for p in changed):
            head_ci = os.path.join(build_tree, "tools", "ci")
            code = run_step([py, "-I", "-B", "-m", "unittest", "discover", "-s", head_ci, "-t", head_ci, "-p", "test_*.py"], build_tree,
                            os.path.join(logs, "selftest-head.log"), dict(head_env, GITHUB_WORKSPACE=build_tree), isolate=True)
            step("자기 검사(head tools/ci, 바뀜)", code, "로그 logs/selftest-head.log")
    finally:
        cigit.git(sandbox, "worktree", "remove", "--force", build_tree, check=False)
    testrun_json = os.path.join(work, "testrun.json")
    code = run_step([py, "-I", "-B", os.path.join(ci_dir, "testrun.py"), "--repo", sandbox, "--base", base, "--head", head,
                     "--out", testrun_json, "--work", os.path.join(work, "testrun")], work, os.path.join(logs, "testrun.log"), head_env, isolate=True)
    testrun_ok = step("test-run(base·head dotnet test)", code, "로그 logs/testrun.log")
    testrun_bytes = read_bytes(testrun_json)
    mutation_json = os.path.join(work, "mutation.json")
    code = run_step([py, "-I", "-B", os.path.join(ci_dir, "mutation.py"), "--repo", sandbox, "--base", base, "--head", head, "--out", mutation_json,
                     "--work", os.path.join(work, "mutation"), "--summary", "", "--budget-minutes", str(args.budget_minutes)],
                    work, os.path.join(logs, "mutation.log"), head_env, isolate=True)
    mutation_bytes = read_bytes(mutation_json)
    try:
        mutation_status = json.loads((mutation_bytes or b"{}").decode("utf-8")).get("status")
    except ValueError:
        mutation_status = None
    mutation_ok = step("mutation(바뀐 src만 Stryker)", code, f"status={mutation_status}. 로그 logs/mutation.log")

    # 6. base regression_guard: 도구를 새 폴더에 다시 쓰고 처음에 꺼낸 것과 같은지 본다.
    tools2 = os.path.join(work, f"tools-2-{int(time.time())}")
    extract(tools_tar, tools2)
    extract(oracle_tar, tools2)
    if blob_ids(tools2) != snapshot:
        step("도구 다시 꺼내기", 1, "처음에 꺼낸 도구와 다르다")
        return 1
    results = os.path.join(tools2, "results")
    os.makedirs(results)
    files = {"mutation.json": mutation_bytes, "oracle.json": oracle_bytes, "oracle-source.json": oracle_source_bytes, "testrun.json": testrun_bytes}
    for name, data in files.items():
        if data is not None:
            with open(os.path.join(results, name), "wb") as f:
                f.write(data)
    cards = list(args.card) + [args.head]
    guard_json = os.path.join(results, "regression-guard.json")
    cmd = [py, "-I", "-B", os.path.join(tools2, "tools", "ci", "regression_guard.py"), "--repo", repo, "--base", base, "--head", head,
           "--require-base-tools", "--mutation", os.path.join(results, "mutation.json"), "--oracle", os.path.join(results, "oracle.json"),
           "--oracle-source", os.path.join(results, "oracle-source.json"), "--testrun", os.path.join(results, "testrun.json"),
           "--json-out", guard_json, "--summary", ""]
    cmd += guard_job_args({"mutation": mutation_ok, "oracle": oracle_ok, "oracle-head": oracle_head_ok, "test-run": testrun_ok})
    for c in cards:
        cmd += ["--card", c]
    guard_env = dict(plain_env, MUTATION_RESULT="success" if mutation_ok else "failure", ORACLE_RESULT="success" if oracle_ok else "failure")
    guard_env.pop("GITHUB_HEAD_REF", None)
    proc = subprocess.run(cmd, cwd=work, env=guard_env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    guard_out = proc.stdout.decode("utf-8", "replace")
    with open(os.path.join(logs, "regression-guard.log"), "w", encoding="utf-8") as f:
        f.write(guard_out)
    print("--- regression-guard(base 도구) ---")
    print("\n".join(line for line in guard_out.splitlines() if not line.startswith(("[INFO]", "[PASS]"))))
    step("regression-guard(base 도구)", proc.returncode, "로그 logs/regression-guard.log")

    ok = all(s["ok"] for s in steps)
    report = {
        "schema_version": 1,
        "ok": ok,
        "base": base,
        "head_tip": tip,
        "head": head,
        "head_note": head_note,
        "tools_source": tools_source,
        "oracle_source": oracle_source,
        "sensitive_paths": sensitive,
        "steps": steps,
    }
    out = args.json_out or os.path.join(work, "lead-check.json")
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"lead_check: {'통과' if ok else '실패'}. 민감한 경로 {'있음(새 reviewer에게 전체 diff)' if sensitive else '없음'}. 결과 {out}")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
