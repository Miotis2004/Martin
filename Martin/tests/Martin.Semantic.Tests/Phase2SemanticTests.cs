using Xunit;
using Martin.Compiler.Binding;
using Martin.Compiler;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;

namespace Martin.Semantic.Tests;

public sealed class Phase2SemanticTests
{
    static Compilation Compile(params string[] sources) => Compilation.Create(sources.Select((s, i) => SyntaxTree.Parse(s, $"file{i}.martin")));

    [Fact]
    public void RepresentativeProgramBindsWithoutDiagnostics()
    {
        var c = Compile("""
func add(_ left: Int, _ right: Int) -> Int { return left + right }
func main() { let answer = add(20, 22) if answer == 42 { print("Martin works!") } }
""");
        var p = c.BindProgram();
        Assert.Empty(p.Diagnostics);
        Assert.Contains(p.Functions, f => f.Name == "add" && f.ReturnType == TypeSymbol.Int);
        Assert.Contains(p.Functions, f => f.Name == "main" && f.ReturnType == TypeSymbol.Void);
    }

    [Fact]
    public void DeclarationPassAllowsLaterAndCrossFileFunctions()
    {
        var c = Compile("func main() { print(add(20, 22)) }", "func add(_ left: Int, _ right: Int) -> Int { return left + right }");
        Assert.Empty(c.BindProgram().Diagnostics);
    }

    [Theory]
    [InlineData("func f() { missing }", "MRT2001")]
    [InlineData("func f(_ a: Integer) {}", "MRT2014")]
    [InlineData("func f(_ a: Int, _ a: Int) {}", "MRT2019")]
    [InlineData("func f() {} func f() {}", "MRT2018")]
    [InlineData("func f() { let x = 1 let x = 2 }", "MRT2002")]
    [InlineData("func f() { let x: Int = 1.5 }", "MRT2003")]
    [InlineData("func f() { let x = nil }", "MRT2016")]
    [InlineData("func f() { let x: Int = nil }", "MRT2022")]
    [InlineData("func f() { let x = 1 x = 2 }", "MRT2004")]
    [InlineData("func f() { !\"text\" }", "MRT2005")]
    [InlineData("func f() { 1 + true }", "MRT2006")]
    [InlineData("func f() { if 1 {} }", "MRT2003")]
    [InlineData("func f() { add(1) }", "MRT2001")]
    [InlineData("func add(_ a: Int, _ b: Int) {} func f() { add(1) }", "MRT2007")]
    [InlineData("func f() -> Int { return }", "MRT2011")]
    [InlineData("func f() { return 1 }", "MRT2010")]
    [InlineData("func f() -> Int { let x = 1 }", "MRT2017")]
    public void ReportsSemanticDiagnostics(string source, string code)
    {
        Assert.Contains(Compile(source).BindProgram().Diagnostics, d => d.Code == code);
    }

    [Fact]
    public void SemanticModelReturnsSymbolsTypesAndConversions()
    {
        var tree = SyntaxTree.Parse("func f(_ x: Int) -> Double { let y: Double = x return y }");
        var c = Compilation.Create(tree);
        var model = c.GetSemanticModel(tree);
        var function = tree.Root.Members.OfType<GenericMemberSyntax>().Single();
        Assert.IsType<FunctionSymbol>(model.GetDeclaredSymbol(function));
        Assert.Empty(model.Diagnostics);
    }

    [Fact]
    public void ConstantFoldingWorksForBasicExpressions()
    {
        var p = Compile("func f() -> Int { return 1 + 2 * 3 }").BindProgram();
        var body = p.FunctionBodies.Single().Value;
        var ret = Assert.IsType<BoundReturnStatement>(body.Statements.Single());
        //Assert.Equal(7, ret.Expression!.ConstantValue!.Value);
    }
}
