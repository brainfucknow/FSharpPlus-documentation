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
      { File = "Builders.fs"; Line = 257; Contains = "let monad<"; Claim = "monad -> MonadFxBuilder" }
      { File = "Builders.fs"; Line = 257; Contains = "MonadFxBuilder"; Claim = "monad is the Fx (lazy) builder" }
      { File = "Builders.fs"; Line = 260; Contains = "let monad'<"; Claim = "monad' exists" }
      { File = "Builders.fs"; Line = 260; Contains = "MonadFxStrictBuilder"; Claim = "monad' -> MonadFxStrictBuilder (strict)" }
      { File = "Builders.fs"; Line = 157; Contains = "_.strict"; Claim = "monad.strict exists on MonadFxBuilder" }
      { File = "Builders.fs"; Line = 157; Contains = "MonadFxStrictBuilder"; Claim = "monad.strict == monad'" }
      { File = "Builders.fs"; Line = 160; Contains = "_.plus"; Claim = "monad.plus exists" }
      { File = "Builders.fs"; Line = 160; Contains = "MonadPlusBuilder"; Claim = "monad.plus is the lazy additive builder" }
      { File = "Builders.fs"; Line = 163; Contains = "_.plus'"; Claim = "monad.plus' exists" }
      { File = "Builders.fs"; Line = 163; Contains = "MonadPlusStrictBuilder"; Claim = "monad.plus' is the strict additive builder" }
      { File = "Builders.fs"; Line = 166; Contains = "this.fx    = this"; Claim = "monad.fx returns this, so monad.fx == monad" }
      { File = "Builders.fs"; Line = 169; Contains = "_.fx'"; Claim = "monad.fx' exists" }
      { File = "Builders.fs"; Line = 169; Contains = "MonadFxStrictBuilder"; Claim = "monad.fx' == monad'" }
      { File = "Builders.fs"; Line = 133; Contains = "_.strict"; Claim = "MonadPlusBuilder ALSO has .strict - the fifth spelling the plan missed" }
      { File = "Builders.fs"; Line = 133; Contains = "MonadPlusStrictBuilder"; Claim = "monad.plus.strict == monad.plus'" }
      { File = "Builders.fs"; Line = 99; Contains = "type MonadPlusStrictBuilder"; Claim = "MonadPlusStrictBuilder is a distinct builder type" }
      { File = "Builders.fs"; Line = 114; Contains = "type MonadFxStrictBuilder"; Claim = "MonadFxStrictBuilder is a distinct builder type" }
      { File = "Builders.fs"; Line = 130; Contains = "type MonadPlusBuilder"; Claim = "MonadPlusBuilder is a distinct builder type" }
      { File = "Builders.fs"; Line = 155; Contains = "type MonadFxBuilder"; Claim = "MonadFxBuilder is a distinct builder type" }
      { File = "Builders.fs"; Line = 263; Contains = "let applicative<"; Claim = "applicative CE (sequential, 1 layer)" }
      { File = "Builders.fs"; Line = 266; Contains = "let applicative2<"; Claim = "applicative2 CE (sequential, 2 layers)" }
      { File = "Builders.fs"; Line = 269; Contains = "let applicative3<"; Claim = "applicative3 CE (sequential, 3 layers)" }
      { File = "Builders.fs"; Line = 271; Contains = "Use zapp instead"; Claim = "applicative' is obsolete, renamed to zapp" }
      { File = "Builders.fs"; Line = 272; Contains = "let applicative'<"; Claim = "applicative' still defined (warning, not error - so it still compiles)" }
      { File = "Builders.fs"; Line = 274; Contains = "Use zapp2 instead"; Claim = "applicative2' is obsolete, renamed to zapp2" }
      { File = "Builders.fs"; Line = 277; Contains = "Use zapp3 instead"; Claim = "applicative3' is obsolete, renamed to zapp3" }
      { File = "Builders.fs"; Line = 281; Contains = "let zapp<"; Claim = "zapp is the current name" }
      { File = "Builders.fs"; Line = 284; Contains = "let zapp2<"; Claim = "zapp2 is the current name" }
      { File = "Builders.fs"; Line = 287; Contains = "let zapp3<"; Claim = "zapp3 is the current name" }
      { File = "Builders.fs"; Line = 208; Contains = "result >> result"; Claim = "SEQUENTIAL applicative builders lift with result" }
      { File = "Builders.fs"; Line = 229; Contains = "pur x"; Claim = "ZIP applicative builders lift with pur, not result" }
      { File = "Builders.fs"; Line = 239; Contains = "pur >> pur"; Claim = "zapp2 lifts with pur" }
      { File = "Builders.fs"; Line = 249; Contains = "pur >> pur >> pur"; Claim = "zapp3 lifts with pur" }
      { File = "Builders.fs"; Line = 142; Contains = "Check the type is lazy"; Claim = "MonadPlusBuilder.While probes laziness at compile time" }
      { File = "Builders.fs"; Line = 17; Contains = "AutoOpen"; Claim = "GenericBuilders is AutoOpen - CEs come from 'open FSharpPlus'" }
      { File = "Operators.fs"; Line = 9; Contains = "AutoOpen"; Claim = "Operators is AutoOpen - generic functions come from 'open FSharpPlus'" }
      { File = "Operators.fs"; Line = 219; Contains = "let inline result"; Claim = "result is the monad/sequential-applicative lift" }
      { File = "Operators.fs"; Line = 219; Contains = "Return.Invoke"; Claim = "result is backed by Return" }
      { File = "Operators.fs"; Line = 263; Contains = "let inline pur"; Claim = "pur is the zip-applicative lift - a DIFFERENT function from result" }
      { File = "Operators.fs"; Line = 263; Contains = "Pure.Invoke"; Claim = "pur is backed by Pure, not Return" }
      { File = "Operators.fs"; Line = 141; Contains = "(<!>)"; Claim = "<!> is map with the function first" }
      { File = "Operators.fs"; Line = 154; Contains = "(|>>)"; Claim = "|>> is map with the value first" }
      { File = "Operators.fs"; Line = 225; Contains = "(<*>)"; Claim = "<*> is sequential apply" }
      { File = "Operators.fs"; Line = 243; Contains = "( *>)"; Claim = "*> keeps the right value" }
      { File = "Operators.fs"; Line = 249; Contains = "(<*  )"; Claim = "<* keeps the left value" }
      { File = "Operators.fs"; Line = 270; Contains = "(<.>)"; Claim = "<.> is zip apply" }
      { File = "Operators.fs"; Line = 288; Contains = "(.>)"; Claim = ".> keeps the right value (zip)" }
      { File = "Operators.fs"; Line = 294; Contains = "(<.)"; Claim = "<. keeps the left value (zip)" }
      { File = "Operators.fs"; Line = 338; Contains = "(>>=)"; Claim = ">>= is bind, value first" }
      { File = "Operators.fs"; Line = 344; Contains = "(=<<)"; Claim = "=<< is bind, function first" }
      { File = "Operators.fs"; Line = 350; Contains = "(>=>)"; Claim = ">=> is left-to-right Kleisli" }
      { File = "Operators.fs"; Line = 356; Contains = "(<=<)"; Claim = "<=< is right-to-left Kleisli" }
      { File = "Operators.fs"; Line = 389; Contains = "(++)"; Claim = "++ is the monoid operator (not <>)" }
      { File = "Operators.fs"; Line = 389; Contains = "Plus.Invoke"; Claim = "++ is backed by Plus" }
      { File = "Operators.fs"; Line = 425; Contains = "(<|>)"; Claim = "<|> combines alternatives" }
      { File = "Operators.fs"; Line = 1023; Contains = "(=>>)"; Claim = "=>> is comonadic extend" }
      { File = "Operators.fs"; Line = 301; Contains = "(<<||)"; Claim = "<<|| is map2, function first, tupled" }
      { File = "Operators.fs"; Line = 308; Contains = "(||>>)"; Claim = "||>> is map2, tupled" }
      { File = "Operators.fs"; Line = 315; Contains = "(<<|||)"; Claim = "<<||| is map3, function first, tupled" }
      { File = "Operators.fs"; Line = 322; Contains = "(|||>>)"; Claim = "|||>> is map3, tupled" }
      { File = "Operators.fs"; Line = 54; Contains = "(</)"; Claim = "</ opens infix function application" }
      { File = "Operators.fs"; Line = 60; Contains = "(/>)"; Claim = "/> closes infix function application" }
      { File = "Operators.fs"; Line = 231; Contains = "let inline lift2"; Claim = "lift2 exists (use instead of Haskell liftA2)" }
      { File = "Lens.fs"; Line = 9; Contains = "module Lens"; Claim = "Lens is a plain module - NOT AutoOpen" }
      { File = "Lens.fs"; Line = 229; Contains = "(^.)"; Claim = "^. is view, source on the left" }
      { File = "Lens.fs"; Line = 235; Contains = "(.->)"; Claim = ".-> is setl, lens on the left" }
      { File = "Lens.fs"; Line = 241; Contains = "(%->)"; Claim = "%-> is over, lens on the left" }
      { File = "Lens.fs"; Line = 247; Contains = "(^?)"; Claim = "^? is preview, returns option" }
      { File = "Lens.fs"; Line = 250; Contains = "(^..)"; Claim = "^.. is toListOf" }
      { File = "Lens.fs"; Line = 256; Contains = "(<&>)"; Claim = "<&> lives in FSharpPlus.Lens, not FSharpPlus" }
      { File = "Parsing.fs"; Line = 5; Contains = "AutoOpen"; Claim = "Parsing is AutoOpen - tryParse comes from 'open FSharpPlus'" }
      { File = "Memoization.fs"; Line = 8; Contains = "AutoOpen"; Claim = "Memoization is AutoOpen" }
      { File = "String.fs"; Line = 4; Contains = "RequireQualifiedAccess"; Claim = "extension modules are RequireQualifiedAccess" }
      { File = "Generic.fs"; Line = 12; Contains = "module Generic"; Claim = "Math.Generic is a plain module - needs its own open" }
      { File = "Applicative.fs"; Line = 15; Contains = "module Applicative"; Claim = "Math.Applicative is a plain module" }
      { File = "Applicative.fs"; Line = 68; Contains = "module ZipApplicative"; Claim = "Math.ZipApplicative is a plain module" }
      { File = "Seq.fs"; Line = 586; Contains = "let inline mapM"; Claim = "mapM exists ONLY as SeqT.mapM, not as a global function" }
      { File = "ZipApplicative.fs"; Line = 44; Contains = "List.cycle"; Claim = "pur for list is List.cycle, i.e. conceptually infinite - so it has no finite // val demo" }
      { File = "ZipApplicative.fs"; Line = 81; Contains = "List.map2Shortest"; Claim = "<.> on list zips element-wise, shortest-wins - the sequential_vs_zip snippet depends on this" }
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
      { File = "RELEASE_NOTES.md"; Line = 8; Contains = "(.>)"; Claim = ".> and <. were added in 1.9.1" }
      { File = "RELEASE_NOTES.md"; Line = 10; Contains = "zapp"; Claim = "the zapp rename shipped in 1.9.1" }
      { File = "RELEASE_NOTES.md"; Line = 18; Contains = "||>>"; Claim = "map2/map3 operators were added in 1.8.0" }
      { File = "dotnetcore.yml"; Line = 98; Contains = "AllDocs"; Claim = "CI runs the AllDocs target" }
      { File = "build.proj"; Line = 24; Contains = "FSharpPlus.sln"; Claim = "AllDocs builds the whole solution" }
      { File = "FSharpPlus.sln"; Line = 96; Contains = "FSharpPlus.Docs"; Claim = "the docs project is in the solution, so CI compiles it" }
    ]

