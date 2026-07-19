using Martin.Build;
using Martin.Execution;
using Martin.Studio.Core;
using Xunit;

namespace Martin_Studio_Core_Tests;

public sealed class Phase5StudioCoreTests
{
    [Fact]
    public async Task Workspace_open_save_close_tracks_dirty_versions_and_duplicates()
    {
        using var f = new TempProject();
        var ws = new WorkspaceService();
        Assert.True(ws.OpenProject(f.Root).Success);
        var doc = ws.OpenDocument(f.Main);
        Assert.Same(doc, ws.OpenDocument(Path.Combine(f.Root, "Sources", "..", "Sources", "main.martin")));
        ws.ApplyEditorChange(doc.Id, "func main() { return 2 }");
        Assert.True(doc.IsDirty);
        Assert.Equal(1, doc.Version.Value);
        await ws.SaveAsync(doc);
        Assert.False(doc.IsDirty);
        Assert.Contains("return 2", File.ReadAllText(f.Main));
        Assert.True(ws.CloseDocument(doc.Id));
        Assert.Empty(ws.Workspace.OpenDocuments);
    }
    [Fact]
    public void Compilation_snapshot_uses_dirty_text_disk_text_versions_and_deterministic_order()
    {
        using var f = new TempProject(extra: true);
        var ws = new WorkspaceService();
        ws.OpenProject(f.Root);
        var doc = ws.OpenDocument(f.Main);
        ws.ApplyEditorChange(doc.Id, "func main() { return helper() }");
        var snap = ws.CreateSnapshot();
        Assert.Contains(snap.Sources, s => s.FilePath == f.Main && s.Text.Contains("helper()") && s.Version.HasValue && s.Version.Value.Value == 1);
        Assert.Contains(snap.Sources, s => s.FilePath == f.Extra && s.Text.Contains("func helper"));
        Assert.Equal(snap.Sources.Select(s => s.FilePath).OrderBy(x => x, StringComparer.OrdinalIgnoreCase), snap.Sources.Select(s => s.FilePath));
    }
    [Fact]
    public async Task Studio_core_services_expose_contracts_and_save_all_results()
    {
        using var f = new TempProject(extra: true);
        IWorkspaceService ws = new WorkspaceService();
        Assert.True(ws.OpenProject(f.Root).Success);
        var main = ws.OpenDocument(f.Main);
        var extra = ws.OpenDocument(f.Extra);
        ws.ApplyEditorChange(main.Id, "func main() { return 10 }");
        ws.ApplyEditorChange(extra.Id, "func helper() { return 20 }");
        var result = await ws.SaveAllWithResultsAsync();
        Assert.True(result.Success);
        Assert.Equal([f.Extra, f.Main], result.Results.Select(r => r.FilePath).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        Assert.All(result.Results, r => Assert.True(r.Success));
        Assert.False(main.IsDirty);
        Assert.False(extra.IsDirty);
    }
    [Fact]
    public void Diagnostics_reject_stale_and_convert_markers()
    {
        using var f = new TempProject();
        var ws = new WorkspaceService();
        ws.OpenProject(f.Root);
        var doc = ws.OpenDocument(f.Main);
        var service = new DiagnosticService(ws.Workspace);
        service.Apply(new(doc.Id, new(9), [new("OLD", OutputSeverity.Error, "old", doc.FilePath, new(1, 1, 1, 2), "test")]));
        Assert.Empty(ws.Workspace.Diagnostics);
        service.Apply(new(doc.Id, doc.Version, [new("NEW", OutputSeverity.Warning, "new", doc.FilePath, new(1, 1, 1, 2), "test")]));
        Assert.Single(service.ToEditor(doc.Id));
        Assert.Equal("NEW", service.Next()!.Code);
    }
    [Fact]
    public void Output_recent_settings_and_editor_protocol_work()
    {
        var output = new OutputService(2);
        output.Add(OutputChannel.Build, OutputSeverity.Info, "one");
        output.Add(OutputChannel.Program, OutputSeverity.Error, "two");
        output.Add(OutputChannel.Studio, OutputSeverity.Info, "three");
        Assert.Equal(["two", "three"], output.Entries.Select(e => e.Message));
        var recent = new RecentProjectService(1);
        recent.Add("/tmp/A/Martin.toml", "A");
        recent.Add("/tmp/B/Martin.toml", "B");
        Assert.Single(recent.Items);
        var json = EditorMessageProtocol.Serialize("openDocument", new { path = "a" }, "1");
        Assert.Equal("openDocument", EditorMessageProtocol.Deserialize(json)!.Type);
        Assert.Null(EditorMessageProtocol.Deserialize("{"));
    }
    [Fact]
    public async Task Build_and_execution_coordinators_prevent_overlap_and_capture_output()
    {
        using var f = new TempProject();
        var ws = new WorkspaceService();
        ws.OpenProject(f.Root);
        var output = new OutputService();
        var build = new BuildCoordinator(ws.Workspace, output, new FakeBuildService(delay: true));
        var t = build.BuildAsync();
        var overlapping = await build.BuildAsync();
        Assert.Contains(overlapping.Diagnostics, d => d.Code == "MRT5001");
        await t;
        var exec = new ExecutionCoordinator(ws.Workspace, output, new FakeExecutionService());
        var er = await exec.RunAsync(new() { Success = true, EntryPointPath = "fake" });
        Assert.True(er.Completed);
        Assert.Contains(output.Entries, e => e.Channel == OutputChannel.Program && e.Message.Contains("hello"));
    }
    sealed class FakeBuildService(bool delay = false) : IMartinBuildService
    {
        public async Task<BuildResult> BuildAsync(Martin.Compiler.Compilation c, BuildOptions o, CancellationToken ct = default)
        {
            if (delay)
                await Task.Delay(150, ct);
            return new() { Success = true, EntryPointPath = "fake" };
        }
    }
    sealed class FakeExecutionService : IMartinExecutionService
    {
        public Task<ExecutionResult> RunAsync(BuildResult b, ExecutionOptions o, CancellationToken ct = default) => Task.FromResult(new ExecutionResult { Started = true, Completed = true, ExitCode = 0, StandardOutput = "hello" });
    }
    sealed class TempProject : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "MartinPhase5Tests", Guid.NewGuid().ToString("N"));
        public string Main => Path.Combine(Root, "Sources", "main.martin");
        public string Extra => Path.Combine(Root, "Sources", "helper.martin");
        public TempProject(bool extra = false)
        {
            Directory.CreateDirectory(Path.Combine(Root, "Sources"));
            File.WriteAllText(Path.Combine(Root, "Martin.toml"), "manifest-version = 1\n\n[package]\nname = \"P\"\nversion = \"0.1.0\"\n\n[target]\nkind = \"executable\"\nframework = \"net8.0\"\nentry = \"main\"\n");
            File.WriteAllText(Main, "func main() { return 1 }");
            if (extra)
                File.WriteAllText(Extra, "func helper() { return 3 }");
        }
        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
    }
}

