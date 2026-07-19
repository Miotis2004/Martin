# Statements and control flow

**Specification status:** Normative for Martin `0.1` core statements
**Language version:** `0.1`
**Product version:** `0.1.0-alpha`
**Owner:** Parser and semantic analysis

## Statement forms

Core statement forms are block, variable declaration, expression statement, `if`, `if let`, `while`, and `return`. `switch`, `throw`, and `do`/`catch` syntax is parsed in the core grammar but its full semantics are specified in [enums-and-patterns.md](enums-and-patterns.md) and [typed-errors.md](typed-errors.md).

## Blocks and declaration statements

A block executes statements in order and introduces a nested scope. A variable declaration statement creates either a read-only `let` binding or mutable `var` binding. If an initializer is present, it is evaluated before the binding is available for later statements.

## Conditional control flow

An `if` condition must have type `Bool`. The then block executes when the condition is true. An `else if` is parsed as an `else` whose body is another `if` statement.

`if let name = expression` conditionally unwraps an optional expression. In the then block, `name` is a non-optional read-only binding of the optional element type. If the expression is `nil`, the `else` branch runs when present.

## Loops

A `while` condition must have type `Bool`. The loop body executes repeatedly while the condition evaluates to true. Alpha Martin has no `break` or `continue` statements.

## Return

A `return` statement exits the current function, method, or initializer. A `Void` function may use `return` without an expression. A non-`Void` function must return an expression implicitly convertible to the declared return type along every required path.

## Entry-point behavior

For executable programs, top-level statements are accepted as global statements. A conventional `main` function may also be used by generated executable output. Programs should choose one entry-point style; if the implementation reports competing or missing entry points, the diagnostic is authoritative for alpha.

## Examples

Valid fragment:

```martin
func countdown(from start: Int) {
  var value = start
  while value > 0 {
    print(value)
    value = value - 1
  }
}
```

Valid optional-binding fragment:

```martin
let maybeName: String? = "Martin"
if let name = maybeName {
  print(name)
} else {
  print("missing")
}
```

Invalid fragment:

```martin
if 1 {
  print("not a Bool condition")
}
```

## Related samples

- [Functions](../../samples/Functions) demonstrates `while`, assignments, function calls, and returns.
- [Optionals](../../samples/Optionals) demonstrates `if let` and `else` branches.
- [EnumsAndPatterns](../../samples/EnumsAndPatterns) demonstrates `switch` statements over closed domains.

## Cross-references

- Syntax grammar: [syntax-grammar.md#statements](syntax-grammar.md#statements)
- Types and values: [types-and-values.md](types-and-values.md)
- Optionals: [optionals.md](optionals.md)
- Runtime and standard library: [runtime-and-standard-library.md](runtime-and-standard-library.md)
- Diagnostics: [../diagnostics.md](../diagnostics.md)