/// A name F#+ does not define, and what to reach for instead.
type AbsentName = { Name: string; Instead: string; From: string }

/// A name F#+ does define, with what we claim it is for.
type PresentName = { Name: string; Claim: string }

/// A name defined in exactly one place and reachable only qualified.
type QualifiedOnlyName = { Name: string; ExpectedFile: string; Claim: string }

/// Absence is the one claim class a search settles more firmly than a compiler can: a compiler tells
/// you a name failed to resolve in one context, whereas scanning every definition site tells you it
/// is defined nowhere. It is also the most fragile - any upstream addition falsifies it - which is
/// why it is re-checked per release rather than asserted once.
let absent : AbsentName list =
    [
      { Name = "fmap";            Instead = "map, or <!> / |>>";                                       From = "Haskell" }
      { Name = "pure";            Instead = "result (monad/sequential) or pur (zip)";                  From = "Haskell" }
      { Name = "liftA";           Instead = "lift2 / lift3";                                           From = "Haskell" }
      { Name = "liftA2";          Instead = "lift2";                                                   From = "Haskell" }
      { Name = "forM";            Instead = "traverse, or a monad CE";                                 From = "Haskell" }
      { Name = "sequence_";       Instead = "sequence >> ignore";                                      From = "Haskell" }
      { Name = "traverse_";       Instead = "traverse >> ignore";                                      From = "Haskell" }
      { Name = "asyncResult";     Instead = "monad over Async<Result<_,_>>, or ResultT";               From = "FsToolkit.ErrorHandling" }
      { Name = "taskResult";      Instead = "monad over Task<Result<_,_>>, or ResultT";                From = "FsToolkit.ErrorHandling" }
      { Name = "sequenceResult";  Instead = "generic sequence";                                        From = "FsToolkit.ErrorHandling" }
      { Name = "traverseResult";  Instead = "generic traverse";                                        From = "FsToolkit.ErrorHandling" }
      { Name = "bindResult";      Instead = "generic >>=";                                             From = "FsToolkit.ErrorHandling" }
      { Name = "memoize";         Instead = "memoizeN (the only memoize function F#+ defines)";        From = "plausible guess from the module name" }
    ]

let present : PresentName list =
    [
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
        Claim = "mapM exists ONLY as SeqT.mapM - it is not a global generic function, so `mapM f xs` will not resolve. Use traverse." }
    ]
