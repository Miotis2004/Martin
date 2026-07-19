using Martin.Compiler;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase7CSemanticTests
{
    static Compilation Compile(string source) => Compilation.Create(SyntaxTree.Parse(source, "test.martin"));

    [Fact]
    public void BindsEnumCaseConstructionAndExhaustiveSwitch()
    {
        var program = Compile("""
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
""").BindProgram();

        Assert.Empty(program.Diagnostics);
    }

    [Theory]
    [InlineData("enum Choice { case yes case no } func main() { let c = Choice.yes() switch c { case .yes: print(1) } }", "MRT2140")]
    [InlineData("enum Choice { case yes case yes }", "MRT2144")]
    [InlineData("enum Choice { case yes(Int) } func main() { let c = Choice.yes() }", "MRT2142")]
    public void ReportsEnumPatternDiagnostics(string source, string code)
    {
        Assert.Contains(Compile(source).BindProgram().Diagnostics, d => d.Code == code);
    }
}
