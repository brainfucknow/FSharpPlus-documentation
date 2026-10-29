# Operator reference, with argument order

> **Verification status: signatures are source-derived; `verify` snippets are compiler-verified.**
> Every signature below is transcribed from the `let inline` definition in
> `src/FSharpPlus/Operators.fs` or `src/FSharpPlus/Lens.fs` at the pinned commit (1.9.1), cited by
> line, and CI checks each cited line still says what is claimed. Blocks tagged ```` ```fsharp verify ````
> are additionally compiled **and executed** against the released package.

Operator *direction* is one of the plan's predicted failure buckets. The fix is not prose — it is
showing the actual signature, because the signature is what disambiguates `<*` from `*>`.

All operators here need `open FSharpPlus`, **except** the lens operators, which need
`open FSharpPlus.Lens` (see [`required-opens.md`](required-opens.md)).

## The mnemonic that actually works

For the four "keep one side, discard the other" operators, **the angle bracket points at the side you
keep**:

| Operator | Keeps | Discards | Family |
|---|---|---|---|
| `<*` | left | right | sequential applicative |
| `*>` | right | left | sequential applicative |
| `<.` | left | right | zip applicative |
| `.>` | right | left | zip applicative |

Verified against the signatures — `(<*) (x: Applicative<'U>) (y: Applicative<'T>) : Applicative<'U>`
returns the *left* type (`Operators.fs:232`), and `( *>) (x: Applicative<'T>) (y: Applicative<'U>) : Applicative<'U>`
returns the *right* (`:226`). Same pattern for `<.` (`:283`) and `.>` (`:277`).

## Functor

| Operator | Signature | Notes | Line |
|---|---|---|---|
| `<!>` | `(f: 'T->'U) -> Functor<'T> -> Functor<'U>` | `map` as an operator. **Function on the left.** | `:142` |
| `<<\|` | `(f: 'T->'U) -> Functor<'T> -> Functor<'U>` | Same as `<!>` | `:148` |
| `\|>>` | `Functor<'T> -> (f: 'T->'U) -> Functor<'U>` | **Flipped** — value on the left | `:155` |
| `\|>>>` | `Functor1<Functor2<'T>> -> (f: 'T->'U) -> Functor1<Functor2<'U>>` | Maps two layers deep | `:162` |
| `<<<\|` | `(f: 'T->'U) -> Functor1<Functor2<'T>> -> Functor1<Functor2<'U>>` | Two layers, function first | `:169` |

The `<!>` / `|>>` pair is the classic direction trap: **`<!>` takes the function first, `|>>` takes the
value first.** Both produce the same result; only the argument order differs.

The value-replacement operators `|!>` and `<!|` were added after the 1.9.1 release and are not
available in the package verified here. Use `map (fun _ -> replacement)` with 1.9.1.

## Applicative (sequential)

| Operator | Signature | Notes | Line |
|---|---|---|---|
| `<*>` | `Applicative<'T->'U> -> Applicative<'T> -> Applicative<'U>` | Apply a lifted function to a lifted argument | `:205` |
| `*>` | `Applicative<'T> -> Applicative<'U> -> Applicative<'U>` | Keeps **right** | `:226` |
| `<*` | `Applicative<'U> -> Applicative<'T> -> Applicative<'U>` | Keeps **left** | `:232` |

Canonical applicative style: `f <!> x <*> y <*> z`.

## ZipApplicative (non-sequential)

| Operator | Signature | Notes | Line |
|---|---|---|---|
| `<.>` | `ZipApplicative<'T->'U> -> ZipApplicative<'T> -> ZipApplicative<'U>` | The zip counterpart of `<*>` | `:259` |
| `.>` | `ZipApplicative<'T> -> ZipApplicative<'U> -> ZipApplicative<'U>` | Keeps **right** | `:277` |
| `<.` | `ZipApplicative<'U> -> ZipApplicative<'T> -> ZipApplicative<'U>` | Keeps **left** | `:283` |
| `<<\|\|` | `(f: 'T->'U->'V) -> (ZipApplicative<'T> * ZipApplicative<'U>) -> ZipApplicative<'V>` | `map2`; **takes a tuple** | `:290` |
| `\|\|>>` | `(ZipApplicative<'T> * ZipApplicative<'U>) -> (f: 'T->'U->'V) -> ZipApplicative<'V>` | `map2`, flipped | `:297` |
| `<<\|\|\|` | `(f: 'T->'U->'V->'W) -> (3-tuple) -> ZipApplicative<'W>` | `map3` | `:304` |
| `\|\|\|>>` | `(3-tuple) -> (f: 'T->'U->'V->'W) -> ZipApplicative<'W>` | `map3`, flipped | `:311` |

Note `.>` and `<.` were **added in 1.9.1** ("Add missing `(.>)` and `(<.)` zip-applicative operators",
`RELEASE_NOTES.md:9`). Models trained on earlier material will not know them.

The `map2`/`map3` operators take their arguments as a **tuple**, not curried — `(x, y) ||>> f`, not
`x ||>> y ||>> f`. These arrived in 1.8.0 (`RELEASE_NOTES.md:19`).

## Monad

