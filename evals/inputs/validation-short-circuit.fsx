#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus

let parseName (s: string) : Result<string, string list> =
    if s = "" then Error ["empty name"] else Ok s

let parseAge (s: string) : Result<int, string list> =
    match System.Int32.TryParse s with
    | true, n -> Ok n
    | _ -> Error ["bad age"]

let person name age : Result<string * int, string list> = monad' {
    let! n = parseName name
    let! a = parseAge age
    return n, a }

let mutable dependentCalls = 0
let dependent : Result<int, string list> =
    Error ["missing id"]
    |> Result.bind (fun id ->
        dependentCalls <- dependentCalls + 1
        Ok (id + 1))
printfn "errors=%A" (person "" "x")
printfn "nameOnly=%A" (person "" "35")
printfn "ageOnly=%A" (person "Ada" "x")
printfn "success=%A" (person "Ada" "35")
printfn "dependentCalls=%d" dependentCalls
