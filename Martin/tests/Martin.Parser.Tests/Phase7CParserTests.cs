using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Parser.Tests;

public sealed class Phase7CParserTests
{
    [Fact]
    public void ParsesEnumsAndSwitchCasePatterns()
    {
        var tree = SyntaxTree.Parse("""
enum Result {
    case success(String)
    case failure(Int, String)
}

func main() {
    let result = Result.success("ok")
    switch result {
    case .success(let value):
        print(value)
    case .failure(let code, let message):
        print(message)
    }
}
""");

        Assert.Empty(tree.Diagnostics);
        var display = SyntaxTreePrinter.ToDisplayString(tree.Root);
        Assert.Contains("EnumDeclaration", display);
        Assert.Contains("EnumCaseDeclaration", display);
        Assert.Contains("SwitchStatement", display);
        Assert.Contains("EnumCasePattern", display);
        Assert.Contains("ValueBindingPattern", display);
    }
}
