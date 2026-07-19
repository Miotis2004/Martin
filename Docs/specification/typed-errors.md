# Typed errors

**Specification status:** Normative alpha specification
**Language version:** `0.1`
**Product version:** `0.1.0-alpha`
**Owner:** Effects and diagnostics

## Scope

This chapter specifies Martin `0.1` typed errors: error types, throwing declarations, `throw`, `try`, propagation, exhaustive `do`-`catch`, catch patterns, and runtime transport.

## Error types

The compiler provides a built-in `Error` protocol. A thrown error type must conform to `Error`. Enums are the primary error type because their closed case set supports exhaustive catch analysis.

```martin
enum FileError: Error {
    case missing(String)
    case denied(String)
}
```

Struct and class error types are permitted when they explicitly conform to `Error`, but their value space is open; exhaustive catches for open error types generally require a wildcard pattern.

A constructed generic error type is valid when its generic definition has a supported explicit `Error` conformance and its type arguments satisfy their constraints.

## Throwing declarations

Functions, methods, initializers, and protocol method requirements may declare a thrown error type.

```ebnf
throws-clause ::= 'throws' type
```

```martin
func load(_ path: String) throws FileError -> String {
    throw FileError.missing(path)
}
```

Throwing status and thrown error type are part of callable identity for protocol witness matching and generic substitution.

## `throw`

`throw expression` exits the current control path with the expression's error value. The expression type must be compatible with the enclosing callable's declared thrown error type after substitution. A nonthrowing callable must not contain an unhandled `throw`.

## `try`

Every call to a throwing function, method, initializer, or protocol requirement must be preceded by `try`, even when the call appears inside a `do` block. `try` acknowledges the effect; it does not consume the effect.

```martin
do {
    let text = try load("input.txt")
    print(text)
} catch .missing(let path) {
    print(path)
} catch .denied(let path) {
    print(path)
}
```

## Propagation

An acknowledged effect may propagate out of an enclosing callable only when that callable declares the exact same thrown error type after generic substitution. Martin `0.1` does not form implicit error unions and does not widen a concrete error to an arbitrary generic `E: Error` unless the call site's exact substituted type is `E`.

If an expression or block contains distinct unhandled error types, the compiler reports a mixed-effect diagnostic rather than choosing one.

## `do`-`catch`

A `do`-`catch` statement contains a body and one or more catch clauses.

```ebnf
do-catch-statement ::= 'do' block catch-clause+
catch-clause       ::= 'catch' pattern block
```

The `do` body is analyzed for a single exact unhandled error effect. Catch patterns are bound against that error type, in source order, using the same pattern language as `switch`. Pattern variables are scoped only to their catch body.

The catch list must be exhaustive for the body's error type. For closed enum error types, all cases must be covered. For open error types, a wildcard or other irrefutable pattern is required. Unreachable catches are diagnosed.

A catch body may itself perform throwing operations. Those effects are independent of the effect being handled and must be caught by a nested `do`-`catch` or propagated by the containing callable.

## Runtime transport

The generated backend transports Martin error values through a Martin-specific non-generic exception carrier. The carrier preserves the original Martin error value, declared error type metadata, enum case identity, associated payload values, and constructed generic identity. Catch dispatch uses the compiler-produced pattern decision graph and executes exactly one selected catch body.

The runtime carrier is an implementation boundary; Martin programs observe typed error matching and payload binding, not the backend exception class.

## Generic typed errors

Generic thrown error types are substituted through the generic type system. Constructed error identity must be preserved exactly.

```martin
enum DecodeError<Value>: Error {
    case invalid(Value)
}

func fail<T>(_ value: T) throws DecodeError<T> {
    throw DecodeError<T>.invalid(value)
}
```

At `fail<Int>`, the thrown type is exactly `DecodeError<Int>`.

## Related samples

- [TypedErrors](../../samples/TypedErrors) demonstrates an error enum, a throwing method, `try`, exhaustive `do`/`catch`, and payload binding.

## Deferred typed-error features

The following features are deferred:

- Error unions.
- `try?` and `try!`.
- `rethrows`.
- Partial catch propagation.
- Catch `where` clauses.
- Asynchronous typed errors.
- Throwing property accessors.
- Implicit conversion from one concrete error type to another.
