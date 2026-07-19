using Martin.Studio.Core;
using Xunit;

namespace Martin_Studio_WinUI_Tests;

public sealed class Phase5WinUITests
{
    [Fact]
    public void Phase5_ui_command_state_can_be_backed_by_workspace_state()
    {
        var ws = new WorkspaceService();
        Assert.Null(ws.Workspace.Project);
        Assert.DoesNotContain(ws.Workspace.OpenDocuments, d => d.IsDirty);
    }
    [Fact]
    public void Phase5_editor_messages_support_theme_and_document_payloads()
    {
        var theme = EditorMessageProtocol.Deserialize(EditorMessageProtocol.Serialize("setTheme", new { theme = StudioTheme.Dark }));
        var open = EditorMessageProtocol.Deserialize(EditorMessageProtocol.Serialize("openDocument", new { id = Guid.NewGuid(), text = "func main() {}" }));
        Assert.Equal("setTheme", theme!.Type);
        Assert.Equal("openDocument", open!.Type);
    }
}

public sealed class Phase11EditorAssetTests
{
    [Fact]
    public void Phase11_monaco_assets_are_bundled_and_validated()
    {
        var root = FindRepositoryRoot();
        var editorRoot = Path.Combine(root, "Martin", "src", "Martin.Studio.WinUI", "Assets", "Editor");

        var result = EditorAssetValidator.Validate(editorRoot);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues.Select(issue => $"{issue.RelativePath}: {issue.Message}")));
        Assert.Contains(EditorAssetValidator.RequiredRelativePaths, path => path.EndsWith("martin-language.js", StringComparison.Ordinal));
    }

    [Fact]
    public void Phase11_winui_project_includes_editor_assets_as_content()
    {
        var root = FindRepositoryRoot();
        var projectFile = Path.Combine(root, "Martin", "src", "Martin.Studio.WinUI", "Martin.Studio.WinUI.csproj");
        var projectText = File.ReadAllText(projectFile);

        Assert.Contains("Assets\\Editor\\**\\*", projectText);
        Assert.Contains("CopyToOutputDirectory=\"PreserveNewest\"", projectText);
    }

    static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Martin", "Martin.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find repository root.");
    }
}

