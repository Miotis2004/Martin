using Martin.Compiler;
using Martin.Compiler.Binding;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase13ExactConformanceTests
{
    static BoundProgram Compile(string source) => Compilation.Create(SyntaxTree.Parse(source, "conformance.martin")).BindProgram();

    [Fact]
    public void PublishesBothDirectionsOfAnExactWitnessRelationship()
    {
        var program = Compile("""
protocol P { func convert(named value: Int) throws Error -> String }
struct S: P { func convert(named value: Int) throws Error -> String { return "ok" } }
""");

        Assert.Empty(program.Diagnostics);
        var conformance = Assert.Single(program.Conformances);
        Assert.True(Assert.Single(program.ConformanceAttempts).IsValid);
        var relationship = Assert.Single(conformance.Witnesses);
        Assert.Same(relationship.Key, Assert.Single(conformance.RequirementsByWitness[relationship.Value]));
    }

    [Theory]
    [InlineData("func convert(_ value: Int) -> String", "func convert(named value: Int) -> String { return \"ok\" }", "MRT2171")]
    [InlineData("func convert(_ value: Int) -> String", "func convert(_ value: String) -> String { return \"ok\" }", "MRT2172")]
    [InlineData("func convert() -> String", "func convert() -> Int { return 0 }", "MRT2173")]
    [InlineData("func convert() throws Error", "func convert() { }", "MRT2174")]
    [InlineData("mutating func convert()", "func convert() { }", "MRT2166")]
    [InlineData("var value: Int", "let value: Int", "MRT2165")]
    public void ReportsTheExactMismatch(string requirement, string witness, string code)
    {
        var program = Compile($"protocol P {{ {requirement} }} struct S: P {{ {witness} }}");

        Assert.Contains(program.Diagnostics, diagnostic => diagnostic.Code == code);
        Assert.Empty(program.Conformances);
        var attempt = Assert.Single(program.ConformanceAttempts);
        Assert.False(attempt.IsValid);
        Assert.Single(attempt.Mismatches);
        Assert.Equal("conformance.martin", attempt.DeclarationLocation.FilePath);
    }

    [Fact]
    public void ConformanceAndWitnessCanBeDeclaredInDifferentFiles()
    {
        var protocolTree = SyntaxTree.Parse("protocol P { func run(_ value: Int) }", "protocol.martin");
        var typeTree = SyntaxTree.Parse("struct S: P { func run(_ value: Int) { } }", "type.martin");
        var program = Compilation.Create(protocolTree, typeTree).BindProgram();

        Assert.Empty(program.Diagnostics);
        Assert.Single(Assert.Single(program.Conformances).Witnesses);
    }

    [Fact]
    public void UnknownProtocolDoesNotProduceASecondNotProtocolDiagnostic()
    {
        var program = Compile("struct S: Missing { }");

        Assert.Contains(program.Diagnostics, diagnostic => diagnostic.Code == "MRT2014");
        Assert.DoesNotContain(program.Diagnostics, diagnostic => diagnostic.Code == "MRT2163");
        Assert.Empty(program.Conformances);
    }
}
