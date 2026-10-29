/// Claim data for the verification tool.
///
/// This lives in F# source rather than in JSON on purpose: the shapes are checked by the compiler,
/// a typo in a field name is a build error instead of a runtime surprise, and there is no parser to
/// keep in step with the data. It is the same argument the rest of this repo makes for verified
/// examples over prose.
module Verify.Data

/// Maps the short filename used in a citation to its path in the upstream repo. A citation naming
/// a file absent from this map is a hard error - that stops a typo passing as unverifiable.
let citationMap : (string * string) list =
    [
      "Builders.fs",                     "src/FSharpPlus/Builders.fs"
      "Operators.fs",                    "src/FSharpPlus/Operators.fs"
      "Lens.fs",                         "src/FSharpPlus/Lens.fs"
      "Parsing.fs",                      "src/FSharpPlus/Parsing.fs"
      "Memoization.fs",                  "src/FSharpPlus/Memoization.fs"
      "Internals.fs",                    "src/FSharpPlus/Internals.fs"
      "String.fs",                       "src/FSharpPlus/Extensions/String.fs"
      "Option.fs",                       "src/FSharpPlus/Extensions/Option.fs"
      "Generic.fs",                      "src/FSharpPlus/Math/Generic.fs"
      "Applicative.fs",                  "src/FSharpPlus/Math/Applicative.fs"
      "Seq.fs",                          "src/FSharpPlus/Data/Seq.fs"
      "ZipApplicative.fs",               "src/FSharpPlus/Control/ZipApplicative.fs"
      "Error.fs",                        "src/FSharpPlus/Data/Error.fs"
      "Validation.fs",                   "src/FSharpPlus/Data/Validation.fs"
      "Monoids.fs",                      "src/FSharpPlus/Data/Monoids.fs"
      "Program.fs",                      "src/FSharpPlus.Docs/Program.fs"
      "FSharpPlus.Docs.fsproj",          "src/FSharpPlus.Docs/FSharpPlus.Docs.fsproj"
      "RELEASE_NOTES.md",                "RELEASE_NOTES.md"
      "FSharpPlus.sln",                  "FSharpPlus.sln"
      "build.proj",                      "build.proj"
      "Directory.Build.props",           "Directory.Build.props"
      "dotnetcore.yml",                  ".github/workflows/dotnetcore.yml"
      "computation-expressions.fsx",     "docsrc/content/computation-expressions.fsx"
      "lens.fsx",                        "docsrc/content/lens.fsx"
      "tutorial.fsx",                    "docsrc/content/tutorial.fsx"
      "index.fsx",                       "docsrc/content/index.fsx"
      "abstractions.fsx",                "docsrc/content/abstractions.fsx"
      "types.fsx",                       "docsrc/content/types.fsx"
      "abstraction-zipapplicative.fsx",  "docsrc/content/abstraction-zipapplicative.fsx"
      "applicative-functors.fsx",        "docsrc/content/applicative-functors.fsx"
      "numerics.fsx",                    "docsrc/content/numerics.fsx"
    ]

/// "At this file:line in the pinned upstream, this substring is present."
///
/// A checker that only proves a line EXISTS is weak - it passes even when the line now says
/// something unrelated. These pin the meaning. `Claim` is prose for the failure message, so a break
/// reports what became untrue rather than just a line number.
type Assertion = { File: string; Line: int; Contains: string; Claim: string }

