# Runtime and standard library boundary

**Specification status:** Draft normative
**Language version:** `0.1`
**Product version:** `0.1.0-alpha`
**Owner:** Runtime and code generation

## Scope

This chapter specifies the observable Martin `0.1` alpha runtime boundary: program startup, public built-ins, built-in runtime types and protocols that are visible to source programs, checked failures, unchecked host failures, and generated-application runtime expectations.

The Martin standard library for `0.1` is intentionally small. A name is part of the standard-library surface only when it is listed in this chapter or in another normative specification chapter. Runtime implementation classes in the generated C# output or `Martin.Runtime` assembly are implementation details unless explicitly listed here.

## Runtime model

Martin `0.1` programs are compiled to C# and executed on .NET. The generated program calls the Martin entry point, transports Martin typed errors through a private runtime carrier, and delegates console and file operations to `Martin.Runtime` helpers.

Martin programs observe the language-level behavior specified here. They must not depend on generated C# names, helper method names, exception class names, stack traces, assembly layout, or private runtime data structures.

## Entry point and process behavior

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
- Calling `exit(_:)` terminates the process immediately with the supplied exit code, subject to the implementation limits in [implementation-limits.md](implementation-limits.md#process-and-environment-limits).

Multiple `main` declarations, a `main` with parameters, a non-`Void` result, or a throwing `main` is outside the supported executable entry-point shape.

## Built-in types and protocols

The core built-in source types are specified in [types-and-values.md](types-and-values.md): `Void`, `Bool`, `Int`, `Double`, `String`, and `Nil`.

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

## Built-in functions

The following built-in functions are available in the global scope. Names are case-sensitive and overload resolution follows the normal call rules.

### `print(_:)`

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

### `readLine()`

```martin
func readLine() -> String
```

`readLine()` reads one line from standard input and returns it without the line terminator. If the input stream reaches end-of-file, it returns the empty string.

### `writeError(_:)`

```martin
func writeError(_ value: String)
```

`writeError(_:)` writes `value` followed by a line terminator to standard error and returns `Void`.

### `argumentCount()`

```martin
func argumentCount() -> Int
```

`argumentCount()` returns the number of command-line arguments supplied to the generated Martin process, not including the executable path.

### `argument(_:)`

```martin
func argument(_ index: Int) -> String
```

`argument(_:)` returns the command-line argument at zero-based `index`. If `index` is negative or greater than or equal to `argumentCount()`, it returns the empty string.

### `exit(_:)`

```martin
func exit(_ code: Int)
```

`exit(_:)` terminates the current process with `code`. It does not return to Martin code. Exit-code conversion to the host process is subject to [process and environment limits](implementation-limits.md#process-and-environment-limits).

### `readFile(_:)`

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

## Numeric behavior

`Int` uses the generated runtime representation selected by the compiler for the target .NET runtime. The alpha implementation emits checked arithmetic for integer addition, subtraction, multiplication, and unary negation. Overflow in those checked operations is an unchecked host failure, not a Martin typed error.

Integer division and remainder use host integer semantics. Division by zero is an unchecked host failure.

`Double` uses the generated runtime floating-point representation selected by the compiler. Floating-point arithmetic and formatting follow the target .NET runtime's invariant-culture behavior. Floating-point exceptional values and precision details are implementation-defined in the alpha.

## Martin checked failures and unchecked host failures

A checked Martin failure is one represented in the source language and enforced by the compiler, such as:

- a typed `throw` value whose type conforms to `Error`;
- a `try` call that is either caught exhaustively or propagated by a matching `throws` clause;
- `readFile(_:)` failures translated to `FileError`.

An unchecked host failure is a failure raised by the generated runtime or host platform that Martin `0.1` does not model as a source-level typed error. Examples include integer overflow, division by zero, optional value access after an invalid compiler lowering, process termination effects, stack overflow, out-of-memory conditions, runtime assembly load failures, and unexpected .NET exceptions.

Martin `catch` clauses catch Martin typed-error carriers that match the active `throws` boundary. They do not catch unrelated host exceptions.

## Runtime error carrier boundary

Generated code transports a thrown Martin error value through a runtime exception carrier. This carrier preserves:

- the original Martin error value;
- the runtime type declared by the active `throws` clause.

This carrier is not part of the Martin source language. Programs observe the declared typed-error behavior, catch matching, associated-value binding, and propagation rules specified in [typed-errors.md](typed-errors.md), not the carrier class name or generated C# exception details.

## Generated framework and runtime selection

Generated alpha applications target the .NET framework documented by the build and compatibility documentation. The generated application must use the `Martin.Runtime` assembly selected by the same compiler/build release that produced the generated C# output. Mixing compiler, CLI, generated output, and runtime assemblies from arbitrary alpha versions is unsupported unless [runtime compatibility](../runtime-compatibility.md) or [compatibility](../compatibility.md) explicitly permits it.

## Standard-library surface summary

The `0.1` standard-library surface is limited to:

- core built-in types and conversions specified in the core chapters;
- `Error` and `FileError`;
- `print(_:)`, `readLine()`, `writeError(_:)`, `argumentCount()`, `argument(_:)`, `exit(_:)`, and `readFile(_:)`.

No collection library, module system, package dependency API, filesystem writing API, environment-variable API, networking API, date/time API, concurrency API, reflection API, or direct .NET interoperability API is specified for Martin `0.1`.

## Related samples

- [HelloMartin](../../samples/HelloMartin) demonstrates `print(_:)` in the smallest executable project.
- [TypedErrors](../../samples/TypedErrors) demonstrates runtime preservation of typed-error payloads across `throw`, `try`, and `catch`.

## Cross-references

- Specification index: [README.md](README.md)
- Types and values: [types-and-values.md](types-and-values.md)
- Typed errors: [typed-errors.md](typed-errors.md)
- Implementation limits: [implementation-limits.md](implementation-limits.md)
- Diagnostics: [../diagnostics.md](../diagnostics.md)
- Runtime compatibility: [../runtime-compatibility.md](../runtime-compatibility.md)
