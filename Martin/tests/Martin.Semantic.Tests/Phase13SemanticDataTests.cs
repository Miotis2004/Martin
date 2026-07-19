using Martin.Compiler;
using Martin.Compiler.Binding;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase13SemanticDataTests
{
    [Fact]
    public void MatrixCapacityFailureProducesDiagnosticAndErrorAnalysis()
    {
        var cases = string.Join(' ', Enumerable.Range(0, PatternMatrixLimits.Default.MaximumRows + 1)
            .Select(value => $"case {value}: print({value})"));
        var tree = SyntaxTree.Parse($"func inspect(_ value: Int) {{ switch value {{ {cases} }} }}", "large-switch.martin");
        var compilation = Compilation.Create(tree);
        var model = compilation.GetSemanticModel(tree);
        var switchStatement = Descendants(tree.Root).OfType<StatementSyntax>()
            .Single(statement => statement.Kind == SyntaxKind.SwitchStatement);

        var diagnostic = Assert.Single(compilation.Diagnostics, diagnostic => diagnostic.Code == "MRT2197");
        Assert.Equal("Pattern analysis exceeded the supported complexity limit.", diagnostic.Message);
        var analysis = Assert.IsType<SwitchAnalysisResult>(model.GetSwitchAnalysis(switchStatement));
        Assert.True(analysis.HasErrors);
    }

    [Fact]
    public void SemanticModelPublishesNestedPatternAndSwitchInformation()
    {
        var tree = SyntaxTree.Parse("""
            enum Result { case success(Int) case failure }
            func inspect(_ result: Result) {
                switch result { case .success(let value): print(value) case .failure: print(0) }
            }
            """, "patterns.martin");
        var compilation = Compilation.Create(tree);
        var model = compilation.GetSemanticModel(tree);
        var patterns = Descendants(tree.Root).OfType<PatternSyntax>().ToArray();
        var binding = patterns.OfType<ValueBindingPatternSyntax>().Single();
        var switchStatement = Descendants(tree.Root).OfType<StatementSyntax>()
            .Single(statement => statement.Kind == SyntaxKind.SwitchStatement);

        var bindingInfo = Assert.IsType<PatternSemanticInfo>(model.GetPatternInfo(binding));
        Assert.Equal(TypeSymbol.Int, bindingInfo.InputType);
        Assert.IsType<LocalVariableSymbol>(Assert.Single(bindingInfo.DeclaredVariables));
        Assert.NotNull(model.GetSwitchAnalysis(switchStatement));
    }

    [Fact]
    public void CompilationPublishesBothDirectionsOfConformanceQueries()
    {
        var tree = SyntaxTree.Parse("protocol P { func run() } struct S: P { func run() { } }", "protocol.martin");
        var compilation = Compilation.Create(tree);
        var conformance = Assert.Single(compilation.GetConformances());
        var relationship = Assert.Single(conformance.Witnesses);

        Assert.Same(conformance, compilation.GetConformance(conformance.Type, conformance.Protocol));
        Assert.Same(relationship.Value, compilation.GetWitness(relationship.Key, conformance.Type));
        Assert.Contains(relationship.Key, compilation.GetSatisfiedRequirements(relationship.Value));
    }

    private static IEnumerable<SyntaxNode> Descendants(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.GetChildren())
            foreach (var descendant in Descendants(child))
                yield return descendant;
    }
}
