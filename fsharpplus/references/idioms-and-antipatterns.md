# Idioms and antipatterns

## Generic versus concrete

Use generic dispatch when the surrounding inline function is truly polymorphic or the container is an F#+ type. A known list gains nothing from SRTP; `List.map` gives simpler errors and makes intent explicit.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
let overGeneric: int list = map ((+) 1) [1; 2]
let plain: int list = List.map ((+) 1) [1; 2]
```

## Validation versus Result

`Validation` accumulates errors through its applicative instance when the error type supports append; it deliberately is not a monad. `Result` binding stops at the first `Error`. Convert with `Validation.ofResult` and `Validation.toResult`.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
open FSharpPlus.Data
let badName: Validation<string list, string> = Failure ["name"]
let badAge: Validation<string list, int> = Failure ["age"]
let accumulated: Validation<string list, string * int> = lift2 tuple2 badName badAge
let stopped: Result<int, string> = Error "first" >>= fun _ -> Error "second"
let roundTrip: Result<int, string> = Error "x" |> Validation.ofResult |> Validation.toResult
```

## NonEmptyList and DList

Use `NonEmptyList` when emptiness would be invalid and consumers need a guaranteed head; `singleton` and `ofList` are the direct constructors (`ofList` returns an option). Use `DList` when repeatedly appending lists before one final conversion, avoiding repeated left-list traversal.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
open FSharpPlus.Data
let one = NonEmptyList.singleton 1
let maybeMany = NonEmptyList.ofList [1; 2]
let built = DList.singleton 1 |> DList.append (DList.singleton 2)
let materialized = DList.toList built
```

## Lens conventions

Standard optics use an underscore prefix: tuple lenses `_1`, `_2`, `_3`; option prism `_Some`. Read with `view`, replace with `setl`, and transform with `over`. A prism such as `_Some` is normally read with `preview`, not `view`.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
open FSharpPlus.Lens
let read: int = view _1 (1, "a")
let updated: int * string = setl _1 3 (1, "a")
let incremented: int * string = over _1 ((+) 1) (1, "a")
let optional: int option = preview _Some (Some 4)
```

## Antipatterns

Avoid long point-free chains of generic operators: intermediate types disappear and overload diagnostics become harder to locate. Name stages and annotate boundaries instead. Do not open all of `FSharpPlus` merely for one extension—qualify the extended module or open `FSharpPlus.Operators` narrowly.

Prefer a built-in `async` or `task` computation expression when it directly describes the effect. Likewise, if another library deliberately provides a specialized `result` builder, use that builder rather than generic `monad`; specialization is often clearer than showcasing polymorphism.
