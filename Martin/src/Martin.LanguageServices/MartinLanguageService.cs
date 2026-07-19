using System.Collections.Immutable;
using System.Text;
using Martin.Compiler;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Syntax;
using Martin.Compiler.Text;
using Martin.ProjectSystem;

namespace Martin.LanguageServices;

public sealed class MartinLanguageService(LanguageAnalysisCache? cache = null) : ILanguageService, ICompletionService, IHoverService, IDocumentationService, INavigationService, IReferenceService, ISignatureHelpService, IClassificationService, IFormattingService
{
    readonly LanguageAnalysisCache _cache = cache ?? new();
    static readonly string[] Types = ["Int", "Double", "String", "Bool", "Void"];
    static readonly string[] Keywords = ["func", "let", "var", "if", "else", "while", "return", "true", "false", "nil", "struct", "class", "init", "self", "mutating", "protocol", "enum", "case", "switch", "default"];
    public AnalysisCacheMetrics CacheMetrics => _cache.Metrics;

    public void CloseProject(ProjectId projectId)
    {
        _cache.ClearProject(projectId);
    }

    async Task<IAnalysisLease<ProjectAnalysis>> GetProjectAnalysisAsync(LanguageProjectSnapshot project, CancellationToken ct)
    {
        _cache.RetainCurrent(project);
        return await _cache.GetOrCreateProjectAsync(new(project.Id, project.Version), factoryCt =>
                                                                                      {
                                                                                          factoryCt.ThrowIfCancellationRequested();
                                                                                          var trees = project.Documents.Select(d =>
                                                                                                                               { factoryCt.ThrowIfCancellationRequested(); return SyntaxTree.Parse(d.Text, d.FilePath, factoryCt); })
                                                                                                          .ToImmutableArray();
                                                                                          var compilation = Compilation.Create(trees);
                                                                                          var diagnostics = compilation.BindProgram(factoryCt).Diagnostics.Select(ToLanguageDiagnostic).ToImmutableArray();
                                                                                          var models = project.Documents.Zip(trees, (d, t) => (d.Id, Model: compilation.GetSemanticModel(t))).ToImmutableDictionary(x => x.Id, x => x.Model);
                                                                                          var classifications = project.Documents.Zip(trees, (d, t) => (d.Id, Value: SemanticClassificationProvider.Classify(t, models[d.Id]))).ToImmutableDictionary(x => x.Id, x => x.Value);
                                                                                          var patternData = ImmutableArray.CreateBuilder<IndexedPatternSemanticInfo>();
                                                                                          var switchData = ImmutableArray.CreateBuilder<IndexedSwitchAnalysis>();
                                                                                          foreach (var (documentId, model) in models)
                                                                                          {
                                                                                              foreach (var node in Flatten(model.SyntaxTree.Root))
                                                                                              {
                                                                                                  if (node is PatternSyntax pattern && model.GetPatternInfo(pattern) is {} info)
                                                                                                      patternData.Add(new(documentId, pattern.Span, info));
                                                                                                  if (node is StatementSyntax { Kind : SyntaxKind.SwitchStatement } statement && model.GetSwitchAnalysis(statement) is {} analysis)
                                                                                                      switchData.Add(new(documentId, statement.Span, analysis));
                                                                                              }
                                                                                          }
                                                                                          var phase13 = new Phase13SemanticData(patternData.ToImmutable(), switchData.ToImmutable(), compilation.GetConformances(), compilation.GetConformanceAttempts());
                                                                                          var preliminary = new ProjectAnalysis(project.Id, project.Version, compilation, models, diagnostics, classifications) { Phase13 = phase13 };
                                                                                          var declarations = new DeclarationIndexBuilder().Build(project, preliminary);
                                                                                          var references = new ReferenceIndexBuilder().Build(project, preliminary, factoryCt);
                                                                                          return Task.FromResult(preliminary with { Declarations = declarations, References = references });
                                                                                      },
                                                    project.Documents.Sum(d => d.Text.Length) * 4L, ct)
            .ConfigureAwait(false);
    }

