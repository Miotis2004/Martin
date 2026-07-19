using Xunit;
using Martin.Compiler.Syntax;

namespace Martin.Parser.Tests;

public class Phase7EParserTests
{
    [Fact]
    public void ParsesGenericTypeAndFunctionDeclarations()
    {
        var tree = SyntaxTree.Parse("""
struct Box<T> {
    let value: T
}
func identity<T>(_ value: T) -> T {
    return value
}
""");

        Assert.Empty(tree.Diagnostics);
        Assert.Contains(tree.Root.Members, m => m.Kind == SyntaxKind.StructDeclaration && m.GetChildren().Any(c => c.Kind == SyntaxKind.TypeParameterList));
        Assert.Contains(tree.Root.Members, m => m.Kind == SyntaxKind.FunctionDeclaration && m.GetChildren().Any(c => c.Kind == SyntaxKind.TypeParameterList));
    }

    [Fact]
    public void ParsesConstructedGenericTypes()
    {
        var tree = SyntaxTree.Parse("""
struct Pair<A, B> {
    let first: A
    let second: B
}
let pair: Pair<Int, String> = Pair(1, "two")
""");

        Assert.Empty(tree.Diagnostics);
        Assert.Contains(tree.Root.GetChildren().SelectMany(Flatten), n => n.Kind == SyntaxKind.GenericName);
    }

    static IEnumerable<SyntaxNode> Flatten(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.GetChildren())
        foreach (var nested in Flatten(child))
            yield return nested;
    }
}
