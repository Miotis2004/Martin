# Martin Toolchain Architecture

The Martin toolchain is split into parsing/binding, lowering, C# code generation, build orchestration, artifact validation, and execution. The compiler front end produces a bound program with source locations and diagnostics. Phase 9 lowering normalizes backend-sensitive constructs before emission so the emitter writes C# from a backend-ready representation rather than making semantic decisions.

The build service coordinates lowering, deterministic C# generation, temporary project generation, runtime discovery, artifact validation, atomic publication, and cleanup. Execution is owned by the execution service and consumes only validated build results.
