# Computation expression selection table

> **Verification status: source-derived, not compile-verified.**
> The alias graph and builder identities below are read directly from
> `src/FSharpPlus/Builders.fs` at `master` (1.9.1), cited by line. No F# compiler was available where
> this was written, so illustrative snippets have not been executed.

The plan's diagnosis is right and worth restating: this is not really a documentation gap, it is a
**naming surface that cannot be guessed**. One builder has several spellings, and the lazy/strict
distinction is a property of the *target type*, not of the call site. Prose cannot fix that. A table
can.

## Every spelling, and what it actually resolves to

There are **four distinct monadic builder types**. There are **eight-plus spellings** that reach them.

| Spelling | Resolves to | Lazy / strict | Additive (`Zero`/`Combine` via `<|>`) | Source |
|---|---|---|---|---|
| `monad` | `MonadFxBuilder` | lazy | no | `Builders.fs:257` |
| `monad.fx` | `MonadFxBuilder` (returns `this`) | lazy | no | `Builders.fs:166` |
| `monad'` | `MonadFxStrictBuilder` | **strict** | no | `Builders.fs:260` |
| `monad.strict` | `MonadFxStrictBuilder` | **strict** | no | `Builders.fs:157` |
| `monad.fx'` | `MonadFxStrictBuilder` | **strict** | no | `Builders.fs:169` |
| `monad.fx.strict` | `MonadFxStrictBuilder` | **strict** | no | via `:166` then `:157` |
| `monad.plus` | `MonadPlusBuilder` | lazy | **yes** | `Builders.fs:160` |
| `monad.plus'` | `MonadPlusStrictBuilder` | **strict** | **yes** | `Builders.fs:163` |
| `monad.plus.strict` | `MonadPlusStrictBuilder` | **strict** | **yes** | `Builders.fs:133` |

So the four builders, with their canonical spelling first:

| Builder | Canonical | Other spellings |
|---|---|---|
| `MonadFxBuilder` | `monad` | `monad.fx` |
| `MonadFxStrictBuilder` | `monad'` | `monad.strict`, `monad.fx'`, `monad.fx.strict` |
| `MonadPlusBuilder` | `monad.plus` | — |
| `MonadPlusStrictBuilder` | `monad.plus'` | `monad.plus.strict` |

**Correction to the plan:** it lists four spellings. `monad.plus.strict` is a fifth distinct path,
because `.strict` is defined on `MonadPlusBuilder` too (`Builders.fs:133`), not only on
`MonadFxBuilder`.

**One thing that does *not* exist:** the strict builders carry no flavour members of their own.
`MonadFxStrictBuilder` (`:114`) and `MonadPlusStrictBuilder` (`:99`) define no `.plus`, `.fx`, or
`.strict`. So `monad'.plus` is **not** valid — to get a strict additive CE you must write `monad.plus'`
or `monad.plus.strict`, starting from `monad`.

## Applicative CEs

| Spelling | Builder | Layers | Sequential? | Status | Source |
|---|---|---|---|---|---|
| `applicative` | `ApplicativeBuilder` | 1 | sequential | current | `Builders.fs:263` |
| `applicative2` | `ApplicativeBuilder2` | 2 | sequential | current | `Builders.fs:266` |
| `applicative3` | `ApplicativeBuilder3` | 3 | sequential | current | `Builders.fs:269` |
| `zapp` | `ZipApplicativeBuilder` | 1 | **non-sequential** | current | `Builders.fs:281` |
| `zapp2` | `ZipApplicativeBuilder2` | 2 | **non-sequential** | current | `Builders.fs:284` |
| `zapp3` | `ZipApplicativeBuilder3` | 3 | **non-sequential** | current | `Builders.fs:287` |
| `applicative'` | `ZipApplicativeBuilder` | 1 | non-sequential | ⚠️ **obsolete → `zapp`** | `Builders.fs:271-272` |
| `applicative2'` | `ZipApplicativeBuilder2` | 2 | non-sequential | ⚠️ **obsolete → `zapp2`** | `Builders.fs:274-275` |
| `applicative3'` | `ZipApplicativeBuilder3` | 3 | non-sequential | ⚠️ **obsolete → `zapp3`** | `Builders.fs:277-278` |

The obsolete names are `Obsolete(..., false)` — a **warning, not an error**. Both spellings compile,
which is exactly why stale names persist. Since the upstream Docs project sets no
`TreatWarningsAsErrors`, CI does not catch them either: `docsrc/content/abstraction-zipapplicative.fsx:154`
still uses `applicative2'` today.

**If you have `applicative'` in training data or in an existing codebase, the current name is `zapp`.**
This is the version-drift class the plan predicts models will get wrong for years, and it is the single
highest-value row in this document.

