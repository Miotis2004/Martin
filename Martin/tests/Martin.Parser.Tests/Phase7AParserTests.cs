using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Parser.Tests;

public sealed class Phase7AParserTests
{
    [Fact]
    public void ParsesNominalTypeDeclarationsAndMembers()
    {
        var tree = SyntaxTree.Parse("""
struct Counter {
    var value: Int

    init(value: Int) {
        self.value = value
    }

    mutating func increment() {
        self.value = self.value + 1
    }
}

class User {
    let name: String
}
""");

        Assert.Empty(tree.Diagnostics);
        var display = SyntaxTreePrinter.ToDisplayString(tree.Root);
        Assert.Contains("StructDeclaration", display);
        Assert.Contains("ClassDeclaration", display);
        Assert.Contains("PropertyDeclaration", display);
        Assert.Contains("InitializerDeclaration", display);
        Assert.Contains("MethodDeclaration", display);
        Assert.Contains("MemberAccessExpression", display);
        Assert.Contains("PropertyAssignmentExpression", display);
    }
}
