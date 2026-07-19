using System.Collections.Immutable;
using Martin.Build;
using Martin.Execution;
using Martin.ProjectSystem;

namespace Martin.Studio.Core;

public interface ISettingsService
{
    Task<StudioSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(StudioSettings settings, CancellationToken cancellationToken = default);
}
public interface ISessionService
{
    Task<StudioSession> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(StudioSession session, CancellationToken cancellationToken = default);
}
public interface IRecentProjectService
{
    IReadOnlyList<RecentProjectEntry> Items { get; }
    void Load(IEnumerable<RecentProjectEntry> items);
    void Add(string manifestPath, string? displayName = null);
    void Remove(string manifestPath);
    void Clear();
}
public interface IOutputService
{
    IReadOnlyList<OutputEntry> Entries { get; }
    void Add(OutputChannel channel, OutputSeverity severity, string message);
    IReadOnlyList<OutputEntry> GetEntries(OutputFilter? filter = null);
    string CopyAll(OutputFilter? filter = null);
    Task SaveLogAsync(string path, OutputFilter? filter = null, CancellationToken cancellationToken = default);
    void Clear();
}
public interface IWorkspaceService
{
    StudioWorkspace Workspace { get; }
    event EventHandler? EditorSynchronizationRequested;
    event Func<DocumentModel, CancellationToken, Task>? DocumentReloaded;
    event Func<MartinProject, CancellationToken, Task>? ProjectRefreshed;
    ProjectLoadResult OpenProject(string path);
    ProjectLoadResult OpenProjectManifest(string manifestPath);
    ProjectCandidate? LoadProjectCandidate(string path, bool isManifestPath, out ImmutableArray<StudioDiagnostic> diagnostics, CancellationToken cancellationToken = default);
    void CommitProjectCandidate(ProjectCandidate candidate);
    void CloseProject();
    DocumentModel OpenDocument(string path);
    void ActivateDocument(Guid id);
    void ApplyEditorChange(Guid id, string text);
    void UpdateViewState(Guid id, EditorViewState viewState);
    bool TryNavigateToDocument(string path, TextRange? range, out DocumentModel? document);
    Task SaveAsync(DocumentModel document, CancellationToken cancellationToken = default);
    Task<SaveDocumentResult> SaveAsAsync(DocumentModel document, string path, CancellationToken cancellationToken = default);
    Task<SaveAllResult> SaveAllWithResultsAsync(CancellationToken cancellationToken = default);
    Task SaveAllAsync(CancellationToken cancellationToken = default);
    bool CloseDocument(Guid id, bool force = false);
    CompilationSnapshot CreateSnapshot(CancellationToken cancellationToken = default);
    Task<CompilationSnapshot> CreateSnapshotAsync(CancellationToken cancellationToken = default);
    void LoadProjectTreeChildren(ProjectTreeNode node, CancellationToken cancellationToken = default);
    void RefreshProjectTree(CancellationToken cancellationToken = default);
    Task HandleExternalChangesAsync(CancellationToken cancellationToken = default);
    bool TryOpenProjectTreeNode(ProjectTreeNode node, out DocumentModel? document);
    string CopyProjectTreeNodePath(ProjectTreeNode node);
    bool RevealProjectTreeNode(ProjectTreeNode node);
}
public interface IDiagnosticService
{
    void Apply(DocumentDiagnostics diagnostics);
    System.Collections.Immutable.ImmutableArray<EditorDiagnostic> ToEditor(Guid id);
    IReadOnlyList<StudioDiagnostic> GetDiagnostics(DiagnosticFilter? filter = null);
    string CopyAll(DiagnosticFilter? filter = null);
    StudioDiagnostic? Next(DiagnosticFilter? filter = null);
    StudioDiagnostic? Previous(DiagnosticFilter? filter = null);
}
public interface IBuildCoordinator
{
    Task<BuildResult> BuildAsync(CancellationToken cancellationToken = default);
    Task<ProjectCleanResult> CleanAsync(CancellationToken cancellationToken = default);
    void Cancel();
}
public enum ExternalChangeDecision
{
    Reload,
    KeepCurrentView,
    KeepEditorVersion,
    ReloadDiskVersion,
    SaveAs,
    Cancel
}
public interface IExecutionCoordinator
{
    Task<ExecutionResult> RunAsync(BuildResult build, IReadOnlyList<string>? args = null, bool useExternalTerminal = false, bool requireCleanDocuments = false, CancellationToken cancellationToken = default);
    ExecutionResult CreateFreshNoBuildResult(CancellationToken cancellationToken = default);
    void Stop();
}

