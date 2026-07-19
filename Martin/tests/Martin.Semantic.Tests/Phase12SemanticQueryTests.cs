using Martin.Compiler;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Martin.Compiler.Text;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase12SemanticQueryTests
{
    private const string Source = """
struct Counter {
    var value: Int
    mutating func increment(_ amount: Int) { value = value + amount }
}
func add(_ left: Int, _ right: Int) -> Int { return left + right }
func main() { let first = 1 let result = add(first, 2) print(result) }
""";

    private static (SyntaxTree Tree, SemanticModel Model) CreateModel(string source = Source)
    {
        var tree = SyntaxTree.Parse(source, "queries.martin");
        return (tree, Compilation.Create(tree).GetSemanticModel(tree));
    }

    [Fact]
    public void FindsTokensAndNodesWithValidatedBounds()
    {
        var (tree, model) = CreateModel();
        var position = Source.IndexOf("result", StringComparison.Ordinal);

        Assert.Equal("result", model.FindToken(position)!.Text);
        Assert.NotNull(model.FindNode(new TextSpan(position, "result".Length)));
        Assert.Null(model.FindToken(-1));
        Assert.Null(model.FindNode(new TextSpan(tree.Text.Length + 1, 0)));
    }

    [Fact]
    public void EnumeratesDeclarationsAndBoundReferences()
    {
        var (_, model) = CreateModel();

        Assert.Contains(model.GetDeclaredSymbols(), symbol => symbol is FunctionSymbol { Name: "add" });
        Assert.Contains(model.GetDeclaredSymbols(), symbol => symbol is LocalVariableSymbol { Name: "result" });
        Assert.Contains(model.GetSymbolReferences(), reference =>
            reference.Symbol.Name == "add" && reference.Kind == SymbolReferenceKind.Call);
        Assert.Contains(model.GetSymbolReferences(), reference =>
            reference.Symbol.Name == "result" && reference.Kind == SymbolReferenceKind.Read);
        Assert.All(model.GetSymbolReferences(), reference => Assert.Same(model.SyntaxTree.Text, reference.Location.Text));
    }

    [Fact]
    public void LookupHonorsPositionAndLexicalBlocks()
    {
        const string source = "func main() { let outer = 1 if true { let inner = outer print(inner) } print(outer) }";
        var (_, model) = CreateModel(source);
        var inside = source.IndexOf("print(inner)", StringComparison.Ordinal);
        var outside = source.LastIndexOf("print(outer)", StringComparison.Ordinal);

        Assert.Contains(model.LookupSymbols(inside), symbol => symbol.Name == "inner");
        Assert.DoesNotContain(model.LookupSymbols(outside), symbol => symbol.Name == "inner");
        Assert.Contains(model.LookupSymbols(outside), symbol => symbol.Name == "outer");
        Assert.Contains(model.LookupSymbols(outside), symbol => symbol.Name == "print");
    }

    [Fact]
    public void ExposesEnclosingSymbolMembersExpectedTypeAndCandidates()
    {
        var (_, model) = CreateModel();
        var returnPosition = Source.IndexOf("left + right", StringComparison.Ordinal);
        var counter = Assert.IsType<StructTypeSymbol>(model.GetDeclaredSymbols().Single(symbol => symbol.Name == "Counter"));
        var call = Descendants(model.SyntaxTree.Root).Single(node =>
            node.Kind == SyntaxKind.CallExpression && node.ToString().StartsWith("add(first", StringComparison.Ordinal));

        Assert.Equal("add", model.GetEnclosingSymbol(returnPosition)!.Name);
        Assert.Contains(model.LookupMembers(counter, returnPosition), member => member.Name == "increment");
        Assert.Equal(TypeSymbol.Int, model.GetExpectedType(returnPosition));
        Assert.Contains(model.GetInvocationCandidates(call), symbol => symbol.Name == "add");
    }

    [Fact]
    public void CompleteInvocationsReturnEveryBoundCallableKind()
    {
        const string source = "protocol P { func required(_ value: Int) -> Int } struct Box<T> { let value: T init(value: T) { self.value = value } func replace<U>(_ value: U) -> U { return value } } enum Result<T> { case success(value: T) } func use<X: P>(_ p: X) { let box = Box<Int>(value: 1) let text = box.replace<String>(\"x\") let result = Result<Int>.success(value: 1) let required = p.required(1) }";
        var (_, model) = CreateModel(source);
        var calls = Descendants(model.SyntaxTree.Root).Where(node => node.Kind == SyntaxKind.CallExpression).ToArray();

        Assert.IsType<InitializerSymbol>(Assert.Single(model.GetInvocationCandidates(calls[0])));
        var method = Assert.IsType<MethodSymbol>(Assert.Single(model.GetInvocationCandidates(calls[1])));
        Assert.Same(TypeSymbol.String, Assert.Single(method.Parameters).Type);
        Assert.Same(TypeSymbol.String, method.ReturnType);
        Assert.IsType<EnumCaseSymbol>(Assert.Single(model.GetInvocationCandidates(calls[2])));
        Assert.IsType<ProtocolMethodRequirementSymbol>(Assert.Single(model.GetInvocationCandidates(calls[3])));
    }

    private static IEnumerable<SyntaxNode> Descendants(SyntaxNode node) =>
        node.GetChildren().SelectMany(child => new[] { child }.Concat(Descendants(child)));

    [Fact]
    public void QueriesAreSafeForIncompleteSource()
    {
        const string source = "func main() { let value = add(";
        var (_, model) = CreateModel(source);

        Assert.NotNull(model.FindToken(source.Length));
        Assert.NotNull(model.FindNode(source.Length));
        Assert.NotNull(model.LookupSymbols(source.Length));
        Assert.NotNull(model.GetSymbolReferences());
    }

    [Fact]
    public void IncompleteMemberInvocationUsesCalleeAndActualReceiver()
    {
        const string source = "struct Box { func replace(_ value: String, _ count: Int) -> String { return value } } func use(_ box: Box, _ argument: String) { box.replace(argument";
        var (_, model) = CreateModel(source);
        var call = Descendants(model.SyntaxTree.Root).Single(node => node.Kind == SyntaxKind.CallExpression);

        var candidate = Assert.IsType<MethodSymbol>(Assert.Single(model.GetInvocationCandidates(call)));
        Assert.Equal("replace", candidate.Name);
    }
}
