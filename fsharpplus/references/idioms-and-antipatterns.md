# Idioms and antipatterns

## Generic versus concrete

Use generic dispatch when the surrounding inline function is truly polymorphic or the container is an F#+ type. A known list gains nothing from SRTP; `List.map` gives simpler errors and makes intent explicit.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
let overGeneric: int list = map ((+) 1) [1; 2]
let plain: int list = List.map ((+) 1) [1; 2]
```

## Independent Result validation

For independent checks that already return `Result`, prefer `zapp` with `let!`/`and!`, or generic `map2`/`(<.>)`. They accumulate errors when the error type supports F#+'s append/`Plus` operation, such as `string list`; no `Validation` conversion is needed. See the [four-state recipe](validation-and-custom-types.md#diagnostic-semantics).

The operation matters: generic `map2` and `(<.>)` on `Result` accumulate, while ordinary `lift2`, `(<*>)`, `applicative`, and qualified `Result.map2` retain the first error. Do not replace generic `map2` with `Result.map2` when accumulation is intended. This is an intentional exception to preferring concrete functions in monomorphic code.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
let badName: Result<string, string list> = Error ["name"]
let badAge: Result<int, string list> = Error ["age"]
let accumulated: Result<string * int, string list> = map2 tuple2 badName badAge
let zipped: Result<string * int, string list> = tuple2 <!> badName <.> badAge
let ordinary: Result<string * int, string list> = lift2 tuple2 badName badAge
let applied: Result<string * int, string list> = tuple2 <!> badName <*> badAge
let qualified: Result<string * int, string list> = Result.map2 tuple2 badName badAge
printfn "accumulate=%A;%A" accumulated zipped
// expect: accumulate=Error ["name"; "age"];Error ["name"; "age"]
printfn "first=%A;%A;%A" ordinary applied qualified
// expect: first=Error ["name"];Error ["name"];Error ["name"]
```

Non-sequential applicatives describe how effects combine; they do not guarantee parallel execution. `and!` alone does not imply accumulation either: the builder and type select the operations. For dependent checks, use `Result.bind` or a monadic CE; an `Error` skips the later binder, not work already evaluated before binding.

### Existing Validation APIs

`Validation` remains supported; the 1.9.1 source does not justify calling it deprecated. Its ordinary applicative accumulates appendable errors, which can matter for existing APIs or generic algorithms expressed through ordinary `lift2`/`(<*>)` rather than non-sequential operations. It has no generic monadic `Bind`. Keep it at such boundaries; do not introduce a round trip solely to accumulate existing Result errors.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
open FSharpPlus.Data
let left: Validation<string list, string> = Failure ["name"]
let right: Validation<string list, int> = Failure ["age"]
let existingApi: Validation<string list, string * int> = lift2 tuple2 left right
printfn "%A" (Validation.toResult existingApi)
// expect: Error ["name"; "age"]
```

Pinned FSharpPlus 1.9.1 sources: [zip apply and generic Map2](https://github.com/fsprojects/FSharpPlus/blob/d1643d2ac9346c6b2e6bf52765a0648177b5ed9e/src/FSharpPlus/Control/ZipApplicative.fs#L98-L159) call `Result.apply2With Plus.Invoke`; [error combination](https://github.com/fsprojects/FSharpPlus/blob/d1643d2ac9346c6b2e6bf52765a0648177b5ed9e/src/FSharpPlus/Extensions/Result.fs#L177-L181) appends left-to-right. Compare [ordinary Result apply/map2](https://github.com/fsprojects/FSharpPlus/blob/d1643d2ac9346c6b2e6bf52765a0648177b5ed9e/src/FSharpPlus/Extensions/Result.fs#L22-L50) and [Validation instances](https://github.com/fsprojects/FSharpPlus/blob/d1643d2ac9346c6b2e6bf52765a0648177b5ed9e/src/FSharpPlus/Data/Validation.fs#L274-L308).

## NonEmptyList and DList

Use `NonEmptyList` when emptiness would be invalid and consumers need a guaranteed head. `singleton` always succeeds; `ofList` throws `ArgumentException` on an empty list, and `tryOfList` is the option-returning form. Use `DList` when repeatedly appending lists before one final conversion, avoiding repeated left-list traversal. `DList.append left right` keeps `left` first.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
open FSharpPlus.Data
let one: NonEmptyList<int> = NonEmptyList.singleton 1
let some: NonEmptyList<int> option = NonEmptyList.tryOfList [1; 2]
let none: NonEmptyList<int> option = NonEmptyList.tryOfList []
let unchecked: NonEmptyList<int> = NonEmptyList.ofList [3]
let built: DList<int> = DList.append (DList.singleton 1) (DList.singleton 2)
printfn "%A" none
// expect: None
printfn "%A" (NonEmptyList.toList unchecked)
// expect: [3]
printfn "%A" (DList.toList built)
// expect: [1; 2]
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
