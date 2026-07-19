using System.Collections.Immutable;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using System.Reflection;
using Martin.Runtime;
using Martin.Build;
using Martin.Build.Artifacts;
using Martin.Build.BuildState;
using Martin.Execution;
using Martin.Compiler;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Syntax;
using Martin.Compiler.Text;
using Martin.ProjectSystem;

namespace Martin.Studio.Core;

public sealed class JsonSettingsService(string path,IStudioRecoveryService? recoveryService=null):ISettingsService
{
    static readonly JsonSerializerOptions Options=new(){WriteIndented=true};
    public async Task<StudioSettings> LoadAsync(CancellationToken ct=default)=>await AtomicJsonStore.LoadAsync(path,()=>new StudioSettings(),StudioRecoveryKind.SettingsCorruption,"LoadSettings",recoveryService,ct);
    public async Task SaveAsync(StudioSettings s,CancellationToken ct=default)=>await AtomicJsonStore.SaveAsync(path,s with{SchemaVersion=1},Options,ct);
}
public sealed class JsonSessionService(string path,IStudioRecoveryService? recoveryService=null):ISessionService
{
    static readonly JsonSerializerOptions Options=new(){WriteIndented=true};
    public async Task<StudioSession> LoadAsync(CancellationToken ct=default)=>await AtomicJsonStore.LoadAsync(path,()=>new StudioSession(),StudioRecoveryKind.SessionCorruption,"LoadSession",recoveryService,ct);
    public async Task SaveAsync(StudioSession s,CancellationToken ct=default)=>await AtomicJsonStore.SaveAsync(path,s with{SchemaVersion=1},Options,ct);
}
static class AtomicJsonStore
{
    public static async Task<T> LoadAsync<T>(string path,Func<T> fallback,StudioRecoveryKind corruptionKind,string operation,IStudioRecoveryService? recoveryService,CancellationToken ct)
    {
        try
        {
            if(!File.Exists(path))return fallback();
            return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path,ct))??fallback();
        }
        catch(Exception ex) when(ex is not OperationCanceledException)
        {
            PreserveCorruptFile(path);
            recoveryService?.Recover(corruptionKind,operation,ex);
            return fallback();
        }
    }
    static void PreserveCorruptFile(string path)
    {
        try
        {
            if(!File.Exists(path))return;
            var corruptPath=path+".corrupt."+DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff");
            File.Move(path,corruptPath,false);
        }
        catch(IOException){}
        catch(UnauthorizedAccessException){}
    }
    public static async Task SaveAsync<T>(string path,T value,JsonSerializerOptions options,CancellationToken ct)
    {
        var full=Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var tmp=full+"."+Guid.NewGuid().ToString("N")+".tmp";
        await File.WriteAllTextAsync(tmp,JsonSerializer.Serialize(value,options),ct);
        File.Move(tmp,full,true);
    }
}

public sealed class FileStudioLogService(string logsDirectory):IStudioLogService
{
    static readonly JsonSerializerOptions LogOptions=new(){WriteIndented=false};
    readonly object _gate=new();
    public string LogsDirectory{get;}=Path.GetFullPath(logsDirectory);
    public string CurrentLogPath=>Path.Combine(LogsDirectory,$"studio-{DateTimeOffset.UtcNow:yyyyMMdd}.jsonl");
    public void Log(StudioLogCategory category,OutputSeverity severity,string operation,string message,string? detail=null,Exception? exception=null)
    {
        var entry=new StudioLogEntry(DateTimeOffset.UtcNow,category,severity,operation,Sanitize(message),Sanitize(detail),exception?.GetType().FullName);
        Directory.CreateDirectory(LogsDirectory);
        var line=JsonSerializer.Serialize(entry,LogOptions)+Environment.NewLine;
        lock(_gate) File.AppendAllText(CurrentLogPath,line,Encoding.UTF8);
    }
    public IReadOnlyList<StudioLogEntry> ReadCurrent()
    {
        if(!File.Exists(CurrentLogPath))return [];
        var entries=new List<StudioLogEntry>();
        foreach(var line in File.ReadLines(CurrentLogPath))
        {
            try{var entry=JsonSerializer.Deserialize<StudioLogEntry>(line); if(entry is not null)entries.Add(entry);}catch(JsonException){}
        }
        return entries;
    }
    static string? Sanitize(string? value)
    {
        if(value is null)return null;
        var sanitized=value.Replace("\r","\\r").Replace("\n","\\n");
        return sanitized.Length>4096?sanitized[..4096]+"…":sanitized;
    }
}

public sealed class StudioRecoveryService(IOutputService output,IStudioLogService? logService=null):IStudioRecoveryService
{
    public StudioRecoveryResult Recover(StudioRecoveryKind kind,string operation,Exception? exception=null)
    {
        var (code,message)=kind switch
        {
            StudioRecoveryKind.SettingsCorruption => ("MRT5301","Settings could not be loaded; defaults were used."),
            StudioRecoveryKind.SessionCorruption => ("MRT5303","Session could not be restored; Studio started without the saved session."),
            StudioRecoveryKind.WatcherOverflow => ("MRT5009","Project file watcher overflowed; the project tree was refreshed."),
            StudioRecoveryKind.WebView2ProcessFailure => ("MRT5205","Editor process failed; Studio is attempting to recover the editor."),
            _ => ("MRT5404","Unexpected Studio failure was handled.")
        };
        output.Add(OutputChannel.Studio,OutputSeverity.Error,$"{code} {message}");
        logService?.Log(StudioLogCategory.Studio,OutputSeverity.Error,operation,message,exception?.Message,exception);
        return new(kind,true,message,code);
    }
}

public sealed class ProjectCreationService(IStudioLogService? logService=null):IProjectCreationService
{
    public Task<StudioProjectCreationResult> CreateAsync(StudioProjectCreationRequest request,CancellationToken cancellationToken=default)
    {
        logService?.Log(StudioLogCategory.ProjectSystem,OutputSeverity.Info,"CreateProject","Project creation started.",$"name={request.ProjectName};base={Path.GetFullPath(request.BaseDirectory)};target={request.TargetFramework};openAfterCreation={request.OpenAfterCreation}");
        var result=new MartinProjectCreator().Create(new MartinProjectCreationOptions{ProjectName=request.ProjectName,BasePath=request.BaseDirectory,TargetFramework=request.TargetFramework,CreateGitIgnore=request.CreateGitIgnore},cancellationToken);
        var manifest=result.ProjectDirectory is null?null:Path.Combine(result.ProjectDirectory,"Martin.toml");
        logService?.Log(StudioLogCategory.ProjectSystem,result.Success?OutputSeverity.Info:OutputSeverity.Error,"CreateProject",result.Success?"Project creation succeeded.":"Project creation failed.",$"manifest={manifest};diagnostics={result.Diagnostics.Count()}");
        return Task.FromResult(new StudioProjectCreationResult{Success=result.Success,ProjectDirectory=result.ProjectDirectory,ManifestPath=manifest,Diagnostics=result.Diagnostics.Select(d=>new StudioDiagnostic(d.Code,d.Severity==ProjectDiagnosticSeverity.Error?OutputSeverity.Error:OutputSeverity.Warning,d.Message,d.Path??manifest,null,"ProjectSystem")).ToImmutableArray()});
    }
}

