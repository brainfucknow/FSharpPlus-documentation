# Generic functions and operators

All examples assume `#r "nuget: FSharpPlus, 1.9.1"` followed by `open FSharpPlus`. Signatures below use the API reference's higher-kinded-type notation. “Member” is the instance hook selected by the dispatcher; built-in overloads can also be selected.

| Name | API signature | Required static member | Compiled one-line example |
|---|---|---|---|
| `map` | `('T -> 'U) -> Functor<'T> -> Functor<'U>` | `Map (source, mapping)` | `let _: int option = map ((+) 1) (Some 2)` |
| `bind` | `('T -> Monad<'U>) -> Monad<'T> -> Monad<'U>` | `(>>=) (source, binder)` | `let _: int option = bind (fun x -> Some (x + 1)) (Some 2)` |
| `result` | `'T -> Applicative<'T>` | `Return value` | `let returned: int option = result 3` |
| `lift2` | `('T -> 'U -> 'V) -> Applicative<'T> -> Applicative<'U> -> Applicative<'V>` | `Lift2 (f, x, y)` (or applicative fallback) | `let _: int option = lift2 (+) (Some 2) (Some 3)` |
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
| `(>=>)` / `(<=<)` | left-to-right / right-to-left Kleisli composition | `(>=>)` (bind fallback) | `let _ = (((fun x -> Some (x + 1)) >=> (fun x -> Some (x * 2))) 2, ((fun x -> Some (x * 2)) <=< (fun x -> Some (x + 1))) 2)` |
| `(++)` | generic append | `(+)` / append dispatcher | `let _: string = "a" ++ "b"` |

## Names that do not exist

These public generic names are absent from the 1.9.1 `FSharpPlus.Operators` API:

| Invented name | Use instead |
|---|---|
| `fmap` | `map` or `(<!>)` |
| `pure` | `result` (the internal builder helper `pur` is not the public return function) |
| `forM` | `traverse` with arguments in F#+ order |
| `liftA2` | `lift2` |
| `mapM` | `traverse`; the only `mapM` in 1.9.1 is the module function `SeqT.mapM` |
