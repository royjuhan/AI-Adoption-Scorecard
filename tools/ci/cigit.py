"""CI 잡(mutation, regression-guard)이 같이 쓰는 git 도우미.

- base·head 범위를 정하는 규칙은 이 파일 한 곳에만 둔다. 두 잡이 같은 범위를 봐야 한다.
- 표준 라이브러리만 쓴다. 저장소의 src/·tests/를 읽지 않고 git 개체만 읽는다.
"""

from __future__ import annotations

import subprocess
from dataclasses import dataclass

ZERO_SHA = "0" * 40


class GitError(RuntimeError):
    """git 명령이 실패했다."""


def git(repo: str, *args: str, check: bool = True) -> str:
    """git 명령을 돌려 표준 출력(UTF-8)을 돌려준다."""
    proc = subprocess.run(
        ["git", "-C", repo, *args],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    if check and proc.returncode != 0:
        raise GitError(f"git {' '.join(args)}: {proc.stderr.decode('utf-8', 'replace').strip()}")
    return proc.stdout.decode("utf-8", "surrogateescape")


def git_bytes(repo: str, *args: str) -> bytes | None:
    """git 명령의 표준 출력을 바이트로. 실패하면 None."""
    proc = subprocess.run(["git", "-C", repo, *args], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if proc.returncode != 0:
        return None
    return proc.stdout


def rev_parse(repo: str, rev: str) -> str | None:
    """커밋을 40자 SHA로. 없으면 None."""
    if not rev or rev == ZERO_SHA:
        return None
    proc = subprocess.run(
        ["git", "-C", repo, "rev-parse", "--verify", "--quiet", rev + "^{commit}"],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    if proc.returncode != 0:
        return None
    return proc.stdout.decode().strip()


@dataclass(frozen=True)
class Range:
    base: str
    head: str
    note: str  # base를 어떻게 정했는지 사람이 읽을 한 줄


def resolve_range(repo: str, base: str | None, head: str = "HEAD") -> Range:
    """base·head를 정한다.

    - head: 기본 HEAD. PR 이벤트에서는 checkout한 병합 커밋이다.
    - base: 주어진 값(PR: pull_request.base.sha, push: event.before).
      비었거나 0 40개(새 브랜치 push)거나 저장소에 없으면 head의 첫 부모(HEAD~1)로 한다.
    """
    head_sha = rev_parse(repo, head)
    if head_sha is None:
        raise GitError(f"head 커밋이 없다: {head}")
    base_sha = rev_parse(repo, base or "")
    if base_sha is not None:
        return Range(base_sha, head_sha, f"base {base_sha[:12]} (주어진 값)")
    parent = rev_parse(repo, head_sha + "^1")
    if parent is None:
        raise GitError("base가 없고 head에 부모 커밋도 없다")
    given = base if base else "(없음)"
    return Range(parent, head_sha, f"base {parent[:12]} (주어진 base {given}를 쓸 수 없어 head의 첫 부모)")


@dataclass(frozen=True)
class Change:
    status: str  # A, M, D, R, T, C 중 첫 글자
    path: str  # head 쪽 경로(삭제면 base 쪽 경로)
    old_path: str | None  # 이름 변경·복사의 base 쪽 경로


def diff_changes(repo: str, base: str, head: str, paths: list[str] | None = None, renames: bool = True) -> list[Change]:
    """base..head 트리 차이(두 점, 트리 대 트리). renames=True면 git diff -M."""
    args = ["diff", "--name-status", "-z", "--no-color", "--no-ext-diff"]
    args.append("-M" if renames else "--no-renames")
    args += [base, head]
    if paths:
        args += ["--", *paths]
    out = git(repo, *args)
    fields = out.split("\0")
    changes: list[Change] = []
    i = 0
    while i < len(fields):
        status = fields[i]
        if not status:
            i += 1
            continue
        kind = status[0]
        if kind in ("R", "C"):
            old, new = fields[i + 1], fields[i + 2]
            changes.append(Change(kind, new, old))
            i += 3
        else:
            changes.append(Change(kind, fields[i + 1], None))
            i += 2
    return changes


def list_files(repo: str, rev: str, prefix: str) -> list[str]:
    """rev 트리에서 prefix 아래 파일 경로(서수 정렬)."""
    out = git(repo, "ls-tree", "-r", "-z", "--name-only", rev, "--", prefix)
    return sorted(p for p in out.split("\0") if p)


def list_dirs(repo: str, rev: str, folder: str) -> list[str]:
    """rev 트리에서 folder 바로 아래 폴더 이름(서수 정렬)."""
    folder = folder.rstrip("/") + "/"
    out = git(repo, "ls-tree", "-z", rev, "--", folder)
    names = []
    for entry in out.split("\0"):
        if not entry:
            continue
        meta, path = entry.split("\t", 1)
        if meta.split()[1] == "tree":
            names.append(path[len(folder):])
    return sorted(names)


def read_tree_files(repo: str, rev: str, prefix: str, suffix: str) -> dict[str, bytes]:
    """rev 트리에서 prefix 아래, suffix로 끝나는 파일의 내용. git cat-file --batch 한 번으로 읽는다."""
    out = git(repo, "ls-tree", "-r", "-z", rev, "--", prefix)
    wanted: list[tuple[str, str]] = []
    for entry in out.split("\0"):
        if not entry:
            continue
        meta, path = entry.split("\t", 1)
        _, kind, sha = meta.split()
        if kind == "blob" and path.endswith(suffix):
            wanted.append((path, sha))
    if not wanted:
        return {}
    proc = subprocess.run(
        ["git", "-C", repo, "cat-file", "--batch"],
        input="".join(sha + "\n" for _, sha in wanted).encode(),
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    if proc.returncode != 0:
        raise GitError("git cat-file --batch: " + proc.stderr.decode("utf-8", "replace"))
    data = proc.stdout
    result: dict[str, bytes] = {}
    pos = 0
    for path, sha in wanted:
        nl = data.index(b"\n", pos)
        header = data[pos:nl].decode().split()
        size = int(header[2])
        result[path] = data[nl + 1:nl + 1 + size]
        pos = nl + 1 + size + 1
    return result


def read_file(repo: str, rev: str, path: str) -> bytes | None:
    """rev의 path 내용. 없으면 None."""
    return git_bytes(repo, "show", f"{rev}:{path}")


def blob_id(repo: str, rev: str, path: str) -> str | None:
    """rev의 path blob SHA. 없으면 None."""
    out = git_bytes(repo, "rev-parse", "--verify", "--quiet", f"{rev}:{path}")
    if out is None:
        return None
    return out.decode().strip() or None


def commits_in_range(repo: str, base: str, head: str) -> list[str]:
    """head에서 닿고 base에서 닿지 않는 커밋(git rev-list base..head)."""
    out = git(repo, "rev-list", f"{base}..{head}")
    return [c for c in out.split() if c]


def parents(repo: str, commit: str) -> list[str]:
    out = git(repo, "rev-list", "--parents", "-n", "1", commit).split()
    return out[1:]


def commit_message(repo: str, commit: str) -> str:
    return git(repo, "log", "-1", "--format=%B", commit)


def commits_changing(repo: str, base: str, head: str, path: str) -> list[str]:
    """base..head 안에서 path를 바꾼 커밋.

    - 병합이 아닌 커밋: 첫 부모와 path의 blob이 다르면(추가·삭제 포함) 바꾼 것이다.
    - 병합 커밋: 모든 부모와 blob이 다를 때만 바꾼 것이다(충돌 해결·수동 수정).
      한 부모와 같으면 그 부모 쪽 커밋이 바꾼 것이고, 그 커밋을 따로 본다.
      GitHub의 PR 병합 커밋(refs/pull/N/merge)과 merge commit 방식 머지가 여기서 빠진다.
    """
    result = []
    for commit in commits_in_range(repo, base, head):
        mine = blob_id(repo, commit, path)
        ps = parents(repo, commit)
        if not ps:
            if mine is not None:
                result.append(commit)
            continue
        theirs = [blob_id(repo, p, path) for p in ps]
        if len(ps) == 1:
            if theirs[0] != mine:
                result.append(commit)
        elif all(t != mine for t in theirs):
            result.append(commit)
    return result
