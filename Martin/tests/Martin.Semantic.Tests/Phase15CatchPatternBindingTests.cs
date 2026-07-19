using Martin.Compiler;
using Martin.Compiler.Binding;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase15CatchPatternBindingTests
{
    [Fact]
    public void CatchPatternsBindAgainstTheDoBodyErrorTypeAndDeclareScopedPayloads()
    {
        var compilation = Compile("""
            enum LoadError: Error { case missing(String) case denied }
            func load() throws LoadError -> Int { throw LoadError.denied }
            func run() {
                do { let value = try load() }
                catch .missing(let path) { print(path) }
                catch .denied { print(1) }
            }
            """);

        var doCatch = FindOnlyDoCatch(compilation);

        Assert.Equal("LoadError", doCatch.ErrorType.Name);
        var missing = Assert.IsType<BoundEnumCasePattern>(doCatch.CatchClauses[0].Pattern);
        Assert.Equal("missing", missing.Case.Name);
        var variable = Assert.Single(doCatch.CatchClauses[0].Variables);
        Assert.Equal("path", variable.Name);
        Assert.Same(TypeSymbol.String, variable.Type);
        Assert.DoesNotContain(compilation.Diagnostics, diagnostic => diagnostic.Code == "MRT2001" && diagnostic.Message.Contains("path"));
    }

    [Fact]
    public void CatchPatternVariablesDoNotLeakOutsideTheirCatchBody()
    {
        var compilation = Compile("""
            enum LoadError: Error { case missing(String) }
            func load() throws LoadError -> Int { throw LoadError.missing("x") }
            func run() {
                do { let value = try load() }
                catch .missing(let path) { print(path) }
                print(path)
            }
            """);

        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == "MRT2001" && diagnostic.Message.Contains("path"));
    }


    [Fact]
    public void CatchCoverageRequiresAllClosedEnumCases()
    {
        var compilation = Compile("""
            enum LoadError: Error { case missing case denied }
            func load() throws LoadError -> Int { throw LoadError.missing }
            func run() {
                do { let value = try load() }
                catch .missing { print(1) }
            }
            """);

        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == "MRT2193" && diagnostic.Message.Contains("denied"));
    }

    [Fact]
    public void CatchUsefulnessDiagnosesDuplicateAndWildcardCoveredClauses()
    {
        var compilation = Compile("""
            enum LoadError: Error { case missing case denied }
            func load() throws LoadError -> Int { throw LoadError.missing }
            func run() {
                do { let value = try load() }
                catch _ { print(1) }
                catch .missing { print(2) }
            }
            """);

        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == "MRT2198");
    }

    [Fact]
    public void WildcardCatchCoversOpenErrorTypes()
    {
        var compilation = Compile("""
            struct HostError: Error { let code: Int }
            func load() throws HostError -> Int { throw HostError(code: 1) }
            func run() {
                do { let value = try load() }
                catch _ { print(1) }
            }
            """);

        Assert.DoesNotContain(compilation.Diagnostics, diagnostic => diagnostic.Code == "MRT2193");
        Assert.DoesNotContain(compilation.Diagnostics, diagnostic => diagnostic.Code == "MRT2192");
    }

    [Fact]
    public void ExhaustiveCatchClausesProduceDeterministicDecisionGraph()
    {
        var compilation = Compile("""
            enum LoadError: Error { case missing(String) case denied }
            func load() throws LoadError -> Int { throw LoadError.missing("x") }
            func run() {
                do { let value = try load() }
                catch .missing(let path) { print(path) }
                catch .denied { print(1) }
            }
            """);

        var doCatch = FindOnlyDoCatch(compilation);
        Assert.NotNull(doCatch.DecisionGraph);
        var graph = doCatch.DecisionGraph!;

        Assert.Equal("LoadError", graph.InputTemporary.Type.Name);
        Assert.Equal(2, graph.CaseTargets.Length);
        Assert.Equal("missing", graph.CaseTargets[0].Case.Case?.Name);
        Assert.Equal("denied", graph.CaseTargets[1].Case.Case?.Name);
        Assert.Empty(graph.Validate());
        Assert.IsType<TestEnumCaseDecision>(graph.Entry);
        Assert.Contains(ReachableNodes(graph.Entry), node => node is ExtractEnumPayloadDecision);
        Assert.Contains(ReachableNodes(graph.Entry), node => node is BindPatternValueDecision bind && bind.Variable.Name == "path");
    }

    [Fact]
    public void NonExhaustiveCatchClausesDoNotProduceDecisionGraph()
    {
        var compilation = Compile("""
            enum LoadError: Error { case missing case denied }
            func load() throws LoadError -> Int { throw LoadError.missing }
            func run() {
                do { let value = try load() }
                catch .missing { print(1) }
            }
            """);

        Assert.Null(FindOnlyDoCatch(compilation).DecisionGraph);
    }

    private static BoundDoCatchStatement FindOnlyDoCatch(Compilation compilation)
    {
        var body = Assert.Single(compilation.BindProgram().FunctionBodies, pair => pair.Key.Name == "run").Value;
        return Assert.IsType<BoundDoCatchStatement>(Assert.Single(body.Statements));
    }

    private static Compilation Compile(string source) =>
        Compilation.Create(SyntaxTree.Parse(source, "catch-pattern-binding.martin"));

    private static IEnumerable<PatternDecisionNode> ReachableNodes(PatternDecisionNode entry)
    {
        var pending = new Stack<PatternDecisionNode>();
        var visited = new HashSet<PatternDecisionNode>(ReferenceEqualityComparer.Instance);
        pending.Push(entry);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (!visited.Add(node)) continue;
            yield return node;
            foreach (var successor in node.Successors)
                pending.Push(successor);
        }
    }
}
