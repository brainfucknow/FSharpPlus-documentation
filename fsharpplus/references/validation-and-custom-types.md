# Validation and custom types

## Contents

- [U5 Diagnostic semantics](#u5-diagnostic-semantics)
- [U6 Domain containers](#u6-domain-containers)

## U5 Diagnostic semantics

SafetyFirst U5 is library implementation whose *custom* `MergeSources` combines
errors with F#+ `++`; LABS combines warnings with `plus`. Neither makes every
`Result` builder accumulating. The type, operation, and builder jointly decide
semantics. The adaptation uses F#+ `Validation` for independent checks and
ordinary `Result.bind` for a dependent check.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
open FSharpPlus.Data

let name text : Validation<string list, string> = if text = "" then Failure ["empty name"] else Success text
let age value : Validation<string list, int> = if value < 18 then Failure ["under age"] else Success value
let validate n a = lift2 tuple2 (name n) (age a)
let both = validate "" 12 |> Validation.toResult
let success = validate "Ada" 35 |> Validation.toResult
let mutable dependentCalls = 0
let dependent : Result<int,string> =
    Error "missing id" |> Result.bind (fun id -> dependentCalls <- dependentCalls + 1; Ok (id + 1))
if both <> Error ["empty name"; "under age"] || success <> Ok ("Ada",35) || dependentCalls <> 0 then failwith "validation"
printfn "validation=%A;%A" both success
// expect: validation=Error ["empty name"; "under age"];Ok ("Ada", 35)
printfn "dependent=%A;calls=%d" dependent dependentCalls
// expect: dependent=Error "missing id";calls=0
```

Applicative construction evaluates its independent inputs; `and!` alone
guarantees neither accumulation nor parallel execution. Binding skips a later
binder but cannot undo eagerly performed work. See
[Validation versus Result](idioms-and-antipatterns.md#validation-versus-result).

## U6 Domain containers

SafetyFirst U6 implements its own `NonEmpty`, distinct from F#+'s
`NonEmptyList`. NBB U6 is a unit-test sample demonstrating Evented
interoperability, not production-service adoption. These two types are original
minimal adaptations and are not replacements for either downstream type.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus

type Evented<'T> = Evented of 'T * string list with
    static member Map (Evented (value, events), mapping) = Evented (mapping value, events)
    static member Return value = Evented (value, [])
    static member (<*>) (Evented (mapping, left), Evented (value, right)) = Evented (mapping value, left @ right)
    static member (>>=) (Evented (value, left), binder) = let (Evented (next, right)) = binder value in Evented (next, left @ right)
let emit label value = Evented (value, [label])
let mapped: Evented<int> = map ((+) 1) (emit "loaded" 2)
let bound: Evented<int> = bind (fun x -> emit "saved" (x * 2)) (emit "loaded" 3)
let applied: Evented<int> = lift2 (+) (emit "left" 2) (emit "right" 4)
let traversed: Evented<int list> = traverse (fun x -> emit (string x) (x * 10)) [1;2;3]
if mapped <> Evented (3,["loaded"]) || bound <> Evented (6,["loaded";"saved"]) || applied <> Evented (6,["left";"right"]) || traversed <> Evented ([10;20;30],["1";"2";"3"]) then failwith "event order"
printfn "evented=%A;%A;%A;%A" mapped bound applied traversed
// expect: evented=Evented (3, ["loaded"]);Evented (6, ["loaded"; "saved"]);Evented (6, ["left"; "right"]);Evented ([10; 20; 30], ["1"; "2"; "3"])
```

`Map` does not imply shape-preserving `Traverse`. Here a controlled constructor
and explicit traversal preserve the invariant under two outer effects.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus

type NonEmpty<'T> = One of 'T | More of 'T * NonEmpty<'T> with
    member x.Values = match x with | One v -> [v] | More (h,t) -> h::t.Values
    static member Create values =
        let rec build = function | [x] -> Some (One x) | h::t -> build t |> Option.map (fun tail -> More (h,tail)) | [] -> None
        build values
    static member Map (source: NonEmpty<'T>, mapping: 'T -> 'U) : NonEmpty<'U> =
        let rec go = function | One v -> One (mapping v) | More (h,t) -> More (mapping h, go t)
        go source
    static member inline Traverse (source: NonEmpty<'T>, mapping: 'T -> '``Functor<'U>``) : '``Functor<NonEmpty<'U>>`` =
        let rec go item =
            match item with
            | One v -> map One (mapping v)
            | More (h,t) -> lift2 (fun head tail -> More (head,tail)) (mapping h) (go t)
        go source
let source = NonEmpty.Create [1;2;3] |> Option.get
let optionOk: NonEmpty<int> option = traverse (fun x -> Some (x * 2)) source
let optionBad: NonEmpty<int> option = traverse (fun x -> if x = 2 then None else Some x) source
let resultOk: Result<NonEmpty<int>,string> = traverse (fun x -> Ok (x + 1)) source
if NonEmpty.Create [] <> None || optionOk |> Option.map (fun x -> x.Values) <> Some [2;4;6] || optionBad <> None || resultOk |> Result.map (fun x -> x.Values) <> Ok [2;3;4] then failwith "nonempty"
printfn "nonempty=%A;%A;%A" (optionOk |> Option.map (fun x -> x.Values)) optionBad (resultOk |> Result.map (fun x -> x.Values))
// expect: nonempty=Some [2; 4; 6];None;Ok [2; 3; 4]
printfn "empty=%A" (NonEmpty.Create ([]: int list))
// expect: empty=None
```

In a compiled library, hide the union cases behind a signature so callers must use `Create`; the standalone script keeps it visible because inline SRTP members cannot reference a private representation. See
[exact static-member shapes](extending-types.md).
