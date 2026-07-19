# Expressions and precedence

**Specification status:** Normative for Martin `0.1` core expressions
**Language version:** `0.1`
**Product version:** `0.1.0-alpha`
**Owner:** Parser and semantic analysis

## Expression forms

Core expressions include literals, identifiers, `self`, parenthesized expressions, unary expressions, binary expressions, assignments, member access, calls, explicit generic call type arguments, and `try` expressions. Object creation, enum case construction, optional operations, protocol calls, generics, and typed errors are completed in their feature chapters.

## Precedence and associativity

From highest to lowest precedence:

| Precedence | Operators/forms | Associativity |
|---:|---|---|
| 8 | calls, member access, explicit type arguments | left |
| 7 | unary `!`, unary `+`, unary `-`, `try` operand binding | right for repeated unary parsing |
| 6 | `*`, `/`, `%` | left |
| 5 | `+`, `-` | left |
| 4 | `<`, `<=`, `>`, `>=` | left |
| 3 | `==`, `!=` | left |
| 2 | `&&` | left |
| 1 | `||` | left |
| 0 | assignment `=` | right |

Parentheses override precedence.

## Operators

Unary `+` and `-` are numeric operators. Unary `!` is a Boolean logical-negation operator. Arithmetic operators are defined for numeric operands, with `+` also supporting string concatenation where both operands are `String`. Relational operators compare supported numeric operands. `&&` and `||` require `Bool` operands and produce `Bool`.

`==` and `!=` require an equality-supported operand pair. Optional equality with `nil` is specified by [optionals.md](optionals.md).

## Assignment

The left side of `=` must be an assignable variable or property target. Assignment converts the right expression to the target type when an implicit conversion exists. Assignment does not create a new declaration.

## Calls and labels

A call expression resolves to a function, method, initializer, built-in, or enum case candidate. The number of arguments, labels, and implicit conversions must match the selected callable. Explicit generic type arguments are permitted only on generic callables and generic type construction forms.

## Examples

Valid fragment:

```martin
let result = 1 + 2 * 3
let ok = result >= 7 && true
var text = "Martin"
text = text + " alpha"
```

Invalid fragment:

```martin
let bad = true + 1
let alsoBad = "x" < "y"
```

## Cross-references

- Syntax grammar: [syntax-grammar.md](syntax-grammar.md#expressions)
- Types and values: [types-and-values.md](types-and-values.md)
- Generics: [generics.md](generics.md)
- Typed errors: [typed-errors.md](typed-errors.md)
- Diagnostics: [../diagnostics.md](../diagnostics.md)
