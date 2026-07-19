# Declarations, names, and scopes

**Specification status:** Normative for Martin `0.1` core declarations
**Language version:** `0.1`
**Product version:** `0.1.0-alpha`
**Owner:** Semantic analysis

## Declaration kinds

The core language supports function declarations, global variable declarations through global statements, local variable declarations, parameters, and nominal type declarations. Struct, class, enum, protocol, generic, and typed-error declaration details are specified in their dedicated chapters.

## Name binding

Names are matched exactly and are case-sensitive. A use of an identifier must resolve to a declared symbol in scope, a built-in type, a built-in function, a member reachable from a receiver, or an enum case form described by the advanced chapters.

## Scopes

A compilation unit has a global scope containing built-in types, built-in functions, top-level functions, nominal types, and global variables. Function bodies, blocks, `if`, `while`, `do`, `catch`, and `switch` bodies create nested statement scopes as required by their binding rules. Function parameters are in scope throughout the function body.

A local declaration is visible after it is declared within its containing block. Redeclaring a name in the same scope is invalid. Shadowing an outer-scope name in a nested scope is permitted unless a feature-specific chapter imposes a stricter rule.

## Functions and parameters

A function name plus parameter shape identifies callable candidates for overload resolution. Calls must supply the required number of arguments and must match parameter labels. A parameter written with two identifiers uses the first identifier as the external label and the second as the local name. A parameter written with one identifier has no external label.

## Entry-point behavior

Executable source may use global statements or a function named `main` according to [statements.md](statements.md#entry-point-behavior). A program must not depend on multiple competing entry points.

## Examples

Valid fragment:

```martin
func greet(name: String) {
  print(name)
}

func demo() {
  let name = "Martin"
  greet(name)
}
```

Valid shadowing fragment:

```martin
let value = 1
if true {
  let value = 2
  print(value)
}
```

Invalid fragment:

```martin
func demo() {
  let value = 1
  let value = 2 // duplicate in same scope
}
```

## Related samples

- [HelloMartin](../../samples/HelloMartin) uses a conventional `main` function and top-level package declarations.
- [Functions](../../samples/Functions) demonstrates function declarations, parameter labels, and local bindings.

## Cross-references

- Syntax grammar: [syntax-grammar.md](syntax-grammar.md)
- Types and values: [types-and-values.md](types-and-values.md)
- Runtime and built-ins: [runtime-and-standard-library.md](runtime-and-standard-library.md)
- Diagnostics: [../diagnostics.md](../diagnostics.md)
