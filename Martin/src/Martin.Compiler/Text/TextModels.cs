using System.Collections.Immutable;

namespace Martin.Compiler.Text;

public readonly record struct TextSpan(int Start, int Length)
{
    public int End => Start + Length;
    public static TextSpan FromBounds(int start, int end) => new(start, end - start);
    public override string ToString() => $"{Start}..{End}";
}
public readonly record struct LinePosition(int Line, int Character);
public readonly record struct LinePositionSpan(LinePosition Start, LinePosition End);

public sealed class TextLine
{
    public TextLine(SourceText text, int start, int length, int lengthIncludingLineBreak) { Text = text; Start = start; Length = length; LengthIncludingLineBreak = lengthIncludingLineBreak; }
    public SourceText Text { get; }
    public int Start { get; }
    public int Length { get; }
    public int LengthIncludingLineBreak { get; }
    public int End => Start + Length; public int EndIncludingLineBreak => Start + LengthIncludingLineBreak;
    public TextSpan Span => new(Start, Length); public TextSpan SpanIncludingLineBreak => new(Start, LengthIncludingLineBreak);
}

public sealed class SourceText
{
    private readonly string _text; private SourceText(string text, string? filePath) { _text = text; FilePath = filePath; Lines = ParseLines(this, text).ToImmutableArray(); }
    public char this[int index] => _text[index]; public int Length => _text.Length; public string? FilePath { get; }
    public IReadOnlyList<TextLine> Lines { get; }
    public static SourceText From(string text, string? filePath = null) => new(text ?? string.Empty, filePath);
    public override string ToString() => _text; public string ToString(TextSpan span) => _text.Substring(span.Start, span.Length);
    public int GetLineIndex(int position) { var lower = 0; var upper = Lines.Count - 1; while (lower <= upper) { var index = lower + (upper - lower) / 2; var start = Lines[index].Start; if (position == start) return index; if (start > position) upper = index - 1; else lower = index + 1; } return Math.Max(0, lower - 1); }
    public LinePosition GetLinePosition(int position) { var clamped = Math.Clamp(position, 0, Length); var line = GetLineIndex(clamped); return new(line + 1, Math.Max(0, clamped - Lines[line].Start)); }
    public LinePositionSpan GetLinePositionSpan(TextSpan span) => new(GetLinePosition(span.Start), GetLinePosition(span.End));
    private static List<TextLine> ParseLines(SourceText source, string text) { var result = new List<TextLine>(); var position = 0; var lineStart = 0; while (position < text.Length) { var lb = GetLineBreakWidth(text, position); if (lb == 0) { position++; continue; } AddLine(result, source, lineStart, position, lb); position += lb; lineStart = position; } if (position >= lineStart) AddLine(result, source, lineStart, position, 0); return result; }
    private static void AddLine(List<TextLine> lines, SourceText source, int start, int position, int lb) => lines.Add(new(source, start, position - start, position - start + lb));
    private static int GetLineBreakWidth(string text, int i) { var c = text[i]; var l = i + 1 >= text.Length ? '\0' : text[i + 1]; if (c == '\r' && l == '\n') return 2; if (c == '\r' || c == '\n') return 1; return 0; }
}
public readonly record struct TextLocation(SourceText Text, TextSpan Span) { public string? FilePath => Text.FilePath; public LinePosition StartLinePosition => Text.GetLinePosition(Span.Start); public LinePosition EndLinePosition => Text.GetLinePosition(Span.End); public LinePositionSpan LineSpan => Text.GetLinePositionSpan(Span); }
