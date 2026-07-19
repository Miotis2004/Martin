using Martin.Compiler;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase15ErrorTypeConformanceTests
{
    [Fact]
    public void CrossFileExplicitErrorConformanceIsUsedByThrowsClause()
    {
        var error = SyntaxTree.Parse("enum NetworkError: Error { case offline }", "error.martin");
        var function = SyntaxTree.Parse("func connect() throws NetworkError { throw NetworkError.offline }", "client.martin");

        var compilation = Compilation.Create(function, error);
        var program = compilation.BindProgram();

        Assert.DoesNotContain(program.Diagnostics, diagnostic => diagnostic.Code == "MRT2194");
        var conformance = Assert.Single(program.Conformances);
        Assert.Same(conformance.Type, Assert.Single(program.Functions).ErrorType);
        Assert.Equal("Error", conformance.Protocol.Name);
    }

    [Fact]
    public void ConstructedErrorUsesItsDefinitionsValidatedConformance()
    {
        var compilation = Compile("""
            enum DecodeError<Value>: Error { case invalid(value: Value) }
            func decode() throws DecodeError<Int> { throw DecodeError<Int>.invalid(value: 1) }
            """);

        Assert.DoesNotContain(compilation.Diagnostics, diagnostic => diagnostic.Code == "MRT2194");
        var errorType = Assert.IsType<ConstructedTypeSymbol>(Assert.Single(compilation.BindProgram().Functions).ErrorType);
        Assert.Equal("DecodeError", errorType.GenericDefinition.Name);
        Assert.Same(TypeSymbol.Int, Assert.Single(errorType.TypeArguments));
    }

    [Fact]
    public void OnlyErrorConstrainedOpenParametersAreAccepted()
    {
        var accepted = Compile("func forward<E: Error>(_ error: E) throws E { throw error }");
        var rejected = Compile("func forward<E>(_ error: E) throws E { throw error }");

        Assert.DoesNotContain(accepted.Diagnostics, diagnostic => diagnostic.Code == "MRT2194");
        Assert.Contains(rejected.Diagnostics, diagnostic => diagnostic.Code == "MRT2194");
    }

    [Theory]
    [InlineData("Error")]
    [InlineData("Int")]
    [InlineData("String?")]
    [InlineData("Void")]
    public void UnsupportedThrowsTypesAreRejected(string type)
    {
        var compilation = Compile($"func invalid() throws {type} {{ }}");

        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == "MRT2194");
    }

    private static Compilation Compile(string source) =>
        Compilation.Create(SyntaxTree.Parse(source, "errors.martin"));
}
