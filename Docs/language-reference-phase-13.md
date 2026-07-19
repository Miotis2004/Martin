# Phase 13 language reference

Phase 13 completes Martin's advanced pattern-matching and explicit protocol-conformance model.

## Patterns and switches

A `switch` evaluates its input once. Cases are tested from top to bottom and do not fall through.
Supported patterns are `_`, literals, enum cases (including recursively nested associated values),
`.some(pattern)`, `nil`, and immutable `let name` bindings. A binding is scoped only to its case body.

```swift
enum Lookup {
    case found(value: Int)
    case missing
}

func display(_ value: Lookup?) {
    switch value {
        case .some(.found(let number)):
            print(number)
        case .some(.missing):
            print(0)
        case nil:
            print(0)
    }
}
```

Enum, optional, nested optional, and Boolean switches must cover their finite domains. Integer,
floating-point, and string domains are open and therefore require `_` or an irrefutable binding.
Duplicate and subsumed cases are rejected, and a non-exhaustive diagnostic lists representative
missing patterns. Lowering builds a backend-neutral decision graph containing constructor/literal
tests, payload extraction, and binding nodes before the C# backend emits control flow; source
locations remain attached throughout lowering.

## Protocols and conformances

Protocols declare method and readable or writable property requirements. A concrete type opts in by
listing protocols after `:`. Matching is exact across member kind and name, argument count and labels,
parameter and return types, mutation, throwing and error type, property writability, and supported
generic metadata.

```swift
protocol Resettable {
    var name: String
    mutating func reset() throws ResetError
}

struct Job: Resettable {
    var name: String

    mutating func reset() throws ResetError {
        print(name)
    }
}
```

Successful validation creates an explicit conformance record with requirement-to-witness and reverse
witness-to-requirement maps. Cross-file declarations participate in the same compilation. The C#
backend emits deterministic interfaces and bridge members only for validated conformances; bridges
are implementation details and are not Martin symbols.

## Deferred features

The following are intentionally outside Phase 13: mutable `var` bindings, range, tuple (outside enum
payloads), or-patterns, type-cast and reflection patterns; default or static protocol requirements;
protocol existential values; and complete generic constraints/dispatch. Generic completion belongs to
Phase 14, while typed-error runtime completion and catch-pattern expansion belong to Phase 15.

## Formatting

The formatter places enum cases, switch cases and bodies, protocol requirements, and conforming type
members on stable indented lines. It preserves token text, comments, skipped text, and the detected line
ending convention, refuses unsafe malformed documents, and is idempotent. See
[`formatter-safety.md`](formatter-safety.md) for the edit-safety contract and
[`diagnostics.md`](diagnostics.md) for diagnostic families.
