# Findings: re-verifying the plan against upstream source

Everything below was checked against a fresh clone of `fsprojects/FSharpPlus` at `master`
(`RELEASE_NOTES.md` head = **1.9.1, January 11 2026**). Claims are cited as `file:line` so they can
be re-checked. Where the plan and the source disagree, the source wins and I say so.

Two environment facts shape what could and could not be done:

- **This repo (`FSharpPlus-documentation`) is empty** — `LICENSE`, `README.md`, `.gitignore` only.
  The plan's "verification notes" describe paths (`docsrc/content/`, `src/FSharpPlus.Samples`) that
  live in *upstream* `fsprojects/FSharpPlus`, not here.
- **No F# compiler is available and cannot be installed.** `builds.dotnet.microsoft.com` is denied by
  the environment's network policy (`403` to `CONNECT`, confirmed via the proxy status endpoint). This
  is a policy denial, not a transient failure. Every compiler-gated step is therefore blocked here —
  see [What is blocked](#what-is-blocked).

---

## The headline: Spike 0 is already solved upstream

The plan allocates ½ day to "pick the harness mechanism", lists three candidate mechanisms, and flags
option (1) as **unverified** — "Unknown: whether the compiler rejects `#r`/`#load` in a compiled
script even when preprocessed out."

**Option (1) already exists, is already wired to the documentation, and already runs in CI.**

`src/FSharpPlus.Docs/FSharpPlus.Docs.fsproj` does precisely what the plan proposed to build:

```xml
<Compile Include="Samples\*.fsx" />
<Compile Include="..\..\docsrc\content\tutorial.fsx">
  <Link>Docs\%(FileName)%(Extension)</Link>
</Compile>
<Compile Include="..\..\docsrc\content\type-*.fsx" ... />
<Compile Include="..\..\docsrc\content\abstraction-*.fsx" ... />
...
<ProjectReference Include="..\..\src\FSharpPlus\FSharpPlus.fsproj" />
```

It uses a `ProjectReference`, not a `PackageReference` — exactly the constraint the plan wanted
("examples must track the working tree").

`Program.fs` states the intent outright:

> `// This folder contains this project to compile the documentation files and samples and make sure they never break.`

And it is enforced, not aspirational:

- `.github/workflows/dotnetcore.yml:97-98` → `dotnet msbuild -target:AllDocs build.proj`
- `build.proj:22-26` → `AllDocs` runs `dotnet build FSharpPlus.sln -c Release`
- `FSharpPlus.sln:96` → `FSharpPlus.Docs` is a member of the solution

### Consequences that change the plan

1. **The `#if !COMPILED_DOCS` guard is unnecessary.** The doc scripts contain real `#r` directives —
   e.g. `docsrc/content/tutorial.fsx:5` and `index.fsx:4` both have
   `#r @"../../src/FSharpPlus/bin/Release/net8.0/FSharpPlus.dll"` — and they are compiled as
   `<Compile>` items today with no guard and no preprocessing step. The plan's stated unknown is
   answered: the compiler tolerates `#r` in a compiled `.fsx`.
2. **Fallbacks (2) and (3) are not needed as the primary mechanism.** Keep (3) (`dotnet fsi`) as the
   *secondary* job — that recommendation in the plan stands, and PR #372 is still the evidence for it.
3. **Step 3's infrastructure cost drops to roughly zero.** The coverage loop needs somewhere to put
   candidate snippets and a build that rejects the bad ones. Both exist. This is the plan's single
   biggest cost saving.

**Naming correction:** the plan cites `src/FSharpPlus.Samples`. The actual project is
`src/FSharpPlus.Docs`, and it is more than precedent — it already compiles the doc pages themselves,
not just standalone samples.

---

## Coverage is already 64 / 67 pages

Computed by expanding the `<Compile>` globs in the fsproj against `docsrc/content/*.fsx`:

| | Count |
|---|---|
| Doc pages (`docsrc/content/*.fsx`) | 67 |
| Compiled by `FSharpPlus.Docs` | 64 |
| **Not compiled** | **3** — `index.fsx`, `abstractions.fsx`, `types.fsx` |

The gap exists because the globs are `type-*.fsx` and `abstraction-*.fsx` plus an explicit list;
`abstractions.fsx` (plural) and `types.fsx` fall outside both, and `index.fsx` was never listed.

**`index.fsx` is the front page and it contains real, unverified code examples** — ~41 non-comment
lines, including the "Example 1 / Example 2" snippets that are the first F#+ code most readers (and
most scrapers) ever see. It is the highest-traffic page in the corpus and the only substantial one
outside the compiler's reach.

