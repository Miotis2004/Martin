using Martin.CodeGeneration;
using Martin.Compiler;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.CodeGeneration.Tests;

public sealed class Phase14GenericEmissionTests
{
    [Fact]
    public void EmitsGenericDeclarationsConstraintsAndExplicitCalls()
    {
        var generated = Emit("""
protocol Describable { func describe() -> String }
struct Word: Describable { func describe() -> String { return "word" } }
struct Box<T: Describable> {
    let value: T
    func echo<U: Describable>(_ other: U) -> U { return other }
}
func identity<T: Describable>(_ value: T) -> T { return value }
func main() {
    let word = Word()
    let box = Box<Word>(value: identity<Word>(word))
    print(box.echo<Word>(word).describe())
}
""");

        Assert.Contains("internal struct Box<T> where T : Describable", generated);
        Assert.Contains("__method_echo<U>(U __local_other) where U : Describable", generated);
        Assert.Contains("__fn_identity<T>(T __local_value) where T : Describable", generated);
        Assert.Contains("__fn_identity<Word>", generated);
        Assert.Contains(".__method_echo<Word>", generated);
    }

    [Fact]
    public void EmitsConstructedInitializersNestedTypesAndGenericEnums()
    {
        var generated = Emit("""
struct Box<T> { let value: T }
enum Result<T> { case success(Box<T?>) case failure }
func main() {
    let box = Box<Int?>(value: 1)
    let result = Result<Int>.success(box)
}
""");

        Assert.Contains("internal struct Box<T>", generated);
        Assert.Contains("internal abstract class Result<T>", generated);
        Assert.Contains("class success : Result<T>", generated);
        Assert.Contains("Box<Martin.Runtime.Optional<T>> Value0", generated);
        Assert.Contains("new Box<Martin.Runtime.Optional<long>>", generated);
        Assert.Contains("new Result<long>.success", generated);
    }

    [Fact]
    public void GenericEmissionAndSourceMapsAreDeterministic()
    {
        const string source = "struct Box<T> { let value: T } func identity<T>(_ value: T) -> T { return value } func main() { let box = Box<Int>(value: identity<Int>(1)) }";

        var first = EmitResult(source);
        var second = EmitResult(source);

        Assert.Equal(first.GeneratedSource, second.GeneratedSource);
        Assert.Equal(first.SourceMap, second.SourceMap);
        Assert.Contains(first.SourceMap, entry => entry.MartinLocation.FilePath == "generics.martin");
    }

    private static string Emit(string source) => EmitResult(source).GeneratedSource;

    private static CodeGenerationResult EmitResult(string source)
    {
        var program = Compilation.Create(SyntaxTree.Parse(source, "generics.martin")).BindProgram();
        Assert.DoesNotContain(program.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var result = new CSharpEmitter().Emit(new CodeGenerationRequest
        {
            Program = new CSharpLowerer().Lower(program),
            AssemblyName = "GenericApp",
            OutputKind = OutputKind.ConsoleApplication
        });
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return result;
    }
}
