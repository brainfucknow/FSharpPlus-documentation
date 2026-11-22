# Generic functions and operators

All examples assume `#r "nuget: FSharpPlus, 1.9.1"` followed by `open FSharpPlus`. Signatures below use the API reference's higher-kinded-type notation. “Member” is the instance hook selected by the dispatcher; built-in overloads can also be selected.

| Name | API signature | Required static member | Compiled one-line example |
|---|---|---|---|
| `map` | `('T -> 'U) -> Functor<'T> -> Functor<'U>` | `Map (source, mapping)` | `let _: int option = map ((+) 1) (Some 2)` |
| `bind` | `('T -> Monad<'U>) -> Monad<'T> -> Monad<'U>` | `(>>=) (source, binder)` | `let _: int option = bind (fun x -> Some (x + 1)) (Some 2)` |
| `result` | `'T -> Applicative<'T>` | `Return value` | `let returned: int option = result 3` |
| `pur` | `'T -> ZipApplicative<'T>` | `Pure value` | `let pured: int option = pur 3` |
| `lift2` | `('T -> 'U -> 'V) -> Applicative<'T> -> Applicative<'U> -> Applicative<'V>` | `Lift2 (f, x, y)` (or applicative fallback) | `let _: int option = lift2 (+) (Some 2) (Some 3)` |
| `map2` | `('T -> 'U -> 'V) -> ZipApplicative<'T> -> ZipApplicative<'U> -> ZipApplicative<'V>` | `Map2 (f, x, y)` (or zip fallback) | `let _: Result<int * int, string list> = map2 tuple2 (Error ["left"]) (Error ["right"])` |
| `traverse` | `('T -> Functor<'U>) -> Traversable<'T> -> Functor<Traversable<'U>>` | `Traverse (source, f)` | `let _: int list option = traverse (fun x -> Some (x + 1)) [1; 2]` |
| `sequence` | `Traversable<Functor<'T>> -> Functor<Traversable<'T>>` | `Sequence source` | `let _: int list option = sequence [Some 1; Some 2]` |
| `fold` | `('State -> 'T -> 'State) -> 'State -> Foldable<'T> -> 'State` | `Fold (source, folder, state)` | `let _: int = fold (+) 0 [1; 2]` |
| `foldMap` | `('T -> 'Monoid) -> Foldable<'T> -> 'Monoid` | `FoldMap (source, f)` | `let _: string = foldMap string [1; 2]` |
| `konst` | `'T -> 'U -> 'T` | none | `let _: int = konst 4 "ignored"` |
| `flip` | `('T -> 'U -> 'V) -> 'U -> 'T -> 'V` | none | `let _: int = flip (-) 2 5` |
| `curry` / `uncurry` | `(('T * 'U) -> 'V) -> 'T -> 'U -> 'V`; `('T -> 'U -> 'V) -> ('T * 'U) -> 'V` | none | `let _ = (curry (fun (x, y) -> x + y) 1 2, uncurry (+) (1, 2))` |
| `tuple2` / `tuple3` | `'T -> 'U -> 'T * 'U`; `'T -> 'U -> 'V -> 'T * 'U * 'V` | none | `let _ = (tuple2 1 "a", tuple3 1 2 3)` |
| `item1` / `item2` | tuple-like value -> component | `get_Item1`; `get_Item2` | `let _ = (item1 (1, "a"), item2 (1, "a"))` |
| `plus` | `'Monoid -> 'Monoid -> 'Monoid` | `(+) (x, y)` | `let _: string = plus "a" "b"` |
| `zero` | `'Monoid` | `Zero` | `let emptyText: string = zero` |
| `join` | `Monad<Monad<'T>> -> Monad<'T>` | `Join source` (or bind fallback) | `let _: int option = join (Some (Some 1))` |
| `extract` | `Comonad<'T> -> 'T` | `Extract source` | `let _: int = extract (lazy 3)` |
| `extend` | `(Comonad<'T> -> 'U) -> Comonad<'T> -> Comonad<'U>` | `Extend (source, f)` | `let _: Lazy<int> = extend (fun x -> extract x + 1) (lazy 2)` |
| `(>>=)` / `(=<<)` | monadic bind / flipped bind | `(>>=)` | `let _ = (Some 1 >>= (fun x -> Some (x + 1)), (fun x -> Some (x + 1)) =<< Some 1)` |
| `(<!>)` | functor map | `(<!>)` or `Map` | `let _: int option = ((+) 1) <!> Some 2` |
| `(<*>)` | applicative apply | `(<*>)` | `let _: int option = Some ((+) 1) <*> Some 2` |
| `(<.>)` | non-sequential/zip applicative apply | `(<.>)` | `let _: Result<int * int, string list> = tuple2 <!> (Error ["left"] : Result<int, string list>) <.> (Error ["right"] : Result<int, string list>)` |
| `(>=>)` / `(<=<)` | left-to-right / right-to-left Kleisli composition | `(>=>)` (bind fallback) | `let _ = (((fun x -> Some (x + 1)) >=> (fun x -> Some (x * 2))) 2, ((fun x -> Some (x * 2)) <=< (fun x -> Some (x + 1))) 2)` |
| `(++)` | generic append | `(+)` / append dispatcher | `let _: string = "a" ++ "b"` |
| `(\|>>)` | functor map with the source first | `(<!>)` or `Map` | `let _: int list = [1; 2] \|>> ((+) 1)` |
| `(<*)` / `(*>)` | sequence two applicatives, keep the left / right value | `(<*>)` and `Map` | `let _ = (Some 1 <* Some "ignored", Some 1 *> Some "kept")` |
| `curryN` / `uncurryN` | `('T1 * ... * 'Tn -> 'R) -> 'T1 -> ... -> 'Tn -> 'R`; the inverse | none | `let _: int * int = (curryN (fun (a, b, c) -> a + b + c) 1 2 3, uncurryN (fun a b c -> a + b + c) (1, 2, 3))` |
| `tryParse` / `parse` | `string -> 'T option`; `string -> 'T` (throws on bad input) | `TryParse` / `Parse` on the target type; built in for numeric types, `string`, `DateTime`, `DateTimeOffset`; `parse` also for enums | `let _: int option * float = (tryParse "42", parse "1.5")` |

