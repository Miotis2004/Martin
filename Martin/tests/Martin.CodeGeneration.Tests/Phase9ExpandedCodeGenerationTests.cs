using Martin.CodeGeneration;
using Martin.Compiler;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.CodeGeneration.Tests;

public sealed class Phase9ExpandedCodeGenerationTests
{
    static CodeGenerationResult EmitResult(params (string Path, string Source)[] sources)
    {
        var trees = sources.Select(s => SyntaxTree.Parse(s.Source, s.Path)).ToArray();
        var program = Compilation.Create(trees).BindProgram();
        Assert.Empty(program.Diagnostics);
        var result = new CSharpEmitter().Emit(new() { Program = new CSharpLowerer().Lower(program), AssemblyName = "TestApp", OutputKind = OutputKind.ConsoleApplication });
        Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        return result;
    }

    static string Emit(string source) => EmitResult(("test.martin", source)).GeneratedSource;

    [Fact]
    public void LoweringAddsImplicitReturn()
    {
        var cs = Emit("func helper() { print(1) } func main() { helper() }");

        Assert.Contains("internal static void __fn_helper()", cs);
        Assert.Contains("return;", cs);
    }

    [Fact]
    public void BuiltInCallsAreNormalized()
    {
        var cs = Emit("func main() { print(42) }");

        Assert.Contains("Martin.Runtime.MartinConsole.Print", cs);
        Assert.DoesNotContain("__fn_print", cs);
    }

    [Fact]
    public void IfLetInputIsEvaluatedOnce()
    {
        var cs = Emit("func value() -> Int? { return 1 } func main() { if let x = value() { print(x) } }");

        Assert.Equal(1, Count(cs, "= __fn_value()"));
        Assert.Contains("__local___tmp_optional_0 = __fn_value()", cs);
        Assert.Contains("if (__local___tmp_optional_0.HasValue)", cs);
    }

    [Fact]
    public void SwitchInputIsEvaluatedOnce()
    {
        var cs = Emit("""
            enum Choice { case yes(Int) case no }
            func choose() -> Choice { return Choice.yes(1) }
            func main() { switch choose() { case .yes(let value): print(value) case .no: print(0) } }
            """);

        Assert.Equal(1, Count(cs, "= __fn_choose()"));
        Assert.Contains("__local___tmp_switch_0 = __fn_choose()", cs);
        Assert.Contains("switch (__local___tmp_switch_0)", cs);
    }

    [Fact]
    public void OptionalInjectionIsExplicit()
    {
        var cs = Emit("func main() { let count: Int? = 10 }");

        Assert.Contains("new Martin.Runtime.Optional<long>", cs);
    }

    [Fact]
    public void TemporaryNamesAreDeterministic()
    {
        var source = "func value() -> Int? { return 1 } func main() { if let x = value() { print(x) } }";

        Assert.Equal(Emit(source), Emit(source));
    }

    [Fact]
    public void GeneratedLocalsDoNotCollide()
    {
        var cs = Emit("func main() { let __tmp_optional_0: Int? = 1 if let x = __tmp_optional_0 { print(x) } }");

        Assert.Contains("__local___tmp_optional_0", cs);
        Assert.Contains("__tmp_optional_0", cs);
    }

    [Fact]
    public void LoweringPreservesSourceLocations()
    {
        var result = EmitResult(("locations.martin", "func main() { let answer = 42 print(answer) }"));

        Assert.Contains(result.SourceMap, entry => entry.MartinLocation.FilePath == "locations.martin" &&
                                                  entry.MartinLocation.Text.ToString(entry.MartinLocation.Span).Contains("let answer"));
    }

    [Fact]
    public void LoweringHonorsCancellation()
    {
        var program = Compilation.Create(SyntaxTree.Parse("func main() { }", "cancel.martin")).BindProgram();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => new CSharpLowerer().Lower(program, cts.Token));
    }

    [Fact]
    public void GeneratedSourceIsDeterministic()
    {
        var source = "func zed() { } func main() { } func alpha() { }";

        Assert.Equal(Emit(source), Emit(source));
    }

    [Fact]
    public void SourceMapContainsUserStatements()
    {
        var result = EmitResult(("map.martin", "func main() { let answer = 42 print(answer) }"));

        Assert.Contains(result.SourceMap, entry => entry.MartinLocation.Text.ToString(entry.MartinLocation.Span).Contains("let answer"));
        Assert.Contains(result.SourceMap, entry => entry.MartinLocation.Text.ToString(entry.MartinLocation.Span).Contains("print(answer)"));
    }

    [Fact]
    public void CrossFileSourceMapsAreCorrect()
    {
        var result = EmitResult(
            ("helpers.martin", "func helper() { print(1) }"),
            ("main.martin", "func main() { helper() }"));

        Assert.Contains(result.SourceMap, entry => entry.MartinLocation.FilePath == "helpers.martin");
        Assert.Contains(result.SourceMap, entry => entry.MartinLocation.FilePath == "main.martin");
    }

    [Fact]
    public void GeneratedWrappersAreHidden()
    {
        var cs = Emit("func main() { print(42) }");
        var wrapperIndex = cs.IndexOf("public static int Main", StringComparison.Ordinal);
        var hiddenIndex = cs.LastIndexOf("#line hidden", wrapperIndex, StringComparison.Ordinal);

        Assert.True(hiddenIndex >= 0);
    }

    [Fact]
    public void SourceDirectivesCanBeDisabled()
    {
        var program = Compilation.Create(SyntaxTree.Parse("func main() { print(42) }", "directives.martin")).BindProgram();
        var result = new CSharpEmitter().Emit(new() { Program = new CSharpLowerer().Lower(program), AssemblyName = "TestApp", OutputKind = OutputKind.ConsoleApplication, IncludeSourceDirectives = false });

        Assert.True(result.Success);
        Assert.DoesNotContain("#line", result.GeneratedSource);
        Assert.NotEmpty(result.SourceMap);
    }

    [Fact]
    public void EscapedPathsAreValid()
    {
        const string path = "C:\\Projects\\Martin Samples\\quoted \" source.martin";
        var cs = EmitResult((path, "func main() { print(42) }")).GeneratedSource;

        Assert.Contains("C:\\\\Projects\\\\Martin Samples\\\\quoted \\\" source.martin", cs);
    }

    static int Count(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
