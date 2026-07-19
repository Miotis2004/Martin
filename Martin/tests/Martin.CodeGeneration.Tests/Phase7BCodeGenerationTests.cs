using Martin.CodeGeneration;
using Martin.Compiler;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.CodeGeneration.Tests;

public sealed class Phase7BCodeGenerationTests
{
    static void AssertContainsAny(string source, params string[] expected) => Assert.True(expected.Any(source.Contains), $"Expected generated source to contain one of: {string.Join(", ", expected)}\nGenerated source:\n{source}");

    static string Emit(string source)
    {
        var program = Compilation.Create(SyntaxTree.Parse(source, "test.martin")).BindProgram();
        Assert.Empty(program.Diagnostics);
        var result = new CSharpEmitter().Emit(new() { Program = new CSharpLowerer().Lower(program), AssemblyName = "TestApp", OutputKind = OutputKind.ConsoleApplication });
        Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        return result.GeneratedSource;
    }

    [Fact]
    public void EmitsOptionalConstructionNilAndIfLetLowering()
    {
        var cs = Emit("""
func main() {
    let count: Int? = 10
    if let value = count {
        print(value)
    }
    let missing: Int? = nil
    if missing == nil {
        print(0)
    }
}
""");

        Assert.Contains("Martin.Runtime.Optional<long>", cs);
        AssertContainsAny(cs, "new Martin.Runtime.Optional<long>(10L)", "new Martin.Runtime.Optional<long>(10)");
        AssertContainsAny(cs, "Martin.Runtime.Optional<long>.None", "Optional<long>.None");
        Assert.Contains(".HasValue", cs);
        Assert.Contains(".Value", cs);
    }
}
