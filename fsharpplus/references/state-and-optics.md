# State and optics

## Contents

- [U3 State-threaded repository](#u3-state-threaded-repository)
- [U4 Optics over language data](#u4-optics-over-language-data)

## U3 State-threaded repository

U3 is playground/game implementation: `State.get`, `State.put`, and `monad`
thread an immutable `Planet`. It is not evidence of a commercial game. This
distilled repository removes Godot, rendering, persistence, and concurrency.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
open FSharpPlus.Data

type Repo = { Items: Map<int, string>; NextId: int }
let insert name : State<Repo, int> = monad {
    let! state = State.get
    let id = state.NextId
    do! State.put { Items = Map.add id name state.Items; NextId = id + 1 }
    return id }
let find id : State<Repo, string option> = monad { let! state = State.get in return Map.tryFind id state.Items }
let rename id name : State<Repo, unit> = monad {
    let! state = State.get
    do! State.put { state with Items = if Map.containsKey id state.Items then Map.add id name state.Items else state.Items } }
let program : State<Repo, int * int * string option> = monad {
    let! a = insert "north"
    let! b = insert "south"
    do! rename a "north-updated"
    let! missing = find 99
    return a, b, missing }
let initial = { Items = Map.empty; NextId = 10 }
let value, finalState = State.run program initial
if value <> (10, 11, None) || finalState.NextId <> 12 || finalState.Items <> Map [(10,"north-updated");(11,"south")] then failwith "state"
if initial <> { Items = Map.empty; NextId = 10 } then failwith "input mutated"
printfn "value=%A;next=%d;items=%A" value finalState.NextId (Map.toList finalState.Items)
// expect: value=(10, 11, None);next=12;items=[(10, "north-updated"); (11, "south")]
printfn "initial=%A" (Map.toList initial.Items, initial.NextId)
// expect: initial=([], 10)
```

The explicit `State.run` is the boundary. This gives state threading, not
transactions or thread safety; direct record-returning functions can be simpler.

## U4 Optics over language data

LABS U4 is active language-processing implementation defining and calling
lenses. Commented code is excluded. The stand-in below keeps only nested record
updates; pattern matching remains preferable for a single local update.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus.Lens

type Predicate = { Name: string; Negated: bool }
type Property = { Predicate: Predicate; SourceLine: int }
let inline predicateLens x = lens (fun (p: Property) -> p.Predicate) (fun (p: Property) value -> { p with Predicate = value }) x
let inline nameLens x = lens (fun (p: Predicate) -> p.Name) (fun (p: Predicate) value -> { p with Name = value }) x
let inline negatedLens x = lens (fun (p: Predicate) -> p.Negated) (fun (p: Predicate) value -> { p with Negated = value }) x
let original = { Predicate = { Name = "ready"; Negated = false }; SourceLine = 7 }
let renamed = original |> over predicateLens (over nameLens (fun n -> n.ToUpperInvariant()))
let replaced = original |> setl predicateLens { Name = "done"; Negated = true }
let viewed = view predicateLens original |> view nameLens
let repeated = original |> setl predicateLens original.Predicate |> setl predicateLens original.Predicate
if renamed.SourceLine <> 7 || renamed.Predicate.Name <> "READY" || replaced.SourceLine <> 7 || viewed <> "ready" || repeated <> original then failwith "lens checks"
printfn "optics=%s;%A;sibling=%d" viewed renamed.Predicate replaced.SourceLine
// expect: optics=ready;{ Name = "READY"
printfn "laws=%b;%b" (view predicateLens (setl predicateLens replaced.Predicate original) = replaced.Predicate) (repeated = original)
// expect: laws=true;true
```

These finite get/set and repeated-set checks are regression evidence, not a law
proof. An optional path needs a prism and `preview`, never a total-lens `view`.
