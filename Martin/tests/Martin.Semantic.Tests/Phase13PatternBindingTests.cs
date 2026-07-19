using Martin.Compiler;
using Martin.Compiler.Binding;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase13PatternBindingTests
{
    private static (Compilation Compilation, SyntaxTree Tree) Compile(string source)
    {
        var tree = SyntaxTree.Parse(source, "patterns.martin");
        return (Compilation.Create(tree), tree);
    }

    [Fact]
    public void NestedBindingsHavePayloadTypesAndAreVisibleOnlyInTheirCase()
    {
        var (compilation, tree) = Compile("""
enum Result { case success(Int) case failure }
func inspect(_ result: Result) {
    switch result {
        case .success(let value): print(value)
        case .failure: print(value)
    }
}
""");
        var model = compilation.GetSemanticModel(tree);
        var binding = Descendants(tree.Root).OfType<ValueBindingPatternSyntax>().Single();
        var symbol = Assert.IsType<LocalVariableSymbol>(model.GetDeclaredSymbol(binding));
        Assert.Equal(TypeSymbol.Int, symbol.Type);
        Assert.True(symbol.IsReadOnly);
        Assert.Single(model.GetSymbolReferences().Where(reference => ReferenceEquals(reference.Symbol, symbol) && reference.Kind != SymbolReferenceKind.Declaration));
        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == "MRT2001" && diagnostic.Message.Contains("value"));
    }

    [Fact]
    public void DuplicateNestedBindingsAreDiagnosed()
    {
        var (compilation, _) = Compile("""
enum Pair { case pair(Int, String) }
func inspect(_ pair: Pair) { switch pair { case .pair(let item, let item): print(item) } }
""");
        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == "MRT2202");
    }

    [Fact]
    public void SwitchCasesRetainBoundPatternsInSourceOrderAndNormalizeDefault()
    {
        var (compilation, _) = Compile("""
enum Result { case success(Int) case failure }
func inspect(_ result: Result) {
    switch result {
        case .success(let value): print(value)
        case .failure: print(0)
        default: print(1)
    }
}
""");

        var function = compilation.BindProgram().Functions.Single(function => function.Name == "inspect");
        var body = compilation.BindProgram().FunctionBodies[function];
        var statement = Assert.IsType<BoundSwitchStatement>(body.Statements.Single());

        Assert.Collection(statement.Cases,
            first => Assert.Equal(".success(let value)", BoundPatternPrinter.Print(first.Pattern)),
            second => Assert.Equal(".failure", BoundPatternPrinter.Print(second.Pattern)),
            third => Assert.IsType<BoundWildcardPattern>(third.Pattern));
        Assert.All(statement.Cases, @case => Assert.Same(statement.Expression.Type, @case.Pattern.InputType));
        Assert.True(statement.Cases[0].Location.Span.Start < statement.Cases[1].Location.Span.Start);
        Assert.True(statement.Cases[1].Location.Span.Start < statement.Cases[2].Location.Span.Start);
    }

    [Fact]
    public void InvalidCaseDoesNotLeakBindingsOrPreventLaterCasesFromBinding()
    {
        var (compilation, _) = Compile("""
enum Result { case success(Int) case failure }
func inspect(_ result: Result) {
    switch result {
        case .success(let value, let extra): print(value)
        case .failure: print(value)
    }
}
""");

        var function = compilation.BindProgram().Functions.Single(function => function.Name == "inspect");
        var statement = Assert.IsType<BoundSwitchStatement>(compilation.BindProgram().FunctionBodies[function].Statements.Single());
        Assert.True(statement.Cases[0].Pattern.HasErrors);
        Assert.False(statement.Cases[1].Pattern.HasErrors);
        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == "MRT2142");
        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == "MRT2001" && diagnostic.Message.Contains("value"));
    }

    [Theory]
    [InlineData("func f(_ value: Int) { switch value { case nil: print(value) } }", "MRT2203")]
    [InlineData("func f(_ value: Int) { switch value { case .some(let item): print(value) } }", "MRT2204")]
    [InlineData("enum E { case one } func f(_ value: E) { switch value { case Other.one: print(value) } }", "MRT2206")]
    [InlineData("func f(_ value: Int) { switch value { case missing: print(value) } }", "MRT2205")]
    [InlineData("func f(_ value: Int) { switch value { case true: print(value) } }", "MRT2201")]
    public void ReportsPatternTypeAndResolutionErrors(string source, string code)
    {
        var (compilation, _) = Compile(source);
        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Theory]
    [InlineData("struct Record { let value: Int } func f(_ input: Record) { switch input { case _: print(0) } }")]
    [InlineData("class Object { } func f(_ input: Object) { switch input { case let value: print(0) } }")]
    public void UnsupportedSwitchInputsAreRejectedBeforePatternAnalysis(string source)
    {
        var (compilation, tree) = Compile(source);
        var diagnostic = Assert.Single(compilation.Diagnostics);
        Assert.Equal("MRT2207", diagnostic.Code);
        Assert.Contains("Switch input type", diagnostic.Message);

        var function = compilation.BindProgram().Functions.Single(function => function.Name == "f");
        var statement = Assert.IsType<BoundSwitchStatement>(
            compilation.BindProgram().FunctionBodies[function].Statements.Single());
        Assert.True(Assert.IsType<SwitchAnalysisResult>(statement.Analysis).HasErrors);
        Assert.Null(statement.DecisionGraph);

        var pattern = Descendants(tree.Root).OfType<PatternSyntax>().Single();
        Assert.Null(compilation.GetSemanticModel(tree).GetPatternInfo(pattern));
    }

    private static IEnumerable<SyntaxNode> Descendants(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.GetChildren())
            foreach (var descendant in Descendants(child))
                yield return descendant;
    }
}
