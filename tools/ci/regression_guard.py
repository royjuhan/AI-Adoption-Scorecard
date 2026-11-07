#!/usr/bin/env python3
"""regression-guard: PR base 대비 회귀 신호를 막는다(ADR 0005 원문 1, 결정 5. T-010 수용 기준 1).

막는 것(증거가 있으면 통과):
  C. CI 도구·원칙 파일: .github/**, tools/ci/**, tools/oracle/**, .claude/**, .config/dotnet-tools.json, CLAUDE.md,
     docs/README-instructions.md, global.json 변경과 기존 docs/decisions/** 수정·삭제(ci-changed).
     통과: 같은 PR이 docs/decisions/에 새 ADR 파일을 더하고 docs/STATUS.md "원칙 변경" 표에 그 ADR 행을 더한다.
     base에 tools/ci/regression_guard.py가 없으면(부트스트랩, T-010) 적용하지 않는다.
     src/·tests/ 파일에 CI 러너·검증 결과 이름(RUNNER_TEMP, GITHUB_ENV, testrun.json 등)이 늘면 늘 막는다(ci-tamper).
  A. tests/ 아래 파일 삭제, Skip 추가, [Fact]·[Theory] 수 감소, guard 샘플 목록 축소, 테스트 설정 파일 변경,
     실행 결과(TRX)에서 base 통과 테스트가 head에서 빠짐·건너뜀 증가·전체 수 감소.
     증거: mutation.json에서 바뀐 src 파일마다 head 점수 >= base 점수, 비교한 파일이 하나 이상.
     Stryker 처음 테스트 실행에 실패가 있으면 증거가 아니다.
     빠진 이름이 클래스별 통과 수를 지키면(이름 변경) docs/STATUS.md "잠정 결정"에 카드 행을 더해 WARN으로 통과한다.
     head에서 실패한 테스트가 하나라도 있으면 증거와 상관없이 막는다(test-run-failed).
     src와 기존 테스트 파일이 같이 바뀌면 그 테스트 파일을 WARN으로 적는다(tests-changed-with-src).
  B. examples/**/expected/** 의 base에 있던 파일 수정
     증거: 그 파일을 바꾼 커밋 메시지가 모두 "expected migration:"으로 시작하고, base oracle의 oracle.json에서 match.
     삭제는 oracle로 재현할 수 없으므로 늘 실패. 새 파일은 커밋 메시지를 보지 않지만 base oracle이 match여야 한다.
     tools/oracle/**과 기존 expected를 같은 PR에서 바꾸면 base oracle이 재현할 때만 통과(oracle-and-expected).
     "expected migration:" 커밋은 examples/**/expected/**와 docs/spec/**만 바꾼다(migration-commit-scope).
     docs/spec/**에서는 json·yaml·csv 코드 블록 안과 SHA-256 값만 바꾼다.
  잡 결과: --job 이름=결과로 받은 잡(test-run, oracle-head)이 success가 아니면 막는다(job-result).

이 스크립트와 tools/oracle은 CI에서 base 커밋의 것을 돈다(.github/workflows/ci.yml). --require-base-tools면
돌고 있는 이 폴더의 파일이 base 커밋의 tools/ci와 같은지 스스로 확인한다. regression-guard 잡은 head 코드를
하나도 돌리지 않는다(B4). head 코드를 돌리는 잡(mutation, test-run)의 결과 파일은 head 코드가 위조할 수 있다.

자세한 규칙과 한계는 tools/ci/README.md.

종료 코드: 0 통과, 1 규칙 위반, 2 실행할 수 없음(base·head가 없음 등).
"""

from __future__ import annotations

import argparse
import difflib
import hashlib
import json
import os
import re
import sys
from collections import Counter
from dataclasses import dataclass, field
from fractions import Fraction

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import cigit  # noqa: E402
import csharp_scan  # noqa: E402

TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
TESTS_PREFIX = "tests/"
GUARD_SAMPLES_DIR = "tests/Scorecard.Tests/TestData/guard"
EXPECTED_MESSAGE_PREFIX = "expected migration:"
GUARD_SCRIPT = "tools/ci/regression_guard.py"
ORACLE_DIR = "tools/oracle"
ORACLE_CHECK = "tools/oracle/check.py"
DECISIONS_DIR = "docs/decisions/"
STATUS_FILE = "docs/STATUS.md"
PRINCIPLE_SECTION = "원칙 변경"
PROVISIONAL_SECTION = "잠정 결정"
# ci-changed 대상: CI 도구와 원칙 파일. 접두사(폴더)와 정확한 경로. docs/decisions/는 기존 파일의 수정·삭제만 본다.
CI_PREFIXES = (".github/", "tools/ci/", "tools/oracle/", ".claude/")
CI_FILES = (".config/dotnet-tools.json", "CLAUDE.md", "docs/README-instructions.md", "global.json")
# expected migration 커밋이 바꿀 수 있는 곳(ADR 0004 expected 변경 규칙): expected 파일과 스펙.
MIGRATION_ALLOWED_PREFIXES = ("docs/spec/",)
SPEC_PREFIX = "docs/spec/"
# 스펙에서 migration 커밋이 바꿀 수 있는 코드 블록 언어. 블록 밖은 SHA-256 값(16진 64자)만 바꿀 수 있다.
MIGRATION_BLOCK_LANGS = ("json", "yaml", "yml", "csv")
HEX64 = re.compile(r"[0-9a-fA-F]{64}")
# ci-tamper: src/·tests/ 파일에서 이 글자(대소문자 무시)가 늘면 막는다. CI 러너 환경과 검증 도구·결과 파일 이름이다.
# head 코드(테스트)가 base 도구·결과 파일·GITHUB_ENV를 고치는 길(T-010 재리뷰 B4 n1)을 걸러 내는 덫이다. 우회할 수 있다.
TAMPER_PREFIXES = ("src/", "tests/")
TAMPER_PATTERNS = (
    "RUNNER_TEMP", "RUNNER_TOOL_CACHE", "GITHUB_ENV", "GITHUB_PATH", "GITHUB_OUTPUT", "GITHUB_STEP_SUMMARY",
    "GITHUB_WORKSPACE", "ACTIONS_RUNTIME", "ACTIONS_ID_TOKEN", "base-tools", "refs/replace", "regression_guard",
    "testrun.json", "mutation.json", "mutation-report", "oracle.json", "oracle-source", ".trx", "tools/ci", "tools/oracle",
)
CARD_ID = re.compile(r"(?<![A-Za-z0-9-])T-\d{3,}(?!\d)")
MISSING_NAMES_SHOWN = 50


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


def is_ci_path(path: str) -> bool:
    return path.startswith(CI_PREFIXES) or path in CI_FILES


def ci_watched_paths(changes: list[cigit.Change]) -> list[str]:
    """ci-changed가 보는 바뀐 경로. docs/decisions/는 새 파일(A)이 아닌 변경만(기존 ADR 수정·삭제)."""
    paths: set[str] = set()
    for c in changes:
        for p in (c.path, c.old_path):
            if p and is_ci_path(p):
                paths.add(p)
        if c.status != "A" and c.path.startswith(DECISIONS_DIR):
            paths.add(c.path)
    return sorted(paths)


def is_test_infra_path(path: str) -> bool:
    """테스트가 어떻게 빌드·실행되는지 바꾸는 파일. 소스를 세는 검사가 보지 못하는 길이다."""
    name = path.rsplit("/", 1)[-1]
    return (
        (path.startswith(TESTS_PREFIX) and name.endswith(".csproj"))
        or name.startswith("Directory.Build.")
        or name.endswith(".runsettings")
        or name == "xunit.runner.json"
    )


def _project_dirs(repo: str, rev: str) -> list[str]:
    """rev 트리에서 tests/ 아래 .csproj가 있는 폴더."""
    return sorted({p.rsplit("/", 1)[0] for p in cigit.list_files(repo, rev, TESTS_PREFIX) if p.endswith(".csproj")})


def _compiled(path: str, project_dirs: list[str]) -> bool:
    """.csproj 폴더 아래(obj/·bin/ 제외) 파일만 빌드에 든다고 본다."""
    for d in project_dirs:
        if path.startswith(d + "/"):
            rest = path[len(d) + 1:].split("/")
            return not (rest[0] in ("obj", "bin"))
    return False


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


def git_blob_id(content: bytes) -> str:
    """git hash-object와 같은 blob SHA-1."""
    return hashlib.sha1(b"blob %d\0" % len(content) + content).hexdigest()


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
    failing = [r for r in data.get("runs") or [] if isinstance(r, dict) and int(r.get("initial_failures") or 0) > 0]
    if failing:
        return False, [f"Stryker {r.get('side')} {r.get('project')}: 처음 테스트 실행에서 {r.get('initial_failures')}개가 실패했다. 실패한 테스트가 있는 점수는 증거가 아니다" for r in failing]
    status = data.get("status")
    if status == "not_applicable":
        return False, ["바뀐 src 파일이 없어 mutation 증거가 없다"]
    if status == "timeout":
        errors = "; ".join(str(e) for e in data.get("errors") or []) or "이유 없음"
        return False, [f"mutation 측정이 시간 안에 끝나지 않았다(status=timeout). 증거가 아니다: {errors}"]
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


# ---------------------------------------------------------------- 도구 출처


