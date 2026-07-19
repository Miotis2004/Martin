# Changelog

All notable changes to the Martin Programming Language project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project uses pre-1.0 alpha versioning. Alpha releases may change syntax, diagnostics, generated C#, package layout, runtime behavior, Studio behavior, and compatibility rules.

## [Unreleased]

### Added

- Release-candidate staging, checksum publication, hosted CI evidence, and final public tag approval remain pending until the Phase 16 release process is completed.

### Changed

- Future changes must keep the language specification, diagnostic catalog, packages, samples, compatibility policy, and release notes aligned before publication.

### Fixed

- No post-`0.1.0-alpha` fixes have been released yet.

### Known limitations

- See `Docs/known-limitations.md` for the current user-facing limitation list.

### Security

- No unreleased security advisories are recorded in this repository. Report suspected vulnerabilities through `SECURITY.md` instead of public issues.

## [0.1.0-alpha] - 2026-07-18

### Added

- First public-alpha release documentation for Martin, an experimental statically typed language for the .NET ecosystem.
- Compiler support through the documented Phase 15 feature set: lexical analysis, parsing, syntax diagnostics, name binding, type checking, control-flow checks, generated C# executable output, and stable diagnostic reporting.
- Core alpha language support for `let`, `var`, primitive literals, arithmetic, comparison and logical operators, functions, labels, returns, `if`/`else`, `while`, local scopes, and `print`.
- Nominal type support for the documented alpha subset of structs, classes, stored properties, initializers, methods, and value/reference semantics.
- Optionals with optional types, `nil`, optional binding, and optional patterns within documented implementation limits.
- Enums and pattern matching with associated values, recursive enum/optional/Boolean/literal/wildcard/binding patterns, exhaustive `switch`, and pattern diagnostics.
- Protocol declarations, explicit conformances, requirement matching, witness maps, constraints, and C# interface lowering.
- Generics for functions and nominal types, including constructed type identity, recursive substitution, inference, constraints, emission, formatting, and language-service support.
- Typed errors with throwing functions and methods, error enums, `throws(Type)`, `try`, typed `do`/`catch`, exhaustiveness checking, and runtime payload preservation.
- `Martin.Tool` (`martin`) and `Martin.Compiler.Tool` (`martinc`) .NET tool package identities, version reporting, local package installation guidance, and smoke-test expectations.
- `Martin.Runtime` alpha packaging policy for generated executable projects, plus an explicit private-for-alpha policy for compiler implementation libraries unless release review approves otherwise.
- Martin Studio alpha documentation for the Windows x64 package path, stable package identity, unsigned-staging policy, sideloading/signing guidance, package inspection, upgrade behavior, and manual smoke workflow.
- Compiler-backed language services for completion, hover, go-to-definition, find references, signature help, semantic classification, diagnostics, and safe formatting.
- Phase 16 language specification, diagnostic catalog metadata, sample suite, known-limitations document, compatibility policy, support policy, security policy, and contribution guidance.

### Changed

- Repository documentation now describes Phase 15 as complete and treats Phase 16 as public-alpha hardening rather than language-design expansion.
- Public installation instructions use the actual package IDs `Martin.Tool` and `Martin.Compiler.Tool` instead of project names.
- Compatibility guidance now distinguishes product/package version `0.1.0-alpha`, language version `0.1`, manifest schema `1`, Studio MSIX version `0.1.0.0`, and generated target framework defaults.
- Release validation is local-first; hosted GitHub Actions remains optional/manual evidence while paid hosted capacity is unavailable.

### Fixed

- Consolidated stale roadmap-era feature status into user-facing documentation that separates implemented alpha behavior from deferred future work.
- Documented alpha package, runtime, Studio, and compatibility boundaries so users do not have to infer support promises from project files or phase guides.

### Known limitations

- Martin is pre-1.0 alpha software and is not supported for production workloads.
- Only executable Martin projects are supported publicly; library output, Martin package dependency resolution, stable ABI, and generated-output cross-version compatibility are deferred.
- Closures, extensions, modules, explicit access control, async/await, direct .NET interoperability, direct IL controls, debugger integration, collection standard-library guarantees, error unions, `try?`, `try!`, `rethrows`, and async typed errors are not part of the supported alpha subset.
- Martin Studio is scoped to Windows x64 alpha validation and does not yet include multi-project solutions, integrated debugging, full terminal emulation, source-control integration, plugins, automatic updates, rename, or code actions.
- Publishing NuGet packages, uploading a GitHub release, signing with a real certificate, creating the final public tag, or submitting to the Microsoft Store requires explicit release approval.

### Security

- No known security fixes are included in this first alpha entry.
- The release policy forbids committing certificates, private keys, NuGet API keys, GitHub tokens, publisher credentials, or other secrets.
- Security reports should follow `SECURITY.md`; public issues should not include suspected vulnerability details.
