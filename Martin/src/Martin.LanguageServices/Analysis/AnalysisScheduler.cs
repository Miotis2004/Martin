using System.Collections.Concurrent;
using System.Diagnostics;

namespace Martin.LanguageServices;

public sealed record AnalysisSchedulerOptions
{
    public int MaximumConcurrency { get; init; } = 2;
    public int MaximumProjectConcurrency { get; init; } = 1;
    public int MaximumQueuedRequests { get; init; } = 256;
    public TimeSpan SyntaxDiagnosticsDebounce { get; init; } = TimeSpan.FromMilliseconds(200);
    public TimeSpan ProjectAnalysisDebounce { get; init; } = TimeSpan.FromMilliseconds(500);
}

public sealed class AnalysisSchedulerMetrics
{
    long _queued, _started, _completed, _cancelled, _stale, _coalesced, _rejected, _latencyTicks, _currentQueueDepth, _peakQueueDepth;
    public long Queued => Interlocked.Read(ref _queued);
    public long Started => Interlocked.Read(ref _started);
    public long Completed => Interlocked.Read(ref _completed);
    public long Cancelled => Interlocked.Read(ref _cancelled);
    public long Stale => Interlocked.Read(ref _stale);
    public long Coalesced => Interlocked.Read(ref _coalesced);
    public long Rejected => Interlocked.Read(ref _rejected);
    /// <summary>The number of requests waiting for a worker.</summary>
    public long CurrentQueueDepth => Interlocked.Read(ref _currentQueueDepth);
    /// <summary>The largest observed queue depth since this scheduler was created.</summary>
    public long PeakQueueDepth => Interlocked.Read(ref _peakQueueDepth);
    public TimeSpan TotalQueueLatency => TimeSpan.FromTicks(Interlocked.Read(ref _latencyTicks));
    internal void Queue()
    {
        Interlocked.Increment(ref _queued);
        var depth = Interlocked.Increment(ref _currentQueueDepth);
        long peak;
        while (depth > (peak = Interlocked.Read(ref _peakQueueDepth)) &&
               Interlocked.CompareExchange(ref _peakQueueDepth, depth, peak) != peak) { }
    }
    internal void Start(long timestamp)
    {
        Interlocked.Increment(ref _started);
        Interlocked.Decrement(ref _currentQueueDepth);
        Interlocked.Add(ref _latencyTicks, Stopwatch.GetElapsedTime(timestamp).Ticks);
    }
    internal void Complete() => Interlocked.Increment(ref _completed);
    internal void Cancel() => Interlocked.Increment(ref _cancelled);
    internal void MarkStale() => Interlocked.Increment(ref _stale);
    internal void Coalesce() => Interlocked.Increment(ref _coalesced);
    internal void Reject() => Interlocked.Increment(ref _rejected);
}

/// <summary>A bounded, priority ordered scheduler for all compiler-backed editor work.</summary>
public sealed class AnalysisScheduler : IAnalysisScheduler
{
    readonly ILanguageWorkspace _workspace;
    readonly AnalysisSchedulerOptions _options;
    readonly object _gate = new();
    readonly PriorityQueue<IWorkItem, (int Priority, long Sequence)> _queue = new();
    readonly Dictionary<WorkKey, IWorkItem> _pending = [];
    readonly ConcurrentDictionary<DocumentId, CancellationTokenSource> _documents = [];
    readonly ConcurrentDictionary<ProjectId, CancellationTokenSource> _projects = [];
    readonly ConcurrentDictionary<DocumentId, CancellationTokenSource> _diagnosticDelays = [];
    readonly ConcurrentDictionary<ProjectId, CancellationTokenSource> _projectDelays = [];
    readonly ConcurrentDictionary<Task, byte> _backgroundTasks = [];
    readonly SemaphoreSlim _available = new(0);
    readonly SemaphoreSlim _projectConcurrency;
    readonly CancellationTokenSource _shutdown = new();
    readonly Task[] _workers;
    long _sequence;
    bool _disposed;

    public AnalysisScheduler(ILanguageWorkspace workspace, AnalysisSchedulerOptions? options = null)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _options = options ?? new();
        if (_options.MaximumConcurrency <= 0 || _options.MaximumProjectConcurrency <= 0 || _options.MaximumQueuedRequests <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Scheduler bounds must be positive.");
        _projectConcurrency = new(_options.MaximumProjectConcurrency, _options.MaximumProjectConcurrency);
        _workers = Enumerable.Range(0, _options.MaximumConcurrency).Select(_ => Task.Run(WorkerAsync)).ToArray();
    }

