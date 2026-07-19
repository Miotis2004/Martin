using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Martin.Studio.Core;

public static class EditorMessageProtocol
{
    public const int MaxMessageBytes = 1024 * 1024;
    public const int MaxTextLength = 512 * 1024;
    public const int MaxMarkerCount = 10000;
    public const int MaxMarkerMessageLength = 4096;
    public const int MaxVersion = 1_000_000_000;

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize<T>(string type, T payload, string? id = null)
    {
        ValidateOutgoingPayload(type, payload);
        var envelope = new EditorEnvelope<T>(ValidateType(type), id, payload);
        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxMessageBytes)
            throw new InvalidOperationException($"Editor message '{type}' exceeds the maximum size of {MaxMessageBytes} bytes.");
        return json;
    }

    public static EditorEnvelope<JsonElement>? Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || System.Text.Encoding.UTF8.GetByteCount(json) > MaxMessageBytes)
            return null;
        try
        {
            var envelope = JsonSerializer.Deserialize<EditorEnvelope<JsonElement>>(json, JsonOptions);
            return envelope is null || string.IsNullOrWhiteSpace(envelope.Type) ? null : envelope;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static EditorMessageValidationResult TryDeserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return EditorMessageValidationResult.Invalid("Editor message is empty.");
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxMessageBytes)
            return EditorMessageValidationResult.Invalid($"Editor message exceeds the maximum size of {MaxMessageBytes} bytes.");

        try
        {
            var envelope = JsonSerializer.Deserialize<EditorEnvelope<JsonElement>>(json, JsonOptions);
            if (envelope is null || string.IsNullOrWhiteSpace(envelope.Type))
                return EditorMessageValidationResult.Invalid("Editor message type is required.");
            if (envelope.Type.Length > 128)
                return EditorMessageValidationResult.Invalid("Editor message type is too long.");
            if (!AllowedHostMessageTypes.Contains(envelope.Type))
                return EditorMessageValidationResult.Invalid($"Unsupported editor message type '{envelope.Type}'.");
            if (!string.IsNullOrEmpty(envelope.Id) && envelope.Id.Length > 64)
                return EditorMessageValidationResult.Invalid("Editor request id is too long.");
            var payloadError = ValidateIncomingPayload(envelope.Type, envelope.Id, envelope.Payload);
            if (payloadError is not null)
                return EditorMessageValidationResult.Invalid(payloadError);
            return EditorMessageValidationResult.Valid(envelope);
        }
        catch (JsonException ex)
        {
            return EditorMessageValidationResult.Invalid($"Editor message is not valid JSON: {ex.Message}");
        }
    }

    static readonly HashSet<string> AllowedHostMessageTypes = new(StringComparer.Ordinal)
    {
        "editorReady", "textChanged", "viewStateChanged", "cursorChanged", "selectionChanged",
        "saveRequested", "navigationRequested", "response", "editorError", "requestCompleted"
    };

    static string ValidateType(string type) => string.IsNullOrWhiteSpace(type) ? throw new ArgumentException("Editor message type is required.", nameof(type)) : type;

    static void ValidateOutgoingPayload<T>(string type, T payload)
    {
        switch (payload)
        {
            case OpenEditorDocumentPayload p:
                ValidateDocumentId(p.DocumentId, type); ValidateText(p.Text, type); ValidateVersion(p.Version, type); break;
            case EditorTextResponsePayload p:
                ValidateDocumentId(p.DocumentId, type); ValidateText(p.Text, type); ValidateVersion(p.Version, type); break;
            case EditorTextChangedPayload p:
                ValidateDocumentId(p.DocumentId, type); ValidateVersion(p.PreviousVersion, type); ValidateVersion(p.NewVersion, type); ValidateChanges(p.Changes, type); break;
            case EditorRequestTextPayload p:
                ValidateDocumentId(p.DocumentId, type); break;
            case ActivateEditorDocumentPayload p:
                ValidateDocumentId(p.DocumentId, type); break;
            case CloseEditorDocumentPayload p:
                ValidateDocumentId(p.DocumentId, type); break;
            case RevealEditorRangePayload p:
                ValidateDocumentId(p.DocumentId, type); ValidateRange(p.StartLine, p.StartColumn, p.EndLine, p.EndColumn, type); break;
            case EditorViewStatePayload p:
                ValidateDocumentId(p.DocumentId, type); ValidateVersionlessPosition(p.CursorLine, p.CursorColumn, type); break;
            case EditorSetMarkersPayload p:
                ValidateDocumentId(p.DocumentId, type); ValidateMarkers(p.Markers, type); break;
            case SetEditorThemePayload p:
                ValidateTheme(p.Theme, p.EffectiveTheme, type); break;
        }
    }

    static string? ValidateIncomingPayload(string type, string? id, JsonElement payload)
    {
        try
        {
            if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                return $"Editor message '{type}' payload is required.";
            switch (type)
            {
                case "editorReady": return RequireString(payload, "monacoVersion", type, allowEmpty: false, maxLength: 128);
                case "editorError": return RequireString(payload, "message", type, allowEmpty: false, maxLength: MaxMarkerMessageLength) ?? OptionalString(payload, "detail", type, MaxMarkerMessageLength);
                case "textChanged": return ValidateDocumentChangesPayload(payload, type);
                case "viewStateChanged": return ValidateViewStatePayload(payload, type);
                case "cursorChanged":
                case "selectionChanged":
                case "saveRequested": return ValidateDocumentIdPayload(payload, type);
                case "navigationRequested":
                    return RequireString(payload, "filePath", type, allowEmpty: false, maxLength: 32768)
                        ?? ValidateRangePayload(payload, type);
                case "response": return string.IsNullOrWhiteSpace(id) ? "Editor response id is required." : null;
                case "requestCompleted": return RequireString(payload, "command", type, allowEmpty: false, maxLength: 128);
                default: return null;
            }
        }
        catch (InvalidOperationException ex) { return $"Editor message '{type}' payload is invalid: {ex.Message}"; }
    }

    static string? ValidateRangePayload(JsonElement payload, string type)
    {
        foreach (var name in new[] { "startLine", "startColumn", "endLine", "endColumn" })
            if (!payload.TryGetProperty(name, out var value) || !value.TryGetInt32(out var number) || number < 1)
                return $"Editor message '{type}' requires a positive integer '{name}'.";
        return null;
    }

    internal static string? ValidateDocumentChangesPayload(JsonElement payload, string type)
    {
        var error = ValidateDocumentIdPayload(payload, type) ?? RequireVersion(payload, "previousVersion", type) ?? RequireVersion(payload, "newVersion", type);
        if (error is not null) return error;
        if (!payload.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array || changes.GetArrayLength() == 0)
            return $"Editor message '{type}' requires a non-empty changes array.";
        foreach (var change in changes.EnumerateArray())
        {
            error = RequirePositiveInt(change, "rangeOffset", type, allowZero: true)
                ?? RequirePositiveInt(change, "rangeLength", type, allowZero: true)
                ?? RequireString(change, "text", type, true, MaxTextLength);
            if (error is not null) return error;
        }
        return null;
    }

    internal static string? ValidateDocumentTextVersionPayload(JsonElement payload, string type) =>
        ValidateDocumentIdPayload(payload, type)
        ?? RequireString(payload, "text", type, allowEmpty: true, maxLength: MaxTextLength)
        ?? RequireVersion(payload, "version", type);

    static string? ValidateViewStatePayload(JsonElement payload, string type) =>
        ValidateDocumentIdPayload(payload, type) ?? RequirePositiveInt(payload, "cursorLine", type) ?? RequirePositiveInt(payload, "cursorColumn", type);

    static string? ValidateDocumentIdPayload(JsonElement payload, string type)
    {
        if (!payload.TryGetProperty("documentId", out var value) || value.ValueKind != JsonValueKind.String || !Guid.TryParse(value.GetString(), out var id) || id == Guid.Empty)
            return $"Editor message '{type}' requires a non-empty documentId.";
        return null;
    }

    static string? RequireString(JsonElement payload, string name, string type, bool allowEmpty, int maxLength)
    {
        if (!payload.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String) return $"Editor message '{type}' requires string '{name}'.";
        var text = value.GetString() ?? string.Empty;
        if (!allowEmpty && string.IsNullOrWhiteSpace(text)) return $"Editor message '{type}' requires non-empty '{name}'.";
        return text.Length > maxLength ? $"Editor message '{type}' '{name}' is too long." : null;
    }

    static string? OptionalString(JsonElement payload, string name, string type, int maxLength) =>
        payload.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? RequireString(payload, name, type, true, maxLength) : null;

    static string? RequireVersion(JsonElement payload, string name, string type) => RequirePositiveInt(payload, name, type, allowZero: true, max: MaxVersion);
    static string? RequirePositiveInt(JsonElement payload, string name, string type, bool allowZero = false, int max = int.MaxValue)
    {
        if (!payload.TryGetProperty(name, out var value) || !value.TryGetInt32(out var number)) return $"Editor message '{type}' requires integer '{name}'.";
        if (number < (allowZero ? 0 : 1) || number > max) return $"Editor message '{type}' has out-of-range '{name}'.";
        return null;
    }

    static void ValidateDocumentId(Guid documentId, string type) { if (documentId == Guid.Empty) throw new ArgumentException($"Editor message '{type}' requires a non-empty document id."); }
    static void ValidateText(string text, string type) { if (text.Length > MaxTextLength) throw new ArgumentException($"Editor message '{type}' text is too long."); }
    static void ValidateVersion(int version, string type) { if (version is < 0 or > MaxVersion) throw new ArgumentOutOfRangeException(nameof(version), $"Editor message '{type}' version is out of range."); }
    static void ValidateVersionlessPosition(int line, int column, string type) { if (line < 1 || column < 1) throw new ArgumentOutOfRangeException(nameof(line), $"Editor message '{type}' position is out of range."); }
    static void ValidateRange(int startLine, int startColumn, int endLine, int endColumn, string type) { if (startLine < 1 || startColumn < 1 || endLine < startLine || (endLine == startLine && endColumn < startColumn)) throw new ArgumentOutOfRangeException(nameof(startLine), $"Editor message '{type}' range is invalid."); }
    static void ValidateMarkers(IReadOnlyList<EditorMarkerPayload> markers, string type) { if (markers.Count > MaxMarkerCount) throw new ArgumentException($"Editor message '{type}' has too many markers."); foreach (var m in markers) { ValidateRange(m.StartLine, m.StartColumn, m.EndLine, m.EndColumn, type); if (string.IsNullOrWhiteSpace(m.Message) || m.Message.Length > MaxMarkerMessageLength) throw new ArgumentException($"Editor message '{type}' marker message is invalid."); } }
    static void ValidateChanges(IReadOnlyList<EditorContentChangePayload> changes, string type) { if (changes.Count == 0) throw new ArgumentException($"Editor message '{type}' requires changes."); foreach (var change in changes) { if (change.RangeOffset < 0 || change.RangeLength < 0) throw new ArgumentOutOfRangeException(nameof(changes), $"Editor message '{type}' change range is invalid."); ValidateText(change.Text, type); } }
    static void ValidateTheme(StudioTheme theme, string effectiveTheme, string type) { if (!Enum.IsDefined(theme)) throw new ArgumentOutOfRangeException(nameof(theme)); if (effectiveTheme is not ("Light" or "Dark")) throw new ArgumentException($"Editor message '{type}' effective theme is invalid."); }

    public sealed record EditorEnvelope<T>(string Type, string? Id, T Payload);
}

