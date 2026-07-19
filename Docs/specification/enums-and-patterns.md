# Enums and patterns

**Specification status:** Normative alpha specification
**Language version:** `0.1`
**Product version:** `0.1.0-alpha`
**Owner:** Pattern matching

## Scope

This chapter specifies enums, associated values, patterns, switch usefulness, and exhaustiveness in Martin `0.1`.

## Enum declarations

An enum declaration introduces a closed nominal sum type.

```ebnf
enum-declaration ::= 'enum' identifier generic-parameters? inheritance-clause? enum-body
enum-body        ::= '{' enum-member* '}'
enum-member      ::= enum-case | function-declaration | initializer-declaration
enum-case        ::= 'case' identifier associated-values?
associated-values ::= '(' parameter-list? ')'
```

Each case name must be unique within its enum. A case with no associated values is a singleton value. A case with associated values is constructed with arguments matching the case payload shape.

```martin
enum Lookup {
    case found(String)
    case missing
}
```

Enum conformance to protocols, including `Error`, uses the same explicit conformance model as structs and classes.

## Case construction and identity

A constructed enum value preserves its enum type, generic substitutions, case identity, and associated payload values. Generic enum cases are obtained from the generic definition while their payload types are substituted from the constructed enum type.

```martin
enum Result<Value, Failure> {
    case success(Value)
    case failure(Failure)
}

let result: Result<Int, String> = .success(1)
```

The shorthand `.caseName` form is valid only when the expected enum type is known.

## Pattern forms

Martin `0.1` supports the following pattern forms:

```ebnf
pattern          ::= wildcard-pattern
                   | binding-pattern
                   | literal-pattern
                   | enum-case-pattern
                   | optional-some-pattern
                   | nil-pattern
wildcard-pattern ::= '_'
binding-pattern  ::= 'let' identifier
literal-pattern  ::= integer-literal | floating-literal | string-literal | boolean-literal
enum-case-pattern ::= '.' identifier associated-patterns?
associated-patterns ::= '(' pattern (',' pattern)* ')'
optional-some-pattern ::= '.some' '(' pattern ')'
nil-pattern      ::= 'nil'
```

A `let` pattern introduces an immutable binding scoped to the selected case body. Pattern variables must not leak to later cases or the surrounding scope.

## Switch semantics

A `switch` evaluates its subject exactly once. Cases are tested in source order and do not fall through. The first matching case executes. A case body may contain ordinary statements and may return, throw, or continue control flow according to the statement rules.

```martin
switch result {
case .success(let value):
    print(value)
case .failure(let message):
    print(message)
}
```

## Type compatibility

A pattern is valid only when it can match the input type. Enum case patterns must name a case of the input enum after generic substitution. Associated-value subpatterns must match the substituted payload types. Literal patterns must be compatible with the input literal type. `nil` and `.some(...)` require optional input.

## Usefulness and exhaustiveness

The compiler performs usefulness analysis in source order. A case that can never be selected because earlier cases cover all of its values is unreachable and is diagnosed.

Switches over closed finite domains must be exhaustive. This includes enums, `Bool`, optionals, and nested combinations of those domains. Integer, floating-point, and string switches require an irrefutable pattern such as `_` or `let name` to be exhaustive.

For enum and optional inputs, diagnostics should describe missing cases or representative missing patterns when practical.

## Related samples

- [EnumsAndPatterns](../../samples/EnumsAndPatterns) demonstrates associated values, nested optional patterns, and exhaustive `switch`.
- [TypedErrors](../../samples/TypedErrors) demonstrates enum payload matching in `catch` clauses.

## Deferred pattern features

The following pattern forms are not part of Martin `0.1`:

- Mutable `var` bindings.
- Or-patterns.
- Range patterns.
- Tuple patterns outside enum payload structure.
- Type-cast, reflection, and property patterns.
- `where` guards on cases.
- Fallthrough between switch cases.