let assertions : Assertion list =
    [
      { File = "Builders.fs"; Line = 344; Contains = "let monad<"; Claim = "monad -> MonadFxBuilder" }
      { File = "Builders.fs"; Line = 344; Contains = "MonadFxBuilder"; Claim = "monad is the Fx (lazy) builder" }
      { File = "Builders.fs"; Line = 347; Contains = "let monad'<"; Claim = "monad' exists" }
      { File = "Builders.fs"; Line = 347; Contains = "MonadFxStrictBuilder"; Claim = "monad' -> MonadFxStrictBuilder (strict)" }
      { File = "Builders.fs"; Line = 244; Contains = "_.strict"; Claim = "monad.strict exists on MonadFxBuilder" }
      { File = "Builders.fs"; Line = 244; Contains = "MonadFxStrictBuilder"; Claim = "monad.strict == monad'" }
      { File = "Builders.fs"; Line = 247; Contains = "_.plus"; Claim = "monad.plus exists" }
      { File = "Builders.fs"; Line = 247; Contains = "MonadPlusBuilder"; Claim = "monad.plus is the lazy additive builder" }
      { File = "Builders.fs"; Line = 250; Contains = "_.plus'"; Claim = "monad.plus' exists" }
      { File = "Builders.fs"; Line = 250; Contains = "MonadPlusStrictBuilder"; Claim = "monad.plus' is the strict additive builder" }
      { File = "Builders.fs"; Line = 253; Contains = "this.fx    = this"; Claim = "monad.fx returns this, so monad.fx == monad" }
      { File = "Builders.fs"; Line = 256; Contains = "_.fx'"; Claim = "monad.fx' exists" }
      { File = "Builders.fs"; Line = 256; Contains = "MonadFxStrictBuilder"; Claim = "monad.fx' == monad'" }
      { File = "Builders.fs"; Line = 220; Contains = "_.strict"; Claim = "MonadPlusBuilder ALSO has .strict - the fifth spelling the plan missed" }
      { File = "Builders.fs"; Line = 220; Contains = "MonadPlusStrictBuilder"; Claim = "monad.plus.strict == monad.plus'" }
      { File = "Builders.fs"; Line = 186; Contains = "type MonadPlusStrictBuilder"; Claim = "MonadPlusStrictBuilder is a distinct builder type" }
      { File = "Builders.fs"; Line = 201; Contains = "type MonadFxStrictBuilder"; Claim = "MonadFxStrictBuilder is a distinct builder type" }
      { File = "Builders.fs"; Line = 217; Contains = "type MonadPlusBuilder"; Claim = "MonadPlusBuilder is a distinct builder type" }
      { File = "Builders.fs"; Line = 242; Contains = "type MonadFxBuilder"; Claim = "MonadFxBuilder is a distinct builder type" }
      { File = "Builders.fs"; Line = 350; Contains = "let applicative<"; Claim = "applicative CE (sequential, 1 layer)" }
      { File = "Builders.fs"; Line = 353; Contains = "let applicative2<"; Claim = "applicative2 CE (sequential, 2 layers)" }
      { File = "Builders.fs"; Line = 356; Contains = "let applicative3<"; Claim = "applicative3 CE (sequential, 3 layers)" }
      { File = "Builders.fs"; Line = 358; Contains = "Use zapp instead"; Claim = "applicative' is obsolete, renamed to zapp" }
      { File = "Builders.fs"; Line = 359; Contains = "let applicative'<"; Claim = "applicative' still defined (warning, not error - so it still compiles)" }
      { File = "Builders.fs"; Line = 361; Contains = "Use zapp2 instead"; Claim = "applicative2' is obsolete, renamed to zapp2" }
      { File = "Builders.fs"; Line = 364; Contains = "Use zapp3 instead"; Claim = "applicative3' is obsolete, renamed to zapp3" }
      { File = "Builders.fs"; Line = 368; Contains = "let zapp<"; Claim = "zapp is the current name" }
      { File = "Builders.fs"; Line = 371; Contains = "let zapp2<"; Claim = "zapp2 is the current name" }
      { File = "Builders.fs"; Line = 374; Contains = "let zapp3<"; Claim = "zapp3 is the current name" }
      { File = "Builders.fs"; Line = 295; Contains = "result >> result"; Claim = "SEQUENTIAL applicative builders lift with result" }
      { File = "Builders.fs"; Line = 316; Contains = "pur x"; Claim = "ZIP applicative builders lift with pur, not result" }
      { File = "Builders.fs"; Line = 326; Contains = "pur >> pur"; Claim = "zapp2 lifts with pur" }
      { File = "Builders.fs"; Line = 336; Contains = "pur >> pur >> pur"; Claim = "zapp3 lifts with pur" }
      { File = "Builders.fs"; Line = 229; Contains = "Check the type is lazy"; Claim = "MonadPlusBuilder.While probes laziness at compile time" }
      { File = "Builders.fs"; Line = 99; Contains = "AutoOpen"; Claim = "GenericBuilders is AutoOpen - CEs come from 'open FSharpPlus'" }
      { File = "Operators.fs"; Line = 8; Contains = "AutoOpen"; Claim = "Operators is AutoOpen - generic functions come from 'open FSharpPlus'" }
      { File = "Operators.fs"; Line = 199; Contains = "let inline result"; Claim = "result is the monad/sequential-applicative lift" }
      { File = "Operators.fs"; Line = 199; Contains = "Return.Invoke"; Claim = "result is backed by Return" }
      { File = "Operators.fs"; Line = 252; Contains = "let inline pur"; Claim = "pur is the zip-applicative lift - a DIFFERENT function from result" }
      { File = "Operators.fs"; Line = 252; Contains = "Pure.Invoke"; Claim = "pur is backed by Pure, not Return" }
      { File = "Operators.fs"; Line = 142; Contains = "(<!>)"; Claim = "<!> is map with the function first" }
      { File = "Operators.fs"; Line = 155; Contains = "(|>>)"; Claim = "|>> is map with the value first" }
      { File = "Operators.fs"; Line = 205; Contains = "(<*>)"; Claim = "<*> is sequential apply" }
      { File = "Operators.fs"; Line = 226; Contains = "( *>)"; Claim = "*> keeps the right value" }
      { File = "Operators.fs"; Line = 232; Contains = "(<*  )"; Claim = "<* keeps the left value" }
      { File = "Operators.fs"; Line = 259; Contains = "(<.>)"; Claim = "<.> is zip apply" }
      { File = "Operators.fs"; Line = 277; Contains = "(.>)"; Claim = ".> keeps the right value (zip)" }
      { File = "Operators.fs"; Line = 283; Contains = "(<.)"; Claim = "<. keeps the left value (zip)" }
      { File = "Operators.fs"; Line = 328; Contains = "(>>=)"; Claim = ">>= is bind, value first" }
      { File = "Operators.fs"; Line = 334; Contains = "(=<<)"; Claim = "=<< is bind, function first" }
      { File = "Operators.fs"; Line = 340; Contains = "(>=>)"; Claim = ">=> is left-to-right Kleisli" }
      { File = "Operators.fs"; Line = 346; Contains = "(<=<)"; Claim = "<=< is right-to-left Kleisli" }
      { File = "Operators.fs"; Line = 379; Contains = "(++)"; Claim = "++ is the monoid operator (not <>)" }
      { File = "Operators.fs"; Line = 379; Contains = "Plus.Invoke"; Claim = "++ is backed by Plus" }
      { File = "Operators.fs"; Line = 415; Contains = "(<|>)"; Claim = "<|> combines alternatives" }
      { File = "Operators.fs"; Line = 1002; Contains = "(=>>)"; Claim = "=>> is comonadic extend" }
      { File = "Operators.fs"; Line = 290; Contains = "(<<||)"; Claim = "<<|| is map2, function first, tupled" }
      { File = "Operators.fs"; Line = 297; Contains = "(||>>)"; Claim = "||>> is map2, tupled" }
      { File = "Operators.fs"; Line = 304; Contains = "(<<|||)"; Claim = "<<||| is map3, function first, tupled" }
      { File = "Operators.fs"; Line = 311; Contains = "(|||>>)"; Claim = "|||>> is map3, tupled" }
      { File = "Operators.fs"; Line = 52; Contains = "(</)"; Claim = "</ opens infix function application" }
      { File = "Operators.fs"; Line = 58; Contains = "(/>)"; Claim = "/> closes infix function application" }
      { File = "Operators.fs"; Line = 211; Contains = "let inline lift2"; Claim = "lift2 exists (use instead of Haskell liftA2)" }
      { File = "Lens.fs"; Line = 10; Contains = "module Lens"; Claim = "Lens is a plain module - NOT AutoOpen" }
      { File = "Lens.fs"; Line = 236; Contains = "(^.)"; Claim = "^. is view, source on the left" }
      { File = "Lens.fs"; Line = 242; Contains = "(.->)"; Claim = ".-> is setl, lens on the left" }
      { File = "Lens.fs"; Line = 248; Contains = "(%->)"; Claim = "%-> is over, lens on the left" }
      { File = "Lens.fs"; Line = 254; Contains = "(^?)"; Claim = "^? is preview, returns option" }
      { File = "Lens.fs"; Line = 257; Contains = "(^..)"; Claim = "^.. is toListOf" }
      { File = "Lens.fs"; Line = 263; Contains = "(<&>)"; Claim = "<&> lives in FSharpPlus.Lens, not FSharpPlus" }
      { File = "Parsing.fs"; Line = 5; Contains = "AutoOpen"; Claim = "Parsing is AutoOpen - tryParse comes from 'open FSharpPlus'" }
      { File = "Memoization.fs"; Line = 8; Contains = "AutoOpen"; Claim = "Memoization is AutoOpen" }
      { File = "String.fs"; Line = 4; Contains = "RequireQualifiedAccess"; Claim = "extension modules are RequireQualifiedAccess" }
      { File = "Generic.fs"; Line = 12; Contains = "module Generic"; Claim = "Math.Generic is a plain module - needs its own open" }
      { File = "Applicative.fs"; Line = 15; Contains = "module Applicative"; Claim = "Math.Applicative is a plain module" }
      { File = "Applicative.fs"; Line = 68; Contains = "module ZipApplicative"; Claim = "Math.ZipApplicative is a plain module" }
      { File = "Seq.fs"; Line = 607; Contains = "let inline mapM"; Claim = "mapM exists ONLY as SeqT.mapM, not as a global function" }
      { File = "ZipApplicative.fs"; Line = 46; Contains = "List.cycle"; Claim = "pur for list is List.cycle, i.e. conceptually infinite - so it has no finite // val demo" }
      { File = "ZipApplicative.fs"; Line = 84; Contains = "List.map2Shortest"; Claim = "<.> on list zips element-wise, shortest-wins - the sequential_vs_zip snippet depends on this" }
      { File = "Program.fs"; Line = 6; Contains = "let main argv"; Claim = "the docs harness entry point exists" }
      { File = "Program.fs"; Line = 7; Contains = "0"; Claim = "the docs harness body is just '0' - it executes nothing, so // val claims are never checked" }
      { File = "computation-expressions.fsx"; Line = 5; Contains = "monad {"; Claim = "CE page documents monad" }
      { File = "computation-expressions.fsx"; Line = 15; Contains = "monad'"; Claim = "CE page documents monad'" }
      { File = "computation-expressions.fsx"; Line = 31; Contains = "monad.plus'"; Claim = "CE page documents monad.plus'" }
      { File = "computation-expressions.fsx"; Line = 47; Contains = "monad.fx"; Claim = "CE page documents monad.fx for Async" }
      { File = "computation-expressions.fsx"; Line = 47; Contains = "asnNumber: Async<_>"; Claim = "upstream annotates the BINDING, not the expression" }
      { File = "computation-expressions.fsx"; Line = 57; Contains = "lstNumber: list<_>"; Claim = "upstream annotates the binding for list too" }
      { File = "abstraction-zipapplicative.fsx"; Line = 154; Contains = "applicative2'"; Claim = "the ONE remaining obsolete CE use in the upstream docs" }
      { File = "lens.fsx"; Line = 198; Contains = "length, traverse"; Claim = "the lens page notes inline that a second open is needed" }
      { File = "tutorial.fsx"; Line = 5; Contains = "#r"; Claim = "doc pages contain #r yet compile as <Compile> items - no #if guard needed" }
      { File = "index.fsx"; Line = 4; Contains = "#r"; Claim = "index.fsx is a real doc page with directives" }
      { File = "RELEASE_NOTES.md"; Line = 9; Contains = "(.>)"; Claim = ".> and <. were added in 1.9.1" }
      { File = "RELEASE_NOTES.md"; Line = 11; Contains = "zapp"; Claim = "the zapp rename shipped in 1.9.1" }
      { File = "RELEASE_NOTES.md"; Line = 19; Contains = "||>>"; Claim = "map2/map3 operators were added in 1.8.0" }
      { File = "dotnetcore.yml"; Line = 95; Contains = "AllDocs"; Claim = "CI runs the AllDocs target" }
      { File = "build.proj"; Line = 24; Contains = "FSharpPlus.sln"; Claim = "AllDocs builds the whole solution" }
      { File = "FSharpPlus.sln"; Line = 96; Contains = "FSharpPlus.Docs"; Claim = "the docs project is in the solution, so CI compiles it" }
    ]

