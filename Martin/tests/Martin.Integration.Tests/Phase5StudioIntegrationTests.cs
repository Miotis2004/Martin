using Martin.Studio.Core;
using Xunit;

namespace Martin_Integration_Tests;

public sealed class Phase5StudioIntegrationTests
{
    [Fact]
    public async Task Studio_end_to_end_open_edit_snapshot_save_close_restore_shape()
    {
        var root=Path.Combine(Path.GetTempPath(),"MartinPhase5Integration",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root,"Sources"));
        var manifest=Path.Combine(root,"Martin.toml");
        var source=Path.Combine(root,"Sources","main.martin");
        File.WriteAllText(manifest,"manifest-version = 1\n\n[package]\nname = \"Integration\"\nversion = \"0.1.0\"\n\n[target]\nkind = \"executable\"\nframework = \"net8.0\"\nentry = \"main\"\n");
        File.WriteAllText(source,"func main() { return 1 }");
        try{
            var ws=new WorkspaceService(); Assert.True(ws.OpenProject(root).Success); var doc=ws.OpenDocument(source); ws.ApplyEditorChange(doc.Id,"func main() { return 42 }");
            Assert.Contains(ws.CreateSnapshot().Sources,s=>s.FilePath==source&&s.Text.Contains("42"));
            await ws.SaveAllAsync(); Assert.False(doc.IsDirty); Assert.Contains("42",File.ReadAllText(source)); Assert.True(ws.CloseDocument(doc.Id)); ws.CloseProject(); Assert.Null(ws.Workspace.Project);
        }finally{if(Directory.Exists(root))Directory.Delete(root,true);}    
    }
}