def check_tools_source(result: Result, repo: str, rng: cigit.Range, tools_dir: str, require_base: bool) -> None:
    """돌고 있는 tools/ci 파일이 base 커밋의 것과 같은지. base에 guard가 없으면 부트스트랩."""
    if cigit.blob_id(repo, rng.base, GUARD_SCRIPT) is None:
        result.add("INFO", "tools-source", f"base에 {GUARD_SCRIPT}가 없다(부트스트랩). head의 tools/ci·tools/oracle로 돈다")
        return
    differ = []
    for name in sorted(os.listdir(tools_dir)):
        if not name.endswith(".py"):
            continue
        with open(os.path.join(tools_dir, name), "rb") as f:
            mine = git_blob_id(f.read())
        if cigit.blob_id(repo, rng.base, f"tools/ci/{name}") != mine:
            differ.append(name)
    if not differ:
        result.add("INFO", "tools-source", f"tools/ci는 base {rng.base[:12]}의 것과 같다")
    elif require_base:
        result.add("FAIL", "tools-source", f"돌고 있는 tools/ci가 base의 것이 아니다: {', '.join(differ)}. CI는 base 커밋의 tools/ci를 돌려야 한다")
    else:
        result.add("WARN", "tools-source", f"돌고 있는 tools/ci가 base의 것과 다르다: {', '.join(differ)} (로컬 실행)")


# ---------------------------------------------------------------- C. ci-changed


def _section_rows(text: str, title: str) -> list[str]:
    """STATUS.md의 "## <title>" 절 표의 데이터 행(머리 행·구분 행 제외)."""
    rows: list[str] = []
    inside = False
    for line in text.splitlines():
        if line.startswith("## "):
            inside = line[3:].strip().startswith(title)
            continue
        if not inside or not line.lstrip().startswith("|"):
            continue
        cells = line.strip()
        if set(cells.replace("|", "").strip()) <= set("-: "):
            continue
        rows.append(cells)
    return rows[1:] if rows else []


def _principle_rows(text: str) -> list[str]:
    return _section_rows(text, PRINCIPLE_SECTION)


def added_status_rows(repo: str, rng: cigit.Range, title: str) -> list[str]:
    """base..head에서 STATUS.md "## <title>" 표에 더한 행."""
    base_rows = _section_rows((cigit.read_file(repo, rng.base, STATUS_FILE) or b"").decode("utf-8", "replace"), title)
    head_rows = _section_rows((cigit.read_file(repo, rng.head, STATUS_FILE) or b"").decode("utf-8", "replace"), title)
    return list((Counter(head_rows) - Counter(base_rows)).elements())


def card_ids(repo: str, rng: cigit.Range, extra: list[str] | None = None) -> list[str]:
    """이 PR의 카드 번호(T-xxx). --card·브랜치 이름(extra)과 범위 안 병합이 아닌 커밋 제목에서 찾는다."""
    found: set[str] = set()
    for value in extra or []:
        found.update(CARD_ID.findall(value or ""))
    for commit in cigit.commits_in_range(repo, rng.base, rng.head):
        if len(cigit.parents(repo, commit)) > 1:
            continue
        subject = (cigit.commit_message(repo, commit).splitlines() or [""])[0]
        found.update(CARD_ID.findall(subject))
    return sorted(found)


def check_ci_changed(result: Result, repo: str, rng: cigit.Range, changes: list[cigit.Change]) -> None:
    ci_paths = ci_watched_paths(changes)
    if not ci_paths:
        result.add("PASS", "ci-changed", "CI 도구·원칙 파일(.github/**, tools/ci/**, tools/oracle/**, .claude/**, .config/dotnet-tools.json, CLAUDE.md, docs/README-instructions.md, global.json, 기존 docs/decisions/**) 변경 없음")
        return
    shown = ", ".join(ci_paths[:10]) + (f" 외 {len(ci_paths) - 10}개" if len(ci_paths) > 10 else "")
    if cigit.blob_id(repo, rng.base, GUARD_SCRIPT) is None:
        result.add("WARN", "ci-changed", f"부트스트랩: base에 {GUARD_SCRIPT}가 없어 적용하지 않는다. 바뀐 CI 도구: {shown}")
        return
    added_adrs = sorted(c.path for c in changes if c.status == "A" and c.path.startswith(DECISIONS_DIR) and c.path.endswith(".md"))
    added_rows = added_status_rows(repo, rng, PRINCIPLE_SECTION)
    problems = []
    if not added_adrs:
        problems.append(f"{DECISIONS_DIR}에 새 ADR 파일이 없다")
    if not added_rows:
        problems.append(f"{STATUS_FILE} \"{PRINCIPLE_SECTION}\" 표에 더한 행이 없다")
    if added_adrs and added_rows:
        numbers = [m.group(1) for p in added_adrs if (m := re.match(r"(\d+)-", p[len(DECISIONS_DIR):]))]
        if numbers and not any(re.search(rf"(?<!\d){n}(?!\d)", row) for n in numbers for row in added_rows):
            problems.append(f"\"{PRINCIPLE_SECTION}\" 표에 더한 행이 새 ADR 번호({', '.join(numbers)})를 적지 않았다")
    if problems:
        for p in problems:
            result.add("FAIL", "ci-changed", f"CI 도구·원칙 파일을 바꿨다({shown}): {p}")
    else:
        result.add("PASS", "ci-changed", f"CI 도구·원칙 파일을 바꿨다({shown}). 새 ADR {', '.join(added_adrs)}, \"{PRINCIPLE_SECTION}\" 행 {len(added_rows)}개")


