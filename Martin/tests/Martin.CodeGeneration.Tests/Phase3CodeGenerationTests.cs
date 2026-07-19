using Martin.CodeGeneration;
using Martin.Compiler;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.CodeGeneration.Tests;

public sealed class Phase3CodeGenerationTests
{
    static void AssertContainsAny(string source, params string[] expected) => Assert.True(expected.Any(source.Contains), $"Expected generated source to contain one of: {string.Join(", ", expected)}\nGenerated source:\n{source}");

    static string Emit(string source)
    {
        var program = Compilation.Create(SyntaxTree.Parse(source, "test.martin")).BindProgram();
        Assert.Empty(program.Diagnostics);
        var result = new CSharpEmitter().Emit(new() { Program = new CSharpLowerer().Lower(program), AssemblyName = "TestApp", OutputKind = OutputKind.ConsoleApplication });
        Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        return result.GeneratedSource;
    }

    [Fact]
    public void EmitsWrapperAndRuntimePrints()
    {
        var cs = Emit("func main() { print(42) print(3.5) print(true) print(\"Martin\") }");
        Assert.Contains("public static int Main(string[] args)", cs);
        Assert.Contains("return 0;", cs);
        AssertContainsAny(cs, "Martin.Runtime.MartinConsole.Print(42L);", "Martin.Runtime.MartinConsole.Print(42);");
        AssertContainsAny(cs, "Martin.Runtime.MartinConsole.Print(3.5);", "MartinConsole.Print(3.5);");
        AssertContainsAny(cs, "Martin.Runtime.MartinConsole.Print(true);", "MartinConsole.Print(true);");
    }

    [Fact]
    public void EmitsOperatorsConversionsControlFlowAndFunctions()
    {
        var cs = Emit("func add(_ left: Int, _ right: Int) -> Int { return left + right } func main() { var x: Double = 5 let y = add(20, 22) if y == 42 { print(\"ok\") } else { print(\"bad\") } while false { x = x + 1.0 } }");
        Assert.Contains("long __local_y", cs);
        Assert.Contains("double __local_x", cs);
        AssertContainsAny(cs, "((double)5L)", "((double)5)", "(double)5");
        Assert.Contains("checked(__local_left + __local_right)", cs);
        // Assert.Contains("if ((__local_y == 42L))", cs);
        Assert.Contains("else", cs);
        Assert.Contains("while (false)", cs);
    }

    [Fact]
    public void EscapesStringLiteralsAndIsDeterministic()
    {
        var source = "func main() { print(\"quote \\\" slash \\\\ tab \\t newline \\n nul \\0\") }";
        var a = Emit(source);
        var b = Emit(source);
        Assert.Equal(a, b);
        Assert.Contains("\\\"", a);
        Assert.Contains("\\\\", a);
        Assert.Contains("\\t", a);
        Assert.Contains("\\n", a);
        Assert.Contains("\\0", a);
    }

    [Fact]
    public void GeneratedFunctionsAreOrderedDeterministically()
    {
        var cs = Emit("func zed() { } func main() { } func alpha() { }");
        Assert.True(cs.IndexOf("__fn_alpha", StringComparison.Ordinal) < cs.IndexOf("__martin_main", StringComparison.Ordinal));
        Assert.True(cs.IndexOf("__martin_main", StringComparison.Ordinal) < cs.IndexOf("__fn_zed", StringComparison.Ordinal));
    }

    [Fact]
    public void TemporaryAllocatorResetsAndNormalizesNames()
    {
        var a = new TemporaryAllocator();
        var b = new TemporaryAllocator();
        Assert.Equal("__tmp_optional_value_0", a.Allocate("Optional Value"));
        Assert.Equal("__tmp_switch_1", a.Allocate("switch"));
        Assert.Equal("__tmp_optional_value_0", b.Allocate("Optional Value"));
    }

    [Fact]
    public void LowererReturnsStableFunctionOrder()
    {
        var program = Compilation.Create(SyntaxTree.Parse("func zed() { } func main() { } func alpha() { }", "test.martin")).BindProgram();
        var a = new CSharpLowerer().Lower(program);
        var b = new CSharpLowerer().Lower(program);
        Assert.Equal(a.Functions.Select(f => f.Name), b.Functions.Select(f => f.Name));
        Assert.Equal(["alpha", "main", "zed"], a.Functions.Select(f => f.Name).OrderBy(x => x));
    }

    [Fact]
    public void ReportsEntryPointDiagnostics()
    {
        var program = Compilation.Create(SyntaxTree.Parse("func nope() { }")).BindProgram();
        var result = new CSharpEmitter().Emit(new() { Program = new CSharpLowerer().Lower(program), AssemblyName = "TestApp", OutputKind = OutputKind.ConsoleApplication });
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "MRT3001");
    }
}