public sealed class RecentProjectService(int maximum=10):IRecentProjectService{readonly List<RecentProjectEntry> _items=[]; public IReadOnlyList<RecentProjectEntry> Items=>_items; public void Load(IEnumerable<RecentProjectEntry> items){_items.Clear(); foreach(var item in items.OrderByDescending(i=>i.LastOpened).Take(maximum)){var full=Path.GetFullPath(item.ManifestPath); if(!_items.Any(x=>PathComparer.Equals(x.ManifestPath,full)))_items.Add(item with{ManifestPath=full});}} public void Add(string manifestPath,string? displayName=null){var full=Path.GetFullPath(manifestPath);_items.RemoveAll(x=>PathComparer.Equals(x.ManifestPath,full));_items.Insert(0,new(full,displayName??Path.GetFileName(Path.GetDirectoryName(full))??full,DateTimeOffset.UtcNow)); if(_items.Count>maximum)_items.RemoveRange(maximum,_items.Count-maximum);} public void Remove(string manifestPath){var full=Path.GetFullPath(manifestPath);_items.RemoveAll(x=>PathComparer.Equals(x.ManifestPath,full));} public void Clear()=>_items.Clear();}
public sealed class OutputService(int maximum=10_000):IOutputService
{
    readonly object _gate=new(); readonly List<OutputEntry> _entries=[];
    public IReadOnlyList<OutputEntry> Entries=>GetEntries();
    public void Add(OutputChannel c,OutputSeverity s,string m){lock(_gate){_entries.Add(new(DateTimeOffset.UtcNow,c,s,m)); if(_entries.Count>maximum)_entries.RemoveRange(0,_entries.Count-maximum);}}
    public IReadOnlyList<OutputEntry> GetEntries(OutputFilter? filter=null){lock(_gate)return _entries.Where(e=>Matches(e,filter)).ToArray();}
    public string CopyAll(OutputFilter? filter=null)=>string.Join(Environment.NewLine,GetEntries(filter).Select(Format));
    public async Task SaveLogAsync(string path,OutputFilter? filter=null,CancellationToken cancellationToken=default){var directory=Path.GetDirectoryName(path); if(!string.IsNullOrWhiteSpace(directory))Directory.CreateDirectory(directory); await File.WriteAllTextAsync(path,CopyAll(filter),cancellationToken);}
    public void Clear(){lock(_gate)_entries.Clear();}
    static bool Matches(OutputEntry e,OutputFilter? f)=>f is null || ((f.Channels is null||f.Channels.Contains(e.Channel))&&(f.Severities is null||f.Severities.Contains(e.Severity))&&(string.IsNullOrWhiteSpace(f.Text)||e.Message.Contains(f.Text,StringComparison.OrdinalIgnoreCase)||e.Channel.ToString().Contains(f.Text,StringComparison.OrdinalIgnoreCase)||e.Severity.ToString().Contains(f.Text,StringComparison.OrdinalIgnoreCase)));
    static string Format(OutputEntry e)=>$"{e.Timestamp:O} [{e.Channel}] [{e.Severity}] {e.Message}";
}

public sealed class WorkspaceService(IExternalChangePolicy? externalChangePolicy=null, IUiDispatcher? uiDispatcher=null, IStudioRecoveryService? recoveryService=null, IStudioLogService? logService=null):IWorkspaceService,IDisposable
{
    public event EventHandler? EditorSynchronizationRequested;
    public event Func<DocumentModel,CancellationToken,Task>? DocumentReloaded;
    public event Func<MartinProject,CancellationToken,Task>? ProjectRefreshed;
    FileSystemWatcher? _projectWatcher;
    readonly object _treeGate = new();
    readonly HashSet<string> _knownStudioSaves = new(PathComparer.Comparer);
    readonly object _watcherGate = new();
    bool _manifestReloadPending;

    public StudioWorkspace Workspace{get;}=new();

    public ProjectLoadResult OpenProject(string path){var r=MartinProjectLoader.Load(new(){ProjectPath=path}); ApplyProjectLoadResult(r); return r;}
    public ProjectLoadResult OpenProjectManifest(string manifestPath){var r=MartinProjectLoader.Load(new(){ManifestPath=manifestPath}); ApplyProjectLoadResult(r); return r;}
    public ProjectCandidate? LoadProjectCandidate(string path,bool isManifestPath,out ImmutableArray<StudioDiagnostic> diagnostics,CancellationToken cancellationToken=default)
    {
        var options=isManifestPath?new ProjectLoadOptions{ManifestPath=path}:new ProjectLoadOptions{ProjectPath=path};
        var result=MartinProjectLoader.Load(options,cancellationToken);
        var fallback=Path.GetFullPath(path);
        diagnostics=ProjectOpeningService.ToStudioDiagnostics(result.Diagnostics,fallback);
        return result.Success && result.Project is not null
            ? new ProjectCandidate{Project=result.Project,RootNode=CreateTree(result.Project,cancellationToken),Diagnostics=diagnostics}
            : null;
    }
    public void CommitProjectCandidate(ProjectCandidate candidate)
    {
        DisposeWatcher();
        Workspace.Generation=Workspace.Generation.Next();
        Workspace.Project=candidate.Project;
        Workspace.OpenDocuments.Clear();
        Workspace.ActiveDocument=null;
        Workspace.Diagnostics.Clear();
        foreach(var diagnostic in candidate.Diagnostics)Workspace.Diagnostics.Add(diagnostic);
        Workspace.ProjectTree=candidate.RootNode;
        StartWatcher(candidate.Project.RootDirectory);
    }
    public void CloseProject(){DisposeWatcher(); Workspace.Generation=Workspace.Generation.Next(); Workspace.Project=null;Workspace.OpenDocuments.Clear();Workspace.Diagnostics.Clear();Workspace.ActiveDocument=null;Workspace.ProjectTree=null;}
    public DocumentModel OpenDocument(string path){var full=Path.GetFullPath(path); EnsureInsideProject(full); var existing=Workspace.OpenDocuments.FirstOrDefault(d=>PathComparer.Equals(d.FilePath,full)); if(existing!=null){Workspace.ActiveDocument=existing;return existing;} var read=ReadDocument(full); var doc=new DocumentModel{Id=Guid.NewGuid(),FilePath=full,DisplayName=Path.GetFileName(full),Text=read.Text,Encoding=read.Encoding,LineEndings=DetectLineEnding(read.Text),IsReadOnly=IsReadOnly(full),IsDeleted=false,LastDiskWriteTime=File.GetLastWriteTimeUtc(full),SavedContentHash=HashFile(full)}; Workspace.OpenDocuments.Add(doc); Workspace.ActiveDocument=doc; return doc;}
    public void ActivateDocument(Guid id)=>Workspace.ActiveDocument=Workspace.OpenDocuments.FirstOrDefault(d=>d.Id==id) ?? Workspace.ActiveDocument;
    public void ApplyEditorChange(Guid id,string text){var d=Workspace.OpenDocuments.First(x=>x.Id==id); if(d.Text==text)return; d.Text=text; d.IsDirty=true; d.Version=d.Version.Next();}
    public void UpdateViewState(Guid id,EditorViewState viewState){var d=Workspace.OpenDocuments.FirstOrDefault(x=>x.Id==id); if(d is not null)d.ViewState=viewState;}
    public bool TryNavigateToDocument(string path,TextRange? range,out DocumentModel? document){document=null; if(string.IsNullOrWhiteSpace(path)||!File.Exists(path))return false; document=OpenDocument(path); if(range is not null) document.ViewState=document.ViewState with{CursorLine=range.StartLine,CursorColumn=range.StartColumn,Selections=[new SelectionRange(range.StartLine,range.StartColumn,range.EndLine,range.EndColumn)]}; return true;}
    public async Task SaveAsync(DocumentModel d,CancellationToken ct=default){var result=await SaveCoreAsync(d,d.FilePath,updateDocumentPath:false,ct); if(!result.Success)throw new IOException(result.Error);}
    public async Task<SaveDocumentResult> SaveAsAsync(DocumentModel d,string path,CancellationToken ct=default)=>await SaveCoreAsync(d,Path.GetFullPath(path),updateDocumentPath:true,ct);
    public async Task<SaveAllResult> SaveAllWithResultsAsync(CancellationToken ct=default)
    {
        var results=new List<SaveDocumentResult>();
        foreach(var d in Workspace.OpenDocuments.Where(d=>d.IsDirty).OrderBy(d=>d.FilePath,StringComparer.OrdinalIgnoreCase).ToArray())
        {
            ct.ThrowIfCancellationRequested();
            var result=await SaveCoreAsync(d,d.FilePath,updateDocumentPath:false,ct);
            results.Add(result);
        }
        return new(results.ToImmutableArray());
    }
    public async Task SaveAllAsync(CancellationToken ct=default)=>await SaveAllWithResultsAsync(ct);
    public bool CloseDocument(Guid id,bool force=false){var d=Workspace.OpenDocuments.FirstOrDefault(x=>x.Id==id); if(d==null)return true; if(d.IsDirty&&!force)return false; Workspace.OpenDocuments.Remove(d); if(Workspace.ActiveDocument==d)Workspace.ActiveDocument=Workspace.OpenDocuments.FirstOrDefault(); foreach(var old in Workspace.Diagnostics.Where(x=>x.DocumentId==id).ToArray())Workspace.Diagnostics.Remove(old); return true;}
    public CompilationSnapshot CreateSnapshot(CancellationToken cancellationToken=default)=>SnapshotFactory.CreateAsync(Workspace,cancellationToken).GetAwaiter().GetResult();
    public Task<CompilationSnapshot> CreateSnapshotAsync(CancellationToken cancellationToken=default)=>SnapshotFactory.CreateAsync(Workspace,cancellationToken);

