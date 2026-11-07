"""regression-guard·mutation 도우미 자기 검사(unittest).

임시 git 저장소를 만들어 규칙마다 양성(걸려야 함)·음성(걸리면 안 됨)을 본다.
실행: python3 -m unittest discover -s tools/ci -p "test_*.py"
"""

from __future__ import annotations

import contextlib
import io
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import textwrap
import time
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import cigit  # noqa: E402
import csharp_scan  # noqa: E402
import lead_check  # noqa: E402
import mutation  # noqa: E402
import regression_guard as rg  # noqa: E402
import testrun  # noqa: E402

_ENV_BACKUP: dict[str, str | None] = {}
_GIT_HOME = ""
HERE = os.path.dirname(os.path.abspath(__file__))


def repo_file(*parts: str) -> str | None:
    """저장소 파일. 이 폴더의 두 단계 위(checkout 안) → GITHUB_WORKSPACE 순서로 찾는다.

    CI의 regression-guard 잡은 base 커밋의 tools/ci를 임시 폴더에 꺼내 돌린다(옆에 tests/가 없다). 그때는
    GITHUB_WORKSPACE(checkout 폴더)의 파일을 본다. GITHUB_WORKSPACE가 있는데 못 찾으면 실패한다(건너뛰지 않는다).
    """
    candidates = [os.path.join(HERE, "..", "..", *parts)]
    if os.environ.get("GITHUB_WORKSPACE"):
        candidates.append(os.path.join(os.environ["GITHUB_WORKSPACE"], *parts))
    for c in candidates:
        if os.path.isfile(c):
            return c
    if os.environ.get("GITHUB_WORKSPACE"):
        raise AssertionError(f"{'/'.join(parts)}를 찾지 못했다: {candidates}")
    return None


def setUpModule() -> None:  # noqa: N802
    # 사용자 git 설정(서명, 훅 등)과 섞이지 않게 한다.
    global _GIT_HOME
    _GIT_HOME = tempfile.mkdtemp(prefix="guard-test-home-")
    config = os.path.join(_GIT_HOME, "gitconfig")
    with open(config, "w", encoding="utf-8") as f:
        f.write("[user]\n\tname = guard-test\n\temail = guard-test@example.invalid\n[commit]\n\tgpgsign = false\n[init]\n\tdefaultBranch = main\n")
    for key, value in {"GIT_CONFIG_GLOBAL": config, "GIT_CONFIG_NOSYSTEM": "1"}.items():
        _ENV_BACKUP[key] = os.environ.get(key)
        os.environ[key] = value


def tearDownModule() -> None:  # noqa: N802
    for key, value in _ENV_BACKUP.items():
        if value is None:
            os.environ.pop(key, None)
        else:
            os.environ[key] = value
    shutil.rmtree(_GIT_HOME, ignore_errors=True)


