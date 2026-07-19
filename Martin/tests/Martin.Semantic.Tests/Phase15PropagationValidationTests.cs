using Martin.Compiler;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase15PropagationValidationTests
{
    [Fact]
    public void NonthrowingFunctionCannotLeakAcknowledgedEffect()
    {
        var program = Compile("""
            enum Failure: Error { case failed }
            func load() throws Failure -> Int { throw Failure.failed }
            func run() -> Int { return try load() }
            """);

        Assert.Contains(program.Diagnostics, diagnostic => diagnostic.Code == "MRT2192");
    }

    [Fact]
    public void ThrowingFunctionMayPropagateMatchingAcknowledgedEffect()
    {
        var program = Compile("""
            enum Failure: Error { case failed }
            func load() throws Failure -> Int { throw Failure.failed }
            func run() throws Failure -> Int { return try load() }
            """);

        Assert.DoesNotContain(program.Diagnostics, diagnostic => diagnostic.Code is "MRT2192" or "MRT2199");
    }

    [Fact]
    public void IncompatiblePropagationIsDiagnosedOnce()
    {
        var program = Compile("""
            enum Failure: Error { case failed }
            enum Other: Error { case failed }
            func load() throws Failure -> Int { throw Failure.failed }
            func run() throws Other -> Int { return try load() }
            """);

        Assert.Equal(1, program.Diagnostics.Count(diagnostic => diagnostic.Code == "MRT2199"));
    }

    [Fact]
    public void MethodsInitializersBranchesAndCatchBodiesAreValidated()
    {
        var program = Compile("""
            enum Failure: Error { case failed }
            func load() throws Failure -> Int { throw Failure.failed }
            struct Service {
                init() { if true { let value = try load() } }
                func run() { while false { let value = try load() } }
                func handledBody() { do { let value = try load() } catch { print(1) } }
                func leakingCatch() { do { print(1) } catch { let value = try load() } }
            }
            """);

        Assert.Equal(3, program.Diagnostics.Count(diagnostic => diagnostic.Code == "MRT2192"));
    }

    [Fact]
    public void MixedAcknowledgedEffectsAreDiagnosedDuringPropagationValidation()
    {
        var program = Compile("""
            enum AError: Error { case failed }
            enum BError: Error { case failed }
            func readA() throws AError -> Int { throw AError.failed }
            func readB() throws BError -> Int { throw BError.failed }
            func process() throws AError {
                let first = try readA()
                let second = try readB()
            }
            """);

        Assert.Contains(program.Diagnostics, diagnostic => diagnostic.Code == "MRT2402");
        Assert.Contains(program.Diagnostics, diagnostic => diagnostic.Code == "MRT2199");
    }

    [Fact]
    public void MixedDoCatchProtectedBodyIsDiagnosedInsteadOfBeingConsumed()
    {
        var program = Compile("""
            enum AError: Error { case failed }
            enum BError: Error { case failed }
            func readA() throws AError -> Int { throw AError.failed }
            func readB() throws BError -> Int { throw BError.failed }
            func process() {
                do {
                    let first = try readA()
                    let second = try readB()
                } catch {
                    print(1)
                }
            }
            """);

        Assert.Contains(program.Diagnostics, diagnostic => diagnostic.Code == "MRT2402");
    }

    [Fact]
    public void ThrowingMainIsRejected()
    {
        var program = Compile("""
            enum Failure: Error { case failed }
            func main() throws Failure { print(1) }
            """);

        Assert.Contains(program.Diagnostics, diagnostic => diagnostic.Code == "MRT2192" && diagnostic.Message.Contains("main"));
    }

    private static Compilation Compile(string source) =>
        Compilation.Create(SyntaxTree.Parse(source, "propagation-validation.martin"));
}
