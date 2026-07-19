using Martin.CodeGeneration;
using Martin.Compiler;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.CodeGeneration.Tests;

public sealed class Phase9SourceMappingTests
{
    static CodeGenerationResult Emit(params(string Path, string Source)[] sources)
    {
        var trees = sources.Select(s => SyntaxTree.Parse(s.Source, s.Path)).ToArray();
        var program = Compilation.Create(trees).BindProgram();
        Assert.Empty(program.Diagnostics);
        var result = new CSharpEmitter().Emit(new() { Program = new CSharpLowerer().Lower(program), AssemblyName = "TestApp", OutputKind = OutputKind.ConsoleApplication });
        Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        return result;
    }

    [Fact]
    public void SourceMapContainsUserStatements()
    {
        var result = Emit(("single file.martin", "func main() { let answer = 42 print(answer) }"));

        Assert.NotEmpty(result.SourceMap);
        Assert.Contains(result.SourceMap, entry => entry.MartinLocation.FilePath == "single file.martin" && entry.MartinLocation.Text.ToString(entry.MartinLocation.Span).Contains("let answer"));
        Assert.Contains(result.SourceMap, entry => entry.MartinLocation.FilePath == "single file.martin" && entry.MartinLocation.Text.ToString(entry.MartinLocation.Span).Contains("print(answer)"));
    }

    [Fact]
    public void CrossFileSourceMapsAreCorrectAndStable()
    {
        var result = Emit(
            ("helpers.martin", "func helper() { print(1) }"),
            ("main.martin", "func main() { helper() }"));

        Assert.Contains(result.SourceMap, entry => entry.MartinLocation.FilePath == "helpers.martin" && entry.MartinLocation.Text.ToString(entry.MartinLocation.Span).Contains("print(1)"));
        Assert.Contains(result.SourceMap, entry => entry.MartinLocation.FilePath == "main.martin" && entry.MartinLocation.Text.ToString(entry.MartinLocation.Span).Contains("helper()"));
        Assert.Equal(result.SourceMap.OrderBy(entry => entry.GeneratedSpan.Start).Select(entry => entry.GeneratedSpan), result.SourceMap.Select(entry => entry.GeneratedSpan));
    }

    [Fact]
    public void SourceDirectivesCanBeEnabledOrDisabled()
    {
        const string path = "C:\\Projects\\Martin Samples\\quoted \" source.martin";
        var enabled = Emit((path, "func main() { print(42) }"));
        var disabledProgram = Compilation.Create(SyntaxTree.Parse("func main() { print(42) }", path)).BindProgram();
        var disabled = new CSharpEmitter().Emit(new() { Program = new CSharpLowerer().Lower(disabledProgram), AssemblyName = "TestApp", OutputKind = OutputKind.ConsoleApplication, IncludeSourceDirectives = false });

        Assert.Contains("#line", enabled.GeneratedSource);
        Assert.Contains("C:\\\\Projects\\\\Martin Samples\\\\quoted \\\" source.martin", enabled.GeneratedSource);
        Assert.Contains("#line hidden", enabled.GeneratedSource);
        Assert.DoesNotContain("#line", disabled.GeneratedSource);
        Assert.NotEmpty(disabled.SourceMap);
    }
}
