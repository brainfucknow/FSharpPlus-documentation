# Application workflows

## Optional code actions

Build an action only after its reference and target resolve, and distinguish missing configuration from invalid values. This pattern appears in [Marksman's code actions](https://github.com/artempyanykh/marksman/blob/4340227338f8d2b369bf38d19f029f1effb532f6/Marksman/CodeActions.fs#L131-L149) and [configuration parser](https://github.com/artempyanykh/marksman/blob/4340227338f8d2b369bf38d19f029f1effb532f6/Marksman/Config.fs#L86-L103).

Use the builder when several generic steps must compose; a `match` chain is
usually clearer for one or two concrete `option`s. `Result` is separate because
missing and invalid configuration have different meanings.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus

let mutable resolutions = 0
let locate text = if text = "link" then Some "draft.md" else None
let resolve target =
    resolutions <- resolutions + 1
    if target = "draft.md" then Some "/docs/draft.md" else None
let action existing text : string option = monad' {
    let! reference = locate text
    let! target = resolve reference
    do! guard (not (Set.contains target existing))
    return "create:" + target }

let valid = action Set.empty "link"
let unresolved = action Set.empty "other"
let afterMissing = resolutions
let existing = action (Set.singleton "/docs/draft.md") "link"
let parseConfig (value: string option) =
    match value with
    | None -> Ok 30
    | Some text ->
        match System.Int32.TryParse text with
        | true, n when n > 0 -> Ok n
        | _ -> Error ("invalid timeout: " + text)
let good, absent, bad = parseConfig (Some "12"), parseConfig None, parseConfig (Some "zero")
printfn "actions=%A;%A;%A;resolutions=%d" valid unresolved existing resolutions
// expect: actions=Some "create:/docs/draft.md";None;None;resolutions=2
printfn "config=%A;%A;%A" good absent bad
// expect: config=Ok 12;Ok 30;Error "invalid timeout: zero"
```

The counter is inside `resolve`; failed `locate` therefore proves its later
binder was skipped. It says nothing about work evaluated before a bind. See
[computation expressions](computation-expressions.md) and [inference repairs](srtp-errors.md).

## Selective mapping

Maintain operator-based mapping alongside concrete and reusable generic mapping. The operator use appears in the [Sharpino sample](https://github.com/tonyx/Sharpino/blob/a397f8e50384e1fe81aeb33640d2a15d50ec4f39/Sharpino.Sample.16/MaterialManager.fs#L35-L45).

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus

let source = [1; 2; 3]
let operatorForm: int list = source |>> ((*) 10)
let concreteForm = source |> List.map ((*) 10)
let inline increment container = map ((+) 1) container
let listResult: int list = increment [1; 2]
let optionResult: int option = increment (Some 4)
let missing: int option = increment None
printfn "mapped=%A;equal=%b" operatorForm (operatorForm = concreteForm)
// expect: mapped=[10; 20; 30];equal=true
printfn "generic=%A;%A;%A" listResult optionResult missing
// expect: generic=[2; 3];Some 5;None
```

The inline helper earns its genericity because it is used for two containers.
See [namespace ownership](namespaces-and-shadowing.md).