# ---------------------------------------------------------------- C. ci-tamper


def _decode_escapes(text: str) -> str:
    """C#·JSON 문자열의 \\uXXXX·\\UXXXXXXXX를 글자로 바꾼다(덫을 escape로 비켜 가지 못하게)."""
    def one(m: re.Match) -> str:
        code = int(m.group(1) or m.group(2), 16)
        return chr(code) if code <= 0x10FFFF else m.group(0)
    return re.sub(r"\\u([0-9a-fA-F]{4})|\\U([0-9a-fA-F]{8})", one, text)


def tamper_counts(data: bytes | None) -> Counter[str]:
    if not data:
        return Counter()
    text = _decode_escapes(data.decode("utf-8", "replace")).lower()
    return Counter({p: text.count(p.lower()) for p in TAMPER_PATTERNS if p.lower() in text})


def check_tamper(result: Result, repo: str, rng: cigit.Range, changes: list[cigit.Change]) -> None:
    hits = []
    for c in changes:
        if c.status == "D" or not c.path.startswith(TAMPER_PREFIXES):
            continue
        after = tamper_counts(cigit.read_file(repo, rng.head, c.path))
        if not after:
            continue
        before = Counter() if c.status == "A" else tamper_counts(cigit.read_file(repo, rng.base, c.path))
        added = after - before
        if added:
            hits.append(f"{c.path}: {', '.join(sorted(added))}")
    if not hits:
        result.add("PASS", "ci-tamper", "src/·tests/에 CI 러너·검증 결과 이름이 늘지 않았다")
        return
    for h in hits:
        result.add("FAIL", "ci-tamper", f"CI 러너 환경·검증 도구·결과 파일 이름이 늘었다: {h}. head 코드(테스트)가 base 도구·결과 파일·GITHUB_ENV를 고칠 수 있는 길이라 증거와 상관없이 막는다")


# ---------------------------------------------------------------- 잡 결과


def check_jobs(result: Result, jobs: dict[str, str]) -> None:
    for name in sorted(jobs):
        state = jobs[name]
        if state == "success":
            result.add("PASS", "job-result", f"{name} 잡: success")
        else:
            result.add("FAIL", "job-result", f"{name} 잡 결과가 {state}다. 이 잡이 녹색이어야 통과한다")


# ---------------------------------------------------------------- A. test-run


def _validate_testrun(data: dict | None, err: str | None, rng: cigit.Range) -> str | None:
    if data is None:
        return f"testrun.json을 쓸 수 없다({err})"
    if data.get("base") != rng.base or data.get("head") != rng.head:
        return f"testrun.json의 범위가 다르다: {str(data.get('base'))[:12]}..{str(data.get('head'))[:12]}, 이 잡은 {rng.base[:12]}..{rng.head[:12]}"
    if data.get("status") != "measured":
        return f"test-run이 끝나지 않았다(status={data.get('status')}): {'; '.join(str(e) for e in data.get('errors') or []) or '이유 없음'}"
    sides = data.get("sides") or {}
    for side in ("base", "head"):
        entry = sides.get(side)
        if not isinstance(entry, dict) or entry.get("status") != "ok":
            return f"{side} test-run 결과가 없다"
        for key in ("total", "passed", "failed", "skipped", "passed_names", "skipped_names"):
            if key not in entry:
                return f"{side} test-run 결과에 {key}가 없다"
        if not isinstance(entry["passed_names"], list) or len(entry["passed_names"]) != entry["passed"]:
            return f"{side} test-run 결과의 passed_names 수가 passed와 다르다"
    return None


def trx_class(name: str) -> str:
    """TRX testName의 클래스 부분. 인자 "(…)"를 떼고 마지막 "." 앞."""
    return name.split("(", 1)[0].rsplit(".", 1)[0]


def rename_like(base_names: list[str], head_names: list[str], missing: list[str]) -> bool:
    """빠진 이름의 클래스마다 head 통과 수가 base 통과 수 이상인가(이름만 바뀐 것으로 볼 수 있나)."""
    base_count = Counter(trx_class(n) for n in base_names)
    head_count = Counter(trx_class(n) for n in head_names)
    classes = {trx_class(n) for n in missing}
    return bool(classes) and all(head_count[k] >= base_count[k] for k in classes)


