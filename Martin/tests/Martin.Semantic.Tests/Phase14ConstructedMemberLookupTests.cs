using System.Collections.Immutable;
using Martin.Compiler;
using Martin.Compiler.Generics;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase14ConstructedMemberLookupTests
{
    [Fact]
    public void LookupReturnsCanonicalMembersWithDefinitionLocations()
    {
        var tree = SyntaxTree.Parse("struct Box<T> { let value: T }", "box.martin");
        var compilation = Compilation.Create(tree);
        var definition = Assert.Single(compilation.BindProgram().NamedTypes);
        var constructed = compilation.GenericTypes.Construct(definition, [TypeSymbol.Int]);

        var first = Assert.Single(compilation.GetSemanticModel(tree).LookupMembers(constructed, 0));
        var second = Assert.Single(compilation.GetSemanticModel(tree).LookupMembers(constructed, tree.Text.Length));
        var property = Assert.IsType<PropertySymbol>(first);

        Assert.Same(first, second);
        Assert.Same(TypeSymbol.Int, property.Type);
        Assert.Same(constructed, property.ContainingType);
        Assert.Same(Assert.Single(definition.Properties), property.OriginalDefinition);
        Assert.Equal(definition.Locations, constructed.Locations);
        Assert.Equal(property.OriginalDefinition.Locations, property.Locations);
    }

    [Fact]
    public void ContainingAndMethodSubstitutionsComposeIndependently()
    {
        var pair = GenericDefinition("Pair", 2);
        var container = GenericDefinition("Container", 1);
        var outer = container.TypeParameters[0];
        var methodParameter = new TypeParameterSymbol("U", 0, container, []);
        var declaration = SyntaxTree.Parse(string.Empty).Root;
        var definition = new MethodSymbol("convert", container,
                                          [new ParameterSymbol("value", null, 0, methodParameter, [])],
                                          new ConstructedTypeSymbol(pair, [outer, methodParameter], []), declaration, false, [],
                                          typeParameters: [methodParameter]);
        methodParameter.SetContainingSymbol(definition);
        container.AddMember(definition);
        var factory = new GenericTypeFactory();

        var constructedType = factory.Construct(container, [TypeSymbol.Int]);
        var openMethod = Assert.Single(constructedType.Methods);
        var closedMethod = new TypeSubstitution(
                               ImmutableDictionary<TypeParameterSymbol, TypeSymbol>.Empty.Add(methodParameter, TypeSymbol.String),
                               factory)
                               .Substitute(openMethod, constructedType);

        Assert.Same(methodParameter, Assert.Single(openMethod.TypeParameters));
        Assert.Same(TypeSymbol.String, Assert.Single(closedMethod.Parameters).Type);
        var result = Assert.IsType<ConstructedTypeSymbol>(closedMethod.ReturnType);
        Assert.Equal([TypeSymbol.Int, TypeSymbol.String], result.TypeArguments);
        Assert.Same(constructedType, closedMethod.ContainingType);
        Assert.Same(definition, closedMethod.OriginalDefinition);
    }

    [Fact]
    public void ConstructionSubstitutesInitializersAndEnumCasePayloads()
    {
        var definition = GenericDefinition("Result", 1);
        var parameter = definition.TypeParameters[0];
        definition.AddMember(new InitializerSymbol(definition,
                                                   [new ParameterSymbol("value", "value", 0, parameter, [])], null, false, []));
        definition.AddMember(new EnumCaseSymbol("success", definition,
                                                [new ParameterSymbol("value", null, 0, new OptionalTypeSymbol(parameter), [])], []));

        var constructed = new GenericTypeFactory().Construct(definition, [TypeSymbol.String]);

        Assert.Same(TypeSymbol.String, Assert.Single(constructed.Initializers).Parameters[0].Type);
        Assert.Same(TypeSymbol.String,
                    Assert.IsType<OptionalTypeSymbol>(Assert.Single(constructed.Cases).AssociatedValues[0].Type).ElementType);
    }

    private static StructTypeSymbol GenericDefinition(string name, int arity)
    {
        var owner = new StructTypeSymbol(name, [], []);
        var parameters = Enumerable.Range(0, arity)
                             .Select(index => new TypeParameterSymbol($"T{index}", index, owner, []))
                             .ToImmutableArray();
        var definition = new StructTypeSymbol(name, [], parameters);
        foreach (var parameter in parameters)
            parameter.SetContainingSymbol(definition);
        return definition;
    }
}