## Choosing between sequential and zip applicatives

The distinction is about how effects combine, and it is observable:

- `applicative` / `<*>` — **sequential**. For `list`, this is the cartesian product. For `Validation`,
  errors accumulate. Short-circuits where the type short-circuits.
- `zapp` / `<.>` — **non-sequential** ("zip"). For `list`, this zips element-wise. This is what
  `ZipList` semantics give you without wrapping in `ZipList`.

Picking the wrong one compiles fine and silently produces different results, which makes it a
correctness trap rather than a syntax error. Both sides, compiled and executed in CI:

```fsharp verify name=sequential_vs_zip
open FSharpPlus

// <*> on list is the cartesian product
let sequential : int list = (+) <!> [1; 2; 3] <*> [10; 20; 30]
// val sequential : int list = [11; 21; 31; 12; 22; 32; 13; 23; 33]

// <.> on list zips element-wise (List.map2Shortest, ZipApplicative.fs:81)
let zipped : int list = (+) <!> [1; 2; 3] <.> [10; 20; 30]
// val zipped : int list = [11; 22; 33]
```

Nine results versus three, from a one-character difference, with no compile error to warn you.

## Lazy vs strict: why it cannot be inferred from the call site

`monad` is lazy; `monad'` is strict. Which one a given type *requires* depends on whether the type has
a `Delay`/`TryWith` implementation that behaves lazily. `MonadPlusBuilder.While` (`Builders.fs:141-145`)
literally probes this at compile time and emits a warning when the type is not lazy:

```fsharp
member inline this.While ([<InlineIfLambda>]guard, body: '``MonadPlus<'T>``) : '``MonadPlus<'T>`` =
    // Check the type is lazy, otherwise display a warning.
    let __ () = TryWith.InvokeForWhile (Unchecked.defaultof<'``MonadPlus<'T>``>) (fun (_: exn) -> ...)
    this.WhileImpl (guard, body)
```

A strict monadic CE over `option`, compiled and executed in CI:

```fsharp verify name=monad_strict_option
open FSharpPlus

let added : int option = monad' {
    let! a = Some 1
    let! b = Some 2
    return a + b
}
// val added : int option = Some 3
```

Rule of thumb, from what the upstream CE page demonstrates
(`docsrc/content/computation-expressions.fsx`):

| Target type | Use | Page reference |
|---|---|---|
| `Async<_>`, `Lazy<_>`, `seq<_>` — genuinely deferred | `monad` / `monad.fx` | `:47` uses `monad.fx` for `Async<_>` |
| `option`, `Result`, `Choice` — eager, side-effecting | `monad'` | `:15` |
| `list<_>`, `seq<_>` used additively | `monad.plus'` | `:31`, `:57` |

### Probing strictness in FSI

The plan suggests including a one-liner the docs "already suggest". **They do not** — the string
`I'm strict` appears nowhere in the upstream repository, and there is no strictness probe on the CE
page. So here is the probe, authored rather than lifted, and flagged as **not yet executed**:

```fsharp
// If "evaluated" prints immediately, the builder is strict for this type.
// If it prints only when the value is forced/awaited, it is lazy.
let probe : option<unit> = monad { printfn "evaluated" }
```

Once a compiler is available this belongs in the corpus as a verified example with its actual observed
output, per Step 3 — it is a claim about runtime behaviour, so a compile check alone would not validate
it.

## What the upstream CE page does *not* cover

`docsrc/content/computation-expressions.fsx` documents only `monad` (`:5`), `monad'` (`:15`),
`monad.plus'` (`:31`, `:57`) and `monad.fx` (`:47`). Absent from that page entirely:

- `monad.plus` (lazy additive), `monad.strict`, `monad.fx'`, `monad.plus.strict`
- **every applicative CE** — `applicative`, `applicative2/3`, `zapp`, `zapp2/3`

The applicative CEs are documented on other pages (`applicative-functors.fsx`,
`abstraction-zipapplicative.fsx`), so a model retrieving "F#+ computation expressions" gets a page that
silently omits half the CE surface. Merging this table into that page is the concrete fix.

<!-- NEGATIVE TEST - temporary, removed in the next commit.
     Proves the snippets job actually fails: (a) FS0044 blocks a renamed API,
     (b) a wrong // val value fails at runtime. -->

```fsharp verify name=negtest_obsolete
open FSharpPlus

let usesObsolete : int option = applicative' { return 1 }
// val usesObsolete : int option = Some 1
```

```fsharp verify name=negtest_wrong_value
open FSharpPlus

let wrongValue : int list = map ((*) 2) [1; 2; 3]
// val wrongValue : int list = [99; 99; 99]
```