For `Result`, generic `map2` and `(<.>)` accumulate appendable errors. Ordinary `lift2`, `(<*>)`, `applicative`, and qualified `Result.map2` keep the first error. See the [compiled comparison and pinned sources](idioms-and-antipatterns.md#independent-result-validation). Non-sequential behavior is type-dependent: on lists these operations zip rather than form a cartesian product; it does not imply parallel execution.

`tryParse` and `parse` pick the target type from the annotation and use `InvariantCulture`. Other types work through their own .NET `TryParse` or `Parse`, such as `Guid`; `tryParse` on an enum also returns `Some` in 1.9.1. For numbers they differ: `tryParse` uses `NumberStyles.Any`, so it accepts `"1,000"`, `"12.0"` and `"1e3"` as an `int`, while `parse` uses the type's default style and throws on `"1,000"`. Use `System.Int32.TryParse` when such input must be rejected. Name a concrete parser once (`let tryParseInt: string -> int option = tryParse`) and compose it: `tryParse >> Option.toResultWith "bad number"` gives a `Result`.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus

let tryParseInt: string -> int option = tryParse
let parsed: int option list = map tryParseInt ["42"; "1,000"; "12.0"; "1e3"; "x"]
let strict: int option =
    try Some (parse "1,000") with :? System.ArgumentException | :? System.FormatException -> None
printfn "tryParse=%A;parse=%A" parsed strict
// expect: tryParse=[Some 42; Some 1000; Some 12; Some 1000; None];parse=None
```

## Collections and conversions

Most of these work over lists, arrays, sequences and strings; support for other collections varies by function. The return type picks the instance for `ofSeq` and `ofList`, so annotate the binding.

| Name | API signature | Required static member | Compiled one-line example |
|---|---|---|---|
| `length` | `Foldable<'T> -> int` | `Length` | `let _: int = length [1; 2; 3]` |
| `exists` / `tryFind` | `('T -> bool) -> Foldable<'T> -> bool`; `-> 'T option` | `Exists`; `TryFind` | `let _: bool * int option = (exists ((=) 2) [1; 2], tryFind ((<) 1) [1; 2; 3])` |
| `item` / `tryItem` | `'K -> Indexed<'T> -> 'T`; `-> 'T option` | `Item`; `TryItem` | `let _: string * string option = (item 1 ["a"; "b"], tryItem 5 ["a"; "b"])` |
| `take` / `skip` | `int -> Collection<'T> -> Collection<'T>` | `Take`; `Skip` | `let _: int list * int list = (take 2 [1; 2; 3], skip 2 [1; 2; 3])` |
| `rev` | `Collection<'T> -> Collection<'T>` | `Rev` | `let _: int list = rev [1; 2; 3]` |
| `intercalate` | `'Monoid -> Foldable<'Monoid> -> 'Monoid` | `Intercalate` | `let _: string = intercalate ", " ["a"; "b"]` |
| `toList` / `toArray` / `toSeq` | `Foldable<'T> -> 'T list`; `'T []`; `seq<'T>` | `ToList`; `ToArray`; `ToSeq` | `let _: int list = toList (ResizeArray [1; 2])` |
| `ofSeq` / `ofList` | `seq<'T> -> Collection<'T>`; `'T list -> Collection<'T>` | `OfSeq`; `OfList` | `let _: string * int [] = (ofSeq ['a'; 'b'], ofList [1; 2])` |

## Monad transformer operations

These dispatch on the result type, so the same `get` works for `State` and for a `StateT` stack. Each needs the matching instance in the type: `get`/`put` need a state layer, `ask`/`local` a reader layer, `tell` a writer layer, `throw`/`catch` an error layer.

| Name | API signature | Required static member | Example |
|---|---|---|---|
| `lift` | `Monad<'T> -> MonadTrans<Monad<'T>>` | `Lift` | block below |
| `liftAsync` | `Async<'T> -> MonadAsync<'T>` | `LiftAsync` | block below |
| `throw` | `'E -> MonadError<'E, 'T>` | `Throw`; built in for `Result` and `Choice` | block below |
| `catch` | `MonadError<'E1, 'T> -> ('E1 -> MonadError<'E2, 'T>) -> MonadError<'E2, 'T>` | `Catch`; built in for `Result` and `Choice` | block below |
| `ask` / `local` | `MonadReader<'R, 'R>`; `('R1 -> 'R2) -> MonadReader<'R2, 'T> -> MonadReader<'R1, 'T>` | `get_Ask`; `Local` | block below |
| `get` / `put` | `MonadState<'S, 'S>`; `'S -> MonadState<'S, unit>` | `get_Get`; `Put` | block below |
| `tell` | `'Monoid -> MonadWriter<'Monoid, unit>` | `Tell` | block below |

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
open FSharpPlus.Data

let thrown: Result<int, string> = throw "bad"
let caught: Result<int, string> = catch thrown (fun e -> Ok (String.length e))
let counter: State<int, int> = monad {
    let! n = get
    do! put (n + 1)
    return n }
let logged: Writer<string list, int> = monad {
    do! tell ["start"]
    return 1 }
let doubled: Reader<int, int> = local ((*) 2) ask
let lifted: ReaderT<string, Result<int, string>> = lift (Ok 4)
let fromAsync: ResultT<Async<Result<int, string>>> = liftAsync (async { return 5 })
printfn "error=%A;%A" thrown caught
// expect: error=Error "bad";Ok 3
printfn "state=%A;writer=%A;reader=%d" (State.run counter 10) (Writer.run logged) (Reader.run doubled 4)
// expect: state=(10, 11);writer=(1, ["start"]);reader=8
printfn "lift=%A;%A" (ReaderT.run lifted "env") (Async.RunSynchronously (ResultT.run fromAsync))
// expect: lift=Ok 4;Ok 5
```

Inside a `ReaderT` or `StateT` `monad` block, a bare `let! x = ask` or `let! x = get` can fail with FS0071; annotate the bound expression, for example `(ask: ReaderT<string, Result<string, string>>)`. See [SRTP errors, case 6](srtp-errors.md#6-ask-or-get-inside-a-transformer-ce) for when it fails. Qualified forms such as `ReaderT.lift` and `StateT.lift` remain a valid fallback.

## Names that do not exist

These public generic names are absent from the 1.9.1 `FSharpPlus.Operators` API:

| Invented name | Use instead |
|---|---|
| `fmap` | `map` or `(<!>)` |
| `pure` | `result` for monads and sequential applicatives; `pur` for zip applicatives (`zapp`, `(<.>)`). Both are public and they differ: on `list`, `result 1` is `[1]`, while `pur 1` is a cyclic infinite list, so `List.length`, `List.toArray`, or any full traversal of it never terminates. Only zip it against a finite list. |
| `forM` | `traverse` with arguments in F#+ order |
| `mapM` | `traverse`; the only `mapM` in 1.9.1 is the module function `SeqT.mapM` |

## Obsolete names

These still compile in 1.9.1 but are marked `Obsolete`, so they produce a warning, not an error:

| Obsolete name | Use instead |
|---|---|
| `liftA2` | `lift2` |
| `applicative'`, `applicative2'`, `applicative3'` | `zapp`, `zapp2`, `zapp3` |
