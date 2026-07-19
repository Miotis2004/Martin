using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Martin.Studio.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Martin.Services;

public sealed class WinUIFileDialogService(IntPtr windowHandle) : IFileDialogService
{
    private IntPtr _windowHandle = windowHandle;

    public void SetWindowHandle(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            throw new ArgumentException("A valid window handle is required.", nameof(handle));

        _windowHandle = handle;
    }

    private IntPtr WindowHandle => _windowHandle != IntPtr.Zero
        ? _windowHandle
        : throw new InvalidOperationException("The WinUI file dialog service must be initialized with a window handle before showing pickers.");

    public async Task<string?> PickProjectManifestAsync(CancellationToken cancellationToken = default)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowHandle);
        picker.FileTypeFilter.Add(".toml");
        var file = await picker.PickSingleFileAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return file?.Path;
    }

    public async Task<string?> PickProjectFolderAsync(CancellationToken cancellationToken = default) => await PickFolderAsync(cancellationToken);
    public async Task<string?> PickProjectBaseFolderAsync(CancellationToken cancellationToken = default) => await PickFolderAsync(cancellationToken);

    public async Task<string?> PickSaveFileAsync(string suggestedFileName, string? initialDirectory = null, CancellationToken cancellationToken = default)
    {
        var picker = new FileSavePicker { SuggestedFileName = suggestedFileName };
        InitializeWithWindow.Initialize(picker, WindowHandle);
        picker.FileTypeChoices.Add("Martin source", [".martin"]);
        var file = await picker.PickSaveFileAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return file?.Path;
    }

    async Task<string?> PickFolderAsync(CancellationToken cancellationToken)
    {
        var picker = new FolderPicker();
        InitializeWithWindow.Initialize(picker, WindowHandle);
        picker.FileTypeFilter.Add("*");
        var folder = await picker.PickSingleFolderAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return folder?.Path;
    }
}

public sealed class WinUIMessageDialogService : IMessageDialogService
{
    public static Microsoft.UI.Xaml.XamlRoot? DialogXamlRoot { get; set; }

    public async Task<UnsavedChangesDecision> ConfirmUnsavedChangesAsync(IReadOnlyList<DocumentModel> documents, UnsavedChangesContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var names = string.Join(Environment.NewLine, documents.Take(8).Select(d => "• " + d.DisplayName));
        if (documents.Count > 8) names += Environment.NewLine + $"• ...and {documents.Count - 8} more";
        var dialog = new ContentDialog
        {
            Title = context == UnsavedChangesContext.ExitApplication ? "Save changes before exiting?" : "Save changes?",
            Content = $"The following document(s) have unsaved changes:" + Environment.NewLine + Environment.NewLine + names,
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Don't Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = DialogXamlRoot
        };
        var result = await dialog.ShowAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return result switch
        {
            ContentDialogResult.Primary => UnsavedChangesDecision.Save,
            ContentDialogResult.Secondary => UnsavedChangesDecision.Discard,
            _ => UnsavedChangesDecision.Cancel
        };
    }
    public async Task<ExternalChangeDecision> ConfirmExternalChangeAsync(DocumentModel document, ExternalChangeKind kind, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!document.IsDirty)
        {
            var dialog = new ContentDialog
            {
                Title = ExternalChangeTitle(document, kind),
                Content = ExternalChangeMessage(document, kind, "Reload the file from disk or keep the editor's current view?"),
                PrimaryButtonText = "Reload",
                SecondaryButtonText = "Keep Current View",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = DialogXamlRoot
            };
            var result = await dialog.ShowAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return result switch
            {
                ContentDialogResult.Primary => ExternalChangeDecision.Reload,
                ContentDialogResult.Secondary => ExternalChangeDecision.KeepCurrentView,
                _ => ExternalChangeDecision.Cancel
            };
        }

