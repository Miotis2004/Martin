using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using Martin.Compiler;
using Martin.Compiler.Generics;
using Martin.Compiler.Symbols;
using Martin.Compiler.Syntax;
using Martin.LanguageServices;
using Xunit;
using Xunit.Abstractions;

namespace Martin.Performance.Tests;

/// <summary>Dependency-free Phase 14 characterization; timings are observations, not CI gates.</summary>
public sealed class Phase14BenchmarkTests(ITestOutputHelper output)
{
    [Fact(Timeout = 180_000)]
    public async Task RunRequiredWorkloads()
    {
        var results = new List<Phase14BenchmarkResult>();
        var compilation = Compilation.Create(SyntaxTree.Parse(Phase14Data.Program(200), "generics.martin"));
        Measure(results, "cross-file-generic-bind-inference-constraints", 200,
            () => compilation.BindProgram());

        var definition = GenericDefinition("Box", 1);
        definition.AddMember(new PropertySymbol("value", definition, definition.TypeParameters[0], true, null, []));
        var factory = new GenericTypeFactory();
        Measure(results, "repeated-identical-construction", 10_000,
            () => { for (var i = 0; i < 10_000; i++) factory.Construct(definition, [TypeSymbol.Int]); });
        Measure(results, "distinct-nested-construction", 200, () =>
        {
            TypeSymbol argument = TypeSymbol.Int;
            for (var i = 0; i < 200; i++) argument = factory.Construct(definition, [argument]);
        });

        var workspace = Phase14Data.Workspace(50);
        using var cache = new LanguageAnalysisCache(new()
        {
            MaximumDocumentVersions = 4, MaximumProjectVersions = 2,
            ApproximateMemoryLimitBytes = 16 * 1024 * 1024
        });
        var service = new MartinLanguageService(cache);
        await MeasureAsync(results, "generic-completion", 50,
            () => service.CompleteAsync(workspace.Workspace, workspace.Request));

        var report = new Phase14BenchmarkReport(1, results, factory.ConstructionCount, cache.Metrics);
        output.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Assert.All(results, result => Assert.True(result.ElapsedMilliseconds >= 0));
        Assert.Equal(200, report.GenericConstructionCount);
        Assert.InRange(report.Cache.ProjectEntries, 0, 2);
    }

    [Fact(Timeout = 30_000)]
    public async Task CancellationBoundsAndProjectCloseAreObserved()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var definition = GenericDefinition("Box", 1);
        var parameter = definition.TypeParameters[0];
        var factory = new GenericTypeFactory();
        Assert.Throws<OperationCanceledException>(() => factory.Construct(definition, [TypeSymbol.Int], cancelled.Token));
        Assert.Throws<OperationCanceledException>(() => new TypeSubstitution(
            ImmutableDictionary<TypeParameterSymbol, TypeSymbol>.Empty.Add(parameter, TypeSymbol.Int),
            factory, cancelled.Token).Substitute(parameter));
        Assert.Throws<OperationCanceledException>(() => new GenericInferenceEngine().Infer([parameter],
            [new InferenceEquation(parameter, TypeSymbol.Int)], cancelled.Token));
        Assert.Throws<OperationCanceledException>(() => new GenericConstraintValidator([])
            .Validate(parameter, TypeSymbol.Int, cancelled.Token));

        TypeSymbol nested = parameter;
        for (var i = 0; i <= TypeSubstitution.MaximumDepth; i++) nested = new OptionalTypeSymbol(nested);
        Assert.Throws<InvalidOperationException>(() => new TypeSubstitution(
            ImmutableDictionary<TypeParameterSymbol, TypeSymbol>.Empty.Add(parameter, TypeSymbol.Int), factory)
            .Substitute(nested));

