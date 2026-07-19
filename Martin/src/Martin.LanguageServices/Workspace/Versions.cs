namespace Martin.LanguageServices;

public readonly record struct WorkspaceVersion(long Value) { public WorkspaceVersion Next() => new(Value + 1); }
public readonly record struct ProjectVersion(long Value) { public ProjectVersion Next() => new(Value + 1); }
public readonly record struct DocumentVersion(long Value) { public DocumentVersion Next() => new(Value + 1); }
