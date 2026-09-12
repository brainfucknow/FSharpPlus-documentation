# Codecs and composed effects

## Contents

- [U7 Codec pattern](#u7-codec-pattern)
- [U8 Asynchronous optional routing](#u8-asynchronous-optional-routing)

## U7 Codec pattern

Fleece U7 is serialization-library implementation whose architecture uses
`ReaderT` decoders, `Const` encoders, generic operations, and non-empty errors.
Those ingredients alone do not establish every codec behavior. This distilled
codec-pattern adaptation uses an in-memory string map—not JSON—and chooses
first-error `Result` behavior.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
open FSharpPlus.Data

type Wire = Map<string,string>
type Person = { Name: string; Age: int }
type Decoder<'T> = ReaderT<Wire, Result<'T,string>>
type Encoder<'T> = 'T -> Const<Wire,unit>
let field key : Decoder<string> = ReaderT (fun wire -> match Map.tryFind key wire with | Some value -> Ok value | None -> Error ("missing " + key))
let ageField : Decoder<int> = monad {
    let! text = field "age"
    match System.Int32.TryParse text with
    | true, age -> return age
    | _ -> return! ReaderT.lift (Error "invalid age") }
let decode : Decoder<Person> = monad { let! n = field "name" in let! a = ageField in return { Name=n; Age=a } }
let encode : Encoder<Person> = fun p -> Const (Map ["name",p.Name; "age",string p.Age])
let runDecode wire = ReaderT.run decode wire
let runEncode value = Const.run (encode value)
let person = { Name="Mina"; Age=42 }
let valid = runDecode (runEncode person)
let missing = runDecode (Map ["age","42"])
let invalid = runDecode (Map ["name","Mina";"age","old"])
if valid <> Ok person || missing <> Error "missing name" || invalid <> Error "invalid age" || (runEncode person).Count <> 2 then failwith "codec"
printfn "decode=%A;%A;%A" valid missing invalid
// expect: decode=Ok { Name = "Mina"
printfn "encoded=%A;roundtrip=%b" (runEncode person |> Map.toList) (valid = Ok person)
// expect: encoded=[("age", "42"); ("name", "Mina")];roundtrip=true
```

The decoder and encoder are both exercised and round-tripped. This is neither a
Fleece API tutorial nor a compatible replacement. A plain pair of functions is
often clearer until reusable effect composition is needed.

## U8 Asynchronous optional routing

The FSharpPlus.AspNetCore U8 adapter is explicitly a simplified demonstration:
`OptionT<Async<_ option>>` combines async execution with optional matching. It
does not establish broad framework adoption. This in-memory adaptation removes
HTTP, sleeps, and network I/O.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
open FSharpPlus.Data

type Request = { Path: string }
type Handler = Request -> OptionT<Async<string option>>
let trace = ResizeArray<string>()
let route path response : Handler = fun request -> OptionT (async {
    trace.Add ("check:" + path)
    if request.Path = path then trace.Add ("run:" + path); return Some response
    else return None })
let choose (handlers: Handler list) : Handler = fun request ->
    handlers |> List.map (fun handler -> handler request) |> choice
let execute handler request = handler request |> OptionT.run |> Async.RunSynchronously
let selected = execute (choose [route "/first" "one"; route "/second" "two"]) {Path="/second"}
let selectedTrace = trace |> Seq.toList
trace.Clear()
let unmatched = execute (choose [route "/first" "one"; route "/second" "two"]) {Path="/none"}
let unmatchedTrace = trace |> Seq.toList
if selected <> Some "two" || selectedTrace <> ["check:/first";"check:/second";"run:/second"] || unmatched <> None || unmatchedTrace <> ["check:/first";"check:/second"] then failwith "routing"
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
