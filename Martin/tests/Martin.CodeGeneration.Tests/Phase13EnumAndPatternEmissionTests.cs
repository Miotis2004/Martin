using Martin.Compiler;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.CodeGeneration.Tests;

public sealed class Phase13EnumAndPatternEmissionTests
{
    [Fact]
    public void EmitsAssociatedCasesAndDecisionFlow()
    {
        var result = Emit("""
enum Result { case success(Int, String) case failure }
func inspect(_ result: Result) {
    switch result {
        case .success(let code, let message): print(message)
        case .failure: print("failed")
    }
}
func main() { inspect(Result.success(7, "ok")) }
""");

        Assert.Contains("internal abstract record Result", result.GeneratedSource);
        Assert.Contains("internal sealed record success(long Value0, string Value1) : Result;", result.GeneratedSource);
        Assert.Contains("new Result.success(7L, \"ok\")", result.GeneratedSource);
        Assert.Contains(" is Result.success", result.GeneratedSource);
        Assert.Contains("((Result.success)", result.GeneratedSource);
        Assert.Contains("Pattern decision graph reached its defensive failure continuation.", result.GeneratedSource);
    }

    [Fact]
    public void DoesNotSynthesizeStructuralEqualityForUnsupportedPayloads()
    {
        var result = Emit("""
class Token { let value: Int }
enum Box { case token(Token) case empty }
func main() { let box = Box.empty() }
""");

        Assert.Contains("internal abstract class Box", result.GeneratedSource);
        Assert.Contains("internal sealed class token : Box", result.GeneratedSource);
        Assert.Contains("internal Token Value0 { get; }", result.GeneratedSource);
        Assert.DoesNotContain("record token", result.GeneratedSource);
    }

    [Fact]
    public void EnumDeclarationsAndPatternFlowPreserveMartinLocations()
    {
        var result = Emit("""
enum State { case ready(Int) case stopped }
func main() {
    let state = State.ready(1)
    switch state { case .ready(let value): print(value) case .stopped: print(0) }
}
""", "locations.martin");

        Assert.Contains(result.SourceMap, entry => entry.MartinLocation.FilePath == "locations.martin" &&
            entry.MartinLocation.Text.ToString(entry.MartinLocation.Span).Contains("State"));
        Assert.Contains(result.SourceMap, entry => entry.MartinLocation.FilePath == "locations.martin" &&
            entry.MartinLocation.Text.ToString(entry.MartinLocation.Span).Contains("ready"));
        Assert.Contains("#line 1 \"locations.martin\"", result.GeneratedSource);
    }

    [Fact]
    public void EmissionIsDeterministic()
    {
        const string source = "enum Choice { case yes(Int) case no } func main() { let value = Choice.yes(1) switch value { case .yes(let x): print(x) case .no: print(0) } }";
        Assert.Equal(Emit(source).GeneratedSource, Emit(source).GeneratedSource);
    }

    private static CodeGenerationResult Emit(string source, string path = "test.martin")
    {
        var bound = Compilation.Create(SyntaxTree.Parse(source, path)).BindProgram();
        Assert.DoesNotContain(bound.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var result = new CSharpEmitter().Emit(new CodeGenerationRequest
        {
            Program = new CSharpLowerer().Lower(bound),
            AssemblyName = "TestApp",
            OutputKind = OutputKind.ConsoleApplication
        });
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return result;
    }
}
