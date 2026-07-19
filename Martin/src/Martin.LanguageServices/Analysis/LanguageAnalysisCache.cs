using System.Collections.Concurrent;

namespace Martin.LanguageServices;

public readonly record struct DocumentAnalysisKey(DocumentId DocumentId, DocumentVersion DocumentVersion);
public readonly record struct ProjectAnalysisKey(ProjectId ProjectId, ProjectVersion ProjectVersion);

public sealed record AnalysisCacheOptions
{
    public int MaximumDocumentVersions { get; init; } = 128;
    public int MaximumProjectVersions { get; init; } = 8;
    public long ApproximateMemoryLimitBytes { get; init; } = 256L * 1024L * 1024L;
}

public readonly record struct AnalysisCacheMetrics(
    long Hits,
    long Misses,
    long Evictions,
    int DocumentEntries,
    int ProjectEntries,
    long ApproximateMemoryBytes)
{
    public double HitRate => Hits + Misses == 0 ? 0 : (double)Hits / (Hits + Misses);
}

public interface IAnalysisLease<out T> : IDisposable
{
    T Value { get; }
}

/// <summary>
/// A thread-safe, version-aware cache. Values are populated once per key and leased values
/// cannot be evicted. Current versions are retained even when a configured limit is exceeded.
/// </summary>
public sealed class LanguageAnalysisCache : IDisposable
{
    private readonly AnalysisCacheOptions _options;
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<DocumentAnalysisKey, Lazy<Task<Entry<DocumentAnalysisKey, DocumentAnalysis>>>> _documents = [];
    private readonly ConcurrentDictionary<ProjectAnalysisKey, Lazy<Task<Entry<ProjectAnalysisKey, ProjectAnalysis>>>> _projects = [];
    private readonly ConcurrentDictionary<DocumentAnalysisKey, CancellationTokenSource> _documentPopulations = [];
    private readonly ConcurrentDictionary<ProjectAnalysisKey, CancellationTokenSource> _projectPopulations = [];
    private readonly LinkedList<DocumentAnalysisKey> _documentLru = [];
    private readonly LinkedList<ProjectAnalysisKey> _projectLru = [];
    private readonly Dictionary<DocumentAnalysisKey, LinkedListNode<DocumentAnalysisKey>> _documentNodes = [];
    private readonly Dictionary<ProjectAnalysisKey, LinkedListNode<ProjectAnalysisKey>> _projectNodes = [];
    private readonly Dictionary<DocumentId, ProjectId> _documentProjects = [];
    private readonly Dictionary<DocumentId, DocumentVersion> _currentDocuments = [];
    private readonly Dictionary<ProjectId, ProjectVersion> _currentProjects = [];
    private long _hits, _misses, _evictions, _memory;
    private bool _disposed;

    public LanguageAnalysisCache(AnalysisCacheOptions? options = null)
    {
        _options = options ?? new();
        if (_options.MaximumDocumentVersions <= 0) throw new ArgumentOutOfRangeException(nameof(options), "Document cache capacity must be positive.");
        if (_options.MaximumProjectVersions <= 0) throw new ArgumentOutOfRangeException(nameof(options), "Project cache capacity must be positive.");
        if (_options.ApproximateMemoryLimitBytes <= 0) throw new ArgumentOutOfRangeException(nameof(options), "Memory limit must be positive.");
    }

    public AnalysisCacheMetrics Metrics
    {
        get { lock (_gate) return new(_hits, _misses, _evictions, _documents.Count, _projects.Count, _memory); }
    }

    public void RetainCurrent(LanguageWorkspaceSnapshot workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        lock (_gate)
        {
            ThrowIfDisposed();
            var projectVersions = workspace.Projects.ToDictionary(p => p.Id, p => p.Version);
            var documentVersions = workspace.Projects.SelectMany(p => p.Documents).ToDictionary(d => d.Id, d => d.Version);
            foreach (var key in _projects.Keys.Where(k => !projectVersions.TryGetValue(k.ProjectId, out var version) || version != k.ProjectVersion).ToArray())
                RemoveProject(key);
            foreach (var key in _documents.Keys.Where(k => !documentVersions.TryGetValue(k.DocumentId, out var version) || version != k.DocumentVersion).ToArray())
                RemoveDocument(key);
            _currentDocuments.Clear();
            _currentProjects.Clear();
            foreach (var project in workspace.Projects)
            {
                _currentProjects[project.Id] = project.Version;
                foreach (var document in project.Documents)
                {
                    _currentDocuments[document.Id] = document.Version;
                    _documentProjects[document.Id] = project.Id;
                }
            }
            EvictUnderLock();
        }
    }

