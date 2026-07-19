using System.Collections.Immutable;
using Martin.Compiler.Text;
using Martin.ProjectSystem;

namespace Martin.LanguageServices;

/// <summary>A versioned workspace which owns stable identities for one editor lifetime.</summary>
public sealed class LanguageWorkspace : ILanguageWorkspace
{
    readonly object _gate = new();
    readonly Dictionary<string, ProjectId> _projectIds = new(PathComparer);
    readonly Dictionary<(ProjectId Project, string Path), DocumentId> _documentIds = new(new DocumentKeyComparer());
    readonly Dictionary<DocumentId, Tombstone> _tombstones = [];
    LanguageWorkspaceSnapshot _current = LanguageWorkspaceSnapshot.Empty();
    bool _disposed;

    public LanguageWorkspace(TextSynchronizationOptions? textSynchronizationOptions = null)
    {
        TextSynchronizationOptions = textSynchronizationOptions ?? new();
        if (TextSynchronizationOptions.MaximumDocumentSizeBytes <= 0 ||
            TextSynchronizationOptions.MaximumChangePayloadBytes <= 0 ||
            TextSynchronizationOptions.MaximumChanges <= 0)
            throw new ArgumentOutOfRangeException(nameof(textSynchronizationOptions), "Text synchronization limits must be positive.");
    }

    public TextSynchronizationOptions TextSynchronizationOptions { get; }

    public LanguageWorkspaceSnapshot CurrentSnapshot { get { lock (_gate) return _current; } }
    public event EventHandler<LanguageWorkspaceChangedEventArgs>? WorkspaceChanged;

    public async Task<ProjectId> OpenProjectAsync(MartinProject project, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var manifest = Canonical(project.ManifestPath);
        ProjectId id;
        lock (_gate) if (!_projectIds.TryGetValue(manifest, out id)) _projectIds[manifest] = id = ProjectId.CreateNew();
        var documents = await ReadSourcesAsync(id, project.SourceFiles, cancellationToken).ConfigureAwait(false);
        LanguageProjectSnapshot snapshot;
        lock (_gate)
        {
            var existing = _current.FindProject(id);
            if (existing is not null) return id;
            snapshot = new(id, project.Manifest.Package.Name, Canonical(project.RootDirectory), new(0), documents) { ManifestPath = manifest };
        }
        Publish(s => s.UpsertProject(snapshot), new(LanguageWorkspaceChangeKind.ProjectOpened, id));
        return id;
    }

    public Task CloseProjectAsync(ProjectId projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LanguageProjectSnapshot? removed = null;
        Publish(s => { removed = s.FindProject(projectId); return removed is null ? s : s with { Projects = s.Projects.RemoveAll(p => p.Id == projectId), Version = s.Version.Next() }; }, new(LanguageWorkspaceChangeKind.ProjectClosed, projectId));
        if (removed is not null) lock (_gate) foreach (var document in removed.Documents) AddTombstone(document);
        return Task.CompletedTask;
    }

    public Task<DocumentId> OpenDocumentAsync(ProjectId projectId, string filePath, SourceText text, DocumentVersion editorVersion, bool isDirty, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDocumentSize(text);
        var path = Canonical(filePath);
        DocumentId id;
        lock (_gate)
        {
            RequireProject(projectId);
            id = GetDocumentId(projectId, path);
        }
        var document = new LanguageDocumentSnapshot(id, path, text.ToString(), editorVersion) { ProjectId = projectId, IsOpen = true, IsDirty = isDirty, ExistsOnDisk = File.Exists(path) };
        PublishProjectDocument(projectId, document, LanguageWorkspaceChangeKind.DocumentOpened);
        return Task.FromResult(id);
    }

    public Task ReplaceDocumentTextAsync(DocumentId documentId, DocumentVersion previousVersion, DocumentVersion newVersion, SourceText text, bool isDirty, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDocumentSize(text);
        var old = CurrentSnapshot.FindDocument(documentId) ?? throw new KeyNotFoundException($"Unknown document '{documentId.Value}'.");
        if (old.Version != previousVersion) throw new InvalidOperationException($"Stale document version. Expected {old.Version.Value}, received {previousVersion.Value}.");
        if (newVersion.Value <= previousVersion.Value) throw new ArgumentOutOfRangeException(nameof(newVersion), "The new version must be greater than the previous version.");
        PublishProjectDocument(old.ProjectId, old with { Text = text.ToString(), Version = newVersion, IsOpen = true, IsDirty = isDirty }, LanguageWorkspaceChangeKind.DocumentChanged);
        return Task.CompletedTask;
    }

