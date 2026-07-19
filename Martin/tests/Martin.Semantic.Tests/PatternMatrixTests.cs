using Martin.Compiler.Binding;
using Martin.Compiler.Symbols;
using Martin.Compiler.Text;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class PatternMatrixTests
{
    private static readonly TextLocation Location = new(SourceText.From("pattern", "test.martin"), new TextSpan(0, 7));

    [Fact]
    public void DomainsDistinguishFiniteTypesFromOpenScalars()
    {
        var optional = PatternDomain.Create(new OptionalTypeSymbol(TypeSymbol.Bool));
        var boolean = PatternDomain.Create(TypeSymbol.Bool);
        var scalar = PatternDomain.Create(TypeSymbol.Int);

        Assert.True(optional.IsFinite);
        Assert.Equal(["nil", ".some"], optional.Constructors.Select(value => value.Name));
        Assert.True(boolean.IsFinite);
        Assert.Equal(["false", "true"], boolean.Constructors.Select(value => value.Name));
        Assert.True(scalar.HasRemainder);
        Assert.Empty(scalar.Constructors);
    }

    [Fact]
    public void SpecializationExpandsNestedProductSpacesAndBindingsAsWildcards()
    {
        var result = new EnumTypeSymbol("Result", []);
        var success = new EnumCaseSymbol("success", result,
            [new ParameterSymbol("value", null, 0, new OptionalTypeSymbol(TypeSymbol.Bool), [])], []);
        result.AddMember(success);
        var variable = new LocalVariableSymbol("result", true, result, [Location]);
        var matrix = new PatternMatrix([
            new PatternMatrixRow([new BoundValueBindingPattern(variable, result, Location)], 0),
        ]);

        var enumMatrix = matrix.Specialize(PatternConstructor.ForEnumCase(success));
        var optionalMatrix = enumMatrix.Specialize(PatternConstructor.OptionalSome(TypeSymbol.Bool));

        Assert.Equal("[0] _" + Environment.NewLine, optionalMatrix.ToDebugString());
        Assert.Equal(TypeSymbol.Bool, optionalMatrix.Rows[0].Patterns[0].InputType);
    }

    [Fact]
    public void DefaultMatrixKeepsOnlyWildcardRowsAndErrorRowsNeverProveCoverage()
    {
        var error = new BoundLiteralPattern(1, TypeSymbol.Int, Location, hasErrors: true);
        var matrix = new PatternMatrix([
            new PatternMatrixRow([new BoundLiteralPattern(1, TypeSymbol.Int, Location)], 2),
            new PatternMatrixRow([new BoundWildcardPattern(TypeSymbol.Int, Location)], 1),
            new PatternMatrixRow([error], 0),
        ]);

        var remainder = matrix.Default();

        Assert.Single(remainder.Rows);
        Assert.Equal(1, remainder.Rows[0].SourceOrdinal);
        Assert.Single(matrix.Specialize(PatternConstructor.Literal(2)).Rows);
        var errorOnly = new PatternMatrix([new PatternMatrixRow([error], 0)]);
        Assert.Empty(errorOnly.Specialize(PatternConstructor.Literal(1)).Rows);
        Assert.Empty(errorOnly.Default().Rows);
        Assert.StartsWith("[0] 1 /* error */ <error-row>", matrix.ToDebugString());
    }

    [Fact]
    public void MatrixEnforcesShapeAllocationAndCancellationLimits()
    {
        var wildcard = new BoundWildcardPattern(TypeSymbol.Int, Location);
        Assert.Throws<ArgumentException>(() => new PatternMatrix([
            new PatternMatrixRow([wildcard], 0), new PatternMatrixRow([], 1),
        ]));
        Assert.Throws<InvalidOperationException>(() => new PatternMatrix([
            new PatternMatrixRow([wildcard], 0), new PatternMatrixRow([wildcard], 1),
        ], new PatternMatrixLimits(MaximumRows: 1)));
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Throws<OperationCanceledException>(() => new PatternMatrix([
            new PatternMatrixRow([wildcard], 0),
        ], cancellationToken: source.Token));
    }
}
