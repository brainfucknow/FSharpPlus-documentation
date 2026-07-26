# Patches for `fsprojects/FSharpPlus`

A five-patch series turning the findings in [`../FINDINGS.md`](../FINDINGS.md) into changes against
upstream. They are separate from the rest of this repo because they belong in `fsprojects/FSharpPlus`,
which this work has no push access to.

## Base and application

Generated against **`44ebc378fa403400a343784e4285b3fd2672b363`** (2026-02-13, release 1.9.1) — the
commit pinned in [`../upstream.json`](../upstream.json).

```sh
git clone https://github.com/fsprojects/FSharpPlus.git
cd FSharpPlus
git submodule update --init            # external/FSharp.TypeProviders.SDK, needed to build
git checkout 44ebc378fa403400a343784e4285b3fd2672b363
git am /path/to/patch/*.patch
```

Applying to a later `master` may need rebasing; the content is small and self-contained, so conflicts
should be textual rather than semantic.

## The series

| # | Patch | What it does |
|---|---|---|
| 1 | `0001-docs-compile-index-abstractions-and-types-pages.patch` | Adds the three doc pages that no build ever checked to `FSharpPlus.Docs` |
| 2 | `0002-docs-use-zapp2-instead-of-the-obsolete-applicative2.patch` | The one remaining obsolete CE spelling in the docs |
| 3 | `0003-docs-fail-the-build-on-obsolete-APIs-and-degraded-ge.patch` | Promotes `FS0044` and `FS0064` to errors in the docs project |
| 4 | `0004-docs-document-the-full-computation-expression-surfac.patch` | Documents the whole CE surface, with worked examples |
| 5 | `0005-docs-add-a-page-explaining-which-open-is-required.patch` | New page: which `open` each feature area needs |

Patches 1–3 are independently useful and could be taken alone; 3 is what stops 1 and 2 from silently
regressing. Patches 4–5 are documentation content.

### Why each one

**1 — three pages were never compiled.** `FSharpPlus.Docs` exists specifically so the documentation
never breaks, and it already compiles 64 of the 67 pages under `docsrc/content`. `index.fsx`,
`abstractions.fsx` and `types.fsx` fall outside both the explicit list and the `type-*` / `abstraction-*`
globs. `index.fsx` is the front page and carries real examples, making it the most-read page in the
corpus and the only substantial one the compiler never saw. All three compile unchanged.

**2 — `applicative2'` is obsolete.** Renamed to `zapp2` in 1.9.1. `ObsoleteAttribute` is declared with
`isError=false`, so the old spelling still compiles with only a warning and this use survived the
rename unnoticed. It is the only obsolete CE spelling left anywhere under `docsrc/content`.

**3 — nothing was enforcing either.** Neither `Directory.Build.props` nor the docs project sets any
warnings-as-errors, so a stale API name or a silently-monomorphic generic example both build fine.
`FS0064` ("less generic than indicated") is the specific warning that fires when a generic example
degrades — which matters more in docs than in ordinary code, because a reader copies what they see.

**4 — the CE page showed about half the surface.** It has no `(** *)` blocks at all, so the rendered
page is a bare code listing, and it demonstrated only `monad`, `monad'`, `monad.plus'` and `monad.fx`.
Missing: `monad.plus`, the alias spellings, and every applicative builder. The patch adds the builder
tables, lazy-versus-strict guidance, the `zapp` rename note, and worked examples contrasting
`applicative` with `zapp` on the same two lists.

**5 — the required `open` set is not guessable.** `Lens` is a plain module *under* the `FSharpPlus`
namespace, so opening the parent looks sufficient and is not. `lens.fsx` already carries an inline
comment noting a second `open` is needed for `length` and `traverse`, which suggests this trips people
up. The new page states which four modules are `AutoOpen`, which are not, and gives a lookup table.

## What was verified, and how

Everything below was run locally against the base commit with .NET SDK 8.0.129 (matching the
`global.json` pin), not asserted from reading:

- **Every commit in the series builds, so the series is bisectable.** Each commit was checked out in
  turn and built with `dotnet build src/FSharpPlus.Docs/FSharpPlus.Docs.fsproj -c Release`:

  | Commit | Subject | Result |
  |---|---|---|
  | `2548e56` | docs: compile index, abstractions and types pages | BUILD-OK |
  | `42b81c9` | docs: use zapp2 instead of the obsolete applicative2' | BUILD-OK |
  | `5c5a37f` | docs: fail the build on obsolete APIs and degraded generics | BUILD-OK |
  | `cdddc0c` | docs: document the full computation expression surface | BUILD-OK |
  | `6992d07` | docs: add a page explaining which open is required | BUILD-OK |

  The unpatched base also builds clean. Baseline build time is ~5 minutes; the library is SRTP-heavy,
  so a full rebuild per commit is slow but not prohibitive. (The commit hashes are from the local
  replay used to generate these patches; `git am` will produce different hashes for the same content.)
- **Patch 3 was validated by watching it fail first.** Applied before patch 2, the build failed with
  exactly one error:
  `docsrc/content/abstraction-zipapplicative.fsx(154,17): error FS0044: This construct is deprecated.
  This value is obsolete. Use zapp2 instead.`
  That confirms both that the gate fires and that line 154 is the only offender. **`FS0064` reports
  nothing** against current content, so no existing generic example is degraded — a new result, since
  nothing was checking.
- **The documented values in patch 4 were executed**, not eyeballed: `sequentialCE` is the cartesian
  product `[11; 21; 31; 12; 22; 32; 13; 23; 33]`, `zippedCE` is the pointwise `[11; 22; 33]`, and the
  `monad.plus` example forces to `[1; 2; 3]`.
- **Patch 5's examples come from this repo's verified corpus** — see
  [`../corpus/verified-snippets.jsonl`](../corpus/verified-snippets.jsonl), entries
  `lens_needs_two_opens` and `try_parse` — which is compiled and executed in CI against the released
  package, and also under `dotnet fsi` as a second target.
- **The series applies cleanly to a pristine base.** `git apply --check` passes for all five, `git am`
  applies all five in order, and the resulting tree matches the validated state byte-for-byte.

`git am` reports trailing-whitespace warnings on three lines in patch 1. Those are deliberate: the
existing `<Link>Docs\%(FileName)%(Extension)</Link>` entries in `FSharpPlus.Docs.fsproj` all carry a
trailing tab, and the new entries match the surrounding style rather than silently diverging from it.

## Not included

Deliberately left out rather than guessed at:

- **A value-assertion runner for the 84 `// val` claims in the docs.** `src/FSharpPlus.Docs/Program.fs`
  is `let main argv = 0`, so the project proves the pages *compile* and never checks what they
  evaluate to. Closing that is a larger change and needs a maintainer's view on the mechanism; the
  approach used in this repo is in [`../tools/extract_snippets.py`](../tools/extract_snippets.py).
- **A `dotnet fsi` job.** Upstream PR #372 hit `Type constraint mismatch when applying the default type
  'obj'` in a page that compiled fine as a project, so FSI and project compilation disagree on
  SRTP-heavy code. Worth a second CI target, but it is a CI-policy change rather than a docs fix.
- **The remaining coverage gap.** 53 of 192 public functions in the `AutoOpen` `Operators` module have
  zero mentions anywhere in `docsrc/content` — see [`../tools/coverage_gap.py`](../tools/coverage_gap.py).
  Verified examples for 21 of them live in
  [`../reference/generic-functions.md`](../reference/generic-functions.md) and could be contributed
  upstream, but that is a much larger patch and is better proposed after the smaller ones land.
