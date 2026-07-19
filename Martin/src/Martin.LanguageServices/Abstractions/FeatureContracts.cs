using System.Collections.Immutable;

namespace Martin.LanguageServices;

/// <summary>Compatibility-facing contract for version-aware language analysis.</summary>
public interface ILanguageService : IDiagnosticsService
{
    Task<DocumentAnalysis> AnalyzeDocumentAsync(LanguageWorkspaceSnapshot workspace, DocumentId documentId, CancellationToken cancellationToken = default);
}

public interface ICompletionService
{
    ImmutableArray<CompletionItem> Complete(LanguageWorkspaceSnapshot workspace, DocumentId documentId, int position);
    Task<LanguageResult<ImmutableArray<CompletionItem>>> CompleteAsync(LanguageWorkspaceSnapshot workspace, CompletionRequest request, CancellationToken cancellationToken = default);
}

public interface IHoverService
{
    HoverInfo? Hover(LanguageWorkspaceSnapshot workspace, DocumentId documentId, int position);
    Task<LanguageResult<HoverInfo?>> HoverAsync(LanguageWorkspaceSnapshot workspace, HoverRequest request, CancellationToken cancellationToken = default);
}

public interface INavigationService
{
    DefinitionLocation? GoToDefinition(LanguageWorkspaceSnapshot workspace, DocumentId documentId, int position);
    Task<LanguageResult<ImmutableArray<DefinitionLocation>>> GetDefinitionsAsync(LanguageWorkspaceSnapshot workspace, DefinitionRequest request, CancellationToken cancellationToken = default);
}

public interface IReferenceService
{
    ImmutableArray<ReferenceLocation> FindReferences(LanguageWorkspaceSnapshot workspace, string symbol);
    Task<LanguageResult<ImmutableArray<ReferenceLocation>>> FindReferencesAsync(LanguageWorkspaceSnapshot workspace, ReferencesRequest request, CancellationToken cancellationToken = default);
}

public interface ISignatureHelpService
{
    SignatureHelp? GetSignatureHelp(LanguageWorkspaceSnapshot workspace, DocumentId documentId, int position);
    Task<LanguageResult<SignatureHelp?>> GetSignatureHelpAsync(LanguageWorkspaceSnapshot workspace, SignatureHelpRequest request, CancellationToken cancellationToken = default);
}

public interface IClassificationService
{
    Task<DocumentAnalysis> AnalyzeDocumentAsync(LanguageWorkspaceSnapshot workspace, DocumentId documentId, CancellationToken cancellationToken = default);
    Task<LanguageResult<ImmutableArray<ClassifiedSpan>>> GetClassificationsAsync(LanguageWorkspaceSnapshot workspace, ClassificationRequest request, CancellationToken cancellationToken = default);
}

public interface IFormattingService
{
    string FormatDocument(string text);
    FormattingResult GetFormattingEdits(string text, FormattingOptions? options = null);
    Task<LanguageResult<FormattingResult>> FormatDocumentAsync(LanguageWorkspaceSnapshot workspace, FormattingRequest request, CancellationToken cancellationToken = default);
}
