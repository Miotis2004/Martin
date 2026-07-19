using Martin.Compiler;
using Martin.Compiler.Binding;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Syntax;
using Martin.Compiler.Symbols;
using Xunit;

namespace Martin.CodeGeneration.Tests;

public sealed class Phase13DecisionGraphLoweringTests
{
    [Fact]
    public void LowersNestedPatternGraphToBackendNeutralControlFlow()
    {
        var lowered = Lower("""
enum Result { case success(Int?) case failure }
func inspect(_ result: Result) {
    switch result {
        case .success(.some(let value)): print(value)
        case .success(nil): print(0)
        case .failure: print(1)
    }
}
""", "inspect");

        var nodes = Descendants(lowered).ToArray();
        var input = Assert.Single(nodes.OfType<BoundVariableDeclaration>(), declaration =>
            declaration.Variable.Name.StartsWith("$pattern_input_", StringComparison.Ordinal));
        Assert.True(input.IsCompilerGenerated);
        Assert.Same(TypeSymbol.Int, Assert.Single(nodes.OfType<BoundVariableDeclaration>(), declaration =>
            declaration.Variable.Name == "value").Variable.Type);
        Assert.Contains(nodes, node => node is BoundConditionalGotoStatement { Condition: BoundEnumCaseTestExpression });
        Assert.Contains(nodes.OfType<BoundVariableDeclaration>(), declaration =>
            declaration.Initializer is BoundEnumPayloadAccessExpression);
        Assert.Contains(nodes.OfType<BoundVariableDeclaration>(), declaration =>
            declaration.Initializer is BoundGetValueExpression);
        Assert.Contains(nodes, node => node is BoundThrowStatement { IsCompilerGenerated: true });
        Assert.DoesNotContain(nodes, node => node is BoundSwitchStatement or BoundIfLetStatement);
    }

    [Fact]
    public void SwitchInputIsEvaluatedExactlyOnceAndCaseBodiesDoNotFallThrough()
    {
        var lowered = Lower("""
func choose() -> Bool { return true }
func inspect() {
    switch choose() {
        case true: print(1)
        case false: print(0)
    }
}
""", "inspect");

        var nodes = Descendants(lowered).ToArray();
        Assert.Single(nodes.OfType<BoundCallExpression>(), call => call.Function.Name == "choose");
        Assert.Equal(2, nodes.OfType<BoundRuntimeCallExpression>().Count(call =>
            call.MethodName == "Martin.Runtime.MartinConsole.Print"));
        Assert.All(nodes.OfType<BoundConditionalGotoStatement>(), statement => Assert.True(statement.IsCompilerGenerated));
    }

    [Fact]
    public void SharedFailureContinuationsAreLoweredExactlyOnce()
    {
        const string source = """
enum Triple { case values(Int?, Int?, Int?) case empty }
func inspect(_ value: Triple) {
    switch value {
        case .values(.some(1), .some(2), .some(3)): print(1)
        case .values(.some(4), .some(5), .some(6)): print(2)
        case .empty: print(0)
    }
}
""";
        var program = Compilation.Create(SyntaxTree.Parse(source)).BindProgram();
        Assert.DoesNotContain(program.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var function = Assert.Single(program.Functions, symbol => symbol.Name == "inspect");
        var sourceSwitch = Assert.IsType<BoundSwitchStatement>(Assert.Single(program.FunctionBodies[function].Statements));
        var graph = Assert.IsType<PatternDecisionGraph>(sourceSwitch.DecisionGraph);
        var graphNodeCount = DecisionNodes(graph.Entry).Count();

        var lowered = Assert.Single(new CSharpLowerer().Lower(program).LoweredFunctions,
            item => item.Symbol.Name == "inspect").Body;
        var nodes = Descendants(lowered).ToArray();
        Assert.Equal(graphNodeCount, nodes.OfType<BoundLabelStatement>().Count(label =>
            label.Label.StartsWith("__pattern_graph_0_pattern_", StringComparison.Ordinal)));
        Assert.Equal(graphNodeCount, nodes.OfType<BoundLabelStatement>().Select(label => label.Label)
            .Where(label => label.StartsWith("__pattern_graph_0_pattern_", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void IfLetUsesTheSameOptionalExtractionNodesAsPatternLowering()
    {
        var lowered = Lower("""
func inspect(_ value: Int?) {
    if let unwrapped = value { print(unwrapped) }
}
""", "inspect");

        var declaration = Assert.Single(Descendants(lowered).OfType<BoundVariableDeclaration>(), item =>
            item.Variable.Name == "unwrapped");
        Assert.IsType<BoundGetValueExpression>(declaration.Initializer);
        Assert.True(declaration.IsCompilerGenerated);
    }

    private static BoundBlockStatement Lower(string source, string functionName)
    {
        var program = Compilation.Create(SyntaxTree.Parse(source)).BindProgram();
        Assert.DoesNotContain(program.Diagnostics, diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error);
        var lowered = new CSharpLowerer().Lower(program);
        return Assert.Single(lowered.LoweredFunctions, function => function.Symbol.Name == functionName).Body;
    }

    private static IEnumerable<BoundNode> Descendants(BoundNode node)
    {
        yield return node;
        switch (node)
        {
            case BoundBlockStatement block:
                foreach (var statement in block.Statements)
                    foreach (var child in Descendants(statement)) yield return child;
                break;
            case BoundVariableDeclaration declaration:
                foreach (var child in Descendants(declaration.Initializer)) yield return child;
                break;
            case BoundExpressionStatement statement:
                foreach (var child in Descendants(statement.Expression)) yield return child;
                break;
            case BoundIfStatement statement:
                foreach (var child in Descendants(statement.Condition)) yield return child;
                foreach (var child in Descendants(statement.ThenStatement)) yield return child;
                if (statement.ElseStatement is not null)
                    foreach (var child in Descendants(statement.ElseStatement)) yield return child;
                break;
            case BoundConditionalGotoStatement statement:
                foreach (var child in Descendants(statement.Condition)) yield return child;
                break;
            case BoundReturnStatement { Expression: { } expression }:
                foreach (var child in Descendants(expression)) yield return child;
                break;
            case BoundThrowStatement statement:
                foreach (var child in Descendants(statement.Expression)) yield return child;
                break;
            case BoundCallExpression call:
                foreach (var argument in call.Arguments)
                    foreach (var child in Descendants(argument)) yield return child;
                break;
            case BoundRuntimeCallExpression call:
                foreach (var argument in call.Arguments)
                    foreach (var child in Descendants(argument)) yield return child;
                break;
        }
    }

    private static IEnumerable<PatternDecisionNode> DecisionNodes(PatternDecisionNode entry)
    {
        var pending = new Stack<PatternDecisionNode>();
        var visited = new HashSet<PatternDecisionNode>(ReferenceEqualityComparer.Instance);
        pending.Push(entry);
        while (pending.TryPop(out var node))
        {
            if (!visited.Add(node)) continue;
            yield return node;
            foreach (var successor in node.Successors) pending.Push(successor);
        }
    }
}
