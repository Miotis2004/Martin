using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Parser.Tests;

public sealed class Phase7DParserTests
{
    [Fact]
    public void ParsesProtocolsRequirementsAndConformanceClauses()
    {
        var tree = SyntaxTree.Parse("""
protocol Named {
    let name: String
}

protocol Resettable {
    var value: Int
    mutating func reset()
}

struct User: Named, Resettable {
    let name: String
    var value: Int

    mutating func reset() {
        value = 0
    }
}
""");

        Assert.Empty(tree.Diagnostics);
        var display = SyntaxTreePrinter.ToDisplayString(tree.Root);
        Assert.Contains("ProtocolDeclaration", display);
        Assert.Contains("ProtocolPropertyRequirement", display);
        Assert.Contains("ProtocolMethodRequirement", display);
        Assert.Contains("ProtocolConformanceClause", display);
    }
}