Adding three `<Compile>` lines is the cheapest correctness win available in this whole plan. It should
be done first, before any new prose. (`abstractions.fsx` and `types.fsx` are ~19 and ~43 lines of
mostly navigation prose, so they are near-free to add and may need nothing more than inclusion.)

---

## The real harness gap: compilation is checked, values are not

`src/FSharpPlus.Docs/Program.fs` in full:

```fsharp
open System

[<EntryPoint>]
let main argv =
    0
```

The project is an `Exe` whose entry point does nothing. So the harness proves the doc pages **compile**
and proves nothing about what they **evaluate to**.

There are **84 `// val ... = ...` claims** across `docsrc/content/*.fsx`. Every one is an inert comment.
Nothing compares them to a runtime value; a snippet whose documented result is wrong passes CI today.

This is the genuine, specific hole in the existing setup, and it is where the plan's Step 3 phrase
"execute the value assertions" actually has to be built. The plan assumed the doctest target existed
because the *convention* exists — the convention exists, the *check* does not.

There is also no warning gate. Neither `Directory.Build.props` nor either `.fsproj` sets
`TreatWarningsAsErrors`, `WarningsAsErrors`, or `NoWarn`. That matters concretely for two warnings the
plan wants to rely on:

- **`FS0064` ("less generic than indicated")** — the plan wants this as an error, because it is what
  fires when a generic example silently degrades to a monomorphic one. Today it is a warning that
  nothing surfaces.
- **`FS0044` (obsolete)** — which is why the item below has gone unnoticed.

---

## The `zapp` discrepancy: resolved, and narrower than described

The rename is real and is in `master`. `src/FSharpPlus/Builders.fs:271-287`:

```fsharp
[<ObsoleteAttribute("This value is obsolete. Use zapp instead.", false)>]
let applicative'<...>  = ZipApplicativeBuilder<...> ()
[<ObsoleteAttribute("This value is obsolete. Use zapp2 instead.", false)>]
let applicative2'<...> = ZipApplicativeBuilder2<...> ()
[<ObsoleteAttribute("This value is obsolete. Use zapp3 instead.", false)>]
let applicative3'<...> = ZipApplicativeBuilder3<...> ()

let zapp<...>  = ZipApplicativeBuilder<...> ()
let zapp2<...> = ZipApplicativeBuilder2<...> ()
let zapp3<...> = ZipApplicativeBuilder3<...> ()
```

The old names still work — they are `Obsolete(..., false)`, a warning rather than an error — so both
spellings compile, which is exactly the condition under which stale names survive in docs unnoticed.

**Correction to the plan.** The plan says the discrepancy is on `computation-expressions`. It is not.
In `master`:

- `docsrc/content/computation-expressions.fsx` contains **no** occurrence of `applicative'`,
  `applicative2'`, `applicative3'`, `zapp`, or even `applicative` — it documents only the monad CEs
  (`monad` at :5, `monad'` at :15, `monad.plus'` at :31 and :57, `monad.fx` at :47).
- The **only** obsolete CE use anywhere in `docsrc/` is
  **`docsrc/content/abstraction-zipapplicative.fsx:154`** — `let validated = applicative2' {`.

So the fix is a one-line change on one page, not a rewrite of the CE page. If the *published* site
still shows `applicative'` on `computation-expressions.html`, that is the published site being stale
relative to `master`, which answers the plan's secondary question — the site is not being rebuilt from
`master` on every release.

**Second correction.** The plan says to "include the FSI one-liner the docs already suggest for
probing strictness (`let _ : MyType<'t> = monad { printfn "I'm strict" }`)". No such line exists — the
string `I'm strict` appears **nowhere** in the upstream repository, and `computation-expressions.fsx`
contains no strictness probe at all. It is a good idea, but it has to be authored, not lifted.

---

## The CE surface is larger than the plan's corrected list

The plan lists four monad spellings. The source has more, because `.strict` appears on *both* builders.
From `src/FSharpPlus/Builders.fs:130-168`:

