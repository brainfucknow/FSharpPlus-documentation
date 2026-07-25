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

## Verification

Every statement here is meant to be checkable by a machine, and CI checks it. Three jobs, in
[`.github/workflows/verify.yml`](.github/workflows/verify.yml):

| Job | What it proves | Blocking |
|---|---|---|
| `claims` | Every `file:line` citation resolves **and still contains the substring we claim**, against upstream at the pinned SHA. Every API presence/absence claim holds. | yes |
| `snippets` | Every ```` ```fsharp verify ```` block **compiles and executes** against FSharpPlus 1.9.1 from NuGet, and its `// val` lines are asserted. | yes |
| `drift` | The same claim checks against upstream `master`, weekly, advisory — so a stale pin surfaces as news rather than as a wrong table. | no |

Run it locally with [`tools/verify.sh`](tools/verify.sh) (`--no-dotnet` if you have no SDK; the claim
checks need only Python and a git clone).

### Why the citations are pinned

Line numbers are only meaningful against a fixed commit, so [`upstream.json`](upstream.json) pins
`44ebc378` (2026-02-13, release 1.9.1). The `claims` job is a hard gate at that pin; `drift` reports
separately when `master` moves. Absence failures are the exception worth acting on immediately — if
upstream *adds* something we documented as non-existent, the prose is wrong regardless of the pin.

### What a `verify` snippet guarantees

`// val name : Type = value` is not a comment here. The extractor refuses a claim whose snippet does not
annotate `let name : Type`, so **the compiler checks the type** — that is the plan's guardrail against a
snippet that compiles while silently demonstrating a monomorphic instantiation. The generated program
then compares `sprintf "%A" name` against the documented value, so **the runtime checks the result**.
`FS0064` ("less generic than indicated") and `FS0044` (obsolete) are escalated to errors, so a snippet
cannot quietly degrade or use a renamed API.

This is precisely the gap identified upstream: `FSharpPlus.Docs` proves the doc pages *compile* but its
entry point is `let main argv = 0`, so its 84 `// val` claims are never executed. Ours are.

### Honest limits

- The reference tables are **source-derived**, not compiler-derived. Namespace and `[<AutoOpen>]`
  declarations, operator signatures, CE alias graphs and absence-of-definition claims are facts about
  source text, and that is what `claims` verifies. Only the `verify` snippets are compiler-verified.
- Snippets are checked against the **NuGet package**, which is what users consume. Upstream's harness
  uses a `ProjectReference`. The plan flags that these can diverge for inline/SRTP code across an
  assembly boundary; a second job building from source at the pin would close it and is not yet here.
- The compiler-gated steps of the plan — the failure taxonomy, the coverage loop, the pass@1 baseline —
  are still **not** done, and no measured error-frequency table has been invented in their place. CI now
  provides the oracle they need.
- The annotation guidance in `CHEATSHEET.md` remains a generalisation from examples, flagged inline as a
  heuristic rather than a verified contract.
