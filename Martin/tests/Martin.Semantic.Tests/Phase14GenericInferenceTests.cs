using System.Collections.Immutable;
using Martin.Compiler.Generics;
using Martin.Compiler.Symbols;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase14GenericInferenceTests
{
    private sealed class Owner(string name) : Symbol(name, [])
    {
        public override SymbolKind Kind => SymbolKind.Function;
    }

    [Fact]
    public void InfersDirectOptionalAndNestedConstructedPositionsInOrdinalOrder()
    {
        var owner = new Owner("choose");
        var second = new TypeParameterSymbol("B", 1, owner, []);
        var first = new TypeParameterSymbol("A", 0, owner, []);
        var pair = GenericDefinition("Pair", first, second);
        var factory = new GenericTypeFactory();
        var pattern = factory.Construct(pair, [new OptionalTypeSymbol(first), second]);
        var observed = factory.Construct(pair, [new OptionalTypeSymbol(TypeSymbol.String), TypeSymbol.Int]);

        var result = new GenericInferenceEngine().Infer([second, first],
            [new InferenceEquation(pattern, observed)]);

        Assert.True(result.Succeeded);
        Assert.Equal([first, second], result.TypeParameters);
        Assert.Equal([TypeSymbol.String, TypeSymbol.Int], result.TypeArguments);
    }

    [Fact]
    public void RepeatedConflictingCandidatesAreReportedExactlyOnce()
    {
        var owner = new Owner("choose");
        var parameter = new TypeParameterSymbol("T", 0, owner, []);

        var result = new GenericInferenceEngine().Infer([parameter],
        [
            new(parameter, TypeSymbol.Int),
            new(parameter, TypeSymbol.Int),
            new(parameter, TypeSymbol.String)
        ]);

        Assert.False(result.Succeeded);
        Assert.Equal([TypeSymbol.Int, TypeSymbol.String], Assert.Single(result.Conflicts).Candidates);
        Assert.Contains("Conflicting types", Assert.Single(result.FailureMessages));
    }

    [Fact]
    public void ReportsConflictAndUnresolvedParametersIndependently()
    {
        var owner = new Owner("make");
        var first = new TypeParameterSymbol("A", 0, owner, []);
        var second = new TypeParameterSymbol("B", 1, owner, []);

        var result = new GenericInferenceEngine().Infer([first, second],
        [
            new(first, TypeSymbol.Int),
            new(first, TypeSymbol.String)
        ]);

        Assert.Single(result.Conflicts);
        Assert.Equal(second, Assert.Single(result.UnresolvedParameters));
        Assert.Empty(result.TypeArguments);
        Assert.Equal(2, result.FailureMessages.Length);
    }

    [Fact]
    public void ExpectedTypeEquationUsesTheSameDeterministicRules()
    {
        var owner = new Owner("make");
        var parameter = new TypeParameterSymbol("T", 0, owner, []);

        var result = new GenericInferenceEngine().Infer([parameter],
            [new(parameter, TypeSymbol.String, InferenceSource.ExpectedType)]);

        Assert.True(result.Succeeded);
        Assert.Same(TypeSymbol.String, Assert.Single(result.TypeArguments));
    }

    [Fact]
    public void HonorsCancellationWithoutChangingDeclarationSymbols()
    {
        var owner = new Owner("identity");
        var parameter = new TypeParameterSymbol("T", 0, owner, []);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => new GenericInferenceEngine().Infer(
            [parameter], [new(parameter, TypeSymbol.Int)], cancellation.Token));
        Assert.Empty(parameter.Constraints);
        Assert.Same(owner, parameter.ContainingSymbol);
    }

    private static StructTypeSymbol GenericDefinition(string name, params TypeParameterSymbol[] parameters) =>
        new(name, [], parameters.ToImmutableArray());
}
