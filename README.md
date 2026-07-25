# FSharpPlus-documentation

Work toward making [F#+](https://github.com/fsprojects/FSharpPlus) documentation sufficient for
reliable LLM output — treating the F# compiler as the oracle that decides what gets published.

Checked against `fsprojects/FSharpPlus` at `master`, **1.9.1 (January 2026)**.

## Contents

| File | What it is |
|---|---|
| [`FINDINGS.md`](FINDINGS.md) | The plan's premises re-verified against upstream source, with corrections and a revised sequencing. **Read this first.** |
| [`CHEATSHEET.md`](CHEATSHEET.md) | Compact, paste-able context artifact: opens, CE table, operators, absent names |
| [`reference/required-opens.md`](reference/required-opens.md) | Which `open` each feature area needs, and which modules are `[<AutoOpen>]` |
| [`reference/computation-expressions.md`](reference/computation-expressions.md) | Every CE spelling and the builder it resolves to, incl. the `zapp` rename |
| [`reference/operators.md`](reference/operators.md) | Operator table with full signatures and argument order |
| [`reference/disambiguation.md`](reference/disambiguation.md) | Names that do **not** exist in F#+, and mappings from Haskell / Aether / FsToolkit |

## Three findings that change the plan

1. **The compile harness already exists upstream.** `src/FSharpPlus.Docs/FSharpPlus.Docs.fsproj`
   compiles 64 of 67 doc pages as `<Compile>` items with a `ProjectReference`, and CI enforces it via
   `dotnet msbuild -target:AllDocs`. Spike 0 is answered — including its stated unknown: `#r` directives
   are tolerated in compiled `.fsx`, with no `#if` guard.
2. **Compilation is checked; values are not.** `Program.fs` is `let main argv = 0`. The 84
   `// val x : T = v` claims across the docs are inert comments. This — not the harness — is the real
   gap, and it is where "execute the value assertions" has to be built.
3. **The `zapp` discrepancy is one line, not a page.** `computation-expressions.fsx` has no applicative
   CE content at all; the sole obsolete use is `abstraction-zipapplicative.fsx:154`. Nothing catches it
   because the obsolete attribute is a warning and the Docs project sets no `TreatWarningsAsErrors`.

Also found: `index.fsx` — the front page, with real code examples — is one of the three pages **outside**
the compiler's reach. Adding three `<Compile>` lines is the cheapest correctness win in the whole plan.

## Verification status

Everything here is **source-derived and line-cited**, not compile-verified. No .NET SDK could be
installed in the authoring environment — `builds.dotnet.microsoft.com` is denied by network policy — so
the compiler-gated steps of the plan (the failure taxonomy, the coverage loop, the pass@1 baseline) are
**not** done, and no measured error-frequency table has been invented in their place.

What that constraint does *not* affect: namespace and `[<AutoOpen>]` declarations, operator signatures,
CE alias graphs, and absence-of-definition claims are all facts about the source text. Those are the
tables in `reference/`. Where a claim needed a compiler or a runtime, it is flagged inline — see the
annotation section of `CHEATSHEET.md`, which is explicitly marked as a heuristic rather than a verified
contract.
