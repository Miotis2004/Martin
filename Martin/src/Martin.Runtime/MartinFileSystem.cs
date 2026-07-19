using System.Security;

namespace Martin.Runtime;

public enum MartinFileErrorKind
{
    NotFound,
    AccessDenied,
    InvalidPath,
    Io
}

public readonly record struct MartinFileErrorDescriptor(MartinFileErrorKind Kind, string Payload);

public readonly record struct MartinFileReadResult(string? Value, MartinFileErrorDescriptor? Error)
{
    public bool IsSuccess => Error is null;
}

public static class MartinFileSystem
{
    public static MartinFileReadResult ReadFile(string path)
    {
        try
        {
            return new MartinFileReadResult(File.ReadAllText(path), null);
        }
        catch (FileNotFoundException)
        {
            return Failure(MartinFileErrorKind.NotFound, path);
        }
        catch (DirectoryNotFoundException)
        {
            return Failure(MartinFileErrorKind.NotFound, path);
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(MartinFileErrorKind.AccessDenied, path);
        }
        catch (SecurityException)
        {
            return Failure(MartinFileErrorKind.AccessDenied, path);
        }
        catch (ArgumentException)
        {
            return Failure(MartinFileErrorKind.InvalidPath, path);
        }
        catch (NotSupportedException)
        {
            return Failure(MartinFileErrorKind.InvalidPath, path);
        }
        catch (PathTooLongException)
        {
            return Failure(MartinFileErrorKind.InvalidPath, path);
        }
        catch (IOException exception)
        {
            return Failure(MartinFileErrorKind.Io, NormalizeMessage(exception.Message));
        }
    }

    private static MartinFileReadResult Failure(MartinFileErrorKind kind, string payload) =>
        new(null, new MartinFileErrorDescriptor(kind, payload ?? string.Empty));

    private static string NormalizeMessage(string message) => string.IsNullOrWhiteSpace(message) ? "I/O error" : message.Trim();
}
