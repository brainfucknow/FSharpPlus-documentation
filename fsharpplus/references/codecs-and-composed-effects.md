# Codecs and composed effects

## Codec pattern

Compose an in-memory decoder and encoder from reusable effects. The pattern appears in [Fleece's Encoder, Decoder, and Codec definitions](https://github.com/fsprojects/Fleece/blob/9e1adf600f2ac01ade12fdb87e64c5eb3c32c296/src/Fleece/Fleece.fs#L113-L123).

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
open FSharpPlus.Data

type Wire = Map<string,string>
type Person = { Name: string; Age: int }
type Decoder<'T> = ReaderT<Wire, Result<'T,string>>
type Encoder<'T> = 'T -> Const<Wire,unit>
let field key : Decoder<string> =
    ReaderT (fun wire ->
        match Map.tryFind key wire with
        | Some value -> Ok value
        | None -> Error ("missing " + key))
let ageField : Decoder<int> = monad {
    let! text = field "age"
    match System.Int32.TryParse text with
    | true, age -> return age
    | _ -> return! ReaderT.lift (Error "invalid age") }
let decode : Decoder<Person> = monad {
    let! name = field "name"
    let! age = ageField
    return { Name = name; Age = age } }
let encode : Encoder<Person> = fun p -> Const (Map ["name", p.Name; "age", string p.Age])
let runDecode wire = ReaderT.run decode wire
let runEncode value = Const.run (encode value)
let person = { Name = "Mina"; Age = 42 }
let valid = runDecode (runEncode person)
let missing = runDecode (Map ["age","42"])
let invalid = runDecode (Map ["name", "Mina"; "age", "old"])
printfn "decode=%A;%A;%A;%A" (valid |> Result.map (fun p -> p.Name, p.Age)) missing invalid (runEncode person).Count
// expect: decode=Ok ("Mina", 42);Error "missing name";Error "invalid age";2
printfn "encoded=%A;roundtrip=%b" (runEncode person |> Map.toList) (valid = Ok person)
// expect: encoded=[("age", "42"); ("name", "Mina")];roundtrip=true
```

The decoder and encoder are both exercised and round-tripped. This is neither a
Fleece API tutorial nor a compatible replacement. A plain pair of functions is
often clearer until reusable effect composition is needed.

## Asynchronous optional routing

Route asynchronous optional handlers with ordered fallback and an explicit execution boundary. The pattern appears in the [FSharpPlus.AspNetCore adapter](https://github.com/fsprojects/FSharpPlus.AspNetCore/blob/04b39a9b370871c853fd46a02e9fcb5a01964e04/src/FSharpPlus.AspNetCore.Suave/Library.fs#L14-L37).

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
open FSharpPlus.Data

type Request = { Path: string }
type Handler = Request -> OptionT<Async<string option>>
let trace = ResizeArray<string>()
let route path response : Handler = fun request -> OptionT (async {
    trace.Add ("check:" + path)
    if request.Path = path then
        trace.Add ("run:" + path)
        return Some response
    else
        return None })
let choose (handlers: Handler list) : Handler = fun request ->
    handlers |> List.map (fun handler -> handler request) |> choice
let execute handler request = handler request |> OptionT.run |> Async.RunSynchronously
let selected = execute (choose [route "/first" "one"; route "/second" "two"]) { Path = "/second" }
let selectedTrace = trace |> Seq.toList
trace.Clear()
let unmatched = execute (choose [route "/first" "one"; route "/second" "two"]) { Path = "/none" }
let unmatchedTrace = trace |> Seq.toList
printfn "routing=%A;%A" selected unmatched
// expect: routing=Some "two";None
printfn "traces=%A;%A" selectedTrace unmatchedTrace
// expect: traces=["check:/first"; "check:/second"; "run:/second"];["check:/first"; "check:/second"]
```

Construction does not run the `async`; `OptionT.run` plus
`Async.RunSynchronously` is the explicit boundary. A rejected branch never logs
`run`, fallback executes once, and all-unmatched is `None`. For one or two
routes, a concrete `Async<option<_>>` function may be cheaper and clearer. See
[transformers](computation-expressions.md#transformers).
