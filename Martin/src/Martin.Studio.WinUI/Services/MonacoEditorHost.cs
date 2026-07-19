using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Martin.Studio.Core;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.UI.ViewManagement;

namespace Martin.Services;

public sealed class MonacoEditorHost(WebView2 webView, IStudioLogService logService, IStudioRecoveryService recoveryService, StudioLanguageProvider languageProvider) : IEditorHost
{
    private readonly EditorBridge _bridge = new();
    private readonly Queue<QueuedCommand> _queue = new();
    private readonly Dictionary<Guid, EditorDocumentSnapshot> _documents = [];
    private readonly object _gate = new();
    private const int MaxQueuedCommands = 256;
    private static readonly TimeSpan ReadinessTimeout = TimeSpan.FromSeconds(5);
    private bool _initialized;
    private bool _bridgeEventsAttached;
    private bool _webViewMessageAttached;
    private CoreWebView2? _processFailedCore;
    private string? _source;
    private Guid? _activeDocumentId;
    private SetEditorThemePayload? _theme;
    private TaskCompletionSource _ready = CreateReadyCompletionSource();
    private readonly Dictionary<string, CancellationTokenSource> _languageRequests = [];

    public EditorBridgeState State => _bridge.State;
    public event EventHandler<EditorTextChangedPayload>? TextChanged;
    public event EventHandler<EditorViewStatePayload>? ViewStateChanged;
    public event EventHandler<EditorErrorPayload>? Error;
    public event EventHandler<Guid>? SaveRequested;
    public event EventHandler<EditorNavigationRequestedPayload>? NavigationRequested;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;
        _initialized = true;

        if (!_bridgeEventsAttached)
        {
            _bridge.Ready += (_, _) =>
            {
                SignalReady();
                _ = FlushQueuedAsync();
            };
            _bridge.TextChanged += (_, payload) =>
            {
                if (!ValidateDocument(payload.DocumentId, "textChanged"))
                    return;
                lock (_gate)
                {
                    if (_documents.TryGetValue(payload.DocumentId, out var snapshot))
                    {
                        var text = snapshot.Text;
                        foreach (var change in payload.Changes.OrderByDescending(change => change.RangeOffset))
                        {
                            text = text
                                       .Remove(change.RangeOffset, change.RangeLength)
                                       .Insert(change.RangeOffset, change.Text);
                        }

                        snapshot.Text = text;
                        snapshot.Version = payload.NewVersion;
                    }
                }
                TextChanged?.Invoke(this, payload);
            };
            _bridge.ViewStateChanged += (_, payload) =>
            {
                if (!ValidateDocument(payload.DocumentId, "viewStateChanged"))
                    return;
                lock (_gate) if (_documents.TryGetValue(payload.DocumentId, out var snapshot)) snapshot.ViewState = payload;
                ViewStateChanged?.Invoke(this, payload);
            };
            _bridge.Error += (_, payload) => Error?.Invoke(this, payload);
            _bridgeEventsAttached = true;
        }

        await webView.EnsureCoreWebView2Async();
        webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        if (!_webViewMessageAttached)
        {
            webView.WebMessageReceived += WebView_WebMessageReceived;
            _webViewMessageAttached = true;
        }
        if (!ReferenceEquals(_processFailedCore, webView.CoreWebView2))
        {
            if (_processFailedCore is not null)
                _processFailedCore.ProcessFailed -= CoreWebView2_ProcessFailed;
            webView.CoreWebView2.ProcessFailed += CoreWebView2_ProcessFailed;
            _processFailedCore = webView.CoreWebView2;
        }
        _source ??= System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Editor", "index.html");
        webView.Source = new Uri(_source);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public Task OpenDocumentAsync(DocumentModel document, CancellationToken cancellationToken = default)
    {
        var viewState = new EditorViewStatePayload(document.Id, document.ViewState.CursorLine, document.ViewState.CursorColumn, document.ViewState.ScrollTop, document.ViewState.ScrollLeft);
        lock (_gate)
            _documents[document.Id] = new EditorDocumentSnapshot(document.Id, document.FilePath, document.Text, (int)document.Version.Value, document.IsReadOnly) { ViewState = viewState };
        return SendAsync("openDocument", new OpenEditorDocumentPayload(document.Id, document.FilePath, document.Text, "martin", (int)document.Version.Value, viewState), cancellationToken, QueueKind.Document, document.Id);
    }

