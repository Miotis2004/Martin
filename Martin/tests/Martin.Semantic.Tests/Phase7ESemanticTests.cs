using Xunit;
using Martin.Compiler;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;

namespace Martin.Semantic.Tests;

public class Phase7ESemanticTests
{
    [Fact]
    public void GenericStructMembersUseTypeParameterSymbols()
    {
        var tree = SyntaxTree.Parse("""
struct Box<T> {
    let value: T
}
""");

        var program = Compilation.Create(tree).BindProgram();
        Assert.DoesNotContain(program.Diagnostics, d => d.Severity == Martin.Compiler.Diagnostics.DiagnosticSeverity.Error);
        var box = Assert.IsType<StructTypeSymbol>(Assert.Single(program.NamedTypes));
        Assert.Single(box.TypeParameters);
        Assert.Same(box.TypeParameters[0], Assert.Single(box.Properties).Type);
    }

    [Fact]
    public void DuplicateTypeParametersAreDiagnosed()
    {
        var tree = SyntaxTree.Parse("struct Bad<T, T> { let value: T }");
        var diagnostics = Compilation.Create(tree).Diagnostics;
        Assert.Contains(diagnostics, d => d.Code == "MRT2183");
    }
}