public interface IFileDialogService
{
    Task<string?> PickProjectManifestAsync(CancellationToken cancellationToken = default);
    Task<string?> PickProjectFolderAsync(CancellationToken cancellationToken = default);
    Task<string?> PickProjectBaseFolderAsync(CancellationToken cancellationToken = default);
    Task<string?> PickSaveFileAsync(string suggestedFileName, string? initialDirectory = null, CancellationToken cancellationToken = default);
}
public interface IMessageDialogService
{
    Task<UnsavedChangesDecision> ConfirmUnsavedChangesAsync(IReadOnlyList<DocumentModel> documents, UnsavedChangesContext context, CancellationToken cancellationToken = default);
    Task<ExternalChangeDecision> ConfirmExternalChangeAsync(DocumentModel document, ExternalChangeKind kind, CancellationToken cancellationToken = default);
    Task ShowErrorAsync(string title, string message, CancellationToken cancellationToken = default);
    Task ShowInformationAsync(string title, string message, CancellationToken cancellationToken = default);
}
public interface IUiDispatcher
{
    bool HasThreadAccess { get; }
    Task InvokeAsync(Action action, CancellationToken cancellationToken = default);
    Task InvokeAsync(Func<Task> action, CancellationToken cancellationToken = default);
    Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken = default);
}
public interface IClipboardService
{
    Task SetTextAsync(string text, CancellationToken cancellationToken = default);
}
public interface IFileRevealService
{
    Task RevealAsync(string path, CancellationToken cancellationToken = default);
}
public interface IEditorHost
{
    EditorBridgeState State { get; }
    event EventHandler<EditorTextChangedPayload>? TextChanged;
    event EventHandler<EditorViewStatePayload>? ViewStateChanged;
    event EventHandler<EditorErrorPayload>? Error;
    event EventHandler<Guid>? SaveRequested;
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task OpenDocumentAsync(DocumentModel document, CancellationToken cancellationToken = default);
    Task ActivateDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task CloseDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<string> GetDocumentTextAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task ReplaceTextAsync(DocumentModel document, CancellationToken cancellationToken = default);
    Task SetDiagnosticsAsync(Guid documentId, IReadOnlyList<EditorDiagnostic> diagnostics, CancellationToken cancellationToken = default);
    Task RevealRangeAsync(Guid documentId, TextRange range, bool select, CancellationToken cancellationToken = default);
    Task ExecuteCommandAsync(string command, CancellationToken cancellationToken = default);
    Task SetThemeAsync(StudioTheme theme, CancellationToken cancellationToken = default);
    Task FocusEditorAsync(CancellationToken cancellationToken = default);
}
public interface IProjectCreationService
{
    Task<StudioProjectCreationResult> CreateAsync(StudioProjectCreationRequest request, CancellationToken cancellationToken = default);
}

public interface IStudioLogService
{
    string LogsDirectory { get; }
    string CurrentLogPath { get; }
    void Log(StudioLogCategory category, OutputSeverity severity, string operation, string message, string? detail = null, Exception? exception = null);
    IReadOnlyList<StudioLogEntry> ReadCurrent();
}
public interface IStudioRecoveryService
{
    StudioRecoveryResult Recover(StudioRecoveryKind kind, string operation, Exception? exception = null);
}
public interface IProjectOpeningService
{
    Task<bool> ApproveProjectTransitionAsync(CancellationToken cancellationToken = default);
    Task<ProjectOpenResult> OpenProjectAsync(string path, CancellationToken cancellationToken = default);
    Task<ProjectOpenResult> OpenProjectAfterApprovedTransitionAsync(string path, CancellationToken cancellationToken = default);
    Task<ProjectOpenResult> OpenRecentProjectAsync(RecentProjectEntry entry, CancellationToken cancellationToken = default);
    Task<ProjectOpenResult?> ReopenLastProjectAsync(CancellationToken cancellationToken = default);
    Task SaveSessionAsync(CancellationToken cancellationToken = default);
    Task<bool> CloseProjectAsync(CancellationToken cancellationToken = default);
}
