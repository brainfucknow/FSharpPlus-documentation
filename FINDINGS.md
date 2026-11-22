INCONSISTENCY FOUND: gusty uses fmap in documentation

Found inconsistency between FSharpPlus skill definitions and gusty actual coding style:

FILE: gusty-FsControl/README.md
LINE: Contains "[fmap](https://wiki.haskell.org/Functor) for Haskellers"
ISSUE: References fmap in documentation
SKILL PRESCRIPTION: fmap does not exist - use map or (<!>) instead

EVIDENCE:
The FSharpPlus skill (fsharpplus/SKILL.md, AGENTS.md) explicitly states:
- "fmap" does not exist
- Use "map" or "(<!>)" instead

However, gusty FsControl README.md contains:
"Here example with map ([fmap](https://wiki.haskell.org/Functor) for Haskellers, [Select] for C-sharpers):"

This is a clear inconsistency where gusty documentation references a function (fmap) that the skill says does not exist.

Tested: cat gusty-FsControl/README.md
Verdict: INCONSISTENCY FOUND