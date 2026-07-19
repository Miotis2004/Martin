using Martin.Compiler.Text;
using Martin.Compiler.Diagnostics;
namespace Martin.Compiler.Syntax;

internal sealed class Lexer
{
    readonly SourceText _text;
    readonly CancellationToken _cancellationToken;
    int _position;
    public DiagnosticBag Diagnostics { get; } = [];
    public Lexer(SourceText text, CancellationToken cancellationToken = default)
    {
        _text = text;
        _cancellationToken = cancellationToken;
    }
    char Current => Peek(0);
    char Lookahead => Peek(1);
    char Peek(int o)
    {
        var i = _position + o;
        return i >= _text.Length ? '\0' : _text[i];
    }
    char Next()
    {
        _cancellationToken.ThrowIfCancellationRequested();
        var c = Current;
        _position++;
        return c;
    }
    public SyntaxToken Lex()
    {
        _cancellationToken.ThrowIfCancellationRequested();
        var leading = ReadTrivia(true);
        var start = _position;
        SyntaxKind kind;
        object? value = null;
        if (Current == '\0')
            kind = SyntaxKind.EndOfFileToken;
        else if (char.IsLetter(Current) || Current == '_')
        {
            while (char.IsLetterOrDigit(Current) || Current == '_')
                Next();
            var text = _text.ToString(new(start, _position - start));
            kind = SyntaxFacts.GetKeywordKind(text);
            value = kind switch { SyntaxKind.TrueKeyword => true, SyntaxKind.FalseKeyword => false, SyntaxKind.NilKeyword => null,
                                  _ => null };
        }
        else if (char.IsDigit(Current))
        {
            while (char.IsDigit(Current))
                Next();
            var isFloat = false;
            if (Current == '.' && char.IsDigit(Lookahead))
            {
                isFloat = true;
                Next();
                while (char.IsDigit(Current))
                    Next();
            }
            var s = _text.ToString(new(start, _position - start));
            kind = isFloat ? SyntaxKind.FloatingPointLiteralToken : SyntaxKind.IntegerLiteralToken;
            try
            {
                value = isFloat ? double.Parse(s, System.Globalization.CultureInfo.InvariantCulture) : int.Parse(s);
            }
            catch
            {
                Diagnostics.ReportInvalidNumber(new(_text, new(start, _position - start)));
            }
        }
        else if (Current == '"')
        {
            Next();
            var val = "";
            var done = false;
            while (Current != '\0' && Current != '\r' && Current != '\n')
            {
                if (Current == '"')
                {
                    done = true;
                    Next();
                    break;
                }
                if (Current == '\\')
                {
                    var ep = _position;
                    Next();
                    val += Current switch { 'n' => '\n', 'r' => '\r', 't' => '\t', '0' => '\0', '\\' => '\\', '"' => '"',
                                            _ => Current };
                    if ("nrt0\\\"".IndexOf(Current) < 0)
                        Diagnostics.ReportInvalidEscape(new(_text, new(ep, 2)), Current);
                    Next();
                }
                else
                    val += Next();
            }
            if (!done)
                Diagnostics.ReportUnterminatedString(new(_text, new(start, _position - start)));
            kind = SyntaxKind.StringLiteralToken;
            value = val;
        }
        else
        {
            kind = (Current, Lookahead) switch
            {
                ('&', '&') => Adv(SyntaxKind.AmpersandAmpersandToken, 2), ('|', '|') => Adv(SyntaxKind.PipePipeToken, 2), ('=', '=') => Adv(SyntaxKind.EqualEqualToken, 2), ('!', '=') => Adv(SyntaxKind.BangEqualToken, 2), ('<', '=') => Adv(SyntaxKind.LessOrEqualToken, 2), ('>', '=') => Adv(SyntaxKind.GreaterOrEqualToken, 2), ('-', '>') => Adv(SyntaxKind.ArrowToken, 2),
                      _ => Current switch
                {
                    '+' => Adv(SyntaxKind.PlusToken), '-' => Adv(SyntaxKind.MinusToken), '*' => Adv(SyntaxKind.StarToken), '/' => Adv(SyntaxKind.SlashToken), '%' => Adv(SyntaxKind.PercentToken), '!' => Adv(SyntaxKind.BangToken), '=' => Adv(SyntaxKind.EqualToken), '<' => Adv(SyntaxKind.LessToken), '>' => Adv(SyntaxKind.GreaterToken), '(' => Adv(SyntaxKind.OpenParenthesisToken), ')' => Adv(SyntaxKind.CloseParenthesisToken), '{' => Adv(SyntaxKind.OpenBraceToken), '}' => Adv(SyntaxKind.CloseBraceToken), '[' => Adv(SyntaxKind.OpenBracketToken), ']' => Adv(SyntaxKind.CloseBracketToken), ':' => Adv(SyntaxKind.ColonToken), ',' => Adv(SyntaxKind.CommaToken), '.' => Adv(SyntaxKind.DotToken), ';' => Adv(SyntaxKind.SemicolonToken), '?' => Adv(SyntaxKind.QuestionToken),
                    _ => Bad()
                }
            };
        }
        var tokenText = _text.ToString(new(start, _position - start));
        var trailing = ReadTrivia(false);
        return new(kind, start, tokenText, value, false, leading, trailing);
        SyntaxKind Adv(SyntaxKind k, int n = 1)
        {
            _position += n;
            return k;
        }
        SyntaxKind Bad()
        {
            Diagnostics.ReportInvalidCharacter(new(_text, new(_position, 1)), Current);
            Next();
            return SyntaxKind.BadToken;
        }
    }
    IReadOnlyList<SyntaxTrivia> ReadTrivia(bool leading)
    {
        var list = new List<SyntaxTrivia>();
        while (true)
        {
            var start = _position;
            if (Current == ' ' || Current == '\t')
            {
                while (Current == ' ' || Current == '\t')
                    Next();
                list.Add(new(SyntaxKind.WhitespaceTrivia, start, _text.ToString(new(start, _position - start))));
            }
            else if (Current == '\r' || Current == '\n')
            {
                if (Current == '\r' && Lookahead == '\n')
                    _position += 2;
                else
                    _position++;
                list.Add(new(SyntaxKind.LineBreakTrivia, start, _text.ToString(new(start, _position - start))));
                if (!leading)
                    break;
            }
            else if (Current == '/' && Lookahead == '/')
            {
                while (Current != '\0' && Current != '\r' && Current != '\n')
                    Next();
                var txt = _text.ToString(new(start, _position - start));
                list.Add(new(txt.StartsWith("///") ? SyntaxKind.DocumentationCommentTrivia : SyntaxKind.SingleLineCommentTrivia, start, txt));
            }
            else if (Current == '/' && Lookahead == '*')
            {
                _position += 2;
                while (Current != '\0' && !(Current == '*' && Lookahead == '/'))
                    Next();
                if (Current == '\0')
                    Diagnostics.ReportUnterminatedComment(new(_text, new(start, _position - start)));
                else
                    _position += 2;
                list.Add(new(SyntaxKind.MultiLineCommentTrivia, start, _text.ToString(new(start, _position - start))));
            }
            else
                break;
        }
        return list;
    }
}
