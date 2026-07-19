using Martin.Compiler;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase7DSemanticTests
{
    static Compilation Compile(string source) => Compilation.Create(SyntaxTree.Parse(source, "test.martin"));

    [Fact]
    public void ValidatesProtocolConformanceAndWitnesses()
    {
        var program = Compile("""
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
""").BindProgram();

        Assert.Empty(program.Diagnostics);
        Assert.Equal(2, program.Conformances.Length);
        Assert.All(program.Conformances, c => Assert.NotEmpty(c.Witnesses));
    }

    [Theory]
    [InlineData("protocol P { func run() } struct S: P { }", "MRT2161")]
    [InlineData("protocol P { var name: String } struct S: P { let name: String }", "MRT2165")]
    [InlineData("protocol P { mutating func reset() } struct S: P { func reset() { } }", "MRT2166")]
    [InlineData("struct S: Int { }", "MRT2163")]
    [InlineData("protocol P { func run() } struct S: P, P { func run() { } }", "MRT2164")]
    [InlineData("protocol P { func run() } func main() { let p: P = nil }", "MRT2167")]
    public void ReportsProtocolDiagnostics(string source, string code)
    {
        Assert.Contains(Compile(source).BindProgram().Diagnostics, d => d.Code == code);
    }
}
