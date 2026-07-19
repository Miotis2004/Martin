using Martin.Compiler;
using Martin.Compiler.Binding;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase14InferredGenericCallTests
{
    [Fact]
    public void FunctionCallInfersArgumentsAndPublishesConstructedSignature()
    {
        var compilation = Compilation.Create(SyntaxTree.Parse(
            "func identity<T>(_ value: T) -> T { return value } func use() -> Int { return identity(42) }"));

        Assert.Empty(compilation.Diagnostics);
        var call = GetReturnCall<BoundCallExpression>(compilation, "use");
        Assert.Equal([TypeSymbol.Int], call.TypeArguments);
        Assert.Same(TypeSymbol.Int, call.Type);
        Assert.Same(TypeSymbol.Int, Assert.Single(call.Function.Parameters).Type);
        Assert.Equal("identity", call.OriginalDefinition.Name);
    }

    [Fact]
    public void MethodCallInfersNestedConstructedTypeArguments()
    {
        const string source = "struct Box<T> { let value: T } struct Mapper { func unwrap<T>(_ box: Box<T>) -> T { return box.value } } func use(_ mapper: Mapper, _ box: Box<String>) -> String { return mapper.unwrap(box) }";
        var compilation = Compilation.Create(SyntaxTree.Parse(source));

        Assert.Empty(compilation.Diagnostics);
        var call = GetReturnCall<BoundMethodCallExpression>(compilation, "use");
        Assert.Equal([TypeSymbol.String], call.TypeArguments);
        Assert.Same(TypeSymbol.String, call.Type);
    }

    [Fact]
    public void ExpectedResultInfersOtherwiseUnresolvedArgument()
    {
        var compilation = Compilation.Create(SyntaxTree.Parse(
            "func make<T>() -> T { } func use() -> String { return make() }"));

        Assert.DoesNotContain(compilation.Diagnostics, diagnostic => diagnostic.Code is "MRT2306" or "MRT2307");
        var call = GetReturnCall<BoundCallExpression>(compilation, "use");
        Assert.Equal([TypeSymbol.String], call.TypeArguments);
        Assert.Same(TypeSymbol.String, call.Type);
    }

    [Theory]
    [InlineData("func choose<T>(_ a: T, _ b: T) -> T { return a } func use() { choose(1, \"two\") }", "MRT2307")]
    [InlineData("func make<T>() -> T { } func use() { make() }", "MRT2306")]
    public void FailedInferenceProducesActionableDiagnostic(string source, string code)
    {
        var compilation = Compilation.Create(SyntaxTree.Parse(source));
        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    private static T GetReturnCall<T>(Compilation compilation, string functionName)
        where T : BoundExpression
    {
        var program = compilation.BindProgram();
        var function = program.Functions.Single(candidate => candidate.Name == functionName);
        var statement = Assert.IsType<BoundReturnStatement>(Assert.Single(program.FunctionBodies[function].Statements));
        return Assert.IsType<T>(statement.Expression);
    }
}
