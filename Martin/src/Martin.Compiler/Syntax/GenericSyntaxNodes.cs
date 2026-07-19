namespace Martin.Compiler.Syntax;

public sealed class GenericSyntaxNode : SyntaxNode
{
    public GenericSyntaxNode(SyntaxKind kind, params SyntaxNode[] children) { Kind = kind; Children = children; AttachChildren(children); }
    public override SyntaxKind Kind { get; }
    public IReadOnlyList<SyntaxNode> Children { get; }
    public override IEnumerable<SyntaxNode> GetChildren() => Children;
}

public sealed class GenericStatementSyntax : StatementSyntax
{
    public GenericStatementSyntax(SyntaxKind kind, params SyntaxNode[] children) { Kind = kind; Children = children; AttachChildren(children); }
    public override SyntaxKind Kind { get; }
    public IReadOnlyList<SyntaxNode> Children { get; }
    public override IEnumerable<SyntaxNode> GetChildren() => Children;
}

public sealed class GenericExpressionSyntax : ExpressionSyntax
{
    public GenericExpressionSyntax(SyntaxKind kind, params SyntaxNode[] children) { Kind = kind; Children = children; AttachChildren(children); }
    public override SyntaxKind Kind { get; }
    public IReadOnlyList<SyntaxNode> Children { get; }
    public override IEnumerable<SyntaxNode> GetChildren() => Children;
}