def check_testrun(result: Result, triggers: list[Finding], testrun_path: str | None, rng: cigit.Range,
                  provisional_rows: list[str] | None = None, cards: list[str] | None = None) -> None:
    if testrun_path is None:
        result.add("WARN", "test-run", "--testrun이 없어 실행 기반 검사를 하지 않았다(CI는 늘 준다)")
        return
    data, err = _load_json(testrun_path)
    problem = _validate_testrun(data, err, rng)
    if problem:
        result.add("FAIL", "test-run", problem)
        return
    b, h = data["sides"]["base"], data["sides"]["head"]
    result.add("INFO", "test-run", f"전체 {b['total']} -> {h['total']}, 통과 {b['passed']} -> {h['passed']}, 건너뜀 {b['skipped']} -> {h['skipped']}, 실패 {b['failed']} -> {h['failed']}")
    if h["failed"] > 0:
        failed = sorted(h.get("failed_names") or [])
        result.add("FAIL", "test-run-failed", f"head에서 실패한 테스트 {h['failed']}개. 증거와 상관없이 막는다: {'; '.join(failed[:10])}" + (f" 외 {len(failed) - 10}개" if len(failed) > 10 else ""))
    if b["failed"] > 0:
        result.add("WARN", "test-run-base-failed", f"base에서도 실패한 테스트가 {b['failed']}개 있다")
    deficit = Counter(b["passed_names"]) - Counter(h["passed_names"])
    names = sorted(deficit)
    if names and rename_like(b["passed_names"], h["passed_names"], names):
        cards = cards or []
        mentions = [row for row in provisional_rows or []
                    if any(re.search(rf"(?<![A-Za-z0-9-]){re.escape(c)}(?!\d)", row) for c in cards)]
        if mentions:
            note = mentions[0] if len(mentions[0]) <= 120 else mentions[0][:120] + "…"
            for name in names[:MISSING_NAMES_SHOWN]:
                result.add("WARN", "test-run-renamed", f"이름이 바뀐 것으로 본다(클래스별 통과 수 그대로, {STATUS_FILE} \"{PROVISIONAL_SECTION}\" 행 {note}): {name}")
            if len(names) > MISSING_NAMES_SHOWN:
                result.add("WARN", "test-run-renamed", f"그 밖에 {len(names) - MISSING_NAMES_SHOWN}개 이름(전체 {len(names)}개, 목록은 testrun.json)")
            names = []
        else:
            result.add("INFO", "test-run-renamed",
                       f"빠진 이름 {len(names)}개는 클래스별 통과 수가 그대로다(이름 변경일 수 있다). 이름만 바뀐 것이면 같은 PR이 "
                       f"{STATUS_FILE} \"{PROVISIONAL_SECTION}\" 표에 카드({', '.join(cards) or '카드 번호를 찾지 못했다'})를 적은 행을 더해 WARN으로 통과한다")
    for name in names[:MISSING_NAMES_SHOWN]:
        times = f" (x{deficit[name]})" if deficit[name] > 1 else ""
        triggers.append(Finding("FAIL", "test-run-missing", f"base에서 통과한 테스트가 head에서 통과하지 않는다: {name}{times}"))
    if len(names) > MISSING_NAMES_SHOWN:
        triggers.append(Finding("FAIL", "test-run-missing", f"그 밖에 {len(names) - MISSING_NAMES_SHOWN}개 이름(전체 {len(names)}개, 목록은 testrun.json)"))
    if h["skipped"] > b["skipped"]:
        new_skips = sorted((Counter(h["skipped_names"]) - Counter(b["skipped_names"])).elements())
        triggers.append(Finding("FAIL", "test-run-skipped", f"건너뛴 테스트 {b['skipped']} -> {h['skipped']} ({'; '.join(new_skips[:10])})"))
    if h["total"] < b["total"]:
        triggers.append(Finding("FAIL", "test-run-total-decreased", f"돈 테스트 전체 수 {b['total']} -> {h['total']}"))


# ---------------------------------------------------------------- B. oracle 출처


def _oracle_provenance(repo: str, rng: cigit.Range, source_path: str | None) -> tuple[bool, str]:
    """oracle.json이 base oracle(base에 없으면 head oracle)에서 나왔는지. (믿을 수 있나, 이유)"""
    base_tree = cigit.git(repo, "rev-parse", "--verify", "--quiet", f"{rng.base}:{ORACLE_DIR}", check=False).strip() or None
    base_has = cigit.blob_id(repo, rng.base, ORACLE_CHECK) is not None
    if source_path is None:
        if base_has:
            return False, "oracle-source.json이 없어 base oracle의 결과인지 알 수 없다"
        return True, "부트스트랩: base에 oracle이 없다. head oracle 결과를 쓴다(oracle-source.json 없음)"
    data, err = _load_json(source_path)
    if data is None:
        return False, f"oracle-source.json을 쓸 수 없다({err})"
    source, rev, tree = data.get("source"), data.get("rev"), data.get("tree")
    if base_has:
        if source != "base" or rev != rng.base or tree != base_tree:
            return False, f"oracle.json이 base oracle에서 나오지 않았다(source={source}, rev={str(rev)[:12]}, tree={str(tree)[:12]}; base {rng.base[:12]} tree {str(base_tree)[:12]})"
        return True, f"base oracle(base {rng.base[:12]}, tools/oracle {str(base_tree)[:12]})"
    head_tree = cigit.git(repo, "rev-parse", "--verify", "--quiet", f"{rng.head}:{ORACLE_DIR}", check=False).strip() or None
    if source != "head" or tree != head_tree:
        return False, f"부트스트랩인데 oracle-source.json이 head oracle을 가리키지 않는다(source={source}, tree={str(tree)[:12]})"
    return True, "부트스트랩: base에 oracle이 없어 head oracle을 썼다"


