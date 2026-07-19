# Phase 14 Generic Language Reference

**Status:** Milestone 16 formatting and documentation complete  
**Language version:** `0.1.0-alpha`

This reference records the generic rules implemented by Phase 14. The compiler's
bound symbols and semantic model are authoritative; the generated C# is only a
backend representation.

## Declarations and constructed types

Structs, classes, enums, top-level functions, methods, and protocol method
requirements may declare ordered type parameters. A parameter may have one
protocol constraint.

```martin
protocol Printable {
    func text() -> String
}

struct Box<T: Printable> {
    let value: T
}

func identity<T>(_ value: T) -> T {
    return value
}
```

A generic definition is not itself a closed value type. Every use outside its
declaration supplies the exact number of arguments, explicitly or through call
inference. Construction is written `Box<Int>(value: 42)`, and explicit function
calls are written `identity<Int>(42)`. Type arguments may be nested and optional:
`Box<Result<String, FileError?>>`.

Within one compilation, the same definition and ordered type arguments denote
the same constructed type. Argument order is significant. A constructed type
retains its original definition and substitutes its properties, methods,
initializers, enum payloads, returns, thrown-error types, and nested type shapes.

## Call inference

Omitting function or method type arguments requests basic inference. Phase 14
collects exact candidates from:

- direct parameter positions (`T`);
- nested optional positions (`T?`);
- corresponding positions in constructed types (`Box<T>`); and
- a simple expected return type when one is available.

All type parameters must be resolved. Different candidates for one parameter are
an error; Martin does not choose a common supertype. Constraints validate the
result after inference and do not invent a type argument.

Inference intentionally does **not** include overload-directed search, default
arguments, variadic packs, higher-kinded types, variance, arbitrary same-type
equations, user-defined conversions, or return-only inference without a usable
expected type. Constructor calls require an explicit constructed type; constructor
inference and diamond syntax are not part of Phase 14.

## Constraints

`T: ProtocolName` is the supported constraint form. The name must bind to a
protocol, and a concrete type argument satisfies the constraint only when the
compilation contains the required conformance record. Structural similarity is
not conformance. A type parameter passed onward must declare a constraint that
proves the required compatibility.

Constraint checking occurs after arity, inference, and substitution. Constrained
member lookup exposes only members represented by the protocol requirements.
Runtime protocol existentials, associated types, conditional conformances,
generic extensions, and generic protocol types remain deferred.

## Generic enums and patterns

Enum cases are obtained from the generic definition, while associated-value
types are substituted from the constructed enum. Pattern bindings therefore
receive concrete payload types, and exhaustiveness still ranges over every case
in the definition.

```martin
enum Result<Value, Failure> {
    case success(value: Value)
    case failure(error: Failure)
}
```

## Formatting

Generic parameter and argument delimiters are adjacent to their names:
`Box<T>`, not `Box < T >`. Commas use one following space and constraints use one
space after `:`. Nested closers and optional markers stay adjacent, as in
`Box<Result<T, E?>>`. The syntax-aware formatter distinguishes these delimiters
from comparison operators, so `left < right` retains operator spacing. As with
all safe formatting, comments, token text, skipped text, and detected line
endings are preserved; unsafe malformed input is refused. Reformatting formatted
generic source produces no further changes.

## Diagnostics and deferred features

Generic diagnostics are catalogued in [diagnostics.md](diagnostics.md). Historical
`MRT2180`-series codes retain their meanings, while Phase 14 additions use the
reserved `MRT2300`-`MRT2399` range. Invalid or incomplete generic syntax remains
navigable through missing tokens and error symbols where safe.

The following are not implied by Phase 14 completion: default or variadic type
arguments, variance, higher-kinded types, specialization, generic aliases,
generic extensions, conditional conformances, associated types, runtime
existentials, reflection-based construction, and typed-error runtime completion.
Typed error types in generic signatures are preserved and substituted, but their
runtime semantics belong to Phase 15.
