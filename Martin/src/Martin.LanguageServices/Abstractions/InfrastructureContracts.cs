using Martin.Compiler.Text;
using Martin.ProjectSystem;

namespace Martin.LanguageServices;

public interface ILanguageWorkspace : IAsyncDisposable
{
    LanguageWorkspaceSnapshot CurrentSnapshot { get; }
    event EventHandler<LanguageWorkspaceChangedEventArgs>? WorkspaceChanged;
    Task<ProjectId> OpenProjectAsync(MartinProject project, CancellationToken cancellationToken = default);
    Task CloseProjectAsync(ProjectId projectId, CancellationToken cancellationToken = default);
    Task<DocumentId> OpenDocumentAsync(ProjectId projectId, string filePath, SourceText text, DocumentVersion editorVersion, bool isDirty, CancellationToken cancellationToken = default);
    Task<DocumentChangeResult> ApplyDocumentChangesAsync(DocumentChange change, CancellationToken cancellationToken = default);
    Task ReplaceDocumentTextAsync(DocumentId documentId, DocumentVersion previousVersion, DocumentVersion newVersion, SourceText text, bool isDirty, CancellationToken cancellationToken = default);
    Task SaveDocumentAsync(DocumentId documentId, SourceText text, DocumentVersion editorVersion, CancellationToken cancellationToken = default);
    Task CloseDocumentAsync(DocumentId documentId, CancellationToken cancellationToken = default);
    Task RefreshProjectAsync(ProjectId projectId, MartinProject project, CancellationToken cancellationToken = default);
    Task RenameDocumentAsync(DocumentId documentId, string newFilePath, CancellationToken cancellationToken = default);
}

public enum AnalysisPriority
{
    Immediate,
    Interactive,
    Normal,
    Background
}

public abstract record LanguageRequest
{
    public required WorkspaceId WorkspaceId { get; init; }
    public required WorkspaceVersion WorkspaceVersion { get; init; }
    public required ProjectId ProjectId { get; init; }
    public required ProjectVersion ProjectVersion { get; init; }
    public required DocumentId DocumentId { get; init; }
    public required DocumentVersion DocumentVersion { get; init; }
    /// <summary>Identifies duplicate work. Position-sensitive features should include the position.</summary>
    public virtual string FeatureKey => GetType().FullName ?? GetType().Name;
}

public abstract record PositionLanguageRequest : LanguageRequest
{
    public required int Position { get; init; }
    public override string FeatureKey => $"{base.FeatureKey}:{Position}";
}

public sealed record LanguageResult<T>
{
    public required WorkspaceId WorkspaceId { get; init; }
    public required WorkspaceVersion WorkspaceVersion { get; init; }
    public required ProjectId ProjectId { get; init; }
    public required ProjectVersion ProjectVersion { get; init; }
    public required DocumentId DocumentId { get; init; }
    public required DocumentVersion DocumentVersion { get; init; }
    public required T Value { get; init; }
    public bool IsPartial { get; init; }
    public bool IsCancelled { get; init; }
    public bool IsStale { get; init; }
}

public sealed record LanguageAnalysisContext(LanguageWorkspaceSnapshot Workspace, LanguageProjectSnapshot Project, LanguageDocumentSnapshot Document);

public interface IAnalysisScheduler : IAsyncDisposable
{
    Task<LanguageResult<T>> RunAsync<T>(LanguageRequest request, AnalysisPriority priority,
                                        Func<LanguageAnalysisContext, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
    void ScheduleDocumentDiagnostics(DocumentId documentId, DocumentVersion version);
    void ScheduleProjectAnalysis(ProjectId projectId, ProjectVersion version);
    void CancelDocument(DocumentId documentId);
    void CancelProject(ProjectId projectId);
}

public interface IDocumentationService
{
    string? GetDocumentation(LanguageWorkspaceSnapshot workspace, DocumentId documentId, int position);
}
