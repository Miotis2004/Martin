using Martin.Runtime;
using Xunit;

namespace Martin.Runtime.Tests;

public sealed class MartinFileSystemTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "martin-file-runtime-" + Guid.NewGuid().ToString("N"));

    public MartinFileSystemTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void ReadFile_returns_success_for_existing_file()
    {
        var path = Path.Combine(_root, "input.txt");
        File.WriteAllText(path, "hello");

        var result = MartinFileSystem.ReadFile(path);

        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ReadFile_translates_missing_file_to_not_found()
    {
        var path = Path.Combine(_root, "missing.txt");

        var result = MartinFileSystem.ReadFile(path);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        var error = result.Error.Value;
        Assert.Equal(MartinFileErrorKind.NotFound, error.Kind);
        Assert.Equal(path, error.Payload);
    }

    [Fact]
    public void ReadFile_translates_invalid_path_to_invalid_path()
    {
        var result = MartinFileSystem.ReadFile(string.Empty);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        var error = result.Error.Value;
        Assert.Equal(MartinFileErrorKind.InvalidPath, error.Kind);
        Assert.Equal(string.Empty, error.Payload);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
