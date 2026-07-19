# Generics

**Specification status:** Normative alpha specification
**Language version:** `0.1`
**Product version:** `0.1.0-alpha`
**Owner:** Generic type system

## Scope

This chapter specifies generic declarations, constructed identities, inference, constraints, and substitution in Martin `0.1`.

## Generic parameters

Structs, classes, enums, top-level functions, methods, initializers, and protocol method requirements may declare generic parameters. A parameter may have a protocol constraint.

```ebnf
generic-parameters ::= '<' generic-parameter (',' generic-parameter)* '>'
generic-parameter  ::= identifier (':' type)?
```

Generic parameter names are scoped to the declaration. Names must be unique within the parameter list.

```martin
protocol Printable {
    func text() -> String
}

struct Box<T: Printable> {
    let value: T
}
```

## Constructed types

A generic type definition is not itself a closed value type. A use outside its declaration body must provide type arguments or appear in a context where inference is explicitly supported.

```martin
let box: Box<Name> = Box<Name>(value: name)
```

Constructed type identity includes the generic definition and the ordered list of type arguments. `Pair<Int, String>` and `Pair<String, Int>` are distinct types. Repeated construction of the same definition with the same arguments denotes the same semantic type.

Constructed types may be nested and optional, for example `Box<Result<Int, FileError>?>`.

## Generic functions and inference

Generic functions may be called with explicit type arguments, such as `identity<Int>(1)`. The compiler also infers type arguments from supported positions:

- explicit parameter annotations;
- argument expression types;
- expected return type;
- nested optional positions;
- corresponding positions in constructed types; and
- enum associated-value payload positions.

Constructor calls for generic nominal types require a constructed type when inference cannot be derived from a declared expected type.

## Constraints

A constrained type parameter may be used where the constraint's protocol requirements are available. A concrete type argument satisfies a protocol constraint only when it has an explicit, validated conformance to that protocol.

Constraint checking happens after type-argument inference or explicit type-argument parsing. Failed constraints are compile-time errors.

## Substitution

Substitution replaces every occurrence of a generic parameter in a declaration's signature and body-facing member metadata with the corresponding type argument. Substitution applies to:

- stored property types;
- initializer parameters;
- function parameters and return types;
- method receiver type;
- enum associated-value payloads;
- protocol requirement witnesses;
- optional element types;
- constructed nested types; and
- thrown error types.

Substitution must preserve constructed identities rather than comparing display names or generated backend names.

## Generic enums and patterns

Patterns over generic enums use cases from the generic definition, then match associated payloads after substituting type arguments from the input type.

```martin
enum Result<Value, Failure> {
    case success(Value)
    case failure(Failure)
}

let value: Result<Int, String> = .success(1)

switch value {
case .success(let number):
    print(number)
case .failure(let message):
    print(message)
}
```

In the example, `number` has type `Int` and `message` has type `String`.

## Typed errors in generic signatures

Generic thrown error types participate in the same substitution model. If a callable declares `throws E` and `E` is a type parameter constrained to `Error`, the effect identity at a call site is the substituted `E` type for that constructed callable.

## Related samples

- [Generics](../../samples/Generics) demonstrates generic structs, generic functions, inferred type arguments, protocol constraints, and constructed types.
- [TypedErrors](../../samples/TypedErrors) demonstrates a generic error enum used as a thrown type.

## Deferred generic features

The following generic features are deferred:

- Default type arguments.
- Variance annotations.
- Higher-kinded types.
- Type aliases.
- Generic extensions.
- Conditional conformances.
- Associated types.
- Runtime generic existentials.
- Reflection-based generic construction.
- Specialization promises or ABI stability.
