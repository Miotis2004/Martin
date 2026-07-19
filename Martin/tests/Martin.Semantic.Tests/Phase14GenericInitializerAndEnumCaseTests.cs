using Martin.Compiler;
using Martin.Compiler.Binding;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase14GenericInitializerAndEnumCaseTests
{
    [Fact]
    public void ExplicitConstructedInitializerUsesSubstitutedSignature()
    {
        var program = Compile("""
            struct Box<T> { let value: T }
            func make() -> Box<Int> { return Box<Int>(value: 42) }
            """);

        Assert.Empty(program.Diagnostics);
        var creation = Assert.IsType<BoundObjectCreationExpression>(ReturnExpression(program, "make"));
        Assert.Same(TypeSymbol.Int, Assert.Single(creation.Initializer.Parameters).Type);
        Assert.IsType<ConstructedTypeSymbol>(creation.Type);
    }

    [Fact]
    public void ExpectedTypeConstructsGenericEnumCaseAndSubstitutesPayload()
    {
        var program = Compile("""
            enum Result<T> { case success(T) case failure }
            func make() -> Result<String> { return Result.success("done") }
            """);

        Assert.Empty(program.Diagnostics);
        var creation = Assert.IsType<BoundEnumCaseCreationExpression>(ReturnExpression(program, "make"));
        Assert.Same(TypeSymbol.String, Assert.Single(creation.Case.AssociatedValues).Type);
        Assert.IsType<ConstructedTypeSymbol>(creation.Type);
        Assert.Same(creation.Case.OriginalDefinition,
            Assert.Single(program.NamedTypes.Single(type => type.Name == "Result").Cases, item => item.Name == "success"));
    }

    [Theory]
    [InlineData("return Box<Int>(wrong: 42)", "MRT2310")]
    [InlineData("return Box<Int>()", "MRT2111")]
    [InlineData("return Box<Int>(value: \"bad\")", "MRT2003")]
    public void ConstructedInitializerDiagnosesAfterSubstitution(string statement, string code)
    {
        var program = Compile($"struct Box<T> {{ let value: T }} func make() -> Box<Int> {{ {statement} }}");
        Assert.Contains(program.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Fact]
    public void ExplicitConstructedEnumCaseDoesNotRequireContext()
    {
        var program = Compile("enum Result<T> { case success(T) } func make() { Result<Int>.success(42) }");
        Assert.Empty(program.Diagnostics);
        var function = program.Functions.Single(item => item.Name == "make");
        var statement = Assert.IsType<BoundExpressionStatement>(Assert.Single(program.FunctionBodies[function].Statements));
        Assert.IsType<ConstructedTypeSymbol>(Assert.IsType<BoundEnumCaseCreationExpression>(statement.Expression).Type);
    }

    [Theory]
    [InlineData("return Result.success()", "MRT2007")]
    [InlineData("return Result.success(1)", "MRT2003")]
    public void GenericEnumCaseDiagnosesPayloadAfterSubstitution(string statement, string code)
    {
        var program = Compile($"enum Result<T> {{ case success(T) }} func make() -> Result<String> {{ {statement} }}");
        Assert.Contains(program.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Fact]
    public void GenericConstructorInferenceRequiresExplicitContainingArguments()
    {
        var program = Compile("struct Box<T> { let value: T } func make() { Box(value: 42) }");
        Assert.Contains(program.Diagnostics, diagnostic => diagnostic.Code == "MRT2309");
    }

    private static BoundProgram Compile(string source) => Compilation.Create(SyntaxTree.Parse(source)).BindProgram();
    private static BoundExpression ReturnExpression(BoundProgram program, string functionName)
    {
        var function = program.Functions.Single(item => item.Name == functionName);
        return Assert.IsType<BoundReturnStatement>(Assert.Single(program.FunctionBodies[function].Statements)).Expression!;
    }
}
