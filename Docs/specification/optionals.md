# Optionals

**Specification status:** Normative alpha specification
**Language version:** `0.1`
**Product version:** `0.1.0-alpha`
**Owner:** Type system

## Scope

This chapter specifies optional types, `nil`, optional injection, and conditional binding in Martin `0.1`.

## Optional type syntax

An optional type is written by appending `?` to another type.

```ebnf
optional-type ::= type '?'
```

The optional marker binds to the immediately preceding type. Nested optionals are represented by repeated markers, for example `Int??`. Optional markers may also appear around constructed generic types, for example `Box<Int>?`.

## Values

For every type `T`, `T?` has two forms:

- a present value containing a `T`; and
- `nil`, representing absence.

`nil` has no standalone type. It is valid only when the expected type is optional or can be inferred as optional from the assignment, argument, return, payload, or pattern context.

```martin
let name: String? = nil
let other: String? = "Martin"
```

## Optional injection

A value of type `T` may be used where `T?` is expected. The compiler injects the value as a present optional. Injection does not recursively flatten nested optionals unless the expected type requires exactly that shape.

```martin
let count: Int? = 3
let nested: Int?? = count
```

## Conditional binding

`if let` unwraps a present optional and binds an immutable local for the true branch. The binding is scoped only to that branch.

```martin
let value: Int? = 5

if let actual = value {
    print(actual)
}
```

The initializer expression of a conditional binding must have optional type. The bound name has the wrapped type. `var` conditional bindings are deferred.

## Optional patterns

Switch and catch pattern positions may use `nil` and `.some(pattern)` when the input type is optional. Optional pattern coverage is finite: an exhaustive switch over `T?` must cover `nil` and all present values accepted by `.some(...)` or an irrefutable pattern.

```martin
switch value {
case nil:
    print("missing")
case .some(let actual):
    print(actual)
}
```

Pattern coverage and usefulness are specified in [enums-and-patterns.md](enums-and-patterns.md).

## Operations

Martin `0.1` does not provide optional chaining, force unwrap, nil coalescing, or map/flatMap library helpers as language features. A program must unwrap an optional through conditional binding or pattern matching before using the contained value as `T`.

## Related samples

- [Optionals](../../samples/Optionals) demonstrates optional values, `nil`, and conditional binding.
- [EnumsAndPatterns](../../samples/EnumsAndPatterns) demonstrates optional subpatterns in an exhaustive `switch`.

## Deferred features

The following optional features are deferred:

- Force unwrap (`!`).
- Optional chaining.
- Nil-coalescing operators.
- `guard let`.
- Mutable conditional bindings.
- Implicit truthiness for optionals.
