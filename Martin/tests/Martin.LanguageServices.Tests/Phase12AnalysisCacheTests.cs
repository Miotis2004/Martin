using System.Collections.Immutable;
using Martin.Compiler;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.LanguageServices.Tests;

public sealed class Phase12AnalysisCacheTests
{
    [Fact]
    public void Semantic_features_share_the_exact_project_analysis()
    {
        using var cache = new LanguageAnalysisCache();
        var service = new MartinLanguageService(cache);
        var document = new LanguageDocumentSnapshot(DocumentId.CreateNew(), "main.martin",
                                                    "func add(_ value: Int) -> Int { return value }\nfunc main() { print(add(1)) }", new(0));
        var project = new LanguageProjectSnapshot(ProjectId.CreateNew(), "p", ".", new(0), [document]);
        var workspace = new LanguageWorkspaceSnapshot(WorkspaceId.CreateNew(), new(0), [project]);
        var call = document.Text.LastIndexOf("add", StringComparison.Ordinal);

        service.Complete(workspace, document.Id, call);
        service.Hover(workspace, document.Id, call);
        service.GoToDefinition(workspace, document.Id, call);
        service.FindReferences(workspace, "add");
        service.GetSignatureHelp(workspace, document.Id, document.Text.LastIndexOf('1'));

        Assert.Equal(1, cache.Metrics.ProjectEntries);
        Assert.Equal(1, cache.Metrics.Misses);
        Assert.Equal(4, cache.Metrics.Hits);
    }

    [Fact]
    public async Task Concurrent_requests_share_population_and_return_leases()
    {
        using var cache = new LanguageAnalysisCache();
        var id = DocumentId.CreateNew();
        var calls = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<DocumentAnalysis> Populate(CancellationToken _)
        {
            Interlocked.Increment(ref calls);
            await release.Task;
            return CreateDocument(id, new(1), "let value = 1");
        }

        var first = cache.GetOrCreateDocumentAsync(new(id, new(1)), Populate);
        var second = cache.GetOrCreateDocumentAsync(new(id, new(1)), Populate);
        release.SetResult();
        using var firstLease = await first;
        using var secondLease = await second;

        Assert.Equal(1, calls);
        Assert.Same(firstLease.Value, secondLease.Value);
        Assert.Equal(1, cache.Metrics.Misses);
        Assert.Equal(1, cache.Metrics.Hits);
    }

    [Fact]
    public async Task Cancelling_one_waiter_does_not_cancel_shared_population()
    {
        using var cache = new LanguageAnalysisCache();
        var id = DocumentId.CreateNew();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<DocumentAnalysis> Populate(CancellationToken cancellationToken)
        {
            started.SetResult();
            await release.Task.WaitAsync(cancellationToken);
            return CreateDocument(id, new(1), "let value = 1");
        }

        using var waiterCancellation = new CancellationTokenSource();
        var cancelledWaiter = cache.GetOrCreateDocumentAsync(new(id, new(1)), Populate, cancellationToken: waiterCancellation.Token);
        var survivingWaiter = cache.GetOrCreateDocumentAsync(new(id, new(1)), Populate);
        await started.Task;
        waiterCancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => cancelledWaiter);

