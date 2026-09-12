# Namespaces and shadowing

All snippets assume the package reference shown in `generic-functions.md`.

| Namespace or module | Contents and when to open it |
|---|---|
| `FSharpPlus` | Generic functions, builders, and extended core modules. Open for F#+ generic programming. |
| `FSharpPlus.Data` | F#+ data types such as `NonEmptyList`, `Validation`, `DList`, and transformers. Open only when naming these types or their cases. |
| `FSharpPlus.Lens` | `view`, `setl`, `over`, and standard optics. Open only for optics. |
| `FSharpPlus.Math` | Generic numeric helpers. Open only for generic numeric code. |
| `FSharpPlus.Operators` | Definitions underlying the names re-exported by `FSharpPlus`; qualify it when a narrow import is preferable. |
| `FSharpPlus.Control` | The dispatcher types (`Map`, `Bind`, `Traverse`, ...) with their `Invoke` members. `Map.Invoke` and friends are not visible after `open FSharpPlus` alone. Inside an instance implementation prefer the generic functions `map`, `lift2`, and `bind` from `FSharpPlus`; open `FSharpPlus.Control` only when calling a dispatcher directly. |

## What the open changes

`open FSharpPlus` brings the generic unqualified `map` into scope. Without it, sequence mapping is explicitly `Seq.map`; after it, `map` dispatches from the argument type:

```fsharp
let before = Seq.map ((+) 1) (seq [1; 2])
open FSharpPlus
let after: seq<int> = map ((+) 1) (seq [1; 2])
```

The open also makes extended `String`, `Option`, `Result`, and collection modules win normal open-resolution over same-named core modules. Thus the F#+-specific `String.split` shape becomes available and `Option.apply` refers to the extended module:

```fsharp
open FSharpPlus
let parts = String.split [","] "a,b"
let applied: int option = Option.apply (Some ((+) 1)) (Some 2)
```

## Fixing ambiguity

Qualify the intended module:

```fsharp
open FSharpPlus
let xs = Microsoft.FSharp.Collections.List.map ((+) 1) [1; 2]
```

Reorder opens so the later module supplies the short name:

```fsharp
open FSharpPlus
open Microsoft.FSharp.Collections
let ys = List.map ((+) 1) [1; 2]
```

Or constrain generic dispatch at the call site:

```fsharp
open FSharpPlus
let incremented: int option = map ((+) 1) (Some 2 : int option)
```