    public Task<DocumentChangeResult> ApplyDocumentChangesAsync(DocumentChange change, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DocumentChangeResult result = new(DocumentChangeStatus.Rejected, "MRTLS1001", "The document is unknown.");
        Publish(snapshot =>
        {
            var old = snapshot.FindDocument(change.DocumentId);
            if (old is null) return snapshot;
            if (old.Version != change.PreviousVersion)
            {
                result = new(DocumentChangeStatus.RequiresFullTextResynchronization, "MRTLS1000",
                    $"Stale document version. Expected {old.Version.Value}, received {change.PreviousVersion.Value}.");
                return snapshot;
            }
            result = TextChangeValidator.ValidateAndApply(old.Text, change, TextSynchronizationOptions, out var text);
            if (!result.IsApplied) return snapshot;
            var project = snapshot.FindProject(old.ProjectId)!;
            return snapshot.UpsertProject(project.UpsertDocument(old with
            {
                Text = text,
                Version = change.NewVersion,
                IsOpen = true,
                IsDirty = true
            }));
        }, new(LanguageWorkspaceChangeKind.DocumentChanged, DocumentId: change.DocumentId));
        return Task.FromResult(result);
    }

    public Task SaveDocumentAsync(DocumentId documentId, SourceText text, DocumentVersion editorVersion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var old = CurrentSnapshot.FindDocument(documentId) ?? throw new KeyNotFoundException($"Unknown document '{documentId.Value}'.");
        if (editorVersion.Value < old.Version.Value) throw new InvalidOperationException("Cannot save a stale document version.");
        PublishProjectDocument(old.ProjectId, old with { Text = text.ToString(), Version = editorVersion, IsOpen = true, IsDirty = false, ExistsOnDisk = File.Exists(old.FilePath) }, LanguageWorkspaceChangeKind.DocumentSaved);
        return Task.CompletedTask;
    }

    public Task CloseDocumentAsync(DocumentId documentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var old = CurrentSnapshot.FindDocument(documentId) ?? throw new KeyNotFoundException($"Unknown document '{documentId.Value}'.");
        var text = File.Exists(old.FilePath) ? File.ReadAllText(old.FilePath) : old.Text;
        PublishProjectDocument(old.ProjectId, old with { Text = text, IsOpen = false, IsDirty = false, ExistsOnDisk = File.Exists(old.FilePath) }, LanguageWorkspaceChangeKind.DocumentClosed);
        return Task.CompletedTask;
    }

    public async Task RefreshProjectAsync(ProjectId projectId, MartinProject project, CancellationToken cancellationToken = default)
    {
        var disk = await ReadSourcesAsync(projectId, project.SourceFiles, cancellationToken).ConfigureAwait(false);
        var old = CurrentSnapshot.FindProject(projectId) ?? throw new KeyNotFoundException($"Unknown project '{projectId.Value}'.");
        var previous = old.Documents.ToDictionary(d => d.Id);
        var merged = disk.Select(d =>
        {
            if (!previous.TryGetValue(d.Id, out var prior)) return d;
            if (prior.IsOpen) return prior with { ExistsOnDisk = true };
            return string.Equals(prior.Text, d.Text, StringComparison.Ordinal)
                ? prior with { ExistsOnDisk = true }
                : prior with { Text = d.Text, Version = prior.Version.Next(), ExistsOnDisk = true };
        }).ToImmutableArray();
        foreach (var removed in old.Documents.Where(d => merged.All(n => n.Id != d.Id))) lock (_gate) AddTombstone(removed);
        var refreshed = old with { Name = project.Manifest.Package.Name, RootDirectory = Canonical(project.RootDirectory), ManifestPath = Canonical(project.ManifestPath), Documents = Sort(merged), Version = old.Version.Next() };
        Publish(s => s.UpsertProject(refreshed), new(LanguageWorkspaceChangeKind.ProjectRefreshed, projectId));
    }

    public Task RenameDocumentAsync(DocumentId documentId, string newFilePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var old = CurrentSnapshot.FindDocument(documentId) ?? throw new KeyNotFoundException($"Unknown document '{documentId.Value}'.");
        var path = Canonical(newFilePath);
        lock (_gate) { _documentIds.Remove((old.ProjectId, old.FilePath)); _documentIds[(old.ProjectId, path)] = documentId; }
        PublishProjectDocument(old.ProjectId, old with { FilePath = path, ExistsOnDisk = File.Exists(path) }, LanguageWorkspaceChangeKind.DocumentRenamed, old.FilePath);
        return Task.CompletedTask;
    }

