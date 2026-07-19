using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Martin.Build;

namespace Martin.Studio.Core;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IOutputService _outputService;
    private readonly IProjectOpeningService _projectOpeningService;
    private readonly IBuildCoordinator? _buildCoordinator;
    private readonly IExecutionCoordinator? _executionCoordinator;
    private readonly IDiagnosticService? _diagnosticService;
    private readonly ISettingsService? _settingsService;
    private readonly IProjectCreationService? _projectCreationService;
    private readonly IEditorHost? _editorHost;
    private readonly IFileDialogService? _fileDialogService;
    private readonly IMessageDialogService? _messageDialogService;
    private readonly IProjectOpeningService? _sessionPersistence;
    private CancellationTokenSource? _viewStateSaveDebounce;
    private static readonly TimeSpan ViewStateSaveDelay = TimeSpan.FromMilliseconds(500);

    public MainWindowViewModel(
        IWorkspaceService workspaceService,
        ProjectExplorerViewModel projectExplorer,
        DocumentTabsViewModel documentTabs,
        OutputPaneViewModel outputPane,
        ErrorListViewModel errorList,
        StatusBarViewModel statusBar,
        IOutputService outputService,
        IProjectOpeningService projectOpeningService,
        IBuildCoordinator? buildCoordinator = null,
        IExecutionCoordinator? executionCoordinator = null,
        IDiagnosticService? diagnosticService = null,
        ISettingsService? settingsService = null,
        IProjectCreationService? projectCreationService = null,
        IEditorHost? editorHost = null,
        IFileDialogService? fileDialogService = null,
        IMessageDialogService? messageDialogService = null)
    {
        _workspaceService = workspaceService;
        _outputService = outputService;
        _projectOpeningService = projectOpeningService;
        _buildCoordinator = buildCoordinator;
        _executionCoordinator = executionCoordinator;
        _diagnosticService = diagnosticService;
        _settingsService = settingsService;
        _projectCreationService = projectCreationService;
        _editorHost = editorHost;
        _fileDialogService = fileDialogService;
        _messageDialogService = messageDialogService;
        _sessionPersistence = _projectOpeningService;
        ProjectExplorer = projectExplorer;
        DocumentTabs = documentTabs;
        OutputPane = outputPane;
        ErrorList = errorList;
        StatusBar = statusBar;
        WriteOutputCommand = new RelayCommand(WriteStartupOutput);
        OpenProjectCommand = new AsyncRelayCommand<string>(OpenProjectAsync);
        CreateProjectCommand = new AsyncRelayCommand<StudioProjectCreationRequest>(CreateProjectAsync);
        OpenRecentProjectCommand = new AsyncRelayCommand<RecentProjectEntry>(OpenRecentProjectAsync);
        CloseProjectCommand = new AsyncRelayCommand(CloseProjectAsync);
        ActivateDocumentCommand = new RelayCommand<Guid>(ActivateDocument);
        CloseDocumentCommand = new AsyncRelayCommand<Guid>(CloseDocumentAsync);
        CloseAllDocumentsCommand = new AsyncRelayCommand(CloseAllDocumentsAsync, CanCloseActiveDocument);
        CloseOtherDocumentsCommand = new AsyncRelayCommand(CloseOtherDocumentsAsync, CanCloseOtherDocuments);
        SaveCommand = new AsyncRelayCommand(SaveActiveDocumentAsync, CanSaveActiveDocument);
        SaveAsCommand = new AsyncRelayCommand(SaveActiveDocumentAsAsync, CanSaveActiveDocumentAs);
        SaveAllCommand = new AsyncRelayCommand(SaveAllAsync, CanSaveAll);
        CloseActiveDocumentCommand = new RelayCommand(CloseActiveDocument, CanCloseActiveDocument);
        NextDocumentCommand = new RelayCommand(ActivateNextDocument, CanNavigateDocuments);
        PreviousDocumentCommand = new RelayCommand(ActivatePreviousDocument, CanNavigateDocuments);
        NavigateToDiagnosticCommand = new AsyncRelayCommand<StudioDiagnostic>(NavigateToDiagnosticAsync);
        NextDiagnosticCommand = new RelayCommand(NavigateToNextDiagnostic, CanNavigateDiagnostics);
        PreviousDiagnosticCommand = new RelayCommand(NavigateToPreviousDiagnostic, CanNavigateDiagnostics);
        BuildCommand = new AsyncRelayCommand(BuildAsync, CanBuild);
        CancelBuildCommand = new RelayCommand(CancelBuild, CanCancelBuild);
        CleanCommand = new AsyncRelayCommand(CleanAsync, CanClean);
        RunCommand = new AsyncRelayCommand(RunAsync, CanRun);
        RunWithoutBuildCommand = new AsyncRelayCommand(RunWithoutBuildAsync, CanRunWithoutBuild);
        RunInExternalTerminalCommand = new AsyncRelayCommand(RunInExternalTerminalAsync, CanRun);
        StopCommand = new RelayCommand(Stop, CanStop);
        SetThemeCommand = new AsyncRelayCommand<StudioTheme>(SetThemeAsync);
        RefreshCommandState();
    }

    [ObservableProperty]
    private string applicationTitle = "Martin Studio";

    [ObservableProperty]
    private string activeProjectSummary = "No project open";

    [ObservableProperty]
    private StudioTheme currentTheme = StudioTheme.System;

    public ProjectExplorerViewModel ProjectExplorer { get; }
    public DocumentTabsViewModel DocumentTabs { get; }
    public OutputPaneViewModel OutputPane { get; }
    public ErrorListViewModel ErrorList { get; }
    public StatusBarViewModel StatusBar { get; }
    public IRelayCommand WriteOutputCommand { get; }
    public IAsyncRelayCommand<string> OpenProjectCommand { get; }
    public IAsyncRelayCommand<StudioProjectCreationRequest> CreateProjectCommand { get; }
    public IAsyncRelayCommand<RecentProjectEntry> OpenRecentProjectCommand { get; }
    public IAsyncRelayCommand CloseProjectCommand { get; }
    public IRelayCommand<Guid> ActivateDocumentCommand { get; }
    public IAsyncRelayCommand<Guid> CloseDocumentCommand { get; }
    public IAsyncRelayCommand CloseAllDocumentsCommand { get; }
    public IAsyncRelayCommand CloseOtherDocumentsCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand SaveAsCommand { get; }
    public IAsyncRelayCommand SaveAllCommand { get; }
    public IRelayCommand CloseActiveDocumentCommand { get; }
    public IRelayCommand NextDocumentCommand { get; }
    public IRelayCommand PreviousDocumentCommand { get; }
    public IAsyncRelayCommand<StudioDiagnostic> NavigateToDiagnosticCommand { get; }
    public IRelayCommand NextDiagnosticCommand { get; }
    public IRelayCommand PreviousDiagnosticCommand { get; }
    public IAsyncRelayCommand BuildCommand { get; }
    public IRelayCommand CancelBuildCommand { get; }
    public IAsyncRelayCommand CleanCommand { get; }
    public IAsyncRelayCommand RunCommand { get; }
    public IAsyncRelayCommand RunWithoutBuildCommand { get; }
    public IAsyncRelayCommand RunInExternalTerminalCommand { get; }
    public IRelayCommand StopCommand { get; }
    public IAsyncRelayCommand<StudioTheme> SetThemeCommand { get; }
    public IReadOnlyList<BuildConfiguration> BuildConfigurations { get; } = Enum.GetValues<BuildConfiguration>();

    public BuildConfiguration ActiveBuildConfiguration
    {
        get => _workspaceService.Workspace.ActiveBuildConfiguration;
        set => SetBuildConfiguration(value);
    }

    public void RefreshCommandState()
    {
        var project = _workspaceService.Workspace.Project;
        ActiveProjectSummary = project is null
                                   ? "No project open"
                                   : $"Project: {project.Manifest.Package.Name}";
        ApplicationTitle = project is null
                               ? "Martin Studio"
                               : $"{project.Manifest.Package.Name} - Martin Studio";
        OnPropertyChanged(nameof(ActiveBuildConfiguration));

        ProjectExplorer.Refresh(_workspaceService.Workspace.ProjectTree);
        DocumentTabs.Refresh(_workspaceService.Workspace.OpenDocuments, _workspaceService.Workspace.ActiveDocument);
        ErrorList.Refresh(_workspaceService.Workspace.Diagnostics);
        OutputPane.Refresh();
        StatusBar.Refresh(_workspaceService.Workspace);
        NotifyCommandCanExecuteChanged();
    }

    private void NotifyCommandCanExecuteChanged()
    {
        BuildCommand.NotifyCanExecuteChanged();
        CancelBuildCommand.NotifyCanExecuteChanged();
        CleanCommand.NotifyCanExecuteChanged();
        RunCommand.NotifyCanExecuteChanged();
        RunWithoutBuildCommand.NotifyCanExecuteChanged();
        RunInExternalTerminalCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
        SaveAllCommand.NotifyCanExecuteChanged();
        CloseActiveDocumentCommand.NotifyCanExecuteChanged();
        CloseDocumentCommand.NotifyCanExecuteChanged();
        CloseAllDocumentsCommand.NotifyCanExecuteChanged();
        CloseOtherDocumentsCommand.NotifyCanExecuteChanged();
        NextDocumentCommand.NotifyCanExecuteChanged();
        PreviousDocumentCommand.NotifyCanExecuteChanged();
        NextDiagnosticCommand.NotifyCanExecuteChanged();
        PreviousDiagnosticCommand.NotifyCanExecuteChanged();
    }

    private bool CanBuild() => _buildCoordinator is not null && _workspaceService.Workspace.Project is not null && _workspaceService.Workspace.BuildState == BuildState.Idle;
    private bool CanCancelBuild() => _buildCoordinator is not null && _workspaceService.Workspace.BuildState is BuildState.Building;
    private bool CanClean() => _buildCoordinator is not null && _workspaceService.Workspace.Project is not null && _workspaceService.Workspace.BuildState == BuildState.Idle;
    private bool CanRun() => _buildCoordinator is not null && _executionCoordinator is not null && _workspaceService.Workspace.Project is not null && _workspaceService.Workspace.BuildState == BuildState.Idle && _workspaceService.Workspace.ExecutionState == ExecutionState.Idle;
    private bool CanRunWithoutBuild() => _executionCoordinator is not null && _workspaceService.Workspace.Project is not null && _workspaceService.Workspace.ExecutionState == ExecutionState.Idle && !_workspaceService.Workspace.OpenDocuments.Any(d => d.IsDirty);
    private bool CanStop() => _executionCoordinator is not null && _workspaceService.Workspace.ExecutionState == ExecutionState.Running;
    private bool CanNavigateDiagnostics() => _workspaceService.Workspace.Diagnostics.Count > 0;
    private bool CanSaveActiveDocument() => _workspaceService.Workspace.ActiveDocument?.IsDirty == true;
    private bool CanSaveActiveDocumentAs() => _workspaceService.Workspace.ActiveDocument is not null;
    private bool CanSaveAll() => _workspaceService.Workspace.OpenDocuments.Any(d => d.IsDirty);
    private bool CanCloseActiveDocument() => _workspaceService.Workspace.ActiveDocument is not null;
    private bool CanNavigateDocuments() => _workspaceService.Workspace.OpenDocuments.Count > 1;
    private bool CanCloseOtherDocuments() => _workspaceService.Workspace.OpenDocuments.Count > 1 && _workspaceService.Workspace.ActiveDocument is not null;

    private async Task BuildAsync()
    {
        if (_buildCoordinator is null)
            return;
        await _buildCoordinator.BuildAsync();
        await ApplyDiagnosticsToEditorAsync();
        RefreshCommandState();
    }

    private void CancelBuild()
    {
        _buildCoordinator?.Cancel();
        RefreshCommandState();
    }

    private async Task CleanAsync()
    {
        if (_buildCoordinator is null)
            return;
        await _buildCoordinator.CleanAsync();
        await ApplyDiagnosticsToEditorAsync();
        RefreshCommandState();
    }

    private async Task RunAsync()
    {
        if (_buildCoordinator is null || _executionCoordinator is null)
            return;
        var build = await _buildCoordinator.BuildAsync();
        await ApplyDiagnosticsToEditorAsync();
        if (build.Success)
        {
            var runTask = _executionCoordinator.RunAsync(build, null, false);
            RefreshCommandState();
            await runTask;
        }
        RefreshCommandState();
    }

    private async Task RunInExternalTerminalAsync()
    {
        if (_buildCoordinator is null || _executionCoordinator is null)
            return;
        var build = await _buildCoordinator.BuildAsync();
        await ApplyDiagnosticsToEditorAsync();
        if (build.Success)
        {
            var runTask = _executionCoordinator.RunAsync(build, null, true);
            RefreshCommandState();
            await runTask;
        }
        RefreshCommandState();
    }

    private async Task RunWithoutBuildAsync()
    {
        if (_executionCoordinator is null)
            return;
        var freshness = _executionCoordinator.CreateFreshNoBuildResult();
        if (freshness.Completed && !string.IsNullOrWhiteSpace(freshness.StandardOutput))
        {
            var runTask = _executionCoordinator.RunAsync(new Martin.Build.BuildResult { Status = Martin.Build.BuildStatus.Succeeded, EntryPointPath = freshness.StandardOutput, OutputDirectory = System.IO.Path.GetDirectoryName(freshness.StandardOutput) }, null, false, true);
            RefreshCommandState();
            await runTask;
        }
        RefreshCommandState();
    }

    private void Stop()
    {
        _executionCoordinator?.Stop();
        RefreshCommandState();
    }

    private void SetBuildConfiguration(BuildConfiguration configuration)
    {
        if (_workspaceService.Workspace.ActiveBuildConfiguration == configuration)
            return;

        _workspaceService.Workspace.ActiveBuildConfiguration = configuration;
        OnPropertyChanged(nameof(ActiveBuildConfiguration));
        StatusBar.Refresh(_workspaceService.Workspace);
        _ = _sessionPersistence?.SaveSessionAsync();
    }

    private async Task OpenProjectAsync(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            await _projectOpeningService.OpenProjectAsync(path);
        RefreshCommandState();
    }

    private async Task CreateProjectAsync(StudioProjectCreationRequest? request)
    {
        if (request is null || _projectCreationService is null)
            return;

        if (request.OpenAfterCreation && !await _projectOpeningService.ApproveProjectTransitionAsync())
        {
            _outputService.Add(OutputChannel.ProjectSystem, OutputSeverity.Warning, $"Project creation canceled for {request.ProjectName} because the project transition was not approved.");
            RefreshCommandState();
            return;
        }

        _outputService.Add(OutputChannel.ProjectSystem, OutputSeverity.Info, $"Creating project {request.ProjectName} in {request.BaseDirectory}.");
        var result = await _projectCreationService.CreateAsync(request);
        foreach (var diagnostic in result.Diagnostics)
            _workspaceService.Workspace.Diagnostics.Add(diagnostic);

        if (!result.Success || string.IsNullOrWhiteSpace(result.ManifestPath))
        {
            _outputService.Add(OutputChannel.ProjectSystem, OutputSeverity.Error, $"Project creation failed for {request.ProjectName}.");
            RefreshCommandState();
            return;
        }

        _outputService.Add(OutputChannel.ProjectSystem, OutputSeverity.Info, $"Created project at {result.ProjectDirectory}.");
        if (request.OpenAfterCreation)
        {
            var openResult = await _projectOpeningService.OpenProjectAfterApprovedTransitionAsync(result.ManifestPath);
            if (openResult.Success)
            {
                var mainSource = Path.Combine(result.ProjectDirectory!, "Sources", "main.martin");
                if (File.Exists(mainSource))
                    _workspaceService.OpenDocument(mainSource);
            }
            else
            {
                _outputService.Add(OutputChannel.ProjectSystem, OutputSeverity.Error, $"Created project at {result.ProjectDirectory}, but opening it failed.");
            }
        }

        RefreshCommandState();
    }

    private async Task OpenRecentProjectAsync(RecentProjectEntry? entry)
    {
        if (entry is not null)
            await _projectOpeningService.OpenRecentProjectAsync(entry);
        RefreshCommandState();
    }

    private async Task CloseProjectAsync()
    {
        await _projectOpeningService.CloseProjectAsync();
        RefreshCommandState();
    }

    public void ApplyEditorTextChange(Guid id, string text)
    {
        _workspaceService.ApplyEditorChange(id, text);
        DocumentTabs.UpdateDocumentState(id);
        NotifyCommandCanExecuteChanged();
        StatusBar.Refresh(_workspaceService.Workspace);
    }

    public void UpdateEditorViewState(Guid id, EditorViewState viewState)
    {
        _workspaceService.UpdateViewState(id, viewState);
        ScheduleDebouncedSessionSave();
        RefreshCommandState();
    }

    private async Task SaveActiveDocumentAsync()
    {
        if (_workspaceService.Workspace.ActiveDocument is {} document)
        {
            await PullEditorTextAsync(document);
            await _workspaceService.SaveAsync(document);
            if (_sessionPersistence is not null)
                await _sessionPersistence.SaveSessionAsync();
        }
        RefreshCommandState();
    }

    private async Task SaveActiveDocumentAsAsync()
    {
        if (_workspaceService.Workspace.ActiveDocument is not {} document || _fileDialogService is null)
            return;
        await PullEditorTextAsync(document);
        var destination = await _fileDialogService.PickSaveFileAsync(document.DisplayName, Path.GetDirectoryName(document.FilePath));
        if (string.IsNullOrWhiteSpace(destination))
            return;
        var result = await _workspaceService.SaveAsAsync(document, destination);
        if (!result.Success && _messageDialogService is not null)
            await _messageDialogService.ShowErrorAsync("Save As failed", $"{result.FilePath}: {result.Error}");
        else if (result.Success)
        {
            if (_editorHost is not null)
                await _editorHost.OpenDocumentAsync(document);
            if (_sessionPersistence is not null)
                await _sessionPersistence.SaveSessionAsync();
        }
        RefreshCommandState();
    }

    private async Task SaveAllAsync()
    {
        foreach (var document in _workspaceService.Workspace.OpenDocuments.Where(d => d.IsDirty).ToArray())
            await PullEditorTextAsync(document);
        var result = await _workspaceService.SaveAllWithResultsAsync();
        var failures = result.Results.Where(r => !r.Success).ToArray();
        if (failures.Length > 0)
        {
            if (failures.FirstOrDefault() is {} first && _workspaceService.Workspace.OpenDocuments.FirstOrDefault(d => string.Equals(d.FilePath, first.FilePath, StringComparison.OrdinalIgnoreCase)) is {} failedDocument)
                _workspaceService.ActivateDocument(failedDocument.Id);
            if (_messageDialogService is not null)
                await _messageDialogService.ShowErrorAsync("Save All completed with errors", string.Join(Environment.NewLine, failures.Select(f => $"{f.FilePath}: {f.Error}")));
        }
        else if (_sessionPersistence is not null)
            await _sessionPersistence.SaveSessionAsync();
        RefreshCommandState();
    }

    private async Task PullEditorTextAsync(DocumentModel document)
    {
        if (_editorHost is null)
            return;
        var text = await _editorHost.GetDocumentTextAsync(document.Id);
        _workspaceService.ApplyEditorChange(document.Id, text);
    }

    private void CloseActiveDocument()
    {
        if (_workspaceService.Workspace.ActiveDocument is {} document)
            _ = CloseDocumentAsync(document.Id);
    }

    private void ActivateNextDocument() => ActivateDocumentByOffset(1);
    private void ActivatePreviousDocument() => ActivateDocumentByOffset(-1);

    private void ActivateDocumentByOffset(int offset)
    {
        var documents = _workspaceService.Workspace.OpenDocuments;
        if (documents.Count == 0)
            return;
        var active = _workspaceService.Workspace.ActiveDocument;
        var index = active is null ? 0 : documents.IndexOf(active);
        if (index < 0)
            index = 0;
        var next = (index + offset) % documents.Count;
        if (next < 0)
            next += documents.Count;
        ActivateDocument(documents[next].Id);
    }

    private void ActivateDocument(Guid id)
    {
        _workspaceService.ActivateDocument(id);
        _ = _sessionPersistence?.SaveSessionAsync();
        RefreshCommandState();
    }

    private void ScheduleDebouncedSessionSave()
    {
        if (_sessionPersistence is null)
            return;
        _viewStateSaveDebounce?.Cancel();
        var source = new CancellationTokenSource();
        _viewStateSaveDebounce = source;
        _ = SaveSessionAfterDelayAsync(source);
    }

    private async Task SaveSessionAfterDelayAsync(CancellationTokenSource source)
    {
        try
        {
            await Task.Delay(ViewStateSaveDelay, source.Token);
            await _sessionPersistence!.SaveSessionAsync(source.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_viewStateSaveDebounce, source))
                _viewStateSaveDebounce = null;
            source.Dispose();
        }
    }

    private async Task CloseDocumentAsync(Guid id)
    {
        var document = _workspaceService.Workspace.OpenDocuments.FirstOrDefault(d => d.Id == id);
        if (document is null)
            return;
        if (!await ConfirmCloseDocumentsAsync([document], UnsavedChangesContext.CloseDocument))
        {
            RefreshCommandState();
            return;
        }
        if (_editorHost is not null)
            await _editorHost.CloseDocumentAsync(id);
        _workspaceService.CloseDocument(id, force: true);
        if (_sessionPersistence is not null)
            await _sessionPersistence.SaveSessionAsync();
        RefreshCommandState();
    }

    public async Task<bool> CloseAllDocumentsAsync()
    {
        var documents = _workspaceService.Workspace.OpenDocuments.ToArray();
        if (!await ConfirmCloseDocumentsAsync(documents, UnsavedChangesContext.CloseAllDocuments))
        {
            RefreshCommandState();
            return false;
        }
        foreach (var document in documents)
        {
            if (_editorHost is not null)
                await _editorHost.CloseDocumentAsync(document.Id);
            _workspaceService.CloseDocument(document.Id, force: true);
        }
        if (_sessionPersistence is not null)
            await _sessionPersistence.SaveSessionAsync();
        RefreshCommandState();
        return true;
    }

    public async Task<bool> CloseOtherDocumentsAsync()
    {
        var active = _workspaceService.Workspace.ActiveDocument;
        if (active is null)
            return true;
        var documents = _workspaceService.Workspace.OpenDocuments.Where(d => d.Id != active.Id).ToArray();
        if (!await ConfirmCloseDocumentsAsync(documents, UnsavedChangesContext.CloseAllDocuments))
        {
            RefreshCommandState();
            return false;
        }
        foreach (var document in documents)
        {
            if (_editorHost is not null)
                await _editorHost.CloseDocumentAsync(document.Id);
            _workspaceService.CloseDocument(document.Id, force: true);
        }
        if (_sessionPersistence is not null)
            await _sessionPersistence.SaveSessionAsync();
        RefreshCommandState();
        return true;
    }

    public async Task<StudioShutdownResult> ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (_workspaceService.Workspace.ExecutionState is ExecutionState.Running or ExecutionState.ExternalLaunching or ExecutionState.Stopping)
            _executionCoordinator?.Stop();
        if (_workspaceService.Workspace.BuildState is BuildState.Building or BuildState.Cancelling)
            _buildCoordinator?.Cancel();
        var documents = _workspaceService.Workspace.OpenDocuments.ToArray();
        if (!await ConfirmCloseDocumentsAsync(documents, UnsavedChangesContext.ExitApplication, cancellationToken))
            return new StudioShutdownResult(StudioTransitionStatus.Cancelled, "Shutdown canceled because documents have unsaved changes.");
        if (_sessionPersistence is not null)
        {
            _viewStateSaveDebounce?.Cancel();
            await _sessionPersistence.SaveSessionAsync(cancellationToken);
        }
        foreach (var document in documents)
        {
            if (_editorHost is not null)
                await _editorHost.CloseDocumentAsync(document.Id, cancellationToken);
            _workspaceService.CloseDocument(document.Id, force: true);
        }
        await _projectOpeningService.CloseProjectAsync(cancellationToken);
        RefreshCommandState();
        return new StudioShutdownResult(StudioTransitionStatus.Succeeded);
    }

    private async Task<bool> ConfirmCloseDocumentsAsync(IReadOnlyList<DocumentModel> documents, UnsavedChangesContext context, CancellationToken cancellationToken = default)
    {
        if (documents.Count == 0)
            return true;
        foreach (var document in documents)
            await PullEditorTextAsync(document);
        var dirty = documents.Where(d => d.IsDirty).ToArray();
        if (dirty.Length == 0)
            return true;
        var decision = _messageDialogService is null ? UnsavedChangesDecision.Cancel : await _messageDialogService.ConfirmUnsavedChangesAsync(dirty, context, cancellationToken);
        if (decision == UnsavedChangesDecision.Cancel)
            return false;
        if (decision == UnsavedChangesDecision.Discard)
            return true;
        foreach (var document in dirty)
        {
            var result = await _workspaceService.SaveAsAsync(document, document.FilePath, cancellationToken);
            if (!result.Success)
            {
                _workspaceService.ActivateDocument(document.Id);
                if (_messageDialogService is not null)
                    await _messageDialogService.ShowErrorAsync("Save failed", $"{result.FilePath}: {result.Error}", cancellationToken);
                return false;
            }
        }
        return true;
    }

    private async Task NavigateToDiagnosticAsync(StudioDiagnostic? diagnostic)
    {
        if (diagnostic?.FilePath is null || diagnostic.Range is null)
            return;
        if (!_workspaceService.TryNavigateToDocument(diagnostic.FilePath, diagnostic.Range, out var document) || document is null)
            return;
        RefreshCommandState();
        if (_editorHost is not null)
        {
            await _editorHost.OpenDocumentAsync(document);
            if (_diagnosticService is not null)
                await _editorHost.SetDiagnosticsAsync(document.Id, _diagnosticService.ToEditor(document.Id));
            await _editorHost.ActivateDocumentAsync(document.Id);
            await _editorHost.RevealRangeAsync(document.Id, diagnostic.Range, select: true);
            await _editorHost.FocusEditorAsync();
        }
    }

    private async void NavigateToNextDiagnostic() => await NavigateToDiagnosticAsync(_diagnosticService?.Next(ErrorList.Filter) ?? ErrorList.Next());
    private async void NavigateToPreviousDiagnostic() => await NavigateToDiagnosticAsync(_diagnosticService?.Previous(ErrorList.Filter) ?? ErrorList.Previous());

    public async Task ApplyDiagnosticsToEditorAsync(CancellationToken cancellationToken = default)
    {
        if (_editorHost is null || _diagnosticService is null)
            return;
        foreach (var document in _workspaceService.Workspace.OpenDocuments)
            await _editorHost.SetDiagnosticsAsync(document.Id, _diagnosticService.ToEditor(document.Id), cancellationToken);
    }

    private async Task SetThemeAsync(StudioTheme theme)
    {
        CurrentTheme = theme;
        if (_settingsService is not null)
        {
            var settings = await _settingsService.LoadAsync();
            await _settingsService.SaveAsync(settings with { Theme = theme });
        }
        _outputService.Add(OutputChannel.Studio, OutputSeverity.Info, $"Theme changed to {theme}.");
        OutputPane.Refresh();
    }

    public async Task LoadSettingsAsync()
    {
        if (_settingsService is null)
            return;
        CurrentTheme = (await _settingsService.LoadAsync()).Theme;
    }

    private void WriteStartupOutput()
    {
        _outputService.Add(OutputChannel.Studio, OutputSeverity.Info, "Martin Studio MVVM shell initialized.");
        OutputPane.Refresh();
    }
}

