using System.Collections.Immutable;
using Martin.Compiler;
using Martin.Compiler.Generics;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase14GenericIdentityTests
{
    [Fact]
    public void EquivalentCompilationsProduceEquivalentDefinitionAndParameterIdentities()
    {
        const string source = "struct Box<T> { let value: T }";
        var first = Assert.Single(Compilation.Create(SyntaxTree.Parse(source, "models.martin")).BindProgram().NamedTypes);
        var second = Assert.Single(Compilation.Create(SyntaxTree.Parse(source, "models.martin")).BindProgram().NamedTypes);

        Assert.Equal(SymbolIdentity.Create(first), SymbolIdentity.Create(second));
        Assert.Equal(first.TypeParameters[0].Identity, second.TypeParameters[0].Identity);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void TypeParameterIdentityUsesOwnerAndOrdinalRatherThanDisplayName()
    {
        var owner = new StructTypeSymbol("Pair", [], []);
        var first = new TypeParameterSymbol("T", 0, owner, []);
        var renamed = new TypeParameterSymbol("Renamed", 0, owner, []);
        var second = new TypeParameterSymbol("T", 1, owner, []);

        Assert.Equal(first.Identity, renamed.Identity);
        Assert.NotEqual(first.Identity, second.Identity);
    }

    [Fact]
    public void ConstructedKeysCompareArgumentsStructurallyAndInOrder()
    {
        var definition = GenericDefinition("Pair", 2);
        var sameDefinition = GenericDefinition("Pair", 2);
        var first = new ConstructedTypeKey(definition, [TypeSymbol.Int, TypeSymbol.String]);
        var equivalent = new ConstructedTypeKey(sameDefinition, [TypeSymbol.Int, TypeSymbol.String]);
        var reversed = new ConstructedTypeKey(definition, [TypeSymbol.String, TypeSymbol.Int]);

        Assert.Equal(first, equivalent);
        Assert.Equal(first.GetHashCode(), equivalent.GetHashCode());
        Assert.NotEqual(first, reversed);
    }

    [Fact]
    public void ConstructedIdentityRetainsDefinitionAndNestedArgumentRelationships()
    {
        var box = GenericDefinition("Box", 1);
        var inner = new ConstructedTypeSymbol(box, [TypeSymbol.Int], []);
        var outer = new ConstructedTypeSymbol(box, [new OptionalTypeSymbol(inner)], []);
        var identity = outer.Identity;

        Assert.Equal(TypeIdentityKind.Constructed, identity.Kind);
        Assert.Equal(SymbolIdentity.Create(box), identity.Declaration);
        Assert.Equal(TypeIdentityKind.Optional, identity.TypeArguments[0].Kind);
        Assert.Equal(TypeIdentityKind.Constructed, identity.TypeArguments[0].TypeArguments[0].Kind);
    }

    [Fact]
    public void FactoryCanonicalizesConstructionsOnlyWithinItsCompilation()
    {
        const string source = "struct Box<T> { let value: T }";
        var firstCompilation = Compilation.Create(SyntaxTree.Parse(source, "models.martin"));
        var secondCompilation = Compilation.Create(SyntaxTree.Parse(source, "models.martin"));
        var firstDefinition = Assert.Single(firstCompilation.BindProgram().NamedTypes);
        var secondDefinition = Assert.Single(secondCompilation.BindProgram().NamedTypes);

        var first = firstCompilation.GenericTypes.Construct(firstDefinition, [TypeSymbol.Int]);
        var repeated = firstCompilation.GenericTypes.Construct(firstDefinition, [TypeSymbol.Int]);
        var separate = secondCompilation.GenericTypes.Construct(secondDefinition, [TypeSymbol.Int]);

        Assert.Same(first, repeated);
        Assert.NotSame(first, separate);
        Assert.Same(TypeSymbol.Int, Assert.Single(first.Properties).Type);
    }

    [Fact]
    public async Task ConcurrentConstructionPublishesOneCompleteSymbol()
    {
        var definition = GenericDefinition("Box", 1);
        definition.AddMember(new PropertySymbol("value", definition, definition.TypeParameters[0], true, null, []));
        var factory = new GenericTypeFactory();

        var constructions = await Task.WhenAll(Enumerable.Range(0, 32)
                                                   .Select(
                                                       _ => Task.Run(() => factory.Construct(definition, [TypeSymbol.String]))));

        Assert.All(constructions, construction => Assert.Same(constructions[0], construction));
        Assert.All(constructions, construction => Assert.Same(TypeSymbol.String, Assert.Single(construction.Properties).Type));
    }

    [Fact]
    public void InvalidConstructionsAreRejectedRatherThanCached()
    {
        var factory = new GenericTypeFactory();
        var definition = GenericDefinition("Pair", 2);

        Assert.Throws<ArgumentException>(() => factory.Construct(definition, [TypeSymbol.Int]));
        Assert.Throws<ArgumentException>(() => factory.Construct(definition, [TypeSymbol.Int, TypeSymbol.Error]));
    }

    [Fact]
    public void RecursiveConstructionUsesItsCanonicalShell()
    {
        var definition = GenericDefinition("Node", 1);
        var openSelf = new ConstructedTypeSymbol(definition, [definition.TypeParameters[0]], []);
        definition.AddMember(new PropertySymbol("next", definition, new OptionalTypeSymbol(openSelf), true, null, []));

        var constructed = new GenericTypeFactory().Construct(definition, [TypeSymbol.Int]);
        var next = Assert.IsType<OptionalTypeSymbol>(Assert.Single(constructed.Properties).Type);

        Assert.Same(constructed, next.ElementType);
    }

    private static StructTypeSymbol GenericDefinition(string name, int arity)
    {
        var owner = new StructTypeSymbol(name, [], []);
        var parameters = Enumerable.Range(0, arity).Select(i => new TypeParameterSymbol($"T{i}", i, owner, [])).ToImmutableArray();
        return new StructTypeSymbol(name, [], parameters);
    }
}
