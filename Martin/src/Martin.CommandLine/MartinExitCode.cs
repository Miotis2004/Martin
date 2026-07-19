namespace Martin.CommandLine;

public enum MartinExitCode
{
    Success = 0,
    CompilationFailed = 1,
    InvalidArguments = 2,
    ProjectNotFound = 3,
    ManifestInvalid = 4,
    BuildFailed = 5,
    ExecutionFailed = 6,
    TestsUnavailableOrFailed = 7,
    Cancelled = 8,
    InternalError = 70
}
