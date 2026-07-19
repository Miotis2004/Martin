using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Text;
using Martin.Compiler;
using Martin.ProjectSystem;

namespace Martin.Studio.Core;

public readonly record struct DocumentVersion(long Value) { public DocumentVersion Next() => new(Value + 1); }
public readonly record struct WorkspaceGeneration(long Value) { public WorkspaceGeneration Next() => new(Value + 1); }
public enum LineEndingKind { Lf, Crlf, Cr }
public enum StudioTheme { System, Light, Dark }
public enum BuildState { Idle, Building, Cancelling }
public enum ExecutionState { Idle, Running, ExternalLaunching, Stopping }
public enum OutputChannel { Build, Program, Studio, ProjectSystem, Compiler, Workspace, Document, EditorBridge, Execution, Diagnostics, Output, Settings, Session, WebView2 }
public enum OutputSeverity { Info, Warning, Error }
public enum ProjectNodeKind { Project, Manifest, Folder, MartinSourceFile, OtherFile, MissingFile, GeneratedFolder }
public sealed record SelectionRange(int StartLine, int StartColumn, int EndLine, int EndColumn);
public sealed record EditorViewState { public int CursorLine { get; init; } = 1; public int CursorColumn { get; init; } = 1; public double ScrollTop { get; init; } public double ScrollLeft { get; init; } public ImmutableArray<SelectionRange> Selections { get; init; } = []; }
public sealed class DocumentModel { public required Guid Id { get; init; } public required string FilePath { get; set; } public required string DisplayName { get; set; } public string Text { get; set; } = string.Empty; public Encoding Encoding { get; set; } = new UTF8Encoding(false); public LineEndingKind LineEndings { get; set; } public bool IsDirty { get; set; } public bool IsReadOnly { get; set; } public bool IsDeleted { get; set; } public DateTimeOffset? LastDiskWriteTime { get; set; } public string? SavedContentHash { get; set; } public bool HasExternalChanges { get; set; } public DocumentVersion Version { get; set; } = new(0); public EditorViewState ViewState { get; set; } = new(); }
public sealed record TextRange(int StartLine, int StartColumn, int EndLine, int EndColumn);
public sealed record StudioDiagnostic(string Code, OutputSeverity Severity, string Message, string? FilePath, TextRange? Range, string Source, Guid? DocumentId = null, DocumentVersion? Version = null, WorkspaceGeneration? Generation = null);
public sealed record EditorDiagnostic(string Code, OutputSeverity Severity, string Message, TextRange Range, string Source);
public sealed record DocumentDiagnostics(Guid DocumentId, DocumentVersion Version, ImmutableArray<StudioDiagnostic> Diagnostics);
public sealed class StudioSemanticTokensChangedEventArgs(Guid documentId, DocumentVersion version) : EventArgs { public Guid DocumentId { get; } = documentId; public DocumentVersion Version { get; } = version; }
public sealed record OutputEntry(DateTimeOffset Timestamp, OutputChannel Channel, OutputSeverity Severity, string Message);

public enum StudioLogCategory { Studio, Workspace, ProjectSystem, Document, EditorBridge, Build, Execution, Diagnostics, Output, Settings, Session, WebView2 }
public enum StudioRecoveryKind { SettingsCorruption, SessionCorruption, WatcherOverflow, WebView2ProcessFailure, UnexpectedException }
public sealed record StudioLogEntry(DateTimeOffset Timestamp, StudioLogCategory Category, OutputSeverity Severity, string Operation, string Message, string? Detail = null, string? ExceptionType = null);
public sealed record StudioRecoveryResult(StudioRecoveryKind Kind, bool Success, string UserMessage, string DiagnosticCode);
public sealed record OutputFilter(ImmutableHashSet<OutputChannel>? Channels = null, ImmutableHashSet<OutputSeverity>? Severities = null, string? Text = null);
public sealed record DiagnosticFilter(ImmutableHashSet<OutputSeverity>? Severities = null, ImmutableHashSet<string>? Sources = null, string? Text = null);
public sealed record RecentProjectEntry(string ManifestPath, string DisplayName, DateTimeOffset LastOpened);
public sealed record StudioSettings
{
    public int SchemaVersion { get; init; } = 1;
    public StudioTheme Theme { get; init; } = StudioTheme.System;
    public bool ReopenLastProject { get; init; } = true;
    public bool RestoreOpenDocuments { get; init; } = true;
    public int MaximumRecentProjects { get; init; } = 10;
    public int MaximumOutputEntries { get; init; } = 10_000;
    public bool ShowGeneratedFolders { get; init; } = false;
    public bool AutoReloadCleanDocuments { get; init; } = true;
    public bool RestoreWindowPlacement { get; init; } = true;
    public ImmutableArray<RecentProjectEntry> RecentProjects { get; init; } = [];
    public string? LastProject { get; init; }
}
public sealed record DocumentSessionState(string FilePath, EditorViewState ViewState);
public sealed record WindowPlacementState(double Width = 1200, double Height = 800, bool IsMaximized = false, int? X = null, int? Y = null);
public sealed record StudioSession
{
    public int SchemaVersion { get; init; } = 1;
    public string? ProjectPath { get; init; }
    public ImmutableArray<DocumentSessionState> OpenDocuments { get; init; } = [];
    public string? ActiveDocument { get; init; }
    public ImmutableArray<string> ExpandedProjectTreePaths { get; init; } = [];
    public string BottomPanelTab { get; init; } = "Output";
    public bool BottomPanelVisible { get; init; } = true;
    public double ProjectExplorerWidth { get; init; } = 280;
    public bool ProjectExplorerVisible { get; init; } = true;
    public double BottomPanelHeight { get; init; } = 220;
    public WindowPlacementState WindowPlacement { get; init; } = new();
    public Martin.Build.BuildConfiguration ActiveBuildConfiguration { get; init; } = Martin.Build.BuildConfiguration.Debug;
}
public sealed class ProjectTreeNode { public required string Name { get; init; } public required string FullPath { get; init; } public ProjectNodeKind Kind { get; init; } public bool IsExpanded { get; set; } public bool ChildrenLoaded { get; set; } public ObservableCollection<ProjectTreeNode> Children { get; } = []; }
public sealed class StudioWorkspace { public MartinProject? Project { get; internal set; } public WorkspaceGeneration Generation { get; internal set; } public ObservableCollection<DocumentModel> OpenDocuments { get; } = []; public DocumentModel? ActiveDocument { get; internal set; } public ObservableCollection<StudioDiagnostic> Diagnostics { get; } = []; public BuildState BuildState { get; internal set; } public ExecutionState ExecutionState { get; internal set; } public Martin.Build.BuildConfiguration ActiveBuildConfiguration { get; set; } = Martin.Build.BuildConfiguration.Debug; public ProjectTreeNode? ProjectTree { get; internal set; } }
public sealed record CompilationSnapshot(Compilation Compilation, ImmutableArray<SnapshotSource> Sources, ImmutableDictionary<Guid, DocumentVersion> Versions, WorkspaceGeneration Generation) { public ImmutableArray<StudioDiagnostic> Diagnostics { get; init; } = []; public bool HasSourceReadErrors => Diagnostics.Any(d => d.Severity == OutputSeverity.Error); }
public sealed record SnapshotSource(string FilePath, string Text, Guid? DocumentId, DocumentVersion? Version);