public sealed class Phase11EditorBridgeAssetTests
{
    [Fact]
    public void Phase11_shell_hosts_webview2_editor_and_restricts_navigation()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Martin", "src", "Martin.Studio.WinUI", "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "Martin", "src", "Martin.Studio.WinUI", "MainWindow.xaml.cs"));

        Assert.Contains("<WebView2 x:Name=\"EditorWebView\"", xaml);
        Assert.Contains("EnsureCoreWebView2Async", code);
        Assert.Contains("NavigationStarting", code);
        Assert.Contains("StartsWith(editorRoot", code);
        Assert.Contains("ProcessFailed", code);
    }

    [Fact]
    public void Phase11_editor_host_waits_for_ready_before_request_response_messages()
    {
        var root = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(root, "Martin", "src", "Martin.Studio.WinUI", "Services", "MonacoEditorHost.cs"));
        var requestMethodStart = code.IndexOf("private async Task<JsonElement> RequestAsync", StringComparison.Ordinal);
        var sendMethodStart = code.IndexOf("private Task SendKnownDocumentAsync", StringComparison.Ordinal);
        var requestMethod = code[requestMethodStart..sendMethodStart];

        Assert.Contains("TaskCompletionSource _ready", code);
        Assert.Contains("SignalReady();", code);
        Assert.Contains("WaitUntilReadyAsync(cancellationToken)", requestMethod);
        Assert.DoesNotContain("FlushQueuedAsync", requestMethod);
        Assert.Contains("Editor WebView is unavailable", requestMethod);
    }

    [Fact]
    public void Phase11_editor_host_updates_recovery_snapshot_from_incremental_changes()
    {
        var root = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(root, "Martin", "src", "Martin.Studio.WinUI", "Services", "MonacoEditorHost.cs"));
        var handlerStart = code.IndexOf("_bridge.TextChanged +=", StringComparison.Ordinal);
        var nextHandlerStart = code.IndexOf("_bridge.ViewStateChanged +=", handlerStart, StringComparison.Ordinal);
        var handler = code[handlerStart..nextHandlerStart];

        Assert.Contains("payload.Changes.OrderByDescending(change => change.RangeOffset)", handler);
        Assert.Contains(".Remove(change.RangeOffset, change.RangeLength)", handler);
        Assert.Contains(".Insert(change.RangeOffset, change.Text)", handler);
        Assert.Contains("snapshot.Text = text", handler);
        Assert.Contains("snapshot.Version = payload.NewVersion", handler);
        Assert.True(handler.IndexOf("snapshot.Text = text", StringComparison.Ordinal) < handler.IndexOf("TextChanged?.Invoke", StringComparison.Ordinal));
    }

    [Fact]
    public void Phase11_editor_script_implements_typed_bridge_messages()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "Martin", "src", "Martin.Studio.WinUI", "Assets", "Editor", "editor.js"));

        Assert.Contains("openDocument", script);
        Assert.Contains("requestText", script);
        Assert.Contains("setMarkers", script);
        Assert.Contains("textChanged", script);
        Assert.Contains("editorReady", script);
        Assert.Contains("Unsupported editor host message", script);
        Assert.Contains("restoreViewState", script);
        Assert.Contains("const viewState = payload.viewState || payload.ViewState", script);
    }

    [Fact]
    public void Phase11_multi_document_ui_binds_tabs_and_editor_model_lifecycle()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Martin", "src", "Martin.Studio.WinUI", "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "Martin", "src", "Martin.Studio.WinUI", "MainWindow.xaml.cs"));
        var script = File.ReadAllText(Path.Combine(root, "Martin", "src", "Martin.Studio.WinUI", "Assets", "Editor", "editor.js"));

        Assert.Contains("ItemsSource=\"{Binding DocumentTabs.Documents", xaml);
        Assert.Contains("SelectedItem=\"{Binding DocumentTabs.ActiveDocument", xaml);
        Assert.Contains("SelectionChanged=\"DocumentTabs_SelectionChanged\"", xaml);
        Assert.Contains("TabViewItem.HeaderProperty", code);
        Assert.Contains("nameof(DocumentViewModel.Header)", code);
        Assert.Contains("AutomationProperties.NameProperty", code);
        Assert.Contains("nameof(DocumentViewModel.AccessibilityName)", code);
        Assert.DoesNotContain("Header = document.Header", code);
        Assert.Contains("openDocument", code);
        Assert.Contains("closeDocument", code);
        Assert.Contains("saveViewState", script);
        Assert.Contains("restoreViewState", script);
        Assert.Contains("model.dispose", script);
        Assert.Contains("recreateModelForPathChange", script);
        Assert.Contains("entry.model.uri.toString() !== requestedUri", script);
        Assert.Contains("getModelMarkers({ resource: oldModel.uri })", script);
        Assert.Contains("entry.viewState = martinEditor.editor.saveViewState()", script);
        Assert.Contains("revealRange", script);
    }

    [Fact]
    public void Phase11_shell_resynchronizes_monaco_after_document_restore_and_identity_changes()
    {
        var root = FindRepositoryRoot();
        var windowCode = File.ReadAllText(Path.Combine(root, "Martin", "src", "Martin.Studio.WinUI", "MainWindow.xaml.cs"));
        var appCode = File.ReadAllText(Path.Combine(root, "Martin", "src", "Martin.Studio.WinUI", "App.xaml.cs"));
        var workspaceCode = File.ReadAllText(Path.Combine(root, "Martin", "src", "Martin.Studio.Core", "Services", "StudioServices.cs"));

        Assert.Contains("public async Task SynchronizeEditorDocumentsAsync()", windowCode);
        Assert.Contains("await _window.SynchronizeEditorDocumentsAsync();", appCode);
        Assert.Contains("NotifyCollectionChangedAction.Add", windowCode);
        Assert.Contains("EditorSynchronizationRequested?.Invoke", workspaceCode);
        Assert.Contains("DocumentReloaded +=", windowCode);
        Assert.Contains("ProjectRefreshed += _languageProvider.RefreshProjectAsync", windowCode);
        Assert.Contains("ReplaceTextAsync(document, cancellationToken)", windowCode);
        Assert.Contains("snapshot.Version = (int)document.Version.Value", File.ReadAllText(Path.Combine(root, "Martin", "src", "Martin.Studio.WinUI", "Services", "MonacoEditorHost.cs")));
    }

    static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Martin", "Martin.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find repository root.");
    }
}

public sealed class Phase11ShortcutAccessibilityMarkupTests
{
    [Fact]
    public void Phase11_command_bar_wraps_the_configuration_picker_as_a_command_element()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Martin", "src", "Martin.Studio.WinUI", "MainWindow.xaml"));

        Assert.Contains("<AppBarElementContainer>", xaml);
        Assert.Contains("<ComboBox Header=\"Configuration\"", xaml);
        Assert.Contains("</AppBarElementContainer>", xaml);
    }

    [Fact]
    public void Phase11_shell_declares_shortcuts_and_accessibility_metadata()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Martin", "src", "Martin.Studio.WinUI", "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "Martin", "src", "Martin.Studio.WinUI", "MainWindow.xaml.cs"));

        Assert.Contains("KeyboardAcceleratorTextOverride=\"Ctrl+S\"", xaml);
        Assert.Contains("KeyboardAcceleratorTextOverride=\"Ctrl+Shift+B\"", xaml);
        Assert.Contains("AutomationProperties.HelpText=\"Each diagnostic includes severity text", xaml);
        Assert.Contains("TabIndex=\"30\"", xaml);
        Assert.Contains("RegisterKeyboardAccelerators", code);
        Assert.Contains("VirtualKey.F8", code);
        Assert.Contains("VirtualKey.Tab", code);
        Assert.Contains("PostEditorCommand(\"find\"", code);
    }

    static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Martin", "Martin.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
