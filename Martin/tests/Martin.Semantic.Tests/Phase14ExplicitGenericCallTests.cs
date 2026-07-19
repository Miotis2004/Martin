using Martin.Compiler;
using Martin.Compiler.Binding;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase14ExplicitGenericCallTests
{
    [Fact]
    public void ExplicitFunctionCallUsesSubstitutedSignatureAndResult()
    {
        var tree = SyntaxTree.Parse("func identity<T>(_ value: T) -> T { return value } func use() -> Int { return identity<Int>(42) }");
        var compilation = Compilation.Create(tree);

        Assert.Empty(compilation.Diagnostics);
        var use = compilation.BindProgram().Functions.Single(function => function.Name == "use");
        var call = Assert.IsType<BoundCallExpression>(Assert.IsType<BoundReturnStatement>(
            Assert.Single(compilation.BindProgram().FunctionBodies[use].Statements)).Expression);
        Assert.Same(TypeSymbol.Int, call.Type);
        Assert.Equal([TypeSymbol.Int], call.TypeArguments);
        Assert.Equal("identity", call.OriginalDefinition.Name);
        Assert.Same(TypeSymbol.Int, Assert.Single(call.Function.Parameters).Type);
    }

    [Fact]
    public void ExplicitGenericMemberCallUsesSubstitutedSignature()
    {
        var tree = SyntaxTree.Parse("struct Mapper { func convert<T>(_ value: T) -> T { return value } } func use(_ mapper: Mapper) -> String { return mapper.convert<String>(\"ok\") }");
        var compilation = Compilation.Create(tree);

        Assert.Empty(compilation.Diagnostics);
        var use = compilation.BindProgram().Functions.Single(function => function.Name == "use");
        var call = Assert.IsType<BoundMethodCallExpression>(Assert.IsType<BoundReturnStatement>(
            Assert.Single(compilation.BindProgram().FunctionBodies[use].Statements)).Expression);
        Assert.Same(TypeSymbol.String, call.Type);
        Assert.Equal([TypeSymbol.String], call.TypeArguments);
        Assert.Same(TypeSymbol.String, Assert.Single(call.Method.Parameters).Type);
    }

    [Theory]
    [InlineData("func plain(_ value: Int) -> Int { return value } func use() { plain<Int>(1) }", "MRT2308")]
    [InlineData("func pair<T, U>(_ value: T) -> T { return value } func use() { pair<Int>(1) }", "MRT2309")]
    public void InvalidExplicitCallableTypeArgumentsAreDiagnosed(string source, string code)
    {
        var compilation = Compilation.Create(SyntaxTree.Parse(source));
        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Fact]
    public void ExplicitCallableTypeArgumentsValidateProtocolConstraints()
    {
        var source = "protocol P { func value() -> Int } struct Good: P { func value() -> Int { return 1 } } struct Bad { } func constrained<T: P>(_ value: T) { } func use(_ good: Good, _ bad: Bad) { constrained<Good>(good); constrained<Bad>(bad) }";
        var compilation = Compilation.Create(SyntaxTree.Parse(source));
        Assert.Single(compilation.Diagnostics, diagnostic => diagnostic.Code == "MRT2304");
    }

    [Fact]
    public void ComparisonSyntaxIsNotParsedAsGenericCall()
    {
        var tree = SyntaxTree.Parse("func less(_ a: Int, _ b: Int) -> Bool { return a < b }");
        Assert.DoesNotContain(tree.Diagnostics, diagnostic => diagnostic.Severity == Martin.Compiler.Diagnostics.DiagnosticSeverity.Error);
        var returnStatement = DescendantsAndSelf(tree.Root).Single(node => node.Kind == SyntaxKind.ReturnStatement);
        Assert.Contains(DescendantsAndSelf(returnStatement), node => node.Kind == SyntaxKind.BinaryExpression);
    }

    private static IEnumerable<SyntaxNode> DescendantsAndSelf(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.GetChildren())
            foreach (var descendant in DescendantsAndSelf(child))
                yield return descendant;
    }
}
