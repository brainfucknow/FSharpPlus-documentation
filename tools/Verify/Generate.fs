/// Snippet lint, the two generated verification targets, and the JSONL corpus export.
module Verify.Generate

open System
open System.IO
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open Verify.Core

// ---------------------------------------------------------------------------------------------
// Lint
// ---------------------------------------------------------------------------------------------

/// Structural rules that hold without a compiler, so they still apply where no SDK is available.
let lint (snippets: Snippet list) =
    let report = Report ()
    let seen = System.Collections.Generic.Dictionary<string, string> ()
    for s in snippets do
        let where = $"{s.Where} ({s.Name})"

        match seen.TryGetValue s.Module with
        | true, first -> report.Fail $"{where}: duplicate snippet name, already used at {first}"
        | _ -> seen[s.Module] <- where

        let text = String.concat "\n" s.Body
        if not (Regex.IsMatch (text, @"^\s*open\s+FSharpPlus", RegexOptions.Multiline)) then
            report.Fail
                $"{where}: no `open FSharpPlus...` line. Every snippet must carry its own opens - \
                  models copy the snippet, not the page."

        for c in s.Claims do
            // The plan's guardrail: the documented type must be a real annotation, so the compiler
            // checks it. Otherwise a snippet can compile while demonstrating a monomorphic
            // instantiation of a generic function and nothing notices.
            let binding =
                Regex.Match
                    (text,
                     $@"^\s*let\s+(?:mutable\s+)?{Regex.Escape c.Binding}\s*:\s*(?<ann>.+?)\s*=",
                     RegexOptions.Multiline)
            if not binding.Success then
                report.Fail
                    $"{where}: `// val {c.Binding} : {c.Type}` has no matching annotated binding. \
                      Write `let {c.Binding} : {c.Type} = ...` so the compiler checks the type."
            else
                // Bound out of the interpolation: a quoted indexer inside an interpolated string
                // is FS3373.
                let annotation = binding.Groups.["ann"].Value.Trim ()
                if normalise annotation <> normalise c.Type then
                    report.Fail
                        $"{where}: `// val {c.Binding} : {c.Type}` disagrees with the annotation \
                          `{annotation}`"

        if List.isEmpty s.Claims then
            report.Fail
                $"{where}: no `// val name : Type = value` claim, so nothing is asserted. \
                  Either add one or drop the `verify` tag."
    report

// ---------------------------------------------------------------------------------------------
// Generated targets
// ---------------------------------------------------------------------------------------------

let private fsString (s: string) =
    "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""

/// Emitted unindented. The project target nests these inside `let main _ =` and so must indent them;
/// the FSI script has them at top level and must NOT, or they are parsed as a continuation of the
/// preceding `module X =` block and every module reference resolves inside itself.
let private checkCalls (snippets: Snippet list) =
    [ for s in snippets do
        for c in s.Claims do
            yield
                sprintf "check %s %s (sprintf \"%%A\" %s.%s) %s"
                    (fsString s.Where) (fsString $"{s.Name}.{c.Binding}") s.Module c.Binding
                    (fsString c.Value) ]

