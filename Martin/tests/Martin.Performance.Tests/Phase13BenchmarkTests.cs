using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Martin.Compiler;
using Martin.Compiler.Syntax;
using Martin.LanguageServices;
using Xunit;
using Xunit.Abstractions;

namespace Martin.Performance.Tests;

/// <summary>
/// Reproducible Phase 13 characterization benchmarks. Run the command documented in
/// Docs/phase-13-performance.md in Release mode; timings are deliberately not assertions.
/// </summary>
public sealed class Phase13BenchmarkTests(ITestOutputHelper output)
{
    [Fact(Timeout = 180_000)]
    public async Task RunRequiredWorkloads()
    {
        var results = new List<Phase13BenchmarkResult>();
        Measure(results, "large-enum-switch-bind-exhaustiveness-decision", 100,
            () => Compilation.Create(SyntaxTree.Parse(Phase13Data.EnumSwitch(100))).BindProgram());
        Measure(results, "nested-pattern-analysis", 10,
            () => Compilation.Create(SyntaxTree.Parse(Phase13Data.NestedOptionalSwitch(10))).BindProgram());
        Measure(results, "large-protocol-witness-matching", 100,
            () => Compilation.Create(SyntaxTree.Parse(Phase13Data.Protocol(100, 1))).BindProgram());
        Measure(results, "many-conformances", 100,
            () => Compilation.Create(SyntaxTree.Parse(Phase13Data.Protocol(10, 100))).BindProgram());

        var data = Phase13Data.Workspace(100);
        using var cache = new LanguageAnalysisCache(new()
        {
            MaximumDocumentVersions = 4,
            MaximumProjectVersions = 2,
            ApproximateMemoryLimitBytes = 16 * 1024 * 1024
        });
        var service = new MartinLanguageService(cache);
        await MeasureAsync(results, "pattern-completion", 1, () => service.CompleteAsync(data.Workspace,
            Phase13Data.Request(data.Workspace, data.Project, data.Document, data.CompletionPosition)));
        await MeasureAsync(results, "witness-navigation", 1, () => service.GetDefinitionsAsync(data.Workspace,
            new DefinitionRequest
            {
                WorkspaceId = data.Workspace.Id, WorkspaceVersion = data.Workspace.Version,
                ProjectId = data.Project.Id, ProjectVersion = data.Project.Version,
                DocumentId = data.Document.Id, DocumentVersion = data.Document.Version,
                Position = data.NavigationPosition
            }));

        var report = new Phase13BenchmarkReport(1, results, cache.Metrics);
        output.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Assert.All(results, result => Assert.True(result.ElapsedMilliseconds >= 0));
        Assert.InRange(report.Cache.ProjectEntries, 0, 2);
    }

    [Fact(Timeout = 30_000)]
    public async Task CancellationAndProjectCloseAreObserved()
    {
        var data = Phase13Data.Workspace(100);
        using var cache = new LanguageAnalysisCache();
        var service = new MartinLanguageService(cache);
        await service.AnalyzeDocumentAsync(data.Workspace, data.Document.Id);
        Assert.True(cache.Metrics.ProjectEntries > 0);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var timer = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CompleteAsync(data.Workspace,
                Phase13Data.Request(data.Workspace, data.Project, data.Document, data.CompletionPosition), cancellation.Token));
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(1));

        service.CloseProject(data.Project.Id);
        Assert.Equal(0, cache.Metrics.ProjectEntries);
        Assert.Equal(0, cache.Metrics.DocumentEntries);
    }

    private static void Measure(List<Phase13BenchmarkResult> results, string name, int scale, Action action)
    {
        action();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        action();
        timer.Stop();
        results.Add(new(name, scale, timer.Elapsed.TotalMilliseconds,
            GC.GetAllocatedBytesForCurrentThread() - allocated));
    }

    private static async Task MeasureAsync<T>(List<Phase13BenchmarkResult> results, string name, int scale, Func<Task<T>> action)
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

public sealed record Phase13BenchmarkResult(string Name, int Scale, double ElapsedMilliseconds, long AllocatedBytes);
public sealed record Phase13BenchmarkReport(int SchemaVersion, IReadOnlyList<Phase13BenchmarkResult> Results, AnalysisCacheMetrics Cache);

internal static class Phase13Data
{
    public static string EnumSwitch(int count)
    {
        var cases = string.Join(' ', Enumerable.Range(0, count).Select(i => $"case c{i}"));
        var arms = string.Join(' ', Enumerable.Range(0, count).Select(i => $"case .c{i}: return {i}"));
        return $"enum E {{ {cases} }} func run(_ value: E) -> Int {{ switch value {{ {arms} }} }}";
    }

    public static string NestedOptionalSwitch(int depth)
    {
        var type = "Bool" + string.Concat(Enumerable.Repeat("?", depth));
        var pattern = "true";
        for (var i = 0; i < depth; i++) pattern = $".some({pattern})";
        return $"func run(_ value: {type}) -> Int {{ switch value {{ case {pattern}: return 1 default: return 0 }} }}";
    }

    public static string Protocol(int requirements, int conformances)
    {
        var requirementText = string.Join(' ', Enumerable.Range(0, requirements).Select(i => $"func r{i}(_ value: Int) -> Int"));
        var witnesses = string.Join(' ', Enumerable.Range(0, requirements).Select(i => $"func r{i}(_ value: Int) -> Int {{ return value }}"));
        var types = string.Join(' ', Enumerable.Range(0, conformances).Select(i => $"struct S{i}: P {{ {witnesses} }}"));
        return $"protocol P {{ {requirementText} }} {types}";
    }

    public static (LanguageWorkspaceSnapshot Workspace, LanguageProjectSnapshot Project,
        LanguageDocumentSnapshot Document, int CompletionPosition, int NavigationPosition) Workspace(int cases)
    {
        var source = EnumSwitch(cases) + "\nfunc completion(_ value: E) -> Int { switch value { case  } }";
        var completion = source.IndexOf("case  }", StringComparison.Ordinal) + 5;
        var navigation = source.IndexOf(".c0", StringComparison.Ordinal) + 2;
        var projectId = ProjectId.CreateNew();
        var document = new LanguageDocumentSnapshot(DocumentId.CreateNew(), "phase13-benchmark.martin", source, new(0)) { ProjectId = projectId };
        var project = new LanguageProjectSnapshot(projectId, "phase13-benchmark", ".", new(0), [document]);
        return (new(WorkspaceId.CreateNew(), new(0), [project]), project, document, completion, navigation);
    }

    public static CompletionRequest Request(LanguageWorkspaceSnapshot workspace, LanguageProjectSnapshot project,
        LanguageDocumentSnapshot document, int position) => new()
    {
        WorkspaceId = workspace.Id, WorkspaceVersion = workspace.Version,
        ProjectId = project.Id, ProjectVersion = project.Version,
        DocumentId = document.Id, DocumentVersion = document.Version, Position = position
    };
}
