using System.Collections.Immutable;
using System.Text;
using Martin.Compiler.Syntax;
using Martin.Compiler.Text;

namespace Martin.LanguageServices;

/// <summary>Formats only trivia between compiler tokens; token and comment text is never rewritten.</summary>
internal static class SafeFormattingService
{
    private readonly record struct Item(SyntaxKind Kind, string Text, int Start, bool IsTrivia = false, bool BreakBefore = false,
        bool BreakAfter = false, bool IsSwitchCaseStart = false, bool ClosesSwitch = false, bool IsGenericDelimiter = false);

    public static FormattingResult Format(string source, FormattingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= new();
        if (options.IndentSize is < 1 or > 16)
            throw new ArgumentOutOfRangeException(nameof(options), "IndentSize must be between 1 and 16.");

        var tree = SyntaxTree.Parse(source);
        // Recovery can omit or invent syntax. Refusing it is safer than reconstructing a
        // document which no longer contains every byte supplied by the user.
        if (tree.Diagnostics.Length != 0)
            return new() { WasRefused = true, DiagnosticCode = "MRT6451", Message = "Formatter refused unsafe malformed source." };

        var lineStarts = RequiredLineStarts(tree.Root);
        var items = Tokens(tree.Root)
            .Where(t => t.Kind != SyntaxKind.EndOfFileToken && !t.IsMissing)
            .SelectMany(t => t.LeadingTrivia.Concat(t.TrailingTrivia)
                .Where(IsPreservedTrivia).Select(x => new Item(x.Kind, x.Text, x.Span.Start, true))
                .Append(new Item(t.Kind, t.Text, t.Span.Start, BreakBefore: lineStarts.Contains(t.Span.Start),
                    BreakAfter: t.Kind == SyntaxKind.ColonToken && t.Parent?.Kind == SyntaxKind.SwitchCase,
                    IsSwitchCaseStart: t.Parent?.Kind == SyntaxKind.SwitchCase && t.Kind is SyntaxKind.CaseKeyword or SyntaxKind.DefaultKeyword,
                    ClosesSwitch: t.Kind == SyntaxKind.CloseBraceToken && t.Parent?.Kind == SyntaxKind.SwitchStatement,
                    IsGenericDelimiter: t.Kind is SyntaxKind.LessToken or SyntaxKind.GreaterToken &&
                        t.Parent?.Kind is SyntaxKind.TypeParameterList or SyntaxKind.TypeArgumentList)))
            .DistinctBy(x => (x.Start, x.Text, x.Kind)).OrderBy(x => x.Start).ToArray();

        var newline = options.PreserveLineEndings ? DetectNewline(source) : Environment.NewLine;
        var formatted = Render(items, options, newline);
        var edits = MinimalEdit(source, formatted);
        TextEditApplicator.Validate(source, edits);
        if (TextEditApplicator.Apply(source, edits) != formatted)
            throw new InvalidOperationException("The formatter produced edits that do not recreate its output.");
        return new() { Edits = edits };
    }

    private static string Render(Item[] items, FormattingOptions options, string nl)
    {
        var b = new StringBuilder(); var indent = 0; var activeCaseBodies = 0; var lineStart = true; Item? previous = null;
        void NewLine() { while (b.Length > 0 && (b[^1] == ' ' || b[^1] == '\t')) b.Length--; if (b.Length == 0 || !EndsWith(b, nl)) b.Append(nl); lineStart = true; previous = null; }
        void Indent() { if (!lineStart) return; b.Append(options.UseTabs ? new string('\t', indent) : new string(' ', indent * options.IndentSize)); lineStart = false; }
        foreach (var item in items)
        {
            if ((item.IsSwitchCaseStart || item.ClosesSwitch) && activeCaseBodies > 0) { indent--; activeCaseBodies--; }
            if (item.BreakBefore && !lineStart) NewLine();
            if (item.IsTrivia)
            {
                if (!lineStart)
                {
                    if (b.Length > 0 && b[^1] is not (' ' or '\t' or '\r' or '\n')) b.Append(' ');
                }
                else Indent();
                b.Append(item.Text);
                if (item.Kind is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.DocumentationCommentTrivia || item.Text.Contains('\n') || item.Text.Contains('\r')) NewLine();
                else if (item.Kind == SyntaxKind.MultiLineCommentTrivia && b.Length > 0 && b[^1] is not (' ' or '\t')) b.Append(' ');
                previous = null; continue;
            }
            if (item.Kind == SyntaxKind.CloseBraceToken) { indent = Math.Max(0, indent - 1); if (!lineStart) NewLine(); }
            Indent();
            if (NeedsSpace(previous, item)) b.Append(' ');
            b.Append(item.Text);
            if (item.Kind == SyntaxKind.OpenBraceToken) { indent++; NewLine(); }
            else if (item.Kind is SyntaxKind.CloseBraceToken or SyntaxKind.SemicolonToken) NewLine();
            else if (item.Kind == SyntaxKind.CommaToken) b.Append(' ');
            if (item.BreakAfter) { indent++; activeCaseBodies++; NewLine(); }
            previous = item;
        }
        while (b.Length > 0 && char.IsWhiteSpace(b[^1])) b.Length--;
        if (options.InsertFinalNewLine && b.Length > 0) b.Append(nl);
        return b.ToString();
    }

