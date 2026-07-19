using System.Collections.Immutable;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Text;
using Martin.LanguageServices;
using System.Text.Json;

namespace Martin.Studio.Core;

public sealed class StudioLanguageProvider : IAsyncDisposable
{
    readonly Martin.LanguageServices.MartinLanguageService languageService;
    readonly Martin.LanguageServices.LanguageWorkspace _languageWorkspace = new();
    readonly AnalysisScheduler _analysisScheduler;
    readonly LiveDiagnosticPublisher _liveDiagnostics;
    readonly IStudioLogService? _log;
    readonly SemaphoreSlim _lifecycle = new(1, 1);
    Martin.LanguageServices.ProjectId? _projectId;
    string? _manifestPath;
    readonly Dictionary<Guid, Martin.LanguageServices.DocumentId> _documentIds = [];
    readonly Dictionary<Martin.LanguageServices.DocumentId, Guid> _studioIds = [];
    bool _disposed;

    public StudioLanguageProvider(Martin.LanguageServices.MartinLanguageService languageService, IStudioLogService? logService = null)
    {
        this.languageService = languageService ?? throw new ArgumentNullException(nameof(languageService));
        _log = logService;
        _analysisScheduler = new(_languageWorkspace);
        _liveDiagnostics = new(_languageWorkspace, _analysisScheduler, languageService);
        _liveDiagnostics.SnapshotPublished += OnDiagnosticSnapshot;
    }

    public Martin.LanguageServices.LanguageWorkspaceSnapshot Workspace => _languageWorkspace.CurrentSnapshot;
    public event Action<DocumentDiagnostics>? DiagnosticsPublished;
    public event EventHandler<StudioSemanticTokensChangedEventArgs>? SemanticTokensInvalidated;

    /// <summary>Compatibility entry point. New integrations should call the incremental lifecycle methods.</summary>
    public void Attach(StudioWorkspace studioWorkspace) => SynchronizeAsync(studioWorkspace).GetAwaiter().GetResult();

    public async Task SynchronizeAsync(StudioWorkspace studioWorkspace, CancellationToken cancellationToken = default)
    {
        if (studioWorkspace.Project is null)
        {
            await CloseProjectAsync(cancellationToken);
            return;
        }
        await OpenProjectAsync(studioWorkspace.Project, cancellationToken);
        var openIds = studioWorkspace.OpenDocuments.Select(d => d.Id).ToHashSet();
        foreach (var id in _documentIds.Keys.Where(id => !openIds.Contains(id)).ToArray())
            await CloseDocumentAsync(id, cancellationToken);
        foreach (var document in studioWorkspace.OpenDocuments)
            await OpenDocumentAsync(document, cancellationToken);
    }

