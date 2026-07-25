#!/usr/bin/env python3
"""Export the verified snippet corpus as JSONL for retrieval, few-shot prompting or fine-tuning.

The plan's Step 4: "the verified corpus as JSONL - signature, opens, code, expected type, expected
value - usable for RAG, few-shot, or fine-tuning by anyone."

Only `verify`-tagged snippets are exported, so every record has been compiled and executed. That is
the whole value proposition: a corpus of F#+ examples that are known to work, rather than scraped
text that may not.

The output is committed to the repo so consumers do not need to run the toolchain. CI re-runs this
with --check to guarantee the committed file matches the markdown it came from.

Usage:
    export_corpus.py                  # write corpus/verified-snippets.jsonl
    export_corpus.py --check          # exit 1 if the committed file is stale
"""
import argparse
import json
import pathlib
import re
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent
OUT = REPO / "corpus" / "verified-snippets.jsonl"

sys.path.insert(0, str(REPO / "tools"))
from extract_snippets import collect, lint  # noqa: E402

OPEN_LINE = re.compile(r"^\s*open\s+([\w.]+)", re.M)


def record(snippet, pin):
    body = "\n".join(snippet.body).strip()
    opens = OPEN_LINE.findall(body)
    # Code with the // val claim lines stripped - what a consumer should imitate.
    code = "\n".join(
        l for l in snippet.body if not re.match(r"\s*//\s*val\s", l)
    ).strip()
    return {
        "id": snippet.name,
        "source": f"{snippet.source_file}:{snippet.line}",
        "opens": opens,
        "code": code,
        "full_snippet": body,
        "claims": [
            {"binding": n, "type": t, "value": v} for n, t, v in snippet.claims
        ],
        "package": "FSharpPlus",
        "package_version": pin["package_version"],
        "upstream_sha": pin["sha"],
        "verified": {
            "compiled": True,
            "executed": True,
            "how": "compiled as an <Compile> item against the released NuGet package and the "
                   "documented values asserted at runtime via sprintf \"%A\"; "
                   "FS0064 and FS0044 escalated to errors",
        },
    }


def build():
    pin = json.load(open(REPO / "upstream.json", encoding="utf-8"))
    snippets = collect()
    problems = lint(snippets)  # populates .claims and validates annotations
    if problems:
        print(f"refusing to export: {len(problems)} lint problem(s)", file=sys.stderr)
        for p in problems:
            print(f"  - {p}", file=sys.stderr)
        return None
    lines = [json.dumps(record(s, pin), sort_keys=True) for s in sorted(snippets, key=lambda s: s.name)]
    return "\n".join(lines) + "\n"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true",
                    help="verify the committed corpus matches the markdown; do not write")
    args = ap.parse_args()

    content = build()
    if content is None:
        return 1

    n_records = content.count("\n")
    n_claims = sum(len(json.loads(l)["claims"]) for l in content.strip().split("\n"))

    if args.check:
        if not OUT.exists():
            print(f"FAIL: {OUT.relative_to(REPO)} is missing - run tools/export_corpus.py")
            return 1
        if OUT.read_text(encoding="utf-8") != content:
            print(f"FAIL: {OUT.relative_to(REPO)} is stale - run tools/export_corpus.py and commit")
            return 1
        print(f"corpus up to date: {n_records} records, {n_claims} verified claims")
        return 0

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(content, encoding="utf-8")
    print(f"wrote {OUT.relative_to(REPO)}: {n_records} records, {n_claims} verified claims")
    return 0


if __name__ == "__main__":
    sys.exit(main())