# ---------------------------------------------------------------- B. migration 커밋 범위


def _commit_paths(repo: str, commit: str) -> list[str]:
    """커밋이 바꾼 파일. 병합 커밋은 모든 부모와 다른 파일만(commits_changing과 같은 뜻)."""
    ps = cigit.parents(repo, commit)
    if not ps:
        return cigit.list_files(repo, commit, ".")
    sets = []
    for p in ps:
        out = cigit.git(repo, "diff", "--name-only", "-z", "--no-renames", p, commit)
        sets.append({x for x in out.split("\0") if x})
    return sorted(set.intersection(*sets))


def spec_block_lines(lines: list[str]) -> list[bool]:
    """줄마다 json·yaml·csv 코드 블록 안(울타리 줄 포함)인가."""
    inside: list[bool] = []
    fence: tuple[str, int, bool] | None = None  # (울타리 글자, 길이, 허용 언어인가)
    for line in lines:
        text = line.strip()
        if fence is None:
            m = re.match(r"(`{3,}|~{3,})(.*)$", text)
            if m:
                info = m.group(2).strip().split()
                fence = (m.group(1)[0], len(m.group(1)), bool(info) and info[0].lower() in MIGRATION_BLOCK_LANGS)
                inside.append(fence[2])
            else:
                inside.append(False)
            continue
        inside.append(fence[2])
        if re.fullmatch(re.escape(fence[0]) + "{%d,}" % fence[1], text):
            fence = None
    return inside


def spec_diff_problems(old: bytes | None, new: bytes | None) -> list[str]:
    """expected migration 커밋의 스펙 변경이 코드 블록(json·yaml·csv) 안과 SHA-256 값만 바꿨는지. 문제 줄들."""
    if old is None:
        return ["새 스펙 파일이다(블록·SHA-256 줄만 바꿀 수 있다)"]
    if new is None:
        return ["스펙 파일을 지웠다"]
    a = old.decode("utf-8", "replace").splitlines()
    b = new.decode("utf-8", "replace").splitlines()
    ka, kb = spec_block_lines(a), spec_block_lines(b)
    problems = []
    for tag, i1, i2, j1, j2 in difflib.SequenceMatcher(None, a, b, autojunk=False).get_opcodes():
        if tag == "equal":
            continue
        rest_a = [HEX64.sub("<sha256>", a[i]) for i in range(i1, i2) if not ka[i]]
        rest_b = [HEX64.sub("<sha256>", b[j]) for j in range(j1, j2) if not kb[j]]
        if rest_a != rest_b:
            sample = (rest_b or rest_a)[0].strip()
            problems.append(f"{j1 + 1}번째 줄 근처, 블록 밖 글이 바뀌었다: {sample[:60]}")
    return problems


def check_migration_commits(result: Result, repo: str, rng: cigit.Range) -> None:
    for commit in cigit.commits_in_range(repo, rng.base, rng.head):
        message = cigit.commit_message(repo, commit)
        if not message.startswith(EXPECTED_MESSAGE_PREFIX):
            continue
        paths = _commit_paths(repo, commit)
        outside = [p for p in paths if not (is_expected_path(p) or p.startswith(MIGRATION_ALLOWED_PREFIXES))]
        subject = message.splitlines()[0][:60] if message.strip() else ""
        if outside:
            shown = ", ".join(outside[:10]) + (f" 외 {len(outside) - 10}개" if len(outside) > 10 else "")
            result.add("FAIL", "migration-commit-scope", f"커밋 {commit[:12]} \"{subject}\"가 examples/**/expected/**·docs/spec/** 밖을 바꿨다: {shown}")
            continue
        parent = (cigit.parents(repo, commit) or [None])[0]
        spec_bad = []
        for path in (p for p in paths if p.startswith(SPEC_PREFIX)):
            old = cigit.read_file(repo, parent, path) if parent else None
            for problem in spec_diff_problems(old, cigit.read_file(repo, commit, path)):
                spec_bad.append(f"{path}: {problem}")
        if spec_bad:
            for problem in spec_bad[:10]:
                result.add("FAIL", "migration-commit-scope", f"커밋 {commit[:12]} \"{subject}\": {problem}. 스펙은 json·yaml·csv 코드 블록 안과 SHA-256 값만 바꾼다")
        else:
            result.add("PASS", "migration-commit-scope", f"커밋 {commit[:12]} \"{subject}\": expected·docs/spec(블록·SHA-256)만")


# ---------------------------------------------------------------- 검사


