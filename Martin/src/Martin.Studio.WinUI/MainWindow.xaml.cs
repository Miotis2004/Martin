using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Immutable;
using System.Threading;
using Martin.Studio.Core;
using Martin.Build;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Windowing;
using Windows.Foundation;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Martin.Services;

namespace Martin;

/// <summary>
/// Main Martin Studio window hosted by a dependency-injected view model.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel, IStudioLogService logService, IStudioRecoveryService recoveryService, IFileDialogService fileDialogService, IMessageDialogService messageDialogService, IClipboardService clipboardService, IFileRevealService fileRevealService, EditorHostProxy editorHost, IDiagnosticService diagnosticService, IWorkspaceService workspaceService, ISessionService sessionService, StudioLanguageProvider languageProvider)
    {
        ViewModel = viewModel;
        _logService = logService;
        _recoveryService = recoveryService;
        _fileDialogService = fileDialogService;
        _messageDialogService = messageDialogService;
        _clipboardService = clipboardService;
        _fileRevealService = fileRevealService;
        _diagnosticService = diagnosticService;
        _workspaceService = workspaceService;
        _sessionService = sessionService;
        _languageProvider = languageProvider;
        _languageProvider.DiagnosticsPublished += diagnostics =>
        {
            _diagnosticService.Apply(diagnostics);
            _ = ApplyEditorDiagnosticsAsync();
        };
        if (fileDialogService is WinUIFileDialogService winUIFileDialogService)
            winUIFileDialogService.SetWindowHandle(WindowNative.GetWindowHandle(this));

        InitializeComponent();
        var monacoEditorHost = new MonacoEditorHost(EditorWebView, logService, recoveryService, languageProvider);
        monacoEditorHost.NavigationRequested += (_, payload) => _ = NavigateSemanticLocationAsync(payload);
        editorHost.Attach(monacoEditorHost);
        _editorHost = editorHost;
        LayoutRoot.DataContext = ViewModel;
        Title = ViewModel.ApplicationTitle;
        ViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.ApplicationTitle))
                Title = ViewModel.ApplicationTitle;
            if (args.PropertyName == nameof(MainWindowViewModel.CurrentTheme))
                _ = _editorHost.SetThemeAsync(ViewModel.CurrentTheme);
        };
        ViewModel.DocumentTabs.Documents.CollectionChanged += DocumentTabs_CollectionChanged;
        _workspaceService.EditorSynchronizationRequested += (_, _) => _ = SynchronizeEditorDocumentsAsync();
        _workspaceService.DocumentReloaded += ReplaceEditorTextAsync;
        _workspaceService.ProjectRefreshed += _languageProvider.RefreshProjectAsync;
        _workspaceService.Workspace.Diagnostics.CollectionChanged += (_, _) => _ = ApplyEditorDiagnosticsAsync();
        ViewModel.DocumentTabs.PropertyChanged += DocumentTabs_PropertyChanged;
        RefreshDocumentTabItems();
        ViewModel.WriteOutputCommand.Execute(null);
        RegisterKeyboardAccelerators();
        WinUIMessageDialogService.DialogXamlRoot = LayoutRoot.XamlRoot;
        RegisterShutdownInterception();
        _ = RestoreWindowAndPanelStateAsync();
        _editorInitializationTask = InitializeEditorBridgeAsync();
    }

    private void RegisterShutdownInterception()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Closing += AppWindow_Closing;
    }

    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_shutdownConfirmed)
            return;
        args.Cancel = true;
        await RequestShutdownAsync();
    }

    private async Task RequestShutdownAsync()
    {
        if (_shutdownInProgress)
            return;
        _shutdownInProgress = true;
        try
        {
            var result = await ViewModel.ShutdownAsync();
            if (result.CanShutdown)
            {
                await SaveWindowAndPanelStateAsync();
                await _languageProvider.DisposeAsync();
                _shutdownConfirmed = true;
                Close();
            }
        }
        finally
        {
            _shutdownInProgress = false;
        }
    }

    private void RegisterKeyboardAccelerators()
    {
        AddAccelerator(VirtualKey.O, VirtualKeyModifiers.Control, (_, args) =>
                                                                  { args.Handled = true; OpenProjectManifest_Click(this, new RoutedEventArgs()); });
        AddAccelerator(VirtualKey.N, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, (_, args) =>
                                                                                              { args.Handled = true; NewProject_Click(this, new RoutedEventArgs()); });
        AddAccelerator(VirtualKey.S, VirtualKeyModifiers.Control, async (_, args) =>
                                                                  { args.Handled = true; await ViewModel.SaveCommand.ExecuteAsync(null); });
        AddAccelerator(VirtualKey.S, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, async (_, args) =>
                                                                                              { args.Handled = true; await ViewModel.SaveAllCommand.ExecuteAsync(null); });
        AddAccelerator(VirtualKey.W, VirtualKeyModifiers.Control, (_, args) =>
                                                                  { args.Handled = true; ViewModel.CloseActiveDocumentCommand.Execute(null); });
        AddAccelerator(VirtualKey.B, VirtualKeyModifiers.Control, async (_, args) =>
                                                                  { args.Handled = true; await ViewModel.BuildCommand.ExecuteAsync(null); });
        AddAccelerator(VirtualKey.B, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, (_, args) =>
                                                                                              { args.Handled = true; ViewModel.CancelBuildCommand.Execute(null); });
        AddAccelerator(VirtualKey.F5, VirtualKeyModifiers.None, async (_, args) =>
                                                                { args.Handled = true; await ViewModel.RunCommand.ExecuteAsync(null); });
        AddAccelerator(VirtualKey.F5, VirtualKeyModifiers.Control, async (_, args) =>
                                                                   { args.Handled = true; await ViewModel.RunWithoutBuildCommand.ExecuteAsync(null); });
        AddAccelerator(VirtualKey.F5, VirtualKeyModifiers.Shift, (_, args) =>
                                                                 { args.Handled = true; ViewModel.StopCommand.Execute(null); });
        AddAccelerator(VirtualKey.F8, VirtualKeyModifiers.None, async (_, args) =>
                                                                { args.Handled = true; await NavigateDiagnosticAsync(_diagnosticService.Next(ViewModel.ErrorList.Filter)); });
        AddAccelerator(VirtualKey.F8, VirtualKeyModifiers.Shift, async (_, args) =>
                                                                 { args.Handled = true; await NavigateDiagnosticAsync(_diagnosticService.Previous(ViewModel.ErrorList.Filter)); });
        AddAccelerator(VirtualKey.Tab, VirtualKeyModifiers.Control, (_, args) =>
                                                                    { args.Handled = true; ViewModel.NextDocumentCommand.Execute(null); });
        AddAccelerator(VirtualKey.Tab, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, (_, args) =>
                                                                                                { args.Handled = true; ViewModel.PreviousDocumentCommand.Execute(null); });
        AddAccelerator(VirtualKey.F, VirtualKeyModifiers.Control, (_, args) =>
                                                                  { args.Handled = true; ExecuteEditorCommandAndFocus("find"); });
        AddAccelerator(VirtualKey.H, VirtualKeyModifiers.Control, (_, args) =>
                                                                  { args.Handled = true; ExecuteEditorCommandAndFocus("replace"); });
        AddAccelerator((VirtualKey)0xC0, VirtualKeyModifiers.Control, (_, args) =>
                                                                      { args.Handled = true; ToggleOrFocusOutputPanel(); });
    }

    private void AddAccelerator(VirtualKey key, VirtualKeyModifiers modifiers, TypedEventHandler<KeyboardAccelerator, KeyboardAcceleratorInvokedEventArgs> handler)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += handler;
        LayoutRoot.KeyboardAccelerators.Add(accelerator);
    }

    public void ApplyCurrentThemeToEditor() => _ = _editorHost.SetThemeAsync(ViewModel.CurrentTheme);

    private void ExecuteEditorCommandAndFocus(string command)
    {
        _ = _editorHost.ExecuteCommandAsync(command);
        EditorWebView.Focus(FocusState.Keyboard);
    }

    private void ToggleOrFocusOutputPanel()
    {
        if (BottomPanel.Visibility == Visibility.Visible && BottomPanelRow.Height.Value > 0 && BottomPanelTabs.SelectedIndex == 0)
        {
            SetBottomPanelVisible(false);
            ViewModel.StatusBar.Text = "Output panel hidden.";
            _ = SaveWindowAndPanelStateAsync();
            return;
        }

        ShowBottomPanelTab(0, focus: true);
    }

    private void ProjectExplorerMenu_Click(object sender, RoutedEventArgs e)
    {
        SetProjectExplorerVisible(ProjectExplorerColumn.ActualWidth <= 0 || ProjectExplorerPane.Visibility != Visibility.Visible, focus: true);
        _ = SaveWindowAndPanelStateAsync();
    }

    private void OutputMenu_Click(object sender, RoutedEventArgs e) => ShowBottomPanelTab(0, focus: true);

    private void ErrorListMenu_Click(object sender, RoutedEventArgs e) => ShowBottomPanelTab(1, focus: true);

    private void ShowBottomPanelTab(int tabIndex, bool focus)
    {
        SetBottomPanelVisible(true);
        if (BottomPanelTabs.TabItems.Count > tabIndex)
            BottomPanelTabs.SelectedIndex = tabIndex;

        if (focus)
        {
            if (tabIndex == 1)
                ErrorListView.Focus(FocusState.Keyboard);
            else
                OutputListView.Focus(FocusState.Keyboard);
        }

        ViewModel.StatusBar.Text = tabIndex == 1 ? "Error List panel selected." : "Output panel selected.";
        _ = SaveWindowAndPanelStateAsync();
    }

    private void SetProjectExplorerVisible(bool visible, bool focus = false)
    {
        if (!visible && ProjectExplorerColumn.ActualWidth > 0)
            _lastProjectExplorerWidth = ProjectExplorerColumn.ActualWidth;

        ProjectExplorerPane.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        VerticalSplitter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        ProjectExplorerColumn.MinWidth = visible ? 180 : 0;
        ProjectExplorerColumn.Width = visible ? new GridLength(Math.Clamp(GetProjectExplorerWidth(), 180, 520)) : new GridLength(0);
        if (visible && focus)
            ProjectExplorerTree.Focus(FocusState.Keyboard);
        ViewModel.StatusBar.Text = visible ? "Project Explorer selected." : "Project Explorer hidden.";
    }

    private void SetBottomPanelVisible(bool visible)
    {
        if (!visible && BottomPanelRow.ActualHeight > 0)
            _lastBottomPanelHeight = BottomPanelRow.ActualHeight;

        BottomPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        HorizontalSplitter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        BottomPanelRow.MinHeight = visible ? 120 : 0;
        BottomPanelRow.Height = visible ? new GridLength(Math.Clamp(GetBottomPanelHeight(), 120, 420)) : new GridLength(0);
    }

    private double GetProjectExplorerWidth() => ProjectExplorerColumn.ActualWidth > 0? ProjectExplorerColumn.ActualWidth : (ProjectExplorerColumn.Width.Value > 0 ? ProjectExplorerColumn.Width.Value : _lastProjectExplorerWidth);

    private double GetBottomPanelHeight() => BottomPanelRow.ActualHeight > 0? BottomPanelRow.ActualHeight : (BottomPanelRow.Height.Value > 0 ? BottomPanelRow.Height.Value : _lastBottomPanelHeight);

    public MainWindowViewModel ViewModel { get; }

    private readonly IStudioLogService _logService;
    private readonly IStudioRecoveryService _recoveryService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IMessageDialogService _messageDialogService;
    private readonly IClipboardService _clipboardService;
    private readonly IFileRevealService _fileRevealService;
    private readonly IDiagnosticService _diagnosticService;
    private readonly IWorkspaceService _workspaceService;
    private readonly ISessionService _sessionService;
    private readonly StudioLanguageProvider _languageProvider;
    private readonly IEditorHost _editorHost;
    private readonly SemaphoreSlim _editorSynchronizationGate = new(1, 1);
    private readonly SemaphoreSlim _editorChangeGate = new(1, 1);
    private readonly Task _editorInitializationTask;
    private bool _syncingDocumentTabs;
    private bool _shutdownConfirmed;
    private bool _shutdownInProgress;
    private double _lastProjectExplorerWidth = 280;
    private double _lastBottomPanelHeight = 220;

    private async Task InitializeEditorBridgeAsync()
    {
        try
        {
            _editorHost.TextChanged += async (_, payload) =>
            {
                try
                {
                    await ApplyEditorChangesAsync(payload);
                    ViewModel.StatusBar.Text = $"Edited document {payload.DocumentId}.";
                }
                catch (Exception ex)
                {
                    _logService.Log(StudioLogCategory.EditorBridge, OutputSeverity.Error, "ApplyEditorChanges", "Could not synchronize editor changes.", ex.Message, ex);
                    ViewModel.StatusBar.Text = $"Editor synchronization failed: {ex.Message}";
                }
            };
            _editorHost.ViewStateChanged += (_, payload) =>
            {
                ViewModel.UpdateEditorViewState(payload.DocumentId, new EditorViewState { CursorLine = payload.CursorLine, CursorColumn = payload.CursorColumn, ScrollTop = payload.ScrollTop, ScrollLeft = payload.ScrollLeft });
                ViewModel.StatusBar.Text = $"View state saved for {payload.DocumentId}.";
            };
            _editorHost.Error += (_, payload) => ViewModel.StatusBar.Text = $"Editor error: {payload.Message}";
            _editorHost.SaveRequested += async (_, documentId) =>
            {
                if (documentId != Guid.Empty)
                    ViewModel.ActivateDocumentCommand.Execute(documentId);
                await ViewModel.SaveCommand.ExecuteAsync(null);
            };

            await _editorHost.InitializeAsync();
            if (EditorWebView.CoreWebView2 is not null)
                EditorWebView.CoreWebView2.NavigationStarting += EditorWebView_NavigationStarting;
            ViewModel.StatusBar.Text = "Editor initializing.";
            await SynchronizeEditorDocumentsCoreAsync();
            await ApplyEditorDiagnosticsAsync();
            await _editorHost.SetThemeAsync(ViewModel.CurrentTheme);
        }
        catch (Exception ex)
        {
            _logService.Log(StudioLogCategory.WebView2, OutputSeverity.Error, "InitializeEditorBridge", "Editor failed to start.", ex.Message, ex);
            _recoveryService.Recover(StudioRecoveryKind.WebView2ProcessFailure, "InitializeEditorBridge", ex);
            ViewModel.StatusBar.Text = $"Editor failed to start: {ex.Message}";
        }
    }

    private async Task ApplyEditorChangesAsync(EditorTextChangedPayload payload)
    {
        await _editorChangeGate.WaitAsync();
        try
        {
            await ApplyEditorChangesCoreAsync(payload);
        }
        finally
        {
            _editorChangeGate.Release();
        }
    }

    private async Task ApplyEditorChangesCoreAsync(EditorTextChangedPayload payload)
    {
        var document = _workspaceService.Workspace.OpenDocuments.FirstOrDefault(d => d.Id == payload.DocumentId);
        if (document is null)
            return;

        var updatedText = document.Text;
        foreach (var change in payload.Changes.OrderByDescending(change => change.RangeOffset))
        {
            if (change.RangeOffset > updatedText.Length || change.RangeLength > updatedText.Length - change.RangeOffset)
                throw new InvalidOperationException("The editor supplied a change outside the current document.");
            updatedText = updatedText.Remove(change.RangeOffset, change.RangeLength).Insert(change.RangeOffset, change.Text);
        }

        var changes = payload.Changes.Select(change => new Martin.LanguageServices.TextChange(
                                                 new Martin.Compiler.Text.TextSpan(change.RangeOffset, change.RangeLength), change.Text))
                          .ToImmutableArray();
        var result = await _languageProvider.ApplyEditorChangesAsync(payload.DocumentId, new(payload.PreviousVersion), new(payload.NewVersion), changes);

        ViewModel.ApplyEditorTextChange(payload.DocumentId, updatedText);
        if (!result.IsApplied)
            await _languageProvider.ReplaceDocumentTextAsync(document);
    }

    private void EditorWebView_NavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri) || !uri.IsFile)
        {
            args.Cancel = true;
            _logService.Log(StudioLogCategory.WebView2, OutputSeverity.Warning, "NavigationStarting", "External editor navigation was blocked.", args.Uri);
            return;
        }

        var editorRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Assets", "Editor"));
        var target = Path.GetFullPath(uri.LocalPath);
        if (!target.StartsWith(editorRoot, StringComparison.OrdinalIgnoreCase))
        {
            args.Cancel = true;
            _logService.Log(StudioLogCategory.WebView2, OutputSeverity.Warning, "NavigationStarting", "Editor navigation outside asset root was blocked.", target);
        }
    }

    private void VerticalSplitter_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var nextWidth = Math.Clamp(ProjectExplorerColumn.ActualWidth + e.HorizontalChange, 180, 520);
        _lastProjectExplorerWidth = nextWidth;
        ProjectExplorerColumn.Width = new GridLength(nextWidth);
        _ = SaveWindowAndPanelStateAsync();
    }

    private void HorizontalSplitter_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var nextHeight = Math.Clamp(BottomPanelRow.ActualHeight - e.VerticalChange, 120, 420);
        _lastBottomPanelHeight = nextHeight;
        BottomPanelRow.Height = new GridLength(nextHeight);
        _ = SaveWindowAndPanelStateAsync();
    }

    private void BottomPanelTabs_SelectionChanged(object sender, SelectionChangedEventArgs e) => _ = SaveWindowAndPanelStateAsync();

    private async Task RestoreWindowAndPanelStateAsync()
    {
        try
        {
            var session = await _sessionService.LoadAsync();
            _lastProjectExplorerWidth = Math.Clamp(session.ProjectExplorerWidth, 180, 520);
            _lastBottomPanelHeight = Math.Clamp(session.BottomPanelHeight, 120, 420);
            SetProjectExplorerVisible(session.ProjectExplorerVisible);
            BottomPanelRow.Height = new GridLength(_lastBottomPanelHeight);
            SetBottomPanelVisible(session.BottomPanelVisible);
            var tabIndex = string.Equals(session.BottomPanelTab, "Error List", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            if (BottomPanelTabs.TabItems.Count > tabIndex)
                BottomPanelTabs.SelectedIndex = tabIndex;

            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            if (session.WindowPlacement.X is int x && session.WindowPlacement.Y is int y)
                appWindow.Move(new Windows.Graphics.PointInt32(x, y));
            var width = Math.Clamp(session.WindowPlacement.Width, 640, 3840);
            var height = Math.Clamp(session.WindowPlacement.Height, 480, 2160);
            appWindow.Resize(new Windows.Graphics.SizeInt32((int)width, (int)height));
            if (session.WindowPlacement.IsMaximized && appWindow.Presenter is OverlappedPresenter presenter)
                presenter.Maximize();
        }
        catch (Exception ex)
        {
            _logService.Log(StudioLogCategory.Session, OutputSeverity.Warning, "RestoreWindowAndPanelState", "Window and panel state could not be restored.", ex.Message, ex);
        }
    }

    private async Task SaveWindowAndPanelStateAsync()
    {
        try
        {
            var current = await _sessionService.LoadAsync();
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            var maximized = appWindow.Presenter is OverlappedPresenter presenter && presenter.State == OverlappedPresenterState.Maximized;
            var bottomTab = BottomPanelTabs.SelectedIndex == 1 ? "Error List" : "Output";
            await _sessionService.SaveAsync(current with {
                BottomPanelTab = bottomTab,
                BottomPanelVisible = BottomPanel.Visibility == Visibility.Visible && BottomPanelRow.Height.Value > 0,
                ProjectExplorerWidth = GetProjectExplorerWidth(),
                ProjectExplorerVisible = ProjectExplorerPane.Visibility == Visibility.Visible && ProjectExplorerColumn.Width.Value > 0,
                BottomPanelHeight = GetBottomPanelHeight(),
                WindowPlacement = new WindowPlacementState(appWindow.Size.Width, appWindow.Size.Height, maximized, appWindow.Position.X, appWindow.Position.Y)
            });
        }
        catch (Exception ex)
        {
            _logService.Log(StudioLogCategory.Session, OutputSeverity.Warning, "SaveWindowAndPanelState", "Window and panel state could not be saved.", ex.Message, ex);
        }
    }

    private async void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox { Header = "Project name", PlaceholderText = "MyMartinApp" };
        var locationBox = new TextBox { Header = "Location", IsReadOnly = true, Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
        var browseButton = new Button { Content = "Browse..." };
        var frameworkBox = new ComboBox { Header = "Framework", IsEnabled = false, SelectedIndex = 0 };
        frameworkBox.Items.Add("net8.0");
        var gitIgnoreBox = new CheckBox { Content = "Create .gitignore", IsChecked = true };
        var openBox = new CheckBox { Content = "Create and open", IsChecked = true };
        var validationText = new TextBlock { Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Firebrick), TextWrapping = TextWrapping.Wrap };

        browseButton.Click += async (_, _) =>
        {
            var folder = await PickFolderForNewProjectAsync();
            if (!string.IsNullOrWhiteSpace(folder))
                locationBox.Text = folder;
        };

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(nameBox);
        panel.Children.Add(locationBox);
        panel.Children.Add(browseButton);
        panel.Children.Add(frameworkBox);
        panel.Children.Add(gitIgnoreBox);
        panel.Children.Add(openBox);
        panel.Children.Add(validationText);

        var dialog = new ContentDialog {
            Title = "New Martin Project",
            Content = panel,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = LayoutRoot.XamlRoot
        };

        dialog.PrimaryButtonClick += (_, args) =>
        {
            var error = ValidateNewProjectInput(nameBox.Text, locationBox.Text);
            if (error is not null)
            {
                validationText.Text = error;
                args.Cancel = true;
            }
        };

        var response = await dialog.ShowAsync();
        if (response != ContentDialogResult.Primary)
            return;

        var request = new StudioProjectCreationRequest {
            ProjectName = nameBox.Text.Trim(),
            BaseDirectory = locationBox.Text.Trim(),
            TargetFramework = "net8.0",
            CreateGitIgnore = gitIgnoreBox.IsChecked == true,
            OpenAfterCreation = openBox.IsChecked == true
        };
        await ViewModel.CreateProjectCommand.ExecuteAsync(request);
        OpenAllDocumentsInEditor();
    }

    private async Task<string?> PickFolderForNewProjectAsync()
    {
        var picker = new FolderPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.FileTypeFilter.Add("*");
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private static string? ValidateNewProjectInput(string projectName, string location)
    {
        var name = projectName.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return "Project name is required.";
        if (!(char.IsLetter(name[0]) || name[0] == '_') || name.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '.' or '-')))
            return "Project name must start with a letter or underscore and contain only letters, digits, underscores, dots, or hyphens.";
        if (string.IsNullOrWhiteSpace(location))
            return "Location is required.";
        if (!Directory.Exists(location))
            return "Location must be an existing folder.";
        return null;
    }

    private async void OpenProjectManifest_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.FileTypeFilter.Add(".toml");
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            await ViewModel.OpenProjectCommand.ExecuteAsync(file.Path);
            await SynchronizeEditorDocumentsAsync();
        }
    }

    private async void OpenProjectFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.FileTypeFilter.Add("*");
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            await ViewModel.OpenProjectCommand.ExecuteAsync(folder.Path);
            await SynchronizeEditorDocumentsAsync();
        }
    }

    private async void CloseProject_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CloseProjectCommand.ExecuteAsync(null);
    }

    private async void Exit_Click(object sender, RoutedEventArgs e)
    {
        await RequestShutdownAsync();
    }

    private async void ThemeSystem_Click(object sender, RoutedEventArgs e) => await ViewModel.SetThemeCommand.ExecuteAsync(StudioTheme.System);

    private async void ThemeLight_Click(object sender, RoutedEventArgs e) => await ViewModel.SetThemeCommand.ExecuteAsync(StudioTheme.Light);

    private async void ThemeDark_Click(object sender, RoutedEventArgs e) => await ViewModel.SetThemeCommand.ExecuteAsync(StudioTheme.Dark);

    private void Undo_Click(object sender, RoutedEventArgs e) => ExecuteEditorCommandAndFocus("undo");
    private void Redo_Click(object sender, RoutedEventArgs e) => ExecuteEditorCommandAndFocus("redo");
    private void Cut_Click(object sender, RoutedEventArgs e) => ExecuteEditorCommandAndFocus("cut");
    private void Copy_Click(object sender, RoutedEventArgs e) => ExecuteEditorCommandAndFocus("copy");
    private void Paste_Click(object sender, RoutedEventArgs e) => ExecuteEditorCommandAndFocus("paste");
    private void SelectAll_Click(object sender, RoutedEventArgs e) => ExecuteEditorCommandAndFocus("selectAll");
    private void FocusEditor_Click(object sender, RoutedEventArgs e) => _ = _editorHost.FocusEditorAsync();

    private void DocumentTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingDocumentTabs)
            return;

        if (DocumentTabView.SelectedItem is TabViewItem { Tag : DocumentViewModel document })
        {
            ViewModel.ActivateDocumentCommand.Execute(document.Id);
            _ = _editorHost.ActivateDocumentAsync(document.Id);
        }
    }

    private async void DocumentTabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab is TabViewItem { Tag : DocumentViewModel document } && ViewModel.CloseDocumentCommand.CanExecute(document.Id))
            await ViewModel.CloseDocumentCommand.ExecuteAsync(document.Id);
    }

    private void DocumentTabs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshDocumentTabItems();
        _ = SynchronizeEditorDocumentsAsync();
    }

    private void DocumentTabs_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentTabsViewModel.ActiveDocument))
            SelectActiveDocumentTab();
    }

    private void RefreshDocumentTabItems()
    {
        _syncingDocumentTabs = true;
        try
        {
            DocumentTabView.TabItems.Clear();
            foreach (var document in ViewModel.DocumentTabs.Documents)
            {
                var tab = new TabViewItem {
                    IsClosable = true,
                    Tag = document
                };
                tab.SetBinding(
                    TabViewItem.HeaderProperty,
                    new Microsoft.UI.Xaml.Data.Binding {
                        Source = document,
                        Path = new PropertyPath(nameof(DocumentViewModel.Header)),
                        Mode = Microsoft.UI.Xaml.Data.BindingMode.OneWay
                    });
                tab.SetBinding(
                    AutomationProperties.NameProperty,
                    new Microsoft.UI.Xaml.Data.Binding {
                        Source = document,
                        Path = new PropertyPath(nameof(DocumentViewModel.AccessibilityName)),
                        Mode = Microsoft.UI.Xaml.Data.BindingMode.OneWay
                    });
                DocumentTabView.TabItems.Add(tab);
            }

            SelectActiveDocumentTab();
        }
        finally
        {
            _syncingDocumentTabs = false;
        }
    }

    private void SelectActiveDocumentTab()
    {
        _syncingDocumentTabs = true;
        try
        {
            var activeId = ViewModel.DocumentTabs.ActiveDocument?.Id;
            DocumentTabView.SelectedItem = DocumentTabView.TabItems
                                               .OfType<TabViewItem>()
                                               .FirstOrDefault(tab => tab.Tag is DocumentViewModel document && document.Id == activeId);
        }
        finally
        {
            _syncingDocumentTabs = false;
        }
    }

    private void OpenAllDocumentsInEditor()
    {
        _ = OpenAllDocumentsInEditorAsync();
    }

    private Task OpenAllDocumentsInEditorAsync() => SynchronizeEditorDocumentsAsync();

    public async Task SynchronizeEditorDocumentsAsync()
    {
        await _editorInitializationTask;
        await _editorSynchronizationGate.WaitAsync();
        try
        {
            await SynchronizeEditorDocumentsCoreAsync();
        }
        finally
        {
            _editorSynchronizationGate.Release();
        }
    }

    private async Task ReplaceEditorTextAsync(DocumentModel document, CancellationToken cancellationToken)
    {
        await _editorInitializationTask;
        await _editorHost.ReplaceTextAsync(document, cancellationToken);
        await _languageProvider.ReplaceDocumentTextAsync(document, cancellationToken);
    }

    private async Task SynchronizeEditorDocumentsCoreAsync()
    {
        await _languageProvider.SynchronizeAsync(_workspaceService.Workspace);
        foreach (var document in _workspaceService.Workspace.OpenDocuments.ToArray())
            await _editorHost.OpenDocumentAsync(document);
        if (_workspaceService.Workspace.ActiveDocument is {} active)
            await _editorHost.ActivateDocumentAsync(active.Id);
    }

    private async Task ApplyEditorDiagnosticsAsync()
    {
        foreach (var document in _workspaceService.Workspace.OpenDocuments.ToArray())
        {
            try
            {
                await _editorHost.SetDiagnosticsAsync(document.Id, _diagnosticService.ToEditor(document.Id));
            }
            catch (Exception ex)
            {
                _logService.Log(StudioLogCategory.Diagnostics, OutputSeverity.Warning, "ApplyEditorDiagnostics", "Could not apply editor diagnostics.", ex.Message, ex);
            }
        }
    }

    private async Task NavigateDiagnosticAsync(StudioDiagnostic? diagnostic)
    {
        if (diagnostic?.FilePath is null || diagnostic.Range is null)
            return;
        if (!_workspaceService.TryNavigateToDocument(diagnostic.FilePath, diagnostic.Range, out var document) || document is null)
            return;
        ViewModel.RefreshCommandState();
        await _editorHost.OpenDocumentAsync(document);
        await _editorHost.SetDiagnosticsAsync(document.Id, _diagnosticService.ToEditor(document.Id));
        await _editorHost.ActivateDocumentAsync(document.Id);
        await _editorHost.RevealRangeAsync(document.Id, diagnostic.Range, select: true);
        await _editorHost.FocusEditorAsync();
    }

    private async Task NavigateSemanticLocationAsync(EditorNavigationRequestedPayload location)
    {
        var range = new TextRange(location.StartLine, location.StartColumn, location.EndLine, location.EndColumn);
        if (!_workspaceService.TryNavigateToDocument(location.FilePath, range, out var document) || document is null)
            return;

        ViewModel.RefreshCommandState();
        await _editorHost.OpenDocumentAsync(document);
        await _languageProvider.OpenDocumentAsync(document);
        await _editorHost.SetDiagnosticsAsync(document.Id, _diagnosticService.ToEditor(document.Id));
        await _editorHost.ActivateDocumentAsync(document.Id);
        await _editorHost.RevealRangeAsync(document.Id, range, select: true);
        await _editorHost.FocusEditorAsync();
    }

    private void DiagnosticFilter_Changed(object sender, object e)
    {
        if (ViewModel is null)
            return;
        ImmutableHashSet<OutputSeverity>? severities = null;
        if (DiagnosticSeverityFilter?.SelectedItem is ComboBoxItem item && item.Content is string severityText && severityText != "All")
        {
            severities = severityText switch {
                "Error" => ImmutableHashSet.Create(OutputSeverity.Error),
                "Warning" => ImmutableHashSet.Create(OutputSeverity.Warning),
                "Info" => ImmutableHashSet.Create(OutputSeverity.Info),
                _ => null
            };
        }

        var sources = string.IsNullOrWhiteSpace(DiagnosticSourceFilter?.Text)
                          ? null
                          : DiagnosticSourceFilter.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
        var search = string.IsNullOrWhiteSpace(DiagnosticSearchBox?.Text) ? null : DiagnosticSearchBox.Text;
        ViewModel.ErrorList.SetFilter(new DiagnosticFilter(severities, sources, search));
    }

    private async void ErrorList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ErrorListView.SelectedItem is StudioDiagnosticViewModel diagnostic)
            await NavigateDiagnosticAsync(diagnostic.Diagnostic);
    }

    private async void ErrorList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ErrorListView.SelectedItem is StudioDiagnosticViewModel diagnostic)
        {
            e.Handled = true;
            await NavigateDiagnosticAsync(diagnostic.Diagnostic);
        }
    }

    private async void CopyAllDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        await _clipboardService.SetTextAsync(ViewModel.ErrorList.CopyAll());
        ViewModel.StatusBar.Text = "Diagnostics copied.";
    }

    private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_logService.LogsDirectory);
        var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
        startInfo.ArgumentList.Add(_logService.LogsDirectory);
        Process.Start(startInfo);
    }

    private void DebugConfiguration_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ActiveBuildConfiguration = BuildConfiguration.Debug;

    private void ReleaseConfiguration_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ActiveBuildConfiguration = BuildConfiguration.Release;

    private void ProjectExplorerTree_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        if (args.Item is ProjectTreeNodeViewModel node && !node.ChildrenLoaded)
        {
            node.LoadChildren();
        }
    }

    private void ProjectExplorerTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is ProjectTreeNodeViewModel node)
        {
            node.OpenCommand.Execute(null);
            ViewModel.RefreshCommandState();
            OpenAllDocumentsInEditor();
        }
    }
}
