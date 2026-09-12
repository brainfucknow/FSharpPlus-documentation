# State and optics

## State-threaded repository

Thread repository updates through an immutable value. This pattern appears in the [Godot playground repository](https://github.com/ZeromaXHe/ZeromaX-s-Playground/blob/bd3ffa4c800ed96253682c6abdc4a8b3765fc159/MainGame/FrontEndToolFS/HexGlobal/Repository.fs#L22-L65).

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
let find id : State<Repo, string option> = monad {
    let! state = State.get
    return Map.tryFind id state.Items }
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
printfn "value=%A;next=%d;items=%A" value finalState.NextId (Map.toList finalState.Items)
// expect: value=(10, 11, None);next=12;items=[(10, "north-updated"); (11, "south")]
printfn "initial=%A" (Map.toList initial.Items, initial.NextId)
// expect: initial=([], 10)
```

The explicit `State.run` is the boundary. This gives state threading, not
transactions or thread safety; direct record-returning functions can be simpler.

## Optics over language data

Compose lenses to update and inspect a field nested inside language data. The pattern appears in [LABS lens definitions](https://github.com/labs-lang/labs/blob/482cf8f22db96eab844cab5677eda6b9ad7b8680/LabsParser/Types.fs#L89-L105) and [their callers](https://github.com/labs-lang/labs/blob/482cf8f22db96eab844cab5677eda6b9ad7b8680/Frontend/SymbolTable.fs#L1-L80).

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus.Lens

type Predicate = { Name: string; Negated: bool }
type Property = { Predicate: Predicate; SourceLine: int }
let inline predicateLens x = lens (fun (p: Property) -> p.Predicate) (fun (p: Property) value -> { p with Predicate = value }) x
let inline nameLens x = lens (fun (p: Predicate) -> p.Name) (fun (p: Predicate) value -> { p with Name = value }) x
let inline negatedLens x = lens (fun (p: Predicate) -> p.Negated) (fun (p: Predicate) value -> { p with Negated = value }) x
let original = { Predicate = { Name = "ready"; Negated = false }; SourceLine = 7 }
let renamed = original |> over (predicateLens << nameLens) (fun n -> n.ToUpperInvariant())
let replaced = original |> setl predicateLens { Name = "done"; Negated = true }
let viewed = view (predicateLens << nameLens) original
let repeated = original |> setl predicateLens original.Predicate |> setl predicateLens original.Predicate
printfn "optics=%s;%s;%b;sibling=%d" viewed renamed.Predicate.Name renamed.Predicate.Negated replaced.SourceLine
// expect: optics=ready;READY;false;sibling=7
printfn "laws=%b;%b" (view predicateLens (setl predicateLens replaced.Predicate original) = replaced.Predicate) (repeated = original)
// expect: laws=true;true
```

These finite get/set and repeated-set checks are regression evidence, not a law
proof. An optional path needs a prism and `preview`, never a total-lens `view`.
