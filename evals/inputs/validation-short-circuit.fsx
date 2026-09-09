#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
open FSharpPlus.Data

let parseName (s: string) : Validation<string list, string> =
    if s = "" then Failure ["empty name"] else Success s

let parseAge (s: string) : Validation<string list, int> =
    match System.Int32.TryParse s with
    | true, n -> Success n
    | _ -> Failure ["bad age"]

let person = monad {
    let! n = parseName ""
    let! a = parseAge "x"
    return (n, a) }

printfn "%A" person
