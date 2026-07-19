# Martin.Runtime

`Martin.Runtime` contains the runtime helpers required by generated Martin alpha applications.

For `0.1.0-alpha`, the supported public surface is intentionally small:

- `MartinConsole` for generated implementations of console built-ins.
- `MartinFileSystem` and file-error result types for generated checked file I/O boundaries.
- `Optional<T>` for generated optional values.
- `MartinThrownError` and `MartinThrownErrorValue` for generated typed-error propagation.
- `MartinRuntimeInfo` for runtime version and compatibility metadata.

The package is intended for generated Martin applications and Martin toolchain validation. It is not a general-purpose host API, and no binary compatibility is promised across alpha minor versions.
