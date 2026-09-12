# Application workflows

## Contents

- [U1 Optional code actions](#u1-optional-code-actions)
- [U2 Selective mapping](#u2-selective-mapping)

## U1 Optional code actions

**Problem.** Produce an action only when a reference and target resolve and the
destination is absent; parse optional configuration without hiding malformed
values. Marksman U1 is application implementation using `monad'`, `guard`, and
`monad`. This establishes usage, not deployment. The following is an independent
stand-in: strings and a set replace its workspace and protocol types.

Use the builder when several generic steps must compose; a `match` chain is
usually clearer for one or two concrete `option`s. `Result` is separate because
missing and invalid configuration have different meanings.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus

let mutable resolutions = 0
let locate text = if text = "link" then Some "draft.md" else None
let resolve target = resolutions <- resolutions + 1; if target = "draft.md" then Some "/docs/draft.md" else None
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
    | Some text -> match System.Int32.TryParse text with | true, n when n > 0 -> Ok n | _ -> Error ("invalid timeout: " + text)
let good, absent, bad = parseConfig (Some "12"), parseConfig None, parseConfig (Some "zero")
if valid <> Some "create:/docs/draft.md" || unresolved <> None || existing <> None || afterMissing <> 1 then failwith "option behavior"
if good <> Ok 12 || absent <> Ok 30 || bad <> Error "invalid timeout: zero" then failwith "config behavior"
printfn "actions=%A;%A;%A;resolutions=%d" valid unresolved existing resolutions
// expect: actions=Some "create:/docs/draft.md";None;None;resolutions=2
printfn "config=%A;%A;%A" good absent bad
// expect: config=Ok 12;Ok 30;Error "invalid timeout: zero"
```

The counter is inside `resolve`; failed `locate` therefore proves its later
binder was skipped. It says nothing about work evaluated before a bind. See
[computation expressions](computation-expressions.md) and [inference repairs](srtp-errors.md).

## U2 Selective mapping

Sharpino U2 is a sample application that opens `FSharpPlus.Operators` and uses
the F#+ mapping operator `|>>`. Adjacent `result`, `taskResult`, and
`List.traverseResultM` must be attributed to their actual provider, not to that
open. Preserve this style while maintaining such code; for new monomorphic list
code prefer `List.map`.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
open FSharpPlus.Operators

let source = [1; 2; 3]
let operatorForm: int list = source |>> ((*) 10)
let concreteForm = source |> List.map ((*) 10)
let inline increment container = map ((+) 1) container
let listResult: int list = increment [1; 2]
let optionResult: int option = increment (Some 4)
let missing: int option = increment None
if operatorForm <> concreteForm || missing <> None then failwith "mapping semantics"
printfn "mapped=%A;equal=%b" operatorForm (operatorForm = concreteForm)
// expect: mapped=[10; 20; 30];equal=true
printfn "generic=%A;%A;%A" listResult optionResult missing
// expect: generic=[2; 3];Some 5;None
```

The inline helper earns its genericity because it is used for two containers.
See [namespace ownership](namespaces-and-shadowing.md).
