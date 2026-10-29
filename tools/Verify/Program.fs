/// Entry point: one tool with subcommands, replacing what used to be five separate scripts.
module Verify.Program

open Verify.Core

let private usage = """verify - check every claim this repo makes

USAGE
    verify <command> [options]

COMMANDS
    citations   --upstream <path> [--warn-only]
                Every file:line citation resolves AND still contains the substring we claim.

    absence     --upstream <path> [--warn-only]
                Every API presence/absence claim still holds.

    gap         --upstream <path> [--covered-by <md>]... [--max-gap <n>]
                Which public functions have no example anywhere in the upstream docs.

    snippets    [--out <dir>] [--lint-only] [--fsi]
                Lint the verify-tagged snippets and generate the verification targets.

    corpus      [--check]
                Export corpus/verified-snippets.jsonl, or verify the committed copy is current.

    claims      --upstream <path> [--warn-only]
                citations + absence + snippet lint + corpus check, in one pass.

NOTES
    --warn-only reports problems but exits 0. Used by the non-blocking drift job, where citation
    line numbers are EXPECTED to move once upstream advances past the pin.
"""

let private arg (name: string) (args: string list) =
    let rec go = function
        | k :: v :: _ when k = name -> Some v
        | _ :: rest -> go rest
        | [] -> None
    go args

let private argAll (name: string) (args: string list) =
    let rec go acc = function
        | k :: v :: rest when k = name -> go (v :: acc) rest
        | _ :: rest -> go acc rest
        | [] -> List.rev acc
    go [] args

let private flag (name: string) (args: string list) = List.contains name args

let private requireUpstream args =
    match arg "--upstream" args with
    | Some p -> Ok (System.IO.Path.GetFullPath p)
    | None ->
        eprintfn "error: --upstream <path> is required"
        Error 2

[<EntryPoint>]
let main argv =
    let args = List.ofArray argv
    match args with
    | [] | "--help" :: _ | "-h" :: _ ->
        printfn "%s" usage
        0

    | "citations" :: rest ->
        match requireUpstream rest with
        | Error c -> c
        | Ok upstream -> Checks.citations upstream (flag "--warn-only" rest)

    | "absence" :: rest ->
        match requireUpstream rest with
        | Error c -> c
        | Ok upstream -> Checks.absence upstream (flag "--warn-only" rest)

    | "gap" :: rest ->
        match requireUpstream rest with
        | Error c -> c
        | Ok upstream ->
            let maxGap = arg "--max-gap" rest |> Option.map int
            Checks.gap upstream (argAll "--covered-by" rest) maxGap

    | "snippets" :: rest ->
        let snippets = collect ()
        let claims = snippets |> List.sumBy (fun s -> List.length s.Claims)
        printfn "found %d verifiable snippet(s)" (List.length snippets)
        printfn "%d value claim(s)" claims
        let report = Generate.lint snippets
        printfn "lint: %d problem(s)" report.Count
        if report.Count > 0 then
            report.Emit false
        elif flag "--lint-only" rest then
            printfn ""
            printfn "lint clean (--lint-only, nothing generated)"
            0
        else
            let pin = readPin ()
            let out = arg "--out" rest |> Option.defaultValue "build/verify"
            let full = path [ out ]
            let files, checks = Generate.generateProject snippets full pin.PackageVersion
            printfn ""
            printfn "generated %d module(s) and %d check(s) into %s" files checks (relative full)
            printfn "against FSharpPlus %s from NuGet" pin.PackageVersion
            if flag "--fsi" rest then
                let p = Generate.generateFsi snippets full pin.PackageVersion
                printfn "also wrote %s for the dotnet fsi target" (relative p)
            0

    | "corpus" :: rest ->
        Generate.corpus (collect ()) (flag "--check" rest)

    // The claim checks that need no compiler, in the order a reader would want them.
    | "claims" :: rest ->
        match requireUpstream rest with
        | Error c -> c
        | Ok upstream ->
            let warnOnly = flag "--warn-only" rest
            let mutable code = 0
            printfn "==> citations"
            code <- max code (Checks.citations upstream warnOnly)
            printfn ""
            printfn "==> API presence/absence"
            code <- max code (Checks.absence upstream warnOnly)
            printfn ""
            printfn "==> snippet lint"
            let snippets = collect ()
            printfn "found %d verifiable snippet(s)" (List.length snippets)
            printfn "%d value claim(s)" (snippets |> List.sumBy (fun s -> List.length s.Claims))
            let report = Generate.lint snippets
            printfn "lint: %d problem(s)" report.Count
            code <- max code (report.Emit warnOnly)
            printfn ""
            printfn "==> exported corpus freshness"
            code <- max code (Generate.corpus snippets true)
            code

    | cmd :: _ ->
        eprintfn "error: unknown command '%s'" cmd
        eprintfn ""
        eprintfn "%s" usage
        2
