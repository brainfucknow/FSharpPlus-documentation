# Computation expressions

Examples assume the pinned package reference and the indicated opens. The generic `monad` builder delays the computation through the monad's `Delay` hook and supports effect syntax; `monad'` is the strict form. Builder properties refine the protocol without introducing different top-level builders.

| Need | Builder documented by 1.9.1 |
|---|---|
| Lazy/delayed monad with effect support | `monad` |
| Strict/eager monad with effect support | `monad'` or `monad.strict` |
| Delayed monad with empty/choice | `monad.plus` |
| Strict monad with empty/choice | `monad.plus'` |
| Delayed effect-capable form explicitly | `monad.fx` |
| Strict effect-capable form explicitly | `monad.fx'` |

## Option and Result

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
let optionValue: int option = monad {
    let! x = Some 2
    return x + 1 }
let resultValue: Result<int, string> = monad {
    let! x = Ok 2
    return x + 1 }
```

Both builders compile for `option` and `Result` and give the same value. Neither type has a delay of its own, so the lazy `monad` runs the body at once anyway; `monad'` says so directly. The choice matters for types that can delay, such as `Async`, `Lazy`, `seq`, and functions, where `monad` defers the body until the value is run.

## Telling the builder its type

Builders are generic values, so the compiler often needs the result type. Annotate the binding, without parentheses, and leave `_` holes for what inference can fill. The parenthesised form `let (items: list<_>) = ...` is equivalent.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
let items: _ list = monad.plus {
    return 5
    return 6 }
let (sameItems: list<_>) = monad.plus' {
    return 5
    return 6 }
printfn "%A %A" items sameItems
// expect: [5; 6] [5; 6]
```

The builders also take the type as an explicit type argument, which fixes it once for every use. Upstream tests write `let task<'t> = monad'<Task<'t>>`, `applicative<Validation<_,_>> { ... }`, and `monad<SeqT<Async<bool>, 't>>.plus`.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open System.Threading.Tasks
open FSharpPlus
open FSharpPlus.Data
let taskM<'t> = monad'<Task<'t>>
let answer = taskM {
    let! x = Task.FromResult 41
    return x + 1 }
let check (name: string) (age: int) = applicative<Validation<_, _>> {
    let! n = if name = "" then Failure ["name"] else Success name
    and! a = if age < 0 then Failure ["age"] else Success age
    return n, a }
printfn "task=%d" answer.Result
// expect: task=42
printfn "%A" (check "" -1)
// expect: Failure ["name"; "age"]
```

Use a type argument when one builder alias serves many expressions, or when no binding is available to annotate. Otherwise annotating the binding or the function's return type is the more common form.

## Transformers

`FSharpPlus.Data` ships `ReaderT`, `WriterT`, `StateT`, `OptionT`, `ValueOptionT`, `ResultT`, `ChoiceT`, `ListT`, `SeqT`, and `ContT`. `SeqT` has both the original nested-monad representation and a newer two-parameter streaming representation; use the module operations matching the inferred type.

Here `ReaderT` is layered over `Result`. Construction, `lift`, binding through `monad`, and running are all explicit:

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
open FSharpPlus.Data

type Stack<'T> = ReaderT<string, Result<'T, string>>
let lifted: Stack<int> = ReaderT.lift (Ok 4 : Result<int, string>)
let program: Stack<int> = monad {
    let! x = lifted
    return x + 1 }
let outcome: Result<int, string> = ReaderT.run program "environment"
```

## Applicative computation expressions

Version 1.9.1 provides sequential `applicative`, `applicative2`, and `applicative3`, plus non-sequential/zip `zapp`, `zapp2`, and `zapp3`. The older names `applicative'`, `applicative2'`, and `applicative3'` remain but are obsolete aliases for the `zapp` family.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
let combined: int option = applicative {
    let! x = Some 1
    and! y = Some 2
    return x + y }
```

### Independent Result checks with zapp

Use `zapp` for independent Result-returning checks with appendable errors. In contrast, `applicative` uses ordinary `lift2` and keeps the first Result error, even though it also uses `and!`.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
let combine (name: Result<string, string list>) (age: Result<int, string list>) : Result<string * int, string list> = zapp {
    let! n = name
    and! a = age
    return n, a }
printfn "%A" (combine (Error ["name"]) (Error ["age"]))
// expect: Error ["name"; "age"]
printfn "%A" (combine (Error ["name"]) (Ok 35))
// expect: Error ["name"]
printfn "%A" (combine (Ok "Ada") (Error ["age"]))
// expect: Error ["age"]
printfn "%A" (combine (Ok "Ada") (Ok 35))
// expect: Ok ("Ada", 35)
let firstError: Result<string * int, string list> = applicative {
    let! n = (Error ["name"] : Result<string, string list>)
    and! a = (Error ["age"] : Result<int, string list>)
    return n, a }
printfn "ordinary=%A" firstError
// expect: ordinary=Error ["name"]
```

`zapp`'s [MergeSources uses generic map2](https://github.com/fsprojects/FSharpPlus/blob/d1643d2ac9346c6b2e6bf52765a0648177b5ed9e/src/FSharpPlus/Builders.fs#L313-L320). Error accumulation depends on the Result error type supporting F#+'s append/`Plus` operation. Non-sequential does not mean parallel; `and!` syntax alone promises neither concurrency nor accumulation. Use bind/`monad'` when a later check depends on an earlier successful value. See [operation selection and Validation compatibility](idioms-and-antipatterns.md#independent-result-validation).
