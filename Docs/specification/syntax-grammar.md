# Syntax grammar and recovery

**Specification status:** Normative for Martin `0.1` core syntax
**Language version:** `0.1`
**Product version:** `0.1.0-alpha`
**Owner:** Compiler front end

## Scope

This chapter defines valid-program syntax for compilation units, declarations, types, statements, expressions, and calls. Advanced constructs are introduced here only to keep the grammar connected; their semantics are defined in their feature chapters.

## Compilation units and members

```ebnf
compilation-unit ::= { member } end-of-file
member ::= function-declaration
         | struct-declaration
         | class-declaration
         | enum-declaration
         | protocol-declaration
         | global-statement

global-statement ::= statement
```

Global statements are accepted by the alpha parser and participate in executable entry-point behavior described in [statements.md](statements.md#entry-point-behavior).

## Core declarations

```ebnf
function-declaration ::= `func` identifier [ type-parameter-list ] parameter-list [ throws-clause ] [ return-type-clause ] block-statement
parameter-list ::= `(` [ parameter { `,` parameter } [ `,` ] ] `)`
parameter ::= identifier [ identifier ] `:` type
return-type-clause ::= `->` type
throws-clause ::= `throws` type

type ::= identifier [ type-argument-list ] { `?` }
type-argument-list ::= `<` type { `,` type } [ `,` ] `>`
type-parameter-list ::= `<` type-parameter { `,` type-parameter } `>`
type-parameter ::= identifier [ `:` type ]
```

A parameter may have one identifier or two identifiers. The first identifier is the external argument label when two are present; otherwise it is the local parameter name and the call has no required label.

## Statements

```ebnf
statement ::= block-statement
            | variable-declaration-statement
            | if-statement
            | if-let-statement
            | while-statement
            | return-statement
            | throw-statement
            | do-catch-statement
            | switch-statement
            | expression-statement

block-statement ::= `{` { statement } `}`
variable-declaration-statement ::= (`let` | `var`) identifier [ `:` type ] [ `=` expression ] [ `;` ]
if-statement ::= `if` expression block-statement [ `else` ( if-statement | block-statement ) ]
if-let-statement ::= `if` `let` identifier `=` expression block-statement [ `else` ( if-statement | block-statement ) ]
while-statement ::= `while` expression block-statement
return-statement ::= `return` [ expression ] [ `;` ]
throw-statement ::= `throw` expression [ `;` ]
expression-statement ::= expression [ `;` ]
```

`switch-statement` and `do-catch-statement` syntax is defined in the advanced feature chapters and remains separated from core semantics.

## Expressions

```ebnf
expression ::= assignment-expression
assignment-expression ::= binary-expression [ `=` assignment-expression ]
binary-expression ::= unary-expression { binary-operator unary-expression }
unary-expression ::= [ unary-operator ] postfix-expression
postfix-expression ::= primary-expression { member-access | type-argument-list | call-suffix }
member-access ::= `.` identifier
call-suffix ::= `(` [ argument { `,` argument } [ `,` ] ] `)`
argument ::= [ identifier `:` ] expression
primary-expression ::= integer-literal
                     | floating-literal
                     | string-literal
                     | `true`
                     | `false`
                     | `nil`
                     | identifier
                     | `self`
                     | `try` postfix-expression
                     | `(` expression `)`
unary-operator ::= `!` | `+` | `-`
binary-operator ::= `*` | `/` | `%` | `+` | `-` | `<` | `<=` | `>` | `>=` | `==` | `!=` | `&&` | `||`
```

Assignment is right-associative. All binary operators are left-associative at their precedence level. Generic call type arguments are parsed only where the `<...>` list is followed by `(` or `.` so that relational expressions remain unambiguous.

## Recovery boundary

Parser recovery may synthesize missing tokens, skip invalid lexer tokens, and retain skipped syntax nodes for diagnostics and editor services. Recovery output is not additional valid Martin syntax. A conforming alpha program must satisfy the grammar above without relying on synthesized tokens.

## Examples

Valid fragment:

```martin
func add(_ left: Int, _ right: Int) -> Int {
  return left + right
}

let total = add(1, 2)
```

Invalid fragment:

```martin
func broken( -> Int { return 0 }
```

## Cross-references

- Lexical grammar: [lexical-grammar.md](lexical-grammar.md)
- Declarations: [declarations.md](declarations.md)
- Expressions: [expressions.md](expressions.md)
- Statements: [statements.md](statements.md)
- Diagnostics: [../diagnostics.md](../diagnostics.md)
