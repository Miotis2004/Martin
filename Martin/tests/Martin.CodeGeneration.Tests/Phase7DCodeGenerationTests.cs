using Martin.CodeGeneration;
using Martin.Compiler;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.CodeGeneration.Tests;

public sealed class Phase7DCodeGenerationTests
{
    static string Emit(string source)
    {
        var program = Compilation.Create(SyntaxTree.Parse(source, "test.martin")).BindProgram();
        Assert.Empty(program.Diagnostics);
        var result = new CSharpEmitter().Emit(new() { Program = new CSharpLowerer().Lower(program), AssemblyName = "TestApp", OutputKind = OutputKind.ConsoleApplication });
        Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        return result.GeneratedSource;
    }

    [Fact]
    public void EmitsInterfacesAndConformingTypes()
    {
        var cs = Emit("""
protocol Describable {
    func describe() -> String
}

struct User: Describable {
    let name: String

    func describe() -> String {
        return name
    }
}

func main() {
    let user = User(name: "Ada")
    print(user.describe())
}
""");

        Assert.Contains("interface", cs);
        Assert.Contains(": Describable", cs);
        Assert.Contains("string Describable.describe()", cs);
        Assert.Contains("return __method_describe();", cs);
    }
}
