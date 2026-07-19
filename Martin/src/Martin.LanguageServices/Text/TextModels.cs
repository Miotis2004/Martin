using System.Collections.Immutable;
using Martin.Compiler.Text;

namespace Martin.LanguageServices;

public sealed record TextChange(TextSpan Span, string NewText);
public sealed record TextEdit(TextSpan Span, string NewText);
public readonly record struct MartinPosition(int Line, int Character);
public readonly record struct MartinRange(MartinPosition Start, MartinPosition End);

/// <summary>An incremental edit expressed against one immutable document version.</summary>
public sealed record DocumentChange
{
    public required DocumentId DocumentId { get; init; }
    public required DocumentVersion PreviousVersion { get; init; }
    public required DocumentVersion NewVersion { get; init; }
    public ImmutableArray<TextChange> Changes { get; init; } = [];
}

public sealed record TextSynchronizationOptions
{
    public const int DefaultMaximumDocumentSizeBytes = 10 * 1024 * 1024;
    public const int DefaultMaximumChangePayloadBytes = 2 * 1024 * 1024;
    public const int DefaultMaximumChanges = 10_000;

    public int MaximumDocumentSizeBytes { get; init; } = DefaultMaximumDocumentSizeBytes;
    public int MaximumChangePayloadBytes { get; init; } = DefaultMaximumChangePayloadBytes;
    public int MaximumChanges { get; init; } = DefaultMaximumChanges;
}

public enum DocumentChangeStatus { Applied, RequiresFullTextResynchronization, Rejected }

/// <summary>A non-throwing, log-safe description of an editor synchronization attempt.</summary>
public sealed record DocumentChangeResult(DocumentChangeStatus Status, string? DiagnosticCode = null, string? Message = null)
{
    public bool IsApplied => Status == DocumentChangeStatus.Applied;
    public bool RequiresFullTextResynchronization => Status == DocumentChangeStatus.RequiresFullTextResynchronization;
    public static DocumentChangeResult Applied { get; } = new(DocumentChangeStatus.Applied);
}

public interface IPositionConverter
{
    int ToOffset(SourceText text, MartinPosition position);
    MartinPosition ToPosition(SourceText text, int offset);
    TextSpan ToSpan(SourceText text, MartinRange range);
    MartinRange ToRange(SourceText text, TextSpan span);
}

/// <summary>Central UTF-16, zero-based position conversion for language-service code.</summary>
public sealed class PositionConverter : IPositionConverter
{
    public static PositionConverter Instance { get; } = new();

    public int ToOffset(SourceText text, MartinPosition position)
    {
        ArgumentNullException.ThrowIfNull(text);
        if ((uint)position.Line >= (uint)text.Lines.Count)
            throw new ArgumentOutOfRangeException(nameof(position), "The line is outside the document.");
        var line = text.Lines[position.Line];
        if (position.Character < 0 || position.Character > line.Length)
            throw new ArgumentOutOfRangeException(nameof(position), "The UTF-16 character is outside the line.");
        return line.Start + position.Character;
    }

    public MartinPosition ToPosition(SourceText text, int offset)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (offset < 0 || offset > text.Length)
            throw new ArgumentOutOfRangeException(nameof(offset), "The offset is outside the document.");
        var lineIndex = text.GetLineIndex(offset);
        var line = text.Lines[lineIndex];
        // An offset in a line-break has no distinct LSP representation. Canonicalize it
        // to the end of that line rather than exposing CR/LF as editor characters.
        return new(lineIndex, Math.Min(offset - line.Start, line.Length));
    }

    public TextSpan ToSpan(SourceText text, MartinRange range)
    {
        var start = ToOffset(text, range.Start);
        var end = ToOffset(text, range.End);
        if (end < start) throw new ArgumentException("Range end must not precede its start.", nameof(range));
        return TextSpan.FromBounds(start, end);
    }

    public MartinRange ToRange(SourceText text, TextSpan span)
    {
        ValidateSpan(text, span, nameof(span));
        return new(ToPosition(text, span.Start), ToPosition(text, span.End));
    }

    internal static void ValidateSpan(SourceText text, TextSpan span, string parameterName)
    {
        if (span.Start < 0 || span.Length < 0 || span.Start > text.Length || span.Length > text.Length - span.Start)
            throw new ArgumentOutOfRangeException(parameterName, "The span is outside the document.");
    }
}

internal static class TextChangeValidator
{
    internal static DocumentChangeResult ValidateAndApply(string oldText, DocumentChange change, TextSynchronizationOptions options, out string newText)
    {
        newText = oldText;
        if (change.NewVersion.Value <= change.PreviousVersion.Value)
            return Rejected("MRTLS1002", "The new document version must be greater than the previous version.");
        if (change.Changes.IsDefault)
            return Rejected("MRTLS1003", "The change payload is malformed.");
        if (change.Changes.Length > options.MaximumChanges)
            return Rejected("MRTLS1004", "The change payload contains too many edits.");

        long payloadBytes = 0;
        var previousEnd = -1;
        foreach (var edit in change.Changes)
        {
            if (edit is null || edit.NewText is null)
                return Rejected("MRTLS1003", "The change payload is malformed.");
            if (edit.Span.Start < 0 || edit.Span.Length < 0 || edit.Span.Start > oldText.Length || edit.Span.Length > oldText.Length - edit.Span.Start)
                return Rejected("MRTLS1005", "A change span is outside the previous document text.");
            if (previousEnd > edit.Span.Start)
                return Rejected("MRTLS1006", "Changes must be ordered and must not overlap.");
            previousEnd = edit.Span.End;
            payloadBytes += System.Text.Encoding.UTF8.GetByteCount(edit.NewText);
            if (payloadBytes > options.MaximumChangePayloadBytes)
                return Rejected("MRTLS1007", "The change payload exceeds the configured size limit.");
        }

        var result = oldText;
        for (var i = change.Changes.Length - 1; i >= 0; i--)
        {
            var edit = change.Changes[i];
            result = result.Remove(edit.Span.Start, edit.Span.Length).Insert(edit.Span.Start, edit.NewText);
        }
        if (System.Text.Encoding.UTF8.GetByteCount(result) > options.MaximumDocumentSizeBytes)
            return Rejected("MRTLS1008", "The resulting document exceeds the configured size limit.");
        newText = result;
        return DocumentChangeResult.Applied;
    }

    static DocumentChangeResult Rejected(string code, string message) => new(DocumentChangeStatus.Rejected, code, message);
}
