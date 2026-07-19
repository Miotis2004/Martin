using Martin.Compiler;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase14GenericSemanticDataTests
{
    [Fact]
    public void GenericInitializerPublishesConstructedContainingType()
    {
        var tree = SyntaxTree.Parse("struct Box<T> { let value: T init(value: T) { self.value = value } } func main() { let box = Box<Int>(value: 42) }");
        var model = Compilation.Create(tree).GetSemanticModel(tree);
        var call = Descendants(tree.Root).OfType<ExpressionSyntax>().Single(node => node.Kind == SyntaxKind.CallExpression);

        var initializer = Assert.IsType<InitializerSymbol>(model.GetSymbolInfo(call));
        var info = Assert.IsType<GenericSemanticInfo>(model.GetGenericInfo(call));
        Assert.Same(initializer.OriginalDefinition.ContainingType, info.OriginalDefinition);
        var constructed = Assert.IsType<ConstructedTypeSymbol>(info.ConstructedSymbol);
        Assert.Same(initializer.ContainingType, constructed);
        Assert.Equal([TypeSymbol.Int], info.TypeArguments);
        Assert.Equal(constructed.Identity, info.ConstructedTypeIdentity);
    }

    [Fact]
    public void GenericEnumCasePublishesContainingTypeAndSubstitutedPayload()
    {
        var tree = SyntaxTree.Parse("enum Result<T, E> { case success(value: T) case failure(error: E) } func main() { let result = Result<Int, String>.success(value: 42) }");
        var model = Compilation.Create(tree).GetSemanticModel(tree);
        var call = Descendants(tree.Root).OfType<ExpressionSyntax>().Single(node => node.Kind == SyntaxKind.CallExpression);

        var enumCase = Assert.IsType<EnumCaseSymbol>(model.GetSymbolInfo(call));
        Assert.Same(TypeSymbol.Int, Assert.Single(enumCase.AssociatedValues).Type);
        var info = Assert.IsType<GenericSemanticInfo>(model.GetGenericInfo(call));
        Assert.Same(enumCase.OriginalDefinition.ContainingType, info.OriginalDefinition);
        Assert.Same(enumCase.ContainingType, info.ConstructedSymbol);
        Assert.Equal([TypeSymbol.Int, TypeSymbol.String], info.TypeArguments);
        Assert.NotNull(info.ConstructedTypeIdentity);
    }

    [Fact]
    public void InferredCallPublishesDefinitionArgumentsAndSubstitutedSignature()
    {
        var tree = SyntaxTree.Parse("func identity<T>(_ value: T) -> T { return value } func use() -> Int { return identity(42) }");
        var model = Compilation.Create(tree).GetSemanticModel(tree);
        var call = Descendants(tree.Root).OfType<ExpressionSyntax>().Single(node => node.Kind == SyntaxKind.CallExpression);

        var info = Assert.IsType<GenericSemanticInfo>(model.GetGenericInfo(call));
        Assert.True(info.TypeArgumentsWereInferred);
        Assert.Same(TypeSymbol.Int, Assert.Single(info.TypeArguments));
        Assert.Equal("identity", info.OriginalDefinition.Name);
        var signature = Assert.IsType<FunctionSymbol>(info.ConstructedSymbol);
        Assert.Same(TypeSymbol.Int, Assert.Single(signature.Parameters).Type);
        Assert.Same(TypeSymbol.Int, signature.ReturnType);
        Assert.NotEqual(default, info.OriginalDefinitionIdentity);
    }

    [Fact]
    public void ExplicitCallIsDistinguishedAndPublishesConstraintProof()
    {
        var tree = SyntaxTree.Parse("protocol P { func run() } struct S: P { func run() {} } func use<T: P>(_ value: T) -> T { return value } func main(_ value: S) -> S { return use<S>(value) }");
        var model = Compilation.Create(tree).GetSemanticModel(tree);
        var call = Descendants(tree.Root).OfType<ExpressionSyntax>().Single(node => node.Kind == SyntaxKind.CallExpression);

        var info = Assert.IsType<GenericSemanticInfo>(model.GetGenericInfo(call));
        Assert.False(info.TypeArgumentsWereInferred);
        var result = Assert.Single(info.ConstraintResults);
        Assert.True(result.Succeeded);
        Assert.Single(result.Proofs);
    }

    [Fact]
    public void ExplicitArgumentsNotPresentInSignatureArePreservedInDeclarationOrder()
    {
        var tree = SyntaxTree.Parse("func marker<T, U>() -> Void {} func main() -> Void { marker<Int, String>() }");
        var model = Compilation.Create(tree).GetSemanticModel(tree);
        var call = Descendants(tree.Root).OfType<ExpressionSyntax>().Single(node => node.Kind == SyntaxKind.CallExpression);

        var info = Assert.IsType<GenericSemanticInfo>(model.GetGenericInfo(call));
        Assert.False(info.TypeArgumentsWereInferred);
        Assert.Equal([TypeSymbol.Int, TypeSymbol.String], info.TypeArguments);
        Assert.Equal("marker", info.OriginalDefinition.Name);
        Assert.IsType<FunctionSymbol>(info.ConstructedSymbol);
        Assert.Empty(info.ConstraintResults.SelectMany(result => result.Proofs));
    }

    [Fact]
    public void ConstraintOnlyArgumentPublishesItsValidatedConstraintResult()
    {
        var tree = SyntaxTree.Parse("protocol P { func run() } struct S: P { func run() {} } func register<T: P>() -> Void {} func main() -> Void { register<S>() }");
        var model = Compilation.Create(tree).GetSemanticModel(tree);
        var call = Descendants(tree.Root).OfType<ExpressionSyntax>().Single(node => node.Kind == SyntaxKind.CallExpression);

        var info = Assert.IsType<GenericSemanticInfo>(model.GetGenericInfo(call));
        Assert.Same(Assert.IsAssignableFrom<NamedTypeSymbol>(model.GetDeclaredSymbols().Single(symbol => symbol.Name == "S")),
                    Assert.Single(info.TypeArguments));
        var result = Assert.Single(info.ConstraintResults);
        Assert.True(result.Succeeded);
        Assert.Single(result.Proofs);
    }

    [Fact]
    public void ConstructedTypeUsePublishesCanonicalIdentity()
    {
        var tree = SyntaxTree.Parse("struct Box<T> { let value: T } func unwrap(_ box: Box<String>) -> String { return box.value }");
        var model = Compilation.Create(tree).GetSemanticModel(tree);
        var genericName = Descendants(tree.Root).Single(node => node.Kind == SyntaxKind.GenericName);

        var info = Assert.IsType<GenericSemanticInfo>(model.GetGenericInfo(genericName));
        Assert.Equal("Box", info.OriginalDefinition.Name);
        Assert.Same(TypeSymbol.String, Assert.Single(info.TypeArguments));
        Assert.NotNull(info.ConstructedTypeIdentity);
        Assert.Same(model.GetSymbolInfo(genericName), info.ConstructedSymbol);
    }

    private static IEnumerable<SyntaxNode> Descendants(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.GetChildren())
            foreach (var descendant in Descendants(child))
                yield return descendant;
    }
}
