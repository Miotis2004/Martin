using Martin.Compiler;
using Martin.Compiler.Generics;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase14GenericTypeBindingTests
{
    [Fact]
    public void BindsNestedGenericTypesInEveryDeclarationPosition()
    {
        var compilation = Compilation.Create(SyntaxTree.Parse("""
protocol Marker {}
struct Item: Marker {}
struct Box<T> { let value: T }
enum Event<T> { case received(Box<T>?) }
func transform(_ input: Box<Box<Item>>) -> Box<Box<Item>> {
    return input
}
func store(_ input: Box<Item>) {
    let local: Box<Item> = input
}
"""));

        var program = compilation.BindProgram();

        Assert.Empty(program.Diagnostics);
        var function = program.Functions.Single(symbol => symbol.Name == "transform");
        Assert.IsType<ConstructedTypeSymbol>(function.Parameters[0].Type);
        var result = Assert.IsType<ConstructedTypeSymbol>(function.ReturnType);
        Assert.IsType<ConstructedTypeSymbol>(Assert.Single(result.TypeArguments));
        var eventType = program.NamedTypes.Single(type => type.Name == "Event");
        var payload = Assert.Single(Assert.Single(eventType.Cases).AssociatedValues).Type;
        Assert.IsType<OptionalTypeSymbol>(payload);
    }

    [Fact]
    public void InvalidNestedArgumentReportsOnlyItsPrimaryDiagnosticAndRecovers()
    {
        var compilation = Compilation.Create(SyntaxTree.Parse("""
struct Box<T> { let value: T }
func use(_ value: Box<Missing>) {}
"""));

        var diagnostics = compilation.Diagnostics;

        Assert.Single(diagnostics.Where(diagnostic => diagnostic.Code == "MRT2014"));
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Code is "MRT2180" or "MRT2185");
        Assert.Equal(TypeSymbol.Error, Assert.Single(compilation.BindProgram().Functions).Parameters[0].Type);
    }

    [Fact]
    public void ValidatesProtocolConstraintsAfterConformancesAreBuilt()
    {
        var compilation = Compilation.Create(SyntaxTree.Parse("""
protocol Marker {}
struct Good: Marker {}
struct Bad {}
struct Box<T: Marker> { let value: T }
func valid(_ value: Box<Good>) {}
func invalid(_ value: Box<Bad>) {}
"""));

        var failures = compilation.Diagnostics.Where(diagnostic => diagnostic.Code == "MRT2304").ToArray();

        var failure = Assert.Single(failures);
        Assert.Contains("Bad", failure.Message);
        Assert.Contains("Marker", failure.Message);
    }

    [Fact]
    public void TrailingCommaRecoversAsAnArityErrorWithoutInventingAnArgument()
    {
        var compilation = Compilation.Create(SyntaxTree.Parse("""
struct Pair<A, B> {}
func use(_ value: Pair<Int,>) {}
"""));

        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == "MRT2180");
    }
}
