# Build Pipeline

1. Validate build options, including output kind, target framework, output directory, and assembly identity.
2. Bind the compilation and stop on semantic errors.
3. Lower the bound program with cancellation checks before functions, methods, initializers, and statements.
4. Emit deterministic C# with source-map directives for user code and hidden generated wrappers.
5. Create an isolated temporary project and invoke `dotnet build` through the build-runner abstraction.
6. Collect and validate staged artifacts, including runtime compatibility and runtime identity.
7. Publish staging atomically to the requested output directory.
8. Treat temporary cleanup as best effort: cleanup failures are diagnostics/log records and do not replace the original cancellation or publication status.