    public void LoadProjectTreeChildren(ProjectTreeNode node,CancellationToken cancellationToken=default)
    {
        if (Workspace.Project is null || node.ChildrenLoaded || !Directory.Exists(node.FullPath)) return;
        lock(_treeGate)
        {
            if (node.ChildrenLoaded) return;
            node.Children.Clear();
            foreach (var child in EnumerateChildren(Workspace.Project,node.FullPath,cancellationToken)) node.Children.Add(child);
            node.ChildrenLoaded=true;
        }
    }

    public void RefreshProjectTree(CancellationToken cancellationToken=default)
    {
        if (Workspace.Project is null) { Workspace.ProjectTree=null; return; }
        lock(_treeGate) Workspace.ProjectTree=CreateTree(Workspace.Project,cancellationToken);
    }

    public async Task HandleExternalChangesAsync(CancellationToken cancellationToken=default)
    {
        if (uiDispatcher is not null && !uiDispatcher.HasThreadAccess)
        {
            await uiDispatcher.InvokeAsync(() => HandleExternalChangesAsync(cancellationToken), cancellationToken);
            return;
        }
        bool reloadManifest;
        lock (_watcherGate)
        {
            reloadManifest = _manifestReloadPending && Workspace.Project is not null;
            _manifestReloadPending = false;
        }
        var manifestReloadSucceeded = !reloadManifest || ReloadManifestSafely(cancellationToken);
        foreach (var document in Workspace.OpenDocuments.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_knownStudioSaves.Remove(document.FilePath))
                continue;

            var exists = File.Exists(document.FilePath);
            if (!exists)
            {
                document.IsDeleted = true;
                document.HasExternalChanges = true;
                await ResolveExternalChangeAsync(new ExternalChange(document, ExternalChangeKind.Deleted), cancellationToken);
                continue;
            }

            var currentHash = HashFile(document.FilePath);
            var currentWriteTime = File.GetLastWriteTimeUtc(document.FilePath);
            if (document.SavedContentHash is not null && string.Equals(currentHash, document.SavedContentHash, StringComparison.OrdinalIgnoreCase))
            {
                document.LastDiskWriteTime = currentWriteTime;
                document.HasExternalChanges = false;
                continue;
            }

            document.HasExternalChanges = true;
            await ResolveExternalChangeAsync(new ExternalChange(document, ExternalChangeKind.Changed), cancellationToken);
        }