        release.SetResult();
        using var lease = await survivingWaiter;
        Assert.Equal(new DocumentVersion(1), lease.Value.Version);
    }

    [Fact]
    public async Task Superseding_project_version_cancels_shared_population()
    {
        using var cache = new LanguageAnalysisCache();
        var projectId = ProjectId.CreateNew();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var compilation = Compilation.Create(SyntaxTree.Parse("func main() {}"));

        async Task<ProjectAnalysis> Populate(CancellationToken cancellationToken)
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new(projectId, new(1), compilation, ImmutableDictionary<DocumentId, SemanticModel>.Empty, []);
        }

        var population = cache.GetOrCreateProjectAsync(new(projectId, new(1)), Populate);
        await started.Task;
        cache.RetainCurrent(new LanguageProjectSnapshot(projectId, "p", ".", new(2), []));

        await Assert.ThrowsAsync<OperationCanceledException>(() => population);
        Assert.Equal(0, cache.Metrics.ProjectEntries);
    }

    [Fact]
    public async Task Lru_is_bounded_but_current_and_leased_versions_are_retained()
    {
        using var cache = new LanguageAnalysisCache(new() { MaximumDocumentVersions = 1, MaximumProjectVersions = 1, ApproximateMemoryLimitBytes = 1024 });
        var project = ProjectId.CreateNew();
        var id = DocumentId.CreateNew();
        var current = new LanguageDocumentSnapshot(id, "current.martin", "let current = 1", new(2)) { ProjectId = project };
        cache.RetainCurrent(new LanguageWorkspaceSnapshot(
            WorkspaceId.CreateNew(),
            new(1),
            [new(project, "p", ".", new(1), [current])]));

        using var currentLease = await cache.GetOrCreateDocumentAsync(new(id, new(2)),
                                                                      _ => Task.FromResult(CreateDocument(id, new(2), current.Text)));
        var historicalId = DocumentId.CreateNew();
        using var historicalLease = await cache.GetOrCreateDocumentAsync(new(historicalId, new(1)),
                                                                         _ => Task.FromResult(CreateDocument(historicalId, new(1), "let old = 0")));
        Assert.Equal(2, cache.Metrics.DocumentEntries); // the over-capacity entry is actively leased
        historicalLease.Dispose();

        Assert.Equal(1, cache.Metrics.DocumentEntries);
        Assert.True(cache.TryGet(id, new(2), out _));
    }

    [Fact]
    public async Task Clearing_project_removes_document_and_project_analyses()
    {
        using var cache = new LanguageAnalysisCache();
        var projectId = ProjectId.CreateNew();
        var documentId = DocumentId.CreateNew();
        var document = new LanguageDocumentSnapshot(documentId, "main.martin", "func main() {}", new(0)) { ProjectId = projectId };
        cache.RetainCurrent(new LanguageWorkspaceSnapshot(
            WorkspaceId.CreateNew(),
            new(0),
            [new(projectId, "p", ".", new(0), [document])]));
        using var documentLease = await cache.GetOrCreateDocumentAsync(new(documentId, new(0)),
                                                                       _ => Task.FromResult(CreateDocument(documentId, new(0), document.Text)));
        var compilation = documentLease.Value.Compilation;
        using var projectLease = await cache.GetOrCreateProjectAsync(new(projectId, new(0)),
                                                                     _ => Task.FromResult(new ProjectAnalysis(projectId, new(0), compilation, ImmutableDictionary<DocumentId, SemanticModel>.Empty, [])));

        documentLease.Dispose();
        projectLease.Dispose();
        cache.ClearProject(projectId);

        Assert.Equal(0, cache.Metrics.DocumentEntries);
        Assert.Equal(0, cache.Metrics.ProjectEntries);
    }

    [Fact]
    public async Task Closing_project_clears_service_analysis_entries()
    {
        using var cache = new LanguageAnalysisCache();
        var service = new MartinLanguageService(cache);
        var projectId = ProjectId.CreateNew();
        var document = new LanguageDocumentSnapshot(DocumentId.CreateNew(), "main.martin", "func main() {}", new(0)) {
            ProjectId = projectId
        };
        var project = new LanguageProjectSnapshot(projectId, "p", ".", new(0), [document]);
        var workspace = new LanguageWorkspaceSnapshot(WorkspaceId.CreateNew(), new(0), [project]);

        await service.AnalyzeDocumentAsync(workspace, document.Id);
        Assert.NotEqual(0, cache.Metrics.DocumentEntries + cache.Metrics.ProjectEntries);

        service.CloseProject(projectId);

        Assert.Equal(0, cache.Metrics.DocumentEntries);
        Assert.Equal(0, cache.Metrics.ProjectEntries);
    }

    private static DocumentAnalysis CreateDocument(DocumentId id, DocumentVersion version, string text)
    {
        var tree = SyntaxTree.Parse(text);
        return new(id, version, tree, Compilation.Create(tree), [], []);
    }
}
