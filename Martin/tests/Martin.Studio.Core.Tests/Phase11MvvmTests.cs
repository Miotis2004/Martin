using Martin.Build;
using Martin.Compiler;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Text;
using Martin.Execution;
using Martin.ProjectSystem;
using Martin.Studio.Core;
using Xunit;

namespace Martin_Studio_Core_Tests;

public sealed class Phase11MvvmTests
{

    [Fact]
    public async Task Dirty_document_close_cancel_keeps_tab_and_discard_closes_it()
    {
        using var project = new Phase11ProjectOpeningTests.Phase11TempProject();
        var workspace = new WorkspaceService();
        Assert.True(workspace.OpenProject(project.Root).Success);
        var document = workspace.OpenDocument(project.Main);
        workspace.ApplyEditorChange(document.Id, "func main() { return 6 }");
        var output = new OutputService();
        var dialogs = new TestMessageDialogService { Decision = UnsavedChangesDecision.Cancel };
        var viewModel = new MainWindowViewModel(workspace, new ProjectExplorerViewModel(), new DocumentTabsViewModel(), new OutputPaneViewModel(output), new ErrorListViewModel(), new StatusBarViewModel(), output, new TestProjectOpeningService(), messageDialogService: dialogs);

        await viewModel.CloseDocumentCommand.ExecuteAsync(document.Id);

        Assert.Contains(workspace.Workspace.OpenDocuments, d => d.Id == document.Id);
        Assert.True(document.IsDirty);

        dialogs.Decision = UnsavedChangesDecision.Discard;
        await viewModel.CloseDocumentCommand.ExecuteAsync(document.Id);

        Assert.DoesNotContain(workspace.Workspace.OpenDocuments, d => d.Id == document.Id);
    }

    [Fact]
    public async Task Shutdown_save_failure_aborts_and_keeps_dirty_document_open()
    {
        using var project = new Phase11ProjectOpeningTests.Phase11TempProject();
        var workspace = new WorkspaceService();
        Assert.True(workspace.OpenProject(project.Root).Success);
        var document = workspace.OpenDocument(project.Main);
        workspace.ApplyEditorChange(document.Id, "func main() { return 7 }");
        document.FilePath = "\0";
        var output = new OutputService();
        var dialogs = new TestMessageDialogService { Decision = UnsavedChangesDecision.Save };
        var viewModel = new MainWindowViewModel(workspace, new ProjectExplorerViewModel(), new DocumentTabsViewModel(), new OutputPaneViewModel(output), new ErrorListViewModel(), new StatusBarViewModel(), output, new TestProjectOpeningService(), messageDialogService: dialogs);

        var result = await viewModel.ShutdownAsync();

        Assert.False(result.CanShutdown);
        Assert.Contains(workspace.Workspace.OpenDocuments, d => d.Id == document.Id);
        Assert.True(document.IsDirty);
    }

    [Fact]
    public async Task Create_project_approves_transition_before_creating_and_opens_main_only_after_successful_open()
    {
        var workspace = new WorkspaceService();
        var output = new OutputService();
        var opener = new TestProjectOpeningService();
        var creation = new RecordingProjectCreationService(
            new StudioProjectCreationResult
            {
                Success = true,
                ProjectDirectory = Path.Combine(Path.GetTempPath(), "CreatedProject"),
                ManifestPath = Path.Combine(Path.GetTempPath(), "CreatedProject", "Martin.toml")
            });
        var viewModel = new MainWindowViewModel(
            workspace,
            new ProjectExplorerViewModel(),
            new DocumentTabsViewModel(),
            new OutputPaneViewModel(output),
            new ErrorListViewModel(),
            new StatusBarViewModel(),
            output,
            opener,
            projectCreationService: creation);

        opener.ApproveProjectTransitionResult = false;
        await viewModel.CreateProjectCommand.ExecuteAsync(new StudioProjectCreationRequest { ProjectName = "CanceledProject", BaseDirectory = Path.GetTempPath(), TargetFramework = "net8.0", CreateGitIgnore = true, OpenAfterCreation = true });

        Assert.False(creation.WasCalled);
        Assert.Equal([nameof(IProjectOpeningService.ApproveProjectTransitionAsync)], opener.Calls);

        opener.Calls.Clear();
        opener.ApproveProjectTransitionResult = true;
        opener.OpenProjectResult = new ProjectOpenResult(false, creation.Result.ManifestPath, null, []);
        await viewModel.CreateProjectCommand.ExecuteAsync(new StudioProjectCreationRequest { ProjectName = "CreatedProject", BaseDirectory = Path.GetTempPath(), TargetFramework = "net8.0", CreateGitIgnore = true, OpenAfterCreation = true });

        Assert.True(creation.WasCalled);
        Assert.Equal([nameof(IProjectOpeningService.ApproveProjectTransitionAsync), nameof(IProjectOpeningService.OpenProjectAfterApprovedTransitionAsync)], opener.Calls);
        Assert.Empty(workspace.Workspace.OpenDocuments);
        Assert.Contains(output.Entries, entry => entry.Channel == OutputChannel.ProjectSystem && entry.Severity == OutputSeverity.Error && entry.Message.Contains("opening it failed", StringComparison.Ordinal));
    }

    private sealed class RecordingProjectCreationService(StudioProjectCreationResult result) : IProjectCreationService
    {
        public StudioProjectCreationResult Result { get; } = result;
        public bool WasCalled { get; private set; }
        public Task<StudioProjectCreationResult> CreateAsync(StudioProjectCreationRequest request, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(Result);
        }
    }