        var data = Phase14Data.Workspace(20);
        using var cache = new LanguageAnalysisCache();
        var service = new MartinLanguageService(cache);
        await service.AnalyzeDocumentAsync(data.Workspace, data.Document.Id);
        Assert.True(cache.Metrics.ProjectEntries > 0);
        service.CloseProject(data.Project.Id);
        Assert.Equal(0, cache.Metrics.ProjectEntries);
        Assert.Equal(0, cache.Metrics.DocumentEntries);
    }

    private static void Measure(List<Phase14BenchmarkResult> results, string name, int scale, Action action)
    {
        action();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew(); action(); timer.Stop();
        results.Add(new(name, scale, timer.Elapsed.TotalMilliseconds,
            GC.GetAllocatedBytesForCurrentThread() - allocated));
    }

    private static async Task MeasureAsync<T>(List<Phase14BenchmarkResult> results, string name, int scale, Func<Task<T>> action)
    {
        await action();
        var allocated = GC.GetTotalAllocatedBytes(true);
        var timer = Stopwatch.StartNew(); await action(); timer.Stop();
        results.Add(new(name, scale, timer.Elapsed.TotalMilliseconds,
            GC.GetTotalAllocatedBytes(false) - allocated));
    }

    private static StructTypeSymbol GenericDefinition(string name, int arity)
    {
        var owner = new StructTypeSymbol(name, [], []);
        var parameters = Enumerable.Range(0, arity)
            .Select(i => new TypeParameterSymbol($"T{i}", i, owner, [])).ToImmutableArray();
        var definition = new StructTypeSymbol(name, [], parameters);
        foreach (var parameter in parameters) parameter.SetContainingSymbol(definition);
        return definition;
    }
}

public sealed record Phase14BenchmarkResult(string Name, int Scale, double ElapsedMilliseconds, long AllocatedBytes);
public sealed record Phase14BenchmarkReport(int SchemaVersion, IReadOnlyList<Phase14BenchmarkResult> Results,
    int GenericConstructionCount, AnalysisCacheMetrics Cache);

internal static class Phase14Data
{
    public static string Program(int calls) => """
        protocol Describable { func description() -> String }
        struct Item: Describable { func description() -> String { return "item" } }
        struct Box<T> { let value: T }
        func identity<T>(_ value: T) -> T { return value }
        func describe<T: Describable>(_ value: T) -> String { return value.description() }
        """ + string.Join('\n', Enumerable.Range(0, calls)
            .Select(i => $"func use{i}(_ value: Item) -> Item {{ return identity<Item>(value) }}"));

    public static (LanguageWorkspaceSnapshot Workspace, LanguageProjectSnapshot Project,
        LanguageDocumentSnapshot Document, CompletionRequest Request) Workspace(int declarations)
    {
        var projectId = ProjectId.CreateNew();
        var definitions = new LanguageDocumentSnapshot(DocumentId.CreateNew(), "definitions.martin",
            "struct Box<T> { let value: T } func identity<T>(_ value: T) -> T { return value }", new(0)) { ProjectId = projectId };
        var source = string.Join('\n', Enumerable.Range(0, declarations).Select(i => $"func f{i}(_ value: Int) -> Int {{ return identity(value) }}")) +
            "\nfunc completion(_ value: Box<Int>) -> Int { return value. }";
        var document = new LanguageDocumentSnapshot(DocumentId.CreateNew(), "usage.martin", source, new(0)) { ProjectId = projectId };
        var project = new LanguageProjectSnapshot(projectId, "phase14-benchmark", ".", new(0), [definitions, document]);
        var workspace = new LanguageWorkspaceSnapshot(WorkspaceId.CreateNew(), new(0), [project]);
        var request = new CompletionRequest
        {
            WorkspaceId = workspace.Id, WorkspaceVersion = workspace.Version, ProjectId = project.Id,
            ProjectVersion = project.Version, DocumentId = document.Id, DocumentVersion = document.Version,
            Position = source.LastIndexOf(".", StringComparison.Ordinal) + 1
        };
        return (workspace, project, document, request);
    }
}