public sealed record EditorMessageValidationResult(bool IsValid, EditorMessageProtocol.EditorEnvelope<JsonElement>? Envelope, string? Error)
{
    public static EditorMessageValidationResult Valid(EditorMessageProtocol.EditorEnvelope<JsonElement> envelope) => new(true, envelope, null);
    public static EditorMessageValidationResult Invalid(string error) => new(false, null, error);
}

public sealed record EditorReadyPayload(string MonacoVersion);
public sealed record EditorErrorPayload(string Message, string? Detail = null);
public sealed record OpenEditorDocumentPayload(Guid DocumentId, string Path, string Text, string Language, int Version, EditorViewStatePayload? ViewState = null);
public sealed record ActivateEditorDocumentPayload(Guid DocumentId);
public sealed record CloseEditorDocumentPayload(Guid DocumentId);
public sealed record RevealEditorRangePayload(Guid DocumentId, int StartLine, int StartColumn, int EndLine, int EndColumn);
public sealed record EditorViewStatePayload(Guid DocumentId, int CursorLine, int CursorColumn, double ScrollTop, double ScrollLeft);
public sealed record SetEditorThemePayload(StudioTheme Theme, bool HighContrast = false, string EffectiveTheme = "Dark");
public sealed record EditorContentChangePayload(int RangeOffset, int RangeLength, string Text);
public sealed record EditorTextChangedPayload(Guid DocumentId, int PreviousVersion, int NewVersion, IReadOnlyList<EditorContentChangePayload> Changes);
public sealed record EditorRequestTextPayload(Guid DocumentId);
public sealed record EditorTextResponsePayload(Guid DocumentId, string Text, int Version);
public sealed record EditorSetMarkersPayload(Guid DocumentId, IReadOnlyList<EditorMarkerPayload> Markers);
public sealed record EditorMarkerPayload(int StartLine, int StartColumn, int EndLine, int EndColumn, string Severity, string Message, string? Code = null);

