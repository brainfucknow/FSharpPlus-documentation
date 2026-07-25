#!/usr/bin/env python3
"""Verify the presence/absence API claims in reference/disambiguation.md.

Absence is the one class of claim that a grep can settle more firmly than a compiler can:
a compiler tells you a name failed to resolve in one context, whereas searching every
definition site tells you it is not defined anywhere. But absence is also the most fragile
claim, because any upstream addition falsifies it - hence checking it in CI per release.

We search for DEFINITION sites only (`let name`, `let inline name`, `let (op)`), so a name
that merely appears in a comment or an XML doc string does not count as existing.

Usage:
    check_absence.py --upstream <path> [--warn-only]
"""
import argparse
import json
import pathlib
import re
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent


def definition_sites(sources, name):
    """Return (path, lineno, line) for every definition of `name`."""
    esc = re.compile(
        r"^\s*let\s+(?:inline\s+)?(?:rec\s+)?%s\b"
        r"|^\s*let\s+(?:inline\s+)?\(\s*%s\s*\)" % (re.escape(name), re.escape(name))
    )
    hits = []
    for path, lines in sources:
        for i, line in enumerate(lines, 1):
            if esc.match(line):
                hits.append((path, i, line.strip()))
    return hits


def load_sources(base):
    root = base / "src" / "FSharpPlus"
    out = []
    for path in sorted(root.rglob("*.fs")):
        with open(path, encoding="utf-8", errors="replace") as fh:
            out.append((path.relative_to(base).as_posix(), fh.readlines()))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--upstream", required=True)
    ap.add_argument("--warn-only", action="store_true")
    args = ap.parse_args()

    base = pathlib.Path(args.upstream).resolve()
    if not (base / "src" / "FSharpPlus").is_dir():
        print(f"error: {base} is not an FSharpPlus checkout", file=sys.stderr)
        return 2

    claims = json.load(open(REPO / "tools" / "api_claims.json", encoding="utf-8"))
    sources = load_sources(base)
    print(f"scanned {len(sources)} source files under src/FSharpPlus\n")

    failures = []

    for c in claims["absent"]:
        hits = definition_sites(sources, c["name"])
        if hits:
            where = "; ".join(f"{p}:{n}" for p, n, _ in hits[:3])
            failures.append(
                f"ABSENCE BROKEN: `{c['name']}` is now DEFINED at {where}\n"
                f"      disambiguation.md says it does not exist (use {c['instead']} instead).\n"
                f"      Remove it from the absent list and from the prose."
            )

    for c in claims["present"]:
        if not definition_sites(sources, c["name"]):
            failures.append(
                f"PRESENCE BROKEN: `{c['name']}` has no definition site\n"
                f"      we claim it exists: {c['claim']}"
            )

    for c in claims["qualified_only"]:
        hits = definition_sites(sources, c["name"])
        if not hits:
            failures.append(f"`{c['name']}` has no definition at all - claim was: {c['claim']}")
        else:
            unexpected = [f"{p}:{n}" for p, n, _ in hits if p != c["expected_file"]]
            if unexpected:
                failures.append(
                    f"QUALIFIED-ONLY BROKEN: `{c['name']}` is now defined outside "
                    f"{c['expected_file']}: {'; '.join(unexpected)}\n"
                    f"      claim was: {c['claim']}"
                )

    n = len(claims["absent"]) + len(claims["present"]) + len(claims["qualified_only"])
    print(f"{len(claims['absent'])} absence claims, {len(claims['present'])} presence claims, "
          f"{len(claims['qualified_only'])} qualified-only claims  ({n} total)")
    print(f"{len(failures)} failed")

    if failures:
        print()
        for f in failures:
            print(f"  - {f}")
        return 0 if args.warn_only else 1

    print("\nAll API presence/absence claims hold.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
