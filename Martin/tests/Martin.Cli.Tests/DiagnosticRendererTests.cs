using System.Text.Json;
using Martin.CommandLine;
using Xunit;

namespace Martin.Cli.Tests;

public sealed class DiagnosticRendererTests
{
    [Fact]
    public void HumanRendererOrdersDiagnosticsAndShowsSourceExcerpt()
    {
        var path = Path.Combine(Path.GetTempPath(), "MartinDiagnosticTests", Guid.NewGuid().ToString("N") + ".martin");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "func main() {\n\tprint(missing)\n}\n");

        try
        {
            var writer = new StringWriter();
            new HumanDiagnosticRenderer().Render([
                new CommandDiagnostic { Code = "MRT1000", Severity = CommandDiagnosticSeverity.Warning, Message = "later", Location = Loc(path, 1, 1, 1, 2) },
                new CommandDiagnostic { Code = "MRT2004", Severity = CommandDiagnosticSeverity.Error, Message = "Name 'missing' does not exist.", Location = Loc(path, 2, 8, 2, 15) }
            ],
                                                 writer);

            var output = writer.ToString();
            Assert.StartsWith($"{path}(2,8): error MRT2004", output, StringComparison.Ordinal);
            Assert.Contains("2 |     print(missing)", output);
            Assert.Contains("^^^^^^^", output);
            Assert.True(output.IndexOf("error MRT2004", StringComparison.Ordinal) < output.IndexOf("warning MRT1000", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void JsonRendererWritesEnvelopeWithFullLocationsAndRelatedLocations()
    {
        var diagnostic = new CommandDiagnostic {
            Code = "MRT2004",
            Severity = CommandDiagnosticSeverity.Error,
            Message = "Name 'missing' does not exist.",
            Location = Loc("Sources/main.martin", 4, 11, 4, 18),
            RelatedLocations = [new CommandRelatedLocation { Location = Loc("Sources/other.martin", 1, 2, 1, 6), Message = "declared here" }]
        };

        var writer = new StringWriter();
        new JsonDiagnosticRenderer().Render([diagnostic], writer);

        using var document = JsonDocument.Parse(writer.ToString());
        var rendered = document.RootElement.GetProperty("diagnostics")[0];
        Assert.Equal("MRT2004", rendered.GetProperty("code").GetString());
        Assert.Equal(4, rendered.GetProperty("location").GetProperty("startLine").GetInt32());
        Assert.Equal(18, rendered.GetProperty("location").GetProperty("endColumn").GetInt32());
        Assert.Equal("Sources/other.martin", rendered.GetProperty("relatedLocations") [0].GetProperty("location").GetProperty("filePath").GetString());
    }

    [Fact]
    public void JsonRendererMatchesDocumentedEnvelopeContract()
    {
        var diagnostic = new CommandDiagnostic {
            Code = "MRT2004",
            Severity = CommandDiagnosticSeverity.Error,
            Message = "Name 'missing' does not exist.",
            Location = Loc("Sources/main.martin", 4, 11, 4, 18)
        };

        var writer = new StringWriter();
        new JsonDiagnosticRenderer().Render([diagnostic], writer);

        using var document = JsonDocument.Parse(writer.ToString());
        Assert.True(document.RootElement.TryGetProperty("diagnostics", out var diagnostics));
        Assert.Equal(JsonValueKind.Array, diagnostics.ValueKind);

        var rendered = diagnostics[0];
        Assert.Equal("MRT2004", rendered.GetProperty("code").GetString());
        Assert.Equal("error", rendered.GetProperty("severity").GetString());
        Assert.Equal("Name 'missing' does not exist.", rendered.GetProperty("message").GetString());
        Assert.Equal("Sources/main.martin", rendered.GetProperty("location").GetProperty("filePath").GetString());
        Assert.Equal(4, rendered.GetProperty("location").GetProperty("startLine").GetInt32());
        Assert.Equal(11, rendered.GetProperty("location").GetProperty("startColumn").GetInt32());
        Assert.Equal(4, rendered.GetProperty("location").GetProperty("endLine").GetInt32());
        Assert.Equal(18, rendered.GetProperty("location").GetProperty("endColumn").GetInt32());
        Assert.Equal(JsonValueKind.Array, rendered.GetProperty("relatedLocations").ValueKind);
        Assert.False(rendered.TryGetProperty("filePath", out _));
        Assert.False(rendered.TryGetProperty("source", out _));
        Assert.False(rendered.TryGetProperty("path", out _));
    }

    static CommandTextLocation Loc(string filePath, int startLine, int startColumn, int endLine, int endColumn) => new() {
        FilePath = filePath,
        StartLine = startLine,
        StartColumn = startColumn,
        EndLine = endLine,
        EndColumn = endColumn
    };
}
