using Martin.Build;
using Martin.CodeGeneration;
using Martin.Compiler;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Syntax;
using Martin.Execution;
using Martin.LanguageServices;
using System.Collections.Immutable;
using Xunit;

namespace Martin.Integration.Tests;

public sealed class Phase9FullPipelineIntegrationTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "MartinIntegrationTests", Guid.NewGuid().ToString("N"));

    public Phase9FullPipelineIntegrationTests() => Directory.CreateDirectory(root);

    [Fact]
    public async Task CompileBuildRunHelloWorld()
    {
        var run = await BuildAndRunAsync("func main() { print(\"Hello, integration!\") }");

        AssertRunSucceeded(run);
        Assert.Equal("Hello, integration!" + Environment.NewLine, run.StandardOutput);
    }

    [Fact]
    public async Task CompileBuildRunWithArguments()
    {
        var run = await BuildAndRunAsync(
            "func main() { print(argumentCount()) print(argument(0)) print(argument(1)) }",
            new ExecutionOptions { Arguments = ["first arg", "second"] });

        AssertRunSucceeded(run);
        Assert.Equal(string.Join(Environment.NewLine, "2", "first arg", "second") + Environment.NewLine, run.StandardOutput);
    }

    [Fact]
    public async Task CompileBuildRunWithStandardInput()
    {
        var run = await BuildAndRunAsync(
            "func main() { let value = readLine() print(value) }",
            new ExecutionOptions { StandardInput = "from stdin" + Environment.NewLine });

        AssertRunSucceeded(run);
        Assert.Equal("from stdin" + Environment.NewLine, run.StandardOutput);
    }

    [Fact]
    public async Task CompileBuildRunWithStandardError()
    {
        var run = await BuildAndRunAsync("func main() { writeError(\"problem\") }");

        AssertRunSucceeded(run);
        Assert.Equal("problem" + Environment.NewLine, run.StandardError);
    }

    [Fact]
    public async Task CompileBuildRunWithNonzeroExitCode()
    {
        var run = await BuildAndRunAsync("func main() { exit(17) }");

        Assert.Equal(ExecutionStatus.Failed, run.Status);
        Assert.Equal(17, run.ExitCode);
    }

    [Fact]
    public async Task CompileBuildRunOptionalBinding()
    {
        var run = await BuildAndRunAsync("func main() { let value: Int? = 42 if let unwrapped = value { print(unwrapped) } }");

        AssertRunSucceeded(run);
        Assert.Equal("42" + Environment.NewLine, run.StandardOutput);
    }

    [Fact]
    public async Task CompileBuildRunSwitch()
    {
        var source = "enum Choice { case yes(Int) case no } func choose() -> Choice { return Choice.yes(9) } func main() { switch choose() { case .yes(let value): print(value) case .no: print(0) } }";

        var run = await BuildAndRunAsync(source);

        AssertRunSucceeded(run);
        Assert.Equal("9" + Environment.NewLine, run.StandardOutput);
    }

    [Fact]
    public async Task Phase13CrossFilePatternsAndProtocolConformanceCloseEndToEnd()
    {
        using var scenario = new PipelineScenario(root);
        var trees = new[] {
            SyntaxTree.Parse("protocol Describable { func describe() -> String }", "protocol.martin"),
            SyntaxTree.Parse("struct Reporter: Describable { func describe() -> String { return \"protocol-ok\" } }", "reporter.martin"),
            SyntaxTree.Parse("enum Result { case success(Int?) case failure }", "result.martin"),
            SyntaxTree.Parse("func main() { let result = Result.success(42) switch result { case .success(.some(let value)): print(value) case .success(nil): print(0) case .failure: print(-1) } let reporter = Reporter() print(reporter.describe()) }", "main.martin")
        };
        var compilation = Compilation.Create(trees);
        var program = compilation.BindProgram();

        Assert.Empty(program.Diagnostics);
        var conformance = Assert.Single(program.Conformances);
        var witness = Assert.Single(conformance.Witnesses);
        Assert.Same(witness.Key, Assert.Single(conformance.RequirementsByWitness[witness.Value]));

        var build = await scenario.BuildAsync(compilation);
        Assert.True(build.Success, build.StandardOutput + build.StandardError +
                                       string.Join(Environment.NewLine, build.Diagnostics.Select(d => d.Code + ": " + d.Message)));

        var run = await new MartinExecutionService().RunAsync(build, new ExecutionOptions());
        AssertRunSucceeded(run);
        Assert.Equal(string.Join(Environment.NewLine, "42", "protocol-ok") + Environment.NewLine, run.StandardOutput);
    }

    [Fact]
    public async Task Phase14CrossFileGenericsCloseEndToEnd()
    {
        using var scenario = new PipelineScenario(root);
        var trees = new[] {
            SyntaxTree.Parse("protocol Describable { func describe() -> String }", "protocol.martin"),
            SyntaxTree.Parse("struct Word: Describable { func describe() -> String { return \"generic-ok\" } }", "word.martin"),
            SyntaxTree.Parse("enum Outcome<T> { case success(T?) case failure }", "outcome.martin"),
            SyntaxTree.Parse("func wrap<T: Describable>(_ value: T) -> Outcome<T> { return Outcome<T>.success(value) }", "wrap.martin"),
            SyntaxTree.Parse("func main() { let result = wrap(Word()) switch result { case .success(.some(let value)): print(value.describe()) case .success(nil): print(\"empty\") case .failure: print(\"failure\") } }", "main.martin")
        };
        var compilation = Compilation.Create(trees);
        var program = compilation.BindProgram();

        Assert.Empty(program.Diagnostics);

        var build = await scenario.BuildAsync(compilation);
        Assert.True(build.Success, build.StandardOutput + build.StandardError +
                                       string.Join(Environment.NewLine, build.Diagnostics.Select(d => d.Code + ": " + d.Message)));

        var run = await new MartinExecutionService().RunAsync(build, new ExecutionOptions());
        AssertRunSucceeded(run);
        Assert.Equal("generic-ok" + Environment.NewLine, run.StandardOutput);
    }

    [Fact]
    public async Task Phase15TypedErrorsCloseEndToEndAcrossCompilerRuntimeAndLanguageServices()
    {
        using var scenario = new PipelineScenario(root);
        var sources = new[] {
            ("errors.martin", "enum DecodeError<T>: Error { case invalid(value: T) case empty }"),
            ("reader.martin", "struct Decoder { func decode(_ text: String) throws DecodeError<String> -> String { throw DecodeError<String>.invalid(value: text) } }"),
            ("host.martin", "func loadMissing(_ path: String) throws FileError -> String { return try readFile(path) }"),
            ("main.martin", "func main() { let decoder = Decoder() do { print(try decoder.decode(\"payload-42\")) } catch .invalid(let value) { print(value) } catch .empty { print(\"empty\") } do { print(try loadMissing(\"missing-phase15.txt\")) } catch FileError.notFound(let path) { print(path) } catch FileError.accessDenied(let path) { print(path) } catch FileError.invalidPath(let path) { print(path) } catch FileError.io(let message) { print(message) } }")
        };
        var trees = sources.Select(source => SyntaxTree.Parse(source.Item2, source.Item1)).ToArray();
        var compilation = Compilation.Create(trees);
        var program = compilation.BindProgram();

        Assert.Empty(program.Diagnostics);
        Assert.Contains(program.NamedTypes, type => type.Name == "DecodeError" && type.IsErrorType);
        Assert.Contains(program.NamedTypes, type => type.Name == "FileError" && type.IsErrorType);

        var languageService = new MartinLanguageService();
        var documents = sources.Select(source => new LanguageDocumentSnapshot(DocumentId.CreateNew(), source.Item1, source.Item2, new(18))).ToImmutableArray();
        var project = new LanguageProjectSnapshot(ProjectId.CreateNew(), "Phase15Closure", scenario.WorkDirectory, new(18), documents);
        var workspace = new LanguageWorkspaceSnapshot(WorkspaceId.CreateNew(), new(18), [project]);
        var mainDocument = documents.Single(document => document.FilePath == "main.martin");
        var mainAnalysis = await languageService.AnalyzeDocumentAsync(workspace, mainDocument.Id);

        Assert.DoesNotContain(mainAnalysis.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Contains(languageService.Hover(workspace, mainDocument.Id, mainDocument.Text.IndexOf("decoder.decode", StringComparison.Ordinal))!.Markdown, "throws DecodeError<String>");
        Assert.Contains(languageService.Hover(workspace, mainDocument.Id, mainDocument.Text.IndexOf("loadMissing", StringComparison.Ordinal))!.Markdown, "throws FileError");

        var build = await scenario.BuildAsync(compilation);
        Assert.True(build.Success, build.StandardOutput + build.StandardError +
                                       string.Join(Environment.NewLine, build.Diagnostics.Select(d => d.Code + ": " + d.Message)));

        var run = await new MartinExecutionService().RunAsync(build, new ExecutionOptions());
        AssertRunSucceeded(run);
        Assert.Equal(string.Join(Environment.NewLine, "payload-42", "missing-phase15.txt") + Environment.NewLine, run.StandardOutput);
    }

    [Fact]
    public async Task CrossFileBuildMapsDiagnostics()
    {
        using var scenario = new PipelineScenario(root);
        var result = await scenario.BuildAsync(
            ("main.martin", "func main() { helper() }"),
            ("helpers.martin", "func helper() { print(missing) }"));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Location.FilePath == "helpers.martin");
    }

    [Fact]
    public async Task CancelledBuildPreservesPriorExecutable()
    {
        using var scenario = new PipelineScenario(root);
        var first = await scenario.BuildAsync(("main.martin", "func main() { print(\"previous\") }"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var cancelled = await scenario.BuildAsync(cts.Token, ("main.martin", "func main() { print(\"new\") }"));
        var run = await new MartinExecutionService().RunAsync(first, new ExecutionOptions());

        Assert.True(first.Success);
        Assert.Equal(BuildStatus.Cancelled, cancelled.Status);
        AssertRunSucceeded(run);
        Assert.Equal("previous" + Environment.NewLine, run.StandardOutput);
    }

    [Fact]
    public async Task RepeatedBuildReplacesOutputAtomically()
    {
        using var scenario = new PipelineScenario(root);
        var first = await scenario.BuildAsync(("main.martin", "func main() { print(\"first\") }"));
        await File.WriteAllTextAsync(Path.Combine(scenario.OutputDirectory, "stale.tmp"), "stale");
        var second = await scenario.BuildAsync(("main.martin", "func main() { print(\"second\") }"));
        var run = await new MartinExecutionService().RunAsync(second, new ExecutionOptions());

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.False(File.Exists(Path.Combine(scenario.OutputDirectory, "stale.tmp")));
        AssertRunSucceeded(run);
        Assert.Equal("second" + Environment.NewLine, run.StandardOutput);
    }

    [Fact]
    public async Task IncompatibleRuntimeStopsBuild()
    {
        using var scenario = new PipelineScenario(root);
        var runtime = Path.Combine(scenario.WorkDirectory, "not-a-runtime.dll");
        await File.WriteAllTextAsync(runtime, "not an assembly");

        var result = await scenario.BuildAsync(new BuildOptionsOverride { RuntimePath = runtime }, ("main.martin", "func main() { }"));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code is "MRT3201" or "MRT3202" or "MRT3203" or "MRT3221");
    }

    [Fact]
    public async Task LibraryOutputRequestFailsWithStableDiagnostic()
    {
        using var scenario = new PipelineScenario(root);
        var result = await scenario.BuildAsync(new BuildOptionsOverride { OutputKind = OutputKind.Library }, ("main.martin", "func main() { }"));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "MRT3014");
    }

    async Task<ExecutionResult> BuildAndRunAsync(string source, ExecutionOptions? options = null)
    {
        using var scenario = new PipelineScenario(root);
        var build = await scenario.BuildAsync(("main.martin", source));
        Assert.True(build.Success, build.StandardOutput + build.StandardError + string.Join(Environment.NewLine, build.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        return await new MartinExecutionService().RunAsync(build, options ?? new ExecutionOptions());
    }

    static void AssertRunSucceeded(ExecutionResult run)
    {
        Assert.Equal(ExecutionStatus.Completed, run.Status);
        Assert.Equal(0, run.ExitCode);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
        }
    }

    sealed class PipelineScenario : IDisposable
    {
        public PipelineScenario(string root)
        {
            WorkDirectory = Path.Combine(root, Guid.NewGuid().ToString("N"));
            OutputDirectory = Path.Combine(WorkDirectory, "out");
            Directory.CreateDirectory(OutputDirectory);
        }

        public string WorkDirectory { get; }
        public string OutputDirectory { get; }

        public Task<BuildResult> BuildAsync(params(string Path, string Source)[] sources) => BuildAsync(CancellationToken.None, sources);

        public Task<BuildResult> BuildAsync(Compilation compilation) =>
            BuildCompilationAsync(compilation, new BuildOptionsOverride(), CancellationToken.None);

        public Task<BuildResult> BuildAsync(CancellationToken cancellationToken, params(string Path, string Source)[] sources) =>
            BuildAsync(new BuildOptionsOverride(), cancellationToken, sources);

        public Task<BuildResult> BuildAsync(BuildOptionsOverride overrides, params(string Path, string Source)[] sources) =>
            BuildAsync(overrides, CancellationToken.None, sources);

        public Task<BuildResult> BuildAsync(BuildOptionsOverride overrides, CancellationToken cancellationToken, params(string Path, string Source)[] sources)
        {
            var compilation = Compilation.Create(sources.Select(source => SyntaxTree.Parse(source.Source, source.Path)));
            return BuildCompilationAsync(compilation, overrides, cancellationToken);
        }

        Task<BuildResult> BuildCompilationAsync(Compilation compilation, BuildOptionsOverride overrides, CancellationToken cancellationToken)
        {
            return new MartinBuildService().BuildAsync(compilation, new BuildOptions { OutputDirectory = OutputDirectory, AssemblyName = "Phase9Integration" + Guid.NewGuid().ToString("N")[..8], UseAppHost = false, OutputKind = overrides.OutputKind, RuntimePath = overrides.RuntimePath }, cancellationToken);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(WorkDirectory))
                    Directory.Delete(WorkDirectory, recursive: true);
            }
            catch
            {
            }
        }
    }

    sealed record BuildOptionsOverride
    {
        public OutputKind OutputKind { get; init; } = OutputKind.ConsoleApplication;
        public string ? RuntimePath { get; init; }
    }
}