/// A name F#+ does not define, and what to reach for instead.
type AbsentName = { Name: string; Instead: string; From: string }

/// A name F#+ does define, with what we claim it is for.
type PresentName = { Name: string; Claim: string }

/// A name defined in exactly one place and reachable only qualified.
type QualifiedOnlyName = { Name: string; ExpectedFile: string; ExpectedModule: string; Claim: string }

/// Absence is the one claim class a search settles more firmly than a compiler can: a compiler tells
/// you a name failed to resolve in one context, whereas scanning every definition site tells you it
/// is defined nowhere. It is also the most fragile - any upstream addition falsifies it - which is
/// why it is re-checked per release rather than asserted once.
let absent : AbsentName list =
    [
      { Name = "fmap";            Instead = "map, or <!> / |>>";                                       From = "Haskell" }
      { Name = "|!>";             Instead = "map (fun _ -> replacement)";                              From = "upstream after the 1.9.1 release" }
      { Name = "<!|";             Instead = "map (fun _ -> replacement)";                              From = "upstream after the 1.9.1 release" }
      { Name = "pure";            Instead = "result (monad/sequential) or pur (zip)";                  From = "Haskell" }
      { Name = "liftA";           Instead = "map; lift2 / lift3 for multiple arguments";               From = "Haskell" }

      { Name = "forM";            Instead = "traverse, or a monad CE";                                 From = "Haskell" }
      { Name = "sequence_";       Instead = "sequence >> map ignore (retain the outer effect)";        From = "Haskell" }
      { Name = "traverse_";       Instead = "traverse >> map ignore (retain the outer effect)";        From = "Haskell" }
      { Name = "asyncResult";     Instead = "ResultT, or explicit Result handling inside Async";       From = "FsToolkit.ErrorHandling" }
      { Name = "taskResult";      Instead = "ResultT, or explicit Result handling inside Task";        From = "FsToolkit.ErrorHandling" }
      { Name = "sequenceResult";  Instead = "generic sequence";                                        From = "FsToolkit.ErrorHandling" }
      { Name = "traverseResult";  Instead = "generic traverse";                                        From = "FsToolkit.ErrorHandling" }
      { Name = "bindResult";      Instead = "generic >>=";                                             From = "FsToolkit.ErrorHandling" }
      { Name = "memoize";         Instead = "memoizeN (the only memoize function F#+ defines)";        From = "plausible guess from the module name" }
    ]