public sealed partial class ProjectExplorerViewModel : ObservableObject
{
    private readonly IWorkspaceService? _workspaceService;
    private readonly IClipboardService? _clipboardService;
    private readonly IFileRevealService? _fileRevealService;

    public ProjectExplorerViewModel(IWorkspaceService? workspaceService = null, IClipboardService? clipboardService = null, IFileRevealService? fileRevealService = null)
    {
        _workspaceService = workspaceService;
        _clipboardService = clipboardService;
        _fileRevealService = fileRevealService;
        RefreshCommand = new RelayCommand(RefreshProjectExplorer);
    }

    public ObservableCollection<ProjectTreeNodeViewModel> Roots { get; } = [];
    public IRelayCommand RefreshCommand { get; }

    public void Refresh(ProjectTreeNode? root)
    {
        Roots.Clear();
        if (root is not null)
            Roots.Add(new ProjectTreeNodeViewModel(root, _workspaceService, _clipboardService, _fileRevealService));
    }

    private void RefreshProjectExplorer()
    {
        _workspaceService?.RefreshProjectTree();
        Refresh(_workspaceService?.Workspace.ProjectTree);
    }
}

public sealed partial class ProjectTreeNodeViewModel : ObservableObject
{
    private readonly ProjectTreeNode _node;
    private readonly IWorkspaceService? _workspaceService;
    private readonly IClipboardService? _clipboardService;
    private readonly IFileRevealService? _fileRevealService;