/// <summary>Versioned, correlated request emitted by a Monaco semantic provider.</summary>
public sealed record MonacoLanguageRequestPayload(string RequestId, Guid DocumentId, int DocumentVersion, int Line = 1, int Column = 1);
/// <summary>Identity shared by every Monaco language response so clients can reject obsolete work.</summary>
public sealed record MonacoLanguageResponsePayload(string RequestId, int ModelVersion, bool IsStale, bool IsCancelled, bool IsPartial = false);
public sealed record MonacoCancelRequestPayload(string RequestId);

public enum EditorBridgeState { Created, Initializing, Ready, Faulted, Recovering, Disposed }

public sealed class EditorBridgeRequestTimeoutException(string message) : TimeoutException(message);

public sealed class EditorBridge
{
    readonly ConcurrentDictionary<string, PendingRequest> _pending = new();
    readonly IStudioLogService? _logService;

    public EditorBridge(IStudioLogService? logService = null) => _logService = logService;
    int _nextId;

    public EditorBridgeState State { get; private set; } = EditorBridgeState.Created;
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public event EventHandler<EditorReadyPayload>? Ready;
    public event EventHandler<EditorErrorPayload>? Error;
    public event EventHandler<EditorTextChangedPayload>? TextChanged;
    public event EventHandler<EditorViewStatePayload>? ViewStateChanged;

