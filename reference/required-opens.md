# Required `open` declarations

> **Verification status: tables are source-derived; `verify` snippets are compiler-verified.**
> Every row is read from module/namespace declarations and `[<AutoOpen>]` attributes in
> `fsprojects/FSharpPlus` at the pinned commit (1.9.1), cited as `file:line`, and CI checks each cited
> line still says what is claimed. The *namespace facts* do not depend on a compiler — they are
> declarations. Blocks tagged ```` ```fsharp verify ```` are additionally compiled **and executed**
> against the released package.

This is the table the plan predicts is the single highest-leverage artifact, because wrong `open` sets
are expected to be the largest and cheapest-to-fix failure bucket.

## The rule in one line

`open FSharpPlus` gives you the generic functions, the operators, and the computation expressions.
**Everything else needs its own `open`** — and the two most commonly needed extras, `FSharpPlus.Data`
and `FSharpPlus.Lens`, are *not* `AutoOpen`.

## What is auto-opened, and what is not

Exactly four modules in the library carry `[<AutoOpen>]` (verified: these are the only `AutoOpen`
occurrences in `src/FSharpPlus/*.fs` and `src/FSharpPlus/Math/*.fs`):

| Module | Declared in | Reached by |
|---|---|---|
| `Operators` | `Operators.fs:9-10` | `open FSharpPlus` |
| `GenericBuilders` | `Builders.fs:17-18` | `open FSharpPlus` |
| `Parsing` | `Parsing.fs:5-6` | `open FSharpPlus` |
| `Memoization` | `Memoization.fs:8-9` | `open FSharpPlus` |

Notably **not** auto-opened — each needs an explicit `open`:

| Module | Declared in | Why it trips people up |
|---|---|---|
| `Lens` | `Lens.fs:9` (`namespace FSharpPlus`, plain `module`) | It is under `FSharpPlus`, so `open FSharpPlus` *looks* like it should be enough. It is not. |
| `Math.Generic` | `Math/Generic.fs:1,12` | Separate namespace *and* non-auto-opened module. |
| `Math.Applicative` | `Math/Applicative.fs:1,15` | Lifts `+ - * /` into applicatives. |
| `Math.ZipApplicative` | `Math/Applicative.fs:1,68` | Same, for zip-applicatives. |

## The table

| You want | `open` you need | Source |
|---|---|---|
| `map`, `bind`, `join`, `traverse`, `sequence`, `result`, `pur`, `zero`, `plus`, `foldMap`, `length` | `open FSharpPlus` | `Operators.fs:9-10` (`AutoOpen`) |
| Operators `<!>` `<*>` `>>=` `>=>` `<|>` `++` `*>` `<*` `<.>` `.>` `<.` `=>>` | `open FSharpPlus` | `Operators.fs` (`AutoOpen`) |
| CEs `monad`, `monad'`, `monad.plus`, `monad.plus'`, `applicative`, `applicative2/3`, `zapp`, `zapp2/3` | `open FSharpPlus` | `Builders.fs:17-18` (`AutoOpen`) |
| `tryParse`, `parse`, `tryParseArray`, `parseArray`, `(\|Parsed\|_\|)` | `open FSharpPlus` | `Parsing.fs:5-6` (`AutoOpen`) |
| `memoizeN` — there is **no** plain `memoize` | `open FSharpPlus` | `Memoization.fs:8-9` (`AutoOpen`), definition at `:24` |
| Extension modules: `String.toLower`, `Option.*`, `Result.*`, `Seq.*`, `List.*`, `Map.*`, `Task.*`, `Async.*`, `Dict.*`, `ResizeArray.*`, `Obj.*`, … | `open FSharpPlus` | `Extensions/*.fs`, all `namespace FSharpPlus`, each `[<RequireQualifiedAccess>] module` (e.g. `String.fs:4-5`) |
| Types: `Reader`, `Writer`, `State`, `Cont`, `Validation`, `NonEmptyList`, `NonEmptySeq`, `NonEmptyMap`, `NonEmptySet`, `DList`, `ZipList`, `Identity`, `Const`, `Compose`, `Coproduct`, `Kleisli`, `Free`, `ParallelArray`, `MultiMap` | `open FSharpPlus.Data` | all of `Data/*.fs` declare `namespace FSharpPlus.Data` |
| Monoid wrappers: `Dual`, `Mult`, `First`, `Last`, `All`, `Any` | `open FSharpPlus.Data` | `Data/Monoids.fs` |
| Transformers: `OptionT`, `ResultT`, `ChoiceT`, `ReaderT`, `WriterT`, `StateT`, `SeqT`, `ListT`, `ContT`, `ValueOptionT` | `open FSharpPlus.Data` | `Data/*.fs` |
| `view`, `preview`, `setl`, `over`, `_1`, `_2`, `_Some`, `_Ok`, `both`, `toListOf`, `traverseOf` | `open FSharpPlus.Lens` | `Lens.fs:9` — **not** `AutoOpen` |
| Lens operators `^.` `^?` `^..` `.->` `%->` `<&>` | `open FSharpPlus.Lens` | `Lens.fs:229,247,250,235,241,256` |
| Generic numeric literals and ops over any numeric type | `open FSharpPlus.Math.Generic` | `Math/Generic.fs:1,12` |
| Arithmetic lifted into applicatives (`+` on two `Option`s, etc.) | `open FSharpPlus.Math.Applicative` | `Math/Applicative.fs:1,15` |
| Same, non-sequential | `open FSharpPlus.Math.ZipApplicative` | `Math/Applicative.fs:1,68` |
| Writing your own instances — `Map.Invoke`, `Bind.Invoke`, `Return`, `Apply`, `Traverse`, … | `open FSharpPlus.Control` | `Control/*.fs` declare `namespace FSharpPlus.Control` |

## Gotchas worth stating explicitly

**Lens work needs two or three opens, not one.** The upstream lens page documents this inline, which
is a decent signal that it is a real stumbling block — `docsrc/content/lens.fsx:197-199`:

```fsharp
open FSharpPlus.Lens
open FSharpPlus      // This module contain other functions relevant for the examples (length, traverse)
open FSharpPlus.Data // Mult
```

So a lens snippet that also folds or traverses needs `open FSharpPlus` *as well as*
`open FSharpPlus.Lens`, and needs `open FSharpPlus.Data` if it touches a monoid wrapper.

**F#+ extension modules shadow FSharp.Core's.** `String`, `Option`, `Result`, `List`, `Seq`, `Map`,
`Array` and friends are all `[<RequireQualifiedAccess>]` modules in `namespace FSharpPlus`. After
`open FSharpPlus`, `Option.map` may resolve to the F#+ one. That is usually intended and usually
compatible, but it means **`open` order can change meaning**, and it is why every snippet should carry
its own opens rather than inheriting them.

**Never rely on an ambient `open` from earlier in a page.** Models copy the snippet, not the page.
This is a documentation-authoring rule, not a language rule, and it is the one most consistently
violated by the current corpus.

## Canonical preamble

For a snippet that could touch anything, this is the maximal safe set — trim per snippet, but never
trim to *fewer* than what the snippet uses:

```fsharp
open FSharpPlus
open FSharpPlus.Data
open FSharpPlus.Lens
open FSharpPlus.Math.Generic
```

`FSharpPlus.Control` is deliberately absent: it is for authoring instances, and opening it in ordinary
user code pulls a large surface of SRTP invokable types into scope for no benefit.

## Verified examples

Compiled and executed in CI. Lens work genuinely needs both opens — `^.` and `_1` come from
`FSharpPlus.Lens`, and dropping `open FSharpPlus` breaks the surrounding generic functions:

```fsharp verify name=lens_needs_two_opens
open FSharpPlus
open FSharpPlus.Lens

let firstOf : int = (1, "a") ^. _1
// val firstOf : int = 1

let updated : int * string = (1, "a") |> (_1 .-> 99)
// val updated : int * string = (99, "a")
```

`tryParse` comes from the `AutoOpen` `Parsing` module, so `open FSharpPlus` alone is enough:

```fsharp verify name=try_parse
open FSharpPlus

let parsedInt : int option = tryParse "42"
// val parsedInt : int option = Some 42

let failedParse : int option = tryParse "nope"
// val failedParse : int option = None
```
