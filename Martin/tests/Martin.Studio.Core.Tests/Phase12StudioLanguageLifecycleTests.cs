using System.Collections.Immutable;
using Martin.Compiler.Text;
using Martin.LanguageServices;
using Martin.Studio.Core;
using System.Text.Json;
using Xunit;

namespace Martin_Studio_Core_Tests;

public sealed class Phase12StudioLanguageLifecycleTests
{
    [Fact]
    public async Task Monaco_responses_include_the_complete_request_identity()
    {
        using var project = new TempProject();
        using var studio = new WorkspaceService();
        Assert.True(studio.OpenProject(project.Root).Success);
        var document = studio.OpenDocument(project.Main);
        await using var provider = new StudioLanguageProvider(new MartinLanguageService());
        await provider.OpenProjectAsync(studio.Workspace.Project!);
        var languageDocumentId = await provider.OpenDocumentAsync(document);
        var snapshot = provider.Workspace;
        var languageProject = snapshot.Projects.Single();
        var languageDocument = snapshot.FindDocument(languageDocumentId)!;
        var payload = JsonSerializer.SerializeToElement(new {
            documentId = document.Id,
            documentVersion = document.Version.Value,
            modelVersion = document.Version.Value,
            line = 1,
            column = 1
        });

        var response = await provider.HandleMonacoRequestAsync("language/requestHover", payload);
        var json = JsonSerializer.SerializeToElement(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(snapshot.Id.Value.ToString(), json.GetProperty("workspaceId").GetString());
        Assert.Equal(snapshot.Version.Value, json.GetProperty("workspaceVersion").GetInt64());
        Assert.Equal(languageProject.Id.Value.ToString(), json.GetProperty("projectId").GetString());
        Assert.Equal(languageProject.Version.Value, json.GetProperty("projectVersion").GetInt64());
        Assert.Equal(languageDocument.Id.Value.ToString(), json.GetProperty("documentId").GetString());
        Assert.Equal(languageDocument.Version.Value, json.GetProperty("documentVersion").GetInt64());
        Assert.Equal(document.Version.Value, json.GetProperty("modelVersion").GetInt32());
    }

    [Fact]
    public async Task Project_lifecycle_keeps_closed_sources_and_overlays_open_buffers()
    {
        using var project = new TempProject();
        var studio = new WorkspaceService();
        Assert.True(studio.OpenProject(project.Root).Success);
        await using var provider = new StudioLanguageProvider(new MartinLanguageService());

        await provider.OpenProjectAsync(studio.Workspace.Project!);
        Assert.Equal(2, provider.Workspace.Projects.Single().Documents.Length);

        var document = studio.OpenDocument(project.Main);
        await provider.OpenDocumentAsync(document);
        var languageDocument = provider.Workspace.Projects.Single().Documents.Single(d => Path.GetFullPath(d.FilePath) == Path.GetFullPath(project.Main));
        Assert.True(languageDocument.IsOpen);

        var oldText = document.Text;
        var changed = oldText.Replace("1", "42", StringComparison.Ordinal);
        var start = oldText.IndexOf('1');
        var result = await provider.ApplyEditorChangesAsync(document.Id, document.Version, document.Version.Next(),
                                                            [new Martin.LanguageServices.TextChange(new TextSpan(start, 1), "42")]);
        Assert.True(result.IsApplied);
        Assert.Equal(changed, provider.Workspace.FindDocument(languageDocument.Id)!.Text);

        await provider.CloseDocumentAsync(document.Id);
        var closed = provider.Workspace.FindDocument(languageDocument.Id);
        Assert.NotNull(closed);
        Assert.False(closed.IsOpen);
        Assert.Equal(2, provider.Workspace.Projects.Single().Documents.Length);
    }

    [Fact]
    public async Task Rename_reload_project_close_and_reopen_preserve_language_identity()
    {
        using var project = new TempProject();
        var studio = new WorkspaceService();
        studio.OpenProject(project.Root);
        var document = studio.OpenDocument(project.Main);
        await using var provider = new StudioLanguageProvider(new MartinLanguageService());
        await provider.OpenProjectAsync(studio.Workspace.Project!);
        var originalId = await provider.OpenDocumentAsync(document);

        var renamed = Path.Combine(project.Sources, "renamed.martin");
        File.Move(project.Main, renamed);
        document.FilePath = renamed;
        await provider.RenameDocumentAsync(document.Id, renamed);
        Assert.Equal(originalId, provider.Workspace.FindDocument(originalId)!.Id);
        Assert.Equal(Path.GetFullPath(renamed), provider.Workspace.FindDocument(originalId)!.FilePath);

        File.WriteAllText(renamed, "func main() { return 99 }");
        await provider.ReloadDocumentFromDiskAsync(document.Id, document.Version.Next());
        Assert.Contains("99", provider.Workspace.FindDocument(originalId)!.Text);

        await provider.CloseProjectAsync();
        Assert.Empty(provider.Workspace.Projects);
        var reload = studio.OpenProject(project.Root);
        Assert.True(reload.Success);
        await provider.OpenProjectAsync(studio.Workspace.Project!);
        Assert.Contains(provider.Workspace.Projects.Single().Documents, d => d.Id == originalId);
    }

    [Fact]
    public async Task Studio_project_refresh_updates_the_language_workspace_source_set()
    {
        using var project = new TempProject();
        using var studio = new WorkspaceService();
        Assert.True(studio.OpenProject(project.Root).Success);
        await using var provider = new StudioLanguageProvider(new MartinLanguageService());
        await provider.OpenProjectAsync(studio.Workspace.Project!);
        studio.ProjectRefreshed += provider.RefreshProjectAsync;
        var added = Path.Combine(project.Sources, "added.martin");

        await File.WriteAllTextAsync(added, "func added() { return 3 }");
        await studio.HandleExternalChangesAsync();

        Assert.Contains(provider.Workspace.Projects.Single().Documents,
                        document => Path.GetFullPath(document.FilePath) == Path.GetFullPath(added));
    }

    [Fact]
    public async Task Closing_project_clears_language_service_analysis_cache()
    {
        using var project = new TempProject();
        using var studio = new WorkspaceService();
        Assert.True(studio.OpenProject(project.Root).Success);
        using var cache = new LanguageAnalysisCache();
        var service = new MartinLanguageService(cache);
        await using var provider = new StudioLanguageProvider(service);
        await provider.OpenProjectAsync(studio.Workspace.Project!);
        var snapshot = provider.Workspace;

        await service.AnalyzeDocumentAsync(snapshot, snapshot.Projects.Single().Documents[0].Id);
        Assert.NotEqual(0, cache.Metrics.DocumentEntries + cache.Metrics.ProjectEntries);

        await provider.CloseProjectAsync();

        Assert.Equal(0, cache.Metrics.DocumentEntries);
        Assert.Equal(0, cache.Metrics.ProjectEntries);
    }

    sealed class TempProject : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "MartinPhase12Studio", Guid.NewGuid().ToString("N"));
        public string Sources => Path.Combine(Root, "Sources");
        public string Main => Path.Combine(Sources, "main.martin");
        public TempProject()
        {
            Directory.CreateDirectory(Sources);
            File.WriteAllText(Path.Combine(Root, "Martin.toml"), "manifest-version = 1\n\n[package]\nname = \"Lifecycle\"\nversion = \"0.1.0\"\n\n[target]\nkind = \"executable\"\nframework = \"net8.0\"\nentry = \"main\"\n");
            File.WriteAllText(Main, "func main() { return 1 }");
            File.WriteAllText(Path.Combine(Sources, "helper.martin"), "func helper() { return 2 }");
        }
        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
    }
}