        RefreshProjectTree(cancellationToken);
        if (Workspace.Project is not null && manifestReloadSucceeded)
        {
            Workspace.Generation=Workspace.Generation.Next();
            await NotifyProjectRefreshedAsync(Workspace.Project, cancellationToken);
        }
    }

    public bool TryOpenProjectTreeNode(ProjectTreeNode node,out DocumentModel? document)
    {
        document=null;
        if (node.Kind is ProjectNodeKind.Project or ProjectNodeKind.Folder or ProjectNodeKind.GeneratedFolder || !File.Exists(node.FullPath)) return false;
        if (!IsSupportedTextFile(node.FullPath))
        {
            Workspace.Diagnostics.Add(new StudioDiagnostic("MRT5012", OutputSeverity.Warning, $"Unsupported binary file: {node.FullPath}. Use Reveal in File Explorer instead.", node.FullPath, null, "Workspace", Generation: Workspace.Generation));
            return false;
        }
        document=OpenDocument(node.FullPath);
        return true;
    }

    public string CopyProjectTreeNodePath(ProjectTreeNode node)=>node.FullPath;
    public bool RevealProjectTreeNode(ProjectTreeNode node)=>File.Exists(node.FullPath)||Directory.Exists(node.FullPath);
    public void Dispose()=>DisposeWatcher();

    void ApplyProjectLoadResult(ProjectLoadResult r)
    {
        DisposeWatcher();
        if(r.Project is not null) Workspace.Generation=Workspace.Generation.Next(); Workspace.Project=r.Project; Workspace.OpenDocuments.Clear(); Workspace.ActiveDocument=null; Workspace.Diagnostics.Clear(); Workspace.ProjectTree=r.Project is null?null:CreateTree(r.Project);
        if (r.Project is not null) StartWatcher(r.Project.RootDirectory);
    }

    static ProjectTreeNode CreateTree(MartinProject p,CancellationToken ct=default)
    {
        var root=new ProjectTreeNode{Name=p.Manifest.Package.Name,FullPath=p.RootDirectory,Kind=ProjectNodeKind.Project,ChildrenLoaded=false};
        return root;
    }

    static IEnumerable<ProjectTreeNode> EnumerateChildren(MartinProject project,string directory,CancellationToken ct)
    {
        var root=Path.GetFullPath(project.RootDirectory);
        var generated=new HashSet<string>([Path.GetFullPath(Path.Combine(root,project.Manifest.Build.Output)),Path.GetFullPath(Path.Combine(root,project.Manifest.Build.Intermediate)),Path.GetFullPath(Path.Combine(root,".martin")),Path.GetFullPath(Path.Combine(root,"bin")),Path.GetFullPath(Path.Combine(root,"obj"))],PathComparer.Comparer);
        var dirs=Directory.EnumerateDirectories(directory).Select(Path.GetFullPath)
            .Where(d=>ProjectPathPolicy.IsContainedPath(root,d,ProjectPathPolicy.PathComparison))
            .Where(d=>!IsReparsePoint(d)).Where(d=>!generated.Contains(d))
            .OrderBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase);
        foreach(var dir in dirs){ct.ThrowIfCancellationRequested(); yield return new ProjectTreeNode{Name=Path.GetFileName(dir),FullPath=dir,Kind=ProjectNodeKind.Folder,ChildrenLoaded=false};}
        var files=Directory.EnumerateFiles(directory).Select(Path.GetFullPath)
            .Where(f=>ProjectPathPolicy.IsContainedPath(root,f,ProjectPathPolicy.PathComparison)||PathComparer.Equals(f,project.ManifestPath))
            .OrderBy(f=>Path.GetFileName(f).Equals("Martin.toml",StringComparison.OrdinalIgnoreCase)?0:1).ThenBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase);
        foreach(var file in files){ct.ThrowIfCancellationRequested(); yield return new ProjectTreeNode{Name=Path.GetFileName(file),FullPath=file,Kind=KindForFile(project,file),ChildrenLoaded=true};}
    }

    static ProjectNodeKind KindForFile(MartinProject project,string file)=>PathComparer.Equals(file,project.ManifestPath)?ProjectNodeKind.Manifest:file.EndsWith(".martin",StringComparison.OrdinalIgnoreCase)?ProjectNodeKind.MartinSourceFile:ProjectNodeKind.OtherFile;
    static bool IsReparsePoint(string path)=>(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0;
    void EnsureInsideProject(string full){if(Workspace.Project is not null && !PathComparer.Equals(full,Workspace.Project.ManifestPath) && !ProjectPathPolicy.IsContainedPath(Workspace.Project.RootDirectory,full,ProjectPathPolicy.PathComparison))throw new InvalidOperationException("Project Explorer cannot open files outside the project root.");}

    private System.Threading.Timer? _debounceTimer;
    private void HandleWatcherEvent(object sender, FileSystemEventArgs e)
    {
        if (Path.GetFileName(e.FullPath).Equals("Martin.toml", StringComparison.OrdinalIgnoreCase))
            lock (_watcherGate) _manifestReloadPending = true;
        _debounceTimer?.Change(500, Timeout.Infinite);
    }
    private void OnDebounceTimer(object? state) => ObserveWatcherTask(HandleExternalChangesAsync());

    void StartWatcher(string root){try{
        _debounceTimer = new System.Threading.Timer(OnDebounceTimer, null, Timeout.Infinite, Timeout.Infinite);
        _projectWatcher=new FileSystemWatcher(root){IncludeSubdirectories=true,NotifyFilter=NotifyFilters.FileName|NotifyFilters.DirectoryName|NotifyFilters.LastWrite}; FileSystemEventHandler refresh=HandleWatcherEvent; RenamedEventHandler renamed=(_,__)=>HandleWatcherEvent(_, __); _projectWatcher.Created+=refresh;_projectWatcher.Deleted+=refresh;_projectWatcher.Changed+=refresh;_projectWatcher.Renamed+=renamed;_projectWatcher.Error+=(_,e)=>HandleWatcherOverflow(root, e.GetException()); _projectWatcher.EnableRaisingEvents=true;}catch(Exception ex){logService?.Log(StudioLogCategory.Workspace,OutputSeverity.Warning,"StartWatcher","Project file watcher could not be started.",ex.Message,ex);_projectWatcher=null;}}

    void HandleWatcherOverflow(string root, Exception? exception)
    {
        recoveryService?.Recover(StudioRecoveryKind.WatcherOverflow, "ProjectWatcher", exception);
        lock (_watcherGate) _manifestReloadPending = true;
        _debounceTimer?.Change(0, Timeout.Infinite);
    }

    void ObserveWatcherTask(Task task)=>task.ContinueWith(t=>logService?.Log(StudioLogCategory.Workspace,OutputSeverity.Error,"ProjectWatcher","Watcher refresh failed.",t.Exception?.GetBaseException().Message,t.Exception?.GetBaseException()), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

    async Task NotifyProjectRefreshedAsync(MartinProject project, CancellationToken cancellationToken)
    {
        if (ProjectRefreshed is not { } handlers) return;
        foreach (Func<MartinProject,CancellationToken,Task> handler in handlers.GetInvocationList())
            await handler(project, cancellationToken);
    }

    void DisposeWatcher(){
        _debounceTimer?.Dispose(); _debounceTimer = null;
        if(_projectWatcher is not null){_projectWatcher.Dispose();_projectWatcher=null;}
    }
    bool ReloadManifestSafely(CancellationToken ct)
    {
        if (Workspace.Project is null) return true;
        var result = MartinProjectLoader.Load(new ProjectLoadOptions { ManifestPath = Workspace.Project.ManifestPath }, ct);
        if (!result.Success || result.Project is null)
        {
            foreach (var diagnostic in ProjectOpeningService.ToStudioDiagnostics(result.Diagnostics, Workspace.Project.ManifestPath))
                Workspace.Diagnostics.Add(diagnostic with { Generation = Workspace.Generation });
            logService?.Log(StudioLogCategory.ProjectSystem, OutputSeverity.Warning, "ReloadManifest", "Manifest reload failed; preserving the current valid project state.");
            return false;
        }

        Workspace.Project = result.Project;
        return true;
    }

    static bool IsSupportedTextFile(string path)
    {
        var extension = Path.GetExtension(path);
        if (extension.Equals(".martin", StringComparison.OrdinalIgnoreCase) || extension.Equals(".toml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) || extension.Equals(".md", StringComparison.OrdinalIgnoreCase) || extension.Equals(".json", StringComparison.OrdinalIgnoreCase)) return true;
        Span<byte> buffer = stackalloc byte[512];
        using var stream = File.OpenRead(path);
        var read = stream.Read(buffer);
        return !buffer[..read].Contains((byte)0);
    }

    static LineEndingKind DetectLineEnding(string t)=>t.Contains("\r\n")?LineEndingKind.Crlf:t.Contains('\r')?LineEndingKind.Cr:LineEndingKind.Lf;
    static bool IsReadOnly(string path)=>File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReadOnly)!=0;
    static (string Text, Encoding Encoding) ReadDocument(string path)
    {
        var bytes=File.ReadAllBytes(path);
        Encoding encoding=bytes switch
        {
            [0xEF,0xBB,0xBF,..] => new UTF8Encoding(true),
            [0xFF,0xFE,..] => Encoding.Unicode,
            [0xFE,0xFF,..] => Encoding.BigEndianUnicode,
            _ => new UTF8Encoding(false)
        };
        return (encoding.GetString(bytes,encoding.GetPreamble().Length,bytes.Length-encoding.GetPreamble().Length),encoding);
    }
    static string NormalizeLineEndings(string text,LineEndingKind kind)
    {
        var normalized=text.Replace("\r\n","\n").Replace('\r','\n');
        return kind switch{LineEndingKind.Crlf=>normalized.Replace("\n","\r\n"),LineEndingKind.Cr=>normalized.Replace('\n','\r'),_=>normalized};
    }
    async Task ResolveExternalChangeAsync(ExternalChange change,CancellationToken ct)
    {
        var d=change.Document;
        if(!d.IsDirty)
        {
            var choice=externalChangePolicy is null?CleanExternalChangeChoice.KeepCurrentView:await externalChangePolicy.ConfirmCleanChangeAsync(change,ct);
            if(choice==CleanExternalChangeChoice.Reload && File.Exists(d.FilePath)) await ReloadDocumentFromDiskAsync(d,ct);
            else { d.SavedContentHash=File.Exists(d.FilePath)?HashFile(d.FilePath):null; d.LastDiskWriteTime=File.Exists(d.FilePath)?File.GetLastWriteTimeUtc(d.FilePath):null; d.HasExternalChanges=false; }
            return;
        }

        var dirtyChoice=externalChangePolicy is null?DirtyExternalChangeChoice.Cancel:await externalChangePolicy.ConfirmDirtyChangeAsync(change,ct);
        if(dirtyChoice==DirtyExternalChangeChoice.ReloadDiskVersion && File.Exists(d.FilePath)) await ReloadDocumentFromDiskAsync(d,ct);
        else if(dirtyChoice==DirtyExternalChangeChoice.KeepEditorVersion){ d.SavedContentHash=File.Exists(d.FilePath)?HashFile(d.FilePath):null; d.LastDiskWriteTime=File.Exists(d.FilePath)?File.GetLastWriteTimeUtc(d.FilePath):null; d.HasExternalChanges=false; }
        else if(dirtyChoice==DirtyExternalChangeChoice.SaveAs && externalChangePolicy is not null){var saveAs=await externalChangePolicy.GetSaveAsPathAsync(d,ct); if(!string.IsNullOrWhiteSpace(saveAs)) await SaveAsAsync(d,saveAs,ct);}
    }

    async Task ReloadDocumentFromDiskAsync(DocumentModel d,CancellationToken ct)
    {
        var read=ReadDocument(d.FilePath);
        d.Text=read.Text; d.Encoding=read.Encoding; d.LineEndings=DetectLineEnding(read.Text); d.IsDirty=false; d.IsDeleted=false; d.IsReadOnly=IsReadOnly(d.FilePath); d.LastDiskWriteTime=File.GetLastWriteTimeUtc(d.FilePath); d.SavedContentHash=HashFile(d.FilePath); d.HasExternalChanges=false; d.Version=d.Version.Next();
        if(DocumentReloaded is not null)
            foreach(Func<DocumentModel,CancellationToken,Task> handler in DocumentReloaded.GetInvocationList()) await handler(d,ct);
    }

    static bool HasExternalChange(DocumentModel d,string path)=>File.Exists(path)&&d.SavedContentHash is not null&&!string.Equals(HashFile(path),d.SavedContentHash,StringComparison.OrdinalIgnoreCase);
    static string HashFile(string path){using var stream=File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream));}

    async Task<SaveDocumentResult> SaveCoreAsync(DocumentModel d,string path,bool updateDocumentPath,CancellationToken ct)
    {
        string? temporaryPath=null;
        try
        {
            path=Path.GetFullPath(path);
            EnsureInsideProject(path);
            if(File.Exists(path)&&IsReadOnly(path)){d.IsReadOnly=PathComparer.Equals(path,d.FilePath); logService?.Log(StudioLogCategory.Document,OutputSeverity.Warning,updateDocumentPath?"SaveAs":"Save","Save blocked by read-only document.",$"path={path}"); return new(path,false,"Document is read-only.");}
            if(HasExternalChange(d,path))
            {
                d.HasExternalChanges=PathComparer.Equals(path,d.FilePath);
                logService?.Log(StudioLogCategory.Document,OutputSeverity.Warning,updateDocumentPath?"SaveAs":"Save","Save blocked by external changes.",$"path={path};documentId={d.Id}");
                return new(path,false,"External changes detected. Reload, Save As, or explicitly keep the editor version before saving.");
            }

            var targetDirectory=Path.GetDirectoryName(path);
            if(string.IsNullOrWhiteSpace(targetDirectory))return new(path,false,"Save target has no directory.");
            Directory.CreateDirectory(targetDirectory);
            temporaryPath=Path.Combine(targetDirectory,Path.GetFileName(path)+"."+Guid.NewGuid().ToString("N")+".tmp");
            var text=NormalizeLineEndings(d.Text,d.LineEndings);
            await using(var stream=new FileStream(temporaryPath,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,FileOptions.WriteThrough|FileOptions.Asynchronous))
            await using(var writer=new StreamWriter(stream,d.Encoding,81920,leaveOpen:false))
            {
                await writer.WriteAsync(text.AsMemory(),ct);
                await writer.FlushAsync(ct);
                await stream.FlushAsync(ct);
            }

            _knownStudioSaves.Add(path);
            File.Move(temporaryPath,path,true);
            temporaryPath=null;

            if(updateDocumentPath)
            {
                foreach(var duplicate in Workspace.OpenDocuments.Where(x=>x!=d&&PathComparer.Equals(x.FilePath,path)).ToArray())
                    Workspace.OpenDocuments.Remove(duplicate);
                d.FilePath=path;
                d.DisplayName=Path.GetFileName(path);
                RefreshProjectTree(ct);
                EditorSynchronizationRequested?.Invoke(this, EventArgs.Empty);
            }
            d.IsDirty=false; d.IsDeleted=false; d.HasExternalChanges=false; d.IsReadOnly=IsReadOnly(path); d.LastDiskWriteTime=File.GetLastWriteTimeUtc(path); d.SavedContentHash=HashFile(path);
            logService?.Log(StudioLogCategory.Document,OutputSeverity.Info,updateDocumentPath?"SaveAs":"Save",updateDocumentPath?"Document saved as.":"Document saved.",$"path={path};documentId={d.Id};bytes={new FileInfo(path).Length}");
            return new(path,true);
        }
        catch(Exception ex) when(ex is not OperationCanceledException)
        {
            d.IsDeleted=!File.Exists(d.FilePath);
            if(temporaryPath is not null&&File.Exists(temporaryPath))
            {
                try{File.Delete(temporaryPath);}catch{}
            }
            logService?.Log(StudioLogCategory.Document,OutputSeverity.Error,updateDocumentPath?"SaveAs":"Save","Document save failed.",$"path={path};documentId={d.Id};error={ex.Message}",ex);
            return new(path,false,ex.Message);
        }
    }
}