    private sealed class TestMessageDialogService : IMessageDialogService
    {
        public UnsavedChangesDecision Decision { get; set; }
        public Task<UnsavedChangesDecision> ConfirmUnsavedChangesAsync(IReadOnlyList<DocumentModel> documents, UnsavedChangesContext context, CancellationToken cancellationToken = default) => Task.FromResult(Decision);
        public Task<ExternalChangeDecision> ConfirmExternalChangeAsync(DocumentModel document, ExternalChangeKind kind, CancellationToken cancellationToken = default) => Task.FromResult(ExternalChangeDecision.Cancel);
        public Task ShowErrorAsync(string title, string message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ShowInformationAsync(string title, string message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public void MainWindowViewModel_exposes_shell_children_and_startup_output_command()
    {
        var workspace = new WorkspaceService();
        var output = new OutputService();
        var viewModel = new MainWindowViewModel(
            workspace,
            new ProjectExplorerViewModel(),
            new DocumentTabsViewModel(),
            new OutputPaneViewModel(output),
            new ErrorListViewModel(),
            new StatusBarViewModel(),
            output,
            new TestProjectOpeningService());

        Assert.Equal("Martin Studio", viewModel.ApplicationTitle);
        Assert.Equal("No project open", viewModel.ActiveProjectSummary);

        viewModel.WriteOutputCommand.Execute(null);

        Assert.Contains(viewModel.OutputPane.Entries, entry => entry.Message == "Martin Studio MVVM shell initialized.");
    }
}

public sealed class Phase11ProjectOpeningTests
{
    [Fact]
    public async Task Project_opening_accepts_folder_or_manifest_and_persists_recent_project()
    {
        using var project = new Phase11TempProject();
        var workspace = new WorkspaceService();
        var recent = new RecentProjectService();
        var settings = new InMemorySettingsService();
        var output = new OutputService();
        var opener = new ProjectOpeningService(workspace, recent, settings, output);

        var folderResult = await opener.OpenProjectAsync(project.Root);
        Assert.True(folderResult.Success);
        Assert.Equal(project.Manifest, workspace.Workspace.Project!.ManifestPath);
        Assert.Single(recent.Items);

        var manifestResult = await opener.OpenProjectAsync(project.Manifest);
        Assert.True(manifestResult.Success);
        Assert.Equal(project.Manifest, settings.Settings.LastProject);
        Assert.Equal(project.Manifest, settings.Settings.RecentProjects.Single().ManifestPath);
    }

    [Fact]
    public async Task Project_opening_reports_invalid_project_diagnostics_without_adding_recent()
    {
        var workspace = new WorkspaceService();
        var recent = new RecentProjectService();
        var settings = new InMemorySettingsService();
        var output = new OutputService();
        var opener = new ProjectOpeningService(workspace, recent, settings, output);

        var result = await opener.OpenProjectAsync(Path.Combine(Path.GetTempPath(), "missing", "Martin.toml"));

        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Empty(recent.Items);
        Assert.Contains(workspace.Workspace.Diagnostics, diagnostic => diagnostic.Severity == OutputSeverity.Error);
    }

    [Fact]
    public async Task Invalid_project_open_preserves_current_workspace_tabs_and_dirty_text()
    {
        using var project = new Phase11TempProject("Current");
        var workspace = new WorkspaceService();
        var recent = new RecentProjectService();
        var settings = new InMemorySettingsService();
        var output = new OutputService();
        var opener = new ProjectOpeningService(workspace, recent, settings, output);

        Assert.True((await opener.OpenProjectAsync(project.Root)).Success);
        var document = workspace.OpenDocument(project.Main);
        workspace.ApplyEditorChange(document.Id, "func main() { return 99 }");

        var result = await opener.OpenProjectAsync(Path.Combine(Path.GetTempPath(), "missing", Guid.NewGuid().ToString("N"), "Martin.toml"));

        Assert.False(result.Success);
        Assert.Equal(project.Manifest, workspace.Workspace.Project!.ManifestPath);
        Assert.Same(document, workspace.Workspace.ActiveDocument);
        Assert.Contains(workspace.Workspace.OpenDocuments, d => d.Id == document.Id && d.IsDirty && d.Text.Contains("return 99"));
        Assert.Equal(project.Manifest, settings.Settings.LastProject);
        Assert.Single(recent.Items);
    }

    [Fact]
    public async Task Dirty_project_transition_can_cancel_or_save_before_opening_next_project()
    {
        using var first = new Phase11TempProject("First");
        using var second = new Phase11TempProject("Second");
        var workspace = new WorkspaceService();
        var recent = new RecentProjectService();
        var settings = new InMemorySettingsService();
        var output = new OutputService();
        var dirtyPolicy = new TestDirtyPolicy(DirtyProjectTransitionChoice.Cancel);
        var opener = new ProjectOpeningService(workspace, recent, settings, output, dirtyPolicy);

        Assert.True((await opener.OpenProjectAsync(first.Root)).Success);
        var document = workspace.OpenDocument(first.Main);
        workspace.ApplyEditorChange(document.Id, "func main() { return 42 }");

        var canceled = await opener.OpenProjectAsync(second.Root);
        Assert.False(canceled.Success);
        Assert.Equal(first.Manifest, workspace.Workspace.Project!.ManifestPath);

        dirtyPolicy.Choice = DirtyProjectTransitionChoice.SaveAndContinue;
        var opened = await opener.OpenProjectAsync(second.Root);
        Assert.True(opened.Success);
        Assert.Equal(second.Manifest, workspace.Workspace.Project!.ManifestPath);
        Assert.Contains("return 42", File.ReadAllText(first.Main));
    }

    [Fact]
    public async Task Reopen_last_project_loads_recent_projects_from_settings()
    {
        using var project = new Phase11TempProject();
        var workspace = new WorkspaceService();
        var recent = new RecentProjectService();
        var settings = new InMemorySettingsService
        {
            Settings = new StudioSettings
            {
                LastProject = project.Manifest,
                RecentProjects = [new RecentProjectEntry(project.Manifest, "Saved", DateTimeOffset.UtcNow)]
            }
        };
        var opener = new ProjectOpeningService(workspace, recent, settings, new OutputService());

        var result = await opener.ReopenLastProjectAsync();

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(project.Manifest, workspace.Workspace.Project!.ManifestPath);
        Assert.Single(recent.Items);
    }


    [Fact]
    public void Project_explorer_loads_children_deterministically_hides_generated_and_opens_files()
    {
        using var project = new Phase11TempProject();
        Directory.CreateDirectory(Path.Combine(project.Root, "bin"));
        Directory.CreateDirectory(Path.Combine(project.Root, "Zeta"));
        File.WriteAllText(Path.Combine(project.Root, "readme.txt"), "hello");
        File.WriteAllText(Path.Combine(project.Root, "Zeta", "helper.martin"), "func helper() { return 2 }");

        var workspace = new WorkspaceService();
        Assert.True(workspace.OpenProject(project.Root).Success);
        var root = workspace.Workspace.ProjectTree!;

        Assert.False(root.ChildrenLoaded);
        workspace.LoadProjectTreeChildren(root);

        Assert.True(root.ChildrenLoaded);
        Assert.DoesNotContain(root.Children, child => child.Name == "bin");
        Assert.Equal("Martin.toml", root.Children.First(child => child.Kind is ProjectNodeKind.Manifest or ProjectNodeKind.OtherFile or ProjectNodeKind.MartinSourceFile).Name);
        Assert.True(root.Children.TakeWhile(child => child.Kind == ProjectNodeKind.Folder).Any());

        var readme = root.Children.Single(child => child.Name == "readme.txt");
        Assert.True(workspace.TryOpenProjectTreeNode(readme, out var document));
        Assert.NotNull(document);
        Assert.Equal(readme.FullPath, workspace.CopyProjectTreeNodePath(readme));
        Assert.True(workspace.RevealProjectTreeNode(readme));
    }


    [Fact]
    public void Multiple_document_lifecycle_prevents_duplicates_tracks_active_state_and_disposes_models()
    {
        using var project = new Phase11TempProject();
        var second = Path.Combine(project.Root, "Sources", "second.martin");
        File.WriteAllText(second, "func second() { return 2 }");
        var workspace = new WorkspaceService();
        Assert.True(workspace.OpenProject(project.Root).Success);

        var first = workspace.OpenDocument(project.Main);
        var duplicate = workspace.OpenDocument(project.Main);
        var other = workspace.OpenDocument(second);
        workspace.ApplyEditorChange(first.Id, "func main() { return 42 }");
        workspace.UpdateViewState(first.Id, new EditorViewState { CursorLine = 3, CursorColumn = 4, ScrollTop = 50 });
        workspace.ActivateDocument(first.Id);

        Assert.Same(first, duplicate);
        Assert.Equal(2, workspace.Workspace.OpenDocuments.Count);
        Assert.Equal(first, workspace.Workspace.ActiveDocument);
        Assert.True(first.IsDirty);
        Assert.Equal(3, first.ViewState.CursorLine);

        Assert.False(workspace.CloseDocument(first.Id));
        Assert.True(workspace.CloseDocument(first.Id, force: true));
        Assert.DoesNotContain(workspace.Workspace.OpenDocuments, document => document.Id == first.Id);
        Assert.Equal(other, workspace.Workspace.ActiveDocument);
    }

    [Fact]
    public void Document_navigation_opens_existing_document_and_restores_selection_state()
    {
        using var project = new Phase11TempProject();
        var workspace = new WorkspaceService();
        Assert.True(workspace.OpenProject(project.Root).Success);

        Assert.True(workspace.TryNavigateToDocument(project.Main, new TextRange(2, 3, 2, 8), out var document));

        Assert.NotNull(document);
        Assert.Equal(document, workspace.Workspace.ActiveDocument);
        Assert.Equal(2, document.ViewState.CursorLine);
        Assert.Equal(3, document.ViewState.CursorColumn);
        Assert.Single(document.ViewState.Selections);
    }


    [Fact]
    public async Task Document_persistence_preserves_encoding_line_endings_and_save_as_updates_model()
    {
        using var project = new Phase11TempProject();
        var workspace = new WorkspaceService();
        Assert.True(workspace.OpenProject(project.Root).Success);
        await File.WriteAllBytesAsync(project.Main, [0xEF, 0xBB, 0xBF, ..System.Text.Encoding.UTF8.GetBytes("func main() {\r\n return 1\r\n}")]);

        var document = workspace.OpenDocument(project.Main);
        workspace.ApplyEditorChange(document.Id, "func main() {\n return 2\n}");
        await workspace.SaveAsync(document);

        var saved = await File.ReadAllBytesAsync(project.Main);
        Assert.Equal([0xEF, 0xBB, 0xBF], saved.Take(3).ToArray());
        Assert.Contains("\r\n return 2\r\n", System.Text.Encoding.UTF8.GetString(saved));
        Assert.False(document.IsDirty);

        workspace.ApplyEditorChange(document.Id, "func renamed() {\n return 3\n}");
        var saveAsPath = Path.Combine(project.Root, "Sources", "renamed.martin");
        var saveAs = await workspace.SaveAsAsync(document, saveAsPath);

        Assert.True(saveAs.Success);
        Assert.Equal(saveAsPath, document.FilePath);
        Assert.Equal("renamed.martin", document.DisplayName);
        Assert.Contains("renamed", await File.ReadAllTextAsync(saveAsPath));
    }

    [Fact]
    public async Task Document_persistence_reports_read_only_and_deleted_files_without_clearing_dirty_state()
    {
        using var project = new Phase11TempProject();
        var workspace = new WorkspaceService();
        Assert.True(workspace.OpenProject(project.Root).Success);
        var document = workspace.OpenDocument(project.Main);

        document.FilePath = "\0";
        try
        {
            workspace.ApplyEditorChange(document.Id, "func main() { return 5 }");
            var result = await workspace.SaveAllWithResultsAsync();

            Assert.False(result.Success);
            Assert.True(document.IsDirty);
            Assert.True(document.IsReadOnly);
            Assert.Contains("read-only", result.Results.Single().Error);
        }
        finally
        {
            File.SetAttributes(project.Main, File.GetAttributes(project.Main) & ~FileAttributes.ReadOnly);
        }

        File.Delete(project.Main);
        var deletedResult = await workspace.SaveAsAsync(document, Path.Combine(project.Root, "Missing", "main.martin"));

        Assert.True(deletedResult.Success);
        Assert.False(document.IsDeleted);
        Assert.False(document.IsDirty);
    }


    [Fact]
    public async Task Atomic_settings_and_session_services_ignore_corrupt_json_and_persist_versioned_state()
    {
        var root = Path.Combine(Path.GetTempPath(), "MartinPhase11SettingsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var settingsPath = Path.Combine(root, "settings.json");
        var sessionPath = Path.Combine(root, "session.json");
        await File.WriteAllTextAsync(settingsPath, "{");
        await File.WriteAllTextAsync(sessionPath, "{");

        var settingsService = new JsonSettingsService(settingsPath);
        var sessionService = new JsonSessionService(sessionPath);

        Assert.Equal(StudioTheme.System, (await settingsService.LoadAsync()).Theme);
        Assert.Empty((await sessionService.LoadAsync()).OpenDocuments);

        await settingsService.SaveAsync(new StudioSettings { Theme = StudioTheme.Dark, MaximumRecentProjects = 3 });
        await sessionService.SaveAsync(new StudioSession
        {
            ProjectPath = Path.Combine(root, "Martin.toml"),
            OpenDocuments = [new DocumentSessionState(Path.Combine(root, "main.martin"), new EditorViewState { CursorLine = 7, CursorColumn = 2 })],
            ActiveDocument = Path.Combine(root, "main.martin")
        });

        Assert.Equal(1, (await settingsService.LoadAsync()).SchemaVersion);
        Assert.Equal(StudioTheme.Dark, (await settingsService.LoadAsync()).Theme);
        Assert.Equal(7, (await sessionService.LoadAsync()).OpenDocuments.Single().ViewState.CursorLine);
    }

    [Fact]
    public async Task Reopen_last_project_restores_open_documents_active_document_and_view_state_from_session()
    {
        using var project = new Phase11TempProject();
        var second = Path.Combine(project.Root, "Sources", "second.martin");
        File.WriteAllText(second, "func second() { return 2 }");
        var workspace = new WorkspaceService();
        var recent = new RecentProjectService();
        var settings = new InMemorySettingsService
        {
            Settings = new StudioSettings
            {
                LastProject = project.Manifest,
                RestoreOpenDocuments = true,
                RecentProjects = [new RecentProjectEntry(project.Manifest, "Saved", DateTimeOffset.UtcNow)]
            }
        };
        var session = new InMemorySessionService
        {
            Session = new StudioSession
            {
                ProjectPath = project.Manifest,
                OpenDocuments =
                [
                    new DocumentSessionState(project.Main, new EditorViewState { CursorLine = 4, CursorColumn = 5 }),
                    new DocumentSessionState(second, new EditorViewState { CursorLine = 6, CursorColumn = 7 })
                ],
                ActiveDocument = second
            }
        };
        var opener = new ProjectOpeningService(workspace, recent, settings, new OutputService(), null, session);

        var result = await opener.ReopenLastProjectAsync();

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(2, workspace.Workspace.OpenDocuments.Count);
        Assert.Equal(second, workspace.Workspace.ActiveDocument!.FilePath);
        Assert.Equal(6, workspace.Workspace.ActiveDocument.ViewState.CursorLine);
        Assert.Equal(0, session.SaveCount);
    }

    [Fact]
    public async Task Reopen_last_project_ignores_empty_session_paths()
    {
        using var project = new Phase11TempProject();
        var workspace = new WorkspaceService();
        var recent = new RecentProjectService();
        var settings = new InMemorySettingsService
        {
            Settings = new StudioSettings
            {
                LastProject = project.Manifest,
                RestoreOpenDocuments = true
            }
        };
        var session = new InMemorySessionService
        {
            Session = new StudioSession
            {
                ProjectPath = string.Empty,
                OpenDocuments = [new DocumentSessionState(string.Empty, new EditorViewState { CursorLine = 4, CursorColumn = 5 })],
                ActiveDocument = string.Empty
            }
        };
        var opener = new ProjectOpeningService(workspace, recent, settings, new OutputService(), null, session);

        var result = await opener.ReopenLastProjectAsync();

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Empty(workspace.Workspace.OpenDocuments);
    }

    [Fact]
    public async Task View_model_loads_and_persists_theme_without_reopening_workspace()
    {
        var workspace = new WorkspaceService();
        var output = new OutputService();
        var settings = new InMemorySettingsService { Settings = new StudioSettings { Theme = StudioTheme.Light } };
        var viewModel = new MainWindowViewModel(
            workspace,
            new ProjectExplorerViewModel(),
            new DocumentTabsViewModel(),
            new OutputPaneViewModel(output),
            new ErrorListViewModel(),
            new StatusBarViewModel(),
            output,
            new TestProjectOpeningService(),
            settingsService: settings);

        await viewModel.LoadSettingsAsync();
        Assert.Equal(StudioTheme.Light, viewModel.CurrentTheme);

        await viewModel.SetThemeCommand.ExecuteAsync(StudioTheme.Dark);

        Assert.Equal(StudioTheme.Dark, settings.Settings.Theme);
        Assert.Null(workspace.Workspace.Project);
        Assert.Contains(output.Entries, entry => entry.Message.Contains("Theme changed to Dark", StringComparison.Ordinal));
    }

    private sealed class InMemorySettingsService : ISettingsService
    {
        public StudioSettings Settings { get; set; } = new();
        public Task<StudioSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Settings);
        public Task SaveAsync(StudioSettings settings, CancellationToken cancellationToken = default)
        {
            Settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySessionService : ISessionService
    {
        public StudioSession Session { get; set; } = new();
        public int SaveCount { get; private set; }
        public Task<StudioSession> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Session);
        public Task SaveAsync(StudioSession session, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            Session = session;
            return Task.CompletedTask;
        }
    }

    private sealed class TestDirtyPolicy(DirtyProjectTransitionChoice choice) : IDirtyProjectTransitionPolicy
    {
        public DirtyProjectTransitionChoice Choice { get; set; } = choice;
        public Task<DirtyProjectTransitionChoice> ConfirmAsync(IReadOnlyList<DocumentModel> dirtyDocuments, CancellationToken cancellationToken = default) => Task.FromResult(Choice);
    }

    public sealed class Phase11TempProject : IDisposable
    {
        public string Root { get; }
        public string Manifest => Path.Combine(Root, "Martin.toml");
        public string Main => Path.Combine(Root, "Sources", "main.martin");

        public Phase11TempProject(string name = "P")
        {
            Root = Path.Combine(Path.GetTempPath(), "MartinPhase11ProjectOpeningTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "Sources"));
            File.WriteAllText(Manifest, $"manifest-version = 1\n\n[package]\nname = \"{name}\"\nversion = \"0.1.0\"\n\n[target]\nkind = \"executable\"\nframework = \"net8.0\"\nentry = \"main\"\n");
            File.WriteAllText(Main, "func main() { return 1 }");
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
    }
}

public sealed class Phase11EditorBridgeTests
{
    [Fact]
    public void Editor_bridge_validates_message_size_and_required_type()
    {
        Assert.Null(EditorMessageProtocol.Deserialize("{}"));
        var large = "{\"type\":\"editorReady\",\"payload\":{\"monacoVersion\":\"" + new string('x', EditorMessageProtocol.MaxMessageBytes) + "\"}}";
        var result = EditorMessageProtocol.TryDeserialize(large);
        Assert.False(result.IsValid);
        Assert.Contains("maximum size", result.Error);
    }

    [Fact]
    public void Editor_bridge_accepts_ready_errors_and_text_changes()
    {
        var bridge = new EditorBridge();
        EditorReadyPayload? ready = null;
        EditorTextChangedPayload? changed = null;
        bridge.Ready += (_, payload) => ready = payload;
        bridge.TextChanged += (_, payload) => changed = payload;

        bridge.AcceptHostMessage(EditorMessageProtocol.Serialize("editorReady", new EditorReadyPayload("0.52.2")));
        var documentId = Guid.NewGuid();
        bridge.AcceptHostMessage(EditorMessageProtocol.Serialize("textChanged", new EditorTextChangedPayload(documentId, 1, 2, [new(0, 0, "func main() {}")])));

        Assert.Equal(EditorBridgeState.Ready, bridge.State);
        Assert.Equal("0.52.2", ready!.MonacoVersion);
        Assert.Equal(2, changed!.NewVersion);
    }

    [Fact]
    public async Task Editor_bridge_correlates_responses_and_cancels_pending_requests_on_recovery()
    {
        var bridge = new EditorBridge { DefaultTimeout = TimeSpan.FromSeconds(30) };
        var documentId = Guid.NewGuid();
        var (_, _, response) = bridge.CreateRequest("requestText", new EditorRequestTextPayload(documentId));
        bridge.AcceptHostMessage(EditorMessageProtocol.Serialize("response", new EditorTextResponsePayload(documentId, "answer", 3), "1"));

        var payload = await response;
        var text = System.Text.Json.JsonSerializer.Deserialize<EditorTextResponsePayload>(payload.GetRawText(), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Equal("answer", text!.Text);

        var (_, _, abandoned) = bridge.CreateRequest("requestText", new EditorRequestTextPayload(documentId));
        bridge.Recover();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await abandoned);
    }

    [Fact]
    public async Task Editor_bridge_rejects_malformed_payload_for_correlated_response()
    {
        var bridge = new EditorBridge { DefaultTimeout = TimeSpan.FromSeconds(30) };
        var (_, _, response) = bridge.CreateRequest("requestText", new EditorRequestTextPayload(Guid.NewGuid()));

        var validation = bridge.AcceptHostMessage(EditorMessageProtocol.Serialize("response", new { text = 42 }, "1"));

        Assert.False(validation.IsValid);
        Assert.Contains("documentId", validation.Error);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await response);
    }

    [Fact]
    public void Studio_session_tracks_explorer_visibility_and_window_position()
    {
        var session = new StudioSession
        {
            ProjectExplorerVisible = false,
            WindowPlacement = new WindowPlacementState(1000, 700, true, 125, 250)
        };

        Assert.False(session.ProjectExplorerVisible);
        Assert.Equal(125, session.WindowPlacement.X);
        Assert.Equal(250, session.WindowPlacement.Y);
    }
}

public sealed class Phase11ExternalChangeTests
{
    [Fact]
    public async Task Clean_external_change_can_reload_from_disk_and_update_hash_identity()
    {
        using var project = new Phase11ExternalTempProject();
        var policy = new TestExternalChangePolicy { CleanChoice = CleanExternalChangeChoice.Reload };
        var workspace = new WorkspaceService(policy);
        Assert.True(workspace.OpenProject(project.Root).Success);
        var document = workspace.OpenDocument(project.Main);
        DocumentModel? reloadedDocument = null;
        workspace.DocumentReloaded += (reloaded, _) => { reloadedDocument = reloaded; return Task.CompletedTask; };

        File.WriteAllText(project.Main, "func main() { return 99 }");
        await workspace.HandleExternalChangesAsync();

        Assert.Equal("func main() { return 99 }", document.Text);
        Assert.False(document.IsDirty);
        Assert.False(document.HasExternalChanges);
        Assert.NotNull(document.SavedContentHash);
        Assert.Single(policy.CleanChanges);
        Assert.Same(document, reloadedDocument);
    }

    [Fact]
    public async Task Dirty_external_change_blocks_save_until_user_keeps_editor_version()
    {
        using var project = new Phase11ExternalTempProject();
        var policy = new TestExternalChangePolicy { DirtyChoice = DirtyExternalChangeChoice.Cancel };
        var workspace = new WorkspaceService(policy);
        Assert.True(workspace.OpenProject(project.Root).Success);
        var document = workspace.OpenDocument(project.Main);

        workspace.ApplyEditorChange(document.Id, "func main() { return 2 }");
        File.WriteAllText(project.Main, "func main() { return 3 }");

        var blocked = await workspace.SaveAllWithResultsAsync();
        Assert.False(blocked.Success);
        Assert.True(document.IsDirty);
        Assert.True(document.HasExternalChanges);
        Assert.Contains("External changes", blocked.Results.Single().Error);

        await workspace.HandleExternalChangesAsync();
        policy.DirtyChoice = DirtyExternalChangeChoice.KeepEditorVersion;
        await workspace.HandleExternalChangesAsync();
        var saved = await workspace.SaveAllWithResultsAsync();

        Assert.True(saved.Success);
        Assert.Contains("return 2", File.ReadAllText(project.Main));
        Assert.False(document.IsDirty);
    }

    [Fact]
    public async Task Dirty_deleted_document_can_be_saved_as_without_losing_editor_text()
    {
        using var project = new Phase11ExternalTempProject();
        var saveAs = Path.Combine(project.Root, "Sources", "saved-as.martin");
        var policy = new TestExternalChangePolicy { DirtyChoice = DirtyExternalChangeChoice.SaveAs, SaveAsPath = saveAs };
        var workspace = new WorkspaceService(policy);
        Assert.True(workspace.OpenProject(project.Root).Success);
        var document = workspace.OpenDocument(project.Main);

        workspace.ApplyEditorChange(document.Id, "func main() { return 7 }");
        File.Delete(project.Main);
        await workspace.HandleExternalChangesAsync();

        Assert.Equal(saveAs, document.FilePath);
        Assert.False(document.IsDeleted);
        Assert.False(document.IsDirty);
        Assert.Contains("return 7", File.ReadAllText(saveAs));
        Assert.Equal(ExternalChangeKind.Deleted, policy.DirtyChanges.Single().Kind);
    }

    private sealed class TestExternalChangePolicy : IExternalChangePolicy
    {
        public CleanExternalChangeChoice CleanChoice { get; set; } = CleanExternalChangeChoice.KeepCurrentView;
        public DirtyExternalChangeChoice DirtyChoice { get; set; } = DirtyExternalChangeChoice.Cancel;
        public string? SaveAsPath { get; set; }
        public List<ExternalChange> CleanChanges { get; } = [];
        public List<ExternalChange> DirtyChanges { get; } = [];

        public Task<CleanExternalChangeChoice> ConfirmCleanChangeAsync(ExternalChange change, CancellationToken cancellationToken = default)
        {
            CleanChanges.Add(change);
            return Task.FromResult(CleanChoice);
        }

        public Task<DirtyExternalChangeChoice> ConfirmDirtyChangeAsync(ExternalChange change, CancellationToken cancellationToken = default)
        {
            DirtyChanges.Add(change);
            return Task.FromResult(DirtyChoice);
        }

        public Task<string?> GetSaveAsPathAsync(DocumentModel document, CancellationToken cancellationToken = default) => Task.FromResult(SaveAsPath);
    }

    private sealed class Phase11ExternalTempProject : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "MartinPhase11ExternalChangeTests", Guid.NewGuid().ToString("N"));
        public string Manifest => Path.Combine(Root, "Martin.toml");
        public string Main => Path.Combine(Root, "Sources", "main.martin");

        public Phase11ExternalTempProject()
        {
            Directory.CreateDirectory(Path.Combine(Root, "Sources"));
            File.WriteAllText(Manifest, "manifest-version = 1\n\n[package]\nname = \"External\"\nversion = \"0.1.0\"\n\n[target]\nkind = \"executable\"\nframework = \"net8.0\"\nentry = \"main\"\n");
            File.WriteAllText(Main, "func main() { return 1 }");
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
    }
}

public sealed class Phase11SnapshotTests
{
    [Fact]
    public void Snapshot_prefers_open_editor_text_reports_closed_source_read_diagnostics_and_is_immutable()
    {
        using var project = new SnapshotTempProject(extra: true);
        var workspace = new WorkspaceService();
        Assert.True(workspace.OpenProject(project.Root).Success);
        var document = workspace.OpenDocument(project.Main);
        workspace.ApplyEditorChange(document.Id, "func main() { return 42 }");
        File.WriteAllText(project.Main, "func main() { return 1 }");
        File.Delete(project.Extra);
        File.WriteAllText(Path.Combine(project.Root, "README.md"), "# Not source");
        workspace.OpenDocument(Path.Combine(project.Root, "README.md"));
        workspace.OpenDocument(project.Manifest);

        var snapshot = workspace.CreateSnapshot();
        workspace.ApplyEditorChange(document.Id, "func main() { return 100 }");

        Assert.Contains(snapshot.Sources, source => source.FilePath == project.Main && source.Text.Contains("return 42") && source.Version == new DocumentVersion(1));
        Assert.DoesNotContain(snapshot.Sources, source => source.FilePath == project.Extra);
        Assert.Contains(snapshot.Diagnostics, diagnostic => diagnostic.Code == "MRT5201" && diagnostic.FilePath == project.Extra && diagnostic.Source == "StudioSnapshot");
        Assert.Contains(snapshot.Sources, source => source.FilePath == project.Main && source.Text.Contains("return 42"));
        Assert.DoesNotContain(snapshot.Sources, source => source.FilePath.EndsWith("README.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(snapshot.Sources, source => source.FilePath == project.Manifest);
        Assert.DoesNotContain(snapshot.Sources, source => source.Text.Contains("return 100"));
    }

    [Fact]
    public void Snapshot_honors_cancellation_before_reading_sources()
    {
        using var project = new SnapshotTempProject();
        var workspace = new WorkspaceService();
        Assert.True(workspace.OpenProject(project.Root).Success);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => workspace.CreateSnapshot(cts.Token));
    }


    [Fact]
    public void Snapshot_reports_duplicate_canonical_project_source_paths()
    {
        using var project = new SnapshotTempProject();
        var workspace = new WorkspaceService();
        Assert.True(workspace.OpenProject(project.Root).Success);
        var loaded = workspace.Workspace.Project!;
        workspace.CommitProjectCandidate(new ProjectCandidate
        {
            Project = new MartinProject
            {
                RootDirectory = loaded.RootDirectory,
                ManifestPath = loaded.ManifestPath,
                Manifest = loaded.Manifest,
                SourceFiles = [project.Main, Path.Combine(project.Root, "Sources", "..", "Sources", "main.martin")],
                TestFiles = loaded.TestFiles
            },
            RootNode = new ProjectTreeNode { Name = "Snapshots", FullPath = project.Root, Kind = ProjectNodeKind.Project }
        });

        var snapshot = workspace.CreateSnapshot();

        Assert.Single(snapshot.Sources);
        Assert.Contains(snapshot.Diagnostics, diagnostic => diagnostic.Code == "MRT5013" && diagnostic.FilePath == project.Main);
        Assert.True(snapshot.HasSourceReadErrors);
    }

    private sealed class SnapshotTempProject : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "MartinPhase11SnapshotTests", Guid.NewGuid().ToString("N"));
        public string Manifest => Path.Combine(Root, "Martin.toml");
        public string Main => Path.Combine(Root, "Sources", "main.martin");
        public string Extra => Path.Combine(Root, "Sources", "extra.martin");

        public SnapshotTempProject(bool extra = false)
        {
            Directory.CreateDirectory(Path.Combine(Root, "Sources"));
            File.WriteAllText(Manifest, "manifest-version = 1\n\n[package]\nname = \"Snapshots\"\nversion = \"0.1.0\"\n\n[target]\nkind = \"executable\"\nframework = \"net8.0\"\nentry = \"main\"\n");
            File.WriteAllText(Main, "func main() { return 1 }");
            if (extra)
                File.WriteAllText(Extra, "func extra() { return 2 }");
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
    }
}


public sealed class Phase12BuildCommandTests
{
    [Fact]
    public async Task Build_command_uses_snapshot_updates_output_diagnostics_and_state()
    {
        using var project = new Phase12TempProject();
        var workspace = new WorkspaceService();
        Assert.True(workspace.OpenProject(project.Root).Success);
        var document = workspace.OpenDocument(project.Main);
        workspace.ApplyEditorChange(document.Id, "func main() { return 42 }");
        var output = new OutputService();
        var build = new FakeBuildService(new BuildResult
        {
            Success = false,
            Diagnostics = [new Diagnostic("MRT9999", DiagnosticSeverity.Error, "boom", new TextLocation(SourceText.From("func main() {", project.Main), new TextSpan(5, 4)))],
            StandardOutput = "compiler output"
        });
        var coordinator = new BuildCoordinator(workspace.Workspace, output, build);

        var result = await coordinator.BuildAsync();

        Assert.False(result.Success);
        Assert.Equal(BuildState.Idle, workspace.Workspace.BuildState);
        Assert.Contains("return 42", build.LastCompilation!.SyntaxTrees.Single().Text.ToString());
        Assert.Contains(output.Entries, entry => entry.Channel == OutputChannel.Build && entry.Message.Contains("compiler output"));
        Assert.Equal(Path.Combine(project.Root, "bin", "Debug", "net8.0"), build.LastOptions!.OutputDirectory);
        Assert.Equal(BuildConfiguration.Debug, build.LastOptions.Configuration);
        Assert.False(string.IsNullOrWhiteSpace(build.LastOptions.BuildStateInputs!.RuntimeVersion));
        Assert.Contains(workspace.Workspace.Diagnostics, diagnostic => diagnostic.Code == "MRT9999" && diagnostic.FilePath == project.Main && diagnostic.Range is not null && diagnostic.DocumentId == document.Id && diagnostic.Version == document.Version && diagnostic.Generation == workspace.Workspace.Generation);
    }

    [Fact]
    public async Task Clean_command_removes_only_active_configuration_outputs_and_reports_output()
    {
        using var project = new Phase12TempProject();
        var debugOutput = Path.Combine(project.Root, "bin", "Debug", "net8.0");
        var releaseOutput = Path.Combine(project.Root, "bin", "Release", "net8.0");
        var debugIntermediate = Path.Combine(project.Root, "obj", "Debug", "net8.0");
        Directory.CreateDirectory(debugOutput);
        Directory.CreateDirectory(releaseOutput);
        Directory.CreateDirectory(debugIntermediate);
        File.WriteAllText(Path.Combine(debugOutput, "old.dll"), "old");
        File.WriteAllText(Path.Combine(releaseOutput, "keep.dll"), "keep");
        var workspace = new WorkspaceService();
        Assert.True(workspace.OpenProject(project.Root).Success);
        var output = new OutputService();
        var coordinator = new BuildCoordinator(workspace.Workspace, output, new FakeBuildService(new BuildResult { Success = true }));

        var result = await coordinator.CleanAsync();

        Assert.True(result.Success);
        Assert.False(Directory.Exists(debugOutput));
        Assert.False(Directory.Exists(debugIntermediate));
        Assert.True(Directory.Exists(releaseOutput));
        Assert.Contains(output.Entries, entry => entry.Channel == OutputChannel.Build && entry.Message == "Clean succeeded.");
    }

    [Fact]
    public async Task Build_configuration_selection_updates_workspace_and_build()
    {
        using var project = new Phase12TempProject();
        var workspace = new WorkspaceService();
        Assert.True(workspace.OpenProject(project.Root).Success);
        var output = new OutputService();
        var build = new FakeBuildService(new BuildResult { Success = true });
        var viewModel = new MainWindowViewModel(
            workspace, new ProjectExplorerViewModel(), new DocumentTabsViewModel(),
            new OutputPaneViewModel(output), new ErrorListViewModel(), new StatusBarViewModel(),
            output, new TestProjectOpeningService(),
            buildCoordinator: new BuildCoordinator(workspace.Workspace, output, build));

        viewModel.ActiveBuildConfiguration = BuildConfiguration.Release;
        await viewModel.BuildCommand.ExecuteAsync(null);

        Assert.Equal(BuildConfiguration.Release, workspace.Workspace.ActiveBuildConfiguration);
        Assert.Equal(BuildConfiguration.Release, build.LastOptions!.Configuration);
        Assert.Equal(Path.Combine(project.Root, "bin", "Release", "net8.0"), build.LastOptions.OutputDirectory);
    }


    [Fact]
    public async Task Run_command_builds_in_memory_dirty_documents_then_executes()
    {
        using var project = new Phase12TempProject();
        var workspace = new WorkspaceService();
        Assert.True(workspace.OpenProject(project.Root).Success);
        var output = new OutputService();
        var buildResult = new BuildResult { Success = true, EntryPointPath = Path.Combine(project.Root, "bin", "app.dll"), OutputDirectory = Path.Combine(project.Root, "bin") };
        var build = new FakeBuildService(buildResult);
        var execution = new FakeExecutionService();
        var viewModel = new MainWindowViewModel(
            workspace,
            new ProjectExplorerViewModel(),
            new DocumentTabsViewModel(),
            new OutputPaneViewModel(output),
            new ErrorListViewModel(),
            new StatusBarViewModel(),
            output,
            new TestProjectOpeningService(),
            buildCoordinator: new BuildCoordinator(workspace.Workspace, output, build),
            executionCoordinator: new ExecutionCoordinator(workspace.Workspace, output, execution));

        await viewModel.RunCommand.ExecuteAsync(null);

        Assert.NotNull(build.LastCompilation);
        Assert.True(execution.WasRun);
        Assert.Contains(output.Entries, entry => entry.Channel == OutputChannel.Program && entry.Message.Contains("Program exited"));

        var document = workspace.OpenDocument(project.Main);
        workspace.ApplyEditorChange(document.Id, "func main() { return 99 }");
        viewModel.RefreshCommandState();
        Assert.True(viewModel.RunCommand.CanExecute(null));
        Assert.False(viewModel.RunWithoutBuildCommand.CanExecute(null));

        await viewModel.RunCommand.ExecuteAsync(null);

        Assert.Equal(2, execution.RunCount);
        Assert.Contains("return 99", build.LastCompilation!.SyntaxTrees.Single().Text.ToString());
    }

    private sealed class FakeExecutionService : IMartinExecutionService
    {
        public bool WasRun { get; private set; }
        public int RunCount { get; private set; }
        public Task<ExecutionResult> RunAsync(BuildResult build, ExecutionOptions options, CancellationToken cancellationToken = default)
        {
            WasRun = true;
            RunCount++;
            return Task.FromResult(new ExecutionResult { Status = ExecutionStatus.Completed, ExitCode = 0, StandardOutput = "hello" });
        }
    }

    private sealed class FakeBuildService(BuildResult result) : IMartinBuildService
    {
        public Compilation? LastCompilation { get; private set; }
        public BuildOptions? LastOptions { get; private set; }
        public Task<BuildResult> BuildAsync(Compilation compilation, BuildOptions options, CancellationToken cancellationToken = default)
        {
            LastCompilation = compilation;
            LastOptions = options;
            return Task.FromResult(result);
        }
    }

    private sealed class Phase12TempProject : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "MartinPhase12BuildTests", Guid.NewGuid().ToString("N"));
        public string Manifest => Path.Combine(Root, "Martin.toml");
        public string Main => Path.Combine(Root, "Sources", "main.martin");

        public Phase12TempProject()
        {
            Directory.CreateDirectory(Path.Combine(Root, "Sources"));
            File.WriteAllText(Manifest, "manifest-version = 1\n\n[package]\nname = \"BuildCommands\"\nversion = \"0.1.0\"\n\n[target]\nkind = \"executable\"\nframework = \"net8.0\"\nentry = \"main\"\n\n[build]\noutput = \"bin\"\nintermediate = \"obj\"\n");
            File.WriteAllText(Main, "func main() { return 1 }");
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
    }
}

public sealed class Phase14OutputAndErrorListTests
{
    [Fact]
    public async Task Output_is_bounded_filterable_copyable_and_can_be_saved()
    {
        var output = new OutputService(2);
        output.Add(OutputChannel.Studio, OutputSeverity.Info, "startup");
        output.Add(OutputChannel.Build, OutputSeverity.Warning, "warn");
        output.Add(OutputChannel.Program, OutputSeverity.Error, "boom");

        Assert.DoesNotContain(output.Entries, entry => entry.Message == "startup");
        Assert.Single(output.GetEntries(new OutputFilter(Channels: [OutputChannel.Program], Severities: [OutputSeverity.Error])));
        Assert.Contains("[Program] [Error] boom", output.CopyAll());

        var log = Path.Combine(Path.GetTempPath(), "MartinPhase14Output", Guid.NewGuid().ToString("N"), "studio.log");
        await output.SaveLogAsync(log, new OutputFilter(Text: "boom"));
        Assert.Contains("boom", await File.ReadAllTextAsync(log));
    }

    [Fact]
    public void Error_list_filters_sorts_copies_and_navigates_diagnostics()
    {
        using var project = new Phase14TempProject();
        var workspace = new WorkspaceService();
        Assert.True(workspace.OpenProject(project.Root).Success);
        var document = workspace.OpenDocument(project.Main);
        var service = new DiagnosticService(workspace.Workspace);
        service.Apply(new DocumentDiagnostics(document.Id, document.Version,
        [
            new StudioDiagnostic("MRT2000", OutputSeverity.Warning, "later", document.FilePath, new TextRange(4, 2, 4, 6), "Compiler"),
            new StudioDiagnostic("MRT1000", OutputSeverity.Error, "first", document.FilePath, new TextRange(2, 3, 2, 8), "Compiler"),
            new StudioDiagnostic("MRT3000", OutputSeverity.Info, "note", null, null, "Build")
        ]));

        var errors = service.GetDiagnostics(new DiagnosticFilter(Severities: [OutputSeverity.Error]));
        Assert.Single(errors);
        Assert.Equal("MRT1000", errors[0].Code);
        Assert.Contains("MRT1000", service.CopyAll());
        Assert.Equal("MRT1000", service.Next()!.Code);
        Assert.Equal("MRT2000", service.Next()!.Code);
        Assert.Equal("MRT1000", service.Previous()!.Code);

        var viewModel = new ErrorListViewModel();
        viewModel.Refresh(workspace.Workspace.Diagnostics);
        viewModel.SetFilter(new DiagnosticFilter(Text: "later"));
        Assert.Single(viewModel.Diagnostics);
        Assert.Equal("MRT2000", viewModel.Next()!.Code);
    }

    [Fact]
    public void Diagnostic_navigation_commands_open_next_and_previous_source_ranges()
    {
        using var project = new Phase14TempProject();
        var workspace = new WorkspaceService();
        Assert.True(workspace.OpenProject(project.Root).Success);
        var document = workspace.OpenDocument(project.Main);
        var diagnostics = new DiagnosticService(workspace.Workspace);
        diagnostics.Apply(new DocumentDiagnostics(document.Id, document.Version,
        [
            new StudioDiagnostic("MRT1000", OutputSeverity.Error, "first", document.FilePath, new TextRange(2, 3, 2, 8), "Compiler"),
            new StudioDiagnostic("MRT2000", OutputSeverity.Warning, "second", document.FilePath, new TextRange(5, 1, 5, 4), "Compiler")
        ]));
        var output = new OutputService();
        var viewModel = new MainWindowViewModel(
            workspace,
            new ProjectExplorerViewModel(),
            new DocumentTabsViewModel(),
            new OutputPaneViewModel(output),
            new ErrorListViewModel(),
            new StatusBarViewModel(),
            output,
            new TestProjectOpeningService(),
            diagnosticService: diagnostics);
        viewModel.RefreshCommandState();

        viewModel.NextDiagnosticCommand.Execute(null);
        Assert.Equal(2, workspace.Workspace.ActiveDocument!.ViewState.CursorLine);
        viewModel.NextDiagnosticCommand.Execute(null);
        Assert.Equal(5, workspace.Workspace.ActiveDocument!.ViewState.CursorLine);
        viewModel.PreviousDiagnosticCommand.Execute(null);
        Assert.Equal(2, workspace.Workspace.ActiveDocument!.ViewState.CursorLine);
    }

    private sealed class Phase14TempProject : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "MartinPhase14Tests", Guid.NewGuid().ToString("N"));
        public string Manifest => Path.Combine(Root, "Martin.toml");
        public string Main => Path.Combine(Root, "Sources", "main.martin");

        public Phase14TempProject()
        {
            Directory.CreateDirectory(Path.Combine(Root, "Sources"));
            File.WriteAllText(Manifest, "manifest-version = 1\n\n[package]\nname = \"Phase14\"\nversion = \"0.1.0\"\n\n[target]\nkind = \"executable\"\nframework = \"net8.0\"\nentry = \"main\"\n");
            File.WriteAllText(Main, "func main() {\n return 1\n}\n");
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
    }
}

public sealed class Phase11ShortcutAccessibilityTests
{
    [Fact]
    public void View_model_exposes_milestone16_document_shortcut_commands()
    {
        using var project = new Phase11ProjectOpeningTests.Phase11TempProject();
        var workspace = new WorkspaceService();
        Assert.True(workspace.OpenProject(project.Root).Success);
        var document = workspace.OpenDocument(project.Main);
        workspace.ApplyEditorChange(document.Id, "func main() { return 16 }");
        var output = new OutputService();
        var viewModel = new MainWindowViewModel(workspace, new ProjectExplorerViewModel(), new DocumentTabsViewModel(), new OutputPaneViewModel(output), new ErrorListViewModel(), new StatusBarViewModel(), output, new TestProjectOpeningService());
        viewModel.RefreshCommandState();

        Assert.True(viewModel.SaveCommand.CanExecute(null));
        Assert.True(viewModel.SaveAllCommand.CanExecute(null));
        Assert.True(viewModel.CloseActiveDocumentCommand.CanExecute(null));
        Assert.False(viewModel.NextDocumentCommand.CanExecute(null));
    }