    public void RetainCurrent(LanguageProjectSnapshot project)
    {
        ArgumentNullException.ThrowIfNull(project);
        lock (_gate)
        {
            ThrowIfDisposed();
            foreach (var key in _projects.Keys.Where(k => k.ProjectId == project.Id && k.ProjectVersion != project.Version).ToArray())
                RemoveProject(key);
            _currentProjects[project.Id] = project.Version;
            foreach (var document in project.Documents)
            {
                foreach (var key in _documents.Keys.Where(k => k.DocumentId == document.Id && k.DocumentVersion != document.Version).ToArray())
                    RemoveDocument(key);
                _currentDocuments[document.Id] = document.Version;
                _documentProjects[document.Id] = project.Id;
            }
            EvictUnderLock();
        }
    }

    public Task<IAnalysisLease<DocumentAnalysis>> GetOrCreateDocumentAsync(
        DocumentAnalysisKey key,
        Func<CancellationToken, Task<DocumentAnalysis>> factory,
        long approximateSizeBytes = 0,
        CancellationToken cancellationToken = default) =>
        GetOrCreateAsync(key, factory, Math.Max(0, approximateSizeBytes), _documents, true, cancellationToken);

    public Task<IAnalysisLease<ProjectAnalysis>> GetOrCreateProjectAsync(
        ProjectAnalysisKey key,
        Func<CancellationToken, Task<ProjectAnalysis>> factory,
        long approximateSizeBytes = 0,
        CancellationToken cancellationToken = default) =>
        GetOrCreateAsync(key, factory, Math.Max(0, approximateSizeBytes), _projects, false, cancellationToken);

    // Compatibility helpers for callers which do not need a lease. New code should use GetOrCreateDocumentAsync.
    public bool TryGet(DocumentId id, DocumentVersion version, out DocumentAnalysis analysis)
    {
        var key = new DocumentAnalysisKey(id, version);
        if (_documents.TryGetValue(key, out var lazy) && lazy.IsValueCreated && lazy.Value.IsCompletedSuccessfully)
        {
            analysis = lazy.Value.Result.Value;
            lock (_gate) { _hits++; TouchDocument(key); }
            return true;
        }
        analysis = null!;
        lock (_gate) _misses++;
        return false;
    }

    public void Store(DocumentAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        var key = new DocumentAnalysisKey(analysis.DocumentId, analysis.Version);
        var entry = new Entry<DocumentAnalysisKey, DocumentAnalysis>(key, analysis, Estimate(analysis)) { Registered = true };
        if (_documents.TryAdd(key, new(() => Task.FromResult(entry), LazyThreadSafetyMode.ExecutionAndPublication)))
            Added(entry, true);
    }

