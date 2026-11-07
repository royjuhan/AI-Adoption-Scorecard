#!/usr/bin/env python3
"""regression-guard: PR base 대비 회귀 신호를 막는다(ADR 0005 원문 1, 결정 5. T-010 수용 기준 1).

막는 것(증거가 있으면 통과):
  A. tests/ 아래 파일 삭제, Skip 추가, [Fact]·[Theory] 수 감소, guard 샘플 목록 축소
     증거: mutation.json에서 바뀐 src 파일마다 head 점수 >= base 점수, 비교한 파일이 하나 이상.
  B. examples/**/expected/** 의 base에 있던 파일 수정
     증거: 그 파일을 바꾼 head 쪽 커밋 메시지가 모두 "expected migration:"으로 시작하고, oracle.json에서 match.
     삭제는 oracle로 재현할 수 없으므로 늘 실패. 새 파일은 막지 않지만 oracle이 match여야 한다.

자세한 규칙과 한계는 tools/ci/README.md.

종료 코드: 0 통과, 1 규칙 위반, 2 실행할 수 없음(base·head가 없음 등).
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
from dataclasses import dataclass, field
from fractions import Fraction

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import cigit  # noqa: E402
import csharp_scan  # noqa: E402

TESTS_PREFIX = "tests/"
GUARD_SAMPLES_DIR = "tests/Scorecard.Tests/TestData/guard"
EXPECTED_MESSAGE_PREFIX = "expected migration:"


@dataclass
class Finding:
    level: str  # PASS, FAIL, WARN, INFO
    rule: str
    message: str


@dataclass
class Result:
    range: cigit.Range
    findings: list[Finding] = field(default_factory=list)

    @property
    def ok(self) -> bool:
        return not any(f.level == "FAIL" for f in self.findings)

    def add(self, level: str, rule: str, message: str) -> None:
        self.findings.append(Finding(level, rule, message))


def is_expected_path(path: str) -> bool:
    parts = path.split("/")
    return len(parts) >= 3 and parts[0] == "examples" and "expected" in parts[1:-1]


def _scan_tree(repo: str, rev: str) -> dict[str, csharp_scan.FileScan]:
    files = cigit.read_tree_files(repo, rev, TESTS_PREFIX, ".cs")
    return {path: csharp_scan.analyze(data.decode("utf-8", "replace")) for path, data in files.items()}


def _guard_names(scans: dict[str, csharp_scan.FileScan]) -> list[str] | None:
    found = None
    for path in sorted(scans):
        names = scans[path].guard_names
        if names is not None:
            found = (found or []) + names
    return found


def _load_json(path: str | None) -> tuple[dict | None, str | None]:
    if not path:
        return None, "경로가 주어지지 않았다"
    if not os.path.isfile(path):
        return None, f"파일이 없다: {path}"
    try:
        with open(path, encoding="utf-8") as f:
            data = json.load(f)
    except (OSError, ValueError) as e:
        return None, f"읽을 수 없다: {e}"
    if not isinstance(data, dict):
        return None, "최상위가 객체가 아니다"
    return data, None


# ---------------------------------------------------------------- mutation 증거


def _fraction(stats: dict | None) -> Fraction | None:
    """점수 = (killed + timeout) / (killed + timeout + survived + no_coverage). 유효 돌연변이가 없으면 None."""
    if not stats:
        return None
    detected = int(stats.get("killed", 0)) + int(stats.get("timeout", 0))
    valid = detected + int(stats.get("survived", 0)) + int(stats.get("no_coverage", 0))
    if valid == 0:
        return None
    return Fraction(detected, valid)


def _pct(fr: Fraction | None) -> str:
    return "없음" if fr is None else f"{float(fr) * 100:.2f}%"


def mutation_evidence(data: dict | None, load_error: str | None, rng: cigit.Range, job_result: str) -> tuple[bool, list[str]]:
    """mutation.json이 증거가 되는지. (통과, 이유 줄들)"""
    reasons: list[str] = []
    if data is None:
        return False, [f"mutation.json을 쓸 수 없다({load_error}). mutation 잡 결과: {job_result}"]
    if data.get("base") != rng.base or data.get("head") != rng.head:
        return False, [f"mutation.json의 범위가 다르다: {str(data.get('base'))[:12]}..{str(data.get('head'))[:12]}, 이 잡은 {rng.base[:12]}..{rng.head[:12]}"]
    status = data.get("status")
    if status == "not_applicable":
        return False, ["바뀐 src 파일이 없어 mutation 증거가 없다"]
    if status != "measured":
        errors = "; ".join(str(e) for e in data.get("errors") or []) or "이유 없음"
        return False, [f"mutation 측정이 끝나지 않았다(status={status}): {errors}"]
    files = data.get("files") or []
    if not files:
        return False, ["mutation.json에 파일이 없다"]
    ok = True
    compared = 0
    head_total = [0, 0]
    base_total = [0, 0]
    for f in files:
        path = f.get("path")
        head = _fraction(f.get("head"))
        if f.get("head") is None:
            ok = False
            reasons.append(f"{path}: head 점수가 없다")
            continue
        if f.get("base") is None:
            reasons.append(f"{path}: base에 없는 새 파일이라 비교하지 않는다(head {_pct(head)})")
            continue
        base = _fraction(f.get("base"))
        if head is None and base is None:
            reasons.append(f"{path}: 양쪽 모두 유효 돌연변이가 없어 비교하지 않는다")
            continue
        if head is None or base is None:
            ok = False
            reasons.append(f"{path}: 점수를 비교할 수 없다(base {_pct(base)}, head {_pct(head)})")
            continue
        compared += 1
        for total, stats in ((head_total, f["head"]), (base_total, f["base"])):
            d = int(stats.get("killed", 0)) + int(stats.get("timeout", 0))
            total[0] += d
            total[1] += d + int(stats.get("survived", 0)) + int(stats.get("no_coverage", 0))
        if head < base:
            ok = False
            reasons.append(f"{path}: head {_pct(head)} < base {_pct(base)}")
        else:
            reasons.append(f"{path}: head {_pct(head)} >= base {_pct(base)}")
    if compared == 0:
        ok = False
        reasons.append("base와 점수를 비교한 src 파일이 하나도 없다")
    else:
        h = Fraction(*head_total)
        b = Fraction(*base_total)
        if h < b:
            ok = False
        reasons.append(f"합계: head {_pct(h)} {'>=' if h >= b else '<'} base {_pct(b)} (파일 {compared}개)")
    return ok, reasons


# ---------------------------------------------------------------- 검사


def check(repo: str, base: str | None, head: str, mutation_path: str | None, oracle_path: str | None,
          mutation_job: str = "알 수 없음", oracle_job: str = "알 수 없음") -> Result:
    rng = cigit.resolve_range(repo, base, head)
    result = Result(rng)
    result.add("INFO", "range", f"{rng.note}, head {rng.head[:12]}")

    triggers: list[Finding] = []

    # A1. tests/ 파일 삭제. 이름 변경(git diff -M)은 삭제가 아니다.
    tchanges = cigit.diff_changes(repo, rng.base, rng.head, [TESTS_PREFIX], renames=True)
    renames = {c.path: c.old_path for c in tchanges if c.status == "R" and c.old_path}
    for c in tchanges:
        if c.status == "D":
            triggers.append(Finding("FAIL", "test-file-deleted", f"tests 파일 삭제: {c.path}"))
        elif c.status == "R":
            result.add("INFO", "test-file-renamed", f"이름 변경(삭제로 보지 않는다): {c.old_path} -> {c.path}")

    base_scan = _scan_tree(repo, rng.base)
    head_scan = _scan_tree(repo, rng.head)

    # A2. Skip 추가. 파일마다 종류별 수가 늘면 걸린다.
    for path in sorted(head_scan):
        base_path = renames.get(path, path)
        before = base_scan[base_path].skip_kinds() if base_path in base_scan else {}
        after = head_scan[path].skip_kinds()
        for kind in sorted(after):
            added = after[kind] - before.get(kind, 0)
            if added > 0:
                lines = [str(ln) for ln, k in head_scan[path].skips if k == kind][:5]
                triggers.append(Finding("FAIL", "skip-added", f"Skip 추가: {path} {kind} +{added} (줄 {', '.join(lines)})"))

    # A3. [Fact]·[Theory] 수 감소(tests/ 전체).
    base_facts = sum(s.fact_count for s in base_scan.values())
    head_facts = sum(s.fact_count for s in head_scan.values())
    if head_facts < base_facts:
        reverse = {old: new for new, old in renames.items()}
        detail = []
        for bpath in sorted(base_scan):
            hpath = reverse.get(bpath, bpath)
            delta = (head_scan[hpath].fact_count if hpath in head_scan else 0) - base_scan[bpath].fact_count
            if delta < 0:
                detail.append(f"{bpath} {delta}")
        triggers.append(Finding("FAIL", "test-count-decreased", f"[Fact]·[Theory] {base_facts} -> {head_facts} ({'; '.join(detail[:10])})"))
    else:
        result.add("INFO", "test-count", f"[Fact]·[Theory] {base_facts} -> {head_facts}")

    # A4. guard 샘플 목록 축소: GuardTargets.All 대상 이름 수, TestData/guard/* 폴더 수.
    base_names = _guard_names(base_scan) or []
    head_names = _guard_names(head_scan) or []
    base_count, head_count = len(set(base_names)), len(set(head_names))
    missing = sorted(set(base_names) - set(head_names))
    if head_count < base_count:
        triggers.append(Finding("FAIL", "guard-shrunk", f"GuardTargets 대상 {base_count} -> {head_count} (빠진 이름: {', '.join(missing)})"))
    else:
        result.add("INFO", "guard-targets", f"GuardTargets 대상 {base_count} -> {head_count}" + (f" (이름이 바뀜: {', '.join(missing)})" if missing else ""))
    base_dirs = cigit.list_dirs(repo, rng.base, GUARD_SAMPLES_DIR)
    head_dirs = cigit.list_dirs(repo, rng.head, GUARD_SAMPLES_DIR)
    if len(head_dirs) < len(base_dirs):
        gone = sorted(set(base_dirs) - set(head_dirs))
        triggers.append(Finding("FAIL", "guard-shrunk", f"guard 샘플 폴더 {len(base_dirs)} -> {len(head_dirs)} (빠진 폴더: {', '.join(gone)})"))
    else:
        result.add("INFO", "guard-samples", f"guard 샘플 폴더 {len(base_dirs)} -> {len(head_dirs)}")

    # A 증거: mutation.
    if triggers:
        data, err = _load_json(mutation_path)
        ok, reasons = mutation_evidence(data, err, rng, mutation_job)
        for t in triggers:
            result.add("FAIL" if not ok else "WARN", t.rule, t.message + ("" if not ok else " — mutation 증거로 통과"))
        for r in reasons:
            result.add("PASS" if ok else "FAIL", "mutation-evidence", r)
        if ok and any(t.rule in ("test-file-deleted", "test-count-decreased") for t in triggers):
            result.add("WARN", "status-note", "테스트 삭제·수 감소는 증거가 있어도 docs/STATUS.md에 적는다(ADR 0005 남은 위험)")
    else:
        result.add("PASS", "tests", "tests 삭제·Skip 추가·[Fact]/[Theory] 감소·guard 축소 없음")

    # B. examples/**/expected/**
    echanges = [c for c in cigit.diff_changes(repo, rng.base, rng.head, ["examples/"], renames=False) if is_expected_path(c.path)]
    if not echanges:
        result.add("PASS", "expected", "examples/**/expected/** 변경 없음")
        return result
    oracle, oracle_err = _load_json(oracle_path)
    oracle_files: dict[str, dict] = {}
    if oracle is not None:
        for entry in oracle.get("files") or []:
            if isinstance(entry, dict) and isinstance(entry.get("path"), str):
                p = entry["path"].replace("\\", "/")
                oracle_files[p[2:] if p.startswith("./") else p] = entry

    def oracle_reason(path: str) -> str | None:
        """oracle이 이 파일을 같은 바이트로 재현했으면 None, 아니면 이유."""
        if oracle is None:
            return f"oracle.json을 쓸 수 없다({oracle_err}). oracle 잡 결과: {oracle_job}"
        entry = oracle_files.get(path)
        if entry is None:
            return "oracle.json에 이 파일이 없다"
        if entry.get("status") != "match":
            return f"oracle status={entry.get('status')} (expected {str(entry.get('expected_sha256'))[:12]}, oracle {str(entry.get('oracle_sha256'))[:12]})"
        content = cigit.read_file(repo, rng.head, path)
        actual = hashlib.sha256(content or b"").hexdigest()
        for key in ("expected_sha256", "oracle_sha256"):
            if entry.get(key) is not None and entry.get(key) != actual:
                return f"oracle.json의 {key}가 head 파일 SHA-256과 다르다({str(entry.get(key))[:12]} != {actual[:12]})"
        return None

    for c in echanges:
        if c.status == "D":
            result.add("FAIL", "expected-deleted", f"{c.path}: base에 있던 expected 파일 삭제. oracle로 재현할 수 없어 늘 막는다")
        elif c.status == "A":
            why = oracle_reason(c.path)
            if why:
                result.add("FAIL", "expected-added", f"{c.path}: 새 expected 파일인데 oracle이 재현하지 못했다: {why}")
            else:
                result.add("PASS", "expected-added", f"{c.path}: 새 expected 파일, oracle match")
        else:
            commits = cigit.commits_changing(repo, rng.base, rng.head, c.path)
            bad = [cm for cm in commits if not cigit.commit_message(repo, cm).startswith(EXPECTED_MESSAGE_PREFIX)]
            why = oracle_reason(c.path)
            problems = []
            if not commits:
                problems.append("이 파일을 바꾼 커밋을 범위 안에서 찾지 못했다")
            for cm in bad:
                subject = cigit.commit_message(repo, cm).splitlines()[0] if cigit.commit_message(repo, cm).strip() else ""
                problems.append(f"커밋 {cm[:12]} 메시지가 \"{EXPECTED_MESSAGE_PREFIX}\"로 시작하지 않는다: {subject[:60]}")
            if why:
                problems.append(why)
            if problems:
                for p in problems:
                    result.add("FAIL", "expected-changed", f"{c.path}: {p}")
            else:
                result.add("PASS", "expected-changed", f"{c.path}: 커밋 {len(commits)}개 모두 \"{EXPECTED_MESSAGE_PREFIX}\", oracle match")
    return result


# ---------------------------------------------------------------- 출력


def render_lines(result: Result) -> list[str]:
    lines = [f"[{f.level}] {f.rule}: {f.message}" for f in result.findings]
    lines.append(f"regression-guard: {'통과' if result.ok else '실패'}")
    return lines


def render_summary(result: Result) -> str:
    def esc(s: str) -> str:
        return s.replace("|", "\\|").replace("\n", " ")

    out = [f"## regression-guard: {'통과' if result.ok else '실패'}", "", f"범위: `{result.range.base[:12]}..{result.range.head[:12]}` ({esc(result.range.note)})", ""]
    out += ["| 결과 | 규칙 | 내용 |", "| --- | --- | --- |"]
    for f in result.findings:
        out.append(f"| {f.level} | `{f.rule}` | {esc(f.message)} |")
    out.append("")
    out.append("규칙: `tools/ci/README.md`")
    return "\n".join(out) + "\n"


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description="PR base 대비 회귀 신호 검사(ADR 0005)")
    ap.add_argument("--repo", default=".")
    ap.add_argument("--base", default=os.environ.get("CI_BASE", ""), help="base 커밋. 비거나 없으면 head의 첫 부모")
    ap.add_argument("--head", default="HEAD")
    ap.add_argument("--mutation", help="mutation.json 경로")
    ap.add_argument("--oracle", help="oracle.json 경로")
    ap.add_argument("--json-out", help="결과 JSON을 쓸 경로")
    ap.add_argument("--summary", default=os.environ.get("GITHUB_STEP_SUMMARY"), help="job summary(Markdown)를 덧붙일 경로")
    args = ap.parse_args(argv)
    try:
        result = check(
            args.repo, args.base, args.head, args.mutation, args.oracle,
            mutation_job=os.environ.get("MUTATION_RESULT", "알 수 없음"),
            oracle_job=os.environ.get("ORACLE_RESULT", "알 수 없음"),
        )
    except cigit.GitError as e:
        print(f"regression-guard: 실행할 수 없다: {e}", file=sys.stderr)
        return 2
    for line in render_lines(result):
        print(line)
    if args.summary:
        with open(args.summary, "a", encoding="utf-8") as f:
            f.write(render_summary(result))
    if args.json_out:
        payload = {
            "schema_version": 1,
            "ok": result.ok,
            "base": result.range.base,
            "head": result.range.head,
            "findings": [{"level": f.level, "rule": f.rule, "message": f.message} for f in result.findings],
        }
        with open(args.json_out, "w", encoding="utf-8", newline="\n") as f:
            json.dump(payload, f, ensure_ascii=False, indent=2)
            f.write("\n")
    return 0 if result.ok else 1


if __name__ == "__main__":
    sys.exit(main())
