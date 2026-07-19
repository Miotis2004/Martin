using System.Diagnostics;
using System.Text.Json;
using Martin.Compiler;
using Martin.Compiler.Binding;
using Martin.Compiler.Syntax;
using Martin.Compiler.Symbols;
using Martin.LanguageServices;
using Xunit;
using Xunit.Abstractions;

namespace Martin.Performance.Tests;

/// <summary>Dependency-free Phase 15 characterization; timings are observations, not CI gates.</summary>
public sealed class Phase15BenchmarkTests(ITestOutputHelper output)
{
    [Fact(Timeout = 180_000)]
    public async Task RunRequiredTypedErrorWorkloads()
    {
        var results = new List<Phase15BenchmarkResult>();

        Measure(results, "large-effect-body", 750, () => Bind(Phase15Data.LargeEffectBody(750), "large-effect-body.martin"));
        Measure(results, "deep-nested-try-expressions", 128, () => Bind(Phase15Data.DeepNestedTryExpressions(128), "nested-try.martin"));
        Measure(results, "sequential-throwing-calls-single-type", 1_000, () => Bind(Phase15Data.SequentialThrowingCalls(1_000), "sequential-throws.martin"));
        Measure(results, "nested-do-catch-statements", 96, () => Bind(Phase15Data.NestedDoCatchStatements(96), "nested-catches.martin"));
        Measure(results, "large-error-enum-catch-graph", 256, () => Bind(Phase15Data.LargeErrorEnumCatchGraph(256), "large-error-enum.martin"));
        Measure(results, "associated-value-catch-patterns", 128, () => Bind(Phase15Data.AssociatedValueCatchPatterns(128), "payload-catches.martin"));
        Measure(results, "generic-constructed-error-types", 200, () => Bind(Phase15Data.GenericConstructedErrors(200), "generic-errors.martin"));
        Measure(results, "cross-file-throwing-call-graph", 320, () => Bind(Phase15Data.CrossFileThrowingCallGraph(320)));

        var workspace = Phase15Data.RapidEditWorkspace(80);
        using var cache = new LanguageAnalysisCache(new() {
            MaximumDocumentVersions = 4,
            MaximumProjectVersions = 2,
            ApproximateMemoryLimitBytes = 16 * 1024 * 1024
        });
        var service = new MartinLanguageService(cache);
        await MeasureAsync(results, "typed-error-language-service-rapid-edits", 80,
                           () => service.AnalyzeDocumentAsync(workspace.Workspace, workspace.Document.Id));

        var report = new Phase15BenchmarkReport(1, results, cache.Metrics, PatternMatrixLimits.Default.MaximumRows,
                                                PatternMatrixLimits.Default.MaximumColumns, Phase15Data.MaximumGeneratedPatternNesting);
        output.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

        Assert.All(results, result =>
                            {
                                Assert.True(result.ElapsedMilliseconds >= 0);
                                Assert.True(result.AllocatedBytes >= 0);
                            });
        Assert.InRange(report.Cache.ProjectEntries, 0, 2);
        Assert.InRange(report.Cache.DocumentEntries, 0, 4);
    }

    [Fact(Timeout = 30_000)]
    public async Task CancellationComplexityAndProjectCloseSafeguardsAreObserved()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
                                                      Compilation.Create(SyntaxTree.Parse(Phase15Data.SequentialThrowingCalls(32), "cancelled.martin")).BindProgram(cancelled.Token));
        Assert.Throws<OperationCanceledException>(() =>
                                                      ErrorEffect.Combine(Enumerable.Repeat(new ErrorEffect(true, TypeSymbol.Int), 32), cancelled.Token));

        var tooLarge = Bind(Phase15Data.SwitchBeyondMatrixRowLimit(PatternMatrixLimits.Default.MaximumRows + 1), "too-large-switch.martin");
        Assert.Contains(tooLarge.Diagnostics, diagnostic => diagnostic.Code == "MRT2207");

        var tooDeep = Bind(Phase15Data.TooDeepPattern(257), "too-deep-pattern.martin");
        Assert.Contains(tooDeep.Diagnostics, diagnostic => diagnostic.Code == "MRT2202");

        var data = Phase15Data.RapidEditWorkspace(20);
        using var cache = new LanguageAnalysisCache();
        var service = new MartinLanguageService(cache);
        await service.AnalyzeDocumentAsync(data.Workspace, data.Document.Id);
        Assert.True(cache.Metrics.ProjectEntries > 0);
        service.CloseProject(data.Project.Id);
        Assert.Equal(0, cache.Metrics.ProjectEntries);
        Assert.Equal(0, cache.Metrics.DocumentEntries);

        var first = Bind("enum FirstError: Error { case failed } func first() throws FirstError { throw FirstError.failed }", "first.martin");
        var second = Bind("enum SecondError: Error { case failed } func second() throws SecondError { throw SecondError.failed }", "second.martin");
        Assert.Contains(first.NamedTypes, type => type.Name == "FirstError");
        Assert.DoesNotContain(second.NamedTypes, type => type.Name == "FirstError");
    }

    private static BoundProgram Bind(string source, string path) =>
        Compilation.Create(SyntaxTree.Parse(source, path)).BindProgram();

    private static BoundProgram Bind(IEnumerable<(string Source, string Path)> sources) =>
        Compilation.Create(sources.Select(source => SyntaxTree.Parse(source.Source, source.Path))).BindProgram();

    private static void Measure(List<Phase15BenchmarkResult> results, string name, int scale, Action action)
    {
        action();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        action();
        timer.Stop();
        results.Add(new(name, scale, timer.Elapsed.TotalMilliseconds,
                        GC.GetAllocatedBytesForCurrentThread() - allocated));
    }

    private static async Task MeasureAsync<T>(List<Phase15BenchmarkResult> results, string name, int scale, Func<Task<T>> action)
    {
        await action();
        var allocated = GC.GetTotalAllocatedBytes(true);
        var timer = Stopwatch.StartNew();
        await action();
        timer.Stop();
        results.Add(new(name, scale, timer.Elapsed.TotalMilliseconds,
                        GC.GetTotalAllocatedBytes(false) - allocated));
    }
}

