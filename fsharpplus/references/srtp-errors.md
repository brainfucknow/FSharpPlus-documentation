# SRTP error guide

The failing snippets below were run separately with .NET 8 FSI and the pinned package. Diagnostics omit the temporary filename and the long remainder of overload lists. Each corrected snippet was also run separately and passed.

## 1. No `Map` instance

**Intentionally failing:**

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
type Plain = Plain of int
let x = map id (Plain 1)
```

Captured text:

```text
error FS0071: Type constraint mismatch when applying the default type 'obj' for a type inference variable. No overloads match for method 'Map'.
Known return type: obj
Known type parameters: < Plain * (obj -> obj) , Control.Map >
```

`Plain` supplies neither a known overload nor the static `Map` protocol. Use ordinary construction for monomorphic code, or add the member described in `extending-types.md`.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
type Plain = Plain of int
let x = Plain 1 |> fun (Plain value) -> Plain (value + 1)
```

## 2. Return type is not determined

**Intentionally failing:**

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
let x = result 1
```

Captured text:

```text
error FS0030: Value restriction: The value 'x' has an inferred generic type
    val x: '_a when (Control.Return or '_a) : (static member Return: '_a * Control.Return -> (int -> '_a))
However, values cannot have generic type variables like '_a in "let x: '_a".
```

The compiler cannot choose the result container. This is the current FSI rendering of the insufficient-information/“unique overload could not be determined” error class; annotate the result at the call site.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
let x: int option = result 1
```

## 3. Top-level partial generic application

**Intentionally failing:**

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
let mapper = map ((+) 1)
```

Captured text:

```text
error FS0030: Value restriction: The value 'mapper' has an inferred generic function type
    val mapper: ('_a -> '_b) when (Control.Map or '_a or '_b) : (static member Map: ('_a * (int -> int)) * Control.Map -> '_b)
However, values cannot have generic type variables like '_a in "let f: '_a".
```

FSI tries to store an unresolved SRTP specialization as a value. Eta-expand it and annotate the parameter (or make an intentional `inline` generic function).

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
let mapper (source: int list) = map ((+) 1) source
```

## 4. Null leaves both input and output unresolved

**Intentionally failing:**

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
let x = map id null
```

Captured text:

```text
error FS0030: Value restriction: The value 'x' has an inferred generic type
    val x: '_a when (Control.Map or '_b or '_a) : (static member Map: ('_b * ('_c -> '_c)) * Control.Map -> '_a) and '_b: null
However, values cannot have generic type variables like '_a in "let x: '_a".
```

Neither side constrains the dispatcher. Annotate the argument at the call site; the result then follows.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
let x: string = map id (null: string)
```

## 5. A same-named operator from another open

**Intentionally failing:**

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
module Other =
    let (<*>) (f: int -> int) (x: int) = f x
open FSharpPlus
open Other
let x: int option = ((+) 1) <*> Some 2
```

Captured text:

```text
error FS0001: This expression was expected to have type
    'int option'
but here has type
    'int'
```

The later `open Other` selects its monomorphic operator; this is name shadowing, not a missing applicative instance. Qualify the F#+ operator (and see `namespaces-and-shadowing.md`).

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
let x: int option = FSharpPlus.Operators.(<*>) (Some ((+) 1)) (Some 2)
```

## 6. Annotated `let!` pattern inside a transformer CE

**Intentionally failing:**

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
open FSharpPlus.Data
type Config = { BaseUrl: string }
let url (path: string) : ReaderT<Config, Result<string, string>> = monad {
    let! (config: Config) = ask
    return config.BaseUrl + "/" + path }
```

Captured text:

```text
error FS0071: Type constraint mismatch when applying the default type 'obj' for a type inference variable. No overloads match for method 'op_GreaterGreaterEquals'.
Known return type: Result<string,string>
Known type parameters: < obj , (Config -> Result<string,string>) >
```

The pattern annotation fixes the bound value but leaves the monad of `ask` unresolved, so `>>=` defaults to `obj`. The return annotation on `url` already carries the whole stack; remove the pattern annotation and let the builder infer from it.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
open FSharpPlus.Data
type Config = { BaseUrl: string }
let url (path: string) : ReaderT<Config, Result<string, string>> = monad {
    let! config = ask
    return config.BaseUrl + "/" + path }
let outcome: Result<string, string> = ReaderT.run (url "users") { BaseUrl = "https://example.com" }
printfn "%A" outcome
// expect: Ok "https://example.com/users"
```

## 7. `monad` over a custom type without `Delay`

**Intentionally failing:**

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
type Box<'T> = Box of 'T with
    static member Return (value: 'T) : Box<'T> = Box value
    static member (>>=) (Box value: Box<'T>, binder: 'T -> Box<'U>) : Box<'U> = binder value
let summed: Box<int> = monad {
    let! x = Box 1
    return x + 1 }
```

Captured text:

```text
error FS0073: internal error: Undefined or unsolved type variable: ^_?106199
```

The location is reported as `unknown(1,1)`, which makes this hard to trace. The delayed `monad` builder wraps the body in `Delay`, and with only an annotated `(>>=)` available the fallback leaves a type variable unsolved. Add `Delay` to the type, or use the strict builder `monad'`, which does not delay.

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus
type Box<'T> = Box of 'T with
    static member Return (value: 'T) : Box<'T> = Box value
    static member (>>=) (Box value: Box<'T>, binder: 'T -> Box<'U>) : Box<'U> = binder value
    static member Delay (body: unit -> Box<'T>) : Box<'T> = body ()
let delayed: Box<int> = monad {
    let! x = Box 1
    return x + 1 }
let strict: Box<int> = monad' {
    let! x = Box 1
    return x + 1 }
printfn "%A %A" delayed strict
// expect: Box 2 Box 2
```