    async Task<ImmutableArray<LanguageDocumentSnapshot>> ReadSourcesAsync(ProjectId projectId, IEnumerable<string> sources, CancellationToken ct)
    {
        var builder = ImmutableArray.CreateBuilder<LanguageDocumentSnapshot>();
        foreach (var source in sources.Where(p => string.Equals(Path.GetExtension(p), ".martin", StringComparison.OrdinalIgnoreCase)).Select(Canonical).Distinct(PathComparer).OrderBy(p => p, PathComparer))
        {
            ct.ThrowIfCancellationRequested();
            var text = await File.ReadAllTextAsync(source, ct).ConfigureAwait(false);
            DocumentId id; lock (_gate) id = GetDocumentId(projectId, source);
            builder.Add(new(id, source, text, new(0)) { ProjectId = projectId, ExistsOnDisk = true });
        }
        // The builder grows geometrically, so its capacity is not guaranteed to
        // match the number of discovered sources. MoveToImmutable requires an
        // exact match and therefore fails for ordinary projects as well as empty
        // ones; ToImmutable safely copies only the populated elements.
        return builder.ToImmutable();
    }

    void PublishProjectDocument(ProjectId projectId, LanguageDocumentSnapshot document, LanguageWorkspaceChangeKind kind, string? oldPath = null) =>
        Publish(s => { var project = s.FindProject(projectId) ?? throw new KeyNotFoundException(); return s.UpsertProject(project.UpsertDocument(document)); }, new(kind, projectId, document.Id, oldPath, document.FilePath));

    void Publish(Func<LanguageWorkspaceSnapshot, LanguageWorkspaceSnapshot> update, LanguageWorkspaceChange change)
    {
        LanguageWorkspaceSnapshot oldSnapshot, newSnapshot;
        lock (_gate) { ThrowIfDisposed(); oldSnapshot = _current; newSnapshot = update(oldSnapshot); if (ReferenceEquals(oldSnapshot, newSnapshot)) return; _current = newSnapshot; PruneTombstones(); }
        WorkspaceChanged?.Invoke(this, new(oldSnapshot, newSnapshot, change));
    }

    ProjectId RequireProject(ProjectId id) => _current.FindProject(id)?.Id ?? throw new KeyNotFoundException($"Unknown project '{id.Value}'.");
    void EnsureDocumentSize(SourceText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (System.Text.Encoding.UTF8.GetByteCount(text.ToString()) > TextSynchronizationOptions.MaximumDocumentSizeBytes)
            throw new ArgumentException("The document exceeds the configured size limit.", nameof(text));
    }
    DocumentId GetDocumentId(ProjectId project, string path) { if (_documentIds.TryGetValue((project, path), out var id)) return id; _documentIds[(project, path)] = id = DocumentId.CreateNew(); return id; }
    void AddTombstone(LanguageDocumentSnapshot document) => _tombstones[document.Id] = new(document.ProjectId, document.FilePath, DateTimeOffset.UtcNow.AddMinutes(2));
    void PruneTombstones()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _tombstones.Where(x => x.Value.Expires <= now).ToArray())
        {
            _tombstones.Remove(pair.Key);
            _documentIds.Remove((pair.Value.ProjectId, pair.Value.Path));
        }
    }
    void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(LanguageWorkspace)); }
    public ValueTask DisposeAsync() { lock (_gate) { _disposed = true; _projectIds.Clear(); _documentIds.Clear(); _tombstones.Clear(); _current = new(_current.Id, _current.Version.Next(), []); } return ValueTask.CompletedTask; }
    static ImmutableArray<LanguageDocumentSnapshot> Sort(IEnumerable<LanguageDocumentSnapshot> documents) => documents.OrderBy(d => d.FilePath, PathComparer).ToImmutableArray();
    static string Canonical(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    sealed record Tombstone(ProjectId ProjectId, string Path, DateTimeOffset Expires);
    sealed class DocumentKeyComparer : IEqualityComparer<(ProjectId Project, string Path)>
    {
        public bool Equals((ProjectId Project, string Path) x, (ProjectId Project, string Path) y) => x.Project == y.Project && PathComparer.Equals(x.Path, y.Path);
        public int GetHashCode((ProjectId Project, string Path) obj) => HashCode.Combine(obj.Project, PathComparer.GetHashCode(obj.Path));
    }
}
