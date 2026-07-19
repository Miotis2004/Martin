# Protocols

**Specification status:** Normative alpha specification
**Language version:** `0.1`
**Product version:** `0.1.0-alpha`
**Owner:** Protocol system

## Scope

This chapter specifies explicit protocol declarations, conformances, requirements, and witnesses in Martin `0.1`.

## Protocol declarations

A protocol declaration introduces a named set of requirements.

```ebnf
protocol-declaration ::= 'protocol' identifier generic-parameters? protocol-body
protocol-body        ::= '{' protocol-requirement* '}'
protocol-requirement ::= function-signature | initializer-signature
```

Protocol requirements in the alpha language are method and initializer signatures. A requirement records member kind, name, generic metadata, argument labels, parameter types, return type, mutating status, throwing status, and thrown error type.

```martin
protocol Resettable {
    mutating func reset()
}
```

## Explicit conformance

A nominal type conforms to a protocol only by listing the protocol in its inheritance clause.

```martin
struct Job: Resettable {
    var attempts: Int

    mutating func reset() {
        attempts = 0
    }
}
```

The compiler validates conformance at the declaration. Structural matching without an explicit conformance clause is not supported.

## Witness matching

A witness must match the corresponding requirement after all generic substitutions are applied. Matching is exact for:

- member kind;
- name;
- argument count and labels;
- parameter types;
- return type;
- mutating status; and
- throwing status and thrown error type.

A nonthrowing witness does not satisfy a throwing requirement in Martin `0.1`; the witness must have the same throwing effect after substitution.

## Built-in `Error` protocol

The compiler provides a built-in `Error` protocol used by typed errors. It has stable compiler-owned identity. Error types must explicitly conform to `Error` unless another rule in [typed-errors.md](typed-errors.md#error-types) permits them.

## Protocol-constrained generics

A generic type parameter may be constrained to a protocol. Within a constrained generic body, member lookup on that parameter exposes only the protocol requirements. Constraint satisfaction uses explicit conformance of the concrete type argument.

```martin
protocol Printable {
    func text() -> String
}

func describe<T: Printable>(_ value: T) -> String {
    return value.text()
}
```

## Related samples

- [Protocols](../../samples/Protocols) demonstrates explicit conformance, method witnesses, and mutating requirements.
- [Generics](../../samples/Generics) demonstrates protocol-constrained generic calls.

## Deferred protocol features

The following protocol features are deferred:

- Protocol existential values.
- Associated types.
- Default implementations.
- Static requirements.
- Protocol inheritance.
- Conditional conformances.
- Generic protocol types as runtime values.
- Dynamic dispatch beyond the compiler-supported constrained-call model.
