# Types and values

**Specification status:** Normative for Martin `0.1` core semantic rules
**Language version:** `0.1`
**Product version:** `0.1.0-alpha`
**Owner:** Semantic analysis

## Type universe

Martin `0.1` has these built-in core types: `Void`, `Bool`, `Int`, `Double`, `String`, and `Nil`. `Nil` is the type of the `nil` literal and is not a general value type for variable declarations except through optional conversion. Optional, nominal, enum, protocol, generic, and typed-error types are specified in their feature chapters.

## Values

`Bool` has values `true` and `false`. `Int` values come from decimal integer literals within the alpha implementation range. `Double` values come from decimal floating-point literals. `String` values are immutable text values produced by string literals and string operations. `Void` is the result type of functions and statements that do not produce a value.

## Type annotations and inference

A local or global `let` or `var` declaration may provide a type annotation, an initializer, or both. A declaration without an annotation must have an initializer whose type can be determined. When both are present, the initializer must be implicitly convertible to the annotated type.

Function parameters and explicit return types must name defined types. If a function omits `->`, its return type is `Void`.

## Conversions

The alpha core language defines these implicit conversions:

- identity conversion from a type to itself;
- `Int` to `Double` numeric widening;
- `Nil` to an optional type;
- a value of `T` to `T?` where optional semantics permit wrapping.

No implicit conversion exists from `Double` to `Int`, from `String` to a numeric type, or from `Bool` to a numeric type.

## Mutability and initialization

`let` declares a read-only binding. `var` declares a mutable binding. Parameters are read-only. Assignment to a `let` binding, parameter, or read-only property is invalid.

A declaration's value must be definitely initialized before it is read. A `let` binding may be initialized by its declaration initializer. User-defined property initialization and initializer rules are specified in [structs-and-classes.md](structs-and-classes.md).

## Equality and numeric behavior

`==` and `!=` are available only for supported operand type pairs. Numeric binary operations on mixed `Int` and `Double` operands convert the `Int` operand to `Double`. Integer division follows the host integer division semantics used by generated alpha code; division by zero is an unchecked host failure unless separately diagnosed by a future implementation.

## Examples

Valid fragment:

```martin
let count: Int = 3
let widened: Double = count
var name = "Martin"
name = name + " alpha"
```

Invalid fragment:

```martin
let count = 3
count = 4 // immutable binding
let text: String = 4 // no Int-to-String conversion
```

## Cross-references

- Syntax grammar: [syntax-grammar.md](syntax-grammar.md)
- Expressions: [expressions.md](expressions.md)
- Optionals: [optionals.md](optionals.md)
- Implementation limits: [implementation-limits.md](implementation-limits.md)
- Diagnostics: [../diagnostics.md](../diagnostics.md)
