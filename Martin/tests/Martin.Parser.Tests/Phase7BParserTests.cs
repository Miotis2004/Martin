using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Parser.Tests;

public sealed class Phase7BParserTests
{
    [Fact]
    public void ParsesOptionalTypesNilAndIfLet()
    {
        var tree = SyntaxTree.Parse("""
func main() {
    let value: Int?? = nil
    if let count = value {
        print(count)
    } else {
        print(0)
    }
}
""");

        Assert.Empty(tree.Diagnostics);
        var display = SyntaxTreePrinter.ToDisplayString(tree.Root);
        Assert.Contains("OptionalType", display);
        Assert.Contains("NilKeyword", display);
        Assert.Contains("IfLetStatement", display);
        Assert.Contains("ElseClause", display);
    }
}
