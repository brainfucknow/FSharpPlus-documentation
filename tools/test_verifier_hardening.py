"""Exercise the built verifier's scope checks and both generated execution targets.

Run after building tools/Verify/Verify.fsproj, with .NET 8 on PATH (or DOTNET set).
All fixtures and generated builds live in disposable directories outside the checkout.
"""

import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
DOTNET = os.environ.get("DOTNET", "dotnet")
ASSEMBLY = ROOT / "tools/Verify/bin/Release/net8.0"


class VerifierHardeningTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="verify-regression-")
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        shutil.copytree(ASSEMBLY, self.root / "tool")
        shutil.copyfile(ROOT / "upstream.json", self.root / "upstream.json")
        shutil.copyfile(ROOT / "global.json", self.root / "global.json")
        self.tool = self.root / "tool/verify.dll"

    def run_command(self, *args):
        return subprocess.run(
            [DOTNET, *map(str, args)], cwd=self.root,
            capture_output=True, text=True, timeout=120,
        )

    def verify(self, *args):
        return self.run_command(self.tool, *args)

    def assert_succeeded(self, result):
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def source_fixture(self, module_body):
        upstream = self.root / "upstream"
        source = upstream / "src/FSharpPlus"
        (source / "Data").mkdir(parents=True, exist_ok=True)
        # The gate needs all claimed public definitions, but the regression varies only mapM's
        # enclosing scope. These are valid, intentionally minimal source definitions.
        names = "map result pur lift2 lift3 liftA2 traverse sequence bind join map2 map3 zip unzip tap memoizeN tryParse parse view setl over preview toListOf"
        (source / "Operators.fs").write_text(
            "module FSharpPlus.Operators\n" + "".join(f"let {name} = ()\n" for name in names.split()),
            encoding="utf-8",
        )
        (source / "Data/Seq.fs").write_text(module_body, encoding="utf-8")
        return upstream

    def test_qualified_child_of_autoopen_parent_is_accepted(self):
        upstream = self.source_fixture(
            "namespace FSharpPlus.Data\n"
            "[<AutoOpen>]\nmodule SeqT_V2 =\n"
            "  module SeqT =\n    let mapM f xs = List.map f xs\n"
        )
        self.assert_succeeded(self.verify("absence", "--upstream", upstream))

    def test_same_file_global_and_wrong_module_definitions_are_rejected(self):
        baseline = (
            "namespace FSharpPlus.Data\n"
            "module SeqT =\n    let mapM f xs = List.map f xs\n"
        )
        additions = [
            "\n[<AutoOpen>]\nmodule Global =\n    let mapM f xs = List.map f xs\n",
            "\nmodule Other =\n    let mapM f xs = List.map f xs\n",
            "\nnamespace OtherNamespace\nmodule SeqT =\n    let mapM f xs = List.map f xs\n",
        ]
        for addition in additions:
            with self.subTest(addition=addition):
                upstream = self.source_fixture(baseline + addition)
                result = self.verify("absence", "--upstream", upstream)
                self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
                self.assertIn("QUALIFIED-ONLY BROKEN", result.stdout)

    def test_autoopen_on_expected_module_is_rejected(self):
        upstream = self.source_fixture(
            "namespace FSharpPlus.Data\n"
            "[<AutoOpen>]\nmodule SeqT =\n    let mapM f xs = List.map f xs\n"
        )
        result = self.verify("absence", "--upstream", upstream)
        self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("QUALIFIED-ONLY BROKEN", result.stdout)

    def snippet(self, body):
        (self.root / "reference").mkdir(exist_ok=True)
        (self.root / "reference/example.md").write_text(
            "```fsharp verify name=runtime_regression\nopen FSharpPlus\n" + body + "\n```\n",
            encoding="utf-8",
        )

    def execute_both_targets(self):
        out = self.root / "generated"
        self.assert_succeeded(self.verify("snippets", "--out", out, "--fsi"))
        self.assert_succeeded(self.run_command("build", out / "verify.fsproj", "-c", "Release", "--nologo"))
        return [
            self.run_command("run", "--project", out / "verify.fsproj", "-c", "Release", "--no-build"),
            self.run_command("fsi", "--nologo", "--warnaserror:64,44", out / "verify.fsx"),
        ]

    def test_wrong_whitespace_inside_string_fails_both_targets(self):
        self.snippet('let value : string = "two  spaces"\n// val value : string = "two spaces"')
        for result in self.execute_both_targets():
            self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("1 failed", result.stdout)

    def test_escaped_strings_and_formatting_whitespace_pass_both_targets(self):
        self.snippet(
            r'let value : string = "quoted \"two  spaces\" and \\ slash"' + "\n"
            + r'// val value : string = "quoted "two  spaces" and \ slash"' + "\n"
            + 'let values : int list = [1; 2]\n// val values : int list = [1;   2]'
        )
        for result in self.execute_both_targets():
            self.assert_succeeded(result)
            self.assertIn("2 passed", result.stdout)

    def test_caches_worktrees_and_skill_are_not_reference_inputs(self):
        self.snippet("let value : int = 1\n// val value : int = 1")
        for directory in [".upstream", ".worktrees/task", "build", "evals/workspace", "fsharpplus/references"]:
            target = self.root / directory
            target.mkdir(parents=True, exist_ok=True)
            (target / "foreign.md").write_text(
                "Unknown.fs:999\n```fsharp verify name=foreign\nnot valid F#\n```\n"
            )
        result = self.verify("snippets", "--lint-only")
        self.assert_succeeded(result)
        self.assertIn("found 1 verifiable snippet(s)", result.stdout)


if __name__ == "__main__":
    unittest.main()
