using System.Collections.Immutable;
using Martin.Compiler;
using Martin.Compiler.Syntax;
using Xunit;

namespace Martin.LanguageServices.Tests;

public sealed class Phase12ReferenceIndexTests
{
    [Fact]
    public void BoundReferencesExcludeTextualNoiseAndKeepHomonymsSeparate()
    {
        const string source = """
            func first() -> Int { let value: Int = 1; return value }
            func second() -> Int { let value: Int = 2; return value }
            // value
            let message: String = "value"
            """;
        var (project, analysis) = Analyze(("main.martin", source));
        var index = new ReferenceIndexBuilder().Build(project, analysis);
        var declarations = new DeclarationIndexBuilder().Build(project, analysis);
        var values = declarations.ByName["value"];

        Assert.Equal(2, values.Length);
        Assert.All(values, id => Assert.Equal(2, index.Find(id).Length));
        Assert.DoesNotContain(index.BySymbol.Values.SelectMany(x => x),
            location => source[location.Span.Start..location.Span.End] == "value" && location.Span.Start > source.IndexOf("//", StringComparison.Ordinal));
    }

    [Fact]
    public void CrossFileReferencesAreVersionedOrderedAndClassified()
    {
        var (project, analysis) = Analyze(
            ("a.martin", "func answer() -> Int { return 42 }"),
            ("b.martin", "func main() -> Int { return answer() }"));
        var index = new ReferenceIndexBuilder().Build(project, analysis);
        var declaration = new DeclarationIndexBuilder().Build(project, analysis);
        var answer = declaration.ByName["answer"].Single();
        var references = index.Find(answer);

        Assert.True(index.IsComplete);
        Assert.Equal(project.Version, index.ProjectVersion);
        Assert.Equal([ReferenceKind.Declaration, ReferenceKind.Call], references.Select(r => r.Kind));
        Assert.Equal(2, references.Select(r => r.DocumentId).Distinct().Count());
        Assert.Empty(index.Find(answer, includeDeclaration: false).Where(r => r.IsDeclaration));
    }

    [Fact]
    public void PartialBuildAndCancellationAreExplicit()
    {
        var (project, analysis) = Analyze(("a.martin", "func a() {}"), ("b.martin", "func b() { a() }"));
        var partial = new ReferenceIndexBuilder().Build(project, analysis, [project.Documents[0].Id]);
        Assert.False(partial.IsComplete);
        Assert.Single(partial.ByDocument);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => new ReferenceIndexBuilder().Build(project, analysis, cancelled.Token));
    }

    static (LanguageProjectSnapshot Project, ProjectAnalysis Analysis) Analyze(params (string Path, string Text)[] sources)
    {
        var documents = sources.Select(source => new LanguageDocumentSnapshot(DocumentId.CreateNew(),
            Path.GetFullPath(source.Path), source.Text, new(0))).ToImmutableArray();
        var project = new LanguageProjectSnapshot(ProjectId.CreateNew(), "References", Environment.CurrentDirectory, new(7), documents);
        var trees = documents.Select(document => SyntaxTree.Parse(document.Text, document.FilePath)).ToImmutableArray();
        var compilation = Compilation.Create(trees);
        compilation.BindProgram();
        var models = documents.Zip(trees).ToImmutableDictionary(pair => pair.First.Id,
            pair => compilation.GetSemanticModel(pair.Second));
        return (project, new(project.Id, project.Version, compilation, models, []));
    }
}
