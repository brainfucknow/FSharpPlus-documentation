# FSharpPlus-documentation

Work toward making [F#+](https://github.com/fsprojects/FSharpPlus) documentation sufficient for
reliable LLM output — treating the F# compiler as the oracle that decides what gets published.

Targets **FSharpPlus 1.9.1**. Source citations refer to the release commit recorded in
[`upstream.json`](upstream.json), rather than a moving `master` branch.

## Contents

| File | What it is |
|---|---|
| [`FINDINGS.md`](FINDINGS.md) | The plan's premises re-verified against upstream source, with corrections and a revised sequencing. **Read this first.** |
| [`CHEATSHEET.md`](CHEATSHEET.md) | Compact, paste-able context artifact: opens, CE table, operators, absent names |
| [`reference/required-opens.md`](reference/required-opens.md) | Which `open` each feature area needs, and which modules are `[<AutoOpen>]` |
| [`reference/computation-expressions.md`](reference/computation-expressions.md) | Every CE spelling and the builder it resolves to, incl. the `zapp` rename |
| [`reference/operators.md`](reference/operators.md) | Operator table with full signatures and argument order |
| [`reference/disambiguation.md`](reference/disambiguation.md) | Names that do **not** exist in F#+, and mappings from Haskell / Aether / FsToolkit |
| [`reference/generic-functions.md`](reference/generic-functions.md) | Verified examples for public functions with **no upstream example** — `sort`, `groupBy`, `item1`–`5`, `lift3`, `gets`, … |
| [`AGENTS.md`](AGENTS.md) | Rules for coding agents working in F#+ codebases |
| [`corpus/verified-snippets.jsonl`](corpus/verified-snippets.jsonl) | The verified corpus as JSONL — opens, code, expected type and value — for RAG, few-shot or fine-tuning |
| [`patch/`](patch/) | A five-patch series applying these findings to `fsprojects/FSharpPlus` itself, built and validated against the pinned base |
| [`fsharpplus/SKILL.md`](fsharpplus/SKILL.md) | Task-oriented skill, practical recipes, and compiler-diagnostic guidance |
| [`evals/README.md`](evals/README.md) | Optional model A/B evaluations and the evaluation-runner test commands |

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

## The measured coverage gap

The `gap` command of [`tools/Verify`](tools/Verify) computes the Step 3 work-list rather than curating it:

Run `gap --upstream .upstream --covered-by reference/generic-functions.md` after building the tool
to report the function count, the upstream gap, and the examples supplied by this repository for the
current pin. CI checks the gap against its recorded baseline.

**Zero mentions** means no example, no prose, not one passing reference. The real gap is larger — a
single mention is not an example — so this understates it. And the missing functions are not obscure:
`sort`, `sortBy`, `distinct`, `groupBy`, `maxBy`, `minBy`, `forall`, `scan`, `sumBy`, and the entire
`item1`–`item5` / `mapItem1`–`mapItem5` family.

"Covered" here means *appears inside a compiled-and-executed snippet* — prose mentions do not count, so
the number cannot be inflated by writing about a function instead of demonstrating it.

## Verification

The source reference uses [`tools/Verify`](tools/Verify), an F# project, for citation checks and
tagged examples. [`.github/workflows/verify.yml`](.github/workflows/verify.yml) runs these checks
alongside the separate skill-example verifier and Python evaluation-runner tests described below.
The source-reference jobs are:

| Job | What it proves | Blocking |
|---|---|---|
| `claims` | Every `file:line` citation resolves **and still contains the substring we claim**, against upstream at the pinned SHA. Every API presence/absence claim holds. | yes |
| `snippets` | Every ```` ```fsharp verify ```` block **compiles and executes** against FSharpPlus 1.9.1 from NuGet, and its `// val` lines are asserted. | yes |
| `fsi` | The same snippets executed under **`dotnet fsi`** — a second, independent target, because PR #372 showed FSI and project compilation disagree on SRTP-heavy code. | yes |
| `drift` | The same claim checks against upstream `master`, weekly, advisory — so a stale pin surfaces as news rather than as a wrong table. | no |

Run the full local pipeline with [`tools/verify.sh`](tools/verify.sh), including skill examples and
evaluation-runner tests, or drive the source-reference tool directly:

```sh
dotnet build tools/Verify/Verify.fsproj -c Release
V="dotnet tools/Verify/bin/Release/net8.0/verify.dll"

$V claims   --upstream .upstream          # citations, API claims, snippet lint, corpus freshness
$V gap      --upstream .upstream --covered-by reference/generic-functions.md
$V snippets --out build/verify --fsi      # generate both verification targets
$V corpus   --check                       # is the committed JSONL current?
```

A .NET SDK is required for all `tools/Verify` commands, including claim checks. The Python
evaluation-runner unit tests do not require .NET; compiling skill examples does.

### Running the checks locally in a cloud container

Use .NET SDK **8.0.425**, the version recorded for the skill's compiler-diagnostic expectations, and
Python 3. Install .NET through Microsoft's official installer or the supported package repository
for your operating system; package availability differs between Debian and Ubuntu. Check
`dotnet --version` before validation. A newer major SDK can change the diagnostics expected by the
intentionally failing examples even when normal snippets still compile.

Preserve the environment's configured proxy, CA trust, TLS verification, and artifact checksums.
If an SDK or NuGet request fails, diagnose that destination's response and use the environment's
supported network settings. A denial in one cloud session does not establish that the same host is
blocked in another. The prepared cloud environment provides the SDK at
`/workspace/.tools/dotnet8/dotnet`; add that directory to PATH or pass it with the skill verifier's
`--dotnet` option.

Generated source-reference projects target **net8.0**. Building the upstream docs project additionally
needs `git submodule update --init` for `external/FSharp.TypeProviders.SDK`. Each verification runner
prints the assertion or example totals for the current checkout; use that run's output as evidence.

### Why the citations are pinned

Line numbers are only meaningful against a fixed commit, so [`upstream.json`](upstream.json) pins
the FSharpPlus 1.9.1 release commit. The `claims` job is a hard gate at that pin; `drift` reports
separately when `master` moves. Absence failures are the exception worth acting on immediately — if
upstream *adds* something we documented as non-existent, the prose is wrong regardless of the pin.

### What a `verify` snippet guarantees

`// val name : Type = value` is not a comment here. The tool refuses a claim whose snippet does not
annotate `let name : Type`, so **the compiler checks the type** — that is the plan's guardrail against a
snippet that compiles while silently demonstrating a monomorphic instantiation. The generated program
then compares `sprintf "%A" name` against the documented value, so **the runtime checks the result**.
`FS0064` ("less generic than indicated") and `FS0044` (obsolete) are escalated to errors, so a snippet
cannot quietly degrade or use a renamed API.

This is precisely the gap identified upstream: `FSharpPlus.Docs` proves the doc pages *compile* but its
entry point is `let main argv = 0`, so its 84 `// val` claims are never executed. Ours are.

### The gates were proven to fail

A gate that cannot go red is decoration, so each one was deliberately broken and observed failing:

| Gate | Injected fault | Observed |
|---|---|---|
| `FS0044` obsolete-as-error | a snippet using `applicative'` | `error FS0044: This construct is deprecated. This value is obsolete. Use zapp instead` → `Build FAILED, 0 Warning(s) 1 Error(s)` |
| Value assertion | `// val` claiming `[99; 99; 99]` for `map ((*) 2) [1; 2; 3]` | `FAIL negtest_wrong_value.wrongValue / expected: [99; 99; 99] / actual: [2; 4; 6]` → `19 passed, 1 failed`, exit 1 |
| Citation drift | a shifted + renamed copy of upstream | every affected assertion reported by broken-claim name, exit 1 |
| Absence claim | — | caught a real error unprompted: this repo claimed `memoize` exists when upstream defines only `memoizeN` |

The table records historical negative checks; its totals describe those fault-injection runs, not
the current corpus size. Rerun the checks after changing the verifier or examples.

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

## Skill documentation and evaluations

The task-oriented [FSharpPlus skill](fsharpplus/SKILL.md) and its references serve practical coding
tasks. The root [`reference/`](reference/) directory records source-backed API tables. Both target
FSharpPlus 1.9.1, but their executable examples use separate conventions and runners.

### Skill verification

Run `python3 scripts/verify_fsharpplus_examples.py` to compile every F# example in the skill. The verifier requires each normal example to compile, each intentionally failing SRTP example to fail with its documented F# diagnostic code, and every `// expect:` line inside an example to appear in that example's output. Pass `--dotnet /path/to/dotnet` when `dotnet` is not on `PATH`.

Keep plain `fsharp` fences and `// expect:` output claims in `fsharpplus/`. The root source-reference
examples use `fsharp verify name=...` fences with `// val` claims and are checked by `tools/Verify`.
One runner's passing result does not cover the other collection. Run both when changing shared API
guidance; run `python3 -m unittest evals.test_run_ab` when changing the evaluation runner.

### Model evaluation

Run `python3 evals/run_ab.py` to compare the same model with and without the skill on the tasks in `evals/evals.json`. See `evals/README.md` for the method and how to read the results.

See the [real-world use-case recipes](fsharpplus/references/real-world-use-cases.md) for practical, source-backed patterns.