    public ProjectTreeNodeViewModel(ProjectTreeNode node, IWorkspaceService? workspaceService = null, IClipboardService? clipboardService = null, IFileRevealService? fileRevealService = null)
    {
        _node = node;
        _workspaceService = workspaceService;
        _clipboardService = clipboardService;
        _fileRevealService = fileRevealService;
        Name = node.Name;
        FullPath = node.FullPath;
        Kind = node.Kind;
        LoadChildrenCommand = new RelayCommand(LoadChildren);
        OpenCommand = new RelayCommand(() => _workspaceService?.TryOpenProjectTreeNode(_node, out _));
        CopyPathCommand = new AsyncRelayCommand(CopyPathAsync);
        RevealCommand = new AsyncRelayCommand(RevealAsync);
        RefreshCommand = new RelayCommand(RefreshNode);
        RefreshFromNode();
    }

    public string Name { get; }
    public string FullPath { get; }
    public ProjectNodeKind Kind { get; }
    public bool ChildrenLoaded => _node.ChildrenLoaded;
    public string? CopiedPath { get; private set; }
    public bool WasRevealRequested { get; private set; }
    public ObservableCollection<ProjectTreeNodeViewModel> Children { get; } = [];
    public IRelayCommand LoadChildrenCommand { get; }
    public IRelayCommand OpenCommand { get; }
    public IRelayCommand CopyPathCommand { get; }
    public IRelayCommand RevealCommand { get; }
    public IRelayCommand RefreshCommand { get; }

