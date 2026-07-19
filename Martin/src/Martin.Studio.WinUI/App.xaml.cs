using Martin.Build;
using Martin.Execution;
using Martin.Studio.Core;
using Martin.Services;
using Microsoft.UI.Dispatching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.UI.ViewManagement;

namespace Martin;

/// <summary>
/// Provides application-specific behavior and the Studio composition root.
/// </summary>
public partial class App : Application
{
    private MainWindow? _window;
    private readonly UISettings _uiSettings = new();
    private readonly AccessibilitySettings _accessibilitySettings = new();
    private MainWindowViewModel? _mainViewModel;

    public App()
    {
        Services = ConfigureServices();
        UnhandledException += App_UnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        InitializeComponent();
        SubscribeToSystemThemeChanges();
    }

    public IServiceProvider Services { get; }

    private void SubscribeToSystemThemeChanges()
    {
        TrySubscribeSystemThemeChange(() => _uiSettings.ColorValuesChanged += (_, _) => NotifySystemThemeChanged());
        TrySubscribeSystemThemeChange(() => _accessibilitySettings.HighContrastChanged += (_, _) => NotifySystemThemeChanged());
    }

    private void TrySubscribeSystemThemeChange(Action subscribe)
    {
        try
        {
            subscribe();
        }
        catch (COMException exception)
        {
            ReportUnexpectedFailure(exception);
        }
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ReportUnexpectedFailure(e.Exception);
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        ReportUnexpectedFailure(e.ExceptionObject as Exception);
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        ReportUnexpectedFailure(e.Exception);
    }

