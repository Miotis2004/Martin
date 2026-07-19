using Martin.CodeGeneration;
using Martin.Compiler;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.CodeGeneration.Tests;

public sealed class Phase15TypedErrorEmissionTests
{
    [Fact]
    public void EmitsTypedThrowWrapperAndMartinSpecificCatchBoundary()
    {
        var generated = Emit("""
enum LoadError: Error { case missing(String) case denied }
func load(_ path: String) throws LoadError -> Int { throw LoadError.missing(path) }
func main() {
    do { print(try load("config")) }
    catch LoadError.missing(let path) { print(path) }
    catch LoadError.denied { print("denied") }
}
""");

        Assert.Contains("throw new Martin.Runtime.MartinThrownErrorValue(new LoadError.missing(__local_path), typeof(LoadError));", generated);
        Assert.Contains("catch (Martin.Runtime.MartinThrownError __martin_error_", generated);
        Assert.Contains(".DeclaredErrorType != typeof(LoadError)", generated);
        Assert.DoesNotContain("catch (System.Exception)", generated);
        Assert.Contains("__local__pattern_input_0 = (LoadError)__martin_error_", generated);
        Assert.Contains("__martin_catch_", generated);
        Assert.Contains(" is LoadError.missing", generated);
        Assert.Contains("string __local_path = ", generated);
    }

    [Fact]
    public void EmitsConstructedGenericDeclaredErrorTypes()
    {
        var generated = Emit("""
enum Failure<T>: Error { case bad(T) }
func fail<T>(_ value: T) throws Failure<T> { throw Failure<T>.bad(value) }
func main() {
    do { try fail<Int>(1) }
    catch Failure<Int>.bad(let value) { print(value) }
}
""");

        Assert.Contains("typeof(Failure<long>)", generated);
        Assert.Contains("throw new Martin.Runtime.MartinThrownErrorValue(new Failure<T>.bad(__local_value), typeof(Failure<T>));", generated);
        Assert.Contains("Failure<long> __local__pattern_input_0 = (Failure<long>)__martin_error_", generated);
        Assert.DoesNotContain("new System.Exception", generated);
    }


    [Fact]
    public void ErasesBuiltInErrorGenericConstraintsInCSharp()
    {
        var generated = Emit("""
func forward<E: Error>(_ value: E) throws E { throw value }
func main() { }
""");

        Assert.Contains("internal static void forward<E>(E __local_value)", generated);
        Assert.DoesNotContain("where E : Error", generated);
    }

    [Fact]
    public void ErasesBuiltInErrorStructAndClassConformancesInCSharp()
    {
        var generated = Emit("""
protocol Describable { var message: String { get } }
struct ServiceError: Error, Describable { let message: String }
class ClientError: Error { }
func main() { }
""");

        Assert.Contains("internal struct ServiceError : Describable", generated);
        Assert.Contains("internal sealed class ClientError", generated);
        Assert.DoesNotContain("ServiceError : Error", generated);
        Assert.DoesNotContain("ClientError : Error", generated);
    }

    [Fact]
    public void EmitsRuntimeBoundaryTranslationForReadFile()
    {
        var generated = Emit("""
func main() {
    do { print(try readFile("missing.txt")) }
    catch FileError.notFound(let path) { print(path) }
    catch FileError.accessDenied(let path) { print(path) }
    catch FileError.invalidPath(let path) { print(path) }
    catch FileError.io(let message) { print(message) }
}
""");

        Assert.Contains("internal static string __martin_readFile(string path)", generated);
        Assert.Contains("Martin.Runtime.MartinFileSystem.ReadFile(path)", generated);
        Assert.Contains("Martin.Runtime.MartinFileErrorKind.NotFound => new FileError.notFound", generated);
        Assert.Contains("throw new Martin.Runtime.MartinThrownErrorValue(__martin_value, typeof(FileError));", generated);
    }

    private static string Emit(string source)
    {
        var program = Compilation.Create(SyntaxTree.Parse(source, "typed-errors.martin")).BindProgram();
        Assert.DoesNotContain(program.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var result = new CSharpEmitter().Emit(new CodeGenerationRequest
        {
            Program = new CSharpLowerer().Lower(program),
            AssemblyName = "TypedErrorApp",
            OutputKind = OutputKind.ConsoleApplication
        });
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return result.GeneratedSource;
    }
}