    public void LoadChildren()
    {
        _workspaceService?.LoadProjectTreeChildren(_node);
        RefreshFromNode();
        OnPropertyChanged(nameof(ChildrenLoaded));
    }

    private void RefreshNode()
    {
        _node.ChildrenLoaded = false;
        LoadChildren();
    }

    private async Task CopyPathAsync()
    {
        CopiedPath = _workspaceService?.CopyProjectTreeNodePath(_node) ?? FullPath;
        if (_clipboardService is not null)
            await _clipboardService.SetTextAsync(CopiedPath);
    }

    private async Task RevealAsync()
    {
        WasRevealRequested = _workspaceService?.RevealProjectTreeNode(_node) == true;
        if (WasRevealRequested && _fileRevealService is not null)
            await _fileRevealService.RevealAsync(FullPath);
    }

    private void RefreshFromNode()
    {
        Children.Clear();
        foreach (var child in _node.Children)
            Children.Add(new ProjectTreeNodeViewModel(child, _workspaceService, _clipboardService, _fileRevealService));
    }
}

public sealed partial class DocumentTabsViewModel : ObservableObject
{
    [ObservableProperty]
    private DocumentViewModel? activeDocument;

    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    public void Refresh(IEnumerable<DocumentModel> documents, DocumentModel? active)
    {
        var models = documents.ToArray();
        var modelIds = models.Select(document => document.Id).ToHashSet();
        foreach (var stale in Documents.Where(document => !modelIds.Contains(document.Id)).ToArray())
            Documents.Remove(stale);

        for (var index = 0; index < models.Length; index++)
        {
            var document = Documents.FirstOrDefault(candidate => candidate.Id == models[index].Id);
            if (document is null)
            {
                Documents.Insert(index, new DocumentViewModel(models[index]));
            }
            else
            {
                var currentIndex = Documents.IndexOf(document);
                if (currentIndex != index)
                    Documents.Move(currentIndex, index);
                document.NotifyStateChanged();
            }
        }

        ActiveDocument = active is null ? null : Documents.FirstOrDefault(d => d.Id == active.Id);
    }

