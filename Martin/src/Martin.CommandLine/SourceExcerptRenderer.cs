namespace Martin.CommandLine;

public sealed class SourceExcerptRenderer
{
    const int TabWidth = 4;

    public void Render(CommandDiagnostic diagnostic, TextWriter writer)
    {
        if (diagnostic.Location?.FilePath is not { Length : > 0 } filePath || !File.Exists(filePath) || diagnostic.Location.StartLine <= 0)
            return;

        var lines = File.ReadLines(filePath)
                        .Skip(diagnostic.Location.StartLine - 1)
                        .Take(Math.Max(1, diagnostic.Location.EndLine - diagnostic.Location.StartLine + 1))
                        .ToArray();
        if (lines.Length == 0)
            return;

        var lineNumberWidth = Math.Max(diagnostic.Location.EndLine, diagnostic.Location.StartLine).ToString().Length;
        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = diagnostic.Location.StartLine + i;
            var expandedLine = ExpandTabs(lines[i]);
            writer.WriteLine($"{new string(' ', lineNumberWidth)} | ");
            writer.WriteLine($"{lineNumber.ToString().PadLeft(lineNumberWidth)} | {expandedLine}");
            writer.WriteLine($"{new string(' ', lineNumberWidth)} | {UnderlineForLine(diagnostic.Location, lines[i], lineNumber)}");
        }
    }

    static string UnderlineForLine(CommandTextLocation location, string originalLine, int lineNumber)
    {
        var sourceLength = ExpandTabs(originalLine).Length;
        var startColumn = lineNumber == location.StartLine ? Math.Max(location.StartColumn, 1) : 1;
        var endColumn = lineNumber == location.EndLine ? Math.Max(location.EndColumn, startColumn + 1) : sourceLength + 1;
        var startVisual = VisualColumn(originalLine, startColumn);
        var endVisual = Math.Max(VisualColumn(originalLine, endColumn), startVisual + 1);
        return new string(' ', Math.Max(startVisual - 1, 0)) + new string('^', Math.Max(endVisual - startVisual, 1));
    }

    static int VisualColumn(string line, int column)
    {
        var visual = 1;
        for (var i = 0; i < Math.Min(column - 1, line.Length); i++)
            visual += line[i] == '\t' ? TabWidth - ((visual - 1) % TabWidth) : 1;
        return visual;
    }

    static string ExpandTabs(string line)
    {
        var writer = new StringWriter();
        var visual = 1;
        foreach (var ch in line)
        {
            if (ch == '\t')
            {
                var spaces = TabWidth - ((visual - 1) % TabWidth);
                writer.Write(new string(' ', spaces));
                visual += spaces;
            }
            else
            {
                writer.Write(ch);
                visual++;
            }
        }
        return writer.ToString();
    }
}
