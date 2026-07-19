# Martin Language Guide

**Language version:** `0.1`  
**Product version:** `0.1.0-alpha`  
**Document purpose:** A single, reader-oriented guide to the supported Martin language  
**Normative source:** [The chaptered language specification](specification/README.md)

This is the best place to start learning Martin. It collects the supported alpha language into one document, introduces features in learning order, and keeps the detailed grammar and semantic rules close to the examples that use them.

The chaptered specification remains the normative source of truth. This guide intentionally preserves those rules in consolidated form so readers do not have to move between many files. Development guides and roadmap documents describe implementation history, not current language behavior.

## Quick Start

This section takes Martin from a clean source checkout to installed command-line tools and a running Martin project. The packages are currently produced locally rather than downloaded from a public package feed.

### Prerequisites

Install:

- Git.
- The .NET SDK selected by the repository's `global.json`: SDK `10.0.100`, or a compatible .NET 10 feature-band SDK allowed by its roll-forward policy.

Confirm that the SDK resolver finds a compatible version:

```sh
dotnet --version
```

The Martin CLI executables target `net8.0`, but building the repository source uses the .NET 10 SDK selected by `global.json`.

### Get the source

```sh
git clone https://github.com/Miotis2004/Martin_Programming_Language.git
cd Martin_Programming_Language
```

All commands in the remainder of this section run from the repository root unless stated otherwise.

### Compile and run the CLIs directly from source

Run the project-oriented `martin` CLI:

```sh
dotnet run --project Martin/src/Martin.Cli/Martin.Cli.csproj -- --help
```

Run the lower-level `martinc` compiler driver:

```sh
dotnet run --project Martin/src/Martin.Compiler.Cli/Martin.Compiler.Cli.csproj -- --help
```

These commands restore dependencies, compile the selected project and its project references, and then launch the tool. Building the CLI projects directly also avoids requiring the additional Windows tooling used by the Martin Studio project.

To create Release builds without launching the tools:

```sh
dotnet build Martin/src/Martin.Cli/Martin.Cli.csproj --configuration Release -p:Platform=x64
dotnet build Martin/src/Martin.Compiler.Cli/Martin.Compiler.Cli.csproj --configuration Release -p:Platform=x64
```

### Create local .NET tool packages

The package and command names are intentionally different:

| Package ID | Installed command | Purpose |
|---|---|---|
| `Martin.Tool` | `martin` | Project creation, build, run, formatting, and cleanup |
| `Martin.Compiler.Tool` | `martinc` | Direct source-file compiler workflows |

PowerShell:

```powershell
New-Item -ItemType Directory -Force .\artifacts\packages | Out-Null

dotnet pack .\Martin\src\Martin.Cli\Martin.Cli.csproj `
    --configuration Release `
    -p:Platform=x64 `
    -o .\artifacts\packages

dotnet pack .\Martin\src\Martin.Compiler.Cli\Martin.Compiler.Cli.csproj `
    --configuration Release `
    -p:Platform=x64 `
    -o .\artifacts\packages
```

Bash:

```bash
mkdir -p artifacts/packages

dotnet pack Martin/src/Martin.Cli/Martin.Cli.csproj \
    --configuration Release \
    -p:Platform=x64 \
    -o artifacts/packages

dotnet pack Martin/src/Martin.Compiler.Cli/Martin.Compiler.Cli.csproj \
    --configuration Release \
    -p:Platform=x64 \
    -o artifacts/packages
```

The output directory should contain packages named similarly to:

```text
Martin.Tool.0.1.0-alpha.nupkg
Martin.Compiler.Tool.0.1.0-alpha.nupkg
```

### Install from the local package source

An isolated tool path is recommended while developing Martin because it does not modify global tool state.

PowerShell:

```powershell
$feed = (Resolve-Path .\artifacts\packages).Path
$tools = Join-Path $PWD "artifacts\tool-install"

dotnet tool install Martin.Tool `
    --tool-path $tools `
    --version 0.1.0-alpha `
    --add-source $feed

dotnet tool install Martin.Compiler.Tool `
    --tool-path $tools `
    --version 0.1.0-alpha `
    --add-source $feed

& "$tools\martin" --version
& "$tools\martinc" --version
```

Bash:

```bash
feed="$(pwd)/artifacts/packages"
tools="$(pwd)/artifacts/tool-install"

dotnet tool install Martin.Tool \
    --tool-path "$tools" \
    --version 0.1.0-alpha \
    --add-source "$feed"

dotnet tool install Martin.Compiler.Tool \
    --tool-path "$tools" \
    --version 0.1.0-alpha \
    --add-source "$feed"

"$tools/martin" --version
"$tools/martinc" --version
```

The explicit version is required because `0.1.0-alpha` is a prerelease version.

For personal use, the same packages may instead be installed globally:

```sh
dotnet tool install --global Martin.Tool --version 0.1.0-alpha --add-source artifacts/packages
dotnet tool install --global Martin.Compiler.Tool --version 0.1.0-alpha --add-source artifacts/packages
martin --version
martinc --version
```

If a globally installed command is not found, confirm that the standard .NET global-tool directory is on `PATH` and restart the terminal.

### Create, build, and run a Martin project

When using the isolated installation, substitute the full path to `martin` for the command below. When using a global installation, run:

```sh
mkdir MartinProjects
cd MartinProjects
martin new HelloMartin
cd HelloMartin
martin build
martin run
martin format --check
martin clean
```

The generated project contains `Martin.toml` and `Sources/main.martin`. After a successful build, `martin run --no-build` may be used when the build state and output are still current.

### Compile a source file with `martinc`

Create `main.martin`:

```martin
func main() {
    print("Hello from martinc!")
}
```

Compile it directly:

```sh
martinc main.martin --output out --name HelloMartinc
```

Use `martin` for normal project workflows and `martinc` for explicit source-file compilation.

### Replace or uninstall a local build

When rebuilding another package with the same `0.1.0-alpha` version, uninstall the existing tool before reinstalling it so a cached package is not mistaken for the new build:

```sh
dotnet tool uninstall --global Martin.Tool
dotnet tool uninstall --global Martin.Compiler.Tool
```

For an isolated installation, include the same `--tool-path` used during installation:

```sh
dotnet tool uninstall Martin.Tool --tool-path artifacts/tool-install
dotnet tool uninstall Martin.Compiler.Tool --tool-path artifacts/tool-install
```

Then rebuild the packages and repeat the installation commands.

## Contents