        var keepEditor = new RadioButton { Content = "Keep Editor Version", IsChecked = true, Margin = new Thickness(0, 8, 0, 0) };
        var reloadDisk = new RadioButton { Content = "Reload Disk Version", Margin = new Thickness(0, 8, 0, 0) };
        var saveAs = new RadioButton { Content = "Save As", Margin = new Thickness(0, 8, 0, 0) };
        var content = new StackPanel { Spacing = 4 };
        content.Children.Add(new TextBlock
        {
            Text = ExternalChangeMessage(document, kind, "The editor has unsaved changes. Choose how to resolve the disk change."),
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(keepEditor);
        content.Children.Add(reloadDisk);
        content.Children.Add(saveAs);

        var dirtyDialog = new ContentDialog
        {
            Title = ExternalChangeTitle(document, kind),
            Content = content,
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = DialogXamlRoot
        };
        var dirtyResult = await dirtyDialog.ShowAsync();
        cancellationToken.ThrowIfCancellationRequested();
        if (dirtyResult != ContentDialogResult.Primary)
            return ExternalChangeDecision.Cancel;
        if (reloadDisk.IsChecked == true)
            return ExternalChangeDecision.ReloadDiskVersion;
        if (saveAs.IsChecked == true)
            return ExternalChangeDecision.SaveAs;
        return ExternalChangeDecision.KeepEditorVersion;
    }

    private static string ExternalChangeTitle(DocumentModel document, ExternalChangeKind kind) => kind switch
    {
        ExternalChangeKind.Deleted => $"'{document.DisplayName}' was deleted outside Martin Studio",
        ExternalChangeKind.Renamed => $"'{document.DisplayName}' was renamed outside Martin Studio",
        _ => $"'{document.DisplayName}' changed outside Martin Studio"
    };

    private static string ExternalChangeMessage(DocumentModel document, ExternalChangeKind kind, string prompt)
    {
        var action = kind switch
        {
            ExternalChangeKind.Deleted => "deleted",
            ExternalChangeKind.Renamed => "renamed",
            _ => "changed"
        };
        return $"The file was {action} on disk:" + Environment.NewLine + document.FilePath + Environment.NewLine + Environment.NewLine + prompt;
    }
    public async Task ShowErrorAsync(string title, string message, CancellationToken cancellationToken = default) => await ShowAsync(title, message, cancellationToken);
    public async Task ShowInformationAsync(string title, string message, CancellationToken cancellationToken = default) => await ShowAsync(title, message, cancellationToken);
    static async Task ShowAsync(string title, string message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dialog = new ContentDialog { Title = title, Content = message, CloseButtonText = "OK", XamlRoot = WinUIMessageDialogService.DialogXamlRoot };
        await dialog.ShowAsync();
    }
}

public sealed class WinUIUiDispatcher(DispatcherQueue dispatcherQueue) : IUiDispatcher
{
    public bool HasThreadAccess => dispatcherQueue.HasThreadAccess;
    public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        if (HasThreadAccess) { action(); return Task.CompletedTask; }
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcherQueue.TryEnqueue(() => { try { cancellationToken.ThrowIfCancellationRequested(); action(); tcs.SetResult(); } catch (Exception ex) { tcs.SetException(ex); } }))
            tcs.SetException(new InvalidOperationException("Could not enqueue UI action."));
        return tcs.Task;
    }
    public Task InvokeAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        if (HasThreadAccess) return action();
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }))
            tcs.SetException(new InvalidOperationException("Could not enqueue UI action."));
        return tcs.Task;
    }
    public async Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken = default)
    {
        T? result = default;
        await InvokeAsync(() => result = action(), cancellationToken);
        return result!;
    }
}

public sealed class WinUIClipboardService : IClipboardService
{
    public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        return Task.CompletedTask;
    }
}

public sealed class WindowsFileRevealService : IFileRevealService
{
    public Task RevealAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { "/select,", path }, UseShellExecute = true });
        else if (Directory.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { path }, UseShellExecute = true });
        return Task.CompletedTask;
    }
}

public sealed class EditorHostProxy : IEditorHost
{
    private IEditorHost? _inner;

    public EditorBridgeState State => _inner?.State ?? EditorBridgeState.Created;
    public event EventHandler<EditorTextChangedPayload>? TextChanged;
    public event EventHandler<EditorViewStatePayload>? ViewStateChanged;
    public event EventHandler<EditorErrorPayload>? Error;
    public event EventHandler<Guid>? SaveRequested;

