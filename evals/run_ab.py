#!/usr/bin/env python3
"""Measure whether the fsharpplus skill improves generated F#+ code.

Each eval prompt runs through `claude -p` with the same model in two
configurations: one told to read the skill first and one without the skill.
Neither configuration may execute dotnet, so the comparison isolates what the
skill contributes as knowledge. Every solution.fsx is then compiled with
`dotnet fsi`, its output is checked against the eval's expected fragments, and
an aggregate table is printed.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor
from math import sqrt
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SKILL_DIR = ROOT / "fsharpplus"
INPUTS = ROOT / "evals" / "inputs"
CONFIGS = ("with_skill", "baseline")
ERROR = re.compile(r"error (FS\d{4})")

COMMON_RULES = (
    "\n\nWrite a single self-contained script to `{out}`. "
    "The dotnet SDK is not available to you: do not run dotnet, fsi, or any build. "
    "Do not read files outside the current directory{skill_clause}. "
    "Write the file, then reply with one sentence."
)


def build_prompt(eval_: dict, config: str, out: Path) -> str:
    skill_clause = ""
    prefix = ""
    if config == "with_skill":
        skill_clause = f" except the skill under `{SKILL_DIR}`"
        prefix = (
            f"Before doing anything else, read `{SKILL_DIR / 'SKILL.md'}` and follow it, "
            "including the reference files it routes you to for this task.\n\n"
        )
    return prefix + eval_["prompt"] + COMMON_RULES.format(out=out, skill_clause=skill_clause)


def run_claude(eval_: dict, config: str, run_dir: Path, model: str) -> dict:
    run_dir.mkdir(parents=True, exist_ok=True)
    for name in eval_.get("files", []):
        shutil.copy(INPUTS / name, run_dir / name)
    out = run_dir / "solution.fsx"
    command = [
        "claude", "-p", build_prompt(eval_, config, out),
        "--model", model,
        "--permission-mode", "acceptEdits",
        "--allowedTools", "Read,Write,Edit,Glob,Grep",
        "--output-format", "json",
    ]
    if config == "with_skill":
        command += ["--add-dir", str(SKILL_DIR)]
    env = {k: v for k, v in os.environ.items() if not k.startswith(("CLAUDE_CODE", "CLAUDECODE"))}
    started = time.time()
    completed = subprocess.run(
        command, cwd=run_dir, env=env, stdin=subprocess.DEVNULL,
        capture_output=True, text=True, timeout=1800, check=False,
    )
    record = {"duration_s": round(time.time() - started, 1), "exit": completed.returncode}
    try:
        payload = json.loads(completed.stdout)
        record["usage"] = payload.get("usage")
        record["cost_usd"] = payload.get("total_cost_usd")
        record["reply"] = payload.get("result")
    except json.JSONDecodeError:
        record["raw"] = completed.stdout[-2000:] + completed.stderr[-2000:]
    (run_dir / "run.json").write_text(json.dumps(record, indent=2))
    return record


def grade(eval_: dict, run_dir: Path, dotnet: str) -> dict:
    solution = run_dir / "solution.fsx"
    if not solution.exists():
        errors = []
        run_json = run_dir / "run.json"
        if run_json.exists():
            record = json.loads(run_json.read_text())
            if record.get("exit"):
                errors.append(f"claude exit {record['exit']}: {record.get('raw', '').strip()[:80]}")
        result = {"compiled": False, "missing": True, "passed": 0, "total": len(eval_["checks"]), "errors": errors}
        (run_dir / "grading.json").write_text(json.dumps(result, indent=2))
        return result
    completed = subprocess.run([dotnet, "fsi", "--exec", str(solution)], capture_output=True, text=True, timeout=600, check=False)
    output = completed.stdout + completed.stderr
    (run_dir / "compile.log").write_text(output)
    flat = re.sub(r"\s+", " ", output)
    passed = sum(1 for check in eval_["checks"] if re.sub(r"\s+", " ", check) in flat)
    result = {
        "compiled": completed.returncode == 0,
        "missing": False,
        "passed": passed,
        "total": len(eval_["checks"]),
        "errors": sorted(set(ERROR.findall(output))),
    }
    (run_dir / "grading.json").write_text(json.dumps(result, indent=2))
    return result


def wilson_interval(passes: int, total: int) -> tuple[float, float]:
    if total == 0:
        return 0.0, 0.0
    z = 1.96
    rate = passes / total
    denominator = 1 + z * z / total
    centre = (rate + z * z / (2 * total)) / denominator
    margin = z * sqrt(rate * (1 - rate) / total + z * z / (4 * total * total)) / denominator
    return centre - margin, centre + margin


def run_directories(workspace: Path, eval_: dict, config: str) -> list[Path]:
    config_dir = workspace / eval_["id"] / config
    return sorted(
        (path for path in config_dir.glob("run-*") if path.is_dir()),
        key=lambda path: (int(path.name[4:]) if path.name[4:].isdigit() else sys.maxsize, path.name),
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--model", default="sonnet", help="model passed to claude -p (default: sonnet)")
    parser.add_argument("--workspace", type=Path, default=ROOT / "evals" / "workspace", help="where runs are stored")
    parser.add_argument("--only", nargs="*", default=None, help="eval ids to run (default: all)")
    parser.add_argument("--configs", nargs="*", default=list(CONFIGS), choices=CONFIGS)
    parser.add_argument("--grade-only", action="store_true", help="skip generation; compile and grade existing runs")
    parser.add_argument("--repeats", type=int, default=1, help="runs per eval and configuration (default: 1)")
    parser.add_argument("--jobs", type=int, default=4, help="parallel claude runs")
    parser.add_argument("--dotnet", default="dotnet")
    args = parser.parse_args()
    if args.repeats < 1:
        parser.error("--repeats must be at least 1")

    evals = json.loads((ROOT / "evals" / "evals.json").read_text())["evals"]
    if args.only:
        evals = [e for e in evals if e["id"] in args.only]
    if args.grade_only:
        jobs = [
            (e, c, run_dir)
            for e in evals
            for c in args.configs
            for run_dir in run_directories(args.workspace, e, c)
        ]
    else:
        jobs = [
            (e, c, args.workspace / e["id"] / c / f"run-{run}")
            for e in evals
            for c in args.configs
            for run in range(1, args.repeats + 1)
        ]

    if not args.grade_only:
        with ThreadPoolExecutor(max_workers=args.jobs) as pool:
            list(pool.map(lambda job: run_claude(job[0], job[1], job[2], args.model), jobs))

    rows = [(e["id"], c, grade(e, d, args.dotnet), d) for e, c, d in jobs]
    widths = {config: max(18, len(config) + 2) for config in args.configs}
    print(f"{'eval':26}" + "".join(f"{config:{widths[config]}}" for config in args.configs))
    for eval_ in evals:
        cells = []
        for config in args.configs:
            mine = [g for eval_id, c, g, _ in rows if eval_id == eval_["id"] and c == config]
            full = sum(g["compiled"] and g["passed"] == g["total"] for g in mine)
            cells.append(f"{full}/{len(mine)}")
        print(f"{eval_['id']:26}" + "".join(f"{cell:{widths[config]}}" for cell, config in zip(cells, args.configs)))
    print()
    for config in args.configs:
        mine = [g for _, c, g, _ in rows if c == config]
        full = sum(g["compiled"] and g["passed"] == g["total"] for g in mine)
        low, high = wilson_interval(full, len(mine))
        rate = full / len(mine) if mine else 0.0
        print(f"{config}: {full}/{len(mine)} runs fully passing, {rate:.1%} ({low:.1%}-{high:.1%})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