- [Quick Start](#quick-start)
- [Start here](#start-here)
- [Core mental model](#core-mental-model)
- [Source text and lexical grammar](#source-text-and-lexical-grammar)
- [Syntax grammar and recovery](#syntax-grammar-and-recovery)
- [Types and values](#types-and-values)
- [Declarations, names, and scopes](#declarations-names-and-scopes)
- [Expressions and precedence](#expressions-and-precedence)
- [Statements and control flow](#statements-and-control-flow)
- [Structs and classes](#structs-and-classes)
- [Optionals](#optionals)
- [Enums and patterns](#enums-and-patterns)
- [Protocols](#protocols)
- [Generics](#generics)
- [Typed errors](#typed-errors)
- [Runtime and standard library](#runtime-and-standard-library)
- [Implementation limits and deferred features](#implementation-limits-and-deferred-features)

## Start here

### Your first Martin program

A Martin executable commonly begins with a `main` function:

```martin
func main() {
    print("Hello, Martin!")
}
```

Martin source files use the `.martin` extension. A standalone project places source under `Sources/` and declares the project in `Martin.toml`:

```text
HelloMartin/
├── Martin.toml
└── Sources/
    └── main.martin
```

A minimal manifest is:

```toml
manifest-version = 1

[package]
name = "HelloMartin"
version = "0.1.0-alpha"

[target]
kind = "executable"
framework = "net8.0"
entry = "main"

[sources]
include = ["Sources/**/*.martin"]
```

Build and run it with:

```sh
martin build
martin run
```

The complete working version is available in [samples/HelloMartin](../samples/HelloMartin).

### A small language tour

This program combines bindings, type inference, a labeled function parameter, a return value, mutation, and a loop:

```martin
func add(_ left: Int, _ right: Int) -> Int {
    return left + right
}

func countdown(from start: Int) {
    var value = start
    while value > 0 {
        print(value)
        value = value - 1
    }
}

func main() {
    let total = add(2, 3)
    print(total)
    countdown(from: 3)
}
```

Important details:

- `let` creates an immutable binding.
- `var` creates a mutable binding.
- Martin is statically typed, but local types can often be inferred.
- Function parameter and return types are explicit.
- The first parameter name controls the call-site label. `_` means no label.
- Conditions must have type `Bool`.
- Statements are grouped with braces.
- Semicolons are normally optional.

### Recommended learning path

| If you want to learn... | Read |
|---|---|
| Tokens, comments, literals, and keywords | [Source text and lexical grammar](#source-text-and-lexical-grammar) |
| Program structure and exact accepted forms | [Syntax grammar and recovery](#syntax-grammar-and-recovery) |
| Variables, functions, expressions, and flow control | [Types and values](#types-and-values) through [Statements and control flow](#statements-and-control-flow) |
| User-defined data types | [Structs and classes](#structs-and-classes) |
| Missing values | [Optionals](#optionals) |
| Closed alternatives and exhaustive branching | [Enums and patterns](#enums-and-patterns) |
| Shared behavioral contracts | [Protocols](#protocols) |
| Reusable type-safe algorithms and containers | [Generics](#generics) |
| Checked failure handling | [Typed errors](#typed-errors) |
| Built-ins and host-runtime boundaries | [Runtime and standard library](#runtime-and-standard-library) |
| Features that are not part of the alpha | [Implementation limits](#implementation-limits-and-deferred-features) |

The [sample suite](../samples/README.md) follows approximately the same progression and is the fastest way to see complete programs.

## Core mental model

Martin is an experimental, statically typed language that currently compiles through generated C# to the .NET runtime.

A Martin program is built from:

- **Bindings:** immutable `let` values and mutable `var` values.
- **Functions:** typed parameters, optional external labels, explicit throwing effects, and optional return values.
- **Nominal types:** structs, classes, enums, and protocols.
- **Control flow:** `if`, `if let`, `while`, `switch`, `return`, `throw`, and typed `do`/`catch`.
- **Type composition:** optionals and generic constructed types.
- **Effects:** throwing calls are part of a callable's type behavior and must be explicitly handled or propagated.

The compiler rejects unsupported combinations rather than silently treating planned features as available. When in doubt, check [Implementation limits and deferred features](#implementation-limits-and-deferred-features).

---

## Source text and lexical grammar

### Scope

This chapter specifies the source text, tokenization, literals, keywords, operators, punctuation, and trivia accepted by the Martin `0.1` alpha lexer. Valid-program grammar is defined in [syntax-grammar.md](#syntax-grammar-and-recovery); semantic meaning is defined in the core semantic chapters.

### Source text

A Martin source file is a sequence of Unicode scalar values supplied by the host as text. The alpha lexer treats characters according to the host character classification used for letters, digits, and line breaks.

Line endings may be LF (`\n`), CR (`\r`), or CRLF (`\r\n`). They are trivia and do not terminate statements by themselves. Semicolons may terminate selected declarations and statements, but are optional where the syntax chapter states they are optional.

### Lexical grammar

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

### Keywords

The following exact lowercase identifiers are reserved keywords and must not be used as ordinary identifiers in positions where the parser expects an identifier token:

```text
let var func if else while return true false nil struct class init self mutating
enum case switch default throws throw try do catch protocol static
```

`true`, `false`, and `nil` are literal keywords. `self` is an expression keyword.

### Operators and punctuation

The lexer recognizes these fixed tokens:

```text
+ - * / % ! && || = == != < <= > >= -> ( ) { } [ ] : , . ; ?
```

`[` and `]` are tokens for parser and recovery use; the alpha core grammar does not define array types or array literals.

### Literal values

Integer literals are decimal digits only and produce `Int` values when they fit the implementation's alpha integer range. Floating-point literals require at least one digit on both sides of the decimal point and produce `Double` values. There are no hexadecimal, binary, exponent, suffix, separator, or sign-bearing numeric literals; unary `+` and `-` are expressions, not part of the literal token.

String literals support only the escapes listed above. An unrecognized escape is diagnosed and the escaped character is retained in the token value. Newlines are not permitted inside string literals.

### Trivia and comments

Whitespace, comments, and line breaks are trivia. Trivia may appear between tokens. A trailing line break is attached as trailing trivia and may affect formatting, but it does not change the valid syntax.

Multi-line comments do not nest. An unterminated multi-line comment or string literal is invalid and must produce a lexical diagnostic.

### Invalid characters

Any source character that is not part of a token or trivia item is invalid. The lexer reports the character and continues by skipping it for parser input.

### Examples

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

### Cross-references

- Syntax grammar: [syntax-grammar.md](#syntax-grammar-and-recovery)
- Expressions and precedence: [expressions.md](#expressions-and-precedence)
- Implementation limits: [implementation-limits.md](#implementation-limits-and-deferred-features)
- Diagnostics: [../diagnostics.md](diagnostics.md)

---

## Syntax grammar and recovery

### Scope

This chapter defines valid-program syntax for compilation units, declarations, types, statements, expressions, and calls. Advanced constructs are introduced here only to keep the grammar connected; their semantics are defined in their feature chapters.

### Compilation units and members

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

Global statements are accepted by the alpha parser and participate in executable entry-point behavior described in [Statements and control flow](#entry-point-behavior-1).

### Core declarations

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

### Statements

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

### Expressions

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

### Recovery boundary

Parser recovery may synthesize missing tokens, skip invalid lexer tokens, and retain skipped syntax nodes for diagnostics and editor services. Recovery output is not additional valid Martin syntax. A conforming alpha program must satisfy the grammar above without relying on synthesized tokens.

### Examples

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

### Cross-references

- Lexical grammar: [lexical-grammar.md](#source-text-and-lexical-grammar)
- Declarations: [declarations.md](#declarations-names-and-scopes)
- Expressions: [expressions.md](#expressions-and-precedence)
- Statements: [statements.md](#statements-and-control-flow)
- Diagnostics: [../diagnostics.md](diagnostics.md)

---

## Types and values

### Type universe

Martin `0.1` has these built-in core types: `Void`, `Bool`, `Int`, `Double`, `String`, and `Nil`. `Nil` is the type of the `nil` literal and is not a general value type for variable declarations except through optional conversion. Optional, nominal, enum, protocol, generic, and typed-error types are specified in their feature chapters.

### Values

`Bool` has values `true` and `false`. `Int` values come from decimal integer literals within the alpha implementation range. `Double` values come from decimal floating-point literals. `String` values are immutable text values produced by string literals and string operations. `Void` is the result type of functions and statements that do not produce a value.

### Type annotations and inference

A local or global `let` or `var` declaration may provide a type annotation, an initializer, or both. A declaration without an annotation must have an initializer whose type can be determined. When both are present, the initializer must be implicitly convertible to the annotated type.

Function parameters and explicit return types must name defined types. If a function omits `->`, its return type is `Void`.

### Conversions

The alpha core language defines these implicit conversions:

- identity conversion from a type to itself;
- `Int` to `Double` numeric widening;
- `Nil` to an optional type;
- a value of `T` to `T?` where optional semantics permit wrapping.

No implicit conversion exists from `Double` to `Int`, from `String` to a numeric type, or from `Bool` to a numeric type.

### Mutability and initialization

`let` declares a read-only binding. `var` declares a mutable binding. Parameters are read-only. Assignment to a `let` binding, parameter, or read-only property is invalid.

A declaration's value must be definitely initialized before it is read. A `let` binding may be initialized by its declaration initializer. User-defined property initialization and initializer rules are specified in [structs-and-classes.md](#structs-and-classes).

### Equality and numeric behavior

`==` and `!=` are available only for supported operand type pairs. Numeric binary operations on mixed `Int` and `Double` operands convert the `Int` operand to `Double`. Integer division follows the host integer division semantics used by generated alpha code; division by zero is an unchecked host failure unless separately diagnosed by a future implementation.

### Examples

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

### Cross-references

- Syntax grammar: [syntax-grammar.md](#syntax-grammar-and-recovery)
- Expressions: [expressions.md](#expressions-and-precedence)
- Optionals: [optionals.md](#optionals)
- Implementation limits: [implementation-limits.md](#implementation-limits-and-deferred-features)
- Diagnostics: [../diagnostics.md](diagnostics.md)

---

## Declarations, names, and scopes

### Declaration kinds

The core language supports function declarations, global variable declarations through global statements, local variable declarations, parameters, and nominal type declarations. Struct, class, enum, protocol, generic, and typed-error declaration details are specified in their dedicated chapters.

### Name binding

Names are matched exactly and are case-sensitive. A use of an identifier must resolve to a declared symbol in scope, a built-in type, a built-in function, a member reachable from a receiver, or an enum case form described by the advanced chapters.

### Scopes

A compilation unit has a global scope containing built-in types, built-in functions, top-level functions, nominal types, and global variables. Function bodies, blocks, `if`, `while`, `do`, `catch`, and `switch` bodies create nested statement scopes as required by their binding rules. Function parameters are in scope throughout the function body.

A local declaration is visible after it is declared within its containing block. Redeclaring a name in the same scope is invalid. Shadowing an outer-scope name in a nested scope is permitted unless a feature-specific chapter imposes a stricter rule.

### Functions and parameters

A function name plus parameter shape identifies callable candidates for overload resolution. Calls must supply the required number of arguments and must match parameter labels. A parameter written with two identifiers uses the first identifier as the external label and the second as the local name. A parameter written with one identifier has no external label.

### Entry-point behavior

Executable source may use global statements or a function named `main` according to [Statements and control flow](#entry-point-behavior-1). A program must not depend on multiple competing entry points.

### Examples

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

### Related samples

- [HelloMartin](../samples/HelloMartin) uses a conventional `main` function and top-level package declarations.
- [Functions](../samples/Functions) demonstrates function declarations, parameter labels, and local bindings.

### Cross-references

- Syntax grammar: [syntax-grammar.md](#syntax-grammar-and-recovery)
- Types and values: [types-and-values.md](#types-and-values)
- Runtime and built-ins: [runtime-and-standard-library.md](#runtime-and-standard-library)
- Diagnostics: [../diagnostics.md](diagnostics.md)

---

## Expressions and precedence

### Expression forms

Core expressions include literals, identifiers, `self`, parenthesized expressions, unary expressions, binary expressions, assignments, member access, calls, explicit generic call type arguments, and `try` expressions. Object creation, enum case construction, optional operations, protocol calls, generics, and typed errors are completed in their feature chapters.

### Precedence and associativity

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

### Operators

Unary `+` and `-` are numeric operators. Unary `!` is a Boolean logical-negation operator. Arithmetic operators are defined for numeric operands, with `+` also supporting string concatenation where both operands are `String`. Relational operators compare supported numeric operands. `&&` and `||` require `Bool` operands and produce `Bool`.

`==` and `!=` require an equality-supported operand pair. Optional equality with `nil` is specified by [optionals.md](#optionals).

### Assignment

The left side of `=` must be an assignable variable or property target. Assignment converts the right expression to the target type when an implicit conversion exists. Assignment does not create a new declaration.

### Calls and labels

A call expression resolves to a function, method, initializer, built-in, or enum case candidate. The number of arguments, labels, and implicit conversions must match the selected callable. Explicit generic type arguments are permitted only on generic callables and generic type construction forms.

### Examples

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

### Cross-references

- Syntax grammar: [syntax-grammar.md](#expressions)
- Types and values: [types-and-values.md](#types-and-values)
- Generics: [generics.md](#generics)
- Typed errors: [typed-errors.md](#typed-errors)
- Diagnostics: [../diagnostics.md](diagnostics.md)

---

## Statements and control flow

### Statement forms

Core statement forms are block, variable declaration, expression statement, `if`, `if let`, `while`, and `return`. `switch`, `throw`, and `do`/`catch` syntax is parsed in the core grammar but its full semantics are specified in [enums-and-patterns.md](#enums-and-patterns) and [typed-errors.md](#typed-errors).

### Blocks and declaration statements

A block executes statements in order and introduces a nested scope. A variable declaration statement creates either a read-only `let` binding or mutable `var` binding. If an initializer is present, it is evaluated before the binding is available for later statements.

### Conditional control flow

An `if` condition must have type `Bool`. The then block executes when the condition is true. An `else if` is parsed as an `else` whose body is another `if` statement.

`if let name = expression` conditionally unwraps an optional expression. In the then block, `name` is a non-optional read-only binding of the optional element type. If the expression is `nil`, the `else` branch runs when present.

### Loops

A `while` condition must have type `Bool`. The loop body executes repeatedly while the condition evaluates to true. Alpha Martin has no `break` or `continue` statements.

### Return

A `return` statement exits the current function, method, or initializer. A `Void` function may use `return` without an expression. A non-`Void` function must return an expression implicitly convertible to the declared return type along every required path.

### Entry-point behavior

For executable programs, top-level statements are accepted as global statements. A conventional `main` function may also be used by generated executable output. Programs should choose one entry-point style; if the implementation reports competing or missing entry points, the diagnostic is authoritative for alpha.

### Examples

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

### Related samples

- [Functions](../samples/Functions) demonstrates `while`, assignments, function calls, and returns.
- [Optionals](../samples/Optionals) demonstrates `if let` and `else` branches.
- [EnumsAndPatterns](../samples/EnumsAndPatterns) demonstrates `switch` statements over closed domains.

### Cross-references

- Syntax grammar: [syntax-grammar.md#statements](#statements)
- Types and values: [types-and-values.md](#types-and-values)
- Optionals: [optionals.md](#optionals)
- Runtime and standard library: [runtime-and-standard-library.md](#runtime-and-standard-library)
- Diagnostics: [../diagnostics.md](diagnostics.md)

---

## Structs and classes

### Scope

This chapter specifies user-defined `struct` and `class` declarations in Martin `0.1`. Terminology and EBNF notation are defined in the [specification index](specification/README.md#normative-terminology). General declaration, expression, and statement rules are specified in [Declarations, names, and scopes](#declarations-names-and-scopes), [Expressions and precedence](#expressions-and-precedence), and [Statements and control flow](#statements-and-control-flow).

### Declarations

A struct or class declaration introduces a named nominal type in the surrounding declaration scope.

```ebnf
struct-declaration ::= 'struct' identifier generic-parameters? inheritance-clause? type-body
class-declaration  ::= 'class' identifier generic-parameters? inheritance-clause? type-body
inheritance-clause ::= ':' type (',' type)*
type-body          ::= '{' type-member* '}'
type-member        ::= variable-declaration | initializer-declaration | function-declaration
```

The inheritance clause is used for explicit protocol conformance in the alpha language. Class inheritance is not supported. A type name must not conflict with another declaration in the same scope.

### Stored properties

Stored properties are declared with `let` or `var` inside the type body. A stored property has a name and an explicit type unless the declaration grammar for variables permits an initializer-derived type in the surrounding context.

A `let` stored property is immutable after initialization. A `var` stored property is mutable from mutating instance methods and from initialization code. Property access uses member selection:

```martin
struct Point {
    let x: Int
    let y: Int
}

let origin = Point(x: 0, y: 0)
print(origin.x)
```

### Initialization

A type may declare one or more `init` members. Constructor calls use the nominal type name followed by arguments. Argument labels must match the selected initializer or synthesized memberwise construction shape.

```martin
struct Counter {
    var value: Int

    init(value: Int) {
        self.value = value
    }
}

let counter = Counter(value: 1)
```

Initialization must definitely assign every stored property before the constructed value is used. A property must not be read through `self` before it is initialized. Throwing initializers are specified in [typed-errors.md](#throwing-declarations).

### Methods and `self`

Instance methods are declared with `func` in the type body. Method bodies have an implicit `self` value whose type is the containing nominal type after generic substitution. Method calls use member selection and the ordinary call rules.

```martin
struct Greeter {
    let name: String

    func greeting() -> String {
        return name
    }
}
```

A mutating method must be declared with the `mutating` modifier when it assigns through `self` or changes stored state of a value type.

```martin
struct Tally {
    var value: Int

    mutating func increment() {
        value = value + 1
    }
}
```

A non-mutating method must not assign to stored state. Class instances have reference identity at runtime, but alpha method mutation rules still require the declaration to match the compiler's mutability checks.

### Value and reference behavior

Struct values have value semantics at Martin's language boundary. Assigning or passing a struct value produces an independent value according to the generated runtime representation. Class values have reference semantics: assigning or passing a class value copies the reference, and mutations through that reference are observed by all aliases.

Equality for user-defined structs and classes is not synthesized unless explicitly implemented by supported language features. Programs must not rely on undocumented host-language equality for nominal values.

### Protocol conformance

A struct or class conforms to a protocol only through an explicit inheritance clause, for example `struct File: Resettable`. The declaration must provide witnesses for all protocol requirements after applying generic substitution. Protocol rules are specified in [protocols.md](#protocols).

### Generic nominal types

Structs and classes may declare generic parameters and constraints. A generic declaration is an open definition; a use outside the declaration body must be a constructed type such as `Box<Int>`, or a context where generic inference is explicitly supported. Constructed identity and substitution rules are specified in [generics.md](#generics).

### Related samples

- [StructsAndClasses](../samples/StructsAndClasses) demonstrates stored properties, synthesized construction, initializers, and methods.
- [Generics](../samples/Generics) demonstrates a generic `struct` declaration and constructed type use.

### Deferred features

The following are not part of Martin `0.1` structs and classes:

- Class inheritance and overriding.
- Access control modifiers.
- Extensions.
- Static stored properties and static protocol requirements.
- Deinitializers.
- Reflection over fields or methods.
- Implicit derivation of equality, hashing, ordering, or printing.

---

## Optionals

### Scope

This chapter specifies optional types, `nil`, optional injection, and conditional binding in Martin `0.1`.

### Optional type syntax

An optional type is written by appending `?` to another type.

```ebnf
optional-type ::= type '?'
```

The optional marker binds to the immediately preceding type. Nested optionals are represented by repeated markers, for example `Int??`. Optional markers may also appear around constructed generic types, for example `Box<Int>?`.

### Values

For every type `T`, `T?` has two forms:

- a present value containing a `T`; and
- `nil`, representing absence.

`nil` has no standalone type. It is valid only when the expected type is optional or can be inferred as optional from the assignment, argument, return, payload, or pattern context.

```martin
let name: String? = nil
let other: String? = "Martin"
```

### Optional injection

A value of type `T` may be used where `T?` is expected. The compiler injects the value as a present optional. Injection does not recursively flatten nested optionals unless the expected type requires exactly that shape.

```martin
let count: Int? = 3
let nested: Int?? = count
```

### Conditional binding

`if let` unwraps a present optional and binds an immutable local for the true branch. The binding is scoped only to that branch.

```martin
let value: Int? = 5

if let actual = value {
    print(actual)
}
```

The initializer expression of a conditional binding must have optional type. The bound name has the wrapped type. `var` conditional bindings are deferred.

### Optional patterns

Switch and catch pattern positions may use `nil` and `.some(pattern)` when the input type is optional. Optional pattern coverage is finite: an exhaustive switch over `T?` must cover `nil` and all present values accepted by `.some(...)` or an irrefutable pattern.

```martin
switch value {
case nil:
    print("missing")
case .some(let actual):
    print(actual)
}
```

Pattern coverage and usefulness are specified in [enums-and-patterns.md](#enums-and-patterns).

### Operations

Martin `0.1` does not provide optional chaining, force unwrap, nil coalescing, or map/flatMap library helpers as language features. A program must unwrap an optional through conditional binding or pattern matching before using the contained value as `T`.

### Related samples

- [Optionals](../samples/Optionals) demonstrates optional values, `nil`, and conditional binding.
- [EnumsAndPatterns](../samples/EnumsAndPatterns) demonstrates optional subpatterns in an exhaustive `switch`.

### Deferred features

The following optional features are deferred:

- Force unwrap (`!`).
- Optional chaining.
- Nil-coalescing operators.
- `guard let`.
- Mutable conditional bindings.
- Implicit truthiness for optionals.

---

## Enums and patterns

### Scope

This chapter specifies enums, associated values, patterns, switch usefulness, and exhaustiveness in Martin `0.1`.

### Enum declarations

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

### Case construction and identity

A constructed enum value preserves its enum type, generic substitutions, case identity, and associated payload values. Generic enum cases are obtained from the generic definition while their payload types are substituted from the constructed enum type.

```martin
enum Result<Value, Failure> {
    case success(Value)
    case failure(Failure)
}

let result: Result<Int, String> = .success(1)
```

The shorthand `.caseName` form is valid only when the expected enum type is known.

### Pattern forms

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

### Switch semantics

A `switch` evaluates its subject exactly once. Cases are tested in source order and do not fall through. The first matching case executes. A case body may contain ordinary statements and may return, throw, or continue control flow according to the statement rules.

```martin
switch result {
case .success(let value):
    print(value)
case .failure(let message):
    print(message)
}
```

### Type compatibility

A pattern is valid only when it can match the input type. Enum case patterns must name a case of the input enum after generic substitution. Associated-value subpatterns must match the substituted payload types. Literal patterns must be compatible with the input literal type. `nil` and `.some(...)` require optional input.

### Usefulness and exhaustiveness

The compiler performs usefulness analysis in source order. A case that can never be selected because earlier cases cover all of its values is unreachable and is diagnosed.

Switches over closed finite domains must be exhaustive. This includes enums, `Bool`, optionals, and nested combinations of those domains. Integer, floating-point, and string switches require an irrefutable pattern such as `_` or `let name` to be exhaustive.

For enum and optional inputs, diagnostics should describe missing cases or representative missing patterns when practical.

### Related samples

- [EnumsAndPatterns](../samples/EnumsAndPatterns) demonstrates associated values, nested optional patterns, and exhaustive `switch`.
- [TypedErrors](../samples/TypedErrors) demonstrates enum payload matching in `catch` clauses.

### Deferred pattern features

The following pattern forms are not part of Martin `0.1`:

- Mutable `var` bindings.
- Or-patterns.
- Range patterns.
- Tuple patterns outside enum payload structure.
- Type-cast, reflection, and property patterns.
- `where` guards on cases.
- Fallthrough between switch cases.

---

## Protocols

### Scope

This chapter specifies explicit protocol declarations, conformances, requirements, and witnesses in Martin `0.1`.

### Protocol declarations

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

### Explicit conformance

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

### Witness matching

A witness must match the corresponding requirement after all generic substitutions are applied. Matching is exact for:

- member kind;
- name;
- argument count and labels;
- parameter types;
- return type;
- mutating status; and
- throwing status and thrown error type.

A nonthrowing witness does not satisfy a throwing requirement in Martin `0.1`; the witness must have the same throwing effect after substitution.

### Built-in `Error` protocol

The compiler provides a built-in `Error` protocol used by typed errors. It has stable compiler-owned identity. Error types must explicitly conform to `Error` unless another rule in [typed-errors.md](#error-types) permits them.

### Protocol-constrained generics

A generic type parameter may be constrained to a protocol. Within a constrained generic body, member lookup on that parameter exposes only the protocol requirements. Constraint satisfaction uses explicit conformance of the concrete type argument.

```martin
protocol Printable {
    func text() -> String
}

func describe<T: Printable>(_ value: T) -> String {
    return value.text()
}
```

### Related samples

- [Protocols](../samples/Protocols) demonstrates explicit conformance, method witnesses, and mutating requirements.
- [Generics](../samples/Generics) demonstrates protocol-constrained generic calls.

### Deferred protocol features

The following protocol features are deferred:

- Protocol existential values.
- Associated types.
- Default implementations.
- Static requirements.
- Protocol inheritance.
- Conditional conformances.
- Generic protocol types as runtime values.
- Dynamic dispatch beyond the compiler-supported constrained-call model.

---

## Generics

### Scope

This chapter specifies generic declarations, constructed identities, inference, constraints, and substitution in Martin `0.1`.

### Generic parameters

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

### Constructed types

A generic type definition is not itself a closed value type. A use outside its declaration body must provide type arguments or appear in a context where inference is explicitly supported.

```martin
let box: Box<Name> = Box<Name>(value: name)
```

Constructed type identity includes the generic definition and the ordered list of type arguments. `Pair<Int, String>` and `Pair<String, Int>` are distinct types. Repeated construction of the same definition with the same arguments denotes the same semantic type.

Constructed types may be nested and optional, for example `Box<Result<Int, FileError>?>`.

### Generic functions and inference

Generic functions may be called with explicit type arguments, such as `identity<Int>(1)`. The compiler also infers type arguments from supported positions:

- explicit parameter annotations;
- argument expression types;
- expected return type;
- nested optional positions;
- corresponding positions in constructed types; and
- enum associated-value payload positions.

Constructor calls for generic nominal types require a constructed type when inference cannot be derived from a declared expected type.

### Constraints

A constrained type parameter may be used where the constraint's protocol requirements are available. A concrete type argument satisfies a protocol constraint only when it has an explicit, validated conformance to that protocol.

Constraint checking happens after type-argument inference or explicit type-argument parsing. Failed constraints are compile-time errors.

### Substitution

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

### Generic enums and patterns

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

### Typed errors in generic signatures

Generic thrown error types participate in the same substitution model. If a callable declares `throws E` and `E` is a type parameter constrained to `Error`, the effect identity at a call site is the substituted `E` type for that constructed callable.

### Related samples

- [Generics](../samples/Generics) demonstrates generic structs, generic functions, inferred type arguments, protocol constraints, and constructed types.
- [TypedErrors](../samples/TypedErrors) demonstrates a generic error enum used as a thrown type.

### Deferred generic features

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

---

## Typed errors

### Scope

This chapter specifies Martin `0.1` typed errors: error types, throwing declarations, `throw`, `try`, propagation, exhaustive `do`-`catch`, catch patterns, and runtime transport.

### Error types

The compiler provides a built-in `Error` protocol. A thrown error type must conform to `Error`. Enums are the primary error type because their closed case set supports exhaustive catch analysis.

```martin
enum FileError: Error {
    case missing(String)
    case denied(String)
}
```

Struct and class error types are permitted when they explicitly conform to `Error`, but their value space is open; exhaustive catches for open error types generally require a wildcard pattern.

A constructed generic error type is valid when its generic definition has a supported explicit `Error` conformance and its type arguments satisfy their constraints.

### Throwing declarations

Functions, methods, initializers, and protocol method requirements may declare a thrown error type.

```ebnf
throws-clause ::= 'throws' type
```

```martin
func load(_ path: String) throws FileError -> String {
    throw FileError.missing(path)
}
```

Throwing status and thrown error type are part of callable identity for protocol witness matching and generic substitution.

### `throw`

`throw expression` exits the current control path with the expression's error value. The expression type must be compatible with the enclosing callable's declared thrown error type after substitution. A nonthrowing callable must not contain an unhandled `throw`.

### `try`

Every call to a throwing function, method, initializer, or protocol requirement must be preceded by `try`, even when the call appears inside a `do` block. `try` acknowledges the effect; it does not consume the effect.

```martin
do {
    let text = try load("input.txt")
    print(text)
} catch .missing(let path) {
    print(path)
} catch .denied(let path) {
    print(path)
}
```

### Propagation

An acknowledged effect may propagate out of an enclosing callable only when that callable declares the exact same thrown error type after generic substitution. Martin `0.1` does not form implicit error unions and does not widen a concrete error to an arbitrary generic `E: Error` unless the call site's exact substituted type is `E`.

If an expression or block contains distinct unhandled error types, the compiler reports a mixed-effect diagnostic rather than choosing one.

### `do`-`catch`

A `do`-`catch` statement contains a body and one or more catch clauses.

```ebnf
do-catch-statement ::= 'do' block catch-clause+
catch-clause       ::= 'catch' pattern block
```

The `do` body is analyzed for a single exact unhandled error effect. Catch patterns are bound against that error type, in source order, using the same pattern language as `switch`. Pattern variables are scoped only to their catch body.

The catch list must be exhaustive for the body's error type. For closed enum error types, all cases must be covered. For open error types, a wildcard or other irrefutable pattern is required. Unreachable catches are diagnosed.

A catch body may itself perform throwing operations. Those effects are independent of the effect being handled and must be caught by a nested `do`-`catch` or propagated by the containing callable.

### Runtime transport

The generated backend transports Martin error values through a Martin-specific non-generic exception carrier. The carrier preserves the original Martin error value, declared error type metadata, enum case identity, associated payload values, and constructed generic identity. Catch dispatch uses the compiler-produced pattern decision graph and executes exactly one selected catch body.

The runtime carrier is an implementation boundary; Martin programs observe typed error matching and payload binding, not the backend exception class.

### Generic typed errors

Generic thrown error types are substituted through the generic type system. Constructed error identity must be preserved exactly.

```martin
enum DecodeError<Value>: Error {
    case invalid(Value)
}

func fail<T>(_ value: T) throws DecodeError<T> {
    throw DecodeError<T>.invalid(value)
}
```

At `fail<Int>`, the thrown type is exactly `DecodeError<Int>`.

### Related samples

- [TypedErrors](../samples/TypedErrors) demonstrates an error enum, a throwing method, `try`, exhaustive `do`/`catch`, and payload binding.

### Deferred typed-error features

The following features are deferred:

- Error unions.
- `try?` and `try!`.
- `rethrows`.
- Partial catch propagation.
- Catch `where` clauses.
- Asynchronous typed errors.
- Throwing property accessors.
- Implicit conversion from one concrete error type to another.

---

## Runtime and standard library

### Scope

This chapter specifies the observable Martin `0.1` alpha runtime boundary: program startup, public built-ins, built-in runtime types and protocols that are visible to source programs, checked failures, unchecked host failures, and generated-application runtime expectations.

The Martin standard library for `0.1` is intentionally small. A name is part of the standard-library surface only when it is listed in this chapter or in another normative specification chapter. Runtime implementation classes in the generated C# output or `Martin.Runtime` assembly are implementation details unless explicitly listed here.

### Runtime model

Martin `0.1` programs are compiled to C# and executed on .NET. The generated program calls the Martin entry point, transports Martin typed errors through a private runtime carrier, and delegates console and file operations to `Martin.Runtime` helpers.

Martin programs observe the language-level behavior specified here. They must not depend on generated C# names, helper method names, exception class names, stack traces, assembly layout, or private runtime data structures.

### Entry point and process behavior

An executable Martin program must define exactly one supported entry point:

```martin
func main() {
    print("Hello from Martin!")
}
```

The alpha entry point contract is:

- The entry point is a top-level function named `main`.
- It has no parameters.
- It returns `Void`.
- It must not declare a `throws` effect.
- Command-line arguments are captured before `main` is invoked and are then available through `argumentCount()` and `argument(_:)`.
- If `main` returns normally, the process exits with code `0`.
- Calling `exit(_:)` terminates the process immediately with the supplied exit code, subject to the implementation limits in [implementation-limits.md](#process-and-environment-limits).

Multiple `main` declarations, a `main` with parameters, a non-`Void` result, or a throwing `main` is outside the supported executable entry-point shape.

### Built-in types and protocols

The core built-in source types are specified in [types-and-values.md](#types-and-values): `Void`, `Bool`, `Int`, `Double`, `String`, and `Nil`.

The runtime and typed-error boundary also exposes these built-in declarations:

- `protocol Error`: the compiler-owned marker protocol for values that may be thrown by typed-error declarations.
- `enum FileError: Error`: the compiler-owned error enum thrown by `readFile(_:)`.

`FileError` has these cases:

```martin
enum FileError: Error {
    case notFound(path: String)
    case accessDenied(path: String)
    case invalidPath(path: String)
    case io(message: String)
}
```

Programs may catch and pattern-match `FileError` like any other enum that conforms to `Error`. Programs must not redeclare `FileError` or `Error` in the same global scope.

### Built-in functions

The following built-in functions are available in the global scope. Names are case-sensitive and overload resolution follows the normal call rules.

#### `print(_:)`

```martin
func print(_ value: String)
func print(_ value: Int)
func print(_ value: Double)
func print(_ value: Bool)
```

`print(_:)` writes one textual representation of `value` followed by a line terminator to standard output.

- `String` values are written unchanged.
- `Int` and `Double` values are formatted with invariant-culture formatting.
- `Bool` values are written as `true` or `false`.
- `print(_:)` returns `Void`.

#### `readLine()`

```martin
func readLine() -> String
```

`readLine()` reads one line from standard input and returns it without the line terminator. If the input stream reaches end-of-file, it returns the empty string.

#### `writeError(_:)`

```martin
func writeError(_ value: String)
```

`writeError(_:)` writes `value` followed by a line terminator to standard error and returns `Void`.

#### `argumentCount()`

```martin
func argumentCount() -> Int
```

`argumentCount()` returns the number of command-line arguments supplied to the generated Martin process, not including the executable path.

#### `argument(_:)`

```martin
func argument(_ index: Int) -> String
```

`argument(_:)` returns the command-line argument at zero-based `index`. If `index` is negative or greater than or equal to `argumentCount()`, it returns the empty string.

#### `exit(_:)`

```martin
func exit(_ code: Int)
```

`exit(_:)` terminates the current process with `code`. It does not return to Martin code. Exit-code conversion to the host process is subject to [process and environment limits](#process-and-environment-limits).

#### `readFile(_:)`

```martin
func readFile(_ path: String) throws FileError -> String
```

`readFile(_:)` reads the entire text file at `path` and returns its contents. It is a throwing built-in and therefore must be called with `try` in a context that either handles or propagates `FileError`.

```martin
func show(path: String) {
    do {
        let text = try readFile(path)
        print(text)
    } catch .notFound(let missingPath) {
        writeError(missingPath)
    } catch .accessDenied(let deniedPath) {
        writeError(deniedPath)
    } catch .invalidPath(let invalidPath) {
        writeError(invalidPath)
    } catch .io(let message) {
        writeError(message)
    }
}
```

File failures are translated as follows:

| Host condition | Martin error |
| --- | --- |
| File or containing directory is not found | `FileError.notFound(path:)` |
| Access is denied by authorization or security policy | `FileError.accessDenied(path:)` |
| Path syntax is invalid, unsupported, or too long | `FileError.invalidPath(path:)` |
| Other I/O failure | `FileError.io(message:)` |

The payload for path-based cases is the requested path. The payload for `io(message:)` is a normalized host I/O message.

### Numeric behavior

`Int` uses the generated runtime representation selected by the compiler for the target .NET runtime. The alpha implementation emits checked arithmetic for integer addition, subtraction, multiplication, and unary negation. Overflow in those checked operations is an unchecked host failure, not a Martin typed error.

Integer division and remainder use host integer semantics. Division by zero is an unchecked host failure.

`Double` uses the generated runtime floating-point representation selected by the compiler. Floating-point arithmetic and formatting follow the target .NET runtime's invariant-culture behavior. Floating-point exceptional values and precision details are implementation-defined in the alpha.

### Martin checked failures and unchecked host failures

A checked Martin failure is one represented in the source language and enforced by the compiler, such as:

- a typed `throw` value whose type conforms to `Error`;
- a `try` call that is either caught exhaustively or propagated by a matching `throws` clause;
- `readFile(_:)` failures translated to `FileError`.

An unchecked host failure is a failure raised by the generated runtime or host platform that Martin `0.1` does not model as a source-level typed error. Examples include integer overflow, division by zero, optional value access after an invalid compiler lowering, process termination effects, stack overflow, out-of-memory conditions, runtime assembly load failures, and unexpected .NET exceptions.

Martin `catch` clauses catch Martin typed-error carriers that match the active `throws` boundary. They do not catch unrelated host exceptions.

### Runtime error carrier boundary

Generated code transports a thrown Martin error value through a runtime exception carrier. This carrier preserves:

- the original Martin error value;
- the runtime type declared by the active `throws` clause.

This carrier is not part of the Martin source language. Programs observe the declared typed-error behavior, catch matching, associated-value binding, and propagation rules specified in [typed-errors.md](#typed-errors), not the carrier class name or generated C# exception details.

### Generated framework and runtime selection

Generated alpha applications target the .NET framework documented by the build and compatibility documentation. The generated application must use the `Martin.Runtime` assembly selected by the same compiler/build release that produced the generated C# output. Mixing compiler, CLI, generated output, and runtime assemblies from arbitrary alpha versions is unsupported unless [runtime compatibility](runtime-compatibility.md) or [compatibility](compatibility.md) explicitly permits it.

### Standard-library surface summary

The `0.1` standard-library surface is limited to:

- core built-in types and conversions specified in the core chapters;
- `Error` and `FileError`;
- `print(_:)`, `readLine()`, `writeError(_:)`, `argumentCount()`, `argument(_:)`, `exit(_:)`, and `readFile(_:)`.

No collection library, module system, package dependency API, filesystem writing API, environment-variable API, networking API, date/time API, concurrency API, reflection API, or direct .NET interoperability API is specified for Martin `0.1`.

### Related samples

- [HelloMartin](../samples/HelloMartin) demonstrates `print(_:)` in the smallest executable project.
- [TypedErrors](../samples/TypedErrors) demonstrates runtime preservation of typed-error payloads across `throw`, `try`, and `catch`.

### Cross-references

- Specification index: [Chaptered specification](specification/README.md)
- Types and values: [types-and-values.md](#types-and-values)
- Typed errors: [typed-errors.md](#typed-errors)
- Implementation limits: [implementation-limits.md](#implementation-limits-and-deferred-features)
- Diagnostics: [../diagnostics.md](diagnostics.md)
- Runtime compatibility: [runtime-compatibility.md](runtime-compatibility.md)

---

## Implementation limits and deferred features

### Scope

This chapter defines the Martin `0.1` alpha implementation limits and deferred-feature boundaries. The limits in this chapter are part of the alpha contract: programs that require behavior outside these boundaries are not portable Martin `0.1` programs even if a particular build appears to accept or execute them.

### General alpha boundary

Martin `0.1` is an executable-language alpha. It is intended for experiments, samples, diagnostics validation, and feedback. It does not promise source compatibility with future pre-1.0 releases except where compatibility documents explicitly say so.

The implementation is authoritative while drafting the alpha specification. At release, the specification, diagnostics catalog, and compatibility documents define the supported subset.

### Source and project limits

- Source files are Martin text files discovered by the project system and parsed as complete compilation units.
- Supported project manifests use manifest schema version `1`.
- The alpha project system builds executable console applications.
- Package dependency resolution for Martin packages is not specified.
- Multi-project Martin solutions are not specified.
- Module declarations, import declarations, and namespace declarations are not specified.
- Access control is not specified; declarations use the visibility behavior implemented by the compiler and generated C# for the supported executable model.

### Output-kind and build limits

The supported output kind for code generation is a console application. The following output kinds are deferred:

- reusable Martin libraries;
- direct .NET class libraries as a stable public ABI;
- single-file native executables as a language guarantee;
- direct IL generation;
- ahead-of-time compilation guarantees;
- debugger-specific output contracts.

Generated source and build artifacts are implementation details unless explicitly documented by the build pipeline or source-mapping documentation.

### Runtime and standard-library limits

The standard library is limited to the surface listed in [runtime-and-standard-library.md](#standard-library-surface-summary). In particular, Martin `0.1` does not specify:

- arrays, dictionaries, sets, or collection literals as a complete standard library;
- filesystem write, delete, copy, directory enumeration, or path-normalization APIs;
- networking APIs;
- date, time, timer, or random APIs;
- environment-variable APIs;
- threading, async, task, actor, or coroutine APIs;
- reflection or dynamic invocation;
- direct .NET interop from Martin source.

### Process and environment limits

- `main` cannot receive parameters directly; use `argumentCount()` and `argument(_:)`.
- `argument(_:)` returns the empty string for indexes outside the captured argument range.
- `exit(_:)` converts a Martin `Int` to the host process exit-code representation. Values outside the host-supported range may fail with an unchecked host exception.
- Standard input, output, and error behavior follows the host process streams.
- Console encoding, terminal color behavior, interactive input availability, and shell-specific quoting are host-environment concerns.

### Numeric limits

- `Int` arithmetic for addition, subtraction, multiplication, and unary negation is checked in generated code.
- Integer overflow is an unchecked host failure rather than a Martin typed error.
- Integer division by zero and remainder by zero are unchecked host failures.
- Floating-point precision, rounding, infinities, NaN behavior, and overflow behavior follow the target .NET runtime and are not abstracted by Martin `0.1`.
- Numeric literal range handling is limited to the compiler behavior documented by the lexical and type chapters.

### Recursion, nesting, and complexity limits

Martin `0.1` does not specify portable numeric maxima for source-file size, declaration count, expression depth, statement nesting depth, generic nesting depth, protocol-conformance count, pattern-matrix size, or recursion depth.

The implementation may reject, cancel, or fail to compile pathologically large programs because of parser recursion, semantic-analysis work, pattern usefulness/exhaustiveness analysis, generic substitution, generated C# compiler limits, memory pressure, or stack limits. Such failures are implementation limits unless accompanied by a specific diagnostic catalog entry.

Authors of portable alpha samples should keep declarations, generic constructions, nested patterns, nested optionals, and statement nesting small enough for deterministic local validation.

### Typed-error limits

Typed errors are limited to the rules in [typed-errors.md](#typed-errors). Deferred typed-error features include:

- error unions;
- `try?` and `try!`;
- `rethrows`;
- async typed errors;
- multi-error `throws` clauses;
- catching unchecked host exceptions in Martin source.

A Martin `catch` handles Martin typed-error carriers for the declared error type. It is not a general exception-handling construct for host failures.

### Generic and protocol limits

Generic declarations, generic construction, protocol requirements, conformances, and constraints are limited to the forms specified in [generics.md](#generics) and [protocols.md](#protocols). Deferred features include:

- associated types;
- conditional conformances;
- protocol existentials as a complete runtime feature;
- generic protocol values as runtime values;
- higher-kinded types;
- variance annotations;
- extension declarations.

### Pattern and enum limits

Pattern matching and enum behavior are limited to [enums-and-patterns.md](#enums-and-patterns). Exhaustiveness and usefulness checks are defined for the supported pattern set. Pattern forms accepted only for recovery or rejected by semantic analysis are not supported language features.

### Object-model limits

Struct and class support is limited to [structs-and-classes.md](#structs-and-classes). Deferred object-model features include:

- inheritance;
- access control;
- deinitializers;
- property observers;
- operator overloading;
- static members unless otherwise specified by the relevant chapter;
- extensions.

### Unsupported syntax and future features

The following roadmap or future-language features are not implemented as Martin `0.1` features unless another normative chapter explicitly specifies them:

- modules and imports;
- closures and lambdas;
- async/await;
- macros;
- attributes as a stable language feature;
- package manifests with dependency resolution;
- standard-library collections;
- custom operators;
- direct C# or .NET API calls;
- stable binary compatibility for Martin libraries.

Documentation examples for these features must be labeled as future work or fragments and must not be presented as supported alpha programs.

### Cross-references

- Specification index: [Chaptered specification](specification/README.md)
- Runtime and standard library: [runtime-and-standard-library.md](#runtime-and-standard-library)
- Compatibility policy: [compatibility.md](compatibility.md)
- Runtime compatibility: [runtime-compatibility.md](runtime-compatibility.md)
- Diagnostics: [../diagnostics.md](diagnostics.md)
