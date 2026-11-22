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
let positive n = if n > 0 then Some n else None
let parseTimeout: string -> int option = tryParse >=> positive
let parseConfig value : Result<int, string> =
    match value with
    | None -> Ok 30
    | Some text -> parseTimeout text |> Option.toResultWith ("invalid timeout: " + text)
let good, absent, bad = parseConfig (Some "12"), parseConfig None, parseConfig (Some "zero")
let lenient = parseConfig (Some "1,000")
printfn "actions=%A;%A;%A;resolutions=%d" valid unresolved existing resolutions
// expect: actions=Some "create:/docs/draft.md";None;None;resolutions=2
printfn "config=%A;%A;%A;lenient=%A" good absent bad lenient
// expect: config=Ok 12;Ok 30;Error "invalid timeout: zero";lenient=Ok 1000
```

The counter is inside `resolve`; failed `locate` therefore proves its later
binder was skipped. It says nothing about work evaluated before a bind. See
[computation expressions](computation-expressions.md) and [inference repairs](srtp-errors.md).

`parseTimeout` composes two `option`-returning steps with `>=>`, as the
[F#+ tutorial](https://github.com/fsprojects/FSharpPlus/blob/d1643d2ac9346c6b2e6bf52765a0648177b5ed9e/docsrc/content/tutorial.fsx#L79-L97)
does with `tryParseInt >=> tryDivide 100`. The annotation names the parsed
type. `Option.toResultWith` then names the error, as in
[upstream's codec test](https://github.com/fsprojects/FSharpPlus/blob/d1643d2ac9346c6b2e6bf52765a0648177b5ed9e/tests/FSharpPlus.Tests/General.fs#L1323).
`tryParse` accepts more than `System.Int32.TryParse`, such as `"1,000"`,
`"12.0"` and `"1e3"`; keep `Int32.TryParse` when existing code must reject
that input.

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