    public void ClearProject(ProjectId projectId)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _currentProjects.Remove(projectId);
            foreach (var key in _projects.Keys.Where(k => k.ProjectId == projectId).ToArray()) RemoveProject(key);
            var ids = _documentProjects.Where(p => p.Value == projectId).Select(p => p.Key).ToArray();
            foreach (var id in ids)
            {
                _currentDocuments.Remove(id);
                _documentProjects.Remove(id);
                foreach (var key in _documents.Keys.Where(k => k.DocumentId == id).ToArray()) RemoveDocument(key);
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            CancelAll(_documentPopulations);
            CancelAll(_projectPopulations);
            _documents.Clear(); _projects.Clear();
            _documentLru.Clear(); _projectLru.Clear();
            _documentNodes.Clear(); _projectNodes.Clear();
            _documentProjects.Clear(); _currentDocuments.Clear(); _currentProjects.Clear();
            _memory = 0;
        }
    }

    private async Task<IAnalysisLease<TValue>> GetOrCreateAsync<TKey, TValue>(
        TKey key, Func<CancellationToken, Task<TValue>> factory, long size,
        ConcurrentDictionary<TKey, Lazy<Task<Entry<TKey, TValue>>>> cache, bool document,
        CancellationToken cancellationToken) where TKey : notnull where TValue : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        ThrowIfDisposed();
        var populationCancellation = new CancellationTokenSource();
        var candidate = new Lazy<Task<Entry<TKey, TValue>>>(async () =>
        {
            // Shared population must not be cancelled by one waiter.
            var value = await factory(populationCancellation.Token).ConfigureAwait(false);
            return new(key, value, size == 0 ? Estimate(value) : size);
        }, LazyThreadSafetyMode.ExecutionAndPublication);
        var lazy = cache.GetOrAdd(key, candidate);
        if (ReferenceEquals(lazy, candidate))
            AddPopulationCancellation(key, populationCancellation, document);
        else
            populationCancellation.Dispose();
        lock (_gate) { if (ReferenceEquals(lazy, candidate)) _misses++; else _hits++; }
        Entry<TKey, TValue> entry;
        try { entry = await lazy.Value.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch
        {
            if (lazy.IsValueCreated && (lazy.Value.IsFaulted || lazy.Value.IsCanceled))
            {
                cache.TryRemove(key, out _);
                CancelPopulation(key, document);
            }
            throw;
        }
        lock (_gate)
        {
            ThrowIfDisposed();
            entry.Leases++;
            if (!entry.Registered)
            {
                entry.Registered = true;
                AddedDynamic(entry, document);
            }
            TouchDynamic(key, document);
        }
        return new Lease<TValue>(entry.Value, () => Release(entry));
    }

    private void AddedDynamic<TKey, TValue>(Entry<TKey, TValue> entry, bool document) where TKey : notnull where TValue : class
    {
        if (document) Added((Entry<DocumentAnalysisKey, DocumentAnalysis>)(object)entry, true);
        else Added((Entry<ProjectAnalysisKey, ProjectAnalysis>)(object)entry, false);
    }

    private void Added<TKey, TValue>(Entry<TKey, TValue> entry, bool document) where TKey : notnull where TValue : class
    {
        lock (_gate)
        {
            _memory += entry.Size;
            if (document)
            {
                var key = (DocumentAnalysisKey)(object)entry.Key;
                _documentNodes[key] = _documentLru.AddFirst(key);
            }
            else
            {
                var key = (ProjectAnalysisKey)(object)entry.Key;
                _projectNodes[key] = _projectLru.AddFirst(key);
            }
            EvictUnderLock();
        }
    }

    private void Release<TKey, TValue>(Entry<TKey, TValue> entry) where TKey : notnull where TValue : class
    {
        lock (_gate)
        {
            entry.Leases--;
            if (entry.Leases == 0 && entry.RemoveWhenReleased)
            {
                if (entry.Key is DocumentAnalysisKey documentKey) RemoveDocument(documentKey);
                else if (entry.Key is ProjectAnalysisKey projectKey) RemoveProject(projectKey);
            }
            EvictUnderLock();
        }
    }

    private void TouchDynamic<TKey>(TKey key, bool document) where TKey : notnull
    {
        if (document) TouchDocument((DocumentAnalysisKey)(object)key);
        else TouchProject((ProjectAnalysisKey)(object)key);
    }
    private void TouchDocument(DocumentAnalysisKey key) { if (_documentNodes.Remove(key, out var n)) { _documentLru.Remove(n); _documentNodes[key] = _documentLru.AddFirst(key); } }
    private void TouchProject(ProjectAnalysisKey key) { if (_projectNodes.Remove(key, out var n)) { _projectLru.Remove(n); _projectNodes[key] = _projectLru.AddFirst(key); } }

    private void EvictUnderLock()
    {
        while (_documents.Count > _options.MaximumDocumentVersions || _projects.Count > _options.MaximumProjectVersions || _memory > _options.ApproximateMemoryLimitBytes)
        {
            var removed = false;
            if (_documents.Count > _options.MaximumDocumentVersions || (_memory > _options.ApproximateMemoryLimitBytes && _documents.Count > 0))
                for (var node = _documentLru.Last; node is not null; node = node.Previous)
                    if (!IsCurrent(node.Value) && LeaseCount(_documents, node.Value) == 0) { RemoveDocument(node.Value); removed = true; break; }
            if (!removed && (_projects.Count > _options.MaximumProjectVersions || _memory > _options.ApproximateMemoryLimitBytes))
                for (var node = _projectLru.Last; node is not null; node = node.Previous)
                    if (!IsCurrent(node.Value) && LeaseCount(_projects, node.Value) == 0) { RemoveProject(node.Value); removed = true; break; }
            if (!removed) break;
        }
    }

    private bool IsCurrent(DocumentAnalysisKey key) => _currentDocuments.TryGetValue(key.DocumentId, out var v) && v == key.DocumentVersion;
    private bool IsCurrent(ProjectAnalysisKey key) => _currentProjects.TryGetValue(key.ProjectId, out var v) && v == key.ProjectVersion;
    private static int LeaseCount<TKey, TValue>(ConcurrentDictionary<TKey, Lazy<Task<Entry<TKey, TValue>>>> cache, TKey key) where TKey : notnull where TValue : class =>
        cache.TryGetValue(key, out var l) && l.IsValueCreated && l.Value.IsCompletedSuccessfully ? l.Value.Result.Leases : 0;
    private void RemoveDocument(DocumentAnalysisKey key)
    {
        if (!_documents.TryGetValue(key, out var l)) return;
        if (l.IsValueCreated && l.Value.IsCompletedSuccessfully && l.Value.Result.Leases > 0) { l.Value.Result.RemoveWhenReleased = true; return; }
        if (!_documents.TryRemove(key, out l)) return;
        CancelPopulation(key, true);
        RemoveNode(key); if (l.IsValueCreated && l.Value.IsCompletedSuccessfully) _memory -= l.Value.Result.Size; _evictions++;
    }
    private void RemoveProject(ProjectAnalysisKey key)
    {
        if (!_projects.TryGetValue(key, out var l)) return;
        if (l.IsValueCreated && l.Value.IsCompletedSuccessfully && l.Value.Result.Leases > 0) { l.Value.Result.RemoveWhenReleased = true; return; }
        if (!_projects.TryRemove(key, out l)) return;
        CancelPopulation(key, false);
        RemoveNode(key); if (l.IsValueCreated && l.Value.IsCompletedSuccessfully) _memory -= l.Value.Result.Size; _evictions++;
    }
    private void RemoveNode(DocumentAnalysisKey key) { if (_documentNodes.Remove(key, out var n)) _documentLru.Remove(n); }
    private void RemoveNode(ProjectAnalysisKey key) { if (_projectNodes.Remove(key, out var n)) _projectLru.Remove(n); }
    private static long Estimate<T>(T value) => value switch { DocumentAnalysis d => Math.Max(1, d.SyntaxTree.Text.Length * 2L), ProjectAnalysis p => Math.Max(1, p.Compilation.SyntaxTrees.Sum(t => t.Text.Length) * 2L), _ => 1 };
    private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(LanguageAnalysisCache)); }

    private void AddPopulationCancellation<TKey>(TKey key, CancellationTokenSource source, bool document) where TKey : notnull
    {
        if (document) _documentPopulations[(DocumentAnalysisKey)(object)key] = source;
        else _projectPopulations[(ProjectAnalysisKey)(object)key] = source;
    }

    private void CancelPopulation<TKey>(TKey key, bool document) where TKey : notnull
    {
        CancellationTokenSource? source;
        var removed = document
            ? _documentPopulations.TryRemove((DocumentAnalysisKey)(object)key, out source)
            : _projectPopulations.TryRemove((ProjectAnalysisKey)(object)key, out source);
        if (!removed) return;
        source!.Cancel();
        source.Dispose();
    }

    private static void CancelAll<TKey>(ConcurrentDictionary<TKey, CancellationTokenSource> populations) where TKey : notnull
    {
        foreach (var pair in populations.ToArray())
            if (populations.TryRemove(pair.Key, out var source)) { source.Cancel(); source.Dispose(); }
    }

    private sealed class Entry<TKey, TValue>(TKey key, TValue value, long size) where TKey : notnull where TValue : class
    { public TKey Key { get; } = key; public TValue Value { get; } = value; public long Size { get; } = size; public int Leases; public bool Registered; public bool RemoveWhenReleased; }
    private sealed class Lease<T>(T value, Action release) : IAnalysisLease<T>
    { private Action? _release = release; public T Value { get; } = value; public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke(); }
}