    private static bool NeedsSpace(Item? previous, Item current)
    {
        if (previous is null) return false;
        var left = previous.Value.Kind;
        var right = current.Kind;
        if (current.IsGenericDelimiter || previous.Value.IsGenericDelimiter && left == SyntaxKind.LessToken) return false;
        if (previous.Value.IsGenericDelimiter && left == SyntaxKind.GreaterToken &&
            right is SyntaxKind.OpenParenthesisToken or SyntaxKind.DotToken or SyntaxKind.QuestionToken or SyntaxKind.CommaToken or SyntaxKind.GreaterToken)
            return false;
        if (right == SyntaxKind.DotToken && left is SyntaxKind.CaseKeyword or SyntaxKind.CatchKeyword) return true;
        if (right is SyntaxKind.CommaToken or SyntaxKind.ColonToken or SyntaxKind.DotToken or SyntaxKind.SemicolonToken or SyntaxKind.CloseParenthesisToken or SyntaxKind.CloseBracketToken or SyntaxKind.QuestionToken) return false;
        if (left is SyntaxKind.OpenParenthesisToken or SyntaxKind.OpenBracketToken or SyntaxKind.DotToken) return false;
        if (right == SyntaxKind.OpenParenthesisToken && left is SyntaxKind.IdentifierToken or SyntaxKind.SelfKeyword) return false;
        if (left == SyntaxKind.ColonToken) return true;
        if (right == SyntaxKind.OpenBraceToken) return true;
        return IsWord(left) && IsWord(right) || IsOperator(left) || IsOperator(right);
    }

    private static bool IsWord(SyntaxKind k) => k is SyntaxKind.IdentifierToken or SyntaxKind.IntegerLiteralToken or SyntaxKind.FloatingPointLiteralToken or SyntaxKind.StringLiteralToken || SyntaxFacts.IsKeyword(k);
    private static bool IsOperator(SyntaxKind k) => SyntaxFacts.GetBinaryOperatorPrecedence(k) > 0 || k is SyntaxKind.EqualToken or SyntaxKind.ArrowToken;
    private static bool IsPreservedTrivia(SyntaxTrivia t) => t.Kind is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia or SyntaxKind.DocumentationCommentTrivia or SyntaxKind.SkippedTextTrivia;
    private static HashSet<int> RequiredLineStarts(SyntaxNode root)
    {
        var result = new HashSet<int>();
        Visit(root);
        return result;

        void Visit(SyntaxNode node)
        {
            if (node.Kind is SyntaxKind.EnumCaseDeclaration or SyntaxKind.ProtocolMethodRequirement or
                SyntaxKind.ProtocolPropertyRequirement or SyntaxKind.PropertyDeclaration or SyntaxKind.MethodDeclaration or
                SyntaxKind.InitializerDeclaration or SyntaxKind.SwitchCase ||
                node is StatementSyntax && node.Parent?.Kind is SyntaxKind.BlockStatement or SyntaxKind.SwitchCase)
            {
                var first = Tokens(node).FirstOrDefault(t => !t.IsMissing);
                if (first is not null) result.Add(first.Span.Start);
            }
            foreach (var child in node.GetChildren())
                if (child is not SyntaxToken) Visit(child);
        }
    }
    private static IEnumerable<SyntaxToken> Tokens(SyntaxNode node) { if (node is SyntaxToken token) { yield return token; yield break; } foreach (var child in node.GetChildren()) foreach (var descendantToken in Tokens(child)) yield return descendantToken; }
    private static string DetectNewline(string text) { var i = text.IndexOfAny(['\r', '\n']); return i < 0 ? Environment.NewLine : text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? "\r\n" : text[i].ToString(); }
    private static bool EndsWith(StringBuilder b, string value) { if (b.Length < value.Length) return false; for (var i = 0; i < value.Length; i++) if (b[b.Length - value.Length + i] != value[i]) return false; return true; }
    private static ImmutableArray<TextEdit> MinimalEdit(string oldText, string newText)
    {
        if (oldText == newText) return [];
        var start = 0; while (start < oldText.Length && start < newText.Length && oldText[start] == newText[start]) start++;
        var oldEnd = oldText.Length; var newEnd = newText.Length;
        while (oldEnd > start && newEnd > start && oldText[oldEnd - 1] == newText[newEnd - 1]) { oldEnd--; newEnd--; }
        return [new(new TextSpan(start, oldEnd - start), newText[start..newEnd])];
    }
}

public static class TextEditApplicator
{
    public static void Validate(string source, ImmutableArray<TextEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(source); if (edits.IsDefault) throw new ArgumentException("Edits must be initialized.", nameof(edits));
        var end = 0; foreach (var edit in edits) { if (edit.NewText is null || edit.Span.Start < end || edit.Span.Start < 0 || edit.Span.Length < 0 || edit.Span.End > source.Length) throw new ArgumentException("Edits must be sorted, non-overlapping, and within the source.", nameof(edits)); end = edit.Span.End; }
    }
    public static string Apply(string source, ImmutableArray<TextEdit> edits)
    {
        Validate(source, edits); var b = new StringBuilder(source); for (var i = edits.Length - 1; i >= 0; i--) { var e = edits[i]; b.Remove(e.Span.Start, e.Span.Length).Insert(e.Span.Start, e.NewText); }
        return b.ToString();
    }
}