public enum UnsavedChangesDecision { Save, Discard, Cancel }
public enum UnsavedChangesContext { CloseDocument, CloseAllDocuments, CloseProject, OpenAnotherProject, CreateNewProject, ExitApplication }
public enum ShutdownTransitionKind { CloseProject, OpenProject, CreateProject, ExitApplication }
public enum StudioTransitionStatus { Succeeded, Cancelled, Failed }
public sealed record StudioTransitionResult(StudioTransitionStatus Status, string? Message = null, ImmutableArray<StudioDiagnostic> Diagnostics = default)
{
    public bool Success => Status == StudioTransitionStatus.Succeeded;
    public static StudioTransitionResult Succeeded() => new(StudioTransitionStatus.Succeeded);
    public static StudioTransitionResult Cancelled(string? message = null) => new(StudioTransitionStatus.Cancelled, message);
    public static StudioTransitionResult Failed(string message, ImmutableArray<StudioDiagnostic> diagnostics = default) => new(StudioTransitionStatus.Failed, message, diagnostics);
}
public sealed record StudioShutdownRequest(UnsavedChangesContext Context = UnsavedChangesContext.ExitApplication);
public sealed record StudioShutdownResult(StudioTransitionStatus Status, string? Message = null) { public bool CanShutdown => Status == StudioTransitionStatus.Succeeded; }
public sealed record StudioProjectCreationRequest { public required string ProjectName { get; init; } public required string BaseDirectory { get; init; } public string TargetFramework { get; init; } = "net8.0"; public bool CreateGitIgnore { get; init; } = true; public bool OpenAfterCreation { get; init; } = true; }
public sealed record StudioProjectCreationResult { public bool Success { get; init; } public string? ProjectDirectory { get; init; } public string? ManifestPath { get; init; } public ImmutableArray<StudioDiagnostic> Diagnostics { get; init; } = []; }

public sealed record SaveDocumentResult(string FilePath, bool Success, string? Error = null);
public sealed record SaveAllResult(ImmutableArray<SaveDocumentResult> Results) { public bool Success => Results.All(r => r.Success); }
public sealed record ProjectCandidate
{
    public required MartinProject Project { get; init; }
    public required ProjectTreeNode RootNode { get; init; }
    public ImmutableArray<StudioDiagnostic> Diagnostics { get; init; } = [];
}
public sealed record ProjectOpenResult(bool Success, string? ProjectPath, string? DisplayName, ImmutableArray<StudioDiagnostic> Diagnostics)
{
    public static ProjectOpenResult Failed(string message, string? path = null, string code = "MRT5002") =>
        new(false, path, null, [new StudioDiagnostic(code, OutputSeverity.Error, message, path, null, "Studio")]);
}
public enum DirtyProjectTransitionChoice { Cancel, DiscardAndContinue, SaveAndContinue }
public enum ExternalChangeKind { Changed, Deleted, Renamed }
public enum CleanExternalChangeChoice { Reload, KeepCurrentView }
public enum DirtyExternalChangeChoice { Cancel, KeepEditorVersion, ReloadDiskVersion, SaveAs }
public sealed record ExternalChange(DocumentModel Document, ExternalChangeKind Kind, string? OldPath = null, string? NewPath = null);
public interface IExternalChangePolicy { Task<CleanExternalChangeChoice> ConfirmCleanChangeAsync(ExternalChange change, CancellationToken cancellationToken = default); Task<DirtyExternalChangeChoice> ConfirmDirtyChangeAsync(ExternalChange change, CancellationToken cancellationToken = default); Task<string?> GetSaveAsPathAsync(DocumentModel document, CancellationToken cancellationToken = default); }
public interface IDirtyProjectTransitionPolicy { Task<DirtyProjectTransitionChoice> ConfirmAsync(IReadOnlyList<DocumentModel> dirtyDocuments, CancellationToken cancellationToken = default); }
