# Source text and lexical grammar

**Specification status:** Normative for Martin `0.1` core lexical rules
**Language version:** `0.1`
**Product version:** `0.1.0-alpha`
**Owner:** Compiler front end

## Scope

This chapter specifies the source text, tokenization, literals, keywords, operators, punctuation, and trivia accepted by the Martin `0.1` alpha lexer. Valid-program grammar is defined in [syntax-grammar.md](syntax-grammar.md); semantic meaning is defined in the core semantic chapters.

## Source text

A Martin source file is a sequence of Unicode scalar values supplied by the host as text. The alpha lexer treats characters according to the host character classification used for letters, digits, and line breaks.

Line endings may be LF (`\n`), CR (`\r`), or CRLF (`\r\n`). They are trivia and do not terminate statements by themselves. Semicolons may terminate selected declarations and statements, but are optional where the syntax chapter states they are optional.

## Lexical grammar

```ebnf
source-file ::= { token | trivia } end-of-file
identifier  ::= ( letter | `_` ) { letter | digit | `_` }
integer-literal ::= digit { digit }
floating-literal ::= digit { digit } `.` digit { digit }
string-literal ::= `"` { string-character | escape-sequence } `"`

escape-sequence ::= `\\n` | `\\r` | `\\t` | `\\0` | `\\\\` | `\\"`
string-character ::= any source character except `"`, `\\`, CR, LF, or end-of-file

whitespace ::= { ` ` | tab }+
line-break ::= LF | CR | CRLF
single-line-comment ::= `//` { any source character except CR, LF, or end-of-file }
documentation-comment ::= `///` { any source character except CR, LF, or end-of-file }
multi-line-comment ::= `/*` { any source character } `*/`
```

A `documentation-comment` is lexically distinct trivia, but the core language does not assign it normative semantic meaning in alpha.

## Keywords

The following exact lowercase identifiers are reserved keywords and must not be used as ordinary identifiers in positions where the parser expects an identifier token:

```text
let var func if else while return true false nil struct class init self mutating
enum case switch default throws throw try do catch protocol static
```

`true`, `false`, and `nil` are literal keywords. `self` is an expression keyword.

## Operators and punctuation

The lexer recognizes these fixed tokens:

```text
+ - * / % ! && || = == != < <= > >= -> ( ) { } [ ] : , . ; ?
```

`[` and `]` are tokens for parser and recovery use; the alpha core grammar does not define array types or array literals.

## Literal values

Integer literals are decimal digits only and produce `Int` values when they fit the implementation's alpha integer range. Floating-point literals require at least one digit on both sides of the decimal point and produce `Double` values. There are no hexadecimal, binary, exponent, suffix, separator, or sign-bearing numeric literals; unary `+` and `-` are expressions, not part of the literal token.

String literals support only the escapes listed above. An unrecognized escape is diagnosed and the escaped character is retained in the token value. Newlines are not permitted inside string literals.

## Trivia and comments

Whitespace, comments, and line breaks are trivia. Trivia may appear between tokens. A trailing line break is attached as trailing trivia and may affect formatting, but it does not change the valid syntax.

Multi-line comments do not nest. An unterminated multi-line comment or string literal is invalid and must produce a lexical diagnostic.

## Invalid characters

Any source character that is not part of a token or trivia item is invalid. The lexer reports the character and continues by skipping it for parser input.

## Examples

Valid fragment:

```martin
/// Greets the user.
func main() {
  let name: String = "Martin\n"
  print(name)
}
```

Invalid fragment:

```martin
let value = 1. // not a floating literal; `.` is a separate token
let bad = "unterminated
```

## Cross-references

- Syntax grammar: [syntax-grammar.md](syntax-grammar.md)
- Expressions and precedence: [expressions.md](expressions.md)
- Implementation limits: [implementation-limits.md](implementation-limits.md)
- Diagnostics: [../diagnostics.md](../diagnostics.md)
