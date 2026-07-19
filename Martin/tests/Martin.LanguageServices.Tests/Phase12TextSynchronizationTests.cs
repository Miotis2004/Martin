using Martin.Compiler.Text;
using Martin.LanguageServices;
using Martin.ProjectSystem;
using Xunit;

namespace Martin.LanguageServices.Tests;

public sealed class Phase12TextSynchronizationTests
{
    [Fact]
    public void Position_conversion_is_zero_based_utf16_and_preserves_line_endings()
    {
        var converter = PositionConverter.Instance;
        var text = SourceText.From("a😀\r\n\tCafe\u0301\n");

        Assert.Equal(3, converter.ToOffset(text, new(0, 3))); // emoji is two UTF-16 code units
        Assert.Equal(new MartinPosition(1, 1), converter.ToPosition(text, 6));
        Assert.Equal(new TextSpan(1, 5), converter.ToSpan(text, new(new(0, 1), new(1, 1))));
        Assert.Equal(new MartinRange(new(0, 1), new(1, 1)), converter.ToRange(text, new(1, 5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => converter.ToOffset(text, new(0, 4)));
        Assert.Throws<ArgumentException>(() => converter.ToSpan(text, new(new(1, 0), new(0, 0))));
    }

    [Fact]
    public async Task Incremental_changes_apply_in_reverse_and_stale_changes_request_recovery()
    {
        using var project = new TempProject("alpha beta gamma\r\n");
        await using var workspace = new LanguageWorkspace();
        var projectId = await workspace.OpenProjectAsync(project.Load());
        var id = await workspace.OpenDocumentAsync(projectId, project.Main,
            SourceText.From("alpha beta gamma\r\n", project.Main), new(4), true);

        var applied = await workspace.ApplyDocumentChangesAsync(new()
        {
            DocumentId = id,
            PreviousVersion = new(4),
            NewVersion = new(5),
            Changes = [new(new(0, 5), "A"), new(new(11, 5), "G")]
        });
        Assert.True(applied.IsApplied);
        Assert.Equal("A beta G\r\n", workspace.CurrentSnapshot.FindDocument(id)!.Text);

        var stale = await workspace.ApplyDocumentChangesAsync(new()
        {
            DocumentId = id, PreviousVersion = new(4), NewVersion = new(6), Changes = []
        });
        Assert.True(stale.RequiresFullTextResynchronization);
        Assert.Equal(new DocumentVersion(5), workspace.CurrentSnapshot.FindDocument(id)!.Version);

        await workspace.ReplaceDocumentTextAsync(id, new(5), new(7), SourceText.From("recovered\n"), true);
        Assert.Equal("recovered\n", workspace.CurrentSnapshot.FindDocument(id)!.Text);
    }

    [Fact]
    public async Task Invalid_overlapping_and_oversized_changes_are_controlled_rejections()
    {
        using var project = new TempProject("abcdef");
        await using var workspace = new LanguageWorkspace(new() { MaximumChangePayloadBytes = 3 });
        var projectId = await workspace.OpenProjectAsync(project.Load());
        var id = workspace.CurrentSnapshot.FindProject(projectId)!.Documents.Single().Id;

        var overlap = await workspace.ApplyDocumentChangesAsync(new()
        {
            DocumentId = id, PreviousVersion = new(0), NewVersion = new(1),
            Changes = [new(new(1, 3), "x"), new(new(3, 1), "y")]
        });
        Assert.Equal(DocumentChangeStatus.Rejected, overlap.Status);
        Assert.Equal("MRTLS1006", overlap.DiagnosticCode);

        var oversized = await workspace.ApplyDocumentChangesAsync(new()
        {
            DocumentId = id, PreviousVersion = new(0), NewVersion = new(1),
            Changes = [new(new(0, 0), "four")]
        });
        Assert.Equal("MRTLS1007", oversized.DiagnosticCode);
        Assert.Equal("abcdef", workspace.CurrentSnapshot.FindDocument(id)!.Text);
    }

    sealed class TempProject : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "MartinPhase12Text", Guid.NewGuid().ToString("N"));
        public string Main => Path.Combine(Root, "main.martin");
        public TempProject(string text)
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "Martin.toml"), "manifest-version = 1\n\n[package]\nname = \"Text\"\nversion = \"0.1.0\"\n\n[target]\nkind = \"executable\"\nframework = \"net8.0\"\nentry = \"main\"\n");
            File.WriteAllText(Main, text);
        }
        public MartinProject Load() => Assert.IsType<MartinProject>(MartinProjectLoader.Load(new ProjectLoadOptions { ProjectPath = Root }).Project);
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