    public async Task OpenProjectAsync(Martin.ProjectSystem.MartinProject project, CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            var manifest = Path.GetFullPath(project.ManifestPath);
            if (_projectId is not null && string.Equals(_manifestPath, manifest, Martin.ProjectSystem.ProjectPathPolicy.PathComparison))
                return;
            await CloseProjectCoreAsync(cancellationToken);
            _projectId = await _languageWorkspace.OpenProjectAsync(project, cancellationToken);
            _manifestPath = manifest;
            _liveDiagnostics.ScheduleProject(_projectId.Value);
            Log("OpenLanguageProject", "Language analysis started for the project.", manifest);
        }
        finally { _lifecycle.Release(); }
    }

    public async Task RefreshProjectAsync(Martin.ProjectSystem.MartinProject project, CancellationToken cancellationToken = default)
    {
        if (_projectId is not { } id) throw new InvalidOperationException("No language project is open.");
        await _languageWorkspace.RefreshProjectAsync(id, project, cancellationToken);
        foreach (var mapping in _documentIds.Where(p => Workspace.FindDocument(p.Value) is null).ToArray())
        { _documentIds.Remove(mapping.Key); _studioIds.Remove(mapping.Value); }
        Log("RefreshLanguageProject", "Project sources and manifest were refreshed.");
    }

    public async Task<DocumentId> OpenDocumentAsync(DocumentModel document, CancellationToken cancellationToken = default)
    {
        if (_projectId is not { } project) throw new InvalidOperationException("No language project is open.");
        var id = await _languageWorkspace.OpenDocumentAsync(project, document.FilePath, SourceText.From(document.Text, document.FilePath),
            new(document.Version.Value), document.IsDirty, cancellationToken);
        if (_documentIds.TryGetValue(document.Id, out var previous)) _studioIds.Remove(previous);
        _documentIds[document.Id] = id; _studioIds[id] = document.Id;
        return id;
    }

    public Task<DocumentChangeResult> ApplyEditorChangesAsync(Guid studioId, DocumentVersion previousVersion, DocumentVersion newVersion,
        ImmutableArray<Martin.LanguageServices.TextChange> changes, CancellationToken cancellationToken = default)
    {
        if (!_documentIds.TryGetValue(studioId, out var id))
            return Task.FromResult(new DocumentChangeResult(DocumentChangeStatus.RequiresFullTextResynchronization, "MRTLS1010", "The editor document is not open."));
        return _languageWorkspace.ApplyDocumentChangesAsync(new Martin.LanguageServices.DocumentChange
        { DocumentId = id, PreviousVersion = new(previousVersion.Value), NewVersion = new(newVersion.Value), Changes = changes }, cancellationToken);
    }

    public async Task ReplaceDocumentTextAsync(DocumentModel document, CancellationToken cancellationToken = default)
    {
        if (!_documentIds.TryGetValue(document.Id, out var id)) { await OpenDocumentAsync(document, cancellationToken); return; }
        var current = Workspace.FindDocument(id) ?? throw new InvalidOperationException("The mapped language document is missing.");
        var next = new Martin.LanguageServices.DocumentVersion(Math.Max(document.Version.Value, current.Version.Value + 1));
        await _languageWorkspace.ReplaceDocumentTextAsync(id, current.Version, next, SourceText.From(document.Text, document.FilePath), document.IsDirty, cancellationToken);
    }

    public Task ApplyEditorTextAsync(DocumentModel document, CancellationToken cancellationToken = default) => ReplaceDocumentTextAsync(document, cancellationToken);

    public async Task SaveDocumentAsync(DocumentModel document, CancellationToken cancellationToken = default)
    {
        if (!_documentIds.TryGetValue(document.Id, out var id)) { await OpenDocumentAsync(document, cancellationToken); return; }
        await _languageWorkspace.SaveDocumentAsync(id, SourceText.From(document.Text, document.FilePath), new(document.Version.Value), cancellationToken);
    }

    public async Task CloseDocumentAsync(Guid studioId, CancellationToken cancellationToken = default)
    {
        if (!_documentIds.Remove(studioId, out var id)) return;
        _studioIds.Remove(id);
        if (Workspace.FindDocument(id) is not null) await _languageWorkspace.CloseDocumentAsync(id, cancellationToken);
    }

    public async Task RenameDocumentAsync(Guid studioId, string newPath, CancellationToken cancellationToken = default)
    {
        if (!_documentIds.TryGetValue(studioId, out var id)) throw new KeyNotFoundException("The Studio document is not mapped.");
        await _languageWorkspace.RenameDocumentAsync(id, newPath, cancellationToken);
    }

    public async Task ReloadDocumentFromDiskAsync(Guid studioId, DocumentVersion editorVersion, CancellationToken cancellationToken = default)
    {
        if (!_documentIds.TryGetValue(studioId, out var id)) throw new KeyNotFoundException("The Studio document is not mapped.");
        var current = Workspace.FindDocument(id) ?? throw new InvalidOperationException("The mapped language document is missing.");
        var text = await File.ReadAllTextAsync(current.FilePath, cancellationToken);
        var next = new Martin.LanguageServices.DocumentVersion(Math.Max(editorVersion.Value, current.Version.Value + 1));
        await _languageWorkspace.ReplaceDocumentTextAsync(id, current.Version, next, SourceText.From(text, current.FilePath), false, cancellationToken);
    }

    public async Task CloseProjectAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken);
        try { ThrowIfDisposed(); await CloseProjectCoreAsync(cancellationToken); }
        finally { _lifecycle.Release(); }
    }

    async Task CloseProjectCoreAsync(CancellationToken cancellationToken)
    {
        if (_projectId is { } id)
        {
            await _languageWorkspace.CloseProjectAsync(id, cancellationToken);
            languageService.CloseProject(id);
        }
        _projectId = null; _manifestPath = null; _documentIds.Clear(); _studioIds.Clear();
    }

    void OnDiagnosticSnapshot(object? sender, DiagnosticSnapshotEventArgs e)
    {
        foreach (var set in e.Snapshot.Documents.Values)
        {
            if (!_studioIds.TryGetValue(set.DocumentId, out var id)) continue;
            var version = new DocumentVersion(set.DocumentVersion.Value);
            var diagnostics = set.Diagnostics.Select(d => new StudioDiagnostic(d.Code, ToStudioSeverity(d.Severity), d.Message, d.FilePath,
                new(d.LineSpan.Start.Line + 1, d.LineSpan.Start.Character + 1, d.LineSpan.End.Line + 1, d.LineSpan.End.Character + 1), d.Source, id, version)).ToImmutableArray();
            DiagnosticsPublished?.Invoke(new(id, version, diagnostics));
            SemanticTokensInvalidated?.Invoke(this, new(id, version));
        }
    }

    void Log(string operation, string message, string? detail = null) => _log?.Log(StudioLogCategory.Diagnostics, OutputSeverity.Info, operation, message, detail);
    void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(StudioLanguageProvider)); }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _lifecycle.WaitAsync();
        try
        {
            if (_disposed) return;
            await CloseProjectCoreAsync(CancellationToken.None);
            _liveDiagnostics.SnapshotPublished -= OnDiagnosticSnapshot;
            await _liveDiagnostics.DisposeAsync();
            await _analysisScheduler.DisposeAsync();
            await _languageWorkspace.DisposeAsync();
            _disposed = true;
        }
        finally { _lifecycle.Release(); _lifecycle.Dispose(); }
    }

    public async Task<DocumentDiagnostics> AnalyzeAsync(DocumentModel document, CancellationToken cancellationToken = default)
    {
        if (!_documentIds.TryGetValue(document.Id, out var languageDocumentId))
            return new(document.Id, document.Version, []);

        var response = await languageService.GetDiagnosticsAsync(
            Workspace,
            new Martin.LanguageServices.VersionedRequest<object?>(languageDocumentId, new Martin.LanguageServices.DocumentVersion(document.Version.Value), null),
            cancellationToken);

        if (response.IsStale)
            return new(document.Id, document.Version, []);

        var diagnostics = response.Result
            .Select(d => new StudioDiagnostic(d.Code, ToStudioSeverity(d.Severity), d.Message, d.FilePath,
                new TextRange(d.LineSpan.Start.Line + 1, d.LineSpan.Start.Character + 1, d.LineSpan.End.Line + 1, d.LineSpan.End.Character + 1), d.Source,
                document.Id, document.Version))
            .ToImmutableArray();
        return new(document.Id, document.Version, diagnostics);
    }

    static OutputSeverity ToStudioSeverity(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Info => OutputSeverity.Info,
        DiagnosticSeverity.Warning => OutputSeverity.Warning,
        _ => OutputSeverity.Error
    };

    public async Task<object> HandleMonacoRequestAsync(string type, JsonElement payload, CancellationToken cancellationToken = default)
    {
        var studioId = payload.GetProperty("documentId").GetGuid();
        var requestedVersion = payload.GetProperty("documentVersion").GetInt32();
        var modelVersion = payload.TryGetProperty("modelVersion", out var model) ? model.GetInt32() : requestedVersion;
        if (!_documentIds.TryGetValue(studioId, out var documentId)) return Stale(MonacoResponseIdentity.Unknown(requestedVersion, modelVersion));
        var workspace = Workspace;
        var document = workspace.FindDocument(documentId);
        var project = workspace.Projects.FirstOrDefault(p => p.Documents.Any(d => d.Id == documentId));
        if (document is null || project is null || document.Version.Value != requestedVersion) return Stale(MonacoResponseIdentity.From(workspace, project, document, modelVersion));
        var position = type is "language/requestFormatting" or "language/requestSemanticTokens" ? 0 :
            PositionConverter.Instance.ToOffset(document.SourceText, new(payload.GetProperty("line").GetInt32() - 1, payload.GetProperty("column").GetInt32() - 1));
        var identity = new RequestIdentity(workspace, project, document, position);
        var responseIdentity = MonacoResponseIdentity.From(workspace, project, document, modelVersion);

        return type switch
        {
            "language/requestCompletion" => Completion(await ScheduleAsync(identity.Completion(payload), AnalysisPriority.Immediate, languageService.CompleteAsync, cancellationToken), document, responseIdentity, !IsCurrent(responseIdentity)),
            "language/requestHover" => Hover(await ScheduleAsync(identity.Hover(), AnalysisPriority.Interactive, languageService.HoverAsync, cancellationToken), document, responseIdentity, !IsCurrent(responseIdentity)),
            "language/requestDefinition" => Locations(await ScheduleAsync(identity.Definition(), AnalysisPriority.Interactive, languageService.GetDefinitionsAsync, cancellationToken), workspace, responseIdentity, !IsCurrent(responseIdentity)),
            "language/requestReferences" => References(await ScheduleAsync(identity.References(payload), AnalysisPriority.Background, languageService.FindReferencesAsync, cancellationToken), workspace, responseIdentity, !IsCurrent(responseIdentity)),
            "language/requestSignatureHelp" => Signature(await ScheduleAsync(identity.Signature(payload), AnalysisPriority.Immediate, languageService.GetSignatureHelpAsync, cancellationToken), responseIdentity, !IsCurrent(responseIdentity)),
            "language/requestFormatting" => Formatting(await ScheduleAsync(identity.Formatting(payload), AnalysisPriority.Interactive, languageService.FormatDocumentAsync, cancellationToken), document, responseIdentity, !IsCurrent(responseIdentity)),
            "language/requestSemanticTokens" => Tokens(await ScheduleAsync(identity.Classification(), AnalysisPriority.Normal, languageService.GetClassificationsAsync, cancellationToken), document, responseIdentity, !IsCurrent(responseIdentity)),
            _ => Stale(responseIdentity)
        };
    }

    public object CreateMonacoStatusResponse(JsonElement payload, bool isCancelled, string? error = null)
    {
        var version = payload.TryGetProperty("documentVersion", out var value) && value.TryGetInt32(out var parsed) ? parsed : -1;
        var modelVersion = payload.TryGetProperty("modelVersion", out var model) && model.TryGetInt32(out var modelValue) ? modelValue : version;
        var identity = MonacoResponseIdentity.Unknown(version, modelVersion);
        if (payload.TryGetProperty("documentId", out var id) && id.TryGetGuid(out var studioId) && _documentIds.TryGetValue(studioId, out var documentId))
        {
            var workspace = Workspace;
            var document = workspace.FindDocument(documentId);
            var project = workspace.Projects.FirstOrDefault(p => p.Documents.Any(d => d.Id == documentId));
            identity = MonacoResponseIdentity.From(workspace, project, document, modelVersion);
        }
        return new { identity.WorkspaceId, identity.WorkspaceVersion, identity.ProjectId, identity.ProjectVersion, identity.DocumentId, identity.DocumentVersion, modelVersion = identity.ModelVersion, isStale = !isCancelled, isCancelled, error };
    }

    bool IsCurrent(MonacoResponseIdentity identity)
    {
        var current = Workspace;
        if (current.Id.Value.ToString() != identity.WorkspaceId || current.Version.Value != identity.WorkspaceVersion) return false;
        var project = current.Projects.FirstOrDefault(p => p.Id.Value.ToString() == identity.ProjectId);
        if (project is null || project.Version.Value != identity.ProjectVersion) return false;
        var document = project.Documents.FirstOrDefault(d => d.Id.Value.ToString() == identity.DocumentId);
        return document is not null && document.Version.Value == identity.DocumentVersion;
    }

    Task<LanguageResult<T>> ScheduleAsync<TRequest, T>(TRequest request, AnalysisPriority priority,
        Func<LanguageWorkspaceSnapshot, TRequest, CancellationToken, Task<LanguageResult<T>>> operation,
        CancellationToken cancellationToken) where TRequest : LanguageRequest =>
        _analysisScheduler.RunAsync(request, priority, async (context, ct) =>
            (await operation(context.Workspace, request, ct).ConfigureAwait(false)).Value, cancellationToken);

    static object Completion(LanguageResult<ImmutableArray<CompletionItem>> result, LanguageDocumentSnapshot document, MonacoResponseIdentity identity, bool invalidated) => new
    {
        identity.WorkspaceId,
        identity.WorkspaceVersion,
        identity.ProjectId,
        identity.ProjectVersion,
        identity.DocumentId,
        identity.DocumentVersion,
        modelVersion = identity.ModelVersion,
        isStale = result.IsStale || invalidated,
        isCancelled = result.IsCancelled,
        items = (result.Value.IsDefault ? ImmutableArray<CompletionItem>.Empty : result.Value).Select(x => new { x.Label, kind = x.Kind.ToString(), x.Detail, documentation = EscapeMarkdown(x.DocumentationMarkdown), insertText = x.InsertText ?? x.TextEdit.NewText, isSnippet = x.InsertTextFormat == InsertTextFormat.Snippet, range = Range(document, x.TextEdit.Span), commitCharacters = x.CommitCharacters.Select(c => c.ToString()) })
    };
    static object Hover(LanguageResult<HoverInfo?> result, LanguageDocumentSnapshot document, MonacoResponseIdentity identity, bool invalidated) => new { identity.WorkspaceId, identity.WorkspaceVersion, identity.ProjectId, identity.ProjectVersion, identity.DocumentId, identity.DocumentVersion, modelVersion = identity.ModelVersion, isStale = result.IsStale || invalidated, isCancelled = result.IsCancelled, hover = result.Value is null ? null : new { markdown = EscapeMarkdown(result.Value.Markdown), range = Range(document, result.Value.Span) } };
    object Locations(LanguageResult<ImmutableArray<DefinitionLocation>> result, LanguageWorkspaceSnapshot workspace, MonacoResponseIdentity identity, bool invalidated) => new { identity.WorkspaceId, identity.WorkspaceVersion, identity.ProjectId, identity.ProjectVersion, identity.DocumentId, identity.DocumentVersion, modelVersion = identity.ModelVersion, isStale = result.IsStale || invalidated, isCancelled = result.IsCancelled, locations = (result.Value.IsDefault ? ImmutableArray<DefinitionLocation>.Empty : result.Value).Select(x => Location(workspace, x.DocumentId, x.FilePath, x.Span)) };
    object References(LanguageResult<ImmutableArray<ReferenceLocation>> result, LanguageWorkspaceSnapshot workspace, MonacoResponseIdentity identity, bool invalidated) => new { identity.WorkspaceId, identity.WorkspaceVersion, identity.ProjectId, identity.ProjectVersion, identity.DocumentId, identity.DocumentVersion, modelVersion = identity.ModelVersion, isStale = result.IsStale || invalidated, isCancelled = result.IsCancelled, locations = (result.Value.IsDefault ? ImmutableArray<ReferenceLocation>.Empty : result.Value).Select(x => Location(workspace, x.DocumentId, x.FilePath, x.Span)) };
    static object Signature(LanguageResult<SignatureHelp?> result, MonacoResponseIdentity identity, bool invalidated) => new { identity.WorkspaceId, identity.WorkspaceVersion, identity.ProjectId, identity.ProjectVersion, identity.DocumentId, identity.DocumentVersion, modelVersion = identity.ModelVersion, isStale = result.IsStale || invalidated, isCancelled = result.IsCancelled, signatureHelp = result.Value is null ? null : new { signatures = result.Value.Signatures.Select(s => new { s.Label, documentation = EscapeMarkdown(s.DocumentationMarkdown), parameters = s.Parameters.Select(p => new { p.Label, documentation = EscapeMarkdown(p.DocumentationMarkdown) }) }), activeSignature = result.Value.ActiveSignature, activeParameter = result.Value.ActiveParameter } };
    static object Formatting(LanguageResult<FormattingResult> result, LanguageDocumentSnapshot document, MonacoResponseIdentity identity, bool invalidated) => new { identity.WorkspaceId, identity.WorkspaceVersion, identity.ProjectId, identity.ProjectVersion, identity.DocumentId, identity.DocumentVersion, modelVersion = identity.ModelVersion, isStale = result.IsStale || invalidated, isCancelled = result.IsCancelled, edits = result.Value?.Edits.Select(e => new { range = Range(document, e.Span), text = e.NewText }) ?? [] };
    static object Tokens(LanguageResult<ImmutableArray<ClassifiedSpan>> result, LanguageDocumentSnapshot document, MonacoResponseIdentity identity, bool invalidated)
    {
        var data = new List<int>(); var previousLine = 0; var previousCharacter = 0;
        foreach (var item in (result.Value.IsDefault ? ImmutableArray<ClassifiedSpan>.Empty : result.Value).OrderBy(x => x.Span.Start))
        {
            var range = PositionConverter.Instance.ToRange(document.SourceText, item.Span); if (range.Start.Line != range.End.Line) continue;
            var type = SemanticTokenType(item.Kind); var lineDelta = range.Start.Line - previousLine; var characterDelta = lineDelta == 0 ? range.Start.Character - previousCharacter : range.Start.Character;
            data.AddRange([lineDelta, characterDelta, range.End.Character - range.Start.Character, type, (int)item.Modifiers]); previousLine = range.Start.Line; previousCharacter = range.Start.Character;
        }
        return new { identity.WorkspaceId, identity.WorkspaceVersion, identity.ProjectId, identity.ProjectVersion, identity.DocumentId, identity.DocumentVersion, modelVersion = identity.ModelVersion, isStale = result.IsStale || invalidated, isCancelled = result.IsCancelled, data };
    }
    static int SemanticTokenType(ClassificationKind kind) => kind switch { ClassificationKind.Keyword => 0, ClassificationKind.Comment or ClassificationKind.DocumentationComment => 1, ClassificationKind.NumberLiteral => 2, ClassificationKind.StringLiteral => 3, ClassificationKind.Operator => 4, ClassificationKind.Type => 5, ClassificationKind.Struct => 6, ClassificationKind.Class => 7, ClassificationKind.Enum => 8, ClassificationKind.EnumCase => 9, ClassificationKind.Protocol => 10, ClassificationKind.Function => 11, ClassificationKind.Method => 12, ClassificationKind.Initializer => 13, ClassificationKind.Property => 14, ClassificationKind.Parameter => 15, _ => 16 };
    object Location(LanguageWorkspaceSnapshot workspace, DocumentId id, string path, TextSpan span) { var document = workspace.FindDocument(id); return new { documentId = FindStudioId(id), filePath = path, range = document is null ? new { startLine = 1, startColumn = 1, endLine = 1, endColumn = 1 } : Range(document, span) }; }
    Guid? FindStudioId(DocumentId id) => _studioIds.TryGetValue(id, out var key) ? key : null;
    static object Range(LanguageDocumentSnapshot document, TextSpan span) { var r = PositionConverter.Instance.ToRange(document.SourceText, span); return new { startLine = r.Start.Line + 1, startColumn = r.Start.Character + 1, endLine = r.End.Line + 1, endColumn = r.End.Character + 1 }; }
    static object Stale(MonacoResponseIdentity identity) => new { identity.WorkspaceId, identity.WorkspaceVersion, identity.ProjectId, identity.ProjectVersion, identity.DocumentId, identity.DocumentVersion, modelVersion = identity.ModelVersion, isStale = true, isCancelled = false };

    readonly record struct MonacoResponseIdentity(string WorkspaceId, long WorkspaceVersion, string ProjectId, long ProjectVersion, string DocumentId, long DocumentVersion, int ModelVersion)
    {
        public static MonacoResponseIdentity From(LanguageWorkspaceSnapshot workspace, LanguageProjectSnapshot? project, LanguageDocumentSnapshot? document, int modelVersion) => new(
            workspace.Id.Value.ToString(), workspace.Version.Value, project?.Id.Value.ToString() ?? string.Empty, project?.Version.Value ?? -1,
            document?.Id.Value.ToString() ?? string.Empty, document?.Version.Value ?? modelVersion, modelVersion);
        public static MonacoResponseIdentity Unknown(int documentVersion, int modelVersion) => new(string.Empty, -1, string.Empty, -1, string.Empty, documentVersion, modelVersion);
    }
    static string? EscapeMarkdown(string? value) => value?.Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);

    readonly record struct RequestIdentity(LanguageWorkspaceSnapshot Workspace, LanguageProjectSnapshot Project, LanguageDocumentSnapshot Document, int Position)
    {
        public CompletionRequest Completion(JsonElement p) => new() { WorkspaceId = Workspace.Id, WorkspaceVersion = Workspace.Version, ProjectId = Project.Id, ProjectVersion = Project.Version, DocumentId = Document.Id, DocumentVersion = Document.Version, Position = Position, MaximumResults = p.TryGetProperty("maximumResults", out var m) ? Math.Clamp(m.GetInt32(), 1, 1000) : 200, TriggerCharacter = Character(p, "triggerCharacter") };
        public HoverRequest Hover() => new() { WorkspaceId = Workspace.Id, WorkspaceVersion = Workspace.Version, ProjectId = Project.Id, ProjectVersion = Project.Version, DocumentId = Document.Id, DocumentVersion = Document.Version, Position = Position };
        public DefinitionRequest Definition() => new() { WorkspaceId = Workspace.Id, WorkspaceVersion = Workspace.Version, ProjectId = Project.Id, ProjectVersion = Project.Version, DocumentId = Document.Id, DocumentVersion = Document.Version, Position = Position };
        public ReferencesRequest References(JsonElement p) => new() { WorkspaceId = Workspace.Id, WorkspaceVersion = Workspace.Version, ProjectId = Project.Id, ProjectVersion = Project.Version, DocumentId = Document.Id, DocumentVersion = Document.Version, Position = Position, IncludeDeclaration = p.TryGetProperty("includeDeclaration", out var i) && i.GetBoolean() };
        public SignatureHelpRequest Signature(JsonElement p) => new() { WorkspaceId = Workspace.Id, WorkspaceVersion = Workspace.Version, ProjectId = Project.Id, ProjectVersion = Project.Version, DocumentId = Document.Id, DocumentVersion = Document.Version, Position = Position, TriggerCharacter = Character(p, "triggerCharacter"), IsRetrigger = p.TryGetProperty("isRetrigger", out var r) && r.GetBoolean() };
        public FormattingRequest Formatting(JsonElement p) => new() { WorkspaceId = Workspace.Id, WorkspaceVersion = Workspace.Version, ProjectId = Project.Id, ProjectVersion = Project.Version, DocumentId = Document.Id, DocumentVersion = Document.Version, Options = new FormattingOptions { IndentSize = p.TryGetProperty("tabSize", out var t) ? Math.Clamp(t.GetInt32(), 1, 8) : 4, UseTabs = p.TryGetProperty("insertSpaces", out var s) && !s.GetBoolean() } };
        public ClassificationRequest Classification() => new() { WorkspaceId = Workspace.Id, WorkspaceVersion = Workspace.Version, ProjectId = Project.Id, ProjectVersion = Project.Version, DocumentId = Document.Id, DocumentVersion = Document.Version };
        static char? Character(JsonElement p, string name) => p.TryGetProperty(name, out var c) && c.ValueKind == JsonValueKind.String && c.GetString() is { Length: 1 } text ? text[0] : null;
    }
}
