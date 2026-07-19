namespace Martin.Compiler.Syntax;

public abstract class SyntaxVisitor
{
    public virtual void Visit(SyntaxNode? node) => node?.Accept(this);
    public virtual void DefaultVisit(SyntaxNode node) { foreach (var child in node.GetChildren()) Visit(child); }
    public virtual void VisitWildcardPattern(WildcardPatternSyntax node) => DefaultVisit(node);
    public virtual void VisitIdentifierPattern(IdentifierPatternSyntax node) => DefaultVisit(node);
    public virtual void VisitLiteralPattern(LiteralPatternSyntax node) => DefaultVisit(node);
    public virtual void VisitEnumCasePattern(EnumCasePatternSyntax node) => DefaultVisit(node);
    public virtual void VisitOptionalSomePattern(OptionalSomePatternSyntax node) => DefaultVisit(node);
    public virtual void VisitNilPattern(NilPatternSyntax node) => DefaultVisit(node);
    public virtual void VisitValueBindingPattern(ValueBindingPatternSyntax node) => DefaultVisit(node);
    public virtual void VisitPatternArgumentList(PatternArgumentListSyntax node) => DefaultVisit(node);
}

public abstract class SyntaxVisitor<TResult>
{
    public virtual TResult Visit(SyntaxNode node) => node.Accept(this);
    public abstract TResult DefaultVisit(SyntaxNode node);
    public virtual TResult VisitWildcardPattern(WildcardPatternSyntax node) => DefaultVisit(node);
    public virtual TResult VisitIdentifierPattern(IdentifierPatternSyntax node) => DefaultVisit(node);
    public virtual TResult VisitLiteralPattern(LiteralPatternSyntax node) => DefaultVisit(node);
    public virtual TResult VisitEnumCasePattern(EnumCasePatternSyntax node) => DefaultVisit(node);
    public virtual TResult VisitOptionalSomePattern(OptionalSomePatternSyntax node) => DefaultVisit(node);
    public virtual TResult VisitNilPattern(NilPatternSyntax node) => DefaultVisit(node);
    public virtual TResult VisitValueBindingPattern(ValueBindingPatternSyntax node) => DefaultVisit(node);
    public virtual TResult VisitPatternArgumentList(PatternArgumentListSyntax node) => DefaultVisit(node);
}

public class SyntaxRewriter : SyntaxVisitor<SyntaxNode>
{
    public override SyntaxNode DefaultVisit(SyntaxNode node) => node;
    public override SyntaxNode VisitWildcardPattern(WildcardPatternSyntax node) => new WildcardPatternSyntax((SyntaxToken)Visit(node.UnderscoreToken));
    public override SyntaxNode VisitIdentifierPattern(IdentifierPatternSyntax node) => new IdentifierPatternSyntax((SyntaxToken)Visit(node.Identifier));
    public override SyntaxNode VisitLiteralPattern(LiteralPatternSyntax node) => new LiteralPatternSyntax((SyntaxToken)Visit(node.LiteralToken));
    public override SyntaxNode VisitNilPattern(NilPatternSyntax node) => new NilPatternSyntax((SyntaxToken)Visit(node.NilKeyword));
    public override SyntaxNode VisitValueBindingPattern(ValueBindingPatternSyntax node) => new ValueBindingPatternSyntax((SyntaxToken)Visit(node.LetKeyword), (SyntaxToken)Visit(node.Identifier));
    public override SyntaxNode VisitOptionalSomePattern(OptionalSomePatternSyntax node) => new OptionalSomePatternSyntax((SyntaxToken)Visit(node.DotToken), (SyntaxToken)Visit(node.SomeIdentifier), (SyntaxToken)Visit(node.OpenParenToken), (PatternSyntax)Visit(node.ValuePattern), (SyntaxToken)Visit(node.CloseParenToken));
    public override SyntaxNode VisitEnumCasePattern(EnumCasePatternSyntax node) => new EnumCasePatternSyntax(node.Qualifier is null ? null : (SyntaxToken)Visit(node.Qualifier), node.DotToken is null ? null : (SyntaxToken)Visit(node.DotToken), (SyntaxToken)Visit(node.CaseName), node.Arguments is null ? null : (PatternArgumentListSyntax)Visit(node.Arguments));
    public override SyntaxNode VisitPatternArgumentList(PatternArgumentListSyntax node) => new PatternArgumentListSyntax((SyntaxToken)Visit(node.OpenParenToken), node.Arguments.Select(x => (PatternSyntax)Visit(x)).ToArray(), node.CommaTokens.Select(x => (SyntaxToken)Visit(x)).ToArray(), (SyntaxToken)Visit(node.CloseParenToken));
}