public sealed class CancelDirtyProjectTransitionPolicy:IDirtyProjectTransitionPolicy{public Task<DirtyProjectTransitionChoice> ConfirmAsync(IReadOnlyList<DocumentModel> dirtyDocuments,CancellationToken cancellationToken=default)=>Task.FromResult(DirtyProjectTransitionChoice.Cancel);}

public sealed class ProjectOpeningService(IWorkspaceService workspace,IRecentProjectService recentProjects,ISettingsService settingsService,IOutputService outputService,IDirtyProjectTransitionPolicy? dirtyPolicy=null,ISessionService? sessionService=null,IStudioLogService? logService=null):IProjectOpeningService
{
    public Task<bool> ApproveProjectTransitionAsync(CancellationToken ct=default) => PrepareTransitionAsync(ct);

    public Task<ProjectOpenResult> OpenProjectAsync(string path,CancellationToken ct=default) => OpenProjectCoreAsync(path, true, ct);

    public Task<ProjectOpenResult> OpenProjectAfterApprovedTransitionAsync(string path,CancellationToken ct=default) => OpenProjectCoreAsync(path, false, ct);

    async Task<ProjectOpenResult> OpenProjectCoreAsync(string path,bool approveTransition,CancellationToken ct)
    {
        var full=Path.GetFullPath(path);
        logService?.Log(StudioLogCategory.ProjectSystem,OutputSeverity.Info,"OpenProject","Project open started.",$"path={full}");
        var isManifest=Path.GetFileName(full).Equals("Martin.toml",StringComparison.OrdinalIgnoreCase);
        var candidate=workspace.LoadProjectCandidate(full,isManifest,out var diagnostics,ct);
        if(candidate is null)
        {
            foreach(var diagnostic in diagnostics)workspace.Workspace.Diagnostics.Add(diagnostic);
            outputService.Add(OutputChannel.Studio,OutputSeverity.Error,$"Project could not be opened: {full}");
            logService?.Log(StudioLogCategory.ProjectSystem,OutputSeverity.Error,"OpenProject","Project open failed.",$"path={full};diagnostics={diagnostics.Length}");
            return new(false,full,null,diagnostics);
        }
        if(approveTransition&&!await PrepareTransitionAsync(ct)){logService?.Log(StudioLogCategory.Workspace,OutputSeverity.Warning,"ProjectTransition","Project open canceled during transition.",$"path={full}"); return ProjectOpenResult.Failed("Project open canceled because documents have unsaved changes.",full,"MRT5003");}
        var targetSession=sessionService is null?null:await sessionService.LoadAsync(ct);
        await SaveSessionAsync(ct);
        workspace.CommitProjectCandidate(candidate);
        var project=candidate.Project;
        recentProjects.Add(project.ManifestPath,project.Manifest.Package.Name);
        await PersistSettingsAsync(project.ManifestPath,ct);
        if(targetSession is not null)RestoreSessionState(project.ManifestPath,targetSession,ct);
        outputService.Add(OutputChannel.Studio,OutputSeverity.Info,$"Opened project {project.Manifest.Package.Name}.");
        logService?.Log(StudioLogCategory.ProjectSystem,OutputSeverity.Info,"OpenProject","Project open succeeded.",$"manifest={project.ManifestPath};documents={workspace.Workspace.OpenDocuments.Count}");
        return new(true,project.ManifestPath,project.Manifest.Package.Name,candidate.Diagnostics);
    }

    public async Task<ProjectOpenResult> OpenRecentProjectAsync(RecentProjectEntry entry,CancellationToken ct=default)
    {
        if(!File.Exists(entry.ManifestPath)){recentProjects.Remove(entry.ManifestPath); await PersistSettingsAsync(null,ct); logService?.Log(StudioLogCategory.ProjectSystem,OutputSeverity.Warning,"OpenRecentProject","Recent project manifest no longer exists.",$"manifest={entry.ManifestPath}"); return ProjectOpenResult.Failed($"Recent project manifest no longer exists: {entry.ManifestPath}",entry.ManifestPath);}
        return await OpenProjectAsync(entry.ManifestPath,ct);
    }

    public async Task<ProjectOpenResult?> ReopenLastProjectAsync(CancellationToken ct=default)
    {
        var settings=await settingsService.LoadAsync(ct);
        recentProjects.Load(settings.RecentProjects);
        if(!settings.ReopenLastProject||string.IsNullOrWhiteSpace(settings.LastProject))return null;
        if(!File.Exists(settings.LastProject))return ProjectOpenResult.Failed($"Last project manifest no longer exists: {settings.LastProject}",settings.LastProject);
        return await OpenProjectAsync(settings.LastProject,ct);
    }

    public async Task SaveSessionAsync(CancellationToken ct=default)
    {
        if(sessionService is null||workspace.Workspace.Project is null)return;
        var active=workspace.Workspace.ActiveDocument?.FilePath;
        var current=await sessionService.LoadAsync(ct);
        var session=current with
        {
            ProjectPath=workspace.Workspace.Project?.ManifestPath,
            OpenDocuments=workspace.Workspace.OpenDocuments.Select(d=>new DocumentSessionState(d.FilePath,d.ViewState)).ToImmutableArray(),
            ActiveDocument=active,
            ExpandedProjectTreePaths=CollectExpandedPaths(workspace.Workspace.ProjectTree).ToImmutableArray(),
            ActiveBuildConfiguration=workspace.Workspace.ActiveBuildConfiguration
        };
        await sessionService.SaveAsync(session,ct);
    }

    public async Task<bool> CloseProjectAsync(CancellationToken ct=default)
    {
        if(!await PrepareTransitionAsync(ct))return false;
        await SaveSessionAsync(ct);
        workspace.CloseProject();
        await PersistSettingsAsync(null,ct);
        outputService.Add(OutputChannel.Studio,OutputSeverity.Info,"Closed project.");
        logService?.Log(StudioLogCategory.ProjectSystem,OutputSeverity.Info,"CloseProject","Project closed.");
        return true;
    }

    async Task<bool> PrepareTransitionAsync(CancellationToken ct)
    {
        if(workspace.Workspace.BuildState is BuildState.Building or BuildState.Cancelling || workspace.Workspace.ExecutionState is ExecutionState.Running or ExecutionState.ExternalLaunching or ExecutionState.Stopping)
        {
            outputService.Add(OutputChannel.Studio,OutputSeverity.Warning,"Project transition canceled because a build or execution operation is active.");
            logService?.Log(StudioLogCategory.Workspace,OutputSeverity.Warning,"ProjectTransition","Project transition canceled because a build or execution operation is active.");
            return false;
        }
        var dirty=workspace.Workspace.OpenDocuments.Where(d=>d.IsDirty).ToArray();
        if(dirty.Length==0)return true;
        var choice=dirtyPolicy is null?DirtyProjectTransitionChoice.Cancel:await dirtyPolicy.ConfirmAsync(dirty,ct);
        if(choice==DirtyProjectTransitionChoice.Cancel){logService?.Log(StudioLogCategory.Workspace,OutputSeverity.Warning,"ProjectTransition","Project transition canceled because dirty documents were not saved.",$"dirtyDocuments={dirty.Length}"); return false;}
        if(choice==DirtyProjectTransitionChoice.SaveAndContinue){var saved=await workspace.SaveAllWithResultsAsync(ct); logService?.Log(StudioLogCategory.Document,saved.Success?OutputSeverity.Info:OutputSeverity.Error,"SaveAllBeforeTransition",saved.Success?"Dirty documents saved before project transition.":"Saving dirty documents before project transition failed.",$"dirtyDocuments={dirty.Length};results={saved.Results.Length}"); return saved.Success;}
        return true;
    }


