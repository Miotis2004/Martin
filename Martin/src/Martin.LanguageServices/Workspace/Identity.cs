namespace Martin.LanguageServices;

public readonly record struct WorkspaceId(Guid Value) { public static WorkspaceId CreateNew() => new(Guid.NewGuid()); }
public readonly record struct ProjectId(Guid Value) { public static ProjectId CreateNew() => new(Guid.NewGuid()); }
public readonly record struct DocumentId(Guid Value) { public static DocumentId CreateNew() => new(Guid.NewGuid()); }
public readonly record struct SymbolId(string Value);