def check(repo: str, base: str | None, head: str, mutation_path: str | None, oracle_path: str | None,
          mutation_job: str = "알 수 없음", oracle_job: str = "알 수 없음", *,
          testrun_path: str | None = None, oracle_source_path: str | None = None,
          require_base_tools: bool = False, tools_dir: str = TOOLS_DIR,
          cards: list[str] | None = None, jobs: dict[str, str] | None = None) -> Result:
    rng = cigit.resolve_range(repo, base, head)
    result = Result(rng)
    result.add("INFO", "range", f"{rng.note}, head {rng.head[:12]}")
    check_tools_source(result, repo, rng, tools_dir, require_base_tools)
    if jobs:
        check_jobs(result, jobs)

    all_changes = cigit.diff_changes(repo, rng.base, rng.head, None, renames=False)

    # C. CI 도구·원칙 파일 변경, src/·tests/의 CI 환경 덫
    check_ci_changed(result, repo, rng, all_changes)
    check_tamper(result, repo, rng, all_changes)

    triggers: list[Finding] = []

    # A1. tests/ 파일 삭제. 이름 변경(git diff -M)은 삭제가 아니다.
    tchanges = cigit.diff_changes(repo, rng.base, rng.head, [TESTS_PREFIX], renames=True)
    renames = {c.path: c.old_path for c in tchanges if c.status == "R" and c.old_path}
    for c in tchanges:
        if c.status == "D":
            triggers.append(Finding("FAIL", "test-file-deleted", f"tests 파일 삭제: {c.path}"))
        elif c.status == "R":
            result.add("INFO", "test-file-renamed", f"이름 변경(삭제로 보지 않는다): {c.old_path} -> {c.path}")

    # A5. 테스트 빌드·실행 설정 파일 변경
    for path in sorted({c.path for c in all_changes if is_test_infra_path(c.path)}):
        triggers.append(Finding("FAIL", "test-infra-changed", f"테스트 빌드·실행 설정 파일이 바뀌었다: {path}"))

    # src와 기존 테스트 파일이 같이 바뀌면 그 테스트 파일을 적는다(단언을 약하게 바꿨는지 사람이 본다).
    if any(c.path.startswith("src/") for c in all_changes):
        modified = sorted(c.path for c in tchanges if c.status in ("M", "R", "T") and c.path.endswith(".cs"))
        if modified:
            shown = ", ".join(modified[:20]) + (f" 외 {len(modified) - 20}개" if len(modified) > 20 else "")
            result.add("WARN", "tests-changed-with-src", f"src와 기존 테스트 파일이 같이 바뀌었다. 단언을 약하게 바꾸지 않았는지 reviewer가 본다: {shown}")

    base_scan = _scan_tree(repo, rng.base)
    head_scan = _scan_tree(repo, rng.head)

    # A2. Skip 추가. 파일마다 종류별 수가 늘면 걸린다(프로젝트 밖 파일도 본다).
    for path in sorted(head_scan):
        base_path = renames.get(path, path)
        before = base_scan[base_path].skip_kinds() if base_path in base_scan else {}
        after = head_scan[path].skip_kinds()
        for kind in sorted(after):
            added = after[kind] - before.get(kind, 0)
            if added > 0:
                lines = [str(ln) for ln, k in head_scan[path].skips if k == kind][:5]
                triggers.append(Finding("FAIL", "skip-added", f"Skip 추가: {path} {kind} +{added} (줄 {', '.join(lines)})"))

    # A3. [Fact]·[Theory] 수 감소. .csproj 폴더 아래(빌드에 드는) 파일만 센다.
    base_dirs_proj = _project_dirs(repo, rng.base)
    head_dirs_proj = _project_dirs(repo, rng.head)
    base_facts = sum(s.fact_count for p, s in base_scan.items() if _compiled(p, base_dirs_proj))
    head_facts = sum(s.fact_count for p, s in head_scan.items() if _compiled(p, head_dirs_proj))
    outside = sorted(p for p, s in head_scan.items() if s.fact_count and not _compiled(p, head_dirs_proj))
    if outside:
        result.add("INFO", "test-count-outside", "프로젝트(.csproj 폴더) 밖이라 세지 않은 파일: " + ", ".join(f"{p} ({head_scan[p].fact_count})" for p in outside[:10]))
    if head_facts < base_facts:
        reverse = {old: new for new, old in renames.items()}
        detail = []
        for bpath in sorted(base_scan):
            if not _compiled(bpath, base_dirs_proj):
                continue
            hpath = reverse.get(bpath, bpath)
            now = head_scan[hpath].fact_count if hpath in head_scan and _compiled(hpath, head_dirs_proj) else 0
            delta = now - base_scan[bpath].fact_count
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

    # A6. 실행 기반: TRX에서 base 통과 이름 ⊆ head 통과 이름, 건너뜀·전체 수.
    check_testrun(result, triggers, testrun_path, rng, added_status_rows(repo, rng, PROVISIONAL_SECTION), card_ids(repo, rng, cards))

    # A 증거: mutation.
    if triggers:
        data, err = _load_json(mutation_path)
        ok, reasons = mutation_evidence(data, err, rng, mutation_job)
        for t in triggers:
            result.add("FAIL" if not ok else "WARN", t.rule, t.message + ("" if not ok else " — mutation 증거로 통과"))
        for r in reasons:
            result.add("PASS" if ok else "FAIL", "mutation-evidence", r)
        if ok and any(t.rule in ("test-file-deleted", "test-count-decreased", "test-run-missing", "test-run-total-decreased") for t in triggers):
            result.add("WARN", "status-note", "테스트 삭제·수 감소는 증거가 있어도 docs/STATUS.md에 적는다(ADR 0005 남은 위험)")
    else:
        result.add("PASS", "tests", "tests 삭제·Skip 추가·[Fact]/[Theory] 감소·guard 축소·테스트 설정 변경·실행 결과 축소 없음")

    # B. expected migration 커밋 범위(ADR 0004 expected 변경 규칙)
    check_migration_commits(result, repo, rng)

    # B. examples/**/expected/**
    echanges = [c for c in all_changes if is_expected_path(c.path)]
    if not echanges:
        result.add("PASS", "expected", "examples/**/expected/** 변경 없음")
        return result
    oracle, oracle_err = _load_json(oracle_path)
    trusted, provenance = _oracle_provenance(repo, rng, oracle_source_path)
    result.add("INFO" if trusted else "FAIL", "oracle-source", provenance)
    oracle_files: dict[str, dict] = {}
    if oracle is not None:
        for entry in oracle.get("files") or []:
            if isinstance(entry, dict) and isinstance(entry.get("path"), str):
                p = entry["path"].replace("\\", "/")
                oracle_files[p[2:] if p.startswith("./") else p] = entry

    def oracle_reason(path: str) -> str | None:
        """base oracle이 이 파일을 같은 바이트로 재현했으면 None, 아니면 이유."""
        if not trusted:
            return f"oracle 결과를 증거로 쓸 수 없다: {provenance}"
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
            if not isinstance(entry.get(key), str):
                return f"oracle.json의 {key}가 없다(null). SHA-256 없이는 재현을 확인할 수 없다"
            if entry[key] != actual:
                return f"oracle.json의 {key}가 head 파일 SHA-256과 다르다({entry[key][:12]} != {actual[:12]})"
        return None

    # oracle-and-expected: tools/oracle과 기존 expected를 같은 PR에서 바꾸면 base oracle이 재현해야 한다.
    oracle_changed = any(c.path.startswith(ORACLE_DIR + "/") for c in all_changes)
    existing = [c for c in echanges if c.status in ("M", "D", "T")]
    if oracle_changed and existing:
        base_has_oracle = cigit.blob_id(repo, rng.base, ORACLE_CHECK) is not None
        for c in existing:
            if not base_has_oracle:
                why = "base에 oracle이 없어 base oracle로 재현할 수 없다"
            elif c.status == "D":
                why = "삭제한 파일은 재현할 수 없다"
            else:
                why = oracle_reason(c.path)
            if why:
                result.add("FAIL", "oracle-and-expected", f"{c.path}: tools/oracle과 기존 expected를 같은 PR에서 바꿨다. base oracle이 재현해야 한다: {why}")
            else:
                result.add("PASS", "oracle-and-expected", f"{c.path}: tools/oracle이 바뀌었지만 base oracle이 head expected를 재현했다")

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
    ap.add_argument("--oracle-source", help="oracle-source.json 경로(oracle.json을 낸 oracle의 출처)")
    ap.add_argument("--testrun", help="testrun.json 경로(tools/ci/testrun.py). 없으면 실행 기반 검사를 건너뛰고 WARN")
    ap.add_argument("--require-base-tools", action="store_true", help="돌고 있는 tools/ci가 base 커밋의 것이 아니면 실패(CI)")
    ap.add_argument("--card", action="append", default=[], help="이 PR의 카드 번호(T-xxx). 여러 번. 브랜치 이름도 된다")
    ap.add_argument("--job", action="append", default=[], metavar="이름=결과", help="이 잡이 기다린 잡의 결과. success가 아니면 실패")
    ap.add_argument("--json-out", help="결과 JSON을 쓸 경로")
    ap.add_argument("--summary", default=os.environ.get("GITHUB_STEP_SUMMARY"), help="job summary(Markdown)를 덧붙일 경로")
    args = ap.parse_args(argv)
    jobs: dict[str, str] = {}
    for item in args.job:
        name, sep, state = item.partition("=")
        if not sep or not name:
            print(f"regression-guard: --job은 이름=결과 모양이다: {item}", file=sys.stderr)
            return 2
        jobs[name] = state or "없음"
    cards = list(args.card) + [os.environ.get("GITHUB_HEAD_REF", "")]
    try:
        result = check(
            args.repo, args.base, args.head, args.mutation, args.oracle,
            mutation_job=os.environ.get("MUTATION_RESULT", "알 수 없음"),
            oracle_job=os.environ.get("ORACLE_RESULT", "알 수 없음"),
            testrun_path=args.testrun,
            oracle_source_path=args.oracle_source,
            require_base_tools=args.require_base_tools,
            cards=cards,
            jobs=jobs,
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
