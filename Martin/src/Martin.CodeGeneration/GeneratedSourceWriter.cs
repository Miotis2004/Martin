using System.Collections.Immutable;
using System.Text;
using Martin.Compiler.Text;

namespace Martin.CodeGeneration;

internal sealed class GeneratedSourceWriter
{
    readonly StringBuilder _b = new();
    readonly ImmutableArray<GeneratedSourceMapEntry>.Builder _sourceMap = ImmutableArray.CreateBuilder<GeneratedSourceMapEntry>();
    int _indent;
    bool _lineStart = true;

    public int Position { get; private set; }
    public int Line { get; private set; } = 1;
    public int Column { get; private set; }
    public ImmutableArray<GeneratedSourceMapEntry> SourceMap => _sourceMap.ToImmutable();

    public void Write(string text)
    {
        foreach (var ch in text.Replace("\r\n", "\n").Replace('\r', '\n'))
        {
            if (_lineStart && ch != '\n')
            {
                var indent = new string(' ', _indent * 4);
                _b.Append(indent);
                Position += indent.Length;
                Column += indent.Length;
                _lineStart = false;
            }

            _b.Append(ch);
            Position++;
            if (ch == '\n')
            {
                Line++;
                Column = 0;
                _lineStart = true;
            }
            else
            {
                Column++;
            }
        }
    }

    public void WriteLine() => Write("\n");
    public void WriteLine(string text) { Write(text); WriteLine(); }
    public IDisposable Indent() { _indent++; return new IndentScope(this); }
    public IDisposable MapTo(TextLocation? location) => new MapScope(this, location);
    public override string ToString() => _b.ToString().TrimEnd() + "\n";

    sealed class IndentScope(GeneratedSourceWriter writer) : IDisposable { public void Dispose() => writer._indent--; }

    sealed class MapScope : IDisposable
    {
        readonly GeneratedSourceWriter _writer;
        readonly TextLocation? _location;
        readonly int _start;

        public MapScope(GeneratedSourceWriter writer, TextLocation? location)
        {
            _writer = writer;
            _location = location;
            _start = writer.Position;
        }

        public void Dispose()
        {
            if (_location is not { } location)
                return;

            var length = _writer.Position - _start;
            if (length > 0)
                _writer._sourceMap.Add(new GeneratedSourceMapEntry(new TextSpan(_start, length), location));
        }
    }
}