    void RestoreSessionState(string projectPath,StudioSession session,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(session.ProjectPath))return;
        if(!PathComparer.Equals(Path.GetFullPath(projectPath),Path.GetFullPath(session.ProjectPath)))return;
        RestoreExpandedPaths(workspace.Workspace.ProjectTree, session.ExpandedProjectTreePaths);
        workspace.Workspace.ActiveBuildConfiguration=session.ActiveBuildConfiguration;
        foreach(var documentState in session.OpenDocuments)
        {
            ct.ThrowIfCancellationRequested();
            if(string.IsNullOrWhiteSpace(documentState.FilePath)||!File.Exists(documentState.FilePath))continue;
            try{var document=workspace.OpenDocument(documentState.FilePath); document.ViewState=documentState.ViewState;}catch{ }
        }
        if(!string.IsNullOrWhiteSpace(session.ActiveDocument))
        {
            var active=workspace.Workspace.OpenDocuments.FirstOrDefault(d=>PathComparer.Equals(d.FilePath,session.ActiveDocument));
            if(active is not null)workspace.ActivateDocument(active.Id);
        }
    }

    static void RestoreExpandedPaths(ProjectTreeNode? node, ImmutableArray<string> expandedPaths)
    {
        if(node is null || expandedPaths.IsDefaultOrEmpty)return;
        var set=expandedPaths.ToHashSet(PathComparer.Comparer);
        ApplyExpansion(node,set);
    }
    static void ApplyExpansion(ProjectTreeNode node, HashSet<string> expandedPaths)
    {
        node.IsExpanded=expandedPaths.Contains(node.FullPath);
        foreach(var child in node.Children)ApplyExpansion(child,expandedPaths);
    }

    static IEnumerable<string> CollectExpandedPaths(ProjectTreeNode? node)
    {
        if(node is null)yield break;
        if(node.IsExpanded)yield return node.FullPath;
        foreach(var child in node.Children)foreach(var path in CollectExpandedPaths(child))yield return path;
    }

    async Task PersistSettingsAsync(string? lastProject,CancellationToken ct)
    {
        var current=await settingsService.LoadAsync(ct);
        await settingsService.SaveAsync(current with{RecentProjects=recentProjects.Items.ToImmutableArray(),LastProject=lastProject},ct);
    }

    public static ImmutableArray<StudioDiagnostic> ToStudioDiagnostics(IEnumerable<ProjectDiagnostic> diagnostics,string fallbackPath)=>diagnostics.Select(d=>new StudioDiagnostic(d.Code,d.Severity==ProjectDiagnosticSeverity.Error?OutputSeverity.Error:OutputSeverity.Warning,d.Message,d.Path??fallbackPath,null,"ProjectSystem")).ToImmutableArray();
}

public sealed class DiagnosticService(StudioWorkspace workspace):IDiagnosticService
{
    int _nav=-1;
    public void Apply(DocumentDiagnostics d){var doc=workspace.OpenDocuments.FirstOrDefault(x=>x.Id==d.DocumentId); if(doc==null||doc.Version!=d.Version)return; foreach(var old in workspace.Diagnostics.Where(x=>x.DocumentId==d.DocumentId).ToArray())workspace.Diagnostics.Remove(old); foreach(var diag in d.Diagnostics)workspace.Diagnostics.Add(diag with{DocumentId=d.DocumentId,Version=d.Version,Generation=workspace.Generation});}
    public ImmutableArray<EditorDiagnostic> ToEditor(Guid id)
    {
        var doc=workspace.OpenDocuments.FirstOrDefault(x=>x.Id==id);
        if(doc is null)return [];
        return workspace.Diagnostics.Where(d=>d.DocumentId==id&&d.Range!=null&&IsCurrent(d,doc)).Select(d=>new EditorDiagnostic(d.Code,d.Severity,d.Message,Clamp(d.Range!,doc.Text),d.Source)).ToImmutableArray();
    }
    public IReadOnlyList<StudioDiagnostic> GetDiagnostics(DiagnosticFilter? filter=null)=>workspace.Diagnostics.Where(d=>Matches(d,filter)).OrderByDescending(d=>d.Severity).ThenBy(d=>d.FilePath??string.Empty,StringComparer.OrdinalIgnoreCase).ThenBy(d=>d.Range?.StartLine??0).ThenBy(d=>d.Range?.StartColumn??0).ThenBy(d=>d.Code,StringComparer.OrdinalIgnoreCase).ToArray();
    public string CopyAll(DiagnosticFilter? filter=null)=>string.Join(Environment.NewLine,GetDiagnostics(filter).Select(Format));
    public StudioDiagnostic? Next(DiagnosticFilter? filter=null)=>Navigate(1,filter);
    public StudioDiagnostic? Previous(DiagnosticFilter? filter=null)=>Navigate(-1,filter);
    StudioDiagnostic? Navigate(int delta,DiagnosticFilter? filter){var list=GetDiagnostics(filter); if(list.Count==0)return null; _nav=(_nav+delta)%list.Count; if(_nav<0)_nav+=list.Count; return list[_nav];}
    static bool Matches(StudioDiagnostic d,DiagnosticFilter? f)=>f is null || ((f.Severities is null||f.Severities.Contains(d.Severity))&&(f.Sources is null||f.Sources.Contains(d.Source))&&(string.IsNullOrWhiteSpace(f.Text)||d.Message.Contains(f.Text,StringComparison.OrdinalIgnoreCase)||d.Code.Contains(f.Text,StringComparison.OrdinalIgnoreCase)||(d.FilePath?.Contains(f.Text,StringComparison.OrdinalIgnoreCase)??false)||d.Source.Contains(f.Text,StringComparison.OrdinalIgnoreCase)));
    bool IsCurrent(StudioDiagnostic d,DocumentModel doc)=>d.Generation==workspace.Generation&&d.Version==doc.Version;
    static TextRange Clamp(TextRange range,string text)
    {
        var lines=text.Replace("\r\n","\n").Replace('\r','\n').Split('\n');
        var maxLine=Math.Max(1,lines.Length);
        var sl=Math.Clamp(range.StartLine,1,maxLine); var el=Math.Clamp(range.EndLine,sl,maxLine);
        var sc=Math.Clamp(range.StartColumn,1,lines[sl-1].Length+1); var ec=Math.Clamp(range.EndColumn,1,lines[el-1].Length+1);
        if(el==sl&&ec<sc)ec=sc;
        return new(sl,sc,el,ec);
    }
    static string Format(StudioDiagnostic d)=>$"{d.Severity} {d.Code} {d.Message} {d.FilePath ?? string.Empty} {d.Range?.StartLine.ToString() ?? string.Empty}:{d.Range?.StartColumn.ToString() ?? string.Empty} {d.Source}";
}
public sealed class BuildCoordinator(StudioWorkspace workspace,OutputService output,IMartinBuildService build,IStudioLogService? logService=null):IBuildCoordinator
{
    CancellationTokenSource? _cts;

