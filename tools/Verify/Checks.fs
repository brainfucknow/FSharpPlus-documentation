/// The claim checks: citations, API presence/absence, and the coverage gap.
module Verify.Checks

open System.IO
open System.Text.RegularExpressions
open Verify.Core
open Verify.Data

// ---------------------------------------------------------------------------------------------
// Shared
// ---------------------------------------------------------------------------------------------

let private fileMap = Map.ofList citationMap

let private ensureUpstream (upstream: string) =
    if not (Directory.Exists (Path.Combine (upstream, "src", "FSharpPlus"))) then
        eprintfn "error: %s does not look like an FSharpPlus checkout" upstream
        false
    else true

/// Cache upstream files across the citation and assertion passes - the two share most of them.
let private lineCache = System.Collections.Generic.Dictionary<string, string[] option> ()

let private linesOf (upstream: string) (relPath: string) =
    match lineCache.TryGetValue relPath with
    | true, v -> v
    | _ ->
        let full = Path.Combine (upstream, relPath)
        let v = if File.Exists full then Some (File.ReadAllLines full) else None
        lineCache[relPath] <- v
        v

// ---------------------------------------------------------------------------------------------
// Citations
// ---------------------------------------------------------------------------------------------

/// Matches `Builders.fs:257` and `Builders.fs:271-287`, but not `1.9.1` or `net8.0`.
let private citation =
    Regex (@"\b(?<file>[A-Za-z0-9_][A-Za-z0-9_.\-]*\.(?:fs|fsx|fsproj|md|sln|proj|yml|txt)):(?<from>\d+)(?:-(?<to>\d+))?\b")

/// Citations naming this repo's own files are self-references, not claims about upstream.
let private selfFiles = set [ "FINDINGS.md"; "README.md"; "CHEATSHEET.md" ]

let private checkReachability (upstream: string) (report: Report) =
    let mutable checked_ = 0
    for md in markdownFiles () do
        let rel = relative md
        for m in citation.Matches (File.ReadAllText md) do
            let name = m.Groups["file"].Value
            if not (selfFiles.Contains name) then
                let first = int m.Groups["from"].Value
                let last = if m.Groups["to"].Success then int m.Groups["to"].Value else first
                match fileMap.TryFind name with
                | None ->
                    report.Fail
                        $"{rel}: citation `{m.Value}` names a file absent from Data.citationMap - \
                          add it or fix the typo"
                | Some target ->
                    match linesOf upstream target with
                    | None -> report.Fail $"{rel}: `{m.Value}` -> {target} does not exist upstream"
                    | Some lines ->
                        checked_ <- checked_ + 1
                        if first < 1 || last > lines.Length then
                            report.Fail
                                $"{rel}: `{m.Value}` out of range ({target} has {lines.Length} lines)"
                        elif lines[first - 1 .. last - 1] |> String.concat "" |> _.Trim() |> (=) "" then
                            report.Fail $"{rel}: `{m.Value}` points at blank line(s) in {target}"
    checked_

let private checkAssertions (upstream: string) (report: Report) =
    for a in assertions do
        match fileMap.TryFind a.File with
        | None -> report.Fail $"assertion for {a.File}:{a.Line} - file not in Data.citationMap"
        | Some target ->
            match linesOf upstream target with
            | None -> report.Fail $"{a.File}:{a.Line} - {target} missing upstream"
            | Some lines ->
                if a.Line < 1 || a.Line > lines.Length then
                    report.Fail
                        $"{a.File}:{a.Line} out of range ({lines.Length} lines) - claim: {a.Claim}"
                elif not (lines[a.Line - 1].Contains a.Contains) then
                    let actual = lines[a.Line - 1].Trim ()
                    let shown = if actual.Length > 100 then actual.Substring (0, 100) else actual
                    report.Fail
                        $"{a.File}:{a.Line} no longer contains '{a.Contains}'\n      \
                          claim broken: {a.Claim}\n      line now says: {shown}"
    List.length assertions

let citations (upstream: string) (warnOnly: bool) =
    if not (ensureUpstream upstream) then 2
    else
        let pin = readPin ()
        printfn "upstream:  %s" upstream
        printfn "pinned at: %s (%s, release %s)" (pin.Sha.Substring (0, 12)) pin.ShaDate pin.Release
        printfn ""
        let report = Report ()
        let nReach = checkReachability upstream report
        let reachFailures = report.Count
        let nAssert = checkAssertions upstream report
        printfn "reachability: %d citations checked, %d failed" nReach reachFailures
        printfn "assertions:   %d assertions checked, %d failed" nAssert (report.Count - reachFailures)
        let code = report.Emit warnOnly
        if report.Count > 0 then
            printfn ""
            printfn "If upstream moved intentionally, update the citation, the assertion, and the"
            printfn "prose it backs - then bump 'sha' in upstream.json."
        else
            printfn ""
            printfn "All citations resolve and all asserted content is intact."
        code

// ---------------------------------------------------------------------------------------------
// API presence / absence
// ---------------------------------------------------------------------------------------------

/// Definition sites only - `let name`, `let inline name`, `let (op)`. A name appearing in a comment
/// or an XML doc string does not count as existing.
let private definitionSites (sources: (string * string[]) list) (name: string) =
    let esc = Regex.Escape name
    let pattern =
        Regex ($@"^\s*let\s+(?:inline\s+)?(?:rec\s+)?{esc}\b|^\s*let\s+(?:inline\s+)?\(\s*{esc}\s*\)")
    [ for path, lines in sources do
        for i in 0 .. lines.Length - 1 do
            if pattern.IsMatch lines[i] then yield path, i + 1 ]

