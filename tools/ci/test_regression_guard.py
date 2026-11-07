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
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import cigit  # noqa: E402
import csharp_scan  # noqa: E402
import mutation  # noqa: E402
import regression_guard as rg  # noqa: E402
import testrun  # noqa: E402

_ENV_BACKUP: dict[str, str | None] = {}
_GIT_HOME = ""


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
        here = os.path.dirname(os.path.abspath(__file__))
        path = os.path.join(here, "..", "..", "tests", "Scorecard.Tests", "Guards", "GuardTargets.cs")
        if not os.path.isfile(path):
            self.skipTest("저장소 밖에서 돌림")
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

    def test_migration_commit_with_spec_passes(self) -> None:
        self.repo.replace(self.PATH, '"schema_version": 1', '"schema_version": 1,\n  "case": "ex"')
        self.repo.write("docs/spec/classification.md", "블록\n")
        self.repo.commit("expected migration: 스펙 블록과 같이")
        result = self.run_guard(oracle_path=self.oracle_file({self.PATH: "match"}))
        self.assertNoFail(result)
        self.assertRule(result, "PASS", "migration-commit-scope", "expected·docs/spec만")

    def test_migration_commit_without_expected_is_also_checked(self) -> None:
        self.repo.replace("tests/Demo.Tests/FooTests.cs", "One", "OneRenamed")
        self.repo.commit("expected migration: 사실은 테스트 고침")
        self.assertRule(self.run_guard(), "FAIL", "migration-commit-scope", "FooTests.cs")


class TestTestRun(GuardRepoTestCase):
    def make_testrun(self, base_names: list[str], head_names: list[str], base_skipped: list[str] | None = None,
                     head_skipped: list[str] | None = None, status: str = "measured", base: str | None = None,
                     head_status: str = "ok") -> str:
        def side(names: list[str], skipped: list[str], st: str = "ok") -> dict:
            return {"status": st, "total": len(names) + len(skipped), "passed": len(names), "failed": 0, "skipped": len(skipped),
                    "passed_names": sorted(names), "failed_names": [], "skipped_names": sorted(skipped)}
        data = {"schema_version": 1, "status": status, "base": base or self.base, "head": self.head(), "errors": [] if status == "measured" else ["빌드 실패"],
                "sides": {"base": side(base_names, base_skipped or []), "head": side(head_names, head_skipped or [], head_status)}}
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
        # CI는 base 커밋의 tools/ci를 꺼내 돌린다. 그 폴더 옆에는 ci.yml이 없으니 checkout 폴더의 것을 본다.
        here = os.path.dirname(os.path.abspath(__file__))
        candidates = [os.path.join(here, "..", "..", ".github", "workflows", "ci.yml")]
        if os.environ.get("GITHUB_WORKSPACE"):
            candidates.append(os.path.join(os.environ["GITHUB_WORKSPACE"], ".github", "workflows", "ci.yml"))
        found = [c for c in candidates if os.path.isfile(c)]
        if not found:
            self.skipTest("ci.yml을 찾지 못함(저장소 밖에서 돌림)")
        with open(found[0], encoding="utf-8") as f:
            text = f.read()
        job = text[text.index("\n  mutation:"):text.index("\n  oracle:")]
        minutes = int(re.search(r"timeout-minutes: (\d+)", job).group(1))
        budget = float(re.search(r"--budget-minutes (\d+)", job).group(1))
        self.assertLess(budget, minutes)
        self.assertLessEqual(mutation.RUN_TIMEOUT_SECONDS * 2, budget * 60)

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


if __name__ == "__main__":
    unittest.main()
