using Martin.Compiler;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.Semantic.Tests;

public sealed class Phase7ASemanticTests
{
    static Compilation Compile(params string[] sources) => Compilation.Create(sources.Select((s, i) => SyntaxTree.Parse(s, $"file{i}.martin")));

    [Fact]
    public void DiscoversTypesBeforeBindingMembersAndSynthesizesInitializers()
    {
        var program = Compile("""
struct User { let address: Address }
class Address { var id: Int }
func main() {
    let address = Address(id: 1)
    let user = User(address: address)
    print(user.address.id)
}
""").BindProgram();

        Assert.Empty(program.Diagnostics);
        Assert.Contains(program.NamedTypes, t => t.Name == "User" && t.IsStruct);
        Assert.Contains(program.NamedTypes, t => t.Name == "Address" && t.IsClass);
        Assert.All(program.NamedTypes, t => Assert.NotEmpty(t.Initializers));
    }

    [Fact]
    public void BindsMethodsMemberAccessAndClassPropertyMutationThroughLetBinding()
    {
        var program = Compile("""
class Counter {
    var value: Int

    func increment() {
        self.value = self.value + 1
    }
}
func main() {
    let counter = Counter(value: 1)
    counter.value = 2
    counter.increment()
}
""").BindProgram();

        Assert.Empty(program.Diagnostics);
        var counter = Assert.Single(program.NamedTypes, t => t.Name == "Counter");
        Assert.Single(counter.Methods);
    }

    [Theory]
    [InlineData("struct A {} struct A {}", "MRT2100")]
    [InlineData("struct A { var x: Int var x: Int }", "MRT2101")]
    [InlineData("struct A { var x: Int } func main() { let a = A(x: 1) a.y }", "MRT2102")]
    [InlineData("struct A { let x: Int init() {} }", "MRT2103")]
    [InlineData("struct A { let x: Int } func main() { var a = A(x: 1) a.x = 2 }", "MRT2104")]
    [InlineData("struct A { var x: Int } func main() { let a = A(x: 1) a.x = 2 }", "MRT2105")]
    [InlineData("struct A { let x: Int init(x: Int) { return x } }", "MRT2106")]
    public void ReportsNominalTypeDiagnostics(string source, string code)
    {
        Assert.Contains(Compile(source).BindProgram().Diagnostics, d => d.Code == code);
    }
}
