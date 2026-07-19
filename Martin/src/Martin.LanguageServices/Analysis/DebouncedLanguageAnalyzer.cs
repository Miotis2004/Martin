using System.Collections.Immutable;

namespace Martin.LanguageServices;

/// <summary>Compatibility analyzer; a cancellable scheduler will replace its global debounce policy.</summary>
public sealed class DebouncedLanguageAnalyzer(MartinLanguageService service, TimeSpan delay)
{
    private CancellationTokenSource? _latest;

    public async Task<VersionedResponse<ImmutableArray<LanguageDiagnostic>>> AnalyzeLatestAsync(LanguageWorkspaceSnapshot workspace, VersionedRequest<object?> request, CancellationToken cancellationToken = default)
    {
        _latest?.Cancel();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _latest = linked;
        await Task.Delay(delay, linked.Token);
        return await service.GetDiagnosticsAsync(workspace, request, linked.Token);
    }
}