    public void UpdateDocumentState(Guid id)
    {
        Documents.FirstOrDefault(document => document.Id == id)?.NotifyStateChanged();
    }
}

public sealed partial class DocumentViewModel(DocumentModel document) : ObservableObject
{
    public DocumentModel Model => document;
    public Guid Id => document.Id;
    public string DisplayName => document.DisplayName;
    public string FilePath => document.FilePath;
    public string Text => document.Text;
    public DocumentVersion Version => document.Version;
    public EditorViewState ViewState => document.ViewState;
    public string Header => document.IsDirty ? DisplayName + " *" : DisplayName;
    public string AccessibilityName => document.IsDirty ? $"{DisplayName}, modified" : DisplayName;
    public bool IsDirty => document.IsDirty;

    public void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(Version));
        OnPropertyChanged(nameof(ViewState));
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(AccessibilityName));
        OnPropertyChanged(nameof(IsDirty));
    }
}

public sealed partial class OutputPaneViewModel(IOutputService outputService) : ObservableObject
{
    public ObservableCollection<OutputEntry> Entries { get; } = [];
    public OutputFilter Filter { get; private set; } = new();
    public bool AutoScroll { get; private set; } = true;

    public void SetFilter(OutputFilter filter)
    {
        Filter = filter;
        Refresh();
    }
    public void SetAutoScroll(bool autoScroll) => AutoScroll = autoScroll;
    public void Clear()
    {
        outputService.Clear();
        Refresh();
    }
    public string CopyAll() => outputService.CopyAll(Filter);
    public Task SaveLogAsync(string path, CancellationToken cancellationToken = default) => outputService.SaveLogAsync(path, Filter, cancellationToken);