    static IEnumerable<SyntaxNode> Flatten(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.GetChildren())
            foreach (var descendant in Flatten(child))
                yield return descendant;
    }

    T WithProjectAnalysis<T>(LanguageProjectSnapshot project, Func<ProjectAnalysis, T> action)
    {
        using var lease = GetProjectAnalysisAsync(project, CancellationToken.None).GetAwaiter().GetResult();
        return action(lease.Value);
    }

    async Task<T> WithProjectAnalysisAsync<T>(LanguageProjectSnapshot project, Func<ProjectAnalysis, T> action, CancellationToken ct)
    {
        using var lease = await GetProjectAnalysisAsync(project, ct).ConfigureAwait(false);
        return action(lease.Value);
    }
    public Task<ImmutableArray<LanguageDiagnostic>> GetSyntaxDiagnosticsAsync(LanguageDocumentSnapshot document, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var tree = SyntaxTree.Parse(document.Text, document.FilePath);
        return Task.FromResult(tree.Diagnostics.Select(d => ToLanguageDiagnostic(d) with { DiagnosticSource = LanguageDiagnosticSource.LiveSyntax, Source = "Martin.Live.Syntax" }).ToImmutableArray());
    }
    public async Task<ImmutableArray<LanguageDiagnostic>> GetProjectSemanticDiagnosticsAsync(LanguageProjectSnapshot project, CancellationToken ct = default)
    {
        using var lease = await GetProjectAnalysisAsync(project, ct).ConfigureAwait(false);
        var syntax = lease.Value.Compilation.SyntaxTrees.SelectMany(t => t.Diagnostics).Select(d => (d.Code, d.Location.FilePath, d.Location.Span, d.Message)).ToHashSet();
        return lease.Value.Diagnostics
            .Where(d => !syntax.Contains((d.Code, d.FilePath, d.Span, d.Message)))
            .Select(d => d with { DiagnosticSource = LanguageDiagnosticSource.LiveSemantic, Source = "Martin.Live.Semantic" })
            .ToImmutableArray();
    }
    public LanguageWorkspaceSnapshot CreateWorkspaceFromProject(MartinProject project)
    {
        var docs = project.SourceFiles.Select(f => new LanguageDocumentSnapshot(DocumentId.CreateNew(), f, File.Exists(f) ? File.ReadAllText(f) : string.Empty, new(0))).ToImmutableArray();
        var p = new LanguageProjectSnapshot(ProjectId.CreateNew(), project.Manifest.Package.Name, project.RootDirectory, new(0), docs);
        return new(WorkspaceId.CreateNew(), new(0), [p]);
    }
    public async Task<DocumentAnalysis> AnalyzeDocumentAsync(LanguageWorkspaceSnapshot workspace, DocumentId documentId, CancellationToken ct = default)
    {
        var doc = workspace.FindDocument(documentId) ?? throw new InvalidOperationException("Document not found.");
        var project = workspace.Projects.First(p => p.Documents.Any(d => d.Id == doc.Id));
        _cache.RetainCurrent(workspace);
        var leases = new List<IAnalysisLease<DocumentAnalysis>>(project.Documents.Length);
        try
        {
            foreach (var source in project.Documents)
            {
                var key = new DocumentAnalysisKey(source.Id, source.Version);
                leases.Add(await _cache.GetOrCreateDocumentAsync(key,
                                                                 _ =>
                                                                 {
                                                                     var tree = SyntaxTree.Parse(source.Text, source.FilePath);
                                                                     var syntaxDiagnostics = tree.Diagnostics.Select(ToLanguageDiagnostic).ToImmutableArray();
                                                                     return Task.FromResult(new DocumentAnalysis(source.Id, source.Version, tree, Compilation.Create(tree), syntaxDiagnostics, Classify(tree)));
                                                                 },
                                                                 source.Text.Length * 2L, ct));
            }
            var trees = leases.Select(l => l.Value.SyntaxTree).ToImmutableArray();
            using var projectLease = await GetProjectAnalysisAsync(project, ct);
            var document = leases.Single(l => l.Value.DocumentId == documentId).Value;
            var diagnostics = document.Diagnostics.Concat(projectLease.Value.Diagnostics.Where(d => StringComparer.OrdinalIgnoreCase.Equals(d.FilePath, doc.FilePath))).Distinct().ToImmutableArray();
            return document with {
                Compilation = projectLease.Value.Compilation,
                Diagnostics = diagnostics,
                Classifications = projectLease.Value.Classifications.GetValueOrDefault(documentId, document.Classifications)
            };
        }
        finally
        {
            foreach (var lease in leases)
                lease.Dispose();
        }
    }
    public async Task<VersionedResponse<ImmutableArray<LanguageDiagnostic>>> GetDiagnosticsAsync(LanguageWorkspaceSnapshot w, VersionedRequest<object?> request, CancellationToken ct = default)
    {
        var current = w.FindDocument(request.DocumentId);
        if (current is null || current.Version != request.Version)
            return new(request.DocumentId, request.Version, true, []);
        var a = await AnalyzeDocumentAsync(w, request.DocumentId, ct);
        return new(request.DocumentId, request.Version, false, a.Diagnostics);
    }
    public ImmutableArray<CompletionItem> Complete(LanguageWorkspaceSnapshot w, DocumentId id, int position)
    {
        var d = w.FindDocument(id);
        if (d is null)
            return [];
        var p = w.Projects.First(x => x.Documents.Any(y => y.Id == id));
        return WithProjectAnalysis(p, a => SemanticCompletionProvider.Complete(w, p, a, d, position, 200, CancellationToken.None));
    }
    public async Task<LanguageResult<ImmutableArray<CompletionItem>>> CompleteAsync(LanguageWorkspaceSnapshot w, CompletionRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var p = w.FindProject(request.ProjectId);
        var d = w.FindDocument(request.DocumentId);
        var stale = w.Id != request.WorkspaceId || w.Version != request.WorkspaceVersion || p is null || p.Version != request.ProjectVersion || d is null || d.Version != request.DocumentVersion || !p.Documents.Any(x => x.Id == request.DocumentId);
        var value = stale ? ImmutableArray<CompletionItem>.Empty : await WithProjectAnalysisAsync(p!, a => SemanticCompletionProvider.Complete(w, p!, a, d!, request.Position, request.MaximumResults, ct), ct);
        return new LanguageResult<ImmutableArray<CompletionItem>> { WorkspaceId = request.WorkspaceId, WorkspaceVersion = request.WorkspaceVersion, ProjectId = request.ProjectId, ProjectVersion = request.ProjectVersion, DocumentId = request.DocumentId, DocumentVersion = request.DocumentVersion, Value = value, IsStale = stale };
    }
    public HoverInfo? Hover(LanguageWorkspaceSnapshot w, DocumentId id, int position)
    {
        var doc = w.FindDocument(id);
        if (doc is null)
            return null;
        var project = w.Projects.FirstOrDefault(p => p.Documents.Any(d => d.Id == id));
        return project is null ? null : WithProjectAnalysis(project, a => SemanticHoverProvider.Get(project, a, doc, position, CancellationToken.None));
    }
    public async Task<LanguageResult<HoverInfo?>> HoverAsync(LanguageWorkspaceSnapshot w, HoverRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var project = w.FindProject(request.ProjectId);
        var document = w.FindDocument(request.DocumentId);
        var stale = w.Id != request.WorkspaceId || w.Version != request.WorkspaceVersion || project is null || project.Version != request.ProjectVersion || document is null || document.Version != request.DocumentVersion || !project.Documents.Any(d => d.Id == request.DocumentId);
        var value = stale ? null : await WithProjectAnalysisAsync(project!, a => SemanticHoverProvider.Get(project!, a, document!, request.Position, ct), ct);
        return new LanguageResult < HoverInfo ?> { WorkspaceId = request.WorkspaceId, WorkspaceVersion = request.WorkspaceVersion, ProjectId = request.ProjectId, ProjectVersion = request.ProjectVersion, DocumentId = request.DocumentId, DocumentVersion = request.DocumentVersion, Value = value, IsStale = stale };
    }
    public string? GetDocumentation(LanguageWorkspaceSnapshot w, DocumentId id, int position) => Hover(w, id, position)?.Markdown;
    public DefinitionLocation? GoToDefinition(LanguageWorkspaceSnapshot w, DocumentId id, int position)
    {
        var project = w.Projects.FirstOrDefault(p => p.Documents.Any(d => d.Id == id));
        if (project is null)
            return null;
        var context = WithProjectAnalysis(project, a => SemanticNavigationProvider.Build(a, CancellationToken.None));
        return SemanticNavigationProvider.Definitions(context, SemanticNavigationProvider.Resolve(context, id, position)).FirstOrDefault();
    }
    public ImmutableArray<ReferenceLocation> FindReferences(LanguageWorkspaceSnapshot w, string symbol)
    {
        foreach (var project in w.Projects)
        {
            var context = WithProjectAnalysis(project, a => SemanticNavigationProvider.Build(a, CancellationToken.None));
            var ids = context.Declarations.ByName.TryGetValue(symbol, out var found) ? found : [];
            if (ids.Length == 1)
                return SemanticNavigationProvider.References(context, ids[0], default, true);
        }
        return [];
    }
    public async Task<LanguageResult<ImmutableArray<DefinitionLocation>>> GetDefinitionsAsync(LanguageWorkspaceSnapshot w, DefinitionRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var project = w.FindProject(request.ProjectId);
        var document = w.FindDocument(request.DocumentId);
        var stale = IsStale(w, project, document, request);
        var value = ImmutableArray<DefinitionLocation>.Empty;
        if (!stale)
        {
            var context = await WithProjectAnalysisAsync(project!, a => SemanticNavigationProvider.Build(a, ct), ct);
            value = SemanticNavigationProvider.Definitions(context, SemanticNavigationProvider.Resolve(context, request.DocumentId, request.Position));
        }
        return Result(request, value, stale);
    }
    public async Task<LanguageResult<ImmutableArray<ReferenceLocation>>> FindReferencesAsync(LanguageWorkspaceSnapshot w, ReferencesRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var project = w.FindProject(request.ProjectId);
        var document = w.FindDocument(request.DocumentId);
        var stale = IsStale(w, project, document, request);
        var value = ImmutableArray<ReferenceLocation>.Empty;
        if (!stale)
        {
            var context = await WithProjectAnalysisAsync(project!, a => SemanticNavigationProvider.Build(a, ct), ct);
            value = SemanticNavigationProvider.References(context, SemanticNavigationProvider.Resolve(context, request.DocumentId, request.Position), request.DocumentId, request.IncludeDeclaration);
        }
        return Result(request, value, stale);
    }
    public SignatureHelp? GetSignatureHelp(LanguageWorkspaceSnapshot w, DocumentId id, int position)
    {
        var doc = w.FindDocument(id);
        var project = w.Projects.FirstOrDefault(p => p.Documents.Any(d => d.Id == id));
        return doc is null || project is null ? null : WithProjectAnalysis(project, a => SemanticSignatureHelpProvider.Get(project, a, doc, position, CancellationToken.None));
    }
    public async Task<LanguageResult<SignatureHelp?>> GetSignatureHelpAsync(LanguageWorkspaceSnapshot w, SignatureHelpRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var p = w.FindProject(request.ProjectId);
        var d = w.FindDocument(request.DocumentId);
        var stale = IsStale(w, p, d, request);
        var value = stale ? null : await WithProjectAnalysisAsync(p!, a => SemanticSignatureHelpProvider.Get(p!, a, d!, request.Position, ct), ct);
        return Result(request, value, stale);
    }
    public async Task<LanguageResult<ImmutableArray<ClassifiedSpan>>> GetClassificationsAsync(LanguageWorkspaceSnapshot w, ClassificationRequest request, CancellationToken ct = default)
    {
        var p = w.FindProject(request.ProjectId);
        var d = w.FindDocument(request.DocumentId);
        var stale = IsStale(w, p, d, request);
        if (stale)
            return Result(request, ImmutableArray<ClassifiedSpan>.Empty, true);
        var analysis = await AnalyzeDocumentAsync(w, request.DocumentId, ct);
        return Result(request, analysis.Classifications, false);
    }
    public FormattingResult GetFormattingEdits(string text, FormattingOptions? options = null) => SafeFormattingService.Format(text, options);
    public string FormatDocument(string text)
    {
        var result = GetFormattingEdits(text);
        return result.WasRefused ? text : TextEditApplicator.Apply(text, result.Edits);
    }
    public Task<LanguageResult<FormattingResult>> FormatDocumentAsync(LanguageWorkspaceSnapshot w, FormattingRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var p = w.FindProject(request.ProjectId);
        var d = w.FindDocument(request.DocumentId);
        var stale = IsStale(w, p, d, request);
        var value = stale ? new FormattingResult() : SafeFormattingService.Format(d!.Text, request.Options);
        return Task.FromResult(Result(request, value, stale));
    }
    static LanguageDiagnostic ToLanguageDiagnostic(Diagnostic d) => new(d.Code, d.Severity, d.Message, d.Location.FilePath ?? string.Empty, d.Location.Span, d.Location.LineSpan, "Martin");
    static ImmutableArray<ClassifiedSpan> Classify(SyntaxTree tree) => Flatten(tree.Root).OfType<SyntaxToken>().SelectMany(TokenClassifications).ToImmutableArray();
    static IEnumerable<ClassifiedSpan> TokenClassifications(SyntaxToken t)
    {
        foreach (var tr in t.LeadingTrivia.Concat(t.TrailingTrivia).Where(x => x.Kind is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia or SyntaxKind.DocumentationCommentTrivia))
            yield return new(tr.Span, tr.Kind == SyntaxKind.DocumentationCommentTrivia ? ClassificationKind.DocumentationComment : ClassificationKind.Comment);
        var kind = t.Kind switch { var k when SyntaxFacts.IsKeyword(k) => ClassificationKind.Keyword, SyntaxKind.IdentifierToken => ClassificationKind.UnresolvedIdentifier, SyntaxKind.IntegerLiteralToken or SyntaxKind.FloatingPointLiteralToken => ClassificationKind.NumberLiteral, SyntaxKind.StringLiteralToken => ClassificationKind.StringLiteral, var k when SyntaxFacts.GetBinaryOperatorPrecedence(k) > 0 || SyntaxFacts.GetUnaryOperatorPrecedence(k) > 0 || k is SyntaxKind.EqualToken or SyntaxKind.ArrowToken => ClassificationKind.Operator,
                                   _ => ClassificationKind.Punctuation };
        yield return new(t.Span, kind);
    }
    static void WriteComments(StringBuilder sb, IEnumerable<SyntaxTrivia> trivia, int indent)
    {
        foreach (var tr in trivia.Where(IsCommentTrivia))
        {
            TrimLine(sb, indent);
            sb.Append(tr.Text);
            sb.AppendLine();
        }
    }
    static void WriteTrailingComments(StringBuilder sb, IEnumerable<SyntaxTrivia> trivia)
    {
        foreach (var tr in trivia.Where(IsCommentTrivia))
        {
            if (sb.Length > 0 && !char.IsWhiteSpace(sb[^1]))
                sb.Append(' ');
            sb.Append(tr.Text);
        }
    }
    static bool IsCommentTrivia(SyntaxTrivia trivia) => trivia.Kind is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia or SyntaxKind.DocumentationCommentTrivia;
    static ImmutableArray<string> CollectSymbols(LanguageWorkspaceSnapshot w)
    {
        var result = ImmutableArray.CreateBuilder<string>();
        foreach (var d in w.Projects.SelectMany(p => p.Documents))
        {
            var tree = SyntaxTree.Parse(d.Text, d.FilePath);
            var compilation = Compilation.Create(tree);
            _ = compilation.BindProgram();
            result.AddRange(compilation.GetSemanticModel(tree).GetDeclaredSymbols().Select(s => s.Name));
        }
        return result.Distinct().ToImmutableArray();
    }
    static (int start, string text) WordAt(string text, int pos)
    {
        if (text.Length == 0)
            return (0, "");
        pos = Math.Clamp(pos, 0, text.Length - 1);
        while (pos > 0 && !IsWord(text[pos]) && IsWord(text[pos - 1]))
            pos--;
        if (!IsWord(text[pos]))
            return (pos, "");
        var s = pos;
        while (s > 0 && IsWord(text[s - 1]))
            s--;
        var e = pos;
        while (e < text.Length && IsWord(text[e]))
            e++;
        return (s, text[s..e]);
        static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
    static bool IsStale(LanguageWorkspaceSnapshot w, LanguageProjectSnapshot? p, LanguageDocumentSnapshot? d, LanguageRequest r) => w.Id != r.WorkspaceId || w.Version != r.WorkspaceVersion || p is null || p.Version != r.ProjectVersion || d is null || d.Version != r.DocumentVersion || !p.Documents.Any(x => x.Id == r.DocumentId);
    static LanguageResult<T> Result<T>(LanguageRequest r, T value, bool stale) => new() { WorkspaceId = r.WorkspaceId, WorkspaceVersion = r.WorkspaceVersion, ProjectId = r.ProjectId, ProjectVersion = r.ProjectVersion, DocumentId = r.DocumentId, DocumentVersion = r.DocumentVersion, Value = value, IsStale = stale };
    static bool NeedSpaceBefore(SyntaxKind kind, StringBuilder sb) => sb.Length > 0 && !char.IsWhiteSpace(sb[^1]) && kind is SyntaxKind.IdentifierToken or SyntaxKind.TrueKeyword or SyntaxKind.FalseKeyword or SyntaxKind.NilKeyword or SyntaxKind.LetKeyword or SyntaxKind.VarKeyword or SyntaxKind.FuncKeyword or SyntaxKind.IfKeyword or SyntaxKind.WhileKeyword or SyntaxKind.ReturnKeyword or SyntaxKind.StructKeyword or SyntaxKind.ClassKeyword or SyntaxKind.ProtocolKeyword or SyntaxKind.EnumKeyword or SyntaxKind.CaseKeyword or SyntaxKind.SwitchKeyword or SyntaxKind.DefaultKeyword or SyntaxKind.InitKeyword or SyntaxKind.MutatingKeyword or SyntaxKind.SelfKeyword or SyntaxKind.IntegerLiteralToken or SyntaxKind.FloatingPointLiteralToken or SyntaxKind.StringLiteralToken;
    static void TrimLine(StringBuilder sb, int indent)
    {
        if (sb.Length > 0 && !sb.ToString().EndsWith(Environment.NewLine))
            sb.AppendLine();
        sb.Append(new string(' ', indent * 4));
    }
    static void EnsureIndent(StringBuilder sb, int indent)
    {
        if (sb.Length == 0 || sb.ToString().EndsWith(Environment.NewLine))
            sb.Append(new string(' ', indent * 4));
    }
}
