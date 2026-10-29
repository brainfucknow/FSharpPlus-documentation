#!/usr/bin/env python3
"""Compile every F# example in the FSharpPlus skill.

Fenced examples run in isolation. The one-line examples in the generic-function
lookup run together because each is a complete top-level binding. Blocks marked
"Intentionally failing" must fail, and their FS diagnostic must match the error
recorded immediately below the block. A line `// expect: text` inside a block
requires `text` to appear in the script's output, so prose claims about values
are checked, not only compilation.
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
import tempfile
from pathlib import Path

PACKAGE_REFERENCE = '#r "nuget: FSharpPlus, 1.9.1"\n'
FENCE = re.compile(r"```fsharp\n(.*?)```", re.DOTALL)
ERROR = re.compile(r"error (FS\d{4})")
EXPECT = re.compile(r"^\s*// expect: (.*\S)\s*$", re.MULTILINE)
ROOT = Path(__file__).resolve().parents[1]
SKILL = ROOT / "fsharpplus"


def run_fsi(dotnet: str, source: str, label: str, should_fail: bool, error: str | None) -> bool:
    if '#r "nuget:' not in source:
        source = PACKAGE_REFERENCE + source
    with tempfile.NamedTemporaryFile("w", suffix=".fsx", delete=False) as script:
        script.write(source)
        path = Path(script.name)
    try:
        result = subprocess.run(
            [dotnet, "fsi", "--exec", str(path)],
            capture_output=True,
            text=True,
            timeout=90,
            check=False,
        )
    finally:
        path.unlink(missing_ok=True)

    output = result.stdout + result.stderr
    passed = result.returncode != 0 if should_fail else result.returncode == 0
    if error is not None:
        passed = passed and error in ERROR.findall(output)
    expected_lines = EXPECT.findall(source)
    missing = [line for line in expected_lines if line not in output]
    passed = passed and not missing
    status = "PASS" if passed else "FAIL"
    expectation = f"expected {error}" if error else "expected failure" if should_fail else "expected success"
    if expected_lines:
        expectation += f", {len(expected_lines) - len(missing)}/{len(expected_lines)} expected lines"
    print(f"{status}: {label} ({expectation}, exit {result.returncode})")
    if not passed:
        print(output.rstrip(), file=sys.stderr)
    return passed


def fenced_cases() -> list[tuple[str, str, bool, str | None]]:
    cases = []
    for document in sorted(SKILL.rglob("*.md")):
        text = document.read_text(encoding="utf-8")
        for number, match in enumerate(FENCE.finditer(text), start=1):
            before = text[: match.start()].rstrip()
            should_fail = before.endswith("**Intentionally failing:**")
            expected_error = None
            if should_fail:
                following = text[match.end() :]
                diagnostic = ERROR.search(following)
                if diagnostic is None:
                    raise ValueError(f"{document}: failing block {number} has no captured FS diagnostic")
                expected_error = diagnostic.group(1)
            label = f"{document.relative_to(ROOT)} fenced block {number}"
            cases.append((label, match.group(1), should_fail, expected_error))
    return cases


def generic_table_case() -> tuple[str, str, bool, None]:
    document = SKILL / "references" / "generic-functions.md"
    examples = []
    for line in document.read_text(encoding="utf-8").splitlines():
        if not line.startswith("| `"):
            continue
        cells = re.split(r"(?<!\\)\|", line)
        if len(cells) >= 6 and cells[4].strip().startswith("`"):
            example = cells[4].strip().removeprefix("`").removesuffix("`")
            examples.append(example.replace(r"\|", "|"))
    if not examples:
        raise ValueError("generic-functions.md contains no one-line table examples")
    source = PACKAGE_REFERENCE + "open FSharpPlus\n" + "\n".join(examples) + "\n"
    return (f"{document.relative_to(ROOT)} one-line table examples", source, False, None)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dotnet", default="dotnet", help="dotnet executable (default: dotnet)")
    args = parser.parse_args()
    cases = [generic_table_case(), *fenced_cases()]
    results = [run_fsi(args.dotnet, source, label, should_fail, error) for label, source, should_fail, error in cases]
    failures = len(results) - sum(results)
    print(f"Verified {len(results)} example groups: {sum(results)} passed, {failures} failed.")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
