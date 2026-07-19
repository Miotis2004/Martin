using System.Collections.Immutable;
using Martin.Compiler.Binding;
using Martin.Compiler.Symbols;
using Martin.Compiler.Text;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class PatternCoverageAnalyzerTests
{
    private static readonly TextLocation Location = new(SourceText.From("pattern", "coverage.martin"), new TextSpan(0, 7));
    private readonly PatternCoverageAnalyzer _analyzer = new();

    [Fact]
    public void BooleanCoverageIsRecursiveAndFindsDuplicateCases()
    {
        var result = Analyze(TypeSymbol.Bool,
                             Literal(true, TypeSymbol.Bool),
                             Literal(true, TypeSymbol.Bool),
                             Literal(false, TypeSymbol.Bool));

        Assert.True(result.IsExhaustive);
        Assert.Equal(1, Assert.Single(result.UnreachableCases).SourceOrdinal);
        Assert.Empty(result.MissingWitnesses);
    }

    [Fact]
    public void NestedOptionalCoverageProducesAUsefulWitness()
    {
        var inner = new OptionalTypeSymbol(TypeSymbol.Bool);
        var outer = new OptionalTypeSymbol(inner);
        var result = Analyze(outer,
                             new BoundNilPattern(outer, Location),
                             new BoundOptionalSomePattern(outer, new BoundNilPattern(inner, Location), Location),
                             new BoundOptionalSomePattern(outer,
                                                          new BoundOptionalSomePattern(inner, Literal(true, TypeSymbol.Bool), Location), Location));

        Assert.False(result.IsExhaustive);
        Assert.Equal([".some(.some(false))"], Render(result));
        Assert.Equal(outer, Assert.Single(result.MissingWitnesses).Type);
        Assert.Equal(inner, Assert.Single(result.MissingWitnesses).Arguments[0].Type);
    }

    [Fact]
    public void EnumPayloadIsNotCoveredByOneNestedPattern()
    {
        var choice = new EnumTypeSymbol("Choice", []);
        var value = new EnumCaseSymbol("value", choice,
                                       [new ParameterSymbol("flag", null, 0, TypeSymbol.Bool, [])], []);
        var empty = new EnumCaseSymbol("empty", choice, [], []);
        choice.AddMember(value);
        choice.AddMember(empty);

        var result = Analyze(choice,
                             new BoundEnumCasePattern(choice, value, [Literal(true, TypeSymbol.Bool)], Location),
                             new BoundEnumCasePattern(choice, empty, [], Location));

        Assert.False(result.IsExhaustive);
        Assert.Equal([".value(false)"], Render(result));
    }

    [Fact]
    public void BroadNestedPatternSubsumesLaterSpecificPattern()
    {
        var optional = new OptionalTypeSymbol(TypeSymbol.Bool);
        var result = Analyze(optional,
                             new BoundOptionalSomePattern(optional, new BoundWildcardPattern(TypeSymbol.Bool, Location), Location),
                             new BoundOptionalSomePattern(optional, Literal(true, TypeSymbol.Bool), Location),
                             new BoundNilPattern(optional, Location));

        Assert.True(result.IsExhaustive);
        Assert.Equal(1, Assert.Single(result.UnreachableCases).SourceOrdinal);
    }

    [Fact]
    public void OpenScalarRequiresWildcardAndWitnessCountIsBounded()
    {
        var result = new PatternCoverageAnalyzer(maximumWitnesses: 1).Analyze(TypeSymbol.Int, [Literal(1, TypeSymbol.Int), Literal(2, TypeSymbol.Int)]);

        Assert.False(result.IsExhaustive);
        Assert.Equal(["_"], Render(result));
        var withWildcard = Analyze(TypeSymbol.Int, Literal(1, TypeSymbol.Int), new BoundWildcardPattern(TypeSymbol.Int, Location), Literal(2, TypeSymbol.Int));
        Assert.True(withWildcard.IsExhaustive);
        Assert.Equal(2, Assert.Single(withWildcard.UnreachableCases).SourceOrdinal);
    }

    [Fact]
    public void ErrorPatternsNeverProveCoverage()
    {
        var result = Analyze(TypeSymbol.Bool,
                             new BoundWildcardPattern(TypeSymbol.Bool, Location, hasErrors: true));

        Assert.False(result.IsExhaustive);
        Assert.Equal(["false", "true"], Render(result));
        Assert.Empty(result.UnreachableCases);
    }

    [Fact]
    public void UnreachableOutputIsBoundedForLargeMalformedSwitches()
    {
        var patterns = Enumerable.Repeat<BoundPattern>(
                                     new BoundWildcardPattern(TypeSymbol.Bool, Location), 1_000)
                           .ToImmutableArray();

        var result = new PatternCoverageAnalyzer(maximumUnreachableCases: 16)
                         .Analyze(TypeSymbol.Bool, patterns);

        Assert.True(result.IsExhaustive);
        Assert.Equal(16, result.UnreachableCases.Length);
    }

    private SwitchAnalysisResult Analyze(TypeSymbol type, params BoundPattern[] patterns) =>
        _analyzer.Analyze(type, patterns.ToImmutableArray());

    private static string[] Render(SwitchAnalysisResult result) =>
        result.MissingWitnesses.Select(MissingPatternWitnessDiagnosticRenderer.Render).ToArray();

    private static BoundLiteralPattern Literal(object value, TypeSymbol type) => new(value, type, Location);
}