/// Compile-and-execute target: one module per snippet plus a program that asserts every claim.
let generateProject (snippets: Snippet list) (out: string) (packageVersion: string) =
    Directory.CreateDirectory out |> ignore
    for f in Directory.EnumerateFiles (out, "*.fs") do File.Delete f

    let files =
        [ for s in snippets do
            let name = $"{s.Module}.fs"
            let content =
                [ $"// generated from {s.Where} - do not edit"
                  $"module {s.Module}"
                  "" ]
                @ s.Body
            File.WriteAllText (Path.Combine (out, name), String.concat "\n" content + "\n")
            yield name ]

    let checks = checkCalls snippets |> List.map (fun l -> "    " + l)
    let program =
        String.concat "\n"
            [ "// generated - do not edit"
              "module Program"
              ""
              "open System.Text.RegularExpressions"
              ""
              "let mutable failures = 0"
              "let mutable passes = 0"
              ""
              "/// FSI's `val x : T = v` rendering and sprintf \"%A\" agree for the shapes we document,"
              "/// but not on incidental whitespace, so compare normalised."
              "let private norm (s: string) = Regex.Replace(s.Trim(), @\"\\s+\", \" \")"
              ""
              "let check (where: string) (label: string) (actual: string) (expected: string) ="
              "    if norm actual = norm expected then"
              "        passes <- passes + 1"
              "    else"
              "        failures <- failures + 1"
              "        printfn \"FAIL %s\" label"
              "        printfn \"     declared at %s\" where"
              "        printfn \"     expected: %s\" (norm expected)"
              "        printfn \"     actual:   %s\" (norm actual)"
              ""
              "[<EntryPoint>]"
              "let main _ ="
              (if List.isEmpty checks then "    ()" else String.concat "\n" checks)
              "    printfn \"\""
              "    printfn \"%d value assertion(s): %d passed, %d failed\" (passes + failures) passes failures"
              "    if failures > 0 then 1 else 0"
              "" ]
    File.WriteAllText (Path.Combine (out, "Program.fs"), program)

    let compiles = files |> List.map (sprintf "    <Compile Include=\"%s\" />") |> String.concat "\n"
    let proj =
        String.concat "\n"
            [ "<Project Sdk=\"Microsoft.NET.Sdk\">"
              "  <!-- generated by tools/Verify - do not edit -->"
              "  <PropertyGroup>"
              "    <OutputType>Exe</OutputType>"
              "    <TargetFramework>net8.0</TargetFramework>"
              "    <!-- Compile against net8.0 (upstream's TFM) but allow the app to RUN on a newer"
              "         major runtime. Cloud containers may only ship a later SDK - e.g. Ubuntu 24.04's"
              "         dotnet-sdk-10.0, the only route available when the egress proxy blocks the usual"
              "         dotnet download hosts. Without this, the build succeeds and `dotnet run` dies"
              "         with \"Framework 'Microsoft.NETCore.App', version '8.0.0' not found\". No effect"
              "         where the net8.0 runtime is present. -->"
              "    <RollForward>LatestMajor</RollForward>"
              "    <!-- FS0064 = \"this construct is less generic than indicated\". The plan wants this as"
              "         an error: it is the warning that fires when a generic example silently degrades"
              "         to a monomorphic one. FS0044 = obsolete, so a doc snippet cannot use a renamed"
              "         API. -->"
              "    <WarningsAsErrors>FS0064;FS0044</WarningsAsErrors>"
              "    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>"
              "  </PropertyGroup>"
              "  <ItemGroup>"
              compiles
              "    <Compile Include=\"Program.fs\" />"
              "  </ItemGroup>"
              "  <ItemGroup>"
              $"    <PackageReference Include=\"FSharpPlus\" Version=\"{packageVersion}\" />"
              "  </ItemGroup>"
              "</Project>"
              "" ]
    File.WriteAllText (Path.Combine (out, "verify.fsproj"), proj)
    List.length files, List.length checks