public sealed class EditorProtocolValidationTests
{
    [Fact]
    public void Open_document_payload_includes_restored_view_state()
    {
        var documentId = Guid.NewGuid();
        var payload = new OpenEditorDocumentPayload(
            documentId,
            "main.martin",
            "func main() {}",
            "martin",
            1,
            new EditorViewStatePayload(documentId, 7, 3, 120, 8));

        var envelope = EditorMessageProtocol.Deserialize(EditorMessageProtocol.Serialize("openDocument", payload));

        Assert.NotNull(envelope);
        Assert.Equal(7, envelope.Payload.GetProperty("viewState").GetProperty("cursorLine").GetInt32());
        Assert.Equal(120, envelope.Payload.GetProperty("viewState").GetProperty("scrollTop").GetDouble());
    }

    [Fact]
    public void Editor_protocol_rejects_malformed_known_payloads_without_throwing()
    {
        var bridge = new EditorBridge();
        var invalid = bridge.AcceptHostMessage("{\"type\":\"textChanged\",\"payload\":{\"documentId\":\"not-a-guid\",\"text\":\"x\",\"version\":1}}");
        var missing = bridge.AcceptHostMessage("{\"type\":\"saveRequested\",\"payload\":{}}");
        var badVersion = bridge.AcceptHostMessage($"{{\"type\":\"textChanged\",\"payload\":{{\"documentId\":\"{Guid.NewGuid()}\",\"text\":\"x\",\"version\":-1}}}}");

        Assert.False(invalid.IsValid);
        Assert.False(missing.IsValid);
        Assert.False(badVersion.IsValid);
    }

    [Fact]
    public void Editor_protocol_rejects_unknown_response_ids_and_invalid_outgoing_payloads()
    {
        var bridge = new EditorBridge();
        var unknownResponse = bridge.AcceptHostMessage("{\"type\":\"response\",\"id\":\"missing\",\"payload\":{}}");

        Assert.False(unknownResponse.IsValid);
        Assert.Throws<ArgumentException>(() => EditorMessageProtocol.Serialize("openDocument", new OpenEditorDocumentPayload(Guid.Empty, "main.martin", "", "martin", 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => EditorMessageProtocol.Serialize("revealRange", new RevealEditorRangePayload(Guid.NewGuid(), 2, 1, 1, 1)));
        Assert.Throws<ArgumentException>(() => EditorMessageProtocol.Serialize("setTheme", new SetEditorThemePayload(StudioTheme.Dark, EffectiveTheme: "Blue")));
    }
}