class TempRepo:
    def __init__(self) -> None:
        self.root = tempfile.mkdtemp(prefix="guard-test-")
        self.git("init", "-q", "-b", "main")

    def git(self, *args: str) -> str:
        proc = subprocess.run(["git", "-C", self.root, *args], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        if proc.returncode != 0:
            raise RuntimeError(f"git {' '.join(args)}: {proc.stderr.decode()}")
        return proc.stdout.decode().strip()

    def write(self, path: str, text: str) -> None:
        full = os.path.join(self.root, path)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w", encoding="utf-8", newline="\n") as f:
            f.write(textwrap.dedent(text).lstrip("\n"))

    def read(self, path: str) -> str:
        with open(os.path.join(self.root, path), encoding="utf-8") as f:
            return f.read()

    def replace(self, path: str, old: str, new: str) -> None:
        text = self.read(path)
        assert old in text, (path, old)
        self.write(path, text.replace(old, new, 1))

    def remove(self, path: str) -> None:
        self.git("rm", "-q", "-r", path)

    def commit(self, message: str) -> str:
        self.git("add", "-A")
        self.git("commit", "-q", "--allow-empty", "-m", message)
        return self.git("rev-parse", "HEAD")

    def close(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)


FOO_TESTS = """
    namespace Demo.Tests;

    public class FooTests
    {
        [Fact]
        public void One() { }

        [Fact]
        public void Two() { }

        [Theory]
        [InlineData(1)]
        public void Three(int x) { }
    }
"""

GUARD_TARGETS = """
    namespace Demo.Tests.Guards;

    // 대상 목록. 주석 안의 new("x", "y")는 세지 않는다.
    internal static class GuardTargets
    {
        public static readonly GuardTarget[] All =
        [
            new("t1", "s1", Run),
            new("t2", "s2", Run),
        ];

        public static readonly Plan P = new(Seed: 7, Name: "rows.csv");

        private static void Run(string a, string b) { }
    }
"""


class GuardRepoTestCase(unittest.TestCase):
    """base 커밋 하나를 만든 저장소에서 head를 바꿔 본다."""

    def setUp(self) -> None:
        self.repo = TempRepo()
        r = self.repo
        r.write("tests/Demo.Tests/Demo.Tests.csproj", "<Project />\n")
        r.write("tests/Demo.Tests/FooTests.cs", FOO_TESTS)
        r.write("tests/Demo.Tests/Guards/GuardTargets.cs", GUARD_TARGETS)
        r.write("tests/Scorecard.Tests/TestData/guard/s1/case.yaml", "name: s1\n")
        r.write("tests/Scorecard.Tests/TestData/guard/s2/case.yaml", "name: s2\n")
        r.write("src/Demo.Core/Demo.Core.csproj", "<Project />\n")
        r.write("src/Demo.Core/Foo.cs", "namespace Demo; public static class Foo { public static int F(int x) => x + 1; }\n")
        r.write("examples/ex/case.yaml", "name: ex\n")
        r.write("examples/ex/expected/classification.json", '{\n  "schema_version": 1\n}\n')
        self.base = r.commit("base")
        self.tmp = tempfile.mkdtemp(prefix="guard-art-")

    def tearDown(self) -> None:
        self.repo.close()
        shutil.rmtree(self.tmp, ignore_errors=True)

    # ---- 도우미

    def head(self) -> str:
        return self.repo.git("rev-parse", "HEAD")

    def mutation_file(self, files: list[dict] | None = None, status: str = "measured", base: str | None = None, head: str | None = None) -> str:
        if files is None:
            files = [self.mfile("src/Demo.Core/Foo.cs", head=(9, 1), base=(9, 1))]
        data = {"schema_version": 1, "status": status, "base": base or self.base, "head": head or self.head(), "files": files, "errors": []}
        path = os.path.join(self.tmp, "mutation.json")
        with open(path, "w", encoding="utf-8") as f:
            json.dump(data, f)
        return path

    @staticmethod
    def stats(killed: int, survived: int) -> dict:
        return {"killed": killed, "timeout": 0, "survived": survived, "no_coverage": 0, "compile_error": 0, "ignored": 0}

    def mfile(self, path: str, head: tuple[int, int] | None, base: tuple[int, int] | None, new: bool = False) -> dict:
        return {
            "path": path,
            "base_path": None if new else path,
            "head": None if head is None else self.stats(*head),
            "base": None if (base is None or new) else self.stats(*base),
        }

    def oracle_file(self, entries: dict[str, str], sha_of_head: bool = True, wrong_sha: bool = False, null_sha: bool = False) -> str:
        files = []
        for path, status in entries.items():
            entry = {"path": path, "status": status}
            if sha_of_head:
                import hashlib

                content = cigit.read_file(self.repo.root, "HEAD", path) or b""
                sha = hashlib.sha256(content).hexdigest()
                entry["expected_sha256"] = ("0" * 64) if wrong_sha else sha
                entry["oracle_sha256"] = sha if status == "match" else "f" * 64
                if null_sha:
                    entry["oracle_sha256"] = None
            files.append(entry)
        path = os.path.join(self.tmp, "oracle.json")
        with open(path, "w", encoding="utf-8") as f:
            json.dump({"files": files}, f)
        return path

    def run_guard(self, mutation_path: str | None = None, oracle_path: str | None = None, base: str | None = "BASE", **kwargs) -> rg.Result:
        return rg.check(self.repo.root, self.base if base == "BASE" else base, "HEAD", mutation_path, oracle_path, **kwargs)

    def assertRule(self, result: rg.Result, level: str, rule: str, contains: str = "") -> None:  # noqa: N802
        hits = [f for f in result.findings if f.level == level and f.rule == rule and contains in f.message]
        self.assertTrue(hits, f"{level} {rule} '{contains}' 없음:\n" + "\n".join(rg.render_lines(result)))

    def assertNoFail(self, result: rg.Result) -> None:  # noqa: N802
        self.assertTrue(result.ok, "\n".join(rg.render_lines(result)))

    def assertFail(self, result: rg.Result) -> None:  # noqa: N802
        self.assertFalse(result.ok, "\n".join(rg.render_lines(result)))


class TestBasics(GuardRepoTestCase):
    def test_no_change_passes(self) -> None:
        self.repo.commit("빈 커밋")
        result = self.run_guard()
        self.assertNoFail(result)
        self.assertRule(result, "PASS", "tests")
        self.assertRule(result, "PASS", "expected")

    def test_src_only_change_passes_without_evidence(self) -> None:
        self.repo.replace("src/Demo.Core/Foo.cs", "x + 1", "x + 2")
        self.repo.commit("src만")
        self.assertNoFail(self.run_guard())

    def test_empty_base_falls_back_to_first_parent(self) -> None:
        self.repo.write("tests/Demo.Tests/NewTests.cs", "public class N { [Fact] public void A() { } }\n")
        first = self.repo.commit("하나")
        self.repo.remove("tests/Demo.Tests/NewTests.cs")
        self.repo.commit("둘: 삭제")
        result = rg.check(self.repo.root, "", "HEAD", None, None)
        self.assertEqual(result.range.base, first)
        self.assertRule(result, "INFO", "range", "첫 부모")
        self.assertRule(result, "FAIL", "test-file-deleted", "NewTests.cs")
        zero = rg.check(self.repo.root, cigit.ZERO_SHA, "HEAD", None, None)
        self.assertEqual(zero.range.base, first)

    def test_main_exit_codes(self) -> None:
        self.repo.commit("빈 커밋")
        out = os.path.join(self.tmp, "guard.json")
        summary = os.path.join(self.tmp, "summary.md")
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            code = rg.main(["--repo", self.repo.root, "--base", self.base, "--json-out", out, "--summary", summary])
        self.assertEqual(code, 0)
        with open(out, encoding="utf-8") as f:
            self.assertTrue(json.load(f)["ok"])
        with open(summary, encoding="utf-8") as f:
            self.assertIn("regression-guard: 통과", f.read())
        self.repo.remove("tests/Demo.Tests/FooTests.cs")
        self.repo.commit("삭제")
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(rg.main(["--repo", self.repo.root, "--base", self.base, "--summary", ""]), 1)
            self.assertEqual(rg.main(["--repo", self.repo.root, "--base", self.base, "--head", "없는-커밋", "--summary", ""]), 2)


class TestDeletion(GuardRepoTestCase):
    def test_deleted_test_file_fails_without_evidence(self) -> None:
        self.repo.remove("tests/Demo.Tests/FooTests.cs")
        self.repo.commit("테스트 삭제")
        result = self.run_guard()
        self.assertFail(result)
        self.assertRule(result, "FAIL", "test-file-deleted", "FooTests.cs")
        self.assertRule(result, "FAIL", "test-count-decreased", "3 -> 0")
        self.assertRule(result, "FAIL", "mutation-evidence", "mutation.json을 쓸 수 없다")

    def test_deleted_non_cs_test_file_fails(self) -> None:
        self.repo.write("tests/Scorecard.Tests/TestData/guard/s1/extra.csv", "a\n")
        self.base = self.repo.commit("자료 더함")
        self.repo.remove("tests/Scorecard.Tests/TestData/guard/s1/extra.csv")
        self.repo.commit("자료 삭제")
        self.assertRule(self.run_guard(), "FAIL", "test-file-deleted", "extra.csv")

    def test_deleted_test_file_passes_with_equal_mutation_score(self) -> None:
        self.repo.remove("tests/Demo.Tests/FooTests.cs")
        self.repo.replace("src/Demo.Core/Foo.cs", "x + 1", "x + 2")
        self.repo.commit("테스트 삭제 + src")
        result = self.run_guard(self.mutation_file())
        self.assertNoFail(result)
        self.assertRule(result, "WARN", "test-file-deleted", "mutation 증거로 통과")
        self.assertRule(result, "PASS", "mutation-evidence", "합계")
        self.assertRule(result, "WARN", "status-note", "STATUS.md")

    def test_lower_mutation_score_fails(self) -> None:
        self.repo.remove("tests/Demo.Tests/FooTests.cs")
        self.repo.commit("삭제")
        path = self.mutation_file([self.mfile("src/Demo.Core/Foo.cs", head=(8, 2), base=(9, 1))])
        result = self.run_guard(path)
        self.assertFail(result)
        self.assertRule(result, "FAIL", "mutation-evidence", "head 80.00% < base 90.00%")

    def test_one_file_lower_fails_even_if_total_higher(self) -> None:
        self.repo.remove("tests/Demo.Tests/FooTests.cs")
        self.repo.commit("삭제")
        path = self.mutation_file([
            self.mfile("src/Demo.Core/A.cs", head=(100, 0), base=(50, 50)),
            self.mfile("src/Demo.Core/B.cs", head=(8, 2), base=(9, 1)),
        ])
        self.assertRule(self.run_guard(path), "FAIL", "mutation-evidence", "B.cs: head 80.00% < base 90.00%")

    def test_not_applicable_mutation_fails(self) -> None:
        self.repo.remove("tests/Demo.Tests/FooTests.cs")
        self.repo.commit("삭제")
        path = self.mutation_file([], status="not_applicable")
        self.assertRule(self.run_guard(path), "FAIL", "mutation-evidence", "바뀐 src 파일이 없어")

    def test_new_src_file_only_is_not_evidence(self) -> None:
        self.repo.remove("tests/Demo.Tests/FooTests.cs")
        self.repo.commit("삭제")
        path = self.mutation_file([self.mfile("src/Demo.Core/New.cs", head=(10, 0), base=None, new=True)])
        result = self.run_guard(path)
        self.assertFail(result)
        self.assertRule(result, "FAIL", "mutation-evidence", "비교한 src 파일이 하나도 없다")

    def test_mutation_error_or_other_range_fails(self) -> None:
        self.repo.remove("tests/Demo.Tests/FooTests.cs")
        self.repo.commit("삭제")
        self.assertRule(self.run_guard(self.mutation_file(status="error")), "FAIL", "mutation-evidence", "status=error")
        other = self.mutation_file(base="1" * 40)
        self.assertRule(self.run_guard(other), "FAIL", "mutation-evidence", "범위가 다르다")

    def test_head_score_missing_fails(self) -> None:
        self.repo.remove("tests/Demo.Tests/FooTests.cs")
        self.repo.commit("삭제")
        path = self.mutation_file([self.mfile("src/Demo.Core/Foo.cs", head=(0, 0), base=(9, 1))])
        self.assertRule(self.run_guard(path), "FAIL", "mutation-evidence", "비교할 수 없다")

    def test_rename_is_not_deletion(self) -> None:
        os.makedirs(os.path.join(self.repo.root, "tests/Demo.Tests/Renamed"))
        self.repo.git("mv", "tests/Demo.Tests/FooTests.cs", "tests/Demo.Tests/Renamed/FooTests.cs")
        self.repo.commit("이름 변경")
        result = self.run_guard()
        self.assertNoFail(result)
        self.assertRule(result, "INFO", "test-file-renamed", "Renamed/FooTests.cs")


class TestSkip(GuardRepoTestCase):
    def assert_skip(self, old: str, new: str, kind: str) -> None:
        self.repo.replace("tests/Demo.Tests/FooTests.cs", old, new)
        self.repo.commit("Skip")
        result = self.run_guard()
        self.assertFail(result)
        self.assertRule(result, "FAIL", "skip-added", kind)

    def test_fact_skip_named_argument(self) -> None:
        self.assert_skip("[Fact]\n    public void One()", '[Fact(Skip = "나중에")]\n    public void One()', "Skip=")

    def test_fact_skip_without_spaces(self) -> None:
        self.assert_skip("[Fact]\n    public void One()", '[Fact(Skip="x")]\n    public void One()', "Skip=")

    def test_inline_data_skip(self) -> None:
        self.assert_skip("[InlineData(1)]", '[InlineData(1, Skip = "x")]', "Skip=")

    def test_trait_category_skip(self) -> None:
        self.assert_skip("[Fact]\n    public void One()", '[Fact, Trait("Category", "Skip")]\n    public void One()', "Trait(skip)")

    def test_trait_on_its_own_line(self) -> None:
        self.assert_skip("[Fact]\n    public void One()", '[Fact]\n    [Trait("Category","Skipped")]\n    public void One()', "Trait(skip)")

    def test_explicit_true(self) -> None:
        self.assert_skip("[Fact]\n    public void One()", "[Fact(Explicit = true)]\n    public void One()", "Explicit=")

    def test_assert_skip_call(self) -> None:
        self.assert_skip("public void Two() { }", 'public void Two() { Assert.Skip("x"); }', "Assert.Skip")

    def test_skip_when(self) -> None:
        self.assert_skip("[Fact]\n    public void One()", '[Fact(SkipWhen = nameof(X))]\n    public void One()', "SkipWhen=")

    def test_if_false_wrapping(self) -> None:
        self.repo.replace("tests/Demo.Tests/FooTests.cs", "    [Fact]\n    public void One() { }\n", "#if false\n    [Fact]\n    public void One() { }\n#endif\n")
        self.repo.commit("#if false")
        result = self.run_guard()
        self.assertFail(result)
        self.assertRule(result, "FAIL", "skip-added", "#if")
        self.assertRule(result, "FAIL", "test-count-decreased", "3 -> 2")

    def test_if_undefined_symbol_is_skip(self) -> None:
        self.repo.replace("tests/Demo.Tests/FooTests.cs", "    [Fact]\n    public void One() { }\n", "#if NEVER\n    [Fact]\n    public void One() { }\n#endif\n")
        self.repo.commit("#if NEVER")
        result = self.run_guard()
        self.assertRule(result, "FAIL", "skip-added", "#if")

    def test_skip_with_evidence_passes(self) -> None:
        self.repo.replace("tests/Demo.Tests/FooTests.cs", "[Fact]\n    public void One()", '[Fact(Skip = "x")]\n    public void One()')
        self.repo.replace("src/Demo.Core/Foo.cs", "x + 1", "x + 2")
        self.repo.commit("Skip + src")
        result = self.run_guard(self.mutation_file())
        self.assertNoFail(result)
        self.assertRule(result, "WARN", "skip-added", "mutation 증거로 통과")

    def test_skip_words_in_comment_string_and_linq_do_not_count(self) -> None:
        self.repo.replace("tests/Demo.Tests/FooTests.cs", "public void Two() { }", textwrap.dedent('''\
            public void Two()
                {
                    // [Fact(Skip = "주석")] Assert.Skip("주석")
                    /* [Trait("Category", "Skip")] #if false */
                    var s = "[Fact(Skip = \\"문자열\\")] Assert.Skip(";
                    var v = @"Skip = ""x"" ";
                    var r = """
                        [Fact(Skip = "원시 문자열")]
                        #if false
                        """;
                    var n = new[] { 1, 2 }.Skip(1).ToArray();
                    var i = $"{n.Skip(1).Count()} Skip = {s}";
                }'''))
        self.repo.commit("Skip 글자만")
        self.assertNoFail(self.run_guard())

    def test_moving_skip_within_file_is_not_added(self) -> None:
        self.repo.replace("tests/Demo.Tests/FooTests.cs", "[Fact]\n    public void One()", '[Fact(Skip = "x")]\n    public void One()')
        self.base = self.repo.commit("Skip 있는 base")
        self.repo.replace("tests/Demo.Tests/FooTests.cs", '[Fact(Skip = "x")]\n    public void One()', "[Fact]\n    public void One()")
        self.repo.replace("tests/Demo.Tests/FooTests.cs", "[Fact]\n    public void Two()", '[Fact(Skip = "y")]\n    public void Two()')
        self.repo.commit("Skip 옮김")
        self.assertNoFail(self.run_guard())

    def test_existing_skip_in_renamed_file_is_not_added(self) -> None:
        self.repo.replace("tests/Demo.Tests/FooTests.cs", "[Fact]\n    public void One()", '[Fact(Skip = "x")]\n    public void One()')
        self.base = self.repo.commit("Skip 있는 base")
        self.repo.git("mv", "tests/Demo.Tests/FooTests.cs", "tests/Demo.Tests/BarTests.cs")
        self.repo.commit("이름 변경")
        self.assertNoFail(self.run_guard())


class TestFactCount(GuardRepoTestCase):
    def test_removed_fact_fails(self) -> None:
        self.repo.replace("tests/Demo.Tests/FooTests.cs", "    [Fact]\n    public void Two() { }\n", "")
        self.repo.commit("Fact 하나 뺌")
        result = self.run_guard()
        self.assertFail(result)
        self.assertRule(result, "FAIL", "test-count-decreased", "3 -> 2")
        self.assertRule(result, "FAIL", "test-count-decreased", "FooTests.cs -1")

    def test_commented_out_fact_fails(self) -> None:
        self.repo.replace("tests/Demo.Tests/FooTests.cs", "    [Fact]\n    public void Two() { }\n", "    // [Fact]\n    // public void Two() { }\n    /* [Fact] */\n")
        self.repo.commit("주석 처리")
        self.assertRule(self.run_guard(), "FAIL", "test-count-decreased", "3 -> 2")

    def test_fact_in_string_does_not_count(self) -> None:
        self.repo.replace("tests/Demo.Tests/FooTests.cs", "    [Fact]\n    public void Two() { }\n", '    private const string S = "[Fact]";\n    private const string T = @"[Theory]";\n')
        self.repo.commit("문자열로")
        self.assertRule(self.run_guard(), "FAIL", "test-count-decreased", "3 -> 2")

    def test_fact_to_theory_and_qualified_names_keep_count(self) -> None:
        text = self.repo.read("tests/Demo.Tests/FooTests.cs")
        text = text.replace("[Fact]\n    public void One()", "[Xunit.Theory, InlineData(1)]\n    public void One()", 1)
        text = text.replace("[Fact]\n    public void Two()", "[method: global::Xunit.FactAttribute]\n    public void Two()", 1)
        self.repo.write("tests/Demo.Tests/FooTests.cs", text)
        self.repo.commit("이름만 바꿈")
        result = self.run_guard()
        self.assertNoFail(result)
        self.assertRule(result, "INFO", "test-count", "3 -> 3")

    def test_moving_facts_between_files_keeps_count(self) -> None:
        self.repo.replace("tests/Demo.Tests/FooTests.cs", "    [Fact]\n    public void Two() { }\n", "")
        self.repo.write("tests/Demo.Tests/MovedTests.cs", "public class Moved\n{\n    [Fact]\n    public void Two() { }\n}\n")
        self.repo.commit("옮김")
        self.assertNoFail(self.run_guard())

    def test_more_facts_pass(self) -> None:
        self.repo.write("tests/Demo.Tests/MoreTests.cs", "public class More\n{\n    [Fact] public void A() { }\n    [Fact] public void B() { }\n}\n")
        self.repo.commit("더함")
        result = self.run_guard()
        self.assertNoFail(result)
        self.assertRule(result, "INFO", "test-count", "3 -> 5")


class TestGuardSamples(GuardRepoTestCase):
    def test_removed_target_fails(self) -> None:
        self.repo.replace("tests/Demo.Tests/Guards/GuardTargets.cs", '        new("t2", "s2", Run),\n', "")
        self.repo.commit("대상 뺌")
        result = self.run_guard()
        self.assertFail(result)
        self.assertRule(result, "FAIL", "guard-shrunk", "대상 2 -> 1 (빠진 이름: t2)")

    def test_commented_out_target_fails(self) -> None:
        self.repo.replace("tests/Demo.Tests/Guards/GuardTargets.cs", '        new("t2", "s2", Run),\n', '        // new("t2", "s2", Run),\n')
        self.repo.commit("대상 주석 처리")
        self.assertRule(self.run_guard(), "FAIL", "guard-shrunk", "대상 2 -> 1")

    def test_removed_sample_folder_fails(self) -> None:
        self.repo.remove("tests/Scorecard.Tests/TestData/guard/s2")
        self.repo.commit("샘플 뺌")
        result = self.run_guard()
        self.assertFail(result)
        self.assertRule(result, "FAIL", "guard-shrunk", "샘플 폴더 2 -> 1 (빠진 폴더: s2)")

    def test_removed_guard_targets_file_fails(self) -> None:
        self.repo.remove("tests/Demo.Tests/Guards/GuardTargets.cs")
        self.repo.commit("목록 파일 삭제")
        self.assertRule(self.run_guard(), "FAIL", "guard-shrunk", "대상 2 -> 0")

    def test_added_or_renamed_target_passes(self) -> None:
        self.repo.replace("tests/Demo.Tests/Guards/GuardTargets.cs", '        new("t2", "s2", Run),\n', '        new("t2b", "s2", Run),\n        new("t3", "s1", Run),\n')
        self.repo.commit("대상 이름 바꿈·더함")
        result = self.run_guard()
        self.assertNoFail(result)
        self.assertRule(result, "INFO", "guard-targets", "2 -> 3 (이름이 바뀜: t2)")

    def test_guard_shrink_with_evidence_passes(self) -> None:
        self.repo.replace("tests/Demo.Tests/Guards/GuardTargets.cs", '        new("t2", "s2", Run),\n', "")
        self.repo.replace("src/Demo.Core/Foo.cs", "x + 1", "x + 2")
        self.repo.commit("대상 뺌 + src")
        self.assertNoFail(self.run_guard(self.mutation_file()))


class TestExpected(GuardRepoTestCase):
    PATH = "examples/ex/expected/classification.json"

    def change_expected(self, message: str) -> None:
        self.repo.replace(self.PATH, '"schema_version": 1', '"schema_version": 1,\n  "case": "ex"')
        self.repo.commit(message)

    def test_migration_message_and_oracle_match_pass(self) -> None:
        self.change_expected("expected migration: case 키 추가")
        result = self.run_guard(oracle_path=self.oracle_file({self.PATH: "match"}))
        self.assertNoFail(result)
        self.assertRule(result, "PASS", "expected-changed", "oracle match")

    def test_other_message_fails(self) -> None:
        self.change_expected("T-099: expected 고침")
        result = self.run_guard(oracle_path=self.oracle_file({self.PATH: "match"}))
        self.assertFail(result)
        self.assertRule(result, "FAIL", "expected-changed", "T-099")

    def test_message_must_start_with_prefix(self) -> None:
        self.change_expected("fix: expected migration: 앞에 다른 글자")
        self.assertFail(self.run_guard(oracle_path=self.oracle_file({self.PATH: "match"})))

    def test_oracle_diff_fails(self) -> None:
        self.change_expected("expected migration: case 키 추가")
        result = self.run_guard(oracle_path=self.oracle_file({self.PATH: "diff"}))
        self.assertFail(result)
        self.assertRule(result, "FAIL", "expected-changed", "oracle status=diff")

    def test_oracle_missing_or_without_file_fails(self) -> None:
        self.change_expected("expected migration: case 키 추가")
        self.assertRule(self.run_guard(oracle_path=os.path.join(self.tmp, "none.json")), "FAIL", "expected-changed", "oracle.json을 쓸 수 없다")
        self.assertRule(self.run_guard(oracle_path=self.oracle_file({"examples/other/expected/x.json": "match"})), "FAIL", "expected-changed", "oracle.json에 이 파일이 없다")

    def test_oracle_sha_must_be_head_file(self) -> None:
        self.change_expected("expected migration: case 키 추가")
        result = self.run_guard(oracle_path=self.oracle_file({self.PATH: "match"}, wrong_sha=True))
        self.assertRule(result, "FAIL", "expected-changed", "expected_sha256")

    def test_two_commits_one_bad_fails(self) -> None:
        self.change_expected("expected migration: 첫째")
        self.repo.replace(self.PATH, '"case": "ex"', '"case": "ex2"')
        self.repo.commit("둘째: 메시지 없음")
        result = self.run_guard(oracle_path=self.oracle_file({self.PATH: "match"}))
        self.assertFail(result)
        self.assertRule(result, "FAIL", "expected-changed", "둘째")

    def test_unrelated_commits_need_no_prefix(self) -> None:
        self.change_expected("expected migration: case 키 추가")
        self.repo.replace("src/Demo.Core/Foo.cs", "x + 1", "x + 2")
        self.repo.commit("T-099: src 고침")
        self.assertNoFail(self.run_guard(oracle_path=self.oracle_file({self.PATH: "match"})))

    def test_deleted_expected_fails_even_with_message(self) -> None:
        self.repo.remove(self.PATH)
        self.repo.commit("expected migration: 지움")
        result = self.run_guard(oracle_path=self.oracle_file({}))
        self.assertFail(result)
        self.assertRule(result, "FAIL", "expected-deleted", self.PATH)

    def test_renamed_expected_is_deleted(self) -> None:
        self.repo.git("mv", self.PATH, "examples/ex/expected/renamed.json")
        self.repo.commit("expected migration: 이름 변경")
        self.assertRule(self.run_guard(oracle_path=self.oracle_file({"examples/ex/expected/renamed.json": "match"})), "FAIL", "expected-deleted", self.PATH)

    def test_new_expected_needs_oracle_match_only(self) -> None:
        new = "examples/ex/expected/metrics.json"
        self.repo.write(new, '{\n  "schema_version": 1\n}\n')
        self.repo.commit("T-099: 새 expected")
        self.assertRule(self.run_guard(oracle_path=self.oracle_file({new: "match"})), "PASS", "expected-added", new)
        self.assertRule(self.run_guard(oracle_path=self.oracle_file({new: "missing"})), "FAIL", "expected-added", "status=missing")
        self.assertRule(self.run_guard(), "FAIL", "expected-added", "oracle.json을 쓸 수 없다")

    def test_non_expected_example_file_is_free(self) -> None:
        self.repo.replace("examples/ex/case.yaml", "ex", "ex-2")
        self.repo.commit("case.yaml 고침")
        self.assertNoFail(self.run_guard())

    def test_pr_merge_commit_is_not_counted(self) -> None:
        # PR: 기능 브랜치가 expected migration 커밋을 하고, main이 앞으로 간 뒤 GitHub가 병합 커밋을 만든다.
        r = self.repo
        r.git("checkout", "-q", "-b", "feature")
        self.change_expected("expected migration: case 키 추가")
        r.git("checkout", "-q", "main")
        r.replace("src/Demo.Core/Foo.cs", "x + 1", "x + 3")
        main_tip = r.commit("main: 다른 카드")
        r.git("merge", "-q", "--no-ff", "-m", "Merge feature into main", "feature")
        self.base = main_tip
        self.assertNoFail(self.run_guard(oracle_path=self.oracle_file({self.PATH: "match"})))

    def test_merge_that_rewrites_expected_is_counted(self) -> None:
        r = self.repo
        r.git("checkout", "-q", "-b", "feature")
        self.change_expected("expected migration: case 키 추가")
        r.git("checkout", "-q", "main")
        main_tip = r.commit("main: 빈 커밋")
        r.git("merge", "-q", "--no-ff", "--no-commit", "feature")
        r.replace(self.PATH, '"case": "ex"', '"case": "병합 때 손댐"')
        r.commit("Merge feature into main")
        self.base = main_tip
        result = self.run_guard(oracle_path=self.oracle_file({self.PATH: "match"}))
        self.assertRule(result, "FAIL", "expected-changed", "Merge feature")


class TestCsharpScan(unittest.TestCase):
    def facts(self, src: str) -> int:
        return csharp_scan.analyze(textwrap.dedent(src)).fact_count

    def test_attribute_forms(self) -> None:
        src = """
            [assembly: CollectionBehavior(DisableTestParallelization = true)]
            namespace N;
            public class C
            {
                [Fact] public void A() { }
                [Fact, Trait("k", "v")] public void B() { }
                [Theory][InlineData(1)][InlineData(2)] public void D(int x) { }
                [Xunit.Fact] public void E() { }
                [global::Xunit.TheoryAttribute, MemberData(nameof(X))] public void F(int x) { }
                [method: Fact] public void G() { }
                [Trait("Fact", "Theory")] public void H() { }
                public void I() { var a = new int[3]; var b = a[0]; var d = new Dictionary<string, int> { ["Fact"] = 1 }; }
            }
        """
        self.assertEqual(self.facts(src), 6)

    def test_comments_strings_and_inactive_regions(self) -> None:
        src = '''
            public class C
            {
                // [Fact]
                /* [Fact]
                   [Theory] */
                private const string S = "[Fact] \\" [Theory]";
                private const string V = @"[Fact] "" [Theory]";
                private const string R = """
                    [Fact]
                    """;
                private string I(int x) => $"{(x > 0 ? "[Fact]" : "[Theory]")} {{[Fact]}}";
                private const char Q = '"';
                private const char B = '\\'';
            #if false
                [Fact] public void Off() { }
            #elif true
                [Fact] public void On() { }
            #else
                [Fact] public void Off2() { }
            #endif
            #if DEBUG
                [Fact] public void Unknown() { }
            #endif
            #if !true || (false && SOMETHING)
                [Fact] public void Off3() { }
            #endif
            #define LOCAL
            #if LOCAL
                [Fact] public void Defined() { }
            #endif
            #if false
            #if true
                [Fact] public void NestedOff() { }
            #endif
            #endif
            }
        '''
        scan = csharp_scan.analyze(textwrap.dedent(src))
        self.assertEqual(scan.fact_count, 3, scan.facts)  # On, Unknown(DEBUG는 모름 → 센다), Defined
        self.assertEqual(scan.skip_kinds().get("#if"), 6)

    def test_skip_markers(self) -> None:
        src = """
            public class C
            {
                [Fact(Skip = "a")] public void A() { }
                [Theory, InlineData(1, Skip="b")] public void B(int x) { }
                [SkippableFact] public void D() { Skip.If(true); Skip.IfNot(false); }
                [Fact(Explicit = true)] public void E() { Assert.Skip("x"); Assert.SkipWhen(true, "y"); }
                [Fact] public void F() { throw new SkipException("z"); }
                [Fact] public void G() { var n = list.Skip(1).Count(); var skip = 1; var Explicit = 2; }
                [Fact, Trait("Category", "Skip")] public void H() { }
                [Trait("kind", "slow")] public void I() { }
            }
        """
        kinds = csharp_scan.analyze(textwrap.dedent(src)).skip_kinds()
        self.assertEqual(kinds, {
            "Skip=": 2, "SkippableFact": 1, "Skip.If": 1, "Skip.IfNot": 1, "Explicit=": 1,
            "Assert.Skip": 1, "Assert.SkipWhen": 1, "SkipException": 1, "Trait(skip)": 1,
        })

    def test_guard_target_names(self) -> None:
        names = csharp_scan.analyze(textwrap.dedent(GUARD_TARGETS)).guard_names
        self.assertEqual(names, ["t1", "t2"])
        array_form = """
            internal static class GuardTargets
            {
                public static readonly GuardTarget[] All = new GuardTarget[]
                {
                    new GuardTarget("a", "s"),
                    new GuardTarget(Name: "b", Sample: "s", Run: X),
                };
            }
        """
        self.assertEqual(csharp_scan.analyze(textwrap.dedent(array_form)).guard_names, ["a", "b"])
        self.assertIsNone(csharp_scan.analyze("public class Other { }").guard_names)

    def test_real_guard_targets_file(self) -> None:
        path = repo_file("tests", "Scorecard.Tests", "Guards", "GuardTargets.cs")
        if path is None:
            self.skipTest("저장소 밖에서 돌림(GITHUB_WORKSPACE도 없음)")
        with open(path, encoding="utf-8") as f:
            names = csharp_scan.analyze(f.read()).guard_names
        self.assertTrue(names, "실제 GuardTargets.All에서 이름을 읽지 못했다")
        self.assertEqual(len(names), len(set(names)))


class TestMutationHelpers(unittest.TestCase):
    def setUp(self) -> None:
        self.repo = TempRepo()
        r = self.repo
        r.write("src/A/A.csproj", "<Project />\n")
        r.write("src/A/Keep.cs", "class Keep { }\n")
        r.write("src/A/Sub/Move.cs", "class Move { int x = 1; int y = 2; int z = 3; }\n")
        r.write("src/A/Gone.cs", "class Gone { }\n")
        r.write("src/B/B.csproj", "<Project />\n")
        r.write("src/B/Program.cs", "class P { }\n")
        r.write("tests/T.cs", "class T { }\n")
        self.base = r.commit("base")

    def tearDown(self) -> None:
        self.repo.close()

    def test_changed_src_files(self) -> None:
        r = self.repo
        r.replace("src/A/Keep.cs", "Keep", "Keep2")
        r.git("mv", "src/A/Sub/Move.cs", "src/A/Sub/Moved.cs")
        r.remove("src/A/Gone.cs")
        r.write("src/B/New.cs", "class N { }\n")
        r.write("src/B/notes.md", "메모\n")
        r.replace("tests/T.cs", "T", "T2")
        head = r.commit("변경")
        files = mutation.changed_src_files(r.root, self.base, head)
        self.assertEqual(files, [
            {"path": "src/A/Keep.cs", "base_path": "src/A/Keep.cs"},
            {"path": "src/A/Sub/Moved.cs", "base_path": "src/A/Sub/Move.cs"},
            {"path": "src/B/New.cs", "base_path": None},
        ])
        self.assertEqual(mutation.find_project(r.root, head, "src/A/Sub/Moved.cs"), "src/A/A.csproj")
        self.assertEqual(mutation.find_project(r.root, head, "src/B/New.cs"), "src/B/B.csproj")
        self.assertEqual(mutation.changed_src_files(r.root, head, head), [])

    def test_read_report_and_build_result(self) -> None:
        root = tempfile.mkdtemp(prefix="mut-report-")
        try:
            report = {
                "projectRoot": os.path.join(root, "src", "A"),
                "files": {
                    os.path.join(root, "src", "A", "Keep.cs"): {"mutants": [{"status": s} for s in ["Killed"] * 7 + ["Survived", "NoCoverage", "Timeout", "CompileError", "Ignored"]]},
                    os.path.join(root, "src", "A", "Other.cs"): {"mutants": [{"status": "Survived"}]},
                },
            }
            path = os.path.join(root, "report.json")
            with open(path, "w", encoding="utf-8") as f:
                json.dump(report, f)
            stats = mutation.read_report(path, root, ["src/A/Keep.cs", "src/A/Missing.cs"])
        finally:
            shutil.rmtree(root, ignore_errors=True)
        keep = stats["src/A/Keep.cs"]
        self.assertEqual((keep["killed"], keep["timeout"], keep["survived"], keep["no_coverage"], keep["compile_error"], keep["ignored"]), (7, 1, 1, 1, 1, 1))
        self.assertEqual(keep["score"], 80.0)
        self.assertIsNone(stats["src/A/Missing.cs"])

        rng = cigit.Range("b" * 40, "h" * 40, "시험")
        base_stats = mutation.score_stats(["Killed"] * 9 + ["Survived"])
        files = [
            {"path": "src/A/Keep.cs", "base_path": "src/A/Keep.cs", "project": "src/A/A.csproj", "head": keep, "base": base_stats},
            {"path": "src/B/New.cs", "base_path": None, "project": "src/B/B.csproj", "head": mutation.score_stats(["Killed"])},
        ]
        data = mutation.build_result(rng, files, [], [], "5.0.0")
        self.assertEqual(data["status"], "measured")
        self.assertEqual((data["head_score"], data["base_score"]), (80.0, 90.0))
        self.assertEqual(data["files"][1]["base_score"], None)
        self.assertEqual(data["files"][1]["head_score"], 100.0)
        self.assertIn("새 파일", mutation.render_summary(data))
        ok, reasons = rg.mutation_evidence(data, None, rng, "success")
        self.assertFalse(ok, reasons)

        empty = mutation.build_result(rng, [], [], [], "5.0.0")
        self.assertEqual(empty["status"], "not_applicable")
        self.assertIn("해당 없음", mutation.render_summary(empty))
        self.assertEqual(mutation.build_result(rng, files, ["실패"], [], "5.0.0")["status"], "error")

    def test_not_applicable_main_writes_json(self) -> None:
        r = self.repo
        r.replace("tests/T.cs", "T", "T2")
        r.commit("tests만")
        out = tempfile.mktemp(suffix=".json")
        try:
            with contextlib.redirect_stdout(io.StringIO()):
                code = mutation.main(["--repo", r.root, "--base", self.base, "--out", out, "--summary", ""])
            self.assertEqual(code, 0)
            with open(out, encoding="utf-8") as f:
                data = json.load(f)
        finally:
            if os.path.exists(out):
                os.remove(out)
        self.assertEqual(data["status"], "not_applicable")
        self.assertEqual(data["files"], [])
        self.assertEqual(data["base"], self.base)

    def test_run_failure_writes_error_json(self) -> None:
        # src가 바뀌었는데 tool manifest가 없으면 Stryker를 돌리지 못한다. 그래도 mutation.json(status: error)을 쓴다.
        r = self.repo
        r.replace("src/A/Keep.cs", "Keep", "Keep2")
        r.commit("src")
        out = tempfile.mktemp(suffix=".json")
        work = tempfile.mkdtemp(prefix="mut-work-")
        try:
            with contextlib.redirect_stdout(io.StringIO()):
                code = mutation.main(["--repo", r.root, "--base", self.base, "--out", out, "--work", work, "--summary", ""])
            with open(out, encoding="utf-8") as f:
                data = json.load(f)
        finally:
            if os.path.exists(out):
                os.remove(out)
            shutil.rmtree(work, ignore_errors=True)
        self.assertEqual(code, 1)
        self.assertEqual(data["status"], "error")
        self.assertIn("dotnet-tools.json", data["errors"][0])
        ok, reasons = rg.mutation_evidence(data, None, cigit.Range(data["base"], data["head"], ""), "failure")
        self.assertFalse(ok)
        self.assertIn("status=error", reasons[0])


STATUS_TEXT = """
    # STATUS

    ## 원칙 변경 (맨 위에 둔다)

    | 날짜 | ADR | 바뀐 것 | 결정한 사람 | PR |
    | --- | --- | --- | --- | --- |
    | 2026-11-07 | 0005 | 자동 진행 | juhan | T-010 |

    ## 머지된 카드

    | 카드 | PR |
    | --- | --- |
    | T-010 | #11 |
"""


class TestCiChanged(GuardRepoTestCase):
    """CI 도구 변경(ci-changed). base에 tools/ci/regression_guard.py가 있어야 적용된다."""

    def make_guarded_base(self) -> None:
        r = self.repo
        r.write("tools/ci/regression_guard.py", "# base guard\n")
        r.write("tools/oracle/check.py", "# base oracle\n")
        r.write(".github/workflows/ci.yml", "name: ci\n")
        r.write(".config/dotnet-tools.json", '{"tools": {}}\n')
        r.write("docs/STATUS.md", STATUS_TEXT)
        r.write("docs/decisions/0005-auto-mode.md", "# 0005\n")
        self.base = r.commit("도구가 있는 base")

    def add_adr_and_row(self, number: str = "0006", row_number: str | None = None, section: str = "원칙 변경") -> None:
        self.repo.write(f"docs/decisions/{number}-ci-change.md", f"# {number}\n")
        anchor = "| 2026-11-07 | 0005 | 자동 진행 | juhan | T-010 |\n" if section == "원칙 변경" else "| T-010 | #11 |\n"
        row = f"| 2026-11-08 | {row_number or number} | CI 바꿈 | 리드 | T-012 |\n" if section == "원칙 변경" else f"| T-012 {row_number or number} | #12 |\n"
        self.repo.replace("docs/STATUS.md", anchor, row + anchor)

    def test_bootstrap_without_base_guard_warns_only(self) -> None:
        self.repo.write(".github/workflows/ci.yml", "name: ci\n")
        self.repo.write("tools/ci/regression_guard.py", "# 새 guard\n")
        self.repo.commit("T-010: 도구 처음 넣음")
        result = self.run_guard()
        self.assertNoFail(result)
        self.assertRule(result, "WARN", "ci-changed", "부트스트랩")

    def test_workflow_change_without_adr_fails(self) -> None:
        self.make_guarded_base()
        self.repo.replace(".github/workflows/ci.yml", "name: ci", "name: ci\n# --filter 넣음")
        self.repo.commit("T-099: 워크플로 고침")
        result = self.run_guard()
        self.assertFail(result)
        self.assertRule(result, "FAIL", "ci-changed", "새 ADR 파일이 없다")
        self.assertRule(result, "FAIL", "ci-changed", "더한 행이 없다")

    def test_every_watched_path_fails(self) -> None:
        self.make_guarded_base()
        paths = {
            "tools/ci/regression_guard.py": "# 약하게 바꿈\n",
            "tools/oracle/check.py": "# 위조\n",
            ".config/dotnet-tools.json": '{"tools": {"x": {}}}\n',
            ".github/actions/a/action.yml": "runs: {}\n",
        }
        for path, text in paths.items():
            with self.subTest(path=path):
                self.repo.git("reset", "-q", "--hard", self.base)
                self.repo.write(path, text)
                self.repo.commit(f"T-099: {path}")
                self.assertRule(self.run_guard(), "FAIL", "ci-changed", path)

    def test_adr_and_status_row_pass(self) -> None:
        self.make_guarded_base()
        self.repo.replace("tools/ci/regression_guard.py", "# base guard", "# 고친 guard")
        self.add_adr_and_row("0006")
        self.repo.commit("T-012: guard 고침, ADR 0006")
        result = self.run_guard()
        self.assertNoFail(result)
        self.assertRule(result, "PASS", "ci-changed", "docs/decisions/0006-ci-change.md")

    def test_status_row_must_name_the_new_adr(self) -> None:
        self.make_guarded_base()
        self.repo.replace("tools/ci/regression_guard.py", "# base guard", "# 고친 guard")
        self.add_adr_and_row("0006", row_number="0007")
        self.repo.commit("T-012: 번호가 다른 행")
        self.assertRule(self.run_guard(), "FAIL", "ci-changed", "새 ADR 번호(0006)")

    def test_row_in_other_section_does_not_count(self) -> None:
        self.make_guarded_base()
        self.repo.replace("tools/ci/regression_guard.py", "# base guard", "# 고친 guard")
        self.add_adr_and_row("0006", section="머지된 카드")
        self.repo.commit("T-012: 다른 표에 행")
        self.assertRule(self.run_guard(), "FAIL", "ci-changed", "더한 행이 없다")

    def test_existing_adr_edit_is_not_new(self) -> None:
        self.make_guarded_base()
        self.repo.replace("tools/ci/regression_guard.py", "# base guard", "# 고친 guard")
        self.repo.replace("docs/decisions/0005-auto-mode.md", "# 0005", "# 0005 고침")
        self.repo.replace("docs/STATUS.md", "| 2026-11-07 | 0005 |", "| 2026-11-08 | 0005 | 다시 |\n| 2026-11-07 | 0005 |")
        self.repo.commit("T-012: 옛 ADR만 고침")
        self.assertRule(self.run_guard(), "FAIL", "ci-changed", "새 ADR 파일이 없다")

    def test_principle_rows_parse(self) -> None:
        rows = rg._principle_rows(textwrap.dedent(STATUS_TEXT))
        self.assertEqual(rows, ["| 2026-11-07 | 0005 | 자동 진행 | juhan | T-010 |"])


class TestToolsSource(GuardRepoTestCase):
    def setUp(self) -> None:
        super().setUp()
        self.tools = tempfile.mkdtemp(prefix="guard-tools-")
        for name, text in {"regression_guard.py": "# g\n", "cigit.py": "# c\n"}.items():
            with open(os.path.join(self.tools, name), "w", encoding="utf-8", newline="\n") as f:
                f.write(text)
            self.repo.write(f"tools/ci/{name}", text)
        self.base = self.repo.commit("tools/ci가 있는 base")

    def tearDown(self) -> None:
        shutil.rmtree(self.tools, ignore_errors=True)
        super().tearDown()

    def test_same_as_base(self) -> None:
        self.repo.commit("빈 커밋")
        result = self.run_guard(tools_dir=self.tools, require_base_tools=True)
        self.assertNoFail(result)
        self.assertRule(result, "INFO", "tools-source", "같다")

    def test_running_tools_differ_from_base(self) -> None:
        self.repo.commit("빈 커밋")
        with open(os.path.join(self.tools, "cigit.py"), "w", encoding="utf-8") as f:
            f.write("# head에서 바꾼 것\n")
        self.assertRule(self.run_guard(tools_dir=self.tools, require_base_tools=True), "FAIL", "tools-source", "cigit.py")
        self.assertRule(self.run_guard(tools_dir=self.tools), "WARN", "tools-source", "로컬 실행")

    def test_git_blob_id(self) -> None:
        self.assertEqual(rg.git_blob_id(b"# g\n"), cigit.blob_id(self.repo.root, "HEAD", "tools/ci/regression_guard.py"))


class TestTestInfra(GuardRepoTestCase):
    def test_infra_files_fail_without_evidence(self) -> None:
        cases = {
            "tests/Demo.Tests/Demo.Tests.csproj": '<Project><ItemGroup><Compile Remove="FooTests.cs" /></ItemGroup></Project>\n',
            "Directory.Build.props": "<Project />\n",
            "tests/Directory.Build.targets": "<Project />\n",
            "tests/Demo.Tests/test.runsettings": "<RunSettings />\n",
            "tests/Demo.Tests/xunit.runner.json": '{"methodDisplay": "method"}\n',
        }
        for path, text in cases.items():
            with self.subTest(path=path):
                self.repo.git("reset", "-q", "--hard", self.base)
                self.repo.write(path, text)
                self.repo.commit(f"T-099: {path}")
                result = self.run_guard()
                self.assertFail(result)
                self.assertRule(result, "FAIL", "test-infra-changed", path)

    def test_infra_change_with_evidence_passes(self) -> None:
        self.repo.write("Directory.Build.props", "<Project />\n")
        self.repo.replace("src/Demo.Core/Foo.cs", "x + 1", "x + 2")
        self.repo.commit("설정 + src")
        self.assertRule(self.run_guard(self.mutation_file()), "WARN", "test-infra-changed", "mutation 증거로 통과")

    def test_src_csproj_is_not_test_infra(self) -> None:
        self.repo.replace("src/Demo.Core/Demo.Core.csproj", "<Project />", "<Project Sdk=\"x\" />")
        self.repo.commit("src csproj")
        self.assertNoFail(self.run_guard())


class TestProjectScope(GuardRepoTestCase):
    def test_facts_outside_project_do_not_count(self) -> None:
        # 리뷰 재현 e1: 컴파일되는 파일에서 Fact를 빼고 프로젝트 밖 tests/Notes.cs에 같은 수를 더한다.
        self.repo.replace("tests/Demo.Tests/FooTests.cs", "    [Fact]\n    public void Two() { }\n", "")
        self.repo.write("tests/Notes.cs", "public class N { [Fact] public void A() { } }\n")
        self.repo.commit("밖으로 옮김")
        result = self.run_guard()
        self.assertFail(result)
        self.assertRule(result, "FAIL", "test-count-decreased", "3 -> 2")
        self.assertRule(result, "INFO", "test-count-outside", "tests/Notes.cs (1)")

    def test_moving_file_out_of_project_counts_as_decrease(self) -> None:
        self.repo.git("mv", "tests/Demo.Tests/FooTests.cs", "tests/FooTests.cs")
        self.repo.commit("프로젝트 밖으로")
        self.assertRule(self.run_guard(), "FAIL", "test-count-decreased", "3 -> 0")

    def test_obj_and_bin_do_not_count(self) -> None:
        self.repo.replace("tests/Demo.Tests/FooTests.cs", "    [Fact]\n    public void Two() { }\n", "")
        self.repo.write("tests/Demo.Tests/obj/Gen.cs", "public class G { [Fact] public void A() { } }\n")
        self.repo.commit("obj에 넣음")
        self.assertRule(self.run_guard(), "FAIL", "test-count-decreased", "3 -> 2")


class TestSkipForms(GuardRepoTestCase):
    def test_derived_fact_attribute_file(self) -> None:
        # 리뷰 재현 e3·e3b: 같은 이름공간에 FactAttribute를 상속해 Skip을 재정의한다.
        self.repo.write("tests/Demo.Tests/FactAttribute.cs", "namespace Demo.Tests;\n\npublic sealed class FactAttribute : Xunit.FactAttribute\n{\n    public override string Skip => \"임시\";\n}\n")
        self.repo.commit("가린 Fact")
        result = self.run_guard()
        self.assertFail(result)
        self.assertRule(result, "FAIL", "skip-added", "derived-attribute")
        self.assertRule(result, "FAIL", "skip-added", "shadow-attribute")
        self.assertRule(result, "FAIL", "skip-added", "Skip=>")

    def test_unicode_escape_in_skip(self) -> None:
        # 리뷰 재현 e4: [Fact(Skip = "…")]
        self.repo.replace("tests/Demo.Tests/FooTests.cs", "[Fact]\n    public void One()", '[Fact(Sk\\u0069p = "임시")]\n    public void One()')
        self.repo.commit("이스케이프")
        self.assertRule(self.run_guard(), "FAIL", "skip-added", "Skip=")

    def test_alias_attribute(self) -> None:
        self.repo.replace("tests/Demo.Tests/FooTests.cs", "namespace Demo.Tests;", "using F = Xunit.SkippableFactAttribute;\nnamespace Demo.Tests;")
        self.repo.commit("별칭")
        self.assertRule(self.run_guard(), "FAIL", "skip-added", "using-alias")


class TestOracleSource(GuardRepoTestCase):
    PATH = "examples/ex/expected/classification.json"

    def setUp(self) -> None:
        super().setUp()
        self.repo.write("tools/oracle/check.py", "# base oracle\n")
        self.base = self.repo.commit("oracle이 있는 base")

    def source_file(self, source: str = "base", rev: str | None = None, tree: str | None = None) -> str:
        rev = rev or (self.base if source == "base" else self.head())
        if tree is None:
            tree = self.repo.git("rev-parse", f"{rev}:tools/oracle")
        path = os.path.join(self.tmp, "oracle-source.json")
        with open(path, "w", encoding="utf-8") as f:
            json.dump({"schema_version": 1, "source": source, "rev": rev, "tree": tree}, f)
        return path

    def change_expected(self, message: str = "expected migration: case 키 추가") -> None:
        self.repo.replace(self.PATH, '"schema_version": 1', '"schema_version": 1,\n  "case": "ex"')
        self.repo.commit(message)

    def test_base_oracle_source_passes(self) -> None:
        self.change_expected()
        result = self.run_guard(oracle_path=self.oracle_file({self.PATH: "match"}), oracle_source_path=self.source_file())
        self.assertNoFail(result)
        self.assertRule(result, "INFO", "oracle-source", "base oracle")

    def test_missing_or_head_source_fails(self) -> None:
        self.change_expected()
        oracle = self.oracle_file({self.PATH: "match"})
        self.assertRule(self.run_guard(oracle_path=oracle), "FAIL", "oracle-source", "oracle-source.json이 없어")
        head = self.run_guard(oracle_path=oracle, oracle_source_path=self.source_file("head"))
        self.assertRule(head, "FAIL", "oracle-source", "base oracle에서 나오지 않았다")
        self.assertRule(head, "FAIL", "expected-changed", "증거로 쓸 수 없다")
        self.assertRule(self.run_guard(oracle_path=oracle, oracle_source_path=self.source_file(tree="0" * 40)), "FAIL", "oracle-source", "tree=")

    def test_oracle_and_expected_in_same_pr(self) -> None:
        # 리뷰 재현 e6·e6b: oracle을 위조하고 expected를 바꾼다. base oracle은 재현하지 못한다(diff).
        self.repo.replace("tools/oracle/check.py", "# base oracle", "# 위조: expected를 그대로 돌려준다")
        self.repo.commit("T-099: oracle 고침")
        self.change_expected()
        source = self.source_file()
        result = self.run_guard(oracle_path=self.oracle_file({self.PATH: "diff"}), oracle_source_path=source)
        self.assertFail(result)
        self.assertRule(result, "FAIL", "oracle-and-expected", "base oracle이 재현해야 한다")
        ok = self.run_guard(oracle_path=self.oracle_file({self.PATH: "match"}), oracle_source_path=source)
        self.assertNoFail(ok)
        self.assertRule(ok, "PASS", "oracle-and-expected", "base oracle이 head expected를 재현했다")

    def test_null_sha_fails(self) -> None:
        self.change_expected()
        result = self.run_guard(oracle_path=self.oracle_file({self.PATH: "match"}, null_sha=True), oracle_source_path=self.source_file())
        self.assertRule(result, "FAIL", "expected-changed", "null")


class TestOracleBootstrap(GuardRepoTestCase):
    PATH = "examples/ex/expected/classification.json"

    def test_new_oracle_with_expected_change_fails(self) -> None:
        self.repo.write("tools/oracle/check.py", "# 처음 넣는 oracle\n")
        self.repo.commit("T-099: oracle 넣음")
        self.repo.replace(self.PATH, '"schema_version": 1', '"schema_version": 2')
        self.repo.commit("expected migration: 같이 바꿈")
        result = self.run_guard(oracle_path=self.oracle_file({self.PATH: "match"}))
        self.assertFail(result)
        self.assertRule(result, "FAIL", "oracle-and-expected", "base에 oracle이 없어")


class TestMigrationScope(GuardRepoTestCase):
    PATH = "examples/ex/expected/classification.json"

    def test_migration_commit_touching_src_fails(self) -> None:
        self.repo.replace(self.PATH, '"schema_version": 1', '"schema_version": 1,\n  "case": "ex"')
        self.repo.replace("src/Demo.Core/Foo.cs", "x + 1", "x + 2")
        self.repo.commit("expected migration: 엔진도 같이")
        result = self.run_guard(oracle_path=self.oracle_file({self.PATH: "match"}))
        self.assertFail(result)
        self.assertRule(result, "FAIL", "migration-commit-scope", "src/Demo.Core/Foo.cs")

    SPEC = "docs/spec/classification.md"
    SPEC_TEXT = (
        "# 스펙\n\n규칙 글.\n\n`examples/ex/expected/classification.json` (SHA-256 `" + "a" * 64 + "`):\n\n"
        "```json\n{\n  \"schema_version\": 1\n}\n```\n\n```text\n설명 블록\n```\n\n끝 글.\n"
    )

    def spec_base(self) -> None:
        self.repo.write(self.SPEC, self.SPEC_TEXT)
        self.base = self.repo.commit("스펙이 있는 base")

    def migrate(self, new_spec: str) -> rg.Result:
        self.repo.replace(self.PATH, '"schema_version": 1', '"schema_version": 1,\n  "case": "ex"')
        self.repo.write(self.SPEC, new_spec)
        self.repo.commit("expected migration: 스펙 블록과 같이")
        return self.run_guard(oracle_path=self.oracle_file({self.PATH: "match"}))

    def test_migration_commit_with_spec_block_and_sha_passes(self) -> None:
        self.spec_base()
        new = self.SPEC_TEXT.replace("a" * 64, "b" * 64).replace('  "schema_version": 1\n}', '  "schema_version": 1,\n  "case": "ex"\n}')
        result = self.migrate(new)
        self.assertNoFail(result)
        self.assertRule(result, "PASS", "migration-commit-scope", "블록·SHA-256")

    def test_migration_commit_spec_prose_fails(self) -> None:
        # 리뷰 n7 같은 스펙 글(표) 변경을 expected migration 커밋에 섞지 못한다.
        cases = {
            "글": self.SPEC_TEXT.replace("규칙 글.", "규칙 글을 바꿈."),
            "SHA 줄의 파일 이름": self.SPEC_TEXT.replace("`examples/ex/expected/classification.json`", "`examples/ex/expected/metrics.json`"),
            "text 블록": self.SPEC_TEXT.replace("설명 블록", "설명 블록 고침"),
            "울타리 언어": self.SPEC_TEXT.replace("```json", "```text"),
            "블록 밖으로 끌어냄": self.SPEC_TEXT.replace('  "schema_version": 1\n}\n```', '  "schema_version": 1\n}\n```\n새 글'),
        }
        self.spec_base()
        for name, text in cases.items():
            with self.subTest(name=name):
                self.repo.git("reset", "-q", "--hard", self.base)
                result = self.migrate(text)
                self.assertFail(result)
                self.assertRule(result, "FAIL", "migration-commit-scope", "블록 밖 글이 바뀌었다")

    def test_migration_commit_new_spec_file_fails(self) -> None:
        result = self.migrate("블록\n")
        self.assertRule(result, "FAIL", "migration-commit-scope", "새 스펙 파일이다")

    def test_spec_block_lines(self) -> None:
        lines = ["글", "```json", "{", "```", "~~~yaml", "a: 1", "~~~", "````csv", "a,b", "```", "````", "```", "x", "```"]
        self.assertEqual(rg.spec_block_lines(lines),
                         [False, True, True, True, True, True, True, True, True, True, True, False, False, False])
        self.assertEqual(rg.spec_diff_problems(b"a\n", b"a\n"), [])
        self.assertEqual(rg.spec_diff_problems(b"x " + b"1" * 64 + b"\n", b"x " + b"2" * 64 + b"\n"), [])
        self.assertTrue(rg.spec_diff_problems(b"x " + b"1" * 64 + b"\n", b"y " + b"2" * 64 + b"\n"))

    def test_migration_commit_without_expected_is_also_checked(self) -> None:
        self.repo.replace("tests/Demo.Tests/FooTests.cs", "One", "OneRenamed")
        self.repo.commit("expected migration: 사실은 테스트 고침")
        self.assertRule(self.run_guard(), "FAIL", "migration-commit-scope", "FooTests.cs")


class TestTestRun(GuardRepoTestCase):
    def make_testrun(self, base_names: list[str], head_names: list[str], base_skipped: list[str] | None = None,
                     head_skipped: list[str] | None = None, status: str = "measured", base: str | None = None,
                     head_status: str = "ok", head_failed: list[str] | None = None, base_failed: list[str] | None = None) -> str:
        def side(names: list[str], skipped: list[str], st: str = "ok", failed: list[str] | None = None) -> dict:
            failed = failed or []
            return {"status": st, "total": len(names) + len(skipped) + len(failed), "passed": len(names), "failed": len(failed),
                    "skipped": len(skipped), "passed_names": sorted(names), "failed_names": sorted(failed), "skipped_names": sorted(skipped)}
        data = {"schema_version": 1, "status": status, "base": base or self.base, "head": self.head(), "errors": [] if status == "measured" else ["빌드 실패"],
                "sides": {"base": side(base_names, base_skipped or [], "ok", base_failed),
                          "head": side(head_names, head_skipped or [], head_status, head_failed)}}
        path = os.path.join(self.tmp, "testrun.json")
        with open(path, "w", encoding="utf-8") as f:
            json.dump(data, f)
        return path

    def test_no_testrun_warns(self) -> None:
        self.repo.commit("빈 커밋")
        result = self.run_guard()
        self.assertNoFail(result)
        self.assertRule(result, "WARN", "test-run", "--testrun이 없어")

    def test_superset_passes(self) -> None:
        self.repo.commit("빈 커밋")
        result = self.run_guard(testrun_path=self.make_testrun(["a", "b"], ["a", "b", "c"]))
        self.assertNoFail(result)
        self.assertRule(result, "INFO", "test-run", "전체 2 -> 3")

    def test_missing_name_is_reported_by_name(self) -> None:
        # 리뷰 재현 e2·e5: 빌드·실행에서 테스트가 빠진다(소스 셈은 그대로).
        self.repo.commit("빈 커밋")
        result = self.run_guard(testrun_path=self.make_testrun(["T.a", "T.b"], ["T.a", "T.c"]))
        self.assertFail(result)
        self.assertRule(result, "FAIL", "test-run-missing", "T.b")

    def test_duplicate_name_shrink(self) -> None:
        self.repo.commit("빈 커밋")
        result = self.run_guard(testrun_path=self.make_testrun(["a", "a", "a"], ["a", "x", "y"]))
        self.assertRule(result, "FAIL", "test-run-missing", "a (x2)")

    def test_skip_increase_and_total_decrease(self) -> None:
        self.repo.commit("빈 커밋")
        skipped = self.run_guard(testrun_path=self.make_testrun(["a", "b"], ["a", "b"], head_skipped=["c"]))
        self.assertRule(skipped, "FAIL", "test-run-skipped", "0 -> 1 (c)")
        fewer = self.run_guard(testrun_path=self.make_testrun(["a", "b"], ["a"], base_skipped=["s"], head_skipped=["s"]))
        self.assertRule(fewer, "FAIL", "test-run-total-decreased", "3 -> 2")

    def test_many_missing_names_are_summarized(self) -> None:
        self.repo.commit("빈 커밋")
        base = [f"T.n{i:02d}" for i in range(60)]
        result = self.run_guard(testrun_path=self.make_testrun(base, []))
        lines = [f for f in result.findings if f.rule == "test-run-missing"]
        self.assertEqual(len(lines), rg.MISSING_NAMES_SHOWN + 1)
        self.assertRule(result, "FAIL", "test-run-missing", "그 밖에 10개")

    def test_missing_name_with_mutation_evidence_passes(self) -> None:
        self.repo.replace("src/Demo.Core/Foo.cs", "x + 1", "x + 2")
        self.repo.commit("src")
        result = self.run_guard(self.mutation_file(), testrun_path=self.make_testrun(["a", "b"], ["a"]))
        self.assertNoFail(result)
        self.assertRule(result, "WARN", "test-run-missing", "mutation 증거로 통과")
        self.assertRule(result, "WARN", "status-note", "STATUS.md")

    def test_unusable_testrun_fails(self) -> None:
        self.repo.commit("빈 커밋")
        self.assertRule(self.run_guard(testrun_path=os.path.join(self.tmp, "none.json")), "FAIL", "test-run", "파일이 없다")
        self.assertRule(self.run_guard(testrun_path=self.make_testrun(["a"], ["a"], base="1" * 40)), "FAIL", "test-run", "범위가 다르다")
        self.assertRule(self.run_guard(testrun_path=self.make_testrun(["a"], ["a"], status="error")), "FAIL", "test-run", "status=error")
        self.assertRule(self.run_guard(testrun_path=self.make_testrun(["a"], ["a"], head_status="error")), "FAIL", "test-run", "head test-run 결과가 없다")

    def test_head_failure_fails_even_with_evidence(self) -> None:
        # 리뷰 재현 n3a·n3b: head 테스트가 실패했는데 mutation 증거로 통과했다.
        self.repo.replace("src/Demo.Core/Foo.cs", "x + 1", "x + 2")
        self.repo.commit("src")
        result = self.run_guard(self.mutation_file(), testrun_path=self.make_testrun(["a", "b"], ["a"], head_failed=["b"]))
        self.assertFail(result)
        self.assertRule(result, "FAIL", "test-run-failed", "1개")
        self.assertRule(result, "FAIL", "test-run-failed", ": b")

    def test_base_failure_warns(self) -> None:
        self.repo.commit("빈 커밋")
        result = self.run_guard(testrun_path=self.make_testrun(["a"], ["a", "b"], base_failed=["b"]))
        self.assertNoFail(result)
        self.assertRule(result, "WARN", "test-run-base-failed", "1개")

    # ---- 이름 변경 출구(STATUS.md "잠정 결정")

    RENAMED = (["N.C.M(x: 1)", "N.C.M(x: 2)", "N.D.K"], ["N.C.M(x: 1)", "N.C.M(x: 3)", "N.D.K"])

    def provisional_base(self) -> None:
        self.repo.write("docs/STATUS.md", PROVISIONAL_STATUS)
        self.base = self.repo.commit("STATUS가 있는 base")

    def add_provisional_row(self, card: str) -> None:
        self.repo.write("docs/STATUS.md", textwrap.dedent(PROVISIONAL_STATUS).lstrip("\n") + f"| 2026-11-08 | {card} | 표 행의 답 순서 | 이름 변경을 받아들임 | #99 |\n")

    def test_rename_with_provisional_row_warns(self) -> None:
        # 리뷰 재현 n7: 스펙 표 행이 바뀌어 Theory 이름이 바뀐다. 클래스별 통과 수는 그대로다.
        self.provisional_base()
        self.add_provisional_row("T-099")
        self.repo.commit("T-099: 6.1 표 행의 답 순서")
        result = self.run_guard(testrun_path=self.make_testrun(*self.RENAMED))
        self.assertNoFail(result)
        self.assertRule(result, "WARN", "test-run-renamed", "N.C.M(x: 2)")
        self.assertFalse([f for f in result.findings if f.rule == "test-run-missing"])

    def test_rename_without_row_fails_with_hint(self) -> None:
        self.provisional_base()
        self.repo.commit("T-099: 6.1 표 행의 답 순서")
        result = self.run_guard(testrun_path=self.make_testrun(*self.RENAMED))
        self.assertFail(result)
        self.assertRule(result, "FAIL", "test-run-missing", "N.C.M(x: 2)")
        self.assertRule(result, "INFO", "test-run-renamed", "T-099")

    def test_rename_row_must_name_the_card(self) -> None:
        self.provisional_base()
        self.add_provisional_row("T-100")
        self.repo.commit("T-099: 다른 카드의 행")
        self.assertRule(self.run_guard(testrun_path=self.make_testrun(*self.RENAMED)), "FAIL", "test-run-missing", "N.C.M(x: 2)")
        # 카드 번호는 --card(브랜치 이름)로도 준다.
        self.assertNoFail(self.run_guard(testrun_path=self.make_testrun(*self.RENAMED), cards=["task/T-100-rename"]))

    def test_row_in_other_section_does_not_count(self) -> None:
        self.provisional_base()
        self.repo.replace("docs/STATUS.md", "| 2026-11-07 | 0005 | 자동 진행 | juhan | T-010 |", "| 2026-11-07 | 0005 | 자동 진행 | juhan | T-010 |\n| 2026-11-08 | T-099 | x | x | x |")
        self.repo.commit("T-099: 원칙 표에 행")
        self.assertRule(self.run_guard(testrun_path=self.make_testrun(*self.RENAMED)), "FAIL", "test-run-missing", "N.C.M(x: 2)")

    def test_lost_test_in_class_is_not_a_rename(self) -> None:
        self.provisional_base()
        self.add_provisional_row("T-099")
        self.repo.commit("T-099: 하나를 지우고 다른 클래스에 더함")
        result = self.run_guard(testrun_path=self.make_testrun(["N.C.M(x: 1)", "N.C.M(x: 2)"], ["N.C.M(x: 1)", "N.E.New"]))
        self.assertFail(result)
        self.assertRule(result, "FAIL", "test-run-missing", "N.C.M(x: 2)")

    def test_trx_class_and_card_ids(self) -> None:
        self.assertEqual(rg.trx_class('A.B.C.M(changes: "a.b", x: 1)'), "A.B.C")
        self.assertEqual(rg.trx_class("A.B.C.M"), "A.B.C")
        self.repo.commit("T-123: 카드 커밋")
        self.repo.commit("카드 없는 커밋 T-12")
        rng = cigit.resolve_range(self.repo.root, self.base, "HEAD")
        self.assertEqual(rg.card_ids(self.repo.root, rng, ["task/T-045-x", "", "origin/main"]), ["T-045", "T-123"])


PROVISIONAL_STATUS = """
    # STATUS

    ## 원칙 변경 (맨 위에 둔다)

    | 날짜 | ADR | 바뀐 것 | 결정한 사람 | PR |
    | --- | --- | --- | --- | --- |
    | 2026-11-07 | 0005 | 자동 진행 | juhan | T-010 |

    ## 잠정 결정 (ADR 0005 결정 3. 리드가 추천안을 채택한 것)

    | 날짜 | 카드 | 질문 | 채택한 추천안 | PR |
    | --- | --- | --- | --- | --- |
"""


TRX = """<?xml version="1.0" encoding="utf-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult testName="N.A" outcome="Passed" />
    <UnitTestResult testName="N.B(x: 1)" outcome="Failed" />
    <UnitTestResult testName="N.C" outcome="NotExecuted" />
    <UnitTestResult testName="N.D" outcome="Passed">
      <InnerResults>
        <UnitTestResult testName="N.D(1)" outcome="Passed" />
        <UnitTestResult testName="N.D(2)" outcome="NotExecuted" />
      </InnerResults>
    </UnitTestResult>
  </Results>
</TestRun>
"""


class TestTestRunHelpers(unittest.TestCase):
    def test_parse_trx(self) -> None:
        folder = tempfile.mkdtemp(prefix="trx-")
        try:
            path = os.path.join(folder, "r.trx")
            with open(path, "w", encoding="utf-8") as f:
                f.write(TRX)
            data = testrun.parse_trx([path])
        finally:
            shutil.rmtree(folder, ignore_errors=True)
        self.assertEqual((data["total"], data["passed"], data["failed"], data["skipped"]), (5, 2, 1, 2))
        self.assertEqual(data["passed_names"], ["N.A", "N.D(1)"])
        self.assertEqual(data["skipped_names"], ["N.C", "N.D(2)"])

    def test_command_is_fixed(self) -> None:
        cmd = testrun.command("s.runsettings", "out")
        self.assertEqual(cmd[:5], ["dotnet", "test", "AIAdoptionScorecard.sln", "-c", "Release"])
        self.assertIn("--settings", cmd)
        self.assertIn("--filter", cmd)
        self.assertEqual(cmd[cmd.index("--logger") + 1], "trx")


class TestCsharpScanForms(unittest.TestCase):
    def kinds(self, src: str) -> dict[str, int]:
        return csharp_scan.analyze(textwrap.dedent(src)).skip_kinds()

    def test_unicode_escapes(self) -> None:
        src = r"""
            public class C
            {
                [Fact(Skip = "a")] public void A() { }
                [Fact(Skip = "b")] public void B() { }
                [Fact(@Sk\U00000069p = "c")] public void D() { }
                [Fact, Trait("Category", "Skip")] public void E() { }
                [Fact, Trait("Category", "\x53kip")] public void F() { }
                [Fact] public void G() { var s = "Skip = 1"; }
            }
        """
        scan = csharp_scan.analyze(textwrap.dedent(src))
        self.assertEqual(scan.fact_count, 6)
        self.assertEqual(scan.skip_kinds(), {"Skip=": 3, "Trait(skip)": 2})

    def test_escaped_fact_name_counts(self) -> None:
        self.assertEqual(csharp_scan.analyze(r"public class C { [Fact] public void A() { } }").fact_count, 1)

    def test_skip_member_forms(self) -> None:
        src = """
            public sealed class A : Xunit.FactAttribute
            {
                public override string Skip => "x";
            }
            public sealed class B : Xunit.TheoryAttribute
            {
                public override string? Skip { get => "y"; set { } }
                public B() { Skip ??= "z"; }
            }
            public class Plain : TestBase { public void M() { var n = list.Skip(1).Count(); } }
        """
        self.assertEqual(self.kinds(src), {"derived-attribute": 2, "Skip=>": 1, "Skip{": 1, "Skip??=": 1})

    def test_shadow_and_alias(self) -> None:
        src = """
            using MyFact = Xunit.FactAttribute;
            global using Theory = Other.Thing;
            using Plain = System.String;
            namespace N;
            internal class Fact : System.Attribute { }
            internal class Derived : MyFact { }
            public class C
            {
                [MyFact] public void A() { }
            }
        """
        scan = csharp_scan.analyze(textwrap.dedent(src))
        self.assertEqual(scan.skip_kinds(), {"using-alias": 2, "shadow-attribute": 1, "derived-attribute": 1})
        self.assertEqual(scan.fact_count, 1)

    def test_generic_and_multiple_bases(self) -> None:
        src = """
            public class G<T> : Base<T>, IThing where T : class { }
            public class H : IThing, global::Xunit.FactAttribute { }
        """
        self.assertEqual(self.kinds(src), {"derived-attribute": 1})


class TestMutationLimits(unittest.TestCase):
    def setUp(self) -> None:
        self.repo = TempRepo()
        self.repo.write("src/A/A.csproj", "<Project />\n")
        self.repo.write("src/A/Keep.cs", "class Keep { }\n")
        self.repo.write(".config/dotnet-tools.json", '{"version": 1, "tools": {"dotnet-stryker": {"version": "5.0.0"}}}\n')
        self.base = self.repo.commit("base")

    def tearDown(self) -> None:
        self.repo.close()

    def test_timeouts_fit_job(self) -> None:
        self.assertLessEqual(mutation.RUN_TIMEOUT_SECONDS * 2, mutation.DEFAULT_BUDGET_MINUTES * 60)
        # CI는 base 커밋의 tools/ci와 .github/workflows/ci.yml을 같은 임시 폴더에 꺼낸다(두 단계 위가 ci.yml 자리).
        path = repo_file(".github", "workflows", "ci.yml")
        if path is None:
            self.skipTest("ci.yml을 찾지 못함(저장소 밖에서 돌림)")
        with open(path, encoding="utf-8") as f:
            text = f.read()
        job = text[text.index("\n  mutation:"):text.index("\n  oracle:")]
        minutes = int(re.search(r"timeout-minutes: (\d+)", job).group(1))
        budget = float(re.search(r"--budget-minutes (\d+)", job).group(1))
        self.assertLess(budget, minutes)
        self.assertLessEqual(mutation.RUN_TIMEOUT_SECONDS * 2, budget * 60)
        if "\n  test-run:" in text:
            job = text[text.index("\n  test-run:"):text.index("\n  regression-guard:")]
            minutes = int(re.search(r"timeout-minutes: (\d+)", job).group(1))
            self.assertLess(testrun.SIDE_TIMEOUT_SECONDS * 2, minutes * 60)

    def test_timeout_status_and_guard_message(self) -> None:
        rng = cigit.Range("b" * 40, "h" * 40, "시험")
        files = [{"path": "src/A/Keep.cs", "base_path": "src/A/Keep.cs", "project": "src/A/A.csproj", "head": None, "base": None}]
        data = mutation.build_result(rng, files, ["head: src/A/A.csproj 시간 초과"], [], "5.0.0", timed_out=True)
        self.assertEqual(data["status"], "timeout")
        self.assertIn("시간 초과", mutation.render_summary(data))
        ok, reasons = rg.mutation_evidence(data, None, rng, "failure")
        self.assertFalse(ok)
        self.assertIn("status=timeout", reasons[0])

    def test_run_stryker_timeout_returns_124(self) -> None:
        root = tempfile.mkdtemp(prefix="mut-timeout-")
        original = mutation.subprocess.run

        def fake_run(*args, **kwargs):
            raise subprocess.TimeoutExpired(args[0], kwargs.get("timeout"))

        try:
            os.makedirs(os.path.join(root, mutation.TEST_PROJECT_DIR))
            mutation.subprocess.run = fake_run
            code, _ = mutation.run_stryker(root, "src/A/A.csproj", ["src/A/Keep.cs"], os.path.join(root, "out"), os.path.join(root, "log.txt"), 5)
            with open(os.path.join(root, "log.txt"), encoding="utf-8") as f:
                log = f.read()
        finally:
            mutation.subprocess.run = original
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual(code, mutation.TIMEOUT_EXIT)
        self.assertIn("시간 초과(5초)", log)

    def test_manifest_comes_from_base(self) -> None:
        self.repo.write(".config/dotnet-tools.json", '{"version": 1, "tools": {"dotnet-stryker": {"version": "9.9.9"}}}\n')
        head = self.repo.commit("PR이 버전을 바꿈")
        manifest, source = mutation.tool_manifest(self.repo.root, cigit.Range(self.base, head, ""))
        self.assertEqual((mutation.tool_version(manifest), source), ("5.0.0", "base"))

    def test_stryker_configs_are_removed(self) -> None:
        root = tempfile.mkdtemp(prefix="mut-config-")
        try:
            folder = os.path.join(root, mutation.TEST_PROJECT_DIR)
            os.makedirs(folder)
            for name in ("stryker-config.json", "stryker-config.yaml", "other.json"):
                with open(os.path.join(folder, name), "w", encoding="utf-8") as f:
                    f.write("{}")
            removed = mutation.remove_stryker_configs(root)
            left = sorted(os.listdir(folder))
        finally:
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual(removed, ["stryker-config.json", "stryker-config.yaml"])
        self.assertEqual(left, ["other.json"])


class TestCiChangedPrinciples(GuardRepoTestCase):
    """T-010 재리뷰 사소 8: .claude/**, CLAUDE.md, docs/README-instructions.md, global.json, 기존 ADR 수정."""

    def setUp(self) -> None:
        super().setUp()
        TestCiChanged.make_guarded_base(self)

    def test_principle_files_fail_without_adr(self) -> None:
        cases = {
            ".claude/settings.json": "{}\n",
            ".claude/agents/ci-dev.md": "# 바꿈\n",
            "CLAUDE.md": "# 바꿈\n",
            "docs/README-instructions.md": "# 바꿈\n",
            "global.json": "{}\n",
        }
        for path, text in cases.items():
            with self.subTest(path=path):
                self.repo.git("reset", "-q", "--hard", self.base)
                self.repo.write(path, text)
                self.repo.commit(f"T-099: {path}")
                result = self.run_guard()
                self.assertFail(result)
                self.assertRule(result, "FAIL", "ci-changed", path)

    def test_existing_adr_edit_is_watched(self) -> None:
        self.repo.replace("docs/decisions/0005-auto-mode.md", "# 0005", "# 0005 고침")
        self.repo.commit("T-099: ADR 0005만 고침")
        self.assertRule(self.run_guard(), "FAIL", "ci-changed", "docs/decisions/0005-auto-mode.md")

    def test_new_adr_alone_is_not_watched(self) -> None:
        self.repo.write("docs/decisions/0006-new.md", "# 0006\n")
        self.repo.commit("T-099: 새 ADR만")
        result = self.run_guard()
        self.assertNoFail(result)
        self.assertRule(result, "PASS", "ci-changed", "변경 없음")

    def test_principle_change_with_adr_and_row_passes(self) -> None:
        self.repo.write("CLAUDE.md", "# 바꿈\n")
        TestCiChanged.add_adr_and_row(self, "0006")
        self.repo.commit("T-012: 원칙 바꿈, ADR 0006")
        result = self.run_guard()
        self.assertNoFail(result)
        self.assertRule(result, "PASS", "ci-changed", "CLAUDE.md")


class TestTamper(GuardRepoTestCase):
    """T-010 재리뷰 B4: head 테스트가 CI 러너의 base 도구·결과 파일·GITHUB_ENV를 고치는 길(덫)."""

    def test_runner_temp_in_new_test_fails(self) -> None:
        # 리뷰 재현 n1: 테스트가 $RUNNER_TEMP/base-tools/tools/ci/regression_guard.py를 덮어쓴다.
        self.repo.write("tests/Demo.Tests/RunnerTempTests.cs", """
            public class RunnerTempTests
            {
                [Fact]
                public void Runner_temp_is_writable()
                {
                    var t = System.Environment.GetEnvironmentVariable("RUNNER_TEMP");
                    var g = System.IO.Path.Combine(t, "base-tools", "tools", "ci", "regression_guard.py");
                }
            }
        """)
        self.repo.commit("T-099: 러너 임시 폴더 확인 테스트")
        result = self.run_guard()
        self.assertFail(result)
        self.assertRule(result, "FAIL", "ci-tamper", "RUNNER_TEMP")
        self.assertRule(result, "FAIL", "ci-tamper", "base-tools")
        self.assertRule(result, "FAIL", "ci-tamper", "regression_guard")

    def test_escaped_and_other_files_fail(self) -> None:
        cases = {
            "tests/Demo.Tests/E.cs": 'class E { string s = "\\u0047ITHUB_ENV"; }\n',
            "src/Demo.Core/T.cs": 'class T { string s = "out/TestRun.json"; }\n',
            "tests/Demo.Tests/data.json": '{"p": ".git/refs/replace/x"}\n',
            "tests/Demo.Tests/Results.cs": 'class R { string s = "x.trx"; }\n',
        }
        for path, text in cases.items():
            with self.subTest(path=path):
                self.repo.git("reset", "-q", "--hard", self.base)
                self.repo.write(path, text)
                self.repo.commit(f"T-099: {path}")
                self.assertRule(self.run_guard(), "FAIL", "ci-tamper", path)

    def test_existing_occurrence_passes_and_evidence_does_not_help(self) -> None:
        self.repo.write("tests/Demo.Tests/Env.cs", 'class Env { string s = "GITHUB_WORKSPACE"; }\n')
        self.base = self.repo.commit("base에 이미 있음")
        self.repo.replace("tests/Demo.Tests/Env.cs", "class Env", "class Env2")
        self.repo.commit("이름만")
        self.assertRule(self.run_guard(), "PASS", "ci-tamper")
        self.repo.replace("tests/Demo.Tests/Env.cs", '"GITHUB_WORKSPACE";', '"GITHUB_WORKSPACE" + "RUNNER_TEMP";')
        self.repo.replace("src/Demo.Core/Foo.cs", "x + 1", "x + 2")
        self.repo.commit("하나 더 + src")
        result = self.run_guard(self.mutation_file())
        self.assertFail(result)
        self.assertRule(result, "FAIL", "ci-tamper", "RUNNER_TEMP")

    def test_tamper_counts(self) -> None:
        self.assertEqual(rg.tamper_counts(b'"runner_temp" "\\u0052UNNER_TEMP"')["RUNNER_TEMP"], 2)
        self.assertEqual(rg.tamper_counts(b"plain text"), {})
        self.assertEqual(rg.tamper_counts(None), {})


class TestJobResults(GuardRepoTestCase):
    def test_job_results(self) -> None:
        self.repo.commit("빈 커밋")
        ok = self.run_guard(jobs={"test-run": "success", "oracle-head": "success"})
        self.assertNoFail(ok)
        self.assertRule(ok, "PASS", "job-result", "test-run")
        bad = self.run_guard(jobs={"test-run": "failure", "oracle-head": "skipped"})
        self.assertFail(bad)
        self.assertRule(bad, "FAIL", "job-result", "test-run 잡 결과가 failure")
        self.assertRule(bad, "FAIL", "job-result", "oracle-head 잡 결과가 skipped")

    def test_main_job_argument(self) -> None:
        self.repo.commit("빈 커밋")
        common = ["--repo", self.repo.root, "--base", self.base, "--summary", ""]
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(rg.main(common + ["--job", "test-run=success"]), 0)
            self.assertEqual(rg.main(common + ["--job", "test-run=failure"]), 1)
            self.assertEqual(rg.main(common + ["--job", "test-run"]), 2)


class TestTestsChangedWithSrc(GuardRepoTestCase):
    def test_src_and_existing_test_warns(self) -> None:
        # 리뷰 재현 n3c: src 변경과 기존 테스트의 단언 변경이 같이 오면 그 테스트 파일을 적는다.
        self.repo.replace("src/Demo.Core/Foo.cs", "x + 1", "x + 2")
        self.repo.replace("tests/Demo.Tests/FooTests.cs", "public void One() { }", "public void One() { /* 단언 뺌 */ }")
        self.repo.commit("src + 테스트")
        result = self.run_guard()
        self.assertNoFail(result)
        self.assertRule(result, "WARN", "tests-changed-with-src", "tests/Demo.Tests/FooTests.cs")

    def test_no_warning_without_src_or_for_new_tests(self) -> None:
        self.repo.replace("tests/Demo.Tests/FooTests.cs", "public void One() { }", "public void One() { var x = 1; }")
        self.repo.commit("테스트만")
        self.assertFalse([f for f in self.run_guard().findings if f.rule == "tests-changed-with-src"])
        self.repo.git("reset", "-q", "--hard", self.base)
        self.repo.replace("src/Demo.Core/Foo.cs", "x + 1", "x + 2")
        self.repo.write("tests/Demo.Tests/NewTests.cs", "public class N { [Fact] public void A() { } }\n")
        self.repo.commit("src + 새 테스트")
        self.assertFalse([f for f in self.run_guard().findings if f.rule == "tests-changed-with-src"])


class TestMutationInitialFailures(unittest.TestCase):
    def test_parse_stryker_log(self) -> None:
        root = tempfile.mkdtemp(prefix="mut-log-")
        try:
            log = os.path.join(root, "x.log")
            with open(log, "w", encoding="utf-8") as f:
                f.write("[16:35:39 INF] Number of tests found: 1282. Initial test run started.\n"
                        "[16:35:53 WRN] 1 tests are failing. Stryker will continue but outcome will be impacted.\n")
            self.assertEqual(mutation.initial_failures(log), 1)
            with open(log, "w", encoding="utf-8") as f:
                f.write("[16:35:53 INF] Initial test run ok.\n")
            self.assertEqual(mutation.initial_failures(log), 0)
            self.assertEqual(mutation.initial_failures(os.path.join(root, "없음.log")), 0)
        finally:
            shutil.rmtree(root, ignore_errors=True)

    def test_guard_rejects_runs_with_initial_failures(self) -> None:
        rng = cigit.Range("b" * 40, "h" * 40, "")
        stats = {"killed": 9, "timeout": 0, "survived": 0, "no_coverage": 0}
        data = {"status": "measured", "base": rng.base, "head": rng.head,
                "files": [{"path": "src/A/A.cs", "base_path": "src/A/A.cs", "head": stats, "base": stats}],
                "runs": [{"side": "head", "project": "src/A/A.csproj", "initial_failures": 1}]}
        ok, reasons = rg.mutation_evidence(data, None, rng, "success")
        self.assertFalse(ok)
        self.assertIn("처음 테스트 실행에서 1개가 실패", reasons[0])
        data["runs"][0]["initial_failures"] = 0
        self.assertTrue(rg.mutation_evidence(data, None, rng, "success")[0])

    def test_manifest_from_head_commit_in_bootstrap(self) -> None:
        repo = TempRepo()
        try:
            repo.write("src/A/A.cs", "class A { }\n")
            base = repo.commit("base: manifest 없음")
            repo.write(".config/dotnet-tools.json", '{"version": 1, "tools": {"dotnet-stryker": {"version": "4.4.1"}}}\n')
            head = repo.commit("head: manifest 넣음")
            os.remove(os.path.join(repo.root, ".config", "dotnet-tools.json"))  # 작업 트리가 아니라 커밋에서 읽는다
            manifest, source = mutation.tool_manifest(repo.root, cigit.Range(base, head, ""))
            self.assertEqual((mutation.tool_version(manifest), source), ("4.4.1", "head"))
        finally:
            repo.close()


class TestNoReplaceObjects(unittest.TestCase):
    def test_replace_refs_are_ignored(self) -> None:
        # B4: head 코드가 .git/refs/replace/에 대체 개체를 넣어도 guard는 진짜 개체를 읽는다.
        repo = TempRepo()
        try:
            repo.write("tests/T.cs", "real\n")
            repo.commit("하나")
            real = repo.git("rev-parse", "HEAD:tests/T.cs")
            fake = subprocess.run(["git", "-C", repo.root, "hash-object", "-w", "--stdin"], input=b"fake\n",
                                  stdout=subprocess.PIPE, check=True).stdout.decode().strip()
            repo.git("replace", real, fake)
            plain = {k: v for k, v in os.environ.items() if k != "GIT_NO_REPLACE_OBJECTS"}  # CI 잡 환경에는 이 값이 있다
            shown = subprocess.run(["git", "-C", repo.root, "cat-file", "-p", "HEAD:tests/T.cs"], stdout=subprocess.PIPE, env=plain, check=True)
            self.assertEqual(shown.stdout, b"fake\n")  # 그냥 git은 대체 개체를 보인다
            self.assertEqual(cigit.read_file(repo.root, "HEAD", "tests/T.cs"), b"real\n")
            self.assertEqual(cigit.read_tree_files(repo.root, "HEAD", "tests/", ".cs"), {"tests/T.cs": b"real\n"})
        finally:
            repo.close()


class TestLeadCheckHelpers(unittest.TestCase):
    """T-010 재리뷰 B5: 리드가 머지 전에 도는 lead_check.py의 도우미."""

    def setUp(self) -> None:
        self.repo = TempRepo()
        r = self.repo
        r.write("tools/ci/regression_guard.py", "# guard\n")
        r.write("tools/ci/cigit.py", "# cigit\n")
        r.write("src/A.cs", "class A { }\n")
        r.write("docs/a.md", "하나\n")
        self.base = r.commit("base")

    def tearDown(self) -> None:
        self.repo.close()

    def test_sensitive_paths(self) -> None:
        cases = {
            ".github/workflows/ci.yml": True, "tools/ci/x.py": True, "tools/other/y.sh": True, ".config/dotnet-tools.json": True,
            ".claude/settings.json": True, "CLAUDE.md": True, "docs/README-instructions.md": True, "docs/decisions/0006-x.md": True,
            "global.json": True, "src/A.cs": False, "docs/spec/x.md": False, "tests/X.cs": False, "docs/STATUS.md": False,
        }
        for path, want in cases.items():
            self.assertEqual(lead_check.is_sensitive(path), want, path)
        self.repo.write(".claude/settings.json", "{}\n")
        self.repo.write("src/A.cs", "class A { int x; }\n")
        head = self.repo.commit("민감한 경로 하나")
        self.assertEqual(lead_check.sensitive_paths(self.repo.root, self.base, head), [".claude/settings.json"])

    def test_merged_head(self) -> None:
        r = self.repo
        r.git("checkout", "-q", "-b", "pr")
        r.write("src/B.cs", "class B { }\n")
        tip = r.commit("T-099: PR")
        self.assertEqual(lead_check.merged_head(r.root, self.base, tip)[0], tip)  # base를 품는다
        r.git("checkout", "-q", "main")
        r.write("docs/a.md", "둘\n")
        main = r.commit("main이 앞으로")
        head, note = lead_check.merged_head(r.root, main, tip)
        self.assertNotEqual(head, tip)
        self.assertEqual(cigit.parents(r.root, head), [main, tip])
        self.assertEqual(lead_check.merged_head(r.root, main, tip)[0], head)  # 같은 입력이면 같은 병합 커밋
        self.assertIn("병합한 커밋", note)
        self.assertEqual(r.git("rev-parse", "main"), main)  # ref를 건드리지 않는다
        r.git("checkout", "-q", "pr")
        r.write("docs/a.md", "셋\n")
        conflict = r.commit("같은 줄")
        with self.assertRaises(lead_check.CheckError):
            lead_check.merged_head(r.root, main, conflict)

    def test_archive_extract_and_blob_ids(self) -> None:
        dest = tempfile.mkdtemp(prefix="lc-extract-")
        try:
            data = lead_check.archive_bytes(self.repo.root, self.base, ["tools/ci", "tools/oracle"])
            lead_check.extract(data, dest)
            ids = lead_check.blob_ids(dest)
            self.assertEqual(sorted(ids), ["tools/ci/cigit.py", "tools/ci/regression_guard.py"])
            self.assertEqual(ids["tools/ci/cigit.py"], cigit.blob_id(self.repo.root, self.base, "tools/ci/cigit.py"))
            self.assertEqual(lead_check.archive_bytes(self.repo.root, self.base, ["없는/폴더"]), b"")
            whole = tempfile.mkdtemp(prefix="lc-whole-")
            lead_check.extract(lead_check.archive_bytes(self.repo.root, self.base, None), whole)
            self.assertTrue(os.path.isfile(os.path.join(whole, "docs", "a.md")))
            shutil.rmtree(whole, ignore_errors=True)
        finally:
            shutil.rmtree(dest, ignore_errors=True)

    def test_make_sandbox(self) -> None:
        r = self.repo
        r.git("checkout", "-q", "-b", "pr")
        r.write("src/B.cs", "class B { }\n")
        tip = r.commit("T-099: PR")
        r.git("checkout", "-q", "main")
        r.write("docs/a.md", "둘\n")
        main = r.commit("main")
        head, _ = lead_check.merged_head(r.root, main, tip)
        folder = os.path.join(tempfile.mkdtemp(prefix="lc-sandbox-"), "s")
        try:
            lead_check.make_sandbox(r.root, main, tip, head, folder)
            self.assertEqual(cigit.rev_parse(folder, "refs/lead-check/head"), head)
            self.assertEqual(cigit.rev_parse(folder, "refs/lead-check/base"), main)
            self.assertFalse(os.path.exists(os.path.join(folder, ".git", "FETCH_HEAD")))
            self.assertNotIn(r.root, cigit.git(folder, "config", "--list"))
        finally:
            shutil.rmtree(os.path.dirname(folder), ignore_errors=True)

    def test_run_step_kills_leftover_processes(self) -> None:
        root = tempfile.mkdtemp(prefix="lc-step-")
        try:
            pid_file = os.path.join(root, "pid")
            code = lead_check.run_step(["sh", "-c", f"sleep 60 & echo $! > {pid_file}; exit 3"], root, os.path.join(root, "log", "x.log"),
                                       dict(os.environ), isolate=True)
            self.assertEqual(code, 3)
            with open(pid_file, encoding="utf-8") as f:
                pid = int(f.read().strip())

            def alive() -> bool:
                try:
                    with open(f"/proc/{pid}/stat", encoding="utf-8") as f:
                        return f.read().rsplit(")", 1)[1].split()[0] != "Z"
                except OSError:
                    return False
            for _ in range(50):
                if not alive():
                    break
                time.sleep(0.05)
            self.assertFalse(alive(), "남은 프로세스가 죽지 않았다")
        finally:
            shutil.rmtree(root, ignore_errors=True)


# ---------------------------------------------------------------- T-011a: oracle selftest, guard --job


def workflow_text() -> str | None:
    path = repo_file(".github", "workflows", "ci.yml")
    if path is None:
        return None
    with open(path, encoding="utf-8") as f:
        return f.read()


def workflow_job(text: str, name: str) -> str:
    """ci.yml에서 잡 하나. 두 칸 들여쓴 `name:` 줄부터 다음 잡 줄 앞까지."""
    lines = text.split("\n")
    start = lines.index(f"  {name}:")
    end = next((i for i in range(start + 1, len(lines)) if re.fullmatch(r"  [A-Za-z0-9_-]+:\s*", lines[i])), len(lines))
    return "\n".join(lines[start:end])


def workflow_step_run(job: str, name_prefix: str) -> str:
    """잡 안의 `- name: <name_prefix>…` 단계의 `run: |` 블록(들여쓰기를 뗀 셸 글)."""
    lines = job.split("\n")
    k = next(i for i, line in enumerate(lines) if line.strip().startswith(f"- name: {name_prefix}"))
    dash = len(lines[k]) - len(lines[k].lstrip())
    end = next((i for i in range(k + 1, len(lines)) if lines[i].strip() and len(lines[i]) - len(lines[i].lstrip()) <= dash), len(lines))
    r = next(i for i in range(k + 1, end) if lines[i].strip() == "run: |")
    indent = len(lines[r]) - len(lines[r].lstrip())
    body = []
    for line in lines[r + 1:end]:
        if line.strip() and len(line) - len(line.lstrip()) <= indent:
            break
        body.append(line)
    return textwrap.dedent("\n".join(body)).strip("\n") + "\n"


FAKE_ORACLE_CHECK = """
    import os, pathlib, sys
    pathlib.Path(os.environ["RUNNER_TEMP"], "ran-check").write_text(" ".join(sys.argv[1:]), encoding="utf-8")
    out = sys.argv[sys.argv.index("--json") + 1]
    pathlib.Path(out).write_text('{{"files": []}}\\n', encoding="utf-8")
    sys.exit({code})
"""

FAKE_ORACLE_SELFTEST = """
    import os, pathlib, sys
    pathlib.Path(os.environ["RUNNER_TEMP"], "ran-selftest").write_text(" ".join(sys.argv[1:]), encoding="utf-8")
    sys.exit({code})
"""


def write_fake_oracle(root: str, check_code: int = 0, selftest_code: int | None = 0) -> None:
    """tools/oracle/check.py·selftest.py 가짜. 돌았으면 RUNNER_TEMP에 표시 파일을 남긴다. selftest_code None이면 selftest가 없다."""
    folder = os.path.join(root, "tools", "oracle")
    os.makedirs(folder, exist_ok=True)
    with open(os.path.join(folder, "check.py"), "w", encoding="utf-8", newline="\n") as f:
        f.write(textwrap.dedent(FAKE_ORACLE_CHECK.format(code=check_code)).lstrip("\n"))
    selftest = os.path.join(folder, "selftest.py")
    if selftest_code is None:
        if os.path.exists(selftest):
            os.remove(selftest)
    else:
        with open(selftest, "w", encoding="utf-8", newline="\n") as f:
            f.write(textwrap.dedent(FAKE_ORACLE_SELFTEST.format(code=selftest_code)).lstrip("\n"))


class TestOracleHeadWorkflow(unittest.TestCase):
    """T-011a: ci.yml oracle-head 단계의 셸을 그대로 꺼내 임시 저장소에서 돌린다.

    tools/oracle이 바뀐 PR이면 head check.py와 head selftest.py를 둘 다 돌리고, 하나라도 실패하면 단계(잡)가 실패한다.
    음성 대조: head selftest가 실패하는 변경 → 종료 1.
    """

    def setUp(self) -> None:
        text = workflow_text()
        if text is None:
            self.skipTest("ci.yml을 찾지 못함(저장소 밖에서 돌림)")
        if shutil.which("bash") is None:
            self.skipTest("bash 없음")
        self.script = workflow_step_run(workflow_job(text, "oracle-head"), "head oracle")
        self.repo = TempRepo()
        self.tmp = tempfile.mkdtemp(prefix="oracle-head-")
        # 단계는 PATH의 python3를 부른다. 이 테스트를 도는 python으로 고정한다.
        bin_dir = os.path.join(self.tmp, "bin")
        os.makedirs(bin_dir)
        os.symlink(sys.executable, os.path.join(bin_dir, "python3"))
        self.path_env = bin_dir + os.pathsep + os.environ.get("PATH", "")
        self.script_path = os.path.join(self.tmp, "step.sh")
        with open(self.script_path, "w", encoding="utf-8", newline="\n") as f:
            f.write(self.script)

    def tearDown(self) -> None:
        if hasattr(self, "repo"):
            self.repo.close()
            shutil.rmtree(self.tmp, ignore_errors=True)

    def run_step(self, base: str) -> tuple[int, str, set[str]]:
        runner_temp = tempfile.mkdtemp(dir=self.tmp)
        env = dict(os.environ, CI_BASE=base, RUNNER_TEMP=runner_temp, GIT_NO_REPLACE_OBJECTS="1", PATH=self.path_env,
                   PYTHONDONTWRITEBYTECODE="1")
        # GitHub의 기본 셸(ubuntu, shell 없음)은 bash -e {0}다.
        proc = subprocess.run(["bash", "--noprofile", "--norc", "-e", self.script_path], cwd=self.repo.root, env=env,
                              stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        ran = {name for name in ("ran-check", "ran-selftest") if os.path.exists(os.path.join(runner_temp, name))}
        if "ran-selftest" in ran:
            with open(os.path.join(runner_temp, "ran-selftest"), encoding="utf-8") as f:
                self.assertEqual(f.read(), "--repo .")
        return proc.returncode, proc.stdout.decode("utf-8", "replace"), ran

    def base_with_oracle(self, selftest_code: int = 0) -> str:
        write_fake_oracle(self.repo.root, selftest_code=selftest_code)
        self.repo.write("docs/a.md", "하나\n")
        return self.repo.commit("base")

    def test_script_runs_check_and_selftest(self) -> None:
        self.assertIn("tools/oracle/check.py", self.script)
        self.assertIn("tools/oracle/selftest.py", self.script)

    def test_unchanged_oracle_runs_nothing(self) -> None:
        base = self.base_with_oracle(selftest_code=1)  # 돌면 실패할 selftest
        self.repo.write("docs/a.md", "둘\n")
        self.repo.commit("T-099: 문서만")
        code, out, ran = self.run_step(base)
        self.assertEqual(code, 0, out)
        self.assertIn("tools/oracle 변경 없음", out)
        self.assertEqual(ran, set())

    def test_bootstrap_without_base_oracle_runs_nothing(self) -> None:
        self.repo.write("docs/a.md", "하나\n")
        base = self.repo.commit("base")
        write_fake_oracle(self.repo.root, selftest_code=1)
        self.repo.commit("T-099: oracle 처음")
        code, out, ran = self.run_step(base)
        self.assertEqual(code, 0, out)
        self.assertIn("부트스트랩", out)
        self.assertEqual(ran, set())

    def test_changed_oracle_passes_when_both_pass(self) -> None:
        base = self.base_with_oracle()
        self.repo.write("tools/oracle/README.md", "고침\n")
        self.repo.commit("T-099: oracle 고침")
        code, out, ran = self.run_step(base)
        self.assertEqual(code, 0, out)
        self.assertEqual(ran, {"ran-check", "ran-selftest"})

    def test_negative_failing_head_selftest_fails_the_step(self) -> None:
        # 음성 대조: head selftest가 실패하는 변경. CI_BASE가 비어도(HEAD^1) 같다.
        base = self.base_with_oracle()
        write_fake_oracle(self.repo.root, selftest_code=1)
        self.repo.commit("T-099: selftest가 실패하게")
        for ci_base in (base, ""):
            code, out, ran = self.run_step(ci_base)
            self.assertEqual(code, 1, out)
            self.assertIn("selftest.py 실패", out)
            self.assertEqual(ran, {"ran-check", "ran-selftest"})

    def test_failing_check_still_runs_selftest(self) -> None:
        base = self.base_with_oracle()
        write_fake_oracle(self.repo.root, check_code=1)
        self.repo.commit("T-099: check가 실패하게")
        code, out, ran = self.run_step(base)
        self.assertEqual(code, 1, out)
        self.assertIn("check.py 실패", out)
        self.assertEqual(ran, {"ran-check", "ran-selftest"})

    def test_deleted_selftest_fails(self) -> None:
        base = self.base_with_oracle()
        write_fake_oracle(self.repo.root, selftest_code=None)
        self.repo.commit("T-099: selftest 지움")
        code, out, ran = self.run_step(base)
        self.assertEqual(code, 1, out)
        self.assertIn("selftest.py가 없다", out)
        self.assertEqual(ran, {"ran-check"})

    def test_base_oracle_job_runs_check_only(self) -> None:
        # 결정한 해석 10: oracle 잡(head 코드 없음)은 base check.py만 돈다. base selftest는 돌리지 않는다.
        job = workflow_job(workflow_text() or "", "oracle")
        code_lines = "\n".join(line for line in job.split("\n") if not line.strip().startswith("#"))
        self.assertIn('"$CI_ORACLE/check.py"', code_lines)
        self.assertNotIn("selftest", code_lines)


class TestGuardJobsWiring(GuardRepoTestCase):
    """T-011a(T-010 3차 리뷰 막지 않는 지적 4): guard가 기다리는 잡마다 --job 결과를 받는다. lead_check도 같은 잡."""

    def test_workflow_passes_every_needed_job(self) -> None:
        text = workflow_text()
        if text is None:
            self.skipTest("ci.yml을 찾지 못함(저장소 밖에서 돌림)")
        job = workflow_job(text, "regression-guard")
        needs = [n.strip() for n in re.search(r"needs: \[([^\]]*)\]", job).group(1).split(",")]
        self.assertEqual(sorted(needs), sorted(lead_check.GUARD_JOBS))
        for name in needs:
            hits = re.findall(rf'--job "{re.escape(name)}=\$([A-Z_]+)"', job)
            self.assertEqual(len(hits), 1, f"--job {name}")
            self.assertRegex(job, rf"\n\s+{hits[0]}: \$\{{\{{ needs\.{re.escape(name)}\.result \}}\}}\n")

    def test_lead_check_job_args(self) -> None:
        args = lead_check.guard_job_args({"mutation": True, "oracle": False, "oracle-head": True})
        self.assertEqual(args, ["--job", "mutation=success", "--job", "oracle=failure", "--job", "oracle-head=success",
                                "--job", "test-run=failure"])

    def test_failed_oracle_or_mutation_job_fails_guard(self) -> None:
        self.repo.commit("빈 커밋")
        common = ["--repo", self.repo.root, "--base", self.base, "--summary", ""]
        all_ok = {name: True for name in lead_check.GUARD_JOBS}
        for name in lead_check.GUARD_JOBS:
            out = io.StringIO()
            with contextlib.redirect_stdout(out), contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(rg.main(common + lead_check.guard_job_args({**all_ok, name: False})), 1, name)
            self.assertIn(f"[FAIL] job-result: {name} 잡 결과가 failure", out.getvalue())
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(rg.main(common + lead_check.guard_job_args(all_ok)), 0)


class TestLeadCheckHeadOracle(unittest.TestCase):
    """T-011a: lead_check의 head oracle 단계도 check.py와 selftest.py를 둘 다 돈다(ci.yml oracle-head와 같다)."""

    def setUp(self) -> None:
        self.root = tempfile.mkdtemp(prefix="lc-oracle-")
        self.tree = os.path.join(self.root, "tree")
        self.work = os.path.join(self.root, "work")
        os.makedirs(self.work)
        self.env = dict(os.environ, RUNNER_TEMP=self.work, PYTHONDONTWRITEBYTECODE="1")
        self.steps: list[tuple[str, int | None, str]] = []

    def tearDown(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)

    def step(self, name: str, code: int | None, detail: str, ok_codes: tuple[int, ...] = (0,)) -> bool:
        self.steps.append((name, code, detail))
        return code in ok_codes

    def run_oracle(self, check_code: int = 0, selftest_code: int | None = 0) -> bool:
        write_fake_oracle(self.tree, check_code, selftest_code)
        self.steps = []
        for name in ("ran-check", "ran-selftest"):
            with contextlib.suppress(FileNotFoundError):
                os.remove(os.path.join(self.work, name))
        return lead_check.run_head_oracle(sys.executable, self.tree, self.work, os.path.join(self.work, "logs"), self.env, self.step)

    def test_both_pass(self) -> None:
        self.assertTrue(self.run_oracle())
        self.assertEqual([c for _, c, _ in self.steps], [0, 0])
        self.assertTrue(os.path.exists(os.path.join(self.work, "ran-selftest")))

    def test_negative_failing_selftest(self) -> None:
        self.assertFalse(self.run_oracle(selftest_code=1))
        self.assertEqual([c for _, c, _ in self.steps], [0, 1])
        self.assertIn("selftest", self.steps[1][0])

    def test_failing_check_still_runs_selftest(self) -> None:
        self.assertFalse(self.run_oracle(check_code=1))
        self.assertEqual([c for _, c, _ in self.steps], [1, 0])

    def test_missing_selftest_fails(self) -> None:
        self.assertFalse(self.run_oracle(selftest_code=None))
        self.assertEqual(self.steps[1][1], None)
        self.assertIn("selftest.py가 없다", self.steps[1][2])


if __name__ == "__main__":
    unittest.main()
