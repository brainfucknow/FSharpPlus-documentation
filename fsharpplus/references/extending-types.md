# Extending custom types

The instance-member shapes below follow the tutorial and the dispatchers in `FSharpPlus.Control`. This complete wrapper compiles as one unit:

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus

type Box<'T> = Box of 'T with
    static member Map (Box value: Box<'T>, mapping: 'T -> 'U) : Box<'U> =
        Box (mapping value)
    static member Return (value: 'T) : Box<'T> = Box value
    static member (<*>) (Box mapping: Box<'T -> 'U>, Box value: Box<'T>) : Box<'U> =
        Box (mapping value)
    static member (>>=) (Box value: Box<'T>, binder: 'T -> Box<'U>) : Box<'U> =
        binder value
    static member ToSeq (Box value: Box<'T>) : seq<'T> = Seq.singleton value

let mapped: Box<int> = map ((+) 1) (Box 1)
let returned: Box<int> = result 2
let applied: Box<int> = Box ((+) 1) <*> Box 2
let bound: Box<int> = bind (fun x -> Box (x * 2)) (Box 3)
let elements: seq<int> = toSeq (Box 4)
```

## Steps and exact shapes

1. Functor support is `static member Map (Box value: Box<'T>, mapping: 'T -> 'U) : Box<'U>`. The source comes before the mapping inside a tuple.
2. Applicative return is `static member Return (value: 'T) : Box<'T>` and application is `static member (<*>) (Box mapping: Box<'T -> 'U>, Box value: Box<'T>) : Box<'U>`.
3. Monad support is `static member (>>=) (Box value: Box<'T>, binder: 'T -> Box<'U>) : Box<'U>`.
4. Minimal foldable conversion is `static member ToSeq (Box value: Box<'T>) : seq<'T>`; generic folds can fall back through it.

The member name and shape are the SRTP protocol: changing arity, order, or tupled form prevents resolution even when the implementation looks equivalent.

## Resolution checklist

- Put members on the type, make them `static member`, and keep the type public enough for the caller.
- Compare capitalization, operator spelling, argument order, tuple form, and result annotation with a compiled signature.
- Mark a generic wrapper function `inline`; otherwise SRTP constraints cannot specialize at its call site.
- Annotate the source and expected result at the generic call.
- Reduce to one operation (`map`, then `result`/`(<*>)`, then `bind`, then `toSeq`) and compile after each addition.
- For `traverse`, implement the documented `Traverse (source, mapping)` shape rather than assuming `Map` plus `ToSeq` preserves the custom container.
