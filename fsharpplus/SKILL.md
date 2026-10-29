---
name: fsharpplus
description: Use for FSharpPlus or F#+; code containing `open FSharpPlus`; polymorphic F# `map`, `bind`, or `traverse`; operators `>>=`, `<!>`, `<*>`, or `>=>`; F# monad transformers ReaderT, StateT, OptionT, or ResultT; the `monad` computation expression; F# lenses/optics; or SRTP and "member constraint" compiler errors in code that opens FSharpPlus. Use even when the user does not name the library but the code clearly uses these APIs.
---

F#+ provides generic programming over F# types through statically resolved type parameters (SRTP). It extends core F# rather than replacing it; concrete core modules remain the default for concrete code. This skill targets FSharpPlus 1.9.1.

## Route the task

| Symptom or task | Read |
|---|---|
| Need a generic function or operator | `references/generic-functions.md` |
| Ambiguous or missing function; wrong overload picked | `references/namespaces-and-shadowing.md` |
| Compiler error mentioning member constraints or no overloads | `references/srtp-errors.md` |
| `monad` CE, applicative CE, or transformer stacks | `references/computation-expressions.md` |
| Make a custom type work with `map`, `bind`, or `traverse` | `references/extending-types.md` |
| Design choice: Validation vs Result, generic vs concrete, lens naming | `references/idioms-and-antipatterns.md` |
| Practical pattern from a problem (optional actions, state, optics, validation, codecs, async routing) | `references/real-world-use-cases.md` |

## Non-negotiable rules

1. Always compile-verify F#+ code before presenting it; SRTP errors only appear at compile time.
2. Start with `open FSharpPlus`; add `FSharpPlus.Data` only for its types; add `FSharpPlus.Lens` only for optics.
3. Prefer a concrete module function such as `List.map` or `Option.bind` in monomorphic code; use the generic form only for actually generic code or an F#+ type.
4. When generic resolution fails, annotate the binding's result type or the function's return type before trying anything else. Do not annotate `let!` patterns inside `monad`; that breaks inference in transformer stacks (see `references/srtp-errors.md`, case 6).
5. Do not invent functions. Check `references/generic-functions.md`, then the [API reference](https://fsprojects.github.io/FSharpPlus/reference/index.html); if still absent, it does not exist.
