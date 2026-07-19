using System.Collections.Immutable;

namespace Martin.LanguageServices;

public interface IDiagnosticsService
{
    Task<VersionedResponse<ImmutableArray<LanguageDiagnostic>>> GetDiagnosticsAsync(
        LanguageWorkspaceSnapshot workspace,
        VersionedRequest<object?> request,
        CancellationToken cancellationToken = default);
}