| Operator | Signature | Notes | Line |
|---|---|---|---|
| `>>=` | `Monad<'T> -> ('T->Monad<'U>) -> Monad<'U>` | bind; **value first** | `:328` |
| `=<<` | `('T->Monad<'U>) -> Monad<'T> -> Monad<'U>` | bind; **function first** | `:334` |
| `>=>` | `('T->Monad<'U>) -> ('U->Monad<'V>) -> ('T->Monad<'V>)` | Kleisli, left-to-right | `:340` |
| `<=<` | `('U->Monad<'V>) -> ('T->Monad<'U>) -> ('T->Monad<'V>)` | Kleisli, right-to-left | `:346` |

## Monoid / Alternative

| Operator | Signature | Notes | Line |
|---|---|---|---|
| `++` | `'Monoid -> 'Monoid -> 'Monoid` | `plus`; combines two monoids | `:379` |
| `<\|>` | `Functor<'T> -> Functor<'T> -> Functor<'T>` | Combines two alternatives | `:415` |

## Comonad / Arrow

| Operator | Signature | Notes | Line |
|---|---|---|---|
| `=>>` | `Comonad<'T> -> (Comonad<'T>->'U) -> Comonad<'U>` | extend | `:1002` |
| `***` | `Arrow<'T1,'U1> -> Arrow<'T2,'U2> -> Arrow<'T1*'T2,'U1*'U2>` | Parallel composition | `:529` |
| `+++` | `ArrowChoice<'T1,'U1> -> ArrowChoice<'T2,'U2> -> ArrowChoice<Choice<..>,Choice<..>>` | Choice composition | `:550` |

## Common combinators

| Operator | Signature | Notes | Line |
|---|---|---|---|
| `</` | `x -> (\|>) x` | Opens an infix function application: `x </f/> y` | `:52` |
| `/>` | `x -> flip x` | Closes it | `:58` |
| `\|-` | `'T -> ('T->unit) -> 'T` | Side-effect, returns input (`tap`, flipped) | `:64` |

`x </f/> y` is F#+'s way of writing `f x y` infix. It is unusual enough that it is worth naming
explicitly — a model that has never seen it will not parse it as one construct.

## Lens operators — need `open FSharpPlus.Lens`

| Operator | Meaning | Line (`Lens.fs`) |
|---|---|---|
| `^.` | `source ^. lens` → `view lens source` | `:236` |
| `^?` | `source ^? prism` → `preview prism source` (returns `option`) | `:254` |
| `^..` | `source ^.. lens` → `toListOf lens source` | `:257` |
| `.->` | `lens .-> value` → `setl lens value` | `:242` |
| `%->` | `lens %-> updater` → `over lens updater` | `:248` |
| `<&>` | `Functor<'t> -> ('t->'u) -> Functor<'u>` (flipped map) | `:263` |

Two things models reliably get wrong here:

1. **`<&>` lives in `FSharpPlus.Lens`, not `FSharpPlus`.** It is a flipped `map`, so it looks like it
   belongs with the functor operators, but `open FSharpPlus` alone will not bring it into scope.
2. **The source goes on the left for reading (`^.`, `^?`, `^..`) and the lens goes on the left for
   writing (`.->`, `%->`).** The operand order flips between reading and writing.

Lens composition uses plain function composition `<<`, and composes **outside-in** — `_1 << _2` focuses
the second component of the first. This is the opposite of what Aether users expect, which is a
cross-library contamination risk the plan calls out.

## Verified examples

Blocks tagged `verify` are compiled and **executed** in CI against F#+ 1.9.1 from NuGet, and the
`// val` lines are asserted, not decorative. See [`../tools/Verify`](../tools/Verify).

Map direction — `<!>` takes the function first, `|>>` takes the value first, same result:

```fsharp verify name=map_direction
open FSharpPlus

let viaOperator : int list = (+) 1 <!> [1; 2; 3]
// val viaOperator : int list = [2; 3; 4]

let viaFlipped : int list = [1; 2; 3] |>> (+) 1
// val viaFlipped : int list = [2; 3; 4]
```

The keep-side mnemonic — the angle bracket points at the side you keep:

```fsharp verify name=keep_side_sequential
open FSharpPlus

let keepsLeft : int option = Some 1 <* Some 2
// val keepsLeft : int option = Some 1

let keepsRight : int option = Some 1 *> Some 2
// val keepsRight : int option = Some 2
```

`++` is the monoid operator (F#'s `<>` is *not*):

```fsharp verify name=monoid_plus
open FSharpPlus

let combinedLists : int list = [1; 2] ++ [3; 4]
// val combinedLists : int list = [1; 2; 3; 4]

let combinedStrings : string = "ab" ++ "cd"
// val combinedStrings : string = "abcd"
```

Bind direction:

```fsharp verify name=bind_direction
open FSharpPlus

let valueFirst : int option = Some 2 >>= fun x -> Some (x * 10)
// val valueFirst : int option = Some 20

let functionFirst : int option = (fun x -> Some (x * 10)) =<< Some 2
// val functionFirst : int option = Some 20
```

Value replacement on the released package:

```fsharp verify name=replace_functor_values
open FSharpPlus

let replaced : string list = map (fun _ -> "ready") [1; 2; 3]
// val replaced : string list = ["ready"; "ready"; "ready"]
```

## Not verified here

Precedence and associativity are determined by F#'s operator rules from the leading characters, not by
these definitions. The `map_direction` and `bind_direction` snippets above confirm that `<!>` and `>>=`
chain unparenthesised in those specific shapes; the general precedence table is not established by
them.
