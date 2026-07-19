using Martin.Compiler;
using Martin.Compiler.Binding;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase15ThrowBindingTests
{
    [Fact]
    public void ValidThrowRetainsConvertedValueAndExactDeclaredErrorType()
    {
        var program = Compile("""
            enum Failure: Error { case failed }
            func run(_ failure: Failure) throws Failure { throw failure }
            """);

        var function = Assert.Single(program.Functions);
        var statement = Assert.IsType<BoundThrowStatement>(
            Assert.Single(program.FunctionBodies[function].Statements));

        Assert.True(statement.IsValid);
        Assert.False(statement.SuppressesPropagationDiagnostics);
        Assert.Same(function.ErrorType, statement.ErrorType);
        Assert.Same(statement.ErrorType, statement.Expression.Type);
    }

    [Fact]
    public void ThrowInNonthrowingCallableReportsOnePrimaryDiagnosticAndIsSuppressed()
    {
        var program = Compile("func run() { throw 1 }");
        var function = Assert.Single(program.Functions);
        var statement = Assert.IsType<BoundThrowStatement>(
            Assert.Single(program.FunctionBodies[function].Statements));

        Assert.Equal("MRT2195", Assert.Single(program.Diagnostics).Code);
        Assert.False(statement.IsValid);
        Assert.True(statement.SuppressesPropagationDiagnostics);
        Assert.Same(TypeSymbol.Error, statement.ErrorType);
    }

    [Fact]
    public void WrongThrowTypeReportsExpectedAndActualTypesWithoutGenericConversionDiagnostic()
    {
        var program = Compile("""
            enum Failure: Error { case failed }
            func run() throws Failure { throw 1 }
            """);
        var diagnostic = Assert.Single(program.Diagnostics);
        var function = Assert.Single(program.Functions);
        var statement = Assert.IsType<BoundThrowStatement>(
            Assert.Single(program.FunctionBodies[function].Statements));

        Assert.Equal("MRT2191", diagnostic.Code);
        Assert.Contains("Failure", diagnostic.Message);
        Assert.Contains("Int", diagnostic.Message);
        Assert.False(statement.IsValid);
        Assert.Same(function.ErrorType, statement.ErrorType);
        Assert.Same(TypeSymbol.Int, statement.Expression.Type);
    }

    [Fact]
    public void ErrorConstrainedGenericParameterCanBeThrownWithinItsScope()
    {
        var program = Compile("func forward<E: Error>(_ error: E) throws E { throw error }");
        var function = Assert.Single(program.Functions);
        var statement = Assert.IsType<BoundThrowStatement>(
            Assert.Single(program.FunctionBodies[function].Statements));

        Assert.Empty(program.Diagnostics);
        Assert.True(statement.IsValid);
        Assert.IsType<TypeParameterSymbol>(statement.ErrorType);
        Assert.Same(function.ErrorType, statement.ErrorType);
        Assert.Same(statement.ErrorType, statement.Expression.Type);
    }

    [Fact]
    public void ThrowBindingUsesMethodAndInitializerDeclarations()
    {
        var program = Compile("""
            enum Failure: Error { case failed }
            struct Service {
                init(_ failure: Failure) throws Failure { throw failure }
                func run(_ failure: Failure) throws Failure { throw failure }
            }
            """);
        var service = Assert.Single(program.NamedTypes.Where(type => type.Name == "Service"));
        var method = Assert.Single(service.Methods);
        var initializer = Assert.Single(service.Initializers);

        Assert.True(Assert.IsType<BoundThrowStatement>(Assert.Single(program.MethodBodies[method].Statements)).IsValid);
        Assert.True(Assert.IsType<BoundThrowStatement>(Assert.Single(program.InitializerBodies[initializer].Statements)).IsValid);
        Assert.Empty(program.Diagnostics);
    }

    private static BoundProgram Compile(string source) =>
        Compilation.Create(SyntaxTree.Parse(source, "throw-binding.martin")).BindProgram();
}
