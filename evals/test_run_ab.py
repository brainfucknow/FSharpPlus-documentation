import json
import os
import subprocess
import sys
import tempfile
import textwrap
import unittest
from pathlib import Path

from evals.run_ab import codex_command, codex_result, wilson_interval


ROOT = Path(__file__).resolve().parents[1]
RUNNER = ROOT / "evals" / "run_ab.py"
CHECKS = (
    "Node (Leaf 2, Node (Leaf 3, Leaf 4))",
    "Some (Node (Leaf 1, Node (Leaf 2, Leaf 3)))",
    "None",
)


class RunAbTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.bin = self.root / "bin"
        self.workspace = self.root / "workspace"
        self.bin.mkdir()
        self.write_executable("claude", self.agent_script("claude"))
        self.write_executable("codex", self.agent_script("codex"))
        self.write_executable(
            "dotnet",
            f"""#!/usr/bin/env python3
import os
import sys
print({CHECKS!r}[0])
print({CHECKS!r}[1])
print({CHECKS!r}[2])
if os.environ.get("FAKE_COMPILE_FAIL"):
    print("error FS0001: fake compile failure", file=sys.stderr)
raise SystemExit(1 if os.environ.get("FAKE_COMPILE_FAIL") else 0)
""",
        )
        self.env = os.environ.copy()
        self.env["PATH"] = f"{self.bin}{os.pathsep}{self.env.get('PATH', '')}"

    def tearDown(self):
        self.temporary.cleanup()

    def write_executable(self, name, source):
        path = self.bin / name
        path.write_text(source)
        path.chmod(0o755)

    def agent_script(self, agent):
        solution = "\n".join(f'printfn "%s" "{check}"' for check in CHECKS) + "\n"
        output = (
            "print(json.dumps({'usage': {'input_tokens': 4, 'output_tokens': 2}, "
            "'total_cost_usd': 0.01, 'result': 'done'}))"
            if agent == "claude"
            else "print(json.dumps({'type': 'item.completed', 'item': {'type': 'agent_message', 'text': 'done'}})); "
            "print(json.dumps({'type': 'turn.completed', 'usage': {'input_tokens': 4, 'output_tokens': 2}}))"
        )
        prompt = "sys.argv[sys.argv.index('-p') + 1]" if agent == "claude" else "sys.argv[-1]"
        return textwrap.dedent(
            f"""\
            #!/usr/bin/env python3
            import json
            import os
            import pathlib
            import re
            import sys
            prompt = {prompt}
            if os.environ.get("FAKE_AGENT_FAIL"):
                {output}
                raise SystemExit(7)
            path = re.search(r"script to `([^`]+)`", prompt).group(1)
            pathlib.Path(path).write_text({solution!r})
            {output}
            """
        )

    def run_runner(self, *arguments, env=None):
        return subprocess.run(
            [sys.executable, str(RUNNER), "--workspace", str(self.workspace), *arguments],
            cwd=ROOT,
            env=env or self.env,
            capture_output=True,
            text=True,
            check=False,
        )

    def test_repeats_create_complete_run_directories(self):
        completed = self.run_runner("--only", "extend-tree", "--repeats", "3")
        self.assertEqual(0, completed.returncode, completed.stderr)
        required = {"solution.fsx", "run.json", "compile.log", "grading.json"}
        for config in ("with_skill", "baseline"):
            config_dir = self.workspace / "extend-tree" / config
            self.assertEqual(["run-1", "run-2", "run-3"], sorted(path.name for path in config_dir.iterdir()))
            for run_dir in config_dir.iterdir():
                self.assertTrue(required <= {path.name for path in run_dir.iterdir()})

    def test_agent_failure_writes_missing_grade(self):
        env = self.env | {"FAKE_AGENT_FAIL": "1"}
        completed = self.run_runner("--only", "extend-tree", "--configs", "baseline", env=env)
        self.assertEqual(0, completed.returncode, completed.stderr)
        grading = json.loads((self.workspace / "extend-tree/baseline/run-1/grading.json").read_text())
        self.assertTrue(grading["missing"])
        self.assertTrue(any("claude exit 7" in error for error in grading["errors"]))

    def test_compile_failure_does_not_pass(self):
        env = self.env | {"FAKE_COMPILE_FAIL": "1"}
        completed = self.run_runner("--only", "extend-tree", "--configs", "with_skill", "--verbose", env=env)
        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("with_skill: 0/1 runs fully passing", completed.stdout)
        grading = json.loads((self.workspace / "extend-tree/with_skill/run-1/grading.json").read_text())
        self.assertFalse(grading["compiled"])
        self.assertEqual(grading["passed"], grading["total"])
        self.assertEqual(["FS0001"], grading["errors"])
        self.assertRegex(completed.stdout, r"extend-tree\s+with_skill\s+run-1\s+FAILED\s+3/3\s+.*FS0001")

    def test_grade_only_ignores_repeats(self):
        generated = self.run_runner("--only", "extend-tree", "--configs", "baseline", "--repeats", "3")
        self.assertEqual(0, generated.returncode, generated.stderr)
        for grading in self.workspace.glob("extend-tree/baseline/run-*/grading.json"):
            grading.unlink()
        graded = self.run_runner(
            "--only", "extend-tree", "--configs", "baseline", "--grade-only", "--repeats", "1"
        )
        self.assertEqual(0, graded.returncode, graded.stderr)
        self.assertIn("baseline: 3/3 runs fully passing", graded.stdout)
        self.assertEqual(3, len(list(self.workspace.glob("extend-tree/baseline/run-*/grading.json"))))

    def test_empty_grade_only_config_reports_no_runs(self):
        config_dir = self.workspace / "extend-tree/with_skill"
        config_dir.mkdir(parents=True)
        completed = self.run_runner("--only", "extend-tree", "--configs", "with_skill", "--grade-only")
        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("with_skill: no runs", completed.stdout)
        self.assertIn(f"warning: no run-* directories in {config_dir}", completed.stderr)

    def test_smaller_repeat_batch_removes_stale_runs(self):
        first = self.run_runner("--only", "extend-tree", "--configs", "baseline", "--repeats", "3")
        self.assertEqual(0, first.returncode, first.stderr)
        second = self.run_runner("--only", "extend-tree", "--configs", "baseline", "--repeats", "2")
        self.assertEqual(0, second.returncode, second.stderr)
        config_dir = self.workspace / "extend-tree/baseline"
        self.assertEqual(["run-1", "run-2"], sorted(path.name for path in config_dir.iterdir()))
        self.assertIn(f"removed {config_dir / 'run-3'}", second.stdout)

    def test_codex_result_uses_final_message_and_completed_usage(self):
        stdout = "\n".join(
            json.dumps(event)
            for event in (
                {"type": "item.completed", "item": {"type": "agent_message", "text": "first"}},
                {"type": "item.completed", "item": {"type": "agent_message", "text": "last"}},
                {"type": "turn.completed", "usage": {"input_tokens": 8, "output_tokens": 3}},
            )
        )
        result = codex_result(stdout)
        self.assertEqual("last", result["reply"])
        self.assertEqual({"input_tokens": 8, "output_tokens": 3}, result["usage"])
        self.assertIsNone(result["cost_usd"])
        self.assertNotIn("--add-dir", codex_command("prompt", None, "with_skill"))

    def test_wilson_intervals(self):
        for passes, total, expected in (
            (3, 3, (1.0, 0.4385, 1.0)),
            (0, 3, (0.0, 0.0, 0.5615)),
            (50, 100, (0.5, 0.4038, 0.5962)),
        ):
            actual = wilson_interval(passes, total)
            self.assertEqual(expected, tuple(round(value, 4) for value in actual))


if __name__ == "__main__":
    unittest.main()
