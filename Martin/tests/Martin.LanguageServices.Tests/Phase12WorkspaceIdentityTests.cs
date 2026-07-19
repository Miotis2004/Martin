using Martin.Compiler.Text;
using Martin.LanguageServices;
using Martin.ProjectSystem;
using Xunit;

namespace Martin.LanguageServices.Tests;

public sealed class Phase12WorkspaceIdentityTests
{
    [Fact]
    public async Task Opening_project_materializes_sources_without_requiring_exact_builder_capacity()
    {
        using var project = new TempProject();
        await using var workspace = new LanguageWorkspace();

        var projectId = await workspace.OpenProjectAsync(project.Load());

        var openedProject = Assert.Single(workspace.CurrentSnapshot.Projects);
        Assert.Equal(projectId, openedProject.Id);
        Assert.Equal(project.Main, Assert.Single(openedProject.Documents).FilePath);
    }

    [Fact]
    public async Task Snapshots_keep_ids_and_open_overlays_override_disk()
    {
        using var project = new TempProject();
        await using var workspace = new LanguageWorkspace();
        var projectId = await workspace.OpenProjectAsync(project.Load());
        var first = workspace.CurrentSnapshot;
        var diskDocument = Assert.Single(first.Projects.Single().Documents);

        var documentId = await workspace.OpenDocumentAsync(projectId, project.Main,
            SourceText.From("func main() { return 2 }", project.Main), new(7), true);
        var second = workspace.CurrentSnapshot;

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(diskDocument.Id, documentId);
        Assert.Equal("func main() { return 2 }", second.FindDocument(documentId)!.Text);
        Assert.True(second.FindDocument(documentId)!.IsDirty);
    }

    [Fact]
    public async Task Refresh_adds_and_removes_sources_and_known_rename_keeps_identity()
    {
        using var project = new TempProject();
        await using var workspace = new LanguageWorkspace();
        var projectId = await workspace.OpenProjectAsync(project.Load());
        var originalId = workspace.CurrentSnapshot.Projects.Single().Documents.Single().Id;
        var renamed = Path.Combine(project.Sources, "renamed.martin");
        File.Move(project.Main, renamed);

        await workspace.RenameDocumentAsync(originalId, renamed);
        await workspace.RefreshProjectAsync(projectId, project.Load());
        Assert.Equal(originalId, workspace.CurrentSnapshot.Projects.Single().Documents.Single().Id);

        var extra = Path.Combine(project.Sources, "extra.martin");
        await File.WriteAllTextAsync(extra, "func extra() { return 3 }");
        await workspace.RefreshProjectAsync(projectId, project.Load());
        Assert.Equal(2, workspace.CurrentSnapshot.Projects.Single().Documents.Length);
        File.Delete(extra);
        await workspace.RefreshProjectAsync(projectId, project.Load());
        Assert.Single(workspace.CurrentSnapshot.Projects.Single().Documents);
    }

    [Fact]
    public async Task Closing_and_reopening_document_preserves_id_and_events_are_published()
    {
        using var project = new TempProject();
        await using var workspace = new LanguageWorkspace();
        var changes = new List<LanguageWorkspaceChangeKind>();
        workspace.WorkspaceChanged += (_, e) => changes.Add(e.Change.Kind);
        var projectId = await workspace.OpenProjectAsync(project.Load());
        var firstId = await workspace.OpenDocumentAsync(projectId, project.Main, SourceText.From("edited", project.Main), new(1), true);
        await workspace.CloseDocumentAsync(firstId);
        var reopenedId = await workspace.OpenDocumentAsync(projectId, project.Main, SourceText.From("edited again", project.Main), new(2), true);

        Assert.Equal(firstId, reopenedId);
        Assert.Contains(LanguageWorkspaceChangeKind.ProjectOpened, changes);
        Assert.Contains(LanguageWorkspaceChangeKind.DocumentClosed, changes);
        Assert.Contains(LanguageWorkspaceChangeKind.DocumentOpened, changes);
    }

    sealed class TempProject : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "MartinPhase12", Guid.NewGuid().ToString("N"));
        public string Sources => Path.Combine(Root, "Sources");
        public string Main => Path.Combine(Sources, "main.martin");
        public TempProject()
        {
            Directory.CreateDirectory(Sources);
            File.WriteAllText(Path.Combine(Root, "Martin.toml"), "manifest-version = 1\n\n[package]\nname = \"Identity\"\nversion = \"0.1.0\"\n\n[target]\nkind = \"executable\"\nframework = \"net8.0\"\nentry = \"main\"\n");
            File.WriteAllText(Main, "func main() { return 1 }");
        }
        public MartinProject Load() => Assert.IsType<MartinProject>(MartinProjectLoader.Load(new ProjectLoadOptions { ProjectPath = Root }).Project);
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