    public async Task<BuildResult> BuildAsync(CancellationToken ct=default)
    {
        if(workspace.BuildState==BuildState.Building)return Failure("MRT5001","A build is already running.");
        if(workspace.Project==null)return Failure("MRT5002","No project is open.");

        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct);
        _cts=linked;
        workspace.BuildState=BuildState.Building;
        workspace.Diagnostics.Clear();
        output.Add(OutputChannel.Build,OutputSeverity.Info,"Build started.");
        logService?.Log(StudioLogCategory.Build,OutputSeverity.Info,"Build","Build started.",$"configuration={workspace.ActiveBuildConfiguration};project={workspace.Project.ManifestPath}");
        try
        {
            var snap=await SnapshotFactory.CreateAsync(workspace,linked.Token);
            foreach(var diagnostic in snap.Diagnostics)workspace.Diagnostics.Add(diagnostic);
            if(snap.HasSourceReadErrors)
            {
                output.Add(OutputChannel.Build,OutputSeverity.Error,"Build failed while creating source snapshot.");
                logService?.Log(StudioLogCategory.Build,OutputSeverity.Error,"CreateSnapshot","Build snapshot creation failed.",$"diagnostics={snap.Diagnostics.Length};generation={snap.Generation.Value}");
                return Failure("MRT5010","Build failed while creating source snapshot.");
            }

            var p=workspace.Project;
            var result=await build.BuildAsync(snap.Compilation,new()
            {
                AssemblyName=p.Manifest.Package.Name,
                OutputDirectory=BuildOutputDirectory(p, workspace.ActiveBuildConfiguration),
                Configuration=workspace.ActiveBuildConfiguration,
                TargetFramework=p.Manifest.Target.Framework,
                UseAppHost=false,
                BuildStateInputs=new ProjectBuildStateInputs
                {
                    ProjectRoot=p.RootDirectory,
                    ManifestPath=p.ManifestPath,
                    SourceFiles=snap.Sources.Select(source=>source.FilePath).ToImmutableArray(),
                    CompilerVersion=InformationalVersion(typeof(Compilation).Assembly),
                    RuntimeVersion=MartinRuntimeInfo.RuntimeVersion
                }
            },linked.Token);
            AddProcessOutput(result);
            ApplyDiagnostics(result.Diagnostics,snap);
            if(result.WasCancelled)output.Add(OutputChannel.Build,OutputSeverity.Warning,"Build cancelled.");
            else output.Add(OutputChannel.Build,result.Success?OutputSeverity.Info:OutputSeverity.Error,result.Success?"Build succeeded.":"Build failed.");
            logService?.Log(StudioLogCategory.Build,result.WasCancelled?OutputSeverity.Warning:result.Success?OutputSeverity.Info:OutputSeverity.Error,"Build",result.WasCancelled?"Build cancelled.":result.Success?"Build succeeded.":"Build failed.",$"diagnostics={result.Diagnostics.Length};entryPoint={result.EntryPointPath}");
            return result;
        }
        catch(OperationCanceledException)
        {
            var result=new BuildResult{Status=BuildStatus.Cancelled,Diagnostics=[Diag("MRT3010","The build was cancelled.")]};
            ApplyDiagnostics(result.Diagnostics);
            output.Add(OutputChannel.Build,OutputSeverity.Warning,"Build cancelled.");
            logService?.Log(StudioLogCategory.Build,OutputSeverity.Warning,"Build","Build cancelled.");
            return result;
        }
        finally{workspace.BuildState=BuildState.Idle;_cts=null;}
    }

    public Task<ProjectCleanResult> CleanAsync(CancellationToken ct=default)
    {
        if(workspace.BuildState==BuildState.Building)return Task.FromResult(CleanFailure("MRT5001","A build is already running."));
        if(workspace.Project==null)return Task.FromResult(CleanFailure("MRT5002","No project is open."));
        workspace.Diagnostics.Clear();
        output.Add(OutputChannel.Build,OutputSeverity.Info,"Clean started.");
        var p=workspace.Project;
        var result=new MartinProjectCleaner().Clean(new ProjectCleanOptions
        {
            ProjectRoot=p.RootDirectory,
            TargetPaths=
            [
                Path.Combine(p.Manifest.Build.Output,workspace.ActiveBuildConfiguration.ToString(),p.Manifest.Target.Framework),
                Path.Combine(p.Manifest.Build.Intermediate,workspace.ActiveBuildConfiguration.ToString(),p.Manifest.Target.Framework)
            ]
        });
        foreach(var diagnostic in result.Diagnostics.Select(ToStudioDiagnostic))workspace.Diagnostics.Add(diagnostic);
        output.Add(OutputChannel.Build,result.Success?OutputSeverity.Info:OutputSeverity.Error,result.Success?"Clean succeeded.":"Clean failed.");
        logService?.Log(StudioLogCategory.Build,result.Success?OutputSeverity.Info:OutputSeverity.Error,"Clean",result.Success?"Clean succeeded.":"Clean failed.",$"diagnostics={result.Diagnostics.Count()}");
        return Task.FromResult(result);
    }

    public void Cancel(){if(_cts!=null){workspace.BuildState=BuildState.Cancelling;output.Add(OutputChannel.Build,OutputSeverity.Warning,"Cancel build requested.");logService?.Log(StudioLogCategory.Build,OutputSeverity.Warning,"CancelBuild","Cancel build requested.");_cts.Cancel();}}

    void AddProcessOutput(BuildResult result)
    {
        if(!string.IsNullOrWhiteSpace(result.StandardOutput))output.Add(OutputChannel.Build,OutputSeverity.Info,result.StandardOutput.TrimEnd());
        if(!string.IsNullOrWhiteSpace(result.StandardError))output.Add(OutputChannel.Build,OutputSeverity.Error,result.StandardError.TrimEnd());
    }

    void ApplyDiagnostics(IEnumerable<Diagnostic> diagnostics, CompilationSnapshot? snapshot=null)
    {
        foreach(var diagnostic in diagnostics.Select(d=>ToStudioDiagnostic(d,snapshot)))workspace.Diagnostics.Add(diagnostic);
    }

    static StudioDiagnostic ToStudioDiagnostic(Diagnostic d, CompilationSnapshot? snapshot=null)
    {
        var span=d.Location.LineSpan;
        var range=new TextRange(span.Start.Line,span.Start.Character+1,span.End.Line,span.End.Character+1);
        var filePath=d.Location.FilePath;
        SnapshotSource? source=null;
        if(snapshot is not null && !string.IsNullOrWhiteSpace(filePath))
            source=snapshot.Sources.FirstOrDefault(s=>PathComparer.Comparer.Equals(s.FilePath,filePath));
        return new(d.Code,ToSeverity(d.Severity),d.Message,filePath,range,"Martin.Build",source?.DocumentId,source?.Version,snapshot?.Generation);
    }

    static StudioDiagnostic ToStudioDiagnostic(ProjectDiagnostic d)=>new(d.Code,d.Severity==ProjectDiagnosticSeverity.Error?OutputSeverity.Error:OutputSeverity.Warning,d.Message,d.Path,d.Line is null||d.Column is null?null:new TextRange(d.Line.Value,d.Column.Value,d.Line.Value,d.Column.Value),"Martin.ProjectSystem");
    static string BuildOutputDirectory(MartinProject project,BuildConfiguration configuration)=>Path.Combine(project.RootDirectory,project.Manifest.Build.Output,configuration.ToString(),project.Manifest.Target.Framework);
    static string InformationalVersion(Assembly assembly)=>assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??assembly.GetName().Version?.ToString()??"unknown";
    static OutputSeverity ToSeverity(DiagnosticSeverity severity)=>severity switch{DiagnosticSeverity.Warning=>OutputSeverity.Warning,DiagnosticSeverity.Info=>OutputSeverity.Info,_=>OutputSeverity.Error};
    static BuildResult Failure(string code,string message)=>new(){Diagnostics=[Diag(code,message)]};
    static ProjectCleanResult CleanFailure(string code,string message){var plan=new ProjectCleanPlan{Diagnostics=[new ProjectDiagnostic(code,ProjectDiagnosticSeverity.Error,message)]};return new ProjectCleanResult{Plan=plan,Diagnostics=plan.Diagnostics};}
    static Diagnostic Diag(string c,string m)=>new(c,DiagnosticSeverity.Error,m,new TextLocation(SourceText.From(""),new TextSpan(0,0)));
}

public sealed class ExecutionCoordinator(StudioWorkspace workspace,OutputService output,IMartinExecutionService exec,IStudioLogService? logService=null):IExecutionCoordinator
{
    CancellationTokenSource? _cts;

    public Task<ExecutionResult> RunAsync(BuildResult build,IReadOnlyList<string>? args=null,CancellationToken ct=default) => RunAsync(build,args,false,false,ct);