    public AnalysisSchedulerMetrics Metrics { get; } = new();
    public event Func<DocumentId, DocumentVersion, CancellationToken, Task>? DocumentDiagnosticsRequested;
    public event Func<ProjectId, ProjectVersion, CancellationToken, Task>? ProjectAnalysisRequested;

    public Task<LanguageResult<T>> RunAsync<T>(LanguageRequest request, AnalysisPriority priority,
        Func<LanguageAnalysisContext, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(operation);
        var key = new WorkKey(request.FeatureKey, request.ProjectId, request.DocumentId, request.DocumentVersion);
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_pending.TryGetValue(key, out var duplicate) && duplicate is WorkItem<T> typed)
            { Metrics.Coalesce(); return typed.Task; }
            if (_queue.Count >= _options.MaximumQueuedRequests)
            { Metrics.Reject(); return Task.FromResult(Result<T>(request, default!, cancelled: true)); }

            // Only the newest position-sensitive interactive request for a document is useful.
            if (priority <= AnalysisPriority.Interactive)
                ReplaceToken(_documents, request.DocumentId);
            var documentToken = _documents.GetOrAdd(request.DocumentId, _ => new()).Token;
            var projectToken = _projects.GetOrAdd(request.ProjectId, _ => new()).Token;
            var item = new WorkItem<T>(request, operation, key, cancellationToken, documentToken, projectToken);
            _pending[key] = item;
            _queue.Enqueue(item, ((int)priority, _sequence++));
            Metrics.Queue(); _available.Release();
            return item.Task;
        }
    }

    public void CancelDocument(DocumentId id)
    {
        ReplaceToken(_documents, id);
        if (_diagnosticDelays.TryRemove(id, out var delay)) { delay.Cancel(); delay.Dispose(); }
    }
    public void CancelProject(ProjectId id)
    {
        ReplaceToken(_projects, id);
        if (_projectDelays.TryRemove(id, out var delay)) { delay.Cancel(); delay.Dispose(); }
    }

    public void ScheduleDocumentDiagnostics(DocumentId id, DocumentVersion version) => Debounce(
        _diagnosticDelays, id, _options.SyntaxDiagnosticsDebounce,
        ct => InvokeDocumentDiagnosticsAsync(id, version, ct));

    public void ScheduleProjectAnalysis(ProjectId id, ProjectVersion version) => Debounce(
        _projectDelays, id, _options.ProjectAnalysisDebounce,
        async ct => { await _projectConcurrency.WaitAsync(ct).ConfigureAwait(false); try { await InvokeProjectAnalysisAsync(id, version, ct).ConfigureAwait(false); } finally { _projectConcurrency.Release(); } });

    async Task WorkerAsync()
    {
        try
        {
            while (true)
            {
                await _available.WaitAsync(_shutdown.Token).ConfigureAwait(false);
                IWorkItem? item;
                lock (_gate) { if (!_queue.TryDequeue(out item, out _)) continue; }
                Metrics.Start(item.EnqueuedTimestamp);
                await item.ExecuteAsync(this, _shutdown.Token).ConfigureAwait(false);
                lock (_gate)
                    if (_pending.TryGetValue(item.Key, out var current) && ReferenceEquals(current, item))
                        _pending.Remove(item.Key);
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
    }

    LanguageAnalysisContext? Resolve(LanguageRequest request)
    {
        var snapshot = _workspace.CurrentSnapshot;
        if (snapshot.Id != request.WorkspaceId || snapshot.Version != request.WorkspaceVersion) return null;
        var project = snapshot.FindProject(request.ProjectId);
        var document = snapshot.FindDocument(request.DocumentId);
        return project is not null && document is not null && project.Version == request.ProjectVersion &&
            document.ProjectId == project.Id && document.Version == request.DocumentVersion
            ? new(snapshot, project, document) : null;
    }

    static LanguageResult<T> Result<T>(LanguageRequest r, T value, bool cancelled = false, bool stale = false) => new()
    { WorkspaceId=r.WorkspaceId, WorkspaceVersion=r.WorkspaceVersion, ProjectId=r.ProjectId, ProjectVersion=r.ProjectVersion,
      DocumentId=r.DocumentId, DocumentVersion=r.DocumentVersion, Value=value, IsCancelled=cancelled, IsStale=stale };

    void Debounce<TKey>(ConcurrentDictionary<TKey, CancellationTokenSource> map, TKey key, TimeSpan delay, Func<CancellationToken, Task> action) where TKey : notnull
    {
        ThrowIfDisposed();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        if (map.TryGetValue(key, out var old)) { old.Cancel(); old.Dispose(); }
        map[key] = cts;
        var task = ObserveDebounceAsync(map, key, cts, delay, action);
        _backgroundTasks.TryAdd(task, 0);
        _ = task.ContinueWith(t => { if (t.IsFaulted) _ = t.Exception; _backgroundTasks.TryRemove(t, out _); }, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    async Task InvokeDocumentDiagnosticsAsync(DocumentId id, DocumentVersion version, CancellationToken ct)
    {
        if (DocumentDiagnosticsRequested is not { } handlers) return;
        foreach (Func<DocumentId, DocumentVersion, CancellationToken, Task> handler in handlers.GetInvocationList())
        { ct.ThrowIfCancellationRequested(); await handler(id, version, ct).ConfigureAwait(false); }
    }

    async Task InvokeProjectAnalysisAsync(ProjectId id, ProjectVersion version, CancellationToken ct)
    {
        if (ProjectAnalysisRequested is not { } handlers) return;
        foreach (Func<ProjectId, ProjectVersion, CancellationToken, Task> handler in handlers.GetInvocationList())
        { ct.ThrowIfCancellationRequested(); await handler(id, version, ct).ConfigureAwait(false); }
    }

    static async Task ObserveDebounceAsync<TKey>(ConcurrentDictionary<TKey, CancellationTokenSource> map, TKey key, CancellationTokenSource cts, TimeSpan delay, Func<CancellationToken, Task> action) where TKey:notnull
    {
        try { await Task.Delay(delay, cts.Token).ConfigureAwait(false); await action(cts.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
        finally { if (((ICollection<KeyValuePair<TKey, CancellationTokenSource>>)map).Remove(new(key, cts))) cts.Dispose(); }
    }

    static void ReplaceToken<TKey>(ConcurrentDictionary<TKey, CancellationTokenSource> map, TKey key) where TKey:notnull
    {
        var next = new CancellationTokenSource();
        if (map.TryGetValue(key, out var old)) { map[key] = next; old.Cancel(); old.Dispose(); }
        else if (!map.TryAdd(key, next)) { old = map[key]; map[key] = next; old.Cancel(); old.Dispose(); }
    }

    void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(AnalysisScheduler)); }

    public async ValueTask DisposeAsync()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; }
        _shutdown.Cancel();
        foreach (var c in _documents.Values.Concat(_projects.Values).Concat(_diagnosticDelays.Values).Concat(_projectDelays.Values)) c.Cancel();
        await Task.WhenAll(_workers).ConfigureAwait(false);
        await Task.WhenAll(_backgroundTasks.Keys).ConfigureAwait(false);
        lock (_gate) while (_queue.TryDequeue(out var item, out _)) item.Cancel();
        foreach (var c in _documents.Values.Concat(_projects.Values).Concat(_diagnosticDelays.Values).Concat(_projectDelays.Values)) c.Dispose();
        _available.Dispose(); _projectConcurrency.Dispose(); _shutdown.Dispose();
    }

    readonly record struct WorkKey(string Feature, ProjectId Project, DocumentId Document, DocumentVersion Version);
    interface IWorkItem { WorkKey Key { get; } long EnqueuedTimestamp { get; } Task ExecuteAsync(AnalysisScheduler owner, CancellationToken shutdown); void Cancel(); }
    sealed class WorkItem<T>(LanguageRequest request, Func<LanguageAnalysisContext,CancellationToken,Task<T>> operation,
        WorkKey key, CancellationToken caller, CancellationToken document, CancellationToken project) : IWorkItem
    {
        readonly TaskCompletionSource<LanguageResult<T>> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public WorkKey Key => key; public long EnqueuedTimestamp { get; } = Stopwatch.GetTimestamp(); public Task<LanguageResult<T>> Task => _completion.Task;
        public async Task ExecuteAsync(AnalysisScheduler owner, CancellationToken shutdown)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(caller, document, project, shutdown);
            try
            {
                var context = owner.Resolve(request);
                if (context is null) { owner.Metrics.MarkStale(); _completion.TrySetResult(Result<T>(request, default!, stale:true)); return; }
                var value = await operation(context, linked.Token).ConfigureAwait(false);
                if (owner.Resolve(request) is null) { owner.Metrics.MarkStale(); _completion.TrySetResult(Result<T>(request, default!, stale:true)); }
                else { owner.Metrics.Complete(); _completion.TrySetResult(Result(request, value)); }
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested) { owner.Metrics.Cancel(); _completion.TrySetResult(Result<T>(request, default!, cancelled:true)); }
            catch (Exception ex) { _completion.TrySetException(ex); }
        }
        public void Cancel() { _completion.TrySetResult(Result<T>(request, default!, cancelled:true)); }
    }
}
