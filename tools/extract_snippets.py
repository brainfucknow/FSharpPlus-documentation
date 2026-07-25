#!/usr/bin/env python3
"""Extract verifiable F#+ snippets from markdown and generate a project that checks them.

This is the compiler-oracle half of CI. A fenced block tagged `fsharp verify` is a claim
this repo makes about F#+ behaviour, and it gets compiled and executed:

    ```fsharp verify name=map_over_list
    open FSharpPlus
    let doubled : int list = map ((*) 2) [1; 2; 3]
    // val doubled : int list = [2; 4; 6]
    ```

Two things are enforced, matching the guardrails the plan asks for:

  * The compiler checks the TYPE, because `// val name : T = v` is only accepted when the
    snippet really annotates `let name : T`. A snippet that compiles but demonstrates a
    monomorphic instantiation of a generic function cannot hide - the annotation is the claim.
  * The generated program checks the VALUE at runtime via sprintf "%A", so a wrong documented
    result fails rather than sitting there as an inert comment. (Upstream's 84 `// val` comments
    are never executed; that is the gap this closes for our own corpus.)

Lint runs with no toolchain, so the structural rules hold even where dotnet is unavailable.

Usage:
    extract_snippets.py --out build/verify [--lint-only]
"""
import argparse
import json
import pathlib
import re
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent

FENCE = re.compile(r"^```fsharp\s+verify(?P<attrs>[^\n]*)$", re.M)
# A 4+-backtick fence wraps markdown that *shows* snippet syntax rather than being one.
# Without this, documenting the convention silently adds a snippet to the corpus.
OUTER_FENCE = re.compile(r"^````+\s*\w*\s*$")
VAL = re.compile(r"^\s*//\s*val\s+(?P<name>[A-Za-z_][A-Za-z0-9_']*)\s*:\s*(?P<type>[^=]+?)\s*=\s*(?P<value>.+?)\s*$")
IDENT = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")


class Snippet:
    def __init__(self, name, source_file, line, body):
        self.name = name
        self.source_file = source_file
        self.line = line
        self.body = body
        self.claims = []  # (name, type, expected)

    @property
    def module(self):
        return "Verify" + "".join(p.title() for p in re.split(r"[^A-Za-z0-9]+", self.name) if p)


def parse_markdown(path):
    text = path.read_text(encoding="utf-8")
    lines = text.splitlines()
    out = []
    i = 0
    auto = 0
    in_outer = False
    while i < len(lines):
        if OUTER_FENCE.match(lines[i]):
            in_outer = not in_outer
            i += 1
            continue
        if in_outer:
            i += 1
            continue
        m = FENCE.match(lines[i])
        if not m:
            i += 1
            continue
        attrs = dict(
            kv.split("=", 1) for kv in m.group("attrs").split() if "=" in kv
        )
        start = i + 1
        j = start
        while j < len(lines) and lines[j].strip() != "```":
            j += 1
        body = lines[start:j]
        auto += 1
        name = attrs.get("name") or f"{path.stem.replace('-', '_')}_{auto}"
        out.append(Snippet(name, path.relative_to(REPO).as_posix(), start + 1, body))
        i = j + 1
    return out


def collect():
    snippets = []
    for md in sorted(REPO.rglob("*.md")):
        if ".git" in md.parts or "build" in md.parts:
            continue
        snippets.extend(parse_markdown(md))
    return snippets


def lint(snippets):
    problems = []
    seen = {}
    for s in snippets:
        where = f"{s.source_file}:{s.line} ({s.name})"

        if not IDENT.match(s.module.replace("Verify", "") or "x"):
            problems.append(f"{where}: name does not produce a usable module identifier")
        if s.module in seen:
            problems.append(f"{where}: duplicate snippet name, already used at {seen[s.module]}")
        seen[s.module] = where

        text = "\n".join(s.body)
        if not re.search(r"^\s*open\s+FSharpPlus", text, re.M):
            problems.append(
                f"{where}: no `open FSharpPlus...` line. Every snippet must carry its own opens - "
                f"models copy the snippet, not the page."
            )

        for raw in s.body:
            m = VAL.match(raw)
            if not m:
                continue
            name, typ, value = m.group("name"), m.group("type").strip(), m.group("value").strip()
            s.claims.append((name, typ, value))
            # The plan's guardrail: the documented type must be a real annotation.
            pat = re.compile(
                r"^\s*let\s+(?:mutable\s+)?%s\s*:\s*(?P<ann>.+?)\s*=" % re.escape(name), re.M
            )
            found = pat.search(text)
            if not found:
                problems.append(
                    f"{where}: `// val {name} : {typ}` has no matching annotated binding. "
                    f"Write `let {name} : {typ} = ...` so the compiler checks the type."
                )
            elif " ".join(found.group("ann").split()) != " ".join(typ.split()):
                problems.append(
                    f"{where}: `// val {name} : {typ}` disagrees with the annotation "
                    f"`{found.group('ann').strip()}`"
                )

        if not s.claims:
            problems.append(
                f"{where}: no `// val name : Type = value` claim, so nothing is asserted. "
                f"Either add one or drop the `verify` tag."
            )
    return problems