    public async Task<ExecutionResult> RunAsync(BuildResult build,IReadOnlyList<string>? args,bool useExternalTerminal,bool requireCleanDocuments=false,CancellationToken ct=default)
    {
        if(workspace.ExecutionState is ExecutionState.Running or ExecutionState.ExternalLaunching or ExecutionState.Stopping)return Failure("MRT5101","A program is already running.");
        if(requireCleanDocuments && workspace.OpenDocuments.Any(d=>d.IsDirty))return Failure("MRT5102","Save dirty documents before running the project.");
        if(!build.Success || string.IsNullOrWhiteSpace(build.EntryPointPath))return Failure("MRT5104","Run requires a successful validated build result.");
        _cts=CancellationTokenSource.CreateLinkedTokenSource(ct);
        workspace.ExecutionState=useExternalTerminal?ExecutionState.ExternalLaunching:ExecutionState.Running;
        output.Add(OutputChannel.Program,OutputSeverity.Info,useExternalTerminal?"Starting program in external terminal.":"Starting program.");
        logService?.Log(StudioLogCategory.Execution,OutputSeverity.Info,useExternalTerminal?"RunExternal":"Run",useExternalTerminal?"External terminal launch started.":"Program run started.",$"project={workspace.Project?.ManifestPath};arguments={args?.Count??0}");
        try
        {
            var r=await exec.RunAsync(build,new ExecutionOptions{Arguments=args??[],UseExternalTerminal=useExternalTerminal,WorkingDirectory=workspace.Project?.RootDirectory},_cts.Token);
            AddResultOutput(r);
            output.Add(OutputChannel.Program,r.Completed?OutputSeverity.Info:r.WasCancelled?OutputSeverity.Warning:OutputSeverity.Error,r.Completed?$"Program exited with code {r.ExitCode ?? 0}.":r.WasCancelled?"Program stopped.":"Program failed to start or exited with errors.");
            logService?.Log(StudioLogCategory.Execution,r.Completed?OutputSeverity.Info:r.WasCancelled?OutputSeverity.Warning:OutputSeverity.Error,useExternalTerminal?"RunExternal":"Run",r.Completed?"Program exited.":r.WasCancelled?"Program stopped.":"Program failed.",$"status={r.Status};exitCode={r.ExitCode};diagnostics={r.Diagnostics.Length}");
            return r;
        }
        catch(OperationCanceledException){var r=Failure("MRT5103","Program stopped.") with{Status=ExecutionStatus.Cancelled}; output.Add(OutputChannel.Program,OutputSeverity.Warning,"Program stopped."); logService?.Log(StudioLogCategory.Execution,OutputSeverity.Warning,"Run","Program stopped."); return r;}
        finally{workspace.ExecutionState=ExecutionState.Idle;_cts=null;}
    }

    public ExecutionResult CreateFreshNoBuildResult(CancellationToken ct=default)
    {
        if(workspace.Project is null){var f=Failure("MRT5002","No project is open."); AddResultOutput(f); return f;}
        if(workspace.OpenDocuments.Any(d=>d.IsDirty)){var f=Failure("MRT5102","Save dirty documents before running the project."); AddResultOutput(f); return f;}
        var p=workspace.Project;
        var outputDirectory=BuildOutputDirectory(p,workspace.ActiveBuildConfiguration);
        var runtime=new RuntimeDiscovery().Discover(new BuildOptions{OutputDirectory=outputDirectory,AssemblyName=p.Manifest.Package.Name,TargetFramework=p.Manifest.Target.Framework,UseAppHost=false});
        if(!runtime.Success || runtime.Runtime is null){var f=new ExecutionResult{Status=ExecutionStatus.Failed,Diagnostics=runtime.Diagnostics}; AddResultOutput(f); return f;}
        var freshness=new BuildFreshnessChecker().Check(outputDirectory,new BuildFreshnessCheckInputs
        {
            ProjectRoot=p.RootDirectory,
            ManifestPath=p.ManifestPath,
            SourceFiles=p.SourceFiles,
            CompilerVersion=InformationalVersion(typeof(Compilation).Assembly),
            RuntimeVersion=MartinRuntimeInfo.RuntimeVersion,
            RuntimeSha256=runtime.Runtime.Sha256,
            Configuration=workspace.ActiveBuildConfiguration,
            TargetFramework=p.Manifest.Target.Framework,
            AssemblyName=p.Manifest.Package.Name
        },ct);
        if(!freshness.IsFresh){var f=Failure("MRT4805",$"Existing build output is stale: {freshness.Reason}."); AddResultOutput(f); return f;}
        output.Add(OutputChannel.Program,OutputSeverity.Info,"Existing build output is fresh.");
        return new(){Status=ExecutionStatus.Completed,ExitCode=0,Diagnostics=[],StandardOutput=freshness.EntryPointPath??string.Empty};
    }

    void AddResultOutput(ExecutionResult r){if(r.StandardOutput.Length>0)output.Add(OutputChannel.Program,OutputSeverity.Info,r.StandardOutput.TrimEnd()); if(r.StandardError.Length>0)output.Add(OutputChannel.Program,OutputSeverity.Error,r.StandardError.TrimEnd()); foreach(var d in r.Diagnostics)output.Add(OutputChannel.Program,ToSeverity(d.Severity),$"{d.Code} {d.Message}");}
    static ExecutionResult Failure(string code,string message)=>new(){Status=ExecutionStatus.Failed,Diagnostics=[new(code,DiagnosticSeverity.Error,message,new TextLocation(SourceText.From(""),new TextSpan(0,0)))]};
    static string BuildOutputDirectory(MartinProject project,BuildConfiguration configuration)=>Path.Combine(project.RootDirectory,project.Manifest.Build.Output,configuration.ToString(),project.Manifest.Target.Framework);
    static string InformationalVersion(Assembly assembly)=>assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??assembly.GetName().Version?.ToString()??"unknown";
    static OutputSeverity ToSeverity(DiagnosticSeverity severity)=>severity switch{DiagnosticSeverity.Warning=>OutputSeverity.Warning,DiagnosticSeverity.Info=>OutputSeverity.Info,_=>OutputSeverity.Error};
    public void Stop(){if(_cts!=null){workspace.ExecutionState=ExecutionState.Stopping;output.Add(OutputChannel.Program,OutputSeverity.Warning,"Stop requested.");logService?.Log(StudioLogCategory.Execution,OutputSeverity.Warning,"Stop","Stop requested.");_cts.Cancel();}}
}
static class SnapshotFactory
{
    public static async Task<CompilationSnapshot> CreateAsync(StudioWorkspace workspace,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var project=workspace.Project;
        if(project is null)
        {
            return new CompilationSnapshot(Compilation.Create([]),[],ImmutableDictionary<Guid,DocumentVersion>.Empty,workspace.Generation);
        }

        var openByPath=workspace.OpenDocuments
            .GroupBy(d=>CanonicalPath(d.FilePath),PathComparer.Comparer)
            .ToDictionary(g=>g.Key,g=>g.First(),PathComparer.Comparer);

        var diagnostics=ImmutableArray.CreateBuilder<StudioDiagnostic>();
        foreach(var duplicate in project.SourceFiles.Select(CanonicalPath).GroupBy(path=>path,PathComparer.Comparer).Where(group=>group.Count()>1).OrderBy(group=>RelativeSortKey(project.RootDirectory,group.Key),PathComparer.Comparer))
        {
            cancellationToken.ThrowIfCancellationRequested();
            diagnostics.Add(SourceReadDiagnostic("MRT5013",$"Duplicate source path after normalization: {duplicate.Key}",duplicate.Key));
        }

        var files=project.SourceFiles
            .Select(CanonicalPath)
            .Distinct(PathComparer.Comparer)
            .OrderBy(path=>RelativeSortKey(project.RootDirectory,path),PathComparer.Comparer)
            .ToArray();

        var sources=ImmutableArray.CreateBuilder<SnapshotSource>(files.Length);
        foreach(var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if(openByPath.TryGetValue(file,out var document))
            {
                sources.Add(new SnapshotSource(file,document.Text,document.Id,document.Version));
                continue;
            }

            try
            {
                if(!File.Exists(file))
                {
                    diagnostics.Add(SourceReadDiagnostic("MRT5011",$"Source file could not be found: {file}",file));
                    continue;
                }

                sources.Add(new SnapshotSource(file,await File.ReadAllTextAsync(file,cancellationToken),null,null));
            }
            catch(Exception ex) when(ex is not OperationCanceledException)
            {
                diagnostics.Add(SourceReadDiagnostic("MRT5014",$"Source file could not be read: {ex.Message}",file));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var immutableSources=sources.ToImmutable();
        return new CompilationSnapshot(
            Compilation.Create(immutableSources.Select(s=>SyntaxTree.Parse(s.Text,s.FilePath))),
            immutableSources,
            immutableSources.Where(s=>s.DocumentId.HasValue).ToImmutableDictionary(s=>s.DocumentId!.Value,s=>s.Version!.Value),
            workspace.Generation)
        { Diagnostics=diagnostics.Select(d=>d with{Generation=workspace.Generation}).ToImmutableArray() };
    }

    static string CanonicalPath(string path)=>Path.GetFullPath(path);
    static string RelativeSortKey(string rootDirectory,string filePath)=>Path.GetRelativePath(Path.GetFullPath(rootDirectory),filePath).Replace(Path.DirectorySeparatorChar,'/').Replace(Path.AltDirectorySeparatorChar,'/');
    static StudioDiagnostic SourceReadDiagnostic(string code,string message,string filePath)=>new(code,OutputSeverity.Error,message,filePath,null,"StudioSnapshot");
}
