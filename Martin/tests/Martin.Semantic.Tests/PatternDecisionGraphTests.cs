using System.Collections.Immutable;
using Martin.Compiler;
using Martin.Compiler.Binding;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Martin.Compiler.Text;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class PatternDecisionGraphTests
{
    [Fact]
    public void BoundSwitchPublishesOrderedGraphWithSingleInputTemporary()
    {
        var tree = SyntaxTree.Parse("""
enum Result { case success(Int?) case failure }
func inspect(_ result: Result) {
    switch result {
        case .success(.some(let value)): print(value)
        case .success(nil): print(0)
        case .failure: print(1)
    }
}
""");
        var compilation = Compilation.Create(tree);
        var function = compilation.BindProgram().Functions.Single(symbol => symbol.Name == "inspect");
        var statement = Assert.IsType<BoundSwitchStatement>(
            compilation.BindProgram().FunctionBodies[function].Statements.Single());

        var graph = Assert.IsType<PatternDecisionGraph>(statement.DecisionGraph);
        Assert.Same(statement.Expression, graph.InputExpression);
        Assert.Equal(statement.Expression.Type, graph.InputTemporary.Type);
        Assert.Equal([0, 1, 2], graph.CaseTargets.Select(target => target.CaseIndex));
        Assert.Empty(graph.Validate());

        var nodes = Traverse(graph.Entry).ToArray();
        var enumTest = Assert.IsType<TestEnumCaseDecision>(nodes[0]);
        Assert.Equal("success", enumTest.Case.Name);
        var enumExtraction = Assert.IsType<ExtractEnumPayloadDecision>(enumTest.WhenMatched);
        var optionalTest = Assert.IsType<TestOptionalHasValueDecision>(enumExtraction.Next);
        var optionalExtraction = Assert.IsType<ExtractOptionalValueDecision>(optionalTest.WhenHasValue);
        Assert.IsType<BindPatternValueDecision>(optionalExtraction.Next);
        Assert.Contains(nodes, node => node is FailureDecision);
        Assert.Equal(nodes.Length, nodes.Select(node => node.Label).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void InvalidSwitchDoesNotCreateDecisionGraph()
    {
        var tree = SyntaxTree.Parse("func f(_ value: Int) { switch value { case nil: print(value) } }");
        var compilation = Compilation.Create(tree);
        var function = compilation.BindProgram().Functions.Single(symbol => symbol.Name == "f");
        var statement = Assert.IsType<BoundSwitchStatement>(
            compilation.BindProgram().FunctionBodies[function].Statements.Single());

        Assert.Null(statement.DecisionGraph);
    }

    [Fact]
    public void BuilderObservesCancellation()
    {
        var text = SourceText.From("switch", "cancel.martin");
        var location = new TextLocation(text, new TextSpan(0, text.Length));
        var input = new BoundLiteralExpression(true, TypeSymbol.Bool);
        var @case = new BoundSwitchCase(new BoundWildcardPattern(TypeSymbol.Bool, location),
            new BoundBlockStatement([]), location);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => new PatternDecisionGraphBuilder().Build(
            input, ImmutableArray.Create(@case), location, cancellation.Token));
    }

    private static IEnumerable<PatternDecisionNode> Traverse(PatternDecisionNode entry)
    {
        var pending = new Stack<PatternDecisionNode>();
        var visited = new HashSet<PatternDecisionNode>(ReferenceEqualityComparer.Instance);
        pending.Push(entry);
        while (pending.TryPop(out var node))
        {
            if (!visited.Add(node)) continue;
            yield return node;
            for (var i = node.Successors.Length - 1; i >= 0; i--) pending.Push(node.Successors[i]);
        }
    }
}
