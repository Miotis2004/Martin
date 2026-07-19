using Martin.CodeGeneration;
using Martin.Compiler;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.CodeGeneration.Tests;

public sealed class Phase13ProtocolEmissionTests
{
    static string Emit(string source, bool includeSourceDirectives = false)
    {
        var program = Compilation.Create(SyntaxTree.Parse(source, "protocols.martin")).BindProgram();
        Assert.Empty(program.Diagnostics);
        var result = new CSharpEmitter().Emit(new()
        {
            Program = new CSharpLowerer().Lower(program),
            AssemblyName = "TestApp",
            OutputKind = OutputKind.ConsoleApplication,
            IncludeSourceDirectives = includeSourceDirectives
        });
        Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        return result.GeneratedSource;
    }

    [Fact]
    public void EmitsExplicitMethodAndPropertyBridges()
    {
        var cs = Emit("""
protocol MutableValue {
    var value: Int
    mutating func increment()
}

struct Counter: MutableValue {
    var value: Int
    mutating func increment() { value = value + 1 }
}

func main() { var counter = Counter(value: 0) }
""");

        Assert.Contains("internal interface MutableValue", cs);
        Assert.Contains("long value { get; set; }", cs);
        Assert.Contains("void increment();", cs);
        Assert.Contains("internal struct Counter : MutableValue", cs);
        Assert.Contains("long MutableValue.value", cs);
        Assert.Contains("get => this.value;", cs);
        Assert.Contains("set => this.value = value;", cs);
        Assert.Contains("void MutableValue.increment()", cs);
        Assert.Contains("__method_increment();", cs);
        Assert.DoesNotContain("public void", cs);
    }

    [Fact]
    public void EmitsDeterministicInterfacesAndConformances()
    {
        const string source = """
protocol Zebra { func zebra() }
protocol Alpha { func alpha() }
class Both: Zebra, Alpha {
    func zebra() {}
    func alpha() {}
}
func main() { let value = Both() }
""";

        var first = Emit(source);
        var second = Emit(source);

        Assert.Equal(first, second);
        Assert.True(first.IndexOf("interface Alpha", StringComparison.Ordinal) < first.IndexOf("interface Zebra", StringComparison.Ordinal));
        Assert.Contains("sealed class Both : Alpha, Zebra", first);
    }

    [Fact]
    public void MapsRequirementsAndGeneratedBridgesToMartinSource()
    {
        var cs = Emit("""
protocol Named { func name() -> String }
struct Person: Named { func name() -> String { return "Ada" } }
func main() { let person = Person() }
""", includeSourceDirectives: true);

        Assert.Contains("#line 1 \"protocols.martin\"", cs);
        Assert.Contains("#line 2 \"protocols.martin\"", cs);
        Assert.Contains("#line hidden", cs);
    }
}