    public Task ActivateDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        lock (_gate) _activeDocumentId = documentId;
        return SendKnownDocumentAsync(documentId, "activateDocument", new ActivateEditorDocumentPayload(documentId), cancellationToken);
    }

    public Task CloseDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _documents.Remove(documentId);
            if (_activeDocumentId == documentId)
                _activeDocumentId = null;
        }
        return SendAsync("closeDocument", new CloseEditorDocumentPayload(documentId), cancellationToken, QueueKind.Document, documentId);
    }

    public async Task<string> GetDocumentTextAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        EnsureKnownDocument(documentId, "requestText");
        var response = await RequestAsync("requestText", new EditorRequestTextPayload(documentId), cancellationToken);
        var payload = response.Deserialize<EditorTextResponsePayload>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return payload?.DocumentId == documentId ? payload.Text : throw new InvalidOperationException("Editor returned text for the wrong document.");
    }

    public Task SetDiagnosticsAsync(Guid documentId, IReadOnlyList<EditorDiagnostic> diagnostics, CancellationToken cancellationToken = default)
    {
        EnsureKnownDocument(documentId, "setMarkers");
        var markers = diagnostics.Count == 0 ? Array.Empty<EditorMarkerPayload>() : diagnostics.Select(d => new EditorMarkerPayload(d.Range.StartLine, d.Range.StartColumn, d.Range.EndLine, d.Range.EndColumn, d.Severity.ToString(), d.Message, d.Code)).ToArray();
        lock (_gate) if (_documents.TryGetValue(documentId, out var snapshot)) snapshot.Markers = markers;
        return SendAsync("setMarkers", new EditorSetMarkersPayload(documentId, markers), cancellationToken, QueueKind.Diagnostics, documentId);
    }

    public Task RevealRangeAsync(Guid documentId, TextRange range, bool select, CancellationToken cancellationToken = default) =>
        SendKnownDocumentAsync(documentId, "revealRange", new { documentId, range.StartLine, range.StartColumn, range.EndLine, range.EndColumn, select }, cancellationToken);

    public Task ExecuteCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (!AllowedCommands.Contains(command))
            throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported editor command.");
        return SendAsync("executeCommand", new { command }, cancellationToken);
    }

    public Task SetThemeAsync(StudioTheme theme, CancellationToken cancellationToken = default)
    {
        var highContrast = IsHighContrastEnabled();
        var effectiveTheme = theme == StudioTheme.Light ? "Light" : theme == StudioTheme.Dark ? "Dark"
                                                                                              : (IsSystemLightTheme() ? "Light" : "Dark");
        var payload = new SetEditorThemePayload(theme, highContrast, effectiveTheme);
        lock (_gate) _theme = payload;
        return SendAsync("setTheme", payload, cancellationToken, QueueKind.Theme);
    }

    private static bool IsHighContrastEnabled()
    {
        try
        {
            return new AccessibilitySettings().HighContrast;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static bool IsSystemLightTheme()
    {
        var foreground = new UISettings().GetColorValue(UIColorType.Foreground);
        return foreground.R + foreground.G + foreground.B < 384;
    }
    public Task ReplaceTextAsync(DocumentModel document, CancellationToken cancellationToken = default)
    {
        lock (_gate) if (_documents.TryGetValue(document.Id, out var snapshot))
        {
            snapshot.Text = document.Text;
            snapshot.Version = (int)document.Version.Value;
        }
        return SendKnownDocumentAsync(document.Id, "replaceText", new { documentId = document.Id, text = document.Text }, cancellationToken);
    }
    public Task SetReadOnlyAsync(Guid documentId, bool isReadOnly, CancellationToken cancellationToken = default)
    {
        lock (_gate) if (_documents.TryGetValue(documentId, out var snapshot)) snapshot.IsReadOnly = isReadOnly;
        return SendKnownDocumentAsync(documentId, "setReadOnly", new { documentId, isReadOnly }, cancellationToken);
    }
    public Task ClearMarkersAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        lock (_gate) if (_documents.TryGetValue(documentId, out var snapshot)) snapshot.Markers = [];
        return SendKnownDocumentAsync(documentId, "clearMarkers", new { documentId }, cancellationToken);
    }
    public Task FocusEditorAsync(CancellationToken cancellationToken = default) => SendAsync("focusEditor", new {}, cancellationToken);

    private async Task<JsonElement> RequestAsync(string type, object payload, CancellationToken cancellationToken)
    {
        await WaitUntilReadyAsync(cancellationToken);
        var (_, json, response) = _bridge.CreateRequest(type, payload, cancellationToken: cancellationToken);
        var coreWebView = webView.CoreWebView2 ?? throw new InvalidOperationException("Editor WebView is unavailable.");
        coreWebView.PostWebMessageAsJson(json);
        return await response;
    }

    private Task SendKnownDocumentAsync(Guid id, string type, object payload, CancellationToken token)
    {
        EnsureKnownDocument(id, type);
        return SendAsync(type, payload, token, QueueKind.Document, id);
    }

    private Task SendAsync(string type, object payload, CancellationToken token, QueueKind kind = QueueKind.Normal, Guid? documentId = null)
    {
        token.ThrowIfCancellationRequested();
        var json = _bridge.CreateCommand(type, payload);
        if (_bridge.State == EditorBridgeState.Ready && webView.CoreWebView2 is not null)
        {
            webView.CoreWebView2.PostWebMessageAsJson(json);
            return Task.CompletedTask;
        }
        lock (_gate)
        {
            if (kind == QueueKind.Theme)
                RemoveQueued(q => q.Kind == QueueKind.Theme);
            if (kind == QueueKind.Diagnostics && documentId is Guid id)
                RemoveQueued(q => q.Kind == QueueKind.Diagnostics && q.DocumentId == id);
            if (_queue.Count >= MaxQueuedCommands)
                throw new InvalidOperationException("Editor command queue is full.");
            _queue.Enqueue(new QueuedCommand(json, kind, documentId));
        }
        return Task.CompletedTask;
    }

    private async Task FlushQueuedAsync()
    {
        await Task.Yield();
        while (true)
        {
            QueuedCommand ? command;
            lock (_gate) command = _queue.Count == 0 ? null : _queue.Dequeue();
            if (command is null || webView.CoreWebView2 is null || _bridge.State != EditorBridgeState.Ready)
                return;
            webView.CoreWebView2.PostWebMessageAsJson(command.Json);
        }
    }

    private Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        if (_bridge.State == EditorBridgeState.Ready)
            return Task.CompletedTask;
        Task readyTask;
        lock (_gate) readyTask = _ready.Task;
        return WaitForReadinessAsync(readyTask, cancellationToken);
    }

    private static async Task WaitForReadinessAsync(Task readyTask, CancellationToken cancellationToken)
    {
        try
        {
            await readyTask.WaitAsync(ReadinessTimeout, cancellationToken);
        }
        catch (TimeoutException ex)
        {
            throw new EditorBridgeRequestTimeoutException("Editor did not become ready before the readiness timeout expired.") { Source = ex.Source };
        }
    }

    private void SignalReady()
    {
        TaskCompletionSource ready;
        lock (_gate) ready = _ready;
        ready.TrySetResult();
    }

    private void ResetReadyTask()
    {
        lock (_gate)
        {
            if (_ready.Task.IsCompleted)
                _ready = CreateReadyCompletionSource();
        }
    }

    private static TaskCompletionSource CreateReadyCompletionSource() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void WebView_WebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        var envelope = EditorMessageProtocol.Deserialize(args.WebMessageAsJson);
        if (envelope?.Type == "language/cancelRequest")
        {
            var requestId = envelope.Payload.TryGetProperty("requestId", out var request) ? request.GetString() : envelope.Id;
            lock (_gate) if (requestId is not null && _languageRequests.Remove(requestId, out var cancellation))
            {
                cancellation.Cancel();
                cancellation.Dispose();
            }
            return;
        }
        if (envelope?.Type.StartsWith("language/request", StringComparison.Ordinal) == true)
        {
            _ = RouteLanguageRequestAsync(envelope);
            return;
        }
        var result = _bridge.AcceptHostMessage(args.WebMessageAsJson);
        if (!result.IsValid)
        {
            logService.Log(StudioLogCategory.EditorBridge, OutputSeverity.Warning, "MRT5203", "Invalid editor message.", result.Error);
            return;
        }
        if (result.Envelope?.Type == "saveRequested")
            SaveRequested?.Invoke(this, result.Envelope.Payload.TryGetProperty("documentId", out var id) && id.TryGetGuid(out var guid) ? guid : Guid.Empty);
        if (result.Envelope?.Type == "navigationRequested")
        {
            var payload = result.Envelope.Payload.Deserialize<EditorNavigationRequestedPayload>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (payload is not null)
                NavigationRequested?.Invoke(this, payload);
        }
    }

    private async Task RouteLanguageRequestAsync(EditorMessageProtocol.EditorEnvelope<JsonElement> envelope)
    {
        var requestId = envelope.Id ?? (envelope.Payload.TryGetProperty("requestId", out var id) ? id.GetString() : null);
        if (string.IsNullOrWhiteSpace(requestId))
            return;
        var cancellation = new CancellationTokenSource();
        lock (_gate) _languageRequests[requestId] = cancellation;
        object response;
        try
        {
            response = await languageProvider.HandleMonacoRequestAsync(envelope.Type, envelope.Payload, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            response = languageProvider.CreateMonacoStatusResponse(envelope.Payload, true);
        }
        catch (Exception ex)
        {
            logService.Log(StudioLogCategory.EditorBridge, OutputSeverity.Warning, "MonacoLanguageRequest", "A Monaco language request failed.", ex.Message);
            response = languageProvider.CreateMonacoStatusResponse(envelope.Payload, false, "Language request failed.");
        }
        finally
        {
            lock (_gate) if (_languageRequests.Remove(requestId, out var source)) source.Dispose();
        }
        webView.CoreWebView2?.PostWebMessageAsJson(EditorMessageProtocol.Serialize(envelope.Type + "Response", response, requestId));
    }

    private void CoreWebView2_ProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs args)
    {
        ResetReadyTask();
        _bridge.Recover();
        recoveryService.Recover(StudioRecoveryKind.WebView2ProcessFailure, "MonacoEditorHost.ProcessFailed");
        lock (_gate)
        {
            _initialized = false;
            _queue.Clear();
            EnqueueRecoveryCommands();
        }
        _ = InitializeAsync();
    }

    private void EnqueueRecoveryCommands()
    {
        if (_theme is not null)
            _queue.Enqueue(new QueuedCommand(_bridge.CreateCommand("setTheme", _theme), QueueKind.Theme, null));
        foreach (var snapshot in _documents.Values)
        {
            _queue.Enqueue(new QueuedCommand(_bridge.CreateCommand("openDocument", new OpenEditorDocumentPayload(snapshot.DocumentId, snapshot.Path, snapshot.Text, "martin", snapshot.Version, snapshot.ViewState)), QueueKind.Document, snapshot.DocumentId));
            _queue.Enqueue(new QueuedCommand(_bridge.CreateCommand("setReadOnly", new { documentId = snapshot.DocumentId, isReadOnly = snapshot.IsReadOnly }), QueueKind.Document, snapshot.DocumentId));
            if (snapshot.Markers.Count > 0)
                _queue.Enqueue(new QueuedCommand(_bridge.CreateCommand("setMarkers", new EditorSetMarkersPayload(snapshot.DocumentId, snapshot.Markers)), QueueKind.Diagnostics, snapshot.DocumentId));
        }
        if (_activeDocumentId is Guid activeDocumentId)
            _queue.Enqueue(new QueuedCommand(_bridge.CreateCommand("activateDocument", new ActivateEditorDocumentPayload(activeDocumentId)), QueueKind.Document, activeDocumentId));
    }

    private bool ValidateDocument(Guid documentId, string type)
    {
        lock (_gate) if (_documents.ContainsKey(documentId)) return true;
        logService.Log(StudioLogCategory.EditorBridge, OutputSeverity.Warning, "MRT5203", $"Rejected {type} for unopened document.", documentId.ToString());
        return false;
    }
    private void EnsureKnownDocument(Guid documentId, string type)
    {
        lock (_gate) if (_documents.ContainsKey(documentId)) return;
        throw new InvalidOperationException($"Cannot send {type} for unopened document {documentId}.");
    }
    private void RemoveQueued(Predicate<QueuedCommand> match)
    {
        var keep = _queue.Where(q => !match(q)).ToArray();
        _queue.Clear();
        foreach (var q in keep)
            _queue.Enqueue(q);
    }

    private enum QueueKind
    {
        Normal,
        Theme,
        Diagnostics,
        Document
    }
    private sealed record QueuedCommand(string Json, QueueKind Kind, Guid? DocumentId);
    private sealed class EditorDocumentSnapshot(Guid documentId, string path, string text, int version, bool isReadOnly)
    {
        public Guid DocumentId { get; } = documentId;
        public string Path { get; } = path;
        public int Version { get; set; } = version;
        public string Text { get; set; } = text;
        public bool IsReadOnly { get; set; } = isReadOnly;
        public IReadOnlyList<EditorMarkerPayload> Markers { get; set; } = [];
        public EditorViewStatePayload? ViewState { get; set; }
    }
    private static readonly HashSet<string> AllowedCommands = new(StringComparer.OrdinalIgnoreCase) { "undo", "redo", "cut", "copy", "paste", "find", "replace", "selectAll" };
}

public sealed record EditorNavigationRequestedPayload(string FilePath, int StartLine, int StartColumn, int EndLine, int EndColumn);