def fs_string(s):
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'


def generate(snippets, out, package_version):
    out.mkdir(parents=True, exist_ok=True)
    for f in out.glob("*.fs"):
        f.unlink()

    files = []
    for s in snippets:
        path = out / f"{s.module}.fs"
        header = [
            f"// generated from {s.source_file}:{s.line} - do not edit",
            f"module {s.module}",
            "",
        ]
        path.write_text("\n".join(header + s.body) + "\n", encoding="utf-8")
        files.append(path.name)

    checks = []
    for s in snippets:
        for name, typ, expected in s.claims:
            checks.append(
                f"    check {fs_string(s.source_file + ':' + str(s.line))} "
                f"{fs_string(s.name + '.' + name)} "
                f"(sprintf \"%A\" {s.module}.{name}) {fs_string(expected)}"
            )

    program = f"""// generated - do not edit
module Program

open System
open System.Text.RegularExpressions

let mutable failures = 0
let mutable passes = 0

/// FSI's `val x : T = v` rendering and sprintf "%A" agree for the shapes we document,
/// but not on incidental whitespace, so compare normalised.
let private norm (s: string) = Regex.Replace(s.Trim(), @"\\s+", " ")

let check (where: string) (label: string) (actual: string) (expected: string) =
    if norm actual = norm expected then
        passes <- passes + 1
    else
        failures <- failures + 1
        printfn "FAIL %s" label
        printfn "     declared at %s" where
        printfn "     expected: %s" (norm expected)
        printfn "     actual:   %s" (norm actual)

[<EntryPoint>]
let main _ =
{chr(10).join(checks) if checks else "    ()"}
    printfn ""
    printfn "%d value assertion(s): %d passed, %d failed" (passes + failures) passes failures
    if failures > 0 then 1 else 0
"""
    (out / "Program.fs").write_text(program, encoding="utf-8")

    compiles = "\n".join(f'    <Compile Include="{f}" />' for f in files)
    proj = f"""<Project Sdk="Microsoft.NET.Sdk">
  <!-- generated by tools/extract_snippets.py - do not edit -->
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <!-- FS0064 = "this construct is less generic than indicated". The plan wants this as an
         error: it is the warning that fires when a generic example silently degrades to a
         monomorphic one. FS0044 = obsolete, so a doc snippet cannot use a renamed API. -->
    <WarningsAsErrors>FS0064;FS0044</WarningsAsErrors>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
{compiles}
    <Compile Include="Program.fs" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="FSharpPlus" Version="{package_version}" />
  </ItemGroup>
</Project>
"""
    (out / "verify.fsproj").write_text(proj, encoding="utf-8")
    return len(files), len(checks)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="build/verify")
    ap.add_argument("--lint-only", action="store_true")
    args = ap.parse_args()

    pin = json.load(open(REPO / "upstream.json", encoding="utf-8"))
    snippets = collect()
    print(f"found {len(snippets)} verifiable snippet(s)")

    problems = lint(snippets)
    total_claims = sum(len(s.claims) for s in snippets)
    print(f"{total_claims} value claim(s)")
    print(f"lint: {len(problems)} problem(s)")
    if problems:
        print()
        for p in problems:
            print(f"  - {p}")
        return 1

    if args.lint_only:
        print("\nlint clean (--lint-only, nothing generated)")
        return 0

    out = (REPO / args.out).resolve()
    n_files, n_checks = generate(snippets, out, pin["package_version"])
    print(f"\ngenerated {n_files} module(s) and {n_checks} check(s) into {out.relative_to(REPO)}")
    print(f"against FSharpPlus {pin['package_version']} from NuGet")
    return 0


if __name__ == "__main__":
    sys.exit(main())
