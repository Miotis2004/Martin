using System.Collections.Immutable;

namespace Martin.LanguageServices;

/// <summary>
/// Connects workspace changes to the two diagnostic debounce queues and publishes
/// immutable replacement snapshots. A snapshot is only published if every identity
/// still matches the current workspace.
/// </summary>
public sealed class LiveDiagnosticPublisher : IAsyncDisposable
{
    readonly ILanguageWorkspace _workspace;
    readonly AnalysisScheduler _scheduler;
    readonly MartinLanguageService _languageService;
    readonly object _gate = new();
    readonly Dictionary<(ProjectId, LanguageDiagnosticSource), DiagnosticSnapshot> _current = [];
    bool _disposed;

    public LiveDiagnosticPublisher(ILanguageWorkspace workspace, AnalysisScheduler scheduler, MartinLanguageService languageService)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _languageService = languageService ?? throw new ArgumentNullException(nameof(languageService));
        _workspace.WorkspaceChanged += OnWorkspaceChanged;
        _scheduler.DocumentDiagnosticsRequested += PublishSyntaxAsync;
        _scheduler.ProjectAnalysisRequested += PublishSemanticAsync;
    }

    public event EventHandler<DiagnosticSnapshotEventArgs>? SnapshotPublished;

    public ImmutableArray<DiagnosticSnapshot> CurrentSnapshots
    {
        get {
            lock (_gate) return _current.Values.OrderBy(x => x.ProjectId.Value).ThenBy(x => x.Source).ToImmutableArray();
        }
    }

    public void ScheduleProject(ProjectId projectId)
    {
        var project = _workspace.CurrentSnapshot.FindProject(projectId);
        if (project is null)
            return;
        foreach (var document in project.Documents)
            _scheduler.ScheduleDocumentDiagnostics(document.Id, document.Version);
        _scheduler.ScheduleProjectAnalysis(project.Id, project.Version);
    }

    async Task PublishSyntaxAsync(DocumentId documentId, DocumentVersion version, CancellationToken cancellationToken)
    {
        var before = _workspace.CurrentSnapshot;
        var document = before.FindDocument(documentId);
        if (document is null || document.Version != version)
            return;
        var project = before.FindProject(document.ProjectId);
        if (project is null)
            return;
        var diagnostics = await _languageService.GetSyntaxDiagnosticsAsync(document, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var current = _workspace.CurrentSnapshot;
        var currentProject = current.FindProject(project.Id);
        var currentDocument = current.FindDocument(documentId);
        if (current.Id != before.Id || currentProject?.Version != project.Version || currentDocument?.Version != version)
            return;

        DiagnosticSnapshot snapshot;
        lock (_gate)
        {
            var key = (project.Id, LanguageDiagnosticSource.LiveSyntax);
            _current.TryGetValue(key, out var prior);
            var documents = prior?.ProjectVersion == project.Version
                                ? prior.Documents
                                : project.Documents.ToImmutableDictionary(d => d.Id, d => new DocumentDiagnosticSet {
                                      DocumentId = d.Id,
                                      DocumentVersion = d.Version
                                  });
            documents = documents.SetItem(documentId, new DocumentDiagnosticSet {
                DocumentId = documentId,
                DocumentVersion = version,
                Diagnostics = Mark(diagnostics, LanguageDiagnosticSource.LiveSyntax)
            });
            snapshot = new() { ProjectId = project.Id, ProjectVersion = project.Version, Source = LanguageDiagnosticSource.LiveSyntax, Documents = documents };
            _current[key] = snapshot;
        }
        SnapshotPublished?.Invoke(this, new(snapshot));
    }

    async Task PublishSemanticAsync(ProjectId projectId, ProjectVersion version, CancellationToken cancellationToken)
    {
        var before = _workspace.CurrentSnapshot;
        var project = before.FindProject(projectId);
        if (project is null || project.Version != version)
            return;
        var diagnostics = await _languageService.GetProjectSemanticDiagnosticsAsync(project, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var current = _workspace.CurrentSnapshot;
        var currentProject = current.FindProject(projectId);
        if (current.Id != before.Id || currentProject?.Version != version)
            return;

        var byPath = project.Documents.ToDictionary(d => Path.GetFullPath(d.FilePath), PathComparer);
        var builders = project.Documents.ToDictionary(d => d.Id,
                                                      _ => ImmutableArray.CreateBuilder<LanguageDiagnostic>());
        foreach (var diagnostic in diagnostics)
            if (!string.IsNullOrWhiteSpace(diagnostic.FilePath) && byPath.TryGetValue(Path.GetFullPath(diagnostic.FilePath), out var document))
                builders[document.Id].Add(diagnostic);
        var sets = project.Documents.ToImmutableDictionary(d => d.Id, d => new DocumentDiagnosticSet {
            DocumentId = d.Id,
            DocumentVersion = d.Version,
            Diagnostics = Mark(builders[d.Id].ToImmutable(), LanguageDiagnosticSource.LiveSemantic)
        });
        var snapshot = new DiagnosticSnapshot { ProjectId = projectId, ProjectVersion = version, Source = LanguageDiagnosticSource.LiveSemantic, Documents = sets };
        lock (_gate) _current[(projectId, LanguageDiagnosticSource.LiveSemantic)] = snapshot;
        SnapshotPublished?.Invoke(this, new(snapshot));
    }

    void OnWorkspaceChanged(object? sender, LanguageWorkspaceChangedEventArgs e)
    {
        if (e.Change.Kind == LanguageWorkspaceChangeKind.ProjectClosed && e.Change.ProjectId is {} closed)
        {
            _scheduler.CancelProject(closed);
            var old = e.OldSnapshot.FindProject(closed);
            if (old is null)
                return;
            foreach (var document in old.Documents)
                _scheduler.CancelDocument(document.Id);
            foreach (var source in new[] { LanguageDiagnosticSource.LiveSyntax, LanguageDiagnosticSource.LiveSemantic })
            {
                var cleared = new DiagnosticSnapshot { ProjectId = closed, ProjectVersion = old.Version, Source = source };
                lock (_gate) _current.Remove((closed, source));
                SnapshotPublished?.Invoke(this, new(cleared));
            }
            return;
        }
        if (e.Change.ProjectId is {} projectId)
        {
            var project = e.NewSnapshot.FindProject(projectId);
            if (project is null)
                return;
            // Clear markers carrying an older project/document identity immediately;
            // the debounced replacements will populate these exact-version sets.
            foreach (var source in new[] { LanguageDiagnosticSource.LiveSyntax, LanguageDiagnosticSource.LiveSemantic })
            {
                var emptyDocuments = project.Documents.ToImmutableDictionary(d => d.Id, d => new DocumentDiagnosticSet {
                    DocumentId = d.Id,
                    DocumentVersion = d.Version
                });
                var cleared = new DiagnosticSnapshot {
                    ProjectId = project.Id,
                    ProjectVersion = project.Version,
                    Source = source,
                    Documents = emptyDocuments
                };
                lock (_gate) _current[(project.Id, source)] = cleared;
                SnapshotPublished?.Invoke(this, new(cleared));
            }
            ScheduleProject(projectId);
        }
    }

    static ImmutableArray<LanguageDiagnostic> Mark(IEnumerable<LanguageDiagnostic> diagnostics, LanguageDiagnosticSource source) =>
        diagnostics.Select(d => d with { DiagnosticSource = source, Source = source == LanguageDiagnosticSource.LiveSyntax ? "Martin.Live.Syntax" : "Martin.Live.Semantic" })
            .DistinctBy(d => (d.Code, d.FilePath, d.Span, d.Message))
            .ToImmutableArray();

    static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;
        _disposed = true;
        _workspace.WorkspaceChanged -= OnWorkspaceChanged;
        _scheduler.DocumentDiagnosticsRequested -= PublishSyntaxAsync;
        _scheduler.ProjectAnalysisRequested -= PublishSemanticAsync;
        lock (_gate) _current.Clear();
        return ValueTask.CompletedTask;
    }
}
