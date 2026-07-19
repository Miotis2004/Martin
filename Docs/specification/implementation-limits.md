# Implementation limits and deferred features

**Specification status:** Draft normative
**Language version:** `0.1`
**Product version:** `0.1.0-alpha`
**Owner:** Release management

## Scope

This chapter defines the Martin `0.1` alpha implementation limits and deferred-feature boundaries. The limits in this chapter are part of the alpha contract: programs that require behavior outside these boundaries are not portable Martin `0.1` programs even if a particular build appears to accept or execute them.

## General alpha boundary

Martin `0.1` is an executable-language alpha. It is intended for experiments, samples, diagnostics validation, and feedback. It does not promise source compatibility with future pre-1.0 releases except where compatibility documents explicitly say so.

The implementation is authoritative while drafting the alpha specification. At release, the specification, diagnostics catalog, and compatibility documents define the supported subset.

## Source and project limits

- Source files are Martin text files discovered by the project system and parsed as complete compilation units.
- Supported project manifests use manifest schema version `1`.
- The alpha project system builds executable console applications.
- Package dependency resolution for Martin packages is not specified.
- Multi-project Martin solutions are not specified.
- Module declarations, import declarations, and namespace declarations are not specified.
- Access control is not specified; declarations use the visibility behavior implemented by the compiler and generated C# for the supported executable model.

## Output-kind and build limits

The supported output kind for code generation is a console application. The following output kinds are deferred:

- reusable Martin libraries;
- direct .NET class libraries as a stable public ABI;
- single-file native executables as a language guarantee;
- direct IL generation;
- ahead-of-time compilation guarantees;
- debugger-specific output contracts.

Generated source and build artifacts are implementation details unless explicitly documented by the build pipeline or source-mapping documentation.

## Runtime and standard-library limits

The standard library is limited to the surface listed in [runtime-and-standard-library.md](runtime-and-standard-library.md#standard-library-surface-summary). In particular, Martin `0.1` does not specify:

- arrays, dictionaries, sets, or collection literals as a complete standard library;
- filesystem write, delete, copy, directory enumeration, or path-normalization APIs;
- networking APIs;
- date, time, timer, or random APIs;
- environment-variable APIs;
- threading, async, task, actor, or coroutine APIs;
- reflection or dynamic invocation;
- direct .NET interop from Martin source.

## Process and environment limits

- `main` cannot receive parameters directly; use `argumentCount()` and `argument(_:)`.
- `argument(_:)` returns the empty string for indexes outside the captured argument range.
- `exit(_:)` converts a Martin `Int` to the host process exit-code representation. Values outside the host-supported range may fail with an unchecked host exception.
- Standard input, output, and error behavior follows the host process streams.
- Console encoding, terminal color behavior, interactive input availability, and shell-specific quoting are host-environment concerns.

## Numeric limits

- `Int` arithmetic for addition, subtraction, multiplication, and unary negation is checked in generated code.
- Integer overflow is an unchecked host failure rather than a Martin typed error.
- Integer division by zero and remainder by zero are unchecked host failures.
- Floating-point precision, rounding, infinities, NaN behavior, and overflow behavior follow the target .NET runtime and are not abstracted by Martin `0.1`.
- Numeric literal range handling is limited to the compiler behavior documented by the lexical and type chapters.

## Recursion, nesting, and complexity limits

Martin `0.1` does not specify portable numeric maxima for source-file size, declaration count, expression depth, statement nesting depth, generic nesting depth, protocol-conformance count, pattern-matrix size, or recursion depth.

The implementation may reject, cancel, or fail to compile pathologically large programs because of parser recursion, semantic-analysis work, pattern usefulness/exhaustiveness analysis, generic substitution, generated C# compiler limits, memory pressure, or stack limits. Such failures are implementation limits unless accompanied by a specific diagnostic catalog entry.

Authors of portable alpha samples should keep declarations, generic constructions, nested patterns, nested optionals, and statement nesting small enough for deterministic local validation.

## Typed-error limits

Typed errors are limited to the rules in [typed-errors.md](typed-errors.md). Deferred typed-error features include:

- error unions;
- `try?` and `try!`;
- `rethrows`;
- async typed errors;
- multi-error `throws` clauses;
- catching unchecked host exceptions in Martin source.

A Martin `catch` handles Martin typed-error carriers for the declared error type. It is not a general exception-handling construct for host failures.

## Generic and protocol limits

Generic declarations, generic construction, protocol requirements, conformances, and constraints are limited to the forms specified in [generics.md](generics.md) and [protocols.md](protocols.md). Deferred features include:

- associated types;
- conditional conformances;
- protocol existentials as a complete runtime feature;
- generic protocol values as runtime values;
- higher-kinded types;
- variance annotations;
- extension declarations.

## Pattern and enum limits

Pattern matching and enum behavior are limited to [enums-and-patterns.md](enums-and-patterns.md). Exhaustiveness and usefulness checks are defined for the supported pattern set. Pattern forms accepted only for recovery or rejected by semantic analysis are not supported language features.

## Object-model limits

Struct and class support is limited to [structs-and-classes.md](structs-and-classes.md). Deferred object-model features include:

- inheritance;
- access control;
- deinitializers;
- property observers;
- operator overloading;
- static members unless otherwise specified by the relevant chapter;
- extensions.

## Unsupported syntax and future features

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

## Cross-references

- Specification index: [README.md](README.md)
- Runtime and standard library: [runtime-and-standard-library.md](runtime-and-standard-library.md)
- Compatibility policy: [../compatibility.md](../compatibility.md)
- Runtime compatibility: [../runtime-compatibility.md](../runtime-compatibility.md)
- Diagnostics: [../diagnostics.md](../diagnostics.md)
