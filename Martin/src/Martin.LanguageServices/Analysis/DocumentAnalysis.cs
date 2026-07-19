using System.Collections.Immutable;
using Martin.Compiler;
using Martin.Compiler.Syntax;
using Martin.Compiler.Binding;
using Martin.Compiler.Symbols;
using Martin.Compiler.Text;

namespace Martin.LanguageServices;

public sealed record DocumentAnalysis(DocumentId DocumentId, DocumentVersion Version, SyntaxTree SyntaxTree, Compilation Compilation, ImmutableArray<LanguageDiagnostic> Diagnostics, ImmutableArray<ClassifiedSpan> Classifications);

public sealed record IndexedPatternSemanticInfo(DocumentId DocumentId, TextSpan Span, PatternSemanticInfo Info);
public sealed record IndexedSwitchAnalysis(DocumentId DocumentId, TextSpan Span, SwitchAnalysisResult Analysis);

/// <summary>Phase 13 data owned by, and versioned with, a project analysis.</summary>
public sealed record Phase13SemanticData(
    ImmutableArray<IndexedPatternSemanticInfo> Patterns,
    ImmutableArray<IndexedSwitchAnalysis> Switches,
    ImmutableArray<ProtocolConformance> Conformances,
    ImmutableArray<ProtocolConformanceAttempt> ConformanceAttempts)
{
    public static Phase13SemanticData Empty { get; } = new([], [], [], []);
}

public sealed record ProjectAnalysis(
    ProjectId ProjectId,
    ProjectVersion Version,
    Compilation Compilation,
    ImmutableDictionary<DocumentId, SemanticModel> SemanticModels,
    ImmutableArray<LanguageDiagnostic> Diagnostics,
    ImmutableDictionary<DocumentId, ImmutableArray<ClassifiedSpan>> Classifications)
{
    /// <summary>The project-wide declaration index produced by this binding.</summary>
    public DeclarationIndex Declarations { get; init; } = null!;

    /// <summary>The project-wide bound-reference index produced by this binding.</summary>
    public ReferenceIndex References { get; init; } = null!;

    /// <summary>Pattern, switch, and protocol relationships for this exact project version.</summary>
    public Phase13SemanticData Phase13 { get; init; } = Phase13SemanticData.Empty;

    public ProjectAnalysis(
        ProjectId projectId,
        ProjectVersion version,
        Compilation compilation,
        ImmutableDictionary<DocumentId, SemanticModel> semanticModels,
        ImmutableArray<LanguageDiagnostic> diagnostics)
        : this(projectId, version, compilation, semanticModels, diagnostics,
            ImmutableDictionary<DocumentId, ImmutableArray<ClassifiedSpan>>.Empty)
    {
    }
}
