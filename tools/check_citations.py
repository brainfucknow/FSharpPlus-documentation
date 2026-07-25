#!/usr/bin/env python3
"""Verify every file:line citation in this repo's markdown against an upstream checkout.

Two levels of checking:

  1. Reachability - every `Foo.fs:123` citation names a file in tools/citation_map.json and
     points at a line that exists and is not blank. Catches line drift and typos.
  2. Meaning - every entry in tools/assertions.json still finds its expected substring at
     the cited line. A line existing is not the same as it still saying what we claimed;
     this is the check that actually defends the tables.

Usage:
    check_citations.py --upstream <path>            # hard gate, uses pinned sha
    check_citations.py --upstream <path> --warn-only # report but always exit 0

Exit code is 1 on any failure unless --warn-only.
"""
import argparse
import json
import pathlib
import re
import sys

# Matches `Builders.fs:257` and `Builders.fs:271-287`, but not bare `1.9.1` or `net8.0`.
CITATION = re.compile(r"\b([A-Za-z0-9_][A-Za-z0-9_.\-]*\.(?:fs|fsx|fsproj|md|sln|proj|yml|txt)):(\d+)(?:-(\d+))?\b")

# Citations pointing at this repo's own files are self-references, not upstream claims.
SELF = {"FINDINGS.md", "README.md", "CHEATSHEET.md"}

REPO = pathlib.Path(__file__).resolve().parent.parent


def load(path):
    with open(path, encoding="utf-8") as fh:
        return json.load(fh)


def markdown_files():
    return sorted(
        p for p in REPO.rglob("*.md")
        if ".git" not in p.parts and "build" not in p.parts
    )


def read_lines(base, relpath):
    path = base / relpath
    if not path.exists():
        return None
    with open(path, encoding="utf-8", errors="replace") as fh:
        return fh.readlines()


def check_reachability(base, filemap):
    """Every citation resolves to an existing, non-blank line."""
    failures = []
    checked = 0
    for md in markdown_files():
        text = md.read_text(encoding="utf-8")
        rel = md.relative_to(REPO)
        for m in CITATION.finditer(text):
            name, start, end = m.group(1), int(m.group(2)), m.group(3)
            if name in SELF:
                continue
            if name not in filemap:
                failures.append(f"{rel}: citation `{m.group(0)}` names a file absent from "
                                f"tools/citation_map.json - add it or fix the typo")
                continue
            lines = read_lines(base, filemap[name])
            if lines is None:
                failures.append(f"{rel}: `{m.group(0)}` -> {filemap[name]} does not exist upstream")
                continue
            checked += 1
            last = int(end) if end else start
            if start < 1 or last > len(lines):
                failures.append(f"{rel}: `{m.group(0)}` out of range "
                                f"({filemap[name]} has {len(lines)} lines)")
                continue
            if not "".join(lines[start - 1:last]).strip():
                failures.append(f"{rel}: `{m.group(0)}` points at blank line(s) in {filemap[name]}")
    return checked, failures


def check_assertions(base, filemap, assertions):
    """Every asserted substring is still present at its cited line."""
    failures = []
    for a in assertions:
        name, line, want, claim = a["file"], a["line"], a["contains"], a.get("claim", "")
        if name not in filemap:
            failures.append(f"assertion for {name}:{line} - file not in citation_map.json")
            continue
        lines = read_lines(base, filemap[name])
        if lines is None:
            failures.append(f"{name}:{line} - {filemap[name]} missing upstream")
            continue
        if line < 1 or line > len(lines):
            failures.append(f"{name}:{line} out of range ({len(lines)} lines) - claim: {claim}")
            continue
        actual = lines[line - 1]
        if want not in actual:
            failures.append(
                f"{name}:{line} no longer contains {want!r}\n"
                f"      claim broken: {claim}\n"
                f"      line now says: {actual.strip()[:100]}"
            )
    return len(assertions), failures


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--upstream", required=True, help="path to an FSharpPlus checkout")
    ap.add_argument("--warn-only", action="store_true", help="report failures but exit 0")
    args = ap.parse_args()

    base = pathlib.Path(args.upstream).resolve()
    if not (base / "src" / "FSharpPlus").is_dir():
        print(f"error: {base} does not look like an FSharpPlus checkout", file=sys.stderr)
        return 2

    filemap = load(REPO / "tools" / "citation_map.json")["files"]
    assertions = load(REPO / "tools" / "assertions.json")["assertions"]
    pin = load(REPO / "upstream.json")

    print(f"upstream:  {base}")
    print(f"pinned at: {pin['sha'][:12]} ({pin['sha_date']}, release {pin['release']})")
    print()

    n_reach, reach_fail = check_reachability(base, filemap)
    n_assert, assert_fail = check_assertions(base, filemap, assertions)

    print(f"reachability: {n_reach} citations checked, {len(reach_fail)} failed")
    print(f"assertions:   {n_assert} assertions checked, {len(assert_fail)} failed")

    failures = reach_fail + assert_fail
    if failures:
        print(f"\n{len(failures)} problem(s):\n")
        for f in failures:
            print(f"  - {f}")
        print("\nIf upstream moved intentionally, update the citation, the assertion, and the")
        print("prose it backs - then bump 'sha' in upstream.json.")
        return 0 if args.warn_only else 1

    print("\nAll citations resolve and all asserted content is intact.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
