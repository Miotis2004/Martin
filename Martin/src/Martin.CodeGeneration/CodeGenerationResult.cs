using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Martin.Compiler.Binding;
using Martin.Compiler.Diagnostics;
using Martin.Compiler.Symbols;
using Martin.Compiler.Text;

namespace Martin.CodeGeneration;

public sealed record CodeGenerationRequest
{
    public required LoweredProgram Program { get; init; }
    public required string AssemblyName { get; init; }
    public required OutputKind OutputKind { get; init; }
    public bool IncludeSourceDirectives { get; init; } = true;
}
public sealed record GeneratedSourceMapEntry(TextSpan GeneratedSpan, TextLocation MartinLocation);
public sealed record CodeGenerationResult
{
    public bool Success { get; init; }
    public string GeneratedSource { get; init; } = string.Empty;
    public ImmutableArray<Diagnostic> Diagnostics { get; init; } = [];
    public ImmutableArray<GeneratedSourceMapEntry> SourceMap { get; init; } = [];
}
public sealed class CodeGenerationException(string message, BoundNode? node = null, Exception? innerException = null) : Exception
(message, innerException)
{
    public BoundNode ? Node { get; } = node;
}
