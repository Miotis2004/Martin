# Martin Language Specification

**Specification status:** Normative for the Martin `0.1.0-alpha` language unless a chapter is marked non-normative.
**Language version:** `0.1`
**Product version:** `0.1.0-alpha`
**Owner:** Martin language and compiler maintainers

This directory is the authoritative entry point for the Martin alpha language specification. Development guides, roadmap documents, and architecture notes remain historical or engineering references; they are not normative for user-visible language behavior unless this specification explicitly incorporates them.

## Normative terminology

The specification uses the following terms consistently:

- **must** or **shall**: required behavior for conforming Martin `0.1` implementations.
- **must not**: prohibited behavior.
- **may**: permitted behavior.
- **implementation limit**: a deliberate alpha boundary that valid programs must respect.
- **unspecified**: a behavior where programs must not depend on one particular choice.
- **undefined behavior**: reserved for cases where the implementation intentionally provides no guarantee; the alpha specification should prefer explicit diagnostics or documented host failures instead.
- **non-normative**: explanatory text, examples, or historical notes that do not define required behavior.

## EBNF notation

Grammar chapters use this Extended Backus-Naur Form convention:

| Form | Meaning |
|---|---|
| `production ::= expression` | Defines a nonterminal production. |
| `nonterminal` | A named grammar production. |
| `` `token` `` | A literal token or keyword. |
| `A B` | Sequence: `A` followed by `B`. |
| `A | B` | Alternative: either `A` or `B`. |
| `[ A ]` | Optional: zero or one `A`. |
| `{ A }` | Repetition: zero or more `A`. |
| `{ A }+` | Repetition: one or more `A`. |
| `( A )` | Grouping. |

Valid-program grammar is documented separately from parser recovery. Recovery rules describe diagnostics and editor resilience, not additional accepted syntax.

## Cross-reference and example rules

Each normative chapter must include:

1. Language-version metadata.
2. An owner line naming the maintainer group responsible for the chapter.
3. A status line identifying whether the chapter is normative, draft-normative, or non-normative.
4. Links to related grammar, semantic chapters, diagnostics, samples, or implementation-limit entries where applicable.
5. Valid examples for accepted syntax where practical.
6. Invalid examples or triggering scenarios for notable diagnostics where practical.

Executable examples should be pulled from release samples or validation fixtures whenever possible. Fragment-only examples must be clearly labeled as fragments.

## Chapter index

| Chapter | Path | Owner | Status |
|---|---|---|---|
| Source text and lexical grammar | [lexical-grammar.md](lexical-grammar.md) | Compiler front end | Normative core rules |
| Syntax grammar and recovery | [syntax-grammar.md](syntax-grammar.md) | Compiler front end | Normative core rules |
| Types and values | [types-and-values.md](types-and-values.md) | Semantic analysis | Normative core rules |
| Declarations, names, and scopes | [declarations.md](declarations.md) | Semantic analysis | Normative core rules |
| Expressions and precedence | [expressions.md](expressions.md) | Parser and semantic analysis | Normative core rules |
| Statements and control flow | [statements.md](statements.md) | Parser and semantic analysis | Normative core rules |
| Structs and classes | [structs-and-classes.md](structs-and-classes.md) | Type system | Draft normative framework |
| Optionals | [optionals.md](optionals.md) | Type system | Draft normative framework |
| Enums and patterns | [enums-and-patterns.md](enums-and-patterns.md) | Pattern matching | Draft normative framework |
| Protocols | [protocols.md](protocols.md) | Protocol system | Draft normative framework |
| Generics | [generics.md](generics.md) | Generic type system | Draft normative framework |
| Typed errors | [typed-errors.md](typed-errors.md) | Effects and diagnostics | Draft normative framework |
| Runtime and standard library boundary | [runtime-and-standard-library.md](runtime-and-standard-library.md) | Runtime and code generation | Draft normative rules |
| Implementation limits and deferred features | [implementation-limits.md](implementation-limits.md) | Release management | Draft normative rules |

## Alpha samples

The executable examples for the supported language progression live in the [alpha sample suite](../../samples/README.md). The suite is validated from isolated copies by `scripts/validate-samples.sh` and `scripts/validate-samples.ps1`.

## Relationship to phase documents

Phase guides document implementation history and may contain obsolete roadmap intent. When this specification and a phase guide disagree, the alpha release must either update this specification to match implemented behavior or record the divergence as a known issue. Users should start here for language rules.
