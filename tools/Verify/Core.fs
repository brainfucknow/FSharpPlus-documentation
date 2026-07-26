/// Shared plumbing: repo layout, the upstream pin, and markdown fence parsing.
module Verify.Core

open System
open System.IO
open System.Text.RegularExpressions
open System.Text.Json

// ---------------------------------------------------------------------------------------------
// Repo layout
// ---------------------------------------------------------------------------------------------

/// Walk up from the executing assembly until we find the repo root. The tool is run from CI, from
/// tools/verify.sh and by hand, so resolving relative to the binary beats trusting the cwd.
let repoRoot =
    let rec search (dir: DirectoryInfo) =
        if isNull dir then
            failwith "could not locate the repo root (no upstream.json found in any parent)"
        elif File.Exists (Path.Combine (dir.FullName, "upstream.json")) then dir.FullName
        else search dir.Parent
    search (DirectoryInfo AppContext.BaseDirectory)

let path parts = Path.Combine (Array.ofList (repoRoot :: parts))

/// Repo-relative, forward-slashed, so messages read the same on every platform.
let relative (full: string) =
    Path.GetRelativePath(repoRoot, full).Replace ('\\', '/')

// ---------------------------------------------------------------------------------------------
// The upstream pin
// ---------------------------------------------------------------------------------------------

type Pin =
    { Sha: string
      ShaDate: string
      Release: string
      PackageVersion: string }

let readPin () =
    use doc = JsonDocument.Parse (File.ReadAllText (path ["upstream.json"]))
    // Annotated because GetProperty is overloaded on string/ReadOnlySpan and inference cannot pick.
    let get (name: string) = doc.RootElement.GetProperty(name).GetString ()
    { Sha = get "sha"
      ShaDate = get "sha_date"
      Release = get "release"
      PackageVersion = get "package_version" }

// ---------------------------------------------------------------------------------------------
// Markdown
// ---------------------------------------------------------------------------------------------

/// Every markdown file in the repo, excluding generated and VCS directories.
let markdownFiles () =
    Directory.EnumerateFiles (repoRoot, "*.md", SearchOption.AllDirectories)
    |> Seq.filter (fun p ->
        let parts = (relative p).Split '/'
        not (Array.contains ".git" parts || Array.contains "build" parts))
    |> Seq.sort
    |> List.ofSeq

let private verifyFence = Regex (@"^```fsharp\s+verify(?<attrs>[^\n]*)$")

/// A 4+-backtick fence wraps markdown that *shows* snippet syntax rather than being a snippet.
/// Without skipping these, documenting the convention silently adds a snippet to the corpus - which
/// is exactly what happened once before AGENTS.md was written.
let private outerFence = Regex (@"^````+\s*\w*\s*$")

/// A ```fsharp verify block: where it came from, its attributes, and its body lines.
type Fence =
    { SourceFile: string
      /// 1-based line of the first body line, matching the citation convention used in prose.
      Line: int
      Attrs: Map<string, string>
      Body: string list }