    [Fact]
    public void Diagnostic_accessible_text_includes_non_color_state_and_location()
    {
        var errorList = new ErrorListViewModel();
        errorList.Refresh([new StudioDiagnostic("MRT0016", OutputSeverity.Error, "Shortcut failed", "main.martin", new TextRange(4, 2, 4, 9), "Accessibility")]);

        var diagnostic = Assert.Single(errorList.Diagnostics);
        Assert.Contains("Error MRT0016", diagnostic.AccessibleText);
        Assert.Contains("line 4, column 2", diagnostic.AccessibleText);
        Assert.Contains("Source: Accessibility", diagnostic.AccessibleText);
    }
}

internal sealed class InMemorySettingsService18 : ISettingsService
{
    public StudioSettings Settings { get; set; } = new();
    public Task<StudioSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Settings);
    public Task SaveAsync(StudioSettings settings, CancellationToken cancellationToken = default) { Settings = settings; return Task.CompletedTask; }
}

public sealed class Phase17LoggingRecoveryTests
{
    [Fact]
    public void Structured_log_service_writes_jsonl_entries_without_source_payloads()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MartinPhase17Logs", Guid.NewGuid().ToString("N"));
        var logs = new FileStudioLogService(directory);

        logs.Log(StudioLogCategory.EditorBridge, OutputSeverity.Warning, "request-timeout", "Editor request timed out.", "requestId=42");

