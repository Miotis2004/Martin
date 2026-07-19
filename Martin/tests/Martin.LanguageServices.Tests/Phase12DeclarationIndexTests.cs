using System.Collections.Immutable;
using Martin.Compiler;
using Martin.Compiler.Syntax;
using Martin.LanguageServices;
using Xunit;

namespace Martin.LanguageServices.Tests;

public sealed class Phase12DeclarationIndexTests
{
    [Fact]
    public void StableIdsSurviveEquivalentCompilationAndOverloadsRemainDistinct()
    {
        const string source = """
            /// Adds an integer.
            func convert(value: Int) -> Int { return value }
            func convert(value: String) -> String { return value }
            """;
        var (project, first) = Analyze(source);
        var firstIndex = new DeclarationIndexBuilder().Build(project, first);
        var (_, second) = Analyze(source, project);
        var secondIndex = new DeclarationIndexBuilder().Build(project, second);

        var overloads = firstIndex.ByName["convert"];
        Assert.Equal(2, overloads.Length);
        Assert.Equal(2, overloads.Distinct().Count());
        Assert.Equal(overloads, secondIndex.ByName["convert"]);
        Assert.Contains(firstIndex.ById.Values, descriptor => descriptor.DocumentationMarkdown == "Adds an integer.");
    }

    [Fact]
    public void IndexesContainersParametersLocalsAndExactCompilerLocations()
    {
        const string source = """
            struct Box {
                let value: Int
                func get(fallback: Int) -> Int {
                    let selected: Int = fallback
                    return selected
                }
            }
            """;
        var (project, analysis) = Analyze(source);
        var index = new DeclarationIndexBuilder().Build(project, analysis);

        Assert.Equal(project.Version, index.ProjectVersion);
        Assert.Contains("Box", index.ByName.Keys);
        Assert.Contains("value", index.ByName.Keys);
        Assert.Contains("fallback", index.ByName.Keys);
        Assert.Contains("selected", index.ByName.Keys);
        Assert.All(index.ByDocument[project.Documents[0].Id], id => Assert.Equal(project.Documents[0].Id, index.ById[id].Definition.DocumentId));
        Assert.NotNull(index.ById[index.ByName["get"].Single()].ContainingSymbolId);
        Assert.NotNull(index.ById[index.ByName["fallback"].Single()].ContainingSymbolId);
    }

    [Fact]
    public void BuiltInAndConstructedTypeIdentitiesAreDeterministic()
    {
        var projectId = ProjectId.CreateNew();
        var first = new SymbolIdentityFactory(projectId);
        var second = new SymbolIdentityFactory(ProjectId.CreateNew());

        Assert.Equal(first.Create(Martin.Compiler.Symbols.TypeSymbol.Int), second.Create(Martin.Compiler.Symbols.TypeSymbol.Int));
        Assert.DoesNotContain("System.", first.Create(Martin.Compiler.Symbols.TypeSymbol.Int).Value);
    }

    static (LanguageProjectSnapshot Project, ProjectAnalysis Analysis) Analyze(string source, LanguageProjectSnapshot? existing = null)
    {
        var path = Path.GetFullPath("index-test.martin");
        var document = existing?.Documents[0] ?? new LanguageDocumentSnapshot(DocumentId.CreateNew(), path, source, new(0));
        document = document with { Text = source };
        var project = existing ?? new LanguageProjectSnapshot(ProjectId.CreateNew(), "IndexTests", Path.GetDirectoryName(path)!, new(0), [document]);
        var tree = SyntaxTree.Parse(source, path);
        var compilation = Compilation.Create(tree);
        compilation.BindProgram();
        var model = compilation.GetSemanticModel(tree);
        var analysis = new ProjectAnalysis(project.Id, project.Version, compilation,
            ImmutableDictionary<DocumentId, SemanticModel>.Empty.Add(document.Id, model), []);
        return (project, analysis);
    }
}
