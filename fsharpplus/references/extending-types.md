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
- Call `map`, `lift2`, and `bind` from `FSharpPlus` inside an instance body. `Map.Invoke` and the other dispatchers live in `FSharpPlus.Control`, which `open FSharpPlus` does not bring into scope.

## Traverse on a recursive type

`Traverse` must be `static member inline` because the applicative is a type parameter. Rebuild the structure with `map` for leaves and `lift2` for nodes:

```fsharp
#r "nuget: FSharpPlus, 1.9.1"
open FSharpPlus

type Tree<'T> =
    | Leaf of 'T
    | Node of Tree<'T> * Tree<'T>
    static member Map (source: Tree<'T>, mapping: 'T -> 'U) : Tree<'U> =
        match source with
        | Leaf value -> Leaf (mapping value)
        | Node (left, right) -> Node (Tree.Map (left, mapping), Tree.Map (right, mapping))
    static member inline Traverse (source: Tree<'T>, mapping: 'T -> '``Functor<'U>``) : '``Functor<Tree<'U>>`` =
        let rec go tree =
            match tree with
            | Leaf value -> map Leaf (mapping value)
            | Node (left, right) -> lift2 (fun l r -> Node (l, r)) (go left) (go right)
        go source

let positive x = if x > 0 then Some x else None
let mapped: Tree<int> = map ((+) 1) (Node (Leaf 1, Leaf 2))
let allPositive: Tree<int> option = traverse positive (Node (Leaf 1, Leaf 2))
let onePositive: Tree<int> option = traverse positive (Node (Leaf 1, Leaf -2))
printfn "%A" mapped
// expect: Node (Leaf 2, Leaf 3)
printfn "%A" allPositive
// expect: Some (Node (Leaf 1, Leaf 2))
printfn "%A" onePositive
// expect: None
```