/// Second target. Upstream PR #372 showed FSI and project compilation disagree on SRTP-heavy code,
/// and FSI is how a user actually pastes a snippet, so passing one target is not passing both.
/// Bodies are indented into `module <Name> =` blocks so bindings cannot collide across snippets.
let generateFsi (snippets: Snippet list) (out: string) (packageVersion: string) =
    Directory.CreateDirectory out |> ignore
    let header =
        [ "// generated by tools/Verify - do not edit"
          $"#r \"nuget: FSharpPlus, {packageVersion}\""
          ""
          "open System.Text.RegularExpressions"
          ""
          "let mutable failures = 0"
          "let mutable passes = 0"
          "let norm (s: string) = Regex.Replace(s.Trim(), @\"\\s+\", \" \")"
          ""
          "let check (where: string) (label: string) (actual: string) (expected: string) ="
          "    if norm actual = norm expected then passes <- passes + 1"
          "    else"
          "        failures <- failures + 1"
          "        printfn \"FAIL %s\" label"
          "        printfn \"     declared at %s\" where"
          "        printfn \"     expected: %s\" (norm expected)"
          "        printfn \"     actual:   %s\" (norm actual)"
          "" ]
    let modules =
        [ for s in snippets do
            yield $"// from {s.Where}"
            yield $"module {s.Module} ="
            for line in s.Body do
                yield (if line.Trim () = "" then "" else "    " + line)
            yield "" ]
    let footer =
        [ ""
          "printfn \"\""
          "printfn \"FSI: %d value assertion(s): %d passed, %d failed\" (passes + failures) passes failures"
          "if failures > 0 then exit 1" ]
    let path' = Path.Combine (out, "verify.fsx")
    File.WriteAllText (path', String.concat "\n" (header @ modules @ checkCalls snippets @ footer) + "\n")
    path'

// ---------------------------------------------------------------------------------------------
// Corpus
// ---------------------------------------------------------------------------------------------

let private corpusFile = path [ "corpus"; "verified-snippets.jsonl" ]

let private verifiedHow =
    "compiled as an <Compile> item against the released NuGet package and the documented values "
    + "asserted at runtime via sprintf \"%A\"; FS0064 and FS0044 escalated to errors"

/// One JSON object per line: signature, opens, code, expected type and expected value. Only
/// verify-tagged snippets are exported, so every record has been compiled and executed - which is
/// the whole point, versus scraped text that may not work.
let private corpusText (snippets: Snippet list) (pin: Pin) =
    let sb = StringBuilder ()
    for s in snippets |> List.sortBy (fun s -> s.Name) do
        use stream = new MemoryStream ()
        (
            use w = new Utf8JsonWriter (stream)
            w.WriteStartObject ()
            w.WriteStartArray "claims"
            for c in s.Claims do
                w.WriteStartObject ()
                w.WriteString ("binding", c.Binding)
                w.WriteString ("type", c.Type)
                w.WriteString ("value", c.Value)
                w.WriteEndObject ()
            w.WriteEndArray ()
            w.WriteString ("code", codeOf s.Body)
            w.WriteString ("full_snippet", (String.concat "\n" s.Body).Trim ())
            w.WriteString ("id", s.Name)
            w.WriteStartArray "opens"
            for o in opensOf s.Body do w.WriteStringValue o
            w.WriteEndArray ()
            w.WriteString ("package", "FSharpPlus")
            w.WriteString ("package_version", pin.PackageVersion)
            w.WriteString ("source", s.Where)
            w.WriteString ("upstream_sha", pin.Sha)
            w.WriteStartObject "verified"
            w.WriteBoolean ("compiled", true)
            w.WriteBoolean ("executed", true)
            w.WriteString ("how", verifiedHow)
            w.WriteEndObject ()
            w.WriteEndObject ()
        )
        sb.Append(Encoding.UTF8.GetString (stream.ToArray ())).Append '\n' |> ignore
    sb.ToString ()

let corpus (snippets: Snippet list) (check: bool) =
    let report = lint snippets
    if report.Count > 0 then
        eprintfn "refusing to export: %d lint problem(s)" report.Count
        for p in report.Problems do eprintfn "  - %s" p
        1
    else
        let text = corpusText snippets (readPin ())
        let records = List.length snippets
        let claims = snippets |> List.sumBy (fun s -> List.length s.Claims)
        if check then
            if not (File.Exists corpusFile) then
                printfn "FAIL: %s is missing - run `verify corpus`" (relative corpusFile)
                1
            elif File.ReadAllText corpusFile <> text then
                printfn "FAIL: %s is stale - run `verify corpus` and commit" (relative corpusFile)
                1
            else
                printfn "corpus up to date: %d records, %d verified claims" records claims
                0
        else
            Directory.CreateDirectory (Path.GetDirectoryName corpusFile) |> ignore
            File.WriteAllText (corpusFile, text)
            printfn "wrote %s: %d records, %d verified claims" (relative corpusFile) records claims
            0