public sealed record Phase15BenchmarkResult(string Name, int Scale, double ElapsedMilliseconds, long AllocatedBytes);
public sealed record Phase15BenchmarkReport(int SchemaVersion, IReadOnlyList<Phase15BenchmarkResult> Results,
                                            AnalysisCacheMetrics Cache, int PatternMatrixMaximumRows, int PatternMatrixMaximumColumns, int MaximumGeneratedPatternNesting);

internal static class Phase15Data
{
    public const int MaximumGeneratedPatternNesting = 256;

    public static string LargeEffectBody(int calls) => Header() + LoadFunction() +
                                                       "func run() throws LoadError -> Int {\n" + string.Join('\n', Enumerable.Range(0, calls).Select(i => $"let value{i}: Int = try load()")) + "\nreturn 0\n}";

    public static string DeepNestedTryExpressions(int depth) => Header() + LoadFunction() +
                                                                $"func run() throws LoadError -> Int {{ return {Enumerable.Repeat("try (", depth).Aggregate("load()", (current, prefix) => prefix + current + ")")} }}";

    public static string SequentialThrowingCalls(int calls) => Header() + LoadFunction() + string.Join('\n', Enumerable.Range(0, calls).Select(i => $"func run{i}() throws LoadError -> Int {{ return try load() }}"));

    public static string NestedDoCatchStatements(int depth) => Header() + LoadFunction() + "func run() {\n" +
                                                               string.Concat(Enumerable.Repeat("do {\n", depth)) + "let value = try load()\n" +
                                                               string.Concat(Enumerable.Repeat("} catch .missing { print(1) } catch .denied { print(2) }\n", depth)) + "}";

    public static string LargeErrorEnumCatchGraph(int cases)
    {
        var enumCases = string.Join(' ', Enumerable.Range(0, cases).Select(i => $"case c{i}"));
        var catches = string.Join('\n', Enumerable.Range(0, cases).Select(i => $"catch .c{i} {{ print({i}) }}"));
        return $"enum LargeError: Error {{ {enumCases} }}\nfunc load() throws LargeError -> Int {{ throw LargeError.c0 }}\nfunc run() {{ do {{ let value = try load() }}\n{catches}\n}}";
    }

    public static string AssociatedValueCatchPatterns(int cases)
    {
        var enumCases = string.Join(' ', Enumerable.Range(0, cases).Select(i => $"case c{i}(String)"));
        var catches = string.Join('\n', Enumerable.Range(0, cases).Select(i => $"catch .c{i}(let value{i}) {{ print(value{i}) }}"));
        return $"enum PayloadError: Error {{ {enumCases} }}\nfunc load() throws PayloadError -> Int {{ throw PayloadError.c0(\"x\") }}\nfunc run() {{ do {{ let value = try load() }}\n{catches}\n}}";
    }

    public static string GenericConstructedErrors(int functions) => "enum DecodeError<T>: Error { case invalid(value: T) }\n" +
                                                                    string.Join('\n', Enumerable.Range(0, functions).Select(i => $"func decode{i}() throws DecodeError<Int> -> Int {{ throw DecodeError<Int>.invalid(value: {i}) }}"));

    public static IEnumerable<(string Source, string Path)> CrossFileThrowingCallGraph(int functions)
    {
        yield return (Header() + LoadFunction(), "errors.martin");
        yield return (string.Join('\n', Enumerable.Range(0, functions).Select(i => $"func hop{i}() throws LoadError -> Int {{ return try {(i == 0 ? "load" : $"hop{i - 1}")}() }}")), "graph.martin");
    }

    public static string SwitchBeyondMatrixRowLimit(int rows) => "enum Big { case value }\nfunc run(_ value: Big) { switch value {\n" +
                                                                 string.Join('\n', Enumerable.Range(0, rows).Select(
                                                                                       _ => "case .value: print(1)")) +
                                                                 "\n}\n}";

    public static string TooDeepPattern(int depth) => "func run(_ value: Int" + string.Concat(Enumerable.Repeat("?", depth)) + ") { switch value {\ncase " +
                                                      string.Concat(Enumerable.Repeat(".some(", depth)) + "let item" + string.Concat(Enumerable.Repeat(")", depth)) + ": print(item)\n}\n}";

    public static (LanguageWorkspaceSnapshot Workspace, LanguageProjectSnapshot Project, LanguageDocumentSnapshot Document) RapidEditWorkspace(int declarations)
    {
        var projectId = ProjectId.CreateNew();
        var source = Header() + LoadFunction() + string.Join('\n', Enumerable.Range(0, declarations).Select(i => $"func edit{i}() throws LoadError -> Int {{ return try load() }}")) + "\nfunc main() { do { let value = try load() } catch .missing { print(1) } catch .denied { print(2) } }";
        var document = new LanguageDocumentSnapshot(DocumentId.CreateNew(), "phase15-edits.martin", source, new(17)) { ProjectId = projectId };
        var project = new LanguageProjectSnapshot(projectId, "phase15-benchmark", ".", new(17), [document]);
        return (new LanguageWorkspaceSnapshot(WorkspaceId.CreateNew(), new(17), [project]), project, document);
    }

    private static string Header() => "enum LoadError: Error { case missing case denied }\n";
    private static string LoadFunction() => "func load() throws LoadError -> Int { throw LoadError.missing }\n";
}
