using Martin.Compiler;
using Martin.Compiler.Binding;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase15ErrorEffectTests
{
    [Fact]
    public void ThrowingCallPublishesEffectWithoutChangingValueType()
    {
        var error = new EnumTypeSymbol("NetworkError", []);
        var function = new FunctionSymbol("load", [], TypeSymbol.String, null, false, [], isThrowing: true, errorType: error);
        var call = new BoundCallExpression(function, []);

        Assert.Same(TypeSymbol.String, call.Type);
        Assert.True(call.ErrorEffect.CanThrow);
        Assert.Same(error, call.ErrorEffect.ErrorType);
    }

    [Fact]
    public void CompositeExpressionsPreserveAndCombineEffects()
    {
        var error = new EnumTypeSymbol("NetworkError", []);
        var throwing = new FunctionSymbol("load", [], TypeSymbol.Int, null, false, [], isThrowing: true, errorType: error);
        var argument = new BoundCallExpression(throwing, []);
        var outer = new FunctionSymbol("consume", [new ParameterSymbol("value", null, 0, TypeSymbol.Int, [])], TypeSymbol.Int, null, false, []);
        var call = new BoundCallExpression(outer, [new BoundConversionExpression(TypeSymbol.Int, argument)]);

        Assert.Same(error, call.ErrorEffect.ErrorType);
        Assert.Same(TypeSymbol.Int, call.Type);
    }

    [Fact]
    public void IncompatibleEffectsProduceAConflictingRecoveryEffect()
    {
        var first = new ErrorEffect(true, new EnumTypeSymbol("FirstError", []));
        var second = new ErrorEffect(true, new EnumTypeSymbol("SecondError", []));

        var combined = ErrorEffect.Combine(first, second);

        Assert.True(combined.CanThrow);
        Assert.True(combined.IsConflicting);
        Assert.Same(TypeSymbol.Error, combined.ErrorType);
    }

    [Fact]
    public void TryUsesCompleteOperandEffectAndRejectsNonthrowingOperands()
    {
        var valid = Compile("""
            enum LoadError: Error { case failed }
            func load() throws LoadError -> Int { throw LoadError.failed }
            func run() throws LoadError -> Int { return try (load() + 1) }
            """);
        var invalid = Compile("func value() -> Int { return try (1 + 2) }");

        Assert.DoesNotContain(valid.Diagnostics, diagnostic => diagnostic.Code is "MRT2190" or "MRT2196");
        Assert.Contains(invalid.Diagnostics, diagnostic => diagnostic.Code == "MRT2196");
    }

    [Fact]
    public void NestedTryDoesNotAcknowledgeTheSameOperationTwice()
    {
        var compilation = Compile("""
            enum LoadError: Error { case failed }
            func load() throws LoadError -> Int { throw LoadError.failed }
            func run() throws LoadError -> Int { return try (try load()) }
            """);

        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == "MRT2196");
    }

    [Fact]
    public void ThrowingMethodsAndInitializersRequireTryUniformly()
    {
        var compilation = Compile("""
            enum BuildError: Error { case failed }
            struct Builder {
                init() throws BuildError { throw BuildError.failed }
                func build() throws BuildError -> Int { throw BuildError.failed }
            }
            func run(_ builder: Builder) { let a = Builder(); let b = builder.build() }
            """);

        Assert.Equal(2, compilation.Diagnostics.Count(diagnostic => diagnostic.Code == "MRT2190"));
    }

    private static Compilation Compile(string source) =>
        Compilation.Create(SyntaxTree.Parse(source, "effects.martin"));
}
