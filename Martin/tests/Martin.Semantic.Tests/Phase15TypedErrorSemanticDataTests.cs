using Martin.Compiler;
using Martin.Compiler.Syntax;
using Martin.Compiler.Symbols;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase15TypedErrorSemanticDataTests
{
    [Fact]
    public void ThrowingCallAndTryPublishExactErrorIdentityAndAcknowledgement()
    {
        var tree = SyntaxTree.Parse("""
            enum Failure: Error { case failed }
            func load() throws Failure -> Int { throw Failure.failed }
            func run() throws Failure -> Int { return try load() }
            """);
        var model = Compilation.Create(tree).GetSemanticModel(tree);
        var runCall = Descendants(tree.Root).OfType<ExpressionSyntax>()
            .Where(node => node.Kind == SyntaxKind.CallExpression)
            .Last();
        var tryExpression = Descendants(tree.Root).OfType<ExpressionSyntax>()
            .Single(node => node.Kind == SyntaxKind.TryExpression);

        var callInfo = Assert.IsType<TypedErrorSemanticInfo>(model.GetTypedErrorInfo(runCall));
        Assert.True(callInfo.Effect.CanThrow);
        Assert.True(callInfo.IsAcknowledged);
        Assert.False(callInfo.IsCaught);
        Assert.True(callInfo.IsPropagated);
        Assert.Equal("Failure", callInfo.DeclaredErrorType?.Name);

        var tryInfo = Assert.IsType<TypedErrorSemanticInfo>(model.GetTryInfo(tryExpression));
        Assert.Equal("Failure", tryInfo.Effect.ErrorType?.Name);
        Assert.True(tryInfo.IsAcknowledged);
    }

    [Fact]
    public void ExhaustiveDoCatchPublishesCaughtStatusAndCatchPatternInfo()
    {
        var tree = SyntaxTree.Parse("""
            enum Failure: Error { case failed }
            func load() throws Failure -> Int { throw Failure.failed }
            func run() { do { let value = try load() } catch .failed { print(1) } }
            """);
        var model = Compilation.Create(tree).GetSemanticModel(tree);
        var runCall = Descendants(tree.Root).OfType<ExpressionSyntax>()
            .Where(node => node.Kind == SyntaxKind.CallExpression)
            .First(node => node.Span.Start > tree.Text.ToString().IndexOf("do", StringComparison.Ordinal));
        var catchClause = Descendants(tree.Root).Single(node => node.Kind == SyntaxKind.CatchClause);
        var catchPattern = Descendants(tree.Root).OfType<PatternSyntax>().Single();

        var callInfo = Assert.IsType<TypedErrorSemanticInfo>(model.GetTypedErrorInfo(runCall));
        Assert.True(callInfo.IsCaught);
        Assert.False(callInfo.IsPropagated);
        Assert.Equal("Failure", callInfo.DeclaredErrorType?.Name);

        var catchInfo = Assert.IsType<TypedErrorSemanticInfo>(model.GetCatchInfo(catchClause));
        Assert.True(catchInfo.IsCaught);
        Assert.Equal("Failure", catchInfo.DeclaredErrorType?.Name);
        Assert.Equal("failed", Assert.IsType<PatternSemanticInfo>(model.GetPatternInfo(catchPattern)).EnumCase?.Name);
    }


    [Fact]
    public void ConstructedGenericErrorTypeIsPreservedInTypedErrorSemanticInfo()
    {
        var tree = SyntaxTree.Parse("""
            enum DecodeError<Value>: Error { case invalid(value: Value) }
            func decode() throws DecodeError<Int> { throw DecodeError<Int>.invalid(value: 1) }
            func run() throws DecodeError<Int> { return try decode() }
            """);
        var model = Compilation.Create(tree).GetSemanticModel(tree);
        var runCall = Descendants(tree.Root).OfType<ExpressionSyntax>()
            .Where(node => node.Kind == SyntaxKind.CallExpression)
            .Last();
        var tryExpression = Descendants(tree.Root).OfType<ExpressionSyntax>()
            .Single(node => node.Kind == SyntaxKind.TryExpression);

        var callInfo = Assert.IsType<TypedErrorSemanticInfo>(model.GetTypedErrorInfo(runCall));
        var callError = Assert.IsType<ConstructedTypeSymbol>(callInfo.Effect.ErrorType);
        Assert.Equal("DecodeError", callError.GenericDefinition.Name);
        Assert.Same(TypeSymbol.Int, Assert.Single(callError.TypeArguments));

        var tryInfo = Assert.IsType<TypedErrorSemanticInfo>(model.GetTryInfo(tryExpression));
        var declaredError = Assert.IsType<ConstructedTypeSymbol>(tryInfo.DeclaredErrorType);
        Assert.Equal("DecodeError", declaredError.GenericDefinition.Name);
        Assert.Same(TypeSymbol.Int, Assert.Single(declaredError.TypeArguments));
    }

    [Fact]
    public void ThrowsClauseErrorTypePublishesSemanticReference()
    {
        var tree = SyntaxTree.Parse("""
            enum FileError: Error { case missing }
            func load() throws FileError -> String { throw FileError.missing }
            """);
        var model = Compilation.Create(tree).GetSemanticModel(tree);
        var throwsType = Descendants(tree.Root)
            .Where(node => node.Kind == SyntaxKind.TypeClause)
            .Last();

        var symbol = Assert.IsType<EnumTypeSymbol>(model.GetSymbolInfo(throwsType));
        Assert.Equal("FileError", symbol.Name);
        Assert.Contains(model.GetSymbolReferences(), reference =>
            ReferenceEquals(reference.Symbol, symbol) &&
            reference.Kind == SymbolReferenceKind.Type &&
            reference.Location.Span.Start == tree.Text.ToString().IndexOf("FileError ->", StringComparison.Ordinal));
    }

    private static IEnumerable<SyntaxNode> Descendants(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.GetChildren())
            foreach (var descendant in Descendants(child)) yield return descendant;
    }
}