    public void Refresh()
    {
        Entries.Clear();
        foreach (var entry in outputService.GetEntries(Filter))
            Entries.Add(entry);
    }
}

public sealed partial class ErrorListViewModel : ObservableObject
{
    private int _navigationIndex = -1;
    private StudioDiagnostic[] _allDiagnostics = [];
    public ObservableCollection<StudioDiagnosticViewModel> Diagnostics { get; } = [];
    public DiagnosticFilter Filter { get; private set; } = new();

    public void SetFilter(DiagnosticFilter filter)
    {
        Filter = filter;
        ApplyFilter();
    }
    public string CopyAll() => string.Join(Environment.NewLine, Diagnostics.Select(d => d.AccessibleText));
    public StudioDiagnostic? Next() => Navigate(1)?.Diagnostic;
    public StudioDiagnostic? Previous() => Navigate(-1)?.Diagnostic;

    public void Refresh(IEnumerable<StudioDiagnostic> diagnostics)
    {
        _allDiagnostics = diagnostics.ToArray();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Diagnostics.Clear();
        foreach (var diagnostic in _allDiagnostics.Where(Matches).OrderByDescending(d => d.Severity).ThenBy(d => d.FilePath ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(d => d.Range?.StartLine ?? 0).ThenBy(d => d.Range?.StartColumn ?? 0).ThenBy(d => d.Code, StringComparer.OrdinalIgnoreCase))
            Diagnostics.Add(new StudioDiagnosticViewModel(diagnostic));
        if (_navigationIndex >= Diagnostics.Count)
            _navigationIndex = Diagnostics.Count - 1;
    }

    private StudioDiagnosticViewModel? Navigate(int delta)
    {
        if (Diagnostics.Count == 0)
            return null;
        _navigationIndex = (_navigationIndex + delta) % Diagnostics.Count;
        if (_navigationIndex < 0)
            _navigationIndex += Diagnostics.Count;
        return Diagnostics[_navigationIndex];
    }
    private bool Matches(StudioDiagnostic d) => (Filter.Severities is null || Filter.Severities.Contains(d.Severity)) && (Filter.Sources is null || Filter.Sources.Contains(d.Source)) && (string.IsNullOrWhiteSpace(Filter.Text) || d.Message.Contains(Filter.Text, StringComparison.OrdinalIgnoreCase) || d.Code.Contains(Filter.Text, StringComparison.OrdinalIgnoreCase) || (d.FilePath?.Contains(Filter.Text, StringComparison.OrdinalIgnoreCase) ?? false) || d.Source.Contains(Filter.Text, StringComparison.OrdinalIgnoreCase));
}

public sealed class StudioDiagnosticViewModel(StudioDiagnostic diagnostic)
{
    public StudioDiagnostic Diagnostic { get; } = diagnostic;
    public OutputSeverity Severity => Diagnostic.Severity;
    public string Code => Diagnostic.Code;
    public string Message => Diagnostic.Message;
    public string? FilePath => Diagnostic.FilePath;
    public string FileName => FilePath is null ? string.Empty : Path.GetFileName(FilePath);
    public TextRange? Range => Diagnostic.Range;
    public int? Line => Range?.StartLine;
    public int? Column => Range?.StartColumn;
    public string Source => Diagnostic.Source;
    public string LocationText => Range is null ? "project" : $"line {Range.StartLine}, column {Range.StartColumn}";
    public string AccessibleText => $"{Severity} {Code}: {Message}. Location: {(FilePath is null ? "project" : Path.GetFileName(FilePath))}, {LocationText}. Source: {Source}.";
}

public sealed partial class RecentProjectsViewModel(IRecentProjectService recentProjectService) : ObservableObject
{
    public ObservableCollection<RecentProjectEntry> Items { get; } = [];
    public void Refresh()
    {
        Items.Clear();
        foreach (var item in recentProjectService.Items)
            Items.Add(item);
    }
}

public sealed partial class StatusBarViewModel : ObservableObject
{
    [ObservableProperty]
    private string text = "Ready";

    public void Refresh(StudioWorkspace workspace)
    {
        Text = workspace.Project is null
                   ? "Ready"
                   : $"{workspace.OpenDocuments.Count} document(s), build {workspace.BuildState}, run {workspace.ExecutionState}";
    }
}