    private void ReportUnexpectedFailure(Exception? exception)
    {
        var recovery = Services.GetService<IStudioRecoveryService>();
        recovery?.Recover(StudioRecoveryKind.UnexpectedException, "UnhandledException", exception);
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IStudioLogService>(_ => new FileStudioLogService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Martin", "Studio", "Logs")));
        services.AddSingleton<IStudioRecoveryService, StudioRecoveryService>();
        services.AddSingleton<WorkspaceService>();
        services.AddSingleton<IWorkspaceService>(provider => provider.GetRequiredService<WorkspaceService>());
        services.AddSingleton(provider => provider.GetRequiredService<WorkspaceService>().Workspace);
        services.AddSingleton<IRecentProjectService>(_ => new RecentProjectService());
        services.AddSingleton<IOutputService>(_ => new OutputService());
        services.AddSingleton<ISettingsService>(provider => new JsonSettingsService(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Martin", "Studio", "settings.json"),
            provider.GetRequiredService<IStudioRecoveryService>()));
        services.AddSingleton<ISessionService>(provider => new JsonSessionService(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Martin", "Studio", "session.json"),
            provider.GetRequiredService<IStudioRecoveryService>()));
        services.AddSingleton<IProjectOpeningService>(provider => new ProjectOpeningService(
            provider.GetRequiredService<IWorkspaceService>(),
            provider.GetRequiredService<IRecentProjectService>(),
            provider.GetRequiredService<ISettingsService>(),
            provider.GetRequiredService<IOutputService>(),
            provider.GetService<IDirtyProjectTransitionPolicy>(),
            provider.GetService<ISessionService>(),
            provider.GetRequiredService<IStudioLogService>()));
        services.AddSingleton<IProjectCreationService>(provider => new ProjectCreationService(provider.GetRequiredService<IStudioLogService>()));
        services.AddSingleton<IFileDialogService>(_ => new WinUIFileDialogService(IntPtr.Zero));
        services.AddSingleton<IMessageDialogService, WinUIMessageDialogService>();
        services.AddSingleton<IUiDispatcher>(_ => new WinUIUiDispatcher(DispatcherQueue.GetForCurrentThread()));
        services.AddSingleton<IClipboardService, WinUIClipboardService>();
        services.AddSingleton<IFileRevealService, WindowsFileRevealService>();
        services.AddSingleton<EditorHostProxy>();
        services.AddSingleton<IEditorHost>(provider => provider.GetRequiredService<EditorHostProxy>());
        services.AddSingleton<IDirtyProjectTransitionPolicy, WinUIDirtyProjectTransitionPolicy>();
        services.AddSingleton<IExternalChangePolicy, WinUIExternalChangePolicy>();
        services.AddSingleton<IDiagnosticService>(provider => new DiagnosticService(
            provider.GetRequiredService<IWorkspaceService>().Workspace));
        services.AddSingleton<IBuildCoordinator>(provider => new BuildCoordinator(
            provider.GetRequiredService<IWorkspaceService>().Workspace,
            (OutputService)provider.GetRequiredService<IOutputService>(),
            provider.GetRequiredService<IMartinBuildService>(),
            provider.GetRequiredService<IStudioLogService>()));
        services.AddSingleton<IExecutionCoordinator>(provider => new ExecutionCoordinator(
            provider.GetRequiredService<IWorkspaceService>().Workspace,
            (OutputService)provider.GetRequiredService<IOutputService>(),
            provider.GetRequiredService<IMartinExecutionService>(),
            provider.GetRequiredService<IStudioLogService>()));

        services.AddSingleton<IMartinBuildService, MartinBuildService>();
        services.AddSingleton<IExternalTerminalLauncher, WindowsExternalTerminalLauncher>();
        services.AddSingleton<IMartinExecutionService>(provider => new MartinExecutionService(
            provider.GetRequiredService<IExternalTerminalLauncher>()));
        services.AddSingleton<Martin.LanguageServices.MartinLanguageService>();
        services.AddSingleton(_ => EditorAssetValidator.Validate(Path.Combine(AppContext.BaseDirectory, "Assets", "Editor")));
        services.AddSingleton<StudioLanguageProvider>();

        services.AddSingleton<ProjectExplorerViewModel>();
        services.AddSingleton<DocumentTabsViewModel>();
        services.AddSingleton<OutputPaneViewModel>();
        services.AddSingleton<ErrorListViewModel>();
        services.AddSingleton<RecentProjectsViewModel>();
        services.AddSingleton<StatusBarViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = Services.GetRequiredService<MainWindow>();
        var assetValidation = Services.GetRequiredService<EditorAssetValidationResult>();
        if (!assetValidation.IsValid)
        {
            var output = Services.GetRequiredService<IOutputService>();
            output.Add(OutputChannel.Studio, OutputSeverity.Error, "MRT5201 Editor assets are missing: " + string.Join(", ", assetValidation.Issues.Select(issue => issue.RelativePath)));
        }

        var opener = Services.GetRequiredService<IProjectOpeningService>();
        var viewModel = Services.GetRequiredService<MainWindowViewModel>();
        _mainViewModel = viewModel;
        await viewModel.LoadSettingsAsync();
        ApplyTheme(viewModel.CurrentTheme);
        viewModel.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainWindowViewModel.CurrentTheme)) ApplyTheme(viewModel.CurrentTheme); };
        await opener.ReopenLastProjectAsync();
        viewModel.RefreshCommandState();
        await _window.SynchronizeEditorDocumentsAsync();
        _window.Activate();
    }

    private void NotifySystemThemeChanged()
    {
        if (_mainViewModel?.CurrentTheme != StudioTheme.System) return;
        _window?.DispatcherQueue.TryEnqueue(() =>
        {
            ApplyTheme(StudioTheme.System);
            if (_window is MainWindow mainWindow)
                mainWindow.ApplyCurrentThemeToEditor();
        });
    }

    private static void ApplyTheme(StudioTheme theme)
    {
        if (Current is not App app || app._window?.Content is not FrameworkElement root) return;
        root.RequestedTheme = theme switch
        {
            StudioTheme.Light => ElementTheme.Light,
            StudioTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }
}
