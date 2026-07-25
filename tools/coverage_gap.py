#!/usr/bin/env python3
"""Report which public F#+ functions have no example anywhere in the upstream docs.

This is the work-list for the plan's Step 3 ("for every public function with no example").
It is computed, not curated, so it cannot drift out of date the way a hand-kept TODO list would.

Two populations are compared:

  * every named function defined in the AutoOpen `Operators` module - these are what
    `open FSharpPlus` puts in scope, so they are what a model will reach for
  * every mention of that name, in any form, across docsrc/content/*.fsx

A name with zero mentions has no example, no prose, nothing. Note the converse is weaker: a
single mention is not an example, so this UNDERSTATES the real documentation gap.

Usage:
    coverage_gap.py --upstream <path>              # print the gap
    coverage_gap.py --upstream <path> --covered-by reference/generic-functions.md
                                                   # also report what THIS repo now covers
    coverage_gap.py --upstream <path> --max-gap 53 # fail if upstream's gap grows past a baseline
"""
import argparse
import pathlib
import re
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent

# `let inline foo` / `let foo` at module-member indentation. Operator definitions like
# `let inline (<*>)` are deliberately excluded - they are covered by reference/operators.md.
DEF = re.compile(r"^    let (?:inline )?(\w[\w']*)\b")


def operator_module_functions(base):
    path = base / "src" / "FSharpPlus" / "Operators.fs"
    names = {}
    with open(path, encoding="utf-8", errors="replace") as fh:
        for lineno, line in enumerate(fh, 1):
            m = DEF.match(line)
            if m:
                names.setdefault(m.group(1), lineno)
    return names


def doc_text(base):
    parts = []
    for path in sorted((base / "docsrc" / "content").glob("*.fsx")):
        parts.append(path.read_text(encoding="utf-8", errors="replace"))
    return "\n".join(parts)


def mentioned(text, name):
    return re.search(r"\b%s\b" % re.escape(name), text) is not None


VERIFY_FENCE = re.compile(r"^```fsharp\s+verify\b", re.M)
# Mirrors tools/extract_snippets.py: a 4+-backtick fence wraps markdown that shows snippet
# syntax rather than being a snippet, so it must not count toward coverage either.
OUTER_FENCE = re.compile(r"^````+\s*\w*\s*$")


def verified_snippet_bodies(text):
    """Only code inside ```fsharp verify blocks counts as coverage.

    Naming a function in prose is not documenting it - and this file's own "still uncovered"
    table names every function it does NOT cover, so a plain substring search over the whole
    page would score those as covered. Coverage means: it appears in a snippet the compiler
    and the value assertions have actually run.
    """
    out = []
    lines = text.splitlines()
    i = 0
    in_outer = False
    while i < len(lines):
        if OUTER_FENCE.match(lines[i]):
            in_outer = not in_outer
            i += 1
            continue
        if in_outer:
            i += 1
            continue
        if VERIFY_FENCE.match(lines[i]):
            j = i + 1
            while j < len(lines) and lines[j].strip() != "```":
                j += 1
            out.extend(lines[i + 1:j])
            i = j + 1
        else:
            i += 1
    # Drop the // val claim lines: they restate the name but are the assertion, not the usage.
    return "\n".join(l for l in out if not re.match(r"\s*//", l))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--upstream", required=True)
    ap.add_argument("--covered-by", action="append", default=[],
                    help="repo-relative markdown whose verified snippets count as coverage")
    ap.add_argument("--max-gap", type=int, default=None,
                    help="exit 1 if the upstream gap exceeds this baseline")
    args = ap.parse_args()

    base = pathlib.Path(args.upstream).resolve()
    if not (base / "src" / "FSharpPlus").is_dir():
        print(f"error: {base} is not an FSharpPlus checkout", file=sys.stderr)
        return 2

    names = operator_module_functions(base)
    docs = doc_text(base)
    gap = sorted(n for n in names if not mentioned(docs, n))

    print(f"public named functions in the AutoOpen Operators module: {len(names)}")
    print(f"with zero mentions anywhere in upstream docsrc/content:   {len(gap)}"
          f"  ({100 * len(gap) // max(1, len(names))}%)")

    ours = ""
    for rel in args.covered_by:
        p = REPO / rel
        if p.exists():
            ours += verified_snippet_bodies(p.read_text(encoding="utf-8")) + "\n"
    if args.covered_by:
        covered = [n for n in gap if mentioned(ours, n)]
        print(f"of those, now carrying a verified example in this repo:    {len(covered)}")
        print()
        print("covered here:")
        for n in covered:
            print(f"  + {n}  (Operators.fs:{names[n]})")
        print()
        print("still uncovered:")
        for n in gap:
            if n not in covered:
                print(f"  - {n}  (Operators.fs:{names[n]})")
    else:
        print()
        for n in gap:
            print(f"  - {n}  (Operators.fs:{names[n]})")

    if args.max_gap is not None and len(gap) > args.max_gap:
        print(f"\nFAIL: upstream gap {len(gap)} exceeds baseline {args.max_gap} - new public "
              f"functions shipped without docs.")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