        Assert.True(File.Exists(logs.CurrentLogPath));
        var entry = Assert.Single(logs.ReadCurrent());
        Assert.Equal(StudioLogCategory.EditorBridge, entry.Category);
        Assert.Equal("request-timeout", entry.Operation);
        Assert.Contains("Editor request timed out", File.ReadAllText(logs.CurrentLogPath));
    }


    [Fact]
    public async Task Project_opening_and_save_operations_are_logged_without_source_text()
    {
        using var project = new Phase11ProjectOpeningTests.Phase11TempProject();
        var logs = new FileStudioLogService(Path.Combine(Path.GetTempPath(), "MartinPhase17WorkflowLogs", Guid.NewGuid().ToString("N")));
        var workspace = new WorkspaceService(logService: logs);
        var recent = new RecentProjectService();
        var settings = new InMemorySettingsService18();
        var output = new OutputService();
        var opener = new ProjectOpeningService(workspace, recent, settings, output, logService: logs);

        var result = await opener.OpenProjectAsync(project.Root);
        var document = workspace.OpenDocument(project.Main);
        workspace.ApplyEditorChange(document.Id, "func main() { return 12345 }");
        await workspace.SaveAsync(document);

        Assert.True(result.Success);
        var text = File.ReadAllText(logs.CurrentLogPath);
        Assert.Contains("OpenProject", text);
        Assert.Contains("Save", text);
        Assert.DoesNotContain("return 12345", text);
    }

