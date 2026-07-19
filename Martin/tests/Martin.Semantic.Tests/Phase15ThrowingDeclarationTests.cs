using System.Collections.Immutable;
using Martin.Compiler;
using Martin.Compiler.Analysis.Protocols;
using Martin.Compiler.Generics;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase15ThrowingDeclarationTests
{
    [Fact]
    public void EveryDeclaredCallableCategoryPublishesItsExactErrorType()
    {
        var tree = SyntaxTree.Parse("""
            enum Failure: Error { case failed }
            protocol Worker { func work() throws Failure -> Int }
            struct Service: Worker {
                init() throws Failure { throw Failure.failed }
                func work() throws Failure -> Int { throw Failure.failed }
            }
            func run() throws Failure -> Int { throw Failure.failed }
            """);
        var program = Compilation.Create(tree).BindProgram();
        var failure = program.NamedTypes.Single(type => type.Name == "Failure");
        var service = program.NamedTypes.Single(type => type.Name == "Service");
        var protocol = Assert.IsType<ProtocolTypeSymbol>(program.NamedTypes.Single(type => type.Name == "Worker"));

        AssertCallable(program.Functions.Single(function => function.Name == "run"), failure);
        AssertCallable(service.Methods.Single(method => method.Name == "work"), failure);
        AssertCallable(Assert.Single(service.Initializers), failure);
        AssertCallable(Assert.IsType<ProtocolMethodRequirementSymbol>(Assert.Single(protocol.Requirements)), failure);
    }

    [Fact]
    public void SubstitutionPreservesThrowingMetadataForEveryGenericCallableShape()
    {
        var owner = new StructTypeSymbol("Owner", []);
        var parameter = new TypeParameterSymbol("E", 0, owner, []);
        var substitution = new TypeSubstitution(
            ImmutableDictionary<TypeParameterSymbol, TypeSymbol>.Empty.Add(parameter, TypeSymbol.String),
            new GenericTypeFactory());
        var syntax = SyntaxTree.Parse("func placeholder() {}").Root.Members[0];
        var function = new FunctionSymbol("f", [], TypeSymbol.Void, syntax, false, [], [parameter], true, parameter);
        var method = new MethodSymbol("m", owner, [], TypeSymbol.Void, syntax, false, [], true, parameter, [parameter]);
        var initializer = new InitializerSymbol(owner, [], syntax, false, [], true, parameter);
        var protocol = new ProtocolTypeSymbol("P", []);
        var requirement = new ProtocolMethodRequirementSymbol("r", protocol, [], TypeSymbol.Void, false,
            true, parameter, [parameter], [], "P::r", []);

        AssertCallable(substitution.Substitute(function), TypeSymbol.String);
        AssertCallable(substitution.Substitute(method, owner), TypeSymbol.String);
        AssertCallable(substitution.Substitute(initializer, owner), TypeSymbol.String);
        AssertCallable(substitution.Substitute(requirement), TypeSymbol.String);
    }

    [Fact]
    public void ProtocolWitnessRequiresExactConstructedErrorIdentity()
    {
        var factory = new GenericTypeFactory();
        var genericError = new EnumTypeSymbol("Failure", [],
            [new TypeParameterSymbol("T", 0, TypeSymbol.Error, [])]);
        var intError = factory.Construct(genericError, [TypeSymbol.Int]);
        var stringError = factory.Construct(genericError, [TypeSymbol.String]);
        var protocol = new ProtocolTypeSymbol("P", []);
        var requirement = new ProtocolMethodRequirementSymbol("run", protocol, [], TypeSymbol.Void, false,
            true, intError, [], [], "P::run", []);
        var owner = new StructTypeSymbol("S", []);
        var syntax = SyntaxTree.Parse("func placeholder() {}").Root.Members[0];
        var witness = new MethodSymbol("run", owner, [], TypeSymbol.Void, syntax, false, [], true, stringError);

        var match = ProtocolRequirementMatcher.Match(requirement, [witness], containingTypeIsStruct: true);

        Assert.False(match.IsSuccess);
        Assert.Equal(ConformanceMismatchKind.ErrorType, match.Mismatch?.Kind);
    }

    private static void AssertCallable(Symbol symbol, TypeSymbol expectedErrorType)
    {
        var metadata = symbol switch
        {
            FunctionSymbol function => (function.IsThrowing, function.ErrorType),
            MethodSymbol method => (method.IsThrowing, method.ErrorType),
            InitializerSymbol initializer => (initializer.IsThrowing, initializer.ErrorType),
            ProtocolMethodRequirementSymbol requirement => (requirement.IsThrowing, requirement.ErrorType),
            _ => throw new Xunit.Sdk.XunitException($"Unexpected callable symbol '{symbol.GetType().Name}'.")
        };
        Assert.True(metadata.IsThrowing);
        Assert.Same(expectedErrorType, metadata.ErrorType);
    }
}
