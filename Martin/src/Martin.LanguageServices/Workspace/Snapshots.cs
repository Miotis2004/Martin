using System.Collections.Immutable;
using Martin.Compiler.Text;

namespace Martin.LanguageServices;

public sealed record LanguageDocumentSnapshot(DocumentId Id, string FilePath, string Text, DocumentVersion Version)
{
    public ProjectId ProjectId { get; init; }
    public bool IsOpen { get; init; }
    public bool IsDirty { get; init; }
    public bool ExistsOnDisk { get; init; } = true;
    public SourceText SourceText => SourceText.From(Text, FilePath);
    public LanguageDocumentSnapshot WithText(string text) => this with { Text = text, Version = Version.Next() };
    public LanguageDocumentSnapshot Apply(TextChange change) => WithText(Text.Remove(change.Span.Start, change.Span.Length).Insert(change.Span.Start, change.NewText));
}

public sealed record LanguageProjectSnapshot(ProjectId Id, string Name, string RootDirectory, ProjectVersion Version, ImmutableArray<LanguageDocumentSnapshot> Documents)
{
    public string ManifestPath { get; init; } = string.Empty;
    public LanguageProjectSnapshot UpsertDocument(LanguageDocumentSnapshot document)
    {
        var documents = Documents.RemoveAll(d => d.Id == document.Id).Add(document)
            .Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.FilePath, b.FilePath));
        return this with { Documents = documents, Version = Version.Next() };
    }
}

public sealed record LanguageWorkspaceSnapshot(WorkspaceId Id, WorkspaceVersion Version, ImmutableArray<LanguageProjectSnapshot> Projects)
{
    public LanguageProjectSnapshot? FindProject(ProjectId id) => Projects.FirstOrDefault(p => p.Id == id);
    public LanguageDocumentSnapshot? FindDocument(DocumentId id) => Projects.SelectMany(p => p.Documents).FirstOrDefault(d => d.Id == id);
    public LanguageWorkspaceSnapshot UpsertProject(LanguageProjectSnapshot project) => this with
    {
        Projects = Projects.RemoveAll(p => p.Id == project.Id).Add(project),
        Version = Version.Next()
    };
    public static LanguageWorkspaceSnapshot Empty() => new(WorkspaceId.CreateNew(), new(0), []);
}

public sealed record VersionedRequest<T>(DocumentId DocumentId, DocumentVersion Version, T Parameters);
public sealed record VersionedResponse<T>(DocumentId DocumentId, DocumentVersion Version, bool IsStale, T Result);