    [Fact]
    public void Recovery_service_reports_user_facing_diagnostic_and_logs_attempt()
    {
        var output = new OutputService();
        var logs = new FileStudioLogService(Path.Combine(Path.GetTempPath(), "MartinPhase17Recovery", Guid.NewGuid().ToString("N")));
        var recovery = new StudioRecoveryService(output, logs);

        var result = recovery.Recover(StudioRecoveryKind.WatcherOverflow, "watcher-error", new IOException("buffer overflow"));

        Assert.True(result.Success);
        Assert.Equal("MRT5009", result.DiagnosticCode);
        Assert.Contains(output.Entries, entry => entry.Message.Contains("MRT5009"));
        Assert.Contains(logs.ReadCurrent(), entry => entry.Operation == "watcher-error" && entry.ExceptionType == typeof(IOException).FullName);
    }
}

public sealed class Phase16FunctionalVerificationTests
{
    [Fact]
    public async Task Open_edit_build_from_memory_save_run_close_and_restore_workflow()
    {
        using var project = new Phase16VerificationProject();
        var workspace = new WorkspaceService();
        var recent = new RecentProjectService();
        var settings = new InMemorySettingsService18 { Settings = new StudioSettings { RestoreOpenDocuments = true } };
        var session = new InMemorySessionService16();
        var output = new OutputService();
        var opener = new ProjectOpeningService(workspace, recent, settings, output, null, session);
        var buildService = new RecordingBuildService16(project.EntryPoint);
        var executionService = new RecordingExecutionService16();
        var build = new BuildCoordinator(workspace.Workspace, output, buildService);
        var execution = new ExecutionCoordinator(workspace.Workspace, output, executionService);

        Assert.True((await opener.OpenProjectAsync(project.Root)).Success);
        var main = workspace.OpenDocument(project.Main);
        var helper = workspace.OpenDocument(project.Helper);
        workspace.ActivateDocument(main.Id);
        workspace.UpdateViewState(main.Id, new EditorViewState { CursorLine = 3, CursorColumn = 9, ScrollTop = 120 });
        workspace.ApplyEditorChange(main.Id, "func main() { return 16 }");

        var buildResult = await build.BuildAsync();

        Assert.True(buildResult.Success);
        Assert.Contains("return 16", buildService.LastCompilation!.SyntaxTrees.Single(tree => tree.Text.FilePath == project.Main).Text.ToString());
        Assert.Contains("return 2", buildService.LastCompilation.SyntaxTrees.Single(tree => tree.Text.FilePath == project.Helper).Text.ToString());
        Assert.True(main.IsDirty);

        var saveAll = await workspace.SaveAllWithResultsAsync();
        Assert.True(saveAll.Success);
        Assert.False(main.IsDirty);
        Assert.Contains("return 16", await File.ReadAllTextAsync(project.Main));

        var runResult = await execution.RunAsync(buildResult);
        Assert.True(runResult.Completed);
        Assert.True(executionService.WasRun);
        Assert.Contains(output.Entries, entry => entry.Channel == OutputChannel.Program && entry.Message.Contains("Program exited with code 0", StringComparison.Ordinal));

        await opener.SaveSessionAsync();
        workspace.CloseProject();
        Assert.Null(workspace.Workspace.Project);

        var restored = await opener.ReopenLastProjectAsync();

        Assert.NotNull(restored);
        Assert.True(restored.Success);
        Assert.Equal(project.Manifest, workspace.Workspace.Project!.ManifestPath);
        Assert.Equal(project.Main, workspace.Workspace.ActiveDocument!.FilePath);
        Assert.Equal(3, workspace.Workspace.ActiveDocument.ViewState.CursorLine);
        Assert.Equal(120, workspace.Workspace.ActiveDocument.ViewState.ScrollTop);
        Assert.Contains(workspace.Workspace.OpenDocuments, document => document.FilePath == project.Helper);
    }

