using Martin.Compiler;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase7BSemanticTests
{
    static Compilation Compile(string source) => Compilation.Create(SyntaxTree.Parse(source, "test.martin"));

    [Fact]
    public void BindsNilInjectionAndIfLetUnwrappedScope()
    {
        var program = Compile("""
func main() {
    let count: Int? = 10
    let missing: Int? = nil
    if let value = count {
        print(value)
    }
    if missing == nil {
        print(0)
    }
}
""").BindProgram();

        Assert.Empty(program.Diagnostics);
    }

    [Theory]
    [InlineData("func main() { let value = nil }", "MRT2123")]
    [InlineData("func main() { let value: Int = nil }", "MRT2022")]
    [InlineData("struct User { let name: String } func main() { let user: User? = nil print(user.name) }", "MRT2126")]
    [InlineData("func main() { let value = 1 if let x = value { print(x) } }", "MRT2122")]
    public void ReportsOptionalDiagnostics(string source, string code)
    {
        Assert.Contains(Compile(source).BindProgram().Diagnostics, d => d.Code == code);
    }
}
