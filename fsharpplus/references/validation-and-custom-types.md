# Validation and custom types

## Diagnostic semantics

Accumulate independent diagnostics while keeping dependent checks short-circuiting. The distinction appears in [SafetyFirst error combination](https://github.com/ntwilson/SafetyFirst/blob/973fba6d9de8efce8b9f986775cf6b70a82c236f/SafetyFirst/ResultModule.fs#L12-L20) and [LABS warning transformation](https://github.com/labs-lang/labs/blob/482cf8f22db96eab844cab5677eda6b9ad7b8680/Frontend/Outcome.fs#L20-L29).

For Result-returning checks, use `zapp` directly rather than converting through `Validation`. Here the trace shows that both independent check functions run, in deterministic left-to-right order.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus

let trace = ResizeArray<string> ()
let name text : Result<string, string list> =
    trace.Add "name"
    if text = "" then Error ["empty name"] else Ok text
let age value : Result<int, string list> =
    trace.Add "age"
    if value < 18 then Error ["under age"] else Ok value
let validate n a : Result<string * int, string list> = zapp {
    let! validName = name n
    and! validAge = age a
    return validName, validAge }
let both = validate "" 12
let bothTrace = trace |> Seq.toList
let nameOnly = validate "" 35
let ageOnly = validate "Ada" 12
let success = validate "Ada" 35
let mutable dependentCalls = 0
let dependent: Result<int, string list> =
    Error ["missing id"]
    |> Result.bind (fun id ->
        dependentCalls <- dependentCalls + 1
        Ok (id + 1))
printfn "validation=%A;%A" both success
// expect: validation=Error ["empty name"; "under age"];Ok ("Ada", 35)
printfn "single=%A;%A" nameOnly ageOnly
// expect: single=Error ["empty name"];Error ["under age"]
printfn "trace=%A" bothTrace
// expect: trace=["name"; "age"]
printfn "dependent=%A;calls=%d" dependent dependentCalls
// expect: dependent=Error ["missing id"];calls=0
```

The error type must support F#+'s append/`Plus` operation. Neither non-sequential applicatives nor `and!` alone guarantee parallel execution. The builder determines accumulation: `applicative` on Result keeps the first error, whereas `zapp` accumulates. Binding skips a later binder but cannot undo eagerly performed work. See [operation selection and existing Validation APIs](idioms-and-antipatterns.md#independent-result-validation) for a compiled comparison and pinned upstream sources. The external examples above motivate the diagnostic pattern; the Result-native implementation here uses FSharpPlus 1.9.1's non-sequential applicative.

### Constructor chains

Upstream F#+ code builds a value from independent checks with operators, one per line: the constructor, then `<!>`, then one apply per field. On `Result`, use `<.>` so errors accumulate; `<*>` would keep only the first. `curryN` turns a tupled `Create` into the curried function the chain needs.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus

type Signup = { Name: string; Email: string; Age: int } with
    static member Create (name, email, age) = { Name = name; Email = email; Age = age }
let mkName text : Result<string, string list> = if text = "" then Error ["empty name"] else Ok text
let mkEmail text : Result<string, string list> = if String.contains '@' text then Ok text else Error ["no @ in email"]
let mkAge age : Result<int, string list> = if age < 18 then Error ["under age"] else Ok age
let mkSignup name email age : Result<Signup, string list> =
    curryN Signup.Create
    <!> mkName name
    <.> mkEmail email
    <.> mkAge age
let firstOnly: Result<Signup, string list> =
    curryN Signup.Create
    <!> mkName ""
    <*> mkEmail "ada"
    <*> mkAge 12
printfn "chain=%A;%A" (mkSignup "" "ada" 12) firstOnly
// expect: chain=Error ["empty name"; "no @ in email"; "under age"];Error ["empty name"]
printfn "ok=%A" (map (fun s -> s.Name, s.Age) (mkSignup "Ada" "ada@example.org" 35))
// expect: ok=Ok ("Ada", 35)
```

When the checks already return `Validation`, its ordinary `<*>` accumulates. `<*` runs a check for its errors and keeps the left value, which suits smart constructors that test one input several ways.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
open FSharpPlus.Data

type Signup = { Name: string; Email: string; Age: int } with
    static member Create (name, email, age) = { Name = name; Email = email; Age = age }
let nonEmpty field text : Validation<string list, string> =
    if text = "" then Failure [field + " is empty"] else Success text
let hasAt text : Validation<string list, string> =
    if String.contains '@' text then Success text else Failure ["no @ in email"]
let hasDot text : Validation<string list, string> =
    if String.contains '.' text then Success text else Failure ["no . in email"]
let email text : Validation<string list, string> =
    result text <*
    hasAt  text <*
    hasDot text
let validAge age : Validation<string list, int> = if age < 18 then Failure ["under age"] else Success age
let signup name address age : Validation<string list, Signup> =
    curryN Signup.Create
    <!> nonEmpty "name" name
    <*> email address
    <*> validAge age
printfn "email=%A;%A" (email "ada") (email "ada@example.org")
// expect: email=Failure ["no @ in email"; "no . in email"];Success "ada@example.org"
printfn "signup=%A" (signup "" "ada" 12)
// expect: signup=Failure ["name is empty"; "no @ in email"; "no . in email"; "under age"]
```

These follow the layout of the [F#+ Validation page](https://github.com/fsprojects/FSharpPlus/blob/d1643d2ac9346c6b2e6bf52765a0648177b5ed9e/docsrc/content/type-validation.fsx#L125-L129) and its [smart constructors](https://github.com/fsprojects/FSharpPlus/blob/d1643d2ac9346c6b2e6bf52765a0648177b5ed9e/docsrc/content/type-validation.fsx#L194-L198), with `curryN` as in the [Free monad page](https://github.com/fsprojects/FSharpPlus/blob/d1643d2ac9346c6b2e6bf52765a0648177b5ed9e/docsrc/content/type-free.fsx#L97-L102). The `zapp` block above and these chains are interchangeable for Result; pick the one the surrounding code uses. Do not convert Result checks to `Validation` just to use `<*>`; see [existing Validation APIs](idioms-and-antipatterns.md#existing-validation-apis).

## Domain containers

Give domain containers the static members required for generic composition and traversal. The patterns appear in [SafetyFirst NonEmpty](https://github.com/ntwilson/SafetyFirst/blob/973fba6d9de8efce8b9f986775cf6b70a82c236f/SafetyFirst/NonEmpty.fs#L91-L115) and the [NBB Evented sample](https://github.com/osstotalsoft/nbb/blob/a1a5aacd24a66c4eaf96947252f484b9cb2766c0/test/UnitTests/Core/NBB.Core.Evented.FSharp.Tests/Sample.fs#L15-L47).

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus

type Evented<'T> = Evented of 'T * string list with
    static member Map (Evented (value, events), mapping) = Evented (mapping value, events)
    static member Return value = Evented (value, [])
    static member (<*>) (Evented (mapping, left), Evented (value, right)) = Evented (mapping value, left @ right)
    static member (>>=) (Evented (value, left), binder) =
        let (Evented (next, right)) = binder value
        Evented (next, left @ right)
let emit label value = Evented (value, [label])
let mapped: Evented<int> = map ((+) 1) (emit "loaded" 2)
let bound: Evented<int> = bind (fun x -> emit "saved" (x * 2)) (emit "loaded" 3)
let applied: Evented<int> = lift2 (+) (emit "left" 2) (emit "right" 4)
let traversed: Evented<int list> = traverse (fun x -> emit (string x) (x * 10)) [1; 2; 3]
printfn "evented=%A;%A;%A;%A" mapped bound applied traversed
// expect: evented=Evented (3, ["loaded"]);Evented (6, ["loaded"; "saved"]);Evented (6, ["left"; "right"]);Evented ([10; 20; 30], ["1"; "2"; "3"])
```

`Map` does not imply shape-preserving `Traverse`. Here a controlled constructor
and explicit traversal preserve the invariant under two outer effects.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus

type NonEmpty<'T> = One of 'T | More of 'T * NonEmpty<'T> with
    member x.Values =
        match x with
        | One v -> [v]
        | More (h, t) -> h :: t.Values
    static member Create values =
        let rec build = function
            | [x] -> Some (One x)
            | h :: t -> build t |> Option.map (fun tail -> More (h, tail))
            | [] -> None
        build values
    static member Map (source: NonEmpty<'T>, mapping: 'T -> 'U) : NonEmpty<'U> =
        let rec go = function
            | One v -> One (mapping v)
            | More (h, t) -> More (mapping h, go t)
        go source
    static member inline Traverse (source: NonEmpty<'T>, mapping: 'T -> '``Functor<'U>``) : '``Functor<NonEmpty<'U>>`` =
        let rec go item =
            match item with
            | One v -> map One (mapping v)
            | More (h, t) -> lift2 (fun head tail -> More (head, tail)) (mapping h) (go t)
        go source
let source = Option.get (NonEmpty.Create [1; 2; 3])
let values (x: NonEmpty<int>) = x.Values
let optionOk: NonEmpty<int> option = traverse (fun x -> Some (x * 2)) source
let optionBad: NonEmpty<int> option = traverse (fun x -> if x = 2 then None else Some x) source
let resultOk: Result<NonEmpty<int>, string> = traverse (fun x -> Ok (x + 1)) source
printfn "nonempty=%A;%A;%A" (Option.map values optionOk) optionBad (Result.map values resultOk)
// expect: nonempty=Some [2; 4; 6];None;Ok [2; 3; 4]
printfn "empty=%A" (NonEmpty.Create ([]: int list))
// expect: empty=None
```

In a compiled library, hide the union cases behind a signature so callers must use `Create`; the standalone script keeps it visible because inline SRTP members cannot reference a private representation. See
[exact static-member shapes](extending-types.md).