/// Extract the verify-tagged fences from markdown text, ignoring anything inside an outer fence.
let fences (sourceFile: string) (text: string) =
    let lines = text.Replace("\r\n", "\n").Split '\n'
    let out = ResizeArray<Fence> ()
    let mutable i = 0
    let mutable inOuter = false
    while i < lines.Length do
        if outerFence.IsMatch lines[i] then
            inOuter <- not inOuter
            i <- i + 1
        elif inOuter then
            i <- i + 1
        else
            let m = verifyFence.Match lines[i]
            if not m.Success then
                i <- i + 1
            else
                let attrs =
                    m.Groups["attrs"].Value.Split ([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
                    |> Array.choose (fun kv ->
                        match kv.Split ('=', 2) with
                        | [| k; v |] -> Some (k, v)
                        | _ -> None)
                    |> Map.ofArray
                let start = i + 1
                let mutable j = start
                while j < lines.Length && lines[j].Trim () <> "```" do
                    j <- j + 1
                out.Add
                    { SourceFile = sourceFile
                      Line = start + 1
                      Attrs = attrs
                      Body = List.ofArray lines[start .. j - 1] }
                i <- j + 1
    List.ofSeq out

// ---------------------------------------------------------------------------------------------
// Snippets
// ---------------------------------------------------------------------------------------------

/// A documented value: `// val name : Type = value`.
type Claim = { Binding: string; Type: string; Value: string }

type Snippet =
    { Name: string
      SourceFile: string
      Line: int
      Body: string list
      Claims: Claim list }

    /// Where the snippet is declared, in the file:line form used throughout this repo.
    member s.Where = $"{s.SourceFile}:{s.Line}"

    /// F# module name for the generated file. Snippet names are lower_snake_case, so capitalising
    /// each part gives a valid identifier.
    member s.Module =
        let part (p: string) =
            string (Char.ToUpperInvariant p[0]) + p.Substring(1).ToLowerInvariant ()
        Regex.Split (s.Name, @"[^A-Za-z0-9]+")
        |> Array.filter (fun p -> p <> "")
        |> Array.map part
        |> String.concat ""
        |> sprintf "Verify%s"

let valLine =
    Regex (@"^\s*//\s*val\s+(?<name>[A-Za-z_][A-Za-z0-9_']*)\s*:\s*(?<type>[^=]+?)\s*=\s*(?<value>.+?)\s*$")

let private openLine = Regex (@"^\s*open\s+(?<ns>[\w.]+)", RegexOptions.Multiline)

/// Collapse whitespace, so comparisons ignore incidental formatting.
let normalise (s: string) = Regex.Replace (s.Trim (), @"\s+", " ")

let claimsOf (body: string list) =
    body
    |> List.choose (fun line ->
        let m = valLine.Match line
        if m.Success then
            Some
                { Binding = m.Groups["name"].Value
                  Type = m.Groups["type"].Value.Trim ()
                  Value = m.Groups["value"].Value.Trim () }
        else None)

let opensOf (body: string list) =
    openLine.Matches (String.concat "\n" body)
    |> Seq.map (fun m -> m.Groups["ns"].Value)
    |> List.ofSeq

/// Snippet body with the `// val` claim lines removed - what a consumer should imitate.
let codeOf (body: string list) =
    body
    |> List.filter (fun l -> not (Regex.IsMatch (l, @"^\s*//\s*val\s")))
    |> String.concat "\n"
    |> fun s -> s.Trim ()

/// Collect every verify-tagged snippet in the repo, ordered by name so output is deterministic.
let collect () =
    markdownFiles ()
    |> List.collect (fun file ->
        let rel = relative file
        fences rel (File.ReadAllText file)
        |> List.mapi (fun idx f ->
            let fallback = $"""{Path.GetFileNameWithoutExtension(file).Replace ('-', '_')}_{idx + 1}"""
            let name = f.Attrs.TryFind "name" |> Option.defaultValue fallback
            { Name = name
              SourceFile = f.SourceFile
              Line = f.Line
              Body = f.Body
              Claims = claimsOf f.Body }))

/// Only code inside verify blocks counts as coverage. Naming a function in prose is not documenting
/// it - and reference/generic-functions.md names every function it does NOT cover, so a plain
/// substring search over the page would score those as covered.
let verifiedSnippetCode (text: string) =
    fences "" text
    |> List.collect (fun f -> f.Body)
    |> List.filter (fun l -> not (Regex.IsMatch (l, @"^\s*//")))
    |> String.concat "\n"

// ---------------------------------------------------------------------------------------------
// Reporting
// ---------------------------------------------------------------------------------------------

/// Accumulates failures so a run reports everything wrong at once rather than the first problem.
type Report () =
    let problems = ResizeArray<string> ()
    member _.Fail (msg: string) = problems.Add msg
    member _.Problems = List.ofSeq problems
    member _.Count = problems.Count

    member r.Emit (warnOnly: bool) =
        if r.Count > 0 then
            printfn ""
            printfn "%d problem(s):" r.Count
            printfn ""
            for p in r.Problems do printfn "  - %s" p
            if warnOnly then 0 else 1
        else 0
