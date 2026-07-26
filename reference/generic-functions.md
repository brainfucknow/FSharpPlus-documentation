# Generic functions with no upstream example

> **Verification status: every snippet here is compiled and executed in CI.**
> Blocks are tagged ```` ```fsharp verify ````, so their `// val` lines are asserted against
> FSharpPlus 1.9.1 rather than being decorative. The gap analysis is reproducible via
> the `gap` command of [`../tools/Verify`](../tools/Verify).

## Why this page exists

`verify gap` compares every named function in the `AutoOpen` `Operators` module against
every mention in `docsrc/content/*.fsx` at the pinned commit:

```
public named functions in the AutoOpen Operators module: 192
with zero mentions anywhere in upstream docsrc/content:   53  (27%)
```

**Zero mentions** means no example, no prose, no passing reference — a model has nothing to imitate.
The real gap is larger, because a single mention is not an example; this measure only catches total
absence.

These are not obscure corners. They include `sort`, `sortBy`, `distinct`, `groupBy`, `maxBy`, `minBy`,
`forall`, `tryFind`, `scan`, `sumBy` and the whole `item1`–`item5` / `mapItem1`–`mapItem5` family —
exactly the functions ordinary code reaches for, and exactly the ones where a model will assume
`List.sortBy` semantics and get the generic version subtly wrong.

This page is the plan's Step 3 applied to that list: draft, compile, discard what fails, keep what
passes.

## Sorting

All of these are generic over the collection type, which is the point — the same `sort` works on
`list` and `array`. That genericity is invisible in a docs corpus that never shows it.

```fsharp verify name=generic_sort
open FSharpPlus

let sortedList : int list = sort [3; 1; 2]
// val sortedList : int list = [1; 2; 3]

let sortedArray : int [] = sort [|3; 1; 2|]
// val sortedArray : int [] = [|1; 2; 3|]

let byLength : string list = sortBy (fun (s: string) -> s.Length) ["ccc"; "a"; "bb"]
// val byLength : string list = ["a"; "bb"; "ccc"]

let biggestFirst : int list = sortByDescending id [1; 3; 2]
// val biggestFirst : int list = [3; 2; 1]
```

## Dedup and grouping

```fsharp verify name=generic_distinct_group
open FSharpPlus

let deduped : int list = distinct [1; 2; 2; 3; 1]
// val deduped : int list = [1; 2; 3]

let dedupedByKey : int list = distinctBy (fun x -> x % 3) [1; 2; 4; 5; 3]
// val dedupedByKey : int list = [1; 2; 3]

let grouped : (int * int list) list = groupBy (fun x -> x % 2) [1; 2; 3; 4]
// val grouped : (int * int list) list = [(1, [1; 3]); (0, [2; 4])]
```

Note `groupBy`'s return type: `Collection<Key * Collection<T>>` (`Operators.fs:1226`). For `list` that
is `(int * int list) list` — a list of tuples, not a `Map`. Models that assume a dictionary-shaped
result get a type error, which is at least a loud failure.

## Foldable queries

These work on any `Foldable`, not just `list`.

```fsharp verify name=generic_foldable_queries
open FSharpPlus

let allPositive : bool = forall (fun x -> x > 0) [1; 2; 3]
// val allPositive : bool = true

let firstBig : int option = tryFind (fun x -> x > 1) [1; 2; 3]
// val firstBig : int option = Some 2

let lastItem : int option = tryLast [1; 2; 3]
// val lastItem : int option = Some 3

let biggest : int = maximum [1; 3; 2]
// val biggest : int = 3

let longest : string = maxBy (fun (s: string) -> s.Length) ["a"; "ccc"; "bb"]
// val longest : string = "ccc"

let shortest : string = minBy (fun (s: string) -> s.Length) ["aa"; "c"; "bbb"]
// val shortest : string = "c"
```

`pick` returns the projected value directly — **not** an option — and raises when nothing matches
(`Operators.fs:694`). `tryFind` is the total counterpart.

```fsharp verify name=generic_pick
open FSharpPlus

let picked : string = pick (fun x -> if x > 1 then Some (string x) else None) [1; 2; 3]
// val picked : string = "2"
```

## Accumulating and limiting

```fsharp verify name=generic_scan_limit_sum
open FSharpPlus

let running : int list = scan (+) 0 [1; 2; 3]
// val running : int list = [0; 1; 3; 6]

let firstTwo : int list = limit 2 [1; 2; 3; 4]
// val firstTwo : int list = [1; 2]

let total : int = sumBy id [1; 2; 3]
// val total : int = 6
```

`scan` includes the seed, so the result is one element longer than the input. `limit` is a total
`take` — it does not raise when the collection is shorter than the count.

## Tuple accessors

Fifteen functions (`item1`–`item5`, `mapItem1`–`mapItem5`, plus the `Item*` control types) with **zero**
mentions upstream. They generalise `fst`/`snd` to arbitrary positions and arities.

```fsharp verify name=tuple_accessors
open FSharpPlus

let second : string = item2 (1, "two", 3.0)
// val second : string = "two"

let third : float = item3 (1, "two", 3.0)
// val third : float = 3.0

let bumped : int * string = mapItem1 ((+) 10) (1, "two")
// val bumped : int * string = (11, "two")

let shouted : int * string = mapItem2 (fun (s: string) -> s.ToUpper ()) (1, "two")
// val shouted : int * string = (1, "TWO")
```

`mapItem*` returns a tuple whose element type may change (`'T -> 'U`, `Operators.fs:1444`), so it is a
lens-like update, not a mutation.

## `lift3`

`lift2` is documented; `lift3` sitting immediately beside it (`Operators.fs:237`) is not mentioned once.

```fsharp verify name=lift3_example
open FSharpPlus

let summed : int option = lift3 (fun a b c -> a + b + c) (Some 1) (Some 2) (Some 3)
// val summed : int option = Some 6

let shortCircuited : int option = lift3 (fun a b c -> a + b + c) (Some 1) None (Some 3)
// val shortCircuited : int option = None
```

## `gets` — reading a projection of state

`get` is documented; `gets` (`Operators.fs:1062`) is not. It is `get` composed with a projection.

```fsharp verify name=state_gets
open FSharpPlus
open FSharpPlus.Data

let doubledState : int * int = State.run (gets ((*) 2)) 21
// val doubledState : int * int = (42, 21)
```

The state itself is returned unchanged as the second component — `gets` reads, it does not modify.

## Still uncovered

Deliberately not attempted here, with reasons rather than silence:

| Functions | Why not yet |
|---|---|
| `listen`, `callCC` | Need a `Writer` / `Cont` scaffold to demonstrate meaningfully; worth a page each, not a one-liner |
| `arrFirst`, `arrSecond`, `fanout`, `fanin`, `getApp`, `getCatId` | Arrow/Category surface — needs the abstraction explained first, not just a value |
| `setItem` | Mutates via `member set_Item` and returns `unit`, so it has no `// val` shape |
| `ofBytesBE`, `toBytesBE`, `ofBytesWithOptions`, `toBigInt`, `isqrt`, `sqrtRem`, `getPi`, `getOne`, `getMinValue`, `getMaxValue`, `negate`, `tryNegate`, `trySubtract` | Numeric/`Math.Generic` surface; belongs with a numerics page |
| `choosei` | Indexed functor; needs the `*i` indexed family covered together |
| `liftM`, `getEmpty` | Legacy/internal-ish aliases — worth confirming they are intended public API before documenting |

Run `verify gap --upstream <path> --covered-by reference/generic-functions.md` for the live
split between covered and uncovered.