    public string CreateCommand<T>(string type, T payload) => EditorMessageProtocol.Serialize(type, payload);

    public (string Id, string Json, Task<JsonElement> Response) CreateRequest<T>(string type, T payload, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var id = Interlocked.Increment(ref _nextId).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var pending = new PendingRequest(type, timeout ?? DefaultTimeout, cancellationToken);
        if (!_pending.TryAdd(id, pending))
            throw new InvalidOperationException("Could not register editor request.");
        return (id, EditorMessageProtocol.Serialize(type, payload, id), pending.Task);
    }

    public async Task<JsonElement> RequestAsync(Func<string, string> send, string type, object payload, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var (_, json, response) = CreateRequest(type, payload, timeout, cancellationToken);
        send(json);
        return await response.ConfigureAwait(false);
    }

    public EditorMessageValidationResult AcceptHostMessage(string json)
    {
        var result = EditorMessageProtocol.TryDeserialize(json);
        if (!result.IsValid || result.Envelope is null)
        {
            _logService?.Log(StudioLogCategory.EditorBridge, OutputSeverity.Warning, "EditorMessageValidation", "Editor message validation failed.", result.Error);
            return result;
        }

        var envelope = result.Envelope;
        try
        {
            switch (envelope.Type)
            {
                case "editorReady":
                    State = EditorBridgeState.Ready;
                    Ready?.Invoke(this, DeserializePayload<EditorReadyPayload>(envelope) ?? new EditorReadyPayload("unknown"));
                    break;
                case "editorError":
                    State = EditorBridgeState.Faulted;
                    Error?.Invoke(this, DeserializePayload<EditorErrorPayload>(envelope) ?? new EditorErrorPayload("Unknown editor error."));
                    break;
                case "textChanged":
                    var changed = DeserializePayload<EditorTextChangedPayload>(envelope);
                    if (changed is not null) TextChanged?.Invoke(this, changed);
                    break;
                case "viewStateChanged":
                    var viewState = DeserializePayload<EditorViewStatePayload>(envelope);
                    if (viewState is not null) ViewStateChanged?.Invoke(this, viewState);
                    break;
                case "response":
                    if (string.IsNullOrWhiteSpace(envelope.Id) || !_pending.TryRemove(envelope.Id, out var pending))
                        return InvalidAfterEnvelope($"Editor response id '{envelope.Id}' does not match a pending request.");
                    var responseError = ValidateResponsePayload(pending.RequestType, envelope.Payload);
                    if (responseError is not null)
                    {
                        pending.SetException(new InvalidOperationException(responseError));
                        return InvalidAfterEnvelope(responseError);
                    }
                    pending.SetResult(envelope.Payload);
                    break;
            }
        }
        catch (JsonException ex) { return InvalidAfterEnvelope($"Editor message '{envelope.Type}' payload could not be deserialized: {ex.Message}"); }
        catch (NotSupportedException ex) { return InvalidAfterEnvelope($"Editor message '{envelope.Type}' payload could not be deserialized: {ex.Message}"); }

        return result;
    }

