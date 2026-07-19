using Martin.Compiler;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase13ProtocolRequirementSymbolTests
{
    [Fact]
    public void PublishesCompleteMethodAndPropertyRequirementMetadata()
    {
        var tree = SyntaxTree.Parse("""
protocol Transformer {
    let identifier: String
    var value: Int
    mutating func transform<T: Error>(_ input: T, named output: String) throws Error -> String
}
""", "requirements.martin");
        var compilation = Compilation.Create(tree);
        var program = compilation.BindProgram();

        Assert.Empty(program.Diagnostics);
        var protocol = Assert.IsType<ProtocolTypeSymbol>(Assert.Single(program.NamedTypes));
        var properties = protocol.Requirements.OfType<ProtocolPropertyRequirementSymbol>().ToArray();
        Assert.True(properties[0].RequiresGetter);
        Assert.False(properties[0].RequiresSetter);
        Assert.Same(TypeSymbol.String, properties[0].PropertyType);
        Assert.True(properties[1].RequiresGetter);
        Assert.True(properties[1].RequiresSetter);

        var method = Assert.Single(protocol.Requirements.OfType<ProtocolMethodRequirementSymbol>());
        Assert.Same(protocol, method.ContainingProtocol);
        Assert.True(method.IsMutating);
        Assert.True(method.IsThrowing);
        Assert.Equal("Error", method.ErrorType?.Name);
        Assert.Equal("String", method.ReturnType.Name);
        Assert.Equal(new string?[] { null, "named" }, method.Parameters.Select(parameter => parameter.Label));
        Assert.Equal(new[] { "input", "output" }, method.Parameters.Select(parameter => parameter.LocalName));
        Assert.Equal(new[] { 0, 1 }, method.Parameters.Select(parameter => parameter.Ordinal));
        Assert.All(method.Parameters, parameter => Assert.NotNull(parameter.DeclarationLocation));
        Assert.Single(method.TypeParameters);
        Assert.Single(method.GenericRequirements);
        Assert.NotNull(method.DeclarationLocation);
        Assert.Contains("Transformer::method:transform`1", method.StableId);

        var model = compilation.GetSemanticModel(tree);
        Assert.All(protocol.Requirements, requirement => Assert.Contains(requirement, model.GetDeclaredSymbols()));
    }

    [Fact]
    public void RequirementIdsAreDeterministicAndOverloadsRemainDistinct()
    {
        const string source = "protocol P { func read(_ value: Int) -> Int func read(named value: String) -> String }";
        static string[] Ids(string text) => Compilation.Create(SyntaxTree.Parse(text, "same.martin"))
            .BindProgram().NamedTypes.OfType<ProtocolTypeSymbol>().Single().Requirements
            .Select(requirement => requirement.StableId).ToArray();

        var first = Ids(source);
        var second = Ids(source);
        Assert.Equal(first, second);
        Assert.Equal(2, first.Distinct().Count());
    }

    [Theory]
    [InlineData("protocol P { func run() func run() }", "MRT2170")]
    [InlineData("protocol P { static func run() }", "MRT2179")]
    [InlineData("protocol P { static var value: Int }", "MRT2179")]
    public void ReportsRequirementShapeDiagnostics(string source, string code)
    {
        var diagnostics = Compilation.Create(SyntaxTree.Parse(source)).BindProgram().Diagnostics;
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == code);
    }
}
