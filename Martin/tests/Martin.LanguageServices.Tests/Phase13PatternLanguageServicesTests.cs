using Martin.Compiler.Text;
using Xunit;

namespace Martin.LanguageServices.Tests;

public sealed class Phase13PatternLanguageServicesTests
{
    private const string Source = """
        enum Result { case success(Int) case failure(String, Int) case cancelled }
        func inspect(_ result: Result) {
            switch result {
                case .success(let value): print(value)
                case .cancelled: print(0)
                case .failure(let message, let code): print(code)
            }
        }
        """;

    [Fact]
    public void Enum_case_completion_is_input_typed_and_inserts_payload_bindings()
    {
        var incomplete = Source.Replace("case .cancelled", "case .", StringComparison.Ordinal);
        var (service, workspace, _, document) = Create(incomplete);
        var position = incomplete.IndexOf("case .", StringComparison.Ordinal) + "case .".Length;

        var items = service.Complete(workspace, document.Id, position);

        var success = Assert.Single(items, item => item.Label == ".success");
        Assert.Equal(InsertTextFormat.Snippet, success.InsertTextFormat);
        Assert.Equal("success(let ${1:value})", success.TextEdit.NewText);
        Assert.Contains(items, item => item.Label == ".failure");
    }

    [Fact]
    public void Missing_case_completion_preserves_nested_witness_shape()
    {
        const string source = """
            enum Inner { case some(Int) case failure(String, Int) }
            enum Outer { case result(Inner, Inner) }
            func inspect(_ value: Outer) {
                switch value {
                    case .
                }
            }
            """;
        var (service, workspace, _, document) = Create(source);
        var position = source.IndexOf("case .", StringComparison.Ordinal) + "case .".Length;

        var items = service.Complete(workspace, document.Id, position);

        var result = Assert.Single(items, item => item.Label == ".result(.some(_), .failure(_, _))");
        Assert.Equal(InsertTextFormat.Snippet, result.InsertTextFormat);
        Assert.Equal("result(.some(let ${1:value}), .failure(let ${2:value2}, let ${3:value3}))", result.TextEdit.NewText);
    }

    [Fact]
    public async Task Pattern_bindings_and_enum_cases_are_classified_semantically()
    {
        var (service, workspace, project, document) = Create(Source);
        var request = new ClassificationRequest { WorkspaceId = workspace.Id, WorkspaceVersion = workspace.Version,
            ProjectId = project.Id, ProjectVersion = project.Version, DocumentId = document.Id, DocumentVersion = document.Version };

        var spans = (await service.GetClassificationsAsync(workspace, request)).Value;

        var binding = Source.IndexOf("value", StringComparison.Ordinal);
        Assert.Contains(spans, span => span.Span == new TextSpan(binding, 5) && span.Kind == ClassificationKind.PatternVariable);
        var enumCase = Source.LastIndexOf("success(let", StringComparison.Ordinal);
        Assert.Contains(spans, span => span.Span == new TextSpan(enumCase, 7) && span.Kind == ClassificationKind.EnumCase);
    }

    [Fact]
    public void Enum_pattern_payload_has_signature_help()
    {
        var (service, workspace, _, document) = Create(Source);
        var position = Source.IndexOf("let code", StringComparison.Ordinal);

        var help = service.GetSignatureHelp(workspace, document.Id, position);

        Assert.NotNull(help);
        Assert.Equal("failure", help!.Name);
        Assert.Equal(1, help.ActiveParameter);
        Assert.Equal(["String", "Int"], help.Signatures[0].Parameters.Select(parameter => parameter.Type));
    }

    private static (MartinLanguageService Service, LanguageWorkspaceSnapshot Workspace, LanguageProjectSnapshot Project,
        LanguageDocumentSnapshot Document) Create(string source)
    {
        var document = new LanguageDocumentSnapshot(DocumentId.CreateNew(), "main.martin", source, new(1));
        var project = new LanguageProjectSnapshot(ProjectId.CreateNew(), "test", ".", new(1), [document]);
        return (new(), new(WorkspaceId.CreateNew(), new(1), [project]), project, document);
    }
}