let present : PresentName list =
    [
      { Name = "liftA2";      Claim = "obsolete compatibility alias; prefer lift2" }
      { Name = "map";         Claim = "the generic functor map" }
      { Name = "result";      Claim = "monad / sequential applicative lift" }
      { Name = "pur";         Claim = "zip applicative lift" }
      { Name = "lift2";       Claim = "replaces Haskell liftA2" }
      { Name = "lift3";       Claim = "3-argument lift" }
      { Name = "traverse";    Claim = "replaces Haskell mapM" }
      { Name = "sequence";    Claim = "replaces Haskell sequenceA" }
      { Name = "bind";        Claim = "monadic bind" }
      { Name = "join";        Claim = "monadic join" }
      { Name = "map2";        Claim = "2-argument map" }
      { Name = "map3";        Claim = "3-argument map" }
      { Name = "zip";         Claim = "generic zip" }
      { Name = "unzip";       Claim = "generic unzip" }
      { Name = "tap";         Claim = "side-effect passthrough, |- flipped" }
      { Name = "memoizeN";    Claim = "from the AutoOpen Memoization module - note there is no plain `memoize`" }
      { Name = "tryParse";    Claim = "from the AutoOpen Parsing module" }
      { Name = "parse";       Claim = "from the AutoOpen Parsing module" }
      { Name = "view";        Claim = "lens read - needs open FSharpPlus.Lens" }
      { Name = "setl";        Claim = "lens write" }
      { Name = "over";        Claim = "lens modify" }
      { Name = "preview";     Claim = "prism read" }
      { Name = "toListOf";    Claim = "fold a lens to a list" }
    ]

let qualifiedOnly : QualifiedOnlyName list =
    [
      { Name = "mapM"
        ExpectedFile = "src/FSharpPlus/Data/Seq.fs"
        ExpectedModule = "FSharpPlus.Data.SeqT"
        Claim = "mapM exists ONLY as SeqT.mapM - it is not a global generic function, so `mapM f xs` will not resolve. Use traverse." }
    ]
