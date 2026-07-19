namespace Martin.LanguageServices;

public enum LanguageWorkspaceChangeKind
{
    ProjectOpened, ProjectClosed, ProjectRefreshed, DocumentAdded, DocumentRemoved,
    DocumentRenamed, DocumentOpened, DocumentChanged, DocumentSaved, DocumentClosed
}

public sealed record LanguageWorkspaceChange(
    LanguageWorkspaceChangeKind Kind,
    ProjectId? ProjectId = null,
    DocumentId? DocumentId = null,
    string? OldPath = null,
    string? NewPath = null);

public sealed class LanguageWorkspaceChangedEventArgs(
    LanguageWorkspaceSnapshot oldSnapshot,
    LanguageWorkspaceSnapshot newSnapshot,
    LanguageWorkspaceChange change) : EventArgs
{
    public LanguageWorkspaceSnapshot OldSnapshot { get; } = oldSnapshot;
    public LanguageWorkspaceSnapshot NewSnapshot { get; } = newSnapshot;
    public LanguageWorkspaceChange Change { get; } = change;
}