    [Fact]
    public async Task Build_cancellation_and_program_stop_return_to_idle_and_keep_studio_usable()
    {
        using var project = new Phase16VerificationProject();
        var workspace = new WorkspaceService();
        Assert.True(workspace.OpenProject(project.Root).Success);
        var output = new OutputService();
        var slowBuild = new CancellableBuildService16();
        var build = new BuildCoordinator(workspace.Workspace, output, slowBuild);

        var buildTask = build.BuildAsync();
        Assert.True(await slowBuild.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        build.Cancel();
        var cancelled = await buildTask;

        Assert.True(cancelled.WasCancelled);
        Assert.Equal(BuildState.Idle, workspace.Workspace.BuildState);
        Assert.Contains(output.Entries, entry => entry.Channel == OutputChannel.Build && entry.Message.Contains("Build cancelled", StringComparison.Ordinal));

        var executionService = new BlockingExecutionService16();
        var execution = new ExecutionCoordinator(workspace.Workspace, output, executionService);
        var runTask = execution.RunAsync(new BuildResult { Status = BuildStatus.Succeeded, EntryPointPath = project.EntryPoint, OutputDirectory = Path.GetDirectoryName(project.EntryPoint) });
        Assert.True(await executionService.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        execution.Stop();
        var stopped = await runTask;

        Assert.True(stopped.WasCancelled);
        Assert.Equal(ExecutionState.Idle, workspace.Workspace.ExecutionState);
        Assert.Contains(output.Entries, entry => entry.Channel == OutputChannel.Program && entry.Message.Contains("Program stopped", StringComparison.Ordinal));

        var secondBuild = await build.BuildAsync();
        Assert.True(secondBuild.Success);
    }

    [Fact]
    public void Editor_bridge_rejects_invalid_payloads_times_out_requests_and_recovers_cleanly()
    {
        var bridge = new EditorBridge { DefaultTimeout = TimeSpan.FromMilliseconds(25) };

        var invalid = bridge.AcceptHostMessage("{not-json");
        Assert.False(invalid.IsValid);
        Assert.Contains("not valid JSON", invalid.Error);

        var (_, _, timeout) = bridge.CreateRequest("requestText", new EditorRequestTextPayload(Guid.NewGuid()));
        Assert.ThrowsAsync<EditorBridgeRequestTimeoutException>(async () => await timeout).GetAwaiter().GetResult();

        var (_, _, abandoned) = bridge.CreateRequest("requestText", new EditorRequestTextPayload(Guid.NewGuid()), TimeSpan.FromSeconds(30));
        bridge.Recover();
        Assert.ThrowsAsync<InvalidOperationException>(async () => await abandoned).GetAwaiter().GetResult();
        Assert.Equal(EditorBridgeState.Recovering, bridge.State);
    }

    private sealed class InMemorySessionService16 : ISessionService
    {
        public StudioSession Session { get; set; } = new();
        public Task<StudioSession> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Session);
        public Task SaveAsync(StudioSession session, CancellationToken cancellationToken = default) { Session = session; return Task.CompletedTask; }
    }

    private sealed class RecordingBuildService16(string entryPoint) : IMartinBuildService
    {
        public Compilation? LastCompilation { get; private set; }
        public Task<BuildResult> BuildAsync(Compilation compilation, BuildOptions options, CancellationToken cancellationToken = default)
        {
            LastCompilation = compilation;
            Directory.CreateDirectory(Path.GetDirectoryName(entryPoint)!);
            File.WriteAllText(entryPoint, "fake");
            return Task.FromResult(new BuildResult { Status = BuildStatus.Succeeded, EntryPointPath = entryPoint, OutputDirectory = Path.GetDirectoryName(entryPoint), StandardOutput = "build ok" });
        }
    }

    private sealed class CancellableBuildService16 : IMartinBuildService
    {
        private int _calls;
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<BuildResult> BuildAsync(Compilation compilation, BuildOptions options, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _calls) == 1)
            {
                Started.SetResult(true);
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }

            return new BuildResult { Status = BuildStatus.Succeeded, EntryPointPath = Path.Combine(options.OutputDirectory, options.AssemblyName + ".dll"), OutputDirectory = options.OutputDirectory };
        }
    }

    private sealed class RecordingExecutionService16 : IMartinExecutionService
    {
        public bool WasRun { get; private set; }
        public Task<ExecutionResult> RunAsync(BuildResult build, ExecutionOptions options, CancellationToken cancellationToken = default)
        {
            WasRun = true;
            return Task.FromResult(new ExecutionResult { Status = ExecutionStatus.Completed, ExitCode = 0, StandardOutput = "run ok" });
        }
    }

    private sealed class BlockingExecutionService16 : IMartinExecutionService
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<ExecutionResult> RunAsync(BuildResult build, ExecutionOptions options, CancellationToken cancellationToken = default)
        {
            Started.SetResult(true);
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return new ExecutionResult { Status = ExecutionStatus.Completed, ExitCode = 0 };
        }
    }

    private sealed class Phase16VerificationProject : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "MartinPhase16FunctionalVerification", Guid.NewGuid().ToString("N"));
        public string Manifest => Path.Combine(Root, "Martin.toml");
        public string Main => Path.Combine(Root, "Sources", "main.martin");
        public string Helper => Path.Combine(Root, "Sources", "helper.martin");
        public string EntryPoint => Path.Combine(Root, "bin", "Phase16.dll");

        public Phase16VerificationProject()
        {
            Directory.CreateDirectory(Path.Combine(Root, "Sources"));
            File.WriteAllText(Manifest, "manifest-version = 1\n\n[package]\nname = \"Phase16\"\nversion = \"0.1.0\"\n\n[target]\nkind = \"executable\"\nframework = \"net8.0\"\nentry = \"main\"\n\n[build]\noutput = \"bin\"\nintermediate = \"obj\"\n");
            File.WriteAllText(Main, "func main() { return 1 }");
            File.WriteAllText(Helper, "func helper() { return 2 }");
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
    }
    [Fact]
    public async Task Project_explorer_commands_copy_reveal_refresh_and_block_binary_open()
    {
        using var project = new Phase11ProjectOpeningTests.Phase11TempProject();
        var binary = Path.Combine(project.Root, "asset.bin");
        File.WriteAllBytes(binary, [0, 1, 2, 3]);
        var clipboard = new RecordingClipboardService();
        var reveal = new RecordingFileRevealService();
        var workspace = new WorkspaceService();
        Assert.True(workspace.OpenProject(project.Root).Success);
        var explorer = new ProjectExplorerViewModel(workspace, clipboard, reveal);

        explorer.Refresh(workspace.Workspace.ProjectTree);
        explorer.RefreshCommand.Execute(null);
        var root = Assert.Single(explorer.Roots);
        root.LoadChildren();
        var binaryNode = root.Children.Single(child => child.Name == "asset.bin");

        binaryNode.CopyPathCommand.Execute(null);
        await clipboard.WaitAsync();
        Assert.Equal(binary, clipboard.Text);

        binaryNode.RevealCommand.Execute(null);
        await reveal.WaitAsync();
        Assert.Equal(binary, reveal.Path);

        Assert.False(workspace.TryOpenProjectTreeNode(new ProjectTreeNode { Name = "asset.bin", FullPath = binary, Kind = ProjectNodeKind.OtherFile, ChildrenLoaded = true }, out _));
        Assert.Contains(workspace.Workspace.Diagnostics, diagnostic => diagnostic.Code == "MRT5012" && diagnostic.FilePath == binary);
    }

    [Fact]
    public async Task Manifest_reload_preserves_current_project_when_manifest_is_invalid()
    {
        using var project = new Phase11ProjectOpeningTests.Phase11TempProject();
        var workspace = new WorkspaceService(uiDispatcher: new InlineUiDispatcher());
        Assert.True(workspace.OpenProject(project.Root).Success);
        var originalProject = workspace.Workspace.Project;
        var generation = workspace.Workspace.Generation;

        await File.WriteAllTextAsync(project.Manifest, "manifest-version = 99\n");
        MarkManifestReloadPending(workspace);
        await workspace.HandleExternalChangesAsync();

        Assert.Same(originalProject, workspace.Workspace.Project);
        Assert.Equal(generation, workspace.Workspace.Generation);
        Assert.Contains(workspace.Workspace.Diagnostics, diagnostic => diagnostic.Source == "ProjectSystem");
    }

    [Fact]
    public async Task Source_file_external_change_does_not_reload_manifest()
    {
        using var project = new Phase11ProjectOpeningTests.Phase11TempProject();
        var workspace = new WorkspaceService(uiDispatcher: new InlineUiDispatcher());
        Assert.True(workspace.OpenProject(project.Root).Success);
        var originalProject = workspace.Workspace.Project;
        var generation = workspace.Workspace.Generation;

        await File.WriteAllTextAsync(project.Manifest, "manifest-version = 99\n");
        await File.WriteAllTextAsync(project.Main, "func main() { return 42 }");
        await workspace.HandleExternalChangesAsync();

        Assert.Same(originalProject, workspace.Workspace.Project);
        Assert.DoesNotContain(workspace.Workspace.Diagnostics, diagnostic => diagnostic.Source == "ProjectSystem");
        Assert.Equal(generation.Next(), workspace.Workspace.Generation);
    }

    [Fact]
    public async Task Manifest_reload_success_advances_generation_once()
    {
        using var project = new Phase11ProjectOpeningTests.Phase11TempProject();
        var workspace = new WorkspaceService(uiDispatcher: new InlineUiDispatcher());
        Assert.True(workspace.OpenProject(project.Root).Success);
        var generation = workspace.Workspace.Generation;

        MarkManifestReloadPending(workspace);
        await workspace.HandleExternalChangesAsync();

        Assert.Equal(generation.Next(), workspace.Workspace.Generation);
    }

    private static void MarkManifestReloadPending(WorkspaceService workspace)
    {
        var field = typeof(WorkspaceService).GetField("_manifestReloadPending", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(workspace, true);
    }

    private sealed class RecordingClipboardService : IClipboardService
    {
        private readonly TaskCompletionSource _set = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? Text { get; private set; }
        public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            Text = text;
            _set.TrySetResult();
            return Task.CompletedTask;
        }
        public Task WaitAsync() => _set.Task;
    }

    private sealed class RecordingFileRevealService : IFileRevealService
    {
        private readonly TaskCompletionSource _set = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? Path { get; private set; }
        public Task RevealAsync(string path, CancellationToken cancellationToken = default)
        {
            Path = path;
            _set.TrySetResult();
            return Task.CompletedTask;
        }
        public Task WaitAsync() => _set.Task;
    }

    private sealed class InlineUiDispatcher : IUiDispatcher
    {
        public bool HasThreadAccess => true;
        public Task InvokeAsync(Action action, CancellationToken cancellationToken = default) { action(); return Task.CompletedTask; }
        public Task InvokeAsync(Func<Task> action, CancellationToken cancellationToken = default) => action();
        public Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken = default) => Task.FromResult(action());
    }

}

internal sealed class TestProjectOpeningService : IProjectOpeningService
{
    public bool ApproveProjectTransitionResult { get; set; } = true;
    public ProjectOpenResult OpenProjectResult { get; set; } = new(false, null, null, []);
    public List<string> Calls { get; } = [];

    public Task<bool> ApproveProjectTransitionAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add(nameof(ApproveProjectTransitionAsync));
        return Task.FromResult(ApproveProjectTransitionResult);
    }

    public Task<ProjectOpenResult> OpenProjectAsync(string path, CancellationToken cancellationToken = default)
    {
        Calls.Add(nameof(OpenProjectAsync));
        return Task.FromResult(OpenProjectResult);
    }

    public Task<ProjectOpenResult> OpenProjectAfterApprovedTransitionAsync(string path, CancellationToken cancellationToken = default)
    {
        Calls.Add(nameof(OpenProjectAfterApprovedTransitionAsync));
        return Task.FromResult(OpenProjectResult);
    }

    public Task<ProjectOpenResult> OpenRecentProjectAsync(RecentProjectEntry entry, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProjectOpenResult(false, null, null, []));

    public Task<ProjectOpenResult?> ReopenLastProjectAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<ProjectOpenResult?>(null);

    public Task SaveSessionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> CloseProjectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}