    public void Attach(IEditorHost inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        if (_inner is not null)
            throw new InvalidOperationException("An editor host has already been attached.");

        _inner = inner;
        inner.TextChanged += (_, payload) => TextChanged?.Invoke(this, payload);
        inner.ViewStateChanged += (_, payload) => ViewStateChanged?.Invoke(this, payload);
        inner.Error += (_, payload) => Error?.Invoke(this, payload);
        inner.SaveRequested += (_, documentId) => SaveRequested?.Invoke(this, documentId);
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => RequireInner().InitializeAsync(cancellationToken);
    public Task OpenDocumentAsync(DocumentModel document, CancellationToken cancellationToken = default) => RequireInner().OpenDocumentAsync(document, cancellationToken);
    public Task ActivateDocumentAsync(Guid documentId, CancellationToken cancellationToken = default) => RequireInner().ActivateDocumentAsync(documentId, cancellationToken);
    public Task CloseDocumentAsync(Guid documentId, CancellationToken cancellationToken = default) => RequireInner().CloseDocumentAsync(documentId, cancellationToken);
    public Task<string> GetDocumentTextAsync(Guid documentId, CancellationToken cancellationToken = default) => RequireInner().GetDocumentTextAsync(documentId, cancellationToken);
    public Task ReplaceTextAsync(DocumentModel document, CancellationToken cancellationToken = default) => RequireInner().ReplaceTextAsync(document, cancellationToken);
    public Task SetDiagnosticsAsync(Guid documentId, IReadOnlyList<EditorDiagnostic> diagnostics, CancellationToken cancellationToken = default) => RequireInner().SetDiagnosticsAsync(documentId, diagnostics, cancellationToken);
    public Task RevealRangeAsync(Guid documentId, TextRange range, bool select, CancellationToken cancellationToken = default) => RequireInner().RevealRangeAsync(documentId, range, select, cancellationToken);
    public Task ExecuteCommandAsync(string command, CancellationToken cancellationToken = default) => RequireInner().ExecuteCommandAsync(command, cancellationToken);
    public Task SetThemeAsync(StudioTheme theme, CancellationToken cancellationToken = default) => RequireInner().SetThemeAsync(theme, cancellationToken);
    public Task FocusEditorAsync(CancellationToken cancellationToken = default) => RequireInner().FocusEditorAsync(cancellationToken);

    private IEditorHost RequireInner() =>
        _inner ?? throw new InvalidOperationException("No editor host has been attached.");
}


public sealed class WinUIExternalChangePolicy(IMessageDialogService dialogs, IFileDialogService files) : IExternalChangePolicy
{
    public async Task<CleanExternalChangeChoice> ConfirmCleanChangeAsync(ExternalChange change, CancellationToken cancellationToken = default)
    {
        var decision = await dialogs.ConfirmExternalChangeAsync(change.Document, change.Kind, cancellationToken);
        return decision == ExternalChangeDecision.Reload ? CleanExternalChangeChoice.Reload : CleanExternalChangeChoice.KeepCurrentView;
    }

    public async Task<DirtyExternalChangeChoice> ConfirmDirtyChangeAsync(ExternalChange change, CancellationToken cancellationToken = default)
    {
        var decision = await dialogs.ConfirmExternalChangeAsync(change.Document, change.Kind, cancellationToken);
        return decision switch
        {
            ExternalChangeDecision.KeepEditorVersion => DirtyExternalChangeChoice.KeepEditorVersion,
            ExternalChangeDecision.ReloadDiskVersion => DirtyExternalChangeChoice.ReloadDiskVersion,
            ExternalChangeDecision.SaveAs => DirtyExternalChangeChoice.SaveAs,
            _ => DirtyExternalChangeChoice.Cancel
        };
    }

    public Task<string?> GetSaveAsPathAsync(DocumentModel document, CancellationToken cancellationToken = default) =>
        files.PickSaveFileAsync(document.DisplayName, Path.GetDirectoryName(document.FilePath), cancellationToken);
}

public sealed class WinUIDirtyProjectTransitionPolicy(IMessageDialogService dialogs) : IDirtyProjectTransitionPolicy
{
    public async Task<DirtyProjectTransitionChoice> ConfirmAsync(IReadOnlyList<DocumentModel> dirtyDocuments, CancellationToken cancellationToken = default)
    {
        var decision = await dialogs.ConfirmUnsavedChangesAsync(dirtyDocuments, UnsavedChangesContext.CloseProject, cancellationToken);
        return decision switch
        {
            UnsavedChangesDecision.Save => DirtyProjectTransitionChoice.SaveAndContinue,
            UnsavedChangesDecision.Discard => DirtyProjectTransitionChoice.DiscardAndContinue,
            _ => DirtyProjectTransitionChoice.Cancel
        };
    }
}
