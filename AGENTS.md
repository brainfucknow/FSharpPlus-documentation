# Working in F#+ codebases

Rules for coding agents. Everything here is either derived from `fsprojects/FSharpPlus` source at a
pinned commit (1.9.1) and re-checked in CI, or demonstrated by a snippet that CI compiles and executes.
Fuller tables in [`reference/`](reference/); paste-able summary in [`CHEATSHEET.md`](CHEATSHEET.md);
machine-readable corpus in [`corpus/verified-snippets.jsonl`](corpus/verified-snippets.jsonl).

## 1. Put the opens in every snippet

`open FSharpPlus` gives you generic functions, operators, computation expressions, `tryParse`,
`memoizeN`, and the extension modules. **Nothing else is auto-opened.** Only four modules carry
`[<AutoOpen>]`: `Operators`, `GenericBuilders`, `Parsing`, `Memoization`.

```fsharp
open FSharpPlus              // map, bind, traverse, operators, CEs
open FSharpPlus.Data         // Reader Writer State Validation NonEmptyList DList ZipList, transformers
open FSharpPlus.Lens         // view setl over _1 _Some, and ^. ^? .-> %-> <&>
open FSharpPlus.Math.Generic // generic numeric ops
```

`Lens` and `Math.Generic` live under `FSharpPlus`/`FSharpPlus.Math` but are **plain modules** — `open
FSharpPlus` does not bring them in. Do not inherit opens from earlier in a file: the snippet is what
gets copied.

## 2. Do not invent these — they do not exist

`fmap` · `pure` · `liftA` · `liftA2` · `forM` · `sequence_` · `traverse_` · `memoize` ·
`asyncResult` · `taskResult` · `sequenceResult` · `traverseResult` · `bindResult`

Use instead: `map` · `result`/`pur` · `lift2`/`lift3` · `traverse` · `sequence` · `memoizeN` · `>>=`.

- `mapM` exists **only** as `SeqT.mapM`, not as a global function. Use `traverse`.
- `<>` is **not** the monoid operator in F#+. `++` is.
- `memoizeN` is the only memoize function; there is no plain `memoize`.
- `result` (`Return`) and `pur` (`Pure`) are **different functions** — `result` for monads and
  sequential applicatives, `pur` for zip applicatives.

## 3. Use current names

`applicative'` / `applicative2'` / `applicative3'` were renamed to **`zapp` / `zapp2` / `zapp3`** in
1.9.1. The old names still compile — they are `Obsolete` *warnings*, not errors — so a wrong name will
not necessarily fail a build. Prefer the new ones.

Also newer than most training data: `.>` and `<.` (1.9.1), `||>>` / `|||>>` / `<<||` / `<<|||` (1.8.0),
the `Obj` module and `Result.bindTask` / `bindInto` (1.9.1).

## 4. Pick the right computation expression

| Write | Lazy/strict | Additive |
|---|---|---|
| `monad` (= `monad.fx`) | lazy | no |
| `monad'` (= `monad.strict` = `monad.fx'`) | strict | no |
| `monad.plus` | lazy | yes |
| `monad.plus'` (= `monad.plus.strict`) | strict | yes |

Rough guide: `Async`/`Lazy`/`seq` → `monad`; `option`/`Result`/`Choice` → `monad'`; `list` used
additively → `monad.plus'`. `monad'.plus` does **not** exist — start from `monad`.

Applicative CEs: `applicative`/`applicative2`/`applicative3` are sequential; `zapp`/`zapp2`/`zapp3` are
zip.

## 5. Watch operator direction

The angle bracket points at the side you keep: `<*` and `<.` keep the left, `*>` and `.>` keep the right.

`<!>` takes the **function** first; `|>>` takes the **value** first. `>>=` takes the value first; `=<<`
takes the function first.

`<*>` on `list` is the **cartesian product**; `<.>` zips element-wise. Both compile — picking the wrong
one is a silent wrong answer, not a type error:

```fsharp
(+) <!> [1; 2; 3] <*> [10; 20; 30]   // [11; 21; 31; 12; 22; 32; 13; 23; 33]
(+) <!> [1; 2; 3] <.> [10; 20; 30]   // [11; 22; 33]
```

Lens operators put the **source** on the left for reading (`^.`, `^?`, `^..`) and the **lens** on the
left for writing (`.->`, `%->`). Lens composition is `<<` and reads outside-in — the opposite of Aether,
so porting operand order verbatim focuses the wrong thing while still compiling.

## 6. Annotate the binding when inference stalls

The library is SRTP-heavy and CE builders are declared with an explicit type parameter, so
return-type annotations are frequently needed. Annotate the **binding**, and prefer a partially open
type so inference still does work:

```fsharp
let (result: Async<_>) = monad.fx { ... }
let (items: list<_>)   = monad.plus' { ... }
```

This is a heuristic generalised from upstream examples, not a verified rule — a precise
"annotation required here" table needs the compiler as an oracle and does not exist yet.

## 7. Two hazards to know

- **FSI and project compilation can disagree** on SRTP-heavy code. Upstream PR #372 hit `Type
  constraint mismatch when applying the default type 'obj'` in a doc page that was otherwise fine.
  Verifying in one target does not verify the other.
- **`FS0064` "less generic than indicated"** is the warning that fires when a generic definition
  silently degrades to a monomorphic one. It is *not* an error in upstream's own build, so code can look
  generic without being generic. This repo escalates it to an error for its own snippets.

## 8. If you add an example to this repo

Tag it so CI proves it:

````markdown
```fsharp verify name=my_example
open FSharpPlus

let doubled : int list = map ((*) 2) [1; 2; 3]
// val doubled : int list = [2; 4; 6]
```
````

`// val name : Type = value` is enforced, not decorative. [`tools/Verify`](tools/Verify) rejects a claim
whose snippet does not annotate `let name : Type` — so the compiler checks the type — and the generated
program asserts the value at runtime, in two independent targets (project compilation and `dotnet fsi`).
Run `./tools/verify.sh` locally; it needs a .NET SDK.
