using Martin.Compiler;
using Martin.Compiler.Binding;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase14GenericPatternTests
{
    [Fact]
    public void ConstructedEnumSwitchIsExhaustiveAndSubstitutesPayloadBindings()
    {
        var statement = BindSwitch("""
            enum Result<T> { case success(T) case failure }
            func inspect(_ result: Result<Int>) {
                switch result {
                    case .success(let value): print(value)
                    case .failure: print(0)
                }
            }
            """, out var program);

        Assert.Empty(program.Diagnostics);
        Assert.True(statement.Analysis!.IsExhaustive);
        var success = Assert.IsType<BoundEnumCasePattern>(statement.Cases[0].Pattern);
        Assert.IsType<ConstructedTypeSymbol>(success.InputType);
        Assert.Same(TypeSymbol.Int, Assert.Single(success.Case.AssociatedValues).Type);
        Assert.Same(TypeSymbol.Int, Assert.Single(success.DeclaredVariables).Type);
        Assert.Same(success.Case.OriginalDefinition,
            program.NamedTypes.Single(type => type.Name == "Result").Cases.Single(@case => @case.Name == "success"));
    }

    [Fact]
    public void MissingWitnessNamesTheConstructedEnumType()
    {
        var statement = BindSwitch("""
            enum Result<T> { case success(T) case failure }
            func inspect(_ result: Result<String>) {
                switch result { case .success(let value): print(value) }
            }
            """, out var program);

        var diagnostic = Assert.Single(program.Diagnostics, item => item.Code == "MRT2140");
        Assert.Contains("Result<String>.failure", diagnostic.Message);
        Assert.Equal("Result<String>.failure",
            MissingPatternWitnessDiagnosticRenderer.Render(Assert.Single(statement.Analysis!.MissingWitnesses)));
    }

    [Fact]
    public void ConstructedEnumDecisionGraphRetainsOneInputAndConcretePayloadTemporary()
    {
        var statement = BindSwitch("""
            enum Result<T> { case success(T) case failure }
            func inspect(_ result: Result<Int>) {
                switch result {
                    case .success(let value): print(value)
                    case .failure: print(0)
                }
            }
            """, out var program);

        Assert.Empty(program.Diagnostics);
        var graph = Assert.IsType<PatternDecisionGraph>(statement.DecisionGraph);
        Assert.Same(statement.Expression, graph.InputExpression);
        Assert.Equal(statement.Expression.Type, graph.InputTemporary.Type);
        var test = Assert.IsType<TestEnumCaseDecision>(graph.Entry);
        var extraction = Assert.IsType<ExtractEnumPayloadDecision>(test.WhenMatched);
        Assert.Same(TypeSymbol.Int, extraction.Destination.Type);
        Assert.Empty(graph.Validate());
    }

    private static BoundSwitchStatement BindSwitch(string source, out BoundProgram program)
    {
        program = Compilation.Create(SyntaxTree.Parse(source)).BindProgram();
        var function = program.Functions.Single(symbol => symbol.Name == "inspect");
        return Assert.IsType<BoundSwitchStatement>(Assert.Single(program.FunctionBodies[function].Statements));
    }
}
