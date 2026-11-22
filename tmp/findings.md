# Findings: Inconsistencies between FSharpPlus skill and gusty's style

## Research Question
Are the FSharpPlus skill definitions in this repository consistent with gusty's F# coding style?

## Methodology
1. Extracted style rules from skill definitions (fsharpplus/SKILL.md, fsharpplus/references/*.md)
2. Analyzed gusty's actual code from FsControl, FSharpPlus 1.9.1
3. Compared point by point

## Skill Definition Style Rules
- Obsolete names: liftA2 should use lift2; applicative', applicative2', applicative3' should use zapp, zapp2, zapp3
- Non-existent names: fmap, pure, forM, mapM do not exist; use map/(<!>) instead of fmap, result or pur instead of pure
- Annotation style: Do not annotate let! patterns inside monad; that breaks inference in transformer stacks

## Inconsistencies Found

### 1. liftA2 usage in FsControl (MAJOR INCONSISTENCY)

Skill Rule: liftA2 is obsolete, use lift2 instead (fsharpplus/references/generic-functions.md: "Names that do not exist" section)

gusty's actual code: In gusty-FsControl/FsControl.Core/Operators.fs:
```fsharp
/// Apply 2 lifted arguments to a lifted function.
let inline liftA2 (f:'T->'U->'V) (a:'``Applicative<'T>``) (b:'``Applicative<'U>``) : '``Applicative<'V>`` = f <!> a <*> b
```

Verdict: INCONSISTENT - gusty defines and uses liftA2 in his own FsControl library, but the skill says it's obsolete.

### 2. applicative2' usage in FSharpPlus documentation (MAJOR INCONSISTENCY)

Skill Rule: applicative2' is obsolete, use zapp2 instead (fsharpplus/references/generic-functions.md: "Obsolete names" section)

FSharpPlus source code: In FSharpPlus-1.9.1/src/FSharpPlus/Builders.fs:
```fsharp
[<ObsoleteAttribute("This value is obsolete. Use zapp2 instead.", false)>]
let applicative2' = ZipApplicativeBuilder2()
```

gusty's actual usage: In FSharpPlus-1.9.1/docsrc/content/abstraction-zipapplicative.fsx:154:
```fsharp
let validated = applicative2' {
    let! x = async { return Ok 1 }
    and! y = async { return Error ["Error1"] }
    and! z = async { return Error ["Error2"] }
    return x + y + z
}
```

Verdict: INCONSISTENT - gusty's own FSharpPlus documentation uses the obsolete applicative2' builder.

### 3. fmap reference in FSharpPlus documentation (MAJOR INCONSISTENCY)

Skill Rule: fmap does not exist, use map or (<!>) instead (fsharpplus/references/generic-functions.md: "Names that do not exist" section)

gusty's actual usage: In FSharpPlus-1.9.1/docsrc/content/tutorial.fsx:33:
```fsharp
here's an example with map ([fmap](https://wiki.haskell.org/Functor) for Haskellers, [Select](http://www.dotnetperls.com/select) for C-sharpers):
```

Verdict: INCONSISTENT - gusty's own FSharpPlus documentation refers to fmap as a valid name, but the skill says it doesn't exist.

## Overall Verdict

INCONSISTENT: Multiple direct contradictions found between the FSharpPlus skill definitions and gusty's actual F# coding style in both his FsControl library and the official FSharpPlus documentation.

## Evidence Summary

| Inconsistency | Skill Rule | gusty's Actual Usage | Location |
|---|---|---|---|
| liftA2 obsolete | Use lift2 instead | Defines and uses liftA2 | gusty-FsControl/FsControl.Core/Operators.fs |
| applicative2' obsolete | Use zapp2 instead | Uses applicative2' | FSharpPlus-1.9.1/docsrc/content/abstraction-zipapplicative.fsx:154 |
| fmap doesn't exist | Use map instead | References fmap | FSharpPlus-1.9.1/docsrc/content/tutorial.fsx:33 |

## Tested Commands
Tested: Read gusty-FsControl/FsControl.Core/Operators.fs
Tested: Read FSharpPlus-1.9.1/src/FSharpPlus/Builders.fs  
Tested: Read FSharpPlus-1.9.1/docsrc/content/abstraction-zipapplicative.fsx
Tested: Read FSharpPlus-1.9.1/docsrc/content/tutorial.fsx
Tested: Read fsharpplus/references/generic-functions.md
Tested: Read fsharpplus/SKILL.md