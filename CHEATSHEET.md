# F#+ cheat sheet (for context windows)

Verified against `fsprojects/FSharpPlus` `master` — **1.9.1, January 2026**.
Source-derived (line-cited); **not compile-verified** — no F# toolchain was available where this was
written. Fuller tables: [`reference/`](reference/).

---

## Opens

`open FSharpPlus` gives you generic functions, operators, computation expressions, `tryParse`, `memoize`,
and the extension modules (`String.*`, `Option.*`, `Result.*`, `Seq.*`, …).

**Everything else is explicit** — these are *not* auto-opened:

```fsharp
open FSharpPlus              // map, bind, traverse, operators, monad/applicative/zapp CEs
open FSharpPlus.Data         // Reader Writer State Cont Validation NonEmptyList DList ZipList
                             // OptionT ResultT ReaderT StateT WriterT SeqT ...
open FSharpPlus.Lens         // view preview setl over _1 _2 _Some _Ok  and  ^. ^? ^.. .-> %-> <&>
open FSharpPlus.Math.Generic // generic numeric ops
```

Only four modules carry `[<AutoOpen>]`: `Operators`, `GenericBuilders`, `Parsing`, `Memoization`.
`Lens` and `Math.Generic` are under `FSharpPlus`/`FSharpPlus.Math` but are **plain modules** — `open
FSharpPlus` does not bring them in.

**Put the opens in every snippet.** Do not inherit them from earlier in a file.

---

## Computation expressions

| Write | Builder | Lazy/strict | Additive |
|---|---|---|---|
| `monad` | `MonadFxBuilder` | lazy | no |
| `monad'` | `MonadFxStrictBuilder` | strict | no |
| `monad.plus` | `MonadPlusBuilder` | lazy | yes |
| `monad.plus'` | `MonadPlusStrictBuilder` | strict | yes |

Aliases for the same four: `monad.fx` = `monad`; `monad.strict` = `monad.fx'` = `monad.fx.strict` =
`monad'`; `monad.plus.strict` = `monad.plus'`.
`monad'.plus` **does not exist** — strict builders have no flavour members; start from `monad`.

Rough guide: `Async`/`Lazy`/`seq` → `monad`; `option`/`Result`/`Choice` → `monad'`; `list` used
additively → `monad.plus'`.

| Applicative CE | Layers | Effects |
|---|---|---|
| `applicative`, `applicative2`, `applicative3` | 1/2/3 | sequential |
| `zapp`, `zapp2`, `zapp3` | 1/2/3 | non-sequential (zip) |

⚠️ **`applicative'` / `applicative2'` / `applicative3'` are obsolete → use `zapp` / `zapp2` / `zapp3`**
(renamed 1.9.1). The old names still *compile* — they are warnings — so they persist silently.

---

## Operators

**The angle bracket points at the side you keep:**

| | Keeps left | Keeps right |
|---|---|---|
| sequential | `<*` | `*>` |
| zip | `<.` | `.>` |

### Functor
```
<!>   f <!> x        map, function first        (same as <<|)
|>>   x |>> f        map, value first
|!>   x |!> v        replace value
```
### Applicative
```
<*>   ff <*> x       sequential apply           f <!> x <*> y <*> z
<.>   ff <.> x       zip apply
(x,y) ||>> f         map2  (tuple, not curried)
f <<|| (x,y)         map2, function first
```
### Monad
```
>>=   x >>= f        bind, value first
=<<   f =<< x        bind, function first
>=>   f >=> g        Kleisli left-to-right
<=<   g <=< f        Kleisli right-to-left
```
### Monoid / Alternative
```
++    x ++ y         plus / mappend   (NOT <>)
<|>   x <|> y        alternative
```
### Lens — needs `open FSharpPlus.Lens`
```
source ^. lens       view            source ^? prism      preview -> option
source ^.. lens      toListOf        lens .-> value       set
lens %-> f           over            x <&> f              flipped map
```
Lens composition is `<<` and reads **outside-in**: `_1 << _2` = second component of the first.
(Opposite of Aether — porting operand order verbatim silently focuses the wrong thing.)

### Infix function application
```
x </f/> y     ==     f x y
```

---

## Names that do **not** exist

`fmap` · `pure` · `liftA` · `liftA2` · `forM` · `sequence_` · `traverse_` · `asyncResult` ·
`taskResult` · `sequenceResult` · `traverseResult` · `bindResult`

Use instead: `map` · `result`/`pur` · `lift2`/`lift3` · `traverse` · `sequence` · `>>=`.

`mapM` exists **only** as `SeqT.mapM` — not a global function. Use `traverse`.

`<>` is **not** the monoid operator in F#+; `++` is.

### `result` vs `pur` — different functions
```fsharp
result x   // Return.Invoke — monads and SEQUENTIAL applicatives
pur x      // Pure.Invoke   — ZIP applicatives
```
`pure` is neither and does not exist.

---

## Recently added — likely missing from training data

- `.>` `<.` zip-applicative operators — **1.9.1**
- `||>>` `|||>>` `<<||` `<<|||` map2/map3 operators — **1.8.0**
- `Obj` module, `Result.bindTask`, `Result.bindInto` — **1.9.1**
- `zapp` / `zapp2` / `zapp3` — **1.9.1**

---

## Where annotations are mandatory

The library is SRTP-heavy, so return-type annotations are frequently required to drive resolution. The
CE builders are declared with an explicit type parameter — e.g. `monad<'``monad<'t>``>` — which is why
the upstream docs annotate the binding rather than the expression:

```fsharp
let (asnNumber: Async<_>) = monad.fx { ... }   // docsrc/content/computation-expressions.fsx:47
let (lstNumber: list<_>)  = monad.plus' { ... } // :57
```

Annotate the **binding**, and prefer `Async<_>` over a fully-applied type so inference still does work.

⚠️ This section is the one part of this cheat sheet that is a *generalisation from examples* rather than
a restatement of a declaration. A precise "annotation is required here" table needs the compiler as
oracle — it is Step 2's annotation-placement guide, and it is **not** done. Treat the rule above as a
strong heuristic, not a verified contract.

Two known hazards worth knowing while that gap is open:

- **FSI and project compilation can disagree** on SRTP-heavy code. Upstream PR #372 hit `Type
  constraint mismatch when applying the default type 'obj'` in a doc page that was otherwise fine.
  A snippet verified in one target is not thereby verified in the other.
- **`FS0064` "less generic than indicated"** is the warning that fires when a generic example silently
  degrades to a monomorphic one. It is *not* an error in the upstream build today, so a snippet can
  look generic and not be.