`MonadFxBuilder` exposes `.strict`, `.plus`, `.plus'`, `.fx` (returns `this`), `.fx'`.
`MonadPlusBuilder` **also** exposes `.strict` (`:133`).

That last one is what the plan missed: `monad.plus.strict` is a valid spelling, and it denotes the same
builder as `monad.plus'`. Full alias table in
[`reference/computation-expressions.md`](reference/computation-expressions.md).

---

## Corrections summary

| Plan claim | Verdict | Reality |
|---|---|---|
| Docs are literate `.fsx` rendered by `fsdocs` | ✅ | 67 pages in `docsrc/content/` |
| Examples use a `// val x : T = v` convention | ⚠️ partly | Convention yes (84 uses); **it is never checked** — `Program.fs` returns `0` and does nothing |
| "the doctest target already exists in the source" | ❌ | The *convention* exists; the *check* does not |
| A project keeps samples compiling: `src/FSharpPlus.Samples` | ⚠️ renamed & bigger | It is `src/FSharpPlus.Docs`, and it compiles 64 of 67 **doc pages**, not just samples |
| Spike 0 mechanism unverified; `#r` may be rejected | ❌ | Already solved upstream; `#r` is tolerated in compiled `.fsx`; no guard used |
| Multiple non-obvious `open`s needed | ✅ | Confirmed; `lens.fsx:197-199` documents this inline. Table derived in [`reference/required-opens.md`](reference/required-opens.md) |
| FSI and project compilation can disagree | ✅ (unchanged) | PR #372 evidence stands; keep `dotnet fsi` as a second job |
| `zapp` rename happened | ✅ | `Builders.fs:270-287`; old names are `Obsolete` warnings, so both still compile |
| Discrepancy is on `computation-expressions` | ❌ | That page has no applicative CE content at all. Sole obsolete use: `abstraction-zipapplicative.fsx:154` |
| Docs "already suggest" a strictness probe one-liner | ❌ | `I'm strict` appears nowhere upstream; must be authored |
| Four spellings for the monad builder | ⚠️ undercount | `monad.plus.strict` also exists (`Builders.fs:132`) |

---

## Revised sequencing

The plan's order assumed the harness had to be built. It does not. Reordered by cost-to-value using
what is now known:

| Order | Work | Effort | Change from plan |
|---|---|---|---|
| 1 | Add `index.fsx`, `abstractions.fsx`, `types.fsx` to `FSharpPlus.Docs.fsproj` | minutes | **New** — found during verification; front page is currently unverified |
| 2 | Fix `abstraction-zipapplicative.fsx:154` → `zapp2` | minutes | Was framed as a CE-page rewrite; it is one line |
| 3 | Turn `TreatWarningsAsErrors` on for `FS0044` + `FS0064` in the Docs project | ~½ day | **New** — without it, stale names and degraded generics re-accumulate silently |
| 4 | Build the value-assertion runner for the 84 `// val` claims | 1–2 days | Was assumed to exist |
| 5 | ~~Spike 0~~ | **0** | Already done upstream |
| 6 | Step 1 taxonomy | 2 days | Unchanged, but needs a compiler |
| 7 | Step 2 tables | 3 days | Three are drafted here, source-derived — see `reference/` |
| 8 | Step 5 baseline, then Step 3 coverage loop | ongoing | Infrastructure is free now |

Items 1–3 are worth doing regardless of whether the rest of the plan proceeds: each is a small change
that stops the corpus from decaying, and item 3 is what makes items 1 and 2 stay fixed.

---

## What is blocked

Compiler-gated and **not** attempted here, because no F# toolchain can be installed in this
environment (policy denial, documented above):

- **Step 0** — moot; already solved upstream.
- **Step 1** (failure taxonomy) — requires compiling model output. The error-class table must be
  *measured*, and the plan is right that it should not be hand-asserted, so I have not invented one.
- **Step 3** (coverage loop) — requires the compiler as filter.
- **Step 5** (pass@1 compile-rate baseline) — requires the compiler.

What was produced instead is the subset that can be grounded in source rather than in a compiler: the
three reference tables in [`reference/`](reference/), extracted from `Operators.fs`, `Builders.fs`,
`Lens.fs` and the module structure. See the verification-status note in each file — they are
**source-derived, not compile-verified**, and the distinction is deliberate given the plan's own
insistence on verification.
