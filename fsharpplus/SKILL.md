---
name: fsharpplus
license: MIT
description: Use for FSharpPlus or F#+; code containing `open FSharpPlus`; polymorphic F# `map`, `bind`, or `traverse`; operators `>>=`, `<!>`, `<*>`, or `>=>`; F# monad transformers ReaderT, StateT, OptionT, or ResultT; the `monad` computation expression; F# lenses/optics; or SRTP and "member constraint" compiler errors in code that opens FSharpPlus. Use even when the user does not name the library but the code clearly uses these APIs.
---

F#+ provides generic programming over F# types through statically resolved type parameters (SRTP). It extends core F# rather than replacing it. This skill targets FSharpPlus 1.9.1; its examples are verified with .NET SDK 8.0.425. For other versions, check the project's installed API and compile against that project. The license is in `LICENSE`; recipe provenance is in `references/use-case-sources.json`.

## Route the task

| Symptom or task | Read |
|---|---|
| Need a generic function or operator, `tryParse`, collection helpers, or transformer operations (`lift`, `get`, `throw`) | `references/generic-functions.md` |
| Ambiguous or missing function; wrong overload picked | `references/namespaces-and-shadowing.md` |
| Compiler error mentioning member constraints or no overloads | `references/srtp-errors.md` |
| `monad` CE, applicative CE, or transformer stacks | `references/computation-expressions.md` |
| Make a custom type work with `map`, `bind`, or `traverse` | `references/extending-types.md` |
| Independent Result errors with zapp; Validation compatibility; generic vs concrete style; layout; lens naming | `references/idioms-and-antipatterns.md` |
| Practical pattern from a problem (optional actions, state, optics, validation, codecs, async routing) | `references/real-world-use-cases.md` |

## Non-negotiable rules

1. Always compile-verify F#+ code before presenting it; SRTP errors only appear at compile time.
2. Start with `open FSharpPlus`; add `FSharpPlus.Data` only for its types; add `FSharpPlus.Lens` only for optics.
3. Match the surrounding codebase. In F#+-style code, generic `map`, `>>=`, `<!>`/`<*>`, `traverse`, and `monad` builders are idiomatic even on known types; where F#+ is incidental, concrete functions such as `List.map` and built-in builders keep diagnostics simpler. With no surrounding code, use generic forms for polymorphic code or F#+ types, concrete ones otherwise. Semantics differ: Result error accumulation needs `zapp`, generic `map2` or `(<.>)`, not `Result.map2`; on `list`, `<*>` is cartesian and `<.>` zips.
4. When generic resolution fails, annotate the binding or the function's return type before trying anything else, leaving `_` holes for inference: `let items: _ list = monad.plus' { ... }`. A builder type argument such as `monad'<Task<_>>` is an alternative (see `references/computation-expressions.md`). Do not annotate `let!` patterns inside `monad`; that breaks inference in transformer stacks. Annotating the bound expression, as in `let! env = (ask: Stack<string>)`, is fine (see `references/srtp-errors.md`, case 6).
5. Do not invent functions. Check `references/generic-functions.md`, then the [API reference](https://fsprojects.github.io/FSharpPlus/reference/index.html); if still absent, it does not exist.
