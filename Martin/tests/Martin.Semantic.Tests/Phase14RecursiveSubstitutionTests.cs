using System.Collections.Immutable;
using Martin.Compiler.Generics;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase14RecursiveSubstitutionTests
{
    [Fact]
    public void ConstructedMembersRecursivelySubstituteEverySignaturePosition()
    {
        var pair = GenericDefinition("Pair", 2);
        var container = GenericDefinition("Container", 1);
        var outerParameter = container.TypeParameters[0];
        var methodParameter = new TypeParameterSymbol("U", 0, container, []);
        var nestedReturn = new ConstructedTypeSymbol(pair,
                                                     [new OptionalTypeSymbol(outerParameter), methodParameter], []);
        var declaration = SyntaxTree.Parse(string.Empty).Root;
        var method = new MethodSymbol("convert", container,
                                      [new ParameterSymbol("value", null, 0, outerParameter, [], true, true)],
                                      nestedReturn, declaration, false, [], true, new OptionalTypeSymbol(outerParameter),
                                      [methodParameter], [new ProtocolConstraint(new ProtocolTypeSymbol("Displayable", []))]);
        container.AddMember(new PropertySymbol("value", container,
                                               new OptionalTypeSymbol(new ConstructedTypeSymbol(pair, [outerParameter, TypeSymbol.String], [])),
                                               true, null, []));
        container.AddMember(method);
        container.AddMember(new InitializerSymbol(container,
                                                  [new ParameterSymbol("value", "value", 0, outerParameter, [])], declaration, false, [],
                                                  true, outerParameter));
        container.AddMember(new EnumCaseSymbol("item", container,
                                               [new ParameterSymbol("payload", null, 0, new OptionalTypeSymbol(outerParameter), [])], []));

        var constructed = new GenericTypeFactory().Construct(container, [TypeSymbol.Int]);

        var propertyType = Assert.IsType<OptionalTypeSymbol>(Assert.Single(constructed.Properties).Type);
        var propertyPair = Assert.IsType<ConstructedTypeSymbol>(propertyType.ElementType);
        Assert.Same(TypeSymbol.Int, propertyPair.TypeArguments[0]);
        Assert.Same(TypeSymbol.String, propertyPair.TypeArguments[1]);

        var substitutedMethod = Assert.Single(constructed.Methods);
        Assert.Same(constructed, substitutedMethod.ContainingType);
        Assert.Same(TypeSymbol.Int, substitutedMethod.Parameters[0].Type);
        Assert.True(substitutedMethod.Parameters[0].IsVariadic);
        Assert.True(substitutedMethod.Parameters[0].HasDefaultValue);
        var returnType = Assert.IsType<ConstructedTypeSymbol>(substitutedMethod.ReturnType);
        Assert.Same(TypeSymbol.Int, Assert.IsType<OptionalTypeSymbol>(returnType.TypeArguments[0]).ElementType);
        Assert.Same(methodParameter, returnType.TypeArguments[1]);
        Assert.Same(method, substitutedMethod.OriginalDefinition);
        Assert.Same(methodParameter, Assert.Single(substitutedMethod.TypeParameters));
        Assert.Same(TypeSymbol.Int, Assert.IsType<OptionalTypeSymbol>(substitutedMethod.ErrorType).ElementType);

        var initializer = Assert.Single(constructed.Initializers);
        Assert.Same(constructed, initializer.ContainingType);
        Assert.Same(TypeSymbol.Int, initializer.Parameters[0].Type);
        Assert.Same(TypeSymbol.Int, initializer.ErrorType);
        Assert.Same(TypeSymbol.Int,
                    Assert.IsType<OptionalTypeSymbol>(Assert.Single(constructed.Cases).AssociatedValues[0].Type).ElementType);
    }

    [Fact]
    public void FunctionSubstitutionComposesMappingsAndPreservesDefinition()
    {
        var owner = new StructTypeSymbol("Owner", [], []);
        var first = new TypeParameterSymbol("T", 0, owner, []);
        var second = new TypeParameterSymbol("U", 1, owner, []);
        var function = new FunctionSymbol("transform",
                                          [new ParameterSymbol("input", null, 0, new OptionalTypeSymbol(first), [])],
                                          second, null, false, [], [first, second], true, first);
        var substitution = new TypeSubstitution(
            ImmutableDictionary<TypeParameterSymbol, TypeSymbol>.Empty.Add(first, second).Add(second, TypeSymbol.String),
            new GenericTypeFactory());

        var result = substitution.Substitute(function);

        Assert.Same(TypeSymbol.String,
                    Assert.IsType<OptionalTypeSymbol>(Assert.Single(result.Parameters).Type).ElementType);
        Assert.Same(TypeSymbol.String, result.ReturnType);
        Assert.Same(TypeSymbol.String, result.ErrorType);
        Assert.Same(function, result.OriginalDefinition);
    }

    [Fact]
    public void ErrorAndCyclicRecoveryMappingsDoNotCrashSubstitution()
    {
        var definition = GenericDefinition("Box", 1);
        var parameter = definition.TypeParameters[0];
        var openBox = new ConstructedTypeSymbol(definition, [parameter], []);
        var errors = new TypeSubstitution(
            ImmutableDictionary<TypeParameterSymbol, TypeSymbol>.Empty.Add(parameter, TypeSymbol.Error),
            new GenericTypeFactory());
        var cycle = new TypeSubstitution(
            ImmutableDictionary<TypeParameterSymbol, TypeSymbol>.Empty.Add(parameter, parameter),
            new GenericTypeFactory());

        Assert.Same(TypeSymbol.Error, errors.Substitute(openBox));
        Assert.Same(TypeSymbol.Error, errors.Substitute(TypeSymbol.Error));
        Assert.Same(parameter, cycle.Substitute(parameter));
    }

    private static StructTypeSymbol GenericDefinition(string name, int arity)
    {
        var placeholder = new StructTypeSymbol(name, [], []);
        var parameters = Enumerable.Range(0, arity)
                             .Select(index => new TypeParameterSymbol($"T{index}", index, placeholder, []))
                             .ToImmutableArray();
        var definition = new StructTypeSymbol(name, [], parameters);
        foreach (var parameter in parameters)
            parameter.SetContainingSymbol(definition);
        return definition;
    }
}
