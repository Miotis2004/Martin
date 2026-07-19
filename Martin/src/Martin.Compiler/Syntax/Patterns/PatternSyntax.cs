namespace Martin.Compiler.Syntax;

/// <summary>A semantic-free source representation of a Martin pattern.</summary>
public abstract class PatternSyntax : SyntaxNode
{
    protected void AttachPatternChildren(params SyntaxNode[] children) => AttachChildren(children);

    public abstract override void Accept(SyntaxVisitor visitor);
    public abstract override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor);
}

public sealed class WildcardPatternSyntax : PatternSyntax
{
    public WildcardPatternSyntax(SyntaxToken underscoreToken) { UnderscoreToken = underscoreToken; AttachPatternChildren(underscoreToken); }
    public SyntaxToken UnderscoreToken { get; }
    public override SyntaxKind Kind => SyntaxKind.WildcardPattern;
    public override IEnumerable<SyntaxNode> GetChildren() { yield return UnderscoreToken; }
    public override void Accept(SyntaxVisitor visitor) => visitor.VisitWildcardPattern(this);
    public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor) => visitor.VisitWildcardPattern(this);
}

public sealed class IdentifierPatternSyntax : PatternSyntax
{
    public IdentifierPatternSyntax(SyntaxToken identifier) { Identifier = identifier; AttachPatternChildren(identifier); }
    public SyntaxToken Identifier { get; }
    public override SyntaxKind Kind => SyntaxKind.IdentifierPattern;
    public override IEnumerable<SyntaxNode> GetChildren() { yield return Identifier; }
    public override void Accept(SyntaxVisitor visitor) => visitor.VisitIdentifierPattern(this);
    public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor) => visitor.VisitIdentifierPattern(this);
}

public sealed class LiteralPatternSyntax : PatternSyntax
{
    public LiteralPatternSyntax(SyntaxToken literalToken) { LiteralToken = literalToken; AttachPatternChildren(literalToken); }
    public SyntaxToken LiteralToken { get; }
    public override SyntaxKind Kind => SyntaxKind.LiteralPattern;
    public override IEnumerable<SyntaxNode> GetChildren() { yield return LiteralToken; }
    public override void Accept(SyntaxVisitor visitor) => visitor.VisitLiteralPattern(this);
    public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor) => visitor.VisitLiteralPattern(this);
}

public sealed class PatternArgumentListSyntax : SyntaxNode
{
    private readonly IReadOnlyList<SyntaxNode> _children;
    public PatternArgumentListSyntax(SyntaxToken openParenToken, IReadOnlyList<PatternSyntax> arguments, IReadOnlyList<SyntaxToken> commaTokens, SyntaxToken closeParenToken)
    {
        OpenParenToken = openParenToken; Arguments = arguments; CommaTokens = commaTokens; CloseParenToken = closeParenToken;
        var children = new List<SyntaxNode> { openParenToken };
        for (var i = 0; i < arguments.Count; i++) { children.Add(arguments[i]); if (i < commaTokens.Count) children.Add(commaTokens[i]); }
        children.Add(closeParenToken); _children = children; AttachChildren(children);
    }
    public SyntaxToken OpenParenToken { get; }
    public IReadOnlyList<PatternSyntax> Arguments { get; }
    public IReadOnlyList<SyntaxToken> CommaTokens { get; }
    public SyntaxToken CloseParenToken { get; }
    public override SyntaxKind Kind => SyntaxKind.PatternArgumentList;
    public override IEnumerable<SyntaxNode> GetChildren() => _children;
    public override void Accept(SyntaxVisitor visitor) => visitor.VisitPatternArgumentList(this);
    public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor) => visitor.VisitPatternArgumentList(this);
}

public sealed class EnumCasePatternSyntax : PatternSyntax
{
    public EnumCasePatternSyntax(SyntaxToken? qualifier, SyntaxToken? dotToken, SyntaxToken caseName, PatternArgumentListSyntax? arguments)
    {
        Qualifier = qualifier; DotToken = dotToken; CaseName = caseName; Arguments = arguments; AttachPatternChildren(GetChildren().ToArray());
    }
    public SyntaxToken? Qualifier { get; }
    public SyntaxToken? DotToken { get; }
    public SyntaxToken CaseName { get; }
    public PatternArgumentListSyntax? Arguments { get; }
    public override SyntaxKind Kind => SyntaxKind.EnumCasePattern;
    public override IEnumerable<SyntaxNode> GetChildren() { if (Qualifier is not null) yield return Qualifier; if (DotToken is not null) yield return DotToken; yield return CaseName; if (Arguments is not null) yield return Arguments; }
    public override void Accept(SyntaxVisitor visitor) => visitor.VisitEnumCasePattern(this);
    public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor) => visitor.VisitEnumCasePattern(this);
}

public sealed class OptionalSomePatternSyntax : PatternSyntax
{
    public OptionalSomePatternSyntax(SyntaxToken dotToken, SyntaxToken someIdentifier, SyntaxToken openParenToken, PatternSyntax valuePattern, SyntaxToken closeParenToken)
    { DotToken = dotToken; SomeIdentifier = someIdentifier; OpenParenToken = openParenToken; ValuePattern = valuePattern; CloseParenToken = closeParenToken; AttachPatternChildren(dotToken, someIdentifier, openParenToken, valuePattern, closeParenToken); }
    public SyntaxToken DotToken { get; }
    public SyntaxToken SomeIdentifier { get; }
    public SyntaxToken OpenParenToken { get; }
    public PatternSyntax ValuePattern { get; }
    public SyntaxToken CloseParenToken { get; }
    public override SyntaxKind Kind => SyntaxKind.OptionalSomePattern;
    public override IEnumerable<SyntaxNode> GetChildren() { yield return DotToken; yield return SomeIdentifier; yield return OpenParenToken; yield return ValuePattern; yield return CloseParenToken; }
    public override void Accept(SyntaxVisitor visitor) => visitor.VisitOptionalSomePattern(this);
    public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor) => visitor.VisitOptionalSomePattern(this);
}

public sealed class NilPatternSyntax : PatternSyntax
{
    public NilPatternSyntax(SyntaxToken nilKeyword) { NilKeyword = nilKeyword; AttachPatternChildren(nilKeyword); }
    public SyntaxToken NilKeyword { get; }
    public override SyntaxKind Kind => SyntaxKind.NilPattern;
    public override IEnumerable<SyntaxNode> GetChildren() { yield return NilKeyword; }
    public override void Accept(SyntaxVisitor visitor) => visitor.VisitNilPattern(this);
    public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor) => visitor.VisitNilPattern(this);
}

public sealed class ValueBindingPatternSyntax : PatternSyntax
{
    public ValueBindingPatternSyntax(SyntaxToken letKeyword, SyntaxToken identifier) { LetKeyword = letKeyword; Identifier = identifier; AttachPatternChildren(letKeyword, identifier); }
    public SyntaxToken LetKeyword { get; }
    public SyntaxToken Identifier { get; }
    public override SyntaxKind Kind => SyntaxKind.ValueBindingPattern;
    public override IEnumerable<SyntaxNode> GetChildren() { yield return LetKeyword; yield return Identifier; }
    public override void Accept(SyntaxVisitor visitor) => visitor.VisitValueBindingPattern(this);
    public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor) => visitor.VisitValueBindingPattern(this);
}
