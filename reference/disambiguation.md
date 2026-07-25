# Disambiguation: what F#+ calls things, and what it does not have

> **Verification status: absence is grep-verified; presence is source-cited.**
> "Does not exist" rows were checked by searching every `let`/`let inline` definition across
> `src/FSharpPlus/**/*.fs` at `master` (1.9.1). Absence of a definition is a fact a grep can establish,
> so this page is firmer than it would be if it relied on a compiler. Snippets are not compile-verified.

The plan's rationale for this page is sound: **explicitly naming APIs that do not exist suppresses a
whole hallucination class.** A model that has been told `fmap` is absent will reach for `map`; a model
told nothing will emit `fmap` and fail.

## Names that do not exist in F#+

Each of these has **zero** definitions in the library. If you have one in your code or your training
data, it came from Haskell, FSharpx, FsToolkit.ErrorHandling, or Aether.

| Name you might reach for | Where it comes from | Use instead | F#+ source |
|---|---|---|---|
| `fmap` | Haskell | `map`, or `<!>` / `\|>>` | `Operators.fs:141` |
| `pure` | Haskell | **`result`** (monad/applicative) or **`pur`** (zip applicative) — see below | `:219`, `:263` |
| `liftA`, `liftA2` | Haskell | `lift2`, `lift3` | `Operators.fs:231` |
| `forM` | Haskell | `traverse` with flipped args, or `for` in a `monad` CE | — |
| `sequence_`, `traverse_` | Haskell | `sequence` / `traverse` then `ignore` | — |
| `asyncResult`, `taskResult` CEs | FsToolkit.ErrorHandling | `monad` over `Async<Result<_,_>>`, or `ResultT` from `FSharpPlus.Data` | `Data/Error.fs` |
| `sequenceResult`, `traverseResult`, `bindResult` | FsToolkit.ErrorHandling | generic `sequence`, `traverse`, `>>=` | `Operators.fs` |
| `Result.sequence` / FsToolkit's `Result` CE helpers | FsToolkit | the generic functions work on `Result` directly | — |
| `memoize` | plausible guess from the module name | **`memoizeN`** — the only memoize function F#+ defines | `Memoization.fs:24` |

**`mapM` is a special case.** It exists, but **only** as `SeqT.mapM` (`Data/Seq.fs:586`) — a qualified
member on `SeqT`, not a global generic function. Writing `mapM f xs` at the top level will not resolve.
The generic function you want is `traverse`.

## `result` vs `pur` — a distinction that is easy to miss

These are **two different functions** backed by two different SRTP invokables:

| Function | Definition | Backed by | Use for |
|---|---|---|---|
| `result` | `let inline result (x: 'T) : '``Functor<'T>`` = Return.Invoke x` | `Return` | Monads and **sequential** applicatives |
| `pur` | `let inline pur (x: 'T) : '``ZipFunctor<'T>`` = Pure.Invoke x` | `Pure` | **Zip** applicatives |

Source: `Operators.fs:219` and `Operators.fs:263`.

They are not interchangeable, and `pure` — the name a Haskell-trained model will produce — is neither
of them and does not exist.

The CE builders make the split visible: the **zip** applicative builders lift with `pur`
(`Builders.fs:229`, `:239`, `:249`) while the **sequential** ones lift with `result`
(`Builders.fs:208`, `:218`). That is the clearest available signal for which function a given
applicative context wants.

`result` lifting into two different monads, compiled and executed in CI:

```fsharp verify name=result_lifts
open FSharpPlus

let intoOption : int option = result 42
// val intoOption : int option = Some 42

let intoResult : Result<int,string> = result 42
// val intoResult : Result<int,string> = Ok 42
```

Note `pur` is deliberately not given a value assertion here: for `list` its instance is
`fun x -> List.cycle [x]` (`Control/ZipApplicative.fs:44`), i.e. conceptually infinite, so it is not
demonstrable as a finite `// val` claim on that type.

`traverse` is what you want where Haskell would use `mapM`:

```fsharp verify name=traverse_options
open FSharpPlus

let allSome : int list option = traverse (fun x -> if x > 0 then Some x else None) [1; 2; 3]
// val allSome : int list option = Some [1; 2; 3]

let oneNone : int list option = traverse (fun x -> if x > 0 then Some x else None) [1; -2; 3]
// val oneNone : int list option = None
```

## Mapping from other libraries

### From Haskell

| Haskell | F#+ | Note |
|---|---|---|
| `fmap` / `<$>` | `map` / `<!>` | `<!>` takes the function first, same as `<$>` |
| `pure` / `return` | `result` (or `pur` for zip) | see above |
| `<*>` | `<*>` | same spelling, sequential |
| `>>=` | `>>=` | same |
| `>=>` | `>=>` | same |
| `mappend` / `<>` | `plus` / `++` | `<>` is **not** the F#+ monoid operator; `++` is (`Operators.fs:389`) |
| `mempty` | `zero` | |
| `traverse` / `mapM` | `traverse` | `mapM` is not a global function in F#+ |
| `sequenceA` / `sequence` | `sequence` | |
| `<|>` | `<|>` | same |
| `extend` / `<<=` | `=>>` | note the direction differs from Haskell's `<<=` (`Operators.fs:1023`) |

### From Aether (lenses)

Aether and F#+ both use `^.`-style optics operators, and they **compose in opposite directions**. F#+
lens composition is plain function composition `<<` and reads **outside-in**: `_1 << _2` focuses the
second component *of* the first. Porting Aether code by mechanically keeping the operand order will
produce lenses that compile and focus the wrong thing — a silent-wrong-answer failure, not a compile
error.

F#+ lens functions and operators require `open FSharpPlus.Lens`
(see [`required-opens.md`](required-opens.md)) — they are **not** auto-opened.

### From FSharpx

FSharpx's generic functions largely predate F#+ and use different names. The safest approach is to
consult [`required-opens.md`](required-opens.md) and reach for the F#+ generic function rather than
translating name-for-name.

## Version drift to watch

| Old name | Current name | Since | Status |
|---|---|---|---|
| `applicative'` | `zapp` | 1.9.1 | Obsolete **warning**, still compiles |
| `applicative2'` | `zapp2` | 1.9.1 | Obsolete warning, still compiles |
| `applicative3'` | `zapp3` | 1.9.1 | Obsolete warning, still compiles |

Source: `Builders.fs:270-287`, `RELEASE_NOTES.md:10`.

Because these are `Obsolete(..., false)` — warnings, not errors — **both spellings compile**, so nothing
in a normal build stops the old name from spreading. The upstream docs still contain one
(`docsrc/content/abstraction-zipapplicative.fsx:154`).

Also new enough to be missing from most training data:

- `.>` and `<.` zip-applicative operators — added 1.9.1 (`RELEASE_NOTES.md:8`)
- `||>>` / `|||>>` / `<<||` / `<<|||` map2/map3 operators — added 1.8.0 (`RELEASE_NOTES.md:18`)
- `Obj` module, `Result.bindTask` / `bindInto` — added 1.9.1

## A caution about this page

Every "does not exist" row is only as current as the commit it was checked against (`master`, 1.9.1).
Unlike the operator and CE tables — which restate what the source says and stay true as long as those
lines are unchanged — an absence claim is falsified by any future addition. This page should be
regenerated by script against each release rather than hand-maintained, and that script is small: it is
the same grep over `let`/`let inline` definitions used to build it.
