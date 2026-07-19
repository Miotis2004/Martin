using System.Collections.Immutable;
using Martin.Build.Diagnostics;
using Martin.CodeGeneration;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Text;
using Xunit;

namespace Martin.Build.Tests;

public sealed class GeneratedDiagnosticTranslationTests
{
    [Fact]
    public void Parser_extracts_paths_with_spaces_and_multiple_diagnostics()
    {
        var output = """
            /tmp/martin build/Program.g.cs(12,34): warning CS0219: The variable 'x' is assigned but its value is never used [/tmp/martin build/GeneratedProgram.csproj]
            /tmp/martin build/Program.g.cs(13,9): error CS0103: The name 'missing' does not exist in the current context [/tmp/martin build/GeneratedProgram.csproj]
            """;

        var diagnostics = new GeneratedDiagnosticParser().Parse(output);

        Assert.Equal(2, diagnostics.Count);
        Assert.Equal("/tmp/martin build/Program.g.cs", diagnostics[0].FilePath);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostics[0].Severity);
        Assert.Equal("CS0219", diagnostics[0].Code);
        Assert.Equal(DiagnosticSeverity.Error, diagnostics[1].Severity);
    }

    [Fact]
    public void Translator_maps_exact_generated_location_to_martin_location()
    {
        var martinText = SourceText.From("func main() { print(missing) }", "main.martin");
        var generatedSource = "class Program\n{\n    static void Main() { missing(); }\n}\n";
        var generatedOffset = generatedSource.IndexOf("missing", StringComparison.Ordinal);
        var martinOffset = martinText.ToString().IndexOf("print", StringComparison.Ordinal);
        var sourceMap = new[]
        {
            new GeneratedSourceMapEntry(new TextSpan(generatedOffset, "missing();".Length), new TextLocation(martinText, new TextSpan(martinOffset, "print(missing)".Length)))
        }.ToImmutableArray();
        var generatedDiagnostic = new GeneratedDiagnostic("Program.g.cs", 3, 26, DiagnosticSeverity.Error, "CS0103", "The name 'missing' does not exist.", "Program.g.cs(3,26): error CS0103: The name 'missing' does not exist.");

        var translated = new GeneratedDiagnosticTranslator().Translate([generatedDiagnostic], generatedSource, sourceMap);

        var diagnostic = Assert.Single(translated);
        Assert.Equal("CS0103", diagnostic.Code);
        Assert.Equal("main.martin", diagnostic.Location.FilePath);
        Assert.Contains("print(missing)", diagnostic.Location.Text.ToString(diagnostic.Location.Span));
        Assert.Contains("Generated C# CS0103", diagnostic.Message);
    }

    [Fact]
    public void Translator_uses_nearest_enclosing_source_map_entry()
    {
        var martinText = SourceText.From("func main() { if true { print(1) } }", "main.martin");
        var generatedSource = "if (true)\n{\n    global::System.Console.WriteLine(1);\n}\n";
        var outer = new GeneratedSourceMapEntry(new TextSpan(0, generatedSource.Length), new TextLocation(martinText, new TextSpan(14, 20)));
        var innerStart = generatedSource.IndexOf("WriteLine", StringComparison.Ordinal);
        var inner = new GeneratedSourceMapEntry(new TextSpan(innerStart, 12), new TextLocation(martinText, new TextSpan(24, 8)));
        var diagnostic = new GeneratedDiagnostic("Program.g.cs", 3, 29, DiagnosticSeverity.Warning, "CS9999", "warning", "Program.g.cs(3,29): warning CS9999: warning");

        var translated = new GeneratedDiagnosticTranslator().Translate([diagnostic], generatedSource, [outer, inner]);

        Assert.Equal(inner.MartinLocation, Assert.Single(translated).Location);
    }

    [Fact]
    public void Translator_returns_stable_unmapped_diagnostic_for_generated_wrapper()
    {
        var generatedSource = "class Program { static void Main() { } }\n";
        var diagnostic = new GeneratedDiagnostic("Program.g.cs", 1, 7, DiagnosticSeverity.Error, "CS1514", "{ expected", "Program.g.cs(1,7): error CS1514: { expected");

        var translated = new GeneratedDiagnosticTranslator().Translate([diagnostic], generatedSource, []);

        var unmapped = Assert.Single(translated);
        Assert.Equal("MRT3020", unmapped.Code);
        Assert.Equal(DiagnosticSeverity.Error, unmapped.Severity);
        Assert.Contains("could not be mapped", unmapped.Message);
        Assert.Contains("CS1514", unmapped.Message);
    }
}