let absence (upstream: string) (warnOnly: bool) =
    if not (ensureUpstream upstream) then 2
    else
        let root = Path.Combine (upstream, "src", "FSharpPlus")
        let sources =
            Directory.EnumerateFiles (root, "*.fs", SearchOption.AllDirectories)
            |> Seq.sort
            |> Seq.map (fun p ->
                Path.GetRelativePath(upstream, p).Replace ('\\', '/'), File.ReadAllLines p)
            |> List.ofSeq
        printfn "scanned %d source files under src/FSharpPlus" (List.length sources)
        printfn ""
        let report = Report ()

        for c in absent do
            match definitionSites sources c.Name with
            | [] -> ()
            | hits ->
                let where = hits |> List.truncate 3 |> List.map (fun (p, n) -> $"{p}:{n}") |> String.concat "; "
                report.Fail
                    $"ABSENCE BROKEN: `{c.Name}` is now DEFINED at {where}\n      \
                      disambiguation.md says it does not exist (use {c.Instead} instead).\n      \
                      Remove it from the absent list and from the prose."

        for c in present do
            if List.isEmpty (definitionSites sources c.Name) then
                report.Fail
                    $"PRESENCE BROKEN: `{c.Name}` has no definition site\n      \
                      we claim it exists: {c.Claim}"

        for c in qualifiedOnly do
            match definitionSites sources c.Name with
            | [] -> report.Fail $"`{c.Name}` has no definition at all - claim was: {c.Claim}"
            | hits ->
                match hits |> List.filter (fun (p, _) -> p <> c.ExpectedFile) with
                | [] -> ()
                | unexpected ->
                    let where = unexpected |> List.map (fun (p, n) -> $"{p}:{n}") |> String.concat "; "
                    report.Fail
                        $"QUALIFIED-ONLY BROKEN: `{c.Name}` is now defined outside \
                          {c.ExpectedFile}: {where}\n      claim was: {c.Claim}"

        let total = List.length absent + List.length present + List.length qualifiedOnly
        printfn "%d absence claims, %d presence claims, %d qualified-only claims  (%d total)"
            (List.length absent) (List.length present) (List.length qualifiedOnly) total
        printfn "%d failed" report.Count
        let code = report.Emit warnOnly
        if report.Count = 0 then
            printfn ""
            printfn "All API presence/absence claims hold."
        code

// ---------------------------------------------------------------------------------------------
// Coverage gap
// ---------------------------------------------------------------------------------------------

/// `let inline foo` / `let foo` at module-member indentation. Operators are excluded - they are
/// covered by reference/operators.md rather than by this measure.
let private definition = Regex @"^    let (?:inline )?(?<name>\w[\w']*)\b"

let gap (upstream: string) (coveredBy: string list) (maxGap: int option) =
    if not (ensureUpstream upstream) then 2
    else
        let operators = Path.Combine (upstream, "src", "FSharpPlus", "Operators.fs")
        let names =
            File.ReadAllLines operators
            |> Array.indexed
            |> Array.choose (fun (i, line) ->
                let m = definition.Match line
                if m.Success then Some (m.Groups["name"].Value, i + 1) else None)
            |> Array.fold (fun acc (n, l) -> if Map.containsKey n acc then acc else Map.add n l acc) Map.empty

        let docs =
            Directory.EnumerateFiles (Path.Combine (upstream, "docsrc", "content"), "*.fsx")
            |> Seq.sort
            |> Seq.map File.ReadAllText
            |> String.concat "\n"

        let mentioned (text: string) (name: string) =
            Regex.IsMatch (text, $@"\b{Regex.Escape name}\b")

        let gapNames = names |> Map.toList |> List.map fst |> List.filter (mentioned docs >> not) |> List.sort

        printfn "public named functions in the AutoOpen Operators module: %d" names.Count
        printfn "with zero mentions anywhere in upstream docsrc/content:   %d  (%d%%)"
            (List.length gapNames) (100 * List.length gapNames / max 1 names.Count)

        if List.isEmpty coveredBy then
            printfn ""
            for n in gapNames do printfn "  - %s  (Operators.fs:%d)" n names[n]
        else
            let ours =
                coveredBy
                |> List.map (fun rel -> path [ rel ])
                |> List.filter File.Exists
                |> List.map (File.ReadAllText >> verifiedSnippetCode)
                |> String.concat "\n"
            let covered = gapNames |> List.filter (mentioned ours)
            printfn "of those, now carrying a verified example in this repo:    %d" (List.length covered)
            printfn ""
            printfn "covered here:"
            for n in covered do printfn "  + %s  (Operators.fs:%d)" n names[n]
            printfn ""
            printfn "still uncovered:"
            for n in gapNames do
                if not (List.contains n covered) then printfn "  - %s  (Operators.fs:%d)" n names[n]

        match maxGap with
        | Some limit when List.length gapNames > limit ->
            printfn ""
            printfn "FAIL: upstream gap %d exceeds baseline %d - new public functions shipped without docs."
                (List.length gapNames) limit
            1
        | _ -> 0
