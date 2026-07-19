using Martin.Compiler.Binding;
using Martin.Compiler.Symbols;
using Martin.Compiler.Text;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class BoundPatternTests
{
    private static readonly TextLocation Location = new(SourceText.From("pattern", "test.martin"), new TextSpan(0, 7));

    [Fact]
    public void NestedPatternsCollectVariablesAndErrors()
    {
        var enumType = new EnumTypeSymbol("Result", []);
        var parameter = new ParameterSymbol("value", null, 0, TypeSymbol.Int, []);
        var enumCase = new EnumCaseSymbol("success", enumType, [parameter], []);
        enumType.AddMember(enumCase);
        var variable = new LocalVariableSymbol("value", true, TypeSymbol.Int, [Location]);
        var binding = new BoundValueBindingPattern(variable, TypeSymbol.Int, Location);
        var pattern = new BoundEnumCasePattern(enumType, enumCase, [binding], Location);

        Assert.Equal(TypeSymbol.Int, binding.InputType);
        Assert.True(binding.IsIrrefutable);
        Assert.Same(variable, Assert.Single(pattern.DeclaredVariables));
        Assert.False(pattern.HasErrors);
        Assert.Equal(".success(let value)", BoundPatternPrinter.Print(pattern));
    }

    [Fact]
    public void ChildErrorsPropagateThroughRecursivePatterns()
    {
        var optional = new OptionalTypeSymbol(TypeSymbol.Bool);
        var invalidChild = new BoundLiteralPattern(true, TypeSymbol.Bool, Location, hasErrors: true);
        var pattern = new BoundOptionalSomePattern(optional, invalidChild, Location);

        Assert.True(pattern.HasErrors);
        Assert.Equal(optional, pattern.InputType);
        Assert.Equal(".some(true /* error */) /* error */", BoundPatternPrinter.Print(pattern));
    }

    [Fact]
    public void VisitorDispatchesToConcretePattern()
    {
        var visitor = new KindVisitor();

        Assert.Equal("nil", visitor.Visit(new BoundNilPattern(new OptionalTypeSymbol(TypeSymbol.String), Location)));
        Assert.Equal("wildcard", visitor.Visit(new BoundWildcardPattern(TypeSymbol.Int, Location)));
    }

    private sealed class KindVisitor : BoundPatternVisitor<string>
    {
        protected override string DefaultVisit(BoundPattern pattern) => "other";
        public override string VisitNilPattern(BoundNilPattern pattern) => "nil";
        public override string VisitWildcardPattern(BoundWildcardPattern pattern) => "wildcard";
    }
}