    static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    static T? DeserializePayload<T>(EditorMessageProtocol.EditorEnvelope<JsonElement> envelope) => envelope.Payload.Deserialize<T>(PayloadJsonOptions);

    static string? ValidateResponsePayload(string requestType, JsonElement payload) => requestType switch
    {
        "requestText" => EditorMessageProtocol.ValidateDocumentTextVersionPayload(payload, "response"),
        _ => $"Editor request '{requestType}' does not define a response payload."
    };

    EditorMessageValidationResult InvalidAfterEnvelope(string error)
    {
        _logService?.Log(StudioLogCategory.EditorBridge, OutputSeverity.Warning, "EditorMessageValidation", "Editor message validation failed.", error);
        return EditorMessageValidationResult.Invalid(error);
    }

    public void Recover()
    {
        State = EditorBridgeState.Recovering;
        _logService?.Log(StudioLogCategory.EditorBridge, OutputSeverity.Warning, "EditorRecovery", "Editor bridge entered recovery.", $"pendingRequests={_pending.Count}");
        foreach (var item in _pending)
            if (_pending.TryRemove(item.Key, out var pending)) pending.SetException(new InvalidOperationException("Editor process recovered before the request completed."));
    }

    sealed class PendingRequest : IDisposable
    {
        readonly TaskCompletionSource<JsonElement> _source = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly CancellationTokenRegistration _registration;
        readonly Timer _timer;
        readonly string _type;

        public PendingRequest(string type, TimeSpan timeout, CancellationToken cancellationToken)
        {
            _type = type;
            _registration = cancellationToken.Register(() => SetException(new OperationCanceledException(cancellationToken)));
            _timer = new Timer(_ => SetException(new EditorBridgeRequestTimeoutException($"Editor request '{_type}' timed out.")), null, timeout, Timeout.InfiniteTimeSpan);
        }

        public Task<JsonElement> Task => _source.Task;
        public string RequestType => _type;
        public void SetResult(JsonElement payload) { Dispose(); _source.TrySetResult(payload); }
        public void SetException(Exception exception) { Dispose(); _source.TrySetException(exception); }
        public void Dispose() { _timer.Dispose(); _registration.Dispose(); }
    }
}

internal static class PathComparer { public static readonly StringComparer Comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal; public static bool Equals(string a, string b) => Comparer.Equals(Path.GetFullPath(a), Path.GetFullPath(b)); }
